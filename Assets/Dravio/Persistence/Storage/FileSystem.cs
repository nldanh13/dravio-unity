using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Dravio.Persistence.Storage
{
    /// <summary>
    /// Thao tác file tối thiểu mà FileSaveStore cần. Tách ra để test mô phỏng mất điện ở từng bước
    /// (MemoryFileSystem có điểm "crash") và để nền tảng đặc biệt (WebGL, console) thay cài đặt.
    /// </summary>
    public interface IFileSystem
    {
        bool Exists(string path);
        byte[] ReadAll(string path);
        /// <summary>Ghi toàn bộ rồi flush xuống thiết bị (fsync) trước khi trả về.</summary>
        void WriteDurable(string path, byte[] data);
        /// <summary>Đổi tên nguyên tử trong cùng thư mục; không có đích thì tạo, có thì lỗi.</summary>
        void MoveNoReplace(string from, string to);
        void Delete(string path);
        IReadOnlyList<string> List(string directory);
        void EnsureDirectory(string directory);
    }

    /// <summary>Hệ thống file thật (System.IO). FileStream.Flush(true) gọi fsync/FlushFileBuffers.</summary>
    public sealed class DiskFileSystem : IFileSystem
    {
        public bool Exists(string path) => File.Exists(path);
        public byte[] ReadAll(string path) => File.ReadAllBytes(path);

        public void WriteDurable(string path, byte[] data)
        {
            using (var fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                fs.Write(data, 0, data.Length);
                fs.Flush(true);
            }
        }

        public void MoveNoReplace(string from, string to) => File.Move(from, to);
        public void Delete(string path) { if (File.Exists(path)) File.Delete(path); }

        public IReadOnlyList<string> List(string directory)
        {
            if (!Directory.Exists(directory)) return Array.Empty<string>();
            var names = new List<string>();
            foreach (string f in Directory.GetFiles(directory)) names.Add(Path.GetFileName(f));
            return names;
        }

        public void EnsureDirectory(string directory) => Directory.CreateDirectory(directory);
    }

    /// <summary>Phát sinh khi MemoryFileSystem mô phỏng mất điện: mọi thao tác sau đó đều thất bại cho tới khi "khởi động lại".</summary>
    public sealed class SimulatedPowerLossException : IOException
    {
        public SimulatedPowerLossException() : base("simulated power loss") { }
    }

    /// <summary>
    /// Hệ thống file trong bộ nhớ có mô phỏng mất điện: <see cref="CrashAfterOperations"/> = số thao tác ghi còn
    /// thành công trước khi "mất điện". Một lần ghi bị cắt giữa chừng để lại file tạm cụt (dữ liệu chưa fsync).
    /// File đã fsync + rename thì bền. Dùng để kiểm mọi điểm mất điện của FileSaveStore.
    /// </summary>
    public sealed class MemoryFileSystem : IFileSystem
    {
        private readonly Dictionary<string, byte[]> _files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        private int _ops;
        public int? CrashAfterOperations { get; set; }
        public bool Crashed { get; private set; }
        public int MutatingOperations => _ops;

        /// <summary>Khởi động lại sau mất điện: dữ liệu bền còn nguyên, thao tác tiếp tục được.</summary>
        public void Reboot() { Crashed = false; CrashAfterOperations = null; }

        private bool Step()
        {
            if (Crashed) throw new SimulatedPowerLossException();
            if (CrashAfterOperations.HasValue && _ops >= CrashAfterOperations.Value) { Crashed = true; return false; }
            _ops++;
            return true;
        }

        public bool Exists(string path) { if (Crashed) throw new SimulatedPowerLossException(); return _files.ContainsKey(path); }

        public byte[] ReadAll(string path)
        {
            if (Crashed) throw new SimulatedPowerLossException();
            if (!_files.TryGetValue(path, out byte[] d)) throw new FileNotFoundException(path);
            return (byte[])d.Clone();
        }

        public void WriteDurable(string path, byte[] data)
        {
            if (!Step())
            {
                // Mất điện giữa lúc ghi: còn lại nửa đầu (chưa fsync) của file tạm.
                _files[path] = data.AsSpanPrefix(data.Length / 2);
                throw new SimulatedPowerLossException();
            }
            if (_files.ContainsKey(path)) throw new IOException("exists: " + path);
            _files[path] = (byte[])data.Clone();
        }

        public void MoveNoReplace(string from, string to)
        {
            if (!Step()) throw new SimulatedPowerLossException();
            if (!_files.TryGetValue(from, out byte[] d)) throw new FileNotFoundException(from);
            if (_files.ContainsKey(to)) throw new IOException("exists: " + to);
            _files.Remove(from);
            _files[to] = d;
        }

        public void Delete(string path)
        {
            if (!Step()) throw new SimulatedPowerLossException();
            _files.Remove(path);
        }

        public IReadOnlyList<string> List(string directory)
        {
            if (Crashed) throw new SimulatedPowerLossException();
            string prefix = directory.TrimEnd('/') + "/";
            var names = new List<string>();
            foreach (string k in _files.Keys) if (k.StartsWith(prefix, StringComparison.Ordinal) && k.IndexOf('/', prefix.Length) < 0) names.Add(k.Substring(prefix.Length));
            return names;
        }

        public void EnsureDirectory(string directory) { }

        /// <summary>Ghi đè/hỏng file trực tiếp (mô phỏng hỏng đĩa hoặc người dùng sửa tay).</summary>
        public void Corrupt(string path, byte[] data) => _files[path] = data;
        public void CorruptText(string path, string text) => _files[path] = Encoding.UTF8.GetBytes(text);
        public IReadOnlyCollection<string> AllPaths => _files.Keys;
    }

    internal static class ByteArrayExtensions
    {
        public static byte[] AsSpanPrefix(this byte[] data, int length)
        {
            var b = new byte[length];
            Array.Copy(data, b, length);
            return b;
        }
    }
}
