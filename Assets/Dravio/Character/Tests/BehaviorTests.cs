using System.Collections.Generic;
using System.Linq;
using Dravio.Character.Integration;
using NUnit.Framework;
using UnityEngine;

namespace Dravio.Character.Tests
{
    /// <summary>
    /// U1-CHAR.6: behaviour planner (intent -> clips, transitions, idle variations, personality presentation,
    /// reactions), head look and blink maths, environment hints, and the backward-compatible Sanctuary sync.
    /// Pure logic: Unity Test Runner and `dotnet test Assets/Dravio/Character/Tests/DotNet~`.
    /// </summary>
    public sealed class BehaviorTests
    {
        private static readonly string[] Walker = { "Idle", "Eat", "Sleep", "Happy", "Walk", "Run", "Rest", "Wake", "LookAround", "Curious", "Drink", "Stretch", "Bask" };
        private static readonly string[] Swimmer = { "Idle", "Eat", "Sleep", "Happy", "Swim", "Rest", "Wake", "LookAround", "Curious" };
        private static readonly string[] OldModel = { "Idle", "Eat", "Sleep", "Happy", "Walk", "Run" }; // pre-U1-CHAR.6 asset

        private static DragonBehaviorPlanner Planner(string[] clips, DragonPersonalityStyle style = DragonPersonalityStyle.Unspecified, uint seed = 7)
        {
            return new DragonBehaviorPlanner(new DragonAnimationMap(clips), c => c == DragonInteraction.Wake ? 2f : 1.5f,
                DragonPersonalityPresentation.For(style), seed);
        }

        private static List<DragonBehaviorOutput> Run(DragonBehaviorPlanner p, DragonBehaviorIntent intent, float seconds, float dt = 0.1f, float turn = 0f)
        {
            var list = new List<DragonBehaviorOutput>();
            for (float t = 0f; t < seconds - 1e-4f; t += dt) list.Add(p.Update(dt, intent, 1f, turn));
            return list;
        }

        [Test]
        public void Intents_MapToClipsTheSpeciesHas_AndSubstituteHonestly()
        {
            var p = Planner(Walker);
            Assert.AreEqual(DragonLocomotion.Bask, p.Update(0.1f, DragonBehaviorIntent.Bask, 0f, 0f).locomotion);
            var swim = Planner(Swimmer).Update(0.1f, DragonBehaviorIntent.Bask, 0f, 0f);
            Assert.AreEqual(DragonLocomotion.Rest, swim.locomotion, "no Bask: nearest lying clip");
            Assert.IsTrue(swim.substituted);
            var old = Planner(OldModel);
            var o = old.Update(0.1f, DragonBehaviorIntent.Recover, 0f, 0f);
            Assert.AreEqual(DragonLocomotion.Sleep, o.locomotion, "old asset without Rest: Recover rests as Sleep");
            o = old.Update(0.1f, DragonBehaviorIntent.Fly, 0f, 0f);
            Assert.AreEqual(DragonLocomotion.Idle, o.locomotion, "never fake an ability");
            StringAssert.Contains("Fly", o.note);
            o = old.Update(0.1f, DragonBehaviorIntent.Drink, 0f, 0f);
            Assert.AreEqual(DragonInteraction.None, o.trigger, "no Drink clip: nothing is faked");
            Assert.IsTrue(o.substituted);
        }

        [Test]
        public void LeavingSleep_PlaysWakeFirst_AndNothingInterruptsIt()
        {
            var p = Planner(Walker);
            Run(p, DragonBehaviorIntent.Sleep, 3f);
            Assert.AreEqual(DragonLocomotion.Sleep, p.Shown);
            var walk = Run(p, DragonBehaviorIntent.Walk, 3f);
            Assert.AreEqual(DragonInteraction.Wake, walk[0].trigger, "Sleep -> Wake -> Walk");
            Assert.AreEqual(1, walk.Count(x => x.trigger != DragonInteraction.None));
            Assert.IsTrue(walk.Take(19).All(x => !x.headLookAllowed), "no look-at while the Wake one-shot runs");
            var noWake = Planner(new[] { "Idle", "Sleep", "Walk" });
            Run(noWake, DragonBehaviorIntent.Sleep, 1f);
            Assert.AreEqual(DragonInteraction.None, noWake.Update(0.1f, DragonBehaviorIntent.Walk, 1f, 0f).trigger, "no Wake clip: Animator crossfade only");
        }

        [Test]
        public void Reactions_FollowPersonality_WaitForRunningOneShot_AndNeverDisturbSleep()
        {
            Assert.AreEqual(DragonInteraction.Happy, React(DragonPersonalityStyle.Playful, DragonReaction.Petted));
            Assert.AreEqual(DragonInteraction.Curious, React(DragonPersonalityStyle.Curious, DragonReaction.Petted));
            Assert.AreEqual(DragonInteraction.LookAround, React(DragonPersonalityStyle.Shy, DragonReaction.Petted), "shy: a wary look first");
            Assert.AreEqual(DragonInteraction.Eat, React(DragonPersonalityStyle.Calm, DragonReaction.Fed));

            var p = Planner(Walker, DragonPersonalityStyle.Playful);
            var eat = p.Update(0.1f, DragonBehaviorIntent.Eat, 0f, 0f);
            Assert.AreEqual(DragonInteraction.Eat, eat.trigger);
            p.React(DragonReaction.Petted);
            var next = Run(p, DragonBehaviorIntent.Idle, 3f);
            int happyAt = next.FindIndex(x => x.trigger == DragonInteraction.Happy);
            Assert.GreaterOrEqual(happyAt, 13, "Eat (1.5 s) finishes before the reaction plays");

            var sleeper = Planner(Walker, DragonPersonalityStyle.Playful);
            Run(sleeper, DragonBehaviorIntent.Sleep, 1f);
            sleeper.React(DragonReaction.Called);
            var s = sleeper.Update(0.1f, DragonBehaviorIntent.Sleep, 0f, 0f);
            Assert.AreEqual(DragonInteraction.None, s.trigger);
            StringAssert.Contains("asleep", s.note);
        }

        private static DragonInteraction React(DragonPersonalityStyle style, DragonReaction r)
        {
            var p = Planner(Walker, style);
            p.Update(0.1f, DragonBehaviorIntent.Idle, 0f, 0f);
            p.React(r);
            return p.Update(0.1f, DragonBehaviorIntent.Idle, 0f, 0f).trigger;
        }

        [Test]
        public void IdleVariations_AreDeterministic_PersonalityPaced_AndOnlyFromAvailableClips()
        {
            var a = Run(Planner(Walker, DragonPersonalityStyle.Playful, 11), DragonBehaviorIntent.Idle, 600f).Select(x => x.trigger).ToList();
            var b = Run(Planner(Walker, DragonPersonalityStyle.Playful, 11), DragonBehaviorIntent.Idle, 600f).Select(x => x.trigger).ToList();
            CollectionAssert.AreEqual(a, b, "same seed, same idle life");
            int playful = a.Count(x => x != DragonInteraction.None);
            int calm = Run(Planner(Walker, DragonPersonalityStyle.Calm, 11), DragonBehaviorIntent.Idle, 600f).Count(x => x.trigger != DragonInteraction.None);
            Assert.Greater(playful, calm, "playful dragons fidget more often than calm ones");
            Assert.Greater(calm, 10);
            var shy = Run(Planner(Walker, DragonPersonalityStyle.Shy, 3), DragonBehaviorIntent.Idle, 900f).Select(x => x.trigger).Where(x => x != DragonInteraction.None).ToList();
            Assert.Greater(shy.Count(x => x == DragonInteraction.LookAround), shy.Count(x => x == DragonInteraction.Happy), "shy: mostly wary looks");
            var old = Run(Planner(OldModel, DragonPersonalityStyle.Curious), DragonBehaviorIntent.Idle, 300f).Select(x => x.trigger).Where(x => x != DragonInteraction.None).ToList();
            Assert.IsTrue(old.All(x => x == DragonInteraction.Happy), "old asset: only the clips it has");
            Assert.IsTrue(Run(Planner(Walker), DragonBehaviorIntent.Walk, 300f).All(x => x.trigger == DragonInteraction.None), "no idle variation while walking");
        }

        [Test]
        public void TurningOnTheSpot_Steps_AndRecoverIsGentle()
        {
            var p = Planner(Walker);
            Assert.AreEqual(DragonLocomotion.Walk, p.Update(0.1f, DragonBehaviorIntent.Idle, 0f, 120f).locomotion, "turning fast: step");
            Assert.AreEqual(DragonLocomotion.Idle, p.Update(0.1f, DragonBehaviorIntent.Idle, 0f, 10f).locomotion);
            var r = Planner(Walker, DragonPersonalityStyle.Playful).Update(0.1f, DragonBehaviorIntent.Recover, 0f, 0f);
            Assert.AreEqual(DragonLocomotion.Rest, r.locomotion);
            Assert.Less(r.tempo, DragonPersonalityPresentation.For(DragonPersonalityStyle.Playful).tempo, "slower, gentle");
        }

        [Test]
        public void EnvironmentHints_MapToIntents_UnknownIsNone()
        {
            Assert.AreEqual(DragonBehaviorIntent.Drink, DragonBehaviorHints.FromEnvironmentHint("drink"));
            Assert.AreEqual(DragonBehaviorIntent.Bask, DragonBehaviorHints.FromEnvironmentHint("BASK"));
            Assert.AreEqual(DragonBehaviorIntent.Curious, DragonBehaviorHints.FromEnvironmentHint("sniff"));
            Assert.AreEqual(DragonBehaviorIntent.LookAround, DragonBehaviorHints.FromEnvironmentHint("look_up"));
            Assert.AreEqual(DragonBehaviorIntent.None, DragonBehaviorHints.FromEnvironmentHint("dance"));
            Assert.AreEqual(DragonPersonalityStyle.Shy, DragonPersonalityPresentation.StyleFromName("Shy"));
            Assert.AreEqual(DragonPersonalityStyle.Unspecified, DragonPersonalityPresentation.StyleFromName("grumpy"));
        }

        [Test]
        public void HeadLook_ClampsIgnoresBehind_SplitsAndSmooths()
        {
            float y, p;
            HeadLookSolver.Angles(1f, 0f, 1f, 90f, 30f, out y, out p);
            Assert.AreEqual(45f, y, 1e-3f);
            HeadLookSolver.Angles(10f, 10f, 0.1f, 55f, 25f, out y, out p);
            Assert.AreEqual(55f, y, 1e-3f); Assert.AreEqual(25f, p, 1e-3f);
            HeadLookSolver.Angles(0f, 0f, -1f, 55f, 25f, out y, out p);
            Assert.AreEqual(0f, y); Assert.AreEqual(0f, p);
            float n, h;
            HeadLookSolver.Split(40f, 0.25f, out n, out h);
            Assert.AreEqual(10f, n, 1e-4f); Assert.AreEqual(30f, h, 1e-4f);
            float v = 0f;
            for (int i = 0; i < 60; i++) v = HeadLookSolver.Smooth(v, 10f, 1f / 60f, 0.18f);
            Assert.That(v, Is.InRange(9.5f, 10f));
            Assert.AreEqual(5f, HeadLookSolver.Smooth(0f, 10f, 0.18f, 0.18f), 1e-4f, "half-life");
        }

        [Test]
        public void Blink_ClosedAsleep_MostlyOpenAwake_Deterministic()
        {
            var b = new BlinkScheduler(5);
            Assert.AreEqual(1f, b.Update(0.1f, true));
            var c = new BlinkScheduler(5);
            int closed = 0, blinks = 0; float last = 0f;
            for (int i = 0; i < 6000; i++) // 100 s at 60 fps
            {
                float w = c.Update(1f / 60f, false);
                if (w > 0.5f) closed++;
                if (last <= 0f && w > 0f) blinks++;
                last = w;
            }
            Assert.That(blinks, Is.InRange(14, 45), "every 2.5-6 s");
            Assert.Less(closed, 600, "eyes open > 90 % of the time");
            Assert.AreEqual(Sum(new BlinkScheduler(5)), Sum(new BlinkScheduler(5)), "same seed, same blinks");
        }

        private static float Sum(BlinkScheduler s) { float t = 0f; for (int i = 0; i < 600; i++) t += s.Update(0.05f, false); return t; }

        [Test]
        public void Catalog_ClipSeconds_UsesLengthsThenLegacyFields()
        {
            var e = new DragonVisualCatalog.Entry { eatSeconds = 2f, happySeconds = 1.5f,
                clipLengths = new[] { new DragonVisualCatalog.ClipLength { clip = "Drink", seconds = 2.5f } } };
            Assert.AreEqual(2.5f, DragonVisualCatalog.ClipSecondsOf(e, DragonInteraction.Drink));
            Assert.AreEqual(2f, DragonVisualCatalog.ClipSecondsOf(e, DragonInteraction.Eat), "pre-U1-CHAR.6 entries keep working");
            Assert.AreEqual(0f, DragonVisualCatalog.ClipSecondsOf(e, DragonInteraction.Stretch));
            Assert.AreEqual((int)DragonLocomotion.Sleep, 6, "Animator ints of older states never change");
            Assert.AreEqual((int)DragonInteraction.Happy, 2);
        }

        // ---- sync ----

        private class LegacyTarget : IDragonVisualTarget
        {
            public readonly Dictionary<string, DragonVisualState> State = new Dictionary<string, DragonVisualState>();
            public readonly List<string> Log = new List<string>();
            public bool IsLive(string uid) { return State.ContainsKey(uid); }
            public bool Spawn(DragonVisualSpawn s) { State[s.uid] = s.state; return true; }
            public void Despawn(string uid) { State.Remove(uid); }
            public bool SetStage(string uid, DragonVisualStage stage) { Log.Add("stage"); return true; }
            public bool ApplyState(string uid, DragonVisualState st) { State[uid] = st; return true; }
            public bool Trigger(string uid, DragonInteraction i) { Log.Add("trigger " + i); return true; }
        }

        private sealed class BehaviorTarget : LegacyTarget, IDragonBehaviorTarget
        {
            public string[] Clips = Walker;
            public float LastTempo = 1f; public bool LastLook;
            public DragonAnimationMap ClipsOf(string uid) { return IsLive(uid) ? new DragonAnimationMap(Clips) : null; }
            public float ClipSeconds(string uid, DragonInteraction c) { return 1f; }
            public void SetTempo(string uid, float t) { LastTempo = t; }
            public void SetLook(string uid, bool a, Vector3 w, float weight, float hold) { LastLook = a; }
        }

        private static SanctuaryDragonFrame F(DragonBehaviorIntent intent, DragonLocomotion loco = DragonLocomotion.Walk, DragonPersonalityStyle style = DragonPersonalityStyle.Unspecified)
        {
            return new SanctuaryDragonFrame { uid = "a", speciesId = "emberling", stage = DragonVisualStage.Adult, visible = true,
                locomotion = loco, intent = intent, personality = style, rotation = Quaternion.identity, hasLookTarget = true };
        }

        [Test]
        public void Sync_WithoutIntent_IsTheU5Behaviour()
        {
            var t = new BehaviorTarget();
            var sync = new SanctuaryDragonSync(t);
            sync.Sync(new[] { F(DragonBehaviorIntent.None, DragonLocomotion.Swim) });
            sync.Sync(new[] { F(DragonBehaviorIntent.None, DragonLocomotion.Swim) }, 1f);
            Assert.AreEqual(DragonLocomotion.Swim, t.State["a"].locomotion, "legacy: the frame's locomotion as is");
            Assert.IsNull(sync.PlannerOf("a"));
            var legacy = new LegacyTarget();
            var s2 = new SanctuaryDragonSync(legacy);
            s2.Sync(new[] { F(DragonBehaviorIntent.Sleep, DragonLocomotion.Walk) }, 0.1f);
            Assert.AreEqual(DragonLocomotion.Walk, legacy.State["a"].locomotion, "a U5 target ignores intents");
            Assert.IsTrue(s2.React("a", DragonReaction.Petted));
            CollectionAssert.Contains(legacy.Log, "trigger Happy");
        }

        [Test]
        public void Sync_WithIntent_PlansTriggersAndSwitchesBackCleanly()
        {
            var t = new BehaviorTarget();
            var sync = new SanctuaryDragonSync(t);
            sync.Sync(new[] { F(DragonBehaviorIntent.Sleep, style: DragonPersonalityStyle.Calm) }, 0.1f);
            Assert.AreEqual(DragonLocomotion.Sleep, t.State["a"].locomotion);
            Assert.IsFalse(t.LastLook, "no look-at while asleep");
            var r = sync.Sync(new[] { F(DragonBehaviorIntent.Idle, style: DragonPersonalityStyle.Calm) }, 0.1f);
            Assert.AreEqual(1, r.triggered);
            CollectionAssert.Contains(t.Log, "trigger Wake");
            Assert.AreEqual(DragonPersonalityPresentation.For(DragonPersonalityStyle.Calm).tempo, t.LastTempo);
            for (int i = 0; i < 20; i++) sync.Sync(new[] { F(DragonBehaviorIntent.Idle, style: DragonPersonalityStyle.Calm) }, 0.1f);
            Assert.IsTrue(t.LastLook, "awake and idle: the head may follow the target");
            sync.React("a", DragonReaction.Petted);
            sync.Sync(new[] { F(DragonBehaviorIntent.Idle, style: DragonPersonalityStyle.Calm) }, 0.1f);
            CollectionAssert.Contains(t.Log, "trigger Happy");
            sync.Sync(new[] { F(DragonBehaviorIntent.None, DragonLocomotion.Run) }, 0.1f);
            Assert.AreEqual(DragonLocomotion.Run, t.State["a"].locomotion);
            Assert.AreEqual(1f, t.LastTempo, "back to legacy: tempo reset");
            Assert.IsFalse(t.LastLook);
            Assert.IsNull(sync.PlannerOf("a"));
        }

#if DRAVIO_DOTNET_TESTS
        /// <summary>A target that itself allocates nothing per call (the logging fakes would).</summary>
        private sealed class LeanTarget : IDragonBehaviorTarget
        {
            private readonly HashSet<string> _live = new HashSet<string>();
            private readonly DragonAnimationMap _map = new DragonAnimationMap(Walker);
            public int Triggers;
            public bool IsLive(string uid) { return _live.Contains(uid); }
            public bool Spawn(DragonVisualSpawn s) { _live.Add(s.uid); return true; }
            public void Despawn(string uid) { _live.Remove(uid); }
            public bool SetStage(string uid, DragonVisualStage stage) { return true; }
            public bool ApplyState(string uid, DragonVisualState st) { return true; }
            public bool Trigger(string uid, DragonInteraction i) { Triggers++; return true; }
            public DragonAnimationMap ClipsOf(string uid) { return _map; }
            public float ClipSeconds(string uid, DragonInteraction c) { return 1f; }
            public void SetTempo(string uid, float tempo) { }
            public void SetLook(string uid, bool a, Vector3 w, float weight, float hold) { }
        }

        [Test]
        public void Sync_SteadyState_AllocatesNothingPerFrame()
        {
            // 30 dragons, every intent and personality: no per-frame garbage on mobile (measured in .NET only;
            // Unity's Mono does not report per-thread allocations). A lambda hoisted into Behave once cost 32 B/dragon/frame.
            var sync = new SanctuaryDragonSync(new LeanTarget());
            var frames = Enumerable.Range(0, 30).Select(i => new SanctuaryDragonFrame
            {
                uid = "d" + i, speciesId = "emberling", stage = DragonVisualStage.Adult, visible = true, rotation = Quaternion.identity,
                intent = i < 5 ? DragonBehaviorIntent.None : (DragonBehaviorIntent)(1 + i % 16), personality = (DragonPersonalityStyle)(i % 5), hasLookTarget = true,
            }).ToArray();
            for (int k = 0; k < 300; k++) sync.Sync(frames, 1f / 30f);
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int k = 0; k < 300; k++) sync.Sync(frames, 1f / 30f);
            long bytes = System.GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Less(bytes, 300L, "under 1 byte per frame = nothing allocated per frame (one-off JIT bytes allowed)");
        }
#endif

        [Test]
        public void Sync_StageChangeRebuildsPlannerForTheNewModel_AndThirtyDragonsStayCheap()
        {
            var t = new BehaviorTarget { Clips = OldModel };
            var sync = new SanctuaryDragonSync(t);
            var f = F(DragonBehaviorIntent.Bask);
            f.stage = DragonVisualStage.Baby;
            sync.Sync(new[] { f }, 0.1f);
            Assert.AreEqual(DragonLocomotion.Sleep, t.State["a"].locomotion, "Baby model has no Bask/Rest");
            t.Clips = Walker;
            f.stage = DragonVisualStage.Adult;
            sync.Sync(new[] { f }, 0.1f);
            Assert.AreEqual(DragonLocomotion.Bask, t.State["a"].locomotion, "Adult model basks");

            var many = new BehaviorTarget();
            var s30 = new SanctuaryDragonSync(many);
            var frames = Enumerable.Range(0, 30).Select(i => new SanctuaryDragonFrame
            {
                uid = "d" + i, speciesId = "emberling", stage = DragonVisualStage.Adult, visible = true, rotation = Quaternion.identity,
                intent = (DragonBehaviorIntent)(1 + i % 16), personality = (DragonPersonalityStyle)(i % 5),
            }).ToArray();
            int triggered = 0, substituted = 0;
            for (int k = 0; k < 600; k++) { var rep = s30.Sync(frames, 1f / 30f); triggered += rep.triggered; substituted += rep.substituted; Assert.AreEqual(0, rep.failed); }
            Assert.AreEqual(30, s30.ShownCount);
            Assert.Greater(triggered, 30, "one-shots keep playing");
            Assert.Greater(substituted, 0, "Swim/Fly/Burrow intents on a walker are reported, not faked");
            Assert.IsTrue(many.State.Values.All(s => s.locomotion != DragonLocomotion.Swim && s.locomotion != DragonLocomotion.Fly));
        }
    }
}
