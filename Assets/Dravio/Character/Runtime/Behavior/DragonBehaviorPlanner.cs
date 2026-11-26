using System;

namespace Dravio.Character
{
    /// <summary>What to show for one dragon this tick (output of <see cref="DragonBehaviorPlanner.Update"/>).</summary>
    public struct DragonBehaviorOutput
    {
        /// <summary>Looping state for the Animator "Locomotion" parameter (already resolved to a clip the species has).</summary>
        public DragonLocomotion locomotion;
        /// <summary>Ground speed passed through for the playback rate (0 for in-place states).</summary>
        public float speed;
        /// <summary>One-shot to start this tick, or None. Only set on the tick it starts.</summary>
        public DragonInteraction trigger;
        /// <summary>Animator speed for non-locomotion clips (personality tempo; gentler while recovering).</summary>
        public float tempo;
        /// <summary>True when a head look-at may be layered on top (standing or lying, no head-driven one-shot running).</summary>
        public bool headLookAllowed;
        /// <summary>True when the request was shown with another clip (missing clip); see <see cref="note"/>.</summary>
        public bool substituted;
        public string note;
    }

    /// <summary>
    /// Turns the Sanctuary's behaviour intent into clips the species actually has, per dragon (U1-CHAR.6). Pure C#,
    /// deterministic per seed, no Domain and no engine calls, so it is unit-tested outside Unity. Rules:
    ///   - intents map to a looping state and/or a repeating one-shot; a missing clip falls back honestly
    ///     (Bask -> Rest -> Sleep -> Idle; Run -> Walk; an ability the species lacks -> Idle) and is reported;
    ///   - leaving Sleep plays Wake first when the species has it (Sleep -> Wake -> target, no snap);
    ///   - a one-shot is never cut by another one-shot (only a new intent that needs no one-shot can follow);
    ///   - long uninterrupted Idle draws idle variations (LookAround, Curious, Stretch, Happy) with the
    ///     personality's weights and interval; turning on the spot shows stepping (Walk) instead of sliding;
    ///   - player reactions (Petted, Called, Fed) play the personality's response; a sleeping dragon is not
    ///     disturbed (the reaction is dropped and reported; waking it is the Sanctuary's decision).
    /// </summary>
    public sealed class DragonBehaviorPlanner
    {
        /// <summary>Turning faster than this on the spot (deg/s) shows stepping (Walk) instead of an Idle slide.</summary>
        public const float TurnInPlaceDegPerSec = 60f;
        private const float RepeatGapSeconds = 0.25f;

        private readonly DragonAnimationMap _clips;
        private readonly Func<DragonInteraction, float> _seconds;
        private DragonPersonalityPresentation _p;
        private uint _rng;

        public DragonLocomotion Shown { get; private set; }
        public DragonInteraction ActiveAction { get; private set; }
        public float ActionRemaining { get; private set; }
        public DragonBehaviorIntent LastIntent { get; private set; }
        public float IdleSeconds { get; private set; }
        public float NextVariationAt { get; private set; }
        private float _gap;
        private DragonReaction _pending;
        // substitution note, rebuilt only when the situation changes (no per-frame string garbage)
        private DragonBehaviorIntent _noteIntent = (DragonBehaviorIntent)(-1);
        private DragonLocomotion _noteWanted, _noteTarget;
        private DragonInteraction _noteRepeat;
        private string _note;

        /// <param name="clipSeconds">Length of a one-shot clip in seconds (from the catalog); &lt;= 0 means unknown (1 s is used).</param>
        public DragonBehaviorPlanner(DragonAnimationMap clips, Func<DragonInteraction, float> clipSeconds, DragonPersonalityPresentation presentation, uint seed)
        {
            _clips = clips ?? new DragonAnimationMap(null);
            _seconds = clipSeconds ?? (_ => 1f);
            _p = presentation ?? DragonPersonalityPresentation.For(DragonPersonalityStyle.Unspecified);
            _rng = seed == 0 ? 0x9E3779B9u : seed;
            Shown = DragonLocomotion.Idle;
            NextVariationAt = NextInterval();
        }

        public DragonPersonalityPresentation Presentation { get { return _p; } }

        public void SetPresentation(DragonPersonalityPresentation p)
        {
            if (p == null || ReferenceEquals(p, _p)) return;
            _p = p;
            NextVariationAt = Math.Min(NextVariationAt, NextInterval());
        }

        /// <summary>Queues a player-interaction response; it plays on the next Update that can show it.</summary>
        public void React(DragonReaction reaction) { if (reaction != DragonReaction.None) _pending = reaction; }

        public DragonBehaviorOutput Update(float dt, DragonBehaviorIntent intent, float speed, float turnRateDegPerSec)
        {
            if (dt < 0f) dt = 0f;
            var o = new DragonBehaviorOutput { tempo = _p.tempo, speed = 0f };
            if (ActiveAction != DragonInteraction.None)
            {
                ActionRemaining -= dt;
                if (ActionRemaining <= 0f) { ActiveAction = DragonInteraction.None; ActionRemaining = 0f; _gap = RepeatGapSeconds; }
            }
            else if (_gap > 0f) _gap = Math.Max(0f, _gap - dt);
            if (intent != LastIntent) { IdleSeconds = 0f; _gap = 0f; }
            LastIntent = intent;

            DragonLocomotion wanted;
            DragonInteraction repeat;
            Map(intent, out wanted, out repeat);
            bool sub;
            DragonLocomotion target = _clips.Resolve(wanted, out sub);
            bool missingRepeat = repeat != DragonInteraction.None && !_clips.CanPlay(repeat);
            if (sub || missingRepeat)
            {
                o.substituted = true;
                if (intent != _noteIntent || wanted != _noteWanted || target != _noteTarget || repeat != _noteRepeat)
                {
                    _noteIntent = intent; _noteWanted = wanted; _noteTarget = target; _noteRepeat = repeat;
                    _note = missingRepeat ? intent + ": no " + repeat + " clip" : intent + ": no " + wanted + " clip, showing " + target;
                }
                o.note = _note;
                if (missingRepeat) repeat = DragonInteraction.None;
            }
            if (intent == DragonBehaviorIntent.Recover) o.tempo = _p.tempo * 0.85f;

            // leaving Sleep: Wake first (the species' own rise into Idle)
            if (Shown == DragonLocomotion.Sleep && target != DragonLocomotion.Sleep && ActiveAction == DragonInteraction.None
                && _clips.CanPlay(DragonInteraction.Wake))
                Start(DragonInteraction.Wake, ref o);

            // turning on the spot: step instead of sliding
            bool moving = target == DragonLocomotion.Walk || target == DragonLocomotion.Run || target == DragonLocomotion.Swim
                || target == DragonLocomotion.Fly || target == DragonLocomotion.Burrow;
            if (intent == DragonBehaviorIntent.Idle && target == DragonLocomotion.Idle && Math.Abs(turnRateDegPerSec) >= TurnInPlaceDegPerSec
                && _clips.Has(DragonLocomotion.Walk) && ActiveAction == DragonInteraction.None)
            {
                target = DragonLocomotion.Walk;
                moving = true;
            }
            Shown = target;
            o.locomotion = target;
            if (moving) o.speed = speed;

            if (ActiveAction == DragonInteraction.None && _pending != DragonReaction.None)
            {
                var r = _pending;
                _pending = DragonReaction.None;
                if (target == DragonLocomotion.Sleep)
                {
                    o.note = "reaction " + r + " dropped: the dragon is asleep";
                }
                else
                {
                    var a = _p.ReactionFor(r);
                    if (a != DragonInteraction.None && !_clips.CanPlay(a)) a = _clips.CanPlay(DragonInteraction.Happy) ? DragonInteraction.Happy : DragonInteraction.None;
                    if (a != DragonInteraction.None) Start(a, ref o);
                }
            }

            if (ActiveAction == DragonInteraction.None && repeat != DragonInteraction.None && _gap <= 0f)
                Start(repeat, ref o);

            // idle variations: only while really idle (intent Idle, shown Idle, nothing running)
            if (intent == DragonBehaviorIntent.Idle && target == DragonLocomotion.Idle && ActiveAction == DragonInteraction.None)
            {
                IdleSeconds += dt;
                if (IdleSeconds >= NextVariationAt)
                {
                    var v = PickVariation();
                    IdleSeconds = 0f;
                    NextVariationAt = NextInterval();
                    if (v != DragonInteraction.None) Start(v, ref o);
                }
            }
            else if (ActiveAction == DragonInteraction.None) IdleSeconds = 0f;

            o.headLookAllowed = ActiveAction == DragonInteraction.None
                && (target == DragonLocomotion.Idle || target == DragonLocomotion.Walk || target == DragonLocomotion.Rest || target == DragonLocomotion.Bask);
            return o;
        }

        private void Start(DragonInteraction a, ref DragonBehaviorOutput o)
        {
            float s = _seconds(a);
            ActiveAction = a;
            ActionRemaining = (s > 0f ? s : 1f) / Math.Max(0.1f, o.tempo);
            o.trigger = a;
        }

        private static void Map(DragonBehaviorIntent i, out DragonLocomotion loco, out DragonInteraction repeat)
        {
            repeat = DragonInteraction.None;
            switch (i)
            {
                case DragonBehaviorIntent.Walk: loco = DragonLocomotion.Walk; break;
                case DragonBehaviorIntent.Run: loco = DragonLocomotion.Run; break;
                case DragonBehaviorIntent.Swim: loco = DragonLocomotion.Swim; break;
                case DragonBehaviorIntent.Fly: loco = DragonLocomotion.Fly; break;
                case DragonBehaviorIntent.Burrow: loco = DragonLocomotion.Burrow; break;
                case DragonBehaviorIntent.Sleep: loco = DragonLocomotion.Sleep; break;
                case DragonBehaviorIntent.Rest: case DragonBehaviorIntent.Recover: loco = DragonLocomotion.Rest; break;
                case DragonBehaviorIntent.Bask: loco = DragonLocomotion.Bask; break;
                case DragonBehaviorIntent.Eat: loco = DragonLocomotion.Idle; repeat = DragonInteraction.Eat; break;
                case DragonBehaviorIntent.Drink: loco = DragonLocomotion.Idle; repeat = DragonInteraction.Drink; break;
                case DragonBehaviorIntent.Play: loco = DragonLocomotion.Idle; repeat = DragonInteraction.Happy; break;
                case DragonBehaviorIntent.LookAround: loco = DragonLocomotion.Idle; repeat = DragonInteraction.LookAround; break;
                case DragonBehaviorIntent.Curious: loco = DragonLocomotion.Idle; repeat = DragonInteraction.Curious; break;
                case DragonBehaviorIntent.Stretch: loco = DragonLocomotion.Idle; repeat = DragonInteraction.Stretch; break;
                default: loco = DragonLocomotion.Idle; break;
            }
        }

        private DragonInteraction PickVariation()
        {
            float a = _clips.CanPlay(DragonInteraction.LookAround) ? Math.Max(0f, _p.weightLookAround) : 0f;
            float b = _clips.CanPlay(DragonInteraction.Curious) ? Math.Max(0f, _p.weightCurious) : 0f;
            float c = _clips.CanPlay(DragonInteraction.Stretch) ? Math.Max(0f, _p.weightStretch) : 0f;
            float d = _clips.CanPlay(DragonInteraction.Happy) ? Math.Max(0f, _p.weightHappy) : 0f;
            float sum = a + b + c + d;
            if (sum <= 0f) return DragonInteraction.None;
            float r = Next01() * sum;
            if ((r -= a) < 0f) return DragonInteraction.LookAround;
            if ((r -= b) < 0f) return DragonInteraction.Curious;
            if ((r -= c) < 0f) return DragonInteraction.Stretch;
            return DragonInteraction.Happy;
        }

        private float NextInterval()
        {
            float lo = Math.Max(0.5f, _p.idleVariationMinSeconds), hi = Math.Max(lo, _p.idleVariationMaxSeconds);
            return lo + (hi - lo) * Next01();
        }

        private float Next01()
        {
            _rng ^= _rng << 13; _rng ^= _rng >> 17; _rng ^= _rng << 5; // xorshift32: deterministic per dragon
            return (_rng & 0xFFFFFF) / 16777216f;
        }

        /// <summary>Stable per-dragon seed from its UID (FNV-1a; string.GetHashCode is not stable across runs).</summary>
        public static uint SeedFor(string uid)
        {
            uint h = 2166136261u;
            if (uid != null) foreach (char ch in uid) { h ^= ch; h *= 16777619u; }
            return h == 0 ? 1u : h;
        }
    }
}
