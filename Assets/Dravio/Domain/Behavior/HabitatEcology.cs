using System;
using System.Collections.Generic;
using Dravio.Domain.Biology;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;

namespace Dravio.Domain.Behavior
{
    /// <summary>Nhóm yếu tố sinh cảnh.</summary>
    public enum EcologyFactorKind
    {
        /// <summary>Điều kiện cứng: sai một cái là Incompatible (giống Suitability.Evaluate).</summary>
        [WireName("gate")] Gate,
        /// <summary>Chỉ số chất lượng: mỗi điểm thiếu so với comfortMin trừ 1 điểm suitability.</summary>
        [WireName("quality")] Quality,
        /// <summary>Thông tin thêm, không vào điểm (nơi trú).</summary>
        [WireName("info")] Info,
    }

    /// <summary>Một dòng giải thích: yếu tố, giá trị hiện có, yêu cầu của loài, đạt hay không, và khóa i18n giải thích.</summary>
    public sealed class EcologyFactor
    {
        /// <summary>temperature, humidity, salinity, depth, altitude, nativeHabitat, respiration, locomotion, perch, airQuality, waterQuality, vegetation, foodAvailability, shelter.</summary>
        public string Key { get; internal set; }
        public EcologyFactorKind Kind { get; internal set; }
        public bool Ok { get; internal set; }
        /// <summary>false = chỉ số chất lượng không tính cho realm/microhabitat này (giống bộ khóa của Suitability).</summary>
        public bool Relevant { get; internal set; } = true;
        /// <summary>Giá trị hiện có (wire của band, hoặc số 0–100 dạng chuỗi).</summary>
        public string Actual { get; internal set; }
        /// <summary>Yêu cầu của loài (danh sách wire nối bằng "|", hoặc "≥N").</summary>
        public string Required { get; internal set; }
        /// <summary>Điểm bị trừ (chỉ Quality).</summary>
        public int Deficit { get; internal set; }
        /// <summary><c>ecology.&lt;key&gt;.&lt;ok|too-cold|too-hot|...&gt;</c>.</summary>
        public string ExplanationKey { get; internal set; }
    }

    /// <summary>Giải thích đầy đủ độ phù hợp loài × microhabitat. Điểm và kết luận lấy trực tiếp từ <see cref="Suitability.Evaluate"/>.</summary>
    public sealed class HabitatEcologyReport
    {
        public string SpeciesId { get; internal set; }
        public string HabitatId { get; internal set; }
        public bool Allowed { get; internal set; }
        public int Score { get; internal set; }
        public SuitabilityReason Reason { get; internal set; }
        public SuitabilityTier Tier { get; internal set; }
        public IReadOnlyList<EcologyFactor> Factors { get; internal set; }
        /// <summary>Khóa giải thích quan trọng nhất (gate sai đầu tiên, hoặc chất lượng thiếu nhiều nhất); <c>ecology.ok</c> nếu không có vấn đề.</summary>
        public string PrimaryIssueKey { get; internal set; }
    }

    /// <summary>
    /// Habitat Ecology (U2.9 Workstream C). Không phải engine mới: điểm lấy từ Suitability (GDD tr. 5); lớp này chỉ tách từng yếu tố để UI giải thích.
    /// </summary>
    public static class HabitatEcology
    {
        public static HabitatEcologyReport Explain(string speciesId, string habitatId) =>
            HabitatCatalog.TryGet(habitatId, out HabitatProfile h) ? Explain(speciesId, habitatId, h) : Explain(speciesId, habitatId, null);

        /// <summary>Giải thích với môi trường giả định <paramref name="env"/> (vd chất lượng giảm sau sự kiện).</summary>
        public static HabitatEcologyReport Explain(string speciesId, string habitatId, HabitatProfile env)
        {
            SpeciesBiology b = SpeciesCatalog.GetBiology(speciesId);
            var factors = new List<EcologyFactor>();
            HabitatResult r;
            if (env == null || !HabitatCatalog.TryGet(habitatId, out HabitatProfile canon))
            {
                r = new HabitatResult(false, 0, SuitabilityReason.InvalidEnv);
                return Report(speciesId, habitatId, r, factors);
            }
            r = Suitability.Evaluate(speciesId, habitatId, env);

            factors.Add(Gate("nativeHabitat", Has(b.HabitatRules, habitatId), habitatId, string.Join("|", b.HabitatRules), "not-native"));
            bool tempOk = Has(b.ThermalBands, env.TemperatureBand);
            factors.Add(Gate("temperature", tempOk, env.TemperatureBand.ToWire(), Join(b.ThermalBands),
                tempOk ? null : (int)env.TemperatureBand < (int)Min(b.ThermalBands) ? "too-cold" : "too-hot"));
            factors.Add(Gate("humidity", env.HumidityBand == canon.HumidityBand, env.HumidityBand.ToWire(), canon.HumidityBand.ToWire(), "altered"));
            bool salOk = env.SalinityBand == canon.SalinityBand && Has(b.SalinityBands, env.SalinityBand);
            factors.Add(Gate("salinity", salOk, env.SalinityBand.ToWire(), Join(b.SalinityBands),
                Has(b.SalinityBands, env.SalinityBand) ? "altered" : "wrong-salinity"));
            bool depthOk = env.DepthBand == canon.DepthBand && Has(b.DepthBands, env.DepthBand);
            factors.Add(Gate("depth", depthOk, env.DepthBand.ToWire(), Join(b.DepthBands), Has(b.DepthBands, env.DepthBand) ? "altered" : "wrong-depth"));
            bool altOk = env.AltitudeBand == canon.AltitudeBand && Has(b.AltitudeBands, env.AltitudeBand);
            factors.Add(Gate("altitude", altOk, env.AltitudeBand.ToWire(), Join(b.AltitudeBands), Has(b.AltitudeBands, env.AltitudeBand) ? "altered" : "wrong-altitude"));
            bool zoneOk = env.ZoneId == canon.ZoneId && env.Realm == canon.Realm;
            factors.Add(Gate("realm", zoneOk, env.Realm.ToWire(), canon.Realm.ToWire(), "altered"));
            bool water = env.Realm == Realm.Ocean;
            bool respOk = water ? b.Respiration != Respiration.Lung : b.Respiration != Respiration.Gill;
            factors.Add(Gate("respiration", respOk, b.Respiration.ToWire(), water ? "gill|dual" : "lung|dual", water ? "needs-air" : "needs-water"));
            Locomotion need = water ? Locomotion.Swim : Locomotion.Walk;
            factors.Add(Gate("locomotion", Has(b.Locomotion, need), Join(b.Locomotion), need.ToWire(), water ? "cannot-swim" : "cannot-walk"));
            if (env.Realm == Realm.Sky)
            {
                bool flyOk = Has(b.Locomotion, Locomotion.Fly) && env.Perch;
                factors.Add(Gate("perch", flyOk, env.Perch ? "perch" : "none", "fly+perch", Has(b.Locomotion, Locomotion.Fly) ? "no-perch" : "cannot-fly"));
            }

            // Chất lượng: cùng bộ khóa liên quan với Suitability.
            QualityKey[] relevant = env.Realm == Realm.Ocean
                ? new[] { QualityKey.WaterQuality, QualityKey.Vegetation, QualityKey.FoodAvailability }
                : habitatId == "origin_pondbank" && Has(b.Locomotion, Locomotion.Swim)
                    ? new[] { QualityKey.AirQuality, QualityKey.WaterQuality, QualityKey.Vegetation, QualityKey.FoodAvailability }
                    : new[] { QualityKey.AirQuality, QualityKey.Vegetation, QualityKey.FoodAvailability };
            foreach (QualityKey k in new[] { QualityKey.AirQuality, QualityKey.WaterQuality, QualityKey.Vegetation, QualityKey.FoodAvailability })
            {
                bool rel = Array.IndexOf(relevant, k) >= 0;
                double actual = env.Quality[k], min = b.ComfortMin[k];
                int deficit = rel ? (int)Math.Ceiling(Math.Max(0, min - actual)) : 0;
                factors.Add(new EcologyFactor
                {
                    Key = k.ToWire(), Kind = EcologyFactorKind.Quality, Relevant = rel, Ok = deficit == 0, Deficit = deficit,
                    Actual = Num(actual), Required = "≥" + Num(min),
                    ExplanationKey = "ecology." + k.ToWire() + "." + (!rel ? "not-relevant" : deficit == 0 ? "ok" : "low"),
                });
            }
            // Nơi trú: mỗi thế giới có nơi trú không giới hạn (ADR 0202 D3); chỗ đậu là nơi nghỉ của loài bay.
            factors.Add(new EcologyFactor
            {
                Key = "shelter", Kind = EcologyFactorKind.Info, Ok = true, Actual = env.Perch ? "zone-shelter+perch" : "zone-shelter", Required = "zone-shelter",
                ExplanationKey = "ecology.shelter." + (env.Perch ? "perch" : "zone"),
            });
            return Report(speciesId, habitatId, r, factors);
        }

        private static HabitatEcologyReport Report(string speciesId, string habitatId, HabitatResult r, List<EcologyFactor> factors)
        {
            string primary = r.Reason == SuitabilityReason.InvalidEnv ? "ecology.invalid-env" : null;
            if (primary == null) foreach (EcologyFactor f in factors) if (f.Kind == EcologyFactorKind.Gate && !f.Ok) { primary = f.ExplanationKey; break; }
            if (primary == null)
            {
                EcologyFactor worst = null;
                foreach (EcologyFactor f in factors) if (f.Kind == EcologyFactorKind.Quality && f.Deficit > 0 && (worst == null || f.Deficit > worst.Deficit)) worst = f;
                primary = worst?.ExplanationKey ?? "ecology.ok";
            }
            return new HabitatEcologyReport
            {
                SpeciesId = speciesId, HabitatId = habitatId, Allowed = r.Allowed, Score = r.Score, Reason = r.Reason, Tier = Suitability.TierOf(r),
                Factors = factors, PrimaryIssueKey = primary,
            };
        }

        private static EcologyFactor Gate(string key, bool ok, string actual, string required, string failSuffix) => new EcologyFactor
        {
            Key = key, Kind = EcologyFactorKind.Gate, Ok = ok, Actual = actual, Required = required,
            ExplanationKey = "ecology." + key + "." + (ok ? "ok" : failSuffix),
        };

        private static string Num(double v) => v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        private static bool Has<T>(IReadOnlyList<T> l, T v) => BehaviorCatalog.Has(l, v);
        private static string Join<T>(IReadOnlyList<T> l) where T : struct, Enum
        {
            var parts = new string[l.Count];
            for (int i = 0; i < l.Count; i++) parts[i] = l[i].ToWire();
            return string.Join("|", parts);
        }
        private static TemperatureBand Min(IReadOnlyList<TemperatureBand> l)
        {
            TemperatureBand m = l[0];
            foreach (TemperatureBand t in l) if (t < m) m = t;
            return m;
        }
    }

    /// <summary>
    /// Đề xuất ADR 0203 C1 (trả lời ADR 0202 P8 "môi trường → comfort"). <b>TẮT ở production</b>: chưa có code production nào gọi
    /// <see cref="EcologyComfort.Apply"/>. Test và báo cáo cân bằng tiêm <see cref="ProposedForReview"/>.
    /// </summary>
    public sealed class EcologyComfortRules
    {
        public EcologyComfortRules(bool enabled, int cozyFloor, int ecosystemFloor, int favoriteHabitatBonus, long stepMs, bool offlineMayDecrease)
        {
            Enabled = enabled; CozyFloor = cozyFloor; EcosystemFloor = ecosystemFloor; FavoriteHabitatBonus = favoriteHabitatBonus;
            StepMs = stepMs; OfflineMayDecrease = offlineMayDecrease;
        }

        public bool Enabled { get; }
        /// <summary>Comfort do môi trường không bao giờ dưới mức này ở Cozy. 60 = không bao giờ tự chặn lai/thám hiểm (ngưỡng comfort ≥ 60).</summary>
        public int CozyFloor { get; }
        public int EcosystemFloor { get; }
        /// <summary>Cộng vào mục tiêu khi ở microhabitat ưa thích (cá tính); chỉ có thể giúp, không phạt.</summary>
        public int FavoriteHabitatBonus { get; }
        /// <summary>Comfort đổi 1 điểm mỗi mốc StepMs tuyệt đối (tách khoảng thời gian không đổi kết quả).</summary>
        public long StepMs { get; }
        /// <summary>false = offline chỉ được tăng comfort, không bao giờ giảm (không phạt khi offline).</summary>
        public bool OfflineMayDecrease { get; }

        public static readonly EcologyComfortRules Production = new EcologyComfortRules(false, 60, 40, 5, 6 * 60_000L, false);
        public static readonly EcologyComfortRules ProposedForReview = new EcologyComfortRules(true, 60, 40, 5, 6 * 60_000L, false);
    }

    public static class EcologyComfort
    {
        /// <summary>Mục tiêu comfort = clamp(max(sàn theo độ khó, suitability + thưởng nơi ưa thích), 0, 100).</summary>
        public static int Target(int suitabilityScore, bool favoriteHabitat, Difficulty difficulty, EcologyComfortRules rules)
        {
            int floor = difficulty == Difficulty.Cozy ? rules.CozyFloor : rules.EcosystemFloor;
            int t = suitabilityScore + (favoriteHabitat ? rules.FavoriteHabitatBonus : 0);
            return Math.Max(0, Math.Min(100, Math.Max(floor, t)));
        }

        /// <summary>Mục tiêu của một rồng; null nếu không ở microhabitat (kho, chăm sóc, trú, đang bận) — comfort giữ nguyên.</summary>
        public static int? TargetOf(GameState s, DragonState d, EcologyComfortRules rules)
        {
            if (d.Location.Kind != LocationKind.Zone || d.Activity != DragonActivity.Idle || s.OpenCaseOf(d.Uid) != null) return null;
            if (!HabitatCatalog.Contains(d.Location.HabitatId)) return null;
            HabitatResult r = Suitability.ForSpecies(d.SpeciesId, d.Location.HabitatId);
            bool fav = PersonalityProfiles.Of(d).FavoriteHabitatId == d.Location.HabitatId;
            return Target(r.Score, fav, s.Player.Settings.Difficulty, rules);
        }

        /// <summary>Số mốc StepMs tuyệt đối trong (fromMs, toMs].</summary>
        public static long Steps(long fromMs, long toMs, long stepMs)
        {
            if (toMs <= fromMs) return 0;
            return FloorDiv(toMs, stepMs) - FloorDiv(fromMs, stepMs);
        }

        /// <summary>Comfort mới sau khoảng (fromMs, toMs]: tiến về mục tiêu 1 điểm mỗi mốc; offline không giảm (mặc định đề xuất).</summary>
        public static int Step(int current, int target, long fromMs, long toMs, AdvanceMode mode, EcologyComfortRules rules)
        {
            long n = Steps(fromMs, toMs, rules.StepMs);
            if (n == 0 || current == target) return current;
            if (current < target) return (int)Math.Min(target, current + n);
            if (mode == AdvanceMode.Offline && !rules.OfflineMayDecrease) return current;
            return (int)Math.Max(target, current - n);
        }

        /// <summary>
        /// Áp đề xuất C1 lên state (chỉ dùng cho test / mô phỏng cân bằng). Không làm gì khi <c>rules.Enabled = false</c>.
        /// Chỉ đổi Vitals.Comfort, không đụng vàng, XP, bond hay health. Trả số rồng bị đổi.
        /// </summary>
        public static int Apply(GameState s, long fromMs, long toMs, AdvanceMode mode, EcologyComfortRules rules, List<DomainEvent> events)
        {
            if (rules == null || !rules.Enabled) return 0;
            int changed = 0;
            foreach (DragonState d in s.Dragons)
            {
                if (d.Vitals == null) continue;
                int? target = TargetOf(s, d, rules);
                if (target == null) continue;
                int next = Step(d.Vitals.Comfort, target.Value, fromMs, toMs, mode, rules);
                if (next == d.Vitals.Comfort) continue;
                Vitals before = d.Vitals.Clone();
                d.Vitals.Comfort = next;
                events?.Add(new VitalsChanged(d.Uid, before, d.Vitals.Clone()));
                changed++;
            }
            return changed;
        }

        private static long FloorDiv(long a, long b)
        {
            long q = a / b;
            if ((a % b != 0) && ((a < 0) != (b < 0))) q--;
            return q;
        }
    }
}
