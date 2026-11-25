using System;
using Dravio.Domain;

namespace Dravio.Persistence
{
    /// <summary>Bộ hẹn giờ lặp được tiêm (setInterval). Unity cài bằng đồng hồ realtime trong Update; test cài bằng tay.</summary>
    public interface IIntervalScheduler
    {
        IDisposable Every(long intervalMs, Action action);
    }

    /// <summary>
    /// Autosave mỗi 30 s + Flush() cho sự kiện vòng đời (chạy nền, mất focus, thoát, trước import)
    /// (src/persistence/autosave.ts; GDD 3.1 trang 18). Không tự đọc đồng hồ.
    /// </summary>
    public sealed class Autosave
    {
        public const long IntervalMs = 30_000;

        private readonly Func<GameState, bool> _save;
        private readonly Func<GameState> _getState;
        private readonly IIntervalScheduler _scheduler;
        private readonly long _intervalMs;
        private IDisposable _handle;

        public Autosave(Func<GameState, bool> save, Func<GameState> getState, IIntervalScheduler scheduler, long intervalMs = IntervalMs)
        {
            _save = save; _getState = getState; _scheduler = scheduler; _intervalMs = intervalMs;
        }

        public event Action<GameState> Saved;
        public event Action<Exception> Error;

        public bool Running => _handle != null;

        public void Start()
        {
            if (_handle != null) return;
            _handle = _scheduler.Every(_intervalMs, () => Flush());
        }

        public void Stop()
        {
            _handle?.Dispose();
            _handle = null;
        }

        /// <summary>Lưu ngay nếu có state; lỗi được báo qua Error, không ném ra (lần sau thử lại).</summary>
        public bool Flush()
        {
            GameState state = _getState();
            if (state == null) return false;
            try
            {
                bool saved = _save(state);
                if (saved) Saved?.Invoke(state);
                return saved;
            }
            catch (Exception e)
            {
                Error?.Invoke(e);
                return false;
            }
        }
    }
}
