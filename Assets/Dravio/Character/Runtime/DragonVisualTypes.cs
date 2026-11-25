using System;
using UnityEngine;

namespace Dravio.Character
{
    /// <summary>Visual growth stage. The integration layer maps the Domain GrowthStage onto this;
    /// the visual runtime has no Domain or Persistence dependency.</summary>
    public enum DragonVisualStage { Unstaged = 0, Baby = 1, Juvenile = 2, Adult = 3 }

    /// <summary>Continuous movement mode. Values match the Animator "Locomotion" int parameter.</summary>
    public enum DragonLocomotion { Idle = 0, Walk = 1, Run = 2, Swim = 3, Fly = 4, Burrow = 5, Sleep = 6 }

    /// <summary>One-shot interaction. Values match the Animator "Action" int parameter (0 = none).</summary>
    public enum DragonInteraction { None = 0, Eat = 1, Happy = 2 }

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
        public static readonly int LocomotionHash = Animator.StringToHash(Locomotion);
        public static readonly int ActionHash = Animator.StringToHash(Action);
        public static readonly int SpeedHash = Animator.StringToHash(Speed);
        public static readonly int PlaybackRateHash = Animator.StringToHash(PlaybackRate);
    }
}
