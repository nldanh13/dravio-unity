using NUnit.Framework;
using Dravio.Character.Diagnostics;

namespace Dravio.Character.Tests
{
    /// <summary>U1-CHAR.4: per-clip ground speed, QA warnings lookup, foot IK policy and the lab's playback maths.</summary>
    public sealed class AnimationProductionTests
    {
        private static DragonVisualCatalog.Entry Entry()
        {
            return new DragonVisualCatalog.Entry
            {
                speciesId = "emberling", locomotionReferenceSpeed = 0.14f,
                clipSpeeds = new[]
                {
                    new DragonVisualCatalog.ClipSpeed { clip = "Walk", groundSpeed = 0.14f },
                    new DragonVisualCatalog.ClipSpeed { clip = "Run", groundSpeed = 0.61f },
                },
                animQaWarnings = new[] { "Sleep: deformation edge compression over limit (0.41 vs 0.45)", "model: bones mirror", "Eat: ground x" },
            };
        }

        [Test]
        public void ReferenceSpeed_IsPerClipWithFallback()
        {
            var e = Entry();
            Assert.AreEqual(0.14f, DragonVisualCatalog.ReferenceSpeed(e, DragonLocomotion.Walk));
            Assert.AreEqual(0.61f, DragonVisualCatalog.ReferenceSpeed(e, DragonLocomotion.Run));
            Assert.AreEqual(0.14f, DragonVisualCatalog.ReferenceSpeed(e, DragonLocomotion.Swim), "no Swim speed: fallback");
            Assert.AreEqual(0f, DragonVisualCatalog.ReferenceSpeed(null, DragonLocomotion.Walk));
            // a run at its authored speed plays at rate 1, not at 0.61 / 0.14
            Assert.AreEqual(1f, DragonAnimationMap.PlaybackRate(DragonLocomotion.Run, 0.61f, DragonVisualCatalog.ReferenceSpeed(e, DragonLocomotion.Run)));
        }

        [Test]
        public void WarningsFor_ReturnsClipAndModelLinesOnly()
        {
            var w = DragonVisualCatalog.WarningsFor(Entry(), "Sleep");
            Assert.AreEqual(2, w.Count);
            Assert.IsTrue(w[0].StartsWith("Sleep:"));
            Assert.AreEqual(1, DragonVisualCatalog.WarningsFor(Entry(), "Walk").Count);
        }

        [Test]
        public void FootIk_OnlyOnGround()
        {
            Assert.IsTrue(DragonFootGrounding.AppliesTo(DragonLocomotion.Walk, DragonInteraction.None));
            Assert.IsTrue(DragonFootGrounding.AppliesTo(DragonLocomotion.Sleep, DragonInteraction.None));
            Assert.IsTrue(DragonFootGrounding.AppliesTo(DragonLocomotion.Idle, DragonInteraction.Eat));
            Assert.IsFalse(DragonFootGrounding.AppliesTo(DragonLocomotion.Idle, DragonInteraction.Happy), "hop: feet leave the ground");
            Assert.IsFalse(DragonFootGrounding.AppliesTo(DragonLocomotion.Swim, DragonInteraction.None));
            Assert.IsFalse(DragonFootGrounding.AppliesTo(DragonLocomotion.Fly, DragonInteraction.None));
            Assert.IsFalse(DragonFootGrounding.AppliesTo(DragonLocomotion.Burrow, DragonInteraction.None));
        }

        [Test]
        public void FootIk_SwingFadeAndPelvisDrop()
        {
            Assert.AreEqual(1f, DragonFootGrounding.SwingFactor(0.03f, 0.03f, 0.04f), "planted");
            Assert.AreEqual(0.5f, DragonFootGrounding.SwingFactor(0.05f, 0.03f, 0.04f));
            Assert.AreEqual(0f, DragonFootGrounding.SwingFactor(0.2f, 0.03f, 0.04f), "swing keeps its arc");
            // foot 2 must go 8 cm down but its leg can only extend 3 cm more: the pelvis drops 5 cm
            Assert.AreEqual(-0.05f, DragonFootGrounding.PelvisDrop(new[] { 0.02f, -0.08f }, new[] { 0.01f, 0.03f }));
            Assert.AreEqual(0f, DragonFootGrounding.PelvisDrop(new[] { 0.10f, -0.01f }, new[] { 0.0f, 0.05f }), "never raised");
        }

        [Test]
        public void Lab_PlaybackTimeAndClipStates()
        {
            Assert.AreEqual(0.25f, DragonAnimationLab.AdvanceTime(0.75f, 0.5f, 1f, true));
            Assert.AreEqual(0.9f, DragonAnimationLab.AdvanceTime(0.1f, -0.2f, 1f, true));
            Assert.AreEqual(1f, DragonAnimationLab.AdvanceTime(0.75f, 0.5f, 1f, false));
            Assert.AreEqual(DragonLocomotion.Idle, DragonAnimationLab.ShownLocomotion("Eat"));
            Assert.AreEqual(DragonLocomotion.Burrow, DragonAnimationLab.ShownLocomotion("Burrow"));
            // U1-CHAR.7: TakeOff/Land are now one-shot interactions (Animator Action); ground IK stays off for them
            Assert.AreEqual(DragonInteraction.TakeOff, (DragonInteraction)System.Enum.Parse(typeof(DragonInteraction), "TakeOff"));
            Assert.IsFalse(DragonFootGrounding.AppliesTo(DragonAnimationLab.ShownLocomotion("TakeOff"), DragonInteraction.TakeOff), "flight transitions: no ground IK");
            Assert.IsFalse(DragonFootGrounding.AppliesTo(DragonAnimationLab.ShownLocomotion("Glide"), DragonInteraction.None));
            Assert.IsTrue(DragonFootGrounding.AppliesTo(DragonAnimationLab.ShownLocomotion("Perch"), DragonInteraction.None), "perched: feet on the perch");
        }
    }
}
