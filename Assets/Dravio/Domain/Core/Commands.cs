using System.Collections.Generic;
using Dravio.Domain.Catalog;

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

    /// <summary>Người chơi thả một viên thức ăn vào microhabitat (chưa cho XP; XP đến từ <see cref="ConsumeFoodCommand"/>).</summary>
    public sealed class FeedCommand : GameCommand
    {
        public FeedCommand(string requestId, DietId dietId, string zoneId, string habitatId) : base(requestId)
        {
            DietId = dietId; ZoneId = zoneId; HabitatId = habitatId;
        }
        public DietId DietId { get; }
        public string ZoneId { get; }
        public string HabitatId { get; }
        public override string Type => "feed";
        internal override bool IsWellFormed()
        {
            bool diet = false;
            foreach (DietId d in CareRules.DietIds) if (d == DietId) diet = true;
            return diet && IsId(ZoneId) && IsId(HabitatId);
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

    /// <summary>Kết quả dispatch. Khi thất bại, <see cref="State"/> là chính object state đầu vào (không đổi gì).</summary>
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
