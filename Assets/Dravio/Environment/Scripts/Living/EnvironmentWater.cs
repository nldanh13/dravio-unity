using UnityEngine;

namespace Dravio.EnvironmentArt
{
    /// <summary>
    /// Pond surface effects: up to four expanding ripple rings computed in the Painterly
    /// Water shader (no extra meshes or render textures). Call <see cref="AddRipple"/>
    /// when something touches the water; the interaction feedback does this for drink
    /// and splash points. The small ambient rings come from the pond ring
    /// particles instead.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnvironmentWater : MonoBehaviour
    {
        const int MaxRipples = 4;

        [Tooltip("Ring speed in metres per second.")]
        public float ringSpeed = 0.9f;
        [Tooltip("Ring thickness in metres.")]
        public float ringWidth = 0.12f;
        [Tooltip("Seconds a ring lives.")]
        public float ringLifetime = 2.2f;

        static readonly int RipplesId = Shader.PropertyToID("_DravioRipples");
        static readonly int ParamsId = Shader.PropertyToID("_DravioRippleParams");

        readonly Vector4[] _ripples = new Vector4[MaxRipples];
        int _next;
        float _lastActive = -100f;

        public static EnvironmentWater Active { get; private set; }

        void OnEnable()
        {
            Active = this;
            for (int i = 0; i < MaxRipples; i++) _ripples[i] = Vector4.zero;
            Upload();
        }

        void OnDisable()
        {
            if (Active == this) Active = null;
            for (int i = 0; i < MaxRipples; i++) _ripples[i] = Vector4.zero;
            Upload();
        }

        /// <summary>Starts a ring at a world position (strength 0..1). Oldest ring is reused.</summary>
        public void AddRipple(Vector3 world, float strength = 1f)
        {
            float now = Time.timeSinceLevelLoad; // same clock as _Time.y in shaders
            _ripples[_next] = new Vector4(world.x, world.z, now, Mathf.Clamp01(strength));
            _next = (_next + 1) % MaxRipples;
            _lastActive = now;
            Upload();
        }

        /// <summary>Several rings in a quick burst (splash).</summary>
        public void AddSplash(Vector3 world, float strength = 1f)
        {
            AddRipple(world, strength);
            AddRipple(world + new Vector3(0.12f, 0f, -0.08f), strength * 0.7f);
        }

        void Update()
        {
            // Rings expire in the shader; clear the data once all are old so the
            // shader loop exits early.
            if (_lastActive > 0f && Time.timeSinceLevelLoad - _lastActive > ringLifetime + 0.1f)
            {
                for (int i = 0; i < MaxRipples; i++) _ripples[i] = Vector4.zero;
                _lastActive = -100f;
                Upload();
            }
        }

        void Upload()
        {
            Shader.SetGlobalVectorArray(RipplesId, _ripples);
            Shader.SetGlobalVector(ParamsId, new Vector4(ringSpeed, ringWidth, ringLifetime, 0f));
        }
    }
}
