using System.Collections.Generic;

namespace Dravio.Domain
{
    // U2.6 — Sức khỏe, thiên tai, cứu hộ (GDD 3.1 trang 9–11, Phụ lục C.2 HealthCase / DisasterState; Schema v8, chỉ bản Unity).
    // Chuỗi wire là một phần của Dravio Save Schema v8: không đổi tên sau khi phát hành.

    /// <summary>Mức tổn thương P0 (GDD tr. 10: nhẹ / trung bình; nặng là P1, chưa có).</summary>
    public enum CaseSeverity { Mild, Moderate }

    /// <summary>Vị trí tổn thương P0 (GDD tr. 10). ID ổn định, độc lập tên hiển thị.</summary>
    public enum InjurySite
    {
        [WireName("skin_scale")] SkinScale,
        [WireName("limb_wing")] LimbWing,
        [WireName("respiratory")] Respiratory,
        [WireName("fin_gill")] FinGill,
    }

    /// <summary>Ổn định → lành tổn thương → phục hồi chức năng → tái hòa nhập (GDD tr. 10; Phụ lục C.2).</summary>
    public enum CasePhase { Stabilize, Heal, Rehab, Return }

    /// <summary>queued = chờ chỗ trạm (đang trú an toàn); active = đã vào trạm; closed = đã xuất viện (không còn trong save).</summary>
    public enum CaseStatus { Queued, Active, Closed }

    /// <summary>Forecast → Preparation → Active → Rescue → Recovery → Closed (GDD tr. 9).</summary>
    public enum DisasterPhase { Forecast, Preparation, Active, Rescue, Recovery, Closed }

    /// <summary>
    /// Chỉ số giả tưởng 0–100 (GDD tr. 10: health, energy, comfort, mobility, hydration). Không phải sinh hiệu thật.
    /// Rồng mới nở / rồng có sẵn khi migration nhận giá trị nền <see cref="Catalog.HealthCatalog.VitalBaseline"/> (ADR 0202 D1).
    /// </summary>
    public sealed class Vitals
    {
        public int Health { get; set; } = Catalog.HealthCatalog.VitalBaseline;
        public int Energy { get; set; } = Catalog.HealthCatalog.VitalBaseline;
        public int Comfort { get; set; } = Catalog.HealthCatalog.VitalBaseline;
        public int Mobility { get; set; } = Catalog.HealthCatalog.VitalBaseline;
        public int Hydration { get; set; } = Catalog.HealthCatalog.VitalBaseline;

        public Vitals Clone() => (Vitals)MemberwiseClone();
    }

    /// <summary>
    /// Ca chăm sóc (GDD Phụ lục C.2 HealthCase + các mốc thời gian để tính tiến trình theo mốc, không lưu remainingSec).
    /// Chỉ ca đang mở (queued / active) nằm trong save; ca xuất viện bị xóa và chỉ còn trong event.
    /// </summary>
    public sealed class HealthCase
    {
        public string CaseId { get; set; }
        public string DragonId { get; set; }
        /// <summary>null = ca không đến từ thiên tai (dành cho tutorial / cứu hộ hoang dã P1).</summary>
        public string EventId { get; set; }
        /// <summary>Nguyên nhân: ID hazard (wildfire / strong_current / thunderstorm).</summary>
        public HazardId Cause { get; set; }
        public InjurySite Site { get; set; }
        public CaseSeverity Severity { get; set; }
        /// <summary>Khóa triệu chứng giả tưởng (i18n), theo catalog vị trí tổn thương.</summary>
        public List<string> Symptoms { get; set; } = new List<string>();
        public bool Assessed { get; set; }
        public string CarePlanId { get; set; }
        public long CreatedAtMs { get; set; }
        /// <summary>0 = chưa bắt đầu chăm sóc (tự ổn định, chờ quyết định).</summary>
        public long StartedAtMs { get; set; }
        /// <summary>0 khi chưa bắt đầu.</summary>
        public long DurationMs { get; set; }
        /// <summary>StartedAtMs + DurationMs; 0 khi chưa bắt đầu.</summary>
        public long FinishAtMs { get; set; }
        /// <summary>Mốc lượng giá kế tiếp (đổi phase hoặc hoàn tất); 0 khi chưa bắt đầu.</summary>
        public long NextReviewAtMs { get; set; }
        public CasePhase Phase { get; set; }
        /// <summary>Tiến trình 0–1000 (‰), đơn điệu tăng.</summary>
        public int Progress { get; set; }
        /// <summary>Đã lượng giá sau khi hoàn tất — điều kiện xuất viện.</summary>
        public bool Reviewed { get; set; }
        public CaseStatus Status { get; set; }

        public HealthCase Clone()
        {
            var c = (HealthCase)MemberwiseClone();
            c.Symptoms = Symptoms == null ? null : new List<string>(Symptoms);
            return c;
        }
    }

    /// <summary>Rồng được đưa vào nơi trú của zone trong lúc cảnh báo; quay về đúng microhabitat cũ khi hazard qua.</summary>
    public sealed class ShelteredDragon
    {
        public string Uid { get; set; }
        public string HabitatId { get; set; }
        /// <summary>Đang trong ngân sách sản xuất lúc vào trú — được trả lại nếu còn ngân sách khi quay về.</summary>
        public bool Producing { get; set; }
        /// <summary>true = hệ thống tự đưa vào trú (offline / Zen / Companion); false = người chơi gửi moveToShelter.</summary>
        public bool Auto { get; set; }

        public ShelteredDragon Clone() => (ShelteredDragon)MemberwiseClone();
    }

    /// <summary>Sự kiện thiên tai đang mở (GDD Phụ lục C.2 Disaster). Sự kiện đóng bị xóa; cooldown nằm trong <see cref="HazardState"/>.</summary>
    public sealed class DisasterState
    {
        public string Id { get; set; }
        public HazardId Hazard { get; set; }
        public string ZoneId { get; set; }
        public CaseSeverity Severity { get; set; }
        public DisasterPhase Phase { get; set; }
        public long CreatedAtMs { get; set; }
        /// <summary>Thời gian dự báo đã tích (chỉ khi World foreground online).</summary>
        public long ForecastProgressMs { get; set; }
        /// <summary>0 = chưa xảy ra.</summary>
        public long StruckAtMs { get; set; }
        public List<string> CaseIds { get; set; } = new List<string>();
        public List<ShelteredDragon> Sheltered { get; set; } = new List<ShelteredDragon>();

        public DisasterState Clone()
        {
            var c = (DisasterState)MemberwiseClone();
            c.CaseIds = CaseIds == null ? null : new List<string>(CaseIds);
            if (Sheltered != null) { c.Sheltered = new List<ShelteredDragon>(Sheltered.Count); foreach (var x in Sheltered) c.Sheltered.Add(x?.Clone()); }
            return c;
        }
    }

    /// <summary>Đồng hồ hazard: chỉ tích khi World foreground online (ADR 0003), cooldown 24 h sau khi đóng theo từng thế giới.</summary>
    public sealed class HazardState
    {
        public long ForegroundMs { get; set; }
        /// <summary>Số lần kiểm hazard đã xử lý (chỉ số seed; không xử lý lại một mốc).</summary>
        public long ChecksDone { get; set; }
        /// <summary>zoneId → thời điểm hết cooldown (ms). Thứ tự theo WorldCatalog.ZoneIds.</summary>
        public List<KeyValuePair<string, long>> CooldownUntilMs { get; set; } = new List<KeyValuePair<string, long>>();

        public long CooldownOf(string zoneId)
        {
            foreach (var kv in CooldownUntilMs) if (kv.Key == zoneId) return kv.Value;
            return 0;
        }

        public void SetCooldown(string zoneId, long untilMs)
        {
            CooldownUntilMs.RemoveAll(kv => kv.Key == zoneId);
            CooldownUntilMs.Add(new KeyValuePair<string, long>(zoneId, untilMs));
            CooldownUntilMs.Sort((a, b) => Catalog.WorldCatalog.IndexOfZone(a.Key).CompareTo(Catalog.WorldCatalog.IndexOfZone(b.Key)));
        }

        public HazardState Clone()
        {
            var c = (HazardState)MemberwiseClone();
            c.CooldownUntilMs = new List<KeyValuePair<string, long>>(CooldownUntilMs);
            return c;
        }
    }
}
