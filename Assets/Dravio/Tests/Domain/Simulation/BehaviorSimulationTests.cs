using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Dravio.Domain.Behavior;
using Dravio.Domain.Biology;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Queries;
using Dravio.Domain.Tests.Support;
using NUnit.Framework;

namespace Dravio.Domain.Tests.Simulation
{
    /// <summary>
    /// U2.9 — mô phỏng cân bằng của Behavioral Engine và các luật đề xuất (ADR 0203).
    /// Test CI: kinh tế không đổi khi chèn truy vấn hành vi; luật comfort đề xuất không chặn rồng nào ở môi trường canonical.
    /// Báo cáo đầy đủ chỉ sinh khi đặt DRAVIO_BEHAVIOR_OUT=&lt;file.md&gt;.
    /// </summary>
    public sealed class BehaviorSimulationTests
    {
        private static readonly DayPhase[] Phases = { DayPhase.Dawn, DayPhase.Day, DayPhase.Dusk, DayPhase.Night };
        private static readonly Personality[] Archetypes = { Personality.Playful, Personality.Calm, Personality.Curious, Personality.Shy };

        [Test]
        public void Economy_is_identical_with_behavior_queries_interleaved()
        {
            foreach (Policy p in new[] { Policy.Collector, Policy.BreedTrader })
                foreach (int seed in new[] { 3, 11 })
                {
                    var plain = new EconomySimulator(p, EconomySimulationTests.Schedules[1], seed);
                    RunResult a = plain.Run(new double[] { 24, 72 });
                    var observed = new EconomySimulator(p, EconomySimulationTests.Schedules[1], seed);
                    int queries = 0;
                    var journal = new ObservationJournal();
                    observed.AfterCommand = (s, now) =>
                    {
                        DayPhase ph = DayCycle.PhaseOfMinute((int)((now / 60_000) % DayCycle.MinutesPerDay));
                        foreach (BehaviorDecision d in BehaviorQueries.All(s, now, ph)) { journal.Observe(d, now, ResearchRules.ProposedForReview); queries++; }
                        foreach (DragonState d in s.Dragons) BehaviorQueries.Ecology(s, d.Uid, EcologyComfortRules.ProposedForReview);
                    };
                    RunResult b = observed.Run(new double[] { 24, 72 });
                    Assert.That(queries, Is.GreaterThan(100));
                    foreach (double h in new double[] { 24, 72 })
                    {
                        Assert.That(b.At[h].GoldG, Is.EqualTo(a.At[h].GoldG), p + " seed " + seed + " @" + h);
                        Assert.That(b.At[h].EarnedG, Is.EqualTo(a.At[h].EarnedG));
                        Assert.That(b.At[h].RateGPerSec, Is.EqualTo(a.At[h].RateGPerSec));
                        Assert.That(b.At[h].Dragons, Is.EqualTo(a.At[h].Dragons));
                    }
                    Assert.That(MiniJson.Diff(StateJson.ToJson(plain.State), StateJson.ToJson(observed.State)), Is.Null);
                }
        }

        [Test]
        public void Proposed_comfort_rule_never_blocks_a_canonical_placement()
        {
            foreach (DragonSpecies sp in SpeciesCatalog.All)
                foreach (string hid in sp.Biology.HabitatRules)
                {
                    int score = Suitability.ForSpecies(sp.Id, hid).Score;
                    foreach (Difficulty diff in new[] { Difficulty.Cozy, Difficulty.Ecosystem })
                        Assert.That(EcologyComfort.Target(score, false, diff, EcologyComfortRules.ProposedForReview), Is.GreaterThanOrEqualTo(HealthCatalog.BreedMinComfort),
                            sp.Id + "@" + hid + " " + diff);
                }
        }

        // ------------------------------------------------------------------ báo cáo

        private static GameState Pair(string species, string habitatId, Personality p)
        {
            (GameState s, _) = Fixtures.NewGame();
            s = s.DeepClone();
            s.Dragons.Clear(); s.ProductionDragonIds.Clear();
            string z = HabitatCatalog.ZoneOf(habitatId);
            s.Dragons.Add(Fixtures.Adult("main", species, d => { d.Location = DragonLocation.Zone(z, habitatId); d.Personality = p; }));
            s.Dragons.Add(Fixtures.Adult("buddy", species, d => { d.Location = DragonLocation.Zone(z, habitatId); d.Personality = Personality.Calm; }));
            return s;
        }

        /// <summary>Một ngày 24 h, mỗi phút một mẫu (pha theo giờ), cho rồng "main". Trả số phút theo hành vi.</summary>
        private static Dictionary<DragonBehavior, int> Day(GameState s, long dayStart)
        {
            var c = BehaviorCatalog.All.ToDictionary(b => b, b => 0);
            for (int m = 0; m < DayCycle.MinutesPerDay; m++)
                c[BehaviorEngine.Decide(s, "main", dayStart + m * 60_000L, DayCycle.PhaseOfMinute(m)).Behavior.Value]++;
            return c;
        }

        private static string Pct(double part, double total) => total <= 0 ? "–" : (100.0 * part / total).ToString("0", CultureInfo.InvariantCulture) + " %";

        [Test]
        public void Write_behavior_report_when_requested()
        {
            string outPath = Environment.GetEnvironmentVariable("DRAVIO_BEHAVIOR_OUT");
            if (outPath == null) Assert.Ignore("Đặt DRAVIO_BEHAVIOR_OUT để sinh báo cáo mô phỏng hành vi.");
            long day0 = new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
            DragonBehavior[] cols = BehaviorCatalog.All.Where(b => b != DragonBehavior.SeekFood && b != DragonBehavior.Eat).ToArray();
            var sb = new StringBuilder();
            sb.AppendLine("<!-- Sinh bởi BehaviorSimulationTests.Write_behavior_report_when_requested. Không sửa tay. -->");
            sb.AppendLine();

            // 1. Ma trận.
            sb.AppendLine("### 1. Ma trận hành vi 16 loài");
            sb.AppendLine();
            sb.AppendLine("Ô ghi microhabitat nơi hành vi xảy ra được, hoặc lý do loài không làm được. Idle, Rest, Explore, Play, SeekFood, Eat: mọi loài, mọi microhabitat hợp lệ. Sleep: mọi loài (theo nhịp sinh học, hoặc Energy < 30).");
            sb.AppendLine();
            sb.AppendLine("| Loài | Chu kỳ | Di chuyển | Bask | Swim | Fly | Burrow |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            foreach (SpeciesBehaviorProfile m in BehaviorQueries.Matrix())
            {
                string Cell(DragonBehavior b)
                {
                    BehaviorAvailability a = m.Behaviors.Single(x => x.Behavior == b);
                    if (a.SpeciesBlock != BehaviorBlock.None) return "✗ " + a.SpeciesBlock.ToWire();
                    return a.Habitats.Count == 0 ? "✗ (không có microhabitat phù hợp)" : "✓ " + string.Join(", ", a.Habitats);
                }
                sb.AppendLine($"| {m.SpeciesId} | {m.Activity.ToWire()} | {string.Join("+", m.Locomotion.Select(l => l.ToWire()))} | {Cell(DragonBehavior.Bask)} | {Cell(DragonBehavior.Swim)} | {Cell(DragonBehavior.Fly)} | {Cell(DragonBehavior.Burrow)} |");
            }
            sb.AppendLine();

            // 2. Phân bổ thời gian theo loài.
            sb.AppendLine("### 2. Phân bổ thời gian trong một ngày 24 h (trung bình 4 cá tính × mọi microhabitat hợp lệ, Adult, có một bạn cùng chỗ, không thức ăn)");
            sb.AppendLine();
            sb.AppendLine("| Loài | " + string.Join(" | ", cols.Select(c => c.ToWire())) + " |");
            sb.AppendLine("|---|" + string.Join("|", cols.Select(_ => "---")) + "|");
            foreach (DragonSpecies sp in SpeciesCatalog.All)
            {
                var sum = BehaviorCatalog.All.ToDictionary(b => b, b => 0); int total = 0;
                foreach (string hid in sp.Biology.HabitatRules)
                    foreach (Personality p in Archetypes)
                    {
                        Dictionary<DragonBehavior, int> d = Day(Pair(sp.Id, hid, p), day0);
                        foreach (var kv in d) { sum[kv.Key] += kv.Value; total += kv.Value; }
                    }
                sb.AppendLine($"| {sp.Id} | " + string.Join(" | ", cols.Select(c => Pct(sum[c], total))) + " |");
            }
            sb.AppendLine();

            // 3. Theo cá tính.
            sb.AppendLine("### 3. Phân bổ theo cá tính (trung bình 16 loài, ban ngày 07:00–17:30)");
            sb.AppendLine();
            sb.AppendLine("| Cá tính | " + string.Join(" | ", cols.Select(c => c.ToWire())) + " |");
            sb.AppendLine("|---|" + string.Join("|", cols.Select(_ => "---")) + "|");
            foreach (Personality p in Archetypes)
            {
                var sum = BehaviorCatalog.All.ToDictionary(b => b, b => 0); int total = 0;
                foreach (DragonSpecies sp in SpeciesCatalog.All)
                    foreach (string hid in sp.Biology.HabitatRules)
                    {
                        GameState s = Pair(sp.Id, hid, p);
                        for (int m = DayCycle.DayStartMinute; m < DayCycle.DuskStartMinute; m++)
                        { sum[BehaviorEngine.Decide(s, "main", day0 + m * 60_000L, DayPhase.Day).Behavior.Value]++; total++; }
                    }
                sb.AppendLine($"| {p.ToWire()} | " + string.Join(" | ", cols.Select(c => Pct(sum[c], total))) + " |");
            }
            sb.AppendLine();

            // 4. Comfort đề xuất C1.
            sb.AppendLine("### 4. Luật comfort đề xuất C1 (TẮT ở production)");
            sb.AppendLine();
            sb.AppendLine("Mục tiêu comfort = max(sàn, suitability + 5 nếu là microhabitat ưa thích). Sàn Cozy 60, Ecosystem 40. Online đổi 1 điểm / 6 phút về mục tiêu; offline chỉ tăng. Ngưỡng lai / thám hiểm: comfort ≥ 60.");
            sb.AppendLine();
            sb.AppendLine("| Loài | Microhabitat | Suitability canonical | Mục tiêu Ecosystem | Chất lượng −20 | Chất lượng −40 | Chặn lai ở −40 (Ecosystem)? | Giờ online 100 → mục tiêu ở −40 |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|");
            int blocked = 0, rows = 0;
            foreach (DragonSpecies sp in SpeciesCatalog.All)
                foreach (string hid in sp.Biology.HabitatRules)
                {
                    HabitatProfile h = Fixtures.Habitat(hid);
                    int Score(int drop)
                    {
                        QualityValues q = h.Quality;
                        foreach (QualityKey k in new[] { QualityKey.AirQuality, QualityKey.WaterQuality, QualityKey.Vegetation, QualityKey.FoodAvailability })
                            q = q.With(k, Math.Max(0, q[k] - drop));
                        return Suitability.Evaluate(sp.Id, hid, Fixtures.Env(h, quality: q)).Score;
                    }
                    int s0 = Score(0), s20 = Score(20), s40 = Score(40);
                    int t40 = EcologyComfort.Target(s40, false, Difficulty.Ecosystem, EcologyComfortRules.ProposedForReview);
                    bool block = t40 < HealthCatalog.BreedMinComfort;
                    if (block) blocked++;
                    rows++;
                    double hours = (100 - t40) * EcologyComfortRules.ProposedForReview.StepMs / 3_600_000.0;
                    sb.AppendLine($"| {sp.Id} | {hid} | {s0} | {EcologyComfort.Target(s0, false, Difficulty.Ecosystem, EcologyComfortRules.ProposedForReview)} | {s20} | {s40} | {(block ? "có" : "không")} | {hours.ToString("0.#", CultureInfo.InvariantCulture)} |");
                }
            sb.AppendLine();
            sb.AppendLine($"Tổng: {rows} cặp loài × microhabitat hợp lệ; {blocked} cặp sẽ bị chặn lai ở Ecosystem nếu chất lượng giảm 40 điểm (P7 giả định). Ở Cozy: 0 (sàn 60).");
            sb.AppendLine();

            // 5. Nghiên cứu.
            sb.AppendLine("### 5. Nghiên cứu: số tập tính cần ghi nhận và số lần quan sát để đạt Ethology (đề xuất D2, TẮT)");
            sb.AppendLine();
            sb.AppendLine("Mô phỏng: một rồng Calm, quan sát một lần mỗi nhịp 60 s, lần lượt ở mỗi microhabitat hợp lệ, xoay vòng 4 pha; thêm 1 lần cho ăn và 1 drop thức ăn cho mỗi rồng. Đếm số lần quan sát tới khi đủ.");
            sb.AppendLine();
            sb.AppendLine("| Loài | Tập tính cần ghi nhận | Số lần quan sát (≈ phút chơi) |");
            sb.AppendLine("|---|---|---|");
            foreach (DragonSpecies sp in SpeciesCatalog.All)
            {
                List<DragonBehavior> need = Research.ObservableBehaviors(sp.Id);
                var journal = new ObservationJournal();
                GameState s = Pair(sp.Id, sp.Biology.HabitatRules[0], Personality.Calm);
                DragonState d = s.FindDragon("main");
                d.LastFeedXpAtMs = day0;
                journal.Observe(BehaviorEngine.Decide(s, "main", day0 + 1, DayPhase.Day), day0 + 1, ResearchRules.ProposedForReview);
                d.LastFeedXpAtMs = null;
                // Hai drop: một cho "buddy" (UID đứng trước), một cho "main".
                foreach (string fid in new[] { "f1", "f2" })
                    s.Food.Drops.Add(new FoodDrop { Id = fid, DietId = sp.Biology.DietIds[0], ZoneId = d.Location.ZoneId, HabitatId = d.Location.HabitatId, SpawnedAtMs = day0 + 60_000, ExpiresAtMs = day0 + 70_000 });
                journal.Observe(BehaviorEngine.Decide(s, "main", day0 + 60_000, DayPhase.Day), day0 + 60_000, ResearchRules.ProposedForReview);
                s.Food.Drops.Clear();
                int n = 2; long t = day0 + 120_000;
                while (n < 5000 && !need.All(b => journal.BehaviorsOf(sp.Id).Contains(b)))
                {
                    string hid = sp.Biology.HabitatRules[(n / 40) % sp.Biology.HabitatRules.Count];
                    d.Location = DragonLocation.Zone(HabitatCatalog.ZoneOf(hid), hid);
                    s.FindDragon("buddy").Location = d.Location;
                    journal.Observe(BehaviorEngine.Decide(s, "main", t, Phases[(n / 10) % 4]), t, ResearchRules.ProposedForReview);
                    n++; t += BehaviorCatalog.EpochMs;
                }
                sb.AppendLine($"| {sp.Id} | {need.Count} ({string.Join(", ", need.Select(b => b.ToWire()))}) | {n} |");
            }
            sb.AppendLine();
            File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(false));
        }
    }
}
