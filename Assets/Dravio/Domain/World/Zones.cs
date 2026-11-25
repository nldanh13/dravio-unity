using System;
using System.Collections.Generic;
using Dravio.Domain.Biology;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Economy;

namespace Dravio.Domain.World
{
    /// <summary>Đích của lệnh moveDragon: một microhabitat trong zone, hoặc kho an toàn.</summary>
    public sealed class MoveTarget
    {
        private MoveTarget(LocationKind kind, string zoneId, string habitatId) { Kind = kind; ZoneId = zoneId; HabitatId = habitatId; }
        public LocationKind Kind { get; }
        public string ZoneId { get; }
        public string HabitatId { get; }
        public static MoveTarget Zone(string zoneId, string habitatId) => new MoveTarget(LocationKind.Zone, zoneId, habitatId);
        public static readonly MoveTarget Storage = new MoveTarget(LocationKind.Storage, null, null);
        /// <summary>Dùng cho test hình dạng lệnh: mọi kind khác zone/storage là invalid-command như bản TS.</summary>
        public static MoveTarget Raw(LocationKind kind, string zoneId, string habitatId) => new MoveTarget(kind, zoneId, habitatId);
    }

    /// <summary>Kết quả kiểm một đích bố trí (dùng chung cho lệnh moveDragon và màn hình chọn chỗ).</summary>
    public sealed class PlacementCheck
    {
        internal PlacementCheck(string zoneId, string habitatId, HabitatResult result, int freeSlots, DomainErrorCode? blocked)
        {
            ZoneId = zoneId; HabitatId = habitatId; Result = result; Tier = Suitability.TierOf(result); FreeSlots = freeSlots; Blocked = blocked;
        }
        public string ZoneId { get; }
        public string HabitatId { get; }
        public HabitatResult Result { get; }
        public SuitabilityTier Tier { get; }
        public int FreeSlots { get; }
        /// <summary>null = được phép chuyển tới.</summary>
        public DomainErrorCode? Blocked { get; }
    }

    public sealed class HabitatUnlockInfo
    {
        internal HabitatUnlockInfo(string habitatId, bool unlocked, string requires, bool eligible) { HabitatId = habitatId; Unlocked = unlocked; Requires = requires; Eligible = eligible; }
        public string HabitatId { get; }
        public bool Unlocked { get; }
        public string Requires { get; }
        public bool Eligible { get; }
    }

    /// <summary>
    /// Zone và luật bố trí (GDD 3.1 trang 5; ADR 0003, 0008, 0009; src/engine/world/zones.ts).
    /// Zone là chỗ vật lý (P0: 5 chỗ/zone); microhabitat là cấu hình trong zone, không thêm chỗ.
    /// Ngân sách sản xuất toàn cục = 5 + 2 × cấp, tối đa 25; rồng ngoài ngân sách vẫn sống và lớn.
    /// Các hàm Apply* đổi state làm việc tại chỗ (dispatcher đã sao chép); hàm Check*/truy vấn chỉ đọc.
    /// </summary>
    public static class Zones
    {
        /// <summary>Microhabitat của một zone theo thứ tự HABITAT_PROFILES.</summary>
        public static List<string> HabitatsOfZone(string zoneId)
        {
            var list = new List<string>();
            foreach (HabitatProfile h in HabitatCatalog.All) if (h.ZoneId == zoneId) list.Add(h.Id);
            return list;
        }

        /// <summary>Số rồng đang chiếm chỗ vật lý trong zone (mọi microhabitat cộng lại).</summary>
        public static int ZoneOccupancy(GameState state, string zoneId)
        {
            int n = 0;
            foreach (DragonState d in state.Dragons) if (d.Location.Kind == LocationKind.Zone && d.Location.ZoneId == zoneId) n++;
            return n;
        }

        public static int CurrentProductionBudget(GameState state) => EconomyRates.ProductionBudget(state.Player.Upgrades.IslandCapacityLevel);

        private static DomainErrorCode? BusyReason(DragonState dragon)
        {
            LocationKind k = dragon.Location.Kind;
            if (k == LocationKind.Released || k == LocationKind.Care || k == LocationKind.Shelter) return DomainErrorCode.DragonUnavailable;
            if (dragon.Activity != DragonActivity.Idle) return DomainErrorCode.DragonUnavailable;
            return null;
        }

        /// <summary>
        /// Kiểm một đích cụ thể. Thứ tự (ADR 0008 mục 4): microhabitat thuộc zone → rồng rảnh → zone mở → microhabitat mở
        /// → suitability ≥ 50 → không phải chỗ hiện tại → còn chỗ (rồng cùng zone không tính chính nó).
        /// Trả null khi microhabitat không thuộc zone (TS trả { blocked: 'habitat-locked' } không kèm chi tiết).
        /// </summary>
        public static PlacementCheck CheckPlacement(GameState state, DragonState dragon, string zoneId, string habitatId, out DomainErrorCode? blocked)
        {
            if (HabitatCatalog.ZoneOf(habitatId) != zoneId) { blocked = DomainErrorCode.HabitatLocked; return null; }
            ZoneState zone = state.FindZone(zoneId);
            HabitatResult result = Suitability.ForSpecies(dragon.SpeciesId, habitatId);
            bool sameZone = dragon.Location.Kind == LocationKind.Zone && dragon.Location.ZoneId == zoneId;
            int freeSlots = zone != null ? zone.Capacity - ZoneOccupancy(state, zoneId) + (sameZone ? 1 : 0) : 0;
            DomainErrorCode? b = BusyReason(dragon);
            if (b == null && (zone == null || !zone.Unlocked)) b = DomainErrorCode.ZoneLocked;
            else if (b == null && !zone.UnlockedHabitatIds.Contains(habitatId)) b = DomainErrorCode.HabitatLocked;
            else if (b == null && !result.Allowed) b = DomainErrorCode.HabitatUnsuitable;
            else if (b == null && sameZone && dragon.Location.HabitatId == habitatId) b = DomainErrorCode.AlreadyThere;
            else if (b == null && freeSlots <= 0) b = DomainErrorCode.ZoneFull;
            blocked = b;
            return new PlacementCheck(zoneId, habitatId, result, freeSlots, b);
        }

        /// <summary>Mọi microhabitat của các zone kèm suitability và lý do chặn — cho UI và test.</summary>
        public static List<PlacementCheck> PlacementOptions(GameState state, DragonState dragon)
        {
            var list = new List<PlacementCheck>();
            foreach (string z in WorldCatalog.ZoneIds)
                foreach (string h in HabitatsOfZone(z))
                {
                    PlacementCheck c = CheckPlacement(state, dragon, z, h, out _);
                    if (c != null) list.Add(c);
                }
            return list;
        }

        /// <summary>Kiểm moveDragon (không đổi state). null = hợp lệ.</summary>
        public static DomainErrorCode? CheckMove(GameState state, DragonState dragon, MoveTarget target)
        {
            if (target.Kind == LocationKind.Storage)
            {
                DomainErrorCode? busy = BusyReason(dragon);
                if (busy != null) return busy;
                if (dragon.Location.Kind == LocationKind.Storage) return DomainErrorCode.AlreadyThere;
                return null;
            }
            CheckPlacement(state, dragon, target.ZoneId, target.HabitatId, out DomainErrorCode? blocked);
            return blocked;
        }

        /// <summary>
        /// Chuyển rồng giữa microhabitat hoặc vào kho an toàn (không sản xuất). Vào kho thì rời ngân sách sản xuất.
        /// Gọi sau khi đã quyết toán thời gian. Đổi <paramref name="state"/> tại chỗ.
        /// </summary>
        public static DomainErrorCode? ApplyMove(GameState state, DragonState dragon, MoveTarget target, List<DomainEvent> events)
        {
            DomainErrorCode? err = CheckMove(state, dragon, target);
            if (err != null) return err;
            DragonLocation from = dragon.Location;
            DragonLocation to = target.Kind == LocationKind.Storage ? DragonLocation.Storage : DragonLocation.Zone(target.ZoneId, target.HabitatId);
            dragon.Location = to;
            bool leaves = to.Kind != LocationKind.Zone && state.ProductionDragonIds.Contains(dragon.Uid);
            events.Add(new DragonMoved(dragon.Uid, from, to));
            if (leaves)
            {
                state.ProductionDragonIds.RemoveAll(id => id == dragon.Uid);
                events.Add(new ProductionChanged(dragon.Uid, false));
            }
            return null;
        }

        /// <summary>Kiểm setProductionSlot (không đổi state). Bỏ chọn luôn được; chọn chỉ khi rồng ở sinh cảnh đã mở và hợp lệ, còn ngân sách.</summary>
        public static DomainErrorCode? CheckProduction(GameState state, DragonState dragon, bool enabled)
        {
            bool inList = state.ProductionDragonIds.Contains(dragon.Uid);
            if (!enabled) return inList ? (DomainErrorCode?)null : DomainErrorCode.AlreadyThere;
            if (inList) return DomainErrorCode.AlreadyThere;
            if (dragon.Location.Kind == LocationKind.Released) return DomainErrorCode.DragonUnavailable;
            if (!Placement.IsPlacedInValidHabitat(state, dragon)) return DomainErrorCode.NotPlaced;
            if (state.ProductionDragonIds.Count >= CurrentProductionBudget(state)) return DomainErrorCode.ProductionBudgetFull;
            return null;
        }

        public static DomainErrorCode? ApplyProduction(GameState state, DragonState dragon, bool enabled, List<DomainEvent> events)
        {
            DomainErrorCode? err = CheckProduction(state, dragon, enabled);
            if (err != null) return err;
            if (enabled) state.ProductionDragonIds.Add(dragon.Uid);
            else state.ProductionDragonIds.RemoveAll(id => id == dragon.Uid);
            events.Add(new ProductionChanged(dragon.Uid, enabled));
            return null;
        }

        /// <summary>
        /// Kiểm unlockHabitat (ADR 0009): ID có luật → zone mở → chưa mở → đã có khám phá điều kiện.
        /// Miễn phí; không đổi sức chứa hay ngân sách.
        /// </summary>
        public static DomainErrorCode? CheckUnlock(GameState state, string habitatId)
        {
            HabitatUnlockRule rule = ProgressCatalog.UnlockRuleFor(habitatId);
            if (rule == null) return DomainErrorCode.UnknownHabitat;
            ZoneState zone = state.FindZone(HabitatCatalog.ZoneOf(habitatId));
            if (zone == null || !zone.Unlocked) return DomainErrorCode.ZoneLocked;
            if (zone.UnlockedHabitatIds.Contains(habitatId)) return DomainErrorCode.HabitatAlreadyUnlocked;
            if (rule.Requires != null && !state.Discoveries.Contains(rule.Requires)) return DomainErrorCode.UnlockRequirementMissing;
            return null;
        }

        /// <summary>Mở microhabitat; danh sách đã mở giữ theo thứ tự zone (thứ tự mở không ảnh hưởng).</summary>
        public static DomainErrorCode? ApplyUnlock(GameState state, string habitatId, List<DomainEvent> events)
        {
            DomainErrorCode? err = CheckUnlock(state, habitatId);
            if (err != null) return err;
            ZoneState zone = state.FindZone(HabitatCatalog.ZoneOf(habitatId));
            var unlocked = new List<string>();
            foreach (string h in HabitatsOfZone(zone.Id)) if (h == habitatId || zone.UnlockedHabitatIds.Contains(h)) unlocked.Add(h);
            zone.UnlockedHabitatIds = unlocked;
            events.Add(new HabitatUnlocked(zone.Id, habitatId));
            return null;
        }

        /// <summary>Trạng thái mở khóa từng microhabitat có luật của Khởi Nguyên, cho UI.</summary>
        public static List<HabitatUnlockInfo> HabitatUnlockStatus(GameState state)
        {
            var list = new List<HabitatUnlockInfo>();
            foreach (string h in HabitatsOfZone("origin"))
            {
                HabitatUnlockRule rule = ProgressCatalog.UnlockRuleFor(h);
                if (rule == null) continue;
                ZoneState zone = state.FindZone(HabitatCatalog.ZoneOf(h));
                bool unlocked = zone != null && zone.UnlockedHabitatIds.Contains(h);
                list.Add(new HabitatUnlockInfo(h, unlocked, rule.Requires, !unlocked && CheckUnlock(state, h) == null));
            }
            return list;
        }

        /// <summary>
        /// Ghi nhận một khám phá. KHÔNG phải lệnh người chơi: chỉ engine gọi (lệnh surveyHabitat sau khi tự kiểm điều kiện).
        /// discoveries giữ sắp xếp theo đơn vị UTF-16 như Array.prototype.sort.
        /// </summary>
        public static DomainErrorCode? RecordDiscovery(GameState state, string discoveryId, List<DomainEvent> events)
        {
            if (state.Discoveries.Contains(discoveryId)) return DomainErrorCode.AlreadyThere;
            state.Discoveries.Add(discoveryId);
            state.Discoveries.Sort(StringComparer.Ordinal);
            events.Add(new DiscoveryRecorded(discoveryId));
            return null;
        }
    }

    /// <summary>
    /// Khảo sát microhabitat chưa mở để ghi nhận khám phá (ADR 0009, 0013; src/engine/world/survey.ts).
    /// Thao tác người chơi; engine kiểm điều kiện rồi mới ghi. Cùng một đường cho người theo và người bỏ qua hướng dẫn.
    /// - Đá ấm (DISC_EMBERLING_THERMAL): đang nuôi Emberling (chưa thả) và đã xem hồ sơ sinh học của nó.
    /// - Bờ hồ (DISC_AQUATIC_HABITAT): không cần sở hữu Droplet (quyết định tác giả).
    /// </summary>
    public static class Survey
    {
        public static bool RequirementMet(GameState state, string discoveryId)
        {
            switch (discoveryId)
            {
                case "DISC_EMBERLING_THERMAL":
                    bool hasEmber = false;
                    foreach (DragonState d in state.Dragons) if (d.SpeciesId == "emberling" && d.Location.Kind != LocationKind.Released) hasEmber = true;
                    return hasEmber && state.Tutorial.ProfilesViewed.Contains("emberling");
                case "DISC_AQUATIC_HABITAT":
                    return true;
                default:
                    throw new DomainInvariantException("Unknown discovery id: " + discoveryId);
            }
        }

        public static DomainErrorCode? Check(GameState state, string habitatId)
        {
            HabitatUnlockRule rule = ProgressCatalog.UnlockRuleFor(habitatId);
            if (rule == null || rule.Requires == null) return DomainErrorCode.UnknownHabitat;
            ZoneState zone = state.FindZone(HabitatCatalog.ZoneOf(habitatId));
            if (zone == null || !zone.Unlocked) return DomainErrorCode.ZoneLocked;
            if (state.Discoveries.Contains(rule.Requires)) return DomainErrorCode.DiscoveryAlreadyKnown;
            if (!RequirementMet(state, rule.Requires)) return DomainErrorCode.SurveyRequirementMissing;
            return null;
        }

        public static DomainErrorCode? Apply(GameState state, string habitatId, List<DomainEvent> events)
        {
            DomainErrorCode? err = Check(state, habitatId);
            if (err != null) return err;
            var recorded = new List<DomainEvent>();
            if (Zones.RecordDiscovery(state, ProgressCatalog.UnlockRuleFor(habitatId).Requires, recorded) != null) return DomainErrorCode.DiscoveryAlreadyKnown;
            events.Add(new HabitatSurveyed(habitatId));
            events.AddRange(recorded);
            return null;
        }
    }

    /// <summary>Mở hồ sơ sinh học của một loài người chơi đang nuôi (TUT_03; điều kiện khảo sát nhiệt độ; src/engine/progression/tutorial.ts).</summary>
    public static class BiologyProfile
    {
        public static DomainErrorCode? Apply(GameState state, string speciesId, List<DomainEvent> events)
        {
            bool kept = false;
            foreach (DragonState d in state.Dragons) if (d.Location.Kind != LocationKind.Released && d.SpeciesId == speciesId) kept = true;
            if (!kept) return DomainErrorCode.UnknownDragon;
            List<string> viewed = state.Tutorial.ProfilesViewed;
            if (viewed.Contains(speciesId)) return null; // xem lại: thành công, không event
            viewed.Add(speciesId);
            viewed.Sort(StringComparer.Ordinal);
            events.Add(new BiologyProfileViewed(speciesId));
            return null;
        }
    }
}
