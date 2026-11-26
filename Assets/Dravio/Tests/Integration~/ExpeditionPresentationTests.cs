using System.Linq;
using Dravio.Domain;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Progression;
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
            // U2.11 (ADR 0206): chuyến rừng đầu tiên đạt mốc 1, cộng thêm thưởng mốc đúng một lần.
            var milestone=result.Events.OfType<ExplorationMilestoneReached>().ToArray();
            Assert.That(milestone.Select(e=>e.Claims),Is.EqualTo(new[]{1}));
            var bonus=milestone.SelectMany(e=>e.Bonus).ToDictionary(b=>b.Key,b=>b.Value);
            foreach(var kv in rewards)Assert.That(s.Player.Inventory[kv.Key],Is.EqualTo(before[kv.Key]+kv.Value+(bonus.TryGetValue(kv.Key,out var extra)?extra:0)));
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
        [Test] public void ProjectQuoteAndCanonicalPurchaseAgree()
        {
            // Explicit economic fixture, not a way to award resources in the player UI.
            var s=Adult();s.Player.Gold=MicroGold.FromWholeGold(100000);
            s.SanctuaryProjectsBuilt=2;s.Player.Inventory["wood"]=1;s.Player.Inventory["seed"]=1;
            var quote=ProgressionQueries.SanctuaryEconomy(s,Now);
            Assert.That(quote.NextProjectBlockedBy,Is.Null);
            Assert.That(ExpeditionPresentation.ProjectShortfalls(s,quote),Is.Empty);
            var result=CommandDispatcher.Dispatch(s,new BuildSanctuaryProjectCommand("project"),new FixedClock(Now));
            Assert.That(result.Ok,Is.True,result.Error?.ToString());
            var ev=result.Events.OfType<SanctuaryProjectBuilt>().Single();
            Assert.That(ev.CostGoldMicro,Is.EqualTo(quote.NextProjectCostGoldMicro.Value));
            Assert.That(ev.MaterialEach,Is.EqualTo(quote.NextProjectMaterialEach));
            Assert.That(result.State.SanctuaryProjectsBuilt,Is.EqualTo(3));
            Assert.That(result.State.Player.Inventory["wood"],Is.Zero);
            Assert.That(result.State.Player.Inventory["seed"],Is.Zero);
            Assert.That(s.SanctuaryProjectsBuilt,Is.EqualTo(2));
            Assert.That(result.State.ProductionDragonIds,Is.EqualTo(s.ProductionDragonIds));
        }
        [Test] public void ProjectSummaryUsesCanonicalWorldFallbackAndMaterialNeeds()
        {
            // Fixture selects a late project to test presentation without replaying all earlier purchases.
            var s=New();s.SanctuaryProjectsBuilt=8;var rev=s.Revision;
            var q=ProgressionQueries.SanctuaryEconomy(s,Now);
            Assert.That(q.NextProject.Id,Is.EqualTo("ocean_tide_pools"));
            Assert.That(q.NextProjectMaterialFallback,Is.True);
            Assert.That(ExpeditionPresentation.ProjectSummary(s,q,false),
                Does.Contain("Tide pools").And.Contain("Fallback material source: Origin").And.Contain("wood").And.Contain("origin_forest"));
            s.FindZone("ocean").Unlocked=true;
            q=ProgressionQueries.SanctuaryEconomy(s,Now);
            Assert.That(q.NextProjectMaterialFallback,Is.False);
            Assert.That(ExpeditionPresentation.ProjectSummary(s,q,false),Does.Contain("crystal").And.Contain("coral").And.Not.Contain("Fallback material source"));
            Assert.That(ExpeditionPresentation.ProjectShortfalls(s,q).Select(x=>x.Key),Is.EqualTo(new[]{"crystal","coral"}));
            Assert.That(s.Revision,Is.EqualTo(rev));
        }
        [Test] public void ProjectSummaryHandlesSkyFallbackAndMax()
        {
            var s=New();s.FindZone("ocean").Unlocked=true;s.FindZone("sky").Unlocked=true;
            s.SanctuaryProjectsBuilt=14;
            var q=ProgressionQueries.SanctuaryEconomy(s,Now);
            Assert.That(ExpeditionPresentation.ProjectSummary(s,q,false),Does.Contain("Cloud bridge").And.Contain("Fallback material source: Ocean"));
            Assert.That(q.NextProjectMaterials.Select(x=>x.MaterialId),Does.Not.Contain("stardust"));
            s.SanctuaryProjectsBuilt=60;
            q=ProgressionQueries.SanctuaryEconomy(s,Now);
            Assert.That(ExpeditionPresentation.ProjectSummary(s,q,false),Does.Contain("60/60").And.Contain("All projects completed"));
            Assert.That(ExpeditionPresentation.ProjectShortfalls(s,q),Is.Empty);
        }
        [Test] public void SkyProgressShowsLockedEggMilestoneWithoutClaimPermission()
        {
            var s=New();
            var sky=ProgressionQueries.ExplorationRoutes(s).Single(x=>x.RouteId=="sky_cloud");
            Assert.That(sky.RewardsDefined,Is.False);
            Assert.That(sky.NextMilestoneAt,Is.EqualTo(15));
            Assert.That(ExpeditionPresentation.RouteProgress(sky,true),Does.Contain("15").And.Contain("aurorawing").And.Contain("chưa thể đạt"));
            Assert.That(ExpeditionPresentation.CanStart(s,"sky_cloud",s.Dragons[0].Uid,out _),Is.False);
        }
        [Test] public void RewardEggMessageDistinguishesDeliveryFromQueue()
        {
            Assert.That(ExpeditionPresentation.EggGrant(new RewardEggGranted("EXPEDITION_EGG_AURORAWING","aurorawing",false),false),Does.Contain("Queued").And.Contain("aurorawing"));
            Assert.That(ExpeditionPresentation.EggGrant(new RewardEggGranted("EXPEDITION_EGG_AURORAWING","aurorawing",true),false),Does.Contain("Delivered"));
        }
        [TestCase(0L,"00:00:00")]
        [TestCase(1L,"00:00:01")]
        [TestCase(900000L,"00:15:00")]
        [TestCase(90061000L,"25:01:01")]
        public void CountdownDoesNotWrapOrRoundDown(long ms,string expected)
            =>Assert.That(ExpeditionPresentation.Remaining(ms),Is.EqualTo(expected));
    }
}
