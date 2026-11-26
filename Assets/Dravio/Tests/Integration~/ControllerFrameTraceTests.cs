using Dravio;
using NUnit.Framework;
using System.Globalization;
namespace Dravio.Tests
{
    public sealed class ControllerFrameTraceTests
    {
        static ControllerFrameSample Sample(double dt=.05)=>new ControllerFrameSample(dt,3,1,2,1,1,1,0,0,.55,1.2,.4);
        [Test] public void BoundedCaptureAndStop()
        {
            var t=new ControllerFrameTrace(2);t.Add(Sample());t.Add(Sample());t.Add(Sample());
            Assert.That(t.Count,Is.EqualTo(2));Assert.That(t.Recording,Is.False);
            Assert.That(t.Summary(),Does.Contain("motionAttempts=4").And.Contain("arrivalCalls=2").And.Contain("routePlans=2"));
            var stopped=new ControllerFrameTrace(2);stopped.Stop();stopped.Add(Sample());Assert.That(stopped.Count,Is.Zero);
        }
        [Test] public void CsvUsesInvariantNumericFormat()
        {
            var t=new ControllerFrameTrace(1);t.Add(Sample());var old=CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture=new CultureInfo("fr-FR");
                Assert.That(t.Csv(),Does.Contain("0,0.05,3,1,2,1,1,1,0,0,0.55,1.2,0.4"));
                Assert.That(t.Summary(),Does.Contain("motionP95ms=1.200"));
            }
            finally{CultureInfo.CurrentCulture=old;}
        }
    }
}
