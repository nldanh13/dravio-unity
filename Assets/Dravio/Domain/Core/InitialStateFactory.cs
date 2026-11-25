using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Dravio.Domain.Biology;
using Dravio.Domain.Catalog;

namespace Dravio.Domain.Core
{
    /// <summary>Khởi tạo save mới (src/engine/core/initialState.ts; GDD trang 4, kịch bản S01; ADR 0004 mục 11).</summary>
    public static class InitialStateFactory
    {
        /// <summary>150 G.</summary>
        public static readonly MicroGold StartingGold = MicroGold.FromMicro(150_000_000);

        private static readonly Personality[] Personalities = { Personality.Playful, Personality.Calm, Personality.Curious, Personality.Shy };

        /// <summary>
        /// UID cục bộ từ RNG đã tiêm: prefix + "_" + 4 khối hex 4 chữ số thường (floor(next × 0x10000)).
        /// </summary>
        public static string NewUid(IRng rng, string prefix = "d")
        {
            var sb = new StringBuilder(prefix.Length + 17);
            sb.Append(prefix).Append('_');
            for (int i = 0; i < 4; i++)
            {
                int v = (int)System.Math.Floor(rng.Next() * 0x10000);
                sb.Append(v.ToString("x4", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Save mới: 150 G, một Emberling baby XP 0 ở origin_meadow đã chọn vào slot sản xuất, biến thể Normal,
        /// genotype founder từ UID + contentVersion. Origin mở (5 chỗ, chỉ đồng cỏ), Ocean/Sky khóa.
        /// Hướng dẫn ở trạng thái active (TUT_01). Tiêu đúng 5 số của RNG (4 cho UID, 1 cho tính cách) như bản TS.
        /// </summary>
        public static GameState Create(string saveId, long nowMs, IRng rng)
        {
            string uid = NewUid(rng);
            DragonSpecies species = SpeciesCatalog.Get("emberling");
            var ember = new DragonState
            {
                Uid = uid,
                SpeciesId = species.Id,
                Genotype = Genetics.FounderGenotype(uid, ContentInfo.ContentVersion, species.Loci),
                Mutation = MutationType.Normal,
                XpSec = 0,
                Locked = false,
                Location = DragonLocation.Zone("origin", "origin_meadow"),
                HatchedAtMs = nowMs,
                Personality = rng.PickUniform(Personalities),
                Bond = 0,
                ParentIds = null,
                Generation = 1,
                Ancestry = Ancestry.Unknown,
                Origin = DragonOrigin.Pack,
                Activity = DragonActivity.Idle,
                BreedCooldownUntilMs = 0,
            };
            return new GameState
            {
                SchemaVersion = ContentInfo.SchemaVersion,
                ContentVersion = ContentInfo.ContentVersion,
                SaveId = saveId,
                Revision = 0,
                Player = new PlayerState
                {
                    Gold = StartingGold,
                    GoldRemainder = 0,
                    TotalGoldEarned = MicroGold.Zero,
                    Inventory = new Dictionary<string, long>(),
                    Upgrades = new UpgradesState(),
                    PityCount = 0,
                    Stats = new GameStats { LastDailyResetDate = JsCompat.UtcDay(nowMs) },
                    Settings = new GameSettings
                    {
                        Language = LanguageCode.Vi,
                        SfxVolume = 0.8,
                        BgmVolume = 0.6,
                        HapticsEnabled = true,
                        FpsLimit = 60,
                        TimeMode = TimeMode.Auto,
                        CompactMode = false,
                        ReducedMotion = false,
                        AlwaysOnTop = false,
                        Mode = AppMode.World,
                        Difficulty = Difficulty.Ecosystem,
                    },
                },
                Dragons = new List<DragonState> { ember },
                Zones = new List<ZoneState>
                {
                    new ZoneState { Id = "origin", Realm = Realm.Land, Unlocked = true, Capacity = WorldCatalog.P0ZoneCapacity, UnlockedHabitatIds = new List<string> { "origin_meadow" } },
                    new ZoneState { Id = "ocean", Realm = Realm.Ocean, Unlocked = false, Capacity = WorldCatalog.P0ZoneCapacity },
                    new ZoneState { Id = "sky", Realm = Realm.Sky, Unlocked = false, Capacity = WorldCatalog.P0ZoneCapacity },
                },
                ProductionDragonIds = new List<string> { uid },
                Food = new FoodState { LastSpawnAtMs = null, Drops = new List<FoodDrop>() },
                ProcessedRequestIds = new List<string>(),
                Clock = new ClockState(nowMs, nowMs),
                OfflineReceipt = null,
                Discoveries = new List<string>(),
                Tutorial = new TutorialState { Status = TutorialStatus.Active, StartedAtMs = nowMs, StarterPackUsed = false },
                PendingPack = null,
                UnlockedAchievements = new List<string>(),
                ClaimedAchievements = new List<string>(),
            };
        }
    }
}
