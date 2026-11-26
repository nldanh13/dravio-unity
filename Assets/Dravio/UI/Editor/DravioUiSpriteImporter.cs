using Dravio.UI;
using UnityEditor;
using UnityEngine;

namespace Dravio.Editor.UiTools
{
    /// <summary>
    /// Import profile for runtime UI sprites under Assets/Dravio/UI/Sprites (VIS.2a). Safety settings are
    /// enforced on every import: Sprite (Single, FullRect), RGBA with alpha-is-transparency, sRGB, no mipmaps,
    /// Clamp, Bilinear, no NPOT rescale. Size and compression are only set on first import so QA can tune a
    /// slot. A border from ui-sprite-slots.json is copied only while the sprite has none (Sprite Editor wins).
    /// ArtSource is never touched.
    /// </summary>
    internal sealed class DravioUiSpriteImporter : AssetPostprocessor
    {
        public static bool Applies(string path)=>path.Replace('\\','/').StartsWith(DravioUiSlotManifest.SpritesRoot,System.StringComparison.Ordinal)
            && path.EndsWith(".png",System.StringComparison.OrdinalIgnoreCase);

        void OnPreprocessTexture()
        {
            if(!Applies(assetPath))return;
            var ti=(TextureImporter)assetImporter;
            bool first=ti.importSettingsMissing;
            ti.textureType=TextureImporterType.Sprite;
            ti.spriteImportMode=SpriteImportMode.Single;
            ti.sRGBTexture=true;
            ti.alphaSource=TextureImporterAlphaSource.FromInput;
            ti.alphaIsTransparency=true;
            ti.mipmapEnabled=false;
            ti.wrapMode=TextureWrapMode.Clamp;
            ti.filterMode=FilterMode.Bilinear;
            ti.npotScale=TextureImporterNPOTScale.None;
            ti.isReadable=false;
            var settings=new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            settings.spriteMeshType=SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape=false;
            ti.SetTextureSettings(settings);

            var manifest=DravioUiSlotManifest.Load();
            int maxSize=1024;
            if(manifest!=null && manifest.TryDescribe(assetPath,out var slot,out var set))
            {
                maxSize=slot!=null?DravioUiSlotManifest.RecommendedMaxSize(slot):DravioUiSlotManifest.RecommendedMaxSize(set);
                var border=slot!=null?slot.Border:default;
                if(slot!=null && slot.sliced && ti.spriteBorder==Vector4.zero && !border.IsZero)
                    ti.spriteBorder=new Vector4(border.Left,border.Bottom,border.Right,border.Top);
            }
            if(!first)return;
            ti.spritePixelsPerUnit=100;
            ti.maxTextureSize=maxSize;
            ti.textureCompression=TextureImporterCompression.Uncompressed;
            foreach(var platform in new[]{"Android","iPhone"})
                ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                {
                    name=platform,overridden=true,maxTextureSize=maxSize,
                    format=TextureImporterFormat.ASTC_4x4,compressionQuality=100,
                });
        }
    }
}
