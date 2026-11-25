using System;
using Dravio.Domain.Catalog;

namespace Dravio.Domain.Economy
{
    /// <summary>
    /// Công thức kinh tế thuần (src/engine/economy/rates.ts). Phép nhân hệ số dùng double đúng thứ tự như
    /// TypeScript rồi floor về microG nguyên, nên kết quả trùng từng microG (đã đối chiếu golden 192 tổ hợp).
    /// </summary>
    public static class EconomyRates
    {
        /// <summary>growthStage: Baby &lt; 35 %, Juvenile &lt; 100 %, Adult = 100 % thời gian trưởng thành.</summary>
        public static GrowthStage GrowthStageOf(double xpSec, DragonSpecies species)
        {
            double ratio = xpSec / species.GrowthTimeSec;
            if (ratio < 0.35) return GrowthStage.Baby;
            if (ratio < 1) return GrowthStage.Juvenile;
            return GrowthStage.Adult;
        }

        /// <summary>rate = floor(baseRateMicroG × stageMult × mutationMult) microG/s.</summary>
        public static long RateMicroGPerSec(DragonSpecies species, GrowthStage stage, MutationType mutation)
        {
            double v = species.BaseRateMicroG * EconomyTables.StageRateMult(stage);
            v *= EconomyTables.MutationRateMult(mutation);
            return (long)Math.Floor(v);
        }

        /// <summary>sell = floor(baseSell × stageSell × mutationSell) G. Lệnh bán chưa port ở U2.1.</summary>
        public static long SellPriceG(DragonSpecies species, GrowthStage stage, MutationType mutation)
        {
            double v = species.BaseSellG * EconomyTables.StageSellMult(stage);
            v *= EconomyTables.MutationSellMult(mutation);
            return (long)Math.Floor(v);
        }

        /// <summary>Ngân sách sản xuất toàn cục: 5 + 2 × cấp, tối đa 25.</summary>
        public static int ProductionBudget(int capacityLevel) => Math.Min(25, 5 + 2 * capacityLevel);

        /// <summary>upgradeCost = floor(base × scale^level) G; null khi đã tối đa. Lệnh mua chưa port ở U2.1.</summary>
        public static long? UpgradeCostG(UpgradeDef def, int level)
        {
            if (level >= def.Max) return null;
            return (long)Math.Floor(def.BaseG * Math.Pow(def.Scale, level));
        }

        /// <summary>
        /// offlineGoldMicro của rates.ts (hàm tiện ích; engine dùng tích phân theo stage trong <see cref="Biology.Lifecycle"/>).
        /// Giữ phần lẻ: trả phần nguyên và phần dư (đơn vị microG / 1000).
        /// </summary>
        public static (long goldMicro, double remainder) OfflineGoldMicro(long totalRateMicroGPerSec, long elapsedMs, double remainder = 0)
        {
            double scaled = (double)totalRateMicroGPerSec * elapsedMs * EconomyTables.OfflineGoldFactor + remainder;
            double gold = Math.Floor(scaled / 1000);
            return ((long)gold, scaled - gold * 1000);
        }

        /// <summary>microToG.</summary>
        public static long MicroToG(long micro) => new MicroGold(micro).WholeGoldFloor;
    }
}
