using System.IO;
using Dravio.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Dravio.Editor.UiTools
{
    /// <summary>
    /// Theme creation/assignment and HUD QA helpers (VIS.2a). Nothing here runs automatically: a theme only
    /// affects the HUD after it is assigned to FantasyPreviewUI.theme, and the scene is not saved for you.
    /// </summary>
    internal static class DravioUiThemeMenu
    {
        const string ThemeFolder="Assets/Dravio/UI/Themes";

        [MenuItem("Dravio/UI/Theme/Create Legacy Theme (neutral)")]
        static void CreateLegacy()=>Create("DravioUiTheme_Legacy",t=>t.ResetToLegacyPalette());

        [MenuItem("Dravio/UI/Theme/Create Bright Fantasy v1 Theme (proposed)")]
        static void CreateBright()=>Create("DravioUiTheme_BrightFantasyV1",t=>t.ResetToBrightFantasyV1());

        static void Create(string name,System.Action<DravioUiTheme> preset)
        {
            Directory.CreateDirectory(ThemeFolder);
            AssetDatabase.Refresh();
            var theme=ScriptableObject.CreateInstance<DravioUiTheme>();
            preset(theme);
            FillSprites(theme);
            var path=AssetDatabase.GenerateUniqueAssetPath(ThemeFolder+"/"+name+".asset");
            AssetDatabase.CreateAsset(theme,path);
            AssetDatabase.SaveAssets();
            Selection.activeObject=theme;
            Debug.Log("Dravio UI: created "+path+" ("+theme.presetName+"). Assign it with Dravio > UI > Theme > Assign Selected Theme To Open HUD.");
        }

        [MenuItem("Dravio/UI/Theme/Fill Selected Theme Sprites From Slots")]
        static void FillSelected()
        {
            if(!(Selection.activeObject is DravioUiTheme theme)){Debug.LogWarning("Dravio UI: select a DravioUiTheme asset.");return;}
            Undo.RecordObject(theme,"Fill Dravio UI theme sprites");
            int n=FillSprites(theme);
            EditorUtility.SetDirty(theme);AssetDatabase.SaveAssets();
            Debug.Log("Dravio UI: "+n+" sprite(s) linked from "+DravioUiSlotManifest.AssetPath+"; empty slots keep their fallback colour.");
        }

        /// <summary>Links sprites that exist at the contract paths; never creates or edits art.</summary>
        static int FillSprites(DravioUiTheme theme)
        {
            var manifest=DravioUiSlotManifest.Load();
            if(manifest?.slots==null)return 0;
            int linked=0;
            foreach(var slot in manifest.slots)
            {
                var target=theme.Find(slot.Role);
                if(target==null)continue;
                var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(slot.path);
                if(sprite!=null){target.sprite=sprite;linked++;}
                target.borderUnits=slot.borderUnits>0?slot.borderUnits:target.borderUnits;
                target.pressed=AssetDatabase.LoadAssetAtPath<Sprite>(slot.StatePath("pressed"))??target.pressed;
                target.disabled=AssetDatabase.LoadAssetAtPath<Sprite>(slot.StatePath("disabled"))??target.disabled;
            }
            return linked;
        }

        [MenuItem("Dravio/UI/Theme/Assign Selected Theme To Open HUD")]
        static void Assign()
        {
            var theme=Selection.activeObject as DravioUiTheme;
            var hud=Object.FindAnyObjectByType<FantasyPreviewUI>(FindObjectsInactive.Include);
            if(hud==null){Debug.LogWarning("Dravio UI: no FantasyPreviewUI in the open scene.");return;}
            var so=new SerializedObject(hud);
            so.FindProperty("theme").objectReferenceValue=theme;
            so.ApplyModifiedProperties();
            if(Application.isPlaying)hud.SetTheme(theme);
            else EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
            Debug.Log("Dravio UI: HUD theme = "+(theme!=null?theme.name:"none (legacy colours)")+". Scene marked dirty; save only after QA.",hud);
        }

        [MenuItem("Dravio/UI/QA/Simulate Notch/None")] static void NotchNone()=>HudSafeAreaOverride.Simulate(HudSafeAreaOverride.Notch.None);
        [MenuItem("Dravio/UI/QA/Simulate Notch/Left")] static void NotchLeft()=>HudSafeAreaOverride.Simulate(HudSafeAreaOverride.Notch.Left);
        [MenuItem("Dravio/UI/QA/Simulate Notch/Right")] static void NotchRight()=>HudSafeAreaOverride.Simulate(HudSafeAreaOverride.Notch.Right);

        /// <summary>Captures the Game View (overlay HUD included) for the before/after record.</summary>
        [MenuItem("Dravio/UI/QA/Capture Game View")]
        static void Capture()
        {
            if(!Application.isPlaying){Debug.LogWarning("Dravio UI: enter Play Mode, set the Game View size, then capture.");return;}
            var hud=Object.FindAnyObjectByType<FantasyPreviewUI>();
            string lang=hud!=null && !hud.Vietnamese?"en":"vi";
            string theme=hud!=null && hud.Theme!=null?hud.Theme.name:"legacy";
            Directory.CreateDirectory("Screenshots/vis2a");
            var file="Screenshots/vis2a/"+Screen.width+"x"+Screen.height+"_"+lang+"_"+theme+"_notch-"+HudSafeAreaOverride.Simulated+".png";
            ScreenCapture.CaptureScreenshot(file);
            Debug.Log("Dravio UI: capture queued → "+file);
        }
    }
}
