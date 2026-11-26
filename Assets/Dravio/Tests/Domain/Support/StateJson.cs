using System;
using System.Collections.Generic;
using Dravio.Domain.Core;
using Dravio.Domain.World;

namespace Dravio.Domain.Tests.Support
{
    /// <summary>
    /// Ánh xạ test-only GameState ↔ cây JSON theo đúng hình dạng Dravio Save Schema v4 của TypeScript
    /// (trường phẳng goldMicro…settings). Chỉ để đối chiếu golden; persistence thật thuộc U2.2.
    /// </summary>
    public static class StateJson
    {
        private static Dictionary<string, object> Obj() => new Dictionary<string, object>(StringComparer.Ordinal);
        private static List<object> Arr(IEnumerable<string> xs) { var l = new List<object>(); foreach (var x in xs) l.Add(x); return l; }
        private static double N(long v) => v;

        public static Dictionary<string, object> ToJson(GameState s)
        {
            var o = Obj();
            o["schemaVersion"] = (double)s.SchemaVersion;
            o["contentVersion"] = s.ContentVersion;
            o["saveId"] = s.SaveId;
            o["revision"] = N(s.Revision);
            o["goldMicro"] = N(s.Player.Gold.Micro);
            o["goldRemainder"] = N(s.Player.GoldRemainder);
            o["totalGoldEarnedMicro"] = N(s.Player.TotalGoldEarned.Micro);
            var dragons = new List<object>();
            foreach (var d in s.Dragons) dragons.Add(Dragon(d));
            o["dragons"] = dragons;
            var zones = new List<object>();
            foreach (var z in s.Zones)
            {
                var zo = Obj();
                zo["id"] = z.Id; zo["realm"] = z.Realm.ToWire(); zo["unlocked"] = z.Unlocked; zo["capacity"] = (double)z.Capacity;
                zo["unlockedHabitatIds"] = Arr(z.UnlockedHabitatIds); zo["decorationIds"] = Arr(z.DecorationIds);
                zo["activeDecorationIds"] = Arr(z.ActiveDecorationIds); zo["recovery"] = (double)z.Recovery;
                zones.Add(zo);
            }
            o["zones"] = zones;
            o["productionDragonIds"] = Arr(s.ProductionDragonIds);
            var inv = Obj();
            foreach (var kv in s.Player.Inventory) inv[kv.Key] = N(kv.Value);
            o["inventory"] = inv;
            var u = s.Player.Upgrades;
            var uo = Obj();
            uo["islandCapacityLevel"] = (double)u.IslandCapacityLevel; uo["foodQualityLevel"] = (double)u.FoodQualityLevel;
            uo["foodCooldownLevel"] = (double)u.FoodCooldownLevel; uo["mutationLuckLevel"] = (double)u.MutationLuckLevel; uo["hatchSpeedLevel"] = (double)u.HatchSpeedLevel;
            o["upgrades"] = uo;
            var fo = Obj();
            fo["lastSpawnAtMs"] = s.Food.LastSpawnAtMs.HasValue ? (object)N(s.Food.LastSpawnAtMs.Value) : null;
            var drops = new List<object>();
            foreach (var f in s.Food.Drops)
            {
                var x = Obj();
                x["id"] = f.Id; x["dietId"] = f.DietId.ToWire(); x["zoneId"] = f.ZoneId; x["habitatId"] = f.HabitatId;
                x["spawnedAtMs"] = N(f.SpawnedAtMs); x["expiresAtMs"] = N(f.ExpiresAtMs);
                drops.Add(x);
            }
            fo["drops"] = drops;
            o["food"] = fo;
            o["discoveries"] = Arr(s.Discoveries);
            var t = s.Tutorial;
            var to = Obj();
            to["status"] = t.Status.ToWire(); to["completed"] = Arr(t.Completed); to["startedAtMs"] = N(t.StartedAtMs);
            to["profilesViewed"] = Arr(t.ProfilesViewed); to["starterPackUsed"] = t.StarterPackUsed;
            o["tutorial"] = to;
            o["pendingPack"] = s.PendingPack == null ? null : PendingPackJson(s.PendingPack);
            if (s.BreedingNest != null)
            {
                BreedingEgg e = s.BreedingNest;
                var eo = Obj();
                eo["id"] = e.Id; eo["parentIds"] = Arr(e.ParentIds); eo["speciesId"] = e.SpeciesId; eo["genotype"] = Genotype(e.Genotype); eo["mutation"] = e.Mutation.ToWire();
                eo["createdAtMs"] = N(e.CreatedAtMs); eo["finishAtMs"] = N(e.FinishAtMs); eo["durationMs"] = N(e.DurationMs); eo["tapReducedMs"] = N(e.TapReducedMs);
                eo["lastTapAtMs"] = N(e.LastTapAtMs); eo["status"] = e.Status.ToWire();
                o["breedingNest"] = eo; // v5; vắng mặt khi tổ trống để so được với golden v4 của TS
            }
            o["unlockedAchievements"] = Arr(s.UnlockedAchievements);
            o["claimedAchievements"] = Arr(s.ClaimedAchievements);
            o["processedRequestIds"] = Arr(s.ProcessedRequestIds);
            o["pityCount"] = (double)s.Player.PityCount;
            var st = s.Player.Stats;
            var so = Obj();
            so["totalHatched"] = N(st.TotalHatched); so["totalSold"] = N(st.TotalSold); so["totalBred"] = N(st.TotalBred);
            so["totalFeedClicks"] = N(st.TotalFeedClicks); so["dailyFreePacksClaimed"] = N(st.DailyFreePacksClaimed); so["lastDailyResetDate"] = st.LastDailyResetDate;
            o["stats"] = so;
            var se = s.Player.Settings;
            var seo = Obj();
            seo["language"] = se.Language.ToWire(); seo["sfxVolume"] = se.SfxVolume; seo["bgmVolume"] = se.BgmVolume; seo["hapticsEnabled"] = se.HapticsEnabled;
            seo["fpsLimit"] = (double)se.FpsLimit; seo["timeMode"] = se.TimeMode.ToWire(); seo["compactMode"] = se.CompactMode; seo["reducedMotion"] = se.ReducedMotion;
            seo["alwaysOnTop"] = se.AlwaysOnTop; seo["mode"] = se.Mode.ToWire(); seo["difficulty"] = se.Difficulty.ToWire();
            o["settings"] = seo;
            var co = Obj();
            co["lastProcessedAtMs"] = N(s.Clock.LastProcessedAtMs); co["highWaterAtMs"] = N(s.Clock.HighWaterAtMs);
            o["clock"] = co;
            if (s.OfflineReceipt == null) o["offlineReceipt"] = null;
            else
            {
                var r = s.OfflineReceipt;
                var ro = Obj();
                ro["id"] = r.Id; ro["fromMs"] = N(r.FromMs); ro["toMs"] = N(r.ToMs); ro["goldMicro"] = N(r.GoldMicro);
                ro["maturedIds"] = Arr(r.MaturedIds); ro["bonusClaimed"] = r.BonusClaimed; ro["createdAtMs"] = N(r.CreatedAtMs);
                o["offlineReceipt"] = ro;
            }
            return o;
        }

        /// <summary>Bỏ phần U2.1 chưa sở hữu (tutorial, achievements) và nén processedRequestIds giống golden.</summary>
        public static Dictionary<string, object> ToGoldenSubset(GameState s, bool fullIds, bool keepProfiles = false, long rewardsMicro = 0)
        {
            var o = ToJson(s);
            o["goldMicro"] = (double)(s.Player.Gold.Micro - rewardsMicro);
            var tutorial = (Dictionary<string, object>)o["tutorial"];
            o.Remove("tutorial"); o.Remove("unlockedAchievements"); o.Remove("claimedAchievements");
            // U2.3: hồ sơ sinh học đã xem là trạng thái của lệnh viewBiologyProfile, nên được so; tiến độ bước hướng dẫn thì chưa port.
            if (keepProfiles) o["tutorial"] = new Dictionary<string, object> { ["profilesViewed"] = tutorial["profilesViewed"], ["starterPackUsed"] = tutorial["starterPackUsed"] };
            if (!fullIds)
            {
                var ids = (List<object>)o["processedRequestIds"];
                o.Remove("processedRequestIds");
                o["processedRequestCount"] = (double)ids.Count;
                o["processedRequestTail"] = ids.GetRange(Math.Max(0, ids.Count - 3), Math.Min(3, ids.Count));
            }
            return o;
        }

        private static Dictionary<string, object> Genotype(Genotype g)
        {
            var o = Obj();
            foreach (var kv in g) o[kv.Key] = new List<object> { kv.Value.First, kv.Value.Second };
            return o;
        }

        private static Dictionary<string, object> PendingPackJson(PendingPack p)
        {
            var o = Obj();
            o["id"] = p.Id; o["packId"] = p.PackId; o["speciesId"] = p.SpeciesId; o["genotype"] = Genotype(p.Genotype);
            o["mutation"] = p.Mutation.ToWire(); o["createdAtMs"] = N(p.CreatedAtMs); o["tutorial"] = p.Tutorial;
            return o;
        }

        public static Dictionary<string, object> Dragon(DragonState d)
        {
            var o = Obj();
            o["uid"] = d.Uid;
            o["speciesId"] = d.SpeciesId;
            if (d.Nickname != null) o["nickname"] = d.Nickname;
            o["genotype"] = Genotype(d.Genotype);
            o["mutation"] = d.Mutation.ToWire();
            o["xpSec"] = d.XpSec;
            o["locked"] = d.Locked;
            o["location"] = LocationJson(d.Location);
            o["hatchedAtMs"] = N(d.HatchedAtMs);
            o["personality"] = d.Personality.ToWire();
            o["bond"] = (double)d.Bond;
            if (d.BondDay != null) o["bondDay"] = d.BondDay;
            if (d.BondDaily.HasValue) o["bondDaily"] = (double)d.BondDaily.Value;
            if (d.LastFeedXpAtMs.HasValue) o["lastFeedXpAtMs"] = N(d.LastFeedXpAtMs.Value);
            if (d.LastPetAtMs.HasValue) o["lastPetAtMs"] = N(d.LastPetAtMs.Value);
            o["parentIds"] = d.ParentIds == null ? null : Arr(d.ParentIds);
            if (d.GrandparentIds != null) o["grandparentIds"] = Arr(d.GrandparentIds);
            o["generation"] = (double)d.Generation;
            o["ancestry"] = d.Ancestry.ToWire();
            o["origin"] = d.Origin.ToWire();
            o["activity"] = d.Activity.ToWire();
            o["breedCooldownUntilMs"] = N(d.BreedCooldownUntilMs);
            if (d.RegistryRef != null) o["registryRef"] = d.RegistryRef;
            return o;
        }

        public static Dictionary<string, object> LocationJson(DragonLocation l)
        {
            var lo = Obj();
            lo["kind"] = l.Kind.ToWire();
            if (l.ZoneId != null) lo["zoneId"] = l.ZoneId;
            if (l.HabitatId != null) lo["habitatId"] = l.HabitatId;
            if (l.CaseId != null) lo["caseId"] = l.CaseId;
            return lo;
        }

        // ---- JSON → GameState (để nạp state bắt đầu của kịch bản golden) ----

        private static T E<T>(object v) where T : struct, Enum
        {
            if (WireNames.TryParse((string)v, out T r)) return r;
            throw new FormatException("bad " + typeof(T).Name + ": " + v);
        }
        private static long L(object v) => checked((long)(double)v);
        private static int Int(object v) => checked((int)(double)v);
        private static List<string> Strs(object v) { var l = new List<string>(); foreach (var x in (List<object>)v) l.Add((string)x); return l; }
        private static object Get(Dictionary<string, object> o, string k) => o.TryGetValue(k, out object v) ? v : null;

        public static GameState FromJson(Dictionary<string, object> o)
        {
            var s = new GameState
            {
                SchemaVersion = Int(o["schemaVersion"]),
                ContentVersion = (string)o["contentVersion"],
                SaveId = (string)o["saveId"],
                Revision = L(o["revision"]),
            };
            var st = (Dictionary<string, object>)o["stats"];
            var se = (Dictionary<string, object>)o["settings"];
            var u = (Dictionary<string, object>)o["upgrades"];
            s.Player = new PlayerState
            {
                Gold = MicroGold.FromMicro(L(o["goldMicro"])),
                GoldRemainder = L(o["goldRemainder"]),
                TotalGoldEarned = MicroGold.FromMicro(L(o["totalGoldEarnedMicro"])),
                PityCount = Int(o["pityCount"]),
                Upgrades = new UpgradesState
                {
                    IslandCapacityLevel = Int(u["islandCapacityLevel"]), FoodQualityLevel = Int(u["foodQualityLevel"]), FoodCooldownLevel = Int(u["foodCooldownLevel"]),
                    MutationLuckLevel = Int(u["mutationLuckLevel"]), HatchSpeedLevel = Int(u["hatchSpeedLevel"]),
                },
                Stats = new GameStats
                {
                    TotalHatched = L(st["totalHatched"]), TotalSold = L(st["totalSold"]), TotalBred = L(st["totalBred"]),
                    TotalFeedClicks = L(st["totalFeedClicks"]), DailyFreePacksClaimed = L(st["dailyFreePacksClaimed"]), LastDailyResetDate = (string)st["lastDailyResetDate"],
                },
                Settings = new GameSettings
                {
                    Language = E<LanguageCode>(se["language"]), SfxVolume = (double)se["sfxVolume"], BgmVolume = (double)se["bgmVolume"],
                    HapticsEnabled = (bool)se["hapticsEnabled"], FpsLimit = Int(se["fpsLimit"]), TimeMode = E<TimeMode>(se["timeMode"]),
                    CompactMode = (bool)se["compactMode"], ReducedMotion = (bool)se["reducedMotion"], AlwaysOnTop = (bool)se["alwaysOnTop"],
                    Mode = E<AppMode>(se["mode"]), Difficulty = E<Difficulty>(se["difficulty"]),
                },
            };
            foreach (var kv in (Dictionary<string, object>)o["inventory"]) s.Player.Inventory[kv.Key] = L(kv.Value);
            foreach (Dictionary<string, object> d in (List<object>)o["dragons"]) s.Dragons.Add(DragonFromJson(d));
            foreach (Dictionary<string, object> z in (List<object>)o["zones"])
                s.Zones.Add(new ZoneState
                {
                    Id = (string)z["id"], Realm = E<Realm>(z["realm"]), Unlocked = (bool)z["unlocked"], Capacity = Int(z["capacity"]),
                    UnlockedHabitatIds = Strs(z["unlockedHabitatIds"]), DecorationIds = Strs(z["decorationIds"]), ActiveDecorationIds = Strs(z["activeDecorationIds"]),
                    Recovery = Int(z["recovery"]),
                });
            s.ProductionDragonIds = Strs(o["productionDragonIds"]);
            var food = (Dictionary<string, object>)o["food"];
            s.Food = new FoodState { LastSpawnAtMs = food["lastSpawnAtMs"] == null ? (long?)null : L(food["lastSpawnAtMs"]) };
            foreach (Dictionary<string, object> f in (List<object>)food["drops"])
                s.Food.Drops.Add(new FoodDrop { Id = (string)f["id"], DietId = E<DietId>(f["dietId"]), ZoneId = (string)f["zoneId"], HabitatId = (string)f["habitatId"], SpawnedAtMs = L(f["spawnedAtMs"]), ExpiresAtMs = L(f["expiresAtMs"]) });
            s.ProcessedRequestIds = Strs(o["processedRequestIds"]);
            var c = (Dictionary<string, object>)o["clock"];
            s.Clock = new ClockState(L(c["lastProcessedAtMs"]), L(c["highWaterAtMs"]));
            if (o["offlineReceipt"] is Dictionary<string, object> r)
                s.OfflineReceipt = new OfflineReceipt { Id = (string)r["id"], FromMs = L(r["fromMs"]), ToMs = L(r["toMs"]), GoldMicro = L(r["goldMicro"]), MaturedIds = Strs(r["maturedIds"]), BonusClaimed = (bool)r["bonusClaimed"], CreatedAtMs = L(r["createdAtMs"]) };
            s.Discoveries = Strs(o["discoveries"]);
            if (Get(o, "tutorial") is Dictionary<string, object> t)
                // Golden U2.3 chỉ giữ profilesViewed/starterPackUsed; phần tiến độ hướng dẫn (chưa port) lấy mặc định của save mới.
                s.Tutorial = new TutorialState
                {
                    Status = t.ContainsKey("status") ? E<TutorialStatus>(t["status"]) : TutorialStatus.Active,
                    Completed = t.ContainsKey("completed") ? Strs(t["completed"]) : new List<string>(),
                    StartedAtMs = t.ContainsKey("startedAtMs") ? L(t["startedAtMs"]) : s.Clock.LastProcessedAtMs,
                    ProfilesViewed = Strs(t["profilesViewed"]),
                    StarterPackUsed = (bool)t["starterPackUsed"],
                };
            if (Get(o, "pendingPack") is Dictionary<string, object> p)
                s.PendingPack = new PendingPack { Id = (string)p["id"], PackId = (string)p["packId"], SpeciesId = (string)p["speciesId"], Genotype = GenotypeFromJson(p["genotype"]), Mutation = E<MutationType>(p["mutation"]), CreatedAtMs = L(p["createdAtMs"]), Tutorial = (bool)p["tutorial"] };
            if (Get(o, "unlockedAchievements") is List<object>) s.UnlockedAchievements = Strs(o["unlockedAchievements"]);
            if (Get(o, "claimedAchievements") is List<object>) s.ClaimedAchievements = Strs(o["claimedAchievements"]);
            return s;
        }

        public static Genotype GenotypeFromJson(object v)
        {
            var g = new Genotype();
            foreach (var kv in (Dictionary<string, object>)v) { var pair = (List<object>)kv.Value; g.Set(kv.Key, (string)pair[0], (string)pair[1]); }
            return g;
        }

        public static DragonState DragonFromJson(Dictionary<string, object> d)
        {
            var loc = (Dictionary<string, object>)d["location"];
            var kind = E<LocationKind>(loc["kind"]);
            DragonLocation location;
            switch (kind)
            {
                case LocationKind.Zone: location = DragonLocation.Zone((string)loc["zoneId"], (string)Get(loc, "habitatId")); break;
                case LocationKind.Shelter: location = DragonLocation.Shelter((string)loc["zoneId"]); break;
                case LocationKind.Care: location = DragonLocation.Care((string)loc["caseId"]); break;
                case LocationKind.Released: location = DragonLocation.Released; break;
                default: location = DragonLocation.Storage; break;
            }
            return new DragonState
            {
                Uid = (string)d["uid"], SpeciesId = (string)d["speciesId"], Nickname = (string)Get(d, "nickname"), Genotype = GenotypeFromJson(d["genotype"]),
                Mutation = E<MutationType>(d["mutation"]), XpSec = (double)d["xpSec"], Locked = (bool)d["locked"], Location = location,
                HatchedAtMs = L(d["hatchedAtMs"]), Personality = E<Personality>(d["personality"]), Bond = Int(d["bond"]),
                BondDay = (string)Get(d, "bondDay"), BondDaily = Get(d, "bondDaily") == null ? (int?)null : Int(d["bondDaily"]),
                LastFeedXpAtMs = Get(d, "lastFeedXpAtMs") == null ? (long?)null : L(d["lastFeedXpAtMs"]),
                LastPetAtMs = Get(d, "lastPetAtMs") == null ? (long?)null : L(d["lastPetAtMs"]),
                ParentIds = d["parentIds"] == null ? null : Strs(d["parentIds"]).ToArray(),
                GrandparentIds = Get(d, "grandparentIds") == null ? null : Strs(d["grandparentIds"]),
                Generation = Int(d["generation"]), Ancestry = E<Ancestry>(d["ancestry"]), Origin = E<DragonOrigin>(d["origin"]),
                Activity = E<DragonActivity>(d["activity"]), BreedCooldownUntilMs = L(d["breedCooldownUntilMs"]), RegistryRef = (string)Get(d, "registryRef"),
            };
        }

        // ---- Event → JSON ----

        public static Dictionary<string, object> Event(DomainEvent e)
        {
            var o = Obj();
            o["type"] = e.Type;
            switch (e)
            {
                case TimeAdvanced t: o["fromMs"] = N(t.FromMs); o["toMs"] = N(t.ToMs); o["elapsedMs"] = N(t.ElapsedMs); o["mode"] = t.Mode.ToWire(); o["goldMicro"] = N(t.GoldMicro); break;
                case StageChanged x: o["dragonUid"] = x.DragonUid; o["from"] = x.From.ToWire(); o["to"] = x.To.ToWire(); break;
                case OfflineReceiptCreated x: o["receiptId"] = x.ReceiptId; o["goldMicro"] = N(x.GoldMicro); o["elapsedMs"] = N(x.ElapsedMs); break;
                case FoodSpawned x: o["foodId"] = x.FoodId; o["dietId"] = x.DietId.ToWire(); o["zoneId"] = x.ZoneId; o["habitatId"] = x.HabitatId; o["expiresAtMs"] = N(x.ExpiresAtMs); break;
                case FoodExpired x: o["foodId"] = x.FoodId; break;
                case FoodConsumed x: o["foodId"] = x.FoodId; o["dragonUid"] = x.DragonUid; o["xpGainedSec"] = (double)x.XpGainedSec; break;
                case BondChanged x: o["dragonUid"] = x.DragonUid; o["from"] = (double)x.From; o["to"] = (double)x.To; o["source"] = x.Source.ToWire(); break;
                case DragonPetted x: o["dragonUid"] = x.DragonUid; break;
                case DragonRenamed x: o["dragonUid"] = x.DragonUid; o["nickname"] = x.Nickname; break;
                case DragonMoved x: o["dragonUid"] = x.DragonUid; o["from"] = LocationJson(x.From); o["to"] = LocationJson(x.To); break;
                case ProductionChanged x: o["dragonUid"] = x.DragonUid; o["enabled"] = x.Enabled; break;
                case HabitatUnlocked x: o["zoneId"] = x.ZoneId; o["habitatId"] = x.HabitatId; break;
                case DiscoveryRecorded x: o["discoveryId"] = x.DiscoveryId; break;
                case HabitatSurveyed x: o["habitatId"] = x.HabitatId; break;
                case BiologyProfileViewed x: o["speciesId"] = x.SpeciesId; break;
                case PackBought x: o["packId"] = x.PackId; o["speciesId"] = x.SpeciesId; o["priceGoldMicro"] = N(x.PriceGoldMicro); o["tutorial"] = x.Tutorial; break;
                case DragonHatched x: o["dragonUid"] = x.DragonUid; o["speciesId"] = x.SpeciesId; o["mutation"] = x.Mutation.ToWire(); o["location"] = LocationJson(x.Location); o["tutorial"] = x.Tutorial; break;
                case AchievementUnlocked x: o["achievementId"] = x.AchievementId; o["rewardGoldMicro"] = N(x.RewardGoldMicro); break;
                case TutorialStepCompleted x: o["stepId"] = x.StepId; break;
                case TutorialStatusChanged x: o["status"] = x.Status.ToWire(); break;
                default: throw new NotSupportedException(e.Type);
            }
            return o;
        }

        // ---- Command JSON → GameCommand (lệnh không biểu diễn được trong C# có kiểu trả null = invalid-command) ----

        private static MoveTarget Target(object v) =>
            v is Dictionary<string, object> t && t.TryGetValue("kind", out object k) && WireNames.TryParse(k as string, out LocationKind kind)
                ? MoveTarget.Raw(kind, Get(t, "zoneId") as string, Get(t, "habitatId") as string) : null;

        /// <summary>Toàn bộ state (U2.4 so cả tutorial/achievements/vàng thật), processedRequestIds nén khi fullIds = false.</summary>
        public static Dictionary<string, object> ToGoldenFull(GameState s, bool fullIds)
        {
            var o = ToJson(s);
            if (!fullIds)
            {
                var ids = (List<object>)o["processedRequestIds"];
                o.Remove("processedRequestIds");
                o["processedRequestCount"] = (double)ids.Count;
                o["processedRequestTail"] = ids.GetRange(Math.Max(0, ids.Count - 3), Math.Min(3, ids.Count));
            }
            return o;
        }

        public static GameCommand Command(Dictionary<string, object> c)
        {
            string type = Get(c, "type") as string;
            string rid = Get(c, "requestId") as string;
            switch (type)
            {
                case "advanceTime":
                    return WireNames.TryParse(Get(c, "mode") as string, out AdvanceMode m) ? new AdvanceTimeCommand(rid, m) : null;
                case "feed":
                    return WireNames.TryParse(Get(c, "dietId") as string, out DietId diet) ? new FeedCommand(rid, diet, Get(c, "zoneId") as string, Get(c, "habitatId") as string) : null;
                case "consumeFood": return new ConsumeFoodCommand(rid, Get(c, "foodId") as string, Get(c, "dragonUid") as string);
                case "pet": return new PetCommand(rid, Get(c, "dragonUid") as string);
                case "renameDragon": return new RenameDragonCommand(rid, Get(c, "dragonUid") as string, Get(c, "nickname") as string);
                case "moveDragon": return new MoveDragonCommand(rid, Get(c, "dragonUid") as string, Target(Get(c, "target")));
                case "buyTutorialPack": return new BuyTutorialPackCommand(rid);
                case "claimPack": return new ClaimPackCommand(rid, Target(Get(c, "target")));
                case "setTutorialStatus":
                    // "completed" có trong enum C# nhưng lệnh từ chối (IsWellFormed) giống parseCommand của TS.
                    return WireNames.TryParse(Get(c, "status") as string, out TutorialStatus ts) ? new SetTutorialStatusCommand(rid, ts) : null;
                case "setProductionSlot":
                    return Get(c, "enabled") is bool en ? new SetProductionSlotCommand(rid, Get(c, "dragonUid") as string, en) : null;
                case "unlockHabitat": return new UnlockHabitatCommand(rid, Get(c, "habitatId") as string);
                case "surveyHabitat": return new SurveyHabitatCommand(rid, Get(c, "habitatId") as string);
                case "viewBiologyProfile": return new ViewBiologyProfileCommand(rid, Get(c, "speciesId") as string);
                default: return null;
            }
        }
    }
}
