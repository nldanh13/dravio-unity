using System;

namespace Dravio.Character
{
    /// <summary>How an expression is shown through the bones every rig has (U1-CHAR.8). Angles in degrees.</summary>
    public struct DragonExpressionState
    {
        /// <summary>-1 tucked .. +1 high (added on the first tail bone).</summary>
        public float tailCarriage;
        /// <summary>Side-to-side tail wag: amplitude at the tip and frequency (0 = none).</summary>
        public float tailWagDeg, tailWagHz;
        /// <summary>Ear-fin perk 0..1 (EarFin bones only).</summary>
        public float earPerk;
        /// <summary>Head roll (+ = tilt to the dragon's right) and neck pitch (+ = raised).</summary>
        public float headTiltDeg, neckRaiseDeg;
        public DragonExpression expression;
    }

    /// <summary>
    /// Expression presentation (U1-CHAR.8): which expression the dragon shows (from the running clip, the social act
    /// and the personality) and how much tail, ear fins, head tilt and neck carry it. There is no face rig: eyes are
    /// shown through gaze (head look-at with gaze-evoked blinks where the mesh has eyelid shape keys). Values are
    /// art-direction defaults, tuned with the art director in this file; they never touch Domain state.
    /// </summary>
    public static class DragonExpressionSolver
    {
        public const float MaxTiltDeg = 15f, MaxNeckDeg = 10f, MaxWagDeg = 25f;

        public static DragonExpressionState For(DragonExpression e, float personalityTailCarriage)
        {
            var s = new DragonExpressionState { expression = e, tailCarriage = personalityTailCarriage };
            switch (e)
            {
                case DragonExpression.Happy: s.tailCarriage += 0.4f; s.tailWagDeg = 18f; s.tailWagHz = 2.0f; s.earPerk = 0.6f; s.neckRaiseDeg = 4f; break;
                case DragonExpression.Playful: s.tailCarriage += 0.6f; s.tailWagDeg = 22f; s.tailWagHz = 2.5f; s.earPerk = 0.8f; s.neckRaiseDeg = 2f; break;
                case DragonExpression.Curious: s.tailCarriage += 0.2f; s.tailWagDeg = 6f; s.tailWagHz = 1.0f; s.earPerk = 1f; s.headTiltDeg = 12f; s.neckRaiseDeg = 6f; break;
                case DragonExpression.Alert: s.tailCarriage += 0.3f; s.earPerk = 1f; s.neckRaiseDeg = 8f; break;
                case DragonExpression.Calm: s.tailCarriage -= 0.1f; s.tailWagDeg = 5f; s.tailWagHz = 0.4f; s.earPerk = 0.2f; s.neckRaiseDeg = -2f; break;
                case DragonExpression.Shy: s.tailCarriage = Math.Min(s.tailCarriage, -0.6f); s.headTiltDeg = 6f; s.neckRaiseDeg = -8f; break;
                case DragonExpression.Affection: s.tailCarriage += 0.3f; s.tailWagDeg = 10f; s.tailWagHz = 1.2f; s.earPerk = 0.5f; s.headTiltDeg = 8f; s.neckRaiseDeg = -3f; break;
                case DragonExpression.Sleepy: s.tailCarriage -= 0.2f; s.neckRaiseDeg = -6f; break;
            }
            s.tailCarriage = Clamp(s.tailCarriage, 1f);
            s.headTiltDeg = Clamp(s.headTiltDeg, MaxTiltDeg);
            s.neckRaiseDeg = Clamp(s.neckRaiseDeg, MaxNeckDeg);
            s.tailWagDeg = Math.Min(Math.Max(0f, s.tailWagDeg), MaxWagDeg);
            return s;
        }

        /// <summary>Expression without a social act: from the running one-shot, the looping state and the personality.</summary>
        public static DragonExpression FromContext(DragonInteraction active, DragonLocomotion shown, DragonPersonalityStyle style)
        {
            switch (active)
            {
                case DragonInteraction.Happy: case DragonInteraction.Greet: return DragonExpression.Happy;
                case DragonInteraction.Curious: return DragonExpression.Curious;
                case DragonInteraction.LookAround: return style == DragonPersonalityStyle.Shy ? DragonExpression.Shy : DragonExpression.Alert;
                case DragonInteraction.Stretch: return DragonExpression.Playful;
                case DragonInteraction.Nuzzle: return DragonExpression.Affection;
                case DragonInteraction.Eat: case DragonInteraction.Drink: return DragonExpression.Neutral;
            }
            if (active != DragonInteraction.None) return DragonExpression.Neutral; // transitions: no layered expression
            switch (shown)
            {
                case DragonLocomotion.Sleep: return DragonExpression.Sleepy;
                case DragonLocomotion.Rest: case DragonLocomotion.Bask: case DragonLocomotion.SwimRest: return DragonExpression.Calm;
            }
            return style == DragonPersonalityStyle.Shy ? DragonExpression.Shy : DragonExpression.Neutral;
        }

        /// <summary>
        /// Whether a head turn is large enough to carry a gaze-evoked blink: in humans the lid closes with every head
        /// turn above about 17 deg (Evinger et al. 1994), and many vertebrates blink with large gaze shifts.
        /// </summary>
        public static bool GazeShiftBlinks(float shiftDeg) { return Math.Abs(shiftDeg) >= GazeBlinkDeg; }
        public const float GazeBlinkDeg = 17f;

        private static float Clamp(float v, float lim) { return v < -lim ? -lim : (v > lim ? lim : v); }
    }
}
