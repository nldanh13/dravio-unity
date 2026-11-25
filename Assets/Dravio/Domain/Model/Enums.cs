namespace Dravio.Domain
{
    // Các enum đóng của domain. Mỗi giá trị ánh xạ 1-1 với chuỗi trong TypeScript gốc
    // (src/types/game.ts, src/types/biology.ts, src/types/state.ts) qua WireNames.
    // Không thêm giá trị mới: chuỗi wire là một phần của Dravio Save Schema.

    // ---- src/types/game.ts ----

    public enum ElementType { Fire, Water, Nature, Sky }

    public enum Rarity { Common, Rare, Epic, Legendary }

    public enum MutationType { Normal, Crystal, Golden, Prismatic }

    /// <summary>Giai đoạn vòng đời. Là giá trị dẫn xuất từ XP, không lưu trong save.</summary>
    public enum GrowthStage { Baby, Juvenile, Adult }

    public enum LanguageCode { Vi, En }

    public enum Realm { Land, Ocean, Sky }

    /// <summary>Nhóm sinh sản: T = đất, A = thủy-lưỡng cư, V = bay. Khác với Realm.</summary>
    public enum BreedingGroup
    {
        [WireName("T")] T,
        [WireName("A")] A,
        [WireName("V")] V,
    }

    public enum Personality { Playful, Calm, Curious, Shy }

    public enum BodyStyle { Round, Serpent, Winged, Horned }

    public enum LocationKind { Zone, Storage, Shelter, Care, Released }

    public enum DragonActivity { Idle, Breeding, Expedition, Care }

    public enum DragonOrigin { Pack, Breed, Rescue, Gift }

    public enum Ancestry { Known, Unknown }

    // ---- src/types/biology.ts ----

    public enum TemperatureBand { Cold, Cool, Mild, Warm, Hot }

    public enum HumidityBand { Dry, Normal, Humid, Submerged }

    public enum SalinityBand { None, Fresh, Brackish, Marine }

    public enum DepthBand { None, Shallow, Deep }

    public enum AltitudeBand { Ground, Low, High }

    public enum Respiration { Lung, Gill, Dual }

    public enum Locomotion { Walk, Swim, Fly, Burrow }

    public enum Protection { Scale, Armor, Fur }

    /// <summary>Chu kỳ hoạt động sinh học (G0Anatomy.activity: day/night/both).</summary>
    public enum ActivityCycle { Day, Night, Both }

    public enum QualityKey { AirQuality, WaterQuality, Vegetation, FoodAvailability }

    public enum HazardId
    {
        [WireName("wildfire")] Wildfire,
        [WireName("strong_current")] StrongCurrent,
        [WireName("thunderstorm")] Thunderstorm,
    }

    // ---- src/data/care.ts ----

    /// <summary>DIET_IDS = fruit, leaf, aquatic, aether.</summary>
    public enum DietId { Fruit, Leaf, Aquatic, Aether }

    // ---- src/types/state.ts ----

    public enum TimeMode { Auto, Day, Sunset, Night }

    public enum AppMode { World, Companion, Zen }

    public enum Difficulty { Cozy, Ecosystem }

    public enum TutorialStatus { Active, Skipped, Completed }

    /// <summary>Trạng thái trứng trong tổ (BreedingEgg.status).</summary>
    public enum EggStatus { Incubating, Ready }

    /// <summary>Trạng thái chuyến thám hiểm.</summary>
    public enum ExpeditionStatus { [WireName("in-progress")] InProgress, Ready }

    // ---- src/engine/core/commands.ts ----

    /// <summary>Chế độ quyết toán thời gian: online 100 %, offline 75 % và receipt.</summary>
    public enum AdvanceMode { Online, Offline }

    /// <summary>Nguồn tăng gắn bó trong event bondChanged.</summary>
    public enum BondSource { Feed, Pet }
}
