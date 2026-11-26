using System.Collections.Generic;
using Dravio.Domain.Behavior;

namespace Dravio.Domain.Core
{
    // U2.10 (ADR 0204, đã duyệt E1, E2, E5, R11): lệnh và event mới. Không đổi lệnh cũ.

    /// <summary>E5: xây dự án Sanctuary kế tiếp (chỉ thẩm mỹ). Giá lấy từ <see cref="Catalog.SanctuaryProjectCatalog"/>.</summary>
    public sealed class BuildSanctuaryProjectCommand : GameCommand
    {
        public BuildSanctuaryProjectCommand(string requestId) : base(requestId) { }
        public override string Type => "buildSanctuaryProject";
        internal override bool IsWellFormed() => true;
    }

    /// <summary>
    /// R9: người chơi quan sát một rồng đang hiện ở sinh cảnh. Engine đọc quyết định của Behavioral Engine tại thời điểm lệnh với pha
    /// ngày đêm mà Sanctuary đang hiển thị, rồi ghi phát hiện theo <see cref="ResearchPacing.Production"/>. Không cho vàng / XP / bond.
    /// </summary>
    public sealed class ObserveDragonCommand : GameCommand
    {
        public ObserveDragonCommand(string requestId, string dragonUid, DayPhase phase) : base(requestId) { DragonUid = dragonUid; Phase = phase; }
        public string DragonUid { get; }
        public DayPhase Phase { get; }
        public override string Type => "observeDragon";
        internal override bool IsWellFormed() => IsId(DragonUid) && System.Enum.IsDefined(typeof(DayPhase), Phase);
    }

    public sealed class SanctuaryProjectBuilt : DomainEvent
    {
        public SanctuaryProjectBuilt(int index, long costGoldMicro, int materialEach) : this(index, costGoldMicro, materialEach, Catalog.SanctuaryProjectCatalog.MaterialIds) { }
        public SanctuaryProjectBuilt(int index, long costGoldMicro, int materialEach, IReadOnlyList<string> materialIds)
        {
            Index = index; CostGoldMicro = costGoldMicro; MaterialEach = materialEach; MaterialIds = materialIds;
        }
        /// <summary>U2.13: vật liệu đã dùng (mỗi ID đúng <see cref="MaterialEach"/> đơn vị).</summary>
        public IReadOnlyList<string> MaterialIds { get; }
        public override string Type => "sanctuaryProjectBuilt";
        /// <summary>Thứ tự dự án vừa xây (0-based).</summary>
        public int Index { get; }
        public long CostGoldMicro { get; }
        /// <summary>Số đơn vị đã dùng của MỖI vật liệu trong <see cref="MaterialIds"/> (U2.10: gỗ và hạt giống).</summary>
        public int MaterialEach { get; }
    }

    public sealed class DragonObserved : DomainEvent
    {
        public DragonObserved(string dragonUid, string speciesId, IReadOnlyList<ResearchFinding> findings, int deferredByDailyLimit)
        { DragonUid = dragonUid; SpeciesId = speciesId; Findings = findings; DeferredByDailyLimit = deferredByDailyLimit; }
        public override string Type => "dragonObserved";
        public string DragonUid { get; }
        public string SpeciesId { get; }
        public IReadOnlyList<ResearchFinding> Findings { get; }
        public int DeferredByDailyLimit { get; }
    }

    /// <summary>R11: nhật ký đêm ghi khi người chơi quay lại sau một đêm offline (chỉ khi có phát hiện mới).</summary>
    public sealed class NightJournalWritten : DomainEvent
    {
        public NightJournalWritten(long sampleAtMs, IReadOnlyList<ResearchFinding> findings, IReadOnlyList<string> speciesIds)
        { SampleAtMs = sampleAtMs; Findings = findings; SpeciesIds = speciesIds; }
        public override string Type => "nightJournalWritten";
        public long SampleAtMs { get; }
        public IReadOnlyList<ResearchFinding> Findings { get; }
        /// <summary>Loài tương ứng từng phát hiện (cùng thứ tự).</summary>
        public IReadOnlyList<string> SpeciesIds { get; }
    }
}
