using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Dravio.Character.Editor
{
    /// <summary>
    /// Static environment prop prefab: LODGroup from *_LOD# renderers (prop LOD profile heights), one simple
    /// collider (Box from LOD0 bounds, convex MeshCollider or static MeshCollider from the exported
    /// *_Collider mesh, whose renderer is removed), static editor flags, EnvironmentPropInfo with size metadata.
    /// No Animator, no rig. Never overwrites an existing prefab.
    /// </summary>
    internal static class PropPrefabBuilder
    {
        internal const string PrefabFolder = "Assets/Dravio/Character/Prefabs/Environment";
        private static readonly Regex LodRegex = new Regex(@"_LOD(\d+)$", RegexOptions.IgnoreCase);

        internal static GameObject Build(string modelPath, Material material, ImportManifest.Entry e, out string message)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null) { message = "Model not imported: " + modelPath; return null; }
            string prefabPath = PrefabFolder + "/" + e.name + ".prefab";
            if (File.Exists(prefabPath)) { message = "exists, kept unchanged: " + prefabPath; return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath); }
            var audit = CharacterModelValidator.Validate(modelPath, e.profile);
            if (audit.Errors.Count > 0) { message = "audit failed:\n" + CharacterModelValidator.Format(modelPath, audit); return null; }
            CharacterBudgets budgets = CharacterBudgets.Load(e.profile);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                foreach (var a in instance.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a);
                var byLevel = new SortedDictionary<int, List<Renderer>>();
                GameObject colliderGo = null;
                foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.gameObject.name.EndsWith("_Collider")) { colliderGo = r.gameObject; continue; }
                    var m = LodRegex.Match(r.gameObject.name);
                    if (!m.Success) continue;
                    int level = int.Parse(m.Groups[1].Value);
                    List<Renderer> list;
                    if (!byLevel.TryGetValue(level, out list)) byLevel[level] = list = new List<Renderer>();
                    list.Add(r);
                    if (material != null) r.sharedMaterials = Enumerable.Repeat(material, r.sharedMaterials.Length).ToArray();
                }
                if (byLevel.Count == 0) { message = "No _LOD# renderers in " + modelPath; return null; }
                var lods = new List<LOD>();
                float prev = 1f;
                foreach (var kv in byLevel)
                {
                    var cfg = budgets.ForLevel(kv.Key);
                    float h = cfg != null ? cfg.screenRelativeTransitionHeight : prev * 0.4f;
                    if (h >= prev) h = prev * 0.5f;
                    lods.Add(new LOD(h, kv.Value.ToArray()));
                    prev = h;
                }
                var group = instance.GetComponent<LODGroup>();
                if (group == null) group = instance.AddComponent<LODGroup>(); // no ?? on UnityEngine.Object
                group.fadeMode = LODFadeMode.None;
                group.SetLODs(lods.ToArray());
                group.RecalculateBounds();
                string colliderType = e.collider != null ? e.collider.type : "box";
                string colliderNote = AddCollider(instance, byLevel.First().Value, colliderGo, colliderType);
                var info = instance.GetComponent<EnvironmentPropInfo>();
                if (info == null) info = instance.AddComponent<EnvironmentPropInfo>();
                info.propId = e.propId; info.category = e.category; info.lodProfile = e.profile; info.colliderType = colliderType;
                if (e.size != null)
                {
                    info.height = e.size.height; info.footprintRadius = e.size.footprintRadius;
                    info.size = new Vector3(e.size.x, e.size.z, e.size.y); // Blender Z-up -> Unity Y-up
                }
                info.sizeConfirmed = e.sizeConfirmed;
                const StaticEditorFlags flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic |
                                                StaticEditorFlags.OccluderStatic | StaticEditorFlags.ContributeGI |
                                                StaticEditorFlags.ReflectionProbeStatic;
                foreach (var t in instance.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
                Directory.CreateDirectory(PrefabFolder);
                bool ok;
                var prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath, out ok);
                message = ok ? "created " + prefabPath + " (" + lods.Count + " LODs, " + colliderNote + ")" : "saving failed: " + prefabPath;
                return ok ? prefab : null;
            }
            finally { Object.DestroyImmediate(instance); }
        }

        private static string AddCollider(GameObject root, List<Renderer> lod0, GameObject colliderGo, string type)
        {
            if (type == "convex" || type == "mesh")
            {
                var mf = colliderGo != null ? colliderGo.GetComponent<MeshFilter>() : null;
                if (mf != null && mf.sharedMesh != null)
                {
                    var mesh = mf.sharedMesh;
                    var mr = colliderGo.GetComponent<MeshRenderer>();
                    if (mr != null) Object.DestroyImmediate(mr);
                    Object.DestroyImmediate(mf);
                    var mc = colliderGo.AddComponent<MeshCollider>();
                    mc.sharedMesh = mesh;
                    mc.convex = type == "convex";
                    return (mc.convex ? "convex" : "static") + " MeshCollider " + mesh.triangles.Length / 3 + " tris";
                }
                type = "box"; // no collider mesh exported: fall back
            }
            if (colliderGo != null) colliderGo.SetActive(false); // keep the hierarchy (prefab instance); never rendered
            var b = new Bounds();
            bool any = false;
            foreach (var r in lod0)
            {
                var f = r.GetComponent<MeshFilter>();
                if (f == null || f.sharedMesh == null) continue;
                var mb = f.sharedMesh.bounds;
                for (int i = 0; i < 8; i++) // corners into root space (handles the importer's axis rotation)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = root.transform.InverseTransformPoint(r.transform.TransformPoint(corner));
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                }
            }
            var box = root.AddComponent<BoxCollider>();
            box.center = b.center;
            box.size = b.size;
            return "BoxCollider " + b.size.ToString("0.00");
        }

    }
}
