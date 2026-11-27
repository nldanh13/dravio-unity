using System;
using Dravio.Domain;
using Dravio.Domain.Behavior;
using Dravio.Domain.Queries;

namespace Dravio
{
    // No gameplay rule here: this is the supported subset of the current Origin presentation.
    public static class AutonomousPresentationPolicy
    {
        // Coordinates are local to the camera's visible safe area, with y increasing downward.
        public static (float x,float y)? PlaceLabel(float anchorX,float anchorY,float width,float height,
            float viewWidth,float viewHeight,(float x,float y,float width,float height)? occupied)
        {
            if(width<=0 || height<=0 || width>viewWidth || height>viewHeight)return null;
            float x=Math.Max(0,Math.Min(anchorX-width/2,viewWidth-width));
            float y=Math.Max(0,Math.Min(anchorY-height,viewHeight-height));
            if(!occupied.HasValue)return (x,y);
            var other=occupied.Value;
            const float gap=4;
            if(x+width+gap<=other.x || x>=other.x+other.width+gap ||
                y+height+gap<=other.y || y>=other.y+other.height+gap)return (x,y);
            float below=other.y+other.height+gap, above=other.y-height-gap;
            if(below+height<=viewHeight && (above<0 || below-y<=y-above))return (x,below);
            if(above>=0)return (x,above);
            float right=other.x+other.width+gap, left=other.x-width-gap;
            if(right+width<=viewWidth && (left<0 || right-x<=x-left))return (right,y);
            if(left>=0)return (left,y);
            return null;
        }

        public static bool RequiresUnimplementedMedium(DragonBehavior behavior)=>
            behavior==DragonBehavior.Swim || behavior==DragonBehavior.Fly || behavior==DragonBehavior.Burrow;
        public static bool UsesPoint(DragonBehavior behavior)=>
            behavior==DragonBehavior.Bask || behavior==DragonBehavior.Rest || behavior==DragonBehavior.Explore;
        public static bool AllowsPoint(DragonBehavior behavior,BehaviorRole role)=>
            role==BehaviorRole.Presentation && UsesPoint(behavior);

        // Called again immediately before Send; never replace the Domain reducer's checks.
        public static bool CanConsumeMarker(GameState s,string uid,string markerUid,string foodId,long nowMs,DayPhase phase)
        {
            if(s==null || string.IsNullOrEmpty(uid) || string.IsNullOrEmpty(foodId) ||
                !string.Equals(uid,markerUid,StringComparison.Ordinal))return false;
            var d=s.FindDragon(uid);
            var f=s.Food.Drops.Find(x=>x.Id==foodId);
            if(d==null || f==null || !BehaviorEngine.CanEat(d,f,nowMs))return false;
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
