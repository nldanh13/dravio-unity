"""CHAR.8 data consistency (no Blender needed): Aurorawing blockout palette = canonical palette, egg prop category,
Sanctuary style samples match the Domain project catalog (id, world, kind) and carry the integration metadata."""
import ast
import json
import re
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
CHAR = REPO / 'Assets/Dravio/Character/Data'
SANC = REPO / 'Assets/Dravio/Environment/Sanctuary/Data'


def module_constant(path, name):
    tree = ast.parse(path.read_text(encoding='utf-8'))
    for node in tree.body:
        if isinstance(node, ast.Assign) and any(getattr(t, 'id', None) == name for t in node.targets):
            return ast.literal_eval(node.value)
    raise KeyError(name)


class AurorawingBlockout(unittest.TestCase):
    def test_palette_is_the_canonical_species_palette(self):
        spec = json.loads((CHAR / 'species-character-spec.json').read_text(encoding='utf-8'))
        species = spec['species'] if isinstance(spec, dict) else spec
        aw = next(s for s in species if s['id'] == 'aurorawing')
        palette = module_constant(REPO / 'scripts/art/character/make_aurorawing_review.py', 'PALETTE')
        self.assertEqual({k: v.upper() for k, v in palette.items()}, {k: v.upper() for k, v in aw['canonical']['palette'].items()})
        self.assertEqual(aw['canonical']['locomotion'], ['walk', 'fly'], 'archetype winged comes from canonical locomotion')

    def test_stages_grow_and_young_have_bigger_heads(self):
        st = module_constant(REPO / 'scripts/art/character/make_aurorawing_review.py', 'STAGES')
        self.assertEqual(list(st), ['Baby', 'Juvenile', 'Adult'])
        self.assertLess(st['Baby']['height'], st['Juvenile']['height'])
        self.assertLess(st['Juvenile']['height'], st['Adult']['height'])
        self.assertGreaterEqual(st['Baby']['head'], st['Juvenile']['head'])
        self.assertGreaterEqual(st['Juvenile']['head'], st['Adult']['head'])

    def test_egg_category_exists_and_local_overrides_flag_proposals(self):
        budgets = json.loads((CHAR / 'character-budgets.json').read_text(encoding='utf-8'))
        egg = [c for c in budgets['propCategories']['categories'] if c['category'] == 'egg']
        self.assertEqual(len(egg), 1)
        self.assertEqual((egg[0]['profile'], egg[0]['collider']), ('prop_small', 'convex'))
        ov = json.loads((CHAR / 'asset-overrides.char8-local.json').read_text(encoding='utf-8'))
        self.assertEqual(ov['species']['aurorawing']['limbCount'], 'auto', 'never a guessed GDD number')
        self.assertIn('PROPOSAL', ov['_comment'])


class SanctuaryStyleSamples(unittest.TestCase):
    def domain_catalog(self):
        src = (REPO / 'Assets/Dravio/Domain/Catalog/SanctuaryProjectContent.cs').read_text(encoding='utf-8')
        rows = re.findall(r'new SanctuaryProjectDef\("([a-z_]+)", "[^"]*", "([a-z]+)", ProjectFootprint\.(\w+)', src)
        return {pid: (world, kind.lower()) for pid, world, kind in rows}

    def test_samples_match_domain_ids_worlds_and_kinds(self):
        cat = self.domain_catalog()
        self.assertEqual(len(cat), 20)
        files = sorted(SANC.glob('*.sanctuary.json'))
        self.assertEqual(len(files), 3, 'one style sample per world')
        worlds = set()
        for f in files:
            m = json.loads(f.read_text(encoding='utf-8'))
            self.assertIn(m['projectId'], cat)
            self.assertEqual((m['world'], m['kind']), cat[m['projectId']])
            self.assertEqual(m['prefabId'], 'PF_SANC_' + m['projectId'])
            self.assertTrue((SANC.parent / m['model']).exists(), m['model'])
            tris = [l['triangles'] for l in m['lods']]
            self.assertEqual(len(tris), 3)
            self.assertTrue(tris[0] > tris[1] > tris[2] > 0)
            self.assertLessEqual(tris[0], m['lod0Budget'])
            self.assertEqual(m['uvErrors'], 0)
            self.assertIn('ANCHOR_Approach', m['anchors'])
            self.assertIn('ANCHOR_VFX_Complete', m['anchors'])
            self.assertEqual(sorted(m['notDecidedHere']), ['buffs', 'buildMaterials', 'cost', 'rewards'])
            worlds.add(m['world'])
        self.assertEqual(worlds, {'origin', 'ocean', 'sky'})


if __name__ == '__main__':
    unittest.main()
