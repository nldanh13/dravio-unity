using System;
using System.Collections.Generic;
using System.Linq;
using Dravio.Domain.Behavior;
using Dravio.Domain.Breeding;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Economy;
using Dravio.Domain.Progression;
using Dravio.Domain.Queries;
using Dravio.Domain.Sanctuary;
using Dravio.Domain.World;

namespace Dravio.Domain.Tests.Simulation
{
    /// <summary>Lịch chơi trong ngày: các phiên (giờ bắt đầu tính từ 09:00, độ dài phút).</summary>
    public sealed class Schedule
    {
        public Schedule(string name, params (double startHour, int minutes)[] sessions) { Name = name; Sessions = sessions; }
        public string Name { get; }
        public (double startHour, int minutes)[] Sessions { get; }
        public int MinutesPerDay => Sessions.Sum(x => x.minutes);
    }

    public enum Policy
    {
        /// <summary>Không mua, không bán: chỉ để rồng khởi đầu sản xuất (mốc so sánh).</summary>
        Idle,
        /// <summary>Sưu tầm thông thường: mỗi phiên mua tối đa một gói (gói đắt nhất đủ tiền), lai khi có cặp, không bao giờ bán.</summary>
        Collector,
        /// <summary>Kiểm tra khai thác: mua Starter liên tục, nuôi trong kho tới Adult rồi bán; giữ 5 rồng sinh vàng tốt nhất.</summary>
        StarterTrader,
        /// <summary>Kiểm tra khai thác: có cặp nhóm T (Emberling + trứng hướng dẫn Sproutling), lai liên tục cặp rẻ nhất và bán con khi Adult.</summary>
        BreedTrader,
        /// <summary>U2.10 — thư giãn: tối đa một gói mỗi ngày (gói đắt nhất đủ tiền), không lai, không bán.</summary>
        Relaxed,
        /// <summary>U2.10 — lai tạo: lai liên tục cặp đắt nhất đủ tiền, gõ tổ, tối đa một gói mỗi ngày, không bán.</summary>
        Breeder,
        /// <summary>
        /// U2.10 — nghiên cứu: như Relaxed + cho ăn mỗi phiên, xem hồ sơ mọi loài, quan sát mọi rồng mỗi 30 s (ResearchPacing.ProposedU210),
        /// thám hiểm tuyến mở bằng rồng Adult không sinh vàng; U2.11: nhận nhiệm vụ bằng lệnh completeQuest thật rồi gửi unlockZone thật.
        /// </summary>
        Researcher,
        /// <summary>
        /// U2.12 — người chơi theo hướng dẫn hành trình (<see cref="ProgressionQueries.Journey"/>): như Researcher, cộng thêm: tiếp tục hướng dẫn nếu đã bỏ qua,
        /// mua thêm gói (trong giới hạn E1) khi hướng dẫn báo thiếu loài, đặt rồng theo yêu cầu nhiệm vụ (Adult biết bơi / bay, cư dân Thủy Vực / Thiên Giới),
        /// và lai một cặp rồi để con ở cùng microhabitat với bố mẹ (nhiệm vụ Thân thiết). Chỉ dùng lệnh thật.
        /// </summary>
        Guided,
        /// <summary>
        /// U2.12 — người chơi nhẹ nhàng có hướng dẫn: như Relaxed (1 gói/ngày), nhưng làm theo các bước rẻ của hướng dẫn hành trình: xem hồ sơ, cho ăn
        /// một lần mỗi phiên, quan sát đàn một lần đầu phiên, gửi thám hiểm, nhận nhiệm vụ, mở thế giới. Không quan sát liên tục, không lai.
        /// </summary>
        Casual,
    }

    /// <summary>
    /// Hành vi người chơi trong mô phỏng (U2.10). Luật kinh tế là luật THẬT của catalog / dispatcher (từ U2.10: Starter 150 G, tối đa 5 gói Starter / ngày,
    /// lệnh buildSanctuaryProject). Riêng E3 (phí lai Common, chưa duyệt) vẫn thử bằng "quỹ ảo" (VirtualSinkG): phần chênh được trừ ngay lúc lai
    /// và mọi quyết định mua dùng số dư sau khi trừ quỹ ảo.
    /// </summary>
    public sealed class EconomyVariant
    {
        public string Name = "baseline";
        /// <summary>Phí lai Common × Common thử nghiệm (G); phần chênh so với 80 G vào quỹ ảo. E3, chưa duyệt.</summary>
        public long CommonBreedFeeG = 80;
        /// <summary>Người chơi (không khai thác) mua nâng cấp: mỗi phiên tối đa một nâng cấp rẻ nhất có giá ≤ 50 % số dư.</summary>
        public bool BuyUpgrades;
        /// <summary>E5: người chơi xây dự án Sanctuary (lệnh thật) khi số dư ≥ 1,2 × giá và đủ gỗ, hạt giống.</summary>
        public bool BuildProjects;
        /// <summary>Relaxed / Collector / Breeder cũng gửi thám hiểm (để có gỗ, hạt cho dự án). Researcher luôn gửi.
        /// U2.11: khi đó họ cũng nhận nhiệm vụ đủ yêu cầu (đầu phiên và mỗi 10 phút) và mở thế giới khi đủ điều kiện.</summary>
        public bool RunExpeditions;
        /// <summary>Nhịp nghiên cứu của Researcher (null = <see cref="EconomySimulator.ResearchPacing"/>).</summary>
        public ResearchPacing Pacing;
        /// <summary>U2.13: bảng cân bằng sky_cloud áp dụng trong lần chạy (null = <see cref="SkyCloudBalance.Production"/>, tức tuyến tắt).</summary>
        public SkyCloudBalance Sky;
        /// <summary>U2.13: biến thể Live với một bảng sky_cloud (chỉ mô phỏng).</summary>
        public static EconomyVariant LiveWithSky(SkyCloudBalance sky) =>
            new EconomyVariant { Name = "live+" + sky.Id, BuyUpgrades = true, BuildProjects = true, RunExpeditions = true, Sky = sky };
        public static readonly EconomyVariant Baseline = new EconomyVariant();
        /// <summary>Có mua nâng cấp, KHÔNG có dự án Sanctuary (để thấy riêng tác dụng của E5).</summary>
        public static readonly EconomyVariant NoProjects = new EconomyVariant { Name = "no-projects", BuyUpgrades = true };
        /// <summary>Game thật sau U2.10: nâng cấp + dự án Sanctuary + thám hiểm Khởi Nguyên.</summary>
        public static readonly EconomyVariant Live = new EconomyVariant { Name = "live", BuyUpgrades = true, BuildProjects = true, RunExpeditions = true };
        /// <summary>E3 (chưa duyệt): phí lai Common 120 G qua quỹ ảo.</summary>
        public static readonly EconomyVariant E3BreedFee = new EconomyVariant { Name = "e3-breed-fee", CommonBreedFeeG = 120 };
    }

    public sealed class Snapshot
    {
        public double Hours;
        public long GoldG, EarnedG, SoldG, SpentPacksG, SpentBreedG, RewardsG;
        public double RateGPerSec;
        public int Dragons, Species, Cells, Achievements, PacksBought, Bred;
        // U2.10
        public long VirtualSinkG, MaterialsHeld, MaterialsSpent, SpentUpgradesG, ProjectsG;
        public int Projects, UpgradeLevels;
        /// <summary>Gỗ + hạt giống đã dùng cho dự án Sanctuary.</summary>
        public long ProjectMaterials;
        public int Profiled, LifeCycle, Ethology, IndividualsKnown, ZonesUnlocked, Observations;
        // U2.11
        /// <summary>Nhiệm vụ đã nhận (cổng + phụ) qua lệnh completeQuest thật.</summary>
        public int QuestsCompleted;
        /// <summary>Vật liệu đã giao cho nhiệm vụ.</summary>
        public long QuestMaterials;
        /// <summary>Vật liệu thưởng từ mốc thám hiểm.</summary>
        public long MilestoneMaterials;
        public int ExpeditionsClaimed;
        /// <summary>Số cặp rồng (chưa thả) ở bậc Friend trở lên.</summary>
        public int FriendPairs;
        public int TitlesEarned;
        // U2.12 — hệ sinh thái
        /// <summary>Cư dân (rồng ở sinh cảnh) theo thế giới: origin, ocean, sky.</summary>
        public int OriginResidents, OceanResidents, SkyResidents;
        /// <summary>Cư dân ở microhabitat suitability Good (≥ 80).</summary>
        public int GoodResidents;
        /// <summary>Số loài khác nhau đang sống ở sinh cảnh.</summary>
        public int ResidentSpecies;
        public int StorageDragons;
        /// <summary>Số cặp bậc Thân (≥ 80).</summary>
        public int ClosePairs;
        /// <summary>Giá dự án Sanctuary kế tiếp (G), 0 khi đã xây đủ.</summary>
        public long NextProjectCostG;
        public long Wood, Seed, Crystal, Coral;
        // U2.13
        public long Stardust;
        /// <summary>Chuyến đã nhận theo tuyến.</summary>
        public int ForestClaims, ReefClaims, SkyClaims;
        public bool OwnsAurorawing, Codex16;
    }

    public sealed class RunResult
    {
        public Dictionary<double, Snapshot> At = new Dictionary<double, Snapshot>();
        /// <summary>Giờ (đồng hồ thật) lần đầu số dư ≥ giá gói; null nếu chưa bao giờ.</summary>
        public Dictionary<string, double?> FirstAffordHours = new Dictionary<string, double?>();
        /// <summary>U2.10: giờ mở Thủy Vực / Thiên Giới (chỉ Researcher, theo đề xuất nhiệm vụ); null nếu chưa.</summary>
        public Dictionary<string, double?> ZoneUnlockHours = new Dictionary<string, double?> { ["ocean"] = null, ["sky"] = null };
        /// <summary>U2.11: giờ nhận từng nhiệm vụ (lệnh completeQuest thật).</summary>
        public Dictionary<string, double> QuestHours = new Dictionary<string, double>();
        /// <summary>U2.13: giờ trứng Aurorawing được trao (grantedRewards) và giờ nở / nhận vào kho; null nếu chưa.</summary>
        public double? AurorawingGrantedHours, AurorawingOwnedHours, Codex16Hours;
        public int AurorawingGrants;
    }

    /// <summary>
    /// Mô phỏng tiến trình tất định qua CommandDispatcher thật (không có quảng cáo). Mỗi seed = một saveId khác nhau → luồng RNG giao dịch khác nhau.
    /// Giữa các phiên là offline (75 %, tối đa 24 h); trong phiên quyết toán online và ra quyết định mỗi 30 s. Không mô phỏng cho ăn (chỉ cho XP, không cho vàng),
    /// nên thời gian trưởng thành là cận trên. Chỉ khu Khởi Nguyên (Ocean/Sky còn khóa ở G1): tối đa 5 rồng sinh vàng.
    /// </summary>
    public sealed class EconomySimulator
    {
        private readonly Policy _policy;
        private readonly Schedule _schedule;
        private GameState _s;
        private long _now;
        private int _rid;
        private readonly RunResult _result = new RunResult();
        private long _sold, _spentPacks, _spentBreed, _rewards;
        private int _packs, _bred;
        private bool _boughtThisSession;
        private readonly EconomyVariant _variant;
        private long _virtualSink, _materialsSpent;
        private int _observations;
        private string _lastPackDay;
        private bool _fedThisSession;
        private int _sessions;
        private long _spentUpgrades, _projectsG, _lastOnlineMs;
        private int _projects;
        private long _projectMaterials;
        private long _questMaterials, _milestoneMaterials;
        private int _questsDone;
        private long _tickInSession;
        private bool _upgradedThisSession;
        /// <summary>Rồng Researcher đặt có chủ đích (sinh cảnh thế giới mới, rồng biết bay); ArrangeProducers không đổi chỗ chúng.</summary>
        private readonly HashSet<string> _pinned = new HashSet<string>();
        /// <summary>U2.10: sổ nghiên cứu của save (Schema v9). Nhật ký đêm do dispatcher ghi khi quyết toán offline.</summary>
        public ResearchLog Research => _s.Research;
        public static ResearchPacing ResearchPacing = Dravio.Domain.Behavior.ResearchPacing.Production;

        public static readonly long Start = new DateTimeOffset(2026, 12, 1, 9, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

        /// <summary>Nhịp nghiên cứu của lần chạy này.</summary>
        public ResearchPacing Pacing => _variant.Pacing ?? ResearchPacing;

        public EconomySimulator(Policy policy, Schedule schedule, int seed, EconomyVariant variant = null)
        {
            _policy = policy; _schedule = schedule; _variant = variant ?? EconomyVariant.Baseline;
            _now = Start; _lastOnlineMs = Start;
            _s = InitialStateFactory.Create("sim-" + seed, _now, Rng.Create((uint)(seed * 2654435761u)));
            foreach (PackDef p in PackCatalog.All) _result.FirstAffordHours[p.Id] = null;
        }

        public GameState State => _s;

        /// <summary>U2.9: gọi sau mỗi lệnh (state hiện tại, thời điểm). Dùng để chèn truy vấn chỉ đọc và kiểm chúng không đổi kết quả kinh tế.</summary>
        public Action<GameState, long> AfterCommand { get; set; }

        private DispatchResult Do(GameCommand c)
        {
            DispatchResult r = CommandDispatcher.Dispatch(_s, c, new FixedClock(_now));
            if (r.Ok && !r.Duplicate)
            {
                foreach (DomainEvent e in r.Events)
                {
                    if (e is DragonSold ds) _sold += ds.PriceGoldMicro;
                    if (e is PackBought pb)
                    {
                        _spentPacks += pb.PriceGoldMicro; _packs++;
                    }
                    if (e is BreedStarted bs)
                    {
                        _spentBreed += bs.FeeGoldMicro;
                        if (bs.FeeGoldMicro == 80 * 1_000_000L) _virtualSink += (_variant.CommonBreedFeeG - 80) * 1_000_000;
                    }
                    if (e is AchievementUnlocked a) _rewards += a.RewardGoldMicro;
                    if (e is UpgradeBought ub) _spentUpgrades += ub.CostGoldMicro;
                    if (e is SanctuaryProjectBuilt sp) { _projectsG += sp.CostGoldMicro; _projects++; _materialsSpent += 2L * sp.MaterialEach; _projectMaterials += 2L * sp.MaterialEach; }
                    if (e is QuestRewarded qr)
                    {
                        _questsDone++;
                        long m = qr.Delivered.Sum(kv => (long)kv.Value);
                        _questMaterials += m; _materialsSpent += m;
                        _result.QuestHours[qr.QuestId] = (_now - Start) / 3_600_000.0;
                    }
                    if (e is ExplorationMilestoneReached em) _milestoneMaterials += em.Bonus.Sum(kv => (long)kv.Value);
                    if (e is RewardEggGranted rg && rg.RewardId == SkyCloudBalance.AurorawingEggRewardId)
                    {
                        _result.AurorawingGrants++;
                        _result.AurorawingGrantedHours = _result.AurorawingGrantedHours ?? (_now - Start) / 3_600_000.0;
                    }
                    if (e is DragonHatched dh && !dh.Tutorial && r.State.FindDragon(dh.DragonUid)?.Origin == DragonOrigin.Breed) _bred++;
                }
                _s = r.State;
                if (_s.Player.Gold.Micro < 0) throw new InvalidOperationException("negative gold");
                if (_result.AurorawingOwnedHours == null && _s.Dragons.Any(d => d.SpeciesId == SkyCloudBalance.AurorawingSpeciesId)) _result.AurorawingOwnedHours = (_now - Start) / 3_600_000.0;
                if (_result.Codex16Hours == null && _s.UnlockedAchievements.Contains("ACH_CODEX_16")) _result.Codex16Hours = (_now - Start) / 3_600_000.0;
            }
            AfterCommand?.Invoke(_s, _now);
            return r;
        }

        private string Rid() => "sim-" + (++_rid);

        public RunResult Run(double[] horizonsHours)
        {
            using (SkyCloudBalance.Use(_variant.Sky ?? SkyCloudBalance.Production)) return RunCore(horizonsHours);
        }

        private RunResult RunCore(double[] horizonsHours)
        {
            double maxH = horizonsHours.Max();
            var marks = new SortedSet<double>(horizonsHours);
            int days = (int)Math.Ceiling(maxH / 24) + 1;
            bool first = true;
            for (int day = 0; day < days; day++)
                foreach (var (startHour, minutes) in _schedule.Sessions)
                {
                    long sessionStart = Start + (long)((day * 24 + startHour) * 3_600_000);
                    long sessionEnd = sessionStart + minutes * 60_000L;
                    // Mốc rơi vào lúc offline: quyết toán offline tới mốc (như khi mở app đúng lúc đó để xem).
                    foreach (double h in marks.ToList())
                    {
                        long t = Start + (long)(h * 3_600_000);
                        if (t <= sessionStart) { Settle(t, AdvanceMode.Offline); Mark(h); marks.Remove(h); }
                    }
                    if (marks.Count == 0) return _result;
                    Settle(sessionStart, AdvanceMode.Offline);
                    if (first) { Onboarding(); first = false; }
                    _boughtThisSession = false; _fedThisSession = false; _upgradedThisSession = false; _sessions++; _tickInSession = 0;
                    for (_now = sessionStart; _now < sessionEnd; _now += 30_000)
                    {
                        Do(new AdvanceTimeCommand(Rid(), AdvanceMode.Online));
                        Act();
                        _tickInSession++;
                        TrackAfford();
                        foreach (double h in marks.ToList())
                            if (Start + (long)(h * 3_600_000) <= _now) { Mark(h); marks.Remove(h); }
                    }
                    _lastOnlineMs = sessionEnd;
                }
            foreach (double h in marks) Mark(h);
            return _result;
        }

        private void Settle(long t, AdvanceMode mode)
        {
            if (t <= _now) return;
            _now = t;
            Do(new AdvanceTimeCommand(Rid(), mode));
            TrackAfford();
        }

        private void TrackAfford()
        {
            foreach (PackDef p in PackCatalog.All)
                if (_result.FirstAffordHours[p.Id] == null && _s.Player.Gold >= MicroGold.FromWholeGold(p.PriceG))
                    _result.FirstAffordHours[p.Id] = (_now - Start) / 3_600_000.0;
        }

        private void Mark(double h)
        {
            long rate = 0;
            foreach (DragonState d in _s.Dragons)
                if (Placement.IsProducing(_s, d))
                {
                    DragonSpecies sp = SpeciesCatalog.Get(d.SpeciesId);
                    rate += EconomyRates.RateMicroGPerSec(sp, EconomyRates.GrowthStageOf(d.XpSec, sp), d.Mutation);
                }
            _result.At[h] = new Snapshot
            {
                Hours = h, GoldG = _s.Player.Gold.Micro / 1_000_000, EarnedG = _s.Player.TotalGoldEarned.Micro / 1_000_000, SoldG = _sold / 1_000_000,
                SpentPacksG = _spentPacks / 1_000_000, SpentBreedG = _spentBreed / 1_000_000, RewardsG = _rewards / 1_000_000, RateGPerSec = rate / 1e6,
                Dragons = _s.Dragons.Count(d => d.Location.Kind != LocationKind.Released), Species = Codex.SpeciesDiscovered(_s), Cells = Codex.CellsDiscovered(_s),
                Achievements = _s.UnlockedAchievements.Count, PacksBought = _packs, Bred = _bred,
                VirtualSinkG = _virtualSink / 1_000_000, MaterialsSpent = _materialsSpent, Observations = _observations,
                MaterialsHeld = _s.Player.Inventory.Values.Sum(), ZonesUnlocked = _s.Zones.Count(z => z.Unlocked),
                SpentUpgradesG = _spentUpgrades / 1_000_000, ProjectsG = _projectsG / 1_000_000, Projects = _projects, ProjectMaterials = _projectMaterials,
                UpgradeLevels = EconomyTables.Upgrades.Sum(u => u.LevelOf(_s.Player.Upgrades)),
                QuestsCompleted = _questsDone, QuestMaterials = _questMaterials, MilestoneMaterials = _milestoneMaterials,
                ExpeditionsClaimed = _s.ExpeditionsClaimed.Values.Sum(), TitlesEarned = Exploration.Titles(_s).Count(t => t.Earned),
                FriendPairs = FriendPairs(),
            };
            {
                Snapshot sn = _result.At[h];
                var res = _s.Dragons.Where(d => d.Location.Kind == LocationKind.Zone).ToList();
                sn.OriginResidents = res.Count(d => d.Location.ZoneId == "origin");
                sn.OceanResidents = res.Count(d => d.Location.ZoneId == "ocean");
                sn.SkyResidents = res.Count(d => d.Location.ZoneId == "sky");
                sn.GoodResidents = res.Count(d => Dravio.Domain.Biology.Suitability.TierOf(Dravio.Domain.Biology.Suitability.ForSpecies(d.SpeciesId, d.Location.HabitatId)) == Dravio.Domain.Biology.SuitabilityTier.Good);
                sn.ResidentSpecies = res.Select(d => d.SpeciesId).Distinct().Count();
                sn.StorageDragons = _s.Dragons.Count(d => d.Location.Kind == LocationKind.Storage);
                sn.ClosePairs = PairsAtLeast(RelationTier.Close);
                sn.NextProjectCostG = SanctuaryProjectCatalog.CostG(_s.SanctuaryProjectsBuilt) ?? 0;
                long Inv(string m) => _s.Player.Inventory.TryGetValue(m, out long v) ? v : 0;
                sn.Wood = Inv("wood"); sn.Seed = Inv("seed"); sn.Crystal = Inv("crystal"); sn.Coral = Inv("coral"); sn.Stardust = Inv(SkyCloudBalance.MaterialId);
                sn.ForestClaims = Exploration.Claimed(_s, "origin_forest"); sn.ReefClaims = Exploration.Claimed(_s, "ocean_reef"); sn.SkyClaims = Exploration.Claimed(_s, SkyCloudBalance.RouteId);
                sn.OwnsAurorawing = _s.Dragons.Any(d => d.SpeciesId == SkyCloudBalance.AurorawingSpeciesId && d.Location.Kind != LocationKind.Released);
                sn.Codex16 = _s.UnlockedAchievements.Contains("ACH_CODEX_16");
            }
            if (_policy == Policy.Researcher || _policy == Policy.Guided || _policy == Policy.Casual)
            {
                Dictionary<ResearchTier, int> tiers = ResearchProgress.TierCounts(_s, Research, Pacing);
                Snapshot sn = _result.At[h];
                sn.Profiled = tiers[ResearchTier.Profiled]; sn.LifeCycle = tiers[ResearchTier.LifeCycle]; sn.Ethology = tiers[ResearchTier.Ethology];
                sn.IndividualsKnown = _s.Dragons.Count(d => d.Location.Kind != LocationKind.Released &&
                    ResearchProgress.Individual(_s, d.Uid, Research, Pacing)?.RevealedTraits.Count == 4);
            }
        }

        private int FriendPairs() => PairsAtLeast(RelationTier.Friend);

        private int PairsAtLeast(RelationTier tier)
        {
            var kept = _s.Dragons.Where(d => d.Location.Kind != LocationKind.Released).ToList();
            int n = 0;
            for (int i = 0; i < kept.Count; i++)
                for (int j = i + 1; j < kept.Count; j++)
                    if (RelationRules.TierOf(SocialEcology.Familiarity(_s, kept[i], kept[j])) >= tier) n++;
            return n;
        }

        /// <summary>Hướng dẫn rút gọn: xem hồ sơ, mở đá ấm và bờ hồ (miễn phí), bỏ qua tutorial; Idle cũng làm (không tốn vàng).</summary>
        private void Onboarding()
        {
            Do(new ViewBiologyProfileCommand(Rid(), "emberling"));
            foreach (string h in new[] { "origin_warm_rocks", "origin_pondbank" }) { Do(new SurveyHabitatCommand(Rid(), h)); Do(new UnlockHabitatCommand(Rid(), h)); }
        }

        /// <summary>Số dư dùng để ra quyết định: vàng thật trừ quỹ ảo của biến thể.</summary>
        private bool CanAfford(long priceG) => _s.Player.Gold.Micro - _virtualSink >= priceG * 1_000_000;

        /// <summary>Giới hạn thật E1 (PackDailyLimit); dispatcher cũng chặn, đây chỉ để người chơi không gửi lệnh chắc chắn bị từ chối.</summary>
        private bool StarterAllowedToday() => PackDailyLimit.StarterLeftToday(_s, _now) > 0;

        private static long StarterPriceG => PackCatalog.Get("starter").PriceG;

        /// <summary>Mua gói đắt nhất đủ tiền (tôn trọng biến thể). Trả true nếu đã thử mua (giữ đúng hành vi Collector U2.7).</summary>
        private bool BuyBestPack()
        {
            if (_s.PendingPack != null) return false;
            foreach (PackDef p in PackCatalog.All.Reverse())
            {
                if (p.Id == "starter" && !StarterAllowedToday()) continue;
                if (CanAfford(p.PriceG)) { Do(new BuyPackCommand(Rid(), p.Id)); return true; }
            }
            return false;
        }

        /// <summary>Tối đa một gói mỗi ngày UTC (Relaxed / Breeder / Researcher).</summary>
        private void DailyPack()
        {
            if (!_s.Tutorial.StarterPackUsed && _s.Player.Gold >= MicroGold.FromWholeGold(100)) { Do(new BuyTutorialPackCommand(Rid())); return; }
            string day = JsCompat.UtcDay(_now);
            if (_lastPackDay == day) return;
            if (BuyBestPack()) _lastPackDay = day;
        }

        private long Rate(DragonState d)
        {
            DragonSpecies sp = SpeciesCatalog.Get(d.SpeciesId);
            return EconomyRates.RateMicroGPerSec(sp, GrowthStage.Adult, d.Mutation);
        }

        /// <summary>Giữ khu Khởi Nguyên đầy rồng có rate Adult cao nhất; mọi rồng trong khu đều trong ngân sách sản xuất.</summary>
        private void ArrangeProducers()
        {
            if (_s.PendingPack != null) Do(new ClaimPackCommand(Rid(), MoveTarget.Storage));
            // U2.12: Guided tự nhận trứng lai vào sinh cảnh của bố/mẹ (BreedForFamily).
            if (_policy != Policy.Guided && _s.BreedingNest != null && (_s.BreedingNest.Status == EggStatus.Ready || _s.BreedingNest.FinishAtMs <= _now)) Do(new ClaimBreedCommand(Rid(), MoveTarget.Storage));
            for (int guard = 0; guard < 10; guard++)
            {
                var inZone = _s.Dragons.Where(d => d.Location.Kind == LocationKind.Zone).ToList();
                var candidates = _s.Dragons.Where(d => d.Location.Kind == LocationKind.Storage && d.Activity == DragonActivity.Idle)
                    .OrderByDescending(Rate).ToList();
                // U2.12 (tối ưu, cùng kết quả): Khởi Nguyên đầy và rồng kho tốt nhất không hơn rồng yếu nhất có thể nhường chỗ → vòng dưới chắc chắn dừng,
                // nên không cần quét PlacementOptions của cả kho (kho vài trăm rồng sau 365 ngày).
                if (candidates.Count == 0) break;
                if (Zones.ZoneOccupancy(_s, "origin") >= 5)
                {
                    DragonState weakest = inZone.Where(d => d.Activity == DragonActivity.Idle && !_pinned.Contains(d.Uid)).OrderBy(Rate).FirstOrDefault();
                    if (weakest == null || Rate(weakest) >= Rate(candidates[0])) break;
                }
                DragonState best = candidates.FirstOrDefault(d => Zones.PlacementOptions(_s, d).Any(o => o.Blocked == null || o.Blocked == DomainErrorCode.ZoneFull));
                if (best == null) break;
                if (Zones.ZoneOccupancy(_s, "origin") >= 5)
                {
                    DragonState worst = inZone.Where(d => d.Activity == DragonActivity.Idle && !_pinned.Contains(d.Uid)).OrderBy(Rate).FirstOrDefault();
                    if (worst == null || Rate(worst) >= Rate(best)) break;
                    if (!Do(new MoveDragonCommand(Rid(), worst.Uid, MoveTarget.Storage)).Ok) break;
                }
                PlacementCheck spot = Zones.PlacementOptions(_s, _s.FindDragon(best.Uid)).Where(o => o.Blocked == null).OrderByDescending(o => o.Result.Score).FirstOrDefault();
                if (spot == null || !Do(new MoveDragonCommand(Rid(), best.Uid, MoveTarget.Zone(spot.ZoneId, spot.HabitatId))).Ok) break;
            }
            foreach (DragonState d in _s.Dragons.Where(d => d.Location.Kind == LocationKind.Zone && !_s.ProductionDragonIds.Contains(d.Uid)).ToList())
                Do(new SetProductionSlotCommand(Rid(), d.Uid, true));
        }

        private void Act()
        {
            if (_policy == Policy.Idle) return;
            ArrangeProducers();
            switch (_policy)
            {
                case Policy.Collector:
                    if (!_s.Tutorial.StarterPackUsed && _s.Player.Gold >= MicroGold.FromWholeGold(100)) Do(new BuyTutorialPackCommand(Rid()));
                    else if (!_boughtThisSession && BuyBestPack()) _boughtThisSession = true;
                    TryBreed(cheapest: false);
                    if (_variant.RunExpeditions) { RunExpedition(); if (_tickInSession % 20 == 0) ResearchQuests(); }
                    break;
                case Policy.StarterTrader:
                    if (_s.PendingPack == null && CanAfford(StarterPriceG) && StarterAllowedToday()) Do(new BuyPackCommand(Rid(), "starter"));
                    SellSurplusAdults();
                    break;
                case Policy.BreedTrader:
                    if (!_s.Tutorial.StarterPackUsed && _s.Player.Gold >= MicroGold.FromWholeGold(100)) Do(new BuyTutorialPackCommand(Rid()));
                    TryBreed(cheapest: true);
                    SellSurplusAdults();
                    break;
                case Policy.Relaxed:
                    DailyPack();
                    if (_variant.RunExpeditions) { RunExpedition(); if (_tickInSession % 20 == 0) ResearchQuests(); }
                    break;
                case Policy.Breeder:
                    DailyPack();
                    TryBreed(cheapest: false);
                    if (_s.BreedingNest != null && _s.BreedingNest.Status == EggStatus.Incubating) Do(new TapEggCommand(Rid()));
                    if (_variant.RunExpeditions) { RunExpedition(); if (_tickInSession % 20 == 0) ResearchQuests(); }
                    break;
                case Policy.Researcher:
                    DailyPack();
                    PlaceForResearch();
                    ResearchActions();
                    break;
                case Policy.Guided:
                    DailyPack();
                    GuidedActions();
                    PlaceForResearch();
                    ResearchActions();
                    BreedForFamily();
                    break;
                case Policy.Casual:
                    DailyPack();
                    CasualActions();
                    break;
            }
            ArrangeProducers();
            SpendLongTerm();
        }

        /// <summary>Nâng cấp (nếu biến thể bật) và dự án Sanctuary E5 (nếu bật). Mỗi phiên tối đa một nâng cấp.</summary>
        private void SpendLongTerm()
        {
            if (_variant.BuyUpgrades && !_upgradedThisSession)
            {
                long budget = (_s.Player.Gold.Micro - _virtualSink) / 2;
                UpgradeDef cheapest = null; long best = long.MaxValue;
                foreach (UpgradeDef u in EconomyTables.Upgrades)
                {
                    long? c = EconomyRates.UpgradeCostG(u, u.LevelOf(_s.Player.Upgrades));
                    if (c.HasValue && c.Value * 1_000_000 <= budget && c.Value < best) { best = c.Value; cheapest = u; }
                }
                if (cheapest != null && Do(new BuyUpgradeCommand(Rid(), cheapest.Id)).Ok) _upgradedThisSession = true;
            }
            if (_variant.BuildProjects && SanctuaryProjects.Check(_s) == null)
            {
                long cost = SanctuaryProjectCatalog.CostG(_s.SanctuaryProjectsBuilt).Value * 1_000_000;
                if (_s.Player.Gold.Micro - _virtualSink >= cost + cost / 5) Do(new BuildSanctuaryProjectCommand(Rid()));
            }
        }

        /// <summary>
        /// Researcher đặt rồng có chủ đích: sau khi mở thế giới mới, đưa tới 2 loài chưa ghi chép ở đó vào sinh cảnh của nó; giữ một Adult biết bay
        /// ở sinh cảnh (cần cho nhiệm vụ Thiên Giới). Chỉ dùng lệnh moveDragon thật.
        /// </summary>
        private void PlaceForResearch()
        {
            foreach (ZoneState z in _s.Zones.Where(z => z.Unlocked && z.Id != "origin").ToList())
            {
                for (int guard = 0; guard < 2; guard++)
                {
                    if (_s.Dragons.Count(d => d.Location.Kind == LocationKind.Zone && d.Location.ZoneId == z.Id) >= 2) break;
                    var here = new HashSet<string>(_s.Dragons.Where(d => d.Location.Kind == LocationKind.Zone && d.Location.ZoneId == z.Id).Select(d => d.SpeciesId));
                    DragonState who = null; PlacementCheck spot = null;
                    foreach (DragonState d in _s.Dragons.Where(d => d.Location.Kind == LocationKind.Storage && d.Activity == DragonActivity.Idle && !here.Contains(d.SpeciesId)))
                    {
                        spot = Zones.PlacementOptions(_s, d).Where(o => o.ZoneId == z.Id && o.Blocked == null).OrderByDescending(o => o.Result.Score).FirstOrDefault();
                        if (spot != null) { who = d; break; }
                    }
                    if (who == null || !Do(new MoveDragonCommand(Rid(), who.Uid, MoveTarget.Zone(spot.ZoneId, spot.HabitatId))).Ok) break;
                    _pinned.Add(who.Uid);
                }
            }
            bool IsAdultFlyer(DragonState d)
            {
                DragonSpecies sp = SpeciesCatalog.Get(d.SpeciesId);
                return BehaviorCatalog.Has(sp.Biology.Locomotion, Locomotion.Fly) && EconomyRates.GrowthStageOf(d.XpSec, sp) == GrowthStage.Adult;
            }
            if (_s.Dragons.Any(d => d.Location.Kind == LocationKind.Zone && IsAdultFlyer(d))) return;
            foreach (DragonState f in _s.Dragons.Where(d => d.Location.Kind == LocationKind.Storage && d.Activity == DragonActivity.Idle && IsAdultFlyer(d)).ToList())
            {
                var options = Zones.PlacementOptions(_s, f).Where(o => o.Blocked == null || o.Blocked == DomainErrorCode.ZoneFull).OrderByDescending(o => o.Result.Score).ToList();
                PlacementCheck spot = options.FirstOrDefault(o => o.Blocked == null);
                if (spot == null && options.Count > 0)
                {
                    // Khu đầy: đưa rồng sinh vàng thấp nhất (không ghim) ở khu đó về kho để nhường chỗ.
                    PlacementCheck full = options[0];
                    DragonState worst = _s.Dragons.Where(d => d.Location.Kind == LocationKind.Zone && d.Location.ZoneId == full.ZoneId && d.Activity == DragonActivity.Idle && !_pinned.Contains(d.Uid))
                        .OrderBy(Rate).FirstOrDefault();
                    if (worst != null && Do(new MoveDragonCommand(Rid(), worst.Uid, MoveTarget.Storage)).Ok)
                        spot = Zones.PlacementOptions(_s, _s.FindDragon(f.Uid)).Where(o => o.Blocked == null).OrderByDescending(o => o.Result.Score).FirstOrDefault();
                }
                if (spot != null && Do(new MoveDragonCommand(Rid(), f.Uid, MoveTarget.Zone(spot.ZoneId, spot.HabitatId))).Ok) { _pinned.Add(f.Uid); return; }
            }
        }

        private void TryBreed(bool cheapest)
        {
            if (_s.BreedingNest != null) return;
            // Mỗi (loài, biến thể) một đại diện, tối đa 40 — đủ để chọn cặp, tránh O(n²) với kho 500 rồng.
            var adults = _s.Dragons.Where(d => d.Location.Kind != LocationKind.Released && d.Activity == DragonActivity.Idle &&
                EconomyRates.GrowthStageOf(d.XpSec, SpeciesCatalog.Get(d.SpeciesId)) == GrowthStage.Adult)
                .GroupBy(d => d.SpeciesId + "/" + d.Mutation + "/" + (d.ParentIds == null ? "f" : "b")).Select(g => g.First()).Take(40).ToList();
            var pairs = new List<(DragonState a, DragonState b, long fee)>();
            for (int i = 0; i < adults.Count; i++)
                for (int j = i + 1; j < adults.Count; j++)
                    if (BreedingRules.CheckBreed(adults[i], adults[j], _now) == null)
                        pairs.Add((adults[i], adults[j], BreedingRules.BreedFeeG(SpeciesCatalog.Get(adults[i].SpeciesId), SpeciesCatalog.Get(adults[j].SpeciesId))));
            var pick = (cheapest ? pairs.OrderBy(p => p.fee) : pairs.OrderByDescending(p => p.fee))
                .FirstOrDefault(p => _s.Player.Gold.Micro - _virtualSink >= (p.fee == 80 ? _variant.CommonBreedFeeG : p.fee) * 1_000_000);
            if (pick.a != null) Do(new StartBreedCommand(Rid(), pick.a.Uid, pick.b.Uid));
        }

        private static DayPhase PhaseAt(long now) => DayCycle.PhaseOfMinute((int)(((now / 60_000) % DayCycle.MinutesPerDay + DayCycle.MinutesPerDay) % DayCycle.MinutesPerDay));

        private void ObserveAll()
        {
            foreach (BehaviorDecision d in BehaviorEngine.DecideAll(_s, _now, PhaseAt(_now)))
                // Cùng hàm với lệnh observeDragon (ResearchRules210.Observe), gọi thẳng để mô phỏng 90 ngày không phải sao chép state mỗi lần chạm.
                if (Research.Observe(_s, d, _now, Pacing).Recorded) _observations++;
        }

        /// <summary>
        /// Researcher: hồ sơ, cho ăn, quan sát, thám hiểm, nhận nhiệm vụ (lệnh completeQuest thật, U2.11) rồi mở thế giới bằng lệnh unlockZone thật.
        /// </summary>
        private void ResearchActions()
        {
            foreach (string sp in _s.Dragons.Where(d => d.Location.Kind != LocationKind.Released).Select(d => d.SpeciesId).Distinct().ToList())
                if (!_s.Tutorial.ProfilesViewed.Contains(sp)) Do(new ViewBiologyProfileCommand(Rid(), sp));
            if (!_fedThisSession)
            {
                DragonState eater = _s.Dragons.FirstOrDefault(d => d.Location.Kind == LocationKind.Zone && d.Activity == DragonActivity.Idle);
                // U2.12 Guided: cho ăn loài chưa được ghi "tìm ăn / ăn" trước (Ethology cần cả hai), xoay vòng theo thứ tự catalog.
                if (_policy == Policy.Guided)
                    eater = _s.Dragons.Where(d => d.Location.Kind == LocationKind.Zone && d.Activity == DragonActivity.Idle)
                        .OrderBy(d => Observed(d.SpeciesId).Contains(DragonBehavior.Eat) && Observed(d.SpeciesId).Contains(DragonBehavior.SeekFood) ? 1 : 0)
                        .ThenBy(d => SpeciesOrder(d.SpeciesId)).FirstOrDefault() ?? eater;
                if (eater != null)
                {
                    DietId diet = _policy == Policy.Guided ? TargetDiet(eater) : SpeciesCatalog.GetBiology(eater.SpeciesId).DietIds[0];
                    DispatchResult fr = Do(new FeedCommand(Rid(), diet, eater.Location.ZoneId, eater.Location.HabitatId));
                    if (fr.Ok)
                    {
                        _fedThisSession = true;
                        FoodDrop drop = _s.Food.Drops.LastOrDefault();
                        if (drop != null)
                        {
                            // Phiên chẵn: người chơi quan sát lúc rồng đang tìm ăn (SeekFood), trước khi nó ăn; phiên lẻ: quan sát lúc ăn (Eat).
                            if (_sessions % 2 == 0) ObserveAll();
                            Dictionary<string, string> assign = BehaviorEngine.AssignFood(_s, drop.ZoneId, drop.HabitatId, _now);
                            string uid = assign.FirstOrDefault(kv => kv.Value == drop.Id).Key ?? eater.Uid;
                            if (_policy == Policy.Guided) uid = eater.Uid;
                            Do(new ConsumeFoodCommand(Rid(), drop.Id, uid));
                        }
                    }
                }
            }
            ObserveAll();
            Research.PruneIndividuals(_s);

            RunExpedition();
            if (_tickInSession % 20 == 0) ResearchQuests();
        }

        /// <summary>Thám hiểm: rồng Adult không sinh vàng (trong kho), tuyến mở có bảng thưởng; nhận khi xong.</summary>
        private void RunExpedition()
        {
            if (_s.Expedition != null && (_s.Expedition.Status == ExpeditionStatus.Ready || _s.Expedition.FinishAtMs <= _now)) Do(new ClaimExpeditionCommand(Rid()));
            if (_s.Expedition == null)
            {
                // Muốn xây dự án mà thiếu gỗ / hạt giống: ưu tiên tuyến có các vật liệu đó (người chơi thật chọn tuyến theo thứ mình cần).
                IEnumerable<ExpeditionRoute> routes = ExpeditionCatalog.Routes.Reverse();
                if (_variant.BuildProjects && SanctuaryProjectCatalog.MaterialIds.Any(m => (_s.Player.Inventory.TryGetValue(m, out long have) ? have : 0) <
                        Math.Max(4, SanctuaryProjectCatalog.MaterialEach(_s.SanctuaryProjectsBuilt))))
                    routes = routes.OrderByDescending(r => r.Materials.Count(m => SanctuaryProjectCatalog.MaterialIds.Contains(m)));
                foreach (ExpeditionRoute route in routes)
                {
                    if (!Dravio.Domain.Expedition.Expeditions.RouteOpen(_s, route) || !route.RewardsDefined) continue;
                    DragonState who = _s.Dragons.Where(d => !_s.ProductionDragonIds.Contains(d.Uid))
                        .FirstOrDefault(d => Dravio.Domain.Expedition.Expeditions.CheckStart(_s, route.Id, d.Uid, out _) == null);
                    if (who != null && Do(new StartExpeditionCommand(Rid(), route.Id, who.Uid)).Ok) break;
                }
            }
        }

        private void ResearchQuests()
        {

            // U2.11: lệnh completeQuest THẬT. Người chơi nhận mọi nhiệm vụ đủ yêu cầu; nhiệm vụ cổng trước (catalog đã xếp cổng trước nhiệm vụ cùng thế giới).
            // Lặp vì nhận một nhiệm vụ có thể mở tiền đề của nhiệm vụ khác.
            for (int pass = 0; pass < 4; pass++)
            {
                List<QuestStatusView> ready = QuestProposals.Evaluate(_s, Research, Pacing).Where(v => v.CanComplete).ToList();
                if (ready.Count == 0) break;
                foreach (QuestStatusView q in ready.OrderBy(v => v.IsCanonicalGate ? 0 : 1)) Do(new CompleteQuestCommand(Rid(), q.QuestId));
            }
            foreach (ZoneUnlockRule rule in WorldUnlockCatalog.Rules)
                if (_s.FindZone(rule.ZoneId)?.Unlocked == false && WorldExpansion.MissingRequirements(_s, rule.ZoneId).Count == 0 &&
                    Do(new UnlockZoneCommand(Rid(), rule.ZoneId)).Ok)
                    _result.ZoneUnlockHours[rule.ZoneId] = (_now - Start) / 3_600_000.0;
        }

        // ---------------------------------------------------------------- U2.12: người chơi theo hướng dẫn hành trình

        private bool _extraPackThisSession;

        private IReadOnlyList<DragonBehavior> Observed(string speciesId) => ResearchProgress.Species(_s, speciesId, Research, Pacing).ObservedBehaviors;

        /// <summary>
        /// Guided: loại thức ăn để chính rồng mục tiêu được giao phần ăn (AssignFood ưu tiên rồng thích loại đó, rồi UID nhỏ): chọn loại mà không rồng nào
        /// khác cùng microhabitat (UID nhỏ hơn hoặc thích loại đó) ăn được; không có thì loại đầu tiên. Người chơi thật làm vậy khi đã biết khẩu vị.
        /// </summary>
        private DietId TargetDiet(DragonState target)
        {
            IReadOnlyList<DietId> diets = SpeciesCatalog.GetBiology(target.SpeciesId).DietIds;
            var rivals = _s.Dragons.Where(d => d.Uid != target.Uid && d.Location.Kind == LocationKind.Zone && d.Location.HabitatId == target.Location.HabitatId && d.Activity == DragonActivity.Idle).ToList();
            foreach (DietId diet in diets)
                if (!rivals.Any(d => SpeciesCatalog.GetBiology(d.SpeciesId).DietIds.Contains(diet) &&
                        (string.CompareOrdinal(d.Uid, target.Uid) < 0 || PersonalityProfiles.Of(d).FavoriteDiet == diet)))
                    return diet;
            DietId fav = PersonalityProfiles.Of(target).FavoriteDiet;
            return diets.Contains(fav) ? fav : diets[0];
        }

        private static int SpeciesOrder(string id)
        {
            for (int i = 0; i < SpeciesCatalog.All.Count; i++) if (SpeciesCatalog.All[i].Id == id) return i;
            return int.MaxValue;
        }
        /// <summary>Cặp bố mẹ và con dành cho nhiệm vụ Thân thiết (Guided).</summary>
        private string _familyParent, _familyChild;

        /// <summary>Số bước hướng dẫn đã làm theo (để test kiểm hướng dẫn thật sự được dùng).</summary>
        public int GuideStepsFollowed { get; private set; }

        private static bool IsAdult(DragonState d) => EconomyRates.GrowthStageOf(d.XpSec, SpeciesCatalog.Get(d.SpeciesId)) == GrowthStage.Adult;

        /// <summary>Đặt rồng vào microhabitat tốt nhất (lọc realm), nhường chỗ bằng rồng sinh vàng thấp nhất không ghim nếu thế giới đầy. Ghim rồng đã đặt.</summary>
        private bool PlacePinned(DragonState d, Realm? realm)
        {
            if (d == null || d.Activity != DragonActivity.Idle) return false;
            bool InRealm(PlacementCheck o) => !realm.HasValue || (HabitatCatalog.TryGet(o.HabitatId, out HabitatProfile hp) && hp.Realm == realm.Value);
            var options = Zones.PlacementOptions(_s, d).Where(o => InRealm(o) && (o.Blocked == null || o.Blocked == DomainErrorCode.ZoneFull))
                .OrderByDescending(o => o.Result.Score).ToList();
            PlacementCheck spot = options.FirstOrDefault(o => o.Blocked == null);
            if (spot == null && options.Count > 0)
            {
                string zone = options[0].ZoneId;
                DragonState worst = _s.Dragons.Where(x => x.Location.Kind == LocationKind.Zone && x.Location.ZoneId == zone && x.Activity == DragonActivity.Idle && !_pinned.Contains(x.Uid))
                    .OrderBy(Rate).FirstOrDefault();
                if (worst != null && Do(new MoveDragonCommand(Rid(), worst.Uid, MoveTarget.Storage)).Ok)
                    spot = Zones.PlacementOptions(_s, _s.FindDragon(d.Uid)).Where(o => InRealm(o) && o.Blocked == null).OrderByDescending(o => o.Result.Score).FirstOrDefault();
            }
            if (spot == null || !Do(new MoveDragonCommand(Rid(), d.Uid, MoveTarget.Zone(spot.ZoneId, spot.HabitatId))).Ok) return false;
            _pinned.Add(d.Uid);
            return true;
        }

        /// <summary>Làm theo các bước của <see cref="ProgressionQueries.Journey"/> (mỗi 10 phút trong phiên và đầu phiên).</summary>
        private void GuidedActions()
        {
            if (_tickInSession == 0) _extraPackThisSession = false;
            if (_tickInSession % 20 != 0) return;
            JourneyDto j = ProgressionQueries.Journey(_s);
            foreach (JourneyStep st in j.Steps)
            {
                switch (st.Code)
                {
                    case JourneyStepCode.ResumeTutorial:
                        if (Do(new SetTutorialStatusCommand(Rid(), TutorialStatus.Active)).Ok) GuideStepsFollowed++;
                        break;
                    case JourneyStepCode.SurveyHabitat when st.Ready:
                        if (Do(new SurveyHabitatCommand(Rid(), st.Target)).Ok) GuideStepsFollowed++;
                        break;
                    case JourneyStepCode.UnlockHabitat when st.Ready:
                        if (Do(new UnlockHabitatCommand(Rid(), st.Target)).Ok) GuideStepsFollowed++;
                        break;
                    case JourneyStepCode.QuestRequirement when st.Requirement == QuestRequirementKind.AdultWithLocomotion:
                    {
                        QuestRequirementDef def = QuestProposals.Get(st.QuestId).Requirements.First(r => r.Kind == QuestRequirementKind.AdultWithLocomotion);
                        DragonState c = st.Candidates.Select(uid => _s.FindDragon(uid)).FirstOrDefault(d => d != null && d.Location.Kind == LocationKind.Storage);
                        if (c != null && PlacePinned(c, def.Realm)) GuideStepsFollowed++;
                        else if (st.Candidates.Count == 0) ExtraPack();
                        break;
                    }
                    case JourneyStepCode.QuestRequirement when st.Detail == "need-species":
                        ExtraPack();
                        break;
                }
            }
            // Tập tính chỉ có ở một số microhabitat (vd tắm nắng cần nơi trên cạn, không lạnh): chuyển rồng ghim sang sinh cảnh khác của loài khi còn thiếu.
            foreach (DragonState d in _s.Dragons.Where(d => d.Location.Kind == LocationKind.Zone && d.Activity == DragonActivity.Idle).ToList())
            {
                IReadOnlyList<DragonBehavior> seen = Observed(d.SpeciesId);
                SpeciesBiology bio = SpeciesCatalog.GetBiology(d.SpeciesId);
                HabitatCatalog.TryGet(d.Location.HabitatId, out HabitatProfile here);
                var missingHere = Dravio.Domain.Behavior.Research.ObservableBehaviors(d.SpeciesId).Where(b => !seen.Contains(b) && !BehaviorCatalog.Allowed(bio, here, b)).ToList();
                if (missingHere.Count == 0) continue;
                PlacementCheck spot = Zones.PlacementOptions(_s, d).Where(o => o.Blocked == null && HabitatCatalog.TryGet(o.HabitatId, out HabitatProfile hp)
                    && missingHere.Any(b => BehaviorCatalog.Allowed(bio, hp, b))).OrderByDescending(o => o.Result.Score).FirstOrDefault();
                if (spot != null && Do(new MoveDragonCommand(Rid(), d.Uid, MoveTarget.Zone(spot.ZoneId, spot.HabitatId))).Ok) { _pinned.Add(d.Uid); GuideStepsFollowed++; }
            }
            // Cư dân Thủy Vực / Thiên Giới cho nhiệm vụ bảo tồn: lấp sinh cảnh realm đó bằng rồng có suitability Good.
            foreach (QuestStatusView v in QuestProposals.Evaluate(_s, Research, Pacing).Where(v => v.State == QuestState.InProgress))
                foreach (QuestRequirementStatus r in v.Requirements.Where(r => r.State == RequirementState.Unmet && r.Def.Kind == QuestRequirementKind.ResidentsThriving && r.Def.Realm.HasValue))
                {
                    Realm realm = r.Def.Realm.Value;
                    DragonState c = _s.Dragons.Where(d => d.Location.Kind == LocationKind.Storage && d.Activity == DragonActivity.Idle)
                        .FirstOrDefault(d => Zones.PlacementOptions(_s, d).Any(o => o.Blocked == null && HabitatCatalog.TryGet(o.HabitatId, out HabitatProfile hp) && hp.Realm == realm
                            && Dravio.Domain.Biology.Suitability.TierOf(o.Result) == Dravio.Domain.Biology.SuitabilityTier.Good));
                    if (c != null && PlacePinned(c, realm)) GuideStepsFollowed++;
                }
            // Nhiệm vụ ghi chép theo realm (Tide Journal, Sky Chronicle): đưa một loài chưa được ghi chép ở realm đó vào sinh cảnh của nó.
            foreach (QuestStatusView v in QuestProposals.Evaluate(_s, Research, Pacing).Where(v => v.State == QuestState.InProgress))
                foreach (QuestRequirementStatus r in v.Requirements.Where(r => r.State == RequirementState.Unmet && r.Def.Kind == QuestRequirementKind.SpeciesRecordedInRealm))
                {
                    Realm realm = r.Def.Realm.Value;
                    var recorded = new HashSet<string>(Research.AllSpecies.Where(x => ResearchProgress.Species(_s, x.SpeciesId, Research, Pacing).ObservedHabitats
                        .Any(h => HabitatCatalog.TryGet(h, out HabitatProfile hp) && hp.Realm == realm)).Select(x => x.SpeciesId));
                    var inRealm = new HashSet<string>(_s.Dragons.Where(d => d.Location.Kind == LocationKind.Zone && HabitatCatalog.TryGet(d.Location.HabitatId, out HabitatProfile hp) && hp.Realm == realm).Select(d => d.SpeciesId));
                    DragonState c = _s.Dragons.Where(d => d.Location.Kind == LocationKind.Storage && d.Activity == DragonActivity.Idle && !recorded.Contains(d.SpeciesId) && !inRealm.Contains(d.SpeciesId))
                        .FirstOrDefault(d => Zones.PlacementOptions(_s, d).Any(o => (o.Blocked == null || o.Blocked == DomainErrorCode.ZoneFull) && HabitatCatalog.TryGet(o.HabitatId, out HabitatProfile hp) && hp.Realm == realm));
                    if (c != null && PlacePinned(c, realm)) GuideStepsFollowed++;
                    else if (c == null) ExtraPack();
                }
        }

        /// <summary>Gói thêm khi hướng dẫn báo thiếu loài (tối đa một gói thêm mỗi phiên, vẫn trong giới hạn Starter E1).</summary>
        private void ExtraPack()
        {
            if (_extraPackThisSession || _s.PendingPack != null) return;
            _extraPackThisSession = true;
            if (BuyBestPack()) GuideStepsFollowed++;
        }

        /// <summary>
        /// Nhiệm vụ Thân thiết: khi đã nhận First Friends, lai cặp Adult rẻ nhất cùng loài với một rồng đang ở sinh cảnh, nhận trứng thẳng vào microhabitat
        /// của bố/mẹ (lệnh claimBreed với đích là sinh cảnh), ghim cả hai.
        /// </summary>
        private void BreedForFamily()
        {
            if (!QuestProposals.IsCompleted(_s, "Q_SOCIAL_FIRST_FRIENDS") || QuestProposals.IsCompleted(_s, "Q_SOCIAL_CLOSE_BOND")) return;
            if (_familyChild != null)
            {
                DragonState parent = _s.FindDragon(_familyParent), child = _s.FindDragon(_familyChild);
                if (parent == null || child == null || parent.Location.Kind == LocationKind.Released || child.Location.Kind == LocationKind.Released) { _familyChild = null; return; }
                if (parent.Location.Kind != LocationKind.Zone) PlacePinned(parent, null);
                parent = _s.FindDragon(_familyParent);
                if (parent.Location.Kind == LocationKind.Zone && (child.Location.Kind != LocationKind.Zone || child.Location.HabitatId != parent.Location.HabitatId) && child.Activity == DragonActivity.Idle
                    && Do(new MoveDragonCommand(Rid(), child.Uid, MoveTarget.Zone(parent.Location.ZoneId, parent.Location.HabitatId))).Ok)
                    _pinned.Add(child.Uid);
                return;
            }
            if (_s.BreedingNest != null)
            {
                if (_familyParent == null) return;
                DragonState p = _s.FindDragon(_familyParent);
                if (_s.BreedingNest.Status == EggStatus.Ready || _s.BreedingNest.FinishAtMs <= _now)
                {
                    MoveTarget target = p != null && p.Location.Kind == LocationKind.Zone ? MoveTarget.Zone(p.Location.ZoneId, p.Location.HabitatId) : MoveTarget.Storage;
                    DispatchResult r = Do(new ClaimBreedCommand(Rid(), target));
                    if (!r.Ok && target.Kind == LocationKind.Zone) r = Do(new ClaimBreedCommand(Rid(), MoveTarget.Storage));
                    DragonHatched h = r.Ok ? r.Events.OfType<DragonHatched>().FirstOrDefault() : null;
                    if (h != null) { _familyChild = h.DragonUid; _pinned.Add(h.DragonUid); GuideStepsFollowed++; }
                }
                else if (_s.BreedingNest.Status == EggStatus.Incubating) Do(new TapEggCommand(Rid()));
                return;
            }
            var adults = _s.Dragons.Where(d => d.Location.Kind != LocationKind.Released && d.Activity == DragonActivity.Idle && IsAdult(d)).ToList();
            foreach (DragonState a in adults.Where(d => d.Location.Kind == LocationKind.Zone).OrderBy(d => d.Uid, StringComparer.Ordinal))
                foreach (DragonState b in adults.Where(d => d.Uid != a.Uid && d.SpeciesId == a.SpeciesId).OrderBy(d => d.Uid, StringComparer.Ordinal))
                {
                    if (BreedingRules.CheckBreed(a, b, _now) != null) continue;
                    long fee = BreedingRules.BreedFeeG(SpeciesCatalog.Get(a.SpeciesId), SpeciesCatalog.Get(b.SpeciesId));
                    if (!CanAfford(fee)) continue;
                    if (Do(new StartBreedCommand(Rid(), a.Uid, b.Uid)).Ok) { _familyParent = a.Uid; _pinned.Add(a.Uid); GuideStepsFollowed++; }
                    return;
                }
        }

        /// <summary>Người chơi nhẹ nhàng: các bước rẻ của hướng dẫn, một lần quan sát đầu phiên, một bữa ăn mỗi phiên.</summary>
        private void CasualActions()
        {
            if (_tickInSession == 0)
            {
                JourneyDto j = ProgressionQueries.Journey(_s);
                foreach (JourneyStep st in j.Steps)
                {
                    if (st.Code == JourneyStepCode.ResumeTutorial && Do(new SetTutorialStatusCommand(Rid(), TutorialStatus.Active)).Ok) GuideStepsFollowed++;
                    if (st.Code == JourneyStepCode.SurveyHabitat && st.Ready && Do(new SurveyHabitatCommand(Rid(), st.Target)).Ok) GuideStepsFollowed++;
                    if (st.Code == JourneyStepCode.UnlockHabitat && st.Ready && Do(new UnlockHabitatCommand(Rid(), st.Target)).Ok) GuideStepsFollowed++;
                    if (st.Code == JourneyStepCode.QuestRequirement && st.Requirement == QuestRequirementKind.ResearchTier)
                        foreach (string sp in st.Candidates)
                            if (!_s.Tutorial.ProfilesViewed.Contains(sp) && Do(new ViewBiologyProfileCommand(Rid(), sp)).Ok) GuideStepsFollowed++;
                    if (st.Code == JourneyStepCode.QuestRequirement && st.Requirement == QuestRequirementKind.AdultWithLocomotion)
                    {
                        QuestRequirementDef def = QuestProposals.Get(st.QuestId).Requirements.First(r => r.Kind == QuestRequirementKind.AdultWithLocomotion);
                        DragonState c = st.Candidates.Select(uid => _s.FindDragon(uid)).FirstOrDefault(d => d != null && d.Location.Kind == LocationKind.Storage);
                        if (c != null && PlacePinned(c, def.Realm)) GuideStepsFollowed++;
                    }
                }
                ObserveAll();
            }
            // Bữa ăn sau 1 phút (hồi quan sát 30 s của lần quan sát đầu phiên đã hết). Phiên chẵn quan sát lúc tìm ăn, phiên lẻ lúc ăn — như Researcher.
            if (!_fedThisSession && _tickInSession >= 2)
            {
                // Mỗi phiên cho một rồng khác ăn (xoay vòng theo UID), chọn loại thức ăn để đúng rồng đó được ăn.
                var zone = _s.Dragons.Where(d => d.Location.Kind == LocationKind.Zone && d.Activity == DragonActivity.Idle).OrderBy(d => d.Uid, StringComparer.Ordinal).ToList();
                DragonState eater = zone.Count == 0 ? null : zone[_sessions % zone.Count];
                if (eater != null && Do(new FeedCommand(Rid(), TargetDiet(eater), eater.Location.ZoneId, eater.Location.HabitatId)).Ok)
                {
                    _fedThisSession = true;
                    FoodDrop drop = _s.Food.Drops.LastOrDefault();
                    if (drop != null)
                    {
                        if (_sessions % 2 == 0) ObserveAll();
                        Do(new ConsumeFoodCommand(Rid(), drop.Id, eater.Uid));
                        if (_sessions % 2 == 1) ObserveAll();
                    }
                }
            }
            RunExpedition();
            if (_tickInSession % 20 == 0) ResearchQuests();
        }

        /// <summary>Bán mọi rồng Adult trong kho (không sinh vàng) trừ hai bố mẹ dự phòng mỗi nhóm; giữ nguyên 5 rồng trong khu.</summary>
        private void SellSurplusAdults()
        {
            var keep = new HashSet<string>();
            foreach (var g in _s.Dragons.Where(d => d.Location.Kind == LocationKind.Storage).GroupBy(d => SpeciesCatalog.Get(d.SpeciesId).Group))
                foreach (DragonState d in g.OrderBy(d => SpeciesCatalog.Get(d.SpeciesId).Rarity).Take(_policy == Policy.BreedTrader ? 2 : 0)) keep.Add(d.Uid);
            foreach (DragonState d in _s.Dragons.ToList())
            {
                if (d.Location.Kind != LocationKind.Storage || keep.Contains(d.Uid) || d.Locked || d.Activity != DragonActivity.Idle) continue;
                DragonSpecies sp = SpeciesCatalog.Get(d.SpeciesId);
                if (EconomyRates.GrowthStageOf(d.XpSec, sp) != GrowthStage.Adult) continue;
                Do(new SellDragonCommand(Rid(), d.Uid));
            }
        }
    }
}
