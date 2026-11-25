using System;

namespace Dravio.Domain.Core
{
    /// <summary>Đồng hồ được tiêm (src/engine/core/clock.ts). Domain không gọi DateTime.Now.</summary>
    public interface IClock
    {
        /// <summary>Unix epoch milliseconds.</summary>
        long NowMs();
    }

    /// <summary>Đồng hồ cố định cho test/replay: chỉ đổi khi gọi Set/Advance.</summary>
    public sealed class FixedClock : IClock
    {
        private long _t;
        public FixedClock(long ms) { _t = ms; }
        public long NowMs() => _t;
        public void Set(long ms) { _t = ms; }
        public void Advance(long deltaMs) { _t += deltaMs; }
    }

    /// <summary>Mốc thời gian đã quyết toán (GameState.clock).</summary>
    public sealed class ClockState
    {
        public ClockState(long lastProcessedAtMs, long highWaterAtMs)
        {
            LastProcessedAtMs = lastProcessedAtMs;
            HighWaterAtMs = highWaterAtMs;
        }

        public long LastProcessedAtMs { get; }
        public long HighWaterAtMs { get; }
    }

    public readonly struct ElapsedResult
    {
        public ElapsedResult(long elapsedMs, ClockState next)
        {
            ElapsedMs = elapsedMs;
            Next = next;
        }

        /// <summary>Thời gian được xử lý, đã chặn trong 0..24h.</summary>
        public long ElapsedMs { get; }
        public ClockState Next { get; }
    }

    /// <summary>Quy tắc thời gian online/offline (GDD 3.1 trang 18).</summary>
    public static class TimeRules
    {
        public const long OfflineCapMs = 24L * 60 * 60 * 1000;

        /// <summary>
        /// elapsed = clamp(now − lastProcessedAt, 0, 24h). Đồng hồ lùi: giữ high-water mark,
        /// không tính thời gian cho tới khi vượt lại mốc cũ.
        /// </summary>
        public static ElapsedResult ComputeElapsed(ClockState state, long nowMs)
        {
            long effectiveNow = Math.Max(nowMs, state.HighWaterAtMs);
            long raw = effectiveNow - state.LastProcessedAtMs;
            long elapsedMs = Math.Min(Math.Max(raw, 0), OfflineCapMs);
            return new ElapsedResult(elapsedMs, new ClockState(effectiveNow, effectiveNow));
        }
    }
}
