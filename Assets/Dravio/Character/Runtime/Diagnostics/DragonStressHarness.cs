using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Dravio.Character.Integration;
using Unity.Profiling;
using UnityEngine;

namespace Dravio.Character.Diagnostics
{
    /// <summary>
    /// Stress run for the living-dragon runtime: for each count (default 5, 15, 30) spawns that many dragons
    /// from the catalog (round-robin over entries), drives random wandering + locomotion/interaction changes,
    /// warms up, then samples FPS, frame time, draw calls, SetPass calls, triangles and memory with
    /// ProfilerRecorder. Render counters are only available in the Editor and Development builds.
    /// Writes JSON to Application.persistentDataPath/dragon-stress-&lt;timestamp&gt;.json and logs a summary.
    /// Used by the scene Dravio/Character/Create Stress Test Scene creates; never part of a gameplay scene.
    /// </summary>
    public sealed class DragonStressHarness : MonoBehaviour
    {
        public DragonVisualManager manager;
        public int[] counts = { 5, 15, 30 };
        public float warmupSeconds = 3f;
        public float sampleSeconds = 10f;
        public float areaRadius = 12f;
        public int seed = 1234;
        public bool quitWhenDone;
        [Tooltip("U1-CHAR.6: drive the dragons through SanctuaryDragonSync with behaviour intents, personalities and a look target (the camera) instead of raw locomotion")]
        public bool behaviourIntents;

        [Serializable] public sealed class Result
        {
            public int dragons; public int visibleAvg; public int animatorEvaluationsAvg;
            public float fpsAvg, fps1Low, frameMsAvg, frameMsMax;
            public long drawCallsAvg, setPassAvg, trianglesAvg, verticesAvg;
            public long totalUsedMemoryMB, gcUsedMemoryMB, systemUsedMemoryMB;
            public int poolCreated, poolReused, poolDestroyed;
            public bool behaviourIntents; public int oneShotsStarted, substitutions;
        }
        [Serializable] public sealed class Report
        {
            public string unityVersion, platform, device, gpu, timestamp, renderCountersNote;
            public bool development;
            public List<Result> results = new List<Result>();
        }

        public Report LastReport { get; private set; }
        public bool Running { get; private set; }

        private System.Random _rng;
        private readonly List<string> _uids = new List<string>();
        private readonly Dictionary<string, Vector3> _targets = new Dictionary<string, Vector3>();
        private string _status = "";
        // behaviour mode (U1-CHAR.6)
        private SanctuaryDragonSync _sync;
        private readonly List<SanctuaryDragonFrame> _frames = new List<SanctuaryDragonFrame>();
        private readonly Dictionary<string, SanctuaryDragonFrame> _sim = new Dictionary<string, SanctuaryDragonFrame>();
        private int _oneShots, _subs;
        private static readonly DragonBehaviorIntent[] Arrived = { DragonBehaviorIntent.Idle, DragonBehaviorIntent.Rest, DragonBehaviorIntent.Bask,
            DragonBehaviorIntent.Curious, DragonBehaviorIntent.LookAround, DragonBehaviorIntent.Drink, DragonBehaviorIntent.Eat, DragonBehaviorIntent.Sleep,
            DragonBehaviorIntent.Greet };
        // U1-CHAR.7: medium intents, sent only when the model can show them (ReadinessFor), to exercise transitions
        private static readonly DragonBehaviorIntent[] Mediums = { DragonBehaviorIntent.SwimIdle, DragonBehaviorIntent.Fly, DragonBehaviorIntent.Underground, DragonBehaviorIntent.Perch };
        private static readonly DragonMotionMode[] MediumModes = { DragonMotionMode.Water, DragonMotionMode.Air, DragonMotionMode.Underground, DragonMotionMode.Perched };

        private IEnumerator Start()
        {
            if (manager == null) manager = FindAnyObjectByType<DragonVisualManager>();
            if (manager == null || manager.catalog == null || manager.catalog.entries.Count == 0)
            {
                Debug.LogError("[DragonStress] Needs a DragonVisualManager with a non-empty catalog (run Dravio/Character/Import Batch Manifest first).");
                yield break;
            }
            Running = true;
            _rng = new System.Random(seed);
            var report = new Report
            {
                unityVersion = Application.unityVersion, platform = Application.platform.ToString(),
                device = SystemInfo.deviceModel, gpu = SystemInfo.graphicsDeviceName,
                timestamp = DateTime.UtcNow.ToString("o"), development = Debug.isDebugBuild,
                renderCountersNote = "Draw calls/SetPass/triangles come from ProfilerRecorder and are 0 in non-development players."
            };
            foreach (int n in counts)
            {
                SpawnTo(n);
                _status = "Warm-up " + n;
                float t = 0f;
                while (t < warmupSeconds) { Drive(); t += Time.unscaledDeltaTime; yield return null; }
                _oneShots = 0; _subs = 0;
                yield return Sample(n, report);
            }
            if (_sync != null) { _sync.Clear(); _sim.Clear(); _uids.Clear(); }
            manager.DespawnAll();
            LastReport = report;
            string path = Path.Combine(Application.persistentDataPath, "dragon-stress-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".json");
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            foreach (var r in report.results)
                Debug.Log(string.Format("[DragonStress] {0} dragons: {1:0.0} fps avg ({2:0.0} 1% low), {3:0.00} ms avg, draw calls {4}, SetPass {5}, tris {6}, used mem {7} MB",
                    r.dragons, r.fpsAvg, r.fps1Low, r.frameMsAvg, r.drawCallsAvg, r.setPassAvg, r.trianglesAvg, r.totalUsedMemoryMB));
            Debug.Log("[DragonStress] Report written: " + path);
            _status = "Done: " + path;
            Running = false;
            if (quitWhenDone) Application.Quit();
        }

        private IEnumerator Sample(int n, Report report)
        {
            using (var draw = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count"))
            using (var setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count"))
            using (var tris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count"))
            using (var verts = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertices Count"))
            using (var totalMem = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory"))
            using (var gcMem = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Used Memory"))
            using (var sysMem = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory"))
            {
                var frames = new List<float>();
                long sDraw = 0, sSet = 0, sTris = 0, sVerts = 0; int sVis = 0, sEval = 0, k = 0;
                float t = 0f;
                _status = "Sampling " + n;
                while (t < sampleSeconds)
                {
                    Drive();
                    yield return null;
                    float dt = Time.unscaledDeltaTime;
                    t += dt; frames.Add(dt);
                    sDraw += draw.LastValue; sSet += setPass.LastValue; sTris += tris.LastValue; sVerts += verts.LastValue;
                    sVis += manager.VisibleLastFrame; sEval += manager.EvaluatedLastFrame; k++;
                }
                frames.Sort();
                float sum = 0f; foreach (var f in frames) sum += f;
                int lowCount = Math.Max(1, frames.Count / 100);
                float lowSum = 0f; for (int i = frames.Count - lowCount; i < frames.Count; i++) lowSum += frames[i];
                k = Math.Max(1, k);
                report.results.Add(new Result
                {
                    dragons = n, visibleAvg = sVis / k, animatorEvaluationsAvg = sEval / k,
                    fpsAvg = frames.Count / Math.Max(1e-5f, sum), fps1Low = lowCount / Math.Max(1e-5f, lowSum),
                    frameMsAvg = 1000f * sum / Math.Max(1, frames.Count), frameMsMax = 1000f * frames[frames.Count - 1],
                    drawCallsAvg = sDraw / k, setPassAvg = sSet / k, trianglesAvg = sTris / k, verticesAvg = sVerts / k,
                    totalUsedMemoryMB = totalMem.LastValue / (1024 * 1024), gcUsedMemoryMB = gcMem.LastValue / (1024 * 1024),
                    systemUsedMemoryMB = sysMem.LastValue / (1024 * 1024),
                    poolCreated = manager.PoolCreated, poolReused = manager.PoolReused, poolDestroyed = manager.PoolDestroyed,
                    behaviourIntents = behaviourIntents, oneShotsStarted = _oneShots, substitutions = _subs
                });
            }
        }

        private void SpawnTo(int n)
        {
            if (behaviourIntents) { SpawnFrames(n); return; }
            var entries = manager.catalog.entries;
            while (_uids.Count > n) { manager.Despawn(_uids[_uids.Count - 1]); _uids.RemoveAt(_uids.Count - 1); }
            for (int i = _uids.Count; i < n; i++)
            {
                var e = entries[i % entries.Count];
                string uid = "stress-" + i;
                var pos = RandomPoint();
                manager.Spawn(new DragonVisualSpawn
                {
                    uid = uid, speciesId = e.speciesId, stage = e.stage,
                    state = DragonVisualState.At(pos, Quaternion.Euler(0f, (float)_rng.NextDouble() * 360f, 0f))
                });
                _uids.Add(uid);
                _targets[uid] = RandomPoint();
            }
        }

        private Vector3 RandomPoint()
        {
            double a = _rng.NextDouble() * Math.PI * 2, r = Math.Sqrt(_rng.NextDouble()) * areaRadius;
            return transform.position + new Vector3((float)(Math.Cos(a) * r), 0f, (float)(Math.Sin(a) * r));
        }

        private static readonly DragonLocomotion[] Moving = { DragonLocomotion.Walk, DragonLocomotion.Run, DragonLocomotion.Swim, DragonLocomotion.Fly, DragonLocomotion.Burrow };

        /// <summary>Synthetic integration layer: wander between random points; the visual runtime substitutes
        /// clips a species does not have (reported once per instance).</summary>
        private void SpawnFrames(int n)
        {
            if (_sync == null) _sync = new SanctuaryDragonSync(manager);
            var entries = manager.catalog.entries;
            while (_uids.Count > n) { _sim.Remove(_uids[_uids.Count - 1]); _uids.RemoveAt(_uids.Count - 1); }
            for (int i = _uids.Count; i < n; i++)
            {
                var e = entries[i % entries.Count];
                string uid = "stress-" + i;
                _sim[uid] = new SanctuaryDragonFrame
                {
                    uid = uid, speciesId = e.speciesId, stage = e.stage, visible = true, intent = DragonBehaviorIntent.Walk,
                    personality = (DragonPersonalityStyle)(1 + i % 4), position = RandomPoint(),
                    rotation = Quaternion.Euler(0f, (float)_rng.NextDouble() * 360f, 0f),
                };
                _uids.Add(uid);
                _targets[uid] = RandomPoint();
            }
            DriveFrames(0f);
        }

        /// <summary>Synthetic Sanctuary for behaviour mode: walk to a point, then an arrival intent for a while.</summary>
        private void DriveFrames(float dt)
        {
            var cam = Camera.main;
            _frames.Clear();
            foreach (var uid in _uids)
            {
                var f = _sim[uid];
                var to = _targets[uid] - f.position; to.y = 0f;
                var status = _sync.MotionStatusOf(uid);
                if (status.rootOffsetToApply != 0f) f.position += Vector3.up * status.rootOffsetToApply;
                if (!status.canTranslate) { f.speed = 0f; f.turnRateDegPerSec = 0f; } // a transition is playing: keep still
                else if (f.intent == DragonBehaviorIntent.Walk || f.intent == DragonBehaviorIntent.Run)
                {
                    if (to.magnitude < 0.3f) f.intent = Arrived[_rng.Next(Arrived.Length)];
                    else
                    {
                        f.speed = f.intent == DragonBehaviorIntent.Run ? 3f : 1.2f;
                        var before = f.rotation;
                        f.rotation = Quaternion.RotateTowards(f.rotation, Quaternion.LookRotation(to.normalized), 180f * dt);
                        f.turnRateDegPerSec = dt > 0f ? Quaternion.Angle(before, f.rotation) / dt : 0f;
                        f.position += f.rotation * Vector3.forward * f.speed * dt;
                    }
                }
                else
                {
                    f.speed = 0f; f.turnRateDegPerSec = 0f;
                    if (_rng.NextDouble() < 0.002) { _targets[uid] = RandomPoint(); f.intent = _rng.NextDouble() < 0.25 ? DragonBehaviorIntent.Run : DragonBehaviorIntent.Walk; }
                    else if (_rng.NextDouble() < 0.001)
                    {
                        int m = _rng.Next(Mediums.Length);
                        if (_sync.ReadinessFor(uid, MediumModes[m]) == DragonMotionReadiness.Ready) f.intent = Mediums[m];
                    }
                    else if (_rng.NextDouble() < 0.001) _sync.React(uid, DragonReaction.Petted);
                }
                f.hasLookTarget = cam != null;
                if (cam != null) f.lookTarget = cam.transform.position;
                _sim[uid] = f;
                _frames.Add(f);
            }
            var r = _sync.Sync(_frames, dt);
            _oneShots += r.triggered; _subs += r.substituted;
        }

        private void Drive()
        {
            float dt = Time.deltaTime;
            if (behaviourIntents) { DriveFrames(dt); return; }
            foreach (var uid in _uids)
            {
                DragonVisualInstance inst;
                if (!manager.TryGet(uid, out inst)) continue;
                var s = inst.State;
                var target = _targets[uid];
                var to = target - s.position; to.y = 0f;
                if (to.magnitude < 0.3f || _rng.NextDouble() < 0.002)
                {
                    _targets[uid] = RandomPoint();
                    if (_rng.NextDouble() < 0.3) manager.Trigger(uid, _rng.NextDouble() < 0.5 ? DragonInteraction.Eat : DragonInteraction.Happy);
                    s.locomotion = _rng.NextDouble() < 0.2 ? DragonLocomotion.Idle : Moving[_rng.Next(Moving.Length)];
                }
                if (s.locomotion != DragonLocomotion.Idle && s.locomotion != DragonLocomotion.Sleep && inst.CurrentInteraction == DragonInteraction.None)
                {
                    s.speed = s.locomotion == DragonLocomotion.Run ? 3f : 1.2f;
                    if (to.sqrMagnitude > 1e-4f)
                        s.rotation = Quaternion.RotateTowards(s.rotation, Quaternion.LookRotation(to.normalized), 180f * dt);
                    s.position += s.rotation * Vector3.forward * s.speed * dt;
                }
                else s.speed = 0f;
                manager.ApplyState(uid, s);
            }
        }

        private void OnGUI()
        {
            if (manager == null) return;
            GUI.Label(new Rect(10, 10, 900, 22), "Dragon stress: " + _status + " | live " + manager.Count +
                      " | visible " + manager.VisibleLastFrame + " | animator evals " + manager.EvaluatedLastFrame +
                      " | " + (1f / Mathf.Max(1e-5f, Time.smoothDeltaTime)).ToString("0") + " fps");
        }
    }
}
