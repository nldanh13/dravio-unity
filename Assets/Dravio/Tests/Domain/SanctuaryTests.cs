using System;
using System.Collections.Generic;
using System.Linq;
using Dravio.Domain.Breeding;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Economy;
using Dravio.Domain.Progression;
using Dravio.Domain.Sanctuary;
using Dravio.Domain.World;
using NUnit.Framework;
using static Dravio.Domain.Tests.Support.Fixtures;

namespace Dravio.Domain.Tests
{
    /// <summary>
    /// U2.7 — kho rồng, nâng cấp, trang trí, Bách khoa và 10 thành tựu còn lại. IMPLEMENTATION MỚI theo GDD 3.1 (trang 15, 22, Phụ lục C.3, E, F);
    /// chỉ sellPriceG / upgradeCostG là hàm port từ TS (golden U2.1).
    /// </summary>
    public sealed class SanctuaryTests
    {
        private const long G = 1_000_000;
        private static readonly MoveTarget Meadow = MoveTarget.Zone("origin", "origin_meadow");

        private static (GameState, FixedClock) Rich(long gold = 100_000)
        {
            var clock = new FixedClock(T0);
            GameState s = InitialStateFactory.Create("save-1", T0, Rng.Create(101));
            s.Player.Gold = MicroGold.FromWholeGold(gold);
            // Đã nhận thưởng trưởng thành: tránh +100 G làm lệch các phép so vàng.
            s.UnlockedAchievements.Add("ACH_FIRST_ADULT"); s.ClaimedAchievements.Add("ACH_FIRST_ADULT");
            return (s, clock);
        }

        private static GameState With(GameState s, params DragonState[] ds) { GameState c = s.DeepClone(); c.Dragons.AddRange(ds); return Dravio.Domain.Tests.Support.Fixtures.Normalize(c); }
        private static void As(DragonState d, string species) { d.SpeciesId = species; d.XpSec = SpeciesCatalog.Get(species).GrowthTimeSec; }
        private static long Gold(GameState s) => s.Player.Gold.Micro;
        private static long Earned(GameState a, GameState b) => b.Player.TotalGoldEarned.Micro - a.Player.TotalGoldEarned.Micro;
        private static long Rewards(DispatchResult r) => r.Events.OfType<AchievementUnlocked>().Sum(e => e.RewardGoldMicro);
        private static List<string> Unlocked(DispatchResult r) => r.Events.OfType<AchievementUnlocked>().Select(e => e.AchievementId).ToList();

        // ---------------- sellDragon ----------------

        [Test]
        public void Sell_pays_stage_and_mutation_price_outside_totalGoldEarned_and_removes_the_dragon_everywhere()
        {
            var (s0, clock) = Rich(0);
            GameState s = With(s0, Adult("a", "magma", d => { d.Mutation = MutationType.Crystal; d.Location = DragonLocation.Zone("origin", "origin_meadow"); }), Adult("j", "sprout", d => d.XpSec = 200));
            s.ProductionDragonIds.Add("a");
            DispatchResult r = Ok(s, new SellDragonCommand(Rid(), "a"), clock);
            Assert.That(Gold(r.State) - Gold(s) - Earned(s, r.State) - Rewards(r), Is.EqualTo((long)Math.Floor(380 * 1.0 * 2.2) * G), "magma Adult Crystal = floor(380 × 1 × 2,2)");
            Assert.That(Unlocked(r), Is.EqualTo(new[] { "ACH_MUTATION_CRYSTAL" }), "ô Bách khoa được ghi trước khi rồng rời kho");
            Assert.That(r.Events.OfType<DragonSold>().Single().PriceGoldMicro, Is.EqualTo(836 * G));
            Assert.That(r.State.Player.TotalGoldEarned, Is.EqualTo(s.Player.TotalGoldEarned), "vàng bán không tính vào totalGoldEarned");
            Assert.That((r.State.FindDragon("a"), r.State.ProductionDragonIds.Contains("a"), r.State.Player.Stats.TotalSold), Is.EqualTo(((DragonState)null, false, 1L)));
            Assert.That(Codex.Find(r.State, "magma").DiscoveredMutations, Does.Contain(MutationType.Crystal), "Bách khoa giữ khám phá sau khi bán");
            long juv = EconomyRates.SellPriceG(SpeciesCatalog.Get("sprout"), GrowthStage.Juvenile, MutationType.Normal);
            Assert.That(Ok(r.State, new SellDragonCommand(Rid(), "j"), clock).Events.OfType<DragonSold>().Single().PriceGoldMicro, Is.EqualTo(juv * G));
            Fail(r.State, new SellDragonCommand(Rid(), "a"), clock, DomainErrorCode.UnknownDragon);
        }

        [Test]
        public void Sell_refuses_locked_busy_care_shelter_released_and_the_last_dragon()
        {
            var (s0, clock) = Rich();
            GameState s = With(s0,
                Adult("lk", "emberling", d => d.Locked = true), Adult("ex", "emberling", d => d.Activity = DragonActivity.Expedition),
                Adult("ca", "emberling", d => d.Location = DragonLocation.Care("c1")), Adult("sh", "emberling", d => d.Location = DragonLocation.Shelter("origin")),
                Adult("rl", "emberling", d => d.Location = DragonLocation.Released));
            Fail(s, new SellDragonCommand(Rid(), "lk"), clock, DomainErrorCode.DragonLocked);
            Fail(s, new SellDragonCommand(Rid(), "ex"), clock, DomainErrorCode.DragonBusy);
            Fail(s, new SellDragonCommand(Rid(), "ca"), clock, DomainErrorCode.DragonBusy);
            Fail(s, new SellDragonCommand(Rid(), "sh"), clock, DomainErrorCode.DragonBusy);
            Fail(s, new SellDragonCommand(Rid(), "rl"), clock, DomainErrorCode.DragonUnavailable);
            Fail(s0, new SellDragonCommand(Rid(), s0.Dragons[0].Uid), clock, DomainErrorCode.LastDragon);
            // Bố mẹ đang ấp không bán được.
            GameState pair = With(s0, Adult("pa", "emberling"), Adult("pb", "sprout"));
            GameState breeding = Ok(pair, new StartBreedCommand(Rid(), "pa", "pb"), clock).State;
            Fail(breeding, new SellDragonCommand(Rid(), "pa"), clock, DomainErrorCode.DragonBusy);
        }

        [Test]
        public void Selling_a_parent_keeps_the_childs_lineage_so_inbreeding_stays_blocked()
        {
            var (s0, clock) = Rich();
            GameState s = Ok(With(s0, Adult("pa", "emberling"), Adult("pb", "sprout")), new StartBreedCommand(Rid(), "pa", "pb"), clock).State;
            clock.Set(s.BreedingNest.FinishAtMs);
            s = Ok(s, new ClaimBreedCommand(Rid(), MoveTarget.Storage), clock).State;
            string child = s.Dragons.Last().Uid;
            clock.Advance(BreedingCatalog.ParentCooldownMs);
            s = Ok(s, new SellDragonCommand(Rid(), "pb"), clock).State;
            GameState grown = s.DeepClone();
            As(grown.FindDragon(child), grown.FindDragon(child).SpeciesId);
            Assert.That(grown.FindDragon(child).ParentIds, Is.EqualTo(new[] { "pa", "pb" }), "parentIds không bị sửa khi bố/mẹ bị bán");
            Fail(grown, new StartBreedCommand(Rid(), child, "pa"), clock, DomainErrorCode.BreedRelated);
        }

        // ---------------- bulkSell & lock ----------------

        [Test]
        public void BulkSell_sells_unlocked_idle_common_adults_after_confirming_the_quoted_total()
        {
            var (s0, clock) = Rich(0);
            GameState s = With(s0, Adult("c1", "emberling"), Adult("c2", "zephyr", d => d.Mutation = MutationType.Golden), Adult("lk", "droplet", d => d.Locked = true),
                Adult("rare", "magma"), Adult("juv", "sprout", d => d.XpSec = 100), Adult("busy", "emberling", d => d.Location = DragonLocation.Care("c9")));
            long quote = SanctuaryRules.BulkSellQuoteMicro(s);
            Assert.That(quote, Is.EqualTo((75 + (long)Math.Floor(85 * 4.5)) * G));
            Fail(s, new BulkSellCommand(Rid(), quote - 1), clock, DomainErrorCode.QuoteChanged);
            DispatchResult r = Ok(s, new BulkSellCommand(Rid(), quote), clock);
            Assert.That(r.Events.OfType<DragonSold>().Select(e => e.DragonUid), Is.EqualTo(new[] { "c1", "c2" }));
            Assert.That(Gold(r.State) - Gold(s) - Earned(s, r.State) - Rewards(r), Is.EqualTo(quote));
            Fail(r.State, new BulkSellCommand(Rid(), quote), clock, DomainErrorCode.NothingToSell);
        }

        [Test]
        public void BulkSell_always_keeps_one_dragon()
        {
            var (s0, clock) = Rich(0);
            GameState s = s0.DeepClone();
            As(s.Dragons[0], "emberling");
            s.Dragons.Add(Adult("c2", "sprout"));
            DispatchResult r = Ok(s, new BulkSellCommand(Rid(), SanctuaryRules.BulkSellQuoteMicro(s)), clock);
            Assert.That(r.State.Dragons.Select(d => d.Uid), Is.EqualTo(new[] { s.Dragons[0].Uid }), "chừa rồng đầu tiên trong kho");
        }

        [Test]
        public void Lock_toggles_protects_from_selling_and_does_not_block_breeding()
        {
            var (s0, clock) = Rich();
            GameState s = With(s0, Adult("pa", "emberling"), Adult("pb", "sprout"));
            DispatchResult l = Ok(s, new LockDragonCommand(Rid(), "pa", true), clock);
            Assert.That(l.Events.OfType<DragonLockChanged>().Single().Locked, Is.True);
            Fail(l.State, new LockDragonCommand(Rid(), "pa", true), clock, DomainErrorCode.AlreadyThere);
            Fail(l.State, new SellDragonCommand(Rid(), "pa"), clock, DomainErrorCode.DragonLocked);
            Assert.That(Ok(l.State, new StartBreedCommand(Rid(), "pa", "pb"), clock).State.BreedingNest, Is.Not.Null);
            Assert.That(Ok(l.State, new LockDragonCommand(Rid(), "pa", false), clock).State.FindDragon("pa").Locked, Is.False);
            Fail(s, new LockDragonCommand(Rid(), "ghost", true), clock, DomainErrorCode.UnknownDragon);
        }

        // ---------------- upgrades ----------------

        [Test]
        public void Every_upgrade_charges_floor_base_scale_pow_level_until_its_max()
        {
            foreach (UpgradeDef def in EconomyTables.Upgrades)
            {
                var (s, clock) = Rich(100_000_000);
                for (int level = 0; level < def.Max; level++)
                {
                    long cost = EconomyRates.UpgradeCostG(def, level).Value;
                    Assert.That(cost, Is.EqualTo((long)Math.Floor(def.BaseG * Math.Pow(def.Scale, level))));
                    GameState poor = s.DeepClone(); poor.Player.Gold = MicroGold.FromMicro(cost * G - 1);
                    Fail(poor, new BuyUpgradeCommand(Rid(), def.Id), clock, DomainErrorCode.NotEnoughGold);
                    DispatchResult r = Ok(s, new BuyUpgradeCommand(Rid(), def.Id), clock);
                    Assert.That(Gold(s) - Gold(r.State) + Earned(s, r.State), Is.EqualTo(cost * G), def.Id + " level " + level);
                    Assert.That(def.LevelOf(r.State.Player.Upgrades), Is.EqualTo(level + 1));
                    s = r.State;
                }
                Fail(s, new BuyUpgradeCommand(Rid(), def.Id), clock, DomainErrorCode.UpgradeMaxed);
            }
            var (x, c) = Rich();
            Fail(x, new BuyUpgradeCommand(Rid(), "goldMultiplier"), c, DomainErrorCode.UnknownUpgrade);
        }

        [Test]
        public void Upgrade_effects_budget_food_and_hatch_speed_apply_only_as_GDD_says()
        {
            var (s0, clock) = Rich(100_000);
            GameState s = Ok(s0, new BuyUpgradeCommand(Rid(), "islandCapacityLevel"), clock).State;
            Assert.That(Zones.CurrentProductionBudget(s), Is.EqualTo(7));
            GameState pair = With(s, Adult("pa", "emberling"), Adult("pb", "sprout"));
            GameState nest = Ok(pair, new StartBreedCommand(Rid(), "pa", "pb"), clock).State;
            long before = nest.BreedingNest.DurationMs;
            GameState faster = Ok(nest, new BuyUpgradeCommand(Rid(), "hatchSpeedLevel"), clock).State;
            Assert.That(faster.BreedingNest.DurationMs, Is.EqualTo(before), "hatchSpeed chỉ áp cho tổ mới");
            Assert.That(faster.Player.Upgrades.HatchSpeedLevel, Is.EqualTo(1));
        }

        // ---------------- decorations ----------------

        [Test]
        public void Decorations_cost_the_catalog_price_once_per_zone_toggle_free_and_never_change_gold_rates()
        {
            var (s0, clock) = Rich(100_000);
            DispatchResult b = Ok(s0, new BuyDecorationCommand(Rid(), "origin", "crystal_pond"), clock);
            Assert.That(Gold(s0) - Gold(b.State) + Earned(s0, b.State), Is.EqualTo(1200 * G));
            ZoneState z = b.State.FindZone("origin");
            Assert.That((z.DecorationIds.Single(), z.ActiveDecorationIds.Single()), Is.EqualTo(("crystal_pond", "crystal_pond")));
            Fail(b.State, new BuyDecorationCommand(Rid(), "origin", "crystal_pond"), clock, DomainErrorCode.DecorationOwned);
            Fail(b.State, new BuyDecorationCommand(Rid(), "ocean", "cherry_tree"), clock, DomainErrorCode.ZoneLocked);
            Fail(b.State, new BuyDecorationCommand(Rid(), "origin", "gold_statue"), clock, DomainErrorCode.UnknownDecoration);
            GameState off = Ok(b.State, new ToggleDecorationCommand(Rid(), "origin", "crystal_pond", false), clock).State;
            Assert.That((off.FindZone("origin").ActiveDecorationIds.Count, Gold(off)), Is.EqualTo((0, Gold(b.State))), "bật/tắt miễn phí");
            Fail(off, new ToggleDecorationCommand(Rid(), "origin", "crystal_pond", false), clock, DomainErrorCode.AlreadyThere);
            Fail(off, new ToggleDecorationCommand(Rid(), "origin", "cherry_tree", true), clock, DomainErrorCode.DecorationNotOwned);
            // Không hiệu ứng kinh tế: cùng một khoảng thời gian, vàng sản xuất như nhau có hay không có trang trí.
            clock.Advance(600_000);
            long withDecor = Earned(b.State, Ok(b.State, Online(), clock).State);
            GameState plain = s0.DeepClone(); plain.Player.Gold = b.State.Player.Gold;
            Assert.That(withDecor, Is.EqualTo(Earned(s0, Ok(s0, Online(), clock).State)));
        }

        // ---------------- achievements & codex ----------------

        private static GameState WithPending(GameState s, string species, MutationType m, FixedClock clock)
        {
            GameState b = Ok(s, new BuyPackCommand(Rid(), "starter"), clock).State.DeepClone();
            b.PendingPack.SpeciesId = species; b.PendingPack.Mutation = m;
            return b;
        }

        [TestCase("crystal", "ACH_MUTATION_CRYSTAL", 100)]
        [TestCase("golden", "ACH_MUTATION_GOLDEN", 300)]
        [TestCase("prismatic", "ACH_MUTATION_PRISMATIC", 1000)]
        public void Mutation_achievements_unlock_on_the_first_owned_dragon_of_that_variant_once(string mutation, string id, long rewardG)
        {
            var (s, clock) = Rich();
            WireNames.TryParse(mutation, out MutationType m);
            GameState p = WithPending(s, "sprout", m, clock);
            Assert.That(Unlocked(Ok(s, new BuyPackCommand(Rid(), "starter"), clock)), Does.Not.Contain(id), "trứng chưa nhận không tính");
            DispatchResult c = Ok(p, new ClaimPackCommand(Rid(), MoveTarget.Storage), clock);
            Assert.That(c.Events.OfType<AchievementUnlocked>().Single(e => e.AchievementId == id).RewardGoldMicro, Is.EqualTo(rewardG * G));
            GameState sold = Ok(c.State, new SellDragonCommand(Rid(), c.State.Dragons.Last().Uid), clock).State;
            GameState again = Ok(WithPending(sold, "sprout", m, clock), new ClaimPackCommand(Rid(), MoveTarget.Storage), clock).State;
            Assert.That(again.UnlockedAchievements.Count(x => x == id), Is.EqualTo(1), "chỉ thưởng một lần, kể cả sau khi bán và có lại");
        }

        [Test]
        public void Legendary_first_breed_and_codex_milestones_follow_appendix_F()
        {
            var (s, clock) = Rich();
            DispatchResult leg = Ok(WithPending(s, "gaia", MutationType.Normal, clock), new ClaimPackCommand(Rid(), MoveTarget.Storage), clock);
            Assert.That(Unlocked(leg), Does.Contain("ACH_LEGENDARY"));
            GameState pair = With(s, Adult("pa", "emberling"), Adult("pb", "sprout"));
            GameState nest = Ok(pair, new StartBreedCommand(Rid(), "pa", "pb"), clock).State;
            Assert.That(nest.UnlockedAchievements, Does.Not.Contain("ACH_FIRST_BREED"), "bắt đầu lai chưa tính");
            clock.Set(nest.BreedingNest.FinishAtMs);
            Assert.That(Unlocked(Ok(nest, new ClaimBreedCommand(Rid(), MoveTarget.Storage), clock)), Does.Contain("ACH_FIRST_BREED"));

            // Bách khoa: 15 loài → chưa; đủ 16 → ACH_CODEX_16. 31 ô → chưa; 32 → ACH_CODEX_MASTER.
            var all = SpeciesCatalog.All.Select(sp => sp.Id).ToList();
            GameState fifteen = With(s, all.Skip(1).Take(14).Select((id, i) => Adult("x" + i, id)).ToArray()); // + emberling khởi đầu = 15
            Assert.That(Ok(fifteen, Online(), clock).State.UnlockedAchievements, Does.Not.Contain("ACH_CODEX_16"));
            GameState sixteen = With(fifteen, Adult("last", all.Last()));
            DispatchResult c16 = Ok(sixteen, Online(), clock);
            Assert.That(Unlocked(c16), Does.Contain("ACH_CODEX_16"));
            Assert.That(Codex.SpeciesDiscovered(c16.State), Is.EqualTo(16));
            var cells = new List<DragonState>();
            foreach (string id in all.Take(8)) foreach (MutationType m in Mutation.Order) cells.Add(Adult(id + m, id, d => d.Mutation = m));
            GameState c31 = With(s, cells.Where(d => d.Uid != "emberlingNormal").Take(30).ToArray()); // + Emberling Normal khởi đầu = 31
            Assert.That(Codex.CellsDiscovered(Ok(c31, Online(), clock).State), Is.EqualTo(31));
            Assert.That(Ok(c31, Online(), clock).State.UnlockedAchievements, Does.Not.Contain("ACH_CODEX_MASTER"));
            Assert.That(Unlocked(Ok(With(c31, cells.Last()), Online(), clock)), Does.Contain("ACH_CODEX_MASTER"));
        }

        [Test]
        public void Gold_milestones_count_only_production_and_decor_collection_needs_all_six_kinds()
        {
            var (s0, clock) = Rich(1_000_000);
            GameState near = s0.DeepClone();
            near.Player.TotalGoldEarned = MicroGold.FromMicro(10_000 * G - 1);
            GameState sold = Ok(With(near, Adult("x", "celestia")), new SellDragonCommand(Rid(), "x"), clock).State;
            Assert.That(sold.UnlockedAchievements, Does.Not.Contain("ACH_GOLD_10K"), "vàng bán không tính");
            clock.Advance(1000);
            Assert.That(Unlocked(Ok(near, Online(), clock)), Does.Contain("ACH_GOLD_10K"));
            GameState d = s0;
            foreach (DecorationDef def in DecorationCatalog.All.Take(5)) d = Ok(d, new BuyDecorationCommand(Rid(), "origin", def.Id), clock).State;
            Assert.That(d.UnlockedAchievements, Does.Not.Contain("ACH_DECOR_ALL"));
            DispatchResult all = Ok(d, new BuyDecorationCommand(Rid(), "origin", DecorationCatalog.All.Last().Id), clock);
            Assert.That(all.Events.OfType<AchievementUnlocked>().Single(e => e.AchievementId == "ACH_DECOR_ALL").RewardGoldMicro, Is.EqualTo(1000 * G));
        }

        [Test]
        public void Codex_records_hatches_per_species_but_not_the_starting_emberling()
        {
            var (s, clock) = Rich();
            Assert.That(Codex.Find(s, "emberling").TotalHatched, Is.EqualTo(0));
            GameState c = Ok(WithPending(s, "emberling", MutationType.Normal, clock), new ClaimPackCommand(Rid(), MoveTarget.Storage), clock).State;
            Assert.That(Codex.Find(c, "emberling").TotalHatched, Is.EqualTo(1));
            clock.Advance(400_000);
            Assert.That(Codex.Find(Ok(c, Online(), clock).State, "emberling").ObservedStages, Is.EqualTo(new[] { GrowthStage.Baby, GrowthStage.Juvenile, GrowthStage.Adult }));
        }
    }
}
