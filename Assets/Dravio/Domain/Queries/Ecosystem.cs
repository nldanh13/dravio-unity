using System;
using System.Collections.Generic;
using Dravio.Domain.Biology;
using Dravio.Domain.Breeding;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Economy;
using Dravio.Domain.Expedition;
using Dravio.Domain.Progression;
using Dravio.Domain.Sanctuary;
using Dravio.Domain.World;

namespace Dravio.Domain.Queries
{
    // ---------------- DTO chỉ đọc (snapshot tại một thời điểm; không giữ tham chiếu vào GameState) ----------------

    /// <summary>Trạng thái dẫn xuất của một rồng cho UI/3D. Thứ tự ưu tiên: released → care → shelter → breeding → expedition → zone → storage.</summary>
    public enum DragonStatus { Released, Care, Shelter, Breeding, Expedition, InHabitat, Storage }

    public sealed class DragonSnapshot
    {
        public string Uid { get; internal set; }
        public string SpeciesId { get; internal set; }
        public GrowthStage Stage { get; internal set; }
        public MutationType Mutation { get; internal set; }
        public DragonStatus Status { get; internal set; }
        public string ZoneId { get; internal set; }
        public string HabitatId { get; internal set; }
        public bool Producing { get; internal set; }
        public bool Locked { get; internal set; }
        /// <summary>Suitability 0–100 của chỗ hiện tại; null khi không ở microhabitat.</summary>
        public int? Suitability { get; internal set; }
        /// <summary>Vàng/giây (microG) khi đang sản xuất, 0 nếu không.</summary>
        public long RateMicroGPerSec { get; internal set; }
    }

    public sealed class HabitatOverview
    {
        public string HabitatId { get; internal set; }
        public string ZoneId { get; internal set; }
        public Realm Realm { get; internal set; }
        public TemperatureBand Temperature { get; internal set; }
        public HumidityBand Humidity { get; internal set; }
        public SalinityBand Salinity { get; internal set; }
        public DepthBand Depth { get; internal set; }
        public AltitudeBand Altitude { get; internal set; }
        public bool Perch { get; internal set; }
        public bool Unlocked { get; internal set; }
        /// <summary>Khám phá cần để mở (luật ADR 0009); null nếu không cần hoặc chưa có luật (Ocean/Sky).</summary>
        public string UnlockRequires { get; internal set; }
        public bool UnlockEligible { get; internal set; }
        public IReadOnlyList<DragonSnapshot> Dragons { get; internal set; }
        public int ProducingCount { get; internal set; }
    }

    public sealed class ZoneOverview
    {
        public string ZoneId { get; internal set; }
        public Realm Realm { get; internal set; }
        public bool Unlocked { get; internal set; }
        public int Capacity { get; internal set; }
        public int Occupancy { get; internal set; }
        public int FreeSlots => Math.Max(0, Capacity - Occupancy);
        public IReadOnlyList<HabitatOverview> Habitats { get; internal set; }
        /// <summary>Trang trí sở hữu / đang bật (chỉ hiển thị — không có hiệu ứng kinh tế hay sinh học, GDD Phụ lục E).</summary>
        public IReadOnlyList<string> Decorations { get; internal set; }
        public IReadOnlyList<string> ActiveDecorations { get; internal set; }
        /// <summary>Cạnh WorldGraph nối zone này và việc cạnh đó đang mở (cả hai zone + hai cửa ngõ đã mở).</summary>
        public IReadOnlyList<KeyValuePair<string, bool>> Edges { get; internal set; }
    }

    public sealed class TravelOption
    {
        public string ZoneId { get; internal set; }
        public string HabitatId { get; internal set; }
        public int Score { get; internal set; }
        public SuitabilityTier Tier { get; internal set; }
        public int FreeSlots { get; internal set; }
        /// <summary>null = chuyển được.</summary>
        public DomainErrorCode? Blocked { get; internal set; }
        /// <summary>Đường đi khi có (kể cả khi bị chặn vì lý do khác); null nếu không có đường.</summary>
        public TravelRoute Route { get; internal set; }
    }

    public sealed class ExpeditionCandidate
    {
        public string DragonUid { get; internal set; }
        public DomainErrorCode? Blocked { get; internal set; }
        public string Detail { get; internal set; }
        /// <summary>Điều kiện sức khỏe ADR chưa kiểm được (chưa có dữ liệu U2.6) — UI nên hiện "chưa hỗ trợ", không phải "đạt".</summary>
        public IReadOnlyList<string> UncheckedConditions { get; internal set; }
    }

    public sealed class ExpeditionOption
    {
        public string RouteId { get; internal set; }
        public string NameVi { get; internal set; }
        public string ZoneId { get; internal set; }
        public long DurationMs { get; internal set; }
        public ExpeditionRequirement Requirement { get; internal set; }
        public IReadOnlyList<string> Materials { get; internal set; }
        public int MinQty { get; internal set; }
        public int MaxQty { get; internal set; }
        /// <summary>null = khởi hành được (với ít nhất một rồng); RouteLocked / ExpeditionBusy nếu tuyến/slot không dùng được.</summary>
        public DomainErrorCode? Blocked { get; internal set; }
        public IReadOnlyList<ExpeditionCandidate> Candidates { get; internal set; }
    }

    public sealed class DiscoveryProgress
    {
        public IReadOnlyList<string> Discoveries { get; internal set; }
        public IReadOnlyList<HabitatUnlockInfo> HabitatUnlocks { get; internal set; }
        public IReadOnlyList<string> ProfilesViewed { get; internal set; }
        public int SpeciesDiscovered { get; internal set; }
        public int SpeciesTotal { get; internal set; }
        public int CodexCells { get; internal set; }
        public int CodexCellsTotal { get; internal set; }
        public IReadOnlyList<CodexEntry> Codex { get; internal set; }
        public IReadOnlyList<string> ZonesUnlocked { get; internal set; }
        public IReadOnlyList<string> AchievementsUnlocked { get; internal set; }
    }

    public sealed class ZoneUnlockInfo
    {
        public string ZoneId { get; internal set; }
        public bool Unlocked { get; internal set; }
        /// <summary>Điều kiện còn thiếu: tutorial-completed, origin-habitats, zone:&lt;id&gt;, quest:&lt;id&gt;. Rỗng khi mở được.</summary>
        public IReadOnlyList<string> Missing { get; internal set; }
        /// <summary>null = gửi unlockZone được ngay.</summary>
        public DomainErrorCode? Blocked { get; internal set; }
        public string RewardId { get; internal set; }
        public string RewardSpeciesId { get; internal set; }
        public bool RewardGranted { get; internal set; }
        /// <summary>Trứng đã cấp nhưng còn trong hàng chờ (kho đầy / đang có trứng chờ nhận).</summary>
        public bool RewardQueued { get; internal set; }
    }

    public sealed class MaterialStock
    {
        public string MaterialId { get; internal set; }
        public string NameVi { get; internal set; }
        public bool Active { get; internal set; }
        public long Quantity { get; internal set; }
    }

    public sealed class EcosystemSummary
    {
        /// <summary>Rồng chưa thả.</summary>
        public int Living { get; internal set; }
        /// <summary>Đang ở microhabitat và rảnh (không lai/thám hiểm).</summary>
        public int ActiveInHabitats { get; internal set; }
        public int Producing { get; internal set; }
        public int ProductionBudget { get; internal set; }
        public int Storage { get; internal set; }
        public int Care { get; internal set; }
        public int Shelter { get; internal set; }
        public int Breeding { get; internal set; }
        public int Expedition { get; internal set; }
        public int Released { get; internal set; }
        public int StorageLimit { get; internal set; }
        /// <summary>Chỗ đã dùng theo luật kho (rồng + trứng chờ + trứng trong tổ).</summary>
        public int StorageUsed { get; internal set; }
        public long RateMicroGPerSec { get; internal set; }
        public IReadOnlyList<KeyValuePair<string, int>> OccupancyByZone { get; internal set; }
    }

    /// <summary>Hành động trên một rồng: null = được phép; ngược lại là mã lỗi mà lệnh tương ứng sẽ trả (cùng hàm kiểm của Domain).</summary>
    public sealed class DragonActions
    {
        public string DragonUid { get; internal set; }
        public DomainErrorCode? MoveToStorage { get; internal set; }
        /// <summary>Có ít nhất một microhabitat chuyển tới được; chi tiết xem TravelOptions.</summary>
        public bool CanMoveToAnyHabitat { get; internal set; }
        public DomainErrorCode? EnableProduction { get; internal set; }
        public DomainErrorCode? DisableProduction { get; internal set; }
        public DomainErrorCode? Sell { get; internal set; }
        public long SellPriceG { get; internal set; }
        public DomainErrorCode? Lock { get; internal set; }
        public DomainErrorCode? Unlock { get; internal set; }
        /// <summary>
        /// Lý do lai xét riêng rồng này (Adult, rảnh, cooldown, không care/shelter/released, genotype hợp lệ) — kiểm với một bạn đời giả cùng loài,
        /// không họ hàng. Điều kiện theo cặp (nhóm, họ hàng, ứng viên, phí) xem Hatchery.Preview.
        /// </summary>
        public BreedRejection? Breed { get; internal set; }
        /// <summary>Tuyến → lý do (null = đi được).</summary>
        public IReadOnlyList<KeyValuePair<string, DomainErrorCode?>> Expeditions { get; internal set; }
    }

    /// <summary>
    /// API truy vấn CHỈ ĐỌC cho tầng Unity (U2.8). Mọi kết quả dùng đúng hàm kiểm mà CommandDispatcher dùng, nên UI không phải tự tính lại luật.
    /// Không đổi state, không tiêu RNG, không phụ thuộc đồng hồ trừ tham số nowMs (cooldown). Gọi trên snapshot GameState hiện tại của GameSession.
    /// </summary>
    public static class Ecosystem
    {
        public static DragonStatus StatusOf(GameState s, DragonState d)
        {
            switch (d.Location.Kind)
            {
                case LocationKind.Released: return DragonStatus.Released;
                case LocationKind.Care: return DragonStatus.Care;
                case LocationKind.Shelter: return DragonStatus.Shelter;
            }
            if (d.Activity == DragonActivity.Breeding) return DragonStatus.Breeding;
            if (d.Activity == DragonActivity.Expedition) return DragonStatus.Expedition;
            if (d.Activity == DragonActivity.Care) return DragonStatus.Care;
            return d.Location.Kind == LocationKind.Zone ? DragonStatus.InHabitat : DragonStatus.Storage;
        }

        public static DragonSnapshot Snapshot(GameState s, DragonState d)
        {
            DragonSpecies sp = SpeciesCatalog.Get(d.SpeciesId);
            GrowthStage stage = EconomyRates.GrowthStageOf(d.XpSec, sp);
            bool producing = Placement.IsProducing(s, d);
            return new DragonSnapshot
            {
                Uid = d.Uid, SpeciesId = d.SpeciesId, Stage = stage, Mutation = d.Mutation, Status = StatusOf(s, d),
                ZoneId = d.Location.ZoneId, HabitatId = d.Location.HabitatId, Producing = producing, Locked = d.Locked,
                Suitability = d.Location.Kind == LocationKind.Zone && d.Location.HabitatId != null ? Dravio.Domain.Biology.Suitability.ForSpecies(d.SpeciesId, d.Location.HabitatId).Score : (int?)null,
                RateMicroGPerSec = producing ? EconomyRates.RateMicroGPerSec(sp, stage, d.Mutation) : 0,
            };
        }

        public static HabitatOverview Habitat(GameState s, string habitatId)
        {
            if (!HabitatCatalog.TryGet(habitatId, out HabitatProfile h)) return null;
            ZoneState z = s.FindZone(h.ZoneId);
            HabitatUnlockInfo info = Zones.HabitatUnlockStatus(s).Find(x => x.HabitatId == habitatId);
            var dragons = new List<DragonSnapshot>();
            foreach (DragonState d in s.Dragons)
                if (d.Location.Kind == LocationKind.Zone && d.Location.HabitatId == habitatId) dragons.Add(Snapshot(s, d));
            return new HabitatOverview
            {
                HabitatId = h.Id, ZoneId = h.ZoneId, Realm = h.Realm, Temperature = h.TemperatureBand, Humidity = h.HumidityBand, Salinity = h.SalinityBand,
                Depth = h.DepthBand, Altitude = h.AltitudeBand, Perch = h.Perch, Unlocked = z != null && z.UnlockedHabitatIds.Contains(habitatId),
                UnlockRequires = info?.Requires, UnlockEligible = info?.Eligible ?? false, Dragons = dragons, ProducingCount = dragons.FindAll(x => x.Producing).Count,
            };
        }

        public static List<ZoneOverview> World(GameState s)
        {
            var list = new List<ZoneOverview>();
            foreach (string zoneId in WorldCatalog.ZoneIds)
            {
                ZoneState z = s.FindZone(zoneId);
                var habitats = new List<HabitatOverview>();
                foreach (string h in Zones.HabitatsOfZone(zoneId)) habitats.Add(Habitat(s, h));
                var edges = new List<KeyValuePair<string, bool>>();
                foreach (WorldEdge e in WorldGraph.EdgesOf(zoneId)) edges.Add(new KeyValuePair<string, bool>(e.Id, Travel.FindRoute(s, e.A, e.B, e.Requires) != null));
                list.Add(new ZoneOverview
                {
                    ZoneId = zoneId, Realm = z?.Realm ?? Realm.Land, Unlocked = z?.Unlocked ?? false, Capacity = z?.Capacity ?? 0, Occupancy = Zones.ZoneOccupancy(s, zoneId),
                    Habitats = habitats, Decorations = z?.DecorationIds ?? new List<string>(), ActiveDecorations = z?.ActiveDecorationIds ?? new List<string>(), Edges = edges,
                });
            }
            return list;
        }

        public static List<TravelOption> TravelOptions(GameState s, string uid)
        {
            DragonState d = s.FindDragon(uid);
            var list = new List<TravelOption>();
            if (d == null) return list;
            foreach (PlacementCheck c in Zones.PlacementOptions(s, d))
                list.Add(new TravelOption
                {
                    ZoneId = c.ZoneId, HabitatId = c.HabitatId, Score = c.Result.Score, Tier = c.Tier, FreeSlots = c.FreeSlots, Blocked = c.Blocked,
                    Route = Travel.RouteFor(s, d, c.ZoneId),
                });
            return list;
        }

        /// <summary>Đúng mã lỗi mà moveDragon sẽ trả với đích này (null = hợp lệ).</summary>
        public static DomainErrorCode? TravelEligibility(GameState s, string uid, MoveTarget target)
        {
            DragonState d = s.FindDragon(uid);
            return d == null ? DomainErrorCode.UnknownDragon : Zones.CheckMove(s, d, target);
        }

        public static List<ExpeditionOption> ExpeditionOptions(GameState s)
        {
            var list = new List<ExpeditionOption>();
            foreach (ExpeditionRoute r in ExpeditionCatalog.Routes)
            {
                var cands = new List<ExpeditionCandidate>();
                foreach (DragonState d in s.Dragons)
                {
                    if (d.Location.Kind == LocationKind.Released) continue;
                    DomainErrorCode? b = Expeditions.DragonBlock(s, r, d, out string detail);
                    cands.Add(new ExpeditionCandidate { DragonUid = d.Uid, Blocked = b, Detail = detail, UncheckedConditions = Expeditions.UncheckedHealthConditions });
                }
                DomainErrorCode? routeBlock = !Expeditions.RouteOpen(s, r) ? DomainErrorCode.RouteLocked : !r.RewardsDefined ? DomainErrorCode.RouteRewardsUndefined
                    : s.Expedition != null ? DomainErrorCode.ExpeditionBusy : (DomainErrorCode?)null;
                list.Add(new ExpeditionOption
                {
                    RouteId = r.Id, NameVi = r.NameVi, ZoneId = r.ZoneId, DurationMs = r.DurationMs, Requirement = r.Requirement, Materials = r.Materials,
                    MinQty = r.MinQty, MaxQty = r.MaxQty, Blocked = routeBlock, Candidates = cands,
                });
            }
            return list;
        }

        /// <summary>Đúng mã lỗi startExpedition sẽ trả (null = hợp lệ); <paramref name="detail"/> giải thích DragonUnsuited.</summary>
        public static DomainErrorCode? ExpeditionEligibility(GameState s, string routeId, string uid, out string detail) => Expeditions.CheckStart(s, routeId, uid, out detail);

        /// <summary>Trạng thái mở Thủy Vực / Thiên Giới và trứng thưởng (ADR U2.8-0201).</summary>
        public static List<ZoneUnlockInfo> ZoneUnlocks(GameState s)
        {
            var list = new List<ZoneUnlockInfo>();
            foreach (ZoneUnlockRule r in WorldUnlockCatalog.Rules)
            {
                ZoneState z = s.FindZone(r.ZoneId);
                list.Add(new ZoneUnlockInfo
                {
                    ZoneId = r.ZoneId, Unlocked = z?.Unlocked == true, Missing = WorldExpansion.MissingRequirements(s, r.ZoneId),
                    Blocked = WorldExpansion.CheckZoneUnlock(s, r.ZoneId, out _), RewardId = r.RewardId, RewardSpeciesId = r.EggSpeciesId,
                    RewardGranted = s.GrantedRewards.Contains(r.RewardId), RewardQueued = s.RewardQueue.Exists(q => q.RewardId == r.RewardId),
                });
            }
            return list;
        }

        /// <summary>Kho vật liệu theo catalog chuẩn (gồm vật liệu chưa kích hoạt, số lượng 0).</summary>
        public static List<MaterialStock> Materials(GameState s)
        {
            var list = new List<MaterialStock>();
            foreach (MaterialDef m in ExpeditionCatalog.Materials)
                list.Add(new MaterialStock { MaterialId = m.Id, NameVi = m.NameVi, Active = m.Active, Quantity = s.Player.Inventory.TryGetValue(m.Id, out long q) ? q : 0 });
            return list;
        }

        public static DiscoveryProgress Discovery(GameState s)
        {
            var zones = new List<string>();
            foreach (ZoneState z in s.Zones) if (z.Unlocked) zones.Add(z.Id);
            return new DiscoveryProgress
            {
                Discoveries = s.Discoveries, HabitatUnlocks = Zones.HabitatUnlockStatus(s), ProfilesViewed = s.Tutorial?.ProfilesViewed ?? new List<string>(),
                SpeciesDiscovered = Codex.SpeciesDiscovered(s), SpeciesTotal = SpeciesCatalog.All.Count, CodexCells = Codex.CellsDiscovered(s),
                CodexCellsTotal = SpeciesCatalog.All.Count * Mutation.Order.Count, Codex = s.Codex, ZonesUnlocked = zones, AchievementsUnlocked = s.UnlockedAchievements,
            };
        }

        public static EcosystemSummary Summary(GameState s)
        {
            var sum = new EcosystemSummary { ProductionBudget = Zones.CurrentProductionBudget(s), StorageLimit = StorageCatalog.MaxDragons, StorageUsed = Hatchery.OccupiedSlots(s) };
            long rate = 0;
            foreach (DragonState d in s.Dragons)
            {
                DragonStatus st = StatusOf(s, d);
                if (st == DragonStatus.Released) { sum.Released++; continue; }
                sum.Living++;
                switch (st)
                {
                    case DragonStatus.Care: sum.Care++; break;
                    case DragonStatus.Shelter: sum.Shelter++; break;
                    case DragonStatus.Breeding: sum.Breeding++; break;
                    case DragonStatus.Expedition: sum.Expedition++; break;
                    case DragonStatus.InHabitat: sum.ActiveInHabitats++; break;
                    case DragonStatus.Storage: sum.Storage++; break;
                }
                DragonSnapshot snap = Snapshot(s, d);
                if (snap.Producing) { sum.Producing++; rate += snap.RateMicroGPerSec; }
            }
            sum.RateMicroGPerSec = rate;
            var occ = new List<KeyValuePair<string, int>>();
            foreach (string z in WorldCatalog.ZoneIds) occ.Add(new KeyValuePair<string, int>(z, Zones.ZoneOccupancy(s, z)));
            sum.OccupancyByZone = occ;
            return sum;
        }

        public static List<DragonSnapshot> DragonsInZone(GameState s, string zoneId)
        {
            var list = new List<DragonSnapshot>();
            foreach (DragonState d in s.Dragons) if (d.Location.Kind == LocationKind.Zone && d.Location.ZoneId == zoneId) list.Add(Snapshot(s, d));
            return list;
        }

        public static List<DragonSnapshot> DragonsWithStatus(GameState s, DragonStatus status)
        {
            var list = new List<DragonSnapshot>();
            foreach (DragonState d in s.Dragons) if (StatusOf(s, d) == status) list.Add(Snapshot(s, d));
            return list;
        }

        /// <summary>Mọi hành động trên một rồng kèm lý do bị khóa. null nếu UID không tồn tại.</summary>
        public static DragonActions Actions(GameState s, string uid, long nowMs)
        {
            DragonState d = s.FindDragon(uid);
            if (d == null) return null;
            var exp = new List<KeyValuePair<string, DomainErrorCode?>>();
            foreach (ExpeditionRoute r in ExpeditionCatalog.Routes) exp.Add(new KeyValuePair<string, DomainErrorCode?>(r.Id, Expeditions.CheckStart(s, r.Id, uid, out _)));
            bool anyHabitat = false;
            foreach (PlacementCheck c in Zones.PlacementOptions(s, d)) if (c.Blocked == null) anyHabitat = true;
            return new DragonActions
            {
                DragonUid = uid, MoveToStorage = Zones.CheckMove(s, d, MoveTarget.Storage), CanMoveToAnyHabitat = anyHabitat,
                EnableProduction = Zones.CheckProduction(s, d, true), DisableProduction = Zones.CheckProduction(s, d, false),
                Sell = SanctuaryRules.CheckSell(s, uid), SellPriceG = SanctuaryRules.SellPriceG(d),
                Lock = d.Locked ? DomainErrorCode.AlreadyThere : d.Location.Kind == LocationKind.Released ? DomainErrorCode.DragonUnavailable : (DomainErrorCode?)null,
                Unlock = !d.Locked ? DomainErrorCode.AlreadyThere : d.Location.Kind == LocationKind.Released ? DomainErrorCode.DragonUnavailable : (DomainErrorCode?)null,
                Breed = BreedingRules.CheckBreed(d, new DragonState { Uid = "__self_check__", SpeciesId = d.SpeciesId, Genotype = d.Genotype, XpSec = d.XpSec, Location = DragonLocation.Storage, Activity = DragonActivity.Idle }, nowMs),
                Expeditions = exp,
            };
        }
    }
}
