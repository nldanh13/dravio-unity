using System;
using UnityEditor;
using UnityEngine;

namespace Dravio.Character.Editor
{
    /// <summary>
    /// First-import defaults for character assets placed under Assets/Dravio/Character/Models and
    /// Assets/Dravio/Character/Textures only. Applied once (importSettingsMissing), so any later manual
    /// change in the Inspector is kept. Files elsewhere in the project are never touched.
    /// Texture role comes from the pipeline's file suffixes: _BaseColor, _Normal, _MetallicSmoothness.
    /// </summary>
    internal sealed class CharacterImportPreset : AssetPostprocessor
    {
        internal const string ModelFolder = "Assets/Dravio/Character/Models/";
        internal const string TextureFolder = "Assets/Dravio/Character/Textures/";

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(ModelFolder, StringComparison.Ordinal) || !assetImporter.importSettingsMissing) return;
            var mi = (ModelImporter)assetImporter;
            mi.importCameras = false;
            mi.importLights = false;
            mi.isReadable = false;
            mi.optimizeMeshVertices = true;
            mi.optimizeMeshPolygons = true;
            mi.weldVertices = true;
            mi.meshCompression = ModelImporterMeshCompression.Off; // compression can visibly shift skinned vertices
            mi.importNormals = ModelImporterNormals.Import;
            mi.importTangents = ModelImporterTangents.CalculateMikk; // matches Blender/glTF tangent space for normal maps
            mi.skinWeights = ModelImporterSkinWeights.Standard; // 4 influences, the pipeline budget
            mi.animationType = ModelImporterAnimationType.Generic;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            Debug.Log("Dravio character import defaults applied (first import only): " + assetPath);
        }

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(TextureFolder, StringComparison.Ordinal) || !assetImporter.importSettingsMissing) return;
            var budgets = CharacterBudgets.Load();
            var ti = (TextureImporter)assetImporter;
            string name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            ti.mipmapEnabled = true;
            ti.maxTextureSize = budgets.textures.maxSize;
            ti.wrapMode = TextureWrapMode.Clamp;
            if (name.EndsWith("_Normal", StringComparison.OrdinalIgnoreCase))
            {
                ti.textureType = TextureImporterType.NormalMap; // glTF/Blender normal maps are OpenGL (+Y), as Unity expects
            }
            else if (name.EndsWith("_MetallicSmoothness", StringComparison.OrdinalIgnoreCase))
            {
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = false;
                ti.alphaSource = TextureImporterAlphaSource.FromInput; // A = smoothness
                ti.alphaIsTransparency = false;
            }
            else
            {
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = true;
            }
            foreach (var platform in new[] { "Android", "iPhone" })
            {
                var s = ti.GetPlatformTextureSettings(platform);
                s.overridden = true;
                s.maxTextureSize = budgets.textures.recommendedSize;
                s.format = TextureImporterFormat.ASTC_6x6;
                ti.SetPlatformTextureSettings(s);
            }
            Debug.Log("Dravio character texture defaults applied (first import only): " + assetPath);
        }
    }
}
