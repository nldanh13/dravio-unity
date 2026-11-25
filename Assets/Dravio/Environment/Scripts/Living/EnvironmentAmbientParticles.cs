using UnityEngine;

namespace Dravio.EnvironmentArt
{
    /// <summary>
    /// Fireflies, light dust and pond rings driven by the time of day, plus two small
    /// burst systems for interaction feedback. The particle systems themselves are built
    /// by the editor tool (Dravio > Environment > 7); this component only sets rates
    /// and counts. Budget at High: 36 + 14 fireflies, 48 dust motes, 8 rings, bursts of
    /// up to 16. Medium and Low scale counts by 0.65 and 0.35.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnvironmentAmbientParticles : MonoBehaviour
    {
        public enum BurstStyle { PetalPuff, RuneSparkle, HeatMotes, FirefliesGather, Splash, DustPuff }

        public EnvironmentTimeOfDay timeOfDay;
        public EnvironmentWind wind;

        [Header("Ambient")]
        public ParticleSystem fireflies;
        public ParticleSystem firefliesPond;
        public ParticleSystem dust;
        public ParticleSystem pondRings;
        [Header("Feedback bursts")]
        [Tooltip("Light particles that float up (sparkles, petals, heat motes, fireflies).")]
        public ParticleSystem burstRise;
        [Tooltip("Particles with gravity (water droplets, dust).")]
        public ParticleSystem burstFall;

        [Header("High tier maximums")]
        public int firefliesMax = 36;
        public int firefliesPondMax = 14;
        public int dustMax = 48;
        public int pondRingsMax = 8;

        float _next;
        EnvironmentQuality.Tier _tier;

        void OnEnable()
        {
            _tier = EnvironmentQuality.Current;
            EnvironmentQuality.Changed += OnTier;
            ApplyLimits();
        }

        void OnDisable() => EnvironmentQuality.Changed -= OnTier;

        void OnTier(EnvironmentQuality.Tier t)
        {
            _tier = t;
            ApplyLimits();
        }

        void ApplyLimits()
        {
            float k = EnvironmentQuality.ParticleScale(_tier);
            SetMax(fireflies, firefliesMax, k);
            SetMax(firefliesPond, firefliesPondMax, k);
            SetMax(dust, dustMax, k);
            SetMax(pondRings, pondRingsMax, k);
        }

        static void SetMax(ParticleSystem ps, int max, float k)
        {
            if (ps == null) return;
            var main = ps.main;
            main.maxParticles = Mathf.Max(1, Mathf.CeilToInt(max * k));
        }

        void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + EnvironmentQuality.UpdateInterval(_tier);
            if (timeOfDay == null) return;
            var s = timeOfDay.State;
            float k = EnvironmentQuality.ParticleScale(_tier);
            Drive(fireflies, firefliesMax * k, s.fireflies);
            Drive(firefliesPond, firefliesPondMax * k, s.fireflies);
            Drive(dust, dustMax * k, s.dust);
            Drive(pondRings, pondRingsMax * k, s.pondRings);

            if (dust != null && wind != null)
            {
                var vel = dust.velocityOverLifetime;
                var d = wind.Direction * (0.08f + wind.Gust * 0.45f);
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.World;
                vel.x = new ParticleSystem.MinMaxCurve(d.x);
                vel.y = new ParticleSystem.MinMaxCurve(0.02f);
                vel.z = new ParticleSystem.MinMaxCurve(d.y);
            }
        }

        /// <summary>Keeps roughly max * weight particles alive: rate = count / mean lifetime.</summary>
        static void Drive(ParticleSystem ps, float max, float weight)
        {
            if (ps == null) return;
            var main = ps.main;
            var em = ps.emission;
            float life = Mathf.Max(0.5f, (main.startLifetime.constantMin + main.startLifetime.constantMax) * 0.5f);
            float target = max * Mathf.Clamp01(weight);
            em.rateOverTime = target / life;
            if (target < 0.5f)
            {
                if (ps.isEmitting) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
            else if (!ps.isPlaying || !ps.isEmitting)
            {
                ps.Play(false);
            }
        }

        /// <summary>Small cosmetic burst at a world position (interaction feedback).</summary>
        public void Burst(BurstStyle style, Vector3 at, int count = 10)
        {
            bool fall = style == BurstStyle.Splash || style == BurstStyle.DustPuff;
            var ps = fall ? burstFall : burstRise;
            if (ps == null) return;
            count = Mathf.Clamp(Mathf.CeilToInt(count * Mathf.Max(0.5f, EnvironmentQuality.ParticleScale(_tier))), 1, 16);
            var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                var r = Random.insideUnitSphere;
                Color c;
                float size, life;
                Vector3 v;
                switch (style)
                {
                    case BurstStyle.PetalPuff:
                        c = i % 3 == 0 ? new Color(1f, 0.85f, 0.4f) : new Color(1f, 0.62f, 0.78f);
                        size = Random.Range(0.06f, 0.11f); life = Random.Range(1.2f, 2f);
                        v = new Vector3(r.x * 0.6f, 0.5f + Mathf.Abs(r.y) * 0.5f, r.z * 0.6f);
                        break;
                    case BurstStyle.RuneSparkle:
                        c = new Color(0.55f, 0.95f, 1f) * 1.6f;
                        size = Random.Range(0.05f, 0.09f); life = Random.Range(0.8f, 1.5f);
                        v = new Vector3(r.x * 0.35f, 0.7f + Mathf.Abs(r.y) * 0.6f, r.z * 0.35f);
                        break;
                    case BurstStyle.HeatMotes:
                        c = new Color(1f, 0.6f, 0.25f) * 1.3f;
                        size = Random.Range(0.04f, 0.07f); life = Random.Range(1.5f, 2.5f);
                        v = new Vector3(r.x * 0.2f, 0.35f + Mathf.Abs(r.y) * 0.3f, r.z * 0.2f);
                        break;
                    case BurstStyle.FirefliesGather:
                        c = new Color(0.95f, 1f, 0.45f) * 1.5f;
                        size = Random.Range(0.06f, 0.09f); life = Random.Range(2.5f, 4f);
                        v = r * 0.25f + Vector3.up * 0.1f;
                        break;
                    case BurstStyle.Splash:
                        c = new Color(0.85f, 0.95f, 1f, 0.9f);
                        size = Random.Range(0.05f, 0.09f); life = Random.Range(0.5f, 0.8f);
                        v = new Vector3(r.x * 1.1f, 1.6f + Mathf.Abs(r.y) * 1.2f, r.z * 1.1f);
                        break;
                    default: // DustPuff
                        c = new Color(0.72f, 0.58f, 0.42f, 0.7f);
                        size = Random.Range(0.12f, 0.2f); life = Random.Range(0.7f, 1.1f);
                        v = new Vector3(r.x * 0.7f, 0.5f + Mathf.Abs(r.y) * 0.4f, r.z * 0.7f);
                        break;
                }
                p.position = at + r * 0.15f;
                p.velocity = v;
                p.startColor = c;
                p.startSize = size;
                p.startLifetime = life;
                ps.Emit(p, 1);
            }
        }
    }
}
