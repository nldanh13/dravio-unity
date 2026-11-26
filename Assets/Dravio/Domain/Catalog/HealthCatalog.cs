using System;
using System.Collections.Generic;

namespace Dravio.Domain.Catalog
{
    /// <summary>Kế hoạch chăm sóc giả tưởng (GDD tr. 10). Không phải chỉ dẫn thú y thật; mọi khóa i18n có nhãn giả tưởng.</summary>
    public sealed class CarePlanDef
    {
        internal CarePlanDef(string id, InjurySite site, bool suitable, string explanationKey)
        {
            Id = id; Site = site; Suitable = suitable; ExplanationKey = explanationKey;
        }
        public string Id { get; }
        public InjurySite Site { get; }
        /// <summary>true = kế hoạch phù hợp (được tăng tốc); false = lựa chọn không phù hợp: chỉ hiện giải thích, không áp dụng, không gây tổn thương mới.</summary>
        public bool Suitable { get; }
        public string ExplanationKey { get; }
    }

    /// <summary>Hồ sơ vị trí tổn thương (GDD tr. 10, bảng "Tình huống P0").</summary>
    public sealed class InjurySiteDef
    {
        internal InjurySiteDef(InjurySite site, string[] symptoms) { Site = site; Symptoms = Array.AsReadOnly(symptoms); }
        public InjurySite Site { get; }
        public IReadOnlyList<string> Symptoms { get; }
    }

    /// <summary>
    /// Luật sức khỏe / chăm sóc / thiên tai đã có nguồn (U2.6). Mỗi hằng số ghi nguồn:
    /// [GDD] GDD 3.1; [ADR0003] docs/adr/0003 của TS; [ADR0201] Unity U2.8; [G0] G0 Decision Pack (SCENARIOS / PACK_README, "giả thuyết cân bằng G3");
    /// [ADR0202] quyết định triển khai của U2.6, chờ chủ dự án duyệt. Luật GDD chưa xác định nằm trong <see cref="DisasterRules"/> và bị tắt ở bản chạy thật.
    /// </summary>
    public static class HealthCatalog
    {
        // ---- Chỉ số ----
        public const int VitalMin = 0;
        public const int VitalMax = 100;
        /// <summary>[ADR0202 D1] Giá trị nền khi chưa có tổn thương = tối đa thang 0–100. Cần chủ dự án xác nhận.</summary>
        public const int VitalBaseline = 100;

        // ---- Ngưỡng điều kiện ----
        /// <summary>[ADR0003] Lai: health ≥ 80, mobility ≥ 80, comfort ≥ 60, không có HealthCase mở.</summary>
        public const int BreedMinHealth = 80, BreedMinMobility = 80, BreedMinComfort = 60;
        /// <summary>[ADR0201 mục 4] Thám hiểm: health ≥ 80, mobility ≥ 80, comfort ≥ 60, không có ca chăm sóc mở.</summary>
        public const int ExpeditionMinHealth = 80, ExpeditionMinMobility = 80, ExpeditionMinComfort = 60;

        // ---- Trạm cứu hộ / chăm sóc ----
        /// <summary>[GDD tr. 11] Trạm cứu hộ P0 có 2 chỗ; hàng chờ không giới hạn ca đang mở.</summary>
        public const int StationSlots = 2;
        /// <summary>[G0 S02] Thời lượng mặc định: nhẹ 20 phút (GDD 15–30), trung bình 80 phút (GDD 60–120).</summary>
        public const long MildDurationMs = 20L * 60 * 1000, ModerateDurationMs = 80L * 60 * 1000;
        /// <summary>[GDD tr. 10] Phạm vi thử nghiệm: nhẹ 15–30 phút, trung bình 60–120 phút (validator).</summary>
        public const long MildMinMs = 15L * 60 * 1000, MildMaxMs = 30L * 60 * 1000, ModerateMinMs = 60L * 60 * 1000, ModerateMaxMs = 120L * 60 * 1000;
        /// <summary>[GDD tr. 10 "tối đa 25 %", G0 S02 "rút 25 %"] Kế hoạch phù hợp còn 3/4 thời lượng → 15 / 60 phút.</summary>
        public const int PlanSpeedupNumerator = 3, PlanSpeedupDenominator = 4;
        /// <summary>[G0 S02] Tỷ lệ phase 10/50/30/10 % (ổn định / lành / phục hồi / tái hòa nhập); mốc chia ceil, phase cuối lấy phần còn lại.</summary>
        public static readonly IReadOnlyList<int> PhasePercent = Array.AsReadOnly(new[] { 10, 50, 30, 10 });
        /// <summary>[G0 S02] Hoàn tất phase return: mobility phục hồi tới 90, health ≥ 90, comfort ≥ 80 (không bao giờ giảm chỉ số đang cao hơn).</summary>
        public const int RecoveredHealth = 90, RecoveredMobility = 90, RecoveredComfort = 80;

        public static long BaseDurationMs(CaseSeverity s) => s == CaseSeverity.Mild ? MildDurationMs : ModerateDurationMs;

        public static long DurationMs(CaseSeverity s, bool suitablePlan) =>
            suitablePlan ? BaseDurationMs(s) * PlanSpeedupNumerator / PlanSpeedupDenominator : BaseDurationMs(s);

        public static bool DurationInRange(CaseSeverity s, long ms) =>
            s == CaseSeverity.Mild ? ms >= MildMinMs && ms <= MildMaxMs : ms >= ModerateMinMs && ms <= ModerateMaxMs;

        // ---- Thiên tai: phần GDD / ADR0003 / G0 đã nêu ----
        /// <summary>[GDD tr. 9] Dự báo 10 phút chơi thực (tích khi World foreground, ADR0003).</summary>
        public const long ForecastMs = 10L * 60 * 1000;
        /// <summary>[GDD tr. 9] Cooldown tối thiểu 24 giờ sau khi đóng.</summary>
        public const long CooldownMs = 24L * 60 * 60 * 1000;
        /// <summary>[GDD tr. 9] Tối đa 2 ca thương tích nhẹ/trung bình mỗi sự kiện.</summary>
        public const int MaxCasesPerEvent = 2;
        /// <summary>[G0 PACK_README] hazardSeverity đề xuất: nhẹ 0,30; trung bình 0,60.</summary>
        public static double HazardSeverityCoefficient(CaseSeverity s) => s == CaseSeverity.Mild ? 0.30 : 0.60;
        /// <summary>[G0 PACK_README] protection: nơi trú đầy đủ = 1; không trú = 0. ("Dự phòng một phần 0,5" chưa có điều kiện → không dùng.)</summary>
        public const double ShelterProtection = 1.0;

        /// <summary>[GDD tr. 9] Tổn thương có thể gặp theo hazard (chỉ các vị trí có trong bảng P0 của trang 10).</summary>
        public static IReadOnlyList<InjurySite> SitesFor(HazardId h)
        {
            switch (h)
            {
                case HazardId.Wildfire: return WildfireSites;     // Da/vảy, kích ứng hô hấp
                case HazardId.StrongCurrent: return CurrentSites; // Vây (mệt, va chạm: chưa có vị trí riêng)
                default: return StormSites;                       // Cánh, chi (hoảng sợ: không phải vị trí)
            }
        }
        private static readonly IReadOnlyList<InjurySite> WildfireSites = Array.AsReadOnly(new[] { InjurySite.SkinScale, InjurySite.Respiratory });
        private static readonly IReadOnlyList<InjurySite> CurrentSites = Array.AsReadOnly(new[] { InjurySite.FinGill });
        private static readonly IReadOnlyList<InjurySite> StormSites = Array.AsReadOnly(new[] { InjurySite.LimbWing });

        /// <summary>[GDD tr. 10; G0 S02] Triệu chứng giả tưởng theo vị trí (khóa i18n careSymptom.&lt;id&gt;).</summary>
        public static readonly IReadOnlyList<InjurySiteDef> Sites = Array.AsReadOnly(new[]
        {
            new InjurySiteDef(InjurySite.SkinScale, new[] { "scale_scrape", "fantasy_burn" }),
            new InjurySiteDef(InjurySite.LimbWing, new[] { "limited_movement", "fatigue" }),
            new InjurySiteDef(InjurySite.Respiratory, new[] { "fatigue", "smoke_adaptation" }),
            new InjurySiteDef(InjurySite.FinGill, new[] { "reduced_swimming" }),
        });

        public static InjurySiteDef Site(InjurySite s)
        {
            foreach (InjurySiteDef d in Sites) if (d.Site == s) return d;
            throw new Core.DomainInvariantException("unknown injury site " + s);
        }

        /// <summary>
        /// [GDD tr. 10 cột "Biểu hiện / kế hoạch game"; G0 S02] Một kế hoạch phù hợp cho mỗi vị trí; G0 S02 nêu "tập bay sớm" là lựa chọn không phù hợp cho cánh.
        /// Khóa i18n: carePlan.&lt;id&gt; và carePlanWhy.&lt;id&gt;.
        /// </summary>
        public static readonly IReadOnlyList<CarePlanDef> CarePlans = Array.AsReadOnly(new[]
        {
            new CarePlanDef("protect_wound_shelter", InjurySite.SkinScale, true, "carePlanWhy.protect_wound_shelter"),
            new CarePlanDef("rest_protect_gradual", InjurySite.LimbWing, true, "carePlanWhy.rest_protect_gradual"),
            new CarePlanDef("early_flight_training", InjurySite.LimbWing, false, "carePlanWhy.early_flight_training"),
            new CarePlanDef("clean_air_monitor", InjurySite.Respiratory, true, "carePlanWhy.clean_air_monitor"),
            new CarePlanDef("matched_pool_monitor", InjurySite.FinGill, true, "carePlanWhy.matched_pool_monitor"),
        });

        public static bool TryGetPlan(string id, out CarePlanDef plan)
        {
            foreach (CarePlanDef p in CarePlans) if (p.Id == id) { plan = p; return true; }
            plan = null;
            return false;
        }

        public static List<CarePlanDef> PlansFor(InjurySite site)
        {
            var list = new List<CarePlanDef>();
            foreach (CarePlanDef p in CarePlans) if (p.Site == site) list.Add(p);
            return list;
        }
    }

    /// <summary>Cách chọn mức của một sự kiện ở chế độ Ecosystem (Cozy luôn nhẹ theo GDD tr. 4).</summary>
    public enum EventSeverityPolicy
    {
        /// <summary>GDD chưa xác định.</summary>
        Undefined,
        /// <summary>[Đề xuất ADR0202 P2] Chọn đều nhẹ / trung bình bằng RNG của sự kiện.</summary>
        Uniform,
    }

    /// <summary>Giảm chỉ số khi bị thương (đề xuất ADR0202 P4). Số âm hoặc 0; chỉ số bị kẹp trong 0–100.</summary>
    public sealed class VitalImpact
    {
        public VitalImpact(int health, int mobility, int comfort) { Health = health; Mobility = mobility; Comfort = comfort; }
        public int Health { get; }
        public int Mobility { get; }
        public int Comfort { get; }
    }

    /// <summary>
    /// Luật thiên tai mà GDD 3.1 / ADR chưa chốt đủ để chạy thật (GDD tr. 24: "Tần số thiên tai … thời gian hồi phục … cần thử nghiệm").
    /// <see cref="Production"/> TẮT thiên tai tự nhiên cho tới khi chủ dự án duyệt ADR 0202; test và bản cân bằng dùng <see cref="ProposedForReview"/>
    /// hoặc tự dựng bộ luật. Không có lệnh nào cho người chơi tự tạo thiên tai (GDD tr. 9).
    /// </summary>
    public sealed class DisasterRules
    {
        public DisasterRules(bool enabled, long checkIntervalForegroundMs, double chance, IReadOnlyList<KeyValuePair<string, HazardId>> zoneHazards,
            EventSeverityPolicy severityPolicy, Func<InjurySite, CaseSeverity, VitalImpact> impact)
        {
            if (checkIntervalForegroundMs <= 0) throw new ArgumentOutOfRangeException(nameof(checkIntervalForegroundMs));
            if (double.IsNaN(chance) || chance < 0 || chance > 1) throw new ArgumentOutOfRangeException(nameof(chance));
            Enabled = enabled; CheckIntervalForegroundMs = checkIntervalForegroundMs; Chance = chance;
            ZoneHazards = zoneHazards ?? Array.Empty<KeyValuePair<string, HazardId>>();
            SeverityPolicy = severityPolicy; Impact = impact;
        }

        /// <summary>false = không kiểm hazard, không tích dự báo (ca và lệnh chăm sóc vẫn chạy cho dữ liệu đã có).</summary>
        public bool Enabled { get; }
        /// <summary>[ADR0003 — giả thuyết G3] Kiểm hazard mỗi 60 phút World foreground.</summary>
        public long CheckIntervalForegroundMs { get; }
        /// <summary>[ADR0003 — giả thuyết G3] 20 % mỗi lần kiểm nếu thế giới không có sự kiện mở và đã qua cooldown.</summary>
        public double Chance { get; }
        /// <summary>[Đề xuất ADR0202 P1, suy từ bảng GDD tr. 9] Hazard của từng thế giới.</summary>
        public IReadOnlyList<KeyValuePair<string, HazardId>> ZoneHazards { get; }
        public EventSeverityPolicy SeverityPolicy { get; }
        /// <summary>[Đề xuất ADR0202 P4] null = không đổi chỉ số khi bị thương (ca vẫn chặn lai / thám hiểm / bán).</summary>
        public Func<InjurySite, CaseSeverity, VitalImpact> Impact { get; }

        public bool TryHazardOf(string zoneId, out HazardId hazard)
        {
            foreach (var kv in ZoneHazards) if (kv.Key == zoneId) { hazard = kv.Value; return true; }
            hazard = default;
            return false;
        }

        /// <summary>Các mục còn chờ duyệt (hiện trong truy vấn để UI / QA thấy lý do thiên tai đang tắt).</summary>
        public IReadOnlyList<string> PendingDecisions => Pending;

        private static readonly IReadOnlyList<string> Pending = Array.AsReadOnly(new[]
        {
            "P1 zone-hazard-mapping", "P2 ecosystem-event-severity", "P3 injury-site-choice", "P4 vital-impact-per-injury",
            "P5 phase-durations-preparation-active-rescue", "P6 tutorial-rescue-fixture-and-reward", "P7 zone-environment-damage", "P8 environment-comfort-formula",
        });

        private static readonly IReadOnlyList<KeyValuePair<string, HazardId>> ProposedZoneHazards = Array.AsReadOnly(new[]
        {
            new KeyValuePair<string, HazardId>("origin", HazardId.Wildfire),
            new KeyValuePair<string, HazardId>("ocean", HazardId.StrongCurrent),
            new KeyValuePair<string, HazardId>("sky", HazardId.Thunderstorm),
        });

        /// <summary>[Đề xuất ADR0202 P4] Vị trí chính giảm 20 (nhẹ) / 40 (trung bình); health giảm một nửa mức đó; comfort giảm 10 / 20.</summary>
        public static VitalImpact ProposedImpact(InjurySite site, CaseSeverity sev)
        {
            int k = sev == CaseSeverity.Mild ? 1 : 2;
            bool mobilitySite = site == InjurySite.LimbWing || site == InjurySite.FinGill;
            return new VitalImpact(health: -10 * k, mobility: mobilitySite ? -20 * k : 0, comfort: -10 * k);
        }

        private const long Hour = 60L * 60 * 1000;

        /// <summary>Bản chạy thật: thiên tai tự nhiên TẮT (chờ ADR 0202). Giá trị ADR0003 vẫn ghi để truy vấn hiển thị.</summary>
        public static readonly DisasterRules Production = new DisasterRules(false, Hour, 0.20, ProposedZoneHazards, EventSeverityPolicy.Undefined, null);

        /// <summary>Bộ luật đề xuất trong ADR 0202 (chưa duyệt) — dùng cho test và bản cân bằng.</summary>
        public static readonly DisasterRules ProposedForReview = new DisasterRules(true, Hour, 0.20, ProposedZoneHazards, EventSeverityPolicy.Uniform, ProposedImpact);
    }
}
