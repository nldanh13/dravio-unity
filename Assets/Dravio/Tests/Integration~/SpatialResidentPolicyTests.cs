using Dravio.Navigation;
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
