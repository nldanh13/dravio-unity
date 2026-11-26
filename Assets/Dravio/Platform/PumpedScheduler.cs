using System;
using System.Collections.Generic;
using Dravio.Persistence;

namespace Dravio.Platform
{
    /// <summary>
    /// Bộ hẹn giờ lặp được "bơm" bằng thời gian đơn điệu (monotonic) từ nền tảng — Unity: Time.realtimeSinceStartupAsDouble.
    /// Không dùng đồng hồ tường nên đổi giờ hệ thống không làm autosave dồn dập hay ngừng.
    /// Khi bị trễ nhiều chu kỳ (app bị treo), mỗi việc chỉ chạy một lần rồi hẹn lại từ thời điểm hiện tại.
    /// </summary>
    public sealed class PumpedScheduler : IIntervalScheduler
    {
        private sealed class Entry : IDisposable
        {
            public long Interval;
            public long NextAt;
            public Action Action;
            public bool Disposed;
            public void Dispose() => Disposed = true;
        }

        private readonly List<Entry> _entries = new List<Entry>();
        private long _now;
        private bool _started;

        public IDisposable Every(long intervalMs, Action action)
        {
            if (intervalMs <= 0) throw new ArgumentOutOfRangeException(nameof(intervalMs));
            var e = new Entry { Interval = intervalMs, NextAt = _now + intervalMs, Action = action };
            _entries.Add(e);
            return e;
        }

        public int ActiveCount { get { int n = 0; foreach (var e in _entries) if (!e.Disposed) n++; return n; } }

        /// <summary>Gọi mỗi frame với thời gian đơn điệu (ms).</summary>
        public void Pump(long monotonicNowMs)
        {
            if (!_started)
            {
                // Lần bơm đầu đặt gốc thời gian: các việc đã đăng ký tính từ đây.
                foreach (var e in _entries) e.NextAt = monotonicNowMs + e.Interval;
                _started = true;
            }
            if (monotonicNowMs < _now) monotonicNowMs = _now;
            _now = monotonicNowMs;
            foreach (Entry e in _entries.ToArray())
            {
                if (e.Disposed || _now < e.NextAt) continue;
                e.NextAt = _now - e.NextAt >= e.Interval ? _now + e.Interval : e.NextAt + e.Interval;
                e.Action();
            }
            _entries.RemoveAll(x => x.Disposed);
        }
    }
}
