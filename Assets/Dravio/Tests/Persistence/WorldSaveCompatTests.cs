using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dravio.Domain;
using Dravio.Domain.Core;
using Dravio.Domain.World;
using Dravio.Persistence.Schema;
using Dravio.Persistence.Storage;
using Dravio.Persistence.Tests.Support;
using Dravio.Platform;
using NUnit.Framework;

namespace Dravio.Persistence.Tests
{
    /// <summary>
    /// U2.3 × Save Schema v4: vị trí microhabitat, kho, ngân sách sản xuất, khám phá, sinh cảnh đã mở và hồ sơ sinh học đã xem
    /// sau các lệnh world phải lưu → nạp nguyên vẹn, không thêm trường mới vào schema, và được bản TypeScript đọc lại
    /// (DRAVIO_EXPORT_DIR + Tools~/verify-csharp-saves.test.ts).
    /// </summary>
    public sealed class WorldSaveCompatTests
    {
        private static GameState Play()
        {
            var clock = new FixedClock(PFix.T0);
            GameState s = PFix.Fresh(8);
            string ember = s.Dragons[0].Uid;
            GameState Do(GameState st, GameCommand c, long advance = 0)
            {
                clock.Advance(advance);
                DispatchResult r = CommandDispatcher.Dispatch(st, c, clock);
                Assert.That(r.Ok, Is.True, c.Type + ": " + r.Error);
                return r.State;
            }
            s = Do(s, new ViewBiologyProfileCommand("v1", "emberling"));
            s = Do(s, new SurveyHabitatCommand("s1", "origin_warm_rocks"), 15_000);
            s = Do(s, new UnlockHabitatCommand("u1", "origin_warm_rocks"));
            s = Do(s, new SurveyHabitatCommand("s2", "origin_pondbank"));
            s = Do(s, new UnlockHabitatCommand("u2", "origin_pondbank"));
            s = Do(s, new MoveDragonCommand("m1", ember, MoveTarget.Zone("origin", "origin_warm_rocks")), 100_000);
            s = Do(s, new SetProductionSlotCommand("p1", ember, false), 20_000);
            s = Do(s, new MoveDragonCommand("m2", ember, MoveTarget.Storage), 5_000);
            s = Do(s, new MoveDragonCommand("m3", ember, MoveTarget.Zone("origin", "origin_meadow")), 5_000);
            s = Do(s, new SetProductionSlotCommand("p2", ember, true), 1_000);
            Assert.That(s.Dragons[0].Location.HabitatId, Is.EqualTo("origin_meadow"));
            Assert.That(s.ProductionDragonIds, Is.EqualTo(new[] { ember }));
            return s;
        }

        [Test]
        public void World_state_round_trips_through_current_Save_Schema_without_extra_fields()
        {
            GameState s = Play();
            Assert.That(s.Discoveries, Is.EqualTo(new[] { "DISC_AQUATIC_HABITAT", "DISC_EMBERLING_THERMAL" }));
            Assert.That(s.FindZone("origin").UnlockedHabitatIds, Is.EqualTo(new[] { "origin_meadow", "origin_pondbank", "origin_warm_rocks" }));
            Assert.That(s.Tutorial.ProfilesViewed, Is.EqualTo(new[] { "emberling" }));
            string text = SaveSchema.Serialize(s, PFix.T0);
            ParseResult r = SaveFile.Parse(text);
            Assert.That(r.Ok, Is.True, r.ToString());
            Assert.That(PFix.Canonical(r.Save.State), Is.EqualTo(PFix.Canonical(SaveSchema.ToPersistedState(s))));
            var state = (Json.JsonObject)((Json.JsonObject)Json.JsonParser.Parse(text)).Get("state");
            Assert.That(state.Keys.OrderBy(k => k, StringComparer.Ordinal), Is.EqualTo(GameStateJson.StateKeys.OrderBy(k => k, StringComparer.Ordinal)), "schema v9 keys only (v4 + breedingNest + codex + expedition + health + U2.10)");
            Assert.That(state.Get("schemaVersion").ToString(), Is.EqualTo("9"));
            string dir = Environment.GetEnvironmentVariable("DRAVIO_EXPORT_DIR");
            PFix.Export(dir, "u23-world", text);
        }

        [Test]
        public void Commands_sent_through_a_session_are_autosaved_and_survive_a_restart()
        {
            var store = new MemorySaveStore();
            var rig = new SessionRig(store, PFix.T0);
            GameSession s = rig.NewSession();
            string ember = s.State.Dragons[0].Uid;
            Assert.That(s.Send(id => new SurveyHabitatCommand(id, "origin_pondbank")), Is.Null);
            Assert.That(s.Send(id => new UnlockHabitatCommand(id, "origin_pondbank")), Is.Null);
            Assert.That(s.Send(id => new MoveDragonCommand(id, ember, MoveTarget.Storage)), Is.Null);
            Assert.That(s.Send(id => new SetProductionSlotCommand(id, ember, true)).Code, Is.EqualTo(DomainErrorCode.NotPlaced));
            Assert.That(s.Send(id => new ViewBiologyProfileCommand(id, "emberling")), Is.Null);
            rig.Run(30_000);
            s.Dispose();
            GameSession again = rig.NewSession();
            Assert.That(again.State.Discoveries, Is.EqualTo(new[] { "DISC_AQUATIC_HABITAT" }));
            Assert.That(again.State.FindZone("origin").UnlockedHabitatIds, Is.EqualTo(new[] { "origin_meadow", "origin_pondbank" }));
            Assert.That(again.State.Dragons[0].Location.Kind, Is.EqualTo(LocationKind.Storage));
            Assert.That(again.State.ProductionDragonIds, Is.Empty);
            Assert.That(again.State.Tutorial.ProfilesViewed, Is.EqualTo(new[] { "emberling" }));
        }

        [Test]
        public void A_tampered_save_that_breaks_world_invariants_is_rejected_on_load()
        {
            GameState s = Play();
            GameState bad = s.DeepClone();
            bad.FindZone("origin").UnlockedHabitatIds = new List<string> { "origin_meadow", "origin_warm_rocks" };
            bad.Discoveries.Clear(); // sinh cảnh mở mà không có khám phá
            ParseResult r = SaveFile.Parse(SaveSchema.Serialize(bad, PFix.T0));
            Assert.That(r.Code, Is.EqualTo(SaveErrorCode.InvalidState));
            Assert.That(r.Details, Does.Contain("zones[0].unlockedHabitatIds: origin_warm_rocks requires DISC_EMBERLING_THERMAL"));
            GameState full = s.DeepClone();
            for (int i = 0; i < 5; i++)
                full.Dragons.Add(new DragonState
                {
                    Uid = "x" + i, SpeciesId = "emberling", Genotype = s.Dragons[0].Genotype.Clone(), XpSec = 0,
                    Location = DragonLocation.Zone("origin", "origin_meadow"), Personality = Personality.Calm,
                });
            ParseResult r2 = SaveFile.Parse(SaveSchema.Serialize(full, PFix.T0));
            Assert.That(r2.Details, Does.Contain("zones.origin: 6 dragons exceed capacity 5"));
        }
    }
}
