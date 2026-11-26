using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Dravio.Character.Editor
{
    /// <summary>
    /// Creates a new URP/Lit material from a pipeline texture set:
    /// &lt;Name&gt;_BaseColor, &lt;Name&gt;_Normal (optional), &lt;Name&gt;_MetallicSmoothness (optional; R = metallic,
    /// A = smoothness, written by prepare_character.py from glTF metallicRoughness).
    /// This bypasses FBX material conversion, which cannot carry packed glTF channels.
    /// Never overwrites an existing material.
    /// </summary>
    internal static class CharacterMaterialBuilder
    {
        internal const string MaterialFolder = "Assets/Dravio/Character/Materials";

        [MenuItem("Dravio/Character/Create URP Material From Selected BaseColor Texture")]
        private static void Menu()
        {
            var tex = Selection.objects.OfType<Texture2D>().FirstOrDefault();
            string path = tex != null ? AssetDatabase.GetAssetPath(tex) : null;
            if (path == null || !Path.GetFileNameWithoutExtension(path).EndsWith("_BaseColor"))
            {
                Debug.LogWarning("Dravio material: select a *_BaseColor texture (pipeline naming).");
                return;
            }
            string message;
            var mat = Build(path, out message);
            if (mat != null) { Debug.Log(message); EditorGUIUtility.PingObject(mat); Selection.activeObject = mat; }
            else Debug.LogError(message);
        }

        internal static Material Build(string baseColorPath, out string message)
        {
            string dir = Path.GetDirectoryName(baseColorPath).Replace('\\', '/');
            string stem = Path.GetFileNameWithoutExtension(baseColorPath);
            string name = stem.Substring(0, stem.Length - "_BaseColor".Length);
            string ext = Path.GetExtension(baseColorPath);
            string matPath = MaterialFolder + "/" + name + ".mat";
            if (File.Exists(matPath)) { message = "Material exists, not overwritten: " + matPath; return null; }
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) { message = "URP Lit shader not found; is URP the active pipeline?"; return null; }
            var mat = new Material(shader) { name = name };
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(baseColorPath));
            mat.SetColor("_BaseColor", Color.white);
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/" + name + "_Normal" + ext);
            if (normal != null)
            {
                var ti = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(normal)) as TextureImporter;
                if (ti != null && ti.textureType != TextureImporterType.NormalMap)
                {
                    message = "Normal texture is not imported as NormalMap: " + AssetDatabase.GetAssetPath(normal);
                    return null;
                }
                mat.SetTexture("_BumpMap", normal);
                mat.SetFloat("_BumpScale", 1f);
                mat.EnableKeyword("_NORMALMAP");
            }
            var ms = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/" + name + "_MetallicSmoothness" + ext);
            if (ms != null)
            {
                mat.SetTexture("_MetallicGlossMap", ms);
                mat.SetFloat("_Smoothness", 1f); // URP multiplies map alpha by this
                mat.SetFloat("_SmoothnessTextureChannel", 0f); // 0 = metallic map alpha
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            else
            {
                mat.SetFloat("_Metallic", 0f);
                mat.SetFloat("_Smoothness", 0.25f);
            }
            Directory.CreateDirectory(MaterialFolder);
            AssetDatabase.CreateAsset(mat, matPath);
            AssetDatabase.SaveAssets();
            message = "Dravio URP material created: " + matPath + " (normal=" + (normal != null) + ", metallicSmoothness=" + (ms != null)
                      + "). Compare against the Blender .blend under the same light before accepting.";
            return mat;
        }
    }
}
