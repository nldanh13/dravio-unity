using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Dravio.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace Dravio.Editor.UiTools
{
    /// <summary>
    /// TextMeshPro/SDF readiness for Vietnamese (VIS.2a). Audit only, plus one isolated sample scene.
    /// It does not convert any serialized UnityEngine.UI.Text in the HUD, change fonts in existing scenes or
    /// add a font file to the repository (font choice and licence belong to AI3/owner).
    /// </summary>
    internal static class DravioTmpVietnameseTools
    {
        const string FontFolder="Assets/Dravio/UI/Fonts/Generated";
        const string SampleScene="Assets/Dravio/UI/Samples/TmpVietnameseSample.unity";
        const string Pangram="Chăm sóc rồng non • Ấp trứng • Đồng cỏ Khởi Nguyên • Nhiệm vụ • Bách khoa\n"+
            "Ăn quả mọng đỏ, uống nước suối, nghỉ dưới bóng cây cổ thụ. Sẵn sàng khám phá đảo mây!\n"+
            "ẮẰẲẴẶ ẤẦẨẪẬ ẾỀỂỄỆ ỐỒỔỖỘ ỚỜỞỠỢ ỨỪỬỮỰ ỲỶỸỴÝ Đđ\n"+
            "The quick brown fox jumps over the lazy dragon 0123456789";

        [MenuItem("Dravio/UI/TMP/Audit Selected Font For Vietnamese")]
        static void Audit()
        {
            var selected=Selection.activeObject;
            TMP_FontAsset asset=selected as TMP_FontAsset;
            bool temporary=false;
            if(asset==null && selected is Font font)
            {
                asset=TMP_FontAsset.CreateFontAsset(font,64,6,GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,false);
                temporary=true;
            }
            if(asset==null){Debug.LogWarning("Dravio TMP: select a TMP Font Asset or a .ttf/.otf Font.");return;}
            var missing=VietnameseGlyphs.Missing(VietnameseGlyphs.Required,c=>asset.HasCharacter(c,true,true));
            var hudMissing=VietnameseGlyphs.Missing(HudStrings(),c=>asset.HasCharacter(c,true,true));
            var msg="Dravio TMP audit '"+selected.name+"': required set "+VietnameseGlyphs.Required.Length+" chars, missing "+missing.Count+
                (missing.Count>0?" ["+new string(missing.ToArray())+"]":"")+"; HUD source strings missing "+hudMissing.Count+
                (hudMissing.Count>0?" ["+new string(hudMissing.ToArray())+"]":"")+
                ". Atlas mode "+asset.atlasPopulationMode+". Licence: check the font's own licence file (OFL/Apache) before committing it.";
            if(missing.Count>0 || hudMissing.Count>0)Debug.LogWarning(msg,selected);else Debug.Log(msg,selected);
            if(temporary)Object.DestroyImmediate(asset);
        }

        /// <summary>Every quoted literal in the HUD builder/runtime, so the audit covers the real copy.</summary>
        static string HudStrings()
        {
            var sb=new StringBuilder();
            foreach(var file in new[]{"Assets/Dravio/Editor/FantasyPreviewHud.cs","Assets/Dravio/Scripts/FantasyPreviewUI.cs",
                "Assets/Dravio/Scripts/DragonCarePreview.cs","Assets/Dravio/Scripts/ResearchDiscoveryPanel.cs"})
            {
                if(!File.Exists(file))continue;
                var text=File.ReadAllText(file);
                bool inside=false;
                for(int i=0;i<text.Length;i++)
                {
                    char c=text[i];
                    if(c=='"' && (i==0 || text[i-1]!='\\')){inside=!inside;continue;}
                    if(inside && c>127)sb.Append(c);
                }
            }
            return new string(sb.ToString().Distinct().ToArray());
        }

        [MenuItem("Dravio/UI/TMP/Create Isolated Vietnamese Sample From Selected Font")]
        static void CreateSample()
        {
            if(!(Selection.activeObject is Font font)){Debug.LogWarning("Dravio TMP: select a .ttf/.otf Font in the Project window.");return;}
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            Directory.CreateDirectory(FontFolder);Directory.CreateDirectory(Path.GetDirectoryName(SampleScene));
            AssetDatabase.Refresh();
            var assetPath=AssetDatabase.GenerateUniqueAssetPath(FontFolder+"/"+font.name+" SDF.asset");
            // Dynamic SDF, single 2048 atlas (≈4 MiB Alpha8). Pre-populated with the Vietnamese set so the first
            // frame does not rasterise glyphs; can be switched to Static after QA.
            var asset=TMP_FontAsset.CreateFontAsset(font,64,6,GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,false);
            asset.name=font.name+" SDF";
            AssetDatabase.CreateAsset(asset,assetPath);
            asset.atlasTextures[0].name=font.name+" Atlas";
            AssetDatabase.AddObjectToAsset(asset.atlasTextures[0],asset);
            asset.material.name=font.name+" Material";
            AssetDatabase.AddObjectToAsset(asset.material,asset);
            asset.TryAddCharacters(VietnameseGlyphs.Required,out string missing);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            var scene=EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects,NewSceneMode.Single);
            var canvasGo=new GameObject("TMP Vietnamese sample",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            var canvas=canvasGo.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);
            scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;scaler.matchWidthOrHeight=1;
            var legacyFont=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            float y=-20;
            foreach(var size in new[]{14,16,20,28})
            {
                var legacy=Node("Legacy Text "+size,canvasGo.transform,new Vector2(20,y),new Vector2(600,size*5.2f));
                var t=legacy.gameObject.AddComponent<Text>();
                t.font=legacyFont;t.fontSize=size;t.text="[Legacy "+size+"] "+Pangram;t.color=Color.white;t.raycastTarget=false;
                var tmpNode=Node("TMP "+size,canvasGo.transform,new Vector2(640,y),new Vector2(620,size*5.2f));
                var tmp=tmpNode.gameObject.AddComponent<TextMeshProUGUI>();
                tmp.font=asset;tmp.fontSize=size;tmp.text="[TMP SDF "+size+"] "+Pangram;tmp.color=Color.white;tmp.raycastTarget=false;
                y-=size*5.6f;
            }
            var cam=Camera.main;if(cam!=null){cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.53f,.33f,.20f);}
            EditorSceneManager.SaveScene(scene,SampleScene);
            Debug.Log("Dravio TMP: sample "+SampleScene+" with "+assetPath+". Missing after population: "+
                (string.IsNullOrEmpty(missing)?"none":missing)+". Compare legacy vs TMP at 1280x720 and 2400x1080; no HUD text was changed.",asset);
        }

        static RectTransform Node(string name,Transform parent,Vector2 topLeft,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);
            var r=(RectTransform)go.transform;r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);
            r.anchoredPosition=topLeft;r.sizeDelta=size;return r;
        }
    }
}
