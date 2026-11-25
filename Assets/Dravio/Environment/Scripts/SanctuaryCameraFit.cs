using UnityEngine;

namespace Dravio.EnvironmentArt
{
    /// <summary>
    /// Landscape-only framing for the fixed orthographic sanctuary camera.
    /// The Fantasy UI panel occupies the right side of the screen. Its width is
    /// measured in screen heights (a Canvas that scales with height), so on 16:9,
    /// 19.5:9 and 20:9 phones the panel hides the same world width. The camera is
    /// shifted right by a constant so the free area stays centred on the habitats;
    /// wider phones reveal extra dressing on both sides. Below <see cref="minAspect"/>
    /// the orthographic size grows to keep the playable width visible.
    /// Visual only: no input, no gameplay, no camera movement at runtime.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public sealed class SanctuaryCameraFit : MonoBehaviour
    {
        [Tooltip("Camera position before the UI offset (from OriginSanctuaryLayout.json).")]
        public Vector3 anchorPosition = new Vector3(0f, 15.27f, -20.66f);
        public Vector3 eulerAngles = new Vector3(32f, 0f, 0f);
        [Min(0.1f)] public float baseOrthographicSize = 6f;
        [Tooltip("Right UI panel width in screen heights.")]
        [Range(0f, 1.5f)] public float rightPanelScreenHeights = 0.5f;
        [Tooltip("Left safe margin (notch) in screen heights.")]
        [Range(0f, 0.5f)] public float leftMarginScreenHeights = 0.05f;
        [Tooltip("Narrowest supported landscape aspect (16:9).")]
        public float minAspect = 16f / 9f;

        Camera _camera;
        float _lastAspect = -1f;
        bool _warnedPortrait;

        void OnEnable()
        {
            _camera = GetComponent<Camera>();
            _lastAspect = -1f;
            Apply();
        }

        void LateUpdate() => Apply();

        // Re-applied on the next LateUpdate; changing components inside OnValidate is avoided.
        void OnValidate() => _lastAspect = -1f;

        /// <summary>Camera pose for a given aspect. Shared by the runtime and editor validation.</summary>
        public static void ComputePose(Vector3 anchor, Vector3 euler, float baseSize, float panel, float margin,
                                       float minAspect, float aspect, out Vector3 position, out Quaternion rotation,
                                       out float orthographicSize)
        {
            rotation = Quaternion.Euler(euler);
            float free = Mathf.Max(aspect - panel - margin, 0.2f);
            float minFree = Mathf.Max(minAspect - panel - margin, 0.2f);
            orthographicSize = aspect < minAspect ? baseSize * minFree / free : baseSize;
            position = anchor + rotation * Vector3.right * (orthographicSize * (panel - margin));
        }

        void Apply()
        {
            if (_camera == null) _camera = GetComponent<Camera>();
            if (_camera == null) return;
            float aspect = _camera.aspect;
            if (Mathf.Approximately(aspect, _lastAspect)) return;
            _lastAspect = aspect;
            if (aspect < 1f && !_warnedPortrait)
            {
                _warnedPortrait = true;
                Debug.LogWarning("Dravio: Origin Sanctuary is landscape-only (16:9 to 20:9). Portrait framing is not supported.");
            }
            ComputePose(anchorPosition, eulerAngles, baseOrthographicSize, rightPanelScreenHeights,
                        leftMarginScreenHeights, minAspect, aspect, out var pos, out var rot, out float size);
            _camera.orthographic = true;
            _camera.orthographicSize = size;
            transform.SetPositionAndRotation(pos, rot);
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (_camera == null) _camera = GetComponent<Camera>();
            if (_camera == null) return;
            float a = _camera.aspect;
            float x1 = 1f - rightPanelScreenHeights / a;
            float x0 = leftMarginScreenHeights / a;
            DrawViewportOnGround(new Rect(x0, 0f, x1 - x0, 1f), new Color(0.3f, 1f, 0.4f, 0.9f));
            DrawViewportOnGround(new Rect(x1, 0f, 1f - x1, 1f), new Color(1f, 0.35f, 0.3f, 0.9f));
        }

        void DrawViewportOnGround(Rect r, Color c)
        {
            Gizmos.color = c;
            var pts = new[] { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin),
                              new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax) };
            var ground = new Plane(Vector3.up, Vector3.zero);
            var world = new Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                var ray = _camera.ViewportPointToRay(new Vector3(pts[i].x, pts[i].y, 0f));
                world[i] = ground.Raycast(ray, out float d) ? ray.GetPoint(d) : ray.origin;
            }
            for (int i = 0; i < 4; i++) Gizmos.DrawLine(world[i], world[(i + 1) % 4]);
        }
#endif
    }
}
