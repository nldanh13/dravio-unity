using System;
using System.Collections.Generic;
using System.Linq;
using Dravio.Domain;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Health;
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
    /// U2.6 × Save Schema v8: migration v7 → v8 không mất dữ liệu (save gốc giữ nguyên văn), ca chăm sóc / thiên tai / đồng hồ hazard qua vòng lưu–nạp,
    /// sửa tay bị từ chối, phiên lưu ngay khi có cảnh báo / ca mới và nạp lại không tạo lại ca.
    /// </summary>
    public sealed class HealthSaveTests
    {
        private const long Min = 60_000, Hour = 60 * Min;

        private static readonly DisasterRules Always = new DisasterRules(true, Hour, 1.0, new[]
        {
            new KeyValuePair<string, HazardId>("origin", HazardId.Wildfire),
            new KeyValuePair<string, HazardId>("ocean", HazardId.StrongCurrent),
            new KeyValuePair<string, HazardId>("sky", HazardId.Thunderstorm),
        }, EventSeverityPolicy.Uniform, DisasterRules.ProposedImpact);

        private static DragonState Adult(string uid, string species, DragonLocation loc = null) => new DragonState
        {
            Uid = uid, SpeciesId = species, Genotype = Gen(), Mutation = MutationType.Normal, XpSec = SpeciesCatalog.Get(species).GrowthTimeSec,
            Location = loc ?? DragonLocation.Storage, Personality = Personality.Calm, Generation = 1, Ancestry = Ancestry.Unknown, Origin = DragonOrigin.Pack,
            Activity = DragonActivity.Idle, Vitals = new Vitals(),
        };

        private static Genotype Gen() { var g = new Genotype(); g.Set("pigment", "R", "r"); g.Set("pattern", "P", "p"); g.Set("eye", "B", "b"); return g; }

        private static GameState Do(GameState s, GameCommand c, long now, DisasterRules r = null)
        {
            DispatchResult res = CommandDispatcher.Dispatch(s, c, new FixedClock(now), null, r);
            Assert.That(res.Ok, Is.True, c.Type + ": " + res.Error);
            return res.State;
        }

        /// <summary>Một ca đang chăm sóc (đã nhận định, đã đặt kế hoạch, đã bắt đầu) + một cảnh báo Forecast đang chạy, có rồng đã trú.</summary>
        private static GameState WithCareAndForecast()
        {
            GameState s = PFix.Fresh();
            s.Dragons.Add(Adult("z", "zephyr"));
            s.Dragons.Add(Adult("m", "sprout", DragonLocation.Zone("origin", "origin_meadow")));
            HealthCase c = new HealthCase
            {
                CaseId = "case-z", DragonId = "z", Cause = HazardId.Thunderstorm, Site = InjurySite.LimbWing, Severity = CaseSeverity.Moderate,
                Symptoms = new List<string>(HealthCatalog.Site(InjurySite.LimbWing).Symptoms), CreatedAtMs = s.Clock.LastProcessedAtMs, Status = CaseStatus.Active,
            };
            s.HealthCases.Add(c);
            s.FindDragon("z").Location = DragonLocation.Care("case-z");
            s.FindDragon("z").Activity = DragonActivity.Care;
            s.FindDragon("z").Vitals.Mobility = 40;
            long t = s.Clock.LastProcessedAtMs;
            s = Do(s, new AssessCaseCommand(PFix.Rid(), "case-z"), t);
            s = Do(s, new SetCarePlanCommand(PFix.Rid(), "case-z", "rest_protect_gradual"), t);
            s = Do(s, new StartCareCommand(PFix.Rid(), "case-z"), t);
            t += Hour;
            s = Do(s, new AdvanceTimeCommand(PFix.Rid(), AdvanceMode.Online), t, Always);
            Assert.That(s.Disasters.Single().Phase, Is.EqualTo(DisasterPhase.Forecast));
            s = Do(s, new MoveToShelterCommand(PFix.Rid(), "m"), t, Always);
            Assert.That(GameStateValidator.Validate(s).IsValid, Is.True, GameStateValidator.Validate(s).ToString());
            return s;
        }

        [Test]
        public void V7_save_migrates_to_v8_with_baseline_vitals_keeping_every_legacy_field_and_the_verbatim_archive()
        {
            GameState s = PFix.Fresh();
            s.Dragons.Add(Adult("sh", "sprout", DragonLocation.Shelter("origin")));   // vị trí cũ hợp lệ ở v7 (không có ca)
            s.Dragons.Add(Adult("cr", "sprout", DragonLocation.Care("legacy")));
            s.FindDragon("cr").Activity = DragonActivity.Care;
            s.Player.Inventory["wood"] = 5;
            s.SchemaVersion = 7;
            string v7 = PFix.EnvelopeAt(7, s);
            var v7State = (JsonObject)((JsonObject)JsonParser.Parse(v7)).Get("state");
            Assert.That(v7State.Get("healthCases"), Is.InstanceOf<JsonNull>().Or.Null, "fixture v7 thật sự không có trường v8");
            Assert.That(((JsonObject)((JsonArray)v7State.Get("dragons"))[0]).Get("vitals"), Is.InstanceOf<JsonNull>().Or.Null);

            var store = new MemorySaveStore();
            store.Slots[SaveSlot.Main] = v7;
            var mgr = new SaveManager(store, () => PFix.T0);
            LoadOutcome r = mgr.Load();
            Assert.That((r.Kind, r.Save.MigratedFrom), Is.EqualTo((LoadKind.Loaded, (int?)7)), r.Kind.ToString());
            GameState m = r.Save.State;
            Assert.That((m.SchemaVersion, m.HealthCases.Count, m.Disasters.Count, m.Hazard.ForegroundMs, m.Hazard.ChecksDone), Is.EqualTo((9, 0, 0, 0L, 0L)));
            foreach (DragonState d in m.Dragons)
                Assert.That((d.Vitals.Health, d.Vitals.Energy, d.Vitals.Comfort, d.Vitals.Mobility, d.Vitals.Hydration), Is.EqualTo((100, 100, 100, 100, 100)), d.Uid);
            Assert.That((m.FindDragon("sh").Location.Kind, m.FindDragon("cr").Location.CaseId, m.FindDragon("cr").Activity), Is.EqualTo((LocationKind.Shelter, "legacy", DragonActivity.Care)),
                "ADR 0202 D8: không tự di chuyển rồng của save cũ");
            Assert.That(m.Player.Inventory["wood"], Is.EqualTo(5L));
            // Mọi trường v7 còn nguyên: bỏ phần v8 thêm vào thì trùng từng byte với state v7 (trừ schemaVersion).
            JsonObject migrated = GameStateJson.ToJson(m);
            JsonObject strippedBack = (JsonObject)((JsonObject)JsonParser.Parse(PFix.EnvelopeAt(7, m))).Get("state");
            Assert.That(SaveSchema.CanonicalJson(strippedBack), Is.EqualTo(SaveSchema.CanonicalJson(v7State)));
            Assert.That(migrated.Get("hazard"), Is.Not.Null);

            Assert.That(mgr.Save(m), Is.True);
            Assert.That(store.GetArchive("archive:schema-7"), Is.EqualTo(v7), "save v7 gốc được giữ nguyên văn");
        }

        [Test]
        public void Older_saves_v1_and_v2_still_load_through_every_step_to_v9()
        {
            foreach (string f in new[] { "save-schema-v1.json", "save-schema-v2.json" })
            {
                var store = new MemorySaveStore();
                store.Slots[SaveSlot.Main] = PFix.Fixture(f);
                LoadOutcome r = new SaveManager(store, () => PFix.T0).Load();
                Assert.That(r.Kind, Is.EqualTo(LoadKind.Loaded), f);
                Assert.That(r.Save.State.SchemaVersion, Is.EqualTo(9), f);
                Assert.That(r.Save.State.Dragons.All(d => d.Vitals != null && d.Vitals.Health == 100), Is.True, f);
            }
        }

        [Test]
        public void Cases_disasters_shelter_and_hazard_clock_round_trip_byte_for_byte()
        {
            GameState s = WithCareAndForecast();
            long t = s.Clock.LastProcessedAtMs;
            string text = SaveSchema.Serialize(s, t);
            ParseResult back = SaveFile.Parse(text);
            Assert.That(back.Ok, Is.True, back.ToString());
            Assert.That(PFix.Canonical(back.Save.State), Is.EqualTo(PFix.Canonical(SaveSchema.ToPersistedState(s))));
            GameState b = back.Save.State;
            Assert.That((b.FindCase("case-z").CarePlanId, b.FindCase("case-z").Phase, b.Disasters.Single().Sheltered.Single().HabitatId, b.Hazard.ChecksDone),
                Is.EqualTo(("rest_protect_gradual", s.FindCase("case-z").Phase, "origin_meadow", 1L)));
            Assert.That(SaveSchema.Serialize(b, t), Is.EqualTo(text), "ghi lại không đổi một byte");
            Assert.That(() => PFix.AsV4((JsonObject)((JsonObject)JsonParser.Parse(text)).Get("state")), Throws.InvalidOperationException, "TS v4 không biểu diễn được ca mở");
            PFix.Export(Environment.GetEnvironmentVariable("DRAVIO_EXPORT_DIR"), "u26-care-forecast", text);

            // Đi tiếp sau khi nạp giống hệt đi tiếp không qua save.
            GameState direct = Do(s, new AdvanceTimeCommand("same", AdvanceMode.Online), t + 10 * Min, Always);
            GameState viaSave = Do(b, new AdvanceTimeCommand("same", AdvanceMode.Online), t + 10 * Min, Always);
            Assert.That(PFix.Canonical(viaSave), Is.EqualTo(PFix.Canonical(direct)));
        }

        [Test]
        public void Healthy_v8_saves_still_project_to_TS_v4()
        {
            GameState s = PFix.Fresh();
            s = PFix.Advanced(s, 5 * Min);
            string text = SaveSchema.Serialize(s, s.Clock.LastProcessedAtMs);
            Assert.That(SaveFile.Parse(PFix.AsV4Text(text)).Ok, Is.True);
            PFix.Export(Environment.GetEnvironmentVariable("DRAVIO_EXPORT_DIR"), "u26-healthy", text);
        }

        [Test]
        public void Hand_edited_health_data_is_rejected_as_invalid_state()
        {
            GameState s = WithCareAndForecast();
            long t = s.Clock.LastProcessedAtMs;
            void Bad(Action<GameState> edit, string why)
            {
                GameState c = s.DeepClone(); edit(c);
                ParseResult p = SaveFile.Parse(SaveSchema.Serialize(c, t));
                Assert.That(p.Ok, Is.False, why);
                Assert.That(p.Code, Is.EqualTo(SaveErrorCode.InvalidState), why);
            }
            Bad(c => c.FindDragon("z").Vitals.Health = 150, "chỉ số > 100");
            Bad(c => { c.FindCase("case-z").FinishAtMs -= Min; }, "rút ngắn chăm sóc");
            Bad(c => c.FindCase("case-z").Assessed = false, "kế hoạch đặt khi chưa nhận định");
            Bad(c => c.Disasters[0].Sheltered.Add(new ShelteredDragon { Uid = "ghost", HabitatId = "origin_meadow" }), "trú ẩn rồng không tồn tại");
            Bad(c => c.FindCase("case-z").DragonId = "m", "ca trỏ sang rồng khác");
        }

        [Test]
        public void Hand_edits_that_drop_vitals_or_corrupt_the_hazard_block_are_rejected()
        {
            GameState s = PFix.Fresh();
            string text = SaveSchema.Serialize(s, PFix.T0);
            var env = (JsonObject)JsonParser.Parse(text);
            var st = ((JsonObject)env.Get("state")).ShallowCopy();
            st.Set("hazard", JsonValue.From("oops"));
            string broken = JsonWriter.Stringify(env.ShallowCopy().Set("state", st).Set("checksum", JsonValue.From(SaveSchema.ChecksumOf(st))));
            Assert.That(SaveFile.Parse(broken).Ok, Is.False);
        }

        private static (SessionRig rig, MemorySaveStore store) RigWith(GameState s)
        {
            var store = new MemorySaveStore();
            var mgr = new SaveManager(store, () => s.Clock.LastProcessedAtMs);
            mgr.Load();
            mgr.Save(s);
            return (new SessionRig(store, s.Clock.LastProcessedAtMs) { Rules = Always }, store);
        }

        [Test]
        public void Session_saves_immediately_on_a_forecast_and_a_reload_after_quitting_keeps_the_same_event_and_auto_shelters()
        {
            GameState s = PFix.Fresh();
            s.Dragons.Add(Adult("m", "sprout", DragonLocation.Zone("origin", "origin_meadow")));
            s.Hazard.ForegroundMs = Hour - 15_000; // mốc kiểm rơi vào giây thứ 15, trước lượt autosave 30 s đầu tiên
            var (rig, store) = RigWith(s);
            GameSession a = rig.NewSession();
            rig.Run(14_000);
            Assert.That(a.State.Disasters, Is.Empty);
            int w = store.Writes;
            rig.Run(1000);
            Assert.That(a.State.Disasters.Count, Is.EqualTo(1));
            Assert.That(store.Writes, Is.EqualTo(w + 1), "cảnh báo lưu ngay");
            ParseResult saved = SaveFile.Parse(store.Slots[SaveSlot.Main]);
            Assert.That(saved.Save.State.Disasters.Single().Id, Is.EqualTo(a.State.Disasters.Single().Id));
            string evId = a.State.Disasters.Single().Id;
            a.Dispose(); // thoát game trong lúc cảnh báo

            rig.Frozen(3 * Hour);
            GameSession b = rig.NewSession();
            Assert.That(b.State.Disasters.Single().Id, Is.EqualTo(evId), "không tạo lại / không roll lại sự kiện");
            Assert.That(b.State.Disasters.Single().Sheltered.Select(x => x.Uid), Does.Contain("m"), "offline → tự đưa vào nơi trú");
            Assert.That(b.State.HealthCases, Is.Empty, "không gây thương offline");
            rig.Run(10 * Min);
            Assert.That(b.State.Disasters, Is.Empty, "đã trú đủ → không có ca, sự kiện đóng");
            Assert.That(b.State.FindDragon("m").Location.HabitatId, Is.EqualTo("origin_meadow"));
            b.Dispose();
            GameSession c = rig.NewSession();
            Assert.That((c.State.Disasters.Count, c.State.HealthCases.Count), Is.EqualTo((0, 0)));
            Assert.That(c.State.Hazard.CooldownOf("origin"), Is.GreaterThan(0));
            c.Dispose();
        }

        [Test]
        public void Care_commands_are_immediate_saves_in_the_session()
        {
            var cmds = new GameCommand[]
            {
                new MoveToShelterCommand("a", "x"), new AssessCaseCommand("b", "c"), new SetCarePlanCommand("c", "c", "p"),
                new StartCareCommand("d", "c"), new ReviewCaseCommand("e", "c"), new DischargeDragonCommand("f", "c", MoveTarget.Storage),
            };
            foreach (GameCommand c in cmds) Assert.That(GameSession.IsImmediateSave(c), Is.True, c.Type);
        }
    }
}
