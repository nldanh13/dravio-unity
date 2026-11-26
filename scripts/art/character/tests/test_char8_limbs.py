"""CHAR.8: limb measurement on real (chibi, winged, off-centre) generated models — synthetic point clouds, no Blender."""
import sys
import unittest
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
import rig_geometry as rg  # noqa: E402

RNG = np.random.default_rng(7)


def paw(x, y, r=0.07, n=400):
    a = RNG.uniform(0, 2 * np.pi, n)
    d = r * np.sqrt(RNG.uniform(0, 1, n))
    return np.c_[x + d * np.cos(a), y + d * np.sin(a), RNG.uniform(0, 0.02, n)]


def body(x0=0.0, n=4000):
    return np.c_[RNG.uniform(-0.25, 0.25, n) + x0, RNG.uniform(-0.8, 0.8, n), RNG.uniform(0.25, 1.0, n)]


def limbs(points):
    return rg.Analysis(np.vstack(points)).detect_limbs()


class Char8Limbs(unittest.TestCase):
    def test_well_separated_feet_still_measure_four(self):
        r = limbs([body(), paw(-0.18, -0.5), paw(0.18, -0.5), paw(-0.18, 0.4), paw(0.18, 0.4)])
        self.assertEqual((r['limbCountDetected'], r['confidence']), (4, 'high'))

    def test_front_paws_standing_close_together_are_split_at_the_empty_band(self):
        # chibi stance: front paws 6 cm apart -> one grid cluster straddling the midline before CHAR.8
        r = limbs([body(), paw(-0.10, -0.5), paw(0.10, -0.5), paw(-0.18, 0.4), paw(0.18, 0.4)])
        self.assertEqual((r['limbCountDetected'], r['confidence']), (4, 'high'))

    def test_belly_contact_without_a_gap_is_not_split(self):
        belly = np.c_[RNG.uniform(-0.2, 0.2, 800), RNG.uniform(-0.1, 0.1, 800), RNG.uniform(0, 0.02, 800)]
        r = limbs([body(), belly, paw(-0.18, -0.5), paw(0.18, -0.5), paw(-0.18, 0.4), paw(0.18, 0.4)])
        self.assertEqual(r['limbCountDetected'], 4)
        self.assertEqual(r['groundContactsMidline'], 1)

    def test_small_tail_tip_off_midline_is_not_a_fifth_foot_and_off_centre_models_stay_symmetric(self):
        x0 = 0.05   # model not centred on x = 0
        tail = paw(x0 + 0.09, 0.75, r=0.03, n=60)
        r = limbs([body(x0), paw(x0 - 0.15, -0.5), paw(x0 + 0.15, -0.5), paw(x0 - 0.16, 0.4), paw(x0 + 0.16, 0.4), tail])
        self.assertEqual((r['limbCountDetected'], r['confidence']), (4, 'high'))


if __name__ == '__main__':
    unittest.main()
