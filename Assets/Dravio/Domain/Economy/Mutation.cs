using System;
using System.Collections.Generic;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;

namespace Dravio.Domain.Economy
{
    /// <summary>Kết quả một lần roll biến thể: biến thể và bộ đếm pity mới.</summary>
    public readonly struct MutationRoll
    {
        public MutationRoll(MutationType mutation, int pityCount) { Mutation = mutation; PityCount = pityCount; }
        public MutationType Mutation { get; }
        public int PityCount { get; }
    }

    /// <summary>
    /// Phân phối biến thể Aether và pity Prismatic (GDD 3.1 trang 15; ADR 0003 #6; src/engine/economy/mutation.ts).
    /// L = cấp luck (0–8), b = số bố mẹ có biến thể (0–2); w = 1 + 0,25L; q = min(0,60; 15w/(85+15w) + 0,08b).
    /// </summary>
    public static class Mutation
    {
        public static readonly IReadOnlyList<MutationType> Order = Array.AsReadOnly(new[] { MutationType.Normal, MutationType.Crystal, MutationType.Golden, MutationType.Prismatic });

        /// <summary>Xác suất [normal, crystal, golden, prismatic]; cùng thứ tự phép tính dấu phẩy động với TS.</summary>
        public static double[] Distribution(int luckLevel, int mutatedParents)
        {
            double w = 1 + 0.25 * luckLevel;
            double q = Math.Min(0.6, (15 * w) / (85 + 15 * w) + 0.08 * mutatedParents);
            return new[] { 1 - q, q * EconomyTables.ShareCrystal, q * EconomyTables.ShareGolden, q * EconomyTables.SharePrismatic };
        }

        /// <summary>
        /// Lần thứ 200 liên tiếp không Prismatic bảo đảm Prismatic (không tiêu RNG); reset khi ra Prismatic.
        /// Trứng hướng dẫn, trứng mở vùng cố định và lượt nghiên cứu định hướng KHÔNG gọi hàm này.
        /// </summary>
        public static MutationRoll Roll(IRng rng, int luckLevel, int mutatedParents, int pityCount)
        {
            if (pityCount + 1 >= EconomyTables.PrismaticPity) return new MutationRoll(MutationType.Prismatic, 0);
            double[] d = Distribution(luckLevel, mutatedParents);
            var entries = new List<KeyValuePair<MutationType, double>>(4);
            for (int i = 0; i < 4; i++) entries.Add(new KeyValuePair<MutationType, double>(Order[i], d[i]));
            MutationType m = rng.PickWeighted(entries);
            return new MutationRoll(m, m == MutationType.Prismatic ? 0 : pityCount + 1);
        }
    }

    /// <summary>Roll gói trứng (GDD 3.1 trang 15; src/engine/economy/packs.ts rollPackSpecies).</summary>
    public static class PackRolls
    {
        public static readonly IReadOnlyList<Rarity> RarityOrder = Array.AsReadOnly(new[] { Rarity.Common, Rarity.Rare, Rarity.Epic, Rarity.Legendary });

        /// <summary>Một gói một trứng: chọn độ hiếm theo bảng (2 bước RNG: độ hiếm, rồi loài), chọn đều trong loài packEligible (loại Amberhorn/Aurorawing).</summary>
        public static string RollSpecies(IRng rng, PackDef pack)
        {
            var entries = new List<KeyValuePair<Rarity, double>>(4);
            foreach (Rarity r in RarityOrder) entries.Add(new KeyValuePair<Rarity, double>(r, PackCatalog.Odds(pack, r)));
            Rarity rarity = rng.PickWeighted(entries);
            var pool = new List<DragonSpecies>();
            foreach (DragonSpecies s in SpeciesCatalog.All) if (s.PackEligible && s.Rarity == rarity) pool.Add(s);
            return rng.PickUniform(pool).Id;
        }
    }
}
