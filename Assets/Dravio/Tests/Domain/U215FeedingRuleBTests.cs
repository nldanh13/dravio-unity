using System;
using System.Collections.Generic;
using System.Linq;
using Dravio.Domain.Behavior;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.World;
using NUnit.Framework;
using static Dravio.Domain.Tests.Support.Fixtures;

namespace Dravio.Domain.Tests
{
    /// <summary>
    /// U2.15a (issue #74) — luật cho ăn B đã duyệt: <see cref="FeedCommand.TargetDragonUid"/> tùy chọn; rồng được chọn ăn trước nếu ăn được, không thì luật A.
    /// Không thêm phần ăn, XP, tiêu hao hay dữ liệu lưu; lệnh cũ (không mục tiêu) cho kết quả trùng luật A.
    /// </summary>
    public sealed class U215FeedingRuleBTests
    {
        private static readonly long Day0 = new DateTimeOffset(2026, 12, 1, 12, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        private const string Zone = "origin", Hab = "origin_meadow";

        /// <summary>Đồng cỏ Khởi Nguyên với các rồng Adult theo thứ tự UID cho trước.</summary>
        internal static GameState Meadow(params (string uid, string species)[] dragons)
        {
            (GameState s, _) = NewGame();
            s = s.DeepClone();
            s.Dragons.Clear();
            s.ProductionDragonIds.Clear();
            Personality[] ps = { Personality.Calm, Personality.Playful, Personality.Curious, Personality.Shy };
            for (int i = 0; i < dragons.Length; i++)
            {
                int k = i;
                s.Dragons.Add(Adult(dragons[i].uid, dragons[i].species, d => { d.Location = DragonLocation.Zone(Zone, Hab); d.Personality = ps[k % ps.Length]; }));
            }
            foreach (ZoneState z in s.Zones) if (z.Id == Zone) z.UnlockedHabitatIds = Zones.HabitatsOfZone(Zone);
            foreach (string disc in ProgressCatalog.DiscoveryIds) if (!s.Discoveries.Contains(disc)) s.Discoveries.Add(disc);
            s.Discoveries.Sort(StringComparer.Ordinal);
            s.Tutorial.Status = TutorialStatus.Completed;
            s.Tutorial.Completed = new List<string>(ProgressCatalog.TutorialStepIds);
            s.Clock = new ClockState(Day0, Day0);
            return s;
        }

        private static GameState FiveSprouts() => Meadow(("d0", "sprout"), ("d1", "sprout"), ("d2", "sprout"), ("d3", "sprout"), ("d4", "sprout"));

        private static DietId DietOf(GameState s, string uid) => SpeciesCatalog.GetBiology(s.FindDragon(uid).SpeciesId).DietIds[0];

        private static FeedCommand FeedFor(GameState s, string target, DietId? diet = null) =>
            new FeedCommand(Rid(), diet ?? DietOf(s, target ?? s.Dragons[0].Uid), Zone, Hab, target);

        /// <summary>Luật A nguyên bản (U2.14), chép lại để so: rồng thích loại đó trước, rồi UID ordinal; drop hết hạn sớm trước.</summary>
        private static Dictionary<string, string> LegacyA(GameState s, long now)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            var drops = s.Food.Drops.Where(f => f.ZoneId == Zone && f.HabitatId == Hab && f.ExpiresAtMs > now)
                .OrderBy(f => f.ExpiresAtMs).ThenBy(f => f.Id, StringComparer.Ordinal).ToList();
            var here = s.Dragons.Where(d => d.Location.Kind == LocationKind.Zone && d.Location.ZoneId == Zone && d.Location.HabitatId == Hab)
                .OrderBy(d => d.Uid, StringComparer.Ordinal).ToList();
            foreach (FoodDrop f in drops)
            {
                DragonState best = null; bool bestFav = false;
                foreach (DragonState d in here)
                {
                    if (result.ContainsKey(d.Uid) || !BehaviorEngine.CanEat(d, f, now)) continue;
                    bool fav = PersonalityProfiles.Of(d).FavoriteDiet == f.DietId;
                    if (best == null || (fav && !bestFav)) { best = d; bestFav = fav; }
                }
                if (best != null) result[best.Uid] = f.Id;
            }
            return result;
        }

        // ================================================================== hợp đồng lệnh

        [Test]
        public void Old_four_argument_constructor_means_no_target_and_is_rule_A()
        {
            var old = new FeedCommand("r", DietId.Fruit, Zone, Hab);
            Assert.That(old.TargetDragonUid, Is.Null);
            Assert.That(old.Type, Is.EqualTo("feed"));
            GameState s = FiveSprouts();
            DispatchResult r = Ok(s, new FeedCommand(Rid(), DietOf(s, "d0"), Zone, Hab), new FixedClock(Day0));
            FoodDrop drop = r.State.Food.Drops.Single();
            Assert.That(drop.TargetDragonUid, Is.Null);
            Assert.That(r.Events.OfType<FoodSpawned>().Single().TargetDragonUid, Is.Null);
            Assert.That(BehaviorEngine.AssignFood(r.State, Zone, Hab, Day0 + 1), Is.EqualTo(LegacyA(r.State, Day0 + 1)));
            FoodAssignment a = BehaviorEngine.AssignmentOf(r.State, drop.Id, Day0 + 1);
            Assert.That((a.Source, a.TargetDragonUid, a.TargetBlockedReason), Is.EqualTo((FoodAssignmentSource.Fallback, (string)null, (string)null)));
        }

        [Test]
        public void Without_targets_assignment_matches_legacy_rule_A_on_many_random_meadows()
        {
            string[] species = SpeciesCatalog.All.Where(sp => sp.Biology.HabitatRules.Contains(Hab)).Select(sp => sp.Id).ToArray();
            Assume.That(species.Length, Is.GreaterThan(2));
            var rng = new Random(2015);
            int compared = 0;
            for (int round = 0; round < 200; round++)
            {
                int n = rng.Next(1, 7);
                GameState s = Meadow(Enumerable.Range(0, n).Select(i => ("u" + rng.Next(0, 99).ToString("00") + "-" + i, species[rng.Next(species.Length)])).ToArray());
                long t = Day0;
                for (int m = 0; m < rng.Next(1, 5); m++, t += 1_000)
                {
                    DietId diet = CareRules.DietIds[rng.Next(CareRules.DietIds.Count)];
                    DispatchResult f = CommandDispatcher.Dispatch(s, new FeedCommand(Rid(), diet, Zone, Hab), new FixedClock(t));
                    if (f.Ok) s = f.State;
                }
                if (rng.Next(2) == 0 && s.Dragons.Count > 0) s.Dragons[0].LastFeedXpAtMs = t - 3_000;
                Assert.That(BehaviorEngine.AssignFood(s, Zone, Hab, t), Is.EqualTo(LegacyA(s, t)), "round " + round);
                compared++;
            }
            Assert.That(compared, Is.EqualTo(200));
        }

        [Test]
        public void Malformed_target_is_rejected_but_unknown_target_falls_back_to_rule_A()
        {
            GameState s = FiveSprouts();
            var clock = new FixedClock(Day0);
            Fail(s, new FeedCommand(Rid(), DietOf(s, "d0"), Zone, Hab, ""), clock, DomainErrorCode.InvalidCommand);
            Fail(s, new FeedCommand(Rid(), DietOf(s, "d0"), Zone, Hab, new string('x', 65)), clock, DomainErrorCode.InvalidCommand);
            DispatchResult r = Ok(s, new FeedCommand(Rid(), DietOf(s, "d0"), Zone, Hab, "no-such-dragon"), clock);
            FoodDrop drop = r.State.Food.Drops.Single();
            Assert.That(drop.TargetDragonUid, Is.EqualTo("no-such-dragon"), "lệnh không bị từ chối: mục tiêu chỉ là ưu tiên");
            FoodAssignment a = BehaviorEngine.AssignmentOf(r.State, drop.Id, Day0 + 1);
            Assert.That((a.AssignedDragonUid, a.Source, a.TargetBlockedReason), Is.EqualTo(("d0", FoodAssignmentSource.Fallback, "unknown-dragon")));
        }

        // ================================================================== luật B

        [Test]
        public void Selected_dragon_with_the_largest_uid_of_the_same_species_eats_first()
        {
            GameState s = FiveSprouts();
            foreach (string target in new[] { "d4", "d2", "d0" })
            {
                GameState w = s.DeepClone();
                DispatchResult f = Ok(w, FeedFor(w, target), new FixedClock(Day0));
                string foodId = f.State.Food.Drops.Single().Id;
                Assert.That(LegacyA(f.State, Day0 + 1)["d0"], Is.EqualTo(foodId), "luật A luôn chọn UID nhỏ nhất");
                Assert.That(BehaviorEngine.AssignFood(f.State, Zone, Hab, Day0 + 1), Is.EqualTo(new Dictionary<string, string> { [target] = foodId }));
                BehaviorDecision d = BehaviorEngine.Decide(f.State, target, Day0 + 1, DayPhase.Day);
                Assert.That((d.Behavior, d.TargetFoodId), Is.EqualTo(((DragonBehavior?)DragonBehavior.SeekFood, foodId)), "mục tiêu thật sự 'tìm ăn'");
                Assert.That(BehaviorEngine.AssignmentOf(f.State, foodId, Day0 + 1).Source, Is.EqualTo(FoodAssignmentSource.Target));
            }
        }

        [Test]
        public void Target_beats_another_dragon_whose_favourite_is_that_food()
        {
            GameState s = Meadow(("a", "emberling"), ("b", "sprout"), ("c", "zephyr"), ("d", "sparkwing"), ("e", "blossom"));
            // Tìm mục tiêu + loại thức ăn mà luật A giao cho rồng khác (vì rồng khác thích loại đó hoặc UID nhỏ hơn).
            int checkedCases = 0;
            foreach (DragonState t in s.Dragons)
                foreach (DietId diet in SpeciesCatalog.GetBiology(t.SpeciesId).DietIds)
                {
                    GameState w = s.DeepClone();
                    DispatchResult f = Ok(w, FeedFor(w, t.Uid, diet), new FixedClock(Day0));
                    string foodId = f.State.Food.Drops.Single().Id;
                    string a = LegacyA(f.State, Day0 + 1).FirstOrDefault(kv => kv.Value == foodId).Key;
                    Assert.That(BehaviorEngine.AssignmentOf(f.State, foodId, Day0 + 1).AssignedDragonUid, Is.EqualTo(t.Uid), t.Uid + " " + diet);
                    if (a != t.Uid) checkedCases++;
                }
            Assert.That(checkedCases, Is.GreaterThan(0), "có trường hợp luật A giao cho rồng khác");
        }

        [Test]
        public void Busy_target_on_expedition_falls_back_to_rule_A_without_wasting_food()
        {
            GameState s = FiveSprouts();
            s = Ok(s, new StartExpeditionCommand(Rid(), "origin_forest", "d3"), new FixedClock(Day0)).State;
            DispatchResult f = Ok(s, FeedFor(s, "d3"), new FixedClock(Day0 + 1_000));
            string foodId = f.State.Food.Drops.Single().Id;
            FoodAssignment a = BehaviorEngine.AssignmentOf(f.State, foodId, Day0 + 1_500);
            Assert.That((a.AssignedDragonUid, a.Source, a.TargetBlockedReason), Is.EqualTo(("d0", FoodAssignmentSource.Fallback, "busy")));
            Assert.That(BehaviorEngine.AssignFood(f.State, Zone, Hab, Day0 + 1_500), Is.EqualTo(LegacyA(f.State, Day0 + 1_500)));
        }

        [Test]
        public void Target_in_storage_or_another_habitat_falls_back_as_wrong_place()
        {
            GameState s = FiveSprouts();
            s.FindDragon("d2").Location = DragonLocation.Storage;
            s.FindDragon("d3").Location = DragonLocation.Zone(Zone, "origin_pondbank");
            foreach (string t in new[] { "d2", "d3" })
            {
                DispatchResult f = Ok(s, FeedFor(s, t), new FixedClock(Day0));
                FoodAssignment a = BehaviorEngine.AssignmentOf(f.State, f.State.Food.Drops.Single().Id, Day0 + 1);
                Assert.That((a.AssignedDragonUid, a.TargetBlockedReason), Is.EqualTo(("d0", "wrong-place")), t);
            }
        }

        [Test]
        public void Target_on_cooldown_falls_back_then_takes_the_food_once_the_cooldown_ends_before_expiry()
        {
            GameState s = FiveSprouts();
            s.FindDragon("d4").LastFeedXpAtMs = Day0 - 2_000; // hồi 8 s → hết lúc Day0 + 6 s; phần ăn sống tới Day0 + 12 s
            DispatchResult f = Ok(s, FeedFor(s, "d4"), new FixedClock(Day0));
            string foodId = f.State.Food.Drops.Single().Id;
            FoodAssignment early = BehaviorEngine.AssignmentOf(f.State, foodId, Day0 + 1_000);
            Assert.That((early.AssignedDragonUid, early.TargetBlockedReason), Is.EqualTo(("d0", "cooldown")));
            Fail(f.State, new ConsumeFoodCommand(Rid(), foodId, "d4"), new FixedClock(Day0 + 1_000), DomainErrorCode.FeedCooldown);
            FoodAssignment later = BehaviorEngine.AssignmentOf(f.State, foodId, Day0 + 6_000);
            Assert.That((later.AssignedDragonUid, later.Source), Is.EqualTo(("d4", FoodAssignmentSource.Target)), "luật B đánh giá lại mỗi lần hỏi");
        }

        [Test]
        public void Target_that_cannot_eat_this_diet_falls_back_as_wrong_diet()
        {
            GameState s = Meadow(("a", "emberling"), ("b", "sprout"));
            DietId notForB = CareRules.DietIds.First(d => !SpeciesCatalog.GetBiology("sprout").DietIds.Contains(d) && SpeciesCatalog.GetBiology("emberling").DietIds.Contains(d));
            DispatchResult f = Ok(s, FeedFor(s, "b", notForB), new FixedClock(Day0));
            FoodAssignment a = BehaviorEngine.AssignmentOf(f.State, f.State.Food.Drops.Single().Id, Day0 + 1);
            Assert.That((a.AssignedDragonUid, a.TargetBlockedReason), Is.EqualTo(("a", "wrong-diet")));
        }

        [Test]
        public void Expired_food_has_no_recipient_and_is_cleaned_by_the_next_command()
        {
            GameState s = FiveSprouts();
            DispatchResult f = Ok(s, FeedFor(s, "d4"), new FixedClock(Day0));
            FoodDrop drop = f.State.Food.Drops.Single();
            FoodAssignment a = BehaviorEngine.AssignmentOf(f.State, drop.Id, drop.ExpiresAtMs);
            Assert.That((a.AssignedDragonUid, a.Source, a.TargetBlockedReason), Is.EqualTo(((string)null, FoodAssignmentSource.None, "food-expired")));
            Fail(f.State, new ConsumeFoodCommand(Rid(), drop.Id, "d4"), new FixedClock(drop.ExpiresAtMs), DomainErrorCode.FoodExpired);
            DispatchResult next = Ok(f.State, Online(), new FixedClock(drop.ExpiresAtMs + 1));
            Assert.That(next.Events.OfType<FoodExpired>().Single().FoodId, Is.EqualTo(drop.Id));
            Assert.That(BehaviorEngine.AssignmentOf(next.State, drop.Id, drop.ExpiresAtMs + 1), Is.Null);
        }

        [Test]
        public void Offline_return_expires_targeted_food_like_any_other_and_pays_nothing_extra()
        {
            GameState s = FiveSprouts();
            DispatchResult f = Ok(s, FeedFor(s, "d4"), new FixedClock(Day0));
            DispatchResult back = Ok(f.State, Offline(), new FixedClock(Day0 + 3_600_000));
            Assert.That(back.State.Food.Drops, Is.Empty);
            Assert.That(back.Events.OfType<FoodExpired>().Count(), Is.EqualTo(1));
            Assert.That(back.Events.OfType<FoodConsumed>(), Is.Empty, "không tự ăn khi offline");
            Assert.That(back.State.FindDragon("d4").LastFeedXpAtMs, Is.Null);
        }

        [Test]
        public void Two_drops_for_the_same_target_give_it_the_earlier_one_and_the_other_goes_to_rule_A()
        {
            GameState s = FiveSprouts();
            DispatchResult f1 = Ok(s, FeedFor(s, "d4"), new FixedClock(Day0));
            DispatchResult f2 = Ok(f1.State, FeedFor(f1.State, "d4"), new FixedClock(Day0 + 1_000));
            string first = f1.State.Food.Drops.Single().Id, second = f2.State.Food.Drops.Last().Id;
            var map = BehaviorEngine.AssignFood(f2.State, Zone, Hab, Day0 + 1_500);
            Assert.That(map, Is.EqualTo(new Dictionary<string, string> { ["d4"] = first, ["d0"] = second }));
            FoodAssignment a2 = BehaviorEngine.AssignmentOf(f2.State, second, Day0 + 1_500);
            Assert.That((a2.AssignedDragonUid, a2.TargetBlockedReason), Is.EqualTo(("d0", "target-has-other-food")));
        }

        [Test]
        public void Targeted_drop_wins_its_target_even_when_an_earlier_untargeted_drop_would_have_taken_it_under_rule_A()
        {
            GameState s = Meadow(("d0", "sprout"));
            DispatchResult f1 = Ok(s, new FeedCommand(Rid(), DietOf(s, "d0"), Zone, Hab), new FixedClock(Day0));
            DispatchResult f2 = Ok(f1.State, FeedFor(f1.State, "d0"), new FixedClock(Day0 + 1_000));
            string untargeted = f1.State.Food.Drops.Single().Id, targeted = f2.State.Food.Drops.Last().Id;
            Assert.That(LegacyA(f2.State, Day0 + 1_500)["d0"], Is.EqualTo(untargeted));
            Assert.That(BehaviorEngine.AssignFood(f2.State, Zone, Hab, Day0 + 1_500)["d0"], Is.EqualTo(targeted));
            Assert.That(BehaviorEngine.AssignmentOf(f2.State, untargeted, Day0 + 1_500).Source, Is.EqualTo(FoodAssignmentSource.None));
        }

        [Test]
        public void Duplicate_feed_request_does_not_drop_a_second_portion()
        {
            GameState s = FiveSprouts();
            FeedCommand cmd = FeedFor(s, "d4");
            DispatchResult a = Ok(s, cmd, new FixedClock(Day0));
            DispatchResult again = CommandDispatcher.Dispatch(a.State, cmd, new FixedClock(Day0 + 1_000));
            Assert.That((again.Ok, again.Duplicate, ReferenceEquals(again.State, a.State)), Is.EqualTo((true, true, true)));
            Assert.That(again.State.Food.Drops.Count, Is.EqualTo(1));
            Assert.That(again.State.Player.Stats.TotalFeedClicks, Is.EqualTo(s.Player.Stats.TotalFeedClicks + 1));
        }

        [Test]
        public void Longest_valid_feed_request_produces_edible_portions()
        {
            var state=FiveSprouts();
            var feed=new FeedCommand(new string('r',64),DietOf(state,"d0"),Zone,Hab,"d0",new[]{"d1"});
            var spawned=Ok(state,feed,new FixedClock(Day0));
            state=spawned.State;
            foreach(var f in spawned.Events.OfType<FoodSpawned>())
                state=Ok(state,new ConsumeFoodCommand(Rid(),f.FoodId,f.TargetDragonUid),new FixedClock(Day0+1000)).State;
            Assert.That(state.Food.Drops,Is.Empty);
        }

        [Test]
        public void Scattered_meal_is_one_click_and_feeds_multiple_distinct_dragons()
        {
            var state=FiveSprouts();
            var drop=new FeedCommand(Rid(),DietOf(state,"d0"),Zone,Hab,"d0",new[]{"d1","d2","d3","d4"});
            var result=Ok(state,drop,new FixedClock(Day0));
            Assert.That(result.State.Player.Stats.TotalFeedClicks,Is.EqualTo(state.Player.Stats.TotalFeedClicks+1));
            Assert.That(result.Events.OfType<FoodSpawned>().Count(),Is.EqualTo(5));
            var assignments=BehaviorEngine.AssignFood(result.State,Zone,Hab,Day0);
            Assert.That(assignments.Count,Is.EqualTo(5));
            var current=result.State;
            foreach(var pair in assignments)
                current=Ok(current,new ConsumeFoodCommand(Rid(),pair.Value,pair.Key),new FixedClock(Day0+3000)).State;
            Assert.That(current.Food.Drops,Is.Empty);
            Assert.That(current.Dragons.Count(d=>d.LastFeedXpAtMs==Day0+3000),Is.EqualTo(5));
        }

        [Test]
        public void Scatter_respects_remaining_capacity_and_skips_unavailable_diners()
        {
            var s=FiveSprouts();
            s.FindDragon("d2").LastFeedXpAtMs=Day0;
            for(int i=0;i<3;i++)
                s=Ok(s,FeedFor(s,"d0"),new FixedClock(Day0+i*1000)).State;
            var drop=new FeedCommand(Rid(),DietOf(s,"d0"),Zone,Hab,"d0",new[]{"missing","d2","d3","d4"});
            var result=Ok(s,drop,new FixedClock(Day0+3000));
            Assert.That(result.Events.OfType<FoodSpawned>().Select(e=>e.TargetDragonUid),Is.EqualTo(new[]{"d0","d3"}));
            Assert.That(result.State.Food.Drops.Count,Is.EqualTo(5));
        }

        // ================================================================== sự kiện + không thêm gì

        [Test]
        public void Events_report_the_target_and_the_actual_recipient()
        {
            GameState s = FiveSprouts();
            DispatchResult f = Ok(s, FeedFor(s, "d3"), new FixedClock(Day0));
            FoodSpawned sp = f.Events.OfType<FoodSpawned>().Single();
            Assert.That((sp.TargetDragonUid, sp.ZoneId, sp.HabitatId), Is.EqualTo(("d3", Zone, Hab)));
            DispatchResult eat = Ok(f.State, new ConsumeFoodCommand(Rid(), sp.FoodId, "d3"), new FixedClock(Day0 + 3_000));
            FoodConsumed fc = eat.Events.OfType<FoodConsumed>().Single();
            Assert.That((fc.DragonUid, fc.TargetDragonUid, fc.AteTarget), Is.EqualTo(("d3", "d3", true)));
            // consumeFood vẫn chấp nhận rồng khác ăn được (va chạm thật của Sanctuary): sự kiện cho biết không phải mục tiêu.
            DispatchResult f2 = Ok(eat.State, FeedFor(eat.State, "d4"), new FixedClock(Day0 + 4_000));
            FoodConsumed other = Ok(f2.State, new ConsumeFoodCommand(Rid(), f2.State.Food.Drops.Single().Id, "d1"), new FixedClock(Day0 + 5_000)).Events.OfType<FoodConsumed>().Single();
            Assert.That((other.DragonUid, other.TargetDragonUid, other.AteTarget), Is.EqualTo(("d1", "d4", false)));
        }

        [Test]
        public void Targeting_changes_who_eats_but_not_xp_drops_gold_or_consumption()
        {
            GameState s = FiveSprouts();
            GameState a = s.DeepClone(), b = s.DeepClone();
            DispatchResult fa = Ok(a, new FeedCommand("same", DietOf(s, "d4"), Zone, Hab), new FixedClock(Day0));
            DispatchResult fb = Ok(b, new FeedCommand("same", DietOf(s, "d4"), Zone, Hab, "d4"), new FixedClock(Day0));
            string eaterA = BehaviorEngine.AssignmentOf(fa.State, "food-same", Day0 + 1).AssignedDragonUid;
            string eaterB = BehaviorEngine.AssignmentOf(fb.State, "food-same", Day0 + 1).AssignedDragonUid;
            Assert.That((eaterA, eaterB), Is.EqualTo(("d0", "d4")));
            DispatchResult ca = Ok(fa.State, new ConsumeFoodCommand("eat", "food-same", eaterA), new FixedClock(Day0 + 3_000));
            DispatchResult cb = Ok(fb.State, new ConsumeFoodCommand("eat", "food-same", eaterB), new FixedClock(Day0 + 3_000));
            Assert.That(cb.Events.OfType<FoodConsumed>().Single().XpGainedSec, Is.EqualTo(ca.Events.OfType<FoodConsumed>().Single().XpGainedSec));
            Assert.That(cb.State.FindDragon("d4").XpSec - s.FindDragon("d4").XpSec, Is.EqualTo(ca.State.FindDragon("d0").XpSec - s.FindDragon("d0").XpSec).Within(1e-9));
            Assert.That(cb.State.Player.Gold, Is.EqualTo(ca.State.Player.Gold));
            Assert.That((cb.State.Food.Drops.Count, cb.State.Player.Stats.TotalFeedClicks), Is.EqualTo((ca.State.Food.Drops.Count, ca.State.Player.Stats.TotalFeedClicks)));
            Assert.That(cb.State.Player.Inventory, Is.EqualTo(ca.State.Player.Inventory));
        }

        // ================================================================== tất định + phát lại

        [Test]
        public void Targeted_feeding_log_replays_to_the_identical_canonical_state()
        {
            GameState start = FiveSprouts();
            var log = new List<(GameCommand cmd, long now)>();
            GameState s = start;
            long t = Day0;
            var rng = new Random(15);
            for (int i = 0; i < 120; i++, t += 9_000)
            {
                string target = i % 4 == 3 ? null : "d" + rng.Next(0, 6); // d5 không tồn tại
                var feed = new FeedCommand("f" + i, DietOf(s, "d0"), Zone, Hab, target);
                log.Add((feed, t));
                DispatchResult r = CommandDispatcher.Dispatch(s, feed, new FixedClock(t));
                s = r.State;
                if (!r.Ok) continue;
                FoodAssignment a = BehaviorEngine.AssignmentOf(s, "food-f" + i, t + 1_000);
                if (a?.AssignedDragonUid == null) continue;
                var eat = new ConsumeFoodCommand("c" + i, a.FoodId, a.AssignedDragonUid);
                log.Add((eat, t + 3_000));
                s = CommandDispatcher.Dispatch(s, eat, new FixedClock(t + 3_000)).State;
            }
            GameState replay = start;
            foreach (var (cmd, now) in log) replay = CommandDispatcher.Dispatch(replay, cmd, new FixedClock(now)).State;
            Assert.That(Support.MiniJson.Show(Support.StateJson.ToJson(replay)), Is.EqualTo(Support.MiniJson.Show(Support.StateJson.ToJson(s))));
            // Ghi / đọc lệnh qua dạng dây (wire) của test giữ trường targetDragonUid.
            var wire = new Dictionary<string, object> { ["type"] = "feed", ["requestId"] = "w", ["dietId"] = "fruit", ["zoneId"] = Zone, ["habitatId"] = Hab, ["targetDragonUid"] = "d4" };
            Assert.That(((FeedCommand)Support.StateJson.Command(wire)).TargetDragonUid, Is.EqualTo("d4"));
            wire.Remove("targetDragonUid");
            Assert.That(((FeedCommand)Support.StateJson.Command(wire)).TargetDragonUid, Is.Null, "lệnh cũ không có trường → null");
        }

        [Test]
        public void Assignment_queries_never_change_state()
        {
            GameState s = FiveSprouts();
            DispatchResult f = Ok(s, FeedFor(s, "d4"), new FixedClock(Day0));
            string before = Support.MiniJson.Show(Support.StateJson.ToJson(f.State));
            BehaviorEngine.AssignFood(f.State, Zone, Hab, Day0 + 1);
            BehaviorEngine.AssignmentOf(f.State, f.State.Food.Drops[0].Id, Day0 + 1);
            BehaviorEngine.DecideAll(f.State, Day0 + 1, DayPhase.Day);
            Assert.That(Support.MiniJson.Show(Support.StateJson.ToJson(f.State)), Is.EqualTo(before));
            Assert.That(BehaviorEngine.AssignmentOf(f.State, "food-nope", Day0), Is.Null);
        }
    }
}
