using System.Collections.Generic;
using System.Linq;
using Dravio.Domain.Biology;
using Dravio.Domain.Breeding;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Economy;
using NUnit.Framework;
using static Dravio.Domain.Tests.Support.Fixtures;

namespace Dravio.Domain.Tests
{
    /// <summary>Port của tests/unit/genetics.test.ts, g0Decisions.test.ts (ADR 0003, S03) và phần mutation/packs của economy.test.ts.</summary>
    public sealed class GeneticsRulesTests
    {
        private static Genotype G(string p, string q, string e) { var g = new Genotype(); g.Set("pigment", p.Substring(0, 1), p.Substring(1)); g.Set("pattern", q.Substring(0, 1), q.Substring(1)); g.Set("eye", e.Substring(0, 1), e.Substring(1)); return g; }
        private static double P(List<KeyValuePair<AllelePair, double>> d, string key) => d.Where(e => e.Key.ToString() == key).Select(e => e.Value).DefaultIfEmpty(0).Single();
        private static DragonSpecies S(string id) => SpeciesCatalog.Get(id);

        [Test]
        public void Mendel_Rr_x_rr_is_half_half_and_Pp_x_Pp_is_1_2_1()
        {
            var d = Genetics.LocusDistribution("pigment", new AllelePair("R", "r"), new AllelePair("r", "r"));
            Assert.That((P(d, "Rr"), P(d, "rr")), Is.EqualTo((0.5, 0.5)));
            var p = Genetics.LocusDistribution("pattern", new AllelePair("P", "p"), new AllelePair("P", "p"));
            Assert.That((P(p, "PP"), P(p, "Pp"), P(p, "pp")), Is.EqualTo((0.25, 0.5, 0.25)));
        }

        [Test]
        public void Phenotype_is_derived_and_missing_loci_are_not_expressed()
        {
            var ph = Genetics.DerivePhenotype(G("rr", "Pp", "Bb"), new[] { "pigment", "pattern", "eye", "horn" }).ToDictionary(k => k.Key, k => k.Value);
            Assert.That(ph, Is.EqualTo(new Dictionary<string, string> { ["pigment"] = "phenotype.pigment.cool", ["pattern"] = "phenotype.pattern.medium", ["eye"] = "phenotype.eye.amber", ["horn"] = "phenotype.notExpressed" }));
            Assert.That(Genetics.FounderGenotype("u1", "c1", LociCatalog.LocusIds).ToList(), Is.EqualTo(Genetics.FounderGenotype("u1", "c1", LociCatalog.LocusIds).ToList()));
        }

        [Test]
        public void Candidate_species_follow_group_elements_and_rarity_gates()
        {
            Assert.That(BreedingRules.CandidateSpecies(S("emberling"), S("droplet")), Is.Empty);
            Assert.That(BreedingRules.CandidateSpecies(S("emberling"), S("sprout")).Select(s => s.Id).OrderBy(x => x), Is.EqualTo(new[] { "emberling", "magma", "sprout" }));
            Assert.That(BreedingRules.CandidateSpecies(S("magma"), S("sprout")).Select(s => s.Id), Does.Contain("amberhorn"));
            Assert.That(BreedingRules.CandidateSpecies(S("solaris"), S("sparkwing")).Select(s => s.Id), Does.Not.Contain("celestia"));
            Assert.That(BreedingRules.CandidateSpecies(S("solaris"), S("aurorawing")).Select(s => s.Id), Does.Contain("celestia"));
            foreach (var (a, b) in new[] { ("emberling", "sprout"), ("magma", "sprout"), ("solaris", "aurorawing"), ("droplet", "mossback"), ("leviathan", "gaia") })
                Assert.That(BreedingRules.SpeciesDistribution(S(a), S(b)).Sum(e => e.Value), Is.EqualTo(1).Within(1e-12));
        }

        [Test]
        public void ADR0003_fee_offspring_weights_relatedness_and_mutation_baseline()
        {
            Assert.That(BreedingRules.BreedFeeG(S("emberling"), S("magma")), Is.EqualTo(350));
            Assert.That(BreedingRules.BreedFeeG(S("emberling"), S("sprout")), Is.EqualTo(80));
            Assert.That(BreedingRules.BreedFeeG(S("solaris"), S("celestia")), Is.EqualTo(5000));
            var p = BreedingRules.PreviewBreed(Adult("a", "emberling"), Adult("b", "sprout")).Species;
            Assert.That(p.Select(e => e.Key).OrderBy(x => x), Is.EqualTo(new[] { "emberling", "magma", "sprout" }));
            foreach (var e in p) Assert.That(e.Value, Is.EqualTo(1.0 / 3).Within(1e-12));
            Assert.That(BreedingRules.CheckBreed(Adult("a", "emberling", d => d.ParentIds = new[] { "p1", "p2" }), Adult("b", "sprout", d => d.ParentIds = new[] { "p2", "p3" }), 0), Is.EqualTo(BreedRejection.Related));
            Assert.That(BreedingRules.CheckBreed(Adult("a", "emberling", d => d.GrandparentIds = new List<string> { "b" }), Adult("b", "sprout"), 0), Is.EqualTo(BreedRejection.Related));
            Assert.That(BreedingRules.CheckBreed(Adult("a", "emberling"), Adult("b", "sprout"), 0), Is.Null, "ancestry unknown không bị suy diễn là họ hàng");
            double[] d0 = Mutation.Distribution(0, 0);
            Assert.That(d0.Select(x => System.Math.Round(x * 1000) / 10), Is.EqualTo(new[] { 85, 11, 3.5, 0.5 }));
            MutationRoll forced = Mutation.Roll(Rng.Create(1), 0, 0, 199);
            Assert.That((forced.Mutation, forced.PityCount), Is.EqualTo((MutationType.Prismatic, 0)));
        }

        [Test]
        public void Breeding_eligibility_rejects_released_and_shelter_but_allows_locked_storage_and_rejects_bad_alleles()
        {
            DragonState b = Adult("b", "sprout");
            Assert.That(BreedingRules.CheckBreed(Adult("a", "emberling", d => d.Location = DragonLocation.Released), b, 0), Is.EqualTo(BreedRejection.Released));
            Assert.That(BreedingRules.CheckBreed(Adult("a", "emberling", d => d.Location = DragonLocation.Shelter("origin")), b, 0), Is.EqualTo(BreedRejection.EmergencyShelter));
            Assert.That(BreedingRules.CheckBreed(Adult("a", "emberling", d => d.Locked = true), b, 0), Is.Null);
            Assert.That(BreedingRules.QueryBreedPreview(Adult("a", "emberling", d => d.Genotype.Set("pigment", "X", "r")), b, 0, out BreedRejection? r), Is.Null);
            Assert.That(r, Is.EqualTo(BreedRejection.InvalidGenotype));
            Assert.That(Genetics.IsValidGenotype(G("rR", "Pp", "Bb"), LociCatalog.LocusIds), Is.False);
        }

        [Test]
        public void S03_preview_has_8_equally_likely_genotypes_fee_80_and_consumes_no_RNG()
        {
            DragonState a = Adult("ember-A", "emberling", d => { d.Genotype = G("Rr", "Pp", "Bb"); d.ParentIds = new[] { "pa1", "pa2" }; d.GrandparentIds = new List<string> { "ga1", "ga2" }; });
            DragonState b = Adult("sprout-B", "sprout", d => { d.Genotype = G("rr", "pp", "bb"); d.ParentIds = new[] { "pb1", "pb2" }; d.GrandparentIds = new List<string> { "gb1", "gb2" }; });
            BreedPreview p = BreedingRules.QueryBreedPreview(a, b, 0, out _);
            Assert.That(p.FeeG, Is.EqualTo(80));
            var loci = p.Loci.ToDictionary(e => e.Key, e => e.Value);
            Assert.That(loci.Values.All(l => l.Count == 2 && l.All(e => e.Value == 0.5)), Is.True);
            var combos = from x in loci["pigment"] from y in loci["pattern"] from z in loci["eye"] select x.Value * y.Value * z.Value;
            Assert.That(combos.ToList(), Has.Count.EqualTo(8).And.All.EqualTo(1.0 / 8));
            IRng r1 = Rng.Create(303), r2 = Rng.Create(303);
            BreedingRules.PreviewBreed(a, b);
            Assert.That(r1.Next(), Is.EqualTo(r2.Next()));
            Assert.That(BreedingRules.CheckBreed(a, Adult("y", "sprout", d => d.ParentIds = new[] { "pa1", "zz" }), 0), Is.EqualTo(BreedRejection.Related));
            Assert.That(BreedingRules.CheckBreed(a, Adult("y", "sprout", d => d.GrandparentIds = new List<string> { "ga2" }), 0), Is.EqualTo(BreedRejection.Related));
            Assert.That(BreedingRules.CheckBreed(a, Adult("pa1", "sprout"), 0), Is.EqualTo(BreedRejection.Related));
            Assert.That(BreedingRules.CheckBreed(a, Adult("y", "sprout", d => d.Location = DragonLocation.Care("c1")), 0), Is.EqualTo(BreedRejection.Busy));
        }

        [Test]
        public void Hatch_time_scales_with_hatch_speed_and_pack_odds_sum_to_100()
        {
            Assert.That(BreedingRules.HatchSeconds(Rarity.Common, 0), Is.EqualTo(300));
            Assert.That(BreedingRules.HatchSeconds(Rarity.Legendary, 6), Is.EqualTo((long)System.Math.Ceiling(10_800 * 0.4)));
            foreach (PackDef pack in PackCatalog.All) Assert.That(PackRolls.RarityOrder.Sum(r => PackCatalog.Odds(pack, r)), Is.EqualTo(100), pack.Id);
            IRng rng = Rng.Create(9);
            for (int i = 0; i < 2000; i++) Assert.That(SpeciesCatalog.Get(PackRolls.RollSpecies(rng, PackCatalog.Get("astral"))).PackEligible, Is.True);
        }
    }
}
