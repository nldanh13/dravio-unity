using System.Security.Cryptography;
using System.Text;
using Dravio.Domain;
using Dravio.Persistence.Json;

namespace Dravio.Persistence.Schema
{
    /// <summary>
    /// Dravio Save Schema (GDD 3.1 trang 18; ADR 0006, 0007, 0009, 0013; src/persistence/schema.ts).
    /// Envelope = { format, schemaVersion, contentVersion, savedAtMs, revision, checksum, state }.
    /// checksum = SHA-256 (hex thường) của canonicalJson(state) — phát hiện file hỏng/sửa tay, KHÔNG phải chữ ký.
    /// </summary>
    public static class SaveSchema
    {
        public const string Format = "dravio-save";

        /// <summary>Schema hiện tại = Dravio.Domain.Catalog.ContentInfo.SchemaVersion (4).</summary>
        public static int CurrentVersion => Dravio.Domain.Catalog.ContentInfo.SchemaVersion;

        /// <summary>Giới hạn kích thước save (5 MB UTF-8). Save P0 thực tế &lt; 200 KB.</summary>
        public const int MaxSaveBytes = 5 * 1024 * 1024;

        public static string CanonicalJson(JsonValue state) => JsonWriter.Canonical(state);

        public static string Sha256Hex(string text)
        {
            byte[] bytes = new UTF8Encoding(false, false).GetBytes(text);
            byte[] digest;
            using (SHA256 sha = SHA256.Create()) digest = sha.ComputeHash(bytes);
            var sb = new StringBuilder(64);
            foreach (byte b in digest) sb.Append(b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        public static string ChecksumOf(JsonValue state) => Sha256Hex(CanonicalJson(state));

        /// <summary>
        /// toPersistedState: bỏ dữ liệu tạm không thuộc save — viên thức ăn đang rơi (ADR 0004 #10).
        /// Không đổi state đầu vào.
        /// </summary>
        public static GameState ToPersistedState(GameState state)
        {
            GameState c = state.DeepClone();
            c.Food = new FoodState { LastSpawnAtMs = null };
            return c;
        }

        /// <summary>createEnvelope: luôn ghi ở schema hiện tại; state đã bỏ dữ liệu tạm.</summary>
        public static JsonObject CreateEnvelope(GameState state, long savedAtMs)
        {
            GameState persisted = ToPersistedState(state);
            JsonObject json = GameStateJson.ToJson(persisted);
            return new JsonObject()
                .Set("format", JsonValue.From(Format))
                .Set("schemaVersion", JsonValue.From(CurrentVersion))
                .Set("contentVersion", JsonValue.From(persisted.ContentVersion))
                .Set("savedAtMs", JsonValue.From(savedAtMs))
                .Set("revision", JsonValue.From(persisted.Revision))
                .Set("checksum", JsonValue.From(ChecksumOf(json)))
                .Set("state", json);
        }

        /// <summary>serializeSave: chuỗi JSON (UTF-8 khi ghi file) của envelope.</summary>
        public static string Serialize(GameState state, long savedAtMs) => JsonWriter.Stringify(CreateEnvelope(state, savedAtMs));
    }
}
