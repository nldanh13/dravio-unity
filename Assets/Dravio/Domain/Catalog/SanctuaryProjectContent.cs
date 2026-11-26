using System;
using System.Collections.Generic;

namespace Dravio.Domain.Catalog
{
    /// <summary>Kiểu công trình trang trí (để Sanctuary chọn prefab / vị trí neo). Chỉ trình diễn.</summary>
    public enum ProjectFootprint
    {
        /// <summary>Công trình đứng trên nền (cổng, tượng, đình).</summary>
        [WireName("landmark")] Landmark,
        /// <summary>Lối đi, cầu, bậc đá.</summary>
        [WireName("path")] Path,
        /// <summary>Thảm thực vật, vườn.</summary>
        [WireName("garden")] Garden,
        /// <summary>Ánh sáng, đèn, rune.</summary>
        [WireName("light")] Light,
        /// <summary>Mặt nước, thác, hồ.</summary>
        [WireName("water")] Water,
        /// <summary>Mở rộng cảnh quan nền (nối tiếp sau khi hết nội dung đặt tên).</summary>
        [WireName("vista")] Vista,
    }

    /// <summary>Nội dung một dự án Sanctuary theo thứ tự xây (index 0-based).</summary>
    public sealed class SanctuaryProjectDef
    {
        internal SanctuaryProjectDef(string id, string nameVi, string zoneId, ProjectFootprint footprint, string styleBlueprintId)
        {
            Id = id; NameVi = nameVi; ZoneId = zoneId; Footprint = footprint; StyleBlueprintId = styleBlueprintId;
        }
        public string Id { get; }
        public string NameVi { get; }
        public string NameKey => "project." + Id + ".name";
        /// <summary>Thế giới công trình thuộc về. Nếu thế giới đó chưa mở lúc hiển thị, Sanctuary đặt công trình ở Khởi Nguyên (xem <see cref="SanctuaryProjectContent.DisplayZone"/>).</summary>
        public string ZoneId { get; }
        public ProjectFootprint Footprint { get; }
        /// <summary>Mẫu trang trí (thưởng nhiệm vụ) đổi kiểu dáng của công trình khi người chơi đã có. null = không có biến thể.</summary>
        public string StyleBlueprintId { get; }
    }

    /// <summary>
    /// U2.11 (ADR 0206): nội dung dự án trang trí Sanctuary. CHỈ TRÌNH DIỄN: không đổi giá, vật liệu, giới hạn hay hiệu ứng của E5 (ADR 0205).
    /// Dự án thứ n luôn là <see cref="Named"/>[n] (n &lt; 20), sau đó là "Mở rộng cảnh quan" cấp n − 19 xoay vòng qua ba thế giới. Thứ tự đi theo
    /// hành trình: 8 công trình Khởi Nguyên (dự án 1–8, tổng ≈ 0,4 triệu G), 6 Thủy Vực, 6 Thiên Giới. Không cần lưu gì thêm: save chỉ có số dự án.
    /// </summary>
    public static class SanctuaryProjectContent
    {
        public static readonly IReadOnlyList<SanctuaryProjectDef> Named = Array.AsReadOnly(new[]
        {
            // ---- Chương 1 — Khởi Nguyên: phục dựng khu đền cổ ----
            new SanctuaryProjectDef("origin_mossy_steps", "Bậc đá rêu phong", "origin", ProjectFootprint.Path, null),
            new SanctuaryProjectDef("origin_lantern_row", "Hàng đèn đá dẫn lối", "origin", ProjectFootprint.Light, "blueprint.forest_lantern"),
            new SanctuaryProjectDef("origin_meadow_arbor", "Giàn hoa đồng cỏ", "origin", ProjectFootprint.Garden, "blueprint.meadow_arbor"),
            new SanctuaryProjectDef("origin_trail_shrine", "Miếu nhỏ bên lối mòn", "origin", ProjectFootprint.Landmark, "blueprint.trail_marker"),
            new SanctuaryProjectDef("origin_lily_pond", "Ao sen bờ hồ", "origin", ProjectFootprint.Water, null),
            new SanctuaryProjectDef("origin_rune_circle", "Vòng đá rune", "origin", ProjectFootprint.Light, null),
            new SanctuaryProjectDef("origin_ancient_gate", "Cổng đá cổ phục dựng", "origin", ProjectFootprint.Landmark, null),
            new SanctuaryProjectDef("origin_elder_statue", "Tượng rồng tổ", "origin", ProjectFootprint.Landmark, null),
            // ---- Chương 2 — Thủy Vực: rạn san hô và cửa sông ----
            new SanctuaryProjectDef("ocean_tide_pools", "Vũng triều", "ocean", ProjectFootprint.Water, "blueprint.tide_pool"),
            new SanctuaryProjectDef("ocean_coral_arch", "Vòm san hô", "ocean", ProjectFootprint.Landmark, null),
            new SanctuaryProjectDef("ocean_shell_path", "Lối vỏ sò", "ocean", ProjectFootprint.Path, null),
            new SanctuaryProjectDef("ocean_kelp_garden", "Vườn tảo bẹ", "ocean", ProjectFootprint.Garden, null),
            new SanctuaryProjectDef("ocean_glow_buoys", "Phao phát sáng", "ocean", ProjectFootprint.Light, null),
            new SanctuaryProjectDef("ocean_sunken_shrine", "Đền chìm", "ocean", ProjectFootprint.Landmark, null),
            // ---- Chương 3 — Thiên Giới: đảo mây ----
            new SanctuaryProjectDef("sky_cloud_bridge", "Cầu mây", "sky", ProjectFootprint.Path, null),
            new SanctuaryProjectDef("sky_cloud_garden", "Vườn trên mây", "sky", ProjectFootprint.Garden, "blueprint.cloud_garden"),
            new SanctuaryProjectDef("sky_wind_chimes", "Chuông gió", "sky", ProjectFootprint.Light, null),
            new SanctuaryProjectDef("sky_falling_spring", "Suối rơi", "sky", ProjectFootprint.Water, null),
            new SanctuaryProjectDef("sky_star_observatory", "Đài ngắm sao", "sky", ProjectFootprint.Landmark, null),
            new SanctuaryProjectDef("sky_aurora_spire", "Tháp cực quang", "sky", ProjectFootprint.Landmark, null),
        });

        private static readonly string[] VistaZones = { "origin", "ocean", "sky" };

        /// <summary>Nội dung của dự án thứ <paramref name="index"/> (0-based), hoặc null khi ngoài 0 … Max − 1.</summary>
        public static SanctuaryProjectDef Get(int index)
        {
            if (index < 0 || index >= SanctuaryProjectCatalog.Max) return null;
            if (index < Named.Count) return Named[index];
            int level = index - Named.Count + 1;
            string zone = VistaZones[(level - 1) % VistaZones.Length];
            return new SanctuaryProjectDef("vista_" + zone + "_" + level, "Mở rộng cảnh quan " + level, zone, ProjectFootprint.Vista, null);
        }

        /// <summary>Thế giới để hiển thị công trình: thế giới của nó nếu đã mở, ngược lại Khởi Nguyên (công trình không bao giờ biến mất).</summary>
        public static string DisplayZone(GameState s, int index)
        {
            SanctuaryProjectDef d = Get(index);
            if (d == null) return null;
            return s.FindZone(d.ZoneId)?.Unlocked == true ? d.ZoneId : "origin";
        }
    }
}
