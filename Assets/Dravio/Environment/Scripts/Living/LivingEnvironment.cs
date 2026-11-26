using UnityEngine;

namespace Dravio.EnvironmentArt
{
    /// <summary>
    /// Entry point for other systems: one object that holds the living environment parts.
    /// Living World, the camera or UI code can use <see cref="Active"/> instead of
    /// searching the scene. Each part also works on its own.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LivingEnvironment : MonoBehaviour
    {
        public SanctuaryLivingData data;
        public SanctuaryLocationData locations;
        public EnvironmentTimeOfDay timeOfDay;
        public EnvironmentWind wind;
        public EnvironmentWater water;
        public EnvironmentAmbientParticles particles;
        public EnvironmentAudio ambience;
        public EnvironmentInteractions interactions;

        public static LivingEnvironment Active { get; private set; }

        public DayPhase Phase => timeOfDay != null ? timeOfDay.Phase : DayPhase.Day;
        public float Hour => timeOfDay != null ? timeOfDay.hour : 13f;

        void OnEnable() => Active = this;

        void OnDisable()
        {
            if (Active == this) Active = null;
        }

        /// <summary>Moves the ambience focus (call with the active dragon's position).</summary>
        public void SetFocus(Vector3 world)
        {
            if (ambience != null) ambience.SetFocus(world);
        }
    }
}
