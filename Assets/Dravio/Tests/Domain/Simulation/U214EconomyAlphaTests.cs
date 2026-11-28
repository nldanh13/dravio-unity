using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.World;
using NUnit.Framework;

namespace Dravio.Domain.Tests.Simulation
{
    /// <summary>
    /// U2.14 — kinh tế và tiến trình Alpha: 1 / 7 / 30 / 90 / 365 ngày, bốn kiểu người chơi của đề bài (thư giãn, nghiên cứu, sưu tầm, lai giống) và
    /// hai kiểu có hướng dẫn (Guided, Casual), 60 dự án Sanctuary, vật liệu theo thế giới, không nguồn vàng ngoài luật. Mọi lần chạy đi qua
    /// <see cref="AlphaJourney"/> (lệnh thật + kiểm sổ sau mỗi lệnh). Báo cáo khi đặt DRAVIO_U214_OUT=&lt;thư mục&gt;.
    /// </summary>
    public sealed class U214EconomyAlphaTests
    {
        private static readonly string[] Worlds = { "origin", "ocean", "sky" };

        /// <summary>Vật liệu mà các tuyến có bảng thưởng (luật hiện hành) cho ra khi các thế giới <paramref name="open"/> đã mở.</summary>
        private static HashSet<string> Sources(IEnumerable<string> open) =>
            new HashSet<string>(ExpeditionCatalog.Routes.Where(r => r.RewardsDefined && open.Contains(r.ZoneId)).SelectMany(r => r.Materials), StringComparer.Ordinal);

        [Test]
        public void Every_one_of_the_60_projects_asks_only_for_materials_an_open_approved_route_produces()
        {
            // Mỗi trạng thái thế giới đạt được trong game (Thủy Vực trước Thiên Giới), bảng sky tắt (production) và bảng A (mô phỏng).
            foreach (SkyCloudBalance sky in new[] { SkyCloudBalance.Production, SkyCloudBalance.ProposedA })
                using (SkyCloudBalance.Use(sky))
                    for (int w = 1; w <= Worlds.Length; w++)
                    {
                        GameState s = EconomySimulator.NewSave(1);
                        if (w >= 2) WorldExpansion.ApplyZoneUnlock(s, "ocean", null);
                        if (w >= 3) WorldExpansion.ApplyZoneUnlock(s, "sky", null);
                        HashSet<string> have = Sources(Worlds.Take(w));
                        for (int n = 0; n < SanctuaryProjectCatalog.Max; n++)
                        {
                            ProjectMaterials m = SanctuaryProjectCatalog.Materials(s, n);
                            foreach (string id in m.Ids)
                                Assert.That(have.Contains(id), Is.True, $"{sky.Id}, {w} thế giới, dự án {n}: cần {id} mà chưa có tuyến nào cho");
                            if (!sky.Enabled) Assert.That(m.Ids, Does.Not.Contain(SkyCloudBalance.MaterialId), "bảng tắt: không bao giờ đòi bụi sao");
                        }
                    }
        }

        [Test]
        public void Sixty_projects_cost_table_is_unchanged_and_grows_25_percent_per_project()
        {
            long total = 0;
            for (int n = 0; n < SanctuaryProjectCatalog.Max; n++)
            {
                long c = SanctuaryProjectCatalog.CostG(n).Value;
                if (n > 0) Assert.That((double)c / SanctuaryProjectCatalog.CostG(n - 1).Value, Is.EqualTo(1.25).Within(0.001), "dự án " + n);
                total += c;
            }
            Assert.That(SanctuaryProjectCatalog.CostG(0), Is.EqualTo(20_000));
            Assert.That(SanctuaryProjectCatalog.CostG(SanctuaryProjectCatalog.Max), Is.Null);
            // Tổng ~ 5 × giá dự án cuối (cấp số nhân 1,25): con số dùng trong ECONOMY_ALPHA.md.
            Assert.That(total, Is.EqualTo(CumulativeG(60)));
            Assert.That(total, Is.GreaterThan(40_000_000_000L));
        }

        public static long CumulativeG(int n)
        {
            long t = 0;
            for (int i = 0; i < n; i++) t += SanctuaryProjectCatalog.CostG(i).Value;
            return t;
        }

        [Test]
        public void One_week_of_each_player_archetype_keeps_ledgers_closed_and_never_blocks_on_materials([Values(Policy.Relaxed, Policy.Researcher, Policy.Collector, Policy.Breeder)] Policy p)
        {
            AlphaJourney j = AlphaJourney.Run(p, EconomySimulationTests.Schedules[1], 2, new double[] { 1, 7 });
            Assert.That(j.Violations, Is.Empty, string.Join("\n", j.Violations.Take(10)));
            Snapshot d7 = j.Result.At[24 * 7];
            Assert.That(d7.GoldG, Is.GreaterThanOrEqualTo(0));
            Assert.That(d7.PacksBought, Is.GreaterThan(0), p + " mua gói trong tuần đầu");
            Assert.That(j.Result.At[24].Dragons, Is.GreaterThan(1), p + " có rồng mới ngày đầu");
        }

        // ================================================================== báo cáo

        internal static readonly Policy[] ReportPolicies = { Policy.Relaxed, Policy.Researcher, Policy.Collector, Policy.Breeder, Policy.Guided, Policy.Casual };
        internal static readonly double[] ReportDays = { 1, 7, 30, 90, 365 };

        private static string F(double v) =>
            Math.Abs(v) >= 1e6 ? (v / 1e6).ToString("0.00", CultureInfo.InvariantCulture) + " M" :
            Math.Abs(v) >= 1e4 ? (v / 1e3).ToString("0", CultureInfo.InvariantCulture) + " k" : v.ToString(v % 1 == 0 ? "0" : "0.0", CultureInfo.InvariantCulture);

        private static double Median(IEnumerable<double> xs)
        {
            double[] a = xs.OrderBy(x => x).ToArray();
            return a.Length == 0 ? double.NaN : a.Length % 2 == 1 ? a[a.Length / 2] : (a[a.Length / 2 - 1] + a[a.Length / 2]) / 2;
        }

        [Test]
        public void Write_u214_economy_report_when_requested()
        {
            string outDir = Environment.GetEnvironmentVariable("DRAVIO_U214_OUT");
            if (outDir == null) Assert.Ignore("Đặt DRAVIO_U214_OUT để sinh báo cáo kinh tế U2.14.");
            int seeds = int.TryParse(Environment.GetEnvironmentVariable("DRAVIO_SIM_SEEDS"), out int n) ? n : 3;
            string pf = Environment.GetEnvironmentVariable("DRAVIO_SIM_POLICIES");
            string vf = Environment.GetEnvironmentVariable("DRAVIO_U214_VARIANTS") ?? "production";
            double[] days = ReportDays;
            if (double.TryParse(Environment.GetEnvironmentVariable("DRAVIO_SIM_DAYS"), NumberStyles.Float, CultureInfo.InvariantCulture, out double maxDays)) days = days.Where(d => d < maxDays).Append(maxDays).ToArray();
            System.IO.Directory.CreateDirectory(outDir);
            var policies = ReportPolicies.Where(p => pf == null || pf.Split(',').Contains(p.ToString())).ToArray();
            foreach (string variant in vf.Split(','))
            {
                var sb = new StringBuilder();
                SkyCloudBalance sky = variant == "skyA" ? SkyCloudBalance.ProposedA : null;
                foreach (Policy p in policies)
                    foreach (Schedule sc in EconomySimulationTests.Schedules)
                    {
                        var runs = new AlphaJourney[seeds];
                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        System.Threading.Tasks.Parallel.For(0, seeds, i => runs[i] = AlphaJourney.Run(p, sc, i + 1, days, EconomyVariant.Alpha(sky)));
                        double M(double d, Func<Snapshot, double> f) => Median(runs.Select(r => f(r.Result.At[d * 24])));
                        string Ms(Func<Snapshot, double> f) => string.Join(" / ", days.Select(d => F(M(d, f))));
                        string Day(string key) { var xs = runs.Where(r => r.FirstAt.ContainsKey(key)).Select(r => r.FirstAt[key] / 24).ToList(); return xs.Count == 0 ? "—" : F(Median(xs)) + $" ({xs.Count * 100 / seeds}%)"; }
                        double last = days.Last();
                        int viol = runs.Sum(r => r.Violations.Count);
                        // ECO|biến thể|kiểu|lịch|vàng giữ (1/7/30/90/365)|vàng sản xuất tích lũy|G/s cuối|gói|loài|dự án|nhiệm vụ|thế giới|rồng|kho|
                        //   gỗ/hạt cuối|pha lê/san hô cuối|bụi sao cuối|chặn vì vật liệu cuối|ngày Thủy Vực|ngày Thiên Giới|vi phạm|lệnh|giây
                        sb.AppendLine($"ECO|{variant}|{p}|{sc.Name}|{Ms(x => x.GoldG)}|{Ms(x => x.EarnedG)}|{F(M(last, x => x.RateGPerSec))}|{Ms(x => x.PacksBought)}|{Ms(x => x.Species)}|{Ms(x => x.Projects)}|{Ms(x => x.QuestsCompleted)}" +
                            $"|{Ms(x => x.ZonesUnlocked)}|{F(M(last, x => x.Dragons))}|{F(M(last, x => x.StorageDragons))}|{F(M(last, x => x.Wood))} / {F(M(last, x => x.Seed))}|{F(M(last, x => x.Crystal))} / {F(M(last, x => x.Coral))}|{F(M(last, x => x.Stardust))}" +
                            $"|{runs.Count(r => r.Result.At[last * 24].NextBlockedByMaterials) * 100 / seeds}%|{Day("zone:ocean")}|{Day("zone:sky")}|{viol}|{runs.Sum(r => r.Commands)}|{sw.Elapsed.TotalSeconds:0}");
                        // SPC|biến thể|kiểu|lịch|loài:ngày trung vị (% seed)…
                        sb.AppendLine($"SPC|{variant}|{p}|{sc.Name}|" + string.Join(" ", SpeciesCatalog.All.Select(s => s.Id + ":" + (s.Id == "emberling" ? "0 (100%)" : Day("species:" + s.Id)))));
                        // PAY|biến thể|kiểu|lịch|tổng vàng sản xuất|bán|thành tựu|chi|trứng thưởng|nhiệm vụ trung vị|mốc trung vị|lần trả trùng
                        sb.AppendLine($"PAY|{variant}|{p}|{sc.Name}|{F(Median(runs.Select(r => r.ProductionMicro / 1e6)))}|{F(Median(runs.Select(r => r.SalesMicro / 1e6)))}|{F(Median(runs.Select(r => r.AchievementMicro / 1e6)))}|{F(Median(runs.Select(r => r.SpentMicro / 1e6)))}" +
                            $"|{F(Median(runs.Select(r => (double)r.PaidOnce.Keys.Count(k => k.StartsWith("reward-egg:", StringComparison.Ordinal)))))}|{F(Median(runs.Select(r => (double)r.PaidOnce.Keys.Count(k => k.StartsWith("quest:", StringComparison.Ordinal)))))}" +
                            $"|{F(Median(runs.Select(r => (double)r.PaidOnce.Keys.Count(k => k.StartsWith("milestone:", StringComparison.Ordinal)))))}|{runs.Sum(r => r.PaidOnce.Values.Count(v => v > 1))}" +
                            $"|{Day("reward-egg:" + SkyCloudBalance.AurorawingEggRewardId)}|{Day("project:30")}|{Day("project:35")}");
                        System.IO.File.AppendAllText(System.IO.Path.Combine(outDir, "progress.log"), $"{variant} {p} {sc.Name} {sw.Elapsed.TotalSeconds:0}s viol={viol}\n");
                    }
                System.IO.File.WriteAllText(System.IO.Path.Combine(outDir, "economy-" + variant + ".txt"), sb.ToString(), new UTF8Encoding(false));
            }
        }
    }
}
