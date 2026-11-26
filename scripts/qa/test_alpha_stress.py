import copy
import unittest
from check_alpha_stress import evaluate


def fixture():
    return {"platform": "Android", "development": True, "device": "synthetic",
            "gpu": "synthetic", "unityVersion": "6000.6.10f1", "timestamp": "fixture",
            "results": [{"dragons": n, "visibleAvg": n, "fpsAvg": 30, "fps1Low": 26,
                         "frameMsAvg": 1000 / 30, "frameMsMax": 50,
                         "drawCallsAvg": 100, "setPassAvg": 50, "trianglesAvg": 100000,
                         "totalUsedMemoryMB": 200} for n in (5, 15, 30)]}


class StressScreenTests(unittest.TestCase):
    def test_pass_is_not_approval(self):
        r = fixture()
        before = copy.deepcopy(r)
        self.assertEqual(evaluate(r)["status"], "SCREEN_PASS")
        self.assertEqual(r, before)

    def test_below_target(self):
        r = fixture()
        r["results"][2]["fpsAvg"] = 24
        r["results"][2]["fps1Low"] = 20
        r["results"][2]["frameMsAvg"] = 1000 / 24
        self.assertEqual(evaluate(r)["status"], "SCREEN_FAIL")

    def test_render_budget_warning_not_certification(self):
        r = fixture()
        r["results"][2]["trianglesAvg"] = 600000
        self.assertEqual(evaluate(r)["status"], "SCREEN_PASS_WITH_WARNINGS")

    def test_editor_and_missing_counters_do_not_pass(self):
        r = fixture()
        r["platform"] = "WindowsEditor"
        r["results"][0]["drawCallsAvg"] = 0
        self.assertEqual(evaluate(r)["status"], "INCOMPLETE")

    def test_missing_duplicate_and_invalid_metrics(self):
        r = fixture()
        r["results"] = [r["results"][0], r["results"][0], r["results"][1]]
        r["results"][2]["fpsAvg"] = float("nan")
        self.assertEqual(evaluate(r)["status"], "INCOMPLETE")

    def test_fps_inconsistency_and_low_visibility(self):
        r = fixture()
        r["results"][2]["visibleAvg"] = 1
        r["results"][2]["frameMsAvg"] = 5
        self.assertEqual(evaluate(r)["status"], "INCOMPLETE")

    def test_60_fps_is_separate_target(self):
        self.assertEqual(evaluate(fixture(), 60)["status"], "SCREEN_FAIL")


if __name__ == "__main__":
    unittest.main()
