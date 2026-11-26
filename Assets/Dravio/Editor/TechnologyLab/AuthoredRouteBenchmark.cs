using System;
using System.Collections.Generic;
using Dravio.Navigation;

namespace Dravio.TechnologyLab
{
    // JSON mirror for headless experiments. Deliberately not a second production geometry authority.
    [Serializable] public sealed class LabLocationMap
    {
        [Serializable] public sealed class Habitat { public string id; public float[] polygonXZ; }
        [Serializable] public sealed class Area { public float[] polygonXZ; public float top; }
        [Serializable] public sealed class Floor { public string habitatId; public float[] polygonXZ; public float y; }
        [Serializable] public sealed class Volume { public string habitatId,medium; public float[] polygonXZ; public float yMin,yMax; }
        [Serializable] public sealed class Spawn
        {
            public string id,habitatId,medium;
            public float x,y,z;
            public NavPoint Point=>new NavPoint(x,y,z);
        }
        public string zoneId;
        public float walkClearance=.45f;
        public Habitat[] habitats=Array.Empty<Habitat>();
        public Area[] obstacles=Array.Empty<Area>(),waterAreas=Array.Empty<Area>();
        public Floor[] walkAreas=Array.Empty<Floor>();
        public Volume[] volumes=Array.Empty<Volume>();
        public Spawn[] spawnPoints=Array.Empty<Spawn>();

        public static bool Contains(float[] poly,float x,float z)
        {
            if(poly==null || poly.Length<6 || poly.Length%2!=0)return false;
            bool inside=false;int n=poly.Length/2;
            for(int i=0,j=n-1;i<n;j=i++)
            {
                float xi=poly[2*i],zi=poly[2*i+1],xj=poly[2*j],zj=poly[2*j+1];
                if((zi>z)!=(zj>z) && x<(xj-xi)*(z-zi)/(zj-zi)+xi)inside=!inside;
            }
            return inside;
        }
        public static double EdgeDistance(float[] poly,float x,float z)
        {
            if(poly==null || poly.Length<4 || poly.Length%2!=0)return double.PositiveInfinity;
            double best=double.PositiveInfinity;
            for(int i=0;i<poly.Length;i+=2)
            {
                int j=(i+2)%poly.Length;
                double dx=poly[j]-poly[i],dz=poly[j+1]-poly[i+1],px=x-poly[i],pz=z-poly[i+1];
                double t=Math.Max(0,Math.Min(1,(px*dx+pz*dz)/Math.Max(dx*dx+dz*dz,1e-6)));
                best=Math.Min(best,Math.Sqrt((px-t*dx)*(px-t*dx)+(pz-t*dz)*(pz-t*dz)));
            }
            return best;
        }
        bool Blocked(NavPoint p,float clearance,bool spatial)
        {
            foreach(var o in obstacles)
            {
                if(spatial && o.top>0 && o.top<p.Y-.05f)continue;
                if(Contains(o.polygonXZ,p.X,p.Z)||EdgeDistance(o.polygonXZ,p.X,p.Z)<clearance)return true;
            }
            return false;
        }
        public bool Clear(NavPoint p,string habitat,NavigationMedium medium,float clearance=.25f)
        {
            if(!p.Finite || string.IsNullOrEmpty(habitat) || float.IsNaN(clearance)||float.IsInfinity(clearance)||clearance<0)return false;
            if(medium==NavigationMedium.Ground)
            {
                string at=null;
                foreach(var h in habitats)if(Contains(h.polygonXZ,p.X,p.Z)){at=h.id;break;}
                if(at!=habitat || Blocked(p,walkClearance,false))return false;
                foreach(var w in waterAreas)if(Contains(w.polygonXZ,p.X,p.Z))return false;
                float y=0,best=float.PositiveInfinity;
                if(walkAreas.Length>0)
                {
                    foreach(var f in walkAreas)
                        if(f.habitatId==habitat && Contains(f.polygonXZ,p.X,p.Z) && Math.Abs(f.y-p.Y)<best)
                        {y=f.y;best=Math.Abs(f.y-p.Y);}
                    if(float.IsInfinity(best))return false;
                }
                return Math.Abs(p.Y-y)<=.02f;
            }
            if(medium!=NavigationMedium.Swim && medium!=NavigationMedium.Air)return false;
            string mode=medium==NavigationMedium.Swim?"swim":"fly";
            foreach(var v in volumes)
            {
                if(v.habitatId!=habitat || v.medium!=mode || p.Y-clearance<v.yMin || p.Y+clearance>v.yMax ||
                    !Contains(v.polygonXZ,p.X,p.Z) || EdgeDistance(v.polygonXZ,p.X,p.Z)<clearance)continue;
                if(!Blocked(new NavPoint(p.X,p.Y-clearance,p.Z),clearance,true))return true;
            }
            return false;
        }
        public bool ClearReviewEnvelope(NavPoint p,string habitat,NavigationMedium medium,MediumReviewEnvelope envelope)
        {
            if(!p.Finite||!envelope.Valid||(medium!=NavigationMedium.Swim&&medium!=NavigationMedium.Air))return false;
            string mode=medium==NavigationMedium.Swim?"swim":"fly";
            foreach(var v in volumes)
            {
                if(v.habitatId!=habitat||v.medium!=mode||p.Y-envelope.Vertical<v.yMin||p.Y+envelope.Vertical>v.yMax||
                    !Contains(v.polygonXZ,p.X,p.Z)||EdgeDistance(v.polygonXZ,p.X,p.Z)<envelope.Horizontal)continue;
                if(!Blocked(new NavPoint(p.X,p.Y-envelope.Vertical,p.Z),envelope.Horizontal,true))return true;
            }
            return false;
        }
        public static bool Matches(Spawn s,NavigationMedium m)=>m==NavigationMedium.Ground?
            string.IsNullOrEmpty(s.medium)||s.medium=="ground":m==NavigationMedium.Swim?s.medium=="swim":
            m==NavigationMedium.Air&&s.medium=="air";
        public IEnumerable<Spawn> Candidates(string habitat,NavigationMedium medium,float clearance=.25f)
        {
            foreach(var s in spawnPoints)
                if(s.habitatId==habitat && Matches(s,medium) && Clear(s.Point,habitat,medium,clearance))yield return s;
        }
    }
    public sealed class RouteBenchmarkRow
    {
        public string Zone,Habitat;
        public NavigationMedium Medium;
        public float Step,Clearance;
        public int Spawns,Pairs,Direct,Detours,Invalid,Unreachable,Budget,Unsafe,MaxExpanded;
        public double TotalLength,ElapsedMs;
    }
    public static class AuthoredRouteBenchmark
    {
        public static RouteBenchmarkRow Run(LabLocationMap map,string habitat,NavigationMedium medium,float step=.45f,float clearance=.25f,int budget=2048)
        {
            var row=new RouteBenchmarkRow{Zone=map.zoneId,Habitat=habitat,Medium=medium,Step=step,Clearance=clearance};
            var points=new List<LabLocationMap.Spawn>(map.Candidates(habitat,medium,clearance));row.Spawns=points.Count;
            bool Clear(NavPoint p)=>map.Clear(p,habitat,medium,clearance);
            var watch=System.Diagnostics.Stopwatch.StartNew();
            for(int i=0;i<points.Count;i++)
            for(int j=0;j<points.Count;j++)
            {
                if(i==j)continue;row.Pairs++;
                var a=points[i].Point;var goal=points[j].Point;
                var path=SanctuaryRoutePlanner.Plan(a,goal,medium,Clear,step,budget);
                row.MaxExpanded=Math.Max(row.MaxExpanded,path.Expanded);
                if(path.Success)
                {
                    if(path.Status==RouteStatus.Direct)row.Direct++;else row.Detours++;
                    var previous=a;
                    foreach(var p in path.Waypoints)
                    {
                        // Independent denser check: detect sampled geometry misses.
                        if(!SanctuaryRoutePlanner.SegmentClear(previous,p,Clear,.025f))row.Unsafe++;
                        row.TotalLength+=NavPoint.Distance(previous,p);previous=p;
                    }
                    if(NavPoint.Distance(previous,goal)>.0001)row.Unsafe++;
                }
                else if(path.Status==RouteStatus.InvalidEndpoint)row.Invalid++;
                else if(path.Status==RouteStatus.BudgetExceeded)row.Budget++;
                else row.Unreachable++;
            }
            watch.Stop();row.ElapsedMs=watch.Elapsed.TotalMilliseconds;return row;
        }
        public static string Describe(RouteBenchmarkRow r)=>FormattableString.Invariant(
            $"{r.Zone}/{r.Habitat} {r.Medium} step={r.Step:F2} margin={r.Clearance:F2}: spawns={r.Spawns} pairs={r.Pairs} direct={r.Direct} detours={r.Detours} invalid={r.Invalid} unreachable={r.Unreachable} budget={r.Budget} unsafeEdges={r.Unsafe} maxNodes={r.MaxExpanded} length={r.TotalLength:F2}m cpu={r.ElapsedMs:F2}ms");
    }
}
