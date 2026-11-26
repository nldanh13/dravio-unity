using System;
using System.Collections.Generic;
using System.Linq;
using Dravio.Navigation;

namespace Dravio.TechnologyLab
{
    // Independent visual-only reservation experiment. No Domain commands or save data.
    public sealed class GoalReservationQueue
    {
        sealed class Claim
        {
            public string Uid,Goal;
            public long Since,Expires;
        }
        readonly Dictionary<string,Claim> claims=new Dictionary<string,Claim>(StringComparer.Ordinal);
        readonly Dictionary<string,string> owners=new Dictionary<string,string>(StringComparer.Ordinal);
        long clock=long.MinValue;
        void Advance(long now)
        {
            if(now<clock)throw new ArgumentException("non-monotonic clock");
            clock=now;
            foreach(var c in claims.Values.Where(c=>c.Expires<=now).ToArray())Cancel(c.Uid);
        }
        public void Request(string uid,string goal,long now,long expires)
        {
            Advance(now);
            if(string.IsNullOrEmpty(uid)||string.IsNullOrEmpty(goal)||expires<=now)throw new ArgumentException("claim");
            if(claims.TryGetValue(uid,out var old))
            {
                // Repeated requests neither jump the queue nor prolong a lease.
                if(old.Goal==goal)return;
                Cancel(uid);
            }
            claims[uid]=new Claim{Uid=uid,Goal=goal,Since=now,Expires=expires};
        }
        public void Resolve(long now,Func<string,string,bool> eligible)
        {
            if(eligible==null)throw new ArgumentNullException(nameof(eligible));
            Advance(now);
            foreach(var c in claims.Values.Where(c=>!eligible(c.Uid,c.Goal)).ToArray())Cancel(c.Uid);
            foreach(var c in claims.Values.OrderBy(c=>c.Since).ThenBy(c=>c.Uid,StringComparer.Ordinal))
                if(!owners.ContainsKey(c.Goal))owners[c.Goal]=c.Uid;
        }
        public string Owner(string goal)=>goal!=null && owners.TryGetValue(goal,out var uid)?uid:null;
        public void Cancel(string uid)
        {
            if(uid==null || !claims.TryGetValue(uid,out var c))return;
            if(Owner(c.Goal)==uid)owners.Remove(c.Goal);
            claims.Remove(uid);
        }
        public int Pending=>claims.Count-owners.Count;
    }

    // Detect lack of waypoint progress (not merely standing), then try a bounded reroute
    // around current bodies. Retain original goal and original path on failure.
    public sealed class RecoveryFollower
    {
        sealed class Track
        {
            public LabVec[] Route;
            public int Index,NextCheck,LastProgress;
            public double Best=double.PositiveInfinity;
        }
        readonly Dictionary<int,Track> tracks=new Dictionary<int,Track>();
        readonly Func<NavPoint,bool> geometry;
        readonly float y;
        int budgetTick=-1,remaining;
        public int Attempts { get; private set; }
        public int Recovered { get; private set; }
        public int Failed { get; private set; }
        public int Expanded { get; private set; }
        public RecoveryFollower(IReadOnlyDictionary<int,LabVec[]> routes,float height,Func<NavPoint,bool> clear)
        {
            geometry=clear??throw new ArgumentNullException(nameof(clear));y=height;
            foreach(var pair in routes)
            {
                if(pair.Value==null||pair.Value.Length==0)throw new ArgumentException("empty route");
                tracks.Add(pair.Key,new Track{Route=(LabVec[])pair.Value.Clone(),NextCheck=40});
            }
        }
        NavPoint P(LabVec p)=>new NavPoint((float)p.X,y,(float)p.Y);
        public LabVec Next(LabAgent agent,IReadOnlyList<LabAgent> crowd,int tick)
        {
            if(!tracks.TryGetValue(agent.Id,out var t))return agent.Position;
            if(budgetTick!=tick){budgetTick=tick;remaining=1;}
            while(t.Index<t.Route.Length-1 && (agent.Position-t.Route[t.Index]).Length<=.08)
            {t.Index++;t.Best=double.PositiveInfinity;t.LastProgress=tick;}
            double distance=(agent.Position-t.Route[t.Index]).Length;
            if(distance<t.Best-.04){t.Best=distance;t.LastProgress=tick;}
            if(tick-t.LastProgress<40 || tick<t.NextCheck || remaining==0)return t.Route[t.Index];
            // Do not spend a search while another body occupies the goal. Never move the goal.
            bool Clear(NavPoint p)
            {
                if(!geometry(p))return false;
                var point=new LabVec(p.X,p.Z);
                foreach(var other in crowd)
                    if(other.Id!=agent.Id && (point-other.Position).Length<agent.Radius+other.Radius+.025-1e-6)return false;
                return true;
            }
            t.NextCheck=tick+40;
            if(!Clear(P(agent.Goal)))return t.Route[t.Index];
            remaining--;Attempts++;
            var result=AdaptiveRouteLab.Plan(P(agent.Position),P(agent.Goal),NavigationMedium.Ground,Clear,768);
            Expanded+=result.Expanded;
            if(!result.Success){Failed++;return t.Route[t.Index];}
            t.Route=result.Path.Waypoints.Select(p=>new LabVec(p.X,p.Z)).ToArray();
            t.Index=0;t.Best=double.PositiveInfinity;t.LastProgress=tick;Recovered++;
            return t.Route[0];
        }
    }
}
