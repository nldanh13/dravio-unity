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

    /// <summary>Counts of one <see cref="SanctuaryDragonSync.Sync"/> call (for logs and the performance report).</summary>
    public struct SanctuarySyncReport
    {
        public int spawned, despawned, stageChanged, updated, failed;
        public override string ToString()
        {
            return "spawned=" + spawned + " despawned=" + despawned + " stage=" + stageChanged + " updated=" + updated + " failed=" + failed;
        }
    }

    /// <summary>
    /// Keeps the displayed dragons equal to the list the Sanctuary layer passes each frame:
    /// new and visible → Spawn; gone or hidden → Despawn; species or variant changed → respawn; stage changed →
    /// SetStage (state kept); otherwise ApplyState. Order of the input does not matter; duplicate UIDs are an
    /// error (first wins, counted as failed). Pure bookkeeping: no behaviour is decided here.
    /// </summary>
    public sealed class SanctuaryDragonSync
    {
        private struct Shown { public string speciesId; public string variantId; public DragonVisualStage stage; }

        private readonly IDragonVisualTarget _target;
        private readonly Dictionary<string, Shown> _shown = new Dictionary<string, Shown>(StringComparer.Ordinal);
        private readonly HashSet<string> _seen = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _visibleNow = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> _gone = new List<string>();

        public SanctuaryDragonSync(IDragonVisualTarget target)
        {
            if (target == null) throw new ArgumentNullException("target");
            _target = target;
        }

        public int ShownCount { get { return _shown.Count; } }
        public bool IsShown(string uid) { return uid != null && _shown.ContainsKey(uid); }

        public SanctuarySyncReport Sync(IList<SanctuaryDragonFrame> frames)
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
                        }
                        _target.ApplyState(f.uid, state);
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
                        report.spawned++;
                    }
                    else { report.failed++; Forget(f.uid); }
                }
            _gone.Clear();
            foreach (var uid in _shown.Keys) if (!_visibleNow.Contains(uid)) _gone.Add(uid);
            foreach (var uid in _gone) { _target.Despawn(uid); _shown.Remove(uid); report.despawned++; }
            return report;
        }

        /// <summary>One-shot interaction (Eat, Happy) for a shown dragon.</summary>
        public bool Trigger(string uid, DragonInteraction interaction)
        {
            return IsShown(uid) && _target.Trigger(uid, interaction);
        }

        /// <summary>Despawns everything this sync showed (scene unload).</summary>
        public void Clear()
        {
            foreach (var uid in _shown.Keys) _target.Despawn(uid);
            _shown.Clear();
        }

        private void Forget(string uid)
        {
            if (_target.IsLive(uid)) _target.Despawn(uid);
            _shown.Remove(uid);
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
