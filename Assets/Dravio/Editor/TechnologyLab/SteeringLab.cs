using System;
using System.Collections.Generic;

namespace Dravio.TechnologyLab
{
    // Original, isolated research prototype. No Unity, Domain, saved state or third-party code.
    public readonly struct LabVec
    {
        public readonly double X,Y;
        public LabVec(double x,double y){X=x;Y=y;}
        public static LabVec operator +(LabVec a,LabVec b)=>new LabVec(a.X+b.X,a.Y+b.Y);
        public static LabVec operator -(LabVec a,LabVec b)=>new LabVec(a.X-b.X,a.Y-b.Y);
        public static LabVec operator *(LabVec a,double k)=>new LabVec(a.X*k,a.Y*k);
        public double LengthSquared=>X*X+Y*Y;
        public double Length=>Math.Sqrt(LengthSquared);
        public LabVec Unit=>Length>1e-9?this*(1/Length):new LabVec();
        public bool Finite=>!double.IsNaN(X)&&!double.IsInfinity(X)&&!double.IsNaN(Y)&&!double.IsInfinity(Y);
        public static double Dot(LabVec a,LabVec b)=>a.X*b.X+a.Y*b.Y;
        public static LabVec Rotate(LabVec v,double radians)
        {double c=Math.Cos(radians),s=Math.Sin(radians);return new LabVec(v.X*c-v.Y*s,v.X*s+v.Y*c);}
    }
    public enum LabMode { StopOnly, PredictiveSteering }
    public enum LabScenario { HeadOn, Crossing, Obstacle, MixedGroups }
    public sealed class LabAgent
    {
        public readonly int Id,Group;
        public readonly double Radius,Speed;
        public LabVec Position { get; internal set; }
        public LabVec Velocity { get; internal set; }
        public LabVec Goal { get; }
        public bool Arrived=> (Goal-Position).Length<=.12;
        public LabAgent(int id,LabVec position,LabVec goal,double radius=.24,double speed=1,int group=-1)
        {Id=id;Position=position;Goal=goal;Radius=radius;Speed=speed;Group=group;}
    }
    public readonly struct LabObstacle
    {
        public readonly LabVec Center;
        public readonly double Radius;
        public LabObstacle(LabVec center,double radius){Center=center;Radius=radius;}
    }
    public sealed class SteeringLab
    {
        const double Epsilon=1e-8;
        const double Margin=.025;
        const double Horizon=.8;
        const double RiskGap=.25;
        static readonly double[] Angles={0,-25,25,-50,50,-75,75,-100,100,-135,135,180};
        static readonly double[] Speeds={1,.55};
        readonly LabAgent[] agents;
        readonly LabObstacle[] obstacles;
        readonly LabVec[] planned,desired;
        readonly double halfExtent;
        readonly Func<LabVec,LabVec,bool> geometry;
        readonly Func<LabAgent,LabVec> waypoint;
        readonly LabVec[] targets;
        readonly bool broadPhase;
        readonly List<int>[] neighbors;
        public long SweptPairChecks { get; private set; }
        public IReadOnlyList<LabAgent> Agents=>agents;
        public IReadOnlyList<LabObstacle> Obstacles=>obstacles;
        public double HalfExtent=>halfExtent;
        public int Tick { get; private set; }
        public int Arrived { get; private set; }
        public long StoppedAgentTicks { get; private set; }
        public long MovingAgentTicks { get; private set; }
        public long SafetyRejects { get; private set; }
        public double MinGap { get; private set; }=double.PositiveInfinity;
        public double DistanceTravelled { get; private set; }
        public SteeringLab(LabAgent[] input,LabObstacle[] obstacles,double halfExtent,
            Func<LabVec,LabVec,bool> geometry=null,Func<LabAgent,LabVec> waypoint=null,bool broadPhase=false)
        {
            if(input==null || input.Length==0 || !(halfExtent>0) || !Finite(halfExtent))throw new ArgumentException("arena");
            this.halfExtent=halfExtent;this.geometry=geometry;this.waypoint=waypoint;this.broadPhase=broadPhase;
            this.obstacles=obstacles==null?Array.Empty<LabObstacle>():(LabObstacle[])obstacles.Clone();
            foreach(var o in this.obstacles)
                if(!o.Center.Finite || !(o.Radius>0) || !Finite(o.Radius))throw new ArgumentException("obstacle");
            agents=new LabAgent[input.Length];
            var ids=new HashSet<int>();
            for(int i=0;i<input.Length;i++)
            {
                var a=input[i];
                if(a==null || !ids.Add(a.Id) || !a.Position.Finite || !a.Goal.Finite ||
                    !(a.Radius>0) || !(a.Speed>0) || !Finite(a.Radius) || !Finite(a.Speed))
                    throw new ArgumentException("agent");
                agents[i]=new LabAgent(a.Id,a.Position,a.Goal,a.Radius,a.Speed,a.Group);
                if(!WorldClear(a.Position,a.Position,a.Radius) || !WorldClear(a.Goal,a.Goal,a.Radius))
                    throw new ArgumentException("agent endpoint");
            }
            Array.Sort(agents,(a,b)=>a.Id.CompareTo(b.Id));
            for(int i=0;i<agents.Length;i++)
                for(int j=0;j<i;j++)
                    if((agents[i].Position-agents[j].Position).Length+Epsilon<agents[i].Radius+agents[j].Radius+Margin)
                        throw new ArgumentException("initial overlap");
            planned=new LabVec[agents.Length];desired=new LabVec[agents.Length];targets=new LabVec[agents.Length];
            neighbors=new List<int>[agents.Length];
            for(int i=0;i<agents.Length;i++)neighbors[i]=new List<int>(agents.Length-1);
            UpdateMetrics();
        }
        static bool Finite(double x)=>!double.IsNaN(x)&&!double.IsInfinity(x);
        // Exact minimum relative separation for two linear moves over t=[0,1].
        public static double SweptDistance(LabVec a,LabVec aEnd,LabVec b,LabVec bEnd)
        {
            var d=a-b;var v=(aEnd-a)-(bEnd-b);
            double t=v.LengthSquared>Epsilon?Math.Max(0,Math.Min(1,-LabVec.Dot(d,v)/v.LengthSquared)):0;
            return (d+v*t).Length;
        }
        bool WorldClear(LabVec start,LabVec end,double radius)
        {
            if(geometry!=null && !geometry(start,end))return false;
            double limit=halfExtent-radius-Margin;
            if(Math.Abs(start.X)>limit || Math.Abs(start.Y)>limit || Math.Abs(end.X)>limit || Math.Abs(end.Y)>limit)return false;
            foreach(var o in obstacles)
                if(SweptDistance(start,end,o.Center,o.Center)+Epsilon<radius+o.Radius+Margin)return false;
            return true;
        }
        // Conservative broad phase: relative travel over the entire prediction horizon
        // cannot close more than (speed A + speed B) * horizon. Preserve index order.
        void BuildNeighbors()
        {
            for(int i=0;i<agents.Length;i++)neighbors[i].Clear();
            for(int i=0;i<agents.Length;i++)
            for(int j=i+1;j<agents.Length;j++)
            {
                var a=agents[i];var b=agents[j];
                double limit=a.Radius+b.Radius+Margin+RiskGap+(a.Speed+b.Speed)*Horizon+1e-6;
                if(broadPhase && (a.Position-b.Position).LengthSquared>limit*limit)continue;
                neighbors[i].Add(j);neighbors[j].Add(i);
            }
        }
        bool Safe(int i,LabVec end)
        {
            var a=agents[i];
            if(!WorldClear(a.Position,end,a.Radius))return false;
            foreach(int j in neighbors[i])
            {
                SweptPairChecks++;
                if(SweptDistance(a.Position,end,agents[j].Position,planned[j])+Epsilon<
                    a.Radius+agents[j].Radius+Margin)return false;
            }
            return true;
        }
        LabVec Preferred(int i,double flock)
        {
            var a=agents[i];var seek=(targets[i]-a.Position).Unit;
            if(flock<=0 || a.Group<0)return seek;
            var align=new LabVec();var center=new LabVec();int count=0;
            for(int j=0;j<agents.Length;j++)
            {
                var other=agents[j];
                if(j==i || other.Group!=a.Group || other.Arrived || (other.Position-a.Position).Length>2.4)continue;
                align+=other.Velocity.Unit;center+=other.Position-a.Position;count++;
            }
            if(count==0)return seek;
            return (seek+(align.Unit*.3+center.Unit*.2)*flock).Unit;
        }
        double Score(int i,LabVec velocity,double dt,LabVec preferred)
        {
            var a=agents[i];var goal=targets[i]-a.Position;
            // A short predictive horizon biases choices away from a future conflict.
            // This is sampled steering, not ORCA, and the prediction itself is not a safety proof.
            double horizon=Horizon;
            double score=(goal.Length-(goal-velocity*dt).Length)/dt*2+
                LabVec.Dot(velocity,preferred)*.30;
            foreach(int j in neighbors[i])
            {
                SweptPairChecks++;
                var other=agents[j];
                double gap=SweptDistance(a.Position,a.Position+velocity*horizon,
                    other.Position,other.Position+desired[j]*horizon)-a.Radius-other.Radius-Margin;
                if(gap<RiskGap)score-=Math.Max(0,RiskGap-gap)*5;
            }
            // Anticipate static obstacles too. Symmetric angle ties keep a consistent passing side.
            foreach(var o in obstacles)
            {
                double gap=SweptDistance(a.Position,a.Position+velocity*horizon,o.Center,o.Center)-a.Radius-o.Radius-Margin;
                if(gap<RiskGap)score-=Math.Max(0,RiskGap-gap)*5;
            }
            return score;
        }
        public void Step(LabMode mode,double dt=.05,double flock=0)
        {
            if(!(dt>0) || dt>.1 || !Finite(dt) || !Finite(flock) || flock<0 || flock>1 ||
                (mode!=LabMode.StopOnly && mode!=LabMode.PredictiveSteering))throw new ArgumentException("step");
            for(int i=0;i<agents.Length;i++)
            {
                var a=agents[i];
                planned[i]=a.Position;
                targets[i]=a.Arrived?a.Position:(waypoint==null?a.Goal:waypoint(a));
                if(!targets[i].Finite)targets[i]=a.Position;
                desired[i]=a.Arrived?new LabVec():(targets[i]-a.Position).Unit*Math.Min(a.Speed,(targets[i]-a.Position).Length/dt);
            }
            BuildNeighbors();
            // Rotate priority every tick. Already selected trajectories and all remaining stationary
            // bodies are reserved. Replacing a stationary trajectory only when all swept pairs are
            // safe preserves the invariant inductively; all positions commit together.
            for(int k=0;k<agents.Length;k++)
            {
                int i=(Tick%agents.Length+k)%agents.Length;var a=agents[i];if(a.Arrived)continue;
                if(mode==LabMode.StopOnly)
                {
                    var end=a.Position+desired[i]*dt;
                    if(Safe(i,end))planned[i]=end;else SafetyRejects++;
                    continue;
                }
                var preferred=Preferred(i,flock);
                double best=double.NegativeInfinity;
                foreach(double angle in Angles)
                foreach(double scale in Speeds)
                {
                    var v=LabVec.Rotate(preferred,angle*Math.PI/180)*Math.Min(a.Speed*scale,(targets[i]-a.Position).Length/dt);
                    var end=a.Position+v*dt;
                    if(!Safe(i,end)){SafetyRejects++;continue;}
                    double score=Score(i,v,dt,preferred);
                    if(score>best+Epsilon){best=score;planned[i]=end;}
                }
                // An alternative may briefly move away from the goal to pass another agent.
                // If every sample is unsafe, the existing stationary reservation remains.
            }
            for(int i=0;i<agents.Length;i++)
            {
                var a=agents[i];var delta=planned[i]-a.Position;
                if(!a.Arrived){if(delta.Length<1e-7)StoppedAgentTicks++;else MovingAgentTicks++;}
                DistanceTravelled+=delta.Length;a.Velocity=delta*(1/dt);a.Position=planned[i];
            }
            Tick++;UpdateMetrics();
        }
        void UpdateMetrics()
        {
            Arrived=0;
            for(int i=0;i<agents.Length;i++)
            {
                if(agents[i].Arrived)Arrived++;
                for(int j=0;j<i;j++)
                    MinGap=Math.Min(MinGap,(agents[i].Position-agents[j].Position).Length-agents[i].Radius-agents[j].Radius);
                foreach(var o in obstacles)
                    MinGap=Math.Min(MinGap,(agents[i].Position-o.Center).Length-agents[i].Radius-o.Radius);
            }
        }
        public static SteeringLab Create(LabScenario scenario,int count=5,bool broadPhase=false)
        {
            if(scenario==LabScenario.HeadOn)
                return new SteeringLab(new[]{new LabAgent(0,new LabVec(-3,0),new LabVec(3,0)),new LabAgent(1,new LabVec(3,0),new LabVec(-3,0))},null,6,broadPhase:broadPhase);
            if(count<2 || count>30)throw new ArgumentException("count");
            if(scenario!=LabScenario.Crossing && scenario!=LabScenario.Obstacle && scenario!=LabScenario.MixedGroups)
                throw new ArgumentException("scenario");
            var agents=new LabAgent[count];
            for(int i=0;i<count;i++)
            {
                double a=2*Math.PI*i/count;
                var p=new LabVec(Math.Cos(a)*4.5,Math.Sin(a)*4.5);
                int group=scenario==LabScenario.MixedGroups?i%2:-1;
                agents[i]=new LabAgent(i,p,p*(-1),.24,1,group);
            }
            return new SteeringLab(agents,scenario==LabScenario.Obstacle?
                new[]{new LabObstacle(new LabVec(0,0),1.15)}:null,6,broadPhase:broadPhase);
        }
    }
}
