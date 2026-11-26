using System.Globalization;
using Dravio.TechnologyLab;
using NUnit.Framework;
namespace Dravio.Tests
{
    public sealed class RigContactCaptureTests
    {
        [Test] public void MissingAndSwingFeetAreNotSupport()
        {
            var frame=new RigContactFrame(0,new[]{
                new RigFootSample(-1,-1,0,0,true),new RigFootSample(1,-1,0,.1,true),
                new RigFootSample(-1,1,0,0,false),new RigFootSample(1,1,0,-.03,true)});
            Assert.That(frame.Hits,Is.EqualTo(3));Assert.That(frame.NearGround,Is.EqualTo(2));
            Assert.That(frame.Penetrations,Is.EqualTo(1));Assert.That(frame.MaxPenetration,Is.EqualTo(.03));
            Assert.That(frame.Candidate.Valid,Is.False);
        }
        [Test] public void SupportPlaneUsesRealGroundHeightsAndLimitsTilt()
        {
            var frame=new RigContactFrame(.1,new[]{
                new RigFootSample(-1,-1,-.5,-.5,true),new RigFootSample(1,-1,-.5,-.5,true),
                new RigFootSample(-1,1,.5,.5,true),new RigFootSample(1,1,.5,.5,true)});
            Assert.That(frame.Candidate.Valid,Is.True);
            Assert.That(frame.Candidate.Pitch,Is.EqualTo(18).Within(1e-8));
            Assert.That(frame.MaxPenetration,Is.Zero);Assert.That(frame.MeanNearGroundAbsGap,Is.Zero);
        }
        [Test] public void NoHitIsNotAZeroGapSuccess()
        {
            var frame=new RigContactFrame(0,new[]{new RigFootSample(0,0,0,0,false),new RigFootSample(0,0,0,double.NaN,true)});
            Assert.That(frame.Hits,Is.Zero);Assert.That(frame.MeanNearGroundAbsGap,Is.EqualTo(-1));
            Assert.That(frame.Candidate.Valid,Is.False);
        }
        [Test] public void CaptureBoundAndInvariantExport()
        {
            var c=new RigContactCapture(1);var f=new RigContactFrame(.25,new[]{new RigFootSample(0,0,0,-.03,true)});
            Assert.That(c.Add(f),Is.True);Assert.That(c.Add(f),Is.False);
            var old=CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture=new CultureInfo("fr-FR");
                Assert.That(c.Csv(),Does.Contain("0,0.25,1,1,1,1,0.03,0.03,0,0,0,0"));
                Assert.That(c.Summary(),Does.Contain("maxPenetrationM=0.0300"));
            }
            finally{CultureInfo.CurrentCulture=old;}
        }
    }
}
