"""U1-ENV.3 checks without Blender or Unity (needs numpy and Pillow).

    python -m unittest scripts/art/environment/test_living_environment.py
"""
import hashlib
import json
import math
import os
import sys
import tempfile
import unittest

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
sys.path.insert(0, HERE)
import ambience_audio  # noqa: E402
import framing  # noqa: E402
import fx_textures  # noqa: E402
import layout  # noqa: E402
import living  # noqa: E402

UNITY_ENV = os.path.join(REPO, "Assets", "Dravio", "Environment")
DATA = os.path.join(UNITY_ENV, "Data")

# Frozen U1-ENV.2 outputs: U1-ENV.3 must not change them.
U1_ENV2_SHA256 = {
    "OriginSanctuaryLocations.json": "fd22230880c33607280f33a99edc392b7b3ae2956b0a8f02f23a690f41b69f99",
    "OriginSanctuaryColliders.json": "c971edb5bb950b4feb161bac21057beba5079e577c087c84d97356eaee9e4f83",
    "OriginSanctuaryLayout.json": "0e7f4f43634ee9e9dbb0493a2f41f64575fb724ae5d2f9a0c8bf6f4e61e377da",
}
U1_ENV2_IDS = {
    "habitats": ["origin_pondbank", "origin_meadow", "origin_warm_rocks"],
    "spawnPoints": [f"origin_meadow.spawn_{i:02d}" for i in range(1, 6)]
                   + [f"origin_pondbank.spawn_{i:02d}" for i in range(1, 5)]
                   + [f"origin_warm_rocks.spawn_{i:02d}" for i in range(1, 5)],
    "transitions": ["origin_meadow__origin_pondbank_01", "origin_meadow__origin_pondbank_02",
                    "origin_meadow__origin_warm_rocks_03", "origin_meadow__origin_warm_rocks_04"],
    "perchPoints": ["origin_warm_rocks.perch_01", "origin_warm_rocks.perch_02", "origin_warm_rocks.perch_03",
                    "origin_pondbank.perch_04", "origin_pondbank.perch_05", "origin_meadow.perch_06"],
}


def sha(path):
    with open(path, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()


class PreservationTests(unittest.TestCase):
    def test_u1_env2_data_files_unchanged(self):
        for name, digest in U1_ENV2_SHA256.items():
            self.assertEqual(sha(os.path.join(DATA, name)), digest, name + " changed")

    def test_u1_env2_ids_unchanged(self):
        loc = layout.build_locations(layout.build_layout()["placements"])
        for key, ids in U1_ENV2_IDS.items():
            self.assertEqual([e["id"] for e in loc[key]], ids, key)

    def test_living_data_only_references_existing_habitats(self):
        hids = set(U1_ENV2_IDS["habitats"])
        d = living.build_living()
        for p in d["interactionPoints"]:
            self.assertIn(p["habitatId"], hids)
        for e in d["audioEmitters"]:
            self.assertIn(e["id"], hids)


class TimeOfDayTests(unittest.TestCase):
    def test_keyframes_sorted_and_in_range(self):
        hours = [k["hour"] for k in living.KEYFRAMES]
        self.assertEqual(hours, sorted(hours))
        self.assertTrue(all(0 <= h < 24 for h in hours))
        self.assertEqual(len(hours), len(set(hours)))

    def test_default_hour_matches_the_static_scene_lighting(self):
        """The Origin builder's static light (scene before the living system runs) must equal the default hour,
        or the scene jumps when time of day starts. Read straight from OriginSanctuaryEnvironmentBuilder.cs
        (ENV Alpha #79 changed both sides together)."""
        import re
        cs = open(os.path.join(UNITY_ENV, "Editor", "OriginSanctuaryEnvironmentBuilder.cs"), encoding="utf-8").read()

        def color(pattern):
            m = re.search(pattern + r"\s*=\s*new Color\(([^)]*)\)", cs)
            return [float(v.strip().rstrip("f")) for v in m.group(1).split(",")][:3]

        s = living.evaluate(living.DEFAULT_HOUR)
        self.assertAlmostEqual(s["lightElevation"], 46.0)
        self.assertAlmostEqual(s["lightAzimuth"], -38.0)
        self.assertIn("Quaternion.Euler(46f, -38f, 0f)", cs)
        self.assertAlmostEqual(s["lightIntensity"], float(re.search(r"sun\.intensity = ([0-9.]+)f", cs).group(1)))
        self.assertAlmostEqual(s["shadowStrength"], float(re.search(r"sun\.shadowStrength = ([0-9.]+)f", cs).group(1)))
        for got, want in ((s["lightColor"], color(r"sun\.color")), (s["ambientSky"], color(r"ambientSkyColor")),
                          (s["ambientEquator"], color(r"ambientEquatorColor")), (s["ambientGround"], color(r"ambientGroundColor")),
                          (s["fogColor"], color(r"Color sky"))):
            for a, b in zip(got, want):
                self.assertAlmostEqual(a, b)
        self.assertEqual(s["nightGlow"], 0.0)
        self.assertEqual(s["ambientFloorCut"], 0.0)

    def test_phases_cover_the_day(self):
        seen = {living.phase_at(m / 60.0) for m in range(0, 24 * 60, 5)}
        self.assertEqual(seen, {"dawn", "day", "dusk", "night"})
        self.assertEqual(living.phase_at(13), "day")
        self.assertEqual(living.phase_at(18.5), "dusk")
        self.assertEqual(living.phase_at(23), "night")
        self.assertEqual(living.phase_at(2), "night")
        self.assertEqual(living.phase_at(6), "dawn")

    def test_wraps_at_midnight(self):
        a, b = living.evaluate(23.999), living.evaluate(0.0)
        for k in living.SCALAR_KEYS:
            self.assertAlmostEqual(a[k], b[k], delta=0.02, msg=k)

    def test_sun_moon_swap_is_invisible(self):
        keys = living.KEYFRAMES
        for i, k in enumerate(keys):
            nxt = keys[(i + 1) % len(keys)]
            if k["isMoon"] != nxt["isMoon"]:
                self.assertEqual(k["lightIntensity"], 0.0, f"swap after {k['hour']}")
                self.assertEqual(nxt["lightIntensity"], 0.0, f"swap at {nxt['hour']}")

    def test_smooth_minute_by_minute(self):
        prev = living.evaluate(0)
        for m in range(1, 24 * 60 + 1):
            cur = living.evaluate(m / 60.0)
            self.assertLess(abs(cur["lightIntensity"] - prev["lightIntensity"]), 0.05)
            for c in range(3):
                self.assertLess(abs(cur["ambientEquator"][c] - prev["ambientEquator"][c]), 0.03)
            # Direction may only move fast while the light is (nearly) off.
            if max(cur["lightIntensity"], prev["lightIntensity"]) > 0.05:
                self.assertLess(abs(cur["lightAzimuth"] - prev["lightAzimuth"]), 2.0)
            prev = cur

    def test_light_above_horizon_and_from_camera_side(self):
        for m in range(0, 24 * 60, 5):
            s = living.evaluate(m / 60.0)
            if s["lightIntensity"] > 0.01:
                self.assertGreaterEqual(s["lightElevation"], 0.0)
                fwd = living.light_forward(s["lightElevation"], s["lightAzimuth"])
                self.assertGreaterEqual(fwd[2], -1e-6, f"light from behind the scene at {m / 60:.2f}h")

    def test_night_is_dark_but_readable(self):
        def mid_grey(s):
            amb = max(sum(s["ambientEquator"]) / 3, 0.27 * (1 - s["ambientFloorCut"]))  # shader floor mirror
            lit = s["lightIntensity"] * max(0.0, math.sin(math.radians(s["lightElevation"])))
            return 0.5 * (amb + lit * sum(s["lightColor"]) / 3)
        day, night = mid_grey(living.evaluate(13)), mid_grey(living.evaluate(22))
        self.assertLess(night, day * 0.45)
        self.assertGreater(night, 0.1)
        n = living.evaluate(22)
        self.assertGreater(n["ambientEquator"][2], n["ambientEquator"][0])  # moonlit blue

    def test_effects_follow_the_clock(self):
        self.assertGreater(living.evaluate(22)["fireflies"], 0.8)
        self.assertLess(living.evaluate(13)["fireflies"], 0.05)
        self.assertGreater(living.evaluate(16.5)["dust"], 0.5)
        self.assertLess(living.evaluate(22)["dust"], 0.05)
        self.assertGreater(living.evaluate(22)["audioCrickets"], living.evaluate(13)["audioCrickets"])
        self.assertGreater(living.evaluate(13)["audioBirds"], living.evaluate(22)["audioBirds"])
        for m in range(0, 24 * 60, 10):
            s = living.evaluate(m / 60.0)
            self.assertGreater(s["wind"], 0.3)  # calm, never a storm
            self.assertLessEqual(s["wind"], 1.2)


class InteractionTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.pl = layout.build_layout()["placements"]
        cls.obs = layout.obstacle_polygons(cls.pl)
        cls.loc = layout.build_locations(cls.pl)
        cls.points = living.build_interactions()

    def test_ids_unique_and_named(self):
        ids = [p["id"] for p in self.points]
        self.assertEqual(len(ids), len(set(ids)))
        for p in self.points:
            self.assertTrue(p["id"].startswith(p["habitatId"] + ".interact_" + p["kind"] + "_"), p["id"])
            self.assertIn(p["kind"], living.INTERACTION_KINDS)

    def test_every_habitat_has_several_points(self):
        for hid in U1_ENV2_IDS["habitats"]:
            self.assertGreaterEqual(sum(p["habitatId"] == hid for p in self.points), 3, hid)

    def test_approach_walkable_in_its_habitat(self):
        for p in self.points:
            self.assertTrue(layout.walkable(p["x"], p["z"], p["habitatId"], self.obs), p["id"])

    def test_water_points(self):
        for p in self.points:
            in_swim = ((p["targetX"] - layout.POND_CENTER[0]) / layout.SWIM_RADII[0]) ** 2 + \
                      ((p["targetZ"] - layout.POND_CENTER[1]) / layout.SWIM_RADII[1]) ** 2 < 1
            if p["requiresSwim"]:
                self.assertTrue(in_swim, p["id"])
            if p["kind"] == "drink":
                self.assertTrue(layout.in_water(p["targetX"], p["targetZ"]), p["id"])

    def test_faces_target_and_target_is_close(self):
        for p in self.points:
            want = math.degrees(math.atan2(p["targetX"] - p["x"], p["targetZ"] - p["z"]))
            self.assertAlmostEqual(p["yaw"], want, delta=0.5, msg=p["id"])
            d = math.hypot(p["targetX"] - p["x"], p["targetZ"] - p["z"])
            if p["kind"] != "stargaze":
                self.assertLess(d, 2.0, p["id"])

    def test_spacing_from_spawns_transitions_and_each_other(self):
        for p in self.points:
            for s in self.loc["spawnPoints"]:
                self.assertGreaterEqual(math.hypot(p["x"] - s["x"], p["z"] - s["z"]), 0.8, p["id"] + " vs " + s["id"])
            for t in self.loc["transitions"]:
                self.assertGreater(math.hypot(p["x"] - t["x"], p["z"] - t["z"]), t["radius"], p["id"] + " in " + t["id"])
            for q in self.points:
                if q is not p:
                    self.assertGreaterEqual(math.hypot(p["x"] - q["x"], p["z"] - q["z"]), 0.9, p["id"] + " vs " + q["id"])

    def test_feedback_and_sfx_are_known(self):
        sfx = set(ambience_audio.ONE_SHOTS)
        for p in self.points:
            if p["sfx"]:
                self.assertIn(p["sfx"], sfx)
            self.assertIn(p["feedback"], {"ripple_small", "splash", "fireflies_gather", "rune_sparkle", "petal_puff",
                                          "none", "heat_motes", "dust_puff"})
            for ph in p["preferredPhases"]:
                self.assertIn(ph, {"dawn", "day", "dusk", "night"})


class FramingTests(unittest.TestCase):
    def test_design_insets_give_u1_env2_pose(self):
        for a in layout.ASPECTS.values():
            size, sr, su = framing.compute(a, framing.insets())
            self.assertAlmostEqual(size, layout.ORTHO_SIZE)
            self.assertAlmostEqual(sr, layout.ui_offset())
            self.assertAlmostEqual(su, 0.0)
            self.assertEqual(framing.free_rect(a, framing.insets()), layout.free_viewport(a))

    def test_never_zooms_in_and_zoom_is_bounded(self):
        for sc in framing.SCENARIOS.values():
            for a in layout.ASPECTS.values():
                size = framing.compute(a, framing.insets(**sc))[0]
                self.assertGreaterEqual(size, layout.ORTHO_SIZE - 1e-9)
                self.assertLess(size, layout.ORTHO_SIZE * 1.6)

    def test_gameplay_points_clear_of_ui_and_safe_area(self):
        loc = layout.build_locations(layout.build_layout()["placements"])
        pts = [(s["id"], (s["x"], 1.2, s["z"])) for s in loc["spawnPoints"]]
        pts += [(p["id"], (p["x"], 1.2, p["z"])) for p in living.build_interactions()]
        pts += [(t["id"], (t["x"], 0.0, t["z"])) for t in loc["transitions"]]
        for h in loc["habitats"]:
            xz = h["polygonXZ"]
            pts += [(h["id"], (xz[i], 0.0, xz[i + 1])) for i in range(0, len(xz), 2)]
        for name, sc in framing.SCENARIOS.items():
            ins = framing.insets(**sc)
            for label, a in layout.ASPECTS.items():
                x0, y0, x1, y1 = framing.free_rect(a, ins)
                for pid, p in pts:
                    vx, vy = framing.to_screen(p, a, ins)
                    self.assertTrue(x0 - 1e-6 <= vx <= x1 + 1e-6 and y0 - 1e-6 <= vy <= y1 + 1e-6,
                                    f"{name} {label}: {pid} at ({vx:.3f}, {vy:.3f}) outside {x0:.3f}..{x1:.3f}")

    def test_reported_ui_includes_safe_area(self):
        # A UI that reports less than the safe-area inset cannot shrink the inset.
        self.assertEqual(framing.insets(ui_right=0.05, safe_right=0.15)[1], 0.15)
        # Without a report the design panel sits inside the safe area.
        self.assertAlmostEqual(framing.insets(safe_right=0.15)[1], layout.RIGHT_PANEL_HEIGHTS + 0.15)


class AudioTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.tmp = tempfile.mkdtemp()
        cls.clips = ambience_audio.generate_all(cls.tmp)

    def test_loops_are_seamless_mono_and_levelled(self):
        for name in ambience_audio.LOOPS:
            x, sr, ch = ambience_audio.read_wav(os.path.join(self.tmp, name + ".wav"))
            self.assertEqual((sr, ch), (22050, 1), name)
            self.assertAlmostEqual(len(x) / sr, ambience_audio.LOOP_SECONDS, places=3)
            step = np.abs(np.diff(x))
            self.assertLessEqual(abs(x[0] - x[-1]), np.percentile(step, 99.5) + 1e-3, name + " seam click")
            self.assertLessEqual(np.max(np.abs(x)), 10 ** (-1 / 20) + 1e-3, name)
            r = 20 * math.log10(math.sqrt(float(np.mean(x ** 2))))
            self.assertGreater(r, -40, name + " too quiet")
            self.assertLess(r, -14, name + " too loud for a bed")

    def test_one_shots_short_and_clean(self):
        for name in ambience_audio.ONE_SHOTS:
            x, sr, ch = ambience_audio.read_wav(os.path.join(self.tmp, name + ".wav"))
            self.assertEqual((sr, ch), (22050, 1))
            self.assertLess(len(x) / sr, 2.0, name)
            self.assertLess(abs(x[-1]), 0.02, name + " ends with a click")
            self.assertLess(abs(x[0]), 0.05, name + " starts with a click")

    def test_every_referenced_clip_exists(self):
        d = living.build_living()
        names = set(ambience_audio.LOOPS) | set(ambience_audio.ONE_SHOTS)
        for lp in d["audioLoops"]:
            self.assertIn(lp["clip"], names)
        for s in d["oneShots"]:
            self.assertIn(s, names)

    def test_committed_audio_matches_generator(self):
        for name in ambience_audio.LOOPS + ambience_audio.ONE_SHOTS:
            path = os.path.join(UNITY_ENV, "Audio", name + ".wav")
            self.assertTrue(os.path.exists(path), name)
            a, _, _ = ambience_audio.read_wav(path)
            b, _, _ = ambience_audio.read_wav(os.path.join(self.tmp, name + ".wav"))
            self.assertEqual(len(a), len(b), name)
            self.assertLess(float(np.max(np.abs(a - b))), 2e-3, name)


class CommittedLivingTests(unittest.TestCase):
    def test_living_json_matches_generator(self):
        with open(os.path.join(DATA, "OriginSanctuaryLiving.json"), encoding="utf-8") as f:
            self.assertEqual(json.load(f), json.loads(json.dumps(living.build_living())))

    def test_fx_textures_match_generator(self):
        from PIL import Image
        tmp = tempfile.mkdtemp()
        for name, path in fx_textures.generate_all(tmp).items():
            a = np.asarray(Image.open(path))
            b = np.asarray(Image.open(os.path.join(UNITY_ENV, "Textures", name)))
            self.assertEqual(a.shape, (64, 64, 4))
            self.assertTrue(np.array_equal(a, b), name)
            self.assertEqual(int(a[0, 0, 3]), 0, name + " corner must be transparent")

    def test_particle_budget(self):
        total = sum(v["maxParticles"] for v in living.PARTICLES.values())
        self.assertLessEqual(total, 120)


if __name__ == "__main__":
    unittest.main()
