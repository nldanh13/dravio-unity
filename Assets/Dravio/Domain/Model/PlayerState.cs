using System.Collections.Generic;

namespace Dravio.Domain
{
    /// <summary>Cấp nâng cấp (GameState.upgrades). Giới hạn tối đa nằm trong EconomyTables.Upgrades.</summary>
    public sealed class UpgradesState
    {
        public int IslandCapacityLevel { get; set; }
        public int FoodQualityLevel { get; set; }
        public int FoodCooldownLevel { get; set; }
        public int MutationLuckLevel { get; set; }
        public int HatchSpeedLevel { get; set; }

        public UpgradesState Clone() => (UpgradesState)MemberwiseClone();
    }

    /// <summary>Thống kê (GameState.stats).</summary>
    public sealed class GameStats
    {
        public long TotalHatched { get; set; }
        public long TotalSold { get; set; }
        public long TotalBred { get; set; }
        public long TotalFeedClicks { get; set; }
        public long DailyFreePacksClaimed { get; set; }
        /// <summary>UTC YYYY-MM-DD.</summary>
        public string LastDailyResetDate { get; set; }

        public GameStats Clone() => (GameStats)MemberwiseClone();
    }

    /// <summary>Cài đặt người chơi (GameState.settings). Domain chỉ lưu và kiểm tra, không áp dụng.</summary>
    public sealed class GameSettings
    {
        public LanguageCode Language { get; set; } = LanguageCode.Vi;
        public double SfxVolume { get; set; } = 0.8;
        public double BgmVolume { get; set; } = 0.6;
        public bool HapticsEnabled { get; set; } = true;
        /// <summary>30 hoặc 60.</summary>
        public int FpsLimit { get; set; } = 60;
        public TimeMode TimeMode { get; set; } = TimeMode.Auto;
        public bool CompactMode { get; set; }
        public bool ReducedMotion { get; set; }
        public bool AlwaysOnTop { get; set; }
        public AppMode Mode { get; set; } = AppMode.World;
        public Difficulty Difficulty { get; set; } = Difficulty.Ecosystem;

        public GameSettings Clone() => (GameSettings)MemberwiseClone();
    }

    /// <summary>
    /// Phần "người chơi" của GameState: ví, vật phẩm, nâng cấp, thống kê, cài đặt, pity.
    /// Bản TypeScript lưu các trường này phẳng trong GameState; U2.1 gom lại cho rõ trách nhiệm,
    /// mỗi thuộc tính ghi tên trường JSON gốc để lớp save (U2.2) ánh xạ 1-1 mà không đổi schema.
    /// </summary>
    public sealed class PlayerState
    {
        /// <summary>(goldMicro) Số dư hiện tại.</summary>
        public MicroGold Gold { get; set; }

        /// <summary>
        /// (goldRemainder) Phần lẻ dưới 1 microG, đơn vị microG / 4000 (REMAINDER_SCALE = 1000 ms × mẫu số 4
        /// của hệ số online 4/4 và offline 3/4). Luôn trong 0..3999.
        /// </summary>
        public long GoldRemainder { get; set; }

        /// <summary>(totalGoldEarnedMicro) Tổng vàng sản xuất. Thưởng thành tựu không tính vào đây.</summary>
        public MicroGold TotalGoldEarned { get; set; }

        /// <summary>(inventory) itemId → số lượng ≥ 0.</summary>
        public Dictionary<string, long> Inventory { get; set; } = new Dictionary<string, long>();

        public UpgradesState Upgrades { get; set; } = new UpgradesState();

        /// <summary>(pityCount) Bộ đếm Prismatic pity, 0..199. Logic roll chưa port ở U2.1.</summary>
        public int PityCount { get; set; }

        public GameStats Stats { get; set; } = new GameStats();

        public GameSettings Settings { get; set; } = new GameSettings();

        public PlayerState Clone()
        {
            var c = (PlayerState)MemberwiseClone();
            c.Inventory = Inventory == null ? null : new Dictionary<string, long>(Inventory);
            c.Upgrades = Upgrades?.Clone();
            c.Stats = Stats?.Clone();
            c.Settings = Settings?.Clone();
            return c;
        }
    }
}
