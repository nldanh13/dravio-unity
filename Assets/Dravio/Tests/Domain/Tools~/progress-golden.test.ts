/**
 * Golden vectors U2.4 (tutorial, Starter pack, achievements) từ TypeScript gốc nldanh13/dravio @ b33ec7a3.
 * So TOÀN BỘ state (gồm tutorial, achievements, vàng thật) và MỌI event.
 *   cp progress-golden.test.ts <dravio>/tests/unit/zz-progress-golden.test.ts
 *   GOLDEN_OUT=<abs>/ts-progress-golden-b33ec7a.json npx vitest run tests/unit/zz-progress-golden.test.ts
 */
import { readFileSync, writeFileSync } from 'node:fs';
import { it } from 'vitest';
import { DRAGON_SPECIES, getSpecies } from '../../src/data/dragons';
import { HABITAT_PROFILES } from '../../src/data/g0Biology';
import { fixedClock } from '../../src/engine/core/clock';
import { dispatch } from '../../src/engine/core/commands';
import { createInitialState } from '../../src/engine/core/initialState';
import { createRng, hashSeed, pickUniform, pickWeighted, randomInt } from '../../src/engine/core/rng';
import { validateGameState } from '../../src/engine/core/validation';
import { hatchFromPack, packPlacementOptions } from '../../src/engine/inventory/packs';
import { implementedAchievements } from '../../src/engine/progression/achievements';
import { currentStep } from '../../src/engine/progression/tutorial';
import { parseSaveText } from '../../src/persistence/saveFile';
import type { DragonInstance } from '../../src/types/game';
import type { GameState } from '../../src/types/state';

const T0 = Date.UTC(2026, 10, 25, 9, 0, 0);
const full = (s: GameState, fullIds = true) => {
  const { processedRequestIds: ids, ...rest } = s;
  return JSON.parse(JSON.stringify(fullIds ? s : { ...rest, processedRequestCount: ids.length, processedRequestTail: ids.slice(-3) }));
};
function adult(uid: string, speciesId: string, extra: Partial<DragonInstance> = {}): DragonInstance {
  return { uid, speciesId, genotype: { pigment: ['R', 'r'], pattern: ['P', 'p'], eye: ['B', 'b'] }, mutation: 'normal', xpSec: getSpecies(speciesId).growthTimeSec,
    locked: false, location: { kind: 'storage' }, hatchedAtMs: 0, personality: 'calm', bond: 0, parentIds: null, generation: 1, ancestry: 'unknown',
    origin: 'pack', activity: 'idle', breedCooldownUntilMs: 0, ...extra };
}
function probe(s: GameState) {
  return {
    currentStep: currentStep(s.tutorial),
    packOptions: packPlacementOptions(s).map((o) => ({ zoneId: o.zoneId, habitatId: o.habitatId, score: o.result.score, freeSlots: o.freeSlots, blocked: o.blocked })),
    hatchPreview: s.pendingPack ? JSON.parse(JSON.stringify(hatchFromPack(s.pendingPack, 0))) : null,
  };
}
type Step = { advance?: number; set?: number; cmd: Record<string, unknown> };
function runScenario(name: string, start: GameState, steps: Step[] | ((s: GameState, i: number) => Step | null), t0 = T0) {
  const v0 = validateGameState(start);
  if (!v0.ok) throw new Error(`${name} start invalid: ${v0.errors.join(';')}`);
  const clock = fixedClock(t0);
  let state = start;
  const out = [];
  const next = typeof steps === 'function' ? steps : (_: GameState, i: number) => steps[i] ?? null;
  for (let i = 0; ; i++) {
    const st = next(state, i);
    if (!st) break;
    if (st.set !== undefined) clock.set(st.set);
    if (st.advance !== undefined) clock.advance(st.advance);
    const r = dispatch(state, st.cmd, { clock });
    if (r.ok) { const v = validateGameState(r.state); if (!v.ok) throw new Error(`${name} step ${i}: ${v.errors.join(';')}`); }
    out.push({ nowMs: clock.nowMs(), command: st.cmd, ok: r.ok, duplicate: r.ok ? r.duplicate : false, errorCode: r.ok ? null : r.error.code, sameStateObject: r.state === state,
      events: r.ok ? JSON.parse(JSON.stringify(r.events)) : [], state: r.state === state ? null : full(r.state, false), probe: probe(r.state) });
    state = r.state;
  }
  return { name, start: full(start), steps: out, final: full(state) };
}

it('writes progress golden vectors', async () => {
  let n = 0;
  const id = () => `p-${++n}`;
  const game = (saveId = 'tut', seed = 8) => createInitialState({ saveId, nowMs: T0, rng: createRng(seed) });
  const at = (s: GameState) => s.dragons.find((d) => d.speciesId === 'emberling')!.uid;
  const cmd = {
    adv: (mode = 'online') => ({ type: 'advanceTime', requestId: id(), mode }),
    feed: (dietId = 'fruit', habitatId = 'origin_meadow') => ({ type: 'feed', requestId: id(), dietId, zoneId: 'origin', habitatId }),
    eat: (dragonUid: string) => (s: GameState) => ({ type: 'consumeFood', requestId: id(), foodId: s.food.drops.at(-1)?.id ?? 'none', dragonUid }),
    view: (speciesId = 'emberling') => ({ type: 'viewBiologyProfile', requestId: id(), speciesId }),
    survey: (habitatId: string) => ({ type: 'surveyHabitat', requestId: id(), habitatId }),
    unlock: (habitatId: string) => ({ type: 'unlockHabitat', requestId: id(), habitatId }),
    buy: (requestId = id()) => ({ type: 'buyTutorialPack', requestId }),
    claim: (target: unknown) => ({ type: 'claimPack', requestId: id(), target }),
    status: (status: unknown) => ({ type: 'setTutorialStatus', requestId: id(), status }),
    move: (dragonUid: string, target: unknown) => ({ type: 'moveDragon', requestId: id(), dragonUid, target }),
  };
  const meadow = { kind: 'zone', zoneId: 'origin', habitatId: 'origin_meadow' };
  /** Bước có lệnh phụ thuộc state (consumeFood cần foodId vừa thả). */
  const dyn = (list: (Step | ((s: GameState) => Step))[]) => (s: GameState, i: number) => { const x = list[i]; return x === undefined ? null : typeof x === 'function' ? x(s) : x; };
  const scenarios = [];

  { const s = game(); const e = at(s);
    scenarios.push(runScenario('full-playthrough', s, dyn([
      { advance: 14_999, cmd: cmd.adv() }, { advance: 1, cmd: cmd.adv() },
      { advance: 2000, cmd: cmd.feed('leaf') }, (st) => ({ cmd: cmd.eat(e)(st) }),
      { advance: 2000, cmd: cmd.feed() }, (st) => ({ cmd: cmd.eat(e)(st) }),
      { cmd: cmd.view() }, { cmd: cmd.survey('origin_warm_rocks') }, { cmd: cmd.unlock('origin_warm_rocks') },
      { cmd: cmd.buy() }, { cmd: cmd.claim(meadow) },
      { cmd: cmd.survey('origin_pondbank') }, { cmd: cmd.unlock('origin_pondbank') },
      { advance: 300_000, cmd: cmd.adv() }, { advance: 1000, cmd: cmd.adv() },
      { cmd: cmd.status('skipped') }, { cmd: cmd.status('active') }, { advance: 16_000, cmd: cmd.adv() }, { advance: 600_000, cmd: cmd.adv('offline') },
    ]))); }

  { const s = game(); const e = at(s);
    scenarios.push(runScenario('order-cascade', s, dyn([
      { advance: 1000, cmd: cmd.view() }, { cmd: cmd.survey('origin_warm_rocks') }, { cmd: cmd.unlock('origin_warm_rocks') },
      { advance: 15_000, cmd: cmd.adv() }, { advance: 1000, cmd: cmd.feed() }, (st) => ({ cmd: cmd.eat(e)(st) }),
    ]))); }

  { const s = game(); const e = at(s);
    scenarios.push(runScenario('tut01-needs-zone', s, [
      { advance: 1000, cmd: cmd.move(e, { kind: 'storage' }) }, { advance: 20_000, cmd: cmd.adv() }, { cmd: cmd.move(e, meadow) },
    ])); }

  { const s = game(); const e = at(s);
    scenarios.push(runScenario('skip-and-resume', s, dyn([
      { cmd: cmd.status('skipped') }, { cmd: cmd.status('skipped') }, { advance: 20_000, cmd: cmd.adv() },
      { cmd: cmd.view() }, { cmd: cmd.survey('origin_warm_rocks') }, { cmd: cmd.unlock('origin_warm_rocks') },
      { cmd: cmd.survey('origin_pondbank') }, { cmd: cmd.unlock('origin_pondbank') }, { cmd: cmd.buy() }, { cmd: cmd.claim({ kind: 'storage' }) },
      { advance: 1000, cmd: cmd.status('active') }, { advance: 15_000, cmd: cmd.adv() }, { advance: 1000, cmd: cmd.feed() }, (st) => ({ cmd: cmd.eat(e)(st) }),
      { cmd: cmd.status('active') }, { cmd: cmd.status('completed') }, { cmd: cmd.status('done') }, { cmd: { type: 'setTutorialStatus', requestId: id() } },
      { advance: 300_000, cmd: cmd.adv() }, { cmd: cmd.status('skipped') }, { cmd: cmd.status('active') }, { advance: 20_000, cmd: cmd.adv() },
    ]))); }

  { const s = game();
    scenarios.push(runScenario('pack-rules', s, [
      { cmd: cmd.claim({ kind: 'storage' }) }, { cmd: cmd.buy('buy-1') }, { cmd: cmd.buy('buy-1') }, { cmd: cmd.buy() },
      { cmd: cmd.claim({ kind: 'zone', zoneId: 'origin', habitatId: 'origin_warm_rocks' }) }, { cmd: cmd.claim({ kind: 'zone', zoneId: 'origin', habitatId: 'origin_pondbank' }) },
      { cmd: cmd.claim({ kind: 'zone', zoneId: 'ocean', habitatId: 'origin_meadow' }) }, { cmd: cmd.claim({ kind: 'zone', zoneId: 'ocean', habitatId: 'ocean_reef' }) },
      { cmd: cmd.claim(null) }, { cmd: cmd.claim({ kind: 'zone' }) }, { cmd: cmd.claim({ kind: 'released' }) },
      { advance: 5_000, cmd: cmd.claim({ kind: 'storage' }) }, { cmd: cmd.buy() }, { cmd: cmd.claim({ kind: 'storage' }) },
      { advance: 480_000, cmd: cmd.adv() }, { advance: 1000, cmd: cmd.adv() },
    ])); }

  { const s0 = game('gold'); const s = { ...s0, goldMicro: 100_000_000 - 1 };
    scenarios.push(runScenario('pack-needs-100G', s, [{ cmd: cmd.buy() }, { advance: 40_000, cmd: cmd.adv() }, { cmd: cmd.buy() }])); }
  { const s0 = game('gold2'); const s = { ...s0, goldMicro: 100_000_000 };
    scenarios.push(runScenario('pack-exactly-100G', s, [{ cmd: cmd.buy() }])); }

  { const s0 = game('full', 3);
    const s = { ...s0, dragons: [...s0.dragons, ...['a', 'b', 'c', 'd'].map((u) => adult(u, 'emberling', { location: meadow as DragonInstance['location'] }))] };
    scenarios.push(runScenario('claim-zone-full', s, [
      { cmd: cmd.buy() }, { cmd: cmd.claim(meadow) }, { cmd: cmd.move('a', { kind: 'storage' }) }, { cmd: cmd.claim(meadow) }, { cmd: cmd.claim(meadow) },
    ])); }

  // Chống nhận thưởng nhiều lần: thành tựu đã mở (kể cả đã mở mà chưa nhận) không bao giờ thưởng lại.
  { const s0 = game('dup');
    const grown = { ...s0, dragons: s0.dragons.map((d) => ({ ...d, xpSec: 300 })), unlockedAchievements: ['ACH_FIRST_ADULT'] as GameState['unlockedAchievements'], claimedAchievements: ['ACH_FIRST_ADULT'] as GameState['claimedAchievements'] };
    scenarios.push(runScenario('no-double-reward', grown, [{ advance: 60_000, cmd: cmd.adv() }, { advance: 86_400_000, cmd: cmd.adv('offline') }, { cmd: cmd.buy() }, { cmd: cmd.claim({ kind: 'storage' }) }, { advance: 1000, cmd: cmd.adv() }]));
    const unclaimed = { ...grown, claimedAchievements: [] as GameState['claimedAchievements'] };
    scenarios.push(runScenario('unlocked-but-unclaimed-stays-unclaimed', unclaimed, [{ advance: 60_000, cmd: cmd.adv() }]));
    scenarios.push(runScenario('starting-emberling-is-not-a-hatch', game('fresh'), [{ advance: 400_000, cmd: cmd.adv() }, { advance: 1000, cmd: cmd.adv() }])); }

  // Save v2 thật (main@b5820f9) sau migration: lần dispatch đầu tiên ghi nhận những gì save đã đạt.
  { const text = readFileSync('tests/fixtures/save-schema-v2.json', 'utf8');
    const r = await parseSaveText(text);
    if (!r.ok) throw new Error(r.code);
    const s = r.save.state; const e = s.dragons.find((d) => d.speciesId === 'emberling')!.uid;
    scenarios.push(runScenario('fixture-v2-after-migration', s, [{ advance: 15_000, cmd: cmd.adv() }, { cmd: cmd.view() }, { cmd: cmd.buy() }, { cmd: cmd.claim({ kind: 'storage' }) }, { cmd: cmd.move(e, { kind: 'storage' }) }], s.clock.lastProcessedAtMs)); }

  { const s0 = game('fuzz', 21);
    const s: GameState = { ...s0, dragons: [...s0.dragons, adult('dr', 'droplet', { xpSec: 0 }), adult('ze', 'zephyr', { xpSec: 10 })], goldMicro: 260_000_000 };
    const habitats = [...Object.keys(HABITAT_PROFILES), 'moon'];
    const species = [...DRAGON_SPECIES.map((x) => x.id), 'nope'];
    const r = createRng(hashSeed('u2.4-progress-fuzz'));
    const gen = (st: GameState, i: number): Step | null => {
      if (i >= 500) return null;
      const adv = pickWeighted(r, [[0, 4], [1000, 2], [8_000, 2], [16_000, 2], [61_000, 1], [600_000, 1]] as const);
      const uids = st.dragons.map((d) => d.uid).concat(['ghost']);
      const k = randomInt(r, 24);
      const target = () => (randomInt(r, 3) === 0 ? { kind: 'storage' } : { kind: 'zone', zoneId: pickUniform(r, ['origin', 'origin', 'ocean']), habitatId: pickUniform(r, habitats) });
      let c: Record<string, unknown>;
      if (k < 3) c = cmd.buy(randomInt(r, 10) === 0 && n > 0 ? `p-${n}` : undefined);
      else if (k < 6) c = cmd.claim(target());
      else if (k < 8) c = cmd.status(pickUniform(r, ['active', 'skipped', 'skipped', 'completed']));
      else if (k < 10) c = cmd.view(pickUniform(r, species));
      else if (k < 12) c = cmd.survey(pickUniform(r, habitats));
      else if (k < 14) c = cmd.unlock(pickUniform(r, habitats));
      else if (k < 16) c = cmd.move(pickUniform(r, uids), target());
      else if (k < 17) c = { type: 'setProductionSlot', requestId: id(), dragonUid: pickUniform(r, uids), enabled: randomInt(r, 2) === 0 };
      else if (k < 19) c = cmd.adv(randomInt(r, 3) === 0 ? 'offline' : 'online');
      else if (k < 21) c = cmd.feed(pickUniform(r, ['fruit', 'leaf', 'aquatic', 'aether']), pickUniform(r, ['origin_meadow', 'origin_pondbank', 'origin_warm_rocks']));
      else if (k < 23) c = cmd.eat(pickUniform(r, uids))(st);
      else c = { type: 'pet', requestId: id(), dragonUid: pickUniform(r, uids) };
      return { advance: adv, cmd: c };
    };
    scenarios.push(runScenario('progress-fuzz', s, gen)); }

  writeFileSync(process.env.GOLDEN_OUT ?? 'progress-golden.json', JSON.stringify({ source: 'nldanh13/dravio@b33ec7a3dc98b4a74857fe92d143475028fc0d96', T0, implemented: implementedAchievements(), scenarios }));
});
