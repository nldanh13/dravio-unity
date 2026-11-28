import tempfile
import unittest
from pathlib import Path
from check_alpha_build import preflight, POLICY, SETTINGS, BUILD_SETTINGS, VERSION, PACKAGES, BOOTSTRAP_MODELS


class BuildPreflightTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.scenes = ["Assets/Origin.unity", "Assets/Ocean.unity", "Assets/Sky.unity"]
        self.write(POLICY, "\n".join(f'public const string {zone}Scene="{rel}";'
                   for zone, rel in zip(("Origin", "Ocean", "Sky"), self.scenes)))
        self.write(VERSION, "m_EditorVersion: 6000.6.10f1\n")
        self.write(SETTINGS, """PlayerSettings:
  productName: Dravio
  bundleVersion: 0.1.0
  AndroidMinSdkVersion: 26
  AndroidTargetSdkVersion: 0
  AndroidTargetArchitectures: 2
  androidUseSwappy: 1
  applicationIdentifier:
    Android: com.example.dravio
  scriptingBackend:
    Android: 1
""")
        self.write(PACKAGES, '{"dependencies":{"com.unity.render-pipelines.universal":"17.6.0","com.unity.inputsystem":"1","com.unity.ugui":"2"}}')
        for i, rel in enumerate(self.scenes):
            self.write(rel, "%YAML scene fixture")
            self.write(rel + ".meta", f"guid: {i+1:032x}\n")
        self.scene_list(self.scenes)

    def write(self, rel, data):
        path = self.root / rel
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(data, encoding="utf-8")

    def scene_list(self, scenes):
        self.write(BUILD_SETTINGS, "EditorBuildSettings:\n  m_Scenes:\n" +
                   "".join(f"  - enabled: 1\n    path: {rel}\n    guid: {self.scenes.index(rel)+1:032x}\n" for rel in scenes)
                   + "  m_configObjects: {}\n")

    def codes(self, result):
        return {i["code"] for i in result["issues"] if i["level"] == "BLOCK"}

    def test_source_ready_not_release_approval(self):
        r = preflight(self.root)
        self.assertEqual(r["status"], "SOURCE_READY")
        self.assertIn("target-sdk-auto", self.codes(preflight(self.root, release=True)))

    def test_wrong_scene_order(self):
        self.scene_list(self.scenes[::-1])
        self.assertIn("scene-order", self.codes(preflight(self.root)))

    def test_missing_and_lfs_scene(self):
        (self.root / self.scenes[0]).unlink()
        self.write(self.scenes[1], "version https://git-lfs.github.com/spec/v1\n")
        r = preflight(self.root)
        self.assertTrue({"scene-file", "bootstrap-model"} <= self.codes(r))
        self.write(BOOTSTRAP_MODELS[0], "fbx fixture")
        self.assertNotIn("bootstrap-model", self.codes(preflight(self.root)))

    def test_guid_mismatch_and_missing_registration(self):
        self.write(self.scenes[0] + ".meta", "guid: " + "f"*32 + "\n")
        self.scene_list(self.scenes[:2])
        self.assertTrue({"scene-guid", "scene-registration"} <= self.codes(preflight(self.root)))

    def test_wrong_pacing_backend_and_architecture(self):
        path = self.root / SETTINGS
        self.write(SETTINGS, path.read_text().replace("androidUseSwappy: 1", "androidUseSwappy: 0")
                   .replace("AndroidTargetArchitectures: 2", "AndroidTargetArchitectures: 1")
                   .replace("Android: 1", "Android: 0"))
        self.assertTrue({"frame-pacing", "arm64", "il2cpp"} <= self.codes(preflight(self.root)))

    def test_missing_policy_does_not_pass(self):
        (self.root / POLICY).unlink()
        self.assertIn("scene-policy", self.codes(preflight(self.root)))


if __name__ == "__main__":
    unittest.main()
