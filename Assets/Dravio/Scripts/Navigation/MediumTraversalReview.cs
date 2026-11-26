using System;

namespace Dravio.Navigation
{
    /// <summary>U5.4.2 conservative *review* envelope, centred on the visual root.
    /// Extents are catalog sizes, not measured root-relative swept mesh bounds.
    /// Never use this approximation as production transition certification.</summary>
    public readonly struct MediumReviewEnvelope
    {
        public readonly float Horizontal,Vertical;
        public MediumReviewEnvelope(float horizontal,float vertical){Horizontal=horizontal;Vertical=vertical;}
        static bool Positive(float x)=>x>0 && !float.IsInfinity(x) && !float.IsNaN(x);
        public bool Valid=>Positive(Horizontal)&&Positive(Vertical);
        public static bool TryFromDimensions(float bodyLength,float bodyWidth,float bodyHeight,float mediumWidth,float mediumHeight,out MediumReviewEnvelope envelope)
        {
            envelope=default;
            if(!Positive(bodyLength)||!Positive(bodyWidth)||!Positive(bodyHeight)||!Positive(mediumWidth)||!Positive(mediumHeight))return false;
            // Circumscribed horizontal circle (allows all yaw angles), vertical half-extent + review margin.
            double w=Math.Max(bodyWidth,mediumWidth);
            double radius=Math.Sqrt((double)bodyLength*bodyLength+w*w)*.5+.10;
            envelope=new MediumReviewEnvelope((float)radius,Math.Max(bodyHeight,mediumHeight)*.5f+.10f);
            return envelope.Valid;
        }
    }
    public static class SpatialMediumReview
    {
        public static RouteResult Plan(NavPoint start,NavPoint goal,NavigationMedium medium,Func<NavPoint,bool> clear,int budget=2048)
        {
            if(medium!=NavigationMedium.Swim && medium!=NavigationMedium.Air)return new RouteResult(RouteStatus.UnsupportedMedium,Array.Empty<NavPoint>(),0);
            var result=SanctuaryRoutePlanner.Plan(start,goal,medium,clear,.45f,budget);
            if(!result.Success)return result;
            var previous=start;
            foreach(var point in result.Waypoints)
            {
                if(!SanctuaryRoutePlanner.SegmentClear(previous,point,clear,.025f))
                    return new RouteResult(RouteStatus.Unreachable,Array.Empty<NavPoint>(),result.Expanded);
                previous=point;
            }
            return result;
        }
    }

    public enum MediumTransitionKind { TakeOff, Land, WaterEnter, WaterExit }
    public enum MediumTransitionState { Pending, Running, Completed, Faulted }
    /// <summary>One exclusive transition reservation for a single UID. This is a tested contract,
    /// not a certificate generator: caller must supply real, independently reviewed corridor evidence.
    /// No authored certificates exist in the current Origin/Ocean/Sky JSON.</summary>
    public sealed class MediumTransitionGate
    {
        public readonly string Uid,Zone,Habitat,CertificateId;
        public readonly MediumTransitionKind Kind;
        public readonly NavigationMedium Source,Target;
        public readonly NavPoint Start,End;
        public NavPoint Position { get; private set; }
        public MediumTransitionState State { get; private set; }
        public string Fault { get; private set; }
        public bool MotionLocked=>State!=MediumTransitionState.Completed;
        readonly Func<NavPoint,bool> targetClear,corridorClear;
        bool sawClip,offsetApplied;
        long lastTick;
        readonly float expectedOffset;

        MediumTransitionGate(string uid,string zone,string habitat,string certificate,MediumTransitionKind kind,
            NavigationMedium source,NavigationMedium target,NavPoint start,NavPoint end,float offset,
            Func<NavPoint,bool> targetClear,Func<NavPoint,bool> corridorClear,long tick)
        {
            Uid=uid;Zone=zone;Habitat=habitat;CertificateId=certificate;Kind=kind;Source=source;Target=target;
            Start=start;End=end;Position=start;expectedOffset=offset;this.targetClear=targetClear;this.corridorClear=corridorClear;
            lastTick=tick;State=MediumTransitionState.Pending;
        }
        public static MediumTransitionGate TryBegin(string uid,string zone,string habitat,string certificate,MediumTransitionKind kind,
            NavPoint start,NavPoint end,float offset,bool canonicalAllowed,bool exactModelApproved,bool authoredClip,bool ready,
            Func<NavPoint,bool> sourceClear,Func<NavPoint,bool> targetClear,Func<NavPoint,bool> corridorClear,long tick)
        {
            if(string.IsNullOrWhiteSpace(uid)||string.IsNullOrWhiteSpace(zone)||string.IsNullOrWhiteSpace(habitat)||
                string.IsNullOrWhiteSpace(certificate)||!canonicalAllowed||!exactModelApproved||!authoredClip||!ready||
                sourceClear==null||targetClear==null||corridorClear==null||!start.Finite||!end.Finite||
                float.IsNaN(offset)||float.IsInfinity(offset))return null;
            NavigationMedium source,target;
            switch(kind)
            {
                case MediumTransitionKind.TakeOff: source=NavigationMedium.Ground;target=NavigationMedium.Air;if(offset<=0)return null;break;
                case MediumTransitionKind.Land: source=NavigationMedium.Air;target=NavigationMedium.Ground;if(offset>=0)return null;break;
                case MediumTransitionKind.WaterEnter: source=NavigationMedium.Ground;target=NavigationMedium.Swim;if(offset!=0)return null;break;
                case MediumTransitionKind.WaterExit: source=NavigationMedium.Swim;target=NavigationMedium.Ground;if(offset!=0)return null;break;
                default:return null;
            }
            // CHAR.7 only supplies vertical takeoff/land offsets. No invented horizontal swim-entry displacement.
            var projected=new NavPoint(start.X,start.Y+offset,start.Z);
            if(NavPoint.Distance(projected,end)>.001||!sourceClear(start)||!targetClear(end)||
                !SanctuaryRoutePlanner.SegmentClear(start,end,corridorClear,.025f))return null;
            return new MediumTransitionGate(uid,zone,habitat,certificate,kind,source,target,start,end,offset,targetClear,corridorClear,tick);
        }
        void Fail(string reason){State=MediumTransitionState.Faulted;Fault=reason;}
        // Call once for each *new* Character Sync result, not every read of MotionStatusOf.
        // Caller must retain/quarantine a faulted model; no teleport rollback.
        public bool Observe(long tick,string uid,string zone,string habitat,bool live,bool eligible,bool exactModelUnchanged,
            NavigationMedium shownMode,bool activeExpectedClip,bool completedExpectedClip,bool canTranslate,float rootOffset)
        {
            if(State==MediumTransitionState.Completed||State==MediumTransitionState.Faulted)return false;
            if(tick<=lastTick){Fail("duplicate-or-stale-sync");return false;}lastTick=tick;
            if(uid!=Uid||zone!=Zone||habitat!=Habitat||!live||!eligible||!exactModelUnchanged||
                float.IsNaN(rootOffset)||float.IsInfinity(rootOffset)||!corridorClear(Position))
            {Fail("identity-eligibility-or-corridor-changed");return false;}
            if(shownMode!=Source && shownMode!=Target){Fail("unexpected-medium");return false;}
            if(activeExpectedClip)
            {
                if(canTranslate){Fail("transition-did-not-lock");return false;}
                sawClip=true;State=MediumTransitionState.Running;
            }
            bool offsetDue=!offsetApplied && (Kind==MediumTransitionKind.Land?activeExpectedClip:
                Kind==MediumTransitionKind.TakeOff?completedExpectedClip:false);
            if(offsetDue)
            {
                if(!sawClip||Math.Abs(rootOffset-expectedOffset)>.001||
                    !SanctuaryRoutePlanner.SegmentClear(Position,End,corridorClear,.025f))
                {Fail("wrong-or-unsafe-root-offset");return false;}
                Position=End;offsetApplied=true;
            }
            else if(Math.Abs(rootOffset)>.0001){Fail("unexpected-root-offset");return false;}
            if(completedExpectedClip)
            {
                if(!sawClip||shownMode!=Target||!canTranslate||
                    (expectedOffset!=0 && !offsetApplied)||!targetClear(Position)||NavPoint.Distance(Position,End)>.001)
                {Fail("uncertified-completion");return false;}
                State=MediumTransitionState.Completed;return true;
            }
            if(shownMode==Target && !completedExpectedClip){Fail("missing-completion-event");return false;}
            return false;
        }
    }
}
