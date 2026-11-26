using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Dravio.Domain.Biology;
using Dravio.Domain.Breeding;
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
    /// <summary>Một dòng ma trận thu thập (tính từ catalog và luật thật, không mô phỏng).</summary>
    public sealed class CollectionRow
    {
        public string SpeciesId;
        public Rarity Rarity;
        public BreedingGroup Group;
        /// <summary>Gói → xác suất mỗi lần mở (0..1).</summary>
        public readonly SortedDictionary<string, double> PackOdds = new SortedDictionary<string, double>(StringComparer.Ordinal);
        public bool StartingDragon, TutorialPack;
        /// <summary>Trứng thế giới (rewardId) khi mở thế giới.</summary>
        public string ZoneEgg, ZoneEggZone;
        /// <summary>Trứng mốc thám hiểm (rewardId, tuyến, số chuyến).</summary>
        public string ExpeditionEgg, ExpeditionEggRoute;
        public int ExpeditionEggClaims;
        /// <summary>Cặp lai cho tỉ lệ cao nhất (không cần chính loài này làm bố/mẹ), null nếu không có.</summary>
        public string BestPair;
        public double BestPairChance;
        public long BestPairFeeG;
        /// <summary>Có cặp lai mà một bố/mẹ là chính loài này (tự nhân giống).</summary>
        public bool SelfBreedable;
        public List<string> Habitats = new List<string>();
        /// <summary>Thế giới sớm nhất có microhabitat đặt được (origin → ocean → sky).</summary>
        public string EarliestWorld;
        public bool ObtainableProduction, ObtainableWithTableA;
    }

    /// <summary>
    /// U2.14 — ma trận thu thập 16 loài: nguồn, điều kiện mở khóa, điều kiện sinh cảnh, khả năng tiếp cận từ save mới, bế tắc.
    /// Aurorawing tách bạch: luật trứng (đã duyệt, mốc 15 chuyến sky_cloud) và tuyến sky_cloud (bảng thưởng CHƯA duyệt, tắt).
    /// Báo cáo khi đặt DRAVIO_U214_OUT=&lt;thư mục&gt; (collection-matrix.md).
    /// </summary>
    public sealed class U214CollectionMatrixTests
    {
        private static readonly string[] WorldOrder = { "origin", "ocean", "sky" };
        private static readonly long Day0 = new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

        private static bool Placeable(string speciesId, string habitatId, ICollection<string> open)
        {
            string zone = HabitatCatalog.ZoneOf(habitatId);
            if (zone == null || !open.Contains(zone) || !Suitability.ForSpecies(speciesId, habitatId).Allowed) return false;
            List<Locomotion> need = Travel.EntryLocomotion(zone);
            return need.Count == 0 || SpeciesCatalog.GetBiology(speciesId).Locomotion.Any(need.Contains);
        }

        public static List<CollectionRow> Build()
        {
            ProgressionAuditReport prod = ProgressionQueries.AuditCatalog();
            ProgressionAuditReport withA;
            using (SkyCloudBalance.Use(SkyCloudBalance.ProposedA)) withA = ProgressionQueries.AuditCatalog();
            var rows = new List<CollectionRow>();
            foreach (DragonSpecies sp in SpeciesCatalog.All)
            {
                var row = new CollectionRow { SpeciesId = sp.Id, Rarity = sp.Rarity, Group = sp.Group };
                row.StartingDragon = sp.Id == "emberling";
                row.TutorialPack = sp.Id == ProgressCatalog.TutorialPackSpeciesId;
                if (sp.PackEligible)
                    foreach (PackDef p in PackCatalog.All)
                    {
                        int same = SpeciesCatalog.All.Count(x => x.PackEligible && x.Rarity == sp.Rarity);
                        double odds = PackCatalog.Odds(p, sp.Rarity) / 100.0 / same;
                        if (odds > 0) row.PackOdds[p.Id] = odds;
                    }
                ZoneUnlockRule zr = WorldUnlockCatalog.Rules.FirstOrDefault(r => r.EggSpeciesId == sp.Id);
                if (zr != null) { row.ZoneEgg = zr.RewardId; row.ZoneEggZone = zr.ZoneId; }
                ExplorationMilestoneDef em = Exploration.Milestones.FirstOrDefault(m => m.EggSpeciesId == sp.Id);
                if (em != null) { row.ExpeditionEgg = em.EggRewardId; row.ExpeditionEggRoute = em.RouteId; row.ExpeditionEggClaims = em.Claims; }
                foreach (DragonSpecies a in SpeciesCatalog.All)
                    foreach (DragonSpecies b in SpeciesCatalog.All)
                    {
                        if (string.CompareOrdinal(a.Id, b.Id) > 0) continue;
                        double p = BreedingRules.SpeciesDistribution(a, b).Where(kv => kv.Key == sp.Id).Sum(kv => kv.Value);
                        if (p <= 0) continue;
                        if (a.Id == sp.Id || b.Id == sp.Id) { row.SelfBreedable = true; continue; }
                        if (p > row.BestPairChance + 1e-12) { row.BestPairChance = p; row.BestPair = a.Id + " × " + b.Id; row.BestPairFeeG = BreedingRules.BreedFeeG(a, b); }
                    }
                row.Habitats = sp.Biology.HabitatRules.ToList();
                var open = new List<string>();
                foreach (string w in WorldOrder)
                {
                    open.Add(w);
                    if (row.EarliestWorld == null && row.Habitats.Any(h => Placeable(sp.Id, h, open))) row.EarliestWorld = w;
                }
                row.ObtainableProduction = prod.ObtainableSpecies.Contains(sp.Id);
                row.ObtainableWithTableA = withA.ObtainableSpecies.Contains(sp.Id);
                rows.Add(row);
            }
            return rows;
        }

        // ================================================================== kết luận ma trận

        [Test]
        public void Fifteen_of_sixteen_species_are_collectible_in_production_and_only_aurorawing_waits_for_the_sky_table()
        {
            List<CollectionRow> rows = Build();
            Assert.That(rows.Count, Is.EqualTo(16));
            Assert.That(rows.Where(r => !r.ObtainableProduction).Select(r => r.SpeciesId), Is.EqualTo(new[] { "aurorawing" }));
            Assert.That(rows.All(r => r.ObtainableWithTableA), Is.True, "bảng A (chỉ trong phạm vi Use) → 16/16");
            Assert.That(SkyCloudBalance.Current.Enabled, Is.False);
            // Mọi loài đặt được ở ít nhất một sinh cảnh của ba thế giới.
            Assert.That(rows.Where(r => r.EarliestWorld == null).Select(r => r.SpeciesId), Is.Empty);
        }

        [Test]
        public void Every_species_except_amberhorn_and_aurorawing_comes_from_packs_and_both_legendaries_need_mystic_or_astral()
        {
            List<CollectionRow> rows = Build();
            Assert.That(rows.Where(r => r.PackOdds.Count == 0).Select(r => r.SpeciesId), Is.EquivalentTo(new[] { "amberhorn", "aurorawing" }));
            foreach (CollectionRow r in rows.Where(r => r.Rarity == Rarity.Legendary))
                Assert.That(r.PackOdds.Keys, Is.EquivalentTo(new[] { "astral", "mystic" }), r.SpeciesId);
            foreach (CollectionRow r in rows.Where(r => r.Rarity == Rarity.Epic && r.PackOdds.Count > 0))
                Assert.That(r.PackOdds.ContainsKey("starter"), Is.False, r.SpeciesId + ": Epic không có trong Starter");
            Assert.That(rows.Single(r => r.SpeciesId == "droplet").ZoneEgg, Is.EqualTo("ZONE_EGG_OCEAN"));
            Assert.That(rows.Single(r => r.SpeciesId == "zephyr").ZoneEgg, Is.EqualTo("ZONE_EGG_SKY"));
        }

        [Test]
        public void Amberhorn_is_breed_only_from_a_rare_or_epic_fire_nature_parent_of_group_T()
        {
            CollectionRow a = Build().Single(r => r.SpeciesId == "amberhorn");
            Assert.That(a.PackOdds, Is.Empty);
            Assert.That(a.BestPair, Does.Contain("magma"), "cặp nhóm T có bố/mẹ Rare mang Fire + Nature (Magma)");
            Assert.That(a.BestPairChance, Is.EqualTo(0.09 / 0.99).Within(1e-9), "60/30/9 chia đều trong nhóm: Epic 9 / 99");
            Assert.That(a.BestPairFeeG, Is.EqualTo(350));
            Assert.That(a.EarliestWorld, Is.EqualTo("origin"));
        }

        [Test]
        public void Aurorawing_first_copy_comes_only_from_the_approved_15th_sky_trip_egg_while_the_route_table_is_off()
        {
            CollectionRow r = Build().Single(x => x.SpeciesId == "aurorawing");
            Assert.That(r.PackOdds, Is.Empty, "không có trong gói");
            Assert.That(r.ZoneEgg, Is.Null);
            Assert.That(r.BestPair, Is.Null, "không cặp nào thiếu Aurorawing lai ra Aurorawing (nhóm V không loài nào khác mang Water)");
            Assert.That(r.SelfBreedable, Is.True, "Aurorawing × rồng nhóm V (vd Zephyr) lai ra Aurorawing");
            // Luật trứng đã duyệt: mốc tồn tại ngay cả khi bảng tắt.
            Assert.That((r.ExpeditionEgg, r.ExpeditionEggRoute, r.ExpeditionEggClaims), Is.EqualTo((SkyCloudBalance.AurorawingEggRewardId, SkyCloudBalance.RouteId, 15)));
            // Tuyến chưa kích hoạt: không có bảng thưởng → lệnh bị từ chối → mốc không bao giờ đạt.
            ExpeditionCatalog.TryGet(SkyCloudBalance.RouteId, out ExpeditionRoute route);
            Assert.That(route.RewardsDefined, Is.False);
            Assert.That(r.ObtainableProduction, Is.False);
            Assert.That(r.ObtainableWithTableA, Is.True);
            var zephyr = SpeciesCatalog.Get("zephyr");
            Assert.That(BreedingRules.CandidateSpecies(SpeciesCatalog.Get("aurorawing"), zephyr).Select(s => s.Id), Does.Contain("aurorawing"));
        }

        // ================================================================== bế tắc

        [Test]
        public void Deadlock_selling_the_only_aurorawing_loses_the_species_for_good_but_keeps_the_codex()
        {
            using (SkyCloudBalance.Use(SkyCloudBalance.ProposedA))
            {
                GameState s = U213SkyCloudTests.SkyWorld(Day0, Adult("keeper", "sprout"));
                (GameState end, long now, _) = U213SkyCloudTests.FlyTrips(s, Day0, 15);
                DispatchResult open = Ok(end, new ClaimPackCommand(Rid(), MoveTarget.Storage), new FixedClock(now));
                DragonState aw = open.State.Dragons.Single(d => d.SpeciesId == "aurorawing");
                Assert.That(open.State.UnlockedAchievements, Does.Not.Contain("ACH_CODEX_16"), "fixture chưa đủ 16 loài");
                Assert.That(open.State.Codex.Any(c => c.SpeciesId == "aurorawing"), Is.True, "Bách khoa đã ghi");
                DispatchResult sold = Ok(open.State, new SellDragonCommand(Rid(), aw.Uid), new FixedClock(now));
                Assert.That(sold.State.Dragons.Any(d => d.SpeciesId == "aurorawing"), Is.False);
                Assert.That(sold.State.Codex.Any(c => c.SpeciesId == "aurorawing"), Is.True, "bán không xóa Bách khoa");
                (GameState later, _, List<DomainEvent> ev) = U213SkyCloudTests.FlyTrips(sold.State, now, 30);
                Assert.That(ev.OfType<RewardEggGranted>(), Is.Empty, "trứng đã trao một lần: không có trứng thứ hai");
                Assert.That(later.Dragons.Any(d => d.SpeciesId == "aurorawing"), Is.False, "không còn nguồn nào: gói, trứng thế giới, lai đều không cho Aurorawing");
                Assert.That(open.State.FindDragon(aw.Uid).Locked, Is.False, "luật hiện hành không tự khóa rồng thưởng duy nhất (đề xuất UI cảnh báo / khóa, không đổi luật)");
            }
        }

        [Test]
        public void Ocean_only_species_bought_before_the_ocean_opens_wait_and_grow_in_storage_no_deadlock()
        {
            (GameState s, FixedClock clock) = NewGame();
            s = s.DeepClone();
            DragonState f = Adult("frost", "frostfin", d => { d.XpSec = 0; d.Location = DragonLocation.Storage; });
            s.Dragons.Add(f);
            Assert.That(Zones.PlacementOptions(s, s.FindDragon("frost")).Where(o => o.Blocked == null), Is.Empty, "chưa có sinh cảnh đặt được");
            clock.Advance(10 * 60_000);
            DispatchResult r = Ok(s, Online(), clock);
            Assert.That(r.State.FindDragon("frost").XpSec, Is.GreaterThan(0), "rồng trong kho vẫn lớn");
            GameState opened = r.State.DeepClone();
            // Thủy Vực chỉ mở sau khi mở đủ sinh cảnh Khởi Nguyên (luật U2.11); đường vào ocean_cool_pool đi qua bờ hồ → cửa sông.
            opened.Zones.Single(z => z.Id == "origin").UnlockedHabitatIds = Zones.HabitatsOfZone("origin");
            WorldExpansion.ApplyZoneUnlock(opened, "ocean", null);
            var opts = Zones.PlacementOptions(opened, opened.FindDragon("frost"));
            Assert.That(opts.Any(o => o.Blocked == null), Is.True, "mở Thủy Vực → đặt được: " + string.Join(", ", opts.Select(o => o.HabitatId + ":" + o.Blocked)));
        }

        [Test]
        public void Storage_full_never_loses_a_reward_egg_and_packs_are_refused_instead_of_dropped()
        {
            (GameState s, FixedClock clock) = NewGame();
            s = s.DeepClone();
            for (int i = s.Dragons.Count; i < StorageCatalog.MaxDragons; i++) s.Dragons.Add(Adult("fill-" + i.ToString("000", CultureInfo.InvariantCulture), "sprout", d => d.Location = DragonLocation.Storage));
            s.Player.Gold = MicroGold.FromWholeGold(1_000_000);
            s.Tutorial.StarterPackUsed = true;
            DispatchResult buy = CommandDispatcher.Dispatch(s, new BuyPackCommand(Rid(), "starter"), clock);
            Assert.That(buy.Ok, Is.False);
            Assert.That(buy.Error.Code, Is.EqualTo(DomainErrorCode.StorageFull));
            Assert.That(buy.State.Player.Gold, Is.EqualTo(s.Player.Gold), "không trừ vàng");
        }

        // ================================================================== báo cáo

        private static string Pct(double p) => p <= 0 ? "—" : (p * 100).ToString(p * 100 >= 10 ? "0" : p * 100 >= 1 ? "0.0" : "0.00", CultureInfo.InvariantCulture) + " %";

        public static string Table()
        {
            var sb = new StringBuilder();
            sb.AppendLine("| Loài | Độ hiếm / nhóm | Gói (mỗi lần mở) | Nguồn khác | Lai tốt nhất (không cần chính loài) | Tự nhân giống | Sinh cảnh | Thế giới sớm nhất | Production | Bảng A (mô phỏng) |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
            foreach (CollectionRow r in Build())
            {
                var other = new List<string>();
                if (r.StartingDragon) other.Add("rồng khởi đầu");
                if (r.TutorialPack) other.Add("gói hướng dẫn 100 G");
                if (r.ZoneEgg != null) other.Add($"`{r.ZoneEgg}` (mở {r.ZoneEggZone})");
                if (r.ExpeditionEgg != null) other.Add($"`{r.ExpeditionEgg}` ({r.ExpeditionEggRoute} chuyến {r.ExpeditionEggClaims})");
                string packs = r.PackOdds.Count == 0 ? "—" : string.Join(", ", PackCatalog.All.Where(p => r.PackOdds.ContainsKey(p.Id)).Select(p => $"{p.Id} {Pct(r.PackOdds[p.Id])}"));
                string pair = r.BestPair == null ? "—" : $"{r.BestPair} {Pct(r.BestPairChance)} ({r.BestPairFeeG} G)";
                sb.AppendLine($"| {r.SpeciesId} | {r.Rarity} / {r.Group} | {packs} | {(other.Count == 0 ? "—" : string.Join("; ", other))} | {pair} | {(r.SelfBreedable ? "có" : "không")} | {string.Join(", ", r.Habitats)} | {r.EarliestWorld} | {(r.ObtainableProduction ? "**có**" : "**không**")} | {(r.ObtainableWithTableA ? "có" : "không")} |");
            }
            return sb.ToString();
        }

        [Test]
        public void Write_u214_collection_matrix_when_requested()
        {
            string outDir = Environment.GetEnvironmentVariable("DRAVIO_U214_OUT");
            if (outDir == null) Assert.Ignore("Đặt DRAVIO_U214_OUT để sinh ma trận thu thập U2.14.");
            System.IO.Directory.CreateDirectory(outDir);
            System.IO.File.WriteAllText(System.IO.Path.Combine(outDir, "collection-matrix.md"), Table(), new UTF8Encoding(false));
        }
    }
}
