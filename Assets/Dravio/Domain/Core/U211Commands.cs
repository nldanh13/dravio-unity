namespace Dravio.Domain.Core
{
    // U2.11 (ADR 0206): lệnh mới. Không đổi lệnh cũ.

    /// <summary>
    /// Nhận hoàn thành một nhiệm vụ đủ yêu cầu (cổng hoặc phụ). Engine kiểm lại mọi yêu cầu sau khi quyết toán thời gian, trừ vật liệu giao, ghi nhiệm vụ.
    /// Không mở thế giới (vẫn cần unlockZone), không cho vàng.
    /// </summary>
    public sealed class CompleteQuestCommand : GameCommand
    {
        public CompleteQuestCommand(string requestId, string questId) : base(requestId) { QuestId = questId; }
        public string QuestId { get; }
        public override string Type => "completeQuest";
        internal override bool IsWellFormed() => IsId(QuestId);
    }
}
