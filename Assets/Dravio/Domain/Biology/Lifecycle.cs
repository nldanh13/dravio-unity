using System;
using System.Collections.Generic;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Economy;

namespace Dravio.Domain.Biology
{
    /// <summary>Một đoạn thời gian có stage không đổi.</summary>
    public readonly struct GrowthSegment
    {
        public GrowthSegment(GrowthStage stage, long durationMs) { Stage = stage; DurationMs = durationMs; }
        public GrowthStage Stage { get; }
        public long DurationMs { get; }
    }

    public readonly struct StageChange
    {
        public StageChange(GrowthStage from, GrowthStage to) { From = from; To = to; }
        public GrowthStage From { get; }
        public GrowthStage To { get; }
    }

    public sealed class GrowthResult
    {
        internal GrowthResult(long xpMs, IReadOnlyList<StageChange> stageChanges, long goldMicro, long remainder)
        {
            XpMs = xpMs; StageChanges = stageChanges; GoldMicro = goldMicro; Remainder = remainder;
        }

        public long XpMs { get; }
        public IReadOnlyList<StageChange> StageChanges { get; }
        /// <summary>Vàng đã cộng (microG nguyên).</summary>
        public long GoldMicro { get; }
        /// <summary>Phần lẻ mới, đơn vị microG / <see cref="Lifecycle.RemainderScale"/>.</summary>
        public long Remainder { get; }
    }

    /// <summary>
    /// Vòng đời Baby → Juvenile (35 %) → Adult (100 %) và tích phân vàng theo mốc stage
    /// (GDD 3.1 trang 15, 18; src/engine/progression/growth.ts). Toàn bộ bằng số nguyên ms.
    /// </summary>
    public static class Lifecycle
    {
        /// <summary>Hệ số vàng dạng phân số nguyên num/4: online 4/4, offline 3/4.</summary>
        public const int GoldFactorDen = 4;
        public static int GoldFactorNum(AdvanceMode mode) => mode == AdvanceMode.Online ? 4 : 3;
        /// <summary>goldRemainder có đơn vị microG / 4000.</summary>
        public const long RemainderScale = 1000L * GoldFactorDen;

        /// <summary>xpMsOf: Math.round(xpSec × 1000).</summary>
        public static long XpMsOf(DragonState d) => (long)JsCompat.Round(d.XpSec * 1000);

        public static long JuvenileAtMs(int growthTimeSec) => growthTimeSec * 350L;
        public static long AdultAtMs(int growthTimeSec) => growthTimeSec * 1000L;

        public static GrowthStage StageAtXpMs(long xpMs, int growthTimeSec)
        {
            if (xpMs < JuvenileAtMs(growthTimeSec)) return GrowthStage.Baby;
            if (xpMs < AdultAtMs(growthTimeSec)) return GrowthStage.Juvenile;
            return GrowthStage.Adult;
        }

        /// <summary>Stage hiện tại của rồng (dẫn xuất, không lưu).</summary>
        public static GrowthStage StageOf(DragonState d) => EconomyRates.GrowthStageOf(d.XpSec, SpeciesCatalog.Get(d.SpeciesId));

        /// <summary>Chia dtMs thành các đoạn stage không đổi, bắt đầu từ xpMs. XP tối đa = thời gian trưởng thành.</summary>
        public static List<GrowthSegment> GrowthSegments(long xpMs, long dtMs, int growthTimeSec)
        {
            long juvenileAt = JuvenileAtMs(growthTimeSec);
            long adultAt = AdultAtMs(growthTimeSec);
            var segments = new List<GrowthSegment>();
            long xp = xpMs;
            long left = dtMs;
            while (left > 0)
            {
                GrowthStage stage = StageAtXpMs(xp, growthTimeSec);
                long until = stage == GrowthStage.Baby ? juvenileAt - xp : stage == GrowthStage.Juvenile ? adultAt - xp : left;
                long dur = Math.Min(left, until);
                segments.Add(new GrowthSegment(stage, dur));
                left -= dur;
                xp = Math.Min(adultAt, xp + dur);
            }
            return segments;
        }

        /// <summary>
        /// Tiến hóa một rồng thêm dtMs. <paramref name="producing"/> = rồng ở slot sản xuất hợp lệ suốt khoảng.
        /// Vàng = Σ rate(stage) × ms × num, chia REMAINDER_SCALE, phần dư chuyển sang lần sau.
        /// </summary>
        public static GrowthResult AdvanceDragon(DragonState dragon, long dtMs, bool producing, AdvanceMode mode, long remainder)
        {
            DragonSpecies species = SpeciesCatalog.Get(dragon.SpeciesId);
            long startXp = XpMsOf(dragon);
            List<GrowthSegment> segments = GrowthSegments(startXp, dtMs, species.GrowthTimeSec);
            long goldMicro = 0;
            long rem = remainder;
            if (producing)
            {
                int num = GoldFactorNum(mode);
                foreach (GrowthSegment seg in segments)
                {
                    long rate = EconomyRates.RateMicroGPerSec(species, seg.Stage, dragon.Mutation);
                    long scaled = checked(rate * seg.DurationMs * num + rem);
                    long whole = scaled / RemainderScale;
                    goldMicro = checked(goldMicro + whole);
                    rem = scaled - whole * RemainderScale;
                }
            }
            long adultAt = AdultAtMs(species.GrowthTimeSec);
            long endXp = Math.Min(adultAt, startXp + dtMs);
            var changes = new List<StageChange>();
            for (int i = 1; i < segments.Count; i++)
            {
                if (segments[i - 1].Stage != segments[i].Stage) changes.Add(new StageChange(segments[i - 1].Stage, segments[i].Stage));
            }
            // Chạm đúng mốc ở cuối khoảng (đoạn kế tiếp độ dài 0 không được tạo).
            GrowthStage endStage = StageAtXpMs(endXp, species.GrowthTimeSec);
            GrowthStage lastStage = segments.Count > 0 ? segments[segments.Count - 1].Stage : StageAtXpMs(startXp, species.GrowthTimeSec);
            if (endStage != lastStage) changes.Add(new StageChange(lastStage, endStage));
            return new GrowthResult(endXp, changes, goldMicro, rem);
        }

        /// <summary>Thêm XP tức thời (thức ăn), chặn ở mức trưởng thành; trả các lần đổi stage (Baby→Adult phát 2 lần).</summary>
        public static (long xpMs, IReadOnlyList<StageChange> stageChanges) AddXp(DragonState dragon, double xpSec)
        {
            DragonSpecies species = SpeciesCatalog.Get(dragon.SpeciesId);
            long adultAt = AdultAtMs(species.GrowthTimeSec);
            long before = XpMsOf(dragon);
            long after = Math.Min(adultAt, before + (long)JsCompat.Round(xpSec * 1000));
            GrowthStage s0 = StageAtXpMs(before, species.GrowthTimeSec);
            GrowthStage s1 = StageAtXpMs(after, species.GrowthTimeSec);
            var changes = new List<StageChange>();
            if (s0 != s1)
            {
                if (s0 == GrowthStage.Baby && s1 == GrowthStage.Adult)
                {
                    changes.Add(new StageChange(GrowthStage.Baby, GrowthStage.Juvenile));
                    changes.Add(new StageChange(GrowthStage.Juvenile, GrowthStage.Adult));
                }
                else
                {
                    changes.Add(new StageChange(s0, s1));
                }
            }
            return (after, changes);
        }
    }

    /// <summary>Gắn bó (src/engine/biology/bond.ts): +1, trần 10 / ngày UTC / rồng và 100 tuyệt đối, không bao giờ giảm.</summary>
    public static class BondRules
    {
        /// <summary>Cập nhật rồng tại chỗ; trả số bond thực nhận (0 hoặc 1). Luôn ghi bondDay/bondDaily như bản TS.</summary>
        public static int GainBond(DragonState dragon, long nowMs)
        {
            string day = JsCompat.UtcDay(nowMs);
            int daily = dragon.BondDay == day ? (dragon.BondDaily ?? 0) : 0;
            dragon.BondDay = day;
            if (daily >= CareRules.BondDailyCap || dragon.Bond >= CareRules.BondMax)
            {
                dragon.BondDaily = daily;
                return 0;
            }
            dragon.Bond += 1;
            dragon.BondDaily = daily + 1;
            return 1;
        }
    }
}
