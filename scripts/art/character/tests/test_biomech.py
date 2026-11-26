"""U1-CHAR.7b: published biomechanics relations used by the rig pipeline (BIOMECHANICS_RESEARCH.md). Python only.

  python -m unittest discover -s scripts/art/character/tests -v
"""
import json
import math
import re
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
import rig_geometry as rg  # noqa: E402

REPO = Path(__file__).resolve().parents[4]
PROFILES = json.loads((REPO / 'Assets/Dravio/Character/Data/rig-profiles.json').read_text(encoding='utf-8'))


class Froude(unittest.TestCase):
    def test_alexander_relation_round_trips(self):
        for stride, h in ((0.146, 0.206), (0.35, 0.206), (1.5, 0.9)):
            fr, v, period = rg.froude_cadence(stride, h)
            self.assertAlmostEqual(stride / h, 2.3 * fr ** 0.3, places=9)
            self.assertAlmostEqual(fr, v * v / (9.81 * h), places=9)
            self.assertAlmostEqual(period * v, stride, places=9)

    def test_dynamic_similarity_scales_with_size(self):
        # geometrically similar animals (same relative stride) share Fr; the period grows as sqrt(h)
        fr1, _, t1 = rg.froude_cadence(0.3, 0.2)
        fr2, _, t2 = rg.froude_cadence(1.2, 0.8)
        self.assertAlmostEqual(fr1, fr2, places=9)
        self.assertAlmostEqual(t2 / t1, math.sqrt(4.0), places=9)

    def test_longer_relative_stride_means_faster_gait(self):
        self.assertLess(rg.froude_cadence(0.15, 0.2)[0], rg.froude_cadence(0.35, 0.2)[0])

    def test_only_erect_legged_archetypes_opt_in(self):
        A = PROFILES['archetypes']
        self.assertEqual(A['quadruped']['walk'].get('cadence'), 'froude')
        self.assertEqual(A['quadruped']['run'].get('cadence'), 'froude')
        self.assertNotIn('cadence', A['serpentine']['walk'], 'long bodies: undulation adds to the stride')
        self.assertNotIn('cadence', A['serpentine']['run'])


class Strouhal(unittest.TestCase):
    def test_lengths_per_cycle_gives_the_target_strouhal(self):
        amp, L, T = 0.32, 1.63, 32 / 30
        lpc = rg.strouhal_lengths_per_cycle(amp, L, 0.3)
        speed = lpc * L / T
        self.assertAlmostEqual(rg.strouhal(amp, speed, T), 0.3, places=9)

    def test_target_inside_the_cruising_band(self):
        st = PROFILES['motionDefaults']['strouhal']
        self.assertTrue(0.2 <= st <= 0.4, 'Taylor et al. 2003: cruising swimmers and flyers 0.2-0.4')

    def test_swim_amplitude_band_is_the_fish_band(self):
        self.assertEqual(PROFILES['motionDefaults']['swimAmplitudeBandL'], [0.1, 0.3], 'Saadat et al. 2017')

    def test_strouhal_of_nothing_is_zero(self):
        self.assertEqual(rg.strouhal(0.3, 0.0, 1.0), 0.0)


class CoordinatedTurn(unittest.TestCase):
    def test_rate_at_the_bank_limit(self):
        w = rg.coordinated_turn_rate_deg(2.0, 30)
        self.assertAlmostEqual(math.tan(math.radians(30)), 2.0 * math.radians(w) / 9.81, places=9)
        self.assertGreater(rg.coordinated_turn_rate_deg(1.0, 30), w, 'slower flight turns tighter')

    def test_bank_limit_matches_the_runtime_solver(self):
        cs = (REPO / 'Assets/Dravio/Character/Runtime/Behavior/DragonMotionContract.cs').read_text(encoding='utf-8')
        runtime = float(re.search(r'MaxBankDeg\s*=\s*([0-9.]+)f', cs).group(1))
        self.assertEqual(PROFILES['motionDefaults']['maxBankDeg'], runtime)
        g = float(re.search(r'Gravity\s*=\s*([0-9.]+)f', cs).group(1))
        self.assertEqual(g, rg.G)


class FlightProfiles(unittest.TestCase):
    def test_takeoff_is_leg_driven_and_landing_flares(self):
        w = PROFILES['archetypes']['winged']
        self.assertGreaterEqual(w['takeoff']['launchShare'], 0.8)
        self.assertLessEqual(w['takeoff']['launchShare'], 0.95)
        self.assertGreater(w['land']['flarePitch'], 0.0)
        self.assertLess(math.degrees(w['land']['flarePitch']), 30.0)


if __name__ == '__main__':
    unittest.main()
