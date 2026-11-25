using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dravio.EnvironmentArt
{
    public enum DayPhase { Dawn, Day, Dusk, Night }

    /// <summary>
    /// Living environment data for the Origin Sanctuary (U1-ENV.3), generated from
    /// Data/OriginSanctuaryLiving.json (scripts/art/environment/living.py): day phases,
    /// time-of-day lighting keyframes, dragon interaction points, particle volumes and
    /// ambience emitters. Cosmetic only. It sits beside <see cref="SanctuaryLocationData"/>
    /// and never changes it; habitat IDs are the same strings.
    /// </summary>
    [CreateAssetMenu(menuName = "Dravio/Environment/Sanctuary Living Data", fileName = "OriginSanctuaryLiving")]
    public sealed class SanctuaryLivingData : ScriptableObject
    {
        [Serializable]
        public sealed class Phase
        {
            public string id;
            public float start;
            public float end;
        }

        [Serializable]
        public sealed class Keyframe
        {
            public float hour;
            public bool isMoon;
            public float lightElevation, lightAzimuth;
            public float[] lightColor;
            public float lightIntensity, shadowStrength;
            public float[] ambientSky, ambientEquator, ambientGround, fogColor;
            public float nightGlow, ambientFloorCut, wind;
            public float fireflies, dust, pondRings;
            public float audioBirds, audioCrickets, audioFrogs, audioInsects, audioWind, audioPond;
        }

        [Serializable]
        public sealed class InteractionPoint
        {
            public string id;
            public string habitatId;
            public string kind;
            public float x, y, z, yaw;
            public float targetX, targetY, targetZ;
            public float radius = 0.6f;
            public int capacity = 1;
            public string[] preferredPhases;
            public bool requiresSwim;
            public string animationHint;
            public string feedback;
            public string sfx;

            /// <summary>Where the dragon stands.</summary>
            public Vector3 Position => new Vector3(x, y, z);
            /// <summary>Facing towards <see cref="Target"/>.</summary>
            public Quaternion Rotation => Quaternion.Euler(0f, yaw, 0f);
            /// <summary>What the dragon looks at or touches.</summary>
            public Vector3 Target => new Vector3(targetX, targetY, targetZ);

            public bool Prefers(DayPhase phase)
            {
                if (preferredPhases == null || preferredPhases.Length == 0) return true;
                string id = PhaseId(phase);
                foreach (var p in preferredPhases)
                    if (p == id) return true;
                return false;
            }
        }

        [Serializable]
        public sealed class Pond
        {
            public float[] center;
            public float[] radii;
            public float[] swimRadii;
            public Vector3 Center => center != null && center.Length >= 3 ? new Vector3(center[0], center[1], center[2]) : Vector3.zero;
        }

        [Serializable]
        public sealed class ParticleVolume
        {
            public string id;
            public float[] center;
            public float[] size;
            public int maxParticles;
            public float[] lifetime;
            public Vector3 Center => V(center);
            public Vector3 Size => V(size);
        }

        [Serializable]
        public sealed class AudioLoop
        {
            public string id;
            public string clip;
            public string weightKey;
            public string emitterId;
        }

        [Serializable]
        public sealed class AudioEmitter
        {
            public string id;
            public float x, y, z, innerRadius, outerRadius;
            public Vector3 Position => new Vector3(x, y, z);
        }

        /// <summary>Evaluated time-of-day state (no allocations).</summary>
        public struct State
        {
            public float hour;
            public DayPhase phase;
            public bool isMoon;
            public float lightElevation, lightAzimuth, lightIntensity, shadowStrength;
            public Color lightColor, ambientSky, ambientEquator, ambientGround, fogColor;
            public float nightGlow, ambientFloorCut, wind, fireflies, dust, pondRings;
            public float audioBirds, audioCrickets, audioFrogs, audioInsects, audioWind, audioPond;

            public Quaternion LightRotation => Quaternion.Euler(lightElevation, lightAzimuth, 0f);

            public float AudioWeight(string key)
            {
                switch (key)
                {
                    case "audioBirds": return audioBirds;
                    case "audioCrickets": return audioCrickets;
                    case "audioFrogs": return audioFrogs;
                    case "audioInsects": return audioInsects;
                    case "audioWind": return audioWind;
                    case "audioPond": return audioPond;
                    default: return 0f;
                }
            }
        }

        public int version;
        public string zoneId;
        public int layoutVersion;
        public float defaultHour = 13f;
        public float dayLengthMinutes = 24f;
        public Phase[] phases = Array.Empty<Phase>();
        public Keyframe[] keyframes = Array.Empty<Keyframe>();
        public InteractionPoint[] interactionPoints = Array.Empty<InteractionPoint>();
        public Pond pond = new Pond();
        public ParticleVolume[] particleVolumes = Array.Empty<ParticleVolume>();
        public AudioLoop[] audioLoops = Array.Empty<AudioLoop>();
        public AudioEmitter[] audioEmitters = Array.Empty<AudioEmitter>();
        public string[] oneShots = Array.Empty<string>();

        public void LoadFromJson(string json) => JsonUtility.FromJsonOverwrite(json, this);

        // --------------------------------------------------------- time of day
        public static string PhaseId(DayPhase p)
        {
            switch (p)
            {
                case DayPhase.Dawn: return "dawn";
                case DayPhase.Dusk: return "dusk";
                case DayPhase.Night: return "night";
                default: return "day";
            }
        }

        static DayPhase ParsePhase(string id)
        {
            switch (id)
            {
                case "dawn": return DayPhase.Dawn;
                case "dusk": return DayPhase.Dusk;
                case "night": return DayPhase.Night;
                default: return DayPhase.Day;
            }
        }

        public static float WrapHour(float h)
        {
            h %= 24f;
            return h < 0f ? h + 24f : h;
        }

        public DayPhase PhaseAt(float hour)
        {
            float h = WrapHour(hour);
            foreach (var p in phases)
            {
                bool inside = p.start <= p.end ? (h >= p.start && h < p.end) : (h >= p.start || h < p.end);
                if (inside) return ParsePhase(p.id);
            }
            return DayPhase.Day;
        }

        /// <summary>Piecewise-linear keyframe blend that wraps at 24 h. Mirrors living.evaluate().</summary>
        public State EvaluateTimeOfDay(float hour)
        {
            var s = new State { hour = WrapHour(hour) };
            s.phase = PhaseAt(s.hour);
            int n = keyframes.Length;
            if (n == 0)
            {
                s.lightColor = Color.white;
                s.lightIntensity = 1f;
                s.lightElevation = 46f;
                s.lightAzimuth = -38f;
                return s;
            }
            int i = 0;
            while (i < n && keyframes[i].hour <= s.hour) i++;
            var a = keyframes[(i - 1 + n) % n];
            var b = keyframes[i % n];
            float span = WrapHour(b.hour - a.hour);
            if (span <= 0f) span = 24f;
            float t = WrapHour(s.hour - a.hour) / span;

            s.isMoon = t < 0.5f ? a.isMoon : b.isMoon;
            s.lightElevation = Mathf.Lerp(a.lightElevation, b.lightElevation, t);
            s.lightAzimuth = Mathf.Lerp(a.lightAzimuth, b.lightAzimuth, t);
            s.lightIntensity = Mathf.Lerp(a.lightIntensity, b.lightIntensity, t);
            s.shadowStrength = Mathf.Lerp(a.shadowStrength, b.shadowStrength, t);
            s.nightGlow = Mathf.Lerp(a.nightGlow, b.nightGlow, t);
            s.ambientFloorCut = Mathf.Lerp(a.ambientFloorCut, b.ambientFloorCut, t);
            s.wind = Mathf.Lerp(a.wind, b.wind, t);
            s.fireflies = Mathf.Lerp(a.fireflies, b.fireflies, t);
            s.dust = Mathf.Lerp(a.dust, b.dust, t);
            s.pondRings = Mathf.Lerp(a.pondRings, b.pondRings, t);
            s.audioBirds = Mathf.Lerp(a.audioBirds, b.audioBirds, t);
            s.audioCrickets = Mathf.Lerp(a.audioCrickets, b.audioCrickets, t);
            s.audioFrogs = Mathf.Lerp(a.audioFrogs, b.audioFrogs, t);
            s.audioInsects = Mathf.Lerp(a.audioInsects, b.audioInsects, t);
            s.audioWind = Mathf.Lerp(a.audioWind, b.audioWind, t);
            s.audioPond = Mathf.Lerp(a.audioPond, b.audioPond, t);
            s.lightColor = Color.Lerp(C(a.lightColor), C(b.lightColor), t);
            s.ambientSky = Color.Lerp(C(a.ambientSky), C(b.ambientSky), t);
            s.ambientEquator = Color.Lerp(C(a.ambientEquator), C(b.ambientEquator), t);
            s.ambientGround = Color.Lerp(C(a.ambientGround), C(b.ambientGround), t);
            s.fogColor = Color.Lerp(C(a.fogColor), C(b.fogColor), t);
            return s;
        }

        // ------------------------------------------------------- interactions
        public InteractionPoint FindInteraction(string id)
        {
            foreach (var p in interactionPoints)
                if (p.id == id) return p;
            return null;
        }

        public IEnumerable<InteractionPoint> InteractionsIn(string habitatId)
        {
            foreach (var p in interactionPoints)
                if (p.habitatId == habitatId) yield return p;
        }

        public IEnumerable<InteractionPoint> InteractionsOfKind(string kind)
        {
            foreach (var p in interactionPoints)
                if (p.kind == kind) yield return p;
        }

        public ParticleVolume FindVolume(string id)
        {
            foreach (var v in particleVolumes)
                if (v.id == id) return v;
            return null;
        }

        public AudioEmitter FindEmitter(string id)
        {
            foreach (var e in audioEmitters)
                if (e.id == id) return e;
            return null;
        }

        static Color C(float[] c) => c != null && c.Length >= 3 ? new Color(c[0], c[1], c[2], 1f) : Color.white;
        static Vector3 V(float[] a) => a != null && a.Length >= 3 ? new Vector3(a[0], a[1], a[2]) : Vector3.zero;
    }
}
