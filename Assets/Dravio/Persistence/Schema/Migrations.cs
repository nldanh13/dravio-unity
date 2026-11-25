using System;
using System.Collections.Generic;
using Dravio.Domain.Catalog;
using Dravio.Persistence.Json;

namespace Dravio.Persistence.Schema
{
    /// <summary>Một bước migration thuần: state schema From → schema To. Ném lỗi nếu dữ liệu không chuyển được.</summary>
    public sealed class MigrationStep
    {
        public MigrationStep(int from, int to, Func<JsonObject, JsonObject> run) { From = from; To = to; Run = run; }
        public int From { get; }
        public int To { get; }
        public Func<JsonObject, JsonObject> Run { get; }
    }

    public enum MigrateErrorCode { NewerVersion, UnsupportedVersion, MigrationFailed }

    public sealed class MigrateResult
    {
        internal MigrateResult(JsonObject state, int fromVersion, bool preRelease) { Ok = true; State = state; FromVersion = fromVersion; PreRelease = preRelease; Details = Array.Empty<string>(); }
        internal MigrateResult(MigrateErrorCode code, string detail) { Ok = false; Code = code; Details = new[] { detail }; }
        public bool Ok { get; }
        public JsonObject State { get; }
        public int FromVersion { get; }
        public bool PreRelease { get; }
        public MigrateErrorCode Code { get; }
        public IReadOnlyList<string> Details { get; }
    }

    /// <summary>
    /// Migration theo schemaVersion (src/persistence/migrations.ts; ADR 0006, 0007, 0009, 0013).
    /// v1 → v2 thêm discoveries (ghi bù khám phá cho sinh cảnh đã mở, ADR 0010); v2 → v4 thêm tutorial,
    /// pendingPack, unlockedAchievements, claimedAchievements; số 3 là bí danh tiền phát hành của v1 và được dành riêng.
    /// v4 → v5 (U2.5, implementation mới chỉ ở bản Unity) thêm breedingNest = null.
    /// </summary>
    public static class Migrations
    {
        public static readonly IReadOnlyDictionary<int, int> PreReleaseSchemaAliases = new Dictionary<int, int> { [3] = 1 };

        public static readonly IReadOnlyList<MigrationStep> Steps = Array.AsReadOnly(new[]
        {
            new MigrationStep(1, 2, state =>
            {
                if (state.ContainsKey("discoveries")) throw new InvalidOperationException("v1 state already has discoveries");
                var opened = new HashSet<string>(StringComparer.Ordinal);
                if (state.Get("zones") is JsonArray zones)
                    foreach (JsonValue z in zones)
                        if (z is JsonObject zo && zo.Get("unlockedHabitatIds") is JsonArray ids)
                            foreach (JsonValue h in ids) if (h is JsonString hs) opened.Add(hs.Value);
                var discoveries = new JsonArray();
                foreach (string id in ProgressCatalog.DiscoveryIds)
                {
                    bool any = false;
                    foreach (HabitatUnlockRule r in ProgressCatalog.HabitatUnlockRules) if (r.Requires == id && opened.Contains(r.HabitatId)) any = true;
                    if (any) discoveries.Add(JsonValue.From(id));
                }
                return state.ShallowCopy().Set("discoveries", discoveries);
            }),
            new MigrationStep(2, 4, state =>
            {
                foreach (string k in new[] { "tutorial", "pendingPack", "unlockedAchievements", "claimedAchievements" })
                    if (state.ContainsKey(k)) throw new InvalidOperationException("v2 state already has " + k);
                JsonValue startedAt = state.Get("clock") is JsonObject c && c.Get("lastProcessedAtMs") is JsonNumber n ? (JsonValue)n : JsonValue.From(0L);
                var tutorial = new JsonObject().Set("status", JsonValue.From("active")).Set("completed", new JsonArray()).Set("startedAtMs", startedAt)
                    .Set("profilesViewed", new JsonArray()).Set("starterPackUsed", JsonValue.False);
                return state.ShallowCopy().Set("tutorial", tutorial).Set("pendingPack", JsonValue.Null)
                    .Set("unlockedAchievements", new JsonArray()).Set("claimedAchievements", new JsonArray());
            }),
            // U2.5 (chỉ bản Unity): thêm tổ ấp rỗng. TS v4 chưa có lệnh lai nên rồng mang activity = "breeding" không có trứng nào —
            // trả về "idle" để không bị khóa vĩnh viễn. Không đổi vàng, rồng, gene, phả hệ hay thành tựu.
            new MigrationStep(4, 5, state =>
            {
                if (state.ContainsKey("breedingNest")) throw new InvalidOperationException("v4 state already has breedingNest");
                JsonObject next = state.ShallowCopy().Set("breedingNest", JsonValue.Null);
                if (state.Get("dragons") is JsonArray dragons)
                {
                    var fixedDragons = new JsonArray();
                    foreach (JsonValue d in dragons)
                        fixedDragons.Add(d is JsonObject dob && dob.Get("activity") is JsonString a && a.Value == "breeding"
                            ? dob.ShallowCopy().Set("activity", JsonValue.From("idle")) : d);
                    next.Set("dragons", fixedDragons);
                }
                return next;
            }),
        });

        public static MigrateResult Migrate(int fromVersion, JsonObject input) => Migrate(fromVersion, input, Steps, SaveSchema.CurrentVersion);

        public static MigrateResult Migrate(int fromVersion, JsonObject input, IReadOnlyList<MigrationStep> steps, int target)
        {
            bool preRelease = PreReleaseSchemaAliases.TryGetValue(fromVersion, out int alias);
            int version = preRelease ? alias : fromVersion;
            JsonObject state = preRelease ? input.ShallowCopy().Set("schemaVersion", JsonValue.From(version)) : input;
            if (version < 1) return new MigrateResult(MigrateErrorCode.UnsupportedVersion, "schemaVersion " + fromVersion);
            if (version > target) return new MigrateResult(MigrateErrorCode.NewerVersion, "schemaVersion " + fromVersion);
            while (version < target)
            {
                MigrationStep step = null;
                foreach (MigrationStep s in steps) if (s.From == version) { step = s; break; }
                if (step == null || step.To <= version || step.To > target)
                    return new MigrateResult(MigrateErrorCode.UnsupportedVersion, "no migration from schemaVersion " + version);
                try
                {
                    state = step.Run(state).ShallowCopy().Set("schemaVersion", JsonValue.From(step.To));
                }
                catch (Exception e)
                {
                    return new MigrateResult(MigrateErrorCode.MigrationFailed, version + " → " + step.To + ": " + e.Message);
                }
                version = step.To;
            }
            return new MigrateResult(state, fromVersion, preRelease);
        }
    }
}
