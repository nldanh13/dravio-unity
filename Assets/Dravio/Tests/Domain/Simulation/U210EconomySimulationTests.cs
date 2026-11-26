using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Dravio.Domain.Behavior;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Economy;
using Dravio.Domain.Queries;
using Dravio.Domain.Tests.Support;
using NUnit.Framework;

namespace Dravio.Domain.Tests.Simulation
{
    /// <summary>
    /// U2.10 Workstream D — mô phỏng 7 / 30 / 90 ngày, 7 kiểu người chơi, 3 lịch chơi, biến thể trước/sau (ADR 0204 E1–E6).
    /// CI chạy bất biến ngắn. Báo cáo đầy đủ chỉ sinh khi đặt DRAVIO_U210_SIM_OUT=&lt;file.md&gt; (DRAVIO_SIM_SEEDS, mặc định 20;
    /// lọc bằng DRAVIO_SIM_POLICIES, DRAVIO_SIM_VARIANTS = no-projects,live, DRAVIO_SIM_SCHEDULES = tên lịch). Các seed chạy song song, kết quả không đổi.
    /// </summary>
    public sealed class U210EconomySimulationTests
    {
        private static readonly double[] Horizons = { 24, 24 * 7, 24 * 30, 24 * 90 };

        [Test]
        public void New_player_types_are_deterministic_and_valid()
        {
            foreach (Policy p in new[] { Policy.Relaxed, Policy.Breeder, Policy.Researcher })
            {
                var a = new EconomySimulator(p, EconomySimulationTests.Schedules[1], 5);
                var b = new EconomySimulator(p, EconomySimulationTests.Schedules[1], 5);
                RunResult ra = a.Run(new double[] { 24 * 4 }), rb = b.Run(new double[] { 24 * 4 });
                Assert.That(ra.At[96].GoldG, Is.EqualTo(rb.At[96].GoldG), p.ToString());
                Assert.That(MiniJson.Diff(StateJson.ToJson(a.State), StateJson.ToJson(b.State)), Is.Null);
                Assert.That(GameStateValidator.Validate(a.State).IsValid, Is.True, p + ": " + GameStateValidator.Validate(a.State));
            }
        }

        [Test]
        public void U210_read_only_queries_do_not_change_the_economy()
        {
            var plain = new EconomySimulator(Policy.Collector, EconomySimulationTests.Schedules[1], 9);
            RunResult a = plain.Run(new double[] { 72 });
            var observed = new EconomySimulator(Policy.Collector, EconomySimulationTests.Schedules[1], 9);
            var log = new ResearchLog();
            int calls = 0;
            observed.AfterCommand = (s, now) =>
            {
                DayPhase ph = DayCycle.PhaseOfMinute((int)((now / 60_000) % DayCycle.MinutesPerDay));
                foreach (BehaviorDecision d in BehaviorQueries.All(s, now, ph)) log.Observe(s, d, now, ResearchPacing.ProposedU210);
                ProgressionQueries.ObservationOpportunities(s, now, ph, log, ResearchPacing.ProposedU210);
                ProgressionQueries.SocialInteractionCandidates(s, now, ph);
                ProgressionQueries.Overview(s, log, ResearchPacing.ProposedU210);
                foreach (DragonState d in s.Dragons) SocialEcology.ComfortTargetModifier(s, d.Uid, SocialRules.ProposedForReview);
                calls++;
            };
            RunResult b = observed.Run(new double[] { 72 });
            Assert.That(calls, Is.GreaterThan(100));
            Assert.That(b.At[72].GoldG, Is.EqualTo(a.At[72].GoldG));
            Assert.That(b.At[72].Dragons, Is.EqualTo(a.At[72].Dragons));
            Assert.That(MiniJson.Diff(StateJson.ToJson(plain.State), StateJson.ToJson(observed.State)), Is.Null);
        }

        [Test]
        public void Starter_trader_is_held_to_five_starter_packs_a_day_by_the_real_rules()
        {
            Schedule sc = EconomySimulationTests.Schedules[2];
            var sim = new EconomySimulator(Policy.StarterTrader, sc, 4);
            RunResult r = sim.Run(new double[] { 24 * 7 });
            Assert.That(r.At[168].PacksBought, Is.InRange(1, PackCatalog.StarterDailyLimit * 8), "≤ 5 Starter / ngày UTC, 8 ngày UTC chạm tới");
            Assert.That(r.At[168].SpentPacksG, Is.EqualTo(r.At[168].PacksBought * 150L), "Starter 150 G");
            Assert.That(r.At[168].VirtualSinkG, Is.Zero, "E1/E2 là luật thật, không còn quỹ ảo");
            Assert.That(GameStateValidator.Validate(sim.State).IsValid, Is.True, GameStateValidator.Validate(sim.State).ToString());
        }

        [Test]
        public void Researcher_progress_is_paced_over_days_and_unlocks_worlds_only_through_canonical_commands()
        {
            var sim = new EconomySimulator(Policy.Researcher, EconomySimulationTests.Schedules[1], 3);
            RunResult r = sim.Run(new double[] { 24, 24 * 30 });
            Assert.That(r.At[24].Ethology, Is.Zero, "không đạt Ethology trong ngày đầu");
            Assert.That(r.At[720].LifeCycle, Is.GreaterThanOrEqualTo(3));
            Assert.That(r.At[720].Observations, Is.GreaterThan(100));
            if (r.ZoneUnlockHours["ocean"].HasValue)
            {
                Assert.That(r.ZoneUnlockHours["ocean"].Value, Is.GreaterThan(24), "không mở Thủy Vực trong ngày đầu");
                Assert.That(sim.State.CompletedQuests, Does.Contain("QUEST_AQUATIC_EXPLORATION"));
                Assert.That(sim.State.GrantedRewards, Does.Contain("ZONE_EGG_OCEAN"));
            }
            Assert.That(GameStateValidator.Validate(sim.State).IsValid, Is.True, GameStateValidator.Validate(sim.State).ToString());
        }

        [Test]
        public void Sanctuary_projects_are_a_real_long_term_sink_for_gold_and_expedition_materials()
        {
            Schedule morning = EconomySimulationTests.Schedules[0];
            double h90 = 24 * 90;
            var cur = new EconomySimulator(Policy.Researcher, morning, 3, EconomyVariant.NoProjects);
            var live = new EconomySimulator(Policy.Researcher, morning, 3, EconomyVariant.Live);
            RunResult a = cur.Run(new[] { h90 }), b = live.Run(new[] { h90 });
            Assert.That(a.At[h90].UpgradeLevels, Is.GreaterThan(0), "người chơi có mua nâng cấp");
            Assert.That(b.At[h90].Projects, Is.GreaterThan(SanctuaryProjectCatalog.FirstMaterialProject), "đã qua các dự án cần vật liệu");
            Assert.That(b.At[h90].Projects, Is.EqualTo(live.State.SanctuaryProjectsBuilt));
            Assert.That(b.At[h90].ProjectMaterials, Is.GreaterThan(0), "dự án tiêu gỗ và hạt giống");
            Assert.That(b.At[h90].GoldG, Is.LessThan(a.At[h90].GoldG), "dự án Sanctuary hút bớt vàng tồn");
            Assert.That(b.At[h90].Ethology, Is.GreaterThan(0), "nhật ký đêm: người chơi buổi sáng vẫn hoàn thành chu kỳ ngày đêm");
            Assert.That(b.At[h90].Species, Is.EqualTo(a.At[h90].Species).Within(2), "điểm tiêu không làm chậm sưu tầm");
            Assert.That(GameStateValidator.Validate(live.State).IsValid, Is.True, GameStateValidator.Validate(live.State).ToString());
            Assert.That(ResearchSchemaDraft.Validate(live.Research.ToSnapshot(), live.State), Is.Empty);
        }

        // ------------------------------------------------------------------ báo cáo

        private static double Pct(IEnumerable<double> xs, double q)
        {
            var s = xs.OrderBy(x => x).ToList();
            if (s.Count == 0) return double.NaN;
            double idx = q * (s.Count - 1);
            int lo = (int)Math.Floor(idx), hi = (int)Math.Ceiling(idx);
            return s[lo] + (s[hi] - s[lo]) * (idx - lo);
        }

        private static string F(double v) => double.IsNaN(v) ? "—" : Math.Abs(v) >= 1_000_000 ? (v / 1_000_000).ToString("0.00", CultureInfo.InvariantCulture) + " M"
            : Math.Abs(v) >= 100 ? v.ToString("N0", CultureInfo.InvariantCulture) : v.ToString("0.##", CultureInfo.InvariantCulture);

        private static double Balance(Snapshot x) => x.GoldG - x.VirtualSinkG;
        /// <summary>Nguồn vàng: sản xuất + bán + thưởng thành tựu.</summary>
        private static double Faucets(Snapshot x) => x.EarnedG + x.SoldG + x.RewardsG;
        /// <summary>Điểm tiêu: gói + lai + nâng cấp + dự án Sanctuary + quỹ ảo (chỉ E3 thử nghiệm).</summary>
        private static double Sinks(Snapshot x) => x.SpentPacksG + x.SpentBreedG + x.SpentUpgradesG + x.ProjectsG + x.VirtualSinkG;

        private static string Label(double h) => h == 24 ? "1 ngày" : (h / 24) + " ngày";

        [Test]
        public void Write_u210_economy_report_when_requested()
        {
            string outPath = Environment.GetEnvironmentVariable("DRAVIO_U210_SIM_OUT");
            if (outPath == null) Assert.Ignore("Đặt DRAVIO_U210_SIM_OUT để sinh báo cáo U2.10.");
            int seeds = int.TryParse(Environment.GetEnvironmentVariable("DRAVIO_SIM_SEEDS"), out int n) ? n : 20;
            string only = Environment.GetEnvironmentVariable("DRAVIO_SIM_POLICIES");
            string onlyVariants = Environment.GetEnvironmentVariable("DRAVIO_SIM_VARIANTS");
            string onlySchedules = Environment.GetEnvironmentVariable("DRAVIO_SIM_SCHEDULES");
            var variants = new[] { EconomyVariant.NoProjects, EconomyVariant.Live }.Where(v => onlyVariants == null || onlyVariants.Split(',').Contains(v.Name)).ToList();
            var sb = new StringBuilder();
            sb.AppendLine("<!-- Sinh bởi U210EconomySimulationTests.Write_u210_economy_report_when_requested; seeds = " + seeds + " -->");
            sb.AppendLine();
            sb.AppendLine("### Tóm tắt (trung vị, " + seeds + " seed)");
            sb.AppendLine();
            sb.AppendLine("| Người chơi | Lịch | Biến thể | Số dư 7 ng | Số dư 30 ng | Số dư 90 ng | Nguồn 90 ng | Tiêu 90 ng | Tiêu / nguồn | Ngày thu nhập đang giữ | Bán 90 ng | Nâng cấp (cấp) | Dự án | Gỗ + hạt cho dự án | Loài | Ô codex |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            var detail = new StringBuilder();
            var policies = ((Policy[])Enum.GetValues(typeof(Policy))).Where(p => p != Policy.Idle && (only == null || only.Split(',').Contains(p.ToString()))).ToList();
            foreach (Policy p in policies)
                foreach (Schedule sc in EconomySimulationTests.Schedules.Where(x => onlySchedules == null || onlySchedules.Split(',').Contains(x.Name)))
                    foreach (EconomyVariant v in variants)
                    {
                        // Mỗi seed độc lập và tất định: chạy song song, gom theo thứ tự seed (kết quả không phụ thuộc số luồng).
                        var runs = new RunResult[seeds];
                        System.Threading.Tasks.Parallel.For(0, seeds, i => runs[i] = new EconomySimulator(p, sc, i + 1, v).Run(Horizons));
                        Func<double, Func<Snapshot, double>, double, double> P = (h, f, q) => Pct(runs.Select(r => f(r.At[h])), q);
                        double last = Horizons.Last();
                        sb.AppendLine($"| {p} | {sc.Name} | {v.Name} | {F(P(168, Balance, .5))} | {F(P(720, Balance, .5))} | {F(P(last, Balance, .5))} " +
                            $"| {F(P(last, Faucets, .5))} | {F(P(last, Sinks, .5))} | {P(last, x => Sinks(x) / Math.Max(1, Faucets(x)), .5).ToString("0.00", CultureInfo.InvariantCulture)} " +
                            $"| {F(P(last, x => Balance(x) / Math.Max(1, Faucets(x) / (x.Hours / 24)), .5))} | {F(P(last, x => x.SoldG, .5))} | {F(P(last, x => x.UpgradeLevels, .5))} | {F(P(last, x => x.Projects, .5))} | {F(P(last, x => x.ProjectMaterials, .5))} " +
                            $"| {F(P(last, x => x.Species, .5))} | {F(P(last, x => x.Cells, .5))} |");
                        detail.AppendLine($"#### {p} — {sc.Name} — {v.Name}");
                        detail.AppendLine();
                        detail.AppendLine("| Mốc | Số dư G (P10 / trung vị / P90) | Vàng SX | Bán | Chi gói | Chi lai | Chi nâng cấp | Dự án (G) | Quỹ ảo | G/s | Rồng | Loài | Ô codex | Thành tựu |" + (p == Policy.Researcher ? " Profiled | LifeCycle | Ethology | Cá thể hiểu rõ | Quan sát | Vật liệu dùng | Thế giới |" : ""));
                        detail.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|" + (p == Policy.Researcher ? "---|---|---|---|---|---|---|" : ""));
                        foreach (double h in Horizons)
                        {
                            detail.Append($"| {Label(h)} | {F(P(h, x => x.GoldG - x.VirtualSinkG, .1))} / {F(P(h, x => x.GoldG - x.VirtualSinkG, .5))} / {F(P(h, x => x.GoldG - x.VirtualSinkG, .9))} " +
                                $"| {F(P(h, x => x.EarnedG, .5))} | {F(P(h, x => x.SoldG, .5))} | {F(P(h, x => x.SpentPacksG, .5))} | {F(P(h, x => x.SpentBreedG, .5))} | {F(P(h, x => x.SpentUpgradesG, .5))} | {F(P(h, x => x.ProjectsG, .5))} | {F(P(h, x => x.VirtualSinkG, .5))} " +
                                $"| {F(P(h, x => x.RateGPerSec, .5))} | {F(P(h, x => x.Dragons, .5))} | {F(P(h, x => x.Species, .5))} | {F(P(h, x => x.Cells, .5))} | {F(P(h, x => x.Achievements, .5))} |");
                            if (p == Policy.Researcher)
                                detail.Append($" {F(P(h, x => x.Profiled, .5))} | {F(P(h, x => x.LifeCycle, .5))} | {F(P(h, x => x.Ethology, .5))} | {F(P(h, x => x.IndividualsKnown, .5))} | {F(P(h, x => x.Observations, .5))} | {F(P(h, x => x.MaterialsSpent, .5))} | {F(P(h, x => x.ZonesUnlocked, .5))} |");
                            detail.AppendLine();
                        }
                        detail.AppendLine();
                        detail.Append("Lần đầu đủ tiền (giờ, trung vị): ");
                        detail.AppendLine(string.Join(" · ", PackCatalog.All.Select(pk =>
                        {
                            var hs = runs.Select(r => r.FirstAffordHours[pk.Id]).Where(x => x.HasValue).Select(x => x.Value).ToList();
                            return $"{pk.Id}: {F(Pct(hs, .5))} h ({hs.Count * 100 / runs.Length}%)";
                        })));
                        if (p == Policy.Researcher)
                        {
                            foreach (string z in new[] { "ocean", "sky" })
                            {
                                var hs = runs.Select(r => r.ZoneUnlockHours[z]).Where(x => x.HasValue).Select(x => x.Value / 24).ToList();
                                detail.AppendLine($"Mở {z} theo đề xuất nhiệm vụ: trung vị ngày {F(Pct(hs, .5))} (P10 {F(Pct(hs, .1))}, P90 {F(Pct(hs, .9))}); {hs.Count * 100 / runs.Length}% seed mở trong 90 ngày.");
                                detail.AppendLine();
                            }
                        }
                        detail.AppendLine();
                    }
            sb.AppendLine();
            sb.Append(detail);
            File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(false));
        }

        /// <summary>Phân tích giải tích trước/sau (không mô phỏng): EV theo catalog. Sinh vào DRAVIO_U210_EV_OUT.</summary>
        [Test]
        public void Write_u210_analytic_tables_when_requested()
        {
            string outPath = Environment.GetEnvironmentVariable("DRAVIO_U210_EV_OUT");
            if (outPath == null) Assert.Ignore("Đặt DRAVIO_U210_EV_OUT.");
            var sb = new StringBuilder();
            double EvMult(int luck)
            {
                double[] d = Mutation.Distribution(luck, 0);
                double sum = 0;
                for (int i = 0; i < 4; i++) sum += d[i] * EconomyTables.MutationSellMult(Mutation.Order[i]);
                return sum;
            }
            double ev0 = EvMult(0), ev8 = EvMult(8);
            sb.AppendLine($"E[hệ số bán theo biến thể]: luck 0 = {ev0:0.0000}, luck 8 = {ev8:0.0000}");
            sb.AppendLine();
            sb.AppendLine("| Gói | Giá trước U2.10 | EV bán Adult (luck 0) | EV/giá trước | EV/giá trước, luck 8 | Giá từ U2.10 | EV/giá từ U2.10 (luck 0 / 8) |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            foreach (PackDef pk in PackCatalog.All)
            {
                double ev = 0;
                foreach (Rarity r in PackRolls.RarityOrder)
                {
                    var sp = SpeciesCatalog.All.Where(x => x.Rarity == r && x.PackEligible).ToList();
                    if (sp.Count == 0) continue;
                    ev += PackCatalog.Odds(pk, r) / 100.0 * sp.Average(x => (double)x.BaseSellG);
                }
                long before = pk.Id == "starter" ? ProgressCatalog.TutorialPackPriceG : pk.PriceG;
                sb.AppendLine($"| {pk.Id} | {before} | {ev * ev0:0.0} | {ev * ev0 / before:0.00} | {ev * ev8 / before:0.00} | {pk.PriceG} | {ev * ev0 / pk.PriceG:0.00} / {ev * ev8 / pk.PriceG:0.00} |");
            }
            sb.AppendLine();
            double child = (75 + 80 + 380) / 3.0 * ev0;
            sb.AppendLine($"Lai Emberling × Sprout: EV con Adult = {child:0.0} G; EV/phí: 80 G → {child / 80:0.00}; đề xuất {EconomyVariant.E3BreedFee.CommonBreedFeeG} G → {child / EconomyVariant.E3BreedFee.CommonBreedFeeG:0.00}");
            sb.AppendLine();
            sb.AppendLine("| Nâng cấp | Tổng chi tới cấp tối đa (G) | Cấp 1 (G) | Tác dụng ở G1 |");
            sb.AppendLine("|---|---|---|---|");
            foreach (UpgradeDef u in EconomyTables.Upgrades)
            {
                long total = 0;
                for (int lv = 0; lv < u.Max; lv++) total += EconomyRates.UpgradeCostG(u, lv) ?? 0;
                sb.AppendLine($"| {u.Id} | {total:N0} | {EconomyRates.UpgradeCostG(u, 0):N0} | |");
            }
            File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(false));
        }
    }
}
