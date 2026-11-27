using UnityEditor;
using UnityEngine;

namespace Dravio.EnvironmentArt.EditorTools
{
    /// <summary>
    /// Mobile import rules for the generated Origin Sanctuary kit. Scoped strictly to
    /// Assets/Dravio/Environment/Models, /Textures and /Audio so the Emberling FBX, its
    /// Animator and every other asset keep their own importer settings.
    /// U1-ENV.4: the same rules for the zone kits in
    /// Assets/Dravio/Environment/Zones/&lt;Zone&gt;/Models, /Textures and /Audio.
    /// </summary>
    public sealed class EnvironmentImportSettings : AssetPostprocessor
    {
        public const string ModelsFolder = "Assets/Dravio/Environment/Models/";
        public const string TexturesFolder = "Assets/Dravio/Environment/Textures/";
        public const string AudioFolder = "Assets/Dravio/Environment/Audio/";
        public const string ZonesFolder = "Assets/Dravio/Environment/Zones/";

        public override uint GetVersion() => 7;

        /// <summary>Origin folder (e.g. Environment/Models/) or Environment/Zones/&lt;Zone&gt;/Models/.</summary>
        static bool InKitFolder(string path, string originFolder, string sub)
        {
            if (path.StartsWith(originFolder, System.StringComparison.Ordinal)) return true;
            if (!path.StartsWith(ZonesFolder, System.StringComparison.Ordinal)) return false;
            string rest = path.Substring(ZonesFolder.Length);
            int slash = rest.IndexOf('/');
            return slash > 0 && rest.Substring(slash + 1).StartsWith(sub + "/", System.StringComparison.Ordinal);
        }

        void OnPreprocessModel()
        {
            if (!InKitFolder(assetPath, ModelsFolder, "Models")) return;
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
            if (!InKitFolder(assetPath, TexturesFolder, "Textures")) return;
            var ti = (TextureImporter)assetImporter;
            string file = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            if (file.StartsWith("T_ENV_FX_", System.StringComparison.Ordinal))
            {
                ConfigureFx(ti, file);
                return;
            }
            if (file.StartsWith("T_ENV_WaterFlow_", System.StringComparison.Ordinal))
            {
                ConfigureWaterFlow(ti, file);
                return;
            }
            bool foliage = file.Contains("Foliage");
            bool tiling = file.Contains("Ground") || file.Contains("Water");
            int max = 512;
            if (file.Contains("PropsAtlas") || foliage) max = 1024;
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

        // U1-ENV.3 particle sprites: tiny white alpha masks.
        // U1-ENV.4: caustics (256, tiling), light shaft (64x256) and cloud wisp (128) masks.
        static void ConfigureFx(TextureImporter ti, string file)
        {
            int max = file.Contains("Caustics") || file.Contains("LightShaft") ? 256 : file.Contains("CloudWisp") ? 128 : 64;
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = true;
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = true;
            ti.wrapMode = file.Contains("Caustics") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.isReadable = false;
            ti.maxTextureSize = max;
            ti.textureCompression = TextureImporterCompression.Compressed;
            foreach (string platform in new[] { "Android", "iPhone" })
            {
                var ps = ti.GetPlatformTextureSettings(platform);
                ps.overridden = true;
                ps.maxTextureSize = max;
                ps.format = TextureImporterFormat.ASTC_4x4;
                ti.SetPlatformTextureSettings(ps);
            }
        }

        // VIS.4b (Issue #91): VIS9 flowing-water maps for "Painterly Flow". Colour + alpha, sRGB.
        // Silk / ripples tile (repeat, 256); the 4x4 foam flipbook clamps (512, 18 px cell gutters
        // keep the first mips clean). ASTC 6x6 like the rest of the kit's colour maps.
        static void ConfigureWaterFlow(TextureImporter ti, string file)
        {
            bool foam = file.Contains("Foam");
            int max = foam ? 512 : 256;
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = true;
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = true;
            ti.streamingMipmaps = false;
            ti.wrapMode = foam ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            ti.filterMode = FilterMode.Bilinear;
            ti.anisoLevel = 1;
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

        // U1-ENV.3 ambience: mono, Vorbis loops kept compressed in memory, ADPCM one-shots.
        void OnPreprocessAudio()
        {
            if (!InKitFolder(assetPath, AudioFolder, "Audio")) return;
            var ai = (AudioImporter)assetImporter;
            bool loop = System.IO.Path.GetFileNameWithoutExtension(assetPath).StartsWith("AMB_", System.StringComparison.Ordinal);
            ai.forceToMono = true;
            ai.loadInBackground = loop;
            var s = ai.defaultSampleSettings;
            s.loadType = loop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = loop ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
            s.quality = loop ? 0.45f : 1f;
            s.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
            s.sampleRateOverride = 22050;
            s.preloadAudioData = true;
            ai.defaultSampleSettings = s;
        }
    }
}
