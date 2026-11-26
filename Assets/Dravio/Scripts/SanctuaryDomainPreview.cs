using System;
using System.Globalization;
using System.Linq;
using Dravio.Domain;
using Dravio.Domain.Biology;
using Dravio.Domain.Breeding;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.World;
using Dravio.Domain.Progression;
using Dravio.Domain.Queries;
using Dravio.Domain.Economy;
using Dravio.Domain.Sanctuary;
using Dravio.Domain.Health;
using Dravio.Platform;
using Dravio.Platform.Unity;
using Dravio.EnvironmentArt;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Dravio
{
    // Temporary in-memory adapter. U2.2 owns persistence and real lifecycle time policy.
    // Only the canonical command bus mutates domain state. Visual RNG is not involved.
    public sealed class SanctuaryDomainPreview : MonoBehaviour
    {
        [SerializeField] private Text gold, progress, notice;
        [SerializeField] private GameObject fruit;
        private GameState state;
        [SerializeField] private DravioSessionHost host;
        [SerializeField] private SanctuaryLocationData locations;
        [SerializeField] private Text saveStatus, tutorialStatus, rosterStatus, guideNotice;
        [SerializeField] private Text hatcheryStatus, parentsStatus, hatcheryNotice;
        [SerializeField] private Text projectStatus;
        [SerializeField] private Button projectBuild,standardStarter;
        public void ConfigureEconomy(Text status,Button build,Button starter){projectStatus=status;projectBuild=build;standardStarter=starter;}
        [SerializeField] private Text managementSummary,managementWorld,managementCodex,managementMaterials,managementExpedition,managementFeedback;
        [SerializeField] private Button unlockOceanButton,unlockSkyButton,claimExpeditionButton;
        [SerializeField] private Text expeditionRouteText,expeditionDragonText;
        [SerializeField] private Button nextExpeditionRoute,nextExpeditionDragon,startExpedition;
        private string expeditionRouteId,expeditionDragonUid;
        public void ConfigureExpedition(Text route,Text dragon,Button nextRoute,Button nextDragon,Button start)
        {expeditionRouteText=route;expeditionDragonText=dragon;nextExpeditionRoute=nextRoute;nextExpeditionDragon=nextDragon;startExpedition=start;}
        [SerializeField] private Text[] upgradeStatus;
        [SerializeField] private Button[] upgradeButtons;
        [SerializeField] private Text healthStatus,careCaseStatus,carePlanStatus;
        [SerializeField] private Button healthNext,healthShelter,healthAssess,healthNextPlan,healthApplyPlan,healthStart,healthReview,healthDischarge;
        private string healthUid,healthCaseId,healthPlanId;
        [SerializeField] private LivingSanctuaryPresentation livingPresentation;
        private bool livingMode;
        private string parentA, parentB;
        public void ConfigureLiving(LivingSanctuaryPresentation presentation)=>livingPresentation=presentation;
        public void EnableLivingPresentation(){livingMode=true;foodId=null;if(fruit!=null)fruit.SetActive(false);}
        private GameSession Session=>host!=null?host.Session:null;
        private bool persistentMode=>host!=null;
        public string HabitatId=>state?.FindDragon(uid)?.Location?.HabitatId ?? "origin_meadow";
        public SanctuaryLocationData Locations=>locations;
        public bool IsPersistentPreview=>persistentMode;
        private FixedClock clock;
        private string uid, foodId;
        private Vector3 spawn;
        private double fractionalMs;
        private long nextTick, nextConsumeAttempt;
        private int request;
        private bool vi=true, focused=true, paused, skipFrame=true;
        private string messageEn="",messageVi="";
        public bool HasFood => state!=null && foodId!=null && state.Food.Drops.Any(d=>d.Id==foodId);
        public Vector3 FoodPosition => fruit!=null?fruit.transform.position:spawn;
        private bool worldSwitching;
        public void SetWorldSwitching(bool value){worldSwitching=value;Refresh();}
        private bool CanSend => !worldSwitching && state!=null && focused && !paused && (!persistentMode || (Session!=null && Session.Status==SessionStatus.Ready && !Session.Hidden));
        public bool CanInteract => CanSend && state.FindDragon(uid)!=null;

        public void Configure(Text goldLabel,Text progressLabel,Text noticeLabel,GameObject fruitVisual)
        {gold=goldLabel;progress=progressLabel;notice=noticeLabel;fruit=fruitVisual;}

        public void ConfigurePersistent(DravioSessionHost sessionHost,SanctuaryLocationData locationData,Text saved,Text tutorial,Text roster,Text guideFeedback)
        {host=sessionHost;locations=locationData;saveStatus=saved;tutorialStatus=tutorial;rosterStatus=roster;guideNotice=guideFeedback;}

        public void ConfigureHatchery(Text status,Text parents,Text feedback)
        {hatcheryStatus=status;parentsStatus=parents;hatcheryNotice=feedback;}

        public void ConfigureManagement(Text summary,Text world,Text codex,Text materials,Text expedition,Text feedback,
            Button ocean,Button sky,Button claim,Text[] upgrades,Button[] purchases)
        {
            managementSummary=summary;managementWorld=world;managementCodex=codex;managementMaterials=materials;
            managementExpedition=expedition;managementFeedback=feedback;unlockOceanButton=ocean;unlockSkyButton=sky;
            claimExpeditionButton=claim;upgradeStatus=upgrades;upgradeButtons=purchases;
        }

        public void ConfigureHealth(Text status,Text caseStatus,Text planStatus,Button next,Button shelter,Button assess,
            Button nextPlan,Button applyPlan,Button start,Button review,Button discharge)
        {
            healthStatus=status;careCaseStatus=caseStatus;carePlanStatus=planStatus;
            healthNext=next;healthShelter=shelter;healthAssess=assess;healthNextPlan=nextPlan;
            healthApplyPlan=applyPlan;healthStart=start;healthReview=review;healthDischarge=discharge;
        }

        private void Awake()
        {
            spawn=transform.position;
            focused=Application.isFocused;
            if(fruit!=null)fruit.SetActive(false);
            if(persistentMode)return; // Host owns the only clock and command bus.
            var now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            clock=new FixedClock(now);
            var id=Guid.NewGuid().ToString("N");
            state=InitialStateFactory.Create(id,now,new Mulberry32(Rng.HashSeed(id)));
            uid=state.Dragons[0].Uid;
            nextTick=now+1000;
            SetMessage("Session only — no save or offline rewards.","Phiên thử — chưa lưu hoặc thưởng offline.");
            Refresh();
        }

        private void Start()
        {
            if(!persistentMode)return;
            if(Session==null){SetMessage("Session unavailable.","Không mở được phiên chơi.");return;}
            Session.Changed+=OnSessionChanged;
            Session.DomainEvents+=OnDomainEvents;
            OnSessionChanged();
            // The initial offline receipt can be created before this component subscribes.
            if(state?.OfflineReceipt!=null)
            {
                var r=state.OfflineReceipt;
                SetMessage("Offline receipt: "+GoldText(r.GoldMicro)+" G","Báo cáo offline: "+GoldText(r.GoldMicro)+" G");
            }
            else SetMessage("Persistent game session.","Phiên chơi có lưu tiến trình.");
        }

        private void OnDestroy()
        {
            if(Session!=null){Session.Changed-=OnSessionChanged;Session.DomainEvents-=OnDomainEvents;}
        }

        private void OnSessionChanged()
        {
            if(Session==null)return;
            state=Session.State;
            uid=state?.Dragons.FirstOrDefault(d=>d.SpeciesId=="emberling" && d.Location.Kind==LocationKind.Zone && d.Location.ZoneId=="origin")?.Uid;
            if(!livingMode && state!=null && foodId==null)
            {
                var drop=state.Food.Drops.FirstOrDefault(d=>d.ZoneId=="origin" && d.HabitatId==HabitatId && d.DietId==DietId.Fruit);
                if(drop!=null)
                {
                    foodId=drop.Id;
                    if(fruit!=null){fruit.transform.position=SafeFruitPosition()+Vector3.up*.12f;fruit.SetActive(true);}
                }
            }
            SyncFood();
            Refresh();
        }

        private void OnDomainEvents(IReadOnlyList<DomainEvent> events)
        {
            foreach(var e in events)
            {
                if(e is OfflineReceiptCreated offline)SetMessage("Offline: +"+GoldText(offline.GoldMicro)+" G","Offline: +"+GoldText(offline.GoldMicro)+" G");
                else if(e is ExpeditionReady)SetMessage("Expedition returned; claim materials.","Rồng đã về; hãy nhận vật liệu.");
                else if(e is RewardEggDelivered delivered)SetMessage("Queued reward egg delivered; check Hatchery.","Trứng thưởng chờ đã được giao; kiểm tra ô ấp.");
                else if(e is NightJournalWritten journal)SetMessage("Night journal: "+journal.Findings.Count+" findings.","Nhật ký đêm: "+journal.Findings.Count+" phát hiện.");
                else if(e is AchievementUnlocked achievement)SetMessage("Achievement: +"+GoldText(achievement.RewardGoldMicro)+" G","Thành tựu: +"+GoldText(achievement.RewardGoldMicro)+" G");
                else if(e is FoodExpired expired && expired.FoodId==foodId)SetMessage("Food expired.","Thức ăn đã hết hạn.");
            }
        }

        private static string GoldText(long micro)=>(micro/(decimal)MicroGold.MicroPerGold).ToString("0.00",CultureInfo.InvariantCulture);
        private void SyncFood()
        {
            if(foodId!=null && !HasFood){foodId=null;if(fruit!=null)fruit.SetActive(false);}
        }

        private void OnApplicationFocus(bool value){focused=value;skipFrame=true;}
        private void OnApplicationPause(bool value){paused=value;skipFrame=true;}
        private void Update()
        {
            if(persistentMode || !CanInteract)return;
            if(skipFrame){skipFrame=false;return;}
            double dt=Time.unscaledDeltaTime;
            if(dt<=0 || dt>5)return;
            fractionalMs+=dt*1000;
            long delta=(long)fractionalMs;
            fractionalMs-=delta;
            clock.Advance(delta);
            if(clock.NowMs()>=nextTick)
            {
                nextTick=clock.NowMs()+1000;
                Dispatch(new AdvanceTimeCommand(Id(),AdvanceMode.Online));
            }
        }

        public void SetVietnamese(bool value){vi=value;Refresh();if(livingPresentation!=null)livingPresentation.SetVietnamese(value);}
        private string Id()=>"preview-"+(++request).ToString(CultureInfo.InvariantCulture);

        private DispatchResult Dispatch(GameCommand command)
        {
            var result=CommandDispatcher.Dispatch(state,command,clock);
            if(result.Ok)state=result.State;
            else ShowError(result.Error);
            if(foodId!=null && !HasFood)
            {
                SyncFood();
                if(result.Events.Any(e=>e is FoodExpired))SetMessage("Food expired.","Thức ăn đã hết hạn.");
            }
            Refresh();
            return result;
        }

        private IReadOnlyList<DomainEvent> Send(Func<string,GameCommand> build)
        {
            if(!CanSend)return null;
            if(!persistentMode){var r=Dispatch(build(Id()));return r.Ok?r.Events:null;}
            IReadOnlyList<DomainEvent> received=Array.Empty<DomainEvent>();
            void Capture(IReadOnlyList<DomainEvent> events){received=events;}
            Session.DomainEvents+=Capture;
            DomainError error;
            try{error=Session.Send(build);}
            finally{Session.DomainEvents-=Capture;}
            if(error!=null){ShowError(error);return null;}
            OnSessionChanged();
            return received;
        }

        private Vector3 SafeFruitPosition()
        {
            var here=transform.position;
            if(locations==null)return spawn+new Vector3(.25f,0,.55f);
            foreach(var offset in new[]{transform.forward*.7f,transform.right*.7f,-transform.right*.7f,-transform.forward*.7f,Vector3.zero})
            {
                var point=here+offset;
                if(locations.IsWalkable(point,HabitatId) && SegmentWalkable(here,point))return point;
            }
            return here;
        }

        public bool SegmentWalkable(Vector3 a,Vector3 b)
        {
            if(locations==null)return true;
            int steps=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(a,b)/.15f));
            for(int i=0;i<=steps;i++)
                if(!locations.IsWalkable(Vector3.Lerp(a,b,(float)i/steps),HabitatId))return false;
            return true;
        }

        public bool TryWanderTarget(int index,out Vector3 target)
        {
            target=transform.position;
            if(locations==null)return false;
            var candidates=locations.SpawnPointsIn(HabitatId).Where(p=>locations.IsWalkable(p.Position,HabitatId)).ToArray();
            for(int i=0;i<candidates.Length;i++)
            {
                var p=candidates[(index+i)%candidates.Length].Position;
                if(Vector3.Distance(transform.position,p)>.12f && SegmentWalkable(transform.position,p)){target=p;return true;}
            }
            return false;
        }

        private void GuideCommand(Func<string,GameCommand> build,string en,string vn)
        {
            var events=Send(build);
            if(events!=null)
            {
                var reward=events.OfType<AchievementUnlocked>().Sum(e=>e.RewardGoldMicro);
                string bonus=reward>0?" (+"+GoldText(reward)+" G)":"";
                var eggs=events.OfType<RewardEggGranted>().ToArray();
                string enEgg=string.Join("\n",eggs.Select(x=>ExpeditionPresentation.EggGrant(x,false)));
                string viEgg=string.Join("\n",eggs.Select(x=>ExpeditionPresentation.EggGrant(x,true)));
                SetMessage(en+bonus+(eggs.Length>0?"\n"+enEgg:""),vn+bonus+(eggs.Length>0?"\n"+viEgg:""));
            }
        }
        public void ViewBiology(){GuideCommand(id=>new ViewBiologyProfileCommand(id,"emberling"),"Profile recorded.","Đã ghi nhận hồ sơ.");}
        public void SurveyRocks(){GuideCommand(id=>new SurveyHabitatCommand(id,"origin_warm_rocks"),"Survey complete.","Đã khảo sát bãi đá ấm.");}
        public void UnlockRocks(){GuideCommand(id=>new UnlockHabitatCommand(id,"origin_warm_rocks"),"Habitat unlocked.","Đã mở khóa bãi đá ấm.");}
        public void SurveyPond(){GuideCommand(id=>new SurveyHabitatCommand(id,"origin_pondbank"),"Survey complete.","Đã khảo sát bờ hồ.");}
        public void UnlockPond(){GuideCommand(id=>new UnlockHabitatCommand(id,"origin_pondbank"),"Habitat unlocked.","Đã mở khóa bờ hồ.");}
        public void BuyStarter(){GuideCommand(id=>new BuyTutorialPackCommand(id),"Starter purchased.","Đã mua Starter.");}
        public void ClaimMeadow(){GuideCommand(id=>new ClaimPackCommand(id,MoveTarget.Zone("origin","origin_meadow")),"Egg claimed to meadow.","Đã nhận rồng vào đồng cỏ.");}
        public void ClaimStorage(){GuideCommand(id=>new ClaimPackCommand(id,MoveTarget.Storage),"Egg claimed to storage.","Đã nhận rồng vào kho.");}
        // Hatchery uses the same session, no second clock, random roll or currency.
        public void BuyStandardStarter()=>BuyPack("starter");
        public void BuyElemental()=>BuyPack("elemental");
        public void BuyMystic()=>BuyPack("mystic");
        public void BuyAstral()=>BuyPack("astral");
        private void BuyPack(string packId)
        {
            if(!persistentMode)return;
            GuideCommand(id=>new BuyPackCommand(id,packId),"Pack purchased; claim the egg.","Đã mua gói; hãy nhận trứng.");
        }

        private DragonState[] AdultCandidates()=>state==null?Array.Empty<DragonState>():
            state.Dragons.Where(d=>d.Location.Kind!=LocationKind.Released && Lifecycle.StageOf(d)==GrowthStage.Adult).ToArray();

        private string NextParent(string previous,string other)
        {
            var choices=AdultCandidates().Where(d=>d.Uid!=other).ToArray();
            if(choices.Length==0)return null;
            int index=Array.FindIndex(choices,d=>d.Uid==previous);
            return choices[(index+1)%choices.Length].Uid;
        }
        public void SelectParentA(){parentA=NextParent(parentA,parentB);RefreshHatchery();}
        public void SelectParentB(){parentB=NextParent(parentB,parentA);RefreshHatchery();}
        public void StartBreeding()
        {
            if(!persistentMode || !CanSend || parentA==null || parentB==null)return;
            GuideCommand(id=>new StartBreedCommand(id,parentA,parentB),"Incubation started.","Đã bắt đầu ấp trứng.");
        }
        public void TapBreedingEgg()
        {
            if(persistentMode)GuideCommand(id=>new TapEggCommand(id),"Egg tapped.","Đã chạm trứng.");
        }
        public void ClaimBreedMeadow()
        {
            if(persistentMode)GuideCommand(id=>new ClaimBreedCommand(id,MoveTarget.Zone("origin","origin_meadow")),"Hatched into meadow.","Đã nhận rồng lai vào đồng cỏ.");
        }
        public void ClaimBreedStorage()
        {
            if(persistentMode)GuideCommand(id=>new ClaimBreedCommand(id,MoveTarget.Storage),"Hatched into storage.","Đã nhận rồng lai vào kho.");
        }
        private string ParentLabel(string id)
        {
            var d=state?.FindDragon(id);
            if(d==null)return vi?"Chưa chọn":"Not selected";
            return (string.IsNullOrEmpty(d.Nickname)?d.SpeciesId:d.Nickname)+" #"+d.Uid.Substring(0,Math.Min(6,d.Uid.Length));
        }
        private void RefreshHatchery()
        {
            if(hatcheryStatus==null || state==null)return;
            var egg=state.BreedingNest;
            string pending=state.PendingPack==null?(vi?"Chưa có gói chờ":"No pending pack"):
                (vi?"Gói chờ nhận: ":"Pending pack: ")+state.PendingPack.PackId;
            string nest=vi?"Tổ ấp trống":"Nest empty";
            if(egg!=null)
            {
                long left=Math.Max(0,egg.FinishAtMs-state.Clock.LastProcessedAtMs);
                nest=egg.Status==EggStatus.Ready?(vi?"Trứng đã sẵn sàng nhận":"Egg ready to claim"):
                    (vi?"Đang ấp: ":"Incubating: ")+TimeSpan.FromMilliseconds(left).ToString(@"hh\:mm\:ss");
            }
            var economy=ProgressionQueries.SanctuaryEconomy(state,state.Clock.LastProcessedAtMs);
            hatcheryStatus.text=pending+"\n"+nest+"\n"+(vi?"Starter còn hôm nay: ":"Starter left today: ")+economy.StarterPacksLeftToday+"/"+economy.StarterDailyLimit;
            if(standardStarter!=null)standardStarter.interactable=CanSend && persistentMode && economy.StarterPacksLeftToday>0 && state.PendingPack==null;
            if(parentsStatus==null)return;
            var adults=AdultCandidates();
            if(!adults.Any(d=>d.Uid==parentA))parentA=null;
            if(!adults.Any(d=>d.Uid==parentB))parentB=null;
            string details="A: "+ParentLabel(parentA)+"\nB: "+ParentLabel(parentB);
            if(parentA!=null && parentB!=null)
            {
                var preview=Hatchery.Preview(state,parentA,parentB,state.Clock.LastProcessedAtMs,out var error);
                if(error!=null || preview==null)details+="\n"+(vi?"Chưa thể lai: ":"Cannot breed: ")+error;
                else details+="\n"+(vi?"Phí: ":"Fee: ")+preview.FeeG.ToString(CultureInfo.InvariantCulture)+" G";
            }
            else details+="\n"+(vi?"Chọn hai rồng trưởng thành.":"Select two adult dragons.");
            parentsStatus.text=details;
        }

        // All mutations use the existing session and the canonical Domain command bus.
        public void BuildSanctuaryProject()
        {
            if(!persistentMode || !CanSend)return;
            var economy=ProgressionQueries.SanctuaryEconomy(state,state.Clock.LastProcessedAtMs);
            if(economy.NextProjectBlockedBy!=null || !economy.NextProjectCostGoldMicro.HasValue)return;
            GuideCommand(id=>new BuildSanctuaryProjectCommand(id),"Project recorded; decoration art pending.","Đã ghi nhận dự án; hình ảnh trang trí chưa có.");
        }
        public void UnlockOcean(){if(persistentMode)GuideCommand(id=>new UnlockZoneCommand(id,"ocean"),"Ocean unlocked.","Đã mở Thủy Vực.");}
        public void UnlockSky(){if(persistentMode)GuideCommand(id=>new UnlockZoneCommand(id,"sky"),"Sky unlocked.","Đã mở Thiên Giới.");}
        public void ClaimExpeditionReward()
        {
            if(persistentMode && CanSend && ExpeditionPresentation.CanClaim(state))
                GuideCommand(id=>new ClaimExpeditionCommand(id),"Expedition reward claimed.","Đã nhận thưởng thám hiểm.");
        }
        public void NextExpeditionRoute()
        {
            if(!CanSend||!persistentMode)return;
            var options=Ecosystem.ExpeditionOptions(state);
            expeditionRouteId=ResearchDiscoveryView.Next(options.Select(r=>r.RouteId),expeditionRouteId);
            RefreshManagement();
        }
        public void NextExpeditionDragon()
        {
            if(!CanSend||!persistentMode)return;
            var route=ExpeditionPresentation.Route(state,expeditionRouteId);
            if(route==null)return;
            expeditionDragonUid=ResearchDiscoveryView.Next(route.Candidates.Select(c=>c.DragonUid),expeditionDragonUid);
            RefreshManagement();
        }
        public void StartSelectedExpedition()
        {
            if(!CanSend||!persistentMode)return;
            string route=expeditionRouteId,dragon=expeditionDragonUid;
            if(!ExpeditionPresentation.CanStart(state,route,dragon,out string reason))
            {SetMessage(ExpeditionPresentation.Reason(reason,false),ExpeditionPresentation.Reason(reason,true));RefreshManagement();return;}
            GuideCommand(id=>new StartExpeditionCommand(id,route,dragon),
                "Expedition started; dragon returns automatically, rewards require claim.",
                "Đã khởi hành; rồng tự trở về, phần thưởng cần nhận.");
        }
        void RefreshExpedition(bool ready)
        {
            if(expeditionRouteText==null)return;
            nextExpeditionRoute.interactable=nextExpeditionDragon.interactable=startExpedition.interactable=false;
            if(state==null){expeditionRouteText.text=expeditionDragonText.text="—";return;}
            var options=Ecosystem.ExpeditionOptions(state);
            if(expeditionRouteId==null)expeditionRouteId=options.FirstOrDefault()?.RouteId;
            var route=options.FirstOrDefault(r=>r.RouteId==expeditionRouteId);
            if(route==null){expeditionRouteText.text=expeditionDragonText.text="—";nextExpeditionRoute.interactable=ready&&options.Count>0;return;}
            if(expeditionDragonUid==null)expeditionDragonUid=route.Candidates.FirstOrDefault()?.DragonUid;
            var dragon=state.FindDragon(expeditionDragonUid);
            var candidate=route.Candidates.FirstOrDefault(c=>c.DragonUid==expeditionDragonUid);
            bool eligible=ExpeditionPresentation.CanStart(state,route.RouteId,expeditionDragonUid,out string reason);
            var progress=ProgressionQueries.ExplorationRoutes(state).FirstOrDefault(x=>x.RouteId==route.RouteId);
            expeditionRouteText.text=(vi?route.NameVi:route.RouteId)+" · "+ZoneName(route.ZoneId)+
                "\n"+ExpeditionPresentation.Remaining(route.DurationMs)+" · "+route.Requirement+
                "\n"+(route.Materials.Count==0?(vi?"Bảng thưởng chưa duyệt":"Reward table unavailable"):
                    (vi?"Vật liệu có thể nhận: ":"Possible materials: ")+string.Join(", ",route.Materials.Select(x=>ExpeditionPresentation.MaterialName(x,vi)))+" · "+route.MinQty+"–"+route.MaxQty)+
                "\n"+(route.Blocked.HasValue?ExpeditionPresentation.Reason(route.Blocked.Value.ToWire(),vi):(vi?"Tuyến khả dụng":"Route available"))+
                "\n"+ExpeditionPresentation.RouteProgress(progress,vi);
            expeditionDragonText.text=(dragon==null||candidate==null?(vi?"Chưa chọn rồng hợp lệ":"No valid dragon selected"):
                dragon.SpeciesId+" · "+Lifecycle.StageOf(dragon)+"\nUID: "+dragon.Uid)+
                "\n"+ExpeditionPresentation.Reason(reason,vi);
            nextExpeditionRoute.interactable=ready&&options.Count>1;
            nextExpeditionDragon.interactable=ready&&(route.Candidates.Count>1||(candidate==null&&route.Candidates.Count>0));
            startExpedition.interactable=ready&&eligible;
        }
        public void BuySanctuaryUpgrade(string upgradeId)
        {
            if(persistentMode)GuideCommand(id=>new BuyUpgradeCommand(id,upgradeId),"Upgrade purchased.","Đã mua nâng cấp.");
        }

        private string ZoneName(string id)=>id switch
        {
            "origin"=>vi?"Khởi Nguyên":"Origin",
            "ocean"=>vi?"Thủy Vực":"Ocean",
            "sky"=>vi?"Thiên Giới":"Sky",
            _=>id
        };
        private string UpgradeName(string id)=>id switch
        {
            "islandCapacityLevel"=>vi?"Ngân sách sản xuất":"Production budget",
            "foodQualityLevel"=>vi?"Chất lượng thức ăn":"Food quality",
            "foodCooldownLevel"=>vi?"Số thức ăn đồng thời":"Food capacity",
            "mutationLuckLevel"=>vi?"May mắn biến thể":"Mutation luck",
            "hatchSpeedLevel"=>vi?"Tốc độ ấp":"Hatch speed",
            _=>id
        };
        private string RequirementName(string id)
        {
            if(id=="tutorial-completed")return vi?"Hoàn thành hướng dẫn":"Complete tutorial";
            if(id=="origin-habitats")return vi?"Mở đủ sinh cảnh Khởi Nguyên":"Unlock Origin habitats";
            if(id.StartsWith("zone:",StringComparison.Ordinal))return (vi?"Mở ":"Unlock ")+ZoneName(id.Substring(5));
            if(id.StartsWith("quest:",StringComparison.Ordinal))return (vi?"Nhận nhiệm vụ: ":"Claim quest: ")+ResearchDiscoveryView.QuestName(id.Substring(6),vi);
            return id;
        }

        private DragonState[] HealthDragons()=>state==null?Array.Empty<DragonState>():
            state.Dragons.Where(d=>d.Location.Kind!=LocationKind.Released).ToArray();

        public void SelectNextHealthDragon()
        {
            var dragons=HealthDragons();
            if(dragons.Length==0)return;
            int index=Array.FindIndex(dragons,d=>d.Uid==healthUid);
            healthUid=dragons[(index+1)%dragons.Length].Uid;
            RefreshHealth();
        }
        public void SelectNextHealthPlan()
        {
            if(state==null || healthCaseId==null)return;
            var plans=HealthQueries.CarePlans(state,healthCaseId);
            if(plans.Count==0)return;
            int index=plans.FindIndex(p=>p.PlanId==healthPlanId);
            healthPlanId=plans[(index+1)%plans.Count].PlanId;
            RefreshHealth();
        }
        public void ShelterHealthDragon()
        {
            if(persistentMode && healthUid!=null)GuideCommand(id=>new MoveToShelterCommand(id,healthUid),"Dragon sheltered.","Đã đưa rồng vào nơi trú.");
        }
        public void AssessHealthCase()
        {
            if(persistentMode && healthCaseId!=null)GuideCommand(id=>new AssessCaseCommand(id,healthCaseId),"Case assessed.","Đã nhận định ca.");
        }
        public void ApplyHealthPlan()
        {
            if(persistentMode && healthCaseId!=null && healthPlanId!=null)
                GuideCommand(id=>new SetCarePlanCommand(id,healthCaseId,healthPlanId),"Plan selected.","Đã chọn kế hoạch.");
        }
        public void StartHealthCare()
        {
            if(persistentMode && healthCaseId!=null)GuideCommand(id=>new StartCareCommand(id,healthCaseId),"Care started.","Đã bắt đầu chăm sóc.");
        }
        public void ReviewHealthCase()
        {
            if(persistentMode && healthCaseId!=null)GuideCommand(id=>new ReviewCaseCommand(id,healthCaseId),"Recovery reviewed.","Đã lượng giá phục hồi.");
        }
        public void DischargeHealthCase()
        {
            if(persistentMode && healthCaseId!=null)GuideCommand(id=>new DischargeDragonCommand(id,healthCaseId,MoveTarget.Storage),"Discharged to storage.","Đã xuất viện về kho.");
        }

        private void RefreshHealth()
        {
            if(healthStatus==null)return;
            bool ready=persistentMode && CanSend;
            healthNext.interactable=healthShelter.interactable=healthAssess.interactable=healthNextPlan.interactable=
                healthApplyPlan.interactable=healthStart.interactable=healthReview.interactable=healthDischarge.interactable=false;
            if(state==null)return;
            var dragons=HealthDragons();
            if(!dragons.Any(d=>d.Uid==healthUid))healthUid=dragons.FirstOrDefault()?.Uid;
            var selected=dragons.FirstOrDefault(d=>d.Uid==healthUid);
            var station=HealthQueries.Station(state);
            var disasters=HealthQueries.Disasters(state);
            string stationText=(vi?"Trạm: ":"Station: ")+station.Used+"/"+station.Slots+
                (vi?" · Chờ: ":" · Queue: ")+station.Queue.Count;
            string warning=disasters.NaturalDisastersEnabled?(vi?"Thiên tai tự nhiên bật":"Natural disasters enabled"):
                (vi?"Thiên tai tự nhiên tắt · chờ ADR 0202":"Natural disasters disabled · ADR 0202 pending");
            if(selected==null)
            {
                healthStatus.text=stationText+"\n"+warning;
                careCaseStatus.text=vi?"Không có rồng.":"No dragons.";
                carePlanStatus.text="—";healthCaseId=healthPlanId=null;return;
            }
            healthNext.interactable=dragons.Length>1;
            var info=HealthQueries.Dragon(state,selected.Uid);
            var v=info.Vitals;
            healthStatus.text=ParentLabel(selected.Uid)+"\n"+stationText+"\n"+warning+
                (v==null?"\n—":(vi?"\nSức khỏe ":"\nHealth ")+v.Health+"/100 · "+(vi?"Vận động ":"Mobility ")+v.Mobility+"/100"+
                (vi?"\nTiện nghi ":"\nComfort ")+v.Comfort+"/100 · "+(vi?"Năng lượng ":"Energy ")+v.Energy+"/100"+
                (vi?"\nĐủ nước ":"\nHydration ")+v.Hydration+"/100")+
                (info.ExposedToWarning?(vi?"\nCó cảnh báo · nên trú ẩn":"\nWarning · shelter advised"):"");
            healthShelter.interactable=ready && DisasterEngine.CheckMoveToShelter(state,selected)==null;
            var c=HealthQueries.Cases(state).FirstOrDefault(x=>x.DragonUid==healthUid);
            if(c==null)
            {
                healthCaseId=healthPlanId=null;
                careCaseStatus.text=vi?"Không có ca chăm sóc mở.":"No open care case.";
                carePlanStatus.text="—";return;
            }
            if(healthCaseId!=c.CaseId){healthCaseId=c.CaseId;healthPlanId=c.CarePlanId;}
            careCaseStatus.text=c.Site+" · "+c.Severity+"\n"+c.Status+" · "+c.Phase+
                " · "+(c.Progress/10.0).ToString("0.0",CultureInfo.InvariantCulture)+"%"+
                (c.QueuePosition>0?"\n"+(vi?"Vị trí chờ: ":"Queue position: ")+c.QueuePosition:"")+
                (c.Symptoms.Count>0?"\n"+string.Join(", ",c.Symptoms):"")+
                "\n"+(vi?"Bước tiếp: ":"Next: ")+c.NextStep;
            healthAssess.interactable=ready && CareCenter.CheckAssess(state,c.CaseId)==null;
            healthStart.interactable=ready && CareCenter.CheckStart(state,c.CaseId)==null;
            healthReview.interactable=ready && CareCenter.CheckReview(state,c.CaseId)==null;
            healthDischarge.interactable=ready && HealthQueries.DischargeEligibility(state,c.CaseId,MoveTarget.Storage)==null;
            var plans=HealthQueries.CarePlans(state,c.CaseId);
            if(!plans.Any(p=>p.PlanId==healthPlanId))healthPlanId=plans.FirstOrDefault()?.PlanId;
            var plan=plans.FirstOrDefault(p=>p.PlanId==healthPlanId);
            carePlanStatus.text=plan==null?"—":plan.PlanId+"\n"+(plan.Suitable?(vi?"Phù hợp":"Suitable"):(vi?"Không phù hợp":"Unsuitable"))+
                "\n"+plan.ExplanationKey+"\n"+(vi?"Đã chọn: ":"Applied: ")+(c.CarePlanId??"—");
            healthNextPlan.interactable=plans.Count>1 && c.Assessed && c.NextStep=="setCarePlan|startCare";
            healthApplyPlan.interactable=ready && plan!=null && CareCenter.CheckSetPlan(state,c.CaseId,plan.PlanId,out _)==null;
        }

        private void RefreshManagement()
        {
            if(managementSummary==null)return; // Older generated scenes do not have this page.
            bool ready=CanSend && persistentMode;
            unlockOceanButton.interactable=false;
            unlockSkyButton.interactable=false;
            claimExpeditionButton.interactable=false;
            foreach(var button in upgradeButtons)button.interactable=false;
            if(projectBuild!=null)projectBuild.interactable=false;
            RefreshExpedition(ready);
            if(state==null)return;

            if(projectStatus!=null)
            {
                var e=ProgressionQueries.SanctuaryEconomy(state,state.Clock.LastProcessedAtMs);
                projectStatus.text=(vi?"Dự án trang trí: ":"Decorative projects: ")+e.ProjectsBuilt+"/"+e.ProjectsMax+
                    "\n"+(e.NextProjectCostGoldMicro.HasValue?(vi?"Giá tiếp theo: ":"Next cost: ")+GoldText(e.NextProjectCostGoldMicro.Value)+" G":"—")+
                    "\n"+(e.NextProjectMaterialEach>0?e.NextProjectMaterialEach+" × "+string.Join(", ",e.NextProjectMaterialIds):"")+
                    "\n"+(vi?"Thiếu vật liệu: ":"Missing materials: ")+string.Join(", ",ExpeditionPresentation.ProjectShortfalls(state,e).Select(m=>m.Key+" × "+m.Value))+
                    "\n"+(e.NextProjectBlockedBy??"")+
                    "\n"+(vi?"Hình ảnh trang trí chưa triển khai.":"Decoration art not implemented.");
                if(projectBuild!=null)projectBuild.interactable=ready && e.NextProjectCostGoldMicro.HasValue && e.NextProjectBlockedBy==null;
            }
            var summary=Ecosystem.Summary(state);
            managementSummary.text=(vi?"Rồng: ":"Dragons: ")+summary.Living+
                "\n"+(vi?"Sản xuất: ":"Producing: ")+summary.Producing+"/"+summary.ProductionBudget+
                "\n"+(vi?"Kho: ":"Storage: ")+summary.StorageUsed+"/"+summary.StorageLimit+
                "\n"+GoldText(summary.RateMicroGPerSec)+" G/s";
            var unlocks=Ecosystem.ZoneUnlocks(state);
            managementWorld.text=string.Join("\n\n",Ecosystem.World(state).Select(z=>
            {
                var rule=unlocks.FirstOrDefault(x=>x.ZoneId==z.ZoneId);
                string detail=ZoneName(z.ZoneId)+" · "+(z.Unlocked?(vi?"Đã mở":"Unlocked"):(vi?"Đã khóa":"Locked"))+
                    " · "+z.Occupancy+"/"+z.Capacity;
                if(rule!=null && !rule.Unlocked && rule.Missing.Count>0)
                    detail+="\n"+string.Join("; ",rule.Missing.Select(RequirementName));
                if(rule!=null && rule.RewardQueued)detail+="\n"+(vi?"Trứng thưởng đang chờ":"Reward egg queued");
                return detail;
            }));
            unlockOceanButton.interactable=ready && unlocks.Any(x=>x.ZoneId=="ocean" && !x.Unlocked && x.Blocked==null);
            unlockSkyButton.interactable=ready && unlocks.Any(x=>x.ZoneId=="sky" && !x.Unlocked && x.Blocked==null);

            for(int i=0;i<EconomyTables.Upgrades.Count && i<upgradeButtons.Length && i<upgradeStatus.Length;i++)
            {
                var def=EconomyTables.Upgrades[i];
                int level=def.LevelOf(state.Player.Upgrades);
                long? cost=EconomyRates.UpgradeCostG(def,level);
                upgradeStatus[i].text=UpgradeName(def.Id)+" · "+level+"/"+def.Max+"\n"+
                    (cost.HasValue?cost.Value.ToString(CultureInfo.InvariantCulture)+" G":(vi?"Đã tối đa":"Max level"));
                upgradeButtons[i].interactable=ready && SanctuaryRules.CheckUpgrade(state,def.Id)==null;
            }
            var job=state.Expedition;
            if(job==null)managementExpedition.text=vi?"Chưa có chuyến thám hiểm.":"No active expedition.";
            else
            {
                long left=Math.Max(0,job.FinishAtMs-state.Clock.LastProcessedAtMs);
                managementExpedition.text=job.RouteId+"\nUID: "+job.DragonUid+"\n"+(job.Status==ExpeditionStatus.Ready?
                    (vi?"Rồng đã về · Có thể nhận thưởng":"Dragon returned · Ready to claim")+"\n"+string.Join(", ",job.Rewards.Select(m=>m.Key+" × "+m.Value)):
                    (vi?"Đang đi · Tạm ngừng sinh vàng":"Away · No gold production")+"\n"+ExpeditionPresentation.Remaining(left));
                claimExpeditionButton.interactable=ready && ExpeditionPresentation.CanClaim(state);
            }
            managementMaterials.text=string.Join("\n",Ecosystem.Materials(state).Where(m=>m.Active||m.Quantity>0).Select(m=>
                (vi?m.NameVi:m.MaterialId)+": "+m.Quantity+(m.Active?"":(vi?" (chưa kích hoạt)":" (inactive)"))));
            var discovery=Ecosystem.Discovery(state);
            managementCodex.text=(vi?"Loài: ":"Species: ")+discovery.SpeciesDiscovered+"/"+discovery.SpeciesTotal+
                " · "+(vi?"Biến thể: ":"Variants: ")+discovery.CodexCells+"/"+discovery.CodexCellsTotal+
                "\n"+(vi?"Thành tựu: ":"Achievements: ")+discovery.AchievementsUnlocked.Count+
                "\n\n"+string.Join("\n",SpeciesCatalog.All.Select(sp=>
                {
                    var entry=discovery.Codex.FirstOrDefault(e=>e.SpeciesId==sp.Id);
                    return sp.Id+" · "+(entry==null?"0/4":entry.DiscoveredMutations.Count+"/4");
                }));
        }

        public void SaveGame(){if(Session!=null && Session.Status==SessionStatus.Ready)Session.SaveNow();}

        public bool RequestFood()
        {
            if(!CanInteract)return false;
            if(HasFood){SetMessage("One fruit is already available in this preview.","Đã có một quả trong bản thử.");return false;}
            var point=SafeFruitPosition();
            if(locations!=null && !locations.IsWalkable(point,HabitatId))return false;
            var events=Send(id=>new FeedCommand(id,DietId.Fruit,"origin",HabitatId));
            var drop=events?.OfType<FoodSpawned>().FirstOrDefault();
            if(drop==null)return false;
            foodId=drop.FoodId;
            nextConsumeAttempt=0;
            if(fruit!=null)
            {
                fruit.transform.position=point+Vector3.up*.12f;
                fruit.SetActive(true);
            }
            SetMessage("Fruit placed — Emberling will approach it.","Đã thả trái cây — Emberling sẽ đến gần.");
            return true;
        }

        public bool TryConsume()
        {
            long now=persistentMode?(long)(Time.realtimeSinceStartupAsDouble*1000):clock.NowMs();
            if(!CanInteract || !HasFood || now<nextConsumeAttempt)return false;
            Vector3 distance=FoodPosition-transform.position;distance.y=0;
            if(distance.sqrMagnitude>.55f*.55f)return false;
            nextConsumeAttempt=now+500;
            string requestedFood=foodId;
            var events=Send(id=>new ConsumeFoodCommand(id,requestedFood,uid));
            var consumed=events?.OfType<FoodConsumed>().FirstOrDefault();
            if(consumed==null)return false;
            SetMessage("Consumed fruit: +"+consumed.XpGainedSec+" XP","Đã ăn trái cây: +"+consumed.XpGainedSec+" XP");
            return true;
        }

        public bool TryPet()
        {
            if(!CanInteract)return false;
            var events=Send(id=>new PetCommand(id,uid));
            if(events==null)return false;
            bool gained=events.Any(e=>e is BondChanged);
            SetMessage(gained?"Bond increased.":"Petted — bond cap reached.",gained?"Gắn kết tăng.":"Đã vuốt ve — đạt giới hạn gắn kết.");
            return events.Any(e=>e is DragonPetted);
        }

        private void ShowError(DomainError e)
        {
            string code=e?.WireCode??"unknown";
            switch(code)
            {
                case "pack-daily-limit":SetMessage("Daily Starter limit reached (UTC).","Đã hết lượt Starter hôm nay (UTC).");break;
                case "pet-cooldown":SetMessage("Pet cooldown: 30 seconds.","Vuốt ve: chờ đủ 30 giây.");break;
                case "feed-cooldown":SetMessage("Eating cooldown: 8 seconds.","Ăn: chờ đủ 8 giây.");break;
                case "food-too-fast":SetMessage("Please wait before dropping food.","Chờ trước khi thả thêm thức ăn.");break;
                default:SetMessage("Command rejected: "+code,"Lệnh bị từ chối: "+code);break;
            }
        }

        private void SetMessage(string en,string vn){messageEn=en;messageVi=vn;if(notice!=null)notice.text=vi?vn:en;if(guideNotice!=null)guideNotice.text=vi?vn:en;if(hatcheryNotice!=null)hatcheryNotice.text=vi?vn:en;if(managementFeedback!=null)managementFeedback.text=vi?vn:en;}
        private void Refresh()
        {
            if(notice!=null)notice.text=vi?messageVi:messageEn;
            if(guideNotice!=null)guideNotice.text=vi?messageVi:messageEn;
            if(hatcheryNotice!=null)hatcheryNotice.text=vi?messageVi:messageEn;
            if(managementFeedback!=null)managementFeedback.text=vi?messageVi:messageEn;
            RefreshManagement();
            RefreshHealth();
            if(saveStatus!=null && Session!=null)
            {
                saveStatus.text=Session.Status!=SessionStatus.Ready?(vi?"Phiên: ":"Session: ")+Session.Status:
                    !Session.Persistent?(vi?"Không có lưu bền":"Non-persistent storage"):
                    Session.SaveError?(vi?"Lỗi lưu":"Save failed"):
                    Session.LastSavedAtMs.HasValue?(vi?"Đã lưu ":"Saved ")+DateTimeOffset.FromUnixTimeMilliseconds(Session.LastSavedAtMs.Value).ToLocalTime().ToString("HH:mm:ss"):(vi?"Chờ lưu":"Not saved yet");
            }
            if(state==null)return;
            RefreshHatchery();
            if(gold!=null)gold.text=GoldText(state.Player.Gold.Micro)+" G";
            if(tutorialStatus!=null)
            {
                string step=Tutorial.CurrentStep(state.Tutorial);
                string instruction=step switch {
                    "TUT_01"=>vi?"Quan sát 15 giây":"Observe for 15 seconds",
                    "TUT_02"=>vi?"Cho Emberling ăn":"Feed Emberling",
                    "TUT_03"=>vi?"Ghi nhận hồ sơ sinh học":"Record biology profile",
                    "TUT_04"=>vi?"Khảo sát và mở bãi đá ấm":"Survey and unlock warm rocks",
                    "TUT_05"=>vi?"Mua và nhận Starter":"Buy and claim Starter",
                    "TUT_06"=>vi?"Khảo sát và mở bờ hồ":"Survey and unlock pondbank",
                    "TUT_07"=>vi?"Nuôi rồng trưởng thành":"Raise an adult",
                    "TUT_08"=>vi?"Mở đủ sinh cảnh Origin":"Unlock all Origin habitats",
                    _=>state.Tutorial.Status.ToString()};
                tutorialStatus.text=(vi?"Hướng dẫn ":"Tutorial ")+state.Tutorial.Completed.Count+"/8 · "+instruction+
                    (state.PendingPack!=null?(vi?" · Có trứng chờ nhận":" · Egg pending"):"");
            }
            if(rosterStatus!=null)rosterStatus.text=string.Join("\n",state.Dragons.Where(d=>d.Location.Kind!=LocationKind.Released).Select(d=>d.SpeciesId+" · "+(d.Location.Kind==LocationKind.Storage?(vi?"Kho":"Storage"):d.Location.HabitatId)+" · "+Lifecycle.StageOf(d)));
            if(livingMode)return; // The selected resident profile is owned by the living presenter.
            var dragon=state.FindDragon(uid);
            if(dragon==null){if(progress!=null)progress.text=vi?"Không có Emberling tại Origin":"No Emberling in Origin";return;}
            var species=SpeciesCatalog.Get(dragon.SpeciesId);
            var stage=Lifecycle.StageOf(dragon);
            string stageText=vi?(stage==GrowthStage.Baby?"Rồng non":stage==GrowthStage.Juvenile?"Rồng trẻ":"Trưởng thành"):stage.ToString();

            if(progress!=null)progress.text=stageText+"  |  XP "+dragon.XpSec.ToString("0.0",CultureInfo.InvariantCulture)+
                "/"+species.GrowthTimeSec+"  |  "+(vi?"Gắn kết ":"Bond ")+dragon.Bond+"/100";
        }
    }
}
