using System;
using UnityEngine;

namespace Dravio.Character
{
    /// <summary>Visual growth stage. The integration layer maps the Domain GrowthStage onto this;
    /// the visual runtime has no Domain or Persistence dependency.</summary>
    public enum DragonVisualStage { Unstaged = 0, Baby = 1, Juvenile = 2, Adult = 3 }

    /// <summary>Continuous (looping) state. Values match the Animator "Locomotion" int parameter. U1-CHAR.6 appended
    /// Rest and Bask (lying, awake); U1-CHAR.7 appended the water, perch and glide states. Existing values never change.</summary>
    public enum DragonLocomotion
    {
        Idle = 0, Walk = 1, Run = 2, Swim = 3, Fly = 4, Burrow = 5, Sleep = 6, Rest = 7, Bask = 8,
        SwimIdle = 9, SwimRest = 10, SwimSurface = 11, Perch = 12, Glide = 13,
    }

    /// <summary>One-shot clip over the current state. Values match the Animator "Action" int parameter (0 = none).
    /// U1-CHAR.6 appended Drink .. Curious; U1-CHAR.7 appended Greet and the medium transitions (TakeOff .. BurrowEmerge);
    /// U1-CHAR.8 appended Nuzzle (head and neck reach toward a partner). Existing values never change.</summary>
    public enum DragonInteraction
    {
        None = 0, Eat = 1, Happy = 2, Drink = 3, Wake = 4, Stretch = 5, LookAround = 6, Curious = 7,
        Greet = 8, TakeOff = 9, Land = 10, WaterEnter = 11, WaterExit = 12, DigPrep = 13, BurrowEnter = 14, BurrowEmerge = 15,
        Nuzzle = 16,
    }

    /// <summary>The medium a dragon is shown in (U1-CHAR.7). The Sanctuary owns where the dragon is; this is only
    /// what the visual runtime shows and which transition it must play to change it.</summary>
    public enum DragonMotionMode { Ground = 0, Water = 1, Air = 2, Perched = 3, Underground = 4 }

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
        // ---- U1-CHAR.7 (appended) ----
        /// <summary>In water, holding position / resting under water (gill or dual breathers) / swimming at the surface.</summary>
        SwimIdle, SwimRest, SwimSurface,
        /// <summary>In the air without flapping.</summary>
        Glide,
        /// <summary>Standing on a perch the Sanctuary chose (species with perching feet).</summary>
        Perch,
        /// <summary>Below ground: the dragon digs in (DigPrep, BurrowEnter) and is then hidden; leaving it plays BurrowEmerge.</summary>
        Underground,
        /// <summary>A simple social greeting (look at the other dragon with lookTarget).</summary>
        Greet,
        // ---- U1-CHAR.8 (appended) ----
        /// <summary>Set by the social coordinator while a pair act runs: hold the medium's idle, no repeats or idle
        /// variations (the coordinator cues the one-shots). Not meant to be sent by the Sanctuary.</summary>
        Social,
    }

    /// <summary>
    /// U1-CHAR.8: a two-dragon act the Sanctuary asks for (both frames name each other as partner with the same act).
    /// Presentation only: it creates no relationship and changes no Domain state. Who meets whom, where they stand and
    /// when the act ends are the Sanctuary's decisions.
    /// </summary>
    public enum DragonSocialAct
    {
        None = 0,
        /// <summary>Face each other and greet (the responder answers after a short latency).</summary>
        Greet,
        /// <summary>Play bouts: play bow invitation, then turn-taking with role reversal (Bekoff).</summary>
        Play,
        /// <summary>Lie down next to each other, breathing in phase, occasional glances.</summary>
        RestTogether,
        /// <summary>One reaches its head toward the other's; the other answers.</summary>
        Nuzzle,
        /// <summary>An older dragon (Juvenile/Adult) with a Baby: the older one watches and nuzzles, the Baby plays around it.</summary>
        Nurture,
    }

    /// <summary>Role inside a pair act (deterministic: older stage leads, then UID order).</summary>
    public enum DragonSocialRole { None = 0, Leader, Responder, Guardian, Young }

    /// <summary>Where a pair act is.</summary>
    public enum DragonSocialPhase
    {
        None = 0,
        /// <summary>The partner is not shown or does not name this dragon with the same act.</summary>
        WaitingForPartner,
        /// <summary>Further apart (or closer) than the act's spacing: the Sanctuary should bring them to desiredSpacing.</summary>
        OutOfRange,
        /// <summary>In range but one of them is busy (transition, one-shot, asleep, different medium).</summary>
        Preparing,
        /// <summary>The act is playing.</summary>
        Acting,
        /// <summary>A one-off act (Greet, Nuzzle) finished; the pair keeps looking at each other until the Sanctuary ends it.</summary>
        Done,
        /// <summary>The act cannot be shown for this pair (see note): e.g. Nurture without a Baby, different media.</summary>
        Rejected,
    }

    /// <summary>
    /// U1-CHAR.8: expression shown through head, neck, tail, ear fins and gaze (no face rig is assumed; eyelids
    /// only where the mesh has blink shape keys). Chosen from the running clip, the social act and the personality.
    /// </summary>
    public enum DragonExpression { Neutral = 0, Happy, Playful, Curious, Alert, Calm, Shy, Affection, Sleepy }

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
