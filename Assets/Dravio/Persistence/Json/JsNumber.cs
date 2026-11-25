using System;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace Dravio.Persistence.Json
{
    /// <summary>
    /// Number::toString(10) của ECMAScript (cách JSON.stringify in số), tính chính xác bằng số nguyên lớn:
    /// tìm số chữ số k nhỏ nhất (1..17) mà giá trị làm tròn đúng tới k chữ số vẫn nằm trong khoảng làm tròn của double
    /// (round-half-even khi đọc lại). Không dùng double.ToString/Parse nên kết quả không phụ thuộc runtime
    /// (.NET, Mono của Unity, IL2CPP).
    /// </summary>
    public static class JsNumber
    {
        private const double TwoPow53 = 9007199254740992.0;

        public static string ToJsString(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "null";
            if (v == 0) return "0"; // gồm -0
            if (Math.Abs(v) < TwoPow53 && Math.Floor(v) == v)
                return ((long)v).ToString(CultureInfo.InvariantCulture);
            ShortestDigits(Math.Abs(v), out string digits, out int n);
            return Layout(v < 0, digits, n);
        }

        /// <summary>Chữ số ngắn nhất (không có số 0 cuối) và n sao cho |v| ≈ 0.digits × 10^n.</summary>
        public static void ShortestDigits(double a, out string digits, out int n)
        {
            long bits = BitConverter.DoubleToInt64Bits(a);
            int expField = (int)((bits >> 52) & 0x7FF);
            long frac = bits & 0xFFFFFFFFFFFFFL;
            BigInteger m;
            int e;
            if (expField == 0) { m = frac; e = -1074; }
            else { m = frac | (1L << 52); e = expField - 1075; }
            bool lowerCloser = frac == 0 && expField > 1;
            bool inclusive = m.IsEven;

            // Giá trị và biên viết dưới dạng X / 2^(2-e) khi e < 2, hoặc X × 2^(e-2) khi e ≥ 2 (nhân 4 để biên là số nguyên).
            BigInteger val = m * 4, hi = m * 4 + 2, lo = lowerCloser ? m * 4 - 1 : m * 4 - 2;
            BigInteger den = BigInteger.One;
            if (e - 2 >= 0) { BigInteger f = BigInteger.Pow(2, e - 2); val *= f; hi *= f; lo *= f; }
            else den = BigInteger.Pow(2, 2 - e);

            // n: 10^(n-1) ≤ a < 10^n.
            n = (int)Math.Floor(Math.Log10(a)) + 1;
            while (Compare(val, den, n - 1) < 0) n--;
            while (Compare(val, den, n) >= 0) n++;

            for (int p = 1; p <= 17; p++)
            {
                // c = round_half_even(a × 10^(p-n))
                BigInteger num = val, d = den;
                int shift = p - n;
                if (shift >= 0) num *= BigInteger.Pow(10, shift); else d *= BigInteger.Pow(10, -shift);
                BigInteger q = BigInteger.DivRem(num, d, out BigInteger r);
                int cmp = (r * 2).CompareTo(d);
                if (cmp > 0 || (cmp == 0 && !q.IsEven)) q += 1;
                // Giá trị ứng viên C = q × 10^(n-p) so với lo/hi (cùng mẫu den).
                BigInteger cNum = q, cDen = BigInteger.One;
                if (-shift >= 0) cNum *= BigInteger.Pow(10, -shift); else cDen = BigInteger.Pow(10, shift);
                // So sánh cNum/cDen với lo/den và hi/den.
                BigInteger left = cNum * den;
                int vsLo = left.CompareTo(lo * cDen);
                int vsHi = left.CompareTo(hi * cDen);
                bool ok = inclusive ? vsLo >= 0 && vsHi <= 0 : vsLo > 0 && vsHi < 0;
                if (!ok) continue;
                string s = q.ToString(CultureInfo.InvariantCulture);
                int nn = n;
                if (s.Length > p) { nn += 1; } // làm tròn lên thành 10^p
                digits = s.TrimEnd('0');
                if (digits.Length == 0) digits = "0";
                n = nn;
                return;
            }
            throw new InvalidOperationException("No shortest representation found");
        }

        /// <summary>So sánh val/den với 10^k.</summary>
        private static int Compare(BigInteger val, BigInteger den, int k)
        {
            if (k >= 0) return val.CompareTo(den * BigInteger.Pow(10, k));
            return (val * BigInteger.Pow(10, -k)).CompareTo(den);
        }

        private static string Layout(bool neg, string digits, int n)
        {
            int k = digits.Length;
            var sb = new StringBuilder(26);
            if (neg) sb.Append('-');
            if (k <= n && n <= 21) sb.Append(digits).Append('0', n - k);
            else if (0 < n && n <= 21) sb.Append(digits, 0, n).Append('.').Append(digits, n, k - n);
            else if (-6 < n && n <= 0) sb.Append("0.").Append('0', -n).Append(digits);
            else
            {
                int e = n - 1;
                sb.Append(digits[0]);
                if (k > 1) sb.Append('.').Append(digits, 1, k - 1);
                sb.Append('e').Append(e >= 0 ? '+' : '-').Append(Math.Abs(e).ToString(CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }
    }
}
