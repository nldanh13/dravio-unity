using System;
using System.Collections.Generic;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Economy;

namespace Dravio.Domain.Behavior
{
    // U2.10 Workstream A — Research & Discovery (ADR 0204 R1–R9, ĐỀ XUẤT).
    // Mở rộng Research.cs của U2.9 (không phải hệ thống thứ hai): cùng ResearchTier, CodexSection, ObservableBehaviors.
    // Toàn bộ module CHỈ ĐỌC GameState. Sổ ghi chép ResearchLog nằm NGOÀI GameState và không vào save v8;
    // khối Schema v9 chỉ là bản nháp (ResearchSchemaDraft), chưa nối vào GameStateJson.

    /// <summary>Loại phát hiện mới của một lần quan sát.</summary>
    public enum ResearchFindingKind
    {
        /// <summary>Tập tính loài chưa từng ghi nhận.</summary>
        [WireName("behavior")] Behavior,
        /// <summary>Pha ngày đêm loài chưa từng được quan sát (chu kỳ ngày đêm).</summary>
        [WireName("phase")] Phase,
        /// <summary>Microhabitat (thuộc HabitatRules của loài) nơi loài chưa từng được quan sát (sinh cảnh phù hợp).</summary>
        [WireName("habitat")] Habitat,
        /// <summary>Một nét cá thể được hé lộ (cá tính, nhịp sinh học, nơi ưa thích, món ưa thích).</summary>
        [WireName("individual")] Individual,
    }

    /// <summary>Nét cá thể được khám phá dần (R5). Thứ tự = thứ tự hé lộ.</summary>
    public enum IndividualTrait
    {
        /// <summary>Archetype cá tính (Playful/Calm/Curious/Shy).</summary>
        [WireName("archetype")] Archetype,
        /// <summary>Đã thấy cá thể cả lúc hoạt động lẫn lúc nghỉ/ngủ.</summary>
        [WireName("chronotype")] Chronotype,
        /// <summary>Microhabitat ưa thích (PersonalityProfile.FavoriteHabitatId).</summary>
        [WireName("favoriteHabitat")] FavoriteHabitat,
        /// <summary>Món ưa thích (PersonalityProfile.FavoriteDiet).</summary>
        [WireName("favoriteDiet")] FavoriteDiet,
    }

    /// <summary>
    /// Nhịp nghiên cứu (ADR 0204 R3–R5). Production TẮT. <see cref="ProposedU210"/> là đề xuất của U2.10 cho vấn đề "nghiên cứu lên cấp quá nhanh"
    /// của U2.9 (14–51 lần quan sát): giới hạn phát hiện cấp loài mỗi ngày UTC và yêu cầu nhiều ngày khác nhau cho Ethology.
    /// </summary>
    public sealed class ResearchPacing
    {
        public ResearchPacing(bool enabled, long observationCooldownMs, int speciesFindingsPerDay, int minDistinctDaysForEthology,
            int observationsForFavoriteHabitat, int observationsForFavoriteDiet)
        {
            Enabled = enabled; ObservationCooldownMs = observationCooldownMs; SpeciesFindingsPerDay = speciesFindingsPerDay;
            MinDistinctDaysForEthology = minDistinctDaysForEthology; ObservationsForFavoriteHabitat = observationsForFavoriteHabitat;
            ObservationsForFavoriteDiet = observationsForFavoriteDiet;
        }

        public bool Enabled { get; }
        /// <summary>Mỗi rồng tối đa một lần ghi nhận trong khoảng này (chống spam chạm, giữ từ ADR 0203 D3).</summary>
        public long ObservationCooldownMs { get; }
        /// <summary>Số phát hiện cấp loài (tập tính + pha + sinh cảnh) tối đa mỗi loài mỗi ngày UTC. 0 = không giới hạn (hành vi U2.9).</summary>
        public int SpeciesFindingsPerDay { get; }
        /// <summary>Ethology cần ghi chép của loài trải qua ít nhất ngần này ngày UTC khác nhau. 0/1 = không yêu cầu.</summary>
        public int MinDistinctDaysForEthology { get; }
        /// <summary>Số lần quan sát cá thể tối thiểu (và đã thấy nó ở nơi ưa thích) để hé lộ nơi ưa thích.</summary>
        public int ObservationsForFavoriteHabitat { get; }
        /// <summary>Số lần quan sát cá thể tối thiểu (và đã thấy nó ăn hoặc tìm ăn) để hé lộ món ưa thích.</summary>
        public int ObservationsForFavoriteDiet { get; }

        /// <summary>Bản chạy thật: TẮT. Khi tắt, Codex và hồ sơ cá thể hiển thị đầy đủ như hiện tại (không đổi trải nghiệm).</summary>
        public static readonly ResearchPacing Production = new ResearchPacing(false, 30_000, 2, 2, 3, 3);
        /// <summary>Đề xuất U2.10 (chỉ test, mô phỏng). 2 phát hiện/loài/ngày, Ethology cần ≥ 2 ngày.</summary>
        public static readonly ResearchPacing ProposedU210 = new ResearchPacing(true, 30_000, 2, 2, 3, 3);
        /// <summary>Tương đương ResearchRules.ProposedForReview của U2.9 (không giới hạn ngày) — để so sánh trước/sau.</summary>
        public static readonly ResearchPacing UnpacedU29 = new ResearchPacing(true, 30_000, 0, 0, 3, 3);
    }

    /// <summary>Một phát hiện của một lần quan sát.</summary>
    public readonly struct ResearchFinding
    {
        public ResearchFinding(ResearchFindingKind kind, string value) { Kind = kind; Value = value; }
        public ResearchFindingKind Kind { get; }
        /// <summary>Chuỗi wire của tập tính / pha / microhabitat / nét cá thể.</summary>
        public string Value { get; }
        public override string ToString() => Kind.ToWire() + ":" + Value;
    }

    /// <summary>Kết quả của <see cref="ResearchLog.Observe"/>.</summary>
    public sealed class ResearchObservationResult
    {
        /// <summary>true = lần quan sát được tính (dù có phát hiện mới hay không).</summary>
        public bool Recorded { get; internal set; }
        /// <summary>ok / research-disabled / not-observable / observation-cooldown.</summary>
        public string Reason { get; internal set; }
        public IReadOnlyList<ResearchFinding> Findings { get; internal set; } = Array.Empty<ResearchFinding>();
        /// <summary>Phát hiện cấp loài có thật lúc này nhưng bị hoãn vì đã đủ giới hạn ngày (không mất, quan sát lại ngày khác).</summary>
        public int DeferredByDailyLimit { get; internal set; }
    }

    /// <summary>Ghi chép một loài. Tập hợp có thứ tự tất định.</summary>
    public sealed class SpeciesResearchRecord
    {
        internal readonly SortedSet<DragonBehavior> Behaviors = new SortedSet<DragonBehavior>();
        internal readonly SortedSet<DayPhase> Phases = new SortedSet<DayPhase>();
        internal readonly SortedSet<string> Habitats = new SortedSet<string>(StringComparer.Ordinal);
        internal readonly SortedSet<string> Days = new SortedSet<string>(StringComparer.Ordinal);
        /// <summary>Phát hiện cấp loài theo ngày UTC (để áp giới hạn).</summary>
        internal readonly SortedDictionary<string, int> FindingsByDay = new SortedDictionary<string, int>(StringComparer.Ordinal);
        public string SpeciesId { get; internal set; }
    }

    /// <summary>Ghi chép một cá thể (theo UID).</summary>
    public sealed class IndividualResearchRecord
    {
        internal readonly SortedSet<DayPhase> ActivePhases = new SortedSet<DayPhase>();
        internal readonly SortedSet<DayPhase> RestPhases = new SortedSet<DayPhase>();
        internal readonly SortedSet<IndividualTrait> Revealed = new SortedSet<IndividualTrait>();
        public string Uid { get; internal set; }
        public string SpeciesId { get; internal set; }
        public int Observations { get; internal set; }
        public bool SeenInFavoriteHabitat { get; internal set; }
        public bool SeenFeeding { get; internal set; }
        public long LastObservedAtMs { get; internal set; }
    }

    /// <summary>
    /// Sổ ghi chép nghiên cứu U2.10 (thay thế mở rộng cho <see cref="ObservationJournal"/>). KHÔNG nằm trong GameState và không được lưu ở v8.
    /// Mọi đầu vào là quyết định của Behavioral Engine (chỉ đọc), nên ghi chép chỉ chứa điều người chơi thực sự thấy.
    /// </summary>
    public sealed class ResearchLog
    {
        private readonly SortedDictionary<string, SpeciesResearchRecord> _species = new SortedDictionary<string, SpeciesResearchRecord>(StringComparer.Ordinal);
        private readonly SortedDictionary<string, IndividualResearchRecord> _individuals = new SortedDictionary<string, IndividualResearchRecord>(StringComparer.Ordinal);

        public SpeciesResearchRecord SpeciesRecord(string speciesId) => _species.TryGetValue(speciesId ?? "", out var r) ? r : null;
        public IndividualResearchRecord IndividualRecord(string uid) => _individuals.TryGetValue(uid ?? "", out var r) ? r : null;
        public IEnumerable<SpeciesResearchRecord> AllSpecies => _species.Values;
        public IEnumerable<IndividualResearchRecord> AllIndividuals => _individuals.Values;

        internal SpeciesResearchRecord EnsureSpecies(string id)
        {
            if (!_species.TryGetValue(id, out var r)) _species[id] = r = new SpeciesResearchRecord { SpeciesId = id };
            return r;
        }

        internal IndividualResearchRecord EnsureIndividual(string uid, string speciesId)
        {
            if (!_individuals.TryGetValue(uid, out var r)) _individuals[uid] = r = new IndividualResearchRecord { Uid = uid, SpeciesId = speciesId };
            return r;
        }

        /// <summary>Bỏ ghi chép cá thể không còn trong kho (bán, thả). Ghi chép cấp loài không bao giờ mất (như Codex).</summary>
        public int PruneIndividuals(GameState s)
        {
            var gone = new List<string>();
            foreach (string uid in _individuals.Keys)
            {
                DragonState d = s.FindDragon(uid);
                if (d == null || d.Location.Kind == LocationKind.Released) gone.Add(uid);
            }
            foreach (string uid in gone) _individuals.Remove(uid);
            return gone.Count;
        }

        /// <summary>Có thể quan sát rồng này lúc này không (không tính giới hạn ngày). Lý do giống <see cref="Observe"/>.</summary>
        public string Observability(BehaviorDecision decision, long nowMs, ResearchPacing pacing)
        {
            if (pacing == null || !pacing.Enabled) return "research-disabled";
            if (decision == null || !decision.Visible || decision.Behavior == null || decision.ZoneId == null) return "not-observable";
            IndividualResearchRecord ind = IndividualRecord(decision.Uid);
            if (ind != null && ind.Observations > 0 && nowMs >= ind.LastObservedAtMs && nowMs - ind.LastObservedAtMs < pacing.ObservationCooldownMs)
                return "observation-cooldown";
            return "ok";
        }

        /// <summary>Thời gian còn lại của hồi quan sát (0 = quan sát được).</summary>
        public long CooldownRemainingMs(string uid, long nowMs, ResearchPacing pacing)
        {
            IndividualResearchRecord ind = IndividualRecord(uid);
            if (pacing == null || ind == null || ind.Observations == 0 || nowMs < ind.LastObservedAtMs) return 0;
            long left = pacing.ObservationCooldownMs - (nowMs - ind.LastObservedAtMs);
            return left > 0 ? left : 0;
        }

        /// <summary>Số phát hiện cấp loài còn được trong ngày UTC của nowMs (int.MaxValue khi không giới hạn).</summary>
        public int FindingsLeftToday(string speciesId, long nowMs, ResearchPacing pacing)
        {
            if (pacing == null || pacing.SpeciesFindingsPerDay <= 0) return int.MaxValue;
            SpeciesResearchRecord r = SpeciesRecord(speciesId);
            int used = r != null && r.FindingsByDay.TryGetValue(JsCompat.UtcDay(nowMs), out int n) ? n : 0;
            return Math.Max(0, pacing.SpeciesFindingsPerDay - used);
        }

        /// <summary>Phát hiện cấp loài tiềm năng nếu quan sát quyết định này (chưa áp giới hạn ngày).</summary>
        public List<ResearchFinding> PotentialSpeciesFindings(BehaviorDecision d)
        {
            var o = new List<ResearchFinding>();
            if (d == null || d.Behavior == null || d.ZoneId == null) return o;
            SpeciesResearchRecord r = SpeciesRecord(d.SpeciesId);
            if (r == null || !r.Behaviors.Contains(d.Behavior.Value)) o.Add(new ResearchFinding(ResearchFindingKind.Behavior, WireBehavior(d.Behavior.Value)));
            if (r == null || !r.Phases.Contains(d.Phase)) o.Add(new ResearchFinding(ResearchFindingKind.Phase, WirePhase(d.Phase)));
            if (d.HabitatId != null && (r == null || !r.Habitats.Contains(d.HabitatId))) o.Add(new ResearchFinding(ResearchFindingKind.Habitat, d.HabitatId));
            return o;
        }

        /// <summary>Nét cá thể sẽ được hé lộ nếu quan sát quyết định này.</summary>
        public List<IndividualTrait> PotentialIndividualReveals(GameState s, BehaviorDecision d, ResearchPacing pacing)
        {
            var o = new List<IndividualTrait>();
            DragonState dragon = s?.FindDragon(d?.Uid);
            if (dragon == null || d.Behavior == null || pacing == null) return o;
            IndividualResearchRecord cur = IndividualRecord(d.Uid);
            int obs = (cur?.Observations ?? 0) + 1;
            bool active = IsActiveObservation(d), rest = !active;
            bool hasActive = (cur != null && cur.ActivePhases.Count > 0) || active;
            bool hasRest = (cur != null && cur.RestPhases.Count > 0) || rest;
            PersonalityProfile p = PersonalityProfiles.Of(dragon);
            bool fav = (cur?.SeenInFavoriteHabitat ?? false) || d.HabitatId == p.FavoriteHabitatId;
            bool feeding = (cur?.SeenFeeding ?? false) || d.Behavior == DragonBehavior.Eat || d.Behavior == DragonBehavior.SeekFood;
            void Maybe(IndividualTrait t, bool ok) { if (ok && (cur == null || !cur.Revealed.Contains(t))) o.Add(t); }
            Maybe(IndividualTrait.Archetype, true);
            Maybe(IndividualTrait.Chronotype, hasActive && hasRest);
            Maybe(IndividualTrait.FavoriteHabitat, fav && obs >= pacing.ObservationsForFavoriteHabitat);
            Maybe(IndividualTrait.FavoriteDiet, feeding && obs >= pacing.ObservationsForFavoriteDiet);
            return o;
        }

        /// <summary>
        /// Người chơi chạm "quan sát" một rồng đang ở sinh cảnh. Ghi tập tính, pha, microhabitat của loài (tối đa <see cref="ResearchPacing.SpeciesFindingsPerDay"/>
        /// phát hiện mới mỗi loài mỗi ngày UTC; phần vượt bị hoãn, không mất) và tiến trình cá thể. Hồi 30 s mỗi rồng.
        /// Không đổi GameState, không cho vàng/XP/bond.
        /// </summary>
        public ResearchObservationResult Observe(GameState s, BehaviorDecision d, long nowMs, ResearchPacing pacing)
        {
            string why = Observability(d, nowMs, pacing);
            if (why != "ok") return new ResearchObservationResult { Recorded = false, Reason = why };
            var findings = new List<ResearchFinding>();
            string day = JsCompat.UtcDay(nowMs);
            int left = FindingsLeftToday(d.SpeciesId, nowMs, pacing);
            int deferred = 0;
            SpeciesResearchRecord sr = EnsureSpecies(d.SpeciesId);
            foreach (ResearchFinding f in PotentialSpeciesFindings(d))
            {
                if (left <= 0) { deferred++; continue; }
                bool added = f.Kind == ResearchFindingKind.Behavior ? sr.Behaviors.Add(d.Behavior.Value)
                    : f.Kind == ResearchFindingKind.Phase ? sr.Phases.Add(d.Phase)
                    : sr.Habitats.Add(d.HabitatId);
                if (!added) continue;
                findings.Add(f);
                if (left != int.MaxValue) left--;
                sr.FindingsByDay[day] = (sr.FindingsByDay.TryGetValue(day, out int n) ? n : 0) + 1;
            }
            sr.Days.Add(day);

            List<IndividualTrait> reveals = PotentialIndividualReveals(s, d, pacing);
            IndividualResearchRecord ind = EnsureIndividual(d.Uid, d.SpeciesId);
            ind.Observations += 1;
            ind.LastObservedAtMs = nowMs;
            if (IsActiveObservation(d)) ind.ActivePhases.Add(d.Phase); else ind.RestPhases.Add(d.Phase);
            DragonState dragon = s?.FindDragon(d.Uid);
            if (dragon != null && d.HabitatId == PersonalityProfiles.Of(dragon).FavoriteHabitatId) ind.SeenInFavoriteHabitat = true;
            if (d.Behavior == DragonBehavior.Eat || d.Behavior == DragonBehavior.SeekFood) ind.SeenFeeding = true;
            foreach (IndividualTrait t in reveals)
                if (ind.Revealed.Add(t)) findings.Add(new ResearchFinding(ResearchFindingKind.Individual, t.ToWire()));
            return new ResearchObservationResult { Recorded = true, Reason = "ok", Findings = findings, DeferredByDailyLimit = deferred };
        }

        /// <summary>Hoạt động = không phải Rest/Sleep. Dùng cho "chu kỳ ngày đêm" của cá thể.</summary>
        internal static bool IsActiveObservation(BehaviorDecision d) =>
            d.Behavior != DragonBehavior.Sleep && d.Behavior != DragonBehavior.Rest;

        internal static string WireBehavior(DragonBehavior b)
        {
            string n = b.ToString();
            return char.ToLowerInvariant(n[0]) + n.Substring(1);
        }

        internal static string WirePhase(DayPhase p) => p.ToString().ToLowerInvariant();

        // ---- snapshot (bản nháp khối Save Schema v9) ----

        public ResearchLogSnapshot ToSnapshot()
        {
            var snap = new ResearchLogSnapshot();
            foreach (SpeciesResearchRecord r in _species.Values)
            {
                var e = new ResearchLogSnapshot.SpeciesEntry { SpeciesId = r.SpeciesId };
                foreach (DragonBehavior b in r.Behaviors) e.Behaviors.Add(WireBehavior(b));
                foreach (DayPhase p in r.Phases) e.Phases.Add(WirePhase(p));
                e.Habitats.AddRange(r.Habitats);
                e.Days.AddRange(r.Days);
                foreach (var kv in r.FindingsByDay) e.FindingsByDay.Add(new KeyValuePair<string, int>(kv.Key, kv.Value));
                snap.Species.Add(e);
            }
            foreach (IndividualResearchRecord r in _individuals.Values)
            {
                var e = new ResearchLogSnapshot.IndividualEntry
                {
                    Uid = r.Uid, SpeciesId = r.SpeciesId, Observations = r.Observations, SeenInFavoriteHabitat = r.SeenInFavoriteHabitat,
                    SeenFeeding = r.SeenFeeding, LastObservedAtMs = r.LastObservedAtMs,
                };
                foreach (DayPhase p in r.ActivePhases) e.ActivePhases.Add(WirePhase(p));
                foreach (DayPhase p in r.RestPhases) e.RestPhases.Add(WirePhase(p));
                foreach (IndividualTrait t in r.Revealed) e.Revealed.Add(t.ToWire());
                snap.Individuals.Add(e);
            }
            return snap;
        }

        /// <summary>Nạp từ snapshot đã qua <see cref="ResearchSchemaDraft.Validate"/>. Giá trị lạ ném DomainInvariantException.</summary>
        public static ResearchLog FromSnapshot(ResearchLogSnapshot snap)
        {
            var log = new ResearchLog();
            if (snap == null) return log;
            foreach (var e in snap.Species)
            {
                SpeciesResearchRecord r = log.EnsureSpecies(e.SpeciesId);
                foreach (string b in e.Behaviors) r.Behaviors.Add(ParseBehavior(b));
                foreach (string p in e.Phases) r.Phases.Add(ParsePhase(p));
                foreach (string h in e.Habitats) r.Habitats.Add(h);
                foreach (string d in e.Days) r.Days.Add(d);
                foreach (var kv in e.FindingsByDay) r.FindingsByDay[kv.Key] = kv.Value;
            }
            foreach (var e in snap.Individuals)
            {
                IndividualResearchRecord r = log.EnsureIndividual(e.Uid, e.SpeciesId);
                r.Observations = e.Observations; r.SeenInFavoriteHabitat = e.SeenInFavoriteHabitat; r.SeenFeeding = e.SeenFeeding; r.LastObservedAtMs = e.LastObservedAtMs;
                foreach (string p in e.ActivePhases) r.ActivePhases.Add(ParsePhase(p));
                foreach (string p in e.RestPhases) r.RestPhases.Add(ParsePhase(p));
                foreach (string t in e.Revealed) r.Revealed.Add(ParseTrait(t));
            }
            return log;
        }

        internal static bool TryParseBehavior(string s, out DragonBehavior b)
        {
            foreach (DragonBehavior x in BehaviorCatalog.All) if (WireBehavior(x) == s) { b = x; return true; }
            b = default; return false;
        }

        internal static bool TryParsePhase(string s, out DayPhase p)
        {
            foreach (DayPhase x in new[] { DayPhase.Dawn, DayPhase.Day, DayPhase.Dusk, DayPhase.Night }) if (WirePhase(x) == s) { p = x; return true; }
            p = default; return false;
        }

        internal static bool TryParseTrait(string s, out IndividualTrait t)
        {
            foreach (IndividualTrait x in new[] { IndividualTrait.Archetype, IndividualTrait.Chronotype, IndividualTrait.FavoriteHabitat, IndividualTrait.FavoriteDiet })
                if (x.ToWire() == s) { t = x; return true; }
            t = default; return false;
        }

        private static DragonBehavior ParseBehavior(string s) => TryParseBehavior(s, out var b) ? b : throw new DomainInvariantException("research: unknown behavior " + s);
        private static DayPhase ParsePhase(string s) => TryParsePhase(s, out var p) ? p : throw new DomainInvariantException("research: unknown phase " + s);
        private static IndividualTrait ParseTrait(string s) => TryParseTrait(s, out var t) ? t : throw new DomainInvariantException("research: unknown trait " + s);
    }

    /// <summary>
    /// Bản nháp khối <c>research</c> của Save Schema v9 (ADR 0204 R8). Chỉ là DTO: CHƯA nối vào GameStateJson, CHƯA đổi SchemaVersion.
    /// Mọi danh sách sắp xếp ordinal để checksum ổn định.
    /// </summary>
    public sealed class ResearchLogSnapshot
    {
        public sealed class SpeciesEntry
        {
            public string SpeciesId;
            public List<string> Behaviors = new List<string>();
            public List<string> Phases = new List<string>();
            public List<string> Habitats = new List<string>();
            public List<string> Days = new List<string>();
            public List<KeyValuePair<string, int>> FindingsByDay = new List<KeyValuePair<string, int>>();
        }

        public sealed class IndividualEntry
        {
            public string Uid;
            public string SpeciesId;
            public int Observations;
            public bool SeenInFavoriteHabitat;
            public bool SeenFeeding;
            public long LastObservedAtMs;
            public List<string> ActivePhases = new List<string>();
            public List<string> RestPhases = new List<string>();
            public List<string> Revealed = new List<string>();
        }

        public List<SpeciesEntry> Species = new List<SpeciesEntry>();
        public List<IndividualEntry> Individuals = new List<IndividualEntry>();
    }

    /// <summary>Bản nháp migration v8 → v9 và validator cho khối research (ADR 0204 R8). Không được gọi từ Persistence.</summary>
    public static class ResearchSchemaDraft
    {
        public const int DraftSchemaVersion = 9;
        /// <summary>Giữ lịch sử phát hiện theo ngày tối đa 14 ngày gần nhất (đủ cho giới hạn ngày, chặn phình save).</summary>
        public const int MaxFindingDaysKept = 14;

        /// <summary>Migration v8 → v9: tạo khối rỗng, KHÔNG suy diễn từ Codex (người chơi chưa quan sát gì). Không chạm vào dữ liệu v8.</summary>
        public static ResearchLogSnapshot MigrateFromV8(GameState v8) => new ResearchLogSnapshot();

        /// <summary>Lỗi cấu trúc của khối research so với state (rỗng = hợp lệ).</summary>
        public static List<string> Validate(ResearchLogSnapshot snap, GameState s)
        {
            var errors = new List<string>();
            if (snap == null) { errors.Add("research: null"); return errors; }
            string prev = null;
            foreach (var e in snap.Species)
            {
                string path = "research.species[" + e.SpeciesId + "]";
                if (!SpeciesCatalog.TryGet(e.SpeciesId, out _)) { errors.Add(path + ": unknown species"); continue; }
                if (prev != null && string.CompareOrdinal(prev, e.SpeciesId) >= 0) errors.Add(path + ": not sorted or duplicate");
                prev = e.SpeciesId;
                if (Progression.Codex.Find(s, e.SpeciesId) == null) errors.Add(path + ": species not in codex");
                List<DragonBehavior> observable = Research.ObservableBehaviors(e.SpeciesId);
                foreach (string b in e.Behaviors)
                    if (!ResearchLog.TryParseBehavior(b, out DragonBehavior x)) errors.Add(path + ".behaviors: unknown " + b);
                    else if (!observable.Contains(x)) errors.Add(path + ".behaviors: impossible for species " + b);
                foreach (string p in e.Phases) if (!ResearchLog.TryParsePhase(p, out _)) errors.Add(path + ".phases: unknown " + p);
                IReadOnlyList<string> rules = SpeciesCatalog.GetBiology(e.SpeciesId).HabitatRules;
                foreach (string h in e.Habitats) if (!BehaviorCatalog.Has(rules, h)) errors.Add(path + ".habitats: not a habitat of species " + h);
                foreach (string d in e.Days) if (!JsCompat.IsUtcDayPattern(d)) errors.Add(path + ".days: invalid " + d);
                if (e.FindingsByDay.Count > MaxFindingDaysKept) errors.Add(path + ".findingsByDay: too many days");
                foreach (var kv in e.FindingsByDay) if (!JsCompat.IsUtcDayPattern(kv.Key) || kv.Value < 0) errors.Add(path + ".findingsByDay: invalid " + kv.Key);
            }
            prev = null;
            foreach (var e in snap.Individuals)
            {
                string path = "research.individuals[" + e.Uid + "]";
                if (prev != null && string.CompareOrdinal(prev, e.Uid) >= 0) errors.Add(path + ": not sorted or duplicate");
                prev = e.Uid;
                DragonState d = s.FindDragon(e.Uid);
                if (d == null || d.Location.Kind == LocationKind.Released) errors.Add(path + ": dragon not kept (prune on load)");
                else if (d.SpeciesId != e.SpeciesId) errors.Add(path + ": species mismatch");
                if (e.Observations < 0) errors.Add(path + ".observations: negative");
                foreach (string t in e.Revealed) if (!ResearchLog.TryParseTrait(t, out _)) errors.Add(path + ".revealed: unknown " + t);
            }
            return errors;
        }
    }

    /// <summary>Hồ sơ nghiên cứu một loài (mở rộng SpeciesResearchView của U2.9).</summary>
    public sealed class SpeciesResearchProfile
    {
        public string SpeciesId { get; internal set; }
        /// <summary>false = luật đang tắt: mọi phần Codex mở cho loài đã khám phá (như hiện tại).</summary>
        public bool Gated { get; internal set; }
        public ResearchTier Tier { get; internal set; }
        public IReadOnlyList<CodexSection> RevealedSections { get; internal set; }
        public IReadOnlyList<DragonBehavior> ObservableBehaviors { get; internal set; }
        public IReadOnlyList<DragonBehavior> ObservedBehaviors { get; internal set; }
        public IReadOnlyList<DayPhase> ObservedPhases { get; internal set; }
        /// <summary>Microhabitat trong HabitatRules của loài (sinh cảnh phù hợp cần ghi nhận).</summary>
        public IReadOnlyList<string> NativeHabitats { get; internal set; }
        public IReadOnlyList<string> ObservedHabitats { get; internal set; }
        /// <summary>Đã thấy cả pha hoạt động và pha nghỉ (chu kỳ ngày đêm).</summary>
        public bool CircadianCovered { get; internal set; }
        public int DistinctDays { get; internal set; }
        public int RequiredDistinctDays { get; internal set; }
        /// <summary>0–100: phần ghi chép Ethology đã có (tập tính + pha + ngày).</summary>
        public int EthologyProgressPct { get; internal set; }
        /// <summary>research.next.&lt;own|viewProfile|raiseToAdult|observe|returnTomorrow|done&gt;.</summary>
        public string NextStepKey { get; internal set; }
        /// <summary>Số cá thể của loài đang trong kho (chưa thả).</summary>
        public int Individuals { get; internal set; }
    }

    /// <summary>Hồ sơ nghiên cứu một cá thể.</summary>
    public sealed class IndividualResearchProfile
    {
        public string Uid { get; internal set; }
        public string SpeciesId { get; internal set; }
        public bool Gated { get; internal set; }
        public int Observations { get; internal set; }
        public IReadOnlyList<IndividualTrait> RevealedTraits { get; internal set; }
        /// <summary>null khi chưa hé lộ (luật bật). Khi luật tắt luôn có giá trị.</summary>
        public Personality? Archetype { get; internal set; }
        public string FavoriteHabitatId { get; internal set; }
        public DietId? FavoriteDiet { get; internal set; }
        /// <summary>Pha đã thấy cá thể hoạt động / nghỉ.</summary>
        public IReadOnlyList<DayPhase> ActivePhases { get; internal set; }
        public IReadOnlyList<DayPhase> RestPhases { get; internal set; }
        /// <summary>research.individual.next.&lt;observe|observeOtherPhase|observeAtHome|observeFeeding|done&gt;.</summary>
        public string NextStepKey { get; internal set; }
    }

    /// <summary>Một rồng có thể quan sát lúc này và điều sẽ học được (chống spam: chỉ "đáng quan sát" khi có phát hiện).</summary>
    public sealed class ObservationOpportunity
    {
        public string Uid { get; internal set; }
        public string SpeciesId { get; internal set; }
        public DragonBehavior Behavior { get; internal set; }
        public DayPhase Phase { get; internal set; }
        public string ZoneId { get; internal set; }
        public string HabitatId { get; internal set; }
        /// <summary>ok / observation-cooldown.</summary>
        public string Availability { get; internal set; }
        public long CooldownRemainingMs { get; internal set; }
        /// <summary>Phát hiện cấp loài sẽ được ghi (đã áp giới hạn ngày).</summary>
        public IReadOnlyList<ResearchFinding> SpeciesFindings { get; internal set; }
        /// <summary>Phát hiện cấp loài có nhưng bị hoãn vì đã đủ giới hạn hôm nay.</summary>
        public int DeferredByDailyLimit { get; internal set; }
        public IReadOnlyList<IndividualTrait> IndividualReveals { get; internal set; }
        /// <summary>true khi quan sát lúc này cho ít nhất một phát hiện. UI chỉ nên nhấn mạnh các cơ hội này.</summary>
        public bool Worthwhile { get; internal set; }
        /// <summary>Gợi ý ngắn: research.hint.&lt;newBehavior|newPhase|newHabitat|individual|tomorrow|cooldown|nothingNew&gt;.</summary>
        public string HintKey { get; internal set; }
    }

    /// <summary>Tính hồ sơ, cơ hội quan sát. Hàm thuần, chỉ đọc.</summary>
    public static class ResearchProgress
    {
        public static SpeciesResearchProfile Species(GameState s, string speciesId, ResearchLog log, ResearchPacing pacing)
        {
            pacing = pacing ?? ResearchPacing.Production;
            SpeciesBiology b = SpeciesCatalog.GetBiology(speciesId);
            CodexEntry e = Progression.Codex.Find(s, speciesId);
            List<DragonBehavior> observable = Research.ObservableBehaviors(speciesId);
            SpeciesResearchRecord r = log?.SpeciesRecord(speciesId);
            var seen = r != null ? new List<DragonBehavior>(r.Behaviors) : new List<DragonBehavior>();
            var phases = r != null ? new List<DayPhase>(r.Phases) : new List<DayPhase>();
            bool circadian = r != null && Research.PhasesCovered(b, r.Phases, r.Behaviors);
            int days = r?.Days.Count ?? 0;
            int needDays = Math.Max(1, pacing.MinDistinctDaysForEthology);

            ResearchTier tier = ResearchTier.Unknown;
            string next = "research.next.own";
            if (e != null)
            {
                tier = ResearchTier.Sighted; next = "research.next.viewProfile";
                if (s.Tutorial.ProfilesViewed.Contains(speciesId))
                {
                    tier = ResearchTier.Profiled; next = "research.next.raiseToAdult";
                    if (e.ObservedStages.Count >= 3)
                    {
                        tier = ResearchTier.LifeCycle; next = "research.next.observe";
                        bool all = true;
                        foreach (DragonBehavior x in observable) if (!seen.Contains(x)) all = false;
                        if (all && circadian && days >= needDays) { tier = ResearchTier.Ethology; next = "research.next.done"; }
                        else if (pacing.Enabled && all && circadian) next = "research.next.returnTomorrow";
                    }
                }
            }

            var sections = new List<CodexSection>();
            if (!pacing.Enabled) { if (e != null) sections.AddRange(new[] { CodexSection.Identity, CodexSection.Habitat, CodexSection.Physiology, CodexSection.Behavior }); }
            else
            {
                if (tier >= ResearchTier.Sighted) sections.Add(CodexSection.Identity);
                if (tier >= ResearchTier.Profiled) sections.Add(CodexSection.Habitat);
                if (tier >= ResearchTier.LifeCycle) sections.Add(CodexSection.Physiology);
                if (tier >= ResearchTier.Ethology) sections.Add(CodexSection.Behavior);
            }

            int seenObservable = 0;
            foreach (DragonBehavior x in observable) if (seen.Contains(x)) seenObservable++;
            int parts = observable.Count + 2 + needDays;
            int have = seenObservable + (circadian ? 2 : Math.Min(1, phases.Count)) + Math.Min(days, needDays);
            int individuals = 0;
            foreach (DragonState d in s.Dragons) if (d.SpeciesId == speciesId && d.Location.Kind != LocationKind.Released) individuals++;
            return new SpeciesResearchProfile
            {
                SpeciesId = speciesId, Gated = pacing.Enabled, Tier = tier, RevealedSections = sections, ObservableBehaviors = observable,
                ObservedBehaviors = seen, ObservedPhases = phases, NativeHabitats = b.HabitatRules,
                ObservedHabitats = r != null ? new List<string>(r.Habitats) : new List<string>(), CircadianCovered = circadian,
                DistinctDays = days, RequiredDistinctDays = needDays, EthologyProgressPct = parts == 0 ? 0 : have * 100 / parts, NextStepKey = next,
                Individuals = individuals,
            };
        }

        public static IndividualResearchProfile Individual(GameState s, string uid, ResearchLog log, ResearchPacing pacing)
        {
            pacing = pacing ?? ResearchPacing.Production;
            DragonState d = s.FindDragon(uid);
            if (d == null || d.Location.Kind == LocationKind.Released) return null;
            PersonalityProfile p = PersonalityProfiles.Of(d);
            IndividualResearchRecord r = log?.IndividualRecord(uid);
            var revealed = new List<IndividualTrait>();
            if (!pacing.Enabled) revealed.AddRange(new[] { IndividualTrait.Archetype, IndividualTrait.Chronotype, IndividualTrait.FavoriteHabitat, IndividualTrait.FavoriteDiet });
            else if (r != null) revealed.AddRange(r.Revealed);
            string next = "research.individual.next.done";
            if (pacing.Enabled)
            {
                if (!revealed.Contains(IndividualTrait.Archetype)) next = "research.individual.next.observe";
                else if (!revealed.Contains(IndividualTrait.Chronotype)) next = "research.individual.next.observeOtherPhase";
                else if (!revealed.Contains(IndividualTrait.FavoriteHabitat)) next = "research.individual.next.observeAtHome";
                else if (!revealed.Contains(IndividualTrait.FavoriteDiet)) next = "research.individual.next.observeFeeding";
            }
            return new IndividualResearchProfile
            {
                Uid = uid, SpeciesId = d.SpeciesId, Gated = pacing.Enabled, Observations = r?.Observations ?? 0, RevealedTraits = revealed,
                Archetype = revealed.Contains(IndividualTrait.Archetype) ? d.Personality : (Personality?)null,
                FavoriteHabitatId = revealed.Contains(IndividualTrait.FavoriteHabitat) ? p.FavoriteHabitatId : null,
                FavoriteDiet = revealed.Contains(IndividualTrait.FavoriteDiet) ? p.FavoriteDiet : (DietId?)null,
                ActivePhases = r != null ? new List<DayPhase>(r.ActivePhases) : new List<DayPhase>(),
                RestPhases = r != null ? new List<DayPhase>(r.RestPhases) : new List<DayPhase>(),
                NextStepKey = next,
            };
        }

        /// <summary>
        /// Cơ hội quan sát của mọi rồng đang hiện ở sinh cảnh (thứ tự: đáng quan sát trước, rồi UID). Khi luật tắt trả danh sách rỗng
        /// (nút "quan sát" giữ hành vi U2.9: chỉ mở hồ sơ loài).
        /// </summary>
        public static List<ObservationOpportunity> Opportunities(GameState s, long nowMs, DayPhase phase, ResearchLog log, ResearchPacing pacing)
        {
            var o = new List<ObservationOpportunity>();
            pacing = pacing ?? ResearchPacing.Production;
            if (!pacing.Enabled) return o;
            log = log ?? new ResearchLog();
            foreach (BehaviorDecision d in BehaviorEngine.DecideAll(s, nowMs, phase))
            {
                if (d == null || !d.Visible || d.Behavior == null || d.ZoneId == null) continue;
                string avail = log.Observability(d, nowMs, pacing);
                List<ResearchFinding> pot = log.PotentialSpeciesFindings(d);
                int left = log.FindingsLeftToday(d.SpeciesId, nowMs, pacing);
                var take = new List<ResearchFinding>();
                int deferred = 0;
                foreach (ResearchFinding f in pot) { if (take.Count < left) take.Add(f); else deferred++; }
                List<IndividualTrait> reveals = log.PotentialIndividualReveals(s, d, pacing);
                bool worth = avail == "ok" && (take.Count > 0 || reveals.Count > 0);
                string hint = avail != "ok" ? "research.hint.cooldown"
                    : take.Exists(x => x.Kind == ResearchFindingKind.Behavior) ? "research.hint.newBehavior"
                    : take.Exists(x => x.Kind == ResearchFindingKind.Phase) ? "research.hint.newPhase"
                    : take.Exists(x => x.Kind == ResearchFindingKind.Habitat) ? "research.hint.newHabitat"
                    : reveals.Count > 0 ? "research.hint.individual"
                    : deferred > 0 ? "research.hint.tomorrow" : "research.hint.nothingNew";
                o.Add(new ObservationOpportunity
                {
                    Uid = d.Uid, SpeciesId = d.SpeciesId, Behavior = d.Behavior.Value, Phase = d.Phase, ZoneId = d.ZoneId, HabitatId = d.HabitatId,
                    Availability = avail, CooldownRemainingMs = log.CooldownRemainingMs(d.Uid, nowMs, pacing), SpeciesFindings = take,
                    DeferredByDailyLimit = deferred, IndividualReveals = reveals, Worthwhile = worth, HintKey = hint,
                });
            }
            o.Sort((a, b) => a.Worthwhile != b.Worthwhile ? (a.Worthwhile ? -1 : 1) : string.CompareOrdinal(a.Uid, b.Uid));
            return o;
        }

        /// <summary>Đếm số loài đã khám phá ở mỗi cấp (cấp ≥ tier), cho tổng quan tiến trình.</summary>
        public static Dictionary<ResearchTier, int> TierCounts(GameState s, ResearchLog log, ResearchPacing pacing)
        {
            var o = new Dictionary<ResearchTier, int>();
            foreach (ResearchTier t in new[] { ResearchTier.Sighted, ResearchTier.Profiled, ResearchTier.LifeCycle, ResearchTier.Ethology }) o[t] = 0;
            foreach (CodexEntry e in s.Codex)
            {
                ResearchTier tier = Species(s, e.SpeciesId, log, pacing).Tier;
                foreach (ResearchTier t in new[] { ResearchTier.Sighted, ResearchTier.Profiled, ResearchTier.LifeCycle, ResearchTier.Ethology })
                    if (tier >= t) o[t]++;
            }
            return o;
        }

        /// <summary>Stage hiện tại (tiện cho UI cá thể).</summary>
        internal static GrowthStage StageOf(DragonState d) => EconomyRates.GrowthStageOf(d.XpSec, SpeciesCatalog.Get(d.SpeciesId));
    }
}
