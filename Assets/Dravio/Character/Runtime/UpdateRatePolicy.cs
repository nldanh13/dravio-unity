using System;

namespace Dravio.Character
{
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
