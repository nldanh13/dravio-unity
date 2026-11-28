using System;
using System.Linq;
using Dravio.Domain;
using Dravio.Domain.Behavior;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Queries;
using Dravio.Platform;
using Dravio.Platform.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace Dravio
{
    // Read-only browser plus one existing ObserveDragon command through the living controller.
    public sealed class ResearchDiscoveryPanel:MonoBehaviour
    {
        [SerializeField] DravioSessionHost host;
        [SerializeField] LivingSanctuaryPresentation living;
        [SerializeField] FantasyPreviewUI localization;
        [SerializeField] GameObject page;
        [SerializeField] Text overview,detail,feedback;
        [SerializeField] Button codex,journal,quests,prev,next,record;
        Button chronicle;
        Text chronicleLabel, hintLabel;
        Button hint;
        string hintSave;
        int hintChapter = -1;
        GameSession subscribed;
        string species,uid,quest;
        int mode;
        bool dirty=true,lastVi=true;
        float refreshAt;
        string lastResultEn="",lastResultVi="";
        GameState lastState;
        public void Configure(DravioSessionHost h,LivingSanctuaryPresentation l,FantasyPreviewUI loc,GameObject p,
            Text o,Text d,Text f,Button c,Button j,Button q,Button back,Button forward,Button observe)
        {host=h;living=l;localization=loc;page=p;overview=o;detail=d;feedback=f;codex=c;journal=j;quests=q;prev=back;next=forward;record=observe;}
        bool Vi=>localization==null||localization.Vietnamese;
        void Start()
        {
            // A separate full-width control also works in previously generated playable scenes.
            chronicle=Instantiate(record,record.transform.parent);
            chronicle.name="Migration chronicle";
            chronicle.transform.SetSiblingIndex(quests.transform.parent.GetSiblingIndex()+1);
            chronicleLabel=chronicle.GetComponentInChildren<Text>();
            chronicle.interactable=true;
            chronicle.onClick.AddListener(()=>SetMode(3));
            hint=Instantiate(record,record.transform.parent);
            hint.name="Chapter hint";
            hint.transform.SetSiblingIndex(chronicle.transform.GetSiblingIndex()+1);
            hintLabel=hint.GetComponentInChildren<Text>();
            hint.interactable=true;
            hint.gameObject.SetActive(false);
            hint.onClick.AddListener(ToggleHint);
            codex.onClick.AddListener(()=>SetMode(0));journal.onClick.AddListener(()=>SetMode(1));quests.onClick.AddListener(()=>SetMode(2));
            prev.onClick.AddListener(()=>Cycle(-1));next.onClick.AddListener(()=>Cycle(1));record.onClick.AddListener(Record);
            record.interactable=false;
        }
        void ToggleHint()
        {
            var state=host?.Session?.State;
            if(mode!=3 || state==null)return;
            int chapter=Dravio.Domain.Progression.MigrationChronicle.ChaptersCleared(state);
            hintChapter=hintSave==state.SaveId && hintChapter==chapter ? -1 : chapter;
            hintSave=state.SaveId;
            Refresh();ScrollTop();
        }
        void ScrollTop(){var scroll=page!=null?page.GetComponent<ScrollRect>():null;if(scroll!=null)scroll.verticalNormalizedPosition=1f;}
        void SetMode(int m){mode=m;dirty=true;Refresh();ScrollTop();}
        void OnDestroy(){if(subscribed!=null)subscribed.DomainEvents-=OnEvents;}
        void OnEvents(System.Collections.Generic.IReadOnlyList<DomainEvent> events)
        {
            dirty=true;
            foreach(var e in events)
            {
                if(e is DragonObserved o)
                {
                    lastResultEn="Recorded "+o.SpeciesId+": "+(o.Findings.Count==0?"no new findings":string.Join(", ",o.Findings))+
                        " · deferred "+o.DeferredByDailyLimit;
                    lastResultVi="Đã ghi "+o.SpeciesId+": "+(o.Findings.Count==0?"chưa có phát hiện mới":string.Join(", ",o.Findings))+
                        " · hoãn "+o.DeferredByDailyLimit;
                }
                else if(e is NightJournalWritten n)
                {
                    // Event is live-only; accumulated records remain in save, not event history.
                    lastResultEn="Night journal: "+string.Join(", ",n.SpeciesIds.Zip(n.Findings,(s,f)=>s+" "+f));
                    lastResultVi="Nhật ký đêm: "+string.Join(", ",n.SpeciesIds.Zip(n.Findings,(s,f)=>s+" "+f));
                }
            }
        }
        void Update()
        {
            var session=host!=null?host.Session:null;
            if(session!=subscribed)
            {
                if(subscribed!=null)subscribed.DomainEvents-=OnEvents;
                subscribed=session;if(subscribed!=null)subscribed.DomainEvents+=OnEvents;dirty=true;
            }
            if(page==null||!page.activeInHierarchy)return;
            if(dirty||!ReferenceEquals(lastState,session?.State)||lastVi!=Vi||Time.unscaledTime>=refreshAt)Refresh();
        }
        string[] Choices(GameState state)
        {
            if(mode==0)return SpeciesCatalog.All.Select(s=>s.Id).ToArray();
            if(mode==1)return living.VisibleResearchOpportunities().Select(o=>o.Uid).ToArray();
            return ProgressionQueries.QuestRequirements(state).Select(q=>q.QuestId).ToArray();
        }
        void Cycle(int step)
        {
            var s=host?.Session?.State;if(s==null)return;
            var ids=Choices(s);if(ids.Length==0)return;
            string current=mode==0?species:mode==1?uid:quest;
            int i=Array.IndexOf(ids,current);var value=ids[(i<0?0:(i+step+ids.Length)%ids.Length)];
            if(mode==0)species=value;else if(mode==1)uid=value;else quest=value;
            dirty=true;Refresh();ScrollTop();
        }
        void Record()
        {
            if(mode!=1||string.IsNullOrEmpty(uid))return;
            if(!living.TryRecordVisibleResearch(uid))
            {lastResultEn="Observation unavailable; check the current resident and cooldown.";lastResultVi="Chưa thể quan sát; kiểm tra cư dân và thời gian chờ.";}
            dirty=true;Refresh();
        }
        static void Write(Text label,string value)
        {
            if(label.text!=value)label.text=value;
            var layout=label.GetComponent<LayoutElement>();
            if(layout!=null){float height=Mathf.Max(48,label.preferredHeight+12);if(Mathf.Abs(layout.preferredHeight-height)>1)layout.preferredHeight=height;}
        }
        void Refresh()
        {
            dirty=false;refreshAt=Time.unscaledTime+.75f;lastVi=Vi;
            var s=host?.Session?.State;lastState=s;record.interactable=false;prev.interactable=next.interactable=false;
            chronicleLabel.text=Vi?"Cốt truyện và lịch sự kiện":"Story and event calendar";
            record.gameObject.SetActive(mode!=3);
            prev.transform.parent.gameObject.SetActive(mode!=3);
            if(mode!=3 || s==null || host.Session.Status!=SessionStatus.Ready)hint.gameObject.SetActive(false);
            if(s==null||host.Session.Status!=SessionStatus.Ready)
            {Write(overview,Vi?"Đang tải dữ liệu nghiên cứu":"Loading research");Write(detail,"—");Write(feedback,"—");return;}
            bool vi=Vi;
            if(mode==3)
            {
                int chapter=Dravio.Domain.Progression.MigrationChronicle.ChaptersCleared(s);
                bool showHint=hintSave==s.SaveId && hintChapter==chapter;
                hint.gameObject.SetActive(chapter<4);
                hintLabel.text=showHint?(vi?"Ẩn gợi ý":"Hide hint"):(vi?"Xem gợi ý chương này":"Show chapter hint");
                Write(overview,ChronicleView.Story(s,vi,showHint));
                var today=Dravio.Domain.Progression.MigrationChronicle.LocalDay(s.Clock.HighWaterAtMs);
                Write(detail,ChronicleView.Calendar(s,today,vi));
                Write(feedback,vi?"Trong thời gian sự kiện, hoàn thành mục tiêu để lưu dấu vào hồ sơ.":"Complete an objective during its event to add a stamp to this save.");
                return;
            }
            var progress=ProgressionQueries.Overview(s);
            Write(overview,(vi?"Bách khoa: ":"Codex: ")+progress.CodexCells+"/"+progress.CodexCellsTotal+
                " · "+(vi?"Loài: ":"Species: ")+progress.SpeciesDiscovered+"/"+progress.SpeciesTotal+
                "\n"+(vi?"Ghi nghiên cứu: ":"Research recording: ")+(progress.Rules.ResearchRecordingEnabled?"ON":"OFF")+
                " · "+(vi?"Nhật ký đêm: ":"Night journal: ")+(progress.Rules.NightJournalEnabled?"ON":"OFF"));
            if(mode==0)
            {
                var ids=SpeciesCatalog.All.Select(x=>x.Id).ToArray();
                species=ResearchDiscoveryView.KeepOrFirst(ids,species);
                Write(detail,(vi?"Ô loài ":"Species slot ")+(Array.IndexOf(ids,species)+1)+"/"+ids.Length+"\n\n"+ResearchDiscoveryView.Species(s,species,vi));
                prev.interactable=next.interactable=ids.Length>1;
            }
            else if(mode==1)
            {
                var list=living.VisibleResearchOpportunities();
                uid=ResearchDiscoveryView.KeepOrFirst(list.Select(o=>o.Uid),uid);
                var o=list.FirstOrDefault(x=>x.Uid==uid);
                if(o==null)Write(detail,vi?"Không có cư dân đang hiện để quan sát.":"No visible resident to observe.");
                else
                {
                    var d=s.FindDragon(uid);var ind=ProgressionQueries.IndividualResearch(s,uid);
                    Write(detail,o.SpeciesId+" · "+(d?.Nickname??"")+"\nUID: "+uid+"\n"+o.ZoneId+" / "+o.HabitatId+
                        "\n"+o.Behavior+" · "+o.Phase+"\n\n"+ResearchDiscoveryView.Hint(o.HintKey,vi)+
                        "\n"+(vi?"Chờ: ":"Cooldown: ")+Math.Ceiling(o.CooldownRemainingMs/1000.0)+" s"+
                        "\n"+(vi?"Dự kiến phát hiện: ":"Potential findings: ")+string.Join(", ",o.SpeciesFindings)+
                        "\n"+(vi?"Hoãn theo giới hạn ngày: ":"Deferred by daily cap: ")+o.DeferredByDailyLimit+
                        "\n"+(vi?"Nét cá thể dự kiến: ":"Potential traits: ")+string.Join(", ",o.IndividualReveals)+
                        "\n\n"+(vi?"Số lần ghi nhận cá thể: ":"Individual observations: ")+ind.Observations+
                        "\n"+(vi?"Nét đã ghi: ":"Recorded traits: ")+string.Join(", ",ind.RevealedTraits));
                    record.interactable=ResearchDiscoveryView.CanRecord(o);
                }
                prev.interactable=next.interactable=list.Count>1;
            }
            else
            {
                var list=ProgressionQueries.QuestRequirements(s);quest=ResearchDiscoveryView.KeepOrFirst(list.Select(q=>q.QuestId),quest);
                Write(detail,ResearchDiscoveryView.Quest(list.FirstOrDefault(q=>q.QuestId==quest),progress.Rules.QuestCompletionEnabled,vi));
                prev.interactable=next.interactable=list.Count>1;
            }
            Write(feedback,(vi?lastResultVi:lastResultEn)+"\n"+(vi?"Phát hiện tích lũy được lưu; thông báo sự kiện chỉ trong phiên.":"Accumulated findings are saved; event messages are session-only."));
        }
    }
}
