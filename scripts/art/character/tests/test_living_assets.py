"""Unit tests for the Blender-free parts of U1-CHAR.3: archetype classification and batch filename parsing.

  python -m unittest discover -s scripts/art/character/tests -v
"""
import json
import sys
import unittest
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent))
import batch_import as bi  # noqa: E402
import build_archetypes as ba  # noqa: E402

ARCH = json.loads(bi.ARCHETYPES.read_text(encoding='utf-8'))
IDS = [s['id'] for s in ARCH['species']]
CATS = json.loads(bi.BUDGETS.read_text(encoding='utf-8'))['propCategories']['categories']


def sp(loc, sil, body='Dragon'):
    return {'id': 'x', 'canonical': {'locomotion': loc, 'silhouetteVi': sil, 'bodyStyle': body}}


class Archetypes(unittest.TestCase):
    def test_generated_file_is_current(self):
        self.assertEqual(ba.main(['--check']), 0)

    def test_sixteen_species_and_no_shared_emberling_rig(self):
        self.assertEqual(len(IDS), 16)
        self.assertGreater(len({s['archetype'] for s in ARCH['species']}), 3)

    def test_negation_and_comparison_are_not_features(self):
        self.assertFalse(ba.has_word('thân không cánh', 'cánh'))
        self.assertFalse(ba.has_word('vây như cánh', 'cánh'))
        self.assertTrue(ba.has_word('hai cánh lớn', 'cánh'))

    def test_rules(self):
        self.assertEqual(ba.classify(sp(['walk', 'fly'], 'x'))['archetype'], 'winged')
        self.assertEqual(ba.classify(sp(['swim'], 'x', 'Serpent'))['aquaticVariant'], True)
        self.assertEqual(ba.classify(sp(['swim'], 'x'))['archetype'], 'aquatic')
        self.assertEqual(ba.classify(sp(['walk'], 'dáng bốn chân'))['archetype'], 'quadruped')
        w = ba.classify(sp(['walk'], 'mập mạp'))
        self.assertEqual((w['archetype'], w['requiresInput']), ('walker', ['limbCount']))

    def test_clips_follow_locomotion(self):
        c = ba.classify(sp(['walk', 'burrow'], 'bốn chân'))['clips']
        self.assertIn('Burrow', c)
        self.assertNotIn('Fly', c)
        self.assertNotIn('Swim', c)
        for base in ('Idle', 'Eat', 'Sleep', 'Happy'):
            self.assertIn(base, c)


class Naming(unittest.TestCase):
    def c(self, stem):
        return bi.classify(stem, IDS, CATS)

    def test_dragon_with_stage(self):
        self.assertEqual(self.c('Emberling_Adult_v3'), {'kind': 'dragon', 'species': 'emberling', 'stage': 'Adult',
                                                         'stageFromName': True})

    def test_dragon_without_stage(self):
        self.assertEqual(self.c('hunyuan-zephyr-001')['stage'], 'Unstaged')

    def test_props_win_over_species(self):
        r = self.c('emberling_statue')
        self.assertEqual((r['kind'], r['category']), ('prop', 'dragon_statue'))

    def test_prop_id_strips_prefixes(self):
        r = self.c('SAMPLE_prop_treasure_chest')
        self.assertEqual((r['category'], r['propId'], r['collider']), ('chest', 'treasure_chest', 'box'))

    def test_unrecognized_and_ambiguous(self):
        self.assertEqual(self.c('mystery_object')['kind'], 'unrecognized')
        self.assertEqual(self.c('emberling_vs_zephyr')['kind'], 'unrecognized')

    def test_substring_is_not_a_token(self):
        self.assertEqual(self.c('rockemberling')['kind'], 'unrecognized')


if __name__ == '__main__':
    unittest.main()
