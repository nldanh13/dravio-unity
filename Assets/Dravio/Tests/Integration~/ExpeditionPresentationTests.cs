using System.Linq;
using Dravio.Domain;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Queries;
using NUnit.Framework;

namespace Dravio.Tests
{
    public sealed class ExpeditionPresentationTests
    {
        const long Now=1795000000000;
        static GameState New()=>InitialStateFactory.Create("expedition-ui",Now,Rng.Create(1));
        static GameState Adult()
        {
            var s=New();s.Dragons[0].XpSec=SpeciesCatalog.Get(s.Dragons[0].SpeciesId).GrowthTimeSec;
            return s;
        }
        [Test] public void EligibilityFollowsCanonicalQueryAndLockedRoutes()
        {
            var s=New();var uid=s.Dragons[0].Uid;
            Assert.That(ExpeditionPresentation.CanStart(s,"origin_forest",uid,out var reason),Is.False);
            Assert.That(reason,Does.Contain("not-adult"));
            s=Adult();uid=s.Dragons[0].Uid;
            foreach(var route in Ecosystem.ExpeditionOptions(s))
                Assert.That(ExpeditionPresentation.CanStart(s,route.RouteId,uid,out _),
                    Is.EqualTo(Ecosystem.ExpeditionEligibility(s,route.RouteId,uid,out _)==null));
            Assert.That(ExpeditionPresentation.CanStart(s,"origin_forest",uid,out _),Is.True);
            Assert.That(ExpeditionPresentation.CanStart(s,"ocean_reef",uid,out _),Is.False);
            Assert.That(ExpeditionPresentation.CanStart(s,"sky_cloud",uid,out _),Is.False);
            Assert.That(ExpeditionPresentation.CanStart(s,"origin_forest","missing",out _),Is.False);
            Assert.That(ExpeditionPresentation.CanClaim(s),Is.False);
        }
        [Test] public void CanonicalStartOfflineReturnAndClaimPreserveUidAndPayOnce()
        {
            var s=Adult();var uid=s.Dragons[0].Uid;var clock=new FixedClock(Now);
            var start=CommandDispatcher.Dispatch(s,new StartExpeditionCommand("start", "origin_forest",uid),clock);
            Assert.That(start.Ok,Is.True,start.Error?.ToString());s=start.State;
            Assert.That(s.FindDragon(uid).Activity,Is.EqualTo(DragonActivity.Expedition));
            Assert.That(ConnectedWorldPolicy.InView(s,s.FindDragon(uid),"origin"),Is.False);
            Assert.That(ExpeditionPresentation.CanStart(s,"origin_forest",uid,out _),Is.False);
            Assert.That(ExpeditionPresentation.CanClaim(s),Is.False);
            var rewards=s.Expedition.Rewards.ToArray();clock.Set(s.Expedition.FinishAtMs);
            var settle=CommandDispatcher.Dispatch(s,new AdvanceTimeCommand("offline",AdvanceMode.Offline),clock);
            Assert.That(settle.Ok,Is.True,settle.Error?.ToString());s=settle.State;
            Assert.That(ExpeditionPresentation.CanClaim(s),Is.True);
            Assert.That(ConnectedWorldPolicy.InView(s,s.FindDragon(uid),"origin"),Is.True);
            Assert.That(s.Dragons.Count(d=>d.Uid==uid),Is.EqualTo(1));
            var before=Ecosystem.Materials(s).ToDictionary(m=>m.MaterialId,m=>m.Quantity);
            var claim=new ClaimExpeditionCommand("claim");
            var result=CommandDispatcher.Dispatch(s,claim,clock);
            Assert.That(result.Ok,Is.True,result.Error?.ToString());s=result.State;
            foreach(var kv in rewards)Assert.That(s.Player.Inventory[kv.Key],Is.EqualTo(before[kv.Key]+kv.Value));
            Assert.That(ExpeditionPresentation.CanClaim(s),Is.False);
            var dup=CommandDispatcher.Dispatch(s,claim,clock);
            Assert.That(dup.Duplicate,Is.True);Assert.That(dup.State.Player.Inventory.Values.Sum(),Is.EqualTo(s.Player.Inventory.Values.Sum()));
            Assert.That(s.Expedition,Is.Null);
        }
        [Test] public void ProjectMaterialShortfallsAreReadOnlyAndFollowQuote()
        {
            var s=New();s.SanctuaryProjectsBuilt=2;s.Player.Inventory["wood"]=1;
            var quote=ProgressionQueries.SanctuaryEconomy(s,Now);var rev=s.Revision;
            var missing=ExpeditionPresentation.ProjectShortfalls(s,quote);
            Assert.That(missing.Count,Is.EqualTo(1));Assert.That(missing[0].Key,Is.EqualTo("seed"));
            Assert.That(missing[0].Value,Is.EqualTo(1));Assert.That(s.Revision,Is.EqualTo(rev));
            Assert.That(s.Player.Inventory.ContainsKey("seed"),Is.False);
            s.Player.Inventory["seed"]=1;
            Assert.That(ExpeditionPresentation.ProjectShortfalls(s,ProgressionQueries.SanctuaryEconomy(s,Now)),Is.Empty);
        }
        [TestCase(0L,"00:00:00")]
        [TestCase(1L,"00:00:01")]
        [TestCase(900000L,"00:15:00")]
        [TestCase(90061000L,"25:01:01")]
        public void CountdownDoesNotWrapOrRoundDown(long ms,string expected)
            =>Assert.That(ExpeditionPresentation.Remaining(ms),Is.EqualTo(expected));
    }
}
