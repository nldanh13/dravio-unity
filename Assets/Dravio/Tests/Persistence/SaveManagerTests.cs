using System;
using System.Collections.Generic;
using System.Linq;
using Dravio.Domain;
using Dravio.Persistence.Json;
using Dravio.Persistence.Schema;
using Dravio.Persistence.Storage;
using Dravio.Persistence.Tests.Support;
using NUnit.Framework;

namespace Dravio.Persistence.Tests
{
    /// <summary>Port của persistence.test.ts: migration, fixture v1/v2 thật, SaveManager, import/export, autosave.</summary>
    public sealed class SaveManagerTests
    {
        private static int RevisionOf(string text) { var r = SaveFile.Parse(text ?? ""); return r.Ok ? (int)r.Save.State.Revision : -1; }

        // ---- migration framework ----

        [Test]
        public void Schema_v7_is_current_and_steps_are_1_2_4_5_6_7()
        {
            Assert.That(SaveSchema.CurrentVersion, Is.EqualTo(7));
            Assert.That(PFix.Fresh().SchemaVersion, Is.EqualTo(7));
            Assert.That(Migrations.Steps.Select(s => s.From + "->" + s.To), Is.EqualTo(new[] { "1->2", "2->4", "4->5", "5->6", "6->7" }));
            Assert.That(Migrations.PreReleaseSchemaAliases[3], Is.EqualTo(1));
        }

        [Test]
        public void Current_version_passes_through_unchanged()
        {
            var s = new JsonObject().Set("schemaVersion", JsonValue.From(7L));
            MigrateResult r = Migrations.Migrate(7, s);
            Assert.That((r.Ok, r.FromVersion, r.PreRelease), Is.EqualTo((true, 7, false)));
            Assert.That(r.State, Is.SameAs(s));
        }

        [Test]
        public void Runs_steps_in_order_and_stamps_schemaVersion()
        {
            var steps = new[]
            {
                new MigrationStep(1, 2, st => st.ShallowCopy().Set("a", JsonValue.From(1L))),
                new MigrationStep(2, 4, st => st.ShallowCopy().Set("b", JsonValue.From(((JsonNumber)st.Get("a")).Value + 1))),
            };
            MigrateResult r = Migrations.Migrate(1, new JsonObject().Set("schemaVersion", JsonValue.From(1L)), steps, 4);
            Assert.That(r.Ok, Is.True);
            Assert.That(JsonWriter.Canonical(r.State), Is.EqualTo("{\"a\":1,\"b\":2,\"schemaVersion\":4}"));
        }

        [Test]
        public void Reports_missing_step_throwing_step_and_newer_version()
        {
            Assert.That(Migrations.Migrate(1, new JsonObject(), Array.Empty<MigrationStep>(), 2).Code, Is.EqualTo(MigrateErrorCode.UnsupportedVersion));
            var boom = new[] { new MigrationStep(1, 2, _ => throw new InvalidOperationException("bad data")) };
            MigrateResult r = Migrations.Migrate(1, new JsonObject(), boom, 2);
            Assert.That((r.Code, r.Details[0]), Is.EqualTo((MigrateErrorCode.MigrationFailed, "1 → 2: bad data")));
            Assert.That(Migrations.Migrate(5, new JsonObject()).Code, Is.EqualTo(MigrateErrorCode.NewerVersion));
        }

        [Test]
        public void v1_to_v2_backfills_discoveries_only_for_open_gated_habitats()
        {
            MigrationStep step = Migrations.Steps.Single(m => m.From == 1);
            JsonObject Z(params string[] ids) { var a = new JsonArray(); foreach (var i in ids) a.Add(JsonValue.From(i)); return new JsonObject().Set("zones", new JsonArray().Add(new JsonObject().Set("id", JsonValue.From("origin")).Set("unlockedHabitatIds", a))); }
            Assert.That(JsonWriter.Stringify(step.Run(Z("origin_meadow", "origin_pondbank")).Get("discoveries")), Is.EqualTo("[\"DISC_AQUATIC_HABITAT\"]"));
            Assert.That(JsonWriter.Stringify(step.Run(Z("origin_meadow")).Get("discoveries")), Is.EqualTo("[]"));
        }

        // ---- fixtures thật do bản TS ghi ----

        [TestCase("save-schema-v1.json", 1)]
        [TestCase("save-schema-v2.json", 2)]
        public void Real_TypeScript_save_is_archived_verbatim_and_saved_as_v4_with_progress_intact(string file, int version)
        {
            string text = PFix.Fixture(file);
            var original = (JsonObject)((JsonObject)JsonParser.Parse(text)).Get("state");
            var (mgr, store) = PFix.Manager();
            store.Slots[SaveSlot.Main] = text;
            LoadOutcome r = mgr.Load();
            Assert.That(r.Kind, Is.EqualTo(LoadKind.Loaded));
            Assert.That(r.Save.MigratedFrom, Is.EqualTo(version));
            Assert.That(store.Writes, Is.EqualTo(0), "loading never writes");
            Assert.That(r.Save.State.Tutorial.StartedAtMs, Is.EqualTo((long)((JsonNumber)((JsonObject)original.Get("clock")).Get("lastProcessedAtMs")).Value));
            Assert.That(mgr.Save(r.Save.State), Is.True);
            Assert.That(store.GetArchive("archive:schema-" + version), Is.EqualTo(text));
            ParseResult again = SaveFile.Parse(store.Get(SaveSlot.Main));
            Assert.That(again.Ok && again.Save.MigratedFrom == null, Is.True);
            JsonObject now = GameStateJson.ToJson(again.Save.State);
            foreach (string k in new[] { "goldMicro", "dragons", "zones", "clock", "totalGoldEarnedMicro" })
                Assert.That(JsonWriter.Canonical(now.Get(k)), Is.EqualTo(JsonWriter.Canonical(original.Get(k))), k);
        }

        // ---- SaveManager ----

        [Test]
        public void First_run_is_empty_then_save_and_load_returns_the_same_state()
        {
            var (mgr, store) = PFix.Manager();
            Assert.That(mgr.Load().Kind, Is.EqualTo(LoadKind.Empty));
            GameState s = PFix.Fresh();
            Assert.That(mgr.Save(s), Is.True);
            LoadOutcome again = new SaveManager(store, () => PFix.T0).Load();
            Assert.That(again.Kind, Is.EqualTo(LoadKind.Loaded));
            Assert.That(PFix.Canonical(again.Save.State), Is.EqualTo(PFix.Canonical(s)));
        }

        [Test]
        public void Duplicate_save_of_the_same_revision_is_skipped()
        {
            var (mgr, store) = PFix.Manager();
            GameState s = PFix.Fresh();
            mgr.Save(s);
            Assert.That(mgr.Save(s), Is.False);
            Assert.That(mgr.Save(s.DeepClone()), Is.False, "same saveId + revision is a duplicate even as a different object");
            Assert.That(store.Writes, Is.EqualTo(1));
            Assert.That(mgr.Save(PFix.Advanced(s, 1000)), Is.True);
            Assert.That(store.Writes, Is.EqualTo(2));
            Assert.That(mgr.Save(s, force: true), Is.True, "force rewrites");
        }

        [Test]
        public void Keeps_two_rotating_backups()
        {
            var (mgr, store) = PFix.Manager();
            GameState s = PFix.Fresh();
            var revs = new List<int>();
            for (int i = 0; i < 4; i++) { s = PFix.Advanced(s, 1000); revs.Add((int)s.Revision); mgr.Save(s); }
            Assert.That(new[] { RevisionOf(store.Get(SaveSlot.Main)), RevisionOf(store.Get(SaveSlot.Backup1)), RevisionOf(store.Get(SaveSlot.Backup2)) },
                Is.EqualTo(new[] { revs[3], revs[2], revs[1] }));
        }

        [Test]
        public void Falls_back_to_backup1_when_main_is_corrupt_then_backup2()
        {
            var (mgr, store) = PFix.Manager();
            GameState s = PFix.Fresh();
            for (int i = 0; i < 3; i++) { s = PFix.Advanced(s, 1000); mgr.Save(s); }
            store.Slots[SaveSlot.Main] = "{\"truncated";
            LoadOutcome r1 = new SaveManager(store, () => PFix.T0).Load();
            Assert.That(r1.Slot, Is.EqualTo(SaveSlot.Backup1));
            Assert.That(Helpers.Names(r1.Failures), Is.EqualTo(new[] { "Main:corrupt-json" }));
            store.Slots[SaveSlot.Backup1] = store.Get(SaveSlot.Backup1).Replace("\"goldMicro\":", "\"goldMicro\":9");
            LoadOutcome r2 = new SaveManager(store, () => PFix.T0).Load();
            Assert.That(r2.Slot, Is.EqualTo(SaveSlot.Backup2));
            Assert.That(Helpers.Names(r2.Failures), Is.EqualTo(new[] { "Main:corrupt-json", "Backup1:bad-checksum" }));
        }

        [Test]
        public void All_slots_broken_is_unrecoverable_and_never_overwritten_until_forced()
        {
            var (_, store) = PFix.Manager();
            store.Slots[SaveSlot.Main] = "garbage";
            var mgr = new SaveManager(store, () => PFix.T0);
            Assert.That(mgr.Load().Kind, Is.EqualTo(LoadKind.Unrecoverable));
            Assert.That(mgr.Save(PFix.Fresh()), Is.False);
            Assert.That(store.Get(SaveSlot.Main), Is.EqualTo("garbage"));
            Assert.That(mgr.Save(PFix.Fresh(), force: true), Is.True);
            Assert.That(store.Get(SaveSlot.Backup1), Is.EqualTo("garbage"), "broken data is kept as a backup, not deleted");
        }

        [Test]
        public void Save_from_a_newer_version_blocks_writes_even_forced_and_is_never_overwritten()
        {
            var (_, store) = PFix.Manager();
            string newer = PFix.EnvelopeAt(8);
            store.Slots[SaveSlot.Main] = newer;
            var mgr = new SaveManager(store, () => PFix.T0);
            Assert.That(mgr.Load().Kind, Is.EqualTo(LoadKind.NewerVersion));
            Assert.That(mgr.Block, Is.EqualTo(WriteBlock.NewerVersion));
            Assert.That(mgr.Save(PFix.Fresh(), force: true), Is.False);
            Assert.That(store.Get(SaveSlot.Main), Is.EqualTo(newer));
        }

        [Test]
        public void Read_only_session_never_writes()
        {
            var (mgr, store) = PFix.Manager();
            mgr.SetReadOnly(true);
            Assert.That(mgr.Save(PFix.Fresh()), Is.False);
            Assert.That(store.Writes, Is.EqualTo(0));
            mgr.SetReadOnly(false);
            Assert.That(mgr.Save(PFix.Fresh()), Is.True);
        }

        [Test]
        public void Prerelease_v3_save_keeps_its_original_text_in_a_permanent_archive()
        {
            var (_, store) = PFix.Manager();
            string original = PFix.EnvelopeAt(3);
            store.Slots[SaveSlot.Main] = original;
            var mgr = new SaveManager(store, () => PFix.T0);
            LoadOutcome r = mgr.Load();
            Assert.That(r.Save.MigratedFrom, Is.EqualTo(3));
            Assert.That(store.Writes, Is.EqualTo(0));
            GameState s = r.Save.State;
            Assert.That(mgr.Save(s), Is.True);
            for (int i = 0; i < 3; i++) { s = PFix.Advanced(s, 1000); mgr.Save(s); }
            Assert.That(store.GetArchive("archive:schema-3"), Is.EqualTo(original));
            ParseResult main = SaveFile.Parse(store.Get(SaveSlot.Main));
            Assert.That((main.Save.State.SchemaVersion, main.Save.MigratedFrom), Is.EqualTo((7, (int?)null)));
        }

        [Test]
        public void Archive_is_never_overwritten()
        {
            var (_, store) = PFix.Manager();
            store.Archives["archive:schema-3"] = "first";
            store.Slots[SaveSlot.Main] = PFix.EnvelopeAt(3);
            var mgr = new SaveManager(store, () => PFix.T0);
            mgr.Save(mgr.Load().Save.State);
            Assert.That(store.GetArchive("archive:schema-3"), Is.EqualTo("first"));
        }

        [Test]
        public void Failed_write_is_reported_and_the_next_save_retries()
        {
            var (mgr, store) = PFix.Manager();
            store.FailNextWrite = true;
            Assert.Throws<SaveWriteException>(() => mgr.Save(PFix.Fresh()));
            Assert.That(mgr.Save(PFix.Fresh()), Is.True);
        }

        [Test]
        public void Concurrent_saves_from_two_threads_never_interleave()
        {
            var (mgr, store) = PFix.Manager();
            GameState a = PFix.Fresh();
            var states = new List<GameState> { a };
            for (int i = 0; i < 50; i++) states.Add(PFix.Advanced(states[states.Count - 1], 1000));
            System.Threading.Tasks.Parallel.ForEach(states, s => mgr.Save(s, force: true));
            Assert.That(store.Writes, Is.EqualTo(51));
            Assert.That(SaveFile.Parse(store.Get(SaveSlot.Main)).Ok && SaveFile.Parse(store.Get(SaveSlot.Backup1)).Ok && SaveFile.Parse(store.Get(SaveSlot.Backup2)).Ok, Is.True);
        }

        [Test]
        public void Another_writer_changing_main_is_detected_as_conflict_and_nothing_is_overwritten()
        {
            var store = new MemorySaveStore();
            var a = new SaveManager(store, () => PFix.T0);
            var b = new SaveManager(store, () => PFix.T0);
            a.Load(); b.Load();
            GameState s = PFix.Fresh();
            Assert.That(a.Save(s), Is.True);
            string afterA = store.Get(SaveSlot.Main);
            Assert.That(b.Save(PFix.Fresh(9)), Is.False);
            Assert.That(b.Block, Is.EqualTo(WriteBlock.Conflict));
            Assert.That(store.Get(SaveSlot.Main), Is.EqualTo(afterA));
            Assert.That(b.Save(PFix.Fresh(9), force: true), Is.False, "conflict is not bypassed by force");
        }

        // ---- export / import ----

        [Test]
        public void Export_then_import_preview_then_confirm_current_save_becomes_backup()
        {
            var (mgr, store) = PFix.Manager();
            GameState current = PFix.Advanced(PFix.Fresh(1), 5000);
            mgr.Save(current);
            GameState other = PFix.Advanced(PFix.Fresh(2), 60_000);
            string file = new SaveManager(new MemorySaveStore(), () => PFix.T0 + 9).ExportText(other);
            ImportPrepareResult prep = mgr.PrepareImport(file);
            Assert.That(prep.Ok, Is.True);
            Assert.That((prep.Preview.SaveId, prep.Preview.DragonCount, prep.Preview.SpeciesCount, prep.Preview.GoldMicro, prep.Preview.SavedAtMs, prep.Preview.MigratedFrom),
                Is.EqualTo(("save-2", 1, 1, other.Player.Gold.Micro, PFix.T0 + 9, (int?)null)));
            Assert.That(store.Writes, Is.EqualTo(1), "nothing written before confirmation");
            GameState imported = mgr.ConfirmImport(current);
            Assert.That(imported.SaveId, Is.EqualTo("save-2"));
            Assert.That(imported.Revision, Is.EqualTo(other.Revision + 1));
            Assert.That(SaveFile.Parse(store.Get(SaveSlot.Main)).Save.State.SaveId, Is.EqualTo("save-2"));
            Assert.That(SaveFile.Parse(store.Get(SaveSlot.Backup1)).Save.State.SaveId, Is.EqualTo("save-1"));
        }

        [Test]
        public void Confirm_without_prepare_does_nothing_and_cancel_clears()
        {
            var (mgr, store) = PFix.Manager();
            Assert.That(mgr.ConfirmImport(null), Is.Null);
            Assert.That(mgr.PrepareImport(mgr.ExportText(PFix.Fresh())).Ok, Is.True);
            mgr.CancelImport();
            Assert.That(mgr.ConfirmImport(null), Is.Null);
            Assert.That(store.Writes, Is.EqualTo(0));
        }

        [Test]
        public void Import_strips_entitlements_Verified_and_unknown_keys_even_with_valid_checksum()
        {
            var (mgr, store) = PFix.Manager();
            JsonObject env = SaveSchema.CreateEnvelope(PFix.Fresh(), PFix.T0);
            JsonObject state = ((JsonObject)env.Get("state")).ShallowCopy();
            state.Set("entitlements", new JsonObject().Set("removeAds", JsonValue.True)).Set("purchases", new JsonArray().Add(JsonValue.From("astral")));
            var dragons = new JsonArray();
            foreach (JsonObject d in (JsonArray)state.Get("dragons")) dragons.Add(d.ShallowCopy().Set("registryRef", JsonValue.From("reg-123")).Set("verified", JsonValue.True));
            state.Set("dragons", dragons);
            string forged = JsonWriter.Stringify(env.ShallowCopy().Set("state", state).Set("checksum", JsonValue.From(SaveSchema.ChecksumOf(state))));
            ImportPrepareResult prep = mgr.PrepareImport(forged);
            Assert.That(prep.Ok, Is.True);
            Assert.That(string.Join(" ", prep.Preview.Warnings), Does.Match("entitlements.*purchases").And.Contain("registryRef"));
            GameState s = mgr.ConfirmImport(null);
            Assert.That(s.Dragons.All(d => d.RegistryRef == null), Is.True);
            string mainText = store.Get(SaveSlot.Main);
            Assert.That(mainText, Does.Not.Contain("entitlements").And.Not.Contain("registryRef").And.Not.Contain("verified"));
        }

        [Test]
        public void Broken_import_is_rejected_without_touching_the_store()
        {
            var (mgr, store) = PFix.Manager();
            mgr.Save(PFix.Fresh());
            ImportPrepareResult r = mgr.PrepareImport("not json");
            Assert.That((r.Ok, r.Code), Is.EqualTo((false, SaveErrorCode.CorruptJson)));
            Assert.That(mgr.ConfirmImport(null), Is.Null);
            Assert.That(store.Writes, Is.EqualTo(1));
        }

        // ---- autosave ----

        private sealed class ManualScheduler : IIntervalScheduler
        {
            public readonly List<(long ms, Action a, Handle h)> Jobs = new List<(long, Action, Handle)>();
            public sealed class Handle : IDisposable { public bool Disposed; public void Dispose() => Disposed = true; }
            public IDisposable Every(long ms, Action a) { var h = new Handle(); Jobs.Add((ms, a, h)); return h; }
            public void FireAll() { foreach (var j in Jobs.ToArray()) if (!j.h.Disposed) j.a(); }
            public int Active => Jobs.Count(j => !j.h.Disposed);
        }

        [Test]
        public void Autosave_every_30s_and_on_flush_only_when_state_changed()
        {
            var (mgr, store) = PFix.Manager();
            GameState state = PFix.Fresh();
            var sched = new ManualScheduler();
            var auto = new Autosave(s => mgr.Save(s), () => state, sched);
            auto.Start(); auto.Start();
            Assert.That(sched.Active, Is.EqualTo(1));
            Assert.That(sched.Jobs[0].ms, Is.EqualTo(30_000));
            sched.FireAll();
            Assert.That(store.Writes, Is.EqualTo(1));
            sched.FireAll();
            Assert.That(auto.Flush(), Is.False);
            state = PFix.Advanced(state, 30_000);
            Assert.That(auto.Flush(), Is.True);
            Assert.That(store.Writes, Is.EqualTo(2));
            auto.Stop();
            Assert.That(sched.Active, Is.EqualTo(0));
        }

        [Test]
        public void Autosave_reports_write_errors_instead_of_throwing()
        {
            var (mgr, store) = PFix.Manager();
            var errors = new List<Exception>();
            store.FailNextWrite = true;
            var auto = new Autosave(s => mgr.Save(s), () => PFix.Fresh(), new ManualScheduler());
            auto.Error += errors.Add;
            Assert.That(auto.Flush(), Is.False);
            Assert.That(errors.Count, Is.EqualTo(1));
            Assert.That(auto.Flush(), Is.True, "next autosave retries");
        }
    }
}
