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

    def test_every_placement_has_a_builder(self):
        names = builder_asset_names()
        self.assertEqual(len(names), 25)
        for p in self.data["placements"]:
            self.assertIn(p["asset"], names)

    def test_every_builder_asset_is_used(self):
        used = {p["asset"] for p in self.data["placements"]}
        self.assertEqual(builder_asset_names(), used)

    def test_clearing_and_path_free_of_scatter(self):
        cx, cz, r = layout.CLEARING
        small = ("Grass", "Flower", "Fern")
        for p in self.data["placements"]:
            if any(s in p["asset"] for s in small):
                self.assertGreaterEqual(math.hypot(p["x"] - cx, p["z"] - cz), r, p)
                self.assertGreaterEqual(layout.path_distance(p["x"], p["z"]), layout.PATH_WIDTH * 0.75 - 1e-6)

    def test_camera_is_fixed_orthographic_config(self):
        cam = self.data["camera"]
        self.assertEqual(len(cam["position"]), 3)
        self.assertGreater(cam["orthographicSize"], 0)


class TextureTests(unittest.TestCase):
    def test_generated_sizes(self):
        with tempfile.TemporaryDirectory() as d:
            files = painterly_textures.generate_all(d)
            sizes = {os.path.basename(f): read_png_size(f) for f in files}
        self.assertEqual(sizes["T_ENV_PropsAtlas.png"], (1024, 1024))
        self.assertEqual(sizes["T_ENV_PropsEmission.png"], (512, 512))
        self.assertEqual(sizes["T_ENV_Foliage.png"], (512, 512))
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

    def test_manifest_within_budget(self):
        with open(os.path.join(UNITY_ENV, "Data", "OriginSanctuaryManifest.json"), encoding="utf-8") as f:
            manifest = json.load(f)
        for name, st in manifest["assets"].items():
            self.assertLessEqual(st["triangles"], st["budget"], name)
            self.assertEqual(st["uv_errors"], 0, name)
            self.assertLessEqual(len(st["materials"]), 3, name)
        self.assertLess(manifest["layout"]["scene_triangles"], 60000)

    def test_committed_layout_matches_generator(self):
        with open(os.path.join(UNITY_ENV, "Data", "OriginSanctuaryLayout.json"), encoding="utf-8") as f:
            committed = json.load(f)
        self.assertEqual(committed, json.loads(json.dumps(layout.build_layout())))


if __name__ == "__main__":
    unittest.main()
