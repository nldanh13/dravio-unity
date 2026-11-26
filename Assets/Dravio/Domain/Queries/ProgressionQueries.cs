using System.Collections.Generic;
using Dravio.Domain.Behavior;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Economy;
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
        /// <summary>Codex khóa theo cấp nghiên cứu (R1–R9). false từ U2.10 (chưa duyệt), Codex mở đủ.</summary>
        public bool ResearchPacingEnabled { get; internal set; }
        /// <summary>Sổ nghiên cứu ghi phát hiện (lệnh quan sát). true từ U2.10.</summary>
        public bool ResearchRecordingEnabled { get; internal set; }
        /// <summary>Nhật ký đêm R11. true từ U2.10.</summary>
        public bool NightJournalEnabled { get; internal set; }
        public bool SocialGameplayEffectsEnabled { get; internal set; }
        /// <summary>Lệnh completeQuest. true từ U2.11 (ADR 0206).</summary>
        public bool QuestCompletionEnabled { get; internal set; }
        /// <summary>Độ quen theo thời gian sống chung được lưu (chỉ trình diễn). true từ U2.11.</summary>
        public bool RelationsRecordingEnabled { get; internal set; }
        /// <summary>Mốc thưởng thám hiểm theo tuyến. true từ U2.11.</summary>
        public bool ExplorationMilestonesEnabled { get; internal set; }
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

    /// <summary>U2.10 E1 + E5: gói Starter còn mua hôm nay và dự án Sanctuary tiếp theo.</summary>
    public sealed class SanctuaryEconomyDto
    {
        public int StarterPacksLeftToday { get; internal set; }
        public int StarterDailyLimit { get; internal set; }
        public long StarterPriceGoldMicro { get; internal set; }
        public int ProjectsBuilt { get; internal set; }
        public int ProjectsMax { get; internal set; }
        /// <summary>null khi đã xây đủ.</summary>
        public long? NextProjectCostGoldMicro { get; internal set; }
        /// <summary>Số gỗ và số hạt giống (mỗi loại) dự án tiếp theo cần.</summary>
        public int NextProjectMaterialEach { get; internal set; }
        public IReadOnlyList<string> NextProjectMaterialIds { get; internal set; }
        /// <summary>null = xây được; ngược lại mã lỗi wire (not-enough-gold, not-enough-materials, projects-maxed).</summary>
        public string NextProjectBlockedBy { get; internal set; }
        /// <summary>U2.11: nội dung trang trí của dự án kế tiếp (null khi đã xây đủ).</summary>
        public SanctuaryProjectDef NextProject { get; internal set; }
    }

    /// <summary>U2.11: thưởng đã nhận (dẫn xuất, không lưu riêng).</summary>
    public sealed class RewardCollectionDto
    {
        public IReadOnlyList<string> CodexNotes { get; internal set; }
        public IReadOnlyList<string> DecorBlueprints { get; internal set; }
        /// <summary>Danh hiệu đã đạt (nhiệm vụ, khám phá, nghiên cứu, thám hiểm).</summary>
        public IReadOnlyList<string> Titles { get; internal set; }
    }

    /// <summary>U2.11: sổ nhiệm vụ cho màn hành trình.</summary>
    public sealed class QuestLogDto
    {
        public IReadOnlyList<QuestStatusView> Quests { get; internal set; }
        /// <summary>Nhiệm vụ có thể nhận ngay (gửi completeQuest).</summary>
        public IReadOnlyList<string> ReadyQuestIds { get; internal set; }
        public RewardCollectionDto Rewards { get; internal set; }
        /// <summary>Điều kiện còn thiếu để mở từng thế giới đang khóa (mã của WorldExpansion.MissingRequirements). Rỗng = gửi unlockZone được.</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<string>> ZoneUnlockMissing { get; internal set; }
    }

    /// <summary>U2.11: tiến trình nghiên cứu sinh học theo thế giới.</summary>
    public sealed class RealmResearchDto
    {
        public Realm Realm { get; internal set; }
        /// <summary>Số loài sống được ở thế giới này (có microhabitat bản địa thuộc realm).</summary>
        public int SpeciesTotal { get; internal set; }
        public int SpeciesDiscovered { get; internal set; }
        public int LifeCycle { get; internal set; }
        public int Ethology { get; internal set; }
        /// <summary>0–100: trung bình cấp nghiên cứu / 4 trên mọi loài của realm (loài chưa gặp tính 0).</summary>
        public int ProgressPct { get; internal set; }
    }

    public sealed class ResearchProgressionDto
    {
        public IReadOnlyDictionary<ResearchTier, int> Tiers { get; internal set; }
        public IReadOnlyList<RealmResearchDto> Realms { get; internal set; }
        /// <summary>Danh hiệu nghiên cứu và khám phá (kể cả chưa đạt, có tiến độ).</summary>
        public IReadOnlyList<TitleView> Titles { get; internal set; }
    }

    /// <summary>U2.11: tiến độ một tuyến thám hiểm.</summary>
    public sealed class RouteExplorationDto
    {
        public string RouteId { get; internal set; }
        public bool Open { get; internal set; }
        /// <summary>false = tuyến khóa vì bảng thưởng chưa duyệt (sky_cloud).</summary>
        public bool RewardsDefined { get; internal set; }
        public int Claimed { get; internal set; }
        /// <summary>null khi đã qua mọi mốc.</summary>
        public int? NextMilestoneAt { get; internal set; }
        public int NextMilestoneBonusEach { get; internal set; }
        public string NextMilestoneRewardId { get; internal set; }
    }

    /// <summary>U2.11: quan hệ của một cá thể với các cá thể khác trong kho, độ quen giảm dần.</summary>
    public sealed class RelationsDto
    {
        public string Uid { get; internal set; }
        public IReadOnlyList<SocialRelation> Relations { get; internal set; }
    }

    /// <summary>U2.11: một dự án Sanctuary (đã xây hoặc kế tiếp) kèm nội dung hiển thị.</summary>
    public sealed class SanctuaryProjectView
    {
        public int Index { get; internal set; }
        public SanctuaryProjectDef Content { get; internal set; }
        public bool Built { get; internal set; }
        /// <summary>Thế giới hiển thị (thế giới của dự án nếu đã mở, ngược lại origin).</summary>
        public string DisplayZoneId { get; internal set; }
        /// <summary>true = đã có mẫu trang trí đổi kiểu dáng (thưởng nhiệm vụ).</summary>
        public bool StyleVariant { get; internal set; }
    }

    /// <summary>
    /// API chỉ đọc U2.10 (ADR 0204 E1–E5). Cùng nguyên tắc với <see cref="BehaviorQueries"/>: snapshot, không đổi state, không lệnh.
    /// <c>log</c> null = sổ nghiên cứu của save (<see cref="GameState.Research"/>, Schema v9).
    /// Mọi tham số luật mặc định là Production.
    /// </summary>
    public static class ProgressionQueries
    {
        public static SanctuaryEconomyDto SanctuaryEconomy(GameState s, long nowMs)
        {
            int n = s.SanctuaryProjectsBuilt;
            long? cost = SanctuaryProjectCatalog.CostG(n);
            DomainErrorCode? blocked = Economy.SanctuaryProjects.Check(s);
            return new SanctuaryEconomyDto
            {
                StarterPacksLeftToday = PackDailyLimit.StarterLeftToday(s, nowMs), StarterDailyLimit = PackCatalog.StarterDailyLimit,
                StarterPriceGoldMicro = MicroGold.FromWholeGold(PackCatalog.Get("starter").PriceG).Micro,
                ProjectsBuilt = n, ProjectsMax = SanctuaryProjectCatalog.Max,
                NextProjectCostGoldMicro = cost == null ? (long?)null : MicroGold.FromWholeGold(cost.Value).Micro,
                NextProjectMaterialEach = cost == null ? 0 : SanctuaryProjectCatalog.MaterialEach(n),
                NextProjectMaterialIds = SanctuaryProjectCatalog.MaterialIds,
                NextProjectBlockedBy = blocked == null ? null : WireNames.ToWire(blocked.Value),
                NextProject = SanctuaryProjectContent.Get(n),
            };
        }

        public static ResearchProfileDto ResearchProfile(GameState s, string speciesId, ResearchLog log = null, ResearchPacing pacing = null)
        {
            pacing = pacing ?? ResearchPacing.Production;
            log = log ?? s.Research;
            var inds = new List<IndividualResearchProfile>();
            foreach (DragonState d in s.Dragons)
                if (d.SpeciesId == speciesId && d.Location.Kind != LocationKind.Released) inds.Add(ResearchProgress.Individual(s, d.Uid, log, pacing));
            return new ResearchProfileDto { Species = ResearchProgress.Species(s, speciesId, log, pacing), Individuals = inds };
        }

        public static IndividualResearchProfile IndividualResearch(GameState s, string uid, ResearchLog log = null, ResearchPacing pacing = null) =>
            ResearchProgress.Individual(s, uid, log ?? s.Research, pacing ?? ResearchPacing.Production);

        public static List<ObservationOpportunity> ObservationOpportunities(GameState s, long nowMs, DayPhase phase, ResearchLog log = null, ResearchPacing pacing = null) =>
            ResearchProgress.Opportunities(s, nowMs, phase, log ?? s.Research, pacing ?? ResearchPacing.Production);

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
            QuestProposals.Evaluate(s, log ?? s.Research, pacing, rules);

        public static RuleFlagsDto Rules(ResearchPacing pacing = null, SocialRules social = null, QuestRules quests = null) => new RuleFlagsDto
        {
            ResearchPacingEnabled = (pacing ?? ResearchPacing.Production).CodexGated,
            ResearchRecordingEnabled = (pacing ?? ResearchPacing.Production).Enabled,
            NightJournalEnabled = (pacing ?? ResearchPacing.Production).Enabled && (pacing ?? ResearchPacing.Production).NightJournal,
            SocialGameplayEffectsEnabled = (social ?? SocialRules.Production).GameplayEffectsEnabled,
            QuestCompletionEnabled = (quests ?? QuestRules.Production).Enabled,
            RelationsRecordingEnabled = true,
            ExplorationMilestonesEnabled = true,
            ComfortDriftEnabled = EcologyComfortRules.Production.Enabled,
            NaturalDisastersEnabled = DisasterRules.Production.Enabled,
            EnergyDrainEnabled = false,
        };

        public static ProgressionOverviewDto Overview(GameState s, ResearchLog log = null, ResearchPacing pacing = null, QuestRules quests = null)
        {
            pacing = pacing ?? ResearchPacing.Production;
            log = log ?? s.Research;
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

        // ---------------- U2.11 (ADR 0206) ----------------

        public static RewardCollectionDto Rewards(GameState s)
        {
            var notes = new List<string>(); var blueprints = new List<string>(); var titles = new List<string>();
            foreach (QuestProposalDef q in QuestSystem.Rewards(s))
            {
                if (q.Reward == QuestRewardKind.CodexNote) notes.Add(q.RewardId);
                else if (q.Reward == QuestRewardKind.DecorBlueprint) blueprints.Add(q.RewardId);
            }
            foreach (string id in Exploration.EarnedRewardIds(s)) if (id.StartsWith("note.", System.StringComparison.Ordinal)) notes.Add(id);
            foreach (TitleView tv in Exploration.Titles(s)) if (tv.Earned && !titles.Contains(tv.TitleId)) titles.Add(tv.TitleId);
            return new RewardCollectionDto { CodexNotes = notes, DecorBlueprints = blueprints, Titles = titles };
        }

        public static QuestLogDto QuestLog(GameState s)
        {
            List<QuestStatusView> q = QuestProposals.Evaluate(s);
            var ready = new List<string>();
            foreach (QuestStatusView v in q) if (v.CanComplete) ready.Add(v.QuestId);
            var missing = new Dictionary<string, IReadOnlyList<string>>();
            foreach (ZoneUnlockRule r in WorldUnlockCatalog.Rules)
                if (s.FindZone(r.ZoneId)?.Unlocked == false) missing[r.ZoneId] = WorldExpansion.MissingRequirements(s, r.ZoneId);
            return new QuestLogDto { Quests = q, ReadyQuestIds = ready, Rewards = Rewards(s), ZoneUnlockMissing = missing };
        }

        public static ResearchProgressionDto ResearchProgression(GameState s, ResearchLog log = null, ResearchPacing pacing = null)
        {
            log = log ?? s.Research;
            pacing = pacing ?? ResearchPacing.Production;
            var realms = new List<RealmResearchDto>();
            foreach (Realm realm in new[] { Realm.Land, Realm.Ocean, Realm.Sky })
            {
                var dto = new RealmResearchDto { Realm = realm };
                int tierSum = 0;
                foreach (DragonSpecies sp in SpeciesCatalog.All)
                {
                    bool lives = false;
                    foreach (string h in sp.HabitatRules) if (HabitatCatalog.TryGet(h, out HabitatProfile hp) && hp.Realm == realm) lives = true;
                    if (!lives) continue;
                    dto.SpeciesTotal++;
                    bool discovered = false;
                    foreach (CodexEntry e in s.Codex) if (e.SpeciesId == sp.Id) discovered = true;
                    if (!discovered) continue;
                    dto.SpeciesDiscovered++;
                    ResearchTier tier = ResearchProgress.Species(s, sp.Id, log, pacing).Tier;
                    if (tier >= ResearchTier.LifeCycle) dto.LifeCycle++;
                    if (tier >= ResearchTier.Ethology) dto.Ethology++;
                    tierSum += (int)tier;
                }
                dto.ProgressPct = dto.SpeciesTotal == 0 ? 0 : tierSum * 100 / (dto.SpeciesTotal * (int)ResearchTier.Ethology);
                realms.Add(dto);
            }
            return new ResearchProgressionDto { Tiers = ResearchProgress.TierCounts(s, log, pacing), Realms = realms, Titles = Exploration.Titles(s, log, pacing) };
        }

        public static List<RouteExplorationDto> ExplorationRoutes(GameState s)
        {
            var o = new List<RouteExplorationDto>();
            foreach (ExpeditionRoute r in ExpeditionCatalog.Routes)
            {
                ExplorationMilestoneDef next = Exploration.NextMilestone(s, r.Id);
                o.Add(new RouteExplorationDto
                {
                    RouteId = r.Id, Open = Expedition.Expeditions.RouteOpen(s, r), RewardsDefined = r.RewardsDefined, Claimed = Exploration.Claimed(s, r.Id),
                    NextMilestoneAt = next?.Claims, NextMilestoneBonusEach = next?.BonusEach ?? 0, NextMilestoneRewardId = next?.RewardId,
                });
            }
            return o;
        }

        public static RelationsDto Relations(GameState s, string uid)
        {
            DragonState d = s.FindDragon(uid);
            if (d == null || d.Location.Kind == LocationKind.Released) return null;
            var list = new List<SocialRelation>();
            foreach (DragonState x in s.Dragons)
                if (x.Uid != uid && x.Location.Kind != LocationKind.Released) list.Add(SocialEcology.Relation(s, d, x));
            list.Sort((a, b) => a.Familiarity != b.Familiarity ? b.Familiarity.CompareTo(a.Familiarity) : string.CompareOrdinal(a.UidA + a.UidB, b.UidA + b.UidB));
            return new RelationsDto { Uid = uid, Relations = list };
        }

        /// <summary>Dự án đã xây (thứ tự xây) và dự án kế tiếp (Built = false), kèm nội dung hiển thị.</summary>
        public static List<SanctuaryProjectView> SanctuaryProjects(GameState s)
        {
            var o = new List<SanctuaryProjectView>();
            int built = s.SanctuaryProjectsBuilt;
            for (int i = 0; i <= built && i < SanctuaryProjectCatalog.Max; i++)
            {
                SanctuaryProjectDef c = SanctuaryProjectContent.Get(i);
                o.Add(new SanctuaryProjectView
                {
                    Index = i, Content = c, Built = i < built, DisplayZoneId = SanctuaryProjectContent.DisplayZone(s, i),
                    StyleVariant = c.StyleBlueprintId != null && QuestSystem.HasReward(s, c.StyleBlueprintId),
                });
            }
            return o;
        }
    }
}
