using System;
using System.Collections.Generic;
using System.Globalization;
using Dravio.Domain;
using Dravio.Domain.Core;
using Dravio.Persistence.Json;

namespace Dravio.Persistence.Schema
{
    /// <summary>
    /// Ánh xạ GameState (C#) ↔ state JSON của Dravio Save Schema v9 (= v4 của TS + breedingNest (U2.5) + codex (U2.7) + expedition (U2.8) + vitals / healthCases / disasters / hazard (U2.6) + packDaily / sanctuaryProjectsBuilt / research (U2.10)), trường phẳng như src/types/state.ts.
    /// PlayerState của C# được trải phẳng thành goldMicro, goldRemainder, totalGoldEarnedMicro, inventory,
    /// upgrades, pityCount, stats, settings. Trường optional (undefined trong TS) bị bỏ khỏi JSON khi null.
    /// Thứ tự khóa theo createInitialState của TS (checksum không phụ thuộc thứ tự vì canonicalJson sắp xếp khóa).
    /// </summary>
    public static class GameStateJson
    {
        /// <summary>Khóa GameState: STATE_KEYS của saveFile.ts (schema v4) + khóa chỉ có ở bản Unity (v5–v8).</summary>
        public static readonly IReadOnlyList<string> StateKeys = Array.AsReadOnly(new[]
        {
            "schemaVersion", "contentVersion", "saveId", "revision", "goldMicro", "goldRemainder", "totalGoldEarnedMicro",
            "dragons", "zones", "productionDragonIds", "inventory", "upgrades", "food", "processedRequestIds", "pityCount",
            "stats", "settings", "clock", "offlineReceipt", "discoveries", "tutorial", "pendingPack", "unlockedAchievements", "claimedAchievements",
            // v5 (U2.5), v6 (U2.7), v7 (U2.8), chỉ có ở bản Unity
            "breedingNest", "codex", "expedition", "completedQuests", "grantedRewards", "rewardQueue",
            // v8 (U2.6), chỉ có ở bản Unity
            "healthCases", "disasters", "hazard",
            // v9 (U2.10), chỉ có ở bản Unity
            "packDaily", "sanctuaryProjectsBuilt", "research",
        });

        /// <summary>Khóa rồng được phép đi qua import (DRAGON_KEYS). registryRef (Verified) không thuộc danh sách.</summary>
        public static readonly IReadOnlyList<string> DragonKeys = Array.AsReadOnly(new[]
        {
            "uid", "speciesId", "nickname", "genotype", "mutation", "xpSec", "locked", "location", "hatchedAtMs", "personality",
            "bond", "bondDay", "bondDaily", "lastFeedXpAtMs", "lastPetAtMs", "parentIds", "grandparentIds", "generation",
            "ancestry", "origin", "activity", "breedCooldownUntilMs", "vitals",
        });

        // ---------------- GameState → JSON ----------------

        private static JsonValue N(long v) => JsonValue.From(v);
        private static JsonValue S(string v) => JsonValue.From(v);
        private static JsonArray Arr(IEnumerable<string> xs) { var a = new JsonArray(); if (xs != null) foreach (var x in xs) a.Add(S(x)); return a; }

        public static JsonObject ToJson(GameState s)
        {
            PlayerState p = s.Player;
            var o = new JsonObject()
                .Set("schemaVersion", N(s.SchemaVersion))
                .Set("contentVersion", S(s.ContentVersion))
                .Set("saveId", S(s.SaveId))
                .Set("revision", N(s.Revision))
                .Set("goldMicro", N(p.Gold.Micro))
                .Set("goldRemainder", N(p.GoldRemainder))
                .Set("totalGoldEarnedMicro", N(p.TotalGoldEarned.Micro));
            var dragons = new JsonArray();
            foreach (DragonState d in s.Dragons) dragons.Add(DragonToJson(d));
            o.Set("dragons", dragons);
            var zones = new JsonArray();
            foreach (ZoneState z in s.Zones)
                zones.Add(new JsonObject()
                    .Set("id", S(z.Id)).Set("realm", S(z.Realm.ToWire())).Set("unlocked", JsonValue.From(z.Unlocked)).Set("capacity", N(z.Capacity))
                    .Set("unlockedHabitatIds", Arr(z.UnlockedHabitatIds)).Set("decorationIds", Arr(z.DecorationIds))
                    .Set("activeDecorationIds", Arr(z.ActiveDecorationIds)).Set("recovery", N(z.Recovery)));
            o.Set("zones", zones);
            o.Set("productionDragonIds", Arr(s.ProductionDragonIds));
            var inv = new JsonObject();
            foreach (var kv in p.Inventory) inv.Set(kv.Key, N(kv.Value));
            o.Set("inventory", inv);
            UpgradesState u = p.Upgrades;
            o.Set("upgrades", new JsonObject()
                .Set("islandCapacityLevel", N(u.IslandCapacityLevel)).Set("foodQualityLevel", N(u.FoodQualityLevel))
                .Set("foodCooldownLevel", N(u.FoodCooldownLevel)).Set("mutationLuckLevel", N(u.MutationLuckLevel)).Set("hatchSpeedLevel", N(u.HatchSpeedLevel)));
            var drops = new JsonArray();
            foreach (FoodDrop f in s.Food.Drops)
                drops.Add(new JsonObject().Set("id", S(f.Id)).Set("dietId", S(f.DietId.ToWire())).Set("zoneId", S(f.ZoneId)).Set("habitatId", S(f.HabitatId))
                    .Set("spawnedAtMs", N(f.SpawnedAtMs)).Set("expiresAtMs", N(f.ExpiresAtMs)));
            o.Set("food", new JsonObject().Set("lastSpawnAtMs", s.Food.LastSpawnAtMs.HasValue ? N(s.Food.LastSpawnAtMs.Value) : JsonValue.Null).Set("drops", drops));
            o.Set("discoveries", Arr(s.Discoveries));
            TutorialState t = s.Tutorial;
            o.Set("tutorial", new JsonObject().Set("status", S(t.Status.ToWire())).Set("completed", Arr(t.Completed)).Set("startedAtMs", N(t.StartedAtMs))
                .Set("profilesViewed", Arr(t.ProfilesViewed)).Set("starterPackUsed", JsonValue.From(t.StarterPackUsed)));
            o.Set("pendingPack", s.PendingPack == null ? JsonValue.Null : PendingPackToJson(s.PendingPack));
            o.Set("unlockedAchievements", Arr(s.UnlockedAchievements));
            o.Set("breedingNest", s.BreedingNest == null ? JsonValue.Null : NestToJson(s.BreedingNest));
            var codex = new JsonObject();
            foreach (CodexEntry e in s.Codex)
            {
                var muts = new JsonArray(); foreach (MutationType m in e.DiscoveredMutations) muts.Add(S(m.ToWire()));
                var stages = new JsonArray(); foreach (GrowthStage gst in e.ObservedStages) stages.Add(S(gst.ToWire()));
                codex.Set(e.SpeciesId, new JsonObject().Set("discoveredMutations", muts).Set("totalHatched", N(e.TotalHatched)).Set("observedStages", stages));
            }
            o.Set("codex", codex);
            if (s.Expedition == null) o.Set("expedition", JsonValue.Null);
            else
            {
                ExpeditionJob j = s.Expedition;
                var rewards = new JsonObject();
                foreach (var kv in j.Rewards) rewards.Set(kv.Key, N(kv.Value));
                o.Set("expedition", new JsonObject().Set("id", S(j.Id)).Set("routeId", S(j.RouteId)).Set("dragonUid", S(j.DragonUid))
                    .Set("startedAtMs", N(j.StartedAtMs)).Set("finishAtMs", N(j.FinishAtMs)).Set("rewards", rewards).Set("status", S(j.Status.ToWire())));
            }
            o.Set("completedQuests", Arr(s.CompletedQuests));
            o.Set("grantedRewards", Arr(s.GrantedRewards));
            var queue = new JsonArray();
            foreach (QueuedEgg q in s.RewardQueue)
                queue.Add(new JsonObject().Set("rewardId", S(q.RewardId)).Set("speciesId", S(q.SpeciesId))
                    .Set("mutation", S(Dravio.Domain.WireNames.ToWire(q.Mutation))).Set("queuedAtMs", N(q.QueuedAtMs)));
            o.Set("rewardQueue", queue);
            o.Set("claimedAchievements", Arr(s.ClaimedAchievements));
            HealthToJson(s, o);
            if (s.SchemaVersion >= 9) U210ToJson(s, o);
            o.Set("processedRequestIds", Arr(s.ProcessedRequestIds));
            o.Set("pityCount", N(p.PityCount));
            GameStats st = p.Stats;
            o.Set("stats", new JsonObject().Set("totalHatched", N(st.TotalHatched)).Set("totalSold", N(st.TotalSold)).Set("totalBred", N(st.TotalBred))
                .Set("totalFeedClicks", N(st.TotalFeedClicks)).Set("dailyFreePacksClaimed", N(st.DailyFreePacksClaimed)).Set("lastDailyResetDate", S(st.LastDailyResetDate)));
            GameSettings se = p.Settings;
            o.Set("settings", new JsonObject().Set("language", S(se.Language.ToWire())).Set("sfxVolume", JsonValue.From(se.SfxVolume)).Set("bgmVolume", JsonValue.From(se.BgmVolume))
                .Set("hapticsEnabled", JsonValue.From(se.HapticsEnabled)).Set("fpsLimit", N(se.FpsLimit)).Set("timeMode", S(se.TimeMode.ToWire()))
                .Set("compactMode", JsonValue.From(se.CompactMode)).Set("reducedMotion", JsonValue.From(se.ReducedMotion)).Set("alwaysOnTop", JsonValue.From(se.AlwaysOnTop))
                .Set("mode", S(se.Mode.ToWire())).Set("difficulty", S(se.Difficulty.ToWire())));
            o.Set("clock", new JsonObject().Set("lastProcessedAtMs", N(s.Clock.LastProcessedAtMs)).Set("highWaterAtMs", N(s.Clock.HighWaterAtMs)));
            if (s.OfflineReceipt == null) o.Set("offlineReceipt", JsonValue.Null);
            else
            {
                OfflineReceipt r = s.OfflineReceipt;
                o.Set("offlineReceipt", new JsonObject().Set("id", S(r.Id)).Set("fromMs", N(r.FromMs)).Set("toMs", N(r.ToMs)).Set("goldMicro", N(r.GoldMicro))
                    .Set("maturedIds", Arr(r.MaturedIds)).Set("bonusClaimed", JsonValue.From(r.BonusClaimed)).Set("createdAtMs", N(r.CreatedAtMs)));
            }
            return o;
        }

        // ---------------- U2.6 (schema v8) ----------------

        private static JsonObject VitalsToJson(Vitals v) =>
            new JsonObject().Set("health", N(v.Health)).Set("energy", N(v.Energy)).Set("comfort", N(v.Comfort)).Set("mobility", N(v.Mobility)).Set("hydration", N(v.Hydration));

        private static void HealthToJson(GameState s, JsonObject o)
        {
            var cases = new JsonArray();
            foreach (HealthCase c in s.HealthCases)
            {
                var co = new JsonObject().Set("caseId", S(c.CaseId)).Set("dragonId", S(c.DragonId));
                if (c.EventId != null) co.Set("eventId", S(c.EventId));
                co.Set("cause", S(c.Cause.ToWire())).Set("site", S(c.Site.ToWire())).Set("severity", S(c.Severity.ToWire())).Set("symptoms", Arr(c.Symptoms))
                    .Set("assessed", JsonValue.From(c.Assessed));
                if (c.CarePlanId != null) co.Set("carePlanId", S(c.CarePlanId));
                co.Set("createdAtMs", N(c.CreatedAtMs)).Set("startedAtMs", N(c.StartedAtMs)).Set("durationMs", N(c.DurationMs)).Set("finishAtMs", N(c.FinishAtMs))
                    .Set("nextReviewAtMs", N(c.NextReviewAtMs)).Set("phase", S(c.Phase.ToWire())).Set("progress", N(c.Progress))
                    .Set("reviewed", JsonValue.From(c.Reviewed)).Set("status", S(c.Status.ToWire()));
                cases.Add(co);
            }
            o.Set("healthCases", cases);
            var evs = new JsonArray();
            foreach (DisasterState ev in s.Disasters)
            {
                var sh = new JsonArray();
                foreach (ShelteredDragon x in ev.Sheltered)
                    sh.Add(new JsonObject().Set("uid", S(x.Uid)).Set("habitatId", S(x.HabitatId)).Set("producing", JsonValue.From(x.Producing)).Set("auto", JsonValue.From(x.Auto)));
                evs.Add(new JsonObject().Set("id", S(ev.Id)).Set("hazard", S(ev.Hazard.ToWire())).Set("zoneId", S(ev.ZoneId)).Set("severity", S(ev.Severity.ToWire()))
                    .Set("phase", S(ev.Phase.ToWire())).Set("createdAtMs", N(ev.CreatedAtMs)).Set("forecastProgressMs", N(ev.ForecastProgressMs))
                    .Set("struckAtMs", N(ev.StruckAtMs)).Set("caseIds", Arr(ev.CaseIds)).Set("sheltered", sh));
            }
            o.Set("disasters", evs);
            var cd = new JsonObject();
            foreach (var kv in s.Hazard.CooldownUntilMs) cd.Set(kv.Key, N(kv.Value));
            o.Set("hazard", new JsonObject().Set("foregroundMs", N(s.Hazard.ForegroundMs)).Set("checksDone", N(s.Hazard.ChecksDone)).Set("cooldownUntilMs", cd));
        }

        private static Vitals VitalsFromJson(JsonValue v, string path, List<string> errors)
        {
            if (v == null) return null; // trước v8 (đã qua migration thì luôn có); validator v8 báo thiếu
            var r = new Reader(new List<string>());
            if (!(v is JsonObject o)) { errors.Add(path + ".vitals: out of range"); return null; }
            var vit = new Vitals
            {
                Health = r.IntOr(o, "health", "x", -1), Energy = r.IntOr(o, "energy", "x", -1), Comfort = r.IntOr(o, "comfort", "x", -1),
                Mobility = r.IntOr(o, "mobility", "x", -1), Hydration = r.IntOr(o, "hydration", "x", -1),
            };
            if (r.Errors.Count > 0) errors.Add(path + ".vitals: out of range");
            return vit;
        }

        private static void HealthFromJson(JsonObject o, GameState s, List<string> errors)
        {
            s.HealthCases = null;
            if (o.Get("healthCases") is JsonArray ca)
            {
                var r = new Reader(new List<string>());
                s.HealthCases = new List<HealthCase>();
                foreach (JsonValue cv in ca)
                {
                    if (!(cv is JsonObject c)) { r.Errors.Add("x"); continue; }
                    s.HealthCases.Add(new HealthCase
                    {
                        CaseId = r.Str(c, "caseId", "x"), DragonId = r.Str(c, "dragonId", "x"), EventId = r.OptStr(c, "eventId", "x"),
                        Cause = r.Enum(c, "cause", "x", HazardId.Wildfire), Site = r.Enum(c, "site", "x", InjurySite.SkinScale), Severity = r.Enum(c, "severity", "x", CaseSeverity.Mild),
                        Symptoms = r.StrList(c, "symptoms", "x"), Assessed = r.Bool(c, "assessed", "x"), CarePlanId = r.OptStr(c, "carePlanId", "x"),
                        CreatedAtMs = r.LongOr(c, "createdAtMs", "x", -1), StartedAtMs = r.LongOr(c, "startedAtMs", "x", -1), DurationMs = r.LongOr(c, "durationMs", "x", -1),
                        FinishAtMs = r.LongOr(c, "finishAtMs", "x", -1), NextReviewAtMs = r.LongOr(c, "nextReviewAtMs", "x", -1),
                        Phase = r.Enum(c, "phase", "x", CasePhase.Stabilize), Progress = r.IntOr(c, "progress", "x", -1), Reviewed = r.Bool(c, "reviewed", "x"),
                        Status = r.Enum(c, "status", "x", CaseStatus.Queued),
                    });
                }
                if (r.Errors.Count > 0) errors.Add("healthCases: invalid");
            }
            else errors.Add("healthCases: invalid");

            s.Disasters = null;
            if (o.Get("disasters") is JsonArray da)
            {
                var r = new Reader(new List<string>());
                s.Disasters = new List<DisasterState>();
                foreach (JsonValue dv in da)
                {
                    if (!(dv is JsonObject d)) { r.Errors.Add("x"); continue; }
                    var ev = new DisasterState
                    {
                        Id = r.Str(d, "id", "x"), Hazard = r.Enum(d, "hazard", "x", HazardId.Wildfire), ZoneId = r.Str(d, "zoneId", "x"),
                        Severity = r.Enum(d, "severity", "x", CaseSeverity.Mild), Phase = r.Enum(d, "phase", "x", DisasterPhase.Forecast),
                        CreatedAtMs = r.LongOr(d, "createdAtMs", "x", -1), ForecastProgressMs = r.LongOr(d, "forecastProgressMs", "x", -1),
                        StruckAtMs = r.LongOr(d, "struckAtMs", "x", -1), CaseIds = r.StrList(d, "caseIds", "x"), Sheltered = new List<ShelteredDragon>(),
                    };
                    if (d.Get("sheltered") is JsonArray sa)
                        foreach (JsonValue sv in sa)
                        {
                            if (!(sv is JsonObject so)) { r.Errors.Add("x"); continue; }
                            ev.Sheltered.Add(new ShelteredDragon { Uid = r.Str(so, "uid", "x"), HabitatId = r.Str(so, "habitatId", "x"), Producing = r.Bool(so, "producing", "x"), Auto = r.Bool(so, "auto", "x") });
                        }
                    else r.Errors.Add("x");
                    s.Disasters.Add(ev);
                }
                if (r.Errors.Count > 0) errors.Add("disasters: invalid");
            }
            else errors.Add("disasters: invalid");

            s.Hazard = null;
            if (o.Get("hazard") is JsonObject h && h.Get("cooldownUntilMs") is JsonObject cd)
            {
                var r = new Reader(new List<string>());
                s.Hazard = new HazardState { ForegroundMs = r.LongOr(h, "foregroundMs", "x", -1), ChecksDone = r.LongOr(h, "checksDone", "x", -1) };
                foreach (var kv in cd)
                {
                    if (Reader.AsSafeLong(kv.Value, out long v)) s.Hazard.CooldownUntilMs.Add(new KeyValuePair<string, long>(kv.Key, v));
                    else r.Errors.Add("x");
                }
                if (r.Errors.Count > 0) errors.Add("hazard: invalid");
            }
            else errors.Add("hazard: invalid");
        }

        private static JsonObject GenotypeToJson(Genotype g)
        {
            var o = new JsonObject();
            foreach (var kv in g) o.Set(kv.Key, new JsonArray().Add(S(kv.Value.First)).Add(S(kv.Value.Second)));
            return o;
        }

        private static JsonObject PendingPackToJson(PendingPack p) =>
            new JsonObject().Set("id", S(p.Id)).Set("packId", S(p.PackId)).Set("speciesId", S(p.SpeciesId)).Set("genotype", GenotypeToJson(p.Genotype))
                .Set("mutation", S(p.Mutation.ToWire())).Set("createdAtMs", N(p.CreatedAtMs)).Set("tutorial", JsonValue.From(p.Tutorial));

        private static JsonObject NestToJson(BreedingEgg e) =>
            new JsonObject().Set("id", S(e.Id)).Set("parentIds", Arr(e.ParentIds)).Set("speciesId", S(e.SpeciesId)).Set("genotype", GenotypeToJson(e.Genotype))
                .Set("mutation", S(e.Mutation.ToWire())).Set("createdAtMs", N(e.CreatedAtMs)).Set("finishAtMs", N(e.FinishAtMs)).Set("durationMs", N(e.DurationMs))
                .Set("tapReducedMs", N(e.TapReducedMs)).Set("lastTapAtMs", N(e.LastTapAtMs)).Set("status", S(e.Status.ToWire()));

        public static JsonObject DragonToJson(DragonState d)
        {
            var o = new JsonObject().Set("uid", S(d.Uid)).Set("speciesId", S(d.SpeciesId));
            if (d.Nickname != null) o.Set("nickname", S(d.Nickname));
            o.Set("genotype", GenotypeToJson(d.Genotype)).Set("mutation", S(d.Mutation.ToWire())).Set("xpSec", JsonValue.From(d.XpSec)).Set("locked", JsonValue.From(d.Locked));
            var loc = new JsonObject().Set("kind", S(d.Location.Kind.ToWire()));
            if (d.Location.ZoneId != null) loc.Set("zoneId", S(d.Location.ZoneId));
            if (d.Location.HabitatId != null) loc.Set("habitatId", S(d.Location.HabitatId));
            if (d.Location.CaseId != null) loc.Set("caseId", S(d.Location.CaseId));
            o.Set("location", loc).Set("hatchedAtMs", N(d.HatchedAtMs)).Set("personality", S(d.Personality.ToWire())).Set("bond", N(d.Bond));
            if (d.BondDay != null) o.Set("bondDay", S(d.BondDay));
            if (d.BondDaily.HasValue) o.Set("bondDaily", N(d.BondDaily.Value));
            if (d.LastFeedXpAtMs.HasValue) o.Set("lastFeedXpAtMs", N(d.LastFeedXpAtMs.Value));
            if (d.LastPetAtMs.HasValue) o.Set("lastPetAtMs", N(d.LastPetAtMs.Value));
            o.Set("parentIds", d.ParentIds == null ? JsonValue.Null : Arr(d.ParentIds));
            if (d.GrandparentIds != null) o.Set("grandparentIds", Arr(d.GrandparentIds));
            o.Set("generation", N(d.Generation)).Set("ancestry", S(d.Ancestry.ToWire())).Set("origin", S(d.Origin.ToWire()))
                .Set("activity", S(d.Activity.ToWire())).Set("breedCooldownUntilMs", N(d.BreedCooldownUntilMs));
            if (d.Vitals != null) o.Set("vitals", VitalsToJson(d.Vitals));
            if (d.RegistryRef != null) o.Set("registryRef", S(d.RegistryRef));
            return o;
        }

        // ---------------- JSON → GameState ----------------

        /// <summary>
        /// Đọc state JSON thành GameState có kiểu. Bản TS chạy validateGameState trực tiếp trên object thô; C# phải
        /// đổi sang kiểu trước, nên mọi sai kiểu/thiếu trường được báo bằng cùng đường dẫn và thông điệp mà
        /// validateGameState của TS sẽ báo (ví dụ "goldMicro: must be a non-negative integer"), sau đó
        /// GameStateValidator kiểm phần còn lại. Khóa lạ bị bỏ qua (xem README: khác biệt nền tảng).
        /// </summary>
        public static GameState FromJson(JsonObject o, List<string> errors)
        {
            var r = new Reader(errors);
            var s = new GameState
            {
                SchemaVersion = r.IntOr(o, "schemaVersion", "schemaVersion: invalid", -1),
                ContentVersion = r.Str(o, "contentVersion", "contentVersion: invalid"),
                SaveId = r.Str(o, "saveId", "saveId: invalid"),
                Revision = r.LongOr(o, "revision", "revision: invalid", -1),
            };
            var p = s.Player;
            p.Gold = MicroGold.FromMicro(r.LongOr(o, "goldMicro", "goldMicro: must be a non-negative integer", -1));
            p.GoldRemainder = r.LongOr(o, "goldRemainder", "goldRemainder: out of range", -1);
            p.TotalGoldEarned = MicroGold.FromMicro(r.LongOr(o, "totalGoldEarnedMicro", "totalGoldEarnedMicro: invalid", -1));
            p.PityCount = r.IntOr(o, "pityCount", "pityCount: out of range", -1);

            s.Dragons = null;
            if (o.Get("dragons") is JsonArray da)
            {
                s.Dragons = new List<DragonState>();
                for (int i = 0; i < da.Count; i++)
                {
                    if (da[i] is JsonObject dob) s.Dragons.Add(DragonFromJson(dob, "dragons[" + i + "]", r));
                    else { errors.Add("dragons[" + i + "]: not an object"); s.Dragons.Add(null); }
                }
            }
            else errors.Add("dragons: not an array");

            s.Zones = null;
            if (o.Get("zones") is JsonArray za && za.Count == 3)
            {
                s.Zones = new List<ZoneState>();
                for (int i = 0; i < za.Count; i++)
                {
                    string zp = "zones[" + i + "]";
                    if (!(za[i] is JsonObject z)) { errors.Add(zp + ": invalid"); s.Zones.Add(null); continue; }
                    s.Zones.Add(new ZoneState
                    {
                        Id = r.Str(z, "id", zp + ": invalid"),
                        Realm = r.Enum(z, "realm", zp + ".realm: invalid", Realm.Land),
                        Unlocked = r.Bool(z, "unlocked", zp + ".unlocked: invalid"),
                        Capacity = r.IntOr(z, "capacity", zp + ".capacity: must be 5 in P0", -1),
                        UnlockedHabitatIds = r.StrList(z, "unlockedHabitatIds", zp + ".unlockedHabitatIds: invalid"),
                        DecorationIds = r.StrList(z, "decorationIds", zp + ".decorationIds: invalid"),
                        ActiveDecorationIds = r.StrList(z, "activeDecorationIds", zp + ".activeDecorationIds: invalid"),
                        Recovery = r.IntOr(z, "recovery", zp + ".recovery: out of range", -1),
                    });
                }
            }
            else errors.Add("zones: must list origin, ocean, sky");

            s.ProductionDragonIds = r.StrList(o, "productionDragonIds", "productionDragonIds: invalid");

            p.Inventory = null;
            if (o.Get("inventory") is JsonObject inv)
            {
                p.Inventory = new Dictionary<string, long>(StringComparer.Ordinal);
                foreach (var kv in inv)
                {
                    if (Reader.AsSafeLong(kv.Value, out long n)) p.Inventory[kv.Key] = n;
                    else { errors.Add("inventory: invalid"); break; }
                }
            }
            else errors.Add("inventory: invalid");

            p.Upgrades = null;
            if (o.Get("upgrades") is JsonObject up)
            {
                p.Upgrades = new UpgradesState
                {
                    IslandCapacityLevel = r.IntOr(up, "islandCapacityLevel", "upgrades.islandCapacityLevel: out of range", -1),
                    FoodQualityLevel = r.IntOr(up, "foodQualityLevel", "upgrades.foodQualityLevel: out of range", -1),
                    FoodCooldownLevel = r.IntOr(up, "foodCooldownLevel", "upgrades.foodCooldownLevel: out of range", -1),
                    MutationLuckLevel = r.IntOr(up, "mutationLuckLevel", "upgrades.mutationLuckLevel: out of range", -1),
                    HatchSpeedLevel = r.IntOr(up, "hatchSpeedLevel", "upgrades.hatchSpeedLevel: out of range", -1),
                };
            }
            else errors.Add("upgrades: invalid");

            s.Food = null;
            if (o.Get("food") is JsonObject food && food.Get("drops") is JsonArray drops &&
                (food.Get("lastSpawnAtMs") is JsonNull || Reader.AsSafeLong(food.Get("lastSpawnAtMs"), out _)))
            {
                s.Food = new FoodState { LastSpawnAtMs = Reader.AsSafeLong(food.Get("lastSpawnAtMs"), out long ls) ? ls : (long?)null };
                for (int i = 0; i < drops.Count; i++)
                {
                    string fp = "food.drops[" + i + "]";
                    var fr = new Reader(new List<string>());
                    if (!(drops[i] is JsonObject f)) { errors.Add(fp + ": invalid"); s.Food.Drops.Add(null); continue; }
                    var drop = new FoodDrop
                    {
                        Id = fr.Str(f, "id", "x"), DietId = fr.Enum(f, "dietId", "x", DietId.Fruit), ZoneId = fr.Str(f, "zoneId", "x"), HabitatId = fr.Str(f, "habitatId", "x"),
                        SpawnedAtMs = fr.LongOr(f, "spawnedAtMs", "x", 0), ExpiresAtMs = fr.LongOr(f, "expiresAtMs", "x", 0),
                    };
                    if (fr.Errors.Count > 0) { errors.Add(fp + ": invalid"); s.Food.Drops.Add(null); }
                    else s.Food.Drops.Add(drop);
                }
            }
            else errors.Add("food: invalid");

            s.ProcessedRequestIds = r.StrList(o, "processedRequestIds", "processedRequestIds: invalid");

            p.Stats = null;
            if (o.Get("stats") is JsonObject st)
            {
                p.Stats = new GameStats
                {
                    TotalHatched = r.LongOr(st, "totalHatched", "stats.totalHatched: invalid", -1),
                    TotalSold = r.LongOr(st, "totalSold", "stats.totalSold: invalid", -1),
                    TotalBred = r.LongOr(st, "totalBred", "stats.totalBred: invalid", -1),
                    TotalFeedClicks = r.LongOr(st, "totalFeedClicks", "stats.totalFeedClicks: invalid", -1),
                    DailyFreePacksClaimed = r.LongOr(st, "dailyFreePacksClaimed", "stats.dailyFreePacksClaimed: invalid", -1),
                    LastDailyResetDate = r.Str(st, "lastDailyResetDate", "stats.lastDailyResetDate: invalid"),
                };
            }
            else errors.Add("stats: invalid");

            p.Settings = null;
            if (o.Get("settings") is JsonObject se)
            {
                p.Settings = new GameSettings
                {
                    Language = r.Enum(se, "language", "settings.language: invalid", LanguageCode.Vi),
                    SfxVolume = r.Num(se, "sfxVolume", "settings.sfxVolume: out of range"),
                    BgmVolume = r.Num(se, "bgmVolume", "settings.bgmVolume: out of range"),
                    HapticsEnabled = r.Bool(se, "hapticsEnabled", "settings.hapticsEnabled: invalid"),
                    FpsLimit = r.IntOr(se, "fpsLimit", "settings.fpsLimit: invalid", 0),
                    TimeMode = r.Enum(se, "timeMode", "settings.timeMode: invalid", TimeMode.Auto),
                    CompactMode = r.Bool(se, "compactMode", "settings.compactMode: invalid"),
                    ReducedMotion = r.Bool(se, "reducedMotion", "settings.reducedMotion: invalid"),
                    AlwaysOnTop = r.Bool(se, "alwaysOnTop", "settings.alwaysOnTop: invalid"),
                    Mode = r.Enum(se, "mode", "settings.mode: invalid", AppMode.World),
                    Difficulty = r.Enum(se, "difficulty", "settings.difficulty: invalid", Difficulty.Ecosystem),
                };
            }
            else errors.Add("settings: invalid");

            s.Clock = null;
            if (o.Get("clock") is JsonObject c && Reader.AsSafeLong(c.Get("lastProcessedAtMs"), out long lp) && Reader.AsSafeLong(c.Get("highWaterAtMs"), out long hw))
                s.Clock = new ClockState(lp, hw);
            else errors.Add("clock: invalid");

            JsonValue rv = o.Get("offlineReceipt");
            if (rv is JsonObject ro)
            {
                var rr = new Reader(new List<string>());
                s.OfflineReceipt = new OfflineReceipt
                {
                    Id = rr.Str(ro, "id", "x"), FromMs = rr.LongOr(ro, "fromMs", "x", 0), ToMs = rr.LongOr(ro, "toMs", "x", 0), GoldMicro = rr.LongOr(ro, "goldMicro", "x", 0),
                    MaturedIds = ro.Get("maturedIds") is JsonArray ? rr.AnyList(ro, "maturedIds") : null,
                    BonusClaimed = rr.Bool(ro, "bonusClaimed", "x"), CreatedAtMs = rr.LongOr(ro, "createdAtMs", "x", 0),
                };
                if (rr.Errors.Count > 0) errors.Add("offlineReceipt: invalid");
            }
            else if (!(rv is JsonNull)) errors.Add("offlineReceipt: invalid");

            s.Discoveries = r.StrList(o, "discoveries", "discoveries: invalid");

            if (o.Get("tutorial") is JsonObject t)
            {
                s.Tutorial = new TutorialState
                {
                    Status = r.Enum(t, "status", "tutorial.status: invalid", TutorialStatus.Active),
                    Completed = r.StrList(t, "completed", "tutorial.completed: not an ordered prefix of TUT_01…TUT_08"),
                    StartedAtMs = r.LongOr(t, "startedAtMs", "tutorial.startedAtMs: invalid", -1),
                    ProfilesViewed = r.StrList(t, "profilesViewed", "tutorial.profilesViewed: invalid"),
                    StarterPackUsed = r.Bool(t, "starterPackUsed", "tutorial.starterPackUsed: not boolean"),
                };
            }
            else { s.Tutorial = null; errors.Add("tutorial: not an object"); }

            JsonValue pv = o.Get("pendingPack");
            if (pv is JsonObject po)
            {
                var pr = new Reader(new List<string>());
                s.PendingPack = new PendingPack
                {
                    Id = pr.Str(po, "id", "x"), PackId = pr.Str(po, "packId", "x"), SpeciesId = pr.Str(po, "speciesId", "x"),
                    Genotype = GenotypeFromJson(po.Get("genotype"), pr), Mutation = pr.Enum(po, "mutation", "x", MutationType.Normal),
                    CreatedAtMs = pr.LongOr(po, "createdAtMs", "x", -1), Tutorial = pr.Bool(po, "tutorial", "x"),
                };
                if (pr.Errors.Count > 0) errors.Add("pendingPack: invalid");
            }
            else if (!(pv is JsonNull)) errors.Add("pendingPack: invalid");

            s.UnlockedAchievements = r.StrList(o, "unlockedAchievements", "unlockedAchievements: invalid");
            s.ClaimedAchievements = r.StrList(o, "claimedAchievements", "claimedAchievements: invalid");

            JsonValue nv = o.Get("breedingNest");
            if (nv is JsonObject no)
            {
                var nr = new Reader(new List<string>());
                List<string> parents = no.Get("parentIds") is JsonArray ? nr.StrList(no, "parentIds", "x") : null;
                s.BreedingNest = new BreedingEgg
                {
                    Id = nr.Str(no, "id", "x"), ParentIds = parents?.ToArray(), SpeciesId = nr.Str(no, "speciesId", "x"),
                    Genotype = GenotypeFromJson(no.Get("genotype"), nr), Mutation = nr.Enum(no, "mutation", "x", MutationType.Normal),
                    CreatedAtMs = nr.LongOr(no, "createdAtMs", "x", -1), FinishAtMs = nr.LongOr(no, "finishAtMs", "x", -1),
                    DurationMs = nr.LongOr(no, "durationMs", "x", -1), TapReducedMs = nr.LongOr(no, "tapReducedMs", "x", -1),
                    LastTapAtMs = nr.LongOr(no, "lastTapAtMs", "x", -1),
                    Status = nr.Enum(no, "status", "x", EggStatus.Incubating),
                };
                if (parents == null) nr.Errors.Add("parentIds");
                if (nr.Errors.Count > 0) errors.Add("breedingNest: invalid");
            }
            else if (!(nv is JsonNull)) errors.Add("breedingNest: invalid");

            if (o.Get("codex") is JsonObject co)
            {
                var cr = new Reader(new List<string>());
                s.Codex = new List<CodexEntry>();
                foreach (var kv in co)
                {
                    if (!(kv.Value is JsonObject eo)) { cr.Errors.Add("x"); continue; }
                    var e = new CodexEntry { SpeciesId = kv.Key, TotalHatched = cr.LongOr(eo, "totalHatched", "x", -1) };
                    foreach (string m in cr.StrList(eo, "discoveredMutations", "x") ?? new List<string>())
                        if (WireNames.TryParse(m, out MutationType mt)) e.DiscoveredMutations.Add(mt); else cr.Errors.Add("x");
                    foreach (string stw in cr.StrList(eo, "observedStages", "x") ?? new List<string>())
                        if (WireNames.TryParse(stw, out GrowthStage gs)) e.ObservedStages.Add(gs); else cr.Errors.Add("x");
                    s.Codex.Add(e);
                }
                if (cr.Errors.Count > 0) errors.Add("codex: invalid");
            }
            else { s.Codex = null; errors.Add("codex: invalid"); }

            JsonValue ev = o.Get("expedition");
            if (ev is JsonObject eo2)
            {
                var er = new Reader(new List<string>());
                var job = new ExpeditionJob
                {
                    Id = er.Str(eo2, "id", "x"), RouteId = er.Str(eo2, "routeId", "x"), DragonUid = er.Str(eo2, "dragonUid", "x"),
                    StartedAtMs = er.LongOr(eo2, "startedAtMs", "x", -1), FinishAtMs = er.LongOr(eo2, "finishAtMs", "x", -1),
                    Status = er.Enum(eo2, "status", "x", ExpeditionStatus.InProgress),
                };
                if (eo2.Get("rewards") is JsonObject ro2)
                    foreach (var kv in ro2)
                    {
                        if (kv.Value is JsonNumber n && GameStateJson.Reader.AsSafeLong(n, out long v) && v > 0 && v <= int.MaxValue) job.Rewards.Add(new KeyValuePair<string, int>(kv.Key, (int)v));
                        else er.Errors.Add("x");
                    }
                else er.Errors.Add("x");
                s.Expedition = job;
                if (er.Errors.Count > 0) errors.Add("expedition: invalid");
            }
            else if (!(ev is JsonNull)) errors.Add("expedition: invalid");

            var qr = new Reader(new List<string>());
            s.CompletedQuests = qr.StrList(o, "completedQuests", "x") ?? new List<string>();
            s.GrantedRewards = qr.StrList(o, "grantedRewards", "x") ?? new List<string>();
            if (qr.Errors.Count > 0) errors.Add("worldUnlock: invalid");
            s.RewardQueue = new List<QueuedEgg>();
            if (o.Get("rewardQueue") is JsonArray rq)
            {
                var rr = new Reader(new List<string>());
                foreach (JsonValue qv in rq)
                {
                    if (!(qv is JsonObject qo)) { rr.Errors.Add("x"); continue; }
                    s.RewardQueue.Add(new QueuedEgg
                    {
                        RewardId = rr.Str(qo, "rewardId", "x"), SpeciesId = rr.Str(qo, "speciesId", "x"),
                        Mutation = rr.Enum(qo, "mutation", "x", MutationType.Normal), QueuedAtMs = rr.LongOr(qo, "queuedAtMs", "x", -1),
                    });
                }
                if (rr.Errors.Count > 0) errors.Add("rewardQueue: invalid");
            }
            else errors.Add("rewardQueue: invalid");
            HealthFromJson(o, s, errors);
            if (s.SchemaVersion >= 9) U210FromJson(o, s, errors);
            return s;
        }

        // ---------------- v9 (U2.10): giới hạn gói theo ngày, dự án Sanctuary, sổ nghiên cứu ----------------

        private static void U210ToJson(GameState s, JsonObject o)
        {
            PackDailyState pd = s.PackDaily ?? new PackDailyState();
            o.Set("packDaily", new JsonObject().Set("day", pd.Day == null ? JsonValue.Null : S(pd.Day)).Set("starterBought", N(pd.StarterBought)));
            o.Set("sanctuaryProjectsBuilt", N(s.SanctuaryProjectsBuilt));
            // Ghi bản đã làm sạch (như khi nạp): save ghi ra luôn đọc lại được, kể cả nếu sổ nghiên cứu lệch state ở đâu đó.
            Dravio.Domain.Behavior.ResearchLogSnapshot snap = (s.Research ?? new Dravio.Domain.Behavior.ResearchLog()).ToSnapshot();
            if (s.Dragons != null && s.Codex != null) snap = Dravio.Domain.Behavior.ResearchSchemaDraft.Sanitize(snap, s);
            o.Set("research", ResearchToJson(snap));
        }

        private static JsonObject ResearchToJson(Dravio.Domain.Behavior.ResearchLogSnapshot snap)
        {
            var species = new JsonArray();
            foreach (var e in snap.Species)
            {
                var fbd = new JsonObject();
                foreach (var kv in e.FindingsByDay) fbd.Set(kv.Key, N(kv.Value));
                species.Add(new JsonObject().Set("speciesId", S(e.SpeciesId)).Set("behaviors", Arr(e.Behaviors)).Set("phases", Arr(e.Phases))
                    .Set("habitats", Arr(e.Habitats)).Set("days", Arr(e.Days)).Set("findingsByDay", fbd));
            }
            var inds = new JsonArray();
            foreach (var e in snap.Individuals)
                inds.Add(new JsonObject().Set("uid", S(e.Uid)).Set("speciesId", S(e.SpeciesId)).Set("observations", N(e.Observations))
                    .Set("seenInFavoriteHabitat", JsonValue.From(e.SeenInFavoriteHabitat)).Set("seenFeeding", JsonValue.From(e.SeenFeeding))
                    .Set("lastObservedAtMs", N(e.LastObservedAtMs)).Set("activePhases", Arr(e.ActivePhases)).Set("restPhases", Arr(e.RestPhases))
                    .Set("revealed", Arr(e.Revealed)));
            return new JsonObject().Set("species", species).Set("individuals", inds);
        }

        private static void U210FromJson(JsonObject o, GameState s, List<string> errors)
        {
            var r = new Reader(new List<string>());
            s.PackDaily = null;
            if (o.Get("packDaily") is JsonObject pd)
            {
                JsonValue day = pd.Get("day");
                s.PackDaily = new PackDailyState { Day = day is JsonString ds ? ds.Value : null, StarterBought = r.IntOr(pd, "starterBought", "x", -1) };
                if (!(day is JsonString) && !(day is JsonNull)) r.Errors.Add("x");
            }
            else r.Errors.Add("x");
            if (r.Errors.Count > 0) errors.Add("packDaily: invalid");
            s.SanctuaryProjectsBuilt = new Reader(errors).IntOr(o, "sanctuaryProjectsBuilt", "sanctuaryProjectsBuilt: out of range", -1);
            // Sổ nghiên cứu không chạm vàng, rồng hay vật liệu: khối hỏng hoặc mục không hợp lệ bị BỎ khi nạp, không làm hỏng save (ADR 0204 R8).
            s.Research = new Dravio.Domain.Behavior.ResearchLog();
            if (s.Dragons == null || s.Codex == null) return;
            Dravio.Domain.Behavior.ResearchLogSnapshot snap = ResearchFromJson(o.Get("research"));
            if (snap == null) return;
            s.Research = Dravio.Domain.Behavior.ResearchLog.FromSnapshot(Dravio.Domain.Behavior.ResearchSchemaDraft.Sanitize(snap, s));
        }

        private static Dravio.Domain.Behavior.ResearchLogSnapshot ResearchFromJson(JsonValue v)
        {
            if (!(v is JsonObject o) || !(o.Get("species") is JsonArray sa) || !(o.Get("individuals") is JsonArray ia)) return null;
            var snap = new Dravio.Domain.Behavior.ResearchLogSnapshot();
            var r = new Reader(new List<string>());
            foreach (JsonValue x in sa)
            {
                if (!(x is JsonObject e)) continue;
                var se = new Dravio.Domain.Behavior.ResearchLogSnapshot.SpeciesEntry
                {
                    SpeciesId = e.Get("speciesId") is JsonString sp ? sp.Value : null,
                    Behaviors = r.StrList(e, "behaviors", "x") ?? new List<string>(),
                    Phases = r.StrList(e, "phases", "x") ?? new List<string>(),
                    Habitats = r.StrList(e, "habitats", "x") ?? new List<string>(),
                    Days = r.StrList(e, "days", "x") ?? new List<string>(),
                };
                if (e.Get("findingsByDay") is JsonObject f)
                    foreach (var kv in f)
                        if (Reader.AsSafeLong(kv.Value, out long n) && n >= 0 && n <= int.MaxValue) se.FindingsByDay.Add(new KeyValuePair<string, int>(kv.Key, (int)n));
                snap.Species.Add(se);
            }
            foreach (JsonValue x in ia)
            {
                if (!(x is JsonObject e)) continue;
                var ir = new Reader(new List<string>());
                var ie = new Dravio.Domain.Behavior.ResearchLogSnapshot.IndividualEntry
                {
                    Uid = e.Get("uid") is JsonString u ? u.Value : null,
                    SpeciesId = e.Get("speciesId") is JsonString sp ? sp.Value : null,
                    Observations = ir.IntOr(e, "observations", "x", -1),
                    SeenInFavoriteHabitat = e.Get("seenInFavoriteHabitat") is JsonBool b1 && b1.Value,
                    SeenFeeding = e.Get("seenFeeding") is JsonBool b2 && b2.Value,
                    LastObservedAtMs = ir.LongOr(e, "lastObservedAtMs", "x", 0),
                    ActivePhases = ir.StrList(e, "activePhases", "x") ?? new List<string>(),
                    RestPhases = ir.StrList(e, "restPhases", "x") ?? new List<string>(),
                    Revealed = ir.StrList(e, "revealed", "x") ?? new List<string>(),
                };
                if (ir.Errors.Count == 0) snap.Individuals.Add(ie);
            }
            return snap;
        }

        private static Genotype GenotypeFromJson(JsonValue v, Reader r)
        {
            if (!(v is JsonObject o)) { r.Errors.Add("genotype"); return null; }
            var g = new Genotype();
            foreach (var kv in o)
            {
                if (kv.Value is JsonArray a && a.Count == 2 && a[0] is JsonString x && a[1] is JsonString y) g.Set(kv.Key, x.Value, y.Value);
                else { r.Errors.Add("genotype"); return null; }
            }
            return g;
        }

        public static DragonState DragonFromJson(JsonObject d, string path, Reader parent)
        {
            var errors = parent.Errors;
            var r = new Reader(errors);
            var g = new Reader(new List<string>());
            var dragon = new DragonState
            {
                Uid = r.Str(d, "uid", path + ".uid: invalid"),
                SpeciesId = d.Get("speciesId") is JsonString sp ? sp.Value : null,
                Nickname = r.OptStr(d, "nickname", path + ".nickname: invalid"),
                Genotype = GenotypeFromJson(d.Get("genotype"), g),
                Mutation = r.Enum(d, "mutation", path + ".mutation: invalid", MutationType.Normal),
                XpSec = r.Num(d, "xpSec", path + ".xpSec: out of range"),
                Locked = r.Bool(d, "locked", path + ".locked: not boolean"),
                HatchedAtMs = r.LongOr(d, "hatchedAtMs", path + ".hatchedAtMs: invalid", -1),
                Personality = r.Enum(d, "personality", path + ".personality: invalid", Personality.Calm),
                Bond = r.IntOr(d, "bond", path + ".bond: out of range", -1),
                BondDay = r.OptStr(d, "bondDay", path + ".bondDay: invalid"),
                BondDaily = r.OptInt(d, "bondDaily", path + ".bondDaily: out of range"),
                LastFeedXpAtMs = r.OptLong(d, "lastFeedXpAtMs", path + ".lastFeedXpAtMs: invalid"),
                LastPetAtMs = r.OptLong(d, "lastPetAtMs", path + ".lastPetAtMs: invalid"),
                Generation = r.IntOr(d, "generation", path + ".generation: invalid", 0),
                Ancestry = r.Enum(d, "ancestry", path + ".ancestry: invalid", Ancestry.Unknown),
                Origin = r.Enum(d, "origin", path + ".origin: invalid", DragonOrigin.Pack),
                Activity = r.Enum(d, "activity", path + ".activity: invalid", DragonActivity.Idle),
                BreedCooldownUntilMs = d.ContainsKey("breedCooldownUntilMs") ? r.LongOr(d, "breedCooldownUntilMs", path + ".breedCooldownUntilMs: invalid", -1) : 0,
                RegistryRef = d.Get("registryRef") is JsonString rf ? rf.Value : null,
            };
            if (g.Errors.Count > 0) errors.Add(path + ".genotype: invalid");
            JsonValue parents = d.Get("parentIds");
            if (parents is JsonArray pa && pa.Count == 2 && pa[0] is JsonString p0 && pa[1] is JsonString p1) dragon.ParentIds = new[] { p0.Value, p1.Value };
            else if (!(parents is JsonNull)) errors.Add(path + ".parentIds: invalid");
            if (d.Get("grandparentIds") is JsonArray gp) dragon.GrandparentIds = r.AnyList(d, "grandparentIds");
            dragon.Location = LocationFromJson(d.Get("location"), path, errors);
            dragon.Vitals = VitalsFromJson(d.Get("vitals"), path, errors);
            return dragon;
        }

        private static DragonLocation LocationFromJson(JsonValue v, string path, List<string> errors)
        {
            if (!(v is JsonObject o) || !(o.Get("kind") is JsonString k) || !WireNames.TryParse(k.Value, out LocationKind kind))
            {
                errors.Add(path + ".location: invalid");
                return null;
            }
            string zone = o.Get("zoneId") is JsonString z ? z.Value : null;
            switch (kind)
            {
                case LocationKind.Zone:
                    string habitat = o.Get("habitatId") is JsonString h ? h.Value : null;
                    if (zone == null) errors.Add(path + ".location.zoneId: unknown");
                    if (habitat == null) errors.Add(path + ".location.habitatId: unknown");
                    return DragonLocation.Zone(zone, habitat);
                case LocationKind.Shelter:
                    if (zone == null) errors.Add(path + ".location.zoneId: unknown");
                    return DragonLocation.Shelter(zone);
                case LocationKind.Care:
                    if (!(o.Get("caseId") is JsonString cs) || cs.Value.Length == 0) { errors.Add(path + ".location.caseId: invalid"); return DragonLocation.Care("?"); }
                    return DragonLocation.Care(cs.Value);
                case LocationKind.Released: return DragonLocation.Released;
                default: return DragonLocation.Storage;
            }
        }

        /// <summary>Đọc trường có kiểu, ghi thông điệp lỗi kiểu TS khi sai kiểu.</summary>
        public sealed class Reader
        {
            public Reader(List<string> errors) { Errors = errors; }
            public List<string> Errors { get; }

            /// <summary>Number.isSafeInteger.</summary>
            public static bool AsSafeLong(JsonValue v, out long value)
            {
                value = 0;
                if (!(v is JsonNumber n)) return false;
                double d = n.Value;
                if (double.IsNaN(d) || double.IsInfinity(d) || Math.Floor(d) != d || Math.Abs(d) > JsCompat.MaxSafeInteger) return false;
                value = (long)d;
                return true;
            }

            private void Fail(string msg) { if (!Errors.Contains(msg)) Errors.Add(msg); }

            public long LongOr(JsonObject o, string key, string msg, long fallback)
            {
                if (AsSafeLong(o.Get(key), out long v)) return v;
                Fail(msg);
                return fallback;
            }

            public int IntOr(JsonObject o, string key, string msg, int fallback)
            {
                if (AsSafeLong(o.Get(key), out long v) && v >= int.MinValue && v <= int.MaxValue) return (int)v;
                Fail(msg);
                return fallback;
            }

            public double Num(JsonObject o, string key, string msg)
            {
                if (o.Get(key) is JsonNumber n) return n.Value;
                Fail(msg);
                return double.NaN;
            }

            public bool Bool(JsonObject o, string key, string msg)
            {
                if (o.Get(key) is JsonBool b) return b.Value;
                Fail(msg);
                return false;
            }

            public string Str(JsonObject o, string key, string msg)
            {
                if (o.Get(key) is JsonString s) return s.Value;
                Fail(msg);
                return null;
            }

            public string OptStr(JsonObject o, string key, string msg)
            {
                JsonValue v = o.Get(key);
                if (v == null) return null;
                if (v is JsonString s) return s.Value;
                Fail(msg);
                return null;
            }

            public int? OptInt(JsonObject o, string key, string msg)
            {
                JsonValue v = o.Get(key);
                if (v == null) return null;
                if (AsSafeLong(v, out long n) && n >= int.MinValue && n <= int.MaxValue) return (int)n;
                Fail(msg);
                return -1;
            }

            public long? OptLong(JsonObject o, string key, string msg)
            {
                JsonValue v = o.Get(key);
                if (v == null) return null;
                if (AsSafeLong(v, out long n)) return n;
                Fail(msg);
                return -1;
            }

            public T Enum<T>(JsonObject o, string key, string msg, T fallback) where T : struct, System.Enum
            {
                if (o.Get(key) is JsonString s && WireNames.TryParse(s.Value, out T v)) return v;
                Fail(msg);
                return fallback;
            }

            /// <summary>Danh sách chuỗi; null (và lỗi) nếu không phải mảng chuỗi.</summary>
            public List<string> StrList(JsonObject o, string key, string msg)
            {
                if (!(o.Get(key) is JsonArray a)) { Fail(msg); return null; }
                var list = new List<string>(a.Count);
                foreach (JsonValue x in a)
                {
                    if (x is JsonString s) list.Add(s.Value);
                    else { Fail(msg); return null; }
                }
                return list;
            }

            /// <summary>Mảng bất kỳ (TS chỉ kiểm Array.isArray): phần tử không phải chuỗi được giữ dạng JSON.</summary>
            public List<string> AnyList(JsonObject o, string key)
            {
                var list = new List<string>();
                foreach (JsonValue x in (JsonArray)o.Get(key)) list.Add(x is JsonString s ? s.Value : JsonWriter.Stringify(x));
                return list;
            }
        }

        internal static string Num(long v) => v.ToString(CultureInfo.InvariantCulture);
    }
}
