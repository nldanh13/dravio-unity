"""U1-CHAR.6: behaviour clip sets from canonical locomotion (build_archetypes.py), Python only."""
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
import build_archetypes as ba  # noqa: E402


class BehaviourClips(unittest.TestCase):
    def setUp(self):
        self.sp = {s['id']: s for s in ba.build()['species']}

    def test_every_species_rests_wakes_and_looks(self):
        for s in self.sp.values():
            for c in ('Rest', 'Wake', 'LookAround', 'Curious'):
                self.assertIn(c, s['clips'], s['id'])

    def test_ground_behaviours_only_for_walkers(self):
        for s in self.sp.values():
            walks = 'walk' in s['locomotion']
            for c in ('Drink', 'Stretch', 'Bask'):
                self.assertEqual(c in s['clips'], walks, f"{s['id']} {c}")
        self.assertNotIn('Bask', self.sp['frostfin']['clips'], 'swimmer: no legs to lie on the rocks')
        self.assertNotIn('Drink', self.sp['leviathan']['clips'])

    def test_behaviour_clips_never_add_abilities_and_come_last(self):
        loco = {'Walk', 'Run', 'Swim', 'Fly', 'Burrow'}
        for s in self.sp.values():
            got_loco = [c for c in s['clips'] if c in loco]
            want = [c for l in s['locomotion'] for c in ba.LOCO_CLIPS.get(l, [])]
            self.assertEqual(got_loco, want, s['id'])
            first_behaviour = s['clips'].index('Rest')
            self.assertTrue(all(s['clips'].index(c) < first_behaviour for c in got_loco + ba.BASE_CLIPS),
                            'older clips keep their frame ranges')


if __name__ == '__main__':
    unittest.main()
