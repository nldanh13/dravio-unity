using System;
using System.Linq;
using Dravio.Domain;
using Dravio.Domain.Behavior;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.World;
using Dravio.Persistence.Json;
using Dravio.Persistence.Schema;
using Dravio.Persistence.Storage;
using Dravio.Persistence.Tests.Support;
using NUnit.Framework;

namespace Dravio.Persistence.Tests
{
    /// <summary>
    /// U2.10 × Save Schema v9 (ADR 0204 E1, E2, E5, R11): migration v8 → v9 không mất dữ liệu (save gốc giữ nguyên văn), giới hạn gói theo ngày,
    /// dự án Sanctuary và sổ nghiên cứu qua vòng lưu–nạp, ảnh v4 cho TS, và sổ nghiên cứu hỏng không bao giờ làm mất save.
    /// </summary>
    public sealed class EconomyResearchSaveTests
    {
        private static GameState Do(GameState s, GameCommand c, long now)
        {
            DispatchResult r = CommandDispatcher.Dispatch(s, c, new FixedClock(now));
            Assert.That(r.Ok, Is.True, c.Type + ": " + r.Error);
            return r.State;
        }

        /// <summary>Đã mua 1 gói Starter, xây 3 dự án (dự án thứ 3 tốn gỗ + hạt) và quan sát một rồng.</summary>
        private static GameState Played()
        {
            GameState s = PFix.Fresh();
            long t = PFix.T0 + 60_000;
            s.Player.Gold = MicroGold.FromWholeGold(200_000);
            s.Player.Inventory["wood"] = 3;
            s.Player.Inventory["seed"] = 3;
            s = Do(s, new BuyPackCommand(PFix.Rid(), "starter"), t);
            for (int i = 0; i < 3; i++) s = Do(s, new BuildSanctuaryProjectCommand(PFix.Rid()), t);
            s = Do(s, new ObserveDragonCommand(PFix.Rid(), s.Dragons[0].Uid, DayPhase.Day), t);
            Assert.That((s.PackDaily.StarterBought, s.SanctuaryProjectsBuilt, s.Player.Inventory["wood"], s.Player.Inventory["seed"]), Is.EqualTo((1, 3, 2L, 2L)));
            Assert.That(s.Research.IsEmpty, Is.False);
            Assert.That(GameStateValidator.Validate(s).IsValid, Is.True, GameStateValidator.Validate(s).ToString());
            return s;
        }

        [Test]
        public void V8_save_migrates_to_v9_with_empty_blocks_keeping_every_v8_field_and_the_verbatim_archive()
        {
            GameState s = PFix.Fresh();
            s.Player.Inventory["wood"] = 7;
            s.Player.Gold = MicroGold.FromWholeGold(12_345);
            s.SchemaVersion = 8;
            string v8 = PFix.EnvelopeAt(8, s);
            var v8State = (JsonObject)((JsonObject)JsonParser.Parse(v8)).Get("state");
            Assert.That(v8State.Get("packDaily"), Is.Null, "fixture v8 thật sự không có trường v9");
            Assert.That(v8State.Get("research"), Is.Null);

            var store = new MemorySaveStore();
            store.Slots[SaveSlot.Main] = v8;
            var mgr = new SaveManager(store, () => PFix.T0);
            LoadOutcome r = mgr.Load();
            Assert.That((r.Kind, r.Save.MigratedFrom), Is.EqualTo((LoadKind.Loaded, (int?)8)), r.Kind.ToString());
            GameState m = r.Save.State;
            Assert.That((m.SchemaVersion, m.PackDaily.Day, m.PackDaily.StarterBought, m.SanctuaryProjectsBuilt, m.Research.IsEmpty), Is.EqualTo((9, (string)null, 0, 0, true)));
            Assert.That((m.Player.Gold.Micro, m.Player.Inventory["wood"]), Is.EqualTo((MicroGold.FromWholeGold(12_345).Micro, 7L)));
            // Bỏ phần v9 thêm vào thì trùng từng byte với state v8 (trừ schemaVersion).
            JsonObject strippedBack = (JsonObject)((JsonObject)JsonParser.Parse(PFix.EnvelopeAt(8, m))).Get("state");
            Assert.That(SaveSchema.CanonicalJson(strippedBack), Is.EqualTo(SaveSchema.CanonicalJson(v8State)));

            Assert.That(mgr.Save(m), Is.True);
            Assert.That(store.GetArchive("archive:schema-8"), Is.EqualTo(v8), "save v8 gốc được giữ nguyên văn");
        }

        [Test]
        public void Pack_limit_projects_and_research_round_trip_byte_for_byte_and_still_project_to_TS_v4()
        {
            GameState s = Played();
            string text = SaveSchema.Serialize(s, PFix.T0);
            ParseResult r = SaveFile.Parse(text);
            Assert.That(r.Ok, Is.True, r.ToString());
            GameState back = r.Save.State;
            Assert.That(PFix.Canonical(back), Is.EqualTo(PFix.Canonical(SaveSchema.ToPersistedState(s))));
            Assert.That((back.PackDaily.Day, back.PackDaily.StarterBought, back.SanctuaryProjectsBuilt), Is.EqualTo((s.PackDaily.Day, 1, 3)));
            Assert.That(back.Research.ToSnapshot().Species.Select(e => e.SpeciesId), Is.EqualTo(s.Research.ToSnapshot().Species.Select(e => e.SpeciesId)));

            // Sau khi nạp lại, giới hạn trong ngày vẫn đếm đúng: 4 gói nữa được, gói thứ 6 bị từ chối.
            long t = PFix.T0 + 120_000;
            GameState g = back;
            for (int i = 0; i < PackCatalog.StarterDailyLimit - 1; i++)
            {
                g = Do(g, new ClaimPackCommand(PFix.Rid(), MoveTarget.Storage), t);
                g = Do(g, new BuyPackCommand(PFix.Rid(), "starter"), t);
            }
            g = Do(g, new ClaimPackCommand(PFix.Rid(), MoveTarget.Storage), t);
            DispatchResult sixth = CommandDispatcher.Dispatch(g, new BuyPackCommand(PFix.Rid(), "starter"), new FixedClock(t));
            Assert.That(sixth.Error?.Code, Is.EqualTo(DomainErrorCode.PackDailyLimit));

            string dir = Environment.GetEnvironmentVariable("DRAVIO_EXPORT_DIR");
            PFix.Export(dir, "u210-economy-research", text);
            JsonObject v4 = PFix.AsV4((JsonObject)((JsonObject)JsonParser.Parse(text)).Get("state"));
            foreach (string k in new[] { "packDaily", "sanctuaryProjectsBuilt", "research" }) Assert.That(v4.Get(k), Is.Null, k);
            Assert.That(SaveFile.Parse(PFix.AsV4Text(text)).Ok, Is.True, "ảnh v4: phần chung vẫn hợp lệ với TS");
        }

        [Test]
        public void A_damaged_research_block_is_cleaned_on_load_and_never_loses_the_save()
        {
            GameState s = Played();
            JsonObject env = (JsonObject)JsonParser.Parse(SaveSchema.Serialize(s, PFix.T0));
            JsonObject st = ((JsonObject)env.Get("state")).ShallowCopy();
            var research = (JsonObject)st.Get("research");
            var species = new JsonArray();
            foreach (JsonValue v in (JsonArray)research.Get("species")) species.Add(v);
            species.Add(new JsonObject().Set("speciesId", JsonValue.From("not-a-dragon")).Set("behaviors", new JsonArray()).Set("phases", new JsonArray())
                .Set("habitats", new JsonArray()).Set("days", new JsonArray()).Set("findingsByDay", new JsonObject()));
            species.Add(new JsonObject().Set("speciesId", JsonValue.From("emberling")).Set("behaviors", new JsonArray().Add(JsonValue.From("swim")))
                .Set("phases", new JsonArray().Add(JsonValue.From("bogus"))).Set("habitats", new JsonArray().Add(JsonValue.From("ocean_reef")))
                .Set("days", new JsonArray().Add(JsonValue.From("yesterday"))).Set("findingsByDay", new JsonObject()));
            var inds = new JsonArray();
            foreach (JsonValue v in (JsonArray)research.Get("individuals")) inds.Add(v);
            inds.Add(new JsonObject().Set("uid", JsonValue.From("sold-long-ago")).Set("speciesId", JsonValue.From("emberling")).Set("observations", JsonValue.From(3L))
                .Set("seenInFavoriteHabitat", JsonValue.From(false)).Set("seenFeeding", JsonValue.From(false)).Set("lastObservedAtMs", JsonValue.From(0L))
                .Set("activePhases", new JsonArray()).Set("restPhases", new JsonArray()).Set("revealed", new JsonArray()));
            st.Set("research", new JsonObject().Set("species", species).Set("individuals", inds));
            string tampered = JsonWriter.Stringify(env.ShallowCopy().Set("state", st).Set("checksum", JsonValue.From(SaveSchema.ChecksumOf(st))));

            ParseResult r = SaveFile.Parse(tampered);
            Assert.That(r.Ok, Is.True, "sổ nghiên cứu hỏng không làm hỏng save: " + r);
            GameState m = r.Save.State;
            Assert.That(GameStateValidator.Validate(m).IsValid, Is.True);
            ResearchLogSnapshot snap = m.Research.ToSnapshot();
            Assert.That(ResearchSchemaDraft.Validate(snap, m), Is.Empty);
            Assert.That(snap.Species.Select(e => e.SpeciesId), Does.Not.Contain("not-a-dragon"));
            Assert.That(snap.Individuals.Select(e => e.Uid), Does.Not.Contain("sold-long-ago"));
            Assert.That(snap.Species.Single(e => e.SpeciesId == "emberling").Behaviors, Does.Not.Contain("swim"));
            Assert.That((m.Player.Gold.Micro, m.SanctuaryProjectsBuilt, m.Dragons.Count), Is.EqualTo((s.Player.Gold.Micro, 3, s.Dragons.Count)), "vàng, dự án, rồng nguyên vẹn");

            // Khối research không phải object: vẫn nạp, sổ rỗng.
            st.Set("research", JsonValue.From("oops"));
            string broken = JsonWriter.Stringify(env.ShallowCopy().Set("state", st).Set("checksum", JsonValue.From(SaveSchema.ChecksumOf(st))));
            ParseResult r2 = SaveFile.Parse(broken);
            Assert.That(r2.Ok, Is.True, r2.ToString());
            Assert.That(r2.Save.State.Research.IsEmpty, Is.True);
        }

        [Test]
        public void Tampered_pack_counter_or_project_count_is_rejected()
        {
            GameState s = Played();
            JsonObject env = (JsonObject)JsonParser.Parse(SaveSchema.Serialize(s, PFix.T0));
            foreach (Func<JsonObject, JsonObject> tamper in new Func<JsonObject, JsonObject>[]
            {
                x => x.Set("packDaily", new JsonObject().Set("day", JsonValue.From("2026-11-01")).Set("starterBought", JsonValue.From(-1L))),
                x => x.Set("packDaily", new JsonObject().Set("day", JsonValue.Null).Set("starterBought", JsonValue.From(2L))),
                x => x.Set("packDaily", JsonValue.From("x")),
                x => x.Set("sanctuaryProjectsBuilt", JsonValue.From(-3L)),
                x => x.Set("sanctuaryProjectsBuilt", JsonValue.From((long)SanctuaryProjectCatalog.Max + 1)),
            })
            {
                JsonObject st = tamper(((JsonObject)env.Get("state")).ShallowCopy());
                string text = JsonWriter.Stringify(env.ShallowCopy().Set("state", st).Set("checksum", JsonValue.From(SaveSchema.ChecksumOf(st))));
                Assert.That(SaveFile.Parse(text).Ok, Is.False, JsonWriter.Stringify(st.Get("packDaily")) + " / " + JsonWriter.Stringify(st.Get("sanctuaryProjectsBuilt")));
            }
        }
    }
}
