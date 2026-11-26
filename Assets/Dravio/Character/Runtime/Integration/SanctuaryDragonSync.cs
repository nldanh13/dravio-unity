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
            if (_behavior == null || f.intent == DragonBehaviorIntent.None)
            {
                if (_behaving.Remove(f.uid)) { _behavior.SetTempo(f.uid, 1f); _behavior.SetLook(f.uid, false, default(Vector3), 0f, 1f); }
                _planners.Remove(f.uid);
                return false;
            }
            DragonBehaviorPlanner p;
            DragonPersonalityStyle style;
            if (!_planners.TryGetValue(f.uid, out p))
            {
                p = CreatePlanner(f.uid, f.personality);
                if (p == null) return false;
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
            _planners.Remove(uid); _styles.Remove(uid); _behaving.Remove(uid); _notes.Remove(uid);
        }

        /// <summary>Despawns everything this sync showed (scene unload).</summary>
        public void Clear()
        {
            foreach (var uid in _shown.Keys) _target.Despawn(uid);
            _shown.Clear();
            _planners.Clear(); _styles.Clear(); _behaving.Clear(); _notes.Clear();
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
