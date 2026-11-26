using System;
using System.Collections.Generic;
using Dravio.Domain.Biology;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Health;
using Dravio.Domain.World;

namespace Dravio.Domain.Queries
{
    /// <summary>Sức khỏe một rồng: chỉ số, ca mở, điều kiện lai / thám hiểm còn thiếu, độ phù hợp môi trường hiện tại.</summary>
    public sealed class DragonHealthOverview
    {
        public string DragonUid { get; internal set; }
        /// <summary>Bản sao chỉ số (null chỉ với state trước v8 chưa migrate).</summary>
        public Vitals Vitals { get; internal set; }
        public string OpenCaseId { get; internal set; }
        /// <summary>Điều kiện lai còn thiếu (ADR 0003), rỗng = đạt phần sức khỏe.</summary>
        public IReadOnlyList<string> BreedingShortfalls { get; internal set; }
        /// <summary>Điều kiện thám hiểm còn thiếu (ADR 0201 mục 4), rỗng = đạt phần sức khỏe.</summary>
        public IReadOnlyList<string> ExpeditionShortfalls { get; internal set; }
        /// <summary>Suitability tại microhabitat hiện tại (null nếu không ở zone). Chỉ hiển thị: GDD chưa có công thức nối môi trường → comfort (ADR 0202 P8).</summary>
        public int? HabitatScore { get; internal set; }
        public SuitabilityTier? HabitatTier { get; internal set; }
        /// <summary>Đang ở zone có cảnh báo (Forecast / Preparation) và chưa trú → nên gửi moveToShelter.</summary>
        public bool ExposedToWarning { get; internal set; }
    }

    /// <summary>Một ca chăm sóc cho UI: phase, tiến trình tại "bây giờ" (đã quyết toán), lệnh kế tiếp hợp lệ.</summary>
    public sealed class CaseOverview
    {
        public string CaseId { get; internal set; }
        public string DragonUid { get; internal set; }
        public string EventId { get; internal set; }
        public HazardId Cause { get; internal set; }
        public InjurySite Site { get; internal set; }
        public CaseSeverity Severity { get; internal set; }
        public CaseStatus Status { get; internal set; }
        /// <summary>Vị trí trong hàng chờ FIFO (1-based), 0 nếu đã vào trạm.</summary>
        public int QueuePosition { get; internal set; }
        /// <summary>Triệu chứng chỉ hiện sau khi nhận định.</summary>
        public IReadOnlyList<string> Symptoms { get; internal set; }
        public bool Assessed { get; internal set; }
        public string CarePlanId { get; internal set; }
        public CasePhase Phase { get; internal set; }
        public int Progress { get; internal set; }
        public long FinishAtMs { get; internal set; }
        public long NextReviewAtMs { get; internal set; }
        public bool Reviewed { get; internal set; }
        /// <summary>Tên lệnh UI nên mời người chơi gửi tiếp: assessCase / setCarePlan|startCare / wait / reviewCase / dischargeDragon / waitForStation.</summary>
        public string NextStep { get; internal set; }
    }

    public sealed class CarePlanOption
    {
        public string PlanId { get; internal set; }
        public bool Suitable { get; internal set; }
        public string ExplanationKey { get; internal set; }
    }

    public sealed class StationOverview
    {
        public int Slots { get; internal set; }
        public int Used { get; internal set; }
        public IReadOnlyList<string> Queue { get; internal set; }
    }

    public sealed class DisasterOverview
    {
        public string ZoneId { get; internal set; }
        /// <summary>null = không có sự kiện mở.</summary>
        public string EventId { get; internal set; }
        public HazardId? Hazard { get; internal set; }
        public CaseSeverity? Severity { get; internal set; }
        public DisasterPhase? Phase { get; internal set; }
        /// <summary>Thời gian World foreground còn lại tới lúc hazard xảy ra (chỉ khi Forecast).</summary>
        public long ForecastRemainingMs { get; internal set; }
        public long CooldownUntilMs { get; internal set; }
        public IReadOnlyList<string> Sheltered { get; internal set; }
        public IReadOnlyList<string> CaseIds { get; internal set; }
    }

    public sealed class DisasterSystemOverview
    {
        /// <summary>false ở bản chạy thật cho tới khi ADR 0202 được duyệt.</summary>
        public bool NaturalDisastersEnabled { get; internal set; }
        public IReadOnlyList<string> PendingDecisions { get; internal set; }
        public long ForegroundMs { get; internal set; }
        public long NextCheckInForegroundMs { get; internal set; }
        public IReadOnlyList<DisasterOverview> Zones { get; internal set; }
    }

    /// <summary>
    /// Truy vấn chỉ đọc của U2.6 cho Unity (cùng nguyên tắc với <see cref="Ecosystem"/>): DTO là snapshot, không giữ tham chiếu vào GameState.
    /// Đọc trên <c>GameSession.State</c> (đã quyết toán mỗi tick 1 Hz).
    /// </summary>
    public static class HealthQueries
    {
        public static DragonHealthOverview Dragon(GameState s, string uid)
        {
            DragonState d = s.FindDragon(uid);
            if (d == null) return null;
            int? score = null; SuitabilityTier? tier = null;
            if (d.Location.Kind == LocationKind.Zone && HabitatCatalog.Contains(d.Location.HabitatId))
            {
                HabitatResult r = Suitability.ForSpecies(d.SpeciesId, d.Location.HabitatId);
                score = r.Score; tier = Suitability.TierOf(r);
            }
            DisasterState ev = d.Location.Kind == LocationKind.Zone ? s.OpenDisasterIn(d.Location.ZoneId) : null;
            return new DragonHealthOverview
            {
                DragonUid = d.Uid, Vitals = d.Vitals?.Clone(), OpenCaseId = s.OpenCaseOf(d.Uid)?.CaseId,
                BreedingShortfalls = HealthChecks.BreedingShortfalls(s, d), ExpeditionShortfalls = HealthChecks.ExpeditionShortfalls(s, d),
                HabitatScore = score, HabitatTier = tier,
                ExposedToWarning = ev != null && (ev.Phase == DisasterPhase.Forecast || ev.Phase == DisasterPhase.Preparation) && DisasterEngine.IsExposed(s, ev, d),
            };
        }

        public static List<CaseOverview> Cases(GameState s)
        {
            var list = new List<CaseOverview>();
            int q = 0;
            foreach (HealthCase c in s.HealthCases)
            {
                if (c.Status == CaseStatus.Queued) q++;
                string next = c.Status == CaseStatus.Queued ? "waitForStation"
                    : !c.Assessed ? "assessCase"
                    : c.StartedAtMs == 0 ? "setCarePlan|startCare"
                    : c.Progress < 1000 ? "wait"
                    : !c.Reviewed ? "reviewCase" : "dischargeDragon";
                list.Add(new CaseOverview
                {
                    CaseId = c.CaseId, DragonUid = c.DragonId, EventId = c.EventId, Cause = c.Cause, Site = c.Site, Severity = c.Severity, Status = c.Status,
                    QueuePosition = c.Status == CaseStatus.Queued ? q : 0, Symptoms = c.Assessed ? new List<string>(c.Symptoms) : new List<string>(),
                    Assessed = c.Assessed, CarePlanId = c.CarePlanId, Phase = c.Phase, Progress = c.Progress, FinishAtMs = c.FinishAtMs, NextReviewAtMs = c.NextReviewAtMs,
                    Reviewed = c.Reviewed, NextStep = next,
                });
            }
            return list;
        }

        /// <summary>Kế hoạch cho vị trí tổn thương của ca (kể cả lựa chọn không phù hợp, để UI hiện giải thích).</summary>
        public static List<CarePlanOption> CarePlans(GameState s, string caseId)
        {
            var list = new List<CarePlanOption>();
            HealthCase c = s.FindCase(caseId);
            if (c == null) return list;
            foreach (CarePlanDef p in HealthCatalog.PlansFor(c.Site))
                list.Add(new CarePlanOption { PlanId = p.Id, Suitable = p.Suitable, ExplanationKey = p.ExplanationKey });
            return list;
        }

        public static StationOverview Station(GameState s)
        {
            var queue = new List<string>();
            foreach (HealthCase c in s.HealthCases) if (c.Status == CaseStatus.Queued) queue.Add(c.CaseId);
            return new StationOverview { Slots = HealthCatalog.StationSlots, Used = CareCenter.ActiveCount(s), Queue = queue };
        }

        /// <summary>Đúng mã lỗi dischargeDragon sẽ trả với đích này (null = hợp lệ).</summary>
        public static DomainErrorCode? DischargeEligibility(GameState s, string caseId, MoveTarget target) => CareCenter.CheckDischarge(s, caseId, target);

        public static DisasterSystemOverview Disasters(GameState s, DisasterRules rules = null)
        {
            rules = rules ?? DisasterRules.Production;
            var zones = new List<DisasterOverview>();
            foreach (string z in WorldCatalog.ZoneIds)
            {
                DisasterState ev = s.OpenDisasterIn(z);
                var sh = new List<string>();
                if (ev != null) foreach (ShelteredDragon x in ev.Sheltered) sh.Add(x.Uid);
                zones.Add(new DisasterOverview
                {
                    ZoneId = z, EventId = ev?.Id, Hazard = ev?.Hazard, Severity = ev?.Severity, Phase = ev?.Phase,
                    ForecastRemainingMs = ev != null && ev.Phase == DisasterPhase.Forecast ? HealthCatalog.ForecastMs - ev.ForecastProgressMs : 0,
                    CooldownUntilMs = s.Hazard.CooldownOf(z), Sheltered = sh, CaseIds = ev == null ? new List<string>() : new List<string>(ev.CaseIds),
                });
            }
            long next = (s.Hazard.ChecksDone + 1) * rules.CheckIntervalForegroundMs - s.Hazard.ForegroundMs;
            return new DisasterSystemOverview
            {
                NaturalDisastersEnabled = rules.Enabled, PendingDecisions = rules.PendingDecisions,
                ForegroundMs = s.Hazard.ForegroundMs, NextCheckInForegroundMs = Math.Max(0, next), Zones = zones,
            };
        }
    }
}
