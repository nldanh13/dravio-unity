using UnityEngine;

namespace Dravio.Character
{
    /// <summary>Size and collision metadata of a static environment prop (from scripts/art/character/build_prop.py).
    /// Static props never carry an Animator or a rig.</summary>
    [DisallowMultipleComponent]
    public sealed class EnvironmentPropInfo : MonoBehaviour
    {
        public string propId;
        public string category;
        public string lodProfile;
        public string colliderType;
        [Tooltip("Metres, as exported (pivot at bottom centre)")] public float height;
        public float footprintRadius;
        public Vector3 size;
        [Tooltip("True when the real-world size was set by art (asset-overrides.json); false = generator units")]
        public bool sizeConfirmed;
    }
}
