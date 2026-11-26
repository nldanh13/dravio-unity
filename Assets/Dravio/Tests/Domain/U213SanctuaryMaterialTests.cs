using System;
using System.Collections.Generic;
using System.Linq;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Economy;
using Dravio.Domain.Progression;
using Dravio.Domain.Queries;
using Dravio.Domain.World;
using NUnit.Framework;
using static Dravio.Domain.Tests.Support.Fixtures;

namespace Dravio.Domain.Tests
{
    /// <summary>
    /// U2.13 (ADR 0208) — vật liệu dự án Sanctuary theo thế giới (đã duyệt). Giá vàng, số dự án, công thức số lượng giữ nguyên;
    /// thế giới chưa mở / chưa có nguồn thì lùi về thế giới thấp hơn nên không bao giờ bế tắc.
    /// </summary>
    public sealed class U213SanctuaryMaterialTests
    {
        private static readonly long Day0 = new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

        /// <summary>Save có rồng Khởi Nguyên, mở tới <paramref name="worlds"/> thế giới (1 = origin, 2 = + ocean, 3 = + sky).</summary>
        private static GameState World(int worlds, long goldG = 0)
        {
            (GameState s, _) = NewGame();
            s = s.DeepClone();
            foreach (ZoneState z in s.Zones) if (z.Id == "origin") z.UnlockedHabitatIds = Zones.HabitatsOfZone("origin");
            foreach (string disc in ProgressCatalog.DiscoveryIds) if (!s.Discoveries.Contains(disc)) s.Discoveries.Add(disc);
            s.Discoveries.Sort(StringComparer.Ordinal);
            if (worlds >= 2) WorldExpansion.ApplyZoneUnlock(s, "ocean", null);
            if (worlds >= 3) WorldExpansion.ApplyZoneUnlock(s, "sky", null);
            s.Tutorial.Status = TutorialStatus.Completed;
            s.Tutorial.Completed = new List<string>(ProgressCatalog.TutorialStepIds);
            s.Clock = new ClockState(Day0, Day0);
            if (goldG > 0) s.Player.Gold = MicroGold.FromWholeGold(goldG);
            return s;
        }

        private static string Ids(ProjectMaterials m) => string.Join("+", m.Ids);

        [Test]
        public void Gold_prices_count_and_quantity_formula_are_unchanged()
        {
            Assert.That(SanctuaryProjectCatalog.Max, Is.EqualTo(60));
            Assert.That(Enumerable.Range(0, 11).Select(SanctuaryProjectCatalog.MaterialEach), Is.EqualTo(new[] { 0, 0, 1, 1, 1, 2, 2, 2, 2, 2, 3 }));
            Assert.That((SanctuaryProjectCatalog.CostG(0), SanctuaryProjectCatalog.CostG(1), SanctuaryProjectCatalog.CostG(59)), Is.EqualTo(((long?)20_000, (long?)25_000, (long?)10_440_487_148)));
            foreach (int worlds in new[] { 1, 2, 3 })
                foreach (SkyCloudBalance b in new[] { SkyCloudBalance.Disabled, SkyCloudBalance.ProposedA })
                    using (SkyCloudBalance.Use(b))
                    {
                        GameState s = World(worlds);
                        for (int n = 0; n < SanctuaryProjectCatalog.Max; n++)
                            Assert.That(SanctuaryProjectCatalog.Materials(s, n).Total, Is.EqualTo(2 * SanctuaryProjectCatalog.MaterialEach(n)), "tổng đơn vị mỗi dự án giữ nguyên, n=" + n);
                    }
        }

        [Test]
        public void Materials_follow_the_world_of_each_project_once_that_world_is_open_and_sourced()
        {
            GameState origin = World(1), ocean = World(2), sky = World(3);
            // #9 (index 8) là công trình Thủy Vực đầu tiên, #15 (index 14) Thiên Giới đầu tiên, index 20 là "Mở rộng cảnh quan 1" (origin), 21 ocean, 22 sky.
            Assert.That(Ids(SanctuaryProjectCatalog.Materials(origin, 7)), Is.EqualTo("wood+seed"));
            Assert.That(Ids(SanctuaryProjectCatalog.Materials(origin, 8)), Is.EqualTo("wood+seed"), "Thủy Vực chưa mở → lùi về Khởi Nguyên");
            Assert.That(SanctuaryProjectCatalog.Materials(origin, 8).Fallback, Is.True);
            Assert.That(Ids(SanctuaryProjectCatalog.Materials(ocean, 8)), Is.EqualTo("crystal+coral"));
            Assert.That(Ids(SanctuaryProjectCatalog.Materials(ocean, 14)), Is.EqualTo("crystal+coral"), "Thiên Giới chưa mở → lùi về Thủy Vực");
            using (SkyCloudBalance.Use(SkyCloudBalance.Disabled))
                Assert.That(Ids(SanctuaryProjectCatalog.Materials(sky, 14)), Is.EqualTo("crystal+coral"), "đối chứng U2.14: bảng tắt → chưa có nguồn bụi sao → lùi");
            // U2.15: production = bảng A (khối dưới giữ Use(ProposedA) để kiểm rõ ràng; cùng kết quả với production).
            Assert.That(Ids(SanctuaryProjectCatalog.Materials(sky, 14)), Is.EqualTo("stardust"));
            using (SkyCloudBalance.Use(SkyCloudBalance.ProposedA))
            {
                ProjectMaterials m = SanctuaryProjectCatalog.Materials(sky, 14);
                Assert.That((Ids(m), m.Each, m.Fallback), Is.EqualTo(("stardust", 2 * SanctuaryProjectCatalog.MaterialEach(14), false)));
                Assert.That(Ids(SanctuaryProjectCatalog.Materials(sky, 22)), Is.EqualTo("stardust"));
                Assert.That(Ids(SanctuaryProjectCatalog.Materials(sky, 21)), Is.EqualTo("crystal+coral"));
                Assert.That(Ids(SanctuaryProjectCatalog.Materials(sky, 20)), Is.EqualTo("wood+seed"));
                Assert.That(Ids(SanctuaryProjectCatalog.Materials(ocean, 22)), Is.EqualTo("crystal+coral"), "Thiên Giới chưa mở");
            }
            Assert.That(Ids(SanctuaryProjectCatalog.Materials(sky, 0)), Is.EqualTo("wood+seed"));
            Assert.That(SanctuaryProjectCatalog.Materials(sky, 0).Each, Is.EqualTo(0));
        }

        [Test]
        public void Every_one_of_the_60_projects_is_buildable_from_open_world_routes_in_every_world_state()
        {
            // Không bế tắc: với mỗi trạng thái thế giới, mọi dự án chỉ cần vật liệu có tuyến thám hiểm mở cho; xây đủ 60 bằng lệnh thật.
            foreach (int worlds in new[] { 1, 2, 3 })
                foreach (SkyCloudBalance b in new[] { SkyCloudBalance.Disabled, SkyCloudBalance.ProposedA })
                    using (SkyCloudBalance.Use(b))
                    {
                        GameState s = World(worlds);
                        var sourced = new HashSet<string>(ExpeditionCatalog.Routes.Where(r => r.RewardsDefined && s.FindZone(r.ZoneId)?.Unlocked == true).SelectMany(r => r.Materials));
                        // Chỉ nạp vật liệu có nguồn (người chơi đi thám hiểm được); vật liệu không có nguồn = 0.
                        foreach (string m in ExpeditionCatalog.MaterialIds) s.Player.Inventory[m] = sourced.Contains(m) ? 1_000 : 0;
                        long now = Day0;
                        for (int n = 0; n < SanctuaryProjectCatalog.Max; n++)
                        {
                            ProjectMaterials need = SanctuaryProjectCatalog.Materials(s, n);
                            Assert.That(need.Ids.All(sourced.Contains), Is.True, $"{worlds} thế giới, {b.Id}: dự án {n} cần {Ids(need)} không có nguồn");
                            // Vàng nạp đúng giá (không kiểm vàng ở đây; tổng 60 dự án ≈ 4,2 × 10¹⁰ G vượt số nguyên an toàn nếu giữ cùng lúc).
                            s.Player.Gold = MicroGold.FromWholeGold(SanctuaryProjectCatalog.CostG(n).Value);
                            DispatchResult r = CommandDispatcher.Dispatch(s, new BuildSanctuaryProjectCommand(Rid()), new FixedClock(now));
                            Assert.That(r.Ok, Is.True, $"{worlds} thế giới, {b.Id}: dự án {n}: {r.Error}");
                            SanctuaryProjectBuilt ev = r.Events.OfType<SanctuaryProjectBuilt>().Single();
                            Assert.That((ev.Index, ev.MaterialEach, string.Join("+", ev.MaterialIds)), Is.EqualTo((n, need.Each, Ids(need))));
                            s = r.State;
                        }
                        Assert.That(s.SanctuaryProjectsBuilt, Is.EqualTo(60));
                        Assert.That(GameStateValidator.Validate(s).IsValid, Is.True, GameStateValidator.Validate(s).ToString());
                        Assert.That(s.Player.Inventory.Values.All(v => v >= 0), Is.True);
                        Assert.That(CommandDispatcher.Dispatch(s, new BuildSanctuaryProjectCommand(Rid()), new FixedClock(now)).Error.Code, Is.EqualTo(DomainErrorCode.ProjectsMaxed));
                    }
        }

        [Test]
        public void Strict_per_world_rule_would_deadlock_at_project_9_before_the_ocean_opens()
        {
            // Lý do phải lùi: không lùi thì người chơi chưa mở Thủy Vực kẹt ở dự án #9 (index 8) vì không có tuyến pha lê / san hô.
            using (SanctuaryMaterialRules.Use(SanctuaryMaterialRules.PerWorldStrict))
            {
                GameState s = World(1, goldG: 1_000_000);
                foreach (string m in ExpeditionCatalog.MaterialIds) s.Player.Inventory[m] = m == "wood" || m == "seed" ? 1_000 : 0;
                for (int n = 0; n < 8; n++) s = Ok(s, new BuildSanctuaryProjectCommand(Rid()), new FixedClock(Day0)).State;
                Fail(s, new BuildSanctuaryProjectCommand(Rid()), new FixedClock(Day0), DomainErrorCode.NotEnoughMaterials);
            }
        }

        [Test]
        public void Built_projects_are_never_recalculated_and_opening_a_world_only_changes_future_costs()
        {
            GameState s = World(1, goldG: 10_000_000);
            foreach (string m in ExpeditionCatalog.MaterialIds) s.Player.Inventory[m] = 200;
            for (int n = 0; n < 10; n++) s = Ok(s, new BuildSanctuaryProjectCommand(Rid()), new FixedClock(Day0)).State;
            long wood = s.Player.Inventory["wood"], crystal = s.Player.Inventory["crystal"];
            Assert.That(crystal, Is.EqualTo(200), "dự án Thủy Vực xây trước khi mở Thủy Vực đã dùng gỗ + hạt");
            GameState opened = s.DeepClone();
            WorldExpansion.ApplyZoneUnlock(opened, "ocean", null);
            Assert.That((opened.SanctuaryProjectsBuilt, opened.Player.Inventory["wood"], opened.Player.Inventory["crystal"]), Is.EqualTo((10, wood, crystal)), "mở thế giới không hoàn hay trừ vật liệu");
            Assert.That(Ids(SanctuaryProjectCatalog.Materials(opened, 10)), Is.EqualTo("crystal+coral"), "dự án #11 (Vòm san hô) giờ dùng vật liệu Thủy Vực");
            Assert.That(Ids(SanctuaryProjectCatalog.Materials(opened, 14)), Is.EqualTo("crystal+coral"));
        }

        [Test]
        public void Ui_quote_lists_dynamic_materials_with_have_and_missing()
        {
            GameState s = World(2, goldG: 10_000_000);
            s.SanctuaryProjectsBuilt = 8;
            s.Player.Inventory["crystal"] = 1;
            SanctuaryEconomyDto q = ProgressionQueries.SanctuaryEconomy(s, Day0);
            Assert.That(q.NextProjectMaterialIds, Is.EqualTo(new[] { "crystal", "coral" }));
            Assert.That(q.NextProjectMaterialEach, Is.EqualTo(SanctuaryProjectCatalog.MaterialEach(8)));
            Assert.That(q.NextProjectMaterials.Select(x => (x.MaterialId, x.Required, x.Have, x.Missing)),
                Is.EqualTo(new[] { ("crystal", 2, 1L, 1L), ("coral", 2, 0L, 2L) }));
            Assert.That((q.NextProjectMaterialZoneId, q.NextProjectMaterialFallback, q.NextProjectBlockedBy), Is.EqualTo(("ocean", false, "not-enough-materials")));
            List<SanctuaryProjectCostView> plan = ProgressionQueries.SanctuaryProjectCosts(s);
            Assert.That(plan.Count, Is.EqualTo(60));
            Assert.That(plan.Take(8).All(p => p.Built), Is.True);
            Assert.That(plan.Where(p => p.ProjectZoneId == "sky").All(p => p.Fallback && p.MaterialZoneId == "ocean"), Is.True, "Thiên Giới chưa mở → pha lê + san hô");
            Assert.That(plan.Sum(p => p.CostGoldMicro), Is.EqualTo(Enumerable.Range(0, 60).Sum(i => MicroGold.FromWholeGold(SanctuaryProjectCatalog.CostG(i).Value).Micro)));
        }

        [Test]
        public void Legacy_rule_reproduces_the_u210_wood_and_seed_costs_for_comparison()
        {
            using (SanctuaryMaterialRules.Use(SanctuaryMaterialRules.LegacyWoodSeed))
            {
                GameState s = World(3);
                for (int n = 0; n < 60; n++) Assert.That(Ids(SanctuaryProjectCatalog.Materials(s, n)), Is.EqualTo("wood+seed"));
            }
        }
    }
}
