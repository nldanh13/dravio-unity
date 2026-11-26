using System.Globalization;

namespace Dravio.Domain.Text
{
    /// <summary>
    /// Đếm extended grapheme cluster theo UAX #29 (GB3–GB13, gồm GB9c Indic conjunct và GB11 emoji ZWJ).
    /// Bản TypeScript dùng <c>Intl.Segmenter</c>; StringInfo của Mono/Unity cũ không theo UAX #29 đầy đủ
    /// (ví dụ đếm chuỗi emoji gia đình 👨‍👩‍👧‍👦 thành nhiều ký tự), nên domain tự cài để kết quả độc lập runtime.
    /// Thuộc tính Unicode lấy từ <see cref="CharUnicodeInfo"/> cộng bảng phạm vi rút gọn cho Extended_Pictographic,
    /// Prepend, InCB; khác biệt còn lại được ghi trong README (mục "Giới hạn đã biết").
    /// </summary>
    public static class GraphemeCounter
    {
        private enum Gcb { Other, CR, LF, Control, Extend, ZWJ, RegionalIndicator, Prepend, SpacingMark, L, V, T, LV, LVT }

        public static int Count(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            int count = 0;
            Gcb prev = Gcb.Other;
            int emoji = 0;    // 0: ngoài chuỗi; 1: ExtPict Extend*; 2: ExtPict Extend* ZWJ
            int riRun = 0;    // số Regional Indicator liên tiếp trong cụm hiện tại
            int incb = 0;     // 0: ngoài; 1: Consonant [Extend|Linker]*; 2: … đã có Linker
            bool first = true;
            for (int i = 0; i < s.Length;)
            {
                int cp = char.IsSurrogatePair(s, i) ? char.ConvertToUtf32(s[i], s[i + 1]) : s[i];
                Gcb cur = Classify(s, i, cp);
                bool pict = IsExtendedPictographic(cp);
                bool brk;
                if (first)
                {
                    brk = true;
                }
                else if (prev == Gcb.CR && cur == Gcb.LF) brk = false;                                   // GB3
                else if (prev == Gcb.Control || prev == Gcb.CR || prev == Gcb.LF) brk = true;            // GB4
                else if (cur == Gcb.Control || cur == Gcb.CR || cur == Gcb.LF) brk = true;               // GB5
                else if (prev == Gcb.L && (cur == Gcb.L || cur == Gcb.V || cur == Gcb.LV || cur == Gcb.LVT)) brk = false; // GB6
                else if ((prev == Gcb.LV || prev == Gcb.V) && (cur == Gcb.V || cur == Gcb.T)) brk = false;               // GB7
                else if ((prev == Gcb.LVT || prev == Gcb.T) && cur == Gcb.T) brk = false;                                // GB8
                else if (cur == Gcb.Extend || cur == Gcb.ZWJ) brk = false;                               // GB9
                else if (cur == Gcb.SpacingMark) brk = false;                                            // GB9a
                else if (prev == Gcb.Prepend) brk = false;                                               // GB9b
                else if (incb == 2 && IsInCBConsonant(cp)) brk = false;                                  // GB9c
                else if (emoji == 2 && pict) brk = false;                                                // GB11
                else if (prev == Gcb.RegionalIndicator && cur == Gcb.RegionalIndicator && riRun % 2 == 1) brk = false; // GB12/13
                else brk = true;                                                                         // GB999

                if (brk) { count++; riRun = 0; }

                // Cập nhật trạng thái chuỗi cho lần so sánh tiếp theo.
                if (pict) emoji = 1;
                else if (emoji >= 1 && cur == Gcb.Extend) emoji = 1;
                else if (emoji == 1 && cur == Gcb.ZWJ) emoji = 2;
                else emoji = 0;

                if (IsInCBConsonant(cp)) incb = 1;
                else if (incb >= 1 && IsInCBLinker(cp)) incb = 2;
                else if (incb >= 1 && (cur == Gcb.ZWJ || (cur == Gcb.Extend && !IsInCBLinker(cp)))) { /* giữ nguyên */ }
                else incb = 0;

                riRun = cur == Gcb.RegionalIndicator ? riRun + 1 : 0;
                prev = cur;
                first = false;
                i += cp > 0xFFFF ? 2 : 1;
            }
            return count;
        }

        private static Gcb Classify(string s, int index, int cp)
        {
            if (cp == 0x000D) return Gcb.CR;
            if (cp == 0x000A) return Gcb.LF;
            if (cp == 0x200D) return Gcb.ZWJ;
            if (cp >= 0x1F1E6 && cp <= 0x1F1FF) return Gcb.RegionalIndicator;
            if (IsPrepend(cp)) return Gcb.Prepend;
            // Hangul
            if ((cp >= 0x1100 && cp <= 0x115F) || (cp >= 0xA960 && cp <= 0xA97C)) return Gcb.L;
            if ((cp >= 0x1160 && cp <= 0x11A7) || (cp >= 0xD7B0 && cp <= 0xD7C6)) return Gcb.V;
            if ((cp >= 0x11A8 && cp <= 0x11FF) || (cp >= 0xD7CB && cp <= 0xD7FB)) return Gcb.T;
            if (cp >= 0xAC00 && cp <= 0xD7A3) return (cp - 0xAC00) % 28 == 0 ? Gcb.LV : Gcb.LVT;
            if (IsExtendExtra(cp)) return Gcb.Extend;
            if (cp == 0x0E33 || cp == 0x0EB3) return Gcb.SpacingMark;
            if (cp >= 0xD800 && cp <= 0xDFFF) return Gcb.Control; // surrogate lẻ
            UnicodeCategory cat = CharUnicodeInfo.GetUnicodeCategory(s, index);
            switch (cat)
            {
                case UnicodeCategory.Control:
                case UnicodeCategory.LineSeparator:
                case UnicodeCategory.ParagraphSeparator:
                    return Gcb.Control;
                case UnicodeCategory.Format:
                    return cp == 0x200C ? Gcb.Extend : Gcb.Control;
                case UnicodeCategory.NonSpacingMark:
                case UnicodeCategory.EnclosingMark:
                    return Gcb.Extend;
                case UnicodeCategory.SpacingCombiningMark:
                    return Gcb.SpacingMark;
                default:
                    return Gcb.Other;
            }
        }

        /// <summary>Grapheme_Extend ngoài Mn/Me: ZWNJ, emoji modifier, tag, katakana voiced marks, một số Mc.</summary>
        private static bool IsExtendExtra(int cp) =>
            cp == 0x200C ||
            (cp >= 0x1F3FB && cp <= 0x1F3FF) ||
            (cp >= 0xE0020 && cp <= 0xE007F) ||
            (cp >= 0xFF9E && cp <= 0xFF9F) ||
            cp == 0x09BE || cp == 0x09D7 || cp == 0x0B3E || cp == 0x0B57 || cp == 0x0BBE || cp == 0x0BD7 ||
            cp == 0x0CC2 || cp == 0x0CD5 || cp == 0x0CD6 || cp == 0x0D3E || cp == 0x0D57 || cp == 0x0DCF || cp == 0x0DDF ||
            cp == 0x1D165 || (cp >= 0x1D16E && cp <= 0x1D172) || (cp >= 0x302E && cp <= 0x302F);

        private static bool IsPrepend(int cp) =>
            (cp >= 0x0600 && cp <= 0x0605) || cp == 0x06DD || cp == 0x070F || (cp >= 0x0890 && cp <= 0x0891) || cp == 0x08E2 ||
            cp == 0x0D4E || cp == 0x110BD || cp == 0x110CD || (cp >= 0x111C2 && cp <= 0x111C3) || cp == 0x1193F || cp == 0x11941 ||
            cp == 0x11A3A || (cp >= 0x11A84 && cp <= 0x11A89) || cp == 0x11D46 || cp == 0x11F02;

        /// <summary>InCB=Linker: virama của Devanagari, Bengali, Gujarati, Oriya, Telugu, Malayalam.</summary>
        private static bool IsInCBLinker(int cp) => cp == 0x094D || cp == 0x09CD || cp == 0x0ACD || cp == 0x0B4D || cp == 0x0C4D || cp == 0x0D4D;

        /// <summary>InCB=Consonant (Unicode 15.1) cho sáu chữ viết có Linker.</summary>
        private static bool IsInCBConsonant(int cp) =>
            (cp >= 0x0915 && cp <= 0x0939) || (cp >= 0x0958 && cp <= 0x095F) || (cp >= 0x0978 && cp <= 0x097F) ||
            (cp >= 0x0995 && cp <= 0x09A8) || (cp >= 0x09AA && cp <= 0x09B0) || cp == 0x09B2 || (cp >= 0x09B6 && cp <= 0x09B9) ||
            (cp >= 0x09DC && cp <= 0x09DD) || cp == 0x09DF || (cp >= 0x09F0 && cp <= 0x09F1) ||
            (cp >= 0x0A95 && cp <= 0x0AA8) || (cp >= 0x0AAA && cp <= 0x0AB0) || (cp >= 0x0AB2 && cp <= 0x0AB3) || (cp >= 0x0AB5 && cp <= 0x0AB9) || cp == 0x0AF9 ||
            (cp >= 0x0B15 && cp <= 0x0B28) || (cp >= 0x0B2A && cp <= 0x0B30) || (cp >= 0x0B32 && cp <= 0x0B33) || (cp >= 0x0B35 && cp <= 0x0B39) ||
            (cp >= 0x0B5C && cp <= 0x0B5D) || cp == 0x0B5F || cp == 0x0B71 ||
            (cp >= 0x0C15 && cp <= 0x0C28) || (cp >= 0x0C2A && cp <= 0x0C39) || (cp >= 0x0C58 && cp <= 0x0C5A) ||
            (cp >= 0x0D15 && cp <= 0x0D3A);

        /// <summary>Extended_Pictographic (rút gọn theo các khối emoji; không gồm Regional Indicator và emoji modifier).</summary>
        private static bool IsExtendedPictographic(int cp)
        {
            if (cp < 0x00A9) return false;
            if (cp == 0x00A9 || cp == 0x00AE || cp == 0x203C || cp == 0x2049 || cp == 0x2122 || cp == 0x2139) return true;
            if ((cp >= 0x2194 && cp <= 0x2199) || (cp >= 0x21A9 && cp <= 0x21AA) || (cp >= 0x231A && cp <= 0x231B) || cp == 0x2328 ||
                cp == 0x2388 || cp == 0x23CF || (cp >= 0x23E9 && cp <= 0x23F3) || (cp >= 0x23F8 && cp <= 0x23FA) || cp == 0x24C2 ||
                (cp >= 0x25AA && cp <= 0x25AB) || cp == 0x25B6 || cp == 0x25C0 || (cp >= 0x25FB && cp <= 0x25FE)) return true;
            if (cp >= 0x2600 && cp <= 0x27BF) return true;
            if ((cp >= 0x2934 && cp <= 0x2935) || (cp >= 0x2B05 && cp <= 0x2B07) || (cp >= 0x2B1B && cp <= 0x2B1C) || cp == 0x2B50 || cp == 0x2B55 ||
                cp == 0x3030 || cp == 0x303D || cp == 0x3297 || cp == 0x3299) return true;
            if (cp >= 0x1F000 && cp <= 0x1FAFF)
                return !(cp >= 0x1F1E6 && cp <= 0x1F1FF) && !(cp >= 0x1F3FB && cp <= 0x1F3FF);
            return cp >= 0x1FC00 && cp <= 0x1FFFD;
        }
    }
}
