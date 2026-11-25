"""Deterministic Origin Sanctuary scene layout, written in UNITY coordinates.

Unity: X right, Y up, Z away from the fixed camera (camera sits at -Z).
The same JSON drives the Blender preview render and the Unity scene builder, so
the two never drift apart. Edit here, re-run the Blender build, rebuild the scene.

    python scripts/art/environment/layout.py <output.json>
"""
import json
import math
import random
import sys

LAYOUT_VERSION = 1
SEED = 4117

# Fixed 2.5D orthographic camera (Unity space).
CAMERA = {
    "position": [0.0, 9.0, -16.0],
    "lookAt": [0.0, 1.9, 4.2],
    "orthographicSize": 6.6,
    # SanctuaryCameraFit keeps at least this much world visible on narrow
    # (portrait phone) screens.
    "minVisibleWidth": 13.0,
}

# Central clearing kept free for Emberling and gameplay.
CLEARING = (0.0, 0.0, 2.5)  # x, z, radius
# Painted earth path from the clearing to the gate (ground vertex alpha).
PATH = [(0.0, -0.5), (0.0, 2.8), (0.35, 5.2), (0.0, 7.6)]
PATH_WIDTH = 1.15


def P(asset, x, z, yaw=0.0, s=1.0, y=0.0):
    return {"asset": asset, "x": round(x, 3), "y": round(y, 3), "z": round(z, 3),
            "yaw": round(yaw, 2), "scale": round(s, 3)}


def hero_props():
    return [
        P("SM_ENV_Ground_Sanctuary", 0, 0),
        P("SM_ENV_Gate_Ancient", 0, 8.2),
        P("SM_ENV_Wall_Ruin_A", -4.35, 8.0, yaw=4),
        P("SM_ENV_Wall_Ruin_B", 4.4, 7.8, yaw=-6),
        P("SM_ENV_Column_Intact", -3.3, 5.1, yaw=12),
        P("SM_ENV_Column_Broken", 3.4, 5.2, yaw=-20),
        P("SM_ENV_Column_Broken", -6.6, 3.2, yaw=140, s=0.95),
        P("SM_ENV_Column_Intact", 6.7, 3.0, yaw=-35, s=0.92),
        P("SM_ENV_Column_Fallen", 5.0, -0.6, yaw=-62),
        P("SM_ENV_Statue_Dragon", -6.1, 6.4, yaw=28, s=1.3),
        P("SM_ENV_Tree_Ancient", -9.6, 4.6, yaw=10, s=1.05),
        P("SM_ENV_Tree_Ancient", 9.9, 5.6, yaw=200, s=0.95),
        P("SM_ENV_Tree_Ancient", 2.2, 13.5, yaw=80, s=1.15),
        P("SM_ENV_Tree_Ancient", -3.6, 14.2, yaw=300, s=1.05),
        P("SM_ENV_Root_Cluster", -8.3, 2.4, yaw=-20),
        P("SM_ENV_Root_Cluster", 8.8, 3.6, yaw=160),
        P("SM_ENV_Vine_Hanging", -1.2, 8.05, y=3.9),
        P("SM_ENV_Vine_Hanging", 1.65, 8.05, y=3.55, yaw=180, s=0.85),
        P("SM_ENV_Vine_Hanging", -4.2, 7.6, y=2.2, s=0.7),
        P("SM_ENV_Pond_Small", -4.4, -2.3, yaw=8),
        P("SM_ENV_Runestone_A", -2.7, 2.4, yaw=22),
        P("SM_ENV_Runestone_B", 2.8, 2.2, yaw=-18),
        P("SM_ENV_Runestone_A", 6.2, -3.0, yaw=-38, s=0.9),
        P("SM_ENV_Runestone_B", -7.4, 0.2, yaw=40, s=0.9),
        P("SM_ENV_Rock_Mossy_A", 7.6, 0.9, yaw=40),
        P("SM_ENV_Rock_Mossy_A", -2.0, 10.4, yaw=200, s=1.2),
        P("SM_ENV_Rock_Mossy_B", -5.4, 3.9, yaw=70),
        P("SM_ENV_Rock_Mossy_B", 4.6, 2.6, yaw=10, s=0.8),
        P("SM_ENV_Rock_Mossy_C", 1.9, -3.6, yaw=120),
        P("SM_ENV_Rock_Mossy_C", -1.4, 6.3, yaw=300, s=0.8),
        P("SM_ENV_Mushroom_Cluster_Red", -8.6, 3.4, yaw=30),
        P("SM_ENV_Mushroom_Cluster_Glow", 8.3, 2.3, yaw=-50),
        P("SM_ENV_Mushroom_Cluster_Glow", -5.7, 5.2, yaw=10, s=0.8),
        P("SM_ENV_Mushroom_Cluster_Red", 3.7, -2.6, yaw=200, s=0.9),
    ]


# Circles (x, z, r) where small scatter must not spawn.
def blockers():
    return [
        (CLEARING[0], CLEARING[1], CLEARING[2]),
        (0.0, 8.2, 2.9), (-4.35, 8.0, 1.9), (4.4, 7.8, 1.9),
        (-3.3, 5.1, 0.7), (3.4, 5.2, 0.7), (-6.6, 3.2, 0.7), (6.7, 3.0, 0.7),
        (5.0, -0.6, 1.3), (-6.1, 6.4, 1.2), (-9.6, 4.6, 1.3), (9.9, 5.6, 1.3),
        (-4.4, -2.3, 2.1), (-2.7, 2.4, 0.5), (2.8, 2.2, 0.5), (6.2, -3.0, 0.5),
        (-7.4, 0.2, 0.5), (7.6, 0.9, 0.9), (-5.4, 3.9, 0.6),
    ]


def path_distance(x, z):
    best = 1e9
    for (ax, az), (bx, bz) in zip(PATH, PATH[1:]):
        dx, dz = bx - ax, bz - az
        t = max(0.0, min(1.0, ((x - ax) * dx + (z - az) * dz) / (dx * dx + dz * dz)))
        best = min(best, math.hypot(x - (ax + t * dx), z - (az + t * dz)))
    return best


def scatter():
    rng = random.Random(SEED)
    out = []
    kinds = [
        ("SM_ENV_Grass_Tuft_A", 46, 0.8, 1.3),
        ("SM_ENV_Grass_Tuft_B", 40, 0.8, 1.25),
        ("SM_ENV_Flower_Patch_Pink", 12, 0.85, 1.15),
        ("SM_ENV_Flower_Patch_Blue", 10, 0.85, 1.15),
        ("SM_ENV_Flower_Patch_Gold", 10, 0.85, 1.15),
        ("SM_ENV_Fern", 12, 0.8, 1.2),
    ]
    blocks = blockers()
    for asset, count, smin, smax in kinds:
        placed, tries = 0, 0
        while placed < count and tries < count * 60:
            tries += 1
            # Denser at the frame edges and around ruins, lighter in the middle.
            x = rng.uniform(-11.0, 11.0)
            z = rng.uniform(-6.5, 11.0)
            if abs(x) < 3.0 and z < 1.0 and rng.random() < 0.6:
                continue
            if any(math.hypot(x - bx, z - bz) < br for bx, bz, br in blocks):
                continue
            if path_distance(x, z) < PATH_WIDTH * 0.75:
                continue
            out.append(P(asset, x, z, yaw=rng.uniform(0, 360), s=rng.uniform(smin, smax)))
            placed += 1
    return out


def build_layout():
    return {
        "version": LAYOUT_VERSION,
        "seed": SEED,
        "camera": CAMERA,
        "clearing": {"x": CLEARING[0], "z": CLEARING[1], "radius": CLEARING[2]},
        "placements": hero_props() + scatter(),
    }


def write(path):
    data = build_layout()
    with open(path, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=1)
    return data


if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else "OriginSanctuaryLayout.json"
    d = write(out)
    print("placements:", len(d["placements"]), "->", out)
