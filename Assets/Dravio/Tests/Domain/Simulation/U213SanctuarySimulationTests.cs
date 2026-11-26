using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.World;
using NUnit.Framework;
using static Dravio.Domain.Tests.Simulation.U213SkyCloudSimulationTests;

namespace Dravio.Domain.Tests.Simulation
{
    /// <summary>
    /// U2.13 (ADR 0208) — mô phỏng vật liệu dự án Sanctuary theo thế giới: trước (U2.10 gỗ + hạt) / sau (theo thế giới), có / không bảng sky_cloud.
    /// Báo cáo khi đặt DRAVIO_U213_SANCTUARY_OUT=&lt;thư mục&gt;.
    /// </summary>
    public sealed class U213SanctuarySimulationTests
    {
        [Test]
        public void Per_world_materials_keep_gold_rules_and_never_block_a_guided_player_on_materials_for_long()
        {
            var after = new EconomySimulator(Policy.Guided, EconomySimulationTests.Schedules[1], 1, EconomyVariant.LiveWith(SkyCloudBalance.ProposedA, SanctuaryMaterialRules.PerWorld));
            RunResult r = after.Run(new double[] { 24 * 60 });
            Snapshot x = r.At[24 * 60];
            Assert.That(x.Projects, Is.GreaterThan(10));
            Assert.That(x.ProjectMaterialsById.Keys, Is.SupersetOf(new[] { "wood", "seed" }));
            Assert.That(after.State.Player.Inventory.Values.All(v => v >= 0), Is.True);
            long expected = 150 + x.EarnedG + x.SoldG + x.RewardsG - x.SpentPacksG - x.SpentBreedG - x.SpentUpgradesG - x.ProjectsG;
            Assert.That(Math.Abs(x.GoldG - expected), Is.LessThanOrEqualTo(8), "vàng bảo toàn, không nguồn mới");
        }

        /// <summary>Nhu cầu vật liệu cho cả 60 dự án theo trạng thái thế giới (tính từ luật thật, không mô phỏng).</summary>
        internal static string DemandTable()
        {
            var sb = new StringBuilder();
            sb.AppendLine("| Trạng thái | Luật | Gỗ | Hạt | Pha lê | San hô | Bụi sao | Dự án lùi vật liệu | Tổng đơn vị |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|");
            foreach (var (label, worlds, sky) in new[] { ("Chỉ Khởi Nguyên", 1, SkyCloudBalance.Disabled), ("+ Thủy Vực", 2, SkyCloudBalance.Disabled),
                ("+ Thiên Giới, bảng sky tắt", 3, SkyCloudBalance.Disabled), ("+ Thiên Giới, bảng A", 3, SkyCloudBalance.ProposedA) })
                foreach (SanctuaryMaterialRules rules in new[] { SanctuaryMaterialRules.LegacyWoodSeed, SanctuaryMaterialRules.PerWorld })
                    using (SkyCloudBalance.Use(sky))
                    using (SanctuaryMaterialRules.Use(rules))
                    {
                        GameState s = InitialStateFactory.Create("demand", EconomySimulator.Start, Rng.Create(1u));
                        if (worlds >= 2) WorldExpansion.ApplyZoneUnlock(s, "ocean", null);
                        if (worlds >= 3) WorldExpansion.ApplyZoneUnlock(s, "sky", null);
                        var d = new Dictionary<string, long>();
                        int fb = 0; long total = 0;
                        for (int n = 0; n < SanctuaryProjectCatalog.Max; n++)
                        {
                            ProjectMaterials m = SanctuaryProjectCatalog.Materials(s, n);
                            if (m.Each > 0 && m.Fallback && rules.Mode == SanctuaryMaterialMode.PerWorld) fb++;
                            foreach (string id in m.Ids) { d[id] = (d.TryGetValue(id, out long v) ? v : 0) + m.Each; total += m.Each; }
                        }
                        long G(string id) => d.TryGetValue(id, out long v) ? v : 0;
                        sb.AppendLine($"| {label} | {rules.Mode} | {G("wood")} | {G("seed")} | {G("crystal")} | {G("coral")} | {G("stardust")} | {fb} | {total} |");
                    }
            return sb.ToString();
        }

        [Test]
        public void Write_u213_sanctuary_report_when_requested()
        {
            string outDir = Environment.GetEnvironmentVariable("DRAVIO_U213_SANCTUARY_OUT");
            if (outDir == null) Assert.Ignore("Đặt DRAVIO_U213_SANCTUARY_OUT để sinh báo cáo vật liệu Sanctuary U2.13.");
            int seeds = int.TryParse(Environment.GetEnvironmentVariable("DRAVIO_SIM_SEEDS"), out int n) ? n : 3;
            int days = int.TryParse(Environment.GetEnvironmentVariable("DRAVIO_SIM_DAYS"), out int dd) ? dd : 365;
            string pf = Environment.GetEnvironmentVariable("DRAVIO_SIM_POLICIES");
            var policies = new[] { Policy.Casual, Policy.Guided, Policy.Researcher }.Where(p => pf == null || pf.Split(',').Contains(p.ToString())).ToArray();
            System.IO.Directory.CreateDirectory(outDir);
            System.IO.File.WriteAllText(System.IO.Path.Combine(outDir, "demand.md"), DemandTable(), new UTF8Encoding(false));
            double last = 24.0 * days;
            double[] horizons = { 24 * 7, 24 * 30, 24 * 90, last };
            var variants = new[]
            {
                ("before-legacy", SkyCloudBalance.Disabled, SanctuaryMaterialRules.LegacyWoodSeed),
                ("after-perworld-skyoff", SkyCloudBalance.Disabled, SanctuaryMaterialRules.PerWorld),
                ("after-perworld-skyA", SkyCloudBalance.ProposedA, SanctuaryMaterialRules.PerWorld),
            };
            string vf = Environment.GetEnvironmentVariable("DRAVIO_SANCTUARY_VARIANTS");
            foreach (var (name, sky, rules) in variants.Where(v => vf == null || vf.Split(',').Contains(v.Item1)))
            {
                var sb = new StringBuilder();
                foreach (Policy p in policies)
                    foreach (Schedule sc in EconomySimulationTests.Schedules)
                    {
                        var runs = new RunResult[seeds];
                        System.Threading.Tasks.Parallel.For(0, seeds, i => runs[i] = new EconomySimulator(p, sc, i + 1, EconomyVariant.LiveWith(sky, rules)).Run(horizons));
                        double M(double h, Func<Snapshot, double> f) => Pct(runs.Select(r => f(r.At[h])), .5);
                        double Used(Snapshot x, string id) => x.ProjectMaterialsById.TryGetValue(id, out long v) ? v : 0;
                        int blocked = runs.Count(r => r.At[last].NextBlockedByMaterials);
                        // PRJ|biến thể|kiểu|lịch|dự án 30/90/365|dự án lùi|dùng gỗ/hạt|dùng pha lê/san hô|dùng bụi sao|giữ gỗ/hạt|giữ pha lê/san hô|giữ bụi sao|% seed bị chặn vì vật liệu lúc 365|vàng 365|chuyến rừng/rạn/mây
                        sb.AppendLine($"PRJ|{name}|{p}|{sc.Name}|{F(M(24 * 30, x => x.Projects))} / {F(M(24 * 90, x => x.Projects))} / {F(M(last, x => x.Projects))}|{F(M(last, x => x.FallbackProjects))}" +
                            $"|{F(M(last, x => Used(x, "wood")))} / {F(M(last, x => Used(x, "seed")))}|{F(M(last, x => Used(x, "crystal")))} / {F(M(last, x => Used(x, "coral")))}|{F(M(last, x => Used(x, "stardust")))}" +
                            $"|{F(M(last, x => x.Wood))} / {F(M(last, x => x.Seed))}|{F(M(last, x => x.Crystal))} / {F(M(last, x => x.Coral))}|{F(M(last, x => x.Stardust))}" +
                            $"|{blocked * 100 / seeds}%|{F(M(last, x => x.GoldG))}|{F(M(last, x => x.ForestClaims))} / {F(M(last, x => x.ReefClaims))} / {F(M(last, x => x.SkyClaims))}");
                    }
                System.IO.File.WriteAllText(System.IO.Path.Combine(outDir, name + ".txt"), sb.ToString(), new UTF8Encoding(false));
            }
        }
    }
}
