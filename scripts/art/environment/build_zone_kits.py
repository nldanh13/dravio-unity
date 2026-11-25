"""U1-ENV.4  Ocean (Thủy Vực) and Sky (Thiên Giới) environment kits for Dravio.

Same pipeline as build_origin_sanctuary.py (whose Kit, zones, unwrap, vertex colours
and FBX export are imported, not copied): every mesh is built from code, unwrapped
into the zone's own painterly atlas and exported one FBX per asset. Reused Origin
builders (gate, columns, wall, statue, rune stone, trees, shrubs) are rebuilt with the
zone material set, so a zone scene only ever uses its own four materials.

Blender 5.3 (or the `bpy` 5.3 module). From the repo root:

  blender --factory-startup --background --python-exit-code 1 \
      --python scripts/art/environment/build_zone_kits.py -- \
      --out art-exports/zones --unity Assets/Dravio/Environment [--zones ocean,sky] [--preview]

  --unity  copies into Assets/Dravio/Environment/Zones/<Ocean|Sky>/{Models,Textures,Data,Audio}
           only. The Origin Sanctuary folders (Models/, Textures/, Data/, Audio/) and
           its scene are never written.

Checks that fail the build: triangle budget per asset and per scene, UVs leaving their
atlas tile, perch / rest heights that do not match the mesh under them (ray cast),
walk areas that are not flat at their declared height, scatter outside its surface.
"""
import json
import math
import random
import shutil
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import build_origin_sanctuary as K  # noqa: E402  (imports bpy first; main() is guarded)
import bpy  # noqa: E402
import bmesh  # noqa: E402
from mathutils import Matrix, Vector, noise  # noqa: E402
from mathutils.bvhtree import BVHTree  # noqa: E402

import zone_common as zc  # noqa: E402
import ocean_zone  # noqa: E402
import sky_zone  # noqa: E402
import zone_textures  # noqa: E402
import zone_audio  # noqa: E402

OUT = Path(K.arg("--out", "art-exports/zones")).resolve()
UNITY = K.arg("--unity")
PREVIEW = K.arg("--preview", flag=True)
ZONES_ARG = [z for z in (K.arg("--zones", "ocean,sky") or "").split(",") if z]
Z = K.Z
FRONT = K.FRONT
SCENE_TRIANGLE_BUDGET = 60000  # same as the Origin scene (ReportBudget)


def B(x, y, z):
    """Unity (x right, y up, z away) -> Blender object space."""
    return (-x, -z, y)


# ================================================================== OCEAN
def build_seabed(k):
    """Sand sheet, flat (y = 0) inside FLAT_RECT, rising into dunes at the sides and a
    reef shelf at the back so the sunken ruins sit on higher ground."""
    ux0, ux1, uz0, uz1 = -16.0, 20.0, -11.0, 28.0
    nx, nz = 36, 30
    fx0, fz0, fx1, fz1 = ocean_zone.FLAT_RECT
    grid = {}
    for i in range(nx + 1):
        for j in range(nz + 1):
            ux = ux0 + (ux1 - ux0) * i / nx
            uz = uz0 + (uz1 - uz0) * j / nz
            out = max(fx0 - ux, ux - fx1, fz0 - uz, uz - fz1, 0.0)
            h = noise.noise(Vector((ux * 0.14, uz * 0.14, 5.3))) * 0.3 * min(1.0, out / 2.0)
            h += min(2.2, max(0.0, uz - 9.0) * 0.35) + 0.08 * max(0.0, abs(ux) - 10.0)
            grid[i, j] = k.bm.verts.new(B(ux, h if out > 0 else 0.0, uz))
    n0 = len(k.bm.faces)
    for i in range(nx):
        for j in range(nz):
            k.bm.faces.new((grid[i, j], grid[i, j + 1], grid[i + 1, j + 1], grid[i + 1, j]))
    faces = k._finish(k._new_faces(n0), "GROUND", True)
    bmesh.ops.recalc_face_normals(k.bm, faces=faces)
    for f in faces:  # all quads face up (+Z)
        if f.normal.z < 0:
            f.normal_flip()


def build_coral_branch(k):
    r = k.rng
    chains = []
    for i in range(6):
        a = i * 2 * math.pi / 6 + r.uniform(-.3, .3)
        lean = r.uniform(0.25, 0.55)
        h = r.uniform(0.8, 1.25)
        p0 = (0, 0, 0.05)
        p1 = (math.cos(a) * lean * 0.4, math.sin(a) * lean * 0.4, h * 0.5)
        p2 = (math.cos(a) * lean, math.sin(a) * lean, h)
        chains.append([(p0, 0.11), (p1, 0.075), (p2, 0.045)])
        b = a + r.choice((-0.7, 0.7))
        chains.append([(p1, 0.06), ((p1[0] + math.cos(b) * 0.3, p1[1] + math.sin(b) * 0.3, p1[2] + 0.35), 0.035)])
    me = K.skin_mesh(chains, subdiv=1, decimate=0.55, step=0.18)
    k.append_mesh(me, "mushroom_cap", smooth=True)
    bpy.data.meshes.remove(me)
    k.blob((0, 0, 0.05), (0.35, 0.32, 0.18), "stone_cool", subdiv=1, rough=0.3, flat_bottom=-0.4, smooth=False)


def build_coral_brain(k):
    faces = k.blob((0, 0, 0.3), (0.72, 0.66, 0.5), "moss", subdiv=3, rough=0.1, freq=2.4, flat_bottom=-0.55)
    for i in range(3):
        a = k.rng.uniform(0, 6.28)
        faces += k.blob((math.cos(a) * 0.75, math.sin(a) * 0.65, 0.08), (0.22, 0.2, 0.15), "stone_cool", subdiv=1,
                        rough=0.3, flat_bottom=-0.4, smooth=False)


def build_coral_table(k):
    k.cone((0, 0, 0), 0.22, 0.13, 0.9, "stone_cool", seg=8)
    top = k.blob((0, 0, 0.94), (0.85, 0.8, 0.09), "lily", subdiv=2, rough=0.12, freq=1.6)
    for v in {v for f in top for v in f.verts}:
        v.co.z = min(v.co.z, 1.04)
    k.blob((0.35, -0.25, 0.62), (0.42, 0.38, 0.06), "lily", subdiv=2, rough=0.15, freq=1.8)
    k.blob((0, 0, 0.04), (0.4, 0.36, 0.12), "stone_cool", subdiv=1, rough=0.3, flat_bottom=-0.5, smooth=False)


def build_sea_fan(k):
    k.card((0, 0, 0), 1.25, 1.45, "F_fern", yaw=0.1, tilt=0.05)
    k.card((0.18, -0.12, 0), 0.9, 1.1, "F_fern", yaw=0.55, tilt=-0.1)
    k.blob((0, 0, 0.04), (0.18, 0.16, 0.1), "stone_cool", subdiv=1, rough=0.3, flat_bottom=-0.5, smooth=False)


def build_kelp(k):
    r = k.rng
    for i in range(5):
        a = i * math.pi / 5 + r.uniform(-.2, .2)
        cell = "F_reed" if i % 2 == 0 else "F_ivy_strand"
        p = (math.cos(a) * 0.18, math.sin(a) * 0.18, 0)
        k.card(p, 0.7, r.uniform(2.6, 3.6), cell, yaw=a, tilt=r.uniform(-.08, .08))
    k.blob((0, 0, 0.05), (0.3, 0.28, 0.16), "bark", subdiv=1, rough=0.35, flat_bottom=-0.4, smooth=False)


def build_anemone(k):
    r = k.rng
    k.cone((0, 0, 0), 0.34, 0.28, 0.3, "root", seg=10)
    k.cone((0, 0, 0.3), 0.29, 0.29, 0.02, "glow_cap", seg=10)
    for i in range(16):
        a = i * 2 * math.pi / 16 + r.uniform(-.1, .1)
        rr = 0.12 + 0.13 * (i % 2)
        k.cone((math.cos(a) * rr, math.sin(a) * rr, 0.3), 0.045, 0.012, r.uniform(0.28, 0.42), "glow_cap", seg=5,
               rot=(math.sin(a) * -0.6, math.cos(a) * 0.6, 0))


def build_shells(k):
    r = k.rng
    for i in range(5):
        a = r.uniform(0, 6.28)
        d = r.uniform(0.05, 0.4)
        x, y = math.cos(a) * d, math.sin(a) * d
        if i % 2 == 0:  # spiral cone shell lying on its side
            k.cone((x, y, 0.06), 0.08, 0.01, 0.24, "mushroom_stem", seg=7, rot=(math.pi / 2 - 0.2, 0, a))
        else:  # scallop
            k.blob((x, y, 0.03), (0.13, 0.11, 0.035), "mushroom_cap", subdiv=1, rough=0.1, flat_bottom=-0.3, rot_z=a)
    for i in range(3):
        a = r.uniform(0, 6.28)
        k.blob((math.cos(a) * 0.5, math.sin(a) * 0.4, 0.04), (0.12, 0.1, 0.08), "stone_warm", subdiv=1, rough=0.3,
               flat_bottom=-0.4, smooth=False)


def build_reef_rock(k, size, subdiv, pieces=1, tufts=3):
    K.build_rock(k, size, subdiv, pieces)  # stone_cool blobs + moss (algae crust) tops
    r = k.rng
    for i in range(tufts):
        a = r.uniform(0, 6.28)
        k.cone((math.cos(a) * size * 0.55, math.sin(a) * size * 0.45, size * 0.55), 0.07, 0.04, 0.25,
               "mushroom_cap", seg=6, rot=(r.uniform(-.4, .4), r.uniform(-.4, .4), 0))


def build_column_sunken(k):
    K.build_column_broken(k)
    for v in k.bm.verts:  # half buried in sand
        v.co.z -= 0.12
    r = k.rng
    for i in range(5):  # barnacle clumps
        a = r.uniform(0, 6.28)
        k.blob((math.cos(a) * 0.44, math.sin(a) * 0.44, r.uniform(0.2, 1.2)), (0.08, 0.08, 0.06), "mushroom_stem",
               subdiv=1, rough=0.4, smooth=False)


def build_crystal(k):
    r = k.rng
    k.blob((0, 0, 0.08), (0.55, 0.45, 0.22), "rune_slate", subdiv=1, rough=0.3, flat_bottom=-0.5, smooth=False)
    for i, (h, rad) in enumerate(((1.4, 0.24), (1.0, 0.19), (0.8, 0.17), (0.6, 0.14), (0.5, 0.12))):
        a = i * 2.3
        d = 0.0 if i == 0 else 0.28
        tilt = 0.0 if i == 0 else 0.35
        base = (math.cos(a) * d, math.sin(a) * d, 0.05)
        rot = (math.sin(a) * -tilt, math.cos(a) * tilt, r.uniform(0, 1))
        k.cone(base, rad, rad * 0.95, h * 0.8, "rune_glyph", seg=6, rot=rot, smooth=False)
        R = Matrix.Rotation(rot[2], 3, 'Z') @ Matrix.Rotation(rot[1], 3, 'Y') @ Matrix.Rotation(rot[0], 3, 'X')
        tip = Vector(base) + R @ Vector((0, 0, h * 0.8))
        k.cone(tuple(tip), rad * 0.95, 0.01, h * 0.25, "glow_cap", seg=6, rot=rot, smooth=False)


def build_vent(k):
    faces = k.blob((0, 0, 0.25), (0.6, 0.55, 0.38), "rune_slate", subdiv=2, rough=0.25, flat_bottom=-0.6)
    faces += k.cone((0, 0, 0.45), 0.32, 0.18, 0.35, "rune_slate", seg=8, caps=False)
    k.cone((0, 0, 0.72), 0.17, 0.17, 0.02, "glow_cap", seg=8)
    k.moss_tops(faces, 0.75, density=0.6)


OCEAN_CARDS = dict(K.COVER_CARDS)
OCEAN_CARDS.update({"reed": (2, 0.5, 1.4, 0.05), "cattail": (2, 0.45, 1.1, 0.05), "shrub_leaf": (3, 0.7, 0.6, 0.2),
                    "flower_gold": (2, 0.45, 0.55, 0.05), "twig_dry": (2, 0.6, 0.75, 0.05),
                    "flower_blue": (2, 0.5, 0.4, 0.2), "meadow_tall": (2, 0.5, 1.0, 0.1)})
SKY_CARDS = dict(K.COVER_CARDS)


def build_zone_cover(k, zone, chunk, cards):
    pts = zc.scatter_cover(zone)[chunk]
    for cell, x, y, z, yaw, sc in pts:
        bx, by, bz = B(x, y, z)
        a = -math.radians(yaw)
        n, w, h, tilt = cards[cell]
        k.cross_cards((bx, by, bz), w * sc, h * sc, "F_" + cell, count=n, yaw=a, tilt=tilt)


# ==================================================================== SKY
def _poly_island(k, top_poly_unity, origin, depth, rim=True, cuts=3, seed_shift=0.0):
    """Floating island: flat GROUND top (exactly local z = 0 inside the polygon), a rocky
    lip, and an underside tapering to a point. Polygon is Unity XZ (absolute), origin is
    the placement XZ so the mesh pivot sits on it."""
    ox, oz = origin
    pts = [B(x - ox, 0.0, z - oz) for x, z in top_poly_unity]
    ring = [k.bm.verts.new(p) for p in pts]
    n0 = len(k.bm.faces)
    top = k.bm.faces.new(ring)
    if top.normal.z < 0:
        top.normal_flip()
    res = bmesh.ops.triangulate(k.bm, faces=[top], quad_method='BEAUTY', ngon_method='BEAUTY')
    tris = res["faces"]
    edges = [e for e in {e for f in tris for e in f.edges} if not e.is_boundary]
    bmesh.ops.subdivide_edges(k.bm, edges=edges, cuts=cuts, use_grid_fill=True)
    tops = [f for f in k._new_faces(n0)]
    bmesh.ops.triangulate(k.bm, faces=[f for f in tops if len(f.verts) > 3], quad_method='BEAUTY',
                          ngon_method='BEAUTY')
    tops = k._new_faces(n0)
    k._finish(tops, "GROUND", True)
    # bmesh ops re-allocate elements: look the rim vertices up again by position.
    by_pos = {(round(v.co.x, 4), round(v.co.y, 4)): v for f in tops for v in f.verts}
    ring = [by_pos[(round(p[0], 4), round(p[1], 4))] for p in pts]
    # Underside: rings below the rim, shrinking toward a point.
    cx = sum(p[0] for p in pts) / len(pts)
    cy = sum(p[1] for p in pts) / len(pts)
    levels = [(-0.28, 1.04, "stone_cool"), (-0.75, 0.9, "stone_cool"), (-depth * 0.45, 0.62, "mushroom_cap"),
              (-depth * 0.78, 0.32, "earth")]
    prev = ring
    off = Vector((seed_shift, seed_shift * 0.7, 0))
    n1 = len(k.bm.faces)
    for z, s, zone in levels:
        cur = []
        for p in pts:
            d = Vector((p[0] - cx, p[1] - cy, 0))
            j = 1.0 + 0.12 * noise.noise(Vector((p[0] * 0.6, p[1] * 0.6, z)) + off)
            cur.append(k.bm.verts.new((cx + d.x * s * j, cy + d.y * s * j, z * (1 + 0.15 * noise.noise(
                Vector((p[0], p[1], 1.3)) + off)))))
        m = len(cur)
        for i in range(m):
            j2 = (i + 1) % m
            f = k.bm.faces.new((prev[i], prev[j2], cur[j2], cur[i]))
            f.material_index = Z[zone]
            f.smooth = True
        prev = cur
    tip = k.bm.verts.new((cx, cy, -depth))
    for i in range(len(prev)):
        f = k.bm.faces.new((prev[i], prev[(i + 1) % len(prev)], tip))
        f.material_index = Z["earth"]
        f.smooth = True
    under = k._new_faces(n1)
    bmesh.ops.recalc_face_normals(k.bm, faces=under)
    if rim:  # rim stones and hanging vines on the camera side
        r = k.rng
        stones = []
        for i, p in enumerate(pts):
            if r.random() < 0.55:
                stones += k.blob((p[0] * 1.0, p[1] * 1.0, -0.05), (0.26, 0.22, 0.2), "stone_cool", subdiv=1,
                                 rough=0.3, smooth=False, rot_z=r.uniform(0, 3))
        k.moss_tops(stones, 0.55, density=0.8)
        for i, p in enumerate(pts):
            if p[1] > cy and r.random() < 0.6:  # front edge (Blender +Y = camera side)
                k.card((p[0], p[1] + 0.05, -0.15), 0.55, r.uniform(1.0, 1.8), "F_ivy_strand",
                       yaw=r.uniform(-.3, .3), pivot_bottom=False)
    return tops


def build_island_main(k):
    _poly_island(k, sky_zone.MAIN_MESH, (0.0, 0.0), 6.5, cuts=3)


def grow(poly, centre, d):
    out = []
    for x, z in poly:
        dx, dz = x - centre[0], z - centre[1]
        L = math.hypot(dx, dz) or 1.0
        out.append((x + dx / L * d, z + dz / L * d))
    return out


def build_islet_east(k):
    _poly_island(k, grow(sky_zone.EAST, sky_zone.EAST_C, 0.6), sky_zone.EAST_C, 3.6, cuts=2, seed_shift=3.0)


def build_islet_shrine(k):
    _poly_island(k, grow(sky_zone.SHRINE, sky_zone.SHRINE_C, 0.6), sky_zone.SHRINE_C, 3.0, cuts=2, seed_shift=7.0)


def _blob_poly(r, n, rx, rz):
    return [(math.cos(2 * math.pi * i / n) * rx * r.uniform(0.8, 1.15),
             math.sin(2 * math.pi * i / n) * rz * r.uniform(0.8, 1.15)) for i in range(n)]


def build_islet_float(k, big):
    r = k.rng
    poly = _blob_poly(r, 9, 1.6 if big else 1.1, 1.2 if big else 0.9)
    _poly_island(k, poly, (0.0, 0.0), 2.6 if big else 1.8, cuts=1, seed_shift=11.0 if big else 13.0)
    if big:  # small blossom tree on top
        me = K.skin_mesh([[((0, 0, -0.1), 0.18), ((0.1, 0, 0.8), 0.12), ((0.2, 0.05, 1.4), 0.08)]], subdiv=1,
                         decimate=0.6)
        k.append_mesh(me, "bark", smooth=True)
        bpy.data.meshes.remove(me)
        k.blob((0.2, 0.05, 1.7), (0.75, 0.65, 0.55), "leaf_canopy", subdiv=2, rough=0.25, freq=1.8)
    else:
        k.blob((0.3, 0.1, 0.2), (0.4, 0.35, 0.28), "stone_cool", subdiv=1, rough=0.3, flat_bottom=-0.5, smooth=False)
    for i in range(5):
        a = r.uniform(0, 6.28)
        k.cross_cards((math.cos(a) * 0.6, math.sin(a) * 0.5, 0), 0.5, 0.5, "F_grass_a", count=2, yaw=a)


def build_cloud_sea(k):
    """Big cloud plane (Unity y = placement y). Vertex A fades to the edges."""
    sx, sz = 32.0, 26.0
    nx, nz = 16, 13
    grid = {}
    for i in range(nx + 1):
        for j in range(nz + 1):
            x = -sx + 2 * sx * i / nx
            z = -sz + 2 * sz * j / nz
            h = noise.noise(Vector((x * 0.12, z * 0.12, 2.0))) * 0.35
            grid[i, j] = k.bm.verts.new(B(x, h, z))
    n0 = len(k.bm.faces)
    for i in range(nx):
        for j in range(nz):
            k.bm.faces.new((grid[i, j], grid[i + 1, j], grid[i + 1, j + 1], grid[i, j + 1]))
    faces = k._finish(k._new_faces(n0), "WATER", True)
    for f in faces:
        if f.normal.z < 0:
            f.normal_flip()
    k.water_radii = (sx * 1.25, sz * 1.25)


def build_cloud_puff(k):
    r = k.rng
    E, Bl = 'ELLIPSOID', 'BALL'
    q0 = K.quat_from_axis((1, 0, 0), 0)
    els = [(E, (0, 0, 0.4), 0.9, (1.6, 1.1, 0.7), q0)]
    for i in range(6):
        a = i * 2 * math.pi / 6 + r.uniform(-.3, .3)
        els.append((Bl, (math.cos(a) * 1.1, math.sin(a) * 0.7, 0.35 + r.uniform(0, 0.4)), r.uniform(0.5, 0.8),
                    (1, 1, 1), q0))
    me = K.metaball_mesh(els, resolution=0.16, threshold=0.6, decimate=0.5)
    k.append_mesh(me, "mushroom_stem", smooth=True)
    bpy.data.meshes.remove(me)
    for v in k.bm.verts:
        v.co.z = max(v.co.z, 0.05 + 0.08 * math.sin(v.co.x * 2.0))


def build_shrine(k):
    faces = k.cone((0, 0, 0), 1.12, 1.06, 0.3, "stone_blocks", seg=12)
    faces += k.cone((0, 0, 0.3), 0.98, 0.96, 0.12, "stone_warm", seg=12)
    for i in range(6):
        a = i * 2 * math.pi / 6
        faces += k.cone((math.cos(a) * 0.82, math.sin(a) * 0.82, 0.42), 0.13, 0.11, 1.95, "stone_warm", seg=8)
    faces += k.cone((0, 0, 2.37), 1.04, 1.04, 0.22, "stone_warm", seg=12)
    faces += k.cone((0, 0, 2.59), 1.06, 1.0, 0.06, "gold_trim", seg=12)
    k.moss_tops(faces, 0.7, from_zones={"stone_blocks", "stone_warm"}, density=0.6)
    n0 = len(k.bm.faces)
    bmesh.ops.create_uvsphere(k.bm, u_segments=12, v_segments=6, radius=1.0,
                              matrix=Matrix.Translation((0, 0, 2.6)) @ Matrix.Diagonal((0.95, 0.95, 0.5, 1)))
    dome = k._finish(k._new_faces(n0), "statue_stone", True)
    bm_del = [f for f in dome if f.calc_center_median().z < 2.62]
    bmesh.ops.delete(k.bm, geom=bm_del, context='FACES')
    k.cone((0, 0, 3.08), 0.07, 0.01, 0.35, "gold_trim", seg=6)
    k.blob((0, 0, 0.9), (0.28, 0.28, 0.28), "glow_cap", subdiv=2, rough=0.05)  # floating crystal heart
    k.cone((0, 0, 0.42), 0.26, 0.2, 0.3, "rune_slate", seg=8)


def build_spire(k):
    faces = []
    z = 0.0
    r = k.rng
    for i, (h, r0, r1) in enumerate(((0.7, 0.5, 0.42), (0.6, 0.42, 0.36), (0.55, 0.36, 0.33), (0.35, 0.33, 0.36))):
        faces += k.cone((r.uniform(-.03, .03), r.uniform(-.03, .03), z), r0, r1, h, "stone_cool", seg=7,
                        rot=(r.uniform(-.04, .04), r.uniform(-.04, .04), r.uniform(0, 1)), smooth=False)
        z += h
    for v in {v for f in faces for v in f.verts}:
        v.co.z = min(v.co.z, 2.2)
    faces += k.cone((0, 0, 2.17), 0.38, 0.36, 0.03, "moss", seg=7, smooth=False)
    faces += k.blob((0.45, 0.2, 0.1), (0.3, 0.26, 0.22), "stone_cool", subdiv=1, rough=0.3, flat_bottom=-0.4,
                    smooth=False)
    k.moss_tops(faces, 0.6, density=0.5)


def build_bridge(k):
    """Plank bridge, Unity local x -1.15 (y 0) -> +1.15 (y 0.9), arching slightly."""
    n = 9
    for i in range(n):
        t = i / (n - 1)
        ux = -1.15 + 2.3 * t
        uy = 0.9 * t + 0.18 * math.sin(t * math.pi) - 0.05
        slope = math.atan2(0.9 + 0.18 * math.pi * math.cos(t * math.pi), 2.3)
        k.box(B(ux, uy, 0.0), (0.26, 0.95, 0.06), "lily", rot=(0, slope, k.rng.uniform(-.04, .04)))
    for s in (-1, 1):
        chain = []
        for i in range(5):
            t = i / 4
            ux = -1.15 + 2.3 * t
            uy = 0.9 * t + 0.18 * math.sin(t * math.pi) + 0.55
            k.cone(B(ux, uy - 0.6, s * 0.5), 0.04, 0.035, 0.65, "bark", seg=5)
            chain.append((B(ux, uy, s * 0.5), 0.025))
        me = K.skin_mesh([chain], subdiv=0)
        k.append_mesh(me, "root", smooth=True)
        bpy.data.meshes.remove(me)


ASSETS = {
    "ocean": {
        "SM_ENV_Ocean_Seabed": build_seabed,
        "SM_ENV_Ocean_Arch_Estuary": K.build_gate,
        "SM_ENV_Ocean_Wall_Ruin": K.build_wall_a,
        "SM_ENV_Ocean_Statue_Dragon": K.build_statue,
        "SM_ENV_Ocean_Column_Sunken": build_column_sunken,
        "SM_ENV_Ocean_Column_Fallen": K.build_column_fallen,
        "SM_ENV_Ocean_Rock_Reef_A": lambda k: build_reef_rock(k, 0.95, 3, tufts=4),
        "SM_ENV_Ocean_Rock_Reef_B": lambda k: build_reef_rock(k, 0.62, 2, pieces=2, tufts=2),
        "SM_ENV_Ocean_Coral_Branch": build_coral_branch,
        "SM_ENV_Ocean_Coral_Brain": build_coral_brain,
        "SM_ENV_Ocean_Coral_Table": build_coral_table,
        "SM_ENV_Ocean_Sea_Fan": build_sea_fan,
        "SM_ENV_Ocean_Kelp": build_kelp,
        "SM_ENV_Ocean_Anemone_Glow": build_anemone,
        "SM_ENV_Ocean_Shell_Cluster": build_shells,
        "SM_ENV_Ocean_Crystal_Cool": build_crystal,
        "SM_ENV_Ocean_Vent_Rock": build_vent,
        "SM_ENV_Ocean_Cover_Estuary": lambda k: build_zone_cover(k, ocean_zone, "ocean_estuary", OCEAN_CARDS),
        "SM_ENV_Ocean_Cover_Reef": lambda k: build_zone_cover(k, ocean_zone, "ocean_reef", OCEAN_CARDS),
        "SM_ENV_Ocean_Cover_Cool_Pool": lambda k: build_zone_cover(k, ocean_zone, "ocean_cool_pool", OCEAN_CARDS),
        "SM_ENV_Ocean_Cover_Edges": lambda k: build_zone_cover(k, ocean_zone, "edges", OCEAN_CARDS),
    },
    "sky": {
        "SM_ENV_Sky_Island_Main": build_island_main,
        "SM_ENV_Sky_Islet_East": build_islet_east,
        "SM_ENV_Sky_Islet_Shrine": build_islet_shrine,
        "SM_ENV_Sky_Islet_Float_A": lambda k: build_islet_float(k, True),
        "SM_ENV_Sky_Islet_Float_B": lambda k: build_islet_float(k, False),
        "SM_ENV_Sky_Cloud_Sea": build_cloud_sea,
        "SM_ENV_Sky_Cloud_Puff": build_cloud_puff,
        "SM_ENV_Sky_Gate": K.build_gate,
        "SM_ENV_Sky_Column": K.build_column_intact,
        "SM_ENV_Sky_Column_Broken": K.build_column_broken,
        "SM_ENV_Sky_Shrine": build_shrine,
        "SM_ENV_Sky_Runestone": K.build_runestone_a,
        "SM_ENV_Sky_Tree_Blossom": K.build_tree_b,
        "SM_ENV_Sky_Shrub": K.build_shrub,
        "SM_ENV_Sky_Spire_Perch": build_spire,
        "SM_ENV_Sky_Bridge": build_bridge,
        "SM_ENV_Sky_Cover_Main": lambda k: build_zone_cover(k, sky_zone, "main", SKY_CARDS),
        "SM_ENV_Sky_Cover_East": lambda k: build_zone_cover(k, sky_zone, "east", SKY_CARDS),
        "SM_ENV_Sky_Cover_Shrine": lambda k: build_zone_cover(k, sky_zone, "shrine", SKY_CARDS),
    },
}
ZONE_MODULES = {"ocean": ocean_zone, "sky": sky_zone}

BUDGET = {
    "SM_ENV_Ocean_Seabed": 2600, "SM_ENV_Ocean_Arch_Estuary": 2600, "SM_ENV_Ocean_Statue_Dragon": 3500,
    "SM_ENV_Ocean_Cover_Estuary": 3500, "SM_ENV_Ocean_Cover_Reef": 3000, "SM_ENV_Ocean_Cover_Cool_Pool": 3000,
    "SM_ENV_Ocean_Cover_Edges": 3500, "SM_ENV_Ocean_Coral_Branch": 2200, "SM_ENV_Ocean_Rock_Reef_A": 1800,
    "SM_ENV_Sky_Island_Main": 2600, "SM_ENV_Sky_Islet_East": 1600, "SM_ENV_Sky_Islet_Shrine": 1600,
    "SM_ENV_Sky_Gate": 2600, "SM_ENV_Sky_Tree_Blossom": 3000, "SM_ENV_Sky_Cover_Main": 3500,
    "SM_ENV_Sky_Cover_East": 2000, "SM_ENV_Sky_Cover_Shrine": 1500, "SM_ENV_Sky_Cloud_Sea": 1000,
    "SM_ENV_Sky_Shrine": 2200,
}
DEFAULT_BUDGET = 1500
# Painted map sizes per zone (px). Totals are checked again by the tests.
TEXTURE_BUDGET_PIXELS = 1024 * 1024 + 512 * 512 + 1024 * 512 + 512 * 512 + 256 * 256 + 3 * 256 * 256


# ============================================================ MATERIALS
def make_zone_materials(zone, tex_dir):
    Zc = zone.capitalize()

    def img(name):
        return bpy.data.images.load(str(tex_dir / name), check_existing=True)

    def principled(name, image, alpha=False, emission=None, rough=0.9, water=False, ground=False):
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        nt = m.node_tree
        bsdf = nt.nodes.get("Principled BSDF")
        bsdf.inputs["Roughness"].default_value = rough
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = image
        vc = nt.nodes.new("ShaderNodeVertexColor")
        vc.layer_name = "Color"
        mix = nt.nodes.new("ShaderNodeMix")
        mix.data_type = 'RGBA'
        mix.blend_type = 'MULTIPLY'
        mix.inputs["Factor"].default_value = 0.0 if water else (0.6 if ground else 1.0)
        nt.links.new(tex.outputs["Color"], mix.inputs["A"])
        nt.links.new(vc.outputs["Color"], mix.inputs["B"])
        nt.links.new(mix.outputs["Result"], bsdf.inputs["Base Color"])
        if alpha:
            nt.links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
        if water:
            bsdf.inputs["Alpha"].default_value = 0.92
        if emission is not None:
            et = nt.nodes.new("ShaderNodeTexImage")
            et.image = emission
            nt.links.new(et.outputs["Color"], bsdf.inputs["Emission Color"])
            bsdf.inputs["Emission Strength"].default_value = 2.5
        return m

    mats = {
        "ENV_Props": principled(f"ENV_{Zc}_Props", img(f"T_ENV_{Zc}_PropsAtlas.png"),
                                emission=img(f"T_ENV_{Zc}_PropsEmission.png")),
        "ENV_Foliage": principled(f"ENV_{Zc}_Foliage", img(f"T_ENV_{Zc}_Foliage.png"), alpha=True),
        "ENV_Ground": principled(f"ENV_{Zc}_Ground", img(f"T_ENV_{Zc}_Ground.png"), ground=True),
    }
    if zone == "sky":
        mats["ENV_Water"] = principled("ENV_Sky_Water", img("T_ENV_Sky_Water.png"), rough=0.6, water=True)
    K.MATERIALS.clear()
    K.MATERIALS.update(mats)
    return mats


# ============================================================ VALIDATION
def validate_uvs(obj):
    me = obj.data
    uv = me.uv_layers["UVMap"].data
    errors = 0
    for p in me.polygons:
        slot = me.materials[p.material_index].name
        if not (slot.endswith("_Props") or slot.endswith("_Foliage")):
            continue
        us = [uv[li].uv[0] for li in p.loop_indices]
        vs = [uv[li].uv[1] for li in p.loop_indices]
        if min(us) < -1e-4 or max(us) > 1 + 1e-4 or min(vs) < -1e-4 or max(vs) > 1 + 1e-4:
            errors += 1
            continue
        if slot.endswith("_Props"):
            g = K.atlas.PROPS_GRID
            if len({min(int(u * g), g - 1) for u in us}) > 1 or len({min(int(v * g), g - 1) for v in vs}) > 1:
                errors += 1
    return errors


def bvh(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    t = BVHTree.FromBMesh(bm)
    bm.free()
    return t


def surface_y(tree, placement, ux, uz):
    """World height of a mesh under world (ux, uz) for a placement (ray cast down)."""
    s = placement["scale"]
    lx, lz = (ux - placement["x"]) / s, (uz - placement["z"]) / s
    lx, lz = zc.origin._rot_y(lx, lz, -placement["yaw"])
    hit = tree.ray_cast(Vector(B(lx, 50.0, lz)), Vector((0, 0, -1)))
    if hit[0] is None:
        return None
    return placement["y"] + hit[0].z * s


def check_locations(zone, objects, placements):
    """Perch / rest heights against the mesh under them; walk areas flat at their y."""
    trees = {}

    def tree(asset):
        if asset not in trees:
            trees[asset] = bvh(objects[asset])
        return trees[asset]

    report, failures = [], []
    points = [("perch",) + p for p in getattr(zone, "PERCHES", [])] + [("rest",) + p for p in getattr(zone, "REST_POINTS", [])]
    for kind, hid, src, x, y, z in points:
        cands = [p for p in placements if p["asset"] == src]
        best = None
        for p in cands:
            sy = surface_y(tree(src), p, x, z)
            if sy is not None and (best is None or abs(sy - y) < abs(best - y)):
                best = sy
        ok = best is not None and abs(best - y) <= 0.15
        report.append({"kind": kind, "source": src, "x": x, "z": z, "declaredY": y,
                       "meshY": None if best is None else round(best, 3), "ok": ok})
        if not ok:
            failures.append(f"{kind} on {src} at ({x}, {z}): declared y {y}, mesh y {best}")
    # Walk areas: sample a grid inside each polygon, the surface asset must be flat there.
    surfaces = getattr(zone, "ISLAND_ASSETS", {a["id"]: "SM_ENV_Ocean_Seabed" for a in zone.WALK_AREAS})
    for a in zone.WALK_AREAS:
        src = surfaces[a["id"]]
        pl = next(p for p in placements if p["asset"] == src)
        xs = [q[0] for q in a["polygon"]]
        zs = [q[1] for q in a["polygon"]]
        worst, n = 0.0, 0
        for i in range(12):
            for j in range(12):
                x = min(xs) + (max(xs) - min(xs)) * (i + 0.5) / 12
                z = min(zs) + (max(zs) - min(zs)) * (j + 0.5) / 12
                if not zc.point_in_poly(x, z, a["polygon"]):
                    continue
                sy = surface_y(tree(src), pl, x, z)
                d = 9.0 if sy is None else abs(sy - a["y"])
                worst = max(worst, d)
                n += 1
        ok = worst <= 0.02
        report.append({"kind": "walkArea", "id": a["id"], "samples": n, "maxDeviation": round(worst, 4), "ok": ok})
        if not ok:
            failures.append(f"walk area {a['id']}: surface deviates {worst:.3f} m from y {a['y']}")
    return report, failures


def bake_ground_colours(obj, zone, path_lines, path_width=1.0):
    """Ground faces: soft shade under every collider footprint (RGB) + path mask (A)."""
    me = obj.data
    shade_src = []
    for o in zc.obstacle_polygons(zone):
        xs = [p[0] for p in o["polygon"]]
        zs = [p[1] for p in o["polygon"]]
        cx, cz = sum(xs) / len(xs), sum(zs) / len(zs)
        shade_src.append((cx, cz, max(math.hypot(x - cx, z - cz) for x, z in o["polygon"]) * 0.8))

    def path_d(x, z):
        best = 1e9
        for line in path_lines:
            for (ax, az), (bx, bz) in zip(line, line[1:]):
                dx, dz = bx - ax, bz - az
                t = max(0.0, min(1.0, ((x - ax) * dx + (z - az) * dz) / (dx * dx + dz * dz)))
                best = min(best, math.hypot(x - (ax + t * dx), z - (az + t * dz)))
        return best

    col = me.color_attributes["Color"]
    data = [0.0] * (len(me.loops) * 4)
    col.data.foreach_get("color", data)
    ground_slot = next(i for i, m in enumerate(me.materials) if m.name.endswith("_Ground"))
    loc = obj.matrix_world
    for p in me.polygons:
        if p.material_index != ground_slot:
            continue
        for li in p.loop_indices:
            co = loc @ me.vertices[me.loops[li].vertex_index].co
            ux, uz = -co.x, -co.y
            shade = 1.0
            for bx, bz, br in shade_src:
                dd = math.hypot(ux - bx, uz - bz)
                if dd < br * 1.6:
                    shade = min(shade, 0.74 + 0.26 * min(1.0, max(0.0, (dd - br * 0.6) / (br * 0.9))))
            edge = noise.noise(Vector((ux * 0.9, uz * 0.9, 1.7))) * 0.25
            path = 1.0 - min(1.0, max(0.0, (path_d(ux, uz) - path_width * 0.5 + edge) / 0.45))
            data[li * 4:li * 4 + 4] = (shade, shade, shade * 0.98, path)
    col.data.foreach_set("color", data)


OCEAN_PATHS = [[(-5.0, 5.2), (-4.4, 2.2), (-2.6, 0.4), (0.0, -0.3), (2.6, 0.3), (4.6, 0.2)]]
SKY_PATHS = [[(0.0, 5.7), (0.3, 3.0), (0.0, 0.4), (1.6, -0.7), (3.1, -1.0)], [(0.0, 0.4), (-2.2, 1.4), (-3.6, 2.6)]]


# ================================================================ PREVIEW
def render_zone_preview(zone, objects, layout, path, fx_dir):
    """Cycles preview with the fixed Unity camera, day and night, at 16:9 and 20:9."""
    scn = bpy.context.scene
    coll = bpy.data.collections.new("PreviewLayout")
    scn.collection.children.link(coll)
    for o in objects.values():
        o.hide_render = True
    for pl in layout["placements"]:
        src = objects.get(pl["asset"])
        if src is None:
            continue
        inst = bpy.data.objects.new(pl["asset"] + "_inst", src.data)
        inst.location = B(pl["x"], pl["y"], pl["z"])
        inst.rotation_euler = (0, 0, -math.radians(pl["yaw"]))
        inst.scale = (pl["scale"],) * 3
        coll.objects.link(inst)
    cd = bpy.data.cameras.new("Cam")
    cd.type = 'ORTHO'
    cd.clip_end = 200
    cam = bpy.data.objects.new("Cam", cd)
    px, py, pz = zc.origin.camera_position()
    cam.location = B(px, py, pz)
    p = math.radians(zc.origin.PITCH_DEG)
    cam.rotation_euler = Vector((0.0, -math.cos(p), -math.sin(p))).to_track_quat('-Z', 'Y').to_euler()
    scn.collection.objects.link(cam)
    scn.camera = cam
    sun_d = bpy.data.lights.new("Sun", 'SUN')
    sun_d.angle = math.radians(8)
    sun = bpy.data.objects.new("Sun", sun_d)
    scn.collection.objects.link(sun)
    world = bpy.data.worlds.new("World")
    world.use_nodes = True
    nt = world.node_tree
    bg = nt.nodes.get("Background")
    vol = nt.nodes.new("ShaderNodeVolumePrincipled")
    nt.links.new(vol.outputs["Volume"], nt.nodes.get("World Output").inputs["Volume"])
    scn.world = world
    scn.render.engine = 'CYCLES'
    scn.cycles.device = 'CPU'
    scn.cycles.samples = 24
    scn.cycles.use_denoising = True
    scn.cycles.max_bounces = 3
    scn.cycles.volume_bounces = 0
    if hasattr(scn.cycles, "volume_step_rate"):
        scn.cycles.volume_step_rate = 4.0
    scn.view_settings.view_transform = 'Standard'

    # Cosmetic FX stand-ins: caustic sheet + light shafts (ocean), glowing motes at night.
    fx_coll = bpy.data.collections.new("PreviewFX")
    scn.collection.children.link(fx_coll)

    def fx_material(name, tex, strength, color):
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        t = m.node_tree
        t.nodes.clear()
        out = t.nodes.new("ShaderNodeOutputMaterial")
        em = t.nodes.new("ShaderNodeEmission")
        em.inputs["Color"].default_value = color
        em.inputs["Strength"].default_value = strength
        tr = t.nodes.new("ShaderNodeBsdfTransparent")
        mx = t.nodes.new("ShaderNodeMixShader")
        im = t.nodes.new("ShaderNodeTexImage")
        im.image = bpy.data.images.load(str(fx_dir / tex), check_existing=True)
        t.links.new(im.outputs["Alpha"], mx.inputs["Fac"])
        t.links.new(tr.outputs["BSDF"], mx.inputs[1])
        t.links.new(em.outputs["Emission"], mx.inputs[2])
        t.links.new(mx.outputs["Shader"], out.inputs["Surface"])
        return m, em

    fx_nodes = []
    living = zc.build_living(zone)
    for fx in living["fx"]:
        me = bpy.data.meshes.new(fx["id"])
        w, h = fx["width"], fx["height"]
        bm = bmesh.new()
        uvl = bm.loops.layers.uv.new("UVMap")
        if fx["kind"] == "caustics":
            vs = [bm.verts.new(B(fx["x"] + dx * w / 2, fx["y"], fx["z"] + dz * h / 2)) for dx, dz in
                  ((-1, -1), (1, -1), (1, 1), (-1, 1))]
            uvs = [(0, 0), (fx["tiling"], 0), (fx["tiling"], fx["tiling"] * h / w), (0, fx["tiling"] * h / w)]
        else:
            t = math.radians(fx["tilt"])
            vs = []
            for dx, dy in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
                yy = dy * h / 2
                vs.append(bm.verts.new(B(fx["x"] + dx * w / 2 + math.sin(t) * yy, fx["y"] + math.cos(t) * yy, fx["z"])))
            uvs = [(0, 0), (1, 0), (1, 1), (0, 1)]
        f = bm.faces.new(vs)
        for l, uv in zip(f.loops, uvs):
            l[uvl].uv = uv
        bm.to_mesh(me)
        bm.free()
        strength = 1.2 * fx["alpha"] if fx["kind"] == "caustics" else 1.1 * fx["alpha"]
        mat, em = fx_material(fx["id"], fx["texture"] + ".png", strength, (0.85, 1.0, 1.0, 1))
        me.materials.append(mat)
        o = bpy.data.objects.new(fx["id"], me)
        fx_coll.objects.link(o)
        fx_nodes.append((em, strength))
    mote_mat = bpy.data.materials.new("Mote")
    mote_mat.use_nodes = True
    mb = mote_mat.node_tree.nodes.get("Principled BSDF")
    mote_col = (0.45, 1.0, 1.0, 1) if zone.ZONE_ID == "ocean" else (0.85, 0.92, 1.0, 1)
    mb.inputs["Base Color"].default_value = mote_col
    mb.inputs["Emission Color"].default_value = mote_col
    mb.inputs["Emission Strength"].default_value = 12.0
    mote_mesh = bpy.data.meshes.new("MoteMesh")
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=1, radius=0.04)
    bm.to_mesh(mote_mesh)
    bm.free()
    mote_mesh.materials.append(mote_mat)
    motes = bpy.data.collections.new("PreviewMotes")
    scn.collection.children.link(motes)
    rng = random.Random(77)

    props = K.MATERIALS.get("ENV_Props")
    pbsdf = props.node_tree.nodes.get("Principled BSDF") if props else None
    outputs = []
    shots = [("day", 13.0, ("16x9", "20x9")), ("night", 22.0, ("16x9",))]
    if zone.ZONE_ID == "sky":
        shots.insert(1, ("dusk", 18.3, ("16x9",)))
    fog = zone.FOG
    for label, hour, aspects in shots:
        st = zc.evaluate(zone, hour)
        f = zc.origin_living.light_forward(st["lightElevation"], st["lightAzimuth"])
        sun.rotation_euler = Vector((-f[0], -f[2], f[1])).to_track_quat('-Z', 'Y').to_euler()
        sun_d.color = st["lightColor"]
        sun_d.energy = 3.2 * st["lightIntensity"] / 1.35
        bg.inputs["Color"].default_value = tuple(st["ambientEquator"]) + (1,)
        bg.inputs["Strength"].default_value = 0.9 if label != "night" else 0.7
        vol.inputs["Color"].default_value = tuple(st["fogColor"]) + (1,)
        vol.inputs["Density"].default_value = 0.0
        vol.inputs["Emission Color"].default_value = tuple(st["fogColor"]) + (1,)
        density = 1.6 / (fog["end"] - fog["start"]) * (0.3 if zone.ZONE_ID == "ocean" else 0.17)
        vol.inputs["Density"].default_value = density * 0.6
        vol.inputs["Emission Strength"].default_value = density * 0.55
        scn.view_settings.exposure = -0.8 if label == "night" else 0.0
        if pbsdf is not None:
            pbsdf.inputs["Emission Strength"].default_value = 2.5 * (1 + 1.6 * st["nightGlow"])
        light = st["lightIntensity"] / 1.05
        for em, s in fx_nodes:
            em.inputs["Strength"].default_value = s * light
        for o in list(motes.objects):
            bpy.data.objects.remove(o)
        for vid, v in living_particles(living):
            if v["channel"] not in ("fireflies", "firefliesPond"):
                continue
            for _ in range(int(round(v["maxParticles"] * st["fireflies"]))):
                c, s = v["center"], v["size"]
                mo = bpy.data.objects.new("Mote", mote_mesh)
                mo.location = B(c[0] + (rng.random() - 0.5) * s[0], c[1] + (rng.random() - 0.5) * s[1],
                                c[2] + (rng.random() - 0.5) * s[2])
                motes.objects.link(mo)
        for asp in aspects:
            a = 16 / 9 if asp == "16x9" else 20 / 9
            hpx = 720
            scn.render.resolution_x, scn.render.resolution_y = int(round(hpx * a)), hpx
            cd.ortho_scale = 2 * zc.origin.ORTHO_SIZE * a
            out = Path(str(path).replace(".png", f"_{label}_{asp}.png"))
            scn.render.filepath = str(out)
            bpy.ops.render.render(write_still=True)
            outputs.append(out)
            print("[zones] preview", out)
    scn.view_settings.exposure = 0.0
    return outputs


def living_particles(living):
    return [(v["id"], v) for v in living["particleVolumes"]]


# ==================================================================== MAIN
def build_zone(zone_id):
    zone = ZONE_MODULES[zone_id]
    Zc = zone_id.capitalize()
    out = OUT / Zc
    out.mkdir(parents=True, exist_ok=True)
    tex_dir = out / "Textures"
    zone_textures.generate_zone(zone_id, str(tex_dir))
    data_files = zc.write_all(zone, str(out / "Data"))
    audio_dir = out / "Audio"
    zone_audio.generate_zone(zone_id, str(audio_dir))

    bpy.ops.wm.read_factory_settings(use_empty=True)
    make_zone_materials(zone_id, tex_dir)
    fbx_dir = out / "Models"
    fbx_dir.mkdir(exist_ok=True)
    manifest = {"generator": "scripts/art/environment/build_zone_kits.py", "zoneId": zone.ZONE_ID,
                "blender": bpy.app.version_string, "assets": {}}
    objects, failures = {}, []
    for i, (name, fn) in enumerate(ASSETS[zone_id].items()):
        k = K.Kit(name, seed=(4000 if zone_id == "ocean" else 6000) + i * 37)
        fn(k)
        obj = K.to_object(k)
        objects[name] = obj
        st = K.stats(obj)
        st["uv_errors"] = validate_uvs(obj)
        budget = BUDGET.get(name, DEFAULT_BUDGET)
        st["budget"] = budget
        manifest["assets"][name] = st
        if st["triangles"] > budget:
            failures.append(f"{name}: {st['triangles']} tris > budget {budget}")
        if st["uv_errors"]:
            failures.append(f"{name}: {st['uv_errors']} faces with UVs outside their atlas tile")
        print(f"[zones] {name:34s} {st['triangles']:5d} tris  {st['materials']}")

    if zone_id == "ocean":
        bake_ground_colours(objects["SM_ENV_Ocean_Seabed"], zone, OCEAN_PATHS)
    else:
        for a, asset in zone.ISLAND_ASSETS.items():
            o = objects[asset]
            pl = next(p for p in zone.hero_props() if p["asset"] == asset)
            o.location = B(pl["x"], pl["y"], pl["z"])  # bake in world space, then reset
            bpy.context.view_layer.update()
            bake_ground_colours(o, zone, SKY_PATHS, path_width=0.9)
            o.location = (0, 0, 0)
            bpy.context.view_layer.update()
    for name, obj in objects.items():
        K.export_fbx(obj, fbx_dir / f"{name}.fbx")

    layout = zc.build_layout(zone)
    placements = layout["placements"]
    missing = sorted({p["asset"] for p in placements} - set(objects))
    if missing:
        failures.append("placements without asset: " + ", ".join(missing))
    total = sum(manifest["assets"][p["asset"]]["triangles"] for p in placements if p["asset"] in manifest["assets"])
    manifest["layout"] = {"placements": len(placements), "scene_triangles": total,
                          "scene_triangle_budget": SCENE_TRIANGLE_BUDGET}
    if total > SCENE_TRIANGLE_BUDGET:
        failures.append(f"scene triangles {total} > {SCENE_TRIANGLE_BUDGET}")
    report, loc_failures = check_locations(zone, objects, placements)
    manifest["locationChecks"] = report
    failures += loc_failures
    with open(out / "Data" / f"{zone.FILE_PREFIX}Manifest.json", "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=1)
    print(f"[zones] {zone_id}: {len(placements)} placements, ~{total} triangles, "
          f"{sum(1 for r in report if r['ok'])}/{len(report)} location checks OK")

    for i, o in enumerate(objects.values()):
        o.location = ((i % 6) * 7.0, (i // 6) * 7.0, 0)
    bpy.ops.wm.save_as_mainfile(filepath=str(out / f"{Zc}Kit.blend"))
    for o in objects.values():
        o.location = (0, 0, 0)

    if PREVIEW:
        render_zone_preview(zone, objects, layout, out / f"{Zc}_preview.png", tex_dir)

    if UNITY:
        dst = Path(UNITY).resolve() / "Zones" / Zc
        for sub in ("Models", "Textures", "Data", "Audio"):
            (dst / sub).mkdir(parents=True, exist_ok=True)
        for p in fbx_dir.glob("*.fbx"):
            shutil.copy2(p, dst / "Models" / p.name)
        for p in tex_dir.glob("*.png"):
            shutil.copy2(p, dst / "Textures" / p.name)
        for p in (out / "Data").glob("*.json"):
            shutil.copy2(p, dst / "Data" / p.name)
        for p in audio_dir.glob("*.wav"):
            shutil.copy2(p, dst / "Audio" / p.name)
        print("[zones] copied into", dst)
    return failures


def main():
    failures = []
    for z in ZONES_ARG:
        failures += [f"{z}: {f}" for f in build_zone(z)]
    if failures:
        for f in failures:
            print("[zones] FAILURE:", f)
        raise SystemExit(1)
    print("[zones] done ->", OUT)


if __name__ == "__main__":
    main()
