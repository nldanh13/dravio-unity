using System;
using System.Collections.Generic;
using System.Linq;
using Dravio.Domain.Breeding;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Economy;
using Dravio.Domain.Progression;
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
    }

    public sealed class Snapshot
    {
        public double Hours;
        public long GoldG, EarnedG, SoldG, SpentPacksG, SpentBreedG, RewardsG;
        public double RateGPerSec;
        public int Dragons, Species, Cells, Achievements, PacksBought, Bred;
    }

    public sealed class RunResult
    {
        public Dictionary<double, Snapshot> At = new Dictionary<double, Snapshot>();
        /// <summary>Giờ (đồng hồ thật) lần đầu số dư ≥ giá gói; null nếu chưa bao giờ.</summary>
        public Dictionary<string, double?> FirstAffordHours = new Dictionary<string, double?>();
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

        public static readonly long Start = new DateTimeOffset(2026, 12, 1, 9, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

        public EconomySimulator(Policy policy, Schedule schedule, int seed)
        {
            _policy = policy; _schedule = schedule;
            _now = Start;
            _s = InitialStateFactory.Create("sim-" + seed, _now, Rng.Create((uint)(seed * 2654435761u)));
            foreach (PackDef p in PackCatalog.All) _result.FirstAffordHours[p.Id] = null;
        }

        public GameState State => _s;

        private DispatchResult Do(GameCommand c)
        {
            DispatchResult r = CommandDispatcher.Dispatch(_s, c, new FixedClock(_now));
            if (r.Ok && !r.Duplicate)
            {
                foreach (DomainEvent e in r.Events)
                {
                    if (e is DragonSold ds) _sold += ds.PriceGoldMicro;
                    if (e is PackBought pb) { _spentPacks += pb.PriceGoldMicro; _packs++; }
                    if (e is BreedStarted bs) _spentBreed += bs.FeeGoldMicro;
                    if (e is AchievementUnlocked a) _rewards += a.RewardGoldMicro;
                    if (e is DragonHatched dh && !dh.Tutorial && r.State.FindDragon(dh.DragonUid)?.Origin == DragonOrigin.Breed) _bred++;
                }
                _s = r.State;
                if (_s.Player.Gold.Micro < 0) throw new InvalidOperationException("negative gold");
            }
            return r;
        }

        private string Rid() => "sim-" + (++_rid);

        public RunResult Run(double[] horizonsHours)
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
                    _boughtThisSession = false;
                    for (_now = sessionStart; _now < sessionEnd; _now += 30_000)
                    {
                        Do(new AdvanceTimeCommand(Rid(), AdvanceMode.Online));
                        Act();
                        TrackAfford();
                        foreach (double h in marks.ToList())
                            if (Start + (long)(h * 3_600_000) <= _now) { Mark(h); marks.Remove(h); }
                    }
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
            };
        }

        /// <summary>Hướng dẫn rút gọn: xem hồ sơ, mở đá ấm và bờ hồ (miễn phí), bỏ qua tutorial; Idle cũng làm (không tốn vàng).</summary>
        private void Onboarding()
        {
            Do(new ViewBiologyProfileCommand(Rid(), "emberling"));
            foreach (string h in new[] { "origin_warm_rocks", "origin_pondbank" }) { Do(new SurveyHabitatCommand(Rid(), h)); Do(new UnlockHabitatCommand(Rid(), h)); }
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
            if (_s.BreedingNest != null && (_s.BreedingNest.Status == EggStatus.Ready || _s.BreedingNest.FinishAtMs <= _now)) Do(new ClaimBreedCommand(Rid(), MoveTarget.Storage));
            for (int guard = 0; guard < 10; guard++)
            {
                var inZone = _s.Dragons.Where(d => d.Location.Kind == LocationKind.Zone).ToList();
                var candidates = _s.Dragons.Where(d => d.Location.Kind == LocationKind.Storage && d.Activity == DragonActivity.Idle)
                    .OrderByDescending(Rate).ToList();
                DragonState best = candidates.FirstOrDefault(d => Zones.PlacementOptions(_s, d).Any(o => o.Blocked == null || o.Blocked == DomainErrorCode.ZoneFull));
                if (best == null) break;
                if (Zones.ZoneOccupancy(_s, "origin") >= 5)
                {
                    DragonState worst = inZone.Where(d => d.Activity == DragonActivity.Idle).OrderBy(Rate).FirstOrDefault();
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
                    else if (!_boughtThisSession)
                        foreach (PackDef p in PackCatalog.All.Reverse())
                            if (_s.PendingPack == null && _s.Player.Gold >= MicroGold.FromWholeGold(p.PriceG)) { Do(new BuyPackCommand(Rid(), p.Id)); _boughtThisSession = true; break; }
                    TryBreed(cheapest: false);
                    break;
                case Policy.StarterTrader:
                    if (_s.PendingPack == null && _s.Player.Gold >= MicroGold.FromWholeGold(100)) Do(new BuyPackCommand(Rid(), "starter"));
                    SellSurplusAdults();
                    break;
                case Policy.BreedTrader:
                    if (!_s.Tutorial.StarterPackUsed && _s.Player.Gold >= MicroGold.FromWholeGold(100)) Do(new BuyTutorialPackCommand(Rid()));
                    TryBreed(cheapest: true);
                    SellSurplusAdults();
                    break;
            }
            ArrangeProducers();
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
            var pick = (cheapest ? pairs.OrderBy(p => p.fee) : pairs.OrderByDescending(p => p.fee)).FirstOrDefault(p => _s.Player.Gold >= MicroGold.FromWholeGold(p.fee));
            if (pick.a != null) Do(new StartBreedCommand(Rid(), pick.a.Uid, pick.b.Uid));
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
