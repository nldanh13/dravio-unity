"""End-to-end self-test of the character pipeline on generated TEST assets only.

  python scripts/art/character/run_selftest.py <work_dir> --blender "C:/.../blender.exe"
  python scripts/art/character/run_selftest.py <work_dir> --bpy-module      # pip install bpy==5.3.*

Steps per test asset: make_test_assets -> inspect (source) -> prepare_character ->
existing rig exporter (scripts/art/build_emberling_living.py --source-glb) -> build_lods ->
inspect (final FBX, i.e. an FBX re-import round trip) -> validate_character -> selftest_checks.
Writes <work_dir>/selftest-summary.json and prints a table. Exit 1 if any step fails.
These numbers describe TEST geometry; they are not results for any real Dravio model.
"""
import argparse
import json
import subprocess
import sys
import time
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[2]
RIG = REPO / 'scripts/art/build_emberling_living.py'
ASSETS = ['TEST_Quadruped_VC_NoUV', 'TEST_Quadruped_PBR_UV', 'TEST_Quadruped_Low']

SHIM = """import runpy, sys
script = sys.argv[1]
sys.argv = ['blender', '--'] + sys.argv[2:]
runpy.run_path(script, run_name='__main__')
"""


def runner(a, work):
    if a.blender:
        return lambda script, *args: [a.blender, '--factory-startup', '--background', '--python-exit-code', '1',
                                      '--python', str(script), '--', *map(str, args)]
    shim = work / '_bpy_shim.py'
    shim.write_text(SHIM, encoding='utf-8')
    return lambda script, *args: [sys.executable, str(shim), str(script), *map(str, args)]


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('work_dir', type=Path)
    g = ap.add_mutually_exclusive_group(required=True)
    g.add_argument('--blender')
    g.add_argument('--bpy-module', action='store_true')
    a = ap.parse_args()
    work = a.work_dir.resolve()
    work.mkdir(parents=True, exist_ok=True)
    cmd = runner(a, work)
    log = (work / 'selftest.log').open('w', encoding='utf-8')
    steps = []

    def step(label, argv):
        t = time.time()
        p = subprocess.run(argv, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, cwd=REPO)
        log.write(f'\n===== {label} =====\n$ {" ".join(argv)}\n{p.stdout}')
        ok = p.returncode == 0
        steps.append({'step': label, 'ok': ok, 'seconds': round(time.time() - t, 1), 'exit': p.returncode})
        print(f"{'OK  ' if ok else 'FAIL'} {label} ({steps[-1]['seconds']}s)")
        return ok

    d = {k: work / k for k in ('test-assets', 'reports', 'prepared', 'rigged', 'lods')}
    for p in d.values():
        p.mkdir(exist_ok=True)
    ow = '--overwrite-outputs'
    step('make_test_assets', cmd(HERE / 'make_test_assets.py', d['test-assets'], ow))
    summary = {'assets': {}}
    for name in ASSETS:
        src = d['test-assets'] / f'{name}.glb'
        r = {}
        step(f'{name}: inspect source', cmd(HERE / 'inspect_character.py', src, d['reports'] / f'{name}.source.inspect.json', ow))
        if not step(f'{name}: prepare', cmd(HERE / 'prepare_character.py', src, d['prepared'], ow)):
            continue
        prepared = d['prepared'] / f'{name}_Prepared.glb'
        rig_fbx = d['rigged'] / f'{name}_Rigged.fbx'
        for f in d['rigged'].glob(f'{name}_Rigged*'):
            if f.is_file():
                f.unlink()  # rig exporter has no overwrite guard of its own; these are our outputs
        if not step(f'{name}: rig (existing exporter)', cmd(RIG, rig_fbx, '--source-glb', prepared)):
            continue
        if not step(f'{name}: build_lods', cmd(HERE / 'build_lods.py', rig_fbx.with_suffix('.blend'), d['lods'],
                                                '--name', name, ow)):
            continue
        lods_fbx = d['lods'] / f'{name}_LODs.fbx'
        step(f'{name}: inspect LOD FBX (re-import)', cmd(HERE / 'inspect_character.py', lods_fbx,
                                                         d['reports'] / f'{name}_LODs.inspect.json', ow))
        val = d['reports'] / f'{name}.validation.json'
        step(f'{name}: validate', [sys.executable, str(HERE / 'validate_character.py'),
                                    '--inspect', str(d['reports'] / f'{name}_LODs.inspect.json'),
                                    '--lods', str(d['lods'] / f'{name}_lods.json'),
                                    '--prepare', str(d['prepared'] / f'{name}_prepare.json'), '--out', str(val)])
        step(f'{name}: selftest_checks', cmd(HERE / 'selftest_checks.py', work, name))
        for key, path in [('source', d['reports'] / f'{name}.source.inspect.json'),
                          ('prepare', d['prepared'] / f'{name}_prepare.json'),
                          ('lods', d['lods'] / f'{name}_lods.json'),
                          ('final', d['reports'] / f'{name}_LODs.inspect.json'),
                          ('validation', val), ('checks', d['reports'] / f'{name}.checks.json')]:
            if path.exists():
                r[key] = json.loads(path.read_text(encoding='utf-8'))
        summary['assets'][name] = condense(r)
    summary['steps'] = steps
    summary['all_ok'] = all(s['ok'] for s in steps)
    (work / 'selftest-summary.json').write_text(json.dumps(summary, indent=2, ensure_ascii=False), encoding='utf-8')
    print(json.dumps(summary['assets'], indent=1, ensure_ascii=False))
    print('ALL OK' if summary['all_ok'] else 'FAILURES - see selftest.log')
    return 0 if summary['all_ok'] else 1


def condense(r):
    out = {}
    if 'source' in r:
        s = r['source']['summary']
        out['source'] = {'triangles': s['triangles_total'], 'uv': s['any_uv'],
                         'vertex_color': s['any_meaningful_vertex_color'], 'base_color_texture': s['any_base_color_texture']}
    if 'prepare' in r:
        p = r['prepare']
        out['prepare'] = {'actions': [x['step'] for x in p['actions']], 'base_color_source': p['base_color_source'],
                          'bake_error_png': p.get('color_error_png'), 'uv_overlap': p.get('uv_overlap_fraction'),
                          'texture_set': p.get('texture_set'), 'source_unchanged': p['source_unchanged']}
    if 'lods' in r:
        out['lods'] = [{'level': l['level'], 'triangles': l['triangles'], 'in_budget': l['in_budget'],
                        'rest_p95_mm': round(l['rest_deviation']['p95_m'] * 1000, 3),
                        'rest_p95_pct_diag': round(l['rest_deviation']['p95_rel'] * 100, 4),
                        'deformed_p95_pct_diag_worst': round(l.get('deformed_p95_rel_worst', 0) * 100, 4),
                        'texture_drift_p95': (l.get('texture_drift') or {}).get('p95_abs_srgb8'),
                        'uv_flipped': (l['uv_stats'][0] or {}).get('flipped_faces'),
                        'max_influences': (l['skin'] or [{}])[0].get('max_influences')}
                       for l in r['lods']['lods']]
        out['lod_warnings'] = r['lods']['warnings']
    if 'final' in r:
        out['fbx_reimport'] = r['final']['summary']
    if 'validation' in r:
        v = r['validation']
        out['validation'] = {'status': v['status'], 'errors': v['errors'], 'warnings': v['warnings'],
                             'messages': [f"[{i['level']}] {i['code']}: {i['message']}" for i in v['items'] if i['level'] != 'pass']}
    if 'checks' in r:
        out['checks'] = r['checks']
    return out


if __name__ == '__main__':
    sys.exit(main())
