using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Dravio.Persistence.Json
{
    /// <summary>
    /// Ghi JSON giống hệt <c>JSON.stringify</c> của JavaScript (ES2019 well-formed):
    /// số theo Number::toString (ngắn nhất mà vẫn khôi phục đúng), NaN/±Infinity → null, -0 → 0;
    /// chuỗi chỉ escape " \ và ký tự &lt; 0x20 (dạng \b \f \n \r \t hoặc \u00xx thường), surrogate lẻ → \udxxx.
    /// </summary>
    public static class JsonWriter
    {
        public static string Stringify(JsonValue v)
        {
            var sb = new StringBuilder();
            Write(sb, v, canonical: false);
            return sb.ToString();
        }

        /// <summary>
        /// canonicalJson của schema.ts: khóa object sắp xếp theo đơn vị UTF-16 (Array.prototype.sort mặc định).
        /// Dùng để tính checksum SHA-256, nên phải trùng từng byte với bản TypeScript.
        /// </summary>
        public static string Canonical(JsonValue v)
        {
            var sb = new StringBuilder();
            Write(sb, v, canonical: true);
            return sb.ToString();
        }

        private static void Write(StringBuilder sb, JsonValue v, bool canonical)
        {
            switch (v)
            {
                case null:
                case JsonNull _:
                    sb.Append("null");
                    return;
                case JsonBool b:
                    sb.Append(b.Value ? "true" : "false");
                    return;
                case JsonNumber n:
                    sb.Append(FormatNumber(n.Value));
                    return;
                case JsonString s:
                    WriteString(sb, s.Value);
                    return;
                case JsonArray a:
                    sb.Append('[');
                    for (int i = 0; i < a.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        Write(sb, a[i], canonical);
                    }
                    sb.Append(']');
                    return;
                case JsonObject o:
                    sb.Append('{');
                    IEnumerable<string> keys = o.Keys;
                    if (canonical)
                    {
                        var list = new List<string>(o.Keys);
                        list.Sort(StringComparer.Ordinal);
                        keys = list;
                    }
                    bool first = true;
                    foreach (string k in keys)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        WriteString(sb, k);
                        sb.Append(':');
                        Write(sb, o.Get(k), canonical);
                    }
                    sb.Append('}');
                    return;
                default:
                    throw new ArgumentException("Unknown JSON node " + v.GetType());
            }
        }

        public static string Quote(string s)
        {
            var sb = new StringBuilder(s.Length + 2);
            WriteString(sb, s);
            return sb.ToString();
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); continue;
                    case '\\': sb.Append("\\\\"); continue;
                    case '\b': sb.Append("\\b"); continue;
                    case '\f': sb.Append("\\f"); continue;
                    case '\n': sb.Append("\\n"); continue;
                    case '\r': sb.Append("\\r"); continue;
                    case '\t': sb.Append("\\t"); continue;
                }
                if (c < 0x20)
                {
                    sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                }
                else if (char.IsHighSurrogate(c))
                {
                    if (i + 1 < s.Length && char.IsLowSurrogate(s[i + 1])) { sb.Append(c).Append(s[i + 1]); i++; }
                    else sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                }
                else if (char.IsLowSurrogate(c))
                {
                    sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                }
                else
                {
                    sb.Append(c);
                }
            }
            sb.Append('"');
        }

        /// <summary>Number::toString(10) của ECMAScript, xem <see cref="JsNumber.ToJsString"/>.</summary>
        public static string FormatNumber(double v) => JsNumber.ToJsString(v);
    }
}
