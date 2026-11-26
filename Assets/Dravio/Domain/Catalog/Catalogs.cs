using System;
using System.Collections.Generic;
using Dravio.Domain.Core;

namespace Dravio.Domain.Catalog
{
    /// <summary>Phiên bản schema và nội dung (src/types/state.ts, src/data/content.ts).</summary>
    public static class ContentInfo
    {
        /// <summary>
        /// Dravio Save Schema của bản Unity: 7. TypeScript b33ec7a dừng ở 4 (v1 ADR 0007, v2 discoveries ADR 0009,
        /// v4 tutorial/pendingPack/achievements ADR 0013; số 3 được dành riêng). v5 (U2.5, implementation mới, không có ở TS)
        /// thêm breedingNest (GDD Phụ lục C.2); v6 (U2.7, cũng chỉ ở Unity) thêm codex; v7 (U2.8) thêm expedition; v8 (U2.6) thêm vitals, healthCases, disasters, hazard; v9 (U2.10, ADR 0204) thêm packDaily, sanctuaryProjectsBuilt, research; v10 (U2.11, ADR 0206) thêm completedSideQuests, expeditionsClaimed, relations. <see cref="TsSchemaVersion"/> là phiên bản cuối có parity với TS.
        /// </summary>
        public const int SchemaVersion = 10;

        /// <summary>Schema cuối cùng bản TypeScript b33ec7a đọc/ghi được.</summary>
        public const int TsSchemaVersion = 4;

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

        /// <summary>Vị trí trong ZoneIds; −1 nếu lạ.</summary>
        public static int IndexOfZone(string id)
        {
            for (int i = 0; i < ZoneIds.Count; i++) if (ZoneIds[i] == id) return i;
            return -1;
        }
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

        /// <summary>Lần thứ 200 liên tiếp không Prismatic bảo đảm Prismatic (GDD trang 15; Economy.Mutation.Roll).</summary>
        public const int PrismaticPity = 200;

        /// <summary>Phần chia biến thể hiếm 11 : 3,5 : 0,5 trên 15 (MUTATION_SHARE).</summary>
        public const double ShareCrystal = 11.0 / 15, ShareGolden = 3.5 / 15, SharePrismatic = 0.5 / 15;

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

    /// <summary>
    /// Lai tạo và tổ ấp (GDD 3.1 trang 8; ADR 0003 #1, #2; src/data/breeding.ts). Trọng số con 60/30/9/1, phí theo độ hiếm cao nhất
    /// của bố mẹ 80/350/1500/5000 G, ấp theo độ hiếm con 5/15/60/180 phút, cooldown bố mẹ 10 phút.
    /// Ba hằng tap cuối là luật GDD trang 32 / ADR 0003 cho tapEgg — bản TS b33ec7a chưa có dữ liệu này.
    /// </summary>
    public static class BreedingCatalog
    {
        public static int OffspringWeight(Rarity r) => r == Rarity.Common ? 60 : r == Rarity.Rare ? 30 : r == Rarity.Epic ? 9 : 1;
        public static long FeeG(Rarity r) => r == Rarity.Common ? 80 : r == Rarity.Rare ? 350 : r == Rarity.Epic ? 1500 : 5000;
        public static int BaseHatchSec(Rarity r) => r == Rarity.Common ? 5 * 60 : r == Rarity.Rare ? 15 * 60 : r == Rarity.Epic ? 60 * 60 : 180 * 60;
        public const long ParentCooldownMs = 10 * 60 * 1000;
        /// <summary>Mỗi lần gõ tổ giảm 3 s (GDD trang 32).</summary>
        public const long TapReduceMs = 3000;
        /// <summary>Hai lần gõ cách nhau ít nhất 2 s.</summary>
        public const long TapMinIntervalMs = 2000;
        /// <summary>Tổng giảm do gõ ≤ 10 % thời lượng sau nâng cấp.</summary>
        public const double TapMaxShare = 0.10;
        /// <summary>Một tổ ấp trong P0.</summary>
        public const int NestSlots = 1;
    }

    /// <summary>
    /// Giới hạn kho (GDD 3.1 Phụ lục C: "Giới hạn kho đề xuất 500 rồng (v2), pending không mất khi đầy"). Bản TS b33ec7a chưa dùng.
    /// Đây là con số GDD gọi là đề xuất; ghi trong danh sách quyết định cần xác nhận (README U2.5).
    /// </summary>
    public static class StorageCatalog
    {
        public const int MaxDragons = 500;
    }

    /// <summary>
    /// Trang trí (GDD 3.1 Phụ lục E, giữ ID và giá v2). "Mỗi món một bản sao theo khu; bật/tắt miễn phí, không bán lại; 6 vị trí định sẵn P0;
    /// chỉ đặt ở môi trường hợp lệ; hiệu ứng sinh cảnh là đề xuất, KHÔNG cộng vàng." TS b33ec7a không có catalog này — implementation mới.
    /// GDD không ghi môi trường hợp lệ của từng món nên mọi zone đã mở đều nhận được (ghi trong danh sách quyết định còn thiếu).
    /// </summary>
    public sealed class DecorationDef
    {
        internal DecorationDef(string id, string nameVi, long priceG, string effectVi) { Id = id; NameVi = nameVi; PriceG = priceG; EffectVi = effectVi; Effects = DecorationEffects.None; }
        public string Id { get; }
        public string NameVi { get; }
        public long PriceG { get; }
        /// <summary>Hiệu ứng hình ảnh đề xuất (lớp trình bày); không có hiệu ứng kinh tế.</summary>
        public string EffectVi { get; }
        /// <summary>Hiệu ứng gameplay — MVP luôn 0 (ADR U2.8-0201). Engine chưa đọc; bật sau cần ADR + mô phỏng kinh tế.</summary>
        public DecorationEffects Effects { get; }
    }

    /// <summary>Trường cấu hình hiệu ứng trang trí để mở rộng sau. Mọi giá trị MVP = 0: không vàng, XP, Health/Mobility/Comfort hay sức chứa.</summary>
    public sealed class DecorationEffects
    {
        public static readonly DecorationEffects None = new DecorationEffects();
        public int GoldPermille { get; }
        public int XpPermille { get; }
        public int Health { get; }
        public int Mobility { get; }
        public int Comfort { get; }
        public int Capacity { get; }
        public bool IsZero => GoldPermille == 0 && XpPermille == 0 && Health == 0 && Mobility == 0 && Comfort == 0 && Capacity == 0;
    }

    public static class DecorationCatalog
    {
        public static readonly IReadOnlyList<DecorationDef> All = Array.AsReadOnly(new[]
        {
            new DecorationDef("cherry_tree", "Cây Anh Đào", 350, "Cánh hoa, điểm nghỉ"),
            new DecorationDef("crystal_pond", "Hồ Pha Lê", 1200, "Sóng nhẹ, tương tác nước"),
            new DecorationDef("sky_lantern", "Đèn Lồng Bay", 2500, "Ánh sáng ấm"),
            new DecorationDef("ancient_gate", "Cổng Đá Cổ", 6000, "Rune phát sáng"),
            new DecorationDef("glow_mushroom", "Nấm Phát Sáng", 12000, "Đom đóm"),
            new DecorationDef("star_observatory", "Đài Quan Sát Sao", 25000, "Vệt sao; công trình chức năng P1 riêng"),
        });

        public static bool TryGet(string id, out DecorationDef def)
        {
            foreach (DecorationDef d in All) if (d.Id == id) { def = d; return true; }
            def = null;
            return false;
        }

        /// <summary>Vị trí cố định mỗi khu P0 (= số món, mỗi món một bản sao/khu).</summary>
        public const int SlotsPerZone = 6;
    }

    /// <summary>Điều kiện sinh học của một tuyến thám hiểm (GDD tr. 22 "1 Adult sống đất / bơi phù hợp / bay khỏe").</summary>
    public enum ExpeditionRequirement { LandDweller, Swimmer, Flyer }

    /// <summary>
    /// Tuyến thám hiểm P0 (GDD 3.1 trang 22; ADR U2.8-0201 do chủ dự án duyệt). TS b33ec7a không có — implementation mới.
    /// 15/30/45 phút, tổng 2–4 đơn vị vật liệu, không phí, tỷ lệ thành công MVP 100 % (trường SuccessPermille có sẵn để mở rộng, chưa kích hoạt).
    /// ID tuyến ổn định, không phụ thuộc tên hiển thị. Tuyến chưa có bảng thưởng được duyệt (Materials rỗng) bị khóa.
    /// </summary>
    public sealed class ExpeditionRoute
    {
        internal ExpeditionRoute(string id, string nameVi, string zoneId, int minutes, ExpeditionRequirement requirement, string[] materials, int minQty, int maxQty)
        {
            Id = id; NameVi = nameVi; ZoneId = zoneId; DurationMs = minutes * 60_000L; Requirement = requirement; Materials = Array.AsReadOnly(materials); MinQty = minQty; MaxQty = maxQty;
            SuccessPermille = 1000;
        }
        public string Id { get; }
        public string NameVi { get; }
        /// <summary>Thế giới của tuyến; tuyến chỉ mở khi zone này đã mở (không vượt khóa thế giới).</summary>
        public string ZoneId { get; }
        public long DurationMs { get; }
        public ExpeditionRequirement Requirement { get; }
        /// <summary>Vật liệu có thể nhận (mỗi đơn vị chọn đều trong danh sách).</summary>
        public IReadOnlyList<string> Materials { get; }
        public int MinQty { get; }
        public int MaxQty { get; }
        /// <summary>Tỷ lệ thành công ‰. MVP = 1000 (ADR U2.8-0201): mọi chuyến đủ điều kiện đều thành công, không chết rồng, không mất vật phẩm.
        /// Engine CHƯA đọc trường này; mở rộng thất bại sau cần ADR mới.</summary>
        public int SuccessPermille { get; }
        /// <summary>Có bảng thưởng đã duyệt chưa. false → tuyến bị khóa (route-rewards-undefined).</summary>
        public bool RewardsDefined => Materials.Count > 0;
    }

    /// <summary>Vật liệu chuẩn (ADR U2.8-0201). Chỉ vật liệu có nguồn thu thập được quy định mới Active; chưa có công thức dùng.</summary>
    public sealed class MaterialDef
    {
        internal MaterialDef(string id, string nameVi, bool active) { Id = id; NameVi = nameVi; Active = active; }
        public string Id { get; }
        public string NameVi { get; }
        /// <summary>Có nguồn thu thập đã quy định (tuyến thám hiểm). stone chưa có nguồn → chưa kích hoạt.</summary>
        public bool Active { get; }
    }

    public static class ExpeditionCatalog
    {
        public static readonly IReadOnlyList<ExpeditionRoute> Routes = Array.AsReadOnly(new[]
        {
            new ExpeditionRoute("origin_forest", "Rừng Khởi Nguyên", "origin", 15, ExpeditionRequirement.LandDweller, new[] { "wood", "seed" }, 2, 4),
            // GDD "ngọc/rêu biển" → crystal/coral (chủ dự án duyệt).
            new ExpeditionRoute("ocean_reef", "Rạn Thủy Vực", "ocean", 30, ExpeditionRequirement.Swimmer, new[] { "crystal", "coral" }, 2, 4),
            // GDD "bụi sao" không có ID chuẩn: bảng thưởng TBD → tuyến khóa (chủ dự án duyệt).
            new ExpeditionRoute("sky_cloud", "Sân mây Thiên Giới", "sky", 45, ExpeditionRequirement.Flyer, new string[0], 2, 4),
        });

        public static readonly IReadOnlyList<MaterialDef> Materials = Array.AsReadOnly(new[]
        {
            new MaterialDef("wood", "Gỗ", true),
            new MaterialDef("seed", "Hạt giống", true),
            new MaterialDef("stone", "Đá", false),
            new MaterialDef("crystal", "Pha lê", true),
            new MaterialDef("coral", "San hô", true),
        });

        /// <summary>ID vật liệu chuẩn theo thứ tự catalog (cả vật liệu chưa kích hoạt).</summary>
        public static readonly IReadOnlyList<string> MaterialIds = Array.AsReadOnly(new[] { "wood", "seed", "stone", "crystal", "coral" });

        /// <summary>"Một slot thám hiểm chung P0".</summary>
        public const int Slots = 1;

        public static bool TryGet(string id, out ExpeditionRoute route)
        {
            foreach (ExpeditionRoute r in Routes) if (r.Id == id) { route = r; return true; }
            route = null;
            return false;
        }
    }

    /// <summary>Nhiệm vụ khám phá (ADR U2.8-0201). Nội dung/mục tiêu CHƯA xác định (TBD): engine chưa tự hoàn thành nhiệm vụ nào.</summary>
    public sealed class QuestDef
    {
        internal QuestDef(string id, string nameVi, string objectiveTbd) { Id = id; NameVi = nameVi; Objective = objectiveTbd; }
        public string Id { get; }
        public string NameVi { get; }
        /// <summary>"TBD" khi chưa có thiết kế mục tiêu.</summary>
        public string Objective { get; }
        public bool ObjectiveDefined => Objective != "TBD";
    }

    /// <summary>Điều kiện mở một thế giới và trứng cố định trao một lần (ADR U2.8-0201). Không điều kiện cấp độ, không chi phí vàng.</summary>
    public sealed class ZoneUnlockRule
    {
        internal ZoneUnlockRule(string zoneId, bool tutorial, bool originHabitats, string requiresZone, string quest, string rewardId, string eggSpecies)
        {
            ZoneId = zoneId; RequiresTutorialCompleted = tutorial; RequiresAllOriginHabitats = originHabitats; RequiresZone = requiresZone; RequiresQuest = quest;
            RewardId = rewardId; EggSpeciesId = eggSpecies;
        }
        public string ZoneId { get; }
        public bool RequiresTutorialCompleted { get; }
        public bool RequiresAllOriginHabitats { get; }
        public string RequiresZone { get; }
        public string RequiresQuest { get; }
        public string RewardId { get; }
        public string EggSpeciesId { get; }
        /// <summary>Trứng thưởng luôn Normal, không tính pity.</summary>
        public MutationType EggMutation => MutationType.Normal;
    }

    public static class WorldUnlockCatalog
    {
        public static readonly IReadOnlyList<QuestDef> Quests = Array.AsReadOnly(new[]
        {
            new QuestDef("QUEST_AQUATIC_EXPLORATION", "Khám phá thủy sinh", "TBD"),
            new QuestDef("QUEST_SKY_EXPLORATION", "Khám phá Thiên Giới", "TBD"),
        });

        public static readonly IReadOnlyList<ZoneUnlockRule> Rules = Array.AsReadOnly(new[]
        {
            new ZoneUnlockRule("ocean", tutorial: true, originHabitats: true, requiresZone: null, quest: "QUEST_AQUATIC_EXPLORATION", rewardId: "ZONE_EGG_OCEAN", eggSpecies: "droplet"),
            new ZoneUnlockRule("sky", tutorial: false, originHabitats: false, requiresZone: "ocean", quest: "QUEST_SKY_EXPLORATION", rewardId: "ZONE_EGG_SKY", eggSpecies: "zephyr"),
        });

        public static ZoneUnlockRule RuleFor(string zoneId)
        {
            foreach (ZoneUnlockRule r in Rules) if (r.ZoneId == zoneId) return r;
            return null;
        }

        public static bool IsQuestId(string id) { foreach (QuestDef q in Quests) if (q.Id == id) return true; return false; }
        public static bool IsRewardId(string id) { foreach (ZoneUnlockRule r in Rules) if (r.RewardId == id) return true; return false; }
    }

    /// <summary>Gói trứng (src/data/economy.ts PACKS): giá G và tỷ lệ % theo độ hiếm (GDD 3.1 trang 15).</summary>
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
            // U2.10 E2 (ADR 0204, đã duyệt): Starter 100 → 150 G. Gói hướng dẫn giữ 100 G (ProgressCatalog.TutorialPackPriceG, parity TS).
            new PackDef("starter", 150, 90, 10, 0, 0),
            new PackDef("elemental", 900, 48, 45, 7, 0),
            new PackDef("mystic", 6000, 15, 50, 32, 3),
            new PackDef("astral", 30000, 0, 28, 55, 17),
        });

        public static PackDef Get(string id)
        {
            if (TryGet(id, out PackDef p)) return p;
            throw new DomainInvariantException("Unknown pack id: " + id);
        }

        public static bool TryGet(string id, out PackDef pack)
        {
            foreach (PackDef p in All) if (p.Id == id) { pack = p; return true; }
            pack = null;
            return false;
        }

        /// <summary>U2.10 E1 (ADR 0204, đã duyệt): tối đa ngần này gói Starter mua bằng vàng mỗi ngày UTC (không tính gói hướng dẫn).</summary>
        public const int StarterDailyLimit = 5;

        public static int Odds(PackDef p, Rarity r) => r == Rarity.Common ? p.OddsCommon : r == Rarity.Rare ? p.OddsRare : r == Rarity.Epic ? p.OddsEpic : p.OddsLegendary;
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
        /// <summary>Giá gói hướng dẫn giữ 100 G như TS b33ec7a (E2 chỉ đổi gói Starter mua thường).</summary>
        public const long TutorialPackPriceG = 100;
        public static readonly MutationType TutorialPackMutation = MutationType.Normal;

        public static bool IsDiscoveryId(string id) => Contains(DiscoveryIds, id);
        /// <summary>ACH_CODEX_MASTER: "≥ 32/64 biến thể gốc" (GDD trang 22 và Phụ lục F).</summary>
        public const int CodexMasterCells = 32;

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
