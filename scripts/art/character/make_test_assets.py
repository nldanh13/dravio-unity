"""Generate licence-clean, fully procedural TEST character models for the pipeline.

These are NOT Dravio species art and must never be shipped or reported as an
optimised real model. They exist only so the inspection, bake, LOD and
validation tools can be exercised without a real GLB.

Outputs (in <out_dir>):
  TEST_Quadruped_VC_NoUV.glb   ~60k tris, vertex colours, no UV, no textures
  TEST_Quadruped_PBR_UV.glb    ~40k tris, UV0 + base colour / normal / metallic-roughness
  TEST_Quadruped_Low.glb       ~6k tris, vertex colours, below the LOD0 target

All geometry/textures are generated from code in this file (CC0 / repository licence).
"""
import argparse
import math
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import char_common as cc  # noqa: E402

import bpy  # noqa: E402
import numpy as np  # noqa: E402  (bundled with Blender)


def primitive_blockout():
    """Wingless quadruped blockout facing Blender -Y (pipeline convention), ground at z=0."""
    parts = []

    def sphere(loc, scale):
        bpy.ops.mesh.primitive_uv_sphere_add(segments=32, ring_count=16, location=loc)
        o = bpy.context.object
        o.scale = scale
        parts.append(o)

    def cyl(loc, r, depth, rot=(0, 0, 0)):
        bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=r, depth=depth, location=loc, rotation=rot)
        parts.append(bpy.context.object)

    sphere((0, 0.02, 0.42), (0.24, 0.40, 0.22))          # body
    sphere((0, -0.40, 0.66), (0.17, 0.19, 0.16))         # head
    sphere((0, -0.56, 0.62), (0.09, 0.10, 0.075))        # muzzle
    cyl((0, -0.26, 0.56), 0.10, 0.26, (math.radians(-50), 0, 0))  # neck
    for x in (-0.15, 0.15):
        for y in (-0.22, 0.24):
            cyl((x, y, 0.17), 0.065, 0.34)               # legs
            sphere((x, y - 0.03, 0.035), (0.08, 0.10, 0.045))  # paws
        sphere((x * 0.6, -0.50, 0.73), (0.03, 0.03, 0.03))     # brow bumps (not horns)
    # tail: chain of shrinking spheres
    for i in range(7):
        t = i / 6
        sphere((0.05 * math.sin(t * 2.5), 0.42 + 0.42 * t, 0.40 - 0.18 * t + 0.10 * t * t),
               (0.11 * (1 - 0.6 * t), 0.13, 0.10 * (1 - 0.6 * t)))
    bpy.ops.object.select_all(action='DESELECT')
    for o in parts:
        o.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    body = bpy.context.object
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return body


def remesh_to(obj, target_tris):
    """Voxel remesh into one watertight surface, then decimate to the requested size."""
    mod = obj.modifiers.new('Remesh', 'REMESH')
    mod.mode = 'VOXEL'
    mod.voxel_size = 0.006
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=mod.name)
    tris = cc.triangle_count(obj.data)
    if tris > target_tris:
        dec = obj.modifiers.new('Decimate', 'DECIMATE')
        dec.ratio = target_tris / tris
        bpy.ops.object.modifier_apply(modifier=dec.name)
    bpy.ops.object.shade_smooth()
    return obj


def paint_vertex_colors(obj):
    """Deterministic test pattern: back/belly gradient + stripes + spots (high-frequency on purpose)."""
    me = obj.data
    attr = me.color_attributes.new('Col', 'BYTE_COLOR', 'POINT')
    back = np.array([0.80, 0.33, 0.22])
    belly = np.array([0.97, 0.85, 0.60])
    stripe = np.array([0.45, 0.16, 0.14])
    spot = np.array([0.98, 0.78, 0.25])
    for v in me.vertices:
        x, y, z = v.co
        t = min(1.0, max(0.0, (0.46 - z) / 0.30)) if abs(x) < 0.16 else 0.0
        c = back * (1 - t) + belly * t
        if z > 0.5 and math.sin(y * 40) > 0.6:
            c = stripe
        if (math.sin(x * 55) * math.sin(y * 55) * math.sin(z * 55)) > 0.55:
            c = spot
        attr.data[v.index].color_srgb = (float(c[0]), float(c[1]), float(c[2]), 1.0)
    me.color_attributes.active_color = attr
    me.color_attributes.render_color_index = 0
    mat = bpy.data.materials.new('TEST_VertexColor')
    nt = mat.node_tree
    bsdf = nt.nodes['Principled BSDF']
    vc = nt.nodes.new('ShaderNodeVertexColor')
    vc.layer_name = 'Col'
    nt.links.new(vc.outputs['Color'], bsdf.inputs['Base Color'])
    bsdf.inputs['Roughness'].default_value = 0.8
    me.materials.clear()
    me.materials.append(mat)


def make_image(name, size, rgb, colorspace='sRGB'):
    img = bpy.data.images.new(name, size, size, alpha=False)
    img.colorspace_settings.name = colorspace
    px = np.ones((size, size, 4), dtype=np.float32)
    px[..., :3] = rgb
    img.pixels.foreach_set(px.ravel())
    img.pack()
    return img


def pbr_material(obj, size=1024):
    me = obj.data
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.004)
    bpy.ops.object.mode_set(mode='OBJECT')
    me.uv_layers[0].name = 'UVMap'
    u, v = np.meshgrid(np.linspace(0, 1, size), np.linspace(0, 1, size))
    scales = 0.5 + 0.5 * np.sin(u * 140) * np.sin(v * 140)
    base = np.stack([0.55 + 0.25 * scales, 0.30 + 0.10 * scales, 0.22 + 0.05 * scales], -1)
    nx = 0.5 + 0.18 * np.cos(u * 140) * np.sin(v * 140)
    ny = 0.5 + 0.18 * np.sin(u * 140) * np.cos(v * 140)
    nz = np.sqrt(np.clip(1 - (2 * nx - 1) ** 2 - (2 * ny - 1) ** 2, 0, 1)) * 0.5 + 0.5
    normal = np.stack([nx, ny, nz], -1)
    mr = np.stack([np.ones_like(u), 0.55 + 0.35 * scales, 0.05 * (scales > 0.9)], -1)
    imgs = {'base': make_image('TEST_BaseColor', size, base),
            'normal': make_image('TEST_Normal', size, normal, 'Non-Color'),
            'mr': make_image('TEST_MetallicRoughness', size, mr, 'Non-Color')}
    mat = bpy.data.materials.new('TEST_PBR')
    nt = mat.node_tree
    bsdf = nt.nodes['Principled BSDF']
    tb = nt.nodes.new('ShaderNodeTexImage'); tb.image = imgs['base']
    tn = nt.nodes.new('ShaderNodeTexImage'); tn.image = imgs['normal']
    tm = nt.nodes.new('ShaderNodeTexImage'); tm.image = imgs['mr']
    nm = nt.nodes.new('ShaderNodeNormalMap')
    sep = nt.nodes.new('ShaderNodeSeparateColor')
    nt.links.new(tb.outputs['Color'], bsdf.inputs['Base Color'])
    nt.links.new(tn.outputs['Color'], nm.inputs['Color'])
    nt.links.new(nm.outputs['Normal'], bsdf.inputs['Normal'])
    nt.links.new(tm.outputs['Color'], sep.inputs['Color'])
    nt.links.new(sep.outputs['Green'], bsdf.inputs['Roughness'])
    nt.links.new(sep.outputs['Blue'], bsdf.inputs['Metallic'])
    me.materials.clear()
    me.materials.append(mat)


def export_glb(obj, path, vertex_color):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    kwargs = dict(filepath=str(path), use_selection=True, export_format='GLB',
                  export_yup=True, export_apply=True)
    # Blender 4.2+ option name; exports the active colour attribute as COLOR_0.
    kwargs['export_vertex_color'] = 'ACTIVE' if vertex_color else 'NONE'
    r = bpy.ops.export_scene.gltf(**kwargs)
    if 'FINISHED' not in r:
        raise RuntimeError(f"glTF export failed: {path}")


def build(target_tris, name):
    cc.reset_scene()
    body = primitive_blockout()
    remesh_to(body, target_tris)
    body.name = name
    return body


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('out_dir', type=Path)
    ap.add_argument('--overwrite-outputs', action='store_true')
    a = ap.parse_args(cc.script_args())
    a.out_dir.mkdir(parents=True, exist_ok=True)
    plan = [('TEST_Quadruped_VC_NoUV', 60000, 'vc'),
            ('TEST_Quadruped_PBR_UV', 40000, 'pbr'),
            ('TEST_Quadruped_Low', 6000, 'vc')]
    for name, tris, kind in plan:
        path = a.out_dir / f'{name}.glb'
        if path.exists() and not a.overwrite_outputs:
            print('exists, skipped:', path)
            continue
        obj = build(tris, name)
        if kind == 'vc':
            paint_vertex_colors(obj)
        else:
            pbr_material(obj)
        export_glb(obj, path, vertex_color=(kind == 'vc'))
        print(f'TEST asset {path.name}: {cc.triangle_count(obj.data)} tris')


if __name__ == '__main__':
    main()
