using Dravio.Domain.Behavior;

namespace Dravio
{
    // No gameplay rule here: this is the supported subset of the current Origin presentation.
    public static class AutonomousPresentationPolicy
    {
        public static bool RequiresUnimplementedMedium(DragonBehavior behavior)=>
            behavior==DragonBehavior.Swim || behavior==DragonBehavior.Fly || behavior==DragonBehavior.Burrow;
        public static bool UsesPoint(DragonBehavior behavior)=>
            behavior==DragonBehavior.Bask || behavior==DragonBehavior.Rest || behavior==DragonBehavior.Explore;
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
