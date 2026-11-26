using Dravio.Domain;

namespace Dravio.Platform
{
    /// <summary>
    /// Quy tắc thời gian online/offline (GDD 3.1 trang 9, 18; ADR 0010; src/platform/timePolicy.ts).
    /// - Online (100 %) chỉ khi thế giới đang hiển thị và tick 1 Hz chạy đều.
    /// - Ẩn/chạy nền: quyết toán online tới lúc ẩn, rồi khi hiện lại tính phần vắng mặt là offline
    ///   (75 %, chặn 24 h, receipt nếu &gt; 60 s — luật nằm trong Domain, không đổi ở đây).
    /// - Máy ngủ hoặc tiến trình bị treo mà không có sự kiện: tick kế tiếp thấy khoảng trống &gt; 5 s → offline.
    /// </summary>
    public static class TimePolicy
    {
        public const long TickMs = 1000;
        /// <summary>Khoảng trống tối đa giữa hai lần quyết toán vẫn coi là online (đề xuất kỹ thuật ADR 0010).</summary>
        public const long OnlineGapLimitMs = 5000;

        public static AdvanceMode ModeFor(long lastProcessedAtMs, long nowMs, bool visible)
        {
            if (!visible) return AdvanceMode.Offline;
            return nowMs - lastProcessedAtMs > OnlineGapLimitMs ? AdvanceMode.Offline : AdvanceMode.Online;
        }
    }

    /// <summary>Tín hiệu vòng đời đã chuẩn hóa mà GameSession hiểu.</summary>
    public enum LifecycleAction { None, Hide, Show, Flush }

    /// <summary>
    /// Ánh xạ tín hiệu vòng đời của ứng dụng (Unity: OnApplicationPause / OnApplicationFocus / OnApplicationQuit)
    /// sang Hide/Show/Flush — tương đương visibilitychange/freeze/pagehide/pageshow của bản web (lifecycle.ts).
    ///
    /// Bản web: mất focus nhưng tab vẫn hiển thị vẫn là online; chỉ ẩn mới là offline. Tương ứng:
    /// - Pause(true) (mobile chạy nền, desktop tạm dừng khi runInBackground = false) → Hide.
    /// - Pause(false) → Show.
    /// - Focus(false): mobile (thanh thông báo, cuộc gọi, chuyển app) → Hide; desktop (cửa sổ vẫn hiện) → Flush.
    /// - Focus(true) → Show (bỏ qua nếu đang không ẩn).
    /// - Quit → Hide (quyết toán online tới lúc thoát rồi lưu).
    /// Mọi trường hợp lọt lưới (bị treo, ngủ máy) vẫn được quy tắc khoảng trống 5 s bắt.
    /// </summary>
    public sealed class LifecycleMapper
    {
        public LifecycleMapper(bool focusLossHides) { FocusLossHides = focusLossHides; }

        public bool FocusLossHides { get; }

        public LifecycleAction OnPause(bool paused) => paused ? LifecycleAction.Hide : LifecycleAction.Show;

        public LifecycleAction OnFocus(bool focused) => focused ? LifecycleAction.Show : FocusLossHides ? LifecycleAction.Hide : LifecycleAction.Flush;

        public LifecycleAction OnQuit() => LifecycleAction.Hide;
    }
}
