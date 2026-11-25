using UnityEngine;

namespace Dravio.EnvironmentArt
{
    /// <summary>
    /// Drop-in bridge for the Fantasy UI team: add this to the right-hand panel's
    /// RectTransform and the sanctuary camera keeps the habitats clear of it, using the
    /// panel's real on-screen width (any Canvas scaler, any device). No code in the UI is
    /// needed and the UI is not modified. When the panel is hidden or this component is
    /// disabled, the camera returns to the design width.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public sealed class SanctuaryUiInsetSource : MonoBehaviour
    {
        [Tooltip("Camera to keep clear. Empty = the camera with SanctuaryCameraFit tagged MainCamera.")]
        public SanctuaryCameraFit cameraFit;

        void OnEnable()
        {
            if (cameraFit == null && Camera.main != null) cameraFit = Camera.main.GetComponent<SanctuaryCameraFit>();
            if (cameraFit == null) cameraFit = FindAnyObjectByType<SanctuaryCameraFit>();
            if (cameraFit != null) cameraFit.SetFantasyUiPanel((RectTransform)transform, CanvasCamera());
        }

        void OnDisable()
        {
            if (cameraFit != null) cameraFit.ClearFantasyUi();
        }

        Camera CanvasCamera()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return null;
            canvas = canvas.rootCanvas;
            return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }
    }
}
