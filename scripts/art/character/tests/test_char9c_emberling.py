"""CHAR.9c (issue #95) Emberling rig / skin / animation QA (no Blender needed).

Guards what the report claims: the original GLBs and the CHAR.9b contract FBX are untouched, the candidate FBX in
ArtStaging is the file that was measured, QA limits are unchanged, and the candidate passes the five contract clips
at EVERY frame (worst single frame, linear skinning) after an FBX re-import, plus all 15 rig v7 clips in the batch."""
import ast
import hashlib
import json
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
R9C = REPO / 'scripts/art/character/reports_char9c'
R9B = REPO / 'scripts/art/character/reports_char9b'
CANDIDATE = REPO / 'ArtStaging/Character/Emberling/EmberlingRefinedGLB.fbx'
CONTRACT_FBX = REPO / 'Assets/Dravio/Art/Models/EmberlingRefinedGLB.fbx'
BUDGETS = REPO / 'Assets/Dravio/Character/Data/character-budgets.json'
OVERRIDES = REPO / 'Assets/Dravio/Character/Data/asset-overrides.char8-production.json'
SOURCES = REPO / 'ArtSource/Dragons/images'
CONTRACT = ['Idle', 'Walk', 'Eat', 'Doze', 'Happy']
CONTRACT_BONES = ["Root", "Pelvis", "Chest", "Head", "Tail1", "Tail2", "Flame", "Fore_L", "Fore_R", "Hind_L", "Hind_R",
                  "Eye_L", "Eye_R", "ForeLower_L", "ForeLower_R", "HindLower_L", "HindLower_R",
                  "Spine", "Neck", "Wing_L", "Wing_R", "TailVFX_Anchor"]


def load(d, name):
    return json.loads((d / name).read_text(encoding='utf-8'))


def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()


def limits():
    return json.loads(BUDGETS.read_text(encoding='utf-8'))['animationQa']


class Char9cEmberling(unittest.TestCase):
    def test_sources_and_contract_fbx_untouched(self):
        h = load(R9C, 'HASHES.json')['sha256']
        # CHAR.9b contract FBX in Assets is NOT overwritten (Unity integration keeps using it until approval)
        self.assertEqual(sha(CONTRACT_FBX), load(R9B, 'HASHES.json')['sha256']['Assets/Dravio/Art/Models/EmberlingRefinedGLB.fbx'])
        self.assertEqual(sha(CONTRACT_FBX), h['Assets/Dravio/Art/Models/EmberlingRefinedGLB.fbx'])
        audit = load(R9C, 'source_glb_audit.json')['files']
        self.assertEqual(len(audit), 16)
        for row in audit:
            self.assertEqual(sha(SOURCES / row['file']), row['sha256'], row['file'])

    def test_candidate_is_the_measured_file(self):
        h = load(R9C, 'HASHES.json')['sha256']
        self.assertEqual(sha(CANDIDATE), h['ArtStaging/Character/Emberling/EmberlingRefinedGLB.fbx'])
        self.assertEqual(load(R9C, 'verify_candidate_fbx.json')['sha256'], sha(CANDIDATE))
        self.assertEqual(load(R9C, 'verify_char9b_fbx.json')['sha256'], sha(CONTRACT_FBX))

    def test_limits_not_loosened(self):
        lim = limits()
        self.assertEqual((lim['maxStretchP999'], lim['minCompressP001'], lim['maxGroundPenetrationRel']), (1.6, 0.45, 0.01))
        for rep in ('verify_candidate_fbx.json', 'verify_char9b_fbx.json'):
            got = load(R9C, rep)['limits']
            self.assertEqual(got, {k: lim[k] for k in got}, rep)
        for rep in ('batch_DRG_emberling_Unstaged_animqa_allframes.json', 'export_EmberlingRefinedGLB_animqa_allframes.json'):
            self.assertEqual(load(R9C, rep)['limits'], lim, rep)

    def test_candidate_passes_every_frame_after_reimport(self):
        d = load(R9C, 'verify_candidate_fbx.json')
        lim = limits()
        self.assertEqual(d['skinning'], 'linear')
        self.assertEqual(d['errors'], [])
        self.assertEqual(d['status'], 'PASS')
        self.assertEqual(d['structure']['contractBonesMissing'], [])
        self.assertLessEqual(d['structure']['maxInfluences'], 4)
        self.assertEqual(d['structure']['unweightedVertices'], 0)
        self.assertGreater(d['structure']['seamSplitPairs'], 0)
        self.assertEqual(d['structure']['seamPairMaxWeightL1Diff'], 0.0)  # welded seams: identical weights
        for name in CONTRACT:
            self.assertEqual(d['clips'][name]['seamGap_mm'], 0.0, name)
            c = d['clips'][name]
            a, b = c['unityFrames']
            self.assertEqual(c['framesMeasured'], b - a + 1, name)  # every frame, not 8 samples
            self.assertLessEqual(c['stretchP999'], lim['maxStretchP999'], name)
            self.assertGreaterEqual(c['compressP001'], lim['minCompressP001'], name)
            self.assertLessEqual(c['groundPenetrationRel'], lim['maxGroundPenetrationRel'], name)

    def test_baseline_difference_is_documented(self):
        """CHAR.9b PASS used anim_qa's 8 pooled frames; the same file fails Eat compression at its worst frame."""
        d = load(R9C, 'verify_char9b_fbx.json')
        old = load(R9B, 'reimport_EmberlingRefinedGLB_animqa.json')
        for name in CONTRACT:  # our 8-frame pooled reproduction equals the CHAR.9b numbers
            self.assertAlmostEqual(d['clips'][name]['pooledAnimQa8Frames']['stretchP999'],
                                   old['clips'][name]['deformation']['stretchP999'], delta=0.002)
            self.assertAlmostEqual(d['clips'][name]['pooledAnimQa8Frames']['compressP001'],
                                   old['clips'][name]['deformation']['compressP001'], delta=0.002)
        self.assertLess(d['clips']['Eat']['compressP001'], limits()['minCompressP001'])
        # and the CHAR.9b skin tears open at UV seams (no edge spans a seam, so edge stretch never saw it)
        self.assertGreater(d['clips']['Eat']['seamGap_mm'], 40.0)
        self.assertEqual(d['status'], 'FAIL')

    def test_batch_fifteen_clips_every_frame(self):
        d = load(R9C, 'batch_DRG_emberling_Unstaged_animqa_allframes.json')
        lim = limits()
        self.assertEqual(d['skinning'], 'linear')
        self.assertEqual(d['status'], 'PASS')
        self.assertEqual(d['warnings'], [])
        self.assertEqual(len(d['clips']), 15)
        for name, c in d['clips'].items():
            df = c['deformation']
            self.assertEqual(df['framesMeasured'], c['frames'][1] - c['frames'][0] + 1, name)
            self.assertLessEqual(df['worstFrameStretchP999'], lim['maxStretchP999'], name)
            self.assertGreaterEqual(df['worstFrameCompressP001'], lim['minCompressP001'], name)
            self.assertLessEqual(c.get('groundPenetrationRel') or 0.0, lim['maxGroundPenetrationRel'], name)
            self.assertLessEqual(c.get('footSlideRel') or 0.0, lim['maxFootSlideRel'], name)
            self.assertEqual(c['seamGapRel'], 0.0, name)

    def test_repair_config_emberling_only(self):
        ov = json.loads(OVERRIDES.read_text(encoding='utf-8'))
        rep = ov['species']['emberling']['repair']
        self.assertEqual(rep['skinning'], 'linear')
        self.assertEqual(rep['blendWidth_m'], 0.24)
        self.assertIs(rep['seamWeld'], True)
        self.assertEqual(rep['amplitude']['Run'], {'legs': 0.6})
        others = [k for k, v in ov['species'].items() if k != 'emberling' and 'repair' in v]
        self.assertEqual(others, [])

    def test_contact_clearance_measured_on_candidate(self):
        d = load(R9C, 'contact_clearance_origin.json')
        self.assertTrue(d['fbx'].endswith('ArtStaging/Character/Emberling/EmberlingRefinedGLB.fbx'))
        self.assertEqual(d['runtime']['walkClearance_m'], 0.45)
        # reported finding: the navigator's clearance is smaller than the dragon's reach -> clipping is possible
        self.assertGreater(d['requiredObstacleClearanceAnyYaw_m'], d['runtime']['walkClearance_m'])
        self.assertGreater(d['requiredDragonSpacingAnyYaw_m'], d['runtime']['dragonSpacing_m'])
        for name in CONTRACT:
            self.assertGreater(d['scene'][name]['walkablePoints'], 1000)

    def test_reach_recorded_for_navigation(self):
        d = load(R9C, 'batch_DRG_emberling_Unstaged_animqa_allframes.json')
        for name, c in d['clips'].items():
            r = c['reach_m']
            self.assertGreater(r['radius'], 0.5, name)
            self.assertGreaterEqual(r['radius'], max(r['front'], r['back'], r['side']) - 1e-6, name)
        self.assertAlmostEqual(d['reach_m']['radius'], max(c['reach_m']['radius'] for c in d['clips'].values()))
        # same number the scene clearance tool measured on the FBX (within 1 cm)
        self.assertAlmostEqual(d['reach_m']['front'], load(R9C, 'contact_clearance_origin.json')['requiredObstacleClearanceAnyYaw_m'], delta=0.01)

    def test_anim_qa_default_sampling_unchanged(self):
        src = (REPO / 'scripts/art/character/anim_qa.py').read_text(encoding='utf-8')
        for node in ast.parse(src).body:
            if isinstance(node, ast.Assign) and any(getattr(t, 'id', None) == 'SAMPLES' for t in node.targets):
                self.assertEqual(ast.literal_eval(node.value), 8)  # earlier reports stay comparable
                break
        else:
            self.fail('SAMPLES not found')
        self.assertIn("--strict-frame", src)


if __name__ == '__main__':
    unittest.main()
