using System;
using UnityEngine;

namespace Dravio.EnvironmentArt
{
    /// <summary>
    /// Cost tier for the living environment on landscape phones. Detected once from the
    /// device; the game (settings menu, thermal monitor) may override it with <see cref="Set"/>.
    /// Only scales cosmetic work: particle counts, update rates, audio voices, night shadows.
    /// </summary>
    public static class EnvironmentQuality
    {
        public enum Tier { Low, Medium, High }

        static bool _detected;
        static Tier _tier = Tier.High;

        public static event Action<Tier> Changed;

        public static Tier Current
        {
            get
            {
                if (!_detected) { _tier = Detect(); _detected = true; }
                return _tier;
            }
        }

        public static void Set(Tier tier)
        {
            _detected = true;
            if (_tier == tier) return;
            _tier = tier;
            Changed?.Invoke(tier);
        }

        public static Tier Detect()
        {
            if (Application.isEditor) return Tier.High;
            if (!Application.isMobilePlatform) return Tier.High;
            int mem = SystemInfo.systemMemorySize;
            int cores = SystemInfo.processorCount;
            if (mem > 0 && mem < 3500 || cores <= 4) return Tier.Low;
            if (mem > 0 && mem < 6000) return Tier.Medium;
            return Tier.High;
        }

        /// <summary>Particle count multiplier.</summary>
        public static float ParticleScale(Tier t) => t == Tier.Low ? 0.35f : t == Tier.Medium ? 0.65f : 1f;

        /// <summary>Seconds between lighting / audio mix updates.</summary>
        public static float UpdateInterval(Tier t) => t == Tier.Low ? 0.25f : t == Tier.Medium ? 0.15f : 0.1f;

        /// <summary>Simultaneous one-shot voices.</summary>
        public static int OneShotVoices(Tier t) => t == Tier.Low ? 2 : t == Tier.Medium ? 3 : 4;

        /// <summary>Moonlight shadows are subtle; skipped on Low to save the shadow pass at night.</summary>
        public static bool NightShadows(Tier t) => t != Tier.Low;
    }
}
