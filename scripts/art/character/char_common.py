"""Shared helpers for the Dravio character asset pipeline (Blender 5.x / bpy).

Everything here is read-only with respect to source files: loaders import into
an empty Blender scene, and writers refuse to touch the source path.
"""
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
import bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree

SUPPORTED = {'.glb', '.gltf', '.fbx', '.blend'}


def script_args():
    """Arguments after `--` when run by Blender, or argv[1:] when run with the bpy module."""
    return sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else sys.argv[1:]


def sha256(path):
    h = hashlib.sha256()
    with open(path, 'rb') as f:
        for chunk in iter(lambda: f.read(1 << 20), b''):
            h.update(chunk)
    return h.hexdigest()


def guard_output(source, output, overwrite=False):
    """Never write onto the source model; never silently replace an earlier output."""
    source = Path(source).resolve()
    output = Path(output).resolve()
    if output == source:
        raise SystemExit(f"Refusing to overwrite the source model: {source}")
    if output.exists() and not overwrite:
        raise SystemExit(f"Output exists (pass --overwrite-outputs to replace a previous OUTPUT): {output}")
    output.parent.mkdir(parents=True, exist_ok=True)
    return output


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def load_model(path, merge_vertices=False):
    """Import a model into an empty scene. .blend files are opened (never saved back).

    glTF stores one vertex per unique (position, normal, UV, colour) tuple, so a GLB
    imported without merging is split along every UV/normal seam. Processing tools
    pass merge_vertices=True to recover connected topology; UVs stay per-loop."""
    path = Path(path).resolve()
    ext = path.suffix.lower()
    if ext not in SUPPORTED:
        raise SystemExit(f"Unsupported model type {ext}; expected one of {sorted(SUPPORTED)}")
    if ext == '.blend':
        bpy.ops.wm.open_mainfile(filepath=str(path))
        # Detach from the source path so an accidental save cannot overwrite it.
        bpy.context.preferences.filepaths.use_auto_save_temporary_files = False
        return
    reset_scene()
    if ext in {'.glb', '.gltf'}:
        result = bpy.ops.import_scene.gltf(filepath=str(path), merge_vertices=merge_vertices)
    else:
        result = bpy.ops.import_scene.fbx(filepath=str(path))
    if 'FINISHED' not in result:
        raise RuntimeError(f"Import failed: {path}")
    bpy.context.view_layer.update()


def mesh_objects():
    return [o for o in bpy.context.scene.objects if o.type == 'MESH']


def triangle_count(mesh):
    return sum(max(0, len(p.vertices) - 2) for p in mesh.polygons)


def evaluated_world_coords(obj, depsgraph=None):
    """Triangulated, evaluated (modifiers/armature applied) world-space geometry."""
    depsgraph = depsgraph or bpy.context.evaluated_depsgraph_get()
    ev = obj.evaluated_get(depsgraph)
    me = ev.to_mesh()
    try:
        me.calc_loop_triangles()
        mw = obj.matrix_world
        verts = [mw @ v.co for v in me.vertices]
        tris = [tuple(t.vertices) for t in me.loop_triangles]
    finally:
        ev.to_mesh_clear()
    return verts, tris


def bvh_for(objs, depsgraph=None):
    verts, tris = [], []
    for o in objs:
        v, t = evaluated_world_coords(o, depsgraph)
        base = len(verts)
        verts.extend(v)
        tris.extend(tuple(i + base for i in tri) for tri in t)
    return BVHTree.FromPolygons(verts, tris, all_triangles=True), verts


def bounds_of(points):
    lo = Vector(tuple(min(p[i] for p in points) for i in range(3)))
    hi = Vector(tuple(max(p[i] for p in points) for i in range(3)))
    return lo, hi


def surface_deviation(objs_a, objs_b, depsgraph=None, samples=None):
    """Two-sided vertex-to-surface distance between object sets, in metres and
    as a fraction of A's bounding-box diagonal. Not a full Hausdorff on faces,
    but symmetric over both vertex sets, which is what silhouettes depend on."""
    bvh_a, va = bvh_for(objs_a, depsgraph)
    bvh_b, vb = bvh_for(objs_b, depsgraph)
    lo, hi = bounds_of(va)
    diag = max((hi - lo).length, 1e-9)

    def dists(points, bvh):
        step = max(1, len(points) // samples) if samples else 1
        out = []
        for p in points[::step]:
            hit = bvh.find_nearest(p)
            out.append((hit[0] - p).length if hit[0] is not None else float('inf'))
        return out

    d = dists(vb, bvh_a) + dists(va, bvh_b)
    d.sort()
    mean = sum(d) / len(d)
    p95 = d[int(0.95 * (len(d) - 1))]
    mx = d[-1]
    return {'mean_m': mean, 'p95_m': p95, 'max_m': mx,
            'mean_rel': mean / diag, 'p95_rel': p95 / diag, 'max_rel': mx / diag,
            'bbox_diag_m': diag}


def principled_inputs(material):
    """Describe what drives Base Color / Normal / Roughness / Metallic of a material."""
    info = {'name': material.name if material else None, 'principled': False}
    if not material or not material.use_nodes or not material.node_tree:
        return info
    nodes = material.node_tree.nodes
    bsdf = next((n for n in nodes if n.type == 'BSDF_PRINCIPLED'), None)
    if bsdf is None:
        return info
    info['principled'] = True

    def upstream(socket, depth=0):
        if not socket.is_linked or depth > 6:
            return None
        node = socket.links[0].from_node
        if node.type == 'TEX_IMAGE':
            img = node.image
            return {'kind': 'image', 'image': img.name if img else None,
                    'size': list(img.size) if img else None,
                    'colorspace': img.colorspace_settings.name if img else None,
                    'channel': socket.links[0].from_socket.name}
        if node.type in {'VERTEX_COLOR', 'ATTRIBUTE'}:
            return {'kind': 'vertex_color',
                    'attribute': getattr(node, 'layer_name', None) or getattr(node, 'attribute_name', None)}
        if node.type == 'NORMAL_MAP':
            inner = upstream(node.inputs['Color'], depth + 1)
            return {'kind': 'normal_map', 'space': node.space, 'source': inner}
        for inp in node.inputs:
            r = upstream(inp, depth + 1)
            if r:
                r = dict(r)
                r.setdefault('via', []).append(node.type)
                return r
        return {'kind': node.type}

    def value(name):
        sock = bsdf.inputs.get(name)
        if sock is None:
            return None
        link = upstream(sock)
        if link:
            return link
        v = sock.default_value
        try:
            return {'kind': 'constant', 'value': [round(x, 4) for x in v]}
        except TypeError:
            return {'kind': 'constant', 'value': round(float(v), 4)}

    for key, sock in [('base_color', 'Base Color'), ('normal', 'Normal'),
                      ('roughness', 'Roughness'), ('metallic', 'Metallic'),
                      ('alpha', 'Alpha'), ('emission', 'Emission Color')]:
        info[key] = value(sock)
    return info


def uv_stats(mesh, layer_name):
    """UV coverage/health on one UV map, computed with bmesh."""
    bm = bmesh.new()
    bm.from_mesh(mesh)
    try:
        uv = bm.loops.layers.uv.get(layer_name)
        if uv is None:
            return None
        total3d = 0.0
        total_uv = 0.0
        zero_area = 0
        flipped = 0
        outside = 0
        stretch = []
        bm.faces.ensure_lookup_table()
        for f in bm.faces:
            a3 = f.calc_area()
            pts = [l[uv].uv for l in f.loops]
            # signed shoelace area
            s = 0.0
            for i in range(len(pts)):
                x1, y1 = pts[i]
                x2, y2 = pts[(i + 1) % len(pts)]
                s += x1 * y2 - x2 * y1
            s *= 0.5
            if abs(s) < 1e-12:
                zero_area += 1
            elif s < 0:
                flipped += 1
            if any(p.x < -1e-4 or p.x > 1 + 1e-4 or p.y < -1e-4 or p.y > 1 + 1e-4 for p in pts):
                outside += 1
            total3d += a3
            total_uv += abs(s)
            stretch.append((a3, abs(s)))
        # area-distortion: ratio of (uv share / 3d share), p95 deviation from 1
        ratios = sorted((u / total_uv) / (a / total3d) for a, u in stretch
                        if a > 0 and u > 0 and total_uv > 0 and total3d > 0)
        n = len(bm.faces)

        def pct(q):
            return ratios[int(q * (len(ratios) - 1))] if ratios else None

        return {'faces': n, 'uv_area_used': round(total_uv, 5),
                'zero_area_faces': zero_area, 'flipped_faces': flipped,
                'faces_outside_0_1': outside,
                'area_ratio_p05': pct(.05), 'area_ratio_p50': pct(.5), 'area_ratio_p95': pct(.95)}
    finally:
        bm.free()


def topology_stats(mesh, weld=False):
    """Topology health. weld=True first merges coincident vertices (distance 1e-6) on a
    temporary copy, separating real holes/islands from glTF attribute-seam splits."""
    bm = bmesh.new()
    bm.from_mesh(mesh)
    if weld:
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
    bm.verts.index_update()
    try:
        non_manifold = sum(1 for e in bm.edges if not e.is_manifold)
        boundary = sum(1 for e in bm.edges if e.is_boundary)
        loose_verts = sum(1 for v in bm.verts if not v.link_edges)
        degenerate = sum(1 for f in bm.faces if f.calc_area() < 1e-12)
        ngons = sum(1 for f in bm.faces if len(f.verts) > 4)
        quads = sum(1 for f in bm.faces if len(f.verts) == 4)
        tris = sum(1 for f in bm.faces if len(f.verts) == 3)
        # connected components (islands)
        seen = set()
        islands = 0
        for v in bm.verts:
            if v.index in seen:
                continue
            islands += 1
            stack = [v]
            seen.add(v.index)
            while stack:
                cur = stack.pop()
                for e in cur.link_edges:
                    o = e.other_vert(cur)
                    if o.index not in seen:
                        seen.add(o.index)
                        stack.append(o)
        return {'non_manifold_edges': non_manifold, 'boundary_edges': boundary,
                'loose_vertices': loose_verts, 'degenerate_faces': degenerate,
                'ngons': ngons, 'quads': quads, 'triangles_faces': tris, 'islands': islands}
    finally:
        bm.free()


def skin_stats(obj):
    arm_mod = next((m for m in obj.modifiers if m.type == 'ARMATURE' and m.object), None)
    if arm_mod is None:
        return None
    deform = {b.name for b in arm_mod.object.data.bones if b.use_deform}
    names = {g.index: g.name for g in obj.vertex_groups}
    max_inf = 0
    unweighted = 0
    not_normalized = 0
    for v in obj.data.vertices:
        ws = [g.weight for g in v.groups if names.get(g.group) in deform and g.weight > 1e-6]
        max_inf = max(max_inf, len(ws))
        if not ws:
            unweighted += 1
        elif abs(sum(ws) - 1.0) > 1e-3:
            not_normalized += 1
    return {'armature': arm_mod.object.name, 'bones': len(arm_mod.object.data.bones),
            'deform_bones': len(deform), 'max_influences': max_inf,
            'unweighted_vertices': unweighted, 'not_normalized_vertices': not_normalized}


def write_json(path, data):
    Path(path).write_text(json.dumps(data, indent=2, ensure_ascii=False, default=_json_default),
                          encoding='utf-8')


def _json_default(o):
    if isinstance(o, Vector):
        return [round(x, 6) for x in o]
    if isinstance(o, Path):
        return str(o)
    if isinstance(o, float) and not math.isfinite(o):
        return None
    raise TypeError(type(o))


def weld_coincident_boundaries(obj, rel_dist=1e-6):
    """Weld open-boundary vertices that share a position (glTF hard-edge/UV splits).

    UVs and colours are per-loop in Blender, so they survive. Where the two copies
    had different normals (a hard edge in the source) the welded edge is marked
    sharp so shading is kept. Returns the number of vertices welded.
    """
    me = obj.data
    bm = bmesh.new()
    bm.from_mesh(me)
    try:
        lo, hi = bounds_of([v.co for v in bm.verts]) if bm.verts else (Vector(), Vector())
        dist = max((hi - lo).length * rel_dist, 1e-8)
        boundary = [v for v in bm.verts if v.is_boundary]
        if not boundary:
            return 0
        normals = {v: v.normal.copy() for v in boundary}
        targetmap = bmesh.ops.find_doubles(bm, verts=boundary, dist=dist)['targetmap']
        if not targetmap:
            return 0
        hard = {t for v, t in targetmap.items() if normals[v].angle(normals[t], 0.0) > math.radians(1.0)}
        bmesh.ops.weld_verts(bm, targetmap=targetmap)
        for e in bm.edges:
            if e.verts[0] in hard and e.verts[1] in hard:
                e.smooth = False
        bm.to_mesh(me)
        me.update()
        return len(targetmap)
    finally:
        bm.free()


def image_to_array(img):
    """Image pixels as float32 (h, w, 4). Byte images are display (sRGB) encoded, float images linear."""
    import numpy as np
    w, h = img.size
    arr = np.empty(w * h * 4, dtype=np.float32)
    img.pixels.foreach_get(arr)
    return arr.reshape(h, w, 4)


def sample_bilinear(arr, uvs):
    """Bilinear sample with UV wrap=clamp; uvs is (n, 2) in [0,1]; returns (n, 4)."""
    import numpy as np
    h, w = arr.shape[:2]
    x = np.clip(uvs[:, 0] * w - 0.5, 0, w - 1)
    y = np.clip(uvs[:, 1] * h - 0.5, 0, h - 1)
    x0 = np.floor(x).astype(int); y0 = np.floor(y).astype(int)
    x1 = np.minimum(x0 + 1, w - 1); y1 = np.minimum(y0 + 1, h - 1)
    fx = (x - x0)[:, None]; fy = (y - y0)[:, None]
    return (arr[y0, x0] * (1 - fx) * (1 - fy) + arr[y0, x1] * fx * (1 - fy)
            + arr[y1, x0] * (1 - fx) * fy + arr[y1, x1] * fx * fy)


def linear_to_srgb(c):
    import numpy as np
    c = np.clip(c, 0, 1)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * np.power(c, 1 / 2.4) - 0.055)


def srgb_to_linear(c):
    import numpy as np
    c = np.clip(c, 0, 1)
    return np.where(c <= 0.04045, c / 12.92, np.power((c + 0.055) / 1.055, 2.4))
