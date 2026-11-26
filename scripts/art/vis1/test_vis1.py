"""VIS.1 (Issue #80) checks that run without Blender or Unity (numpy + Pillow).

    python -m unittest scripts/art/vis1/test_vis1.py

What they protect: Origin-only scope, gameplay/navigation data unchanged, GUIDs kept,
the mobile triangle budget, and that every copy of the new lighting grade agrees
(living.py -> JSON -> Unity .asset -> static scene -> builder).
"""
import json
import os
import re
import subprocess
import sys
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
ENV = os.path.join(ROOT, "scripts", "art", "environment")
sys.path.insert(0, ENV)
import living  # noqa: E402
import painterly_textures  # noqa: E402

UNITY_ENV = os.path.join(ROOT, "Assets", "Dravio", "Environment")
DATA = os.path.join(UNITY_ENV, "Data")
BUILDER = os.path.join(UNITY_ENV, "Editor", "OriginSanctuaryEnvironmentBuilder.cs")
SCENE = os.path.join(UNITY_ENV, "Scenes", "OriginLongTermGameplay_Environment.unity")
BASELINE = "6eb1295a7b42bbfb5c3567c71cbc271c4bc65875"  # PR #63 head this work started from
CHANGED_MODELS = ["SM_ENV_Gate_Ancient", "SM_ENV_Column_Intact", "SM_ENV_Tree_Ancient", "SM_ENV_Tree_Ancient_B",
                  "SM_ENV_Shrub_Round", "SM_ENV_Ground_Sanctuary", "SM_ENV_Cover_Edges"]


def read(path):
    with open(path, encoding="utf-8") as f:
        return f.read()


def git_show(rev, path):
    try:
        return subprocess.run(["git", "show", f"{rev}:{path}"], cwd=ROOT, capture_output=True, text=True,
                              check=True).stdout
    except (OSError, subprocess.CalledProcessError):
        return None


def parse_asset_keyframes(text):
    frames = []
    body = "\n" + text.split("  keyframes:\n", 1)[1]
    body = re.split(r"\n  [A-Za-z]", body, maxsplit=1)[0]  # stop at the next top-level field
    for block in re.split(r"\n  - hour: ", body)[1:]:
        kf = {"hour": float(block.split("\n", 1)[0])}
        for key in living.COLOR_KEYS:
            m = re.search(r"\n    %s:\n    - ([-0-9.e]+)\n    - ([-0-9.e]+)\n    - ([-0-9.e]+)" % key, block)
            kf[key] = [float(v) for v in m.groups()]
        kf["lightIntensity"] = float(re.search(r"\n    lightIntensity: ([-0-9.e]+)", block).group(1))
        frames.append(kf)
    return frames


class LightingGradeTests(unittest.TestCase):
    def test_json_is_generator_output(self):
        committed = json.loads(read(os.path.join(DATA, "OriginSanctuaryLiving.json")))
        self.assertEqual(committed["keyframes"], json.loads(json.dumps(living.KEYFRAMES)))

    def test_unity_asset_keyframes_match_json(self):
        frames = parse_asset_keyframes(read(os.path.join(DATA, "OriginSanctuaryLiving.asset")))
        self.assertEqual(len(frames), len(living.KEYFRAMES))
        for got, want in zip(frames, living.KEYFRAMES):
            self.assertAlmostEqual(got["hour"], want["hour"])
            self.assertAlmostEqual(got["lightIntensity"], want["lightIntensity"], places=4)
            for key in living.COLOR_KEYS:
                for a, b in zip(got[key], want[key]):
                    self.assertAlmostEqual(a, b, places=4, msg=f"{want['hour']} {key}")

    def test_builder_and_scene_static_lighting_equal_default_hour(self):
        s = living.evaluate(living.DEFAULT_HOUR)
        src = read(BUILDER)
        scene = read(SCENE)

        def fmt(c):
            return "new Color(%s)" % ", ".join("%.2ff" % v for v in c)
        for key, cs_name in (("ambientSky", "ambientSkyColor"), ("ambientEquator", "ambientEquatorColor"),
                             ("ambientGround", "ambientGroundColor")):
            self.assertIn("RenderSettings.%s = %s;" % (cs_name, fmt(s[key])), src)
        self.assertIn("Color sky = %s;" % fmt(s["fogColor"]), src)
        self.assertIn("sun.intensity = %.2ff;" % s["lightIntensity"], src)
        self.assertIn("sun.color = new Color(1f, %.2ff, %.2ff);" % tuple(s["lightColor"][1:]), src)
        rgb = lambda c: "{r: %g, g: %g, b: %g, a: 1}" % tuple(c)  # noqa: E731
        self.assertIn("m_AmbientSkyColor: " + rgb(s["ambientSky"]), scene)
        self.assertIn("m_FogColor: " + rgb(s["fogColor"]), scene)
        self.assertIn("m_Intensity: %g\n" % s["lightIntensity"], scene)

    def test_ocean_and_sky_keep_u1_env2_day(self):
        u = {k["hour"]: k for k in living.U1_ENV2_KEYFRAMES}[living.DEFAULT_HOUR]
        self.assertEqual(u["lightColor"], [1.0, 0.92, 0.78])
        self.assertAlmostEqual(u["lightIntensity"], 1.35)
        self.assertEqual([k["hour"] for k in living.U1_ENV2_KEYFRAMES], [k["hour"] for k in living.KEYFRAMES])

    def test_material_grade_matches_builder(self):
        src = read(BUILDER)
        for name in ("Props", "Foliage", "Ground"):
            mat = read(os.path.join(UNITY_ENV, "Materials", f"M_ENV_{name}.mat"))
            self.assertIn("- _ShadowTint: {r: 0.56, g: 0.62, b: 0.9, a: 1}", mat)
            self.assertIn("- _RampSoftness: 0.26", mat)
            self.assertIn("- _PathColor: {r: 0.5, g: 0.38, b: 0.25, a: 1}", mat)
            self.assertIn("- _RimStrength: %s" % ("0" if name == "Ground" else "0.22"), mat)
        self.assertIn('m.SetColor("_ShadowTint", new Color(0.56f, 0.62f, 0.90f, 1f));', src)
        self.assertIn('m.SetColor("_PathColor", new Color(0.50f, 0.38f, 0.25f, 1f));', src)


class ContractTests(unittest.TestCase):
    def test_budget(self):
        manifest = json.loads(read(os.path.join(DATA, "OriginSanctuaryManifest.json")))
        self.assertEqual(manifest["layout"]["placements"], 45)
        self.assertLess(manifest["layout"]["scene_triangles"], 60000)
        for name, st in manifest["assets"].items():
            self.assertLessEqual(st["triangles"], st["budget"], name)
            self.assertLessEqual(len(st["materials"]), 3, name)

    def test_vis1_ground_map_is_seamless(self):
        import numpy as np
        g = painterly_textures.build_ground_vis1().astype(int)
        edge = np.abs(g[0] - g[-1]).mean()
        interior = np.abs(g[100] - g[101]).mean()
        self.assertLess(edge, interior * 3 + 4)

    @unittest.skipIf(git_show(BASELINE, "README.md") is None, "baseline commit not available")
    def test_guids_and_gameplay_data_unchanged_since_baseline(self):
        for name in CHANGED_MODELS:
            rel = f"Assets/Dravio/Environment/Models/{name}.fbx.meta"
            self.assertEqual(read(os.path.join(ROOT, rel)), git_show(BASELINE, rel), rel)
        for rel in ("Assets/Dravio/Environment/Data/OriginSanctuaryLayout.json",
                    "Assets/Dravio/Environment/Data/OriginSanctuaryLocations.json",
                    "Assets/Dravio/Environment/Data/OriginSanctuaryColliders.json",
                    "Assets/Dravio/Environment/Data/OriginSanctuaryLocations.asset",
                    "Assets/Dravio/Scenes/OriginLongTermGameplay.unity",
                    "Assets/Dravio/Art/Imported/Prefabs/EmberlingRefinedGLBVisual.prefab",
                    "Assets/Dravio/Art/Models/EmberlingRefinedGLB.fbx.meta"):
            self.assertEqual(read(os.path.join(ROOT, rel)), git_show(BASELINE, rel), rel)
        for tex in ("T_ENV_PropsAtlas", "T_ENV_Foliage", "T_ENV_Ground"):
            rel = f"Assets/Dravio/Environment/Textures/{tex}.png.meta"
            self.assertEqual(read(os.path.join(ROOT, rel)), git_show(BASELINE, rel), rel)

    def test_character_shader_has_urp_passes(self):
        src = read(os.path.join(ROOT, "Assets", "Dravio", "Art", "Shaders", "DravioCharacterStylized.shader"))
        self.assertIn('Shader "Dravio/Character/Stylized Lit"', src)
        for tag in ('"LightMode"="UniversalForward"', '"LightMode"="ShadowCaster"', '"LightMode"="DepthOnly"'):
            self.assertIn(tag, src)
        self.assertIn("CBUFFER_START(UnityPerMaterial)", src)


if __name__ == "__main__":
    unittest.main()
