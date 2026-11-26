using Dravio.Domain;
using Dravio.Domain.Core;
using NUnit.Framework;
namespace Dravio.Tests
{
    public sealed class LivingResidentPolicyTests
    {
        static GameState NewState()=>InitialStateFactory.Create("living-test",1795000000000,Rng.Create(1));
        [Test] public void FounderIsVisibleAndWalks()
        {
            var s=NewState();var d=s.Dragons[0];long revision=s.Revision;
            Assert.That(LivingResidentPolicy.InOriginView(s,d),Is.True);
            Assert.That(LivingResidentPolicy.CanWalk(d),Is.True);
            Assert.That(s.Revision,Is.EqualTo(revision));
        }
        [TestCase(DragonActivity.Breeding)]
        [TestCase(DragonActivity.Expedition)]
        [TestCase(DragonActivity.Care)]
        public void BusyResidentsAreHidden(DragonActivity activity)
        {
            var s=NewState();var d=s.Dragons[0];d.Activity=activity;
            Assert.That(LivingResidentPolicy.InOriginView(s,d),Is.False);
        }
        [Test] public void NonOriginAndNonHabitatAreHidden()
        {
            var s=NewState();var d=s.Dragons[0];
            foreach(var loc in new[]{DragonLocation.Storage,DragonLocation.Released,DragonLocation.Care("case"),
                DragonLocation.Shelter("origin"),DragonLocation.Zone("ocean","ocean_estuary")})
            {d.Location=loc;Assert.That(LivingResidentPolicy.InOriginView(s,d),Is.False);}
        }
        [Test] public void SelectionCyclesByUid()
        {
            var ids=new[]{"a","b","c"};
            Assert.That(LivingResidentPolicy.NextUid(ids,null),Is.EqualTo("a"));
            Assert.That(LivingResidentPolicy.NextUid(ids,"b"),Is.EqualTo("c"));
            Assert.That(LivingResidentPolicy.NextUid(ids,"c"),Is.EqualTo("a"));
            Assert.That(LivingResidentPolicy.NextUid(new string[0],"a"),Is.Null);
        }
    }
}
