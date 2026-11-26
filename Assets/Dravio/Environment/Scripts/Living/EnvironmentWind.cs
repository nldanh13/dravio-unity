using UnityEngine;

namespace Dravio.EnvironmentArt
{
    /// <summary>
    /// Global wind for the sanctuary: a calm base that follows the time of day plus
    /// soft gusts from smooth noise. Writes <c>_DravioWind</c> (read by the Painterly
    /// Lit and Water shaders) and exposes <see cref="Gust"/> for particles and audio.
    /// Cosmetic only: there are no storms or wind effects on gameplay.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class EnvironmentWind : MonoBehaviour
    {
        public EnvironmentTimeOfDay timeOfDay;
        [Tooltip("Direction the gusts blow towards (world XZ).")]
        public Vector2 direction = new Vector2(0.85f, 0.35f);
        [Range(0f, 2f)] public float strength = 1f;
        [Tooltip("How strong gusts get (0 = steady breeze).")]
        [Range(0f, 1f)] public float gustiness = 0.6f;
        [Tooltip("Average seconds between gust peaks.")]
        [Min(1f)] public float gustPeriod = 9f;

        static readonly int WindId = Shader.PropertyToID("_DravioWind");

        /// <summary>Current gust, 0..1.</summary>
        public float Gust { get; private set; }
        /// <summary>Base wind from the time of day times <see cref="strength"/>.</summary>
        public float Base { get; private set; } = 1f;
        public Vector2 Direction => direction.sqrMagnitude > 1e-4f ? direction.normalized : Vector2.right;

        void OnDisable() => Shader.SetGlobalVector(WindId, Vector4.zero);

        void Update()
        {
            float t = Application.isPlaying ? Time.time : 0f;
            float tod = timeOfDay != null && timeOfDay.data != null ? timeOfDay.State.wind : 1f;
            Base = tod * strength;
            float n = Mathf.PerlinNoise(t / gustPeriod, 0.37f) * 0.7f + Mathf.PerlinNoise(t / (gustPeriod * 0.31f), 4.1f) * 0.3f;
            Gust = Mathf.Clamp01(Mathf.SmoothStep(0.35f, 0.85f, n) * gustiness * Base);
            var d = Direction;
            Shader.SetGlobalVector(WindId, new Vector4(Base - 1f, Gust, d.x, d.y));
        }
    }
}
