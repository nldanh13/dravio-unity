using System;
using Dravio.Domain;
using Dravio.Domain.Behavior;
using Dravio.Domain.Queries;

namespace Dravio
{
    // No gameplay rule here: this is the supported subset of the current Origin presentation.
    public static class AutonomousPresentationPolicy
    {
        public static bool RequiresUnimplementedMedium(DragonBehavior behavior)=>
            behavior==DragonBehavior.Swim || behavior==DragonBehavior.Fly || behavior==DragonBehavior.Burrow;
        public static bool UsesPoint(DragonBehavior behavior)=>
            behavior==DragonBehavior.Bask || behavior==DragonBehavior.Rest || behavior==DragonBehavior.Explore;
        public static bool AllowsPoint(DragonBehavior behavior,BehaviorRole role)=>
            role==BehaviorRole.Presentation && UsesPoint(behavior);

        // Called again immediately before Send; never replace the Domain reducer's checks.
        public static bool CanConsumeMarker(GameState s,string uid,string markerUid,string foodId,long nowMs,DayPhase phase,bool shared)
        {
            if(s==null || string.IsNullOrEmpty(uid) || string.IsNullOrEmpty(foodId) ||
                !string.Equals(uid,markerUid,StringComparison.Ordinal))return false;
            var d=s.FindDragon(uid);
            var f=s.Food.Drops.Find(x=>x.Id==foodId);
            if(d==null || f==null || !BehaviorEngine.CanEat(d,f,nowMs))return false;
            if(!shared)return true;
            var decision=BehaviorQueries.Dragon(s,uid,nowMs,phase);
            return decision?.Role==BehaviorRole.Gameplay && decision.Behavior==DragonBehavior.SeekFood && decision.TargetFoodId==foodId;
        }
        public static bool PointMatches(DragonBehavior behavior,string kind)=>behavior switch
        {
            DragonBehavior.Bask=>kind=="bask",
            DragonBehavior.Rest=>kind=="rest",
            DragonBehavior.Explore=>kind=="inspect_rune" || kind=="sniff_flowers" ||
                kind=="watch_glow" || kind=="stargaze" || kind=="inspect_statue",
            _=>false
        };
    }
}
