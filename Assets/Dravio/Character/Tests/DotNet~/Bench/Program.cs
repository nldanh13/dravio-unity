// CPU/allocation benchmark of SanctuaryDragonSync + DragonBehaviorPlanner outside Unity (U1-CHAR.6).
//   dotnet run -c Release --project "Assets/Dravio/Character/Tests/DotNet~/Bench/Dravio.Character.Bench.csproj"
// Measures the pure-C# part only (no Animator, skinning, rendering). Set LEGACY=1 to time frames without intents.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Dravio.Character;
using Dravio.Character.Integration;
using UnityEngine;

sealed class NullTarget : IDragonMotionTarget
{
    // a walking + swimming model with every U1-CHAR.7 water clip (droplet-like) and the flight clips of a flyer
    private static readonly string[] Clips = { "Idle", "Eat", "Sleep", "Happy", "Walk", "Run", "Rest", "Wake", "LookAround", "Curious", "Drink", "Stretch", "Bask",
        "Greet", "Swim", "SwimIdle", "SwimRest", "SwimSurface", "WaterEnter", "WaterExit", "Fly", "Glide", "TakeOff", "Land" };
    private readonly DragonVisualCatalog.MotionInfo _info = new DragonVisualCatalog.MotionInfo { takeOffRootOffset = 0.4f, landRootOffset = 0.3f, maxTurnAir = 60f };
    private readonly Dictionary<string, DragonAnimationMap> _live = new Dictionary<string, DragonAnimationMap>();
    public bool IsLive(string uid) { return _live.ContainsKey(uid); }
    public bool Spawn(DragonVisualSpawn s) { _live[s.uid] = new DragonAnimationMap(Clips); return true; }
    public void Despawn(string uid) { _live.Remove(uid); }
    public bool SetStage(string uid, DragonVisualStage stage) { return true; }
    public bool ApplyState(string uid, DragonVisualState s) { return true; }
    public bool Trigger(string uid, DragonInteraction i) { return true; }
    public DragonAnimationMap ClipsOf(string uid) { DragonAnimationMap m; return _live.TryGetValue(uid, out m) ? m : null; }
    public float ClipSeconds(string uid, DragonInteraction c) { return 2f; }
    public void SetTempo(string uid, float t) { }
    public void SetLook(string uid, bool a, Vector3 w, float weight, float hold) { }
    public DragonVisualCatalog.MotionInfo MotionInfoOf(string uid) { return _info; }
    public void SetHidden(string uid, bool hidden) { }
    public void SetMotionShape(string uid, DragonMotionMode mode, float turn, float v, float s) { }
    public void SetExpression(string uid, float tail, float ear) { }
}

static class Bench
{
    static void Main()
    {
        bool legacy = Environment.GetEnvironmentVariable("LEGACY") == "1";
        Console.WriteLine((legacy ? "legacy (no intent)" : "behaviour intents") + ", " + Environment.ProcessorCount + " cores");
        foreach (int n in new[] { 5, 15, 30 })
        {
            var sync = new SanctuaryDragonSync(new NullTarget());
            var frames = new SanctuaryDragonFrame[n];
            for (int i = 0; i < n; i++)
                frames[i] = new SanctuaryDragonFrame
                {
                    uid = "d" + i, speciesId = "sample", stage = DragonVisualStage.Adult, visible = true, rotation = Quaternion.identity,
                    intent = legacy ? DragonBehaviorIntent.None : (DragonBehaviorIntent)(1 + i % 23), // every intent incl. U1-CHAR.7 media
                    personality = (DragonPersonalityStyle)(i % 5), hasLookTarget = true,
                };
            for (int k = 0; k < 3000; k++) sync.Sync(frames, 1f / 60f);
            const int count = 7200; // two minutes at 60 fps
            long a0 = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();
            for (int k = 0; k < count; k++) sync.Sync(frames, 1f / 60f);
            sw.Stop();
            long bytes = GC.GetAllocatedBytesForCurrentThread() - a0;
            Console.WriteLine(n + " dragons: " + (sw.Elapsed.TotalMilliseconds * 1000.0 / count).ToString("0.0") + " us/frame, "
                + (bytes / (double)count).ToString("0.0") + " B/frame allocated");
        }
    }
}
