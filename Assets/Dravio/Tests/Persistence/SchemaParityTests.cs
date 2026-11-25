using System;
using System.Collections.Generic;
using System.Linq;
using Dravio.Domain;
using Dravio.Persistence.Json;
using Dravio.Persistence.Schema;
using Dravio.Persistence.Tests.Support;
using NUnit.Framework;

namespace Dravio.Persistence.Tests
{
    /// <summary>
    /// Đối chiếu với golden sinh từ persistence TypeScript gốc: canonicalJson, SHA-256, serializeSave, parseSaveText
    /// trên 54 file (hợp lệ, hỏng, sửa tay, phiên bản khác, migration, fixture thật v1/v2).
    /// U2.5: bản Unity ghi Schema v5 (= v4 + breedingNest). Golden TS dừng ở v4, nên mỗi phép so dùng ảnh v5 của kết quả TS
    /// (PFix.AsV5 = đúng bước migration 4 → 5) hoặc chiếu save C# về v4 (PFix.AsV4 khi tổ trống) — phần chung vẫn phải trùng từng byte.
    /// </summary>
    public sealed class SchemaParityTests
    {
        private static IEnumerable<JsonObject> Items(string key) => ((JsonArray)PFix.Golden.Get(key)).Cast<JsonObject>();
        private static string Str(JsonObject o, string k) => ((JsonString)o.Get(k)).Value;

        [Test]
        public void Golden_targets_the_reference_commit()
        {
            Assert.That(Str(PFix.Golden, "source"), Is.EqualTo("nldanh13/dravio@" + Dravio.Domain.Catalog.ContentInfo.ReferenceCommit));
        }

        [Test]
        public void Canonical_json_and_sha256_match_TypeScript_for_every_golden_state()
        {
            foreach (JsonObject e in Items("envelopes"))
            {
                var errors = new List<string>();
                GameState s = GameStateJson.FromJson(PFix.AsV5((JsonObject)e.Get("state")), errors);
                Assert.That(errors, Is.Empty, Str(e, "name"));
                string canonical = SaveSchema.CanonicalJson(PFix.AsV4(GameStateJson.ToJson(SaveSchema.ToPersistedState(s))));
                Assert.That(canonical, Is.EqualTo(Str(e, "canonical")), Str(e, "name"));
                Assert.That(SaveSchema.Sha256Hex(canonical), Is.EqualTo(Str(e, "checksum")), Str(e, "name"));
            }
        }

        [Test]
        public void Serialized_save_of_a_fresh_state_is_byte_identical_to_TypeScript()
        {
            // state-0..2 là createInitialState (cùng thứ tự khóa với TS), nên toàn bộ chuỗi phải trùng.
            int i = 0;
            foreach (JsonObject e in Items("envelopes").Take(3))
            {
                var errors = new List<string>();
                GameState s = GameStateJson.FromJson(PFix.AsV5((JsonObject)e.Get("state")), errors);
                string v5 = SaveSchema.Serialize(s, PFix.T0 + 1000 + i);
                Assert.That(((JsonNumber)((JsonObject)JsonParser.Parse(v5)).Get("schemaVersion")).Value, Is.EqualTo(5));
                Assert.That(PFix.AsV4Text(v5), Is.EqualTo(Str(e, "text")), Str(e, "name"));
                i++;
            }
        }

        [Test]
        public void TypeScript_written_saves_load_in_CSharp_and_CSharp_saves_keep_the_same_checksum()
        {
            foreach (JsonObject e in Items("envelopes"))
            {
                ParseResult r = SaveFile.Parse(Str(e, "text"));
                Assert.That(r.Ok, Is.True, Str(e, "name") + ": " + r);
                Assert.That(r.Save.MigratedFrom, Is.EqualTo(4), "save v4 của TS đi qua migration 4 → 5");
                string again = SaveSchema.Serialize(r.Save.State, 1);
                var env = (JsonObject)JsonParser.Parse(PFix.AsV4Text(again));
                Assert.That(Str(env, "checksum"), Is.EqualTo(Str(e, "checksum")), Str(e, "name"));
            }
        }

        /// <summary>Bỏ khóa mà model C# có kiểu không giữ (khóa lạ của state/rồng), để so phần chung.</summary>
        private static JsonObject KnownKeysOnly(JsonObject state)
        {
            var o = new JsonObject();
            foreach (var kv in state)
            {
                if (!GameStateJson.StateKeys.Contains(kv.Key)) continue;
                if (kv.Key == "dragons" && kv.Value is JsonArray da)
                {
                    var arr = new JsonArray();
                    foreach (JsonObject d in da)
                    {
                        var dd = new JsonObject();
                        foreach (var dk in d) if (GameStateJson.DragonKeys.Contains(dk.Key) || dk.Key == "registryRef") dd.Set(dk.Key, dk.Value);
                        arr.Add(dd);
                    }
                    o.Set(kv.Key, arr);
                }
                else o.Set(kv.Key, kv.Value);
            }
            return o;
        }

        private static IEnumerable<TestCaseData> ParseCases()
        {
            foreach (JsonObject c in Items("parsed")) yield return new TestCaseData(Str(c, "name")).SetName("Parse_" + Str(c, "name"));
        }

        [TestCaseSource(nameof(ParseCases))]
        public void parseSaveText_outcome_matches_TypeScript(string name)
        {
            JsonObject c = Items("parsed").Single(x => Str(x, "name") == name);
            ParseResult r = SaveFile.Parse(Str(c, "text"));
            bool ok = ((JsonBool)c.Get("ok")).Value;
            if (name == "v5-newer")
            {
                // Khác biệt có chủ đích: với TS v5 là "bản mới hơn"; với Unity v5 là hiện tại, và file golden này (v4 đổi số) thiếu breedingNest.
                Assert.That((r.Ok, r.Code), Is.EqualTo((false, SaveErrorCode.InvalidState)));
                Assert.That(r.Details, Does.Contain("breedingNest: invalid"));
                return;
            }
            Assert.That(r.Ok, Is.EqualTo(ok), r.ToString());
            if (ok)
            {
                // TS: migratedFrom null với save v4; Unity: mọi save TS đều đi thêm bước 4 → 5 nên migratedFrom = phiên bản gốc (≥ 1).
                double? migrated = c.Get("migratedFrom") is JsonNumber mn ? mn.Value : 4;
                Assert.That(r.Save.MigratedFrom.HasValue ? (double?)r.Save.MigratedFrom.Value : null, Is.EqualTo(migrated));
                Assert.That(r.Save.SavedAtMs, Is.EqualTo((long)((JsonNumber)c.Get("savedAtMs")).Value));
                string expected = SaveSchema.CanonicalJson(KnownKeysOnly(PFix.AsV5((JsonObject)c.Get("state"))));
                Assert.That(PFix.Canonical(r.Save.State), Is.EqualTo(expected));
                return;
            }
            Assert.That(r.Code.ToWire(), Is.EqualTo(Str(c, "code")));
            var tsDetails = ((JsonArray)c.Get("details")).Select(d => ((JsonString)d).Value).ToList();
            // Mọi lỗi TS báo đều phải có trong lỗi C# (C# có thể báo thêm lỗi kiểu phát hiện khi đổi sang model có kiểu).
            foreach (string d in tsDetails) Assert.That(r.Details, Does.Contain(d), "missing detail");
            if (r.Code != SaveErrorCode.InvalidState && r.Code != SaveErrorCode.MigrationFailed) Assert.That(r.Details, Is.EqualTo(tsDetails));
        }

        [Test]
        public void Oversize_text_is_rejected_before_parsing()
        {
            Assert.That(SaveFile.Parse("\"" + new string('x', SaveSchema.MaxSaveBytes) + "\"").Code, Is.EqualTo(SaveErrorCode.TooLarge));
            // Đếm theo byte UTF-8, không theo ký tự: 2 MB chữ "ệ" (3 byte) vượt 5 MB.
            Assert.That(SaveFile.Parse(new string('ệ', 2 * 1024 * 1024)).Code, Is.EqualTo(SaveErrorCode.TooLarge));
            Assert.That(SaveFile.Parse(null).Code, Is.EqualTo(SaveErrorCode.CorruptJson));
        }

        [Test]
        public void Transient_food_is_not_persisted_and_round_trip_preserves_state()
        {
            GameState s = PFix.Fresh();
            var clock = new Dravio.Domain.Core.FixedClock(PFix.T0 + 1000);
            var fed = Dravio.Domain.Core.CommandDispatcher.Dispatch(s, new Dravio.Domain.Core.FeedCommand("f1", DietId.Fruit, "origin", "origin_meadow"), clock);
            Assert.That(fed.Ok, Is.True);
            s = fed.State;
            Assert.That(s.Food.Drops.Count, Is.EqualTo(1));
            ParseResult r = SaveFile.Parse(SaveSchema.Serialize(s, PFix.T0 + 2000));
            Assert.That(r.Ok, Is.True);
            Assert.That(r.Save.State.Food.Drops, Is.Empty);
            Assert.That(r.Save.State.Food.LastSpawnAtMs, Is.Null);
            Assert.That(r.Save.State.Player.Gold, Is.EqualTo(s.Player.Gold));
            Assert.That(r.Save.SavedAtMs, Is.EqualTo(PFix.T0 + 2000));
            Assert.That(s.Food.Drops.Count, Is.EqualTo(1), "serialize must not mutate the live state");
            GameState expected = SaveSchema.ToPersistedState(s);
            Assert.That(PFix.Canonical(r.Save.State), Is.EqualTo(PFix.Canonical(expected)));
        }

        [Test]
        public void Import_sanitizer_drops_entitlements_and_Verified_like_TypeScript()
        {
            foreach (JsonObject rep in Items("importReports"))
            {
                string name = Str(rep, "name");
                ParseResult r = SaveFile.Parse(Str(Items("parsed").Single(x => Str(x, "name") == name), "text"));
                GameState clean = SaveFile.SanitizeImported(r.Save, out SaveFile.ImportSanitizeReport report);
                var exp = (JsonObject)rep.Get("report");
                Assert.That(report.DroppedStateKeys, Is.EqualTo(((JsonArray)exp.Get("droppedStateKeys")).Select(x => ((JsonString)x).Value)), name);
                Assert.That(report.DroppedDragonKeys, Is.EqualTo(((JsonArray)exp.Get("droppedDragonKeys")).Select(x => ((JsonString)x).Value)), name);
                Assert.That(PFix.Canonical(clean), Is.EqualTo(SaveSchema.CanonicalJson(KnownKeysOnly(PFix.AsV5((JsonObject)rep.Get("state"))))), name);
                Assert.That(clean.Dragons.All(d => d.RegistryRef == null), Is.True);
                var pv = (JsonObject)rep.Get("preview");
                SaveFile.ImportPreview p = SaveFile.PreviewOf(clean, r.Save.SavedAtMs, r.Save.MigratedFrom, Array.Empty<string>());
                Assert.That((p.SaveId, p.GoldMicro, p.DragonCount, p.SpeciesCount),
                    Is.EqualTo((Str(pv, "saveId"), (long)((JsonNumber)pv.Get("goldMicro")).Value, (int)((JsonNumber)pv.Get("dragonCount")).Value, (int)((JsonNumber)pv.Get("speciesCount")).Value)), name);
            }
        }

        [Test]
        public void Time_policy_matches_TypeScript()
        {
            foreach (JsonObject p in Items("policy"))
            {
                var mode = Dravio.Platform.TimePolicy.ModeFor((long)((JsonNumber)p.Get("last")).Value, (long)((JsonNumber)p.Get("now")).Value, ((JsonBool)p.Get("visible")).Value);
                Assert.That(mode.ToWire(), Is.EqualTo(Str(p, "mode")));
            }
        }
    }
}
