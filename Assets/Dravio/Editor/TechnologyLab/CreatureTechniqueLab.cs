using System;
using System.Collections.Generic;
using System.Linq;

namespace Dravio.TechnologyLab
{
    // TL.4 mathematical contact prototype. No skeleton, raycast, real anatomy or rig mutation.
    public readonly struct ContactSample
    {
        public readonly double X,Z,Height;
        public readonly bool Valid;
        public ContactSample(double x,double z,double height,bool valid=true){X=x;Z=z;Height=height;Valid=valid;}
    }
    public readonly struct ContactPose
    {
        public readonly bool Valid;
        public readonly double Pitch,Roll,BodyOffset;
        public ContactPose(bool valid,double pitch=0,double roll=0,double offset=0)
        {Valid=valid;Pitch=pitch;Roll=roll;BodyOffset=offset;}
    }
    public static class ContactLab
    {
        static bool Finite(double x)=>!double.IsNaN(x)&&!double.IsInfinity(x);
        static double Clamp(double x,double limit)=>Math.Max(-limit,Math.Min(limit,x));
        // Least squares plane y = a*x + b*z + c; requires 3 non-collinear contacts.
        public static ContactPose Solve(IEnumerable<ContactSample> input,double maxTilt=18,double maxOffset=.12)
        {
            if(input==null || !Finite(maxTilt)||!Finite(maxOffset)||maxTilt<0||maxOffset<0)return new ContactPose(false);
            var p=input.Where(x=>x.Valid && Finite(x.X)&&Finite(x.Z)&&Finite(x.Height)).ToArray();
            if(p.Length<3)return new ContactPose(false);
            double x0=p.Average(x=>x.X),z0=p.Average(x=>x.Z),y0=p.Average(x=>x.Height);
            double xx=0,zz=0,xz=0,xy=0,zy=0;
            foreach(var q in p){double x=q.X-x0,z=q.Z-z0,y=q.Height-y0;xx+=x*x;zz+=z*z;xz+=x*z;xy+=x*y;zy+=z*y;}
            double det=xx*zz-xz*xz;
            if(det<=1e-9 || !Finite(det))return new ContactPose(false);
            double a=(xy*zz-zy*xz)/det,b=(zy*xx-xy*xz)/det,c=y0-a*x0-b*z0;
            if(!Finite(a)||!Finite(b)||!Finite(c))return new ContactPose(false);
            double pitch=Math.Atan(b)*180/Math.PI,roll=Math.Atan(a)*180/Math.PI;
            // Clamp combined tilt, not two independent angles that could exceed the limit.
            double tilt=Math.Sqrt(pitch*pitch+roll*roll);
            if(tilt>maxTilt && tilt>0){pitch*=maxTilt/tilt;roll*=maxTilt/tilt;}
            return new ContactPose(true,pitch,roll,Clamp(c,maxOffset));
        }
        public static double Blend(double current,double target,double dt,double response,double maxRate)
        {
            if(!Finite(current)||!Finite(target)||!Finite(dt)||!Finite(response)||!Finite(maxRate)||dt<0||response<0||maxRate<0)return current;
            return current+Clamp((target-current)*(1-Math.Exp(-response*dt)),maxRate*dt);
        }
        // Offset only while a foot is planted and its sampled surface is valid.
        public static double FootOffset(double authoredY,double groundY,bool planted,bool hit,double maxReach)
            =>planted && hit && Finite(authoredY)&&Finite(groundY)&&Finite(maxReach)&&maxReach>=0?Clamp(groundY-authoredY,maxReach):0;
    }

    public enum StimulusKind { Food, Social, Point, Player }
    public sealed class LabStimulus
    {
        public readonly string Id,Habitat;
        public readonly StimulusKind Kind;
        public readonly LabVec Position;
        public readonly long ExpiresAt;
        public readonly bool Eligible;
        public LabStimulus(string id,string habitat,StimulusKind kind,LabVec pos,long expiresAt,bool eligible=true)
        {Id=id;Habitat=habitat;Kind=kind;Position=pos;ExpiresAt=expiresAt;Eligible=eligible;}
    }
    public sealed class LabMemory
    {
        readonly Dictionary<string,(LabStimulus value,long seen)> known=new Dictionary<string,(LabStimulus,long)>(StringComparer.Ordinal);
        readonly long ttl;
        long lastNow=long.MinValue;
        public LabMemory(long ttlMs=5000){if(ttlMs<=0)throw new ArgumentOutOfRangeException(nameof(ttlMs));ttl=ttlMs;}
        public void Sense(LabVec origin,string habitat,double range,long now,IEnumerable<LabStimulus> stimuli,Func<LabVec,LabVec,bool> lineOfSight)
        {
            if(now<lastNow)throw new ArgumentException("time moved backwards");
            lastNow=now;
            foreach(var key in known.Keys.ToArray())
            {
                var v=known[key];
                if(v.value.ExpiresAt<=now || now-v.seen>=ttl || v.value.Habitat!=habitat)known.Remove(key);
            }
            if(stimuli==null || lineOfSight==null || !origin.Finite || !(range>=0) || double.IsInfinity(range))return;
            foreach(var v in stimuli.OrderBy(x=>x?.Id,StringComparer.Ordinal))
            {
                if(v==null || string.IsNullOrEmpty(v.Id))continue;
                if(!v.Eligible || v.ExpiresAt<=now || v.Habitat!=habitat){known.Remove(v.Id);continue;}
                if(!v.Position.Finite || (v.Position-origin).Length>range || !lineOfSight(origin,v.Position))continue;
                known[v.Id]=(v,now);
            }
        }
        public IReadOnlyList<LabStimulus> Snapshot()=>known.Values.Select(x=>x.value).OrderBy(x=>x.Id,StringComparer.Ordinal).ToArray();
    }
    public enum ActionStep { None, Approach, Face, ReadyToRequest, Cancelled, Complete }
    // TL.5 action sequencing: no gameplay command, only a one-shot request signal.
    public sealed class ActionSequenceLab
    {
        public string TargetId { get; private set; }
        public ActionStep Step { get; private set; }
        public int Requests { get; private set; }
        public void Begin(string target)
        {
            if(string.IsNullOrEmpty(target))throw new ArgumentException(nameof(target));
            TargetId=target;Step=ActionStep.Approach;Requests=0;
        }
        public bool Tick(bool canonicalEligible,bool pathAvailable,bool inReach,bool facing,bool motionReady)
        {
            if(Step==ActionStep.None || Step==ActionStep.Cancelled || Step==ActionStep.Complete || Step==ActionStep.ReadyToRequest)return false;
            if(!canonicalEligible){Step=ActionStep.Cancelled;return false;}
            if(!inReach){Step=pathAvailable?ActionStep.Approach:ActionStep.Cancelled;return false;}
            if(!facing){Step=ActionStep.Face;return false;}
            if(!motionReady){Step=ActionStep.Face;return false;}
            Step=ActionStep.ReadyToRequest;Requests++;return true;
        }
        public void Resolve(bool accepted)
        {if(Step==ActionStep.ReadyToRequest)Step=accepted?ActionStep.Complete:ActionStep.Cancelled;}
    }

    // TL.6 explicit compatibility supplied by caller; no assumption all species are social.
    public sealed class SocialCandidate
    {
        public readonly string Uid,Habitat;
        public readonly LabVec Position;
        public readonly bool WantsCompany,Available;
        public SocialCandidate(string uid,string habitat,LabVec position,bool wants,bool available=true)
        {Uid=uid;Habitat=habitat;Position=position;WantsCompany=wants;Available=available;}
    }
    public readonly struct SocialPair
    {
        public readonly string A,B;
        public SocialPair(string a,string b){A=a;B=b;}
    }
    public static class SocialPairLab
    {
        public static IReadOnlyList<SocialPair> Match(IEnumerable<SocialCandidate> input,
            Func<SocialCandidate,SocialCandidate,bool> compatible,double range=2)
        {
            if(input==null||compatible==null||!(range>=0)||double.IsInfinity(range))return Array.Empty<SocialPair>();
            var all=input.Where(x=>x!=null && !string.IsNullOrEmpty(x.Uid) && !string.IsNullOrEmpty(x.Habitat) &&
                x.Position.Finite && x.Available && x.WantsCompany).OrderBy(x=>x.Uid,StringComparer.Ordinal).ToArray();
            var edges=new List<(SocialCandidate a,SocialCandidate b,double distance)>();
            for(int i=0;i<all.Length;i++)
            for(int j=i+1;j<all.Length;j++)
            {
                var a=all[i];var b=all[j];double distance=(a.Position-b.Position).Length;
                if(a.Uid==b.Uid || a.Habitat!=b.Habitat || distance>range || !compatible(a,b)||!compatible(b,a))continue;
                edges.Add((a,b,distance));
            }
            var used=new HashSet<string>(StringComparer.Ordinal);var result=new List<SocialPair>();
            foreach(var e in edges.OrderBy(x=>x.distance).ThenBy(x=>x.a.Uid,StringComparer.Ordinal).ThenBy(x=>x.b.Uid,StringComparer.Ordinal))
            {
                if(used.Contains(e.a.Uid)||used.Contains(e.b.Uid))continue;
                used.Add(e.a.Uid);used.Add(e.b.Uid);result.Add(new SocialPair(e.a.Uid,e.b.Uid));
            }
            return result;
        }
    }
}
