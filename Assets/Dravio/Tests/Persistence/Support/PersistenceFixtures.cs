using System;
using System.Collections.Generic;
using System.IO;
using Dravio.Domain;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Persistence;
using Dravio.Persistence.Json;
using Dravio.Persistence.Schema;
using Dravio.Persistence.Storage;
using Dravio.Platform;

namespace Dravio.Persistence.Tests.Support
{
    public static class PFix
    {
        /// <summary>Date.UTC(2026, 10, 1, 8, 0, 0) như persistence.test.ts.</summary>
        public static readonly long T0 = new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

        public const string GoldenRelative = "Assets/Dravio/Tests/Persistence/Golden/ts-persistence-golden-b33ec7a.json";
        public const string FixturesRelative = "Assets/Dravio/Tests/Persistence/Fixtures";

        private static JsonObject _golden;
        public static JsonObject Golden => _golden ?? (_golden = (JsonObject)JsonParser.Parse(File.ReadAllText(Locate(GoldenRelative))));

        public static string Locate(string relative)
        {
            foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    string p = Path.Combine(dir.FullName, relative);
                    if (File.Exists(p) || Directory.Exists(p)) return p;
                    dir = dir.Parent;
                }
            }
            throw new FileNotFoundException(relative);
        }

        /// <summary>Đọc fixture nguyên văn (bytes UTF-8 → string, không bỏ gì).</summary>
        public static string Fixture(string name) => new System.Text.UTF8Encoding(false).GetString(File.ReadAllBytes(Path.Combine(Locate(FixturesRelative), name)));

        public static GameState Fresh(uint seed = 7) => InitialStateFactory.Create("save-" + seed, T0, Rng.Create(seed));

        private static int _seq;
        public static string Rid(string p = "r") => p + "-" + (++_seq);

        public static GameState Advanced(GameState s, long ms, string requestId = null)
        {
            var clock = new FixedClock(s.Clock.LastProcessedAtMs + ms);
            DispatchResult r = CommandDispatcher.Dispatch(s, new AdvanceTimeCommand(requestId ?? Rid("a"), AdvanceMode.Online), clock);
            if (!r.Ok) throw new InvalidOperationException(r.Error.ToString());
            return r.State;
        }

        public static string Canonical(GameState s) => SaveSchema.CanonicalJson(GameStateJson.ToJson(s));

        /// <summary>
        /// State v4 của TS → schema hiện tại của Unity bằng đúng các bước migration 4 → 5 → 6 của C# (thêm breedingNest = null, codex).
        /// Golden TS dừng ở v4, nên mọi phép so parity của U2.2 chạy trên ảnh này: phần chung phải trùng từng byte.
        /// </summary>
        public static JsonObject AsUnity(JsonObject tsV4State)
        {
            MigrateResult m = Migrations.Migrate(4, tsV4State);
            if (!m.Ok) throw new InvalidOperationException(string.Join("; ", m.Details));
            return m.State;
        }

        /// <summary>
        /// Chiếu ngược state Unity về v4 của TS (chỉ khi tổ trống): bỏ breedingNest và codex (Bách khoa là dữ liệu dẫn xuất thêm, TS chưa có),
        /// schemaVersion = 4. Trang trí đã mua vẫn nằm trong zones.decorationIds — TS chấp nhận chuỗi bất kỳ ở đó.
        /// </summary>
        public static JsonObject AsV4(JsonObject unityState)
        {
            if (!(unityState.Get("breedingNest") is JsonNull)) throw new InvalidOperationException("breedingNest not empty: no v4 projection");
            var o = new JsonObject();
            foreach (var kv in unityState) if (kv.Key != "breedingNest" && kv.Key != "codex") o.Set(kv.Key, kv.Key == "schemaVersion" ? JsonValue.From(4) : kv.Value);
            return o;
        }

        /// <summary>
        /// Xuất một save C# cho bộ đọc TS (CI: Tools~/verify-csharp-saves.test.ts). name.unity.json là file schema Unity thật (v6) — TS phải từ chối
        /// "newer-version" (chỉ đọc, không ghi đè). Khi tổ trống còn có name.save.json = ảnh v4 + name.canonical.txt mà TS phải đọc trùng state.
        /// </summary>
        public static void Export(string dir, string name, string v5Text)
        {
            if (dir == null) return;
            var utf8 = new System.Text.UTF8Encoding(false);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, name + ".unity.json"), v5Text, utf8);
            var state = (JsonObject)((JsonObject)JsonParser.Parse(v5Text)).Get("state");
            if (!(state.Get("breedingNest") is JsonNull)) return;
            File.WriteAllText(Path.Combine(dir, name + ".save.json"), AsV4Text(v5Text), utf8);
            File.WriteAllText(Path.Combine(dir, name + ".canonical.txt"), SaveSchema.CanonicalJson(AsV4(state)), utf8);
        }

        /// <summary>Envelope v4 của TS dựng lại từ save C# v5 (tổ trống): so được từng byte với serializeSave của TS.</summary>
        public static string AsV4Text(string v5Text)
        {
            var env = (JsonObject)JsonParser.Parse(v5Text);
            JsonObject state = AsV4((JsonObject)env.Get("state"));
            var o = new JsonObject();
            foreach (var kv in env)
            {
                if (kv.Key == "schemaVersion") o.Set(kv.Key, JsonValue.From(4));
                else if (kv.Key == "state") o.Set(kv.Key, state);
                else if (kv.Key == "checksum") o.Set(kv.Key, JsonValue.From(SaveSchema.ChecksumOf(state)));
                else o.Set(kv.Key, kv.Value);
            }
            return JsonWriter.Stringify(o);
        }

        /// <summary>Envelope ở schemaVersion bất kỳ với checksum hợp lệ (như envelopeAt của persistence.test.ts).</summary>
        public static string EnvelopeAt(int version, GameState state = null)
        {
            JsonObject env = SaveSchema.CreateEnvelope(state ?? Fresh(), T0);
            JsonObject s = ((JsonObject)env.Get("state")).ShallowCopy();
            if (version < 6) s.Remove("codex");
            if (version < 5) s.Remove("breedingNest");
            if (version < 4) foreach (string k in new[] { "tutorial", "pendingPack", "unlockedAchievements", "claimedAchievements" }) s.Remove(k);
            if (version == 1 || version == 3) s.Remove("discoveries");
            s.Set("schemaVersion", JsonValue.From(version));
            return JsonWriter.Stringify(env.ShallowCopy().Set("schemaVersion", JsonValue.From(version)).Set("state", s).Set("checksum", JsonValue.From(SaveSchema.ChecksumOf(s))));
        }

        public static (SaveManager mgr, MemorySaveStore store) Manager(MemorySaveStore store = null)
        {
            store = store ?? new MemorySaveStore();
            return (new SaveManager(store, () => T0), store);
        }
    }

    /// <summary>Đồng hồ tường giả có thể nhảy tiến/lùi.</summary>
    public sealed class ManualClock : IClock
    {
        public long Now;
        public ManualClock(long now) { Now = now; }
        public long NowMs() => Now;
    }

    /// <summary>Môi trường phiên giả: đồng hồ tường + thời gian đơn điệu riêng, scheduler bơm bằng tay.</summary>
    public sealed class SessionRig
    {
        public readonly ManualClock Clock;
        public readonly PumpedScheduler Scheduler = new PumpedScheduler();
        public long Monotonic;
        public ISaveStore Store;
        public IWriterLock Lock;
        private int _ids;

        public SessionRig(ISaveStore store, long wallNow, IWriterLock writerLock = null)
        {
            Store = store; Clock = new ManualClock(wallNow); Lock = writerLock;
        }

        public GameSession NewSession(bool visible = true)
        {
            var s = new GameSession(new SessionEnvironment
            {
                Clock = Clock, Store = Store, WriterLock = Lock, Scheduler = Scheduler, StartVisible = visible,
                NewId = () => "id-" + (++_ids).ToString("D6", System.Globalization.CultureInfo.InvariantCulture),
            });
            Scheduler.Pump(Monotonic);
            s.Start();
            return s;
        }

        /// <summary>Thời gian thật trôi: đồng hồ tường và đơn điệu cùng tiến, bơm scheduler từng bước 1 s.</summary>
        public void Run(long ms, long stepMs = 1000)
        {
            long left = ms;
            while (left > 0)
            {
                long d = Math.Min(stepMs, left);
                Clock.Now += d; Monotonic += d; left -= d;
                Scheduler.Pump(Monotonic);
            }
        }

        /// <summary>Tiến trình bị đóng băng/không chạy (chạy nền, ngủ máy): thời gian trôi nhưng không có frame.</summary>
        public void Frozen(long ms) { Clock.Now += ms; Monotonic += ms; }
    }

    public static class Helpers
    {
        public static List<string> Names(IEnumerable<SlotFailure> fs) { var l = new List<string>(); foreach (var f in fs) l.Add(f.ToString()); return l; }
    }
}
