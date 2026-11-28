using System;
using UnityEngine;

namespace Dravio.EnvironmentArt
{
    /// <summary>
    /// Landscape-only framing for the fixed orthographic sanctuary camera.
    ///
    /// Design (U1-ENV.2): the Fantasy UI panel takes the right side of the screen and is
    /// measured in screen heights, so on 16:9, 19.5:9 and 20:9 phones it hides the same
    /// world width; the camera shifts right so the free area stays centred on the habitats.
    ///
    /// U1-ENV.3 adds the real numbers at runtime:
    ///   * the Fantasy UI reports how much of the right edge it covers
    ///     (<see cref="SetFantasyUiOccluderPixels"/>, <see cref="SetFantasyUiPanel"/>, or a
    ///     <see cref="SanctuaryUiInsetSource"/> on the panel);
    ///   * the device Safe Area (<see cref="Screen.safeArea"/>: notch, rounded corners, home bar).
    /// The camera then zooms out only as much as needed and re-centres the free area.
    /// With no report and no safe-area insets the pose is exactly the U1-ENV.2 one.
    /// Other systems read <see cref="FreeViewport"/> or listen to <see cref="FreeViewportChanged"/>.
    /// Visual only: no input, no gameplay.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public sealed class SanctuaryCameraFit : MonoBehaviour
    {
        [Tooltip("Camera position before the UI offset (from OriginSanctuaryLayout.json).")]
        public Vector3 anchorPosition = new Vector3(0f, 15.27f, -20.66f);
        public Vector3 eulerAngles = new Vector3(32f, 0f, 0f);
        [Min(0.1f)] public float baseOrthographicSize = 6f;
        [Tooltip("Design right UI panel width in screen heights (used until the UI reports its real width).")]
        [Range(0f, 1.5f)] public float rightPanelScreenHeights = 0.5f;
        [Tooltip("Design left margin in screen heights.")]
        [Range(0f, 0.5f)] public float leftMarginScreenHeights = 0.05f;
        [Tooltip("Design top HUD margin in screen heights.")]
        [Range(0f, 0.5f)] public float topMarginScreenHeights = 0.06f;
        [Tooltip("Design bottom margin in screen heights.")]
        [Range(0f, 0.5f)] public float bottomMarginScreenHeights = 0.05f;
        [Tooltip("Narrowest supported landscape aspect (16:9).")]
        public float minAspect = 16f / 9f;
        [Tooltip("Follow Screen.safeArea (notch, rounded corners, home indicator).")]
        public bool useSafeArea = true;

        /// <summary>Pixels the Fantasy UI covers from the right screen edge; negative = not reported.</summary>
        public float ReportedUiOccluderPixels { get; private set; } = -1f;
        /// <summary>Free part of the screen (normalised viewport) after UI and safe area.</summary>
        public Rect FreeViewport { get; private set; } = new Rect(0f, 0f, 1f, 1f);
        /// <summary>Insets in screen heights actually used: x = left, y = right, z = top, w = bottom.</summary>
        public Vector4 ActiveInsets { get; private set; }

        public event Action<Rect> FreeViewportChanged;

        /// <summary>U6.3 island view: 1 = the fitted overview; smaller = zoomed in. Runtime only (edit mode stays 1).</summary>
        public float ViewZoom { get; private set; } = 1f;
        /// <summary>U6.3 island view offset in the camera plane (x = camera right, y = camera up), world units.</summary>
        public Vector2 ViewOffset { get; private set; }
        /// <summary>Orthographic size of the fitted overview before <see cref="ViewZoom"/>.</summary>
        public float FittedSize { get; private set; }

        Camera _camera;
        bool _dirty = true;
        bool _warnedPortrait;
        float _lastAspect = -1f;
        Rect _lastSafe;
        Vector2 _lastScreen;
        Rect? _safeAreaOverride;
        RectTransform _uiPanel;
        Camera _uiCamera;

        /// <summary>Inputs for <see cref="ComputeFraming"/>; insets are in screen heights.</summary>
        public struct FramingInput
        {
            public Vector3 anchor, euler;
            public float baseSize, minAspect, aspect;
            public float designLeft, designRight, designTop, designBottom;
            public float left, right, top, bottom;
        }

        public struct FramingResult
        {
            public Vector3 position;
            public Quaternion rotation;
            public float orthographicSize;
            public Rect freeViewport;
        }

        void OnEnable()
        {
            _camera = GetComponent<Camera>();
            _dirty = true;
            Apply();
        }

        void LateUpdate()
        {
            if (_uiPanel != null) MeasurePanel();
            Apply();
        }

        void OnValidate() => _dirty = true;

        // ------------------------------------------------------------ public API
        /// <summary>
        /// Reports the real Fantasy UI: distance in pixels from the right screen edge to the
        /// panel's left edge (so it already includes any safe-area padding the UI uses).
        /// Pass a negative value to go back to the design width.
        /// </summary>
        public void SetFantasyUiOccluderPixels(float pixelsFromRightEdge)
        {
            if (Mathf.Approximately(pixelsFromRightEdge, ReportedUiOccluderPixels)) return;
            ReportedUiOccluderPixels = pixelsFromRightEdge;
            _dirty = true;
        }

        /// <summary>Tracks a UI panel RectTransform each frame (Overlay canvas: leave uiCamera null).</summary>
        public void SetFantasyUiPanel(RectTransform panel, Camera uiCamera = null)
        {
            _uiPanel = panel;
            _uiCamera = uiCamera;
            if (panel == null) SetFantasyUiOccluderPixels(-1f);
            else MeasurePanel();
        }

        /// <summary>
        /// U6.3: zoom/pan on top of the fitted pose (callers keep it inside the overview, see IslandViewMath).
        /// The framing itself (ComputeFraming, FreeViewport) is unchanged.
        /// </summary>
        public void SetView(float zoom, Vector2 offset)
        {
            zoom = Mathf.Clamp(zoom, 0.1f, 2f); // U6.5: > 1 zooms out past the overview (island in the sea)
            if (Mathf.Approximately(zoom, ViewZoom) && offset == ViewOffset) return;
            ViewZoom = zoom;
            ViewOffset = offset;
            _dirty = true;
        }

        public void ClearFantasyUi()
        {
            _uiPanel = null;
            SetFantasyUiOccluderPixels(-1f);
        }

        /// <summary>Overrides Screen.safeArea (pixels), e.g. for tests. Null restores the device value.</summary>
        public void SetSafeAreaOverride(Rect? pixels)
        {
            _safeAreaOverride = pixels;
            _dirty = true;
        }

        /// <summary>Pixels covered from the right edge by a RectTransform, or -1 if not visible.</summary>
        public static float MeasureOccluderPixels(RectTransform panel, Camera uiCamera, float screenWidth)
        {
            if (panel == null || !panel.gameObject.activeInHierarchy) return -1f;
            var corners = new Vector3[4];
            panel.GetWorldCorners(corners);
            float minX = float.MaxValue;
            for (int i = 0; i < 4; i++)
                minX = Mathf.Min(minX, RectTransformUtility.WorldToScreenPoint(uiCamera, corners[i]).x);
            return Mathf.Clamp(screenWidth - minX, 0f, screenWidth);
        }

        // --------------------------------------------------------------- framing
        /// <summary>
        /// Pose for the given insets (screen heights). The size only grows, enough to keep
        /// the design free width at <c>minAspect</c> and the design free height; the free
        /// area is re-centred where the design centred it. Mirrors scripts/art/environment/framing.py.
        /// </summary>
        public static FramingResult ComputeFraming(in FramingInput i)
        {
            float aspect = Mathf.Max(i.aspect, 0.1f);
            float needW = (i.minAspect - i.designLeft - i.designRight) / Mathf.Max(aspect - i.left - i.right, 0.05f);
            float needH = (1f - i.designTop - i.designBottom) / Mathf.Max(1f - i.top - i.bottom, 0.05f);
            float size = i.baseSize * Mathf.Max(1f, needW, needH);
            var rot = Quaternion.Euler(i.euler);
            var pos = i.anchor
                      + rot * Vector3.right * (size * (i.right - i.left))
                      + rot * Vector3.up * (size * ((i.bottom - i.designBottom) - (i.top - i.designTop)));
            float x0 = i.left / aspect, x1 = 1f - i.right / aspect;
            return new FramingResult
            {
                position = pos, rotation = rot, orthographicSize = size,
                freeViewport = Rect.MinMaxRect(x0, i.bottom, Mathf.Max(x0, x1), Mathf.Max(i.bottom, 1f - i.top)),
            };
        }

        /// <summary>U1-ENV.2 signature (design insets only). Kept for the editor validator and callers.</summary>
        public static void ComputePose(Vector3 anchor, Vector3 euler, float baseSize, float panel, float margin,
                                       float minAspect, float aspect, out Vector3 position, out Quaternion rotation,
                                       out float orthographicSize)
        {
            var r = ComputeFraming(new FramingInput
            {
                anchor = anchor, euler = euler, baseSize = baseSize, minAspect = minAspect, aspect = aspect,
                designLeft = margin, designRight = panel, designTop = 0f, designBottom = 0f,
                left = margin, right = panel, top = 0f, bottom = 0f,
            });
            position = r.position;
            rotation = r.rotation;
            orthographicSize = r.orthographicSize;
        }

        /// <summary>Insets (screen heights) from a safe area and an optional UI report, as used at runtime.</summary>
        public Vector4 ResolveInsets(float screenWidth, float screenHeight, Rect safeArea, float uiOccluderPixels)
        {
            float h = Mathf.Max(1f, screenHeight);
            float safeL = Mathf.Max(0f, safeArea.xMin) / h;
            float safeR = Mathf.Max(0f, screenWidth - safeArea.xMax) / h;
            float safeB = Mathf.Max(0f, safeArea.yMin) / h;
            float safeT = Mathf.Max(0f, screenHeight - safeArea.yMax) / h;
            float right = uiOccluderPixels >= 0f
                ? Mathf.Max(uiOccluderPixels / h, safeR)
                : rightPanelScreenHeights + safeR;
            return new Vector4(Mathf.Max(leftMarginScreenHeights, safeL), right,
                               Mathf.Max(topMarginScreenHeights, safeT), Mathf.Max(bottomMarginScreenHeights, safeB));
        }

        void MeasurePanel()
        {
            float px = MeasureOccluderPixels(_uiPanel, _uiCamera, Screen.width);
            SetFantasyUiOccluderPixels(px);
        }

        void Apply()
        {
            if (_camera == null) _camera = GetComponent<Camera>();
            if (_camera == null) return;
            float aspect = _camera.aspect;
            var screen = new Vector2(Screen.width, Screen.height);
            Rect safe = _safeAreaOverride ?? (useSafeArea ? Screen.safeArea : new Rect(0f, 0f, screen.x, screen.y));
            if (!_dirty && Mathf.Approximately(aspect, _lastAspect) && safe == _lastSafe && screen == _lastScreen) return;
            _dirty = false;
            _lastAspect = aspect;
            _lastSafe = safe;
            _lastScreen = screen;
            if (aspect < 1f && !_warnedPortrait)
            {
                _warnedPortrait = true;
                Debug.LogWarning("Dravio: Origin Sanctuary is landscape-only (16:9 to 20:9). Portrait framing is not supported.");
            }

            // Screen.safeArea is in screen pixels; when the camera renders to a smaller
            // target (e.g. editor previews) the ratios still hold.
            Vector4 ins = ResolveInsets(screen.x, screen.y, safe, ReportedUiOccluderPixels);
            var r = ComputeFraming(new FramingInput
            {
                anchor = anchorPosition, euler = eulerAngles, baseSize = baseOrthographicSize,
                minAspect = minAspect, aspect = aspect,
                designLeft = leftMarginScreenHeights, designRight = rightPanelScreenHeights,
                designTop = topMarginScreenHeights, designBottom = bottomMarginScreenHeights,
                left = ins.x, right = ins.y, top = ins.z, bottom = ins.w,
            });
            _camera.orthographic = true;
            FittedSize = r.orthographicSize;
            _camera.orthographicSize = r.orthographicSize * ViewZoom;
            transform.SetPositionAndRotation(
                r.position + r.rotation * Vector3.right * ViewOffset.x + r.rotation * Vector3.up * ViewOffset.y,
                r.rotation);
            ActiveInsets = ins;
            if (r.freeViewport != FreeViewport)
            {
                FreeViewport = r.freeViewport;
                FreeViewportChanged?.Invoke(FreeViewport);
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (_camera == null) _camera = GetComponent<Camera>();
            if (_camera == null) return;
            var f = FreeViewport;
            DrawViewportOnGround(f, new Color(0.3f, 1f, 0.4f, 0.9f));
            DrawViewportOnGround(Rect.MinMaxRect(f.xMax, 0f, 1f, 1f), new Color(1f, 0.35f, 0.3f, 0.9f));
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
