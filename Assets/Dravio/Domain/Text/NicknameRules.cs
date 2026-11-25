using System;
using System.Text;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;

namespace Dravio.Domain.Text
{
    public readonly struct NicknameResult
    {
        private NicknameResult(bool ok, string value, DomainErrorCode code) { Ok = ok; Value = value; ErrorCode = code; }
        public bool Ok { get; }
        /// <summary>Tên đã chuẩn hóa khi Ok.</summary>
        public string Value { get; }
        /// <summary>name-empty | name-too-long | name-invalid-chars khi không Ok.</summary>
        public DomainErrorCode ErrorCode { get; }
        internal static NicknameResult Success(string v) => new NicknameResult(true, v, default);
        internal static NicknameResult Fail(DomainErrorCode c) => new NicknameResult(false, null, c);
    }

    /// <summary>
    /// Chuẩn hóa và kiểm tên rồng (src/engine/inventory/nickname.ts; ADR 0004 mục 8):
    /// NFC → trim kiểu JavaScript → cấm ký tự điều khiển (Cc), bidi (U+202A–202E, U+2066–2069, U+200E, U+200F)
    /// và U+FFFD → 1–24 grapheme. Tên được lưu nguyên văn; lớp UI phải hiển thị như text thuần, không rich text.
    /// </summary>
    public static class NicknameRules
    {
        public static NicknameResult Normalize(string raw)
        {
            if (raw == null) throw new ArgumentNullException(nameof(raw));
            string value = JsCompat.Trim(NormalizeNfc(raw));
            if (ContainsForbidden(value)) return NicknameResult.Fail(DomainErrorCode.NameInvalidChars);
            int n = GraphemeCounter.Count(value);
            if (n == 0) return NicknameResult.Fail(DomainErrorCode.NameEmpty);
            if (n > CareRules.NicknameMaxGraphemes) return NicknameResult.Fail(DomainErrorCode.NameTooLong);
            return NicknameResult.Success(value);
        }

        public static bool ContainsForbidden(string s)
        {
            foreach (char c in s)
            {
                if (char.IsControl(c)) return true;
                if ((c >= '\u202A' && c <= '\u202E') || (c >= '\u2066' && c <= '\u2069') || c == '\u200E' || c == '\u200F' || c == '\uFFFD') return true;
            }
            return false;
        }

        /// <summary>
        /// String.prototype.normalize('NFC'). .NET ném lỗi với surrogate lẻ còn JS giữ nguyên chúng,
        /// nên chuẩn hóa từng đoạn hợp lệ và giữ surrogate lẻ tại chỗ.
        /// </summary>
        public static string NormalizeNfc(string s)
        {
            int bad = IndexOfLoneSurrogate(s, 0);
            if (bad < 0) return s.Normalize(NormalizationForm.FormC);
            var sb = new StringBuilder(s.Length);
            int start = 0;
            while (bad >= 0)
            {
                if (bad > start) sb.Append(s.Substring(start, bad - start).Normalize(NormalizationForm.FormC));
                sb.Append(s[bad]);
                start = bad + 1;
                bad = IndexOfLoneSurrogate(s, start);
            }
            if (start < s.Length) sb.Append(s.Substring(start).Normalize(NormalizationForm.FormC));
            return sb.ToString();
        }

        private static int IndexOfLoneSurrogate(string s, int from)
        {
            for (int i = from; i < s.Length; i++)
            {
                if (char.IsHighSurrogate(s[i]))
                {
                    if (i + 1 < s.Length && char.IsLowSurrogate(s[i + 1])) { i++; continue; }
                    return i;
                }
                if (char.IsLowSurrogate(s[i])) return i;
            }
            return -1;
        }
    }
}
