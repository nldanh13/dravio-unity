using System;
using System.Collections.Generic;
using Dravio.Domain.Catalog;

namespace Dravio.Domain.Behavior
{
    /// <summary>Cấp hiểu biết về một loài (ADR 0203 D1). Tích lũy: cấp N cần đủ điều kiện mọi cấp trước.</summary>
    public enum ResearchTier
    {
        /// <summary>Chưa có trong Bách khoa.</summary>
        [WireName("unknown")] Unknown,
        /// <summary>Đã sở hữu (ô Bách khoa đầu tiên).</summary>
        [WireName("sighted")] Sighted,
        /// <summary>Đã mở hồ sơ sinh học (viewBiologyProfile).</summary>
        [WireName("profiled")] Profiled,
        /// <summary>Đã quan sát đủ 3 giai đoạn sống (Codex.ObservedStages).</summary>
        [WireName("lifeCycle")] LifeCycle,
        /// <summary>Đã ghi nhận mọi tập tính loài làm được và thấy nó cả lúc hoạt động lẫn lúc ngủ. Cần sổ quan sát (thử nghiệm, chưa lưu).</summary>
        [WireName("ethology")] Ethology,
    }

    /// <summary>Phần hồ sơ Codex được mở theo cấp.</summary>
    public enum CodexSection
    {
        /// <summary>Nguyên tố, độ hiếm, hình dáng, realm.</summary>
        [WireName("identity")] Identity,
        /// <summary>Microhabitat, nhiệt độ, độ mặn, độ sâu, độ cao, hô hấp, di chuyển, thức ăn.</summary>
        [WireName("habitat")] Habitat,
        /// <summary>Thời gian lớn, ngưỡng comfortMin, sức chống chịu hazard.</summary>
        [WireName("physiology")] Physiology,
        /// <summary>Ma trận hành vi, chu kỳ ngày đêm, ghi chú cá tính.</summary>
        [WireName("behavior")] Behavior,
    }

    /// <summary>ADR 0203 D1–D3. <b>TẮT ở production</b>: khi tắt, mọi phần hồ sơ của loài đã khám phá đều mở (giữ nguyên hành vi hiện tại).</summary>
    public sealed class ResearchRules
    {
        public ResearchRules(bool enabled, long observationCooldownMs)
        {
            Enabled = enabled; ObservationCooldownMs = observationCooldownMs;
        }

        public bool Enabled { get; }
        /// <summary>Mỗi rồng ghi nhận tối đa một lần trong khoảng này (chống spam chạm).</summary>
        public long ObservationCooldownMs { get; }

        public static readonly ResearchRules Production = new ResearchRules(false, 30_000);
        public static readonly ResearchRules ProposedForReview = new ResearchRules(true, 30_000);
    }

    /// <summary>Kết quả một lần ghi nhận tập tính.</summary>
    public readonly struct ObservationResult
    {
        public ObservationResult(bool recorded, bool newBehavior, bool newPhase, string reason)
        {
            Recorded = recorded; NewBehavior = newBehavior; NewPhase = newPhase; Reason = reason;
        }

        public bool Recorded { get; }
        public bool NewBehavior { get; }
        public bool NewPhase { get; }
        /// <summary>ok / research-disabled / not-observable / observation-cooldown.</summary>
        public string Reason { get; }
    }

    /// <summary>
    /// Sổ quan sát tập tính (thử nghiệm ADR 0203 D2). KHÔNG nằm trong GameState và không được lưu: khi luật được duyệt sẽ thêm
    /// khối <c>research</c> vào Save Schema v9 kèm migration v8 → v9 (ADR 0203 D4). Thứ tự duyệt tất định (thứ tự catalog).
    /// </summary>
    public sealed class ObservationJournal
    {
        private readonly Dictionary<string, SortedSet<DragonBehavior>> _behaviors = new Dictionary<string, SortedSet<DragonBehavior>>(StringComparer.Ordinal);
        private readonly Dictionary<string, SortedSet<DayPhase>> _phases = new Dictionary<string, SortedSet<DayPhase>>(StringComparer.Ordinal);
        private readonly Dictionary<string, long> _lastByUid = new Dictionary<string, long>(StringComparer.Ordinal);

        public IReadOnlyCollection<DragonBehavior> BehaviorsOf(string speciesId) =>
            _behaviors.TryGetValue(speciesId, out var s) ? (IReadOnlyCollection<DragonBehavior>)s : Array.Empty<DragonBehavior>();

        public IReadOnlyCollection<DayPhase> PhasesOf(string speciesId) =>
            _phases.TryGetValue(speciesId, out var s) ? (IReadOnlyCollection<DayPhase>)s : Array.Empty<DayPhase>();

        /// <summary>Ghi nhận quyết định hành vi hiện tại của một rồng (người chơi chạm "quan sát"). Chỉ rồng đang ở sinh cảnh mới quan sát được.</summary>
        public ObservationResult Observe(BehaviorDecision decision, long nowMs, ResearchRules rules)
        {
            if (rules == null || !rules.Enabled) return new ObservationResult(false, false, false, "research-disabled");
            if (decision == null || !decision.Visible || decision.Behavior == null || decision.ZoneId == null)
                return new ObservationResult(false, false, false, "not-observable");
            if (_lastByUid.TryGetValue(decision.Uid, out long last) && nowMs >= last && nowMs - last < rules.ObservationCooldownMs)
                return new ObservationResult(false, false, false, "observation-cooldown");
            _lastByUid[decision.Uid] = nowMs;
            if (!_behaviors.TryGetValue(decision.SpeciesId, out var bs)) _behaviors[decision.SpeciesId] = bs = new SortedSet<DragonBehavior>();
            if (!_phases.TryGetValue(decision.SpeciesId, out var ps)) _phases[decision.SpeciesId] = ps = new SortedSet<DayPhase>();
            bool nb = bs.Add(decision.Behavior.Value);
            bool np = ps.Add(decision.Phase);
            return new ObservationResult(true, nb, np, "ok");
        }
    }

    /// <summary>Tiến trình nghiên cứu của một loài cho UI Codex.</summary>
    public sealed class SpeciesResearchView
    {
        public string SpeciesId { get; internal set; }
        /// <summary>false = luật nghiên cứu đang tắt: mọi phần đã mở cho loài đã khám phá.</summary>
        public bool Gated { get; internal set; }
        public ResearchTier Tier { get; internal set; }
        public IReadOnlyList<CodexSection> RevealedSections { get; internal set; }
        /// <summary>Tập tính loài làm được (ở ít nhất một microhabitat của nó) — mục tiêu của cấp Ethology.</summary>
        public IReadOnlyList<DragonBehavior> ObservableBehaviors { get; internal set; }
        public IReadOnlyList<DragonBehavior> ObservedBehaviors { get; internal set; }
        /// <summary>Khóa gợi ý bước tiếp theo: <c>research.next.&lt;own|viewProfile|raiseToAdult|observe|done&gt;</c>.</summary>
        public string NextStepKey { get; internal set; }
    }

    public static class Research
    {
        /// <summary>Tập tính loài làm được ở ít nhất một microhabitat hợp lệ của nó (gồm cả SeekFood/Eat), thứ tự catalog.</summary>
        public static List<DragonBehavior> ObservableBehaviors(string speciesId)
        {
            SpeciesBiology b = SpeciesCatalog.GetBiology(speciesId);
            var o = new List<DragonBehavior>();
            foreach (DragonBehavior x in BehaviorCatalog.All)
            {
                if (BehaviorCatalog.SpeciesBlock(b, x) != BehaviorBlock.None) continue;
                bool any = false;
                foreach (string hid in b.HabitatRules)
                    if (HabitatCatalog.TryGet(hid, out HabitatProfile h) && BehaviorCatalog.HabitatBlock(h, x) == BehaviorBlock.None) any = true;
                if (any) o.Add(x);
            }
            return o;
        }

        /// <summary>Thấy ngủ và thấy hoạt động: cần ít nhất một pha mà loài hoạt động mạnh (≥ 60) và một pha nghỉ (≤ 60 hoặc đã thấy Sleep).</summary>
        internal static bool PhasesCovered(SpeciesBiology b, IReadOnlyCollection<DayPhase> phases, IReadOnlyCollection<DragonBehavior> seen)
        {
            bool active = false, rest = false;
            foreach (DayPhase p in phases)
            {
                int a = DayCycle.Activeness(b.Activity, p);
                if (a >= 60) active = true;
                if (a <= 60) rest = true;
            }
            foreach (DragonBehavior x in seen) if (x == DragonBehavior.Sleep) rest = true;
            return active && rest;
        }

        public static SpeciesResearchView Species(GameState s, string speciesId, ObservationJournal journal, ResearchRules rules)
        {
            rules = rules ?? ResearchRules.Production;
            SpeciesBiology b = SpeciesCatalog.GetBiology(speciesId);
            CodexEntry e = Progression.Codex.Find(s, speciesId);
            List<DragonBehavior> observable = ObservableBehaviors(speciesId);
            var seen = new List<DragonBehavior>(journal?.BehaviorsOf(speciesId) ?? (IReadOnlyCollection<DragonBehavior>)Array.Empty<DragonBehavior>());

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
                        if (all && journal != null && PhasesCovered(b, journal.PhasesOf(speciesId), seen)) { tier = ResearchTier.Ethology; next = "research.next.done"; }
                    }
                }
            }

            var sections = new List<CodexSection>();
            if (!rules.Enabled)
            {
                if (e != null) sections.AddRange(new[] { CodexSection.Identity, CodexSection.Habitat, CodexSection.Physiology, CodexSection.Behavior });
            }
            else
            {
                if (tier >= ResearchTier.Sighted) sections.Add(CodexSection.Identity);
                if (tier >= ResearchTier.Profiled) sections.Add(CodexSection.Habitat);
                if (tier >= ResearchTier.LifeCycle) sections.Add(CodexSection.Physiology);
                if (tier >= ResearchTier.Ethology) sections.Add(CodexSection.Behavior);
            }
            return new SpeciesResearchView
            {
                SpeciesId = speciesId, Gated = rules.Enabled, Tier = tier, RevealedSections = sections, ObservableBehaviors = observable,
                ObservedBehaviors = seen, NextStepKey = next,
            };
        }
    }
}
