using UnityEngine;

namespace Dravio.EnvironmentArt
{
    /// <summary>
    /// Keeps the fixed orthographic sanctuary camera readable on any aspect ratio.
    /// Landscape keeps the authored size; narrow (portrait phone) screens grow the
    /// orthographic size so at least <see cref="minVisibleWidth"/> world units fit.
    /// Visual only: no input, no gameplay, no camera movement.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public sealed class SanctuaryCameraFit : MonoBehaviour
    {
        [Min(0.1f)] public float baseOrthographicSize = 6.6f;
        [Min(0.1f)] public float minVisibleWidth = 13f;

        Camera _camera;
        float _lastAspect = -1f;

        void OnEnable()
        {
            _camera = GetComponent<Camera>();
            _lastAspect = -1f;
            Apply();
        }

        void LateUpdate() => Apply();

        // Re-applied on the next LateUpdate; changing another component inside
        // OnValidate is avoided on purpose.
        void OnValidate() => _lastAspect = -1f;

        void Apply()
        {
            if (_camera == null) _camera = GetComponent<Camera>();
            if (_camera == null || !_camera.orthographic) return;
            float aspect = _camera.aspect;
            if (Mathf.Approximately(aspect, _lastAspect)) return;
            _lastAspect = aspect;
            float fitWidth = minVisibleWidth / (2f * Mathf.Max(aspect, 0.01f));
            _camera.orthographicSize = Mathf.Max(baseOrthographicSize, fitWidth);
        }
    }
}
