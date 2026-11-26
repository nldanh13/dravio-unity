"""CHAR.9b Emberling vertical slice (no Blender needed): the committed EmberlingRefinedGLB.fbx is the file the QA
reports measured, the QA limits were not loosened, the numbers are linear skinning (what Unity plays), and the
five contract clips pass on the export AND on the re-imported FBX."""
import ast
import hashlib
import json
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
REPORTS = REPO / 'scripts/art/character/reports_char9b'
FBX = REPO / 'Assets/Dravio/Art/Models/EmberlingRefinedGLB.fbx'
BUDGETS = REPO / 'Assets/Dravio/Character/Data/character-budgets.json'
OVERRIDES = REPO / 'Assets/Dravio/Character/Data/asset-overrides.char8-production.json'
CONTRACT = ['Idle', 'Walk', 'Eat', 'Doze', 'Happy']


def load(name):
    return json.loads((REPORTS / name).read_text(encoding='utf-8'))


def module_constant(path, name):
    for node in ast.parse(path.read_text(encoding='utf-8')).body:
        if isinstance(node, ast.Assign) and any(getattr(t, 'id', None) == name for t in node.targets):
            return ast.literal_eval(node.value)
    raise KeyError(name)


class EmberlingVerticalSlice(unittest.TestCase):
    def test_committed_fbx_is_the_measured_one(self):
        h = load('HASHES.json')['sha256']
        self.assertEqual(hashlib.sha256(FBX.read_bytes()).hexdigest(), h['Assets/Dravio/Art/Models/EmberlingRefinedGLB.fbx'])

    def test_limits_not_loosened(self):
        lim = json.loads(BUDGETS.read_text(encoding='utf-8'))['animationQa']
        # the CHAR.9b acceptance numbers (issue #76) are exactly the budget file's limits
        self.assertEqual((lim['maxStretchP999'], lim['minCompressP001'], lim['maxGroundPenetrationRel']), (1.6, 0.45, 0.01))
        for rep in ('batch_DRG_emberling_Unstaged_animqa.json', 'export_EmberlingRefinedGLB_animqa.json',
                    'reimport_EmberlingRefinedGLB_animqa.json'):
            self.assertEqual(load(rep)['limits'], lim, rep + ' measured with the current character-budgets.json')

    def test_contract_clips_pass_on_export_and_reimport_with_linear_skinning(self):
        for rep in ('export_EmberlingRefinedGLB_animqa.json', 'reimport_EmberlingRefinedGLB_animqa.json'):
            d = load(rep)
            self.assertEqual(d['skinning'], 'linear', rep)
            self.assertEqual(d['status'], 'PASS', rep)
            self.assertEqual(d['warnings'], [], rep)
            self.assertEqual(sorted(d['clips']), sorted(CONTRACT), rep)
            for name in CONTRACT:
                c = d['clips'][name]
                self.assertLessEqual(c['deformation']['stretchP999'], 1.6, (rep, name))
                self.assertGreaterEqual(c['deformation']['compressP001'], 0.45, (rep, name))
                self.assertLessEqual(c.get('groundPenetrationRel') or 0.0, 0.01, (rep, name))
        a, b = load('export_EmberlingRefinedGLB_animqa.json'), load('reimport_EmberlingRefinedGLB_animqa.json')
        for name in CONTRACT:  # FBX round trip changes nothing measurable
            self.assertAlmostEqual(a['clips'][name]['deformation']['compressP001'],
                                   b['clips'][name]['deformation']['compressP001'], places=2)

    def test_all_fifteen_rig_v7_clips_pass_in_the_batch(self):
        d = load('batch_DRG_emberling_Unstaged_animqa.json')
        self.assertEqual((d['status'], d['skinning'], len(d['clips'])), ('PASS', 'linear', 15))

    def test_contract_take_layout_matches_the_scene_generator(self):
        target = module_constant(REPO / 'scripts/art/character/export_emberling_legacy_contract.py', 'TARGET')
        self.assertEqual([(t[0], t[2], t[3]) for t in target],
                         [('Idle', 1, 181), ('Walk', 182, 217), ('Eat', 218, 278), ('Doze', 279, 399), ('Happy', 400, 445)])
        rig = load('EmberlingRefinedGLB_contract_rig.json')
        self.assertEqual({k: (v['start'], v['end']) for k, v in rig['clips'].items()},
                         {t[0]: (t[2], t[3]) for t in target})
        qa = load('EmberlingRefinedGLB.qa.json')
        for bone in module_constant(REPO / 'scripts/art/character/export_emberling_legacy_contract.py', 'CONTRACT_BONES'):
            self.assertIn(bone, qa['bones'])

    def test_repair_config_is_emberling_only_and_linear(self):
        ov = json.loads(OVERRIDES.read_text(encoding='utf-8'))
        with_repair = [s for s, v in ov['species'].items() if 'repair' in v] + [f for f, v in ov.get('files', {}).items() if 'repair' in v]
        self.assertEqual(with_repair, ['emberling'], 'other species get the repair only after their own QA run')
        rp = ov['species']['emberling']['repair']
        self.assertEqual(rp['skinning'], 'linear')
        for clip, groups in rp['amplitude'].items():
            for g, k in groups.items():
                self.assertIn(g, ('head', 'legs', 'knees'))
                self.assertTrue(0.0 < k <= 1.0, (clip, g, k))


if __name__ == '__main__':
    unittest.main()
