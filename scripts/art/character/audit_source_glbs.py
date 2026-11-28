"""CHAR.9c — read-only audit of the original dragon GLBs (ArtSource/Dragons/images/NN_<species>.glb).

  python scripts/art/character/audit_source_glbs.py <glb_dir> <out.json> [--md out.md]
  (bpy 5.3 module, or blender --background --python <this> -- <args>)

Two independent measurements per file:
  * glTF container (pure Python, no Blender): generator, nodes, meshes/primitives, attributes (POSITION/NORMAL/
    TEXCOORD_n/COLOR_n/JOINTS/WEIGHTS), skins, animations, materials, textures/images and their pixel sizes.
  * Blender import (welded copy in memory, source never written): triangles, vertices before/after position weld
    (glTF splits every UV/normal seam), boundary + non-manifold edges after weld, loose islands, degenerate faces,
    signed volume (negative = inside-out winding), share of faces whose normal points inward (ray-free test:
    face normal vs. direction from mesh centroid, informative only for convex-ish bodies), UV-flipped faces
    (negative UV area), UV out of [0,1], bounding box, ground offset (min Z) and pivot offset from footprint centre.
The source SHA-256 is checked before and after: the script never writes to its input.
"""
import argparse
import hashlib
import json
import struct
import sys
from pathlib import Path

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else sys.argv[1:]
ap = argparse.ArgumentParser()
ap.add_argument('glb_dir', type=Path)
ap.add_argument('out_json', type=Path)
ap.add_argument('--md', type=Path)
A = ap.parse_args(argv)


def sha256(p):
    return hashlib.sha256(Path(p).read_bytes()).hexdigest()


def png_size(b):
    if b[:8] == b'\x89PNG\r\n\x1a\n':
        return struct.unpack('>II', b[16:24])
    if b[:2] == b'\xff\xd8':  # JPEG: walk segments to SOF
        i = 2
        while i < len(b):
            if b[i] != 0xFF:
                i += 1
                continue
            m = b[i + 1]
            if m in (0xC0, 0xC1, 0xC2):
                h, w = struct.unpack('>HH', b[i + 5:i + 9])
                return w, h
            i += 2 + struct.unpack('>H', b[i + 2:i + 4])[0]
    return None


def read_glb(p):
    data = Path(p).read_bytes()
    magic, ver, length = struct.unpack('<4sII', data[:12])
    assert magic == b'glTF', 'not a GLB'
    off, js, binc = 12, None, b''
    while off < length:
        clen, ctype = struct.unpack('<II', data[off:off + 8])
        chunk = data[off + 8:off + 8 + clen]
        if ctype == 0x4E4F534A:
            js = json.loads(chunk)
        elif ctype == 0x004E4942:
            binc = chunk
        off += 8 + clen
    return js, binc


def container(p):
    j, binc = read_glb(p)
    prims, attrs = [], set()
    for m in j.get('meshes', []):
        for pr in m['primitives']:
            prims.append(pr)
            attrs |= set(pr['attributes'])
    imgs = []
    for im in j.get('images', []):
        size = None
        if 'bufferView' in im:
            bv = j['bufferViews'][im['bufferView']]
            raw = binc[bv.get('byteOffset', 0):bv.get('byteOffset', 0) + bv['byteLength']]
            size = png_size(raw)
        imgs.append({'mime': im.get('mimeType'), 'size': list(size) if size else None})
    mats = []
    for m in j.get('materials', []):
        pbr = m.get('pbrMetallicRoughness', {})
        mats.append({'name': m.get('name'), 'baseColorTexture': 'baseColorTexture' in pbr,
                     'baseColorFactor': pbr.get('baseColorFactor'), 'metallic': pbr.get('metallicFactor', 1.0),
                     'roughness': pbr.get('roughnessFactor', 1.0), 'metallicRoughnessTexture': 'metallicRoughnessTexture' in pbr,
                     'normalTexture': 'normalTexture' in m, 'doubleSided': m.get('doubleSided', False),
                     'alphaMode': m.get('alphaMode', 'OPAQUE')})
    return {'generator': j.get('asset', {}).get('generator'), 'nodes': len(j.get('nodes', [])),
            'meshes': len(j.get('meshes', [])), 'primitives': len(prims), 'attributes': sorted(attrs),
            'skins': len(j.get('skins', [])), 'animations': len(j.get('animations', [])),
            'materials': mats, 'textures': len(j.get('textures', [])), 'images': imgs,
            'extensionsUsed': j.get('extensionsUsed', [])}


def blender_measure(p):
    import bpy
    import bmesh
    import numpy as np
    from mathutils import Vector
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(p))
    meshes = [o for o in bpy.data.objects if o.type == 'MESH']
    arms = [o for o in bpy.data.objects if o.type == 'ARMATURE']
    dg = bpy.context.evaluated_depsgraph_get()
    bm = bmesh.new()
    for o in meshes:
        tmp = bmesh.new()
        tmp.from_mesh(o.data)
        tmp.transform(o.matrix_world)
        me = bpy.data.meshes.new('tmp')
        tmp.to_mesh(me)
        bm.from_mesh(me)
        tmp.free()
    v_raw = len(bm.verts)
    tris = sum(len(f.verts) - 2 for f in bm.faces)
    uv = bm.loops.layers.uv.active
    uv_flip = uv_out = 0
    if uv:
        for f in bm.faces:
            ls = f.loops
            a = 0.0
            for i in range(len(ls)):
                u0 = ls[i][uv].uv
                u1 = ls[(i + 1) % len(ls)][uv].uv
                a += u0.x * u1.y - u1.x * u0.y
                if not (-1e-4 <= u0.x <= 1.0001 and -1e-4 <= u0.y <= 1.0001):
                    uv_out += 1
            if a < 0:
                uv_flip += 1
    degenerate = sum(1 for f in bm.faces if f.calc_area() < 1e-12)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
    bm.edges.ensure_lookup_table()
    boundary = sum(1 for e in bm.edges if e.is_boundary)
    nonman = sum(1 for e in bm.edges if not e.is_manifold and not e.is_boundary)
    # islands
    seen, islands, sizes = set(), 0, []
    for v in bm.verts:
        if v.index in seen:
            continue
        islands += 1
        stack, cnt = [v], 0
        seen.add(v.index)
        while stack:
            x = stack.pop()
            cnt += 1
            for e in x.link_edges:
                y = e.other_vert(x)
                if y.index not in seen:
                    seen.add(y.index)
                    stack.append(y)
        sizes.append(cnt)
    vol = bm.calc_volume(signed=True)
    co = np.array([v.co[:] for v in bm.verts])
    c = co.mean(axis=0)
    inward = sum(1 for f in bm.faces if f.normal.dot(f.calc_center_median() - Vector(c)) < 0)
    mn, mx = co.min(axis=0), co.max(axis=0)
    out = {'meshObjects': len(meshes), 'armatures': len(arms), 'triangles': tris, 'verticesImported': v_raw,
           'verticesWelded': len(bm.verts), 'faces': len(bm.faces), 'degenerateFaces': degenerate,
           'boundaryEdgesWelded': boundary, 'nonManifoldEdgesWelded': nonman, 'islands': islands,
           'smallIslands(<50v)': sum(1 for s in sizes if s < 50), 'signedVolume_m3': round(vol, 6),
           'inwardFacingShare': round(inward / max(1, len(bm.faces)), 4),
           'uvLayers': len(bm.loops.layers.uv), 'uvFlippedShare': round(uv_flip / max(1, len(bm.faces)), 4),
           'uvLoopsOutside01': uv_out,
           'bboxMin': [round(x, 4) for x in mn], 'bboxMax': [round(x, 4) for x in mx],
           'size_m': [round(x, 4) for x in (mx - mn)], 'groundOffsetZ': round(float(mn[2]), 4),
           'pivotOffsetXY': [round(float((mn[0] + mx[0]) / 2), 4), round(float((mn[1] + mx[1]) / 2), 4)],
           'objectTransforms': [{'name': o.name, 'loc': [round(x, 4) for x in o.location],
                                 'rotEuler': [round(x, 4) for x in o.rotation_euler],
                                 'scale': [round(x, 4) for x in o.scale]} for o in meshes]}
    bm.free()
    return out


def issues(c, b):
    out = []
    if c['skins'] == 0:
        out.append('no skin/skeleton in source (rig must be built)')
    if c['animations'] == 0:
        out.append('no animation in source')
    if b['signedVolume_m3'] < 0:
        out.append('inside-out winding (negative volume)')
    if b['boundaryEdgesWelded'] > 0:
        out.append(f"{b['boundaryEdgesWelded']} open boundary edges after weld")
    if b['nonManifoldEdgesWelded'] > 0:
        out.append(f"{b['nonManifoldEdgesWelded']} non-manifold edges after weld")
    if b['islands'] > 1:
        out.append(f"{b['islands']} disconnected islands")
    if b['uvLayers'] == 0:
        out.append('no UV0')
    elif b['uvFlippedShare'] > 0.05:
        out.append(f"info: {b['uvFlippedShare']*100:.0f}% UV faces mirrored (atlas charts; matters only for tangent-space normal maps)")
    if b['degenerateFaces']:
        out.append(f"{b['degenerateFaces']} degenerate faces")
    if abs(b['groundOffsetZ']) > 0.005:
        out.append(f"info: origin at bbox centre, not feet (min Z {b['groundOffsetZ']}); pipeline re-grounds")
    if max(abs(x) for x in b['pivotOffsetXY']) > 0.02:
        out.append(f"pivot off footprint centre {b['pivotOffsetXY']}")
    if any(m['metallic'] > 0.5 and not m['metallicRoughnessTexture'] for m in c['materials']):
        out.append('metallic factor > 0.5 without metallic-roughness texture')
    if not any(m['normalTexture'] for m in c['materials']):
        out.append('no normal map')
    return out


rows = []
for p in sorted(A.glb_dir.glob('*.glb')):
    h0 = sha256(p)
    c = container(p)
    b = blender_measure(p)
    h1 = sha256(p)
    assert h0 == h1, f'source changed: {p}'
    rows.append({'file': p.name, 'species': p.stem.split('_', 1)[1], 'bytes': p.stat().st_size, 'sha256': h0,
                 'container': c, 'blender': b, 'issues': issues(c, b)})
    print(p.name, 'done', flush=True)

A.out_json.write_text(json.dumps({'tool': 'audit_source_glbs.py', 'measured': 'Blender ' + __import__('bpy').app.version_string,
                                  'files': rows}, indent=1))
if A.md:
    L = ['| # | Loài | SHA-256 (12) | Tam giác | Đỉnh (hàn) | Biên hở | Đảo | Thể tích | UV lật | Texture | Skin/Anim | Kích thước (m) | Vấn đề |',
         '|---|---|---|---|---|---|---|---|---|---|---|---|---|']
    for i, r in enumerate(rows, 1):
        c, b = r['container'], r['blender']
        tex = ', '.join(f"{s[0]}²" if s and s[0] == s[1] else str(s) for s in (im['size'] for im in c['images'])) or '—'
        L.append(f"| {i} | {r['species']} | `{r['sha256'][:12]}` | {b['triangles']} | {b['verticesWelded']} | "
                 f"{b['boundaryEdgesWelded']} | {b['islands']} | {b['signedVolume_m3']:.4f} | {b['uvFlippedShare']*100:.0f}% | "
                 f"{tex} | {c['skins']}/{c['animations']} | {' × '.join(f'{x:.3f}' for x in b['size_m'])} | "
                 f"{'; '.join(r['issues'])} |")
    A.md.write_text('\n'.join(L) + '\n')
