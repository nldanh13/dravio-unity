using System;
using System.Collections.Generic;
using System.Linq;
using Dravio.Domain;
using Dravio.Domain.Catalog;
using Dravio.Domain.Queries;

namespace Dravio
{
    // Pure projection policy, no Unity, no state mutation.
    public static class LivingResidentPolicy
    {
        public static bool InOriginView(GameState state,DragonState dragon)=>
            state!=null && dragon!=null && dragon.Location.Kind==LocationKind.Zone &&
            dragon.Location.ZoneId=="origin" && Ecosystem.StatusOf(state,dragon)==DragonStatus.InHabitat;

        public static bool CanWalk(DragonState dragon)=>
            dragon!=null && SpeciesCatalog.Get(dragon.SpeciesId).Biology.Locomotion.Contains(Locomotion.Walk);

        public static string NextUid(IReadOnlyList<string> ids,string current)
        {
            if(ids==null || ids.Count==0)return null;
            for(int i=0;i<ids.Count;i++)
                if(string.Equals(ids[i],current,StringComparison.Ordinal))return ids[(i+1)%ids.Count];
            return ids[0];
        }
    }
}
