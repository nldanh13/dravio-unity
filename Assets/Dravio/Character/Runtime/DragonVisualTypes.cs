using System;
using UnityEngine;

namespace Dravio.Character
{
    /// <summary>Visual growth stage. The integration layer maps the Domain GrowthStage onto this;
    /// the visual runtime has no Domain or Persistence dependency.</summary>
    public enum DragonVisualStage { Unstaged = 0, Baby = 1, Juvenile = 2, Adult = 3 }

    /// <summary>Continuous (looping) state. Values match the Animator "Locomotion" int parameter. U1-CHAR.6 appended
    /// Rest and Bask (lying, awake); existing values never change.</summary>
    public enum DragonLocomotion { Idle = 0, Walk = 1, Run = 2, Swim = 3, Fly = 4, Burrow = 5, Sleep = 6, Rest = 7, Bask = 8 }

    /// <summary>One-shot clip over the current state. Values match the Animator "Action" int parameter (0 = none).
    /// U1-CHAR.6 appended Drink .. Curious; existing values never change.</summary>
    public enum DragonInteraction { None = 0, Eat = 1, Happy = 2, Drink = 3, Wake = 4, Stretch = 5, LookAround = 6, Curious = 7 }

    /// <summary>
    /// What the Sanctuary wants a dragon to be doing (U1-CHAR.6). The visual runtime turns it into clips the species
    /// actually has (<see cref="DragonBehaviorPlanner"/>); it never decides the behaviour itself. None = the legacy
    /// path (the frame's <c>locomotion</c> is shown as is).
    /// </summary>
    public enum DragonBehaviorIntent
    {
        None = 0,
        Idle, Walk, Run, Swim, Fly, Burrow,
        Sleep, Rest, Bask,
        Eat, Drink, Play,
        LookAround, Curious, Stretch,
        /// <summary>Gentle rest while cared for (the Sanctuary decides when, from Domain state); never a sick or hurt pose.</summary>
        Recover,
    }

    /// <summary>A player interaction the Sanctuary reports; the shown response depends on personality and clips.</summary>
    public enum DragonReaction { None = 0, Petted, Fed, Called }

    /// <summary>Presentation style from the Domain Personality (Playful, Calm, Curious, Shy). Unspecified = neutral.
    /// It only changes how motion is shown (tempo, idle variations, reactions, head look), never Domain rules.</summary>
    public enum DragonPersonalityStyle { Unspecified = 0, Playful, Calm, Curious, Shy }

    /// <summary>Everything the integration layer tells the visual runtime about one dragon.
    /// Position/rotation are the simulation's; the runtime only presents them.</summary>
    [Serializable]
    public struct DragonVisualState
    {
        public DragonLocomotion locomotion;
        /// <summary>Ground/air speed in metres per second (drives clip playback rate).</summary>
        public float speed;
        public Vector3 position;
        public Quaternion rotation;

        public static DragonVisualState At(Vector3 position, Quaternion rotation)
        {
            return new DragonVisualState { locomotion = DragonLocomotion.Idle, position = position, rotation = rotation };
        }
    }

    /// <summary>Identity and appearance inputs for spawning. Genotype is the Domain's allele text per locus
    /// (e.g. "pigment" -> "Rr"); how it looks comes only from a <see cref="PhenotypeProfile"/> asset.</summary>
    [Serializable]
    public sealed class DragonVisualSpawn
    {
        public string uid;
        public string speciesId;
        public DragonVisualStage stage;
        public string variantId;
        public GenotypeEntry[] genotype = Array.Empty<GenotypeEntry>();
        public DragonVisualState state;
    }

    [Serializable]
    public struct GenotypeEntry
    {
        public string locus;
        public string alleles;
        public GenotypeEntry(string locus, string alleles) { this.locus = locus; this.alleles = alleles; }
    }

    public static class DragonAnimatorParams
    {
        public const string Locomotion = "Locomotion";
        public const string Action = "Action";
        public const string Speed = "Speed";
        public const string PlaybackRate = "PlaybackRate";
        /// <summary>U1-CHAR.6: speed multiplier of the non-locomotion states (personality tempo). Older controllers lack it.</summary>
        public const string Tempo = "Tempo";
        public static readonly int LocomotionHash = Animator.StringToHash(Locomotion);
        public static readonly int ActionHash = Animator.StringToHash(Action);
        public static readonly int SpeedHash = Animator.StringToHash(Speed);
        public static readonly int PlaybackRateHash = Animator.StringToHash(PlaybackRate);
        public static readonly int TempoHash = Animator.StringToHash(Tempo);
    }
}
