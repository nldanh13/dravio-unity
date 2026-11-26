using System;
using System.Collections.Generic;

namespace Dravio.Domain.Catalog
{
    /// <summary>Một mốc thưởng của bảng cân bằng sky_cloud (chuyến nhận thứ <see cref="Claims"/>, thêm <see cref="BonusEach"/> bụi sao, ghi chú / danh hiệu).</summary>
    public sealed class SkyCloudMilestone
    {
        public SkyCloudMilestone(int claims, int bonusEach, string rewardId) { Claims = claims; BonusEach = bonusEach; RewardId = rewardId; }
        public int Claims { get; }
        public int BonusEach { get; }
        public string RewardId { get; }
    }

    /// <summary>
    /// U2.13 (ADR 0208) — bảng cân bằng tuyến thám hiểm <c>sky_cloud</c> ("Sân mây Thiên Giới", vật liệu <c>stardust</c> "Bụi sao").
    /// <para>
    /// <b>CHƯA DUYỆT → TẮT trong game:</b> <see cref="Production"/> = <see cref="Disabled"/>, tuyến vẫn trả <c>route-rewards-undefined</c> như U2.11/U2.12.
    /// Các bảng khác chỉ dùng trong mô phỏng và test qua <see cref="Use"/> (phạm vi theo luồng, như luật thiên tai của dispatcher), để chủ dự án
    /// so sánh trước khi duyệt. Bật tuyến = đổi <see cref="Production"/> sang bảng được duyệt (một dòng), không đổi save (Schema v10).
    /// </para>
    /// <para>
    /// Đã duyệt độc lập với bảng: trứng Aurorawing một lần tại mốc <see cref="AurorawingEggClaims"/> chuyến sky_cloud đã nhận
    /// (<see cref="AurorawingEggRewardId"/>, qua grantedRewards + rewardQueue như trứng thế giới). Khi tuyến tắt thì không bao giờ đạt.
    /// </para>
    /// Không bảng nào cho vàng hay XP: thưởng chỉ là vật liệu <c>stardust</c>.
    /// </summary>
    public sealed class SkyCloudBalance
    {
        public const string RouteId = "sky_cloud";
        public const string MaterialId = "stardust";
        /// <summary>Đã duyệt (U2.13): mốc chuyến sky_cloud đã nhận trao trứng Aurorawing.</summary>
        public const int AurorawingEggClaims = 15;
        public const string AurorawingEggRewardId = "EXPEDITION_EGG_AURORAWING";
        public const string AurorawingSpeciesId = "aurorawing";

        public SkyCloudBalance(string id, bool enabled, int durationMinutes, int minQty, int maxQty, params SkyCloudMilestone[] milestones)
        {
            if (enabled && (durationMinutes <= 0 || minQty <= 0 || maxQty < minQty)) throw new ArgumentException("sky_cloud balance", nameof(id));
            Id = id; Enabled = enabled; DurationMinutes = durationMinutes; MinQty = minQty; MaxQty = maxQty;
            Milestones = Array.AsReadOnly(milestones ?? new SkyCloudMilestone[0]);
            Routes = Array.AsReadOnly(new[]
            {
                new ExpeditionRoute("origin_forest", "Rừng Khởi Nguyên", "origin", 15, ExpeditionRequirement.LandDweller, new[] { "wood", "seed" }, 2, 4),
                // GDD "ngọc/rêu biển" → crystal/coral (chủ dự án duyệt).
                new ExpeditionRoute("ocean_reef", "Rạn Thủy Vực", "ocean", 30, ExpeditionRequirement.Swimmer, new[] { "crystal", "coral" }, 2, 4),
                // GDD "bụi sao": bảng thưởng chờ duyệt (U2.13). Tắt = không vật liệu → route-rewards-undefined.
                enabled
                    ? new ExpeditionRoute(RouteId, "Sân mây Thiên Giới", "sky", durationMinutes, ExpeditionRequirement.Flyer, new[] { MaterialId }, minQty, maxQty)
                    : new ExpeditionRoute(RouteId, "Sân mây Thiên Giới", "sky", 45, ExpeditionRequirement.Flyer, new string[0], 2, 4),
            });
        }

        public string Id { get; }
        /// <summary>false = tuyến khóa (bảng chưa duyệt).</summary>
        public bool Enabled { get; }
        public int DurationMinutes { get; }
        public int MinQty { get; }
        public int MaxQty { get; }
        public IReadOnlyList<SkyCloudMilestone> Milestones { get; }
        internal IReadOnlyList<ExpeditionRoute> Routes { get; }

        /// <summary>Hiện trạng v10: tuyến khóa.</summary>
        public static readonly SkyCloudBalance Disabled = new SkyCloudBalance("disabled", false, 45, 2, 4);

        /// <summary>
        /// Đề xuất A (khuyến nghị, SKY_CLOUD_PROPOSAL): 45 phút, 2–4 bụi sao, mốc 1 / 5 / 15 / 40 cho 2 / 3 / 4 / 6 bụi sao (cùng khung U2.11).
        /// </summary>
        public static readonly SkyCloudBalance ProposedA = new SkyCloudBalance("A-45m-2to4", true, 45, 2, 4,
            new SkyCloudMilestone(1, 2, "note.cloud_first_flight"), new SkyCloudMilestone(5, 3, null),
            new SkyCloudMilestone(15, 4, "title.cloud_rider"), new SkyCloudMilestone(40, 6, "title.star_warden"));

        /// <summary>Phương án B: chuyến ngắn 30 phút (bằng tuyến rạn), 2–4 bụi sao, cùng mốc.</summary>
        public static readonly SkyCloudBalance ProposedB = new SkyCloudBalance("B-30m-2to4", true, 30, 2, 4,
            new SkyCloudMilestone(1, 2, "note.cloud_first_flight"), new SkyCloudMilestone(5, 3, null),
            new SkyCloudMilestone(15, 4, "title.cloud_rider"), new SkyCloudMilestone(40, 6, "title.star_warden"));

        /// <summary>Phương án C: chuyến dài 60 phút, 3–5 bụi sao, mốc lớn hơn (3 / 4 / 6 / 8).</summary>
        public static readonly SkyCloudBalance ProposedC = new SkyCloudBalance("C-60m-3to5", true, 60, 3, 5,
            new SkyCloudMilestone(1, 3, "note.cloud_first_flight"), new SkyCloudMilestone(5, 4, null),
            new SkyCloudMilestone(15, 6, "title.cloud_rider"), new SkyCloudMilestone(40, 8, "title.star_warden"));

        /// <summary>Bảng của game thật. CHƯA DUYỆT → <see cref="Disabled"/>.</summary>
        public static readonly SkyCloudBalance Production = Disabled;

        public static readonly IReadOnlyList<SkyCloudBalance> Candidates = Array.AsReadOnly(new[] { ProposedA, ProposedB, ProposedC });

        [ThreadStatic] private static SkyCloudBalance _current;

        /// <summary>Bảng đang áp dụng cho luồng hiện tại (mặc định <see cref="Production"/>).</summary>
        public static SkyCloudBalance Current => _current ?? Production;

        /// <summary>Áp dụng một bảng cho luồng hiện tại tới khi Dispose (chỉ mô phỏng / test). Lồng được.</summary>
        public static IDisposable Use(SkyCloudBalance balance) => new Scope(balance);

        private sealed class Scope : IDisposable
        {
            private readonly SkyCloudBalance _previous;
            private bool _done;
            public Scope(SkyCloudBalance b) { _previous = _current; _current = b; }
            public void Dispose()
            {
                if (_done) return;
                _done = true;
                _current = _previous;
            }
        }
    }
}
