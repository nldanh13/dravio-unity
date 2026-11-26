using System;
using System.Collections.Generic;

namespace Dravio.Navigation
{
    public enum NavigationMedium { Ground, Swim, Air, Burrow }
    public enum RouteStatus { Direct, Found, InvalidEndpoint, Unreachable, BudgetExceeded, UnsupportedMedium }
    public readonly struct NavPoint
    {
        public readonly float X,Y,Z;
        public NavPoint(float x,float y,float z){X=x;Y=y;Z=z;}
        public static NavPoint Lerp(NavPoint a,NavPoint b,float t)=>new NavPoint(a.X+(b.X-a.X)*t,a.Y+(b.Y-a.Y)*t,a.Z+(b.Z-a.Z)*t);
        public static double Distance(NavPoint a,NavPoint b)
        {double x=(double)a.X-b.X,y=(double)a.Y-b.Y,z=(double)a.Z-b.Z;return Math.Sqrt(x*x+y*y+z*z);}
        public bool Finite=>!float.IsNaN(X)&&!float.IsInfinity(X)&&!float.IsNaN(Y)&&!float.IsInfinity(Y)&&!float.IsNaN(Z)&&!float.IsInfinity(Z);
    }
    public sealed class RouteResult
    {
        public RouteStatus Status { get; }
        public IReadOnlyList<NavPoint> Waypoints { get; }
        public int Expanded { get; }
        public bool Success=>Status==RouteStatus.Direct || Status==RouteStatus.Found;
        internal RouteResult(RouteStatus status,IReadOnlyList<NavPoint> points,int expanded)
        {Status=status;Waypoints=points;Expanded=expanded;}
    }

    /// <summary>Bounded deterministic A* on a start-relative lattice; pure, no scene or game state.
    /// Every edge and final goal connection is checked by the supplied geometry predicate.
    /// No partial route or unchecked straight-line fallback on failure.</summary>
    public static class SanctuaryRoutePlanner
    {
        readonly struct Cell : IEquatable<Cell>
        {
            public readonly int X,Y,Z;
            public Cell(int x,int y,int z){X=x;Y=y;Z=z;}
            public bool Equals(Cell c)=>X==c.X && Y==c.Y && Z==c.Z;
            public override bool Equals(object o)=>o is Cell c && Equals(c);
            public override int GetHashCode()=>unchecked((X*73856093)^(Y*19349663)^(Z*83492791));
        }
        sealed class Node
        {
            public Cell cell,parent;
            public double cost,score;
            public bool hasParent,closed;
        }
        // Binary heap; stale improved entries are ignored by the search.
        sealed class Open
        {
            readonly List<(Cell cell,double score,double cost,long order)> heap=new List<(Cell,double,double,long)>();
            long order;
            static bool Less((Cell cell,double score,double cost,long order) a,(Cell cell,double score,double cost,long order) b)
                =>a.score<b.score || (a.score==b.score && a.order<b.order);
            public int Count=>heap.Count;
            public void Push(Cell c,double score,double cost)
            {
                var v=(c,score,cost,order++);int i=heap.Count;heap.Add(v);
                while(i>0){int p=(i-1)/2;if(!Less(v,heap[p]))break;heap[i]=heap[p];i=p;}heap[i]=v;
            }
            public (Cell cell,double score,double cost,long order) Pop()
            {
                var result=heap[0];var last=heap[heap.Count-1];heap.RemoveAt(heap.Count-1);
                if(heap.Count==0)return result;
                int i=0;while(i*2+1<heap.Count){int c=i*2+1;if(c+1<heap.Count && Less(heap[c+1],heap[c]))c++;
                    if(!Less(heap[c],last))break;heap[i]=heap[c];i=c;}heap[i]=last;return result;
            }
        }
        static RouteResult Fail(RouteStatus s,int count=0)=>new RouteResult(s,Array.Empty<NavPoint>(),count);

        public static bool SegmentClear(NavPoint a,NavPoint b,Func<NavPoint,bool> clear,float spacing=.1f)
        {
            if(clear==null || !a.Finite || !b.Finite || !(spacing>0f) || float.IsInfinity(spacing))return false;
            double distance=NavPoint.Distance(a,b);
            if(distance/spacing>4096)return false;
            int n=Math.Max(1,(int)Math.Ceiling(distance/spacing));
            for(int i=0;i<=n;i++)if(!clear(NavPoint.Lerp(a,b,(float)i/n)))return false;
            return true;
        }
        static IReadOnlyList<NavPoint> Smooth(NavPoint start,List<NavPoint> path,Func<NavPoint,bool> clear)
        {
            var result=new List<NavPoint>();var previous=start;int i=0;
            while(i<path.Count)
            {
                int chosen=i;
                for(int j=Math.Min(i+4,path.Count-1);j>i;j--)
                    if(SegmentClear(previous,path[j],clear)){chosen=j;break;}
                result.Add(path[chosen]);previous=path[chosen];i=chosen+1;
            }
            return result;
        }
        public static RouteResult Plan(NavPoint start,NavPoint goal,NavigationMedium medium,
            Func<NavPoint,bool> clear, float step=.45f,int maxExpanded=768)
        {
            if(medium==NavigationMedium.Burrow || (medium!=NavigationMedium.Ground && medium!=NavigationMedium.Swim && medium!=NavigationMedium.Air))
                return Fail(RouteStatus.UnsupportedMedium);
            if(clear==null || !start.Finite || !goal.Finite || !(step>=.1f) || step>2f || maxExpanded<1 || maxExpanded>8192 ||
                (medium==NavigationMedium.Ground && Math.Abs(start.Y-goal.Y)>.02f) || !clear(start) || !clear(goal))
                return Fail(RouteStatus.InvalidEndpoint);
            if(SegmentClear(start,goal,clear))return new RouteResult(RouteStatus.Direct,new[]{goal},0);
            var root=new Cell(0,0,0);var nodes=new Dictionary<Cell,Node>();
            var open=new Open();double h=NavPoint.Distance(start,goal);
            nodes[root]=new Node{cell=root,score=h};open.Push(root,h,0);int expanded=0;
            bool spatial=medium!=NavigationMedium.Ground;
            NavPoint At(Cell c)=>new NavPoint(start.X+c.X*step,start.Y+c.Y*step,start.Z+c.Z*step);
            while(open.Count>0)
            {
                var next=open.Pop();var node=nodes[next.cell];
                if(node.closed || next.cost!=node.cost)continue;
                if(expanded>=maxExpanded)return Fail(RouteStatus.BudgetExceeded,expanded);
                node.closed=true;expanded++;
                NavPoint from=At(node.cell);
                if(NavPoint.Distance(from,goal)<=step*1.8 && SegmentClear(from,goal,clear))
                {
                    var path=new List<NavPoint>{goal};var cursor=node;
                    while(cursor.hasParent){path.Add(At(cursor.cell));cursor=nodes[cursor.parent];}
                    path.Reverse();
                    // Short checked shortcuts reduce stair-step turns without unbounded all-pairs work.
                    return new RouteResult(RouteStatus.Found,Smooth(start,path,clear),expanded);
                }
                // Cardinal neighbors only: no diagonal corner cutting. Full edge checked at <= 0.1m.
                for(int axis=0;axis<(spatial?3:2);axis++)
                for(int sign=-1;sign<=1;sign+=2)
                {
                    var c=new Cell(node.cell.X+(axis==0?sign:0),node.cell.Y+(axis==2?sign:0),node.cell.Z+(axis==1?sign:0));
                    var at=At(c);double g=node.cost+step;
                    if(nodes.TryGetValue(c,out var known) && (known.closed || known.cost<=g))continue;
                    if(!SegmentClear(from,at,clear))continue;
                    var item=known??new Node{cell=c};
                    item.cost=g;item.score=g+NavPoint.Distance(at,goal);item.parent=node.cell;item.hasParent=true;
                    nodes[c]=item;open.Push(c,item.score,g);
                }
            }
            return Fail(RouteStatus.Unreachable,expanded);
        }
    }
}
