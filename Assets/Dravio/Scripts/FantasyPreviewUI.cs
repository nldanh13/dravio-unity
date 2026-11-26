using System;
using UnityEngine;
using UnityEngine.UI;

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
        private bool vi=true, collapsed;
        static readonly Color SurfaceInk=new Color(.075f,.13f,.12f,.97f);
        static readonly Color Ivory=new Color(.96f,.91f,.79f);
        static readonly Color Accent=new Color(.83f,.68f,.40f);
        static readonly Color ButtonInk=new Color(.10f,.18f,.16f);
        public bool Vietnamese=>vi;
        private int page;
        private int lastW=-1,lastH=-1;
        private Rect lastSafe;

        public void Configure(RectTransform safe, RectTransform details, GameObject[] bodies,
            Button[] nav, Button lang, Button toggle, Text toggleLabel, Copy[] copy, DragonCarePreview controller)
        {
            safeRoot=safe;panel=details;pages=bodies;tabs=nav;language=lang;
            collapse=toggle;collapseLabel=toggleLabel;translations=copy;care=controller;
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
            DravioHudArt.Apply(safeRoot, panel, tabs, language, collapse);
            PrioritizeEggClaims();
            Select(0);
            Localize();
            Layout();
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
            if(Screen.width!=lastW || Screen.height!=lastH || Screen.safeArea!=lastSafe)Layout();
        }

        private void Layout()
        {
            lastW=Screen.width;lastH=Screen.height;lastSafe=Screen.safeArea;
            float w=Mathf.Max(1,lastW),h=Mathf.Max(1,lastH);
            Rect safe=lastSafe.width>0 && lastSafe.height>0?lastSafe:new Rect(0,0,w,h);
            safeRoot.anchorMin=new Vector2(safe.xMin/w,safe.yMin/h);
            safeRoot.anchorMax=new Vector2(safe.xMax/w,safe.yMax/h);
            safeRoot.offsetMin=Vector2.zero;safeRoot.offsetMax=Vector2.zero;
            // Both desktop and phone use the same landscape rail.
            // Scale against a shorter reference height on mobile for touch comfort.
            // A narrow Editor window retains the rail; it is not a portrait mobile mode.
            bool compact=Application.isMobilePlatform || h<600;
            var scaler=GetComponent<CanvasScaler>();
            if(scaler!=null)
            {
                scaler.referenceResolution=compact?new Vector2(960,540):new Vector2(1280,720);
                scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight=1;
            }
            float scale= h/(compact?540f:720f);
            float safeWidth=safe.width/Mathf.Max(.01f,scale);
            float railWidth=Mathf.Min(compact?Mathf.Clamp(safeWidth*.36f,280f,380f):326f,safeWidth*.48f);
            panel.anchorMin=new Vector2(1,0);
            panel.anchorMax=new Vector2(1,1);
            panel.offsetMin=new Vector2(-railWidth,12);
            panel.offsetMax=new Vector2(-12,-72);
        }

        private void Select(int index)
        {
            page=Mathf.Clamp(index,0,pages.Length-1);
            collapsed=false;
            ApplyVisibility();
        }

        private void ApplyVisibility()
        {
            panel.gameObject.SetActive(!collapsed);
            for(int i=0;i<pages.Length;i++)
            {
                pages[i].SetActive(i==page);
                var colors=tabs[i].colors;
                colors.normalColor=i==page?Accent:new Color(.19f,.29f,.25f);
                colors.highlightedColor=i==page?new Color(.96f,.83f,.56f):new Color(.29f,.40f,.34f);
                colors.selectedColor=colors.normalColor;
                tabs[i].colors=colors;
                var label=tabs[i].GetComponentInChildren<Text>(true);
                if(label!=null)label.color=i==page?ButtonInk:Ivory;
                DravioHudArt.ApplyTab(tabs[i],i==page);
            }
            if(collapseLabel!=null)collapseLabel.text=collapsed?(vi?"Mở":"Show"):(vi?"Ẩn":"Hide");
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
