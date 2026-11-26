using System;
using System.Collections.Generic;
using System.Globalization;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Economy;
using Dravio.Domain.World;

namespace Dravio.Domain.Health
{
    /// <summary>
    /// Điều kiện sức khỏe dùng chung cho lai (ADR 0003) và thám hiểm (ADR 0201 mục 4). Đọc state chính, không tạo bản sao.
    /// Danh sách trả về là các điều kiện CHƯA đạt, theo thứ tự cố định; rỗng = đạt.
    /// </summary>
    public static class HealthChecks
    {
        public const string NoOpenCase = "no-open-care-case";

        public static List<string> BreedingShortfalls(GameState s, DragonState d) =>
            Shortfalls(s, d, HealthCatalog.BreedMinHealth, HealthCatalog.BreedMinMobility, HealthCatalog.BreedMinComfort);

        public static List<string> ExpeditionShortfalls(GameState s, DragonState d) =>
            Shortfalls(s, d, HealthCatalog.ExpeditionMinHealth, HealthCatalog.ExpeditionMinMobility, HealthCatalog.ExpeditionMinComfort);

        private static List<string> Shortfalls(GameState s, DragonState d, int minHealth, int minMobility, int minComfort)
        {
            var o = new List<string>();
            Vitals v = d.Vitals;
            // Thiếu dữ liệu không bao giờ được coi là "khỏe" (Issue #34).
            if (v == null) { o.Add("vitals-missing"); return o; }
            if (v.Health < minHealth) o.Add("health>=" + minHealth.ToString(CultureInfo.InvariantCulture));
            if (v.Mobility < minMobility) o.Add("mobility>=" + minMobility.ToString(CultureInfo.InvariantCulture));
            if (v.Comfort < minComfort) o.Add("comfort>=" + minComfort.ToString(CultureInfo.InvariantCulture));
            if (s.OpenCaseOf(d.Uid) != null) o.Add(NoOpenCase);
            return o;
        }
    }

    /// <summary>
    /// Trạm cứu hộ và quy trình chăm sóc (GDD tr. 10–11; G0 S02): nhận định → kế hoạch → thực hiện → lượng giá → xuất viện.
    /// Tiến trình tính theo mốc thời gian tuyệt đối (startedAt + duration), nên online, offline hay nạp lại đều cho cùng kết quả.
    /// Không có tổn thương mới do chọn sai; không tự xuất viện; không có thưởng vàng (ADR 0202 D6).
    /// </summary>
    public static class CareCenter
    {
        public static int ActiveCount(GameState s)
        {
            int n = 0;
            foreach (HealthCase c in s.HealthCases) if (c.Status == CaseStatus.Active) n++;
            return n;
        }

        /// <summary>Mốc kết thúc từng phase (ms tính từ lúc bắt đầu): ceil theo tỷ lệ cộng dồn, phase cuối lấy phần còn lại.</summary>
        public static long[] PhaseBoundaries(long durationMs)
        {
            var b = new long[HealthCatalog.PhasePercent.Count];
            int cum = 0;
            for (int i = 0; i < b.Length; i++)
            {
                cum += HealthCatalog.PhasePercent[i];
                b[i] = i == b.Length - 1 ? durationMs : (durationMs * cum + 99) / 100;
            }
            return b;
        }

        /// <summary>Phase, tiến trình (‰) và mốc lượng giá kế tiếp của ca tại thời điểm t. Chưa bắt đầu = ổn định, 0.</summary>
        public static CasePhase PhaseAt(HealthCase c, long t, out int progressPermille, out long nextReviewAtMs)
        {
            if (c.StartedAtMs <= 0 || c.DurationMs <= 0) { progressPermille = 0; nextReviewAtMs = 0; return CasePhase.Stabilize; }
            long elapsed = Math.Max(0, Math.Min(c.DurationMs, t - c.StartedAtMs));
            long[] b = PhaseBoundaries(c.DurationMs);
            progressPermille = (int)(elapsed * 1000 / c.DurationMs);
            for (int i = 0; i < b.Length; i++)
                if (elapsed < b[i]) { nextReviewAtMs = c.StartedAtMs + b[i]; return (CasePhase)i; }
            nextReviewAtMs = c.FinishAtMs;
            return CasePhase.Return;
        }

        public static bool IsComplete(HealthCase c) => c.StartedAtMs > 0 && c.Progress >= 1000;

        /// <summary>Cập nhật mọi ca đã bắt đầu tới thời điểm toMs (gọi trong quyết toán thời gian). Tiến trình đơn điệu.</summary>
        internal static void Progress(GameState s, long toMs, List<DomainEvent> events)
        {
            foreach (HealthCase c in s.HealthCases)
            {
                if (c.Status != CaseStatus.Active || c.StartedAtMs <= 0) continue;
                int before = c.Progress;
                CasePhase from = c.Phase;
                CasePhase to = PhaseAt(c, toMs, out int progress, out long next);
                if (progress < c.Progress) continue; // đồng hồ lùi: giữ nguyên, không lùi tiến trình
                long[] b = PhaseBoundaries(c.DurationMs);
                for (int i = (int)from; i < (int)to; i++) events.Add(new CasePhaseChanged(c.CaseId, (CasePhase)i, (CasePhase)(i + 1), c.StartedAtMs + b[i]));
                c.Phase = to;
                c.Progress = progress;
                c.NextReviewAtMs = next;
                if (before < 1000 && progress >= 1000)
                {
                    DragonState d = s.FindDragon(c.DragonId);
                    if (d != null) Recover(d, events);
                    events.Add(new CareCompleted(c.CaseId, c.FinishAtMs));
                }
            }
        }

        /// <summary>[G0 S02] Hoàn tất phase return: mobility → 90, health ≥ 90, comfort ≥ 80; không giảm chỉ số đang cao hơn.</summary>
        private static void Recover(DragonState d, List<DomainEvent> events)
        {
            Vitals before = d.Vitals.Clone();
            d.Vitals.Health = Math.Max(d.Vitals.Health, HealthCatalog.RecoveredHealth);
            d.Vitals.Mobility = Math.Max(d.Vitals.Mobility, HealthCatalog.RecoveredMobility);
            d.Vitals.Comfort = Math.Max(d.Vitals.Comfort, HealthCatalog.RecoveredComfort);
            if (before.Health != d.Vitals.Health || before.Mobility != d.Vitals.Mobility || before.Comfort != d.Vitals.Comfort)
                events.Add(new VitalsChanged(d.Uid, before, d.Vitals.Clone()));
        }

        /// <summary>
        /// Mở ca cho một rồng bị thương (caseId duy nhất, tất định). Rời ngân sách sản xuất; vào trạm nếu còn chỗ, nếu đầy thì
        /// tự ổn định ở nơi trú của zone và xếp hàng FIFO (GDD tr. 9, 11).
        /// </summary>
        internal static HealthCase Open(GameState s, string caseId, DisasterState ev, DragonState d, InjurySite site, CaseSeverity severity,
            VitalImpact impact, long nowMs, List<DomainEvent> events)
        {
            if (impact != null)
            {
                Vitals before = d.Vitals.Clone();
                d.Vitals.Health = Clamp(d.Vitals.Health + impact.Health);
                d.Vitals.Mobility = Clamp(d.Vitals.Mobility + impact.Mobility);
                d.Vitals.Comfort = Clamp(d.Vitals.Comfort + impact.Comfort);
                events.Add(new VitalsChanged(d.Uid, before, d.Vitals.Clone()));
            }
            bool queued = ActiveCount(s) >= HealthCatalog.StationSlots;
            var c = new HealthCase
            {
                CaseId = caseId, DragonId = d.Uid, EventId = ev?.Id, Cause = ev?.Hazard ?? HazardId.Wildfire, Site = site, Severity = severity,
                Symptoms = new List<string>(HealthCatalog.Site(site).Symptoms), CreatedAtMs = nowMs, Phase = CasePhase.Stabilize,
                Status = queued ? CaseStatus.Queued : CaseStatus.Active,
            };
            s.HealthCases.Add(c);
            DragonLocation from = d.Location;
            string zone = ev?.ZoneId ?? from.ZoneId ?? "origin";
            d.Location = queued ? DragonLocation.Shelter(zone) : DragonLocation.Care(caseId);
            d.Activity = DragonActivity.Care;
            if (s.ProductionDragonIds.Remove(d.Uid)) events.Add(new ProductionChanged(d.Uid, false));
            events.Add(new DragonMoved(d.Uid, from, d.Location));
            events.Add(new HealthCaseOpened(caseId, d.Uid, ev?.Id, site, severity, queued));
            return c;
        }

        private static int Clamp(int v) => Math.Max(HealthCatalog.VitalMin, Math.Min(HealthCatalog.VitalMax, v));

        /// <summary>Đưa ca chờ vào trạm theo FIFO (thứ tự tạo ca) khi có chỗ trống.</summary>
        internal static void AdmitQueued(GameState s, List<DomainEvent> events)
        {
            foreach (HealthCase c in s.HealthCases)
            {
                if (ActiveCount(s) >= HealthCatalog.StationSlots) return;
                if (c.Status != CaseStatus.Queued) continue;
                c.Status = CaseStatus.Active;
                DragonState d = s.FindDragon(c.DragonId);
                DragonLocation from = d.Location;
                d.Location = DragonLocation.Care(c.CaseId);
                events.Add(new DragonMoved(d.Uid, from, d.Location));
                events.Add(new HealthCaseAdmitted(c.CaseId, d.Uid));
            }
        }

        // ---------------- Lệnh ----------------

        private static DomainErrorCode? Find(GameState s, string caseId, out HealthCase c)
        {
            c = s.FindCase(caseId);
            return c == null ? DomainErrorCode.UnknownCase : (DomainErrorCode?)null;
        }

        public static DomainErrorCode? CheckAssess(GameState s, string caseId)
        {
            DomainErrorCode? e = Find(s, caseId, out HealthCase c);
            if (e != null) return e;
            return c.Assessed ? DomainErrorCode.CaseAlreadyAssessed : (DomainErrorCode?)null;
        }

        public static DomainErrorCode? ApplyAssess(GameState s, string caseId, List<DomainEvent> events)
        {
            DomainErrorCode? e = CheckAssess(s, caseId);
            if (e != null) return e;
            HealthCase c = s.FindCase(caseId);
            c.Assessed = true;
            events.Add(new HealthCaseAssessed(c.CaseId, new List<string>(c.Symptoms)));
            return null;
        }

        /// <summary>
        /// setCarePlan: cần đã nhận định, chưa bắt đầu. Lựa chọn không phù hợp → lỗi care-plan-unsuitable kèm khóa giải thích,
        /// state không đổi, không cấp bonus, không gây tổn thương mới (GDD tr. 10).
        /// </summary>
        public static DomainErrorCode? CheckSetPlan(GameState s, string caseId, string planId, out string explanationKey)
        {
            explanationKey = null;
            DomainErrorCode? e = Find(s, caseId, out HealthCase c);
            if (e != null) return e;
            if (!c.Assessed) return DomainErrorCode.CaseNotAssessed;
            if (c.StartedAtMs > 0) return DomainErrorCode.CareAlreadyStarted;
            if (!HealthCatalog.TryGetPlan(planId, out CarePlanDef plan)) return DomainErrorCode.UnknownCarePlan;
            if (plan.Site != c.Site) return DomainErrorCode.CarePlanWrongSite;
            explanationKey = plan.ExplanationKey;
            if (!plan.Suitable) return DomainErrorCode.CarePlanUnsuitable;
            return c.CarePlanId == planId ? DomainErrorCode.AlreadyThere : (DomainErrorCode?)null;
        }

        public static DomainErrorCode? ApplySetPlan(GameState s, string caseId, string planId, List<DomainEvent> events)
        {
            DomainErrorCode? e = CheckSetPlan(s, caseId, planId, out _);
            if (e != null) return e;
            s.FindCase(caseId).CarePlanId = planId;
            events.Add(new CarePlanSet(caseId, planId));
            return null;
        }

        /// <summary>startCare: ca đã vào trạm, đã nhận định, chưa bắt đầu. Có kế hoạch phù hợp → 3/4 thời lượng; không có → thời lượng mặc định.</summary>
        public static DomainErrorCode? CheckStart(GameState s, string caseId)
        {
            DomainErrorCode? e = Find(s, caseId, out HealthCase c);
            if (e != null) return e;
            if (c.Status == CaseStatus.Queued) return DomainErrorCode.CaseQueued;
            if (!c.Assessed) return DomainErrorCode.CaseNotAssessed;
            if (c.StartedAtMs > 0) return DomainErrorCode.CareAlreadyStarted;
            return null;
        }

        public static DomainErrorCode? ApplyStart(GameState s, string caseId, long nowMs, List<DomainEvent> events)
        {
            DomainErrorCode? e = CheckStart(s, caseId);
            if (e != null) return e;
            HealthCase c = s.FindCase(caseId);
            bool bonus = c.CarePlanId != null && HealthCatalog.TryGetPlan(c.CarePlanId, out CarePlanDef p) && p.Suitable;
            c.StartedAtMs = Math.Max(1, nowMs);
            c.DurationMs = HealthCatalog.DurationMs(c.Severity, bonus);
            c.FinishAtMs = c.StartedAtMs + c.DurationMs;
            c.Phase = PhaseAt(c, c.StartedAtMs, out int progress, out long next);
            c.Progress = progress;
            c.NextReviewAtMs = next;
            events.Add(new CareStarted(c.CaseId, c.DurationMs, c.FinishAtMs, bonus));
            return null;
        }

        /// <summary>reviewCase: lượng giá sau khi đủ thời lượng (bước "theo dõi trước khi cho bay lại", G0 S02).</summary>
        public static DomainErrorCode? CheckReview(GameState s, string caseId)
        {
            DomainErrorCode? e = Find(s, caseId, out HealthCase c);
            if (e != null) return e;
            if (c.StartedAtMs <= 0) return DomainErrorCode.CareNotStarted;
            if (!IsComplete(c)) return DomainErrorCode.CareNotComplete;
            return c.Reviewed ? DomainErrorCode.CaseAlreadyReviewed : (DomainErrorCode?)null;
        }

        public static DomainErrorCode? ApplyReview(GameState s, string caseId, List<DomainEvent> events)
        {
            DomainErrorCode? e = CheckReview(s, caseId);
            if (e != null) return e;
            s.FindCase(caseId).Reviewed = true;
            events.Add(new HealthCaseReviewed(caseId));
            return null;
        }

        /// <summary>
        /// dischargeDragon: ca đã lượng giá. Đích phải là kho an toàn hoặc microhabitat hợp lệ còn chỗ (đi qua đủ luật bố trí như từ kho);
        /// nếu đầy thì lệnh bị từ chối và rồng tiếp tục ở trạm an toàn (GDD tr. 11 "nếu đầy thì chờ an toàn").
        /// </summary>
        public static DomainErrorCode? CheckDischarge(GameState s, string caseId, MoveTarget target)
        {
            DomainErrorCode? e = Find(s, caseId, out HealthCase c);
            if (e != null) return e;
            if (!c.Reviewed) return DomainErrorCode.CaseNotReviewed;
            if (target.Kind == LocationKind.Storage) return null;
            DragonState probe = s.FindDragon(c.DragonId).Clone();
            probe.Location = DragonLocation.Storage;
            probe.Activity = DragonActivity.Idle;
            Zones.CheckPlacement(s, probe, target.ZoneId, target.HabitatId, out DomainErrorCode? blocked);
            return blocked;
        }

        public static DomainErrorCode? ApplyDischarge(GameState s, string caseId, MoveTarget target, List<DomainEvent> events)
        {
            DomainErrorCode? e = CheckDischarge(s, caseId, target);
            if (e != null) return e;
            HealthCase c = s.FindCase(caseId);
            DragonState d = s.FindDragon(c.DragonId);
            DragonLocation from = d.Location;
            d.Location = target.Kind == LocationKind.Storage ? DragonLocation.Storage : DragonLocation.Zone(target.ZoneId, target.HabitatId);
            d.Activity = DragonActivity.Idle;
            s.HealthCases.Remove(c);
            events.Add(new DragonMoved(d.Uid, from, d.Location));
            events.Add(new DragonDischarged(caseId, d.Uid, d.Location));
            AdmitQueued(s, events);
            DisasterEngine.RefreshAll(s, events);
            return null;
        }
    }

    /// <summary>
    /// Máy trạng thái thiên tai (GDD tr. 9; ADR 0003; G0 PACK_README) — chỉ chạy khi <see cref="DisasterRules.Enabled"/>.
    /// - Kiểm hazard theo thời gian World foreground online; offline / Zen / Companion không sinh sự kiện hay tổn thương mới.
    /// - Tối đa một sự kiện mở mỗi thế giới; cooldown 24 h sau khi đóng; dự báo 10 phút; tối đa 2 ca mỗi sự kiện.
    /// - Risk = clamp(severity × exposure × (1 − resilience) × (1 − protection), 0, 1); roll một lần theo eventId + dragonId.
    /// - RNG: hashSeed("hazard", saveId, zoneId, checkIndex), hashSeed("injury", eventId, uid), hashSeed("injury-site", eventId, uid).
    /// </summary>
    public static class DisasterEngine
    {
        /// <summary>Thời gian World foreground còn lại tới mốc kế tiếp (kiểm hazard hoặc hết dự báo). null = không có mốc.</summary>
        internal static long? NextMilestoneOffset(GameState s, DisasterRules r)
        {
            long f = s.Hazard.ForegroundMs;
            long next = (s.Hazard.ChecksDone + 1) * r.CheckIntervalForegroundMs - f;
            long best = Math.Max(1, next);
            foreach (DisasterState ev in s.Disasters)
                if (ev.Phase == DisasterPhase.Forecast) best = Math.Min(best, Math.Max(1, HealthCatalog.ForecastMs - ev.ForecastProgressMs));
            return best;
        }

        /// <summary>Tích thời gian World foreground cho đồng hồ hazard và cho dự báo đang chạy.</summary>
        internal static void Accrue(GameState s, long elapsedMs)
        {
            if (elapsedMs <= 0) return;
            s.Hazard.ForegroundMs = checked(s.Hazard.ForegroundMs + elapsedMs);
            foreach (DisasterState ev in s.Disasters)
                if (ev.Phase == DisasterPhase.Forecast) ev.ForecastProgressMs = Math.Min(HealthCatalog.ForecastMs, ev.ForecastProgressMs + elapsedMs);
        }

        /// <summary>Xử lý mọi mốc đã tới tại atMs: các lần kiểm hazard đến hạn (theo thứ tự), rồi các dự báo đã đủ 10 phút.</summary>
        internal static void ProcessMilestones(GameState s, DisasterRules r, long atMs, List<DomainEvent> events)
        {
            long due = s.Hazard.ForegroundMs / r.CheckIntervalForegroundMs;
            while (s.Hazard.ChecksDone < due)
            {
                s.Hazard.ChecksDone++;
                RunCheck(s, r, s.Hazard.ChecksDone, atMs, events);
            }
            foreach (DisasterState ev in new List<DisasterState>(s.Disasters))
                if (ev.Phase == DisasterPhase.Forecast && ev.ForecastProgressMs >= HealthCatalog.ForecastMs) Strike(s, r, ev, atMs, events);
        }

        private static void RunCheck(GameState s, DisasterRules r, long index, long atMs, List<DomainEvent> events)
        {
            string idx = index.ToString(CultureInfo.InvariantCulture);
            foreach (string zoneId in WorldCatalog.ZoneIds)
            {
                ZoneState z = s.FindZone(zoneId);
                if (z == null || !z.Unlocked || s.OpenDisasterIn(zoneId) != null || atMs < s.Hazard.CooldownOf(zoneId)) continue;
                if (!r.TryHazardOf(zoneId, out HazardId hazard)) continue;
                IRng rng = Rng.Create(Rng.HashSeed("hazard", s.SaveId, zoneId, idx));
                if (!(rng.Next() < r.Chance)) continue;
                CaseSeverity severity;
                // GDD tr. 4: Cozy = thiên tai minh họa, tổn thương nhẹ. Ecosystem: mức sự kiện chưa được GDD xác định (ADR 0202 P2).
                if (s.Player.Settings.Difficulty == Difficulty.Cozy) severity = CaseSeverity.Mild;
                else if (r.SeverityPolicy == EventSeverityPolicy.Uniform) severity = rng.Next() < 0.5 ? CaseSeverity.Mild : CaseSeverity.Moderate;
                else continue;
                var ev = new DisasterState
                {
                    Id = "dis_" + zoneId + "_" + idx, Hazard = hazard, ZoneId = zoneId, Severity = severity, Phase = DisasterPhase.Forecast, CreatedAtMs = atMs,
                };
                s.Disasters.Add(ev);
                events.Add(new HazardForecast(ev.Id, zoneId, hazard, severity, HealthCatalog.ForecastMs));
            }
        }

        private static void SetPhase(DisasterState ev, DisasterPhase to, List<DomainEvent> events)
        {
            if (ev.Phase == to) return;
            events.Add(new DisasterPhaseChanged(ev.Id, ev.Phase, to));
            ev.Phase = to;
        }

        /// <summary>Rồng tiếp xúc với hazard tại thời điểm xảy ra: đang ở microhabitat của zone, rảnh, không có ca mở.</summary>
        public static bool IsExposed(GameState s, DisasterState ev, DragonState d) =>
            d.Location.Kind == LocationKind.Zone && d.Location.ZoneId == ev.ZoneId && d.Activity == DragonActivity.Idle && s.OpenCaseOf(d.Uid) == null;

        /// <summary>Risk theo GDD tr. 9 (protection = 0 khi tiếp xúc; rồng trú an toàn không nằm trong snapshot → risk 0).</summary>
        public static double RiskOf(DisasterState ev, DragonState d, double protection = 0)
        {
            double res = SpeciesCatalog.GetBiology(d.SpeciesId).ResiliencePct[ev.Hazard] / 100.0;
            double risk = HealthCatalog.HazardSeverityCoefficient(ev.Severity) * 1.0 * (1 - res) * (1 - protection);
            return Math.Max(0, Math.Min(1, risk));
        }

        private static void Strike(GameState s, DisasterRules r, DisasterState ev, long atMs, List<DomainEvent> events)
        {
            // GDD: 10 phút dự báo là thời gian chuẩn bị; Preparation chốt snapshot, Active là lúc hazard xảy ra (ADR 0202 P5).
            SetPhase(ev, DisasterPhase.Preparation, events);
            SetPhase(ev, DisasterPhase.Active, events);
            ev.StruckAtMs = atMs;
            var risks = new List<KeyValuePair<string, double>>();
            var injured = new List<KeyValuePair<DragonState, double>>();
            foreach (DragonState d in s.Dragons)
            {
                if (d.Location.Kind == LocationKind.Shelter && ev.Sheltered.Exists(x => x.Uid == d.Uid)) { risks.Add(new KeyValuePair<string, double>(d.Uid, 0)); continue; }
                if (!IsExposed(s, ev, d)) continue;
                double risk = RiskOf(ev, d);
                risks.Add(new KeyValuePair<string, double>(d.Uid, risk));
                double roll = Rng.Create(Rng.HashSeed("injury", ev.Id, d.Uid)).Next();
                if (roll < risk) injured.Add(new KeyValuePair<DragonState, double>(d, risk));
            }
            // Nhiều hơn 2 ca: chọn risk giảm dần rồi UID (G0 PACK_README).
            injured.Sort((a, b) => a.Value != b.Value ? b.Value.CompareTo(a.Value) : string.CompareOrdinal(a.Key.Uid, b.Key.Uid));
            var caseIds = new List<string>();
            for (int i = 0; i < injured.Count && i < HealthCatalog.MaxCasesPerEvent; i++)
            {
                DragonState d = injured[i].Key;
                IReadOnlyList<InjurySite> sites = HealthCatalog.SitesFor(ev.Hazard);
                InjurySite site = sites.Count == 1 ? sites[0] : Rng.Create(Rng.HashSeed("injury-site", ev.Id, d.Uid)).PickUniform(sites);
                string caseId = "case_" + ev.Id + "_" + i.ToString(CultureInfo.InvariantCulture);
                CareCenter.Open(s, caseId, ev, d, site, ev.Severity, r.Impact?.Invoke(site, ev.Severity), atMs, events);
                ev.CaseIds.Add(caseId);
                caseIds.Add(caseId);
            }
            events.Add(new DisasterStruck(ev.Id, risks, caseIds));
            ReturnSheltered(s, ev, events);
            Refresh(s, ev, events);
        }

        /// <summary>Hazard đã qua: rồng trú về đúng microhabitat cũ (nếu còn hợp lệ và còn chỗ), ngược lại vào kho an toàn.</summary>
        private static void ReturnSheltered(GameState s, DisasterState ev, List<DomainEvent> events)
        {
            foreach (ShelteredDragon sh in ev.Sheltered)
            {
                DragonState d = s.FindDragon(sh.Uid);
                if (d == null || d.Location.Kind != LocationKind.Shelter) continue;
                DragonState probe = d.Clone();
                probe.Location = DragonLocation.Storage;
                Zones.CheckPlacement(s, probe, ev.ZoneId, sh.HabitatId, out DomainErrorCode? blocked);
                DragonLocation from = d.Location;
                d.Location = blocked == null ? DragonLocation.Zone(ev.ZoneId, sh.HabitatId) : DragonLocation.Storage;
                bool restored = false;
                if (blocked == null && sh.Producing && s.ProductionDragonIds.Count < Zones.CurrentProductionBudget(s) && Placement.IsPlacedInValidHabitat(s, d))
                {
                    s.ProductionDragonIds.Add(d.Uid);
                    restored = true;
                }
                events.Add(new DragonMoved(d.Uid, from, d.Location));
                if (restored) events.Add(new ProductionChanged(d.Uid, true));
                events.Add(new DragonLeftShelter(d.Uid, d.Location, restored));
            }
            ev.Sheltered.Clear();
        }

        /// <summary>Sau khi xảy ra: Rescue khi còn ca chờ trạm, Recovery khi còn ca đang chăm sóc, Closed khi mọi ca đã xuất viện.</summary>
        private static void Refresh(GameState s, DisasterState ev, List<DomainEvent> events)
        {
            if (ev.Phase == DisasterPhase.Forecast || ev.Phase == DisasterPhase.Preparation) return;
            bool queued = false, active = false;
            foreach (HealthCase c in s.HealthCases)
                if (c.EventId == ev.Id) { if (c.Status == CaseStatus.Queued) queued = true; else active = true; }
            if (queued) { SetPhase(ev, DisasterPhase.Rescue, events); return; }
            if (active) { SetPhase(ev, DisasterPhase.Recovery, events); return; }
            SetPhase(ev, DisasterPhase.Closed, events);
            long until = (ev.StruckAtMs > 0 ? Math.Max(ev.StruckAtMs, s.Clock.LastProcessedAtMs) : s.Clock.LastProcessedAtMs) + HealthCatalog.CooldownMs;
            s.Hazard.SetCooldown(ev.ZoneId, until);
            s.Disasters.Remove(ev);
            events.Add(new DisasterClosed(ev.Id, ev.ZoneId, until));
        }

        internal static void RefreshAll(GameState s, List<DomainEvent> events)
        {
            foreach (DisasterState ev in new List<DisasterState>(s.Disasters)) Refresh(s, ev, events);
        }

        /// <summary>
        /// Offline / Zen / Companion trong lúc có cảnh báo: tự đưa rồng tiếp xúc vào nơi trú (GDD tr. 9), nên khi hazard xảy ra chúng có risk 0.
        /// Không sinh tổn thương; ca đã có giữ nguyên.
        /// </summary>
        internal static void AutoShelter(GameState s, List<DomainEvent> events)
        {
            foreach (DisasterState ev in s.Disasters)
            {
                if (ev.Phase != DisasterPhase.Forecast && ev.Phase != DisasterPhase.Preparation) continue;
                foreach (DragonState d in s.Dragons)
                    if (IsExposed(s, ev, d)) Shelter(s, ev, d, true, events);
            }
        }

        private static void Shelter(GameState s, DisasterState ev, DragonState d, bool auto, List<DomainEvent> events)
        {
            bool producing = s.ProductionDragonIds.Remove(d.Uid);
            ev.Sheltered.Add(new ShelteredDragon { Uid = d.Uid, HabitatId = d.Location.HabitatId, Producing = producing, Auto = auto });
            DragonLocation from = d.Location;
            d.Location = DragonLocation.Shelter(ev.ZoneId);
            if (producing) events.Add(new ProductionChanged(d.Uid, false));
            events.Add(new DragonMoved(d.Uid, from, d.Location));
            events.Add(new DragonSheltered(d.Uid, ev.ZoneId, ev.Id, auto));
        }

        /// <summary>moveToShelter: chỉ trong lúc cảnh báo (Forecast / Preparation) của zone đang chứa rồng; rồng phải rảnh, không có ca.</summary>
        public static DomainErrorCode? CheckMoveToShelter(GameState s, DragonState d)
        {
            if (d.Location.Kind != LocationKind.Zone || d.Activity != DragonActivity.Idle || s.OpenCaseOf(d.Uid) != null) return DomainErrorCode.DragonUnavailable;
            DisasterState ev = s.OpenDisasterIn(d.Location.ZoneId);
            if (ev == null || (ev.Phase != DisasterPhase.Forecast && ev.Phase != DisasterPhase.Preparation)) return DomainErrorCode.NoDisasterWarning;
            return null;
        }

        public static DomainErrorCode? ApplyMoveToShelter(GameState s, DragonState d, List<DomainEvent> events)
        {
            DomainErrorCode? e = CheckMoveToShelter(s, d);
            if (e != null) return e;
            Shelter(s, s.OpenDisasterIn(d.Location.ZoneId), d, false, events);
            return null;
        }
    }
}
