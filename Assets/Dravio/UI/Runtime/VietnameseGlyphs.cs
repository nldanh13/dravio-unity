using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dravio.UI
{
    /// <summary>
    /// Vietnamese glyph coverage for TextMeshPro/SDF font assets (VIS.2a). The set is generated from the 12
    /// Vietnamese vowels x 6 tones (upper and lower case) plus Đ/đ and printable ASCII, composed to NFC so a
    /// font atlas needs precomposed glyphs only. Combining marks in UI strings (NFD text) are reported
    /// because many SDF atlases position them poorly.
    /// </summary>
    public static class VietnameseGlyphs
    {
        static readonly string[] Vowels={"a","ă","â","e","ê","i","o","ô","ơ","u","ư","y"};
        // none, grave, hook above, tilde, acute, dot below
        static readonly string[] Tones={"","̀","̉","̃","́","̣"};
        static string required;

        /// <summary>Every precomposed Vietnamese letter (lower then upper case) plus Đ/đ, without ASCII.</summary>
        public static string Letters
        {
            get
            {
                if(required!=null)return required;
                var set=new List<char>();
                foreach(var upper in new[]{false,true})
                    foreach(var v in Vowels)
                        foreach(var t in Tones)
                        {
                            var s=(upper?v.ToUpperInvariant():v)+t;
                            s=s.Normalize(NormalizationForm.FormC);
                            if(s.Length!=1)throw new InvalidOperationException("Vietnamese letter does not compose: "+s);
                            if(s[0]>127 && !set.Contains(s[0]))set.Add(s[0]);
                        }
                set.Add('đ');set.Add('Đ');
                return required=new string(set.ToArray());
            }
        }

        /// <summary>Printable ASCII 0x20..0x7E plus the HUD's typographic extras (bullet, dashes, ellipsis, arrows).</summary>
        public static string AsciiAndPunctuation
        {
            get
            {
                var sb=new StringBuilder();
                for(char c=' ';c<='~';c++)sb.Append(c);
                sb.Append("•–—…’“”×→←·°");
                return sb.ToString();
            }
        }

        /// <summary>Full set to pre-populate or audit a UI font asset.</summary>
        public static string Required=>AsciiAndPunctuation+Letters;

        /// <summary>Characters in text that the font does not have (distinct, in order of first use).</summary>
        public static List<char> Missing(string text,Func<char,bool> fontHas)
        {
            var missing=new List<char>();
            if(string.IsNullOrEmpty(text)||fontHas==null)return missing;
            foreach(var c in text)
                if(!char.IsWhiteSpace(c) && !char.IsControl(c) && !missing.Contains(c) && !fontHas(c))missing.Add(c);
            return missing;
        }

        /// <summary>True when text contains combining diacritics (U+0300..U+036F), i.e. it is not NFC.</summary>
        public static bool HasCombiningMarks(string text)=>!string.IsNullOrEmpty(text) && text.Any(c=>c>='̀' && c<='ͯ');
    }
}
