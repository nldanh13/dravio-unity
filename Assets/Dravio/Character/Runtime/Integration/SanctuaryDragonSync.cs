using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dravio.Character.Integration
{
    /// <summary>
    /// What the Unified Sanctuary layer tells the visual runtime about one dragon for one frame. It is built by
    /// the integration layer from the Domain snapshot plus the movement simulation; the Character runtime never
    /// reads Domain or Persistence types. <see cref="visible"/> = false hides the dragon (for example away on an
    /// expedition or sitting in the nest), which the integration layer decides from Domain state.
    /// </summary>
    [Serializable]
    public struct SanctuaryDragonFrame
    {
        public string uid;
        public string speciesId;
        public DragonVisualStage stage;
        public string variantId;
        public GenotypeEntry[] genotype;
        public bool visible;
        public DragonLocomotion locomotion;
        public float speed;
        public Vector3 position;
        public Quaternion rotation;

        // ---- U1-CHAR.6 (all optional: the defaults keep the U1-CHAR.5 behaviour) ----
        /// <summary>What the dragon is doing. None = show <see cref="locomotion"/> as is (legacy path).</summary>
        public DragonBehaviorIntent intent;
        /// <summary>Presentation style from the Domain Personality (see DragonPersonalityPresentation.StyleFromName).</summary>
        public DragonPersonalityStyle personality;
        /// <summary>Yaw rate (deg/s) of the simulation's heading; fast turns on the spot show stepping.</summary>
        public float turnRateDegPerSec;
        /// <summary>World point the head should follow (player, food, another dragon), when hasLookTarget.</summary>
        public bool hasLookTarget;
        public Vector3 lookTarget;

        // ---- U1-CHAR.7 (optional) ----
        /// <summary>Vertical speed (m/s, + = climbing) of the simulation, for pitch in water and in the air.</summary>
        public float verticalSpeed;

        // ---- U1-CHAR.8 (optional) ----
        /// <summary>Pair act with <see cref="partnerUid"/>. Shown only when BOTH frames name each other with the same act;
        /// the Sanctuary decides who meets whom, places them (SocialStatusOf.desiredSpacing) and ends it (None).</summary>
        public DragonSocialAct social;
        public string partnerUid;
    }

    /// <summary>The part of <see cref="DragonVisualManager"/> the sync drives (an interface so it is testable).</summary>
    public interface IDragonVisualTarget
    {
        bool IsLive(string uid);
        bool Spawn(DragonVisualSpawn spawn);
        void Despawn(string uid);
        bool SetStage(string uid, DragonVisualStage stage);
        bool ApplyState(string uid, DragonVisualState state);
        bool Trigger(string uid, DragonInteraction interaction);
    }

    /// <summary>
    /// Optional extension of <see cref="IDragonVisualTarget"/> for behaviour intents (U1-CHAR.6). A target that does
    /// not implement it still works: intents are then ignored and the frame's locomotion is shown (legacy path).
    /// </summary>
    public interface IDragonBehaviorTarget : IDragonVisualTarget
    {
        /// <summary>Clips the dragon's current model has (null if not live).</summary>
        DragonAnimationMap ClipsOf(string uid);
        /// <summary>Length in seconds of a one-shot clip of the dragon's model; &lt;= 0 if unknown.</summary>
        float ClipSeconds(string uid, DragonInteraction clip);
        /// <summary>Animator tempo for non-locomotion clips (1 = authored speed).</summary>
        void SetTempo(string uid, float tempo);
        /// <summary>Head look-at on top of the animation; weight 0..1, hold = fraction of time the look is held.</summary>
        void SetLook(string uid, bool active, Vector3 worldTarget, float weight, float hold);
    }

    /// <summary>
    /// Optional extension for the U1-CHAR.7 motion contract. Targets without it still get intents (the planner
    /// runs); the model is then never hidden underground and no motion shaping or root offsets are available.
    /// </summary>
    public interface IDragonMotionTarget : IDragonBehaviorTarget
    {
        /// <summary>Catalog motion metadata of the dragon's model (null when unknown).</summary>
        DragonVisualCatalog.MotionInfo MotionInfoOf(string uid);
        /// <summary>Hide/show the model without despawning it (underground).</summary>
        void SetHidden(string uid, bool hidden);
        /// <summary>Procedural shaping input: medium, signed turn rate (deg/s), vertical and forward speed (m/s).</summary>
        void SetMotionShape(string uid, DragonMotionMode mode, float turnRateDegPerSec, float verticalSpeed, float speed);
        /// <summary>Expression layer: tail carriage (-1 tucked .. +1 high, from the personality) and ear-fin perk (0..1).</summary>
        void SetExpression(string uid, float tailCarriage, float earPerk);
    }

    /// <summary>
    /// Optional extension for U1-CHAR.8 pair acts and expressions. Without it pair acts still run (head position is
    /// estimated from the body size, no phase sync) and only tail carriage and ear perk are shown.
    /// </summary>
    public interface IDragonSocialTarget : IDragonMotionTarget
    {
        /// <summary>World position of the Head bone (false when not live or no Head bone).</summary>
        bool TryGetHeadPosition(string uid, out Vector3 position);
        /// <summary>Plays the follower's current looping state from the leader's normalized time (same clip only).</summary>
        void SyncPhase(string followerUid, string leaderUid);
        /// <summary>Full expression layer (tail wag and carriage, ear fins, head tilt, neck).</summary>
        void SetExpressionState(string uid, DragonExpressionState state);
    }

    /// <summary>Counts of one <see cref="SanctuaryDragonSync.Sync"/> call (for logs and the performance report).</summary>
    public struct SanctuarySyncReport
    {
        public int spawned, despawned, stageChanged, updated, failed;
        /// <summary>U1-CHAR.6: one-shots started by the behaviour planner, and intents shown with another clip.</summary>
        public int triggered, substituted;
        public override string ToString()
        {
            return "spawned=" + spawned + " despawned=" + despawned + " stage=" + stageChanged + " updated=" + updated + " failed=" + failed
                + " triggered=" + triggered + " substituted=" + substituted;
        }
    }

    /// <summary>
    /// Keeps the displayed dragons equal to the list the Sanctuary layer passes each frame:
    /// new and visible → Spawn; gone or hidden → Despawn; species or variant changed → respawn; stage changed →
    /// SetStage (state kept); otherwise ApplyState. Order of the input does not matter; duplicate UIDs are an
    /// error (first wins, counted as failed). U1-CHAR.6: a frame with an intent (and a target implementing
    /// <see cref="IDragonBehaviorTarget"/>) goes through a per-dragon <see cref="DragonBehaviorPlanner"/>; the
    /// behaviour itself is still the Sanctuary's choice.
    /// </summary>
    public sealed class SanctuaryDragonSync
    {
        private struct Shown { public string speciesId; public string variantId; public DragonVisualStage stage; }

        private readonly IDragonVisualTarget _target;
        private readonly IDragonBehaviorTarget _behavior;
        private readonly IDragonMotionTarget _motion;
        private readonly IDragonSocialTarget _socialTarget;
        private readonly DragonSocialCoordinator _social = new DragonSocialCoordinator();
        private readonly HashSet<string> _socialUids = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, SanctuaryDragonFrame> _socialFrames = new Dictionary<string, SanctuaryDragonFrame>(StringComparer.Ordinal);
        private readonly SocialWorld _world;
        private readonly Dictionary<string, DragonMotionStatus> _status = new Dictionary<string, DragonMotionStatus>(StringComparer.Ordinal);
        private readonly Dictionary<string, DragonBehaviorPlanner> _planners = new Dictionary<string, DragonBehaviorPlanner>(StringComparer.Ordinal);
        private readonly Dictionary<string, DragonPersonalityStyle> _styles = new Dictionary<string, DragonPersonalityStyle>(StringComparer.Ordinal);
        private readonly HashSet<string> _behaving = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, Shown> _shown = new Dictionary<string, Shown>(StringComparer.Ordinal);
        private readonly HashSet<string> _seen = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _visibleNow = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> _gone = new List<string>();

        public SanctuaryDragonSync(IDragonVisualTarget target)
        {
            if (target == null) throw new ArgumentNullException("target");
            _target = target;
            _behavior = target as IDragonBehaviorTarget;
            _motion = target as IDragonMotionTarget;
            _socialTarget = target as IDragonSocialTarget;
            _world = new SocialWorld(this);
        }

        /// <summary>U1-CHAR.8: pair-act state of a dragon (phase, role, spacing the Sanctuary should place the pair at).</summary>
        public DragonSocialStatus SocialStatusOf(string uid)
        {
            DragonSocialStatus s;
            return _social.TryStatus(uid, out s) ? s : new DragonSocialStatus();
        }

        private sealed class SocialWorld : IDragonSocialWorld
        {
            private readonly SanctuaryDragonSync _s;
            public SocialWorld(SanctuaryDragonSync s) { _s = s; }
            public bool TryFrame(string uid, out SanctuaryDragonFrame frame) { return _s._socialFrames.TryGetValue(uid, out frame); }
            public DragonBehaviorPlanner PlannerOf(string uid) { return _s.PlannerOf(uid); }
            public DragonAnimationMap ClipsOf(string uid) { return _s._behavior != null ? _s._behavior.ClipsOf(uid) : null; }
            public DragonVisualCatalog.MotionInfo MotionInfoOf(string uid) { return _s._motion != null ? _s._motion.MotionInfoOf(uid) : null; }
            public void SyncPhase(string follower, string leader) { if (_s._socialTarget != null) _s._socialTarget.SyncPhase(follower, leader); }
            public Vector3 HeadOf(string uid, SanctuaryDragonFrame f)
            {
                Vector3 p;
                if (_s._socialTarget != null && _s._socialTarget.TryGetHeadPosition(uid, out p)) return p;
                return EstimateHead(f, MotionInfoOf(uid));
            }
        }

        /// <summary>Head estimate without a Head bone: 0.8 of the body height up and 0.45 of its length forward of the root.</summary>
        public static Vector3 EstimateHead(SanctuaryDragonFrame f, DragonVisualCatalog.MotionInfo info)
        {
            float h = info != null && info.bodyHeight > 0f ? 0.8f * info.bodyHeight : 1f;
            float l = info != null && info.bodyLength > 0f ? 0.45f * info.bodyLength : 0f;
            var q = f.rotation;
            if (q.x == 0f && q.y == 0f && q.z == 0f && q.w == 0f) q.w = 1f;
            // forward = q * (0, 0, 1)
            float fx = 2f * (q.x * q.z + q.w * q.y), fy = 2f * (q.y * q.z - q.w * q.x), fz = 1f - 2f * (q.x * q.x + q.y * q.y);
            return new Vector3(f.position.x + fx * l, f.position.y + h + fy * l, f.position.z + fz * l);
        }

        /// <summary>
        /// U1-CHAR.7: medium, transition, whether the Sanctuary may move the dragon now, visibility and the root offset to
        /// apply this frame. Updated by every Sync for dragons with an intent; legacy-path dragons report live, Ground,
        /// canTranslate true.
        /// </summary>
        public DragonMotionStatus MotionStatusOf(string uid)
        {
            DragonMotionStatus s;
            if (uid != null && _status.TryGetValue(uid, out s)) return s;
            return new DragonMotionStatus { live = IsShown(uid), canTranslate = true, visible = IsShown(uid) };
        }

        /// <summary>U1-CHAR.7: can the dragon start going into <paramref name="mode"/> now (checked before sending the intent).</summary>
        public DragonMotionReadiness ReadinessFor(string uid, DragonMotionMode mode)
        {
            if (!IsShown(uid)) return DragonMotionReadiness.NotLive;
            DragonBehaviorPlanner p;
            if (_planners.TryGetValue(uid, out p)) return p.ReadinessFor(mode);
            var map = _behavior != null ? _behavior.ClipsOf(uid) : null;
            if (map == null) return mode == DragonMotionMode.Ground ? DragonMotionReadiness.Ready : DragonMotionReadiness.Unsupported;
            return DragonMotionCapabilities.From(map).Supports(mode) ? DragonMotionReadiness.Ready : DragonMotionReadiness.Unsupported;
        }

        /// <summary>U1-CHAR.7: what the dragon's current model can show (species x stage), from its clips.</summary>
        public DragonMotionCapabilities CapabilitiesOf(string uid)
        {
            var map = _behavior != null && IsShown(uid) ? _behavior.ClipsOf(uid) : null;
            return DragonMotionCapabilities.From(map);
        }

        /// <summary>The last note of a dragon's planner (substitutions, dropped reactions), for logs and the Lab.</summary>
        public string LastNote(string uid) { string n; return uid != null && _notes.TryGetValue(uid, out n) ? n : null; }
        private readonly Dictionary<string, string> _notes = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Planner of a shown dragon driven by intents (null on the legacy path or when not shown).</summary>
        public DragonBehaviorPlanner PlannerOf(string uid) { DragonBehaviorPlanner p; return uid != null && _planners.TryGetValue(uid, out p) ? p : null; }

        public int ShownCount { get { return _shown.Count; } }
        public bool IsShown(string uid) { return uid != null && _shown.ContainsKey(uid); }

        /// <summary>Legacy entry (U1-CHAR.5): no time step, so idle variations and one-shot timers do not advance.</summary>
        public SanctuarySyncReport Sync(IList<SanctuaryDragonFrame> frames) { return Sync(frames, 0f); }

        /// <param name="deltaSeconds">Time since the previous Sync (drives one-shot timers and idle variations).</param>
        public SanctuarySyncReport Sync(IList<SanctuaryDragonFrame> frames, float deltaSeconds)
        {
            var report = new SanctuarySyncReport();
            _seen.Clear();
            _visibleNow.Clear();
            // U1-CHAR.8: pair acts are planned first, from both frames of each pair and the planners' last state
            _socialUids.Clear(); _socialFrames.Clear();
            if (frames != null && _behavior != null)
                for (int i = 0; i < frames.Count; i++)
                {
                    var f = frames[i];
                    if (f.social == DragonSocialAct.None || !f.visible || string.IsNullOrEmpty(f.uid) || _socialFrames.ContainsKey(f.uid)) continue;
                    _socialFrames[f.uid] = f; _socialUids.Add(f.uid);
                }
            if (_socialUids.Count > 0 || _social.HasState) _social.Plan(deltaSeconds, _socialUids, _world);
            if (frames != null)
                for (int i = 0; i < frames.Count; i++)
                {
                    SanctuaryDragonFrame f = frames[i];
                    if (string.IsNullOrEmpty(f.uid) || !_seen.Add(f.uid)) { report.failed++; continue; }
                    if (!f.visible) continue; // hidden: removed below like a missing one
                    _visibleNow.Add(f.uid);
                    var state = new DragonVisualState { locomotion = f.locomotion, speed = f.speed, position = f.position, rotation = f.rotation };
                    Shown s;
                    bool live = _shown.TryGetValue(f.uid, out s) && _target.IsLive(f.uid);
                    if (live && s.speciesId == f.speciesId && s.variantId == f.variantId)
                    {
                        if (s.stage != f.stage)
                        {
                            if (!_target.SetStage(f.uid, f.stage)) { report.failed++; Forget(f.uid); continue; }
                            report.stageChanged++;
                            s.stage = f.stage;
                            _shown[f.uid] = s;
                            _planners.Remove(f.uid); // the new model may have other clips
                        }
                        if (!Behave(f, state, deltaSeconds, ref report)) _target.ApplyState(f.uid, state);
                        report.updated++;
                        continue;
                    }
                    var spawn = new DragonVisualSpawn
                    {
                        uid = f.uid, speciesId = f.speciesId, stage = f.stage, variantId = f.variantId,
                        genotype = f.genotype ?? Array.Empty<GenotypeEntry>(), state = state,
                    };
                    if (_target.Spawn(spawn))
                    {
                        _shown[f.uid] = new Shown { speciesId = f.speciesId, variantId = f.variantId, stage = f.stage };
                        _planners.Remove(f.uid);
                        report.spawned++;
                        Behave(f, state, 0f, ref report);
                    }
                    else { report.failed++; Forget(f.uid); }
                }
            _gone.Clear();
            foreach (var uid in _shown.Keys) if (!_visibleNow.Contains(uid)) _gone.Add(uid);
            foreach (var uid in _gone) { _target.Despawn(uid); _shown.Remove(uid); DropBehavior(uid); report.despawned++; }
            return report;
        }

        /// <summary>One-shot interaction (Eat, Happy, ...) for a shown dragon, bypassing the planner.</summary>
        public bool Trigger(string uid, DragonInteraction interaction)
        {
            return IsShown(uid) && _target.Trigger(uid, interaction);
        }

        /// <summary>
        /// A player interaction (pet, feed, call). With intents the dragon answers in its personality's way on the
        /// next Sync (a sleeping dragon is not disturbed); on the legacy path Petted/Called play Happy and Fed plays Eat.
        /// </summary>
        public bool React(string uid, DragonReaction reaction)
        {
            if (!IsShown(uid) || reaction == DragonReaction.None) return false;
            DragonBehaviorPlanner p;
            if (_planners.TryGetValue(uid, out p)) { p.React(reaction); return true; }
            return _target.Trigger(uid, reaction == DragonReaction.Fed ? DragonInteraction.Eat : DragonInteraction.Happy);
        }

        /// <summary>Intent path: planner -> state + one-shot + tempo + look. False = legacy path (caller applies the state).</summary>
        private bool Behave(SanctuaryDragonFrame f, DragonVisualState state, float dt, ref SanctuarySyncReport report)
        {
            DragonSocialDirective sd = default(DragonSocialDirective);
            bool social = _behavior != null && _social.TryDirective(f.uid, out sd);
            if (social && sd.intent != DragonBehaviorIntent.None) f.intent = sd.intent;
            else if (social && f.intent == DragonBehaviorIntent.None) f.intent = DragonBehaviorIntent.Idle; // a pair act needs the planner
            if (_behavior == null || f.intent == DragonBehaviorIntent.None)
            {
                if (_behaving.Remove(f.uid))
                {
                    _behavior.SetTempo(f.uid, 1f); _behavior.SetLook(f.uid, false, default(Vector3), 0f, 1f);
                    if (_motion != null)
                    {
                        _motion.SetHidden(f.uid, false); _motion.SetMotionShape(f.uid, DragonMotionMode.Ground, 0f, 0f, 0f);
                        _motion.SetExpression(f.uid, 0f, 0f);
                    }
                }
                _status.Remove(f.uid);
                _planners.Remove(f.uid);
                return false;
            }
            DragonBehaviorPlanner p;
            DragonPersonalityStyle style;
            if (!_planners.TryGetValue(f.uid, out p))
            {
                p = CreatePlanner(f.uid, f.personality);
                if (p == null) return false;
                DragonMotionStatus prev;
                if (_status.TryGetValue(f.uid, out prev)) p.RestoreMode(prev.mode); // grown into a new model: same medium
            }
            else if (_styles.TryGetValue(f.uid, out style) && style != f.personality)
            {
                p.SetPresentation(DragonPersonalityPresentation.For(f.personality));
                _styles[f.uid] = f.personality;
            }
            _behaving.Add(f.uid);
            DragonBehaviorOutput o = p.Update(dt, f.intent, f.speed, f.turnRateDegPerSec);
            state.locomotion = o.locomotion;
            state.speed = o.speed;
            _target.ApplyState(f.uid, state);
            if (o.trigger != DragonInteraction.None && _target.Trigger(f.uid, o.trigger)) report.triggered++;
            if (o.substituted) report.substituted++;
            if (o.note != null) _notes[f.uid] = o.note;
            float offset = 0f;
            var motionEvent = DragonMotionEvent.None;
            if (_motion != null)
            {
                var info = _motion.MotionInfoOf(f.uid);
                if (info != null)
                {
                    if (o.progressAction != DragonInteraction.None && DragonMotionEvents.Crossed(info.EventTime(o.progressAction), o.progressFrom, o.progressTo))
                        motionEvent = DragonMotionEvents.For(o.progressAction);
                    if (o.completed == DragonInteraction.TakeOff) offset += info.takeOffRootOffset;
                    if (o.trigger == DragonInteraction.Land) offset -= info.landRootOffset;
                }
                _motion.SetHidden(f.uid, !o.visible);
                _motion.SetMotionShape(f.uid, o.mode, f.turnRateDegPerSec, f.verticalSpeed, f.speed);
                var a = p.ActiveAction;
                if (_socialTarget != null)
                {
                    var e = social ? sd.expression : DragonExpressionSolver.FromContext(a, o.locomotion, f.personality);
                    _socialTarget.SetExpressionState(f.uid, DragonExpressionSolver.For(e, p.Presentation.tailCarriage));
                }
                else
                    _motion.SetExpression(f.uid, p.Presentation.tailCarriage,
                        a == DragonInteraction.Curious || a == DragonInteraction.Greet || a == DragonInteraction.LookAround ? 1f : 0f);
            }
            _status[f.uid] = new DragonMotionStatus
            {
                live = true, mode = o.mode, targetMode = o.targetMode, transition = o.transition, completed = o.completed,
                canTranslate = o.canTranslate, actionPlaying = o.actionPlaying, visible = o.visible, rootOffsetToApply = offset,
                capabilities = p.Capabilities, motionEvent = motionEvent,
            };
            if (social && sd.look)
            {
                _behavior.SetTempo(f.uid, o.tempo * (sd.tempoScale > 0f ? sd.tempoScale : 1f));
                _behavior.SetLook(f.uid, o.headLookAllowed, sd.lookTarget, sd.lookWeight, sd.lookHold);
                return true;
            }
            _behavior.SetTempo(f.uid, o.tempo);
            _behavior.SetLook(f.uid, f.hasLookTarget && o.headLookAllowed, f.lookTarget, p.Presentation.headLookWeight, p.Presentation.headLookHold);
            return true;
        }

        /// <summary>Separate method so the ClipSeconds closure is allocated only when a planner is created, not on
        /// every Sync (the compiler hoists a lambda's capture object to the start of the enclosing method).</summary>
        private DragonBehaviorPlanner CreatePlanner(string uid, DragonPersonalityStyle style)
        {
            var map = _behavior.ClipsOf(uid);
            if (map == null) return null;
            var p = new DragonBehaviorPlanner(map, c => _behavior.ClipSeconds(uid, c), DragonPersonalityPresentation.For(style), DragonBehaviorPlanner.SeedFor(uid));
            _planners[uid] = p;
            _styles[uid] = style;
            return p;
        }

        private void DropBehavior(string uid)
        {
            _planners.Remove(uid); _styles.Remove(uid); _behaving.Remove(uid); _notes.Remove(uid); _status.Remove(uid);
        }

        /// <summary>Despawns everything this sync showed (scene unload).</summary>
        public void Clear()
        {
            foreach (var uid in _shown.Keys) _target.Despawn(uid);
            _shown.Clear();
            _planners.Clear(); _styles.Clear(); _behaving.Clear(); _notes.Clear(); _status.Clear();
            _social.Clear(); _socialUids.Clear(); _socialFrames.Clear();
        }

        private void Forget(string uid)
        {
            if (_target.IsLive(uid)) _target.Despawn(uid);
            _shown.Remove(uid);
            DropBehavior(uid);
        }
    }

    /// <summary>
    /// Canonical stage names → visual stage. The Domain's GrowthStage is Baby / Juvenile / Adult; the integration
    /// layer may pass either the enum name or its lower-case form. Anything else is rejected, not guessed.
    /// </summary>
    public static class SanctuaryVisualMapping
    {
        public static bool TryStage(string stageName, out DragonVisualStage stage)
        {
            switch ((stageName ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "baby": stage = DragonVisualStage.Baby; return true;
                case "juvenile": stage = DragonVisualStage.Juvenile; return true;
                case "adult": stage = DragonVisualStage.Adult; return true;
                default: stage = DragonVisualStage.Unstaged; return false;
            }
        }
    }
}
