using System;

namespace Dravio.Character
{
    /// <summary>U1-CHAR.8: procedural layers allowed for one dragon this frame.</summary>
    public struct DragonDetail { public bool layers, blink; }

    /// <summary>
    /// How often a dragon's Animator is evaluated. Visible and near: every frame. Visible but far: every
    /// <see cref="farInterval"/> frames. Off-screen: every <see cref="offscreenInterval"/> frames (time is
    /// accumulated so motion stays in sync). Beyond <see cref="cullDistance"/>: not evaluated.
    /// Instances are staggered by a phase so throttled updates spread across frames.
    /// </summary>
    [Serializable]
    public sealed class UpdateRatePolicy
    {
        public float nearDistance = 12f;
        public int farInterval = 2;
        public int offscreenInterval = 8;
        public float cullDistance = 80f;
        /// <summary>U1-CHAR.8 mobile: procedural layers (shaper, head look, expression) only up to this distance.</summary>
        public float detailDistance = 12f;
        /// <summary>U1-CHAR.8 mobile: eyelid blinks only up to this distance (a blink is a few pixels further away).</summary>
        public float blinkDistance = 6f;

        /// <summary>Which LateUpdate layers run for a dragon evaluated at <paramref name="interval"/> and distance.</summary>
        public DragonDetail DetailFor(int interval, float distance)
        {
            bool full = interval == 1;
            return new DragonDetail
            {
                layers = full && distance <= detailDistance,
                blink = full && distance <= Math.Min(blinkDistance, detailDistance),
            };
        }

        /// <summary>Bone influences per vertex for LOD level (mobile skinning cost): LOD0 4, LOD1 2, further 1.</summary>
        public static int SkinBonesForLod(int lodIndex) { return lodIndex <= 0 ? 4 : (lodIndex == 1 ? 2 : 1); }

        public int IntervalFor(bool visible, float distance)
        {
            if (distance > cullDistance) return 0; // 0 = do not evaluate
            if (!visible) return Math.Max(1, offscreenInterval);
            return distance <= nearDistance ? 1 : Math.Max(1, farInterval);
        }

        public static bool ShouldUpdate(int frame, int phase, int interval)
        {
            if (interval <= 0) return false;
            if (interval == 1) return true;
            int m = (frame + phase) % interval;
            return m == 0;
        }
    }
}
