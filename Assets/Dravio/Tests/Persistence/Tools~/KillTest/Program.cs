using System;
using Dravio.Domain;
using Dravio.Domain.Core;
using Dravio.Persistence;
using Dravio.Persistence.Storage;

// seed <dir>: ghi và xác nhận một save trước khi bắt đầu thử nghiệm crash.
// write <dir>: ghi save liên tục (mỗi lần +1 s thời gian game), in "rev N" sau mỗi lần Save trả về.
// check <dir> <minRev>: nạp lại, kiểm cả 3 slot, yêu cầu revision ≥ bản đã được xác nhận ghi xong (minRev = 0: chưa xác nhận bản nào, chấp nhận thư mục trống).
public static class Program
{
    public static int Main(string[] args)
    {
        string dir = args[1];
        var store = new FileSaveStore(dir);
        if (args[0] == "write" || args[0] == "seed")
        {
            using (FileWriterLock writer = FileWriterLock.TryAcquire(dir))
            {
                if (!writer.Acquired) { Console.WriteLine("locked"); return 3; }
                var mgr = new SaveManager(store, () => 0);
                LoadOutcome o = mgr.Load();
                GameState s = o.Kind == LoadKind.Loaded ? o.Save.State : InitialStateFactory.Create("kill-test", 1_795_000_000_000, Rng.Create(1));
                while (true)
                {
                    var clock = new FixedClock(s.Clock.LastProcessedAtMs + 1000);
                    s = CommandDispatcher.Dispatch(s, new AdvanceTimeCommand(Guid.NewGuid().ToString(), AdvanceMode.Online), clock).State;
                    if (mgr.Save(s))
                    {
                        Console.WriteLine("rev " + s.Revision);
                        Console.Out.Flush();
                        if (args[0] == "seed") return 0;
                    }
                    else if (args[0] == "seed") return 4;
                }
            }
        }
        long min = long.Parse(args[2]);
        var check = new SaveManager(store, () => 0);
        LoadOutcome r = check.Load();
        int valid = 0;
        foreach (SaveSlot slot in new[] { SaveSlot.Main, SaveSlot.Backup1, SaveSlot.Backup2 }) if (store.Get(slot) != null && SaveFile.Parse(store.Get(slot)).Ok) valid++;
        long rev = r.Kind == LoadKind.Loaded ? r.Save.State.Revision : -1;
        Console.WriteLine($"{r.Kind} slot={r.Slot} rev={rev} minConfirmed={min} validSlots={valid} failures={r.Failures.Count}");
        // Bị kill trước lần ghi đầu tiên (chưa có bản nào được xác nhận; revision đầu tiên ≥ 1): thư mục trống là kết quả đúng.
        if (min == 0 && r.Kind == LoadKind.Empty && valid == 0 && r.Failures.Count == 0) return 0;
        return r.Kind == LoadKind.Loaded && r.Slot == SaveSlot.Main && rev >= min && r.Failures.Count == 0 ? 0 : 1;
    }
}
