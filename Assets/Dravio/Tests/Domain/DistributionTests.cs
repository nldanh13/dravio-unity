using System;
using System.Collections.Generic;
using System.Linq;
using Dravio.Domain.Breeding;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Economy;
using NUnit.Framework;
using static Dravio.Domain.Tests.Support.Fixtures;

namespace Dravio.Domain.Tests
{
    /// <summary>
    /// Kiểm thử phân phối xác suất trên nhiều seed (mỗi save một luồng RNG giao dịch khác nhau), đi qua đúng đường lệnh
    /// buyPack / startBreed. Dung sai 4,5σ của phân phối nhị thức — xác suất báo động giả ≈ 7·10⁻⁶ mỗi phép so, và kết quả tất định
    /// (seed cố định) nên test không chập chờn. Tỷ lệ kỳ vọng lấy từ GDD 3.1 trang 8, 15 và ADR 0003.
    /// </summary>
    public sealed class DistributionTests
    {
        private static void Near(int count, int n, double p, string what)
        {
            double sigma = Math.Sqrt(p * (1 - p) / n);
            double f = (double)count / n;
            if (p == 0) Assert.That(count, Is.Zero, what + ": tỷ lệ 0 % không bao giờ xuất hiện");
            else Assert.That(Math.Abs(f - p), Is.LessThanOrEqualTo(4.5 * sigma), $"{what}: {f:P2} vs {p:P2} (n={n})");
        }

        private static GameState Base(long gold)
        {
            GameState s = InitialStateFactory.Create("dist", T0, Rng.Create(5));
            s.Player.Gold = MicroGold.FromWholeGold(gold);
            return s;
        }

        private static IEnumerable<PendingPack> BuyMany(string packId, int n, Action<GameState> setup = null)
        {
            GameState b = Base(PackCatalog.Get(packId).PriceG);
            setup?.Invoke(b);
            var clock = new FixedClock(T0);
            for (int i = 0; i < n; i++)
            {
                GameState s = b.DeepClone();
                s.SaveId = "dist-" + packId + "-" + i;
                DispatchResult r = CommandDispatcher.Dispatch(s, new BuyPackCommand("r", packId), clock);
                Assert.That(r.Ok, Is.True, r.Error?.ToString());
                yield return r.State.PendingPack;
            }
        }

        [TestCase("starter")]
        [TestCase("elemental")]
        [TestCase("mystic")]
        [TestCase("astral")]
        public void Pack_rarity_odds_match_GDD_table_and_species_are_uniform_within_rarity(string packId)
        {
            const int N = 8000;
            PackDef pack = PackCatalog.Get(packId);
            List<PendingPack> eggs = BuyMany(packId, N).ToList();
            foreach (Rarity r in PackRolls.RarityOrder)
            {
                var ofRarity = eggs.Where(e => SpeciesCatalog.Get(e.SpeciesId).Rarity == r).ToList();
                Near(ofRarity.Count, N, PackCatalog.Odds(pack, r) / 100.0, packId + " " + r);
                var pool = SpeciesCatalog.All.Where(s => s.PackEligible && s.Rarity == r).ToList();
                foreach (DragonSpecies sp in pool)
                    if (ofRarity.Count > 0) Near(ofRarity.Count(e => e.SpeciesId == sp.Id), ofRarity.Count, 1.0 / pool.Count, packId + " " + sp.Id + " | " + r);
            }
            Assert.That(eggs.Select(e => e.SpeciesId), Has.None.EqualTo("amberhorn").And.None.EqualTo("aurorawing"), "Amberhorn/Aurorawing không có trong gói");
        }

        [Test]
        public void Pack_mutations_match_85_11_3_5_0_5_and_luck_8_raises_rare_share()
        {
            const int N = 20000;
            var eggs = BuyMany("starter", N).ToList();
            double[] d0 = Mutation.Distribution(0, 0);
            Assert.That(d0.Select(x => Math.Round(x * 1000) / 10), Is.EqualTo(new[] { 85, 11, 3.5, 0.5 }));
            for (int i = 0; i < 4; i++) Near(eggs.Count(e => e.Mutation == Mutation.Order[i]), N, d0[i], "L0 " + Mutation.Order[i]);
            var lucky = BuyMany("starter", N, s => s.Player.Upgrades.MutationLuckLevel = 8).ToList();
            double[] d8 = Mutation.Distribution(8, 0);
            for (int i = 0; i < 4; i++) Near(lucky.Count(e => e.Mutation == Mutation.Order[i]), N, d8[i], "L8 " + Mutation.Order[i]);
        }

        [Test]
        public void Pity_guarantees_a_prismatic_at_least_every_200_rolls_across_seeds()
        {
            foreach (uint seed in new uint[] { 1, 2, 3, 2026, 0xDEADBEEF })
            {
                IRng rng = Rng.Create(seed);
                int pity = 0, sinceLast = 0, maxGap = 0, forced = 0;
                for (int i = 0; i < 50_000; i++)
                {
                    bool willForce = pity + 1 >= EconomyTables.PrismaticPity;
                    MutationRoll m = Mutation.Roll(rng, 0, 0, pity);
                    sinceLast++;
                    if (m.Mutation == MutationType.Prismatic) { maxGap = Math.Max(maxGap, sinceLast); sinceLast = 0; if (willForce) forced++; }
                    pity = m.PityCount;
                }
                Assert.That(maxGap, Is.LessThanOrEqualTo(200), "seed " + seed);
                Assert.That(forced, Is.GreaterThan(0), "pity actually triggers on seed " + seed);
            }
        }

        [Test]
        public void Breeding_species_genotype_and_mutation_frequencies_match_the_preview_across_seeds()
        {
            const int N = 12000;
            GameState b = Base(1000);
            b.Dragons.Add(Adult("pa", "emberling", d => { d.Genotype = new Genotype(); d.Genotype.Set("pigment", "R", "r"); d.Genotype.Set("pattern", "P", "p"); d.Genotype.Set("eye", "B", "b"); d.Mutation = MutationType.Crystal; }));
            b.Dragons.Add(Adult("pb", "sprout", d => { d.Genotype = new Genotype(); d.Genotype.Set("pigment", "r", "r"); d.Genotype.Set("pattern", "P", "p"); d.Genotype.Set("eye", "b", "b"); d.Mutation = MutationType.Golden; }));
            BreedPreview preview = BreedingRules.PreviewBreed(b.FindDragon("pa"), b.FindDragon("pb"));
            var clock = new FixedClock(T0);
            var eggs = new List<BreedingEgg>();
            for (int i = 0; i < N; i++)
            {
                GameState s = b.DeepClone();
                s.SaveId = "breed-dist-" + i;
                DispatchResult r = CommandDispatcher.Dispatch(s, new StartBreedCommand("r", "pa", "pb"), clock);
                Assert.That(r.Ok, Is.True, r.Error?.ToString());
                eggs.Add(r.State.BreedingNest);
            }
            Assert.That(preview.Species.Select(e => e.Key), Is.EquivalentTo(new[] { "emberling", "sprout", "magma" }));
            foreach (var e in preview.Species) Near(eggs.Count(x => x.SpeciesId == e.Key), N, e.Value, "species " + e.Key);
            foreach (var locus in preview.Loci)
                foreach (var g in locus.Value)
                    Near(eggs.Count(x => x.Genotype.TryGet(locus.Key, out AllelePair p) && p.Equals(g.Key)), N, g.Value, locus.Key + " " + g.Key);
            // Hai bố mẹ có biến thể: q = min(0,60; 15/100 + 0,16) = 0,31 (GDD trang 15).
            double[] d = Mutation.Distribution(0, 2);
            Assert.That(1 - d[0], Is.EqualTo(0.31).Within(1e-12));
            for (int i = 0; i < 4; i++) Near(eggs.Count(x => x.Mutation == Mutation.Order[i]), N, d[i], "b=2 " + Mutation.Order[i]);
            Assert.That(eggs.SelectMany(x => x.Genotype).All(kv => kv.Value.First != null && "RrPpBb".Contains(kv.Value.First)), Is.True, "không allele ngoài bố mẹ");
        }

        [Test]
        public void Mendel_ratios_hold_for_every_parent_pair_combination()
        {
            const int N = 6000;
            IRng rng = Rng.Create(Rng.HashSeed("mendel"));
            foreach (LocusDef l in LociCatalog.All)
            {
                var pairs = new[] { new AllelePair(l.Dominant, l.Dominant), new AllelePair(l.Dominant, l.Recessive), new AllelePair(l.Recessive, l.Recessive) };
                foreach (AllelePair x in pairs)
                    foreach (AllelePair y in pairs)
                    {
                        var ga = new Genotype(); ga.Set(l.Id, x);
                        var gb = new Genotype(); gb.Set(l.Id, y);
                        var counts = new Dictionary<AllelePair, int>();
                        for (int i = 0; i < N; i++)
                        {
                            Genotype c = Biology.Genetics.RollOffspringGenotype(rng, ga, gb, new[] { l.Id });
                            c.TryGet(l.Id, out AllelePair p);
                            counts[p] = counts.TryGetValue(p, out int k) ? k + 1 : 1;
                        }
                        var dist = Biology.Genetics.LocusDistribution(l.Id, x, y);
                        Assert.That(dist.Sum(e => e.Value), Is.EqualTo(1.0));
                        Assert.That(counts.Keys, Is.SubsetOf(dist.Select(e => e.Key)));
                        foreach (var e in dist) Near(counts.TryGetValue(e.Key, out int c2) ? c2 : 0, N, e.Value, l.Id + " " + x + "×" + y + "→" + e.Key);
                    }
            }
        }
    }
}
