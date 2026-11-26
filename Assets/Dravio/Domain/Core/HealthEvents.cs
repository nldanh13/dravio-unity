using System.Collections.Generic;

namespace Dravio.Domain.Core
{
    // ---- U2.6: event sức khỏe / thiên tai / cứu hộ (implementation mới; TS b33ec7a không có) ----

    public sealed class HazardForecast : DomainEvent
    {
        public HazardForecast(string eventId, string zoneId, HazardId hazard, CaseSeverity severity, long forecastMs)
        {
            EventId = eventId; ZoneId = zoneId; Hazard = hazard; Severity = severity; ForecastMs = forecastMs;
        }
        public override string Type => "hazardForecast";
        public string EventId { get; }
        public string ZoneId { get; }
        public HazardId Hazard { get; }
        public CaseSeverity Severity { get; }
        /// <summary>Thời gian dự báo cần tích (World foreground).</summary>
        public long ForecastMs { get; }
    }

    public sealed class DisasterPhaseChanged : DomainEvent
    {
        public DisasterPhaseChanged(string eventId, DisasterPhase from, DisasterPhase to) { EventId = eventId; From = from; To = to; }
        public override string Type => "disasterPhaseChanged";
        public string EventId { get; }
        public DisasterPhase From { get; }
        public DisasterPhase To { get; }
    }

    /// <summary>Kết quả roll một lần trên snapshot (GDD tr. 9): mỗi rồng tiếp xúc một risk và kết quả.</summary>
    public sealed class DisasterStruck : DomainEvent
    {
        public DisasterStruck(string eventId, IReadOnlyList<KeyValuePair<string, double>> risks, IReadOnlyList<string> caseIds)
        {
            EventId = eventId; Risks = risks; CaseIds = caseIds;
        }
        public override string Type => "disasterStruck";
        public string EventId { get; }
        /// <summary>UID → risk 0–1 (rồng trú an toàn có risk 0 và không được roll).</summary>
        public IReadOnlyList<KeyValuePair<string, double>> Risks { get; }
        public IReadOnlyList<string> CaseIds { get; }
    }

    public sealed class DisasterClosed : DomainEvent
    {
        public DisasterClosed(string eventId, string zoneId, long cooldownUntilMs) { EventId = eventId; ZoneId = zoneId; CooldownUntilMs = cooldownUntilMs; }
        public override string Type => "disasterClosed";
        public string EventId { get; }
        public string ZoneId { get; }
        public long CooldownUntilMs { get; }
    }

    public sealed class DragonSheltered : DomainEvent
    {
        public DragonSheltered(string dragonUid, string zoneId, string eventId, bool auto) { DragonUid = dragonUid; ZoneId = zoneId; EventId = eventId; Auto = auto; }
        public override string Type => "dragonSheltered";
        public string DragonUid { get; }
        public string ZoneId { get; }
        public string EventId { get; }
        /// <summary>true = tự động (offline / Zen / Companion).</summary>
        public bool Auto { get; }
    }

    public sealed class DragonLeftShelter : DomainEvent
    {
        public DragonLeftShelter(string dragonUid, DragonLocation to, bool productionRestored) { DragonUid = dragonUid; To = to; ProductionRestored = productionRestored; }
        public override string Type => "dragonLeftShelter";
        public string DragonUid { get; }
        /// <summary>Microhabitat cũ, hoặc kho an toàn nếu chỗ cũ không còn dùng được.</summary>
        public DragonLocation To { get; }
        public bool ProductionRestored { get; }
    }

    public sealed class HealthCaseOpened : DomainEvent
    {
        public HealthCaseOpened(string caseId, string dragonUid, string eventId, InjurySite site, CaseSeverity severity, bool queued)
        {
            CaseId = caseId; DragonUid = dragonUid; EventId = eventId; Site = site; Severity = severity; Queued = queued;
        }
        public override string Type => "healthCaseOpened";
        public string CaseId { get; }
        public string DragonUid { get; }
        public string EventId { get; }
        public InjurySite Site { get; }
        public CaseSeverity Severity { get; }
        /// <summary>true = trạm đầy: rồng tự ổn định ở nơi trú và xếp hàng (FIFO).</summary>
        public bool Queued { get; }
    }

    public sealed class HealthCaseAdmitted : DomainEvent
    {
        public HealthCaseAdmitted(string caseId, string dragonUid) { CaseId = caseId; DragonUid = dragonUid; }
        public override string Type => "healthCaseAdmitted";
        public string CaseId { get; }
        public string DragonUid { get; }
    }

    public sealed class HealthCaseAssessed : DomainEvent
    {
        public HealthCaseAssessed(string caseId, IReadOnlyList<string> symptoms) { CaseId = caseId; Symptoms = symptoms; }
        public override string Type => "healthCaseAssessed";
        public string CaseId { get; }
        public IReadOnlyList<string> Symptoms { get; }
    }

    public sealed class CarePlanSet : DomainEvent
    {
        public CarePlanSet(string caseId, string planId) { CaseId = caseId; PlanId = planId; }
        public override string Type => "carePlanSet";
        public string CaseId { get; }
        public string PlanId { get; }
    }

    public sealed class CareStarted : DomainEvent
    {
        public CareStarted(string caseId, long durationMs, long finishAtMs, bool planBonus) { CaseId = caseId; DurationMs = durationMs; FinishAtMs = finishAtMs; PlanBonus = planBonus; }
        public override string Type => "careStarted";
        public string CaseId { get; }
        public long DurationMs { get; }
        public long FinishAtMs { get; }
        public bool PlanBonus { get; }
    }

    public sealed class CasePhaseChanged : DomainEvent
    {
        public CasePhaseChanged(string caseId, CasePhase from, CasePhase to, long atMs) { CaseId = caseId; From = from; To = to; AtMs = atMs; }
        public override string Type => "casePhaseChanged";
        public string CaseId { get; }
        public CasePhase From { get; }
        public CasePhase To { get; }
        public long AtMs { get; }
    }

    /// <summary>Chăm sóc đã đủ thời lượng: chờ lượng giá (reviewCase) rồi xuất viện. Không tự nhận / tự xuất viện.</summary>
    public sealed class CareCompleted : DomainEvent
    {
        public CareCompleted(string caseId, long atMs) { CaseId = caseId; AtMs = atMs; }
        public override string Type => "careCompleted";
        public string CaseId { get; }
        public long AtMs { get; }
    }

    public sealed class HealthCaseReviewed : DomainEvent
    {
        public HealthCaseReviewed(string caseId) { CaseId = caseId; }
        public override string Type => "healthCaseReviewed";
        public string CaseId { get; }
    }

    public sealed class DragonDischarged : DomainEvent
    {
        public DragonDischarged(string caseId, string dragonUid, DragonLocation to) { CaseId = caseId; DragonUid = dragonUid; To = to; }
        public override string Type => "dragonDischarged";
        public string CaseId { get; }
        public string DragonUid { get; }
        public DragonLocation To { get; }
    }

    public sealed class VitalsChanged : DomainEvent
    {
        public VitalsChanged(string dragonUid, Vitals from, Vitals to) { DragonUid = dragonUid; From = from; To = to; }
        public override string Type => "vitalsChanged";
        public string DragonUid { get; }
        public Vitals From { get; }
        public Vitals To { get; }
    }
}
