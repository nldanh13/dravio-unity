"""U6.5  Origin island base: turns the Origin Sanctuary into an island in the sea (presentation only).

Builds ONE mesh, SM_ISLAND_Origin, with the same kit, atlas zones and material slots as the Origin
environment (build_origin_sanctuary.py), so Unity maps it onto the existing M_ENV_Ground / M_ENV_Props /
M_ENV_Foliage materials by slot name and the island looks like the rest of the kit:

  * island top: a rounded (superellipse) ground sheet, flat (y = 0) inside layout.FLAT_RECT exactly like
    SM_ENV_Ground_Sanctuary, so every gameplay position, spawn point and collider stays where it was;
  * a short earth lip and a rocky cliff dropping below the sea line (the sea itself is drawn by
    IslandSeascape at runtime, at SEA_Y);
  * boulders along the waterline and foliage mounds on the rim, kept low in front so the camera sees in.

The runtime (Assets/Dravio/Scripts/IslandSeascape.cs) hides the old ground sheet, the edge cover and every
environment prop outside OUTLINE, then shows this mesh. Nothing in the scene file changes.

  python scripts/art/environment/build_island_base.py --out art-exports/island \
      [--unity Assets/Dravio/Resources/IslandScenery]      (bpy 5.3 module, or blender --background --python)

OUTLINE is mirrored in IslandSeascape.Outline (C#); keep both in sync.
"""
import math
import shutil
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import build_origin_sanctuary as K  # noqa: E402  (imports bpy; its main() is guarded)
import layout as layout_mod  # noqa: E402
import bpy  # noqa: E402
import bmesh  # noqa: E402
from mathutils import Vector, noise  # noqa: E402

OUT = Path(K.arg("--out", "art-exports/island")).resolve()
UNITY = K.arg("--unity")

# Unity XZ: centre, half extents, superellipse exponent. Contains FLAT_RECT (+-8.6, -7..7.4) and the ruins
# behind it (z <= 8); the front edge sits just below the flat rect so the camera sees the shore early.
OUTLINE = dict(cx=0.0, cz=0.6, hx=10.3, hz=9.3, p=5.0)
SEA_Y = -0.95
RINGS = 22
SEGMENTS = 120


def edge_point(t):
    """Point on the outline at angle t (Unity x, z)."""
    c, s = math.cos(t), math.sin(t)
    p = OUTLINE["p"]
    r = (abs(c) ** p + abs(s) ** p) ** (-1.0 / p)
    return OUTLINE["cx"] + OUTLINE["hx"] * r * c, OUTLINE["cz"] + OUTLINE["hz"] * r * s


def ground_h(ux, uz, edge):
    """Same rule as build_ground: flat inside FLAT_RECT, gentle noise outside, easing to the lip."""
    fx0, fz0, fx1, fz1 = layout_mod.FLAT_RECT
    h = noise.noise(Vector((ux * 0.12, uz * 0.12, 3.1))) * 0.22
    out = max(fx0 - ux, ux - fx1, fz0 - uz, uz - fz1, 0.0)
    h *= min(1.0, out / 2.5)
    return h * (1.0 - edge ** 3) - 0.06 * edge ** 4


def V(k, ux, y, uz):
    return k.bm.verts.new((-ux, -uz, y))  # Blender space, as every kit asset


def build(k):
    rng = k.rng
    ts = [2 * math.pi * i / SEGMENTS for i in range(SEGMENTS)]
    edge = [edge_point(t) for t in ts]
    cx, cz = OUTLINE["cx"], OUTLINE["cz"]

    # --- top: rings from the centre to the outline (GROUND) ---------------------------------------
    centre = V(k, cx, ground_h(cx, cz, 0.0), cz)
    rings = []
    for r in range(1, RINGS + 1):
        f = r / RINGS
        ring = []
        for (ex, ez) in edge:
            ux, uz = cx + (ex - cx) * f, cz + (ez - cz) * f
            ring.append(V(k, ux, ground_h(ux, uz, f), uz))
        rings.append(ring)
    n0 = len(k.bm.faces)
    for i in range(SEGMENTS):
        j = (i + 1) % SEGMENTS
        k.bm.faces.new((centre, rings[0][j], rings[0][i]))
        for r in range(RINGS - 1):
            a, b = rings[r], rings[r + 1]
            k.bm.faces.new((a[i], a[j], b[j], b[i]))
    k._finish(k._new_faces(n0), "GROUND", True)

    # --- lip (earth) and cliff (stone) ------------------------------------------------------------
    def outward(i, d):
        ex, ez = edge[i]
        nx, nz = ex - cx, ez - cz
        L = math.hypot(nx, nz)
        return ex + nx / L * d, ez + nz / L * d

    top = rings[-1]
    lip, cliff_mid, cliff_low = [], [], []
    for i in range(SEGMENTS):
        jit = noise.noise(Vector((ts[i] * 3.1, 0.7, 2.2)))
        lx, lz = outward(i, 0.35 + 0.12 * jit)
        lip.append(V(k, lx, -0.16 + 0.05 * jit, lz))
        mx, mz = outward(i, 0.55 + 0.25 * jit)
        cliff_mid.append(V(k, mx, SEA_Y + 0.05 + 0.1 * jit, mz))
        bx, bz = outward(i, 0.95 + 0.35 * jit)
        cliff_low.append(V(k, bx, SEA_Y - 1.4, bz))
    for a, b, zone in ((top, lip, "earth"), (lip, cliff_mid, "stone_warm"), (cliff_mid, cliff_low, "stone_cool")):
        n0 = len(k.bm.faces)
        for i in range(SEGMENTS):
            j = (i + 1) % SEGMENTS
            k.bm.faces.new((a[i], a[j], b[j], b[i]))
        k._finish(k._new_faces(n0), zone, zone != "earth")

    # --- waterline boulders -----------------------------------------------------------------------
    t = 0.0
    while t < 2 * math.pi:
        ex, ez = edge_point(t)
        nx, nz = ex - cx, ez - cz
        L = math.hypot(nx, nz)
        d = rng.uniform(0.5, 1.1)
        bx, bz = ex + nx / L * d, ez + nz / L * d
        rad = rng.uniform(0.35, 0.75)
        f = k.blob((-bx, -bz, SEA_Y + rad * 0.25), (rad, rad * 0.85, rad * 0.7), rng.choice(("stone_cool", "stone_warm")),
                   subdiv=1, rough=0.28, flat_bottom=-0.6, rot_z=rng.uniform(0, 6.28))
        K.set_vis_ao(k, f, lambda co, z0=SEA_Y: 0.55 + 0.45 * min(1.0, max(0.0, (co.z - z0) / 0.6)))
        t += rng.uniform(0.11, 0.2)

    # --- rim foliage: mounds just inside the edge, none in the front third (camera side) ----------
    t = 0.0
    while t < 2 * math.pi:
        ex, ez = edge_point(t)
        if ez > -2.0 and rng.random() < 0.7:  # keep the shore in front of the camera open; leave gaps
            nx, nz = ex - cx, ez - cz
            L = math.hypot(nx, nz)
            d = rng.uniform(0.4, 0.9)
            fx, fz = ex - nx / L * d, ez - nz / L * d
            fx0, fz0, fx1, fz1 = layout_mod.FLAT_RECT
            if not (fx0 - 0.4 < fx < fx1 + 0.4 and fz0 - 0.4 < fz < fz1 + 0.4):
                rx, rz = rng.uniform(0.55, 0.95), rng.uniform(0.45, 0.8) * (1.3 if ez > 6 else 1.0)
                c = (-fx, -fz, rz * rng.uniform(0.35, 0.6))
                f = k.blob(c, (rx, rx * 0.85, rz), "leaf_canopy", subdiv=2, rough=0.22, freq=1.6, flat_bottom=-0.5,
                           rot_z=rng.uniform(0, 6.28))
                topz = c[2] + rz
                K.set_vis_ao(k, f, lambda co, tz=topz: 0.45 + 0.45 * max(0.0, co.z) / tz)
        t += rng.uniform(0.1, 0.2)
    bmesh.ops.recalc_face_normals(k.bm, faces=k.bm.faces[:])


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for s in K.FINAL_SLOTS:  # slot names only: Unity remaps them to the existing M_ENV_* materials
        K.MATERIALS[s] = bpy.data.materials.new(s)
    k = K.Kit("SM_ENV_Ground_Sanctuary", seed=6505)  # ground name: to_object paints the same path mask / shade
    build(k)
    obj = K.to_object(k)
    obj.name = obj.data.name = "SM_ISLAND_Origin"
    st = K.stats(obj)
    path = OUT / "SM_ISLAND_Origin.fbx"
    K.export_fbx(obj, path)
    print(f"[island] SM_ISLAND_Origin {st['triangles']} tris {st['materials']} -> {path}")
    if UNITY:
        dst = Path(UNITY).resolve()
        dst.mkdir(parents=True, exist_ok=True)
        shutil.copy2(path, dst / path.name)
        print("[island] copied into", dst)


if __name__ == "__main__":
    main()
