"""Static environment prop: pivot, LOD0-2, simple collider and size metadata. No rig, ever.

  blender --background --python scripts/art/character/build_prop.py -- <prepared.glb> <out_dir> \
      --category crystal [--name PRP_crystal_cluster] [--height 1.2]

Category -> budget profile and collider type come from character-budgets.json (propCategories).
  pivot      bottom-centre (ground at z = 0, X/Y centred); scale kept unless --height is given
             (generated GLBs have arbitrary scale; real-world size is an art/GDD decision)
  LODs       collapse decimation per profile; never subdivides a lighter source
  collider   box -> metadata only; convex -> convex hull (<= 250 tris, Unity convex limit 255);
             mesh -> simplified static mesh (<= 1000 tris) for non-convex shapes (arches, islands, trees)
Outputs: <Name>_Prop.fbx (+ .blend) with <Name>_LOD0.._LOD2 and optional <Name>_Collider, <Name>_prop.json.
"""
import argparse
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import char_common as cc  # noqa: E402
import build_lods as bl  # noqa: E402

import bpy  # noqa: E402
import bmesh  # noqa: E402
from mathutils import Vector  # noqa: E402


def category_cfg(budgets, category):
    for c in budgets['propCategories']['categories']:
        if c['category'] == category:
            return c
    raise SystemExit(f'Unknown prop category {category}')


def collider_mesh(src_obj, kind, name):
    if kind == 'box':
        return None
    me = src_obj.data.copy()
    obj = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(obj)
    obj.data.materials.clear()
    for layer in list(me.uv_layers):
        me.uv_layers.remove(layer)
    if kind == 'convex':
        bm = bmesh.new()
        for v in me.vertices:
            bm.verts.new(v.co)
        res = bmesh.ops.convex_hull(bm, input=bm.verts[:])
        drop = {g for g in res['geom_interior'] + res['geom_unused'] if isinstance(g, bmesh.types.BMVert)}
        bmesh.ops.delete(bm, geom=list(drop), context='VERTS')
        bm.to_mesh(me)
        bm.free()
        limit = 250
    else:
        limit = 1000
    bl.decimate_to(obj, limit)
    return obj


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('source', type=Path)
    ap.add_argument('out_dir', type=Path)
    ap.add_argument('--category', required=True)
    ap.add_argument('--name')
    ap.add_argument('--height', type=float, help='Scale to this height in metres (art/GDD value)')
    ap.add_argument('--budgets', type=Path, default=bl.DEFAULT_BUDGETS)
    ap.add_argument('--overwrite-outputs', action='store_true')
    a = ap.parse_args(cc.script_args())
    budgets = json.loads(a.budgets.read_text(encoding='utf-8'))
    cat = category_cfg(budgets, a.category)
    lods = sorted(budgets['propProfiles'][cat['profile']], key=lambda l: l['level'])
    src = a.source.resolve()
    name = a.name or f'PRP_{a.category}'
    outs = {k: cc.guard_output(src, a.out_dir / f'{name}{s}', a.overwrite_outputs)
            for k, s in [('fbx', '_Prop.fbx'), ('blend', '_Prop.blend'), ('json', '_prop.json')]}
    before = cc.sha256(src)
    cc.load_model(src, merge_vertices=True)
    objs = cc.mesh_objects()
    if any(o.type == 'ARMATURE' for o in bpy.context.scene.objects):
        raise SystemExit('Props are static; source contains an armature')
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    if len(objs) > 1:
        bpy.ops.object.join()
    base = bpy.context.view_layer.objects.active
    base.parent = None
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    lo, hi = cc.bounds_of([v.co for v in base.data.vertices])
    s = (a.height / (hi.z - lo.z)) if a.height else 1.0
    c = Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))
    for v in base.data.vertices:
        v.co = (v.co - c) * s
    base.data.update()
    base.name = name + '_SOURCE_REFERENCE'
    src_tris = cc.triangle_count(base.data)
    report = {'name': name, 'category': a.category, 'profile': cat['profile'], 'collider': cat['collider'],
              'source': str(src), 'source_sha256_before': before, 'source_triangles': src_tris,
              'scale': {'factor': s, 'source': '--height' if a.height else 'source units (unverified real-world size)'},
              'pivot': 'bottom-centre', 'lods': [], 'warnings': [], 'rigged': False}
    prev = src_tris
    lod_objs = []
    for cfg in lods:
        target = min(cfg['targetTriangles'], prev if cfg['level'] == 0 else int(prev * 0.6))
        lod = bl.copy_object(base, f"{name}_LOD{cfg['level']}")
        got, ratio = bl.decimate_to(lod, max(12, target))
        prev = got
        dev = cc.surface_deviation([base], [lod], samples=20000)
        report['lods'].append({'level': cfg['level'], 'triangles': got, 'target': target,
                               'in_budget': cfg['minTriangles'] <= got <= cfg['maxTriangles'],
                               'rest_deviation': dev, 'decimate_ratio': ratio,
                               'uv_layers': [l.name for l in lod.data.uv_layers],
                               'uv_stats': cc.uv_stats(lod.data, lod.data.uv_layers[0].name) if lod.data.uv_layers else None,
                               'material_slots': [m.name if m else None for m in lod.data.materials],
                               'texture_drift': bl.texture_drift([base], [lod])})
        if not report['lods'][-1]['in_budget']:
            report['warnings'].append(f"LOD{cfg['level']} {got} tris outside {cfg['minTriangles']}-{cfg['maxTriangles']}"
                                      + (' (source lighter than budget; not subdivided)' if got < cfg['minTriangles'] else ''))
        lod_objs.append(lod)
    col = collider_mesh(lod_objs[-1], cat['collider'], f'{name}_Collider')
    lo, hi = cc.bounds_of([v.co for v in base.data.vertices])
    size = hi - lo
    report['size'] = {'x': size.x, 'y': size.y, 'z': size.z, 'height': size.z,
                      'footprintRadius': max(size.x, size.y) / 2,
                      'bounds_min': list(lo), 'bounds_max': list(hi)}
    report['colliderData'] = {'type': cat['collider'], 'boxCenter': list((lo + hi) / 2), 'boxSize': list(size),
                              'mesh': col.name if col else None,
                              'meshTriangles': cc.triangle_count(col.data) if col else 0}
    base.hide_set(True)
    bpy.ops.wm.save_as_mainfile(filepath=str(outs['blend']), copy=True)
    bpy.ops.object.select_all(action='DESELECT')
    for o in lod_objs + ([col] if col else []):
        o.select_set(True)
    r = bpy.ops.export_scene.fbx(filepath=str(outs['fbx']), use_selection=True, object_types={'MESH'},
                                 axis_forward='-Z', axis_up='Y', apply_unit_scale=True, mesh_smooth_type='FACE',
                                 path_mode='COPY', embed_textures=True, bake_anim=False)
    if 'FINISHED' not in r:
        raise RuntimeError('FBX export failed')
    after = cc.sha256(src)
    report.update({'fbx': outs['fbx'].name, 'source_sha256_after': after, 'source_unchanged': before == after})
    cc.write_json(outs['json'], report)
    print(f"Prop {name}: {[l['triangles'] for l in report['lods']]} tris, collider {cat['collider']}, "
          f"size {size.x:.2f}x{size.y:.2f}x{size.z:.2f}")


if __name__ == '__main__':
    main()
