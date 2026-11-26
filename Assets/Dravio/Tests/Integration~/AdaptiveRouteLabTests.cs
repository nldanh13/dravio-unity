using System;
using System.IO;
using System.Text.Json;
using Dravio.Navigation;
using Dravio.TechnologyLab;
using NUnit.Framework;
namespace Dravio.Tests
{
    public sealed class AdaptiveRouteLabTests
    {
        [Test] public void DirectNeedsNoSearch()
        {
            var r=AdaptiveRouteLab.Plan(new NavPoint(0,0,0),new NavPoint(2,0,0),NavigationMedium.Ground,p=>true);
            Assert.That(r.Success,Is.True);Assert.That(r.Attempts,Is.EqualTo(1));Assert.That(r.Expanded,Is.Zero);
        }
        [Test] public void BlockedMapHonorsTotalBudget()
        {
            bool Clear(NavPoint p)=>Math.Abs(p.X)<=2 && Math.Abs(p.Z)<=2 && Math.Abs(p.X)>.1;
            var r=AdaptiveRouteLab.Plan(new NavPoint(-1,0,0),new NavPoint(1,0,0),NavigationMedium.Ground,Clear,32);
            Assert.That(r.Success,Is.False);Assert.That(r.Expanded,Is.LessThanOrEqualTo(32));
        }
        [Test] public void InvalidEndpointDoesNotRetry()
        {
            var r=AdaptiveRouteLab.Plan(new NavPoint(0,0,0),new NavPoint(1,0,0),NavigationMedium.Ground,p=>p.X>0);
            Assert.That(r.Success,Is.False);Assert.That(r.Attempts,Is.EqualTo(1));
        }
        [TestCase("OriginSanctuaryLocations.json",NavigationMedium.Ground)]
        [TestCase("OceanLocations.json",NavigationMedium.Swim)]
        [TestCase("SkyLocations.json",NavigationMedium.Air)]
        public void AuthoredPairsDenseValidated(string file,NavigationMedium medium)
        {
            var map=JsonSerializer.Deserialize<LabLocationMap>(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory,"LabMaps",file)),new JsonSerializerOptions{IncludeFields=true});
            foreach(var h in map.habitats)
            {
                TestContext.Progress.WriteLine(AdaptiveRouteLab.Compare(map,h.id,medium));
                foreach(var a in map.Candidates(h.id,medium))foreach(var b in map.Candidates(h.id,medium))
                {
                    if(a==b)continue;
                    bool Clear(NavPoint p)=>map.Clear(p,h.id,medium);
                    var result=AdaptiveRouteLab.Plan(a.Point,b.Point,medium,Clear);
                    Assert.That(result.Expanded,Is.LessThanOrEqualTo(2048));
                    if(!result.Success)continue;
                    var previous=a.Point;
                    foreach(var q in result.Path.Waypoints)
                    {Assert.That(SanctuaryRoutePlanner.SegmentClear(previous,q,Clear,.025f),Is.True);previous=q;}
                    Assert.That(NavPoint.Distance(previous,b.Point),Is.LessThan(.0001));
                }
            }
        }
    }
}
