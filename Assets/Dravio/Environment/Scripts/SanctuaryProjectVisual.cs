using System;
using UnityEngine;

namespace Dravio.EnvironmentArt
{
    /// <summary>
    /// CHAR.8 — visual of one Sanctuary project building (Domain <c>SanctuaryProjectContent</c>, Docs/u2.11/SANCTUARY_PROJECTS.md).
    /// Presentation only: it never reads or writes game data, cost, materials, buffs or rewards. The integration layer
    /// spawns the prefab whose <see cref="projectId"/> matches a built project and places it at a Sanctuary slot.
    /// Anchors are child transforms named <c>ANCHOR_*</c> from the FBX (lights, glow, approach spot, perches, VFX).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SanctuaryProjectVisual : MonoBehaviour
    {
        [Tooltip("Stable Domain id, e.g. origin_lantern_row.")] public string projectId;
        [Tooltip("origin | ocean | sky")] public string world;
        [Tooltip("landmark | path | garden | light | water | vista")] public string kind;
        [Tooltip("Bottom-centre footprint radius (m) for slot spacing / navigation carving.")] public float footprintRadius;
        [Tooltip("style-sample = for art review only, not accepted.")] public string status = "style-sample";
        [SerializeField] Transform[] anchors = Array.Empty<Transform>();

        public Transform[] Anchors => anchors;

        /// <summary>Anchor by full name (<c>ANCHOR_Approach</c>) or suffix (<c>Approach</c>).</summary>
        public bool TryGetAnchor(string name, out Transform anchor)
        {
            foreach (var a in anchors)
            {
                if (a == null) continue;
                if (a.name == name || a.name == "ANCHOR_" + name) { anchor = a; return true; }
            }
            anchor = null;
            return false;
        }

        /// <summary>Editor/builder only: collect every descendant named ANCHOR_*.</summary>
        public void CollectAnchors()
        {
            var found = new System.Collections.Generic.List<Transform>();
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("ANCHOR_", StringComparison.Ordinal)) found.Add(t);
            found.Sort((x, y) => string.CompareOrdinal(x.name, y.name));
            anchors = found.ToArray();
        }
    }
}
