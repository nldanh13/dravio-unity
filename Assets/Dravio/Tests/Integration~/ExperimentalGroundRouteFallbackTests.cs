using System;
using System.IO;
using System.Text.Json;
using Dravio.Navigation;
using Dravio.TechnologyLab;
using NUnit.Framework;
namespace Dravio.Tests
{
    public sealed class ExperimentalGroundRouteFallbackTests
    {
        [Test] public void SuccessfulBaselineIsReturnedUnchanged()
        {
            var a=new NavPoint(0,0,0);var b=new NavPoint(2,0,0);
            var old=SanctuaryRoutePlanner.Plan(a,b,NavigationMedium.Ground,p=>true);
            var r=ExperimentalGroundRouteFallback.Try(a,b,old,p=>throw new InvalidOperationException());
            Assert.That(r.Path,Is.SameAs(old));Assert.That(r.Attempts,Is.Zero);Assert.That(r.Rescued,Is.False);
        }
        [Test] public void NarrowCorridorUsesFinerGridWithoutChangingGoal()
        {
            var a=new NavPoint(0,0,0);var b=new NavPoint(2,0,0);
            bool Clear(NavPoint p)=>p.X>=-.1f&&p.X<=2.1f&&p.Z>=-.05f&&p.Z<=.65f&&
                (p.X<=.31f||p.X>=1.69f||(p.Z>=.27f&&p.Z<=.33f));
            var old=SanctuaryRoutePlanner.Plan(a,b,NavigationMedium.Ground,Clear);
            Assert.That(old.Success,Is.False);
            var r=ExperimentalGroundRouteFallback.Try(a,b,old,Clear);
            Assert.That(r.Rescued,Is.True);Assert.That(r.Expanded,Is.LessThanOrEqualTo(768));
            var previous=a;foreach(var point in r.Path.Waypoints)
            {Assert.That(SanctuaryRoutePlanner.SegmentClear(previous,point,Clear,.025f),Is.True);previous=point;}
            Assert.That(NavPoint.Distance(previous,b),Is.LessThan(.0001));
        }
        [Test] public void InvalidEndpointDoesNotRetryAndClosedBarrierRemainsClosed()
        {
            var a=new NavPoint(0,0,0);var b=new NavPoint(2,0,0);
            bool Clear(NavPoint p)=>p.X>=-.5&&p.X<=2.5&&Math.Abs(p.Z)<=1&&Math.Abs(p.X-1)>.15;
            var old=SanctuaryRoutePlanner.Plan(a,b,NavigationMedium.Ground,Clear);
            var r=ExperimentalGroundRouteFallback.Try(a,b,old,Clear);
            Assert.That(r.Path.Success,Is.False);Assert.That(r.Expanded,Is.LessThanOrEqualTo(768));
            old=SanctuaryRoutePlanner.Plan(a,b,NavigationMedium.Ground,p=>false);
            r=ExperimentalGroundRouteFallback.Try(a,b,old,p=>false);
            Assert.That(r.Attempts,Is.Zero);
        }
        [Test] public void ReportExactControllerBudgetOnAuthoredOrigin()
        {
            var map=JsonSerializer.Deserialize<LabLocationMap>(File.ReadAllText(Path.Combine(
                TestContext.CurrentContext.TestDirectory,"LabMaps","OriginSanctuaryLocations.json")),
                new JsonSerializerOptions{IncludeFields=true});
            int rescuedTotal=0;
            foreach(var h in map.habitats)
            {
                int pairs=0,fixedCount=0,rescued=0,attempts=0,maxNodes=0;
                bool Clear(NavPoint p)=>map.Clear(p,h.id,NavigationMedium.Ground);
                foreach(var a in map.Candidates(h.id,NavigationMedium.Ground))
                foreach(var b in map.Candidates(h.id,NavigationMedium.Ground))
                {
                    if(a==b)continue;pairs++;
                    var old=SanctuaryRoutePlanner.Plan(a.Point,b.Point,NavigationMedium.Ground,Clear,.45f,768);
                    var result=ExperimentalGroundRouteFallback.Try(a.Point,b.Point,old,Clear);
                    if(old.Success){fixedCount++;Assert.That(result.Path,Is.SameAs(old));}
                    if(result.Rescued)rescued++;
                    attempts+=result.Attempts;maxNodes=Math.Max(maxNodes,result.Expanded+old.Expanded);
                    Assert.That(result.Expanded,Is.LessThanOrEqualTo(768));
                }
                rescuedTotal+=rescued;
                TestContext.Progress.WriteLine($"LAB FALLBACK {h.id} pairs={pairs} fixed={fixedCount} rescued={rescued} attempts={attempts} maxCombinedNodes={maxNodes}");
            }
            Assert.That(rescuedTotal,Is.GreaterThan(0));
        }
    }
}
