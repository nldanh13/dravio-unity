using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Dravio.Domain.Tests.Support
{
    /// <summary>
    /// Bộ đọc/ghi JSON tối giản cho test (không phụ thuộc package). Cây giá trị:
    /// Dictionary&lt;string, object&gt; (giữ thứ tự khóa), List&lt;object&gt;, string, double, bool, null.
    /// Số được đọc bằng double.Parse round-trip nên khớp từng bit với JSON.stringify của V8.
    /// </summary>
    public static class MiniJson
    {
        public static object Parse(string text)
        {
            int i = 0;
            object v = ParseValue(text, ref i);
            SkipWs(text, ref i);
            if (i != text.Length) throw new FormatException("Trailing JSON at " + i);
            return v;
        }

        private static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r')) i++;
        }

        private static object ParseValue(string s, ref int i)
        {
            SkipWs(s, ref i);
            char c = s[i];
            if (c == '{')
            {
                var obj = new Dictionary<string, object>(StringComparer.Ordinal);
                i++;
                SkipWs(s, ref i);
                if (s[i] == '}') { i++; return obj; }
                while (true)
                {
                    SkipWs(s, ref i);
                    string key = ParseString(s, ref i);
                    SkipWs(s, ref i);
                    if (s[i++] != ':') throw new FormatException("':' expected at " + i);
                    obj[key] = ParseValue(s, ref i);
                    SkipWs(s, ref i);
                    char d = s[i++];
                    if (d == '}') return obj;
                    if (d != ',') throw new FormatException("',' expected at " + i);
                }
            }
            if (c == '[')
            {
                var list = new List<object>();
                i++;
                SkipWs(s, ref i);
                if (s[i] == ']') { i++; return list; }
                while (true)
                {
                    list.Add(ParseValue(s, ref i));
                    SkipWs(s, ref i);
                    char d = s[i++];
                    if (d == ']') return list;
                    if (d != ',') throw new FormatException("',' expected at " + i);
                }
            }
            if (c == '"') return ParseString(s, ref i);
            if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
            if (s.Length - i >= 5 && string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
            if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            return double.Parse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static string ParseString(string s, ref int i)
        {
            if (s[i++] != '"') throw new FormatException("string expected at " + i);
            var sb = new StringBuilder();
            while (true)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                char e = s[i++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u': sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture)); i += 4; break;
                    default: throw new FormatException("bad escape at " + i);
                }
            }
        }

        /// <summary>So sánh sâu hai cây JSON (khóa không phân biệt thứ tự, mảng theo thứ tự). Trả đường dẫn khác đầu tiên hoặc null.</summary>
        public static string Diff(object expected, object actual, string path = "$")
        {
            if (expected == null || actual == null)
                return expected == null && actual == null ? null : path + ": expected " + Show(expected) + " got " + Show(actual);
            if (expected is Dictionary<string, object> eo)
            {
                if (!(actual is Dictionary<string, object> ao)) return path + ": expected object got " + Show(actual);
                foreach (var kv in eo)
                {
                    if (!ao.TryGetValue(kv.Key, out object av)) return path + "." + kv.Key + ": missing (expected " + Show(kv.Value) + ")";
                    string d = Diff(kv.Value, av, path + "." + kv.Key);
                    if (d != null) return d;
                }
                foreach (var k in ao.Keys) if (!eo.ContainsKey(k)) return path + "." + k + ": unexpected " + Show(ao[k]);
                return null;
            }
            if (expected is List<object> el)
            {
                if (!(actual is List<object> al)) return path + ": expected array got " + Show(actual);
                if (el.Count != al.Count) return path + ": length " + el.Count + " vs " + al.Count;
                for (int i = 0; i < el.Count; i++)
                {
                    string d = Diff(el[i], al[i], path + "[" + i + "]");
                    if (d != null) return d;
                }
                return null;
            }
            if (expected is double ed)
            {
                double ad;
                if (actual is double x) ad = x;
                else if (actual is long l) ad = l;
                else if (actual is int n) ad = n;
                else return path + ": expected number " + Show(expected) + " got " + Show(actual);
                return ed.Equals(ad) ? null : path + ": expected " + Show(ed) + " got " + Show(ad);
            }
            return expected.Equals(actual) ? null : path + ": expected " + Show(expected) + " got " + Show(actual);
        }

        public static string Show(object v)
        {
            if (v == null) return "null";
            if (v is string s) return "\"" + s + "\"";
            if (v is double d) return d.ToString("R", CultureInfo.InvariantCulture);
            if (v is bool b) return b ? "true" : "false";
            if (v is Dictionary<string, object>) return "{…}";
            if (v is List<object> l) return "[" + l.Count + "]";
            return Convert.ToString(v, CultureInfo.InvariantCulture);
        }
    }
}
