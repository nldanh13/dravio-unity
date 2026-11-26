using System.Collections.Generic;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;

namespace Dravio.Domain.Biology
{
    /// <summary>
    /// Di truyền Mendel P0 (GDD 3.1 trang 8; src/engine/genetics/alleles.ts): chuẩn hóa cặp allele, kiểm genotype,
    /// genotype founder ổn định (U2.1); phân phối con, roll con theo phân ly độc lập và kiểu hình dẫn xuất (U2.5).
    /// Locus pigment R/r và eye B/b trội hoàn toàn, pattern P/p trội không hoàn toàn. Biến thể Aether không phải allele.
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
    
        /// <summary>Số allele trội (0, 1, 2) tại một locus.</summary>
        public static int DominantCount(LocusDef locus, AllelePair pair) => (pair.First == locus.Dominant ? 1 : 0) + (pair.Second == locus.Dominant ? 1 : 0);

        /// <summary>Khóa i18n kiểu hình theo số allele trội (LocusDef.phenotypeKeys của TS: [lặn, dị hợp, trội]).</summary>
        public static string PhenotypeKey(string locusId, AllelePair pair)
        {
            LocusDef locus = LociCatalog.Get(locusId);
            return PhenotypeKeys(locusId)[DominantCount(locus, pair)];
        }

        private static string[] PhenotypeKeys(string locusId)
        {
            switch (locusId)
            {
                case "pigment": return new[] { "phenotype.pigment.cool", "phenotype.pigment.warm", "phenotype.pigment.warm" };
                case "pattern": return new[] { "phenotype.pattern.plain", "phenotype.pattern.medium", "phenotype.pattern.bold" };
                case "eye": return new[] { "phenotype.eye.blue", "phenotype.eye.amber", "phenotype.eye.amber" };
                default: throw new DomainInvariantException("Unknown locus id: " + locusId);
            }
        }

        /// <summary>Kiểu hình theo danh sách locus của loài; locus thiếu trong genotype → "phenotype.notExpressed". Không lưu, luôn dẫn xuất.</summary>
        public static List<KeyValuePair<string, string>> DerivePhenotype(Genotype genotype, IReadOnlyList<string> loci)
        {
            var o = new List<KeyValuePair<string, string>>();
            foreach (string id in loci)
                o.Add(new KeyValuePair<string, string>(id, genotype.TryGet(id, out AllelePair pair) ? PhenotypeKey(id, pair) : "phenotype.notExpressed"));
            return o;
        }

        /// <summary>
        /// Phân phối genotype con tại một locus: mỗi bố mẹ góp một allele với xác suất 1/2 (4 tổ hợp × 0,25).
        /// Thứ tự kết quả theo lần gặp đầu tiên (Map của TS); tổng luôn bằng 1.
        /// </summary>
        public static List<KeyValuePair<AllelePair, double>> LocusDistribution(string locusId, AllelePair a, AllelePair b)
        {
            LocusDef locus = LociCatalog.Get(locusId);
            var o = new List<KeyValuePair<AllelePair, double>>();
            foreach (string x in new[] { a.First, a.Second })
                foreach (string y in new[] { b.First, b.Second })
                {
                    AllelePair pair = NormalizePair(locus, x, y);
                    int i = o.FindIndex(e => e.Key.Equals(pair));
                    if (i < 0) o.Add(new KeyValuePair<AllelePair, double>(pair, 0.25));
                    else o[i] = new KeyValuePair<AllelePair, double>(pair, o[i].Value + 0.25);
                }
            return o;
        }

        /// <summary>
        /// Roll genotype con theo phân ly độc lập, mỗi locus 2 lần rng.Next() (bố rồi mẹ) theo thứ tự locus của loài con.
        /// Locus thiếu ở một bên bố mẹ thì bỏ qua — không tự tạo gene (rollOffspringGenotype).
        /// </summary>
        public static Genotype RollOffspringGenotype(IRng rng, Genotype a, Genotype b, IReadOnlyList<string> loci)
        {
            var o = new Genotype();
            foreach (string id in loci)
            {
                if (!a.TryGet(id, out AllelePair pa) || !b.TryGet(id, out AllelePair pb)) continue;
                LocusDef locus = LociCatalog.Get(id);
                string fromA = rng.Next() < 0.5 ? pa.First : pa.Second;
                string fromB = rng.Next() < 0.5 ? pb.First : pb.Second;
                o.Set(id, NormalizePair(locus, fromA, fromB));
            }
            return o;
        }
    }
}
