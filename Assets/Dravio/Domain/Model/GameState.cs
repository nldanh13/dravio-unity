using System.Collections.Generic;
using Dravio.Domain.Core;

namespace Dravio.Domain
{
    /// <summary>Zone vật lý (ADR 0003): 3 zone, mỗi zone 5 chỗ ở P0; microhabitat là cấu hình trong zone.</summary>
    public sealed class ZoneState
    {
        /// <summary>origin | ocean | sky.</summary>
        public string Id { get; set; }
        public Realm Realm { get; set; }
        public bool Unlocked { get; set; }
        public int Capacity { get; set; }
        public List<string> UnlockedHabitatIds { get; set; } = new List<string>();
        public List<string> DecorationIds { get; set; } = new List<string>();
        public List<string> ActiveDecorationIds { get; set; } = new List<string>();
        /// <summary>Độ phục hồi 0–100.</summary>
        public int Recovery { get; set; }

        public ZoneState Clone()
        {
            var c = (ZoneState)MemberwiseClone();
            c.UnlockedHabitatIds = UnlockedHabitatIds == null ? null : new List<string>(UnlockedHabitatIds);
            c.DecorationIds = DecorationIds == null ? null : new List<string>(DecorationIds);
            c.ActiveDecorationIds = ActiveDecorationIds == null ? null : new List<string>(ActiveDecorationIds);
            return c;
        }
    }

    /// <summary>Viên thức ăn đang tồn tại (sống 12 s). Không lưu tọa độ frame.</summary>
    public sealed class FoodDrop
    {
        public string Id { get; set; }
        public DietId DietId { get; set; }
        public string ZoneId { get; set; }
        public string HabitatId { get; set; }
        public long SpawnedAtMs { get; set; }
        public long ExpiresAtMs { get; set; }

        public FoodDrop Clone() => (FoodDrop)MemberwiseClone();
    }

    /// <summary>GameState.food: thức ăn tạm thời (≤ 12 s) nằm trong state để reducer thuần (ADR 0004 mục 10).</summary>
    public sealed class FoodState
    {
        public long? LastSpawnAtMs { get; set; }
        public List<FoodDrop> Drops { get; set; } = new List<FoodDrop>();

        public FoodState Clone()
        {
            var c = new FoodState { LastSpawnAtMs = LastSpawnAtMs, Drops = Drops == null ? null : new List<FoodDrop>(Drops.Count) };
            if (Drops != null) foreach (var d in Drops) c.Drops.Add(d?.Clone());
            return c;
        }
    }

    /// <summary>Biên nhận offline (tạo khi offline &gt; 60 s). Lệnh nhận bonus chưa có ở TS b33ec7a.</summary>
    public sealed class OfflineReceipt
    {
        public string Id { get; set; }
        public long FromMs { get; set; }
        public long ToMs { get; set; }
        public long GoldMicro { get; set; }
        public List<string> MaturedIds { get; set; } = new List<string>();
        public bool BonusClaimed { get; set; }
        public long CreatedAtMs { get; set; }

        public OfflineReceipt Clone()
        {
            var c = (OfflineReceipt)MemberwiseClone();
            c.MaturedIds = MaturedIds == null ? null : new List<string>(MaturedIds);
            return c;
        }
    }

    /// <summary>Trứng đã roll và lưu, chờ nhận (schema v4). Lệnh buyTutorialPack (U2.4), buyPack (U2.5) tạo; claimPack nhận.</summary>
    public sealed class PendingPack
    {
        public string Id { get; set; }
        public string PackId { get; set; }
        public string SpeciesId { get; set; }
        public Genotype Genotype { get; set; } = new Genotype();
        public MutationType Mutation { get; set; }
        public long CreatedAtMs { get; set; }
        public bool Tutorial { get; set; }

        public PendingPack Clone()
        {
            var c = (PendingPack)MemberwiseClone();
            c.Genotype = Genotype?.Clone();
            return c;
        }
    }

    /// <summary>
    /// Trứng trong tổ ấp (GDD 3.1 Phụ lục C.2 BreedingEgg; schema v5, U2.5). Kết quả (loài, genotype, biến thể) đã roll một lần
    /// lúc bắt đầu lai và lưu ngay — không reroll. Một tổ trong P0.
    /// </summary>
    public sealed class BreedingEgg
    {
        public string Id { get; set; }
        public string[] ParentIds { get; set; }
        public string SpeciesId { get; set; }
        public Genotype Genotype { get; set; } = new Genotype();
        public MutationType Mutation { get; set; }
        public long CreatedAtMs { get; set; }
        /// <summary>Thời điểm nở dự kiến (đã trừ phần giảm do gõ).</summary>
        public long FinishAtMs { get; set; }
        /// <summary>Thời lượng sau nâng cấp hatchSpeed, không đổi khi gõ (mốc 10 %).</summary>
        public long DurationMs { get; set; }
        public long TapReducedMs { get; set; }
        /// <summary>0 = chưa gõ lần nào.</summary>
        public long LastTapAtMs { get; set; }
        public EggStatus Status { get; set; }

        public BreedingEgg Clone()
        {
            var c = (BreedingEgg)MemberwiseClone();
            c.Genotype = Genotype?.Clone();
            c.ParentIds = ParentIds == null ? null : (string[])ParentIds.Clone();
            return c;
        }
    }

    /// <summary>
    /// Một mục Bách khoa (GDD 3.1 Phụ lục C.2 codex; schema v6, U2.7). Chỉ tăng, không bao giờ giảm: khám phá được giữ sau khi bán
    /// hoặc thả (GDD Phụ lục F). Thứ tự danh sách theo catalog (biến thể Normal → Prismatic, stage Baby → Adult).
    /// </summary>
    public sealed class CodexEntry
    {
        public string SpeciesId { get; set; }
        public List<MutationType> DiscoveredMutations { get; set; } = new List<MutationType>();
        /// <summary>Số lần nở loài này (claimPack / claimBreed); rồng có sẵn lúc tạo save không tính.</summary>
        public long TotalHatched { get; set; }
        public List<GrowthStage> ObservedStages { get; set; } = new List<GrowthStage>();

        public CodexEntry Clone()
        {
            var c = (CodexEntry)MemberwiseClone();
            c.DiscoveredMutations = new List<MutationType>(DiscoveredMutations);
            c.ObservedStages = new List<GrowthStage>(ObservedStages);
            return c;
        }
    }

    /// <summary>
    /// Chuyến thám hiểm đang chạy hoặc chờ nhận (GDD tr. 22; schema v7, U2.8). "Chốt thời gian, seed và bảng thưởng khi bắt đầu": phần thưởng
    /// đã roll và lưu ngay lúc khởi hành, nhận đúng một lần; hoàn tất cả khi offline; không tự khởi hành tiếp.
    /// </summary>
    public sealed class ExpeditionJob
    {
        public string Id { get; set; }
        public string RouteId { get; set; }
        public string DragonUid { get; set; }
        public long StartedAtMs { get; set; }
        public long FinishAtMs { get; set; }
        /// <summary>Vật liệu → số lượng, thứ tự theo catalog vật liệu.</summary>
        public List<KeyValuePair<string, int>> Rewards { get; set; } = new List<KeyValuePair<string, int>>();
        public ExpeditionStatus Status { get; set; }

        public ExpeditionJob Clone()
        {
            var c = (ExpeditionJob)MemberwiseClone();
            c.Rewards = new List<KeyValuePair<string, int>>(Rewards);
            return c;
        }
    }

    /// <summary>Tiến trình hướng dẫn (schema v4, ADR 0013). U2.1 chỉ giữ và kiểm tra dữ liệu, chưa port evaluateTutorial.</summary>
    public sealed class TutorialState
    {
        public TutorialStatus Status { get; set; } = TutorialStatus.Active;
        /// <summary>Tiền tố có thứ tự của TUT_01…TUT_08.</summary>
        public List<string> Completed { get; set; } = new List<string>();
        public long StartedAtMs { get; set; }
        public List<string> ProfilesViewed { get; set; } = new List<string>();
        public bool StarterPackUsed { get; set; }

        public TutorialState Clone()
        {
            var c = (TutorialState)MemberwiseClone();
            c.Completed = Completed == null ? null : new List<string>(Completed);
            c.ProfilesViewed = ProfilesViewed == null ? null : new List<string>(ProfilesViewed);
            return c;
        }
    }

    /// <summary>
    /// Toàn bộ trạng thái game (GDD 3.1 Phụ lục C.2–C.3; src/types/state.ts, Dravio Save Schema v4).
    /// Chỉ đổi qua <see cref="CommandDispatcher.Dispatch"/>. Giá trị dẫn xuất (stage, kiểu hình, vàng/s) không lưu ở đây.
    /// </summary>
    public sealed class GameState
    {
        public int SchemaVersion { get; set; }
        public string ContentVersion { get; set; }
        public string SaveId { get; set; }
        /// <summary>Tăng 1 sau mỗi lệnh thành công không trùng.</summary>
        public long Revision { get; set; }

        /// <summary>Ví, vật phẩm, nâng cấp, thống kê, cài đặt (các trường phẳng goldMicro…settings của bản TS).</summary>
        public PlayerState Player { get; set; } = new PlayerState();

        public List<DragonState> Dragons { get; set; } = new List<DragonState>();
        public List<ZoneState> Zones { get; set; } = new List<ZoneState>();
        /// <summary>UID rồng được chọn vào ngân sách sản xuất (5 + 2 × cấp, tối đa 25).</summary>
        public List<string> ProductionDragonIds { get; set; } = new List<string>();
        public FoodState Food { get; set; } = new FoodState();
        /// <summary>requestId đã xử lý gần đây (idempotency), mới nhất ở cuối, tối đa 200.</summary>
        public List<string> ProcessedRequestIds { get; set; } = new List<string>();
        public ClockState Clock { get; set; }
        public OfflineReceipt OfflineReceipt { get; set; }
        /// <summary>Khám phá đã ghi nhận (schema v2), không trùng.</summary>
        public List<string> Discoveries { get; set; } = new List<string>();
        public TutorialState Tutorial { get; set; } = new TutorialState();
        public PendingPack PendingPack { get; set; }
        /// <summary>(breedingNest) Trứng lai đang ấp hoặc chờ nhận; null = tổ trống. Schema v5.</summary>
        public BreedingEgg BreedingNest { get; set; }
        /// <summary>(codex) Bách khoa theo loài, thứ tự catalog; chỉ chứa loài đã khám phá. Schema v6.</summary>
        public List<CodexEntry> Codex { get; set; } = new List<CodexEntry>();
        /// <summary>(expedition) Slot thám hiểm P0; null = trống. Schema v7.</summary>
        public ExpeditionJob Expedition { get; set; }
        public List<string> UnlockedAchievements { get; set; } = new List<string>();
        public List<string> ClaimedAchievements { get; set; } = new List<string>();

        /// <summary>Bản sao sâu. Dispatch làm việc trên bản sao để lệnh bị từ chối không đổi state gốc.</summary>
        public GameState DeepClone()
        {
            var c = (GameState)MemberwiseClone();
            c.Player = Player?.Clone();
            if (Dragons != null) { c.Dragons = new List<DragonState>(Dragons.Count); foreach (var d in Dragons) c.Dragons.Add(d?.Clone()); }
            if (Zones != null) { c.Zones = new List<ZoneState>(Zones.Count); foreach (var z in Zones) c.Zones.Add(z?.Clone()); }
            c.ProductionDragonIds = ProductionDragonIds == null ? null : new List<string>(ProductionDragonIds);
            c.Food = Food?.Clone();
            c.ProcessedRequestIds = ProcessedRequestIds == null ? null : new List<string>(ProcessedRequestIds);
            c.OfflineReceipt = OfflineReceipt?.Clone();
            c.Discoveries = Discoveries == null ? null : new List<string>(Discoveries);
            c.Tutorial = Tutorial?.Clone();
            c.PendingPack = PendingPack?.Clone();
            c.BreedingNest = BreedingNest?.Clone();
            c.Expedition = Expedition?.Clone();
            if (Codex != null) { c.Codex = new List<CodexEntry>(Codex.Count); foreach (var e in Codex) c.Codex.Add(e?.Clone()); }
            c.UnlockedAchievements = UnlockedAchievements == null ? null : new List<string>(UnlockedAchievements);
            c.ClaimedAchievements = ClaimedAchievements == null ? null : new List<string>(ClaimedAchievements);
            return c;
        }

        public DragonState FindDragon(string uid)
        {
            if (Dragons == null) return null;
            foreach (var d in Dragons) if (d != null && d.Uid == uid) return d;
            return null;
        }

        public ZoneState FindZone(string zoneId)
        {
            if (Zones == null) return null;
            foreach (var z in Zones) if (z != null && z.Id == zoneId) return z;
            return null;
        }
    }
}
