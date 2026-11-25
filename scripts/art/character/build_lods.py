"""Build LOD0/LOD1/LOD2 for a character (static or skinned) and measure what was lost.

Usage:
  blender --factory-startup --background --python-exit-code 1 \
      --python scripts/art/character/build_lods.py -- <model.blend|glb|fbx> <out_dir> [options]

Typical skinned flow: prepare_character.py -> build_emberling_living.py --source-glb (rig, 5 clips)
-> build_lods.py on the rigged .blend. All LODs then share ONE armature and the same clips.

Per LOD (targets from Assets/Dravio/Character/Data/character-budgets.json):
  * collapse decimation on a copy (source objects are never edited or exported)
  * vertex-group weights interpolated by the decimator, then limited to 4 and renormalised
  * UV layers, material slots and colour attributes carried through
  * metrics: triangles, rest-pose surface deviation vs the source, deformed deviation at
    sample frames of the existing animation, UV health, Base Color texture drift
A source below the LOD0 minimum is kept as-is (never subdivided) and reported.

Outputs: <Name>_LODs.fbx (+ .blend) whose meshes are named <mesh>_LOD0.._LOD2 (Unity's
LODGroup naming convention), and <Name>_lods.json.
"""
import argparse
import json
import math
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import char_common as cc  # noqa: E402

import bpy  # noqa: E402
import numpy as np  # noqa: E402
from mathutils.bvhtree import BVHTree  # noqa: E402
from mathutils.interpolate import poly_3d_calc  # noqa: E402

REPO = Path(__file__).resolve().parents[3]
DEFAULT_BUDGETS = REPO / 'Assets/Dravio/Character/Data/character-budgets.json'
# Frames inside the existing 5-clip take (Idle 1-181, Walk 182-217, Eat 218-278, Doze 279-399, Happy 400-445)
DEFAULT_FRAMES = [1, 190, 208, 250, 340, 420]


def copy_object(obj, name):
    new = obj.copy()
    new.data = obj.data.copy()
    new.name = name
    new.data.name = name
    for coll in obj.users_collection:
        coll.objects.link(new)
    return new


def decimate_to(obj, target, symmetry=False, protect_group=None, tolerance=0.02):
    """Collapse-decimate a copy of obj's mesh to ~target triangles (iterating the ratio)."""
    original = obj.data.copy()
    current = cc.triangle_count(original)
    if target >= current:
        bpy.data.meshes.remove(original)
        return current, 1.0
    ratio = target / current
    best = None
    for _ in range(5):
        obj.data = original.copy()
        mod = obj.modifiers.new('LOD_Decimate', 'DECIMATE')
        mod.decimate_type = 'COLLAPSE'
        mod.ratio = max(1e-4, min(1.0, ratio))
        mod.use_collapse_triangulate = True
        mod.use_symmetry = symmetry
        mod.symmetry_axis = 'X'
        if protect_group and protect_group in obj.vertex_groups:
            mod.vertex_group = protect_group
            mod.vertex_group_factor = 0.5
            mod.invert_vertex_group = True
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_move_to_index(modifier=mod.name, index=0)
        bpy.ops.object.modifier_apply(modifier=mod.name)
        got = cc.triangle_count(obj.data)
        err = abs(got - target) / target
        if best is None or err < best[0]:
            best = (err, obj.data, got, ratio)
        if err <= tolerance:
            break
        ratio *= target / max(got, 1)
    obj.data = best[1]
    bpy.data.meshes.remove(original)
    return best[2], best[3]


def renormalize_weights(obj, max_influences):
    if not obj.vertex_groups:
        return
    bpy.context.view_layer.objects.active = obj
    for o in bpy.context.selected_objects:
        o.select_set(False)
    obj.select_set(True)
    bpy.ops.object.vertex_group_clean(group_select_mode='ALL', limit=1e-4)
    bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL', limit=max_influences)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL', lock_active=False)


def texture_drift(src_objs, lod_objs, max_samples=20000):
    """Base Color drift caused by UV interpolation during decimation: sample the same
    texture at LOD triangle centroids (LOD UVs) and at the closest source surface point
    (source UVs, barycentric). Returns None when no Base Color image exists."""
    def base_image(objs):
        for o in objs:
            for m in o.data.materials:
                info = cc.principled_inputs(m)
                bc = info.get('base_color') or {}
                if bc.get('kind') == 'image':
                    return bpy.data.images[bc['image']]
        return None
    img = base_image(src_objs)
    if img is None or not all(o.data.uv_layers for o in src_objs + lod_objs):
        return None
    arr = cc.image_to_array(img)
    # Source triangles with per-corner UVs, rest pose (no armature eval: base mesh).
    verts, tris, tri_uv = [], [], []
    for o in src_objs:
        me = o.data
        me.calc_loop_triangles()
        uv = me.uv_layers[0].data
        base = len(verts)
        verts.extend(o.matrix_world @ v.co for v in me.vertices)
        for t in me.loop_triangles:
            tris.append(tuple(base + i for i in t.vertices))
            tri_uv.append([uv[l].uv.copy() for l in t.loops])
    bvh = BVHTree.FromPolygons(verts, tris, all_triangles=True)
    a_uv, b_uv = [], []
    for o in lod_objs:
        me = o.data
        me.calc_loop_triangles()
        uv = me.uv_layers[0].data
        step = max(1, len(me.loop_triangles) // max_samples)
        for i in range(0, len(me.loop_triangles), step):
            t = me.loop_triangles[i]
            c = o.matrix_world @ t.center
            hit = bvh.find_nearest(c)
            if hit[0] is None:
                continue
            fi = hit[2]
            tri = [verts[i] for i in tris[fi]]
            w = poly_3d_calc(tri, hit[0])
            suv = sum((tri_uv[fi][k] * w[k] for k in range(3)), tri_uv[fi][0] * 0)
            luv = sum((uv[l].uv for l in t.loops), uv[t.loops[0]].uv * 0) / 3
            a_uv.append(suv[:]); b_uv.append(luv[:])
    a = cc.sample_bilinear(arr, np.array(a_uv))[:, :3] * 255
    b = cc.sample_bilinear(arr, np.array(b_uv))[:, :3] * 255
    d = np.abs(a - b).max(axis=1)
    return {'image': img.name, 'samples': int(len(d)), 'mean_abs_srgb8': float(d.mean()),
            'p95_abs_srgb8': float(np.percentile(d, 95)), 'max_abs_srgb8': float(d.max())}


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('model', type=Path)
    ap.add_argument('out_dir', type=Path)
    ap.add_argument('--name')
    ap.add_argument('--budgets', type=Path, default=DEFAULT_BUDGETS)
    ap.add_argument('--symmetry', action='store_true', help='Symmetric (X) decimation; only for symmetric sources')
    ap.add_argument('--protect-group', help='Vertex group decimated less (e.g. face/eyes)')
    ap.add_argument('--frames', type=int, nargs='*', default=DEFAULT_FRAMES)
    ap.add_argument('--no-export', action='store_true')
    ap.add_argument('--overwrite-outputs', action='store_true')
    a = ap.parse_args(cc.script_args())
    src = a.model.resolve()
    budgets = json.loads(a.budgets.read_text(encoding='utf-8'))
    lod_cfg = sorted(budgets['lods'], key=lambda l: l['level'])
    max_inf = budgets['skin']['maxInfluences']
    name = a.name or src.stem
    outs = {k: cc.guard_output(src, a.out_dir / f'{name}{s}', a.overwrite_outputs)
            for k, s in [('fbx', '_LODs.fbx'), ('blend', '_LODs.blend'), ('json', '_lods.json')]}
    before = cc.sha256(src)
    cc.load_model(src, merge_vertices=True)
    scene = bpy.context.scene
    sources = [o for o in cc.mesh_objects() if o.visible_get()]
    if not sources:
        raise SystemExit('No mesh objects')
    if any(o.data.shape_keys for o in sources):
        raise SystemExit('Shape keys are not supported by the LOD builder yet (modifier apply would drop them).')
    armature = next((m.object for o in sources for m in o.modifiers if m.type == 'ARMATURE' and m.object), None)
    src_tris = {o.name: cc.triangle_count(o.data) for o in sources}
    total_src = sum(src_tris.values())
    report = {'source': str(src), 'source_sha256_before': before, 'name': name,
              'skinned': armature is not None, 'armature': armature.name if armature else None,
              'source_triangles': total_src, 'source_meshes': src_tris, 'lods': [], 'warnings': []}
    if total_src < lod_cfg[0]['minTriangles']:
        report['warnings'].append(
            f"Source has {total_src} triangles, below the LOD0 minimum {lod_cfg[0]['minTriangles']}. "
            "Kept as-is: subdividing would add triangles without adding detail.")
    scene.frame_set(1)
    prev_total = total_src
    all_lod_objs = []
    for cfg in lod_cfg:
        level = cfg['level']
        target = min(cfg['targetTriangles'], prev_total if level == 0 else int(prev_total * 0.6))
        if level > 0 and target < cfg['minTriangles']:
            report['warnings'].append(f'LOD{level}: target {target} below minimum {cfg["minTriangles"]} '
                                      'because the previous LOD is already small.')
        objs, ratios = [], {}
        for o in sources:
            share = src_tris[o.name] / total_src
            lod = copy_object(o, f'{o.name}_LOD{level}')
            got, ratio = decimate_to(lod, max(12, int(round(target * share))), a.symmetry, a.protect_group)
            ratios[lod.name] = ratio
            if armature:
                renormalize_weights(lod, max_inf)
            objs.append(lod)
        tris = sum(cc.triangle_count(o.data) for o in objs)
        prev_total = tris
        depsgraph = bpy.context.evaluated_depsgraph_get()
        scene.frame_set(1)
        entry = {'level': level, 'target': target, 'triangles': tris,
                 'vertices': sum(len(o.data.vertices) for o in objs),
                 'in_budget': cfg['minTriangles'] <= tris <= cfg['maxTriangles'],
                 'decimate_ratio': ratios, 'meshes': [o.name for o in objs],
                 'uv_layers': [[l.name for l in o.data.uv_layers] for o in objs],
                 'uv_stats': [cc.uv_stats(o.data, o.data.uv_layers[0].name) if o.data.uv_layers else None for o in objs],
                 'material_slots': [[m.name if m else None for m in o.data.materials] for o in objs],
                 'color_attributes': [[c.name for c in o.data.color_attributes] for o in objs],
                 'topology': [cc.topology_stats(o.data) for o in objs],
                 'skin': [cc.skin_stats(o) for o in objs] if armature else None}
        # Rest pose: disable armature evaluation for the comparison.
        arm_mods = [m for o in sources + objs for m in o.modifiers if m.type == 'ARMATURE']
        for m in arm_mods:
            m.show_viewport = False
        depsgraph = bpy.context.evaluated_depsgraph_get()
        entry['rest_deviation'] = cc.surface_deviation(sources, objs, depsgraph, samples=30000)
        for m in arm_mods:
            m.show_viewport = True
        if armature:
            dev = []
            for f in a.frames:
                scene.frame_set(f)
                depsgraph = bpy.context.evaluated_depsgraph_get()
                d = cc.surface_deviation(sources, objs, depsgraph, samples=15000)
                dev.append({'frame': f, **{k: d[k] for k in ('mean_rel', 'p95_rel', 'max_rel')}})
            scene.frame_set(1)
            entry['deformed_deviation'] = dev
            entry['deformed_p95_rel_worst'] = max(d['p95_rel'] for d in dev)
        entry['texture_drift'] = texture_drift(sources, objs)
        entry['limits'] = {
            'p95_rest_ok': entry['rest_deviation']['p95_rel'] <= cfg['maxP95DeviationRel'],
            'p95_deformed_ok': (entry.get('deformed_p95_rel_worst', 0) <= cfg['maxDeformedP95DeviationRel'])
            if armature else None}
        report['lods'].append(entry)
        all_lod_objs.extend(objs)
        print(f"LOD{level}: {tris} tris (target {target}); rest p95 dev "
              f"{entry['rest_deviation']['p95_rel']*100:.3f}% of bbox diag"
              + (f"; deformed worst p95 {entry['deformed_p95_rel_worst']*100:.3f}%" if armature else ''))
    # Export only LOD meshes (+ armature). Sources stay in the .blend, hidden, for reference.
    for o in sources:
        o.hide_set(True)
        o.hide_render = True
        o.name = o.name + '_SOURCE_REFERENCE'
    bpy.ops.wm.save_as_mainfile(filepath=str(outs['blend']), copy=True)
    if not a.no_export:
        bpy.ops.object.select_all(action='DESELECT')
        for o in all_lod_objs:
            o.select_set(True)
        kwargs = dict(filepath=str(outs['fbx']), use_selection=True, axis_forward='-Z', axis_up='Y',
                      apply_unit_scale=True, add_leaf_bones=False, use_armature_deform_only=True,
                      mesh_smooth_type='FACE', path_mode='COPY', embed_textures=True)
        if armature:
            armature.select_set(True)
            bpy.context.view_layer.objects.active = armature
            kwargs.update(object_types={'MESH', 'ARMATURE'}, bake_anim=True, bake_anim_use_all_actions=False,
                          bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True,
                          bake_anim_simplify_factor=0.0)
        else:
            kwargs.update(object_types={'MESH'}, bake_anim=False)
        if 'FINISHED' not in bpy.ops.export_scene.fbx(**kwargs):
            raise RuntimeError('FBX export failed')
        report['fbx'] = outs['fbx'].name
        report['fbx_bytes'] = outs['fbx'].stat().st_size
    after = cc.sha256(src)
    report['source_sha256_after'] = after
    report['source_unchanged'] = before == after
    if before != after:
        raise RuntimeError('Source changed during LOD build')
    cc.write_json(outs['json'], report)
    print('LOD report:', outs['json'])


if __name__ == '__main__':
    main()
