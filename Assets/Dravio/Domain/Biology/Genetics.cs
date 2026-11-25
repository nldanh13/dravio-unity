using System.Collections.Generic;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;

namespace Dravio.Domain.Biology
{
    /// <summary>
    /// Phần di truyền cần cho U2.1 (src/engine/genetics/alleles.ts): chuẩn hóa cặp allele, kiểm genotype,
    /// genotype founder ổn định. Lai tạo, kiểu hình và phân phối con thuộc U4, chưa port.
    /// </summary>
    public static class Genetics
    {
        /// <summary>Allele trội đứng trước (Rr, không phải rR).</summary>
        public static AllelePair NormalizePair(LocusDef locus, string a, string b)
        {
            if (b == locus.Dominant && a != locus.Dominant) return new AllelePair(b, a);
            return new AllelePair(a, b);
        }

        /// <summary>Genotype hợp lệ: đủ mọi locus của loài, allele thuộc bộ allele của locus, đã chuẩn hóa, không có locus thừa.</summary>
        public static bool IsValidGenotype(Genotype genotype, IReadOnlyList<string> loci)
        {
            if (genotype == null) return false;
            foreach (string id in loci)
            {
                if (!genotype.TryGet(id, out AllelePair pair)) return false;
                LocusDef locus = LociCatalog.Get(id);
                if (pair.First == null || pair.Second == null) return false;
                if (!locus.HasAllele(pair.First) || !locus.HasAllele(pair.Second)) return false;
                AllelePair norm = NormalizePair(locus, pair.First, pair.Second);
                if (!norm.Equals(pair)) return false;
            }
            foreach (string key in genotype.LocusIds)
            {
                bool known = false;
                foreach (string id in loci) if (id == key) { known = true; break; }
                if (!known) return false;
            }
            return true;
        }

        /// <summary>
        /// Genotype founder ổn định từ UID + contentVersion: RNG riêng seed = hashSeed("founder", uid, contentVersion),
        /// mỗi allele trội với xác suất founderDominantFreq (50 %). Không tiêu RNG của engine.
        /// </summary>
        public static Genotype FounderGenotype(string uid, string contentVersion, IReadOnlyList<string> loci)
        {
            IRng rng = Rng.Create(Rng.HashSeed("founder", uid, contentVersion));
            var g = new Genotype();
            foreach (string id in loci)
            {
                LocusDef locus = LociCatalog.Get(id);
                string first = rng.Next() < locus.FounderDominantFreq ? locus.Dominant : locus.Recessive;
                string second = rng.Next() < locus.FounderDominantFreq ? locus.Dominant : locus.Recessive;
                g.Set(id, NormalizePair(locus, first, second));
            }
            return g;
        }
    }
}
