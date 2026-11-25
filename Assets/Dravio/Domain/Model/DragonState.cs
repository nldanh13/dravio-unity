using System.Collections.Generic;

namespace Dravio.Domain
{
    /// <summary>
    /// Vị trí của rồng (src/types/game.ts Location). Bất biến: tạo bằng các factory.
    /// Zone giữ cả zoneId và habitatId (ADR 0004 mục 9).
    /// </summary>
    public sealed class DragonLocation
    {
        private DragonLocation(LocationKind kind, string zoneId, string habitatId, string caseId)
        {
            Kind = kind; ZoneId = zoneId; HabitatId = habitatId; CaseId = caseId;
        }

        public LocationKind Kind { get; }
        /// <summary>Có với Zone và Shelter.</summary>
        public string ZoneId { get; }
        /// <summary>Có với Zone (optional trong kiểu TS để tương thích; validator yêu cầu có).</summary>
        public string HabitatId { get; }
        /// <summary>Có với Care.</summary>
        public string CaseId { get; }

        public static DragonLocation Zone(string zoneId, string habitatId) => new DragonLocation(LocationKind.Zone, zoneId, habitatId, null);
        public static readonly DragonLocation Storage = new DragonLocation(LocationKind.Storage, null, null, null);
        public static DragonLocation Shelter(string zoneId) => new DragonLocation(LocationKind.Shelter, zoneId, null, null);
        public static DragonLocation Care(string caseId) => new DragonLocation(LocationKind.Care, null, null, caseId);
        public static readonly DragonLocation Released = new DragonLocation(LocationKind.Released, null, null, null);

        public override string ToString() => Kind.ToWire() + (ZoneId != null ? ":" + ZoneId : "") + (HabitatId != null ? "/" + HabitatId : "") + (CaseId != null ? "#" + CaseId : "");
    }

    /// <summary>
    /// Trạng thái một con rồng (src/types/game.ts DragonInstance). Chỉ giữ dữ liệu gốc;
    /// stage, kiểu hình và vàng/giây là giá trị dẫn xuất, không lưu.
    /// Tên thuộc tính ánh xạ 1-1 với trường JSON của Dravio Save Schema (ghi trong ngoặc).
    /// </summary>
    public sealed class DragonState
    {
        /// <summary>(uid) UID cục bộ, 1–64 ký tự.</summary>
        public string Uid { get; set; }
        /// <summary>(speciesId) ID loài ổn định trong SpeciesCatalog.</summary>
        public string SpeciesId { get; set; }
        /// <summary>(nickname) null = chưa đặt tên.</summary>
        public string Nickname { get; set; }
        public Genotype Genotype { get; set; } = new Genotype();
        public MutationType Mutation { get; set; }
        /// <summary>(xpSec) XP tính bằng giây, 0..growthTimeSec; độ phân giải ms như bản TS.</summary>
        public double XpSec { get; set; }
        public bool Locked { get; set; }
        public DragonLocation Location { get; set; } = DragonLocation.Storage;
        public long HatchedAtMs { get; set; }
        public Personality Personality { get; set; }
        /// <summary>(bond) Gắn bó 0–100, số nguyên; không giảm khi offline.</summary>
        public int Bond { get; set; }
        /// <summary>(bondDay) Ngày UTC YYYY-MM-DD của bộ đếm bondDaily.</summary>
        public string BondDay { get; set; }
        /// <summary>(bondDaily) Bond đã nhận trong bondDay (tối đa 10).</summary>
        public int? BondDaily { get; set; }
        public long? LastFeedXpAtMs { get; set; }
        public long? LastPetAtMs { get; set; }
        /// <summary>(parentIds) null hoặc đúng 2 phần tử.</summary>
        public string[] ParentIds { get; set; }
        /// <summary>(grandparentIds) Cache phả hệ, null khi chưa biết (ADR 0003 #4).</summary>
        public List<string> GrandparentIds { get; set; }
        public int Generation { get; set; } = 1;
        public Ancestry Ancestry { get; set; } = Ancestry.Unknown;
        public DragonOrigin Origin { get; set; } = DragonOrigin.Pack;
        public DragonActivity Activity { get; set; } = DragonActivity.Idle;
        public long BreedCooldownUntilMs { get; set; }
        public string RegistryRef { get; set; }

        public DragonState Clone()
        {
            var c = (DragonState)MemberwiseClone();
            c.Genotype = Genotype?.Clone();
            c.ParentIds = ParentIds == null ? null : (string[])ParentIds.Clone();
            c.GrandparentIds = GrandparentIds == null ? null : new List<string>(GrandparentIds);
            return c;
        }
    }
}
