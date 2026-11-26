using UnityEngine;

namespace Dravio.Character
{
    /// <summary>
    /// Procedural motion shaping layered after the Animator (LateUpdate), U1-CHAR.7:
    ///   - Air: bank into turns and pitch with the climb; Water: pitch with the climb;
    ///   - Water/Ground/Air: body and tail curve into the turn (spread over the spine/tail bones);
    ///   - tail carriage from the personality style and ear-fin perk (EarFin bones only, when the rig has them);
    ///   - U1-CHAR.7b: the bank follows a coordinated turn (MotionShapeSolver.CoordinatedBankDeg) and the neck/head
    ///     counter-roll it (gaze stabilisation, headRollStabilisation);
    ///   - U1-CHAR.8 expression: tail wag (side to side, growing to the tip), head tilt, neck raise/lower
    ///     (<see cref="SetExpressionState"/>; values from DragonExpressionSolver), smoothed.
    /// Banking/pitching rotate the Root bone about the body centre, so the dragon's position (owned by the Sanctuary)
    /// never moves. Horns and crests have no bones (anatomy: rigid / not rigged) and are never animated.
    /// The manager enables it only for near, full-rate dragons; angles are smoothed and clamped (MotionShapeSolver).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DragonMotionShaper : MonoBehaviour
    {
        [Tooltip("Seconds for the shaping angles to cover half the remaining distance")] public float halfLife = 0.25f;
        [Tooltip("Degrees per personality step of tail carriage (+ up)")] public float tailCarriageDeg = 10f;
        [Tooltip("Share of the body bank the neck and head take back (1 = head level; birds hold it nearly level)")]
        [Range(0f, 1f)] public float headRollStabilisation = 1f;

        private Transform _root, _pivotA, _pivotB, _neck, _head;
        private Transform[] _chain = new Transform[0];
        private Transform[] _earFins = new Transform[0];
        private DragonMotionMode _mode;
        private float _turn, _vSpeed, _speed, _maxTurn;
        private float _bank, _pitch, _bend;
        public float TailCarriage { get; set; }   // -1 tucked (shy) .. +1 high (playful)
        public float EarPerk { get; set; }        // 0..1
        private float _wagDeg, _wagHz, _tilt, _neckRaise, _wagPhase;
        private float _wagDegS, _tiltS, _neckS;
        private Transform[] _tails = new Transform[0];
        public DragonExpression Expression { get; private set; }

        /// <summary>U1-CHAR.8 expression layer (also sets TailCarriage and EarPerk).</summary>
        public void SetExpressionState(DragonExpressionState s)
        {
            Expression = s.expression;
            TailCarriage = s.tailCarriage; EarPerk = s.earPerk;
            _wagDeg = s.tailWagDeg; _wagHz = s.tailWagHz; _tilt = s.headTiltDeg; _neckRaise = s.neckRaiseDeg;
        }

        public bool HasBones { get { return _root != null; } }

        private void Awake() { FindBones(); }

        public void FindBones()
        {
            _root = _pivotA = _pivotB = _neck = _head = null;
            var chain = new System.Collections.Generic.List<Transform>();
            var ears = new System.Collections.Generic.List<Transform>();
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                if (n == "Root") _root = t;
                else if (n == "Chest" || n == "Spine1") _pivotA = _pivotA ?? t;
                else if (n == "Pelvis") _pivotB = t;
                else if (n == "Neck") _neck = t;
                else if (n == "Head") _head = t;
                if ((n.StartsWith("Spine") || n.StartsWith("Tail")) && n != "Spine") chain.Add(t);
                if (n.StartsWith("EarFin_")) ears.Add(t);
            }
            _chain = chain.ToArray();
            _earFins = ears.ToArray();
            _tails = System.Array.FindAll(_chain, t => t.name.StartsWith("Tail"));
        }

        public void SetInput(DragonMotionMode mode, float turnRateDegPerSec, float verticalSpeed, float speed, float maxTurnRate)
        {
            _mode = mode; _turn = turnRateDegPerSec; _vSpeed = verticalSpeed; _speed = speed; _maxTurn = maxTurnRate;
        }

        public void ResetPose()
        {
            _bank = _pitch = _bend = 0f; TailCarriage = 0f; EarPerk = 0f; _turn = _vSpeed = _speed = 0f;
            _wagDeg = _wagHz = _tilt = _neckRaise = _wagPhase = _wagDegS = _tiltS = _neckS = 0f; Expression = DragonExpression.Neutral;
        }

        private void LateUpdate()
        {
            if (_root == null) return;
            float bank, pitch, bend, dt = Time.deltaTime;
            MotionShapeSolver.Compute(_mode, _turn, _vSpeed, _speed, _maxTurn, out bank, out pitch, out bend);
            _bank = HeadLookSolver.Smooth(_bank, bank, dt, halfLife);
            _pitch = HeadLookSolver.Smooth(_pitch, pitch, dt, halfLife);
            _bend = HeadLookSolver.Smooth(_bend, bend, dt, halfLife);
            Vector3 up = transform.up, right = transform.right, fwd = transform.forward;
            if (Mathf.Abs(_bank) > 0.01f || Mathf.Abs(_pitch) > 0.01f)
            {
                Vector3 pivot = _pivotA != null && _pivotB != null ? 0.5f * (_pivotA.position + _pivotB.position) : _root.position;
                _root.RotateAround(pivot, fwd, _bank);
                _root.RotateAround(pivot, right, -_pitch);
                if (_head != null && Mathf.Abs(_bank) > 0.01f)
                {
                    float neckRoll, headRoll;
                    MotionShapeSolver.HeadRollCompensation(_bank, headRollStabilisation, out neckRoll, out headRoll);
                    if (_neck != null) _neck.rotation = Quaternion.AngleAxis(neckRoll, fwd) * _neck.rotation;
                    else headRoll += neckRoll;
                    _head.rotation = Quaternion.AngleAxis(headRoll, fwd) * _head.rotation;
                }
            }
            if (_chain.Length > 0 && Mathf.Abs(_bend) > 0.01f)
            {
                float per = _bend / _chain.Length;
                for (int i = 0; i < _chain.Length; i++) _chain[i].rotation = Quaternion.AngleAxis(per, up) * _chain[i].rotation;
            }
            if (Mathf.Abs(TailCarriage) > 0.01f)
                for (int i = 0; i < _chain.Length; i++)
                    if (_chain[i].name.StartsWith("Tail")) { _chain[i].rotation = Quaternion.AngleAxis(-TailCarriage * tailCarriageDeg, right) * _chain[i].rotation; break; }
            _wagDegS = HeadLookSolver.Smooth(_wagDegS, _wagDeg, dt, halfLife);
            _tiltS = HeadLookSolver.Smooth(_tiltS, _tilt, dt, halfLife);
            _neckS = HeadLookSolver.Smooth(_neckS, _neckRaise, dt, halfLife);
            _wagPhase = (_wagPhase + dt * _wagHz) % 1f;
            if (_tails.Length > 0 && _wagDegS > 0.05f)
                for (int i = 0; i < _tails.Length; i++)
                {
                    // travelling wave toward the tip; the tip carries the full amplitude
                    float a = MotionShapeSolver.TailWagDeg(_wagDegS, _wagPhase, i, _tails.Length);
                    _tails[i].rotation = Quaternion.AngleAxis(a, up) * _tails[i].rotation;
                }
            if (_neck != null && Mathf.Abs(_neckS) > 0.01f) _neck.rotation = Quaternion.AngleAxis(-_neckS, right) * _neck.rotation;
            if (_head != null && Mathf.Abs(_tiltS) > 0.01f) _head.rotation = Quaternion.AngleAxis(-_tiltS, fwd) * _head.rotation;
            if (EarPerk > 0.01f)
                for (int i = 0; i < _earFins.Length; i++)
                {
                    float sign = _earFins[i].name.EndsWith("_L") ? 1f : -1f;
                    _earFins[i].rotation = Quaternion.AngleAxis(sign * EarPerk * 15f, fwd) * _earFins[i].rotation;
                }
        }
    }
}
