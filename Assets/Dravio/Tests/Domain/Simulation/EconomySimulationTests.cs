using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Tests.Support;
using NUnit.Framework;

namespace Dravio.Domain.Tests.Simulation
{
    /// <summary>
    /// Mô phỏng kinh tế (U2.7). Test chạy trong CI kiểm bất biến trên vài seed (tất định, không âm vàng, không vượt trần sản xuất).
    /// Báo cáo đầy đủ (nhiều seed, 1 h / 24 h / 7 ngày / 30 ngày) chỉ sinh khi đặt DRAVIO_SIM_OUT=&lt;file.md&gt; (và DRAVIO_SIM_SEEDS, mặc định 100).
    /// </summary>
    public sealed class EconomySimulationTests
    {
        public static readonly Schedule[] Schedules =
        {
            new Schedule("5 phút/ngày", (0, 5)),
            new Schedule("3 × 10 phút/ngày", (0, 10), (4, 10), (11, 10)),
            new Schedule("60 phút/ngày", (0, 30), (11, 30)),
        };

        private static readonly double[] Horizons = { 1, 24, 24 * 7, 24 * 30 };

        [Test]
        public void Simulation_is_deterministic_never_goes_negative_and_respects_the_5_producer_cap()
        {
            foreach (Policy p in Enum.GetValues(typeof(Policy)))
            {
                RunResult a = new EconomySimulator(p, Schedules[1], 7).Run(new double[] { 1, 24 * 3 });
                var simB = new EconomySimulator(p, Schedules[1], 7);
                RunResult b = simB.Run(new double[] { 1, 24 * 3 });
                Assert.That(a.At[72].GoldG, Is.EqualTo(b.At[72].GoldG), p + " deterministic");
                Assert.That(a.At[72].RateGPerSec, Is.LessThanOrEqualTo(5 * 0.55 * 5), p + ": tối đa 5 rồng sinh vàng ở G1");
                Assert.That(GameStateValidator.Validate(simB.State).IsValid, Is.True, p + ": " + GameStateValidator.Validate(simB.State));
                Assert.That(a.At[72].EarnedG, Is.GreaterThan(0));
            }
        }

        private static double Pct(IEnumerable<double> xs, double q)
        {
            var s = xs.OrderBy(x => x).ToList();
            if (s.Count == 0) return double.NaN;
            double idx = q * (s.Count - 1);
            int lo = (int)Math.Floor(idx), hi = (int)Math.Ceiling(idx);
            return s[lo] + (s[hi] - s[lo]) * (idx - lo);
        }

        private static string F(double v) => double.IsNaN(v) ? "—" : v >= 100 ? v.ToString("N0", CultureInfo.InvariantCulture) : v.ToString("0.##", CultureInfo.InvariantCulture);

        [Test]
        public void Probe_timing_when_requested()
        {
            if (Environment.GetEnvironmentVariable("DRAVIO_SIM_PROBE") == null) Assert.Ignore("probe");
            double days = double.Parse(Environment.GetEnvironmentVariable("DRAVIO_SIM_PROBE"), CultureInfo.InvariantCulture);
            foreach (Policy p in Enum.GetValues(typeof(Policy)))
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var sim = new EconomySimulator(p, Schedules[2], 1);
                RunResult r = sim.Run(new[] { days * 24 });
                TestContext.Progress.WriteLine($"{p}: {sw.ElapsedMilliseconds} ms, dragons {r.At[days * 24].Dragons}, gold {r.At[days * 24].GoldG}, packs {r.At[days * 24].PacksBought}");
            }
        }

        [Test]
        public void Write_economy_report_when_requested()
        {
            string outPath = Environment.GetEnvironmentVariable("DRAVIO_SIM_OUT");
            if (outPath == null) Assert.Ignore("Đặt DRAVIO_SIM_OUT để sinh báo cáo mô phỏng.");
            int seeds = int.TryParse(Environment.GetEnvironmentVariable("DRAVIO_SIM_SEEDS"), out int n) ? n : 100;
            var sb = new StringBuilder();
            sb.AppendLine("<!-- Sinh bởi EconomySimulationTests.Write_economy_report_when_requested; seeds = " + seeds + " -->");
            foreach (Policy p in Enum.GetValues(typeof(Policy)))
                foreach (Schedule sc in Schedules)
                {
                    var runs = Enumerable.Range(1, seeds).Select(i => new EconomySimulator(p, sc, i).Run(Horizons)).ToList();
                    sb.AppendLine($"#### {p} — {sc.Name}");
                    sb.AppendLine();
                    sb.AppendLine("| Mốc | Số dư G (P10 / trung vị / P90) | Vàng sản xuất G (trung vị) | Bán G | Chi gói G | Chi lai G | Thưởng G | G/s cuối | Rồng | Loài | Ô codex | Thành tựu |");
                    sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|");
                    foreach (double h in Horizons)
                    {
                        var at = runs.Select(r => r.At[h]).ToList();
                        string label = h < 24 ? h + " giờ" : h == 24 ? "24 giờ" : (h / 24) + " ngày";
                        sb.AppendLine($"| {label} | {F(Pct(at.Select(x => (double)x.GoldG), .1))} / {F(Pct(at.Select(x => (double)x.GoldG), .5))} / {F(Pct(at.Select(x => (double)x.GoldG), .9))} " +
                            $"| {F(Pct(at.Select(x => (double)x.EarnedG), .5))} | {F(Pct(at.Select(x => (double)x.SoldG), .5))} | {F(Pct(at.Select(x => (double)x.SpentPacksG), .5))} " +
                            $"| {F(Pct(at.Select(x => (double)x.SpentBreedG), .5))} | {F(Pct(at.Select(x => (double)x.RewardsG), .5))} | {F(Pct(at.Select(x => x.RateGPerSec), .5))} " +
                            $"| {F(Pct(at.Select(x => (double)x.Dragons), .5))} | {F(Pct(at.Select(x => (double)x.Species), .5))} | {F(Pct(at.Select(x => (double)x.Cells), .5))} | {F(Pct(at.Select(x => (double)x.Achievements), .5))} |");
                    }
                    sb.AppendLine();
                    sb.Append("Lần đầu đủ tiền (giờ, trung vị; tỷ lệ seed đạt trong 30 ngày): ");
                    sb.AppendLine(string.Join(" · ", PackCatalog.All.Select(pk =>
                    {
                        var hs = runs.Select(r => r.FirstAffordHours[pk.Id]).Where(x => x.HasValue).Select(x => x.Value).ToList();
                        return $"{pk.Id} {pk.PriceG:N0} G: {F(Pct(hs, .5))} h ({hs.Count * 100 / runs.Count}%)";
                    })));
                    sb.AppendLine();
                }
            File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(false));
        }
    }
}
