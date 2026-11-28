using System;
using UnityEngine;
using UnityEngine.UI;
using Dravio.UI;

namespace Dravio
{
    // Preview presentation only. No player inventory or persistent progression.
    public sealed class FantasyPreviewUI : MonoBehaviour
    {
        [Serializable] public sealed class Copy
        {
            public Text label;
            public string en, vi;
        }

        [SerializeField] private RectTransform safeRoot, panel;
        [SerializeField] private GameObject[] pages;
        [SerializeField] private Button[] tabs;
        [SerializeField] private Button language, collapse;
        [SerializeField] private Text collapseLabel;
        [SerializeField] private Copy[] translations;
        [SerializeField] private DragonCarePreview care;
        [Tooltip("VIS.2a optional skin. Empty = U6.0.4b colours exactly as before.")]
        [SerializeField] private DravioUiTheme theme;
        private bool vi=true, collapsed;
        static readonly Color SurfaceInk=new Color(.075f,.13f,.12f,.97f);
        static readonly Color Ivory=new Color(.96f,.91f,.79f);
        static readonly Color Accent=new Color(.83f,.68f,.40f);
        static readonly Color ButtonInk=new Color(.10f,.18f,.16f);
        public bool DrawerClosed=>collapsed;
        public bool Vietnamese=>vi;
        public DravioUiTheme Theme=>theme;
        private LivingSanctuaryPresentation sanctuary;
        private Text worldHint;
        private Text journalLabel, nestLabel;
        private Button journalShortcut, nestShortcut;
        private RectTransform worldHeader, promptPlate;
        private float promptUntil;
        private int page;
        private int lastW=-1,lastH=-1;
        private Rect lastSafe;

        public void Configure(RectTransform safe, RectTransform details, GameObject[] bodies,
            Button[] nav, Button lang, Button toggle, Text toggleLabel, Copy[] copy, DragonCarePreview controller)
        {
            safeRoot=safe;panel=details;pages=bodies;tabs=nav;language=lang;
            collapse=toggle;collapseLabel=toggleLabel;translations=copy;care=controller;
        }

        /// <summary>Swap the VIS.2a theme at runtime (null restores the legacy palette on next scene load).</summary>
        public void SetTheme(DravioUiTheme value)
        {
            theme=value;
            if(theme!=null && panel!=null){ApplyArtDirection();ApplyVisibility();}
        }

        private void Start()
        {
            if(Application.isMobilePlatform)
            {
                Screen.autorotateToPortrait=false;
                Screen.autorotateToPortraitUpsideDown=false;
                Screen.autorotateToLandscapeLeft=true;
                Screen.autorotateToLandscapeRight=true;
                Screen.orientation=ScreenOrientation.AutoRotation;
            }
            for(int i=0;i<tabs.Length;i++)
            {
                int index=i;
                tabs[i].onClick.AddListener(()=>Select(index));
            }
            language.onClick.AddListener(()=>{vi=!vi;Localize();});
            collapse.onClick.AddListener(()=>{collapsed=!collapsed;ApplyVisibility();});
            RepairLayout();
            ApplyArtDirection();
            if(theme==null)DravioHudArt.Apply(safeRoot, panel, tabs, language, collapse);
            PrioritizeEggClaims();
            sanctuary=FindAnyObjectByType<LivingSanctuaryPresentation>();
            collapsed=sanctuary!=null && sanctuary.WorldInteractionAvailable;
            if(collapsed)
                foreach(var copy in translations)
                    if(copy.en=="DRAVIO"){worldHint=copy.label;break;}
            if(worldHint!=null && tabs.Length>=7)BuildWorldCorners();
            page=0;
            Localize();
            Layout();
        }

        // Build from the existing playable HUD so checked-in and generated scenes share the layout.
        private void BuildWorldCorners()
        {
            worldHeader=(RectTransform)worldHint.transform.parent;
            var brand=worldHint;
            var camp=Plate("Sanctuary name",new Color(.08f,.20f,.17f,.93f));
            Pin(camp,new Vector2(0,1),new Vector2(14,-14),new Vector2(132,46));
            brand.rectTransform.SetParent(camp,false);
            Fill(brand.rectTransform);
            brand.alignment=TextAnchor.MiddleCenter;
            brand.raycastTarget=false;

            promptPlate=Plate("World action feedback",new Color(.07f,.14f,.13f,.90f));
            worldHint=Instantiate(brand,promptPlate);
            Fill(worldHint.rectTransform);
            worldHint.fontSize=16;
            worldHint.resizeTextForBestFit=true;
            worldHint.resizeTextMinSize=12;
            worldHint.resizeTextMaxSize=16;
            promptUntil=Time.unscaledTime+8f;

            journalShortcut=CornerAction(6,"Field journal","biology",false,out journalLabel);
            nestShortcut=CornerAction(4,"Hatchery","eggs",true,out nestLabel);
        }

        private RectTransform Plate(string name,Color tint)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));
            go.transform.SetParent(safeRoot,false);
            var background=go.GetComponent<Image>();
            background.color=tint;
            background.raycastTarget=false;
            return (RectTransform)go.transform;
        }

        private Button CornerAction(int index,string name,string icon,bool right,out Text label)
        {
            var button=Instantiate(tabs[index],safeRoot);
            button.name=name;
            button.onClick=new Button.ButtonClickedEvent();
            button.onClick.AddListener(()=>Select(index));
            DravioHudArt.ApplyTab(button,false);
            DravioHudArt.AddIcon(button,icon,false);
            Pin((RectTransform)button.transform,new Vector2(right?1:0,0),
                new Vector2(right?-14:14,14),new Vector2(142,56));
            label=button.GetComponentInChildren<Text>(true);
            return button;
        }

        private static void Pin(RectTransform rt,Vector2 corner,Vector2 offset,Vector2 size)
        {
            rt.anchorMin=rt.anchorMax=rt.pivot=corner;
            rt.sizeDelta=size;
            rt.anchoredPosition=offset;
        }

        private static void Fill(RectTransform rt)
        {
            rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;
            rt.offsetMin=new Vector2(6,1);rt.offsetMax=new Vector2(-6,-1);
        }

        // Also repair already-generated scenes, without recreating their assets.
        private void RepairLayout()
        {
            if(panel==null)return;
            var navigation=panel.Find("Navigation");
            if(navigation!=null)
            {
                var row=navigation.GetComponent<LayoutElement>();
                if(row!=null){row.minHeight=40;row.preferredHeight=44;row.flexibleHeight=0;row.layoutPriority=10;}
                var group=navigation.GetComponent<HorizontalLayoutGroup>();
                if(group!=null)group.childForceExpandHeight=false;
            }
            var pageHost=panel.Find("Page host");
            if(pageHost!=null)
            {
                var size=pageHost.GetComponent<LayoutElement>();
                if(size!=null){size.minHeight=0;size.preferredHeight=0;size.flexibleHeight=1;size.layoutPriority=10;}
            }
            foreach(var child in panel.GetComponentsInChildren<LayoutElement>(true))
            {
                if(child.gameObject.name=="Page host")continue;
                if(child.preferredHeight>=0)child.flexibleHeight=0;
                if(child.gameObject.name=="Touch actions")
                {
                    child.minHeight=44;child.preferredHeight=48;
                    var group=child.GetComponent<HorizontalLayoutGroup>();
                    if(group!=null)group.childForceExpandHeight=false;
                    foreach(var button in child.GetComponentsInChildren<Button>(true))
                    {
                        var size=button.GetComponent<LayoutElement>();
                        if(size!=null){size.minHeight=44;size.preferredHeight=48;size.flexibleHeight=0;}
                    }
                }
            }
            if(pages!=null && pages.Length>0 && pages[0]!=null)
            {
                var scroll=pages[0].GetComponent<ScrollRect>();
                if(scroll!=null && scroll.content!=null)
                {
                    // Prioritize actionable controls above the session notes.
                    foreach(Transform child in scroll.content)
                    {
                        var label=child.GetComponent<Text>();
                        var size=child.GetComponent<LayoutElement>();
                        if(label!=null && size!=null && size.preferredHeight>=48)
                            size.preferredHeight=Mathf.Min(size.preferredHeight,36);
                    }
                    var layout=scroll.content.GetComponent<VerticalLayoutGroup>();
                    if(layout!=null)layout.spacing=4;
                }
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
        }

        // Runtime presentation migration: keep serialized button targets, text and
        // localization intact, including scenes authored before this update.
        private void ApplyArtDirection()
        {
            if(panel==null)return;
            if(theme!=null){ApplyTheme();return;}
            var surface=panel.GetComponent<Image>();
            if(surface!=null)surface.color=SurfaceInk;
            var header=safeRoot!=null?safeRoot.Find("Header"):null;
            if(header!=null && header.TryGetComponent<Image>(out var headerImage))headerImage.color=SurfaceInk;
            var layout=panel.GetComponent<VerticalLayoutGroup>();
            if(layout!=null){layout.spacing=6;layout.padding=new RectOffset(10,10,8,8);}
            foreach(var button in safeRoot.GetComponentsInChildren<Button>(true))
            {
                var colors=button.colors;
                colors.normalColor=Accent;
                colors.highlightedColor=new Color(.96f,.83f,.56f);
                colors.pressedColor=new Color(.66f,.52f,.29f);
                colors.selectedColor=colors.highlightedColor;
                colors.disabledColor=new Color(.42f,.43f,.38f,.68f);
                colors.fadeDuration=.10f;
                button.colors=colors;
                var label=button.GetComponentInChildren<Text>(true);
                if(label!=null){label.color=ButtonInk;label.fontStyle=FontStyle.Bold;}
            }
            foreach(var text in panel.GetComponentsInChildren<Text>(true))
            {
                if(text.GetComponentInParent<Button>()!=null)continue;
                // Maintain the original visual hierarchy: gold titles, ivory body, muted notes.
                if(text.fontSize>=18){text.color=Accent;text.fontStyle=FontStyle.Bold;}
                else if(text.fontSize>=15)text.color=Ivory;
            }
            foreach(var page in pages)
            {
                if(page==null)continue;
                var scroll=page.GetComponent<ScrollRect>();
                if(scroll!=null && scroll.viewport!=null && scroll.viewport.TryGetComponent<Image>(out var img))
                    img.color=new Color(.035f,.075f,.065f,.65f);
            }
        }
        // VIS.2a: same element selection and text hierarchy as ApplyArtDirection, drawn from the theme.
        private void ApplyTheme()
        {
            DravioUiSkin.ApplySurface(panel.GetComponent<Image>(),theme,DravioUiRole.Panel);
            var header=safeRoot!=null?safeRoot.Find("Header"):null;
            if(header!=null && header.TryGetComponent<Image>(out var headerImage))
                DravioUiSkin.ApplySurface(headerImage,theme,DravioUiRole.HeaderBar);
            var layout=panel.GetComponent<VerticalLayoutGroup>();
            if(layout!=null){layout.spacing=6;layout.padding=new RectOffset(10,10,8,8);}
            foreach(var button in safeRoot.GetComponentsInChildren<Button>(true))
            {
                if(Array.IndexOf(tabs,button)>=0)continue; // ApplyVisibility owns tab state.
                DravioUiSkin.ApplyButton(button,theme,RoleFor(button));
            }
            foreach(var text in panel.GetComponentsInChildren<Text>(true))
            {
                if(text.GetComponentInParent<Button>()!=null)continue;
                if(text.fontSize>=18)DravioUiSkin.ApplyText(text,theme,DravioTextRole.Title);
                else if(text.fontSize>=15)DravioUiSkin.ApplyText(text,theme,DravioTextRole.Body);
            }
            foreach(var page in pages)
            {
                if(page==null)continue;
                var scroll=page.GetComponent<ScrollRect>();
                if(scroll!=null && scroll.viewport!=null && scroll.viewport.TryGetComponent<Image>(out var img))
                    DravioUiSkin.ApplySurface(img,theme,DravioUiRole.PageBackground);
            }
        }

        private DravioUiRole RoleFor(Button button)
        {
            if(button==language || button==collapse)return DravioUiRole.SecondaryButton;
            // Egg pack claims are the HUD's reward actions (object names are the builder's English labels).
            if(button.name.StartsWith("Claim",StringComparison.Ordinal))return DravioUiRole.RewardButton;
            return DravioUiRole.PrimaryButton;
        }

        private void PrioritizeEggClaims()
        {
            if(pages==null || pages.Length<5 || pages[4]==null)return;
            var scroll=pages[4].GetComponent<ScrollRect>();
            if(scroll==null || scroll.content==null)return;
            var meadow=scroll.content.Find("Claim pack to meadow");
            var storage=scroll.content.Find("Claim pack to storage");
            if(meadow==null || storage==null)return;
            // After the pending-pack status and feedback, before the purchase catalogue.
            meadow.SetSiblingIndex(2);
            storage.SetSiblingIndex(3);
            LayoutRebuilder.MarkLayoutForRebuild(scroll.content);
        }
        private void Update()
        {
            if(worldHint!=null && collapsed)
            {
                string feedback=sanctuary.WorldFeedback(vi);
                string label=string.IsNullOrEmpty(feedback)?WorldPrompt():feedback;
                if(promptPlate!=null)promptPlate.gameObject.SetActive(Time.unscaledTime<promptUntil || !string.IsNullOrEmpty(feedback));
                if(worldHint.text!=label)worldHint.text=label;
            }
            if(Screen.width!=lastW || Screen.height!=lastH || HudSafeAreaOverride.Current!=lastSafe)Layout();
        }

        private void Layout()
        {
            lastW=Screen.width;lastH=Screen.height;lastSafe=HudSafeAreaOverride.Current;
            // Geometry lives in HudLayoutPolicy (VIS.2a, dotnet-tested); numbers unchanged from U6.0.4b.
            // Both desktop and phone use the same landscape rail; compact reference on mobile for touch comfort.
            // A narrow Editor window retains the rail; it is not a portrait mobile mode.
            var result=HudLayoutPolicy.Compute(lastW,lastH,
                new HudPixelRect(lastSafe.x,lastSafe.y,lastSafe.width,lastSafe.height),Application.isMobilePlatform);
            safeRoot.anchorMin=new Vector2(result.AnchorMinX,result.AnchorMinY);
            safeRoot.anchorMax=new Vector2(result.AnchorMaxX,result.AnchorMaxY);
            safeRoot.offsetMin=Vector2.zero;safeRoot.offsetMax=Vector2.zero;
            var scaler=GetComponent<CanvasScaler>();
            if(scaler!=null)
            {
                scaler.referenceResolution=new Vector2(result.ReferenceWidth,result.ReferenceHeight);
                scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight=1;
            }
            if(worldHeader!=null)
            {
                Pin(worldHeader,Vector2.one,new Vector2(-14,-14),new Vector2(344,48));
                Pin(promptPlate,new Vector2(.5f,0),new Vector2(0,82),
                    new Vector2(Mathf.Min(430,result.SafeWidthUnits-28),46));
            }
            panel.anchorMin=new Vector2(1,0);
            panel.anchorMax=new Vector2(1,1);
            panel.offsetMin=new Vector2(-result.RailWidthUnits,HudLayoutPolicy.RailMargin);
            panel.offsetMax=new Vector2(-HudLayoutPolicy.RailMargin,-HudLayoutPolicy.RailTop);
        }

        private void Select(int index)
        {
            page=Mathf.Clamp(index,0,pages.Length-1);
            collapsed=false;
            ApplyVisibility();
        }

        string WorldPrompt()=>vi?"Chạm đất để cho ăn · Chạm rồng để vuốt ve":"Tap ground to feed · Tap a dragon to pet";

        private void ApplyVisibility()
        {
            panel.gameObject.SetActive(!collapsed);
            for(int i=0;i<pages.Length;i++)
            {
                pages[i].SetActive(i==page);
                if(theme!=null)DravioUiSkin.ApplyTab(tabs[i],theme,i==page);
                else DravioHudArt.ApplyTab(tabs[i],i==page);
            }
            if(journalShortcut!=null)
            {
                journalShortcut.gameObject.SetActive(collapsed);
                nestShortcut.gameObject.SetActive(collapsed);
                journalLabel.text=vi?"Sổ tay":"Journal";
                nestLabel.text=vi?"Tổ trứng":"Hatchery";
                if(!collapsed)promptPlate.gameObject.SetActive(false);
            }
            if(worldHint!=null)worldHint.text=collapsed?WorldPrompt():"DRAVIO";
            if(collapseLabel!=null)collapseLabel.text=collapsed?"Menu":(vi?"Ẩn":"Hide");
        }

        private void Localize()
        {
            foreach(var item in translations)
                if(item!=null && item.label!=null)item.label.text=vi?item.vi:item.en;
            if(care!=null)care.SetVietnamese(vi);
            ApplyVisibility();
        }
    }
}
