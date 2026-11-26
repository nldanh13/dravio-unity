#if UNITY_EDITOR || DRAVIO_DOTNET_TESTS
using System;

namespace Dravio
{
    public enum FoodActionPhase { None, Searching, Approach, Face, WaitMotion, Ready, Requested, Accepted, Rejected }
    // Presentation-only, single local marker. Memory permits approach, never authorizes consumption.
    // Identity and clock are supplied by the canonical session; no saved state or gameplay mutations.
    public sealed class FoodActionPresentationGate
    {
        public const long MemoryMs=2000;
        public const double SenseRange=3.0;
        public const double Reach=.55;
        public const double FacingDegrees=15;
        string uid,food,habitat;
        long seenAt,lastNow;
        bool seen,requested;
        public FoodActionPhase Phase { get; private set; }
        public bool Remembered { get; private set; }
        public bool Visible { get; private set; }
        public int Requests { get; private set; }
        public void Clear()
        {
            uid=food=habitat=null;seen=false;requested=false;lastNow=0;seenAt=0;
            Remembered=Visible=false;Phase=FoodActionPhase.None;Requests=0;
        }
        public bool Observe(string owner,string marker,string currentHabitat,long now,long expires,
            bool eligible,double distance,bool clear)
        {
            if(string.IsNullOrEmpty(owner)||string.IsNullOrEmpty(marker)||string.IsNullOrEmpty(currentHabitat))
            {Remembered=Visible=false;return false;}
            if(food!=marker)
            {
                Clear();uid=owner;food=marker;habitat=currentHabitat;lastNow=now;
            }
            // Same marker never changes owner or habitat, even if selection changes.
            if(uid!=owner || habitat!=currentHabitat || now<lastNow || !eligible || now>=expires ||
                double.IsNaN(distance)||double.IsInfinity(distance)||distance<0)
            {
                seen=false;Remembered=Visible=false;
                if(!requested)Phase=FoodActionPhase.Searching;
                return false;
            }
            lastNow=now;
            Visible=clear && distance<=SenseRange;
            if(Visible){seen=true;seenAt=now;}
            Remembered=seen && now-seenAt<MemoryMs;
            if(!requested)Phase=Remembered?FoodActionPhase.Approach:FoodActionPhase.Searching;
            return Remembered;
        }
        public void Presentation(bool reached,bool facing,bool motionReady)
        {
            if(requested)return;
            Phase=!Remembered?FoodActionPhase.Searching:!reached?FoodActionPhase.Approach:
                !facing?FoodActionPhase.Face:!motionReady||!Visible?FoodActionPhase.WaitMotion:FoodActionPhase.Ready;
        }
        public bool TryRequest(string owner,string marker,string currentHabitat,long now,long expires,
            bool eligible,double distance,bool clear,bool facing,bool motionReady)
        {
            // Re-sense live facts at commit boundary. Stale memory cannot authorize a request.
            if(!Observe(owner,marker,currentHabitat,now,expires,eligible,distance,clear)||requested)return false;
            Presentation(distance<=Reach,facing,motionReady);
            if(Phase!=FoodActionPhase.Ready)return false;
            requested=true;Requests++;Phase=FoodActionPhase.Requested;return true;
        }
        public void Resolve(bool accepted)
        {if(Phase==FoodActionPhase.Requested)Phase=accepted?FoodActionPhase.Accepted:FoodActionPhase.Rejected;}
    }
}
#endif
