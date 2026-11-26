using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Dravio.Character.Editor
{
    /// <summary>
    /// Builds a new prefab with a LODGroup from a model whose renderers are named *_LOD0/_LOD1/_LOD2
    /// (output of scripts/art/character/build_lods.py). Transition heights come from the shared budgets.
    /// Never overwrites an existing prefab and never edits the model asset, its importer or any scene.
    /// Optional: select a Material together with the model to assign it to every LOD renderer
    /// in the prefab (the FBX itself is left untouched).
    /// </summary>
    internal static class CharacterLodPrefabBuilder
    {
        internal const string PrefabFolder = "Assets/Dravio/Character/Prefabs";
        private static readonly Regex LodRegex = new Regex(@"_LOD(\d+)$", RegexOptions.IgnoreCase);

        [MenuItem("Dravio/Character/Create LOD Prefab From Selected Model")]
        private static void Menu()
        {
            var modelPath = Selection.objects.Select(AssetDatabase.GetAssetPath)
                .FirstOrDefault(p => AssetImporter.GetAtPath(p) is ModelImporter);
            if (modelPath == null) { Debug.LogWarning("Dravio LOD prefab: select an imported model with _LOD# renderers."); return; }
            var material = Selection.objects.OfType<Material>().FirstOrDefault();
            string result;
            var prefab = Build(modelPath, material, out result);
            if (prefab != null) { Debug.Log(result); EditorGUIUtility.PingObject(prefab); Selection.activeObject = prefab; }
            else Debug.LogError(result);
        }

        internal static GameObject Build(string modelPath, Material material, out string message)
        {
            var budgets = CharacterBudgets.Load();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null) { message = "Model not imported: " + modelPath; return null; }
            string prefabPath = PrefabFolder + "/" + model.name + "_LODGroup.prefab";
            if (File.Exists(prefabPath))
            {
                message = "Prefab already exists, not overwritten: " + prefabPath + ". Rename or delete it to rebuild.";
                return null;
            }
            var audit = CharacterModelValidator.Validate(modelPath);
            if (audit.Errors.Count > 0)
            {
                message = "Model failed the character audit; fix before building the LOD prefab.\n" + CharacterModelValidator.Format(modelPath, audit);
                return null;
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                var byLevel = new SortedDictionary<int, List<Renderer>>();
                foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
                {
                    var m = LodRegex.Match(r.gameObject.name);
                    if (!m.Success) continue;
                    int level = int.Parse(m.Groups[1].Value);
                    List<Renderer> list;
                    if (!byLevel.TryGetValue(level, out list)) byLevel[level] = list = new List<Renderer>();
                    list.Add(r);
                    if (material != null)
                        r.sharedMaterials = Enumerable.Repeat(material, r.sharedMaterials.Length).ToArray();
                }
                if (byLevel.Count < 2) { message = "Need at least two _LOD# renderer levels in " + modelPath; return null; }
                var lods = new List<LOD>();
                int index = 0;
                foreach (var kv in byLevel)
                {
                    var cfg = budgets.ForLevel(kv.Key);
                    // Unity semantics: below this height the next LOD is used; below the last LOD's height it is culled.
                    float height = cfg != null ? cfg.screenRelativeTransitionHeight : 0.5f / (index + 2);
                    lods.Add(new LOD(height, kv.Value.ToArray()));
                    index++;
                }
                // LOD heights must strictly decrease.
                for (int i = 1; i < lods.Count; i++)
                    if (lods[i].screenRelativeTransitionHeight >= lods[i - 1].screenRelativeTransitionHeight)
                    {
                        var l = lods[i];
                        l.screenRelativeTransitionHeight = lods[i - 1].screenRelativeTransitionHeight * 0.5f;
                        lods[i] = l;
                    }
                var group = instance.GetComponent<LODGroup>();
                if (group == null) group = instance.AddComponent<LODGroup>();
                group.fadeMode = LODFadeMode.None;
                group.SetLODs(lods.ToArray());
                // U1-CHAR.8 mobile skinning: fewer bone influences on the smaller LODs (LOD0 4, LOD1 2, further 1)
                for (int i = 0; i < lods.Count; i++)
                    foreach (var r in lods[i].renderers)
                    {
                        var smr = r as SkinnedMeshRenderer;
                        if (smr == null) continue;
                        int bones = UpdateRatePolicy.SkinBonesForLod(i);
                        smr.quality = bones >= 4 ? SkinQuality.Bone4 : (bones == 2 ? SkinQuality.Bone2 : SkinQuality.Bone1);
                        smr.updateWhenOffscreen = false;
                    }
                group.RecalculateBounds();
                Directory.CreateDirectory(PrefabFolder);
                bool ok;
                var prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath, out ok);
                message = ok
                    ? "Dravio LOD prefab created: " + prefabPath + " (" + string.Join(", ", lods.Select((l, i) => "LOD" + i + "@" + l.screenRelativeTransitionHeight.ToString("0.###")).ToArray()) + "). Verify transitions in Scene view at target device resolutions."
                    : "Saving prefab failed: " + prefabPath;
                return ok ? prefab : null;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
