using System.IO;
using System.Text.Json;
using Dravio.TechnologyLab;
using NUnit.Framework;
namespace Dravio.Tests
{
    public sealed class RoutedCrowdLabTests
    {
        [TestCase(5)]
        [TestCase(15)]
        [TestCase(30)]
        public void CompareSameAuthoredAStarRoutes(int count)
        {
            var map=JsonSerializer.Deserialize<LabLocationMap>(File.ReadAllText(Path.Combine(
                TestContext.CurrentContext.TestDirectory,"LabMaps","OriginSanctuaryLocations.json")),
                new JsonSerializerOptions{IncludeFields=true});
            var lab=RoutedCrowdExperiment.Build(map,"origin_meadow",count);
            Assert.That(lab,Is.Not.Null);Assert.That(lab.Valid,Is.GreaterThanOrEqualTo(2));
            Assert.That(lab.Valid+lab.Skipped,Is.EqualTo(count));
            for(int tick=0;tick<400;tick++)lab.Step();
            TestContext.Progress.WriteLine(lab.Summary());
            Assert.That(lab.Baseline.MinGap,Is.GreaterThanOrEqualTo(.02499));
            Assert.That(lab.Steering.MinGap,Is.GreaterThanOrEqualTo(.02499));
            // Do not assert superiority: report regressions or stalls as evidence.
        }
    }
}
