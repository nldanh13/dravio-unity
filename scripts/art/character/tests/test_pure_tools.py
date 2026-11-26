"""Unit tests for the Blender-free tools: validate_character.py and build_species_spec.py.

  python -m unittest discover -s scripts/art/character/tests -v
"""
import copy
import json
import sys
import tempfile
import unittest
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent))
import build_species_spec as bss  # noqa: E402
import validate_character as vc  # noqa: E402

BUDGETS = json.loads(vc.DEFAULT_BUDGETS.read_text(encoding='utf-8'))


def mesh(name, tris, lod=None, uv=True, skin=True, vc_kind='image', normal_cs='Non-Color'):
    return {
        'object': name, 'lod': lod, 'triangles': tris,
        'uv_layers': [{'name': 'UVMap', 'stats': {'faces': tris, 'flipped_faces': 0, 'zero_area_faces': 0,
                                                  'faces_outside_0_1': 0}}] if uv else [],
        'materials': [{'name': 'M', 'base_color': {'kind': vc_kind, 'colorspace': 'sRGB'},
                       'normal': {'kind': 'normal_map', 'source': {'colorspace': normal_cs}}}],
        'topology_welded': {'degenerate_faces': 0, 'loose_vertices': 0, 'non_manifold_edges': 0},
        'skin': {'armature': 'Rig', 'bones': 22, 'max_influences': 4, 'unweighted_vertices': 0,
                 'not_normalized_vertices': 0} if skin else None,
    }


def good_report():
    return {'source': 'x.fbx', 'images': [{'name': 'BC', 'size': [1024, 1024], 'power_of_two': True}],
            'meshes': [mesh('Body_LOD0', 24000, 0), mesh('Body_LOD1', 10000, 1), mesh('Body_LOD2', 4000, 2)]}


def run(report, lods=None):
    r = vc.Result()
    vc.check_inspect(report, BUDGETS, r)
    if lods:
        vc.check_lods(lods, BUDGETS, r)
    return r


def codes(r, level):
    return {i['code'] for i in r.items if i['level'] == level}


class ValidatorTests(unittest.TestCase):
    def test_good_model_passes(self):
        r = run(good_report())
        self.assertEqual(r.errors, [])
        self.assertEqual(r.warnings, [])

    def test_lod0_over_budget_is_error(self):
        rep = good_report()
        rep['meshes'][0]['triangles'] = 25001
        self.assertIn('LOD_OVER_BUDGET', codes(run(rep), 'error'))

    def test_budget_edges_inclusive(self):
        rep = good_report()
        rep['meshes'][0]['triangles'] = 25000
        rep['meshes'][1]['triangles'] = 8000
        rep['meshes'][2]['triangles'] = 3000
        r = run(rep)
        self.assertEqual(r.errors, [])
        self.assertNotIn('LOD_UNDER_TARGET', codes(r, 'warning'))

    def test_under_target_is_warning_not_error(self):
        rep = good_report()
        rep['meshes'][2]['triangles'] = 2000
        r = run(rep)
        self.assertIn('LOD_UNDER_TARGET', codes(r, 'warning'))
        self.assertEqual(r.errors, [])

    def test_non_decreasing_lods(self):
        rep = good_report()
        rep['meshes'][1]['triangles'] = 24000
        self.assertIn('LOD_NOT_DECREASING', codes(run(rep), 'error'))

    def test_missing_uv_and_bad_colorspace(self):
        rep = good_report()
        rep['meshes'][1] = mesh('Body_LOD1', 10000, 1, uv=False, normal_cs='sRGB')
        c = codes(run(rep), 'error')
        self.assertIn('UV_MISSING', c)
        self.assertIn('NORMAL_COLORSPACE', c)

    def test_vertex_color_base_is_warning(self):
        rep = good_report()
        rep['meshes'][0] = mesh('Body_LOD0', 24000, 0, vc_kind='vertex_color')
        self.assertIn('BASECOLOR_VERTEX_COLOR', codes(run(rep), 'warning'))

    def test_skin_rules(self):
        rep = good_report()
        rep['meshes'][0]['skin']['max_influences'] = 5
        rep['meshes'][1]['skin']['armature'] = 'OtherRig'
        rep['meshes'][2]['skin'] = None
        c = codes(run(rep), 'error')
        self.assertTrue({'SKIN_INFLUENCES', 'SKIN_MULTIPLE_ARMATURES', 'SKIN_PARTIAL'} <= c)

    def test_texture_rules(self):
        rep = good_report()
        rep['images'] = [{'name': 'Big', 'size': [4096, 4096], 'power_of_two': True},
                         {'name': 'Odd', 'size': [1000, 1000], 'power_of_two': False},
                         {'name': 'Gone', 'size': [0, 0], 'power_of_two': False}]
        r = run(rep)
        self.assertTrue({'TEXTURE_SIZE', 'TEXTURE_EMPTY'} <= codes(r, 'error'))
        self.assertIn('TEXTURE_NPOT', codes(r, 'warning'))

    def test_single_unnamed_mesh_is_lod0_only(self):
        rep = {'source': 'raw.glb', 'images': [], 'meshes': [mesh('Raw', 60000)]}
        r = run(rep)
        self.assertIn('NO_LOD_NAMING', codes(r, 'warning'))
        self.assertIn('LOD_OVER_BUDGET', codes(r, 'error'))

    def test_lod_report_gates(self):
        lods = {'source_unchanged': True, 'warnings': [], 'lods': [
            {'level': 1, 'rest_deviation': {'p95_rel': 0.02}, 'deformed_p95_rel_worst': 0.001,
             'uv_layers': [['UVMap']], 'texture_drift': None}]}
        c = codes(run(good_report(), lods), 'error')
        self.assertIn('LOD_SHAPE', c)
        lods2 = copy.deepcopy(lods)
        lods2['source_unchanged'] = False
        lods2['lods'][0]['uv_layers'] = [[]]
        c2 = codes(run(good_report(), lods2), 'error')
        self.assertTrue({'SOURCE_CHANGED', 'LOD_UV_LOST'} <= c2)

    def test_cli_exit_codes(self):
        with tempfile.TemporaryDirectory() as d:
            good = Path(d, 'good.json'); good.write_text(json.dumps(good_report()))
            bad_rep = good_report(); bad_rep['meshes'][0]['triangles'] = 99999
            bad = Path(d, 'bad.json'); bad.write_text(json.dumps(bad_rep))
            self.assertEqual(vc.main(['--inspect', str(good)]), 0)
            self.assertEqual(vc.main(['--inspect', str(bad), '--out', str(Path(d, 'o.json'))]), 1)
            self.assertEqual(json.loads(Path(d, 'o.json').read_text())['status'], 'FAIL')
            self.assertEqual(vc.main(['--inspect', str(Path(d, 'missing.json'))]), 2)


class SpeciesSpecTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.spec = bss.build()
        cls.bio = json.loads(bss.BIOLOGY.read_text(encoding='utf-8'))
        cls.by_id = {s['id']: s for s in cls.spec['species']}

    def test_sixteen_species_in_catalog_order(self):
        self.assertEqual(len(self.spec['species']), 16)
        self.assertEqual(self.spec['species'][0]['id'], 'emberling')
        self.assertEqual(set(self.by_id), set(self.bio))

    def test_canonical_fields_verbatim(self):
        for sid, b in self.bio.items():
            c = self.by_id[sid]['canonical']
            for k in ('silhouetteVi', 'locomotion', 'protection', 'respiration', 'palette', 'activity', 'group'):
                self.assertEqual(c[k], b[k], f'{sid}.{k}')

    def test_derived_rules_follow_locomotion(self):
        for sid, b in self.bio.items():
            rig = self.by_id[sid]['rig']
            self.assertEqual(rig['flightWings'], 'fly' in b['locomotion'], sid)
            self.assertEqual(rig['walkingLimbs'], 'walk' in b['locomotion'], sid)
        self.assertFalse(self.by_id['emberling']['rig']['flightWings'])  # "không cánh"
        self.assertFalse(self.by_id['frostfin']['rig']['walkingLimbs'])  # "không chân đi"
        self.assertFalse(self.by_id['leviathan']['rig']['walkingLimbs'])  # "không chi đất"

    def test_every_requirement_cites_a_source_or_is_unspecified(self):
        for s in self.spec['species']:
            for r in s['modelingRequirements']:
                self.assertTrue(r['source'] or r['value'] is None, (s['id'], r))

    def test_stages_never_invent_morphology(self):
        for s in self.spec['species']:
            self.assertEqual([x['stage'] for x in s['stages']], ['Baby', 'Juvenile', 'Adult'])
            for st in s['stages']:
                self.assertIsNone(st['canonicalMorphology'])
                self.assertEqual(st['status'], 'unspecified')
            g = s['canonical']['growthMinutes'] * 60
            self.assertEqual(s['stages'][1]['startsAtGrowthSeconds'], g * 35 // 100)
            self.assertEqual(s['stages'][2]['startsAtGrowthSeconds'], g)

    def test_conflict_is_flagged_not_resolved(self):
        q = ' '.join(self.by_id['frostfin']['openQuestions'])
        self.assertIn('Winged', q)

    def test_committed_files_are_fresh(self):
        self.assertEqual(bss.main(['--check']), 0, 'run build_species_spec.py and commit the output')


if __name__ == '__main__':
    unittest.main()
