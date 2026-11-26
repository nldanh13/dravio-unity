#if UNITY_EDITOR || DRAVIO_DOTNET_TESTS
using System;

namespace Dravio.Navigation
{
    public readonly struct RouteFallbackResult
    {
        public readonly RouteResult Path;
        public readonly int Attempts,Expanded,RejectedUnsafe;
        public readonly bool Rescued;
        public RouteFallbackResult(RouteResult path,int attempts,int expanded,int rejected,bool rescued)
        {Path=path;Attempts=attempts;Expanded=expanded;RejectedUnsafe=rejected;Rescued=rescued;}
    }
    // TL.13: preserve the exact controller baseline on success. Fallback only after
    // a failed coarse search; a separate small additional budget and dense acceptance.
    public static class ExperimentalGroundRouteFallback
    {
        public static RouteFallbackResult Try(NavPoint start,NavPoint goal,RouteResult baseline,Func<NavPoint,bool> clear)
        {
            if(baseline==null)throw new ArgumentNullException(nameof(baseline));
            if(clear==null)throw new ArgumentNullException(nameof(clear));
            if(baseline.Status!=RouteStatus.Unreachable && baseline.Status!=RouteStatus.BudgetExceeded)
                return new RouteFallbackResult(baseline,0,0,0,false);
            int attempts=0,expanded=0,rejected=0;
            foreach(float step in new[]{.30f,.20f})
            {
                attempts++;
                var path=SanctuaryRoutePlanner.Plan(start,goal,NavigationMedium.Ground,clear,step,384);
                expanded+=path.Expanded;
                if(path.Status==RouteStatus.InvalidEndpoint||path.Status==RouteStatus.UnsupportedMedium)break;
                if(!path.Success)continue;
                bool safe=true;var previous=start;
                foreach(var q in path.Waypoints)
                {
                    if(!SanctuaryRoutePlanner.SegmentClear(previous,q,clear,.025f)){safe=false;break;}
                    previous=q;
                }
                if(safe && NavPoint.Distance(previous,goal)<=.0001)
                    return new RouteFallbackResult(path,attempts,expanded,rejected,true);
                rejected++;
            }
            return new RouteFallbackResult(baseline,attempts,expanded,rejected,false);
        }
    }
}
#endif
