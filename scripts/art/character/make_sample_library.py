"""Generate a SAMPLE source folder that imitates a batch of image-to-3D GLBs (dense, UV + baked
texture, a stray floating island, open boundaries) for exercising batch_import.py.

Every file is procedural geometry from this script. Names carry the token SAMPLE and real
species ids only so the filename -> species mapping is exercised; the shapes are generic
archetype stand-ins, not Dravio species designs, and must never be shipped.

  blender --background --python scripts/art/character/make_sample_library.py -- <out_dir>
"""
import argparse
import math
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import char_common as cc  # noqa: E402
import make_test_assets as mta  # noqa: E402

import bpy  # noqa: E402


def join(parts):
    bpy.ops.object.select_all(action='DESELECT')
    for o in parts:
        o.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    o = bpy.context.object
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return o


def sphere(loc, scale, seg=32):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=seg // 2, location=loc)
    o = bpy.context.object
    o.scale = scale
    return o


def cyl(loc, r, depth, rot=(0, 0, 0), verts=24):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=depth, location=loc, rotation=rot)
    return bpy.context.object


def cube(loc, scale, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(location=loc, rotation=rot)
    o = bpy.context.object
    o.scale = scale
    return o


def winged():
    body = mta.primitive_blockout()
    wings = []
    for sign in (-1, 1):
        w = cube((sign * 0.42, -0.05, 0.62), (0.30, 0.16, 0.012), (0, math.radians(sign * 18), 0))
        wings.append(w)
    return join([body] + wings)


def serpent():
    parts = []
    for i in range(22):
        t = i / 21
        parts.append(sphere((0.18 * math.sin(t * 2 * math.pi), -0.9 + 1.8 * t, 0.16 + 0.03 * math.sin(t * 6)),
                            (0.13 * (1 - 0.6 * t) + 0.03, 0.12, 0.12 * (1 - 0.6 * t) + 0.03), 24))
    parts.append(sphere((0, -1.0, 0.2), (0.12, 0.15, 0.1)))
    return join(parts)


def aquatic():
    parts = [sphere((0, 0, 0.3), (0.22, 0.55, 0.2)), sphere((0, -0.55, 0.32), (0.14, 0.16, 0.13)),
             cube((0, 0.62, 0.3), (0.02, 0.12, 0.16))]
    for sign in (-1, 1):
        parts.append(cube((sign * 0.38, -0.15, 0.25), (0.2, 0.09, 0.01), (0, math.radians(sign * 12), 0)))
    return join(parts)


def crystal():
    parts = []
    for i, (x, y, h) in enumerate([(0, 0, 0.6), (0.12, 0.05, 0.4), (-0.1, 0.08, 0.35), (0.03, -0.12, 0.3)]):
        bpy.ops.mesh.primitive_cone_add(vertices=6, radius1=0.07, radius2=0.0, depth=h, location=(x, y, h / 2))
        parts.append(bpy.context.object)
    parts.append(cyl((0, 0, 0.03), 0.22, 0.06, verts=12))
    return join(parts)


def tree():
    parts = [cyl((0, 0, 0.6), 0.12, 1.2), sphere((0, 0, 1.45), (0.6, 0.6, 0.5)), sphere((0.35, 0.1, 1.25), (0.35, 0.35, 0.3))]
    return join(parts)


def chest():
    return join([cube((0, 0, 0.2), (0.35, 0.22, 0.2)), cyl((0, 0, 0.4), 0.22, 0.7, (0, math.radians(90), 0), 16)])


def island():
    bpy.ops.mesh.primitive_cone_add(vertices=48, radius1=1.0, radius2=0.2, depth=1.2, location=(0, 0, 0.6),
                                    rotation=(math.pi, 0, 0))
    a = bpy.context.object
    return join([a, cyl((0, 0, 1.25), 1.0, 0.1, verts=48), sphere((0.3, 0.2, 1.5), (0.3, 0.3, 0.25))])


def add_stray_island(obj):
    """Generated meshes often carry small floating fragments."""
    s = sphere((obj.dimensions.x * 0.3, 0.0, obj.dimensions.z * 1.05), (0.02, 0.02, 0.02), 8)
    return join([obj, s])


def finish(obj, name, tris, out, stray=True):
    obj = mta.remesh_to(obj, tris) if tris else obj
    if stray:
        obj = add_stray_island(obj)
    obj.name = name
    mta.pbr_material(obj, size=512)
    mta.export_glb(obj, out / f'{name}.glb', vertex_color=False)
    print(f'SAMPLE {name}: {cc.triangle_count(obj.data)} tris')


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('out_dir', type=Path)
    a = ap.parse_args(cc.script_args())
    out = a.out_dir
    out.mkdir(parents=True, exist_ok=True)
    plan = [
        ('SAMPLE_emberling_Baby', lambda: mta.primitive_blockout(), 120000),
        ('SAMPLE_zephyr', winged, 90000),
        ('SAMPLE_leviathan_Adult', serpent, 80000),
        ('SAMPLE_frostfin_Juvenile', aquatic, 60000),
        ('SAMPLE_magma_Baby', lambda: mta.primitive_blockout(), 50000),
        ('SAMPLE_mystery_object', lambda: mta.primitive_blockout(), 8000),
        ('SAMPLE_prop_crystal_cluster', crystal, 20000),
        ('SAMPLE_prop_tree_ancient', tree, 30000),
        ('SAMPLE_prop_treasure_chest', chest, 12000),
        ('SAMPLE_prop_floating_island', island, 40000),
    ]
    for name, fn, tris in plan:
        cc.reset_scene()
        finish(fn(), name, tris, out, stray=not name.startswith('SAMPLE_prop'))


if __name__ == '__main__':
    main()
