using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Dravio.Domain;
using Dravio.Persistence.Storage;
using Dravio.Persistence.Tests.Support;
using NUnit.Framework;

namespace Dravio.Persistence.Tests
{
    /// <summary>
    /// FileSaveStore: ghi nguyên tử theo thế hệ + 2 backup trên hệ thống file, khôi phục file hỏng,
    /// mất điện ở từng thao tác ghi (MemoryFileSystem) và trên đĩa thật (thư mục tạm).
    /// </summary>
    public sealed class FileStoreAndPowerLossTests
    {
        private string _tmp;

        [SetUp]
        public void SetUp() => _tmp = Path.Combine(Path.GetTempPath(), "dravio-u22-" + Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown() { try { if (Directory.Exists(_tmp)) Directory.Delete(_tmp, true); } catch (IOException) { } }

        private static int Rev(string text) { ParseResult r = SaveFile.Parse(text ?? ""); return r.Ok ? (int)r.Save.State.Revision : -1; }

        private static List<GameState> Chain(int n)
        {
            var list = new List<GameState> { PFix.Fresh() };
            for (int i = 1; i < n; i++) list.Add(PFix.Advanced(list[i - 1], 1000));
            return list;
        }

        [Test]
        public void Disk_store_round_trip_rotation_and_cleanup()
        {
            var store = new FileSaveStore(_tmp);
            var mgr = new SaveManager(store, () => PFix.T0);
            Assert.That(mgr.Load().Kind, Is.EqualTo(LoadKind.Empty));
            List<GameState> states = Chain(5);
            foreach (GameState s in states) Assert.That(mgr.Save(s), Is.True);
            Assert.That(new[] { Rev(store.Get(SaveSlot.Main)), Rev(store.Get(SaveSlot.Backup1)), Rev(store.Get(SaveSlot.Backup2)) },
                Is.EqualTo(new[] { (int)states[4].Revision, (int)states[3].Revision, (int)states[2].Revision }));
            string[] files = Directory.GetFiles(_tmp).Select(Path.GetFileName).OrderBy(x => x).ToArray();
            Assert.That(files, Is.EqualTo(new[] { "save-000000000003.json", "save-000000000004.json", "save-000000000005.json" }));
            // File là UTF-8 không BOM, đọc được bằng JSON.parse của bản web.
            byte[] bytes = File.ReadAllBytes(Path.Combine(_tmp, files[2]));
            Assert.That(bytes[0], Is.EqualTo((byte)'{'));
            LoadOutcome again = new SaveManager(new FileSaveStore(_tmp), () => PFix.T0).Load();
            Assert.That(PFix.Canonical(again.Save.State), Is.EqualTo(PFix.Canonical(states[4])));
        }

        [Test]
        public void Cleanup_removes_only_temporary_saves_for_published_generations()
        {
            var store = new FileSaveStore(_tmp);
            var mgr = new SaveManager(store, () => PFix.T0);
            List<GameState> states = Chain(2);
            mgr.Save(states[0]);
            string orphan = Path.Combine(_tmp, "save-000000000002.json." + Guid.NewGuid().ToString("N") + ".tmp");
            string future = Path.Combine(_tmp, "save-000000000003.json." + Guid.NewGuid().ToString("N") + ".tmp");
            string unrelated = Path.Combine(_tmp, "notes.json." + Guid.NewGuid().ToString("N") + ".tmp");
            foreach (string path in new[] { orphan, future, unrelated }) File.WriteAllText(path, "{\"half");
            Assert.That(new SaveManager(new FileSaveStore(_tmp), () => PFix.T0).Load().Save.State.Revision, Is.EqualTo(states[0].Revision));
            Assert.That(mgr.Save(states[1]), Is.True);
            Assert.That(File.Exists(orphan), Is.False);
            Assert.That(File.Exists(future), Is.True);
            Assert.That(File.Exists(unrelated), Is.True);
        }

        [Test]
        public void Interrupted_cleanup_keeps_a_committed_save_and_the_same_writer_can_save_again()
        {
            var fs = new MemoryFileSystem();
            var store = new FileSaveStore("/saves", fs);
            var mgr = new SaveManager(store, () => PFix.T0);
            mgr.Load();
            List<GameState> states = Chain(5);
            for (int i = 0; i < 3; i++) Assert.That(mgr.Save(states[i]), Is.True);
            // The temporary write and rename succeed. The first attempt to delete an older generation fails.
            fs.CrashAfterOperations = fs.MutatingOperations + 2;

            Assert.That(mgr.Save(states[3]), Is.True);
            Assert.That(fs.Crashed, Is.True);
            fs.Reboot();

            Assert.That(store.Generations(), Is.EqualTo(new long[] { 4, 3, 2, 1 }));
            Assert.That(Rev(store.Get(SaveSlot.Main)), Is.EqualTo(states[3].Revision));
            Assert.That(mgr.Save(states[4]), Is.True);
            Assert.That(mgr.Block, Is.EqualTo(WriteBlock.None));
            Assert.That(store.Generations(), Is.EqualTo(new long[] { 5, 4, 3 }));
        }

        private static IEnumerable<TestCaseData> Corruptions()
        {
            yield return new TestCaseData((Func<byte[], byte[]>)(b => b.Take(b.Length / 2).ToArray())).SetName("Main_truncated_falls_back_to_backup1");
            yield return new TestCaseData((Func<byte[], byte[]>)(b => new byte[0])).SetName("Main_zero_bytes_falls_back_to_backup1");
            yield return new TestCaseData((Func<byte[], byte[]>)(b => { var c = (byte[])b.Clone(); c[c.Length / 2] ^= 0x01; return c; })).SetName("Main_single_bit_flip_falls_back_to_backup1");
            yield return new TestCaseData((Func<byte[], byte[]>)(b => { var c = (byte[])b.Clone(); c[10] = 0xFF; return c; })).SetName("Main_invalid_UTF8_falls_back_to_backup1");
            yield return new TestCaseData((Func<byte[], byte[]>)(b => Enumerable.Repeat((byte)0, b.Length).ToArray())).SetName("Main_zeroed_sectors_falls_back_to_backup1");
        }

        [TestCaseSource(nameof(Corruptions))]
        public void Corrupt_main_on_disk(Func<byte[], byte[]> corrupt)
        {
            var store = new FileSaveStore(_tmp);
            var mgr = new SaveManager(store, () => PFix.T0);
            List<GameState> states = Chain(3);
            foreach (GameState s in states) mgr.Save(s);
            string mainPath = Path.Combine(_tmp, "save-000000000003.json");
            File.WriteAllBytes(mainPath, corrupt(File.ReadAllBytes(mainPath)));
            LoadOutcome r = new SaveManager(new FileSaveStore(_tmp), () => PFix.T0).Load();
            Assert.That(r.Kind, Is.EqualTo(LoadKind.Loaded));
            Assert.That(r.Slot, Is.EqualTo(SaveSlot.Backup1));
            Assert.That(r.Save.State.Revision, Is.EqualTo(states[1].Revision));
            Assert.That(r.Failures.Single().Slot, Is.EqualTo(SaveSlot.Main));
        }

        [Test]
        public void After_restoring_from_backup_the_next_save_keeps_the_broken_file_as_backup()
        {
            var store = new FileSaveStore(_tmp);
            var mgr = new SaveManager(store, () => PFix.T0);
            List<GameState> states = Chain(3);
            foreach (GameState s in states) mgr.Save(s);
            File.WriteAllText(Path.Combine(_tmp, "save-000000000003.json"), "garbage");
            var mgr2 = new SaveManager(new FileSaveStore(_tmp), () => PFix.T0);
            GameState restored = mgr2.Load().Save.State;
            Assert.That(mgr2.Save(PFix.Advanced(restored, 1000)), Is.True);
            var s2 = new FileSaveStore(_tmp);
            Assert.That(s2.Get(SaveSlot.Backup1), Is.EqualTo("garbage"));
            Assert.That(Rev(s2.Get(SaveSlot.Backup2)), Is.EqualTo((int)states[1].Revision));
        }

        /// <summary>
        /// Mất điện ở MỌI thao tác ghi của một lần lưu: sau khi khởi động lại, save luôn đọc được và là bản trước
        /// hoặc bản mới — không bao giờ hỏng, không bao giờ mất cả main lẫn backup.
        /// </summary>
        [Test]
        public void Power_loss_at_every_write_step_never_loses_the_save()
        {
            List<GameState> states = Chain(4);
            int crashPoints = 0;
            for (int k = 0; ; k++)
            {
                var fs = new MemoryFileSystem();
                var mgr = new SaveManager(new FileSaveStore("/saves", fs), () => PFix.T0);
                mgr.Load();
                for (int i = 0; i < 3; i++) mgr.Save(states[i]);
                fs.CrashAfterOperations = fs.MutatingOperations + k;
                bool crashed;
                try { mgr.Save(states[3]); crashed = fs.Crashed; }
                catch (SaveWriteException) { crashed = true; }
                fs.Reboot();
                LoadOutcome r = new SaveManager(new FileSaveStore("/saves", fs), () => PFix.T0).Load();
                Assert.That(r.Kind, Is.EqualTo(LoadKind.Loaded), "crash point " + k);
                Assert.That(r.Slot, Is.EqualTo(SaveSlot.Main), "main is never torn, crash point " + k);
                long rev = r.Save.State.Revision;
                Assert.That(rev == states[2].Revision || rev == states[3].Revision, Is.True, "crash point " + k);
                var store = new FileSaveStore("/saves", fs);
                int valid = new[] { SaveSlot.Main, SaveSlot.Backup1, SaveSlot.Backup2 }.Count(s => SaveFile.Parse(store.Get(s) ?? "").Ok);
                Assert.That(valid, Is.EqualTo(3), "all three slots stay readable, crash point " + k);
                if (!crashed) break;
                crashPoints++;
                // Lần lưu kế tiếp sau khi khởi động lại hoạt động bình thường và dọn file tạm.
                var mgr2 = new SaveManager(new FileSaveStore("/saves", fs), () => PFix.T0);
                mgr2.Load();
                Assert.That(mgr2.Save(PFix.Advanced(r.Save.State, 1000)), Is.True);
                Assert.That(fs.AllPaths.Any(p => p.EndsWith(".tmp", StringComparison.Ordinal)), Is.False);
            }
            Assert.That(crashPoints, Is.GreaterThanOrEqualTo(3), "write path has temp write, rename and prune steps");
        }

        [Test]
        public void Power_loss_during_the_first_save_after_migration_keeps_either_the_old_save_or_the_archive()
        {
            string v2 = PFix.Fixture("save-schema-v2.json");
            for (int k = 0; ; k++)
            {
                var fs = new MemoryFileSystem();
                fs.CorruptText("/saves/save-000000000001.json", v2);
                var mgr = new SaveManager(new FileSaveStore("/saves", fs), () => PFix.T0);
                LoadOutcome loaded = mgr.Load();
                Assert.That(loaded.Save.MigratedFrom, Is.EqualTo(2));
                fs.CrashAfterOperations = fs.MutatingOperations + k;
                bool crashed;
                try { mgr.Save(loaded.Save.State); crashed = fs.Crashed; }
                catch (SaveWriteException) { crashed = true; }
                fs.Reboot();
                var store = new FileSaveStore("/saves", fs);
                LoadOutcome r = new SaveManager(store, () => PFix.T0).Load();
                Assert.That(r.Kind, Is.EqualTo(LoadKind.Loaded), "crash point " + k);
                if (r.Save.MigratedFrom == null)
                    Assert.That(store.GetArchive("archive:schema-2"), Is.EqualTo(v2), "a v4 main implies the verbatim v2 archive exists");
                else
                    Assert.That(r.Save.MigratedFrom, Is.EqualTo(2));
                if (!crashed) { Assert.That(store.GetArchive("archive:schema-2"), Is.EqualTo(v2)); break; }
            }
        }

        [Test]
        public void Conflict_is_detected_across_two_file_store_instances()
        {
            var a = new SaveManager(new FileSaveStore(_tmp), () => PFix.T0);
            var b = new SaveManager(new FileSaveStore(_tmp), () => PFix.T0);
            a.Load(); b.Load();
            Assert.That(a.Save(PFix.Fresh()), Is.True);
            Assert.That(b.Save(PFix.Fresh(8)), Is.False);
            Assert.That(b.Block, Is.EqualTo(WriteBlock.Conflict));
            Assert.That(SaveFile.Parse(new FileSaveStore(_tmp).Get(SaveSlot.Main)).Save.State.SaveId, Is.EqualTo("save-7"));
        }

        [Test]
        public void Disk_write_failure_surfaces_as_SaveWriteException_and_keeps_old_files()
        {
            var store = new FileSaveStore(_tmp);
            var mgr = new SaveManager(store, () => PFix.T0);
            List<GameState> states = Chain(2);
            mgr.Save(states[0]);
            var fs = new MemoryFileSystem { CrashAfterOperations = 0 };
            var broken = new SaveManager(new FileSaveStore("/x", fs), () => PFix.T0);
            Assert.Throws<SaveWriteException>(() => broken.Save(states[1]));
            Assert.That(Rev(store.Get(SaveSlot.Main)), Is.EqualTo((int)states[0].Revision));
        }

        [Test]
        public void File_writer_lock_allows_one_writer()
        {
            using (FileWriterLock a = FileWriterLock.TryAcquire(_tmp))
            {
                Assert.That(a.Acquired, Is.True);
                using (FileWriterLock b = FileWriterLock.TryAcquire(_tmp)) Assert.That(b.Acquired, Is.False);
            }
            using (FileWriterLock c = FileWriterLock.TryAcquire(_tmp)) Assert.That(c.Acquired, Is.True, "released on dispose");
        }

        [Test]
        public void Archive_file_names_are_safe_and_stable()
        {
            var store = new FileSaveStore(_tmp);
            store.Rotate("x", new RotateOptions { Archive = new ArchiveWrite("archive:schema-3", "orig") });
            Assert.That(File.Exists(Path.Combine(_tmp, "archive-schema-3.json")), Is.True);
            store.Rotate("y", new RotateOptions { Archive = new ArchiveWrite("archive:schema-3", "other") });
            Assert.That(store.GetArchive("archive:schema-3"), Is.EqualTo("orig"));
            Assert.That(Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(_tmp, "archive-schema-3.json"))), Is.EqualTo("orig"));
        }
    }
}
