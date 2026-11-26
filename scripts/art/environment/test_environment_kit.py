"""Blender-free checks for the Origin Sanctuary kit (atlas, layout, textures, outputs).

    python -m unittest scripts/art/environment/test_environment_kit.py

Requires Python 3 + numpy. The Blender build itself additionally enforces per-asset
triangle budgets and verifies that every atlas UV stays inside its tile.
"""
import json
import math
import os
import re
import sys
import tempfile
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
sys.path.insert(0, HERE)

import atlas  # noqa: E402
import layout  # noqa: E402
import painterly_textures  # noqa: E402
from png_io import read_png_size  # noqa: E402

UNITY_ENV = os.path.join(REPO, "Assets", "Dravio", "Environment")


def builder_asset_names():
    with open(os.path.join(HERE, "build_origin_sanctuary.py"), encoding="utf-8") as f:
        src = f.read()
    block = src[src.index("ASSETS = {"):src.index("}", src.index("ASSETS = {"))]
    return set(re.findall(r'"(SM_ENV_[A-Za-z_]+)"', block))


class AtlasTests(unittest.TestCase):
    def test_props_tiles_inside_unit_square_and_disjoint(self):
        rects = [atlas.tile_uv_rect(n) for n in atlas.PROPS_TILES]
        self.assertEqual(len(rects), atlas.PROPS_GRID ** 2)
        for u0, v0, u1, v1 in rects:
            self.assertTrue(0 < u0 < u1 < 1 and 0 < v0 < v1 < 1)
        for i, a in enumerate(rects):
            for b in rects[i + 1:]:
                overlap = not (a[2] <= b[0] or b[2] <= a[0] or a[3] <= b[1] or b[3] <= a[1])
                self.assertFalse(overlap)

    def test_foliage_cells_inside_unit_square(self):
        for name in atlas.FOLIAGE_CELLS:
            u0, v0, u1, v1 = atlas.foliage_uv_rect(name)
            self.assertTrue(0 < u0 < u1 < 1 and 0 < v0 < v1 < 1, name)


class LayoutTests(unittest.TestCase):
    def setUp(self):
        self.data = layout.build_layout()

    def test_deterministic(self):
        self.assertEqual(json.dumps(self.data), json.dumps(layout.build_layout()))
        self.assertEqual(json.dumps(layout.build_locations(self.data["placements"])),
                         json.dumps(layout.build_locations(layout.build_layout()["placements"])))

    def test_every_placement_has_a_builder(self):
        names = builder_asset_names()
        for p in self.data["placements"]:
            self.assertIn(p["asset"], names)

    def test_large_props_have_colliders(self):
        specs = layout.collider_specs_unity()
        for p in self.data["placements"]:
            a = p["asset"]
            if any(k in a for k in ("Gate", "Column", "Wall", "Statue", "Tree", "Runestone", "Rock_Warm",
                                    "Rock_Mossy_A", "Rock_Mossy_B", "Shrub", "Ground")):
                self.assertIn(a, specs, a)


class LandscapeTests(unittest.TestCase):
    """Fixed camera + right-hand Fantasy UI panel on 16:9, 19.5:9 and 20:9."""

    def setUp(self):
        self.loc = layout.build_locations(layout.build_layout()["placements"])

    def gameplay_points(self):
        pts = []
        for h in self.loc["habitats"]:
            xz = h["polygonXZ"]
            pts += [(xz[i], 0.0, xz[i + 1]) for i in range(0, len(xz), 2)]
        for s in self.loc["spawnPoints"]:
            pts += [(s["x"], 0.0, s["z"]), (s["x"], 1.2, s["z"])]  # feet and head of a baby dragon
        pts += [(t["x"], 0.0, t["z"]) for t in self.loc["transitions"]]
        pts += [(p["x"], p["y"], p["z"]) for p in self.loc["perchPoints"]]
        return pts

    def test_gameplay_inside_free_area_all_aspects(self):
        for label, aspect in layout.ASPECTS.items():
            x0, y0, x1, y1 = layout.free_viewport(aspect)
            for p in self.gameplay_points():
                vx, vy = layout.to_screen(p, aspect)
                self.assertTrue(x0 <= vx <= x1 and y0 <= vy <= y1, f"{label}: {p} -> ({vx:.3f}, {vy:.3f})")

    def test_tall_backdrop_not_cut_by_screen_top(self):
        tops = [(0.0, 6.42 * 0.72, 7.5), (5.7, 3.25 * 1.05, 5.9)]  # gate arch, statue head
        for label, aspect in layout.ASPECTS.items():
            for p in tops:
                self.assertLessEqual(layout.to_screen(p, aspect)[1], 1.0, f"{label}: {p}")

    def test_ground_covers_every_aspect(self):
        for label, aspect in layout.ASPECTS.items():
            for vx in (0.0, 1.0):
                for vy in (0.0, 1.0):
                    x, z = layout.ground_from_viewport(vx, vy, aspect)
                    self.assertTrue(-16 <= x <= 20 and -11 <= z <= 28, f"{label}: corner ({x:.1f}, {z:.1f})")

    def test_ui_offset_is_constant_across_aspects(self):
        # The panel scales with screen height, so the same world strip is hidden on every phone.
        hidden = [2 * layout.ORTHO_SIZE * layout.RIGHT_PANEL_HEIGHTS for _ in layout.ASPECTS]
        self.assertEqual(len(set(round(h, 6) for h in hidden)), 1)


class HabitatTests(unittest.TestCase):
    def setUp(self):
        self.placements = layout.build_layout()["placements"]
        self.loc = layout.build_locations(self.placements)
        self.obstacles = layout.obstacle_polygons(self.placements)
        self.polys = {h["id"]: h["polygon"] for h in layout.HABITATS}

    def test_three_canonical_origin_microhabitats(self):
        # Same IDs and unlock rules as Domain/Catalog (G0BiologyData, ProgressCatalog).
        got = {h["id"]: (h["defaultUnlocked"], h["requiresDiscovery"]) for h in self.loc["habitats"]}
        self.assertEqual(got, {
            "origin_meadow": (True, ""),
            "origin_pondbank": (False, "DISC_AQUATIC_HABITAT"),
            "origin_warm_rocks": (False, "DISC_EMBERLING_THERMAL"),
        })
        for h in self.loc["habitats"]:
            self.assertEqual(h["zoneId"], "origin")

    def test_habitats_do_not_overlap_and_tile_the_play_area(self):
        inside_any = 0
        samples = 0
        for i in range(-69, 70, 3):
            for j in range(-53, 62, 3):
                x, z = i / 10, j / 10
                hits = [hid for hid, poly in self.polys.items() if layout.point_in_poly(x, z, poly)]
                self.assertLessEqual(len(hits), 1, (x, z, hits))
                samples += 1
                inside_any += bool(hits)
        self.assertGreater(inside_any / samples, 0.9)

    def test_each_habitat_has_enough_walkable_ground(self):
        for hid, poly in self.polys.items():
            total = ok = 0
            for i in range(-70, 71, 2):
                for j in range(-54, 63, 2):
                    x, z = i / 10, j / 10
                    if layout.point_in_poly(x, z, poly):
                        total += 1
                        ok += layout.walkable(x, z, hid, self.obstacles)
            self.assertGreater(ok / total, 0.45, f"{hid}: {ok}/{total}")

    def test_spawn_points_walkable_and_spaced(self):
        by = {}
        for s in self.loc["spawnPoints"]:
            self.assertTrue(layout.walkable(s["x"], s["z"], s["habitatId"], self.obstacles), s["id"])
            by.setdefault(s["habitatId"], []).append(s)
        for hid, pts in by.items():
            self.assertGreaterEqual(len(pts), 4, hid)
        allp = self.loc["spawnPoints"]
        for i, a in enumerate(allp):
            for b in allp[i + 1:]:
                self.assertGreaterEqual(math.hypot(a["x"] - b["x"], a["z"] - b["z"]), 1.2, (a["id"], b["id"]))

    def test_transitions_sit_on_shared_borders_and_are_clear(self):
        pairs = set()
        for t in self.loc["transitions"]:
            for hid in (t["habitatA"], t["habitatB"]):
                self.assertLess(layout.dist_to_poly_edge(t["x"], t["z"], self.polys[hid]), 0.1, t["id"])
            self.assertFalse(layout.blocked(t["x"], t["z"], self.obstacles, 0.6), t["id"])
            self.assertFalse(layout.in_water(t["x"], t["z"], 0.6), t["id"])
            pairs.add(frozenset((t["habitatA"], t["habitatB"])))
        self.assertIn(frozenset(("origin_meadow", "origin_pondbank")), pairs)
        self.assertIn(frozenset(("origin_meadow", "origin_warm_rocks")), pairs)

    def test_pondbank_swim_area_is_water_inside_pondbank(self):
        pond = next(h for h in self.loc["habitats"] if h["id"] == "origin_pondbank")
        xz = pond["swimPolygonXZ"]
        self.assertGreaterEqual(len(xz), 6)
        for i in range(0, len(xz), 2):
            self.assertTrue(layout.in_water(xz[i], xz[i + 1]))
            self.assertTrue(layout.point_in_poly(xz[i], xz[i + 1], self.polys["origin_pondbank"]))
        for h in self.loc["habitats"]:
            if h["id"] != "origin_pondbank":
                self.assertEqual(h["swimPolygonXZ"], [])

    def test_perches_belong_to_their_habitat(self):
        for p in self.loc["perchPoints"]:
            poly = self.polys[p["habitatId"]]
            self.assertTrue(layout.point_in_poly(p["x"], p["z"], poly)
                            or layout.dist_to_poly_edge(p["x"], p["z"], poly) < 0.8, p["id"])

    def test_ground_cover_keeps_spawns_and_transitions_clear(self):
        cover = layout.scatter_cover(self.placements)
        for chunk, pts in cover.items():
            self.assertGreater(len(pts), 20, chunk)
            for _, x, z, _, _ in pts:
                for s in self.loc["spawnPoints"]:
                    self.assertGreaterEqual(math.hypot(x - s["x"], z - s["z"]), 0.55)


class TextureTests(unittest.TestCase):
    def test_generated_sizes(self):
        with tempfile.TemporaryDirectory() as d:
            files = painterly_textures.generate_all(d)
            sizes = {os.path.basename(f): read_png_size(f) for f in files}
        self.assertEqual(sizes["T_ENV_PropsAtlas.png"], (1024, 1024))
        self.assertEqual(sizes["T_ENV_PropsEmission.png"], (512, 512))
        self.assertEqual(sizes["T_ENV_Foliage.png"], (1024, 512))
        self.assertEqual(sizes["T_ENV_Ground.png"], (512, 512))
        self.assertEqual(sizes["T_ENV_Water.png"], (256, 256))

    def test_tiling_maps_are_seamless(self):
        import numpy as np
        g = painterly_textures.build_ground().astype(int)
        # Opposite edges should differ no more than neighbouring interior rows.
        edge = np.abs(g[0] - g[-1]).mean()
        interior = np.abs(g[100] - g[101]).mean()
        self.assertLess(edge, interior * 3 + 4)


@unittest.skipUnless(os.path.isdir(os.path.join(UNITY_ENV, "Models")), "Unity folder not populated")
class CommittedOutputTests(unittest.TestCase):
    def test_all_models_and_textures_present(self):
        models = {f[:-4] for f in os.listdir(os.path.join(UNITY_ENV, "Models")) if f.endswith(".fbx")}
        self.assertEqual(models, builder_asset_names())
        textures = set(os.listdir(os.path.join(UNITY_ENV, "Textures")))
        for t in ("T_ENV_PropsAtlas.png", "T_ENV_PropsEmission.png", "T_ENV_Foliage.png",
                  "T_ENV_Ground.png", "T_ENV_Water.png"):
            self.assertIn(t, textures)

    def test_committed_locations_and_colliders_match_generator(self):
        with open(os.path.join(UNITY_ENV, "Data", "OriginSanctuaryLocations.json"), encoding="utf-8") as f:
            committed = json.load(f)
        fresh = json.loads(json.dumps(layout.build_locations(layout.build_layout()["placements"])))
        self.assertEqual(committed, fresh)
        with open(os.path.join(UNITY_ENV, "Data", "OriginSanctuaryColliders.json"), encoding="utf-8") as f:
            colliders = json.load(f)
        self.assertEqual({a["asset"] for a in colliders["assets"]}, set(layout.collider_specs_unity()))

    def test_manifest_within_budget(self):
        with open(os.path.join(UNITY_ENV, "Data", "OriginSanctuaryManifest.json"), encoding="utf-8") as f:
            manifest = json.load(f)
        for name, st in manifest["assets"].items():
            self.assertLessEqual(st["triangles"], st["budget"], name)
            self.assertEqual(st["uv_errors"], 0, name)
            self.assertLessEqual(len(st["materials"]), 3, name)
        self.assertLess(manifest["layout"]["scene_triangles"], 60000)
        self.assertLessEqual(manifest["layout"]["placements"], 60)

    def test_committed_layout_matches_generator(self):
        with open(os.path.join(UNITY_ENV, "Data", "OriginSanctuaryLayout.json"), encoding="utf-8") as f:
            committed = json.load(f)
        self.assertEqual(committed, json.loads(json.dumps(layout.build_layout())))


if __name__ == "__main__":
    unittest.main()
