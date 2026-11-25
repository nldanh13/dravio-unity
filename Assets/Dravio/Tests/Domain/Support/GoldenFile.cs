using System;
using System.Collections.Generic;
using System.IO;

namespace Dravio.Domain.Tests.Support
{
    /// <summary>Nạp golden vectors sinh từ engine TypeScript gốc (Tools~/golden.test.ts).</summary>
    public static class GoldenFile
    {
        public const string RelativePath = "Assets/Dravio/Tests/Domain/Golden/ts-golden-b33ec7a.json";
        private static Dictionary<string, object> _cached;

        public static Dictionary<string, object> Root => _cached ?? (_cached = (Dictionary<string, object>)MiniJson.Parse(File.ReadAllText(Locate())));

        public static List<object> List(string key) => (List<object>)Root[key];

        /// <summary>Tìm file từ thư mục làm việc (Unity: thư mục project) hoặc từ thư mục chứa assembly test (dotnet test).</summary>
        public static string Locate()
        {
            foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    string p = Path.Combine(dir.FullName, RelativePath);
                    if (File.Exists(p)) return p;
                    dir = dir.Parent;
                }
            }
            throw new FileNotFoundException("Golden file not found: " + RelativePath);
        }
    }
}
