using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dravio.TechnologyLab
{
    // Observed geometry, not a contact curve or a proof of skin/mesh collision.
    public readonly struct RigFootSample
    {
        public readonly double X,Z,GroundY,FootY;
        public readonly bool Hit;
        public RigFootSample(double x,double z,double groundY,double footY,bool hit)
        {X=x;Z=z;GroundY=groundY;FootY=footY;Hit=hit;}
        static bool Finite(double x)=>!double.IsNaN(x)&&!double.IsInfinity(x);
        public bool Valid=>Hit&&Finite(X)&&Finite(Z)&&Finite(GroundY)&&Finite(FootY);
        public double Gap=>FootY-GroundY;
    }
    public readonly struct RigContactFrame
    {
        public readonly double Time;
        public readonly int Feet,Hits,NearGround,Penetrations;
        public readonly double MaxPenetration,MeanNearGroundAbsGap;
        public readonly ContactPose Candidate;
        public RigContactFrame(double time,IReadOnlyList<RigFootSample> samples,double near=.04,double tolerance=.02)
        {
            if(samples==null||!(near>=0)||double.IsInfinity(near)||!(tolerance>=0)||double.IsInfinity(tolerance))
                throw new ArgumentException("contact samples");
            Time=time;Feet=samples.Count;Hits=NearGround=Penetrations=0;
            MaxPenetration=MeanNearGroundAbsGap=0;
            var plane=new List<ContactSample>();
            double sum=0;
            foreach(var s in samples)
            {
                if(!s.Valid)continue;
                Hits++;double gap=s.Gap;
                MaxPenetration=Math.Max(MaxPenetration,Math.Max(0,-gap));
                if(gap < -tolerance)Penetrations++;
                if(Math.Abs(gap)<=near)
                {
                    NearGround++;sum+=Math.Abs(gap);
                    plane.Add(new ContactSample(s.X,s.Z,s.GroundY));
                }
            }
            MeanNearGroundAbsGap=NearGround>0?sum/NearGround:-1;
            // Exclude distant/swing feet; cannot infer a support plane with fewer than 3 contacts.
            Candidate=ContactLab.Solve(plane);
        }
    }
    public sealed class RigContactCapture
    {
        readonly List<RigContactFrame> frames;
        readonly int capacity;
        public int Count=>frames.Count;
        public bool Full=>Count>=capacity;
        public RigContactCapture(int capacity=600)
        {
            if(capacity<1||capacity>3600)throw new ArgumentOutOfRangeException(nameof(capacity));
            this.capacity=capacity;frames=new List<RigContactFrame>(capacity);
        }
        public bool Add(RigContactFrame sample)
        {
            if(Full)return false;
            frames.Add(sample);return true;
        }
        public string Summary()
        {
            if(Count==0)return "No contact samples";
            int feet=frames.Sum(f=>f.Feet),hits=frames.Sum(f=>f.Hits),near=frames.Sum(f=>f.NearGround);
            double gap=near>0?frames.Where(f=>f.NearGround>0).Sum(f=>f.MeanNearGroundAbsGap*f.NearGround)/near:-1;
            return FormattableString.Invariant($"TL.12 frames={Count} footSamples={feet} hits={hits} missing={feet-hits} nearGround={near} penetrationSamples={frames.Sum(f=>f.Penetrations)} maxPenetrationM={frames.Max(f=>f.MaxPenetration):F4} nearAbsGapM={gap:F4} candidateFrames={frames.Count(f=>f.Candidate.Valid)}");
        }
        public string Csv(string label=null)
        {
            var b=new StringBuilder();
            string prefix=label==null?"":"\""+label.Replace("\"","\"\"").Replace("\r"," ").Replace("\n"," ")+"\",";
            if(label!=null)b.Append("capture_label,");
            b.Append("sample,clip_seconds,feet,hits,near_ground,penetrations,max_penetration_m,mean_near_ground_abs_gap_m,candidate_valid,candidate_pitch_deg,candidate_roll_deg,candidate_offset_m\n");
            for(int i=0;i<Count;i++)
            {
                var f=frames[i];var p=f.Candidate;
                b.Append(prefix);
                b.AppendLine(FormattableString.Invariant($"{i},{f.Time:R},{f.Feet},{f.Hits},{f.NearGround},{f.Penetrations},{f.MaxPenetration:R},{f.MeanNearGroundAbsGap:R},{(p.Valid?1:0)},{p.Pitch:R},{p.Roll:R},{p.BodyOffset:R}"));
            }
            return b.ToString();
        }
    }
}
