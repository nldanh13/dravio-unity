using System;
using System.Collections.Generic;
using System.Linq;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Economy;
using Dravio.Domain.Progression;
using Dravio.Domain.World;

namespace Dravio.Domain.Tests.Simulation
{
    /// <summary>
    /// U2.14 — hành trình Alpha từ save mới, chỉ bằng lệnh chính thức (<see cref="EconomyVariant.ObserveViaCommand"/>), có kiểm toán sau MỖI lệnh:
    /// - Sổ vàng: Δvàng = sản xuất (TimeAdvanced) + bán + thưởng thành tựu − gói − phí lai − nâng cấp − trang trí − dự án. Không khớp = nguồn vàng ngoài luật.
    /// - Trần sản xuất: vàng của một lần quyết toán ≤ Σ rate Adult của rồng trong ngân sách sản xuất × thời gian.
    /// - Sổ vật liệu: Δkho = chuyến thám hiểm + mốc − dự án − giao nhiệm vụ (theo từng vật liệu, không âm).
    /// - Một lần: thành tựu, nhiệm vụ, mốc thám hiểm, trứng thưởng mỗi ID chỉ trả một lần.
    /// Ghi log (lệnh, thời điểm) để phát lại: state cuối phải là hàm thuần của log (không ai sửa GameState ngoài dispatcher).
    /// </summary>
    public sealed class AlphaJourney
    {
        public readonly List<(GameCommand cmd, long nowMs)> Log = new List<(GameCommand, long)>();
        public readonly List<string> Violations = new List<string>();
        /// <summary>Giờ (tính từ <see cref="EconomySimulator.Start"/>) lần đầu đạt từng mốc hành trình.</summary>
        public readonly SortedDictionary<string, double> FirstAt = new SortedDictionary<string, double>(StringComparer.Ordinal);
        public readonly Dictionary<string, int> OkByType = new Dictionary<string, int>(StringComparer.Ordinal);
        public readonly Dictionary<string, int> RejectedByCode = new Dictionary<string, int>(StringComparer.Ordinal);
        public readonly Dictionary<string, int> PaidOnce = new Dictionary<string, int>(StringComparer.Ordinal);
        public int Commands, Duplicates;
        public long ProductionMicro, SalesMicro, AchievementMicro, SpentMicro;
        /// <summary>U2.15b: phần sản xuất quyết toán bằng <c>advanceTime(offline)</c> (thu nhập offline).</summary>
        public long OfflineProductionMicro;
        /// <summary>U2.15b: chi theo nhóm (pack, breed, upgrade, decoration, project) để báo cáo nguồn / đích vàng.</summary>
        public readonly Dictionary<string, long> SpentByMicro = new Dictionary<string, long>(StringComparer.Ordinal);
        /// <summary>U2.15b: vật liệu đã dùng cho dự án Sanctuary (tổng đơn vị).</summary>
        public long ProjectMaterialUnits;
        public EconomySimulator Sim { get; private set; }
        public RunResult Result { get; private set; }
        public int Seed { get; private set; }
        public EconomyVariant Variant { get; private set; }
        /// <summary>Mỗi mốc gameplay quan trọng: bản chụp state ngay sau lệnh đạt mốc (để kiểm save/load tại chỗ).</summary>
        public readonly SortedDictionary<string, GameState> StageSnapshots = new SortedDictionary<string, GameState>(StringComparer.Ordinal);
        private readonly bool _keepLog;
        private readonly HashSet<string> _snapshotKeys;

        private AlphaJourney(bool keepLog, IEnumerable<string> snapshotKeys)
        {
            _keepLog = keepLog;
            _snapshotKeys = new HashSet<string>(snapshotKeys ?? Array.Empty<string>(), StringComparer.Ordinal);
        }

        /// <summary>Mốc mặc định để chụp state (save/load ở các giai đoạn).</summary>
        public static readonly string[] DefaultStages =
        {
            "tutorial.completed", "pack.bought", "breed.started", "breed.hatched", "origin.habitats.all", "zone:ocean", "zone:sky", "project:1", "expedition:ocean_reef", "quest:Q_SOCIAL_CLOSE_BOND",
        };

        public static AlphaJourney Run(Policy policy, Schedule schedule, int seed, double days, EconomyVariant variant = null, bool keepLog = false, IEnumerable<string> snapshotStages = null) =>
            Run(policy, schedule, seed, new[] { days }, variant, keepLog, snapshotStages);

        /// <summary>Chạy tới mốc ngày lớn nhất, chụp <see cref="RunResult.At"/> ở mỗi mốc (giờ = ngày × 24).</summary>
        public static AlphaJourney Run(Policy policy, Schedule schedule, int seed, double[] horizonDays, EconomyVariant variant = null, bool keepLog = false, IEnumerable<string> snapshotStages = null)
        {
            var j = new AlphaJourney(keepLog, snapshotStages) { Seed = seed, Variant = variant ?? EconomyVariant.Alpha(), Policy = policy };
            j.Sim = new EconomySimulator(policy, schedule, seed, j.Variant);
            j.Sim.OnDispatch = j.OnDispatch;
            j.Result = j.Sim.Run(horizonDays.Select(d => d * 24).ToArray());
            return j;
        }

        public Policy Policy { get; private set; }

        /// <summary>Phát lại log từ save mới của cùng seed, với cùng luật trong phạm vi (bảng sky, vật liệu dự án).</summary>
        public GameState Replay()
        {
            if (!_keepLog) throw new InvalidOperationException("log not kept");
            using (SkyCloudBalance.Use(Variant.Sky ?? SkyCloudBalance.Production))
            using (SanctuaryMaterialRules.Use(Variant.ProjectMaterials ?? SanctuaryMaterialRules.Production))
            {
                GameState s = EconomySimulator.NewSave(Seed);
                foreach (var (cmd, now) in Log) s = CommandDispatcher.Dispatch(s, cmd, new FixedClock(now)).State;
                return s;
            }
        }

        private double H(long now) => (now - EconomySimulator.Start) / 3_600_000.0;

        private void First(string key, long now, GameState after)
        {
            if (FirstAt.ContainsKey(key)) return;
            FirstAt[key] = H(now);
            if (_snapshotKeys.Contains(key)) StageSnapshots[key] = after.DeepClone();
        }

        private void Once(string key)
        {
            PaidOnce[key] = (PaidOnce.TryGetValue(key, out int n) ? n : 0) + 1;
            if (PaidOnce[key] == 2) Violations.Add("paid twice: " + key);
        }

        private void Spend(string k, long micro) => SpentByMicro[k] = (SpentByMicro.TryGetValue(k, out long v) ? v : 0) + micro;

        private static long Inv(GameState s, string m) => s.Player.Inventory.TryGetValue(m, out long v) ? v : 0;

        private void OnDispatch(GameCommand cmd, GameState before, DispatchResult r, long now)
        {
            Commands++;
            if (_keepLog) Log.Add((cmd, now));
            if (!r.Ok)
            {
                string code = r.Error.Code.ToString();
                RejectedByCode[code] = (RejectedByCode.TryGetValue(code, out int c) ? c : 0) + 1;
                if (!ReferenceEquals(r.State, before)) Violations.Add("rejected command changed state: " + cmd.Type);
                return;
            }
            if (r.Duplicate) { Duplicates++; return; }
            OkByType[cmd.Type] = (OkByType.TryGetValue(cmd.Type, out int k) ? k : 0) + 1;
            GameState after = r.State;

            // ---------------- sổ vàng
            long expected = 0, prod = 0;
            var mats = new Dictionary<string, long>(StringComparer.Ordinal);
            void Mat(string m, long q) => mats[m] = (mats.TryGetValue(m, out long v) ? v : 0) + q;
            foreach (DomainEvent e in r.Events)
                switch (e)
                {
                    case TimeAdvanced t:
                    {
                        prod += t.GoldMicro;
                        long bound = 1;
                        foreach (DragonState d in before.Dragons)
                            if (before.ProductionDragonIds.Contains(d.Uid) && d.Location.Kind == LocationKind.Zone)
                                bound += EconomyRates.RateMicroGPerSec(SpeciesCatalog.Get(d.SpeciesId), GrowthStage.Adult, d.Mutation) * t.ElapsedMs / 1000 + 1;
                        if (t.GoldMicro > bound) Violations.Add($"production above ceiling at {H(now):0.00}h: {t.GoldMicro} > {bound}");
                        if (t.GoldMicro < 0) Violations.Add("negative production");
                        break;
                    }
                    case DragonSold x: expected += x.PriceGoldMicro; SalesMicro += x.PriceGoldMicro; break;
                    case AchievementUnlocked x:
                        expected += x.RewardGoldMicro; AchievementMicro += x.RewardGoldMicro; Once("achievement:" + x.AchievementId);
                        if (x.RewardGoldMicro != ProgressCatalog.AchievementRewardG(x.AchievementId) * 1_000_000) Violations.Add("achievement reward differs from catalog: " + x.AchievementId);
                        break;
                    case PackBought x: expected -= x.PriceGoldMicro; SpentMicro += x.PriceGoldMicro; Spend("pack", x.PriceGoldMicro); First(x.Tutorial ? "pack.tutorial" : "pack.bought", now, after); break;
                    case BreedStarted x: expected -= x.FeeGoldMicro; SpentMicro += x.FeeGoldMicro; Spend("breed", x.FeeGoldMicro); First("breed.started", now, after); break;
                    case UpgradeBought x: expected -= x.CostGoldMicro; SpentMicro += x.CostGoldMicro; Spend("upgrade", x.CostGoldMicro); break;
                    case DecorationBought x: expected -= x.PriceGoldMicro; SpentMicro += x.PriceGoldMicro; Spend("decoration", x.PriceGoldMicro); break;
                    case SanctuaryProjectBuilt x:
                        expected -= x.CostGoldMicro; SpentMicro += x.CostGoldMicro; Spend("project", x.CostGoldMicro);
                        ProjectMaterialUnits += (long)x.MaterialEach * x.MaterialIds.Count;
                        if (SanctuaryProjectCatalog.CostG(x.Index) * 1_000_000 != x.CostGoldMicro) Violations.Add("project cost differs from catalog: " + x.Index);
                        foreach (string m in x.MaterialIds) Mat(m, -x.MaterialEach);
                        Once("project:" + x.Index);
                        First("project:" + (x.Index + 1), now, after);
                        break;
                    case ExpeditionClaimed x:
                        foreach (var kv in x.Rewards) Mat(kv.Key, kv.Value);
                        Once("expedition:" + x.ExpeditionId);
                        break;
                    case ExplorationMilestoneReached x:
                        foreach (var kv in x.Bonus) Mat(kv.Key, kv.Value);
                        Once("milestone:" + x.RouteId + ":" + x.Claims);
                        First("milestone:" + x.RouteId + ":" + x.Claims, now, after);
                        break;
                    case QuestRewarded x:
                        foreach (var kv in x.Delivered) Mat(kv.Key, -kv.Value);
                        Once("quest:" + x.QuestId);
                        First("quest:" + x.QuestId, now, after);
                        break;
                    case RewardEggGranted x: Once("reward-egg:" + x.RewardId); First("reward-egg:" + x.RewardId, now, after); break;
                    case TutorialStatusChanged x when x.Status == TutorialStatus.Completed: First("tutorial.completed", now, after); break;
                    case FoodConsumed _: First("feed.first", now, after); break;
                    case StageChanged x when x.To == GrowthStage.Adult: First("adult.first", now, after); break;
                    case EggTapped _: First("egg.tapped", now, after); break;
                    case ZoneUnlocked x: First("zone:" + x.ZoneId, now, after); break;
                    case DragonObserved _: First("observe.first", now, after); break;
                    case DragonHatched x:
                        if (after.FindDragon(x.DragonUid)?.Origin == DragonOrigin.Breed) First("breed.hatched", now, after);
                        First("species:" + x.SpeciesId, now, after);
                        break;
                    case HabitatUnlocked _:
                        if (Zones.HabitatsOfZone("origin").All(h => after.FindZone("origin").UnlockedHabitatIds.Contains(h))) First("origin.habitats.all", now, after);
                        break;
                }
            if (cmd is ClaimExpeditionCommand && before.Expedition != null)
            {
                First("expedition:" + before.Expedition.RouteId, now, after);
                // Số lượng của một chuyến nằm trong [MinQty, MaxQty] của tuyến (bảng hiện hành), chỉ vật liệu của tuyến.
                ExpeditionCatalog.TryGet(before.Expedition.RouteId, out ExpeditionRoute route);
                foreach (ExpeditionClaimed x in r.Events.OfType<ExpeditionClaimed>())
                {
                    int qty = x.Rewards.Sum(kv => kv.Value);
                    if (qty < route.MinQty || qty > route.MaxQty) Violations.Add($"expedition quantity {qty} outside [{route.MinQty},{route.MaxQty}] on {route.Id}");
                    if (x.Rewards.Any(kv => !route.Materials.Contains(kv.Key))) Violations.Add("expedition paid a material outside its route: " + route.Id);
                }
            }
            long delta = after.Player.Gold.Micro - before.Player.Gold.Micro;
            if (delta != expected + prod) Violations.Add($"gold ledger mismatch on {cmd.Type} at {H(now):0.00}h: Δ{delta} vs {expected + prod}");
            ProductionMicro += prod;
            if (cmd is AdvanceTimeCommand at && at.Mode == AdvanceMode.Offline) OfflineProductionMicro += prod;
            if (after.Player.Gold.Micro < 0) Violations.Add("negative gold");

            // ---------------- sổ vật liệu
            foreach (string m in ExpeditionCatalog.MaterialIds)
            {
                long d = Inv(after, m) - Inv(before, m);
                long e = mats.TryGetValue(m, out long v) ? v : 0;
                if (d != e) Violations.Add($"material ledger mismatch {m} on {cmd.Type}: Δ{d} vs {e}");
                if (Inv(after, m) < 0) Violations.Add("negative material " + m);
            }
            if (after.Dragons.Count(d => d.Location.Kind != LocationKind.Released) > StorageCatalog.MaxDragons) Violations.Add("more than MaxDragons");
        }
    }
}
