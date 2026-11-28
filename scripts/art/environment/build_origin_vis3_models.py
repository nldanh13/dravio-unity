"""VIS.3b: stage rounded Origin models, using the existing atlas and pivots.

Run in Blender 5.3 from the repository root:
  blender --factory-startup --background --python-exit-code 1 --python scripts/art/environment/build_origin_vis3_models.py -- --out art-exports/vis3b

No Unity assets are written. Review the staged FBX/.blend and candidate manifest
before replacing the six existing FBX files (keep their .meta GUIDs).
"""
import argparse
import json
import math
from pathlib import Path
import sys

import bpy
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

import_dir = Path(__file__).resolve().parent
sys.path.insert(0, str(import_dir))
import build_origin_sanctuary as env

ROOT = import_dir.parents[2]
UNITY_ENV = ROOT / "Assets" / "Dravio" / "Environment"
TARGETS = (
    "SM_ENV_Tree_Ancient", "SM_ENV_Tree_Ancient_B", "SM_ENV_Shrub_Round",
    "SM_ENV_Rock_Mossy_A", "SM_ENV_Rock_Mossy_B", "SM_ENV_Rock_Mossy_C",
)
FRUIT_COUNTS = {"SM_ENV_Tree_Ancient": 3, "SM_ENV_Tree_Ancient_B": 4}
SMOOTH_ZONES = {"leaf_canopy", "stone_cool"}
TOL = 0.002


def bounds(points):
    points = list(points)
    return ([min(p[i] for p in points) for i in range(3)],
            [max(p[i] for p in points) for i in range(3)])


class RoundedKit(env.Kit):
    """Blend noisy blobs towards ellipsoids; preserve each original blob's AABB.

    No topology, RNG sequence, ground contact or material zone changes in this
    operation. Trunk/root skin and foliage cards are not affected.
    """
    def blob(self, center, radius, zone, subdiv=2, rough=0.18, freq=1.3,
             flat_bottom=None, smooth=True, rot_z=0.0):
        faces = super().blob(center, radius, zone, subdiv, rough, freq,
                             flat_bottom, smooth, rot_z)
        if zone not in SMOOTH_ZONES:
            return faces
        verts = sorted({v for f in faces for v in f.verts}, key=lambda v: tuple(v.co))
        old_lo, old_hi = bounds(v.co for v in verts)
        c = Vector(center)
        rot = Matrix.Rotation(rot_z, 3, "Z")
        inv = rot.inverted()
        strength = 0.72 if zone == "leaf_canopy" else 0.58
        for v in verts:
            local = inv @ (v.co - c)
            unit = Vector(tuple(local[i] / radius[i] for i in range(3)))
            # Preserve the flat bottom plane; the subsequent AABB fit also
            # preserves the original lowest point and uppermost silhouette.
            if flat_bottom is not None and local.z <= flat_bottom * radius[2] + 1e-6:
                continue
            if unit.length_squared < 1e-10:
                continue
            direction = unit.normalized()
            ideal = Vector(tuple(direction[i] * radius[i] for i in range(3)))
            if flat_bottom is not None:
                ideal.z = max(ideal.z, flat_bottom * radius[2])
            v.co = v.co.lerp(c + rot @ ideal, strength)
        new_lo, new_hi = bounds(v.co for v in verts)
        for v in verts:
            for i in range(3):
                width = new_hi[i] - new_lo[i]
                if width > 1e-8:
                    v.co[i] = old_lo[i] + (v.co[i] - new_lo[i]) / width * (old_hi[i] - old_lo[i])
        self.bm.normal_update()
        return faces


def add_fruit(k, limit):
    """Place modest decorative fruit on exposed canopy surfaces, no new atlas.

    Reuses the red mushroom-cap tile as a temporary red fruit material (its
    light speckles are a stylized approximation; replace with dedicated art
    later if the camera close-up does not read as fruit).
    """
    k.bm.normal_update()
    k.bm.faces.ensure_lookup_table()
    k.bm.faces.index_update()
    lo, hi = bounds(v.co for v in k.bm.verts)
    bvh = BVHTree.FromBMesh(k.bm)
    xmid, ymid = (lo[0] + hi[0]) * .5, (lo[1] + hi[1]) * .5
    extent = max(hi[i] - lo[i] for i in range(3)) + 2
    radius = .18 if k.name == TARGETS[0] else .16
    centers = []
    # Spread fruit around the canopy; prioritize the +Y camera-facing surface.
    # Raycast the real model, not a guessed sphere, and reject trunk/cards.
    for angle in (math.pi / 2, math.pi / 6, 5 * math.pi / 6, -math.pi / 2,
                  0, math.pi):
        outward = Vector((math.cos(angle), math.sin(angle), 0))
        tangent = Vector((-outward.y, outward.x, 0))
        for frac, offset in ((.68, -.18), (.74, .20), (.59, .05), (.82, -.05)):
            z = lo[2] + (hi[2] - lo[2]) * frac
            origin = Vector((xmid, ymid, z)) + outward * extent
            origin += tangent * offset * (hi[0] - lo[0])
            pos, normal, index, _ = bvh.ray_cast(origin, -outward, extent * 2)
            if index is None or k.bm.faces[index].material_index != env.Z["leaf_canopy"]:
                continue
            if normal.dot(outward) < .25:
                continue
            p = pos + normal * .065
            margin = radius * 1.12
            if any(p[i] - margin < lo[i] or p[i] + margin > hi[i] for i in range(3)):
                continue
            if any((p - other).length < .90 for other in centers):
                continue
            centers.append(p.copy())
            if len(centers) == limit:
                break
        if len(centers) == limit:
            break

    if len(centers) < limit:
        raise RuntimeError(f"{k.name}: only {len(centers)}/{limit} valid fruit sites; review canopy")
    # 80 triangles per subdivision-2 ico sphere. No separate stem mesh.
    # All points are chosen before modifying the bmesh used by the BVH.
    for p in centers:
        faces = k.blob(p, (radius, radius * .96, radius * 1.06), "mushroom_cap",
                       subdiv=2, rough=.025, freq=1.1)
        env.set_vis_ao(k, faces, lambda co, p=p: .84 + .12 * (co.z - p.z) / radius)
    return len(centers)


def inspect(obj):
    st = env.stats(obj)
    st["uv_errors"] = env.validate_uvs(obj)
    st["bounds_min"], st["bounds_max"] = bounds(v.co for v in obj.data.vertices)
    st["nonfinite_vertices"] = sum(
        not all(math.isfinite(c) for c in v.co) for v in obj.data.vertices)
    st["degenerate_faces"] = sum(p.area <= 1e-10 for p in obj.data.polygons)
    return st


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--out", required=True, type=Path)
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out = parser.parse_args(argv).out.resolve()
    if out == ROOT or ROOT / "Assets" == out or ROOT / "Assets" in out.parents:
        raise SystemExit("Stage outside Unity Assets.")
    if out.exists() and (not out.is_dir() or any(out.iterdir())):
        raise SystemExit("Choose an empty staging directory; no overwrite.")

    manifest = json.loads((UNITY_ENV / "Data/OriginSanctuaryManifest.json").read_text(encoding="utf-8"))
    layout = json.loads((UNITY_ENV / "Data/OriginSanctuaryLayout.json").read_text(encoding="utf-8"))
    bpy.ops.wm.read_factory_settings(use_empty=True)
    env.MATERIALS.clear()
    env.make_materials(UNITY_ENV / "Textures")
    candidates, report = {}, {}
    for name in TARGETS:
        seed = 1000 + list(env.ASSETS).index(name) * 37
        legacy = env.Kit(name, seed)
        env.ASSETS[name](legacy)
        original_obj = env.to_object(legacy)
        original = inspect(original_obj)
        mesh = original_obj.data
        bpy.data.objects.remove(original_obj, do_unlink=True)
        bpy.data.meshes.remove(mesh)
        expected = manifest["assets"][name]
        if original["triangles"] != expected["triangles"] or original["materials"] != expected["materials"]:
            raise RuntimeError(f"{name}: generator/committed manifest mismatch; stop before replacement")

        k = RoundedKit(name, seed)
        env.ASSETS[name](k)
        fruits = add_fruit(k, FRUIT_COUNTS[name]) if name in FRUIT_COUNTS else 0
        obj = env.to_object(k)
        candidate = inspect(obj)
        budget = expected["budget"]
        if candidate["triangles"] > budget or candidate["uv_errors"] or candidate["nonfinite_vertices"]:
            raise RuntimeError(f"{name}: budget/UV/finite check failed: {candidate}")
        if candidate["degenerate_faces"] > original["degenerate_faces"]:
            raise RuntimeError(f"{name}: new degenerate faces")
        if candidate["materials"] != original["materials"]:
            raise RuntimeError(f"{name}: changed material slots")
        if any(abs(a-b) > TOL for key in ("bounds_min", "bounds_max")
               for a,b in zip(original[key], candidate[key])):
            raise RuntimeError(f"{name}: changed local bounds; keep collider contract")
        candidates[name] = obj
        report[name] = {"baseline": original, "candidate": candidate, "decorative_fruit": fruits}
        manifest["assets"][name] = {key: candidate[key] for key in
                                     ("triangles", "vertices", "materials", "size_m", "uv_errors")}
        manifest["assets"][name]["budget"] = budget
        print(f"[VIS.3b] {name}: {original['triangles']} -> {candidate['triangles']} triangles, fruit={fruits}")

    total = sum(manifest["assets"][p["asset"]]["triangles"] for p in layout["placements"])
    if total >= 60000:
        raise RuntimeError(f"Origin scene budget exceeded: {total}")
    manifest["layout"]["scene_triangles"] = total
    manifest["vis3b"] = {"generator": "scripts/art/environment/build_origin_vis3_models.py",
                         "blender": bpy.app.version_string, "staged": True}

    # All six candidates must pass before any output is written.
    models = out / "Models"
    models.mkdir(parents=True, exist_ok=True)
    for name, obj in candidates.items():
        env.export_fbx(obj, models / f"{name}.fbx")
    (out / "OriginSanctuaryManifest.json").write_text(json.dumps(manifest, indent=1), encoding="utf-8")
    (out / "validation.json").write_text(json.dumps({"scene_triangles": total, "assets": report}, indent=2), encoding="utf-8")
    for i, obj in enumerate(candidates.values()):
        obj.location.x = (i % 3) * 9
        obj.location.y = (i // 3) * 8
    bpy.ops.wm.save_as_mainfile(filepath=str(out / "Origin_VIS3b_Candidates.blend"))
    print(f"[VIS.3b] Staged six models; {total} scene triangles. Review visually before importing.")


if __name__ == "__main__":
    main()
