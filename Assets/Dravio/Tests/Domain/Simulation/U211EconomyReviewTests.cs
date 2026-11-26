using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Dravio.Domain.Behavior;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Progression;
using NUnit.Framework;

namespace Dravio.Domain.Tests.Simulation
{
    /// <summary>
    /// U2.11 (ADR 0206) — đánh giá kinh tế sau Schema v9 (luật thật E1, E2, E5, R11) cộng luật U2.11 (nhiệm vụ thật, mốc thám hiểm).
    /// CI chạy bất biến ngắn. Báo cáo đầy đủ khi đặt DRAVIO_U211_SIM_OUT=&lt;file.md&gt; (DRAVIO_SIM_SEEDS, mặc định 20).
    /// </summary>
    public sealed class U211EconomyReviewTests
    {
        private static readonly double[] Horizons = { 24 * 7, 24 * 30, 24 * 90 };

        [Test]
        public void Live_researcher_claims_quests_through_the_real_command_and_materials_never_go_negative()
        {
            var sim = new EconomySimulator(Policy.Researcher, EconomySimulationTests.Schedules[1], 2, EconomyVariant.Live);
            RunResult r = sim.Run(new double[] { 24 * 20 });
            Snapshot x = r.At[480];
            Assert.That(x.QuestsCompleted, Is.GreaterThanOrEqualTo(2), "ít nhất Q_ORIGIN_FIELD_NOTES và Q_ORIGIN_FOREST_SURVEY");
            Assert.That(x.QuestsCompleted, Is.EqualTo(sim.State.CompletedQuests.Count + sim.State.CompletedSideQuests.Count));
            Assert.That(x.MilestoneMaterials, Is.GreaterThan(0), "mốc thám hiểm đầu tiên");
            Assert.That(sim.State.Player.Inventory.Values.All(v => v >= 0), Is.True);
            Assert.That(r.QuestHours.ContainsKey("Q_ORIGIN_FIELD_NOTES"), Is.True);
            if (r.ZoneUnlockHours["ocean"].HasValue) Assert.That(r.QuestHours["QUEST_AQUATIC_EXPLORATION"], Is.LessThanOrEqualTo(r.ZoneUnlockHours["ocean"].Value));
            Assert.That(GameStateValidator.Validate(sim.State).IsValid, Is.True, GameStateValidator.Validate(sim.State).ToString());
            Assert.That(sim.State.Relations.Count, Is.GreaterThan(0), "rồng sống chung có độ quen theo thời gian");
        }

        private static double Pct(IEnumerable<double> xs, double q)
        {
            var s = xs.OrderBy(x => x).ToList();
            if (s.Count == 0) return double.NaN;
            double idx = q * (s.Count - 1);
            int lo = (int)Math.Floor(idx), hi = (int)Math.Ceiling(idx);
            return s[lo] + (s[hi] - s[lo]) * (idx - lo);
        }

        private static string F(double v) => double.IsNaN(v) ? "—" : Math.Abs(v) >= 1_000_000 ? (v / 1_000_000).ToString("0.00", CultureInfo.InvariantCulture) + " M"
            : Math.Abs(v) >= 100 ? v.ToString("N0", CultureInfo.InvariantCulture) : v.ToString("0.#", CultureInfo.InvariantCulture);

        private static double Faucets(Snapshot x) => x.EarnedG + x.SoldG + x.RewardsG;
        private static double Sinks(Snapshot x) => x.SpentPacksG + x.SpentBreedG + x.SpentUpgradesG + x.ProjectsG + x.VirtualSinkG;

        [Test]
        public void Write_u211_economy_review_when_requested()
        {
            string outPath = Environment.GetEnvironmentVariable("DRAVIO_U211_SIM_OUT");
            if (outPath == null) Assert.Ignore("Đặt DRAVIO_U211_SIM_OUT để sinh báo cáo U2.11.");
            int seeds = int.TryParse(Environment.GetEnvironmentVariable("DRAVIO_SIM_SEEDS"), out int n) ? n : 20;
            var policies = new[] { Policy.Relaxed, Policy.Collector, Policy.Breeder, Policy.Researcher, Policy.StarterTrader, Policy.BreedTrader };
            var sb = new StringBuilder();
            var quests = new StringBuilder();
            sb.AppendLine("<!-- Sinh bởi U211EconomyReviewTests.Write_u211_economy_review_when_requested; seeds = " + seeds + ", biến thể live -->");
            sb.AppendLine();
            sb.AppendLine("| Người chơi | Lịch | Số dư 30 ng | Số dư 90 ng (P10 / trung vị / P90) | Tiêu / nguồn 90 ng | Dự án 90 ng | Gỗ + hạt cho dự án | Vật liệu giao nhiệm vụ | Vật liệu thưởng mốc | Vật liệu còn giữ | Chuyến thám hiểm | Nhiệm vụ | Danh hiệu | Cặp bạn bè | Thủy Vực (ngày, % seed) | Thiên Giới (ngày, % seed) |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (Policy p in policies)
                foreach (Schedule sc in EconomySimulationTests.Schedules)
                {
                    var runs = new RunResult[seeds];
                    System.Threading.Tasks.Parallel.For(0, seeds, i => runs[i] = new EconomySimulator(p, sc, i + 1, EconomyVariant.Live).Run(Horizons));
                    double last = Horizons.Last();
                    Func<double, Func<Snapshot, double>, double, double> P = (h, f, q) => Pct(runs.Select(r => f(r.At[h])), q);
                    string Zone(string z)
                    {
                        var hs = runs.Select(r => r.ZoneUnlockHours[z]).Where(x => x.HasValue).Select(x => x.Value / 24).ToList();
                        return hs.Count == 0 ? "— (0%)" : $"{F(Pct(hs, .5))} ({hs.Count * 100 / runs.Length}%)";
                    }
                    sb.AppendLine($"| {p} | {sc.Name} | {F(P(720, x => x.GoldG, .5))} | {F(P(last, x => x.GoldG, .1))} / {F(P(last, x => x.GoldG, .5))} / {F(P(last, x => x.GoldG, .9))} " +
                        $"| {P(last, x => Sinks(x) / Math.Max(1, Faucets(x)), .5).ToString("0.00", CultureInfo.InvariantCulture)} | {F(P(last, x => x.Projects, .5))} | {F(P(last, x => x.ProjectMaterials, .5))} " +
                        $"| {F(P(last, x => x.QuestMaterials, .5))} | {F(P(last, x => x.MilestoneMaterials, .5))} | {F(P(last, x => x.MaterialsHeld, .5))} | {F(P(last, x => x.ExpeditionsClaimed, .5))} " +
                        $"| {F(P(last, x => x.QuestsCompleted, .5))} | {F(P(last, x => x.TitlesEarned, .5))} | {F(P(last, x => x.FriendPairs, .5))} | {Zone("ocean")} | {Zone("sky")} |");
                    if (p == Policy.Researcher || p == Policy.Relaxed)
                    {
                        quests.AppendLine($"#### {p} — {sc.Name}");
                        quests.AppendLine();
                        quests.AppendLine("| Nhiệm vụ | % seed nhận trong 90 ngày | Ngày nhận (trung vị) |");
                        quests.AppendLine("|---|---|---|");
                        foreach (QuestProposalDef q in QuestProposals.All)
                        {
                            var hs = runs.Where(r => r.QuestHours.ContainsKey(q.Id)).Select(r => r.QuestHours[q.Id] / 24).ToList();
                            quests.AppendLine($"| `{q.Id}` | {hs.Count * 100 / runs.Length}% | {F(Pct(hs, .5))} |");
                        }
                        quests.AppendLine();
                    }
                }
            sb.AppendLine();
            sb.AppendLine("### Nhiệm vụ theo thời gian");
            sb.AppendLine();
            sb.Append(quests);
            File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(false));
        }
    }
}
