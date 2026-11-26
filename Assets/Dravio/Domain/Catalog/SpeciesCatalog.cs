using System;
using System.Collections.Generic;
using Dravio.Domain.Core;

namespace Dravio.Domain.Catalog
{
    /// <summary>Một loài rồng (src/types/game.ts DragonSpecies). Bất biến.</summary>
    public sealed class DragonSpecies
    {
        internal DragonSpecies(string id, ElementType[] elements, Rarity rarity, BreedingGroup group, Realm[] realms, string[] habitatTags,
            SpeciesBiology biology, string[] loci, bool packEligible, long baseRateMicroG, int growthTimeSec, int baseSellG, BodyStyle bodyStyle)
        {
            Id = id; Elements = Array.AsReadOnly(elements); Rarity = rarity; Group = group; Realms = Array.AsReadOnly(realms);
            HabitatTags = Array.AsReadOnly(habitatTags); Biology = biology; Loci = Array.AsReadOnly(loci); PackEligible = packEligible;
            BaseRateMicroG = baseRateMicroG; GrowthTimeSec = growthTimeSec; BaseSellG = baseSellG; BodyStyle = bodyStyle;
            AssetId = "dragon." + id;
        }

        /// <summary>ID ổn định v2. Không bao giờ đổi (save cũ tham chiếu).</summary>
        public string Id { get; }
        /// <summary>Một hoặc hai nguyên tố.</summary>
        public IReadOnlyList<ElementType> Elements { get; }
        public Rarity Rarity { get; }
        public BreedingGroup Group { get; }
        /// <summary>Realm loài có thể sống (cột "sinh cảnh" GDD trang 6).</summary>
        public IReadOnlyList<Realm> Realms { get; }
        /// <summary>'humid', 'hot', 'cold-water', 'sea'.</summary>
        public IReadOnlyList<string> HabitatTags { get; }
        /// <summary>Anatomy, habitatRules, palette, silhouette canonical G0.</summary>
        public SpeciesBiology Biology { get; }
        public IReadOnlyList<string> HabitatRules => Biology.HabitatRules;
        /// <summary>Locus P0: pigment, pattern, eye.</summary>
        public IReadOnlyList<string> Loci { get; }
        public bool PackEligible { get; }
        /// <summary>Vàng/giây cơ bản (microG/s) = Math.round(G/s × 1 000 000).</summary>
        public long BaseRateMicroG { get; }
        /// <summary>Thời gian trưởng thành (giây) = phút × 60.</summary>
        public int GrowthTimeSec { get; }
        public int BaseSellG { get; }
        public BodyStyle BodyStyle { get; }
        /// <summary>"dragon.&lt;id&gt;" — khóa asset trình bày, domain không nạp asset.</summary>
        public string AssetId { get; }
    }

    /// <summary>
    /// Catalog 16 loài (GDD 3.1 trang 6; src/data/dragons.ts). Giữ nguyên ID và chỉ số kinh tế baseline v2.
    /// Anatomy lấy từ dữ liệu G0 canonical theo ID; nhóm sinh sản được đối chiếu khi nạp catalog.
    /// </summary>
    public static class SpeciesCatalog
    {
        private static readonly string[] P0Loci = { "pigment", "pattern", "eye" };

        private sealed class Row
        {
            public string Id; public ElementType[] Elements; public Rarity Rarity; public BreedingGroup Group; public Realm[] Realms;
            public string[] HabitatTags = Array.Empty<string>();
            /// <summary>G/s như trong bảng GDD (số thực), quy đổi giống hệt TypeScript.</summary>
            public double Rate;
            /// <summary>Phút.</summary>
            public int GrowthMin;
            public int Sell; public BodyStyle Body; public bool PackEligible = true;
        }

        private static readonly ElementType Fire = ElementType.Fire, Water = ElementType.Water, Nature = ElementType.Nature, Sky = ElementType.Sky;
        private static readonly Realm Land = Realm.Land, Ocean = Realm.Ocean, SkyRealm = Realm.Sky;

        // Thứ tự và giá trị chép nguyên văn từ ROWS trong src/data/dragons.ts.
        private static readonly Row[] Rows =
        {
            new Row { Id = "emberling", Elements = new[] { Fire }, Rarity = Rarity.Common, Group = BreedingGroup.T, Realms = new[] { Land }, Rate = 0.025, GrowthMin = 5, Sell = 75, Body = BodyStyle.Round },
            new Row { Id = "droplet", Elements = new[] { Water }, Rarity = Rarity.Common, Group = BreedingGroup.A, Realms = new[] { Ocean, Land }, HabitatTags = new[] { "humid" }, Rate = 0.025, GrowthMin = 6, Sell = 75, Body = BodyStyle.Round },
            new Row { Id = "sprout", Elements = new[] { Nature }, Rarity = Rarity.Common, Group = BreedingGroup.T, Realms = new[] { Land }, Rate = 0.027, GrowthMin = 8, Sell = 80, Body = BodyStyle.Round },
            new Row { Id = "zephyr", Elements = new[] { Sky }, Rarity = Rarity.Common, Group = BreedingGroup.V, Realms = new[] { Land, SkyRealm }, Rate = 0.028, GrowthMin = 8, Sell = 85, Body = BodyStyle.Winged },
            new Row { Id = "steamdrake", Elements = new[] { Fire, Water }, Rarity = Rarity.Rare, Group = BreedingGroup.A, Realms = new[] { Ocean, Land }, HabitatTags = new[] { "humid" }, Rate = 0.07, GrowthMin = 25, Sell = 350, Body = BodyStyle.Serpent },
            new Row { Id = "magma", Elements = new[] { Fire, Nature }, Rarity = Rarity.Rare, Group = BreedingGroup.T, Realms = new[] { Land }, HabitatTags = new[] { "hot" }, Rate = 0.075, GrowthMin = 30, Sell = 380, Body = BodyStyle.Horned },
            new Row { Id = "sparkwing", Elements = new[] { Fire, Sky }, Rarity = Rarity.Rare, Group = BreedingGroup.V, Realms = new[] { Land, SkyRealm }, Rate = 0.08, GrowthMin = 35, Sell = 400, Body = BodyStyle.Winged },
            new Row { Id = "frostfin", Elements = new[] { Water, Sky }, Rarity = Rarity.Rare, Group = BreedingGroup.A, Realms = new[] { Ocean }, HabitatTags = new[] { "cold-water" }, Rate = 0.075, GrowthMin = 30, Sell = 380, Body = BodyStyle.Winged },
            new Row { Id = "mossback", Elements = new[] { Water, Nature }, Rarity = Rarity.Rare, Group = BreedingGroup.A, Realms = new[] { Ocean, Land }, HabitatTags = new[] { "humid" }, Rate = 0.072, GrowthMin = 25, Sell = 360, Body = BodyStyle.Round },
            new Row { Id = "blossom", Elements = new[] { Nature, Sky }, Rarity = Rarity.Rare, Group = BreedingGroup.V, Realms = new[] { Land, SkyRealm }, Rate = 0.08, GrowthMin = 35, Sell = 400, Body = BodyStyle.Winged },
            new Row { Id = "solaris", Elements = new[] { Fire, Sky }, Rarity = Rarity.Epic, Group = BreedingGroup.V, Realms = new[] { Land, SkyRealm }, Rate = 0.18, GrowthMin = 120, Sell = 1800, Body = BodyStyle.Winged },
            new Row { Id = "leviathan", Elements = new[] { Water, Nature }, Rarity = Rarity.Epic, Group = BreedingGroup.A, Realms = new[] { Ocean }, HabitatTags = new[] { "sea" }, Rate = 0.2, GrowthMin = 150, Sell = 2000, Body = BodyStyle.Serpent },
            new Row { Id = "amberhorn", Elements = new[] { Fire, Nature }, Rarity = Rarity.Epic, Group = BreedingGroup.T, Realms = new[] { Land }, Rate = 0.2, GrowthMin = 150, Sell = 2000, Body = BodyStyle.Horned, PackEligible = false },
            new Row { Id = "aurorawing", Elements = new[] { Water, Sky }, Rarity = Rarity.Epic, Group = BreedingGroup.V, Realms = new[] { Land, SkyRealm }, Rate = 0.22, GrowthMin = 180, Sell = 2200, Body = BodyStyle.Winged, PackEligible = false },
            new Row { Id = "celestia", Elements = new[] { Fire, Sky }, Rarity = Rarity.Legendary, Group = BreedingGroup.V, Realms = new[] { Land, SkyRealm }, Rate = 0.5, GrowthMin = 360, Sell = 10000, Body = BodyStyle.Winged },
            new Row { Id = "gaia", Elements = new[] { Water, Nature }, Rarity = Rarity.Legendary, Group = BreedingGroup.A, Realms = new[] { Ocean, Land }, HabitatTags = new[] { "humid" }, Rate = 0.55, GrowthMin = 480, Sell = 11000, Body = BodyStyle.Horned },
        };

        private static readonly Dictionary<string, SpeciesBiology> BiologyById = BuildBiologyIndex();
        private static readonly DragonSpecies[] AllSpecies = Build();
        private static readonly Dictionary<string, DragonSpecies> ById = Index(AllSpecies);

        /// <summary>16 loài theo đúng thứ tự catalog TypeScript.</summary>
        public static IReadOnlyList<DragonSpecies> All => AllSpecies;

        public static bool TryGet(string id, out DragonSpecies species)
        {
            if (id != null && ById.TryGetValue(id, out species)) return true;
            species = null;
            return false;
        }

        /// <summary>getSpecies: ID lạ là vi phạm bất biến (dữ liệu hỏng), không phải lỗi lệnh.</summary>
        public static DragonSpecies Get(string id)
        {
            if (TryGet(id, out DragonSpecies s)) return s;
            throw new DomainInvariantException("Unknown species id: " + id);
        }

        /// <summary>getBiology: dữ liệu G0 theo ID.</summary>
        public static SpeciesBiology GetBiology(string id)
        {
            if (id != null && BiologyById.TryGetValue(id, out SpeciesBiology b)) return b;
            throw new DomainInvariantException("Unknown G0 species: " + id);
        }

        public static IReadOnlyCollection<SpeciesBiology> AllBiology => BiologyById.Values;

        private static Dictionary<string, SpeciesBiology> BuildBiologyIndex()
        {
            var d = new Dictionary<string, SpeciesBiology>(StringComparer.Ordinal);
            foreach (var b in G0BiologyData.Species) d.Add(b.Id, b);
            return d;
        }

        private static DragonSpecies[] Build()
        {
            var list = new DragonSpecies[Rows.Length];
            for (int i = 0; i < Rows.Length; i++)
            {
                Row r = Rows[i];
                SpeciesBiology b = GetBiology(r.Id);
                if (b.Group != r.Group)
                    throw new DomainInvariantException("Species " + r.Id + ": group " + r.Group.ToWire() + " in catalog disagrees with G0 biology " + b.Group.ToWire());
                list[i] = new DragonSpecies(r.Id, r.Elements, r.Rarity, r.Group, r.Realms, r.HabitatTags, b, (string[])P0Loci.Clone(), r.PackEligible,
                    baseRateMicroG: (long)JsCompat.Round(r.Rate * 1_000_000), growthTimeSec: r.GrowthMin * 60, baseSellG: r.Sell, bodyStyle: r.Body);
            }
            return list;
        }

        private static Dictionary<string, DragonSpecies> Index(DragonSpecies[] all)
        {
            var d = new Dictionary<string, DragonSpecies>(StringComparer.Ordinal);
            foreach (var s in all) d.Add(s.Id, s);
            return d;
        }
    }
}
