"""CHAR.9c (issue #95) step 2: the other 15 species (no Blender needed).

Guards the reusable tooling (tune_repair.py, repair_chibi_rig.py pinTransitions/new amplitude groups) and what the
species report claims: limits unchanged, every species welded (0 seam cracks), each committed repair config is the
one that was measured, and a clip that still fails keeps its authored amplitude (the tuner never flattens a motion
it cannot fix)."""
import ast
import json
import sys
import unittest
from pathlib import Path

HERE = Path(__file__).resolve().parents[1]
REPO = Path(__file__).resolve().parents[4]
OVERRIDES = REPO / 'Assets/Dravio/Character/Data/asset-overrides.char8-production.json'
BUDGETS = REPO / 'Assets/Dravio/Character/Data/character-budgets.json'
REPORT = REPO / 'scripts/art/character/reports_char9c/species_tune'
sys.path.insert(0, str(HERE))
import tune_repair  # noqa: E402  (pure python, no bpy)


def module_const(path, name):
    for node in ast.parse(path.read_text(encoding='utf-8')).body:
        if isinstance(node, ast.Assign) and any(getattr(t, 'id', None) == name for t in node.targets):
            return ast.literal_eval(node.value)
    raise AssertionError(name)


class TuneRepairTool(unittest.TestCase):
    def test_bone_to_group(self):
        g = tune_repair.group_of
        self.assertIsNone(g('Pelvis', 'body'))
        self.assertIsNone(g('Root', 'root'))
        self.assertEqual(g('Spine4', 'body'), 'body')
        self.assertEqual(g('Chest', 'body'), 'body')
        self.assertEqual(g('HindLower_L', 'hindLeg'), 'legs')
        self.assertEqual(g('Neck', 'neck'), 'head')
        self.assertEqual(g('WingUpper_R', 'wing'), 'wings')
        self.assertEqual(g('Tail1', 'tail'), 'tail')
        self.assertEqual(g('Fin_L', 'fin'), 'fins')

    def test_pelvis_falls_through_to_next_bone(self):
        worst = [{'bone': 'Pelvis', 'region': 'body'}, {'bone': 'ForeUpper_L', 'region': 'foreLeg'},
                 {'bone': 'HindLower_R', 'region': 'hindLeg'}, {'bone': 'Tail1', 'region': 'tail'}]
        self.assertEqual(tune_repair.target_groups(worst), ['legs', 'tail'])

    def test_transition_table_matches_anim_qa(self):
        qa = module_const(HERE / 'anim_qa.py', 'TRANSITIONS')
        rep = module_const(HERE / 'repair_chibi_rig.py', 'TRANSITION_ENDS')
        self.assertEqual([t[:4] for t in qa], [tuple(t) for t in rep])

    def test_repair_new_options_are_opt_in(self):
        src = (HERE / 'repair_chibi_rig.py').read_text(encoding='utf-8')
        self.assertIn("CFG.get('pinTransitions', False)", src)
        self.assertIn("not n.startswith('Wing')", src)  # wing bones are not legs
        self.assertIn("pb.name not in ('Root', 'Pelvis')", src)
        # Emberling (approved) does not use them: its config is unchanged
        rep = json.loads(OVERRIDES.read_text(encoding='utf-8'))['species']['emberling']['repair']
        self.assertNotIn('pinTransitions', rep)


class SpeciesReport(unittest.TestCase):
    def setUp(self):
        if not (REPORT / 'summary.json').exists():
            self.skipTest('species report not generated')
        self.sum = json.loads((REPORT / 'summary.json').read_text(encoding='utf-8'))
        self.ov = json.loads(OVERRIDES.read_text(encoding='utf-8'))['species']

    def test_limits_not_loosened(self):
        lim = json.loads(BUDGETS.read_text(encoding='utf-8'))['animationQa']
        self.assertEqual((lim['maxStretchP999'], lim['minCompressP001'], lim['maxGroundPenetrationRel']), (1.6, 0.45, 0.01))
        for sp, row in self.sum['species'].items():
            qa = json.loads((REPORT / f'DRG_{sp}_Unstaged_animqa.json').read_text(encoding='utf-8'))
            self.assertEqual(qa['limits'], lim, sp)

    def test_fifteen_species_welded_and_committed(self):
        self.assertEqual(len(self.sum['species']), 15)
        for sp, row in self.sum['species'].items():
            self.assertEqual(row['seamGapRelMax'], 0.0, sp)
            self.assertEqual(self.ov[sp]['repair'], row['config'], sp)
            self.assertEqual(row['config']['skinning'], 'linear')
            self.assertIs(row['config']['seamWeld'], True)
            self.assertIs(row['config']['pinTransitions'], True)

    def test_measured_every_frame_and_failures_reported(self):
        for sp, row in self.sum['species'].items():
            qa = json.loads((REPORT / f'DRG_{sp}_Unstaged_animqa.json').read_text(encoding='utf-8'))
            lim = qa['limits']
            fails = []
            for n, c in qa['clips'].items():
                d = c['deformation']
                self.assertEqual(d['framesMeasured'], c['frames'][1] - c['frames'][0] + 1, (sp, n))
                if d['worstFrameStretchP999'] > lim['maxStretchP999'] or d['worstFrameCompressP001'] < lim['minCompressP001']:
                    fails.append(n)
            self.assertEqual(sorted(fails), row['failingClips'], sp)
            # the tuner never keeps a reduced amplitude on a clip it could not fix
            for n in fails:
                self.assertNotIn(n, row['config'].get('amplitude', {}), (sp, n))
            if not fails:
                self.assertNotEqual(row['verdict'], 'NEEDS_ART', sp)


if __name__ == '__main__':
    unittest.main()
