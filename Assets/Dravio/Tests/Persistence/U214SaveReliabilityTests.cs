using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Dravio.Domain;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Economy;
using Dravio.Domain.Tests.Simulation;
using Dravio.Domain.World;
using Dravio.Persistence.Json;
using Dravio.Persistence.Schema;
using Dravio.Persistence.Storage;
using Dravio.Persistence.Tests.Support;
using Dravio.Platform;
using NUnit.Framework;

namespace Dravio.Persistence.Tests
{
    /// <summary>
    /// U2.14 — Save Schema v10 và độ tin cậy trên save THẬT của hành trình Alpha (Guided, 3 × 10 phút/ngày, 40 ngày, chỉ lệnh chính thức).
    /// Không đổi schema. Mỗi test ghi rõ kịch bản; số liệu thời gian / kích thước in ra console để đưa vào SAVE_RELIABILITY.md.
    /// </summary>
    public sealed class U214SaveReliabilityTests
    {
        private static readonly Schedule ThreeTimesTen = new Schedule("3 × 10 phút/ngày", (0, 10), (4, 10), (11, 10));
        private static AlphaJourney _journey;
        private static readonly object Gate = new object();

        /// <summary>Hành trình có log lệnh và bản chụp state ở các mốc gameplay (chạy một lần cho cả lớp).</summary>
        internal static AlphaJourney Journey()
        {
            lock (Gate)
                return _journey ?? (_journey = AlphaJourney.Run(Policy.Guided, ThreeTimesTen, 1, 40, keepLog: true, snapshotStages: AlphaJourney.DefaultStages));
        }

        private static string Canon(GameState s) => PFix.Canonical(SaveSchema.ToPersistedState(s));

        private static GameState Reload(GameState s, out string text)
        {
            text = SaveSchema.Serialize(s, s.Clock.LastProcessedAtMs);
            ParseResult r = SaveFile.Parse(text);
            Assert.That(r.Ok, Is.True, r.Code + ": " + string.Join("; ", r.Details ?? Array.Empty<string>()));
            return r.Save.State;
        }

        private static IEnumerable<KeyValuePair<string, GameState>> Stages()
        {
            AlphaJourney j = Journey();
            foreach (var kv in j.StageSnapshots) yield return kv;
            yield return new KeyValuePair<string, GameState>("end-of-day-40", j.Sim.State);
        }

        // ================================================================== Schema v10 ở mọi giai đoạn

        [Test]
        public void Schema_v10_save_round_trips_byte_for_byte_at_every_alpha_stage()
        {
            AlphaJourney j = Journey();
            Assert.That(j.Violations, Is.Empty);
            Assert.That(j.StageSnapshots.Keys, Is.EquivalentTo(AlphaJourney.DefaultStages), "hành trình đạt mọi giai đoạn cần chụp");
            foreach (var (stage, s) in Stages().Select(kv => (kv.Key, kv.Value)))
            {
                Assert.That(s.SchemaVersion, Is.EqualTo(10), stage);
                var sw = Stopwatch.StartNew();
                GameState back = Reload(s, out string text);
                long ms = sw.ElapsedMilliseconds;
                Assert.That(Canon(back), Is.EqualTo(Canon(s)), stage);
                Assert.That(SaveSchema.Serialize(back, s.Clock.LastProcessedAtMs), Is.EqualTo(text), stage + ": ghi lại trùng từng byte");
                Assert.That(GameStateValidator.Validate(back).IsValid, Is.True, stage + ": " + GameStateValidator.Validate(back));
                TestContext.Out.WriteLine($"STAGE|{stage}|{(stage == "end-of-day-40" ? 40 : j.FirstAt[stage] / 24):0.00}|{s.Dragons.Count}|{System.Text.Encoding.UTF8.GetByteCount(text)}|{ms}");
            }
        }

        [Test]
        public void A_reloaded_save_plays_on_exactly_like_the_live_state()
        {
            foreach (var kv in Stages())
            {
                GameState live = SaveSchema.ToPersistedState(kv.Value);
                GameState back = Reload(kv.Value, out _);
                long t = live.Clock.LastProcessedAtMs;
                foreach (GameCommand c in new GameCommand[] { new AdvanceTimeCommand("u214-on-" + kv.Key, AdvanceMode.Online), new AdvanceTimeCommand("u214-off-" + kv.Key, AdvanceMode.Offline) })
                {
                    t += 6 * 3_600_000L;
                    DispatchResult a = CommandDispatcher.Dispatch(live, c, new FixedClock(t));
                    DispatchResult b = CommandDispatcher.Dispatch(back, c, new FixedClock(t));
                    Assert.That(b.Ok, Is.EqualTo(a.Ok), kv.Key);
                    live = a.State; back = b.State;
                    Assert.That(Canon(back), Is.EqualTo(Canon(live)), kv.Key + " sau " + c.Type);
                }
            }
        }

        [Test]
        public void The_alpha_journey_is_a_pure_function_of_its_command_log()
        {
            AlphaJourney j = Journey();
            Assert.That(j.Log.Count, Is.EqualTo(j.Commands));
            GameState replayed = j.Replay();
            Assert.That(Canon(replayed), Is.EqualTo(Canon(j.Sim.State)), "không có thay đổi GameState nào ngoài CommandDispatcher");
        }

        // ================================================================== offline

        [Test]
        public void Offline_progression_from_a_late_save_is_capped_at_24_hours_75_percent_and_paid_once()
        {
            GameState s = Journey().Sim.State;
            var store = new MemorySaveStore();
            var mgr = new SaveManager(store, () => s.Clock.LastProcessedAtMs);
            mgr.Load();
            Assert.That(mgr.Save(s), Is.True);
            long bound = 0;
            foreach (DragonState d in s.Dragons)
                if (s.ProductionDragonIds.Contains(d.Uid) && d.Location.Kind == LocationKind.Zone)
                    bound += EconomyRates.RateMicroGPerSec(SpeciesCatalog.Get(d.SpeciesId), GrowthStage.Adult, d.Mutation);
            var rig = new SessionRig(store, s.Clock.LastProcessedAtMs + 30 * 3_600_000L);
            GameSession first = rig.NewSession();
            long gained = first.State.Player.Gold.Micro - s.Player.Gold.Micro;
            Assert.That(gained, Is.GreaterThan(0));
            Assert.That(gained, Is.LessThanOrEqualTo(bound * 86_400L * 3 / 4 + 1), "trần 24 h × 75 %");
            Assert.That(first.State.Clock.LastProcessedAtMs, Is.EqualTo(rig.Clock.Now));
            if (s.Expedition != null) Assert.That(first.State.Expedition.Status, Is.EqualTo(ExpeditionStatus.Ready), "chuyến đang đi hoàn tất khi offline");
            first.Dispose();
            GameSession again = rig.NewSession();
            Assert.That(again.State.Player.Gold.Micro, Is.EqualTo(first.State.Player.Gold.Micro), "mở lại ngay: không trả offline lần hai");
            TestContext.Out.WriteLine($"OFFLINE|gained {gained / 1_000_000} G|bound {bound * 86_400L * 3 / 4 / 1_000_000} G|expedition {(s.Expedition == null ? "none" : s.Expedition.RouteId)}");
        }

        // ================================================================== crash recovery

        [Test]
        public void Power_loss_at_every_write_step_of_late_game_saves_never_loses_progress()
        {
            GameState[] chain = Stages().Select(kv => kv.Value).OrderBy(s => s.Revision).ToArray();
            Assert.That(chain.Length, Is.GreaterThanOrEqualTo(4));
            GameState prev = chain[chain.Length - 2], next = chain[chain.Length - 1];
            int crashPoints = 0;
            for (int k = 0; ; k++)
            {
                var fs = new MemoryFileSystem();
                var mgr = new SaveManager(new FileSaveStore("/saves", fs), () => PFix.T0);
                mgr.Load();
                for (int i = 0; i < chain.Length - 1; i++) mgr.Save(chain[i]);
                fs.CrashAfterOperations = fs.MutatingOperations + k;
                bool crashed;
                try { mgr.Save(next); crashed = fs.Crashed; }
                catch (SaveWriteException) { crashed = true; }
                fs.Reboot();
                LoadOutcome r = new SaveManager(new FileSaveStore("/saves", fs), () => PFix.T0).Load();
                Assert.That(r.Kind, Is.EqualTo(LoadKind.Loaded), "crash point " + k);
                long rev = r.Save.State.Revision;
                Assert.That(rev == prev.Revision || rev == next.Revision, Is.True, "crash point " + k);
                Assert.That(Canon(r.Save.State), Is.EqualTo(Canon(rev == next.Revision ? next : prev)), "crash point " + k);
                if (!crashed) break;
                crashPoints++;
            }
            Assert.That(crashPoints, Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public void Corrupted_main_falls_back_to_the_newest_valid_backup_of_the_journey()
        {
            GameState[] chain = Stages().Select(kv => kv.Value).OrderBy(s => s.Revision).ToArray();
            var store = new MemorySaveStore();
            var mgr = new SaveManager(store, () => PFix.T0);
            mgr.Load();
            foreach (GameState s in chain) mgr.Save(s);
            string main = store.Get(SaveSlot.Main);
            store.Slots[SaveSlot.Main] = main.Substring(0, main.Length / 2);
            LoadOutcome r = new SaveManager(store, () => PFix.T0).Load();
            Assert.That((r.Kind, r.Slot), Is.EqualTo((LoadKind.Loaded, SaveSlot.Backup1)));
            Assert.That(r.Save.State.Revision, Is.EqualTo(chain[chain.Length - 2].Revision), "mất tối đa một lần lưu");
            Assert.That(r.Failures.Select(f => f.Code), Has.Member(SaveErrorCode.CorruptJson));
        }

        // ================================================================== duplicate request

        [Test]
        public void Request_ids_survive_save_load_so_a_retried_command_is_never_applied_twice()
        {
            AlphaJourney j = Journey();
            GameState back = Reload(j.Sim.State, out _);
            Assert.That(back.ProcessedRequestIds, Is.EqualTo(j.Sim.State.ProcessedRequestIds));
            Assert.That(back.ProcessedRequestIds.Count, Is.LessThanOrEqualTo(200));
            var clock = new FixedClock(back.Clock.LastProcessedAtMs + 60_000);
            // Gửi lại đúng các lệnh cuối của hành trình (cùng requestId) sau khi nạp lại: trùng, không event, không đổi state.
            var retried = j.Log.Where(e => back.ProcessedRequestIds.Contains(e.cmd.RequestId)).Skip(Math.Max(0, back.ProcessedRequestIds.Count - 50)).ToList();
            Assert.That(retried, Is.Not.Empty);
            foreach (var (cmd, _) in retried)
            {
                DispatchResult r = CommandDispatcher.Dispatch(back, cmd, clock);
                Assert.That((r.Ok, r.Duplicate, r.Events.Count, ReferenceEquals(r.State, back)), Is.EqualTo((true, true, 0, true)), cmd.Type);
            }
        }

        // ================================================================== kho đầy, hàng chờ trứng, 500 rồng

        /// <summary>Save 500 rồng (giới hạn kho), Khởi Nguyên đủ sinh cảnh, mở Thủy Vực khi kho đầy → trứng thế giới vào hàng chờ.</summary>
        private static GameState FullStorageWithQueuedEgg()
        {
            GameState s = PFix.Advanced(PFix.Fresh(5), 300_000).DeepClone();
            for (int i = s.Dragons.Count; i < StorageCatalog.MaxDragons; i++)
            {
                DragonState d = s.Dragons[0].Clone();
                d.Uid = "fill-" + i.ToString("000", CultureInfo.InvariantCulture);
                d.Location = DragonLocation.Storage;
                d.Nickname = null;
                s.Dragons.Add(d);
            }
            s.Zones.Single(z => z.Id == "origin").UnlockedHabitatIds = Zones.HabitatsOfZone("origin");
            foreach (string disc in ProgressCatalog.DiscoveryIds) if (!s.Discoveries.Contains(disc)) s.Discoveries.Add(disc);
            s.Discoveries.Sort(StringComparer.Ordinal);
            s.Tutorial.Status = TutorialStatus.Completed;
            s.Tutorial.Completed = new List<string>(ProgressCatalog.TutorialStepIds);
            s.CompletedQuests = new List<string> { "QUEST_AQUATIC_EXPLORATION" };
            s.CompletedSideQuests = new List<string> { "Q_ORIGIN_FIELD_NOTES" };
            // Mở Thủy Vực bằng lệnh thật khi kho đã đầy: trứng thế giới phải vào hàng chờ.
            DispatchResult r = CommandDispatcher.Dispatch(s, new UnlockZoneCommand("u214-ocean", "ocean"), new FixedClock(s.Clock.LastProcessedAtMs + 1000));
            Assert.That(r.Ok, Is.True, r.Error?.ToString());
            Assert.That(r.Events.OfType<RewardEggGranted>().Single().Delivered, Is.False);
            return r.State;
        }

        [Test]
        public void Five_hundred_dragons_with_a_queued_reward_egg_save_load_and_deliver_once_room_is_made()
        {
            GameState s = FullStorageWithQueuedEgg();
            Assert.That(s.Dragons.Count, Is.EqualTo(StorageCatalog.MaxDragons));
            Assert.That(s.RewardQueue.Select(q => q.RewardId), Is.EqualTo(new[] { "ZONE_EGG_OCEAN" }), "kho đầy → trứng chờ");
            Assert.That(s.PendingPack, Is.Null);
            Assert.That(GameStateValidator.Validate(s).IsValid, Is.True, GameStateValidator.Validate(s).ToString());
            var sw = Stopwatch.StartNew();
            string text = SaveSchema.Serialize(s, PFix.T0);
            long writeMs = sw.ElapsedMilliseconds; sw.Restart();
            ParseResult r = SaveFile.Parse(text);
            long readMs = sw.ElapsedMilliseconds;
            Assert.That(r.Ok, Is.True);
            int bytes = System.Text.Encoding.UTF8.GetByteCount(text);
            Assert.That(bytes, Is.LessThan(SaveSchema.MaxSaveBytes / 4), "500 rồng còn xa trần 5 MB");
            GameState back = r.Save.State;
            Assert.That(back.RewardQueue.Single().SpeciesId, Is.EqualTo("droplet"));
            var clock = new FixedClock(back.Clock.LastProcessedAtMs + 1000);
            // Kho đầy: gói bị từ chối, không trừ vàng; trứng vẫn chờ.
            DispatchResult buy = CommandDispatcher.Dispatch(back, new BuyPackCommand("u214-buy", "starter"), clock);
            Assert.That(buy.Error?.Code, Is.EqualTo(DomainErrorCode.StorageFull));
            // Bán một rồng → lệnh đó giao trứng chờ (một lần), save vẫn hợp lệ, nạp lại không giao lần nữa.
            DispatchResult sold = CommandDispatcher.Dispatch(back, new SellDragonCommand("u214-sell", "fill-499"), clock);
            Assert.That(sold.Ok, Is.True, sold.Error?.ToString());
            Assert.That(sold.Events.OfType<RewardEggDelivered>().Single().RewardId, Is.EqualTo("ZONE_EGG_OCEAN"));
            Assert.That((sold.State.RewardQueue.Count, sold.State.PendingPack?.SpeciesId), Is.EqualTo((0, "droplet")));
            GameState again = Reload(sold.State, out _);
            DispatchResult sold2 = CommandDispatcher.Dispatch(again, new SellDragonCommand("u214-sell-2", "fill-498"), clock);
            Assert.That(sold2.Events.OfType<RewardEggDelivered>(), Is.Empty);
            Assert.That(again.GrantedRewards.Count(g => g == "ZONE_EGG_OCEAN"), Is.EqualTo(1));
            TestContext.Out.WriteLine($"FULL500|{bytes} bytes|write {writeMs} ms|parse {readMs} ms");
        }

        [Test]
        public void A_save_above_500_dragons_is_reported_as_observed_behaviour()
        {
            GameState s = FullStorageWithQueuedEgg().DeepClone();
            DragonState extra = s.Dragons[0].Clone();
            extra.Uid = "extra-501";
            extra.Location = DragonLocation.Storage;
            s.Dragons.Add(extra);
            ValidationResult v = GameStateValidator.Validate(s);
            // Ghi lại hành vi hiện hành (không đổi luật trong U2.14): validator KHÔNG có trần số rồng; mọi lệnh tạo rồng mới bị chặn ở > 500.
            TestContext.Out.WriteLine("OVER500|validator " + (v.IsValid ? "accepts" : "rejects: " + v));
            Assert.That(v.IsValid, Is.True, "nếu đổi: cập nhật SAVE_RELIABILITY.md mục 501 rồng");
            DispatchResult buy = CommandDispatcher.Dispatch(s, new BuyPackCommand("u214-501", "starter"), new FixedClock(s.Clock.LastProcessedAtMs + 1000));
            Assert.That(buy.Error?.Code, Is.EqualTo(DomainErrorCode.StorageFull));
        }

        // ================================================================== phiên read-only

        [Test]
        public void A_second_session_on_a_late_game_save_is_read_only_and_never_writes_or_imports()
        {
            GameState s = Journey().Sim.State;
            var store = new MemorySaveStore();
            var mgr = new SaveManager(store, () => s.Clock.LastProcessedAtMs);
            mgr.Load();
            mgr.Save(s);
            var held = new HashSet<string>();
            GameSession a = new SessionRig(store, s.Clock.LastProcessedAtMs + 60_000, new InMemoryWriterLock(held)).NewSession();
            int writes = store.Writes;
            string disk = store.Get(SaveSlot.Main);
            var rigB = new SessionRig(store, s.Clock.LastProcessedAtMs + 3_600_000, new InMemoryWriterLock(held));
            GameSession b = rigB.NewSession();
            Assert.That(b.Status, Is.EqualTo(SessionStatus.ReadOnly));
            long gold = b.State.Player.Gold.Micro;
            string uid = b.State.Dragons.First(d => d.Location.Kind != LocationKind.Released).Uid;
            Assert.That(b.Send(id => new PetCommand(id, uid)), Is.Null, "read-only: lệnh bị bỏ qua, không lỗi");
            Assert.That(b.Send(id => new BuildSanctuaryProjectCommand(id)), Is.Null);
            rigB.Run(60_000);
            b.SaveNow();
            b.PrepareImport(new SaveManager(new MemorySaveStore(), () => PFix.T0).ExportText(PFix.Fresh(3)));
            b.ConfirmImport();
            Assert.That(b.State.Player.Gold.Micro, Is.EqualTo(gold));
            Assert.That(b.State.SaveId, Is.EqualTo(s.SaveId), "read-only không nhập save đè");
            Assert.That(store.Writes, Is.EqualTo(writes));
            Assert.That(store.Get(SaveSlot.Main), Is.EqualTo(disk));
            Assert.That(a.Status, Is.EqualTo(SessionStatus.Ready));
        }

        // ================================================================== lỗi nhập save

        private static string WithState(string envelopeText, Action<JsonObject> edit, bool fixChecksum)
        {
            var env = (JsonObject)JsonParser.Parse(envelopeText);
            var state = ((JsonObject)env.Get("state")).ShallowCopy();
            edit(state);
            var o = new JsonObject();
            foreach (var kv in env)
            {
                if (kv.Key == "state") o.Set(kv.Key, state);
                else if (kv.Key == "checksum" && fixChecksum) o.Set(kv.Key, JsonValue.From(SaveSchema.ChecksumOf(state)));
                else o.Set(kv.Key, kv.Value);
            }
            return JsonWriter.Stringify(o);
        }

        [Test]
        public void Import_errors_are_reported_with_their_code_and_never_touch_the_current_game()
        {
            AlphaJourney j = Journey();
            GameState current = j.Sim.State;
            var store = new MemorySaveStore();
            var mgr = new SaveManager(store, () => current.Clock.LastProcessedAtMs);
            mgr.Load();
            mgr.Save(current);
            var rig = new SessionRig(store, current.Clock.LastProcessedAtMs + 1000);
            GameSession session = rig.NewSession();
            string before = Canon(session.State);
            int writes = store.Writes;
            string good = new SaveManager(new MemorySaveStore(), () => PFix.T0).ExportText(j.StageSnapshots["zone:ocean"]);
            var cases = new (string name, string text, SaveErrorCode code)[]
            {
                ("không phải JSON", "{not json", SaveErrorCode.CorruptJson),
                ("file bị cắt", good.Substring(0, good.Length - 40), SaveErrorCode.CorruptJson),
                ("JSON sai định dạng", "{\"format\":\"other\"}", SaveErrorCode.BadFormat),
                ("sửa tay vàng, checksum cũ", WithState(good, st => st.Set("revision", JsonValue.From(999999)), fixChecksum: false), SaveErrorCode.BadChecksum),
                ("save phiên bản mới hơn", PFix.EnvelopeAt(11), SaveErrorCode.NewerVersion),
                ("trứng Aurorawing giả, checksum hợp lệ", WithState(good, st =>
                {
                    var g = new JsonArray();
                    foreach (JsonValue v in (JsonArray)st.Get("grantedRewards")) g.Add(v);
                    g.Add(JsonValue.From(SkyCloudBalance.AurorawingEggRewardId));
                    st.Set("grantedRewards", g);
                }, fixChecksum: true), SaveErrorCode.InvalidState),
                ("quá 5 MB", new string(' ', SaveSchema.MaxSaveBytes + 1), SaveErrorCode.TooLarge),
            };
            foreach (var (name, text, code) in cases)
            {
                session.PrepareImport(text);
                Assert.That(session.ImportError, Is.EqualTo(code), name);
                Assert.That(session.ImportPreview, Is.Null, name);
                session.ConfirmImport();
                session.CancelImport();
                Assert.That(Canon(session.State), Is.EqualTo(before), name + ": game hiện tại không đổi");
                Assert.That(store.Writes, Is.EqualTo(writes), name + ": không ghi");
                TestContext.Out.WriteLine($"IMPORT|{name}|{code.ToWire()}");
            }
            // Save hợp lệ của chính hành trình vẫn nhập được (đối chứng).
            session.PrepareImport(good);
            Assert.That(session.ImportError, Is.Null);
            Assert.That(session.ImportPreview, Is.Not.Null);
        }
    }
}
