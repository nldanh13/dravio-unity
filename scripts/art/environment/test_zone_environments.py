"""U1-ENV.4 checks for the Ocean (Thủy Vực) and Sky (Thiên Giới) kits, without Blender or Unity.
Needs numpy and Pillow.

    python -m unittest scripts/art/environment/test_zone_environments.py
"""
import hashlib
import json
import math
import os
import sys
import tempfile
import unittest

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
sys.path.insert(0, HERE)
import ambience_audio  # noqa: E402
import canonical_world as cw  # noqa: E402
import framing  # noqa: E402
import layout  # noqa: E402
import ocean_zone  # noqa: E402
import sky_zone  # noqa: E402
import zone_audio  # noqa: E402
import zone_common as zc  # noqa: E402

UNITY_ENV = os.path.join(REPO, "Assets", "Dravio", "Environment")
SPECIES = os.path.join(REPO, "Assets", "Dravio", "Data", "species-biology.json")
ZONES = {"ocean": ocean_zone, "sky": sky_zone}

# Frozen Origin Sanctuary data (U1-ENV.2): the zone work must not change it.
ORIGIN_SHA256 = {
    "OriginSanctuaryLocations.json": "fd22230880c33607280f33a99edc392b7b3ae2956b0a8f02f23a690f41b69f99",
    "OriginSanctuaryColliders.json": "c971edb5bb950b4feb161bac21057beba5079e577c087c84d97356eaee9e4f83",
    "OriginSanctuaryLayout.json": "0e7f4f43634ee9e9dbb0493a2f41f64575fb724ae5d2f9a0c8bf6f4e61e377da",
}

# Pixel budget per texture (width x height) on mobile.
TEXTURE_MAX = {"PropsAtlas": (512, 512), "PropsEmission": (128, 128), "Foliage": (1024, 512),
               "Ground": (512, 512), "Water": (256, 256), "Caustics": (256, 256), "LightShaft": (64, 256),
               "Bubble": (64, 64), "CloudWisp": (128, 128), "Petal": (64, 64)}
ZONE_TEXTURE_PIXEL_BUDGET = 2_400_000
# ENV Alpha (#79): ZoneEnvironmentBuilder.BudgetProblems, unchanged: sum over the textures the zone scene's
# materials reference of width * height * 4 * 1.333 must be <= 6 MiB. Textures each zone scene binds: the four
# zone materials, the zone FX quads / particles, and the shared Origin soft dot (bursts).
UNITY_TEXTURE_BUDGET_BYTES = 6 * 1024 * 1024
SCENE_TEXTURES = {
    "ocean": ["PropsAtlas", "PropsEmission", "Foliage", "Ground", "FX_Caustics", "FX_LightShaft", "FX_Bubble"],
    "sky": ["PropsAtlas", "PropsEmission", "Foliage", "Ground", "Water", "FX_CloudWisp", "FX_Petal"],
}


def sha(path):
    with open(path, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()


def zone_dir(z, sub):
    return os.path.join(UNITY_ENV, "Zones", ZONES[z].FILE_PREFIX, sub)


def load_json(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


class CanonicalDataTests(unittest.TestCase):
    """IDs and rules come from the domain data, never invented by the art."""

    @classmethod
    def setUpClass(cls):
        cls.species = load_json(SPECIES)

    def test_zone_habitats_are_the_canonical_microhabitats(self):
        for z, mod in ZONES.items():
            ids = [h["id"] for h in mod.HABITATS]
            self.assertEqual(sorted(ids), sorted(cw.habitats_of(z)), z)
            self.assertEqual(len(ids), len(set(ids)))

    def test_every_canonical_id_is_used_by_species_data(self):
        used = {h for s in self.species.values() for h in s.get("habitatRules", [])}
        for z in ZONES:
            for hid in cw.habitats_of(z):
                self.assertIn(hid, used, hid + " is not referenced by species-biology.json")

    def test_species_of_a_zone_have_the_locomotion_to_live_there(self):
        for sid, s in self.species.items():
            loco = set(s.get("locomotion", []))
            for hid in s.get("habitatRules", []):
                if hid.startswith("ocean_"):
                    self.assertIn("swim", loco, f"{sid} in {hid}")
                if hid.startswith("sky_"):
                    self.assertIn("fly", loco, f"{sid} in {hid}")

    def test_perch_points_follow_the_canonical_perch_flag(self):
        for z, mod in ZONES.items():
            loc = zc.build_locations(mod)
            for hid in cw.habitats_of(z):
                n = sum(1 for p in loc["perchPoints"] if p["habitatId"] == hid)
                if cw.HABITATS[hid]["perch"]:
                    self.assertGreater(n, 0, hid)
                else:
                    self.assertEqual(n, 0, hid + " is perch:false")

    def test_gates_match_the_world_graph_and_stay_locked(self):
        for z, mod in ZONES.items():
            gates = zc.build_locations(mod)["zoneGates"]
            edges = cw.edges_of(z)
            self.assertEqual(sorted(g["worldEdgeId"] for g in gates), sorted(edges), z)
            for g in gates:
                a, b, requires, gateways = edges[g["worldEdgeId"]]
                other = a if b == z else b
                self.assertEqual(g["habitatId"], gateways[z])
                self.assertEqual(g["toZoneId"], other)
                self.assertEqual(g["toHabitatId"], gateways[other])
                self.assertEqual(tuple(g["requires"]), requires)
                self.assertTrue(g["lockedInG1"])

    def test_no_direct_ocean_sky_edge(self):
        for a, b, _, _ in cw.WORLD_EDGES.values():
            self.assertNotEqual({a, b}, {"ocean", "sky"})

    def test_nothing_is_unlocked_by_the_art(self):
        for z, mod in ZONES.items():
            for h in zc.build_locations(mod)["habitats"]:
                self.assertFalse(h["defaultUnlocked"], h["id"])


class LocationTests(unittest.TestCase):
    def test_spawn_count_meets_zone_capacity(self):
        for z, mod in ZONES.items():
            self.assertGreaterEqual(len(zc.build_locations(mod)["spawnPoints"]), cw.ZONE_CAPACITY, z)

    def test_ocean_offers_swim_spawns_in_every_habitat_and_sky_offers_air_spawns(self):
        loc = zc.build_locations(ocean_zone)
        for hid in cw.habitats_of("ocean"):
            self.assertTrue(any(s["habitatId"] == hid and s["medium"] == "swim" for s in loc["spawnPoints"]), hid)
        loc = zc.build_locations(sky_zone)
        media = {s["medium"] for s in loc["spawnPoints"]}
        self.assertEqual(media, {"ground", "air"})

    def test_spawns_and_interactions_are_valid_for_their_medium(self):
        for z, mod in ZONES.items():
            obstacles = zc.obstacle_polygons(mod)
            loc = zc.build_locations(mod)
            for s in loc["spawnPoints"]:
                self.assertTrue(zc.point_valid(mod, s["medium"], s["x"], s["y"], s["z"], s["habitatId"], obstacles), s["id"])
            for p in zc.build_interactions(mod):
                self.assertTrue(zc.point_valid(mod, p["medium"], p["x"], p["y"], p["z"], p["habitatId"], obstacles), p["id"])
                self.assertIn(p["habitatId"], cw.habitats_of(z))
                self.assertEqual(p["requiresSwim"], p["medium"] == "swim")
                self.assertEqual(p["requiresFly"], p["medium"] == "air")

    def test_spawns_are_spread_out(self):
        for z, mod in ZONES.items():
            sp = zc.build_locations(mod)["spawnPoints"]
            for i, a in enumerate(sp):
                for b in sp[i + 1:]:
                    d = math.dist((a["x"], a["y"], a["z"]), (b["x"], b["y"], b["z"]))
                    self.assertGreater(d, 0.9, f"{a['id']} / {b['id']}")

    def test_ids_are_unique_and_prefixed_by_habitat(self):
        for z, mod in ZONES.items():
            loc = zc.build_locations(mod)
            ids = []
            for key in ("spawnPoints", "perchPoints", "restPoints"):
                for p in loc[key]:
                    self.assertTrue(p["id"].startswith(p["habitatId"] + "."), p["id"])
                    ids.append(p["id"])
            ids += [p["id"] for p in zc.build_interactions(mod)]
            self.assertEqual(len(ids), len(set(ids)), z)

    def test_volumes_and_walk_areas_belong_to_their_habitat(self):
        for z, mod in ZONES.items():
            loc = zc.build_locations(mod)
            media = {"ocean": "swim", "sky": "fly"}[z]
            self.assertTrue(loc["volumes"])
            for v in loc["volumes"]:
                self.assertEqual(v["medium"], media)
                self.assertLess(v["yMin"], v["yMax"])
                self.assertIn(v["habitatId"], cw.habitats_of(z))
            for a in loc["walkAreas"]:
                self.assertIn(a["habitatId"], cw.habitats_of(z))

    def test_sky_area_links_connect_real_walk_areas(self):
        loc = zc.build_locations(sky_zone)
        areas = {a["id"] for a in loc["walkAreas"]}
        self.assertTrue(loc["areaLinks"])
        for l in loc["areaLinks"]:
            self.assertIn(l["areaA"], areas)
            self.assertIn(l["areaB"], areas)
            self.assertIn("fly", l["requires"])
        # Every island can be reached from the main island by some link.
        reach, changed = {"sky_low_island.main"}, True
        while changed:
            changed = False
            for l in loc["areaLinks"]:
                if (l["areaA"] in reach) != (l["areaB"] in reach):
                    reach |= {l["areaA"], l["areaB"]}
                    changed = True
        self.assertEqual(reach, areas)

    def test_points_clear_of_ui_and_safe_area(self):
        for z, mod in ZONES.items():
            loc = zc.build_locations(mod)
            pts = [(s["id"], (s["x"], s["y"] + 1.2, s["z"])) for s in loc["spawnPoints"]]
            pts += [(p["id"], (p["x"], p["y"], p["z"])) for p in loc["perchPoints"] + loc["restPoints"]]
            pts += [(p["id"], (p["x"], p["y"] + 0.6, p["z"])) for p in zc.build_interactions(mod)]
            pts += [(g["id"], (g["x"], g["y"] + 1.0, g["z"])) for g in loc["zoneGates"]]
            for name, sc in framing.SCENARIOS.items():
                ins = framing.insets(**sc)
                for label, a in layout.ASPECTS.items():
                    x0, y0, x1, y1 = framing.free_rect(a, ins)
                    for pid, p in pts:
                        vx, vy = framing.to_screen(p, a, ins)
                        self.assertTrue(x0 - 1e-6 <= vx <= x1 + 1e-6 and y0 - 1e-6 <= vy <= y1 + 1e-6,
                                        f"{z} {name} {label}: {pid} at ({vx:.3f}, {vy:.3f})")


class LivingDataTests(unittest.TestCase):
    def test_keyframes_reuse_origin_hours_and_stay_in_range(self):
        for z, mod in ZONES.items():
            liv = zc.build_living(mod)
            hours = [k["hour"] for k in liv["keyframes"]]
            self.assertEqual(hours, sorted(hours))
            for k in liv["keyframes"]:
                for key in ("lightColor", "ambientSky", "ambientEquator", "ambientGround", "fogColor"):
                    self.assertTrue(all(0 <= c <= 1 for c in k[key]), f"{z} {k['hour']} {key}")

    def test_particle_and_audio_slots_map_to_existing_components(self):
        channels = {"fireflies", "firefliesPond", "dust", "pondRings"}
        weights = {"audioBirds", "audioCrickets", "audioFrogs", "audioInsects", "audioWind", "audioPond"}
        for z, mod in ZONES.items():
            liv = zc.build_living(mod)
            got = [v["channel"] for v in liv["particleVolumes"]]
            self.assertEqual(len(got), len(set(got)))
            self.assertTrue(set(got) <= channels, z)
            for loop in liv["audioLoops"]:
                self.assertIn(loop["weightKey"], weights, loop)

    def test_fog_and_fx(self):
        for z, mod in ZONES.items():
            liv = zc.build_living(mod)
            self.assertLess(liv["fog"]["start"], liv["fog"]["end"])
            for f in liv["fx"]:
                self.assertIn(f["kind"], {"caustics", "light_shaft"})
                self.assertLessEqual(f["alpha"], 0.35)

    def test_interaction_sfx_exist_in_the_zone_audio(self):
        for z, mod in ZONES.items():
            names = set(zone_audio.ONE_SHOTS[z]) | set(zone_audio.LOOPS[z])
            for p in zc.build_interactions(mod):
                if p["sfx"]:
                    self.assertIn(p["sfx"], names, p["id"])


class CommittedDataTests(unittest.TestCase):
    """The JSON in Unity is exactly what the generators produce (no hand edits drift)."""

    def test_zone_json_matches_generators(self):
        with tempfile.TemporaryDirectory() as tmp:
            for z, mod in ZONES.items():
                for name, path in zc.write_all(mod, tmp).items():
                    committed = os.path.join(zone_dir(z, "Data"), name)
                    self.assertTrue(os.path.exists(committed), committed)
                    self.assertEqual(load_json(committed), load_json(path), name)

    def test_origin_data_is_unchanged(self):
        for name, digest in ORIGIN_SHA256.items():
            self.assertEqual(sha(os.path.join(UNITY_ENV, "Data", name)), digest, name)

    def test_zone_scene_paths_do_not_collide_with_origin(self):
        with open(os.path.join(UNITY_ENV, "Editor", "ZoneEnvironmentBuilder.cs"), encoding="utf-8") as f:
            src = f.read()
        self.assertIn('prefix + "_Environment.unity"', src)
        for mod in ZONES.values():
            self.assertNotEqual(mod.FILE_PREFIX, "OriginSanctuary")


class BudgetTests(unittest.TestCase):
    def test_manifest_budgets_and_location_checks(self):
        for z, mod in ZONES.items():
            m = load_json(os.path.join(zone_dir(z, "Data"), f"{mod.FILE_PREFIX}Manifest.json"))
            for name, st in m["assets"].items():
                self.assertLessEqual(st["triangles"], st["budget"], name)
                self.assertEqual(st["uv_errors"], 0, name)
                self.assertLessEqual(st["materials"] if isinstance(st["materials"], int) else len(st["materials"]), 3, name)  # islands: ground + props + foliage
            self.assertLessEqual(m["layout"]["scene_triangles"], m["layout"]["scene_triangle_budget"])
            self.assertLessEqual(m["layout"]["scene_triangle_budget"], 60000)
            self.assertTrue(m["locationChecks"])
            for r in m["locationChecks"]:
                self.assertTrue(r["ok"], r)

    def test_every_placed_asset_has_an_fbx(self):
        for z, mod in ZONES.items():
            models = set(os.path.splitext(f)[0] for f in os.listdir(zone_dir(z, "Models")) if f.endswith(".fbx"))
            for p in mod.hero_props():
                self.assertIn(p["asset"], models, p["asset"])

    def test_texture_sizes_and_pixel_budget(self):
        for z in ZONES:
            total = 0
            files = [f for f in os.listdir(zone_dir(z, "Textures")) if f.endswith(".png")]
            self.assertTrue(files)
            for f in files:
                with Image.open(os.path.join(zone_dir(z, "Textures"), f)) as im:
                    w, h = im.size
                key = next(k for k in TEXTURE_MAX if k in f)
                self.assertLessEqual((w, h), TEXTURE_MAX[key], f)
                self.assertTrue(w & (w - 1) == 0 and h & (h - 1) == 0, f + " not power of two")
                total += w * h
            self.assertLessEqual(total, ZONE_TEXTURE_PIXEL_BUDGET, z)

    def test_box_collider_top_under_every_perch_and_rest_matches_its_height(self):
        """Mirror of ZoneEnvironmentBuilder.Validate (ray from y + 3 down, tolerance 0.20, unchanged): the first
        collider hit is the highest prop box whose footprint contains the point. Points on island tops (mesh
        colliders) have no prop box and are checked by the Blender build instead. ENV Alpha (#79): perch_06 on the
        Sky gate keystone was 4.27 vs 4.49 before the keystone box."""
        for z, mod in ZONES.items():
            obstacles = zc.obstacle_polygons(mod)
            points = list(getattr(mod, "PERCHES", [])) + list(getattr(mod, "REST_POINTS", []))
            for i, (hid, src, x, y, zz) in enumerate(points):
                tops = [o["top"] for o in obstacles if o["source"] == src and zc.point_in_poly(x, zz, o["polygon"])
                        and o["top"] <= y + 3.0]
                if not tops:
                    continue
                self.assertLessEqual(abs(max(tops) - y), 0.20, f"{z} {src} ({x}, {zz}): collider top {max(tops)} vs {y}")

    def test_unity_rgba32_estimate_fits_the_validator_budget(self):
        soft_dot = os.path.join(UNITY_ENV, "Textures", "T_ENV_FX_SoftDot.png")
        for z, names in SCENE_TEXTURES.items():
            total = 0
            for n in names:
                f = f"T_ENV_{n}.png" if n.startswith("FX_") else f"T_ENV_{z.capitalize()}_{n}.png"
                with Image.open(os.path.join(zone_dir(z, "Textures"), f)) as im:
                    total += int(im.size[0] * im.size[1] * 4 * 1.333)
            with Image.open(soft_dot) as im:
                total += int(im.size[0] * im.size[1] * 4 * 1.333)
            self.assertLessEqual(total, UNITY_TEXTURE_BUDGET_BYTES, f"{z}: {total / 1048576:.2f} MiB")

    def test_textures_have_no_nan_or_empty_tiles(self):
        for z in ZONES:
            for f in os.listdir(zone_dir(z, "Textures")):
                if not f.endswith(".png"):
                    continue
                a = np.asarray(Image.open(os.path.join(zone_dir(z, "Textures"), f)).convert("RGBA"), dtype=np.float32)
                self.assertGreater(a[..., 3].max(), 0, f)
                if "Atlas" in f or "Foliage" in f:
                    h, w = a.shape[:2]
                    n = 4
                    for ty in range(n):
                        for tx in range(n):
                            tile = a[ty * h // n:(ty + 1) * h // n, tx * w // n:(tx + 1) * w // n]
                            self.assertGreater(tile[..., 3].max(), 0, f"{f} tile {tx},{ty} empty")


class AudioTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.tmp = tempfile.mkdtemp()
        cls.clips = {z: zone_audio.generate_zone(z, os.path.join(cls.tmp, z)) for z in ZONES}

    def test_loops_are_seamless_mono_and_levelled(self):
        for z in ZONES:
            for name in zone_audio.LOOPS[z]:
                x, sr, ch = ambience_audio.read_wav(os.path.join(self.tmp, z, name + ".wav"))
                self.assertEqual((sr, ch), (22050, 1), name)
                step = np.abs(np.diff(x))
                self.assertLessEqual(abs(x[0] - x[-1]), np.percentile(step, 99.5) + 1e-3, name + " seam click")
                self.assertLessEqual(np.max(np.abs(x)), 10 ** (-1 / 20) + 1e-3, name)
                r = 20 * math.log10(math.sqrt(float(np.mean(x ** 2))))
                self.assertGreater(r, -42, name + " too quiet")
                self.assertLess(r, -14, name + " too loud for a bed")

    def test_generation_is_deterministic_and_matches_unity_copy(self):
        for z in ZONES:
            for name in list(zone_audio.LOOPS[z]) + list(zone_audio.ONE_SHOTS[z]):
                fresh = os.path.join(self.tmp, z, name + ".wav")
                committed = os.path.join(zone_dir(z, "Audio"), name + ".wav")
                self.assertTrue(os.path.exists(committed), committed)
                self.assertEqual(sha(fresh), sha(committed), name)


if __name__ == "__main__":
    unittest.main()
