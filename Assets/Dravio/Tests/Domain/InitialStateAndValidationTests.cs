using System;
using System.Collections.Generic;
using System.Linq;
using Dravio.Domain.Biology;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Tests.Support;
using NUnit.Framework;
using static Dravio.Domain.Tests.Support.Fixtures;

namespace Dravio.Domain.Tests
{
    /// <summary>
    /// Port của "initial state (S01 Given)" và "runtime validation rejects bad saves" (gameState.test.ts) cùng
    /// "runtime validation: P0 world invariants" (g1Fixes.test.ts).
    /// </summary>
    public sealed class InitialStateAndValidationTests
    {
        private static IReadOnlyList<string> ErrorsOf(GameState s) => GameStateValidator.Validate(s).Errors;

        [Test]
        public void Initial_state_is_valid_150G_one_baby_Emberling_in_meadow_in_production_Normal()
        {
            var (state, _) = NewGame();
            Assert.That(ErrorsOf(state), Is.Empty);
            Assert.That(state.Player.Gold, Is.EqualTo(InitialStateFactory.StartingGold));
            Assert.That(state.Player.Gold.Micro, Is.EqualTo(150_000_000));
            Assert.That(state.Dragons.Count, Is.EqualTo(1));
            DragonState d = Ember(state);
            Assert.That(d.SpeciesId, Is.EqualTo("emberling"));
            Assert.That(d.XpSec, Is.EqualTo(0));
            Assert.That(Lifecycle.StageOf(d), Is.EqualTo(GrowthStage.Baby));
            Assert.That(d.Mutation, Is.EqualTo(MutationType.Normal));
            Assert.That((d.Location.Kind, d.Location.ZoneId, d.Location.HabitatId), Is.EqualTo((LocationKind.Zone, "origin", "origin_meadow")));
            Assert.That(state.ProductionDragonIds, Is.EqualTo(new[] { d.Uid }));
            Assert.That(state.Zones.Select(z => z.Id + ":" + z.Unlocked + ":" + z.Capacity), Is.EqualTo(new[] { "origin:True:5", "ocean:False:5", "sky:False:5" }));
            Assert.That(state.SchemaVersion, Is.EqualTo(9), "U2.10: schema v9 (thêm packDaily, sanctuaryProjectsBuilt, research)");
            Assert.That(state.Expedition, Is.Null);
            Assert.That(state.Codex.Select(e => e.SpeciesId + ":" + string.Join(",", e.DiscoveredMutations) + ":" + e.TotalHatched), Is.EqualTo(new[] { "emberling:Normal:0" }), "Emberling khởi đầu là khám phá, không phải lần nở");
            Assert.That(state.BreedingNest, Is.Null);
            Assert.That(state.ContentVersion, Is.EqualTo("g0-1.0"));
            Assert.That(state.Tutorial.Status, Is.EqualTo(TutorialStatus.Active));
            Assert.That(d.Uid, Does.Match("^d_[0-9a-f]{16}$"));
        }

        [Test]
        public void Initial_state_is_deterministic_for_the_same_seed()
        {
            string a = MiniJson.Diff(StateJson.ToJson(NewGame(101).state), StateJson.ToJson(NewGame(101).state));
            Assert.That(a, Is.Null);
            Assert.That(Ember(NewGame(101).state).Uid, Is.Not.EqualTo(Ember(NewGame(102).state).Uid));
        }

        [Test]
        public void Initial_state_consumes_exactly_five_rng_values()
        {
            var rng = new Mulberry32(101);
            InitialStateFactory.Create("s", T0, rng);
            var reference = new Mulberry32(101);
            for (int i = 0; i < 5; i++) reference.Next();
            Assert.That(rng.State, Is.EqualTo(reference.State));
        }

        private static IEnumerable<TestCaseData> BadSaves()
        {
            TestCaseData C(string name, Action<GameState> mut) => new TestCaseData(mut).SetName("Rejects_" + name);
            yield return C("NaN_xp", s => Ember(s).XpSec = double.NaN);
            yield return C("infinite_xp", s => Ember(s).XpSec = double.PositiveInfinity);
            yield return C("xp_above_maturity", s => Ember(s).XpSec = 301);
            yield return C("negative_gold", s => s.Player.Gold = MicroGold.FromMicro(-1));
            yield return C("gold_beyond_safe_integer", s => s.Player.Gold = MicroGold.FromMicro(JsCompat.MaxSafeInteger + 1));
            yield return C("unknown_species", s => Ember(s).SpeciesId = "dragonite");
            yield return C("invalid_allele", s => Ember(s).Genotype.Set("pigment", "Q", "r"));
            yield return C("unnormalized_allele_order", s => Ember(s).Genotype.Set("pigment", "r", "R"));
            yield return C("duplicate_UID", s => s.Dragons.Add(Ember(s).Clone()));
            yield return C("unknown_production_UID", s => s.ProductionDragonIds.Add("ghost"));
            yield return C("habitat_not_in_zone", s => Ember(s).Location = DragonLocation.Zone("origin", "ocean_reef"));
            yield return C("unknown_habitat", s => Ember(s).Location = DragonLocation.Zone("origin", "moon"));
            yield return C("bond_out_of_range", s => Ember(s).Bond = 101);
            yield return C("unknown_personality", s => Ember(s).Personality = (Personality)99);
            yield return C("upgrade_above_max", s => s.Player.Upgrades.HatchSpeedLevel = 7);
            yield return C("clock_high_water_behind", s => s.Clock = new ClockState(s.Clock.LastProcessedAtMs, s.Clock.LastProcessedAtMs - 1));
            yield return C("remainder_out_of_range", s => s.Player.GoldRemainder = 4000);
            yield return C("pity_out_of_range", s => s.Player.PityCount = 200);
            yield return C("bond_daily_above_cap", s => Ember(s).BondDaily = 11);
            yield return C("bad_bond_day", s => Ember(s).BondDay = "24/11/2026");
            yield return C("null_location", s => Ember(s).Location = null);
            yield return C("care_without_case", s => Ember(s).Location = DragonLocation.Care(""));
            yield return C("parentIds_wrong_length", s => Ember(s).ParentIds = new[] { "a" });
            yield return C("tutorial_out_of_order", s => s.Tutorial.Completed = new List<string> { "TUT_02" });
            yield return C("claimed_not_unlocked", s => s.ClaimedAchievements = new List<string> { "ACH_FIRST_ADULT" });
            yield return C("unknown_discovery", s => s.Discoveries = new List<string> { "DISC_X" });
            yield return C("bad_settings_volume", s => s.Player.Settings.SfxVolume = 1.5);
            yield return C("bad_fps", s => s.Player.Settings.FpsLimit = 45);
            yield return C("offline_receipt_reversed", s => s.OfflineReceipt = new OfflineReceipt { Id = "o", FromMs = 10, ToMs = 5, MaturedIds = new List<string>() });
            yield return C("null_player", s => s.Player = null);
        }

        [TestCaseSource(nameof(BadSaves))]
        public void Runtime_validation_rejects_bad_saves(Action<GameState> mutate)
        {
            GameState s = NewGame().state;
            mutate(s);
            Assert.That(GameStateValidator.Validate(s).IsValid, Is.False);
        }

        [Test]
        public void Validation_messages_use_TypeScript_paths()
        {
            GameState s = NewGame().state;
            Ember(s).XpSec = double.NaN;
            s.Player.Gold = MicroGold.FromMicro(-1);
            Assert.That(ErrorsOf(s), Is.EqualTo(new[] { "goldMicro: must be a non-negative integer", "dragons[0].xpSec: out of range" }));
            Assert.That(GameStateValidator.Validate(null).Errors, Is.EqualTo(new[] { "state: not an object" }));
        }

        // ---- g1Fixes.test.ts: P0 world invariants (seed 4, T0 = 2026-11-25 09:00 UTC) ----

        private static readonly long FixT0 = new DateTimeOffset(2026, 11, 25, 9, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        private static GameState Fresh() => InitialStateFactory.Create("fix", FixT0, Rng.Create(4));
        private static ZoneState Origin(GameState s) => s.FindZone("origin");

        [Test]
        public void Zone_capacity_must_be_exactly_5_in_P0()
        {
            GameState s = Fresh();
            s.FindZone("ocean").Capacity = 6;
            Assert.That(ErrorsOf(s).Any(e => e.Contains("capacity")), Is.True);
            Assert.That(WorldCatalog.P0ZoneCapacity, Is.EqualTo(5));
        }

        [Test]
        public void Locked_zone_has_no_habitats_origin_keeps_meadow_no_duplicates()
        {
            GameState a = Fresh();
            a.FindZone("ocean").UnlockedHabitatIds = new List<string> { "ocean_estuary" };
            Assert.That(ErrorsOf(a).Any(e => e.Contains("locked zone has unlocked habitats")), Is.True);
            GameState b = Fresh();
            Origin(b).UnlockedHabitatIds = new List<string>();
            Assert.That(ErrorsOf(b).Any(e => e.Contains("origin_meadow must be unlocked")), Is.True);
            GameState c = Fresh();
            Origin(c).UnlockedHabitatIds = new List<string> { "origin_meadow", "origin_meadow" };
            Assert.That(ErrorsOf(c).Any(e => e.Contains("duplicates")), Is.True);
        }

        [Test]
        public void Non_default_habitats_need_their_discovery()
        {
            GameState s = Fresh();
            Origin(s).UnlockedHabitatIds = new List<string> { "origin_meadow", "origin_warm_rocks" };
            Assert.That(ErrorsOf(s).Any(e => e.Contains("origin_warm_rocks requires DISC_EMBERLING_THERMAL")), Is.True);
            s.Discoveries = new List<string> { "DISC_EMBERLING_THERMAL" };
            Assert.That(ErrorsOf(s), Is.Empty);
        }

        [Test]
        public void Placed_dragon_must_be_in_unlocked_zone_and_habitat_and_biologically_allowed()
        {
            GameState locked = Fresh();
            locked.Dragons.Add(Adult("x1", "emberling", d => d.Location = DragonLocation.Zone("origin", "origin_warm_rocks")));
            Assert.That(ErrorsOf(locked).Any(e => e.Contains("habitat origin_warm_rocks is locked")), Is.True);

            GameState lockedZone = Fresh();
            lockedZone.Dragons.Add(Adult("x2", "droplet", d => d.Location = DragonLocation.Zone("ocean", "ocean_estuary")));
            Assert.That(ErrorsOf(lockedZone).Any(e => e.Contains("zone ocean is locked")), Is.True);

            var pair = SpeciesCatalog.All.SelectMany(sp => new[] { "origin_meadow", "origin_warm_rocks", "origin_pondbank" }.Select(h => (sp: sp.Id, h)))
                .First(p => !Suitability.ForSpecies(p.sp, p.h).Allowed);
            GameState bad = Fresh();
            bad.Discoveries = new List<string> { "DISC_EMBERLING_THERMAL", "DISC_AQUATIC_HABITAT" };
            Origin(bad).UnlockedHabitatIds = new List<string> { "origin_meadow", "origin_warm_rocks", "origin_pondbank" };
            bad.Dragons.Add(Adult("x3", pair.sp, d => d.Location = DragonLocation.Zone("origin", pair.h)));
            Assert.That(ErrorsOf(bad).Any(e => e.Contains("unsuitable for " + pair.sp)), Is.True);
        }

        [Test]
        public void Zone_physical_capacity_is_enforced()
        {
            GameState s = Fresh();
            for (int i = 0; i < 5; i++) s.Dragons.Add(Adult("m" + i, "emberling", d => d.Location = DragonLocation.Zone("origin", "origin_meadow")));
            Assert.That(ErrorsOf(s).Any(e => e == "zones.origin: 6 dragons exceed capacity 5"), Is.True);
        }

        [Test]
        public void Production_ids_must_reference_dragons_placed_in_a_zone_within_budget()
        {
            GameState s = Fresh();
            s.Dragons.Add(Adult("st", "emberling"));
            s.ProductionDragonIds.Add("st");
            Assert.That(ErrorsOf(s).Any(e => e.Contains("dragon st is not placed in a zone")), Is.True);
            GameState t = Fresh();
            for (int i = 0; i < 5; i++) t.ProductionDragonIds.Add("p" + i);
            Assert.That(ErrorsOf(t), Does.Contain("productionDragonIds: exceeds production budget"));
        }

        [Test]
        public void Food_drop_habitat_must_belong_to_its_zone()
        {
            GameState s = Fresh();
            s.Food.Drops.Add(new FoodDrop { Id = "f1", DietId = DietId.Fruit, ZoneId = "origin", HabitatId = "ocean_reef", SpawnedAtMs = FixT0, ExpiresAtMs = FixT0 + 12_000 });
            Assert.That(ErrorsOf(s), Does.Contain("food.drops[0]: habitat not in zone"));
        }

        [Test]
        public void Validate_single_dragon()
        {
            Assert.That(GameStateValidator.ValidateDragonInstance(Adult("a", "gaia")), Is.Empty);
            Assert.That(GameStateValidator.ValidateDragonInstance(null), Is.EqualTo(new[] { "dragon: not an object" }));
        }
    }
}
