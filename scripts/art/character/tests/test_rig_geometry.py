"""Unit tests for the Blender-free parts of U1-CHAR.4: geometric landmark/limb detection, skin weight helpers,
gait tracks, batch source checks and per-file overrides, and the before/after QA comparison.

  python -m unittest discover -s scripts/art/character/tests -v
"""
import json
import sys
import tempfile
import unittest
from pathlib import Path

import numpy as np

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent))
import batch_import as bi  # noqa: E402
import compare_animqa as cq  # noqa: E402
import rig_geometry as rg  # noqa: E402

RNG = np.random.default_rng(7)


def ellipsoid(c, r, n):
    u = RNG.normal(size=(n, 3))
    u /= np.linalg.norm(u, axis=1, keepdims=True)
    return np.asarray(c) + u * np.asarray(r)


def cylinder(x, y, z0, z1, r, n):
    t = RNG.uniform(0, 2 * np.pi, n)
    z = RNG.uniform(z0, z1, n)
    return np.c_[x + r * np.cos(t), y + r * np.sin(t), z]


def quadruped(legs=True):
    """Front = -Y, ground z = 0: body, head ahead and up, tail behind, four legs (or none)."""
    parts = [ellipsoid((0, 0, 0.45), (0.2, 0.35, 0.18), 3000),
             ellipsoid((0, -0.5, 0.7), (0.12, 0.13, 0.12), 900),
             cylinder(0, -0.35, 0.5, 0.68, 0.07, 400),           # neck
             cylinder(0, 0.45, 0.35, 0.42, 0.06, 300) + [0, 0, 0],  # tail stub
             ellipsoid((0, 0.7, 0.3), (0.04, 0.25, 0.04), 400)]   # tail
    if legs:
        for x in (-0.13, 0.13):
            for y in (-0.22, 0.22):
                parts.append(cylinder(x, y, 0.0, 0.35, 0.05, 500))
                parts.append(np.c_[x + RNG.uniform(-0.05, 0.05, 80), y + RNG.uniform(-0.05, 0.05, 80), np.zeros(80)])
    else:
        parts[0] = ellipsoid((0, 0, 0.18), (0.2, 0.35, 0.18), 3000)  # belly on the ground
    return np.vstack(parts)


class Geometry(unittest.TestCase):
    def test_four_feet_detected_with_high_confidence(self):
        an = rg.Analysis(quadruped())
        lim = an.detect_limbs()
        self.assertEqual(lim['limbCountDetected'], 4)
        self.assertEqual(lim['confidence'], 'high')
        self.assertEqual(sorted(lim['feet']), ['Fore_L', 'Fore_R', 'Hind_L', 'Hind_R'])
        self.assertLess(lim['feet']['Fore_L'][1], lim['feet']['Hind_L'][1], 'fore feet are in front (-Y)')
        self.assertLess(lim['feet']['Fore_L'][0], 0)

    def test_belly_on_ground_is_not_a_foot(self):
        lim = rg.Analysis(quadruped(legs=False)).detect_limbs()
        self.assertEqual(lim['limbCountDetected'], 0)
        self.assertGreaterEqual(lim['groundContactsMidline'], 1)

    def test_landmarks_follow_the_body_plan(self):
        an = rg.Analysis(quadruped())
        an.run('quadruped', 4, {}, None)
        lm = an.landmarks
        self.assertLess(lm['headCenter'][1], lm['chest'][1], 'head ahead of the chest')
        self.assertLess(lm['chest'][1], lm['pelvis'][1])
        self.assertGreater(lm['tailTip'][1], lm['pelvis'][1], 'tail behind the pelvis')
        self.assertGreater(lm['headCenter'][2], lm['chest'][2] - 0.05)
        counts = an.summary()['regionCounts']
        for region in ('head', 'body', 'tail', 'leg'):
            self.assertGreater(counts[region], 50, region)

    def test_manual_landmarks_win(self):
        an = rg.Analysis(quadruped())
        an.run('quadruped', 4, {'headCenter': [0.0, -0.6, 0.75]}, None)
        self.assertTrue(np.allclose(an.landmarks['headCenter'], [0.0, -0.6, 0.75]))

    def test_main_component_drops_floating_fragment(self):
        edges = [(0, 1), (1, 2), (2, 3), (4, 5)]
        m = rg.main_component_mask(6, edges, min_fraction=0.4)
        self.assertEqual(m.tolist(), [True, True, True, True, False, False])


class Weights(unittest.TestCase):
    def test_chain_weights_are_continuous_and_normalised(self):
        joints = [[0, 0, 1.0], [0, 0, 0.5], [0, 0, 0.0], [0, -0.3, 0.0]]
        names = ['Upper', 'Lower', 'Foot']
        pts = np.c_[np.full(41, 0.08), np.zeros(41), np.linspace(1.0, 0.0, 41)]
        W = rg.chain_weights(pts, joints, names, parent='Pelvis')
        for w in W:
            self.assertAlmostEqual(sum(w.values()), 1.0, places=6)
            self.assertLessEqual(len(w), 4)
        lower = [w.get('Lower', 0.0) for w in W]
        self.assertLess(max(abs(a - b) for a, b in zip(lower, lower[1:])), 0.35, 'no hard step along the leg')
        self.assertGreater(W[0].get('Pelvis', 0), 0.1, 'top of the leg blends into the parent')
        self.assertNotIn('Pelvis', W[30], 'parent never reaches the shin')

    def test_border_mask_and_masked_smoothing(self):
        labels = np.array(['body', 'body', 'tail', 'tail'], dtype=object)
        edges = np.array([[0, 1], [1, 2], [2, 3]])
        band = rg.border_mask(labels, edges, rings=0, pair=('body', 'tail'))
        self.assertEqual(band.tolist(), [False, True, True, False])
        self.assertFalse(rg.border_mask(labels, edges, rings=0, pair=('body', 'leg')).any())
        W = np.array([[1, 0], [1, 0], [0, 1], [0, 1]], float)   # Pelvis | Tail1
        A = np.ones_like(W)
        S = rg.smooth_weights(W, A, edges, iterations=4, alpha=0.5, active=band)
        self.assertTrue(np.allclose(S.sum(axis=1), 1.0))
        self.assertTrue(np.allclose(S[0], W[0]) and np.allclose(S[3], W[3]), 'outside the band unchanged')
        self.assertTrue(0 < S[1, 1] < 1 and 0 < S[2, 0] < 1, 'border blended')
        A2 = A.copy()
        A2[1, 1] = 0.0
        S2 = rg.smooth_weights(W, A2, edges, iterations=4, alpha=0.5, active=band)
        self.assertEqual(S2[1, 1], 0.0, 'a bone never leaks where it is not allowed')

    def test_limit_and_normalize(self):
        w = rg.limit_and_normalize({'a': 0.5, 'b': 0.3, 'c': 0.1, 'd': 0.05, 'e': 0.05}, 4)
        self.assertEqual(len(w), 4)
        self.assertAlmostEqual(sum(w.values()), 1.0)
        self.assertNotIn('e', w) if 'd' in w else self.assertNotIn('d', w)

    def test_root_is_never_a_skin_target(self):
        bones = {'Root', 'Pelvis', 'Spine', 'Chest', 'Neck', 'Head', 'TailRoot', 'Tail1',
                 'ForeUpper_L', 'ForeLower_L', 'ForeFoot_L'}
        for region in ('body', 'leg', 'tail', 'head', 'neck'):
            allowed = rg.allowed_bones(region, bones, leg_key='Fore_L' if region == 'leg' else None)
            self.assertNotIn('Root', allowed, region)
        self.assertNotIn('ForeFoot_L', rg.allowed_bones('body', bones), 'distal leg bones never on the body')
        self.assertNotIn('ForeUpper_L', rg.allowed_bones('head', bones))


class Gait(unittest.TestCase):
    def test_stance_moves_backwards_at_constant_speed(self):
        stride, duty = 0.2, 0.6
        ys = [rg.foot_track(t, 0.0, duty, stride, 0.05) for t in np.linspace(0, duty, 13)]
        self.assertTrue(all(dz == 0.0 for _, dz in ys), 'planted during stance')
        dy = np.diff([y for y, _ in ys])
        self.assertTrue(np.all(dy > 0), 'stance foot moves +Y (backwards): the body walks forwards')
        self.assertTrue(np.allclose(dy, dy[0], rtol=1e-6), 'constant speed: no sliding against the ground')
        swing = [rg.foot_track(t, 0.0, duty, stride, 0.05)[1] for t in np.linspace(duty + 0.01, 0.99, 9)]
        self.assertTrue(all(z > 0 for z in swing), 'foot lifted in swing')
        y0, _ = rg.foot_track(0.0, 0.0, duty, stride, 0.05)
        y1, _ = rg.foot_track(1.0, 0.0, duty, stride, 0.05)
        self.assertAlmostEqual(y0, y1, places=6, msg='cycle closes (loop)')

    def test_walk_phases_are_four_beat(self):
        ph = rg.foot_phases('walk', ['Fore_L', 'Fore_R', 'Hind_L', 'Hind_R'])
        self.assertEqual(len(set(round(v, 3) for v in ph.values())), 4)
        trot = rg.foot_phases('trot', ['Fore_L', 'Fore_R', 'Hind_L', 'Hind_R'])
        self.assertAlmostEqual(trot['Fore_L'], trot['Hind_R'])


class Batch(unittest.TestCase):
    def test_source_folder_errors_are_clear(self):
        with tempfile.TemporaryDirectory() as d:
            d = Path(d)
            with self.assertRaises(SystemExit) as e:
                bi.check_source_dir(d / 'missing')
            self.assertIn('not found', str(e.exception))
            (d / 'notes.txt').write_text('x')
            with self.assertRaises(SystemExit) as e:
                bi.check_source_dir(d)
            self.assertIn('No .glb', str(e.exception))
            (d / 'sub').mkdir()
            (d / 'sub' / 'Emberling_Baby.GLB').write_bytes(b'glTF')
            self.assertEqual([p.name for p in bi.check_source_dir(d)], ['Emberling_Baby.GLB'])

    def test_alias_maps_request_names(self):
        ids = [s['id'] for s in json.loads(bi.ARCHETYPES.read_text(encoding='utf-8'))['species']]
        cats = json.loads(bi.BUDGETS.read_text(encoding='utf-8'))['propCategories']['categories']
        aliases = {k: v for k, v in json.loads(bi.PROFILES.read_text(encoding='utf-8'))['fileNameAliases'].items()
                   if not k.startswith('_')}
        info = bi.classify('Sproutling_Juvenile', ids, cats, aliases)
        self.assertEqual((info['kind'], info['species'], info['stage']), ('dragon', 'sprout', 'Juvenile'))
        self.assertEqual(bi.classify('Sproutling_Juvenile', ids, cats)['kind'], 'unrecognized')

    def test_file_override_wins_and_landmarks_resolve(self):
        ov = {'species': {'zephyr': {'limbCount': 2, 'yawOffset': 0}},
              'files': {'zephyr_Adult_v2': {'limbCount': 'auto', 'landmarks': 'lm/zephyr.json', '_note': 'x'}}}
        info = {'kind': 'dragon', 'species': 'zephyr', 'stage': 'Adult'}
        base = Path('/tmp/ovdir')
        m = bi.asset_overrides(ov, info, 'zephyr_Adult_v2.glb', base)
        self.assertEqual(m['limbCount'], 'auto')
        self.assertEqual(m['yawOffset'], 0)
        self.assertEqual(Path(m['landmarks']), (base / 'lm/zephyr.json').resolve())
        self.assertNotIn('_note', m)
        self.assertEqual(bi.asset_overrides(ov, info, 'zephyr_Baby.glb', base)['limbCount'], 2)

    def test_pipeline_hash_tracks_rig_and_config(self):
        h = bi.pipeline_hash()
        self.assertEqual(h, bi.pipeline_hash())
        with tempfile.TemporaryDirectory() as d:
            extra = Path(d) / 'landmarks.json'
            extra.write_text('{}')
            self.assertNotEqual(h, bi.pipeline_hash([extra]))
        for name in ('rig_archetype.py', 'rig_geometry.py', 'anim_qa.py'):
            self.assertIn(name, bi.PIPELINE_FILES)


class Compare(unittest.TestCase):
    def test_regressions_stay_visible(self):
        lim = {'maxStretchP999': 1.6, 'minCompressP001': 0.45}

        def rep(name, s, c, warn):
            return {'name': name, 'status': 'PASS_WITH_WARNINGS' if warn else 'PASS', 'limits': lim,
                    'clips': {'Walk': {'deformation': {'stretchP999': s, 'compressP001': c, 'worstBones': []}}},
                    'warnings': [{'clip': 'Walk', 'check': 'deformation', 'message': 'm', 'value': s, 'limit': 1.6}] if warn else []}
        md, data = cq.compare({'A': rep('A', 1.2, 0.8, False)}, {'A': rep('A', 1.9, 0.8, True), 'B': rep('B', 1.1, 0.9, False)},
                              'v1', 'v2')
        self.assertIn('**1.900**', md, 'a value over the limit is highlighted even when it got worse')
        self.assertIn('không có (chưa rig)', md)
        self.assertEqual(data[0]['clips']['Walk']['stretchAfter'], 1.9)


if __name__ == '__main__':
    unittest.main()
