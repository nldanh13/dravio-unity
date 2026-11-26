using System;
using System.Linq;
using Dravio.Navigation;
using NUnit.Framework;

namespace Dravio.Tests
{
    public sealed class SanctuaryRoutePlannerTests
    {
        static NavPoint P(float x,float z,float y=0)=>new NavPoint(x,y,z);
        static bool Box(NavPoint p)=>p.X>=-3 && p.X<=3 && p.Z>=-3 && p.Z<=3 && p.Y>=0 && p.Y<=3;
        static void Valid(RouteResult r,NavPoint start,NavPoint goal,Func<NavPoint,bool> clear)
        {
            Assert.That(r.Success,Is.True,r.Status.ToString());var previous=start;
            foreach(var point in r.Waypoints)
            {Assert.That(SanctuaryRoutePlanner.SegmentClear(previous,point,clear),Is.True);previous=point;}
            Assert.That(NavPoint.Distance(previous,goal),Is.LessThan(.00001));
        }
        [Test] public void DirectRouteIsExact()
        {
            var r=SanctuaryRoutePlanner.Plan(P(-2,0),P(2,0),NavigationMedium.Ground,Box);
            Assert.That(r.Status,Is.EqualTo(RouteStatus.Direct));Assert.That(r.Waypoints.Count,Is.EqualTo(1));
        }
        [Test] public void GroundDetoursAroundWallAndRepeatsDeterministically()
        {
            bool Clear(NavPoint p)=>Box(p) && !(Math.Abs(p.X)<.4f && Math.Abs(p.Z)<1.5f);
            var a=P(-2,0);var b=P(2,0);
            var r=SanctuaryRoutePlanner.Plan(a,b,NavigationMedium.Ground,Clear);
            Assert.That(r.Status,Is.EqualTo(RouteStatus.Found));Valid(r,a,b,Clear);
            Assert.That(r.Waypoints.Any(p=>Math.Abs(p.Z)>=1.5f),Is.True);
            var again=SanctuaryRoutePlanner.Plan(a,b,NavigationMedium.Ground,Clear);
            Assert.That(again.Waypoints.Select(x=>(x.X,x.Y,x.Z)),Is.EqualTo(r.Waypoints.Select(x=>(x.X,x.Y,x.Z))));
        }
        [Test] public void ClosedWallNeverReturnsPartialOrCrossingRoute()
        {
            bool Clear(NavPoint p)=>Box(p) && Math.Abs(p.X)>=.3f;
            var r=SanctuaryRoutePlanner.Plan(P(-2,0),P(2,0),NavigationMedium.Ground,Clear);
            Assert.That(r.Success,Is.False);Assert.That(r.Waypoints,Is.Empty);
        }
        [TestCase(NavigationMedium.Swim)]
        [TestCase(NavigationMedium.Air)]
        public void VolumeCanRouteAboveObstacle(NavigationMedium medium)
        {
            bool Clear(NavPoint p)=>Box(p) && Math.Abs(p.Z)<=.3f && !(Math.Abs(p.X)<.4f && p.Y<1.8f);
            var a=P(-2,0,.9f);var b=P(2,0,.9f);
            var r=SanctuaryRoutePlanner.Plan(a,b,medium,Clear);
            Valid(r,a,b,Clear);Assert.That(r.Waypoints.Any(p=>p.Y>=1.8f),Is.True);
        }
        [Test] public void GroundCannotChangeHeightOrUseBurrow()
        {
            Assert.That(SanctuaryRoutePlanner.Plan(P(0,0),P(1,0,1),NavigationMedium.Ground,Box).Status,Is.EqualTo(RouteStatus.InvalidEndpoint));
            Assert.That(SanctuaryRoutePlanner.Plan(P(0,0),P(1,0),NavigationMedium.Burrow,Box).Status,Is.EqualTo(RouteStatus.UnsupportedMedium));
        }
        [Test] public void InvalidEndpointAndBudgetFailClosed()
        {
            Assert.That(SanctuaryRoutePlanner.Plan(P(0,0),P(8,0),NavigationMedium.Ground,Box).Status,Is.EqualTo(RouteStatus.InvalidEndpoint));
            bool Clear(NavPoint p)=>Box(p) && !(Math.Abs(p.X)<.4f && Math.Abs(p.Z)<1.5f);
            var r=SanctuaryRoutePlanner.Plan(P(-2,0),P(2,0),NavigationMedium.Ground,Clear,maxExpanded:1);
            Assert.That(r.Status,Is.EqualTo(RouteStatus.BudgetExceeded));Assert.That(r.Expanded,Is.EqualTo(1));Assert.That(r.Waypoints,Is.Empty);
        }
        [Test] public void SegmentChecksInteriorNotOnlyEndpoints()
        {
            Assert.That(SanctuaryRoutePlanner.SegmentClear(P(-1,0),P(1,0),p=>Math.Abs(p.X)>.06f),Is.False);
        }
    }
}
