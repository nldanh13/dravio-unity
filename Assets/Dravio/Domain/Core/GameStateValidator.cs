using System;
using System.Collections.Generic;
using Dravio.Domain.Biology;
using Dravio.Domain.Catalog;
using Dravio.Domain.Economy;

namespace Dravio.Domain.Core
{
    /// <summary>
    /// Kiểm tra runtime GameState (GDD Phụ lục C.2; src/engine/core/validation.ts): enum, số hữu hạn, giới hạn,
    /// UID duy nhất, tham chiếu, sức chứa, sở hữu vị trí, tiến trình v4. Thông điệp lỗi giống hệt bản TypeScript
    /// ("path: lý do") để log/so sánh hai bản. Một số kiểm tra của TS (kiểu JSON sai, số lẻ) không thể xảy ra với
    /// model C# có kiểu; phần tương ứng được giữ ở dạng kiểm null/enum/phạm vi.
    /// </summary>
    public static class GameStateValidator
    {
        private static bool IsInt(long v) => JsCompat.IsSafeInteger(v);
        private static bool IsFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

        public static ValidationResult Validate(GameState s)
        {
            var errors = new List<string>();
            if (s == null) return new ValidationResult(new[] { "state: not an object" });
            if (s.SchemaVersion < 1) errors.Add("schemaVersion: invalid");
            if (s.ContentVersion == null) errors.Add("contentVersion: invalid");
            if (string.IsNullOrEmpty(s.SaveId)) errors.Add("saveId: invalid");
            if (!IsInt(s.Revision) || s.Revision < 0) errors.Add("revision: invalid");

            // PlayerState là cách gom của C#; bản TS lưu các trường này phẳng nên không có lỗi "player" tương ứng.
            PlayerState p = s.Player;
            if (p == null) { errors.Add("player: not an object"); return new ValidationResult(errors); }
            if (!p.Gold.IsValidBalance) errors.Add("goldMicro: must be a non-negative integer");
            if (p.GoldRemainder < 0 || p.GoldRemainder >= Lifecycle.RemainderScale) errors.Add("goldRemainder: out of range");
            if (!p.TotalGoldEarned.IsValidBalance) errors.Add("totalGoldEarnedMicro: invalid");
            if (p.PityCount < 0 || p.PityCount >= EconomyTables.PrismaticPity) errors.Add("pityCount: out of range");

            var uids = new HashSet<string>(StringComparer.Ordinal);
            if (s.Dragons == null) errors.Add("dragons: not an array");
            else
            {
                for (int i = 0; i < s.Dragons.Count; i++)
                {
                    DragonState d = s.Dragons[i];
                    ValidateDragon(d, "dragons[" + i + "]", errors);
                    if (d != null && d.Uid != null)
                    {
                        if (!uids.Add(d.Uid)) errors.Add("dragons[" + i + "].uid: duplicate " + d.Uid);
                    }
                }
            }

            ValidateZones(s, errors);
            ValidateUpgrades(p.Upgrades, errors);
            ValidateProduction(s, p.Upgrades, errors);

            if (p.Inventory == null) errors.Add("inventory: invalid");
            else foreach (var kv in p.Inventory) if (kv.Value < 0 || !IsInt(kv.Value)) { errors.Add("inventory: invalid"); break; }

            ValidateFood(s.Food, errors);

            if (s.ProcessedRequestIds == null || s.ProcessedRequestIds.Contains(null)) errors.Add("processedRequestIds: invalid");

            GameStats st = p.Stats;
            if (st == null) errors.Add("stats: invalid");
            else
            {
                CheckCounter(st.TotalHatched, "totalHatched", errors);
                CheckCounter(st.TotalSold, "totalSold", errors);
                CheckCounter(st.TotalBred, "totalBred", errors);
                CheckCounter(st.TotalFeedClicks, "totalFeedClicks", errors);
                CheckCounter(st.DailyFreePacksClaimed, "dailyFreePacksClaimed", errors);
                if (!JsCompat.IsUtcDayPattern(st.LastDailyResetDate)) errors.Add("stats.lastDailyResetDate: invalid");
            }

            GameSettings set = p.Settings;
            if (set == null) errors.Add("settings: invalid");
            else
            {
                if (!WireNames.IsDefined(set.Language)) errors.Add("settings.language: invalid");
                if (!IsFinite(set.SfxVolume) || set.SfxVolume < 0 || set.SfxVolume > 1) errors.Add("settings.sfxVolume: out of range");
                if (!IsFinite(set.BgmVolume) || set.BgmVolume < 0 || set.BgmVolume > 1) errors.Add("settings.bgmVolume: out of range");
                if (set.FpsLimit != 30 && set.FpsLimit != 60) errors.Add("settings.fpsLimit: invalid");
                if (!WireNames.IsDefined(set.TimeMode)) errors.Add("settings.timeMode: invalid");
                if (!WireNames.IsDefined(set.Mode)) errors.Add("settings.mode: invalid");
                if (!WireNames.IsDefined(set.Difficulty)) errors.Add("settings.difficulty: invalid");
            }

            ClockState clock = s.Clock;
            if (clock == null || !IsInt(clock.LastProcessedAtMs) || !IsInt(clock.HighWaterAtMs) || clock.LastProcessedAtMs > clock.HighWaterAtMs)
                errors.Add("clock: invalid");

            if (!UniqueStrings(s.Discoveries, ProgressCatalog.IsDiscoveryId)) errors.Add("discoveries: invalid");

            ValidateProgress(s, errors);

            OfflineReceipt r = s.OfflineReceipt;
            if (r != null && !(r.Id != null && IsInt(r.FromMs) && IsInt(r.ToMs) && r.ToMs >= r.FromMs && IsInt(r.GoldMicro) && r.GoldMicro >= 0 &&
                               r.MaturedIds != null && IsInt(r.CreatedAtMs)))
                errors.Add("offlineReceipt: invalid");

            return new ValidationResult(errors);
        }

        /// <summary>validateDragonInstance: kiểm nhanh một rồng.</summary>
        public static IReadOnlyList<string> ValidateDragonInstance(DragonState d)
        {
            var errors = new List<string>();
            ValidateDragon(d, "dragon", errors);
            return errors;
        }

        private static void CheckCounter(long v, string name, List<string> errors)
        {
            if (!IsInt(v) || v < 0) errors.Add("stats." + name + ": invalid");
        }

        private static void ValidateDragon(DragonState d, string path, List<string> errors)
        {
            if (d == null) { errors.Add(path + ": not an object"); return; }
            if (d.Uid == null || d.Uid.Length == 0 || d.Uid.Length > 64) errors.Add(path + ".uid: invalid");
            SpeciesCatalog.TryGet(d.SpeciesId, out DragonSpecies species);
            if (species == null) errors.Add(path + ".speciesId: unknown");
            if (species != null && !Genetics.IsValidGenotype(d.Genotype, species.Loci)) errors.Add(path + ".genotype: invalid alleles or loci");
            if (species == null && !Genetics.IsValidGenotype(d.Genotype, LociCatalog.LocusIds)) errors.Add(path + ".genotype: invalid");
            if (!WireNames.IsDefined(d.Mutation)) errors.Add(path + ".mutation: invalid");
            if (!IsFinite(d.XpSec) || d.XpSec < 0 || (species != null && d.XpSec > species.GrowthTimeSec)) errors.Add(path + ".xpSec: out of range");
            if (!IsInt(d.HatchedAtMs) || d.HatchedAtMs < 0) errors.Add(path + ".hatchedAtMs: invalid");
            if (!WireNames.IsDefined(d.Personality)) errors.Add(path + ".personality: invalid");
            if (d.Bond < 0 || d.Bond > CareRules.BondMax) errors.Add(path + ".bond: out of range");
            if (d.BondDay != null && !JsCompat.IsUtcDayPattern(d.BondDay)) errors.Add(path + ".bondDay: invalid");
            if (d.BondDaily.HasValue && (d.BondDaily.Value < 0 || d.BondDaily.Value > CareRules.BondDailyCap)) errors.Add(path + ".bondDaily: out of range");
            if (d.LastFeedXpAtMs.HasValue && !(IsInt(d.LastFeedXpAtMs.Value) && d.LastFeedXpAtMs.Value >= 0)) errors.Add(path + ".lastFeedXpAtMs: invalid");
            if (d.LastPetAtMs.HasValue && !(IsInt(d.LastPetAtMs.Value) && d.LastPetAtMs.Value >= 0)) errors.Add(path + ".lastPetAtMs: invalid");
            if (!(IsInt(d.BreedCooldownUntilMs) && d.BreedCooldownUntilMs >= 0)) errors.Add(path + ".breedCooldownUntilMs: invalid");
            if (d.ParentIds != null && !(d.ParentIds.Length == 2 && d.ParentIds[0] != null && d.ParentIds[1] != null)) errors.Add(path + ".parentIds: invalid");
            if (d.Generation < 1) errors.Add(path + ".generation: invalid");
            if (!WireNames.IsDefined(d.Ancestry)) errors.Add(path + ".ancestry: invalid");
            if (!WireNames.IsDefined(d.Origin)) errors.Add(path + ".origin: invalid");
            if (!WireNames.IsDefined(d.Activity)) errors.Add(path + ".activity: invalid");
            DragonLocation loc = d.Location;
            if (loc == null || !WireNames.IsDefined(loc.Kind))
            {
                errors.Add(path + ".location: invalid");
            }
            else if (loc.Kind == LocationKind.Zone)
            {
                if (!WorldCatalog.IsZoneId(loc.ZoneId)) errors.Add(path + ".location.zoneId: unknown");
                if (!HabitatCatalog.TryGet(loc.HabitatId, out HabitatProfile h)) errors.Add(path + ".location.habitatId: unknown");
                else if (h.ZoneId != loc.ZoneId) errors.Add(path + ".location: habitat not in zone");
            }
            else if (loc.Kind == LocationKind.Shelter && !WorldCatalog.IsZoneId(loc.ZoneId))
            {
                errors.Add(path + ".location.zoneId: unknown");
            }
            else if (loc.Kind == LocationKind.Care && string.IsNullOrEmpty(loc.CaseId))
            {
                errors.Add(path + ".location.caseId: invalid");
            }
        }

        private static void ValidateZones(GameState s, List<string> errors)
        {
            if (s.Zones == null || s.Zones.Count != WorldCatalog.ZoneIds.Count) { errors.Add("zones: must list origin, ocean, sky"); return; }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            IReadOnlyList<string> discoveries = (IReadOnlyList<string>)s.Discoveries ?? Array.Empty<string>();
            for (int i = 0; i < s.Zones.Count; i++)
            {
                ZoneState z = s.Zones[i];
                string zp = "zones[" + i + "]";
                if (z == null || !WorldCatalog.IsZoneId(z.Id)) { errors.Add(zp + ": invalid"); continue; }
                if (!seen.Add(z.Id)) errors.Add(zp + ".id: duplicate");
                if (!WireNames.IsDefined(z.Realm)) errors.Add(zp + ".realm: invalid");
                // P0: mỗi zone đúng 5 chỗ vật lý; nâng sức chứa theo zone chưa có trong P0 (ADR 0010).
                if (z.Capacity != WorldCatalog.P0ZoneCapacity) errors.Add(zp + ".capacity: must be " + WorldCatalog.P0ZoneCapacity + " in P0");
                if (z.Recovery < 0 || z.Recovery > 100) errors.Add(zp + ".recovery: out of range");
                bool habitatsOk = z.UnlockedHabitatIds != null;
                if (habitatsOk)
                    foreach (string h in z.UnlockedHabitatIds)
                        if (!HabitatCatalog.TryGet(h, out HabitatProfile hp) || hp.ZoneId != z.Id) { habitatsOk = false; break; }
                if (!habitatsOk) errors.Add(zp + ".unlockedHabitatIds: invalid");
                else
                {
                    List<string> habitats = z.UnlockedHabitatIds;
                    if (new HashSet<string>(habitats, StringComparer.Ordinal).Count != habitats.Count) errors.Add(zp + ".unlockedHabitatIds: duplicates");
                    if (!z.Unlocked && habitats.Count > 0) errors.Add(zp + ": locked zone has unlocked habitats");
                    if (z.Id == "origin" && z.Unlocked && !habitats.Contains("origin_meadow")) errors.Add(zp + ": origin_meadow must be unlocked");
                    foreach (string h in habitats)
                    {
                        HabitatUnlockRule rule = ProgressCatalog.UnlockRuleFor(h);
                        if (rule != null && rule.Requires != null && !Contains(discoveries, rule.Requires))
                            errors.Add(zp + ".unlockedHabitatIds: " + h + " requires " + rule.Requires);
                    }
                }
                if (z.DecorationIds == null || z.DecorationIds.Contains(null)) errors.Add(zp + ".decorationIds: invalid");
                if (z.ActiveDecorationIds == null || z.ActiveDecorationIds.Contains(null)) errors.Add(zp + ".activeDecorationIds: invalid");
            }

            if (s.Dragons == null) return;
            // Sức chứa vật lý: số rồng trong zone không vượt capacity.
            foreach (ZoneState z in s.Zones)
            {
                if (z == null) continue;
                int count = 0;
                foreach (DragonState d in s.Dragons)
                    if (d?.Location != null && d.Location.Kind == LocationKind.Zone && d.Location.ZoneId == z.Id) count++;
                if (count > z.Capacity) errors.Add("zones." + z.Id + ": " + count + " dragons exceed capacity " + z.Capacity);
            }
            // Rồng đặt trong zone phải ở zone + microhabitat đã mở và hợp sinh học (suitability ≥ 50).
            for (int i = 0; i < s.Dragons.Count; i++)
            {
                DragonState d = s.Dragons[i];
                if (d?.Location == null || d.Location.Kind != LocationKind.Zone || !SpeciesCatalog.TryGet(d.SpeciesId, out _)) continue;
                DragonLocation loc = d.Location;
                ZoneState zone = null;
                foreach (ZoneState z in s.Zones) if (z != null && z.Id == loc.ZoneId) { zone = z; break; }
                if (zone == null || !HabitatCatalog.Contains(loc.HabitatId)) continue;
                string dp = "dragons[" + i + "].location: ";
                if (!zone.Unlocked) errors.Add(dp + "zone " + loc.ZoneId + " is locked");
                else if (zone.UnlockedHabitatIds == null || !zone.UnlockedHabitatIds.Contains(loc.HabitatId)) errors.Add(dp + "habitat " + loc.HabitatId + " is locked");
                else if (!Suitability.ForSpecies(d.SpeciesId, loc.HabitatId).Allowed) errors.Add(dp + "habitat " + loc.HabitatId + " unsuitable for " + d.SpeciesId);
            }
        }

        private static void ValidateUpgrades(UpgradesState u, List<string> errors)
        {
            if (u == null) { errors.Add("upgrades: invalid"); return; }
            foreach (UpgradeDef def in EconomyTables.Upgrades)
            {
                int lvl = def.LevelOf(u);
                if (lvl < 0 || lvl > def.Max) errors.Add("upgrades." + def.Id + ": out of range");
            }
        }

        private static void ValidateProduction(GameState s, UpgradesState u, List<string> errors)
        {
            List<string> ids = s.ProductionDragonIds;
            if (ids == null || ids.Contains(null)) { errors.Add("productionDragonIds: invalid"); return; }
            if (new HashSet<string>(ids, StringComparer.Ordinal).Count != ids.Count) errors.Add("productionDragonIds: duplicates");
            var byUid = new Dictionary<string, DragonState>(StringComparer.Ordinal);
            if (s.Dragons != null) foreach (DragonState d in s.Dragons) if (d?.Uid != null) byUid[d.Uid] = d;
            foreach (string id in ids)
            {
                if (!byUid.TryGetValue(id, out DragonState d)) errors.Add("productionDragonIds: unknown dragon " + id);
                // Chỉ rồng đang đặt trong zone mới được chọn sản xuất (setProductionSlot: not-placed).
                else if (d.Location == null || d.Location.Kind != LocationKind.Zone) errors.Add("productionDragonIds: dragon " + id + " is not placed in a zone");
            }
            int level = u != null ? u.IslandCapacityLevel : 0;
            if (ids.Count > EconomyRates.ProductionBudget(level)) errors.Add("productionDragonIds: exceeds production budget");
        }

        private static void ValidateFood(FoodState food, List<string> errors)
        {
            if (food == null || (food.LastSpawnAtMs.HasValue && !IsInt(food.LastSpawnAtMs.Value)) || food.Drops == null) { errors.Add("food: invalid"); return; }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < food.Drops.Count; i++)
            {
                FoodDrop f = food.Drops[i];
                string fp = "food.drops[" + i + "]";
                if (f == null || f.Id == null || !WireNames.IsDefined(f.DietId) || !WorldCatalog.IsZoneId(f.ZoneId) || !HabitatCatalog.TryGet(f.HabitatId, out HabitatProfile h) ||
                    !IsInt(f.SpawnedAtMs) || !IsInt(f.ExpiresAtMs) || f.ExpiresAtMs <= f.SpawnedAtMs)
                {
                    errors.Add(fp + ": invalid");
                    continue;
                }
                if (h.ZoneId != f.ZoneId) errors.Add(fp + ": habitat not in zone");
                if (!ids.Add(f.Id)) errors.Add(fp + ".id: duplicate");
            }
        }

        /// <summary>Schema v4: hướng dẫn, trứng chờ nhận, thành tựu (ADR 0013).</summary>
        private static void ValidateProgress(GameState s, List<string> errors)
        {
            TutorialState t = s.Tutorial;
            if (t == null) errors.Add("tutorial: not an object");
            else
            {
                if (!WireNames.IsDefined(t.Status)) errors.Add("tutorial.status: invalid");
                // Bước hoàn thành theo thứ tự nên completed luôn là tiền tố của TUT_01…TUT_08.
                IReadOnlyList<string> steps = ProgressCatalog.TutorialStepIds;
                List<string> c = t.Completed;
                bool prefix = c != null && c.Count <= steps.Count;
                if (prefix) for (int i = 0; i < c.Count; i++) if (c[i] != steps[i]) { prefix = false; break; }
                if (!prefix) errors.Add("tutorial.completed: not an ordered prefix of TUT_01…TUT_08");
                else if (t.Status == TutorialStatus.Completed && c.Count != steps.Count) errors.Add("tutorial.status: completed with steps missing");
                if (!IsInt(t.StartedAtMs) || t.StartedAtMs < 0) errors.Add("tutorial.startedAtMs: invalid");
                if (!UniqueStrings(t.ProfilesViewed, id => SpeciesCatalog.TryGet(id, out _))) errors.Add("tutorial.profilesViewed: invalid");
            }

            PendingPack p = s.PendingPack;
            if (p != null)
            {
                SpeciesCatalog.TryGet(p.SpeciesId, out DragonSpecies species);
                if (p.Id == null || p.Id.Length == 0 || p.Id.Length > 64 || p.PackId == null || species == null ||
                    !Genetics.IsValidGenotype(p.Genotype, species.Loci) || !WireNames.IsDefined(p.Mutation) || !IsInt(p.CreatedAtMs) || p.CreatedAtMs < 0)
                    errors.Add("pendingPack: invalid");
                else if (p.Tutorial && !(t != null && t.StarterPackUsed))
                    errors.Add("pendingPack: tutorial pack without starterPackUsed");
            }

            // U2.5 (schema v5): tổ ấp. Bố mẹ của trứng đang ấp phải tồn tại, chưa thả và ở activity = breeding;
            // ngoài tổ thì không rồng nào được ở activity = breeding (chống rồng bị khóa vĩnh viễn hoặc nhân bản trứng).
            BreedingEgg egg = s.BreedingNest;
            var breedingParents = new HashSet<string>(StringComparer.Ordinal);
            if (egg != null)
            {
                SpeciesCatalog.TryGet(egg.SpeciesId, out DragonSpecies eggSpecies);
                if (egg.Id == null || egg.Id.Length == 0 || egg.Id.Length > 64 || eggSpecies == null || !Genetics.IsValidGenotype(egg.Genotype, eggSpecies.Loci) ||
                    !WireNames.IsDefined(egg.Mutation) || !WireNames.IsDefined(egg.Status) || egg.ParentIds == null || egg.ParentIds.Length != 2 ||
                    egg.ParentIds[0] == null || egg.ParentIds[1] == null || egg.ParentIds[0] == egg.ParentIds[1] ||
                    egg.CreatedAtMs < 0 || egg.DurationMs <= 0 || egg.TapReducedMs < 0 || egg.LastTapAtMs < 0 ||
                    egg.TapReducedMs > (long)Math.Floor(egg.DurationMs * BreedingCatalog.TapMaxShare) ||
                    egg.FinishAtMs != egg.CreatedAtMs + egg.DurationMs - egg.TapReducedMs)
                    errors.Add("breedingNest: invalid");
                else if (egg.Status == EggStatus.Incubating)
                    foreach (string pid in egg.ParentIds)
                    {
                        DragonState pd = s.FindDragon(pid);
                        if (pd == null || pd.Location.Kind == LocationKind.Released || pd.Activity != DragonActivity.Breeding)
                            errors.Add("breedingNest: parent " + pid + " not breeding");
                        else breedingParents.Add(pid);
                    }
            }
            // Chỉ áp dụng từ schema v5: state v4 của TS (không có tổ) có thể mang activity = breeding; migration v4 → v5 trả các rồng đó về idle.
            if (s.Dragons != null && s.SchemaVersion >= 5)
                foreach (DragonState d in s.Dragons)
                    if (d != null && d.Activity == DragonActivity.Breeding && !breedingParents.Contains(d.Uid))
                        errors.Add("dragons." + d.Uid + ".activity: breeding without nest");

            // U2.7 (schema v6): Bách khoa và trang trí. State v4/v5 cũ (golden TS) không có codex và có thể mang ID trang trí tùy ý.
            if (s.SchemaVersion >= 6)
            {
                if (s.Codex == null) errors.Add("codex: invalid");
                else
                {
                    int last = -1;
                    var speciesIds = new List<string>();
                    foreach (DragonSpecies sp in SpeciesCatalog.All) speciesIds.Add(sp.Id);
                    foreach (CodexEntry e in s.Codex)
                    {
                        int idx = e == null ? -1 : speciesIds.IndexOf(e.SpeciesId);
                        if (idx <= last) { errors.Add("codex: unknown, duplicate or unordered species"); break; }
                        last = idx;
                        if (e.DiscoveredMutations == null || e.DiscoveredMutations.Count == 0 || !StrictlyIncreasing(e.DiscoveredMutations) ||
                            e.ObservedStages == null || e.ObservedStages.Count == 0 || !StrictlyIncreasing(e.ObservedStages) || e.TotalHatched < 0)
                            errors.Add("codex." + e.SpeciesId + ": invalid");
                    }
                }
                if (s.Zones != null)
                    foreach (ZoneState z in s.Zones)
                    {
                        if (z?.DecorationIds == null || z.ActiveDecorationIds == null) continue;
                        // ID ngoài catalog được giữ (save cũ của TS cho phép chuỗi bất kỳ); chỉ cấm trùng và bật món chưa sở hữu.
                        if (new HashSet<string>(z.DecorationIds).Count != z.DecorationIds.Count) errors.Add("zones." + z.Id + ".decorationIds: duplicates");
                        if (new HashSet<string>(z.ActiveDecorationIds).Count != z.ActiveDecorationIds.Count || !z.ActiveDecorationIds.TrueForAll(z.DecorationIds.Contains))
                            errors.Add("zones." + z.Id + ".activeDecorationIds: not owned");
                    }
            }

            // U2.8 (schema v7): thám hiểm. Rồng đang đi phải tồn tại, chưa thả, activity = expedition; ngoài job không rồng nào ở trạng thái đó.
            if (s.SchemaVersion >= 7)
            {
                ExpeditionJob j = s.Expedition;
                string traveller = null;
                if (j != null)
                {
                    bool knownRoute = ExpeditionCatalog.TryGet(j.RouteId, out ExpeditionRoute route);
                    bool rewardsOk = j.Rewards != null && j.Rewards.TrueForAll(kv => IndexOf(ExpeditionCatalog.MaterialIds, kv.Key) >= 0 && kv.Value > 0);
                    int total = 0; if (j.Rewards != null) foreach (var kv in j.Rewards) total += kv.Value;
                    if (j.Id == null || j.Id.Length == 0 || j.Id.Length > 64 || !knownRoute || !WireNames.IsDefined(j.Status) || j.StartedAtMs < 0 || !rewardsOk ||
                        j.FinishAtMs != j.StartedAtMs + route.DurationMs || total < route.MinQty || total > route.MaxQty)
                        errors.Add("expedition: invalid");
                    else if (j.Status == ExpeditionStatus.InProgress)
                    {
                        DragonState d = s.FindDragon(j.DragonUid);
                        if (d == null || d.Location.Kind == LocationKind.Released || d.Activity != DragonActivity.Expedition) errors.Add("expedition: dragon " + j.DragonUid + " not travelling");
                        else traveller = d.Uid;
                    }
                }
                if (s.Dragons != null)
                    foreach (DragonState d in s.Dragons)
                        if (d != null && d.Activity == DragonActivity.Expedition && d.Uid != traveller) errors.Add("dragons." + d.Uid + ".activity: expedition without job");
            }

            List<string> unlocked = s.UnlockedAchievements;
            if (!UniqueStrings(unlocked, ProgressCatalog.IsAchievementId)) errors.Add("unlockedAchievements: invalid");
            List<string> claimed = s.ClaimedAchievements;
            if (!UniqueStrings(claimed, ProgressCatalog.IsAchievementId)) errors.Add("claimedAchievements: invalid");
            else if (unlocked != null && !claimed.TrueForAll(unlocked.Contains)) errors.Add("claimedAchievements: not unlocked");
        }

        private static int IndexOf(IReadOnlyList<string> xs, string v)
        {
            for (int i = 0; i < xs.Count; i++) if (xs[i] == v) return i;
            return -1;
        }

        private static bool StrictlyIncreasing<T>(List<T> xs) where T : struct, Enum
        {
            for (int i = 0; i < xs.Count; i++)
            {
                if (!WireNames.IsDefined(xs[i])) return false;
                if (i > 0 && Convert.ToInt32(xs[i - 1]) >= Convert.ToInt32(xs[i])) return false;
            }
            return true;
        }

        private static bool UniqueStrings(List<string> v, Func<string, bool> ok)
        {
            if (v == null) return false;
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (string x in v)
            {
                if (x == null || !ok(x)) return false;
                if (!set.Add(x)) return false;
            }
            return true;
        }

        private static bool Contains(IReadOnlyList<string> list, string v)
        {
            foreach (string x in list) if (x == v) return true;
            return false;
        }
    }
}
