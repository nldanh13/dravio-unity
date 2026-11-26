using System;
using System.Collections.Generic;
using System.Linq;
using Dravio.Domain.Behavior;
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
        /// <summary>U2.10 — thư giãn: tối đa một gói mỗi ngày (gói đắt nhất đủ tiền), không lai, không bán.</summary>
        Relaxed,
        /// <summary>U2.10 — lai tạo: lai liên tục cặp đắt nhất đủ tiền, gõ tổ, tối đa một gói mỗi ngày, không bán.</summary>
        Breeder,
        /// <summary>
        /// U2.10 — nghiên cứu: như Relaxed + cho ăn mỗi phiên, xem hồ sơ mọi loài, quan sát mọi rồng mỗi 30 s (ResearchPacing.ProposedU210),
        /// thám hiểm tuyến mở bằng rồng Adult không sinh vàng; THỬ NGHIỆM hoàn thành nhiệm vụ cổng theo đề xuất ADR 0204 rồi gửi unlockZone thật.
        /// </summary>
        Researcher,
    }

    /// <summary>
    /// Biến thể cân bằng (U2.10 Workstream D) để so sánh trước/sau mà KHÔNG đổi catalog production.
    /// Chênh lệch giá/phí được trừ vào "quỹ ảo" (VirtualSinkG) ngay lúc mua và mọi quyết định mua dùng số dư sau khi trừ quỹ ảo,
    /// nên tương đương trừ thật về dòng tiền (vàng không có tác dụng nào khác ngoài mua).
    /// </summary>
    public sealed class EconomyVariant
    {
        public string Name = "baseline";
        /// <summary>Tối đa số gói Starter mỗi ngày UTC (0 = không giới hạn).</summary>
        public int StarterDailyCap;
        /// <summary>Giá Starter đề xuất (G); phần chênh so với 100 G vào quỹ ảo.</summary>
        public long StarterPriceG = 100;
        /// <summary>Phí lai Common × Common đề xuất (G); phần chênh so với 80 G vào quỹ ảo.</summary>
        public long CommonBreedFeeG = 80;
        public static readonly EconomyVariant Baseline = new EconomyVariant();
        /// <summary>Đề xuất E1–E3 của ADR 0204.</summary>
        public static readonly EconomyVariant ProposedU210 = new EconomyVariant { Name = "proposed", StarterDailyCap = 5, StarterPriceG = 150, CommonBreedFeeG = 120 };
    }

    public sealed class Snapshot
    {
        public double Hours;
        public long GoldG, EarnedG, SoldG, SpentPacksG, SpentBreedG, RewardsG;
        public double RateGPerSec;
        public int Dragons, Species, Cells, Achievements, PacksBought, Bred;
        // U2.10
        public long VirtualSinkG, MaterialsHeld, MaterialsSpent;
        public int Profiled, LifeCycle, Ethology, IndividualsKnown, ZonesUnlocked, Observations;
    }

    public sealed class RunResult
    {
        public Dictionary<double, Snapshot> At = new Dictionary<double, Snapshot>();
        /// <summary>Giờ (đồng hồ thật) lần đầu số dư ≥ giá gói; null nếu chưa bao giờ.</summary>
        public Dictionary<string, double?> FirstAffordHours = new Dictionary<string, double?>();
        /// <summary>U2.10: giờ mở Thủy Vực / Thiên Giới (chỉ Researcher, theo đề xuất nhiệm vụ); null nếu chưa.</summary>
        public Dictionary<string, double?> ZoneUnlockHours = new Dictionary<string, double?> { ["ocean"] = null, ["sky"] = null };
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
        private readonly Dictionary<string, int> _startersByDay = new Dictionary<string, int>();
        private bool _fedThisSession;
        private int _sessions;
        /// <summary>U2.10: sổ nghiên cứu của Researcher (ngoài GameState, như production).</summary>
        public ResearchLog Research { get; } = new ResearchLog();
        public static ResearchPacing ResearchPacing = Dravio.Domain.Behavior.ResearchPacing.ProposedU210;

        public static readonly long Start = new DateTimeOffset(2026, 12, 1, 9, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

        public EconomySimulator(Policy policy, Schedule schedule, int seed, EconomyVariant variant = null)
        {
            _policy = policy; _schedule = schedule; _variant = variant ?? EconomyVariant.Baseline;
            _now = Start;
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
                        if (pb.PackId == "starter" && !pb.Tutorial)
                        {
                            _virtualSink += (_variant.StarterPriceG - 100) * 1_000_000;
                            string day = JsCompat.UtcDay(_now);
                            _startersByDay[day] = (_startersByDay.TryGetValue(day, out int k) ? k : 0) + 1;
                        }
                    }
                    if (e is BreedStarted bs)
                    {
                        _spentBreed += bs.FeeGoldMicro;
                        if (bs.FeeGoldMicro == 80 * 1_000_000L) _virtualSink += (_variant.CommonBreedFeeG - 80) * 1_000_000;
                    }
                    if (e is AchievementUnlocked a) _rewards += a.RewardGoldMicro;
                    if (e is DragonHatched dh && !dh.Tutorial && r.State.FindDragon(dh.DragonUid)?.Origin == DragonOrigin.Breed) _bred++;
                }
                _s = r.State;
                if (_s.Player.Gold.Micro < 0) throw new InvalidOperationException("negative gold");
            }
            AfterCommand?.Invoke(_s, _now);
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
                    _boughtThisSession = false; _fedThisSession = false; _sessions++;
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
                VirtualSinkG = _virtualSink / 1_000_000, MaterialsSpent = _materialsSpent, Observations = _observations,
                MaterialsHeld = _s.Player.Inventory.Values.Sum(), ZonesUnlocked = _s.Zones.Count(z => z.Unlocked),
            };
            if (_policy == Policy.Researcher)
            {
                Dictionary<ResearchTier, int> tiers = ResearchProgress.TierCounts(_s, Research, ResearchPacing);
                Snapshot sn = _result.At[h];
                sn.Profiled = tiers[ResearchTier.Profiled]; sn.LifeCycle = tiers[ResearchTier.LifeCycle]; sn.Ethology = tiers[ResearchTier.Ethology];
                sn.IndividualsKnown = _s.Dragons.Count(d => d.Location.Kind != LocationKind.Released &&
                    ResearchProgress.Individual(_s, d.Uid, Research, ResearchPacing)?.RevealedTraits.Count == 4);
            }
        }

        /// <summary>Hướng dẫn rút gọn: xem hồ sơ, mở đá ấm và bờ hồ (miễn phí), bỏ qua tutorial; Idle cũng làm (không tốn vàng).</summary>
        private void Onboarding()
        {
            Do(new ViewBiologyProfileCommand(Rid(), "emberling"));
            foreach (string h in new[] { "origin_warm_rocks", "origin_pondbank" }) { Do(new SurveyHabitatCommand(Rid(), h)); Do(new UnlockHabitatCommand(Rid(), h)); }
        }

        /// <summary>Số dư dùng để ra quyết định: vàng thật trừ quỹ ảo của biến thể.</summary>
        private bool CanAfford(long priceG) => _s.Player.Gold.Micro - _virtualSink >= priceG * 1_000_000 + (priceG == 100 ? (_variant.StarterPriceG - 100) * 1_000_000 : 0);

        private bool StarterAllowedToday()
        {
            if (_variant.StarterDailyCap <= 0) return true;
            return !_startersByDay.TryGetValue(JsCompat.UtcDay(_now), out int n) || n < _variant.StarterDailyCap;
        }

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
                    else if (!_boughtThisSession && BuyBestPack()) _boughtThisSession = true;
                    TryBreed(cheapest: false);
                    break;
                case Policy.StarterTrader:
                    if (_s.PendingPack == null && CanAfford(100) && StarterAllowedToday()) Do(new BuyPackCommand(Rid(), "starter"));
                    SellSurplusAdults();
                    break;
                case Policy.BreedTrader:
                    if (!_s.Tutorial.StarterPackUsed && _s.Player.Gold >= MicroGold.FromWholeGold(100)) Do(new BuyTutorialPackCommand(Rid()));
                    TryBreed(cheapest: true);
                    SellSurplusAdults();
                    break;
                case Policy.Relaxed:
                    DailyPack();
                    break;
                case Policy.Breeder:
                    DailyPack();
                    TryBreed(cheapest: false);
                    if (_s.BreedingNest != null && _s.BreedingNest.Status == EggStatus.Incubating) Do(new TapEggCommand(Rid()));
                    break;
                case Policy.Researcher:
                    DailyPack();
                    ResearchActions();
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
            var pick = (cheapest ? pairs.OrderBy(p => p.fee) : pairs.OrderByDescending(p => p.fee))
                .FirstOrDefault(p => _s.Player.Gold.Micro - _virtualSink >= (p.fee == 80 ? _variant.CommonBreedFeeG : p.fee) * 1_000_000);
            if (pick.a != null) Do(new StartBreedCommand(Rid(), pick.a.Uid, pick.b.Uid));
        }

        private static DayPhase PhaseAt(long now) => DayCycle.PhaseOfMinute((int)(((now / 60_000) % DayCycle.MinutesPerDay + DayCycle.MinutesPerDay) % DayCycle.MinutesPerDay));

        private void ObserveAll()
        {
            foreach (BehaviorDecision d in BehaviorEngine.DecideAll(_s, _now, PhaseAt(_now)))
                if (Research.Observe(_s, d, _now, ResearchPacing).Recorded) _observations++;
        }

        /// <summary>
        /// Researcher: hồ sơ, cho ăn, quan sát, thám hiểm, và (THỬ NGHIỆM) hoàn thành nhiệm vụ cổng khi đề xuất báo đủ — trừ vật liệu trực tiếp
        /// (chưa có lệnh), ghi nhiệm vụ qua hàm engine canonical WorldExpansion.ApplyQuestComplete, rồi mở thế giới bằng lệnh unlockZone thật.
        /// </summary>
        private void ResearchActions()
        {
            foreach (string sp in _s.Dragons.Where(d => d.Location.Kind != LocationKind.Released).Select(d => d.SpeciesId).Distinct().ToList())
                if (!_s.Tutorial.ProfilesViewed.Contains(sp)) Do(new ViewBiologyProfileCommand(Rid(), sp));
            if (!_fedThisSession)
            {
                DragonState eater = _s.Dragons.FirstOrDefault(d => d.Location.Kind == LocationKind.Zone && d.Activity == DragonActivity.Idle);
                if (eater != null)
                {
                    DietId diet = SpeciesCatalog.GetBiology(eater.SpeciesId).DietIds[0];
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
                            Do(new ConsumeFoodCommand(Rid(), drop.Id, uid));
                        }
                    }
                }
            }
            ObserveAll();
            Research.PruneIndividuals(_s);

            // Thám hiểm: rồng Adult không sinh vàng (trong kho), tuyến mở có bảng thưởng; nhận khi xong.
            if (_s.Expedition != null && (_s.Expedition.Status == ExpeditionStatus.Ready || _s.Expedition.FinishAtMs <= _now)) Do(new ClaimExpeditionCommand(Rid()));
            if (_s.Expedition == null)
            {
                foreach (ExpeditionRoute route in ExpeditionCatalog.Routes.Reverse())
                {
                    if (!Dravio.Domain.Expedition.Expeditions.RouteOpen(_s, route) || !route.RewardsDefined) continue;
                    DragonState who = _s.Dragons.Where(d => !_s.ProductionDragonIds.Contains(d.Uid))
                        .FirstOrDefault(d => Dravio.Domain.Expedition.Expeditions.CheckStart(_s, route.Id, d.Uid, out _) == null);
                    if (who != null && Do(new StartExpeditionCommand(Rid(), route.Id, who.Uid)).Ok) break;
                }
            }

            // THỬ NGHIỆM nhiệm vụ cổng (ADR 0204 Q1). Không phải luật production.
            foreach (QuestStatusView q in QuestProposals.Evaluate(_s, Research, ResearchPacing, QuestRules.ProposedForReview).Where(v => v.IsCanonicalGate && v.CanComplete).ToList())
            {
                GameState w = _s.DeepClone();
                foreach (QuestRequirementStatus r in q.Requirements.Where(r => r.Def.Kind == QuestRequirementKind.MaterialDelivery))
                {
                    w.Player.Inventory[r.Def.Param] -= r.Def.Target;
                    _materialsSpent += r.Def.Target;
                }
                WorldExpansion.ApplyQuestComplete(w, q.QuestId, null);
                _s = w;
            }
            foreach (ZoneUnlockRule rule in WorldUnlockCatalog.Rules)
                if (_s.FindZone(rule.ZoneId)?.Unlocked == false && WorldExpansion.MissingRequirements(_s, rule.ZoneId).Count == 0 &&
                    Do(new UnlockZoneCommand(Rid(), rule.ZoneId)).Ok)
                    _result.ZoneUnlockHours[rule.ZoneId] = (_now - Start) / 3_600_000.0;
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
