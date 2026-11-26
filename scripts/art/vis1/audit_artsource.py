"""VIS.1 - ArtSource GLB audit (Blender 5.3 / bpy 5.3).

Imports every GLB under ArtSource/ into an empty scene and reports what is really
inside it: triangles, mesh/material/texture counts, texture sizes, armature and
animation presence, bounding size and a suitability note for the Origin slice.
Read-only on ArtSource: nothing is written back.

  blender --factory-startup --background --python-exit-code 1 \
      --python scripts/art/vis1/audit_artsource.py -- --out Docs/vis1/artsource_audit.json
"""
import hashlib
import json
import sys
from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[3]
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = Path(argv[argv.index("--out") + 1]) if "--out" in argv else ROOT / "Docs/vis1/artsource_audit.json"


def sha256(p):
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def audit(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(path))
    meshes = [o for o in bpy.data.objects if o.type == 'MESH']
    tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in meshes)
    verts = sum(len(o.data.vertices) for o in meshes)
    lo, hi = Vector((1e9,) * 3), Vector((-1e9,) * 3)
    for o in meshes:
        for c in o.bound_box:
            w = o.matrix_world @ Vector(c)
            lo = Vector(map(min, lo, w))
            hi = Vector(map(max, hi, w))
    size = hi - lo if meshes else Vector()
    images = [{"name": i.name, "size": list(i.size)} for i in bpy.data.images if i.size[0]]
    vcol = any(len(o.data.color_attributes) for o in meshes)
    return {
        "path": str(path.relative_to(ROOT)).replace("\\", "/"),
        "bytes": path.stat().st_size,
        "sha256": sha256(path),
        "meshes": len(meshes),
        "triangles": tris,
        "vertices": verts,
        "materials": len(bpy.data.materials),
        "images": images,
        "vertexColors": vcol,
        "armatures": sum(1 for o in bpy.data.objects if o.type == 'ARMATURE'),
        "actions": len(bpy.data.actions),
        # glTF is Y-up; Blender import converts to Z-up, so size = (x, y, z) metres in Blender axes.
        "sizeBlenderXYZ": [round(v, 3) for v in size],
    }


def main():
    rows = [audit(p) for p in sorted(ROOT.glob("ArtSource/**/*.glb"))]
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps({"tool": "scripts/art/vis1/audit_artsource.py",
                               "blender": bpy.app.version_string, "files": rows}, indent=1),
                   encoding="utf-8")
    for r in rows:
        print(f"[audit] {r['path']:60s} {r['triangles']:7d} tris {r['materials']:2d} mat "
              f"{len(r['images'])} img arm={r['armatures']} act={r['actions']} size={r['sizeBlenderXYZ']}")


main()
