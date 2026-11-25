"""Batch import of generated GLBs (e.g. Hunyuan3D-2.1 output): dragons and static environment props.

  python scripts/art/character/batch_import.py <source_dir> <work_dir> --blender <blender.exe>
        [--unity-out Assets/Dravio/Character/Imported] [--overrides asset-overrides.json] [--force]
  (or --bpy-module instead of --blender)

Per file:
  1. classify the filename: prop keyword (propCategories) first, else a canonical species id,
     optional stage token Baby|Juvenile|Adult (absent -> "Unstaged"); anything else is "unrecognized"
  2. skip if SHA-256 of the source, the pipeline version and the overrides for that asset are
     unchanged since the last successful run (state in <work_dir>/batch-state.json)
  3. dragon: inspect -> prepare (UV/bake, pre-decimate to the LOD0 target) -> rig_archetype
     (archetype template + clips; skipped with a clear reason when canonical data lacks an input,
     e.g. limbCount, and the static asset is still produced) -> build_lods -> inspect FBX -> validate
     prop:   inspect -> prepare -> build_prop (pivot, LODs, collider, size metadata) -> inspect -> validate
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
PIPELINE_VERSION = 'u1-char3-v2'
STAGES = ['Baby', 'Juvenile', 'Adult']
SHIM = "import runpy, sys\nscript = sys.argv[1]\nsys.argv = ['blender', '--'] + sys.argv[2:]\nrunpy.run_path(script, run_name='__main__')\n"


def tokens(stem):
    return [t for t in re.split(r'[_\-\s.]+', stem.lower()) if t]


def classify(stem, species_ids, categories):
    toks = tokens(stem)
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


def process_dragon(r, src, info, out, overrides, budgets, arch):
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
    ov = overrides.get('species', {}).get(info['species'], {})
    rig_args = ['--species', info['species'], '--name', name, '--stage', info['stage']]
    if ov.get('limbCount') is not None:
        rig_args += ['--limb-count', ov['limbCount']]
    if ov.get('yawOffset') is not None:
        rig_args += ['--yaw-offset', ov['yawOffset']]
    rigged = None
    missing = [k for k in sp['requiresInput'] if ov.get(k) is None]
    if missing:
        notes.append(f"Rig skipped: canonical data does not state {', '.join(missing)} for {info['species']}; "
                     f"add it to asset-overrides.json (art director/GDD). Static LODs produced instead.")
    elif step('rig_archetype', r.blender(HERE / 'rig_archetype.py', prepared, d, *rig_args, ow)):
        rigged = load(d / f'{name}_rig.json')
        if not rigged['deformationOk']:
            bad = [k for k, v in rigged['deformation'].items() if not v['ok']]
            notes.append(f'Deformation check flagged clips {bad} (edge stretch/compress limits); review in Blender.')
        if rigged['bind'].get('nearest_bone_fallback_vertices'):
            notes.append(f"{rigged['bind']['nearest_bone_fallback_vertices']} vertices (floating fragments) bound rigidly to nearest bone.")
    lod_src = d / f'{name}_Rigged.blend' if rigged else prepared
    lod_args = [lod_src, d, '--name', name, ow]
    if rigged:
        lod_args += ['--frames'] + [str((c['start'] + c['end']) // 2) for c in rigged['clips'].values()]
    if not step('build_lods', r.blender(HERE / 'build_lods.py', *lod_args)):
        return steps, notes, None
    if not step('inspect_lods', r.blender(HERE / 'inspect_character.py', d / f'{name}_LODs.fbx', d / f'{name}_LODs.inspect.json', ow)):
        return steps, notes, None
    ok, sec, tail = r.python(HERE / 'validate_character.py', '--inspect', d / f'{name}_LODs.inspect.json',
                             '--lods', d / f'{name}_lods.json', '--prepare', d / f'{name}_prepare.json',
                             '--out', d / f'{name}.validation.json')
    steps.append({'step': 'validate', 'ok': True, 'seconds': sec, 'exit': 0 if ok else 1})
    val = load(d / f'{name}.validation.json')
    entry = {'kind': 'dragon', 'name': name, 'species': info['species'], 'stage': info['stage'],
             'archetype': rigged['archetype'] if rigged else sp['archetype'], 'rigged': bool(rigged),
             'clips': rigged['clips'] if rigged else {},
             'clipList': [{'name': k, **v} for k, v in rigged['clips'].items()] if rigged else [],
             'fps': rigged['fps'] if rigged else 30, 'deformationOk': rigged['deformationOk'] if rigged else None,
             'ikChains': rigged['ikChains'] if rigged else [],
             'anchors': rigged['anchors'] if rigged else [], 'validation': val['status'],
             'files': {'fbx': f'{name}_LODs.fbx'}}
    return steps, notes, (d, entry)


def process_prop(r, src, info, out, overrides):
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
    h = overrides.get('props', {}).get(info['propId'], {}).get('height')
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
                '*.validation.json'] if entry['kind'] == 'dragon' else \
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
    a = ap.parse_args(argv)
    src_dir = a.source_dir.resolve()
    work = a.work_dir.resolve()
    work.mkdir(parents=True, exist_ok=True)
    if a.unity_out and src_dir in a.unity_out.resolve().parents:
        raise SystemExit('--unity-out must not be inside the source folder')
    budgets = load(BUDGETS)
    arch = load(ARCHETYPES)
    species_ids = [s['id'] for s in arch['species']]
    categories = budgets['propCategories']['categories']
    overrides = load(a.overrides) if a.overrides else {}
    state_path = work / 'batch-state.json'
    state = load(state_path) if state_path.exists() else {}
    r = Runner(a, work)
    results = []
    manifest_entries = []
    files = sorted(p for p in src_dir.rglob('*') if p.suffix.lower() in ('.glb', '.gltf'))
    for src in files:
        rel = src.relative_to(src_dir).as_posix()
        info = classify(src.stem, species_ids, categories)
        h = sha256(src)
        ov_key = json.dumps(overrides.get('species', {}).get(info.get('species'), {}) if info['kind'] == 'dragon'
                            else overrides.get('props', {}).get(info.get('propId'), {}), sort_keys=True)
        fingerprint = hashlib.sha256(f'{h}|{PIPELINE_VERSION}|{ov_key}'.encode()).hexdigest()
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
            steps, notes, done = process_dragon(r, src, info, out, overrides, budgets, arch)
        else:
            steps, notes, done = process_prop(r, src, info, out, overrides)
        if sha256(src) != h:
            raise SystemExit(f'Source changed during processing: {src}')
        res.update(steps=steps, notes=notes)
        if done:
            d, entry = done
            if a.unity_out:
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
        'results': results,
        'missing': {
            'speciesWithoutAnyModel': [s for s in species_ids if s not in have_species],
            'speciesStagesWithoutModel': [f'{s}/{st}' for s in species_ids for st in STAGES if (s, st) not in have],
            'propCategoriesWithoutModel': [c['category'] for c in categories if c['category'] not in have_cats],
            'rigInputsRequired': sorted({f"{x['species']}: {', '.join(s['requiresInput'])}"
                                         for x in results if x.get('kind') == 'dragon'
                                         for s in arch['species'] if s['id'] == x['species'] and s['requiresInput']
                                         and not all(overrides.get('species', {}).get(x['species'], {}).get(k) is not None
                                                     for k in s['requiresInput'])}),
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
