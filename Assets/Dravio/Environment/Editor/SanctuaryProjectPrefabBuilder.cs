using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Dravio.EnvironmentArt.EditorTools
{
    /// <summary>
    /// CHAR.8 — builds prefabs for the Sanctuary project style samples written by
    /// scripts/art/environment/build_sanctuary_projects.py (--unity Assets/Dravio/Environment/Sanctuary).
    /// For each Data/*.sanctuary.json: model Models/SM_SANC_&lt;id&gt;.fbx → Prefabs/PF_SANC_&lt;id&gt;.prefab with
    ///   * the zone's EXISTING painterly materials (M_ENV_Props, M_ENV_Ocean_Props, …; run the Environment
    ///     "Setup Materials" menus first) — no new material or texture;
    ///   * LODGroup with the screen heights from the JSON (meshes *_LOD0/1/2);
    ///   * a BoxCollider from the JSON bounds, static flags, SanctuaryProjectVisual with the ANCHOR_* children.
    /// Never overwrites an existing prefab (rename or delete it first). Never opens or saves a scene.
    /// </summary>
    public static class SanctuaryProjectPrefabBuilder
    {
        const string Root = "Assets/Dravio/Environment/Sanctuary";

        [Serializable] class Lod { public string mesh; public int triangles; public float screenRelativeHeight; }
        [Serializable] class Bounds { public float[] center; public float[] size; }
        [Serializable] class Meta
        {
            public string projectId, world, kind, prefabId, model, status;
            public string[] materials;
            public Lod[] lods;
            public Bounds boundsUnity;
            public float footprintRadius_m;
        }

        [MenuItem("Dravio/Environment/Sanctuary/Build Project Style-Sample Prefabs", priority = 160)]
        public static void BuildMenu()
        {
            var report = new StringBuilder();
            int made = Build(report);
            Debug.Log($"Dravio Sanctuary: {made} prefab(s) created.\n{report}");
        }

        static string MaterialDir(string world) => world == "origin"
            ? "Assets/Dravio/Environment/Materials"
            : $"Assets/Dravio/Environment/Zones/{char.ToUpperInvariant(world[0])}{world.Substring(1)}/Materials";

        public static int Build(StringBuilder report)
        {
            string dataDir = Root + "/Data";
            if (!AssetDatabase.IsValidFolder(dataDir)) { report.AppendLine("  no " + dataDir); return 0; }
            if (!AssetDatabase.IsValidFolder(Root + "/Prefabs")) AssetDatabase.CreateFolder(Root, "Prefabs");
            int made = 0;
            foreach (string file in Directory.GetFiles(dataDir, "*.sanctuary.json"))
            {
                var m = JsonUtility.FromJson<Meta>(File.ReadAllText(file));
                string prefabPath = $"{Root}/Prefabs/{m.prefabId}.prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null) { report.AppendLine($"  {m.prefabId}: exists, kept"); continue; }
                var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/{m.model}");
                if (model == null) { report.AppendLine($"  {m.prefabId}: model {m.model} missing"); continue; }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
                try
                {
                    go.name = m.prefabId;
                    int missing = 0;
                    foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    {
                        var mats = r.sharedMaterials;
                        for (int i = 0; i < mats.Length; i++)
                        {
                            string slot = mats[i] != null ? mats[i].name.Replace(" (Instance)", "") : (m.materials.Length > i ? m.materials[i] : "");
                            var env = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir(m.world)}/M_{slot}.mat");
                            if (env != null) mats[i] = env; else missing++;
                        }
                        r.sharedMaterials = mats;
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    }
                    if (missing > 0) report.AppendLine($"  {m.prefabId}: {missing} material slot(s) not found in {MaterialDir(m.world)} (run the Environment Setup Materials menu)");
                    var lodGroup = go.GetComponent<LODGroup>() ?? go.AddComponent<LODGroup>();
                    var lods = new LOD[m.lods.Length];
                    for (int i = 0; i < m.lods.Length; i++)
                    {
                        var t = FindDeep(go.transform, m.lods[i].mesh);
                        var rs = t != null ? t.GetComponents<Renderer>() : Array.Empty<Renderer>();
                        lods[i] = new LOD(m.lods[i].screenRelativeHeight, rs);
                    }
                    lodGroup.SetLODs(lods);
                    lodGroup.RecalculateBounds();
                    var box = go.AddComponent<BoxCollider>();
                    box.center = new Vector3(m.boundsUnity.center[0], m.boundsUnity.center[1], m.boundsUnity.center[2]);
                    box.size = new Vector3(m.boundsUnity.size[0], m.boundsUnity.size[1], m.boundsUnity.size[2]);
                    var vis = go.AddComponent<SanctuaryProjectVisual>();
                    vis.projectId = m.projectId; vis.world = m.world; vis.kind = m.kind; vis.footprintRadius = m.footprintRadius_m;
                    vis.status = string.IsNullOrEmpty(m.status) ? "style-sample" : m.status;
                    vis.CollectAnchors();
                    foreach (var t in go.GetComponentsInChildren<Transform>(true))
                        if (!t.name.StartsWith("ANCHOR_", StringComparison.Ordinal))
                            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
                    PrefabUtility.SaveAsPrefabAsset(go, prefabPath, out bool ok);
                    report.AppendLine($"  {m.prefabId}: {(ok ? "created" : "FAILED")} ({vis.Anchors.Length} anchors, LOD {string.Join("/", Array.ConvertAll(m.lods, l => l.triangles))} tris)");
                    if (ok) made++;
                }
                finally { UnityEngine.Object.DestroyImmediate(go); }
            }
            AssetDatabase.SaveAssets();
            return made;
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var f = FindDeep(c, name);
                if (f != null) return f;
            }
            return null;
        }
    }
}
