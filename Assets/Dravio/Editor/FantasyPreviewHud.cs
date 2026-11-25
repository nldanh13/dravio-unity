using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using Object = UnityEngine.Object;

namespace Dravio.Editor
{
    internal static class FantasyPreviewHud
    {
        [Serializable] private class BiologyRoot { public Biology emberling; }
        [Serializable] private class Biology
        {
            public string group,respiration,protection,activity,silhouetteVi;
            public string[] locomotion,dietIds,thermalBands,habitatRules;
        }

        private static readonly Color Ink=new Color(.15f,.11f,.07f);
        private static readonly Color Wood=new Color(.19f,.15f,.10f,.96f);
        private static readonly Color Gold=new Color(.91f,.78f,.51f);
        private static readonly Color Paper=new Color(.95f,.88f,.72f);
        private static readonly Color Muted=new Color(.76f,.70f,.56f);
        private sealed class Layout
        {
            public Font font;
            public readonly List<FantasyPreviewUI.Copy> copy=new();
        }

        public static void Build(GameObject dragon,Animator animator,DragonTurntable turntable)
        {
            var asset=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Dravio/Data/species-biology.json");
            if(asset==null)throw new InvalidOperationException("Canonical biology JSON missing.");
            var biology=JsonUtility.FromJson<BiologyRoot>(asset.text)?.emberling;
            if(biology==null || biology.locomotion==null || biology.dietIds==null ||
                biology.thermalBands==null || biology.habitatRules==null)
                throw new InvalidOperationException("Emberling biology incomplete.");
            // On this new scene only, replace the old visual placeholder HUD.
            foreach(var old in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if(old.renderMode!=RenderMode.WorldSpace)old.enabled=false;
            var canvasGo=new GameObject("Dravio Fantasy HUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            var canvas=canvasGo.GetComponent<Canvas>();
            canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=20;
            var scaler=canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1280,720);
            scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            EnsureEventSystem();
            var ui=new Layout {font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")};
            var safe=RectNode("Safe area",canvasGo.transform);
            Stretch(safe,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            var header=Surface("Header",safe,Wood);
            Stretch(header,new Vector2(0,1),Vector2.one,new Vector2(12,-60),new Vector2(-12,-12));
            var bar=header.gameObject.AddComponent<HorizontalLayoutGroup>();
            bar.padding=new RectOffset(12,8,5,5);bar.spacing=6;bar.childAlignment=TextAnchor.MiddleLeft;
            bar.childControlWidth=true;bar.childControlHeight=true;bar.childForceExpandHeight=true;bar.childForceExpandWidth=false;
            var brand=Label(ui,header,"DRAVIO","DRAVIO",23,Gold,TextAnchor.MiddleLeft,38);
            brand.GetComponent<LayoutElement>().flexibleWidth=1;
            var lang=Button(ui,header,"Tiếng Việt","English",38,108);
            var hide=Button(ui,header,"Hide","Ẩn",38,70);
            var panel=Surface("Sanctuary drawer",safe,Wood);
            Stretch(panel,new Vector2(1,0),new Vector2(1,1),new Vector2(-326,12),new Vector2(-12,-72));
            var vertical=panel.gameObject.AddComponent<VerticalLayoutGroup>();
            vertical.padding=new RectOffset(10,10,10,10);vertical.spacing=8;
            vertical.childControlHeight=true;vertical.childControlWidth=true;
            vertical.childForceExpandHeight=false;vertical.childForceExpandWidth=true;

            var identity=Label(ui,panel,"Emberling • Baby / Normal","Emberling • Rồng non / Thường",20,Gold,TextAnchor.MiddleLeft,40);
            var status=Label(ui,panel,"Resting","Đang nghỉ",16,Paper,TextAnchor.MiddleLeft,30,false);
            var tabsRoot=RectNode("Navigation",panel);
            var tabLayout=tabsRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabLayout.spacing=5;tabLayout.childControlWidth=true;tabLayout.childControlHeight=true;
            tabLayout.childForceExpandWidth=true;tabLayout.childForceExpandHeight=true;
            tabsRoot.gameObject.AddComponent<LayoutElement>().preferredHeight=44;
            var tabs=new[]{Button(ui,tabsRoot,"Care","Chăm sóc",44),Button(ui,tabsRoot,"Biology","Sinh học",44),Button(ui,tabsRoot,"Dragons","Rồng",44)};
            var host=RectNode("Page host",panel);
            host.gameObject.AddComponent<LayoutElement>().flexibleHeight=1;
            var care=Page(host,"Care");
            var info=Page(host,"Biology");
            var roster=Page(host,"Dragons");

            Label(ui,care,"Origin Sanctuary","Thánh địa Khởi Nguyên",17,Gold,TextAnchor.MiddleLeft,35);
            Label(ui,care,"Visual actions only. No food or bond changes yet.","Chỉ thử hoạt ảnh. Chưa trừ thức ăn hay thay đổi gắn kết.",14,Muted,TextAnchor.MiddleLeft,54);
            var feed=Button(ui,care,"Feed • F","Cho ăn • F",46);
            var pet=Button(ui,care,"Pet • P","Vuốt ve • P",46);
            var sleep=Button(ui,care,"Doze / wake • N","Ngủ gật / thức • N",46);
            var inspect=Button(ui,care,"Inspect / resume • Space","Quan sát / tiếp tục • Space",46);
            var resume=Button(ui,care,"Resume exploring","Tiếp tục khám phá",46);
            Label(ui,care,"Inspect: drag the dragon or use arrow keys. R resets rotation.","Quan sát: kéo rồng hoặc dùng phím mũi tên. R đặt lại góc.",14,Muted,TextAnchor.MiddleLeft,60);

            Label(ui,info,"Species reference","Hồ sơ loài",18,Gold,TextAnchor.MiddleLeft,36);
            Fact(ui,info,"Group","Nhóm",biology.group,biology.group);
            Fact(ui,info,"Respiration","Hô hấp",biology.respiration,Translate(biology.respiration));
            Fact(ui,info,"Locomotion","Di chuyển",Join(biology.locomotion,false),Join(biology.locomotion,true));
            Fact(ui,info,"Protection","Bảo vệ",biology.protection,Translate(biology.protection));
            Fact(ui,info,"Activity","Hoạt động",biology.activity,Translate(biology.activity));
            Fact(ui,info,"Diet","Thức ăn",Join(biology.dietIds,false),Join(biology.dietIds,true));
            Fact(ui,info,"Temperature","Nhiệt độ",Join(biology.thermalBands,false),Join(biology.thermalBands,true));
            Fact(ui,info,"Habitats","Sinh cảnh",Join(biology.habitatRules,false),Join(biology.habitatRules,true));
            Label(ui,info,"Four legs, round flame tail, no wings.",biology.silhouetteVi,15,Paper,TextAnchor.MiddleLeft,62);
            Label(ui,info,"Reference data, not live health or comfort.","Dữ liệu loài, không phải chỉ số sức khỏe hay tiện nghi hiện tại.",13,Muted,TextAnchor.MiddleLeft,56);

            Label(ui,roster,"Preview residents","Rồng trong bản thử",18,Gold,TextAnchor.MiddleLeft,36);
            Label(ui,roster,"Emberling • Baby / Normal","Emberling • Rồng non / Thường",17,Paper,TextAnchor.MiddleLeft,42);
            var rosterInspect=Button(ui,roster,"Inspect Emberling","Quan sát Emberling",46);
            Label(ui,roster,"Only the current preview dragon is available. The full roster awaits Core Domain integration.","Hiện chỉ có rồng thử nghiệm. Danh sách rồng thật sẽ được nối với Core Domain.",14,Muted,TextAnchor.MiddleLeft,90);

            var controller=dragon.AddComponent<DragonCarePreview>();
            controller.Configure(animator,turntable,status,feed,pet,sleep,inspect,resume);
            var runtime=canvasGo.AddComponent<FantasyPreviewUI>();
            // persistent callbacks survive editor scene serialization.
            UnityEditor.Events.UnityEventTools.AddPersistentListener(rosterInspect.onClick,controller.ToggleInspect);
            runtime.Configure(safe,panel,new[]{care.parent.parent.gameObject,info.parent.parent.gameObject,roster.parent.parent.gameObject},
                tabs,lang,hide,hide.GetComponentInChildren<Text>(),ui.copy.ToArray(),controller);
        }

        static void EnsureEventSystem()
        {
            var e=Object.FindFirstObjectByType<EventSystem>();
            if(e==null)e=new GameObject("Fantasy EventSystem",typeof(EventSystem)).GetComponent<EventSystem>();
            foreach(var m in e.GetComponents<BaseInputModule>())
                if(!(m is InputSystemUIInputModule))m.enabled=false;
            var input=e.GetComponent<InputSystemUIInputModule>();
            if(input==null)input=e.gameObject.AddComponent<InputSystemUIInputModule>();
            input.enabled=true;
            if(input.actionsAsset==null)input.AssignDefaultActions();
        }

        static RectTransform RectNode(string name,Transform parent)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);
            return go.GetComponent<RectTransform>();
        }
        static RectTransform Surface(string name,Transform parent,Color c)
        {
            var r=RectNode(name,parent);r.gameObject.AddComponent<Image>().color=c;return r;
        }
        static void Stretch(RectTransform r,Vector2 min,Vector2 max,Vector2 offsetMin,Vector2 offsetMax)
        {r.anchorMin=min;r.anchorMax=max;r.offsetMin=offsetMin;r.offsetMax=offsetMax;}

        static RectTransform Page(Transform host,string name)
        {
            var page=RectNode(name,host);
            Stretch(page,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            var scroll=page.gameObject.AddComponent<ScrollRect>();
            var viewport=Surface("Viewport",page,new Color(.13f,.10f,.07f,.20f));
            Stretch(viewport,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            viewport.gameObject.AddComponent<RectMask2D>();
            var body=RectNode("Content",viewport);
            body.anchorMin=new Vector2(0,1);body.anchorMax=new Vector2(1,1);
            body.pivot=new Vector2(.5f,1);body.anchoredPosition=Vector2.zero;body.sizeDelta=Vector2.zero;
            var v=body.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding=new RectOffset(7,7,5,9);v.spacing=6;v.childControlWidth=true;v.childControlHeight=true;
            v.childForceExpandHeight=false;v.childForceExpandWidth=true;
            var fit=body.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport=viewport;scroll.content=body;scroll.horizontal=false;
            scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=24;
            return body;
        }
        static Text Label(Layout ui,Transform parent,string en,string vi,int size,Color color,TextAnchor align,float height,bool localize=true)
        {
            var r=RectNode("Text",parent);var text=r.gameObject.AddComponent<Text>();
            text.font=ui.font;text.text=vi;text.fontSize=size;text.color=color;text.alignment=align;
            text.raycastTarget=false;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;
            text.resizeTextForBestFit=true;text.resizeTextMinSize=12;text.resizeTextMaxSize=size;
            r.gameObject.AddComponent<LayoutElement>().preferredHeight=height;
            if(localize)ui.copy.Add(new FantasyPreviewUI.Copy{label=text,en=en,vi=vi});
            return text;
        }
        static Button Button(Layout ui,Transform parent,string en,string vi,float height,float width=-1)
        {
            var r=Surface(en,parent,Color.white);var b=r.gameObject.AddComponent<Button>();b.targetGraphic=r.GetComponent<Image>();
            var colors=b.colors;colors.normalColor=Gold;colors.highlightedColor=new Color(1,.90f,.65f);
            colors.pressedColor=new Color(.66f,.54f,.34f);colors.selectedColor=colors.highlightedColor;b.colors=colors;
            var nav=b.navigation;nav.mode=Navigation.Mode.None;b.navigation=nav;
            var size=r.gameObject.AddComponent<LayoutElement>();size.preferredHeight=height;size.preferredWidth=width;
            if(width<0)size.flexibleWidth=1;
            var label=Label(ui,r,en,vi,16,Ink,TextAnchor.MiddleCenter,height);
            Stretch(label.rectTransform,Vector2.zero,Vector2.one,new Vector2(5,2),new Vector2(-5,-2));
            return b;
        }
        static void Fact(Layout ui,Transform parent,string en,string vi,string valueEn,string valueVi)
            =>Label(ui,parent,en+"  •  "+valueEn,vi+"  •  "+valueVi,15,Paper,TextAnchor.MiddleLeft,48);
        static string Join(string[] values,bool vi)=>string.Join(", ",values.Select(v=>vi?Translate(v):v));
        static string Translate(string value)=>value switch {
            "lung"=>"phổi","walk"=>"đi bộ","scale"=>"vảy","day"=>"ban ngày",
            "fruit"=>"trái cây","aether"=>"Aether","mild"=>"ôn hòa","warm"=>"ấm","hot"=>"nóng",
            "origin_meadow"=>"Đồng cỏ Khởi Nguyên","origin_warm_rocks"=>"Bãi đá ấm Khởi Nguyên",
            _=>value
        };
    }
}
