using System;
using System.Collections.Generic;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Economy;

namespace Dravio.Domain.Sanctuary
{
    /// <summary>
    /// Quản lý khu bảo tồn (U2.7): kho rồng (bán, bán nhanh, khóa), nâng cấp, trang trí.
    /// IMPLEMENTATION MỚI theo GDD 3.1 (trang 15, Phụ lục C.3, E, F). Bản TS b33ec7a chỉ có hàm thuần sellPriceG / upgradeCostG
    /// (đã port ở U2.1, có golden); các lệnh ở đây không có ở TS nên không tuyên bố parity.
    /// Thả rồng (releaseDragon) GDD chỉ định nghĩa trong luồng cứu hộ (Care / rescue) — chưa port, không có ở đây.
    /// </summary>
    public static class SanctuaryRules
    {
        private static int LivingCount(GameState s)
        {
            int n = 0;
            foreach (DragonState d in s.Dragons) if (d.Location.Kind != LocationKind.Released) n++;
            return n;
        }

        /// <summary>
        /// Lý do không bán được (GDD tr. 15: "không bán rồng cuối, locked, busy hoặc chưa hoàn tất cứu hộ"; tr. 8: "không bán/chuyển bố mẹ bận").
        /// Bận = activity khác idle (lai, thám hiểm, chăm sóc) hoặc đang ở care/shelter.
        /// </summary>
        public static DomainErrorCode? SellBlock(GameState s, DragonState d)
        {
            if (d.Location.Kind == LocationKind.Released) return DomainErrorCode.DragonUnavailable;
            if (d.Locked) return DomainErrorCode.DragonLocked;
            if (d.Activity != DragonActivity.Idle || d.Location.Kind == LocationKind.Care || d.Location.Kind == LocationKind.Shelter) return DomainErrorCode.DragonBusy;
            if (LivingCount(s) <= 1) return DomainErrorCode.LastDragon;
            return null;
        }

        /// <summary>Giá bán hiện tại (G), theo stage dẫn xuất từ XP lúc bán.</summary>
        public static long SellPriceG(DragonState d)
        {
            DragonSpecies sp = SpeciesCatalog.Get(d.SpeciesId);
            return EconomyRates.SellPriceG(sp, EconomyRates.GrowthStageOf(d.XpSec, sp), d.Mutation);
        }

        public static DomainErrorCode? CheckSell(GameState s, string uid)
        {
            DragonState d = s.FindDragon(uid);
            return d == null ? DomainErrorCode.UnknownDragon : SellBlock(s, d);
        }

        /// <summary>Xóa rồng khỏi kho và ngân sách sản xuất; vàng bán KHÔNG tính vào totalGoldEarned. Phả hệ của con cháu giữ nguyên UID bố mẹ.</summary>
        private static void Remove(GameState s, DragonState d, List<DomainEvent> events)
        {
            // Ghi Bách khoa trước khi rồng rời kho: khám phá không bao giờ mất vì bán (GDD Phụ lục F).
            Progression.Codex.Observe(s, events);
            long price = SellPriceG(d);
            MicroGold gold = MicroGold.FromWholeGold(price);
            s.Player.Gold = s.Player.Gold + gold;
            s.Player.Stats.TotalSold += 1;
            s.Dragons.Remove(d);
            s.ProductionDragonIds.Remove(d.Uid);
            events.Add(new DragonSold(d.Uid, d.SpeciesId, gold.Micro));
        }

        public static DomainErrorCode? ApplySell(GameState s, string uid, List<DomainEvent> events)
        {
            DomainErrorCode? err = CheckSell(s, uid);
            if (err != null) return err;
            Remove(s, s.FindDragon(uid), events);
            return null;
        }

        /// <summary>
        /// Danh sách bán nhanh (GDD Phụ lục F): Common, Adult, chưa khóa, không bận, theo thứ tự kho. Nếu mọi rồng còn sống đều đủ điều kiện thì
        /// chừa rồng đầu tiên trong kho (GDD chỉ nói "chừa một rồng", không nói chừa con nào — ghi trong danh sách quyết định).
        /// </summary>
        public static List<DragonState> BulkSellList(GameState s)
        {
            var list = new List<DragonState>();
            foreach (DragonState d in s.Dragons)
            {
                DragonSpecies sp = SpeciesCatalog.Get(d.SpeciesId);
                if (sp.Rarity != Rarity.Common || EconomyRates.GrowthStageOf(d.XpSec, sp) != GrowthStage.Adult) continue;
                if (d.Location.Kind == LocationKind.Released || d.Locked || d.Activity != DragonActivity.Idle ||
                    d.Location.Kind == LocationKind.Care || d.Location.Kind == LocationKind.Shelter) continue;
                list.Add(d);
            }
            if (list.Count > 0 && list.Count >= LivingCount(s)) list.RemoveAt(0);
            return list;
        }

        /// <summary>Tổng giá bán nhanh (microG) để UI xin xác nhận; truyền lại vào BulkSellCommand.</summary>
        public static long BulkSellQuoteMicro(GameState s)
        {
            long total = 0;
            foreach (DragonState d in BulkSellList(s)) total = checked(total + MicroGold.FromWholeGold(SellPriceG(d)).Micro);
            return total;
        }

        public static DomainErrorCode? ApplyBulkSell(GameState s, long expectedTotalMicro, List<DomainEvent> events)
        {
            List<DragonState> list = BulkSellList(s);
            if (list.Count == 0) return DomainErrorCode.NothingToSell;
            if (BulkSellQuoteMicro(s) != expectedTotalMicro) return DomainErrorCode.QuoteChanged;
            foreach (DragonState d in list) Remove(s, d, events);
            return null;
        }

        public static DomainErrorCode? ApplyLock(GameState s, string uid, bool locked, List<DomainEvent> events)
        {
            DragonState d = s.FindDragon(uid);
            if (d == null) return DomainErrorCode.UnknownDragon;
            if (d.Location.Kind == LocationKind.Released) return DomainErrorCode.DragonUnavailable;
            if (d.Locked == locked) return DomainErrorCode.AlreadyThere;
            d.Locked = locked;
            events.Add(new DragonLockChanged(uid, locked));
            return null;
        }

        // ---------------- Nâng cấp ----------------

        private static UpgradeDef FindUpgrade(string id)
        {
            foreach (UpgradeDef u in EconomyTables.Upgrades) if (u.Id == id) return u;
            return null;
        }

        /// <summary>
        /// GDD Phụ lục E: cost = floor(base × scale^level), null khi tối đa. GDD không đặt điều kiện mở khóa nào ngoài giá và cấp tối đa.
        /// </summary>
        public static DomainErrorCode? CheckUpgrade(GameState s, string id)
        {
            UpgradeDef def = FindUpgrade(id);
            if (def == null) return DomainErrorCode.UnknownUpgrade;
            long? cost = EconomyRates.UpgradeCostG(def, def.LevelOf(s.Player.Upgrades));
            if (cost == null) return DomainErrorCode.UpgradeMaxed;
            if (s.Player.Gold < MicroGold.FromWholeGold(cost.Value)) return DomainErrorCode.NotEnoughGold;
            return null;
        }

        public static DomainErrorCode? ApplyUpgrade(GameState s, string id, List<DomainEvent> events)
        {
            DomainErrorCode? err = CheckUpgrade(s, id);
            if (err != null) return err;
            UpgradeDef def = FindUpgrade(id);
            UpgradesState u = s.Player.Upgrades;
            int level = def.LevelOf(u);
            MicroGold cost = MicroGold.FromWholeGold(EconomyRates.UpgradeCostG(def, level).Value);
            s.Player.Gold = s.Player.Gold - cost;
            switch (id)
            {
                case "islandCapacityLevel": u.IslandCapacityLevel++; break;
                case "foodQualityLevel": u.FoodQualityLevel++; break;
                case "foodCooldownLevel": u.FoodCooldownLevel++; break;
                case "mutationLuckLevel": u.MutationLuckLevel++; break;
                case "hatchSpeedLevel": u.HatchSpeedLevel++; break;
            }
            events.Add(new UpgradeBought(id, level + 1, cost.Micro));
            return null;
        }

        // ---------------- Trang trí ----------------

        /// <summary>Khu nhận được trang trí: tồn tại và đã mở. GDD "chỉ đặt ở môi trường hợp lệ" chưa có bảng món ↔ môi trường.</summary>
        private static DomainErrorCode? ZoneBlock(GameState s, string zoneId)
        {
            ZoneState z = s.FindZone(zoneId);
            return z == null || !z.Unlocked ? DomainErrorCode.ZoneLocked : (DomainErrorCode?)null;
        }

        public static DomainErrorCode? CheckBuyDecoration(GameState s, string zoneId, string decorationId)
        {
            if (!DecorationCatalog.TryGet(decorationId, out DecorationDef def)) return DomainErrorCode.UnknownDecoration;
            DomainErrorCode? z = ZoneBlock(s, zoneId);
            if (z != null) return z;
            if (s.FindZone(zoneId).DecorationIds.Contains(decorationId)) return DomainErrorCode.DecorationOwned;
            if (s.Player.Gold < MicroGold.FromWholeGold(def.PriceG)) return DomainErrorCode.NotEnoughGold;
            return null;
        }

        /// <summary>Mua: sở hữu và bật luôn. Danh sách giữ thứ tự catalog. Không có hiệu ứng kinh tế (GDD: bỏ +5 % vàng mỗi món).</summary>
        public static DomainErrorCode? ApplyBuyDecoration(GameState s, string zoneId, string decorationId, List<DomainEvent> events)
        {
            DomainErrorCode? err = CheckBuyDecoration(s, zoneId, decorationId);
            if (err != null) return err;
            DecorationCatalog.TryGet(decorationId, out DecorationDef def);
            ZoneState z = s.FindZone(zoneId);
            MicroGold price = MicroGold.FromWholeGold(def.PriceG);
            s.Player.Gold = s.Player.Gold - price;
            z.DecorationIds = InCatalogOrder(z.DecorationIds, decorationId);
            z.ActiveDecorationIds = InCatalogOrder(z.ActiveDecorationIds, decorationId);
            events.Add(new DecorationBought(zoneId, decorationId, price.Micro));
            events.Add(new DecorationToggled(zoneId, decorationId, true));
            return null;
        }

        public static DomainErrorCode? ApplyToggleDecoration(GameState s, string zoneId, string decorationId, bool active, List<DomainEvent> events)
        {
            if (!DecorationCatalog.TryGet(decorationId, out _)) return DomainErrorCode.UnknownDecoration;
            DomainErrorCode? zb = ZoneBlock(s, zoneId);
            if (zb != null) return zb;
            ZoneState z = s.FindZone(zoneId);
            if (!z.DecorationIds.Contains(decorationId)) return DomainErrorCode.DecorationNotOwned;
            if (z.ActiveDecorationIds.Contains(decorationId) == active) return DomainErrorCode.AlreadyThere;
            if (active) z.ActiveDecorationIds = InCatalogOrder(z.ActiveDecorationIds, decorationId);
            else z.ActiveDecorationIds.Remove(decorationId);
            events.Add(new DecorationToggled(zoneId, decorationId, active));
            return null;
        }

        private static List<string> InCatalogOrder(List<string> current, string add)
        {
            // Món catalog theo thứ tự catalog; ID lạ từ save cũ giữ nguyên ở cuối theo thứ tự cũ.
            var o = new List<string>();
            foreach (DecorationDef d in DecorationCatalog.All) if (d.Id == add || current.Contains(d.Id)) o.Add(d.Id);
            foreach (string id in current) if (!DecorationCatalog.TryGet(id, out _)) o.Add(id);
            return o;
        }
    }
}
