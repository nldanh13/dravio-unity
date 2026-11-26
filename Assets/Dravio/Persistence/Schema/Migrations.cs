using System;
using System.Collections.Generic;
using Dravio.Domain;
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
    /// v4 → v5 (U2.5, implementation mới chỉ ở bản Unity) thêm breedingNest = null; v5 → v6 (U2.7) thêm codex; v6 → v7 (U2.8) thêm expedition, completedQuests, grantedRewards, rewardQueue (rỗng); v7 → v8 (U2.6) thêm vitals cho mọi rồng, healthCases, disasters, hazard; v8 → v9 (U2.10) packDaily, sanctuaryProjectsBuilt, research; v9 → v10 (U2.11) completedSideQuests, expeditionsClaimed, relations.
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
            // U2.7 (chỉ bản Unity): thêm Bách khoa, ghi bù từ rồng đang có (kể cả đã thả — người chơi từng sở hữu). Ô = loài × biến thể,
            // stage quan sát = mọi stage ≤ stage hiện tại. totalHatched theo loài không có dữ liệu ở v5 nên để 0 (không suy diễn).
            // Trang trí giữ nguyên, kể cả ID ngoài catalog (TS chấp nhận chuỗi bất kỳ): bảo toàn dữ liệu bản trước.
            new MigrationStep(5, 6, state =>
            {
                if (state.ContainsKey("codex")) throw new InvalidOperationException("v5 state already has codex");
                var muts = new Dictionary<string, SortedSet<int>>();
                var stages = new Dictionary<string, int>();
                if (state.Get("dragons") is JsonArray dragons)
                    foreach (JsonValue dv in dragons)
                    {
                        if (!(dv is JsonObject d) || !(d.Get("speciesId") is JsonString sid) || !SpeciesCatalog.TryGet(sid.Value, out DragonSpecies sp)) continue;
                        if (!(d.Get("mutation") is JsonString mv) || !Dravio.Domain.WireNames.TryParse(mv.Value, out MutationType m)) continue;
                        double xp = d.Get("xpSec") is JsonNumber xn ? xn.Value : 0;
                        int stage = (int)Dravio.Domain.Economy.EconomyRates.GrowthStageOf(xp, sp);
                        if (!muts.TryGetValue(sp.Id, out SortedSet<int> set)) muts[sp.Id] = set = new SortedSet<int>();
                        set.Add((int)m);
                        stages[sp.Id] = Math.Max(stages.TryGetValue(sp.Id, out int cur) ? cur : 0, stage);
                    }
                var codex = new JsonObject();
                foreach (DragonSpecies sp in SpeciesCatalog.All)
                {
                    if (!muts.TryGetValue(sp.Id, out SortedSet<int> set)) continue;
                    var ma = new JsonArray(); foreach (int m in set) ma.Add(JsonValue.From(Dravio.Domain.WireNames.ToWire((MutationType)m)));
                    var sa = new JsonArray(); for (int st = 0; st <= stages[sp.Id]; st++) sa.Add(JsonValue.From(Dravio.Domain.WireNames.ToWire((GrowthStage)st)));
                    codex.Set(sp.Id, new JsonObject().Set("discoveredMutations", ma).Set("totalHatched", JsonValue.From(0L)).Set("observedStages", sa));
                }
                return state.ShallowCopy().Set("codex", codex);
            }),
            // U2.8 (chỉ bản Unity): thêm slot thám hiểm trống và trạng thái mở thế giới rỗng (ADR 0201: chưa nhiệm vụ nào hoàn thành, chưa trao trứng). Bản trước chưa có lệnh thám hiểm nên rồng mang activity = "expedition" không có
            // chuyến nào — trả về "idle" để không bị khóa vĩnh viễn. Không đổi trường nào khác (vật liệu trong inventory giữ nguyên).
            new MigrationStep(6, 7, state =>
            {
                if (state.ContainsKey("expedition")) throw new InvalidOperationException("v6 state already has expedition");
                JsonObject next = state.ShallowCopy().Set("expedition", JsonValue.Null)
                    .Set("completedQuests", new JsonArray()).Set("grantedRewards", new JsonArray()).Set("rewardQueue", new JsonArray());
                if (state.Get("dragons") is JsonArray dragons)
                {
                    var fixedDragons = new JsonArray();
                    foreach (JsonValue d in dragons)
                        fixedDragons.Add(d is JsonObject dob && dob.Get("activity") is JsonString a && a.Value == "expedition"
                            ? dob.ShallowCopy().Set("activity", JsonValue.From("idle")) : d);
                    next.Set("dragons", fixedDragons);
                }
                return next;
            }),
            // U2.6 (chỉ bản Unity): thêm chỉ số sức khỏe và trạng thái thiên tai / chăm sóc rỗng. Mọi rồng (kể cả đã thả) nhận vitals nền
            // (ADR 0202 D1 — giá trị ghi rõ, không suy diễn "khỏe" từ trường thiếu). Vị trí care / shelter và activity = "care" của save cũ
            // (TS cho phép, golden TS có) được GIỮ NGUYÊN, không bịa ca chăm sóc cho chúng (ADR 0202 D8). Không đổi vàng, gene, phả hệ, vật liệu,
            // thành tựu hay ngân sách sản xuất.
            new MigrationStep(7, 8, state =>
            {
                foreach (string k in new[] { "healthCases", "disasters", "hazard" })
                    if (state.ContainsKey(k)) throw new InvalidOperationException("v7 state already has " + k);
                JsonObject next = state.ShallowCopy().Set("healthCases", new JsonArray()).Set("disasters", new JsonArray())
                    .Set("hazard", new JsonObject().Set("foregroundMs", JsonValue.From(0L)).Set("checksDone", JsonValue.From(0L)).Set("cooldownUntilMs", new JsonObject()));
                if (state.Get("dragons") is JsonArray dragons)
                {
                    var fixedDragons = new JsonArray();
                    foreach (JsonValue d in dragons)
                    {
                        if (!(d is JsonObject dob)) { fixedDragons.Add(d); continue; }
                        if (dob.ContainsKey("vitals")) throw new InvalidOperationException("v7 dragon already has vitals");
                        int b = HealthCatalog.VitalBaseline;
                        JsonObject nd = dob.ShallowCopy().Set("vitals", new JsonObject().Set("health", JsonValue.From(b)).Set("energy", JsonValue.From(b))
                            .Set("comfort", JsonValue.From(b)).Set("mobility", JsonValue.From(b)).Set("hydration", JsonValue.From(b)));
                        fixedDragons.Add(nd);
                    }
                    next.Set("dragons", fixedDragons);
                }
                return next;
            }),
            // U2.10 (chỉ bản Unity): giới hạn gói Starter theo ngày (chưa mua hôm nay), 0 dự án Sanctuary và sổ nghiên cứu rỗng.
            // KHÔNG suy diễn quan sát từ Codex (người chơi chưa quan sát gì), không đổi vàng, rồng, vật liệu, Codex hay nhiệm vụ.
            new MigrationStep(8, 9, state =>
            {
                foreach (string k in new[] { "packDaily", "sanctuaryProjectsBuilt", "research" })
                    if (state.ContainsKey(k)) throw new InvalidOperationException("v8 state already has " + k);
                return state.ShallowCopy()
                    .Set("packDaily", new JsonObject().Set("day", JsonValue.Null).Set("starterBought", JsonValue.From(0L)))
                    .Set("sanctuaryProjectsBuilt", JsonValue.From(0L))
                    .Set("research", new JsonObject().Set("species", new JsonArray()).Set("individuals", new JsonArray()));
            }),
            // U2.11 (chỉ bản Unity): chưa nhiệm vụ phụ nào, chưa đếm chuyến thám hiểm, chưa có thời gian sống chung. KHÔNG suy diễn từ dữ liệu cũ
            // (chuyến đã nhận trước v10 không được thưởng mốc bù), không đổi vàng, rồng, vật liệu, Codex, nhiệm vụ cổng hay sổ nghiên cứu.
            new MigrationStep(9, 10, state =>
            {
                foreach (string k in new[] { "completedSideQuests", "expeditionsClaimed", "relations" })
                    if (state.ContainsKey(k)) throw new InvalidOperationException("v9 state already has " + k);
                return state.ShallowCopy()
                    .Set("completedSideQuests", new JsonArray())
                    .Set("expeditionsClaimed", new JsonObject())
                    .Set("relations", new JsonObject().Set("pairs", new JsonArray()));
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
