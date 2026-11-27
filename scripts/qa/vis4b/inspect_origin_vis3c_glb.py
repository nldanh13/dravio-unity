"""VIS.4b (issue #91) - technical inspection of the ArtSource/Origin_VIS3c GLB drafts.

Blender 5.3 (or the `bpy` 5.3 wheel) only; reads the GLBs, writes nothing into Assets.

  python scripts/qa/vis4b/inspect_origin_vis3c_glb.py -- --out <staging dir> [glb ...]

Per asset: triangle/vertex count vs manifest, welded topology (non-manifold / boundary
edges, loose parts), degenerate faces, stored-normal vs winding agreement, outward-facing
check on the welded mesh, vertex colour layer, pivot/base, front (+Z glTF = -Y Blender)
and bounds. Also renders a backface-tinted check sheet (red = back face visible) from the
Origin camera pitch (32 deg, orthographic) and a 3/4 view, Cycles CPU, NOT a Unity capture.
"""
import json
import math
import sys
from pathlib import Path

import bpy  # noqa: I001  (bpy must be imported before bmesh/mathutils)
import bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree

ROOT = Path(__file__).resolve().parents[3]
SRC = ROOT / "ArtSource" / "Origin_VIS3c"
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = Path(argv[argv.index("--out") + 1]).resolve()
FILES = [Path(a).resolve() for a in argv if a.endswith(".glb")] or sorted(SRC.glob("*.glb"))
if ROOT / "Assets" in OUT.parents or OUT == ROOT / "Assets":
    raise SystemExit("Write QA output outside Unity Assets.")
OUT.mkdir(parents=True, exist_ok=True)
manifest = {}
for d in {f.resolve().parent for f in FILES}:
    if (d / "manifest.json").exists():
        manifest.update({a["name"]: a for a in json.loads((d / "manifest.json").read_text())["assets"]})


def import_glb(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(path))
    meshes = [o for o in bpy.data.objects if o.type == 'MESH']
    bpy.context.view_layer.update()
    return meshes


def inspect(path):
    meshes = import_glb(path)
    r = {"file": str(path.relative_to(ROOT)), "objects": len(meshes),
         "materials": sorted({s.material.name for o in meshes for s in o.material_slots if s.material})}
    tris = verts = degenerate = winding_mismatch = 0
    color_layers = set()
    col_min, col_max, alpha_vals = [1e9] * 3, [-1e9] * 3, set()
    world_pts = []
    bm_all = bmesh.new()
    for o in meshes:
        me = o.data
        me.calc_loop_triangles()
        tris += len(me.loop_triangles)
        verts += len(me.vertices)
        for a in me.color_attributes:
            color_layers.add(f"{a.name}:{a.domain}:{a.data_type}")
            for d in a.data:
                c = d.color
                for i in range(3):
                    col_min[i] = min(col_min[i], c[i]); col_max[i] = max(col_max[i], c[i])
                alpha_vals.add(round(c[3], 3))
        cn = me.corner_normals
        for p in me.polygons:
            if p.area <= 1e-10:
                degenerate += 1
            stored = Vector((0, 0, 0))
            for li in p.loop_indices:
                stored += Vector(cn[li].vector)
            if stored.length > 0 and stored.dot(p.normal) < 0:
                winding_mismatch += 1
        world_pts += [o.matrix_world @ v.co for v in me.vertices]
        tmp = me.copy()
        tmp.transform(o.matrix_world)
        bm_all.from_mesh(tmp)
        bpy.data.meshes.remove(tmp)
    lo = Vector([min(p[i] for p in world_pts) for i in range(3)])
    hi = Vector([max(p[i] for p in world_pts) for i in range(3)])

    # Weld (glTF flat-shaded export duplicates every corner) and look at real topology.
    bmesh.ops.remove_doubles(bm_all, verts=bm_all.verts, dist=1e-5)
    bm_all.edges.ensure_lookup_table(); bm_all.faces.ensure_lookup_table()
    nonmanifold = sum(1 for e in bm_all.edges if len(e.link_faces) > 2)
    boundary = sum(1 for e in bm_all.edges if len(e.link_faces) == 1)
    # Islands.
    seen, islands = set(), 0
    for f in bm_all.faces:
        if f.index in seen:
            continue
        islands += 1
        stack = [f]
        while stack:
            g = stack.pop()
            if g.index in seen:
                continue
            seen.add(g.index)
            for e in g.edges:
                stack += [h for h in e.link_faces if h.index not in seen]
    # Orientation per island: the signed volume of a closed shell is > 0 only when its
    # normals point outward. Open islands (boundary edges) are reported separately.
    island_of = {}
    for f in bm_all.faces:
        if f.index in island_of:
            continue
        stack, k = [f], len(set(island_of.values()))
        while stack:
            g = stack.pop()
            if g.index in island_of:
                continue
            island_of[g.index] = k
            for e in g.edges:
                stack += [h for h in e.link_faces if h.index not in island_of]
    vol, open_isl = {}, set()
    for f in bm_all.faces:
        k = island_of[f.index]
        vs = [v.co for v in f.verts]
        for i in range(1, len(vs) - 1):
            vol[k] = vol.get(k, 0.0) + vs[0].dot(vs[i].cross(vs[i + 1])) / 6.0
        if any(len(e.link_faces) == 1 for e in f.edges):
            open_isl.add(k)
    # Winding consistency: two faces sharing an edge must traverse it in opposite
    # directions. Same-direction pairs mean locally flipped faces even when the island's
    # total volume is positive.
    flipped_edges, flipped_faces = 0, set()
    for e in bm_all.edges:
        if len(e.link_faces) != 2:
            continue
        dirs = []
        for f in e.link_faces:
            for lp in f.loops:
                if lp.edge == e:
                    dirs.append(lp.vert == e.verts[0])
        if dirs[0] == dirs[1]:
            flipped_edges += 1
            flipped_faces.update(f.index for f in e.link_faces)
    closed = [k for k in vol if k not in open_isl]
    inverted_closed = [k for k in closed if vol[k] < 0]
    # Outward check: from each face centre step out along its normal and cast a ray
    # further out. A face whose outward ray is captured by geometry that it sees from
    # the BACK side is enclosed by its own shell, i.e. likely inverted / interior.
    bvh = BVHTree.FromBMesh(bm_all)
    inward = 0
    for f in bm_all.faces:
        if f.calc_area() <= 1e-10:
            continue
        c = f.calc_center_median()
        n = f.normal
        hit, hn, idx, dist = bvh.ray_cast(c + n * 1e-3, n, 50.0)
        if hit is not None and hn.dot(n) > 0.0:
            inward += 1
    faces = len(bm_all.faces)
    bm_all.free()

    m = manifest.get(path.stem, {})
    # glTF -> Blender: (x, y, z)_gltf -> (x, -z, y); glTF min/max y = Blender z.
    gl_lo = [lo.x, lo.z, -hi.y]; gl_hi = [hi.x, hi.z, -lo.y]
    r.update({
        "triangles": tris, "vertices": verts,
        "manifest_triangles": m.get("triangles"), "manifest_vertices": m.get("vertices"),
        "bounds_gltf_min": [round(v, 4) for v in gl_lo], "bounds_gltf_max": [round(v, 4) for v in gl_hi],
        "manifest_bounds": m.get("bounds"),
        "size_m": [round(hi[i] - lo[i], 3) for i in range(3)],
        "pivot_base_y": round(gl_lo[1], 4),
        "pivot_center_xz": [round((gl_lo[0] + gl_hi[0]) / 2, 4), round((gl_lo[2] + gl_hi[2]) / 2, 4)],
        "degenerate_faces": degenerate,
        "normal_vs_winding_mismatch": winding_mismatch,
        "welded_faces": faces, "islands": islands,
        "nonmanifold_edges": nonmanifold, "boundary_edges": boundary,
        "closed_islands": len(closed), "open_islands": len(open_isl),
        "inverted_closed_islands": len(inverted_closed),
        "winding_inconsistent_edges": flipped_edges,
        "faces_touching_inconsistent_edges": len(flipped_faces),
        "inverted_island_volumes_m3": [round(vol[k], 4) for k in inverted_closed][:10],
        "faces_buried_inside_other_shells": inward,
        "faces_buried_pct": round(100.0 * inward / max(faces, 1), 2),
        "color_layers": sorted(color_layers),
        "color_rgb_min": [round(v, 3) for v in col_min], "color_rgb_max": [round(v, 3) for v in col_max],
        "color_alpha_values": sorted(alpha_vals)[:8],
        "uv_layers": sorted({uv.name for o in meshes for uv in o.data.uv_layers}),
    })
    return r


def backface_material():
    m = bpy.data.materials.new("QA_Backface")
    nt = m.node_tree; nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    vc = nt.nodes.new("ShaderNodeVertexColor")
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    mix = nt.nodes.new("ShaderNodeMix"); mix.data_type = 'RGBA'
    mix.inputs[7].default_value = (1.0, 0.0, 0.0, 1)
    nt.links.new(geo.outputs["Backfacing"], mix.inputs["Factor"])
    nt.links.new(vc.outputs["Color"], mix.inputs[6])
    bsdf = nt.nodes.new("ShaderNodeBsdfDiffuse")
    nt.links.new(mix.outputs[2], bsdf.inputs["Color"])
    nt.links.new(bsdf.outputs[0], out.inputs["Surface"])
    return m


def render(path):
    meshes = import_glb(path)
    mat = backface_material()
    for o in meshes:
        o.data.materials.clear(); o.data.materials.append(mat)
    pts = [o.matrix_world @ v.co for o in meshes for v in o.data.vertices]
    lo = Vector([min(p[i] for p in pts) for i in range(3)])
    hi = Vector([max(p[i] for p in pts) for i in range(3)])
    ctr, rad = (lo + hi) / 2, (hi - lo).length / 2
    scn = bpy.context.scene
    ground = bpy.data.meshes.new("g"); g = bpy.data.objects.new("g", ground)
    bm = bmesh.new(); bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=rad * 3); bm.to_mesh(ground); bm.free()
    gm = bpy.data.materials.new("gm"); gm.node_tree.nodes["Principled BSDF"].inputs[0].default_value = (0.45, 0.55, 0.35, 1)
    ground.materials.append(gm); scn.collection.objects.link(g)
    sd = bpy.data.lights.new("s", 'SUN'); sd.energy = 3.5; sd.angle = math.radians(3)
    s = bpy.data.objects.new("s", sd); scn.collection.objects.link(s)
    s.rotation_euler = (math.radians(44), 0, math.radians(-38))
    w = bpy.data.worlds.new("w"); scn.world = w
    w.node_tree.nodes["Background"].inputs[0].default_value = (0.62, 0.73, 0.88, 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = 0.8
    scn.render.engine = 'CYCLES'; scn.cycles.samples = 24; scn.cycles.use_denoising = False
    scn.view_settings.view_transform = 'Standard'
    scn.render.resolution_x = 640; scn.render.resolution_y = 480
    cd = bpy.data.cameras.new("c"); cd.type = 'ORTHO'; cd.ortho_scale = rad * 2.3
    cam = bpy.data.objects.new("c", cd); scn.collection.objects.link(cam); scn.camera = cam
    shots = []
    for label, yaw in (("origin_cam_front", 0.0), ("three_quarter", 35.0), ("back", 180.0)):
        p = math.radians(32.0)
        y = math.radians(yaw)
        d = Vector((math.sin(y) * math.cos(p), -math.cos(y) * math.cos(p), math.sin(p)))
        cam.location = ctr + d * rad * 5
        cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
        f = OUT / f"{path.stem}_{label}.png"
        scn.render.filepath = str(f)
        bpy.ops.render.render(write_still=True)
        shots.append(str(f))
    return shots


report = {"blender": bpy.app.version_string, "assets": []}
for f in FILES:
    r = inspect(f)
    r["renders"] = render(f)
    report["assets"].append(r)
    print(json.dumps({k: v for k, v in r.items() if k != "renders"}))
(OUT / "vis3c_glb_inspection.json").write_text(json.dumps(report, indent=2))
print("[VIS.4b] wrote", OUT / "vis3c_glb_inspection.json")
