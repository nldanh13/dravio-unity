using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dravio.Character
{
    /// <summary>
    /// Species x stage -> prefab and rig facts, filled by the editor batch importer from import-manifest.json
    /// (Dravio/Character/Import Batch Manifest). Lookup order: exact stage, then the species' "Unstaged" model,
    /// then (only if <see cref="allowOtherStageFallback"/>) the nearest other stage. Every fallback is reported
    /// so a missing model is visible instead of silently replaced.
    /// </summary>
    [CreateAssetMenu(menuName = "Dravio/Character/Dragon Visual Catalog", fileName = "DragonVisualCatalog")]
    public sealed class DragonVisualCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class IkChain { public string upper, lower, foot; }
        /// <summary>In-place ground speed (m/s) a clip was authored for (rig.json clips.*.groundSpeed).</summary>
        [Serializable]
        public sealed class ClipSpeed { public string clip; public float groundSpeed; }
        /// <summary>Length in seconds of a clip (rig.json frame range / fps). U1-CHAR.6: used for every one-shot.</summary>
        [Serializable]
        public sealed class ClipLength { public string clip; public float seconds; }

        [Serializable]
        public sealed class Entry
        {
            public string speciesId;
            public DragonVisualStage stage;
            public GameObject prefab;
            public string archetype;
            public bool rigged;
            public string[] clips = Array.Empty<string>();
            public string[] anchors = Array.Empty<string>();
            public IkChain[] ikChains = Array.Empty<IkChain>();
            /// <summary>Seconds of the one-shot clips (Eat, Happy), from rig.json frame ranges.</summary>
            public float eatSeconds, happySeconds;
            /// <summary>Speed (m/s) the Walk/Run clips were authored for; 0 = unknown (playback rate stays 1).
            /// Fallback when <see cref="clipSpeeds"/> has no entry for the shown clip.</summary>
            public float locomotionReferenceSpeed;
            /// <summary>Per-clip authored ground speed (Walk and Run differ).</summary>
            public ClipSpeed[] clipSpeeds = Array.Empty<ClipSpeed>();
            /// <summary>U1-CHAR.6: seconds of every clip (filled by the importer). Older entries have none and fall back
            /// to eatSeconds / happySeconds.</summary>
            public ClipLength[] clipLengths = Array.Empty<ClipLength>();
            /// <summary>anim_qa.py result: PASS / PASS_WITH_WARNINGS, and one line per warning ("Sleep: deformation ...").</summary>
            public string animQaStatus;
            public string[] animQaWarnings = Array.Empty<string>();
            public bool approved;
            public string validation;
            public string sourceManifest;
        }

        public enum Match { None, Exact, Unstaged, OtherStage }

        public bool allowOtherStageFallback = true;
        public List<Entry> entries = new List<Entry>();

        public Entry Resolve(string speciesId, DragonVisualStage stage, out Match match)
        {
            return Resolve(entries, speciesId, stage, allowOtherStageFallback, out match);
        }

        /// <summary>Pure lookup (testable without assets).</summary>
        public static Entry Resolve(IList<Entry> list, string speciesId, DragonVisualStage stage, bool allowOtherStage, out Match match)
        {
            Entry unstaged = null, nearest = null;
            int best = int.MaxValue;
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e == null || !string.Equals(e.speciesId, speciesId, StringComparison.Ordinal)) continue;
                if (e.stage == stage) { match = Match.Exact; return e; }
                if (e.stage == DragonVisualStage.Unstaged) { unstaged = e; continue; }
                int d = Math.Abs((int)e.stage - (int)stage);
                if (d < best) { best = d; nearest = e; }
            }
            if (unstaged != null) { match = Match.Unstaged; return unstaged; }
            if (allowOtherStage && nearest != null) { match = Match.OtherStage; return nearest; }
            match = Match.None;
            return null;
        }

        /// <summary>Authored speed for the shown locomotion clip; falls back to locomotionReferenceSpeed.</summary>
        public static float ReferenceSpeed(Entry e, DragonLocomotion shown)
        {
            if (e == null) return 0f;
            string name = shown.ToString();
            if (e.clipSpeeds != null)
                for (int i = 0; i < e.clipSpeeds.Length; i++)
                    if (e.clipSpeeds[i] != null && e.clipSpeeds[i].clip == name && e.clipSpeeds[i].groundSpeed > 0f)
                        return e.clipSpeeds[i].groundSpeed;
            return e.locomotionReferenceSpeed;
        }

        /// <summary>QA warnings that concern one clip (lines start with "Clip:"), plus model-wide ones.</summary>
        public static List<string> WarningsFor(Entry e, string clip)
        {
            var list = new List<string>();
            if (e == null || e.animQaWarnings == null) return list;
            foreach (var w in e.animQaWarnings)
                if (w != null && (w.StartsWith(clip + ":", StringComparison.Ordinal) || w.StartsWith("model:", StringComparison.Ordinal)))
                    list.Add(w);
            return list;
        }

        public float InteractionSeconds(Entry e, DragonInteraction i) { return ClipSecondsOf(e, i); }

        /// <summary>Seconds of a one-shot clip: clipLengths first, then the U1-CHAR.4 eatSeconds / happySeconds; 0 if unknown.</summary>
        public static float ClipSecondsOf(Entry e, DragonInteraction i)
        {
            if (e == null || i == DragonInteraction.None) return 0f;
            string name = i.ToString();
            if (e.clipLengths != null)
                for (int k = 0; k < e.clipLengths.Length; k++)
                    if (e.clipLengths[k] != null && e.clipLengths[k].clip == name && e.clipLengths[k].seconds > 0f) return e.clipLengths[k].seconds;
            return i == DragonInteraction.Eat ? e.eatSeconds : i == DragonInteraction.Happy ? e.happySeconds : 0f;
        }
    }
}
