using System;
using System.Collections.Generic;
using System.Linq;
using Dravio.Domain.Biology;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Tests.Support;
using Dravio.Domain.World;
using NUnit.Framework;
using static Dravio.Domain.Tests.Support.Fixtures;

namespace Dravio.Domain.Tests
{
    /// <summary>Port của tests/unit/zones.test.ts và phần surveyHabitat / viewBiologyProfile của tutorial.test.ts.</summary>
    public sealed class ZoneCommandTests
    {
        private static readonly long ZT0 = new DateTimeOffset(2026, 11, 25, 9, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        private static readonly MoveTarget Meadow = MoveTarget.Zone("origin", "origin_meadow");
        private static readonly Dictionary<string, string> Needs = new Dictionary<string, string> { ["origin_warm_rocks"] = "DISC_EMBERLING_THERMAL", ["origin_pondbank"] = "DISC_AQUATIC_HABITAT" };

        private static (GameState state, FixedClock clock) Game() => (InitialStateFactory.Create("zones", ZT0, Rng.Create(8)), new FixedClock(ZT0));
        private static string First(GameState s) => s.Dragons[0].Uid;
        private static ZoneState Origin(GameState s) => s.FindZone("origin");

        /// <summary>Fixture: ghi nhận khám phá như hệ nhiệm vụ sẽ làm, rồi mở bằng hàm engine.</summary>
        private static GameState Discover(GameState s, string id)
        {
            GameState c = s.DeepClone();
            Assert.That(Zones.RecordDiscovery(c, id, new List<DomainEvent>()), Is.Null);
            return c;
        }

        private static GameState Unlock(GameState s, params string[] habitats)
        {
            foreach (string h in habitats)
            {
                if (Needs.TryGetValue(h, out string need) && !s.Discoveries.Contains(need)) s = Discover(s, need);
                GameState c = s.DeepClone();
                Assert.That(Zones.ApplyUnlock(c, h, new List<DomainEvent>()), Is.Null, h);
                s = c;
            }
            return s;
        }

        private static GameState With(GameState s, params DragonState[] dragons) { GameState c = s.DeepClone(); c.Dragons.AddRange(dragons); return c; }

        private static DispatchResult Run(GameState s, GameCommand c, IClock clock) => CommandDispatcher.Dispatch(s, c, clock);
        private static MoveDragonCommand Move(string uid, MoveTarget t) => new MoveDragonCommand(Rid(), uid, t);
        private static SetProductionSlotCommand Prod(string uid, bool on) => new SetProductionSlotCommand(Rid(), uid, on);

        // ---- Origin layout ----

        [Test]
        public void Origin_has_3_microhabitats_5_slots_only_meadow_open_ocean_and_sky_locked()
        {
            var (s, _) = Game();
            Assert.That(Zones.HabitatsOfZone("origin"), Is.EqualTo(new[] { "origin_meadow", "origin_pondbank", "origin_warm_rocks" }));
            Assert.That((Origin(s).Capacity, Origin(s).Unlocked), Is.EqualTo((5, true)));
            Assert.That(Origin(s).UnlockedHabitatIds, Is.EqualTo(new[] { "origin_meadow" }));
            Assert.That(s.FindZone("ocean").Unlocked || s.FindZone("sky").Unlocked, Is.False);
            Assert.That(Zones.ZoneOccupancy(s, "origin"), Is.EqualTo(1));
        }

        [Test]
        public void Microhabitats_do_not_add_capacity_or_budget()
        {
            var (s, _) = Game();
            GameState u = Unlock(s, "origin_warm_rocks", "origin_pondbank");
            Assert.That(Origin(u).Capacity, Is.EqualTo(5));
            Assert.That(Zones.CurrentProductionBudget(u), Is.EqualTo(Zones.CurrentProductionBudget(s)));
        }

        // ---- unlockHabitat ----

        [Test]
        public void UnlockHabitat_rejects_unknown_open_missing_discovery_and_locked_zone_without_touching_state()
        {
            var (s, clock) = Game();
            foreach (var (h, code) in new[] { ("nowhere", DomainErrorCode.UnknownHabitat), ("ocean_estuary", DomainErrorCode.UnknownHabitat), ("origin_meadow", DomainErrorCode.HabitatAlreadyUnlocked),
                         ("origin_warm_rocks", DomainErrorCode.UnlockRequirementMissing), ("origin_pondbank", DomainErrorCode.UnlockRequirementMissing) })
                Fail(s, new UnlockHabitatCommand(Rid(), h), clock, code);
            GameState d = Discover(s, "DISC_EMBERLING_THERMAL");
            Fail(d, new UnlockHabitatCommand(Rid(), "origin_pondbank"), clock, DomainErrorCode.UnlockRequirementMissing);
            GameState locked = d.DeepClone();
            Origin(locked).Unlocked = false;
            Assert.That(Zones.CheckUnlock(locked, "origin_warm_rocks"), Is.EqualTo(DomainErrorCode.ZoneLocked));
        }

        [Test]
        public void Unlock_is_free_keeps_capacity_and_budget_and_rejects_a_duplicate()
        {
            var (s0, clock) = Game();
            GameState s = Discover(s0, "DISC_EMBERLING_THERMAL");
            clock.Advance(30_000);
            DispatchResult r = Ok(s, new UnlockHabitatCommand(Rid(), "origin_warm_rocks"), clock);
            Assert.That(r.Events.Select(e => e.Type), Is.EqualTo(new[] { "habitatUnlocked" }), "no time settlement");
            var ev = (HabitatUnlocked)r.Events[0];
            Assert.That((ev.ZoneId, ev.HabitatId), Is.EqualTo(("origin", "origin_warm_rocks")));
            Assert.That(r.State.Player.Gold, Is.EqualTo(s.Player.Gold));
            Assert.That(r.State.Clock.LastProcessedAtMs, Is.EqualTo(s.Clock.LastProcessedAtMs));
            Assert.That(Origin(r.State).UnlockedHabitatIds, Is.EqualTo(new[] { "origin_meadow", "origin_warm_rocks" }));
            Assert.That(Origin(r.State).Capacity, Is.EqualTo(5));
            Fail(r.State, new UnlockHabitatCommand(Rid(), "origin_warm_rocks"), clock, DomainErrorCode.HabitatAlreadyUnlocked);
        }

        [Test]
        public void Pondbank_does_not_require_owning_a_Droplet_and_unlock_order_does_not_matter()
        {
            var (s, clock) = Game();
            Assert.That(s.Dragons.Any(d => d.SpeciesId == "droplet"), Is.False);
            DispatchResult r = Ok(Discover(s, "DISC_AQUATIC_HABITAT"), new UnlockHabitatCommand(Rid(), "origin_pondbank"), clock);
            Assert.That(Origin(r.State).UnlockedHabitatIds, Is.EqualTo(new[] { "origin_meadow", "origin_pondbank" }));
            GameState a = Unlock(Game().state, "origin_pondbank", "origin_warm_rocks");
            GameState b = Unlock(Game().state, "origin_warm_rocks", "origin_pondbank");
            Assert.That(Origin(a).UnlockedHabitatIds, Is.EqualTo(Origin(b).UnlockedHabitatIds));
            Assert.That(a.Discoveries, Is.EqualTo(new[] { "DISC_AQUATIC_HABITAT", "DISC_EMBERLING_THERMAL" }));
        }

        [Test]
        public void RecordDiscovery_is_duplicate_safe_and_self_declaring_commands_do_not_exist()
        {
            var (s, _) = Game();
            GameState d = Discover(s, "DISC_AQUATIC_HABITAT");
            Assert.That(Zones.RecordDiscovery(d.DeepClone(), "DISC_AQUATIC_HABITAT", new List<DomainEvent>()), Is.EqualTo(DomainErrorCode.AlreadyThere));
            // Không có kiểu lệnh nào để UI tự khai báo khám phá/hoàn thành: 13 lệnh công khai của bản TS + 4 lệnh mới của U2.5 + 6 lệnh của U2.7 + 3 lệnh của U2.8.
            var commandTypes = typeof(GameCommand).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(GameCommand)) && !t.IsAbstract).Select(t => t.Name).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            Assert.That(commandTypes, Is.EqualTo(new[] { "AdvanceTimeCommand", "BulkSellCommand", "BuyDecorationCommand", "BuyPackCommand", "BuyTutorialPackCommand", "BuyUpgradeCommand",
                "ClaimBreedCommand", "ClaimExpeditionCommand", "ClaimPackCommand", "ConsumeFoodCommand", "FeedCommand", "LockDragonCommand", "MoveDragonCommand", "PetCommand", "RenameDragonCommand",
                "SellDragonCommand", "SetProductionSlotCommand", "SetTutorialStatusCommand", "StartBreedCommand", "StartExpeditionCommand", "SurveyHabitatCommand", "TapEggCommand",
                "ToggleDecorationCommand", "UnlockHabitatCommand", "UnlockZoneCommand", "ViewBiologyProfileCommand" }));
        }

        [Test]
        public void HabitatUnlockStatus_reports_eligibility_for_the_UI()
        {
            GameState s = Discover(Game().state, "DISC_EMBERLING_THERMAL");
            var st = Zones.HabitatUnlockStatus(s).Select(u => u.HabitatId + ":" + u.Unlocked + ":" + (u.Requires ?? "-") + ":" + u.Eligible).ToArray();
            Assert.That(st, Is.EqualTo(new[] { "origin_meadow:True:-:False", "origin_pondbank:False:DISC_AQUATIC_HABITAT:False", "origin_warm_rocks:False:DISC_EMBERLING_THERMAL:True" }));
        }

        [Test]
        public void Discoveries_and_unlocked_habitats_are_validated_unknown_or_duplicate_ids_rejected()
        {
            GameState s = Unlock(Game().state, "origin_warm_rocks");
            Assert.That(GameStateValidator.Validate(s).IsValid, Is.True);
            GameState fake = s.DeepClone(); fake.Discoveries = new List<string> { "DISC_FAKE" };
            Assert.That(GameStateValidator.Validate(fake).IsValid, Is.False);
            GameState dup = s.DeepClone(); dup.Discoveries = new List<string> { "DISC_EMBERLING_THERMAL", "DISC_EMBERLING_THERMAL" };
            Assert.That(GameStateValidator.Validate(dup).IsValid, Is.False);
        }

        [Test]
        public void After_unlocking_Emberling_moves_to_warm_rocks_keeping_gold_Droplet_fits_pondbank_not_meadow()
        {
            var (s0, clock) = Game();
            GameState s = With(Unlock(s0, "origin_warm_rocks", "origin_pondbank"), Adult("drop", "droplet"));
            clock.Advance(100_000);
            DispatchResult moved = Ok(s, Move(First(s), MoveTarget.Zone("origin", "origin_warm_rocks")), clock);
            Assert.That(moved.State.Player.TotalGoldEarned.Micro - s.Player.TotalGoldEarned.Micro, Is.EqualTo(1_000_000));
            Assert.That(Placement.IsProducing(moved.State, moved.State.Dragons[0]), Is.True);
            Fail(moved.State, Move("drop", Meadow), clock, DomainErrorCode.HabitatUnsuitable);
            DispatchResult placed = Ok(moved.State, Move("drop", MoveTarget.Zone("origin", "origin_pondbank")), clock);
            Assert.That(Zones.ZoneOccupancy(placed.State, "origin"), Is.EqualTo(2));
        }

        // ---- placementOptions ----

        [Test]
        public void PlacementOptions_explains_every_target_for_Emberling_at_the_start()
        {
            var (s, _) = Game();
            var opts = Zones.PlacementOptions(s, s.Dragons[0]);
            var by = opts.ToDictionary(o => o.HabitatId, o => o.Blocked?.ToWire());
            Assert.That(by, Is.EqualTo(new Dictionary<string, string>
            {
                ["origin_meadow"] = "already-there", ["origin_pondbank"] = "habitat-locked", ["origin_warm_rocks"] = "habitat-locked",
                ["ocean_estuary"] = "zone-locked", ["ocean_reef"] = "zone-locked", ["ocean_cool_pool"] = "zone-locked", ["sky_low_island"] = "zone-locked",
            }));
            PlacementCheck m = opts.Single(o => o.HabitatId == "origin_meadow");
            Assert.That((m.Tier, m.FreeSlots), Is.EqualTo((SuitabilityTier.Good, 5)));
        }

        [Test]
        public void Suitability_is_checked_before_capacity_with_the_same_rule_as_the_command()
        {
            GameState s = Unlock(Game().state, "origin_pondbank", "origin_warm_rocks");
            DragonState drop = Adult("drop", "droplet");
            var opts = Zones.PlacementOptions(With(s, drop), drop);
            Assert.That(opts.Single(o => o.HabitatId == "origin_meadow").Blocked, Is.EqualTo(DomainErrorCode.HabitatUnsuitable));
            Assert.That(opts.Single(o => o.HabitatId == "origin_pondbank").Blocked, Is.Null);
        }

        [Test]
        public void The_four_G1_Commons_have_a_good_home_in_Origin_and_land_dragons_never_fly()
        {
            Assert.That(Suitability.ForSpecies("emberling", "origin_meadow").Score, Is.GreaterThanOrEqualTo(80));
            Assert.That(Suitability.ForSpecies("sprout", "origin_meadow").Score, Is.GreaterThanOrEqualTo(80));
            Assert.That(Suitability.ForSpecies("droplet", "origin_pondbank").Score, Is.GreaterThanOrEqualTo(80));
            Assert.That(Suitability.ForSpecies("zephyr", "origin_meadow").Score, Is.GreaterThanOrEqualTo(80));
            Assert.That(Suitability.ForSpecies("droplet", "origin_meadow").Allowed, Is.False);
            Assert.That(Suitability.ForSpecies("emberling", "sky_low_island").Allowed, Is.False);
        }

        // ---- moveDragon ----

        [Test]
        public void Move_within_Origin_after_unlock_keeps_production_settles_time_first_and_validates()
        {
            var (s0, clock) = Game();
            GameState s = Unlock(s0, "origin_warm_rocks");
            clock.Advance(10_000);
            DispatchResult r = Ok(s, Move(First(s), MoveTarget.Zone("origin", "origin_warm_rocks")), clock);
            DragonState d = r.State.Dragons[0];
            Assert.That((d.Location.Kind, d.Location.ZoneId, d.Location.HabitatId), Is.EqualTo((LocationKind.Zone, "origin", "origin_warm_rocks")));
            Assert.That(r.State.ProductionDragonIds, Is.EqualTo(new[] { d.Uid }));
            Assert.That(Placement.IsProducing(r.State, d), Is.True);
            Assert.That(r.Events.Select(e => e.Type), Is.EqualTo(new[] { "timeAdvanced", "dragonMoved" }));
            var mv = (DragonMoved)r.Events[1];
            Assert.That((mv.From.HabitatId, mv.To.HabitatId), Is.EqualTo(("origin_meadow", "origin_warm_rocks")));
        }

        [Test]
        public void Move_rejects_locked_unsuitable_same_spot_and_unknown_without_changing_state_or_clock()
        {
            var (s0, clock) = Game();
            GameState s = With(s0, Adult("drop", "droplet"));
            clock.Advance(5_000);
            Fail(s, Move(First(s), MoveTarget.Zone("origin", "origin_warm_rocks")), clock, DomainErrorCode.HabitatLocked);
            Fail(s, Move(First(s), MoveTarget.Zone("ocean", "ocean_estuary")), clock, DomainErrorCode.ZoneLocked);
            Fail(s, Move(First(s), MoveTarget.Zone("origin", "ocean_estuary")), clock, DomainErrorCode.HabitatLocked);
            Fail(s, Move("drop", Meadow), clock, DomainErrorCode.HabitatUnsuitable);
            Fail(s, Move(First(s), Meadow), clock, DomainErrorCode.AlreadyThere);
            Fail(s, Move("drop", MoveTarget.Storage), clock, DomainErrorCode.AlreadyThere);
            Fail(s, Move("ghost", Meadow), clock, DomainErrorCode.UnknownDragon);
        }

        [Test]
        public void A_full_zone_blocks_a_sixth_dragon_but_allows_moves_inside_the_zone()
        {
            var (s0, clock) = Game();
            GameState s = Unlock(s0, "origin_warm_rocks");
            s = With(s, Adult("a", "emberling", d => d.Location = DragonLocation.Zone("origin", "origin_meadow")), Adult("b", "emberling", d => d.Location = DragonLocation.Zone("origin", "origin_meadow")),
                Adult("c", "emberling", d => d.Location = DragonLocation.Zone("origin", "origin_meadow")), Adult("d", "emberling", d => d.Location = DragonLocation.Zone("origin", "origin_meadow")), Adult("sixth", "sprout"));
            Assert.That(Zones.ZoneOccupancy(s, "origin"), Is.EqualTo(5));
            Fail(s, Move("sixth", Meadow), clock, DomainErrorCode.ZoneFull);
            Ok(s, Move("a", MoveTarget.Zone("origin", "origin_warm_rocks")), clock);
            GameState freed = Ok(s, Move("a", MoveTarget.Storage), clock).State;
            Ok(freed, Move("sixth", Meadow), clock);
        }

        [Test]
        public void Busy_cared_for_sheltered_and_released_dragons_cannot_be_moved()
        {
            var (s0, clock) = Game();
            GameState s = With(s0, Adult("busy", "sprout", d => d.Activity = DragonActivity.Breeding),
                Adult("care", "sprout", d => { d.Location = DragonLocation.Care("case-1"); d.Activity = DragonActivity.Care; }),
                Adult("shelter", "sprout", d => d.Location = DragonLocation.Shelter("origin")), Adult("gone", "sprout", d => d.Location = DragonLocation.Released));
            foreach (string uid in new[] { "busy", "care", "shelter", "gone" })
            {
                Fail(s, Move(uid, Meadow), clock, DomainErrorCode.DragonUnavailable);
                Fail(s, Move(uid, MoveTarget.Storage), clock, DomainErrorCode.DragonUnavailable);
            }
        }

        [Test]
        public void Moving_to_storage_leaves_production_keeps_gold_up_to_the_move_and_the_dragon_still_grows()
        {
            var (s, clock) = Game();
            clock.Advance(100_000);
            DispatchResult r = Ok(s, Move(First(s), MoveTarget.Storage), clock);
            Assert.That(r.State.Player.Gold.Micro - s.Player.Gold.Micro, Is.EqualTo(1_000_000), "100 s baby Emberling: 0.025 × 0.4 G/s");
            Assert.That(r.State.ProductionDragonIds, Is.Empty);
            Assert.That(r.Events.Select(e => e.Type), Is.EqualTo(new[] { "timeAdvanced", "dragonMoved", "productionChanged" }));
            clock.Advance(100_000);
            DispatchResult later = Ok(r.State, Online(), clock);
            Assert.That(later.State.Player.Gold, Is.EqualTo(r.State.Player.Gold));
            Assert.That(later.State.Dragons[0].XpSec, Is.EqualTo(200));
        }

        [Test]
        public void Move_is_idempotent_by_requestId()
        {
            var (s, clock) = Game();
            var cmd = new MoveDragonCommand("same-move", First(s), MoveTarget.Storage);
            DispatchResult a = Run(s, cmd, clock);
            DispatchResult b = Run(a.State, cmd, clock);
            Assert.That((b.Ok, b.Duplicate), Is.EqualTo((true, true)));
            Assert.That(b.State, Is.SameAs(a.State));
        }

        [Test]
        public void Malformed_move_targets_are_invalid_command()
        {
            var (s, clock) = Game();
            foreach (MoveTarget t in new[] { null, MoveTarget.Raw(LocationKind.Zone, null, null), MoveTarget.Raw(LocationKind.Zone, "origin", null), MoveTarget.Raw(LocationKind.Shelter, "origin", null),
                         MoveTarget.Raw(LocationKind.Released, null, null), MoveTarget.Raw(LocationKind.Zone, "", "origin_meadow"), MoveTarget.Raw((LocationKind)42, null, null) })
                Fail(s, new MoveDragonCommand(Rid(), First(s), t), clock, DomainErrorCode.InvalidCommand);
            Fail(s, new UnlockHabitatCommand(Rid(), ""), clock, DomainErrorCode.InvalidCommand);
            Fail(s, new SurveyHabitatCommand(Rid(), null), clock, DomainErrorCode.InvalidCommand);
            Fail(s, new ViewBiologyProfileCommand(Rid(), new string('x', 65)), clock, DomainErrorCode.InvalidCommand);
            Fail(s, new SetProductionSlotCommand(Rid(), "", true), clock, DomainErrorCode.InvalidCommand);
        }

        // ---- setProductionSlot ----

        [Test]
        public void Production_toggles_off_and_on_only_for_placed_dragons()
        {
            var (s, clock) = Game();
            DispatchResult off = Ok(s, Prod(First(s), false), clock);
            Assert.That(off.State.ProductionDragonIds, Is.Empty);
            Fail(off.State, Prod(First(s), false), clock, DomainErrorCode.AlreadyThere);
            DispatchResult on = Ok(off.State, Prod(First(s), true), clock);
            Assert.That(on.State.ProductionDragonIds, Is.EqualTo(new[] { First(s) }));
            Fail(on.State, Prod(First(s), true), clock, DomainErrorCode.AlreadyThere);
            Fail(With(s, Adult("st", "sprout")), Prod("st", true), clock, DomainErrorCode.NotPlaced);
        }

        [Test]
        public void Gold_stops_at_the_moment_production_is_turned_off()
        {
            var (s, clock) = Game();
            clock.Advance(50_000);
            DispatchResult off = Ok(s, Prod(First(s), false), clock);
            Assert.That(off.State.Player.Gold.Micro - s.Player.Gold.Micro, Is.EqualTo(500_000));
            Assert.That(off.Events.Select(e => e.Type), Is.EqualTo(new[] { "timeAdvanced", "productionChanged", "tutorialStepCompleted" }), "50 s with Emberling in the meadow also completes TUT_01");
            clock.Advance(50_000);
            Assert.That(Ok(off.State, Online(), clock).State.Player.Gold, Is.EqualTo(off.State.Player.Gold));
        }

        [Test]
        public void Global_budget_spans_zones_and_rises_by_2_per_capacity_level_up_to_25()
        {
            var (s0, clock) = Game();
            GameState s = s0.DeepClone();
            s.FindZone("ocean").Unlocked = true;
            s.FindZone("ocean").UnlockedHabitatIds = new List<string> { "ocean_estuary" };
            s = With(s, Adult("m1", "emberling", d => d.Location = DragonLocation.Zone("origin", "origin_meadow")), Adult("m2", "emberling", d => d.Location = DragonLocation.Zone("origin", "origin_meadow")),
                Adult("m3", "emberling", d => d.Location = DragonLocation.Zone("origin", "origin_meadow")), Adult("m4", "emberling", d => d.Location = DragonLocation.Zone("origin", "origin_meadow")),
                Adult("e1", "droplet", d => d.Location = DragonLocation.Zone("ocean", "ocean_estuary")), Adult("e2", "droplet", d => d.Location = DragonLocation.Zone("ocean", "ocean_estuary")));
            foreach (string id in new[] { "m1", "m2", "m3", "m4" }) s = Ok(s, Prod(id, true), clock).State;
            Assert.That(s.ProductionDragonIds.Count, Is.EqualTo(5));
            Fail(s, Prod("e1", true), clock, DomainErrorCode.ProductionBudgetFull);
            GameState up = s.DeepClone(); up.Player.Upgrades.IslandCapacityLevel = 1;
            Assert.That(Zones.CurrentProductionBudget(up), Is.EqualTo(7));
            GameState more = Ok(Ok(up, Prod("e1", true), clock).State, Prod("e2", true), clock).State;
            Assert.That(more.ProductionDragonIds.Count, Is.EqualTo(7));
            GameState max = s.DeepClone(); max.Player.Upgrades.IslandCapacityLevel = 10;
            Assert.That(Zones.CurrentProductionBudget(max), Is.EqualTo(25));
            GameState outside = With(s, Adult("young", "sprout", d => d.XpSec = 0));
            clock.Advance(10_000);
            Assert.That(Ok(outside, Online(), clock).State.FindDragon("young").XpSec, Is.EqualTo(10));
        }

        [Test]
        public void Released_dragons_cannot_join_production()
        {
            var (s, clock) = Game();
            Fail(With(s, Adult("gone", "sprout", d => d.Location = DragonLocation.Released)), Prod("gone", true), clock, DomainErrorCode.DragonUnavailable);
        }

        // ---- surveyHabitat / viewBiologyProfile ----

        [Test]
        public void Thermal_survey_needs_an_owned_Emberling_and_its_viewed_profile()
        {
            var (s, clock) = Game();
            Fail(s, new SurveyHabitatCommand(Rid(), "origin_warm_rocks"), clock, DomainErrorCode.SurveyRequirementMissing);
            Fail(s, new UnlockHabitatCommand(Rid(), "origin_warm_rocks"), clock, DomainErrorCode.UnlockRequirementMissing);
            GameState viewed = Ok(s, new ViewBiologyProfileCommand(Rid(), "emberling"), clock).State;
            DispatchResult r = Ok(viewed, new SurveyHabitatCommand(Rid(), "origin_warm_rocks"), clock);
            Assert.That(r.Events.Select(e => e.Type), Is.EqualTo(new[] { "habitatSurveyed", "discoveryRecorded" }));
            Assert.That(((DiscoveryRecorded)r.Events[1]).DiscoveryId, Is.EqualTo("DISC_EMBERLING_THERMAL"));
            Fail(r.State, new SurveyHabitatCommand(Rid(), "origin_warm_rocks"), clock, DomainErrorCode.DiscoveryAlreadyKnown);
            // Emberling đã thả thì không còn "đang nuôi".
            GameState released = viewed.DeepClone(); released.Dragons[0].Location = DragonLocation.Released; released.ProductionDragonIds.Clear();
            Assert.That(Survey.RequirementMet(released, "DISC_EMBERLING_THERMAL"), Is.False);
        }

        [Test]
        public void Pondbank_survey_needs_no_Droplet_meadow_unknown_and_other_zone_habitats_cannot_be_surveyed()
        {
            var (s, clock) = Game();
            Assert.That(Ok(s, new SurveyHabitatCommand(Rid(), "origin_pondbank"), clock).State.Discoveries, Is.EqualTo(new[] { "DISC_AQUATIC_HABITAT" }));
            foreach (string h in new[] { "origin_meadow", "ocean_reef", "nope" }) Fail(s, new SurveyHabitatCommand(Rid(), h), clock, DomainErrorCode.UnknownHabitat);
        }

        [Test]
        public void Full_discovery_path_works_without_the_tutorial()
        {
            var (s, clock) = Game();
            s = Ok(s, new ViewBiologyProfileCommand(Rid(), "emberling"), clock).State;
            s = Ok(s, new SurveyHabitatCommand(Rid(), "origin_warm_rocks"), clock).State;
            s = Ok(s, new UnlockHabitatCommand(Rid(), "origin_warm_rocks"), clock).State;
            s = Ok(s, new SurveyHabitatCommand(Rid(), "origin_pondbank"), clock).State;
            s = Ok(s, new UnlockHabitatCommand(Rid(), "origin_pondbank"), clock).State;
            Assert.That(Origin(s).UnlockedHabitatIds, Is.EqualTo(new[] { "origin_meadow", "origin_pondbank", "origin_warm_rocks" }));
            Assert.That(Zones.HabitatUnlockStatus(s).All(u => u.Unlocked && !u.Eligible), Is.True);
        }

        [Test]
        public void ViewBiologyProfile_only_for_kept_species_and_viewing_twice_is_a_no_op_success()
        {
            var (s, clock) = Game();
            Fail(s, new ViewBiologyProfileCommand(Rid(), "droplet"), clock, DomainErrorCode.UnknownDragon);
            Fail(s, new ViewBiologyProfileCommand(Rid(), "dragonite"), clock, DomainErrorCode.UnknownDragon);
            DispatchResult a = Ok(s, new ViewBiologyProfileCommand(Rid(), "emberling"), clock);
            Assert.That(a.Events.OfType<BiologyProfileViewed>().Single().SpeciesId, Is.EqualTo("emberling"));
            DispatchResult b = Ok(a.State, new ViewBiologyProfileCommand(Rid(), "emberling"), clock);
            Assert.That(b.State.Tutorial.ProfilesViewed, Is.EqualTo(new[] { "emberling" }));
            Assert.That(b.Events, Is.Empty);
            Assert.That(b.State.Revision, Is.EqualTo(a.State.Revision + 1), "still a processed command, like TypeScript");
            GameState two = With(b.State, Adult("dr", "droplet"));
            Assert.That(Ok(two, new ViewBiologyProfileCommand(Rid(), "droplet"), clock).State.Tutorial.ProfilesViewed, Is.EqualTo(new[] { "droplet", "emberling" }), "kept sorted");
        }

        [Test]
        public void World_graph_routes_follow_locomotion()
        {
            Assert.That(WorldGraph.CanTravel("origin", "origin", Array.Empty<Locomotion>()), Is.True);
            Assert.That(WorldGraph.CanTravel("origin", "ocean", new[] { Locomotion.Swim }), Is.True);
            Assert.That(WorldGraph.CanTravel("origin", "ocean", new[] { Locomotion.Walk, Locomotion.Fly }), Is.False);
            Assert.That(WorldGraph.CanTravel("ocean", "sky", new[] { Locomotion.Swim }), Is.False);
            Assert.That(WorldGraph.CanTravel("ocean", "sky", new[] { Locomotion.Swim, Locomotion.Fly }), Is.True, "via Origin");
            Assert.That(WorldGraph.Edges.Any(e => (e.A == "ocean" && e.B == "sky") || (e.A == "sky" && e.B == "ocean")), Is.False, "GDD names no direct ocean–sky edge");
            Assert.That(Suitability.ForSpecies("zephyr", "sky_low_island").Allowed && HabitatCatalog.TryGet("sky_low_island", out HabitatProfile sky) && sky.Perch, Is.True);
        }
    }
}
