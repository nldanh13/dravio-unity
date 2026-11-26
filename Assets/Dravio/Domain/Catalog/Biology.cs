using System;
using System.Collections.Generic;

namespace Dravio.Domain.Catalog
{
    /// <summary>Bốn chỉ số chất lượng môi trường 0–100 (QualityKey).</summary>
    public readonly struct QualityValues
    {
        public QualityValues(double airQuality, double waterQuality, double vegetation, double foodAvailability)
        {
            AirQuality = airQuality; WaterQuality = waterQuality; Vegetation = vegetation; FoodAvailability = foodAvailability;
        }

        public double AirQuality { get; }
        public double WaterQuality { get; }
        public double Vegetation { get; }
        public double FoodAvailability { get; }

        public double this[QualityKey key]
        {
            get
            {
                switch (key)
                {
                    case QualityKey.AirQuality: return AirQuality;
                    case QualityKey.WaterQuality: return WaterQuality;
                    case QualityKey.Vegetation: return Vegetation;
                    case QualityKey.FoodAvailability: return FoodAvailability;
                    default: throw new ArgumentOutOfRangeException(nameof(key));
                }
            }
        }

        public QualityValues With(QualityKey key, double value) => new QualityValues(
            key == QualityKey.AirQuality ? value : AirQuality,
            key == QualityKey.WaterQuality ? value : WaterQuality,
            key == QualityKey.Vegetation ? value : Vegetation,
            key == QualityKey.FoodAvailability ? value : FoodAvailability);
    }

    /// <summary>Sức chống chịu thiên tai theo % (resiliencePct). Chỉ là dữ liệu ở U2.1; thiên tai thuộc U5.</summary>
    public readonly struct HazardResilience
    {
        public HazardResilience(int wildfire, int strongCurrent, int thunderstorm)
        {
            Wildfire = wildfire; StrongCurrent = strongCurrent; Thunderstorm = thunderstorm;
        }

        public int Wildfire { get; }
        public int StrongCurrent { get; }
        public int Thunderstorm { get; }

        public int this[HazardId id] => id == HazardId.Wildfire ? Wildfire : id == HazardId.StrongCurrent ? StrongCurrent : Thunderstorm;
    }

    /// <summary>Bảng màu art bible (#RRGGBB). Dữ liệu tham chiếu cho art, domain không dùng để tính toán.</summary>
    public sealed class SpeciesPalette
    {
        public SpeciesPalette(string primary, string secondary, string belly, string accent)
        {
            Primary = primary; Secondary = secondary; Belly = belly; Accent = accent;
        }

        public string Primary { get; }
        public string Secondary { get; }
        public string Belly { get; }
        public string Accent { get; }
    }

    /// <summary>
    /// Môi trường của một microhabitat (G0HabitatEnv). Dùng cả cho hồ sơ canonical và môi trường
    /// giả định khi kiểm suitability (ví dụ chất lượng không khí giảm).
    /// </summary>
    public sealed class HabitatProfile
    {
        public HabitatProfile(string id, string zoneId, Realm realm, TemperatureBand temperatureBand, HumidityBand humidityBand,
            SalinityBand salinityBand, DepthBand depthBand, AltitudeBand altitudeBand, bool perch,
            double airQuality, double waterQuality, double vegetation, double foodAvailability)
            : this(id, zoneId, realm, temperatureBand, humidityBand, salinityBand, depthBand, altitudeBand, perch,
                new QualityValues(airQuality, waterQuality, vegetation, foodAvailability))
        {
        }

        public HabitatProfile(string id, string zoneId, Realm realm, TemperatureBand temperatureBand, HumidityBand humidityBand,
            SalinityBand salinityBand, DepthBand depthBand, AltitudeBand altitudeBand, bool perch, QualityValues quality)
        {
            Id = id; ZoneId = zoneId; Realm = realm; TemperatureBand = temperatureBand; HumidityBand = humidityBand;
            SalinityBand = salinityBand; DepthBand = depthBand; AltitudeBand = altitudeBand; Perch = perch; Quality = quality;
        }

        public string Id { get; }
        public string ZoneId { get; }
        public Realm Realm { get; }
        public TemperatureBand TemperatureBand { get; }
        public HumidityBand HumidityBand { get; }
        public SalinityBand SalinityBand { get; }
        public DepthBand DepthBand { get; }
        public AltitudeBand AltitudeBand { get; }
        public bool Perch { get; }
        public QualityValues Quality { get; }

        public HabitatProfile WithQuality(QualityValues quality) =>
            new HabitatProfile(Id, ZoneId, Realm, TemperatureBand, HumidityBand, SalinityBand, DepthBand, AltitudeBand, Perch, quality);

        public HabitatProfile WithZoneId(string zoneId) =>
            new HabitatProfile(Id, zoneId, Realm, TemperatureBand, HumidityBand, SalinityBand, DepthBand, AltitudeBand, Perch, Quality);
    }

    /// <summary>
    /// Anatomy giả tưởng canonical của loài (G0Anatomy; ADR 0003). Nguồn duy nhất: G0BiologyData (sinh từ g0Biology.ts).
    /// Không suy ra từ nguyên tố hay nhóm.
    /// </summary>
    public sealed class SpeciesBiology
    {
        public SpeciesBiology(string id, BreedingGroup group, Respiration respiration, Locomotion[] locomotion, Protection protection,
            ActivityCycle activity, DietId[] dietIds, TemperatureBand[] thermalBands, string[] habitatRules, SalinityBand[] salinityBands,
            DepthBand[] depthBands, AltitudeBand[] altitudeBands, QualityValues comfortMin, HazardResilience resiliencePct,
            SpeciesPalette palette, string silhouetteVi, bool draft)
        {
            Id = id; Group = group; Respiration = respiration; Locomotion = Array.AsReadOnly(locomotion); Protection = protection;
            Activity = activity; DietIds = Array.AsReadOnly(dietIds); ThermalBands = Array.AsReadOnly(thermalBands);
            HabitatRules = Array.AsReadOnly(habitatRules); SalinityBands = Array.AsReadOnly(salinityBands);
            DepthBands = Array.AsReadOnly(depthBands); AltitudeBands = Array.AsReadOnly(altitudeBands);
            ComfortMin = comfortMin; ResiliencePct = resiliencePct; Palette = palette; SilhouetteVi = silhouetteVi; Draft = draft;
        }

        public string Id { get; }
        public BreedingGroup Group { get; }
        public Respiration Respiration { get; }
        public IReadOnlyList<Locomotion> Locomotion { get; }
        public Protection Protection { get; }
        public ActivityCycle Activity { get; }
        public IReadOnlyList<DietId> DietIds { get; }
        public IReadOnlyList<TemperatureBand> ThermalBands { get; }
        /// <summary>ID microhabitat hợp lệ.</summary>
        public IReadOnlyList<string> HabitatRules { get; }
        public IReadOnlyList<SalinityBand> SalinityBands { get; }
        public IReadOnlyList<DepthBand> DepthBands { get; }
        public IReadOnlyList<AltitudeBand> AltitudeBands { get; }
        public QualityValues ComfortMin { get; }
        public HazardResilience ResiliencePct { get; }
        public SpeciesPalette Palette { get; }
        public string SilhouetteVi { get; }
        /// <summary>false: số liệu đã chốt ở G0.</summary>
        public bool Draft { get; }
    }
}
