using System;
using System.Collections.Generic;
using Dravio.Domain.Catalog;

namespace Dravio.Domain.Behavior
{
    /// <summary>Lý do một hành vi không khả dụng cho loài (cấp loài) hoặc tại microhabitat (cấp nơi ở).</summary>
    public enum BehaviorBlock
    {
        [WireName("none")] None,
        /// <summary>Loài không có Locomotion tương ứng (Swim / Fly / Burrow).</summary>
        [WireName("no-locomotion")] NoLocomotion,
        /// <summary>Bask cần thở phổi (không phải mang) và chịu được dải Warm/Hot.</summary>
        [WireName("not-thermophile")] NotThermophile,
        /// <summary>Microhabitat không có nước để bơi.</summary>
        [WireName("no-water")] NoWater,
        /// <summary>Fly cần realm Sky hoặc chỗ đậu (perch).</summary>
        [WireName("no-perch")] NoPerch,
        /// <summary>Burrow cần nền đất (Land, không ngập).</summary>
        [WireName("no-soil")] NoSoil,
        /// <summary>Bask cần microhabitat trên cạn, không lạnh.</summary>
        [WireName("no-sun")] NoSun,
    }

    /// <summary>Định nghĩa tĩnh một hành vi (ADR 0203 B1).</summary>
    public sealed class BehaviorDef
    {
        internal BehaviorDef(DragonBehavior id, int baseWeight, bool ambient, string descriptionKey)
        {
            Id = id; BaseWeight = baseWeight; Ambient = ambient; DescriptionKey = descriptionKey;
        }

        public DragonBehavior Id { get; }
        /// <summary>Trọng số gốc khi chọn ngẫu nhiên (0 = không bao giờ được chọn ngẫu nhiên).</summary>
        public int BaseWeight { get; }
        /// <summary>false = chỉ xuất hiện do luật gameplay (SeekFood, Eat).</summary>
        public bool Ambient { get; }
        public string DescriptionKey { get; }
    }

    /// <summary>
    /// Catalog hành vi và luật khả năng theo sinh học G0. Chỉ đọc <see cref="SpeciesBiology"/> và <see cref="HabitatProfile"/>
    /// đã có; không thêm dữ liệu sinh học mới.
    /// </summary>
    public static class BehaviorCatalog
    {
        /// <summary>Độ dài một "nhịp" hành vi trình diễn. Mỗi UID lệch pha riêng để các rồng không đổi hành vi cùng lúc.</summary>
        public const long EpochMs = 60_000;
        /// <summary>Hiển thị Eat trong khoảng này sau khi rồng được cho ăn thật (LastFeedXpAtMs).</summary>
        public const long EatDisplayMs = 5_000;
        /// <summary>Vitals dưới ngưỡng này thì rồng nghỉ (Rest) thay vì hoạt động — chỉ trình diễn, không đổi vitals.</summary>
        public const int RestBelowHealth = 60;
        public const int RestBelowMobility = 60;
        /// <summary>Năng lượng dưới ngưỡng này thì ngủ. Năng lượng hiện không thay đổi trong production (ADR 0202 D1, 0203 C3).</summary>
        public const int SleepBelowEnergy = 30;
        /// <summary>Mức hoạt động theo pha ≤ ngưỡng này: ngủ theo nhịp sinh học.</summary>
        public const int CircadianSleepAtOrBelow = 10;
        /// <summary>Số rồng khác cùng microhabitat để tính là "đông" (cá tính nhút nhát ngại chơi / khám phá).</summary>
        public const int CrowdedAtOthers = 3;

        public static readonly IReadOnlyList<DragonBehavior> All = Array.AsReadOnly(new[]
        {
            DragonBehavior.Idle, DragonBehavior.Rest, DragonBehavior.Sleep, DragonBehavior.Explore, DragonBehavior.SeekFood, DragonBehavior.Eat,
            DragonBehavior.Play, DragonBehavior.Bask, DragonBehavior.Swim, DragonBehavior.Fly, DragonBehavior.Burrow,
        });

        private static readonly BehaviorDef[] Defs =
        {
            new BehaviorDef(DragonBehavior.Idle, 100, true, "behavior.idle"),
            new BehaviorDef(DragonBehavior.Rest, 70, true, "behavior.rest"),
            new BehaviorDef(DragonBehavior.Sleep, 0, true, "behavior.sleep"),
            new BehaviorDef(DragonBehavior.Explore, 80, true, "behavior.explore"),
            new BehaviorDef(DragonBehavior.SeekFood, 0, false, "behavior.seekFood"),
            new BehaviorDef(DragonBehavior.Eat, 0, false, "behavior.eat"),
            new BehaviorDef(DragonBehavior.Play, 60, true, "behavior.play"),
            new BehaviorDef(DragonBehavior.Bask, 90, true, "behavior.bask"),
            new BehaviorDef(DragonBehavior.Swim, 110, true, "behavior.swim"),
            new BehaviorDef(DragonBehavior.Fly, 80, true, "behavior.fly"),
            new BehaviorDef(DragonBehavior.Burrow, 50, true, "behavior.burrow"),
        };

        public static BehaviorDef Get(DragonBehavior b) => Defs[(int)b];

        /// <summary>Khả năng cấp loài (không phụ thuộc nơi ở). None = loài làm được.</summary>
        public static BehaviorBlock SpeciesBlock(SpeciesBiology b, DragonBehavior behavior)
        {
            switch (behavior)
            {
                case DragonBehavior.Swim: return Has(b.Locomotion, Locomotion.Swim) ? BehaviorBlock.None : BehaviorBlock.NoLocomotion;
                case DragonBehavior.Fly: return Has(b.Locomotion, Locomotion.Fly) ? BehaviorBlock.None : BehaviorBlock.NoLocomotion;
                case DragonBehavior.Burrow: return Has(b.Locomotion, Locomotion.Burrow) ? BehaviorBlock.None : BehaviorBlock.NoLocomotion;
                case DragonBehavior.Bask:
                    return b.Respiration != Respiration.Gill && (Has(b.ThermalBands, TemperatureBand.Warm) || Has(b.ThermalBands, TemperatureBand.Hot))
                        ? BehaviorBlock.None : BehaviorBlock.NotThermophile;
                default: return BehaviorBlock.None;
            }
        }

        /// <summary>Điều kiện cấp microhabitat (chỉ cho hành vi phụ thuộc môi trường). None = nơi này cho phép.</summary>
        public static BehaviorBlock HabitatBlock(HabitatProfile h, DragonBehavior behavior)
        {
            switch (behavior)
            {
                case DragonBehavior.Swim: return HasWater(h) ? BehaviorBlock.None : BehaviorBlock.NoWater;
                case DragonBehavior.Fly: return h.Realm == Realm.Sky || h.Perch ? BehaviorBlock.None : BehaviorBlock.NoPerch;
                case DragonBehavior.Burrow:
                    return h.Realm == Realm.Land && h.DepthBand == DepthBand.None && h.HumidityBand != HumidityBand.Submerged ? BehaviorBlock.None : BehaviorBlock.NoSoil;
                case DragonBehavior.Bask:
                    return h.Realm != Realm.Ocean && h.TemperatureBand != TemperatureBand.Cold && h.TemperatureBand != TemperatureBand.Cool ? BehaviorBlock.None : BehaviorBlock.NoSun;
                default: return BehaviorBlock.None;
            }
        }

        /// <summary>Nước bơi được: Ocean (ngập) hoặc bờ nước ngọt ẩm (origin_pondbank).</summary>
        public static bool HasWater(HabitatProfile h) =>
            h.Realm == Realm.Ocean || h.HumidityBand == HumidityBand.Submerged || (h.HumidityBand == HumidityBand.Humid && h.SalinityBand != SalinityBand.None);

        /// <summary>Rồng di chuyển bằng chân được tại nơi này (Gill/không có Walk ở Ocean thì mọi di chuyển là bơi).</summary>
        public static bool WalksHere(SpeciesBiology b, HabitatProfile h) => h.Realm != Realm.Ocean && Has(b.Locomotion, Locomotion.Walk);

        public static bool Allowed(SpeciesBiology b, HabitatProfile h, DragonBehavior behavior) =>
            SpeciesBlock(b, behavior) == BehaviorBlock.None && (h == null || HabitatBlock(h, behavior) == BehaviorBlock.None);

        internal static bool Has<T>(IReadOnlyList<T> list, T v)
        {
            var cmp = EqualityComparer<T>.Default;
            foreach (T x in list) if (cmp.Equals(x, v)) return true;
            return false;
        }
    }
}
