"""CHAR.8  Sanctuary project STYLE SAMPLES — one representative building per world, for art-direction review
before the full 20-building production (catalog: Docs/u2.11/SANCTUARY_PROJECTS.md, Domain SanctuaryProjectContent).

Same kit, atlases and material slots as the existing environment (build_origin_sanctuary.py / build_zone_kits.py):
the samples add NO texture and NO material. Every mesh is code-built.

  blender --factory-startup --background --python-exit-code 1 \
      --python scripts/art/environment/build_sanctuary_projects.py -- \
      --out art-exports/sanctuary [--unity Assets/Dravio/Environment/Sanctuary] [--preview]
  (or: python scripts/art/environment/build_sanctuary_projects.py --out ... with the bpy 5.3 module)

Per sample: SM_SANC_<projectId>.fbx with three meshes <name>_LOD0/_LOD1/_LOD2 (Unity builds the LODGroup from
the suffix) and ANCHOR_* empties; <projectId>.sanctuary.json with prefab id, materials, LOD triangles, bounds,
footprint and anchors in UNITY coordinates (x right, y up, z forward; pivot = bottom centre, front = -Z faces the
Sanctuary camera like every environment prop).
--unity copies FBX + JSON into Assets/Dravio/Environment/Sanctuary/{Models,Data}; existing files are NOT overwritten.

Not decided here (Domain / economy owns them): cost, materials to build, buffs, rewards. These assets are visual only.
"""
import json
import math
import shutil
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import build_origin_sanctuary as K  # noqa: E402  (imports bpy; its main() is guarded)
import build_zone_kits as ZK  # noqa: E402
import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402

REPO = HERE.parent.parent.parent
OUT = Path(K.arg("--out", "art-exports/sanctuary")).resolve()
UNITY = K.arg("--unity")
PREVIEW = K.arg("--preview", flag=True)

LOD_RATIOS = (1.0, 0.5, 0.22)          # proposal, same spirit as prop_large (10k / 4k / 1.2k)
LOD_SCREEN = (0.35, 0.12, 0.02)        # LODGroup transition heights (cull below the last)
BUDGET_LOD0 = 6000                     # per building; a scene shows at most a handful at once


def unity(v):
    """Blender (x, y, z) -> Unity (x, y, z) for the environment FBX export (axis_forward -Z, up Y)."""
    return [round(-v[0], 3), round(v[2], 3), round(-v[1], 3)]


# ------------------------------------------------------------------ concept dressing (ArtSource/Habitats concepts)
def dress(k, x, y, z=0.0, flowers=("flower_pink", "flower_gold", "flower_blue"), ivy=True, scale=1.0):
    """Concept art language: vines with small flowers climbing the stone, flower tufts at the base."""
    r = k.rng
    for i, cell in enumerate(flowers):
        a = r.uniform(0, 6.28)
        k.cross_cards((x + math.cos(a) * 0.22 * scale, y + math.sin(a) * 0.22 * scale, z), 0.32 * scale, 0.28 * scale, "F_" + cell, count=2,
                      yaw=r.uniform(0, 3.14))
    if ivy:
        k.card((x + 0.12 * scale, y + 0.14 * scale, z), 0.3 * scale, 0.9 * scale, "F_ivy_strand", yaw=r.uniform(-0.4, 0.4), tilt=-0.08)


def glow_rune(k, faces):
    """Cyan glowing rune on the camera-facing side (concept: glowing rune stones)."""
    K.runestone_glyph_front(k, faces)


# ------------------------------------------------------------------ builders
def build_origin_lantern_row(k):
    """#2 Hàng đèn đá dẫn lối (light): five stone lanterns along a short worn path, warm glow cores."""
    anchors = {}
    xs = [-3.2, -1.6, 0.0, 1.6, 3.2]
    for i, x in enumerate(xs):
        y = 0.18 * math.sin(i * 1.3)
        base = k.box((x, y, 0.16), (0.55, 0.55, 0.32), "stone_blocks", bevel=0.04)
        glow_rune(k, base)
        dress(k, x, y, 0.0, scale=0.9)
        faces = list(base)
        faces += k.cone((x, y, 0.32), 0.13, 0.11, 0.5, "stone_warm", seg=8, smooth=False)
        faces += k.box((x, y, 0.92), (0.42, 0.42, 0.06), "stone_warm", bevel=0.02)
        for sx in (-1, 1):
            for sy in (-1, 1):
                faces += k.box((x + sx * 0.16, y + sy * 0.16, 1.08), (0.06, 0.06, 0.26), "stone_warm")
        k.blob((x, y, 1.08), (0.11, 0.11, 0.11), "glow_cap", subdiv=2, rough=0.05)
        faces += k.cone((x, y, 1.21), 0.36, 0.04, 0.34, "stone_warm", seg=8, smooth=False)
        k.cone((x, y, 1.53), 0.03, 0.005, 0.12, "gold_trim", seg=6)
        k.moss_tops(faces, 0.55, from_zones={"stone_warm", "stone_blocks"}, density=0.9)
        anchors[f"ANCHOR_Light_{i}"] = (x, y, 1.08)
    for i in range(9):  # path stones between the lanterns
        x = -3.6 + i * 0.9
        k.box((x, 0.75 + 0.06 * math.sin(i), 0.02), (0.55, 0.42, 0.05), "stone_blocks", rot=(0, 0, 0.2 * math.sin(i * 2)), bevel=0.02)
    anchors["ANCHOR_Approach"] = (0.0, 1.6, 0.0)    # where a dragon stands to look at the build
    anchors["ANCHOR_VFX_Complete"] = (0.0, 0.0, 1.9)
    return anchors


def build_ocean_coral_arch(k):
    """#10 Vòm san hô (landmark): a reef-rock arch grown over with branching coral and glowing anemone tips."""
    anchors = {}
    span = 1.9
    for s in (-1, 1):
        k.blob((s * span, 0, 0.45), (0.75, 0.7, 0.55), "stone_cool", subdiv=2, rough=0.28, flat_bottom=-0.6, smooth=False)
    n = 11
    for i in range(n):
        a = math.pi * (i + 0.5) / n
        cx, cz = math.cos(a) * span, 0.7 + math.sin(a) * 2.2
        f = k.blob((cx, 0, cz), (0.55, 0.5, 0.5), "stone_cool", subdiv=2, rough=0.22, smooth=False)
        k.moss_tops(f, 0.6, from_zones={"stone_cool"}, density=0.8)
    chains = []
    for i in range(7):  # coral branches growing out of the arch top and shoulders
        a = math.pi * (i + 0.5) / 7
        base = (math.cos(a) * span, 0.15, 0.7 + math.sin(a) * 2.2 + 0.25)
        tip = (base[0] * 1.15, 0.35, base[2] + 0.55)
        chains.append([(base, 0.08), (((base[0] + tip[0]) / 2, 0.25, (base[2] + tip[2]) / 2 + 0.1), 0.055), (tip, 0.03)])
        anchors[f"ANCHOR_Glow_{i}"] = tip
    me = K.skin_mesh(chains, subdiv=1, decimate=0.55, step=0.16)
    k.append_mesh(me, "mushroom_cap", smooth=True)
    bpy.data.meshes.remove(me)
    for i, (x, y) in enumerate(((-1.2, 0.7), (1.1, 0.8), (0.0, -0.6))):
        k.blob((x, y, 0.08), (0.22, 0.2, 0.12), "glow_cap", subdiv=1, rough=0.1)
    for sx in (-1, 1):
        dress(k, sx * span, 0.5, 0.0, flowers=("flower_blue", "flower_gold"), ivy=True, scale=1.3)
    k.card((-2.6, 0.2, 0), 0.9, 1.3, "F_fern", yaw=0.3)        # sea-fan cards reuse the ocean foliage cell
    k.card((2.5, -0.1, 0), 0.8, 1.1, "F_fern", yaw=-0.4)
    anchors["ANCHOR_SwimThrough"] = (0.0, 0.0, 1.2)  # centre of the opening, for swimmers
    anchors["ANCHOR_Approach"] = (0.0, 1.8, 0.0)
    anchors["ANCHOR_VFX_Complete"] = (0.0, 0.0, 3.2)
    return anchors


def build_sky_star_observatory(k):
    """#19 Đài ngắm sao (landmark): round stone platform, six slim columns, open dome ring and a floating star crystal."""
    anchors = {}
    faces = k.cone((0, 0, 0), 1.6, 1.5, 0.32, "stone_blocks", seg=14)
    faces += k.cone((0, 0, 0.32), 1.35, 1.3, 0.1, "stone_warm", seg=14)
    for i in range(3):  # steps toward the camera
        faces += k.box((0, 1.7 + 0.32 * i, 0.26 - 0.09 * i), (1.3 - 0.15 * i, 0.32, 0.09), "stone_blocks", bevel=0.02)
    for i in range(6):
        a = i * 2 * math.pi / 6 + math.pi / 6
        faces += k.cone((math.cos(a) * 1.1, math.sin(a) * 1.1, 0.42), 0.1, 0.085, 2.2, "stone_warm", seg=8)
        k.cone((math.cos(a) * 1.1, math.sin(a) * 1.1, 2.62), 0.13, 0.13, 0.06, "gold_trim", seg=8)
    faces += k.cone((0, 0, 2.68), 1.28, 1.24, 0.14, "stone_warm", seg=14)   # dome ring
    faces += k.cone((0, 0, 2.82), 1.0, 0.55, 0.5, "stone_warm", seg=14)      # stepped open roof
    k.cone((0, 0, 3.32), 0.55, 0.5, 0.08, "gold_trim", seg=14)
    k.cone((0, 0, 3.4), 0.12, 0.01, 0.6, "gold_trim", seg=6)                  # star finial
    k.blob((0, 0, 1.65), (0.26, 0.26, 0.34), "glow_cap", subdiv=2, rough=0.04)   # floating star crystal
    plinth = k.cone((0, 0, 0.42), 0.3, 0.24, 0.35, "rune_slate", seg=8)           # crystal plinth
    glow_rune(k, plinth)
    for i in range(6):
        a = i * 2 * math.pi / 6 + math.pi / 6
        dress(k, math.cos(a) * 1.25, math.sin(a) * 1.25, 0.42, flowers=("flower_pink", "flower_lilac"), ivy=(i % 2 == 0), scale=0.8)
    k.moss_tops(faces, 0.72, from_zones={"stone_warm", "stone_blocks"}, density=0.4)
    anchors["ANCHOR_Crystal"] = (0, 0, 1.65)
    anchors["ANCHOR_Perch_0"] = (math.cos(math.pi / 6) * 1.1, math.sin(math.pi / 6) * 1.1, 2.75)
    anchors["ANCHOR_Perch_1"] = (-math.cos(math.pi / 6) * 1.1, math.sin(math.pi / 6) * 1.1, 2.75)
    anchors["ANCHOR_Approach"] = (0.0, 2.6, 0.0)
    anchors["ANCHOR_VFX_Complete"] = (0.0, 0.0, 3.9)
    return anchors


SAMPLES = [
    # (projectId, Domain index, world, kind, builder, texture source dir, slot prefix)
    ("origin_lantern_row", 1, "origin", "light", build_origin_lantern_row),
    ("ocean_coral_arch", 9, "ocean", "landmark", build_ocean_coral_arch),
    ("sky_star_observatory", 18, "sky", "landmark", build_sky_star_observatory),
]

TEX_DIRS = {
    "origin": REPO / "Assets/Dravio/Environment/Textures",
    "ocean": REPO / "Assets/Dravio/Environment/Zones/Ocean/Textures",
    "sky": REPO / "Assets/Dravio/Environment/Zones/Sky/Textures",
}


def setup_materials(world):
    if world == "origin":
        K.MATERIALS.clear()
        K.make_materials(TEX_DIRS["origin"])
    else:
        ZK.make_zone_materials(world, TEX_DIRS[world])


def lod_copy(obj, ratio, name):
    me = obj.data.copy()
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    if ratio < 1.0:
        mod = o.modifiers.new("Decimate", 'DECIMATE')
        mod.decimate_type = 'COLLAPSE'
        mod.ratio = ratio
        mod.use_collapse_triangulate = True
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.select_all(action='DESELECT')
        o.select_set(True)
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return o


def tris(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


def build_sample(project_id, index, world, kind, fn, out_dir):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    setup_materials(world)
    name = f"SM_SANC_{project_id}"
    k = K.Kit(name, seed=9100 + index * 31)
    anchors = fn(k)
    base = K.to_object(k)
    uv_errors = ZK.validate_uvs(base)
    lods = []
    for i, r in enumerate(LOD_RATIOS):
        lods.append(lod_copy(base, r, f"{name}_LOD{i}"))
    bpy.data.objects.remove(base)
    empties = []
    for an, p in anchors.items():
        e = bpy.data.objects.new(an, None)
        e.empty_display_type = 'PLAIN_AXES'
        e.empty_display_size = 0.2
        e.location = p
        bpy.context.scene.collection.objects.link(e)
        empties.append(e)
    lod0 = lods[0]
    bb = [Vector(c) for c in lod0.bound_box]
    mn = Vector((min(c.x for c in bb), min(c.y for c in bb), min(c.z for c in bb)))
    mx = Vector((max(c.x for c in bb), max(c.y for c in bb), max(c.z for c in bb)))
    fbx = out_dir / "Models" / f"{name}.fbx"
    fbx.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    for o in lods + empties:
        o.select_set(True)
    bpy.context.view_layer.objects.active = lod0
    bpy.ops.export_scene.fbx(
        filepath=str(fbx), use_selection=True, object_types={'MESH', 'EMPTY'},
        axis_forward='-Z', axis_up='Y', apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
        bake_space_transform=True, mesh_smooth_type='FACE', use_mesh_modifiers=True, use_tspace=False,
        colors_type='SRGB', prioritize_active_color=True, add_leaf_bones=False, bake_anim=False,
        path_mode='STRIP', embed_textures=False, use_custom_props=False)
    size = mx - mn
    meta = {
        "generator": "scripts/art/environment/build_sanctuary_projects.py",
        "blender": bpy.app.version_string,
        "status": "style-sample (not accepted; art-direction review)",
        "projectId": project_id,
        "domainIndex": index,
        "world": world,
        "kind": kind,
        "prefabId": f"PF_SANC_{project_id}",
        "model": f"Models/{name}.fbx",
        "materials": sorted({m.name for m in lod0.data.materials}),
        "lods": [{"mesh": o.name, "triangles": tris(o), "screenRelativeHeight": h} for o, h in zip(lods, LOD_SCREEN)],
        "lod0Budget": BUDGET_LOD0,
        "uvErrors": uv_errors,
        "boundsUnity": {"center": unity((mn + mx) / 2), "size": [round(size.x, 3), round(size.z, 3), round(size.y, 3)]},
        "footprintRadius_m": round(max(size.x, size.y) / 2, 3),
        "pivot": "bottom centre (ground y = 0)",
        "front": "-Z (faces the Sanctuary camera)",
        "anchors": {an: unity(p) for an, p in sorted(anchors.items())},
        "notDecidedHere": ["cost", "buildMaterials", "buffs", "rewards"],
    }
    data = out_dir / "Data" / f"{project_id}.sanctuary.json"
    data.parent.mkdir(parents=True, exist_ok=True)
    data.write_text(json.dumps(meta, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    bpy.ops.wm.save_as_mainfile(filepath=str(out_dir / f"{name}.blend"))
    failures = []
    if meta["lods"][0]["triangles"] > BUDGET_LOD0:
        failures.append(f"{name}: LOD0 {meta['lods'][0]['triangles']} > {BUDGET_LOD0}")
    if uv_errors:
        failures.append(f"{name}: {uv_errors} faces with UVs outside their atlas tile")
    print(f"[sanctuary] {name}: LOD {[l['triangles'] for l in meta['lods']]} size {meta['boundsUnity']['size']} {meta['materials']}")
    if PREVIEW:
        render_preview(lod0, empties, out_dir / f"{name}_preview.png")
    return meta, failures


def render_preview(obj, empties, path):
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    sc.cycles.samples = 32
    sc.render.resolution_x, sc.render.resolution_y = 1280, 800
    w = bpy.data.worlds.new("w")
    sc.world = w
    w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (0.75, 0.82, 0.9, 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = 0.9
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", 'SUN'))
    sun.data.energy = 3.0
    sun.rotation_euler = (0.9, 0.1, 0.7)
    sc.collection.objects.link(sun)
    for o in bpy.data.objects:
        if o.type == 'MESH' and o is not obj:
            o.hide_render = True
    d = max(obj.dimensions)
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    sc.collection.objects.link(cam)
    sc.camera = cam
    bb = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    target = sum(bb, Vector()) / 8.0
    cam.data.lens = 50
    cam.location = target + Vector((0.35, 0.85, 0.42)).normalized() * d * 2.1   # camera side is Blender +Y
    cam.rotation_euler = (target - cam.location).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    metas, failures = [], []
    for pid, idx, world, kind, fn in SAMPLES:
        m, f = build_sample(pid, idx, world, kind, fn, OUT)
        metas.append(m)
        failures += f
    (OUT / "Data").mkdir(parents=True, exist_ok=True)
    (OUT / "Data" / "SanctuarySamplesManifest.json").write_text(json.dumps(metas, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
    if UNITY:
        dst = Path(UNITY).resolve()
        for sub in ("Models", "Data"):
            (dst / sub).mkdir(parents=True, exist_ok=True)
            for p in (OUT / sub).glob("*"):
                t = dst / sub / p.name
                if t.exists():
                    print("[sanctuary] exists, NOT overwritten:", t)
                    continue
                shutil.copy2(p, t)
        print("[sanctuary] copied into", dst)
    for f in failures:
        print("[sanctuary] FAILURE:", f)
    if failures:
        raise SystemExit(1)
    print("[sanctuary] done ->", OUT)


if __name__ == "__main__":
    main()
