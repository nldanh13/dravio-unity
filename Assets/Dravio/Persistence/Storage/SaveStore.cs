using System;
using System.Collections.Generic;

namespace Dravio.Persistence.Storage
{
    /// <summary>Ba slot save (GDD 3.1 trang 18: autosave + 2 backup).</summary>
    public enum SaveSlot { Main, Backup1, Backup2 }

    /// <summary>main đã bị nơi khác ghi kể từ lần đọc/ghi gần nhất của phiên này (ADR 0010 compare-and-swap).</summary>
    public sealed class SaveConflictException : Exception
    {
        public SaveConflictException() : base("save main slot was changed by another writer") { }
    }

    /// <summary>Lỗi I/O khi ghi save. State trong bộ nhớ không bị ảnh hưởng; lần autosave sau thử lại.</summary>
    public sealed class SaveWriteException : Exception
    {
        public SaveWriteException(string message, Exception inner) : base(message, inner) { }
    }

    public sealed class ArchiveWrite
    {
        public ArchiveWrite(string key, string text) { Key = key; Text = text; }
        /// <summary>Ví dụ "archive:schema-3".</summary>
        public string Key { get; }
        public string Text { get; }
    }

    public sealed class RotateOptions
    {
        /// <summary>Bản gốc trước migration; chỉ ghi khi khóa chưa tồn tại, không bao giờ bị xoay hay ghi đè.</summary>
        public ArchiveWrite Archive { get; set; }
        /// <summary>Compare-and-swap: chỉ ghi khi main hiện tại đúng bằng giá trị này (null = chưa có main).</summary>
        public bool HasExpectMain { get; set; }
        public string ExpectMain { get; set; }
    }

    /// <summary>
    /// Kho save key–value (src/persistence/store.ts). Rotate ghi save mới và đẩy main → backup1 → backup2
    /// như một thao tác nguyên tử: sau khi mất điện ở bất kỳ thời điểm nào, kho là trạng thái trước hoặc sau, không lẫn.
    /// </summary>
    public interface ISaveStore
    {
        string Get(SaveSlot slot);
        string GetArchive(string key);
        void Rotate(string text, RotateOptions options = null);
    }

    /// <summary>Kho trong bộ nhớ cho test và khi không có hệ thống file ghi được (không bền).</summary>
    public sealed class MemorySaveStore : ISaveStore
    {
        public readonly Dictionary<SaveSlot, string> Slots = new Dictionary<SaveSlot, string>();
        public readonly Dictionary<string, string> Archives = new Dictionary<string, string>(StringComparer.Ordinal);
        public int Writes { get; private set; }
        public bool FailNextWrite { get; set; }

        public string Get(SaveSlot slot) => Slots.TryGetValue(slot, out string v) ? v : null;
        public string GetArchive(string key) => Archives.TryGetValue(key, out string v) ? v : null;

        public void Rotate(string text, RotateOptions options = null)
        {
            if (options != null && options.HasExpectMain && Get(SaveSlot.Main) != options.ExpectMain) throw new SaveConflictException();
            if (FailNextWrite)
            {
                FailNextWrite = false;
                throw new SaveWriteException("simulated write failure", null);
            }
            string main = Get(SaveSlot.Main), b1 = Get(SaveSlot.Backup1);
            if (b1 != null) Slots[SaveSlot.Backup2] = b1;
            if (main != null) Slots[SaveSlot.Backup1] = main;
            Slots[SaveSlot.Main] = text;
            if (options?.Archive != null && !Archives.ContainsKey(options.Archive.Key)) Archives[options.Archive.Key] = options.Archive.Text;
            Writes++;
        }
    }
}
