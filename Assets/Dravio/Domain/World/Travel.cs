using System;
using System.Collections.Generic;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;

namespace Dravio.Domain.World
{
    /// <summary>Một đường đi hợp lệ: chuỗi zone và cạnh WorldGraph đã dùng.</summary>
    public sealed class TravelRoute
    {
        internal TravelRoute(IReadOnlyList<string> zones, IReadOnlyList<string> edges) { Zones = zones; Edges = edges; }
        /// <summary>Zone xuất phát … zone đích.</summary>
        public IReadOnlyList<string> Zones { get; }
        /// <summary>ID cạnh WorldGraph theo thứ tự đi.</summary>
        public IReadOnlyList<string> Edges { get; }
    }

    /// <summary>
    /// Di chuyển giữa các thế giới (U2.8). Nối WorldGraph.CanTravel (port TS, golden U2.3) vào moveDragon như ADR 0010 mục 4 dự kiến
    /// "nối khi mở realm". Luật bổ sung (implementation mới, GDD tr. 5 "edge = đường đi với điều kiện di chuyển … hoặc công trình"):
    /// một cạnh chỉ dùng được khi cả hai zone đã mở VÀ cả hai microhabitat cửa ngõ đã mở; rồng cần ít nhất một kiểu di chuyển cạnh yêu cầu.
    /// Kho an toàn thuộc khu bảo tồn Khởi Nguyên: rồng trong kho xuất phát từ "origin" (quyết định ghi trong README, GDD chưa nói).
    /// Vận chuyển bằng phương tiện cứu hộ (ngoại lệ GDD) chưa port.
    /// </summary>
    public static class Travel
    {
        public const string StorageHubZone = "origin";

        /// <summary>Zone hiện tại của rồng cho mục đích tìm đường (kho → Khởi Nguyên). null nếu rồng không ở zone/kho.</summary>
        public static string FromZone(DragonState d) =>
            d.Location.Kind == LocationKind.Zone ? d.Location.ZoneId : d.Location.Kind == LocationKind.Storage ? StorageHubZone : null;

        private static bool EdgeOpen(GameState s, WorldEdge e)
        {
            foreach (var kv in e.Gateways)
            {
                ZoneState z = s.FindZone(kv.Key);
                if (z == null || !z.Unlocked || !z.UnlockedHabitatIds.Contains(kv.Value)) return false;
            }
            return true;
        }

        private static bool CanUse(WorldEdge e, IReadOnlyList<Locomotion> loco)
        {
            foreach (Locomotion m in e.Requires) foreach (Locomotion l in loco) if (m == l) return true;
            return false;
        }

        /// <summary>
        /// Đường ngắn nhất (BFS, thứ tự cạnh theo catalog) từ zone xuất phát tới zone đích cho loài này, chỉ qua cạnh đang mở.
        /// Cùng zone → đường một node. null = không có đường.
        /// </summary>
        public static TravelRoute FindRoute(GameState s, string fromZone, string toZone, IReadOnlyList<Locomotion> loco)
        {
            if (fromZone == null || toZone == null) return null;
            if (fromZone == toZone) return new TravelRoute(new[] { fromZone }, Array.Empty<string>());
            var prev = new Dictionary<string, (string zone, string edge)>(StringComparer.Ordinal) { [fromZone] = (null, null) };
            var queue = new Queue<string>();
            queue.Enqueue(fromZone);
            while (queue.Count > 0)
            {
                string z = queue.Dequeue();
                foreach (WorldEdge e in WorldGraph.EdgesOf(z))
                {
                    if (!CanUse(e, loco) || !EdgeOpen(s, e)) continue;
                    string next = e.A == z ? e.B : e.A;
                    if (prev.ContainsKey(next)) continue;
                    prev[next] = (z, e.Id);
                    if (next == toZone)
                    {
                        var zones = new List<string>(); var edges = new List<string>();
                        for (string cur = toZone; cur != null; cur = prev[cur].zone) { zones.Insert(0, cur); if (prev[cur].edge != null) edges.Insert(0, prev[cur].edge); }
                        return new TravelRoute(zones, edges);
                    }
                    queue.Enqueue(next);
                }
            }
            return null;
        }

        public static TravelRoute RouteFor(GameState s, DragonState d, string toZone) =>
            FindRoute(s, FromZone(d), toZone, SpeciesCatalog.GetBiology(d.SpeciesId).Locomotion);
    }

    /// <summary>
    /// Mở rộng thế giới (U2.8, implementation mới). GDD tr. 4 nói Thủy Vực và Thiên Giới mở "qua nhiệm vụ" (G1: khu nước nông và sân bay thấp,
    /// kèm trứng Droplet/Zephyr cố định; G2: mở vùng đầy đủ) nhưng CHƯA có danh mục nhiệm vụ hay điều kiện cụ thể. Vì vậy KHÔNG có lệnh công khai
    /// để người chơi tự mở zone: đây là hàm engine (như Zones.RecordDiscovery) dành cho hệ nhiệm vụ tương lai và cho test. Quyết định còn thiếu
    /// được liệt kê trong README.
    /// </summary>
    public static class WorldExpansion
    {
        /// <summary>
        /// Mở một zone đang khóa và toàn bộ microhabitat của nó (GDD "mở 5 chỗ nước và 5 chỗ không" — hiểu là mở zone thì dùng được;
        /// luật mở từng microhabitat Ocean/Sky chưa có trong TS/GDD). Không đổi sức chứa (5/zone P0), ngân sách hay vàng. Idempotent bị từ chối.
        /// </summary>
        public static DomainErrorCode? ApplyZoneUnlock(GameState s, string zoneId, List<DomainEvent> events)
        {
            ZoneState z = s.FindZone(zoneId);
            if (z == null) return DomainErrorCode.ZoneLocked;
            if (z.Unlocked) return DomainErrorCode.AlreadyThere;
            z.Unlocked = true;
            z.UnlockedHabitatIds = Zones.HabitatsOfZone(zoneId);
            events?.Add(new ZoneUnlocked(zoneId));
            foreach (string h in z.UnlockedHabitatIds) events?.Add(new HabitatUnlocked(zoneId, h));
            return null;
        }
    }
}
