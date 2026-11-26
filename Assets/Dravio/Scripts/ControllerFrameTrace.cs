#if UNITY_EDITOR || DRAVIO_DOTNET_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dravio
{
    public readonly struct ControllerFrameSample
    {
        public readonly double Dt,MinCenterGap,MotionMs,SyncMs;
        public readonly int Residents,Moving,Attempts,Arrivals,Blocked,Plans,Failures,Substitutions;
        public ControllerFrameSample(double dt,int residents,int moving,int attempts,int arrivals,int blocked,
            int plans,int failures,int substitutions,double gap,double motionMs,double syncMs)
        {
            Dt=dt;Residents=residents;Moving=moving;Attempts=attempts;Arrivals=arrivals;Blocked=blocked;
            Plans=plans;Failures=failures;Substitutions=substitutions;MinCenterGap=gap;MotionMs=motionMs;SyncMs=syncMs;
        }
    }
    // Editor-only bounded buffer. No UID, save state, user data or external IO.
    public sealed class ControllerFrameTrace
    {
        readonly List<ControllerFrameSample> frames;
        readonly int capacity;
        bool stopped;
        public int Count=>frames.Count;
        public bool Recording=>!stopped && Count<capacity;
        public ControllerFrameTrace(int capacity)
        {
            if(capacity<1 || capacity>10000)throw new ArgumentOutOfRangeException(nameof(capacity));
            this.capacity=capacity;frames=new List<ControllerFrameSample>(capacity);
        }
        public void Add(ControllerFrameSample frame){if(Recording)frames.Add(frame);}
        public void Stop()=>stopped=true;
        static double P95(IEnumerable<double> values)
        {
            var a=values.OrderBy(x=>x).ToArray();
            return a.Length==0?0:a[Math.Max(0,(int)Math.Ceiling(a.Length*.95)-1)];
        }
        public string Summary()
        {
            if(Count==0)return "TL.10 controller trace: empty";
            double gap=frames.Where(x=>x.MinCenterGap>=0).Select(x=>x.MinCenterGap).DefaultIfEmpty(-1).Min();
            return FormattableString.Invariant($"TL.10 controller trace: frames={Count} simulatedSeconds={frames.Sum(x=>x.Dt):F2} maxResidents={frames.Max(x=>x.Residents)} motionAttempts={frames.Sum(x=>x.Attempts)} arrivalCalls={frames.Sum(x=>x.Arrivals)} dynamicBlocks={frames.Sum(x=>x.Blocked)} routePlans={frames.Sum(x=>x.Plans)} routeFailures={frames.Sum(x=>x.Failures)} substitutions={frames.Sum(x=>x.Substitutions)} minSampledCenterGap={gap:F4} motionP95ms={P95(frames.Select(x=>x.MotionMs)):F3} syncP95ms={P95(frames.Select(x=>x.SyncMs)):F3}");
        }
        public string Csv()
        {
            var b=new StringBuilder("frame,dt,residents,moving,motion_attempts,arrival_calls,dynamic_blocks,route_plans,route_failures,substitutions,min_sampled_center_gap,motion_ms,sync_ms\n");
            for(int i=0;i<frames.Count;i++)
            {
                var x=frames[i];
                b.AppendLine(FormattableString.Invariant($"{i},{x.Dt:R},{x.Residents},{x.Moving},{x.Attempts},{x.Arrivals},{x.Blocked},{x.Plans},{x.Failures},{x.Substitutions},{x.MinCenterGap:R},{x.MotionMs:R},{x.SyncMs:R}"));
            }
            return b.ToString();
        }
    }
}
#endif
