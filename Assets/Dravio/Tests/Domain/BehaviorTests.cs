using System;
using System.Collections.Generic;
using System.Linq;
using Dravio.Domain.Behavior;
using Dravio.Domain.Biology;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Queries;
using Dravio.Domain.Tests.Support;
using NUnit.Framework;
using static Dravio.Domain.Tests.Support.Fixtures;

namespace Dravio.Domain.Tests
{
    /// <summary>
    /// U2.9 — cá tính, Behavioral Engine, Habitat Ecology, Research (ADR 0203). Engine chỉ đọc: test kiểm tính tất định,
    /// khả năng theo sinh học, ưu tiên gameplay, không đổi state / kinh tế, và các luật đề xuất mặc định tắt.
    /// </summary>
    public sealed class BehaviorTests
    {
        private const long Min = 60_000, Hour = 60 * Min;
        private static readonly DayPhase[] Phases = { DayPhase.Dawn, DayPhase.Day, DayPhase.Dusk, DayPhase.Night };
        private static readonly Personality[] Archetypes = { Personality.Playful, Personality.Calm, Personality.Curious, Personality.Shy };

        private static GameState WithDragons(params DragonState[] ds)
        {
            (GameState s, _) = NewGame();
            s = s.DeepClone();
            s.Dragons.Clear();
            s.ProductionDragonIds.Clear();
            s.Dragons.AddRange(ds);
            return s;
        }

        private static DragonState At(string uid, string species, string habitatId, Personality p = Personality.Calm, Action<DragonState> extra = null) =>
            Adult(uid, species, d => { d.Location = DragonLocation.Zone(HabitatCatalog.ZoneOf(habitatId), habitatId); d.Personality = p; extra?.Invoke(d); });

        // ------------------------------------------------------------------ Workstream A — cá tính

        [Test]
        public void Personality_traits_are_stable_per_uid_and_bounded_around_the_archetype()
        {
            var seenJitter = new HashSet<int>();
            for (int i = 0; i < 2000; i++)
            {
                string uid = "dr-" + i;
                foreach (Personality p in Archetypes)
                {
                    PersonalityTraits a = PersonalityProfiles.TraitsOf(uid, p), b = PersonalityProfiles.TraitsOf(uid, p);
                    Assert.That(a.ToString(), Is.EqualTo(b.ToString()));
                    foreach (int v in new[] { a.Energy, a.Curiosity, a.Playfulness, a.Boldness }) Assert.That(v, Is.InRange(0, 100));
                }
                seenJitter.Add(PersonalityProfiles.Jitter(uid, "energy"));
            }
            Assert.That(seenJitter.Min(), Is.EqualTo(-PersonalityProfiles.JitterMax));
            Assert.That(seenJitter.Max(), Is.EqualTo(PersonalityProfiles.JitterMax));
            Assert.That(seenJitter.Count, Is.EqualTo(2 * PersonalityProfiles.JitterMax + 1));
        }

        [Test]
        public void Archetypes_keep_their_ordering_whatever_the_uid_jitter()
        {
            // Khoảng cách giữa các archetype > 2 × jitter ở nét đặc trưng, nên cá thể không bao giờ "đổi" tính cách.
            for (int i = 0; i < 500; i++)
            {
                string uid = "u" + i;
                Assert.That(PersonalityProfiles.TraitsOf(uid, Personality.Playful).Playfulness, Is.GreaterThan(PersonalityProfiles.TraitsOf(uid, Personality.Calm).Playfulness));
                Assert.That(PersonalityProfiles.TraitsOf(uid, Personality.Curious).Curiosity, Is.GreaterThan(PersonalityProfiles.TraitsOf(uid, Personality.Shy).Curiosity));
                Assert.That(PersonalityProfiles.TraitsOf(uid, Personality.Shy).Boldness, Is.LessThan(PersonalityProfiles.TraitsOf(uid, Personality.Calm).Boldness));
            }
        }

        [Test]
        public void Favorite_diet_and_habitat_always_belong_to_the_species_biology()
        {
            foreach (DragonSpecies sp in SpeciesCatalog.All)
                for (int i = 0; i < 200; i++)
                {
                    PersonalityProfile p = PersonalityProfiles.Of(Adult("x" + i, sp.Id));
                    Assert.That(sp.Biology.DietIds, Does.Contain(p.FavoriteDiet), sp.Id);
                    Assert.That(sp.Biology.HabitatRules, Does.Contain(p.FavoriteHabitatId), sp.Id);
                    Assert.That(p.SummaryKey, Is.EqualTo("personality.calm.summary"));
                }
        }

        // ------------------------------------------------------------------ chu kỳ ngày đêm

        [TestCase(0, DayPhase.Night)]
        [TestCase(299, DayPhase.Night)]
        [TestCase(300, DayPhase.Dawn)]
        [TestCase(419, DayPhase.Dawn)]
        [TestCase(420, DayPhase.Day)]
        [TestCase(1049, DayPhase.Day)]
        [TestCase(1050, DayPhase.Dusk)]
        [TestCase(1169, DayPhase.Dusk)]
        [TestCase(1170, DayPhase.Night)]
        [TestCase(1439, DayPhase.Night)]
        [TestCase(1440 + 300, DayPhase.Dawn)]
        [TestCase(-1, DayPhase.Night)]
        [TestCase(-1440 + 420, DayPhase.Day)]
        public void Day_phase_boundaries_match_the_environment_living_data(int minute, DayPhase expected) =>
            Assert.That(DayCycle.PhaseOfMinute(minute), Is.EqualTo(expected));

        // ------------------------------------------------------------------ Workstream B — khả năng theo sinh học

        [Test]
        public void Species_level_capabilities_follow_G0_locomotion_respiration_and_thermal_bands()
        {
            SpeciesBehaviorProfile frost = BehaviorQueries.Species("frostfin");
            Assert.That(Row(frost, DragonBehavior.Bask).SpeciesBlock, Is.EqualTo(BehaviorBlock.NotThermophile));
            Assert.That(Row(frost, DragonBehavior.Fly).SpeciesBlock, Is.EqualTo(BehaviorBlock.NoLocomotion));
            Assert.That(Row(frost, DragonBehavior.Burrow).SpeciesBlock, Is.EqualTo(BehaviorBlock.NoLocomotion));
            Assert.That(Row(frost, DragonBehavior.Swim).Habitats, Is.EqualTo(new[] { "ocean_cool_pool" }));
            SpeciesBehaviorProfile ember = BehaviorQueries.Species("emberling");
            Assert.That(Row(ember, DragonBehavior.Swim).SpeciesBlock, Is.EqualTo(BehaviorBlock.NoLocomotion));
            Assert.That(Row(ember, DragonBehavior.Bask).Habitats, Is.EqualTo(new[] { "origin_meadow", "origin_warm_rocks" }));
            Assert.That(Row(BehaviorQueries.Species("droplet"), DragonBehavior.Swim).Habitats, Is.EqualTo(new[] { "origin_pondbank", "ocean_estuary" }));
            Assert.That(Row(BehaviorQueries.Species("zephyr"), DragonBehavior.Fly).Habitats, Is.EqualTo(new[] { "origin_meadow", "sky_low_island" }));
            Assert.That(Row(BehaviorQueries.Species("magma"), DragonBehavior.Burrow).Habitats, Is.EqualTo(new[] { "origin_warm_rocks" }));
            Assert.That(Row(BehaviorQueries.Species("sprout"), DragonBehavior.Burrow).Habitats, Is.EqualTo(new[] { "origin_meadow", "origin_pondbank" }));
            Assert.That(BehaviorQueries.Matrix().Select(m => m.SpeciesId), Is.EqualTo(SpeciesCatalog.All.Select(s => s.Id)));
            Assert.That(BehaviorQueries.Matrix(), Has.Count.EqualTo(16));
        }

        [TestCase("origin_meadow", "bask,fly,burrow")]
        [TestCase("origin_pondbank", "bask,swim,fly,burrow")]
        [TestCase("origin_warm_rocks", "bask,fly,burrow")]
        [TestCase("ocean_estuary", "swim")]
        [TestCase("ocean_reef", "swim")]
        [TestCase("ocean_cool_pool", "swim")]
        [TestCase("sky_low_island", "bask,fly")]
        public void Habitat_level_conditions_for_environment_dependent_behaviors(string habitatId, string allowed)
        {
            HabitatProfile h = Habitat(habitatId);
            var envBehaviors = new[] { DragonBehavior.Bask, DragonBehavior.Swim, DragonBehavior.Fly, DragonBehavior.Burrow };
            string got = string.Join(",", envBehaviors.Where(b => BehaviorCatalog.HabitatBlock(h, b) == BehaviorBlock.None).Select(b => b.ToWire()));
            Assert.That(got, Is.EqualTo(allowed));
            foreach (DragonBehavior b in BehaviorCatalog.All.Except(envBehaviors)) Assert.That(BehaviorCatalog.HabitatBlock(h, b), Is.EqualTo(BehaviorBlock.None));
        }

        private static BehaviorAvailability Row(SpeciesBehaviorProfile p, DragonBehavior b) => p.Behaviors.Single(x => x.Behavior == b);

        [Test]
        public void Every_decision_is_biologically_possible_for_every_species_in_every_valid_habitat()
        {
            int checkedDecisions = 0;
            var seen = new HashSet<DragonBehavior>();
            foreach (DragonSpecies sp in SpeciesCatalog.All)
                foreach (string hid in sp.Biology.HabitatRules)
                {
                    HabitatProfile h = Habitat(hid);
                    foreach (Personality p in Archetypes)
                    {
                        GameState s = WithDragons(At("a-" + sp.Id, sp.Id, hid, p), At("b-" + sp.Id, sp.Id, hid, p));
                        foreach (DayPhase ph in Phases)
                            for (long t = T0; t < T0 + 3 * Hour; t += BehaviorCatalog.EpochMs)
                            {
                                BehaviorDecision d = BehaviorEngine.Decide(s, "a-" + sp.Id, t, ph);
                                Assert.That(d.Visible, Is.True);
                                DragonBehavior b = d.Behavior.Value;
                                seen.Add(b);
                                Assert.That(BehaviorCatalog.Allowed(sp.Biology, h, b), Is.True, sp.Id + "@" + hid + " " + b);
                                AssertHintPossible(sp.Biology, h, d.Locomotion, sp.Id + "@" + hid + " " + b);
                                checkedDecisions++;
                            }
                    }
                }
            Assert.That(checkedDecisions, Is.GreaterThan(50_000));
            // Mọi hành vi trình diễn đều thực sự xuất hiện ở đâu đó.
            foreach (DragonBehavior b in BehaviorCatalog.All.Where(x => BehaviorCatalog.Get(x).Ambient)) Assert.That(seen, Does.Contain(b), b.ToString());
        }

        private static void AssertHintPossible(SpeciesBiology b, HabitatProfile h, LocomotionHint hint, string ctx)
        {
            switch (hint)
            {
                case LocomotionHint.Swim: Assert.That(b.Locomotion, Does.Contain(Locomotion.Swim), ctx); break;
                case LocomotionHint.Fly: Assert.That(b.Locomotion, Does.Contain(Locomotion.Fly), ctx); break;
                case LocomotionHint.Burrow: Assert.That(b.Locomotion, Does.Contain(Locomotion.Burrow), ctx); break;
                case LocomotionHint.Walk:
                case LocomotionHint.Run:
                    Assert.That(b.Locomotion, Does.Contain(Locomotion.Walk), ctx);
                    Assert.That(h.Realm, Is.Not.EqualTo(Realm.Ocean), ctx);
                    break;
            }
        }

        [Test]
        public void Diurnal_species_sleep_at_night_and_nocturnal_species_sleep_by_day()
        {
            GameState s = WithDragons(At("e", "emberling", "origin_meadow"), At("c", "celestia", "origin_meadow"), At("d", "droplet", "origin_pondbank"));
            for (long t = T0; t < T0 + 2 * Hour; t += BehaviorCatalog.EpochMs)
            {
                Assert.That(BehaviorEngine.Decide(s, "e", t, DayPhase.Night).Behavior, Is.EqualTo(DragonBehavior.Sleep));
                Assert.That(BehaviorEngine.Decide(s, "e", t, DayPhase.Night).Reason, Is.EqualTo("circadian-sleep"));
                Assert.That(BehaviorEngine.Decide(s, "c", t, DayPhase.Day).Behavior, Is.EqualTo(DragonBehavior.Sleep));
                Assert.That(BehaviorEngine.Decide(s, "c", t, DayPhase.Night).Reason, Is.EqualTo("ambient"));
                Assert.That(BehaviorEngine.Decide(s, "d", t, DayPhase.Night).Reason, Is.EqualTo("ambient"), "both-cycle species stay active at night");
            }
            // Ban ngày ở loài ban ngày không có Sleep trong danh sách trọng số dương.
            BehaviorDecision day = BehaviorEngine.Decide(s, "e", T0, DayPhase.Day);
            Assert.That(day.Weights.Single(w => w.Behavior == DragonBehavior.Sleep).Weight, Is.EqualTo(0));
            Assert.That(day.Weights.Any(w => w.Behavior == DragonBehavior.Swim), Is.False, "Emberling never considers swimming");
            // Bask chỉ khi có nắng.
            Assert.That(BehaviorEngine.Decide(s, "e", T0, DayPhase.Dusk).Weights.Single(w => w.Behavior == DragonBehavior.Bask).Weight, Is.EqualTo(0));
        }

        [Test]
        public void Personality_shifts_behavior_shares_in_the_expected_direction()
        {
            Dictionary<DragonBehavior, int> Share(Personality p, int others)
            {
                var counts = BehaviorCatalog.All.ToDictionary(b => b, b => 0);
                for (int i = 0; i < 40; i++)
                {
                    var ds = new List<DragonState> { At("m" + i, "zephyr", "origin_meadow", p, d => d.XpSec = 0) };
                    for (int k = 0; k < others; k++) ds.Add(At("o" + i + "-" + k, "zephyr", "origin_meadow", Personality.Calm));
                    GameState s = WithDragons(ds.ToArray());
                    for (long t = T0; t < T0 + 4 * Hour; t += BehaviorCatalog.EpochMs) counts[BehaviorEngine.Decide(s, "m" + i, t, DayPhase.Day).Behavior.Value]++;
                }
                return counts;
            }
            var playful = Share(Personality.Playful, 1); var calm = Share(Personality.Calm, 1);
            var curious = Share(Personality.Curious, 1); var shy = Share(Personality.Shy, 1);
            Assert.That(playful[DragonBehavior.Play], Is.GreaterThan(calm[DragonBehavior.Play] * 3 / 2));
            Assert.That(curious[DragonBehavior.Explore], Is.GreaterThan(shy[DragonBehavior.Explore]));
            Assert.That(calm[DragonBehavior.Rest] + calm[DragonBehavior.Idle], Is.GreaterThan(playful[DragonBehavior.Rest] + playful[DragonBehavior.Idle]));
            // Nhút nhát + đông (≥ 3 rồng khác): ít chơi hơn khi vắng.
            Assert.That(Share(Personality.Shy, 3)[DragonBehavior.Play], Is.LessThan(shy[DragonBehavior.Play]));
        }

        // ------------------------------------------------------------------ tất định

        [Test]
        public void Decisions_are_deterministic_and_stable_within_an_epoch()
        {
            GameState s = WithDragons(At("a", "sparkwing", "origin_meadow", Personality.Curious), At("b", "droplet", "origin_pondbank", Personality.Playful));
            GameState clone = s.DeepClone();
            for (long t = T0; t < T0 + Hour; t += 7_919)
                foreach (string uid in new[] { "a", "b" })
                {
                    BehaviorDecision x = BehaviorEngine.Decide(s, uid, t, DayPhase.Day), y = BehaviorEngine.Decide(clone, uid, t, DayPhase.Day);
                    Assert.That(Fingerprint(x), Is.EqualTo(Fingerprint(y)));
                    Assert.That(t, Is.InRange(x.StartedAtMs, x.UntilMs - 1));
                    Assert.That(x.UntilMs - x.StartedAtMs, Is.EqualTo(BehaviorCatalog.EpochMs));
                    Assert.That(Fingerprint(BehaviorEngine.Decide(s, uid, x.StartedAtMs, DayPhase.Day)), Is.EqualTo(Fingerprint(x)));
                    Assert.That(Fingerprint(BehaviorEngine.Decide(s, uid, x.UntilMs - 1, DayPhase.Day)), Is.EqualTo(Fingerprint(x)));
                }
            // Các UID lệch pha nhau.
            Assert.That(BehaviorEngine.EpochOffset("a"), Is.Not.EqualTo(BehaviorEngine.EpochOffset("b")));
            Assert.That(BehaviorEngine.EpochIndex("a", -1), Is.LessThanOrEqualTo(BehaviorEngine.EpochIndex("a", 0)));
        }

        /// <summary>
        /// Vân tay golden: 16 loài × microhabitat × 4 cá tính × 4 pha × 120 nhịp. Đổi trọng số/hash là đổi hành vi người chơi thấy, nên phải cập nhật có chủ đích.
        /// CI chạy cả Linux và Windows: chung một giá trị = tất định trên mọi nền tảng.
        /// </summary>
        [Test]
        public void Golden_fingerprint_of_decisions_is_platform_independent()
        {
            uint h = 0x811C9DC5u;
            foreach (DragonSpecies sp in SpeciesCatalog.All)
                foreach (string hid in sp.Biology.HabitatRules)
                    foreach (Personality p in Archetypes)
                    {
                        GameState s = WithDragons(At(sp.Id + "-1", sp.Id, hid, p, d => d.XpSec = 0), At(sp.Id + "-2", sp.Id, hid, Personality.Calm));
                        foreach (DayPhase ph in Phases)
                            for (int e = 0; e < 120; e++)
                                h = Mix(h, Fingerprint(BehaviorEngine.Decide(s, sp.Id + "-1", T0 + e * BehaviorCatalog.EpochMs, ph)));
                    }
            Assert.That(h.ToString("x8"), Is.EqualTo(GoldenFingerprint));
        }

        private const string GoldenFingerprint = "1151cfb6";

        private static uint Mix(uint h, string s)
        {
            unchecked { foreach (char c in s) { h ^= c; h *= 0x01000193u; } h ^= 0x1Fu; h *= 0x01000193u; }
            return h;
        }

        private static string Fingerprint(BehaviorDecision d) =>
            d == null ? "null" : d.Uid + "|" + d.Behavior + "|" + d.Role + "|" + d.Reason + "|" + d.Locomotion + "|" + d.Interaction + "|" + d.TargetFoodId + "|" + d.StartedAtMs + "|" + d.UntilMs;

        // ------------------------------------------------------------------ gameplay

        [Test]
        public void Food_drop_makes_one_eligible_dragon_seek_it_and_consumeFood_then_succeeds()
        {
            (GameState s0, FixedClock clock) = NewGame();
            GameState s = s0.DeepClone();
            s.Dragons.Add(At("z1", "emberling", "origin_meadow", Personality.Shy));
            s.Dragons.Add(At("z2", "sprout", "origin_meadow", Personality.Playful));
            string ember = Ember(s).Uid;
            // Thức ăn "leaf": chỉ Sprout ăn được.
            s = Ok(s, Feed("origin_meadow", DietId.Leaf), clock).State;
            string leaf = s.Food.Drops.Single().Id;
            Assert.That(BehaviorEngine.Decide(s, "z2", clock.NowMs(), DayPhase.Day).TargetFoodId, Is.EqualTo(leaf));
            Assert.That(BehaviorEngine.Decide(s, "z1", clock.NowMs(), DayPhase.Day).Behavior, Is.Not.EqualTo(DragonBehavior.SeekFood));
            Assert.That(BehaviorEngine.Decide(s, "z1", clock.NowMs(), DayPhase.Night).Behavior, Is.EqualTo(DragonBehavior.Sleep));

            // Một drop "fruit": Emberling và rồng Emberling khởi đầu đều ăn được, nhưng chỉ một con được giao.
            clock.Advance(1000);
            s = Ok(s, Feed("origin_meadow", DietId.Fruit), clock).State;
            string fruit = s.Food.Drops.Single(f => f.DietId == DietId.Fruit).Id;
            List<BehaviorDecision> all = BehaviorEngine.DecideAll(s, clock.NowMs(), DayPhase.Day);
            Assert.That(all.Count(d => d.TargetFoodId == fruit), Is.EqualTo(1));
            Assert.That(all.Count(d => d.TargetFoodId == leaf), Is.EqualTo(1));
            Assert.That(all.Where(d => d.TargetFoodId != null).All(d => d.Role == BehaviorRole.Gameplay && d.Behavior == DragonBehavior.SeekFood), Is.True);

            // Mọi SeekFood dẫn tới consumeFood thành công ngay lúc đó.
            foreach (BehaviorDecision d in all.Where(d => d.TargetFoodId != null))
                Ok(s, new ConsumeFoodCommand(Rid(), d.TargetFoodId, d.Uid), clock);

            BehaviorDecision seeker = all.Single(d => d.TargetFoodId == fruit);
            s = Ok(s, new ConsumeFoodCommand(Rid(), fruit, seeker.Uid), clock).State;
            BehaviorDecision eat = BehaviorEngine.Decide(s, seeker.Uid, clock.NowMs() + 1000, DayPhase.Day);
            Assert.That(eat.Behavior, Is.EqualTo(DragonBehavior.Eat));
            Assert.That(eat.Interaction, Is.EqualTo(InteractionHint.Eat));
            Assert.That(eat.UntilMs, Is.EqualTo(clock.NowMs() + BehaviorCatalog.EatDisplayMs));

            // Đang hồi feed (8 s) thì không được giao thức ăn mới — tránh consumeFood bị từ chối feed-cooldown.
            clock.Advance(BehaviorCatalog.EatDisplayMs + 100);
            s = Ok(s, Feed("origin_meadow", DietId.Fruit), clock).State;
            Assert.That(BehaviorEngine.Decide(s, seeker.Uid, clock.NowMs(), DayPhase.Day).Behavior, Is.Not.EqualTo(DragonBehavior.SeekFood));
            _ = ember;
        }

        [Test]
        public void Location_activity_and_vitals_drive_gameplay_decisions()
        {
            GameState s = Normalize(WithDragons(
                Adult("st", "emberling"),
                Adult("care", "emberling", d => d.Location = DragonLocation.Care("case-1")),
                Adult("sh", "emberling", d => d.Location = DragonLocation.Shelter("origin")),
                At("exp", "emberling", "origin_meadow", extra: d => d.Activity = DragonActivity.Expedition),
                At("low", "emberling", "origin_meadow", extra: d => d.Vitals.Mobility = 40),
                At("tired", "emberling", "origin_meadow", extra: d => d.Vitals.Energy = 10),
                Adult("gone", "emberling", d => d.Location = DragonLocation.Released)));
            Assert.That(BehaviorEngine.Decide(s, "st", T0, DayPhase.Day).Visible, Is.False);
            Assert.That(BehaviorEngine.Decide(s, "st", T0, DayPhase.Day).Reason, Is.EqualTo("in-storage"));
            Assert.That(BehaviorEngine.Decide(s, "care", T0, DayPhase.Day).Reason, Is.EqualTo("in-care"));
            Assert.That(BehaviorEngine.Decide(s, "care", T0, DayPhase.Day).Behavior, Is.EqualTo(DragonBehavior.Rest));
            Assert.That(BehaviorEngine.Decide(s, "sh", T0, DayPhase.Day).Reason, Is.EqualTo("sheltered"));
            Assert.That(BehaviorEngine.Decide(s, "exp", T0, DayPhase.Day).Visible, Is.False);
            Assert.That(BehaviorEngine.Decide(s, "low", T0, DayPhase.Day).Reason, Is.EqualTo("vitals-low"));
            Assert.That(BehaviorEngine.Decide(s, "tired", T0, DayPhase.Day).Behavior, Is.EqualTo(DragonBehavior.Sleep));
            Assert.That(BehaviorEngine.Decide(s, "gone", T0, DayPhase.Day), Is.Null);
            Assert.That(BehaviorEngine.Decide(s, "nobody", T0, DayPhase.Day), Is.Null);
            Assert.That(BehaviorEngine.DecideAll(s, T0, DayPhase.Day).Select(d => d.Uid), Is.EqualTo(new[] { "st", "care", "sh", "exp", "low", "tired" }));
        }

        // ------------------------------------------------------------------ không đổi state / kinh tế

        [Test]
        public void Queries_never_mutate_the_game_state()
        {
            (GameState s0, FixedClock clock) = NewGame();
            GameState s = s0.DeepClone();
            s.Dragons.Add(At("q1", "sprout", "origin_meadow", Personality.Curious, d => d.Vitals.Comfort = 70));
            s = Ok(s, Feed(), clock).State;
            string before = MiniJson.Show(StateJson.ToJson(s));
            var journal = new ObservationJournal();
            foreach (DayPhase ph in Phases)
            {
                foreach (BehaviorDecision d in BehaviorQueries.All(s, clock.NowMs(), ph)) journal.Observe(d, clock.NowMs(), ResearchRules.ProposedForReview);
                foreach (DragonState d in s.Dragons)
                {
                    BehaviorQueries.Personality(s, d.Uid);
                    BehaviorQueries.Ecology(s, d.Uid, EcologyComfortRules.ProposedForReview);
                    BehaviorQueries.Research(s, d.SpeciesId, journal, ResearchRules.ProposedForReview);
                }
            }
            Assert.That(MiniJson.Show(StateJson.ToJson(s)), Is.EqualTo(before));
        }

        [Test]
        public void Production_rules_for_new_mechanics_are_off()
        {
            Assert.That(EcologyComfortRules.Production.Enabled, Is.False);
            Assert.That(ResearchRules.Production.Enabled, Is.False);
            Assert.That(DisasterRules.Production.Enabled, Is.False, "U2.9 không bật thiên tai tự nhiên");
            GameState s = WithDragons(At("a", "emberling", "origin_meadow", extra: d => d.Vitals.Comfort = 30));
            string before = MiniJson.Show(StateJson.ToJson(s));
            Assert.That(EcologyComfort.Apply(s, T0, T0 + 24 * Hour, AdvanceMode.Online, EcologyComfortRules.Production, new List<DomainEvent>()), Is.EqualTo(0));
            Assert.That(MiniJson.Show(StateJson.ToJson(s)), Is.EqualTo(before));
            Assert.That(BehaviorQueries.Ecology(s, "a").ComfortDriftEnabled, Is.False);
        }

        [Test]
        public void Proposed_comfort_drift_touches_only_comfort_never_gold_xp_or_bond()
        {
            GameState s = WithDragons(At("a", "sprout", "origin_meadow", extra: d => { d.Vitals.Comfort = 10; d.Bond = 7; }));
            s.ProductionDragonIds.Add("a");
            Dictionary<string, object> before = StateJson.ToJson(s);
            var events = new List<DomainEvent>();
            Assert.That(EcologyComfort.Apply(s, T0, T0 + 10 * Hour, AdvanceMode.Online, EcologyComfortRules.ProposedForReview, events), Is.EqualTo(1));
            Assert.That(events.Single(), Is.InstanceOf<VitalsChanged>());
            DragonState d = s.FindDragon("a");
            Assert.That(d.Vitals.Comfort, Is.GreaterThan(10));
            d.Vitals.Comfort = 10; // phần còn lại phải y nguyên
            Assert.That(MiniJson.Diff(before, StateJson.ToJson(s)), Is.Null);
        }

        // ------------------------------------------------------------------ Workstream C — habitat ecology

        [Test]
        public void Ecology_report_agrees_with_suitability_for_every_species_and_habitat()
        {
            foreach (DragonSpecies sp in SpeciesCatalog.All)
                foreach (HabitatProfile h in HabitatCatalog.All)
                {
                    HabitatResult r = Suitability.ForSpecies(sp.Id, h.Id);
                    HabitatEcologyReport e = HabitatEcology.Explain(sp.Id, h.Id);
                    Assert.That(e.Score, Is.EqualTo(r.Score), sp.Id + "@" + h.Id);
                    Assert.That(e.Allowed, Is.EqualTo(r.Allowed));
                    Assert.That(e.Reason, Is.EqualTo(r.Reason));
                    bool gateFails = e.Factors.Any(f => f.Kind == EcologyFactorKind.Gate && !f.Ok);
                    Assert.That(gateFails, Is.EqualTo(r.Reason == SuitabilityReason.Incompatible), sp.Id + "@" + h.Id);
                    if (!gateFails) Assert.That(e.Score, Is.EqualTo(Math.Max(0, 100 - e.Factors.Sum(f => f.Deficit))));
                    Assert.That(e.PrimaryIssueKey, Does.StartWith("ecology."));
                }
        }

        [Test]
        public void Ecology_explains_why_a_habitat_is_unsuitable()
        {
            Assert.That(HabitatEcology.Explain("frostfin", "ocean_reef").PrimaryIssueKey, Is.EqualTo("ecology.nativeHabitat.not-native"));
            HabitatEcologyReport hot = HabitatEcology.Explain("frostfin", "ocean_reef");
            Assert.That(hot.Factors.Single(f => f.Key == "temperature").ExplanationKey, Is.EqualTo("ecology.temperature.too-hot"));
            Assert.That(HabitatEcology.Explain("emberling", "ocean_estuary").Factors.Single(f => f.Key == "respiration").ExplanationKey, Is.EqualTo("ecology.respiration.needs-air"));
            Assert.That(HabitatEcology.Explain("magma", "sky_low_island").Factors.Single(f => f.Key == "perch").ExplanationKey, Is.EqualTo("ecology.perch.cannot-fly"));
            Assert.That(HabitatEcology.Explain("emberling", "origin_meadow").PrimaryIssueKey, Is.EqualTo("ecology.ok"));
            Assert.That(HabitatEcology.Explain("emberling", "nowhere").Reason, Is.EqualTo(SuitabilityReason.InvalidEnv));

            // Môi trường suy giảm: Sprout cần vegetation ≥ 65; còn 40 → thiếu 25 → 75 "needs-improvement", lý do vegetation.
            HabitatProfile meadow = Habitat("origin_meadow");
            HabitatProfile poor = Env(meadow, quality: meadow.Quality.With(QualityKey.Vegetation, 40));
            HabitatEcologyReport e = HabitatEcology.Explain("sprout", "origin_meadow", poor);
            Assert.That(e.Score, Is.EqualTo(75));
            Assert.That(e.Tier, Is.EqualTo(SuitabilityTier.NeedsImprovement));
            Assert.That(e.PrimaryIssueKey, Is.EqualTo("ecology.vegetation.low"));
            Assert.That(e.Factors.Single(f => f.Key == "waterQuality").Relevant, Is.False);
        }

        [Test]
        public void Proposed_comfort_target_uses_floors_and_favorite_bonus()
        {
            EcologyComfortRules r = EcologyComfortRules.ProposedForReview;
            Assert.That(EcologyComfort.Target(30, false, Difficulty.Cozy, r), Is.EqualTo(60));
            Assert.That(EcologyComfort.Target(30, false, Difficulty.Ecosystem, r), Is.EqualTo(40));
            Assert.That(EcologyComfort.Target(70, true, Difficulty.Ecosystem, r), Is.EqualTo(75));
            Assert.That(EcologyComfort.Target(98, true, Difficulty.Ecosystem, r), Is.EqualTo(100));
            for (int score = 0; score <= 100; score++)
                Assert.That(EcologyComfort.Target(score, false, Difficulty.Cozy, r), Is.GreaterThanOrEqualTo(HealthCatalog.BreedMinComfort),
                    "Cozy: môi trường không bao giờ tự chặn lai / thám hiểm");
        }

        [Test]
        public void Proposed_comfort_drift_is_split_invariant_and_never_drops_offline()
        {
            EcologyComfortRules r = EcologyComfortRules.ProposedForReview;
            long a = T0 + 1234, c = a + 5 * Hour + 77;
            foreach (AdvanceMode mode in new[] { AdvanceMode.Online, AdvanceMode.Offline })
                foreach (int start in new[] { 20, 60, 100 })
                    foreach (int target in new[] { 40, 60, 100 })
                    {
                        int whole = EcologyComfort.Step(start, target, a, c, mode, r);
                        int split = start;
                        for (long t = a; t < c; t += 7 * Min + 13) split = EcologyComfort.Step(split, target, t, Math.Min(c, t + 7 * Min + 13), mode, r);
                        Assert.That(split, Is.EqualTo(whole), mode + " " + start + "→" + target);
                        if (mode == AdvanceMode.Offline) Assert.That(whole, Is.GreaterThanOrEqualTo(start), "offline never lowers comfort");
                        Assert.That(whole, Is.InRange(Math.Min(start, target), Math.Max(start, target)));
                    }
            Assert.That(EcologyComfort.Step(100, 60, T0, T0 + 6 * Min * 10, AdvanceMode.Online, r), Is.EqualTo(90));
        }

        // ------------------------------------------------------------------ Workstream D — research

        [Test]
        public void Research_tiers_follow_existing_progress_and_stay_ungated_in_production()
        {
            GameState s = WithDragons(At("a", "droplet", "origin_pondbank", extra: d => d.XpSec = 0));
            Progression.Codex.Observe(s, null);
            SpeciesResearchView prod = BehaviorQueries.Research(s, "droplet");
            Assert.That(prod.Gated, Is.False);
            Assert.That(prod.RevealedSections, Has.Count.EqualTo(4));
            Assert.That(BehaviorQueries.Research(s, "gaia").RevealedSections, Is.Empty);

            ResearchRules on = ResearchRules.ProposedForReview;
            Assert.That(BehaviorQueries.Research(s, "gaia", null, on).Tier, Is.EqualTo(ResearchTier.Unknown));
            SpeciesResearchView v = BehaviorQueries.Research(s, "droplet", null, on);
            Assert.That(v.Tier, Is.EqualTo(ResearchTier.Sighted));
            Assert.That(v.RevealedSections, Is.EqualTo(new[] { CodexSection.Identity }));
            Assert.That(v.NextStepKey, Is.EqualTo("research.next.viewProfile"));
            s.Tutorial.ProfilesViewed.Add("droplet");
            Assert.That(BehaviorQueries.Research(s, "droplet", null, on).Tier, Is.EqualTo(ResearchTier.Profiled));
            s.FindDragon("a").XpSec = SpeciesCatalog.Get("droplet").GrowthTimeSec;
            Progression.Codex.Observe(s, null);
            Assert.That(BehaviorQueries.Research(s, "droplet", null, on).Tier, Is.EqualTo(ResearchTier.LifeCycle));
            Assert.That(BehaviorQueries.Research(s, "droplet", null, on).ObservableBehaviors, Does.Contain(DragonBehavior.Swim).And.Not.Contain(DragonBehavior.Fly));

            // Quan sát qua nhiều nhịp, nhiều pha và cả hai microhabitat cho tới khi đủ mọi tập tính.
            var journal = new ObservationJournal();
            Assert.That(journal.Observe(BehaviorEngine.Decide(s, "a", T0, DayPhase.Day), T0, ResearchRules.Production).Reason, Is.EqualTo("research-disabled"));
            long t = T0;
            foreach (string hid in new[] { "origin_pondbank", "ocean_estuary" })
            {
                s.FindDragon("a").Location = DragonLocation.Zone(HabitatCatalog.ZoneOf(hid), hid);
                foreach (DayPhase ph in Phases)
                    for (int i = 0; i < 400; i++, t += BehaviorCatalog.EpochMs) journal.Observe(BehaviorEngine.Decide(s, "a", t, ph), t, on);
            }
            // Ăn: cho một lần ăn thật (LastFeedXpAtMs) và một drop đang chờ.
            s.FindDragon("a").LastFeedXpAtMs = t;
            journal.Observe(BehaviorEngine.Decide(s, "a", t + 1, DayPhase.Day), t + 1, on);
            t += Min;
            s.Food.Drops.Add(new FoodDrop { Id = "f1", DietId = DietId.Fruit, ZoneId = "ocean", HabitatId = "ocean_estuary", SpawnedAtMs = t, ExpiresAtMs = t + 10_000 });
            ObservationResult seek = journal.Observe(BehaviorEngine.Decide(s, "a", t, DayPhase.Day), t, on);
            Assert.That(seek.NewBehavior, Is.True);
            Assert.That(journal.Observe(BehaviorEngine.Decide(s, "a", t + 1, DayPhase.Day), t + 1, on).Reason, Is.EqualTo("observation-cooldown"));
            SpeciesResearchView done = BehaviorQueries.Research(s, "droplet", journal, on);
            Assert.That(done.ObservedBehaviors, Is.SupersetOf(done.ObservableBehaviors));
            Assert.That(done.Tier, Is.EqualTo(ResearchTier.Ethology));
            Assert.That(done.RevealedSections, Has.Count.EqualTo(4));
            Assert.That(done.NextStepKey, Is.EqualTo("research.next.done"));
        }

        [Test]
        public void Stored_storage_dragons_are_not_observable()
        {
            var journal = new ObservationJournal();
            GameState s = WithDragons(Adult("st", "emberling"));
            Assert.That(journal.Observe(BehaviorEngine.Decide(s, "st", T0, DayPhase.Day), T0, ResearchRules.ProposedForReview).Reason, Is.EqualTo("not-observable"));
        }
    }
}
