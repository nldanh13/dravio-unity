using System;
using System.Collections.Generic;
using Dravio.Domain.Core;

namespace Dravio.Domain.Catalog
{
    /// <summary>Phiên bản schema và nội dung (src/types/state.ts, src/data/content.ts).</summary>
    public static class ContentInfo
    {
        /// <summary>
        /// Dravio Save Schema hiện tại của TypeScript b33ec7a: 4 (v1 ADR 0007, v2 discoveries ADR 0009,
        /// v4 tutorial/pendingPack/achievements ADR 0013; số 3 được dành riêng, không dùng).
        /// </summary>
        public const int SchemaVersion = 4;

        /// <summary>Phiên bản catalog nội dung (G0 Decision Pack 1.0). Dùng làm seed founder ổn định.</summary>
        public const string ContentVersion = "g0-1.0";

        /// <summary>Commit TypeScript gốc mà U2.1 đối chiếu.</summary>
        public const string ReferenceCommit = "b33ec7a3dc98b4a74857fe92d143475028fc0d96";
    }

    /// <summary>7 microhabitat canonical trong 3 zone (HABITAT_PROFILES).</summary>
    public static class HabitatCatalog
    {
        private static readonly Dictionary<string, HabitatProfile> ById = Build();

        public static IReadOnlyList<HabitatProfile> All => G0BiologyData.Habitats;

        public static bool TryGet(string habitatId, out HabitatProfile profile)
        {
            if (habitatId != null && ById.TryGetValue(habitatId, out profile)) return true;
            profile = null;
            return false;
        }

        public static bool Contains(string habitatId) => habitatId != null && ById.ContainsKey(habitatId);

        /// <summary>habitatZone: zone chứa microhabitat, null khi ID lạ.</summary>
        public static string ZoneOf(string habitatId) => TryGet(habitatId, out HabitatProfile p) ? p.ZoneId : null;

        private static Dictionary<string, HabitatProfile> Build()
        {
            var d = new Dictionary<string, HabitatProfile>(StringComparer.Ordinal);
            foreach (var h in G0BiologyData.Habitats) d.Add(h.Id, h);
            return d;
        }
    }

    /// <summary>Zone vật lý P0 (src/data/worldGraph.ts). Đồ thị di chuyển chưa port ở U2.1.</summary>
    public static class WorldCatalog
    {
        public static readonly IReadOnlyList<string> ZoneIds = Array.AsReadOnly(new[] { "origin", "ocean", "sky" });

        /// <summary>Sức chứa vật lý mỗi zone ở P0.</summary>
        public const int P0ZoneCapacity = 5;

        public static bool IsZoneId(string id) => id == "origin" || id == "ocean" || id == "sky";
    }

    /// <summary>Định nghĩa locus P0 (src/data/genetics.ts). Kiểu hình chưa port ở U2.1.</summary>
    public sealed class LocusDef
    {
        internal LocusDef(string id, string dominant, string recessive, bool incompleteDominance, double founderDominantFreq)
        {
            Id = id; Dominant = dominant; Recessive = recessive; IncompleteDominance = incompleteDominance; FounderDominantFreq = founderDominantFreq;
        }

        public string Id { get; }
        /// <summary>Allele viết hoa là trội.</summary>
        public string Dominant { get; }
        public string Recessive { get; }
        public bool IncompleteDominance { get; }
        /// <summary>Tần số allele trội cho rồng founder (50 %).</summary>
        public double FounderDominantFreq { get; }

        public bool HasAllele(string allele) => allele == Dominant || allele == Recessive;
    }

    public static class LociCatalog
    {
        public static readonly IReadOnlyList<LocusDef> All = Array.AsReadOnly(new[]
        {
            new LocusDef("pigment", "R", "r", incompleteDominance: false, founderDominantFreq: 0.5),
            new LocusDef("pattern", "P", "p", incompleteDominance: true, founderDominantFreq: 0.5),
            new LocusDef("eye", "B", "b", incompleteDominance: false, founderDominantFreq: 0.5),
        });

        public static readonly IReadOnlyList<string> LocusIds = Array.AsReadOnly(new[] { "pigment", "pattern", "eye" });

        public static bool TryGet(string id, out LocusDef locus)
        {
            foreach (var l in All) if (l.Id == id) { locus = l; return true; }
            locus = null;
            return false;
        }

        public static LocusDef Get(string id)
        {
            if (TryGet(id, out LocusDef l)) return l;
            throw new DomainInvariantException("Unknown locus id: " + id);
        }
    }

    /// <summary>Hằng số cho ăn, gắn bó, đặt tên, idempotency, offline (src/data/care.ts).</summary>
    public static class CareRules
    {
        /// <summary>Khoảng cách tối thiểu giữa hai lần thả thức ăn.</summary>
        public const long FoodSpawnIntervalMs = 800;
        /// <summary>Thời gian tồn tại của viên thức ăn.</summary>
        public const long FoodTtlMs = 12_000;
        /// <summary>Mỗi rồng nhận XP từ ăn tối đa một lần mỗi 8 s.</summary>
        public const long FeedXpCooldownMs = 8_000;
        /// <summary>Số viên đồng thời tối đa = 5 + cấp foodCooldownLevel.</summary>
        public const int FoodMaxBase = 5;
        public const long PetCooldownMs = 30_000;
        public const int BondMax = 100;
        /// <summary>Vuốt ve +1, ăn hợp lệ +1, tối đa +10 / ngày UTC / rồng.</summary>
        public const int BondDailyCap = 10;
        /// <summary>Tên rồng 1–24 grapheme sau khi trim (ADR 0004).</summary>
        public const int NicknameMaxGraphemes = 24;
        /// <summary>Số requestId gần nhất được nhớ để chống xử lý trùng.</summary>
        public const int ProcessedRequestLimit = 200;
        /// <summary>Offline &gt; 60 s mới tạo receipt (GDD Phụ lục G).</summary>
        public const long OfflineReceiptMinMs = 60_000;

        public static readonly IReadOnlyList<DietId> DietIds = Array.AsReadOnly(new[] { DietId.Fruit, DietId.Leaf, DietId.Aquatic, DietId.Aether });

        /// <summary>XP ăn = 8 + 6 × cấp foodQualityLevel (giây).</summary>
        public static int FeedXpSec(int foodQualityLevel) => 8 + 6 * foodQualityLevel;
    }

    /// <summary>Định nghĩa nâng cấp (Phụ lục E). Chỉ dùng cho validation và công thức giá ở U2.1; lệnh mua chưa port.</summary>
    public sealed class UpgradeDef
    {
        internal UpgradeDef(string id, int baseG, double scale, int max) { Id = id; BaseG = baseG; Scale = scale; Max = max; }
        /// <summary>Tên trường trong UpgradesState, ví dụ "islandCapacityLevel".</summary>
        public string Id { get; }
        public int BaseG { get; }
        public double Scale { get; }
        public int Max { get; }

        public int LevelOf(UpgradesState u)
        {
            switch (Id)
            {
                case "islandCapacityLevel": return u.IslandCapacityLevel;
                case "foodQualityLevel": return u.FoodQualityLevel;
                case "foodCooldownLevel": return u.FoodCooldownLevel;
                case "mutationLuckLevel": return u.MutationLuckLevel;
                case "hatchSpeedLevel": return u.HatchSpeedLevel;
                default: throw new DomainInvariantException("Unknown upgrade id: " + Id);
            }
        }
    }

    /// <summary>Bảng kinh tế baseline v2 (GDD 3.1 trang 15; src/data/economy.ts). Là giả thuyết cân bằng, không tự chỉnh.</summary>
    public static class EconomyTables
    {
        public const long MicroPerGold = MicroGold.MicroPerGold;

        public static double StageRateMult(GrowthStage s) => s == GrowthStage.Baby ? 0.4 : s == GrowthStage.Juvenile ? 0.75 : 1;

        public static double StageSellMult(GrowthStage s) => s == GrowthStage.Baby ? 0.35 : s == GrowthStage.Juvenile ? 0.65 : 1;

        public static double MutationRateMult(MutationType m)
        {
            switch (m)
            {
                case MutationType.Normal: return 1;
                case MutationType.Crystal: return 1.6;
                case MutationType.Golden: return 2.8;
                case MutationType.Prismatic: return 5;
                default: throw new DomainInvariantException("Unknown mutation " + (int)m);
            }
        }

        public static double MutationSellMult(MutationType m)
        {
            switch (m)
            {
                case MutationType.Normal: return 1;
                case MutationType.Crystal: return 2.2;
                case MutationType.Golden: return 4.5;
                case MutationType.Prismatic: return 10;
                default: throw new DomainInvariantException("Unknown mutation " + (int)m);
            }
        }

        /// <summary>Lần thứ 200 liên tiếp không Prismatic bảo đảm Prismatic. Logic roll chưa port; dùng cho validation pityCount.</summary>
        public const int PrismaticPity = 200;

        /// <summary>Hệ số vàng offline 75 % (dạng số thực, chỉ dùng trong offlineGoldMicro; engine dùng phân số 3/4).</summary>
        public const double OfflineGoldFactor = 0.75;

        public static readonly IReadOnlyList<UpgradeDef> Upgrades = Array.AsReadOnly(new[]
        {
            new UpgradeDef("islandCapacityLevel", 500, 1.85, 10),
            new UpgradeDef("foodQualityLevel", 200, 1.75, 10),
            new UpgradeDef("foodCooldownLevel", 250, 1.8, 8),
            new UpgradeDef("mutationLuckLevel", 1500, 2.2, 8),
            new UpgradeDef("hatchSpeedLevel", 600, 1.9, 6),
        });
    }

    /// <summary>Gói trứng (src/data/economy.ts PACKS): giá G và tỷ lệ % theo độ hiếm. U2.4 chỉ dùng giá gói starter; roll gói chưa port.</summary>
    public sealed class PackDef
    {
        internal PackDef(string id, long priceG, int common, int rare, int epic, int legendary)
        {
            Id = id; PriceG = priceG; OddsCommon = common; OddsRare = rare; OddsEpic = epic; OddsLegendary = legendary;
        }
        public string Id { get; }
        public long PriceG { get; }
        public int OddsCommon { get; }
        public int OddsRare { get; }
        public int OddsEpic { get; }
        public int OddsLegendary { get; }
    }

    public static class PackCatalog
    {
        public static readonly IReadOnlyList<PackDef> All = Array.AsReadOnly(new[]
        {
            new PackDef("starter", 100, 90, 10, 0, 0),
            new PackDef("elemental", 900, 48, 45, 7, 0),
            new PackDef("mystic", 6000, 15, 50, 32, 3),
            new PackDef("astral", 30000, 0, 28, 55, 17),
        });

        public static PackDef Get(string id)
        {
            foreach (PackDef p in All) if (p.Id == id) return p;
            throw new DomainInvariantException("Unknown pack id: " + id);
        }
    }

    /// <summary>Quy tắc mở microhabitat Khởi Nguyên (src/data/habitatUnlocks.ts; ADR 0009).</summary>
    public sealed class HabitatUnlockRule
    {
        internal HabitatUnlockRule(string habitatId, bool isDefault, string requires) { HabitatId = habitatId; IsDefault = isDefault; Requires = requires; }
        public string HabitatId { get; }
        /// <summary>Mở sẵn trong save mới.</summary>
        public bool IsDefault { get; }
        /// <summary>Khám phá cần có, null nếu không cần.</summary>
        public string Requires { get; }
    }

    /// <summary>
    /// Danh mục ID ổn định của tiến trình (tutorial, thành tựu, khám phá) — chỉ để kiểm tra dữ liệu save ở U2.1.
    /// Điều kiện hoàn thành bước hướng dẫn và thưởng thành tựu chưa port (U4).
    /// </summary>
    public static class ProgressCatalog
    {
        public static readonly IReadOnlyList<string> DiscoveryIds = Array.AsReadOnly(new[] { "DISC_EMBERLING_THERMAL", "DISC_AQUATIC_HABITAT" });

        public static readonly IReadOnlyList<HabitatUnlockRule> HabitatUnlockRules = Array.AsReadOnly(new[]
        {
            new HabitatUnlockRule("origin_meadow", true, null),
            new HabitatUnlockRule("origin_warm_rocks", false, "DISC_EMBERLING_THERMAL"),
            new HabitatUnlockRule("origin_pondbank", false, "DISC_AQUATIC_HABITAT"),
        });

        public static readonly IReadOnlyList<string> TutorialStepIds = Array.AsReadOnly(new[] { "TUT_01", "TUT_02", "TUT_03", "TUT_04", "TUT_05", "TUT_06", "TUT_07", "TUT_08" });

        /// <summary>12 achievement ID v2 (GDD Phụ lục F), theo thứ tự ACHIEVEMENT_IDS.</summary>
        public static readonly IReadOnlyList<string> AchievementIds = Array.AsReadOnly(new[]
        {
            "ACH_FIRST_HATCH", "ACH_FIRST_ADULT", "ACH_FIRST_BREED", "ACH_MUTATION_CRYSTAL", "ACH_MUTATION_GOLDEN", "ACH_MUTATION_PRISMATIC",
            "ACH_LEGENDARY", "ACH_GOLD_10K", "ACH_GOLD_250K", "ACH_DECOR_ALL", "ACH_CODEX_16", "ACH_CODEX_MASTER",
        });

        /// <summary>Thưởng (G) của 12 thành tựu v2 (GDD Phụ lục F; ACHIEVEMENT_REWARD_G), theo thứ tự AchievementIds.</summary>
        public static long AchievementRewardG(string id)
        {
            switch (id)
            {
                case "ACH_FIRST_HATCH": return 50;
                case "ACH_FIRST_ADULT": return 100;
                case "ACH_FIRST_BREED": return 150;
                case "ACH_MUTATION_CRYSTAL": return 100;
                case "ACH_MUTATION_GOLDEN": return 300;
                case "ACH_MUTATION_PRISMATIC": return 1000;
                case "ACH_LEGENDARY": return 2000;
                case "ACH_GOLD_10K": return 300;
                case "ACH_GOLD_250K": return 2500;
                case "ACH_DECOR_ALL": return 1000;
                case "ACH_CODEX_16": return 2000;
                case "ACH_CODEX_MASTER": return 3000;
                default: throw new DomainInvariantException("Unknown achievement id: " + id);
            }
        }

        /// <summary>TUT_01 "quan sát Emberling": thời gian tối thiểu kể từ khi bắt đầu hướng dẫn (đề xuất ADR 0013).</summary>
        public const long TutorialObserveMs = 15_000;

        /// <summary>Gói Starter hướng dẫn (GDD trang 4, 15; S01): gói starter, Sproutling (`sprout`), Normal, không tính pity.</summary>
        public const string TutorialPackId = "starter";
        public const string TutorialPackSpeciesId = "sprout";
        public static readonly MutationType TutorialPackMutation = MutationType.Normal;

        public static bool IsDiscoveryId(string id) => Contains(DiscoveryIds, id);
        public static bool IsAchievementId(string id) => Contains(AchievementIds, id);

        public static HabitatUnlockRule UnlockRuleFor(string habitatId)
        {
            foreach (var r in HabitatUnlockRules) if (r.HabitatId == habitatId) return r;
            return null;
        }

        private static bool Contains(IReadOnlyList<string> list, string id)
        {
            if (id == null) return false;
            foreach (var x in list) if (x == id) return true;
            return false;
        }
    }
}
