using System;
using Dravio.Navigation;
using NUnit.Framework;
namespace Dravio.Tests
{
    public sealed class MediumTraversalReviewTests
    {
        static NavPoint P(float y)=>new NavPoint(0,y,0);
        static bool Clear(NavPoint p)=>p.Finite;
        static MediumTransitionGate Begin(MediumTransitionKind kind=MediumTransitionKind.TakeOff,bool approved=true,bool clip=true,string cert="review-1")
        {
            bool land=kind==MediumTransitionKind.Land;
            return MediumTransitionGate.TryBegin("uid","sky","h",cert,kind,P(land?1:0),P(land?0:1),land?-1:1,
                true,approved,clip,true,Clear,Clear,Clear,0);
        }
        [Test] public void MissingEvidenceOrClipBlocksStart()
        {
            Assert.That(Begin(approved:false),Is.Null);Assert.That(Begin(clip:false),Is.Null);
            Assert.That(Begin(cert:null),Is.Null);
            Assert.That(MediumTransitionGate.TryBegin("u","z","h","c",MediumTransitionKind.TakeOff,P(0),P(1),1,true,true,true,true,
                Clear,Clear,p=>p.Y<.4||p.Y>.6,0),Is.Null);
        }
        [Test] public void TakeoffLocksAndAppliesOffsetOnlyOnCompletion()
        {
            var t=Begin();
            Assert.That(t.Observe(1,"uid","sky","h",true,true,true,NavigationMedium.Ground,true,false,false,0),Is.False);
            Assert.That(t.MotionLocked,Is.True);Assert.That(t.Position.Y,Is.Zero);
            Assert.That(t.Observe(2,"uid","sky","h",true,true,true,NavigationMedium.Air,false,true,true,1),Is.True);
            Assert.That(t.Position.Y,Is.EqualTo(1));Assert.That(t.MotionLocked,Is.False);
            Assert.That(t.Observe(3,"uid","sky","h",true,true,true,NavigationMedium.Air,false,true,true,1),Is.False);
            Assert.That(t.Position.Y,Is.EqualTo(1));
        }
        [Test] public void LandingOffsetAtStartAndCompletionUnlock()
        {
            var t=Begin(MediumTransitionKind.Land);
            t.Observe(1,"uid","sky","h",true,true,true,NavigationMedium.Air,true,false,false,-1);
            Assert.That(t.Position.Y,Is.Zero);Assert.That(t.MotionLocked,Is.True);
            t.Observe(2,"uid","sky","h",true,true,true,NavigationMedium.Air,true,false,false,0);
            Assert.That(t.Observe(3,"uid","sky","h",true,true,true,NavigationMedium.Ground,false,true,true,0),Is.True);
        }
        [Test] public void InstantFallbackAndWrongOffsetFailClosed()
        {
            var t=Begin();
            t.Observe(1,"uid","sky","h",true,true,true,NavigationMedium.Air,false,true,true,1);
            Assert.That(t.State,Is.EqualTo(MediumTransitionState.Faulted));Assert.That(t.MotionLocked,Is.True);
            t=Begin();t.Observe(1,"uid","sky","h",true,true,true,NavigationMedium.Ground,true,false,false,0);
            t.Observe(2,"uid","sky","h",true,true,true,NavigationMedium.Air,false,true,true,.2f);
            Assert.That(t.State,Is.EqualTo(MediumTransitionState.Faulted));Assert.That(t.Position.Y,Is.Zero);
        }
        [Test] public void StaleSyncAndChangedIdentityFailClosed()
        {
            var t=Begin();t.Observe(1,"uid","sky","h",true,true,true,NavigationMedium.Ground,true,false,false,0);
            t.Observe(1,"uid","sky","h",true,true,true,NavigationMedium.Ground,true,false,false,0);
            Assert.That(t.State,Is.EqualTo(MediumTransitionState.Faulted));
            t=Begin();t.Observe(1,"other","sky","h",true,true,true,NavigationMedium.Ground,true,false,false,0);
            Assert.That(t.State,Is.EqualTo(MediumTransitionState.Faulted));
        }
        [Test] public void InterruptedClipOrNewlyBlockedEndpointCannotApplyOffset()
        {
            var t=Begin();
            t.Observe(1,"uid","sky","h",true,true,true,NavigationMedium.Ground,true,false,false,0);
            t.Observe(2,"uid","sky","h",true,true,true,NavigationMedium.Ground,false,false,true,0);
            Assert.That(t.State,Is.EqualTo(MediumTransitionState.Faulted));
            bool open=true;
            t=MediumTransitionGate.TryBegin("uid","sky","h","c",MediumTransitionKind.TakeOff,P(0),P(1),1,
                true,true,true,true,Clear,p=>open,Clear,0);
            t.Observe(1,"uid","sky","h",true,true,true,NavigationMedium.Ground,true,false,false,0);
            open=false;
            t.Observe(2,"uid","sky","h",true,true,true,NavigationMedium.Air,false,true,true,1);
            Assert.That(t.State,Is.EqualTo(MediumTransitionState.Faulted));
            Assert.That(t.Position.Y,Is.Zero);
        }
        [Test] public void WaterCannotInventHorizontalOrVerticalMovement()
        {
            Assert.That(MediumTransitionGate.TryBegin("u","o","h","c",MediumTransitionKind.WaterEnter,P(0),P(1),0,true,true,true,true,
                Clear,Clear,Clear,0),Is.Null);
            var t=MediumTransitionGate.TryBegin("u","o","h","c",MediumTransitionKind.WaterEnter,P(0),P(0),0,true,true,true,true,Clear,Clear,Clear,0);
            t.Observe(1,"u","o","h",true,true,true,NavigationMedium.Ground,true,false,false,0);
            Assert.That(t.Observe(2,"u","o","h",true,true,true,NavigationMedium.Swim,false,true,true,0),Is.True);
        }
        [Test] public void EnvelopeRejectsUnknownDimensions()
        {
            Assert.That(MediumReviewEnvelope.TryFromDimensions(1,1,1,0,1,out _),Is.False);
            Assert.That(MediumReviewEnvelope.TryFromDimensions(1,1,1,2,1,out var e),Is.True);
            Assert.That(e.Horizontal,Is.GreaterThan(1));Assert.That(e.Vertical,Is.EqualTo(.6f).Within(.0001));
        }
        [TestCase(NavigationMedium.Swim)]
        [TestCase(NavigationMedium.Air)]
        public void SpatialRouteUsesDenseSafetyCheck(NavigationMedium medium)
        {
            var start=new NavPoint(0,1,0);var end=new NavPoint(2,1,0);
            bool Geometry(NavPoint p)=>p.X>=0&&p.X<=2&&p.Y>=.5&&p.Y<=1.5&&Math.Abs(p.Z)<.4;
            var path=SpatialMediumReview.Plan(start,end,medium,Geometry);
            Assert.That(path.Success,Is.True);Assert.That(path.Waypoints[path.Waypoints.Count-1].X,Is.EqualTo(2));
            Assert.That(SpatialMediumReview.Plan(start,end,medium,p=>Geometry(p)&&Math.Abs(p.X-1)>.12).Success,Is.False);
        }
    }
}
