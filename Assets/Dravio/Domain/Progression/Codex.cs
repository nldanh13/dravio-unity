using System.Collections.Generic;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Economy;

namespace Dravio.Domain.Progression
{
    /// <summary>
    /// Bách khoa Dravio (GDD 3.1 trang 22, Phụ lục C.2, F; U2.7 — implementation mới, TS b33ec7a chưa có codex).
    /// 64 ô = 16 loài × 4 biến thể. Một ô được khám phá khi người chơi sở hữu (chưa thả) một rồng của loài đó với biến thể đó;
    /// trứng chưa nhận không tính. Khám phá chỉ tăng, giữ sau khi bán/thả. ObservedStages ghi mọi stage ≤ stage hiện tại của rồng đã sở hữu.
    /// </summary>
    public static class Codex
    {
        private static readonly GrowthStage[] Stages = { GrowthStage.Baby, GrowthStage.Juvenile, GrowthStage.Adult };

        public static CodexEntry Find(GameState s, string speciesId)
        {
            foreach (CodexEntry e in s.Codex) if (e.SpeciesId == speciesId) return e;
            return null;
        }

        private static CodexEntry Ensure(GameState s, string speciesId)
        {
            CodexEntry e = Find(s, speciesId);
            if (e != null) return e;
            e = new CodexEntry { SpeciesId = speciesId };
            var ordered = new List<CodexEntry>();
            foreach (DragonSpecies sp in SpeciesCatalog.All)
            {
                if (sp.Id == speciesId) ordered.Add(e);
                else { CodexEntry x = Find(s, sp.Id); if (x != null) ordered.Add(x); }
            }
            s.Codex = ordered;
            return e;
        }

        /// <summary>Quét kho, ghi khám phá mới (idempotent). Chạy sau mỗi lệnh thành công, trước thành tựu.</summary>
        public static void Observe(GameState s, List<DomainEvent> events)
        {
            foreach (DragonState d in s.Dragons)
            {
                if (d.Location.Kind == LocationKind.Released) continue;
                DragonSpecies sp = SpeciesCatalog.Get(d.SpeciesId);
                CodexEntry e = Ensure(s, d.SpeciesId);
                if (!e.DiscoveredMutations.Contains(d.Mutation))
                {
                    var muts = new List<MutationType>();
                    foreach (MutationType m in Mutation.Order) if (m == d.Mutation || e.DiscoveredMutations.Contains(m)) muts.Add(m);
                    e.DiscoveredMutations = muts;
                    events?.Add(new CodexDiscovered(d.SpeciesId, d.Mutation));
                }
                GrowthStage stage = EconomyRates.GrowthStageOf(d.XpSec, sp);
                foreach (GrowthStage st in Stages)
                    if (st <= stage && !e.ObservedStages.Contains(st)) { e.ObservedStages.Add(st); e.ObservedStages.Sort(); }
            }
        }

        /// <summary>Gọi khi một rồng nở (claimPack / claimBreed).</summary>
        public static void RecordHatch(GameState s, string speciesId) => Ensure(s, speciesId).TotalHatched += 1;

        public static int SpeciesDiscovered(GameState s) => s.Codex.Count;

        public static int CellsDiscovered(GameState s)
        {
            int n = 0;
            foreach (CodexEntry e in s.Codex) n += e.DiscoveredMutations.Count;
            return n;
        }

        public static bool AnyWithMutation(GameState s, MutationType m)
        {
            foreach (CodexEntry e in s.Codex) if (e.DiscoveredMutations.Contains(m)) return true;
            return false;
        }

        public static bool AnyOfRarity(GameState s, Rarity r)
        {
            foreach (CodexEntry e in s.Codex) if (SpeciesCatalog.Get(e.SpeciesId).Rarity == r) return true;
            return false;
        }
    }
}
