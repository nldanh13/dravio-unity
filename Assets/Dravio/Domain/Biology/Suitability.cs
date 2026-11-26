using System;
using System.Collections.Generic;
using Dravio.Domain.Catalog;

namespace Dravio.Domain.Biology
{
    public enum SuitabilityReason
    {
        [WireName("ok")] Ok,
        [WireName("incompatible")] Incompatible,
        [WireName("invalid-env")] InvalidEnv,
        [WireName("low-suitability")] LowSuitability,
    }

    public enum SuitabilityTier
    {
        [WireName("good")] Good,
        [WireName("needs-improvement")] NeedsImprovement,
        [WireName("unsuitable")] Unsuitable,
    }

    public readonly struct HabitatResult
    {
        public HabitatResult(bool allowed, int score, SuitabilityReason reason) { Allowed = allowed; Score = score; Reason = reason; }
        public bool Allowed { get; }
        public int Score { get; }
        public SuitabilityReason Reason { get; }
        public override string ToString() => Allowed + "/" + Score + "/" + Reason.ToWire();
    }

    /// <summary>
    /// Suitability thuần (GDD 3.1 trang 5; src/engine/world/habitat.ts): hard gates rồi
    /// clamp(100 − Σ ceil(max(0, comfortMin − actual)), 0, 100) trên các chỉ số liên quan.
    /// ≥ 80 phù hợp, 50–79 cho bố trí kèm cảnh báo, &lt; 50 không cho bố trí lâu dài.
    /// </summary>
    public static class Suitability
    {
        public const int Allowed = 50;
        public const int Good = 80;

        private static readonly QualityKey[] AllKeys = { QualityKey.AirQuality, QualityKey.WaterQuality, QualityKey.Vegetation, QualityKey.FoodAvailability };
        private static readonly QualityKey[] OceanKeys = { QualityKey.WaterQuality, QualityKey.Vegetation, QualityKey.FoodAvailability };
        private static readonly QualityKey[] LandKeys = { QualityKey.AirQuality, QualityKey.Vegetation, QualityKey.FoodAvailability };

        /// <summary>
        /// Kiểm loài <paramref name="speciesId"/> tại microhabitat <paramref name="habitatId"/> với môi trường <paramref name="env"/>.
        /// ID loài lạ ném <see cref="Core.DomainInvariantException"/> (giống getBiology của TS).
        /// </summary>
        public static HabitatResult Evaluate(string speciesId, string habitatId, HabitatProfile env)
        {
            SpeciesBiology b = SpeciesCatalog.GetBiology(speciesId);
            if (!HabitatCatalog.TryGet(habitatId, out HabitatProfile h)) return new HabitatResult(false, 0, SuitabilityReason.InvalidEnv);
            foreach (QualityKey k in AllKeys)
            {
                double v = env.Quality[k];
                if (double.IsNaN(v) || double.IsInfinity(v) || v < 0 || v > 100) return new HabitatResult(false, 0, SuitabilityReason.InvalidEnv);
            }
            // Microhabitat là cấu hình vật lý đã duyệt, không phải thêm sức chứa zone.
            bool compatible = Contains(b.HabitatRules, habitatId) && env.ZoneId == h.ZoneId && env.Realm == h.Realm &&
                Contains(b.ThermalBands, env.TemperatureBand) && env.HumidityBand == h.HumidityBand &&
                env.SalinityBand == h.SalinityBand && Contains(b.SalinityBands, env.SalinityBand) &&
                env.DepthBand == h.DepthBand && Contains(b.DepthBands, env.DepthBand) &&
                env.AltitudeBand == h.AltitudeBand && Contains(b.AltitudeBands, env.AltitudeBand) &&
                (env.Realm == Realm.Ocean
                    ? b.Respiration != Respiration.Lung && Contains(b.Locomotion, Locomotion.Swim)
                    : b.Respiration != Respiration.Gill && Contains(b.Locomotion, Locomotion.Walk)) &&
                (env.Realm != Realm.Sky || (Contains(b.Locomotion, Locomotion.Fly) && env.Perch));
            if (!compatible) return new HabitatResult(false, 0, SuitabilityReason.Incompatible);
            // Bỏ qua chỉ số không liên quan; mỗi điểm thiếu trừ 1.
            QualityKey[] relevant = env.Realm == Realm.Ocean ? OceanKeys
                : habitatId == "origin_pondbank" && Contains(b.Locomotion, Locomotion.Swim) ? AllKeys : LandKeys;
            double deficit = 0;
            foreach (QualityKey k in relevant) deficit += Math.Ceiling(Math.Max(0, b.ComfortMin[k] - env.Quality[k]));
            int score = (int)Math.Max(0, Math.Min(100, 100 - deficit));
            bool allowed = score >= Allowed;
            return new HabitatResult(allowed, score, allowed ? SuitabilityReason.Ok : SuitabilityReason.LowSuitability);
        }

        /// <summary>speciesSuitability: theo hồ sơ môi trường canonical của microhabitat.</summary>
        public static HabitatResult ForSpecies(string speciesId, string habitatId)
        {
            if (!HabitatCatalog.TryGet(habitatId, out HabitatProfile h))
            {
                SpeciesCatalog.GetBiology(speciesId);
                return new HabitatResult(false, 0, SuitabilityReason.InvalidEnv);
            }
            return Evaluate(speciesId, habitatId, h);
        }

        public static SuitabilityTier TierOf(HabitatResult r)
        {
            if (!r.Allowed) return SuitabilityTier.Unsuitable;
            return r.Score >= Good ? SuitabilityTier.Good : SuitabilityTier.NeedsImprovement;
        }

        private static bool Contains<T>(IReadOnlyList<T> list, T value)
        {
            var cmp = EqualityComparer<T>.Default;
            foreach (T x in list) if (cmp.Equals(x, value)) return true;
            return false;
        }
    }
}
