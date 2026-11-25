using UnityEngine;

namespace Dravio.EnvironmentArt
{
    /// <summary>
    /// Designer-visible marker for a spawn point, habitat transition, perch or (U1-ENV.3)
    /// dragon interaction point in the
    /// environment scene. Pure data mirror of <see cref="SanctuaryLocationData"/>;
    /// it has no behaviour. Living World should read the data asset, not these objects.
    /// </summary>
    public sealed class SanctuaryMarker : MonoBehaviour
    {
        public enum Kind { Spawn, Transition, Perch, Interaction }

        public Kind kind;
        public string markerId;
        public string habitatId;
        [Tooltip("Second habitat for transitions.")]
        public string otherHabitatId;
    }
}
