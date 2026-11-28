using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Dravio.UI;
using NUnit.Framework;

namespace Dravio.Tests
{
    /// <summary>VIS.2a source checks: HUD geometry, sprite slot contract, Vietnamese glyph coverage.</summary>
    public sealed class UiFoundationTests
    {
        static string Root
        {
            get
            {
                var dir=new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
                while(dir!=null && !Directory.Exists(Path.Combine(dir.FullName,"ProjectSettings")))dir=dir.Parent;
                Assert.That(dir,Is.Not.Null,"Unity project root not found above test output");
                return dir.FullName;
            }
        }
        static string Repo(string rel)=>Path.Combine(Root,rel);

        // Issue #86 resolution matrix. Phone/tablet = mobile (compact reference); desktop = Editor/Standalone.
        static readonly (string name,int w,int h,bool mobile)[] Screens=
        {
            ("1280x720",1280,720,false),("1920x1080",1920,1080,false),("2400x1080 phone",2400,1080,true),
            ("2560x1440",2560,1440,false),("1024x768 tablet",1024,768,true),("1024x768 editor",1024,768,false),
            ("960x540 window",960,540,false),("2400x1080 editor",2400,1080,false),("3200x1440 phone",3200,1440,true),
        };

        static IEnumerable<HudLayoutResult> AllLayouts()
        {
            foreach(var s in Screens)
                foreach(var notch in new[]{0f,.055f})
                    foreach(var right in new[]{false,true})
                    {
                        float inset=s.w*notch;
                        var safe=new HudPixelRect(right?0:inset,0,s.w-inset,s.h);
                        yield return HudLayoutPolicy.Compute(s.w,s.h,safe,s.mobile);
                    }
        }

        [Test] public void LayoutMatchesU604bNumbers()
        {
            var desk=HudLayoutPolicy.Compute(1280,720,new HudPixelRect(0,0,1280,720),false);
            Assert.That(desk.Compact,Is.False);Assert.That(desk.CanvasScale,Is.EqualTo(1f));
            Assert.That(desk.RailWidthUnits,Is.EqualTo(326f));
            var phone=HudLayoutPolicy.Compute(2400,1080,new HudPixelRect(0,0,2400,1080),true);
            Assert.That(phone.Compact,Is.True);Assert.That(phone.CanvasScale,Is.EqualTo(2f));
            Assert.That(phone.ReferenceHeight,Is.EqualTo(540f));
            Assert.That(phone.RailWidthUnits,Is.EqualTo(380f)); // 1200*.36=432 clamped to 380
            var tablet=HudLayoutPolicy.Compute(1024,768,new HudPixelRect(0,0,1024,768),true);
            Assert.That(tablet.RailWidthUnits,Is.EqualTo(280f).Within(.01f)); // 720*.36=259 -> min 280
            var shortWindow=HudLayoutPolicy.Compute(960,540,new HudPixelRect(0,0,960,540),false);
            Assert.That(shortWindow.Compact,Is.True,"h<600 uses compact reference");
        }

        [Test] public void InvalidSafeAreaFallsBackToFullScreen()
        {
            var r=HudLayoutPolicy.Compute(1920,1080,new HudPixelRect(0,0,0,0),false);
            Assert.That(r.AnchorMinX,Is.Zero);Assert.That(r.AnchorMaxX,Is.EqualTo(1f));
            Assert.That(r.SafeWidthUnits,Is.EqualTo(1280f).Within(.01f));
        }

        [Test] public void NotchMovesSafeAnchorsNotDrawerMath()
        {
            var left=HudLayoutPolicy.Compute(2400,1080,new HudPixelRect(132,0,2268,1080),true);
            Assert.That(left.AnchorMinX,Is.EqualTo(132/2400f).Within(1e-5));Assert.That(left.AnchorMaxX,Is.EqualTo(1f).Within(1e-5));
            var right=HudLayoutPolicy.Compute(2400,1080,new HudPixelRect(0,0,2268,1080),true);
            Assert.That(right.AnchorMaxX,Is.EqualTo(2268/2400f).Within(1e-5));
            Assert.That(left.RailWidthUnits,Is.EqualTo(right.RailWidthUnits));
        }

        [Test] public void DrawerLeavesWorldVisibleOnEveryTarget()
        {
            foreach(var r in AllLayouts())
            {
                Assert.That(r.RailWidthUnits,Is.LessThanOrEqualTo(r.SafeWidthUnits*HudLayoutPolicy.MaxRailShare+.01f));
                Assert.That(r.FreeViewportFraction,Is.GreaterThanOrEqualTo(.5f),"Focus/camera free area");
            }
        }

        [Test] public void TabsAndHeaderFitTouchTargets()
        {
            foreach(var r in AllLayouts())
            {
                // Second tab row holds 4 tabs (Guide, Eggs, Manage, Log); width must stay >= 44 units.
                Assert.That(HudLayoutPolicy.TabCellWidth(r.RailWidthUnits,4),Is.GreaterThanOrEqualTo(HudLayoutPolicy.MinTouchTarget));
                // Header: brand (min 100) + gold 128 + language 108 + hide 70 + spacing 3*6 + padding 20, inside 12+12 margins.
                Assert.That(r.SafeWidthUnits-24,Is.GreaterThanOrEqualTo(100+128+108+70+18+20));
                Assert.That(r.UnitsToPixels(HudLayoutPolicy.MinTouchTarget),Is.GreaterThanOrEqualTo(44f));
            }
        }

        [Test] public void PhysicalTouchTargetHelper()
        {
            // 44 units on a 2400x1080 phone (scale 2) at 400 dpi ≈ 5.6 mm: reported, verified on device.
            Assert.That(HudLayoutPolicy.PhysicalMillimetres(44,2,400),Is.EqualTo(5.588f).Within(.01f));
            Assert.That(HudLayoutPolicy.PhysicalMillimetres(44,2,0),Is.Zero);
        }

        [Test] public void BorderValidation()
        {
            Assert.That(UiSpritePolicy.ValidateBorder(1024,384,new UiBorder(112,112,112,112),true),Is.Empty);
            Assert.That(UiSpritePolicy.ValidateBorder(1024,384,default,true),Has.Some.Contains("corners would stretch"));
            Assert.That(UiSpritePolicy.ValidateBorder(256,128,new UiBorder(64,64,64,64),true),Has.Some.Contains("top+bottom"));
            Assert.That(UiSpritePolicy.ValidateBorder(512,512,new UiBorder(8,0,0,0),false),Has.Some.Contains("non-sliced"));
            Assert.That(UiSpritePolicy.ValidateBorder(0,10,default,true),Has.Some.Contains("no pixels"));
        }

        [Test] public void MultiplierRendersBorderAtDesignSize()
        {
            // uGUI draws border/(spritePpu/refPpu*mult). The result must equal the design units.
            foreach(var (border,units,ppu) in new[]{(112f,16f,100f),(56f,16f,50f),(160f,24f,100f),(10f,10f,100f)})
            {
                var m=UiSpritePolicy.PixelsPerUnitMultiplier(border,units,ppu,100);
                Assert.That(border/(ppu/100f*m),Is.EqualTo(units).Within(1e-4));
            }
            Assert.That(UiSpritePolicy.PixelsPerUnitMultiplier(0,16),Is.EqualTo(1f));
        }

        [Test] public void ImportSizeNeverUpscalesAndStaysSmall()
        {
            // Non-sliced art 1024x384 shown 48 units tall: needs 128 px at scale 2.67 -> 512 (192 px tall).
            Assert.That(UiSpritePolicy.RecommendedMaxSize(1024,384,48),Is.EqualTo(512));
            // Icon 512 shown at most 64 units: 171 px -> 256.
            Assert.That(UiSpritePolicy.RecommendedMaxSize(512,512,64),Is.EqualTo(256));
            // Never above the source.
            Assert.That(UiSpritePolicy.RecommendedMaxSize(300,100,400),Is.EqualTo(512));
            Assert.That(UiSpritePolicy.SampleRatio(192,48,2.6667f),Is.GreaterThanOrEqualTo(1f));
            // Panel 1024 with 160 px corners shown at 24 units: corners need 64 px -> 512 import, not 2048.
            Assert.That(UiSpritePolicy.RecommendedMaxSizeSliced(1024,1024,new UiBorder(160,160,160,160),24,326,648),Is.EqualTo(512));
            // Divider: horizontal border only; height (10 units at 2.67 = 27 px) decides -> 128 (32 px tall).
            Assert.That(UiSpritePolicy.RecommendedMaxSizeSliced(512,128,new UiBorder(160,0,160,0),8,286,10),Is.EqualTo(128));
            Assert.That(UiSpritePolicy.AtlasPadding(false,false),Is.EqualTo(4));
            Assert.That(UiSpritePolicy.AtlasPadding(false,true),Is.EqualTo(8));
        }

        [Test] public void SlotManifestIsConsistent()
        {
            using var doc=JsonDocument.Parse(File.ReadAllText(Repo("Assets/Dravio/UI/Data/ui-sprite-slots.json")));
            var root=doc.RootElement;
            var roles=new HashSet<string>(Enum.GetNames(typeof(DravioUiRole)));
            var ids=new HashSet<string>();var paths=new HashSet<string>();
            foreach(var s in root.GetProperty("slots").EnumerateArray())
            {
                var id=s.GetProperty("id").GetString();var role=s.GetProperty("role").GetString();var path=s.GetProperty("path").GetString();
                Assert.That(ids.Add(id),Is.True,"duplicate id "+id);Assert.That(paths.Add(path),Is.True,"duplicate path "+path);
                Assert.That(roles,Does.Contain(role),id);
                Assert.That(path,Does.StartWith("Assets/Dravio/UI/Sprites/").And.EndWith(".png"));
                Assert.That(path,Does.Not.Contain("ArtSource"));
                Assert.That(Path.GetFileName(path),Does.Match("^ui_[a-z0-9_]+\\.png$"),"lower snake case");
                int w=s.GetProperty("sourceWidth").GetInt32(),h=s.GetProperty("sourceHeight").GetInt32();
                var b=s.GetProperty("borderPx").EnumerateArray().Select(x=>x.GetInt32()).ToArray();
                bool sliced=s.GetProperty("sliced").GetBoolean();
                Assert.That(sliced,Is.EqualTo(DravioUiRoles.IsSliced((DravioUiRole)Enum.Parse(typeof(DravioUiRole),role))),id+" sliced flag vs role");
                Assert.That(UiSpritePolicy.ValidateBorder(w,h,new UiBorder(b[0],b[1],b[2],b[3]),sliced),Is.Empty,id);
                float display=s.GetProperty("displayHeightUnits").GetSingle(),displayW=s.GetProperty("displayWidthUnits").GetSingle();
                float units=s.GetProperty("borderUnits").GetSingle();
                var border=new UiBorder(b[0],b[1],b[2],b[3]);
                int max=sliced?UiSpritePolicy.RecommendedMaxSizeSliced(w,h,border,units,displayW,display)
                    :UiSpritePolicy.RecommendedMaxSize(w,h,displayW>0?Math.Min(displayW,display):display);
                float k=Math.Min(1f,max/(float)Math.Max(w,h));
                Assert.That(max,Is.LessThanOrEqualTo(1024),id+" import too large for mobile UI");
                if(sliced)
                {
                    Assert.That(UiSpritePolicy.BorderSampleRatio((int)(border.MaxSide*k),units,UiSpritePolicy.MaxCanvasScale),Is.GreaterThanOrEqualTo(1f),id+" border upscaled");
                    // One import serves scales 1.0..2.67, so 720p desktop is always >= 2.67x; cap it at 3x for main surfaces.
                    if(role!="Divider")
                        Assert.That(UiSpritePolicy.BorderSampleRatio((int)(border.MaxSide*k),units,1f),Is.LessThanOrEqualTo(3f),id+" border aliasing at 720p");
                    if(border.Top+border.Bottom>0)Assert.That(units*2,Is.LessThanOrEqualTo(display+.01f),id+" borders taller than the element");
                    if(border.Left+border.Right>0 && displayW>0)Assert.That(units*2,Is.LessThanOrEqualTo(displayW+.01f),id+" borders wider than the element");
                }
                else
                {
                    float shortUnits=displayW>0?Math.Min(displayW,display):display;
                    Assert.That(UiSpritePolicy.SampleRatio((int)(Math.Min(w,h)*k),shortUnits,UiSpritePolicy.MaxCanvasScale),Is.GreaterThanOrEqualTo(1f),id+" upscaled");
                }
            }
            // Every role that a theme can draw has a slot (Icon is covered by the icon set).
            foreach(var role in roles.Where(r=>r!="Icon"))
                Assert.That(root.GetProperty("slots").EnumerateArray().Any(s=>s.GetProperty("role").GetString()==role),Is.True,role);
            Assert.That(root.GetProperty("icons").GetProperty("ids").GetArrayLength(),Is.EqualTo(32));
            Assert.That(root.GetProperty("items").GetProperty("ids").GetArrayLength(),Is.EqualTo(17));
        }

        [Test] public void VietnameseSetIsComplete()
        {
            var letters=VietnameseGlyphs.Letters;
            Assert.That(letters.Length,Is.EqualTo(134)); // 12 vowels x 6 tones x 2 cases - 12 ASCII + Đ/đ
            Assert.That(letters.Distinct().Count(),Is.EqualTo(letters.Length));
            foreach(var c in "ăâêôơưđĂÂÊÔƠƯĐàảãáạằẳẵắặầẩẫấậềểễếệìỉĩíịòỏõóọồổỗốộờởỡớợùủũúụừửữứựỳỷỹýỵ")
                Assert.That(VietnameseGlyphs.Required,Does.Contain(c.ToString()));
            Assert.That(VietnameseGlyphs.Missing("Rồng ăn",c=>c<128),Is.EqualTo(new[]{'ồ','ă'}));
            Assert.That(VietnameseGlyphs.HasCombiningMarks("Rồng"),Is.True);
            Assert.That(VietnameseGlyphs.HasCombiningMarks("Rồng"),Is.False);
        }

        [Test] public void HudCopyIsNfcAndCoveredByRequiredSet()
        {
            var files=new[]{"Assets/Dravio/Editor/FantasyPreviewHud.cs","Assets/Dravio/Scripts/FantasyPreviewUI.cs",
                "Assets/Dravio/Scripts/DragonCarePreview.cs","Assets/Dravio/Scripts/ResearchDiscoveryPanel.cs"};
            var required=new HashSet<char>(VietnameseGlyphs.Required);
            var outside=new SortedSet<char>();
            foreach(var f in files)
            {
                var text=File.ReadAllText(Repo(f));
                Assert.That(VietnameseGlyphs.HasCombiningMarks(text),Is.False,f+" has decomposed (NFD) Vietnamese");
                Assert.That(text.Normalize(NormalizationForm.FormC),Is.EqualTo(text),f);
                foreach(var c in StringLiterals(text))if(c>127 && !required.Contains(c))outside.Add(c);
            }
            Assert.That(outside,Is.Empty,"HUD glyphs missing from VietnameseGlyphs.Required: "+new string(outside.ToArray()));
        }

        static IEnumerable<char> StringLiterals(string text)
        {
            bool inside=false;
            for(int i=0;i<text.Length;i++)
            {
                char c=text[i];
                if(c=='"' && (i==0 || text[i-1]!='\\')){inside=!inside;continue;}
                if(c=='\n')inside=false;
                if(inside)yield return c;
            }
        }

        [Test] public void RoleTablesAgree()
        {
            Assert.That(DravioUiRoles.IsSliced(DravioUiRole.PrimaryButton),Is.True);
            Assert.That(DravioUiRoles.IsSliced(DravioUiRole.Icon),Is.False);
            Assert.That(DravioUiRoles.PreserveAspect(DravioUiRole.Icon),Is.True);
            foreach(DravioUiRole r in Enum.GetValues(typeof(DravioUiRole)))
                Assert.That(DravioUiRoles.IsSliced(r) && DravioUiRoles.PreserveAspect(r),Is.False,r+": sliced art cannot preserve aspect");
            // Serialized enum values are a contract with theme assets.
            Assert.That((int)DravioUiRole.Icon,Is.EqualTo(22));Assert.That((int)DravioUiRole.PrimaryButton,Is.Zero);
        }
    }
}
