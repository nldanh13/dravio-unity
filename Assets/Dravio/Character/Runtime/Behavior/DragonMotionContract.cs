using System;

namespace Dravio.Character
{
    /// <summary>
    /// What a model can show, derived from its own clips (U1-CHAR.7). Clips come from canonical biology
    /// (dragon-archetypes.json), so this is the species x stage capability of the imported model - never a guess.
    /// </summary>
    public struct DragonMotionCapabilities
    {
        public bool ground, water, air, perch, underground;
        /// <summary>Authored transitions present (missing ones switch at once and are reported).</summary>
        public bool takeOff, land, waterEnter, waterExit, burrowEnter, burrowEmerge, wake;

        public static DragonMotionCapabilities From(DragonAnimationMap m)
        {
            var c = new DragonMotionCapabilities();
            if (m == null) return c;
            c.water = m.Has(DragonLocomotion.Swim);
            c.air = m.Has(DragonLocomotion.Fly);
            c.perch = m.Has(DragonLocomotion.Perch);
            c.underground = m.Has(DragonLocomotion.Burrow);
            // every model that is not a pure swimmer stands on the ground (Idle); a swimmer without Walk lives in water
            c.ground = m.Has(DragonLocomotion.Walk) || (!c.water && m.IsAnimated);
            c.takeOff = m.CanPlay(DragonInteraction.TakeOff);
            c.land = m.CanPlay(DragonInteraction.Land);
            c.waterEnter = m.CanPlay(DragonInteraction.WaterEnter);
            c.waterExit = m.CanPlay(DragonInteraction.WaterExit);
            c.burrowEnter = m.CanPlay(DragonInteraction.BurrowEnter);
            c.burrowEmerge = m.CanPlay(DragonInteraction.BurrowEmerge);
            c.wake = m.CanPlay(DragonInteraction.Wake);
            return c;
        }

        public bool Supports(DragonMotionMode mode)
        {
            switch (mode)
            {
                case DragonMotionMode.Ground: return ground;
                case DragonMotionMode.Water: return water;
                case DragonMotionMode.Air: return air;
                case DragonMotionMode.Perched: return perch;
                case DragonMotionMode.Underground: return underground;
                default: return false;
            }
        }
    }

    /// <summary>Answer to "may this dragon start going into that medium now?"</summary>
    public enum DragonMotionReadiness
    {
        /// <summary>Supported and nothing is running: send the intent.</summary>
        Ready = 0,
        /// <summary>The model cannot show that medium (no clip / ability): do not send it.</summary>
        Unsupported,
        /// <summary>A transition or Wake is playing: wait (keep the dragon where it is).</summary>
        Busy,
        /// <summary>Asleep: the medium change will play Wake first.</summary>
        NeedsWake,
        /// <summary>The dragon is not shown.</summary>
        NotLive,
    }

    /// <summary>Per-dragon motion status the Sanctuary reads after each Sync (SanctuaryDragonSync.MotionStatusOf).</summary>
    public struct DragonMotionStatus
    {
        public bool live;
        public DragonMotionMode mode, targetMode;
        public DragonInteraction transition, completed;
        public bool canTranslate, actionPlaying, visible;
        /// <summary>Vertical offset (m) the Sanctuary should add to the dragon's position this frame so the visual
        /// does not jump: +TakeOff root offset when TakeOff completes, -Land root offset when Land starts (from the
        /// catalog's motion metadata; 0 when unknown).</summary>
        public float rootOffsetToApply;
        public DragonMotionCapabilities capabilities;
    }

    /// <summary>
    /// Procedural shaping layered on the clips (pure maths, U1-CHAR.7): banking and pitching in the air, pitching in
    /// water, body/tail bend while turning. Angles in degrees, clamped; the Sanctuary's simulation provides turn
    /// rate and vertical speed, the shaper only presents them.
    /// </summary>
    public static class MotionShapeSolver
    {
        public const float MaxBankDeg = 30f, MaxPitchDeg = 25f, MaxBendDeg = 20f;

        /// <param name="turnRateDegPerSec">Signed yaw rate (+ = turning right).</param>
        /// <param name="verticalSpeed">m/s, + = climbing.</param>
        /// <param name="speed">Forward speed (m/s), for the climb angle.</param>
        /// <param name="maxTurnRate">The medium's max turn rate from the catalog (0 = 90 deg/s).</param>
        public static void Compute(DragonMotionMode mode, float turnRateDegPerSec, float verticalSpeed, float speed, float maxTurnRate,
                                   out float bankDeg, out float pitchDeg, out float bendDeg)
        {
            float turn = maxTurnRate > 0f ? turnRateDegPerSec / maxTurnRate : turnRateDegPerSec / 90f;
            turn = turn < -1f ? -1f : (turn > 1f ? 1f : turn);
            float climb = (float)(Math.Atan2(verticalSpeed, Math.Max(0.2f, Math.Abs(speed))) * 180.0 / Math.PI);
            bankDeg = pitchDeg = bendDeg = 0f;
            switch (mode)
            {
                case DragonMotionMode.Air:
                    bankDeg = -turn * MaxBankDeg;              // roll into the turn
                    pitchDeg = Clamp(climb, MaxPitchDeg);
                    bendDeg = turn * MaxBendDeg * 0.3f;
                    break;
                case DragonMotionMode.Water:
                    pitchDeg = Clamp(climb, MaxPitchDeg);
                    bendDeg = turn * MaxBendDeg;                // the body curves into the turn
                    break;
                case DragonMotionMode.Ground:
                    bendDeg = turn * MaxBendDeg * 0.5f;
                    break;
            }
        }

        private static float Clamp(float v, float lim) { return v < -lim ? -lim : (v > lim ? lim : v); }
    }
}
