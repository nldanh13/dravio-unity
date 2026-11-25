"""Validate a character against the shared mobile budgets. Pure Python (no Blender).

Inputs are the JSON reports written by the other tools:
  --inspect  <inspect_character report of the FINAL file, e.g. the LOD FBX>   (required)
  --lods     <build_lods report>      (optional: deviation / skin / texture-drift gates)
  --prepare  <prepare_character report> (optional: bake colour-error gates)

  python scripts/art/character/validate_character.py --inspect x.inspect.json --lods x_lods.json \
      --out x.validation.json

Exit code 0 = no errors (warnings allowed), 1 = at least one error, 2 = bad input.
A PASS is a structural/numeric gate, not visual or in-engine acceptance.
"""
import argparse
import json
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[3]
DEFAULT_BUDGETS = REPO / 'Assets/Dravio/Character/Data/character-budgets.json'


class Result:
    def __init__(self):
        self.items = []

    def add(self, level, code, message, **data):
        self.items.append({'level': level, 'code': code, 'message': message, **data})

    def error(self, code, message, **d):
        self.add('error', code, message, **d)

    def warn(self, code, message, **d):
        self.add('warning', code, message, **d)

    def ok(self, code, message, **d):
        self.add('pass', code, message, **d)

    @property
    def errors(self):
        return [i for i in self.items if i['level'] == 'error']

    @property
    def warnings(self):
        return [i for i in self.items if i['level'] == 'warning']


def pct(n, d):
    return 100.0 * n / d if d else 0.0


def check_inspect(rep, budgets, r):
    meshes = rep.get('meshes', [])
    # Prop collision meshes (<Name>_Collider) are not rendered: no LOD budget, UV or material checks.
    colliders = [m for m in meshes if m['object'].endswith('_Collider')]
    meshes = [m for m in meshes if not m['object'].endswith('_Collider')]
    for c in colliders:
        r.ok('COLLIDER_MESH', f"{c['object']}: {c['triangles']} tris (collision only)", mesh=c['object'])
    if not meshes:
        r.error('NO_MESH', 'No mesh objects in file')
        return
    lod_cfg = {l['level']: l for l in budgets['lods']}
    # Triangles per LOD
    per = {}
    for m in meshes:
        per.setdefault(m['lod'] if m['lod'] is not None else 0, []).append(m)
    explicit = any(m['lod'] is not None for m in meshes)
    if not explicit:
        r.warn('NO_LOD_NAMING', 'No _LOD0/_LOD1/_LOD2 mesh names; treated as a single LOD0')
    tri = {lvl: sum(m['triangles'] for m in ms) for lvl, ms in per.items()}
    for lvl, cfg in lod_cfg.items():
        if lvl not in tri:
            (r.error if explicit else r.warn)('LOD_MISSING', f'LOD{lvl} missing', lod=lvl)
            continue
        t = tri[lvl]
        if t > cfg['maxTriangles']:
            r.error('LOD_OVER_BUDGET', f'LOD{lvl} {t} tris > max {cfg["maxTriangles"]}', lod=lvl, triangles=t)
        elif t < cfg['minTriangles']:
            r.warn('LOD_UNDER_TARGET', f'LOD{lvl} {t} tris < target minimum {cfg["minTriangles"]} '
                   '(acceptable only if the source is genuinely simpler)', lod=lvl, triangles=t)
        else:
            r.ok('LOD_BUDGET', f'LOD{lvl} {t} tris within {cfg["minTriangles"]}-{cfg["maxTriangles"]}', lod=lvl)
    levels = sorted(tri)
    for a, b in zip(levels, levels[1:]):
        if tri[b] >= tri[a]:
            r.error('LOD_NOT_DECREASING', f'LOD{b} ({tri[b]}) is not lighter than LOD{a} ({tri[a]})')
    for lvl in tri:
        if lvl not in lod_cfg:
            r.warn('LOD_UNEXPECTED', f'LOD{lvl} has no budget entry')
    # Per-mesh checks
    ub = budgets['uv']
    sb = budgets['skin']
    mb = budgets['materials']
    for m in meshes:
        tag = m['object']
        if not m['uv_layers']:
            r.error('UV_MISSING', f'{tag}: no UV0 (run prepare_character.py)', mesh=tag)
        else:
            s = m['uv_layers'][0].get('stats') or {}
            f = s.get('faces', 0)
            for key, limit, code in [('flipped_faces', ub['maxFlippedFacePct'], 'UV_FLIPPED'),
                                     ('zero_area_faces', ub['maxZeroAreaFacePct'], 'UV_ZERO_AREA'),
                                     ('faces_outside_0_1', ub['maxOutsideUnitPct'], 'UV_OUTSIDE_0_1')]:
                p = pct(s.get(key, 0), f)
                if p > limit:
                    r.warn(code, f'{tag}: {s.get(key)} UV faces ({p:.2f}%) {key} > {limit}%', mesh=tag)
        mats = m['materials']
        if len(mats) > mb['maxSlotsPerLodError']:
            r.error('MATERIAL_SLOTS', f'{tag}: {len(mats)} material slots', mesh=tag)
        elif len(mats) > mb['maxSlotsPerLodWarn']:
            r.warn('MATERIAL_SLOTS', f'{tag}: {len(mats)} material slots (draw calls)', mesh=tag)
        for mat in mats:
            bc = (mat.get('base_color') or {})
            if bc.get('kind') == 'vertex_color':
                r.warn('BASECOLOR_VERTEX_COLOR', f'{tag}/{mat["name"]}: Base Color from vertex colour; '
                       'URP Lit ignores it - bake with prepare_character.py', mesh=tag)
            if bc.get('kind') == 'image' and bc.get('colorspace') not in (None, 'sRGB'):
                r.error('BASECOLOR_COLORSPACE', f'{tag}/{mat["name"]}: Base Color texture not sRGB', mesh=tag)
            nm = mat.get('normal') or {}
            src = nm.get('source') or {}
            if nm.get('kind') == 'normal_map' and src.get('colorspace') == 'sRGB':
                r.error('NORMAL_COLORSPACE', f'{tag}/{mat["name"]}: normal map is sRGB (must be Non-Color)', mesh=tag)
            for ch in ('roughness', 'metallic'):
                c = mat.get(ch) or {}
                if c.get('kind') == 'image' and c.get('colorspace') == 'sRGB':
                    r.warn('DATA_MAP_SRGB', f'{tag}/{mat["name"]}: {ch} map flagged sRGB', mesh=tag)
        topo = m.get('topology_welded') or m.get('topology') or {}
        if topo.get('degenerate_faces'):
            r.warn('DEGENERATE_FACES', f'{tag}: {topo["degenerate_faces"]} zero-area faces', mesh=tag)
        if topo.get('loose_vertices'):
            r.warn('LOOSE_VERTICES', f'{tag}: {topo["loose_vertices"]} loose vertices', mesh=tag)
        if topo.get('non_manifold_edges'):
            r.warn('NON_MANIFOLD', f'{tag}: {topo["non_manifold_edges"]} non-manifold/boundary edges after weld', mesh=tag)
        sk = m.get('skin')
        if sk:
            if sk['max_influences'] > sb['maxInfluences']:
                r.error('SKIN_INFLUENCES', f'{tag}: {sk["max_influences"]} influences > {sb["maxInfluences"]}', mesh=tag)
            if sk['unweighted_vertices']:
                r.error('SKIN_UNWEIGHTED', f'{tag}: {sk["unweighted_vertices"]} unweighted vertices', mesh=tag)
            if sk['not_normalized_vertices']:
                r.error('SKIN_NOT_NORMALIZED', f'{tag}: {sk["not_normalized_vertices"]} vertices not normalised', mesh=tag)
            if sk['bones'] > sb['maxBones']:
                r.warn('SKIN_BONES', f'{tag}: {sk["bones"]} bones > {sb["maxBones"]}', mesh=tag)
    skinned = [m for m in meshes if m.get('skin')]
    if skinned and len(skinned) != len(meshes):
        r.error('SKIN_PARTIAL', 'Some LOD meshes are skinned and some are not')
    if len({m['skin']['armature'] for m in skinned}) > 1:
        r.error('SKIN_MULTIPLE_ARMATURES', 'LOD meshes use different armatures')
    tb = budgets['textures']
    for img in rep.get('images', []):
        w, h = img['size']
        if w == 0 or h == 0:
            r.error('TEXTURE_EMPTY', f'{img["name"]}: image has no pixels (missing file?)')
            continue
        if max(w, h) > tb['maxSize']:
            r.error('TEXTURE_SIZE', f'{img["name"]}: {w}x{h} > {tb["maxSize"]}')
        elif max(w, h) > tb['recommendedSize']:
            r.warn('TEXTURE_SIZE', f'{img["name"]}: {w}x{h} > recommended {tb["recommendedSize"]}')
        if tb['requirePowerOfTwo'] and not img['power_of_two']:
            r.warn('TEXTURE_NPOT', f'{img["name"]}: {w}x{h} is not power-of-two')


def check_lods(rep, budgets, r):
    cfg = {l['level']: l for l in budgets['lods']}
    for l in rep.get('lods', []):
        c = cfg.get(l['level'])
        if not c:
            continue
        dev = l['rest_deviation']['p95_rel']
        (r.ok if dev <= c['maxP95DeviationRel'] else r.error)(
            'LOD_SHAPE', f'LOD{l["level"]} rest p95 deviation {dev*100:.3f}% (limit {c["maxP95DeviationRel"]*100:.2f}%)',
            lod=l['level'], value=dev)
        if l.get('deformed_p95_rel_worst') is not None:
            d = l['deformed_p95_rel_worst']
            (r.ok if d <= c['maxDeformedP95DeviationRel'] else r.error)(
                'LOD_SKINNING', f'LOD{l["level"]} worst deformed p95 deviation {d*100:.3f}% '
                f'(limit {c["maxDeformedP95DeviationRel"]*100:.2f}%)', lod=l['level'], value=d)
        td = l.get('texture_drift')
        if td and td['p95_abs_srgb8'] > budgets['bake']['maxP95ColorErrorSrgb8'] * 2:
            r.warn('LOD_TEXTURE_DRIFT', f'LOD{l["level"]} Base Color drift p95 {td["p95_abs_srgb8"]:.1f}/255', lod=l['level'])
        if not l.get('uv_layers') or not all(l['uv_layers']):
            r.error('LOD_UV_LOST', f'LOD{l["level"]} lost its UV layers')
    if not rep.get('source_unchanged', False):
        r.error('SOURCE_CHANGED', 'LOD build did not confirm the source file is unchanged')
    for w in rep.get('warnings', []):
        r.warn('LOD_BUILD_NOTE', w)


def check_prepare(rep, budgets, r):
    if not rep.get('source_unchanged', False):
        r.error('SOURCE_CHANGED', 'Preparation did not confirm the source file is unchanged')
    err = rep.get('color_error_png')
    if err:
        b = budgets['bake']
        ok = err['p95_abs_srgb8'] <= b['maxP95ColorErrorSrgb8'] and err['within_2_srgb8_pct'] >= b['minWithin2Pct']
        (r.ok if ok else r.error)('BAKE_COLOR', f'Bake colour error p95 {err["p95_abs_srgb8"]:.2f}/255, '
                                  f'{err["within_2_srgb8_pct"]:.1f}% within 2/255')
    ov = rep.get('uv_overlap_fraction')
    if ov is not None and ov > 0.01:
        r.warn('UV_OVERLAP', f'UV0 overlap {ov:.2%} of covered texels')


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--inspect', type=Path, required=True)
    ap.add_argument('--lods', type=Path)
    ap.add_argument('--prepare', type=Path)
    ap.add_argument('--budgets', type=Path, default=DEFAULT_BUDGETS)
    ap.add_argument('--lod-profile', default='lods', help="'lods' (characters) or a propProfiles key, e.g. prop_small")
    ap.add_argument('--out', type=Path)
    a = ap.parse_args(argv)
    try:
        budgets = json.loads(a.budgets.read_text(encoding='utf-8'))
        if a.lod_profile != 'lods':
            budgets['lods'] = budgets['propProfiles'][a.lod_profile]
        inspect = json.loads(a.inspect.read_text(encoding='utf-8'))
    except (OSError, ValueError) as e:
        print('Bad input:', e, file=sys.stderr)
        return 2
    r = Result()
    check_inspect(inspect, budgets, r)
    if a.lods:
        check_lods(json.loads(a.lods.read_text(encoding='utf-8')), budgets, r)
    if a.prepare:
        check_prepare(json.loads(a.prepare.read_text(encoding='utf-8')), budgets, r)
    status = 'FAIL' if r.errors else ('PASS_WITH_WARNINGS' if r.warnings else 'PASS')
    out = {'status': status, 'model': inspect.get('source'), 'errors': len(r.errors),
           'warnings': len(r.warnings), 'items': r.items,
           'note': 'Structural/numeric gate only; visual, Unity import and device checks are separate.'}
    if a.out:
        a.out.write_text(json.dumps(out, indent=2, ensure_ascii=False), encoding='utf-8')
    print(f'{status}: {len(r.errors)} errors, {len(r.warnings)} warnings')
    for i in r.items:
        if i['level'] != 'pass':
            print(f"  [{i['level']}] {i['code']}: {i['message']}")
    return 1 if r.errors else 0


if __name__ == '__main__':
    sys.exit(main())
