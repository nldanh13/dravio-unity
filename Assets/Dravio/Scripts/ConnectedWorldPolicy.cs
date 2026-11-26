using System;
using System.Linq;
using Dravio.Domain;
using Dravio.Domain.Queries;

namespace Dravio
{
    // Viewing a world is not a Domain command, does not move a dragon or change unlocks.
    public static class ConnectedWorldPolicy
    {
        public const string Origin="origin",Ocean="ocean",Sky="sky";
        public const string OriginScene="Assets/Dravio/Scenes/OriginLongTermGameplay.unity";
        public const string OceanScene="Assets/Dravio/Environment/Scenes/Ocean_Environment.unity";
        public const string SkyScene="Assets/Dravio/Environment/Scenes/Sky_Environment.unity";
        public static string ScenePath(string zone)=>zone==Origin?OriginScene:zone==Ocean?OceanScene:zone==Sky?SkyScene:null;
        public static bool CanVisit(GameState s,string zone)=>ScenePath(zone)!=null && s?.FindZone(zone)?.Unlocked==true;
        public static bool InView(GameState s,DragonState d,string zone)
            =>CanVisit(s,zone) && d!=null && d.Location.Kind==LocationKind.Zone &&
                d.Location.ZoneId==zone && s.FindZone(zone).UnlockedHabitatIds.Contains(d.Location.HabitatId) &&
                Ecosystem.StatusOf(s,d)==DragonStatus.InHabitat;
        public static bool MatchesDestination(TravelOption option,string zone,string habitat)
            =>option!=null && option.ZoneId==zone && option.HabitatId==habitat;
    }
}
