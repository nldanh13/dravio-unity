"""Thiên Giới (Sky) environment kit data (U1-ENV.4), Unity coordinates.

Floating islands, cloud-top ruins and blossom meadows above a sea of clouds, framed by
the same fixed landscape camera as the Origin Sanctuary.

The canonical data has ONE Sky microhabitat (canonical_world.py, from G0BiologyData):

  sky_low_island   mild, altitude Low, perch: true, vegetation 70

so every location here uses sky_low_island. The three islands are walk AREAS of that
habitat, not new microhabitats: the main island (y = 0, sky gate back to origin_meadow),
the east islet (y = 0.9, reached over a bridge or by flight) and the shrine islet
(y = 1.6, flight only). One fly volume covers the play space; perches sit on spires,
the column, the shrine dome and the gate keystone. The WorldGraph edge
origin_sky_low_island (requires Fly) is marked as a gate; nothing here unlocks the zone.
"""
import zone_common as zc

ZONE_ID = "sky"
FILE_PREFIX = "Sky"
LAYOUT_VERSION = 1
SEED = 8803
DISPLAY_VI, DISPLAY_EN = "Thiên Giới", "Sky Realm"
SCENE = {"name": "Sky_Environment", "displayNameVi": DISPLAY_VI, "displayNameEn": DISPLAY_EN,
         "background": [0.60, 0.75, 0.93]}  # ENV Alpha: deeper blue so white clouds separate
HID = "sky_low_island"

# ------------------------------------------------------------ walk areas
MAIN = [(-4.6, -5.0), (-1.2, -5.5), (2.4, -5.0), (3.2, -2.2), (3.0, 0.6), (3.1, 3.6), (2.2, 5.6), (0.0, 6.2),
        (-2.2, 5.8), (-3.8, 4.6), (-4.2, 1.6), (-4.9, -1.8)]
EAST = [(4.2, -3.6), (5.4, -4.4), (6.8, -3.7), (7.0, -1.0), (6.3, 0.9), (4.8, 1.1), (4.0, -0.8)]
SHRINE = [(-7.0, 2.6), (-6.0, 1.9), (-4.9, 2.4), (-4.8, 4.6), (-5.7, 5.6), (-6.9, 5.2)]
EAST_Y, SHRINE_Y = 0.9, 1.6
# Island tops extend past the walk polygons (rim, gate plinth); the meshes use these.
MAIN_MESH = [(-5.3, -5.6), (-1.2, -6.3), (2.9, -5.6), (3.8, -2.2), (3.6, 0.6), (3.8, 3.8), (2.9, 6.4), (2.4, 8.0),
             (-2.4, 8.0), (-2.9, 6.5), (-4.5, 5.2), (-4.9, 1.6), (-5.6, -1.8)]
FLAT_RECT = (-5.6, -6.3, 3.8, 8.0)

WALK_AREAS = [
    {"id": f"{HID}.main", "habitatId": HID, "displayNameVi": "Đảo chính", "polygon": MAIN, "y": 0.0},
    {"id": f"{HID}.east", "habitatId": HID, "displayNameVi": "Đảo đông", "polygon": EAST, "y": EAST_Y},
    {"id": f"{HID}.shrine", "habitatId": HID, "displayNameVi": "Đảo điện thờ", "polygon": SHRINE, "y": SHRINE_Y},
]
ISLAND_ASSETS = {f"{HID}.main": "SM_ENV_Sky_Island_Main", f"{HID}.east": "SM_ENV_Sky_Islet_East",
                 f"{HID}.shrine": "SM_ENV_Sky_Islet_Shrine"}

HABITATS = [{"id": HID, "displayNameVi": "Đảo thấp Thiên Giới", "displayNameEn": "Sky Low Island",
             "polygon": [(-7.2, -5.6), (7.2, -5.6), (7.2, 6.3), (-7.2, 6.3)]}]
VOLUMES = [{"id": f"{HID}.fly", "habitatId": HID, "medium": "fly", "polygon": HABITATS[0]["polygon"],
            "yMin": 1.0, "yMax": 3.6}]


def centroid(poly):
    return zc.poly_centroid(poly)


EAST_C, SHRINE_C = centroid(EAST), centroid(SHRINE)


# ------------------------------------------------------------------- props
def hero_props():
    P = zc.P
    return [
        P("SM_ENV_Sky_Cloud_Sea", 0, 6.0, y=-5.5),
        P("SM_ENV_Sky_Island_Main", 0, 0, habitat=HID),
        P("SM_ENV_Sky_Islet_East", EAST_C[0], EAST_C[1], y=EAST_Y, habitat=HID),
        P("SM_ENV_Sky_Islet_Shrine", SHRINE_C[0], SHRINE_C[1], y=SHRINE_Y, habitat=HID),
        P("SM_ENV_Sky_Bridge", 3.55, -1.0, habitat=HID),
        # Cloud-top ruins: the gate is the way home (origin_meadow).
        P("SM_ENV_Sky_Gate", 0.0, 6.9, s=0.7, habitat=HID),
        P("SM_ENV_Sky_Column", -2.9, 5.2, yaw=15, s=0.8, habitat=HID),
        P("SM_ENV_Sky_Column_Broken", 2.5, 4.9, yaw=-25, s=0.85, habitat=HID),
        P("SM_ENV_Sky_Shrine", -6.1, 4.2, y=SHRINE_Y, s=0.65, habitat=HID),
        P("SM_ENV_Sky_Runestone", 1.7, 3.9, yaw=-18, s=0.9, habitat=HID),
        P("SM_ENV_Sky_Tree_Blossom", -3.2, 3.4, yaw=40, s=0.85, habitat=HID),
        P("SM_ENV_Sky_Spire_Perch", -3.9, -4.2, yaw=20, habitat=HID),
        P("SM_ENV_Sky_Spire_Perch", 6.3, -3.0, y=EAST_Y, yaw=-40, s=0.8, habitat=HID),
        P("SM_ENV_Sky_Shrub", 2.4, -3.9, yaw=60, s=0.9, habitat=HID),
        P("SM_ENV_Sky_Shrub", -3.6, 0.4, yaw=200, s=0.8, habitat=HID),
        P("SM_ENV_Sky_Shrub", 6.2, 0.2, y=EAST_Y, yaw=10, s=0.7, habitat=HID),
        # Far islets and cloud banks (dressing, wide screens, under the UI panel).
        P("SM_ENV_Sky_Islet_Float_A", -10.8, 2.2, y=1.2, yaw=30),
        P("SM_ENV_Sky_Islet_Float_A", 10.2, 6.2, y=2.4, yaw=160, s=1.2),
        P("SM_ENV_Sky_Islet_Float_B", 4.2, 12.5, y=2.8, yaw=80, s=1.4),
        P("SM_ENV_Sky_Islet_Float_B", -6.8, 11.6, y=2.2, yaw=250, s=1.1),
        P("SM_ENV_Sky_Islet_Float_B", 11.8, -2.6, y=0.4, yaw=10, s=0.9),
        P("SM_ENV_Sky_Cloud_Puff", -9.6, -2.2, y=-1.6, s=1.4),
        P("SM_ENV_Sky_Cloud_Puff", 9.2, -4.8, y=-1.2, yaw=90, s=1.6),
        P("SM_ENV_Sky_Cloud_Puff", 12.0, 1.6, y=-0.6, yaw=40, s=1.3),
        P("SM_ENV_Sky_Cloud_Puff", -12.4, 6.4, y=-0.4, yaw=200, s=1.5),
        P("SM_ENV_Sky_Cloud_Puff", 6.0, 10.0, y=-2.4, yaw=120, s=1.8),
        P("SM_ENV_Sky_Cloud_Puff", -3.8, 10.4, y=-2.6, yaw=300, s=1.7),
        P("SM_ENV_Sky_Cloud_Puff", -6.4, -6.6, y=-3.2, yaw=10, s=1.6),
        P("SM_ENV_Sky_Cloud_Puff", 5.4, -7.2, y=-3.4, yaw=70, s=1.5),
        # Baked island-top cover.
        P("SM_ENV_Sky_Cover_Main", 0, 0, habitat=HID),
        P("SM_ENV_Sky_Cover_East", 0, 0, habitat=HID),
        P("SM_ENV_Sky_Cover_Shrine", 0, 0, habitat=HID),
    ]


BLENDER_COLLIDERS = {
    # ENV Alpha (#79): the arch is a semicircle of voussoirs (spring 3.88, radius 2.05) whose keystone top is at
    # 3.88 + 2.05 + 0.98 / 2 = 6.42 (build_gate). The lintel box stops at 6.1, so the perch on the keystone
    # (sky_low_island.perch_06, y 4.49 = 6.42 x 0.7) hit a collider 0.22 m lower in Unity. The keystone box below
    # (the keystone itself: 0.8 x 0.86 x 0.98 at z 5.93) puts the collider top on the mesh.
    "SM_ENV_Sky_Gate": [("box", (-2.05, 0, 2.3), (1.25, 1.15, 4.6), 0), ("box", (2.05, 0, 2.3), (1.25, 1.15, 4.6), 0),
                        ("box", (0, 0, 5.3), (5.3, 0.9, 1.6), 0), ("box", (0, 0, 5.93), (0.8, 0.86, 0.98), 0),
                        ("box", (2.9, 1.2, 0.3), (0.8, 0.75, 0.6), 0.5)],
    "SM_ENV_Sky_Column": [("box", (0, 0, 1.8), (1.1, 1.1, 3.6), 0)],
    "SM_ENV_Sky_Column_Broken": [("box", (0, 0, 1.1), (1.1, 1.1, 2.2), 0.2)],
    "SM_ENV_Sky_Shrine": [("box", (0, 0, 1.3), (2.3, 2.3, 2.6), 0), ("box", (0, 0, 2.86), (1.5, 1.5, 0.52), 0)],
    "SM_ENV_Sky_Runestone": [("box", (0, 0, 0.85), (0.85, 0.55, 1.7), 0)],
    "SM_ENV_Sky_Tree_Blossom": [("box", (0, 0, 1.6), (1.2, 1.2, 3.2), 0)],
    "SM_ENV_Sky_Spire_Perch": [("box", (0, 0, 1.1), (0.95, 0.95, 2.2), 0)],
    "SM_ENV_Sky_Shrub": [("box", (0, 0, 0.5), (1.4, 1.3, 1.0), 0)],
}
# Island tops collide with their own mesh (exact rims); see ZoneEnvironmentBuilder.
UNITY_COLLIDERS = {a: [{"type": "mesh", "center": [0, 0, 0], "size": [0, 0, 0], "yaw": 0.0}]
                   for a in ISLAND_ASSETS.values()}
SURFACE_ASSETS = tuple(ISLAND_ASSETS.values()) + ("SM_ENV_Sky_Cloud_Sea", "SM_ENV_Sky_Bridge")

# ------------------------------------------------------ spawns / perches
SPAWNS = [
    (HID, "ground", 0.0, 0.0, 0.2, 180), (HID, "ground", -1.4, 0.0, -1.8, 165), (HID, "ground", 1.4, 0.0, -1.6, 195),
    (HID, "ground", -1.0, 0.0, 2.2, 175), (HID, "ground", 1.1, 0.0, 2.4, 185),
    (HID, "ground", 5.4, EAST_Y, -1.9, 200), (HID, "ground", 5.1, EAST_Y, -0.2, 210),
    (HID, "ground", -5.7, SHRINE_Y, 2.9, 150),
    (HID, "air", -2.0, 2.4, -0.6, 170), (HID, "air", 2.2, 2.8, 0.8, 190), (HID, "air", 4.6, 3.0, 3.2, 200),
    (HID, "air", -5.6, 2.8, -2.6, 160),
]
# sky_low_island is perch: true. (habitat, source, x, y, z); y checked in the Blender build.
PERCHES = [
    (HID, "SM_ENV_Sky_Spire_Perch", -3.9, 2.2, -4.2),
    (HID, "SM_ENV_Sky_Spire_Perch", 6.3, EAST_Y + 2.2 * 0.8, -3.0),
    (HID, "SM_ENV_Sky_Column", -2.9, 2.88, 5.2),
    (HID, "SM_ENV_Sky_Shrine", -5.8, 3.57, 4.2),
    (HID, "SM_ENV_Sky_Runestone", 1.7, 1.52, 3.9),
    (HID, "SM_ENV_Sky_Gate", 0.0, 4.49, 6.9),
]
AREA_LINKS = [
    {"id": f"{HID}.main__east_bridge", "areaA": f"{HID}.main", "areaB": f"{HID}.east", "requires": ["walk", "fly"],
     "source": "SM_ENV_Sky_Bridge", "x": 3.55, "y": 0.45, "z": -1.0, "radius": 0.8},
    {"id": f"{HID}.main__shrine_flight", "areaA": f"{HID}.main", "areaB": f"{HID}.shrine", "requires": ["fly"],
     "source": "", "x": -4.5, "y": 1.6, "z": 3.2, "radius": 1.0},
    {"id": f"{HID}.east__shrine_flight", "areaA": f"{HID}.east", "areaB": f"{HID}.shrine", "requires": ["fly"],
     "source": "", "x": 0.0, "y": 3.0, "z": 1.5, "radius": 1.2},
]
GATES = [{
    "id": "origin_sky_low_island.gate", "worldEdgeId": "origin_sky_low_island",
    "habitatId": HID, "toZoneId": "origin", "toHabitatId": "origin_meadow",
    "requires": ["fly"], "medium": "ground", "source": "SM_ENV_Sky_Gate",
    "x": 0.0, "y": 0.0, "z": 5.6, "radius": 1.1, "lockedInG1": True,
}]

# ------------------------------------------------------------------ cover
COVER = {
    "main": {"polygon": MAIN, "y": 0.0, "inset": 0.25,
             "cells": [("grass_a", 60), ("grass_b", 30), ("flower_pink", 16), ("flower_lilac", 12), ("flower_gold", 10),
                       ("clover", 18), ("meadow_tall", 16)]},
    "east": {"polygon": EAST, "y": EAST_Y, "inset": 0.25,
             "cells": [("grass_a", 24), ("flower_blue", 10), ("clover", 8), ("meadow_tall", 10)]},
    "shrine": {"polygon": SHRINE, "y": SHRINE_Y, "inset": 0.2, "thin_centre": False,
               "cells": [("grass_b", 14), ("flower_lilac", 8), ("clover", 6)]},
}
COVER_ASSETS = {"main": "SM_ENV_Sky_Cover_Main", "east": "SM_ENV_Sky_Cover_East", "shrine": "SM_ENV_Sky_Cover_Shrine"}

# ---------------------------------------------------------- living world
ALL = ["dawn", "day", "dusk", "night"]
INTERACTION_KINDS = {
    "sniff_flowers": {"animationHint": "sniff", "feedback": "petal_puff", "sfx": "SFX_Sky_Petal_Rustle"},
    "inspect_rune": {"animationHint": "inspect", "feedback": "rune_sparkle", "sfx": "SFX_Sky_Wind_Chime"},
    "inspect_shrine": {"animationHint": "inspect", "feedback": "rune_sparkle", "sfx": "SFX_Sky_Wind_Chime"},
    "stargaze": {"animationHint": "look_up", "feedback": "fireflies_gather", "sfx": ""},
    "watch_clouds": {"animationHint": "look", "feedback": "cloud_puff", "sfx": "SFX_Sky_Cloud_Puff"},
    "ride_updraft": {"animationHint": "glide", "feedback": "cloud_puff", "sfx": "SFX_Sky_Wing_Gust"},
    "rest": {"animationHint": "rest", "feedback": "petal_puff", "sfx": "SFX_Sky_Petal_Rustle"},
}
INTERACTIONS = [
    (HID, "sniff_flowers", "ground", (-1.6, 0.0, -3.0), (-1.9, 0.2, -3.8), ["dawn", "day", "dusk"]),
    (HID, "sniff_flowers", "ground", (1.6, 0.0, -3.0), (2.0, 0.2, -3.8), ["dawn", "day", "dusk"]),
    (HID, "inspect_rune", "ground", (1.3, 0.0, 2.9), (1.7, 1.0, 3.9), ALL),
    (HID, "rest", "ground", (-2.0, 0.0, 2.3), (-3.2, 0.5, 3.4), ALL),
    (HID, "watch_clouds", "ground", (0.4, 0.0, -4.3), (0.4, -3.0, -7.5), ["dawn", "day", "dusk"]),
    (HID, "stargaze", "ground", (5.6, EAST_Y, -1.1), (5.6, 6.0, 2.0), ["night"]),
    (HID, "inspect_shrine", "ground", (-5.6, SHRINE_Y, 2.9), (-6.1, 2.6, 4.2), ALL),
    (HID, "ride_updraft", "air", (-5.4, 2.4, -3.0), (-5.4, 3.4, -3.0), ["day", "dusk"]),
    (HID, "ride_updraft", "air", (3.8, 2.6, 2.8), (3.8, 3.4, 2.8), ["dawn", "day"]),
]

PARTICLES = {
    "star_motes": {"channel": "fireflies", "center": [0.0, 2.4, 1.0], "size": [15.0, 3.0, 12.0],
                   "maxParticles": 40, "lifetime": [5.0, 9.0]},
    "shrine_motes": {"channel": "firefliesPond", "center": [-6.1, 2.4, 4.2], "size": [2.4, 1.6, 2.4],
                     "maxParticles": 12, "lifetime": [4.0, 7.0]},
    "petals": {"channel": "dust", "center": [0.0, 1.6, 0.5], "size": [15.0, 2.6, 11.0],
               "maxParticles": 40, "lifetime": [7.0, 11.0]},
    "cloud_wisps": {"channel": "pondRings", "center": [0.0, -1.6, -2.0], "size": [22.0, 1.2, 6.0],
                    "maxParticles": 10, "lifetime": [10.0, 16.0]},
}
AUDIO_LOOPS = {
    "wind": ("AMB_Sky_High_Wind_Loop", "audioWind", None),
    "birds": ("AMB_Sky_Birds_Loop", "audioBirds", None),
    "night": ("AMB_Sky_Night_Shimmer_Loop", "audioCrickets", None),
    "chimes": ("AMB_Sky_Chimes_Loop", "audioInsects", "sky_shrine"),
}
AUDIO_EMITTERS = [{"id": "sky_shrine", "x": -6.1, "y": SHRINE_Y, "z": 4.2, "innerRadius": 2.5, "outerRadius": 10.0}]
FOG = {"mode": "linear", "start": 32.0, "end": 72.0}  # ENV Alpha: haze only on the far islets
FX = []

SKY_FOG_DAY = list(SCENE["background"])


def _grade(g, k):
    n = zc.nightness(k)
    day = 1.0 - n
    g["lightColor"] = zc.mix(k["lightColor"], [1.0, 0.97, 0.92], 0.35 * day)
    # ENV Alpha (#79): owner QA saw blown-out whites. Key 1.06 -> 0.96 of Origin, fill pulled toward a cooler,
    # darker sky blue; the cloud bounce from below stays (it is what makes the island undersides readable).
    g["lightIntensity"] = k["lightIntensity"] * 0.96
    g["ambientSky"] = zc.mix(k["ambientSky"], [0.66, 0.77, 0.95], 0.35 * day)
    g["ambientEquator"] = zc.mix(k["ambientEquator"], [0.60, 0.68, 0.84], 0.4 * day)
    g["ambientGround"] = zc.mix(k["ambientGround"], [0.50, 0.50, 0.62], 0.35 * day)  # light bounced off clouds
    fog = zc.mix(k["fogColor"], SKY_FOG_DAY, 0.7 * day)
    g["fogColor"] = zc.mix(fog, [0.10, 0.12, 0.30], 0.5 * n)
    g["wind"] = min(1.6, k["wind"] * 1.35 + 0.15)
    g["fireflies"] = k["fireflies"]                      # star motes at night
    g["dust"] = 0.25 + 0.55 * day                        # drifting petals
    g["pondRings"] = 0.6 + 0.2 * day                     # cloud wisps
    g["audioBirds"] = k["audioBirds"] * 0.8
    g["audioCrickets"] = k["audioCrickets"] * 0.7        # night shimmer
    g["audioFrogs"] = 0.0
    g["audioInsects"] = 0.6                              # shrine chimes
    g["audioWind"] = 0.55 + 0.35 * k["wind"]
    g["audioPond"] = 0.0


def keyframes():
    return zc.graded_keyframes(_grade)


def write(out_dir):
    return zc.write_all(__import__(__name__), out_dir)


if __name__ == "__main__":
    import sys
    print(write(sys.argv[1] if len(sys.argv) > 1 else "."))
