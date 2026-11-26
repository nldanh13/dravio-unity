using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Dravio.EnvironmentArt
{
    /// <summary>
    /// Ambience for the sanctuary. Six seamless loops (day birds, night crickets, wind,
    /// pond water, night frogs, warm-rock insects) are mixed by the time of day; the pond
    /// and warm-rock loops also fade with distance from a focus point (the camera's
    /// free-area centre by default, or the active dragon via <see cref="SetFocus"/>).
    /// Interaction one-shots go through a small voice pool.
    ///
    /// The orthographic camera is far from the ground, so everything is 2D audio with a
    /// gentle stereo pan from the emitter's screen position instead of 3D roll-off.
    /// All clips are synthesised by scripts/art/environment/ambience_audio.py (original,
    /// no third-party material). Route <see cref="output"/> to the game's ambience mixer
    /// group so the main audio settings control it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnvironmentAudio : MonoBehaviour
    {
        [Serializable]
        public sealed class NamedClip
        {
            public string id;
            public AudioClip clip;
        }

        public SanctuaryLivingData data;
        public EnvironmentTimeOfDay timeOfDay;
        public EnvironmentWind wind;
        [Tooltip("Camera used for stereo pan and the default focus.")]
        public Camera viewCamera;
        public AudioMixerGroup output;

        [Range(0f, 1f)] public float masterVolume = 0.8f;
        [Range(0f, 1f)] public float loopsVolume = 0.7f;
        [Range(0f, 1f)] public float oneShotVolume = 0.9f;
        [Tooltip("Seconds to reach a new mix level.")]
        [Min(0.05f)] public float fadeSeconds = 2.5f;
        [Range(0f, 1f)] public float stereoWidth = 0.55f;

        [Tooltip("Loop clips by id (birds, crickets, wind, pond, frogs, insects).")]
        public NamedClip[] loopClips = Array.Empty<NamedClip>();
        [Tooltip("One-shot clips by sfx id (SFX_*).")]
        public NamedClip[] oneShotClips = Array.Empty<NamedClip>();

        sealed class LoopVoice
        {
            public SanctuaryLivingData.AudioLoop def;
            public SanctuaryLivingData.AudioEmitter emitter;
            public AudioSource source;
            public float level;
        }

        readonly List<LoopVoice> _loops = new List<LoopVoice>();
        readonly List<AudioSource> _oneShots = new List<AudioSource>();
        readonly Dictionary<string, AudioClip> _clipById = new Dictionary<string, AudioClip>();
        Vector3 _focus;
        bool _hasFocus;
        float _next;

        void OnEnable()
        {
            Build();
            EnvironmentQuality.Changed += OnTier;
        }

        void OnDisable()
        {
            EnvironmentQuality.Changed -= OnTier;
            foreach (var l in _loops) if (l.source != null) Destroy(l.source.gameObject);
            foreach (var s in _oneShots) if (s != null) Destroy(s.gameObject);
            _loops.Clear();
            _oneShots.Clear();
        }

        void OnTier(EnvironmentQuality.Tier t) => EnsureVoices(EnvironmentQuality.OneShotVoices(t));

        /// <summary>Where the "listener" is for pond / warm-rock ambience (e.g. the active dragon).</summary>
        public void SetFocus(Vector3 world)
        {
            _focus = world;
            _hasFocus = true;
        }

        public void ClearFocus() => _hasFocus = false;

        void Build()
        {
            _clipById.Clear();
            foreach (var c in oneShotClips) if (c != null && c.clip != null && !string.IsNullOrEmpty(c.id)) _clipById[c.id] = c.clip;
            if (data == null) return;
            var rng = new System.Random(7341);
            foreach (var def in data.audioLoops)
            {
                AudioClip clip = null;
                foreach (var c in loopClips) if (c != null && c.id == def.id) clip = c.clip;
                if (clip == null) continue;
                var src = NewSource("Loop " + def.id);
                src.clip = clip;
                src.loop = true;
                src.volume = 0f;
                src.priority = 64;
                src.Play();
                // Offset start so the loops never line up into an audible pattern.
                if (clip.samples > 0) src.timeSamples = rng.Next(0, clip.samples);
                _loops.Add(new LoopVoice
                {
                    def = def, source = src,
                    emitter = string.IsNullOrEmpty(def.emitterId) ? null : data.FindEmitter(def.emitterId),
                });
            }
            EnsureVoices(EnvironmentQuality.OneShotVoices(EnvironmentQuality.Current));
        }

        void EnsureVoices(int count)
        {
            while (_oneShots.Count < count)
            {
                var s = NewSource("One-shot " + _oneShots.Count);
                s.loop = false;
                s.priority = 96;
                _oneShots.Add(s);
            }
            while (_oneShots.Count > count)
            {
                var s = _oneShots[_oneShots.Count - 1];
                _oneShots.RemoveAt(_oneShots.Count - 1);
                if (s != null) Destroy(s.gameObject);
            }
        }

        AudioSource NewSource(string name)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.dopplerLevel = 0f;
            src.outputAudioMixerGroup = output;
            return src;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (Time.unscaledTime >= _next)
            {
                _next = Time.unscaledTime + EnvironmentQuality.UpdateInterval(EnvironmentQuality.Current);
                UpdateTargets();
            }
            float k = 1f - Mathf.Exp(-dt * 3f / fadeSeconds);
            foreach (var l in _loops)
            {
                if (l.source == null) continue;
                l.source.volume = Mathf.Lerp(l.source.volume, l.level, k);
            }
        }

        void UpdateTargets()
        {
            if (timeOfDay == null) return;
            var s = timeOfDay.State;
            Vector3 focus = _hasFocus ? _focus : DefaultFocus();
            float gust = wind != null ? wind.Gust : 0f;
            foreach (var l in _loops)
            {
                float w = s.AudioWeight(l.def.weightKey);
                if (l.def.id == "wind") w *= 0.55f + 0.9f * gust;
                if (l.emitter != null)
                {
                    float d = Vector2.Distance(new Vector2(focus.x, focus.z), new Vector2(l.emitter.x, l.emitter.z));
                    float near = Mathf.InverseLerp(l.emitter.outerRadius, l.emitter.innerRadius, d);
                    w *= Mathf.Lerp(0.15f, 1f, near);
                    if (l.source != null) l.source.panStereo = Pan(l.emitter.Position);
                }
                l.level = Mathf.Clamp01(w) * loopsVolume * masterVolume;
            }
        }

        Vector3 DefaultFocus()
        {
            if (viewCamera == null) return Vector3.zero;
            var fit = viewCamera.GetComponent<SanctuaryCameraFit>();
            Rect r = fit != null ? fit.FreeViewport : new Rect(0f, 0f, 1f, 1f);
            var ray = viewCamera.ViewportPointToRay(new Vector3(r.center.x, r.center.y, 0f));
            return new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float t) ? ray.GetPoint(t) : Vector3.zero;
        }

        float Pan(Vector3 world)
        {
            if (viewCamera == null) return 0f;
            float vx = viewCamera.WorldToViewportPoint(world).x;
            return Mathf.Clamp((vx - 0.5f) * 2f * stereoWidth, -1f, 1f);
        }

        /// <summary>Plays an SFX_* clip panned to a world position. Returns false if no voice or clip.</summary>
        public bool PlayOneShot(string sfxId, Vector3 world, float volume = 1f)
        {
            if (string.IsNullOrEmpty(sfxId) || !_clipById.TryGetValue(sfxId, out var clip)) return false;
            AudioSource free = null;
            foreach (var s in _oneShots)
                if (s != null && !s.isPlaying) { free = s; break; }
            if (free == null) return false; // budget reached: drop rather than cut a sound
            free.clip = clip;
            free.panStereo = Pan(world);
            free.volume = Mathf.Clamp01(volume) * oneShotVolume * masterVolume;
            free.pitch = UnityEngine.Random.Range(0.95f, 1.05f);
            free.Play();
            return true;
        }
    }
}
