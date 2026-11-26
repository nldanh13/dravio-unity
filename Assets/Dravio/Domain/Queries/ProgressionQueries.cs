using System.Collections.Generic;
using Dravio.Domain.Behavior;
using Dravio.Domain.Catalog;
using Dravio.Domain.Progression;
using Dravio.Domain.World;

namespace Dravio.Domain.Queries
{
    /// <summary>Hồ sơ nghiên cứu: loài + các cá thể của loài trong kho.</summary>
    public sealed class ResearchProfileDto
    {
        public SpeciesResearchProfile Species { get; internal set; }
        public IReadOnlyList<IndividualResearchProfile> Individuals { get; internal set; }
    }

    /// <summary>Cờ luật hiện hành, để UI không phải đoán hệ nào đang bật.</summary>
    public sealed class RuleFlagsDto
    {
        public bool ResearchPacingEnabled { get; internal set; }
        public bool SocialGameplayEffectsEnabled { get; internal set; }
        public bool QuestCompletionEnabled { get; internal set; }
        /// <summary>Luôn false trên nhánh này (ADR 0203 C1 chưa duyệt).</summary>
        public bool ComfortDriftEnabled { get; internal set; }
        /// <summary>Luôn false trên nhánh này (ADR 0202 D7).</summary>
        public bool NaturalDisastersEnabled { get; internal set; }
        /// <summary>Luôn false (ADR 0203 C3 chưa làm).</summary>
        public bool EnergyDrainEnabled { get; internal set; }
    }

    /// <summary>Tổng quan tiến trình dài hạn cho màn Codex / hành trình.</summary>
    public sealed class ProgressionOverviewDto
    {
        public IReadOnlyList<string> UnlockedZones { get; internal set; }
        public int UnlockedHabitats { get; internal set; }
        public int TotalHabitats { get; internal set; }
        public int SpeciesDiscovered { get; internal set; }
        public int SpeciesTotal { get; internal set; }
        public int CodexCells { get; internal set; }
        public int CodexCellsTotal { get; internal set; }
        /// <summary>Số loài ở cấp ≥ tier (Sighted, Profiled, LifeCycle, Ethology).</summary>
        public IReadOnlyDictionary<ResearchTier, int> ResearchTiers { get; internal set; }
        public int QuestsCompleted { get; internal set; }
        public int QuestsReady { get; internal set; }
        public int QuestsInProgress { get; internal set; }
        public int QuestsLocked { get; internal set; }
        public IReadOnlyList<MaterialStock> Materials { get; internal set; }
        /// <summary>Tối đa 3 gợi ý mục tiêu tiếp theo, khóa i18n (quest.&lt;id&gt;.name hoặc research.next.*). Không bao giờ là "kiếm vàng".</summary>
        public IReadOnlyList<string> SuggestedGoals { get; internal set; }
        public RuleFlagsDto Rules { get; internal set; }
    }

    /// <summary>
    /// API chỉ đọc U2.10 (ADR 0204 E1–E5). Cùng nguyên tắc với <see cref="BehaviorQueries"/>: snapshot, không đổi state, không lệnh.
    /// <c>log</c> là <see cref="ResearchLog"/> do tầng Platform giữ trong bộ nhớ (chưa lưu ở v8); null = chưa có ghi chép.
    /// Mọi tham số luật mặc định là Production (tắt).
    /// </summary>
    public static class ProgressionQueries
    {
        public static ResearchProfileDto ResearchProfile(GameState s, string speciesId, ResearchLog log = null, ResearchPacing pacing = null)
        {
            pacing = pacing ?? ResearchPacing.Production;
            var inds = new List<IndividualResearchProfile>();
            foreach (DragonState d in s.Dragons)
                if (d.SpeciesId == speciesId && d.Location.Kind != LocationKind.Released) inds.Add(ResearchProgress.Individual(s, d.Uid, log, pacing));
            return new ResearchProfileDto { Species = ResearchProgress.Species(s, speciesId, log, pacing), Individuals = inds };
        }

        public static IndividualResearchProfile IndividualResearch(GameState s, string uid, ResearchLog log = null, ResearchPacing pacing = null) =>
            ResearchProgress.Individual(s, uid, log, pacing ?? ResearchPacing.Production);

        public static List<ObservationOpportunity> ObservationOpportunities(GameState s, long nowMs, DayPhase phase, ResearchLog log = null, ResearchPacing pacing = null) =>
            ResearchProgress.Opportunities(s, nowMs, phase, log, pacing ?? ResearchPacing.Production);

        public static List<SocialInteractionCandidate> SocialInteractionCandidates(GameState s, long nowMs, DayPhase phase) => SocialEcology.Candidates(s, nowMs, phase);

        public static SocialProfile SocialProfile(GameState s, string uid)
        {
            DragonState d = s.FindDragon(uid);
            return d == null || d.Location.Kind == LocationKind.Released ? null : SocialEcology.Profile(d);
        }

        public static SocialRelation SocialRelation(GameState s, string uidA, string uidB)
        {
            DragonState a = s.FindDragon(uidA), b = s.FindDragon(uidB);
            return a == null || b == null || a == b ? null : SocialEcology.Relation(a, b);
        }

        public static List<QuestStatusView> QuestRequirements(GameState s, ResearchLog log = null, ResearchPacing pacing = null, QuestRules rules = null) =>
            QuestProposals.Evaluate(s, log, pacing, rules);

        public static RuleFlagsDto Rules(ResearchPacing pacing = null, SocialRules social = null, QuestRules quests = null) => new RuleFlagsDto
        {
            ResearchPacingEnabled = (pacing ?? ResearchPacing.Production).Enabled,
            SocialGameplayEffectsEnabled = (social ?? SocialRules.Production).GameplayEffectsEnabled,
            QuestCompletionEnabled = (quests ?? QuestRules.Production).Enabled,
            ComfortDriftEnabled = EcologyComfortRules.Production.Enabled,
            NaturalDisastersEnabled = DisasterRules.Production.Enabled,
            EnergyDrainEnabled = false,
        };

        public static ProgressionOverviewDto Overview(GameState s, ResearchLog log = null, ResearchPacing pacing = null, QuestRules quests = null)
        {
            pacing = pacing ?? ResearchPacing.Production;
            var zones = new List<string>();
            int habitats = 0;
            foreach (ZoneState z in s.Zones) if (z.Unlocked) { zones.Add(z.Id); habitats += z.UnlockedHabitatIds.Count; }
            List<QuestStatusView> q = QuestProposals.Evaluate(s, log, pacing, quests);
            int done = 0, ready = 0, prog = 0, locked = 0;
            var goals = new List<string>();
            foreach (QuestStatusView v in q)
            {
                switch (v.State)
                {
                    case QuestState.Completed: done++; break;
                    case QuestState.Ready: ready++; break;
                    case QuestState.InProgress: prog++; break;
                    default: locked++; break;
                }
            }
            // Gợi ý: nhiệm vụ cổng đang làm trước, rồi nhiệm vụ khác gần xong nhất.
            var open = q.FindAll(v => v.State == QuestState.InProgress || v.State == QuestState.Ready);
            open.Sort((a, b) => a.IsCanonicalGate != b.IsCanonicalGate ? (a.IsCanonicalGate ? -1 : 1) : b.ProgressPct != a.ProgressPct ? b.ProgressPct.CompareTo(a.ProgressPct) : string.CompareOrdinal(a.QuestId, b.QuestId));
            foreach (QuestStatusView v in open) { if (goals.Count >= 3) break; goals.Add("quest." + v.QuestId + ".name"); }
            if (goals.Count < 3)
                foreach (CodexEntry e in s.Codex)
                {
                    string next = ResearchProgress.Species(s, e.SpeciesId, log, pacing).NextStepKey;
                    if (next != "research.next.done" && !goals.Contains(next)) goals.Add(next);
                    if (goals.Count >= 3) break;
                }
            return new ProgressionOverviewDto
            {
                UnlockedZones = zones, UnlockedHabitats = habitats, TotalHabitats = HabitatCatalog.All.Count,
                SpeciesDiscovered = Codex.SpeciesDiscovered(s), SpeciesTotal = SpeciesCatalog.All.Count,
                CodexCells = Codex.CellsDiscovered(s), CodexCellsTotal = SpeciesCatalog.All.Count * Economy.Mutation.Order.Count,
                ResearchTiers = ResearchProgress.TierCounts(s, log, pacing),
                QuestsCompleted = done, QuestsReady = ready, QuestsInProgress = prog, QuestsLocked = locked,
                Materials = Ecosystem.Materials(s), SuggestedGoals = goals, Rules = Rules(pacing, null, quests),
            };
        }
    }
}
