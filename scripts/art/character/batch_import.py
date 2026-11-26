"""Batch import of generated GLBs (e.g. Hunyuan3D-2.1 output): dragons and static environment props.

  python scripts/art/character/batch_import.py <source_dir> <work_dir> --blender <blender.exe>
        [--unity-out Assets/Dravio/Character/Imported] [--overrides asset-overrides.json] [--force]
        [--qa-images]
  (or --bpy-module instead of --blender)

Overrides (asset-overrides.json, art director / GDD inputs; see asset-overrides.example.json):
  species.<id>.{limbCount: 0|2|4|"auto", yawOffset, height, landmarks}   per species
  files.<file stem or relative path>.{same keys}                         per file, wins over species
  "auto" accepts the mesh's own limb measurement only when it is unambiguous (rig_archetype.py).
  landmarks = JSON file (path relative to the overrides file) with manual head/neck/tail/feet points.
Approved assets: names listed in <unity-out>/approved-assets.json are never re-delivered (their FBX,
textures and prefab stay exactly as approved); the batch reports them as approved_kept.

Per file:
  1. classify the filename: prop keyword (propCategories) first, else a canonical species id,
     optional stage token Baby|Juvenile|Adult (absent -> "Unstaged"); anything else is "unrecognized"
  2. skip if SHA-256 of the source, the pipeline version and the overrides for that asset are
     unchanged since the last successful run (state in <work_dir>/batch-state.json)
  3. dragon: inspect -> prepare (UV/bake, pre-decimate to the LOD0 target) -> rig_archetype
     (archetype template + clips; skipped with a clear reason when canonical data lacks an input,
     e.g. limbCount, and the static asset is still produced) -> build_lods -> inspect FBX -> validate
     prop:   inspect -> prepare -> build_prop (pivot, LODs, collider, size metadata) -> inspect -> validate
     dragons with a rig also go through anim_qa.py (deformation, foot sliding, ground, loops, bounds...)
  4. copy deliverables to --unity-out/<Dragons|Environment>/... and write import-manifest.json there
Source GLBs are only read (hash recorded before/after). Report: <work_dir>/batch-report.{json,md}.
"""
import argparse
import hashlib
import json
import re
import shutil
import subprocess
import sys
import time
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[2]
BUDGETS = REPO / 'Assets/Dravio/Character/Data/character-budgets.json'
ARCHETYPES = REPO / 'Assets/Dravio/Character/Data/dragon-archetypes.json'
PIPELINE_VERSION = 'u1-char8-v2'  # CHAR.8: rig_geometry splits close paws, ignores tail-tip contacts, symmetric about the feet midline
PROFILES = REPO / 'Assets/Dravio/Character/Data/rig-profiles.json'
# Every script and data file whose change must re-process an asset (cache fingerprint).
PIPELINE_FILES = ['batch_import.py', 'char_common.py', 'inspect_character.py', 'prepare_character.py',
                  'rig_archetype.py', 'rig_geometry.py', 'anim_qa.py', 'build_lods.py', 'build_prop.py',
                  'validate_character.py']
STAGES = ['Baby', 'Juvenile', 'Adult']
SHIM = "import runpy, sys\nscript = sys.argv[1]\nsys.argv = ['blender', '--'] + sys.argv[2:]\nrunpy.run_path(script, run_name='__main__')\n"


def tokens(stem):
    return [t for t in re.split(r'[_\-\s.]+', stem.lower()) if t]


def classify(stem, species_ids, categories, aliases=None):
    toks = [(aliases or {}).get(t, t) for t in tokens(stem)]
    joined = ' '.join(toks)
    for c in categories:
        if any(k in toks for k in c['keywords']):
            prop = '_'.join(t for t in toks if t not in ('sample', 'prop', 'prp', 'env'))
            return {'kind': 'prop', 'category': c['category'], 'profile': c['profile'], 'collider': c['collider'],
                    'propId': prop or c['category']}
    hits = [s for s in species_ids if s in toks]
    if len(hits) > 1:
        return {'kind': 'unrecognized', 'reason': f'several species ids in name: {hits}'}
    if hits:
        stage = next((st for st in STAGES if st.lower() in toks), None)
        return {'kind': 'dragon', 'species': hits[0], 'stage': stage or 'Unstaged', 'stageFromName': stage is not None}
    return {'kind': 'unrecognized', 'reason': f'no species id or prop keyword in "{joined}"'}


def check_source_dir(src_dir):
    """Clear errors before any work: missing folder, not a folder, or no GLB/glTF inside."""
    if not src_dir.exists():
        raise SystemExit(f'Source folder not found: {src_dir}\n'
                         f'Check the path (e.g. B:\\Dravio\\dravio_16_dragons_3d_pack\\images) and that the drive is mounted.')
    if not src_dir.is_dir():
        raise SystemExit(f'Source path is not a folder: {src_dir}')
    files = sorted(p for p in src_dir.rglob('*') if p.is_file() and p.suffix.lower() in ('.glb', '.gltf'))
    if not files:
        others = sorted({p.suffix.lower() for p in src_dir.rglob('*') if p.is_file()})
        raise SystemExit(f'No .glb/.gltf models found in {src_dir} (searched sub-folders too). '
                         f'File types present: {others or "none"}.')
    return files


def pipeline_hash(extra_files=()):
    h = hashlib.sha256(PIPELINE_VERSION.encode())
    for f in [HERE / n for n in PIPELINE_FILES] + [BUDGETS, ARCHETYPES, PROFILES] + list(extra_files):
        if Path(f).exists():
            h.update(Path(f).name.encode())
            h.update(Path(f).read_bytes())
    return h.hexdigest()


def asset_overrides(overrides, info, rel, base_dir):
    """species.<id> then files.<stem or relative path> (file wins). Resolves the landmarks path."""
    if info['kind'] != 'dragon':
        return dict(overrides.get('props', {}).get(info.get('propId'), {}))
    ov = dict(overrides.get('species', {}).get(info['species'], {}))
    files = overrides.get('files', {})
    for key in (Path(rel).stem, rel):
        ov.update({k: v for k, v in files.get(key, {}).items() if not k.startswith('_')})
    if ov.get('landmarks'):
        p = Path(ov['landmarks'])
        ov['landmarks'] = str(p if p.is_absolute() else (base_dir / p).resolve())
    return {k: v for k, v in ov.items() if not k.startswith('_')}


def sha256(p):
    h = hashlib.sha256()
    with open(p, 'rb') as f:
        for chunk in iter(lambda: f.read(1 << 20), b''):
            h.update(chunk)
    return h.hexdigest()


class Runner:
    def __init__(self, a, work):
        self.a = a
        self.log = (work / 'batch.log').open('a', encoding='utf-8')
        if not a.blender:
            self.shim = work / '_bpy_shim.py'
            self.shim.write_text(SHIM, encoding='utf-8')

    def blender(self, script, *args):
        if self.a.blender:
            argv = [self.a.blender, '--factory-startup', '--background', '--python-exit-code', '1',
                    '--python', str(script), '--', *map(str, args)]
        else:
            argv = [sys.executable, str(self.shim), str(script), *map(str, args)]
        return self.run(argv)

    def python(self, script, *args):
        return self.run([sys.executable, str(script), *map(str, args)])

    def run(self, argv):
        t = time.time()
        p = subprocess.run(argv, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, cwd=REPO)
        self.log.write(f'\n$ {" ".join(argv)}\n{p.stdout}')
        self.log.flush()
        tail = [l for l in p.stdout.splitlines() if l.strip()][-3:]
        return p.returncode == 0, round(time.time() - t, 1), ' | '.join(tail)


def load(p):
    return json.loads(Path(p).read_text(encoding='utf-8'))


def process_dragon(r, src, info, out, ov, budgets, arch, qa_images=False):
    name = f"DRG_{info['species']}_{info['stage']}"
    d = out / 'assets' / name
    d.mkdir(parents=True, exist_ok=True)
    ow = '--overwrite-outputs'
    steps, notes = [], []

    def step(label, ok_t):
        ok, sec, tail = ok_t
        steps.append({'step': label, 'ok': ok, 'seconds': sec, **({} if ok else {'error': tail})})
        return ok

    if not step('inspect_source', r.blender(HERE / 'inspect_character.py', src, d / f'{name}.source.inspect.json', ow)):
        return steps, notes, None
    lod0 = next(l for l in budgets['lods'] if l['level'] == 0)
    if not step('prepare', r.blender(HERE / 'prepare_character.py', src, d, '--name', name,
                                     '--decimate-to', lod0['targetTriangles'], ow)):
        return steps, notes, None
    prepared = d / f'{name}_Prepared.glb'
    sp = next(s for s in arch['species'] if s['id'] == info['species'])
    rig_args = ['--species', info['species'], '--name', name, '--stage', info['stage']]
    for key, flag in (('limbCount', '--limb-count'), ('yawOffset', '--yaw-offset'), ('height', '--height'),
                      ('landmarks', '--landmarks')):
        if ov.get(key) is not None:
            rig_args += [flag, ov[key]]
    rigged = None
    missing = [k for k in sp['requiresInput'] if ov.get(k) is None]
    if missing:
        notes.append(f"Rig skipped: canonical data does not state {', '.join(missing)} for {info['species']}; "
                     f"set it in asset-overrides.json (art director/GDD), or \"limbCount\": \"auto\" to accept "
                     f"an unambiguous measurement of the mesh. Static LODs produced instead.")
    elif step('rig_archetype', r.blender(HERE / 'rig_archetype.py', prepared, d, *rig_args, ow)):
        rigged = load(d / f'{name}_rig.json')
        if rigged.get('limbCountSource', '').startswith('mesh measurement'):
            notes.append(f"limbCount {rigged['limbCount']} taken from the mesh measurement (--limb-count auto): "
                         f"{rigged.get('limbMeasurement')}. Confirm with the art director.")
        if rigged['bind'].get('nearest_bone_fallback_vertices'):
            notes.append(f"{rigged['bind']['nearest_bone_fallback_vertices']} vertices (floating fragments) bound rigidly to nearest bone.")
    elif not missing:
        err = steps[-1].get('error', '')
        notes.append(f'Rig failed, static LODs produced instead: {err}')
    lod_src = d / f'{name}_Rigged.blend' if rigged else prepared
    lod_args = [lod_src, d, '--name', name, ow]
    if rigged:
        lod_args += ['--frames'] + [str((c['start'] + c['end']) // 2) for c in rigged['clips'].values()]
    if not step('build_lods', r.blender(HERE / 'build_lods.py', *lod_args)):
        return steps, notes, None
    qa = None
    if rigged:
        qa_args = [d / f'{name}_Rigged.blend', d / f'{name}_rig.json', d, '--lods-json', d / f'{name}_lods.json', ow]
        if qa_images:
            qa_args.append('--images')
        if step('anim_qa', r.blender(HERE / 'anim_qa.py', *qa_args)):
            qa = load(d / f'{name}_animqa.json')
            if qa['warnings']:
                by = {}
                for w in qa['warnings']:
                    by.setdefault(w['clip'] or 'model', []).append(w['check'])
                notes.append('Animation QA warnings: ' + '; '.join(f'{k}: {", ".join(sorted(set(v)))}' for k, v in by.items()))
    if not step('inspect_lods', r.blender(HERE / 'inspect_character.py', d / f'{name}_LODs.fbx', d / f'{name}_LODs.inspect.json', ow)):
        return steps, notes, None
    ok, sec, tail = r.python(HERE / 'validate_character.py', '--inspect', d / f'{name}_LODs.inspect.json',
                             '--lods', d / f'{name}_lods.json', '--prepare', d / f'{name}_prepare.json',
                             '--out', d / f'{name}.validation.json')
    steps.append({'step': 'validate', 'ok': True, 'seconds': sec, 'exit': 0 if ok else 1})
    val = load(d / f'{name}.validation.json')
    clip_list = [{'name': k, 'start': v['start'], 'end': v['end'], 'loop': v['loop'],
                  'groundSpeed': v.get('groundSpeed', 0.0)} for k, v in rigged['clips'].items()] if rigged else []
    entry = {'kind': 'dragon', 'name': name, 'species': info['species'], 'stage': info['stage'],
             'archetype': rigged['archetype'] if rigged else sp['archetype'], 'rigged': bool(rigged),
             'rigVersion': rigged.get('rigVersion') if rigged else None,
             'clips': rigged['clips'] if rigged else {}, 'clipList': clip_list,
             'fps': rigged['fps'] if rigged else 30, 'deformationOk': rigged['deformationOk'] if rigged else None,
             'ikChains': rigged['ikChains'] if rigged else [],
             'anchors': rigged['anchors'] if rigged else [], 'validation': val['status'],
             'animQa': {'status': qa['status'], 'warnings': [f"{w['clip'] or 'model'}: {w['check']} {w['message']} "
                                                             f"({w['value']} vs {w['limit']})" for w in qa['warnings']]}
             if qa else None,
             'motion': motion_block(rigged, qa),
             'files': {'fbx': f'{name}_LODs.fbx'}}
    return steps, notes, (d, entry)


def motion_block(rigged, qa):
    """U1-CHAR.7 motion metadata for the Unity catalog, flat (JsonUtility reads no dictionaries); 0 = not
    applicable / not measured. Sources: rig.json motion (body size, root offsets, turn limits) and the animation
    QA (motion envelope per medium, authored transitions)."""
    if not rigged:
        return None
    m = rigged.get('motion') or {}
    body, off, turn = m.get('body', {}), m.get('rootOffsets_m', {}), m.get('maxTurnRateDegPerSec', {})
    env = (qa or {}).get('motionEnvelope') or {}
    ev, wl = m.get('events') or {}, m.get('water') or {}
    tr = (qa or {}).get('transitions') or []
    f = lambda v: float(v) if v is not None else 0.0  # noqa: E731
    return {
        'bodyLength': f(body.get('length_m')), 'bodyWidth': f(body.get('width_m')), 'bodyHeight': f(body.get('height_m')),
        'takeOffRootOffset': f(off.get('TakeOff.end')), 'landRootOffset': f(off.get('Land.start')),
        'maxTurnGround': f(turn.get('ground')), 'maxTurnWater': f(turn.get('water')), 'maxTurnAir': f(turn.get('air')),
        'groundHeight': f(env.get('groundHeight_m')), 'groundLength': f(env.get('groundLength_m')), 'groundWidth': f(env.get('groundWidth_m')),
        'swimVerticalExtent': f(env.get('swimVerticalExtent_m')), 'swimWidth': f(env.get('swimWidth_m')),
        'flightSpan': f(env.get('flightSpan_m')), 'flightVerticalExtent': f(env.get('flightVerticalExtent_m')),
        'transitionsChecked': len(tr), 'transitionMaxDeg': max((t.get('poseErrorDeg', 0.0) for t in tr), default=0.0),
        # U1-CHAR.8: normalized event times inside the transition clips (VFX/audio cues) and the surface waterline
        'takeOffLiftOff': f(ev.get('TakeOff.liftOff')), 'landTouchDown': f(ev.get('Land.touchDown')),
        'waterEnterContact': f(ev.get('WaterEnter.contact')), 'waterExitLeave': f(ev.get('WaterExit.leave')),
        'surfaceWaterline': f(wl.get('surfaceWaterline_m')), 'headAboveWater': f(wl.get('headAboveWater_m')),
    }


def process_prop(r, src, info, out, ov):
    name = f"PRP_{info['propId']}"
    d = out / 'assets' / name
    d.mkdir(parents=True, exist_ok=True)
    ow = '--overwrite-outputs'
    steps, notes = [], []

    def step(label, ok_t):
        ok, sec, tail = ok_t
        steps.append({'step': label, 'ok': ok, 'seconds': sec, **({} if ok else {'error': tail})})
        return ok

    if not step('inspect_source', r.blender(HERE / 'inspect_character.py', src, d / f'{name}.source.inspect.json', ow)):
        return steps, notes, None
    if not step('prepare', r.blender(HERE / 'prepare_character.py', src, d, '--name', name, ow)):
        return steps, notes, None
    args = [d / f'{name}_Prepared.glb', d, '--category', info['category'], '--name', name, ow]
    h = ov.get('height')
    if h:
        args += ['--height', h]
    else:
        notes.append('Real-world size not given; kept source units (set props.<id>.height in asset-overrides.json).')
    if not step('build_prop', r.blender(HERE / 'build_prop.py', *args)):
        return steps, notes, None
    if not step('inspect_prop', r.blender(HERE / 'inspect_character.py', d / f'{name}_Prop.fbx', d / f'{name}_Prop.inspect.json', ow)):
        return steps, notes, None
    ok, sec, tail = r.python(HERE / 'validate_character.py', '--inspect', d / f'{name}_Prop.inspect.json',
                             '--lod-profile', info['profile'], '--prepare', d / f'{name}_prepare.json',
                             '--out', d / f'{name}.validation.json')
    steps.append({'step': 'validate', 'ok': True, 'seconds': sec, 'exit': 0 if ok else 1})
    val = load(d / f'{name}.validation.json')
    meta = load(d / f'{name}_prop.json')
    notes += meta['warnings']
    entry = {'kind': 'prop', 'name': name, 'propId': info['propId'], 'category': info['category'],
             'profile': info['profile'], 'collider': meta['colliderData'], 'size': meta['size'],
             'sizeConfirmed': meta['scale']['source'] == '--height',
             'validation': val['status'], 'files': {'fbx': f'{name}_Prop.fbx'}}
    return steps, notes, (d, entry)


def deliver(d, entry, unity_out):
    """Copy only what Unity needs; never into an existing different file without the manifest knowing."""
    sub = (Path('Dragons') / entry['species'] / entry['stage']) if entry['kind'] == 'dragon' else Path('Environment') / entry['category']
    dest = unity_out / sub
    dest.mkdir(parents=True, exist_ok=True)
    copied = []
    patterns = ['*_LODs.fbx', '*_rig.json', '*_BaseColor.png', '*_Normal.png', '*_MetallicSmoothness.png',
                '*.validation.json', '*_animqa.json', '*_animqa.png'] if entry['kind'] == 'dragon' else \
               ['*_Prop.fbx', '*_prop.json', '*_BaseColor.png', '*_Normal.png', '*_MetallicSmoothness.png', '*.validation.json']
    for pat in patterns:
        for f in d.glob(pat):
            shutil.copy2(f, dest / f.name)
            copied.append(str((sub / f.name).as_posix()))
    entry['unityFolder'] = sub.as_posix()
    entry['delivered'] = copied
    return entry


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('source_dir', type=Path)
    ap.add_argument('work_dir', type=Path)
    g = ap.add_mutually_exclusive_group(required=True)
    g.add_argument('--blender')
    g.add_argument('--bpy-module', action='store_true')
    ap.add_argument('--unity-out', type=Path, help='e.g. Assets/Dravio/Character/Imported')
    ap.add_argument('--overrides', type=Path, help='asset-overrides.json (art director/GDD inputs)')
    ap.add_argument('--force', action='store_true', help='Reprocess even if hashes are unchanged')
    ap.add_argument('--qa-images', action='store_true', help='Render the animation QA contact sheet per dragon')
    a = ap.parse_args(argv)
    src_dir = a.source_dir.resolve()
    files = check_source_dir(src_dir)
    work = a.work_dir.resolve()
    work.mkdir(parents=True, exist_ok=True)
    if a.unity_out and src_dir in a.unity_out.resolve().parents:
        raise SystemExit('--unity-out must not be inside the source folder')
    budgets = load(BUDGETS)
    arch = load(ARCHETYPES)
    species_ids = [s['id'] for s in arch['species']]
    categories = budgets['propCategories']['categories']
    overrides = load(a.overrides) if a.overrides else {}
    ov_dir = a.overrides.resolve().parent if a.overrides else REPO
    aliases = load(PROFILES).get('fileNameAliases', {})
    aliases = {k: v for k, v in aliases.items() if not k.startswith('_')}
    pipe = pipeline_hash()
    approved = set()
    if a.unity_out and (a.unity_out / 'approved-assets.json').exists():
        approved = set(load(a.unity_out / 'approved-assets.json').get('approved', []))
    state_path = work / 'batch-state.json'
    state = load(state_path) if state_path.exists() else {}
    r = Runner(a, work)
    results = []
    manifest_entries = []
    for src in files:
        rel = src.relative_to(src_dir).as_posix()
        info = classify(src.stem, species_ids, categories, aliases)
        h = sha256(src)
        ov = asset_overrides(overrides, info, rel, ov_dir) if info['kind'] != 'unrecognized' else {}
        ov_key = json.dumps(ov, sort_keys=True)
        lm_hash = sha256(ov['landmarks']) if ov.get('landmarks') and Path(ov['landmarks']).exists() else ''
        # source, pipeline scripts + data (rig, animation, QA, config), overrides and landmark file
        fingerprint = hashlib.sha256(f'{h}|{pipe}|{ov_key}|{lm_hash}'.encode()).hexdigest()
        res = {'file': rel, 'sha256': h, **info}
        if info['kind'] == 'unrecognized':
            res['status'] = 'unrecognized'
            results.append(res)
            continue
        prev = state.get(rel)
        if prev and prev.get('fingerprint') == fingerprint and prev.get('status') in ('ok', 'ok_with_notes') and not a.force:
            res.update(status='unchanged', previous=prev.get('status'))
            if prev.get('manifest'):
                manifest_entries.append(prev['manifest'])
            results.append(res)
            print(f'= {rel}: unchanged, skipped')
            continue
        print(f'> {rel}: {info["kind"]} {info.get("species") or info.get("category")}')
        out = work
        if info['kind'] == 'dragon':
            steps, notes, done = process_dragon(r, src, info, out, ov, budgets, arch, a.qa_images)
        else:
            steps, notes, done = process_prop(r, src, info, out, ov)
        if sha256(src) != h:
            raise SystemExit(f'Source changed during processing: {src}')
        res.update(steps=steps, notes=notes)
        if done:
            d, entry = done
            if entry['name'] in approved:
                # approved by the art director: keep the delivered files and prefab exactly as they are
                prev_entry = (prev or {}).get('manifest')
                notes.append('Approved asset: new output kept in the work folder only, Unity files not touched '
                             '(remove it from approved-assets.json to re-deliver).')
                entry = dict(prev_entry or entry, approved=True)
            elif a.unity_out:
                entry = deliver(d, entry, a.unity_out)
            manifest_entries.append(entry)
            if entry['validation'] == 'FAIL':
                res['status'] = 'validation_failed'
                v = load(d / f"{entry['name']}.validation.json")
                notes += [i['message'] for i in v['items'] if i['level'] == 'error']
            else:
                res['status'] = 'ok_with_notes' if notes else 'ok'
            res['validation'] = entry['validation']
            res['rigged'] = entry.get('rigged')
            state[rel] = {'fingerprint': fingerprint, 'status': res['status'], 'manifest': entry}
        else:
            res['status'] = 'failed'
            state[rel] = {'fingerprint': fingerprint, 'status': 'failed'}
        results.append(res)
        print(f"  -> {res['status']}" + (f": {notes[0]}" if notes else ''))
    state_path.write_text(json.dumps(state, indent=2, ensure_ascii=False), encoding='utf-8')
    # Coverage: which species x stage and prop categories exist.
    have = {(e['species'], e['stage']) for e in manifest_entries if e['kind'] == 'dragon'}
    have_species = {s for s, _ in have}
    have_cats = {e['category'] for e in manifest_entries if e['kind'] == 'prop'}
    report = {
        'pipelineVersion': PIPELINE_VERSION, 'source_dir': str(src_dir), 'files': len(files),
        'counts': {k: sum(1 for x in results if x['status'] == k)
                   for k in ('ok', 'ok_with_notes', 'validation_failed', 'failed', 'unchanged', 'unrecognized')},
        'approvedKept': sorted(approved & {e['name'] for e in manifest_entries}),
        'results': results,
        'missing': {
            'speciesWithoutAnyModel': [s for s in species_ids if s not in have_species],
            'speciesStagesWithoutModel': [f'{s}/{st}' for s in species_ids for st in STAGES if (s, st) not in have],
            'propCategoriesWithoutModel': [c['category'] for c in categories if c['category'] not in have_cats],
            'rigInputsRequired': sorted({f"{x['species']}: {', '.join(s['requiresInput'])}"
                                         for x in results if x.get('kind') == 'dragon'
                                         and any(n.startswith('Rig skipped') for n in x.get('notes', []))
                                         for s in arch['species'] if s['id'] == x['species']}),
        },
        'note': 'Structural pipeline results only; Unity import, visuals, animation quality and device performance are separate checks.',
    }
    (work / 'batch-report.json').write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding='utf-8')
    (work / 'batch-report.md').write_text(to_md(report), encoding='utf-8')
    if a.unity_out:
        a.unity_out.mkdir(parents=True, exist_ok=True)
        (a.unity_out / 'import-manifest.json').write_text(json.dumps(
            {'pipelineVersion': PIPELINE_VERSION, 'entries': manifest_entries}, indent=2, ensure_ascii=False), encoding='utf-8')
    print(json.dumps(report['counts']))
    return 1 if report['counts']['failed'] or report['counts']['validation_failed'] else 0


def to_md(rep):
    L = ['# Báo cáo batch import', '', f"Nguồn: `{rep['source_dir']}` — {rep['files']} file GLB. Pipeline {rep['pipelineVersion']}.", '',
         '| Trạng thái | Số file |', '|---|---|']
    L += [f'| {k} | {v} |' for k, v in rep['counts'].items()]
    if rep.get('approvedKept'):
        L += ['', f"Đã duyệt, giữ nguyên trong Unity (approved-assets.json): {', '.join(rep['approvedKept'])}"]
    L += ['', '| File | Loại | Species/Category | Stage | Trạng thái | Rig | Kiểm định | Ghi chú |', '|---|---|---|---|---|---|---|---|']
    for x in rep['results']:
        notes = '; '.join(x.get('notes', [])) or x.get('reason', '')
        err = next((s.get('error') for s in x.get('steps', []) if not s['ok']), None)
        L.append(f"| `{x['file']}` | {x['kind']} | {x.get('species') or x.get('category') or ''} | {x.get('stage', '')} | "
                 f"{x['status']} | {'có' if x.get('rigged') else ('không' if x['kind'] == 'dragon' else '')} | "
                 f"{x.get('validation', '')} | {(notes + (' ERROR: ' + err if err else '')).replace('|', '/')} |")
    m = rep['missing']
    L += ['', '## Còn thiếu', '',
          f"- Loài chưa có mô hình nào ({len(m['speciesWithoutAnyModel'])}): {', '.join(m['speciesWithoutAnyModel']) or '—'}",
          f"- Cặp loài/giai đoạn chưa có: {len(m['speciesStagesWithoutModel'])} / 48",
          f"- Nhóm tài nguyên môi trường chưa có: {', '.join(m['propCategoriesWithoutModel']) or '—'}",
          f"- Cần dữ liệu từ art director/GDD để rig: {'; '.join(m['rigInputsRequired']) or '—'}", '',
          rep['note']]
    return '\n'.join(L) + '\n'


if __name__ == '__main__':
    sys.exit(main())
