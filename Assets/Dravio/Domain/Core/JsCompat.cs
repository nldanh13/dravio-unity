using System;
using System.Globalization;

namespace Dravio.Domain.Core
{
    /// <summary>
    /// Hành vi số học/chuỗi của JavaScript mà engine TypeScript gốc dựa vào. Tách riêng để
    /// mọi chỗ cần tương đương bit-by-bit với bản TS đều đi qua một nơi đã kiểm thử.
    /// </summary>
    public static class JsCompat
    {
        /// <summary>Number.MAX_SAFE_INTEGER (2^53 − 1). Save JSON không biểu diễn chính xác số lớn hơn.</summary>
        public const long MaxSafeInteger = 9007199254740991L;

        /// <summary>Tương đương Number.isSafeInteger cho giá trị đã là số nguyên.</summary>
        public static bool IsSafeInteger(long value) => value >= -MaxSafeInteger && value <= MaxSafeInteger;

        /// <summary>
        /// Math.round của JS: làm tròn nửa lên phía +∞ (khác Math.Round mặc định kiểu banker của .NET).
        /// Chính xác cả với 0.49999999999999994 (không dùng floor(x + 0.5)).
        /// </summary>
        public static double Round(double x)
        {
            if (double.IsNaN(x) || double.IsInfinity(x)) return x;
            double f = Math.Floor(x);
            return x - f >= 0.5 ? f + 1 : f;
        }

        /// <summary>new Date(ms).toISOString().slice(0, 10) — ngày UTC dạng YYYY-MM-DD.</summary>
        public static string UtcDay(long nowMs)
        {
            DateTime utc = DateTimeOffset.FromUnixTimeMilliseconds(nowMs).UtcDateTime;
            return utc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        /// <summary>Kiểm tra mẫu /^\d{4}-\d{2}-\d{2}$/ của JS (\d chỉ là chữ số ASCII).</summary>
        public static bool IsUtcDayPattern(string s)
        {
            if (s == null || s.Length != 10) return false;
            for (int i = 0; i < 10; i++)
            {
                char c = s[i];
                if (i == 4 || i == 7)
                {
                    if (c != '-') return false;
                }
                else if (c < '0' || c > '9')
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// String.prototype.trim của JS: bỏ WhiteSpace (TAB, VT, FF, SP, NBSP, ZWNBSP, Zs) và
        /// LineTerminator (LF, CR, LS, PS). Khác string.Trim() của .NET (.NET bỏ U+0085, giữ U+FEFF).
        /// </summary>
        public static string Trim(string s)
        {
            int start = 0;
            int end = s.Length - 1;
            while (start <= end && IsJsWhitespace(s[start])) start++;
            while (end >= start && IsJsWhitespace(s[end])) end--;
            return s.Substring(start, end - start + 1);
        }

        public static bool IsJsWhitespace(char c)
        {
            switch (c)
            {
                case '\u0009': case '\u000A': case '\u000B': case '\u000C': case '\u000D':
                case ' ': case '\u00A0': case '\u1680': case '\u2028': case '\u2029':
                case '\u202F': case '\u205F': case '\u3000': case '\uFEFF':
                    return true;
                default:
                    return c >= '\u2000' && c <= '\u200A';
            }
        }
    }
}
