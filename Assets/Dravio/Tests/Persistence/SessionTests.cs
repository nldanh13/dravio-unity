using System;
using System.Collections.Generic;
using System.Linq;
using Dravio.Domain;
using Dravio.Domain.Core;
using Dravio.Persistence.Storage;
using Dravio.Persistence.Tests.Support;
using Dravio.Platform;
using NUnit.Framework;

namespace Dravio.Persistence.Tests
{
    /// <summary>
    /// GameSession + chính sách thời gian (ADR 0006 mục 9, ADR 0010): online/offline theo vòng đời, autosave 30 s,
    /// giới hạn 24 h và 75 %, đồng hồ lùi, một người ghi, và chống nhận thưởng offline nhiều lần.
    /// </summary>
    public sealed class SessionTests
    {
        // Emberling Adult Normal: 25 000 microG/s online, 18 750 offline (75 %).
        private const long AdultRate = 25_000;

        /// <summary>Save có một Emberling đã Adult trong slot sản xuất, ghi sẵn vào kho.</summary>
        private static (SessionRig rig, GameState saved) AdultSave(ISaveStore store = null)
        {
            store = store ?? new MemorySaveStore();
            GameState s = PFix.Advanced(PFix.Fresh(), 300_000);
            var mgr = new SaveManager(store, () => s.Clock.LastProcessedAtMs);
            mgr.Load();
            mgr.Save(s);
            return (new SessionRig(store, s.Clock.LastProcessedAtMs), s);
        }

        private static long Gold(GameSession s) => s.State.Player.Gold.Micro;
        private static long Earned(GameSession s) => s.State.Player.TotalGoldEarned.Micro;

        [Test]
        public void New_game_is_created_saved_immediately_and_ticks_online_at_1Hz()
        {
            var store = new MemorySaveStore();
            var rig = new SessionRig(store, PFix.T0);
            GameSession s = rig.NewSession();
            Assert.That((s.Status, s.LoadedFrom), Is.EqualTo((SessionStatus.Ready, "new")));
            Assert.That(store.Writes, Is.EqualTo(1));
            rig.Run(10_000);
            Assert.That(s.State.Dragons[0].XpSec, Is.EqualTo(10));
            Assert.That(s.State.Clock.LastProcessedAtMs, Is.EqualTo(PFix.T0 + 10_000));
        }

        [Test]
        public void Autosave_runs_every_30_seconds_of_real_time()
        {
            var store = new MemorySaveStore();
            var rig = new SessionRig(store, PFix.T0);
            GameSession s = rig.NewSession();
            rig.Run(29_000);
            Assert.That(store.Writes, Is.EqualTo(1));
            rig.Run(1_000);
            Assert.That(store.Writes, Is.EqualTo(2));
            rig.Run(30_000);
            Assert.That(store.Writes, Is.EqualTo(3));
            Assert.That(SaveFile.Parse(store.Get(SaveSlot.Main)).Save.State.Revision, Is.EqualTo(s.State.Revision));
        }

        [Test]
        public void Changing_the_wall_clock_does_not_change_the_autosave_cadence()
        {
            var store = new MemorySaveStore();
            var rig = new SessionRig(store, PFix.T0);
            rig.NewSession();
            rig.Clock.Now += 86_400_000; // người chơi chỉnh giờ tới 1 ngày
            rig.Run(1_000);
            Assert.That(store.Writes, Is.EqualTo(1), "autosave follows monotonic time");
        }

        [Test]
        public void Reopening_grants_offline_75_percent_once_and_saves_immediately()
        {
            var (rig, saved) = AdultSave();
            rig.Clock.Now += 8 * 3_600_000L;
            GameSession s = rig.NewSession();
            Assert.That(Gold(s) - saved.Player.Gold.Micro, Is.EqualTo(540_000_000), "8 h × 0.025 G/s × 75 %");
            Assert.That(s.State.OfflineReceipt.GoldMicro, Is.EqualTo(540_000_000));
            ParseResult disk = SaveFile.Parse(rig.Store.Get(SaveSlot.Main));
            Assert.That(disk.Save.State.Clock.LastProcessedAtMs, Is.EqualTo(rig.Clock.Now), "offline settlement persisted before play continues");
            s.Dispose();
            // Mở lại ngay: không còn thời gian vắng mặt → không thưởng thêm.
            GameSession again = rig.NewSession();
            Assert.That(Gold(again), Is.EqualTo(Gold(s)));
        }

        [Test]
        public void Offline_is_capped_at_24_hours_across_restarts()
        {
            var (rig, saved) = AdultSave();
            rig.Clock.Now += 3 * 86_400_000L;
            GameSession s = rig.NewSession();
            Assert.That(Gold(s) - saved.Player.Gold.Micro, Is.EqualTo(86_400L * AdultRate * 3 / 4));
        }

        [Test]
        public void Crash_before_the_offline_settlement_is_saved_cannot_double_the_reward()
        {
            var store = new MemorySaveStore();
            var (rig, saved) = AdultSave(store);
            rig.Clock.Now += 4 * 3_600_000L;
            store.FailNextWrite = true; // lưu sau khi quyết toán offline thất bại (mất điện)
            GameSession first = rig.NewSession();
            Assert.That(first.SaveError, Is.True);
            long firstGold = Gold(first);
            // Khởi động lại: quyết toán lại CÙNG khoảng vắng mặt từ mốc đã lưu, không cộng dồn.
            GameSession second = rig.NewSession();
            Assert.That(Gold(second), Is.EqualTo(firstGold));
            Assert.That(Gold(second) - saved.Player.Gold.Micro, Is.EqualTo(4 * 3600L * AdultRate * 3 / 4));
        }

        [Test]
        public void Hide_settles_online_then_show_settles_the_hidden_time_offline_once()
        {
            var (rig, saved) = AdultSave();
            GameSession s = rig.NewSession();
            rig.Run(10_000);
            long beforeHide = Earned(s);
            s.Handle(LifecycleAction.Hide);
            int writesAfterHide = ((MemorySaveStore)rig.Store).Writes;
            rig.Frozen(600_000); // 10 phút chạy nền
            s.Handle(LifecycleAction.Show);
            Assert.That(Earned(s) - beforeHide, Is.EqualTo(600L * AdultRate * 3 / 4));
            Assert.That(s.State.OfflineReceipt.ToMs - s.State.OfflineReceipt.FromMs, Is.EqualTo(600_000));
            // Show lặp lại hoặc Hide/Show dồn dập không cộng thêm.
            long after = Earned(s);
            s.Handle(LifecycleAction.Show);
            s.Handle(LifecycleAction.Show);
            Assert.That(Earned(s), Is.EqualTo(after));
            Assert.That(writesAfterHide, Is.GreaterThan(1), "hide flushes the save");
        }

        [Test]
        public void Show_while_already_visible_does_not_downgrade_online_time_to_offline()
        {
            var (rig, _) = AdultSave();
            GameSession s = rig.NewSession();
            long e0 = Earned(s);
            rig.Run(500, stepMs: 500);
            s.Handle(LifecycleAction.Show); // desktop lấy lại focus khi chưa từng ẩn
            rig.Run(500, stepMs: 500);
            Assert.That(Earned(s) - e0, Is.EqualTo(AdultRate), "1 s visible = 100 %");
        }

        [Test]
        public void Hidden_session_does_not_tick_online()
        {
            var (rig, _) = AdultSave();
            GameSession s = rig.NewSession();
            s.OnHide();
            long gold = Gold(s);
            rig.Run(60_000);
            Assert.That(Gold(s), Is.EqualTo(gold));
        }

        [Test]
        public void Sleep_without_lifecycle_events_is_settled_offline_by_the_gap_rule()
        {
            var (rig, _) = AdultSave();
            GameSession s = rig.NewSession();
            long earned = Earned(s);
            rig.Frozen(3_600_000); // ngủ máy 1 h, không có Pause/Focus
            rig.Run(1_000);
            Assert.That(Earned(s) - earned, Is.EqualTo(3601L * AdultRate * 3 / 4));
        }

        [Test]
        public void Desktop_focus_loss_keeps_online_while_mobile_focus_loss_hides()
        {
            var desktop = new LifecycleMapper(focusLossHides: false);
            var mobile = new LifecycleMapper(focusLossHides: true);
            Assert.That(desktop.OnFocus(false), Is.EqualTo(LifecycleAction.Flush));
            Assert.That(mobile.OnFocus(false), Is.EqualTo(LifecycleAction.Hide));
            Assert.That(desktop.OnFocus(true), Is.EqualTo(LifecycleAction.Show));
            Assert.That(desktop.OnPause(true), Is.EqualTo(LifecycleAction.Hide));
            Assert.That(desktop.OnPause(false), Is.EqualTo(LifecycleAction.Show));
            Assert.That(desktop.OnQuit(), Is.EqualTo(LifecycleAction.Hide));

            var (rig, _) = AdultSave();
            GameSession s = rig.NewSession();
            s.Handle(desktop.OnFocus(false));
            long e0 = Earned(s);
            rig.Run(10_000);
            Assert.That(Earned(s) - e0, Is.EqualTo(10L * AdultRate), "unfocused but visible window still earns 100 %");
        }

        [Test]
        public void Mobile_background_sequence_Focus_Pause_resume_counts_absence_once_at_75_percent()
        {
            var mobile = new LifecycleMapper(focusLossHides: true);
            var (rig, _) = AdultSave();
            GameSession s = rig.NewSession();
            long e0 = Earned(s);
            s.Handle(mobile.OnFocus(false));
            s.Handle(mobile.OnPause(true));
            rig.Frozen(120_000);
            s.Handle(mobile.OnPause(false));
            s.Handle(mobile.OnFocus(true));
            Assert.That(Earned(s) - e0, Is.EqualTo(120L * AdultRate * 3 / 4));
        }

        [Test]
        public void Clock_rollback_between_sessions_grants_nothing_until_the_high_water_mark_is_passed()
        {
            var (rig, saved) = AdultSave();
            long real = rig.Clock.Now;
            rig.Clock.Now = real - 86_400_000; // chỉnh lùi 1 ngày rồi mở game
            GameSession back = rig.NewSession();
            Assert.That(Gold(back), Is.EqualTo(saved.Player.Gold.Micro));
            Assert.That(back.State.Clock.HighWaterAtMs, Is.EqualTo(real));
            rig.Run(30_000);
            Assert.That(Gold(back), Is.EqualTo(saved.Player.Gold.Micro), "ticking below the high-water mark earns nothing");
            back.Dispose();
            rig.Clock.Now = real + 60_000;
            GameSession forward = rig.NewSession();
            Assert.That(Gold(forward) - saved.Player.Gold.Micro, Is.EqualTo(60L * AdultRate * 3 / 4), "only time beyond the old mark counts");
        }

        [Test]
        public void Clock_forward_exploit_then_back_does_not_pay_twice()
        {
            var (rig, saved) = AdultSave();
            long real = rig.Clock.Now;
            rig.Clock.Now = real + 86_400_000; // nhảy giờ tới 1 ngày để nhận offline
            GameSession cheat = rig.NewSession();
            long afterCheat = Gold(cheat);
            Assert.That(afterCheat - saved.Player.Gold.Micro, Is.EqualTo(86_400L * AdultRate * 3 / 4));
            cheat.Dispose();
            rig.Clock.Now = real + 3_600_000; // trả giờ về thật, 1 h sau
            GameSession honest = rig.NewSession();
            Assert.That(Gold(honest), Is.EqualTo(afterCheat), "the hour is already inside the claimed day");
        }

        [Test]
        public void Restoring_an_older_backup_never_yields_more_than_the_main_path()
        {
            var store = new MemorySaveStore();
            var (rig, _) = AdultSave(store);
            GameSession s = rig.NewSession();
            rig.Run(120_000); // tạo vài bản autosave
            s.OnHide();
            s.Dispose();
            rig.Clock.Now += 3_600_000;
            string main = store.Get(SaveSlot.Main);
            GameSession viaMain = rig.NewSession();
            long goldMain = Gold(viaMain);
            viaMain.Dispose();
            // Người chơi làm hỏng main để buộc khôi phục bản cũ hơn.
            var store2 = new MemorySaveStore();
            store2.Slots[SaveSlot.Main] = "corrupt";
            store2.Slots[SaveSlot.Backup1] = store.Get(SaveSlot.Backup2);
            var rig2 = new SessionRig(store2, rig.Clock.Now);
            GameSession viaBackup = rig2.NewSession();
            Assert.That(viaBackup.LoadedFrom, Is.EqualTo("backup1"));
            Assert.That(Gold(viaBackup), Is.LessThanOrEqualTo(goldMain));
            Assert.That(main, Is.Not.Null);
        }

        /// <summary>
        /// Thuộc tính: chuỗi ngẫu nhiên Pause/Focus/Quit/khởi động lại/đổi giờ/chạy — tổng vàng sản xuất không bao giờ
        /// vượt tốc độ online × thời gian thực (cộng phần vượt high-water khi người chơi chỉnh giờ tới).
        /// </summary>
        [Test]
        public void Random_lifecycle_sequences_never_create_gold_beyond_elapsed_time([Values(1u, 2u, 3u, 4u, 5u)] uint seed)
        {
            var store = new MemorySaveStore();
            var (rig, saved) = AdultSave(store);
            var mapper = new LifecycleMapper(focusLossHides: seed % 2 == 0);
            IRng r = Rng.Create(seed * 7919);
            GameSession s = rig.NewSession();
            long maxWall = rig.Clock.Now;
            long startWall = rig.Clock.Now;
            for (int i = 0; i < 300; i++)
            {
                switch (r.RandomInt(9))
                {
                    case 0: s.Handle(mapper.OnPause(true)); break;
                    case 1: s.Handle(mapper.OnPause(false)); break;
                    case 2: s.Handle(mapper.OnFocus(r.Next() < 0.5)); break;
                    case 3: rig.Frozen(r.RandomInt(4) * 60_000L); break;
                    case 4: s.Handle(mapper.OnQuit()); s.Dispose(); s = rig.NewSession(r.Next() < 0.8); break;
                    case 5: rig.Clock.Now += (r.RandomInt(5) - 3) * 3_600_000L; break; // đổi giờ ±
                    case 6: store.FailNextWrite = true; break;
                    default: rig.Run(r.RandomInt(20) * 1000L); break;
                }
                maxWall = Math.Max(maxWall, rig.Clock.Now);
                ParseResult disk = SaveFile.Parse(store.Get(SaveSlot.Main));
                Assert.That(disk.Ok, Is.True, "save stays valid at step " + i);
                long earned = Math.Max(Earned(s), disk.Save.State.Player.TotalGoldEarned.Micro) - saved.Player.TotalGoldEarned.Micro;
                Assert.That(earned, Is.LessThanOrEqualTo((maxWall - startWall) * AdultRate / 1000), "step " + i);
                Assert.That(s.State.Clock.HighWaterAtMs, Is.LessThanOrEqualTo(maxWall));
                Assert.That(GameStateValidator.Validate(s.State).IsValid, Is.True);
            }
        }

        [Test]
        public void Second_session_without_the_writer_lock_is_read_only_and_never_writes_or_advances()
        {
            var held = new HashSet<string>();
            var store = new MemorySaveStore();
            var rigA = new SessionRig(store, PFix.T0, new InMemoryWriterLock(held));
            GameSession a = rigA.NewSession();
            int writes = store.Writes;
            var rigB = new SessionRig(store, PFix.T0 + 3_600_000, new InMemoryWriterLock(held));
            GameSession b = rigB.NewSession();
            Assert.That(b.Status, Is.EqualTo(SessionStatus.ReadOnly));
            long gold = Gold(b);
            rigB.Run(60_000);
            b.OnHide(); b.OnShow(); b.SaveNow();
            Assert.That(b.Send(id => new PetCommand(id, b.State.Dragons[0].Uid)), Is.Null);
            Assert.That(Gold(b), Is.EqualTo(gold));
            Assert.That(store.Writes, Is.EqualTo(writes));
            Assert.That(a.Status, Is.EqualTo(SessionStatus.Ready));
        }

        [Test]
        public void Session_stops_and_becomes_read_only_when_another_writer_changes_the_save()
        {
            var store = new MemorySaveStore();
            var rig = new SessionRig(store, PFix.T0);
            GameSession s = rig.NewSession();
            new SaveManager(store, () => 0).Save(PFix.Fresh(99), force: true); // nơi khác ghi đè main
            rig.Run(30_000);
            Assert.That(s.Status, Is.EqualTo(SessionStatus.Conflict));
            string other = store.Get(SaveSlot.Main);
            rig.Run(60_000);
            Assert.That(store.Get(SaveSlot.Main), Is.EqualTo(other), "never overwrites the other writer");
        }

        [Test]
        public void Newer_version_and_unrecoverable_saves_stop_the_session_and_StartFresh_keeps_the_broken_data()
        {
            var store = new MemorySaveStore();
            store.Slots[SaveSlot.Main] = PFix.EnvelopeAt(9);
            GameSession newer = new SessionRig(store, PFix.T0).NewSession();
            Assert.That(newer.Status, Is.EqualTo(SessionStatus.NewerVersion));
            Assert.That(store.Writes, Is.EqualTo(0));

            var broken = new MemorySaveStore();
            broken.Slots[SaveSlot.Main] = "garbage";
            var rig = new SessionRig(broken, PFix.T0);
            GameSession s = rig.NewSession();
            Assert.That(s.Status, Is.EqualTo(SessionStatus.Unrecoverable));
            Assert.That(broken.Writes, Is.EqualTo(0));
            s.StartFresh();
            Assert.That(s.Status, Is.EqualTo(SessionStatus.Ready));
            Assert.That(broken.Get(SaveSlot.Backup1), Is.EqualTo("garbage"));
        }

        [Test]
        public void Commands_go_through_the_domain_and_reach_disk_on_the_next_autosave()
        {
            var store = new MemorySaveStore();
            var rig = new SessionRig(store, PFix.T0);
            GameSession s = rig.NewSession();
            string uid = s.State.Dragons[0].Uid;
            Assert.That(s.Send(id => new RenameDragonCommand(id, uid, "  Ember  ")), Is.Null);
            Assert.That(s.Send(id => new PetCommand(id, uid)), Is.Null);
            Assert.That(s.Send(id => new PetCommand(id, uid)).Code, Is.EqualTo(DomainErrorCode.PetCooldown));
            rig.Run(30_000);
            DragonState d = SaveFile.Parse(store.Get(SaveSlot.Main)).Save.State.Dragons[0];
            Assert.That((d.Nickname, d.Bond), Is.EqualTo(("Ember", 1)));
        }

        [Test]
        public void Import_replaces_the_session_state_and_settles_offline_once()
        {
            var store = new MemorySaveStore();
            var rig = new SessionRig(store, PFix.T0 + 3_600_000);
            GameSession s = rig.NewSession();
            GameState other = PFix.Advanced(PFix.Fresh(2), 300_000);
            string file = new SaveManager(new MemorySaveStore(), () => PFix.T0).ExportText(other);
            s.PrepareImport(file);
            Assert.That(s.ImportPreview.SaveId, Is.EqualTo("save-2"));
            s.ConfirmImport();
            Assert.That((s.Status, s.State.SaveId), Is.EqualTo((SessionStatus.Ready, "save-2")));
            long expected = other.Player.Gold.Micro + (3_600_000 - 300_000) / 1000 * AdultRate * 3 / 4;
            Assert.That(Gold(s), Is.EqualTo(expected));
            s.PrepareImport("broken");
            Assert.That(s.ImportError, Is.EqualTo(SaveErrorCode.CorruptJson));
        }

        [Test]
        public void Pumped_scheduler_runs_once_after_a_long_freeze_instead_of_bursting()
        {
            var sched = new PumpedScheduler();
            int n = 0;
            sched.Pump(0);
            sched.Every(1000, () => n++);
            sched.Pump(500);
            Assert.That(n, Is.EqualTo(0));
            sched.Pump(1000);
            Assert.That(n, Is.EqualTo(1));
            sched.Pump(60_000);
            Assert.That(n, Is.EqualTo(2));
            sched.Pump(60_999);
            Assert.That(n, Is.EqualTo(2));
            sched.Pump(61_000);
            Assert.That(n, Is.EqualTo(3));
            sched.Pump(10); // thời gian đơn điệu không lùi
            Assert.That(n, Is.EqualTo(3));
        }
    }
}
