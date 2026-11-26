using System;
using System.Collections.Generic;
using System.Linq;
using Dravio.Domain.Biology;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Expedition;
using Dravio.Domain.Queries;
using Dravio.Domain.World;
using NUnit.Framework;
using static Dravio.Domain.Tests.Support.Fixtures;

namespace Dravio.Domain.Tests
{
    /// <summary>
    /// U2.8 — mở rộng thế giới, di chuyển giữa thế giới, thám hiểm, API truy vấn. Phần di chuyển dùng WorldGraph.CanTravel (port TS, golden U2.3);
    /// luật cửa ngõ, mở zone, thám hiểm và API truy vấn là implementation mới theo GDD 3.1 (tr. 5, 22).
    /// </summary>
    public sealed class WorldExpansionTests
    {
        private const long G = 1_000_000;

        private static (GameState, FixedClock) Game()
        {
            var clock = new FixedClock(T0);
            GameState s = InitialStateFactory.Create("world", T0, Rng.Create(9));
            s.Player.Gold = MicroGold.FromWholeGold(10_000);
            s.UnlockedAchievements.Add("ACH_FIRST_ADULT"); s.ClaimedAchievements.Add("ACH_FIRST_ADULT");
            return (s, clock);
        }

        /// <summary>Mở cả ba thế giới và mọi microhabitat (hàm engine của hệ nhiệm vụ tương lai + luật ADR 0009 cho Khởi Nguyên).</summary>
        private static GameState AllOpen(GameState s0)
        {
            GameState s = s0.DeepClone();
            foreach (string d in ProgressCatalog.DiscoveryIds) if (!s.Discoveries.Contains(d)) Zones.RecordDiscovery(s, d, new List<DomainEvent>());
            foreach (string h in new[] { "origin_warm_rocks", "origin_pondbank" }) if (!s.FindZone("origin").UnlockedHabitatIds.Contains(h)) Zones.ApplyUnlock(s, h, new List<DomainEvent>());
            foreach (string z in new[] { "ocean", "sky" }) if (!s.FindZone(z).Unlocked) Assert.That(WorldExpansion.ApplyZoneUnlock(s, z, new List<DomainEvent>()), Is.Null);
            return s;
        }

        private static GameState With(GameState s, params DragonState[] ds) { GameState c = s.DeepClone(); c.Dragons.AddRange(ds); return Dravio.Domain.Tests.Support.Fixtures.Normalize(c); }
        private static DragonState A(string uid, string species, Action<DragonState> extra = null) => Adult(uid, species, extra);

        // ---------------- thế giới & sinh cảnh ----------------

        [Test]
        public void Three_worlds_seven_habitats_and_zone_unlock_opens_every_habitat_without_changing_capacity_or_gold()
        {
            var (s0, _) = Game();
            Assert.That(WorldCatalog.ZoneIds, Is.EqualTo(new[] { "origin", "ocean", "sky" }));
            Assert.That(HabitatCatalog.All.Select(h => h.Id), Is.EquivalentTo(new[] { "origin_meadow", "origin_pondbank", "origin_warm_rocks", "ocean_estuary", "ocean_reef", "ocean_cool_pool", "sky_low_island" }));
            var events = new List<DomainEvent>();
            GameState s = s0.DeepClone();
            Assert.That(WorldExpansion.ApplyZoneUnlock(s, "ocean", events), Is.Null);
            Assert.That(s.FindZone("ocean").UnlockedHabitatIds, Is.EqualTo(new[] { "ocean_estuary", "ocean_reef", "ocean_cool_pool" }));
            Assert.That(events.Select(e => e.Type), Is.EqualTo(new[] { "zoneUnlocked", "habitatUnlocked", "habitatUnlocked", "habitatUnlocked" }));
            Assert.That((s.FindZone("ocean").Capacity, s.Player.Gold), Is.EqualTo((5, s0.Player.Gold)));
            Assert.That(WorldExpansion.ApplyZoneUnlock(s, "ocean", events), Is.EqualTo(DomainErrorCode.AlreadyThere));
            Assert.That(WorldExpansion.ApplyZoneUnlock(s, "moon", events), Is.EqualTo(DomainErrorCode.ZoneLocked));
            Assert.That(GameStateValidator.Validate(s).IsValid, Is.True, GameStateValidator.Validate(s).ToString());
        }

        [Test]
        public void World_locks_stay_in_force_ocean_and_sky_are_locked_in_a_new_save()
        {
            var (s, clock) = Game();
            GameState w = With(s, A("dr", "droplet"), A("ze", "zephyr"));
            Fail(w, new MoveDragonCommand(Rid(), "dr", MoveTarget.Zone("ocean", "ocean_reef")), clock, DomainErrorCode.ZoneLocked);
            Fail(w, new MoveDragonCommand(Rid(), "ze", MoveTarget.Zone("sky", "sky_low_island")), clock, DomainErrorCode.ZoneLocked);
            Fail(w, new StartExpeditionCommand(Rid(), "ocean_reef", "dr"), clock, DomainErrorCode.RouteLocked);
            Fail(w, new StartExpeditionCommand(Rid(), "sky_cloud", "ze"), clock, DomainErrorCode.RouteLocked);
        }

        // ---------------- di chuyển: 16 loài ----------------

        [Test]
        public void Every_species_moves_only_where_its_locomotion_and_biology_allow_and_routes_agree_with_TS_canTravel()
        {
            var (s0, clock) = Game();
            GameState open = AllOpen(s0);
            foreach (DragonSpecies sp in SpeciesCatalog.All)
            {
                IReadOnlyList<Locomotion> loco = SpeciesCatalog.GetBiology(sp.Id).Locomotion;
                GameState s = With(open, A("x", sp.Id));
                int reachable = 0;
                foreach (HabitatProfile h in HabitatCatalog.All)
                {
                    DomainErrorCode? got = Ecosystem.TravelEligibility(s, "x", MoveTarget.Zone(h.ZoneId, h.Id));
                    bool route = WorldGraph.CanTravel("origin", h.ZoneId, loco);
                    bool suitable = Suitability.ForSpecies(sp.Id, h.Id).Allowed;
                    DomainErrorCode? expected = !suitable ? DomainErrorCode.HabitatUnsuitable : !route ? DomainErrorCode.NoRoute : (DomainErrorCode?)null;
                    Assert.That(got, Is.EqualTo(expected), sp.Id + " → " + h.Id);
                    if (got == null)
                    {
                        reachable++;
                        if (h.ZoneId == "ocean") Assert.That(loco, Has.Member(Locomotion.Swim), sp.Id + " vào Thủy Vực phải bơi được");
                        if (h.ZoneId == "sky") Assert.That(loco, Has.Member(Locomotion.Fly), sp.Id + " lên Thiên Giới phải bay được");
                        DispatchResult r = Ok(s, new MoveDragonCommand(Rid(), "x", MoveTarget.Zone(h.ZoneId, h.Id)), clock);
                        Assert.That(r.State.FindDragon("x").Location.HabitatId, Is.EqualTo(h.Id));
                    }
                }
                Assert.That(reachable, Is.GreaterThan(0), sp.Id + " phải có ít nhất một sinh cảnh hợp lệ tới được (GDD tr. 5)");
                foreach (string from in WorldCatalog.ZoneIds)
                    foreach (string to in WorldCatalog.ZoneIds)
                        Assert.That(Travel.FindRoute(open, from, to, loco) != null, Is.EqualTo(WorldGraph.CanTravel(from, to, loco)), sp.Id + " " + from + "→" + to);
            }
        }

        [Test]
        public void Gateways_must_be_open_and_zone_to_zone_routes_go_through_origin()
        {
            var (s0, clock) = Game();
            GameState s = s0.DeepClone();
            WorldExpansion.ApplyZoneUnlock(s, "ocean", null);
            GameState w = With(s, A("dr", "droplet"));
            Fail(w, new MoveDragonCommand(Rid(), "dr", MoveTarget.Zone("ocean", "ocean_estuary")), clock, DomainErrorCode.NoRoute); // bờ hồ (cửa ngõ) chưa mở
            Zones.RecordDiscovery(w, "DISC_AQUATIC_HABITAT", new List<DomainEvent>());
            Zones.ApplyUnlock(w, "origin_pondbank", new List<DomainEvent>());
            Assert.That(Travel.RouteFor(w, w.FindDragon("dr"), "ocean"), Is.Null, "rồng trong kho không có điểm xuất phát (kho trung lập)");
            Assert.That(Travel.CanEnter(w, w.FindDragon("dr"), "ocean"), Is.True, "vào Thủy Vực qua cạnh cửa sông đã mở, Droplet biết bơi");
            TravelRoute route = Travel.FindRoute(w, "origin", "ocean", SpeciesCatalog.GetBiology("droplet").Locomotion);
            Assert.That((string.Join(">", route.Zones), route.Edges.Single()), Is.EqualTo(("origin>ocean", "origin_ocean_estuary")));
            GameState inOcean = Ok(w, new MoveDragonCommand(Rid(), "dr", MoveTarget.Zone("ocean", "ocean_estuary")), clock).State;
            // Từ Thủy Vực về Khởi Nguyên rồi lên Thiên Giới cần bơi + bay: Droplet chỉ bơi/đi.
            GameState all = AllOpen(inOcean);
            Assert.That(Travel.RouteFor(all, all.FindDragon("dr"), "sky"), Is.Null);
            GameState stored = Ok(all, new MoveDragonCommand(Rid(), "dr", MoveTarget.Storage), clock).State;
            Assert.That(stored.FindDragon("dr").Location.Kind, Is.EqualTo(LocationKind.Storage), "về kho luôn được");
            // Kho trung lập: không phải lối tắt. Droplet trong kho không lên được Thiên Giới (không bay), Emberling không xuống được Thủy Vực (không bơi).
            Assert.That(Ecosystem.TravelEligibility(stored, "dr", MoveTarget.Zone("sky", "sky_low_island")), Is.Not.Null);
            Assert.That(Travel.CanEnter(stored, stored.FindDragon("dr"), "sky"), Is.False);
            Assert.That(Travel.FromZone(stored.FindDragon("dr")), Is.Null, "kho không thuộc Origin/Ocean/Sky");
        }

        [Test]
        public void Capacity_is_5_per_world_and_the_production_budget_is_shared_across_worlds()
        {
            var (s0, clock) = Game();
            GameState s = AllOpen(s0);
            s.Player.Gold = MicroGold.FromWholeGold(1_000_000);
            var swimmers = Enumerable.Range(0, 6).Select(i => A("w" + i, "droplet")).ToArray();
            s = With(s, swimmers);
            for (int i = 0; i < 5; i++) s = Ok(s, new MoveDragonCommand(Rid(), "w" + i, MoveTarget.Zone("ocean", "ocean_estuary")), clock).State;
            Fail(s, new MoveDragonCommand(Rid(), "w5", MoveTarget.Zone("ocean", "ocean_estuary")), clock, DomainErrorCode.ZoneFull);
            // Ngân sách 5 + 2L dùng chung: rồng Emberling ở Khởi Nguyên đã chiếm 1 slot → 4 rồng Thủy Vực vào được, rồng thứ 5 cần nâng cấp.
            for (int i = 0; i < 4; i++) s = Ok(s, new SetProductionSlotCommand(Rid(), "w" + i, true), clock).State;
            Fail(s, new SetProductionSlotCommand(Rid(), "w4", true), clock, DomainErrorCode.ProductionBudgetFull);
            s = Ok(s, new BuyUpgradeCommand(Rid(), "islandCapacityLevel"), clock).State;
            Assert.That(Ok(s, new SetProductionSlotCommand(Rid(), "w4", true), clock).State.ProductionDragonIds.Count, Is.EqualTo(6));
            EcosystemSummary sum = Ecosystem.Summary(s);
            Assert.That(sum.OccupancyByZone.Single(z => z.Key == "ocean").Value, Is.EqualTo(5));
        }

        [Test]
        public void Moving_settles_production_at_the_moment_of_transfer()
        {
            var (s0, clock) = Game();
            GameState s = AllOpen(s0);
            s = With(s, A("dr", "droplet", d => d.Location = DragonLocation.Zone("origin", "origin_pondbank")));
            s.ProductionDragonIds.Clear();
            s.ProductionDragonIds.Add("dr");
            clock.Advance(60_000);
            DispatchResult r = Ok(s, new MoveDragonCommand(Rid(), "dr", MoveTarget.Storage), clock);
            long earned = r.State.Player.TotalGoldEarned.Micro - s.Player.TotalGoldEarned.Micro;
            Assert.That(earned, Is.EqualTo(60 * 25_000), "60 s của Droplet Adult (0,025 G/s) được quyết toán trước khi vào kho");
            clock.Advance(60_000);
            long later = Ok(r.State, Online(), clock).State.TotalGoldEarnedOf() - r.State.TotalGoldEarnedOf();
            Assert.That(later, Is.EqualTo(0), "rồng trong kho không sinh vàng");
        }

        // ---------------- xung đột trạng thái ----------------

        [Test]
        public void Breeding_expedition_and_care_exclude_each_other()
        {
            var (s0, clock) = Game();
            GameState s = With(s0, A("pa", "emberling"), A("pb", "sprout"), A("x", "emberling"), A("ca", "emberling", d => d.Location = DragonLocation.Care("c1")));
            GameState breeding = Ok(s, new StartBreedCommand(Rid(), "pa", "pb"), clock).State;
            Fail(breeding, new StartExpeditionCommand(Rid(), "origin_forest", "pa"), clock, DomainErrorCode.DragonBusy);
            GameState away = Ok(breeding, new StartExpeditionCommand(Rid(), "origin_forest", "x"), clock).State;
            Fail(away, new StartBreedCommand(Rid(), "x", "pb"), clock, DomainErrorCode.NestBusy);
            GameState awayOnly = Ok(s, new StartExpeditionCommand(Rid(), "origin_forest", "x"), clock).State;
            Fail(awayOnly, new StartBreedCommand(Rid(), "x", "pb"), clock, DomainErrorCode.BreedBusy);
            Fail(awayOnly, new MoveDragonCommand(Rid(), "x", MoveTarget.Storage), clock, DomainErrorCode.DragonUnavailable);
            Fail(awayOnly, new SellDragonCommand(Rid(), "x"), clock, DomainErrorCode.DragonBusy);
            Fail(s, new StartExpeditionCommand(Rid(), "origin_forest", "ca"), clock, DomainErrorCode.DragonBusy);
            DragonActions act = Ecosystem.Actions(awayOnly, "x", clock.NowMs());
            Assert.That((act.Sell, act.MoveToStorage, act.Breed), Is.EqualTo(((DomainErrorCode?)DomainErrorCode.DragonBusy, (DomainErrorCode?)DomainErrorCode.DragonUnavailable, (Breeding.BreedRejection?)Breeding.BreedRejection.Busy)));
        }

        // ---------------- thám hiểm ----------------

        [Test]
        public void Expedition_locks_route_seed_and_rewards_at_start_and_pays_once()
        {
            var (s0, clock) = Game();
            GameState s = With(s0, A("x", "emberling"));
            Fail(s, new StartExpeditionCommand(Rid(), "moon_trip", "x"), clock, DomainErrorCode.UnknownRoute);
            DispatchResult st = Ok(s, new StartExpeditionCommand(Rid(), "origin_forest", "x"), clock);
            ExpeditionJob job = st.State.Expedition;
            Assert.That((job.RouteId, job.FinishAtMs - job.StartedAtMs, st.State.FindDragon("x").Activity), Is.EqualTo(("origin_forest", 15 * 60_000L, DragonActivity.Expedition)));
            int total = job.Rewards.Sum(kv => kv.Value);
            Assert.That(total, Is.InRange(2, 4));
            Assert.That(job.Rewards.Select(kv => kv.Key), Is.SubsetOf(new[] { "wood", "seed" }));
            Assert.That(Gold(st.State) + 0, Is.EqualTo(Gold(s) + Earned(s, st.State)), "không phí (GDD không nêu phí)");
            // Tất định: cùng state → cùng phần thưởng; requestId khác không đổi kết quả.
            Assert.That(Ok(s, new StartExpeditionCommand("other", "origin_forest", "x"), clock).State.Expedition.Rewards, Is.EqualTo(job.Rewards));
            Fail(st.State, new StartExpeditionCommand(Rid(), "origin_forest", st.State.Dragons[0].Uid), clock, DomainErrorCode.ExpeditionBusy);
            Fail(st.State, new ClaimExpeditionCommand(Rid()), clock, DomainErrorCode.ExpeditionNotReady);
            clock.Set(job.FinishAtMs);
            DispatchResult c = Ok(st.State, new ClaimExpeditionCommand("claim-1"), clock);
            // U2.11: chuyến đầu tiên của tuyến đạt mốc thám hiểm 1 → thêm 2 đơn vị mỗi vật liệu của tuyến (gỗ, hạt giống), một lần.
            foreach (string m in new[] { "wood", "seed" })
                Assert.That(c.State.Player.Inventory[m], Is.EqualTo(job.Rewards.Where(kv => kv.Key == m).Sum(kv => kv.Value) + 2), m);
            Assert.That(c.Events.OfType<Dravio.Domain.Progression.ExplorationMilestoneReached>().Single().Claims, Is.EqualTo(1));
            Assert.That((c.State.Expedition, c.State.FindDragon("x").Activity), Is.EqualTo(((ExpeditionJob)null, DragonActivity.Idle)));
            DispatchResult dup = CommandDispatcher.Dispatch(c.State, new ClaimExpeditionCommand("claim-1"), clock);
            Assert.That((dup.Duplicate, dup.State.Player.Inventory.Values.Sum()), Is.EqualTo((true, (long)total + 4)));
            Fail(c.State, new ClaimExpeditionCommand(Rid()), clock, DomainErrorCode.NoExpedition);
            Assert.That(c.State.Expedition, Is.Null, "không tự khởi hành tiếp");
        }

        private static long Gold(GameState s) => s.Player.Gold.Micro;
        private static long Earned(GameState a, GameState b) => b.Player.TotalGoldEarned.Micro - a.Player.TotalGoldEarned.Micro;

        [Test]
        public void Expedition_requirements_follow_GDD_land_swim_fly_and_adult()
        {
            var (s0, clock) = Game();
            GameState s = AllOpen(With(s0, A("le", "leviathan"), A("ze", "zephyr"), A("dr", "droplet"), A("baby", "emberling", d => d.XpSec = 0)));
            string Detail(string route, string uid) { Expeditions.CheckStart(s, route, uid, out string d); return d; }
            Fail(s, new StartExpeditionCommand(Rid(), "origin_forest", "le"), clock, DomainErrorCode.DragonUnsuited);
            Assert.That(Detail("origin_forest", "le"), Is.EqualTo("requires-land"), "Leviathan không lên đất (ADR 0003 #3)");
            Assert.That(Detail("ocean_reef", "ze"), Is.EqualTo("requires-swim"));
            Expeditions.DragonBlock(s, ExpeditionCatalog.Routes.Single(r => r.Id == "sky_cloud"), s.FindDragon("dr"), out string skyDetail);
            Assert.That(skyDetail, Is.EqualTo("requires-fly"));
            Assert.That(Detail("origin_forest", "baby"), Is.EqualTo("not-adult"));
            Assert.That(Ok(s, new StartExpeditionCommand(Rid(), "ocean_reef", "le"), clock).State.Expedition.Rewards.Select(k => k.Key), Is.SubsetOf(new[] { "crystal", "coral" }));
            // Sân mây: bảng thưởng chưa duyệt (GDD "bụi sao" không có ID chuẩn) → tuyến khóa (ADR U2.8-0201).
            Fail(s, new StartExpeditionCommand(Rid(), "sky_cloud", "ze"), clock, DomainErrorCode.RouteRewardsUndefined);
            foreach (DragonSpecies sp in SpeciesCatalog.All)
            {
                var loco = SpeciesCatalog.GetBiology(sp.Id).Locomotion;
                GameState one = With(s, A("t", sp.Id));
                Assert.That(Expeditions.CheckStart(one, "ocean_reef", "t", out _) == null, Is.EqualTo(loco.Contains(Locomotion.Swim)), sp.Id);
                ExpeditionRoute sky = ExpeditionCatalog.Routes.Single(r => r.Id == "sky_cloud");
                Assert.That(Expeditions.DragonBlock(one, sky, one.FindDragon("t"), out _) == null, Is.EqualTo(loco.Contains(Locomotion.Fly)), sp.Id + " (điều kiện bay vẫn kiểm dù tuyến đang khóa)");
                Assert.That(Expeditions.CheckStart(one, "origin_forest", "t", out _) == null, Is.EqualTo(sp.Realms.Contains(Realm.Land)), sp.Id);
            }
        }

        [Test]
        public void Expedition_pauses_production_and_completes_offline_releasing_the_dragon_exactly_at_finish()
        {
            var (s0, clock) = Game();
            GameState s = With(s0, A("x", "emberling", d => d.Location = DragonLocation.Zone("origin", "origin_meadow")));
            s.ProductionDragonIds.Add("x");
            s.ProductionDragonIds.RemoveAll(id => id != "x");
            GameState away = Ok(s, new StartExpeditionCommand(Rid(), "origin_forest", "x"), clock).State;
            Assert.That(away.FindDragon("x").Location.HabitatId, Is.EqualTo("origin_meadow"), "rồng giữ chỗ trong khu khi đi");
            clock.Set(away.Expedition.FinishAtMs + 120_000);
            DispatchResult r = Ok(away, Offline(), clock);
            Assert.That(Earned(away, r.State), Is.EqualTo(120 * 25_000 * 3 / 4), "chỉ 120 s sau khi về mới sinh vàng (offline 75 %)");
            Assert.That(r.Events.OfType<ExpeditionReady>().Single().AtMs, Is.EqualTo(away.Expedition.FinishAtMs));
            Assert.That((r.State.Expedition.Status, r.State.FindDragon("x").Activity), Is.EqualTo((ExpeditionStatus.Ready, DragonActivity.Idle)));
        }

        [Test]
        public void Expedition_reward_quantities_cover_2_to_4_across_seeds()
        {
            var counts = new Dictionary<int, int>();
            var (s0, clock) = Game();
            for (int i = 0; i < 600; i++)
            {
                GameState s = With(s0, A("x", "emberling"));
                s.SaveId = "exp-" + i;
                int q = Ok(s, new StartExpeditionCommand("r", "origin_forest", "x"), clock).State.Expedition.Rewards.Sum(kv => kv.Value);
                counts[q] = counts.TryGetValue(q, out int c) ? c + 1 : 1;
            }
            Assert.That(counts.Keys.OrderBy(k => k), Is.EqualTo(new[] { 2, 3, 4 }));
            foreach (int k in counts.Keys) Assert.That(counts[k], Is.InRange(150, 250), "đều trên 2/3/4 (±4,5σ)");
        }

        // ---------------- API truy vấn ----------------

        [Test]
        public void Query_API_matches_command_results_and_counts_every_dragon_once()
        {
            var (s0, clock) = Game();
            GameState s = AllOpen(With(s0, A("pa", "emberling"), A("pb", "sprout"), A("x", "zephyr"), A("ca", "emberling", d => d.Location = DragonLocation.Care("c1")),
                A("rl", "emberling", d => d.Location = DragonLocation.Released), A("lk", "droplet", d => d.Locked = true)));
            s = Ok(s, new StartBreedCommand(Rid(), "pa", "pb"), clock).State;
            s = Ok(s, new StartExpeditionCommand(Rid(), "origin_forest", "x"), clock).State;
            EcosystemSummary sum = Ecosystem.Summary(s);
            Assert.That((sum.Living, sum.Released, sum.Breeding, sum.Expedition, sum.Care, sum.Storage, sum.ActiveInHabitats),
                Is.EqualTo((6, 1, 2, 1, 1, 1, 1)));
            Assert.That(sum.Living, Is.EqualTo(sum.ActiveInHabitats + sum.Storage + sum.Care + sum.Shelter + sum.Breeding + sum.Expedition));
            foreach (DragonState d in s.Dragons)
            {
                DragonActions a = Ecosystem.Actions(s, d.Uid, clock.NowMs());
                DispatchResult sell = CommandDispatcher.Dispatch(s, new SellDragonCommand(Rid(), d.Uid), clock);
                Assert.That(a.Sell, Is.EqualTo(sell.Ok ? (DomainErrorCode?)null : sell.Error.Code), d.Uid + " sell");
                DispatchResult mv = CommandDispatcher.Dispatch(s, new MoveDragonCommand(Rid(), d.Uid, MoveTarget.Storage), clock);
                Assert.That(a.MoveToStorage, Is.EqualTo(mv.Ok ? (DomainErrorCode?)null : mv.Error.Code), d.Uid + " move");
                foreach (TravelOption o in Ecosystem.TravelOptions(s, d.Uid))
                {
                    DispatchResult m = CommandDispatcher.Dispatch(s, new MoveDragonCommand(Rid(), d.Uid, MoveTarget.Zone(o.ZoneId, o.HabitatId)), clock);
                    Assert.That(o.Blocked, Is.EqualTo(m.Ok ? (DomainErrorCode?)null : m.Error.Code), d.Uid + " → " + o.HabitatId);
                }
                foreach (var kv in a.Expeditions)
                {
                    DispatchResult e = CommandDispatcher.Dispatch(s, new StartExpeditionCommand(Rid(), kv.Key, d.Uid), clock);
                    Assert.That(kv.Value, Is.EqualTo(e.Ok ? (DomainErrorCode?)null : e.Error.Code), d.Uid + " " + kv.Key);
                }
            }
            List<ZoneOverview> world = Ecosystem.World(s);
            Assert.That(world.Select(z => z.ZoneId + ":" + z.Unlocked + ":" + z.Habitats.Count), Is.EqualTo(new[] { "origin:True:3", "ocean:True:3", "sky:True:1" }));
            Assert.That(world.Single(z => z.ZoneId == "ocean").Edges.Single(), Is.EqualTo(new KeyValuePair<string, bool>("origin_ocean_estuary", true)));
            Assert.That(Ecosystem.ExpeditionOptions(s).Select(o => o.Blocked), Is.EqualTo(new DomainErrorCode?[] { DomainErrorCode.ExpeditionBusy, DomainErrorCode.ExpeditionBusy, DomainErrorCode.RouteRewardsUndefined }));
            DiscoveryProgress dp = Ecosystem.Discovery(s);
            Assert.That((dp.SpeciesTotal, dp.CodexCellsTotal, dp.ZonesUnlocked.Count), Is.EqualTo((16, 64, 3)));
            // Truy vấn không đổi state.
            string before = Support.MiniJson.Show(Support.StateJson.ToJson(s));
            Ecosystem.World(s); Ecosystem.Summary(s); Ecosystem.ExpeditionOptions(s); Ecosystem.Discovery(s); Ecosystem.Actions(s, "lk", clock.NowMs());
            Assert.That(Support.MiniJson.Show(Support.StateJson.ToJson(s)), Is.EqualTo(before));
        }
    }

    internal static class StateExt
    {
        public static long TotalGoldEarnedOf(this GameState s) => s.Player.TotalGoldEarned.Micro;
    }
}
