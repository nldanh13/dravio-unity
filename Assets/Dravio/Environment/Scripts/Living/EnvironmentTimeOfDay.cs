using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dravio.EnvironmentArt
{
    /// <summary>
    /// Day, dusk and night cycle for the sanctuary. Drives the single directional light
    /// (sun by day, moon by night), the trilight ambient (written straight into the
    /// ambient probe that the Painterly shader samples), fog and camera background, and
    /// the shader globals for night glow. Other living systems read <see cref="State"/>.
    ///
    /// Visual only: it never changes game state. Time comes from one of three sources:
    /// Simulated (dayLengthMinutes per day), DeviceClock (local time) or External, where
    /// the game calls <see cref="SetHour"/> (for example from its own world clock).
    /// Works in edit mode so designers can scrub <see cref="hour"/>.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class EnvironmentTimeOfDay : MonoBehaviour
    {
        public enum TimeSource { Simulated, DeviceClock, External }

        public SanctuaryLivingData data;
        [Tooltip("Directional light used as sun by day and moon by night.")]
        public Light mainLight;
        [Tooltip("Optional: camera whose background colour follows the fog colour.")]
        public Camera targetCamera;

        public TimeSource source = TimeSource.Simulated;
        [Range(0f, 24f)] public float hour = 13f;
        [Tooltip("Real minutes per simulated day. 0 uses the data value.")]
        [Min(0f)] public float dayLengthMinutes = 0f;
        [Tooltip("Multiplier on the simulated clock (pause with 0).")]
        [Min(0f)] public float timeScale = 1f;
        [Tooltip("Scale on the ambient colours (painterly scenes look flat above 1).")]
        [Range(0.5f, 1.5f)] public float ambientScale = 1f;

        public SanctuaryLivingData.State State { get; private set; }
        public DayPhase Phase => State.phase;
        /// <summary>0 at full day, 1 at full night (smooth through dawn and dusk).</summary>
        public float Nightness => Mathf.Clamp01(State.nightGlow);

        public event Action<DayPhase, DayPhase> PhaseChanged;

        static readonly int LivingId = Shader.PropertyToID("_DravioLiving");

        float _nextApply;
        float _lastAppliedHour = -1f;
        bool _hasPhase;
        DayPhase _phase;

        void OnEnable()
        {
            _lastAppliedHour = -1f;
            Apply(true);
        }

        void OnDisable()
        {
            Shader.SetGlobalVector(LivingId, Vector4.zero);
        }

        void OnValidate() => _lastAppliedHour = -1f;

        /// <summary>Sets the clock (any value, wraps at 24). Use with TimeSource.External.</summary>
        public void SetHour(float h)
        {
            hour = SanctuaryLivingData.WrapHour(h);
            Apply(true);
        }

        void Update()
        {
            if (data == null) return;
            if (Application.isPlaying)
            {
                switch (source)
                {
                    case TimeSource.Simulated:
                        float minutes = dayLengthMinutes > 0f ? dayLengthMinutes : Mathf.Max(0.1f, data.dayLengthMinutes);
                        hour = SanctuaryLivingData.WrapHour(hour + Time.deltaTime * timeScale * 24f / (minutes * 60f));
                        break;
                    case TimeSource.DeviceClock:
                        var now = DateTime.Now;
                        hour = now.Hour + now.Minute / 60f + now.Second / 3600f;
                        break;
                }
            }
            Apply(false);
        }

        void Apply(bool force)
        {
            if (data == null) return;
            var tier = EnvironmentQuality.Current;
            float now = Application.isPlaying ? Time.unscaledTime : 0f;
            if (!force && Application.isPlaying && now < _nextApply) return;
            if (!force && Mathf.Abs(hour - _lastAppliedHour) < 0.002f) return;
            _nextApply = now + EnvironmentQuality.UpdateInterval(tier);
            _lastAppliedHour = hour;

            var s = data.EvaluateTimeOfDay(hour);
            State = s;

            if (mainLight != null)
            {
                mainLight.transform.rotation = s.LightRotation;
                mainLight.color = s.lightColor;
                mainLight.intensity = s.lightIntensity;
                bool shadows = s.lightIntensity > 0.04f && (!s.isMoon || EnvironmentQuality.NightShadows(tier));
                mainLight.shadows = shadows ? LightShadows.Soft : LightShadows.None;
                mainLight.shadowStrength = s.shadowStrength;
            }

            Color sky = s.ambientSky * ambientScale, eq = s.ambientEquator * ambientScale, gr = s.ambientGround * ambientScale;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = eq;
            RenderSettings.ambientGroundColor = gr;
            RenderSettings.ambientProbe = TrilightProbe(sky, eq, gr);
            RenderSettings.fogColor = s.fogColor;
            if (targetCamera != null) targetCamera.backgroundColor = s.fogColor;

            Shader.SetGlobalVector(LivingId, new Vector4(s.nightGlow, s.ambientFloorCut, 0f, 0f));

            if (!_hasPhase || s.phase != _phase)
            {
                var old = _phase;
                bool had = _hasPhase;
                _phase = s.phase;
                _hasPhase = true;
                if (had && Application.isPlaying) PhaseChanged?.Invoke(old, s.phase);
            }
        }

        /// <summary>
        /// Spherical-harmonics probe for a trilight gradient: up = sky, horizon = equator,
        /// down = ground. Setting the probe directly makes the change visible immediately
        /// on mobile (no environment re-bake).
        /// </summary>
        public static SphericalHarmonicsL2 TrilightProbe(Color sky, Color equator, Color ground)
        {
            var sh = new SphericalHarmonicsL2();
            sh.AddAmbientLight(equator);
            Color up = sky - equator, down = ground - equator;
            // A directional term of colour c along +-Y adds ~c to that pole.
            sh.AddDirectionalLight(Vector3.up, up * 0.85f, 1f);
            sh.AddDirectionalLight(Vector3.down, down * 0.85f, 1f);
            return sh;
        }
    }
}
