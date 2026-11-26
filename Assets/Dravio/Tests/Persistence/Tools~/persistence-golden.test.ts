/**
 * Golden vectors cho U2.2 (persistence) từ TypeScript gốc nldanh13/dravio @ b33ec7a3.
 *   cp persistence-golden.test.ts <dravio>/tests/unit/zz-persistence-golden.test.ts
 *   GOLDEN_OUT=<abs>/ts-persistence-golden-b33ec7a.json npx vitest run tests/unit/zz-persistence-golden.test.ts
 * Thư mục kết thúc bằng "~" bị Unity bỏ qua.
 */
import { readFileSync, writeFileSync } from 'node:fs';
import { it } from 'vitest';
import { getSpecies } from '../../src/data/dragons';
import { fixedClock } from '../../src/engine/core/clock';
import { dispatch } from '../../src/engine/core/commands';
import { createInitialState } from '../../src/engine/core/initialState';
import { createRng } from '../../src/engine/core/rng';
import { parseSaveText, sanitizeImportedState, previewOf } from '../../src/persistence/saveFile';
import { canonicalJson, createEnvelope, serializeSave, sha256Hex, toPersistedState } from '../../src/persistence/schema';
import { advanceModeFor } from '../../src/platform/timePolicy';
import type { DragonInstance } from '../../src/types/game';
import type { GameState } from '../../src/types/state';

const T0 = Date.UTC(2026, 10, 1, 8, 0, 0);
const fresh = (seed = 7): GameState => createInitialState({ saveId: `save-${seed}`, nowMs: T0, rng: createRng(seed) });

function adult(uid: string, speciesId: string, extra: Partial<DragonInstance>): DragonInstance {
  return { uid, speciesId, genotype: { pigment: ['R', 'r'], pattern: ['P', 'p'], eye: ['B', 'b'] }, mutation: 'normal', xpSec: getSpecies(speciesId).growthTimeSec,
    locked: false, location: { kind: 'storage' }, hatchedAtMs: 0, personality: 'calm', bond: 0, parentIds: null, generation: 1, ancestry: 'unknown',
    origin: 'pack', activity: 'idle', breedCooldownUntilMs: 0, ...extra };
}

function rich(): GameState {
  let s = fresh(11);
  const clock = fixedClock(T0);
  const step = (cmd: Record<string, unknown>, adv = 0) => {
    clock.advance(adv);
    const r = dispatch(s, cmd, { clock });
    if (!r.ok) throw new Error(`${String(cmd.type)}: ${r.error.code}`);
    s = r.state;
  };
  const uid = s.dragons[0]!.uid;
  step({ type: 'advanceTime', requestId: 'a1', mode: 'online' }, 123_457);
  step({ type: 'renameDragon', requestId: 'n1', dragonUid: uid, nickname: 'Lửa \u{1F525} "Nhỏ" \\ / <b>' });
  step({ type: 'pet', requestId: 'p1', dragonUid: uid });
  step({ type: 'feed', requestId: 'f1', dietId: 'fruit', zoneId: 'origin', habitatId: 'origin_meadow' }, 1000);
  step({ type: 'advanceTime', requestId: 'o1', mode: 'offline' }, 3_600_000);
  return {
    ...s,
    discoveries: ['DISC_AQUATIC_HABITAT'],
    zones: s.zones.map((z) => (z.id === 'origin' ? { ...z, unlockedHabitatIds: ['origin_meadow', 'origin_pondbank'], decorationIds: ['deco_a'], activeDecorationIds: ['deco_a'] } : z)),
    dragons: [...s.dragons,
      adult('dr1', 'droplet', { xpSec: 100.5, mutation: 'golden', location: { kind: 'zone', zoneId: 'origin', habitatId: 'origin_pondbank' }, bond: 99, bondDay: '2026-11-01', bondDaily: 3, nickname: 'x\uD800y', lastPetAtMs: T0 }),
      adult('ga1', 'gaia', { xpSec: 1.000001, location: { kind: 'storage' }, parentIds: ['p-a', 'p-b'], grandparentIds: ['g1', 'g2', 'g3'], generation: 2, ancestry: 'known', origin: 'breed', registryRef: 'reg-1' }),
      adult('sh1', 'sprout', { location: { kind: 'shelter', zoneId: 'origin' } }),
      adult('ca1', 'zephyr', { location: { kind: 'care', caseId: 'case-1' }, activity: 'care' }),
      adult('re1', 'celestia', { location: { kind: 'released' }, mutation: 'prismatic' }),
    ],
    inventory: { herb: 3, 'z-item': 0 },
    upgrades: { ...s.upgrades, foodQualityLevel: 2, islandCapacityLevel: 1 },
    pityCount: 17,
    settings: { ...s.settings, language: 'en', sfxVolume: 0.35, bgmVolume: 1, fpsLimit: 30, timeMode: 'night', compactMode: true, mode: 'zen', difficulty: 'cozy' },
    tutorial: { status: 'skipped', completed: ['TUT_01', 'TUT_02'], startedAtMs: T0 + 5, profilesViewed: ['droplet', 'emberling'], starterPackUsed: true },
    pendingPack: { id: 'pk1', packId: 'starter', speciesId: 'sprout', genotype: { pigment: ['r', 'r'], pattern: ['P', 'P'], eye: ['B', 'b'] }, mutation: 'normal', createdAtMs: T0 + 9, tutorial: true },
    unlockedAchievements: ['ACH_FIRST_HATCH', 'ACH_FIRST_ADULT'],
    claimedAchievements: ['ACH_FIRST_HATCH'],
  };
}

it('writes persistence golden vectors', async () => {
  const numbers = [0, -0, 1, -1, 0.1, 0.2, 0.1 + 0.2, 0.8, 0.6, 0.35, 1 / 3, 2 / 3, 100.5, 167.999, 1.000001, 1e21, 1e20, 123456789012345680000, 1e-6, 1e-7, 1.5e-7,
    5e-324, 1.7976931348623157e308, 9007199254740991, 9007199254740993, 2 ** 70, 4.70625, 12345.678e10, -1.25e-10, 0.000123, 1e300, 25e-5, NaN, Infinity, -Infinity];
  const r = createRng(424242);
  for (let i = 0; i < 400; i++) {
    const e = Math.floor(r.next() * 60) - 30;
    numbers.push((r.next() - 0.5) * 10 ** e);
  }
  for (let i = 0; i < 100; i++) numbers.push(Math.floor(r.next() * 2 ** 53));
  const strings = ['', 'abc', 'Lửa Nhỏ', '"q"', '\\', '/', '\b\f\n\r\t', '\u0000\u0001\u001f\u007f\u0080', '\u2028\u2029', '\uFEFF', '\uD800', '\uDFFF', 'a\uD83D', '\uDE00b', '\u{1F525}', '<script>', 'x\u200Dy'];
  const states = [fresh(1), fresh(7), fresh(65535), rich()];
  const envelopes = [];
  for (const [i, s] of states.entries()) {
    const persisted = toPersistedState(s);
    const canonical = canonicalJson(persisted);
    envelopes.push({ name: `state-${i}`, state: JSON.parse(JSON.stringify(s)), canonical, checksum: await sha256Hex(canonical), text: await serializeSave(s, T0 + 1000 + i) });
  }
  const base = await createEnvelope(fresh(), T0);
  const baseText = JSON.stringify(base);
  const withState = async (state: Record<string, unknown>, env: Record<string, unknown> = {}) =>
    JSON.stringify({ ...base, ...env, state, checksum: await sha256Hex(canonicalJson(state)) });
  const { tutorial: _t, pendingPack: _p, unlockedAchievements: _u, claimedAchievements: _c, ...v2 } = base.state;
  void [_t, _p, _u, _c];
  const { discoveries: _d, ...v1 } = v2;
  void _d;
  const parseCases: { name: string; text: string }[] = [
    { name: 'valid-v4', text: baseText },
    { name: 'valid-pretty', text: JSON.stringify(base, null, 2) },
    { name: 'truncated', text: baseText.slice(0, 100) },
    { name: 'empty-text', text: '' },
    { name: 'bom', text: '\uFEFF' + baseText },
    { name: 'array', text: '[1,2]' },
    { name: 'string', text: '"dravio-save"' },
    { name: 'null', text: 'null' },
    { name: 'number', text: '42' },
    { name: 'foreign-object', text: '{"hello":1}' },
    { name: 'legacy-shape', text: JSON.stringify({ version: 2, gold: 10, dragons: [] }) },
    { name: 'wrong-format', text: JSON.stringify({ ...base, format: 'other' }) },
    { name: 'version-string', text: JSON.stringify({ ...base, schemaVersion: '4' }) },
    { name: 'version-fraction', text: JSON.stringify({ ...base, schemaVersion: 4.5 }) },
    { name: 'missing-checksum', text: JSON.stringify({ ...base, checksum: undefined }) },
    { name: 'state-array', text: JSON.stringify({ ...base, state: [] }) },
    { name: 'envelope-state-version-mismatch', text: JSON.stringify({ ...base, schemaVersion: 5 }) },
    { name: 'tampered-gold', text: JSON.stringify({ ...base, state: { ...base.state, goldMicro: 999_000_000_000 } }) },
    { name: 'checksum-uppercase', text: JSON.stringify({ ...base, checksum: base.checksum.toUpperCase() }) },
    { name: 'fractional-gold', text: await withState({ ...base.state, goldMicro: 1.5 }) },
    { name: 'negative-gold', text: await withState({ ...base.state, goldMicro: -1 }) },
    { name: 'string-gold', text: await withState({ ...base.state, goldMicro: '150000000' }) },
    { name: 'dragons-not-array', text: await withState({ ...base.state, dragons: {} }) },
    { name: 'unknown-species', text: await withState({ ...base.state, dragons: base.state.dragons.map((d) => ({ ...d, speciesId: 'dragonite' })) }) },
    { name: 'v5-newer', text: await withState({ ...base.state, schemaVersion: 5 }, { schemaVersion: 5 }) },
    { name: 'v99-newer', text: await withState({ ...base.state, schemaVersion: 99 }, { schemaVersion: 99 }) },
    { name: 'v0-unsupported', text: await withState({ ...base.state, schemaVersion: 0 }, { schemaVersion: 0 }) },
    { name: 'vneg-unsupported', text: await withState({ ...base.state, schemaVersion: -1 }, { schemaVersion: -1 }) },
    { name: 'v1', text: await withState({ ...v1, schemaVersion: 1 }, { schemaVersion: 1 }) },
    { name: 'v2', text: await withState({ ...v2, schemaVersion: 2 }, { schemaVersion: 2 }) },
    { name: 'v3-prerelease', text: await withState({ ...v1, schemaVersion: 3 }, { schemaVersion: 3 }) },
    { name: 'v1-with-discoveries', text: await withState({ ...v2, schemaVersion: 1 }, { schemaVersion: 1 }) },
    { name: 'v2-with-tutorial', text: await withState({ ...base.state, schemaVersion: 2 }, { schemaVersion: 2 }) },
    { name: 'v1-invalid-after-migration', text: await withState({ ...v1, schemaVersion: 1, goldMicro: -5 }, { schemaVersion: 1 }) },
    { name: 'v1-open-pondbank-backfill', text: await withState({ ...v1, schemaVersion: 1, zones: base.state.zones.map((z) => (z.id === 'origin' ? { ...z, unlockedHabitatIds: ['origin_meadow', 'origin_pondbank'] } : z)) }, { schemaVersion: 1 }) },
    { name: 'duplicate-keys-last-wins', text: baseText.replace('{"format":"dravio-save"', '{"format":"nope","format":"dravio-save"') },
    { name: 'unicode-escaped-keys', text: baseText.replace('"format"', '"\\u0066ormat"').replace('"goldMicro"', '"gold\\u004dicro"') },
    { name: 'exponent-number', text: baseText.replace('"goldMicro":150000000', '"goldMicro":1.5e8') },
    { name: 'negative-zero', text: baseText.replace('"goldRemainder":0', '"goldRemainder":-0') },
    { name: 'infinite-gold', text: baseText.replace('"goldMicro":150000000', '"goldMicro":1e400').replace(base.checksum, await sha256Hex(canonicalJson({ ...base.state, goldMicro: null }))) },
    { name: 'missing-savedAt', text: JSON.stringify({ ...base, savedAtMs: undefined }) },
    { name: 'fractional-savedAt', text: JSON.stringify({ ...base, savedAtMs: 12.5 }) },
    { name: 'unknown-state-keys', text: await withState({ ...base.state, entitlements: { removeAds: true }, purchases: ['astral'] }) },
    { name: 'unknown-dragon-keys', text: await withState({ ...base.state, dragons: base.state.dragons.map((d) => ({ ...d, registryRef: 'reg-1', verified: true })) }) },
    { name: 'trailing-comma', text: baseText.replace(/}$/, ',}') },
    { name: 'single-quotes', text: "{'format':'dravio-save'}" },
    { name: 'leading-zero', text: baseText.replace('"revision":0', '"revision":00') },
    { name: 'nan-literal', text: baseText.replace('"goldRemainder":0', '"goldRemainder":NaN') },
    { name: 'raw-control-char', text: baseText.replace('"saveId":"save-7"', '"saveId":"save\u0001-7"') },
    { name: 'trailing-garbage', text: baseText + 'x' },
    { name: 'surrounding-whitespace', text: ' \n\t' + baseText + '\r\n ' },
    { name: 'rich-state', text: envelopes[3]!.text },
  ];
  const fixtureV1 = readFileSync('tests/fixtures/save-schema-v1.json', 'utf8');
  const fixtureV2 = readFileSync('tests/fixtures/save-schema-v2.json', 'utf8');
  parseCases.push({ name: 'fixture-v1', text: fixtureV1 }, { name: 'fixture-v2', text: fixtureV2 });
  const parsed = [];
  for (const c of parseCases) {
    const res = await parseSaveText(c.text);
    parsed.push(res.ok
      ? { name: c.name, text: c.text, ok: true, savedAtMs: res.save.savedAtMs, migratedFrom: res.save.migratedFrom, state: JSON.parse(JSON.stringify(res.save.state)), canonicalAfter: canonicalJson(res.save.state) }
      : { name: c.name, text: c.text, ok: false, code: res.code, details: res.details });
  }
  const forged = parsed.find((p) => p.name === 'unknown-dragon-keys')!;
  const importReports = [];
  for (const name of ['unknown-state-keys', 'unknown-dragon-keys', 'rich-state']) {
    const res = await parseSaveText(parseCases.find((c) => c.name === name)!.text);
    if (!res.ok) throw new Error(name);
    const sz = sanitizeImportedState(res.save.state);
    importReports.push({ name, report: sz.report, preview: previewOf({ ...res.save, state: sz.state }), state: JSON.parse(JSON.stringify(sz.state)) });
  }
  void forged;
  const policy = [[0, 1000, true], [0, 5000, true], [0, 5001, true], [0, 3_600_000, true], [0, 1000, false], [10_000, 5_000, true]].map(([a, b, v]) => ({ last: a, now: b, visible: v, mode: advanceModeFor(a as number, b as number, v as boolean) }));
  const golden = {
    source: 'nldanh13/dravio@b33ec7a3dc98b4a74857fe92d143475028fc0d96',
    numbers: numbers.map((n) => ({ bits: Buffer.from(new Float64Array([n]).buffer).toString('hex'), json: JSON.stringify(n) })),
    strings: strings.map((s) => ({ codeUnits: Array.from({ length: s.length }, (_, i) => s.charCodeAt(i)), json: JSON.stringify(s) })),
    envelopes,
    parsed,
    importReports,
    policy,
  };
  writeFileSync(process.env.GOLDEN_OUT ?? 'persistence-golden.json', JSON.stringify(golden));
});
