using System;
using System.Collections.Generic;
using System.Linq;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Progression;
using Dravio.Domain.World;
using NUnit.Framework;
using static Dravio.Domain.Tests.Support.Fixtures;

namespace Dravio.Domain.Tests
{
    /// <summary>
    /// Port của tests/unit/tutorial.test.ts (phần hướng dẫn, gói Starter, thành tựu, validation Schema v4).
    /// Phần surveyHabitat/viewBiologyProfile đã port ở ZoneCommandTests; migration v2 → v4 ở Persistence tests.
    /// </summary>
    public sealed class TutorialProgressTests
    {
        private static readonly long TT0 = new DateTimeOffset(2026, 11, 25, 9, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        private const long G = 1_000_000;
        private static readonly MoveTarget Meadow = MoveTarget.Zone("origin", "origin_meadow");

        private static (GameState state, FixedClock clock) Game(string saveId = "tut") => (InitialStateFactory.Create(saveId, TT0, Rng.Create(8)), new FixedClock(TT0));
        private static DragonState EmberOf(GameState s) => s.Dragons.First(d => d.SpeciesId == "emberling");
        private static List<string> Steps(DispatchResult r) => r.Events.OfType<TutorialStepCompleted>().Select(e => e.StepId).ToList();
        private static List<string> Unlocked(DispatchResult r) => r.Events.OfType<AchievementUnlocked>().Select(e => e.AchievementId).ToList();

        private static BuyTutorialPackCommand Buy(string rid = null) => new BuyTutorialPackCommand(rid ?? Rid());
        private static ClaimPackCommand Claim(MoveTarget t) => new ClaimPackCommand(Rid(), t);
        private static SetTutorialStatusCommand Status(TutorialStatus s) => new SetTutorialStatusCommand(Rid(), s);

        private static GameState FeedEmber(GameState s, IClock clock)
        {
            DispatchResult fed = Ok(s, Feed(), clock);
            FoodDrop food = fed.State.Food.Drops.Last();
            return Ok(fed.State, new ConsumeFoodCommand(Rid(), food.Id, EmberOf(fed.State).Uid), clock).State;
        }

        [Test]
        public void New_save_starts_tutorial_at_TUT_01_with_no_pack_and_no_achievements()
        {
            var (s, _) = Game();
            Assert.That((s.Tutorial.Status, s.Tutorial.Completed.Count, s.Tutorial.StartedAtMs, s.Tutorial.ProfilesViewed.Count, s.Tutorial.StarterPackUsed),
                Is.EqualTo((TutorialStatus.Active, 0, TT0, 0, false)));
            Assert.That(Tutorial.CurrentStep(s.Tutorial), Is.EqualTo("TUT_01"));
            Assert.That(s.PendingPack, Is.Null);
            Assert.That(s.UnlockedAchievements, Is.Empty);
        }

        [Test]
        public void Full_playthrough_completes_eight_steps_in_order_with_exact_gold()
        {
            var (s, clock) = Game();
            var log = new List<string>();
            DispatchResult Step(GameCommand c) { DispatchResult r = Ok(s, c, clock); log.AddRange(Steps(r)); s = r.State; return r; }

            clock.Advance(14_999); Step(Online());
            Assert.That(log, Is.Empty, "14.999 s chưa đủ quan sát");
            clock.Advance(1); Step(Online());
            Assert.That(log, Is.EqualTo(new[] { "TUT_01" }));

            clock.Advance(2000);
            DispatchResult leaf = Ok(s, Feed(diet: DietId.Leaf), clock);
            Assert.That(Steps(leaf), Is.Empty);
            Fail(leaf.State, new ConsumeFoodCommand(Rid(), leaf.State.Food.Drops[0].Id, EmberOf(s).Uid), clock, DomainErrorCode.WrongDiet);
            clock.Advance(2000);
            s = FeedEmber(leaf.State, clock);
            Assert.That(s.Tutorial.Completed, Is.EqualTo(new[] { "TUT_01", "TUT_02" }));

            Step(new ViewBiologyProfileCommand(Rid(), "emberling"));
            Assert.That(s.Tutorial.Completed.Last(), Is.EqualTo("TUT_03"));
            long gold = s.Player.Gold.Micro, earned0 = s.Player.TotalGoldEarned.Micro;
            Step(new SurveyHabitatCommand(Rid(), "origin_warm_rocks"));
            Assert.That(s.Tutorial.Completed.Last(), Is.EqualTo("TUT_03"), "khảo sát chưa đủ, phải mở");
            Step(new UnlockHabitatCommand(Rid(), "origin_warm_rocks"));
            Assert.That(s.Tutorial.Completed.Last(), Is.EqualTo("TUT_04"));
            Assert.That(s.Player.Gold.Micro, Is.EqualTo(gold), "mở miễn phí");

            DispatchResult bought = Step(Buy());
            long Prod() => s.Player.TotalGoldEarned.Micro - earned0;
            Assert.That(s.Player.Gold.Micro, Is.EqualTo(gold - 100 * G + Prod()));
            Assert.That((s.PendingPack.SpeciesId, s.PendingPack.Mutation, s.PendingPack.PackId, s.PendingPack.Tutorial), Is.EqualTo(("sprout", MutationType.Normal, "starter", true)));
            PackBought pb = bought.Events.OfType<PackBought>().Single();
            Assert.That((pb.PackId, pb.SpeciesId, pb.PriceGoldMicro, pb.Tutorial), Is.EqualTo(("starter", "sprout", 100 * G, true)));
            Assert.That(s.Tutorial.Completed.Last(), Is.EqualTo("TUT_04"), "mua chưa đủ, phải nhận");

            DispatchResult claimed = Step(Claim(Meadow));
            DragonState sprout = s.Dragons.Single(d => d.SpeciesId == "sprout");
            Assert.That((sprout.Mutation, sprout.XpSec, sprout.Origin, sprout.Generation, sprout.Location.HabitatId), Is.EqualTo((MutationType.Normal, 0L, DragonOrigin.Pack, 1, "origin_meadow")));
            Assert.That(s.Player.Stats.TotalHatched, Is.EqualTo(1));
            Assert.That(claimed.Events.OfType<AchievementUnlocked>().Single().RewardGoldMicro, Is.EqualTo(50 * G));
            Assert.That(Unlocked(claimed), Is.EqualTo(new[] { "ACH_FIRST_HATCH" }));
            Assert.That(s.Player.Gold.Micro, Is.EqualTo(gold - 100 * G + 50 * G + Prod()));
            Assert.That(s.Tutorial.Completed.Last(), Is.EqualTo("TUT_05"));

            Step(new SurveyHabitatCommand(Rid(), "origin_pondbank"));
            Step(new UnlockHabitatCommand(Rid(), "origin_pondbank"));
            Assert.That(s.Tutorial.Completed.Last(), Is.EqualTo("TUT_06"), "bờ hồ không cần Droplet");

            long earned = s.Player.TotalGoldEarned.Micro;
            clock.Advance(300_000);
            DispatchResult grown = Step(Online());
            Assert.That(log, Is.EqualTo(ProgressCatalog.TutorialStepIds.Where(id => id != "TUT_02")), "TUT_02 đã ghi trong FeedEmber");
            Assert.That(s.Tutorial.Completed, Is.EqualTo(ProgressCatalog.TutorialStepIds));
            Assert.That(s.Tutorial.Status, Is.EqualTo(TutorialStatus.Completed));
            Assert.That(grown.Events.OfType<TutorialStatusChanged>().Single().Status, Is.EqualTo(TutorialStatus.Completed));
            Assert.That(grown.Events.OfType<AchievementUnlocked>().Single(e => e.AchievementId == "ACH_FIRST_ADULT").RewardGoldMicro, Is.EqualTo(100 * G));
            Assert.That(s.UnlockedAchievements, Is.EqualTo(new[] { "ACH_FIRST_HATCH", "ACH_FIRST_ADULT" }));
            Assert.That(s.ClaimedAchievements, Is.EqualTo(new[] { "ACH_FIRST_HATCH", "ACH_FIRST_ADULT" }));
            Assert.That(s.Player.Gold.Micro - gold, Is.EqualTo(-100 * G + 50 * G + 100 * G + Prod()), "thưởng không vào vàng sản xuất; hướng dẫn không thêm vàng");
            Assert.That(s.Player.TotalGoldEarned.Micro, Is.GreaterThan(earned));

            clock.Advance(1000);
            DispatchResult after = Ok(s, Online(), clock);
            Assert.That(after.Events.Where(e => e is TutorialStepCompleted || e is AchievementUnlocked), Is.Empty, "hoàn tất rồi không đánh giá thêm; thành tựu không lặp");
        }

        [Test]
        public void Steps_complete_only_in_order_and_cascade_once_the_earlier_step_is_done()
        {
            var (s, clock) = Game();
            clock.Advance(1000);
            s = Ok(s, new ViewBiologyProfileCommand(Rid(), "emberling"), clock).State;
            s = Ok(s, new SurveyHabitatCommand(Rid(), "origin_warm_rocks"), clock).State;
            s = Ok(s, new UnlockHabitatCommand(Rid(), "origin_warm_rocks"), clock).State;
            Assert.That(s.Tutorial.Completed, Is.Empty);
            clock.Advance(15_000);
            s = Ok(s, Online(), clock).State;
            Assert.That(s.Tutorial.Completed, Is.EqualTo(new[] { "TUT_01" }));
            clock.Advance(1000);
            s = FeedEmber(s, clock);
            Assert.That(s.Tutorial.Completed, Is.EqualTo(new[] { "TUT_01", "TUT_02", "TUT_03", "TUT_04" }));
        }

        [Test]
        public void TUT_01_needs_a_dragon_in_the_zone_not_only_elapsed_time()
        {
            var (s, clock) = Game();
            clock.Advance(1000);
            s = Ok(s, new MoveDragonCommand(Rid(), EmberOf(s).Uid, MoveTarget.Storage), clock).State;
            clock.Advance(20_000);
            s = Ok(s, Online(), clock).State;
            Assert.That(s.Tutorial.Completed, Is.Empty);
            s = Ok(s, new MoveDragonCommand(Rid(), EmberOf(s).Uid, Meadow), clock).State;
            Assert.That(s.Tutorial.Completed, Is.EqualTo(new[] { "TUT_01" }));
        }

        [Test]
        public void Skipping_keeps_unlock_paths_and_achievements_resuming_credits_done_steps()
        {
            var (s, clock) = Game();
            s = Ok(s, Status(TutorialStatus.Skipped), clock).State;
            Fail(s, Status(TutorialStatus.Skipped), clock, DomainErrorCode.AlreadyThere);
            clock.Advance(20_000);
            s = Ok(s, Online(), clock).State;
            s = Ok(s, new ViewBiologyProfileCommand(Rid(), "emberling"), clock).State;
            foreach (string h in new[] { "origin_warm_rocks", "origin_pondbank" })
            {
                s = Ok(s, new SurveyHabitatCommand(Rid(), h), clock).State;
                s = Ok(s, new UnlockHabitatCommand(Rid(), h), clock).State;
            }
            s = Ok(s, Buy(), clock).State;
            s = Ok(s, Claim(MoveTarget.Storage), clock).State;
            Assert.That((s.Tutorial.Status, s.Tutorial.Completed.Count), Is.EqualTo((TutorialStatus.Skipped, 0)));
            Assert.That(s.FindZone("origin").UnlockedHabitatIds.OrderBy(x => x, StringComparer.Ordinal), Is.EqualTo(new[] { "origin_meadow", "origin_pondbank", "origin_warm_rocks" }));
            Assert.That(s.UnlockedAchievements, Is.EqualTo(new[] { "ACH_FIRST_HATCH" }), "thành tựu vẫn chạy khi bỏ qua");

            clock.Advance(1000);
            GameState resumed = Ok(s, Status(TutorialStatus.Active), clock).State;
            Assert.That((resumed.Tutorial.Status, resumed.Tutorial.Completed.Count, resumed.Tutorial.StartedAtMs), Is.EqualTo((TutorialStatus.Active, 0, clock.NowMs())));
            clock.Advance(15_000);
            s = Ok(resumed, Online(), clock).State;
            Assert.That(s.Tutorial.Completed, Is.EqualTo(new[] { "TUT_01" }));
            clock.Advance(1000);
            s = FeedEmber(s, clock);
            Assert.That(s.Tutorial.Completed, Is.EqualTo(new[] { "TUT_01", "TUT_02", "TUT_03", "TUT_04", "TUT_05", "TUT_06" }));
        }

        [Test]
        public void SetTutorialStatus_rejects_completed_and_skipping_a_completed_tutorial()
        {
            var (s, clock) = Game();
            Assert.That(CommandDispatcher.Dispatch(s, Status(TutorialStatus.Completed), clock).Error.Code, Is.EqualTo(DomainErrorCode.InvalidCommand));
            GameState done = s.DeepClone();
            done.Tutorial.Status = TutorialStatus.Completed;
            done.Tutorial.Completed = ProgressCatalog.TutorialStepIds.ToList();
            Fail(done, Status(TutorialStatus.Skipped), clock, DomainErrorCode.AlreadyThere);
            GameState replay = Ok(done, Status(TutorialStatus.Active), clock).State;
            Assert.That((replay.Tutorial.Status, replay.Tutorial.Completed.Count), Is.EqualTo((TutorialStatus.Active, 0)), "xem lại từ đầu");
        }

        // ---- Starter pack ----

        [Test]
        public void Starter_pack_costs_100G_once_per_save_and_is_idempotent_per_requestId()
        {
            var (s, clock) = Game();
            DispatchResult a = Ok(s, Buy("buy-1"), clock);
            DispatchResult dup = CommandDispatcher.Dispatch(a.State, Buy("buy-1"), clock);
            Assert.That((dup.Ok, dup.Duplicate, dup.State.Player.Gold.Micro), Is.EqualTo((true, true, a.State.Player.Gold.Micro)));
            Fail(a.State, Buy(), clock, DomainErrorCode.PackPending);
            GameState claimed = Ok(a.State, Claim(MoveTarget.Storage), clock).State;
            Fail(claimed, Buy(), clock, DomainErrorCode.TutorialPackUsed);
            Fail(claimed, Claim(MoveTarget.Storage), clock, DomainErrorCode.NoPendingPack);
        }

        [Test]
        public void Starter_pack_needs_exactly_100G()
        {
            var (s, clock) = Game();
            Assert.That(StarterPack.Price.Micro, Is.EqualTo(100 * G));
            GameState poor = s.DeepClone(); poor.Player.Gold = new MicroGold(100 * G - 1);
            Fail(poor, Buy(), clock, DomainErrorCode.NotEnoughGold);
            GameState exact = s.DeepClone(); exact.Player.Gold = new MicroGold(100 * G);
            Assert.That(Ok(exact, Buy(), clock).State.Player.Gold.Micro, Is.EqualTo(0));
        }

        [Test]
        public void Starter_pack_is_fixed_and_reproducible_and_leaves_pity_untouched()
        {
            var (s1, c1) = Game("same");
            var (s2, c2) = Game("same");
            GameState a = Ok(s1, Buy(), c1).State, b = Ok(s2, Buy(), c2).State;
            Assert.That(StateJsonPack(a), Is.EqualTo(StateJsonPack(b)));
            Assert.That(a.Player.PityCount, Is.EqualTo(s1.Player.PityCount));
            DragonState ha = Ok(a, Claim(MoveTarget.Storage), c1).State.Dragons.Last();
            DragonState hb = Ok(b, Claim(MoveTarget.Storage), c2).State.Dragons.Last();
            Assert.That(Support.StateJson.Dragon(ha), Is.EqualTo(Support.StateJson.Dragon(hb)));
            Assert.That((ha.SpeciesId, ha.Mutation), Is.EqualTo(("sprout", MutationType.Normal)));
            // Khác save → trứng khác uid (seed theo saveId), vẫn Sproutling Normal.
            var (s3, c3) = Game("other");
            Assert.That(Ok(s3, Buy(), c3).State.PendingPack.Id, Is.Not.EqualTo(a.PendingPack.Id));
        }

        private static string StateJsonPack(GameState s) => Support.MiniJson.Show(Support.StateJson.ToJson(s)["pendingPack"]);

        [Test]
        public void Claim_uses_the_same_biology_and_capacity_rules_as_moveDragon()
        {
            var (s0, clock) = Game();
            Assert.That(StarterPack.PackPlacementOptions(s0), Is.Empty);
            GameState s = Ok(s0, Buy(), clock).State;
            Assert.That(StarterPack.PackPlacementOptions(s).Where(o => o.Blocked == null).Select(o => o.HabitatId), Is.EqualTo(new[] { "origin_meadow" }));
            Fail(s, Claim(MoveTarget.Zone("origin", "origin_warm_rocks")), clock, DomainErrorCode.HabitatLocked);
            Fail(s, Claim(MoveTarget.Zone("origin", "origin_pondbank")), clock, DomainErrorCode.HabitatLocked);
            Fail(s, Claim(MoveTarget.Zone("ocean", "origin_meadow")), clock, DomainErrorCode.HabitatLocked);
            GameState full = s.DeepClone();
            full.FindZone("origin").Capacity = 1;
            Fail(full, Claim(Meadow), clock, DomainErrorCode.ZoneFull);
            Assert.That(CommandDispatcher.Dispatch(s, new ClaimPackCommand(Rid(), null), clock).Error.Code, Is.EqualTo(DomainErrorCode.InvalidCommand));
        }

        // ---- Achievements ----

        [Test]
        public void Starting_Emberling_is_not_a_first_hatch_but_its_adulthood_counts()
        {
            var (s, clock) = Game("fresh");
            clock.Advance(400_000);
            DispatchResult r = Ok(s, Online(), clock);
            Assert.That(r.State.Player.Stats.TotalHatched, Is.EqualTo(0));
            Assert.That(Unlocked(r), Is.EqualTo(new[] { "ACH_FIRST_ADULT" }));
            Assert.That(r.State.Player.Gold.Micro - s.Player.Gold.Micro - (r.State.Player.TotalGoldEarned.Micro - s.Player.TotalGoldEarned.Micro), Is.EqualTo(100 * G));
        }

        [Test]
        public void An_unlocked_achievement_is_never_rewarded_again_even_if_unclaimed()
        {
            var (s0, clock) = Game("dup");
            GameState s = s0.DeepClone();
            foreach (var d in s.Dragons) d.XpSec = 300;
            s.UnlockedAchievements = new List<string> { "ACH_FIRST_ADULT" };
            clock.Advance(60_000);
            DispatchResult r = Ok(s, Online(), clock);
            Assert.That(Unlocked(r), Is.Empty);
            Assert.That(r.State.ClaimedAchievements, Is.Empty, "không tự nhận thưởng cho thành tựu mở từ trước");
            Assert.That(r.State.Player.Gold.Micro - s.Player.Gold.Micro, Is.EqualTo(r.State.Player.TotalGoldEarned.Micro - s.Player.TotalGoldEarned.Micro));
        }

        [Test]
        public void Failed_commands_never_grant_rewards()
        {
            var (s, clock) = Game();
            GameState grown = s.DeepClone();
            foreach (var d in grown.Dragons) d.XpSec = 300;
            Fail(grown, Claim(MoveTarget.Storage), clock, DomainErrorCode.NoPendingPack);
            Assert.That(grown.UnlockedAchievements, Is.Empty);
        }

        [Test]
        public void Rewards_match_GDD_appendix_F_and_only_two_rules_are_implemented()
        {
            Assert.That(ProgressCatalog.AchievementIds.Select(ProgressCatalog.AchievementRewardG), Is.EqualTo(new long[] { 50, 100, 150, 100, 300, 1000, 2000, 300, 2500, 1000, 2000, 3000 }));
            Assert.That(Achievements.Implemented, Is.EqualTo(new[] { "ACH_FIRST_HATCH", "ACH_FIRST_ADULT" }));
        }

        // ---- Schema v4 validation ----

        [Test]
        public void Validation_rejects_broken_tutorial_pack_and_achievement_fields()
        {
            var (s, clock) = Game();
            bool Valid(Action<GameState> patch) { GameState c = s.DeepClone(); patch(c); return GameStateValidator.Validate(c).IsValid; }
            Assert.That(Valid(_ => { }), Is.True);
            Assert.That(Valid(c => c.Tutorial.Completed = new List<string> { "TUT_02" }), Is.False);
            Assert.That(Valid(c => c.Tutorial.Completed = new List<string> { "TUT_01", "TUT_01" }), Is.False);
            Assert.That(Valid(c => c.Tutorial.Status = TutorialStatus.Completed), Is.False);
            Assert.That(Valid(c => c.Tutorial.Status = (TutorialStatus)99), Is.False);
            Assert.That(Valid(c => c.Tutorial.ProfilesViewed = new List<string> { "nope" }), Is.False);
            Assert.That(Valid(c => c.UnlockedAchievements = new List<string> { "ACH_NOPE" }), Is.False);
            Assert.That(Valid(c => c.ClaimedAchievements = new List<string> { "ACH_FIRST_HATCH" }), Is.False);
            Assert.That(Valid(c => c.UnlockedAchievements = new List<string> { "ACH_FIRST_HATCH", "ACH_FIRST_HATCH" }), Is.False);
            PendingPack pack = Ok(s, Buy(), clock).State.PendingPack;
            Assert.That(Valid(c => c.PendingPack = pack), Is.False, "starterPackUsed false");
            Assert.That(Valid(c => { c.PendingPack = pack; c.Tutorial.StarterPackUsed = true; }), Is.True);
            Assert.That(Valid(c => { c.PendingPack = pack.Clone(); c.PendingPack.SpeciesId = "nope"; c.Tutorial.StarterPackUsed = true; }), Is.False);
        }
    }
}
