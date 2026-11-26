using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Dravio.Domain.Core;
using Dravio.Domain.Tests.Support;
using Dravio.Domain.Text;
using NUnit.Framework;

namespace Dravio.Domain.Tests
{
    public sealed class NicknameAndPurityTests
    {
        [TestCase("", 0)]
        [TestCase("abc", 3)]
        [TestCase("e\u0301", 1)]
        [TestCase("\r\n", 1)]
        [TestCase("\U0001F1FB\U0001F1F3\U0001F1FB\U0001F1F3", 2)]
        [TestCase("\U0001F44D\U0001F3FD", 1)]
        [TestCase("각", 1)]
        [TestCase("한국어", 3)]
        [TestCase("क\u094Dष", 1)]
        [TestCase("x\u200Dy", 2)]
        [TestCase("\U0001F3F4\U000E0067\U000E0062\U000E0065\U000E006E\U000E0067\U000E007F", 1)]
        public void Grapheme_counter_follows_UAX29(string s, int expected)
        {
            Assert.That(GraphemeCounter.Count(s), Is.EqualTo(expected));
        }

        [Test]
        public void JS_trim_differs_from_dotnet_trim_on_NEL_and_BOM()
        {
            Assert.That(JsCompat.Trim("\uFEFFa\uFEFF"), Is.EqualTo("a"));
            Assert.That(JsCompat.Trim("\u0085a"), Is.EqualTo("\u0085a"));
            Assert.That(JsCompat.Trim("\u2028\u3000a\u00A0\t"), Is.EqualTo("a"));
            Assert.That(JsCompat.Trim("   "), Is.EqualTo(""));
        }

        [Test]
        public void NFC_keeps_lone_surrogates_like_JavaScript()
        {
            Assert.That(NicknameRules.NormalizeNfc("E\u0302"), Is.EqualTo("Ê"));
            Assert.That(NicknameRules.NormalizeNfc("a\uD800E\u0302"), Is.EqualTo("a\uD800Ê"));
            Assert.That(NicknameRules.Normalize("a\uDC00b").Ok, Is.True);
        }

        private static string DomainDir() =>
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(GoldenFile.Locate()), "..", "..", "..", "Domain"));

        /// <summary>Core Domain là C# thuần: không UnityEngine/UnityEditor, không nguồn thời gian/ngẫu nhiên ngầm.</summary>
        [Test]
        public void Domain_sources_have_no_Unity_or_ambient_time_random_dependencies()
        {
            var forbidden = new Regex(@"\bUnityEngine\b|\bUnityEditor\b|MonoBehaviour|ScriptableObject|\bSystem\.Random\b|\bnew Random\(|DateTime\.(Now|UtcNow|Today)|DateTimeOffset\.(Now|UtcNow)|Environment\.TickCount|Stopwatch");
            var files = Directory.GetFiles(DomainDir(), "*.cs", SearchOption.AllDirectories);
            Assert.That(files.Length, Is.GreaterThan(20));
            foreach (string f in files)
            {
                string code = Regex.Replace(File.ReadAllText(f), @"//.*", "");
                Assert.That(forbidden.IsMatch(code), Is.False, f);
            }
        }

        [Test]
        public void Domain_assembly_definition_forbids_engine_references()
        {
            string asmdef = File.ReadAllText(Path.Combine(DomainDir(), "Dravio.Domain.asmdef"));
            Assert.That(asmdef, Does.Contain("\"noEngineReferences\": true"));
            Assert.That(asmdef, Does.Contain("\"references\": []"));
        }
    }
}
