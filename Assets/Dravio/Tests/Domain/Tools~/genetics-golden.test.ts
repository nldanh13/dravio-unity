/**
 * Golden vectors U2.5 — chỉ phần ĐÃ CÓ trong TypeScript gốc nldanh13/dravio @ b33ec7a3 (hàm thuần, chưa có lệnh):
 * alleles.ts, genetics/breeding.ts, economy/mutation.ts, economy/packs.ts.
 *   cp genetics-golden.test.ts <dravio>/tests/unit/zz-genetics-golden.test.ts
 *   GOLDEN_OUT=<abs>/ts-genetics-golden-b33ec7a.json npx vitest run tests/unit/zz-genetics-golden.test.ts
 */
import { writeFileSync } from 'node:fs';
import { it } from 'vitest';
import { getPack } from '../../src/engine/economy/packs';
import { DRAGON_SPECIES, getSpecies } from '../../src/data/dragons';
import { LOCI } from '../../src/data/genetics';
import { createRng, hashSeed } from '../../src/engine/core/rng';
import { derivePhenotype, locusDistribution, rollOffspringGenotype } from '../../src/engine/genetics/alleles';
import { areRelated, breedFeeG, candidateSpecies, checkBreed, hatchSeconds, previewBreed, queryBreedPreview, rollBreed, speciesDistribution } from '../../src/engine/genetics/breeding';
import { mutationDistribution, rollMutation } from '../../src/engine/economy/mutation';
import { rollPackSpecies } from '../../src/engine/economy/packs';
import { RARITY_ORDER, type DragonInstance, type Genotype } from '../../src/types/game';
import { adult } from './fixtures';

const pairs = (l: (typeof LOCI)[number]) => {
  const [D, r] = l.alleles;
  return [[D, D], [D, r], [r, r]] as [string, string][];
};
const mapArr = <K, V>(m: Map<K, V>) => [...m.entries()];

it('writes genetics golden vectors', () => {
  const locus = LOCI.flatMap((l) => pairs(l).flatMap((a) => pairs(l).map((b) => ({ locus: l.id, a, b, dist: mapArr(locusDistribution(l.id, a, b)).map(([k, v]) => ({ key: k, pair: v.pair, p: v.p })) }))));
  const genotypes: Genotype[] = [];
  for (const x of pairs(LOCI[0]!)) for (const y of pairs(LOCI[1]!)) for (const z of pairs(LOCI[2]!)) genotypes.push({ pigment: x, pattern: y, eye: z });
  const phenotype = genotypes.map((g) => ({ genotype: g, phenotype: derivePhenotype(g, ['pigment', 'pattern', 'eye', 'horn']) }));
  phenotype.push({ genotype: { pigment: ['R', 'r'] }, phenotype: derivePhenotype({ pigment: ['R', 'r'] }, ['pigment', 'pattern', 'eye']) });

  const speciesPairs = DRAGON_SPECIES.flatMap((a) => DRAGON_SPECIES.map((b) => ({
    a: a.id, b: b.id, candidates: candidateSpecies(a, b).map((s) => s.id), distribution: mapArr(speciesDistribution(a, b)), feeG: breedFeeG(a, b),
  })));
  const hatch = RARITY_ORDER.flatMap((r) => [0, 1, 2, 3, 4, 5, 6].map((L) => ({ rarity: r, level: L, sec: hatchSeconds(r, L) })));
  const mutationDist = [0, 1, 2, 3, 4, 5, 6, 7, 8].flatMap((L) => [0, 1, 2].map((b) => ({ L, b, d: mutationDistribution(L, b as 0 | 1 | 2) })));

  const mutationRolls = [1, 7, 42, 2026].map((seed) => {
    const rng = createRng(seed);
    const out = [];
    let pity = seed === 42 ? 190 : 0;
    for (let i = 0; i < 400; i++) {
      const L = i % 9; const b = (i % 3) as 0 | 1 | 2;
      const r = rollMutation(rng, L, b, pity);
      out.push([L, b, pity, r.mutation, r.pityCount]);
      pity = r.pityCount;
    }
    return { seed, rolls: out, after: rng.next() };
  });
  const packRolls = ['starter', 'elemental', 'mystic', 'astral'].flatMap((id) => [3, 99].map((seed) => {
    const rng = createRng(hashSeed('pack-golden', id, String(seed)));
    const rolls = Array.from({ length: 300 }, () => rollPackSpecies(rng, getPack(id)));
    return { packId: id, seed: hashSeed('pack-golden', id, String(seed)), rolls, after: rng.next() };
  }));

  const g = (p: string, q: string, e: string): Genotype => ({ pigment: [p[0]!, p[1]!], pattern: [q[0]!, q[1]!], eye: [e[0]!, e[1]!] });
  const breedPairs: [DragonInstance, DragonInstance][] = [
    [adult('a', 'emberling', { genotype: g('Rr', 'Pp', 'Bb') }), adult('b', 'sprout', { genotype: g('rr', 'pp', 'bb') })],
    [adult('a', 'magma', { genotype: g('Rr', 'Pp', 'bb') }), adult('b', 'sprout', { genotype: g('rr', 'Pp', 'Bb') })],
    [adult('a', 'solaris', { genotype: g('RR', 'PP', 'BB') }), adult('b', 'aurorawing', { genotype: g('rr', 'pp', 'bb') })],
    [adult('a', 'leviathan', { genotype: g('Rr', 'pp', 'Bb') }), adult('b', 'gaia', { genotype: g('Rr', 'Pp', 'Bb') })],
    [adult('a', 'droplet', { genotype: g('RR', 'Pp', 'bb') }), adult('b', 'mossback', { genotype: g('Rr', 'PP', 'Bb') })],
    [adult('a', 'zephyr', { genotype: g('Rr', 'Pp', 'Bb') }), adult('b', 'sparkwing', { genotype: g('Rr', 'Pp', 'Bb') })],
  ];
  const breeds = breedPairs.map(([a, b], i) => {
    const rng = createRng(hashSeed('breed-golden', String(i)));
    const p = previewBreed(a, b);
    return {
      a, b,
      preview: { species: mapArr(p.species), loci: Object.entries(p.loci).map(([id, m]) => [id, mapArr(m).map(([k, v]) => ({ key: k, pair: v.pair, p: v.p }))]), feeG: p.feeG },
      rolls: Array.from({ length: 200 }, () => rollBreed(rng, a, b)),
      after: rng.next(),
    };
  });
  const offspring = (() => {
    const rng = createRng(77);
    return Array.from({ length: 100 }, (_, i) => rollOffspringGenotype(rng, g('Rr', 'Pp', 'Bb'), i % 2 ? g('rr', 'Pp', 'bb') : { pigment: ['R', 'r'], eye: ['b', 'b'] }, ['pigment', 'pattern', 'eye']));
  })();

  const base = adult('base', 'emberling', { parentIds: ['pa1', 'pa2'], grandparentIds: ['ga1', 'ga2'] });
  const checks: [string, DragonInstance, DragonInstance, number][] = [
    ['ok', base, adult('y', 'sprout'), 0],
    ['self', base, base, 0],
    ['diff-group', base, adult('y', 'droplet'), 0],
    ['shared-parent', base, adult('y', 'sprout', { parentIds: ['pa1', 'zz'] }), 0],
    ['shared-grandparent', base, adult('y', 'sprout', { grandparentIds: ['ga2'] }), 0],
    ['direct-ancestor', base, adult('pa1', 'sprout'), 0],
    ['grandparent', base, adult('ga1', 'sprout'), 0],
    ['child', base, adult('y', 'sprout', { parentIds: ['base', 'q'] }), 0],
    ['grandchild', base, adult('y', 'sprout', { grandparentIds: ['base'] }), 0],
    ['released', base, adult('y', 'sprout', { location: { kind: 'released' } }), 0],
    ['shelter', base, adult('y', 'sprout', { location: { kind: 'shelter', zoneId: 'origin' } }), 0],
    ['care', base, adult('y', 'sprout', { location: { kind: 'care', caseId: 'c1' } }), 0],
    ['not-adult', base, adult('y', 'sprout', { xpSec: 479 }), 0],
    ['adult-boundary', base, adult('y', 'sprout', { xpSec: 480 }), 0],
    ['breeding', base, adult('y', 'sprout', { activity: 'breeding' }), 0],
    ['expedition', base, adult('y', 'sprout', { activity: 'expedition' }), 0],
    ['cooldown', base, adult('y', 'sprout', { breedCooldownUntilMs: 10 }), 5],
    ['cooldown-over', base, adult('y', 'sprout', { breedCooldownUntilMs: 10 }), 10],
    ['bad-allele', base, adult('y', 'sprout', { genotype: { pigment: ['X', 'r'], pattern: ['P', 'p'], eye: ['B', 'b'] } }), 0],
    ['unnormalized', base, adult('y', 'sprout', { genotype: { pigment: ['r', 'R'], pattern: ['P', 'p'], eye: ['B', 'b'] } }), 0],
    ['locked-storage', base, adult('y', 'sprout', { locked: true, location: { kind: 'storage' } }), 0],
    ['unknown-ancestry', adult('a', 'emberling', { ancestry: 'unknown' }), adult('b', 'sprout', { ancestry: 'unknown' }), 0],
    ['legendary-needs-two-epic', adult('a', 'solaris'), adult('b', 'sparkwing'), 0],
  ];
  const checkOut = checks.map(([name, a, b, now]) => ({ name, a, b, now, check: checkBreed(a, b, now), related: areRelated(a, b), query: queryBreedPreview(a, b, now).ok }));

  writeFileSync(process.env.GOLDEN_OUT ?? 'genetics-golden.json', JSON.stringify({
    source: 'nldanh13/dravio@b33ec7a3dc98b4a74857fe92d143475028fc0d96',
    locus, phenotype, speciesPairs, hatch, mutationDist, mutationRolls, packRolls, breeds, offspring, checks: checkOut,
    species: DRAGON_SPECIES.map((s) => ({ id: s.id, packEligible: s.packEligible })),
  }));
});
