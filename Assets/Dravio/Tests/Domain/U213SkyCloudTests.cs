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
    /// U2.13 (ADR 0208) — tuyến Thiên Giới <c>sky_cloud</c> và trứng Aurorawing.
    /// - Bảng thưởng sky_cloud CHƯA DUYỆT: game thật giữ tuyến khóa; các bảng ứng viên chỉ chạy trong phạm vi <see cref="SkyCloudBalance.Use"/>.
    /// - Trứng Aurorawing (đã duyệt): một lần tại chuyến sky_cloud thứ 15 đã nhận, qua grantedRewards + rewardQueue, kho đầy thì chờ.
    /// </summary>
    public sealed class U213SkyCloudTests
    {
        private static readonly long Day0 = new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

        /// <summary>Save đã mở cả ba thế giới, có một Zephyr Adult (biết bay) ở Thiên Giới, không rồng nào trong ngân sách sản xuất.</summary>
        internal static GameState SkyWorld(long now, params DragonState[] extra)
        {
            (GameState s, _) = NewGame();
            s = s.DeepClone();
            s.Dragons.Clear();
            s.ProductionDragonIds.Clear();
            s.Dragons.Add(Adult("flyer", "zephyr", d => d.Location = DragonLocation.Zone("sky", "sky_low_island")));
            s.Dragons.AddRange(extra);
            foreach (ZoneState z in s.Zones) if (z.Id == "origin") z.UnlockedHabitatIds = Zones.HabitatsOfZone("origin");
            foreach (string disc in ProgressCatalog.DiscoveryIds) if (!s.Discoveries.Contains(disc)) s.Discoveries.Add(disc);
            s.Discoveries.Sort(StringComparer.Ordinal);
            WorldExpansion.ApplyZoneUnlock(s, "ocean", null);
            WorldExpansion.ApplyZoneUnlock(s, "sky", null);
            s.Tutorial.Status = TutorialStatus.Completed;
            s.Tutorial.Completed = new List<string>(ProgressCatalog.TutorialStepIds);
            s.Clock = new ClockState(now, now);
            Codex.Observe(s, null);
            return s;
        }

        /// <summary>Đi và nhận <paramref name="trips"/> chuyến sky_cloud liên tiếp bằng lệnh thật; trả state và mọi event của các lệnh nhận.</summary>
        internal static (GameState s, long now, List<DomainEvent> events) FlyTrips(GameState s, long now, int trips, Action<GameState, int> beforeClaim = null)
        {
            var all = new List<DomainEvent>();
            for (int i = 1; i <= trips; i++)
            {
                DispatchResult st = Ok(s, new StartExpeditionCommand(Rid(), SkyCloudBalance.RouteId, "flyer"), new FixedClock(now));
                all.AddRange(st.Events);
                s = st.State;
                now = s.Expedition.FinishAtMs;
                beforeClaim?.Invoke(s, i);
                DispatchResult c = Ok(s, new ClaimExpeditionCommand(Rid()), new FixedClock(now));
                all.AddRange(c.Events);
                s = c.State;
            }
            return (s, now, all);
        }

        // ================================================================== bảng chưa duyệt: game thật không đổi

        [Test]
        public void Production_keeps_the_sky_route_locked_and_stardust_inactive()
        {
            Assert.That(SkyCloudBalance.Production, Is.SameAs(SkyCloudBalance.Disabled), "bảng sky_cloud chưa duyệt");
            Assert.That(SkyCloudBalance.Current.Enabled, Is.False);
            ExpeditionCatalog.TryGet("sky_cloud", out ExpeditionRoute route);
            Assert.That(route.RewardsDefined, Is.False);
            Assert.That(ExpeditionCatalog.Materials.Single(m => m.Id == "stardust").Active, Is.False);
            GameState s = SkyWorld(Day0);
            Fail(s, new StartExpeditionCommand(Rid(), "sky_cloud", "flyer"), new FixedClock(Day0), DomainErrorCode.RouteRewardsUndefined);
            ProgressionAuditReport r = ProgressionQueries.AuditCatalog();
            Assert.That(r.UnreachableQuests.Keys, Is.EqualTo(new[] { "Q_EXPEDITION_CLOUD" }));
            Assert.That(r.ObtainableSpecies, Does.Not.Contain("aurorawing"), "trứng Aurorawing cần tuyến sky_cloud");
        }

        [Test]
        public void Candidate_tables_never_pay_gold_or_xp_and_only_pay_stardust()
        {
            foreach (SkyCloudBalance b in SkyCloudBalance.Candidates)
            {
                Assert.That(b.Enabled, Is.True, b.Id);
                Assert.That(b.Milestones.Select(m => m.Claims), Is.Ordered.Ascending, b.Id);
                Assert.That(b.Milestones.Any(m => m.Claims == SkyCloudBalance.AurorawingEggClaims), Is.True, b.Id + ": trứng gộp vào mốc 15");
                using (SkyCloudBalance.Use(b))
                {
                    ExpeditionCatalog.TryGet("sky_cloud", out ExpeditionRoute route);
                    Assert.That(route.Materials, Is.EqualTo(new[] { "stardust" }), b.Id);
                    Assert.That(route.DurationMs, Is.EqualTo(b.DurationMinutes * 60_000L));
                    GameState s = SkyWorld(Day0);
                    long gold = s.Player.Gold.Micro; double xp = s.FindDragon("flyer").XpSec;
                    (GameState end, _, List<DomainEvent> ev) = FlyTrips(s, Day0, 16);
                    // Không rồng nào sinh vàng: vàng chỉ đổi do thành tựu hiện hành (vd lần đầu thám hiểm), không do tuyến.
                    long ach = ev.OfType<AchievementUnlocked>().Sum(x => x.RewardGoldMicro);
                    Assert.That(end.Player.Gold.Micro - gold, Is.EqualTo(ach), b.Id + ": tuyến không phải nguồn vàng");
                    Assert.That(ev.OfType<AchievementUnlocked>().Select(x => x.AchievementId), Is.EqualTo(new[] { "ACH_FIRST_ADULT" }),
                        "chỉ thành tựu sẵn có của fixture (rồng Adult đầu tiên), không có thưởng vàng nào của tuyến");
                    // XP: lệnh nhận không cộng XP (XP chỉ theo thời gian như mọi rồng; ở đây cùng mốc thời gian với lần quyết toán cuối).
                    DragonState f = end.FindDragon("flyer");
                    Assert.That(f.XpSec, Is.GreaterThanOrEqualTo(xp));
                    long stardust = end.Player.Inventory["stardust"];
                    long expected = ev.OfType<ExpeditionClaimed>().Sum(c => c.Rewards.Sum(kv => (long)kv.Value)) + ev.OfType<ExplorationMilestoneReached>().Sum(m => m.Bonus.Sum(kv => (long)kv.Value));
                    Assert.That(stardust, Is.EqualTo(expected));
                    Assert.That(end.ExpeditionsClaimed["sky_cloud"], Is.EqualTo(16));
                }
            }
            Assert.That(SkyCloudBalance.Current.Enabled, Is.False, "phạm vi Use đã đóng");
        }

        [Test]
        public void Claiming_twice_or_replaying_a_request_never_pays_twice()
        {
            using (SkyCloudBalance.Use(SkyCloudBalance.ProposedA))
            {
                GameState s = SkyWorld(Day0);
                s = Ok(s, new StartExpeditionCommand(Rid(), "sky_cloud", "flyer"), new FixedClock(Day0)).State;
                long done = s.Expedition.FinishAtMs;
                var claim = new ClaimExpeditionCommand(Rid());
                DispatchResult a = Ok(s, claim, new FixedClock(done));
                DispatchResult replay = CommandDispatcher.Dispatch(a.State, claim, new FixedClock(done));
                Assert.That(replay.Duplicate, Is.True);
                Assert.That(replay.State.Player.Inventory["stardust"], Is.EqualTo(a.State.Player.Inventory["stardust"]));
                Fail(a.State, new ClaimExpeditionCommand(Rid()), new FixedClock(done), DomainErrorCode.NoExpedition);
                Assert.That(a.State.ExpeditionsClaimed["sky_cloud"], Is.EqualTo(1));
            }
        }

        // ================================================================== trứng Aurorawing (đã duyệt)

        [Test]
        public void Aurorawing_egg_is_granted_exactly_once_on_the_15th_claimed_sky_trip()
        {
            using (SkyCloudBalance.Use(SkyCloudBalance.ProposedA))
            {
                GameState s = SkyWorld(Day0);
                int grantedAt = -1;
                (GameState end, long now, List<DomainEvent> ev) = FlyTrips(s, Day0, 41, (st, i) => { });
                RewardEggGranted[] grants = ev.OfType<RewardEggGranted>().ToArray();
                Assert.That(grants.Select(g => (g.RewardId, g.SpeciesId)), Is.EqualTo(new[] { ("EXPEDITION_EGG_AURORAWING", "aurorawing") }));
                Assert.That(grants[0].Delivered, Is.True, "kho trống → trứng vào pendingPack ngay");
                Assert.That(end.GrantedRewards.Count(r => r == "EXPEDITION_EGG_AURORAWING"), Is.EqualTo(1));
                // Mốc 15 cũng là mốc vật liệu + danh hiệu của bảng A.
                grantedAt = ev.OfType<ExplorationMilestoneReached>().Single(m => m.RouteId == "sky_cloud" && m.Claims == 15).Claims;
                Assert.That(grantedAt, Is.EqualTo(15));
                Assert.That(GameStateValidator.Validate(end).IsValid, Is.True, GameStateValidator.Validate(end).ToString());
            }
        }

        [Test]
        public void Aurorawing_egg_hatches_as_a_normal_founder_and_completes_the_codex()
        {
            using (SkyCloudBalance.Use(SkyCloudBalance.ProposedA))
            {
                // 15 loài khác đã có trong Codex: Aurorawing là loài cuối.
                GameState s = SkyWorld(Day0);
                foreach (DragonSpecies sp in SpeciesCatalog.All.Where(x => x.Id != "aurorawing" && x.Id != "zephyr"))
                    s.Dragons.Add(Adult("k-" + sp.Id, sp.Id));
                Codex.Observe(s, null);
                Assert.That(Codex.SpeciesDiscovered(s), Is.EqualTo(15));
                (GameState end, long now, _) = FlyTrips(s, Day0, 15);
                Assert.That(end.PendingPack, Is.Not.Null);
                Assert.That((end.PendingPack.SpeciesId, end.PendingPack.Mutation, end.PendingPack.PackId, end.PendingPack.Tutorial),
                    Is.EqualTo(("aurorawing", MutationType.Normal, "EXPEDITION_EGG_AURORAWING", false)));
                DispatchResult open = Ok(end, new ClaimPackCommand(Rid(), MoveTarget.Storage), new FixedClock(now));
                DragonState a = open.State.Dragons.Single(d => d.SpeciesId == "aurorawing");
                Assert.That(EconomyRates.GrowthStageOf(a.XpSec, SpeciesCatalog.Get("aurorawing")), Is.EqualTo(GrowthStage.Baby), "trứng nở Baby như trứng thế giới");
                Assert.That(a.Mutation, Is.EqualTo(MutationType.Normal));
                Assert.That(Codex.SpeciesDiscovered(open.State), Is.EqualTo(16));
                Assert.That(open.State.UnlockedAchievements, Does.Contain("ACH_CODEX_16"));
            }
        }

        [Test]
        public void Aurorawing_egg_waits_in_the_queue_while_an_egg_is_pending_or_storage_is_full()
        {
            using (SkyCloudBalance.Use(SkyCloudBalance.ProposedA))
            {
                GameState s = SkyWorld(Day0);
                // Kho đầy: 499 rồng + Zephyr = 500 = StorageCatalog.MaxDragons.
                for (int i = 0; i < StorageCatalog.MaxDragons - 1; i++) s.Dragons.Add(Adult("fill-" + i.ToString("000"), "sprout"));
                Assert.That(Breeding.Hatchery.HasRoomForNewEgg(s), Is.False);
                (GameState end, long now, List<DomainEvent> ev) = FlyTrips(s, Day0, 15);
                RewardEggGranted g = ev.OfType<RewardEggGranted>().Single();
                Assert.That(g.Delivered, Is.False);
                Assert.That(end.PendingPack, Is.Null);
                Assert.That(end.RewardQueue.Select(q => q.RewardId), Is.EqualTo(new[] { "EXPEDITION_EGG_AURORAWING" }));
                Assert.That(GameStateValidator.Validate(end).IsValid, Is.True, GameStateValidator.Validate(end).ToString());
                // Chuyến tiếp theo không trao lại; bán một rồng → lệnh đó giao trứng (evaluator sau lệnh).
                (GameState more, long now2, List<DomainEvent> ev2) = FlyTrips(end, now, 2);
                Assert.That(ev2.OfType<RewardEggGranted>(), Is.Empty);
                DispatchResult sold = Ok(more, new SellDragonCommand(Rid(), "fill-000"), new FixedClock(now2));
                Assert.That(sold.Events.OfType<RewardEggDelivered>().Single().RewardId, Is.EqualTo("EXPEDITION_EGG_AURORAWING"));
                Assert.That(sold.State.RewardQueue, Is.Empty);
                Assert.That(sold.State.PendingPack.SpeciesId, Is.EqualTo("aurorawing"));
            }
        }

        [Test]
        public void Validator_rejects_a_hand_edited_aurorawing_grant_before_the_milestone()
        {
            using (SkyCloudBalance.Use(SkyCloudBalance.ProposedA))
            {
                GameState s = SkyWorld(Day0);
                (GameState end, _, _) = FlyTrips(s, Day0, 3);
                GameState forged = end.DeepClone();
                forged.GrantedRewards.Add("EXPEDITION_EGG_AURORAWING");
                Assert.That(GameStateValidator.Validate(forged).ToString(), Does.Contain("before milestone"));
                GameState dup = end.DeepClone();
                dup.ExpeditionsClaimed["sky_cloud"] = 15;
                dup.GrantedRewards.Add("EXPEDITION_EGG_AURORAWING");
                Assert.That(GameStateValidator.Validate(dup).IsValid, Is.True);
                dup.GrantedRewards.Add("EXPEDITION_EGG_AURORAWING");
                Assert.That(GameStateValidator.Validate(dup).IsValid, Is.False, "trùng ID bị từ chối");
            }
        }

        [Test]
        public void Approved_table_makes_every_quest_reachable_and_all_sixteen_species_obtainable()
        {
            foreach (SkyCloudBalance b in SkyCloudBalance.Candidates)
                using (SkyCloudBalance.Use(b))
                {
                    ProgressionAuditReport r = ProgressionQueries.AuditCatalog();
                    Assert.That(r.CatalogErrors, Is.Empty, b.Id);
                    Assert.That(r.UnreachableQuests, Is.Empty, b.Id + ": cả 17 nhiệm vụ đạt được");
                    Assert.That(r.UnreachableZones, Is.Empty);
                    Assert.That(r.ObtainableSpecies.Count, Is.EqualTo(SpeciesCatalog.All.Count), b.Id + ": 16/16 loài");
                    Assert.That(r.WaveOf("Q_EXPEDITION_CLOUD"), Is.GreaterThan(r.WaveOf("sky")));
                }
        }

        [Test]
        public void Quest_cloud_becomes_completable_once_the_route_is_enabled()
        {
            using (SkyCloudBalance.Use(SkyCloudBalance.ProposedA))
            {
                GameState s = SkyWorld(Day0);
                s.CompletedQuests = new List<string> { "QUEST_AQUATIC_EXPLORATION", "QUEST_SKY_EXPLORATION" };
                QuestStatusView v = QuestProposals.Evaluate(s).Single(q => q.QuestId == "Q_EXPEDITION_CLOUD");
                Assert.That(v.Requirements.Single(r => r.Def.Kind == QuestRequirementKind.RouteAvailable).State, Is.EqualTo(RequirementState.Met));
                Assert.That(v.CanComplete, Is.True);
                DispatchResult r2 = Ok(s, new CompleteQuestCommand(Rid(), "Q_EXPEDITION_CLOUD"), new FixedClock(Day0));
                Assert.That(r2.Events.OfType<QuestRewarded>().Single().RewardId, Is.EqualTo("note.cloud_terrace"));
            }
        }

        [Test]
        public void Exploration_dto_shows_the_aurorawing_milestone_for_the_ui()
        {
            GameState s = SkyWorld(Day0);
            RouteExplorationDto off = ProgressionQueries.ExplorationRoutes(s).Single(r => r.RouteId == "sky_cloud");
            Assert.That((off.RewardsDefined, off.NextMilestoneAt, off.NextMilestoneEggSpeciesId), Is.EqualTo((false, (int?)15, "aurorawing")));
            using (SkyCloudBalance.Use(SkyCloudBalance.ProposedA))
            {
                RouteExplorationDto on = ProgressionQueries.ExplorationRoutes(s).Single(r => r.RouteId == "sky_cloud");
                Assert.That((on.RewardsDefined, on.NextMilestoneAt, on.NextMilestoneBonusEach, on.NextMilestoneRewardId), Is.EqualTo((true, (int?)1, 2, "note.cloud_first_flight")));
                Assert.That(on.Materials, Is.EqualTo(new[] { "stardust" }));
            }
        }
    }
}
