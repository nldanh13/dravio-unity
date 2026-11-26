using System;
using System.Globalization;
using Dravio.Domain.Core;

namespace Dravio.Domain
{
    /// <summary>
    /// Số tiền Gold (G) duy nhất của Dravio, lưu bằng microG nguyên: 1 G = 1 000 000 microG
    /// (GDD 3.1 trang 15; CLAUDE.md "Tiền tính bằng microG nguyên"). Không có dấu phẩy động, không làm tròn ngầm.
    /// Mọi phép cộng/trừ đều checked: tràn số là vi phạm bất biến, không bao giờ âm thầm quay vòng.
    /// Phạm vi hợp lệ trong save là 0..2^53−1 (giống Number.isSafeInteger của bản TypeScript).
    /// </summary>
    public readonly struct MicroGold : IEquatable<MicroGold>, IComparable<MicroGold>
    {
        /// <summary>MICRO_PER_G = 1 000 000.</summary>
        public const long MicroPerGold = 1_000_000L;

        public static readonly MicroGold Zero = new MicroGold(0);

        public MicroGold(long micro) { Micro = micro; }

        /// <summary>Giá trị thô tính bằng microG.</summary>
        public long Micro { get; }

        public static MicroGold FromMicro(long micro) => new MicroGold(micro);

        /// <summary>G nguyên → microG (checked).</summary>
        public static MicroGold FromWholeGold(long gold)
        {
            try { return new MicroGold(checked(gold * MicroPerGold)); }
            catch (OverflowException) { throw new DomainInvariantException("MicroGold overflow: " + gold.ToString(CultureInfo.InvariantCulture) + " G"); }
        }

        /// <summary>microToG: floor(micro / 1 000 000), làm tròn xuống cả khi âm (Math.floor của JS).</summary>
        public long WholeGoldFloor
        {
            get
            {
                long q = Micro / MicroPerGold;
                return Micro % MicroPerGold < 0 ? q - 1 : q;
            }
        }

        public bool IsNegative => Micro < 0;

        /// <summary>Nằm trong phạm vi lưu được của save (0..2^53−1).</summary>
        public bool IsValidBalance => Micro >= 0 && JsCompat.IsSafeInteger(Micro);

        public MicroGold Add(MicroGold other)
        {
            try { return new MicroGold(checked(Micro + other.Micro)); }
            catch (OverflowException) { throw new DomainInvariantException("MicroGold overflow on add"); }
        }

        public MicroGold Subtract(MicroGold other)
        {
            try { return new MicroGold(checked(Micro - other.Micro)); }
            catch (OverflowException) { throw new DomainInvariantException("MicroGold overflow on subtract"); }
        }

        public static MicroGold operator +(MicroGold a, MicroGold b) => a.Add(b);
        public static MicroGold operator -(MicroGold a, MicroGold b) => a.Subtract(b);
        public static bool operator ==(MicroGold a, MicroGold b) => a.Micro == b.Micro;
        public static bool operator !=(MicroGold a, MicroGold b) => a.Micro != b.Micro;
        public static bool operator <(MicroGold a, MicroGold b) => a.Micro < b.Micro;
        public static bool operator >(MicroGold a, MicroGold b) => a.Micro > b.Micro;
        public static bool operator <=(MicroGold a, MicroGold b) => a.Micro <= b.Micro;
        public static bool operator >=(MicroGold a, MicroGold b) => a.Micro >= b.Micro;

        public bool Equals(MicroGold other) => Micro == other.Micro;
        public override bool Equals(object obj) => obj is MicroGold other && Equals(other);
        public override int GetHashCode() => Micro.GetHashCode();
        public int CompareTo(MicroGold other) => Micro.CompareTo(other.Micro);

        /// <summary>Dạng gỡ lỗi chính xác, ví dụ "4.706250 G". Không dùng để hiển thị cho người chơi (UI dùng i18n).</summary>
        public override string ToString()
        {
            long abs = Math.Abs(Micro);
            string sign = Micro < 0 ? "-" : string.Empty;
            return sign + (abs / MicroPerGold).ToString(CultureInfo.InvariantCulture) + "." +
                   (abs % MicroPerGold).ToString("D6", CultureInfo.InvariantCulture) + " G";
        }
    }
}
