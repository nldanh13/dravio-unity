using System;
using System.Collections.Generic;
using Dravio.Domain.Behavior;
using Dravio.Navigation;

namespace Dravio
{
    public enum SocialPresentationPhase { Approach, Face, Interact, Finished, Cancelled }
    // Pure lease; deliberately no GameSession/command/event dependency. One attempt per participant per epoch.
    public sealed class SocialPresentationLease
    {
        public readonly string A,B,Zone,Habitat;
        public readonly SocialInteractionKind Kind;
        public readonly long CandidateStart,CandidateEnd,Deadline;
        public readonly float MinDistance;
        public SocialPresentationPhase Phase { get; private set; }=SocialPresentationPhase.Approach;
        public long InteractionEnd { get; private set; }
        public bool Terminal=>Phase==SocialPresentationPhase.Finished||Phase==SocialPresentationPhase.Cancelled;
        public bool Contains(string uid)=>uid==A||uid==B;
        public string Partner(string uid)=>uid==A?B:uid==B?A:null;
        internal SocialPresentationLease(SocialInteractionCandidate c,long now)
        {
            A=c.InitiatorUid;B=c.PartnerUid;Zone=c.ZoneId;Habitat=c.HabitatId;Kind=c.Kind;
            CandidateStart=c.StartedAtMs;CandidateEnd=c.UntilMs;Deadline=Math.Min(c.UntilMs,now+12000);
            MinDistance=Math.Max(.55f,c.MinDistanceDm/10f);
        }
        public bool Matches(SocialInteractionCandidate c)=>c!=null&&!c.AffectsGameplay&&c.Role==BehaviorRole.Presentation&&
            c.InitiatorUid==A&&c.PartnerUid==B&&c.ZoneId==Zone&&c.HabitatId==Habitat&&c.Kind==Kind&&
            c.StartedAtMs==CandidateStart&&c.UntilMs==CandidateEnd&&Math.Max(.55f,c.MinDistanceDm/10f)==MinDistance;
        public void Cancel(){if(!Terminal)Phase=SocialPresentationPhase.Cancelled;}
        public void Tick(long now,bool eligible,bool arrived,bool facing,bool motionReady,bool spacing)
        {
            if(Terminal)return;
            if(!eligible||!spacing||now>=Deadline){Cancel();return;}
            if(Phase==SocialPresentationPhase.Interact)
            {
                if(!arrived){Cancel();return;}
                if(now>=InteractionEnd)Phase=SocialPresentationPhase.Finished;
                return;
            }
            if(!arrived){Phase=SocialPresentationPhase.Approach;return;}
            Phase=SocialPresentationPhase.Face;
            if(facing&&motionReady)
            {Phase=SocialPresentationPhase.Interact;InteractionEnd=Math.Min(Deadline,now+2500);}
        }
    }
    public sealed class SocialPresentationScheduler
    {
        readonly HashSet<string> attempted=new HashSet<string>(StringComparer.Ordinal);
        long epoch=long.MinValue;
        public SocialPresentationLease Active { get; private set; }
        public bool TryBegin(SocialInteractionCandidate c,long now,bool eligible)
        {
            long e=now/SocialRules.EpochMs;
            if(e!=epoch){attempted.Clear();epoch=e;}
            if(Active!=null||!eligible||c==null||c.AffectsGameplay||c.Role!=BehaviorRole.Presentation||
                c.Kind==SocialInteractionKind.KeepDistance||string.IsNullOrEmpty(c.InitiatorUid)||
                string.IsNullOrEmpty(c.PartnerUid)||c.InitiatorUid==c.PartnerUid||
                string.IsNullOrEmpty(c.ZoneId)||string.IsNullOrEmpty(c.HabitatId)||
                now<c.StartedAtMs||c.UntilMs-now<3000||
                attempted.Contains(c.InitiatorUid)||attempted.Contains(c.PartnerUid))return false;
            attempted.Add(c.InitiatorUid);attempted.Add(c.PartnerUid);
            Active=new SocialPresentationLease(c,now);return true;
        }
        public void Release(){Active?.Cancel();Active=null;}
        // No reset of attempted history on world visit; no repeated interaction by switching scenes.
        public static bool Spacing(NavPoint a,NavPoint b,float min)
        {
            if(!a.Finite||!b.Finite||!SpatialResidentPolicy.Positive(min))return false;
            double x=(double)a.X-b.X,z=(double)a.Z-b.Z;
            return x*x+z*z>=(double)min*min;
        }
    }
}
