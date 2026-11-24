using UnityEngine;
using UnityEngine.InputSystem;

namespace Dravio
{
    // Visual-only prototype: rotate the dragon, never the fixed world camera.
    public sealed class DragonTurntable : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private float degreesPerScreen = 360f;
        private bool dragging;
        private bool touchDrag;
        private Vector2 previous;
        private Quaternion initial;

        private void Awake() => initial = transform.rotation;

        public void SetCamera(Camera camera) => viewCamera = camera;

        private void OnDisable() => dragging = false;
        private void OnApplicationFocus(bool focused) { if (!focused) dragging = false; }

        private bool HitsDragon(Vector2 position)
        {
            return viewCamera != null &&
                Physics.Raycast(viewCamera.ScreenPointToRay(position), out RaycastHit hit) &&
                (hit.transform == transform || hit.transform.IsChildOf(transform));
        }

        private void Begin(Vector2 position, bool touch)
        {
            dragging = HitsDragon(position);
            touchDrag = touch;
            previous = position;
        }

        private void Move(Vector2 position)
        {
            if (!dragging) return;
            float delta = (position.x - previous.x) * degreesPerScreen / Mathf.Max(1, Screen.width);
            transform.Rotate(Vector3.up, -delta, Space.World);
            previous = position;
        }

        private void Update()
        {
            var touch = Touchscreen.current;
            var mouse = Mouse.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
                Begin(touch.primaryTouch.position.ReadValue(), true);
            else if (!touchDrag || !dragging)
            {
                if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                    Begin(mouse.position.ReadValue(), false);
            }

            if (dragging)
            {
                if (touchDrag)
                {
                    if (touch == null || !touch.primaryTouch.press.isPressed) dragging = false;
                    else Move(touch.primaryTouch.position.ReadValue());
                }
                else
                {
                    if (mouse == null || !mouse.leftButton.isPressed) dragging = false;
                    else Move(mouse.position.ReadValue());
                }
            }

            var keys = Keyboard.current;
            if (keys == null) return;
            if (keys.leftArrowKey.isPressed) transform.Rotate(Vector3.up, 90 * Time.unscaledDeltaTime, Space.World);
            if (keys.rightArrowKey.isPressed) transform.Rotate(Vector3.up, -90 * Time.unscaledDeltaTime, Space.World);
            if (keys.rKey.wasPressedThisFrame) transform.rotation = initial;
        }
    }
}
