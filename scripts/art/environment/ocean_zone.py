"""Thủy Vực (Ocean) environment kit data (U1-ENV.4), Unity coordinates.

Sunken ancient ruins, coral and light falling through the water, framed by the
same fixed landscape camera as the Origin Sanctuary. Three canonical microhabitats
(canonical_world.py, from G0BiologyData):

  ocean_estuary    mild, brackish, shallow, vegetation 85  (left: seagrass flats,
                   the river mouth and the arch back to origin_pondbank)
  ocean_reef       warm, marine, shallow, vegetation 70    (centre: coral and ruins)
  ocean_cool_pool  cool, marine, shallow, vegetation 45    (right: crystals, glow)

All three are Submerged and perch: false, so there are NO perch points here. Dragons
rest on the seabed instead (restPoints). Every habitat is one swim volume above a
standable seabed (y = 0). The WorldGraph edge origin_ocean_estuary (requires Swim)
is marked as a gate in ocean_estuary; nothing here unlocks the zone.
"""
import zone_common as zc

ZONE_ID = "ocean"
FILE_PREFIX = "Ocean"
LAYOUT_VERSION = 1
SEED = 7401
DISPLAY_VI, DISPLAY_EN = "Thủy Vực", "Ocean Realm"
SCENE = {"name": "Ocean_Environment", "displayNameVi": DISPLAY_VI, "displayNameEn": DISPLAY_EN,
         "background": [0.16, 0.46, 0.52]}
FLAT_RECT = (-8.6, -7.0, 8.6, 7.4)
TOP_Z = 5.8

# ------------------------------------------------------------------ habitats
B_EST_REEF = [(-2.3, -5.4), (-2.6, -1.4), (-2.2, 2.2), (-2.7, TOP_Z)]
B_REEF_COOL = [(2.4, -5.4), (2.8, -1.0), (2.4, 2.4), (2.9, TOP_Z)]
PLAY_X = 7.0

HABITATS = [
    {"id": "ocean_estuary", "displayNameVi": "Cửa sông Thủy Vực", "displayNameEn": "Ocean Estuary",
     "polygon": [(-PLAY_X, -5.4)] + B_EST_REEF + [(-PLAY_X, TOP_Z)]},
    {"id": "ocean_reef", "displayNameVi": "Rạn san hô Thủy Vực", "displayNameEn": "Ocean Reef",
     "polygon": B_REEF_COOL + list(reversed(B_EST_REEF))},
    {"id": "ocean_cool_pool", "displayNameVi": "Vực mát Thủy Vực", "displayNameEn": "Ocean Cool Pool",
     "polygon": [(PLAY_X, -5.4), (PLAY_X, TOP_Z)] + list(reversed(B_REEF_COOL))},
]
_POLY = {h["id"]: h["polygon"] for h in HABITATS}

SWIM_Y = (0.3, 3.0)
VOLUMES = [{"id": f"{h['id']}.swim", "habitatId": h["id"], "medium": "swim", "polygon": h["polygon"],
            "yMin": SWIM_Y[0], "yMax": SWIM_Y[1]} for h in HABITATS]
WALK_AREAS = [{"id": f"{h['id']}.seabed", "habitatId": h["id"], "displayNameVi": "Đáy cát",
               "polygon": h["polygon"], "y": 0.0} for h in HABITATS]


# ------------------------------------------------------------------- props
def hero_props():
    P = zc.P
    es, rf, cp = "ocean_estuary", "ocean_reef", "ocean_cool_pool"
    return [
        P("SM_ENV_Ocean_Seabed", 0, 0),
        # Sunken ruins along the back: the estuary arch is the way home (origin_pondbank).
        P("SM_ENV_Ocean_Arch_Estuary", -5.0, 6.9, s=0.75, habitat=es),
        P("SM_ENV_Ocean_Wall_Ruin", -1.6, 7.3, yaw=5, s=0.9),
        P("SM_ENV_Ocean_Statue_Dragon", 1.4, 7.0, yaw=-12, s=0.95),
        P("SM_ENV_Ocean_Column_Sunken", 3.9, 6.6, yaw=-20, s=0.85),
        P("SM_ENV_Ocean_Column_Sunken", -2.9, 6.5, yaw=30, s=0.75),
        P("SM_ENV_Ocean_Rock_Reef_A", 6.1, 6.9, yaw=20, s=1.2),
        P("SM_ENV_Ocean_Kelp", -0.3, 8.7, s=1.15),
        P("SM_ENV_Ocean_Kelp", 3.0, 9.0, yaw=40, s=1.25),
        P("SM_ENV_Ocean_Kelp", -3.9, 8.5, yaw=-30, s=1.05),
        P("SM_ENV_Ocean_Kelp", 6.9, 8.3, yaw=70, s=1.1),
        # ocean_estuary (left).
        P("SM_ENV_Ocean_Rock_Reef_B", -6.3, 1.9, yaw=40, habitat=es),
        P("SM_ENV_Ocean_Shell_Cluster", -5.9, -3.9, yaw=10, habitat=es),
        P("SM_ENV_Ocean_Shell_Cluster", -3.4, 3.6, yaw=200, s=0.8, habitat=es),
        P("SM_ENV_Ocean_Kelp", -6.6, 4.4, s=0.85, habitat=es),
        P("SM_ENV_Ocean_Sea_Fan", -3.1, -4.7, yaw=15, s=0.8, habitat=es),
        # ocean_reef (centre).
        P("SM_ENV_Ocean_Coral_Brain", -1.2, 3.1, yaw=30, habitat=rf),
        P("SM_ENV_Ocean_Coral_Branch", 1.3, 3.5, habitat=rf),
        P("SM_ENV_Ocean_Coral_Table", 0.5, -3.9, s=0.9, habitat=rf),
        P("SM_ENV_Ocean_Coral_Branch", -1.3, -4.4, yaw=70, s=0.75, habitat=rf),
        P("SM_ENV_Ocean_Sea_Fan", 1.9, 4.7, yaw=-10, habitat=rf),
        P("SM_ENV_Ocean_Column_Fallen", -0.3, 5.0, yaw=8, s=0.75, habitat=rf),
        # ocean_cool_pool (right).
        P("SM_ENV_Ocean_Crystal_Cool", 5.7, 3.3, yaw=15, habitat=cp),
        P("SM_ENV_Ocean_Crystal_Cool", 3.7, -4.3, yaw=120, s=0.7, habitat=cp),
        P("SM_ENV_Ocean_Anemone_Glow", 4.3, 2.1, habitat=cp),
        P("SM_ENV_Ocean_Anemone_Glow", 6.3, -1.4, yaw=40, s=0.8, habitat=cp),
        P("SM_ENV_Ocean_Rock_Reef_A", 5.4, -3.9, yaw=60, s=0.85, habitat=cp),
        P("SM_ENV_Ocean_Vent_Rock", 3.5, 0.9, s=0.9, habitat=cp),
        # Wide-screen and under-UI dressing (never part of a habitat).
        P("SM_ENV_Ocean_Rock_Reef_A", -9.3, -3.6, yaw=10, s=1.1),
        P("SM_ENV_Ocean_Coral_Table", -9.0, 2.4, yaw=40, s=1.1),
        P("SM_ENV_Ocean_Kelp", -9.8, 4.8, s=1.25),
        P("SM_ENV_Ocean_Sea_Fan", -8.4, -0.8, yaw=80),
        P("SM_ENV_Ocean_Coral_Brain", -8.7, -5.8, yaw=120, s=0.9),
        P("SM_ENV_Ocean_Rock_Reef_B", 9.0, -2.2, yaw=200),
        P("SM_ENV_Ocean_Coral_Branch", 9.3, 2.9, yaw=150, s=1.1),
        P("SM_ENV_Ocean_Kelp", 10.2, 0.6, yaw=20, s=1.2),
        P("SM_ENV_Ocean_Sea_Fan", 8.7, 5.4, yaw=-30),
        P("SM_ENV_Ocean_Crystal_Cool", 10.6, -4.8, yaw=200, s=0.9),
        P("SM_ENV_Ocean_Rock_Reef_A", 11.6, 4.2, yaw=300, s=1.3),
        # Baked seabed cover, one renderer per habitat / edge band.
        P("SM_ENV_Ocean_Cover_Estuary", 0, 0, habitat=es),
        P("SM_ENV_Ocean_Cover_Reef", 0, 0, habitat=rf),
        P("SM_ENV_Ocean_Cover_Cool_Pool", 0, 0, habitat=cp),
        P("SM_ENV_Ocean_Cover_Edges", 0, 0),
    ]


# Colliders in BLENDER object space (see layout.BLENDER_COLLIDERS). Reused builders keep
# the Origin boxes; kelp, fans, seagrass and shells are soft (no collider).
BLENDER_COLLIDERS = {
    "SM_ENV_Ocean_Arch_Estuary": [("box", (-2.05, 0, 2.3), (1.25, 1.15, 4.6), 0), ("box", (2.05, 0, 2.3), (1.25, 1.15, 4.6), 0),
                                  ("box", (0, 0, 5.3), (5.3, 0.9, 1.6), 0), ("box", (2.9, 1.2, 0.3), (0.8, 0.75, 0.6), 0.5)],
    "SM_ENV_Ocean_Column_Sunken": [("box", (0, 0, 1.1), (1.1, 1.1, 2.2), 0.2)],
    "SM_ENV_Ocean_Column_Fallen": [("box", (0.3, 0, 0.3), (4.0, 0.95, 0.6), 0)],
    "SM_ENV_Ocean_Wall_Ruin": [("box", (0, 0, 1.2), (3.4, 0.7, 2.4), 0)],
    "SM_ENV_Ocean_Statue_Dragon": [("box", (0, 0, 1.6), (1.8, 1.6, 3.2), 0)],
    "SM_ENV_Ocean_Rock_Reef_A": [("box", (0, 0, 0.5), (1.9, 1.6, 1.0), 0)],
    "SM_ENV_Ocean_Rock_Reef_B": [("box", (0, 0, 0.35), (1.3, 1.1, 0.7), 0)],
    "SM_ENV_Ocean_Coral_Brain": [("box", (0, 0, 0.4), (1.45, 1.35, 0.8), 0)],
    "SM_ENV_Ocean_Coral_Branch": [("box", (0, 0, 0.6), (0.9, 0.9, 1.2), 0)],
    "SM_ENV_Ocean_Coral_Table": [("box", (0, 0, 0.45), (0.45, 0.45, 0.9), 0), ("box", (0, 0, 0.98), (1.7, 1.6, 0.12), 0)],
    "SM_ENV_Ocean_Crystal_Cool": [("box", (0, 0, 0.7), (1.1, 0.9, 1.4), 0)],
    "SM_ENV_Ocean_Anemone_Glow": [("box", (0, 0, 0.25), (0.85, 0.85, 0.5), 0)],
    "SM_ENV_Ocean_Vent_Rock": [("box", (0, 0, 0.4), (1.2, 1.1, 0.8), 0)],
}
# Thin box under the playable seabed (Unity local).
UNITY_COLLIDERS = {"SM_ENV_Ocean_Seabed": [{"type": "box", "center": [0.0, -0.25, 0.0], "size": [18.0, 0.5, 15.0], "yaw": 0.0}]}
SURFACE_ASSETS = ("SM_ENV_Ocean_Seabed",)

# --------------------------------------------------------- spawns / gates
ES, RF, CP = "ocean_estuary", "ocean_reef", "ocean_cool_pool"
# (habitat, medium, x, y, z, yaw). The estuary species walk and swim (droplet,
# steamdrake, mossback, gaia), so it has seabed spawns too; reef (leviathan) and
# cool pool (frostfin) species only swim.
SPAWNS = [
    (ES, "seabed", -5.2, 0.0, -1.8, 160), (ES, "seabed", -4.0, 0.0, 1.2, 190),
    (ES, "swim", -5.6, 1.3, 2.9, 170), (ES, "swim", -3.6, 0.9, -3.3, 200), (ES, "swim", -6.0, 1.7, -0.4, 150),
    (RF, "swim", 0.0, 1.2, 0.2, 180), (RF, "swim", -1.0, 0.9, -1.6, 170), (RF, "swim", 1.2, 1.4, -1.2, 190),
    (RF, "swim", -0.6, 1.8, 1.6, 175), (RF, "swim", 0.9, 1.0, 1.9, 185),
    (CP, "swim", 4.6, 1.2, 0.2, 200), (CP, "swim", 5.9, 1.6, 1.4, 210), (CP, "swim", 4.0, 0.9, -1.8, 190),
    (CP, "swim", 6.0, 1.3, -2.6, 205), (CP, "swim", 4.1, 1.6, 3.4, 195),
]
# Mid-water triggers on the shared borders.
TRANSITIONS = [
    (ES, RF, (-2.45, 1.0, -3.0)),
    (ES, RF, (-2.35, 1.0, 1.6)),
    (RF, CP, (2.65, 1.0, -2.4)),
    (RF, CP, (2.5, 1.0, 1.4)),
]
# Ocean habitats are perch: false (canonical). Seabed rest spots instead:
# (habitat, source asset, x, y, z). y = surface height, checked in the Blender build.
REST_POINTS = [
    (ES, "SM_ENV_Ocean_Rock_Reef_B", -6.3, 0.64, 1.9),
    (ES, "SM_ENV_Ocean_Seabed", -4.6, 0.0, -3.6),
    (RF, "SM_ENV_Ocean_Coral_Table", 0.5, 0.94, -3.9),
    (RF, "SM_ENV_Ocean_Column_Fallen", -0.3, 0.5, 5.0),
    (CP, "SM_ENV_Ocean_Rock_Reef_A", 5.4, 0.69, -3.9),
    (CP, "SM_ENV_Ocean_Seabed", 5.0, 0.0, -0.8),
]
GATES = [{
    "id": "origin_ocean_estuary.gate", "worldEdgeId": "origin_ocean_estuary",
    "habitatId": ES, "toZoneId": "origin", "toHabitatId": "origin_pondbank",
    "requires": ["swim"], "medium": "swim", "source": "SM_ENV_Ocean_Arch_Estuary",
    "x": -5.0, "y": 1.0, "z": 5.2, "radius": 1.1, "lockedInG1": True,
}]

# ------------------------------------------------------------------ cover
_OUTSIDE = [h["polygon"] for h in HABITATS]
COVER = {
    ES: {"polygon": _POLY[ES], "cells": [("grass_a", 70), ("grass_b", 40), ("meadow_tall", 26), ("clover", 18),
                                          ("dry_grass", 14)]},
    RF: {"polygon": _POLY[RF], "cells": [("shrub_leaf", 18), ("flower_pink", 14), ("flower_gold", 14),
                                          ("twig_dry", 12), ("flower_lilac", 10), ("grass_b", 16)]},
    CP: {"polygon": _POLY[CP], "cells": [("flower_blue", 14), ("clover", 10), ("grass_b", 14), ("cattail", 8)]},
    "edges": {"polygon": [(-14.5, -9.5), (15.5, -9.5), (15.5, 12.0), (-14.5, 12.0)], "outside": _OUTSIDE,
              "thin_centre": False,
              "cells": [("grass_a", 50), ("dry_grass", 30), ("shrub_leaf", 16), ("twig_dry", 14), ("meadow_tall", 26),
                        ("flower_pink", 8)]},
}
COVER_ASSETS = {ES: "SM_ENV_Ocean_Cover_Estuary", RF: "SM_ENV_Ocean_Cover_Reef", CP: "SM_ENV_Ocean_Cover_Cool_Pool",
                "edges": "SM_ENV_Ocean_Cover_Edges"}

# ---------------------------------------------------------- living world
ALL = ["dawn", "day", "dusk", "night"]
INTERACTION_KINDS = {
    "forage_seagrass": {"animationHint": "forage", "feedback": "dust_puff", "sfx": "SFX_Ocean_Sand_Puff"},
    "inspect_coral": {"animationHint": "inspect", "feedback": "bubbles", "sfx": "SFX_Ocean_Coral_Tick"},
    "inspect_ruins": {"animationHint": "inspect", "feedback": "rune_sparkle", "sfx": "SFX_Ocean_Rune_Chime"},
    "inspect_statue": {"animationHint": "inspect", "feedback": "rune_sparkle", "sfx": "SFX_Ocean_Rune_Chime"},
    "watch_glow": {"animationHint": "look", "feedback": "fireflies_gather", "sfx": "SFX_Ocean_Glow_Shimmer"},
    "bubble_play": {"animationHint": "play", "feedback": "bubbles", "sfx": "SFX_Ocean_Bubbles"},
    "rest": {"animationHint": "rest", "feedback": "dust_puff", "sfx": "SFX_Ocean_Sand_Puff"},
    "drift": {"animationHint": "drift", "feedback": "none", "sfx": ""},
}
# habitat, kind, medium, approach (x, y, z), target (x, y, z), preferred phases
INTERACTIONS = [
    (ES, "forage_seagrass", "seabed", (-5.0, 0.0, -0.5), (-5.4, 0.1, -1.1), ALL),
    (ES, "forage_seagrass", "seabed", (-4.4, 0.0, 2.6), (-4.8, 0.1, 3.2), ALL),
    (ES, "inspect_ruins", "swim", (-5.0, 1.6, 4.4), (-5.0, 3.0, 6.6), ALL),
    (ES, "rest", "seabed", (-4.0, 0.0, -2.4), (-4.6, 0.0, -3.6), ["dusk", "night"]),
    (RF, "inspect_coral", "swim", (-0.9, 1.4, 1.9), (-1.2, 0.6, 3.1), ALL),
    (RF, "inspect_coral", "swim", (1.0, 1.7, 2.4), (1.3, 1.0, 3.5), ALL),
    (RF, "inspect_coral", "swim", (0.5, 1.6, -2.7), (0.5, 1.0, -3.9), ALL),
    (RF, "inspect_statue", "swim", (1.3, 2.1, 5.0), (1.4, 2.4, 7.0), ALL),
    (RF, "drift", "swim", (-0.2, 2.4, -0.4), (0.6, 2.4, 0.4), ["dawn", "day", "dusk"]),
    (CP, "watch_glow", "swim", (4.6, 1.2, 1.1), (4.3, 0.5, 2.1), ["dusk", "night"]),
    (CP, "watch_glow", "swim", (5.2, 1.6, 2.2), (5.7, 1.0, 3.3), ["dusk", "night", "dawn"]),
    (CP, "bubble_play", "swim", (3.7, 1.5, -0.2), (3.5, 1.2, 0.9), ALL),
    (CP, "rest", "seabed", (5.0, 0.0, -2.3), (5.4, 0.69, -3.9), ["dusk", "night"]),
]

VENT = (3.5, 0.75, 0.9)
PARTICLES = {
    # channel = which EnvironmentAmbientParticles slot / time-of-day weight drives it.
    "plankton_glow": {"channel": "fireflies", "center": [0.0, 1.4, 0.3], "size": [14.0, 2.4, 11.0],
                      "maxParticles": 40, "lifetime": [5.0, 9.0]},
    "anemone_glow": {"channel": "firefliesPond", "center": [5.0, 0.8, 0.8], "size": [4.0, 1.2, 6.0],
                     "maxParticles": 16, "lifetime": [4.0, 7.0]},
    "marine_snow": {"channel": "dust", "center": [0.0, 2.0, 0.5], "size": [16.0, 3.4, 12.0],
                    "maxParticles": 56, "lifetime": [8.0, 12.0]},
    "vent_bubbles": {"channel": "pondRings", "center": list(VENT), "size": [0.35, 0.05, 0.35],
                     "maxParticles": 14, "lifetime": [2.6, 3.6]},
}
AUDIO_LOOPS = {
    "bed": ("AMB_Ocean_Underwater_Bed_Loop", "audioPond", None),
    "current": ("AMB_Ocean_Current_Loop", "audioWind", None),
    "reef": ("AMB_Ocean_Reef_Crackle_Loop", "audioBirds", RF),
    "deep": ("AMB_Ocean_Deep_Song_Loop", "audioCrickets", CP),
    "vent": ("AMB_Ocean_Vent_Bubbles_Loop", "audioInsects", "ocean_vent"),
    "estuary": ("AMB_Ocean_Estuary_Flow_Loop", "audioFrogs", ES),
}
AUDIO_EMITTERS = [
    {"id": ES, "x": -5.0, "y": 1.0, "z": 0.5, "innerRadius": 3.0, "outerRadius": 11.0},
    {"id": RF, "x": 0.0, "y": 1.0, "z": 1.5, "innerRadius": 3.0, "outerRadius": 11.0},
    {"id": CP, "x": 5.0, "y": 1.0, "z": 0.5, "innerRadius": 3.0, "outerRadius": 11.0},
    {"id": "ocean_vent", "x": VENT[0], "y": VENT[1], "z": VENT[2], "innerRadius": 1.5, "outerRadius": 7.0},
]
FOG = {"mode": "linear", "start": 18.0, "end": 48.0}
# Cosmetic underwater light: sun shafts from the surface and caustics on the seabed.
# Both fade with the light intensity of the time of day (EnvironmentScrollingFx).
FX = [
    {"id": "caustics", "kind": "caustics", "texture": "T_ENV_FX_Caustics", "x": 0.0, "y": 0.03, "z": 0.4,
     "width": 22.0, "height": 17.0, "yaw": 0.0, "tilt": 90.0, "alpha": 0.22, "scroll": [0.018, 0.011], "tiling": 5.0},
] + [
    {"id": f"light_shaft_{i:02d}", "kind": "light_shaft", "texture": "T_ENV_FX_LightShaft", "x": x, "y": 2.6, "z": z,
     "width": w, "height": 6.5, "yaw": 0.0, "tilt": -18.0, "alpha": a, "scroll": [0.0, 0.0], "tiling": 1.0}
    for i, (x, z, w, a) in enumerate([(-5.6, 1.0, 2.8, 0.26), (-3.9, -1.6, 2.2, 0.22), (-1.6, 2.2, 2.6, 0.2),
                                       (1.2, -0.8, 2.1, 0.17), (3.6, 2.8, 2.0, 0.13), (6.0, 0.4, 1.8, 0.1)], 1)
]

UNDER_SKY_DAY, UNDER_SKY_NIGHT = [0.42, 0.74, 0.80], [0.10, 0.20, 0.36]
UNDER_EQ_DAY, UNDER_EQ_NIGHT = [0.26, 0.52, 0.58], [0.06, 0.14, 0.26]
UNDER_GR_DAY, UNDER_GR_NIGHT = [0.20, 0.30, 0.32], [0.03, 0.06, 0.12]
UNDER_FOG_DAY, UNDER_FOG_NIGHT = list(SCENE["background"]), [0.03, 0.09, 0.17]


def _grade(g, k):
    n = zc.nightness(k)
    day = 1.0 - n
    g["lightColor"] = zc.mix(k["lightColor"], [0.62, 0.92, 1.0], 0.45)
    g["lightIntensity"] = k["lightIntensity"] * 0.78
    g["shadowStrength"] = k["shadowStrength"] * 0.6
    g["ambientSky"] = zc.mix(zc.mix(UNDER_SKY_DAY, UNDER_SKY_NIGHT, n), k["ambientSky"], 0.15)
    g["ambientEquator"] = zc.mix(zc.mix(UNDER_EQ_DAY, UNDER_EQ_NIGHT, n), k["ambientEquator"], 0.2)
    g["ambientGround"] = zc.mix(UNDER_GR_DAY, UNDER_GR_NIGHT, n)
    g["fogColor"] = zc.mix(zc.mix(UNDER_FOG_DAY, UNDER_FOG_NIGHT, n), k["fogColor"], 0.12)
    g["nightGlow"] = max(0.25, k["nightGlow"])          # crystals and anemones always glow a little
    g["ambientFloorCut"] = k["ambientFloorCut"] * 0.8
    g["wind"] = 0.45 + 0.35 * k["wind"]                  # current sway of kelp and seagrass
    g["fireflies"] = max(0.12, k["fireflies"])           # bioluminescent plankton
    g["dust"] = 0.55 + 0.25 * day                        # marine snow
    g["pondRings"] = 0.7                                 # vent bubbles
    g["audioBirds"] = 0.35 + 0.55 * day                  # reef crackle
    g["audioCrickets"] = 0.2 + 0.6 * n                   # deep song
    g["audioFrogs"] = 0.7                                # estuary inflow
    g["audioInsects"] = 0.8                              # vent bubbles
    g["audioWind"] = 0.5 + 0.2 * k["wind"]               # current
    g["audioPond"] = 1.0                                 # underwater bed


def keyframes():
    return zc.graded_keyframes(_grade)


def write(out_dir):
    return zc.write_all(__import__(__name__), out_dir)


if __name__ == "__main__":
    import sys
    print(write(sys.argv[1] if len(sys.argv) > 1 else "."))
