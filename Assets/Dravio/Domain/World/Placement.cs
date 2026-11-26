using Dravio.Domain.Biology;
using Dravio.Domain.Catalog;

namespace Dravio.Domain.World
{
    /// <summary>
    /// Truy vấn vị trí/sản xuất (src/engine/world/placement.ts). Chỉ đọc state.
    /// Lệnh moveDragon / setProductionSlot / unlockHabitat chưa port ở U2.1.
    /// </summary>
    public static class Placement
    {
        /// <summary>Rồng đang ở một microhabitat đã mở và hợp sinh học (suitability ≥ 50).</summary>
        public static bool IsPlacedInValidHabitat(GameState state, DragonState dragon)
        {
            DragonLocation loc = dragon.Location;
            if (loc == null || loc.Kind != LocationKind.Zone || loc.HabitatId == null) return false;
            ZoneState zone = state.FindZone(loc.ZoneId);
            if (zone == null || !zone.Unlocked || zone.UnlockedHabitatIds == null || !zone.UnlockedHabitatIds.Contains(loc.HabitatId)) return false;
            if (HabitatCatalog.ZoneOf(loc.HabitatId) != loc.ZoneId) return false;
            return Suitability.ForSpecies(dragon.SpeciesId, loc.HabitatId).Allowed;
        }

        /// <summary>
        /// Rồng sinh vàng khi: nằm trong ngân sách sản xuất, ở sinh cảnh hợp lệ và không bận
        /// (care/expedition/breeding tạm dừng sản xuất — GDD trang 15).
        /// </summary>
        public static bool IsProducing(GameState state, DragonState dragon) =>
            state.ProductionDragonIds.Contains(dragon.Uid) &&
            dragon.Activity == DragonActivity.Idle &&
            IsPlacedInValidHabitat(state, dragon);
    }
}
