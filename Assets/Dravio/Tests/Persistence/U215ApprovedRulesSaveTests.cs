using System;
using System.Collections.Generic;
using System.Linq;
using Dravio.Domain;
using Dravio.Domain.Behavior;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Progression;
using Dravio.Domain.World;
using Dravio.Persistence.Schema;
using Dravio.Persistence.Tests.Support;
using NUnit.Framework;

namespace Dravio.Persistence.Tests
{
    /// <summary>
    /// U2.15a (issue #74) × Save Schema v10: bảng sky_cloud A (production) và luật cho ăn B không đổi schema, không migration.
    /// Mục tiêu cho ăn chỉ nằm trên phần ăn đang rơi, mà save không lưu phần ăn (ADR 0004 #10).
    /// </summary>
    public sealed class U215ApprovedRulesSaveTests
    {
        private static readonly long Day0 = new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

        private static DragonState Adult(string uid, string species, DragonLocation at)
        {
            var g = new Genotype();
            g.Set("pigment", "R", "r"); g.Set("pattern", "P", "p"); g.Set("eye", "B", "b");
            return new DragonState
            {
                Uid = uid, SpeciesId = species, Genotype = g, Mutation = MutationType.Normal, XpSec = SpeciesCatalog.Get(species).GrowthTimeSec, Location = at,
                Personality = Personality.Calm, Generation = 1, Ancestry = Ancestry.Unknown, Origin = DragonOrigin.Pack, Activity = DragonActivity.Idle,
            };
        }

        private static GameState World()
        {
            GameState s = InitialStateFactory.Create("u215-save", Day0, Rng.Create(7u));
            s.Dragons.Clear();
            s.ProductionDragonIds.Clear();
            s.Dragons.Add(Adult("flyer", "zephyr", DragonLocation.Zone("sky", "sky_low_island")));
            for (int i = 0; i < 4; i++) s.Dragons.Add(Adult("m" + i, "sprout", DragonLocation.Zone("origin", "origin_meadow")));
            foreach (ZoneState z in s.Zones) if (z.Id == "origin") z.UnlockedHabitatIds = Zones.HabitatsOfZone("origin");
            foreach (string disc in ProgressCatalog.DiscoveryIds) if (!s.Discoveries.Contains(disc)) s.Discoveries.Add(disc);
            s.Discoveries.Sort(StringComparer.Ordinal);
            WorldExpansion.ApplyZoneUnlock(s, "ocean", null);
            WorldExpansion.ApplyZoneUnlock(s, "sky", null);
            s.Tutorial.Status = TutorialStatus.Completed;
            s.Tutorial.Completed = new List<string>(ProgressCatalog.TutorialStepIds);
            s.Clock = new ClockState(Day0, Day0);
            Codex.Observe(s, null);
            return s;
        }

        private static GameState Do(GameState s, GameCommand c, long now)
        {
            DispatchResult r = CommandDispatcher.Dispatch(s, c, new FixedClock(now));
            Assert.That(r.Ok, Is.True, c.Type + ": " + r.Error);
            return r.State;
        }

        private static GameState Fly(GameState s, ref long now, int trips)
        {
            for (int i = 0; i < trips; i++)
            {
                s = Do(s, new StartExpeditionCommand(PFix.Rid(), SkyCloudBalance.RouteId, "flyer"), now);
                now = s.Expedition.FinishAtMs;
                s = Do(s, new ClaimExpeditionCommand(PFix.Rid()), now);
            }
            return s;
        }

        [Test]
        public void Targeted_food_in_flight_is_not_persisted_and_the_save_stays_schema_v10()
        {
            GameState s = World();
            s = Do(s, new FeedCommand(PFix.Rid(), SpeciesCatalog.GetBiology("sprout").DietIds[0], "origin", "origin_meadow", "m3"), Day0);
            Assert.That(s.Food.Drops.Single().TargetDragonUid, Is.EqualTo("m3"));
            string text = SaveSchema.Serialize(s, PFix.T0);
            Assert.That(text, Does.Not.Contain("targetDragonUid"));
            ParseResult r = SaveFile.Parse(text);
            Assert.That(r.Ok, Is.True, r.ToString());
            GameState back = r.Save.State;
            Assert.That(back.SchemaVersion, Is.EqualTo(10));
            Assert.That(back.Food.Drops, Is.Empty, "phần ăn đang rơi không vào save (ADR 0004 #10)");
            Assert.That(PFix.Canonical(back), Is.EqualTo(PFix.Canonical(SaveSchema.ToPersistedState(s))));
            // Sau khi nạp: cho ăn lại, ưu tiên vẫn chạy.
            back = Do(back, new FeedCommand(PFix.Rid(), SpeciesCatalog.GetBiology("sprout").DietIds[0], "origin", "origin_meadow", "m3"), back.Clock.HighWaterAtMs + 1_000);
            FoodAssignment a = BehaviorEngine.AssignmentOf(back, back.Food.Drops.Single().Id, back.Clock.HighWaterAtMs + 1);
            Assert.That((a.AssignedDragonUid, a.Source), Is.EqualTo(("m3", FoodAssignmentSource.Target)));
        }

        [Test]
        public void Old_v10_save_from_before_sky_A_continues_and_gets_the_egg_on_the_15th_production_trip()
        {
            // Save "U2.14": 6 chuyến rừng, không bụi sao, không bộ đếm sky_cloud.
            GameState s = World();
            Assert.That(SkyCloudBalance.Current, Is.SameAs(SkyCloudBalance.ProposedA));
            string old;
            using (SkyCloudBalance.Use(SkyCloudBalance.Disabled)) old = SaveSchema.Serialize(s, PFix.T0);
            Assert.That(old, Does.Not.Contain("stardust"));
            ParseResult r = SaveFile.Parse(old);
            Assert.That(r.Ok, Is.True, r.ToString());
            GameState back = r.Save.State;
            long now = back.Clock.HighWaterAtMs + 1_000;
            GameState end = Fly(back, ref now, 15);
            Assert.That(end.GrantedRewards.Count(id => id == SkyCloudBalance.AurorawingEggRewardId), Is.EqualTo(1));
            Assert.That(end.PendingPack?.SpeciesId, Is.EqualTo("aurorawing"));
            Assert.That(end.Player.Inventory["stardust"], Is.GreaterThanOrEqualTo(15 * 2 + 2 + 3 + 4));
            // Lưu / nạp sau mốc: không trao lại.
            GameState again = SaveFile.Parse(SaveSchema.Serialize(end, PFix.T0)).Save.State;
            Assert.That(again.SchemaVersion, Is.EqualTo(10));
            Assert.That(PFix.Canonical(again), Is.EqualTo(PFix.Canonical(SaveSchema.ToPersistedState(end))));
            long t = again.Clock.HighWaterAtMs + 1_000;
            GameState more = Fly(again, ref t, 3);
            Assert.That(more.GrantedRewards.Count(id => id == SkyCloudBalance.AurorawingEggRewardId), Is.EqualTo(1));
        }

        [Test]
        public void Save_midway_through_sky_trips_and_reload_matches_an_uninterrupted_run()
        {
            GameState a = World();
            long na = Day0;
            GameState straight = Fly(a, ref na, 20);
            GameState b = World();
            long nb = Day0;
            GameState half = Fly(b, ref nb, 10);
            GameState reloaded = SaveFile.Parse(SaveSchema.Serialize(half, PFix.T0)).Save.State;
            GameState rest = Fly(reloaded, ref nb, 10);
            Assert.That(rest.ExpeditionsClaimed[SkyCloudBalance.RouteId], Is.EqualTo(20));
            Assert.That(rest.Player.Inventory["stardust"], Is.EqualTo(straight.Player.Inventory["stardust"]), "RNG thám hiểm theo saveId + revision (đều nằm trong save) → nạp giữa chừng cho cùng kết quả");
            Assert.That(rest.GrantedRewards, Is.EqualTo(straight.GrantedRewards));
        }
    }
}
