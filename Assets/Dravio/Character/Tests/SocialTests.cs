using System;
using System.Collections.Generic;
using System.Linq;
using Dravio.Character.Integration;
using NUnit.Framework;
using UnityEngine;

namespace Dravio.Character.Tests
{
    /// <summary>
    /// U1-CHAR.8 pair acts: both frames must name each other; roles (older leads, Nurture guardian), spacing from the
    /// bodies, synchronized starts with the responder's latency, turn-taking with role reversal in play, phase sync
    /// while resting together, Adult-Baby nurture, rejections, expressions, and no per-frame allocation.
    /// Pure logic (Unity + dotnet).
    /// </summary>
    public sealed class SocialTests
    {
        private static readonly string[] Walker = { "Idle", "Eat", "Sleep", "Happy", "Walk", "Run", "Rest", "Wake", "LookAround", "Curious", "Drink", "Stretch", "Bask", "Greet", "Nuzzle" };
        private static readonly string[] Swimmer = { "Idle", "Eat", "Sleep", "Happy", "Swim", "Rest", "Wake", "LookAround", "Curious", "Greet", "SwimIdle", "SwimRest", "Nuzzle" };
        private const float Dt = 0.1f;

        private sealed class SocialTarget : IDragonSocialTarget
        {
            public readonly Dictionary<string, string[]> Clips = new Dictionary<string, string[]>();
            public readonly HashSet<string> Live = new HashSet<string>();
            public readonly List<(int tick, string uid, DragonInteraction clip)> Triggers = new List<(int, string, DragonInteraction)>();
            public readonly Dictionary<string, DragonVisualState> State = new Dictionary<string, DragonVisualState>();
            public readonly Dictionary<string, (bool active, Vector3 target, float hold)> Look = new Dictionary<string, (bool, Vector3, float)>();
            public readonly Dictionary<string, float> Tempo = new Dictionary<string, float>();
            public readonly Dictionary<string, DragonExpressionState> Expr = new Dictionary<string, DragonExpressionState>();
            public readonly List<(string follower, string leader)> Phase = new List<(string, string)>();
            public readonly DragonVisualCatalog.MotionInfo Info = new DragonVisualCatalog.MotionInfo { bodyLength = 1.6f, bodyWidth = 0.5f, bodyHeight = 0.9f };
            public int Tick;
            public bool IsLive(string uid) { return Live.Contains(uid); }
            public bool Spawn(DragonVisualSpawn s) { Live.Add(s.uid); return true; }
            public void Despawn(string uid) { Live.Remove(uid); }
            public bool SetStage(string uid, DragonVisualStage stage) { return true; }
            public bool ApplyState(string uid, DragonVisualState st) { State[uid] = st; return true; }
            public bool Trigger(string uid, DragonInteraction i) { Triggers.Add((Tick, uid, i)); return true; }
            public DragonAnimationMap ClipsOf(string uid) { return Live.Contains(uid) ? new DragonAnimationMap(Clips.TryGetValue(uid, out var c) ? c : Walker) : null; }
            public float ClipSeconds(string uid, DragonInteraction c) { return 1f; }
            public void SetTempo(string uid, float t) { Tempo[uid] = t; }
            public void SetLook(string uid, bool a, Vector3 w, float weight, float hold) { Look[uid] = (a, w, hold); }
            public DragonVisualCatalog.MotionInfo MotionInfoOf(string uid) { return Info; }
            public void SetHidden(string uid, bool hidden) { }
            public void SetMotionShape(string uid, DragonMotionMode mode, float turn, float v, float s) { }
            public void SetExpression(string uid, float tail, float ear) { }
            public bool TryGetHeadPosition(string uid, out Vector3 p) { p = new Vector3(uid == "a" ? 1f : 2f, 0.7f, 0f); return Live.Contains(uid); }
            public void SyncPhase(string follower, string leader) { Phase.Add((follower, leader)); }
            public void SetExpressionState(string uid, DragonExpressionState s) { Expr[uid] = s; }
        }

        private static SanctuaryDragonFrame F(string uid, string partner, DragonSocialAct act, float x, DragonVisualStage stage = DragonVisualStage.Adult,
                                              DragonBehaviorIntent intent = DragonBehaviorIntent.Idle)
        {
            return new SanctuaryDragonFrame { uid = uid, speciesId = "emberling", stage = stage, visible = true, rotation = Quaternion.identity,
                position = new Vector3(x, 0f, 0f), intent = intent, social = act, partnerUid = partner };
        }

        private static float Spacing(DragonSocialAct act, SocialTarget t) { return DragonSocialCoordinator.DesiredSpacing(act, t.Info, t.Info); }

        private static void Run(SanctuaryDragonSync sync, SocialTarget t, SanctuaryDragonFrame[] frames, float seconds)
        {
            for (float s = 0f; s < seconds - 1e-4f; s += Dt) { sync.Sync(frames, Dt); t.Tick++; }
        }

        [Test]
        public void PairAct_NeedsBothFramesToNameEachOther()
        {
            var t = new SocialTarget();
            var sync = new SanctuaryDragonSync(t);
            float d = Spacing(DragonSocialAct.Greet, t);
            var frames = new[] { F("a", "b", DragonSocialAct.Greet, 0f), F("b", null, DragonSocialAct.None, d) };
            Run(sync, t, frames, 2f);
            Assert.AreEqual(DragonSocialPhase.WaitingForPartner, sync.SocialStatusOf("a").phase);
            Assert.IsFalse(t.Triggers.Any(x => x.clip == DragonInteraction.Greet), "no one-sided act: the partner is never driven");
            frames[1] = F("b", "a", DragonSocialAct.Play, d);
            Run(sync, t, frames, 0.5f);
            Assert.AreEqual(DragonSocialPhase.WaitingForPartner, sync.SocialStatusOf("b").phase, "different acts");
        }

        [Test]
        public void Greet_StartsTogether_ResponderAnswersAfterLatency_FacesThePartner_ThenDone()
        {
            var t = new SocialTarget();
            var sync = new SanctuaryDragonSync(t);
            float d = Spacing(DragonSocialAct.Greet, t);
            Assert.AreEqual(0.5f * (1.6f + 1.6f) + 0.16f, d, 1e-5f, "snouts meet at half the two lengths, plus a small gap");
            var frames = new[] { F("b", "a", DragonSocialAct.Greet, d), F("a", "b", DragonSocialAct.Greet, 0f) };
            Run(sync, t, frames, 5f);
            var la = t.Triggers.Where(x => x.uid == "a" && x.clip == DragonInteraction.Greet).ToList();
            var lb = t.Triggers.Where(x => x.uid == "b" && x.clip == DragonInteraction.Greet).ToList();
            Assert.AreEqual(1, la.Count, "leader (same stage: smaller uid) greets once"); Assert.AreEqual(1, lb.Count);
            Assert.AreEqual(3, lb[0].tick - la[0].tick, "responder answers 0.3 s later");
            Assert.AreEqual(DragonSocialRole.Leader, sync.SocialStatusOf("a").role);
            Assert.AreEqual(DragonSocialRole.Responder, sync.SocialStatusOf("b").role);
            Assert.IsTrue(t.Look["a"].active && t.Look["a"].target.x == 2f, "a looks at b's head");
            Assert.IsTrue(t.Look["b"].active && t.Look["b"].target.x == 1f);
            Assert.AreEqual(DragonSocialPhase.Done, sync.SocialStatusOf("a").phase);
            Assert.IsTrue(sync.SocialStatusOf("a").faceEachOther);
            Assert.AreEqual(DragonExpression.Happy, t.Expr["a"].expression);
            Assert.IsFalse(t.Triggers.Any(x => x.clip == DragonInteraction.LookAround || x.clip == DragonInteraction.Stretch), "no idle variations during the act");
        }

        [Test]
        public void OutOfRange_KeepsOwnIntents_WatchesThePartner_ReportsSpacing()
        {
            var t = new SocialTarget();
            var sync = new SanctuaryDragonSync(t);
            var frames = new[] { F("a", "b", DragonSocialAct.Nuzzle, 0f, intent: DragonBehaviorIntent.Walk), F("b", "a", DragonSocialAct.Nuzzle, 6f) };
            Run(sync, t, frames, 1f);
            var s = sync.SocialStatusOf("a");
            Assert.AreEqual(DragonSocialPhase.OutOfRange, s.phase);
            Assert.AreEqual(6f, s.distance, 1e-4f);
            Assert.AreEqual(Spacing(DragonSocialAct.Nuzzle, t), s.desiredSpacing, 1e-5f);
            Assert.IsFalse(s.inRange);
            Assert.AreEqual(DragonLocomotion.Walk, t.State["a"].locomotion, "the Sanctuary is walking it over: its own intent applies");
            Assert.IsTrue(t.Look["a"].active, "already watching the partner");
            Assert.IsFalse(t.Triggers.Any(x => x.clip == DragonInteraction.Nuzzle));
            frames[0].position = new Vector3(6f - Spacing(DragonSocialAct.Nuzzle, t), 0f, 0f);
            Run(sync, t, frames, 3f);
            Assert.AreEqual(1, t.Triggers.Count(x => x.uid == "a" && x.clip == DragonInteraction.Nuzzle), "in range: the leader nuzzles");
            Assert.AreEqual(DragonExpression.Affection, t.Expr["a"].expression);
        }

        [Test]
        public void Play_InvitesWithAPlayBow_ThenTakesTurnsWithRoleReversal()
        {
            var t = new SocialTarget();
            var sync = new SanctuaryDragonSync(t);
            float d = Spacing(DragonSocialAct.Play, t);
            var frames = new[] { F("a", "b", DragonSocialAct.Play, 0f), F("b", "a", DragonSocialAct.Play, d) };
            Run(sync, t, frames, 20f);
            var first = t.Triggers.First();
            Assert.AreEqual(("a", DragonInteraction.Stretch), (first.uid, first.clip), "the leader opens with a play bow");
            int beats = sync.SocialStatusOf("a").beats;
            Assert.GreaterOrEqual(beats, 6, "several turns in 20 s");
            Assert.Greater(t.Triggers.Count(x => x.uid == "a"), 3); Assert.Greater(t.Triggers.Count(x => x.uid == "b"), 3, "both take turns");
            // the actor of each turn alternates: the first trigger after each answer is by the other dragon
            var starts = new List<string>();
            for (int i = 0; i < t.Triggers.Count; i += 2) starts.Add(t.Triggers[i].uid);
            Assert.IsTrue(starts.Take(6).Select((u, i) => u == (i % 2 == 0 ? "a" : "b")).All(x => x), "role reversal: a, b, a, b, ...");
            Assert.AreEqual("b", t.Triggers[1].uid, "the partner answers each move");
            Assert.AreEqual(DragonInteraction.Stretch, t.Triggers[6].clip, "a play bow again every third turn keeps it read as play");
            Assert.AreEqual(DragonExpression.Playful, t.Expr["b"].expression);
            Assert.Greater(t.Expr["b"].tailWagDeg, 0f);
        }

        [Test]
        public void RestTogether_BothLieDown_SideBySide_BreathingInPhase()
        {
            var t = new SocialTarget();
            var sync = new SanctuaryDragonSync(t);
            float d = Spacing(DragonSocialAct.RestTogether, t);
            Assert.AreEqual(0.5f + 0.08f, d, 1e-5f, "side by side: half the two widths");
            var frames = new[] { F("a", "b", DragonSocialAct.RestTogether, 0f), F("b", "a", DragonSocialAct.RestTogether, d) };
            Run(sync, t, frames, 3f);
            Assert.AreEqual(DragonLocomotion.Rest, t.State["a"].locomotion); Assert.AreEqual(DragonLocomotion.Rest, t.State["b"].locomotion);
            Assert.AreEqual(1, t.Phase.Count, "phase synced once"); Assert.AreEqual(("b", "a"), t.Phase[0]);
            Assert.IsFalse(sync.SocialStatusOf("a").faceEachOther);
            Assert.AreEqual(0.3f, t.Look["a"].hold, 1e-5f, "occasional glances, not a stare");
            Assert.AreEqual(DragonExpression.Calm, t.Expr["a"].expression);
        }

        [Test]
        public void Nurture_OlderGuardsAndNuzzles_BabyPlays_FasterTempo_AndNeedsABaby()
        {
            var t = new SocialTarget();
            var sync = new SanctuaryDragonSync(t);
            float d = Spacing(DragonSocialAct.Nurture, t);
            // uid order would make "a" lead; the stage decides: the Adult "z" is the guardian
            var frames = new[] { F("a", "z", DragonSocialAct.Nurture, 0f, DragonVisualStage.Baby), F("z", "a", DragonSocialAct.Nurture, d, DragonVisualStage.Adult) };
            Run(sync, t, frames, 40f);
            Assert.AreEqual(DragonSocialRole.Guardian, sync.SocialStatusOf("z").role);
            Assert.AreEqual(DragonSocialRole.Young, sync.SocialStatusOf("a").role);
            Assert.Greater(t.Triggers.Count(x => x.uid == "a" && (x.clip == DragonInteraction.Happy || x.clip == DragonInteraction.Curious)), 3, "the Baby plays");
            Assert.GreaterOrEqual(t.Triggers.Count(x => x.uid == "z" && x.clip == DragonInteraction.Nuzzle), 1, "the guardian nuzzles it");
            Assert.AreEqual(t.Tempo["z"] * DragonSocialCoordinator.YoungTempoScale, t.Tempo["a"], 1e-4f, "same personality, the Baby moves a little quicker");
            Assert.AreEqual(0.85f, t.Look["z"].hold, 1e-5f, "the guardian keeps watching");
            Assert.AreEqual(DragonExpression.Calm, t.Expr["z"].expression); Assert.AreEqual(DragonExpression.Playful, t.Expr["a"].expression);

            var two = new[] { F("a", "z", DragonSocialAct.Nurture, 0f), F("z", "a", DragonSocialAct.Nurture, d) };
            Run(sync, t, two, 0.5f);
            Assert.AreEqual(DragonSocialPhase.Rejected, sync.SocialStatusOf("a").phase, "two Adults: no nurture");
        }

        [Test]
        public void DifferentMedia_AreRejected_AndEndingTheActRestoresOwnIntents()
        {
            var t = new SocialTarget();
            t.Clips["f"] = Swimmer;
            var sync = new SanctuaryDragonSync(t);
            float d = Spacing(DragonSocialAct.Greet, t);
            var frames = new[] { F("a", "f", DragonSocialAct.Greet, 0f), F("f", "a", DragonSocialAct.Greet, d) };
            Run(sync, t, frames, 1f);
            Assert.AreEqual(DragonSocialPhase.Rejected, sync.SocialStatusOf("a").phase, "a walker on land and a pure swimmer in water");

            var t2 = new SocialTarget();
            var s2 = new SanctuaryDragonSync(t2);
            var g = new[] { F("a", "b", DragonSocialAct.Greet, 0f, intent: DragonBehaviorIntent.Rest), F("b", "a", DragonSocialAct.Greet, d, intent: DragonBehaviorIntent.Rest) };
            Run(s2, t2, g, 1f);
            Assert.AreEqual(DragonLocomotion.Idle, t2.State["a"].locomotion, "the act holds the medium's idle");
            g[0].social = g[1].social = DragonSocialAct.None;
            Run(s2, t2, g, 1f);
            Assert.AreEqual(DragonLocomotion.Rest, t2.State["a"].locomotion, "act ended: the frame's own intent again");
            Assert.AreEqual(DragonSocialPhase.None, s2.SocialStatusOf("a").phase);
        }

        [Test]
        public void MissingClips_FallBackWithinTheModel()
        {
            var t = new SocialTarget();
            var noNuzzle = Walker.Where(c => c != "Nuzzle").ToArray();
            t.Clips["a"] = noNuzzle; t.Clips["b"] = noNuzzle;
            var sync = new SanctuaryDragonSync(t);
            float d = Spacing(DragonSocialAct.Nuzzle, t);
            Run(sync, t, new[] { F("a", "b", DragonSocialAct.Nuzzle, 0f), F("b", "a", DragonSocialAct.Nuzzle, d) }, 3f);
            Assert.AreEqual(DragonInteraction.Curious, t.Triggers.First(x => x.uid == "a").clip, "no Nuzzle clip: a sniff (Curious) toward the partner");
            Assert.AreEqual(DragonInteraction.Happy, t.Triggers.First(x => x.uid == "b").clip);
        }

        [Test]
        public void Expressions_FollowTheActAndClip_WithinLimits()
        {
            var shy = DragonExpressionSolver.For(DragonExpression.Shy, 0.6f);
            Assert.LessOrEqual(shy.tailCarriage, -0.6f, "shy tucks the tail whatever the personality carriage");
            Assert.Less(shy.neckRaiseDeg, 0f);
            var cur = DragonExpressionSolver.For(DragonExpression.Curious, 0f);
            Assert.Greater(cur.headTiltDeg, 0f); Assert.AreEqual(1f, cur.earPerk);
            var hap = DragonExpressionSolver.For(DragonExpression.Playful, 0.9f);
            Assert.AreEqual(1f, hap.tailCarriage, "clamped"); Assert.LessOrEqual(hap.tailWagDeg, DragonExpressionSolver.MaxWagDeg);
            Assert.AreEqual(DragonExpression.Sleepy, DragonExpressionSolver.FromContext(DragonInteraction.None, DragonLocomotion.Sleep, DragonPersonalityStyle.Playful));
            Assert.AreEqual(DragonExpression.Shy, DragonExpressionSolver.FromContext(DragonInteraction.LookAround, DragonLocomotion.Idle, DragonPersonalityStyle.Shy));
            Assert.AreEqual(DragonExpression.Neutral, DragonExpressionSolver.FromContext(DragonInteraction.TakeOff, DragonLocomotion.Fly, DragonPersonalityStyle.Playful));
            Assert.IsTrue(DragonExpressionSolver.GazeShiftBlinks(25f)); Assert.IsFalse(DragonExpressionSolver.GazeShiftBlinks(10f));
            Assert.AreEqual(16, (int)DragonInteraction.Nuzzle, "appended: Animator ints unchanged");
        }

        [Test]
        public void HeadEstimate_WithoutAHeadBone_IsForwardAndUp()
        {
            var f = F("a", null, DragonSocialAct.None, 1f);
            var h = SanctuaryDragonSync.EstimateHead(f, new DragonVisualCatalog.MotionInfo { bodyLength = 2f, bodyHeight = 1f });
            Assert.AreEqual(1f, h.x, 1e-5f); Assert.AreEqual(0.8f, h.y, 1e-5f); Assert.AreEqual(0.9f, h.z, 1e-5f);
        }

        private sealed class EventTarget : IDragonMotionTarget
        {
            public readonly HashSet<string> Live = new HashSet<string>();
            public readonly DragonVisualCatalog.MotionInfo Info = new DragonVisualCatalog.MotionInfo { takeOffLiftOff = 0.32f, landTouchDown = 0.6f, takeOffRootOffset = 0.39f, landRootOffset = 0.31f };
            private readonly DragonAnimationMap _map = new DragonAnimationMap(new[] { "Idle", "Walk", "Fly", "TakeOff", "Land", "Glide", "Happy", "Sleep", "Eat" });
            public bool IsLive(string uid) { return Live.Contains(uid); }
            public bool Spawn(DragonVisualSpawn s) { Live.Add(s.uid); return true; }
            public void Despawn(string uid) { Live.Remove(uid); }
            public bool SetStage(string uid, DragonVisualStage stage) { return true; }
            public bool ApplyState(string uid, DragonVisualState st) { return true; }
            public bool Trigger(string uid, DragonInteraction i) { return true; }
            public DragonAnimationMap ClipsOf(string uid) { return _map; }
            public float ClipSeconds(string uid, DragonInteraction c) { return 1f; }
            public void SetTempo(string uid, float t) { }
            public void SetLook(string uid, bool a, Vector3 w, float weight, float hold) { }
            public DragonVisualCatalog.MotionInfo MotionInfoOf(string uid) { return Info; }
            public void SetHidden(string uid, bool hidden) { }
            public void SetMotionShape(string uid, DragonMotionMode mode, float turn, float v, float s) { }
            public void SetExpression(string uid, float tail, float ear) { }
        }

        [Test]
        public void MotionEvents_FireOnceAtTheAuthoredTimes()
        {
            var t = new EventTarget();
            var sync = new SanctuaryDragonSync(t);
            var f = F("z", null, DragonSocialAct.None, 0f, intent: DragonBehaviorIntent.Fly);
            var events = new List<(int tick, DragonMotionEvent e)>();
            for (int k = 0; k < 40; k++)
            {
                if (k == 20) f.intent = DragonBehaviorIntent.Idle;
                sync.Sync(new[] { f }, Dt);
                var e = sync.MotionStatusOf("z").motionEvent;
                if (e != DragonMotionEvent.None) events.Add((k, e));
            }
            Assert.AreEqual(2, events.Count, string.Join(",", events));
            Assert.AreEqual(DragonMotionEvent.LiftOff, events[0].e);
            Assert.AreEqual(4, events[0].tick, "TakeOff starts at tick 0 and lifts off at 0.32 of 1 s: the 4th step");
            Assert.AreEqual(DragonMotionEvent.TouchDown, events[1].e);
            Assert.AreEqual(26, events[1].tick, "Land starts at tick 20 and touches down at 0.6 s");
        }

        [Test]
        public void TerrainAlign_FollowsAShareOfTheSlope_Clamped()
        {
            float pitch, roll;
            // fore feet 0.1 m higher than the hind feet over 0.5 m: a 11.3 deg slope, body follows 60 %
            TerrainAlignSolver.Compute(0.1f, 0f, 0f, 0f, 0.5f, 0.3f, TerrainAlignSolver.DefaultShare, out pitch, out roll);
            Assert.AreEqual(0.6 * Math.Atan2(0.1, 0.5) * 180 / Math.PI, pitch, 1e-3); Assert.AreEqual(0f, roll);
            TerrainAlignSolver.Compute(0f, 0f, 0.3f, -0.3f, 0.5f, 0.3f, 1f, out pitch, out roll);
            Assert.AreEqual(TerrainAlignSolver.MaxRollDeg, roll, 1e-4f, "clamped");
            TerrainAlignSolver.Compute(0.5f, 0f, 0f, 0f, 0f, 0f, 1f, out pitch, out roll);
            Assert.AreEqual(0f, pitch, "no span (one pair of feet): no tilt");
        }

        [Test]
        public void TailWag_GrowsToTheTip_AndSumsToTheAmplitude()
        {
            int n = 3; float sum = 0f;
            for (int i = 0; i < n; i++) sum += Math.Abs(MotionShapeSolver.TailWagDeg(20f, 0.25f + 0.6f * i / (2f * (float)Math.PI), i, n));
            Assert.AreEqual(20f, sum, 1e-3f, "at the wave crest of each bone the angles add up to the tip amplitude");
            Assert.Less(Math.Abs(MotionShapeSolver.TailWagDeg(20f, 0.25f, 0, n)), Math.Abs(MotionShapeSolver.TailWagDeg(20f, 0.25f + 0.6f * 2 / (2f * (float)Math.PI), 2, n)));
            Assert.AreEqual(0f, MotionShapeSolver.TailWagDeg(20f, 0.3f, 0, 0));
        }

        [Test]
        public void MobilePolicy_DetailLayersOnlyNear_SkinBonesByLod_GazeBlink()
        {
            var p = new UpdateRatePolicy();
            Assert.IsTrue(p.DetailFor(1, 5f).layers && p.DetailFor(1, 5f).blink);
            Assert.IsTrue(p.DetailFor(1, 10f).layers); Assert.IsFalse(p.DetailFor(1, 10f).blink, "blinks only close up");
            Assert.IsFalse(p.DetailFor(1, 20f).layers); Assert.IsFalse(p.DetailFor(2, 5f).layers, "throttled dragons skip the layers");
            Assert.AreEqual(new[] { 4, 2, 1, 1 }, Enumerable.Range(0, 4).Select(UpdateRatePolicy.SkinBonesForLod).ToArray());
            var b = new BlinkScheduler(7);
            Assert.AreEqual(0f, b.Update(0.01f, false), "not yet");
            b.BlinkNow();
            Assert.Greater(b.Update(0.03f, false), 0f, "a gaze shift starts a blink at once");
        }

#if DRAVIO_DOTNET_TESTS
        private sealed class LeanSocial : IDragonSocialTarget
        {
            private readonly HashSet<string> _live = new HashSet<string>();
            private readonly DragonAnimationMap _map = new DragonAnimationMap(Walker);
            private readonly DragonVisualCatalog.MotionInfo _info = new DragonVisualCatalog.MotionInfo { bodyLength = 1.6f, bodyWidth = 0.5f, bodyHeight = 0.9f };
            public bool IsLive(string uid) { return _live.Contains(uid); }
            public bool Spawn(DragonVisualSpawn s) { _live.Add(s.uid); return true; }
            public void Despawn(string uid) { _live.Remove(uid); }
            public bool SetStage(string uid, DragonVisualStage stage) { return true; }
            public bool ApplyState(string uid, DragonVisualState st) { return true; }
            public bool Trigger(string uid, DragonInteraction i) { return true; }
            public DragonAnimationMap ClipsOf(string uid) { return _map; }
            public float ClipSeconds(string uid, DragonInteraction c) { return 1f; }
            public void SetTempo(string uid, float tempo) { }
            public void SetLook(string uid, bool a, Vector3 w, float weight, float hold) { }
            public DragonVisualCatalog.MotionInfo MotionInfoOf(string uid) { return _info; }
            public void SetHidden(string uid, bool hidden) { }
            public void SetMotionShape(string uid, DragonMotionMode mode, float turn, float v, float s) { }
            public void SetExpression(string uid, float tail, float ear) { }
            public bool TryGetHeadPosition(string uid, out Vector3 p) { p = default(Vector3); return false; }
            public void SyncPhase(string follower, string leader) { }
            public void SetExpressionState(string uid, DragonExpressionState s) { }
        }

        [Test]
        public void ThirtyDragonsInPairActs_AllocateNothingPerFrame()
        {
            var acts = new[] { DragonSocialAct.Greet, DragonSocialAct.Play, DragonSocialAct.RestTogether, DragonSocialAct.Nuzzle, DragonSocialAct.Nurture };
            var info = new DragonVisualCatalog.MotionInfo { bodyLength = 1.6f, bodyWidth = 0.5f, bodyHeight = 0.9f };
            var frames = new List<SanctuaryDragonFrame>();
            for (int i = 0; i < 15; i++)
            {
                var act = acts[i % acts.Length];
                float d = DragonSocialCoordinator.DesiredSpacing(act, info, info);
                string a = "p" + i + "a", b = "p" + i + "b";
                frames.Add(F(a, b, act, 10f * i, act == DragonSocialAct.Nurture ? DragonVisualStage.Baby : DragonVisualStage.Adult));
                frames.Add(F(b, a, act, 10f * i + d));
            }
            var arr = frames.ToArray();
            var sync = new SanctuaryDragonSync(new LeanSocial());
            for (int k = 0; k < 300; k++) sync.Sync(arr, 1f / 30f);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int k = 0; k < 300; k++) sync.Sync(arr, 1f / 30f);
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Less(bytes, 300L, "no per-frame garbage with 15 pairs acting");
            Assert.AreEqual(DragonSocialPhase.Acting, sync.SocialStatusOf("p1a").phase);
        }
#endif
    }
}
