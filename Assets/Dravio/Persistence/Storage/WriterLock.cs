using System;
using System.IO;

namespace Dravio.Persistence.Storage
{
    /// <summary>
    /// Một người ghi (ADR 0006 mục 8; src/platform/writerLock.ts). Bản web dùng Web Locks; Unity dùng
    /// file khóa mở độc quyền trong thư mục save: phiên thứ hai (ví dụ mở game hai lần trên desktop) chỉ đọc.
    /// Hệ điều hành tự nhả khóa khi tiến trình chết, nên không cần lease/TTL như bản fallback localStorage.
    /// </summary>
    public interface IWriterLock : IDisposable
    {
        bool Acquired { get; }
        /// <summary>true khi nền tảng không hỗ trợ khóa (ví dụ WebGL): phiên tự coi là người ghi, CAS vẫn chặn ghi đè.</summary>
        bool Unsupported { get; }
    }

    public sealed class FileWriterLock : IWriterLock
    {
        public const string LockFileName = "dravio-save-writer.lock";
        private FileStream _stream;

        private FileWriterLock(FileStream stream, bool unsupported) { _stream = stream; Acquired = stream != null || unsupported; Unsupported = unsupported; }

        public bool Acquired { get; }
        public bool Unsupported { get; }

        public static FileWriterLock TryAcquire(string directory)
        {
            try
            {
                Directory.CreateDirectory(directory);
                var fs = new FileStream(Path.Combine(directory, LockFileName), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                return new FileWriterLock(fs, false);
            }
            catch (IOException)
            {
                return new FileWriterLock(null, false); // phiên khác đang giữ
            }
            catch (UnauthorizedAccessException)
            {
                return new FileWriterLock(null, false);
            }
            catch (PlatformNotSupportedException)
            {
                return new FileWriterLock(null, true);
            }
        }

        public void Dispose()
        {
            _stream?.Dispose();
            _stream = null;
        }
    }

    /// <summary>Khóa trong tiến trình cho test (mô phỏng hai phiên).</summary>
    public sealed class InMemoryWriterLock : IWriterLock
    {
        private readonly System.Collections.Generic.HashSet<string> _held;
        private readonly string _name;
        public InMemoryWriterLock(System.Collections.Generic.HashSet<string> held, string name = "dravio-save-writer")
        {
            _held = held; _name = name;
            Acquired = held.Add(name);
        }
        public bool Acquired { get; private set; }
        public bool Unsupported => false;
        public void Dispose() { if (Acquired) _held.Remove(_name); Acquired = false; }
    }
}
