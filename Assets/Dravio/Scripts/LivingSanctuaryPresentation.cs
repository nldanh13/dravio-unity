using System;
using System.Collections.Generic;
using System.Linq;
using Dravio.Character;
using Dravio.Character.Integration;
using Dravio.Domain;
using Dravio.Domain.Biology;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Queries;
using Dravio.EnvironmentArt;
using Dravio.Platform;
using Dravio.Platform.Unity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Dravio
{
    // Presentation only. GameSession remains the sole writer of canonical state.
    // Origin ground slice: non-ground movement waits for explicit environment/behavior data.
    public sealed class LivingSanctuaryPresentation : MonoBehaviour
    {
        [SerializeField] DravioSessionHost host;
        [SerializeField] SanctuaryLocationData locations;
        [SerializeField] DragonVisualManager manager;
        [SerializeField] DragonCarePreview legacy;
        [SerializeField] SanctuaryDomainPreview adapter;
        [SerializeField] GameObject fruit;
        [SerializeField] Text title,status,progress,notice,profile,travel;
        [SerializeField] Button feed,pet,sleep,inspect,resume,rosterInspect,nextDragon,observe,nextTravel,move,store;
        sealed class Resident
        {
            public string uid,habitat,species,variant;
            public DragonVisualStage stage;
            public GenotypeEntry[] genotype;
            public Vector3 position,target;
            public Quaternion rotation;
            public bool walking,canSleep,hasTarget,sleeping;
            public float rest;
        }
        readonly Dictionary<string,Resident> residents=new(StringComparer.Ordinal);
        readonly Dictionary<string,string> unavailable=new(StringComparer.Ordinal);
        readonly List<SanctuaryDragonFrame> frames=new();
        readonly List<RaycastResult> uiHits=new();
        readonly List<string> removals=new();
        SanctuaryDragonSync sync;
        GameSession session;
        GameState lastState;
        string selected,travelHabitat,foodId,foodUid;
        Vector3 foodPoint;
        float consumeAfter;
        bool vi=true,active,following,focus=true,paused,previousReady;
        Camera cameraView;
        SanctuaryCameraFit cameraFit;
        bool restoreFit;
        Vector3 savedCamera;
        float savedSize;
        string messageEn="",messageVi="";
        bool Ready=>active && focus && !paused && session!=null && session.Status==SessionStatus.Ready && !session.Hidden && session.State!=null;
        GameState State=>session?.State;
        DragonState Selected=>State?.FindDragon(selected);

        public void Configure(DravioSessionHost h,SanctuaryLocationData data,DragonVisualManager visuals,
            DragonCarePreview old,SanctuaryDomainPreview domain,GameObject marker,
            Text heading,Text activity,Text xp,Text feedback,Text details,Text destination,
            Button f,Button p,Button sl,Button i,Button re,Button ri,Button next,Button ob,Button nt,Button mv,Button st)
        {
            host=h;locations=data;manager=visuals;legacy=old;adapter=domain;fruit=marker;
            title=heading;status=activity;progress=xp;notice=feedback;profile=details;travel=destination;
            feed=f;pet=p;sleep=sl;inspect=i;resume=re;rosterInspect=ri;nextDragon=next;observe=ob;nextTravel=nt;move=mv;store=st;
        }

        void Awake()
        {
            // Do not break the playable legacy preview if the local manifest has not been imported.
            active=host!=null && locations!=null && manager!=null && legacy!=null && adapter!=null && manager.catalog!=null &&
                manager.catalog.entries.Any(e=>e!=null && e.speciesId=="emberling" && e.prefab!=null);
            if(!active){FallbackText();return;}
            sync=new SanctuaryDragonSync(manager);
            focus=Application.isFocused;
            adapter.EnableLivingPresentation();
            legacy.enabled=false; // Awake precedes the legacy Start, so no duplicate button or movement handlers.
            foreach(var renderer in legacy.GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
            foreach(var collider in legacy.GetComponentsInChildren<Collider>(true))collider.enabled=false;
            foreach(var light in legacy.GetComponentsInChildren<Light>(true))light.enabled=false;
            foreach(var turn in legacy.GetComponentsInChildren<DragonTurntable>(true))turn.enabled=false;
            foreach(var particles in legacy.GetComponentsInChildren<ParticleSystem>(true))
                particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        void Start()
        {
            if(!active)return;
            session=host.Session;
            if(session!=null)session.DomainEvents+=OnEvents;
            cameraView=Camera.main;
            if(cameraView!=null)cameraFit=cameraView.GetComponent<SanctuaryCameraFit>();
            feed.onClick.AddListener(Feed);pet.onClick.AddListener(Pet);
            sleep.onClick.AddListener(ToggleSleep);inspect.onClick.AddListener(Follow);
            resume.onClick.AddListener(Resume);rosterInspect.onClick.AddListener(Follow);
            nextDragon.onClick.AddListener(NextDragon);observe.onClick.AddListener(Observe);
            nextTravel.onClick.AddListener(NextTravel);move.onClick.AddListener(Move);
            store.onClick.AddListener(Store);
            RefreshRoster();
        }

        void FallbackText()
        {
            if(profile!=null)profile.text=vi?"Chưa nhập DragonVisualCatalog có Emberling. Đang dùng bản thử một rồng.":"Import a DragonVisualCatalog with Emberling. Legacy single-dragon preview is active.";
        }
        public void SetVietnamese(bool value){vi=value;if(active)RefreshProfile();else FallbackText();}
        void OnApplicationFocus(bool value){focus=value;}
        void OnApplicationPause(bool value){paused=value;}
        void OnDestroy()
        {
            if(session!=null)session.DomainEvents-=OnEvents;
            StopFollow();
            if(sync!=null && manager!=null)sync.Clear();
            if(fruit!=null && active)fruit.SetActive(false);
        }

        bool Presentable(DragonState d)=>LivingResidentPolicy.InOriginView(State,d);
        bool ClearPoint(Vector3 point,string habitat,string except)
        {
            if(!locations.IsWalkable(point,habitat))return false;
            foreach(var other in residents.Values)
                if(other.uid!=except && (other.position-point).sqrMagnitude<.55f*.55f)return false;
            return true;
        }
        bool Segment(Vector3 from,Vector3 to,string habitat)
        {
            int n=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(from,to)/.15f));
            for(int i=0;i<=n;i++)if(!locations.IsWalkable(Vector3.Lerp(from,to,(float)i/n),habitat))return false;
            return true;
        }

        void RefreshRoster()
        {
            lastState=State;
            if(lastState==null){RefreshProfile();return;}
            // Remove stale placement first so moved/sold/away dragons cannot reserve a spawn.
            removals.Clear();
            foreach(var item in residents)
            {
                var d=lastState.FindDragon(item.Key);
                if(d==null || !Presentable(d) || d.Location.HabitatId!=item.Value.habitat)removals.Add(item.Key);
            }
            foreach(var id in removals)residents.Remove(id);
            unavailable.Clear();
            foreach(var d in lastState.Dragons.OrderBy(x=>x.Uid,StringComparer.Ordinal))
            {
                if(!Presentable(d))continue;
                if(!LivingResidentPolicy.CanWalk(d)){unavailable[d.Uid]="ground-only";residents.Remove(d.Uid);continue;}
                if(!SanctuaryVisualMapping.TryStage(Lifecycle.StageOf(d).ToString(),out var stage))continue;
                var entry=manager.catalog.Resolve(d.SpeciesId,stage,out _);
                if(entry==null || entry.prefab==null){unavailable[d.Uid]="missing-model";residents.Remove(d.Uid);continue;}
                if(!residents.TryGetValue(d.Uid,out var resident))
                {
                    var spawn=locations.SpawnPointsIn(d.Location.HabitatId).FirstOrDefault(p=>ClearPoint(p.Position,d.Location.HabitatId,d.Uid));
                    if(spawn==null){unavailable[d.Uid]="no-safe-spawn";continue;}
                    resident=new Resident{uid=d.Uid,habitat=d.Location.HabitatId,position=spawn.Position,rotation=spawn.Rotation,rest=1f};
                    residents.Add(d.Uid,resident);
                }
                resident.stage=stage;resident.species=d.SpeciesId;resident.variant=d.Mutation.ToWire();
                resident.walking=entry.rigged && entry.clips!=null && entry.clips.Contains("Walk");
                resident.canSleep=entry.rigged && entry.clips!=null && entry.clips.Contains("Sleep");
                if(!resident.canSleep)resident.sleeping=false;
                resident.genotype=d.Genotype?.Select(g=>new GenotypeEntry(g.Key,g.Value.ToString())).ToArray()??Array.Empty<GenotypeEntry>();
            }
            var current=lastState.FindDragon(selected);
            if(current==null || current.Location.Kind==LocationKind.Released)
            {
                selected=residents.Keys.FirstOrDefault()??lastState.Dragons.FirstOrDefault(d=>d.Location.Kind!=LocationKind.Released)?.Uid;
                travelHabitat=null;
            }
            if(following && (selected==null || !residents.ContainsKey(selected)))StopFollow();
            if(foodId!=null && (!lastState.Food.Drops.Any(f=>f.Id==foodId) || !residents.ContainsKey(foodUid)))ClearFood();
            RefreshProfile();
        }

        void Update()
        {
            if(!active)return;
            if(session==null && host.Session!=null){session=host.Session;session.DomainEvents+=OnEvents;}
            if(!ReferenceEquals(lastState,State))RefreshRoster();
            bool ready=Ready;
            if(ready!=previousReady){previousReady=ready;RefreshButtons();}
            if(!ready)return;
            float dt=Mathf.Min(Time.deltaTime,.1f);
            frames.Clear();
            foreach(var r in residents.Values)
            {
                var locomotion=DragonLocomotion.Idle;
                float speed=0f;
                if(r.sleeping)locomotion=DragonLocomotion.Sleep;
                else if(r.walking && r.uid!=foodUid)
                {
                    r.rest-=dt;
                    if(r.rest<=0f && !r.hasTarget)
                    {
                        var p=locations.SpawnPointsIn(r.habitat).FirstOrDefault(x=>
                            (x.Position-r.position).sqrMagnitude>.25f && ClearPoint(x.Position,r.habitat,r.uid) && Segment(r.position,x.Position,r.habitat));
                        if(p!=null){r.target=p.Position;r.hasTarget=true;}
                        else r.rest=2f;
                    }
                    if(r.hasTarget)
                    {
                        var delta=r.target-r.position;delta.y=0;
                        if(delta.sqrMagnitude<.01f){r.hasTarget=false;r.rest=2f;}
                        else
                        {
                            var target=Vector3.MoveTowards(r.position,r.target,.55f*dt);
                            if(ClearPoint(target,r.habitat,r.uid) && Segment(r.position,target,r.habitat))
                            {
                                r.rotation=Quaternion.RotateTowards(r.rotation,Quaternion.LookRotation(delta),180f*dt);
                                if(Quaternion.Angle(r.rotation,Quaternion.LookRotation(delta))<15f)
                                {r.position=target;locomotion=DragonLocomotion.Walk;speed=.55f;}
                            }
                            else{r.hasTarget=false;r.rest=2f;}
                        }
                    }
                }
                frames.Add(new SanctuaryDragonFrame{uid=r.uid,speciesId=r.species,variantId=r.variant,stage=r.stage,
                    genotype=r.genotype,visible=true,position=r.position,rotation=r.rotation,locomotion=locomotion,speed=speed});
            }
            var report=sync.Sync(frames);
            if(report.spawned>0 || report.despawned>0 || report.stageChanged>0)RefreshProfile();
            if(foodId!=null && Time.unscaledTime>=consumeAfter)Consume();
            ReadSelection();
            // Domain eligibility refreshed on state/selection changes, not every rendered frame.
        }

        void ReadSelection()
        {
            if(cameraView==null)cameraView=Camera.main;
            if(cameraView==null)return;
            Vector2 point;
            var touch=Touchscreen.current;
            var mouse=Mouse.current;
            if(touch!=null && touch.primaryTouch.press.wasPressedThisFrame)point=touch.primaryTouch.position.ReadValue();
            else if(mouse!=null && mouse.leftButton.wasPressedThisFrame)point=mouse.position.ReadValue();
            else return;
            if(EventSystem.current!=null)
            {
                uiHits.Clear();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},uiHits);
                if(uiHits.Any(h=>h.module is GraphicRaycaster))return;
            }
            var ray=cameraView.ScreenPointToRay(point);
            string hit=null;float nearest=float.PositiveInfinity;
            foreach(var r in residents.Values)
                if(manager.TryGet(r.uid,out var instance) && instance.WorldBounds().IntersectRay(ray,out float distance) && distance<nearest)
                {nearest=distance;hit=r.uid;}
            if(hit!=null && Physics.Raycast(ray,out var obstruction,nearest) && obstruction.collider!=null &&
                obstruction.collider.GetComponentInParent<DragonVisualInstance>()==null)return;
            if(hit!=null)Select(hit);
        }

        void Select(string id){if(selected!=id){selected=id;travelHabitat=null;StopFollow();}RefreshProfile();}
        void NextDragon()
        {
            if(State==null)return;
            var ids=State.Dragons.Where(d=>d.Location.Kind!=LocationKind.Released).Select(d=>d.Uid).ToArray();
            string next=LivingResidentPolicy.NextUid(ids,selected);
            if(next!=null)Select(next);
        }
        void Message(string en,string vn){messageEn=en;messageVi=vn;if(status!=null)status.text=vi?vn:en;}
        bool Send(Func<string,GameCommand> build)
        {
            if(!Ready)return false;
            var error=session.Send(build);
            if(error!=null){Message("Rejected: "+error.WireCode,"Lệnh bị từ chối: "+error.WireCode);return false;}
            return true;
        }
        void Pet()
        {
            var d=Selected;if(d==null || !residents.ContainsKey(d.Uid))return;
            if(Send(id=>new PetCommand(id,d.Uid)))Message("Petted.","Đã vuốt ve.");
        }
        void Feed()
        {
            var d=Selected;
            if(d==null || !residents.TryGetValue(d.Uid,out var r) || foodId!=null || !Ready)return;
            var diets=SpeciesCatalog.Get(d.SpeciesId).Biology.DietIds;
            if(diets.Count==0)return;
            // Choose a canonical diet. Position is only presentation; consume is a separate checked command.
            var diet=diets[0];r.sleeping=false;r.hasTarget=false;
            var point=r.position+r.rotation*Vector3.forward*.4f;
            foodPoint=locations.IsWalkable(point,r.habitat)?point:r.position;
            foodUid=d.Uid;
            if(!Send(id=>new FeedCommand(id,diet,d.Location.ZoneId,d.Location.HabitatId)))foodUid=null;
        }
        void Consume()
        {
            var drop=State?.Food.Drops.FirstOrDefault(f=>f.Id==foodId);
            var d=State?.FindDragon(foodUid);
            if(drop==null || d==null || !residents.TryGetValue(foodUid,out var r) || d.Location.HabitatId!=drop.HabitatId ||
                (r.position-foodPoint).sqrMagnitude>.55f*.55f){ClearFood();return;}
            var idToEat=foodId;var who=foodUid;
            if(Send(id=>new ConsumeFoodCommand(id,idToEat,who)))Message("Food consumed.","Rồng đã ăn.");
            ClearFood(); // A rejected drop is not removed from Domain; it expires by the canonical clock.
        }
        void ClearFood(){foodId=foodUid=null;if(fruit!=null)fruit.SetActive(false);RefreshButtons();}
        void OnEvents(IReadOnlyList<DomainEvent> events)
        {
            foreach(var e in events)
            {
                if(e is FoodSpawned f && foodUid!=null && foodId==null)
                {
                    foodId=f.FoodId;consumeAfter=Time.unscaledTime+.6f;RefreshButtons();
                    if(fruit!=null){fruit.transform.position=foodPoint+Vector3.up*.12f;fruit.SetActive(true);}
                }
                else if(e is FoodConsumed consumed)
                {sync.Trigger(consumed.DragonUid,DragonInteraction.Eat);if(consumed.FoodId==foodId)ClearFood();}
                else if(e is DragonPetted p)sync.Trigger(p.DragonUid,DragonInteraction.Happy);
                else if(e is FoodExpired expired && expired.FoodId==foodId)ClearFood();
            }
        }
        void ToggleSleep()
        {
            if(selected!=null && residents.TryGetValue(selected,out var r) && r.canSleep)
            {r.sleeping=!r.sleeping;r.hasTarget=false;Message(r.sleeping?"Resting (visual)":"Awake",r.sleeping?"Đang nghỉ (hoạt ảnh)":"Đã thức");}
        }
        void Observe()
        {
            var d=Selected;if(d==null || !residents.ContainsKey(d.Uid))return;
            if(Send(id=>new ViewBiologyProfileCommand(id,d.SpeciesId)))Message("Profile recorded.","Đã ghi nhận hồ sơ loài.");
        }
        List<TravelOption> TravelOptions()=>State==null || Selected==null?new List<TravelOption>():
            Ecosystem.TravelOptions(State,selected).Where(o=>o.ZoneId=="origin").ToList();
        void NextTravel()
        {
            var options=TravelOptions();if(options.Count==0)return;
            travelHabitat=options[(options.FindIndex(o=>o.HabitatId==travelHabitat)+1)%options.Count].HabitatId;RefreshProfile();
        }
        void Move()
        {
            if(Selected==null || travelHabitat==null)return;
            string who=selected,habitat=travelHabitat;
            if(Send(id=>new MoveDragonCommand(id,who,MoveTarget.Zone("origin",habitat))))Message("Moved to habitat.","Đã chuyển sinh cảnh.");
        }
        void Store()
        {
            if(Selected==null)return;string who=selected;
            if(Send(id=>new MoveDragonCommand(id,who,MoveTarget.Storage)))Message("Moved to storage.","Đã chuyển vào kho.");
        }
        void Follow()
        {
            if(!Ready || selected==null || !residents.ContainsKey(selected) || cameraView==null || !cameraView.orthographic)return;
            if(following){StopFollow();return;}
            savedCamera=cameraView.transform.position;savedSize=cameraView.orthographicSize;
            restoreFit=cameraFit!=null && cameraFit.enabled;
            if(restoreFit)cameraFit.enabled=false;
            following=true;
        }
        void Resume(){StopFollow();if(selected!=null && residents.TryGetValue(selected,out var r))r.sleeping=false;}
        void LateUpdate()
        {
            if(!following || cameraView==null)return;
            if(selected==null || !residents.TryGetValue(selected,out var r)){StopFollow();return;}
            var t=cameraView.transform;float size=Mathf.Min(savedSize,1.8f);
            var subject=r.position+Vector3.up*.45f;
            var free=cameraFit!=null?cameraFit.FreeViewport:new Rect(0,0,1,1);
            float depth=Vector3.Dot(subject-savedCamera,t.forward);
            var target=subject-t.forward*depth-t.right*((free.center.x-.5f)*2f*size*cameraView.aspect)
                -t.up*((free.center.y-.5f)*2f*size);
            float blend=1f-Mathf.Exp(-8f*Time.unscaledDeltaTime);
            t.position=Vector3.Lerp(t.position,target,blend);
            cameraView.orthographicSize=Mathf.Lerp(cameraView.orthographicSize,size,blend);
        }
        void StopFollow()
        {
            if(!following)return;
            following=false;
            if(cameraView!=null){cameraView.transform.position=savedCamera;cameraView.orthographicSize=savedSize;}
            if(restoreFit && cameraFit!=null)cameraFit.enabled=true;
            restoreFit=false;
        }

        void RefreshProfile()
        {
            if(!active)return;
            var d=Selected;
            if(d==null)
            {
                title.text=vi?"Chưa chọn rồng":"No dragon selected";progress.text="—";profile.text=title.text;travel.text="—";
                RefreshButtons();return;
            }
            var snapshot=Ecosystem.Snapshot(State,d);
            title.text=(string.IsNullOrEmpty(d.Nickname)?d.SpeciesId:d.Nickname)+" · "+snapshot.Stage;
            progress.text="XP "+d.XpSec.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+"/"+SpeciesCatalog.Get(d.SpeciesId).GrowthTimeSec+
                " · "+(vi?"Gắn kết ":"Bond ")+d.Bond+"/100";
            string visual=vi?"Không ở sinh cảnh hiện tại":"Not in current habitat view";
            if(residents.TryGetValue(d.Uid,out var r))
            {
                var entry=manager.catalog.Resolve(r.species,r.stage,out var match);
                visual=(manager.IsLive(d.Uid)?(vi?"Model: ":"Model: "):(vi?"Chưa spawn: ":"Not spawned: "))+match+
                    (entry==null?"":" · "+entry.stage+" · "+(entry.approved?"approved":"unapproved"));
            }
            else if(unavailable.TryGetValue(d.Uid,out string why))visual=why switch{
                "missing-model"=>vi?"Thiếu model đúng loài":"Missing species model",
                "ground-only"=>vi?"Chưa hỗ trợ môi trường di chuyển của loài":"Locomotion medium not implemented",
                _=>vi?"Không có điểm đặt an toàn":"No safe spawn"};
            profile.text=title.text+"\nUID: "+d.Uid+"\n"+snapshot.Mutation+" · "+d.Personality+"\n"+snapshot.Status+
                " · "+(snapshot.HabitatId??snapshot.ZoneId??"—")+"\n"+(vi?"Phù hợp: ":"Suitability: ")+(snapshot.Suitability?.ToString()??"—")+
                "\n"+visual+"\n"+(vi?"Hiển thị: ":"Visual residents: ")+residents.Count;
            var options=TravelOptions();
            if(!options.Any(o=>o.HabitatId==travelHabitat))travelHabitat=options.FirstOrDefault()?.HabitatId;
            var option=options.FirstOrDefault(o=>o.HabitatId==travelHabitat);
            travel.text=option==null?"—":option.HabitatId+" · "+option.Score+"/100\n"+
                (option.Blocked.HasValue?option.Blocked.Value.ToWire():(vi?"Có thể chuyển":"Available"));
            status.text=vi?messageVi:messageEn;
            RefreshButtons();
        }
        void RefreshButtons()
        {
            if(!active)return;
            var d=Selected;
            bool visible=d!=null && residents.ContainsKey(d.Uid);
            bool ready=Ready;
            feed.interactable=ready && visible && foodId==null;
            pet.interactable=inspect.interactable=rosterInspect.interactable=observe.interactable=ready && visible;
            sleep.interactable=ready && visible && residents[selected].canSleep;
            resume.interactable=ready && (visible || following);
            nextDragon.interactable=ready && State.Dragons.Count(x=>x.Location.Kind!=LocationKind.Released)>1;
            // Eligibility can change on each Domain revision; never calculate gameplay rules here.
            if(!ready || d==null){nextTravel.interactable=move.interactable=store.interactable=false;return;}
            var options=TravelOptions();
            nextTravel.interactable=options.Count>1;
            move.interactable=options.Any(o=>o.HabitatId==travelHabitat && o.Blocked==null);
            store.interactable=Ecosystem.Actions(State,d.Uid,State.Clock.LastProcessedAtMs).MoveToStorage==null;
        }
    }
}
