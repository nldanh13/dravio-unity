using System;
using System.Collections.Generic;

namespace Dravio.Domain.Catalog
{
    /// <summary>Cạnh của đồ thị thế giới: đường đi giữa hai zone, điều kiện di chuyển và microhabitat cửa ngõ ở hai đầu.</summary>
    public sealed class WorldEdge
    {
        internal WorldEdge(string id, string a, string b, Locomotion[] requires, IReadOnlyDictionary<string, string> gateways)
        {
            Id = id; A = a; B = b; Requires = Array.AsReadOnly(requires); Gateways = gateways;
        }

        /// <summary>ID ổn định.</summary>
        public string Id { get; }
        public string A { get; }
        public string B { get; }
        /// <summary>Rồng cần ít nhất một kiểu di chuyển trong danh sách.</summary>
        public IReadOnlyList<Locomotion> Requires { get; }
        /// <summary>zoneId → microhabitat cửa ngõ ở đầu đó.</summary>
        public IReadOnlyDictionary<string, string> Gateways { get; }
    }

    /// <summary>
    /// Bản đồ thế giới dạng graph (GDD 3.1 trang 5; ADR 0010; src/data/worldGraph.ts). node = zone, edge = đường đi.
    /// Khởi Nguyên là trung tâm: Thủy Vực nối qua cửa sông (cần bơi), Thiên Giới qua đảo thấp có chỗ đậu (cần bay).
    /// GDD chưa nêu cạnh Thủy Vực ↔ Thiên Giới nên không có cạnh trực tiếp.
    /// Bản TS b33ec7a chưa nối CanTravel vào moveDragon (ADR 0010 mục 4: "nối khi mở realm"). U2.8 nối qua World.Travel (cạnh + cửa ngõ đã mở).
    /// </summary>
    public static class WorldGraph
    {
        public static readonly IReadOnlyList<WorldEdge> Edges = Array.AsReadOnly(new[]
        {
            new WorldEdge("origin_ocean_estuary", "origin", "ocean", new[] { Locomotion.Swim },
                new Dictionary<string, string> { ["origin"] = "origin_pondbank", ["ocean"] = "ocean_estuary" }),
            new WorldEdge("origin_sky_low_island", "origin", "sky", new[] { Locomotion.Fly },
                new Dictionary<string, string> { ["origin"] = "origin_meadow", ["sky"] = "sky_low_island" }),
        });

        public static List<WorldEdge> EdgesOf(string zoneId)
        {
            var list = new List<WorldEdge>();
            foreach (WorldEdge e in Edges) if (e.A == zoneId || e.B == zoneId) list.Add(e);
            return list;
        }

        /// <summary>Có đường từ <paramref name="from"/> tới <paramref name="to"/> với các kiểu di chuyển cho trước không (BFS; cùng zone luôn đúng).</summary>
        public static bool CanTravel(string from, string to, IReadOnlyList<Locomotion> locomotion)
        {
            if (from == to) return true;
            var seen = new HashSet<string>(StringComparer.Ordinal) { from };
            var queue = new Queue<string>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                string z = queue.Dequeue();
                foreach (WorldEdge e in EdgesOf(z))
                {
                    bool can = false;
                    foreach (Locomotion m in e.Requires) foreach (Locomotion l in locomotion) if (m == l) can = true;
                    if (!can) continue;
                    string next = e.A == z ? e.B : e.A;
                    if (next == to) return true;
                    if (seen.Add(next)) queue.Enqueue(next);
                }
            }
            return false;
        }
    }
}
