using Dravio.Domain.Behavior;
using NUnit.Framework;

namespace Dravio.Tests
{
    public sealed class AutonomousPresentationPolicyTests
    {
        [TestCase(DragonBehavior.Swim)]
        [TestCase(DragonBehavior.Fly)]
        [TestCase(DragonBehavior.Burrow)]
        public void UnsupportedMediaNeverAcquireGroundPoints(DragonBehavior behavior)
        {
            Assert.That(AutonomousPresentationPolicy.RequiresUnimplementedMedium(behavior),Is.True);
            Assert.That(AutonomousPresentationPolicy.UsesPoint(behavior),Is.False);
            Assert.That(AutonomousPresentationPolicy.PointMatches(behavior,"bask"),Is.False);
        }
        [TestCase(DragonBehavior.Bask,"bask",true)]
        [TestCase(DragonBehavior.Bask,"rest",false)]
        [TestCase(DragonBehavior.Rest,"rest",true)]
        [TestCase(DragonBehavior.Explore,"inspect_rune",true)]
        [TestCase(DragonBehavior.Explore,"sniff_flowers",true)]
        [TestCase(DragonBehavior.Explore,"splash",false)]
        [TestCase(DragonBehavior.Explore,"dig",false)]
        [TestCase(DragonBehavior.SeekFood,"drink",false)]
        [TestCase(DragonBehavior.Eat,"bask",false)]
        public void PointsMatchPresentationIntent(DragonBehavior behavior,string kind,bool expected)
            =>Assert.That(AutonomousPresentationPolicy.PointMatches(behavior,kind),Is.EqualTo(expected));
    }
}
