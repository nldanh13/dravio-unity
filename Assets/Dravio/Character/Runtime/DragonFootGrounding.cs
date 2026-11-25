using UnityEngine;

namespace Dravio.Character
{
    /// <summary>
    /// Optional foot placement for rigs whose rig.json lists ikChains (upper, lower, foot). After animation, each
    /// foot is raised/lowered by the ground height difference under it relative to the dragon's root, then the
    /// two-bone chain is solved analytically keeping the animated bend plane. Foot world rotation is kept.
    /// Enabled by the manager only for near, every-frame dragons in ground states (not Fly/Swim/Burrow/Sleep).
    /// Limits: no pelvis adjustment, so slopes steeper than maxAdjust are clamped rather than matched.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DragonFootGrounding : MonoBehaviour
    {
        public LayerMask groundMask = ~0;
        [Tooltip("Ray starts this far above the animated foot")] public float rayHeight = 0.5f;
        [Tooltip("Max vertical correction in metres (scaled by the dragon's scale)")] public float maxAdjust = 0.25f;
        [Range(0f, 1f)] public float weight = 1f;

        private struct Chain { public Transform upper, lower, foot; }
        private Chain[] _chains = new Chain[0];
        private static readonly RaycastHit[] Hits = new RaycastHit[4];

        public int ChainCount { get { return _chains.Length; } }

        public static bool AppliesTo(DragonLocomotion shown, DragonInteraction interaction)
        {
            return shown == DragonLocomotion.Idle || shown == DragonLocomotion.Walk || shown == DragonLocomotion.Run;
        }

        public void Configure(DragonVisualCatalog.IkChain[] chains)
        {
            var list = new System.Collections.Generic.List<Chain>();
            if (chains != null)
                foreach (var c in chains)
                {
                    var ch = new Chain { upper = Find(c.upper), lower = Find(c.lower), foot = Find(c.foot) };
                    if (ch.upper != null && ch.lower != null && ch.foot != null) list.Add(ch);
                }
            _chains = list.ToArray();
            if (_chains.Length == 0) enabled = false;
        }

        private Transform Find(string boneName)
        {
            if (string.IsNullOrEmpty(boneName)) return null;
            foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name == boneName) return t;
            return null;
        }

        private void LateUpdate()
        {
            if (weight <= 0f || _chains.Length == 0) return;
            float scale = transform.lossyScale.y;
            Vector3 up = transform.up;
            float rootHeight = Vector3.Dot(transform.position, up);
            foreach (var c in _chains)
            {
                Vector3 foot = c.foot.position;
                int n = Physics.RaycastNonAlloc(foot + up * rayHeight * scale, -up, Hits, (rayHeight + maxAdjust) * scale * 2f,
                                                groundMask, QueryTriggerInteraction.Ignore);
                if (n == 0) continue;
                float best = float.MaxValue; Vector3 point = foot;
                for (int i = 0; i < n; i++)
                {
                    if (Hits[i].collider.transform.IsChildOf(transform)) continue;
                    if (Hits[i].distance < best) { best = Hits[i].distance; point = Hits[i].point; }
                }
                if (best == float.MaxValue) continue;
                float delta = Vector3.Dot(point, up) - rootHeight;
                delta = Mathf.Clamp(delta, -maxAdjust * scale, maxAdjust * scale) * weight;
                if (Mathf.Abs(delta) < 1e-4f) continue;
                Quaternion footRot = c.foot.rotation;
                SolveTwoBone(c.upper, c.lower, c.foot, foot + up * delta, transform.forward);
                c.foot.rotation = footRot;
            }
        }

        /// <summary>Analytical two-bone IK in world space; keeps the current bend plane (fallback: hint axis).</summary>
        public static void SolveTwoBone(Transform a, Transform b, Transform c, Vector3 target, Vector3 bendHint)
        {
            Vector3 pa = a.position, pb = b.position, pc = c.position;
            float lab = (pb - pa).magnitude, lbc = (pc - pb).magnitude;
            if (lab < 1e-5f || lbc < 1e-5f) return;
            float lat = Mathf.Clamp((target - pa).magnitude, Mathf.Abs(lab - lbc) + 1e-4f, lab + lbc - 1e-4f);
            Vector3 axis = Vector3.Cross(pc - pa, pb - pa);
            if (axis.sqrMagnitude < 1e-10f) axis = Vector3.Cross(pc - pa, bendHint);
            if (axis.sqrMagnitude < 1e-10f) return;
            axis.Normalize();
            float acab0 = Angle(pc - pa, pb - pa);
            float babc0 = Angle(pa - pb, pc - pb);
            float acab1 = Mathf.Acos(Mathf.Clamp((lbc * lbc - lab * lab - lat * lat) / (-2f * lab * lat), -1f, 1f));
            float babc1 = Mathf.Acos(Mathf.Clamp((lat * lat - lab * lab - lbc * lbc) / (-2f * lab * lbc), -1f, 1f));
            a.rotation = Quaternion.AngleAxis((acab1 - acab0) * Mathf.Rad2Deg, axis) * a.rotation;
            b.rotation = Quaternion.AngleAxis((babc1 - babc0) * Mathf.Rad2Deg, axis) * b.rotation;
            a.rotation = Quaternion.FromToRotation(c.position - pa, target - pa) * a.rotation;
        }

        private static float Angle(Vector3 u, Vector3 v)
        {
            return Mathf.Acos(Mathf.Clamp(Vector3.Dot(u.normalized, v.normalized), -1f, 1f));
        }
    }
}
