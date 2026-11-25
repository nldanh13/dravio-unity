using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dravio.EnvironmentArt
{
    /// <summary>
    /// Runtime registry of the dragon interaction points (drink, splash, bask, sniff
    /// flowers, inspect runes, stargaze...). Living World decides what a dragon does;
    /// this class only answers "which spot", keeps a spot from being used by two dragons
    /// at once, and plays the cosmetic response (particles, ripple, sound) when told the
    /// action happened. Occupants are plain ints (any id the caller likes), so there is
    /// no dependency on the character or domain code, and nothing is saved.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnvironmentInteractions : MonoBehaviour
    {
        public SanctuaryLivingData data;
        public EnvironmentTimeOfDay timeOfDay;
        [Header("Cosmetic feedback (optional)")]
        public EnvironmentAmbientParticles particles;
        public EnvironmentWater water;
        public EnvironmentAudio audioMixer;

        public event Action<SanctuaryLivingData.InteractionPoint, int> Reserved;
        public event Action<SanctuaryLivingData.InteractionPoint, int> Released;
        public event Action<SanctuaryLivingData.InteractionPoint, int> Used;

        readonly Dictionary<string, List<int>> _occupants = new Dictionary<string, List<int>>();

        public static EnvironmentInteractions Active { get; private set; }

        void OnEnable() => Active = this;

        void OnDisable()
        {
            if (Active == this) Active = null;
        }

        public IEnumerable<SanctuaryLivingData.InteractionPoint> All =>
            data != null ? data.interactionPoints : Array.Empty<SanctuaryLivingData.InteractionPoint>();

        public SanctuaryLivingData.InteractionPoint Find(string pointId) => data != null ? data.FindInteraction(pointId) : null;

        public bool IsFree(string pointId)
        {
            var p = Find(pointId);
            if (p == null) return false;
            return !_occupants.TryGetValue(pointId, out var list) || list.Count < Mathf.Max(1, p.capacity);
        }

        /// <summary>
        /// Closest point, optionally filtered by kind and habitat. Free points only unless
        /// <paramref name="includeOccupied"/>. Points that prefer the current day phase win
        /// ties within <paramref name="phaseBonusMetres"/>. Swimming points are skipped
        /// unless <paramref name="canSwim"/>.
        /// </summary>
        public SanctuaryLivingData.InteractionPoint FindNearest(Vector3 from, string kind = null, string habitatId = null,
                                                                 bool canSwim = false, bool includeOccupied = false,
                                                                 float phaseBonusMetres = 2f, bool canFly = true)
        {
            if (data == null) return null;
            DayPhase phase = timeOfDay != null ? timeOfDay.Phase : DayPhase.Day;
            SanctuaryLivingData.InteractionPoint best = null;
            float bestScore = float.MaxValue;
            foreach (var p in data.interactionPoints)
            {
                if (kind != null && p.kind != kind) continue;
                if (habitatId != null && p.habitatId != habitatId) continue;
                if (p.requiresSwim && !canSwim) continue;
                if (p.requiresFly && !canFly) continue;
                if (!includeOccupied && !IsFree(p.id)) continue;
                float score = Vector3.Distance(from, p.Position) - (p.Prefers(phase) ? phaseBonusMetres : 0f);
                if (score < bestScore) { bestScore = score; best = p; }
            }
            return best;
        }

        public bool TryReserve(string pointId, int occupant)
        {
            var p = Find(pointId);
            if (p == null || !IsFree(pointId)) return false;
            if (!_occupants.TryGetValue(pointId, out var list)) _occupants[pointId] = list = new List<int>(1);
            if (list.Contains(occupant)) return true;
            list.Add(occupant);
            Reserved?.Invoke(p, occupant);
            return true;
        }

        public void Release(string pointId, int occupant)
        {
            if (!_occupants.TryGetValue(pointId, out var list) || !list.Remove(occupant)) return;
            var p = Find(pointId);
            if (p != null) Released?.Invoke(p, occupant);
        }

        /// <summary>Frees every point held by an occupant (e.g. the dragon left the scene).</summary>
        public void ReleaseAll(int occupant)
        {
            foreach (var kv in _occupants)
                if (kv.Value.Remove(occupant))
                {
                    var p = Find(kv.Key);
                    if (p != null) Released?.Invoke(p, occupant);
                }
        }

        /// <summary>
        /// Call when the dragon performs the action (for example on the animation event).
        /// Plays the point's cosmetic feedback and raises <see cref="Used"/>.
        /// </summary>
        public void NotifyUsed(string pointId, int occupant)
        {
            var p = Find(pointId);
            if (p == null) return;
            PlayFeedback(p);
            Used?.Invoke(p, occupant);
        }

        public void PlayFeedback(SanctuaryLivingData.InteractionPoint p)
        {
            Vector3 at = p.Target;
            switch (p.feedback)
            {
                case "ripple_small":
                    if (water != null) water.AddRipple(at, 0.6f);
                    break;
                case "splash":
                    if (water != null) water.AddSplash(at, 1f);
                    if (particles != null) particles.Burst(EnvironmentAmbientParticles.BurstStyle.Splash, at, 12);
                    break;
                case "petal_puff":
                    if (particles != null) particles.Burst(EnvironmentAmbientParticles.BurstStyle.PetalPuff, at, 10);
                    break;
                case "rune_sparkle":
                    if (particles != null) particles.Burst(EnvironmentAmbientParticles.BurstStyle.RuneSparkle, at, 12);
                    break;
                case "heat_motes":
                    if (particles != null) particles.Burst(EnvironmentAmbientParticles.BurstStyle.HeatMotes, at, 8);
                    break;
                case "dust_puff":
                    if (particles != null) particles.Burst(EnvironmentAmbientParticles.BurstStyle.DustPuff, at, 8);
                    break;
                case "bubbles": // U1-ENV.4 Ocean
                    if (particles != null) particles.Burst(EnvironmentAmbientParticles.BurstStyle.Bubbles, at, 10);
                    break;
                case "cloud_puff": // U1-ENV.4 Sky
                    if (particles != null) particles.Burst(EnvironmentAmbientParticles.BurstStyle.CloudPuff, at, 6);
                    break;
                case "fireflies_gather":
                    if (particles != null)
                    {
                        // Only meaningful when it is dark enough to see fireflies.
                        int n = timeOfDay != null && timeOfDay.State.fireflies > 0.2f ? 8 : 3;
                        particles.Burst(EnvironmentAmbientParticles.BurstStyle.FirefliesGather, at, n);
                    }
                    break;
            }
            if (audioMixer != null && !string.IsNullOrEmpty(p.sfx)) audioMixer.PlayOneShot(p.sfx, at, 0.8f);
        }
    }
}
