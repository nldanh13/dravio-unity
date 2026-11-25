using UnityEditor;
using UnityEngine;

namespace Dravio.EnvironmentArt.EditorTools
{
    /// <summary>
    /// Mobile import rules for the generated Origin Sanctuary kit. Scoped strictly to
    /// Assets/Dravio/Environment/Models and /Textures so the Emberling FBX, its
    /// Animator and every other asset keep their own importer settings.
    /// </summary>
    public sealed class EnvironmentImportSettings : AssetPostprocessor
    {
        public const string ModelsFolder = "Assets/Dravio/Environment/Models/";
        public const string TexturesFolder = "Assets/Dravio/Environment/Textures/";

        public override uint GetVersion() => 3;

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(ModelsFolder, System.StringComparison.Ordinal)) return;
            var mi = (ModelImporter)assetImporter;
            // Blender export already bakes axis conversion and unit scale (1 m = 1 unit).
            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.bakeAxisConversion = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importVisibility = false;
            mi.importBlendShapes = false;
            mi.importAnimation = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
            // Embedded material names (ENV_Props...) are remapped to project materials
            // by Dravio > Environment > Setup Materials.
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
            mi.isReadable = false;
            mi.meshCompression = ModelImporterMeshCompression.Low;
            mi.optimizeMeshPolygons = true;
            mi.optimizeMeshVertices = true;
            mi.weldVertices = true;
            mi.importNormals = ModelImporterNormals.Import;
            mi.importTangents = ModelImporterTangents.None; // no normal maps in the kit
            mi.generateSecondaryUV = false;                    // realtime lighting only
            mi.addCollider = false;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(TexturesFolder, System.StringComparison.Ordinal)) return;
            var ti = (TextureImporter)assetImporter;
            string file = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            bool foliage = file.Contains("Foliage");
            bool tiling = file.Contains("Ground") || file.Contains("Water");
            int max = 512;
            if (file.Contains("PropsAtlas")) max = 1024;
            else if (file.Contains("Water")) max = 256;

            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = true;
            ti.mipmapEnabled = true;
            ti.streamingMipmaps = false;
            ti.wrapMode = tiling ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.anisoLevel = file.Contains("Ground") ? 2 : 1;
            ti.alphaSource = foliage ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            ti.alphaIsTransparency = foliage;
            // Keeps alpha-tested cards from thinning out in lower mips.
            ti.mipMapsPreserveCoverage = foliage;
            ti.alphaTestReferenceValue = 0.45f;
            ti.isReadable = false;
            ti.maxTextureSize = max;
            ti.textureCompression = TextureImporterCompression.Compressed;

            foreach (string platform in new[] { "Android", "iPhone" })
            {
                var ps = ti.GetPlatformTextureSettings(platform);
                ps.overridden = true;
                ps.maxTextureSize = max;
                ps.format = TextureImporterFormat.ASTC_6x6;
                ps.compressionQuality = 50;
                ti.SetPlatformTextureSettings(ps);
            }
        }
    }
}
