using System;
using System.IO;
using Dravio.Domain;
using Dravio.Persistence.Json;
using Dravio.Persistence.Schema;
using Dravio.Persistence.Tests.Support;
using NUnit.Framework;

namespace Dravio.Persistence.Tests
{
    /// <summary>
    /// Ghi save do C# tạo ra thư mục DRAVIO_EXPORT_DIR để bản TypeScript đọc lại
    /// (Tools~/verify-csharp-saves.test.ts). Không đặt biến môi trường thì test chỉ kiểm round-trip trong C#.
    /// </summary>
    public sealed class CrossRuntimeExportTests
    {
        [Test]
        public void CSharp_saves_round_trip_and_can_be_exported_for_the_TypeScript_reader()
        {
            string dir = Environment.GetEnvironmentVariable("DRAVIO_EXPORT_DIR");
            int n = 0;
            foreach (JsonObject c in (JsonArray)PFix.Golden.Get("parsed"))
            {
                if (!((JsonBool)c.Get("ok")).Value) continue;
                string name = ((JsonString)c.Get("name")).Value;
                ParseResult r = SaveFile.Parse(((JsonString)c.Get("text")).Value);
                GameState s = PFix.Advanced(r.Save.State, 61_000);
                string text = SaveSchema.Serialize(s, PFix.T0 + n);
                ParseResult back = SaveFile.Parse(text);
                Assert.That(back.Ok, Is.True, name);
                Assert.That(PFix.Canonical(back.Save.State), Is.EqualTo(PFix.Canonical(SaveSchema.ToPersistedState(s))), name);
                PFix.Export(dir, name, text);
                n++;
            }
            Assert.That(n, Is.GreaterThan(15));
        }
    }
}
