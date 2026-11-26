using System.Collections.Generic;
using Dravio.Domain.Catalog;
using Dravio.Domain.World;

namespace Dravio.Domain.Core
{
    /// <summary>
    /// Lệnh gửi vào <see cref="CommandDispatcher"/> (src/engine/core/commands.ts, phạm vi U2.1).
    /// Mọi lệnh có RequestId để chống xử lý trùng. UI chỉ gửi lệnh, không tự đổi state.
    /// </summary>
    public abstract class GameCommand
    {
        protected GameCommand(string requestId) { RequestId = requestId; }

        /// <summary>1–64 ký tự (đơn vị UTF-16, giống String.length của JS).</summary>
        public string RequestId { get; }

        /// <summary>Tên lệnh giống trường <c>type</c> của TypeScript.</summary>
        public abstract string Type { get; }

        /// <summary>Kiểm hình dạng lệnh ở ranh giới (parseCommand): enum, ID, độ dài.</summary>
        internal abstract bool IsWellFormed();

        internal static bool IsId(string v) => v != null && v.Length > 0 && v.Length <= 64;
    }

    /// <summary>Quyết toán thời gian tới "bây giờ": XP, vàng theo stage, receipt offline.</summary>
    public sealed class AdvanceTimeCommand : GameCommand
    {
        public AdvanceTimeCommand(string requestId, AdvanceMode mode) : base(requestId) { Mode = mode; }
        public AdvanceMode Mode { get; }
        public override string Type => "advanceTime";
        internal override bool IsWellFormed() => WireNames.IsDefined(Mode);
    }

    /// <summary>
    /// Người chơi thả một viên thức ăn vào microhabitat (chưa cho XP; XP đến từ <see cref="ConsumeFoodCommand"/>).
    /// <para>
    /// U2.15 (luật cho ăn B, issue #74): <see cref="TargetDragonUid"/> tùy chọn = rồng người chơi đang chọn. Phần ăn được giao cho rồng đó trước nếu nó
    /// ăn được (<see cref="Behavior.BehaviorEngine.CanEat"/>: ở đúng microhabitat, rảnh, hết hồi 8 s, đúng chế độ ăn); không thì luật A hiện tại
    /// (rồng thích loại đó, rồi UID nhỏ). Mục tiêu không tồn tại / bận / sai chỗ KHÔNG làm lệnh bị từ chối: lệnh vẫn thả phần ăn và dùng luật A.
    /// null (mặc định, mọi lệnh cũ) = luật A đúng như U2.14 và TS b33ec7a. Mục tiêu chỉ sống trong bộ nhớ của phần ăn (≤ 12 s), không vào save (Schema v10).
    /// </para>
    /// </summary>
    public sealed class FeedCommand : GameCommand
    {
        public FeedCommand(string requestId, DietId dietId, string zoneId, string habitatId) : this(requestId, dietId, zoneId, habitatId, null) { }

        public FeedCommand(string requestId, DietId dietId, string zoneId, string habitatId, string targetDragonUid) : base(requestId)
        {
            DietId = dietId; ZoneId = zoneId; HabitatId = habitatId; TargetDragonUid = targetDragonUid;
        }
        public DietId DietId { get; }
        public string ZoneId { get; }
        public string HabitatId { get; }
        /// <summary>U2.15: rồng được chọn (ưu tiên ăn). null = không ưu tiên (luật A). Nếu có phải là ID hợp lệ (1–64 ký tự), không thì lệnh sai dạng.</summary>
        public string TargetDragonUid { get; }
        public override string Type => "feed";
        internal override bool IsWellFormed()
        {
            bool diet = false;
            foreach (DietId d in CareRules.DietIds) if (d == DietId) diet = true;
            return diet && IsId(ZoneId) && IsId(HabitatId) && (TargetDragonUid == null || IsId(TargetDragonUid));
        }
    }

    /// <summary>
    /// Engine xác nhận một rồng ăn viên thức ăn — nửa sau của luồng cho ăn (ADR 0004 mục 6). Do mô phỏng/va chạm phát,
    /// không phải UI tự báo XP.
    /// </summary>
    public sealed class ConsumeFoodCommand : GameCommand
    {
        public ConsumeFoodCommand(string requestId, string foodId, string dragonUid) : base(requestId) { FoodId = foodId; DragonUid = dragonUid; }
        public string FoodId { get; }
        public string DragonUid { get; }
        public override string Type => "consumeFood";
        internal override bool IsWellFormed() => IsId(FoodId) && IsId(DragonUid);
    }

    public sealed class PetCommand : GameCommand
    {
        public PetCommand(string requestId, string dragonUid) : base(requestId) { DragonUid = dragonUid; }
        public string DragonUid { get; }
        public override string Type => "pet";
        internal override bool IsWellFormed() => IsId(DragonUid);
    }

    public sealed class RenameDragonCommand : GameCommand
    {
        /// <param name="nickname">null xóa tên; chuỗi thô tối đa 1024 đơn vị UTF-16 trước chuẩn hóa.</param>
        public RenameDragonCommand(string requestId, string dragonUid, string nickname) : base(requestId) { DragonUid = dragonUid; Nickname = nickname; }
        public string DragonUid { get; }
        public string Nickname { get; }
        public override string Type => "renameDragon";
        internal override bool IsWellFormed() => IsId(DragonUid) && (Nickname?.Length ?? 0) <= 1024;
    }

    /// <summary>Chuyển rồng tới microhabitat đã mở (kiểm sinh học + sức chứa) hoặc vào kho an toàn (ADR 0008).</summary>
    public sealed class MoveDragonCommand : GameCommand
    {
        public MoveDragonCommand(string requestId, string dragonUid, MoveTarget target) : base(requestId) { DragonUid = dragonUid; Target = target; }
        public string DragonUid { get; }
        public MoveTarget Target { get; }
        public override string Type => "moveDragon";
        internal override bool IsWellFormed() =>
            IsId(DragonUid) && Target != null &&
            (Target.Kind == LocationKind.Storage || (Target.Kind == LocationKind.Zone && IsId(Target.ZoneId) && IsId(Target.HabitatId)));
    }

    /// <summary>Chọn/bỏ rồng khỏi ngân sách sản xuất (5 + 2 × cấp, tối đa 25).</summary>
    public sealed class SetProductionSlotCommand : GameCommand
    {
        public SetProductionSlotCommand(string requestId, string dragonUid, bool enabled) : base(requestId) { DragonUid = dragonUid; Enabled = enabled; }
        public string DragonUid { get; }
        public bool Enabled { get; }
        public override string Type => "setProductionSlot";
        internal override bool IsWellFormed() => IsId(DragonUid);
    }

    /// <summary>Mở microhabitat miễn phí khi đã có khám phá điều kiện (ADR 0009).</summary>
    public sealed class UnlockHabitatCommand : GameCommand
    {
        public UnlockHabitatCommand(string requestId, string habitatId) : base(requestId) { HabitatId = habitatId; }
        public string HabitatId { get; }
        public override string Type => "unlockHabitat";
        internal override bool IsWellFormed() => IsId(HabitatId);
    }

    /// <summary>Khảo sát biển khám phá của microhabitat chưa mở để ghi nhận khám phá (ADR 0013).</summary>
    public sealed class SurveyHabitatCommand : GameCommand
    {
        public SurveyHabitatCommand(string requestId, string habitatId) : base(requestId) { HabitatId = habitatId; }
        public string HabitatId { get; }
        public override string Type => "surveyHabitat";
        internal override bool IsWellFormed() => IsId(HabitatId);
    }

    /// <summary>Mở hồ sơ sinh học của một loài đang nuôi (TUT_03).</summary>
    public sealed class ViewBiologyProfileCommand : GameCommand
    {
        public ViewBiologyProfileCommand(string requestId, string speciesId) : base(requestId) { SpeciesId = speciesId; }
        public string SpeciesId { get; }
        public override string Type => "viewBiologyProfile";
        internal override bool IsWellFormed() => IsId(SpeciesId);
    }

    /// <summary>Mua gói Starter hướng dẫn: 100 G, một lần mỗi save, kết quả cố định Sproutling Normal (ADR 0013).</summary>
    public sealed class BuyTutorialPackCommand : GameCommand
    {
        public BuyTutorialPackCommand(string requestId) : base(requestId) { }
        public override string Type => "buyTutorialPack";
        internal override bool IsWellFormed() => true;
    }

    /// <summary>Nhận trứng đang chờ: nở và đặt vào microhabitat hợp lệ hoặc kho.</summary>
    public sealed class ClaimPackCommand : GameCommand
    {
        public ClaimPackCommand(string requestId, MoveTarget target) : base(requestId) { Target = target; }
        public MoveTarget Target { get; }
        public override string Type => "claimPack";
        internal override bool IsWellFormed() =>
            Target != null && (Target.Kind == LocationKind.Storage || (Target.Kind == LocationKind.Zone && IsId(Target.ZoneId) && IsId(Target.HabitatId)));
    }

    /// <summary>Bỏ qua (skipped) hoặc xem lại (active) hướng dẫn. Completed không đặt được bằng lệnh.</summary>
    public sealed class SetTutorialStatusCommand : GameCommand
    {
        public SetTutorialStatusCommand(string requestId, TutorialStatus status) : base(requestId) { Status = status; }
        public TutorialStatus Status { get; }
        public override string Type => "setTutorialStatus";
        internal override bool IsWellFormed() => Status == TutorialStatus.Active || Status == TutorialStatus.Skipped;
    }

    // ---- U2.5: implementation mới theo GDD 3.1 / ADR 0003 (TS b33ec7a chưa có các lệnh này) ----

    /// <summary>Mua một gói trứng (starter / elemental / mystic / astral) bằng vàng; roll một lần và lưu vào pendingPack.</summary>
    public sealed class BuyPackCommand : GameCommand
    {
        public BuyPackCommand(string requestId, string packId) : base(requestId) { PackId = packId; }
        public string PackId { get; }
        public override string Type => "buyPack";
        internal override bool IsWellFormed() => IsId(PackId);
    }

    /// <summary>Bắt đầu lai hai rồng (startBreed, GDD Phụ lục C.3).</summary>
    public sealed class StartBreedCommand : GameCommand
    {
        public StartBreedCommand(string requestId, string parentA, string parentB) : base(requestId) { ParentA = parentA; ParentB = parentB; }
        public string ParentA { get; }
        public string ParentB { get; }
        public override string Type => "startBreed";
        internal override bool IsWellFormed() => IsId(ParentA) && IsId(ParentB);
    }

    /// <summary>Gõ tổ: −3 s, cách nhau ≥ 2 s, tổng ≤ 10 % thời lượng (tapEgg).</summary>
    public sealed class TapEggCommand : GameCommand
    {
        public TapEggCommand(string requestId) : base(requestId) { }
        public override string Type => "tapEgg";
        internal override bool IsWellFormed() => true;
    }

    /// <summary>Nhận rồng con từ tổ đã ấp xong (claimBreed).</summary>
    public sealed class ClaimBreedCommand : GameCommand
    {
        public ClaimBreedCommand(string requestId, MoveTarget target) : base(requestId) { Target = target; }
        public MoveTarget Target { get; }
        public override string Type => "claimBreed";
        internal override bool IsWellFormed() =>
            Target != null && (Target.Kind == LocationKind.Storage || (Target.Kind == LocationKind.Zone && IsId(Target.ZoneId) && IsId(Target.HabitatId)));
    }

    // ---- U2.7: implementation mới theo GDD 3.1 (Phụ lục C.3, E, F) — TS b33ec7a chỉ có hàm thuần sellPriceG / upgradeCostG ----

    /// <summary>Bán một rồng (sellDragon): giá = floor(baseSell × stageSell × mutationSell) tại thời điểm bán.</summary>
    public sealed class SellDragonCommand : GameCommand
    {
        public SellDragonCommand(string requestId, string dragonUid) : base(requestId) { DragonUid = dragonUid; }
        public string DragonUid { get; }
        public override string Type => "sellDragon";
        internal override bool IsWellFormed() => IsId(DragonUid);
    }

    /// <summary>
    /// Bán nhanh mọi rồng Common Adult chưa khóa, không bận, chừa một rồng (bulkSell, GDD Phụ lục F). <see cref="ExpectedTotalGoldMicro"/> là tổng
    /// người chơi đã xác nhận trên UI; nếu sau quyết toán tổng khác thì từ chối (quote-changed), không bán gì.
    /// </summary>
    public sealed class BulkSellCommand : GameCommand
    {
        public BulkSellCommand(string requestId, long expectedTotalGoldMicro) : base(requestId) { ExpectedTotalGoldMicro = expectedTotalGoldMicro; }
        public long ExpectedTotalGoldMicro { get; }
        public override string Type => "bulkSell";
        internal override bool IsWellFormed() => ExpectedTotalGoldMicro > 0;
    }

    /// <summary>Khóa/mở khóa chống bán rồng yêu thích (lockDragon). Rồng bị khóa vẫn lai được (ADR 0003).</summary>
    public sealed class LockDragonCommand : GameCommand
    {
        public LockDragonCommand(string requestId, string dragonUid, bool locked) : base(requestId) { DragonUid = dragonUid; Locked = locked; }
        public string DragonUid { get; }
        public bool Locked { get; }
        public override string Type => "lockDragon";
        internal override bool IsWellFormed() => IsId(DragonUid);
    }

    /// <summary>Mua một cấp nâng cấp (buyUpgrade): cost = floor(base × scale^level), trừ vàng và tăng cấp cùng transaction (GDD Phụ lục E).</summary>
    public sealed class BuyUpgradeCommand : GameCommand
    {
        public BuyUpgradeCommand(string requestId, string upgradeId) : base(requestId) { UpgradeId = upgradeId; }
        public string UpgradeId { get; }
        public override string Type => "buyUpgrade";
        internal override bool IsWellFormed() => IsId(UpgradeId);
    }

    /// <summary>Mua một món trang trí cho một khu (buyDecoration): mỗi món một bản sao theo khu, không bán lại.</summary>
    public sealed class BuyDecorationCommand : GameCommand
    {
        public BuyDecorationCommand(string requestId, string zoneId, string decorationId) : base(requestId) { ZoneId = zoneId; DecorationId = decorationId; }
        public string ZoneId { get; }
        public string DecorationId { get; }
        public override string Type => "buyDecoration";
        internal override bool IsWellFormed() => IsId(ZoneId) && IsId(DecorationId);
    }

    /// <summary>Bật/tắt món trang trí đã sở hữu (toggleDecoration), miễn phí.</summary>
    public sealed class ToggleDecorationCommand : GameCommand
    {
        public ToggleDecorationCommand(string requestId, string zoneId, string decorationId, bool active) : base(requestId) { ZoneId = zoneId; DecorationId = decorationId; Active = active; }
        public string ZoneId { get; }
        public string DecorationId { get; }
        public bool Active { get; }
        public override string Type => "toggleDecoration";
        internal override bool IsWellFormed() => IsId(ZoneId) && IsId(DecorationId);
    }

    // ---- U2.8: thám hiểm (implementation mới, GDD tr. 22; Phụ lục C.3 startExpedition / claimExpedition) ----

    public sealed class StartExpeditionCommand : GameCommand
    {
        public StartExpeditionCommand(string requestId, string routeId, string dragonUid) : base(requestId) { RouteId = routeId; DragonUid = dragonUid; }
        public string RouteId { get; }
        public string DragonUid { get; }
        public override string Type => "startExpedition";
        internal override bool IsWellFormed() => IsId(RouteId) && IsId(DragonUid);
    }

    /// <summary>Mở một thế giới khi đủ điều kiện (ADR U2.8-0201); trao trứng cố định một lần (vào pendingPack hoặc hàng chờ).</summary>
    public sealed class UnlockZoneCommand : GameCommand
    {
        public UnlockZoneCommand(string requestId, string zoneId) : base(requestId) { ZoneId = zoneId; }
        public string ZoneId { get; }
        public override string Type => "unlockZone";
        internal override bool IsWellFormed() => IsId(ZoneId);
    }

    public sealed class ClaimExpeditionCommand : GameCommand
    {
        public ClaimExpeditionCommand(string requestId) : base(requestId) { }
        public override string Type => "claimExpedition";
        internal override bool IsWellFormed() => true;
    }

    /// <summary>Kết quả dispatch. Khi thất bại, <see cref="State"/> là chính object state đầu vào (không đổi gì).</summary>
    // ---- U2.6: chăm sóc / cứu hộ / trú ẩn (implementation mới, GDD Phụ lục C.3 "Care / rescue" và "moveToShelter") ----

    /// <summary>Đưa rồng vào nơi trú của zone trong lúc có cảnh báo thiên tai.</summary>
    public sealed class MoveToShelterCommand : GameCommand
    {
        public MoveToShelterCommand(string requestId, string dragonUid) : base(requestId) { DragonUid = dragonUid; }
        public string DragonUid { get; }
        public override string Type => "moveToShelter";
        internal override bool IsWellFormed() => IsId(DragonUid);
    }

    public abstract class CaseCommand : GameCommand
    {
        protected CaseCommand(string requestId, string caseId) : base(requestId) { CaseId = caseId; }
        public string CaseId { get; }
        internal override bool IsWellFormed() => IsId(CaseId);
    }

    /// <summary>Nhận định: hiện triệu chứng giả tưởng. Bắt buộc trước setCarePlan / startCare.</summary>
    public sealed class AssessCaseCommand : CaseCommand
    {
        public AssessCaseCommand(string requestId, string caseId) : base(requestId, caseId) { }
        public override string Type => "assessCase";
    }

    public sealed class SetCarePlanCommand : CaseCommand
    {
        public SetCarePlanCommand(string requestId, string caseId, string planId) : base(requestId, caseId) { PlanId = planId; }
        public string PlanId { get; }
        public override string Type => "setCarePlan";
        internal override bool IsWellFormed() => IsId(CaseId) && IsId(PlanId);
    }

    public sealed class StartCareCommand : CaseCommand
    {
        public StartCareCommand(string requestId, string caseId) : base(requestId, caseId) { }
        public override string Type => "startCare";
    }

    /// <summary>Lượng giá sau khi đủ thời lượng; điều kiện để xuất viện.</summary>
    public sealed class ReviewCaseCommand : CaseCommand
    {
        public ReviewCaseCommand(string requestId, string caseId) : base(requestId, caseId) { }
        public override string Type => "reviewCase";
    }

    /// <summary>Xuất viện về kho an toàn hoặc microhabitat hợp lệ còn chỗ.</summary>
    public sealed class DischargeDragonCommand : CaseCommand
    {
        public DischargeDragonCommand(string requestId, string caseId, MoveTarget target) : base(requestId, caseId) { Target = target; }
        public MoveTarget Target { get; }
        public override string Type => "dischargeDragon";
        internal override bool IsWellFormed() =>
            IsId(CaseId) && Target != null && (Target.Kind == LocationKind.Storage || (Target.Kind == LocationKind.Zone && IsId(Target.ZoneId) && IsId(Target.HabitatId)));
    }

    public sealed class DispatchResult
    {
        private DispatchResult(bool ok, GameState state, IReadOnlyList<DomainEvent> events, bool duplicate, DomainError error)
        {
            Ok = ok; State = state; Events = events; Duplicate = duplicate; Error = error;
        }

        public bool Ok { get; }
        public GameState State { get; }
        public IReadOnlyList<DomainEvent> Events { get; }
        /// <summary>requestId đã xử lý trước đó: trả lại state hiện tại, không event, không tăng revision.</summary>
        public bool Duplicate { get; }
        public DomainError Error { get; }

        internal static DispatchResult Success(GameState s, IReadOnlyList<DomainEvent> e, bool duplicate) => new DispatchResult(true, s, e, duplicate, null);
        internal static DispatchResult Failure(GameState s, DomainError error) => new DispatchResult(false, s, System.Array.Empty<DomainEvent>(), false, error);
    }
}
