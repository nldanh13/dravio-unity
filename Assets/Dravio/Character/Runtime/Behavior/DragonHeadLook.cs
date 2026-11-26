using UnityEngine;

namespace Dravio.Character
{
    /// <summary>
    /// Head tracking layered on top of the Animator (LateUpdate), U1-CHAR.6. Turns the Neck and Head bones toward a
    /// world target in the dragon's own frame (yaw about its up axis, pitch about its right axis), split between
    /// the two bones and clamped, with smoothed weight so it blends in and out without a snap. Targets behind the
    /// dragon are ignored (the body should turn: the Sanctuary's job). The manager enables it only for near,
    /// full-rate dragons and only while the behaviour planner allows a look (no head-driven one-shot running).
    /// Bones are found by name (Neck, Head) as built by rig_archetype.py; without them the component stays idle.
    /// U1-CHAR.8: a gaze shift of 17 deg or more (a new look or a target jump) triggers a blink on DragonBlink when the
    /// mesh has eyelid shapes (gaze-evoked blinks, Evinger et al. 1994).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DragonHeadLook : MonoBehaviour
    {
        public string neckBoneName = "Neck";
        public string headBoneName = "Head";
        [Tooltip("Degrees")] public float maxYaw = 55f;
        [Tooltip("Degrees")] public float maxPitch = 25f;
        [Range(0f, 1f)] public float neckShare = 0.55f;
        [Tooltip("Seconds for the angles to cover half the remaining distance")] public float halfLife = 0.18f;
        [Tooltip("Seconds of one glance cycle when hold < 1 (shy)")] public float glancePeriod = 3f;

        private Transform _neck, _head;
        private bool _active;
        private Vector3 _target;
        private float _weightWanted, _hold = 1f;
        private float _yaw, _pitch, _weight, _glance;
        private float _lastYaw, _lastPitch;
        private bool _wasLooking;
        private DragonBlink _blink;
        public int GazeBlinks { get; private set; }

        public bool HasBones { get { return _neck != null && _head != null; } }
        public float CurrentWeight { get { return _weight; } }

        private void Awake() { FindBones(); _blink = GetComponent<DragonBlink>(); }

        public void FindBones()
        {
            _neck = _head = null;
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (_neck == null && t.name == neckBoneName) _neck = t;
                else if (_head == null && t.name == headBoneName) _head = t;
            }
        }

        public void SetTarget(bool active, Vector3 worldTarget, float weight, float hold)
        {
            _active = active;
            _target = worldTarget;
            _weightWanted = Mathf.Clamp01(weight);
            _hold = Mathf.Clamp01(hold <= 0f ? 1f : hold);
        }

        public void ResetPose() { _yaw = _pitch = _weight = _glance = _lastYaw = _lastPitch = 0f; _active = _wasLooking = false; }

        private void LateUpdate()
        {
            if (!HasBones) return;
            float dt = Time.deltaTime;
            _glance = (_glance + dt) % Mathf.Max(0.5f, glancePeriod);
            bool looking = _active && (_hold >= 1f || _glance < _hold * glancePeriod);
            float yaw = 0f, pitch = 0f;
            if (looking)
            {
                Vector3 d = transform.InverseTransformDirection(_target - _head.position);
                HeadLookSolver.Angles(d.x, d.y, d.z, maxYaw, maxPitch, out yaw, out pitch);
                float fromY = _wasLooking ? _lastYaw : _yaw, fromP = _wasLooking ? _lastPitch : _pitch;
                float shift = Mathf.Sqrt((yaw - fromY) * (yaw - fromY) + (pitch - fromP) * (pitch - fromP));
                if (DragonExpressionSolver.GazeShiftBlinks(shift)) { GazeBlinks++; if (_blink != null) _blink.BlinkNow(); }
                _lastYaw = yaw; _lastPitch = pitch;
            }
            _wasLooking = looking;
            _weight = HeadLookSolver.Smooth(_weight, looking ? _weightWanted : 0f, dt, halfLife);
            _yaw = HeadLookSolver.Smooth(_yaw, yaw, dt, halfLife);
            _pitch = HeadLookSolver.Smooth(_pitch, pitch, dt, halfLife);
            if (_weight < 0.001f) return;
            float ny, hy, np, hp;
            HeadLookSolver.Split(_yaw * _weight, neckShare, out ny, out hy);
            HeadLookSolver.Split(_pitch * _weight, neckShare, out np, out hp);
            Vector3 up = transform.up, right = transform.right;
            // applied after the Animator wrote this frame's pose: additive, in the dragon's frame
            _neck.rotation = Quaternion.AngleAxis(ny, up) * Quaternion.AngleAxis(-np, right) * _neck.rotation;
            _head.rotation = Quaternion.AngleAxis(hy, up) * Quaternion.AngleAxis(-hp, right) * _head.rotation;
        }
    }
}
