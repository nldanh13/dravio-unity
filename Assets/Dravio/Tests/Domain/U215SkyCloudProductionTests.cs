using System;
using System.Collections.Generic;
using System.Linq;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Progression;
using Dravio.Domain.Queries;
using Dravio.Domain.World;
using NUnit.Framework;
using static Dravio.Domain.Tests.Support.Fixtures;

namespace Dravio.Domain.Tests
{
    /// <summary>
    /// U2.15a (issue #74) — bảng sky_cloud A đã duyệt chạy trong game thật. Các test này KHÔNG dùng <see cref="SkyCloudBalance.Use"/>: chúng chạy luật production.
    /// 45 phút, 2–4 bụi sao mỗi chuyến, mốc 1 / 5 / 15 / 40 → +2 / 3 / 4 / 6, một trứng Aurorawing ở chuyến nhận thứ 15, không vàng / XP mới.
    /// </summary>
    public sealed class U215SkyCloudProductionTests
    {
        private static readonly long Day0 = new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

        [Test]
        public void Production_trips_take_45_minutes_pay_2_to_4_stardust_and_never_gold_or_xp()
        {
            Assert.That(SkyCloudBalance.Current, Is.SameAs(SkyCloudBalance.ProposedA));
            var seen = new HashSet<long>();
            for (int i = 0; i < 120; i++)
            {
                GameState s = U213SkyCloudTests.SkyWorld(Day0);
                s.SaveId = "sky-" + i;
                MicroGold gold = s.Player.Gold;
                double xp = s.FindDragon("flyer").XpSec;
                DispatchResult st = Ok(s, new StartExpeditionCommand(Rid(), SkyCloudBalance.RouteId, "flyer"), new FixedClock(Day0));
                Assert.That(st.State.Expedition.FinishAtMs - Day0, Is.EqualTo(45 * 60_000L));
                Assert.That(st.State.Expedition.Rewards.Select(kv => kv.Key), Is.EqualTo(new[] { "stardust" }));
                long qty = st.State.Expedition.Rewards.Sum(kv => (long)kv.Value);
                Assert.That(qty, Is.InRange(2, 4));
                seen.Add(qty);
                DispatchResult c = Ok(st.State, new ClaimExpeditionCommand(Rid()), new FixedClock(st.State.Expedition.FinishAtMs));
                long ach = st.Events.Concat(c.Events).OfType<AchievementUnlocked>().Sum(a => a.RewardGoldMicro);
                Assert.That(c.State.Player.Gold.Micro - gold.Micro, Is.EqualTo(ach), "chỉ vàng thành tựu sẵn có, tuyến không trả vàng");
                Assert.That(c.State.Player.Inventory["stardust"], Is.EqualTo(qty + 2), "chuyến 1 + mốc 1 (+2)");
                Assert.That(c.State.FindDragon("flyer").XpSec, Is.GreaterThanOrEqualTo(xp));
                Assert.That(c.Events.OfType<ExpeditionClaimed>().Single().Rewards.Select(kv => kv.Key), Is.EqualTo(new[] { "stardust" }));
            }
            Assert.That(seen, Is.EquivalentTo(new long[] { 2, 3, 4 }), "đủ cả ba mức");
        }

        [Test]
        public void Forty_one_production_trips_pay_each_milestone_and_the_aurorawing_egg_exactly_once()
        {
            GameState s = U213SkyCloudTests.SkyWorld(Day0);
            (GameState end, long now, List<DomainEvent> ev) = U213SkyCloudTests.FlyTrips(s, Day0, 41);
            ExplorationMilestoneReached[] ms = ev.OfType<ExplorationMilestoneReached>().Where(m => m.RouteId == SkyCloudBalance.RouteId).ToArray();
            Assert.That(ms.Select(m => (m.Claims, m.Bonus.Single().Key, m.Bonus.Single().Value, m.RewardId)), Is.EqualTo(new[]
            {
                (1, "stardust", 2, "note.cloud_first_flight"), (5, "stardust", 3, (string)null), (15, "stardust", 4, "title.cloud_rider"), (40, "stardust", 6, "title.star_warden"),
            }));
            RewardEggGranted egg = ev.OfType<RewardEggGranted>().Single();
            Assert.That((egg.RewardId, egg.SpeciesId, egg.Delivered), Is.EqualTo((SkyCloudBalance.AurorawingEggRewardId, SkyCloudBalance.AurorawingSpeciesId, true)));
            Assert.That(end.GrantedRewards.Count(r => r == SkyCloudBalance.AurorawingEggRewardId), Is.EqualTo(1));
            Assert.That(end.ExpeditionsClaimed[SkyCloudBalance.RouteId], Is.EqualTo(41));
            long trips = ev.OfType<ExpeditionClaimed>().Sum(c => c.Rewards.Sum(kv => (long)kv.Value));
            Assert.That(end.Player.Inventory["stardust"], Is.EqualTo(trips + 2 + 3 + 4 + 6), "sổ bụi sao đóng: chuyến + mốc");
            Assert.That(ProgressionQueries.Rewards(end).Titles, Does.Contain("title.cloud_rider").And.Contain("title.star_warden"));
            Assert.That(GameStateValidator.Validate(end).IsValid, Is.True, GameStateValidator.Validate(end).ToString());
            // Thêm 20 chuyến: không mốc / trứng mới.
            (_, _, List<DomainEvent> more) = U213SkyCloudTests.FlyTrips(end, now, 20);
            Assert.That(more.OfType<ExplorationMilestoneReached>().Where(m => m.RouteId == SkyCloudBalance.RouteId), Is.Empty);
            Assert.That(more.OfType<RewardEggGranted>(), Is.Empty);
        }

        [Test]
        public void Aurorawing_egg_waits_in_the_queue_while_another_egg_is_pending_and_is_delivered_after_it_is_opened()
        {
            GameState s = U213SkyCloudTests.SkyWorld(Day0);
            (GameState at14, long now, _) = U213SkyCloudTests.FlyTrips(s, Day0, 14);
            at14 = at14.DeepClone();
            at14.Player.Gold = MicroGold.FromWholeGold(1_000_000);
            DispatchResult buy = Ok(at14, new BuyPackCommand(Rid(), "starter"), new FixedClock(now));
            Assert.That(buy.State.PendingPack, Is.Not.Null);
            (GameState at15, long now15, List<DomainEvent> ev) = U213SkyCloudTests.FlyTrips(buy.State, now, 1);
            RewardEggGranted g = ev.OfType<RewardEggGranted>().Single();
            Assert.That(g.Delivered, Is.False, "đang có trứng chờ mở → trứng Aurorawing xếp hàng");
            Assert.That(at15.RewardQueue.Select(q => q.SpeciesId), Is.EqualTo(new[] { "aurorawing" }));
            Assert.That(at15.PendingPack.SpeciesId, Is.Not.EqualTo("aurorawing"));
            DispatchResult open = Ok(at15, new ClaimPackCommand(Rid(), MoveTarget.Storage), new FixedClock(now15));
            Assert.That(open.Events.OfType<RewardEggDelivered>().Single().RewardId, Is.EqualTo(SkyCloudBalance.AurorawingEggRewardId));
            Assert.That(open.State.PendingPack.SpeciesId, Is.EqualTo("aurorawing"));
            Assert.That(open.State.RewardQueue, Is.Empty);
            DispatchResult hatch = Ok(open.State, new ClaimPackCommand(Rid(), MoveTarget.Storage), new FixedClock(now15));
            Assert.That(hatch.State.Dragons.Count(d => d.SpeciesId == "aurorawing"), Is.EqualTo(1));
            Assert.That(hatch.State.GrantedRewards.Count(r => r == SkyCloudBalance.AurorawingEggRewardId), Is.EqualTo(1));
        }

        [Test]
        public void Full_storage_queues_the_egg_and_stardust_is_not_blocked_by_storage()
        {
            GameState s = U213SkyCloudTests.SkyWorld(Day0);
            for (int i = 0; i < StorageCatalog.MaxDragons - 1; i++) s.Dragons.Add(Adult("fill-" + i.ToString("000"), "sprout"));
            (GameState end, _, List<DomainEvent> ev) = U213SkyCloudTests.FlyTrips(s, Day0, 15);
            Assert.That(ev.OfType<RewardEggGranted>().Single().Delivered, Is.False);
            Assert.That(end.RewardQueue.Single().RewardId, Is.EqualTo(SkyCloudBalance.AurorawingEggRewardId));
            Assert.That(end.Player.Inventory["stardust"], Is.EqualTo(ev.OfType<ExpeditionClaimed>().Sum(c => c.Rewards.Sum(kv => (long)kv.Value)) + 2 + 3 + 4),
                "vật liệu không giới hạn bởi kho rồng");
        }

        [Test]
        public void Duplicate_claim_request_and_second_claim_never_pay_twice_in_production()
        {
            GameState s = U213SkyCloudTests.SkyWorld(Day0);
            (GameState at14, long now, _) = U213SkyCloudTests.FlyTrips(s, Day0, 14);
            GameState started = Ok(at14, new StartExpeditionCommand(Rid(), SkyCloudBalance.RouteId, "flyer"), new FixedClock(now)).State;
            var claim = new ClaimExpeditionCommand(Rid());
            var at = new FixedClock(started.Expedition.FinishAtMs);
            DispatchResult first = Ok(started, claim, at);
            DispatchResult dup = CommandDispatcher.Dispatch(first.State, claim, at);
            Assert.That((dup.Ok, dup.Duplicate, ReferenceEquals(dup.State, first.State)), Is.EqualTo((true, true, true)));
            Assert.That(dup.Events, Is.Empty);
            Fail(first.State, new ClaimExpeditionCommand(Rid()), at, DomainErrorCode.NoExpedition);
            Assert.That(first.State.GrantedRewards.Count(r => r == SkyCloudBalance.AurorawingEggRewardId), Is.EqualTo(1));
        }

        [Test]
        public void Production_sky_log_replays_to_the_identical_canonical_state()
        {
            GameState start = U213SkyCloudTests.SkyWorld(Day0);
            var log = new List<(GameCommand, long)>();
            GameState s = start;
            long now = Day0;
            for (int i = 0; i < 16; i++)
            {
                var a = new StartExpeditionCommand("s" + i, SkyCloudBalance.RouteId, "flyer");
                log.Add((a, now)); s = Ok(s, a, new FixedClock(now)).State; now = s.Expedition.FinishAtMs;
                var c = new ClaimExpeditionCommand("c" + i);
                log.Add((c, now)); s = Ok(s, c, new FixedClock(now)).State;
            }
            GameState r = start;
            foreach (var (cmd, t) in log) r = CommandDispatcher.Dispatch(r, cmd, new FixedClock(t)).State;
            Assert.That(Support.MiniJson.Show(Support.StateJson.ToJson(r)), Is.EqualTo(Support.MiniJson.Show(Support.StateJson.ToJson(s))));
        }

        [Test]
        public void Production_catalog_reaches_17_of_17_quests_and_16_of_16_species()
        {
            ProgressionAuditReport r = ProgressionQueries.AuditCatalog();
            Assert.That(QuestProposals.All.Count, Is.EqualTo(17));
            Assert.That(r.UnreachableQuests, Is.Empty);
            Assert.That(SpeciesCatalog.All.Count, Is.EqualTo(16));
            Assert.That(SpeciesCatalog.All.Select(sp => sp.Id).Where(id => !r.ObtainableSpecies.Contains(id)), Is.Empty);
            Assert.That(r.CatalogErrors, Is.Empty);
        }
    }
}
