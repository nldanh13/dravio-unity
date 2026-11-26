using System.Collections.Generic;
using NUnit.Framework;

namespace Dravio.Character.Tests
{
    /// <summary>Pure-logic tests of the living-dragon runtime (no scene, no assets).</summary>
    public sealed class LivingDragonRuntimeTests
    {
        [Test]
        public void AnimationMap_NeverFakesMissingAbility()
        {
            var walker = new DragonAnimationMap(new[] { "Idle", "Walk", "Eat", "Sleep", "Happy" });
            bool sub;
            Assert.AreEqual(DragonLocomotion.Walk, walker.Resolve(DragonLocomotion.Walk, out sub)); Assert.IsFalse(sub);
            Assert.AreEqual(DragonLocomotion.Walk, walker.Resolve(DragonLocomotion.Run, out sub)); Assert.IsTrue(sub);
            Assert.AreEqual(DragonLocomotion.Idle, walker.Resolve(DragonLocomotion.Fly, out sub)); Assert.IsTrue(sub);
            Assert.AreEqual(DragonLocomotion.Idle, walker.Resolve(DragonLocomotion.Swim, out sub)); Assert.IsTrue(sub);
            Assert.IsTrue(walker.CanPlay(DragonInteraction.Eat));
            Assert.IsFalse(new DragonAnimationMap(new[] { "Idle" }).CanPlay(DragonInteraction.Happy));
            Assert.IsFalse(new DragonAnimationMap(null).IsAnimated);
        }

        [Test]
        public void PlaybackRate_ClampedAndOnlyForMovingStates()
        {
            Assert.AreEqual(1f, DragonAnimationMap.PlaybackRate(DragonLocomotion.Walk, 2f, 0f));
            Assert.AreEqual(2f, DragonAnimationMap.PlaybackRate(DragonLocomotion.Run, 10f, 1f));
            Assert.AreEqual(0.5f, DragonAnimationMap.PlaybackRate(DragonLocomotion.Walk, 0.1f, 1f));
            Assert.AreEqual(1f, DragonAnimationMap.PlaybackRate(DragonLocomotion.Idle, 3f, 1f));
        }

        [Test]
        public void Catalog_ExactThenUnstagedThenNearest()
        {
            var list = new List<DragonVisualCatalog.Entry>
            {
                new DragonVisualCatalog.Entry { speciesId = "emberling", stage = DragonVisualStage.Baby },
                new DragonVisualCatalog.Entry { speciesId = "zephyr", stage = DragonVisualStage.Unstaged },
                new DragonVisualCatalog.Entry { speciesId = "zephyr", stage = DragonVisualStage.Adult },
            };
            DragonVisualCatalog.Match m;
            Assert.AreSame(list[0], DragonVisualCatalog.Resolve(list, "emberling", DragonVisualStage.Baby, false, out m));
            Assert.AreEqual(DragonVisualCatalog.Match.Exact, m);
            Assert.AreSame(list[1], DragonVisualCatalog.Resolve(list, "zephyr", DragonVisualStage.Baby, true, out m));
            Assert.AreEqual(DragonVisualCatalog.Match.Unstaged, m);
            Assert.IsNull(DragonVisualCatalog.Resolve(list, "emberling", DragonVisualStage.Adult, false, out m));
            Assert.AreEqual(DragonVisualCatalog.Match.None, m);
            Assert.AreSame(list[0], DragonVisualCatalog.Resolve(list, "emberling", DragonVisualStage.Adult, true, out m));
            Assert.AreEqual(DragonVisualCatalog.Match.OtherStage, m);
            Assert.IsNull(DragonVisualCatalog.Resolve(list, "leviathan", DragonVisualStage.Adult, true, out m));
        }

        [Test]
        public void Phenotype_EmptyProfileChangesNothing()
        {
            var g = new[] { new GenotypeEntry("pigment", "Rr") };
            Assert.AreEqual(0, PhenotypeProfile.Resolve(new List<PhenotypeProfile.Rule>(), "emberling", null, g).Count);
            Assert.AreEqual(0, PhenotypeProfile.Resolve(null, "emberling", null, g).Count);
        }

        [Test]
        public void Phenotype_MatchesLocusOrderInsensitiveAndLastRuleWins()
        {
            var rules = new List<PhenotypeProfile.Rule>
            {
                new PhenotypeProfile.Rule { locus = "pigment", alleles = "rR", materialProperty = "_Tint", kind = PhenotypeProfile.ValueKind.Float, value = 1 },
                new PhenotypeProfile.Rule { locus = "pigment", alleles = "Rr", speciesId = "emberling", materialProperty = "_Tint", kind = PhenotypeProfile.ValueKind.Float, value = 2 },
                new PhenotypeProfile.Rule { locus = "eye", alleles = "bb", materialProperty = "_Eye", kind = PhenotypeProfile.ValueKind.Float, value = 3 },
                new PhenotypeProfile.Rule { variantId = "frost", materialProperty = "_Var", kind = PhenotypeProfile.ValueKind.Float, value = 4 },
                new PhenotypeProfile.Rule { materialProperty = "_Unkeyed", kind = PhenotypeProfile.ValueKind.Float, value = 5 },
            };
            var g = new[] { new GenotypeEntry("pigment", "Rr"), new GenotypeEntry("eye", "Bb") };
            var o = PhenotypeProfile.Resolve(rules, "emberling", null, g);
            Assert.AreEqual(1, o.Count);
            Assert.AreEqual("_Tint", o[0].property);
            Assert.AreEqual(2f, o[0].value);
            o = PhenotypeProfile.Resolve(rules, "zephyr", "frost", g);
            Assert.AreEqual(2, o.Count);
            Assert.AreEqual(1f, o[0].value);
            Assert.AreEqual("_Var", o[1].property);
            Assert.IsFalse(PhenotypeProfile.SamePair("RR", "rr"));
            Assert.IsTrue(PhenotypeProfile.SamePair("pP", "Pp"));
        }

        [Test]
        public void Pool_ReusesPerKeyAndCapsFreeList()
        {
            int made = 0;
            var pool = new KeyedPool<string, object>(k => { made++; return new object(); }, null, null, maxFreePerKey: 1);
            var a = pool.Get("a");
            Assert.IsTrue(pool.Release("a", a));
            Assert.AreSame(a, pool.Get("a"));
            Assert.AreEqual(1, pool.Reused);
            var b = pool.Get("b");
            Assert.AreNotSame(a, b);
            var a2 = pool.Get("a");
            Assert.IsTrue(pool.Release("a", a));
            Assert.IsFalse(pool.Release("a", a2), "free list full: caller destroys");
            Assert.AreEqual(3, made);
        }

        [Test]
        public void UpdatePolicy_ThrottlesFarAndOffscreen()
        {
            var p = new UpdateRatePolicy { nearDistance = 10, farInterval = 2, offscreenInterval = 8, cullDistance = 50 };
            Assert.AreEqual(1, p.IntervalFor(true, 5));
            Assert.AreEqual(2, p.IntervalFor(true, 20));
            Assert.AreEqual(8, p.IntervalFor(false, 5));
            Assert.AreEqual(0, p.IntervalFor(true, 60));
            int hits = 0;
            for (int f = 0; f < 80; f++) if (UpdateRatePolicy.ShouldUpdate(f, 3, 8)) hits++;
            Assert.AreEqual(10, hits);
            Assert.IsFalse(UpdateRatePolicy.ShouldUpdate(0, 0, 0));
        }
    }
}
