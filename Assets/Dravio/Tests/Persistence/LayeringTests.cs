using System.IO;
using System.Text.RegularExpressions;
using Dravio.Persistence.Tests.Support;
using NUnit.Framework;

namespace Dravio.Persistence.Tests
{
    /// <summary>
    /// Tách lớp: Domain thuần không biết persistence; Persistence và Platform là C# thuần (không UnityEngine);
    /// chỉ Platform/Unity chạm UnityEngine. Đồng hồ hệ thống chỉ được đọc ở Platform/Unity.
    /// </summary>
    public sealed class LayeringTests
    {
        private static string Dir(string name) => Path.GetFullPath(Path.Combine(PFix.Locate("Assets/Dravio"), name));

        private static string Code(string file) => Regex.Replace(Regex.Replace(File.ReadAllText(file), @"//.*", ""), @"/\*.*?\*/", "", RegexOptions.Singleline);

        [TestCase("Persistence")]
        [TestCase("Platform")]
        public void Pure_layers_have_no_Unity_and_no_ambient_wall_clock(string layer)
        {
            var forbidden = new Regex(@"\bUnityEngine\b|\bUnityEditor\b|MonoBehaviour|DateTime\.(Now|UtcNow|Today)|DateTimeOffset\.(Now|UtcNow)|\bSystem\.Random\b|\bnew Random\(");
            foreach (string f in Directory.GetFiles(Dir(layer), "*.cs", SearchOption.AllDirectories))
            {
                if (f.Replace('\\', '/').Contains("/Platform/Unity/")) continue;
                Assert.That(forbidden.IsMatch(Code(f)), Is.False, f);
            }
        }

        [Test]
        public void Domain_does_not_reference_persistence_or_platform()
        {
            // U6.0.2d: InternalsVisibleTo cho assembly test (tên chuỗi, không phải tham chiếu code) được phép; chỉ assembly test, không phải Persistence / Platform.
            var friendTests = new Regex(@"\[assembly:\s*InternalsVisibleTo\(""Dravio\.[A-Za-z]+\.Tests""\)\]");
            foreach (string f in Directory.GetFiles(Dir("Domain"), "*.cs", SearchOption.AllDirectories))
                Assert.That(Regex.IsMatch(friendTests.Replace(Code(f), ""), @"Dravio\.(Persistence|Platform)"), Is.False, f);
        }

        [TestCase("Persistence/Dravio.Persistence.asmdef", true, "\"Dravio.Domain\"")]
        [TestCase("Platform/Dravio.Platform.asmdef", true, "\"Dravio.Domain\", \"Dravio.Persistence\"")]
        [TestCase("Platform/Unity/Dravio.Platform.Unity.asmdef", false, "\"Dravio.Domain\", \"Dravio.Persistence\", \"Dravio.Platform\"")]
        public void Assembly_definitions_enforce_the_layering(string path, bool noEngine, string refs)
        {
            string json = File.ReadAllText(Dir(path));
            Assert.That(json, Does.Contain("\"noEngineReferences\": " + (noEngine ? "true" : "false")));
            Assert.That(json, Does.Contain("\"references\": [" + refs + "]"));
        }

        [Test]
        public void Only_the_Unity_host_reads_the_system_clock()
        {
            string host = Code(Dir("Platform/Unity/DravioSessionHost.cs"));
            Assert.That(host, Does.Contain("DateTimeOffset.UtcNow"));
            Assert.That(host, Does.Contain("realtimeSinceStartupAsDouble"));
        }
    }
}
