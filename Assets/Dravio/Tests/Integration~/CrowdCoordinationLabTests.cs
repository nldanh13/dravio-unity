using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Dravio.TechnologyLab;
using NUnit.Framework;

namespace Dravio.Tests
{
    public sealed class CrowdCoordinationLabTests
    {
        [Test] public void GoalQueueIsExclusiveAndStable()
        {
            var q=new GoalReservationQueue();q.Request("b","food",0,100);
            q.Request("a","food",0,100);q.Request("c","food",1,100);
            q.Resolve(1,(u,g)=>true);
            Assert.That(q.Owner("food"),Is.EqualTo("a"));Assert.That(q.Pending,Is.EqualTo(2));
            q.Request("c","food",2,999);q.Cancel("a");q.Resolve(2,(u,g)=>true);
            Assert.That(q.Owner("food"),Is.EqualTo("b"));
            q.Resolve(100,(u,g)=>true);Assert.That(q.Owner("food"),Is.Null);Assert.That(q.Pending,Is.Zero);
        }
        [Test] public void ChangingGoalOrInvalidEligibilityReleasesLease()
        {
            var q=new GoalReservationQueue();q.Request("a","p",0,100);q.Request("b","p",1,100);
            q.Resolve(1,(u,g)=>true);q.Request("a","other",2,100);
            q.Resolve(2,(u,g)=>true);Assert.That(q.Owner("p"),Is.EqualTo("b"));
            Assert.That(q.Owner("other"),Is.EqualTo("a"));
            q.Resolve(3,(u,g)=>u!="b");Assert.That(q.Owner("p"),Is.Null);
            Assert.Throws<ArgumentException>(()=>q.Resolve(2,(u,g)=>true));
        }
        [TestCase(5)]
        [TestCase(15)]
        [TestCase(30)]
        public void CompareRecoveryOnSameRoutes(int count)
        {
            var map=JsonSerializer.Deserialize<LabLocationMap>(File.ReadAllText(Path.Combine(
                TestContext.CurrentContext.TestDirectory,"LabMaps","OriginSanctuaryLocations.json")),
                new JsonSerializerOptions{IncludeFields=true});
            var lab=RoutedCrowdExperiment.Build(map,"origin_meadow",count);
            Assert.That(lab,Is.Not.Null);
            var goals=lab.Coordinated.Agents.Select(a=>a.Goal).ToArray();
            for(int tick=0;tick<800;tick++)
            {
                var before=lab.Coordinated.Agents.Select(a=>a.Position).ToArray();
                lab.Step(includeRecovery:true);
                for(int i=0;i<before.Length;i++)
                {
                    var a=lab.Coordinated.Agents[i];
                    Assert.That((a.Position-before[i]).Length,Is.LessThanOrEqualTo(a.Speed*.05+1e-8));
                    for(int j=0;j<i;j++)
                        Assert.That(SteeringLab.SweptDistance(before[i],a.Position,before[j],lab.Coordinated.Agents[j].Position),
                            Is.GreaterThanOrEqualTo(a.Radius+lab.Coordinated.Agents[j].Radius+.02499));
                }
                if(tick==399||tick==799)TestContext.Progress.WriteLine(lab.RecoverySummary());
            }
            Assert.That(lab.Coordinated.Agents.Select(a=>a.Goal),Is.EqualTo(goals));
            Assert.That(lab.Recovery.Attempts,Is.LessThanOrEqualTo(800));
            Assert.That(lab.Recovery.Expanded,Is.LessThanOrEqualTo(lab.Recovery.Attempts*768));
            Assert.That(lab.Recovery.Recovered+lab.Recovery.Failed,Is.EqualTo(lab.Recovery.Attempts));
        }
    }
}
