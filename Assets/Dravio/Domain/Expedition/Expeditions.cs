using System;
using System.Collections.Generic;
using System.Globalization;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Economy;

namespace Dravio.Domain.Expedition
{
    /// <summary>
    /// Thám hiểm P0 (U2.8 — IMPLEMENTATION MỚI theo GDD 3.1 trang 22; TS b33ec7a không có). Luật lấy từ GDD:
    /// một slot chung; chuyến đi chốt thời gian, seed và bảng thưởng khi bắt đầu; rồng đi thám hiểm không sinh vàng, không lai, không bán/chuyển;
    /// hoàn tất offline; nhận thưởng một lần; không tự khởi hành tiếp. MVP thành công 100 % (ADR U2.8-0201). Ngưỡng sức khỏe ADR
    /// (Health ≥ 80, Mobility ≥ 80, Comfort ≥ 60, không có ca chăm sóc mở) CHƯA kiểm được vì chưa có dữ liệu sức khỏe U2.6 — không giả lập
    /// "khỏe"; API trả các điều kiện chưa hỗ trợ trong <see cref="UncheckedHealthConditions"/>.
    /// RNG: luồng riêng hashSeed("expedition", saveId, revision) — tất định theo state, độc lập RNG hình ảnh.
    /// </summary>
    public static class Expeditions
    {
        /// <summary>Lý do một rồng không đi được tuyến này (null = được). Chi tiết ghi trong <paramref name="detail"/>.</summary>
        public static DomainErrorCode? DragonBlock(GameState s, ExpeditionRoute route, DragonState d, out string detail)
        {
            detail = null;
            if (d.Location.Kind == LocationKind.Released) return DomainErrorCode.DragonUnavailable;
            if (d.Activity != DragonActivity.Idle || d.Location.Kind == LocationKind.Care || d.Location.Kind == LocationKind.Shelter) return DomainErrorCode.DragonBusy;
            DragonSpecies sp = SpeciesCatalog.Get(d.SpeciesId);
            if (EconomyRates.GrowthStageOf(d.XpSec, sp) != GrowthStage.Adult) { detail = "not-adult"; return DomainErrorCode.DragonUnsuited; }
            IReadOnlyList<Locomotion> loco = SpeciesCatalog.GetBiology(d.SpeciesId).Locomotion;
            bool ok;
            switch (route.Requirement)
            {
                // "1 Adult sống đất": loài có realm đất liền.
                case ExpeditionRequirement.LandDweller: ok = Contains(sp.Realms, Realm.Land); detail = ok ? null : "requires-land"; break;
                case ExpeditionRequirement.Swimmer: ok = Contains(loco, Locomotion.Swim); detail = ok ? null : "requires-swim"; break;
                default: ok = Contains(loco, Locomotion.Fly); detail = ok ? null : "requires-fly"; break;
            }
            return ok ? (DomainErrorCode?)null : DomainErrorCode.DragonUnsuited;
        }

        private static bool Contains<T>(IReadOnlyList<T> xs, T v)
        {
            foreach (T x in xs) if (EqualityComparer<T>.Default.Equals(x, v)) return true;
            return false;
        }

        /// <summary>Tuyến bị khóa khi thế giới của nó chưa mở (không vượt khóa thế giới hiện hành).</summary>
        public static bool RouteOpen(GameState s, ExpeditionRoute route) => s.FindZone(route.ZoneId)?.Unlocked == true;

        /// <summary>Điều kiện sức khỏe ADR U2.8-0201 chưa được hỗ trợ (chưa có dữ liệu U2.6). Rỗng khi tích hợp xong.</summary>
        public static readonly IReadOnlyList<string> UncheckedHealthConditions = Array.AsReadOnly(new[] { "health>=80", "mobility>=80", "comfort>=60", "no-open-care-case" });

        public static DomainErrorCode? CheckStart(GameState s, string routeId, string uid, out string detail)
        {
            detail = null;
            if (!ExpeditionCatalog.TryGet(routeId, out ExpeditionRoute route)) return DomainErrorCode.UnknownRoute;
            if (!RouteOpen(s, route)) return DomainErrorCode.RouteLocked;
            if (!route.RewardsDefined) return DomainErrorCode.RouteRewardsUndefined;
            if (s.Expedition != null) return DomainErrorCode.ExpeditionBusy;
            DragonState d = s.FindDragon(uid);
            if (d == null) return DomainErrorCode.UnknownDragon;
            return DragonBlock(s, route, d, out detail);
        }

        public static DomainErrorCode? ApplyStart(GameState s, string routeId, string uid, long nowMs, List<DomainEvent> events)
        {
            DomainErrorCode? err = CheckStart(s, routeId, uid, out _);
            if (err != null) return err;
            ExpeditionCatalog.TryGet(routeId, out ExpeditionRoute route);
            IRng rng = Rng.Create(Rng.HashSeed("expedition", s.SaveId, s.Revision.ToString(CultureInfo.InvariantCulture)));
            int qty = route.MinQty + rng.RandomInt(route.MaxQty - route.MinQty + 1);
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < qty; i++)
            {
                string m = rng.PickUniform(route.Materials);
                counts[m] = counts.TryGetValue(m, out int c) ? c + 1 : 1;
            }
            var rewards = new List<KeyValuePair<string, int>>();
            foreach (string m in ExpeditionCatalog.MaterialIds) if (counts.TryGetValue(m, out int c)) rewards.Add(new KeyValuePair<string, int>(m, c));
            string id = InitialStateFactory.NewUid(rng, "exp");
            DragonState d = s.FindDragon(uid);
            d.Activity = DragonActivity.Expedition;
            s.Expedition = new ExpeditionJob
            {
                Id = id, RouteId = routeId, DragonUid = uid, StartedAtMs = nowMs, FinishAtMs = nowMs + route.DurationMs, Rewards = rewards, Status = ExpeditionStatus.InProgress,
            };
            events.Add(new ExpeditionStarted(id, routeId, uid, s.Expedition.FinishAtMs));
            return null;
        }

        internal static long? CompletesBy(GameState s, long toMs)
        {
            ExpeditionJob j = s.Expedition;
            return j != null && j.Status == ExpeditionStatus.InProgress && j.FinishAtMs <= toMs ? j.FinishAtMs : (long?)null;
        }

        internal static bool IsTraveller(GameState s, DragonState d) =>
            s.Expedition != null && s.Expedition.Status == ExpeditionStatus.InProgress && s.Expedition.DragonUid == d.Uid;

        /// <summary>Chuyến đi xong: rồng về idle tại atMs (vẫn ở chỗ cũ), thưởng chờ nhận.</summary>
        internal static void Complete(GameState s, long atMs, List<DomainEvent> events)
        {
            ExpeditionJob j = s.Expedition;
            j.Status = ExpeditionStatus.Ready;
            DragonState d = s.FindDragon(j.DragonUid);
            if (d != null && d.Activity == DragonActivity.Expedition) d.Activity = DragonActivity.Idle;
            events.Add(new ExpeditionReady(j.Id, atMs));
        }

        public static DomainErrorCode? CheckClaim(GameState s, long nowMs)
        {
            if (s.Expedition == null) return DomainErrorCode.NoExpedition;
            if (s.Expedition.Status != ExpeditionStatus.Ready && s.Expedition.FinishAtMs > nowMs) return DomainErrorCode.ExpeditionNotReady;
            return null;
        }

        /// <summary>Cộng vật liệu vào inventory đúng một lần rồi giải phóng slot (chạy sau quyết toán tới now).</summary>
        public static DomainErrorCode? ApplyClaim(GameState s, long nowMs, List<DomainEvent> events)
        {
            DomainErrorCode? err = CheckClaim(s, nowMs);
            if (err != null) return err;
            ExpeditionJob j = s.Expedition;
            foreach (var kv in j.Rewards)
                s.Player.Inventory[kv.Key] = checked((s.Player.Inventory.TryGetValue(kv.Key, out long cur) ? cur : 0) + kv.Value);
            s.Expedition = null;
            events.Add(new ExpeditionClaimed(j.Id, j.Rewards));
            return null;
        }
    }
}
