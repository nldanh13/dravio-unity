using System;
using System.Collections.Generic;
using System.Linq;
using Dravio.Navigation;

namespace Dravio.TechnologyLab
{
    /// <summary>Same authored A* routes, geometry, endpoints and disc safety; only local steering differs.
    /// A reference experiment, not a replay of the production controller's turning/replan logic.</summary>
    public sealed class RoutedCrowdExperiment
    {
        public readonly SteeringLab Baseline,Steering;
        public readonly string Habitat;
        public readonly int Requested,Valid,Skipped;
        public readonly float Height;
        public readonly LabLocationMap Map;

        RoutedCrowdExperiment(LabLocationMap map,string habitat,int requested,int valid,int skipped,float height,
            SteeringLab baseline,SteeringLab steering)
        {Map=map;Habitat=habitat;Requested=requested;Valid=valid;Skipped=skipped;Height=height;Baseline=baseline;Steering=steering;}

        public static RoutedCrowdExperiment Build(LabLocationMap map,string habitat,int requested)
        {
            if(requested<2 || requested>30)throw new ArgumentOutOfRangeException(nameof(requested));
            var candidates=map.Candidates(habitat,NavigationMedium.Ground).OrderBy(x=>x.id,StringComparer.Ordinal).ToArray();
            if(candidates.Length<2)return null;
            float height=candidates[0].y;
            bool Clear(NavPoint p)=>map.Clear(p,habitat,NavigationMedium.Ground);
            bool Segment(LabVec a,LabVec b)=>SanctuaryRoutePlanner.SegmentClear(
                new NavPoint((float)a.X,height,(float)a.Y),new NavPoint((float)b.X,height,(float)b.Y),Clear,.025f);
            // Additional samples are explicitly synthetic grid placements within real authored geometry,
            // not claimed to be authored spawn points. Fixed radius .24 + .025 margin.
            var points=new List<NavPoint>();
            void Add(NavPoint p)
            {
                if(points.Count>=requested || Math.Abs(p.Y-height)>.02f || !Clear(p))return;
                foreach(var q in points)if(NavPoint.Distance(p,q)<.60)return;
                points.Add(p);
            }
            foreach(var c in candidates)Add(c.Point);
            var polygon=map.habitats.First(h=>h.id==habitat).polygonXZ;
            float minX=polygon.Where((v,i)=>i%2==0).Min(),maxX=polygon.Where((v,i)=>i%2==0).Max();
            float minZ=polygon.Where((v,i)=>i%2==1).Min(),maxZ=polygon.Where((v,i)=>i%2==1).Max();
            for(float x=minX+.3f;x<maxX && points.Count<requested;x+=.65f)
            for(float z=minZ+.3f;z<maxZ && points.Count<requested;z+=.65f)Add(new NavPoint(x,height,z));
            if(points.Count<2)return null;
            var agents=new List<LabAgent>();var routes=new Dictionary<int,LabVec[]>();
            for(int i=0;i<points.Count;i++)
            {
                var a=points[i];var b=points[(i+points.Count/2)%points.Count];
                var path=SanctuaryRoutePlanner.Plan(a,b,NavigationMedium.Ground,Clear,.45f,2048);
                if(!path.Success)continue;
                var waypoints=path.Waypoints.Select(q=>new LabVec(q.X,q.Z)).ToArray();
                var previous=new LabVec(a.X,a.Z);bool safe=true;
                foreach(var q in waypoints){if(!Segment(previous,q)){safe=false;break;}previous=q;}
                if(!safe)continue;
                agents.Add(new LabAgent(i,new LabVec(a.X,a.Z),new LabVec(b.X,b.Z),.24,.55));
                routes[i]=waypoints;
            }
            if(agents.Count<2)return null;
            Func<LabAgent,LabVec> Follow()
            {
                var index=new Dictionary<int,int>();
                return a=>
                {
                    index.TryGetValue(a.Id,out int k);var route=routes[a.Id];
                    while(k<route.Length-1 && (a.Position-route[k]).Length<=.08)k++;
                    index[a.Id]=k;return route[k];
                };
            }
            double extent=Math.Max(10,Math.Max(Math.Max(Math.Abs(minX),Math.Abs(maxX)),Math.Max(Math.Abs(minZ),Math.Abs(maxZ)))+1);
            return new RoutedCrowdExperiment(map,habitat,requested,agents.Count,requested-agents.Count,height,
                new SteeringLab(agents.ToArray(),null,extent,Segment,Follow()),
                new SteeringLab(agents.ToArray(),null,extent,Segment,Follow()));
        }
        public void Step(){Baseline.Step(LabMode.StopOnly);Steering.Step(LabMode.PredictiveSteering);}
        public string Summary()=>FormattableString.Invariant(
            $"LAB ROUTED {Habitat} requested={Requested} valid={Valid} skipped={Skipped} ticks={Baseline.Tick}: stop arrived={Baseline.Arrived} stopped={Baseline.StoppedAgentTicks} gap={Baseline.MinGap:F4} distance={Baseline.DistanceTravelled:F2}; steer arrived={Steering.Arrived} stopped={Steering.StoppedAgentTicks} gap={Steering.MinGap:F4} distance={Steering.DistanceTravelled:F2}");
    }
}
