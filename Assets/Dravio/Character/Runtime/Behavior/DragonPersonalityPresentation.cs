using System;

namespace Dravio.Character
{
    /// <summary>
    /// How a personality is SHOWN (U1-CHAR.6): tempo, how often and which idle variations play, how a player
    /// interaction is answered, how readily the head follows a look target. These are art-direction defaults for
    /// presentation only: they read no Domain value and change no Personality/Health/gameplay rule. Tune them here
    /// (or replace the preset) with the art director; tests pin only their invariants, not the numbers.
    /// </summary>
    [Serializable]
    public sealed class DragonPersonalityPresentation
    {
        public DragonPersonalityStyle style;
        /// <summary>Animator speed multiplier for idle/lying/one-shot clips (locomotion rate still follows ground speed).</summary>
        public float tempo = 1f;
        /// <summary>Seconds of uninterrupted Idle before the first variation, and between variations (min..max, random).</summary>
        public float idleVariationMinSeconds = 10f, idleVariationMaxSeconds = 20f;
        /// <summary>Relative weights of the idle variations (0 = never). Only clips the species has are drawn.</summary>
        public float weightLookAround = 1f, weightCurious = 1f, weightStretch = 1f, weightHappy = 0.5f;
        /// <summary>0..1: how much of the requested head turn toward a look target is shown.</summary>
        public float headLookWeight = 0.8f;
        /// <summary>Shy dragons glance at the target and away again: fraction of time the look is held (1 = always).</summary>
        public float headLookHold = 1f;
        public DragonInteraction onPetted = DragonInteraction.Happy;
        public DragonInteraction onCalled = DragonInteraction.LookAround;
        public DragonInteraction onFed = DragonInteraction.Eat;

        public static DragonPersonalityPresentation For(DragonPersonalityStyle style)
        {
            var p = new DragonPersonalityPresentation { style = style };
            switch (style)
            {
                case DragonPersonalityStyle.Playful:
                    p.tempo = 1.1f; p.idleVariationMinSeconds = 6f; p.idleVariationMaxSeconds = 12f;
                    p.weightLookAround = 1f; p.weightCurious = 1f; p.weightStretch = 2f; p.weightHappy = 3f;
                    p.headLookWeight = 1f; p.onPetted = DragonInteraction.Happy; p.onCalled = DragonInteraction.Happy;
                    break;
                case DragonPersonalityStyle.Calm:
                    p.tempo = 0.9f; p.idleVariationMinSeconds = 14f; p.idleVariationMaxSeconds = 26f;
                    p.weightLookAround = 2f; p.weightCurious = 0.5f; p.weightStretch = 2f; p.weightHappy = 0.5f;
                    p.headLookWeight = 0.5f; p.onPetted = DragonInteraction.Happy; p.onCalled = DragonInteraction.LookAround;
                    break;
                case DragonPersonalityStyle.Curious:
                    p.tempo = 1f; p.idleVariationMinSeconds = 7f; p.idleVariationMaxSeconds = 14f;
                    p.weightLookAround = 2f; p.weightCurious = 3f; p.weightStretch = 1f; p.weightHappy = 1f;
                    p.headLookWeight = 1f; p.onPetted = DragonInteraction.Curious; p.onCalled = DragonInteraction.Curious;
                    break;
                case DragonPersonalityStyle.Shy:
                    p.tempo = 0.95f; p.idleVariationMinSeconds = 10f; p.idleVariationMaxSeconds = 20f;
                    p.weightLookAround = 3f; p.weightCurious = 1f; p.weightStretch = 0.5f; p.weightHappy = 0.3f;
                    p.headLookWeight = 0.6f; p.headLookHold = 0.4f; p.onPetted = DragonInteraction.LookAround; p.onCalled = DragonInteraction.LookAround;
                    break;
            }
            return p;
        }

        /// <summary>Maps a Domain personality name (Playful, Calm, Curious, Shy; any case) without referencing Domain.</summary>
        public static DragonPersonalityStyle StyleFromName(string personality)
        {
            switch ((personality ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "playful": return DragonPersonalityStyle.Playful;
                case "calm": return DragonPersonalityStyle.Calm;
                case "curious": return DragonPersonalityStyle.Curious;
                case "shy": return DragonPersonalityStyle.Shy;
                default: return DragonPersonalityStyle.Unspecified;
            }
        }

        public DragonInteraction ReactionFor(DragonReaction r)
        {
            return r == DragonReaction.Petted ? onPetted : r == DragonReaction.Called ? onCalled : r == DragonReaction.Fed ? onFed : DragonInteraction.None;
        }
    }
}
