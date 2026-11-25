/**
 * Golden vectors U2.3 (world graph & habitat commands) từ TypeScript gốc nldanh13/dravio @ b33ec7a3.
 *   cp world-golden.test.ts <dravio>/tests/unit/zz-world-golden.test.ts
 *   GOLDEN_OUT=<abs>/ts-world-golden-b33ec7a.json npx vitest run tests/unit/zz-world-golden.test.ts
 */
import { writeFileSync } from 'node:fs';
import { it } from 'vitest';
import { DRAGON_SPECIES, getSpecies } from '../../src/data/dragons';
import { HABITAT_PROFILES } from '../../src/data/g0Biology';
import { WORLD_EDGES, WORLD_ZONE_IDS, canTravel, edgesOf } from '../../src/data/worldGraph';
import { fixedClock } from '../../src/engine/core/clock';
import { dispatch } from '../../src/engine/core/commands';
import { createInitialState } from '../../src/engine/core/initialState';
import { createRng, hashSeed, pickUniform, pickWeighted, randomInt } from '../../src/engine/core/rng';
import { validateGameState } from '../../src/engine/core/validation';
import { surveyRequirementMet } from '../../src/engine/world/survey';
import { checkPlacement, currentProductionBudget, habitatsOfZone, habitatUnlockStatus, placementOptions, zoneOccupancy } from '../../src/engine/world/zones';
import type { DragonInstance } from '../../src/types/game';
import type { GameState } from '../../src/types/state';

const T0 = Date.UTC(2026, 10, 25, 9, 0, 0);
const EVENTS = new Set(['timeAdvanced', 'stageChanged', 'offlineReceiptCreated', 'foodSpawned', 'foodExpired', 'foodConsumed', 'bondChanged', 'dragonPetted', 'dragonRenamed',
  'dragonMoved', 'productionChanged', 'habitatUnlocked', 'discoveryRecorded', 'habitatSurveyed', 'biologyProfileViewed']);
const LOCOS = ['walk', 'swim', 'fly', 'burrow'] as const;

/** Phần state U2.3 sở hữu: bỏ tiến độ hướng dẫn và thành tựu (chưa port), giữ profilesViewed; vàng trừ thưởng thành tựu. */
function subset(s: GameState, rewards: number, fullIds = true) {
  const { tutorial, unlockedAchievements: _u, claimedAchievements: _c, processedRequestIds: ids, ...rest } = s;
  const processed = fullIds ? { processedRequestIds: ids } : { processedRequestCount: ids.length, processedRequestTail: ids.slice(-3) };
  return JSON.parse(JSON.stringify({ ...rest, ...processed, goldMicro: s.goldMicro - rewards, tutorial: { profilesViewed: tutorial.profilesViewed, starterPackUsed: tutorial.starterPackUsed } }));
}

function placementJson(s: GameState, d: DragonInstance) {
  return placementOptions(s, d).map((o) => ({ zoneId: o.zoneId, habitatId: o.habitatId, allowed: o.result.allowed, score: o.result.score, reason: o.result.reason, tier: o.tier, freeSlots: o.freeSlots, blocked: o.blocked }));
}

function adult(uid: string, speciesId: string, extra: Partial<DragonInstance> = {}): DragonInstance {
  return { uid, speciesId, genotype: { pigment: ['R', 'r'], pattern: ['P', 'p'], eye: ['B', 'b'] }, mutation: 'normal', xpSec: getSpecies(speciesId).growthTimeSec,
    locked: false, location: { kind: 'storage' }, hatchedAtMs: 0, personality: 'calm', bond: 0, parentIds: null, generation: 1, ancestry: 'unknown',
    origin: 'pack', activity: 'idle', breedCooldownUntilMs: 0, ...extra };
}

type Step = { advance?: number; cmd: Record<string, unknown>; probe?: string };
function runScenario(name: string, start: GameState, steps: Step[] | ((s: GameState, i: number) => Step | null)) {
  const v0 = validateGameState(start);
  if (!v0.ok) throw new Error(`${name} start invalid: ${v0.errors.join(';')}`);
  const clock = fixedClock(T0);
  let state = start;
  let rewards = 0;
  const out = [];
  const next = typeof steps === 'function' ? steps : (_: GameState, i: number) => steps[i] ?? null;
  for (let i = 0; ; i++) {
    const st = next(state, i);
    if (!st) break;
    if (st.advance !== undefined) clock.advance(st.advance);
    const r = dispatch(state, st.cmd, { clock });
    for (const e of r.ok ? r.events : []) if (e.type === 'achievementUnlocked') rewards += e.rewardGoldMicro;
    if (r.ok) {
      const v = validateGameState(r.state);
      if (!v.ok) throw new Error(`${name} step ${i}: invalid ${v.errors.join(';')}`);
    }
    const probeDragon = st.probe ? r.state.dragons.find((d) => d.uid === st.probe) : undefined;
    out.push({
      nowMs: clock.nowMs(), command: st.cmd, ok: r.ok, duplicate: r.ok ? r.duplicate : false, errorCode: r.ok ? null : r.error.code,
      sameStateObject: r.state === state,
      events: r.ok ? JSON.parse(JSON.stringify(r.events.filter((e) => EVENTS.has(e.type)))) : [],
      state: r.state === state ? null : subset(r.state, rewards, false),
      ...(probeDragon ? { probe: { dragonUid: probeDragon.uid, placement: placementJson(r.state, probeDragon), unlockStatus: habitatUnlockStatus(r.state), budget: currentProductionBudget(r.state),
        occupancy: Object.fromEntries(WORLD_ZONE_IDS.map((z) => [z, zoneOccupancy(r.state, z)])), survey: { thermal: surveyRequirementMet(r.state, 'DISC_EMBERLING_THERMAL'), aquatic: surveyRequirementMet(r.state, 'DISC_AQUATIC_HABITAT') } } } : {}),
    });
    state = r.state;
  }
  return { name, start: subset(start, 0), steps: out, final: subset(state, rewards) };
}

it('writes world golden vectors', () => {
  const subsets = Array.from({ length: 16 }, (_, m) => LOCOS.filter((_, i) => m & (1 << i)));
  const travel = WORLD_ZONE_IDS.flatMap((a) => WORLD_ZONE_IDS.flatMap((b) => subsets.map((loco) => ({ from: a, to: b, locomotion: loco, ok: canTravel(a, b, loco) }))));
  const reach = DRAGON_SPECIES.flatMap((s) => s.habitatRules.map((h) => ({ speciesId: s.id, habitatId: h, zoneId: HABITAT_PROFILES[h as keyof typeof HABITAT_PROFILES].zoneId,
    ok: canTravel('origin', HABITAT_PROFILES[h as keyof typeof HABITAT_PROFILES].zoneId, s.anatomy.locomotion) })));
  const graph = {
    zones: WORLD_ZONE_IDS, edges: WORLD_EDGES, edgesOf: Object.fromEntries(WORLD_ZONE_IDS.map((z) => [z, edgesOf(z).map((e) => e.id)])),
    habitatsOfZone: Object.fromEntries(WORLD_ZONE_IDS.map((z) => [z, habitatsOfZone(z)])), travel, reach,
  };

  let n = 0;
  const id = () => `w-${++n}`;
  const base = (seed = 8) => createInitialState({ saveId: 'zones', nowMs: T0, rng: createRng(seed) });
  const meadow = { kind: 'zone', zoneId: 'origin', habitatId: 'origin_meadow' };
  const scenarios = [];

  { const s = base(); const ember = s.dragons[0]!.uid;
    const m = (dragonUid: string, target: unknown) => ({ type: 'moveDragon', requestId: id(), dragonUid, target });
    scenarios.push(runScenario('unlock-survey-move', s, [
      { cmd: { type: 'unlockHabitat', requestId: id(), habitatId: 'nowhere' } },
      { cmd: { type: 'unlockHabitat', requestId: id(), habitatId: 'ocean_estuary' } },
      { cmd: { type: 'unlockHabitat', requestId: id(), habitatId: 'origin_meadow' } },
      { cmd: { type: 'unlockHabitat', requestId: id(), habitatId: 'origin_warm_rocks' } },
      { cmd: { type: 'unlockHabitat', requestId: id(), habitatId: 'origin_pondbank' } },
      { cmd: { type: 'surveyHabitat', requestId: id(), habitatId: 'origin_warm_rocks' }, probe: ember },
      { cmd: { type: 'viewBiologyProfile', requestId: id(), speciesId: 'droplet' } },
      { cmd: { type: 'viewBiologyProfile', requestId: id(), speciesId: 'nope' } },
      { cmd: { type: 'viewBiologyProfile', requestId: id(), speciesId: 'emberling' }, probe: ember },
      { cmd: { type: 'viewBiologyProfile', requestId: id(), speciesId: 'emberling' } },
      { advance: 30_000, cmd: { type: 'surveyHabitat', requestId: id(), habitatId: 'origin_warm_rocks' }, probe: ember },
      { cmd: { type: 'surveyHabitat', requestId: id(), habitatId: 'origin_warm_rocks' } },
      { cmd: { type: 'surveyHabitat', requestId: id(), habitatId: 'origin_meadow' } },
      { cmd: { type: 'surveyHabitat', requestId: id(), habitatId: 'ocean_reef' } },
      { cmd: { type: 'surveyHabitat', requestId: id(), habitatId: 'nope' } },
      { cmd: { type: 'unlockHabitat', requestId: id(), habitatId: 'origin_pondbank' } },
      { advance: 30_000, cmd: { type: 'unlockHabitat', requestId: id(), habitatId: 'origin_warm_rocks' }, probe: ember },
      { cmd: { type: 'unlockHabitat', requestId: id(), habitatId: 'origin_warm_rocks' } },
      { cmd: { type: 'surveyHabitat', requestId: id(), habitatId: 'origin_pondbank' } },
      { cmd: { type: 'unlockHabitat', requestId: id(), habitatId: 'origin_pondbank' }, probe: ember },
      { advance: 100_000, cmd: m(ember, { kind: 'zone', zoneId: 'origin', habitatId: 'origin_warm_rocks' }), probe: ember },
      { cmd: m(ember, { kind: 'zone', zoneId: 'origin', habitatId: 'origin_warm_rocks' }) },
      { cmd: m(ember, { kind: 'zone', zoneId: 'origin', habitatId: 'origin_pondbank' }) },
      { cmd: m(ember, { kind: 'zone', zoneId: 'ocean', habitatId: 'ocean_estuary' }) },
      { cmd: m(ember, { kind: 'zone', zoneId: 'origin', habitatId: 'ocean_estuary' }) },
      { cmd: m(ember, { kind: 'zone', zoneId: 'origin', habitatId: 'moon' }) },
      { cmd: m('ghost', meadow) },
      { advance: 50_000, cmd: m(ember, { kind: 'storage' }), probe: ember },
      { cmd: m(ember, { kind: 'storage' }) },
      { cmd: { type: 'setProductionSlot', requestId: id(), dragonUid: ember, enabled: true } },
      { cmd: { type: 'setProductionSlot', requestId: id(), dragonUid: ember, enabled: false } },
      { advance: 100_000, cmd: { type: 'advanceTime', requestId: id(), mode: 'online' } },
      { cmd: m(ember, meadow) },
      { advance: 10_000, cmd: { type: 'setProductionSlot', requestId: id(), dragonUid: ember, enabled: true }, probe: ember },
      { cmd: { type: 'setProductionSlot', requestId: id(), dragonUid: ember, enabled: true } },
      { advance: 50_000, cmd: { type: 'setProductionSlot', requestId: id(), dragonUid: ember, enabled: false } },
      { cmd: { type: 'setProductionSlot', requestId: id(), dragonUid: ember, enabled: false } },
      { cmd: { type: 'setProductionSlot', requestId: id(), dragonUid: 'ghost', enabled: true } },
      { cmd: { type: 'moveDragon', requestId: 'same-move', dragonUid: ember, target: { kind: 'storage' } } },
      { cmd: { type: 'moveDragon', requestId: 'same-move', dragonUid: ember, target: { kind: 'storage' } } },
      { cmd: m(ember, null) }, { cmd: m(ember, {}) }, { cmd: m(ember, { kind: 'zone' }) }, { cmd: m(ember, { kind: 'zone', zoneId: 'origin' }) }, { cmd: m(ember, { kind: 'shelter' }) },
      { cmd: { type: 'setProductionSlot', requestId: id(), dragonUid: ember, enabled: 'yes' } },
      { cmd: { type: 'unlockHabitat', requestId: id(), habitatId: '' } },
      { cmd: { type: 'surveyHabitat', requestId: id() } },
      { cmd: { type: 'viewBiologyProfile', requestId: id(), speciesId: 'x'.repeat(65) } },
      { cmd: { type: 'recordDiscovery', requestId: id(), discoveryId: 'DISC_AQUATIC_HABITAT' } },
      { cmd: { type: 'grantHabitatUnlock', requestId: id(), habitatId: 'origin_pondbank' } },
      { cmd: { type: 'completeTutorial', requestId: id() } },
    ])); }

  { const s0 = base();
    const s: GameState = { ...s0,
      discoveries: ['DISC_AQUATIC_HABITAT', 'DISC_EMBERLING_THERMAL'],
      zones: s0.zones.map((z) => (z.id === 'origin' ? { ...z, unlockedHabitatIds: ['origin_meadow', 'origin_pondbank', 'origin_warm_rocks'] } : z.id === 'ocean' ? { ...z, unlocked: true, unlockedHabitatIds: ['ocean_estuary'] } : z)),
      dragons: [...s0.dragons,
        ...['m1', 'm2', 'm3', 'm4'].map((uid) => adult(uid, 'emberling', { location: meadow as DragonInstance['location'] })),
        ...['e1', 'e2'].map((uid) => adult(uid, 'droplet', { location: { kind: 'zone', zoneId: 'ocean', habitatId: 'ocean_estuary' } })),
        adult('drop', 'droplet'), adult('six', 'sprout'),
        adult('busy', 'sprout', { activity: 'breeding' }), adult('care', 'zephyr', { location: { kind: 'care', caseId: 'case-1' }, activity: 'care' }),
        adult('shelter', 'sprout', { location: { kind: 'shelter', zoneId: 'origin' } }), adult('gone', 'celestia', { location: { kind: 'released' } }),
        adult('young', 'zephyr', { xpSec: 0 }),
      ],
    };
    const p = (dragonUid: string, enabled: boolean) => ({ type: 'setProductionSlot', requestId: id(), dragonUid, enabled });
    const m = (dragonUid: string, target: unknown) => ({ type: 'moveDragon', requestId: id(), dragonUid, target });
    scenarios.push(runScenario('budget-capacity-busy', s, [
      { cmd: m('six', meadow), probe: 'six' },
      { cmd: m('m1', { kind: 'zone', zoneId: 'origin', habitatId: 'origin_warm_rocks' }), probe: 'm1' },
      { cmd: m('m1', { kind: 'storage' }) },
      { cmd: m('six', meadow), probe: 'six' },
      { cmd: m('m1', meadow) },
      { cmd: p('m2', true) }, { cmd: p('m3', true) }, { cmd: p('m4', true) }, { cmd: p('six', true) },
      { cmd: p('e1', true), probe: 'e1' },
      { cmd: m('drop', meadow), probe: 'drop' },
      { cmd: m('drop', { kind: 'zone', zoneId: 'origin', habitatId: 'origin_pondbank' }) },
      { cmd: m('drop', { kind: 'zone', zoneId: 'ocean', habitatId: 'ocean_estuary' }), probe: 'drop' },
      ...['busy', 'care', 'shelter', 'gone'].flatMap((uid) => [{ cmd: m(uid, meadow), probe: uid }, { cmd: m(uid, { kind: 'storage' }) }, { cmd: p(uid, true) }]),
      { cmd: p('young', true) },
      { advance: 10_000, cmd: { type: 'advanceTime', requestId: id(), mode: 'online' } },
      { cmd: p('m2', false) }, { cmd: p('e1', true) }, { cmd: p('e2', true) },
      { cmd: m('young', { kind: 'zone', zoneId: 'origin', habitatId: 'origin_meadow' }) },
      { advance: 60_000, cmd: m('e1', { kind: 'storage' }), probe: 'e1' },
      { cmd: { type: 'viewBiologyProfile', requestId: id(), speciesId: 'celestia' } },
      { cmd: { type: 'viewBiologyProfile', requestId: id(), speciesId: 'zephyr' } },
      { cmd: { type: 'viewBiologyProfile', requestId: id(), speciesId: 'droplet' } },
      { cmd: { type: 'viewBiologyProfile', requestId: id(), speciesId: 'emberling' } },
      { cmd: { type: 'surveyHabitat', requestId: id(), habitatId: 'origin_pondbank' } },
      { cmd: { type: 'unlockHabitat', requestId: id(), habitatId: 'origin_pondbank' } },
    ])); }

  // Fuzz: mọi lệnh của U2.1 + U2.3 trên một state nhiều rồng, có nâng cấp ngân sách.
  { const s0 = base(21);
    const s: GameState = { ...s0,
      upgrades: { ...s0.upgrades, islandCapacityLevel: 1, foodQualityLevel: 1 },
      dragons: [...s0.dragons, adult('sp', 'sprout', { xpSec: 50, location: meadow as DragonInstance['location'] }), adult('dr', 'droplet', { xpSec: 10, mutation: 'crystal' }),
        adult('ze', 'zephyr', { xpSec: 0 }), adult('ma', 'magma', { xpSec: 100 }), adult('mo', 'mossback', { xpSec: 0 }), adult('ga', 'gaia', { xpSec: 0, activity: 'expedition' }),
        adult('am', 'amberhorn', { xpSec: 0, mutation: 'golden' }), adult('ce', 'celestia', { location: { kind: 'released' } })],
      productionDragonIds: [s0.dragons[0]!.uid, 'sp'],
    };
    const ids = s.dragons.map((d) => d.uid).concat(['ghost']);
    const habitats = [...Object.keys(HABITAT_PROFILES), 'moon'];
    const species = [...DRAGON_SPECIES.map((x) => x.id), 'nope'];
    const r = createRng(hashSeed('u2.3-world-fuzz'));
    const gen = (st: GameState, i: number): Step | null => {
      if (i >= 400) return null;
      const adv = pickWeighted(r, [[0, 4], [800, 2], [8_000, 2], [30_000, 2], [61_000, 1], [600_000, 1]] as const);
      const k = randomInt(r, 20);
      const uid = pickUniform(r, ids);
      let cmd: Record<string, unknown>;
      if (k < 6) {
        const h = pickUniform(r, habitats);
        const zone = randomInt(r, 6) === 0 ? pickUniform(r, ['origin', 'ocean', 'sky']) : (HABITAT_PROFILES as Record<string, { zoneId: string }>)[h]?.zoneId ?? 'origin';
        cmd = { type: 'moveDragon', requestId: id(), dragonUid: uid, target: randomInt(r, 4) === 0 ? { kind: 'storage' } : { kind: 'zone', zoneId: zone, habitatId: h } };
      } else if (k < 10) cmd = { type: 'setProductionSlot', requestId: id(), dragonUid: uid, enabled: randomInt(r, 2) === 0 };
      else if (k < 12) cmd = { type: 'unlockHabitat', requestId: id(), habitatId: pickUniform(r, habitats) };
      else if (k < 14) cmd = { type: 'surveyHabitat', requestId: id(), habitatId: pickUniform(r, habitats) };
      else if (k < 16) cmd = { type: 'viewBiologyProfile', requestId: id(), speciesId: pickUniform(r, species) };
      else if (k < 17) cmd = { type: 'advanceTime', requestId: id(), mode: randomInt(r, 3) === 0 ? 'offline' : 'online' };
      else if (k < 18) cmd = { type: 'feed', requestId: id(), dietId: pickUniform(r, ['fruit', 'leaf', 'aquatic', 'aether']), zoneId: 'origin', habitatId: pickUniform(r, ['origin_meadow', 'origin_pondbank', 'origin_warm_rocks']) };
      else if (k < 19) { const drop = st.food.drops.length > 0 ? pickUniform(r, st.food.drops) : null; cmd = { type: 'consumeFood', requestId: id(), foodId: drop ? drop.id : 'none', dragonUid: uid }; }
      else cmd = { type: 'pet', requestId: id(), dragonUid: uid };
      return { advance: adv, cmd, probe: i % 10 === 0 && uid !== 'ghost' ? uid : undefined };
    };
    scenarios.push(runScenario('world-fuzz', s, gen)); }

  // checkPlacement khi microhabitat không thuộc zone (TS trả { blocked } không chi tiết).
  const ember = base().dragons[0]!;
  const mismatch = ['ocean_reef', 'moon'].map((h) => ({ zoneId: 'origin', habitatId: h, check: checkPlacement(base(), ember, 'origin', h) }));
  writeFileSync(process.env.GOLDEN_OUT ?? 'world-golden.json', JSON.stringify({ source: 'nldanh13/dravio@b33ec7a3dc98b4a74857fe92d143475028fc0d96', T0, graph, mismatch, scenarios }));
});
