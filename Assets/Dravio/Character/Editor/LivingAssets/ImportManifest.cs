using System;
using System.IO;
using UnityEngine;

#pragma warning disable 0649 // filled by JsonUtility
namespace Dravio.Character.Editor
{
    /// <summary>import-manifest.json written by scripts/art/character/batch_import.py --unity-out.</summary>
    [Serializable]
    internal sealed class ImportManifest
    {
        internal const string DefaultPath = "Assets/Dravio/Character/Imported/import-manifest.json";

        [Serializable] internal sealed class Clip { public string name; public int start; public int end; public bool loop; public float groundSpeed; }
        [Serializable] internal sealed class AnimQa { public string status; public string[] warnings = new string[0]; }
        [Serializable] internal sealed class IkChain { public string upper, lower, foot; }
        [Serializable] internal sealed class Files { public string fbx; }
        [Serializable] internal sealed class Collider { public string type; public float[] boxCenter; public float[] boxSize; public string mesh; public int meshTriangles; }
        [Serializable] internal sealed class Size { public float x, y, z, height, footprintRadius; }

        [Serializable]
        internal sealed class Entry
        {
            public string kind; // "dragon" | "prop"
            public string name;
            public string validation;
            public Files files;
            public string unityFolder;
            // dragon
            public string species;
            public string stage;
            public string archetype;
            public bool rigged;
            public Clip[] clipList = new Clip[0];
            public int fps = 30;
            public IkChain[] ikChains = new IkChain[0];
            public string[] anchors = new string[0];
            public int rigVersion;
            /// <summary>Listed in approved-assets.json: files and prefab are kept exactly as approved.</summary>
            public bool approved;
            public AnimQa animQa;
            // prop
            public string propId;
            public string category;
            public string profile;
            public Collider collider;
            public Size size;
            public bool sizeConfirmed;
        }

        public string pipelineVersion;
        public Entry[] entries = new Entry[0];

        internal static ImportManifest Load(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Import manifest not found (run batch_import.py --unity-out first)", path);
            var m = JsonUtility.FromJson<ImportManifest>(File.ReadAllText(path));
            if (m == null || m.entries == null) throw new InvalidDataException("Invalid import manifest: " + path);
            return m;
        }

        internal static string Folder(string manifestPath)
        {
            return Path.GetDirectoryName(manifestPath).Replace('\\', '/');
        }
    }
}
