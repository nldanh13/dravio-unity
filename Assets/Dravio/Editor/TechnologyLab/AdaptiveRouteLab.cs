using System;
using Dravio.Navigation;

namespace Dravio.TechnologyLab
{
    public sealed class AdaptiveRouteResult
    {
        public RouteResult Path { get; internal set; }
        public int Expanded { get; internal set; }
        public int Attempts { get; internal set; }
        public int RejectedUnsafe { get; internal set; }
        public float Step { get; internal set; }
        public bool Success=>Path!=null && Path.Success;
    }
    // Candidate only. Same clearance predicate, finer grid on failure, strict aggregate node budget.
    public static class AdaptiveRouteLab
    {
        static readonly float[] Steps={.45f,.30f,.20f};
        public static AdaptiveRouteResult Plan(NavPoint start,NavPoint goal,NavigationMedium medium,
            Func<NavPoint,bool> clear,int totalBudget=2048)
        {
            if(totalBudget<1 || totalBudget>8192)throw new ArgumentOutOfRangeException(nameof(totalBudget));
            var result=new AdaptiveRouteResult();
            foreach(float step in Steps)
            {
                int remaining=totalBudget-result.Expanded;
                if(remaining<=0)break;
                // Reserve at least half the total work for fallback on the first attempt.
                int attemptBudget=result.Attempts==0?Math.Max(1,remaining/2):remaining;
                result.Attempts++;result.Step=step;
                var path=SanctuaryRoutePlanner.Plan(start,goal,medium,clear,step,attemptBudget);
                result.Expanded+=path.Expanded;
                if(path.Status==RouteStatus.InvalidEndpoint || path.Status==RouteStatus.UnsupportedMedium)
                {result.Path=path;return result;}
                if(!path.Success){result.Path=path;continue;}
                var previous=start;bool safe=true;
                foreach(var q in path.Waypoints)
                {
                    if(!SanctuaryRoutePlanner.SegmentClear(previous,q,clear,.025f)){safe=false;break;}
                    previous=q;
                }
                if(safe && NavPoint.Distance(previous,goal)<=.0001){result.Path=path;return result;}
                result.RejectedUnsafe++;result.Path=null;
            }
            return result;
        }
        public static string Compare(LabLocationMap map,string habitat,NavigationMedium medium,int budget=2048)
        {
            var points=new System.Collections.Generic.List<LabLocationMap.Spawn>(map.Candidates(habitat,medium));
            bool Clear(NavPoint p)=>map.Clear(p,habitat,medium);
            int pairs=0,original=0,adaptive=0,rescued=0,regressed=0,rejected=0,maxNodes=0;
            long nodes=0;double oldMs=0,newMs=0;
            var timer=new System.Diagnostics.Stopwatch();
            foreach(var a in points)foreach(var b in points)
            {
                if(a==b)continue;pairs++;
                timer.Restart();var old=SanctuaryRoutePlanner.Plan(a.Point,b.Point,medium,Clear,.45f,budget);
                timer.Stop();oldMs+=timer.Elapsed.TotalMilliseconds;
                bool validOld=old.Success;var previous=a.Point;
                if(validOld)foreach(var q in old.Waypoints)
                {
                    if(!SanctuaryRoutePlanner.SegmentClear(previous,q,Clear,.025f)){validOld=false;break;}
                    previous=q;
                }
                timer.Restart();var next=Plan(a.Point,b.Point,medium,Clear,budget);
                timer.Stop();newMs+=timer.Elapsed.TotalMilliseconds;
                if(validOld)original++;if(next.Success)adaptive++;
                if(!validOld && next.Success)rescued++;if(validOld && !next.Success)regressed++;
                rejected+=next.RejectedUnsafe;nodes+=next.Expanded;maxNodes=Math.Max(maxNodes,next.Expanded);
            }
            return FormattableString.Invariant($"LAB ADAPT {map.zoneId}/{habitat} {medium}: pairs={pairs} fixed={original} adaptive={adaptive} rescued={rescued} regressed={regressed} denseRejected={rejected} nodes={nodes} maxNodes={maxNodes} cpu={oldMs:F2}->{newMs:F2}ms");
        }
    }
}
