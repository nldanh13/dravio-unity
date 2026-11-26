using System;
using System.Collections.Generic;

namespace Dravio.Domain.Catalog
{
    /// <summary>
    /// U2.10 E5 (ADR 0204, đã duyệt): dự án Sanctuary — điểm tiêu dài hạn tự nguyện, chỉ thẩm mỹ. Không tăng sản xuất, không cho vàng,
    /// không mở khóa gameplay. Dự án thứ n (đếm từ 0) giá floor(20 000 × 1,25ⁿ) G, cộng vật liệu thám hiểm Khởi Nguyên từ dự án thứ 3
    /// (n ≥ 2): 1 + n / 5 gỗ và cùng số hạt giống, để vật liệu có chỗ tiêu như các game nhiều tài nguyên.
    /// </summary>
    public static class SanctuaryProjectCatalog
    {
        public const long BaseG = 20_000;
        /// <summary>Hệ số tăng giá mỗi dự án (= 5 / 4, tính bằng số nguyên).</summary>
        public const double Scale = 1.25;
        /// <summary>Trần kỹ thuật (giá dự án cuối ≈ 1,0 × 10¹⁰ G, vẫn nằm trong long microG).</summary>
        public const int Max = 60;
        public const int FirstMaterialProject = 2;
        /// <summary>Vật liệu Khởi Nguyên (gỗ, hạt giống). U2.13: vật liệu thật của từng dự án do <see cref="Materials"/> quyết định theo thế giới.</summary>
        public static readonly IReadOnlyList<string> MaterialIds = Array.AsReadOnly(new[] { "wood", "seed" });

        // Số nguyên chính xác (không dùng Math.Pow: kết quả phải giống hệt nhau trên mọi nền tảng): floor(BaseG × 5ⁿ / 4ⁿ).
        private static readonly long[] Costs = BuildCosts();

        private static long[] BuildCosts()
        {
            var o = new long[Max];
            for (int n = 0; n < Max; n++) o[n] = (long)(BaseG * System.Numerics.BigInteger.Pow(5, n) / System.Numerics.BigInteger.Pow(4, n));
            return o;
        }

        /// <summary>Giá vàng của dự án thứ n (0-based); null khi đã xây đủ.</summary>
        public static long? CostG(int n)
        {
            if (n < 0 || n >= Max) return null;
            return Costs[n];
        }

        /// <summary>
        /// Số lượng cơ sở của dự án thứ n (công thức E5 giữ nguyên: 0 cho hai dự án đầu, rồi 1 + n / 5). U2.13: với vật liệu theo thế giới,
        /// số lượng MỖI loại vật liệu của dự án là <see cref="ProjectMaterials.Each"/> (gấp đôi khi thế giới chỉ có một loại vật liệu).
        /// </summary>
        public static int MaterialEach(int n) => n < FirstMaterialProject ? 0 : 1 + n / 5;

        /// <summary>
        /// U2.13 (ADR 0209, đã duyệt "vật liệu theo thế giới"): vật liệu và số lượng của dự án thứ n trên save <paramref name="s"/>, theo
        /// <see cref="SanctuaryMaterialRules.Current"/> (game thật: <see cref="SanctuaryMaterialRules.PerWorld"/>).
        /// </summary>
        public static ProjectMaterials Materials(GameState s, int n) => SanctuaryMaterialRules.Current.For(s, n);
    }

    /// <summary>Vật liệu một dự án Sanctuary cần: mỗi ID trong <see cref="Ids"/> đúng <see cref="Each"/> đơn vị.</summary>
    public sealed class ProjectMaterials
    {
        internal ProjectMaterials(IReadOnlyList<string> ids, int each, string sourceZoneId, string projectZoneId)
        {
            Ids = ids; Each = each; SourceZoneId = sourceZoneId; ProjectZoneId = projectZoneId;
        }
        public IReadOnlyList<string> Ids { get; }
        /// <summary>Số lượng mỗi loại (0 với hai dự án đầu).</summary>
        public int Each { get; }
        /// <summary>Thế giới cung cấp vật liệu (origin / ocean / sky).</summary>
        public string SourceZoneId { get; }
        /// <summary>Thế giới của công trình (SanctuaryProjectContent).</summary>
        public string ProjectZoneId { get; }
        /// <summary>true = thế giới của công trình chưa mở / chưa có nguồn vật liệu, nên dùng vật liệu của thế giới thấp hơn (không bế tắc).</summary>
        public bool Fallback => SourceZoneId != ProjectZoneId;
        public int Total => Each * Ids.Count;
    }

    public enum SanctuaryMaterialMode
    {
        /// <summary>U2.10 E5: mọi dự án dùng gỗ + hạt giống (trước U2.13; chỉ để so sánh).</summary>
        [WireName("legacyWoodSeed")] LegacyWoodSeed,
        /// <summary>U2.13 (khuyến nghị, game thật): vật liệu theo thế giới của công trình; thế giới chưa mở hoặc chưa có nguồn → thế giới thấp hơn.</summary>
        [WireName("perWorld")] PerWorld,
        /// <summary>Chỉ mô phỏng: vật liệu theo thế giới của công trình, KHÔNG lùi (minh họa bế tắc khi thế giới chưa mở).</summary>
        [WireName("perWorldStrict")] PerWorldStrict,
    }

    /// <summary>
    /// U2.13 (ADR 0209) — luật vật liệu dự án Sanctuary. Đã duyệt: Khởi Nguyên gỗ + hạt giống, Thủy Vực pha lê + san hô, Thiên Giới bụi sao (một loại,
    /// gấp đôi số lượng để tổng đơn vị không đổi; chỉ khi bảng sky_cloud được duyệt). Giá vàng, số dự án tối đa, công thức số lượng giữ nguyên.
    /// Không bế tắc: vật liệu của thế giới chưa mở / chưa có tuyến cho vật liệu đó lùi về thế giới thấp hơn (sky → ocean → origin; Khởi Nguyên luôn
    /// có tuyến rừng). Save chỉ lưu số dự án đã xây, nên không cần migration; dự án đã xây không bị tính lại.
    /// </summary>
    public sealed class SanctuaryMaterialRules
    {
        public SanctuaryMaterialRules(SanctuaryMaterialMode mode) { Mode = mode; }
        public SanctuaryMaterialMode Mode { get; }

        public static readonly SanctuaryMaterialRules LegacyWoodSeed = new SanctuaryMaterialRules(SanctuaryMaterialMode.LegacyWoodSeed);
        public static readonly SanctuaryMaterialRules PerWorld = new SanctuaryMaterialRules(SanctuaryMaterialMode.PerWorld);
        public static readonly SanctuaryMaterialRules PerWorldStrict = new SanctuaryMaterialRules(SanctuaryMaterialMode.PerWorldStrict);
        /// <summary>Game thật: đã duyệt vật liệu theo thế giới.</summary>
        public static readonly SanctuaryMaterialRules Production = PerWorld;

        [ThreadStatic] private static SanctuaryMaterialRules _current;
        public static SanctuaryMaterialRules Current => _current ?? Production;

        /// <summary>Áp dụng luật cho luồng hiện tại tới khi Dispose (chỉ mô phỏng / test).</summary>
        public static IDisposable Use(SanctuaryMaterialRules rules) => new Scope(rules);

        private sealed class Scope : IDisposable
        {
            private readonly SanctuaryMaterialRules _previous;
            private bool _done;
            public Scope(SanctuaryMaterialRules r) { _previous = _current; _current = r; }
            public void Dispose() { if (_done) return; _done = true; _current = _previous; }
        }

        private static readonly IReadOnlyList<string> Origin = Array.AsReadOnly(new[] { "wood", "seed" });
        private static readonly IReadOnlyList<string> Ocean = Array.AsReadOnly(new[] { "crystal", "coral" });
        private static readonly IReadOnlyList<string> Sky = Array.AsReadOnly(new[] { SkyCloudBalance.MaterialId });

        /// <summary>Vật liệu của từng thế giới (thứ tự origin, ocean, sky).</summary>
        public static IReadOnlyList<string> WorldMaterials(string zoneId) => zoneId == "sky" ? Sky : zoneId == "ocean" ? Ocean : Origin;

        /// <summary>Mọi vật liệu thế giới có tuyến thám hiểm mở, có bảng thưởng (người chơi luôn đi thêm được).</summary>
        public static bool Sourced(GameState s, string zoneId)
        {
            if (s.FindZone(zoneId)?.Unlocked != true) return false;
            foreach (string m in WorldMaterials(zoneId))
            {
                bool ok = false;
                foreach (ExpeditionRoute r in ExpeditionCatalog.Routes)
                    if (r.RewardsDefined && s.FindZone(r.ZoneId)?.Unlocked == true && Contains(r.Materials, m)) { ok = true; break; }
                if (!ok) return false;
            }
            return true;
        }

        private static bool Contains(IReadOnlyList<string> xs, string x)
        {
            foreach (string v in xs) if (v == x) return true;
            return false;
        }

        private static string Lower(string zoneId) => zoneId == "sky" ? "ocean" : "origin";

        public ProjectMaterials For(GameState s, int n)
        {
            int baseEach = SanctuaryProjectCatalog.MaterialEach(n);
            string projectZone = SanctuaryProjectContent.Get(n)?.ZoneId ?? "origin";
            if (Mode == SanctuaryMaterialMode.LegacyWoodSeed) return new ProjectMaterials(Origin, baseEach, "origin", projectZone);
            string zone = projectZone;
            if (Mode == SanctuaryMaterialMode.PerWorld) while (zone != "origin" && !Sourced(s, zone)) zone = Lower(zone);
            IReadOnlyList<string> ids = WorldMaterials(zone);
            // Tổng đơn vị không đổi: thế giới một vật liệu (bụi sao) cần gấp đôi.
            int each = ids.Count == 1 ? 2 * baseEach : baseEach;
            return new ProjectMaterials(ids, each, zone, projectZone);
        }
    }
}
