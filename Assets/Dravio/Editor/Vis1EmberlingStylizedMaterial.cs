using System.IO;
using UnityEditor;
using UnityEngine;

namespace Dravio.Editor
{
    /// <summary>
    /// VIS.1 (Issue #80): switch the playable Emberling (EmberlingRefinedGLBVisual prefab, used by
    /// OriginLongTermGameplay) from the imported URP/Lit material to "Dravio/Character/Stylized Lit",
    /// which uses the same ramp / shadow tint / ambient model as the Origin environment.
    /// Visual only: no mesh, bone, Animator, collider, transform or GUID changes. The imported FBX and
    /// its embedded material are untouched; the prefab gets a material override that Revert removes.
    /// </summary>
    internal static class Vis1EmberlingStylizedMaterial
    {
        const string PrefabPath = "Assets/Dravio/Art/Imported/Prefabs/EmberlingRefinedGLBVisual.prefab";
        const string MaterialPath = "Assets/Dravio/Art/Materials/M_CHR_Emberling_Stylized.mat";
        const string ShaderName = "Dravio/Character/Stylized Lit";

        [MenuItem("Dravio/Visual/VIS.1 Apply Stylized Emberling Material")]
        static void Apply()
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null || !shader.isSupported)
            {
                Debug.LogError($"Dravio VIS.1: shader '{ShaderName}' missing or not supported; prefab left unchanged.");
                return;
            }
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Material stylized = null;
                int slots = 0;
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        var src = mats[i];
                        if (src == null) continue;
                        if (stylized == null) stylized = GetOrCreate(shader, src);
                        if (mats[i] != stylized) { mats[i] = stylized; slots++; }
                    }
                    r.sharedMaterials = mats;
                }
                if (slots == 0)
                {
                    Debug.Log("Dravio VIS.1: Emberling already uses the stylized material; VIS.3 settings refreshed.");
                    return;
                }
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"Dravio VIS.1: {slots} Emberling material slot(s) now use {MaterialPath}. " +
                          "Open OriginLongTermGameplay and compare; use Revert to undo.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("Dravio/Visual/VIS.1 Revert Emberling to Imported Material")]
        static void Revert()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                int reverted = 0;
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (!PrefabUtility.IsPartOfPrefabInstance(r)) continue;
                    var so = new SerializedObject(r);
                    var prop = so.FindProperty("m_Materials");
                    if (prop == null || !prop.prefabOverride) continue;
                    PrefabUtility.RevertPropertyOverride(prop, InteractionMode.AutomatedAction);
                    reverted++;
                }
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"Dravio VIS.1: reverted {reverted} Emberling renderer(s) to the imported FBX material.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static Material GetOrCreate(Shader shader, Material src)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));
                mat = new Material(shader) { name = "M_CHR_Emberling_Stylized" };
                AssetDatabase.CreateAsset(mat, MaterialPath);
            }
            else mat.shader = shader;
            Texture baseMap = null;
            if (src.HasProperty("_BaseMap")) baseMap = src.GetTexture("_BaseMap");
            if (baseMap == null && src.HasProperty("_MainTex")) baseMap = src.GetTexture("_MainTex");
            if (baseMap == null) Debug.LogWarning("Dravio VIS.1: imported Emberling material has no base map.");
            mat.SetTexture("_BaseMap", baseMap);
            if (src.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", src.GetColor("_BaseColor"));
            // Matches the Origin VIS.1 environment grade (OriginSanctuaryEnvironmentBuilder.ApplyOriginVis1Grade).
            mat.SetColor("_ShadowTint", new Color(0.70f, 0.72f, 0.91f, 1f));
            mat.SetFloat("_RampThreshold", 0f);
            mat.SetFloat("_RampSoftness", 0.34f);
            mat.SetColor("_RimColor", new Color(1f, 0.84f, 0.62f, 1f));
            mat.SetFloat("_RimStrength", 0.24f);
            mat.SetFloat("_SpecStrength", 0.22f);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }
    }
}
