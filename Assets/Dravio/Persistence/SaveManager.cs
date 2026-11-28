using System;
using System.Collections.Generic;
using Dravio.Domain;
using Dravio.Persistence.Schema;
using Dravio.Persistence.Storage;

namespace Dravio.Persistence
{
    public sealed class SlotFailure
    {
        public SlotFailure(SaveSlot slot, SaveErrorCode code, IReadOnlyList<string> details) { Slot = slot; Code = code; Details = details; }
        public SaveSlot Slot { get; }
        public SaveErrorCode Code { get; }
        public IReadOnlyList<string> Details { get; }
        public override string ToString() => Slot + ":" + Code.ToWire();
    }

    public enum LoadKind { Loaded, Empty, Unrecoverable, NewerVersion }

    public sealed class LoadOutcome
    {
        internal LoadOutcome(LoadKind kind, ParsedSave save, SaveSlot slot, IReadOnlyList<SlotFailure> failures) { Kind = kind; Save = save; Slot = slot; Failures = failures; }
        public LoadKind Kind { get; }
        public ParsedSave Save { get; }
        public SaveSlot Slot { get; }
        public IReadOnlyList<SlotFailure> Failures { get; }
    }

    /// <summary>Lý do chặn ghi. Conflict: nơi khác đã ghi main sau lần đọc của phiên này (ADR 0010).</summary>
    public enum WriteBlock { None, ReadOnly, NewerVersion, Unrecoverable, Conflict }

    public sealed class ImportPrepareResult
    {
        internal ImportPrepareResult(SaveFile.ImportPreview preview, SaveErrorCode code, IReadOnlyList<string> details) { Preview = preview; Code = code; Details = details; }
        public bool Ok => Preview != null;
        public SaveFile.ImportPreview Preview { get; }
        public SaveErrorCode Code { get; }
        public IReadOnlyList<string> Details { get; }
    }

    /// <summary>
    /// Người ghi duy nhất của save (src/persistence/saveManager.ts; ADR 0006, 0010).
    /// - Load: main → backup1 → backup2, mỗi slot kiểm JSON + checksum + migration + validate.
    /// - Save do bản mới hơn ghi → khóa ghi cả phiên (kể cả force), không fallback, không ghi đè.
    /// - Mọi slot hỏng → Unrecoverable: không tự ghi đè; chỉ ghi khi người chơi chọn bắt đầu lại/nhập file (force).
    /// - Save: bỏ qua nếu saveId + revision không đổi; mỗi lần ghi xoay 2 backup nguyên tử; CAS chống ghi đè chéo.
    /// API đồng bộ và an toàn luồng (khóa nội bộ thay cho hàng đợi Promise của bản TS).
    /// </summary>
    public sealed class SaveManager
    {
        private readonly ISaveStore _store;
        private readonly Func<long> _nowMs;
        private readonly object _gate = new object();
        private string _lastSavedId;
        private long _lastSavedRevision = -1;
        private WriteBlock _block = WriteBlock.None;
        private bool _readOnly;
        private ArchiveWrite _pendingArchive;
        private bool _mainKnown;
        private string _knownMain;
        private ParsedSave _pendingImport;
        private GameState _pendingImportState;

        public SaveManager(ISaveStore store, Func<long> nowMs)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _nowMs = nowMs ?? throw new ArgumentNullException(nameof(nowMs));
        }

        public WriteBlock Block { get { lock (_gate) return _readOnly ? WriteBlock.ReadOnly : _block; } }

        /// <summary>Phiên không giữ khóa người ghi: chỉ đọc.</summary>
        public void SetReadOnly(bool readOnly)
        {
            lock (_gate) _readOnly = readOnly;
        }

        public LoadOutcome Load()
        {
            lock (_gate)
            {
                var failures = new List<SlotFailure>();
                bool anyData = false;
                _knownMain = _store.Get(SaveSlot.Main);
                _mainKnown = true;
                foreach (SaveSlot slot in new[] { SaveSlot.Main, SaveSlot.Backup1, SaveSlot.Backup2 })
                {
                    string text = slot == SaveSlot.Main ? _knownMain : _store.Get(slot);
                    if (text == null) continue;
                    anyData = true;
                    ParseResult r = SaveFile.Parse(text);
                    if (r.Ok)
                    {
                        if (slot == SaveSlot.Main) { _lastSavedId = r.Save.State.SaveId; _lastSavedRevision = r.Save.State.Revision; }
                        else { _lastSavedId = null; _lastSavedRevision = -1; }
                        if (r.Save.MigratedFrom.HasValue)
                        {
                            _pendingArchive = new ArchiveWrite("archive:schema-" + r.Save.MigratedFrom.Value, text);
                            _lastSavedId = null; _lastSavedRevision = -1;
                        }
                        return new LoadOutcome(LoadKind.Loaded, r.Save, slot, failures);
                    }
                    failures.Add(new SlotFailure(slot, r.Code, r.Details));
                    if (r.Code == SaveErrorCode.NewerVersion)
                    {
                        _block = WriteBlock.NewerVersion;
                        return new LoadOutcome(LoadKind.NewerVersion, null, default, failures);
                    }
                }
                if (anyData)
                {
                    _block = WriteBlock.Unrecoverable;
                    return new LoadOutcome(LoadKind.Unrecoverable, null, default, failures);
                }
                return new LoadOutcome(LoadKind.Empty, null, default, failures);
            }
        }

        /// <summary>
        /// Ghi save. Trả false nếu bị chặn hoặc revision không đổi. Ném <see cref="SaveWriteException"/> khi I/O lỗi
        /// (state trong bộ nhớ không đổi; lần gọi sau thử lại).
        /// </summary>
        public bool Save(GameState state, bool force = false)
        {
            lock (_gate)
            {
                if (_readOnly || (_block != WriteBlock.None && !(force && _block == WriteBlock.Unrecoverable))) return false;
                if (!force && _lastSavedId == state.SaveId && _lastSavedRevision == state.Revision) return false;
                string text = SaveSchema.Serialize(state, _nowMs());
                var options = new RotateOptions { Archive = _pendingArchive, HasExpectMain = _mainKnown, ExpectMain = _knownMain };
                try
                {
                    _store.Rotate(text, options);
                }
                catch (SaveConflictException)
                {
                    _block = WriteBlock.Conflict;
                    return false;
                }
                _knownMain = text;
                _mainKnown = true;
                _pendingArchive = null;
                _lastSavedId = state.SaveId;
                _lastSavedRevision = state.Revision;
                if (_block == WriteBlock.Unrecoverable) _block = WriteBlock.None;
                return true;
            }
        }

        /// <summary>Bước 1 của import: đọc + kiểm file, bỏ entitlement/Verified, trả bản xem trước. Chưa đổi kho.</summary>
        public ImportPrepareResult PrepareImport(string text)
        {
            lock (_gate)
            {
                _pendingImport = null;
                _pendingImportState = null;
                ParseResult r = SaveFile.Parse(text);
                if (!r.Ok) return new ImportPrepareResult(null, r.Code, r.Details);
                GameState state = SaveFile.SanitizeImported(r.Save, out SaveFile.ImportSanitizeReport report);
                var warnings = new List<string>(r.Save.Warnings);
                if (report.DroppedStateKeys.Count > 0) warnings.Add("dropped state keys: " + string.Join(", ", report.DroppedStateKeys));
                if (report.DroppedDragonKeys.Count > 0) warnings.Add("dropped dragon keys: " + string.Join(", ", report.DroppedDragonKeys));
                _pendingImport = new ParsedSave(state, r.Save.SavedAtMs, r.Save.MigratedFrom, warnings, r.Save.RawState);
                _pendingImportState = state;
                return new ImportPrepareResult(SaveFile.PreviewOf(state, r.Save.SavedAtMs, r.Save.MigratedFrom, warnings), default, Array.Empty<string>());
            }
        }

        public void CancelImport()
        {
            lock (_gate) { _pendingImport = null; _pendingImportState = null; }
        }

        /// <summary>
        /// Bước 2: người chơi xác nhận. Save hiện tại được ghi trước (thành backup1 sau khi xoay), rồi save nhập thành main
        /// với revision + 1. Trả state mới, hoặc null nếu không có import chờ / bị chặn / ghi thất bại.
        /// </summary>
        public GameState ConfirmImport(GameState current)
        {
            lock (_gate)
            {
                if (_pendingImport == null || _readOnly || (_block != WriteBlock.None && _block != WriteBlock.Unrecoverable)) return null;
                if (current != null) Save(current, force: true);
                GameState state = _pendingImportState.DeepClone();
                state.Revision += 1;
                bool ok = Save(state, force: true);
                _pendingImport = null;
                _pendingImportState = null;
                return ok ? state : null;
            }
        }

        /// <summary>Xuất file JSON có checksum. Không cần khóa người ghi.</summary>
        public string ExportText(GameState state) => SaveSchema.Serialize(state, _nowMs());
    }
}
