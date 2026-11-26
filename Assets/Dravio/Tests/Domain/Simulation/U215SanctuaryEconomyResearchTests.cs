using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using Dravio.Domain.Catalog;
using NUnit.Framework;

namespace Dravio.Domain.Tests.Simulation
{
    /// <summary>
    /// U2.15b (issue #75) — NGHIÊN CỨU đường cong giá 60 dự án Sanctuary. KHÔNG đổi giá production: các đường cong ứng viên chỉ sống trong file test này
    /// và chỉ áp dụng trong phạm vi <see cref="SanctuaryCostCurve.Use"/>. Mọi lần chạy đi qua <see cref="AlphaJourney"/> (lệnh chính thức + kiểm sổ sau mỗi lệnh).
    /// Báo cáo khi đặt DRAVIO_U215_OUT=&lt;thư mục&gt;.
    /// </summary>
    public sealed class U215SanctuaryEconomyResearchTests
    {
        // ================================================================== ứng viên (số nguyên chính xác, không Math.Pow)

        /// <summary>Nhân dồn từng bước bằng phân số (tử / mẫu) theo đoạn, floor ở mỗi dự án. Trả 60 giá (G).</summary>
        private static long[] Multiplicative(long baseG, Func<int, (int num, int den)> stepRatio)
        {
            var o = new long[SanctuaryProjectCatalog.Max];
            BigInteger num = baseG, den = 1;
            for (int n = 0; n < o.Length; n++)
            {
                if (n > 0) { var (a, b) = stepRatio(n); num *= a; den *= b; }
                o[n] = (long)(num / den);
            }
            return o;
        }

        /// <summary>Phân đoạn: ×1,25 cho dự án 1–10 (giữ nguyên trải nghiệm đầu), ×1,15 cho 11–30, ×1,06 cho 31–60.</summary>
        public static readonly SanctuaryCostCurve Staged = new SanctuaryCostCurve("staged-1.25-1.15-1.06",
            Multiplicative(20_000, n => n < 10 ? (5, 4) : n < 30 ? (115, 100) : (106, 100)));

        /// <summary>Lũy thừa mềm: ×1,13 mỗi dự án từ 20 000 G.</summary>
        public static readonly SanctuaryCostCurve SoftExp = new SanctuaryCostCurve("soft-1.13",
            Multiplicative(20_000, n => (113, 100)));

        /// <summary>Kết hợp giới hạn tăng: ×1,25 như hiện tại nhưng mỗi dự án tăng tối đa 250 000 G so với dự án trước (từ dự án ~19 thành tuyến tính).</summary>
        public static readonly SanctuaryCostCurve HybridCap = new SanctuaryCostCurve("hybrid-1.25-cap250k", HybridCosts(20_000, 250_000));

        private static long[] HybridCosts(long baseG, long capStepG)
        {
            var o = new long[SanctuaryProjectCatalog.Max];
            o[0] = Math.Min(baseG, SanctuaryCostCurve.Production.CostG(0).Value);
            // Giá production chừng nào bước tăng ≤ trần; sau đó tăng đều đúng trần mỗi dự án.
            for (int n = 1; n < o.Length; n++) o[n] = Math.Min(SanctuaryCostCurve.Production.CostG(n).Value, o[n - 1] + capStepG);
            return o;
        }

        public static readonly SanctuaryCostCurve[] Curves = { SanctuaryCostCurve.Production, Staged, SoftExp, HybridCap };

        private static long Total(SanctuaryCostCurve c, int count) { long t = 0; for (int i = 0; i < count; i++) t += c.CostG(i).Value; return t; }

        // ================================================================== kiểm (CI)

        [Test]
        public void Production_curve_is_unchanged_and_candidates_only_apply_inside_a_scope()
        {
            Assert.That(SanctuaryCostCurve.Current, Is.SameAs(SanctuaryCostCurve.Production));
            for (int n = 0; n < SanctuaryProjectCatalog.Max; n++)
                Assert.That(SanctuaryProjectCatalog.CostG(n), Is.EqualTo((long)(20_000 * BigInteger.Pow(5, n) / BigInteger.Pow(4, n))), "dự án " + n);
            Assert.That(Total(SanctuaryCostCurve.Production, 60), Is.EqualTo(U214EconomyAlphaTests.CumulativeG(60)));
            using (SanctuaryCostCurve.Use(Staged))
            {
                Assert.That(SanctuaryProjectCatalog.CostG(40), Is.EqualTo(Staged.CostG(40)));
                using (SanctuaryCostCurve.Use(HybridCap)) Assert.That(SanctuaryProjectCatalog.CostG(40), Is.EqualTo(HybridCap.CostG(40)));
                Assert.That(SanctuaryProjectCatalog.CostG(40), Is.EqualTo(Staged.CostG(40)), "lồng phạm vi");
            }
            Assert.That(SanctuaryProjectCatalog.CostG(40), Is.EqualTo(SanctuaryCostCurve.Production.CostG(40)), "phạm vi đã đóng");
            Assert.That(SanctuaryProjectCatalog.CostG(60), Is.Null);
        }

        [Test]
        public void Candidates_grow_strictly_fit_in_micro_gold_and_staged_and_hybrid_keep_the_first_ten_projects()
        {
            foreach (SanctuaryCostCurve c in Curves)
            {
                for (int n = 1; n < 60; n++) Assert.That(c.CostG(n), Is.GreaterThan(c.CostG(n - 1)), c.Id + " " + n);
                Assert.That(c.CostG(0), Is.EqualTo(20_000), c.Id);
                Assert.That(MicroGold.FromWholeGold(Total(c, 60)).Micro, Is.GreaterThan(0), c.Id);
            }
            for (int n = 0; n < 10; n++)
            {
                Assert.That(Staged.CostG(n), Is.EqualTo(SanctuaryCostCurve.Production.CostG(n)), "staged " + n);
                Assert.That(HybridCap.CostG(n), Is.EqualTo(SanctuaryCostCurve.Production.CostG(n)), "hybrid " + n);
            }
            foreach (SanctuaryCostCurve c in Curves.Skip(1))
                Assert.That(Total(c, 60), Is.InRange(150_000_000L, 400_000_000L), c.Id + ": tổng cùng cỡ để so hình dạng");
        }

        [Test]
        public void A_month_under_each_candidate_keeps_every_ledger_closed_through_canonical_commands([Values(1, 2, 3)] int curve)
        {
            AlphaJourney j;
            using (SanctuaryCostCurve.Use(Curves[curve])) j = AlphaJourney.Run(Policy.Guided, EconomySimulationTests.Schedules[1], 1, 30, EconomyVariant.Alpha(SkyCloudBalance.ProposedA));
            Assert.That(j.Violations, Is.Empty, string.Join("\n", j.Violations.Take(10)));
            Assert.That(j.Result.At[24 * 30].Projects, Is.GreaterThan(0));
            Assert.That(SanctuaryCostCurve.Current, Is.SameAs(SanctuaryCostCurve.Production));
        }

        // ================================================================== báo cáo

        public sealed class Row
        {
            public string Curve, Sky, Policy, Schedule; public int Seed; public double Seconds;
            public Dictionary<int, double?> ProjectDay = new Dictionary<int, double?>();
            public int Projects, Packs, Bred, UpgradeLevels, Expeditions, Species, Quests, Zones, Dragons;
            public long GoldHeldG, ProductionG, OfflineG, SalesG, AchievementG, ProjectMaterialUnits, MaterialsHeld;
            public Dictionary<string, long> SpentG = new Dictionary<string, long>();
            public bool BlockedByMaterials; public int Violations; public long Commands;
            public double? AurorawingDay;
            public Dictionary<int, (int projects, long gold, int packs, int bred)> At = new Dictionary<int, (int, long, int, int)>();
        }

        internal static readonly int[] Milestones = { 1, 5, 10, 20, 30, 40, 60 };
        internal static readonly double[] SnapshotDays = { 30, 90, 180, 365, 540, 730 };

        [Test]
        public void Write_u215_sanctuary_research_when_requested()
        {
            string outDir = Environment.GetEnvironmentVariable("DRAVIO_U215_OUT");
            if (outDir == null) Assert.Ignore("Đặt DRAVIO_U215_OUT để sinh dữ liệu nghiên cứu kinh tế Sanctuary U2.15b.");
            int seeds = int.TryParse(Environment.GetEnvironmentVariable("DRAVIO_SIM_SEEDS"), out int ns) ? ns : 3;
            double days = double.TryParse(Environment.GetEnvironmentVariable("DRAVIO_SIM_DAYS"), NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d : 365;
            string pf = Environment.GetEnvironmentVariable("DRAVIO_SIM_POLICIES");
            string cf = Environment.GetEnvironmentVariable("DRAVIO_U215_CURVES");
            string sf = Environment.GetEnvironmentVariable("DRAVIO_SIM_SCHEDULES");
            string skyMode = Environment.GetEnvironmentVariable("DRAVIO_U215_SKY") ?? "A"; // A | off
            int threads = int.TryParse(Environment.GetEnvironmentVariable("DRAVIO_SIM_THREADS"), out int nt) ? nt : Environment.ProcessorCount;
            string tag = Environment.GetEnvironmentVariable("DRAVIO_U215_TAG") ?? "run";
            System.IO.Directory.CreateDirectory(outDir);

            var policies = (pf ?? "Relaxed,Researcher,Collector,Breeder,Guided,Casual").Split(',').Select(p => (Policy)Enum.Parse(typeof(Policy), p)).ToArray();
            var curves = Curves.Where(c => cf == null || cf.Split(',').Contains(c.Id)).ToArray();
            var schedules = EconomySimulationTests.Schedules.Where(s => sf == null || sf.Split(',').Contains(s.Name)).ToArray();
            SkyCloudBalance sky = skyMode == "off" ? SkyCloudBalance.Disabled : SkyCloudBalance.ProposedA;
            double[] snaps = SnapshotDays.Where(x => x < days).Append(days).Distinct().ToArray();

            var jobs = (from c in curves from p in policies from sc in schedules from seed in Enumerable.Range(1, seeds) select (c, p, sc, seed)).ToList();
            var rows = new ConcurrentBag<Row>();
            string rawPath = System.IO.Path.Combine(outDir, "u215-" + tag + ".tsv");
            object io = new object();
            System.Threading.Tasks.Parallel.ForEach(jobs, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = threads }, job =>
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                AlphaJourney j;
                using (SanctuaryCostCurve.Use(job.c)) j = AlphaJourney.Run(job.p, job.sc, job.seed, snaps, EconomyVariant.Alpha(sky));
                var r = new Row { Curve = job.c.Id, Sky = skyMode, Policy = job.p.ToString(), Schedule = job.sc.Name, Seed = job.seed, Seconds = sw.Elapsed.TotalSeconds };
                foreach (int m in Milestones) r.ProjectDay[m] = j.FirstAt.TryGetValue("project:" + m, out double h) ? h / 24 : (double?)null;
                Snapshot last = j.Result.At[days * 24];
                r.Projects = last.Projects; r.Packs = last.PacksBought; r.Bred = last.Bred; r.UpgradeLevels = last.UpgradeLevels; r.Expeditions = last.ExpeditionsClaimed;
                r.Species = last.Species; r.Quests = last.QuestsCompleted; r.Zones = last.ZonesUnlocked; r.Dragons = last.Dragons;
                r.GoldHeldG = last.GoldG; r.MaterialsHeld = last.MaterialsHeld; r.BlockedByMaterials = last.NextBlockedByMaterials;
                r.ProductionG = j.ProductionMicro / 1_000_000; r.OfflineG = j.OfflineProductionMicro / 1_000_000; r.SalesG = j.SalesMicro / 1_000_000; r.AchievementG = j.AchievementMicro / 1_000_000;
                foreach (var kv in j.SpentByMicro) r.SpentG[kv.Key] = kv.Value / 1_000_000;
                r.ProjectMaterialUnits = j.ProjectMaterialUnits; r.Violations = j.Violations.Count; r.Commands = j.Commands;
                r.AurorawingDay = j.FirstAt.TryGetValue("reward-egg:" + SkyCloudBalance.AurorawingEggRewardId, out double ah) ? ah / 24 : (double?)null;
                foreach (double sd in snaps) { Snapshot s = j.Result.At[sd * 24]; r.At[(int)sd] = (s.Projects, s.GoldG, s.PacksBought, s.Bred); }
                rows.Add(r);
                lock (io) System.IO.File.AppendAllText(rawPath, Tsv(r) + "\n");
            });
            Assert.That(rows.Sum(r => r.Violations), Is.EqualTo(0), "mọi lần chạy giữ sổ đóng");
        }

        private static string N(double? v) => v.HasValue ? v.Value.ToString("0.###", CultureInfo.InvariantCulture) : "";

        internal static string Tsv(Row r)
        {
            string spent(string k) => r.SpentG.TryGetValue(k, out long v) ? v.ToString(CultureInfo.InvariantCulture) : "0";
            var cols = new List<string>
            {
                r.Curve, r.Sky, r.Policy, r.Schedule, r.Seed.ToString(CultureInfo.InvariantCulture),
            };
            cols.AddRange(Milestones.Select(m => N(r.ProjectDay[m])));
            cols.AddRange(new[]
            {
                r.Projects, r.Packs, r.Bred, r.UpgradeLevels, r.Expeditions, r.Species, r.Quests, r.Zones, r.Dragons,
            }.Select(x => x.ToString(CultureInfo.InvariantCulture)));
            cols.AddRange(new[] { r.GoldHeldG, r.ProductionG, r.OfflineG, r.SalesG, r.AchievementG }.Select(x => x.ToString(CultureInfo.InvariantCulture)));
            cols.AddRange(new[] { "pack", "breed", "upgrade", "decoration", "project" }.Select(spent));
            cols.Add(r.ProjectMaterialUnits.ToString(CultureInfo.InvariantCulture));
            cols.Add(r.MaterialsHeld.ToString(CultureInfo.InvariantCulture));
            cols.Add(r.BlockedByMaterials ? "1" : "0");
            cols.Add(r.Violations.ToString(CultureInfo.InvariantCulture));
            cols.Add(r.Commands.ToString(CultureInfo.InvariantCulture));
            cols.Add(N(r.AurorawingDay));
            cols.Add(string.Join(";", r.At.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}:{kv.Value.projects},{kv.Value.gold},{kv.Value.packs},{kv.Value.bred}")));
            cols.Add(r.Seconds.ToString("0", CultureInfo.InvariantCulture));
            return string.Join("\t", cols);
        }

        /// <summary>Thứ tự cột của file TSV (để script phân tích đọc).</summary>
        public const string TsvHeader = "curve\tsky\tpolicy\tschedule\tseed\tp1\tp5\tp10\tp20\tp30\tp40\tp60\tprojects\tpacks\tbred\tupgrades\texpeditions\tspecies\tquests\tzones\tdragons" +
            "\tgoldHeld\tproduction\toffline\tsales\tachievements\tspentPack\tspentBreed\tspentUpgrade\tspentDecoration\tspentProject\tprojectMaterialUnits\tmaterialsHeld\tblockedByMaterials\tviolations\tcommands\taurorawingDay\tsnapshots\tseconds";
    }
}
