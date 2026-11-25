using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dravio.Domain.Biology;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Tests.Support;
using NUnit.Framework;

namespace Dravio.Domain.Tests
{
    /// <summary>Port của tests/unit/catalog.test.ts và phần catalog của g0Biology.test.ts.</summary>
    public sealed class CatalogTests
    {
        private static readonly string[] CanonicalIds =
        {
            "emberling", "droplet", "sprout", "zephyr", "steamdrake", "magma", "sparkwing", "frostfin",
            "mossback", "blossom", "solaris", "leviathan", "amberhorn", "aurorawing", "celestia", "gaia",
        };

        [Test]
        public void Has_exactly_the_16_v2_species_ids_in_order_all_unique()
        {
            var ids = SpeciesCatalog.All.Select(s => s.Id).ToArray();
            Assert.That(ids, Is.EqualTo(CanonicalIds));
            Assert.That(ids.Distinct().Count(), Is.EqualTo(16));
            Assert.That(SpeciesCatalog.AllBiology.Select(b => b.Id).OrderBy(x => x, System.StringComparer.Ordinal),
                Is.EqualTo(CanonicalIds.OrderBy(x => x, System.StringComparer.Ordinal)));
        }

        [Test]
        public void Excludes_amberhorn_and_aurorawing_from_packs_only()
        {
            Assert.That(SpeciesCatalog.All.Where(s => !s.PackEligible).Select(s => s.Id).OrderBy(x => x), Is.EqualTo(new[] { "amberhorn", "aurorawing" }));
        }

        [Test]
        public void Every_species_has_a_realm_and_a_valid_group()
        {
            foreach (var s in SpeciesCatalog.All)
            {
                Assert.That(s.Realms.Count, Is.GreaterThan(0), s.Id);
                Assert.That(WireNames.IsDefined(s.Group), Is.True);
                Assert.That(s.Elements.Count, Is.InRange(1, 2));
                Assert.That(s.Loci, Is.EqualTo(new[] { "pigment", "pattern", "eye" }));
                Assert.That(s.AssetId, Is.EqualTo("dragon." + s.Id));
            }
        }

        /// <summary>Bảng v2 GDD trang 6: (id, rarity, group, baseRateMicroG, growth phút, baseSellG, packEligible).</summary>
        [Test]
        public void Economy_baseline_matches_the_v2_table_for_all_16_species()
        {
            var table = SpeciesCatalog.All.Select(s => string.Join(",", s.Id, s.Rarity.ToWire(), s.Group.ToWire(), s.BaseRateMicroG, s.GrowthTimeSec / 60, s.BaseSellG, s.PackEligible ? "true" : "false")).ToArray();
            Assert.That(table, Is.EqualTo(new[]
            {
                "emberling,common,T,25000,5,75,true",
                "droplet,common,A,25000,6,75,true",
                "sprout,common,T,27000,8,80,true",
                "zephyr,common,V,28000,8,85,true",
                "steamdrake,rare,A,70000,25,350,true",
                "magma,rare,T,75000,30,380,true",
                "sparkwing,rare,V,80000,35,400,true",
                "frostfin,rare,A,75000,30,380,true",
                "mossback,rare,A,72000,25,360,true",
                "blossom,rare,V,80000,35,400,true",
                "solaris,epic,V,180000,120,1800,true",
                "leviathan,epic,A,200000,150,2000,true",
                "amberhorn,epic,T,200000,150,2000,false",
                "aurorawing,epic,V,220000,180,2200,false",
                "celestia,legendary,V,500000,360,10000,true",
                "gaia,legendary,A,550000,480,11000,true",
            }));
            Assert.That(SpeciesCatalog.Get("emberling").GrowthTimeSec, Is.EqualTo(300));
        }

        [Test]
        public void Unknown_species_is_an_invariant_violation()
        {
            Assert.That(SpeciesCatalog.TryGet("dragonite", out _), Is.False);
            Assert.Throws<DomainInvariantException>(() => SpeciesCatalog.Get("dragonite"));
            Assert.Throws<DomainInvariantException>(() => SpeciesCatalog.GetBiology("dragonite"));
        }

        [Test]
        public void Has_7_microhabitats_in_3_zones()
        {
            Assert.That(HabitatCatalog.All.Count, Is.EqualTo(7));
            Assert.That(HabitatCatalog.All.Select(h => h.ZoneId).Distinct().OrderBy(x => x), Is.EqualTo(new[] { "ocean", "origin", "sky" }));
            Assert.That(HabitatCatalog.ZoneOf("origin_pondbank"), Is.EqualTo("origin"));
            Assert.That(HabitatCatalog.ZoneOf("moon"), Is.Null);
        }

        [TestCaseSource(nameof(CanonicalIds))]
        public void Biology_is_final_with_valid_diet_resilience_palette(string id)
        {
            SpeciesBiology b = SpeciesCatalog.GetBiology(id);
            Assert.That(b.Draft, Is.False);
            Assert.That(b.DietIds.Count, Is.GreaterThan(0));
            foreach (HazardId h in new[] { HazardId.Wildfire, HazardId.StrongCurrent, HazardId.Thunderstorm }) Assert.That(b.ResiliencePct[h], Is.InRange(0, 100));
            foreach (QualityKey k in new[] { QualityKey.AirQuality, QualityKey.WaterQuality, QualityKey.Vegetation, QualityKey.FoodAvailability }) Assert.That(b.ComfortMin[k], Is.InRange(0, 100));
            foreach (string c in new[] { b.Palette.Primary, b.Palette.Secondary, b.Palette.Belly, b.Palette.Accent })
                Assert.That(c, Does.Match("^#[0-9A-Fa-f]{6}$"));
        }

        [TestCaseSource(nameof(CanonicalIds))]
        public void Group_and_realms_agree_with_the_GDD_catalog(string id)
        {
            DragonSpecies s = SpeciesCatalog.Get(id);
            SpeciesBiology b = SpeciesCatalog.GetBiology(id);
            Assert.That(b.Group, Is.EqualTo(s.Group));
            var habitatRealms = new HashSet<Realm>(b.HabitatRules.Select(h => Fixtures.Habitat(h).Realm));
            Assert.That(habitatRealms.SetEquals(s.Realms), Is.True, id);
            Assert.That(s.HabitatRules, Is.EqualTo(b.HabitatRules));
        }

        [TestCaseSource(nameof(CanonicalIds))]
        public void Exact_compatibility_and_comfort_at_least_80_in_every_allowed_habitat(string id)
        {
            SpeciesBiology b = SpeciesCatalog.GetBiology(id);
            int best = 0;
            foreach (HabitatProfile h in HabitatCatalog.All)
            {
                HabitatResult r = Suitability.Evaluate(id, h.Id, h);
                Assert.That(r.Allowed, Is.EqualTo(b.HabitatRules.Contains(h.Id)), id + "@" + h.Id);
                if (r.Allowed) { Assert.That(r.Score, Is.GreaterThanOrEqualTo(80)); best = System.Math.Max(best, r.Score); }
            }
            Assert.That(best, Is.GreaterThanOrEqualTo(80));
        }

        /// <summary>Dữ liệu C# sinh tự động phải trùng snapshot JSON đã có trong repo (Assets/Dravio/Data/species-biology.json).</summary>
        [Test]
        public void Generated_biology_equals_the_species_biology_json_snapshot()
        {
            string path = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(GoldenFile.Locate()))), "..", "Data", "species-biology.json");
            path = Path.GetFullPath(path);
            var json = (Dictionary<string, object>)MiniJson.Parse(File.ReadAllText(path));
            Assert.That(json.Keys, Is.EqualTo(CanonicalIds));
            foreach (var kv in json)
            {
                var j = (Dictionary<string, object>)kv.Value;
                SpeciesBiology b = SpeciesCatalog.GetBiology(kv.Key);
                var mine = new Dictionary<string, object>
                {
                    ["group"] = b.Group.ToWire(), ["respiration"] = b.Respiration.ToWire(), ["locomotion"] = W(b.Locomotion), ["protection"] = b.Protection.ToWire(),
                    ["activity"] = b.Activity.ToWire(), ["dietIds"] = W(b.DietIds), ["thermalBands"] = W(b.ThermalBands), ["habitatRules"] = b.HabitatRules.Cast<object>().ToList(),
                    ["salinityBands"] = W(b.SalinityBands), ["depthBands"] = W(b.DepthBands), ["altitudeBands"] = W(b.AltitudeBands),
                    ["comfortMin"] = new Dictionary<string, object> { ["airQuality"] = b.ComfortMin.AirQuality, ["waterQuality"] = b.ComfortMin.WaterQuality, ["vegetation"] = b.ComfortMin.Vegetation, ["foodAvailability"] = b.ComfortMin.FoodAvailability },
                    ["resiliencePct"] = new Dictionary<string, object> { ["wildfire"] = (double)b.ResiliencePct.Wildfire, ["strong_current"] = (double)b.ResiliencePct.StrongCurrent, ["thunderstorm"] = (double)b.ResiliencePct.Thunderstorm },
                    ["palette"] = new Dictionary<string, object> { ["primary"] = b.Palette.Primary, ["secondary"] = b.Palette.Secondary, ["belly"] = b.Palette.Belly, ["accent"] = b.Palette.Accent },
                    ["silhouetteVi"] = b.SilhouetteVi, ["draft"] = b.Draft,
                };
                Assert.That(MiniJson.Diff(j, mine), Is.Null, kv.Key);
            }
        }

        private static List<object> W<T>(IEnumerable<T> xs) where T : struct, System.Enum => xs.Select(x => (object)x.ToWire()).ToList();

        [Test]
        public void Error_codes_keep_the_TypeScript_wire_names_and_i18n_keys()
        {
            Assert.That(new DomainError(DomainErrorCode.PetCooldown).MessageKey, Is.EqualTo("commandError.pet-cooldown"));
            var all = System.Enum.GetValues(typeof(DomainErrorCode)).Cast<DomainErrorCode>().Select(c => c.ToWire()).ToArray();
            // 30 mã đầu giữ nguyên thứ tự và chuỗi của CommandErrorCode (TS b33ec7a); 18 mã sau của U2.5, 10 mã của U2.7 và 9 mã của U2.8 là implementation mới.
            Assert.That(all.Length, Is.EqualTo(82));
            // U2.6: 15 mã mới ở cuối (sức khỏe, chăm sóc, thiên tai).
            Assert.That(all.Skip(67), Is.EqualTo(new[] { "vitals-below-threshold", "breed-vitals-below-threshold", "unknown-case", "case-already-assessed", "case-not-assessed",
                "case-queued", "care-already-started", "care-not-started", "care-not-complete", "case-already-reviewed", "case-not-reviewed", "unknown-care-plan",
                "care-plan-wrong-site", "care-plan-unsuitable", "no-disaster-warning" }));
            Assert.That(all.Skip(58).Take(9), Is.EqualTo(new[] { "no-route", "unknown-route", "route-locked", "expedition-busy", "dragon-unsuited", "no-expedition", "expedition-not-ready",
                "route-rewards-undefined", "unknown-zone" }));
            Assert.That(all.Take(30).Last(), Is.EqualTo("not-enough-gold"));
            Assert.That(all, Does.Contain("name-invalid-chars").And.Contain("not-enough-gold").And.Contain("invalid-command"));
            Assert.That(all.Skip(48).Take(10), Is.EqualTo(new[] { "dragon-locked", "dragon-busy", "last-dragon", "nothing-to-sell", "quote-changed", "unknown-upgrade", "upgrade-maxed",
                "unknown-decoration", "decoration-owned", "decoration-not-owned" }));
            Assert.That(all.Skip(30).Take(18), Is.EqualTo(new[] { "unknown-pack", "storage-full", "nest-busy", "no-egg", "egg-not-ready", "egg-ready", "tap-too-fast", "tap-limit",
                "breed-same-dragon", "breed-invalid-genotype", "breed-released", "breed-emergency-shelter", "breed-not-adult", "breed-busy", "breed-cooldown",
                "breed-different-group", "breed-related", "breed-no-candidates" }));
        }
    }
}
