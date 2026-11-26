using System;

namespace Dravio.Character
{
    /// <summary>
    /// Environment interaction hints (OriginSanctuaryLiving.json ▸ interactionPoints[].animationHint) → behaviour
    /// intent. The Sanctuary picks the point and decides that the dragon uses it; this only names the matching
    /// intent. Unknown hints return None (no guess).
    /// </summary>
    public static class DragonBehaviorHints
    {
        public static DragonBehaviorIntent FromEnvironmentHint(string hint)
        {
            switch ((hint ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "drink": return DragonBehaviorIntent.Drink;
                case "bask": return DragonBehaviorIntent.Bask;
                case "rest": return DragonBehaviorIntent.Rest;
                case "inspect": case "sniff": return DragonBehaviorIntent.Curious;
                case "look": case "look_up": return DragonBehaviorIntent.LookAround;
                case "dig": return DragonBehaviorIntent.Burrow;   // shown only if the species burrows (else Idle + note)
                case "splash": return DragonBehaviorIntent.Play;  // the point itself requires a swimmer
                case "eat": return DragonBehaviorIntent.Eat;
                case "sleep": return DragonBehaviorIntent.Sleep;
                default: return DragonBehaviorIntent.None;
            }
        }
    }

    /// <summary>Head look-at maths (pure). Directions are in the dragon's body frame: +x right, +y up, +z forward.</summary>
    public static class HeadLookSolver
    {
        /// <summary>Yaw/pitch (degrees) toward a body-frame direction, clamped; targets behind the dragon give 0 (no
        /// owl-like turn). Positive yaw = to the right, positive pitch = up.</summary>
        public static void Angles(float x, float y, float z, float maxYawDeg, float maxPitchDeg, out float yawDeg, out float pitchDeg)
        {
            yawDeg = 0f; pitchDeg = 0f;
            float flat = (float)Math.Sqrt(x * x + z * z);
            if (flat < 1e-5f && Math.Abs(y) < 1e-5f) return;
            if (z <= 0f) return; // behind: the body should turn instead (Sanctuary's job)
            yawDeg = Clamp((float)(Math.Atan2(x, z) * 180.0 / Math.PI), maxYawDeg);
            pitchDeg = Clamp((float)(Math.Atan2(y, Math.Max(flat, 1e-5f)) * 180.0 / Math.PI), maxPitchDeg);
        }

        /// <summary>Exponential approach of current toward target (frame-rate independent); halfLife in seconds.</summary>
        public static float Smooth(float current, float target, float dt, float halfLife)
        {
            if (halfLife <= 0f || dt <= 0f) return dt <= 0f ? current : target;
            float k = 1f - (float)Math.Pow(0.5, dt / halfLife);
            return current + (target - current) * k;
        }

        /// <summary>Share of a turn carried by the neck (rest by the head), so no single joint bends far.</summary>
        public static void Split(float angle, float neckShare, out float neck, out float head)
        {
            float s = neckShare < 0f ? 0f : (neckShare > 1f ? 1f : neckShare);
            neck = angle * s; head = angle - neck;
        }

        private static float Clamp(float v, float lim) { lim = Math.Abs(lim); return v < -lim ? -lim : (v > lim ? lim : v); }
    }

    /// <summary>
    /// Blink timing (pure, deterministic per seed). Used only when the mesh has an eyelid blend shape
    /// (blink / eye_close); generated meshes usually have none, and then nothing blinks (no bone hack on the eyes).
    /// Asleep: eyes stay closed.
    /// </summary>
    public sealed class BlinkScheduler
    {
        public float MinInterval = 2.5f, MaxInterval = 6f, CloseSeconds = 0.06f, OpenSeconds = 0.1f, DoubleBlinkChance = 0.15f;
        private uint _rng;
        private float _t, _next;
        private int _pendingDouble;

        public BlinkScheduler(uint seed) { _rng = seed == 0 ? 1u : seed; _next = Interval(); }

        /// <summary>U1-CHAR.8 gaze-evoked blink: start a blink now (no-op while one is already running).</summary>
        public void BlinkNow() { if (_t < _next) _t = _next; }

        /// <summary>Eyelid closure 0 (open) .. 1 (closed) after advancing dt seconds.</summary>
        public float Update(float dt, bool asleep)
        {
            if (asleep) { _t = 0f; return 1f; }
            _t += Math.Max(0f, dt);
            float blink = CloseSeconds + OpenSeconds;
            if (_t < _next) return 0f;
            float u = _t - _next;
            if (u >= blink)
            {
                _t = 0f;
                if (_pendingDouble == 0 && Next01() < DoubleBlinkChance) { _pendingDouble = 1; _next = 0.12f; }
                else { _pendingDouble = 0; _next = Interval(); }
                return 0f;
            }
            return u < CloseSeconds ? u / CloseSeconds : 1f - (u - CloseSeconds) / OpenSeconds;
        }

        private float Interval() { return MinInterval + (Math.Max(MinInterval, MaxInterval) - MinInterval) * Next01(); }

        private float Next01()
        {
            _rng ^= _rng << 13; _rng ^= _rng >> 17; _rng ^= _rng << 5;
            return (_rng & 0xFFFFFF) / 16777216f;
        }
    }
}
