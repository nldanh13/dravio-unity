using System;
using System.Collections.Generic;
using System.Linq;
using Dravio.Domain.Breeding;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Economy;
using Dravio.Domain.World;
using NUnit.Framework;
using static Dravio.Domain.Tests.Support.Fixtures;

namespace Dravio.Domain.Tests
{
    /// <summary>
    /// U2.5 — lệnh buyPack / claimFreeStarterPack / startBreed / tapEgg / claimBreed. IMPLEMENTATION MỚI theo GDD 3.1 + ADR 0003:
    /// TS b33ec7a không có các lệnh này nên test ở đây kiểm luật tài liệu, không phải parity (parity của hàm thuần ở GeneticsGoldenParityTests).
    /// </summary>
    public sealed class HatcheryTests
    {
        private const long G = 1_000_000;
        private static readonly MoveTarget Meadow = MoveTarget.Zone("origin", "origin_meadow");

        private static (GameState, FixedClock) Rich(long gold = 100_000, string saveId = "save-1")
        {
            var clock = new FixedClock(T0);
            GameState s = InitialStateFactory.Create(saveId, T0, Rng.Create(101));
            s.Player.Gold = MicroGold.FromWholeGold(gold);
            return (s, clock);
        }

        /// <summary>Thêm cặp T Adult (Emberling + Sprout) vào kho.</summary>
        private static GameState WithPair(GameState s, Action<DragonState> a = null, Action<DragonState> b = null)
        {
            GameState c = s.DeepClone();
            c.Dragons.Add(Adult("pa", "emberling", a));
            c.Dragons.Add(Adult("pb", "sprout", b));
            return c;
        }

        private static GameState Fill(GameState s, int occupied)
        {
            GameState c = s.DeepClone();
            int i = 0;
            while (Hatchery.OccupiedSlots(c) < occupied) c.Dragons.Add(Adult("fill" + (i++), "zephyr"));
            return c;
        }

        /// <summary>Đổi loài và giữ Adult (XP = thời gian lớn của loài mới).</summary>
        private static void As(DragonState d, string species) { d.SpeciesId = species; d.XpSec = SpeciesCatalog.Get(species).GrowthTimeSec; }

        private static long Gold(GameState s) => s.Player.Gold.Micro;
        private static BuyPackCommand Buy(string pack, string rid = null) => new BuyPackCommand(rid ?? Rid(), pack);
        private static StartBreedCommand Breed(string a = "pa", string b = "pb") => new StartBreedCommand(Rid(), a, b);

        // ---------------- buyPack ----------------

        [Test]
        public void BuyPack_charges_exact_catalog_price_and_stores_one_rolled_egg()
        {
            foreach (PackDef pack in PackCatalog.All)
            {
                var (s0, clock) = Rich(pack.PriceG);
                DispatchResult r = Ok(s0, Buy(pack.Id), clock);
                long earned = r.State.Player.TotalGoldEarned.Micro - s0.Player.TotalGoldEarned.Micro;
                long rewards = r.Events.OfType<AchievementUnlocked>().Sum(e => e.RewardGoldMicro);
                Assert.That(Gold(r.State), Is.EqualTo(earned + rewards), pack.Id + ": paid exactly " + pack.PriceG + " G");
                Assert.That((r.State.PendingPack.PackId, r.State.PendingPack.Tutorial), Is.EqualTo((pack.Id, false)));
                Assert.That(SpeciesCatalog.Get(r.State.PendingPack.SpeciesId).PackEligible, Is.True);
                PackBought pb = r.Events.OfType<PackBought>().Single();
                Assert.That((pb.PackId, pb.PriceGoldMicro, pb.Tutorial), Is.EqualTo((pack.Id, pack.PriceG * G, false)));
                GameState poor = s0.DeepClone(); poor.Player.Gold = MicroGold.FromMicro(pack.PriceG * G - 1);
                Fail(poor, Buy(pack.Id), clock, DomainErrorCode.NotEnoughGold);
            }
        }

        [Test]
        public void BuyPack_rejects_unknown_pending_and_full_storage_without_touching_gold_or_pity()
        {
            var (s, clock) = Rich();
            Fail(s, Buy("legend-chest"), clock, DomainErrorCode.UnknownPack);
            GameState pending = Ok(s, Buy("elemental"), clock).State;
            Fail(pending, Buy("elemental"), clock, DomainErrorCode.PackPending);
            Fail(pending, new ClaimFreeStarterPackCommand(Rid()), clock, DomainErrorCode.PackPending);
            GameState full = Fill(s, StorageCatalog.MaxDragons);
            Fail(full, Buy("elemental"), clock, DomainErrorCode.StorageFull);
            Assert.That(Ok(Fill(s, StorageCatalog.MaxDragons - 1), Buy("elemental"), clock).State.PendingPack, Is.Not.Null, "499 + 1 trứng = 500 vẫn được");
        }

        [Test]
        public void BuyPack_is_idempotent_per_requestId_and_deterministic_per_state()
        {
            var (s, clock) = Rich();
            DispatchResult a = Ok(s, Buy("mystic", "buy-x"), clock);
            DispatchResult dup = CommandDispatcher.Dispatch(a.State, Buy("mystic", "buy-x"), clock);
            Assert.That((dup.Duplicate, Gold(dup.State), dup.Events.Count), Is.EqualTo((true, Gold(a.State), 0)), "không trừ tiền hai lần");
            // Cùng state + cùng thời điểm → cùng trứng (nạp lại save không roll lại được); requestId không ảnh hưởng kết quả.
            DispatchResult b = Ok(s, Buy("mystic", "other-id"), clock);
            Assert.That(Support.StateJson.ToJson(b.State)["pendingPack"], Is.EqualTo(Support.StateJson.ToJson(a.State)["pendingPack"]).Using<object, object>((x, y) => Support.MiniJson.Diff(x, y) == null));
            // Khác save → khác luồng RNG.
            var (o, _) = Rich(saveId: "save-2");
            Assert.That(Ok(o, Buy("mystic"), clock).State.PendingPack.Id, Is.Not.EqualTo(a.State.PendingPack.Id));
        }

        [Test]
        public void Regular_packs_count_pity_and_the_200th_roll_is_prismatic()
        {
            var (s, clock) = Rich();
            Assert.That(Ok(s, Buy("starter"), clock).State.Player.PityCount, Is.EqualTo(1).Or.EqualTo(0));
            GameState edge = s.DeepClone(); edge.Player.PityCount = 199;
            GameState p = Ok(edge, Buy("starter"), clock).State;
            Assert.That((p.PendingPack.Mutation, p.Player.PityCount), Is.EqualTo((MutationType.Prismatic, 0)));
            // Gói hướng dẫn không tính pity.
            GameState t = s.DeepClone(); t.Player.PityCount = 199;
            Assert.That(Ok(t, new BuyTutorialPackCommand(Rid()), clock).State.Player.PityCount, Is.EqualTo(199));
        }

        [Test]
        public void Claiming_a_regular_pack_uses_claimPack_and_counts_the_first_hatch_once()
        {
            var (s, clock) = Rich();
            GameState bought = Ok(s, Buy("elemental"), clock).State;
            DispatchResult c = Ok(bought, new ClaimPackCommand(Rid(), MoveTarget.Storage), clock);
            DragonState d = c.State.Dragons.Last();
            Assert.That((d.SpeciesId, d.Mutation, d.Origin, d.Generation, d.ParentIds), Is.EqualTo((bought.PendingPack.SpeciesId, bought.PendingPack.Mutation, DragonOrigin.Pack, 1, (string[])null)));
            Assert.That(c.State.PendingPack, Is.Null);
            Assert.That(c.Events.OfType<AchievementUnlocked>().Select(e => e.AchievementId), Is.EqualTo(new[] { "ACH_FIRST_HATCH" }));
            GameState again = Ok(c.State, Buy("elemental"), clock).State;
            DispatchResult c2 = Ok(again, new ClaimPackCommand(Rid(), MoveTarget.Storage), clock);
            Assert.That(c2.Events.OfType<AchievementUnlocked>(), Is.Empty, "thưởng lần đầu ấp trứng chỉ một lần");
            Assert.That(c2.State.Player.Stats.TotalHatched, Is.EqualTo(2));
        }

        // ---------------- claimFreeStarterPack ----------------

        [Test]
        public void Free_starter_is_limited_to_3_per_UTC_day_costs_nothing_and_resets_next_day()
        {
            var (s, clock) = Rich(0);
            for (int i = 0; i < StorageCatalog.DailyFreeStarterPacks; i++)
            {
                DispatchResult r = Ok(s, new ClaimFreeStarterPackCommand(Rid()), clock);
                Assert.That(r.State.PendingPack.PackId, Is.EqualTo("starter"));
                Assert.That(r.Events.OfType<PackBought>().Single().PriceGoldMicro, Is.EqualTo(0));
                Assert.That(Gold(r.State), Is.EqualTo(r.State.Player.TotalGoldEarned.Micro + r.Events.OfType<AchievementUnlocked>().Sum(e => e.RewardGoldMicro) + Gold(s) - s.Player.TotalGoldEarned.Micro));
                s = Ok(r.State, new ClaimPackCommand(Rid(), MoveTarget.Storage), clock).State;
                clock.Advance(1000);
            }
            Assert.That(s.Player.Stats.DailyFreePacksClaimed, Is.EqualTo(3));
            Fail(s, new ClaimFreeStarterPackCommand(Rid()), clock, DomainErrorCode.DailyLimit);
            long midnight = (T0 / 86_400_000 + 1) * 86_400_000;
            clock.Set(midnight - 1);
            Fail(s, new ClaimFreeStarterPackCommand(Rid()), clock, DomainErrorCode.DailyLimit);
            clock.Set(midnight);
            GameState next = Ok(s, new ClaimFreeStarterPackCommand(Rid()), clock).State;
            Assert.That((next.Player.Stats.DailyFreePacksClaimed, next.Player.Stats.LastDailyResetDate), Is.EqualTo((1L, JsCompat.UtcDay(midnight))));
        }

        // ---------------- startBreed ----------------

        [Test]
        public void StartBreed_charges_the_fee_of_the_rarest_parent_and_locks_both_parents()
        {
            var (s0, clock) = Rich(1000);
            GameState s = WithPair(s0);
            DispatchResult r = Ok(s, Breed(), clock);
            long earned = r.State.Player.TotalGoldEarned.Micro - s.Player.TotalGoldEarned.Micro + r.Events.OfType<AchievementUnlocked>().Sum(e => e.RewardGoldMicro);
            Assert.That(Gold(s) - Gold(r.State) + earned, Is.EqualTo(80 * G), "Common + Common = 80 G");
            BreedingEgg egg = r.State.BreedingNest;
            Assert.That(egg.ParentIds, Is.EqualTo(new[] { "pa", "pb" }));
            Assert.That(new[] { "emberling", "sprout", "magma" }, Does.Contain(egg.SpeciesId));
            Assert.That(egg.DurationMs, Is.EqualTo(BreedingRules.HatchSeconds(SpeciesCatalog.Get(egg.SpeciesId).Rarity, 0) * 1000));
            Assert.That((egg.Status, egg.FinishAtMs), Is.EqualTo((EggStatus.Incubating, clock.NowMs() + egg.DurationMs)));
            Assert.That(r.State.Dragons.Where(d => d.Uid == "pa" || d.Uid == "pb").Select(d => d.Activity), Is.All.EqualTo(DragonActivity.Breeding));
            Assert.That(r.Events.OfType<BreedStarted>().Single().FeeGoldMicro, Is.EqualTo(80 * G));
            // Parents busy: cannot move, sell (chưa port), or breed again.
            Fail(r.State, new MoveDragonCommand(Rid(), "pa", Meadow), clock, DomainErrorCode.DragonUnavailable);
            Fail(r.State, Breed(), clock, DomainErrorCode.NestBusy);
            GameState rare = WithPair(s0, a => As(a, "magma"));
            Assert.That(Ok(rare, Breed(), clock).Events.OfType<BreedStarted>().Single().FeeGoldMicro, Is.EqualTo(350 * G), "Rare + Common = 350 G");
        }

        [TestCase("same", DomainErrorCode.BreedSameDragon)]
        [TestCase("ghost", DomainErrorCode.UnknownDragon)]
        [TestCase("droplet", DomainErrorCode.BreedDifferentGroup)]
        [TestCase("baby", DomainErrorCode.BreedNotAdult)]
        [TestCase("sibling", DomainErrorCode.BreedRelated)]
        [TestCase("cooldown", DomainErrorCode.BreedCooldown)]
        [TestCase("released", DomainErrorCode.BreedReleased)]
        [TestCase("poor", DomainErrorCode.NotEnoughGold)]
        [TestCase("full", DomainErrorCode.StorageFull)]
        public void StartBreed_rejections_cost_nothing_and_consume_no_RNG(string kind, DomainErrorCode code)
        {
            var (s0, clock) = Rich(1000);
            GameState s = WithPair(s0);
            StartBreedCommand cmd = Breed();
            switch (kind)
            {
                case "same": cmd = Breed("pa", "pa"); break;
                case "ghost": cmd = Breed("pa", "nobody"); break;
                case "droplet": s = WithPair(s0, b: null, a: x => As(x, "droplet")); break;
                case "baby": s = WithPair(s0, b: x => x.XpSec = 0); break;
                case "sibling": s = WithPair(s0, a: x => x.ParentIds = new[] { "m", "f" }, b: x => x.ParentIds = new[] { "m", "z" }); break;
                case "cooldown": s = WithPair(s0, a: x => x.BreedCooldownUntilMs = T0 + 1); break;
                case "released": s = WithPair(s0, a: x => x.Location = DragonLocation.Released); break;
                case "poor": s.Player.Gold = MicroGold.FromMicro(80 * G - 1); break;
                case "full": s = Fill(s, StorageCatalog.MaxDragons); break;
            }
            int pity = s.Player.PityCount;
            Fail(s, cmd, clock, code);
            Assert.That(s.Player.PityCount, Is.EqualTo(pity));
        }

        // ---------------- incubation, tap, completion ----------------

        /// <summary>Cặp Adult đặt ở đồng cỏ, cả hai trong ngân sách sản xuất.</summary>
        private static GameState ProducingPair(GameState s0)
        {
            GameState s = WithPair(s0, a => a.Location = DragonLocation.Zone("origin", "origin_meadow"), b => b.Location = DragonLocation.Zone("origin", "origin_meadow"));
            s.ProductionDragonIds.AddRange(new[] { "pa", "pb" });
            return s;
        }

        [Test]
        public void Parents_stop_producing_while_incubating_and_resume_exactly_at_finish_with_10_minute_cooldown()
        {
            var (s0, clock) = Rich(1000);
            GameState s = Ok(ProducingPair(s0), Breed(), clock).State;
            BreedingEgg egg = s.BreedingNest;
            // Bỏ rồng Baby ban đầu khỏi phép so: chỉ đo vàng của bố mẹ.
            GameState noBaby = s.DeepClone(); noBaby.ProductionDragonIds.RemoveAll(id => id != "pa" && id != "pb");
            clock.Set(egg.FinishAtMs + 60_000);
            DispatchResult r = Ok(noBaby, Online(), clock);
            long goldDelta = r.State.Player.TotalGoldEarned.Micro - noBaby.Player.TotalGoldEarned.Micro;
            Assert.That(goldDelta, Is.EqualTo(60 * (25_000 + 27_000)), "chỉ 60 s sau khi nở bố mẹ mới sinh vàng (Emberling 0,025 + Sprout 0,027 G/s)");
            EggReady ready = r.Events.OfType<EggReady>().Single();
            Assert.That(ready.AtMs, Is.EqualTo(egg.FinishAtMs));
            Assert.That(r.State.BreedingNest.Status, Is.EqualTo(EggStatus.Ready));
            foreach (string uid in new[] { "pa", "pb" })
            {
                DragonState p = r.State.FindDragon(uid);
                Assert.That((p.Activity, p.BreedCooldownUntilMs), Is.EqualTo((DragonActivity.Idle, egg.FinishAtMs + BreedingCatalog.ParentCooldownMs)));
            }
        }

        [Test]
        public void Incubation_completes_offline_too_and_parents_are_released_even_if_the_egg_waits()
        {
            var (s0, clock) = Rich(1000);
            GameState s = Ok(WithPair(s0), Breed(), clock).State;
            clock.Set(s.BreedingNest.FinishAtMs + 3_600_000);
            DispatchResult r = Ok(s, Offline(), clock);
            Assert.That(r.State.BreedingNest.Status, Is.EqualTo(EggStatus.Ready));
            Assert.That(r.State.FindDragon("pa").Activity, Is.EqualTo(DragonActivity.Idle));
            Fail(r.State, Breed(), clock, DomainErrorCode.NestBusy);
        }

        [Test]
        public void Tap_reduces_3s_at_most_every_2s_and_at_most_10_percent()
        {
            var (s0, clock) = Rich(1000);
            GameState s = Ok(WithPair(s0), Breed(), clock).State;
            BreedingEgg egg0 = s.BreedingNest;
            long cap = (long)Math.Floor(egg0.DurationMs * 0.1);
            DispatchResult t = Ok(s, new TapEggCommand(Rid()), clock);
            Assert.That(t.State.BreedingNest.FinishAtMs, Is.EqualTo(egg0.FinishAtMs - 3000));
            Fail(t.State, new TapEggCommand(Rid()), clock, DomainErrorCode.TapTooFast);
            clock.Advance(1999);
            Fail(t.State, new TapEggCommand(Rid()), clock, DomainErrorCode.TapTooFast);
            s = t.State;
            int taps = 1;
            while (true)
            {
                clock.Advance(2000);
                DispatchResult x = CommandDispatcher.Dispatch(s, new TapEggCommand(Rid()), clock);
                if (!x.Ok) { Assert.That(x.Error.Code, Is.EqualTo(DomainErrorCode.TapLimit)); break; }
                s = x.State; taps++;
            }
            Assert.That(s.BreedingNest.TapReducedMs, Is.EqualTo(cap));
            Assert.That(taps, Is.EqualTo((int)Math.Ceiling(cap / 3000.0)));
            Assert.That(s.BreedingNest.FinishAtMs, Is.EqualTo(egg0.FinishAtMs - cap));
            Fail(s0, new TapEggCommand(Rid()), clock, DomainErrorCode.NoEgg);
        }

        [Test]
        public void Last_tap_only_takes_what_is_left_of_the_10_percent_budget()
        {
            var (s0, clock) = Rich(1000);
            GameState s = Ok(WithPair(s0), Breed(), clock).State.DeepClone();
            BreedingEgg egg = s.BreedingNest;
            egg.DurationMs = 100_000; egg.FinishAtMs = egg.CreatedAtMs + egg.DurationMs; // ngân sách gõ 10 000 ms: 3 + 3 + 3 + 1 s
            var reduced = new List<long>();
            for (int i = 0; i < 5; i++)
            {
                DispatchResult r = CommandDispatcher.Dispatch(s, new TapEggCommand(Rid()), clock);
                clock.Advance(2000);
                if (!r.Ok) { Assert.That(r.Error.Code, Is.EqualTo(DomainErrorCode.TapLimit)); break; }
                Assert.That(GameStateValidator.Validate(r.State).IsValid, Is.True);
                reduced.Add(r.Events.OfType<EggTapped>().Single().ReducedMs);
                s = r.State;
            }
            Assert.That(reduced, Is.EqualTo(new long[] { 3000, 3000, 3000, 1000 }));
        }

        [Test]
        public void Egg_in_the_nest_reserves_a_storage_slot()
        {
            var (s0, clock) = Rich(10_000);
            GameState s = Ok(WithPair(s0), Breed(), clock).State;
            GameState full = Fill(s, StorageCatalog.MaxDragons);
            Assert.That(full.Dragons.Count(d => d.Location.Kind != LocationKind.Released), Is.EqualTo(StorageCatalog.MaxDragons - 1), "trứng trong tổ chiếm chỗ thứ 500");
            Fail(full, Buy("starter"), clock, DomainErrorCode.StorageFull);
            clock.Set(s.BreedingNest.FinishAtMs);
            Assert.That(Ok(full, new ClaimBreedCommand(Rid(), MoveTarget.Storage), clock).State.Dragons.Count(d => d.Location.Kind != LocationKind.Released),
                Is.EqualTo(StorageCatalog.MaxDragons), "chỗ đã giữ trước nên trứng luôn nhận được");
        }

        // ---------------- claimBreed & lineage ----------------

        private static (GameState, FixedClock) Hatched(Action<DragonState> a = null, Action<DragonState> b = null, string saveId = "save-1")
        {
            var (s0, clock) = Rich(10_000, saveId);
            GameState s = Ok(WithPair(s0, a, b), Breed(), clock).State;
            Fail(s, new ClaimBreedCommand(Rid(), MoveTarget.Storage), clock, DomainErrorCode.EggNotReady);
            clock.Set(s.BreedingNest.FinishAtMs);
            return (Ok(s, new ClaimBreedCommand(Rid(), MoveTarget.Storage), clock).State, clock);
        }

        [Test]
        public void ClaimBreed_records_parents_generation_grandparents_and_counts_once()
        {
            var (s, clock) = Hatched(a => { a.ParentIds = new[] { "g1", "g2" }; a.Generation = 2; a.Ancestry = Ancestry.Known; });
            DragonState child = s.Dragons.Last();
            Assert.That(child.ParentIds, Is.EqualTo(new[] { "pa", "pb" }));
            Assert.That(child.GrandparentIds, Is.EqualTo(new[] { "g1", "g2" }), "chỉ ông bà đã biết; phía Sprout founder không tự gán");
            Assert.That((child.Generation, child.Ancestry, child.Origin, child.XpSec), Is.EqualTo((3, Ancestry.Known, DragonOrigin.Breed, 0.0)));
            Assert.That(s.BreedingNest, Is.Null);
            Assert.That((s.Player.Stats.TotalHatched, s.Player.Stats.TotalBred), Is.EqualTo((1L, 1L)));
            Fail(s, new ClaimBreedCommand(Rid(), MoveTarget.Storage), clock, DomainErrorCode.NoEgg);
            // Hai founder → con không có grandparentIds (unknown ≠ không có họ hàng).
            var (f, _) = Hatched();
            Assert.That((f.Dragons.Last().GrandparentIds, f.Dragons.Last().Generation), Is.EqualTo(((List<string>)null, 2)));
        }

        [Test]
        public void Offspring_cannot_breed_with_parents_or_siblings()
        {
            var (s, clock) = Hatched();
            DragonState child = s.Dragons.Last();
            GameState grown = s.DeepClone();
            grown.FindDragon(child.Uid).XpSec = SpeciesCatalog.Get(child.SpeciesId).GrowthTimeSec;
            clock.Advance(BreedingCatalog.ParentCooldownMs);
            Fail(grown, Breed(child.Uid, "pa"), clock, DomainErrorCode.BreedRelated);
            Fail(grown, Breed(child.Uid, "pb"), clock, DomainErrorCode.BreedRelated);
            GameState sib = grown.DeepClone();
            DragonState other = child.Clone(); other.Uid = "sib"; As(other, child.SpeciesId == "sprout" ? "emberling" : "sprout");
            sib.Dragons.Add(other);
            Fail(sib, Breed(child.Uid, "sib"), clock, DomainErrorCode.BreedRelated);
        }

        [Test]
        public void ClaimBreed_into_a_zone_uses_moveDragon_placement_rules()
        {
            var (s0, clock) = Rich(10_000);
            GameState s = Ok(WithPair(s0), Breed(), clock).State;
            clock.Set(s.BreedingNest.FinishAtMs);
            Fail(s, new ClaimBreedCommand(Rid(), MoveTarget.Zone("ocean", "ocean_reef")), clock, DomainErrorCode.ZoneLocked);
            Fail(s, new ClaimBreedCommand(Rid(), MoveTarget.Zone("origin", "origin_pondbank")), clock, DomainErrorCode.HabitatLocked);
            Assert.That(Hatchery.NestPlacementOptions(s).Select(o => o.HabitatId), Does.Contain("origin_meadow"));
        }

        [Test]
        public void Breeding_rolls_pity_with_mutated_parent_bonus()
        {
            var (s0, clock) = Rich(1000);
            GameState s = WithPair(s0); s.Player.PityCount = 199;
            GameState r = Ok(s, Breed(), clock).State;
            Assert.That((r.BreedingNest.Mutation, r.Player.PityCount), Is.EqualTo((MutationType.Prismatic, 0)));
        }
    }
}
