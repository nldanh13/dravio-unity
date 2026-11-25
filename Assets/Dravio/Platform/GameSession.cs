using System;
using System.Collections.Generic;
using Dravio.Domain;
using Dravio.Domain.Core;
using Dravio.Persistence;
using Dravio.Persistence.Storage;

namespace Dravio.Platform
{
    public enum SessionStatus { Loading, Ready, ReadOnly, NewerVersion, Unrecoverable, Conflict }

    /// <summary>Phụ thuộc của phiên, tiêm từ nền tảng (Unity) hoặc test.</summary>
    public sealed class SessionEnvironment
    {
        public IClock Clock { get; set; }
        public ISaveStore Store { get; set; }
        /// <summary>false khi không có kho bền (đang dùng MemorySaveStore): UI phải cảnh báo "hãy xuất file".</summary>
        public bool Persistent { get; set; } = true;
        public IWriterLock WriterLock { get; set; }
        public IIntervalScheduler Scheduler { get; set; }
        /// <summary>ID duy nhất cho requestId/saveId (Unity: Guid.NewGuid). Domain không tự sinh.</summary>
        public Func<string> NewId { get; set; } = () => Guid.NewGuid().ToString();
        /// <summary>Trạng thái hiển thị lúc khởi động (Unity: Application.isFocused / không paused).</summary>
        public bool StartVisible { get; set; } = true;
    }

    /// <summary>
    /// Nối Domain + Persistence cho một tiến trình (src/platform/gameSession.ts), không phụ thuộc Unity.
    /// Lớp platform là nơi duy nhất đọc đồng hồ hệ thống và sinh ID; Domain chỉ nhận IClock/IRng đã tiêm.
    /// Phiên không giữ khóa người ghi chỉ đọc: không chạy thời gian, không lưu, không import.
    /// </summary>
    public sealed class GameSession : IDisposable
    {
        private readonly SessionEnvironment _env;
        private SaveManager _manager;
        private Autosave _autosave;
        private IDisposable _tick;
        private bool _hidden;
        private bool _swapping;

        public GameSession(SessionEnvironment env)
        {
            _env = env ?? throw new ArgumentNullException(nameof(env));
        }

        public SessionStatus Status { get; private set; } = SessionStatus.Loading;
        public GameState State { get; private set; }
        public bool Persistent => _env.Persistent;
        public bool LockUnsupported => _env.WriterLock?.Unsupported ?? true;
        public long? LastSavedAtMs { get; private set; }
        /// <summary>"main" | "backup1" | "backup2" | "new".</summary>
        public string LoadedFrom { get; private set; }
        public int? MigratedFrom { get; private set; }
        public IReadOnlyList<SlotFailure> Failures { get; private set; } = Array.Empty<SlotFailure>();
        public bool SaveError { get; private set; }
        public SaveFile.ImportPreview ImportPreview { get; private set; }
        public SaveErrorCode? ImportError { get; private set; }
        public bool Hidden => _hidden;

        /// <summary>Phát sau mỗi thay đổi snapshot (UI đọc lại State/Status).</summary>
        public event Action Changed;
        /// <summary>Event domain của mọi lệnh đã áp dụng, kể cả advanceTime tự động.</summary>
        public event Action<IReadOnlyList<DomainEvent>> DomainEvents;

        private void Notify() => Changed?.Invoke();

        public void Start()
        {
            bool writer = _env.WriterLock == null || _env.WriterLock.Acquired;
            _manager = new SaveManager(_env.Store, () => _env.Clock.NowMs());
            _manager.SetReadOnly(!writer);
            LoadOutcome outcome = _manager.Load();
            Failures = outcome.Failures;
            if (outcome.Kind == LoadKind.NewerVersion) { Status = SessionStatus.NewerVersion; Notify(); return; }
            if (outcome.Kind == LoadKind.Unrecoverable) { Status = SessionStatus.Unrecoverable; Notify(); return; }
            GameState state;
            if (outcome.Kind == LoadKind.Loaded)
            {
                state = outcome.Save.State;
                LoadedFrom = SlotName(outcome.Slot);
                MigratedFrom = outcome.Save.MigratedFrom;
                LastSavedAtMs = outcome.Save.SavedAtMs;
            }
            else
            {
                state = NewGame();
                LoadedFrom = "new";
            }
            if (!writer)
            {
                Status = SessionStatus.ReadOnly;
                State = state;
                Notify();
                return;
            }
            // Thời gian vắng mặt tính offline (75 %, chặn 24 h) trước khi chạy online — rồi lưu NGAY,
            // để đóng app ngay sau đó không làm lại được phần thưởng offline với mốc cũ.
            State = Apply(state, AdvanceMode.Offline);
            _hidden = !_env.StartVisible;
            Status = SessionStatus.Ready;
            Notify();
            SaveNow();
            Run();
        }

        private static string SlotName(SaveSlot s) => s == SaveSlot.Main ? "main" : s == SaveSlot.Backup1 ? "backup1" : "backup2";

        private GameState NewGame()
        {
            string saveId = _env.NewId();
            return InitialStateFactory.Create(saveId, _env.Clock.NowMs(), Rng.Create(Rng.HashSeed("new-save", saveId)));
        }

        private GameState Apply(GameState state, AdvanceMode mode)
        {
            DispatchResult r = CommandDispatcher.Dispatch(state, new AdvanceTimeCommand(_env.NewId(), mode), _env.Clock);
            if (r.Ok && r.Events.Count > 0) DomainEvents?.Invoke(r.Events);
            return r.Ok ? r.State : state;
        }

        private void Run()
        {
            if (_manager == null || _env.Scheduler == null) return;
            _tick = _env.Scheduler.Every(TimePolicy.TickMs, Tick);
            _autosave = new Autosave(
                s => { bool ok = _manager.Save(s); CheckConflict(); return ok; },
                () => Status == SessionStatus.Ready && !_swapping ? State : null,
                _env.Scheduler);
            _autosave.Saved += _ => { LastSavedAtMs = _env.Clock.NowMs(); SaveError = false; Notify(); };
            _autosave.Error += _ => { SaveError = true; Notify(); };
            _autosave.Start();
        }

        /// <summary>Tick 1 Hz: online nếu đang hiện và không có khoảng trống lớn (ngủ máy, treo) — ADR 0010.</summary>
        public void Tick()
        {
            GameState s = State;
            if (s == null || Status != SessionStatus.Ready || _swapping || _hidden) return;
            AdvanceMode mode = TimePolicy.ModeFor(s.Clock.LastProcessedAtMs, _env.Clock.NowMs(), true);
            State = Apply(s, mode);
            Notify();
        }

        public void Handle(LifecycleAction action)
        {
            switch (action)
            {
                case LifecycleAction.Hide: OnHide(); break;
                case LifecycleAction.Show: OnShow(); break;
                case LifecycleAction.Flush: _autosave?.Flush(); break;
            }
        }

        /// <summary>Ẩn/chạy nền/thoát: quyết toán phần online tới lúc này rồi lưu ngay.</summary>
        public void OnHide()
        {
            if (_hidden) { _autosave?.Flush(); return; }
            GameState s = State;
            if (s != null && Status == SessionStatus.Ready && !_swapping)
            {
                AdvanceMode mode = TimePolicy.ModeFor(s.Clock.LastProcessedAtMs, _env.Clock.NowMs(), true);
                State = Apply(s, mode);
                Notify();
            }
            _hidden = true;
            _autosave?.Flush();
        }

        /// <summary>Hiện lại: phần thời gian ẩn tính offline (75 %, receipt nếu &gt; 60 s), rồi tiếp tục tick online.</summary>
        public void OnShow()
        {
            if (!_hidden) return;
            _hidden = false;
            GameState s = State;
            if (s != null && Status == SessionStatus.Ready && !_swapping)
            {
                State = Apply(s, AdvanceMode.Offline);
                Notify();
            }
        }

        public void SaveNow()
        {
            GameState s = State;
            if (_manager == null || s == null || Status != SessionStatus.Ready) return;
            try
            {
                if (_manager.Save(s)) { LastSavedAtMs = _env.Clock.NowMs(); SaveError = false; }
                CheckConflict();
            }
            catch (Exception)
            {
                SaveError = true;
            }
            Notify();
        }

        /// <summary>Save bị phiên khác ghi (CAS thất bại): dừng thời gian và autosave, chuyển sang chỉ xem. Không ghi đè.</summary>
        private void CheckConflict()
        {
            if (_manager?.Block != WriteBlock.Conflict || Status == SessionStatus.Conflict) return;
            StopLoops();
            Status = SessionStatus.Conflict;
            Notify();
        }

        private void StopLoops()
        {
            _tick?.Dispose();
            _tick = null;
            _autosave?.Stop();
        }

        /// <summary>Bắt đầu lại khi mọi slot hỏng (người chơi đã xác nhận). Dữ liệu hỏng bị đẩy xuống backup1.</summary>
        public void StartFresh()
        {
            if (_manager == null || Status != SessionStatus.Unrecoverable || !(_env.WriterLock == null || _env.WriterLock.Acquired)) return;
            GameState state = NewGame();
            _manager.Save(state, force: true);
            State = state;
            Status = SessionStatus.Ready;
            LoadedFrom = "new";
            LastSavedAtMs = _env.Clock.NowMs();
            Notify();
            Run();
        }

        /// <summary>Gửi lệnh người chơi qua command bus; requestId do phiên gán. Phiên chỉ xem/đang thay save → null.</summary>
        public DomainError Send(Func<string, GameCommand> build)
        {
            GameState s = State;
            if (s == null || Status != SessionStatus.Ready || _swapping) return null;
            GameCommand cmd = build(_env.NewId());
            DispatchResult r = CommandDispatcher.Dispatch(s, cmd, _env.Clock);
            if (!r.Ok) return r.Error;
            State = r.State;
            if (r.Events.Count > 0) DomainEvents?.Invoke(r.Events);
            Notify();
            // Mua/nhận trứng: lưu ngay để kết quả đã roll và thưởng đã nhận không thể làm lại bằng cách tắt app
            // (IMMEDIATE_SAVE của gameSession.ts).
            if (cmd is BuyTutorialPackCommand || cmd is ClaimPackCommand) SaveNow();
            return null;
        }

        public string ExportText() => _manager != null && State != null ? _manager.ExportText(State) : null;

        public void PrepareImport(string text)
        {
            if (_manager == null) return;
            ImportPrepareResult r = _manager.PrepareImport(text);
            ImportPreview = r.Ok ? r.Preview : null;
            ImportError = r.Ok ? (SaveErrorCode?)null : r.Code;
            Notify();
        }

        public void CancelImport()
        {
            _manager?.CancelImport();
            ImportPreview = null;
            ImportError = null;
            Notify();
        }

        public void ConfirmImport()
        {
            if (_manager == null) return;
            bool wasRunning = _tick != null;
            _swapping = true;
            GameState imported;
            try { imported = _manager.ConfirmImport(Status == SessionStatus.Ready ? State : null); }
            catch (Exception) { imported = null; }
            if (imported == null)
            {
                _swapping = false;
                ImportPreview = null;
                ImportError = SaveErrorCode.InvalidState;
                Notify();
                return;
            }
            GameState state = Apply(imported, AdvanceMode.Offline);
            _swapping = false;
            State = state;
            Status = SessionStatus.Ready;
            ImportPreview = null;
            ImportError = null;
            LoadedFrom = "main";
            LastSavedAtMs = _env.Clock.NowMs();
            Notify();
            if (!wasRunning) Run();
        }

        public void Dispose()
        {
            StopLoops();
            _env.WriterLock?.Dispose();
        }
    }
}
