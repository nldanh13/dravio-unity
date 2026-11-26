"""Shared builders for the zone environment kits (U1-ENV.4: Ocean and Sky).

A zone module (ocean_zone.py, sky_zone.py) only declares data: habitats, placements,
colliders, volumes, spawns, gates, interactions and its time-of-day grading. This
module turns that into the same JSON files the Origin Sanctuary uses
(<Zone>Layout / Locations / Colliders / Living), plus the U1-ENV.4 additions:

  volumes      swim / fly spaces: XZ polygon + yMin..yMax (Unity metres)
  walkAreas    standable surfaces with their height (sky islands, the seabed)
  restPoints   seabed resting spots (Ocean is perch: false in the canonical data)
  perchPoints  perches, only where the canonical habitat has perch: true
  areaLinks    moves between walk areas of one habitat (bridge, flight hops)
  zoneGates    WorldGraph edge gateways (marked only, never unlocked here)

Camera, Safe Area and Fantasy UI framing are the Origin ones (layout.py, framing.py),
so the same SanctuaryCameraFit works unchanged in every zone scene.
"""
import json
import math
import random

import canonical_world as cw
import layout as origin
import living as origin_living

# -------------------------------------------------------------- geometry
point_in_poly = origin.point_in_poly
dist_to_poly_edge = origin.dist_to_poly_edge
poly_area = origin.poly_area
poly_centroid = origin.poly_centroid
ellipse = origin.ellipse


def P(asset, x, z, yaw=0.0, s=1.0, y=0.0, habitat=""):
    return origin.P(asset, x, z, yaw=yaw, s=s, y=y, habitat=habitat)


def flat(poly):
    return [round(c, 3) for pt in poly for c in pt]


def collider_specs_unity(zone):
    """Blender-space boxes -> Unity local boxes (same convention as layout.py)."""
    out = {}
    for asset, boxes in zone.BLENDER_COLLIDERS.items():
        specs = []
        for kind, (bx, by, bz), (sx, sy, sz), yaw in boxes:
            specs.append({"type": kind, "center": [round(-bx, 3), round(bz, 3), round(-by, 3)],
                          "size": [sx, sz, sy], "yaw": round(-math.degrees(yaw), 2)})
        out[asset] = specs
    for asset, boxes in getattr(zone, "UNITY_COLLIDERS", {}).items():
        out[asset] = [dict(b) for b in boxes]
    return out


def obstacle_polygons(zone, placements=None):
    """World XZ footprints of every collider that rises above its walk surface."""
    placements = placements if placements is not None else zone.hero_props()
    specs = collider_specs_unity(zone)
    skip = set(getattr(zone, "SURFACE_ASSETS", ()))
    out = []
    for p in placements:
        if p["asset"] in skip:
            continue
        for s in specs.get(p["asset"], []):
            cx, cz = s["center"][0] * p["scale"], s["center"][2] * p["scale"]
            hx, hz = s["size"][0] * p["scale"] / 2, s["size"][2] * p["scale"] / 2
            pts = []
            for ox, oz in ((-hx, -hz), (hx, -hz), (hx, hz), (-hx, hz)):
                lx, lz = origin._rot_y(ox, oz, s["yaw"])
                wx, wz = origin._rot_y(cx + lx, cz + lz, p["yaw"])
                pts.append((round(p["x"] + wx, 3), round(p["z"] + wz, 3)))
            top = p["y"] + (s["center"][1] + s["size"][1] / 2) * p["scale"]
            out.append({"source": p["asset"], "polygon": pts, "top": round(top, 3)})
    return out


def habitat(zone, hid):
    return next(h for h in zone.HABITATS if h["id"] == hid)


def blocked(x, z, obstacles, clearance=0.0, y=None):
    """Inside / near a footprint. With y, only colliders reaching that height block."""
    for o in obstacles:
        if y is not None and o["top"] < y - 0.05:
            continue
        if point_in_poly(x, z, o["polygon"]) or dist_to_poly_edge(x, z, o["polygon"]) < clearance:
            return True
    return False


def walk_area_at(zone, x, z, hid=None):
    for a in zone.WALK_AREAS:
        if (hid is None or a["habitatId"] == hid) and point_in_poly(x, z, a["polygon"]):
            return a
    return None


def walkable(zone, x, z, hid, obstacles, clearance=0.45):
    if not point_in_poly(x, z, habitat(zone, hid)["polygon"]):
        return False
    a = walk_area_at(zone, x, z, hid)
    if a is None or dist_to_poly_edge(x, z, a["polygon"]) < clearance * 0.6:
        return False
    return not blocked(x, z, obstacles, clearance)


def in_volume(zone, x, y, z, medium, hid=None, margin=0.0):
    for v in zone.VOLUMES:
        if v["medium"] != medium or (hid is not None and v["habitatId"] != hid):
            continue
        if v["yMin"] + margin <= y <= v["yMax"] - margin and point_in_poly(x, z, v["polygon"]):
            return True
    return False


def point_valid(zone, medium, x, y, z, hid, obstacles):
    """Single rule used by spawns, interactions and tests."""
    if medium in ("ground", "seabed"):
        a = walk_area_at(zone, x, z, hid)
        return a is not None and abs(a["y"] - y) < 1e-3 and walkable(zone, x, z, hid, obstacles)
    if medium in ("swim", "air"):
        vol = "swim" if medium == "swim" else "fly"
        return in_volume(zone, x, y, z, vol, hid, margin=0.2) and not blocked(x, z, obstacles, 0.35, y=y)
    raise ValueError(medium)


# --------------------------------------------------------------- locations
def build_locations(zone):
    placements = zone.hero_props()
    obstacles = obstacle_polygons(zone, placements)
    habitats = []
    for h in zone.HABITATS:
        poly = h["polygon"]
        swim = [v for v in zone.VOLUMES if v["habitatId"] == h["id"] and v["medium"] == "swim"]
        habitats.append({
            "id": h["id"], "zoneId": zone.ZONE_ID,
            "displayNameVi": h["displayNameVi"], "displayNameEn": h["displayNameEn"],
            "defaultUnlocked": False,  # Ocean / Sky stay locked in G1 (domain decides, not art)
            "requiresDiscovery": h.get("requiresDiscovery", ""),
            "polygonXZ": flat(poly),
            "centerXZ": [round(c, 3) for c in poly_centroid(poly)],
            "areaM2": round(poly_area(poly), 2),
            "swimPolygonXZ": flat(swim[0]["polygon"]) if swim else [],
        })
    counts = {}
    spawns = []
    for hid, medium, x, y, z, yaw in zone.SPAWNS:
        counts[hid] = counts.get(hid, 0) + 1
        spawns.append({"id": f"{hid}.spawn_{counts[hid]:02d}", "habitatId": hid, "kind": "dragon",
                       "medium": medium, "x": x, "y": y, "z": z, "yaw": float(yaw)})
    transitions = []
    for i, (a, b, (x, y, z)) in enumerate(getattr(zone, "TRANSITIONS", []), 1):
        transitions.append({"id": f"{a}__{b}_{i:02d}", "habitatA": a, "habitatB": b,
                            "x": x, "y": y, "z": z, "radius": 0.9})
    perches = [{"id": f"{hid}.perch_{i:02d}", "habitatId": hid, "source": src, "x": x, "y": y, "z": z}
               for i, (hid, src, x, y, z) in enumerate(getattr(zone, "PERCHES", []), 1)]
    rests = [{"id": f"{hid}.rest_{i:02d}", "habitatId": hid, "source": src, "x": x, "y": y, "z": z}
             for i, (hid, src, x, y, z) in enumerate(getattr(zone, "REST_POINTS", []), 1)]
    volumes = [{"id": v["id"], "habitatId": v["habitatId"], "medium": v["medium"],
                "polygonXZ": flat(v["polygon"]), "yMin": v["yMin"], "yMax": v["yMax"]} for v in zone.VOLUMES]
    walk = [{"id": a["id"], "habitatId": a["habitatId"], "displayNameVi": a.get("displayNameVi", ""),
             "polygonXZ": flat(a["polygon"]), "y": a["y"]} for a in zone.WALK_AREAS]
    links = [dict(l) for l in getattr(zone, "AREA_LINKS", [])]
    gates = [dict(g) for g in zone.GATES]
    return {
        "version": zone.LAYOUT_VERSION,
        "zoneId": zone.ZONE_ID,
        "flatRectXZ": list(zone.FLAT_RECT),
        "walkClearance": 0.45,
        "habitats": habitats,
        "spawnPoints": spawns,
        "transitions": transitions,
        "perchPoints": perches,
        "waterAreas": [],
        "obstacles": [{"source": o["source"], "polygonXZ": flat(o["polygon"]), "top": o["top"]} for o in obstacles],
        "volumes": volumes,
        "walkAreas": walk,
        "restPoints": rests,
        "areaLinks": links,
        "zoneGates": gates,
    }


# ------------------------------------------------------------------ scatter
def scatter_cover(zone):
    """{chunk: [(cell, x, y, z, yaw, scale)]}; chunk = habitat / area id or 'edges'."""
    rng = random.Random(zone.SEED)
    placements = zone.hero_props()
    obstacles = obstacle_polygons(zone, placements)
    keep_free = [(x, z, 0.55) for _, m, x, y, z, _ in zone.SPAWNS if m in ("ground", "seabed")]
    keep_free += [(g["x"], g["z"], 0.9) for g in zone.GATES]
    out = {}
    for chunk, spec in zone.COVER.items():
        pts = []
        poly, y, outside = spec["polygon"], spec.get("y", 0.0), spec.get("outside", [])
        x0 = min(p[0] for p in poly)
        x1 = max(p[0] for p in poly)
        z0 = min(p[1] for p in poly)
        z1 = max(p[1] for p in poly)
        for cell, count in spec["cells"]:
            placed = tries = 0
            while placed < count and tries < count * 80:
                tries += 1
                x, z = rng.uniform(x0, x1), rng.uniform(z0, z1)
                if not point_in_poly(x, z, poly) or any(point_in_poly(x, z, o) for o in outside):
                    continue
                edge = dist_to_poly_edge(x, z, poly)
                if spec.get("inset", 0.0) > edge:
                    continue
                if spec.get("thin_centre", True) and edge > 1.6 and rng.random() < 0.55:
                    continue
                if blocked(x, z, obstacles, 0.15):
                    continue
                if any(math.hypot(x - fx, z - fz) < fr for fx, fz, fr in keep_free):
                    continue
                pts.append((cell, round(x, 3), y, round(z, 3), round(rng.uniform(0, 360), 1),
                            round(rng.uniform(0.8, 1.25), 3)))
                placed += 1
        out[chunk] = pts
    return out


# -------------------------------------------------------------------- living
def build_interactions(zone):
    counts, out = {}, []
    for hid, kind, medium, (ax, ay, az), (tx, ty, tz), phases in zone.INTERACTIONS:
        counts[(hid, kind)] = counts.get((hid, kind), 0) + 1
        k = zone.INTERACTION_KINDS[kind]
        out.append({
            "id": f"{hid}.interact_{kind}_{counts[(hid, kind)]:02d}",
            "habitatId": hid, "kind": kind, "medium": medium,
            "x": ax, "y": ay, "z": az,
            "yaw": round(origin_living.yaw_to(ax, az, tx, tz), 2),
            "targetX": tx, "targetY": ty, "targetZ": tz,
            "radius": 0.6, "capacity": 1,
            "preferredPhases": list(phases),
            "requiresSwim": medium == "swim",
            "requiresFly": medium == "air",
            "animationHint": k["animationHint"], "feedback": k["feedback"], "sfx": k["sfx"],
        })
    return out


def graded_keyframes(grade):
    """Zone keyframes share Origin's hours and sun path, so a day reads the same
    everywhere and the Origin -> zone hand-off never jumps; only colour and mix change."""
    out = []
    for k in origin_living.U1_ENV2_KEYFRAMES:  # Ocean/Sky keep the U1-ENV.2 day (VIS.1 is Origin-only)
        g = dict(k)
        for key in origin_living.COLOR_KEYS:
            g[key] = list(k[key])
        grade(g, k)
        for key in origin_living.COLOR_KEYS:
            g[key] = [round(max(0.0, min(1.5, c)), 4) for c in g[key]]
        for key in origin_living.SCALAR_KEYS:
            g[key] = round(g[key], 4)
        out.append(g)
    return out


def mix(a, b, t):
    return [a[i] + (b[i] - a[i]) * t for i in range(3)]


def nightness(k):
    return max(0.0, min(1.0, k["nightGlow"]))


def build_living(zone):
    kinds = zone.INTERACTION_KINDS
    return {
        "version": 1,
        "zoneId": zone.ZONE_ID,
        "layoutVersion": zone.LAYOUT_VERSION,
        "defaultHour": origin_living.DEFAULT_HOUR,
        "dayLengthMinutes": origin_living.DAY_LENGTH_MINUTES,
        "phases": origin_living.PHASES,
        "keyframes": zone.keyframes(),
        "interactionPoints": build_interactions(zone),
        "pond": {"center": [0.0, 0.0, 0.0], "radii": [0.0, 0.0], "swimRadii": [0.0, 0.0]},
        "particleVolumes": [dict(id=k, **v) for k, v in zone.PARTICLES.items()],
        "audioLoops": [{"id": k, "clip": c, "weightKey": w, "emitterId": e or ""}
                       for k, (c, w, e) in zone.AUDIO_LOOPS.items()],
        "audioEmitters": zone.AUDIO_EMITTERS,
        "oneShots": sorted({k["sfx"] for k in kinds.values() if k["sfx"]}),
        "fog": zone.FOG,
        "fx": getattr(zone, "FX", []),
    }


def evaluate(zone, hour):
    return origin_living.evaluate(hour, zone.keyframes())


# ----------------------------------------------------------------- layout io
def build_layout(zone):
    cam = dict(origin.CAMERA)
    return {"version": zone.LAYOUT_VERSION, "seed": zone.SEED, "zoneId": zone.ZONE_ID,
            "camera": cam, "placements": zone.hero_props(),
            "scene": zone.SCENE}


def write_all(zone, out_dir):
    import os
    os.makedirs(out_dir, exist_ok=True)
    files = {}
    data = {
        f"{zone.FILE_PREFIX}Layout.json": build_layout(zone),
        f"{zone.FILE_PREFIX}Locations.json": build_locations(zone),
        f"{zone.FILE_PREFIX}Colliders.json": {
            "version": zone.LAYOUT_VERSION,
            "assets": [{"asset": a, "boxes": s} for a, s in sorted(collider_specs_unity(zone).items())]},
        f"{zone.FILE_PREFIX}Living.json": build_living(zone),
    }
    for name, d in data.items():
        p = os.path.join(out_dir, name)
        with open(p, "w", encoding="utf-8") as f:
            json.dump(d, f, indent=1, ensure_ascii=False)
        files[name] = p
    return files


def canonical(hid):
    return cw.HABITATS[hid]
