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
                  'worstBones': d.get('worstBones', [])[:4]}
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
    for rnd in range(a.rounds + 1):
        rd = out / f'{name}_r{rnd}'
        qa = measure(base, name, cfg, rd, a.bpy_python, shim, log)
        s = summary(qa)
        fails = sorted(n for n, v in s.items() if v['fail'])
        other = sorted({(w['clip'] or '-') + ':' + w['check'] for w in qa['warnings'] if w['check'] != 'deformation'})
        history.append({'round': rnd, 'config': json.loads(json.dumps(cfg)), 'failingClips': fails, 'otherWarnings': other,
                        'clips': {n: {'stretch': v['stretch'], 'compress': v['compress']} for n, v in s.items()}})
        print(f'{name} r{rnd}: {len(fails)}/{len(s)} clips over limit {fails}', flush=True)
        if best is None or len(fails) < len(best['failingClips']):
            best = history[-1]
            if (out / f'{name}_best').exists():
                shutil.rmtree(out / f'{name}_best')
            shutil.copytree(rd / 'qa', out / f'{name}_best')
        if not a.keep_rounds and rnd:
            shutil.rmtree(out / f'{name}_r{rnd - 1}', ignore_errors=True)
        changed = False
        for n in fails:
            cur = cfg['amplitude'].setdefault(n, {})
            gs = [g for g in target_groups(s[n]['worstBones']) if cur.get(g, 1.0) > a.floor + 1e-9]
            if not gs:
                needs_art[n] = {'groups': target_groups(s[n]['worstBones']), 'stretch': s[n]['stretch'],
                                'compress': s[n]['compress'], 'worstBones': s[n]['worstBones']}
                continue
            for g in gs:
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
           'history': history}
    if res['best']['config'].get('amplitude') == {}:
        res['best']['config'].pop('amplitude')
    (out / f'tune_{name}.json').write_text(json.dumps(res, indent=1), encoding='utf-8')
    print(json.dumps({'name': name, 'status': res['status'], 'failing': best['failingClips'],
                      'other': best['otherWarnings']}))


if __name__ == '__main__':
    main()
