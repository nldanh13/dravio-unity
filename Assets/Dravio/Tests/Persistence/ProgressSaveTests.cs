using System;
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
    /// U2.4 × Save Schema v4: tutorial / pendingPack / thành tựu qua vòng lưu–nạp, lưu ngay sau mua/nhận gói
    /// (IMMEDIATE_SAVE của gameSession.ts), thưởng không nhận lại sau khởi động lại, và migration v2 → v4 rồi ghi nhận tiến độ.
    /// </summary>
    public sealed class ProgressSaveTests
    {
        private static readonly MoveTarget Meadow = MoveTarget.Zone("origin", "origin_meadow");

        private static GameState Reload(MemorySaveStore store)
        {
            LoadOutcome r = new SaveManager(store, () => PFix.T0).Load();
            Assert.That(r.Kind, Is.EqualTo(LoadKind.Loaded));
            return r.Save.State;
        }

        [Test]
        public void Tutorial_pending_pack_and_achievements_survive_a_v4_save_round_trip()
        {
            GameState s = PFix.Fresh();
            var clock = new FixedClock(s.Clock.LastProcessedAtMs + 1000);
            s = CommandDispatcher.Dispatch(s, new ViewBiologyProfileCommand(PFix.Rid(), "emberling"), clock).State;
            s = CommandDispatcher.Dispatch(s, new BuyTutorialPackCommand(PFix.Rid()), clock).State;
            Assert.That(s.PendingPack, Is.Not.Null);
            var (mgr, store) = PFix.Manager();
            mgr.Load();
            Assert.That(mgr.Save(s), Is.True);
            GameState back = Reload(store);
            Assert.That(PFix.Canonical(back), Is.EqualTo(PFix.Canonical(s)));
            Assert.That((back.Tutorial.StarterPackUsed, back.PendingPack.SpeciesId, back.Tutorial.ProfilesViewed.Single()), Is.EqualTo((true, "sprout", "emberling")));

            DispatchResult claimed = CommandDispatcher.Dispatch(back, new ClaimPackCommand(PFix.Rid(), Meadow), clock);
            Assert.That(claimed.Ok, Is.True, claimed.Error?.ToString());
            Assert.That(mgr.Save(claimed.State), Is.True);
            GameState again = Reload(store);
            Assert.That(PFix.Canonical(again), Is.EqualTo(PFix.Canonical(claimed.State)));
            Assert.That(again.UnlockedAchievements, Is.EqualTo(new[] { "ACH_FIRST_HATCH" }));
            Assert.That(again.ClaimedAchievements, Is.EqualTo(new[] { "ACH_FIRST_HATCH" }));
        }

        /// <summary>Save C# giữa và sau luồng hướng dẫn, xuất cho bộ đọc TypeScript (CI: verify-csharp-saves.test.ts).</summary>
        [Test]
        public void Progress_saves_are_exported_for_the_TypeScript_reader()
        {
            string dir = Environment.GetEnvironmentVariable("DRAVIO_EXPORT_DIR");
            GameState s = PFix.Fresh(11);
            var clock = new FixedClock(s.Clock.LastProcessedAtMs);
            GameState Do(GameState st, GameCommand c, long advance = 0)
            {
                clock.Set(clock.NowMs() + advance);
                DispatchResult r = CommandDispatcher.Dispatch(st, c, clock);
                Assert.That(r.Ok, Is.True, c.Type + ": " + r.Error);
                return r.State;
            }
            s = Do(s, new AdvanceTimeCommand(PFix.Rid(), AdvanceMode.Online), 16_000);
            s = Do(s, new ViewBiologyProfileCommand(PFix.Rid(), "emberling"));
            GameState pending = Do(s, new BuyTutorialPackCommand(PFix.Rid()));
            GameState claimed = Do(pending, new ClaimPackCommand(PFix.Rid(), Meadow));
            GameState skipped = Do(Do(claimed, new AdvanceTimeCommand(PFix.Rid(), AdvanceMode.Online), 300_000), new SetTutorialStatusCommand(PFix.Rid(), TutorialStatus.Skipped));
            int n = 0;
            foreach (var (name, st) in new[] { ("u24-pending-pack", pending), ("u24-claimed", claimed), ("u24-adult-skipped", skipped) })
            {
                string text = SaveSchema.Serialize(st, PFix.T0 + n++);
                ParseResult back = SaveFile.Parse(text);
                Assert.That(back.Ok, Is.True, name);
                Assert.That(PFix.Canonical(back.Save.State), Is.EqualTo(PFix.Canonical(SaveSchema.ToPersistedState(st))), name);
                if (dir == null) continue;
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, name + ".save.json"), text, new System.Text.UTF8Encoding(false));
                File.WriteAllText(Path.Combine(dir, name + ".canonical.txt"), PFix.Canonical(back.Save.State), new System.Text.UTF8Encoding(false));
            }
            Assert.That(skipped.UnlockedAchievements, Is.EqualTo(new[] { "ACH_FIRST_HATCH", "ACH_FIRST_ADULT" }));
        }

        [Test]
        public void Session_saves_immediately_after_buying_and_claiming_the_pack()
        {
            var store = new MemorySaveStore();
            var rig = new SessionRig(store, PFix.T0);
            GameSession s = rig.NewSession();
            int w = store.Writes;
            Assert.That(s.Send(id => new BuyTutorialPackCommand(id)), Is.Null);
            Assert.That(store.Writes, Is.EqualTo(w + 1), "mua gói lưu ngay, không đợi autosave");
            Assert.That(Reload(store).PendingPack, Is.Not.Null);
            Assert.That(s.Send(id => new ClaimPackCommand(id, Meadow)), Is.Null);
            Assert.That(store.Writes, Is.EqualTo(w + 2));
            Assert.That(Reload(store).Dragons.Any(d => d.SpeciesId == "sprout"), Is.True);
            // Lệnh bị từ chối không ghi; lệnh thường đợi autosave.
            Assert.That(s.Send(id => new BuyTutorialPackCommand(id)).Code, Is.EqualTo(DomainErrorCode.TutorialPackUsed));
            Assert.That(s.Send(id => new ViewBiologyProfileCommand(id, "emberling")), Is.Null);
            Assert.That(store.Writes, Is.EqualTo(w + 2));
            s.Dispose();
        }

        [Test]
        public void Rewards_are_not_granted_again_after_restarts()
        {
            var store = new MemorySaveStore();
            var rig = new SessionRig(store, PFix.T0);
            GameSession a = rig.NewSession();
            a.Send(id => new BuyTutorialPackCommand(id));
            a.Send(id => new ClaimPackCommand(id, MoveTarget.Storage));
            long goldAfterClaim = a.State.Player.Gold.Micro;
            Assert.That(a.State.UnlockedAchievements, Is.EqualTo(new[] { "ACH_FIRST_HATCH" }));
            a.Dispose();

            for (int restart = 0; restart < 3; restart++)
            {
                GameSession b = rig.NewSession();
                Assert.That(b.State.UnlockedAchievements, Is.EqualTo(new[] { "ACH_FIRST_HATCH" }));
                long earned0 = b.State.Player.TotalGoldEarned.Micro, gold0 = b.State.Player.Gold.Micro;
                rig.Run(2000);
                Assert.That(b.State.Player.Gold.Micro - gold0, Is.EqualTo(b.State.Player.TotalGoldEarned.Micro - earned0), "restart " + restart + ": no reward re-granted");
                Assert.That(b.State.Player.Stats.TotalHatched, Is.EqualTo(1));
                Assert.That(b.Send(id => new BuyTutorialPackCommand(id)).Code, Is.EqualTo(DomainErrorCode.TutorialPackUsed));
                b.SaveNow();
                b.Dispose();
            }
            Assert.That(goldAfterClaim, Is.GreaterThan(0));
        }

        [Test]
        public void V2_save_migrates_then_first_dispatch_credits_progress_in_order_once()
        {
            var store = new MemorySaveStore();
            store.Slots[SaveSlot.Main] = PFix.Fixture("save-schema-v2.json");
            LoadOutcome r = new SaveManager(store, () => PFix.T0).Load();
            Assert.That((r.Kind, r.Save.MigratedFrom), Is.EqualTo((LoadKind.Loaded, (int?)2)));
            GameState s = r.Save.State;
            Assert.That((s.Tutorial.Status, s.Tutorial.Completed.Count, s.Tutorial.StartedAtMs), Is.EqualTo((TutorialStatus.Active, 0, s.Clock.LastProcessedAtMs)));
            Assert.That((s.PendingPack, s.UnlockedAchievements.Count, s.ClaimedAchievements.Count), Is.EqualTo(((PendingPack)null, 0, 0)), "migration không cấp gì");

            var clock = new FixedClock(s.Clock.LastProcessedAtMs + 15_000);
            DispatchResult a = CommandDispatcher.Dispatch(s, new AdvanceTimeCommand(PFix.Rid(), AdvanceMode.Online), clock);
            Assert.That(a.State.Tutorial.Completed, Is.EqualTo(new[] { "TUT_01", "TUT_02" }), "đã cho ăn ở v2 nhưng chưa xem hồ sơ → dừng ở TUT_03");
            Assert.That(a.State.UnlockedAchievements, Is.EqualTo(new[] { "ACH_FIRST_ADULT" }));
            Assert.That(a.State.Player.Gold.Micro - s.Player.Gold.Micro - (a.State.Player.TotalGoldEarned.Micro - s.Player.TotalGoldEarned.Micro), Is.EqualTo(100_000_000));
            DispatchResult b = CommandDispatcher.Dispatch(a.State, new ViewBiologyProfileCommand(PFix.Rid(), "emberling"), clock);
            Assert.That(b.State.Tutorial.Completed, Is.EqualTo(new[] { "TUT_01", "TUT_02", "TUT_03", "TUT_04" }));
            Assert.That(b.Events.OfType<AchievementUnlocked>(), Is.Empty);
        }
    }
}
