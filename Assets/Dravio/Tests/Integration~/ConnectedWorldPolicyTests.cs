using Dravio.Domain;
using Dravio.Domain.Core;
using NUnit.Framework;
namespace Dravio.Tests
{
    public sealed class ConnectedWorldPolicyTests
    {
        static GameState New()=>InitialStateFactory.Create("connected-worlds",1795000000000,Rng.Create(1));
        [Test] public void KnownUnlockedWorldOnly()
        {
            var s=New();Assert.That(ConnectedWorldPolicy.CanVisit(s,"origin"),Is.True);
            Assert.That(ConnectedWorldPolicy.CanVisit(s,"ocean"),Is.False);
            Assert.That(ConnectedWorldPolicy.CanVisit(s,"sky"),Is.False);
            Assert.That(ConnectedWorldPolicy.CanVisit(s,"unknown"),Is.False);
            Assert.That(ConnectedWorldPolicy.CanVisit(null,"origin"),Is.False);
        }
        [Test] public void SameUidProjectedOnlyInCanonicalZoneAndUnlockedHabitat()
        {
            var s=New();var d=s.Dragons[0];string uid=d.Uid;long rev=s.Revision;
            Assert.That(ConnectedWorldPolicy.InView(s,d,"origin"),Is.True);
            Assert.That(ConnectedWorldPolicy.InView(s,d,"ocean"),Is.False);
            // Synthetic unlocked snapshot for projection tests, not an unlock or movement command.
            var zone=s.FindZone("ocean");zone.Unlocked=true;zone.UnlockedHabitatIds.Clear();
            d.Location=DragonLocation.Zone("ocean","ocean_estuary");
            Assert.That(ConnectedWorldPolicy.InView(s,d,"ocean"),Is.False);
            zone.UnlockedHabitatIds.Add("ocean_estuary");
            Assert.That(ConnectedWorldPolicy.InView(s,d,"ocean"),Is.True);
            Assert.That(ConnectedWorldPolicy.InView(s,d,"origin"),Is.False);
            Assert.That(d.Uid,Is.EqualTo(uid));Assert.That(s.Revision,Is.EqualTo(rev));
            d.Activity=DragonActivity.Expedition;Assert.That(ConnectedWorldPolicy.InView(s,d,"ocean"),Is.False);
            d.Activity=DragonActivity.Idle;d.Location=DragonLocation.Storage;
            Assert.That(ConnectedWorldPolicy.InView(s,d,"ocean"),Is.False);
        }
        [Test] public void PathsAreDistinctAndUnknownFailsClosed()
        {
            Assert.That(ConnectedWorldPolicy.ScenePath("origin"),Is.Not.EqualTo(ConnectedWorldPolicy.ScenePath("ocean")));
            Assert.That(ConnectedWorldPolicy.ScenePath("ocean"),Is.Not.EqualTo(ConnectedWorldPolicy.ScenePath("sky")));
            Assert.That(ConnectedWorldPolicy.ScenePath("../ocean"),Is.Null);
        }
    }
}
