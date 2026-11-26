using UnityEngine;
using UnityEngine.InputSystem;

namespace Dravio
{
    // Visual prototype only: no economy, health, hunger or offline simulation.
    public sealed class DragonWanderPreview : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private DragonTurntable turntable;
        [SerializeField] private float facingOffset = 180f;
        [SerializeField] private float speed = .55f;
        [SerializeField] private float turnSpeed = 180f;
        private static readonly Vector2[] Waypoints = {
            new Vector2(-1.1f,-.8f), new Vector2(-.9f,.35f),
            new Vector2(.9f,.35f), new Vector2(1.1f,-.8f)
        };
        private Vector3 origin;
        private int next;
        private float rest = 2f;
        private bool inspecting;
        private static readonly int Moving = Animator.StringToHash("Moving");

        public void Configure(Animator animation, DragonTurntable rotation)
        {
            animator = animation;
            turntable = rotation;
        }

        private void Start()
        {
            origin = transform.position;
            if (animator == null || turntable == null)
            {
                Debug.LogError("Dravio: wander requires Animator and DragonTurntable.", this);
                enabled = false;
                return;
            }
            animator.applyRootMotion = false;
            SetInspection(false);
        }

        private void SetInspection(bool value)
        {
            inspecting = value;
            turntable.enabled = value;
            animator.SetBool(Moving, false);
            rest = 2f;
        }

        private void OnDisable()
        {
            if (turntable != null) turntable.enabled = true;
            if (animator != null) animator.SetBool(Moving, false);
        }

        private void Update()
        {
            var keys = Keyboard.current;
            if (keys != null && keys.spaceKey.wasPressedThisFrame)
                SetInspection(!inspecting);
            if (inspecting) return;
            float dt = Time.deltaTime;
            if (rest > 0)
            {
                rest -= dt;
                animator.SetBool(Moving, false);
                return;
            }
            Vector2 wp = Waypoints[next];
            Vector3 target = origin + new Vector3(wp.x, 0, wp.y);
            Vector3 direction = target - transform.position;
            direction.y = 0;
            if (direction.sqrMagnitude < .0036f)
            {
                next = (next + 1) % Waypoints.Length;
                rest = 2.2f;
                animator.SetBool(Moving, false);
                return;
            }
            Quaternion heading = Quaternion.LookRotation(direction.normalized, Vector3.up)
                * Quaternion.Euler(0, facingOffset, 0);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, heading, turnSpeed * dt);
            if (Quaternion.Angle(transform.rotation, heading) > 12f)
            {
                animator.SetBool(Moving, false);
                return;
            }
            transform.position = Vector3.MoveTowards(transform.position, target, speed * dt);
            animator.SetBool(Moving, true);
        }
    }
}
