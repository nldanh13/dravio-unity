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
            /// <summary>Speed (m/s) the Walk/Run clips were authored for; 0 = unknown (playback rate stays 1).</summary>
            public float locomotionReferenceSpeed;
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

        public float InteractionSeconds(Entry e, DragonInteraction i)
        {
            if (e == null) return 0f;
            return i == DragonInteraction.Eat ? e.eatSeconds : i == DragonInteraction.Happy ? e.happySeconds : 0f;
        }
    }
}
