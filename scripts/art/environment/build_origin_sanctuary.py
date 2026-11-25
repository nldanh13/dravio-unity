"""U1-ENV  Origin Sanctuary - procedural modular environment kit for Dravio.

Builds every environment mesh from code (no external assets), unwraps it into the
shared painterly atlas, bakes vertex colours (painted AO + wind/path mask) and
exports one FBX per modular asset, ready for the Unity Environment tools.

Blender 5.3 (also runs with the `bpy` 5.3 Python module). From the repo root:

  blender --factory-startup --background --python-exit-code 1 \
      --python scripts/art/environment/build_origin_sanctuary.py -- \
      --out art-exports/environment --unity Assets/Dravio/Environment [--preview]

  --out      working folder: FBX, .blend source, textures, manifest, preview
  --unity    optional: also copy FBX/textures/layout into the Unity folder
             (Models/, Textures/, Data/, Audio/). Never touches other Unity folders.
  --preview  render Cycles previews of the assembled layout: day at 16:9 and 20:9,
             plus dusk and night at 16:9 (U1-ENV.3 living environment)
  --only     comma separated asset names to rebuild (default: all)

Conventions (see README in Assets/Dravio/Environment):
  * 1 Blender unit = 1 m = 1 Unity unit, pivot at the base centre of the asset.
  * Decorative front faces Blender +Y == Unity -Z == towards the fixed camera.
  * Material slots: ENV_Props (atlas, opaque), ENV_Foliage (alpha clip cards),
    ENV_Water (transparent), ENV_Ground (tiling meadow).
  * Vertex colour RGB = painted ambient occlusion / gradient.
    Vertex colour A   = wind weight (props & foliage) or path mask (ground).
"""
import json
import math
import os
import random
import shutil
import sys
from pathlib import Path

import bpy  # must come first when run as the `bpy` Python module
import bmesh
from mathutils import Matrix, Vector, noise

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import atlas  # noqa: E402
import layout as layout_mod  # noqa: E402
import painterly_textures  # noqa: E402
import living  # noqa: E402
import fx_textures  # noqa: E402
import ambience_audio  # noqa: E402

# ----------------------------------------------------------------------- args
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def arg(name, default=None, flag=False):
    if name in argv:
        if flag:
            return True
        i = argv.index(name)
        return argv[i + 1] if i + 1 < len(argv) else default
    return False if flag else default


OUT = Path(arg("--out", "art-exports/environment")).resolve()
UNITY = arg("--unity")
PREVIEW = arg("--preview", flag=True)
ONLY = set(filter(None, (arg("--only", "") or "").split(",")))

# --------------------------------------------------------------------- zones
FOLIAGE_ZONES = ["F_" + c for c in atlas.FOLIAGE_CELLS]
ZONES = list(atlas.PROPS_TILES) + FOLIAGE_ZONES + ["WATER", "GROUND"]
Z = {name: i for i, name in enumerate(ZONES)}

FINAL_SLOTS = ["ENV_Props", "ENV_Foliage", "ENV_Water", "ENV_Ground"]


def final_slot(zone):
    if zone.startswith("F_"):
        return "ENV_Foliage"
    if zone == "WATER":
        return "ENV_Water"
    if zone == "GROUND":
        return "ENV_Ground"
    return "ENV_Props"


# World size (m) one atlas tile covers before it is clamped into its cell.
TILE_WORLD = {
    "stone_warm": 1.4, "stone_cool": 1.4, "stone_blocks": 2.2, "moss": 1.0,
    "bark": 1.6, "root": 1.2, "leaf_canopy": 3.0, "statue_stone": 1.5,
    "mushroom_cap": 0.35, "mushroom_stem": 0.4, "rune_slate": 1.2, "rune_glyph": 1.0,
    "earth": 2.0, "lily": 0.6, "gold_trim": 1.6, "glow_cap": 0.35,
}

# ------------------------------------------------------------------- helpers
FRONT = Vector((0, 1, 0))


class Kit:
    """One asset under construction: a bmesh plus per-face zone indices."""

    def __init__(self, name, seed):
        self.name = name
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")
        self.rng = random.Random(seed)
        self.seed = seed
        self.card_faces = set()

    # -- low level --------------------------------------------------------
    def _new_faces(self, n0):
        self.bm.faces.ensure_lookup_table()
        return list(self.bm.faces[n0:])

    def _finish(self, faces, zone, smooth):
        for f in faces:
            f.material_index = Z[zone]
            f.smooth = smooth
        return faces

    def box(self, center, size, zone, rot=(0, 0, 0), bevel=0.0, smooth=False):
        n0 = len(self.bm.faces)
        m = (Matrix.Translation(center) @ Matrix.Rotation(rot[2], 4, 'Z') @
             Matrix.Rotation(rot[1], 4, 'Y') @ Matrix.Rotation(rot[0], 4, 'X') @
             Matrix.Diagonal((*size, 1)))
        res = bmesh.ops.create_cube(self.bm, size=1.0, matrix=m)
        if bevel > 0:
            verts = res["verts"]
            edges = list({e for v in verts for e in v.link_edges})
            bmesh.ops.bevel(self.bm, geom=verts + edges, offset=bevel, segments=1,
                            affect='EDGES', profile=0.5, clamp_overlap=True)
        return self._finish(self._new_faces(n0), zone, smooth)

    def cone(self, base, r1, r2, depth, zone, seg=10, rot=(0, 0, 0), smooth=True, caps=True):
        """Cylinder/cone whose base centre is at `base`, axis along rotated +Z."""
        n0 = len(self.bm.faces)
        R = (Matrix.Rotation(rot[2], 4, 'Z') @ Matrix.Rotation(rot[1], 4, 'Y') @
             Matrix.Rotation(rot[0], 4, 'X'))
        m = Matrix.Translation(base) @ R @ Matrix.Translation((0, 0, depth / 2))
        bmesh.ops.create_cone(self.bm, cap_ends=caps, cap_tris=False, segments=seg,
                              radius1=r1, radius2=r2, depth=depth, matrix=m)
        faces = self._new_faces(n0)
        for f in faces:  # caps flat, sides smooth
            f.smooth = smooth and abs((R.to_3x3() @ Vector((0, 0, 1))).dot(f.normal)) < 0.9
            f.material_index = Z[zone]
        return faces

    def blob(self, center, radius, zone, subdiv=2, rough=0.18, freq=1.3, flat_bottom=None,
             smooth=True, rot_z=0.0):
        n0 = len(self.bm.faces)
        m = Matrix.Translation(center) @ Matrix.Rotation(rot_z, 4, 'Z')
        res = bmesh.ops.create_icosphere(self.bm, subdivisions=subdiv, radius=1.0, matrix=Matrix())
        off = Vector((self.rng.random() * 50, self.rng.random() * 50, self.rng.random() * 50))
        for v in res["verts"]:
            d = v.co.normalized()
            n = noise.noise(d * freq + off)
            n += 0.5 * noise.noise(d * freq * 2.3 + off)
            co = d * (1.0 + rough * n)
            co = Vector((co.x * radius[0], co.y * radius[1], co.z * radius[2]))
            if flat_bottom is not None:
                co.z = max(co.z, flat_bottom * radius[2])
            v.co = m @ co
        return self._finish(self._new_faces(n0), zone, smooth)

    def card(self, center, width, height, zone, yaw=0.0, tilt=0.0, pivot_bottom=True):
        """Single quad card with UV 0..1 (remapped to the foliage cell later)."""
        R = Matrix.Rotation(yaw, 3, 'Z') @ Matrix.Rotation(tilt, 3, 'X')
        hw = width / 2
        z0, z1 = (0, height) if pivot_bottom else (-height, 0)
        pts = [(-hw, 0, z0), (hw, 0, z0), (hw, 0, z1), (-hw, 0, z1)]
        uvs = [(0, 0), (1, 0), (1, 1), (0, 1)]
        verts = [self.bm.verts.new(Vector(center) + R @ Vector(p)) for p in pts]
        f = self.bm.faces.new(verts)
        for loop, uv in zip(f.loops, uvs):
            loop[self.uv].uv = uv
        f.material_index = Z[zone]
        f.smooth = True
        self.card_faces.add(f)
        return [f]

    def cross_cards(self, center, width, height, zone, count=3, yaw=0.0, tilt=0.12):
        faces = []
        for i in range(count):
            a = yaw + i * math.pi / count
            faces += self.card(center, width, height, zone, yaw=a,
                               tilt=tilt * (1 if i % 2 else -1))
        return faces

    def append_mesh(self, mesh, zone, smooth=True, matrix=None):
        n0 = len(self.bm.faces)
        if matrix is not None:
            mesh.transform(matrix)
        self.bm.from_mesh(mesh)
        faces = self._new_faces(n0)
        return self._finish(faces, zone, smooth)

    # -- shaping -----------------------------------------------------------
    def jitter(self, faces, amount):
        verts = {v for f in faces for v in f.verts}
        for v in verts:
            v.co += Vector((self.rng.uniform(-1, 1), self.rng.uniform(-1, 1),
                            self.rng.uniform(-1, 1))) * amount

    def moss_tops(self, faces, threshold=0.62, from_zones=None, density=1.0):
        """Paint moss on upward faces with a noisy boundary (looks hand placed)."""
        self.bm.normal_update()
        off = Vector((self.seed * 0.37, self.seed * 0.11, 0))
        for f in faces:
            if from_zones and ZONES[f.material_index] not in from_zones:
                continue
            c = f.calc_center_median()
            n = noise.noise(c * 1.7 + off) * 0.35
            if f.normal.z > threshold + n and (density >= 1 or self.rng.random() < density):
                f.material_index = Z["moss"]

    def zone_faces_where(self, faces, pred, zone):
        for f in faces:
            if pred(f):
                f.material_index = Z[zone]


def eval_object_mesh(obj):
    dg = bpy.context.evaluated_depsgraph_get()
    mesh = bpy.data.meshes.new_from_object(obj.evaluated_get(dg))
    return mesh


def remove_object(obj):
    data = obj.data
    bpy.data.objects.remove(obj, do_unlink=True)
    if data is not None and data.users == 0:
        if isinstance(data, bpy.types.Mesh):
            bpy.data.meshes.remove(data)
        elif isinstance(data, bpy.types.MetaBall):
            bpy.data.metaballs.remove(data)


def resample_chain(chain, step):
    """Catmull-Rom resample so Skin produces round, evenly spaced rings."""
    pts = [Vector(p) for p, _ in chain]
    rad = [r for _, r in chain]
    out = []
    for i in range(len(pts) - 1):
        p0, p1 = pts[max(i - 1, 0)], pts[i]
        p2, p3 = pts[i + 1], pts[min(i + 2, len(pts) - 1)]
        n = max(1, int((p2 - p1).length / step))
        for j in range(n):
            t = j / n
            t2, t3 = t * t, t * t * t
            p = 0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 +
                       (-p0 + 3 * p1 - 3 * p2 + p3) * t3)
            out.append((tuple(p), rad[i] + (rad[i + 1] - rad[i]) * t))
    out.append((tuple(pts[-1]), rad[-1]))
    return out


def skin_mesh(chains, subdiv=1, decimate=1.0, step=0.3):
    """Organic tubes (trunks, roots, vines) from (point, radius) chains via Skin."""
    me = bpy.data.meshes.new("skin_src")
    verts, edges, radii, roots = [], [], [], []
    for chain in chains:
        chain = resample_chain(chain, step)
        base = len(verts)
        roots.append(base)
        for i, (p, r) in enumerate(chain):
            verts.append(p)
            radii.append(r)
            if i:
                edges.append((base + i - 1, base + i))
    me.from_pydata(verts, edges, [])
    obj = bpy.data.objects.new("skin_tmp", me)
    bpy.context.scene.collection.objects.link(obj)
    skin = obj.modifiers.new("skin", 'SKIN')
    skin.use_smooth_shade = True
    sv = me.skin_vertices[0].data
    for i, r in enumerate(radii):
        sv[i].radius = (r, r)
    for i in roots:
        sv[i].use_root = True
    if subdiv:
        s = obj.modifiers.new("subd", 'SUBSURF')
        s.levels = subdiv
        s.render_levels = subdiv
    if decimate < 1.0:
        d = obj.modifiers.new("dec", 'DECIMATE')
        d.ratio = decimate
    mesh = eval_object_mesh(obj)
    remove_object(obj)
    return mesh


def metaball_mesh(elements, resolution=0.07, threshold=0.6, decimate=1.0):
    mb = bpy.data.metaballs.new("mb")
    mb.resolution = resolution
    mb.render_resolution = resolution
    mb.threshold = threshold
    obj = bpy.data.objects.new("mb_tmp", mb)
    bpy.context.scene.collection.objects.link(obj)
    for kind, co, radius, size, rot in elements:
        e = mb.elements.new(type=kind)
        e.co = co
        e.radius = radius
        if kind == 'ELLIPSOID':
            e.size_x, e.size_y, e.size_z = size
        elif kind == 'CAPSULE':
            e.size_x = size[0]
        e.rotation = rot
    bpy.context.view_layer.update()
    mesh = eval_object_mesh(obj)
    remove_object(obj)
    if decimate < 1.0:
        tmp = bpy.data.objects.new("dec_tmp", mesh)
        bpy.context.scene.collection.objects.link(tmp)
        d = tmp.modifiers.new("dec", 'DECIMATE')
        d.ratio = decimate
        out = eval_object_mesh(tmp)
        bpy.data.objects.remove(tmp, do_unlink=True)
        bpy.data.meshes.remove(mesh)
        mesh = out
    return mesh


def quat_from_axis(axis, angle):
    from mathutils import Quaternion
    return Quaternion(axis, angle)


# ============================================================ ASSET BUILDERS
def build_gate(k):
    faces = []
    span, pillar_w = 2.05, 0.95
    for s in (-1, 1):
        x = s * span
        faces += k.box((x, 0, 0.2), (1.25, 1.15, 0.4), "stone_blocks", bevel=0.05)
        z = 0.4
        for i in range(4):
            h = 0.78 + k.rng.uniform(-0.05, 0.06)
            faces += k.box((x + k.rng.uniform(-.04, .04), k.rng.uniform(-.03, .03), z + h / 2),
                           (pillar_w * k.rng.uniform(.94, 1.02), 0.88, h), "stone_warm",
                           rot=(0, k.rng.uniform(-.03, .03), k.rng.uniform(-.05, .05)), bevel=0.05)
            z += h + 0.015
        faces += k.box((x, 0, z + 0.14), (1.18, 1.05, 0.28), "stone_warm", bevel=0.05)
    # Arch voussoirs on a semicircle sitting on the pillar capitals.
    spring = 3.88
    n = 9
    for i in range(n):
        a = math.pi * (i + 0.5) / n
        r = span
        cx, cz = math.cos(a) * r, spring + math.sin(a) * r
        size = (0.72 if i != n // 2 else 0.8, 0.86, 0.78 if i != n // 2 else 0.98)
        zone = "stone_warm"
        f = k.box((cx, 0, cz), size, zone, rot=(0, -(a - math.pi / 2), 0), bevel=0.04)
        if i == n // 2:  # glowing keystone glyph on the camera side
            for face in f:
                if face.normal.dot(FRONT) > 0.9:
                    face.material_index = Z["rune_glyph"]
        faces += f
    # Thin gold inlay band across the arch front.
    for i in range(n):
        a = math.pi * (i + 0.5) / n
        r = span - 0.42
        faces += k.box((math.cos(a) * r, 0.44, spring + math.sin(a) * r), (0.5, 0.04, 0.08),
                       "gold_trim", rot=(0, -(a - math.pi / 2), 0))
    # Worn steps on the camera side, and one fallen block.
    faces += k.box((0, 0.95, 0.09), (3.0, 0.8, 0.18), "stone_blocks", bevel=0.04)
    faces += k.box((0, 1.55, 0.05), (2.4, 0.55, 0.12), "stone_blocks", bevel=0.03)
    faces += k.box((2.9, 1.2, 0.26), (0.7, 0.62, 0.55), "stone_warm",
                   rot=(0.25, 0.18, 0.5), bevel=0.05)
    k.moss_tops(faces, 0.6, from_zones={"stone_warm", "stone_blocks"})


def column_drums(k, x, y, z0, count, broken=False):
    faces = []
    z = z0
    for i in range(count):
        h = 0.92 + k.rng.uniform(-.06, .06)
        r = 0.42 - i * 0.012
        f = k.cone((x, y, z), r, r - 0.01, h, "stone_warm", seg=10,
                   rot=(k.rng.uniform(-.02, .02), k.rng.uniform(-.02, .02), k.rng.uniform(0, 1)))
        if broken and i == count - 1:
            # Slanted, chipped break on the top drum.
            top = {v for face in f for v in face.verts if v.co.z > z + h * 0.6}
            for v in top:
                v.co.z -= 0.35 * (v.co.x - x + 0.4) + k.rng.uniform(0, .12)
        faces += f
        z += h + 0.01
    return faces, z


def build_column_intact(k):
    faces = k.box((0, 0, 0.16), (1.1, 1.1, 0.32), "stone_blocks", bevel=0.05)
    f, z = column_drums(k, 0, 0, 0.32, 3)
    faces += f
    faces += k.cone((0, 0, z), 0.46, 0.56, 0.22, "stone_warm", seg=10)
    faces += k.box((0, 0, z + 0.36), (1.12, 1.12, 0.26), "stone_warm", bevel=0.05)
    faces += k.box((0, 0.57, z + 0.36), (0.9, 0.03, 0.08), "gold_trim")
    k.moss_tops(faces, 0.55)


def build_column_broken(k):
    faces = k.box((0, 0, 0.16), (1.1, 1.1, 0.32), "stone_blocks", bevel=0.05, rot=(0, 0, .2))
    f, _ = column_drums(k, 0, 0, 0.32, 2, broken=True)
    faces += f
    faces += k.blob((0.75, 0.35, 0.12), (0.28, 0.25, 0.2), "stone_warm", subdiv=1, rough=.3,
                    flat_bottom=-0.4, smooth=False)
    k.moss_tops(faces, 0.5)


def build_column_fallen(k):
    faces = []
    x = -1.3
    for i in range(3):
        h = 0.9
        rot = (0, math.pi / 2 + k.rng.uniform(-.06, .06), k.rng.uniform(-.25, .25))
        faces += k.cone((x, k.rng.uniform(-.15, .15), 0.32), 0.42, 0.41, h, "stone_warm",
                        seg=10, rot=rot)
        x += h + 0.12 + k.rng.uniform(0, .15)
    faces += k.box((x + 0.35, 0.2, 0.12), (1.0, 1.0, 0.26), "stone_warm",
                   rot=(0.15, 0.1, 0.4), bevel=0.05)
    # Sink slightly into the ground so it reads as long fallen.
    for v in {v for f in faces for v in f.verts}:
        v.co.z -= 0.08
    k.moss_tops(faces, 0.45, density=1.0)


def wall_slab(k, x0, length, depth, heights, zone, yaw=0.0, origin=(0, 0, 0)):
    """Extruded broken wall profile. heights: list of top heights along length."""
    n = len(heights)
    pts = [(-length / 2, 0.0), (length / 2, 0.0)]
    for i in reversed(range(n)):
        pts.append((-length / 2 + length * i / (n - 1), heights[i]))
    R = Matrix.Rotation(yaw, 4, 'Z')
    T = Matrix.Translation(Vector(origin) + Vector((x0, 0, 0)))
    n0 = len(k.bm.faces)
    front = [k.bm.verts.new(T @ R @ Vector((px, depth / 2, pz))) for px, pz in pts]
    back = [k.bm.verts.new(T @ R @ Vector((px, -depth / 2, pz))) for px, pz in pts]
    m = len(pts)
    k.bm.faces.new(front[::-1])
    k.bm.faces.new(back)
    for i in range(m):
        j = (i + 1) % m
        k.bm.faces.new((front[i], front[j], back[j], back[i]))
    faces = k._new_faces(n0)
    bmesh.ops.recalc_face_normals(k.bm, faces=faces)
    return k._finish(faces, zone, False)


def build_wall_a(k):
    heights = [2.3, 2.5, 1.9, 2.0, 1.4, 1.55, 0.95, 0.7]
    faces = wall_slab(k, 0, 3.4, 0.7, heights, "stone_blocks")
    faces += k.box((-1.25, 0.05, 2.62), (0.7, 0.62, 0.42), "stone_warm", rot=(0, 0.06, 0.1), bevel=0.04)
    for i in range(4):
        faces += k.blob((1.0 + i * 0.45 + k.rng.uniform(-.1, .1), 0.75 + k.rng.uniform(-.2, .3), 0.1),
                        (0.28, 0.24, 0.2), "stone_warm", subdiv=1, rough=.35, flat_bottom=-0.3,
                        smooth=False)
    k.moss_tops(faces, 0.55)


def build_wall_b(k):
    faces = wall_slab(k, 0.4, 2.4, 0.7, [1.8, 1.95, 1.4, 1.1, 0.6], "stone_blocks")
    faces += wall_slab(k, 0, 1.6, 0.7, [1.8, 1.3, 0.9, 0.5], "stone_blocks",
                       yaw=math.pi / 2, origin=(-0.85, -0.45, 0))
    faces += k.box((1.95, 0.5, 0.25), (0.62, 0.55, 0.5), "stone_warm", rot=(0.2, 0.1, 0.7), bevel=0.04)
    k.moss_tops(faces, 0.55)


def build_statue(k):
    faces = k.box((0, 0, 0.32), (1.7, 1.5, 0.64), "stone_blocks", bevel=0.05)
    faces += k.box((0, 0, 0.67), (1.78, 1.58, 0.1), "gold_trim", bevel=0.02)
    faces += k.box((0, 0, 0.8), (1.6, 1.4, 0.16), "stone_warm", bevel=0.04)
    base = 0.88
    E, B = 'ELLIPSOID', 'BALL'
    q0 = quat_from_axis((1, 0, 0), 0)
    els = [
        (E, (0, -0.05, base + 0.52), 0.55, (0.95, 1.1, 1.0), q0),          # haunch/body
        (E, (0, 0.18, base + 0.98), 0.48, (0.8, 0.8, 1.05), quat_from_axis((1, 0, 0), -0.35)),
        (B, (0, 0.3, base + 1.45), 0.3, (1, 1, 1), q0),                   # neck
        (B, (0, 0.38, base + 1.72), 0.27, (1, 1, 1), q0),
        (E, (0, 0.5, base + 1.95), 0.33, (0.9, 1.15, 0.85), q0),          # head
        (E, (0, 0.82, base + 1.9), 0.2, (0.9, 1.3, 0.8), q0),             # snout
    ]
    for s in (-1, 1):
        els += [
            (E, (s * 0.38, -0.12, base + 0.38), 0.36, (0.8, 1.2, 0.9), q0),  # hind legs
            (B, (s * 0.38, 0.35, base + 0.16), 0.14, (1, 1, 1), q0),      # hind paws
            (B, (s * 0.25, 0.42, base + 0.7), 0.15, (1, 1, 1), q0),       # forelegs
            (B, (s * 0.26, 0.5, base + 0.35), 0.14, (1, 1, 1), q0),
            (B, (s * 0.27, 0.58, base + 0.12), 0.14, (1, 1, 1), q0),
            (E, (s * 0.45, -0.25, base + 1.08), 0.5,                     # folded wings
             (0.22, 1.05, 0.8), quat_from_axis((0, 0, 1), s * 0.18)),
        ]
    for i in range(9):  # tail curling around the plinth top
        t = i / 8
        a = -math.pi / 2 - t * 2.3
        els.append((B, (0.62 * math.cos(a) + 0.05, 0.55 * math.sin(a) + 0.05, base + 0.1),
                    0.17 * (1 - t * 0.55), (1, 1, 1), q0))
    me = metaball_mesh(els, resolution=0.055, threshold=0.55, decimate=0.42)
    body = k.append_mesh(me, "statue_stone", smooth=True)
    bpy.data.meshes.remove(me)
    # Swept-back horns and spine ridge.
    for s in (-1, 1):
        body += k.cone((s * 0.17, 0.42, base + 2.12), 0.08, 0.015, 0.5, "statue_stone", seg=6,
                       rot=(-1.05, s * 0.35, 0))
    for i in range(5):
        body += k.cone((0, 0.12 - i * 0.16, base + 1.42 - i * 0.18), 0.07, 0.01, 0.2,
                       "statue_stone", seg=5, rot=(-0.6, 0, 0))
    # Softly glowing eyes (bioluminescent tile is in the emission map).
    for s in (-1, 1):
        k.blob((s * 0.17, 0.79, base + 2.0), (0.05, 0.035, 0.04), "glow_cap", subdiv=1, rough=0)
    k.moss_tops(body + faces, 0.72, from_zones={"statue_stone", "stone_warm", "stone_blocks"})


def build_tree(k):
    r = k.rng
    chains = [
        [((0, 0, -0.2), 0.95), ((0.08, 0, 1.1), 0.72), ((-0.1, 0.05, 2.3), 0.58), ((0, 0, 3.3), 0.52)],
        [((0, 0, 3.3), 0.5), ((0.9, 0.1, 4.1), 0.34), ((1.7, 0.2, 4.7), 0.22)],
        [((0, 0, 3.3), 0.5), ((-0.9, -0.1, 4.2), 0.34), ((-1.6, -0.2, 4.9), 0.22)],
        [((0, 0, 3.3), 0.48), ((0.1, -0.3, 4.5), 0.3), ((0.2, -0.4, 5.4), 0.2)],
    ]
    for a in (0.4, 1.9, 3.3, 4.6):  # buttress roots flaring into the ground
        chains.append([((0, 0, 0.5), 0.6), ((math.cos(a) * 0.9, math.sin(a) * 0.9, 0.12), 0.34),
                       ((math.cos(a) * 1.6, math.sin(a) * 1.6, -0.1), 0.14)])
    me = skin_mesh(chains, subdiv=1, decimate=0.5)
    trunk = k.append_mesh(me, "bark", smooth=True)
    bpy.data.meshes.remove(me)
    k.moss_tops(trunk, 0.55, from_zones={"bark"})
    crowns = [((1.7, 0.25, 5.2), (1.7, 1.5, 1.25)), ((-1.6, -0.15, 5.4), (1.65, 1.5, 1.3)),
              ((0.15, -0.35, 6.3), (2.1, 1.8, 1.45)), ((0.2, 0.7, 5.3), (1.4, 1.1, 1.1))]
    for c, rad in crowns:
        k.blob(c, rad, "leaf_canopy", subdiv=3, rough=0.22, freq=1.6)
    # Leaf cards breaking the canopy silhouette at its lower rim.
    for c, rad in crowns:
        for i in range(5):
            a = r.uniform(0, 2 * math.pi)
            p = Vector(c) + Vector((math.cos(a) * rad[0] * 0.85, math.sin(a) * rad[1] * 0.85,
                                    -rad[2] * r.uniform(0.2, 0.55)))
            k.card(p, 1.1, 1.0, "F_leaf_cluster", yaw=a + math.pi / 2, tilt=r.uniform(.3, .8),
                   pivot_bottom=False)


def build_roots(k):
    r = k.rng
    chains = []
    for i in range(5):
        a = i * 2 * math.pi / 5 + r.uniform(-.3, .3)
        L = r.uniform(1.4, 2.3)
        chain = []
        for j in range(6):
            t = j / 5
            arc = math.sin(t * math.pi) * 0.32 * (1 - t * 0.4)
            p = (math.cos(a + t * 0.5) * L * t, math.sin(a + t * 0.5) * L * t, arc - 0.06)
            chain.append((p, 0.24 * (1 - t) + 0.05))
        chains.append(chain)
    me = skin_mesh(chains, subdiv=1, decimate=0.6)
    faces = k.append_mesh(me, "root", smooth=True)
    bpy.data.meshes.remove(me)
    k.moss_tops(faces, 0.7, density=0.7)


def build_vine(k):
    r = k.rng
    pts = []
    for j in range(8):
        t = j / 7
        pts.append(((math.sin(t * 5) * 0.12, 0.05, -t * 2.2), 0.045 * (1 - t * 0.5)))
    me = skin_mesh([pts], subdiv=0)
    k.append_mesh(me, "root", smooth=True)
    bpy.data.meshes.remove(me)
    k.card((0, 0.08, 0), 0.6, 2.3, "F_ivy_strand", yaw=0.05, pivot_bottom=False)
    k.card((0.05, 0.02, -0.15), 0.55, 2.0, "F_ivy_strand", yaw=math.pi / 2 + 0.2, pivot_bottom=False)
    for j in range(3):
        k.card((r.uniform(-.2, .2), 0.12, -0.5 - j * 0.6), 0.55, 0.5, "F_leaf_cluster",
               yaw=r.uniform(-.4, .4), tilt=0.3, pivot_bottom=False)


def build_rock(k, size, subdiv, pieces=1):
    faces = []
    r = k.rng
    for i in range(pieces):
        s = size if i == 0 else size * r.uniform(0.35, 0.55)
        c = (0, 0, 0) if i == 0 else (r.uniform(-1, 1) * size * 0.9, r.uniform(-1, 1) * size * 0.9, 0)
        rad = (s * r.uniform(0.9, 1.15), s * r.uniform(0.75, 0.95), s * r.uniform(0.6, 0.75))
        f = k.blob((c[0], c[1], rad[2] * 0.55), rad, "stone_cool", subdiv=subdiv, rough=0.24,
                   freq=1.1, flat_bottom=-0.55, rot_z=r.uniform(0, 3))
        faces += f
    k.moss_tops(faces, 0.38)


def build_grass(k, cell):
    k.cross_cards((0, 0, 0), 0.62, 0.55, cell, count=3, yaw=k.rng.uniform(0, 1))


def build_flowers(k, cell):
    k.cross_cards((0, 0, 0), 0.5, 0.62, cell, count=2, yaw=0.3)
    k.card((0.12, -0.08, 0), 0.5, 0.42, "F_grass_b", yaw=1.2, tilt=0.15)


def build_fern(k):
    for i in range(6):
        a = i * math.pi / 3 + k.rng.uniform(-.2, .2)
        k.card((math.cos(a) * 0.08, math.sin(a) * 0.08, 0), 0.42, 0.95, "F_fern",
               yaw=a - math.pi / 2, tilt=0.85 + k.rng.uniform(-.15, .1))


def build_mushrooms(k, glow):
    r = k.rng
    cap = "glow_cap" if glow else "mushroom_cap"
    spots = [(0, 0, 0.32, 0.17), (0.2, 0.1, 0.22, 0.12), (-0.16, 0.14, 0.17, 0.1), (0.05, -0.2, 0.12, 0.07)]
    for x, y, h, cr in spots:
        k.cone((x, y, 0), cr * 0.42, cr * 0.32, h, "mushroom_stem", seg=7, caps=False,
               rot=(r.uniform(-.12, .12), r.uniform(-.12, .12), 0))
        n0 = len(k.bm.faces)
        bmesh.ops.create_uvsphere(k.bm, u_segments=10, v_segments=6, radius=1.0,
                                  matrix=Matrix.Translation((x, y, h)) @ Matrix.Diagonal((cr, cr, cr * 0.62, 1)))
        faces = k._finish(k._new_faces(n0), cap, True)
        k.bm.normal_update()
        k.zone_faces_where(faces, lambda f: f.normal.z < -0.2, "mushroom_stem")


def build_pond(k, rx=1.75, ry=1.2, rim=15, lilies=((0.5, 0.2, 0.22), (-0.6, -0.25, 0.18), (0.1, -0.45, 0.15)),
               reeds=4):
    r = k.rng
    k.water_radii = (rx, ry)
    # Earth basin (seen through the transparent water).
    n0 = len(k.bm.faces)
    bmesh.ops.create_circle(k.bm, cap_ends=True, segments=20, radius=1.0,
                            matrix=Matrix.Translation((0, 0, -0.12)) @ Matrix.Diagonal((rx * 1.05, ry * 1.05, 1, 1)))
    k._finish(k._new_faces(n0), "earth", False)
    # Water: centre fan + an inner ring so vertex alpha fades to the shore.
    seg = 24
    centre = k.bm.verts.new((0, 0, 0.04))
    inner = [k.bm.verts.new((math.cos(a) * rx * 0.62, math.sin(a) * ry * 0.62, 0.04))
             for a in (i * 2 * math.pi / seg for i in range(seg))]
    outer = [k.bm.verts.new((math.cos(a) * rx * 1.02, math.sin(a) * ry * 1.02, 0.04))
             for a in (i * 2 * math.pi / seg for i in range(seg))]
    n0 = len(k.bm.faces)
    for i in range(seg):
        j = (i + 1) % seg
        k.bm.faces.new((centre, inner[i], inner[j]))
        k.bm.faces.new((inner[i], outer[i], outer[j], inner[j]))
    k._finish(k._new_faces(n0), "WATER", True)
    # Rim stones.
    rim_faces = []
    for i in range(rim):
        a = i * 2 * math.pi / rim + r.uniform(-.1, .1)
        sz = r.uniform(0.24, 0.42)
        rim_faces += k.blob((math.cos(a) * rx * 1.08, math.sin(a) * ry * 1.08, 0.02),
                            (sz * 1.2, sz, sz * 0.6), "stone_cool", subdiv=1, rough=0.3, flat_bottom=-0.5,
                            smooth=False, rot_z=a)
    k.moss_tops(rim_faces, 0.55, density=0.8)
    for x, y, sz in lilies:
        n0 = len(k.bm.faces)
        bmesh.ops.create_circle(k.bm, cap_ends=True, segments=9, radius=sz,
                                matrix=Matrix.Translation((x, y, 0.06)))
        k._finish(k._new_faces(n0), "lily", False)
    for i in range(reeds):
        a = r.uniform(0, 2 * math.pi)
        k.cross_cards((math.cos(a) * rx * 1.1, math.sin(a) * ry * 1.1, 0), 0.5, 0.6, "F_grass_b", count=2)


def build_pond_bank(k):
    """Larger pond for origin_pondbank (radii match layout.POND_RADII, Unity x/z)."""
    rx, rz = layout_mod.POND_RADII
    build_pond(k, rx=rx, ry=rz, rim=11,
               lilies=((0.7, 0.3, 0.25), (-0.8, -0.35, 0.2), (0.2, -0.6, 0.17), (-0.3, 0.55, 0.15)), reeds=0)


def build_stepping_stones(k):
    faces = []
    for i, (x, y) in enumerate(((0, 0), (0.55, 0.35), (1.15, 0.5), (-0.6, -0.3))):
        faces += k.cone((x, y, -0.04), 0.3 + 0.04 * (i % 2), 0.27, 0.16, "stone_cool", seg=8,
                        rot=(0, 0, k.rng.uniform(0, 3)), smooth=False)
    k.moss_tops(faces, 0.6, density=0.5)


def build_tree_b(k):
    """Leaning, slimmer variant with a layered three-crown canopy."""
    r = k.rng
    chains = [
        [((0, 0, -0.2), 0.7), ((0.25, 0.05, 1.2), 0.5), ((0.55, 0.0, 2.4), 0.4), ((0.6, -0.1, 3.2), 0.36)],
        [((0.6, -0.1, 3.2), 0.34), ((1.5, 0.0, 3.9), 0.22)],
        [((0.6, -0.1, 3.2), 0.34), ((-0.4, -0.2, 4.1), 0.22)],
    ]
    for a in (0.9, 2.6, 4.3):
        chains.append([((0, 0, 0.4), 0.45), ((math.cos(a) * 1.1, math.sin(a) * 1.1, -0.1), 0.12)])
    me = skin_mesh(chains, subdiv=1, decimate=0.5)
    trunk = k.append_mesh(me, "bark", smooth=True)
    bpy.data.meshes.remove(me)
    k.moss_tops(trunk, 0.6, from_zones={"bark"})
    crowns = [((1.6, 0.0, 4.3), (1.4, 1.2, 1.0)), ((-0.5, -0.2, 4.6), (1.5, 1.3, 1.1)),
              ((0.6, -0.1, 5.4), (1.6, 1.4, 1.1))]
    for c, rad in crowns:
        k.blob(c, rad, "leaf_canopy", subdiv=3, rough=0.25, freq=1.8)
        for _ in range(4):
            a = r.uniform(0, 2 * math.pi)
            p = Vector(c) + Vector((math.cos(a) * rad[0] * 0.85, math.sin(a) * rad[1] * 0.85,
                                    -rad[2] * r.uniform(0.2, 0.5)))
            k.card(p, 1.0, 0.9, "F_leaf_cluster", yaw=a + math.pi / 2, tilt=r.uniform(.3, .8),
                   pivot_bottom=False)


def build_rock_warm_flat(k):
    """Sun-warmed basking slab with a flat top (perch surface ~0.44 m)."""
    faces = k.blob((0, 0, 0.12), (1.05, 0.75, 0.4), "stone_warm", subdiv=3, rough=0.18, freq=0.9,
                   flat_bottom=-0.3)
    for v in {v for f in faces for v in f.verts}:
        v.co.z = min(v.co.z, 0.44 + 0.02 * math.sin(v.co.x * 5))
    for i in range(3):
        a = k.rng.uniform(0, 6.28)
        faces += k.blob((math.cos(a) * 1.15, math.sin(a) * 0.85, 0.05), (0.2, 0.17, 0.12), "stone_warm",
                        subdiv=1, rough=0.3, flat_bottom=-0.4, smooth=False)
    k.moss_tops(faces, 0.9, density=0.15)


def build_rock_warm_stack(k):
    faces = k.blob((0, 0, 0.3), (0.85, 0.7, 0.42), "stone_warm", subdiv=2, rough=0.2, flat_bottom=-0.6)
    faces += k.blob((0.1, -0.05, 0.85), (0.6, 0.5, 0.3), "stone_warm", subdiv=2, rough=0.22, rot_z=0.6)
    for v in {v for f in faces for v in f.verts}:
        if v.co.z > 1.05:
            v.co.z = 1.05 + (v.co.z - 1.05) * 0.3
    faces += k.blob((-0.7, 0.35, 0.12), (0.3, 0.25, 0.2), "stone_cool", subdiv=1, rough=0.3,
                    flat_bottom=-0.5, smooth=False)
    k.moss_tops(faces, 0.85, density=0.2)


def build_shrub(k):
    for c, rad in (((0, 0, 0.45), (0.7, 0.6, 0.5)), ((0.45, 0.2, 0.35), (0.45, 0.4, 0.38)),
                   ((-0.4, -0.15, 0.32), (0.45, 0.4, 0.34))):
        k.blob(c, rad, "leaf_canopy", subdiv=2, rough=0.25, freq=2.0, flat_bottom=-0.4)
    for i in range(6):
        a = i * math.pi / 3 + k.rng.uniform(-.2, .2)
        k.card((math.cos(a) * 0.5, math.sin(a) * 0.45, 0), 0.8, 0.8, "F_shrub_leaf", yaw=a + math.pi / 2,
               tilt=0.35)


# Card recipes for baked ground cover: cell -> (cards, width, height, tilt)
COVER_CARDS = {
    "grass_a": (3, 0.6, 0.55, 0.12), "grass_b": (3, 0.6, 0.55, 0.12), "meadow_tall": (2, 0.5, 0.85, 0.1),
    "flower_pink": (2, 0.5, 0.62, 0.1), "flower_blue": (2, 0.5, 0.62, 0.1), "flower_gold": (2, 0.5, 0.62, 0.1),
    "flower_lilac": (2, 0.5, 0.62, 0.1), "clover": (2, 0.5, 0.32, 0.5), "reed": (2, 0.45, 1.1, 0.05),
    "cattail": (2, 0.42, 1.25, 0.05), "dry_grass": (3, 0.6, 0.5, 0.15), "twig_dry": (2, 0.5, 0.6, 0.1),
    "shrub_leaf": (3, 0.9, 0.9, 0.2),
}


def build_cover(k, chunk):
    """Bake a habitat's grass/flower scatter into ONE mesh (one renderer, one draw)."""
    pts = layout_mod.scatter_cover(layout_mod.build_layout()["placements"])[chunk]
    for cell, x, z, yaw, sc in pts:
        bx, by = -x, -z
        a = -math.radians(yaw)
        if cell == "fern":
            for i in range(4):
                b = a + i * math.pi / 2
                k.card((bx + math.cos(b) * 0.06, by + math.sin(b) * 0.06, 0), 0.38 * sc, 0.85 * sc, "F_fern",
                       yaw=b - math.pi / 2, tilt=0.85)
            continue
        n, w, h, tilt = COVER_CARDS[cell]
        k.cross_cards((bx, by, 0), w * sc, h * sc, "F_" + cell, count=n, yaw=a, tilt=tilt)


def runestone_glyph_front(k, faces):
    k.bm.normal_update()
    fronts = [f for f in faces if f.normal.dot(FRONT) > 0.85]
    if not fronts:
        return
    big = max(fronts, key=lambda f: f.calc_area())
    res = bmesh.ops.inset_individual(k.bm, faces=[big], thickness=0.09, depth=-0.025)
    big.material_index = Z["rune_glyph"]
    for f in res.get("faces", []):
        f.material_index = Z["rune_slate"]


def build_runestone_a(k):
    faces = k.box((0, 0, 0.8), (0.72, 0.36, 1.7), "rune_slate", bevel=0.05)
    for v in {v for f in faces for v in f.verts}:
        t = max(0.0, v.co.z) / 1.7
        v.co.x *= 1 - 0.28 * t
        v.co.y += 0.06 * t
    faces += k.box((0, 0, 0.06), (0.95, 0.6, 0.16), "stone_cool", bevel=0.03)
    runestone_glyph_front(k, faces)
    k.moss_tops(faces, 0.7)


def build_runestone_b(k):
    faces = k.cone((0, -0.16, 0.85), 0.6, 0.6, 0.32, "rune_slate", seg=12, rot=(-math.pi / 2, 0, 0),
                   smooth=False)
    faces += k.box((0, 0, 0.42), (1.05, 0.32, 0.85), "rune_slate", bevel=0.04)
    faces += k.box((0, 0, 0.06), (1.3, 0.62, 0.16), "stone_cool", bevel=0.03)
    for v in {v for f in faces for v in f.verts}:
        v.co.y += 0.04 * max(0.0, v.co.z)
    runestone_glyph_front(k, faces)
    k.moss_tops(faces, 0.65)


def build_ground(k):
    """Ground sheet in Blender space covering every landscape aspect. Flat (y = 0) inside
    layout.FLAT_RECT so gameplay positions need no height lookup. Vertex A = path mask."""
    ux0, ux1, uz0, uz1 = -16.0, 20.0, -11.0, 28.0
    nx, nz = 36, 30
    fx0, fz0, fx1, fz1 = layout_mod.FLAT_RECT
    grid = {}
    for i in range(nx + 1):
        for j in range(nz + 1):
            ux = ux0 + (ux1 - ux0) * i / nx
            uz = uz0 + (uz1 - uz0) * j / nz
            h = noise.noise(Vector((ux * 0.12, uz * 0.12, 3.1))) * 0.22
            out = max(fx0 - ux, ux - fx1, fz0 - uz, uz - fz1, 0.0)
            h *= min(1.0, out / 2.5)
            grid[i, j] = k.bm.verts.new((-ux, -uz, h))
    n0 = len(k.bm.faces)
    for i in range(nx):
        for j in range(nz):
            k.bm.faces.new((grid[i, j], grid[i, j + 1], grid[i + 1, j + 1], grid[i + 1, j]))
    faces = k._finish(k._new_faces(n0), "GROUND", True)
    bmesh.ops.recalc_face_normals(k.bm, faces=faces)


ASSETS = {
    "SM_ENV_Gate_Ancient": build_gate,
    "SM_ENV_Column_Intact": build_column_intact,
    "SM_ENV_Column_Broken": build_column_broken,
    "SM_ENV_Column_Fallen": build_column_fallen,
    "SM_ENV_Wall_Ruin_A": build_wall_a,
    "SM_ENV_Wall_Ruin_B": build_wall_b,
    "SM_ENV_Statue_Dragon": build_statue,
    "SM_ENV_Tree_Ancient": build_tree,
    "SM_ENV_Root_Cluster": build_roots,
    "SM_ENV_Vine_Hanging": build_vine,
    "SM_ENV_Rock_Mossy_A": lambda k: build_rock(k, 0.95, 3),
    "SM_ENV_Rock_Mossy_B": lambda k: build_rock(k, 0.62, 2, pieces=2),
    "SM_ENV_Rock_Mossy_C": lambda k: build_rock(k, 0.34, 2, pieces=3),
    "SM_ENV_Grass_Tuft_A": lambda k: build_grass(k, "F_grass_a"),
    "SM_ENV_Grass_Tuft_B": lambda k: build_grass(k, "F_grass_b"),
    "SM_ENV_Flower_Patch_Pink": lambda k: build_flowers(k, "F_flower_pink"),
    "SM_ENV_Flower_Patch_Blue": lambda k: build_flowers(k, "F_flower_blue"),
    "SM_ENV_Flower_Patch_Gold": lambda k: build_flowers(k, "F_flower_gold"),
    "SM_ENV_Fern": build_fern,
    "SM_ENV_Mushroom_Cluster_Red": lambda k: build_mushrooms(k, False),
    "SM_ENV_Mushroom_Cluster_Glow": lambda k: build_mushrooms(k, True),
    "SM_ENV_Pond_Small": build_pond,
    "SM_ENV_Runestone_A": build_runestone_a,
    "SM_ENV_Runestone_B": build_runestone_b,
    "SM_ENV_Ground_Sanctuary": build_ground,
    # U1-ENV.2 variants and habitat pieces.
    "SM_ENV_Tree_Ancient_B": build_tree_b,
    "SM_ENV_Rock_Warm_Flat": build_rock_warm_flat,
    "SM_ENV_Rock_Warm_Stack": build_rock_warm_stack,
    "SM_ENV_Shrub_Round": build_shrub,
    "SM_ENV_Pond_Bank": build_pond_bank,
    "SM_ENV_Stepping_Stones": build_stepping_stones,
    "SM_ENV_Cover_Origin_Meadow": lambda k: build_cover(k, "origin_meadow"),
    "SM_ENV_Cover_Origin_Pondbank": lambda k: build_cover(k, "origin_pondbank"),
    "SM_ENV_Cover_Origin_Warm_Rocks": lambda k: build_cover(k, "origin_warm_rocks"),
    "SM_ENV_Cover_Edges": lambda k: build_cover(k, "edges"),
}

# Vertex-colour tint multiplied into the painted AO (warm, sun-baked stone).
TINT = {"SM_ENV_Rock_Warm_Flat": (1.0, 0.84, 0.68), "SM_ENV_Rock_Warm_Stack": (1.0, 0.86, 0.7)}

# Triangle budgets (mobile). The build fails loudly if an asset exceeds its budget.
BUDGET = {
    "SM_ENV_Gate_Ancient": 2600, "SM_ENV_Statue_Dragon": 3500, "SM_ENV_Tree_Ancient": 3600,
    "SM_ENV_Ground_Sanctuary": 2600, "SM_ENV_Pond_Small": 2000, "SM_ENV_Pond_Bank": 2000,
    "SM_ENV_Tree_Ancient_B": 3000, "SM_ENV_Cover_Origin_Meadow": 3000, "SM_ENV_Cover_Origin_Pondbank": 3000,
    "SM_ENV_Cover_Origin_Warm_Rocks": 3000, "SM_ENV_Cover_Edges": 3000,
}
DEFAULT_BUDGET = 1500


def _ground_shade():
    """(x, z, radius) soft shade under every collider footprint (Unity space)."""
    out = []
    for o in layout_mod.obstacle_polygons(layout_mod.build_layout()["placements"]):
        xs = [p[0] for p in o["polygon"]]
        zs = [p[1] for p in o["polygon"]]
        cx, cz = sum(xs) / len(xs), sum(zs) / len(zs)
        out.append((cx, cz, max(math.hypot(x - cx, z - cz) for x, z in o["polygon"]) * 0.8))
    return out


GROUND_SHADE = _ground_shade()


# ================================================================ UV + COLOUR
def dominant_axis(n):
    a = (abs(n.x), abs(n.y), abs(n.z))
    return a.index(max(a))


def project(co, axis, normal):
    """Planar projection that reads un-mirrored when viewed from the face side."""
    if axis == 0:
        return (co.y if normal.x > 0 else -co.y), co.z
    if axis == 1:
        return (-co.x if normal.y > 0 else co.x), co.z
    return co.x, co.y


def unwrap(k):
    bm, uvl, rng = k.bm, k.uv, random.Random(k.seed * 7 + 1)
    bm.faces.ensure_lookup_table()
    bm.normal_update()
    faces_by_zone = {}
    for f in bm.faces:
        faces_by_zone.setdefault(ZONES[f.material_index], []).append(f)
    for zone, faces in faces_by_zone.items():
        if zone.startswith("F_"):
            u0, v0, u1, v1 = atlas.foliage_uv_rect(zone[2:])
            for f in faces:
                for l in f.loops:
                    u, v = l[uvl].uv
                    l[uvl].uv = (u0 + (u1 - u0) * u, v0 + (v1 - v0) * v)
            continue
        if zone in ("WATER", "GROUND"):
            scale = 0.4 if zone == "WATER" else 0.2
            for f in faces:
                for l in f.loops:
                    l[uvl].uv = (l.vert.co.x * scale, l.vert.co.y * scale)
            continue
        # Props atlas: box-projected islands, uniformly scaled into the tile.
        u0, v0, u1, v1 = atlas.tile_uv_rect(zone)
        world = TILE_WORLD[zone]
        fset = set(faces)
        axis = {f: dominant_axis(f.normal) for f in faces}
        seen = set()
        for f in faces:
            if f in seen:
                continue
            island, stack = [], [f]
            seen.add(f)
            while stack:
                cur = stack.pop()
                island.append(cur)
                for e in cur.edges:
                    for nb in e.link_faces:
                        if nb in fset and nb not in seen and axis[nb] == axis[cur]:
                            seen.add(nb)
                            stack.append(nb)
            pts = {}
            for face in island:
                for l in face.loops:
                    a, b = project(l.vert.co, axis[face], face.normal)
                    pts[l] = (a / world, b / world)
            us = [p[0] for p in pts.values()]
            vs = [p[1] for p in pts.values()]
            mu, mv = min(us), min(vs)
            w, h = max(us) - mu, max(vs) - mv
            s = 1.0 / max(1.0, w, h)
            if zone == "rune_glyph":
                s = 1.0 / max(w * 2.0, h, 1e-6)  # one glyph column, full height
                ou = rng.choice((0.25, 0.75)) - w * s / 2
                ov = (1 - h * s) / 2
            else:
                ou = rng.uniform(0, max(0.0, 1 - w * s))
                ov = rng.uniform(0, max(0.0, 1 - h * s))
            for l, (a, b) in pts.items():
                uu = ou + (a - mu) * s
                vv = ov + (b - mv) * s
                l[uvl].uv = (u0 + (u1 - u0) * uu, v0 + (v1 - v0) * vv)


def to_object(k):
    unwrap(k)
    me = bpy.data.meshes.new(k.name)
    k.bm.to_mesh(me)
    k.bm.free()
    obj = bpy.data.objects.new(k.name, me)
    bpy.context.scene.collection.objects.link(obj)

    zmin = min((v.co.z for v in me.vertices), default=0)
    zmax = max((v.co.z for v in me.vertices), default=1)
    height = max(zmax - zmin, 0.01)
    is_ground = k.name == "SM_ENV_Ground_Sanctuary"
    is_vine = k.name == "SM_ENV_Vine_Hanging"
    zones = [ZONES[p.material_index] for p in me.polygons]

    # Vertex colours: painted AO gradient (RGB) + wind / path mask (A).
    col = me.color_attributes.new("Color", 'BYTE_COLOR', 'CORNER')
    data = [0.0] * (len(me.loops) * 4)
    for p, zone in zip(me.polygons, zones):
        for li in p.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            t = (co.z - zmin) / height
            if is_ground:
                ux, uz = -co.x, -co.y
                d = layout_mod.path_distance(ux, uz)
                edge = noise.noise(Vector((ux * 0.9, uz * 0.9, 1.7))) * 0.25
                path = 1.0 - min(1.0, max(0.0, (d - layout_mod.PATH_WIDTH * 0.5 + edge) / 0.45))
                cl = math.hypot(ux - layout_mod.CLEARING[0], uz - layout_mod.CLEARING[1])
                path = max(path, 0.35 * (1 - min(1.0, max(0.0, (cl - 1.2) / 1.2))))
                shade = 1.0
                for bx, bz, br in GROUND_SHADE:
                    dd = math.hypot(ux - bx, uz - bz)
                    if dd < br * 1.6:
                        shade = min(shade, 0.72 + 0.28 * min(1.0, max(0.0, (dd - br * 0.6) / (br * 0.9))))
                rgb = (shade, shade, shade * 0.97)
                a = path
            elif zone.startswith("F_"):
                g = 0.62 + 0.38 * (t if not is_vine else 1 - t)
                rgb = (g, g, g)
                a = (1 - t) if is_vine else min(1.0, max(0.0, (co.z - zmin) / 0.6)) ** 1.4
            elif zone == "WATER":
                wr = getattr(k, "water_radii", (1.75, 1.2))
                rad = math.hypot(co.x / wr[0], co.y / wr[1])
                rgb = (1, 1, 1)
                a = min(1.0, max(0.0, (1.0 - rad) / 0.38))
            else:
                ao = 0.58 + 0.42 * min(1.0, (co.z - zmin + 0.05) / min(0.9, height * 0.6))
                if zone in ("leaf_canopy",):
                    ao = 0.68 + 0.32 * t
                tint = TINT.get(k.name, (1.0, 1.0, 1.0))
                rgb = (ao * tint[0], ao * tint[1], ao * 0.98 * tint[2])
                a = 0.18 if zone == "leaf_canopy" else (0.7 * (1 - t) if is_vine else 0.0)
            data[li * 4:li * 4 + 4] = (*rgb, a)
    col.data.foreach_set("color", data)
    me.color_attributes.active_color = col

    # Foliage cards get up-facing normals so they shade like the ground (no dark backs).
    normals = []
    for p, zone in zip(me.polygons, zones):
        for li in p.loop_indices:
            if zone.startswith("F_"):
                normals.append(Vector((p.normal.x * 0.25, p.normal.y * 0.25, 1)).normalized())
            else:
                normals.append(None)
    if any(n is not None for n in normals):
        corner = [Vector(cn.vector) for cn in me.corner_normals]
        me.normals_split_custom_set([tuple(n if n is not None else c) for n, c in zip(normals, corner)])

    # Collapse zones into the four final material slots.
    used = [s for s in FINAL_SLOTS if any(final_slot(z) == s for z in zones)]
    idx = {s: i for i, s in enumerate(used)}
    me.polygons.foreach_set("material_index", [idx[final_slot(z)] for z in zones])
    for s in used:
        me.materials.append(MATERIALS[s])
    me.update()
    return obj


# ============================================================== MATERIALS
MATERIALS = {}


def make_materials(tex_dir):
    def img(name, non_color=False):
        im = bpy.data.images.load(str(tex_dir / name), check_existing=True)
        if non_color:
            im.colorspace_settings.name = 'Non-Color'
        return im

    def principled(name, image, alpha=False, emission=None, rough=0.9, water=False):
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
        mix.inputs["Factor"].default_value = 0.0 if (water or name == "ENV_Ground") else 1.0
        nt.links.new(tex.outputs["Color"], mix.inputs["A"])
        nt.links.new(vc.outputs["Color"], mix.inputs["B"])
        nt.links.new(mix.outputs["Result"], bsdf.inputs["Base Color"])
        if alpha:
            nt.links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
        if water:
            bsdf.inputs["Alpha"].default_value = 0.78
        if emission is not None:
            et = nt.nodes.new("ShaderNodeTexImage")
            et.image = emission
            nt.links.new(et.outputs["Color"], bsdf.inputs["Emission Color"])
            bsdf.inputs["Emission Strength"].default_value = 2.5
        m.diffuse_color = (1, 1, 1, 1)
        return m

    MATERIALS["ENV_Props"] = principled("ENV_Props", img("T_ENV_PropsAtlas.png"),
                                        emission=img("T_ENV_PropsEmission.png"))
    MATERIALS["ENV_Foliage"] = principled("ENV_Foliage", img("T_ENV_Foliage.png"), alpha=True)
    MATERIALS["ENV_Water"] = principled("ENV_Water", img("T_ENV_Water.png"), rough=0.15, water=True)
    MATERIALS["ENV_Ground"] = principled("ENV_Ground", img("T_ENV_Ground.png"))


# ================================================================== EXPORT
def export_fbx(obj, path):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(
        filepath=str(path), use_selection=True, object_types={'MESH'},
        axis_forward='-Z', axis_up='Y', apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True,
        mesh_smooth_type='FACE', use_mesh_modifiers=True, use_tspace=False,
        colors_type='SRGB', prioritize_active_color=True, add_leaf_bones=False,
        bake_anim=False, path_mode='STRIP', embed_textures=False,
        use_custom_props=False)


def stats(obj):
    me = obj.data
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    bb = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    return {
        "triangles": tris, "vertices": len(me.vertices),
        "materials": [m.name for m in me.materials],
        "size_m": [round(max(c[i] for c in bb) - min(c[i] for c in bb), 2) for i in range(3)],
    }


def validate_uvs(obj):
    """Every atlas UV must stay inside its tile (no bleeding / wrapping)."""
    me = obj.data
    uv = me.uv_layers["UVMap"].data
    errors = 0
    for p in me.polygons:
        slot = me.materials[p.material_index].name
        if slot not in ("ENV_Props", "ENV_Foliage"):
            continue
        us = [uv[li].uv[0] for li in p.loop_indices]
        vs = [uv[li].uv[1] for li in p.loop_indices]
        if min(us) < -1e-4 or max(us) > 1 + 1e-4 or min(vs) < -1e-4 or max(vs) > 1 + 1e-4:
            errors += 1
            continue
        if slot == "ENV_Props":
            g = atlas.PROPS_GRID
            cu = {min(int(u * g), g - 1) for u in us}
            cv = {min(int(v * g), g - 1) for v in vs}
            if len(cu) > 1 or len(cv) > 1:
                errors += 1
    return errors


# ================================================================== PREVIEW
def render_preview(objects, layout, path):
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
        inst.location = (-pl["x"], -pl["z"], pl["y"])
        inst.rotation_euler = (0, 0, -math.radians(pl["yaw"]))
        inst.scale = (pl["scale"],) * 3
        coll.objects.link(inst)
    cd = bpy.data.cameras.new("Cam")
    cd.type = 'ORTHO'
    cd.clip_end = 200
    cam = bpy.data.objects.new("Cam", cd)
    # Same fixed landscape camera as Unity, including the right-hand UI offset.
    px, py, pz = layout_mod.camera_position()
    cam.location = (-px, -pz, py)
    p = math.radians(layout_mod.PITCH_DEG)
    cam.rotation_euler = Vector((0.0, -math.cos(p), -math.sin(p))).to_track_quat('-Z', 'Y').to_euler()
    scn.collection.objects.link(cam)
    scn.camera = cam
    sun_d = bpy.data.lights.new("Sun", 'SUN')
    sun_d.energy = 3.2
    sun_d.color = (1.0, 0.92, 0.78)
    sun_d.angle = math.radians(8)
    sun = bpy.data.objects.new("Sun", sun_d)
    sun.rotation_euler = (math.radians(40), math.radians(18), math.radians(-35))
    scn.collection.objects.link(sun)
    world = bpy.data.worlds.new("Sky")
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    bg.inputs["Color"].default_value = (0.62, 0.72, 0.58, 1)
    bg.inputs["Strength"].default_value = 0.9
    scn.world = world
    scn.render.engine = 'CYCLES'
    scn.cycles.device = 'CPU'
    scn.cycles.samples = 24
    scn.cycles.use_denoising = True
    scn.cycles.max_bounces = 3
    scn.render.film_transparent = False
    scn.view_settings.view_transform = 'Standard'
    outputs = []
    for label, aspect in (("16x9", 16 / 9), ("20x9", 20 / 9)):
        h = 720
        w = int(round(h * aspect))
        scn.render.resolution_x, scn.render.resolution_y = w, h
        cd.ortho_scale = 2 * layout_mod.ORTHO_SIZE * aspect
        out = Path(str(path).replace(".png", f"_{label}.png"))
        scn.render.filepath = str(out)
        bpy.ops.render.render(write_still=True)
        outputs.append(out)

    # U1-ENV.3: the same camera at dusk and at night, lit from Data/OriginSanctuaryLiving.json.
    h = 720
    scn.render.resolution_x, scn.render.resolution_y = int(round(h * 16 / 9)), h
    cd.ortho_scale = 2 * layout_mod.ORTHO_SIZE * 16 / 9
    props = MATERIALS.get("ENV_Props")
    bsdf = props.node_tree.nodes.get("Principled BSDF") if props else None
    flies = bpy.data.collections.new("PreviewFireflies")
    scn.collection.children.link(flies)
    fly_mat = bpy.data.materials.new("Firefly")
    fly_mat.use_nodes = True
    fb = fly_mat.node_tree.nodes.get("Principled BSDF")
    fb.inputs["Base Color"].default_value = (0.9, 1.0, 0.4, 1)
    fb.inputs["Emission Color"].default_value = (0.9, 1.0, 0.4, 1)
    fb.inputs["Emission Strength"].default_value = 14.0
    fly_mesh = bpy.data.meshes.new("FireflyMesh")
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=1, radius=0.045)
    bm.to_mesh(fly_mesh)
    bm.free()
    fly_mesh.materials.append(fly_mat)
    rng = random.Random(911)
    for label, hour, exposure in (("dusk", 18.3, 0.0), ("night", 22.0, -0.9)):
        st = living.evaluate(hour)
        f = living.light_forward(st["lightElevation"], st["lightAzimuth"])
        sun.rotation_euler = Vector((-f[0], -f[2], f[1])).to_track_quat('-Z', 'Y').to_euler()
        sun_d.color = st["lightColor"]
        sun_d.energy = 3.2 * st["lightIntensity"] / 1.35
        bg.inputs["Color"].default_value = tuple(st["ambientEquator"]) + (1,)
        bg.inputs["Strength"].default_value = 0.9 if exposure == 0.0 else 0.7
        scn.view_settings.exposure = exposure  # night reads darker on screen, as in Unity
        if bsdf is not None:
            bsdf.inputs["Emission Strength"].default_value = 2.5 * (1 + 1.6 * st["nightGlow"])
        for o in list(flies.objects):
            bpy.data.objects.remove(o)
        for vol_id, count in (("fireflies", 36), ("fireflies_pond", 14)):
            v = living.PARTICLES[vol_id]
            for _ in range(int(round(count * st["fireflies"]))):
                ux = v["center"][0] + (rng.random() - 0.5) * v["size"][0]
                uy = v["center"][1] + (rng.random() - 0.5) * v["size"][1]
                uz = v["center"][2] + (rng.random() - 0.5) * v["size"][2]
                fo = bpy.data.objects.new("Firefly", fly_mesh)
                fo.location = (-ux, -uz, uy)
                flies.objects.link(fo)
        out = Path(str(path).replace(".png", f"_{label}_16x9.png"))
        scn.render.filepath = str(out)
        bpy.ops.render.render(write_still=True)
        outputs.append(out)
    scn.view_settings.exposure = 0.0
    return outputs


# ==================================================================== MAIN
def main():
    OUT.mkdir(parents=True, exist_ok=True)
    tex_dir = OUT / "Textures"
    painterly_textures.generate_all(str(tex_dir))
    layout = layout_mod.write(str(OUT / "OriginSanctuaryLayout.json"), str(OUT / "OriginSanctuaryLocations.json"))
    layout_mod.write_colliders(str(OUT / "OriginSanctuaryColliders.json"))
    # U1-ENV.3 living environment: data, particle sprites and synthesised ambience.
    living.write(str(OUT / "OriginSanctuaryLiving.json"))
    fx_textures.generate_all(str(tex_dir))
    audio_dir = OUT / "Audio"
    ambience_audio.generate_all(str(audio_dir))

    # Empty scene (never run inside an artist's unsaved file).
    bpy.ops.wm.read_factory_settings(use_empty=True)
    make_materials(tex_dir)
    fbx_dir = OUT / "Models"
    fbx_dir.mkdir(exist_ok=True)

    manifest = {"generator": "scripts/art/environment/build_origin_sanctuary.py",
                "blender": bpy.app.version_string, "assets": {}}
    objects, failures = {}, []
    for i, (name, fn) in enumerate(ASSETS.items()):
        k = Kit(name, seed=1000 + i * 37)
        fn(k)
        obj = to_object(k)
        objects[name] = obj
        st = stats(obj)
        st["uv_errors"] = validate_uvs(obj)
        budget = BUDGET.get(name, DEFAULT_BUDGET)
        st["budget"] = budget
        manifest["assets"][name] = st
        if st["triangles"] > budget:
            failures.append(f"{name}: {st['triangles']} tris > budget {budget}")
        if st["uv_errors"]:
            failures.append(f"{name}: {st['uv_errors']} faces with UVs outside their atlas tile")
        if not ONLY or name in ONLY:
            export_fbx(obj, fbx_dir / f"{name}.fbx")
        print(f"[env] {name:34s} {st['triangles']:5d} tris  {st['materials']}")

    placements = layout["placements"]
    total = sum(manifest["assets"][p["asset"]]["triangles"] for p in placements)
    manifest["layout"] = {"placements": len(placements), "scene_triangles": total}
    with open(OUT / "manifest.json", "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=1)
    print(f"[env] layout: {len(placements)} placements, ~{total} triangles")

    # Editable source with all assets side by side.
    for i, o in enumerate(objects.values()):
        o.location.x = (i % 6) * 6.0
        o.location.y = (i // 6) * 6.0
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT / "OriginSanctuaryKit.blend"))
    for o in objects.values():
        o.location = (0, 0, 0)

    if PREVIEW:
        render_preview(objects, layout, OUT / "OriginSanctuary_preview.png")

    if UNITY:
        dst = Path(UNITY).resolve()
        for sub in ("Models", "Textures", "Data", "Audio"):
            (dst / sub).mkdir(parents=True, exist_ok=True)
        for p in fbx_dir.glob("*.fbx"):
            shutil.copy2(p, dst / "Models" / p.name)
        for p in tex_dir.glob("*.png"):
            shutil.copy2(p, dst / "Textures" / p.name)
        for name in ("OriginSanctuaryLayout.json", "OriginSanctuaryLocations.json", "OriginSanctuaryColliders.json",
                     "OriginSanctuaryLiving.json"):
            shutil.copy2(OUT / name, dst / "Data" / name)
        for p in audio_dir.glob("*.wav"):
            shutil.copy2(p, dst / "Audio" / p.name)
        shutil.copy2(OUT / "manifest.json", dst / "Data" / "OriginSanctuaryManifest.json")
        print("[env] copied into", dst)

    if failures:
        for f in failures:
            print("[env] BUDGET/UV FAILURE:", f)
        raise SystemExit(1)
    print("[env] done ->", OUT)


if __name__ == "__main__":  # importable by build_zone_kits.py (U1-ENV.4)
    main()
