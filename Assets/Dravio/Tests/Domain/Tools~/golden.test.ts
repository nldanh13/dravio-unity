/**
 * Sinh golden vectors cho U2.1 từ engine TypeScript gốc (nldanh13/dravio @ b33ec7a3).
 * Cách chạy: xem Assets/Dravio/Domain/README.md, mục "Golden vectors".
 *   cp golden.test.ts <dravio>/tests/unit/zz-golden.test.ts
 *   GOLDEN_OUT=<abs path>/ts-golden-b33ec7a.json npx vitest run tests/unit/zz-golden.test.ts
 * File này không thuộc Unity (thư mục kết thúc bằng "~" bị Unity bỏ qua).
 */
import { writeFileSync } from 'node:fs';
import { it } from 'vitest';
import { DRAGON_SPECIES } from '../../src/data/dragons';
import { HABITAT_PROFILES } from '../../src/data/g0Biology';
import { UPGRADES } from '../../src/data/economy';
import { fixedClock } from '../../src/engine/core/clock';
import { dispatch } from '../../src/engine/core/commands';
import { createInitialState } from '../../src/engine/core/initialState';
import { createRng, hashSeed, pickWeighted, randomInt, pickUniform } from '../../src/engine/core/rng';
import { validateGameState } from '../../src/engine/core/validation';
import { founderGenotype } from '../../src/engine/genetics/alleles';
import { offlineGoldMicro, productionBudget, rateMicroGPerSec, sellPriceG, upgradeCostG } from '../../src/engine/economy/rates';
import { graphemeCount, normalizeNickname } from '../../src/engine/inventory/nickname';
import { speciesSuitability } from '../../src/engine/world/habitat';
import { getSpecies } from '../../src/data/dragons';
import type { GameState } from '../../src/types/state';
import type { DragonInstance } from '../../src/types/game';

const T0 = Date.UTC(2026, 10, 24, 10, 0, 0);
const U21_EVENTS = new Set(['timeAdvanced', 'stageChanged', 'offlineReceiptCreated', 'foodSpawned', 'foodExpired', 'foodConsumed', 'bondChanged', 'dragonPetted', 'dragonRenamed']);
/** Phần state U2.1 sở hữu: bỏ tutorial và achievements (chưa port), vàng trừ thưởng thành tựu. */
function subset(s: GameState, rewards: number, fullIds = true) {
  const { tutorial: _t, unlockedAchievements: _u, claimedAchievements: _c, processedRequestIds: ids, ...rest } = s;
  const processed = fullIds ? { processedRequestIds: ids } : { processedRequestCount: ids.length, processedRequestTail: ids.slice(-3) };
  return JSON.parse(JSON.stringify({ ...rest, ...processed, goldMicro: s.goldMicro - rewards }));
}

const NAMES = ['Ember', '  Lửa Nhỏ <b>  ', '', '   ', 'a'.repeat(24), 'a'.repeat(25), '\u{1F468}\u200D\u{1F469}\u200D\u{1F467}\u200D\u{1F466}'.repeat(24), '\u{1F468}\u200D\u{1F469}\u200D\u{1F467}\u200D\u{1F466}'.repeat(25),
  'Ê'.normalize('NFD').repeat(24), 'Ê'.normalize('NFD').repeat(25), 'a\u0000b', 'abc\u202Edef', 'line\nbreak', '\tTab\t', '\u0085x',
  '\uFEFFBom\uFEFF', '\u00A0nbsp\u00A0', '\u3000全角\u3000', 'x\u200Ey', 'x\u2066y', 'x\uFFFDy', '\u{1F1FB}\u{1F1F3}\u{1F1FB}\u{1F1F3}', '\u{1F44D}\u{1F3FD}'.repeat(24), '한국어', '각',
  'e\u0301'.repeat(25), '\u2028line\u2029', 'Rồng Đỏ', 'x\u200Dy', '\u{1F3F4}\u{E0067}\u{E0062}\u{E0065}\u{E006E}\u{E0067}\u{E007F}', 'क\u094Dषत\u094Dर\u093Fय', null];

type Step = { advance?: number; set?: number; cmd: Record<string, unknown> };
function runScenario(name: string, start: GameState, steps: Step[] | ((s: GameState, i: number) => Step | null)) {
  const clock = fixedClock(T0);
  let state = start;
  let rewards = 0;
  const out = [];
  const next = typeof steps === 'function' ? steps : (_: GameState, i: number) => steps[i] ?? null;
  for (let i = 0; ; i++) {
    const st = next(state, i);
    if (!st) break;
    if (st.set !== undefined) clock.set(st.set);
    if (st.advance !== undefined) clock.advance(st.advance);
    const r = dispatch(state, st.cmd, { clock });
    for (const e of r.ok ? r.events : []) if (e.type === 'achievementUnlocked') rewards += e.rewardGoldMicro;
    if (r.ok) {
      const v = validateGameState(r.state);
      if (!v.ok) throw new Error(`${name}: invalid ${v.errors.join(';')}`);
    }
    out.push({
      nowMs: clock.nowMs(),
      command: st.cmd,
      ok: r.ok,
      duplicate: r.ok ? r.duplicate : false,
      errorCode: r.ok ? null : r.error.code,
      sameStateObject: r.state === state,
      events: r.ok ? JSON.parse(JSON.stringify(r.events.filter((e) => U21_EVENTS.has(e.type)))) : [],
      state: r.state === state ? null : subset(r.state, rewards, false),
    });
    state = r.state;
  }
  return { name, start: subset(start, 0), steps: out, final: subset(state, rewards) };
}

function adult(uid: string, speciesId: string, extra: Partial<DragonInstance>): DragonInstance {
  return { uid, speciesId, genotype: { pigment: ['R', 'r'], pattern: ['P', 'p'], eye: ['B', 'b'] }, mutation: 'normal', xpSec: getSpecies(speciesId).growthTimeSec,
    locked: false, location: { kind: 'storage' }, hatchedAtMs: 0, personality: 'calm', bond: 0, parentIds: null, generation: 1, ancestry: 'unknown',
    origin: 'pack', activity: 'idle', breedCooldownUntilMs: 0, ...extra };
}

it('writes golden vectors', () => {
  const rngSeeds = [0, 1, 7, 42, 101, 0x7fffffff, 0x80000000, 0xffffffff, hashSeed('event-1', 'dragon-1')];
  const rng = rngSeeds.map((seed) => { const r = createRng(seed); return { seed, values: Array.from({ length: 64 }, () => r.next()) }; });
  const hashes = [[], [''], ['a'], ['ab', 'c'], ['a', 'bc'], ['founder', 'd_0000', 'g0-1.0'], ['Rồng', '\u{1F409}'], ['event-1', 'dragon-1']].map((parts) => ({ parts, hash: hashSeed(...parts) }));
  const helpers = (() => {
    const r = createRng(2024);
    const ints = Array.from({ length: 50 }, (_, i) => randomInt(r, (i % 7) + 1));
    const uniform = Array.from({ length: 50 }, () => pickUniform(r, ['a', 'b', 'c', 'd']));
    const weighted = Array.from({ length: 200 }, () => pickWeighted(r, [['n', 0.85], ['c', 0.11], ['g', 0.035], ['p', 0.005], ['z', 0]]));
    return { seed: 2024, ints, uniform, weighted };
  })();
  const founders = ['d_0000000000000000', 'd_1a2b3c4d5e6f7081', 'x', 'Rồng-\u{1F409}'].map((uid) => ({ uid, genotype: founderGenotype(uid, 'g0-1.0', ['pigment', 'pattern', 'eye']) }));
  const initialStates = [1, 7, 42, 101, 102, 65535].map((seed) => ({ seed, saveId: `save-${seed}`, nowMs: T0, state: createInitialState({ saveId: `save-${seed}`, nowMs: T0, rng: createRng(seed) }) }));
  const stages = ['baby', 'juvenile', 'adult'] as const;
  const mutations = ['normal', 'crystal', 'golden', 'prismatic'] as const;
  const economy = {
    rates: DRAGON_SPECIES.flatMap((s) => stages.flatMap((st) => mutations.map((m) => ({ speciesId: s.id, stage: st, mutation: m, rate: rateMicroGPerSec(s, st, m), sell: sellPriceG(s, st, m) })))),
    upgradeCosts: UPGRADES.flatMap((u) => Array.from({ length: u.max + 2 }, (_, lvl) => ({ id: u.id, level: lvl, cost: upgradeCostG(u, lvl) }))),
    budgets: Array.from({ length: 13 }, (_, l) => ({ level: l, budget: productionBudget(l) })),
    offline: [[125_000, 8 * 3600_000, 0], [1, 1, 0], [1, 1_333, 750], [2_750_000, 86_400_000, 0], [7, 999, 123]].map(([rate, ms, rem]) => ({ rate, ms, rem, ...offlineGoldMicro(rate!, ms!, rem!) })),
  };
  const suitabilityTable = DRAGON_SPECIES.flatMap((s) => Object.keys(HABITAT_PROFILES).map((h) => ({ speciesId: s.id, habitatId: h, ...speciesSuitability(s.id, h as keyof typeof HABITAT_PROFILES) })));
  const nicknames = [...NAMES.filter((n): n is string => n !== null), 'क\u094Dष'.repeat(24), 'क\u094Dष'.repeat(13), 'ab\r\ncd'].map((raw) => ({ raw, graphemes: graphemeCount(raw.normalize('NFC').trim()), result: normalizeNickname(raw) }));

  const base = (seed = 101) => createInitialState({ saveId: 'save-1', nowMs: T0, rng: createRng(seed) });
  let n = 0;
  const id = () => `req-${++n}`;
  const feed = (habitatId = 'origin_meadow', dietId = 'fruit', zoneId = 'origin') => ({ type: 'feed', requestId: id(), dietId, zoneId, habitatId });
  const scenarios = [];

  { const s = base(); const uid = s.dragons[0]!.uid;
    scenarios.push(runScenario('s01-growth-offline', s, [
      { advance: 300_000, cmd: { type: 'advanceTime', requestId: id(), mode: 'online' } },
      { advance: 8 * 3600_000, cmd: { type: 'advanceTime', requestId: id(), mode: 'offline' } },
      { advance: 60_000, cmd: { type: 'advanceTime', requestId: id(), mode: 'offline' } },
      { advance: 3 * 86_400_000, cmd: { type: 'advanceTime', requestId: id(), mode: 'offline' } },
      { advance: 0, cmd: { type: 'pet', requestId: id(), dragonUid: uid } },
    ])); }
  { const s = base();
    scenarios.push(runScenario('stage-boundaries', s, [
      { advance: 104_999, cmd: { type: 'advanceTime', requestId: id(), mode: 'online' } },
      { advance: 1, cmd: { type: 'advanceTime', requestId: id(), mode: 'online' } },
      { advance: 195_000, cmd: { type: 'advanceTime', requestId: id(), mode: 'online' } },
      { advance: 61_000, cmd: { type: 'advanceTime', requestId: id(), mode: 'offline' } },
    ])); }
  { const s = base(); const uid = s.dragons[0]!.uid;
    scenarios.push(runScenario('feed-consume', s, [
      { cmd: feed() },
      { cmd: { type: 'consumeFood', requestId: id(), foodId: `food-req-${n}`, dragonUid: uid } },
      { advance: 800, cmd: feed() },
      { advance: 1_000, cmd: { type: 'consumeFood', requestId: id(), foodId: `food-req-${n - 1}`, dragonUid: uid } },
      { advance: 6_200, cmd: { type: 'consumeFood', requestId: id(), foodId: `food-req-${n - 2}`, dragonUid: uid } },
      { advance: 100_000, cmd: { type: 'advanceTime', requestId: id(), mode: 'online' } },
      { advance: 0, cmd: feed() },
      { advance: 0, cmd: { type: 'consumeFood', requestId: id(), foodId: `food-req-${n}`, dragonUid: uid } },
    ])); }
  { const s = base(); const uid = s.dragons[0]!.uid;
    scenarios.push(runScenario('feed-limits-errors', s, [
      { cmd: feed() },
      { advance: 799, cmd: feed() },
      { advance: 1, cmd: feed() },
      { advance: 800, cmd: feed() }, { advance: 800, cmd: feed() }, { advance: 800, cmd: feed() },
      { advance: 800, cmd: feed() },
      { cmd: feed('ocean_reef', 'fruit', 'ocean') },
      { cmd: feed('origin_pondbank') },
      { cmd: feed('ocean_reef') },
      { cmd: feed('moon') },
      { cmd: feed('origin_meadow', 'fruit', 'atlantis') },
      { cmd: { type: 'consumeFood', requestId: id(), foodId: 'nope', dragonUid: uid } },
      { cmd: { type: 'consumeFood', requestId: id(), foodId: 'food-req-1', dragonUid: 'ghost' } },
      { advance: 12_000, cmd: { type: 'consumeFood', requestId: id(), foodId: 'food-req-1', dragonUid: uid } },
      { cmd: { type: 'advanceTime', requestId: id(), mode: 'online' } },
      { advance: 800, cmd: feed('origin_meadow', 'aquatic') },
      { cmd: { type: 'consumeFood', requestId: id(), foodId: `food-req-${n}`, dragonUid: uid } },
    ])); }
  { const s = base(); const uid = s.dragons[0]!.uid;
    const steps: { advance?: number; set?: number; cmd: Record<string, unknown> }[] = [];
    for (let i = 0; i < 12; i++) steps.push({ advance: i === 0 ? 0 : 30_000, cmd: { type: 'pet', requestId: id(), dragonUid: uid } });
    steps.push({ advance: 29_999, cmd: { type: 'pet', requestId: id(), dragonUid: uid } });
    steps.push({ set: Date.UTC(2026, 10, 25, 0, 0, 1), cmd: { type: 'pet', requestId: id(), dragonUid: uid } });
    steps.push({ set: T0, cmd: { type: 'pet', requestId: id(), dragonUid: uid } });
    steps.push({ advance: 0, cmd: { type: 'pet', requestId: id(), dragonUid: 'ghost' } });
    scenarios.push(runScenario('pet-bond', s, steps)); }
  { const s = base(); const uid = s.dragons[0]!.uid;
    scenarios.push(runScenario('rename', s, NAMES.map((nickname) => ({ cmd: { type: 'renameDragon', requestId: id(), dragonUid: uid, nickname } })))); }
  { const s = base();
    scenarios.push(runScenario('clock-rollback-duplicate-invalid', s, [
      { advance: 10_000, cmd: { type: 'advanceTime', requestId: 'same', mode: 'online' } },
      { advance: 60_000, cmd: { type: 'advanceTime', requestId: 'same', mode: 'online' } },
      { set: T0 - 3_600_000, cmd: { type: 'advanceTime', requestId: id(), mode: 'online' } },
      { set: T0 + 12_000, cmd: { type: 'advanceTime', requestId: id(), mode: 'online' } },
      { cmd: { type: 'advanceTime', requestId: id(), mode: 'warp' } },
      { cmd: { type: 'pet', requestId: '', dragonUid: 'x' } },
      { cmd: { type: 'pet', requestId: 'r'.repeat(65), dragonUid: 'x' } },
      { cmd: { type: 'feed', requestId: id(), dietId: 'meat', zoneId: 'origin', habitatId: 'origin_meadow' } },
      { cmd: { type: 'renameDragon', requestId: id(), dragonUid: 'x', nickname: 'a'.repeat(1025) } },
      { cmd: { type: 'nope', requestId: id() } },
    ])); }
  // Nhiều rồng, sản xuất/không sản xuất, biến thể, kho, released, bận: kiểm tích phân vàng và thứ tự event.
  { const s0 = base(7);
    const ember = s0.dragons[0]!;
    const s: GameState = { ...s0,
      discoveries: ['DISC_AQUATIC_HABITAT', 'DISC_EMBERLING_THERMAL'],
      zones: s0.zones.map((z) => (z.id === 'origin' ? { ...z, unlockedHabitatIds: ['origin_meadow', 'origin_warm_rocks', 'origin_pondbank'] } : z)),
      dragons: [ember,
        adult('sp1', 'sprout', { xpSec: 100.5, mutation: 'crystal', location: { kind: 'zone', zoneId: 'origin', habitatId: 'origin_meadow' } }),
        adult('dr1', 'droplet', { xpSec: 0, mutation: 'golden', location: { kind: 'zone', zoneId: 'origin', habitatId: 'origin_pondbank' }, bond: 99 }),
        adult('ze1', 'zephyr', { xpSec: 167.999, mutation: 'prismatic', location: { kind: 'zone', zoneId: 'origin', habitatId: 'origin_meadow' } }),
        adult('ga1', 'gaia', { xpSec: 1, location: { kind: 'storage' } }),
        adult('ma1', 'magma', { xpSec: 0, location: { kind: 'zone', zoneId: 'origin', habitatId: 'origin_warm_rocks' }, activity: 'care' }),
        adult('ce1', 'celestia', { xpSec: 0, location: { kind: 'released' } }),
      ],
      productionDragonIds: [ember.uid, 'sp1', 'dr1', 'ze1', 'ma1'],
      upgrades: { ...s0.upgrades, foodQualityLevel: 3, foodCooldownLevel: 2, islandCapacityLevel: 1 },
      goldRemainder: 3999,
    };
    if (!validateGameState(s).ok) throw new Error('multi start invalid: ' + JSON.stringify(validateGameState(s)));
    const ids = s.dragons.map((d) => d.uid);
    const habitats = ['origin_meadow', 'origin_pondbank', 'origin_warm_rocks', 'ocean_reef', 'moon'];
    const diets = ['fruit', 'leaf', 'aquatic', 'aether'];
    const r = createRng(hashSeed('u2.1-fuzz'));
    let last = 'req-x';
    const gen = (st: GameState, i: number): Step | null => {
      if (i >= 300) return null;
      const kind = randomInt(r, 10);
      const adv = pickWeighted(r, [[0, 3], [1, 1], [799, 1], [800, 3], [8_000, 3], [30_000, 2], [61_000, 1], [3_600_000, 1], [-5_000, 1], [100_000_000, 0.3]] as const);
      const reqId = randomInt(r, 25) === 0 ? last : id();
      last = reqId;
      const drops = st.food.drops;
      let cmd: Record<string, unknown>;
      if (kind <= 1) cmd = { type: 'advanceTime', requestId: reqId, mode: randomInt(r, 3) === 0 ? 'offline' : 'online' };
      else if (kind <= 3) cmd = { type: 'feed', requestId: reqId, dietId: pickUniform(r, diets), zoneId: randomInt(r, 8) === 0 ? 'ocean' : 'origin', habitatId: pickUniform(r, habitats) };
      else if (kind <= 6) {
        const drop = drops.length > 0 && randomInt(r, 5) !== 0 ? pickUniform(r, drops) : null;
        const here = drop ? st.dragons.filter((d) => d.location.kind === 'zone' && d.location.habitatId === drop.habitatId).map((d) => d.uid) : [];
        const uid = randomInt(r, 10) === 0 ? 'ghost' : here.length > 0 && randomInt(r, 4) !== 0 ? pickUniform(r, here) : pickUniform(r, ids);
        cmd = { type: 'consumeFood', requestId: reqId, foodId: drop ? drop.id : 'food-none', dragonUid: uid };
      }
      else if (kind <= 8) cmd = { type: 'pet', requestId: reqId, dragonUid: pickUniform(r, ids) };
      else cmd = { type: 'renameDragon', requestId: reqId, dragonUid: pickUniform(r, ids), nickname: pickUniform(r, NAMES) };
      return { advance: adv, cmd };
    };
    scenarios.push(runScenario('multi-dragon-fuzz', s, gen)); }

  const golden = { source: 'nldanh13/dravio@b33ec7a3dc98b4a74857fe92d143475028fc0d96', T0, rng, hashes, helpers, founders, initialStates, economy, suitability: suitabilityTable, nicknames, scenarios };
  writeFileSync(process.env.GOLDEN_OUT ?? 'golden.json', JSON.stringify(golden) + '\n');
});
