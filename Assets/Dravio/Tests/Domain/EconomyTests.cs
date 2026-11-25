using System.Linq;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Economy;
using NUnit.Framework;

namespace Dravio.Domain.Tests
{
    /// <summary>Port phần "growth and rates" của tests/unit/economy.test.ts và kiểm MicroGold.</summary>
    public sealed class EconomyTests
    {
        private static readonly DragonSpecies Ember = SpeciesCatalog.Get("emberling");

        [Test]
        public void Derives_stage_from_xpSec()
        {
            Assert.That(EconomyRates.GrowthStageOf(0, Ember), Is.EqualTo(GrowthStage.Baby));
            Assert.That(EconomyRates.GrowthStageOf(104, Ember), Is.EqualTo(GrowthStage.Baby));
            Assert.That(EconomyRates.GrowthStageOf(105, Ember), Is.EqualTo(GrowthStage.Juvenile));
            Assert.That(EconomyRates.GrowthStageOf(299, Ember), Is.EqualTo(GrowthStage.Juvenile));
            Assert.That(EconomyRates.GrowthStageOf(300, Ember), Is.EqualTo(GrowthStage.Adult));
        }

        [Test]
        public void GDD_example_5_adult_Emberling_offline_8h_gives_2700_G()
        {
            long rate = 5 * EconomyRates.RateMicroGPerSec(Ember, GrowthStage.Adult, MutationType.Normal);
            var r = EconomyRates.OfflineGoldMicro(rate, 8 * 3600 * 1000);
            Assert.That(r.goldMicro / MicroGold.MicroPerGold, Is.EqualTo(2700));
            Assert.That(r.goldMicro % MicroGold.MicroPerGold, Is.EqualTo(0));
        }

        [Test]
        public void Offline_helper_keeps_the_fractional_remainder()
        {
            var one = EconomyRates.OfflineGoldMicro(1, 1);
            Assert.That(one.goldMicro, Is.EqualTo(0));
            var two = EconomyRates.OfflineGoldMicro(1, 1_333, one.remainder);
            Assert.That(two.goldMicro, Is.EqualTo(1));
        }

        [Test]
        public void Sell_price_follows_stage_and_mutation_multipliers()
        {
            Assert.That(EconomyRates.SellPriceG(Ember, GrowthStage.Adult, MutationType.Normal), Is.EqualTo(75));
            Assert.That(EconomyRates.SellPriceG(Ember, GrowthStage.Baby, MutationType.Normal), Is.EqualTo(26));
            Assert.That(EconomyRates.SellPriceG(Ember, GrowthStage.Adult, MutationType.Prismatic), Is.EqualTo(750));
        }

        [Test]
        public void Production_budget_is_5_plus_2_per_level_max_25()
        {
            Assert.That(EconomyRates.ProductionBudget(0), Is.EqualTo(5));
            Assert.That(EconomyRates.ProductionBudget(10), Is.EqualTo(25));
            Assert.That(EconomyRates.ProductionBudget(99), Is.EqualTo(25));
        }

        [Test]
        public void Upgrade_cost_is_floor_base_times_scale_pow_level_null_at_max()
        {
            UpgradeDef cap = EconomyTables.Upgrades.Single(u => u.Id == "islandCapacityLevel");
            Assert.That(EconomyRates.UpgradeCostG(cap, 0), Is.EqualTo(500));
            Assert.That(EconomyRates.UpgradeCostG(cap, 1), Is.EqualTo(925));
            Assert.That(EconomyRates.UpgradeCostG(cap, 10), Is.Null);
        }

        [Test]
        public void Rate_examples_in_microG_per_second()
        {
            Assert.That(EconomyRates.RateMicroGPerSec(Ember, GrowthStage.Baby, MutationType.Normal), Is.EqualTo(10_000));
            Assert.That(EconomyRates.RateMicroGPerSec(Ember, GrowthStage.Juvenile, MutationType.Normal), Is.EqualTo(18_750));
            Assert.That(EconomyRates.RateMicroGPerSec(Ember, GrowthStage.Adult, MutationType.Crystal), Is.EqualTo(40_000));
        }

        [Test]
        public void MicroGold_is_integer_exact_and_checked()
        {
            MicroGold g = MicroGold.FromWholeGold(150);
            Assert.That(g.Micro, Is.EqualTo(150_000_000));
            Assert.That((g + MicroGold.FromMicro(4_706_250)).ToString(), Is.EqualTo("154.706250 G"));
            Assert.That(MicroGold.FromMicro(1_999_999).WholeGoldFloor, Is.EqualTo(1));
            Assert.That(MicroGold.FromMicro(-1).WholeGoldFloor, Is.EqualTo(-1));
            Assert.That(EconomyRates.MicroToG(2_700_000_000), Is.EqualTo(2700));
            Assert.That(MicroGold.FromMicro(-1).IsValidBalance, Is.False);
            Assert.That(MicroGold.FromMicro(JsCompat.MaxSafeInteger).IsValidBalance, Is.True);
            Assert.That(MicroGold.FromMicro(JsCompat.MaxSafeInteger + 1).IsValidBalance, Is.False);
            Assert.Throws<DomainInvariantException>(() => { var _ = MicroGold.FromMicro(long.MaxValue) + MicroGold.FromMicro(1); });
            Assert.Throws<DomainInvariantException>(() => MicroGold.FromWholeGold(long.MaxValue / 1000));
            Assert.That(MicroGold.FromMicro(5) > MicroGold.Zero && MicroGold.Zero == MicroGold.FromMicro(0), Is.True);
        }
    }
}
