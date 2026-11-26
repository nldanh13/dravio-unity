using System;
using UnityEngine;

namespace Dravio.EnvironmentArt
{
    /// <summary>
    /// U1-ENV.4 cosmetic light quads: caustics scrolling over the seabed and soft sun
    /// shafts through the water (Ocean). Alpha follows the time-of-day light intensity,
    /// so they fade out at night. Uses one MaterialPropertyBlock per renderer (no material
    /// copies), updates at the EnvironmentQuality interval, and hides the shafts on the
    /// Low tier. Purely visual: no colliders, no gameplay state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnvironmentScrollingFx : MonoBehaviour
    {
        [Serializable]
        public sealed class Entry
        {
            public Renderer renderer;
            public string kind;
            [Range(0f, 1f)] public float alpha = 0.3f;
            public Vector2 scroll;
            public Vector2 tiling = Vector2.one;
            [Tooltip("Seconds per slow breathing cycle of the alpha (0 = steady).")]
            public float pulseSeconds = 7f;
            [HideInInspector] public float phase;
        }

        public EnvironmentTimeOfDay timeOfDay;
        public Color tint = new Color(0.8f, 1f, 1f, 1f);
        [Tooltip("Light intensity that gives the full alpha (day value of the zone).")]
        public float fullLightIntensity = 1.05f;
        public Entry[] entries = Array.Empty<Entry>();

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");

        MaterialPropertyBlock _mpb;
        float _next;
        EnvironmentQuality.Tier _tier;

        void OnEnable()
        {
            _mpb ??= new MaterialPropertyBlock();
            _tier = EnvironmentQuality.Current;
            EnvironmentQuality.Changed += OnTier;
            for (int i = 0; i < entries.Length; i++) entries[i].phase = i * 1.37f;
            Apply(0f);
        }

        void OnDisable() => EnvironmentQuality.Changed -= OnTier;

        void OnTier(EnvironmentQuality.Tier t) => _tier = t;

        void Update()
        {
            if (!Application.isPlaying) return;
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + EnvironmentQuality.UpdateInterval(_tier) * 0.5f;
            Apply(Time.timeSinceLevelLoad);
        }

        void Apply(float t)
        {
            float light = 1f;
            if (timeOfDay != null) light = Mathf.Clamp01(timeOfDay.State.lightIntensity / Mathf.Max(0.01f, fullLightIntensity));
            foreach (var e in entries)
            {
                if (e?.renderer == null) continue;
                bool shaft = e.kind == "light_shaft";
                bool visible = light > 0.02f && !(shaft && _tier == EnvironmentQuality.Tier.Low);
                if (e.renderer.enabled != visible) e.renderer.enabled = visible;
                if (!visible) continue;
                float pulse = e.pulseSeconds > 0f ? 0.8f + 0.2f * Mathf.Sin((t / e.pulseSeconds + e.phase) * Mathf.PI * 2f) : 1f;
                e.renderer.GetPropertyBlock(_mpb);
                var c = tint;
                c.a = e.alpha * light * pulse;
                _mpb.SetColor(BaseColorId, c);
                Vector2 off = e.scroll * t;
                _mpb.SetVector(BaseMapStId, new Vector4(e.tiling.x, e.tiling.y, off.x % 1f, off.y % 1f));
                e.renderer.SetPropertyBlock(_mpb);
            }
        }
    }
}
