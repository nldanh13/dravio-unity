using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Dravio.Persistence.Storage
{
    /// <summary>
    /// Kho save trên hệ thống file (thay IndexedDB của bản web; Unity dùng Application.persistentDataPath).
    ///
    /// Bản TS xoay main → backup1 → backup2 trong MỘT transaction IndexedDB. Hệ thống file không có transaction
    /// nhiều file, nên kho này dùng các "thế hệ" bất biến: mỗi lần lưu ghi một file mới
    /// <c>save-&lt;gen&gt;.json</c> (ghi file tạm → fsync → rename nguyên tử), rồi mới xóa thế hệ cũ hơn 3 bản gần nhất.
    /// main = thế hệ mới nhất, backup1/backup2 = hai thế hệ trước đó. Mất điện ở bất kỳ bước nào chỉ để lại:
    /// (a) file tạm cụt *.tmp — bị bỏ qua và dọn sau khi đích đã được ghi; hoặc (b) thêm một thế hệ cũ chưa kịp xóa — bị bỏ qua.
    /// Không bao giờ có slot bị ghi đè dở dang, nên không thể mất cả main lẫn backup trong một lần lưu.
    ///
    /// Archive (bản gốc trước migration) là file <c>archive-schema-N.json</c>, ghi một lần trước thế hệ mới,
    /// không bao giờ bị xóa hay ghi đè.
    /// </summary>
    public sealed class FileSaveStore : ISaveStore
    {
        private const string GenPrefix = "save-";
        private const string GenSuffix = ".json";
        private const string TmpSuffix = ".tmp";
        private const int KeepGenerations = 3;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, false);

        private readonly IFileSystem _fs;
        private readonly string _dir;

        public FileSaveStore(string directory, IFileSystem fileSystem = null)
        {
            _dir = directory.TrimEnd('/', '\\');
            _fs = fileSystem ?? new DiskFileSystem();
            _fs.EnsureDirectory(_dir);
        }

        public string Directory => _dir;

        private string PathOf(string name) => _dir + "/" + name;

        private static string GenName(long gen) => GenPrefix + gen.ToString("D12", CultureInfo.InvariantCulture) + GenSuffix;

        /// <summary>Các thế hệ hiện có, mới nhất trước.</summary>
        public List<long> Generations() => Generations(_fs.List(_dir));

        private static List<long> Generations(IReadOnlyList<string> files)
        {
            var gens = new List<long>();
            foreach (string name in files)
                if (TryGeneration(name, out long g)) gens.Add(g);
            gens.Sort((a, b) => b.CompareTo(a));
            return gens;
        }

        private static bool TryGeneration(string name, out long generation)
        {
            generation = 0;
            if (!name.StartsWith(GenPrefix, StringComparison.Ordinal) || !name.EndsWith(GenSuffix, StringComparison.Ordinal)) return false;
            string digits = name.Substring(GenPrefix.Length, name.Length - GenPrefix.Length - GenSuffix.Length);
            return long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out generation)
                && generation > 0 && name == GenName(generation);
        }

        public string Get(SaveSlot slot)
        {
            List<long> gens = Generations();
            int index = (int)slot;
            return index < gens.Count ? ReadText(GenName(gens[index])) : null;
        }

        public string GetArchive(string key) => _fs.Exists(PathOf(ArchiveName(key))) ? ReadText(ArchiveName(key)) : null;

        private static string ArchiveName(string key)
        {
            // "archive:schema-3" → "archive-schema-3.json"; chỉ chữ, số, '-' để tên file an toàn trên mọi nền tảng.
            var sb = new StringBuilder();
            foreach (char c in key) sb.Append(char.IsLetterOrDigit(c) || c == '-' ? c : '-');
            return sb + GenSuffix;
        }

        /// <summary>Đọc bytes và giải mã UTF-8; byte sai được thay bằng U+FFFD (khi đó JSON/checksum sẽ báo hỏng).</summary>
        private string ReadText(string name) => Utf8.GetString(_fs.ReadAll(PathOf(name)));

        public void Rotate(string text, RotateOptions options = null)
        {
            IReadOnlyList<string> files;
            List<long> gens;
            long next;
            try
            {
                files = _fs.List(_dir);
                gens = Generations(files);
                if (options != null && options.HasExpectMain)
                {
                    string current = gens.Count > 0 ? ReadText(GenName(gens[0])) : null;
                    if (current != options.ExpectMain) throw new SaveConflictException();
                }
                if (options?.Archive != null)
                {
                    string an = ArchiveName(options.Archive.Key);
                    if (!_fs.Exists(PathOf(an))) WriteAtomic(an, options.Archive.Text);
                }
                next = gens.Count > 0 ? gens[0] + 1 : 1;
                WriteAtomic(GenName(next), text);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                throw new SaveWriteException("save write failed: " + e.Message, e);
            }

            // Bản mới đã được ghi thành công. Dọn lỗi thì thử lại lần lưu sau.
            for (int i = KeepGenerations - 1; i < gens.Count; i++) TryDelete(GenName(gens[i]));
            CleanTemporaryFiles(files, next);
        }

        private void WriteAtomic(string name, string text)
        {
            string tmp = name + "." + Guid.NewGuid().ToString("N") + TmpSuffix;
            _fs.WriteDurable(PathOf(tmp), Utf8.GetBytes(text));
            _fs.MoveNoReplace(PathOf(tmp), PathOf(name));
        }

        private void CleanTemporaryFiles(IReadOnlyList<string> files, long committedGeneration)
        {
            foreach (string name in files)
            {
                if (!name.EndsWith(TmpSuffix, StringComparison.Ordinal)) continue;
                int separator = name.LastIndexOf(GenSuffix + ".", StringComparison.Ordinal);
                if (separator < 0) continue;
                int endOfDestination = separator + GenSuffix.Length;
                int idStart = endOfDestination + 1;
                int idLength = name.Length - TmpSuffix.Length - idStart;
                if (idLength != 32 || !Guid.TryParseExact(name.Substring(idStart, idLength), "N", out _)) continue;
                string destination = name.Substring(0, endOfDestination);
                if (TryGeneration(destination, out long generation) && generation <= committedGeneration)
                    TryDelete(name);
                else if (destination.StartsWith("archive-", StringComparison.Ordinal))
                    TryDelete(name, destination);
            }
        }

        private void TryDelete(string name, string requiredDestination = null)
        {
            try
            {
                if (requiredDestination == null || _fs.Exists(PathOf(requiredDestination))) _fs.Delete(PathOf(name));
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
