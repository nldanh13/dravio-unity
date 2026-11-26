using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using NUnit.Framework;

namespace Dravio.Domain.Tests.Simulation
{
    /// <summary>
    /// U2.13 (ADR 0208) — mô phỏng bảng thưởng sky_cloud (chưa duyệt, chỉ trong mô phỏng) và trứng Aurorawing (đã duyệt).
    /// CI: một hành trình ngắn với bảng A. Báo cáo đầy đủ khi đặt DRAVIO_U213_SKY_OUT=&lt;thư mục&gt; (DRAVIO_SIM_SEEDS mặc định 3,
    /// DRAVIO_SIM_DAYS mặc định 365, DRAVIO_SIM_POLICIES, DRAVIO_SKY_VARIANTS lọc theo Id).
    /// </summary>
    public sealed class U213SkyCloudSimulationTests
    {
        [Test]
        public void Guided_player_with_table_A_flies_sky_trips_gets_one_aurorawing_and_no_gold_from_the_route()
        {
            var sim = new EconomySimulator(Policy.Guided, EconomySimulationTests.Schedules[2], 1, EconomyVariant.LiveWithSky(SkyCloudBalance.ProposedA));
            RunResult r = sim.Run(new double[] { 24 * 45 });
            Snapshot x = r.At[24 * 45];
            Assert.That(x.ZonesUnlocked, Is.EqualTo(3));
            Assert.That(x.SkyClaims, Is.GreaterThanOrEqualTo(SkyCloudBalance.AurorawingEggClaims), "đủ 15 chuyến sân mây trong 45 ngày (60 phút/ngày)");
            Assert.That(r.AurorawingGrants, Is.EqualTo(1), "trứng trao đúng một lần");
            Assert.That(x.OwnsAurorawing, Is.True);
            Assert.That(x.Stardust, Is.GreaterThan(0));
            Assert.That(sim.State.GrantedRewards.Count(id => id == SkyCloudBalance.AurorawingEggRewardId), Is.EqualTo(1));
            Assert.That(sim.State.Player.Inventory.Values.All(v => v >= 0), Is.True);
            using (SkyCloudBalance.Use(SkyCloudBalance.ProposedA))
                Assert.That(GameStateValidator.Validate(sim.State).IsValid, Is.True, GameStateValidator.Validate(sim.State).ToString());
            // Không vàng từ tuyến: vàng = 150 + sản xuất + bán + thành tựu − chi tiêu (như bất biến U2.12).
            long expected = 150 + x.EarnedG + x.SoldG + x.RewardsG - x.SpentPacksG - x.SpentBreedG - x.SpentUpgradesG - x.ProjectsG;
            Assert.That(Math.Abs(x.GoldG - expected), Is.LessThanOrEqualTo(8));
        }

        [Test]
        public void Without_an_approved_table_no_simulated_player_ever_flies_to_the_cloud_terrace()
        {
            var sim = new EconomySimulator(Policy.Guided, EconomySimulationTests.Schedules[2], 1, EconomyVariant.Live);
            RunResult r = sim.Run(new double[] { 24 * 30 });
            Snapshot x = r.At[24 * 30];
            Assert.That(x.SkyClaims, Is.EqualTo(0));
            Assert.That(x.Stardust, Is.EqualTo(0));
            Assert.That(r.AurorawingGrants, Is.EqualTo(0));
            Assert.That(x.OwnsAurorawing, Is.False);
        }

        // ================================================================== báo cáo (chỉ khi được yêu cầu)

        internal static double Pct(IEnumerable<double> xs, double q)
        {
            var v = xs.OrderBy(x => x).ToList();
            if (v.Count == 0) return double.NaN;
            double idx = q * (v.Count - 1);
            int lo = (int)Math.Floor(idx), hi = (int)Math.Ceiling(idx);
            return v[lo] + (v[hi] - v[lo]) * (idx - lo);
        }

        internal static string F(double v) => double.IsNaN(v) ? "—" : Math.Abs(v) >= 1_000_000 ? (v / 1_000_000).ToString("0.00", CultureInfo.InvariantCulture) + " M"
            : Math.Abs(v) >= 10_000 ? (v / 1000).ToString("0", CultureInfo.InvariantCulture) + " k" : v.ToString("0.#", CultureInfo.InvariantCulture);

        internal static string DayOf(IEnumerable<double?> hours, int n)
        {
            var hs = hours.Where(h => h.HasValue).Select(h => h.Value / 24).ToList();
            return hs.Count == 0 ? "— (0%)" : $"{F(Pct(hs, .5))} ({hs.Count * 100 / n}%)";
        }

        [Test]
        public void Write_u213_sky_report_when_requested()
        {
            string outDir = Environment.GetEnvironmentVariable("DRAVIO_U213_SKY_OUT");
            if (outDir == null) Assert.Ignore("Đặt DRAVIO_U213_SKY_OUT để sinh báo cáo sky_cloud U2.13.");
            int seeds = int.TryParse(Environment.GetEnvironmentVariable("DRAVIO_SIM_SEEDS"), out int n) ? n : 3;
            int days = int.TryParse(Environment.GetEnvironmentVariable("DRAVIO_SIM_DAYS"), out int d) ? d : 365;
            string pf = Environment.GetEnvironmentVariable("DRAVIO_SIM_POLICIES"), vf = Environment.GetEnvironmentVariable("DRAVIO_SKY_VARIANTS");
            var policies = new[] { Policy.Casual, Policy.Guided, Policy.Researcher }.Where(p => pf == null || pf.Split(',').Contains(p.ToString())).ToArray();
            var variants = new[] { SkyCloudBalance.Disabled }.Concat(SkyCloudBalance.Candidates).Where(v => vf == null || vf.Split(',').Contains(v.Id)).ToArray();
            double last = 24.0 * days;
            double[] horizons = { 24 * 7, 24 * 30, 24 * 90, last };
            System.IO.Directory.CreateDirectory(outDir);
            foreach (SkyCloudBalance v in variants)
            {
                var sb = new StringBuilder();
                foreach (Policy p in policies)
                    foreach (Schedule sc in EconomySimulationTests.Schedules)
                    {
                        var runs = new RunResult[seeds];
                        System.Threading.Tasks.Parallel.For(0, seeds, i => runs[i] = new EconomySimulator(p, sc, i + 1, EconomyVariant.LiveWithSky(v)).Run(horizons));
                        double M(double h, Func<Snapshot, double> f, double q = .5) => Pct(runs.Select(r => f(r.At[h])), q);
                        string Trio(Func<Snapshot, double> f) => $"{F(M(24 * 30, f))} / {F(M(24 * 90, f))} / {F(M(last, f))}";
                        // SKY|biến thể|kiểu|lịch|chuyến sân mây 30/90/365|bụi sao giữ 30/90/365|ngày trứng|ngày Codex 16|chuyến rừng 365|chuyến rạn 365|gỗ / hạt 365|pha lê / san hô 365|vàng 365|nhiệm vụ 365|loài 365|ngày mở Thiên Giới
                        sb.AppendLine($"SKY|{v.Id}|{p}|{sc.Name}|{Trio(x => x.SkyClaims)}|{Trio(x => x.Stardust)}|{DayOf(runs.Select(r => r.AurorawingGrantedHours), seeds)}" +
                            $"|{DayOf(runs.Select(r => r.Codex16Hours), seeds)}|{F(M(last, x => x.ForestClaims))}|{F(M(last, x => x.ReefClaims))}|{F(M(last, x => x.Wood))} / {F(M(last, x => x.Seed))}" +
                            $"|{F(M(last, x => x.Crystal))} / {F(M(last, x => x.Coral))}|{F(M(last, x => x.GoldG))}|{F(M(last, x => x.QuestsCompleted))}|{F(M(last, x => x.Species))}" +
                            $"|{DayOf(runs.Select(r => r.ZoneUnlockHours["sky"]), seeds)}|{runs.Max(r => r.AurorawingGrants)}|{F(M(last, x => x.Projects))}");
                    }
                System.IO.File.WriteAllText(System.IO.Path.Combine(outDir, v.Id + ".txt"), sb.ToString(), new UTF8Encoding(false));
            }
        }
    }
}
