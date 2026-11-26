"""U1-CHAR.7: advanced motion clip rules from canonical biology, the owner's GLB file names, and the flat motion
metadata handed to Unity. Python only.

  python -m unittest discover -s scripts/art/character/tests -v
"""
import json
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
import batch_import as bi  # noqa: E402
import build_archetypes as ba  # noqa: E402

REPO = Path(__file__).resolve().parents[4]
BIO = json.loads((REPO / 'Assets/Dravio/Data/species-biology.json').read_text(encoding='utf-8'))


class MotionClipRules(unittest.TestCase):
    def setUp(self):
        self.sp = {s['id']: s for s in ba.build()['species']}

    def test_swim_clips_follow_respiration_and_locomotion(self):
        for sid, s in self.sp.items():
            loc, resp, clips = BIO[sid]['locomotion'], BIO[sid]['respiration'], s['clips']
            swims = 'swim' in loc
            self.assertEqual('SwimIdle' in clips, swims, sid)
            self.assertEqual('SwimRest' in clips, swims and resp in ('gill', 'dual'), sid)
            self.assertEqual('SwimSurface' in clips, swims and resp in ('lung', 'dual'), sid)
            self.assertEqual('WaterEnter' in clips and 'WaterExit' in clips, swims and 'walk' in loc, sid)
        self.assertNotIn('WaterEnter', self.sp['frostfin']['clips'], 'never leaves the water')
        self.assertNotIn('SwimIdle', self.sp['emberling']['clips'], 'no swimming for a non-swimmer')

    def test_perch_only_where_the_silhouette_states_perching_feet(self):
        perchers = {sid for sid, s in self.sp.items() if 'Perch' in s['clips']}
        self.assertEqual(perchers, {sid for sid, b in BIO.items() if 'fly' in b['locomotion'] and 'chân đậu' in b['silhouetteVi'].lower()})
        self.assertEqual(perchers, {'zephyr', 'blossom'})
        self.assertNotIn('Perch', self.sp['sparkwing']['clips'], 'flies but no perching feet stated')

    def test_burrow_phases_only_for_burrowers_and_greet_for_all(self):
        for sid, s in self.sp.items():
            burrows = 'burrow' in BIO[sid]['locomotion']
            for c in ('DigPrep', 'BurrowEnter', 'BurrowEmerge'):
                self.assertEqual(c in s['clips'], burrows, f'{sid} {c}')
            self.assertIn('Greet', s['clips'])

    def test_motion_clips_come_after_older_clips(self):
        for s in self.sp.values():
            g = s['clips'].index('Greet')
            for c in ('Idle', 'Eat', 'Sleep', 'Happy', 'Rest', 'Wake', 'LookAround', 'Curious'):
                self.assertLess(s['clips'].index(c), g)


class OwnerFiles(unittest.TestCase):
    def test_numbered_glb_names_map_to_species(self):
        species = [s['id'] for s in ba.build()['species']]
        cats = json.loads((REPO / 'Assets/Dravio/Character/Data/character-budgets.json').read_text(encoding='utf-8'))['propCategories']['categories']
        names = ['01_emberling', '02_droplet', '03_sprout', '04_zephyr', '05_steamdrake', '06_magma', '07_sparkwing']
        got = [bi.classify(n, species, cats) for n in names]
        self.assertEqual([g['species'] for g in got], [n.split('_')[1] for n in names])
        self.assertTrue(all(g['kind'] == 'dragon' and g['stage'] == 'Unstaged' for g in got))

    def test_local_overrides_only_use_auto_for_unstated_limb_counts(self):
        ov = json.loads((REPO / 'Assets/Dravio/Character/Data/asset-overrides.u7-local.json').read_text(encoding='utf-8'))
        arch = {s['id']: s for s in ba.build()['species']}
        for sid, o in ov['species'].items():
            self.assertEqual(o['limbCount'], 'auto', sid)
            self.assertIn('limbCount', arch[sid]['requiresInput'], f'{sid}: only species whose limb count is not stated')


class MotionMetadata(unittest.TestCase):
    def test_flat_block_with_zero_for_not_applicable(self):
        rig = {'motion': {'body': {'length_m': 1.6, 'width_m': 0.5, 'height_m': 0.9},
                          'rootOffsets_m': {'TakeOff.end': 0.39}, 'maxTurnRateDegPerSec': {'ground': 180, 'water': 90, 'air': 60}}}
        qa = {'motionEnvelope': {'flightSpan_m': 1.58, 'swimVerticalExtent_m': None},
              'transitions': [{'poseErrorDeg': 0.06}, {'poseErrorDeg': 0.21}]}
        m = bi.motion_block(rig, qa)
        self.assertEqual((m['bodyLength'], m['takeOffRootOffset'], m['landRootOffset'], m['flightSpan'], m['swimVerticalExtent']), (1.6, 0.39, 0.0, 1.58, 0.0))
        self.assertEqual((m['transitionsChecked'], m['transitionMaxDeg'], m['maxTurnWater']), (2, 0.21, 90.0))
        self.assertTrue(all(not isinstance(v, (dict, list)) for v in m.values()), 'JsonUtility reads no dictionaries')
        self.assertIsNone(bi.motion_block(None, qa), 'static model: no motion block')

    def test_events_and_waterline_are_flat_fields(self):
        rig = {'motion': {'body': {}, 'events': {'TakeOff.liftOff': 0.32, 'Land.touchDown': 0.6, 'WaterEnter.contact': 0.5},
                          'water': {'surfaceWaterline_m': 0.58, 'headAboveWater_m': 0.15}}}
        m = bi.motion_block(rig, {})
        self.assertEqual((m['takeOffLiftOff'], m['landTouchDown'], m['waterEnterContact'], m['waterExitLeave']), (0.32, 0.6, 0.5, 0.0))
        self.assertEqual((m['surfaceWaterline'], m['headAboveWater']), (0.58, 0.15))


class SocialClips(unittest.TestCase):
    def test_nuzzle_for_every_species_and_last(self):
        for s in ba.build()['species']:
            self.assertEqual(s['clips'][-1], 'Nuzzle', s['id'] + ': appended last so older frame ranges stay put')


if __name__ == '__main__':
    unittest.main()
