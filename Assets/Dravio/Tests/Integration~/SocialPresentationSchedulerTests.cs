using Dravio.Domain.Behavior;
using Dravio.Navigation;
using NUnit.Framework;

namespace Dravio.Tests
{
    public sealed class SocialPresentationSchedulerTests
    {
        // Explicit synthetic DTO fixture: internal setters belong to Domain; no game state is edited.
        static SocialInteractionCandidate Candidate(string a="a",string b="b",SocialInteractionKind kind=SocialInteractionKind.PlayTogether,long end=55000)
        {
            var c=new SocialInteractionCandidate();
            void Set(string name,object value)=>typeof(SocialInteractionCandidate).GetProperty(name).SetValue(c,value);
            Set("InitiatorUid",a);Set("PartnerUid",b);Set("Kind",kind);Set("ZoneId","origin");Set("HabitatId","origin_meadow");
            Set("MinDistanceDm",12);Set("StartedAtMs",0L);Set("UntilMs",end);return c;
        }
        [Test] public void ExclusivePairAndEpochAttemptLimit()
        {
            var s=new SocialPresentationScheduler();var c=Candidate();
            Assert.That(s.TryBegin(c,1000,true),Is.True);
            Assert.That(s.Active.Partner("a"),Is.EqualTo("b"));Assert.That(s.Active.Partner("b"),Is.EqualTo("a"));
            Assert.That(s.Active.Partner("other"),Is.Null);
            Assert.That(s.TryBegin(Candidate("c","d"),1001,true),Is.False);
            s.Release();
            Assert.That(s.TryBegin(Candidate("b","c"),2000,true),Is.False);
            Assert.That(s.TryBegin(Candidate("c","d"),2000,true),Is.True);
            s.Release();Assert.That(s.TryBegin(Candidate(end:120000),61000,true),Is.True);
        }
        [Test] public void InteractionRequiresApproachFaceAndMotionReadiness()
        {
            var s=new SocialPresentationScheduler();var c=Candidate();
            s.TryBegin(c,1000,true);var a=s.Active;
            Assert.That(a.Matches(c),Is.True);Assert.That(a.Matches(Candidate("a","x")),Is.False);
            a.Tick(1100,true,false,false,false,true);Assert.That(a.Phase,Is.EqualTo(SocialPresentationPhase.Approach));
            a.Tick(1200,true,true,false,true,true);Assert.That(a.Phase,Is.EqualTo(SocialPresentationPhase.Face));
            a.Tick(1300,true,true,true,false,true);Assert.That(a.Phase,Is.EqualTo(SocialPresentationPhase.Face));
            a.Tick(1400,true,true,true,true,true);Assert.That(a.Phase,Is.EqualTo(SocialPresentationPhase.Interact));
            a.Tick(3900,true,true,true,false,true);Assert.That(a.Phase,Is.EqualTo(SocialPresentationPhase.Finished));
        }
        [TestCase(false,true)]
        [TestCase(true,false)]
        public void LostEligibilityOrSpacingCancelsBoth(bool eligible,bool spacing)
        {
            var s=new SocialPresentationScheduler();s.TryBegin(Candidate(),1000,true);
            var a=s.Active;a.Tick(1100,eligible,true,true,true,spacing);
            Assert.That(a.Phase,Is.EqualTo(SocialPresentationPhase.Cancelled));
            a.Tick(1200,true,true,true,true,true);Assert.That(a.Terminal,Is.True);
        }
        [Test] public void ExpiredOrUnsupportedOrRepeatedClockCannotStart()
        {
            var s=new SocialPresentationScheduler();
            Assert.That(s.TryBegin(Candidate(kind:SocialInteractionKind.KeepDistance),1000,true),Is.False);
            Assert.That(s.TryBegin(Candidate("a","a"),1000,true),Is.False);
            Assert.That(s.TryBegin(Candidate(end:2000),1000,true),Is.False);
            Assert.That(s.TryBegin(Candidate(),1000,false),Is.False);
            Assert.That(s.TryBegin(Candidate(),1000,true),Is.True);
            var a=s.Active;a.Tick(999,true,true,true,true,true);
            Assert.That(a.Phase,Is.EqualTo(SocialPresentationPhase.Cancelled));s.Release();
            Assert.That(s.TryBegin(Candidate("c","d"),999,true),Is.False);
        }
        [Test] public void LeaseTimesOutAndCancelsWhenPartnerLeavesInteractionRange()
        {
            var s=new SocialPresentationScheduler();s.TryBegin(Candidate(),1000,true);
            s.Active.Tick(13000,true,true,true,true,true);
            Assert.That(s.Active.Phase,Is.EqualTo(SocialPresentationPhase.Cancelled));
            s.Release();s.TryBegin(Candidate("c","d"),14000,true);
            s.Active.Tick(14100,true,true,true,true,true);
            s.Active.Tick(14200,true,false,true,true,true);
            Assert.That(s.Active.Phase,Is.EqualTo(SocialPresentationPhase.Cancelled));
        }
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
