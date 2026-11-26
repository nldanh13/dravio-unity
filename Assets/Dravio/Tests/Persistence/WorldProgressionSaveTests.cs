using System;
using System.Linq;
using Dravio.Domain;
using Dravio.Domain.Behavior;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Persistence.Json;
using Dravio.Persistence.Schema;
using Dravio.Persistence.Storage;
using Dravio.Persistence.Tests.Support;
using NUnit.Framework;

namespace Dravio.Persistence.Tests
{
    /// <summary>
    /// U2.11 × Save Schema v10 (ADR 0206): migration v9 → v10 không mất dữ liệu (save v9 gốc giữ nguyên văn), nhiệm vụ phụ, bộ đếm thám hiểm và
    /// quan hệ qua vòng lưu–nạp, ảnh v4 cho TS, khối quan hệ hỏng không bao giờ làm mất save, số liệu nhiệm vụ / thám hiểm sửa tay bị từ chối.
    /// </summary>
    public sealed class WorldProgressionSaveTests
    {
        private const long Min = 60_000, Hour = 60 * Min;

        private static GameState Do(GameState s, GameCommand c, long now)
        {
            DispatchResult r = CommandDispatcher.Dispatch(s, c, new FixedClock(now));
            Assert.That(r.Ok, Is.True, c.Type + ": " + r.Error);
            return r.State;
        }

        /// <summary>Hai rồng Adult cùng đồng cỏ 3 giờ, nhận Q_ORIGIN_FOREST_SURVEY, nhận một chuyến thám hiểm rừng.</summary>
        private static GameState Played()
        {
            GameState s = PFix.Fresh().DeepClone();
            DragonState e = s.Dragons[0];
            e.XpSec = SpeciesCatalog.Get(e.SpeciesId).GrowthTimeSec;
            DragonState buddy = e.Clone();
            buddy.Uid = "buddy";
            s.Dragons.Add(buddy);
            s.Player.Inventory["wood"] = 3;
            s.Player.Inventory["seed"] = 2;
            long t = PFix.T0 + 3 * Hour;
            s = Do(s, new AdvanceTimeCommand(PFix.Rid(), AdvanceMode.Online), t);
            s = Do(s, new CompleteQuestCommand(PFix.Rid(), "Q_ORIGIN_FOREST_SURVEY"), t);
            s = Do(s, new StartExpeditionCommand(PFix.Rid(), "origin_forest", "buddy"), t);
            t = s.Expedition.FinishAtMs;
            s = Do(s, new ClaimExpeditionCommand(PFix.Rid()), t);
            Assert.That(s.CompletedSideQuests, Is.EqualTo(new[] { "Q_ORIGIN_FOREST_SURVEY" }));
            Assert.That(s.ExpeditionsClaimed["origin_forest"], Is.EqualTo(1));
            Assert.That(s.Relations.SharedMs(e.Uid, "buddy"), Is.EqualTo(3 * Hour), "chuyến đi không tính thời gian sống chung");
            Assert.That(GameStateValidator.Validate(s).IsValid, Is.True, GameStateValidator.Validate(s).ToString());
            return s;
        }

        private static string Resign(JsonObject env, JsonObject st) =>
            JsonWriter.Stringify(env.ShallowCopy().Set("state", st).Set("checksum", JsonValue.From(SaveSchema.ChecksumOf(st))));

        [Test]
        public void V9_save_migrates_to_v10_with_empty_blocks_keeping_every_v9_field_and_the_verbatim_archive()
        {
            GameState s = PFix.Fresh();
            s.Player.Inventory["wood"] = 7;
            s.Player.Gold = MicroGold.FromWholeGold(54_321);
            s.SanctuaryProjectsBuilt = 2;
            s.SchemaVersion = 9;
            string v9 = PFix.EnvelopeAt(9, s);
            var v9State = (JsonObject)((JsonObject)JsonParser.Parse(v9)).Get("state");
            Assert.That(v9State.Get("relations"), Is.Null, "fixture v9 thật sự không có trường v10");
            Assert.That(v9State.Get("sanctuaryProjectsBuilt"), Is.Not.Null);

            var store = new MemorySaveStore();
            store.Slots[SaveSlot.Main] = v9;
            var mgr = new SaveManager(store, () => PFix.T0);
            LoadOutcome r = mgr.Load();
            Assert.That((r.Kind, r.Save.MigratedFrom), Is.EqualTo((LoadKind.Loaded, (int?)9)), r.Kind.ToString());
            GameState m = r.Save.State;
            Assert.That((m.SchemaVersion, m.CompletedSideQuests.Count, m.ExpeditionsClaimed.Count, m.Relations.IsEmpty), Is.EqualTo((10, 0, 0, true)));
            Assert.That((m.Player.Gold.Micro, m.Player.Inventory["wood"], m.SanctuaryProjectsBuilt), Is.EqualTo((MicroGold.FromWholeGold(54_321).Micro, 7L, 2)));
            // Bỏ phần v10 thêm vào thì trùng từng byte với state v9 (trừ schemaVersion).
            JsonObject strippedBack = (JsonObject)((JsonObject)JsonParser.Parse(PFix.EnvelopeAt(9, m))).Get("state");
            Assert.That(SaveSchema.CanonicalJson(strippedBack), Is.EqualTo(SaveSchema.CanonicalJson(v9State)));

            Assert.That(mgr.Save(m), Is.True);
            Assert.That(store.GetArchive("archive:schema-9"), Is.EqualTo(v9), "save v9 gốc được giữ nguyên văn");
            Assert.That(SaveFile.Parse(store.Get(SaveSlot.Main)).Save.State.SchemaVersion, Is.EqualTo(10));
        }

        [Test]
        public void Migration_9_to_10_rejects_a_state_that_already_has_v10_keys()
        {
            var st = new JsonObject().Set("schemaVersion", JsonValue.From(9L)).Set("relations", new JsonObject());
            MigrateResult r = Migrations.Migrate(9, st);
            Assert.That((r.Ok, r.Code), Is.EqualTo((false, MigrateErrorCode.MigrationFailed)));
        }

        [Test]
        public void Quests_expedition_counter_and_relations_round_trip_byte_for_byte_and_still_project_to_TS_v4()
        {
            GameState s = Played();
            string text = SaveSchema.Serialize(s, PFix.T0);
            ParseResult r = SaveFile.Parse(text);
            Assert.That(r.Ok, Is.True, r.ToString());
            GameState back = r.Save.State;
            Assert.That(PFix.Canonical(back), Is.EqualTo(PFix.Canonical(SaveSchema.ToPersistedState(s))));
            Assert.That(back.CompletedSideQuests, Is.EqualTo(s.CompletedSideQuests));
            Assert.That(back.ExpeditionsClaimed, Is.EqualTo(s.ExpeditionsClaimed));
            Assert.That(back.Relations.ToSnapshot().Pairs.Select(p => p.A + "|" + p.B + ":" + p.SharedMs), Is.EqualTo(s.Relations.ToSnapshot().Pairs.Select(p => p.A + "|" + p.B + ":" + p.SharedMs)));
            // Sau khi nạp lại: không nhận lại được nhiệm vụ, mốc thám hiểm đầu không trao lần hai.
            DispatchResult again = CommandDispatcher.Dispatch(back, new CompleteQuestCommand(PFix.Rid(), "Q_ORIGIN_FOREST_SURVEY"), new FixedClock(PFix.T0 + 5 * Hour));
            Assert.That(again.Error?.Code, Is.EqualTo(DomainErrorCode.QuestAlreadyCompleted));

            string dir = Environment.GetEnvironmentVariable("DRAVIO_EXPORT_DIR");
            PFix.Export(dir, "u211-world-progression", text);
            JsonObject v4 = PFix.AsV4((JsonObject)((JsonObject)JsonParser.Parse(text)).Get("state"));
            foreach (string k in new[] { "completedSideQuests", "expeditionsClaimed", "relations" }) Assert.That(v4.Get(k), Is.Null, k);
            Assert.That(SaveFile.Parse(PFix.AsV4Text(text)).Ok, Is.True, "ảnh v4: phần chung vẫn hợp lệ với TS");
        }

        [Test]
        public void A_damaged_relations_block_is_cleaned_on_load_and_never_loses_the_save()
        {
            GameState s = Played();
            JsonObject env = (JsonObject)JsonParser.Parse(SaveSchema.Serialize(s, PFix.T0));
            JsonObject st = ((JsonObject)env.Get("state")).ShallowCopy();
            var pairs = new JsonArray();
            foreach (JsonValue v in (JsonArray)((JsonObject)st.Get("relations")).Get("pairs")) pairs.Add(v);
            string ember = s.Dragons[0].Uid;
            pairs.Add(new JsonObject().Set("a", JsonValue.From("sold-long-ago")).Set("b", JsonValue.From(ember)).Set("sharedMs", JsonValue.From(5L * Hour)));
            pairs.Add(new JsonObject().Set("a", JsonValue.From(ember)).Set("b", JsonValue.From(ember)).Set("sharedMs", JsonValue.From(Hour)));
            pairs.Add(new JsonObject().Set("a", JsonValue.From("buddy")).Set("b", JsonValue.From(ember)).Set("sharedMs", JsonValue.From(999L * Hour)));
            pairs.Add(new JsonObject().Set("a", JsonValue.From(ember)).Set("b", JsonValue.From("buddy")).Set("sharedMs", JsonValue.From(-4L)));
            pairs.Add(JsonValue.From("junk"));
            st.Set("relations", new JsonObject().Set("pairs", pairs));
            ParseResult r = SaveFile.Parse(Resign(env, st));
            Assert.That(r.Ok, Is.True, "quan hệ hỏng không làm hỏng save: " + r);
            GameState m = r.Save.State;
            Assert.That(GameStateValidator.Validate(m).IsValid, Is.True);
            RelationLogSnapshot snap = m.Relations.ToSnapshot();
            Assert.That(snap.Pairs.Count, Is.EqualTo(1), "trùng cặp: giữ mục đầu tiên hợp lệ; bỏ tự-cặp, cá thể không còn, số âm, rác");
            Assert.That(snap.Pairs[0].SharedMs, Is.EqualTo(3 * Hour));
            Assert.That((m.Player.Gold.Micro, m.Dragons.Count, m.CompletedSideQuests.Count), Is.EqualTo((s.Player.Gold.Micro, s.Dragons.Count, 1)), "vàng, rồng, nhiệm vụ nguyên vẹn");

            st.Set("relations", JsonValue.From("oops"));
            ParseResult r2 = SaveFile.Parse(Resign(env, st));
            Assert.That(r2.Ok, Is.True, r2.ToString());
            Assert.That(r2.Save.State.Relations.IsEmpty, Is.True);
        }

        [Test]
        public void Tampered_quests_or_expedition_counters_are_rejected()
        {
            GameState s = Played();
            JsonObject env = (JsonObject)JsonParser.Parse(SaveSchema.Serialize(s, PFix.T0));
            foreach (Func<JsonObject, JsonObject> tamper in new Func<JsonObject, JsonObject>[]
            {
                x => x.Set("completedSideQuests", new JsonArray().Add(JsonValue.From("Q_FREE_GOLD"))),
                x => x.Set("completedSideQuests", new JsonArray().Add(JsonValue.From("QUEST_AQUATIC_EXPLORATION"))),
                x => x.Set("completedSideQuests", new JsonArray().Add(JsonValue.From("Q_ORIGIN_FOREST_SURVEY")).Add(JsonValue.From("Q_ORIGIN_FOREST_SURVEY"))),
                x => x.Set("completedSideQuests", JsonValue.From("x")),
                x => x.Set("expeditionsClaimed", new JsonObject().Set("origin_forest", JsonValue.From(-1L))),
                x => x.Set("expeditionsClaimed", new JsonObject().Set("moon_trip", JsonValue.From(3L))),
                x => x.Set("expeditionsClaimed", JsonValue.From(5L)),
            })
            {
                JsonObject st = tamper(((JsonObject)env.Get("state")).ShallowCopy());
                Assert.That(SaveFile.Parse(Resign(env, st)).Ok, Is.False, JsonWriter.Stringify(st.Get("completedSideQuests")) + " / " + JsonWriter.Stringify(st.Get("expeditionsClaimed")));
            }
        }
    }
}
