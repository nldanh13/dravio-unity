using System;
using System.IO;
using Dravio.Domain.Core;
using Dravio.Persistence.Storage;
using UnityEngine;

namespace Dravio.Platform.Unity
{
    /// <summary>Đồng hồ tường của hệ thống (Unix ms). Chỉ lớp platform được đọc; Domain nhận qua IClock.</summary>
    public sealed class SystemClock : IClock
    {
        public long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    /// <summary>
    /// Lớp mỏng nối vòng đời Unity với <see cref="GameSession"/> thuần C#. Không gắn vào Scene nào trong U2.2:
    /// lớp trình bày (U3+) thêm component này vào một GameObject khởi động và đọc <see cref="Session"/>.
    /// - Save ở Application.persistentDataPath/saves (FileSaveStore, ghi nguyên tử + 2 backup).
    /// - Một người ghi: file khóa độc quyền; phiên thứ hai chỉ đọc.
    /// - OnApplicationPause/Focus/Quit → Hide/Show/Flush theo LifecycleMapper (mobile: mất focus = ẩn).
    /// - Tick 1 Hz và autosave 30 s chạy bằng thời gian đơn điệu realtimeSinceStartup, không phụ thuộc Time.timeScale.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DravioSessionHost : MonoBehaviour
    {
        [SerializeField] private string saveFolderName = "saves";

        // An additive environment must never start a second GameSession, even if misconfigured.
        private static DravioSessionHost _activeHost;
        private readonly PumpedScheduler _scheduler = new PumpedScheduler();
        private LifecycleMapper _lifecycle;

        public GameSession Session { get; private set; }

        public string SaveDirectory => Path.Combine(Application.persistentDataPath, saveFolderName);

        private void Awake()
        {
            if(_activeHost!=null && _activeHost!=this)
            {
                Debug.LogError("[Dravio] Duplicate Session Host rejected; existing GameSession retained.");
                enabled=false;return;
            }
            _activeHost=this;
#if UNITY_ANDROID && !UNITY_EDITOR
            // Alpha baseline: explicit battery-conscious cap; measured on target devices before adding quality tiers.
            Application.targetFrameRate = 30;
#endif
            string dir = SaveDirectory;
            FileWriterLock writerLock = FileWriterLock.TryAcquire(dir);
            ISaveStore store;
            bool persistent = true;
            try
            {
                store = new FileSaveStore(dir);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Dravio] Save directory unavailable, using non-persistent memory store: " + e.Message);
                store = new MemorySaveStore();
                persistent = false;
            }
            _lifecycle = new LifecycleMapper(focusLossHides: Application.isMobilePlatform);
            Session = new GameSession(new SessionEnvironment
            {
                Clock = new SystemClock(),
                Store = store,
                Persistent = persistent,
                WriterLock = writerLock,
                Scheduler = _scheduler,
                StartVisible = Application.isFocused || Application.isMobilePlatform,
            });
            _scheduler.Pump(NowMonotonicMs());
            Session.Start();
        }

        private static long NowMonotonicMs() => (long)(Time.realtimeSinceStartupAsDouble * 1000.0);

        private void Update() => _scheduler.Pump(NowMonotonicMs());

        private void OnApplicationPause(bool paused) => Session?.Handle(_lifecycle.OnPause(paused));

        private void OnApplicationFocus(bool focused) => Session?.Handle(_lifecycle.OnFocus(focused));

        private void OnApplicationQuit() => Session?.Handle(_lifecycle.OnQuit());

        private void OnDestroy()
        {
            // Stop Play / scene unload must flush even if OnApplicationQuit was not called.
            try { Session?.OnHide(); }
            finally
            {
                try{Session?.Dispose();}
                finally
                {
                    Session=null;
                    if(_activeHost==this)_activeHost=null;
                }
            }
        }
    }
}
