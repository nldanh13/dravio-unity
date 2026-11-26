using System.Linq;
using Dravio.Domain;
using Dravio.Domain.Behavior;
using Dravio.Domain.Core;
using Dravio.Domain.Queries;
using NUnit.Framework;
namespace Dravio.Tests
{
    public sealed class ResearchDiscoveryViewTests
    {
        const long Now=1795000000000;
        static GameState New()=>InitialStateFactory.Create("research-ui",Now,Rng.Create(1));
        [Test] public void UnknownSpeciesDoesNotRevealIdentityOrBiology()
        {
            var s=New();var text=ResearchDiscoveryView.Species(s,"leviathan",false);
            Assert.That(text,Does.Contain("Undiscovered").And.Not.Contain("leviathan"));
            Assert.That(ResearchDiscoveryView.Species(s,"emberling",false),Does.Contain("emberling"));
        }
        [Test] public void LocalOpportunitiesRequireLiveUidAndUnlockedView()
        {
            var s=New();var uid=s.Dragons[0].Uid;
            Assert.That(ResearchDiscoveryView.LocalOpportunities(s,Now,DayPhase.Day,"origin",_=>false),Is.Empty);
            Assert.That(ResearchDiscoveryView.LocalOpportunities(s,Now,DayPhase.Day,"ocean",_=>true),Is.Empty);
            var list=ResearchDiscoveryView.LocalOpportunities(s,Now,DayPhase.Day,"origin",x=>x==uid);
            Assert.That(list.Count,Is.EqualTo(1));Assert.That(list[0].Uid,Is.EqualTo(uid));
            Assert.That(ResearchDiscoveryView.CanRecord(list[0]),Is.True);
            Assert.That(ResearchDiscoveryView.CanRecord(null),Is.False);
        }
        [Test] public void SelectionRemainsBoundToUidWhenOrderChanges()
        {
            Assert.That(ResearchDiscoveryView.KeepOrFirst(new[]{"b","a"},"a"),Is.EqualTo("a"));
            Assert.That(ResearchDiscoveryView.KeepOrFirst(new[]{"b"},"a"),Is.EqualTo("b"));
            Assert.That(ResearchDiscoveryView.Next(new[]{"b","a"},"a"),Is.EqualTo("b"));
            Assert.That(ResearchDiscoveryView.Next(new string[0],"a"),Is.Null);
        }
        [Test] public void RealObservationRefreshShowsCanonicalCooldownAndSavedRecord()
        {
            var s=New();string uid=s.Dragons[0].Uid;var clock=new FixedClock(Now);
            var result=CommandDispatcher.Dispatch(s,new ObserveDragonCommand("research-ui-observe",uid,DayPhase.Day),clock);
            Assert.That(result.Ok,Is.True,result.Error?.ToString());
            Assert.That(result.Events.OfType<DragonObserved>().Single().DragonUid,Is.EqualTo(uid));
            Assert.That(result.State.Research.IsEmpty,Is.False);
            Assert.That(s.Research.IsEmpty,Is.True);
            var opportunity=ResearchDiscoveryView.LocalOpportunities(result.State,Now,DayPhase.Day,"origin",x=>x==uid).Single();
            Assert.That(ResearchDiscoveryView.CanRecord(opportunity),Is.False);
            Assert.That(opportunity.CooldownRemainingMs,Is.GreaterThan(0));
            Assert.That(ProgressionQueries.IndividualResearch(result.State,uid).Observations,Is.EqualTo(1));
        }
        [Test] public void QuestAndCodexBrowsingDoesNotMutateCanonicalState()
        {
            var s=New();long rev=s.Revision,gold=s.Player.Gold.Micro;
            foreach(var q in ProgressionQueries.QuestRequirements(s))
            {Assert.That(ResearchDiscoveryView.Quest(q,false,false),Does.Contain("Completion and rewards disabled"));Assert.That(q.CanComplete,Is.False);}
            ResearchDiscoveryView.Species(s,"emberling",true);
            Assert.That(s.Revision,Is.EqualTo(rev));Assert.That(s.Player.Gold.Micro,Is.EqualTo(gold));
            Assert.That(s.Research.IsEmpty,Is.True);
        }
    }
}
