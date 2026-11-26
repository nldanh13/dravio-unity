using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Dravio.Domain.Behavior;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.World;
using NUnit.Framework;
using static Dravio.Domain.Tests.Support.Fixtures;

namespace Dravio.Domain.Tests.Simulation
{
    /// <summary>
    /// U2.13 Nhiệm vụ 5 — NGHIÊN CỨU cho ăn (không đổi luật production). So sánh ba cách giao phần ăn khi người chơi chọn một rồng rồi cho ăn:
    /// A. hiện tại (<see cref="BehaviorEngine.AssignFood"/>: rồng thích loại đó, rồi UID nhỏ);
    /// B. ưu tiên cá thể được chọn (nếu ăn được), không thì như A;
    /// C. hai chế độ: "cá thể" (phần ăn dành riêng cho rồng được chọn; không ăn được thì bỏ phí) và "tự do" (= A).
    /// B và C chỉ được mô hình hóa trong test này; lệnh feed / consumeFood và cooldown là luật thật. Báo cáo khi đặt DRAVIO_U213_FEED_OUT=&lt;file.md&gt;.
    /// </summary>
    public sealed class U213FeedingResearchTests
    {
        private static readonly long Day0 = new DateTimeOffset(2026, 12, 1, 12, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

        public enum Assign { A_Current, B_SelectedFirst, C_IndividualMode }
        public enum DietChoice { FirstDiet, Favorite }

        public sealed class FeedingScenario
        {
            public string Name;
            public string[] Species;
            /// <summary>Chỉ số rồng bận thám hiểm suốt kịch bản (−1 = không).</summary>
            public int BusyIndex = -1;
            /// <summary>Hai bữa liên tiếp cách 2 s cho cùng mục tiêu (kiểm cooldown 8 s).</summary>
            public bool DoubleFeed;
        }

        public sealed class FeedingResult
        {
            public int Sessions, TargetAte, OtherAte, Wasted, CooldownRejected, BusyTarget;
            public int SeekObservedTarget, EatObservedTarget;
            public double EligiblePerDrop;
            public Dictionary<string, double> XpByUid = new Dictionary<string, double>();
            public double TargetHitPct => Sessions == 0 ? 0 : TargetAte * 100.0 / Sessions;
            /// <summary>Tỉ lệ XP-cho-ăn lớn nhất / nhỏ nhất giữa các cá thể (1 = đều).</summary>
            public double XpSpread => XpByUid.Count == 0 ? 0 : XpByUid.Values.Max() / Math.Max(1, XpByUid.Values.Min());
        }

        public static readonly FeedingScenario[] Scenarios =
        {
            new FeedingScenario { Name = "5 loài khác nhau (đồng cỏ)", Species = new[] { "emberling", "sprout", "zephyr", "sparkwing", "blossom" } },
            new FeedingScenario { Name = "5 rồng cùng loài (Sproutling)", Species = new[] { "sprout", "sprout", "sprout", "sprout", "sprout" } },
            new FeedingScenario { Name = "5 loài, mục tiêu thứ 2 đang thám hiểm", Species = new[] { "emberling", "sprout", "zephyr", "sparkwing", "blossom" }, BusyIndex = 1 },
            new FeedingScenario { Name = "5 loài, hai bữa cách 2 s", Species = new[] { "emberling", "sprout", "zephyr", "sparkwing", "blossom" }, DoubleFeed = true },
        };

        private static GameState Meadow(FeedingScenario sc)
        {
            (GameState s, _) = NewGame();
            s = s.DeepClone();
            s.Dragons.Clear();
            s.ProductionDragonIds.Clear();
            Personality[] ps = { Personality.Calm, Personality.Playful, Personality.Curious, Personality.Shy };
            for (int i = 0; i < sc.Species.Length; i++)
            {
                int k = i;
                s.Dragons.Add(Adult("d" + i, sc.Species[i], d => { d.Location = DragonLocation.Zone("origin", "origin_meadow"); d.Personality = ps[k % ps.Length]; }));
            }
            foreach (ZoneState z in s.Zones) if (z.Id == "origin") z.UnlockedHabitatIds = Zones.HabitatsOfZone("origin");
            foreach (string disc in ProgressCatalog.DiscoveryIds) if (!s.Discoveries.Contains(disc)) s.Discoveries.Add(disc);
            s.Discoveries.Sort(StringComparer.Ordinal);
            s.Tutorial.Status = TutorialStatus.Completed;
            s.Tutorial.Completed = new List<string>(ProgressCatalog.TutorialStepIds);
            s.Clock = new ClockState(Day0, Day0);
            return s;
        }

        private static DayPhase PhaseAt(long now) => DayCycle.PhaseOfMinute((int)(((now / 60_000) % DayCycle.MinutesPerDay + DayCycle.MinutesPerDay) % DayCycle.MinutesPerDay));

        private static string Pick(GameState s, FoodDrop drop, DragonState target, Assign mode, long now)
        {
            bool targetCan = target != null && BehaviorEngine.CanEat(target, drop, now);
            if (mode == Assign.C_IndividualMode) return targetCan ? target.Uid : null;
            if (mode == Assign.B_SelectedFirst && targetCan) return target.Uid;
            return BehaviorEngine.AssignFood(s, drop.ZoneId, drop.HabitatId, now).FirstOrDefault(kv => kv.Value == drop.Id).Key;
        }

        /// <summary>Mỗi phiên (cách nhau 10 phút): chọn mục tiêu xoay vòng, thả một phần ăn, giao theo <paramref name="mode"/>, rồng được giao ăn sau 3 s.</summary>
        public static FeedingResult Run(FeedingScenario sc, Assign mode, DietChoice diet, int sessions = 60)
        {
            GameState s = Meadow(sc);
            var r = new FeedingResult();
            foreach (DragonState d in s.Dragons) r.XpByUid[d.Uid] = 0;
            long now = Day0;
            if (sc.BusyIndex >= 0)
            {
                DispatchResult ex = CommandDispatcher.Dispatch(s, new StartExpeditionCommand(Rid(), "origin_forest", "d" + sc.BusyIndex), new FixedClock(now));
                Assert.That(ex.Ok, Is.True, ex.Error?.ToString());
                s = ex.State;
            }
            var log = new ResearchLog();
            double eligible = 0; int drops = 0;
            for (int i = 0; i < sessions; i++, now += 10 * 60_000)
            {
                // Thám hiểm 15 phút: giữ rồng bận suốt kịch bản bằng cách gửi lại khi về.
                if (sc.BusyIndex >= 0 && s.Expedition != null && s.Expedition.FinishAtMs <= now)
                {
                    s = CommandDispatcher.Dispatch(s, new ClaimExpeditionCommand(Rid()), new FixedClock(now)).State;
                    DispatchResult ex = CommandDispatcher.Dispatch(s, new StartExpeditionCommand(Rid(), "origin_forest", "d" + sc.BusyIndex), new FixedClock(now));
                    if (ex.Ok) s = ex.State;
                }
                DragonState target = s.Dragons[i % s.Dragons.Count];
                r.Sessions++;
                if (target.Activity != DragonActivity.Idle) r.BusyTarget++;
                int meals = sc.DoubleFeed ? 2 : 1;
                for (int m = 0; m < meals; m++)
                {
                    long t = now + m * 2_000;
                    IReadOnlyList<DietId> diets = SpeciesCatalog.GetBiology(target.SpeciesId).DietIds;
                    DietId fav = PersonalityProfiles.Of(target).FavoriteDiet;
                    DietId choice = diet == DietChoice.Favorite && diets.Contains(fav) ? fav : diets[0];
                    DispatchResult fr = CommandDispatcher.Dispatch(s, new FeedCommand(Rid(), choice, "origin", "origin_meadow"), new FixedClock(t));
                    if (!fr.Ok) continue;
                    s = fr.State;
                    FoodDrop drop = s.Food.Drops.Last();
                    drops++;
                    eligible += s.Dragons.Count(d => BehaviorEngine.CanEat(d, drop, t));
                    DragonState tgt = s.FindDragon(target.Uid);
                    string who = Pick(s, drop, tgt, mode, t);
                    if (who == null) { r.Wasted++; continue; }
                    // "Tìm ăn" quan sát được khi chính mục tiêu đi tới phần ăn (ở A: quyết định thật của engine).
                    if (who == target.Uid && m == 0)
                    {
                        bool seek = mode == Assign.A_Current ? BehaviorEngine.Decide(s, target.Uid, t + 1_000, PhaseAt(t)).Behavior == DragonBehavior.SeekFood : true;
                        if (seek) r.SeekObservedTarget++;
                    }
                    DispatchResult c = CommandDispatcher.Dispatch(s, new ConsumeFoodCommand(Rid(), drop.Id, who), new FixedClock(t + 3_000));
                    if (!c.Ok)
                    {
                        if (c.Error.Code == DomainErrorCode.FeedCooldown) r.CooldownRejected++;
                        r.Wasted++;
                        continue;
                    }
                    s = c.State;
                    FoodConsumed fc = c.Events.OfType<FoodConsumed>().Single();
                    r.XpByUid[who] += fc.XpGainedSec;
                    if (who == target.Uid) { if (m == 0) r.TargetAte++; } else r.OtherAte++;
                    if (who == target.Uid && m == 0)
                    {
                        BehaviorDecision e = BehaviorEngine.Decide(s, target.Uid, t + 4_000, PhaseAt(t));
                        if (e.Behavior == DragonBehavior.Eat && log.Observe(s, e, t + 4_000, ResearchPacing.Production).Recorded) r.EatObservedTarget++;
                    }
                }
            }
            r.EligiblePerDrop = drops == 0 ? 0 : eligible / drops;
            return r;
        }

        [Test]
        public void Current_rule_starves_attention_for_high_uid_dragons_while_selected_first_reaches_every_target()
        {
            FeedingScenario sameSpecies = Scenarios[1];
            FeedingResult a = Run(sameSpecies, Assign.A_Current, DietChoice.FirstDiet);
            FeedingResult b = Run(sameSpecies, Assign.B_SelectedFirst, DietChoice.FirstDiet);
            FeedingResult c = Run(sameSpecies, Assign.C_IndividualMode, DietChoice.FirstDiet);
            Assert.That(a.TargetHitPct, Is.LessThan(50), "A: cùng loài, cùng khẩu vị → phần ăn về rồng thích loại đó / UID nhỏ");
            Assert.That(b.TargetHitPct, Is.EqualTo(100));
            Assert.That(c.TargetHitPct, Is.EqualTo(100));
            Assert.That(b.Wasted, Is.EqualTo(0));
            // Production không đổi: A vẫn là AssignFood thật.
            Assert.That(a.TargetAte + a.OtherAte + a.Wasted, Is.EqualTo(a.Sessions));
        }

        [Test]
        public void Busy_target_is_never_fed_and_individual_mode_wastes_that_meal()
        {
            FeedingScenario busy = Scenarios[2];
            FeedingResult b = Run(busy, Assign.B_SelectedFirst, DietChoice.Favorite);
            FeedingResult c = Run(busy, Assign.C_IndividualMode, DietChoice.Favorite);
            Assert.That(b.BusyTarget, Is.GreaterThan(0));
            Assert.That(c.Wasted, Is.GreaterThanOrEqualTo(c.BusyTarget), "C cá thể: rồng bận không ăn được → bữa bỏ phí");
            Assert.That(b.Wasted, Is.LessThan(c.Wasted), "B lùi về A khi mục tiêu bận");
        }

        [Test]
        public void Write_u213_feeding_report_when_requested()
        {
            string outPath = Environment.GetEnvironmentVariable("DRAVIO_U213_FEED_OUT");
            if (outPath == null) Assert.Ignore("Đặt DRAVIO_U213_FEED_OUT để sinh báo cáo nghiên cứu cho ăn.");
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.AppendLine("| Kịch bản | Chọn thức ăn | Cách giao | Mục tiêu được ăn | Rồng khác ăn | Bỏ phí | Bị cooldown | Mục tiêu bận | Quan sát \"tìm ăn\" mục tiêu | Quan sát \"ăn\" mục tiêu | Rồng ăn được / phần | XP cho ăn max / min |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (FeedingScenario sc in Scenarios)
                foreach (DietChoice dc in new[] { DietChoice.FirstDiet, DietChoice.Favorite })
                    foreach (Assign m in new[] { Assign.A_Current, Assign.B_SelectedFirst, Assign.C_IndividualMode })
                    {
                        FeedingResult r = Run(sc, m, dc);
                        sb.AppendLine($"| {sc.Name} | {dc} | {m} | {r.TargetHitPct.ToString("0", inv)} % | {r.OtherAte} | {r.Wasted} | {r.CooldownRejected} | {r.BusyTarget} | {r.SeekObservedTarget}/{r.Sessions} | {r.EatObservedTarget}/{r.Sessions} | {r.EligiblePerDrop.ToString("0.0", inv)} | {r.XpSpread.ToString("0.0", inv)} |");
                    }
            System.IO.File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(false));
        }
    }
}
