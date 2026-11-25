using System.Collections.Generic;

namespace Dravio.Domain.Core
{
    /// <summary>
    /// Domain event do engine phát sau mỗi lệnh thành công (src/engine/core/events.ts).
    /// Lớp trình bày (Unity) chỉ đọc, không suy diễn lại luật. U2.1 port 9 loại event, U2.3 thêm 6 loại của world/habitat, U2.4 thêm 5 loại của gói/thành tựu/hướng dẫn (đủ 20 loại của bản TS).
    /// </summary>
    public abstract class DomainEvent
    {
        /// <summary>Tên loại event giống trường <c>type</c> của TypeScript.</summary>
        public abstract string Type { get; }
    }

    public sealed class TimeAdvanced : DomainEvent
    {
        public TimeAdvanced(long fromMs, long toMs, long elapsedMs, AdvanceMode mode, long goldMicro)
        {
            FromMs = fromMs; ToMs = toMs; ElapsedMs = elapsedMs; Mode = mode; GoldMicro = goldMicro;
        }
        public override string Type => "timeAdvanced";
        public long FromMs { get; }
        public long ToMs { get; }
        public long ElapsedMs { get; }
        public AdvanceMode Mode { get; }
        /// <summary>Vàng sản xuất trong khoảng (microG).</summary>
        public long GoldMicro { get; }
    }

    public sealed class StageChanged : DomainEvent
    {
        public StageChanged(string dragonUid, GrowthStage from, GrowthStage to) { DragonUid = dragonUid; From = from; To = to; }
        public override string Type => "stageChanged";
        public string DragonUid { get; }
        public GrowthStage From { get; }
        public GrowthStage To { get; }
    }

    public sealed class OfflineReceiptCreated : DomainEvent
    {
        public OfflineReceiptCreated(string receiptId, long goldMicro, long elapsedMs) { ReceiptId = receiptId; GoldMicro = goldMicro; ElapsedMs = elapsedMs; }
        public override string Type => "offlineReceiptCreated";
        public string ReceiptId { get; }
        public long GoldMicro { get; }
        public long ElapsedMs { get; }
    }

    public sealed class FoodSpawned : DomainEvent
    {
        public FoodSpawned(string foodId, DietId dietId, string zoneId, string habitatId, long expiresAtMs)
        {
            FoodId = foodId; DietId = dietId; ZoneId = zoneId; HabitatId = habitatId; ExpiresAtMs = expiresAtMs;
        }
        public override string Type => "foodSpawned";
        public string FoodId { get; }
        public DietId DietId { get; }
        public string ZoneId { get; }
        public string HabitatId { get; }
        public long ExpiresAtMs { get; }
    }

    public sealed class FoodExpired : DomainEvent
    {
        public FoodExpired(string foodId) { FoodId = foodId; }
        public override string Type => "foodExpired";
        public string FoodId { get; }
    }

    public sealed class FoodConsumed : DomainEvent
    {
        public FoodConsumed(string foodId, string dragonUid, int xpGainedSec) { FoodId = foodId; DragonUid = dragonUid; XpGainedSec = xpGainedSec; }
        public override string Type => "foodConsumed";
        public string FoodId { get; }
        public string DragonUid { get; }
        public int XpGainedSec { get; }
    }

    public sealed class BondChanged : DomainEvent
    {
        public BondChanged(string dragonUid, int from, int to, BondSource source) { DragonUid = dragonUid; From = from; To = to; Source = source; }
        public override string Type => "bondChanged";
        public string DragonUid { get; }
        public int From { get; }
        public int To { get; }
        public BondSource Source { get; }
    }

    public sealed class DragonPetted : DomainEvent
    {
        public DragonPetted(string dragonUid) { DragonUid = dragonUid; }
        public override string Type => "dragonPetted";
        public string DragonUid { get; }
    }

    public sealed class DragonRenamed : DomainEvent
    {
        public DragonRenamed(string dragonUid, string nickname) { DragonUid = dragonUid; Nickname = nickname; }
        public override string Type => "dragonRenamed";
        public string DragonUid { get; }
        /// <summary>null khi xóa tên.</summary>
        public string Nickname { get; }
    }

    // ---- U2.3: world / habitat ----

    public sealed class DragonMoved : DomainEvent
    {
        public DragonMoved(string dragonUid, DragonLocation from, DragonLocation to) { DragonUid = dragonUid; From = from; To = to; }
        public override string Type => "dragonMoved";
        public string DragonUid { get; }
        public DragonLocation From { get; }
        public DragonLocation To { get; }
    }

    public sealed class ProductionChanged : DomainEvent
    {
        public ProductionChanged(string dragonUid, bool enabled) { DragonUid = dragonUid; Enabled = enabled; }
        public override string Type => "productionChanged";
        public string DragonUid { get; }
        public bool Enabled { get; }
    }

    public sealed class HabitatUnlocked : DomainEvent
    {
        public HabitatUnlocked(string zoneId, string habitatId) { ZoneId = zoneId; HabitatId = habitatId; }
        public override string Type => "habitatUnlocked";
        public string ZoneId { get; }
        public string HabitatId { get; }
    }

    public sealed class DiscoveryRecorded : DomainEvent
    {
        public DiscoveryRecorded(string discoveryId) { DiscoveryId = discoveryId; }
        public override string Type => "discoveryRecorded";
        public string DiscoveryId { get; }
    }

    public sealed class HabitatSurveyed : DomainEvent
    {
        public HabitatSurveyed(string habitatId) { HabitatId = habitatId; }
        public override string Type => "habitatSurveyed";
        public string HabitatId { get; }
    }

    public sealed class BiologyProfileViewed : DomainEvent
    {
        public BiologyProfileViewed(string speciesId) { SpeciesId = speciesId; }
        public override string Type => "biologyProfileViewed";
        public string SpeciesId { get; }
    }

    // ---- U2.4: starter pack, achievements, tutorial ----

    public sealed class PackBought : DomainEvent
    {
        public PackBought(string packId, string speciesId, long priceGoldMicro, bool tutorial) { PackId = packId; SpeciesId = speciesId; PriceGoldMicro = priceGoldMicro; Tutorial = tutorial; }
        public override string Type => "packBought";
        public string PackId { get; }
        public string SpeciesId { get; }
        public long PriceGoldMicro { get; }
        public bool Tutorial { get; }
    }

    public sealed class DragonHatched : DomainEvent
    {
        public DragonHatched(string dragonUid, string speciesId, MutationType mutation, DragonLocation location, bool tutorial)
        {
            DragonUid = dragonUid; SpeciesId = speciesId; Mutation = mutation; Location = location; Tutorial = tutorial;
        }
        public override string Type => "dragonHatched";
        public string DragonUid { get; }
        public string SpeciesId { get; }
        public MutationType Mutation { get; }
        public DragonLocation Location { get; }
        public bool Tutorial { get; }
    }

    public sealed class AchievementUnlocked : DomainEvent
    {
        public AchievementUnlocked(string achievementId, long rewardGoldMicro) { AchievementId = achievementId; RewardGoldMicro = rewardGoldMicro; }
        public override string Type => "achievementUnlocked";
        public string AchievementId { get; }
        /// <summary>Đã cộng vào goldMicro (không vào totalGoldEarnedMicro).</summary>
        public long RewardGoldMicro { get; }
    }

    public sealed class TutorialStepCompleted : DomainEvent
    {
        public TutorialStepCompleted(string stepId) { StepId = stepId; }
        public override string Type => "tutorialStepCompleted";
        public string StepId { get; }
    }

    public sealed class TutorialStatusChanged : DomainEvent
    {
        public TutorialStatusChanged(TutorialStatus status) { Status = status; }
        public override string Type => "tutorialStatusChanged";
        public TutorialStatus Status { get; }
    }

    // ---- U2.5: tổ ấp (implementation mới, không có ở TS b33ec7a) ----

    /// <summary>Bắt đầu lai: phí đã trừ, kết quả đã roll và lưu trong tổ (không công bố loài/biến thể trước khi nở).</summary>
    public sealed class BreedStarted : DomainEvent
    {
        public BreedStarted(string eggId, string parentA, string parentB, long feeGoldMicro, long finishAtMs) { EggId = eggId; ParentA = parentA; ParentB = parentB; FeeGoldMicro = feeGoldMicro; FinishAtMs = finishAtMs; }
        public override string Type => "breedStarted";
        public string EggId { get; }
        public string ParentA { get; }
        public string ParentB { get; }
        public long FeeGoldMicro { get; }
        public long FinishAtMs { get; }
    }

    public sealed class EggTapped : DomainEvent
    {
        public EggTapped(string eggId, long reducedMs, long finishAtMs) { EggId = eggId; ReducedMs = reducedMs; FinishAtMs = finishAtMs; }
        public override string Type => "eggTapped";
        public string EggId { get; }
        public long ReducedMs { get; }
        public long FinishAtMs { get; }
    }

    /// <summary>Ấp xong: bố mẹ được giải phóng và vào cooldown 10 phút, kể cả khi trứng còn chờ nhận do kho đầy.</summary>
    public sealed class EggReady : DomainEvent
    {
        public EggReady(string eggId, long atMs) { EggId = eggId; AtMs = atMs; }
        public override string Type => "eggReady";
        public string EggId { get; }
        public long AtMs { get; }
    }

    // ---- U2.7: khu bảo tồn (implementation mới) ----

    public sealed class DragonSold : DomainEvent
    {
        public DragonSold(string dragonUid, string speciesId, long priceGoldMicro) { DragonUid = dragonUid; SpeciesId = speciesId; PriceGoldMicro = priceGoldMicro; }
        public override string Type => "dragonSold";
        public string DragonUid { get; }
        public string SpeciesId { get; }
        /// <summary>Cộng vào goldMicro, KHÔNG vào totalGoldEarnedMicro (GDD Phụ lục F).</summary>
        public long PriceGoldMicro { get; }
    }

    public sealed class DragonLockChanged : DomainEvent
    {
        public DragonLockChanged(string dragonUid, bool locked) { DragonUid = dragonUid; Locked = locked; }
        public override string Type => "dragonLockChanged";
        public string DragonUid { get; }
        public bool Locked { get; }
    }

    public sealed class UpgradeBought : DomainEvent
    {
        public UpgradeBought(string upgradeId, int level, long costGoldMicro) { UpgradeId = upgradeId; Level = level; CostGoldMicro = costGoldMicro; }
        public override string Type => "upgradeBought";
        public string UpgradeId { get; }
        /// <summary>Cấp mới sau khi mua.</summary>
        public int Level { get; }
        public long CostGoldMicro { get; }
    }

    public sealed class DecorationBought : DomainEvent
    {
        public DecorationBought(string zoneId, string decorationId, long priceGoldMicro) { ZoneId = zoneId; DecorationId = decorationId; PriceGoldMicro = priceGoldMicro; }
        public override string Type => "decorationBought";
        public string ZoneId { get; }
        public string DecorationId { get; }
        public long PriceGoldMicro { get; }
    }

    public sealed class DecorationToggled : DomainEvent
    {
        public DecorationToggled(string zoneId, string decorationId, bool active) { ZoneId = zoneId; DecorationId = decorationId; Active = active; }
        public override string Type => "decorationToggled";
        public string ZoneId { get; }
        public string DecorationId { get; }
        public bool Active { get; }
    }

    /// <summary>Ô Bách khoa mới (loài × biến thể) được ghi nhận.</summary>
    public sealed class CodexDiscovered : DomainEvent
    {
        public CodexDiscovered(string speciesId, MutationType mutation) { SpeciesId = speciesId; Mutation = mutation; }
        public override string Type => "codexDiscovered";
        public string SpeciesId { get; }
        public MutationType Mutation { get; }
    }

    // ---- U2.8: thế giới và thám hiểm (implementation mới) ----

    public sealed class ZoneUnlocked : DomainEvent
    {
        public ZoneUnlocked(string zoneId) { ZoneId = zoneId; }
        public override string Type => "zoneUnlocked";
        public string ZoneId { get; }
    }

    public sealed class ExpeditionStarted : DomainEvent
    {
        public ExpeditionStarted(string expeditionId, string routeId, string dragonUid, long finishAtMs) { ExpeditionId = expeditionId; RouteId = routeId; DragonUid = dragonUid; FinishAtMs = finishAtMs; }
        public override string Type => "expeditionStarted";
        public string ExpeditionId { get; }
        public string RouteId { get; }
        public string DragonUid { get; }
        public long FinishAtMs { get; }
    }

    /// <summary>Chuyến đi xong (kể cả offline): rồng về idle đúng tại finishAtMs; thưởng chờ nhận.</summary>
    public sealed class ExpeditionReady : DomainEvent
    {
        public ExpeditionReady(string expeditionId, long atMs) { ExpeditionId = expeditionId; AtMs = atMs; }
        public override string Type => "expeditionReady";
        public string ExpeditionId { get; }
        public long AtMs { get; }
    }

    public sealed class ExpeditionClaimed : DomainEvent
    {
        public ExpeditionClaimed(string expeditionId, IReadOnlyList<KeyValuePair<string, int>> rewards) { ExpeditionId = expeditionId; Rewards = rewards; }
        public override string Type => "expeditionClaimed";
        public string ExpeditionId { get; }
        /// <summary>Vật liệu → số lượng đã cộng vào inventory.</summary>
        public IReadOnlyList<KeyValuePair<string, int>> Rewards { get; }
    }

    public sealed class QuestCompleted : DomainEvent
    {
        public QuestCompleted(string questId) { QuestId = questId; }
        public override string Type => "questCompleted";
        public string QuestId { get; }
    }

    /// <summary>Trứng thưởng cố định đã cấp. Delivered = true nếu đã vào pendingPack ngay; false nếu đang chờ (kho đầy hoặc có trứng chờ nhận).</summary>
    public sealed class RewardEggGranted : DomainEvent
    {
        public RewardEggGranted(string rewardId, string speciesId, bool delivered) { RewardId = rewardId; SpeciesId = speciesId; Delivered = delivered; }
        public override string Type => "rewardEggGranted";
        public string RewardId { get; }
        public string SpeciesId { get; }
        public bool Delivered { get; }
    }

    public sealed class RewardEggDelivered : DomainEvent
    {
        public RewardEggDelivered(string rewardId, string packId) { RewardId = rewardId; PackId = packId; }
        public override string Type => "rewardEggDelivered";
        public string RewardId { get; }
        public string PackId { get; }
    }
}
