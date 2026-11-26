using System.Linq;
using Dravio.Domain.Biology;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Tests.Support;
using NUnit.Framework;

namespace Dravio.Domain.Tests
{
    /// <summary>Port phần hard gates / thresholds của tests/unit/g0Biology.test.ts, cùng vòng đời, gắn bó và genotype.</summary>
    public sealed class BiologyTests
    {
        private static readonly HabitatProfile Meadow = Fixtures.Habitat("origin_meadow");

        private static HabitatResult AtAir(double air) =>
            Suitability.Evaluate("emberling", "origin_meadow", Fixtures.Env(Meadow, quality: Meadow.Quality.With(QualityKey.AirQuality, air)));

        [Test]
        public void Frostfin_cannot_fly_and_leviathan_cannot_live_on_land()
        {
            Assert.That(SpeciesCatalog.GetBiology("frostfin").Locomotion.Contains(Locomotion.Fly), Is.False);
            Assert.That(Suitability.Evaluate("leviathan", "origin_meadow", Meadow).Allowed, Is.False);
        }

        [Test]
        public void Flyers_need_a_perch_in_the_sky()
        {
            HabitatProfile sky = Fixtures.Habitat("sky_low_island");
            Assert.That(Suitability.Evaluate("zephyr", "sky_low_island", sky).Allowed, Is.True);
            Assert.That(Suitability.Evaluate("zephyr", "sky_low_island", Fixtures.Env(sky, perch: false)).Allowed, Is.False);
        }

        [Test]
        public void Land_only_dragons_are_never_placed_in_the_sky_or_ocean()
        {
            foreach (string h in new[] { "sky_low_island", "ocean_reef", "ocean_estuary", "ocean_cool_pool" })
                Assert.That(Suitability.Evaluate("emberling", h, Fixtures.Habitat(h)).Allowed, Is.False, h);
        }

        [Test]
        public void Deep_water_wrong_salinity_wrong_heat_and_wrong_zone_are_rejected()
        {
            HabitatProfile reef = Fixtures.Habitat("ocean_reef");
            Assert.That(Suitability.Evaluate("leviathan", "ocean_reef", Fixtures.Env(reef, depth: DepthBand.Deep)).Allowed, Is.False);
            Assert.That(Suitability.Evaluate("leviathan", "ocean_reef", Fixtures.Env(reef, salinity: SalinityBand.Fresh)).Allowed, Is.False);
            HabitatProfile rocks = Fixtures.Habitat("origin_warm_rocks");
            Assert.That(Suitability.Evaluate("magma", "origin_warm_rocks", Fixtures.Env(rocks, temperature: TemperatureBand.Cold)).Allowed, Is.False);
            Assert.That(Suitability.Evaluate("emberling", "origin_meadow", Fixtures.Env(Meadow, zoneId: "sky")).Allowed, Is.False);
        }

        [Test]
        public void Incompatible_placements_always_score_0()
        {
            HabitatResult r = Suitability.Evaluate("frostfin", "origin_meadow", Meadow);
            Assert.That((r.Allowed, r.Score, r.Reason), Is.EqualTo((false, 0, SuitabilityReason.Incompatible)));
        }

        [Test]
        public void Score_boundaries_100_80_79_50_49_0()
        {
            Assert.That(AtAir(90).Score, Is.EqualTo(100));
            Assert.That((AtAir(45).Allowed, AtAir(45).Score, AtAir(45).Reason), Is.EqualTo((true, 80, SuitabilityReason.Ok)));
            Assert.That((AtAir(44).Allowed, AtAir(44).Score), Is.EqualTo((true, 79)));
            Assert.That((AtAir(15).Allowed, AtAir(15).Score), Is.EqualTo((true, 50)));
            Assert.That((AtAir(14).Allowed, AtAir(14).Score, AtAir(14).Reason), Is.EqualTo((false, 49, SuitabilityReason.LowSuitability)));
            HabitatResult worst = Suitability.Evaluate("emberling", "origin_meadow", Fixtures.Env(Meadow, quality: new QualityValues(0, Meadow.Quality.WaterQuality, 0, 0)));
            Assert.That((worst.Allowed, worst.Score), Is.EqualTo((false, 0)));
            Assert.That(Suitability.TierOf(AtAir(45)), Is.EqualTo(SuitabilityTier.Good));
            Assert.That(Suitability.TierOf(AtAir(44)), Is.EqualTo(SuitabilityTier.NeedsImprovement));
            Assert.That(Suitability.TierOf(AtAir(14)), Is.EqualTo(SuitabilityTier.Unsuitable));
        }

        [Test]
        public void Ignores_factors_irrelevant_to_the_habitat()
        {
            Assert.That(Suitability.Evaluate("emberling", "origin_meadow", Fixtures.Env(Meadow, quality: Meadow.Quality.With(QualityKey.WaterQuality, 0))).Score, Is.EqualTo(100));
        }

        [Test]
        public void Rejects_NaN_out_of_range_quality_and_unknown_habitat_ids()
        {
            Assert.That(AtAir(double.NaN).Reason, Is.EqualTo(SuitabilityReason.InvalidEnv));
            Assert.That(AtAir(101).Reason, Is.EqualTo(SuitabilityReason.InvalidEnv));
            Assert.That(AtAir(-1).Reason, Is.EqualTo(SuitabilityReason.InvalidEnv));
            Assert.That(Suitability.Evaluate("emberling", "unknown", Meadow).Reason, Is.EqualTo(SuitabilityReason.InvalidEnv));
        }

        [Test]
        public void Rejects_unknown_species_ids()
        {
            Assert.Throws<DomainInvariantException>(() => Suitability.Evaluate("nope", "origin_meadow", Meadow));
        }

        [Test]
        public void Growth_segments_split_at_35_and_100_percent()
        {
            var segs = Lifecycle.GrowthSegments(0, 400_000, 300);
            Assert.That(segs.Count, Is.EqualTo(3));
            Assert.That((segs[0].Stage, segs[0].DurationMs), Is.EqualTo((GrowthStage.Baby, 105_000L)));
            Assert.That((segs[1].Stage, segs[1].DurationMs), Is.EqualTo((GrowthStage.Juvenile, 195_000L)));
            Assert.That((segs[2].Stage, segs[2].DurationMs), Is.EqualTo((GrowthStage.Adult, 100_000L)));
        }

        [Test]
        public void AddXp_caps_at_maturity_and_reports_both_changes_when_skipping_juvenile()
        {
            DragonState baby = Fixtures.Adult("x", "emberling", d => d.XpSec = 100);
            var r = Lifecycle.AddXp(baby, 1_000);
            Assert.That(r.xpMs, Is.EqualTo(300_000));
            Assert.That(r.stageChanges.Count, Is.EqualTo(2));
            Assert.That((r.stageChanges[0].From, r.stageChanges[0].To, r.stageChanges[1].From, r.stageChanges[1].To),
                Is.EqualTo((GrowthStage.Baby, GrowthStage.Juvenile, GrowthStage.Juvenile, GrowthStage.Adult)));
        }

        [Test]
        public void AdvanceDragon_offline_pays_three_quarters_with_exact_remainder()
        {
            DragonState d = Fixtures.Adult("x", "emberling");
            // Adult Normal 25 000 microG/s × 1 ms × 3 = 75 000 / 4000 = 18 microG, dư 3000.
            GrowthResult r = Lifecycle.AdvanceDragon(d, 1, producing: true, AdvanceMode.Offline, remainder: 0);
            Assert.That((r.GoldMicro, r.Remainder), Is.EqualTo((18L, 3000L)));
            GrowthResult r2 = Lifecycle.AdvanceDragon(d, 1, producing: true, AdvanceMode.Offline, remainder: r.Remainder);
            Assert.That((r2.GoldMicro, r2.Remainder), Is.EqualTo((19L, 2000L)));
            Assert.That(Lifecycle.AdvanceDragon(d, 60_000, producing: false, AdvanceMode.Online, 7).GoldMicro, Is.EqualTo(0));
        }

        [Test]
        public void Bond_gain_respects_daily_cap_absolute_cap_and_new_UTC_day()
        {
            DragonState d = Fixtures.Adult("x", "emberling");
            for (int i = 0; i < 12; i++) BondRules.GainBond(d, Fixtures.T0 + i);
            Assert.That((d.Bond, d.BondDaily, d.BondDay), Is.EqualTo((10, (int?)10, "2026-11-24")));
            Assert.That(BondRules.GainBond(d, Fixtures.T0 + 86_400_000), Is.EqualTo(1));
            Assert.That((d.Bond, d.BondDaily, d.BondDay), Is.EqualTo((11, (int?)1, "2026-11-25")));
            d.Bond = 100; d.BondDaily = 0;
            Assert.That(BondRules.GainBond(d, Fixtures.T0 + 86_400_000), Is.EqualTo(0));
            Assert.That(d.Bond, Is.EqualTo(100));
        }

        [Test]
        public void Genotype_validation_requires_normalized_known_alleles_and_exact_loci()
        {
            var ok = Fixtures.Adult("x", "emberling").Genotype;
            Assert.That(Genetics.IsValidGenotype(ok, LociCatalog.LocusIds), Is.True);
            var unnormalized = ok.Clone(); unnormalized.Set("pigment", "r", "R");
            Assert.That(Genetics.IsValidGenotype(unnormalized, LociCatalog.LocusIds), Is.False);
            var badAllele = ok.Clone(); badAllele.Set("pigment", "Q", "r");
            Assert.That(Genetics.IsValidGenotype(badAllele, LociCatalog.LocusIds), Is.False);
            var extra = ok.Clone(); extra.Set("wings", "W", "w");
            Assert.That(Genetics.IsValidGenotype(extra, LociCatalog.LocusIds), Is.False);
            var missing = new Genotype(); missing.Set("pigment", "R", "r");
            Assert.That(Genetics.IsValidGenotype(missing, LociCatalog.LocusIds), Is.False);
            Assert.That(Genetics.IsValidGenotype(null, LociCatalog.LocusIds), Is.False);
        }

        [Test]
        public void Founder_genotype_is_stable_per_uid_and_always_valid()
        {
            Genotype a = Genetics.FounderGenotype("d_abc", ContentInfo.ContentVersion, LociCatalog.LocusIds);
            Genotype b = Genetics.FounderGenotype("d_abc", ContentInfo.ContentVersion, LociCatalog.LocusIds);
            Assert.That(a, Is.EqualTo(b).AsCollection);
            for (int i = 0; i < 200; i++)
                Assert.That(Genetics.IsValidGenotype(Genetics.FounderGenotype("d_" + i, ContentInfo.ContentVersion, LociCatalog.LocusIds), LociCatalog.LocusIds), Is.True);
        }
    }
}
