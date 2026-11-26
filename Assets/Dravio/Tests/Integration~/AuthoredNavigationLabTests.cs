using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Dravio.Navigation;
using Dravio.TechnologyLab;
using NUnit.Framework;

namespace Dravio.Tests
{
    public sealed class AuthoredNavigationLabTests
    {
        static LabLocationMap Load(string file)=>JsonSerializer.Deserialize<LabLocationMap>(
            File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory,"LabMaps",file)),new JsonSerializerOptions{IncludeFields=true});
        [TestCase("OriginSanctuaryLocations.json",NavigationMedium.Ground)]
        [TestCase("OceanLocations.json",NavigationMedium.Swim)]
        [TestCase("SkyLocations.json",NavigationMedium.Air)]
        public void BenchmarkAuthoredMaps(string file,NavigationMedium medium)
        {
            var map=Load(file);int pairs=0;
            foreach(var h in map.habitats)
            foreach(float step in new[]{.30f,.45f,.60f})
            {
                var r=AuthoredRouteBenchmark.Run(map,h.id,medium,step);
                TestContext.Progress.WriteLine(AuthoredRouteBenchmark.Describe(r));
                Assert.That(r.Direct+r.Detours+r.Invalid+r.Unreachable+r.Budget,Is.EqualTo(r.Pairs));
                Assert.That(r.MaxExpanded,Is.LessThanOrEqualTo(2048));
                // Dense misses are reported as a promotion blocker, not hidden by changing the production planner.
                pairs+=r.Pairs;
            }
            Assert.That(pairs,Is.GreaterThan(0),"fixture must exercise real authored endpoints");
        }
        [Test] public void OriginPondDoesNotInventVolume()
        {
            var m=Load("OriginSanctuaryLocations.json");
            Assert.That(m.Clear(new NavPoint(-4.9f,0,-.4f),"origin_pondbank",NavigationMedium.Swim),Is.False);
        }
        [Test] public void VolumeClearanceRejectsOutsideHeight()
        {
            var m=Load("OceanLocations.json");var s=m.spawnPoints.First(x=>x.medium=="swim");
            Assert.That(m.Clear(new NavPoint(s.x,.31f,s.z),s.habitatId,NavigationMedium.Swim),Is.False);
            Assert.That(m.Clear(new NavPoint(s.x,8,s.z),s.habitatId,NavigationMedium.Swim),Is.False);
        }
    }
}
