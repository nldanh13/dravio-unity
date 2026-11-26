"""Origin Sanctuary living environment data (U1-ENV.3), Unity coordinates.

One JSON (Data/OriginSanctuaryLiving.json) drives the Unity runtime and the Blender
previews:
  * time of day: phases and lighting keyframes (sun / moon, ambient, fog, night glow,
    wind, fireflies, light dust, pond ripples and ambience mix weights);
  * dragon interaction points (drink, splash, bask, sniff flowers, inspect runes...);
  * particle volumes and ambience emitters.

Purely cosmetic: nothing here changes game state, and there is no weather or disaster
system. The U1-ENV.2 layout, locations and colliders (layout.py) are read but never
changed; habitat IDs are reused as they are.

    python scripts/art/environment/living.py <living.json>
"""
import json
import math
import sys

import layout

LIVING_VERSION = 1

# ------------------------------------------------------------------ time of day
DEFAULT_HOUR = 13.0          # matches the static scene lighting exactly (VIS.1 grade since Issue #80)
DAY_LENGTH_MINUTES = 24.0    # real minutes for a full simulated day (designer setting)

PHASES = [  # [start, end) in hours; night wraps over midnight
    {"id": "dawn", "start": 5.0, "end": 7.0},
    {"id": "day", "start": 7.0, "end": 17.5},
    {"id": "dusk", "start": 17.5, "end": 19.5},
    {"id": "night", "start": 19.5, "end": 5.0},
]

# VIS.1 (Issue #80) Ancient Fantasy grade: golden key light, cooler sky fill, warm bounce,
# warm haze. Same sun direction as U1-ENV.2, so shadows and readability of paths are kept.
# U1-ENV.2 values were sun (1.0, 0.92, 0.78) x1.35, sky (0.80, 0.86, 0.95),
# equator (0.66, 0.72, 0.58), ground (0.40, 0.36, 0.30), fog (0.70, 0.80, 0.66).
# The day keys (8:00, 13:00, 16:30) are also ~25 % lower in total light than U1-ENV.2, which
# clipped sunlit grass and stone to near white; the painted textures now keep their values.
DAY_SUN = {"elev": 46.0, "azim": -38.0, "color": (1.0, 0.86, 0.66), "intensity": 1.12}
DAY_AMBIENT = {"sky": (0.56, 0.66, 0.84), "equator": (0.50, 0.56, 0.48), "ground": (0.34, 0.28, 0.22)}
DAY_FOG = (0.80, 0.84, 0.70)


def K(hour, elev, azim, color, intensity, shadow, sky, equator, ground, fog, glow=0.0, floor_cut=0.0,
      wind=1.0, fireflies=0.0, dust=0.0, rings=0.5, birds=0.0, crickets=0.0, frogs=0.0, insects=0.0,
      wind_bed=0.5, pond=1.0, moon=False):
    return {
        "hour": hour, "isMoon": moon,
        "lightElevation": elev, "lightAzimuth": azim,
        "lightColor": list(color), "lightIntensity": intensity, "shadowStrength": shadow,
        "ambientSky": list(sky), "ambientEquator": list(equator), "ambientGround": list(ground),
        "fogColor": list(fog),
        "nightGlow": glow, "ambientFloorCut": floor_cut, "wind": wind,
        "fireflies": fireflies, "dust": dust, "pondRings": rings,
        "audioBirds": birds, "audioCrickets": crickets, "audioFrogs": frogs, "audioInsects": insects,
        "audioWind": wind_bed, "audioPond": pond,
    }


NIGHT_SKY, NIGHT_EQ, NIGHT_GROUND = (0.30, 0.36, 0.58), (0.20, 0.25, 0.40), (0.10, 0.10, 0.15)
NIGHT_FOG = (0.12, 0.16, 0.26)
MOON = (0.62, 0.72, 1.0)

# Light direction is Unity Euler (elevation, azimuth, 0) of the single directional
# light. The sun sets in the west (right of screen), the moon rises in the east.
# At each swap the intensity is 0, so the direction jump is never visible.
KEYFRAMES = [
    K(0.0, 48, -10, MOON, 0.42, 0.40, NIGHT_SKY, NIGHT_EQ, NIGHT_GROUND, NIGHT_FOG,
      glow=1.0, floor_cut=0.75, wind=0.55, fireflies=1.0, rings=0.25, crickets=1.0, frogs=0.8, wind_bed=0.3, moon=True),
    K(3.0, 34, -45, MOON, 0.36, 0.35, NIGHT_SKY, NIGHT_EQ, NIGHT_GROUND, NIGHT_FOG,
      glow=1.0, floor_cut=0.75, wind=0.5, fireflies=0.7, rings=0.2, crickets=0.8, frogs=0.5, wind_bed=0.3, moon=True),
    K(4.8, 12, -78, MOON, 0.10, 0.25, (0.36, 0.38, 0.56), (0.26, 0.27, 0.40), NIGHT_GROUND, (0.20, 0.20, 0.30),
      glow=0.8, floor_cut=0.6, wind=0.5, fireflies=0.4, rings=0.2, crickets=0.5, frogs=0.3, wind_bed=0.3, moon=True),
    K(5.0, 8, -86, MOON, 0.0, 0.0, (0.42, 0.40, 0.56), (0.32, 0.30, 0.42), (0.13, 0.12, 0.16), (0.28, 0.25, 0.34),
      glow=0.7, floor_cut=0.5, wind=0.5, fireflies=0.3, rings=0.2, crickets=0.4, frogs=0.2, wind_bed=0.3, moon=True),
    K(5.05, 0, 84, (1.0, 0.55, 0.45), 0.0, 0.0, (0.42, 0.40, 0.56), (0.32, 0.30, 0.42), (0.13, 0.12, 0.16), (0.28, 0.25, 0.34),
      glow=0.7, floor_cut=0.5, wind=0.5, fireflies=0.3, rings=0.2, crickets=0.4, frogs=0.2, wind_bed=0.3),
    K(6.0, 9, 74, (1.0, 0.66, 0.50), 0.62, 0.45, (0.70, 0.62, 0.72), (0.62, 0.52, 0.50), (0.30, 0.25, 0.24), (0.78, 0.62, 0.60),
      glow=0.35, floor_cut=0.2, wind=0.7, fireflies=0.1, dust=0.35, rings=0.4, birds=1.0, crickets=0.15, wind_bed=0.4),
    K(8.0, 30, 40, (1.0, 0.88, 0.74), 0.98, 0.65, (0.56, 0.60, 0.68), (0.48, 0.50, 0.42), (0.27, 0.24, 0.21), (0.72, 0.79, 0.68),
      dust=0.55, rings=0.5, birds=0.85, wind_bed=0.5),
    K(DEFAULT_HOUR, DAY_SUN["elev"], DAY_SUN["azim"], DAY_SUN["color"], DAY_SUN["intensity"], 0.75,
      DAY_AMBIENT["sky"], DAY_AMBIENT["equator"], DAY_AMBIENT["ground"], DAY_FOG,
      dust=0.45, rings=0.5, birds=0.6, insects=0.7, wind_bed=0.6),
    K(16.5, 26, -62, (1.0, 0.84, 0.62), 1.0, 0.72, (0.58, 0.58, 0.62), (0.50, 0.48, 0.38), (0.29, 0.24, 0.19), (0.78, 0.76, 0.60),
      wind=1.1, dust=0.8, rings=0.5, birds=0.6, insects=0.8, wind_bed=0.7),
    K(18.3, 7, -78, (1.0, 0.56, 0.34), 0.78, 0.55, (0.66, 0.50, 0.58), (0.68, 0.48, 0.42), (0.30, 0.22, 0.20), (0.86, 0.56, 0.46),
      glow=0.45, floor_cut=0.2, wind=0.9, fireflies=0.35, dust=0.55, rings=0.45, birds=0.3, crickets=0.35, insects=0.3, wind_bed=0.5),
    K(19.2, 2, -85, (0.85, 0.42, 0.40), 0.16, 0.30, (0.40, 0.34, 0.56), (0.34, 0.28, 0.40), (0.14, 0.12, 0.16), (0.36, 0.26, 0.36),
      glow=0.85, floor_cut=0.55, wind=0.7, fireflies=0.85, dust=0.1, rings=0.3, crickets=0.85, frogs=0.6, wind_bed=0.4),
    K(19.4, 0, -88, (0.80, 0.40, 0.42), 0.0, 0.0, (0.36, 0.34, 0.56), (0.28, 0.27, 0.42), (0.12, 0.11, 0.16), (0.24, 0.22, 0.34),
      glow=0.9, floor_cut=0.65, wind=0.65, fireflies=0.9, rings=0.3, crickets=0.9, frogs=0.7, wind_bed=0.35),
    K(19.45, 6, 72, MOON, 0.0, 0.0, (0.36, 0.34, 0.56), (0.28, 0.27, 0.42), (0.12, 0.11, 0.16), (0.24, 0.22, 0.34),
      glow=0.9, floor_cut=0.65, wind=0.65, fireflies=0.9, rings=0.3, crickets=0.9, frogs=0.7, wind_bed=0.35, moon=True),
    K(21.0, 32, 34, MOON, 0.38, 0.40, NIGHT_SKY, NIGHT_EQ, NIGHT_GROUND, NIGHT_FOG,
      glow=1.0, floor_cut=0.75, wind=0.6, fireflies=1.0, rings=0.25, crickets=1.0, frogs=0.9, wind_bed=0.3, moon=True),
]

# U1-ENV.2 day keys, kept for the Ocean / Sky zones (zone_common derives their keyframes
# from these). VIS.1 (Issue #80) regrades the Origin day only; Ocean/Sky follow later,
# after owner review.
_U1_ENV2_DAY = {
    8.0: K(8.0, 30, 40, (1.0, 0.88, 0.74), 1.18, 0.65, (0.78, 0.84, 0.94), (0.66, 0.70, 0.58), (0.38, 0.34, 0.29),
           (0.72, 0.79, 0.68), dust=0.55, rings=0.5, birds=0.85, wind_bed=0.5),
    DEFAULT_HOUR: K(DEFAULT_HOUR, 46.0, -38.0, (1.0, 0.92, 0.78), 1.35, 0.75, (0.80, 0.86, 0.95), (0.66, 0.72, 0.58),
                    (0.40, 0.36, 0.30), (0.70, 0.80, 0.66), dust=0.45, rings=0.5, birds=0.6, insects=0.7, wind_bed=0.6),
    16.5: K(16.5, 26, -62, (1.0, 0.84, 0.62), 1.2, 0.72, (0.80, 0.80, 0.86), (0.70, 0.66, 0.52), (0.40, 0.33, 0.26),
            (0.78, 0.76, 0.60), wind=1.1, dust=0.8, rings=0.5, birds=0.6, insects=0.8, wind_bed=0.7),
}
U1_ENV2_KEYFRAMES = [_U1_ENV2_DAY.get(k["hour"], k) for k in KEYFRAMES]

SCALAR_KEYS = ["lightElevation", "lightAzimuth", "lightIntensity", "shadowStrength", "nightGlow", "ambientFloorCut",
               "wind", "fireflies", "dust", "pondRings", "audioBirds", "audioCrickets", "audioFrogs", "audioInsects",
               "audioWind", "audioPond"]
COLOR_KEYS = ["lightColor", "ambientSky", "ambientEquator", "ambientGround", "fogColor"]


def wrap_hour(h):
    return h % 24.0


def phase_at(hour):
    h = wrap_hour(hour)
    for p in PHASES:
        if p["start"] <= p["end"]:
            if p["start"] <= h < p["end"]:
                return p["id"]
        elif h >= p["start"] or h < p["end"]:
            return p["id"]
    return "day"


def evaluate(hour, keys=KEYFRAMES):
    """Piecewise linear, wrapping at 24 h. Mirrors SanctuaryLivingData.EvaluateTimeOfDay."""
    h = wrap_hour(hour)
    n = len(keys)
    i = 0
    while i < n and keys[i]["hour"] <= h:
        i += 1
    a = keys[(i - 1) % n]
    b = keys[i % n]
    ha, hb = a["hour"], b["hour"]
    span = (hb - ha) % 24.0 or 24.0
    t = ((h - ha) % 24.0) / span
    out = {"hour": h, "isMoon": a["isMoon"] if t < 0.5 else b["isMoon"], "phase": phase_at(h)}
    for k in SCALAR_KEYS:
        out[k] = a[k] + (b[k] - a[k]) * t
    for k in COLOR_KEYS:
        out[k] = [a[k][c] + (b[k][c] - a[k][c]) * t for c in range(3)]
    return out


def light_forward(elev, azim):
    """Unity forward vector of a directional light with Euler(elev, azim, 0)."""
    e, a = math.radians(elev), math.radians(azim)
    return (math.sin(a) * math.cos(e), -math.sin(e), math.cos(a) * math.cos(e))


# ------------------------------------------------------- interaction points
# Places where a dragon can do a small, purely cosmetic action. Living World picks
# them; the environment only answers with particles, a ripple and a sound.
#   approach = where the dragon stands (walkable ground of its habitat)
#   target   = what it looks at / touches (yaw faces it)
# phases are preferences, never locks.
ALL = ["dawn", "day", "dusk", "night"]
INTERACTION_KINDS = {
    "drink":          {"animationHint": "drink",   "feedback": "ripple_small",    "sfx": "SFX_Drink_Lap"},
    "splash":         {"animationHint": "splash",  "feedback": "splash",          "sfx": "SFX_Splash_Small"},
    "watch_glow":     {"animationHint": "look",    "feedback": "fireflies_gather", "sfx": "SFX_Rune_Chime"},
    "inspect_rune":   {"animationHint": "inspect", "feedback": "rune_sparkle",    "sfx": "SFX_Rune_Chime"},
    "inspect_statue": {"animationHint": "inspect", "feedback": "rune_sparkle",    "sfx": "SFX_Rune_Chime"},
    "sniff_flowers":  {"animationHint": "sniff",   "feedback": "petal_puff",      "sfx": "SFX_Flower_Rustle"},
    "stargaze":       {"animationHint": "look_up", "feedback": "fireflies_gather", "sfx": ""},
    "rest":           {"animationHint": "rest",    "feedback": "none",            "sfx": "SFX_Leaves_Rustle"},
    "bask":           {"animationHint": "bask",    "feedback": "heat_motes",      "sfx": "SFX_Rock_Warm_Settle"},
    "dig":            {"animationHint": "dig",     "feedback": "dust_puff",       "sfx": "SFX_Leaves_Rustle"},
}

PB, MD, WR = "origin_pondbank", "origin_meadow", "origin_warm_rocks"
#        habitat, kind, approach (x, z), target (x, y, z), phases, needs swim
INTERACTIONS = [
    (PB, "drink", (-4.9, -2.55), (-4.9, 0.05, -1.75), ALL, False),
    (PB, "drink", (-4.6, 1.7), (-4.5, 0.05, 0.95), ALL, False),
    (PB, "splash", (-3.5, -2.2), (-3.7, 0.05, -1.1), ["dawn", "day", "dusk"], True),
    (PB, "watch_glow", (-6.3, 1.9), (-5.9, 0.3, 2.6), ["dusk", "night"], False),
    (MD, "inspect_rune", (-1.6, 3.0), (-1.9, 1.0, 4.1), ALL, False),
    (MD, "inspect_rune", (1.6, 2.9), (1.9, 1.0, 4.0), ALL, False),
    (MD, "sniff_flowers", (-1.0, -2.7), (-1.0, 0.2, -3.4), ["dawn", "day", "dusk"], False),
    (MD, "sniff_flowers", (1.5, -2.6), (1.8, 0.2, -3.3), ["dawn", "day", "dusk"], False),
    (MD, "stargaze", (0.4, 1.1), (0.4, 6.0, 4.0), ["night"], False),
    (MD, "rest", (0.3, -3.9), (0.3, 0.0, -4.9), ALL, False),
    (WR, "bask", (4.8, -0.8), (4.6, 0.44, 0.6), ["day", "dusk"], False),
    (WR, "bask", (4.8, -2.6), (5.6, 0.38, -3.4), ["day", "dusk"], False),
    (WR, "bask", (5.6, 1.7), (6.2, 1.2, 2.9), ["day", "dusk"], False),
    (WR, "dig", (3.3, 3.4), (3.4, 0.0, 2.8), ["dawn", "day", "dusk"], False),
    (WR, "inspect_statue", (5.2, 4.2), (5.7, 1.6, 5.9), ALL, False),
]


def yaw_to(ax, az, tx, tz):
    return math.degrees(math.atan2(tx - ax, tz - az))


def build_interactions():
    counts, out = {}, []
    for hid, kind, (ax, az), (tx, ty, tz), phases, swim in INTERACTIONS:
        counts[(hid, kind)] = counts.get((hid, kind), 0) + 1
        k = INTERACTION_KINDS[kind]
        out.append({
            "id": f"{hid}.interact_{kind}_{counts[(hid, kind)]:02d}",
            "habitatId": hid, "kind": kind,
            "x": ax, "y": 0.0, "z": az,
            "yaw": round(yaw_to(ax, az, tx, tz), 2),
            "targetX": tx, "targetY": ty, "targetZ": tz,
            "radius": 0.6, "capacity": 1,
            "preferredPhases": list(phases),
            "requiresSwim": swim,
            "animationHint": k["animationHint"], "feedback": k["feedback"], "sfx": k["sfx"],
        })
    return out


# --------------------------------------------------------- particles / audio
WATER_Y = 0.06
PARTICLES = {
    # Max counts are for the High tier; Medium and Low scale them down at runtime.
    "fireflies": {"center": [-1.5, 1.0, 0.0], "size": [12.0, 1.4, 9.0], "maxParticles": 36, "lifetime": [5.0, 8.0]},
    "fireflies_pond": {"center": [layout.POND_CENTER[0], 0.7, layout.POND_CENTER[1]], "size": [5.0, 0.8, 3.6],
                       "maxParticles": 14, "lifetime": [4.0, 7.0]},
    "dust": {"center": [0.0, 1.9, 0.5], "size": [15.0, 3.0, 11.0], "maxParticles": 48, "lifetime": [6.0, 10.0]},
    "pond_rings": {"center": [layout.POND_CENTER[0], WATER_Y + 0.01, layout.POND_CENTER[1]],
                   "size": [layout.SWIM_RADII[0] * 1.6, 0.0, layout.SWIM_RADII[1] * 1.6], "maxParticles": 8,
                   "lifetime": [1.6, 2.4]},
}
POND = {"center": [layout.POND_CENTER[0], WATER_Y, layout.POND_CENTER[1]],
        "radii": list(layout.POND_RADII), "swimRadii": list(layout.SWIM_RADII)}

AUDIO_LOOPS = {  # clip, weight key, emitter (null = global bed), full-volume radius, silent radius
    "birds": ("AMB_Day_Meadow_Loop", "audioBirds", None),
    "crickets": ("AMB_Night_Crickets_Loop", "audioCrickets", None),
    "wind": ("AMB_Wind_Loop", "audioWind", None),
    "pond": ("AMB_Water_Pond_Loop", "audioPond", "origin_pondbank"),
    "frogs": ("AMB_Night_Frogs_Loop", "audioFrogs", "origin_pondbank"),
    "insects": ("AMB_Warm_Rocks_Insects_Loop", "audioInsects", "origin_warm_rocks"),
}
AUDIO_EMITTERS = [
    {"id": "origin_pondbank", "x": layout.POND_CENTER[0], "y": 0.0, "z": layout.POND_CENTER[1], "innerRadius": 3.0, "outerRadius": 11.0},
    {"id": "origin_warm_rocks", "x": 5.0, "y": 0.0, "z": 0.2, "innerRadius": 2.5, "outerRadius": 10.0},
]
ONE_SHOTS = sorted({k["sfx"] for k in INTERACTION_KINDS.values() if k["sfx"]})


def build_living():
    return {
        "version": LIVING_VERSION,
        "zoneId": layout.ZONE_ID,
        "layoutVersion": layout.LAYOUT_VERSION,
        "defaultHour": DEFAULT_HOUR,
        "dayLengthMinutes": DAY_LENGTH_MINUTES,
        "phases": PHASES,
        "keyframes": KEYFRAMES,
        "interactionPoints": build_interactions(),
        "pond": POND,
        "particleVolumes": [dict(id=k, **v) for k, v in PARTICLES.items()],
        "audioLoops": [{"id": k, "clip": c, "weightKey": w, "emitterId": e or ""} for k, (c, w, e) in AUDIO_LOOPS.items()],
        "audioEmitters": AUDIO_EMITTERS,
        "oneShots": ONE_SHOTS,
    }


def write(path):
    data = build_living()
    with open(path, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=1)
    return data


if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else "OriginSanctuaryLiving.json"
    d = write(out)
    print("interaction points:", len(d["interactionPoints"]), "keyframes:", len(d["keyframes"]), "->", out)
