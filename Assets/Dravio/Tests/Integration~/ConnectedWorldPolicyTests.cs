using System.Collections.Generic;
using System.Linq;
using Dravio.Domain;
using Dravio.Domain.Catalog;
using Dravio.Domain.Progression;
using Dravio.Domain.World;
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
        [Test] public void CanonicalTransferPreservesUidAndViewDoesNotMutateState()
        {
            var s=New();var clock=new FixedClock(1795000000000);
            s.UnlockedAchievements.Add("ACH_FIRST_ADULT");s.ClaimedAchievements.Add("ACH_FIRST_ADULT");
            // Test-only fixture satisfies not-yet-playable quest objective; never called by runtime UI.
            foreach(string discovery in ProgressCatalog.DiscoveryIds)Zones.RecordDiscovery(s,discovery,new List<DomainEvent>());
            foreach(string h in new[]{"origin_warm_rocks","origin_pondbank"})Zones.ApplyUnlock(s,h,new List<DomainEvent>());
            s.Tutorial.Status=TutorialStatus.Completed;s.Tutorial.Completed=ProgressCatalog.TutorialStepIds.ToList();
            Assert.That(WorldExpansion.ApplyQuestComplete(s,"QUEST_AQUATIC_EXPLORATION",new List<DomainEvent>()),Is.Null);
            var opened=CommandDispatcher.Dispatch(s,new UnlockZoneCommand("open-ocean","ocean"),clock);
            Assert.That(opened.Ok,Is.True,opened.Error?.ToString());s=opened.State;
            var claim=CommandDispatcher.Dispatch(s,new ClaimPackCommand("claim-ocean",MoveTarget.Storage),clock);
            Assert.That(claim.Ok,Is.True,claim.Error?.ToString());s=claim.State;
            string uid=s.Dragons.Single(d=>d.SpeciesId=="droplet").Uid;
            var option=Dravio.Domain.Queries.Ecosystem.TravelOptions(s,uid).Single(o=>o.ZoneId=="ocean"&&o.HabitatId=="ocean_estuary");
            Assert.That(ConnectedWorldPolicy.MatchesDestination(option,"ocean","ocean_estuary"),Is.True);
            Assert.That(option.Blocked,Is.Null);
            var move=CommandDispatcher.Dispatch(s,new MoveDragonCommand("move-ocean",uid,MoveTarget.Zone("ocean","ocean_estuary")),clock);
            Assert.That(move.Ok,Is.True,move.Error?.ToString());s=move.State;
            var d=s.FindDragon(uid);long rev=s.Revision;long gold=s.Player.Gold.Micro;int count=s.Dragons.Count;
            for(int i=0;i<10;i++)
            {
                Assert.That(ConnectedWorldPolicy.InView(s,d,"ocean"),Is.True);
                Assert.That(ConnectedWorldPolicy.InView(s,d,"origin"),Is.False);
                Assert.That(ConnectedWorldPolicy.InView(s,d,"sky"),Is.False);
            }
            Assert.That(s.Revision,Is.EqualTo(rev));Assert.That(s.Player.Gold.Micro,Is.EqualTo(gold));
            Assert.That(s.Dragons.Count,Is.EqualTo(count));Assert.That(s.Dragons.Count(x=>x.Uid==uid),Is.EqualTo(1));
            var store=CommandDispatcher.Dispatch(s,new MoveDragonCommand("store-ocean",uid,MoveTarget.Storage),clock);
            Assert.That(store.Ok,Is.True,store.Error?.ToString());
            Assert.That(ConnectedWorldPolicy.InView(store.State,store.State.FindDragon(uid),"ocean"),Is.False);
        }
        [Test] public void PathsAreDistinctAndUnknownFailsClosed()
        {
            Assert.That(ConnectedWorldPolicy.ScenePath("origin"),Is.Not.EqualTo(ConnectedWorldPolicy.ScenePath("ocean")));
            Assert.That(ConnectedWorldPolicy.ScenePath("ocean"),Is.Not.EqualTo(ConnectedWorldPolicy.ScenePath("sky")));
            Assert.That(ConnectedWorldPolicy.ScenePath("../ocean"),Is.Null);
        }
    }
}
