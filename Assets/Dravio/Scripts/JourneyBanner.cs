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
    /// U6.1 — always-visible "next step" card for the playable Origin scene. It answers the one question the
    /// tabbed drawer never did: what should I do now, and where is the button? The card shows the chapter,
    /// the next step from <see cref="PlayerJourneyPlanner"/> and a Go button that opens the drawer page holding
    /// that action (Research opens in Quests or Observe mode). Milestones show as a short toast.
    /// Read-only except the one-tap "resume tutorial" (a plain SetTutorialStatus through the session).
    /// It installs itself after the first scene loads, so existing generated scenes need no regeneration.
    /// </summary>
    public sealed class JourneyBanner : MonoBehaviour
    {
        static readonly Color Ink=new Color(.075f,.13f,.12f,.94f);
        static readonly Color Ivory=new Color(.96f,.91f,.79f);
        static readonly Color Gold=new Color(.83f,.68f,.40f);
        static readonly Color Reward=new Color(.95f,.62f,.30f);
        const float Width=430,ToastSeconds=4.5f;

        FantasyPreviewUI ui;
        DravioSessionHost host;
        ResearchDiscoveryPanel research;
        GameSession subscribed;
        RectTransform card,toast;
        Text chapter,title,hint,go,toastText;
        Image accent;
        Button goButton;
        JourneyPrompt prompt;
        GameState promptState;
        bool lastVi=true;
        float toastUntil;
        readonly Queue<string> toasts=new Queue<string>();

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
        }

        void Start()
        {
            if(ui==null||host==null){enabled=false;return;}
            Build();
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
                string line=PlayerJourneyPlanner.Celebrate(s,e,ui.Vietnamese);
                if(line!=null && toasts.Count<4)toasts.Enqueue(line);
            }
        }

        void Update()
        {
            if(host==null)return;
            Subscribe();
            if(ui.Vietnamese!=lastVi){lastVi=ui.Vietnamese;promptState=null;Refresh();}
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
            if(!ReferenceEquals(s,promptState)){promptState=s;prompt=PlayerJourneyPlanner.Next(s);}
            if(prompt==null){card.gameObject.SetActive(false);return;}
            bool vi=ui.Vietnamese;
            chapter.text=prompt.ChapterLabel(vi).ToUpperInvariant();
            title.text=prompt.Title(vi);
            hint.text=prompt.Hint(vi);
            accent.color=prompt.Urgent?Reward:Gold;
            go.text=prompt.Quick==JourneyQuickAction.ResumeTutorial?(vi?"Tiếp tục":"Resume"):(vi?"Đi tới ›":"Go ›");
            goButton.interactable=prompt.Quick==JourneyQuickAction.None || ready;
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
        }

        // ---------- UI construction (runtime, no scene asset changes) ----------
        void Build()
        {
            var font=FindFont();
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
            var col=body.gameObject.AddComponent<VerticalLayoutGroup>();
            col.padding=new RectOffset(0,0,8,8);col.spacing=2;col.childControlWidth=col.childControlHeight=true;
            col.childForceExpandWidth=true;col.childForceExpandHeight=false;
            chapter=Label(body,font,12,Gold,FontStyle.Bold);
            title=Label(body,font,18,Ivory,FontStyle.Bold);
            hint=Label(body,font,14,Ivory,FontStyle.Normal);

            var buttonNode=Node("Go",card);
            var buttonSize=buttonNode.gameObject.AddComponent<LayoutElement>();
            buttonSize.preferredWidth=buttonSize.minWidth=96;buttonSize.minHeight=48;buttonSize.flexibleHeight=0;
            var buttonImage=buttonNode.gameObject.AddComponent<Image>();buttonImage.color=Color.white;
            goButton=buttonNode.gameObject.AddComponent<Button>();goButton.targetGraphic=buttonImage;
            var colors=goButton.colors;colors.normalColor=Gold;colors.highlightedColor=new Color(.96f,.83f,.56f);
            colors.pressedColor=new Color(.66f,.52f,.29f);colors.disabledColor=new Color(.42f,.43f,.38f,.68f);goButton.colors=colors;
            goButton.onClick.AddListener(Go);
            go=Label(buttonNode,font,16,new Color(.10f,.18f,.16f),FontStyle.Bold);go.alignment=TextAnchor.MiddleCenter;
            Stretch(go.rectTransform);
            buttonNode.gameObject.AddComponent<Dravio.UI.DravioPressFeedback>();

            toast=Node("Journey toast",ui.SafeRoot);
            toast.anchorMin=toast.anchorMax=new Vector2(0,1);toast.pivot=new Vector2(0,1);
            toast.sizeDelta=new Vector2(Width,0);
            toast.gameObject.AddComponent<Image>().color=new Color(.30f,.20f,.08f,.95f);
            var tfit=toast.gameObject.AddComponent<ContentSizeFitter>();tfit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var tcol=toast.gameObject.AddComponent<VerticalLayoutGroup>();tcol.padding=new RectOffset(14,14,8,8);
            tcol.childControlWidth=tcol.childControlHeight=true;tcol.childForceExpandHeight=false;
            toastText=Label(toast,font,15,new Color(1f,.93f,.72f),FontStyle.Bold);
            toast.gameObject.SetActive(false);
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
        static Text Label(Transform parent,Font font,int size,Color color,FontStyle style)
        {
            var node=Node("Text",parent);var t=node.gameObject.AddComponent<Text>();
            t.font=font;t.fontSize=size;t.color=color;t.fontStyle=style;t.raycastTarget=false;
            t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Overflow;
            return t;
        }
    }
}
