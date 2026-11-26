using System.IO;
using System.Linq;
using System.Text.Json;
using Dravio.Navigation;
using Dravio.TechnologyLab;
using NUnit.Framework;
namespace Dravio.Tests
{
    public sealed class SpatialResidentPolicyTests
    {
        [TestCase("origin",true,true,true,NavigationMedium.Ground)]
        [TestCase("ocean",true,false,true,NavigationMedium.Swim)]
        [TestCase("sky",false,true,true,NavigationMedium.Air)]
        [TestCase("ocean",false,true,true,NavigationMedium.Ground)]
        [TestCase("sky",true,true,false,NavigationMedium.Ground)]
        public void OnlyAuthoredWorldAndBiologyChooseMedium(string zone,bool swim,bool fly,bool enabled,NavigationMedium expected)
            =>Assert.That(SpatialResidentPolicy.RequestedMedium(zone,swim,fly,enabled),Is.EqualTo(expected));
        [TestCase(true,true,true,false,true,true)]
        [TestCase(false,true,true,false,true,false)]
        [TestCase(true,false,true,false,true,false)]
        [TestCase(true,true,false,false,true,false)]
        [TestCase(true,true,true,true,true,false)]
        [TestCase(true,true,true,false,false,false)]
        public void TranslationRequiresVerifiedModelAndBehavior(bool live,bool exact,bool clip,bool action,bool move,bool expected)
            =>Assert.That(SpatialResidentPolicy.MayTranslate(live,exact,clip,action,move),Is.EqualTo(expected));
        // Rounded SAMPLE dimensions from CHAR.7b motion-report.md, never actual GLB certification.
        [TestCase("droplet",1.64f,.61f,.90f,.90f,.92f)]
        [TestCase("frostfin",1.45f,1.15f,.40f,1.18f,.47f)]
        [TestCase("leviathan",2.17f,.60f,.34f,.68f,.36f)]
        [TestCase("steamdrake",2.27f,.43f,.42f,.52f,.42f)]
        public void OceanSampleEnvelopeSurvey(string species,float length,float width,float height,float swimWidth,float swimHeight)
        {
            Assert.That(MediumReviewEnvelope.TryFromDimensions(length,width,height,swimWidth,swimHeight,out var envelope),Is.True);
            var map=JsonSerializer.Deserialize<LabLocationMap>(File.ReadAllText(Path.Combine(
                TestContext.CurrentContext.TestDirectory,"LabMaps","OceanLocations.json")),new JsonSerializerOptions{IncludeFields=true});
            foreach(var h in map.habitats)
            {
                var authored=map.spawnPoints.Where(s=>s.habitatId==h.id&&s.medium=="swim").ToArray();
                bool Clear(NavPoint p)=>map.ClearReviewEnvelope(p,h.id,NavigationMedium.Swim,envelope);
                var valid=authored.Where(s=>Clear(s.Point)).ToArray();
                int pairs=0,success=0;
                foreach(var a in valid)foreach(var b in valid)
                {
                    if(a==b)continue;pairs++;
                    var path=SpatialMediumReview.Plan(a.Point,b.Point,NavigationMedium.Swim,Clear,768);
                    if(!path.Success)continue;
                    success++;var previous=a.Point;
                    foreach(var q in path.Waypoints)
                    {Assert.That(SanctuaryRoutePlanner.SegmentClear(previous,q,Clear,.025f),Is.True);previous=q;}
                    Assert.That(NavPoint.Distance(previous,b.Point),Is.LessThan(.0001));
                }
                TestContext.Progress.WriteLine($"LAB SPATIAL SAMPLE {species}/{h.id} spawns={valid.Length}/{authored.Length} routes={success}/{pairs}");
            }
        }
        [Test] public void MixedMediumSeparationIsSymmetricAndConservative()
        {
            var a=new NavPoint(0,0,0);var b=new NavPoint(.7f,3,0);
            Assert.That(SpatialResidentPolicy.Separated(a,.5f,b,.275f),Is.False);
            Assert.That(SpatialResidentPolicy.Separated(b,.275f,a,.5f),Is.False);
            Assert.That(SpatialResidentPolicy.Separated(a,.5f,new NavPoint(1,0,0),.275f),Is.True);
            Assert.That(SpatialResidentPolicy.Separated(a,float.NaN,b,.275f),Is.False);
        }
    }
}
