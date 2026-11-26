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

        /// <summary>Vật liệu cần cho dự án thứ n (mỗi loại trong <see cref="MaterialIds"/>).</summary>
        public static int MaterialEach(int n) => n < FirstMaterialProject ? 0 : 1 + n / 5;
    }
}
