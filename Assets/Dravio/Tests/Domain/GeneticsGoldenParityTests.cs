using System.Collections.Generic;
using System.Linq;
using Dravio.Domain.Biology;
using Dravio.Domain.Breeding;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Economy;
using Dravio.Domain.Tests.Support;
using NUnit.Framework;

namespace Dravio.Domain.Tests
{
    /// <summary>
    /// Đối chiếu U2.5 với golden sinh từ hàm thuần của TS @ b33ec7a (Golden/ts-genetics-golden-b33ec7a.json, Tools~/genetics-golden.test.ts):
    /// phân phối Mendel, kiểu hình, ứng viên/phân phối loài con và phí cho 256 cặp loài, thời gian ấp, phân phối biến thể,
    /// chuỗi roll biến thể/pity, roll gói, roll lai và checkBreed. Đây là phần CÓ parity với TS; lệnh U2.5 là implementation mới.
    /// </summary>
    public sealed class GeneticsGoldenParityTests
    {
        private static Dictionary<string, object> O(object v) => (Dictionary<string, object>)v;
        private static List<object> A(object v) => (List<object>)v;
        private static Dictionary<string, object> G => GoldenFile.Genetics;
        private static AllelePair P(object v) { var l = A(v); return new AllelePair((string)l[0], (string)l[1]); }
        private static object PJ(AllelePair p) => new List<object> { p.First, p.Second };
        private static object DistJ(List<KeyValuePair<AllelePair, double>> d) => d.Select(e => (object)new Dictionary<string, object> { ["key"] = e.Key.ToString(), ["pair"] = PJ(e.Key), ["p"] = e.Value }).ToList();
        private static object GenoJ(Genotype g) { var o = new Dictionary<string, object>(); foreach (var kv in g) o[kv.Key] = PJ(kv.Value); return o; }

        [Test]
        public void Genetics_golden_targets_the_reference_commit()
        {
            Assert.That(G["source"], Is.EqualTo("nldanh13/dravio@" + ContentInfo.ReferenceCommit));
            Assert.That(SpeciesCatalog.All.Select(s => (object)new Dictionary<string, object> { ["id"] = s.Id, ["packEligible"] = s.PackEligible }).ToList(), Is.EqualTo(A(G["species"])).Using<object, object>((x, y) => MiniJson.Diff(y, x) == null));
        }

        [Test]
        public void Locus_distribution_and_phenotype_match()
        {
            foreach (var c in A(G["locus"]).Select(O))
                Assert.That(MiniJson.Diff(c["dist"], DistJ(Genetics.LocusDistribution((string)c["locus"], P(c["a"]), P(c["b"])))), Is.Null, c["locus"] + " " + MiniJson.Show(c["a"]) + "×" + MiniJson.Show(c["b"]));
            foreach (var c in A(G["phenotype"]).Select(O))
            {
                var expected = O(c["phenotype"]);
                var got = Genetics.DerivePhenotype(StateJson.GenotypeFromJson(c["genotype"]), expected.Keys.ToList());
                Assert.That(MiniJson.Diff(expected, got.ToDictionary(k => k.Key, k => (object)k.Value)), Is.Null);
            }
        }

        [Test]
        public void Candidates_species_distribution_and_fee_match_for_all_256_pairs()
        {
            var rows = A(G["speciesPairs"]).Select(O).ToList();
            Assert.That(rows.Count, Is.EqualTo(256));
            foreach (var r in rows)
            {
                DragonSpecies a = SpeciesCatalog.Get((string)r["a"]), b = SpeciesCatalog.Get((string)r["b"]);
                string at = a.Id + "×" + b.Id;
                Assert.That(BreedingRules.CandidateSpecies(a, b).Select(s => (object)s.Id).ToList(), Is.EqualTo(A(r["candidates"])), at);
                var dist = BreedingRules.SpeciesDistribution(a, b).Select(e => (object)new List<object> { e.Key, e.Value }).ToList();
                Assert.That(MiniJson.Diff(r["distribution"], dist), Is.Null, at + " distribution (bit-exact)");
                Assert.That(BreedingRules.BreedFeeG(a, b), Is.EqualTo((long)(double)r["feeG"]), at);
            }
        }

        [Test]
        public void Hatch_seconds_and_mutation_distribution_match()
        {
            foreach (var r in A(G["hatch"]).Select(O))
            {
                WireNames.TryParse((string)r["rarity"], out Rarity rar);
                Assert.That(BreedingRules.HatchSeconds(rar, (int)(double)r["level"]), Is.EqualTo((long)(double)r["sec"]));
            }
            foreach (var r in A(G["mutationDist"]).Select(O))
            {
                double[] d = Mutation.Distribution((int)(double)r["L"], (int)(double)r["b"]);
                var e = O(r["d"]);
                Assert.That(d, Is.EqualTo(new[] { (double)e["normal"], (double)e["crystal"], (double)e["golden"], (double)e["prismatic"] }), "L=" + r["L"] + " b=" + r["b"]);
            }
        }

        [Test]
        public void Mutation_roll_sequences_and_pity_match()
        {
            foreach (var r in A(G["mutationRolls"]).Select(O))
            {
                IRng rng = Rng.Create((uint)(double)r["seed"]);
                foreach (var row in A(r["rolls"]).Select(A))
                {
                    MutationRoll m = Mutation.Roll(rng, (int)(double)row[0], (int)(double)row[1], (int)(double)row[2]);
                    Assert.That((m.Mutation.ToWire(), m.PityCount), Is.EqualTo(((string)row[3], (int)(double)row[4])));
                }
                Assert.That(rng.Next(), Is.EqualTo((double)r["after"]), "RNG consumption matches");
            }
        }

        [Test]
        public void Pack_species_rolls_match()
        {
            foreach (var r in A(G["packRolls"]).Select(O))
            {
                IRng rng = Rng.Create((uint)(double)r["seed"]);
                PackDef pack = PackCatalog.Get((string)r["packId"]);
                Assert.That(A(r["rolls"]).Select(_ => (object)PackRolls.RollSpecies(rng, pack)).ToList(), Is.EqualTo(A(r["rolls"])), pack.Id);
                Assert.That(rng.Next(), Is.EqualTo((double)r["after"]));
            }
        }

        [Test]
        public void Breed_preview_and_roll_sequences_match()
        {
            int i = 0;
            foreach (var r in A(G["breeds"]).Select(O))
            {
                DragonState a = StateJson.DragonFromJson(O(r["a"])), b = StateJson.DragonFromJson(O(r["b"]));
                BreedPreview p = BreedingRules.PreviewBreed(a, b);
                var pj = new Dictionary<string, object>
                {
                    ["species"] = p.Species.Select(e => (object)new List<object> { e.Key, e.Value }).ToList(),
                    ["loci"] = p.Loci.Select(e => (object)new List<object> { e.Key, DistJ(e.Value) }).ToList(),
                    ["feeG"] = (double)p.FeeG,
                };
                Assert.That(MiniJson.Diff(r["preview"], pj), Is.Null, a.SpeciesId + "×" + b.SpeciesId);
                IRng rng = Rng.Create(Rng.HashSeed("breed-golden", (i++).ToString(System.Globalization.CultureInfo.InvariantCulture)));
                foreach (var e in A(r["rolls"]).Select(O))
                {
                    BreedOutcome o = BreedingRules.RollBreed(rng, a, b);
                    Assert.That(MiniJson.Diff(e, new Dictionary<string, object> { ["speciesId"] = o.SpeciesId, ["genotype"] = GenoJ(o.Genotype) }), Is.Null);
                }
                Assert.That(rng.Next(), Is.EqualTo((double)r["after"]));
            }
        }

        [Test]
        public void Offspring_genotype_rolls_skip_missing_loci_like_TS()
        {
            IRng rng = Rng.Create(77);
            var full = new Genotype(); full.Set("pigment", "R", "r"); full.Set("pattern", "P", "p"); full.Set("eye", "B", "b");
            var other = new Genotype(); other.Set("pigment", "r", "r"); other.Set("pattern", "P", "p"); other.Set("eye", "b", "b");
            var partial = new Genotype(); partial.Set("pigment", "R", "r"); partial.Set("eye", "b", "b");
            var loci = new[] { "pigment", "pattern", "eye" };
            int i = 0;
            foreach (var e in A(G["offspring"]))
            {
                Genotype g = Genetics.RollOffspringGenotype(rng, full, i++ % 2 == 1 ? other : partial, loci);
                Assert.That(MiniJson.Diff(e, GenoJ(g)), Is.Null);
            }
        }

        [Test]
        public void CheckBreed_and_relatedness_match_every_fixture()
        {
            foreach (var c in A(G["checks"]).Select(O))
            {
                DragonState a = StateJson.DragonFromJson(O(c["a"])), b = StateJson.DragonFromJson(O(c["b"]));
                BreedRejection? r = BreedingRules.CheckBreed(a, b, (long)(double)c["now"]);
                var check = O(c["check"]);
                Assert.That(r == null, Is.EqualTo((bool)check["ok"]), (string)c["name"]);
                if (r != null) Assert.That(r.Value.ToWire(), Is.EqualTo(check["reason"]), (string)c["name"]);
                Assert.That(BreedingRules.AreRelated(a, b), Is.EqualTo((bool)c["related"]), (string)c["name"] + " related");
                Assert.That(BreedingRules.QueryBreedPreview(a, b, (long)(double)c["now"], out _) != null, Is.EqualTo((bool)c["query"]), (string)c["name"] + " query");
            }
        }
    }
}
