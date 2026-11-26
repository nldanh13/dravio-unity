using System;
using System.Collections.Generic;
using Dravio.Domain.Biology;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Economy;

namespace Dravio.Domain.Breeding
{
    /// <summary>Lý do từ chối cặp lai (BreedRejection của src/engine/genetics/breeding.ts), theo đúng thứ tự kiểm.</summary>
    public enum BreedRejection
    {
        [WireName("same-dragon")] SameDragon,
        [WireName("invalid-genotype")] InvalidGenotype,
        [WireName("released")] Released,
        [WireName("emergency-shelter")] EmergencyShelter,
        [WireName("not-adult")] NotAdult,
        [WireName("busy")] Busy,
        [WireName("cooldown")] Cooldown,
        [WireName("different-group")] DifferentGroup,
        [WireName("related")] Related,
        [WireName("no-candidates")] NoCandidates,
    }

    /// <summary>Preview cặp lai: query thuần, không tiêu RNG. UI và engine dùng chung (GDD trang 8, 32).</summary>
    public sealed class BreedPreview
    {
        internal BreedPreview(List<KeyValuePair<string, double>> species, List<KeyValuePair<string, List<KeyValuePair<AllelePair, double>>>> loci, long feeG)
        {
            Species = species; Loci = loci; FeeG = feeG;
        }
        /// <summary>Xác suất từng loài con, thứ tự theo độ hiếm rồi theo catalog; tổng = 1.</summary>
        public IReadOnlyList<KeyValuePair<string, double>> Species { get; }
        /// <summary>Phân phối genotype từng locus (theo locus của bố, chỉ locus cả hai bố mẹ có).</summary>
        public IReadOnlyList<KeyValuePair<string, List<KeyValuePair<AllelePair, double>>>> Loci { get; }
        public long FeeG { get; }
    }

    public readonly struct BreedOutcome
    {
        public BreedOutcome(string speciesId, Genotype genotype) { SpeciesId = speciesId; Genotype = genotype; }
        public string SpeciesId { get; }
        public Genotype Genotype { get; }
    }

    /// <summary>
    /// Luật lai tạo P0 (GDD 3.1 trang 8; ADR 0003; port nguyên văn src/engine/genetics/breeding.ts @ b33ec7a).
    /// checkBreed giữ nguyên bản TS (golden parity). Ngưỡng health ≥ 80 / mobility ≥ 80 / comfort ≥ 60 và "không có HealthCase mở" của ADR 0003
    /// được kiểm ở tầng lệnh từ U2.6 (<see cref="Hatchery.HealthBlock"/>), bằng dữ liệu vitals thật — không giả lập "khỏe".
    /// </summary>
    public static class BreedingRules
    {
        private static int Rank(Rarity r) => (int)r;

        private static bool Contains(IEnumerable<string> xs, string v)
        {
            if (xs == null) return false;
            foreach (string x in xs) if (x == v) return true;
            return false;
        }

        private static bool Intersects(IEnumerable<string> a, IEnumerable<string> b)
        {
            if (a == null || b == null) return false;
            foreach (string x in a) if (Contains(b, x)) return true;
            return false;
        }

        private static List<string> KnownRelatives(DragonState d)
        {
            var o = new List<string>();
            if (d.ParentIds != null) o.AddRange(d.ParentIds);
            if (d.GrandparentIds != null) o.AddRange(d.GrandparentIds);
            return o;
        }

        /// <summary>
        /// Cấm bố mẹ–con, anh chị em (chung cha/mẹ), chung ông bà và ông bà–cháu ĐÃ BIẾT (ADR 0003 #4). Không suy diễn từ dữ liệu thiếu:
        /// parentIds/grandparentIds null nghĩa là "chưa biết", không phải "không có họ hàng", nhưng cũng không bị coi là họ hàng.
        /// </summary>
        public static bool AreRelated(DragonState a, DragonState b)
        {
            if (Contains(a.ParentIds, b.Uid) || Contains(b.ParentIds, a.Uid)) return true;
            if (Intersects(a.ParentIds, b.ParentIds)) return true;
            if (Intersects(a.GrandparentIds, b.GrandparentIds)) return true;
            if (Contains(KnownRelatives(a), b.Uid) || Contains(KnownRelatives(b), a.Uid)) return true;
            return false;
        }

        /// <summary>Ứng viên loài con: cùng nhóm, nguyên tố ⊆ hợp nguyên tố bố mẹ; Epic cần một bố mẹ Rare+, Legendary cần cả hai Epic+.</summary>
        public static List<DragonSpecies> CandidateSpecies(DragonSpecies a, DragonSpecies b)
        {
            var o = new List<DragonSpecies>();
            if (a.Group != b.Group) return o;
            var union = new HashSet<ElementType>(a.Elements);
            union.UnionWith(b.Elements);
            int maxRank = Math.Max(Rank(a.Rarity), Rank(b.Rarity));
            int minRank = Math.Min(Rank(a.Rarity), Rank(b.Rarity));
            foreach (DragonSpecies s in SpeciesCatalog.All)
            {
                if (s.Group != a.Group) continue;
                bool subset = true;
                foreach (ElementType e in s.Elements) if (!union.Contains(e)) subset = false;
                if (!subset) continue;
                if (s.Rarity == Rarity.Epic && maxRank < Rank(Rarity.Rare)) continue;
                if (s.Rarity == Rarity.Legendary && minRank < Rank(Rarity.Epic)) continue;
                o.Add(s);
            }
            return o;
        }

        /// <summary>checkBreed: null = hợp lệ. Thứ tự kiểm giữ nguyên bản TS.</summary>
        public static BreedRejection? CheckBreed(DragonState a, DragonState b, long nowMs)
        {
            if (a.Uid == b.Uid) return BreedRejection.SameDragon;
            if (a.Location.Kind == LocationKind.Released || b.Location.Kind == LocationKind.Released) return BreedRejection.Released;
            if (a.Location.Kind == LocationKind.Shelter || b.Location.Kind == LocationKind.Shelter) return BreedRejection.EmergencyShelter;
            DragonSpecies sa = SpeciesCatalog.Get(a.SpeciesId), sb = SpeciesCatalog.Get(b.SpeciesId);
            if (EconomyRates.GrowthStageOf(a.XpSec, sa) != GrowthStage.Adult || EconomyRates.GrowthStageOf(b.XpSec, sb) != GrowthStage.Adult) return BreedRejection.NotAdult;
            if (a.Activity != DragonActivity.Idle || b.Activity != DragonActivity.Idle) return BreedRejection.Busy;
            if (a.Location.Kind == LocationKind.Care || b.Location.Kind == LocationKind.Care) return BreedRejection.Busy;
            if (a.BreedCooldownUntilMs > nowMs || b.BreedCooldownUntilMs > nowMs) return BreedRejection.Cooldown;
            if (sa.Group != sb.Group) return BreedRejection.DifferentGroup;
            if (AreRelated(a, b)) return BreedRejection.Related;
            if (CandidateSpecies(sa, sb).Count == 0) return BreedRejection.NoCandidates;
            if (!Genetics.IsValidGenotype(a.Genotype, sa.Loci) || !Genetics.IsValidGenotype(b.Genotype, sb.Loci)) return BreedRejection.InvalidGenotype;
            return null;
        }

        /// <summary>Chọn nhóm hiếm 60/30/9/1 sau khi bỏ nhóm rỗng, rồi chia đều trong nhóm.</summary>
        public static List<KeyValuePair<string, double>> SpeciesDistribution(DragonSpecies a, DragonSpecies b)
        {
            List<DragonSpecies> cands = CandidateSpecies(a, b);
            double total = 0;
            foreach (Rarity r in PackRolls.RarityOrder) if (cands.Exists(s => s.Rarity == r)) total += BreedingCatalog.OffspringWeight(r);
            var o = new List<KeyValuePair<string, double>>();
            foreach (Rarity r in PackRolls.RarityOrder)
            {
                List<DragonSpecies> group = cands.FindAll(s => s.Rarity == r);
                if (group.Count == 0) continue;
                double pRarity = BreedingCatalog.OffspringWeight(r) / total;
                foreach (DragonSpecies s in group) o.Add(new KeyValuePair<string, double>(s.Id, pRarity / group.Count));
            }
            return o;
        }

        /// <summary>Phí theo độ hiếm cao nhất của BỐ MẸ, biết trước khi roll (ADR 0003 #1).</summary>
        public static long BreedFeeG(DragonSpecies a, DragonSpecies b) => BreedingCatalog.FeeG((Rarity)Math.Max(Rank(a.Rarity), Rank(b.Rarity)));

        /// <summary>hatchSeconds = ceil(baseHatchSeconds × (1 − 0,1 × level)).</summary>
        public static long HatchSeconds(Rarity rarity, int hatchSpeedLevel) => (long)Math.Ceiling(BreedingCatalog.BaseHatchSec(rarity) * (1 - 0.1 * hatchSpeedLevel));

        public static BreedPreview PreviewBreed(DragonState a, DragonState b)
        {
            DragonSpecies sa = SpeciesCatalog.Get(a.SpeciesId), sb = SpeciesCatalog.Get(b.SpeciesId);
            var loci = new List<KeyValuePair<string, List<KeyValuePair<AllelePair, double>>>>();
            foreach (string id in sa.Loci)
                if (a.Genotype.TryGet(id, out AllelePair pa) && b.Genotype.TryGet(id, out AllelePair pb))
                    loci.Add(new KeyValuePair<string, List<KeyValuePair<AllelePair, double>>>(id, Genetics.LocusDistribution(id, pa, pb)));
            return new BreedPreview(SpeciesDistribution(sa, sb), loci, BreedFeeG(sa, sb));
        }

        /// <summary>queryBreedPreview: cặp không hợp lệ không có preview (không tiêu RNG, không trừ tiền).</summary>
        public static BreedPreview QueryBreedPreview(DragonState a, DragonState b, long nowMs, out BreedRejection? rejection)
        {
            rejection = CheckBreed(a, b, nowMs);
            return rejection == null ? PreviewBreed(a, b) : null;
        }

        /// <summary>Roll một lần khi bắt đầu lai: RNG lần lượt cho độ hiếm, loài, rồi 2 lần/locus của loài con.</summary>
        public static BreedOutcome RollBreed(IRng rng, DragonState a, DragonState b)
        {
            DragonSpecies sa = SpeciesCatalog.Get(a.SpeciesId), sb = SpeciesCatalog.Get(b.SpeciesId);
            List<KeyValuePair<string, double>> dist = SpeciesDistribution(sa, sb);
            if (dist.Count == 0) throw new DomainInvariantException("rollBreed called without candidates");
            var rarities = new List<KeyValuePair<Rarity, double>>();
            foreach (Rarity r in PackRolls.RarityOrder)
                if (dist.Exists(e => SpeciesCatalog.Get(e.Key).Rarity == r)) rarities.Add(new KeyValuePair<Rarity, double>(r, BreedingCatalog.OffspringWeight(r)));
            Rarity rarity = rng.PickWeighted(rarities);
            var ids = new List<string>();
            foreach (var e in dist) if (SpeciesCatalog.Get(e.Key).Rarity == rarity) ids.Add(e.Key);
            string speciesId = rng.PickUniform(ids);
            return new BreedOutcome(speciesId, Genetics.RollOffspringGenotype(rng, a.Genotype, b.Genotype, SpeciesCatalog.Get(speciesId).Loci));
        }
    }
}
