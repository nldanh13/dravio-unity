using System;
using System.Globalization;
using System.Linq;
using Dravio.Domain;
using Dravio.Domain.Biology;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.World;
using Dravio.Domain.Progression;
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
        public bool CanInteract => state!=null && state.FindDragon(uid)!=null && focused && !paused && (!persistentMode || (Session!=null && Session.Status==SessionStatus.Ready && !Session.Hidden));

        public void Configure(Text goldLabel,Text progressLabel,Text noticeLabel,GameObject fruitVisual)
        {gold=goldLabel;progress=progressLabel;notice=noticeLabel;fruit=fruitVisual;}

        public void ConfigurePersistent(DravioSessionHost sessionHost,SanctuaryLocationData locationData,Text saved,Text tutorial,Text roster,Text guideFeedback)
        {host=sessionHost;locations=locationData;saveStatus=saved;tutorialStatus=tutorial;rosterStatus=roster;guideNotice=guideFeedback;}

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
            if(state!=null && foodId==null)
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

        public void SetVietnamese(bool value){vi=value;Refresh();}
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
            if(!CanInteract)return null;
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
                SetMessage(en+bonus,vn+bonus);
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
                case "pet-cooldown":SetMessage("Pet cooldown: 30 seconds.","Vuốt ve: chờ đủ 30 giây.");break;
                case "feed-cooldown":SetMessage("Eating cooldown: 8 seconds.","Ăn: chờ đủ 8 giây.");break;
                case "food-too-fast":SetMessage("Please wait before dropping food.","Chờ trước khi thả thêm thức ăn.");break;
                default:SetMessage("Command rejected: "+code,"Lệnh bị từ chối: "+code);break;
            }
        }

        private void SetMessage(string en,string vn){messageEn=en;messageVi=vn;if(notice!=null)notice.text=vi?vn:en;if(guideNotice!=null)guideNotice.text=vi?vn:en;}
        private void Refresh()
        {
            if(notice!=null)notice.text=vi?messageVi:messageEn;
            if(guideNotice!=null)guideNotice.text=vi?messageVi:messageEn;
            if(saveStatus!=null && Session!=null)
            {
                saveStatus.text=Session.Status!=SessionStatus.Ready?(vi?"Phiên: ":"Session: ")+Session.Status:
                    !Session.Persistent?(vi?"Không có lưu bền":"Non-persistent storage"):
                    Session.SaveError?(vi?"Lỗi lưu":"Save failed"):
                    Session.LastSavedAtMs.HasValue?(vi?"Đã lưu ":"Saved ")+DateTimeOffset.FromUnixTimeMilliseconds(Session.LastSavedAtMs.Value).ToLocalTime().ToString("HH:mm:ss"):(vi?"Chờ lưu":"Not saved yet");
            }
            if(state==null)return;
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
