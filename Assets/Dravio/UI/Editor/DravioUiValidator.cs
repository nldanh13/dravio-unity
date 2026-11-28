using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Dravio.UI;
using UnityEditor;
using UnityEngine;

namespace Dravio.Editor.UiTools
{
    /// <summary>
    /// Dravio > UI > Validate UI Sprites: checks every slot of ui-sprite-slots.json against the import profile
    /// and reports the actual displayed-pixel ratio at the five QA resolutions (VIS.2a). Read-only: it never
    /// edits importers or art. Report: Logs/DravioUiSpriteValidation.md (git-ignored).
    /// </summary>
    internal static class DravioUiValidator
    {
        internal readonly struct QaScreen
        {
            public readonly string Name;public readonly int W,H;public readonly bool Mobile;
            public QaScreen(string n,int w,int h,bool m){Name=n;W=w;H=h;Mobile=m;}
        }
        // Issue #86 matrix. Desktop rows use the 720 reference; phone/tablet rows use the compact 540 reference.
        internal static readonly QaScreen[] Screens=
        {
            new QaScreen("1280x720",1280,720,false),new QaScreen("1920x1080",1920,1080,false),
            new QaScreen("2400x1080 phone",2400,1080,true),new QaScreen("2560x1440",2560,1440,false),
            new QaScreen("1024x768 tablet",1024,768,true),new QaScreen("3200x1440 phone (max)",3200,1440,true),
        };

        [MenuItem("Dravio/UI/Validate UI Sprites")]
        static void Validate()
        {
            var manifest=DravioUiSlotManifest.Load();
            if(manifest==null){Debug.LogError("Dravio UI: "+DravioUiSlotManifest.AssetPath+" missing.");return;}
            var md=new StringBuilder();
            md.AppendLine("# Dravio UI sprite validation ("+manifest.version+")");
            md.AppendLine();
            md.AppendLine("| Slot | File | Source | Import max | Border | Issues | Sample ratio "+string.Join(" / ",Screens.Select(s=>s.Name))+" |");
            md.AppendLine("|---|---|---|---|---|---|---|");
            int missing=0,errors=0,ok=0;
            var known=new HashSet<string>();
            foreach(var slot in manifest.slots)
            {
                foreach(var state in slot.states??new[]{"normal"})
                {
                    var path=slot.StatePath(state);known.Add(path);
                    var issues=Check(path,slot.sourceWidth,slot.sourceHeight,slot.sliced,slot.sliced?slot.borderUnits:slot.DisplayShortUnits,DravioUiSlotManifest.RecommendedMaxSize(slot),out var source,out var import,out var border,out var ratios,out bool exists);
                    if(!exists){if(state=="normal")missing++;md.AppendLine("| "+slot.id+" | "+state+" | — | — | — | MISSING: flat fallback colour | — |");continue;}
                    if(issues.Count==0)ok++;else errors++;
                    md.AppendLine("| "+slot.id+" | "+state+" | "+source+" | "+import+" | "+border+" | "+(issues.Count==0?"ok":string.Join("; ",issues))+" | "+ratios+" |");
                }
            }
            foreach(var set in new[]{manifest.icons,manifest.items})
            {
                if(set?.ids==null)continue;
                foreach(var id in set.ids)
                {
                    var path=set.PathFor(id);known.Add(path);
                    var issues=Check(path,set.sourceSize,set.sourceSize,false,set.MaxDisplayUnits,DravioUiSlotManifest.RecommendedMaxSize(set),out var source,out var import,out var border,out var ratios,out bool exists);
                    if(!exists){missing++;continue;}
                    if(issues.Count==0)ok++;else errors++;
                    md.AppendLine("| "+id+" | icon | "+source+" | "+import+" | "+border+" | "+(issues.Count==0?"ok":string.Join("; ",issues))+" | "+ratios+" |");
                }
            }
            // Misnamed sources must not be used directly: anything in Sprites that is not in the contract.
            var root=DravioUiSlotManifest.SpritesRoot.TrimEnd('/');
            var unmapped=!AssetDatabase.IsValidFolder(root)?new List<string>():AssetDatabase.FindAssets("t:Texture2D",new[]{root})
                .Select(AssetDatabase.GUIDToAssetPath).Where(p=>!known.Contains(p)).ToList();
            md.AppendLine();
            md.AppendLine("Unmapped files (rename to a slot path or add a slot): "+(unmapped.Count==0?"none":string.Join(", ",unmapped)));
            md.AppendLine();
            md.AppendLine("Summary: ok "+ok+", issues "+errors+", missing normal-state slots/icons "+missing+". Ratio = imported px / displayed px; <1 is blurry upscale, >2.5 risks aliasing without mipmaps.");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/DravioUiSpriteValidation.md",md.ToString());
            var log="Dravio UI sprites: ok "+ok+", issues "+errors+", missing "+missing+", unmapped "+unmapped.Count+". Report: Logs/DravioUiSpriteValidation.md";
            if(errors>0 || unmapped.Count>0)Debug.LogWarning(log);else Debug.Log(log);
        }

        // displayUnits = border units for sliced sprites (only corners/edges must be sharp), short side otherwise.
        static List<string> Check(string path,int expectW,int expectH,bool sliced,float displayUnits,int recommendedMax,
            out string source,out string import,out string borderText,out string ratios,out bool exists)
        {
            var issues=new List<string>();source=import=borderText=ratios="—";
            var ti=AssetImporter.GetAtPath(path) as TextureImporter;
            exists=ti!=null;
            if(ti==null)return issues;
            ti.GetSourceTextureWidthAndHeight(out int w,out int h);
            source=w+"x"+h;
            if(w!=expectW || h!=expectH)issues.Add("source "+source+" != contract "+expectW+"x"+expectH);
            if(ti.textureType!=TextureImporterType.Sprite)issues.Add("not Sprite");
            if(ti.mipmapEnabled)issues.Add("mipmaps on");
            if(ti.wrapMode!=TextureWrapMode.Clamp)issues.Add("wrap not Clamp");
            if(!ti.alphaIsTransparency)issues.Add("alphaIsTransparency off");
            if(!ti.DoesSourceTextureHaveAlpha())issues.Add("source has no alpha (RGBA PNG required)");
            if(!ti.sRGBTexture)issues.Add("not sRGB");
            int max=ti.maxTextureSize;
            float k=Mathf.Min(1f,max/(float)Mathf.Max(w,h));
            import=max+" ("+Mathf.RoundToInt(w*k)+"x"+Mathf.RoundToInt(h*k)+"), contract "+recommendedMax;
            if(max>recommendedMax)issues.Add("max size "+max+" above recommended "+recommendedMax+" (memory)");
            var b=ti.spriteBorder;
            var border=new UiBorder((int)b.x,(int)b.y,(int)b.z,(int)b.w);
            borderText=border.ToString();
            issues.AddRange(UiSpritePolicy.ValidateBorder(w,h,border,sliced));
            // Sample ratio: imported px / displayed px, of the border (sliced) or the short side (simple).
            int imported=Mathf.RoundToInt((sliced?border.MaxSide:Mathf.Min(w,h))*k);
            var values=Screens.Select(sc=>sliced
                ?UiSpritePolicy.BorderSampleRatio(imported,displayUnits,ScaleOf(sc))
                :UiSpritePolicy.SampleRatio(imported,displayUnits,ScaleOf(sc))).ToList();
            ratios=string.Join(" / ",values.Select(v=>v.ToString("0.00")));
            if(displayUnits>0 && values.Min()<1f)issues.Add("upscaled on largest screen (ratio "+values.Min().ToString("0.00")+")");
            if(sliced && displayUnits>0 && values.Max()>3f)issues.Add("downsampled "+values.Max().ToString("0.0")+"x on 720p: check edge aliasing");
            return issues;
        }

        static float ScaleOf(QaScreen sc)=>HudLayoutPolicy.Compute(sc.W,sc.H,new HudPixelRect(0,0,sc.W,sc.H),sc.Mobile).CanvasScale;

        [MenuItem("Dravio/UI/QA/Print HUD Layout Matrix")]
        static void PrintMatrix()
        {
            var sb=new StringBuilder("Dravio HUD layout matrix (HudLayoutPolicy, no Game View needed)\n");
            foreach(var screen in Screens)
                foreach(var notch in new[]{0f,.055f})
                {
                    float inset=screen.W*notch;
                    var r=HudLayoutPolicy.Compute(screen.W,screen.H,new HudPixelRect(inset,0,screen.W-inset,screen.H),screen.Mobile);
                    sb.AppendLine(screen.Name+(notch>0?" notch-left":"")+": scale "+r.CanvasScale.ToString("0.00")+", ref "+r.ReferenceWidth+"x"+r.ReferenceHeight+
                        ", safe "+r.SafeWidthUnits.ToString("0")+"u, drawer "+r.RailWidthUnits.ToString("0")+"u, free world "+(r.FreeViewportFraction*100).ToString("0")+"%, "+
                        "4-tab cell "+HudLayoutPolicy.TabCellWidth(r.RailWidthUnits,4).ToString("0")+"u, 44u touch = "+r.UnitsToPixels(HudLayoutPolicy.MinTouchTarget).ToString("0")+"px");
                }
            Debug.Log(sb.ToString());
        }
    }
}
