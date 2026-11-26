using System;
using System.Collections.Generic;
using System.Text;
using Dravio.Domain;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Persistence.Json;
using Dravio.Persistence.Schema;

namespace Dravio.Persistence
{
    /// <summary>Mã lỗi đọc save (saveFile.ts SaveErrorCode), dùng làm hậu tố khóa i18n saveError.*.</summary>
    public enum SaveErrorCode
    {
        [WireName("too-large")] TooLarge,
        [WireName("corrupt-json")] CorruptJson,
        [WireName("bad-format")] BadFormat,
        [WireName("bad-checksum")] BadChecksum,
        [WireName("newer-version")] NewerVersion,
        [WireName("unsupported-version")] UnsupportedVersion,
        [WireName("migration-failed")] MigrationFailed,
        [WireName("invalid-state")] InvalidState,
    }

    public sealed class ParsedSave
    {
        public ParsedSave(GameState state, long savedAtMs, int? migratedFrom, IReadOnlyList<string> warnings, JsonObject rawState)
        {
            State = state; SavedAtMs = savedAtMs; MigratedFrom = migratedFrom; Warnings = warnings; RawState = rawState;
        }
        public GameState State { get; }
        public long SavedAtMs { get; }
        /// <summary>schemaVersion gốc nếu save đã được migrate lên schema hiện tại (ví dụ 3 = bản phát triển trước v1).</summary>
        public int? MigratedFrom { get; }
        public IReadOnlyList<string> Warnings { get; }
        /// <summary>State JSON sau migration (trước khi đổi sang kiểu C#), để báo khóa lạ khi import.</summary>
        public JsonObject RawState { get; }
    }

    public sealed class ParseResult
    {
        private ParseResult(ParsedSave save, SaveErrorCode code, IReadOnlyList<string> details) { Save = save; Code = code; Details = details; }
        public bool Ok => Save != null;
        public ParsedSave Save { get; }
        public SaveErrorCode Code { get; }
        public IReadOnlyList<string> Details { get; }
        internal static ParseResult Success(ParsedSave s) => new ParseResult(s, default, Array.Empty<string>());
        internal static ParseResult Fail(SaveErrorCode c, params string[] details) => new ParseResult(null, c, details);
        internal static ParseResult Fail(SaveErrorCode c, IReadOnlyList<string> details) => new ParseResult(null, c, details);
        public override string ToString() => Ok ? "ok" : Code.ToWire() + (Details.Count > 0 ? ": " + string.Join("; ", Details) : "");
    }

    /// <summary>
    /// Đọc một chuỗi save (từ kho hoặc file import) thành GameState đã kiểm (src/persistence/saveFile.ts).
    /// Thứ tự như bản TS: kích thước → JSON → envelope → checksum trên state đúng như đã ghi → migration →
    /// validate. Không bao giờ ném lỗi với dữ liệu xấu: trả mã lỗi có kiểu.
    /// </summary>
    public static class SaveFile
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, false);

        public static int Utf8ByteCount(string text) => Utf8.GetByteCount(text);

        public static ParseResult Parse(string text)
        {
            if (text == null) return ParseResult.Fail(SaveErrorCode.CorruptJson);
            if (Utf8ByteCount(text) > SaveSchema.MaxSaveBytes) return ParseResult.Fail(SaveErrorCode.TooLarge);
            if (!JsonParser.TryParse(text, out JsonValue raw)) return ParseResult.Fail(SaveErrorCode.CorruptJson);
            if (!(raw is JsonObject env)) return ParseResult.Fail(SaveErrorCode.BadFormat);
            if (!(env.Get("format") is JsonString f) || f.Value != SaveSchema.Format) return ParseResult.Fail(SaveErrorCode.BadFormat);
            if (!(env.Get("schemaVersion") is JsonNumber vn) || double.IsInfinity(vn.Value) || Math.Floor(vn.Value) != vn.Value)
                return ParseResult.Fail(SaveErrorCode.BadFormat, "schemaVersion");
            if (!(env.Get("checksum") is JsonString checksum) || !(env.Get("state") is JsonObject state))
                return ParseResult.Fail(SaveErrorCode.BadFormat, "checksum/state");
            if (!(state.Get("schemaVersion") is JsonNumber sv) || sv.Value != vn.Value)
                return ParseResult.Fail(SaveErrorCode.BadFormat, "state.schemaVersion");
            // Checksum kiểm trên state đúng như đã ghi, trước mọi migration.
            if (SaveSchema.ChecksumOf(state) != checksum.Value) return ParseResult.Fail(SaveErrorCode.BadChecksum);
            int version = vn.Value > int.MaxValue ? int.MaxValue : vn.Value < int.MinValue ? int.MinValue : (int)vn.Value;
            MigrateResult m = Migrations.Migrate(version, state);
            if (!m.Ok)
            {
                SaveErrorCode code = m.Code == MigrateErrorCode.NewerVersion ? SaveErrorCode.NewerVersion
                    : m.Code == MigrateErrorCode.UnsupportedVersion ? SaveErrorCode.UnsupportedVersion : SaveErrorCode.MigrationFailed;
                return ParseResult.Fail(code, m.Details);
            }
            var errors = new List<string>();
            GameState gs = GameStateJson.FromJson(m.State, errors);
            foreach (string e in GameStateValidator.Validate(gs).Errors) if (!errors.Contains(e)) errors.Add(e);
            bool current = version == SaveSchema.CurrentVersion;
            // Bước 4 → 5 (U2.5) chỉ thêm tổ rỗng, không biến đổi dữ liệu: save v4 hỏng nội dung vẫn báo invalid-state như TS,
            // để thông điệp cho người chơi không đổi. migration-failed dành cho save v1/v2 hỏng sau migration thật.
            bool contentCurrent = version >= Dravio.Domain.Catalog.ContentInfo.TsSchemaVersion;
            if (errors.Count > 0) return ParseResult.Fail(contentCurrent ? SaveErrorCode.InvalidState : SaveErrorCode.MigrationFailed, errors);
            long savedAtMs = GameStateJson.Reader.AsSafeLong(env.Get("savedAtMs"), out long sa) ? sa : gs.Clock.LastProcessedAtMs;
            return ParseResult.Success(new ParsedSave(gs, savedAtMs, current ? (int?)null : version, Array.Empty<string>(), m.State));
        }

        // ---------------- Import ----------------

        public sealed class ImportSanitizeReport
        {
            public ImportSanitizeReport(IReadOnlyList<string> droppedStateKeys, IReadOnlyList<string> droppedDragonKeys) { DroppedStateKeys = droppedStateKeys; DroppedDragonKeys = droppedDragonKeys; }
            public IReadOnlyList<string> DroppedStateKeys { get; }
            public IReadOnlyList<string> DroppedDragonKeys { get; }
        }

        /// <summary>
        /// sanitizeImportedState: import do người chơi kiểm soát (checksum không phải chữ ký). Chỉ giữ khóa GameState
        /// đã biết; rồng mất registryRef (Verified) và khóa lạ. Không bao giờ nhập entitlement/quyền mua.
        /// </summary>
        public static GameState SanitizeImported(ParsedSave save, out ImportSanitizeReport report)
        {
            var droppedState = new List<string>();
            foreach (string k in save.RawState.Keys) if (!Contains(GameStateJson.StateKeys, k)) droppedState.Add(k);
            var droppedDragon = new SortedSet<string>(StringComparer.Ordinal);
            if (save.RawState.Get("dragons") is JsonArray dragons)
                foreach (JsonValue d in dragons)
                    if (d is JsonObject o)
                        foreach (string k in o.Keys) if (!Contains(GameStateJson.DragonKeys, k)) droppedDragon.Add(k);
            report = new ImportSanitizeReport(droppedState, new List<string>(droppedDragon));
            GameState s = save.State.DeepClone();
            foreach (DragonState d in s.Dragons) d.RegistryRef = null;
            return s;
        }

        private static bool Contains(IReadOnlyList<string> list, string k)
        {
            foreach (string x in list) if (x == k) return true;
            return false;
        }

        public sealed class ImportPreview
        {
            public string SaveId { get; internal set; }
            public long SavedAtMs { get; internal set; }
            public long GoldMicro { get; internal set; }
            public int DragonCount { get; internal set; }
            public int SpeciesCount { get; internal set; }
            public int? MigratedFrom { get; internal set; }
            public IReadOnlyList<string> Warnings { get; internal set; }
        }

        /// <summary>previewOf: tóm tắt cho màn hình xác nhận import (không đổi state).</summary>
        public static ImportPreview PreviewOf(GameState state, long savedAtMs, int? migratedFrom, IReadOnlyList<string> warnings)
        {
            var species = new HashSet<string>(StringComparer.Ordinal);
            int count = 0;
            foreach (DragonState d in state.Dragons)
            {
                if (d.Location.Kind == LocationKind.Released) continue;
                count++;
                if (SpeciesCatalog.TryGet(d.SpeciesId, out _)) species.Add(d.SpeciesId);
            }
            return new ImportPreview
            {
                SaveId = state.SaveId, SavedAtMs = savedAtMs, GoldMicro = state.Player.Gold.Micro, DragonCount = count,
                SpeciesCount = species.Count, MigratedFrom = migratedFrom, Warnings = warnings,
            };
        }
    }
}
