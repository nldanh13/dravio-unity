"""U1-CHAR.5: build_character_reports.py (Python only).

  python -m unittest discover -s scripts/art/character/tests -v
"""
import json
import sys
import tempfile
import unittest
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent))
import build_character_reports as br  # noqa: E402


def _write(p, obj):
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(json.dumps(obj), encoding='utf-8')


def _lods(src):
    return {'source': src, 'source_unchanged': True, 'lods': [
        {'level': i, 'triangles': t, 'vertices': t // 2, 'in_budget': True} for i, t in enumerate((24000, 10000, 4000))]}


class Reports(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.work = Path(self.tmp.name) / 'work'
        a = self.work / 'assets'
        # SAMPLE emberling, rigged, but missing its Run clip (to prove the biology comparison reports it)
        n = 'DRG_emberling_Baby'
        _write(a / n / f'{n}_rig.json', {
            'species': 'emberling', 'stage': 'Baby', 'archetype': 'quadruped', 'rigVersion': 3, 'limbCount': 4,
            'skin': {'bones': 26, 'deform_bones': 26, 'max_influences': 4, 'unweighted_vertices': 0},
            'clips': {c: {'start': 0, 'end': 30, 'loop': c != 'Eat', 'groundSpeed': 0.14 if c == 'Walk' else 0.0}
                      for c in ('Idle', 'Eat', 'Sleep', 'Happy', 'Walk')},
            'source_unchanged': True})
        _write(a / n / f'{n}_lods.json', _lods('/x/SAMPLE_emberling_Baby.glb'))
        _write(a / n / f'{n}_prepare.json', {'source': '/x/SAMPLE_emberling_Baby.glb', 'source_unchanged': True,
                                              'texture_set': {'base_color': f'{n}_BaseColor.png'}})
        _write(a / n / f'{n}_animqa.json', {'status': 'PASS_WITH_WARNINGS', 'clips': {'Sleep': {'deformation': {'compressP001': 0.44}}},
                                             'warnings': [{'clip': 'Sleep', 'check': 'deformation', 'message': 'edge compression over limit', 'value': 0.44, 'limit': 0.45}]})
        _write(a / n / f'{n}.validation.json', {'status': 'PASS', 'items': []})
        # real (non-SAMPLE) magma, static: no rig
        m = 'DRG_magma_Adult'
        _write(a / m / f'{m}_lods.json', _lods('B:/Dravio/magma_Adult.glb'))
        _write(a / m / f'{m}_prepare.json', {'source': 'B:/Dravio/magma_Adult.glb', 'source_unchanged': True})
        self.out = Path(self.tmp.name) / 'out'
        self.assertEqual(br.main([str(self.work), str(self.out)]), 0)

    def tearDown(self):
        self.tmp.cleanup()

    def test_evidence_labels_sample_and_real(self):
        rows = json.loads((self.out / 'prefab-catalog.json').read_text(encoding='utf-8'))
        ev = {r['name']: r['evidence'] for r in rows}
        self.assertEqual(ev['DRG_emberling_Baby'], 'SAMPLE')
        self.assertIn('chưa nghiệm thu', ev['DRG_magma_Adult'], 'a real model is never reported as accepted')
        self.assertIn('**SAMPLE**', (self.out / 'qa' / 'DRG_emberling_Baby.md').read_text(encoding='utf-8'))

    def test_catalog_paths_and_static_model(self):
        rows = {r['name']: r for r in json.loads((self.out / 'prefab-catalog.json').read_text(encoding='utf-8'))}
        e = rows['DRG_emberling_Baby']
        self.assertEqual(e['prefab'], 'Assets/Dravio/Character/Prefabs/DRG_emberling_Baby_Rigged_LODGroup.prefab')
        self.assertEqual(e['overrideController'], 'Assets/Dravio/Character/Animation/Overrides/DRG_emberling_Baby.overrideController')
        self.assertEqual(e['lodTriangles'], [24000, 10000, 4000])
        m = rows['DRG_magma_Adult']
        self.assertFalse(m['rigged'])
        self.assertNotIn('overrideController', m, 'static model: no Animator')
        md = (self.out / 'prefab-catalog.md').read_text(encoding='utf-8')
        self.assertIn('emberling/Juvenile', md, 'stage without a model is listed as a fallback gap')

    def test_animation_report_flags_clip_missing_against_biology(self):
        md = (self.out / 'animation-report.md').read_text(encoding='utf-8')
        row = next(l for l in md.splitlines() if l.startswith('| DRG_emberling_Baby'))
        expected = next(s['clips'] for s in json.loads(br.ARCHETYPES.read_text(encoding='utf-8'))['species'] if s['id'] == 'emberling')
        missing = [c.strip() for c in row.split('|')[5].split(',')]
        self.assertEqual(missing, [c for c in expected if c not in ('Idle', 'Eat', 'Sleep', 'Happy', 'Walk')])
        self.assertIn('Run', missing, 'emberling walks: Run expected by its biology clip set')
        page = (self.out / 'qa' / 'DRG_emberling_Baby.md').read_text(encoding='utf-8')
        self.assertIn('| Sleep |', page)
        self.assertIn('WARNING', page)

    def test_performance_is_labelled_as_estimate(self):
        md = (self.out / 'performance-budget.md').read_text(encoding='utf-8')
        self.assertIn('Không phải số đo trên thiết bị', md)
        self.assertIn('| 30 |', md)

    def test_refuses_out_equal_work_and_missing_assets(self):
        with self.assertRaises(SystemExit):
            br.main([str(self.work), str(self.work)])
        with self.assertRaises(SystemExit):
            br.main([str(Path(self.tmp.name) / 'nothing'), str(self.out)])


if __name__ == '__main__':
    unittest.main()
