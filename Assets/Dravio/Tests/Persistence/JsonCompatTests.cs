using System;
using System.Collections.Generic;
using System.Globalization;
using Dravio.Persistence.Json;
using Dravio.Persistence.Tests.Support;
using NUnit.Framework;

namespace Dravio.Persistence.Tests
{
    /// <summary>JSON phải trùng JSON.parse / JSON.stringify của V8 từng byte, vì checksum tính trên chuỗi.</summary>
    public sealed class JsonCompatTests
    {
        private static double FromBitsHex(string hexLittleEndian)
        {
            var b = new byte[8];
            for (int i = 0; i < 8; i++) b[i] = byte.Parse(hexLittleEndian.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            if (!BitConverter.IsLittleEndian) Array.Reverse(b);
            return BitConverter.ToDouble(b, 0);
        }

        [Test]
        public void Number_formatting_matches_JSON_stringify_for_535_doubles()
        {
            var list = (JsonArray)PFix.Golden.Get("numbers");
            Assert.That(list.Count, Is.GreaterThan(500));
            foreach (JsonObject n in list)
            {
                double v = FromBitsHex(((JsonString)n.Get("bits")).Value);
                string expected = ((JsonString)n.Get("json")).Value;
                Assert.That(JsonWriter.FormatNumber(v), Is.EqualTo(expected), v.ToString("R", CultureInfo.InvariantCulture));
                if (expected != "null")
                {
                    double back = ((JsonNumber)JsonParser.Parse(expected)).Value;
                    Assert.That(back == v, Is.True, "round-trip " + expected);
                }
            }
        }

        [Test]
        public void String_escaping_matches_JSON_stringify_including_lone_surrogates()
        {
            foreach (JsonObject s in (JsonArray)PFix.Golden.Get("strings"))
            {
                var units = (JsonArray)s.Get("codeUnits");
                var chars = new char[units.Count];
                for (int i = 0; i < units.Count; i++) chars[i] = (char)(int)((JsonNumber)units[i]).Value;
                string value = new string(chars);
                string expected = ((JsonString)s.Get("json")).Value;
                Assert.That(JsonWriter.Quote(value), Is.EqualTo(expected));
                Assert.That(((JsonString)JsonParser.Parse(expected)).Value, Is.EqualTo(value));
            }
        }

        [TestCase("{\"a\":1,}")]
        [TestCase("[1,]")]
        [TestCase("01")]
        [TestCase("-")]
        [TestCase("1.")]
        [TestCase(".5")]
        [TestCase("1e")]
        [TestCase("+1")]
        [TestCase("NaN")]
        [TestCase("Infinity")]
        [TestCase("'a'")]
        [TestCase("\"\t\"")]
        [TestCase("\"\\x41\"")]
        [TestCase("\"\\u12G4\"")]
        [TestCase("\uFEFF{}")]
        [TestCase("{} x")]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("tru")]
        [TestCase("nul")]
        [TestCase("{\"a\" 1}")]
        [TestCase("\u00A0{}")]
        public void Parser_rejects_what_JSON_parse_rejects(string text)
        {
            Assert.That(JsonParser.TryParse(text, out _), Is.False, text);
        }

        [Test]
        public void Parser_accepts_JSON_edge_cases_like_V8()
        {
            Assert.That(((JsonNumber)JsonParser.Parse("1e400")).Value, Is.EqualTo(double.PositiveInfinity));
            Assert.That(((JsonNumber)JsonParser.Parse("-1e400")).Value, Is.EqualTo(double.NegativeInfinity));
            Assert.That(1 / ((JsonNumber)JsonParser.Parse("-0")).Value, Is.EqualTo(double.NegativeInfinity));
            Assert.That(JsonWriter.Stringify(JsonParser.Parse("-0")), Is.EqualTo("0"));
            var o = (JsonObject)JsonParser.Parse(" {\"a\":1,\"b\":2,\"a\":3} ");
            Assert.That(JsonWriter.Stringify(o), Is.EqualTo("{\"a\":3,\"b\":2}"));
            Assert.That(JsonWriter.Canonical(JsonParser.Parse("{\"b\":1,\"a\":[1,{\"d\":2,\"c\":null}],\"B\":0}")), Is.EqualTo("{\"B\":0,\"a\":[1,{\"c\":null,\"d\":2}],\"b\":1}"));
            Assert.That(((JsonString)JsonParser.Parse("\"\\ud83d\\udd25\"")).Value, Is.EqualTo("\U0001F525"));
            Assert.That(JsonWriter.Stringify(JsonParser.Parse("[1E2,0.5e-0,1.25E+1]")), Is.EqualTo("[100,0.5,12.5]"));
        }

        [Test]
        public void Canonical_key_order_is_UTF16_code_unit_order()
        {
            var o = new JsonObject().Set("\uFF21", JsonValue.From(1L)).Set("\U0001F525", JsonValue.From(2L)).Set("a", JsonValue.From(3L)).Set("Z", JsonValue.From(4L));
            // Surrogate (0xD83D) < 0xFF21: JS sort đặt emoji trước chữ toàn độ rộng.
            Assert.That(JsonWriter.Canonical(o), Is.EqualTo("{\"Z\":4,\"a\":3,\"\U0001F525\":2,\"\uFF21\":1}"));
        }

        [Test]
        public void Deep_nesting_is_rejected_instead_of_overflowing_the_stack()
        {
            string deep = new string('[', 100_000) + new string(']', 100_000);
            Assert.That(JsonParser.TryParse(deep, out _), Is.False);
        }
    }
}
