using Dravio.Domain.Behavior;
using Dravio.Navigation;
using NUnit.Framework;

namespace Dravio.Tests
{
    public sealed class SocialPresentationSchedulerTests
    {
        [Test] public void NullCandidateAndUnsafeSpaceFailClosed()
        {
            var s=new SocialPresentationScheduler();
            Assert.That(s.TryBegin(null,1,true),Is.False);
            Assert.That(SocialPresentationScheduler.Spacing(new NavPoint(0,0,0),new NavPoint(.9f,2,0),1),Is.False);
            Assert.That(SocialPresentationScheduler.Spacing(new NavPoint(0,0,0),new NavPoint(1.1f,0,0),1),Is.True);
            Assert.That(SocialPresentationScheduler.Spacing(new NavPoint(float.NaN,0,0),new NavPoint(1,0,0),1),Is.False);
        }
    }
}
