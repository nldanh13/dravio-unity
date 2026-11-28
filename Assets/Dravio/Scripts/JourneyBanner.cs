using System.Collections.Generic;
using Dravio.Domain;
using Dravio.Domain.Core;
using Dravio.Domain.Progression;
using Dravio.Platform;
using Dravio.Platform.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace Dravio
{
    /// <summary>
    /// U6.1/U6.2 — the player's journey on the playable Origin HUD:
    /// - a "next step" card (chapter, progress, step, hint, Go → opens the drawer page and frames the dragon);
    /// - milestone toasts; an end-of-chapter screen with the reward egg;
    /// - a "while you were away" screen after an offline settlement (once per receipt);
    /// - a collection book of every species (silhouettes + how to get them).
    /// Read-only except the one-tap "resume tutorial" (a plain SetTutorialStatus through the session).
    /// It installs itself after the first scene loads, so existing generated scenes need no regeneration.
    /// </summary>
    public sealed class JourneyBanner : MonoBehaviour
    {
        static readonly Color Ink=new Color(.075f,.13f,.12f,.94f);
        static readonly Color Ivory=new Color(.96f,.91f,.79f);
        static readonly Color Muted=new Color(.72f,.70f,.62f);
        static readonly Color Gold=new Color(.83f,.68f,.40f);
        static readonly Color Reward=new Color(.95f,.62f,.30f);
        static readonly Color ButtonInk=new Color(.10f,.18f,.16f);
        const float Width=430,ToastSeconds=4.5f;
        const string AwayKey="dravio.journey.away";

        FantasyPreviewUI ui;
        DravioSessionHost host;
        ResearchDiscoveryPanel research;
        LivingSanctuaryPresentation living;
        GameSession subscribed;
        Font font;
        RectTransform card,toast,modal,book,bookGrid;
        Text chapter,title,hint,go,toastText,modalTitle,modalText,bookTitle,bookDetail;
        Image accent;
        Button goButton;
        JourneyPrompt prompt;
        GameState promptState;
        bool lastVi=true,awayChecked;
        float toastUntil;
        readonly Queue<string> toasts=new Queue<string>();
        AwayReport shown;
        readonly List<Text> cellNames=new List<Text>(),cellStates=new List<Text>();
        readonly List<Image> cellImages=new List<Image>();
        List<CollectionEntry> entries;
        int selectedCell=-1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(Object.FindObjectsByType<JourneyBanner>(FindObjectsInactive.Include).Length>0)return;
            var hosts=Object.FindObjectsByType<DravioSessionHost>(FindObjectsInactive.Exclude);
            var huds=Object.FindObjectsByType<FantasyPreviewUI>(FindObjectsInactive.Exclude);
            // Only the persistent playable scene: one session host and the tabbed HUD with the journey pages.
            if(hosts.Length!=1 || huds.Length!=1 || huds[0].PageCount<=(int)HudPage.Research || huds[0].SafeRoot==null)return;
            var banner=huds[0].gameObject.AddComponent<JourneyBanner>();
            banner.ui=huds[0];banner.host=hosts[0];
            var panels=Object.FindObjectsByType<ResearchDiscoveryPanel>(FindObjectsInactive.Include);
            banner.research=panels.Length>0?panels[0]:null;
            var presenters=Object.FindObjectsByType<LivingSanctuaryPresentation>(FindObjectsInactive.Include);
            banner.living=presenters.Length>0?presenters[0]:null;
        }

        void Start()
        {
            if(ui==null||host==null){enabled=false;return;}
            font=FindFont();
            BuildCard();
            BuildToast();
            BuildModal();
            BuildBook();
            lastVi=ui.Vietnamese;
            Subscribe();
            Refresh();
        }

        void Subscribe()
        {
            var s=host!=null?host.Session:null;
            if(s==subscribed)return;
            Unsubscribe();
            subscribed=s;
            if(s==null)return;
            s.Changed+=OnChanged;s.DomainEvents+=OnEvents;
        }
        void Unsubscribe(){if(subscribed!=null){subscribed.Changed-=OnChanged;subscribed.DomainEvents-=OnEvents;subscribed=null;}}
        void OnDestroy()=>Unsubscribe();
        void OnChanged()=>Refresh();

        void OnEvents(IReadOnlyList<DomainEvent> events)
        {
            var s=subscribed?.State;
            foreach(var e in events)
            {
                if(e is OfflineReceiptCreated)awayChecked=false; // resume from background: show the new receipt
                string line=PlayerJourneyPlanner.Celebrate(s,e,ui.Vietnamese);
                if(line!=null && toasts.Count<4)toasts.Enqueue(line);
            }
            var finale=PlayerJourneyPlanner.ChapterFinale(s,events);
            if(finale!=null)ShowModal(finale);
        }

        void Update()
        {
            if(host==null)return;
            Subscribe();
            if(ui.Vietnamese!=lastVi){lastVi=ui.Vietnamese;promptState=null;Refresh();if(shown!=null)FillModal();if(book.gameObject.activeSelf)FillBook();}
            if(toast!=null)
            {
                if(Time.unscaledTime>=toastUntil && toasts.Count>0)
                {
                    toastText.text=toasts.Dequeue();
                    toast.gameObject.SetActive(true);
                    toastUntil=Time.unscaledTime+ToastSeconds;
                }
                else if(Time.unscaledTime>=toastUntil && toast.gameObject.activeSelf)toast.gameObject.SetActive(false);
            }
        }

        // Keep the toast just under the card, whose height follows its text.
        void LateUpdate()
        {
            if(toast!=null && card!=null && toast.gameObject.activeSelf)
                toast.anchoredPosition=card.anchoredPosition-new Vector2(0,(card.gameObject.activeSelf?card.rect.height:0)+6);
        }

        void Refresh()
        {
            if(card==null)return;
            var s=subscribed?.State;
            bool ready=subscribed!=null && subscribed.Status==SessionStatus.Ready && !subscribed.Hidden;
            card.gameObject.SetActive(s!=null);
            if(s==null)return;
            CheckAway(s,ready);
            if(!ReferenceEquals(s,promptState)){promptState=s;prompt=PlayerJourneyPlanner.Next(s);}
            if(prompt==null){card.gameObject.SetActive(false);return;}
            bool vi=ui.Vietnamese;
            string progress=prompt.Progress(vi);
            chapter.text=prompt.ChapterLabel(vi).ToUpperInvariant()+(progress.Length>0?"  ·  "+progress:"");
            title.text=prompt.Title(vi);
            hint.text=prompt.Hint(vi);
            accent.color=prompt.Urgent?Reward:Gold;
            go.text=prompt.Quick==JourneyQuickAction.ResumeTutorial?(vi?"Tiếp tục":"Resume"):(vi?"Đi tới ›":"Go ›");
            goButton.interactable=prompt.Quick==JourneyQuickAction.None || ready;
            if(book.gameObject.activeSelf)FillBook();
        }

        // "While you were away": once per offline receipt id, remembered across launches.
        void CheckAway(GameState s,bool ready)
        {
            if(awayChecked || !ready)return;
            awayChecked=true;
            var report=AwaySummary.Build(s);
            if(report==null)return;
            string last="";
            try{last=PlayerPrefs.GetString(AwayKey,"");}catch{}
            if(last==report.Id)return;
            try{PlayerPrefs.SetString(AwayKey,report.Id);PlayerPrefs.Save();}catch{}
            ShowModal(report);
        }

        void Go()
        {
            if(prompt==null)return;
            if(prompt.Quick==JourneyQuickAction.ResumeTutorial)
            {
                var s=host!=null?host.Session:null;
                if(s!=null && s.Status==SessionStatus.Ready && !s.Hidden)
                    s.Send(id=>new SetTutorialStatusCommand(id,TutorialStatus.Active));
                ui.SelectPage((int)HudPage.Care);
                return;
            }
            ui.SelectPage((int)prompt.Page);
            if(prompt.Page==HudPage.Research && research!=null)research.ShowMode(prompt.QuestMode?2:1);
            if(prompt.FocusUid!=null && living!=null)living.FocusDragon(prompt.FocusUid);
        }

        // ---------- modal (away report, chapter finale) ----------
        void ShowModal(AwayReport report){shown=report;FillModal();modal.gameObject.SetActive(true);modal.SetAsLastSibling();}
        void FillModal()
        {
            if(shown==null)return;
            bool vi=ui.Vietnamese;
            modalTitle.text=shown.Title(vi);
            modalText.text="• "+string.Join("\n• ",shown.Lines(vi));
        }
        void CloseModal(){shown=null;modal.gameObject.SetActive(false);}

        // ---------- collection book ----------
        void ToggleBook()
        {
            bool open=!book.gameObject.activeSelf;
            book.gameObject.SetActive(open);
            if(open){book.SetAsLastSibling();FillBook();}
        }
        void FillBook()
        {
            var s=subscribed?.State;
            if(s==null)return;
            bool vi=ui.Vietnamese;
            entries=SanctuaryCollection.Build(s);
            int found=0;foreach(var e in entries)if(e.Discovered)found++;
            bookTitle.text=(vi?"Bách khoa rồng · ":"Dragon book · ")+found+"/"+entries.Count;
            for(int i=0;i<cellNames.Count;i++)
            {
                bool has=i<entries.Count;
                cellImages[i].gameObject.SetActive(has);
                if(!has)continue;
                var e=entries[i];
                cellNames[i].text=e.Discovered?e.Name:"???";
                cellStates[i].text=SanctuaryCollection.RarityName(e.Rarity,vi)+(e.Owned>0?" · ×"+e.Owned:"");
                cellImages[i].color=e.Discovered?RarityColor(e.Rarity):new Color(.12f,.15f,.14f,1f);
                cellNames[i].color=e.Discovered?ButtonInk:Muted;
                cellStates[i].color=e.Discovered?ButtonInk:Muted;
            }
            if(selectedCell<0||selectedCell>=entries.Count)
                bookDetail.text=vi?"Chạm một ô để xem cách có loài đó.":"Tap a cell to see how to get that species.";
            else
            {
                var e=entries[selectedCell];
                bookDetail.text=(e.Discovered?e.Name:(vi?"Loài chưa khám phá":"Undiscovered species"))+" · "+SanctuaryCollection.RarityName(e.Rarity,vi)+
                    (e.Tier!=null?(vi?" · nghiên cứu: ":" · research: ")+e.Tier:"")+"\n"+e.How(vi);
            }
        }
        void SelectCell(int i){selectedCell=i;FillBook();}
        static Color RarityColor(Rarity r)=>r switch
        {
            Rarity.Common=>new Color(.86f,.82f,.70f),
            Rarity.Rare=>new Color(.62f,.80f,.86f),
            Rarity.Epic=>new Color(.80f,.68f,.90f),
            _=>new Color(.96f,.80f,.42f),
        };

        // ---------- UI construction (runtime, no scene asset changes) ----------
        void BuildCard()
        {
            card=Node("Journey banner",ui.SafeRoot);
            card.anchorMin=card.anchorMax=new Vector2(0,1);card.pivot=new Vector2(0,1);
            card.anchoredPosition=new Vector2(12,-72);
            card.sizeDelta=new Vector2(Width,0);
            card.gameObject.AddComponent<Image>().color=Ink;
            var fit=card.gameObject.AddComponent<ContentSizeFitter>();fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var row=card.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.padding=new RectOffset(0,10,0,0);row.spacing=10;row.childAlignment=TextAnchor.MiddleLeft;
            row.childControlWidth=row.childControlHeight=true;row.childForceExpandWidth=false;row.childForceExpandHeight=true;

            var bar=Node("Accent",card);accent=bar.gameObject.AddComponent<Image>();
            var barSize=bar.gameObject.AddComponent<LayoutElement>();barSize.preferredWidth=barSize.minWidth=6;

            var body=Node("Body",card);
            var bodySize=body.gameObject.AddComponent<LayoutElement>();bodySize.flexibleWidth=1;
            var col=Column(body,new RectOffset(0,0,8,8),2);
            chapter=Label(body,12,Gold,FontStyle.Bold);
            title=Label(body,18,Ivory,FontStyle.Bold);
            hint=Label(body,14,Ivory,FontStyle.Normal);
            col.childForceExpandHeight=false;

            var buttons=Node("Buttons",card);
            var bs=buttons.gameObject.AddComponent<LayoutElement>();bs.preferredWidth=bs.minWidth=96;
            var bcol=Column(buttons,new RectOffset(0,0,8,8),6);bcol.childAlignment=TextAnchor.MiddleCenter;
            goButton=MakeButton(buttons,48,16,out go);goButton.onClick.AddListener(Go);
            var bookButton=MakeButton(buttons,40,13,out var bookLabel);bookButton.onClick.AddListener(ToggleBook);
            bookLabel.text="Bách khoa";
            SetColors(bookButton,new Color(.30f,.40f,.34f));bookLabel.color=Ivory;
        }

        void BuildToast()
        {
            toast=Node("Journey toast",ui.SafeRoot);
            toast.anchorMin=toast.anchorMax=new Vector2(0,1);toast.pivot=new Vector2(0,1);
            toast.sizeDelta=new Vector2(Width,0);
            toast.gameObject.AddComponent<Image>().color=new Color(.30f,.20f,.08f,.95f);
            var tfit=toast.gameObject.AddComponent<ContentSizeFitter>();tfit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            Column(toast,new RectOffset(14,14,8,8),0);
            toastText=Label(toast,15,new Color(1f,.93f,.72f),FontStyle.Bold);
            toast.gameObject.SetActive(false);
        }

        void BuildModal()
        {
            modal=Node("Journey modal",ui.SafeRoot);Stretch(modal);
            modal.gameObject.AddComponent<Image>().color=new Color(0,0,0,.55f); // also blocks taps behind it
            var panel=Node("Panel",modal);
            panel.anchorMin=panel.anchorMax=panel.pivot=new Vector2(.5f,.5f);panel.sizeDelta=new Vector2(480,0);
            panel.gameObject.AddComponent<Image>().color=Ink;
            var fit=panel.gameObject.AddComponent<ContentSizeFitter>();fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            Column(panel,new RectOffset(22,22,20,20),12);
            modalTitle=Label(panel,22,Gold,FontStyle.Bold);
            modalText=Label(panel,16,Ivory,FontStyle.Normal);
            var ok=MakeButton(panel,48,16,out var okLabel);ok.onClick.AddListener(CloseModal);
            okLabel.text="OK";
            modal.gameObject.SetActive(false);
        }

        void BuildBook()
        {
            book=Node("Collection book",ui.SafeRoot);Stretch(book);
            book.gameObject.AddComponent<Image>().color=new Color(0,0,0,.55f);
            var panel=Node("Panel",book);
            panel.anchorMin=panel.anchorMax=panel.pivot=new Vector2(.5f,.5f);panel.sizeDelta=new Vector2(620,0);
            panel.gameObject.AddComponent<Image>().color=Ink;
            var fit=panel.gameObject.AddComponent<ContentSizeFitter>();fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            Column(panel,new RectOffset(18,18,16,16),10);
            bookTitle=Label(panel,20,Gold,FontStyle.Bold);
            bookGrid=Node("Grid",panel);
            var grid=bookGrid.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize=new Vector2(140,58);grid.spacing=new Vector2(6,6);
            grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=4;
            var gfit=bookGrid.gameObject.AddComponent<ContentSizeFitter>();gfit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            for(int i=0;i<16;i++)
            {
                int index=i;
                var cell=Node("Cell",bookGrid);
                var img=cell.gameObject.AddComponent<Image>();
                var b=cell.gameObject.AddComponent<Button>();b.targetGraphic=img;b.onClick.AddListener(()=>SelectCell(index));
                Column(cell,new RectOffset(8,8,6,6),0).childAlignment=TextAnchor.MiddleCenter;
                var name=Label(cell,15,ButtonInk,FontStyle.Bold);name.alignment=TextAnchor.MiddleCenter;
                var state=Label(cell,12,ButtonInk,FontStyle.Normal);state.alignment=TextAnchor.MiddleCenter;
                cellImages.Add(img);cellNames.Add(name);cellStates.Add(state);
            }
            bookDetail=Label(panel,14,Ivory,FontStyle.Normal);
            var close=MakeButton(panel,44,15,out var closeLabel);close.onClick.AddListener(ToggleBook);
            closeLabel.text="Đóng / Close";
            book.gameObject.SetActive(false);
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
        static VerticalLayoutGroup Column(Transform t,RectOffset padding,float spacing)
        {
            var col=t.gameObject.AddComponent<VerticalLayoutGroup>();
            col.padding=padding;col.spacing=spacing;col.childControlWidth=col.childControlHeight=true;
            col.childForceExpandWidth=true;col.childForceExpandHeight=false;
            return col;
        }
        Text Label(Transform parent,int size,Color color,FontStyle style)
        {
            var node=Node("Text",parent);var t=node.gameObject.AddComponent<Text>();
            t.font=font;t.fontSize=size;t.color=color;t.fontStyle=style;t.raycastTarget=false;
            t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Overflow;
            return t;
        }
        static void SetColors(Button b,Color normal)
        {
            var colors=b.colors;colors.normalColor=normal;colors.highlightedColor=Color.Lerp(normal,Color.white,.25f);
            colors.pressedColor=Color.Lerp(normal,Color.black,.2f);colors.selectedColor=normal;
            colors.disabledColor=new Color(.42f,.43f,.38f,.68f);b.colors=colors;
        }
        Button MakeButton(Transform parent,float height,int size,out Text label)
        {
            var node=Node("Button",parent);
            var le=node.gameObject.AddComponent<LayoutElement>();le.minHeight=le.preferredHeight=height;
            var img=node.gameObject.AddComponent<Image>();img.color=Color.white;
            var b=node.gameObject.AddComponent<Button>();b.targetGraphic=img;SetColors(b,Gold);
            node.gameObject.AddComponent<Dravio.UI.DravioPressFeedback>();
            label=Label(node,size,ButtonInk,FontStyle.Bold);label.alignment=TextAnchor.MiddleCenter;
            Stretch(label.rectTransform);
            return b;
        }
    }
}
