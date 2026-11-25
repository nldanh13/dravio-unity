using System;
using System.Globalization;
using System.Text;

namespace Dravio.Persistence.Json
{
    public sealed class JsonParseException : Exception
    {
        public JsonParseException(string message, int position) : base(message + " at " + position) { Position = position; }
        public int Position { get; }
    }

    /// <summary>
    /// Bộ đọc JSON nghiêm ngặt theo RFC 8259, chấp nhận và từ chối đúng như <c>JSON.parse</c> của V8:
    /// chỉ khoảng trắng SP/TAB/LF/CR (BOM bị từ chối), không dấu phẩy thừa, không số 0 đứng đầu, không NaN,
    /// không ký tự điều khiển thô trong chuỗi, giữ nguyên surrogate lẻ từ escape \uXXXX, số tràn → ±Infinity.
    /// </summary>
    public static class JsonParser
    {
        /// <summary>Giới hạn độ sâu lồng để chống tràn stack với dữ liệu độc hại (V8 có giới hạn tương tự).</summary>
        public const int MaxDepth = 512;

        public static JsonValue Parse(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            int i = 0;
            SkipWs(text, ref i);
            JsonValue v = ParseValue(text, ref i, 0);
            SkipWs(text, ref i);
            if (i != text.Length) throw new JsonParseException("Unexpected trailing content", i);
            return v;
        }

        public static bool TryParse(string text, out JsonValue value)
        {
            try { value = Parse(text); return true; }
            catch (JsonParseException) { value = null; return false; }
        }

        private static void SkipWs(string s, ref int i)
        {
            while (i < s.Length)
            {
                char c = s[i];
                if (c == ' ' || c == '\t' || c == '\n' || c == '\r') i++;
                else break;
            }
        }

        private static JsonValue ParseValue(string s, ref int i, int depth)
        {
            if (depth > MaxDepth) throw new JsonParseException("Nesting too deep", i);
            if (i >= s.Length) throw new JsonParseException("Unexpected end", i);
            char c = s[i];
            switch (c)
            {
                case '{': return ParseObject(s, ref i, depth);
                case '[': return ParseArray(s, ref i, depth);
                case '"': return new JsonString(ParseString(s, ref i));
                case 't': Expect(s, ref i, "true"); return JsonValue.True;
                case 'f': Expect(s, ref i, "false"); return JsonValue.False;
                case 'n': Expect(s, ref i, "null"); return JsonValue.Null;
                default:
                    if (c == '-' || (c >= '0' && c <= '9')) return ParseNumber(s, ref i);
                    throw new JsonParseException("Unexpected token", i);
            }
        }

        private static void Expect(string s, ref int i, string word)
        {
            if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0 || s.Length - i < word.Length) throw new JsonParseException("Unexpected token", i);
            i += word.Length;
        }

        private static JsonObject ParseObject(string s, ref int i, int depth)
        {
            var obj = new JsonObject();
            i++;
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return obj; }
            while (true)
            {
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != '"') throw new JsonParseException("Expected string key", i);
                string key = ParseString(s, ref i);
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != ':') throw new JsonParseException("Expected ':'", i);
                i++;
                SkipWs(s, ref i);
                obj.Set(key, ParseValue(s, ref i, depth + 1));
                SkipWs(s, ref i);
                if (i >= s.Length) throw new JsonParseException("Unexpected end", i);
                char d = s[i++];
                if (d == '}') return obj;
                if (d != ',') throw new JsonParseException("Expected ',' or '}'", i - 1);
            }
        }

        private static JsonArray ParseArray(string s, ref int i, int depth)
        {
            var arr = new JsonArray();
            i++;
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return arr; }
            while (true)
            {
                SkipWs(s, ref i);
                arr.Add(ParseValue(s, ref i, depth + 1));
                SkipWs(s, ref i);
                if (i >= s.Length) throw new JsonParseException("Unexpected end", i);
                char d = s[i++];
                if (d == ']') return arr;
                if (d != ',') throw new JsonParseException("Expected ',' or ']'", i - 1);
            }
        }

        private static string ParseString(string s, ref int i)
        {
            i++; // "
            StringBuilder sb = null;
            int runStart = i;
            while (true)
            {
                if (i >= s.Length) throw new JsonParseException("Unterminated string", i);
                char c = s[i];
                if (c == '"')
                {
                    string result;
                    if (sb == null) result = s.Substring(runStart, i - runStart);
                    else { sb.Append(s, runStart, i - runStart); result = sb.ToString(); }
                    i++;
                    return result;
                }
                if (c < 0x20) throw new JsonParseException("Control character in string", i);
                if (c != '\\') { i++; continue; }
                if (sb == null) sb = new StringBuilder();
                sb.Append(s, runStart, i - runStart);
                i++;
                if (i >= s.Length) throw new JsonParseException("Unterminated escape", i);
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
                    case 'u':
                        if (s.Length - i < 4) throw new JsonParseException("Bad unicode escape", i);
                        int code = 0;
                        for (int k = 0; k < 4; k++)
                        {
                            int h = HexVal(s[i + k]);
                            if (h < 0) throw new JsonParseException("Bad unicode escape", i + k);
                            code = code * 16 + h;
                        }
                        sb.Append((char)code);
                        i += 4;
                        break;
                    default: throw new JsonParseException("Bad escape", i - 1);
                }
                runStart = i;
            }
        }

        private static int HexVal(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return -1;
        }

        private static JsonNumber ParseNumber(string s, ref int i)
        {
            int start = i;
            if (s[i] == '-') i++;
            if (i >= s.Length) throw new JsonParseException("Bad number", i);
            if (s[i] == '0') i++;
            else if (s[i] >= '1' && s[i] <= '9') { while (i < s.Length && s[i] >= '0' && s[i] <= '9') i++; }
            else throw new JsonParseException("Bad number", i);
            if (i < s.Length && s[i] == '.')
            {
                i++;
                if (i >= s.Length || s[i] < '0' || s[i] > '9') throw new JsonParseException("Bad number", i);
                while (i < s.Length && s[i] >= '0' && s[i] <= '9') i++;
            }
            if (i < s.Length && (s[i] == 'e' || s[i] == 'E'))
            {
                i++;
                if (i < s.Length && (s[i] == '+' || s[i] == '-')) i++;
                if (i >= s.Length || s[i] < '0' || s[i] > '9') throw new JsonParseException("Bad number", i);
                while (i < s.Length && s[i] >= '0' && s[i] <= '9') i++;
            }
            string token = s.Substring(start, i - start);
            double v;
            try
            {
                v = double.Parse(token, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture);
            }
            catch (OverflowException)
            {
                // .NET Framework/Mono cũ ném lỗi khi tràn; JS cho ±Infinity.
                v = token[0] == '-' ? double.NegativeInfinity : double.PositiveInfinity;
            }
            if (v == 0 && token[0] == '-') v = -0.0;
            return new JsonNumber(v);
        }
    }
}
