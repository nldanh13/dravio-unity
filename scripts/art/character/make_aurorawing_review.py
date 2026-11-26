"""CHAR.8 — Aurorawing style-review blockouts (3 growth stages) and its reward egg, as SAMPLE source GLBs.

  blender --background --python scripts/art/character/make_aurorawing_review.py -- <out_dir>
  python  scripts/art/character/make_aurorawing_review.py <out_dir>            (bpy module)

Outputs (all procedural, made by this script, no third-party content):
  SAMPLE_aurorawing_Baby.glb, SAMPLE_aurorawing_Juvenile.glb, SAMPLE_aurorawing_Adult.glb
      dense vertex-coloured source meshes for batch_import.py (prepare -> rig_archetype -> LODs -> QA)
  SAMPLE_egg_aurorawing.glb
      static egg for build_prop.py (no rig)

What is canonical and what is a proposal
  canonical (species-character-spec.json, not changed): locomotion walk + fly, surface fur, lung, BodyStyle Winged,
      silhouette "Cánh dải sáng, lông cổ, đuôi cực quang", palette primary #879BD5 / secondary #5F5B9E /
      belly #DDE6FA / accent #8DE6CF, activity night. The archetype (winged) and the clip set come from
      build_archetypes.py, never from this file.
  proposal (art director must confirm, see AURORAWING.md): four legs, the three stage proportions and heights,
      the ribbon count of the wings and tail, the egg shape and pattern.
These are blockouts for proportion / palette / rig review. The SAMPLE token keeps them out of every
"real model" report; they must not ship as final art.
"""
import argparse
import math
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import char_common as cc  # noqa: E402
import make_test_assets as mta  # noqa: E402

import bpy  # noqa: E402

PALETTE = {  # canonical species palette (SPECIES_MORPHOLOGY.md), sRGB
    'primary': '#879BD5', 'secondary': '#5F5B9E', 'belly': '#DDE6FA', 'accent': '#8DE6CF',
}

# Proposal: stage proportions relative to the Adult blockout (head bigger and wings/tail shorter when young).
STAGES = {
    'Baby': {'stance': 0.14, 'height': 0.55, 'head': 1.20, 'wing': 0.55, 'ribbons': 1, 'tail': 0.55, 'ruff': 0.6},
    'Juvenile': {'stance': 0.14, 'height': 0.85, 'head': 1.12, 'wing': 0.80, 'ribbons': 2, 'tail': 0.80, 'ruff': 0.85},
    'Adult': {'stance': 0.17, 'height': 1.20, 'head': 1.00, 'wing': 0.85, 'ribbons': 3, 'tail': 1.00, 'ruff': 1.0},
}


def srgb(hex_):
    return tuple(int(hex_[i:i + 2], 16) / 255.0 for i in (1, 3, 5))


def lerp(a, b, t):
    return tuple(x + (y - x) * t for x, y in zip(a, b))


class Parts:
    def __init__(self):
        self.objs = []

    def sphere(self, loc, scale, seg=32):
        bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=seg // 2, location=loc)
        o = bpy.context.object
        o.scale = scale
        self.objs.append(o)

    def cyl(self, loc, r, depth, rot=(0, 0, 0), verts=24):
        bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=depth, location=loc, rotation=rot)
        self.objs.append(bpy.context.object)

    def slab(self, loc, scale, rot=(0, 0, 0)):
        bpy.ops.mesh.primitive_cube_add(location=loc, rotation=rot)
        o = bpy.context.object
        o.scale = scale
        self.objs.append(o)

    def join(self):
        bpy.ops.object.select_all(action='DESELECT')
        for o in self.objs:
            o.select_set(True)
        bpy.context.view_layer.objects.active = self.objs[0]
        bpy.ops.object.join()
        o = bpy.context.object
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        return o


def aurorawing(stage):
    """Winged quadruped blockout facing Blender -Y, ground at z = 0, roughly 0.9 m tall before stage scaling."""
    p = Parts()
    k = STAGES[stage]
    h = k['head']
    p.sphere((0, 0.02, 0.44), (0.22, 0.40, 0.21))                       # body (slim, flyer)
    p.sphere((0, -0.24, 0.50), (0.20, 0.22, 0.20))                      # chest (flight muscles)
    p.cyl((0, -0.32, 0.60), 0.085, 0.26, (math.radians(-48), 0, 0))     # neck
    p.sphere((0, -0.44, 0.70), (0.15 * h, 0.17 * h, 0.14 * h))         # head
    p.sphere((0, -0.44 - 0.15 * h, 0.67), (0.075 * h, 0.09 * h, 0.06 * h))  # muzzle
    for sign in (-1, 1):                                                # ear tufts (fur), not horns
        p.sphere((sign * 0.09 * h, -0.40, 0.70 + 0.13 * h), (0.03, 0.04, 0.06))
    # neck ruff: "lông cổ" — a collar of soft tufts around the neck base
    r = k['ruff']
    for i in range(10):
        a = 2 * math.pi * i / 10
        p.sphere((0.12 * r * math.cos(a), -0.30 + 0.05 * math.sin(a), 0.56 + 0.10 * r * math.sin(a)),
                 (0.06 * r, 0.05 * r, 0.06 * r), 16)
    # four legs (proposal: canonical data does not state the limb count)
    for x in (-k['stance'], k['stance']):   # adult stance wider: also keeps L/R feet separate for rig_geometry
        for y in (-0.22, 0.24):
            p.cyl((x, y, 0.17), 0.058, 0.34)
            p.sphere((x, y - 0.03, 0.035), (0.075, 0.095, 0.042))
    # wings: "cánh dải sáng" — a shoulder root plus parallel ribbon membranes (bands of light)
    w = k['wing']
    for sign in (-1, 1):
        p.slab((sign * 0.19, -0.16, 0.62), (0.08, 0.10, 0.035))          # shoulder root
        for j in range(3):
            span = (0.34 - 0.06 * j) * w
            p.slab((sign * (0.22 + span), -0.12 + 0.11 * j, 0.68 - 0.03 * j),
                   (span, 0.05, 0.011), (0, math.radians(sign * (14 - 4 * j)), 0))
    # tail: tapering chain, then "đuôi cực quang" ribbons that fan out at the tip
    t = k['tail']
    for i in range(8):
        s = i / 7
        p.sphere((0.04 * math.sin(s * 2.5), 0.42 + 0.55 * t * s, 0.42 - 0.20 * s + 0.12 * s * s),
                 (0.10 * (1 - 0.65 * s), 0.12, 0.09 * (1 - 0.65 * s)))
    tip_y, tip_z = 0.42 + 0.55 * t + 0.06, 0.34
    for j in range(k['ribbons']):
        a = (j - (k['ribbons'] - 1) / 2) * 0.45
        p.slab((0.16 * t * math.sin(a), tip_y + 0.14 * t, tip_z + 0.02 * j), (0.03, 0.16 * t, 0.008), (0, 0, a))
    return p.join()


def paint(obj, stage):
    """Region colours from the canonical palette: back primary, belly / ruff pale, wings secondary banded to accent
    at the tips (aurora), tail ribbons accent. Baked to BaseColor by prepare_character.py."""
    prim, sec, belly, acc = (srgb(PALETTE[n]) for n in ('primary', 'secondary', 'belly', 'accent'))
    me = obj.data
    attr = me.color_attributes.new('Col', 'BYTE_COLOR', 'POINT')
    span = max(abs(v.co.x) for v in me.vertices) or 1.0
    ymax = max(v.co.y for v in me.vertices)
    for v in me.vertices:
        x, y, z = v.co
        if abs(x) > 0.24 and z > 0.5:                     # wing ribbons
            u = min(1.0, (abs(x) - 0.24) / max(0.05, span - 0.24))
            band = 0.5 + 0.5 * math.sin(y * 55)
            c = lerp(sec, acc, min(1.0, u * 0.8 + 0.25 * band))
        elif y > ymax - 0.30 and z < 0.45:                # tail tip ribbons
            c = lerp(prim, acc, min(1.0, (y - (ymax - 0.30)) / 0.30))
        elif -0.40 < y < -0.20 and 0.48 < z < 0.70:       # neck ruff
            c = belly
        elif abs(x) < 0.15 and z < 0.36 and -0.30 < y < 0.30:  # belly
            c = belly
        else:
            c = lerp(prim, sec, min(1.0, max(0.0, (z - 0.40) / 0.40)))
        attr.data[v.index].color_srgb = (c[0], c[1], c[2], 1.0)
    me.color_attributes.active_color = attr
    me.color_attributes.render_color_index = 0
    mat = bpy.data.materials.new('SAMPLE_aurorawing_VC')
    nt = mat.node_tree
    bsdf = nt.nodes['Principled BSDF']
    vc = nt.nodes.new('ShaderNodeVertexColor')
    vc.layer_name = 'Col'
    nt.links.new(vc.outputs['Color'], bsdf.inputs['Base Color'])
    bsdf.inputs['Roughness'].default_value = 0.85        # fur: matte
    me.materials.clear()
    me.materials.append(mat)


def egg():
    """Reward egg: classic ovoid (wider base), aurora bands from the palette. Proposal: 0.22 m tall."""
    bpy.ops.mesh.primitive_uv_sphere_add(segments=48, ring_count=32, location=(0, 0, 0))
    o = bpy.context.object
    for v in o.data.vertices:
        z = v.co.z                                         # -1 .. 1
        taper = 0.78 + 0.22 * (1 - (z + 1) / 2) ** 0.6     # narrower top
        v.co.x *= 0.74 * taper
        v.co.y *= 0.74 * taper
        v.co.z = z + 1.0                                   # 0 .. 2, ground at z = 0 (height ≈ 1.35 × width)
    prim, sec, belly, acc = (srgb(PALETTE[n]) for n in ('primary', 'secondary', 'belly', 'accent'))
    me = o.data
    attr = me.color_attributes.new('Col', 'BYTE_COLOR', 'POINT')
    for v in me.vertices:
        x, y, z = v.co
        a = math.atan2(y, x)
        wave = 0.06 * math.sin(3 * a)
        band = math.sin((z + wave) * 9)
        c = lerp(belly, prim, 0.35 + 0.18 * z)
        if band > 0.65:
            c = lerp(sec, acc, 0.5 + 0.5 * math.sin(a * 2))
        attr.data[v.index].color_srgb = (c[0], c[1], c[2], 1.0)
    me.color_attributes.active_color = attr
    me.color_attributes.render_color_index = 0
    mat = bpy.data.materials.new('SAMPLE_egg_aurorawing_VC')
    nt = mat.node_tree
    bsdf = nt.nodes['Principled BSDF']
    vc = nt.nodes.new('ShaderNodeVertexColor')
    vc.layer_name = 'Col'
    nt.links.new(vc.outputs['Color'], bsdf.inputs['Base Color'])
    bsdf.inputs['Roughness'].default_value = 0.35        # glossy shell
    me.materials.clear()
    me.materials.append(mat)
    bpy.ops.object.shade_smooth()
    return o


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('out_dir', type=Path)
    ap.add_argument('--tris', type=int, default=110000, help='dense source size (prepare decimates to LOD0)')
    a = ap.parse_args(cc.script_args())
    a.out_dir.mkdir(parents=True, exist_ok=True)
    for stage, k in STAGES.items():
        cc.reset_scene()
        o = mta.remesh_to(aurorawing(stage), a.tris)
        paint(o, stage)                                   # region thresholds are in blockout units: paint before scaling
        s = k['height'] / o.dimensions.z
        o.scale = (s, s, s)
        bpy.ops.object.transform_apply(scale=True)
        name = f'SAMPLE_aurorawing_{stage}'
        o.name = name
        mta.export_glb(o, a.out_dir / f'{name}.glb', vertex_color=True)
        print(f'SAMPLE {name}: {cc.triangle_count(o.data)} tris, {o.dimensions.z:.2f} m tall, span {o.dimensions.x:.2f} m')
    cc.reset_scene()
    o = egg()
    o.name = 'SAMPLE_egg_aurorawing'
    mta.export_glb(o, a.out_dir / 'SAMPLE_egg_aurorawing.glb', vertex_color=True)
    print(f'SAMPLE SAMPLE_egg_aurorawing: {cc.triangle_count(o.data)} tris')


if __name__ == '__main__':
    main()
