using System;
using System.Collections.Generic;
using Dravio.Domain.Behavior;
using Dravio.Domain.Biology;
using Dravio.Domain.Catalog;
using Dravio.Domain.Economy;

namespace Dravio.Domain.Progression
{
    // U2.10 Workstream C — World Progression (ADR 0204 Q1–Q8, ĐỀ XUẤT, CHỈ ĐỌC).
    // Điền nội dung cho QUEST_AQUATIC_EXPLORATION / QUEST_SKY_EXPLORATION (ADR 0201: "TBD") và thêm nhiệm vụ nghiên cứu, bảo tồn, thám hiểm.
    // KHÔNG gọi WorldExpansion.ApplyQuestComplete, KHÔNG đổi GameState. Mở thế giới vẫn chỉ qua lệnh unlockZone với đủ điều kiện canonical
    // (hướng dẫn, 3 sinh cảnh Khởi Nguyên, nhiệm vụ, thế giới trước). Không có yêu cầu vàng hay chờ đồng hồ.

    public enum QuestCategory
    {
        [WireName("origin")] Origin,
        [WireName("aquatic")] Aquatic,
        [WireName("sky")] Sky,
        [WireName("research")] Research,
        [WireName("conservation")] Conservation,
        [WireName("expedition")] Expedition,
    }

    /// <summary>
    /// Loại yêu cầu. Cố ý KHÔNG có "đủ vàng" hay "chờ N giờ" (Q2). Mỗi loại đọc dữ liệu canonical (save v8) hoặc sổ nghiên cứu (đề xuất).
    /// </summary>
    public enum QuestRequirementKind
    {
        /// <summary>Số loài (lọc theo realm của microhabitat bản địa) đạt cấp nghiên cứu ≥ Tier.</summary>
        [WireName("researchTier")] ResearchTier,
        /// <summary>Có Adult (chưa thả) có kiểu di chuyển này đang ở một microhabitat (Zone) — thám hiểm theo khả năng sinh học.</summary>
        [WireName("adultWithLocomotion")] AdultWithLocomotion,
        /// <summary>Có đủ vật liệu trong kho. Đề xuất: được trừ khi nhận hoàn thành nhiệm vụ (chưa có lệnh).</summary>
        [WireName("materialDelivery")] MaterialDelivery,
        /// <summary>Số cư dân ở sinh cảnh Good (suitability ≥ 80) — bảo tồn.</summary>
        [WireName("residentsThriving")] ResidentsThriving,
        /// <summary>Số cư dân đang ở microhabitat ưa thích của chúng.</summary>
        [WireName("residentsAtHome")] ResidentsAtHome,
        /// <summary>Số loài có ghi chép đủ chu kỳ ngày đêm (cả pha hoạt động lẫn nghỉ).</summary>
        [WireName("circadianRecords")] CircadianRecords,
        /// <summary>Số cá thể đã hé lộ đủ cá tính và nơi ưa thích.</summary>
        [WireName("individualsKnown")] IndividualsKnown,
        /// <summary>Số loài có ghi chép ở microhabitat thuộc realm cho trước.</summary>
        [WireName("speciesRecordedInRealm")] SpeciesRecordedInRealm,
        /// <summary>Tuyến thám hiểm có thể khởi hành (mở thế giới + bảng thưởng đã duyệt). Dùng để thể hiện tuyến khóa, không để vượt khóa.</summary>
        [WireName("routeAvailable")] RouteAvailable,
    }

    public enum RequirementState
    {
        [WireName("met")] Met,
        [WireName("unmet")] Unmet,
        /// <summary>Không đánh giá được vì dữ liệu chưa có (sổ nghiên cứu tắt / chưa lưu).</summary>
        [WireName("unknown")] Unknown,
        /// <summary>Bị chặn bởi một luật canonical chưa duyệt (vd tuyến sky_cloud chưa có bảng thưởng).</summary>
        [WireName("blocked")] Blocked,
    }

    public sealed class QuestRequirementDef
    {
        internal QuestRequirementDef(QuestRequirementKind kind, int target, string param = null, ResearchTier tier = ResearchTier.Unknown, Realm? realm = null, Locomotion? locomotion = null)
        {
            Kind = kind; Target = target; Param = param; Tier = tier; Realm = realm; Locomotion = locomotion;
        }
        public QuestRequirementKind Kind { get; }
        public int Target { get; }
        /// <summary>ID vật liệu (MaterialDelivery), tuyến (RouteAvailable).</summary>
        public string Param { get; }
        public ResearchTier Tier { get; }
        public Realm? Realm { get; }
        public Locomotion? Locomotion { get; }
        /// <summary>quest.req.&lt;kind&gt;.</summary>
        public string Key => "quest.req." + Kind.ToWire();
    }

    /// <summary>Phần thưởng không lạm phát (Q5): không vàng, không tiền tệ mới.</summary>
    public enum QuestRewardKind
    {
        /// <summary>Hoàn thành nhiệm vụ canonical (mở được unlockZone khi đủ điều kiện khác).</summary>
        [WireName("worldGate")] WorldGate,
        /// <summary>Ghi chú Codex (trang trình diễn).</summary>
        [WireName("codexNote")] CodexNote,
        /// <summary>Mẫu trang trí thẩm mỹ (không hiệu ứng, ADR 0201 mục 6); cần ADR chế tạo để dùng vật liệu.</summary>
        [WireName("decorBlueprint")] DecorBlueprint,
        /// <summary>Danh hiệu nhà nghiên cứu / bảo tồn (hiển thị).</summary>
        [WireName("title")] Title,
    }

    public sealed class QuestProposalDef
    {
        internal QuestProposalDef(string id, QuestCategory cat, string[] requiresZones, string[] prerequisites, QuestRewardKind reward, params QuestRequirementDef[] reqs)
        {
            Id = id; Category = cat; RequiresZones = Array.AsReadOnly(requiresZones); Prerequisites = Array.AsReadOnly(prerequisites); Reward = reward; Requirements = Array.AsReadOnly(reqs);
        }
        public string Id { get; }
        public QuestCategory Category { get; }
        /// <summary>Thế giới phải mở để nhiệm vụ hiện (nội dung chỉ dùng thế giới này, không vòng lặp với khóa thế giới).</summary>
        public IReadOnlyList<string> RequiresZones { get; }
        public IReadOnlyList<string> Prerequisites { get; }
        public QuestRewardKind Reward { get; }
        public IReadOnlyList<QuestRequirementDef> Requirements { get; }
        /// <summary>true = ID có trong WorldUnlockCatalog.Quests (cổng canonical ADR 0201).</summary>
        public bool IsCanonicalGate => WorldUnlockCatalog.IsQuestId(Id);
        public string NameKey => "quest." + Id + ".name";
    }

    /// <summary>Bộ luật nhiệm vụ. Production TẮT: không nhiệm vụ nào được hoàn thành bởi hệ đề xuất.</summary>
    public sealed class QuestRules
    {
        public QuestRules(bool enabled) { Enabled = enabled; }
        public bool Enabled { get; }
        public static readonly QuestRules Production = new QuestRules(false);
        public static readonly QuestRules ProposedForReview = new QuestRules(true);
    }

    public sealed class QuestRequirementStatus
    {
        public QuestRequirementDef Def { get; internal set; }
        public RequirementState State { get; internal set; }
        public int Current { get; internal set; }
        public int Target { get; internal set; }
        /// <summary>Lý do ngắn khi Blocked/Unknown (vd research-disabled, route-rewards-undefined).</summary>
        public string Detail { get; internal set; }
    }

    public enum QuestState
    {
        [WireName("completed")] Completed,
        /// <summary>Đủ mọi yêu cầu. Production không tự hoàn thành (QuestRules tắt).</summary>
        [WireName("ready")] Ready,
        [WireName("inProgress")] InProgress,
        /// <summary>Thế giới / nhiệm vụ trước chưa xong.</summary>
        [WireName("locked")] Locked,
    }

    public sealed class QuestStatusView
    {
        public string QuestId { get; internal set; }
        public QuestCategory Category { get; internal set; }
        public QuestState State { get; internal set; }
        public bool IsCanonicalGate { get; internal set; }
        public IReadOnlyList<string> LockedBy { get; internal set; }
        public IReadOnlyList<QuestRequirementStatus> Requirements { get; internal set; }
        public QuestRewardKind Reward { get; internal set; }
        /// <summary>Luôn false ở production (QuestRules tắt). Hoàn thành thật cần lệnh mới sau khi ADR 0204 được duyệt.</summary>
        public bool CanComplete { get; internal set; }
        public int ProgressPct { get; internal set; }
    }

    public static class QuestProposals
    {
        private static readonly string[] Origin = { "origin" };
        private static readonly string[] OriginOcean = { "origin", "ocean" };
        private static readonly string[] OriginOceanSky = { "origin", "ocean", "sky" };
        private static readonly string[] None = new string[0];

        private static QuestRequirementDef Research(int n, ResearchTier t, Realm? realm = null) => new QuestRequirementDef(QuestRequirementKind.ResearchTier, n, tier: t, realm: realm);
        private static QuestRequirementDef Material(string id, int n) => new QuestRequirementDef(QuestRequirementKind.MaterialDelivery, n, param: id);

        /// <summary>Catalog đề xuất (thứ tự hiển thị). Chỉ dùng ID vật liệu, tuyến, microhabitat, loài canonical.</summary>
        public static readonly IReadOnlyList<QuestProposalDef> All = Array.AsReadOnly(new[]
        {
            // ---- Khởi Nguyên ----
            new QuestProposalDef("Q_ORIGIN_FIELD_NOTES", QuestCategory.Origin, Origin, None, QuestRewardKind.CodexNote,
                Research(2, ResearchTier.Profiled, Realm.Land)),
            new QuestProposalDef("Q_ORIGIN_FOREST_SURVEY", QuestCategory.Expedition, Origin, None, QuestRewardKind.DecorBlueprint,
                new QuestRequirementDef(QuestRequirementKind.AdultWithLocomotion, 1, locomotion: Locomotion.Walk),
                Material("wood", 3), Material("seed", 2)),
            // ---- Thủy Vực (cổng canonical) — chỉ dùng nội dung Khởi Nguyên ----
            new QuestProposalDef("QUEST_AQUATIC_EXPLORATION", QuestCategory.Aquatic, Origin, new[] { "Q_ORIGIN_FIELD_NOTES" }, QuestRewardKind.WorldGate,
                Research(3, ResearchTier.LifeCycle, Realm.Land),
                new QuestRequirementDef(QuestRequirementKind.AdultWithLocomotion, 1, locomotion: Locomotion.Swim),
                new QuestRequirementDef(QuestRequirementKind.CircadianRecords, 2),
                Material("wood", 6), Material("seed", 4)),
            // ---- Thiên Giới (cổng canonical) — dùng nội dung Khởi Nguyên + Thủy Vực ----
            new QuestProposalDef("QUEST_SKY_EXPLORATION", QuestCategory.Sky, OriginOcean, new[] { "QUEST_AQUATIC_EXPLORATION" }, QuestRewardKind.WorldGate,
                Research(1, ResearchTier.Ethology, Realm.Ocean),
                new QuestRequirementDef(QuestRequirementKind.SpeciesRecordedInRealm, 2, realm: Realm.Ocean),
                new QuestRequirementDef(QuestRequirementKind.AdultWithLocomotion, 1, locomotion: Locomotion.Fly),
                Material("crystal", 4), Material("coral", 4)),
            // ---- Nghiên cứu ----
            new QuestProposalDef("Q_RESEARCH_NIGHT_WATCH", QuestCategory.Research, Origin, new[] { "Q_ORIGIN_FIELD_NOTES" }, QuestRewardKind.Title,
                new QuestRequirementDef(QuestRequirementKind.CircadianRecords, 4)),
            new QuestProposalDef("Q_RESEARCH_PERSONALITIES", QuestCategory.Research, Origin, new[] { "Q_ORIGIN_FIELD_NOTES" }, QuestRewardKind.CodexNote,
                new QuestRequirementDef(QuestRequirementKind.IndividualsKnown, 4)),
            new QuestProposalDef("Q_RESEARCH_ETHOLOGIST", QuestCategory.Research, Origin, new[] { "Q_RESEARCH_NIGHT_WATCH" }, QuestRewardKind.Title,
                Research(3, ResearchTier.Ethology)),
            // ---- Bảo tồn ----
            new QuestProposalDef("Q_CONSERVE_AT_HOME", QuestCategory.Conservation, Origin, None, QuestRewardKind.DecorBlueprint,
                new QuestRequirementDef(QuestRequirementKind.ResidentsAtHome, 3),
                new QuestRequirementDef(QuestRequirementKind.ResidentsThriving, 5)),
            new QuestProposalDef("Q_CONSERVE_TIDES", QuestCategory.Conservation, OriginOcean, new[] { "QUEST_AQUATIC_EXPLORATION" }, QuestRewardKind.DecorBlueprint,
                new QuestRequirementDef(QuestRequirementKind.ResidentsThriving, 3, realm: Realm.Ocean)),
            // ---- Thám hiểm theo khả năng sinh học ----
            new QuestProposalDef("Q_EXPEDITION_REEF", QuestCategory.Expedition, OriginOcean, new[] { "QUEST_AQUATIC_EXPLORATION" }, QuestRewardKind.CodexNote,
                new QuestRequirementDef(QuestRequirementKind.AdultWithLocomotion, 1, locomotion: Locomotion.Swim, realm: Realm.Ocean),
                Material("coral", 3)),
            new QuestProposalDef("Q_EXPEDITION_CLOUD", QuestCategory.Expedition, OriginOceanSky, new[] { "QUEST_SKY_EXPLORATION" }, QuestRewardKind.CodexNote,
                new QuestRequirementDef(QuestRequirementKind.RouteAvailable, 1, param: "sky_cloud"),
                new QuestRequirementDef(QuestRequirementKind.AdultWithLocomotion, 1, locomotion: Locomotion.Fly, realm: Realm.Sky)),
        });

        public static QuestProposalDef Get(string id)
        {
            foreach (QuestProposalDef q in All) if (q.Id == id) return q;
            return null;
        }

        /// <summary>
        /// Lỗi thiết kế của catalog (rỗng = hợp lệ). Kiểm Q3/Q4: nhiệm vụ cổng chỉ dùng nội dung thế giới đã mở theo luật canonical,
        /// vật liệu có nguồn ở thế giới đã mở, không phụ thuộc vòng, ID hợp lệ.
        /// </summary>
        public static List<string> ValidateCatalog()
        {
            var e = new List<string>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (QuestProposalDef q in All)
            {
                if (!ids.Add(q.Id)) e.Add(q.Id + ": duplicate");
                foreach (string p in q.Prerequisites) if (Get(p) == null) e.Add(q.Id + ": unknown prerequisite " + p);
                foreach (string z in q.RequiresZones) if (!WorldCatalog.IsZoneId(z)) e.Add(q.Id + ": unknown zone " + z);
                if (q.Requirements.Count == 0) e.Add(q.Id + ": no requirements");
                var open = new HashSet<string>(q.RequiresZones, StringComparer.Ordinal);
                // Cổng canonical: thế giới nó mở KHÔNG được nằm trong RequiresZones (không vòng).
                foreach (ZoneUnlockRule r in WorldUnlockCatalog.Rules)
                    if (r.RequiresQuest == q.Id && open.Contains(r.ZoneId)) e.Add(q.Id + ": requires the zone it unlocks (" + r.ZoneId + ")");
                foreach (QuestRequirementDef req in q.Requirements)
                {
                    if (req.Target <= 0) e.Add(q.Id + ": non-positive target");
                    if (req.Realm.HasValue && !RealmOpen(req.Realm.Value, open)) e.Add(q.Id + ": requirement uses closed realm " + req.Realm.Value);
                    if (req.Kind == QuestRequirementKind.MaterialDelivery)
                    {
                        bool known = false, sourced = false;
                        foreach (MaterialDef m in ExpeditionCatalog.Materials) if (m.Id == req.Param) known = m.Active;
                        foreach (ExpeditionRoute route in ExpeditionCatalog.Routes)
                            if (open.Contains(route.ZoneId) && route.RewardsDefined && BehaviorCatalog.Has(route.Materials, req.Param)) sourced = true;
                        if (!known) e.Add(q.Id + ": inactive or unknown material " + req.Param);
                        else if (!sourced) e.Add(q.Id + ": material " + req.Param + " has no source in open zones");
                    }
                    if (req.Kind == QuestRequirementKind.AdultWithLocomotion && req.Locomotion.HasValue)
                    {
                        // Phải có ít nhất một loài có kiểu di chuyển này sống được ở thế giới đã mở.
                        bool any = false;
                        foreach (DragonSpecies sp in SpeciesCatalog.All)
                        {
                            if (!BehaviorCatalog.Has(sp.Biology.Locomotion, req.Locomotion.Value)) continue;
                            foreach (string h in sp.HabitatRules)
                                if (open.Contains(HabitatCatalog.ZoneOf(h)) && (!req.Realm.HasValue || RealmOfHabitat(h) == req.Realm.Value)) any = true;
                        }
                        if (!any) e.Add(q.Id + ": no species with " + req.Locomotion.Value + " can live in open zones");
                    }
                    if (req.Kind == QuestRequirementKind.RouteAvailable && !ExpeditionCatalog.TryGet(req.Param, out _)) e.Add(q.Id + ": unknown route " + req.Param);
                }
                // Tiền đề không vòng.
                if (Reaches(q.Id, q.Id, new HashSet<string>(StringComparer.Ordinal), first: true)) e.Add(q.Id + ": prerequisite cycle");
            }
            return e;
        }

        private static bool Reaches(string from, string target, HashSet<string> seen, bool first)
        {
            if (!first && from == target) return true;
            if (!seen.Add(from)) return false;
            QuestProposalDef q = Get(from);
            if (q == null) return false;
            foreach (string p in q.Prerequisites) if (Reaches(p, target, seen, false)) return true;
            return false;
        }

        private static bool RealmOpen(Realm r, HashSet<string> zones) =>
            (r == Realm.Land && zones.Contains("origin")) || (r == Realm.Ocean && zones.Contains("ocean")) || (r == Realm.Sky && zones.Contains("sky"));

        private static Realm RealmOfHabitat(string habitatId) => HabitatCatalog.TryGet(habitatId, out HabitatProfile h) ? h.Realm : Realm.Land;

        // ------------------------------------------------------------------ đánh giá

        public static List<QuestStatusView> Evaluate(GameState s, ResearchLog log = null, ResearchPacing pacing = null, QuestRules rules = null)
        {
            var o = new List<QuestStatusView>();
            foreach (QuestProposalDef q in All) o.Add(Evaluate(s, q, log, pacing, rules));
            return o;
        }

        public static QuestStatusView Evaluate(GameState s, QuestProposalDef q, ResearchLog log, ResearchPacing pacing, QuestRules rules)
        {
            pacing = pacing ?? ResearchPacing.Production;
            rules = rules ?? QuestRules.Production;
            var locked = new List<string>();
            foreach (string z in q.RequiresZones) if (s.FindZone(z)?.Unlocked != true) locked.Add("zone:" + z);
            foreach (string p in q.Prerequisites)
                if (!(WorldUnlockCatalog.IsQuestId(p) ? s.CompletedQuests.Contains(p) : Evaluate(s, Get(p), log, pacing, rules).State is QuestState st && (st == QuestState.Completed || st == QuestState.Ready)))
                    locked.Add("quest:" + p);
            var reqs = new List<QuestRequirementStatus>();
            int pctSum = 0;
            foreach (QuestRequirementDef r in q.Requirements)
            {
                QuestRequirementStatus st = Requirement(s, r, log, pacing);
                reqs.Add(st);
                pctSum += st.State == RequirementState.Met ? 100 : st.Target > 0 ? Math.Min(99, st.Current * 100 / st.Target) : 0;
            }
            bool completed = q.IsCanonicalGate && s.CompletedQuests.Contains(q.Id);
            bool allMet = reqs.TrueForAll(x => x.State == RequirementState.Met);
            QuestState state = completed ? QuestState.Completed : locked.Count > 0 ? QuestState.Locked : allMet ? QuestState.Ready : QuestState.InProgress;
            return new QuestStatusView
            {
                QuestId = q.Id, Category = q.Category, State = state, IsCanonicalGate = q.IsCanonicalGate, LockedBy = locked, Requirements = reqs, Reward = q.Reward,
                CanComplete = rules.Enabled && state == QuestState.Ready, ProgressPct = completed ? 100 : q.Requirements.Count == 0 ? 0 : pctSum / q.Requirements.Count,
            };
        }

        private static IEnumerable<DragonState> Kept(GameState s)
        {
            foreach (DragonState d in s.Dragons) if (d.Location.Kind != LocationKind.Released) yield return d;
        }

        private static bool SpeciesInRealm(string speciesId, Realm realm)
        {
            foreach (string h in SpeciesCatalog.GetBiology(speciesId).HabitatRules) if (RealmOfHabitat(h) == realm) return true;
            return false;
        }

        private static QuestRequirementStatus Requirement(GameState s, QuestRequirementDef r, ResearchLog log, ResearchPacing pacing)
        {
            var st = new QuestRequirementStatus { Def = r, Target = r.Target };
            bool needsLog = r.Kind == QuestRequirementKind.CircadianRecords || r.Kind == QuestRequirementKind.IndividualsKnown ||
                r.Kind == QuestRequirementKind.SpeciesRecordedInRealm || (r.Kind == QuestRequirementKind.ResearchTier && r.Tier == ResearchTier.Ethology);
            if (needsLog && (!pacing.Enabled || log == null))
            {
                st.State = RequirementState.Unknown; st.Detail = pacing.Enabled ? "research-log-missing" : "research-disabled";
                return st;
            }
            int n = 0;
            switch (r.Kind)
            {
                case QuestRequirementKind.ResearchTier:
                    foreach (CodexEntry e in s.Codex)
                    {
                        if (r.Realm.HasValue && !SpeciesInRealm(e.SpeciesId, r.Realm.Value)) continue;
                        if (ResearchProgress.Species(s, e.SpeciesId, log, pacing).Tier >= r.Tier) n++;
                    }
                    break;
                case QuestRequirementKind.AdultWithLocomotion:
                    foreach (DragonState d in Kept(s))
                    {
                        if (d.Location.Kind != LocationKind.Zone) continue;
                        if (r.Realm.HasValue && RealmOfHabitat(d.Location.HabitatId) != r.Realm.Value) continue;
                        if (!BehaviorCatalog.Has(SpeciesCatalog.GetBiology(d.SpeciesId).Locomotion, r.Locomotion.Value)) continue;
                        if (EconomyRates.GrowthStageOf(d.XpSec, SpeciesCatalog.Get(d.SpeciesId)) == GrowthStage.Adult) n++;
                    }
                    break;
                case QuestRequirementKind.MaterialDelivery:
                    n = s.Player.Inventory.TryGetValue(r.Param, out long have) ? (int)Math.Min(int.MaxValue, have) : 0;
                    break;
                case QuestRequirementKind.ResidentsThriving:
                    foreach (DragonState d in Kept(s))
                    {
                        if (d.Location.Kind != LocationKind.Zone || !HabitatCatalog.Contains(d.Location.HabitatId)) continue;
                        if (r.Realm.HasValue && RealmOfHabitat(d.Location.HabitatId) != r.Realm.Value) continue;
                        if (Suitability.TierOf(Suitability.ForSpecies(d.SpeciesId, d.Location.HabitatId)) == SuitabilityTier.Good) n++;
                    }
                    break;
                case QuestRequirementKind.ResidentsAtHome:
                    foreach (DragonState d in Kept(s))
                        if (d.Location.Kind == LocationKind.Zone && PersonalityProfiles.Of(d).FavoriteHabitatId == d.Location.HabitatId) n++;
                    break;
                case QuestRequirementKind.CircadianRecords:
                    foreach (CodexEntry e in s.Codex) if (ResearchProgress.Species(s, e.SpeciesId, log, pacing).CircadianCovered) n++;
                    break;
                case QuestRequirementKind.IndividualsKnown:
                    foreach (DragonState d in Kept(s))
                    {
                        IndividualResearchProfile p = ResearchProgress.Individual(s, d.Uid, log, pacing);
                        if (p != null && BehaviorCatalog.Has(p.RevealedTraits, IndividualTrait.Archetype) && BehaviorCatalog.Has(p.RevealedTraits, IndividualTrait.FavoriteHabitat)) n++;
                    }
                    break;
                case QuestRequirementKind.SpeciesRecordedInRealm:
                    foreach (SpeciesResearchRecord rec in log.AllSpecies)
                    {
                        bool any = false;
                        foreach (string h in rec.Habitats) if (RealmOfHabitat(h) == r.Realm.Value) any = true;
                        if (any) n++;
                    }
                    break;
                case QuestRequirementKind.RouteAvailable:
                    ExpeditionCatalog.TryGet(r.Param, out ExpeditionRoute route);
                    if (route != null && !route.RewardsDefined) { st.State = RequirementState.Blocked; st.Detail = "route-rewards-undefined"; return st; }
                    n = route != null && s.FindZone(route.ZoneId)?.Unlocked == true ? 1 : 0;
                    break;
            }
            st.Current = n;
            st.State = n >= r.Target ? RequirementState.Met : RequirementState.Unmet;
            return st;
        }
    }
}
