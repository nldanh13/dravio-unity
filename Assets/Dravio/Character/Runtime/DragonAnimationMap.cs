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

        public DragonAnimationMap(IEnumerable<string> clipNames)
        {
            _clips = new HashSet<string>(clipNames ?? Array.Empty<string>(), StringComparer.Ordinal);
        }

        public bool Has(string clip) { return _clips.Contains(clip); }
        public bool IsAnimated { get { return _clips.Contains("Idle"); } }

        /// <summary>Returns the locomotion that will be shown and whether it differs from the request.</summary>
        public DragonLocomotion Resolve(DragonLocomotion requested, out bool substituted)
        {
            substituted = false;
            if (Has(requested.ToString())) return requested;
            substituted = true;
            if (requested == DragonLocomotion.Run && Has("Walk")) return DragonLocomotion.Walk;
            return DragonLocomotion.Idle;
        }

        public bool CanPlay(DragonInteraction interaction)
        {
            return interaction != DragonInteraction.None && Has(interaction.ToString());
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
