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
        /// <summary>U1-CHAR.8: event reached inside a transition clip this frame (VFX/audio cue), or None.</summary>
        public DragonMotionEvent motionEvent;
    }

    /// <summary>U1-CHAR.8 cue points of the transition clips (authored times exported by the pipeline).</summary>
    public enum DragonMotionEvent { None = 0, LiftOff, TouchDown, WaterContact, WaterLeave }

    public static class DragonMotionEvents
    {
        public static DragonMotionEvent For(DragonInteraction clip)
        {
            switch (clip)
            {
                case DragonInteraction.TakeOff: return DragonMotionEvent.LiftOff;
                case DragonInteraction.Land: return DragonMotionEvent.TouchDown;
                case DragonInteraction.WaterEnter: return DragonMotionEvent.WaterContact;
                case DragonInteraction.WaterExit: return DragonMotionEvent.WaterLeave;
                default: return DragonMotionEvent.None;
            }
        }

        /// <summary>True when progress moved over <paramref name="eventTime"/> this tick (from exclusive, to inclusive).</summary>
        public static bool Crossed(float eventTime, float from, float to) { return eventTime > 0f && from < eventTime && to >= eventTime; }
    }

    /// <summary>
    /// Procedural shaping layered on the clips (pure maths, U1-CHAR.7): banking and pitching in the air, pitching in
    /// water, body/tail bend while turning. Angles in degrees, clamped; the Sanctuary's simulation provides turn
    /// rate and vertical speed, the shaper only presents them.
    /// </summary>
    public static class MotionShapeSolver
    {
        public const float MaxBankDeg = 30f, MaxPitchDeg = 25f, MaxBendDeg = 20f;
        /// <summary>Gravity used by the coordinated-turn bank (m/s^2).</summary>
        public const float Gravity = 9.81f;

        /// <summary>
        /// U1-CHAR.7b: bank of a coordinated (balanced) turn, tan(bank) = V * yawRate / g, the angle flying animals
        /// roll to so lift supplies the turning force (pigeons bank as aerodynamic theory predicts; sources in
        /// BIOMECHANICS_RESEARCH.md). Clamped to MaxBankDeg. Negative = roll right (about forward) for a right turn.
        /// </summary>
        public static float CoordinatedBankDeg(float turnRateDegPerSec, float speed)
        {
            double tan = Math.Abs(speed) * (turnRateDegPerSec * Math.PI / 180.0) / Gravity;
            return -Clamp((float)(Math.Atan(tan) * 180.0 / Math.PI), MaxBankDeg);
        }

        /// <summary>
        /// U1-CHAR.7b gaze stabilisation: birds hold the head almost level against body roll (vestibulo-collic reflex).
        /// Returns the roll the neck and head add (each half of the correction) so the head ends at
        /// (1 - gain) of the body's bank.
        /// </summary>
        public static void HeadRollCompensation(float bankDeg, float gain, out float neckDeg, out float headDeg)
        {
            gain = gain < 0f ? 0f : (gain > 1f ? 1f : gain);
            neckDeg = headDeg = -0.5f * gain * bankDeg;
        }

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
                    // coordinated turn when the forward speed is known; without it (speed 0: not sent) the U1-CHAR.7
                    // linear mapping, so a target that sends no speed keeps banking
                    bankDeg = speed > 0.05f ? CoordinatedBankDeg(turnRateDegPerSec, speed) : -turn * MaxBankDeg;
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

        /// <summary>U1-CHAR.8 tail wag of bone <paramref name="index"/> of <paramref name="count"/> (root first): a wave
        /// travelling to the tip (0.6 rad lag per bone) whose amplitude grows to <paramref name="amplitudeDeg"/> at the tip.</summary>
        public static float TailWagDeg(float amplitudeDeg, float phase01, int index, int count)
        {
            if (count <= 0) return 0f;
            float share = (index + 1f) / count;
            // per-bone share of the tip angle: the rotations add up along the chain
            float perBone = amplitudeDeg * share / (0.5f * (count + 1));
            return perBone * (float)Math.Sin(2.0 * Math.PI * phase01 - 0.6 * index);
        }

        private static float Clamp(float v, float lim) { return v < -lim ? -lim : (v > lim ? lim : v); }
    }

    /// <summary>
    /// U1-CHAR.8 terrain alignment: tilts the body with the ground under the feet. Fore/hind and left/right are the
    /// mean ground offsets (m, + = higher) under those feet relative to the root plane; the body follows a share of the
    /// slope (the legs take the rest, as foot IK does), clamped. Head level is kept by the head look/stabilisation.
    /// </summary>
    public static class TerrainAlignSolver
    {
        public const float DefaultShare = 0.6f, MaxPitchDeg = 20f, MaxRollDeg = 12f;

        /// <param name="span">Fore-to-hind foot distance (m); <paramref name="track"/>: left-to-right (m).</param>
        public static void Compute(float fore, float hind, float left, float right, float span, float track, float share,
                                   out float pitchDeg, out float rollDeg)
        {
            share = share < 0f ? 0f : (share > 1f ? 1f : share);
            pitchDeg = span > 1e-3f ? share * (float)(Math.Atan2(fore - hind, span) * 180.0 / Math.PI) : 0f;
            rollDeg = track > 1e-3f ? share * (float)(Math.Atan2(left - right, track) * 180.0 / Math.PI) : 0f;
            pitchDeg = pitchDeg < -MaxPitchDeg ? -MaxPitchDeg : (pitchDeg > MaxPitchDeg ? MaxPitchDeg : pitchDeg);
            rollDeg = rollDeg < -MaxRollDeg ? -MaxRollDeg : (rollDeg > MaxRollDeg ? MaxRollDeg : rollDeg);
        }
    }
}
