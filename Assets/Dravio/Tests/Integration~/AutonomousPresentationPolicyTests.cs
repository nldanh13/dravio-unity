using Dravio.Domain;
using Dravio.Domain.Behavior;
using Dravio.Domain.Core;
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
        [Test] public void GameplayRestNeverRequestsScenicPoint()
        {
            Assert.That(AutonomousPresentationPolicy.AllowsPoint(DragonBehavior.Rest,BehaviorRole.Gameplay),Is.False);
            Assert.That(AutonomousPresentationPolicy.AllowsPoint(DragonBehavior.Rest,BehaviorRole.Presentation),Is.True);
        }

        [Test] public void FoodMarkerRequiresCorrectUidAndLiveEligibleDrop()
        {
            const long now=1795000000000;
            var s=InitialStateFactory.Create("food-marker-test",now,Rng.Create(1));
            var d=s.Dragons[0];
            s.Food.Drops.Add(new FoodDrop{Id="f",ZoneId=d.Location.ZoneId,HabitatId=d.Location.HabitatId,
                DietId=DietId.Fruit,SpawnedAtMs=now,ExpiresAtMs=now+12000});
            Assert.That(AutonomousPresentationPolicy.CanConsumeMarker(s,d.Uid,d.Uid,"f",now,DayPhase.Day,false),Is.True);
            Assert.That(AutonomousPresentationPolicy.CanConsumeMarker(s,d.Uid,d.Uid,"f",now,DayPhase.Day,true),Is.True);
            Assert.That(AutonomousPresentationPolicy.CanConsumeMarker(s,d.Uid,"another","f",now,DayPhase.Day,false),Is.False);
            Assert.That(AutonomousPresentationPolicy.CanConsumeMarker(s,d.Uid,d.Uid,"f",now+12000,DayPhase.Day,false),Is.False);
            s.Food.Drops.Clear();
            Assert.That(AutonomousPresentationPolicy.CanConsumeMarker(s,d.Uid,d.Uid,"f",now,DayPhase.Day,true),Is.False);
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
