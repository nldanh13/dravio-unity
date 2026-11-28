"""CHAR.9c — propose a per-species `repair` config (asset-overrides species.<id>.repair) by measurement, never by
loosening a limit. Works on a copy of the UNREPAIRED rig v7 output of one dragon (`<Name>_Rigged.blend` +
`<Name>_rig.json`, e.g. from a batch_import run without `repair`); the source GLB is never read or written.

  python scripts/art/character/tune_repair.py <work>/assets/DRG_<sp>_<stage> <out_dir> --bpy-python <python with bpy>
        [--start '{"skinning":"linear","seamWeld":true,"blendWidth_m":0.24,"pinTransitions":true}'] [--rounds 12] [--step 0.85] [--floor 0.5]

Each round: repair_chibi_rig.py (copy of the base) -> anim_qa.py --samples all --strict-frame (every frame, worst
frame against the unchanged limits, seam cracks). For every clip still over a deformation limit, the bone group
that carries most of the bad edges (anim_qa `worstBones`) has that clip's rotation amplitude multiplied by `step`:
  foreLeg/hindLeg -> legs   head/neck -> head   tail -> tail   wing -> wings   fin -> fins   Spine*/Chest -> body
  Pelvis (root of the pose, never scaled) -> the group of the next listed bone. The two top groups are reduced each round.
A group already at `floor` is not reduced further: the clip is reported as NEEDS_ART (authored motion or
weights must change; the tool will not flatten an animation to pass). Ground/foot/bone warnings are reported,
not tuned. Output: <out_dir>/tune_<name>.json with every round, the best config and what is left.
--local (CHAR.9c step 4): keeps the --start config and instead adds repair.local pull-backs (that group's rotation
toward rest, Gaussian in time around the clip's worst frame, step 0.8, floor 0.3); `proposed` drops the entries of
clips that still fail, so nothing is flattened without a pass.
"""
import argparse
import json
import shutil
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[2]
SHIM = "import runpy, sys\nscript = sys.argv[1]\nsys.argv = ['blender', '--'] + sys.argv[2:]\nrunpy.run_path(script, run_name='__main__')\n"
REGION = {'foreLeg': 'legs', 'hindLeg': 'legs', 'leg': 'legs', 'head': 'head', 'neck': 'head', 'tail': 'tail',
          'wing': 'wings', 'fin': 'fins'}


def group_of(bone, region):
    if bone in ('Root', 'Pelvis'):
        return None
    if bone == 'Chest' or bone.startswith('Spine'):
        return 'body'
    return REGION.get(region)


def target_groups(worst, n=2):
    out = []
    for b in worst:
        g = group_of(b['bone'], b['region'])
        if g and g not in out:
            out.append(g)
    return out[:n]


def run(argv, log):
    p = subprocess.run(argv, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, cwd=REPO)
    log.write('$ ' + ' '.join(map(str, argv)) + '\n' + p.stdout + '\n')
    if p.returncode:
        raise SystemExit('failed: %s\n%s' % (argv[2], p.stdout[-2000:]))


def measure(base, name, cfg, rd, py, shim, log):
    if rd.exists():
        shutil.rmtree(rd)
    rd.mkdir(parents=True)
    blend, rig = rd / f'{name}_Rigged.blend', rd / f'{name}_rig.json'
    shutil.copy(base / f'{name}_Rigged.blend', blend)
    shutil.copy(base / f'{name}_rig.json', rig)
    run([py, shim, HERE / 'repair_chibi_rig.py', blend, rig, '--config', json.dumps(cfg)], log)
    run([py, shim, HERE / 'anim_qa.py', blend, rig, rd / 'qa', '--samples', 'all', '--strict-frame', '--overwrite-outputs'], log)
    return json.loads((rd / 'qa' / f'{name}_animqa.json').read_text(encoding='utf-8'))


def summary(qa):
    lim = qa['limits']
    out = {}
    for n, c in qa['clips'].items():
        d = c['deformation']
        s, k = d.get('worstFrameStretchP999', d['stretchP999']), d.get('worstFrameCompressP001', d['compressP001'])
        out[n] = {'stretch': s, 'compress': k, 'fail': s > lim['maxStretchP999'] or k < lim['minCompressP001'],
                  'worstBones': d.get('worstBones', [])[:4], 'worstFrame': d.get('worstFrame'), 'frames': c['frames']}
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('base'); ap.add_argument('out')
    ap.add_argument('--bpy-python', default=sys.executable)
    ap.add_argument('--start', default='{"skinning": "linear", "seamWeld": true, "blendWidth_m": 0.24, "pinTransitions": true}')
    ap.add_argument('--rounds', type=int, default=12)
    ap.add_argument('--step', type=float, default=0.85)
    ap.add_argument('--floor', type=float, default=0.5)
    ap.add_argument('--keep-rounds', action='store_true')
    ap.add_argument('--local', action='store_true',
                    help='keep the --start amplitude and add LOCAL pull-backs (repair.local) around the worst frame of '
                         'each failing clip instead; entries of clips that still fail are removed from the result')
    ap.add_argument('--local-step', type=float, default=0.8)
    ap.add_argument('--local-floor', type=float, default=0.3)
    ap.add_argument('--guard', action='store_true',
                    help='a group reduction that gives its clip a NEW ground/foot/transition/wing warning is undone '
                         'and that group is never reduced again on that clip (amplitude mode)')
    ap.add_argument('--local-width', type=float, default=0.12, help='Gaussian half width, fraction of the clip')
    a = ap.parse_args()
    base, out = Path(a.base).resolve(), Path(a.out).resolve()
    name = base.name
    out.mkdir(parents=True, exist_ok=True)
    shim = out / '_bpy_shim.py'
    shim.write_text(SHIM, encoding='utf-8')
    log = (out / f'tune_{name}.log').open('w', encoding='utf-8')
    cfg = json.loads(a.start)
    cfg.setdefault('amplitude', {})
    history, best, needs_art = [], None, {}
    base_warn, last_change, blocked = None, {}, {}

    def clip_warn(qa):
        out = {}
        for w in qa['warnings']:
            if w['check'] != 'deformation' and w['clip']:
                out.setdefault(w['clip'], set()).add(w['check'])
        return out
    for rnd in range(a.rounds + 1):
        rd = out / f'{name}_r{rnd}'
        qa = measure(base, name, cfg, rd, a.bpy_python, shim, log)
        s = summary(qa)
        fails = sorted(n for n, v in s.items() if v['fail'])
        other = sorted({(w['clip'] or '-') + ':' + w['check'] for w in qa['warnings'] if w['check'] != 'deformation'})
        history.append({'round': rnd, 'config': json.loads(json.dumps(cfg)), 'failingClips': fails, 'otherWarnings': other,
                        'clips': {n: {'stretch': v['stretch'], 'compress': v['compress']} for n, v in s.items()}})
        cw = clip_warn(qa)
        if base_warn is None:
            base_warn = cw
        new_warn = sorted(n for n in cw if cw[n] - base_warn.get(n, set()))
        history[-1]['newWarningClips'] = new_warn
        print(f'{name} r{rnd}: {len(fails)}/{len(s)} clips over limit {fails} new warnings {new_warn}', flush=True)
        score = len(set(fails) | set(new_warn))
        if best is None or score < best['score']:
            history[-1]['score'] = score
            best = history[-1]
            best['score'] = score
            if (out / f'{name}_best').exists():
                shutil.rmtree(out / f'{name}_best')
            shutil.copytree(rd / 'qa', out / f'{name}_best')
        if not a.keep_rounds and rnd:
            shutil.rmtree(out / f'{name}_r{rnd - 1}', ignore_errors=True)
        changed = False
        if a.local:
            loc = cfg.setdefault('local', {})
            for n in fails:
                f0, f1 = s[n]['frames']; wf = s[n]['worstFrame']
                hw = max(2.0, round(a.local_width * (f1 - f0), 1))
                gs = target_groups(s[n]['worstBones'])
                moved = False
                for g in gs:
                    es = [e for e in loc.setdefault(n, []) if e[0] == g and abs(e[1] - wf) <= hw]
                    if es:
                        e = es[0]
                        if e[2] > a.local_floor + 1e-9:
                            e[2] = round(max(a.local_floor, e[2] * a.local_step), 3); moved = True
                    else:
                        loc[n].append([g, int(wf), a.local_step, hw]); moved = True
                if moved:
                    changed = True; needs_art.pop(n, None)
                else:
                    needs_art[n] = {'groups': gs, 'stretch': s[n]['stretch'], 'compress': s[n]['compress'],
                                    'worstFrame': wf, 'worstBones': s[n]['worstBones']}
            cfg['local'] = {k: v for k, v in loc.items() if v}
            if not fails or not changed:
                break
            continue
        if a.guard:
            for n in new_warn:
                for g, old in last_change.get(n, []):
                    cur = cfg['amplitude'].setdefault(n, {})
                    if old == 1.0: cur.pop(g, None)
                    else: cur[g] = old
                    blocked.setdefault(n, set()).add(g); changed = True
            last_change = {}
        for n in fails:
            if n in new_warn and a.guard:
                continue  # reverted this round; measure again first
            cur = cfg['amplitude'].setdefault(n, {})
            gs = [g for g in target_groups(s[n]['worstBones'], n=4) if cur.get(g, 1.0) > a.floor + 1e-9
                  and g not in blocked.get(n, ())][:2]
            if not gs:
                needs_art[n] = {'groups': target_groups(s[n]['worstBones']), 'stretch': s[n]['stretch'],
                                'compress': s[n]['compress'], 'worstBones': s[n]['worstBones']}
                continue
            for g in gs:
                last_change.setdefault(n, []).append((g, cur.get(g, 1.0)))
                cur[g] = round(max(a.floor, cur.get(g, 1.0) * a.step), 3)
            needs_art.pop(n, None)
            changed = True
        cfg['amplitude'] = {k: v for k, v in cfg['amplitude'].items() if v}
        if not fails or not changed:
            break
    if not a.keep_rounds:
        shutil.rmtree(out / f'{name}_r{history[-1]["round"]}', ignore_errors=True)
    res = {'name': name, 'species': qa['species'], 'archetype': qa['archetype'], 'height_m': qa['height'],
           'limits': qa['limits'], 'step': a.step, 'floor': a.floor,
           'best': best, 'final': history[-1],
           'needsArt': {n: v for n, v in needs_art.items() if n in history[-1]['failingClips']},
           'status': 'PASS' if not best['failingClips'] and not best['otherWarnings'] else
                     ('DEFORMATION_PASS' if not best['failingClips'] else 'NEEDS_ART'),
           'blocked': {k: sorted(v) for k, v in blocked.items()}, 'history': history}
    if res['best']['config'].get('amplitude') == {}:
        res['best']['config'].pop('amplitude')
    if a.local:  # no pull-back is kept on a clip it did not fix
        loc = {k: v for k, v in res['best']['config'].get('local', {}).items() if k not in res['best']['failingClips']}
        res['proposed'] = dict(res['best']['config'], local=loc) if loc else {k: v for k, v in res['best']['config'].items() if k != 'local'}
    (out / f'tune_{name}.json').write_text(json.dumps(res, indent=1), encoding='utf-8')
    print(json.dumps({'name': name, 'status': res['status'], 'failing': best['failingClips'],
                      'other': best['otherWarnings']}))


if __name__ == '__main__':
    main()
