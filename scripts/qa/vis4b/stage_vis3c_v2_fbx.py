"""VIS.4b (issue #91) - TRIAL conversion of ArtSource/Origin_VIS3c_v2 GLBs to kit-convention FBX.

Staging only (refuses any path inside Assets). Nothing is placed or imported into Unity.

  python scripts/qa/vis4b/stage_vis3c_v2_fbx.py -- --out <empty staging dir> [--src ArtSource/<dir> ...]

Per AI3 handoff (#94, 27/11): no extra 180 deg yaw for v2 (front is glTF -Z = Blender +Y = Unity -Z,
the kit's camera-facing side); vertex alpha forced to 0 (Painterly Lit reads vertex A as wind
weight / path mask); RGB kept as authored (Painterly Lit multiplies albedo by vertex RGB).
One material slot `ENV_Architecture` and a single UV channel pinned to (0.5, 0.5): intended for a
Painterly Lit material with a white/neutral base map. The current props atlas has no neutral cell
(see report), so remapping to ENV_Props would tint every colour. Export uses the kit's own
`export_fbx` settings.
"""
import json
import sys
from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / "scripts" / "art" / "environment"))
_argv = sys.argv
sys.argv = [sys.argv[0]]  # keep the kit module's own argv parser quiet
import build_origin_sanctuary as env  # noqa: E402
sys.argv = _argv

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = Path(argv[argv.index("--out") + 1]).resolve()
if ROOT / "Assets" in OUT.parents or OUT == ROOT / "Assets":
    raise SystemExit("Stage outside Unity Assets.")
if OUT.exists() and any(OUT.iterdir()):
    raise SystemExit("Choose an empty staging directory.")
OUT.mkdir(parents=True, exist_ok=True)
# Source folders: --src <ArtSource dir> (repeatable). Default VIS3c v2. A later folder's GLB of the
# same name replaces an earlier one (e.g. Hills v3 over v2).
SRCS = [ROOT / argv[i + 1] for i, a in enumerate(argv) if a == "--src"] or [ROOT / "ArtSource" / "Origin_VIS3c_v2"]
GLBS = {}
for d in SRCS:
    for g in sorted(d.glob("*.glb")):
        GLBS[g.name] = g


def bounds(objs):
    pts = [o.matrix_world @ v.co for o in objs for v in o.data.vertices]
    return ([min(p[i] for p in pts) for i in range(3)], [max(p[i] for p in pts) for i in range(3)])


report = {}
for glb in GLBS.values():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(glb))
    meshes = [o for o in bpy.data.objects if o.type == 'MESH']
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    obj.name = glb.stem
    me = obj.data
    col = me.color_attributes[0]
    col.name = "Color"
    for d in col.data:
        c = d.color
        d.color = (c[0], c[1], c[2], 0.0)
    me.color_attributes.active_color = col
    while me.uv_layers:
        me.uv_layers.remove(me.uv_layers[0])
    uv = me.uv_layers.new(name="UVMap")
    for d in uv.data:
        d.uv = (0.5, 0.5)
    me.materials.clear()
    me.materials.append(bpy.data.materials.new("ENV_Architecture"))
    src_lo, src_hi = bounds([obj])
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    fbx = OUT / f"SM_ENV_{glb.stem.replace('Origin_', '')}.fbx"
    env.export_fbx(obj, fbx)

    # Round trip: what Unity-side tools would read back.
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(fbx))
    back = [o for o in bpy.data.objects if o.type == 'MESH']
    lo, hi = bounds(back)
    bm = back[0].data
    alphas = {round(d.color[3], 3) for a in bm.color_attributes for d in a.data}
    # Facade check: mean normal of faces with the brightest/most saturated door/window colours is
    # not robust; use the AABB-weighted normal balance instead: facade detail adds area on +Y.
    plus_y = sum(p.area for p in bm.polygons if p.normal.y > 0.5)
    minus_y = sum(p.area for p in bm.polygons if p.normal.y < -0.5)
    report[fbx.name] = {
        "source": str(glb.relative_to(ROOT)), "triangles": tris,
        "roundtrip_triangles": sum(len(p.vertices) - 2 for p in bm.polygons),
        "materials": [s.material.name for s in back[0].material_slots if s.material],
        "uv_layers": [u.name for u in bm.uv_layers], "color_layers": [a.name for a in bm.color_attributes],
        "vertex_alpha_values": sorted(alphas),
        "bounds_blender_min": [round(v, 3) for v in lo], "bounds_blender_max": [round(v, 3) for v in hi],
        "source_bounds_delta_mm": round(1000 * max(abs(a - b) for a, b in zip(src_lo + src_hi, lo + hi)), 2),
        "pivot_base_z": round(lo[2], 4),
        "facing_area_plusY_m2": round(plus_y, 3), "facing_area_minusY_m2": round(minus_y, 3),
    }
    print(fbx.name, json.dumps(report[fbx.name]))

(OUT / "vis3c_v2_fbx_trial.json").write_text(json.dumps(report, indent=2))
print("[VIS.4b] staged", len(report), "FBX ->", OUT)
