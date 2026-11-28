using System.Collections.Generic;
using Dravio.Domain;
using Dravio.Domain.Core;
using Dravio.Platform;
using Dravio.Platform.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace Dravio
{
    /// <summary>
    /// U6.3 — the island reads like a dragon-park game instead of a tabbed panel:
    /// - the drawer starts closed and its tab row gives way to round buttons along the bottom of the island
    ///   (tap one to open that page, tap it again to go back to the island; a dot marks the journey's page);
    /// - a small bubble above each dragon: name, growth, and "!" for care, shelter or the journey's next step;
    ///   tap a bubble to select that dragon and open Care;
    /// - "+N" gold rises from the dragons that produced it (the real online gold of each TimeAdvanced,
    ///   split by production rate, see <see cref="GoldPopupLedger"/>);
    /// - <see cref="IslandCamera"/>: closer default view, drag to look around, pinch/wheel to zoom.
    /// Read-only: sends no command. Installs itself after the first scene load like <see cref="JourneyBanner"/>.
    /// </summary>
    public sealed class IslandHud : MonoBehaviour
    {
        static readonly Color Ink=new Color(.075f,.13f,.12f,.90f);
        static readonly Color Ivory=new Color(.96f,.91f,.79f);
        static readonly Color Gold=new Color(.83f,.68f,.40f);
        static readonly Color Alert=new Color(.92f,.36f,.26f);
        static readonly Color Coin=new Color(1f,.86f,.35f);
        static readonly Color DockIdle=new Color(.16f,.27f,.23f,.96f);
        static readonly Color ButtonInk=new Color(.10f,.18f,.16f);
        const int MaxBubbles=16,MaxPopups=12;
        const float DockSize=60,DockGap=6,PopupSeconds=1.4f,PopupRise=46;

        FantasyPreviewUI ui;
        DravioSessionHost host;
        LivingSanctuaryPresentation living;
        JourneyBanner banner;
        IslandCamera islandCamera;
        GameSession subscribed;
        Font font;
        Sprite circle,rounded;
        RectTransform layer,dock;
        Canvas canvas;
        bool lastVi=true,introDone;
        int startFrame;
        GameState bubbleState;
        string bubbleFocus;
        readonly GoldPopupLedger ledger=new GoldPopupLedger();
        readonly List<KeyValuePair<string,Vector3>> anchors=new List<KeyValuePair<string,Vector3>>();
        readonly Dictionary<string,Vector3> anchorByUid=new Dictionary<string,Vector3>();
        readonly Dictionary<string,IslandBubble> info=new Dictionary<string,IslandBubble>();
        readonly List<Bubble> bubbles=new List<Bubble>();
        readonly List<Popup> popups=new List<Popup>();
        readonly List<DockButton> buttons=new List<DockButton>();
        HabitatPlots plots;
        readonly List<Chip> chips=new List<Chip>();
        sealed class Chip{public RectTransform rect;public Image stripe;public Text text;}

        sealed class Bubble{public RectTransform rect;public Text name,stage,mark;public Image markBg;public string uid;}
        sealed class Popup{public RectTransform rect;public Text text;public string uid;public float born=-1;}
        sealed class DockButton{public IslandDock.Item item;public Image bg;public Text label;public GameObject dot;}

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(Object.FindObjectsByType<IslandHud>(FindObjectsInactive.Include).Length>0)return;
            var hosts=Object.FindObjectsByType<DravioSessionHost>(FindObjectsInactive.Exclude);
            var huds=Object.FindObjectsByType<FantasyPreviewUI>(FindObjectsInactive.Exclude);
            if(hosts.Length!=1 || huds.Length!=1 || huds[0].PageCount<=(int)HudPage.Research || huds[0].SafeRoot==null)return;
            var hud=huds[0].gameObject.AddComponent<IslandHud>();
            hud.ui=huds[0];hud.host=hosts[0];
            var presenters=Object.FindObjectsByType<LivingSanctuaryPresentation>(FindObjectsInactive.Include);
            hud.living=presenters.Length>0?presenters[0]:null;
        }

        void Start()
        {
            if(ui==null||host==null){enabled=false;return;}
            canvas=ui.GetComponentInParent<Canvas>();
            font=FindFont();
            circle=MakeSprite(128,64,0);
            rounded=MakeSprite(48,14,14);
            islandCamera=gameObject.AddComponent<IslandCamera>();
            plots=gameObject.AddComponent<HabitatPlots>();
            plots.living=living;plots.host=host;
            BuildLayer();
            BuildDock();
            dock.SetSiblingIndex(layer.GetSiblingIndex()+1); // card, toasts, modals and the book stay above it
            startFrame=Time.frameCount;
            lastVi=ui.Vietnamese;
            Subscribe();
        }

        void Subscribe()
        {
            var s=host!=null?host.Session:null;
            if(s==subscribed)return;
            if(subscribed!=null)subscribed.DomainEvents-=OnEvents;
            subscribed=s;
            if(s!=null)s.DomainEvents+=OnEvents;
        }
        void OnDestroy(){if(subscribed!=null)subscribed.DomainEvents-=OnEvents;if(living!=null && living.LabelCovered==(System.Func<string,bool>)Covered)living.LabelCovered=null;}

        // Gold is credited against the producers of the state it was earned in (the state before this batch
        // is no longer available, so the current one is used: the same dragons unless the batch moved them).
        void OnEvents(IReadOnlyList<DomainEvent> events)
        {
            var s=subscribed?.State;
            foreach(var e in events)
                if(e is TimeAdvanced t && t.Mode==AdvanceMode.Online && t.GoldMicro>0)
                    ledger.Earn(t.GoldMicro,GoldPopupLedger.Producers(s));
        }

        void Update()
        {
            if(host==null)return;
            Subscribe();
            if(banner==null)banner=GetComponent<JourneyBanner>();
            // After the HUD's own Start has selected its first page: hide the tab row and open on the island.
            if(!introDone && Time.frameCount>startFrame+1)
            {
                introDone=true;
                // Both tab rows of the playable HUD (the second one holds Guide/Eggs/Manage/Log).
                foreach(var row in new[]{"Navigation","Management navigation"})
                {
                    var nav=ui.Panel!=null?ui.Panel.Find(row):null;
                    if(nav!=null)nav.gameObject.SetActive(false);
                }
                ui.SetCollapsed(true);
            }
            if(ui.Vietnamese!=lastVi){lastVi=ui.Vietnamese;bubbleState=null;foreach(var b in buttons)b.label.text=b.item.Label(lastVi);}
            RefreshDock();
        }

        void LateUpdate()
        {
            if(layer==null)return;
            var s=subscribed?.State;
            // Not gated on Hidden: in the Editor, focusing another window hides the session and would blank the
            // bubbles and chips while the island is still on screen. The sim is paused then; the overlay just stays.
            bool ready=s!=null && subscribed.Status==SessionStatus.Ready;
            layer.gameObject.SetActive(ready);
            if(!ready)return;
            if(living!=null)living.ResidentAnchors(anchors);else anchors.Clear();
            if(anchors.Count==0)LegacyAnchor(s);
            anchorByUid.Clear();
            foreach(var a in anchors)anchorByUid[a.Key]=a.Value;
            string focus=banner!=null && banner.Current!=null?banner.Current.FocusUid:null;
            bool missing=false;
            foreach(var a in anchors)if(!info.ContainsKey(a.Key)){missing=true;break;}
            if(!ReferenceEquals(s,bubbleState) || focus!=bubbleFocus || missing)
            {
                bubbleState=s;bubbleFocus=focus;info.Clear();
                foreach(var a in anchors){var b=IslandBubble.For(s,a.Key,focus);if(b!=null)info[a.Key]=b;}
                ledger.Retain(s);
            }
            var cam=Camera.main;
            PlaceChips(cam);
            PlaceBubbles(cam);
            Diagnose(cam,s);
            SpawnPopups();
            PlacePopups(cam);
        }

        // ---------- bubbles ----------
        void PlaceBubbles(Camera cam)
        {
            int used=0;
            bool vi=ui.Vietnamese;
            string selected=living!=null?living.SelectedUid:null;
            if(cam!=null)
                foreach(var a in anchors)
                {
                    if(used>=MaxBubbles)break;
                    if(!info.TryGetValue(a.Key,out var data))continue;
                    if(!ToLayer(cam,a.Value,out var local))continue;
                    if(used>=bubbles.Count)bubbles.Add(MakeBubble());
                    var b=bubbles[used++];
                    b.uid=a.Key;
                    b.rect.gameObject.SetActive(true);
                    b.rect.anchoredPosition=local;
                    b.name.text=data.Name;
                    b.stage.text=data.Attention!=BubbleAttention.None?data.AttentionText(vi):data.Stage(vi);
                    b.stage.color=data.Attention!=BubbleAttention.None?new Color(1f,.80f,.62f):Ivory;
                    b.markBg.gameObject.SetActive(data.Attention!=BubbleAttention.None);
                    b.markBg.color=data.Attention==BubbleAttention.NextStep?Gold:Alert;
                    b.name.color=a.Key==selected?Coin:Ivory;
                }
            for(int i=used;i<bubbles.Count;i++)bubbles[i].rect.gameObject.SetActive(false);
            if(living!=null && living.LabelCovered==null)living.LabelCovered=Covered;
        }

        bool Covered(string uid)
        {
            foreach(var b in bubbles)if(b.uid==uid && b.rect.gameObject.activeSelf)return true;
            return false;
        }

        // Without an imported DragonVisualCatalog the scene runs the one-dragon legacy preview: anchor its model
        // to the Emberling (else the first dragon) living in a habitat, so the bubble and gold still show.
        DragonCarePreview legacy;
        readonly List<Renderer> legacyRenderers=new List<Renderer>();
        void LegacyAnchor(GameState s)
        {
            if(legacy==null)legacy=Object.FindAnyObjectByType<DragonCarePreview>(FindObjectsInactive.Exclude);
            if(legacy==null || !legacy.isActiveAndEnabled || legacy.Model==null)return;
            string uid=null;
            foreach(var d in s.Dragons)
            {
                if(d.Location.Kind!=LocationKind.Zone)continue;
                if(d.SpeciesId=="emberling"){uid=d.Uid;break;}
                if(uid==null)uid=d.Uid;
            }
            if(uid==null)return;
            legacy.Model.GetComponentsInChildren(false,legacyRenderers);
            bool any=false;var b=new Bounds(legacy.Model.position,Vector3.zero);
            foreach(var r in legacyRenderers)
            {
                if(r==null || !r.enabled)continue;
                if(!any){b=r.bounds;any=true;}else b.Encapsulate(r.bounds);
            }
            anchors.Add(new KeyValuePair<string,Vector3>(uid,new Vector3(b.center.x,b.max.y+.12f,b.center.z)));
        }

        // Editor-only, once: say why no bubble is drawn, so a Console screenshot is enough to fix it.
        bool diagnosed;
        void Diagnose(Camera cam,GameState s)
        {
#if UNITY_EDITOR
            if(diagnosed || Time.frameCount<startFrame+180)return;
            diagnosed=true;
            int inZone=0;foreach(var d in s.Dragons)if(d.Location.Kind==LocationKind.Zone)inZone++;
            int shown=0;foreach(var b in bubbles)if(b.rect.gameObject.activeSelf)shown++;
            if(shown>0)return;
            string why=living==null?"no LivingSanctuaryPresentation":anchors.Count==0?"no living-sanctuary dragon drawn and no legacy preview model (DragonVisualCatalog not imported?)":
                info.Count==0?"no bubble data for "+anchors.Count+" dragons":cam==null?"no Camera.main":
                "anchor off screen: world "+anchors[0].Value+" -> screen "+cam.WorldToScreenPoint(anchors[0].Value)+" of "+Screen.width+"x"+Screen.height;
            Debug.LogWarning("Dravio U6.3: no dragon bubble shown ("+inZone+" dragons in habitats). Reason: "+why);
#endif
        }

        void TapBubble(Bubble b)
        {
            if(b.uid==null)return;
            if(islandCamera!=null && islandCamera.Dragging)return;
            if(living!=null)living.SelectResident(b.uid); // false in the legacy preview: Care already shows its one dragon
            ui.SelectPage((int)HudPage.Care);
        }

        // ---------- habitat name chips (U6.4) ----------
        void PlaceChips(Camera cam)
        {
            int used=0;bool vi=ui.Vietnamese;
            if(cam!=null && plots!=null)
                foreach(var p in plots.Plots)
                {
                    if(!ToLayer(cam,p.LabelPoint,out var local))continue;
                    if(used>=chips.Count)chips.Add(MakeChip());
                    var c=chips[used++];
                    c.rect.gameObject.SetActive(true);
                    c.rect.anchoredPosition=local;
                    c.text.text=p.Unlocked?p.Name(vi)+"  ·  "+HabitatLook.ElementName(p.Element,vi):(vi?"Chưa mở · ":"Locked · ")+p.Name(vi);
                    c.stripe.color=p.Unlocked?HabitatPlots.ElementColor(p.Element):new Color(.6f,.6f,.58f);
                    c.text.color=p.Unlocked?Ivory:new Color(.80f,.78f,.72f);
                }
            for(int i=used;i<chips.Count;i++)chips[i].rect.gameObject.SetActive(false);
        }

        Chip MakeChip()
        {
            var c=new Chip();
            c.rect=Node("Habitat chip",layer);
            c.rect.anchorMin=c.rect.anchorMax=new Vector2(.5f,.5f);c.rect.pivot=new Vector2(.5f,.5f);
            c.rect.SetAsFirstSibling(); // under the dragon bubbles
            var bg=c.rect.gameObject.AddComponent<Image>();bg.sprite=rounded;bg.type=Image.Type.Sliced;
            bg.color=new Color(.06f,.11f,.10f,.72f);bg.raycastTarget=false;
            var row=c.rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.padding=new RectOffset(8,12,4,4);row.spacing=7;row.childAlignment=TextAnchor.MiddleLeft;
            row.childControlWidth=row.childControlHeight=true;row.childForceExpandWidth=row.childForceExpandHeight=false;
            var fit=c.rect.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit=fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var stripe=Node("Element",c.rect);
            var le=stripe.gameObject.AddComponent<LayoutElement>();le.minWidth=le.preferredWidth=13;le.minHeight=le.preferredHeight=13;
            c.stripe=stripe.gameObject.AddComponent<Image>();c.stripe.sprite=circle;c.stripe.raycastTarget=false;
            c.text=Label(c.rect,15,Ivory,FontStyle.Bold);c.text.horizontalOverflow=HorizontalWrapMode.Overflow;
            return c;
        }

        // ---------- gold popups ----------
        void SpawnPopups()
        {
            if(anchorByUid.Count==0)return;
            foreach(var due in ledger.Due(Time.unscaledTime,anchorByUid.Keys))
            {
                Popup p=null;
                foreach(var x in popups)if(x.born<0){p=x;break;}
                if(p==null){if(popups.Count>=MaxPopups)break;p=MakePopup();popups.Add(p);}
                p.uid=due.Key;p.born=Time.unscaledTime;p.text.text="+"+due.Value+(ui.Vietnamese?" vàng":" gold");
                p.rect.gameObject.SetActive(true);
            }
        }
        void PlacePopups(Camera cam)
        {
            foreach(var p in popups)
            {
                if(p.born<0)continue;
                float age=(Time.unscaledTime-p.born)/PopupSeconds;
                if(age>=1f || cam==null || !anchorByUid.TryGetValue(p.uid,out var world) || !ToLayer(cam,world,out var local))
                {p.born=-1;p.rect.gameObject.SetActive(false);continue;}
                p.rect.anchoredPosition=local+new Vector2(0,34+PopupRise*Mathf.Sqrt(age));
                var c=Coin;c.a=age<.7f?1f:1f-(age-.7f)/.3f;p.text.color=c;
                float scale=age<.15f?Mathf.Lerp(.6f,1.1f,age/.15f):Mathf.Lerp(1.1f,1f,Mathf.Min(1f,(age-.15f)/.2f));
                p.rect.localScale=Vector3.one*scale;
            }
        }

        bool ToLayer(Camera cam,Vector3 world,out Vector2 local)
        {
            local=default;
            var screen=cam.WorldToScreenPoint(world);
            if(screen.z<=0f)return false;
            if(screen.x<0 || screen.y<0 || screen.x>Screen.width || screen.y>Screen.height)return false;
            var uiCam=canvas!=null && canvas.rootCanvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.rootCanvas.worldCamera:null;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(layer,screen,uiCam,out local);
        }

        // ---------- dock ----------
        void Tap(IslandDock.Item item)
        {
            if(IslandDock.TapCloses(!ui.Collapsed,ui.CurrentPage,item.Page)){ui.SetCollapsed(true);return;}
            ui.SelectPage((int)item.Page);
        }
        void RefreshDock()
        {
            if(dock==null)return;
            // The closed sanctuary already has corner shortcuts in this HUD.
            dock.gameObject.SetActive(!ui.Collapsed || !ui.HasCornerShortcuts);
            if(!dock.gameObject.activeSelf)return;
            HudPage? hinted=banner!=null && banner.Current!=null?banner.Current.Page:(HudPage?)null;
            foreach(var b in buttons)
            {
                bool open=!ui.Collapsed && ui.CurrentPage==(int)b.item.Page;
                b.bg.color=open?Gold:DockIdle;
                b.label.color=open?ButtonInk:Ivory;
                b.dot.SetActive(hinted==b.item.Page && !open);
            }
        }

        // ---------- construction ----------
        void BuildLayer()
        {
            layer=Node("Island overlay",ui.SafeRoot);Stretch(layer);
            layer.SetAsFirstSibling(); // under the header, card, drawer and dock
        }

        void BuildDock()
        {
            dock=Node("Island dock",ui.SafeRoot);
            dock.anchorMin=dock.anchorMax=dock.pivot=Vector2.zero;
            dock.anchoredPosition=new Vector2(12,10);
            var row=dock.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing=DockGap;row.childControlWidth=row.childControlHeight=true;
            row.childForceExpandWidth=row.childForceExpandHeight=false;
            var fit=dock.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit=fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            foreach(var item in IslandDock.Items)
            {
                var node=Node(item.En,dock);
                var le=node.gameObject.AddComponent<LayoutElement>();le.minWidth=le.preferredWidth=le.minHeight=le.preferredHeight=DockSize;
                var bg=node.gameObject.AddComponent<Image>();bg.sprite=circle;bg.color=DockIdle;
                var button=node.gameObject.AddComponent<Button>();button.targetGraphic=bg;
                var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1,1,1,.92f);
                colors.pressedColor=new Color(.8f,.8f,.8f);colors.selectedColor=Color.white;button.colors=colors;
                node.gameObject.AddComponent<Dravio.UI.DravioPressFeedback>();
                var it=item;button.onClick.AddListener(()=>Tap(it));
                var label=Label(node,12,Ivory,FontStyle.Bold);label.alignment=TextAnchor.MiddleCenter;
                label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;
                label.rectTransform.offsetMin=new Vector2(4,4);label.rectTransform.offsetMax=new Vector2(-4,-4);
                label.text=item.Label(ui.Vietnamese);
                var dot=Node("Dot",node);dot.anchorMin=dot.anchorMax=dot.pivot=new Vector2(1,1);
                dot.anchoredPosition=new Vector2(-2,-2);dot.sizeDelta=new Vector2(16,16);
                var dotImg=dot.gameObject.AddComponent<Image>();dotImg.sprite=circle;dotImg.color=Alert;dotImg.raycastTarget=false;
                buttons.Add(new DockButton{item=item,bg=bg,label=label,dot=dot.gameObject});
            }
        }

        Bubble MakeBubble()
        {
            var b=new Bubble();
            b.rect=Node("Dragon bubble",layer);
            b.rect.anchorMin=b.rect.anchorMax=new Vector2(.5f,.5f);b.rect.pivot=new Vector2(.5f,0);
            var bg=b.rect.gameObject.AddComponent<Image>();bg.sprite=rounded;bg.type=Image.Type.Sliced;bg.color=Ink;
            var button=b.rect.gameObject.AddComponent<Button>();button.targetGraphic=bg;
            button.onClick.AddListener(()=>TapBubble(b));
            var col=b.rect.gameObject.AddComponent<VerticalLayoutGroup>();
            col.padding=new RectOffset(10,10,4,5);col.spacing=0;col.childAlignment=TextAnchor.MiddleCenter;
            col.childControlWidth=col.childControlHeight=true;col.childForceExpandWidth=true;col.childForceExpandHeight=false;
            var fit=b.rect.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit=fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            b.name=Label(b.rect,13,Ivory,FontStyle.Bold);b.name.alignment=TextAnchor.MiddleCenter;
            b.name.horizontalOverflow=HorizontalWrapMode.Overflow;
            b.stage=Label(b.rect,11,Ivory,FontStyle.Normal);b.stage.alignment=TextAnchor.MiddleCenter;
            b.stage.horizontalOverflow=HorizontalWrapMode.Overflow;
            var mark=Node("Mark",b.rect);
            mark.gameObject.AddComponent<LayoutElement>().ignoreLayout=true;
            mark.anchorMin=mark.anchorMax=mark.pivot=new Vector2(1,1);mark.anchoredPosition=new Vector2(8,8);mark.sizeDelta=new Vector2(20,20);
            b.markBg=mark.gameObject.AddComponent<Image>();b.markBg.sprite=circle;b.markBg.raycastTarget=false;
            b.mark=Label(mark,14,Color.white,FontStyle.Bold);b.mark.text="!";b.mark.alignment=TextAnchor.MiddleCenter;
            Stretch(b.mark.rectTransform);
            return b;
        }

        Popup MakePopup()
        {
            var p=new Popup();
            p.rect=Node("Gold popup",layer);
            p.rect.anchorMin=p.rect.anchorMax=p.rect.pivot=new Vector2(.5f,.5f);p.rect.sizeDelta=new Vector2(140,30);
            p.text=Label(p.rect,20,Coin,FontStyle.Bold);p.text.alignment=TextAnchor.MiddleCenter;Stretch(p.text.rectTransform);
            p.text.horizontalOverflow=HorizontalWrapMode.Overflow;
            var outline=p.text.gameObject.AddComponent<Outline>();outline.effectColor=new Color(.25f,.15f,.02f,.9f);outline.effectDistance=new Vector2(1.5f,-1.5f);
            p.rect.gameObject.SetActive(false);
            return p;
        }

        /// <summary>White anti-aliased circle (border 0) or rounded square (9-slice border = radius).</summary>
        static Sprite MakeSprite(int size,float radius,int border)
        {
            var tex=new Texture2D(size,size,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            var px=new Color32[size*size];
            float r=Mathf.Min(radius,size*.5f);
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float cx=Mathf.Clamp(x+.5f,r,size-r),cy=Mathf.Clamp(y+.5f,r,size-r);
                float d=Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(cx,cy));
                byte a=(byte)(255*Mathf.Clamp01(r-d+.5f));
                px[y*size+x]=new Color32(255,255,255,a);
            }
            tex.SetPixels32(px);tex.Apply(false,true);
            return Sprite.Create(tex,new Rect(0,0,size,size),new Vector2(.5f,.5f),100f,0,SpriteMeshType.FullRect,new Vector4(border,border,border,border));
        }

        Font FindFont()
        {
            foreach(var t in ui.SafeRoot.GetComponentsInChildren<Text>(true))if(t.font!=null)return t.font;
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        static RectTransform Node(string name,Transform parent)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);
            return (RectTransform)go.transform;
        }
        static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
        Text Label(Transform parent,int size,Color color,FontStyle style)
        {
            var node=Node("Text",parent);var t=node.gameObject.AddComponent<Text>();
            t.font=font;t.fontSize=size;t.color=color;t.fontStyle=style;t.raycastTarget=false;
            t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Overflow;
            return t;
        }
    }
}
