using System;
using System.IO;
using UnityEngine;

#pragma warning disable 0649 // fields are filled by JsonUtility
namespace Dravio.Character.Editor
{
    /// <summary>
    /// Mobile character budgets shared with scripts/art/character (Python).
    /// Source of truth: Assets/Dravio/Character/Data/character-budgets.json.
    /// </summary>
    [Serializable]
    internal sealed class CharacterBudgets
    {
        internal const string Path = "Assets/Dravio/Character/Data/character-budgets.json";

        [Serializable]
        internal sealed class Lod
        {
            public int level;
            public int minTriangles;
            public int maxTriangles;
            public int targetTriangles;
            public float screenRelativeTransitionHeight;
            public float maxP95DeviationRel;
            public float maxDeformedP95DeviationRel;
        }

        [Serializable] internal sealed class Skin { public int maxInfluences = 4; public int maxBones = 75; }
        [Serializable] internal sealed class Materials { public int maxSlotsPerLodWarn = 2; public int maxSlotsPerLodError = 4; }
        [Serializable] internal sealed class Textures { public int maxSize = 2048; public int recommendedSize = 1024; public bool requirePowerOfTwo = true; }

        /// <summary>Static environment prop LOD budgets (keys mirror character-budgets.json propProfiles).</summary>
        [Serializable] internal sealed class PropProfiles { public Lod[] prop_small = new Lod[0]; public Lod[] prop_large = new Lod[0]; }

        public int version;
        public PropProfiles propProfiles = new PropProfiles();
        public Lod[] lods = new Lod[0];
        public Skin skin = new Skin();
        public Materials materials = new Materials();
        public Textures textures = new Textures();

        internal Lod ForLevel(int level)
        {
            foreach (var l in lods) if (l.level == level) return l;
            return null;
        }

        /// <param name="lodProfile">null/"lods" = character budgets; "prop_small"/"prop_large" swaps in that prop profile.</param>
        internal static CharacterBudgets Load(string lodProfile = null)
        {
            if (!File.Exists(Path))
                throw new FileNotFoundException("Dravio character budgets missing", Path);
            var b = JsonUtility.FromJson<CharacterBudgets>(File.ReadAllText(Path));
            if (b != null && !string.IsNullOrEmpty(lodProfile) && lodProfile != "lods")
            {
                Lod[] p = lodProfile == "prop_small" ? b.propProfiles.prop_small : lodProfile == "prop_large" ? b.propProfiles.prop_large : null;
                if (p == null || p.Length == 0) throw new InvalidDataException("Unknown or empty LOD profile '" + lodProfile + "' in " + Path);
                b.lods = p;
            }
            if (b == null || b.lods == null || b.lods.Length == 0)
                throw new InvalidDataException("Invalid character budgets: " + Path);
            Array.Sort(b.lods, (x, y) => x.level.CompareTo(y.level));
            return b;
        }
    }
}
