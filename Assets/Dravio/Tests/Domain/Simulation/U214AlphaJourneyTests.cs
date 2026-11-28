using System;
using System.Collections.Generic;
using System.Linq;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Progression;
using Dravio.Domain.World;
using NUnit.Framework;

namespace Dravio.Domain.Tests.Simulation
{
    /// <summary>
    /// U2.14 — hành trình Alpha end-to-end. Save mới, người chơi theo hướng dẫn (Guided, 3 × 10 phút/ngày), MỌI hành động là lệnh chính thức
    /// (kể cả quan sát: <c>observeDragon</c>), luật production. Sau mỗi lệnh kiểm sổ vàng, sổ vật liệu, trần sản xuất, và thưởng một lần (<see cref="AlphaJourney"/>).
    /// U2.15: production = bảng sky_cloud A đã duyệt (issue #74); hành trình với bảng TẮT giữ làm đối chứng U2.14 qua <c>EconomyVariant.Alpha(SkyCloudBalance.Disabled)</c>.
    /// </summary>
    public sealed class U214AlphaJourneyTests
    {
        internal static readonly Schedule ThreeTimesTen = EconomySimulationTests.Schedules[1];
        internal const double JourneyDays = 40;

        private static readonly Dictionary<int, AlphaJourney> Cache = new Dictionary<int, AlphaJourney>();

        /// <summary>Hành trình production của seed (dùng chung giữa các test trong lớp, chạy một lần).</summary>
        internal static AlphaJourney Production(int seed)
        {
            lock (Cache)
            {
                if (!Cache.TryGetValue(seed, out AlphaJourney j)) Cache[seed] = j = AlphaJourney.Run(Policy.Guided, ThreeTimesTen, seed, JourneyDays);
                return j;
            }
        }

        /// <summary>Các mốc của đề bài U2.14 mục I, theo thứ tự hành trình.</summary>
        internal static readonly (string key, string label)[] AlphaMilestones =
        {
            ("tutorial.completed", "Hoàn thành tutorial"),
            ("feed.first", "Cho ăn (feed + consumeFood)"),
            ("adult.first", "Rồng đầu tiên thành Adult"),
            ("pack.bought", "Mua gói trứng bằng vàng"),
            ("breed.started", "Bắt đầu lai"),
            ("egg.tapped", "Gõ trứng (ấp)"),
            ("breed.hatched", "Nở rồng lai"),
            ("origin.habitats.all", "Mở đủ sinh cảnh Khởi Nguyên"),
            ("observe.first", "Quan sát (observeDragon)"),
            ("quest:Q_RESEARCH_ETHOLOGIST", "Nghiên cứu sinh học: Ethologist"),
            ("quest:Q_RESEARCH_SKY_CHRONICLE", "Nghiên cứu sinh học: Sky Chronicle (cuối chuỗi)"),
            ("zone:ocean", "Mở Thủy Vực"),
            ("zone:sky", "Mở Thiên Giới"),
            ("expedition:origin_forest", "Thám hiểm rừng (nhận thưởng)"),
            ("expedition:ocean_reef", "Thám hiểm rạn (nhận thưởng)"),
            // U2.15: slot thám hiểm chung chuyển sang sân mây (chi phí cơ hội có chủ đích, Q7 U2.13) → mốc rạn 15 không còn trong 40 ngày; giữ mốc rạn 5.
            ("milestone:ocean_reef:5", "Mốc thám hiểm rạn 5"),
            ("milestone:sky_cloud:15", "Mốc thám hiểm sân mây 15"),
            ("project:1", "Dự án Sanctuary đầu tiên"),
            ("project:10", "Dự án Sanctuary thứ 10"),
            // U2.15: bảng A là production.
            ("expedition:sky_cloud", "Thám hiểm sân mây (nhận thưởng)"),
            ("quest:Q_EXPEDITION_CLOUD", "Nhiệm vụ Q_EXPEDITION_CLOUD"),
            ("reward-egg:" + SkyCloudBalance.AurorawingEggRewardId, "Trứng Aurorawing (chuyến sân mây thứ 15)"),
        };

        [Test]
        public void Alpha_journey_from_a_new_save_reaches_every_alpha_milestone_through_official_commands([Values(1, 2, 3)] int seed)
        {
            AlphaJourney j = Production(seed);
            Assert.That(j.Violations, Is.Empty, string.Join("\n", j.Violations.Take(10)));
            foreach (var (key, label) in AlphaMilestones)
                Assert.That(j.FirstAt.ContainsKey(key), Is.True, $"seed {seed}: chưa đạt '{label}' ({key}) sau {JourneyDays} ngày");
            GameState s = j.Sim.State;
            Assert.That(s.Tutorial.Status, Is.EqualTo(TutorialStatus.Completed));
            Assert.That(s.Tutorial.Completed, Is.EqualTo(ProgressCatalog.TutorialStepIds));
            Assert.That(s.Zones.All(z => z.Unlocked), Is.True);
            Assert.That(GameStateValidator.Validate(s).IsValid, Is.True, GameStateValidator.Validate(s).ToString());
            // U2.15: 17 / 17 nhiệm vụ với bảng A.
            var all = QuestProposals.All.Select(q => q.Id).ToList();
            var done = s.CompletedQuests.Concat(s.CompletedSideQuests).ToList();
            Assert.That(all.Count, Is.EqualTo(17));
            Assert.That(all.Except(done), Is.Empty, "seed " + seed);
            Assert.That(done.Count, Is.EqualTo(done.Distinct().Count()));
            // Hành trình thật dùng đủ các lệnh của đề bài.
            foreach (string type in new[] { "feed", "consumeFood", "buyTutorialPack", "buyPack", "claimPack", "startBreed", "tapEgg", "claimBreed", "surveyHabitat", "unlockHabitat",
                         "viewBiologyProfile", "observeDragon", "completeQuest", "unlockZone", "startExpedition", "claimExpedition", "buildSanctuaryProject", "advanceTime", "moveDragon" })
                Assert.That(j.OkByType.ContainsKey(type), Is.True, "lệnh chưa dùng: " + type);
            Assert.That(j.FirstAt["zone:ocean"], Is.LessThan(j.FirstAt["zone:sky"]));
            Assert.That(j.FirstAt["reward-egg:ZONE_EGG_OCEAN"], Is.EqualTo(j.FirstAt["zone:ocean"]));
            Assert.That(j.PaidOnce["reward-egg:" + SkyCloudBalance.AurorawingEggRewardId], Is.EqualTo(1), "trứng Aurorawing đúng một lần");
            Assert.That(s.GrantedRewards.Count(id => id == SkyCloudBalance.AurorawingEggRewardId), Is.EqualTo(1));
            Assert.That(j.FirstAt["reward-egg:" + SkyCloudBalance.AurorawingEggRewardId], Is.GreaterThan(j.FirstAt["zone:sky"]));
            TestContext.Progress.WriteLine($"U2.15 seed {seed}: sky zone {j.FirstAt["zone:sky"] / 24:0.0} d, Aurorawing egg {j.FirstAt["reward-egg:" + SkyCloudBalance.AurorawingEggRewardId] / 24:0.0} d, Q_EXPEDITION_CLOUD {j.FirstAt["quest:Q_EXPEDITION_CLOUD"] / 24:0.0} d");
        }

        [Test]
        public void Legacy_disabled_table_journey_refuses_the_sky_route_and_stops_at_16_of_17_quests()
        {
            // Đối chứng U2.14 (bảng tắt chỉ trong phạm vi Use).
            AlphaJourney j = AlphaJourney.Run(Policy.Guided, ThreeTimesTen, 1, JourneyDays, EconomyVariant.Alpha(SkyCloudBalance.Disabled));
            Assert.That(SkyCloudBalance.Current, Is.SameAs(SkyCloudBalance.ProposedA), "phạm vi Use đã đóng: game thật dùng bảng A");
            Assert.That(j.Violations, Is.Empty, string.Join("\n", j.Violations.Take(10)));
            GameState s = j.Sim.State;
            Assert.That(QuestProposals.All.Select(q => q.Id).Except(s.CompletedQuests.Concat(s.CompletedSideQuests)), Is.EqualTo(new[] { "Q_EXPEDITION_CLOUD" }));
            Assert.That(s.ExpeditionsClaimed.TryGetValue(SkyCloudBalance.RouteId, out int sky) ? sky : 0, Is.EqualTo(0));
            Assert.That(s.Player.Inventory.TryGetValue(SkyCloudBalance.MaterialId, out long dust) ? dust : 0, Is.EqualTo(0));
            Assert.That(j.FirstAt.ContainsKey("reward-egg:" + SkyCloudBalance.AurorawingEggRewardId), Is.False);
            DragonState flyer = s.Dragons.FirstOrDefault(d => d.Location.Kind != LocationKind.Released && d.SpeciesId == "zephyr");
            Assume.That(flyer, Is.Not.Null);
            using (SkyCloudBalance.Use(SkyCloudBalance.Disabled))
            {
                DispatchResult r = CommandDispatcher.Dispatch(s, new StartExpeditionCommand("u214-sky", SkyCloudBalance.RouteId, flyer.Uid), new FixedClock(s.Clock.LastProcessedAtMs));
                Assert.That(r.Ok, Is.False);
                Assert.That(r.Error.Code, Is.EqualTo(DomainErrorCode.RouteRewardsUndefined), "kiểm bảng thưởng trước khi kiểm slot / rồng");
            }
        }

        [Test]
        public void Quest_milestone_egg_and_achievement_rewards_are_paid_exactly_once()
        {
            AlphaJourney j = Production(2);
            Assert.That(j.PaidOnce.Where(kv => kv.Value != 1).Select(kv => kv.Key), Is.Empty);
            GameState s = j.Sim.State;
            Assert.That(j.PaidOnce.Keys.Count(k => k.StartsWith("quest:", StringComparison.Ordinal)), Is.EqualTo(17));
            Assert.That(j.PaidOnce.Keys.Count(k => k.StartsWith("expedition:", StringComparison.Ordinal)), Is.EqualTo(s.ExpeditionsClaimed.Values.Sum()));
            // Mốc: đúng các mốc của tuyến đã vượt, mỗi mốc một lần.
            foreach (var kv in s.ExpeditionsClaimed)
            {
                int expected = Exploration.Milestones.Count(m => m.RouteId == kv.Key && m.Claims <= kv.Value);
                Assert.That(j.PaidOnce.Keys.Count(k => k.StartsWith("milestone:" + kv.Key + ":", StringComparison.Ordinal)), Is.EqualTo(expected), kv.Key);
            }
            // Gửi lại mọi nhiệm vụ / mở thế giới / requestId cũ: không trả thêm.
            var clock = new FixedClock(s.Clock.LastProcessedAtMs);
            foreach (string q in s.CompletedQuests.Concat(s.CompletedSideQuests))
                Assert.That(CommandDispatcher.Dispatch(s, new CompleteQuestCommand("again-" + q, q), clock).Error?.Code, Is.EqualTo(DomainErrorCode.QuestAlreadyCompleted), q);
            Assert.That(CommandDispatcher.Dispatch(s, new UnlockZoneCommand("again-sky", "sky"), clock).Error?.Code, Is.EqualTo(DomainErrorCode.AlreadyThere));
            string lastRid = s.ProcessedRequestIds.Last();
            DispatchResult dup = CommandDispatcher.Dispatch(s, new ClaimExpeditionCommand(lastRid), clock);
            Assert.That((dup.Ok, dup.Duplicate, ReferenceEquals(dup.State, s)), Is.EqualTo((true, true, true)));
            Assert.That(s.GrantedRewards, Is.Unique);
        }

        [Test]
        public void Rejected_commands_in_the_journey_never_change_state_or_spend_gold()
        {
            AlphaJourney j = Production(3);
            Assert.That(j.RejectedByCode.Values.Sum(), Is.GreaterThan(0), "hành trình thật có lệnh bị từ chối (cooldown, khu đầy...)");
            Assert.That(j.Violations.Where(v => v.StartsWith("rejected", StringComparison.Ordinal)), Is.Empty);
        }

        [Test]
        public void Every_policy_keeps_the_gold_and_material_ledgers_closed([Values(Policy.Relaxed, Policy.Researcher, Policy.Collector, Policy.Breeder, Policy.Casual, Policy.StarterTrader, Policy.BreedTrader)] Policy policy)
        {
            AlphaJourney j = AlphaJourney.Run(policy, ThreeTimesTen, 1, 21);
            Assert.That(j.Violations, Is.Empty, string.Join("\n", j.Violations.Take(10)));
            GameState s = j.Sim.State;
            long start = EconomySimulator.NewSave(1).Player.Gold.Micro;
            Assert.That(s.Player.Gold.Micro, Is.EqualTo(start + j.ProductionMicro + j.SalesMicro + j.AchievementMicro - j.SpentMicro), "vàng chỉ từ sản xuất, bán, thành tựu");
        }

        [Test]
        public void Explicit_table_A_scope_gives_the_same_journey_as_production()
        {
            AlphaJourney j = AlphaJourney.Run(Policy.Guided, ThreeTimesTen, 1, JourneyDays, EconomyVariant.Alpha(SkyCloudBalance.ProposedA));
            Assert.That(SkyCloudBalance.Current, Is.SameAs(SkyCloudBalance.Production));
            Assert.That(j.FirstAt, Is.EquivalentTo(Production(1).FirstAt), "production = bảng A: cùng hành trình từng mốc");
            Assert.That(j.Violations, Is.Empty, string.Join("\n", j.Violations.Take(10)));
            GameState s = j.Sim.State;
            Assert.That(s.CompletedQuests.Concat(s.CompletedSideQuests).Count(), Is.EqualTo(17));
            Assert.That(j.PaidOnce["reward-egg:" + SkyCloudBalance.AurorawingEggRewardId], Is.EqualTo(1));
            Assert.That(s.ExpeditionsClaimed[SkyCloudBalance.RouteId], Is.GreaterThanOrEqualTo(SkyCloudBalance.AurorawingEggClaims));
            Assert.That(j.FirstAt["reward-egg:" + SkyCloudBalance.AurorawingEggRewardId], Is.GreaterThan(j.FirstAt["zone:sky"]));
        }
    }
}
