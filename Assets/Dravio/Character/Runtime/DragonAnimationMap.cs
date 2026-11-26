using System;
using System.Collections.Generic;

namespace Dravio.Character
{
    /// <summary>
    /// Maps requested states to the clips a species actually has (from its rig.json / catalog entry).
    /// A species never plays a clip for an ability it lacks: Fly on a non-flyer falls back to Idle and is
    /// reported, it is not faked with another clip. Run without a Run clip uses Walk (same ability).
    /// Pure logic (no UnityEngine calls) so it is unit-testable outside Unity.
    /// </summary>
    public sealed class DragonAnimationMap
    {
        private readonly HashSet<string> _clips;
        // per-enum lookups built once (enum.ToString() allocates; this runs every frame for every dragon)
        private readonly bool[] _loco, _action;

        public DragonAnimationMap(IEnumerable<string> clipNames)
        {
            _clips = new HashSet<string>(clipNames ?? Array.Empty<string>(), StringComparer.Ordinal);
            var l = (DragonLocomotion[])Enum.GetValues(typeof(DragonLocomotion));
            _loco = new bool[MaxValue(l) + 1];
            foreach (var v in l) _loco[(int)v] = _clips.Contains(v.ToString());
            var a = (DragonInteraction[])Enum.GetValues(typeof(DragonInteraction));
            _action = new bool[MaxValue(a) + 1];
            foreach (var v in a) _action[(int)v] = v != DragonInteraction.None && _clips.Contains(v.ToString());
        }

        private static int MaxValue<T>(T[] values) where T : struct
        {
            int m = 0;
            foreach (var v in values) m = Math.Max(m, Convert.ToInt32(v));
            return m;
        }

        public bool Has(DragonLocomotion l) { int i = (int)l; return i >= 0 && i < _loco.Length && _loco[i]; }

        public bool Has(string clip) { return _clips.Contains(clip); }
        public bool IsAnimated { get { return Has(DragonLocomotion.Idle); } }

        /// <summary>Returns the locomotion that will be shown and whether it differs from the request.</summary>
        public DragonLocomotion Resolve(DragonLocomotion requested, out bool substituted)
        {
            substituted = false;
            if (Has(requested)) return requested;
            substituted = true;
            if (requested == DragonLocomotion.Run && Has(DragonLocomotion.Walk)) return DragonLocomotion.Walk;
            // lying states fall back to the nearest lying clip the species has, never to another ability
            if (requested == DragonLocomotion.Bask && Has(DragonLocomotion.Rest)) return DragonLocomotion.Rest;
            if ((requested == DragonLocomotion.Bask || requested == DragonLocomotion.Rest) && Has(DragonLocomotion.Sleep)) return DragonLocomotion.Sleep;
            // U1-CHAR.7: water states fall back within the swimming ability, Glide to flapping (same ability)
            if (requested == DragonLocomotion.SwimRest && Has(DragonLocomotion.SwimIdle)) return DragonLocomotion.SwimIdle;
            if ((requested == DragonLocomotion.SwimIdle || requested == DragonLocomotion.SwimRest || requested == DragonLocomotion.SwimSurface)
                && Has(DragonLocomotion.Swim)) return DragonLocomotion.Swim;
            if (requested == DragonLocomotion.Glide && Has(DragonLocomotion.Fly)) return DragonLocomotion.Fly;
            return DragonLocomotion.Idle;
        }

        public bool CanPlay(DragonInteraction interaction)
        {
            int i = (int)interaction;
            return i > 0 && i < _action.Length && _action[i];
        }

        /// <summary>Playback multiplier so feet roughly match ground speed. referenceSpeed is the speed the clip
        /// was authored for (m/s); unknown (<= 0) keeps 1. Clamped to avoid visibly broken playback.</summary>
        public static float PlaybackRate(DragonLocomotion shown, float speed, float referenceSpeed)
        {
            if (referenceSpeed <= 0f || speed <= 0f) return 1f;
            if (shown != DragonLocomotion.Walk && shown != DragonLocomotion.Run && shown != DragonLocomotion.Swim
                && shown != DragonLocomotion.Fly && shown != DragonLocomotion.Burrow) return 1f;
            float r = speed / referenceSpeed;
            return r < 0.5f ? 0.5f : (r > 2f ? 2f : r);
        }
    }
}
