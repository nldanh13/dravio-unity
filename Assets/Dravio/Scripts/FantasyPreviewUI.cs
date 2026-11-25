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
            for(int i=0;i<tabs.Length;i++)
            {
                int index=i;
                tabs[i].onClick.AddListener(()=>Select(index));
            }
            language.onClick.AddListener(()=>{vi=!vi;Localize();});
            collapse.onClick.AddListener(()=>{collapsed=!collapsed;ApplyVisibility();});
            Select(0);
            Localize();
            Layout();
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
            // Landscape: right rail. Portrait: lower drawer; scroll long content.
            bool portrait=safe.width<safe.height;
            var scaler=GetComponent<CanvasScaler>();
            if(scaler!=null)
            {
                scaler.referenceResolution=portrait?new Vector2(420,800):new Vector2(1280,720);
                scaler.screenMatchMode=portrait?CanvasScaler.ScreenMatchMode.MatchWidthOrHeight:CanvasScaler.ScreenMatchMode.Expand;
                scaler.matchWidthOrHeight=0;
            }
            panel.anchorMin=portrait?new Vector2(0,0):new Vector2(1,0);
            panel.anchorMax=portrait?new Vector2(1,.48f):new Vector2(1,1);
            panel.offsetMin=portrait?new Vector2(12,12):new Vector2(-326,12);
            panel.offsetMax=portrait?new Vector2(-12,-12):new Vector2(-12,-72);
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
                colors.normalColor=i==page?new Color(.88f,.73f,.43f):new Color(.57f,.48f,.32f);
                tabs[i].colors=colors;
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
