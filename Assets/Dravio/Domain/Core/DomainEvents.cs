namespace Dravio.Domain.Core
{
    /// <summary>
    /// Domain event do engine phát sau mỗi lệnh thành công (src/engine/core/events.ts).
    /// Lớp trình bày (Unity) chỉ đọc, không suy diễn lại luật. U2.1 port 9 loại event của các lệnh trong phạm vi.
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
}
