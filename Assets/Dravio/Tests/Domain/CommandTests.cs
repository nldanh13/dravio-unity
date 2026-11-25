using System;
using System.Linq;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Economy;
using Dravio.Domain.Tests.Support;
using NUnit.Framework;
using static Dravio.Domain.Tests.Support.Fixtures;

namespace Dravio.Domain.Tests
{
    /// <summary>Port các nhóm "command bus", "advanceTime", "feed + consumeFood", "pet and bond", "renameDragon" của gameState.test.ts.</summary>
    public sealed class CommandTests
    {
        private static T Single<T>(DispatchResult r) where T : DomainEvent => r.Events.OfType<T>().Single();

        // ---- command bus ----

        [Test]
        public void Rejects_malformed_commands_without_changing_state()
        {
            var (state, clock) = NewGame();
            GameCommand[] bad =
            {
                null,
                new AdvanceTimeCommand("r", (AdvanceMode)7),
                new PetCommand("", "x"),
                new PetCommand(null, "x"),
                new PetCommand(new string('r', 65), "x"),
                new FeedCommand("r", (DietId)42, "origin", "origin_meadow"),
                new FeedCommand("r", DietId.Fruit, "", "origin_meadow"),
                new RenameDragonCommand("r", "x", new string('a', 1025)),
                new ConsumeFoodCommand("r", null, "x"),
            };
            foreach (GameCommand c in bad) Fail(state, c, clock, DomainErrorCode.InvalidCommand);
        }

        [Test]
        public void Typed_error_for_unknown_dragon_state_unchanged()
        {
            var (state, clock) = NewGame();
            DispatchResult r = Fail(state, new PetCommand("r1", "ghost"), clock, DomainErrorCode.UnknownDragon);
            Assert.That(r.Error.MessageKey, Is.EqualTo("commandError.unknown-dragon"));
        }

        [Test]
        public void Duplicate_requestId_is_idempotent_no_double_gold_no_revision_bump()
        {
            var (state, clock) = NewGame();
            clock.Advance(60_000);
            DispatchResult first = CommandDispatcher.Dispatch(state, new AdvanceTimeCommand("same", AdvanceMode.Online), clock);
            Assert.That(first.Ok, Is.True);
            clock.Advance(60_000);
            DispatchResult again = CommandDispatcher.Dispatch(first.State, new AdvanceTimeCommand("same", AdvanceMode.Online), clock);
            Assert.That((again.Ok, again.Duplicate, again.Events.Count), Is.EqualTo((true, true, 0)));
            Assert.That(again.State, Is.SameAs(first.State));
            Assert.That(first.State.Revision, Is.EqualTo(1));
        }

        [Test]
        public void Rejected_command_can_be_retried_with_the_same_requestId()
        {
            var (state, clock) = NewGame();
            string uid = Ember(state).Uid;
            state = Ok(state, new PetCommand("p1", uid), clock).State;
            Fail(state, new PetCommand("p2", uid), clock, DomainErrorCode.PetCooldown);
            clock.Advance(30_000);
            Assert.That(Ok(state, new PetCommand("p2", uid), clock).Duplicate, Is.False);
        }

        [Test]
        public void Success_returns_a_new_state_and_never_mutates_the_input()
        {
            var (state, clock) = NewGame();
            string before = MiniJson.Show(StateJson.ToJson(state)["goldMicro"]);
            clock.Advance(300_000);
            DispatchResult r = Ok(state, Online(), clock);
            Assert.That(r.State, Is.Not.SameAs(state));
            Assert.That(MiniJson.Show(StateJson.ToJson(state)["goldMicro"]), Is.EqualTo(before));
            Assert.That(Ember(state).XpSec, Is.EqualTo(0));
        }

        [Test]
        public void Keeps_only_the_200_most_recent_request_ids()
        {
            var (state, clock) = NewGame();
            for (int i = 0; i < 205; i++) state = Ok(state, new AdvanceTimeCommand("t" + i, AdvanceMode.Online), clock).State;
            Assert.That(state.ProcessedRequestIds.Count, Is.EqualTo(CareRules.ProcessedRequestLimit));
            Assert.That(state.ProcessedRequestIds[0], Is.EqualTo("t5"));
            Assert.That(state.ProcessedRequestIds.Last(), Is.EqualTo("t204"));
            Assert.That(state.Revision, Is.EqualTo(205));
        }

        [Test]
        public void Post_command_evaluators_run_on_the_working_copy_after_success_only()
        {
            var (state, clock) = NewGame();
            var probe = new Probe();
            CommandDispatcher.Dispatch(state, new PetCommand("x", "ghost"), clock, new[] { probe });
            Assert.That(probe.Calls, Is.EqualTo(0));
            DispatchResult r = CommandDispatcher.Dispatch(state, new PetCommand("y", Ember(state).Uid), clock, new[] { probe });
            Assert.That(probe.Calls, Is.EqualTo(1));
            Assert.That(probe.SawEvents, Is.EqualTo(new[] { "dragonPetted", "bondChanged" }));
            Assert.That(r.State.Revision, Is.EqualTo(1));
        }

        private sealed class Probe : IPostCommandEvaluator
        {
            public int Calls;
            public string[] SawEvents;
            public void Evaluate(GameState working, System.Collections.Generic.List<DomainEvent> events) { Calls++; SawEvents = events.Select(e => e.Type).ToArray(); }
        }

        // ---- advanceTime ----

        [TestCase(0)]
        [TestCase(60)]
        [TestCase(61)]
        public void Elapsed_0_60_61_seconds(int sec)
        {
            var (state, clock) = NewGame();
            clock.Advance(sec * 1000L);
            DispatchResult r = Ok(state, Online(), clock);
            var t = (TimeAdvanced)r.Events[0];
            Assert.That(t.ElapsedMs, Is.EqualTo(sec * 1000L));
            Assert.That(Ember(r.State).XpSec, Is.EqualTo(sec));
        }

        [Test]
        public void S01_300s_online_baby_to_adult_production_split_at_35_100_percent_is_4_70625_G()
        {
            var (state, clock) = NewGame();
            clock.Advance(300_000);
            DispatchResult r = Ok(state, Online(), clock);
            long gained = r.State.Player.TotalGoldEarned.Micro - state.Player.TotalGoldEarned.Micro;
            Assert.That(gained, Is.EqualTo(4_706_250)); // 105×0.025×0.4 + 195×0.025×0.75 G
            // Thành tựu "Rồng trưởng thành đầu tiên" tự nhận +100 G, không tính vào vàng sản xuất (ADR 0013; U2.4).
            Assert.That(r.State.Player.Gold.Micro - state.Player.Gold.Micro, Is.EqualTo(gained + 100_000_000));
            Assert.That(r.Events.OfType<AchievementUnlocked>().Select(e => (e.AchievementId, e.RewardGoldMicro)), Is.EqualTo(new[] { ("ACH_FIRST_ADULT", 100_000_000L) }));
            Assert.That(EconomyRates.GrowthStageOf(Ember(r.State).XpSec, SpeciesCatalog.Get("emberling")), Is.EqualTo(GrowthStage.Adult));
            var changes = r.Events.OfType<StageChanged>().Select(e => (e.From, e.To)).ToArray();
            Assert.That(changes, Is.EqualTo(new[] { (GrowthStage.Baby, GrowthStage.Juvenile), (GrowthStage.Juvenile, GrowthStage.Adult) }));
        }

        [Test]
        public void Stage_boundary_exactly_at_105s_and_300s_in_separate_steps_without_gold_drift()
        {
            var (state, clock) = NewGame();
            GameState start = state;
            clock.Advance(104_999);
            state = Ok(state, Online(), clock).State;
            Assert.That(EconomyRates.GrowthStageOf(Ember(state).XpSec, SpeciesCatalog.Get("emberling")), Is.EqualTo(GrowthStage.Baby));
            clock.Advance(1);
            DispatchResult r = Ok(state, Online(), clock);
            Assert.That(r.Events.OfType<StageChanged>().Any(e => e.To == GrowthStage.Juvenile), Is.True);
            clock.Advance(195_000);
            r = Ok(r.State, Online(), clock);
            Assert.That(r.Events.OfType<StageChanged>().Any(e => e.To == GrowthStage.Adult), Is.True);
            Assert.That(r.State.Player.TotalGoldEarned.Micro - start.Player.TotalGoldEarned.Micro, Is.EqualTo(4_706_250));
        }

        [Test]
        public void S01_adult_offline_8h_gives_540G_at_75_percent_xp_capped_receipt_once()
        {
            var (state, clock) = NewGame();
            clock.Advance(300_000);
            state = Ok(state, Online(), clock).State;
            long before = state.Player.Gold.Micro;
            clock.Advance(8 * 3_600_000L);
            DispatchResult r = Ok(state, Offline(), clock);
            Assert.That(r.State.Player.Gold.Micro - before, Is.EqualTo(540_000_000));
            Assert.That(Ember(r.State).XpSec, Is.EqualTo(300));
            Assert.That((r.State.OfflineReceipt.GoldMicro, r.State.OfflineReceipt.BonusClaimed), Is.EqualTo((540_000_000L, false)));
            Assert.That(r.Events.OfType<OfflineReceiptCreated>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void No_receipt_for_offline_up_to_60s()
        {
            var (state, clock) = NewGame();
            clock.Advance(60_000);
            Assert.That(Ok(state, Offline(), clock).State.OfflineReceipt, Is.Null);
        }

        [Test]
        public void Caps_offline_time_at_24h()
        {
            var (state, clock) = NewGame();
            clock.Advance(3 * TimeRules.OfflineCapMs);
            Assert.That(((TimeAdvanced)Ok(state, Offline(), clock).Events[0]).ElapsedMs, Is.EqualTo(TimeRules.OfflineCapMs));
        }

        [Test]
        public void Clock_going_backwards_grants_nothing_and_keeps_high_water_mark()
        {
            var (state, clock) = NewGame();
            clock.Advance(10_000);
            state = Ok(state, Online(), clock).State;
            MicroGold gold = state.Player.Gold;
            clock.Set(T0 - 3_600_000);
            DispatchResult r = Ok(state, Online(), clock);
            Assert.That(r.State.Player.Gold, Is.EqualTo(gold));
            Assert.That(r.State.Clock.HighWaterAtMs, Is.EqualTo(T0 + 10_000));
        }

        [Test]
        public void Dragons_outside_the_production_budget_still_grow_but_earn_nothing()
        {
            var (state, clock) = NewGame();
            state.ProductionDragonIds.Clear();
            clock.Advance(60_000);
            DispatchResult r = Ok(state, Online(), clock);
            Assert.That(r.State.Player.Gold, Is.EqualTo(state.Player.Gold));
            Assert.That(Ember(r.State).XpSec, Is.EqualTo(60));
        }

        [Test]
        public void Released_dragons_do_not_grow()
        {
            var (state, clock) = NewGame();
            state.ProductionDragonIds.Clear();
            Ember(state).Location = DragonLocation.Released;
            clock.Advance(60_000);
            Assert.That(Ember(Ok(state, Online(), clock).State).XpSec, Is.EqualTo(0));
        }

        [Test]
        public void Busy_dragons_pause_production()
        {
            var (state, clock) = NewGame();
            Ember(state).Activity = DragonActivity.Care;
            clock.Advance(60_000);
            DispatchResult r = Ok(state, Online(), clock);
            Assert.That(r.State.Player.Gold, Is.EqualTo(state.Player.Gold));
            Assert.That(Ember(r.State).XpSec, Is.EqualTo(60));
        }

        // ---- feed + consumeFood ----

        [Test]
        public void Feed_spawns_food_with_12s_TTL_and_no_gold()
        {
            var (state, clock) = NewGame();
            DispatchResult r = Ok(state, Feed(), clock);
            Assert.That(r.State.Food.Drops.Count, Is.EqualTo(1));
            Assert.That(r.State.Food.Drops[0].ExpiresAtMs, Is.EqualTo(T0 + 12_000));
            Assert.That(r.State.Player.Gold, Is.EqualTo(state.Player.Gold));
            Assert.That(r.State.Player.Stats.TotalFeedClicks, Is.EqualTo(1));
            Assert.That(Single<FoodSpawned>(r).FoodId, Is.EqualTo(r.State.Food.Drops[0].Id));
        }

        [Test]
        public void Enforces_0_8s_between_drops_and_the_5_plus_level_concurrent_limit()
        {
            var (state, clock) = NewGame();
            state = Ok(state, Feed(), clock).State;
            clock.Advance(799);
            Fail(state, Feed(), clock, DomainErrorCode.FoodTooFast);
            clock.Advance(1);
            state = Ok(state, Feed(), clock).State;
            for (int i = 0; i < 3; i++) { clock.Advance(800); state = Ok(state, Feed(), clock).State; }
            clock.Advance(800);
            Fail(state, Feed(), clock, DomainErrorCode.FoodLimit);
            state.Player.Upgrades.FoodCooldownLevel = 1;
            Assert.That(Ok(state, Feed(), clock).State.Food.Drops.Count, Is.EqualTo(6));
        }

        [Test]
        public void Rejects_locked_zones_and_habitats()
        {
            var (state, clock) = NewGame();
            Fail(state, Feed("ocean_reef", DietId.Fruit, "ocean"), clock, DomainErrorCode.ZoneLocked);
            Fail(state, Feed("origin_pondbank"), clock, DomainErrorCode.HabitatLocked);
            Fail(state, Feed("ocean_reef"), clock, DomainErrorCode.HabitatLocked);
            Fail(state, Feed("origin_meadow", DietId.Fruit, "atlantis"), clock, DomainErrorCode.ZoneLocked);
        }

        [Test]
        public void Food_expires_after_12s()
        {
            var (state, clock) = NewGame();
            DispatchResult r = Ok(state, Feed(), clock);
            string id = r.State.Food.Drops[0].Id;
            clock.Advance(12_000);
            DispatchResult r2 = Ok(r.State, Online(), clock);
            Assert.That(r2.State.Food.Drops, Is.Empty);
            Assert.That(Single<FoodExpired>(r2).FoodId, Is.EqualTo(id));
        }

        [Test]
        public void S01_first_meal_gives_8s_XP_and_1_bond_no_gold_XP_at_most_once_per_8s()
        {
            var (state, clock) = NewGame();
            state = Ok(state, Feed(), clock).State;
            string food1 = state.Food.Drops[0].Id;
            string uid = Ember(state).Uid;
            DispatchResult r = Ok(state, new ConsumeFoodCommand(Rid(), food1, uid), clock);
            Assert.That(Ember(r.State).XpSec, Is.EqualTo(8));
            Assert.That(Ember(r.State).Bond, Is.EqualTo(1));
            Assert.That(r.State.Player.Gold, Is.EqualTo(state.Player.Gold));
            Assert.That(r.State.Food.Drops, Is.Empty);
            Assert.That(r.Events.Select(e => e.Type), Is.EqualTo(new[] { "timeAdvanced", "foodConsumed", "bondChanged" }));
            state = r.State;

            clock.Advance(800);
            state = Ok(state, Feed(), clock).State;
            string food2 = state.Food.Drops[0].Id;
            clock.Advance(1_000); // 1,8 s sau bữa đầu
            Fail(state, new ConsumeFoodCommand(Rid(), food2, uid), clock, DomainErrorCode.FeedCooldown);
            clock.Advance(6_200); // đủ 8 s
            r = Ok(state, new ConsumeFoodCommand(Rid(), food2, uid), clock);
            // 8 s XP thời gian trôi qua + 8 s mỗi bữa × 2
            Assert.That(Ember(r.State).XpSec, Is.EqualTo(8 + 8 + 8));
        }

        [Test]
        public void Rejects_wrong_diet_expired_food_unknown_food_wrong_place_and_busy_dragon()
        {
            var (state, clock) = NewGame();
            state = Ok(state, Feed(diet: DietId.Aquatic), clock).State;
            string uid = Ember(state).Uid;
            string fid = state.Food.Drops[0].Id;
            Fail(state, new ConsumeFoodCommand(Rid(), fid, uid), clock, DomainErrorCode.WrongDiet);
            Fail(state, new ConsumeFoodCommand(Rid(), "nope", uid), clock, DomainErrorCode.UnknownFood);
            clock.Advance(800);
            state = Ok(state, Feed(), clock).State;
            string fruit = state.Food.Drops.First(f => f.DietId == DietId.Fruit).Id;
            GameState stored = state.DeepClone();
            stored.Dragons[0].Location = DragonLocation.Storage;
            stored.ProductionDragonIds.Clear();
            Fail(stored, new ConsumeFoodCommand(Rid(), fruit, uid), clock, DomainErrorCode.DragonUnavailable);
            GameState busy = state.DeepClone();
            busy.Dragons[0].Activity = DragonActivity.Expedition;
            Fail(busy, new ConsumeFoodCommand(Rid(), fruit, uid), clock, DomainErrorCode.DragonUnavailable);
            clock.Advance(12_000);
            Fail(state, new ConsumeFoodCommand(Rid(), fruit, uid), clock, DomainErrorCode.FoodExpired);
        }

        [Test]
        public void Feeding_XP_can_cross_a_stage_boundary_and_emits_the_change()
        {
            var (state, clock) = NewGame();
            clock.Advance(100_000);
            state = Ok(state, Online(), clock).State;
            state = Ok(state, Feed(), clock).State;
            DispatchResult r = Ok(state, new ConsumeFoodCommand(Rid(), state.Food.Drops[0].Id, Ember(state).Uid), clock);
            StageChanged sc = Single<StageChanged>(r);
            Assert.That((sc.DragonUid, sc.From, sc.To), Is.EqualTo((Ember(state).Uid, GrowthStage.Baby, GrowthStage.Juvenile)));
        }

        [Test]
        public void Food_quality_level_raises_feed_XP_8_plus_6_per_level()
        {
            var (state, clock) = NewGame();
            state.Player.Upgrades.FoodQualityLevel = 2;
            state = Ok(state, Feed(), clock).State;
            DispatchResult r = Ok(state, new ConsumeFoodCommand(Rid(), state.Food.Drops[0].Id, Ember(state).Uid), clock);
            Assert.That(Single<FoodConsumed>(r).XpGainedSec, Is.EqualTo(20));
            Assert.That(Ember(r.State).XpSec, Is.EqualTo(20));
        }

        // ---- pet and bond ----

        [Test]
        public void Pet_gives_1_bond_with_30s_cooldown()
        {
            var (state, clock) = NewGame();
            string uid = Ember(state).Uid;
            state = Ok(state, new PetCommand(Rid(), uid), clock).State;
            Assert.That(Ember(state).Bond, Is.EqualTo(1));
            clock.Advance(29_999);
            Fail(state, new PetCommand(Rid(), uid), clock, DomainErrorCode.PetCooldown);
            clock.Advance(1);
            Assert.That(Ember(Ok(state, new PetCommand(Rid(), uid), clock).State).Bond, Is.EqualTo(2));
        }

        [Test]
        public void Bond_gains_cap_at_10_per_UTC_day_reset_next_day_never_exceed_100()
        {
            var (state, clock) = NewGame();
            string uid = Ember(state).Uid;
            for (int i = 0; i < 12; i++) { state = Ok(state, new PetCommand(Rid(), uid), clock).State; clock.Advance(30_000); }
            Assert.That(Ember(state).Bond, Is.EqualTo(10));
            clock.Set(new DateTimeOffset(2026, 11, 25, 0, 0, 1, TimeSpan.Zero).ToUnixTimeMilliseconds());
            state = Ok(state, new PetCommand(Rid(), uid), clock).State;
            Assert.That(Ember(state).Bond, Is.EqualTo(11));
            Ember(state).Bond = 100; Ember(state).BondDaily = 0;
            clock.Advance(30_000);
            DispatchResult r = Ok(state, new PetCommand(Rid(), uid), clock);
            Assert.That(Ember(r.State).Bond, Is.EqualTo(100));
            // Đã chạm trần: vuốt ve vẫn hợp lệ (có event) nhưng không có bondChanged.
            Assert.That(r.Events.Select(e => e.Type), Is.EqualTo(new[] { "dragonPetted" }));
        }

        [Test]
        public void Clock_rollback_cannot_bypass_the_pet_cooldown()
        {
            var (state, clock) = NewGame();
            string uid = Ember(state).Uid;
            clock.Advance(60_000);
            state = Ok(state, Online(), clock).State;
            state = Ok(state, new PetCommand(Rid(), uid), clock).State;
            clock.Set(T0);
            Fail(state, new PetCommand(Rid(), uid), clock, DomainErrorCode.PetCooldown);
        }

        [Test]
        public void Released_dragon_cannot_be_petted_but_stored_dragon_can()
        {
            var (state, clock) = NewGame();
            state.Dragons.Add(Adult("rel", "sprout", d => d.Location = DragonLocation.Released));
            state.Dragons.Add(Adult("sto", "sprout"));
            Fail(state, new PetCommand(Rid(), "rel"), clock, DomainErrorCode.DragonUnavailable);
            Assert.That(Ok(state, new PetCommand(Rid(), "sto"), clock).State.FindDragon("sto").Bond, Is.EqualTo(1));
        }

        // ---- renameDragon ----

        private static DispatchResult Rename(GameState s, string nickname, IClock clock) =>
            CommandDispatcher.Dispatch(s, new RenameDragonCommand(Rid(), Ember(s).Uid, nickname), clock);

        [Test]
        public void Trims_and_stores_the_name_verbatim_without_HTML_interpretation()
        {
            var (state, clock) = NewGame();
            DispatchResult r = Rename(state, "  Lửa Nhỏ <b>  ", clock);
            Assert.That(r.Ok, Is.True);
            Assert.That(Ember(r.State).Nickname, Is.EqualTo("Lửa Nhỏ <b>"));
            Assert.That(Single<DragonRenamed>(r).Nickname, Is.EqualTo("Lửa Nhỏ <b>"));
        }

        [Test]
        public void Counts_graphemes_24_ok_25_too_long_emoji_and_combining_marks_count_as_one()
        {
            var (state, clock) = NewGame();
            Assert.That(Rename(state, new string('a', 24), clock).Ok, Is.True);
            Assert.That(Rename(state, new string('a', 25), clock).Error.Code, Is.EqualTo(DomainErrorCode.NameTooLong));
            string family = "\U0001F468\u200D\U0001F469\u200D\U0001F467\u200D\U0001F466";
            Assert.That(Rename(state, string.Concat(Enumerable.Repeat(family, 24)), clock).Ok, Is.True);
            Assert.That(Rename(state, string.Concat(Enumerable.Repeat(family, 25)), clock).Error.Code, Is.EqualTo(DomainErrorCode.NameTooLong));
            Assert.That(Rename(state, string.Concat(Enumerable.Repeat("E\u0302", 24)), clock).Ok, Is.True);
        }

        [Test]
        public void Rejects_empty_names_and_control_or_bidi_characters()
        {
            var (state, clock) = NewGame();
            Assert.That(Rename(state, "   ", clock).Error.Code, Is.EqualTo(DomainErrorCode.NameEmpty));
            Assert.That(Rename(state, "a\u0000b", clock).Error.Code, Is.EqualTo(DomainErrorCode.NameInvalidChars));
            Assert.That(Rename(state, "abc\u202Edef", clock).Error.Code, Is.EqualTo(DomainErrorCode.NameInvalidChars));
            Assert.That(Rename(state, "line\nbreak", clock).Error.Code, Is.EqualTo(DomainErrorCode.NameInvalidChars));
            Assert.That(Rename(state, "\u0085x", clock).Error.Code, Is.EqualTo(DomainErrorCode.NameInvalidChars));
        }

        [Test]
        public void Null_clears_the_nickname()
        {
            var (state, clock) = NewGame();
            DispatchResult named = Rename(state, "Ember", clock);
            Assert.That(named.Ok, Is.True);
            DispatchResult cleared = Rename(named.State, null, clock);
            Assert.That(cleared.Ok, Is.True);
            Assert.That(Ember(cleared.State).Nickname, Is.Null);
            Assert.That(Single<DragonRenamed>(cleared).Nickname, Is.Null);
            Assert.That(GameStateValidator.Validate(cleared.State).IsValid, Is.True);
        }
    }
}
