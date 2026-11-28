using Dravio.Domain;
using Dravio.Domain.Behavior;
using Dravio.Domain.Core;
using NUnit.Framework;

namespace Dravio.Tests
{
    public sealed class AutonomousPresentationPolicyTests
    {
        [TestCase(.4f,false,.55f)]
        [TestCase(.55f,true,0f)]
        [TestCase(.825f,true,.3575f)]
        [TestCase(2f,true,.55f)]
        public void MealApproachOnlyStopsWithinUnobstructedReach(float distance,bool clear,float speed)
            =>Assert.That(AutonomousPresentationPolicy.MealApproachSpeed(distance,clear),Is.EqualTo(speed).Within(.0001f));

        [TestCase(null,0,0)]
        [TestCase(1000L,1000L,8)]
        [TestCase(1000L,8999L,1)]
        [TestCase(1000L,9000L,0)]
        public void FeedWaitMatchesMealCooldown(long? lastMeal,long now,int expected)
            =>Assert.That(SanctuaryFeedback.FeedWaitSeconds(lastMeal,now),Is.EqualTo(expected));

        [TestCase(200L,152000000L,true,"200 G · Thiếu 48 G")]
        [TestCase(200L,199999999L,true,"200 G · Thiếu 0,000001 G")]
        [TestCase(200L,199500000L,false,"200 G · Need 0.5 G")]
        [TestCase(200L,200000000L,false,"200 G")]
        [TestCase(null,0L,true,"Đã tối đa")]
        public void UpgradePriceShowsActualShortfall(long? price,long balance,bool vietnamese,string expected)
            =>Assert.That(SanctuaryFeedback.UpgradePrice(price,balance,vietnamese),Is.EqualTo(expected));

        [Test]
        public void FiveMealSlotsAreDistinctAndEvenlySpacedOnOpenGround()
        {
            var spots=new System.Collections.Generic.List<(float x,float z)>();
            for(int slot=0;slot<5;slot++)
                Assert.That(AutonomousPresentationPolicy.TryMealRingOffset(slot,5,(x,z)=>
                {spots.Add((x,z));return true;}),Is.True);
            for(int i=0;i<spots.Count;i++)
            {
                var a=spots[i];
                var b=spots[(i+1)%spots.Count];
                Assert.That(a.x*a.x+a.z*a.z,Is.EqualTo(.65f*.65f).Within(.0001f));
                float dx=a.x-b.x,dz=a.z-b.z;
                Assert.That(dx*dx+dz*dz,Is.EqualTo(.58386f).Within(.001f));
            }
        }

        [Test]
        public void BlockedMealSlotTriesAdjacentAngleBeforeOppositeSide()
        {
            int attempts=0;
            Assert.That(AutonomousPresentationPolicy.TryMealRingOffset(0,5,(x,z)=>
            {attempts++;return x>.5f && z<-.1f;}),Is.True);
            Assert.That(attempts,Is.EqualTo(3));
        }

        [TestCase(1,0)]
        [TestCase(5,24)]
        public void MealRingStopsWhenNoCandidateIsUsable(int count,int expectedAttempts)
        {
            int attempts=0;
            Assert.That(AutonomousPresentationPolicy.TryMealRingOffset(0,count,(x,z)=>
            {attempts++;return false;}),Is.False);
            Assert.That(attempts,Is.EqualTo(expectedAttempts));
        }

        [TestCase(320,180)]
        [TestCase(844,390)]
        [TestCase(1920,1080)]
        public void LabelsStayInsideViewportAndSeparateAsDragonsCross(int width,int height)
        {
            foreach(float x in new float[]{0,width/2,width})
            foreach(float y in new float[]{0,height/2,height})
            foreach(float offset in new float[]{-60,0,60})
            {
                var first=AutonomousPresentationPolicy.PlaceLabel(x,y,86,24,width,height,null).Value;
                var food=AutonomousPresentationPolicy.PlaceLabel(x+offset,y,55,24,width,height,(first.x,first.y,86,24)).Value;
                Assert.That(first.x,Is.InRange(0f,width-86f));
                Assert.That(first.y,Is.InRange(0f,height-24f));
                Assert.That(food.x,Is.InRange(0f,width-55f));
                Assert.That(food.y,Is.InRange(0f,height-24f));
                Assert.That(food.x+55<=first.x || first.x+86<=food.x ||
                    food.y+24<=first.y || first.y+24<=food.y,Is.True);
            }
        }

        [Test]
        public void ShortViewportUsesSideSpaceAndSuppressesOnlyALabelThatCannotFit()
        {
            Assert.That(AutonomousPresentationPolicy.PlaceLabel(20,20,30,20,100,20,(0,0,40,20)),
                Is.EqualTo((44f,0f)));
            Assert.That(AutonomousPresentationPolicy.PlaceLabel(20,20,30,20,70,20,(0,0,40,20)),Is.Null);
            Assert.That(AutonomousPresentationPolicy.PlaceLabel(20,20,86,24,70,20,null),Is.Null);
        }

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
            Assert.That(AutonomousPresentationPolicy.CanConsumeMarker(s,d.Uid,d.Uid,"f",now,DayPhase.Day),Is.True);
            Assert.That(AutonomousPresentationPolicy.CanConsumeMarker(s,d.Uid,"another","f",now,DayPhase.Day),Is.False);
            Assert.That(AutonomousPresentationPolicy.CanConsumeMarker(s,d.Uid,d.Uid,"f",now+12000,DayPhase.Day),Is.False);
            s.Food.Drops.Clear();
            Assert.That(AutonomousPresentationPolicy.CanConsumeMarker(s,d.Uid,d.Uid,"f",now,DayPhase.Day),Is.False);
        }

        [Test]
        public void FoodMarkerRevalidatesTheFallbackWhenTheSelectedDragonRecovers()
        {
            const long now=1795000000000;
            var s=InitialStateFactory.Create("food-marker-test",now,Rng.Create(1));
            var selected=s.Dragons[0];
            var fallback=selected.Clone();
            fallback.Uid="fallback";
            s.Dragons.Add(fallback);
            selected.LastFeedXpAtMs=now;
            s.Food.Drops.Add(new FoodDrop{Id="f",ZoneId=selected.Location.ZoneId,HabitatId=selected.Location.HabitatId,
                DietId=DietId.Fruit,SpawnedAtMs=now,ExpiresAtMs=now+12000,TargetDragonUid=selected.Uid});

            Assert.That(AutonomousPresentationPolicy.CanConsumeMarker(s,fallback.Uid,fallback.Uid,"f",now,DayPhase.Day),Is.True);
            const long afterCooldown=now+8000;
            Assert.That(AutonomousPresentationPolicy.CanConsumeMarker(s,fallback.Uid,fallback.Uid,"f",afterCooldown,DayPhase.Day),Is.False);
            Assert.That(AutonomousPresentationPolicy.CanConsumeMarker(s,selected.Uid,selected.Uid,"f",afterCooldown,DayPhase.Day),Is.True);
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
