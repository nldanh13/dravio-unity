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
    /// Kho là vị trí TRUNG LẬP (ADR U2.8-0201): không thuộc Origin/Ocean/Sky, không sinh vàng, không phải điểm xuất phát tìm đường.
    /// Đưa rồng ra kho áp dụng luật đặt rồng + sinh học + sức chứa + khóa thế giới, cộng chốt "kiểu di chuyển vào vùng"
    /// (<see cref="EntryLocomotion"/>) để kho không bao giờ là lối tắt vượt WorldGraph. Vận chuyển bằng phương tiện cứu hộ chưa port.
    /// </summary>
    public static class Travel
    {
        /// <summary>Zone hiện tại của rồng cho mục đích tìm đường; null nếu rồng không ở zone (kho là trung lập).</summary>
        public static string FromZone(DragonState d) => d.Location.Kind == LocationKind.Zone ? d.Location.ZoneId : null;

        /// <summary>
        /// Kiểu di chuyển cần để vào một vùng: hợp yêu cầu của các cạnh WorldGraph đi VÀO vùng đó (đầu B của cạnh). Thủy Vực: bơi; Thiên Giới: bay;
        /// Khởi Nguyên (gốc đồ thị, chỉ là đầu A): không cần. Lấy từ dữ liệu WorldGraph, không thêm luật.
        /// </summary>
        public static List<Locomotion> EntryLocomotion(string zoneId)
        {
            var o = new List<Locomotion>();
            foreach (WorldEdge e in WorldGraph.Edges) if (e.B == zoneId) foreach (Locomotion l in e.Requires) if (!o.Contains(l)) o.Add(l);
            return o;
        }

        /// <summary>
        /// Rồng từ kho vào được vùng này không: vùng gốc (không có cạnh đi vào) luôn được; vùng khác cần ít nhất một cạnh đi VÀO đang mở
        /// (hai zone + hai cửa ngõ đã mở) mà rồng có kiểu di chuyển của cạnh. Như vậy kho không vượt được kiểu di chuyển lẫn cửa ngõ đóng.
        /// </summary>
        public static bool CanEnter(GameState s, DragonState d, string zoneId)
        {
            IReadOnlyList<Locomotion> have = SpeciesCatalog.GetBiology(d.SpeciesId).Locomotion;
            bool hasIncoming = false;
            foreach (WorldEdge e in WorldGraph.Edges)
            {
                if (e.B != zoneId) continue;
                hasIncoming = true;
                if (CanUse(e, have) && EdgeOpen(s, e)) return true;
            }
            return !hasIncoming;
        }

        /// <summary>Rồng đi tới được zone này không: từ kho → <see cref="CanEnter"/>; từ một zone → có đường WorldGraph.</summary>
        public static bool CanReach(GameState s, DragonState d, string toZone) =>
            d.Location.Kind == LocationKind.Storage ? CanEnter(s, d, toZone) : RouteFor(s, d, toZone) != null;

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
    /// Mở rộng thế giới (U2.8; quyết định ADR U2.8-0201 do chủ dự án duyệt):
    /// Thủy Vực = hoàn thành Tutorial Khởi Nguyên + mở đủ 3 sinh cảnh Khởi Nguyên + nhiệm vụ khám phá thủy sinh → trứng Droplet Normal.
    /// Thiên Giới = đã mở Thủy Vực + nhiệm vụ khám phá Thiên Giới → trứng Zephyr Normal. Không cấp độ, không chi phí vàng, không cần sở hữu Droplet/Zephyr.
    /// Nội dung nhiệm vụ TBD: <see cref="ApplyQuestComplete"/> là hàm engine cho hệ nhiệm vụ tương lai, không có lệnh công khai.
    /// Trứng thưởng: một lần (grantedRewards), Normal, không tính pity; khi kho đầy hoặc đang có trứng chờ → vào rewardQueue, giao sau, không mất.
    /// </summary>
    public static class WorldExpansion
    {
        /// <summary>Điều kiện còn thiếu để mở zone (rỗng = đủ). Mã: tutorial-completed, origin-habitats, zone:&lt;id&gt;, quest:&lt;id&gt;.</summary>
        public static List<string> MissingRequirements(GameState s, string zoneId)
        {
            var o = new List<string>();
            ZoneUnlockRule r = WorldUnlockCatalog.RuleFor(zoneId);
            if (r == null) return o;
            if (r.RequiresTutorialCompleted && s.Tutorial?.Status != TutorialStatus.Completed) o.Add("tutorial-completed");
            if (r.RequiresAllOriginHabitats)
            {
                ZoneState origin = s.FindZone("origin");
                foreach (string h in Zones.HabitatsOfZone("origin")) if (origin == null || !origin.UnlockedHabitatIds.Contains(h)) { o.Add("origin-habitats"); break; }
            }
            if (r.RequiresZone != null && s.FindZone(r.RequiresZone)?.Unlocked != true) o.Add("zone:" + r.RequiresZone);
            if (r.RequiresQuest != null && !s.CompletedQuests.Contains(r.RequiresQuest)) o.Add("quest:" + r.RequiresQuest);
            return o;
        }

        public static DomainErrorCode? CheckZoneUnlock(GameState s, string zoneId, out string detail)
        {
            detail = null;
            ZoneState z = s.FindZone(zoneId);
            if (z == null || WorldUnlockCatalog.RuleFor(zoneId) == null) return DomainErrorCode.UnknownZone;
            if (z.Unlocked) return DomainErrorCode.AlreadyThere;
            List<string> missing = MissingRequirements(s, zoneId);
            if (missing.Count > 0) { detail = string.Join(",", missing); return DomainErrorCode.UnlockRequirementMissing; }
            return null;
        }

        /// <summary>Lệnh unlockZone: kiểm điều kiện, mở zone + mọi microhabitat, cấp trứng cố định một lần.</summary>
        public static DomainErrorCode? ApplyZoneUnlockCommand(GameState s, string zoneId, long nowMs, List<DomainEvent> events)
        {
            DomainErrorCode? err = CheckZoneUnlock(s, zoneId, out _);
            if (err != null) return err;
            ApplyZoneUnlock(s, zoneId, events);
            ZoneUnlockRule r = WorldUnlockCatalog.RuleFor(zoneId);
            if (!s.GrantedRewards.Contains(r.RewardId))
            {
                s.GrantedRewards.Add(r.RewardId);
                s.RewardQueue.Add(new QueuedEgg { RewardId = r.RewardId, SpeciesId = r.EggSpeciesId, Mutation = r.EggMutation, QueuedAtMs = nowMs });
                bool delivered = Rewards.TryDeliver(s, nowMs, events);
                events.Add(new RewardEggGranted(r.RewardId, r.EggSpeciesId, delivered));
            }
            return null;
        }

        /// <summary>
        /// Mở một zone đang khóa và toàn bộ microhabitat của nó (không kiểm điều kiện — dùng cho lệnh sau khi kiểm, cho hệ nhiệm vụ tương lai và test).
        /// Không đổi sức chứa (5/zone P0), ngân sách hay vàng; không cấp trứng.
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

        /// <summary>Đánh dấu nhiệm vụ hoàn thành (hàm engine; nội dung nhiệm vụ TBD, chưa có lệnh). Idempotent bị từ chối.</summary>
        public static DomainErrorCode? ApplyQuestComplete(GameState s, string questId, List<DomainEvent> events)
        {
            if (!WorldUnlockCatalog.IsQuestId(questId)) return DomainErrorCode.InvalidCommand;
            if (s.CompletedQuests.Contains(questId)) return DomainErrorCode.AlreadyThere;
            var ordered = new List<string>();
            foreach (QuestDef q in WorldUnlockCatalog.Quests) if (q.Id == questId || s.CompletedQuests.Contains(q.Id)) ordered.Add(q.Id);
            s.CompletedQuests = ordered;
            events?.Add(new QuestCompleted(questId));
            return null;
        }
    }

    /// <summary>Giao trứng thưởng từ hàng chờ vào pendingPack khi slot trống và kho còn chỗ (chạy sau mỗi lệnh thành công).</summary>
    public static class Rewards
    {
        public static bool TryDeliver(GameState s, long nowMs, List<DomainEvent> events)
        {
            if (s.RewardQueue.Count == 0 || s.PendingPack != null || !Breeding.Hatchery.HasRoomForNewEgg(s)) return false;
            QueuedEgg q = s.RewardQueue[0];
            s.RewardQueue.RemoveAt(0);
            string eggUid = InitialStateFactory.NewUid(Rng.Create(Rng.HashSeed("zone-egg", q.RewardId, s.SaveId)), "e");
            string id = ("pack_" + s.SaveId + "_" + q.RewardId);
            if (id.Length > 64) id = id.Substring(0, 64);
            s.PendingPack = new PendingPack
            {
                Id = id, PackId = q.RewardId, SpeciesId = q.SpeciesId, Mutation = q.Mutation, CreatedAtMs = nowMs, Tutorial = false,
                Genotype = Biology.Genetics.FounderGenotype(eggUid, s.ContentVersion, SpeciesCatalog.Get(q.SpeciesId).Loci),
            };
            events?.Add(new RewardEggDelivered(q.RewardId, id));
            return true;
        }
    }
}
