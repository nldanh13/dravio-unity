"""Origin Sanctuary layout v2: landscape-only, three microhabitats, Unity coordinates.

Unity: X right, Y up, Z away from the fixed camera. One file drives the Blender
build (preview + baked scatter chunks), the Unity scene builder and the location
data Living World will read (habitats, walkable areas, obstacles, spawn points,
transitions, perches).

    python scripts/art/environment/layout.py <layout.json> [<locations.json>]

Habitat IDs and unlock rules mirror the canonical domain data
(Assets/Dravio/Domain/Catalog: G0BiologyData + ProgressCatalog.HabitatUnlockRules).
This module never imports or changes the domain; it only uses the same string IDs.
"""
import json
import math
import random
import sys

LAYOUT_VERSION = 2
SEED = 5209
ZONE_ID = "origin"

# --------------------------------------------------------------- screen / camera
# Landscape phones the layout must support (width / height).
ASPECTS = {"16:9": 16 / 9, "19.5:9": 19.5 / 9, "20:9": 20 / 9}
# Fantasy UI panel on the right, measured in SCREEN HEIGHTS so it stays the same
# size when a Canvas scales with height (match = 1). 0.5 = 28% of a 16:9 screen.
RIGHT_PANEL_HEIGHTS = 0.5
# Left safe margin (notch / rounded corners on 19.5:9 and 20:9 phones).
LEFT_MARGIN_HEIGHTS = 0.05
# Top and bottom HUD strips kept clear of gameplay (screen heights).
TOP_MARGIN_HEIGHTS = 0.06
BOTTOM_MARGIN_HEIGHTS = 0.05

ORTHO_SIZE = 6.0
PITCH_DEG = 32.0
CAM_DISTANCE = 26.0
# Ground point the free (non-UI) part of the screen is centred on.
FOCUS = (0.0, 0.0, 0.9)
# Raise the framing so tall ruins at the back stay inside the screen.
SCREEN_LIFT = 1.75


def _cam_basis():
    p = math.radians(PITCH_DEG)
    fwd = (0.0, -math.sin(p), math.cos(p))
    up = (0.0, math.cos(p), math.sin(p))
    right = (1.0, 0.0, 0.0)
    return right, up, fwd


def camera_anchor():
    """Camera position before the horizontal UI offset (Unity space)."""
    _, up, fwd = _cam_basis()
    return tuple(FOCUS[i] - fwd[i] * CAM_DISTANCE + up[i] * SCREEN_LIFT for i in range(3))


def ui_offset(ortho=ORTHO_SIZE):
    """Shift along camera right so the free area is centred on FOCUS (same for all aspects)."""
    return ortho * (RIGHT_PANEL_HEIGHTS - LEFT_MARGIN_HEIGHTS)


def camera_position(ortho=ORTHO_SIZE):
    a = camera_anchor()
    return (a[0] + ui_offset(ortho), a[1], a[2])


def to_screen(p, aspect, ortho=ORTHO_SIZE):
    """World point -> normalised viewport (0..1, 0..1) for the fixed ortho camera."""
    right, up, _ = _cam_basis()
    c = camera_position(ortho)
    d = (p[0] - c[0], p[1] - c[1], p[2] - c[2])
    sx = sum(d[i] * right[i] for i in range(3))
    sy = sum(d[i] * up[i] for i in range(3))
    return 0.5 + sx / (2 * ortho * aspect), 0.5 + sy / (2 * ortho)


def free_viewport(aspect):
    """Viewport rectangle not covered by UI or margins: (x0, y0, x1, y1)."""
    return (LEFT_MARGIN_HEIGHTS / aspect, BOTTOM_MARGIN_HEIGHTS,
            1 - RIGHT_PANEL_HEIGHTS / aspect, 1 - TOP_MARGIN_HEIGHTS)


def ground_from_viewport(vx, vy, aspect, ortho=ORTHO_SIZE):
    """Inverse: viewport point -> ground (y = 0) point."""
    right, up, fwd = _cam_basis()
    c = camera_position(ortho)
    sx = (vx - 0.5) * 2 * ortho * aspect
    sy = (vy - 0.5) * 2 * ortho
    o = [c[i] + right[i] * sx + up[i] * sy for i in range(3)]
    t = -o[1] / fwd[1]
    return o[0] + fwd[0] * t, o[2] + fwd[2] * t


CAMERA = {
    "anchorPosition": [round(v, 4) for v in camera_anchor()],
    "eulerAngles": [PITCH_DEG, 0.0, 0.0],
    "orthographicSize": ORTHO_SIZE,
    "rightPanelScreenHeights": RIGHT_PANEL_HEIGHTS,
    "leftMarginScreenHeights": LEFT_MARGIN_HEIGHTS,
    "topMarginScreenHeights": TOP_MARGIN_HEIGHTS,
    "bottomMarginScreenHeights": BOTTOM_MARGIN_HEIGHTS,
    "minAspect": round(16 / 9, 4),
    "supportedAspects": list(ASPECTS.keys()),
}

# ------------------------------------------------------------------- habitats
# Shared borders use identical vertices so the three areas tile without gaps.
BORDER_POND_MEADOW = [(-2.4, -5.4), (-2.4, -1.0), (-2.8, 2.2), (-3.3, 5.0)]
BORDER_MEADOW_ROCKS = [(2.6, -5.4), (2.6, -1.2), (3.0, 2.0), (2.9, 5.0)]
PLAY_X = 7.0

HABITATS = [
    {
        "id": "origin_pondbank",
        "displayNameVi": "Bờ hồ Khởi Nguyên",
        "displayNameEn": "Origin Pondbank",
        "defaultUnlocked": False,
        "requiresDiscovery": "DISC_AQUATIC_HABITAT",
        "polygon": [(-PLAY_X, -5.4)] + BORDER_POND_MEADOW + [(-PLAY_X, 5.0)],
    },
    {
        "id": "origin_meadow",
        "displayNameVi": "Đồng cỏ Khởi Nguyên",
        "displayNameEn": "Origin Meadow",
        "defaultUnlocked": True,
        "requiresDiscovery": "",
        "polygon": (BORDER_MEADOW_ROCKS
                    + [(1.3, 5.3), (1.1, 6.2), (-1.1, 6.2), (-1.3, 5.3)]
                    + list(reversed(BORDER_POND_MEADOW))),
    },
    {
        "id": "origin_warm_rocks",
        "displayNameVi": "Đá ấm Khởi Nguyên",
        "displayNameEn": "Origin Warm Rocks",
        "defaultUnlocked": False,
        "requiresDiscovery": "DISC_EMBERLING_THERMAL",
        "polygon": [(PLAY_X, -5.4), (PLAY_X, 5.0)] + list(reversed(BORDER_MEADOW_ROCKS)),
    },
]

POND_CENTER = (-4.9, -0.4)
POND_RADII = (2.3, 1.55)       # matches SM_ENV_Pond_Bank (x, z) at scale 1
SWIM_RADII = (2.0, 1.3)

# Flat play area: the ground mesh is perfectly flat here (y = 0) so spawn points
# and walkable data need no height lookup.
FLAT_RECT = (-8.6, -7.0, 8.6, 7.4)  # x0, z0, x1, z1


def P(asset, x, z, yaw=0.0, s=1.0, y=0.0, habitat=""):
    return {"asset": asset, "x": round(x, 3), "y": round(y, 3), "z": round(z, 3),
            "yaw": round(yaw, 2), "scale": round(s, 3), "habitat": habitat}


def hero_props():
    """Hand placed props. `habitat` is informational (scene grouping)."""
    m, pb, wr = "origin_meadow", "origin_pondbank", "origin_warm_rocks"
    return [
        P("SM_ENV_Ground_Sanctuary", 0, 0),
        # Ruined backdrop.
        P("SM_ENV_Gate_Ancient", 0, 7.5, s=0.72),
        P("SM_ENV_Wall_Ruin_A", -4.3, 7.5, yaw=4, s=0.9),
        P("SM_ENV_Wall_Ruin_B", 4.5, 7.4, yaw=-6, s=0.9),
        P("SM_ENV_Column_Intact", -3.2, 6.1, yaw=12, s=0.85),
        P("SM_ENV_Column_Broken", 3.4, 6.0, yaw=-20, s=0.85),
        # Low hedge line behind the ruins (tall trees here would be cut by the screen top).
        P("SM_ENV_Shrub_Round", -1.9, 10.4, yaw=30, s=1.5),
        P("SM_ENV_Shrub_Round", 2.6, 10.1, yaw=160, s=1.4),
        P("SM_ENV_Shrub_Round", -6.8, 9.8, yaw=80, s=1.6),
        P("SM_ENV_Shrub_Round", 7.2, 9.6, yaw=250, s=1.5),
        P("SM_ENV_Vine_Hanging", -0.85, 7.55, y=2.8, s=0.72),
        P("SM_ENV_Vine_Hanging", 1.2, 7.55, y=2.6, yaw=180, s=0.65),
        # origin_pondbank (left).
        P("SM_ENV_Pond_Bank", POND_CENTER[0], POND_CENTER[1], habitat=pb),
        P("SM_ENV_Stepping_Stones", -2.9, -2.6, yaw=-20, habitat=pb),
        P("SM_ENV_Runestone_B", -6.5, 3.7, yaw=25, s=0.85, habitat=pb),
        P("SM_ENV_Rock_Mossy_B", -6.6, -4.1, yaw=70, habitat=pb),
        P("SM_ENV_Rock_Mossy_C", -3.3, 2.9, yaw=300, s=0.9, habitat=pb),
        P("SM_ENV_Mushroom_Cluster_Glow", -5.9, 2.6, yaw=10, habitat=pb),
        P("SM_ENV_Shrub_Round", -3.6, -4.7, yaw=40, s=0.85, habitat=pb),
        # origin_meadow (centre).
        P("SM_ENV_Runestone_A", -1.9, 4.1, yaw=18, s=0.9, habitat=m),
        P("SM_ENV_Runestone_A", 1.9, 4.0, yaw=-18, s=0.9, habitat=m),
        P("SM_ENV_Mushroom_Cluster_Red", 1.6, -4.4, yaw=200, s=0.9, habitat=m),
        P("SM_ENV_Rock_Mossy_C", -1.4, -4.6, yaw=120, s=0.8, habitat=m),
        # origin_warm_rocks (right).
        P("SM_ENV_Rock_Warm_Flat", 4.6, 0.6, yaw=15, habitat=wr),
        P("SM_ENV_Rock_Warm_Flat", 5.6, -3.4, yaw=-50, s=0.85, habitat=wr),
        P("SM_ENV_Rock_Warm_Stack", 6.2, 2.9, yaw=200, habitat=wr),
        P("SM_ENV_Rock_Warm_Stack", 3.7, -2.4, yaw=80, s=0.7, habitat=wr),
        P("SM_ENV_Statue_Dragon", 5.7, 5.9, yaw=-25, s=1.05, habitat=wr),
        P("SM_ENV_Column_Fallen", 8.8, -3.0, yaw=30, s=0.85),
        # Wide-screen and under-UI dressing (never walkable).
        P("SM_ENV_Tree_Ancient", -9.9, 3.2, yaw=10, s=1.05),
        P("SM_ENV_Root_Cluster", -8.6, 2.4, yaw=-20, s=0.7),
        P("SM_ENV_Shrub_Round", -9.7, -1.4, yaw=110, s=1.1),
        P("SM_ENV_Rock_Mossy_A", -9.2, -4.6, yaw=40),
        P("SM_ENV_Column_Broken", -9.3, 6.8, yaw=140, s=0.9),
        P("SM_ENV_Tree_Ancient", 9.9, 4.6, yaw=200, s=0.95),
        P("SM_ENV_Root_Cluster", 9.0, 3.6, yaw=160, s=0.7),
        P("SM_ENV_Rock_Mossy_A", 8.9, -1.7, yaw=200, s=1.1),
        P("SM_ENV_Shrub_Round", 10.4, -4.4, yaw=10),
        P("SM_ENV_Column_Intact", 8.3, 6.6, yaw=-35, s=0.85),
        P("SM_ENV_Tree_Ancient_B", 12.8, 8.0, yaw=120),
        P("SM_ENV_Tree_Ancient_B", -12.6, 8.4, yaw=60),
        # Baked ground-cover chunks: one renderer per habitat / edge band.
        P("SM_ENV_Cover_Origin_Meadow", 0, 0, habitat=m),
        P("SM_ENV_Cover_Origin_Pondbank", 0, 0, habitat=pb),
        P("SM_ENV_Cover_Origin_Warm_Rocks", 0, 0, habitat=wr),
        P("SM_ENV_Cover_Edges", 0, 0),
    ]


# ------------------------------------------------------------------ colliders
# Simple colliders per asset, written in BLENDER object space to mirror the
# builder code: ("box", (cx, cy, cz), (sx, sy, sz), yaw_radians).
# Converted to Unity local space by collider_specs_unity().
BLENDER_COLLIDERS = {
    "SM_ENV_Gate_Ancient": [("box", (-2.05, 0, 2.3), (1.25, 1.15, 4.6), 0), ("box", (2.05, 0, 2.3), (1.25, 1.15, 4.6), 0),
                            ("box", (0, 0, 5.3), (5.3, 0.9, 1.6), 0), ("box", (2.9, 1.2, 0.3), (0.8, 0.75, 0.6), 0.5)],
    "SM_ENV_Column_Intact": [("box", (0, 0, 1.75), (1.1, 1.1, 3.5), 0)],
    "SM_ENV_Column_Broken": [("box", (0, 0, 1.1), (1.1, 1.1, 2.2), 0.2)],
    "SM_ENV_Column_Fallen": [("box", (0.3, 0, 0.3), (4.0, 0.95, 0.6), 0)],
    "SM_ENV_Wall_Ruin_A": [("box", (0, 0, 1.2), (3.4, 0.7, 2.4), 0)],
    "SM_ENV_Wall_Ruin_B": [("box", (0.4, 0, 0.95), (2.4, 0.7, 1.9), 0), ("box", (-0.85, -0.45, 0.9), (0.7, 1.6, 1.8), 0)],
    "SM_ENV_Statue_Dragon": [("box", (0, 0, 1.6), (1.8, 1.6, 3.2), 0)],
    "SM_ENV_Tree_Ancient": [("box", (0, 0, 2.0), (1.6, 1.6, 4.0), 0)],
    "SM_ENV_Tree_Ancient_B": [("box", (0, 0, 1.6), (1.2, 1.2, 3.2), 0)],
    "SM_ENV_Rock_Mossy_A": [("box", (0, 0, 0.45), (1.9, 1.6, 0.9), 0)],
    "SM_ENV_Rock_Mossy_B": [("box", (0, 0, 0.35), (1.3, 1.1, 0.7), 0)],
    "SM_ENV_Runestone_A": [("box", (0, 0, 0.85), (0.85, 0.55, 1.7), 0)],
    "SM_ENV_Runestone_B": [("box", (0, 0, 0.75), (1.3, 0.6, 1.5), 0)],
    "SM_ENV_Rock_Warm_Flat": [("box", (0, 0, 0.22), (2.1, 1.5, 0.44), 0)],
    "SM_ENV_Rock_Warm_Stack": [("box", (0, 0, 0.6), (1.7, 1.4, 1.2), 0)],
    "SM_ENV_Shrub_Round": [("box", (0, 0, 0.5), (1.4, 1.3, 1.0), 0)],
}
# Thin box under the playable ground (Unity local, centre and size).
GROUND_COLLIDER = {"center": [0.0, -0.25, 0.0], "size": [18.0, 0.5, 15.0]}


def collider_specs_unity():
    out = {}
    for asset, boxes in BLENDER_COLLIDERS.items():
        specs = []
        for kind, (bx, by, bz), (sx, sy, sz), yaw in boxes:
            specs.append({"type": kind, "center": [round(-bx, 3), round(bz, 3), round(-by, 3)],
                          "size": [sx, sz, sy], "yaw": round(-math.degrees(yaw), 2)})
        out[asset] = specs
    out["SM_ENV_Ground_Sanctuary"] = [{"type": "box", "center": GROUND_COLLIDER["center"],
                                        "size": GROUND_COLLIDER["size"], "yaw": 0.0}]
    return out


def _rot_y(x, z, deg):
    r = math.radians(deg)
    return x * math.cos(r) + z * math.sin(r), -x * math.sin(r) + z * math.cos(r)


def obstacle_polygons(placements):
    """World-space XZ footprints of every collider (rectangles), for walkability."""
    specs = collider_specs_unity()
    out = []
    for p in placements:
        if p["asset"] == "SM_ENV_Ground_Sanctuary":
            continue
        for s in specs.get(p["asset"], []):
            cx, cz = s["center"][0] * p["scale"], s["center"][2] * p["scale"]
            hx, hz = s["size"][0] * p["scale"] / 2, s["size"][2] * p["scale"] / 2
            pts = []
            for ox, oz in ((-hx, -hz), (hx, -hz), (hx, hz), (-hx, hz)):
                lx, lz = _rot_y(ox, oz, s["yaw"])
                wx, wz = _rot_y(cx + lx, cz + lz, p["yaw"])
                pts.append((round(p["x"] + wx, 3), round(p["z"] + wz, 3)))
            out.append({"source": p["asset"], "polygon": pts})
    return out


def ellipse(cx, cz, rx, rz, n=16):
    return [(round(cx + math.cos(2 * math.pi * i / n) * rx, 3),
             round(cz + math.sin(2 * math.pi * i / n) * rz, 3)) for i in range(n)]


# --------------------------------------------------------------- geometry util
def point_in_poly(x, z, poly):
    inside = False
    n = len(poly)
    for i in range(n):
        x1, z1 = poly[i]
        x2, z2 = poly[(i + 1) % n]
        if (z1 > z) != (z2 > z):
            if x < (x2 - x1) * (z - z1) / (z2 - z1) + x1:
                inside = not inside
    return inside


def dist_to_poly_edge(x, z, poly):
    best = 1e9
    n = len(poly)
    for i in range(n):
        ax, az = poly[i]
        bx, bz = poly[(i + 1) % n]
        dx, dz = bx - ax, bz - az
        t = max(0.0, min(1.0, ((x - ax) * dx + (z - az) * dz) / (dx * dx + dz * dz + 1e-9)))
        best = min(best, math.hypot(x - (ax + t * dx), z - (az + t * dz)))
    return best


def poly_area(poly):
    a = 0.0
    for i in range(len(poly)):
        x1, z1 = poly[i]
        x2, z2 = poly[(i + 1) % len(poly)]
        a += x1 * z2 - x2 * z1
    return abs(a) / 2


def poly_centroid(poly):
    xs = [p[0] for p in poly]
    zs = [p[1] for p in poly]
    return sum(xs) / len(xs), sum(zs) / len(zs)


def habitat(hid):
    return next(h for h in HABITATS if h["id"] == hid)


def blocked(x, z, obstacles, clearance=0.0):
    for o in obstacles:
        if point_in_poly(x, z, o["polygon"]) or dist_to_poly_edge(x, z, o["polygon"]) < clearance:
            return True
    return False


def in_water(x, z, margin=0.0):
    rx, rz = POND_RADII
    return ((x - POND_CENTER[0]) / (rx + margin)) ** 2 + ((z - POND_CENTER[1]) / (rz + margin)) ** 2 < 1


def walkable(x, z, hid, obstacles, clearance=0.45):
    poly = habitat(hid)["polygon"]
    return (point_in_poly(x, z, poly) and not blocked(x, z, obstacles, clearance)
            and not in_water(x, z, clearance))


# -------------------------------------------------------- spawns / transitions
SPAWNS = {
    "origin_meadow": [(0.0, 0.2, 180), (-1.2, -1.6, 160), (1.3, -1.4, 200), (-0.9, 2.2, 170), (1.0, 2.3, 190)],
    "origin_pondbank": [(-5.4, 2.4, 150), (-6.2, -2.8, 160), (-3.6, 1.5, 200), (-4.9, -3.4, 170)],
    "origin_warm_rocks": [(4.2, 2.2, 200), (6.1, 0.3, 210), (4.0, -1.0, 190), (5.9, -1.6, 200)],
}
# Transition points sit on the shared border (radius = trigger size).
TRANSITIONS = [
    ("origin_meadow", "origin_pondbank", (-2.45, -2.8)),
    ("origin_meadow", "origin_pondbank", (-2.95, 3.4)),
    ("origin_meadow", "origin_warm_rocks", (2.75, 0.4)),
    ("origin_meadow", "origin_warm_rocks", (2.6, -3.8)),
]
# Perch spots (canonical profiles mark all three habitats perch: true).
PERCHES = [
    ("origin_warm_rocks", "SM_ENV_Rock_Warm_Flat", 4.6, 0.6, 0.44),
    ("origin_warm_rocks", "SM_ENV_Rock_Warm_Flat", 5.6, -3.4, 0.38),
    ("origin_warm_rocks", "SM_ENV_Rock_Warm_Stack", 6.2, 2.9, 1.2),
    ("origin_pondbank", "SM_ENV_Stepping_Stones", -2.9, -2.6, 0.12),
    ("origin_pondbank", "SM_ENV_Rock_Mossy_B", -6.6, -4.1, 0.7),
    ("origin_meadow", "SM_ENV_Runestone_A", -1.9, 3.6, 0.0),
]


def build_locations(placements):
    obstacles = obstacle_polygons(placements)
    habitats = []
    for h in HABITATS:
        poly = h["polygon"]
        entry = {
            "id": h["id"], "zoneId": ZONE_ID,
            "displayNameVi": h["displayNameVi"], "displayNameEn": h["displayNameEn"],
            "defaultUnlocked": h["defaultUnlocked"], "requiresDiscovery": h["requiresDiscovery"],
            "polygonXZ": [round(c, 3) for pt in poly for c in pt],
            "centerXZ": [round(c, 3) for c in poly_centroid(poly)],
            "areaM2": round(poly_area(poly), 2),
            "swimPolygonXZ": [],
        }
        if h["id"] == "origin_pondbank":
            entry["swimPolygonXZ"] = [c for pt in ellipse(*POND_CENTER, *SWIM_RADII) for c in pt]
        habitats.append(entry)
    spawns = []
    for hid, pts in SPAWNS.items():
        for i, (x, z, yaw) in enumerate(pts, 1):
            spawns.append({"id": f"{hid}.spawn_{i:02d}", "habitatId": hid, "kind": "dragon",
                           "x": x, "y": 0.0, "z": z, "yaw": float(yaw)})
    transitions = []
    for i, (a, b, (x, z)) in enumerate(TRANSITIONS, 1):
        transitions.append({"id": f"{a}__{b}_{i:02d}", "habitatA": a, "habitatB": b,
                            "x": x, "y": 0.0, "z": z, "radius": 0.9})
    perches = []
    for i, (hid, src, x, z, y) in enumerate(PERCHES, 1):
        perches.append({"id": f"{hid}.perch_{i:02d}", "habitatId": hid, "source": src,
                        "x": x, "y": y, "z": z})
    water = [{"id": "origin_pondbank.pond", "habitatId": "origin_pondbank",
              "polygonXZ": [c for pt in ellipse(*POND_CENTER, *POND_RADII) for c in pt]}]
    return {
        "version": LAYOUT_VERSION,
        "zoneId": ZONE_ID,
        "flatRectXZ": list(FLAT_RECT),
        "walkClearance": 0.45,
        "habitats": habitats,
        "spawnPoints": spawns,
        "transitions": transitions,
        "perchPoints": perches,
        "waterAreas": water,
        "obstacles": [{"source": o["source"], "polygonXZ": [c for pt in o["polygon"] for c in pt]}
                      for o in obstacles],
    }


# ------------------------------------------------------------------- scatter
# Ground cover is baked per habitat into one mesh each (see build script).
COVER_KINDS = {
    "origin_meadow": [("grass_a", 70), ("grass_b", 40), ("meadow_tall", 26), ("flower_pink", 14),
                      ("flower_gold", 14), ("flower_lilac", 10), ("clover", 20)],
    "origin_pondbank": [("reed", 22), ("cattail", 14), ("grass_b", 40), ("fern", 12), ("flower_blue", 12),
                        ("clover", 14)],
    "origin_warm_rocks": [("dry_grass", 46), ("twig_dry", 14), ("grass_a", 14), ("flower_gold", 8)],
    "edges": [("grass_a", 60), ("grass_b", 60), ("fern", 22), ("shrub_leaf", 18), ("meadow_tall", 30),
              ("flower_pink", 10), ("flower_blue", 10)],
}
CLEARING = (0.0, 0.2, 1.4)  # x, z, radius kept free in the meadow centre


def scatter_cover(placements):
    """Deterministic cover points: {chunk: [(cell, x, z, yaw, scale)]} in Unity space."""
    rng = random.Random(SEED)
    obstacles = obstacle_polygons(placements)
    keep_free = [(x, z, 0.55) for pts in SPAWNS.values() for x, z, _ in pts]
    keep_free += [(x, z, 0.6) for _, _, (x, z) in TRANSITIONS]
    out = {}
    for chunk, kinds in COVER_KINDS.items():
        pts = []
        for cell, count in kinds:
            placed = tries = 0
            while placed < count and tries < count * 80:
                tries += 1
                if chunk == "edges":
                    x, z = rng.uniform(-14.5, 15.5), rng.uniform(-9.5, 12.0)
                    if any(point_in_poly(x, z, h["polygon"]) for h in HABITATS):
                        continue
                else:
                    poly = habitat(chunk)["polygon"]
                    x, z = rng.uniform(-PLAY_X, PLAY_X), rng.uniform(-5.4, 6.2)
                    if not point_in_poly(x, z, poly):
                        continue
                    edge = dist_to_poly_edge(x, z, poly)
                    # Lighter in the middle of a habitat so dragons stay readable.
                    if edge > 1.6 and rng.random() < 0.55:
                        continue
                if blocked(x, z, obstacles, 0.15):
                    continue
                if cell in ("reed", "cattail"):
                    if in_water(x, z, -0.1) or not in_water(x, z, 0.9):
                        continue
                elif in_water(x, z, 0.15):
                    continue
                if math.hypot(x - CLEARING[0], z - CLEARING[1]) < CLEARING[2]:
                    continue
                if any(math.hypot(x - fx, z - fz) < fr for fx, fz, fr in keep_free):
                    continue
                pts.append((cell, round(x, 3), round(z, 3), round(rng.uniform(0, 360), 1),
                            round(rng.uniform(0.8, 1.25), 3)))
                placed += 1
        out[chunk] = pts
    return out


# Ground path (vertex alpha) from the meadow up to the gate steps, and to each habitat.
PATHS = [
    [(0.0, -0.4), (0.0, 2.6), (0.3, 4.8), (0.0, 6.9)],
    [(-0.6, -0.6), (-2.4, -1.6), (-3.4, -2.6)],
    [(0.6, -0.2), (2.8, 0.4), (4.0, 0.5)],
]
PATH_WIDTH = 1.1


def path_distance(x, z):
    best = 1e9
    for path in PATHS:
        for (ax, az), (bx, bz) in zip(path, path[1:]):
            dx, dz = bx - ax, bz - az
            t = max(0.0, min(1.0, ((x - ax) * dx + (z - az) * dz) / (dx * dx + dz * dz)))
            best = min(best, math.hypot(x - (ax + t * dx), z - (az + t * dz)))
    return best


def build_layout():
    return {
        "version": LAYOUT_VERSION,
        "seed": SEED,
        "camera": CAMERA,
        "placements": hero_props(),
    }


def write(layout_path, locations_path=None):
    data = build_layout()
    with open(layout_path, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=1)
    if locations_path:
        with open(locations_path, "w", encoding="utf-8") as f:
            json.dump(build_locations(data["placements"]), f, indent=1)
    return data


def write_colliders(path):
    with open(path, "w", encoding="utf-8") as f:
        json.dump({"version": LAYOUT_VERSION,
                   "assets": [{"asset": a, "boxes": s} for a, s in sorted(collider_specs_unity().items())]},
                  f, indent=1)


if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else "OriginSanctuaryLayout.json"
    loc = sys.argv[2] if len(sys.argv) > 2 else None
    d = write(out, loc)
    print("placements:", len(d["placements"]), "->", out)
