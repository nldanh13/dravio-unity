using UnityEngine;

namespace Dravio.Character
{
    /// <summary>
    /// Optional foot placement on uneven ground for rigs whose rig.json lists ikChains (upper, lower, foot).
    /// Runs after the Animator (LateUpdate) and only corrects what the animation cannot know: the ground height
    /// under each foot relative to the dragon's root.
    ///  * per foot: a ray finds the ground; the vertical difference to the root plane is the wanted offset
    ///  * pelvis: when a foot must go down further than its leg can reach, the body (<see cref="pelvis"/>, by
    ///    default the "Root" bone) is lowered by that amount so the leg stays solvable; it is never raised
    ///  * swing: the correction fades out while the animated foot is lifted above its bind height, so a
    ///    stepping foot keeps its authored arc (no conflict with the clip; only planted feet are pinned)
    ///  * smoothing: offsets ease with <see cref="smoothTime"/> so steps and slopes do not pop
    /// The two-bone chain is solved analytically keeping the animated bend plane; foot world rotation is kept.
    /// Ground states only (<see cref="AppliesTo"/>): off for Swim, Fly, Burrow and the Happy hop.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DragonFootGrounding : MonoBehaviour
    {
        public LayerMask groundMask = ~0;
        [Tooltip("Ray starts this far above the animated foot")] public float rayHeight = 0.5f;
        [Tooltip("Max vertical correction in metres (scaled by the dragon's scale)")] public float maxAdjust = 0.25f;
        [Range(0f, 1f)] public float weight = 1f;
        [Tooltip("Seconds to ease foot and pelvis offsets")] public float smoothTime = 0.08f;
        [Tooltip("Foot lift (m, scaled) above its bind height over which the correction fades out (swing phase)")]
        public float swingFadeHeight = 0.04f;
        [Tooltip("Bone lowered when a foot cannot reach the ground; empty = the bone named Root")]
        public Transform pelvis;
        [Tooltip("Lower the pelvis when a foot must reach lower ground")] public bool adjustPelvis = true;

        private struct Chain
        {
            public Transform upper, lower, foot;
            public float bindHeight;   // foot height above the root plane in the bind pose
            public float reach;        // upper + lower length
            public float offset;       // smoothed vertical offset (m)
        }
        private Chain[] _chains = new Chain[0];
        private float _pelvisOffset;
        private static readonly RaycastHit[] Hits = new RaycastHit[4];

        public int ChainCount { get { return _chains.Length; } }
        public float PelvisOffset { get { return _pelvisOffset; } }

        /// <summary>Ground IK only where the dragon stands on the ground. Swim, Fly, Burrow and the Happy hop
        /// are animated without ground contact (or with the feet digging), so they are never corrected.</summary>
        public static bool AppliesTo(DragonLocomotion shown, DragonInteraction interaction)
        {
            if (interaction == DragonInteraction.Happy) return false;
            return shown == DragonLocomotion.Idle || shown == DragonLocomotion.Walk || shown == DragonLocomotion.Run
                || shown == DragonLocomotion.Sleep || shown == DragonLocomotion.Rest || shown == DragonLocomotion.Bask;
        }

        public void Configure(DragonVisualCatalog.IkChain[] chains)
        {
            var list = new System.Collections.Generic.List<Chain>();
            float rootH = Vector3.Dot(transform.position, transform.up);
            if (chains != null)
                foreach (var c in chains)
                {
                    var ch = new Chain { upper = Find(c.upper), lower = Find(c.lower), foot = Find(c.foot) };
                    if (ch.upper == null || ch.lower == null || ch.foot == null) continue;
                    ch.bindHeight = Vector3.Dot(ch.foot.position, transform.up) - rootH;
                    ch.reach = (ch.lower.position - ch.upper.position).magnitude + (ch.foot.position - ch.lower.position).magnitude;
                    list.Add(ch);
                }
            _chains = list.ToArray();
            if (pelvis == null) pelvis = Find("Root");
            if (_chains.Length == 0) enabled = false;
        }

        private void OnDisable()
        {
            _pelvisOffset = 0f;
            for (int i = 0; i < _chains.Length; i++) _chains[i].offset = 0f;
        }

        private Transform Find(string boneName)
        {
            if (string.IsNullOrEmpty(boneName)) return null;
            foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name == boneName) return t;
            return null;
        }

        /// <summary>1 while the animated foot is at or below its bind height, 0 when lifted by fadeHeight or more.</summary>
        public static float SwingFactor(float footHeightAboveRoot, float bindHeight, float fadeHeight)
        {
            if (fadeHeight <= 0f) return 1f;
            float lift = footHeightAboveRoot - bindHeight;
            return Mathf.Clamp01(1f - lift / fadeHeight);
        }

        /// <summary>How far the pelvis must go down so every wanted foot offset is reachable: the most negative
        /// offset beyond what the leg can extend (0 when all feet reach). Never positive (the body is not raised).</summary>
        public static float PelvisDrop(float[] wanted, float[] slack)
        {
            float drop = 0f;
            for (int i = 0; i < wanted.Length; i++)
            {
                float need = wanted[i] + slack[i];   // negative when the foot must go below what the leg reaches
                if (need < drop) drop = need;
            }
            return drop;
        }

        private void LateUpdate()
        {
            if (weight <= 0f || _chains.Length == 0) return;
            float scale = transform.lossyScale.y;
            Vector3 up = transform.up;
            float rootHeight = Vector3.Dot(transform.position, up);
            float k = smoothTime > 0f ? 1f - Mathf.Exp(-Time.deltaTime / smoothTime) : 1f;
            var wanted = new float[_chains.Length];
            var slack = new float[_chains.Length];
            for (int i = 0; i < _chains.Length; i++)
            {
                var c = _chains[i];
                Vector3 foot = c.foot.position;
                float target = 0f;
                int n = Physics.RaycastNonAlloc(foot + up * rayHeight * scale, -up, Hits, (rayHeight + maxAdjust) * scale * 2f,
                                                groundMask, QueryTriggerInteraction.Ignore);
                float best = float.MaxValue; Vector3 point = foot;
                for (int h = 0; h < n; h++)
                {
                    if (Hits[h].collider.transform.IsChildOf(transform)) continue;
                    if (Hits[h].distance < best) { best = Hits[h].distance; point = Hits[h].point; }
                }
                if (best != float.MaxValue)
                {
                    target = Mathf.Clamp(Vector3.Dot(point, up) - rootHeight, -maxAdjust * scale, maxAdjust * scale);
                    target *= SwingFactor(Vector3.Dot(foot, up) - rootHeight, c.bindHeight * scale, swingFadeHeight * scale);
                }
                c.offset = Mathf.Lerp(c.offset, target * weight, k);
                _chains[i] = c;
                wanted[i] = c.offset;
                // how much further down this foot can go before the leg is straight
                float dist = (foot - c.upper.position).magnitude;
                slack[i] = Mathf.Max(0f, c.reach * scale * 0.995f - dist);
            }
            float drop = adjustPelvis && pelvis != null ? PelvisDrop(wanted, slack) : 0f;
            _pelvisOffset = Mathf.Lerp(_pelvisOffset, drop, k);
            if (pelvis != null && Mathf.Abs(_pelvisOffset) > 1e-5f) pelvis.position += up * _pelvisOffset;
            for (int i = 0; i < _chains.Length; i++)
            {
                var c = _chains[i];
                if (Mathf.Abs(c.offset - _pelvisOffset) < 1e-5f && Mathf.Abs(c.offset) < 1e-5f) continue;
                // the pelvis move already carried the foot by _pelvisOffset; aim at the original foot + offset
                Vector3 animatedFoot = c.foot.position - up * _pelvisOffset;
                Quaternion footRot = c.foot.rotation;
                SolveTwoBone(c.upper, c.lower, c.foot, animatedFoot + up * c.offset, transform.forward);
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
