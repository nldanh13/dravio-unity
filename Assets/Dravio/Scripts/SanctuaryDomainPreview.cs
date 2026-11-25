using System;
using System.Globalization;
using System.Linq;
using Dravio.Domain;
using Dravio.Domain.Biology;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
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
        public bool CanInteract => state!=null && focused && !paused;

        public void Configure(Text goldLabel,Text progressLabel,Text noticeLabel,GameObject fruitVisual)
        {gold=goldLabel;progress=progressLabel;notice=noticeLabel;fruit=fruitVisual;}

        private void Awake()
        {
            spawn=transform.position;
            focused=Application.isFocused;
            if(fruit!=null)fruit.SetActive(false);
            var now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            clock=new FixedClock(now);
            var id=Guid.NewGuid().ToString("N");
            state=InitialStateFactory.Create(id,now,new Mulberry32(Rng.HashSeed(id)));
            uid=state.Dragons[0].Uid;
            nextTick=now+1000;
            SetMessage("Session only — no save or offline rewards.","Phiên thử — chưa lưu hoặc thưởng offline.");
            Refresh();
        }

        private void OnApplicationFocus(bool value){focused=value;skipFrame=true;}
        private void OnApplicationPause(bool value){paused=value;skipFrame=true;}
        private void Update()
        {
            if(!CanInteract)return;
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
                foodId=null;
                if(fruit!=null)fruit.SetActive(false);
                if(result.Events.Any(e=>e is FoodExpired))
                    SetMessage("Food expired.","Thức ăn đã hết hạn.");
            }
            Refresh();
            return result;
        }

        public bool RequestFood()
        {
            if(!CanInteract)return false;
            if(HasFood){SetMessage("One fruit is already available in this preview.","Đã có một quả trong bản thử.");return false;}
            var r=Dispatch(new FeedCommand(Id(),DietId.Fruit,"origin","origin_meadow"));
            if(!r.Ok)return false;
            var drop=r.Events.OfType<FoodSpawned>().FirstOrDefault();
            if(drop==null)return false;
            foodId=drop.FoodId;
            nextConsumeAttempt=0;
            if(fruit!=null)
            {
                fruit.transform.position=spawn+new Vector3(.25f,.12f,.55f);
                fruit.SetActive(true);
            }
            SetMessage("Fruit placed — Emberling will approach it.","Đã thả trái cây — Emberling sẽ đến gần.");
            return true;
        }

        public bool TryConsume()
        {
            if(!CanInteract || !HasFood || clock.NowMs()<nextConsumeAttempt)return false;
            Vector3 distance=FoodPosition-transform.position;distance.y=0;
            if(distance.sqrMagnitude>.55f*.55f)return false;
            nextConsumeAttempt=clock.NowMs()+500;
            var r=Dispatch(new ConsumeFoodCommand(Id(),foodId,uid));
            if(!r.Ok)return false;
            var consumed=r.Events.OfType<FoodConsumed>().FirstOrDefault();
            if(consumed==null)return false;
            SetMessage("Consumed fruit: +"+consumed.XpGainedSec+" XP","Đã ăn trái cây: +"+consumed.XpGainedSec+" XP");
            return true;
        }

        public bool TryPet()
        {
            if(!CanInteract)return false;
            var r=Dispatch(new PetCommand(Id(),uid));
            if(!r.Ok)return false;
            bool gained=r.Events.Any(e=>e is BondChanged);
            SetMessage(gained?"Bond increased.":"Petted — bond cap reached.",gained?"Gắn kết tăng.":"Đã vuốt ve — đạt giới hạn gắn kết.");
            return r.Events.Any(e=>e is DragonPetted);
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

        private void SetMessage(string en,string vn){messageEn=en;messageVi=vn;if(notice!=null)notice.text=vi?vn:en;}
        private void Refresh()
        {
            if(notice!=null)notice.text=vi?messageVi:messageEn;
            if(state==null)return;
            var dragon=state.FindDragon(uid);
            var species=SpeciesCatalog.Get(dragon.SpeciesId);
            var stage=Lifecycle.StageOf(dragon);
            string stageText=vi?(stage==GrowthStage.Baby?"Rồng non":stage==GrowthStage.Juvenile?"Rồng trẻ":"Trưởng thành"):stage.ToString();
            if(gold!=null)gold.text=(state.Player.Gold.Micro/(decimal)MicroGold.MicroPerGold).ToString("0.00",CultureInfo.InvariantCulture)+" G";
            if(progress!=null)progress.text=stageText+"  |  XP "+dragon.XpSec.ToString("0.0",CultureInfo.InvariantCulture)+
                "/"+species.GrowthTimeSec+"  |  "+(vi?"Gắn kết ":"Bond ")+dragon.Bond+"/100";
        }
    }
}
