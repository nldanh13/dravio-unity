using System;
using System.Collections.Generic;
using System.Linq;
using Dravio.Character;
using Dravio.Character.Integration;
using Dravio.Domain;
using Dravio.Domain.Biology;
using Dravio.Domain.Behavior;
using BehaviorPhase = Dravio.Domain.Behavior.DayPhase;
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
    // U5.3 ground slice. Domain decisions are read-only; only validated food commands mutate state.
    public sealed class LivingSanctuaryPresentation : MonoBehaviour
    {
        [SerializeField] DravioSessionHost host;
        [SerializeField] SanctuaryLocationData locations;
        [SerializeField] DragonVisualManager manager;
        [SerializeField] DragonCarePreview legacy;
        [SerializeField] SanctuaryDomainPreview adapter;
        [SerializeField] GameObject fruit;
        [Tooltip("Experimental ADR 0203 B7/A4: assign food by preference instead of selected UID. Off until approved.")]
        [SerializeField] bool sharedFoodAllocation;
        [SerializeField] Text title,status,progress,notice,profile,travel;
        [SerializeField] Button feed,pet,sleep,inspect,resume,rosterInspect,nextDragon,observe,nextTravel,move,store;
        sealed class Resident
        {
            public string uid,habitat,species,variant;
            public DragonVisualStage stage;
            public GenotypeEntry[] genotype;
            public Vector3 position,target;
            public Quaternion rotation;
            public bool walking,canSleep,hasTarget,sleeping,usedPoint;
            public float rest;
            public int occupant;
            public BehaviorDecision decision;
            public DragonPersonalityStyle personality;
            public SanctuaryLivingData.InteractionPoint point;
            public DragonBehaviorIntent shownIntent;
            public string fallback;
            public long manualRestUntil;
        }
        readonly Dictionary<string,Resident> residents=new(StringComparer.Ordinal);
        readonly Dictionary<string,string> unavailable=new(StringComparer.Ordinal);
        readonly List<SanctuaryDragonFrame> frames=new();
        readonly List<RaycastResult> uiHits=new();
        readonly List<string> removals=new();
        SanctuaryDragonSync sync;
        EnvironmentInteractions interactions;
        BehaviorPhase phase=BehaviorPhase.Day;
        int nextOccupant=1;
        bool decisionsDirty=true;
        long decisionDeadline;
        int substitutions;
        string foodRequestedUid;
        bool feedPending;
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
            interactions=LivingEnvironment.Active!=null?LivingEnvironment.Active.interactions:null;
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
            foreach(var resident in residents.Values)ReleasePoint(resident);
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
            lastState=State;decisionsDirty=true;
            if(lastState==null){RefreshProfile();return;}
            // Remove stale placement first so moved/sold/away dragons cannot reserve a spawn.
            removals.Clear();
            foreach(var item in residents)
            {
                var d=lastState.FindDragon(item.Key);
                if(d==null || !Presentable(d) || d.Location.HabitatId!=item.Value.habitat)removals.Add(item.Key);
            }
            foreach(var id in removals)RemoveResident(id);
            unavailable.Clear();
            foreach(var d in lastState.Dragons.OrderBy(x=>x.Uid,StringComparer.Ordinal))
            {
                if(!Presentable(d))continue;
                if(!LivingResidentPolicy.CanWalk(d)){unavailable[d.Uid]="ground-only";RemoveResident(d.Uid);continue;}
                if(!SanctuaryVisualMapping.TryStage(Lifecycle.StageOf(d).ToString(),out var stage))continue;
                var entry=manager.catalog.Resolve(d.SpeciesId,stage,out _);
                if(entry==null || entry.prefab==null){unavailable[d.Uid]="missing-model";RemoveResident(d.Uid);continue;}
                if(!residents.TryGetValue(d.Uid,out var resident))
                {
                    var spawn=locations.SpawnPointsIn(d.Location.HabitatId).FirstOrDefault(p=>ClearPoint(p.Position,d.Location.HabitatId,d.Uid));
                    if(spawn==null){unavailable[d.Uid]="no-safe-spawn";continue;}
                    resident=new Resident{uid=d.Uid,habitat=d.Location.HabitatId,position=spawn.Position,rotation=spawn.Rotation,rest=1f,occupant=nextOccupant++};
                    residents.Add(d.Uid,resident);
                }
                resident.stage=stage;resident.species=d.SpeciesId;resident.variant=d.Mutation.ToWire();
                resident.personality=DragonPersonalityPresentation.StyleFromName(d.Personality.ToString());
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
            if(foodId!=null && !lastState.Food.Drops.Any(f=>f.Id==foodId))ClearFood();
            RefreshProfile();
        }

        long NowMs=>State?.Clock.LastProcessedAtMs??0L;
        BehaviorPhase CurrentPhase()
        {
            var living=LivingEnvironment.Active;
            if(living==null || living.timeOfDay==null || living.timeOfDay.data==null)return BehaviorPhase.Day;
            return DayCycle.PhaseOfMinute(Mathf.FloorToInt(living.Hour*60f));
        }
        void RemoveResident(string uid)
        {
            if(residents.TryGetValue(uid,out var r))ReleasePoint(r);
            residents.Remove(uid);
        }
        void ReleasePoint(Resident r)
        {
            if(r.point!=null && interactions!=null)interactions.Release(r.point.id,r.occupant);
            r.point=null;r.usedPoint=false;r.hasTarget=false;r.rest=0f;
        }
        void RefreshDecisions()
        {
            var current=CurrentPhase();long now=NowMs;
            if(!decisionsDirty && current==phase && now<decisionDeadline)return;
            phase=current;decisionsDirty=false;decisionDeadline=now+1000;
            var all=BehaviorQueries.All(State,now,phase);
            foreach(var d in all)
            {
                if(!residents.TryGetValue(d.Uid,out var r))continue;
                var old=r.decision;
                if(old==null || old.Behavior!=d.Behavior || old.StartedAtMs!=d.StartedAtMs ||
                    old.TargetFoodId!=d.TargetFoodId || old.Phase!=d.Phase)ReleasePoint(r);
                r.decision=d;
                if(d.UntilMs>now)decisionDeadline=Math.Min(decisionDeadline,d.UntilMs);
            }
            // Shared allocation is an experimental gameplay change, not silently enabled.
            // Default mode keeps the existing selected-UID food contract.
            if(foodId!=null && sharedFoodAllocation)
            {
                var assigned=all.FirstOrDefault(d=>d.Behavior==DragonBehavior.SeekFood && d.TargetFoodId==foodId);
                foodUid=assigned?.Uid;
            }
            RefreshProfile();
        }
        bool IsFoodTarget(Resident r)=>foodId!=null && r.uid==foodUid;
        bool TryReservePoint(Resident r,DragonBehavior behavior)
        {
            if(interactions==null || !r.walking)return false;
            SanctuaryLivingData.InteractionPoint best=null;float score=float.MaxValue;
            foreach(var p in interactions.All)
            {
                if(p==null || p.habitatId!=r.habitat || p.requiresSwim || p.requiresFly ||
                    (!string.IsNullOrEmpty(p.medium) && p.medium!="ground") ||
                    !AutonomousPresentationPolicy.PointMatches(behavior,p.kind) || !interactions.IsFree(p.id) ||
                    !ClearPoint(p.Position,r.habitat,r.uid) || !Segment(r.position,p.Position,r.habitat))continue;
                float distance=Vector3.Distance(r.position,p.Position)-
                    (p.Prefers((Dravio.EnvironmentArt.DayPhase)phase)?.5f:0f);
                if(distance<score){best=p;score=distance;}
            }
            if(best==null || !interactions.TryReserve(best.id,r.occupant))return false;
            r.point=best;r.target=best.Position;r.hasTarget=true;r.usedPoint=false;
            return true;
        }
        bool MoveToward(Resident r,Vector3 target,float dt,out float speed,out float turnRate)
        {
            speed=0f;turnRate=0f;
            Vector3 delta=target-r.position;delta.y=0;
            if(delta.sqrMagnitude<.01f)return true;
            if(!r.walking || !Segment(r.position,target,r.habitat))return false;
            var next=Vector3.MoveTowards(r.position,target,.55f*dt);
            if(!ClearPoint(next,r.habitat,r.uid) || !Segment(r.position,next,r.habitat))return false;
            var old=r.rotation;var heading=Quaternion.LookRotation(delta);
            r.rotation=Quaternion.RotateTowards(old,heading,180f*dt);
            if(dt>0f)turnRate=Quaternion.Angle(old,r.rotation)/dt;
            if(Quaternion.Angle(r.rotation,heading)<15f){r.position=next;speed=.55f;}
            return (r.position-target).sqrMagnitude<.01f;
        }
        DragonBehaviorIntent GroundIntent(Resident r,float dt,out float speed,out float turnRate,out bool look,out Vector3 lookTarget)
        {
            speed=0f;turnRate=0f;look=false;lookTarget=Vector3.zero;r.fallback=null;
            var behavior=r.decision?.Behavior??DragonBehavior.Idle;
            if(IsFoodTarget(r))
            {
                var d=State.FindDragon(r.uid);var drop=State.Food.Drops.FirstOrDefault(f=>f.Id==foodId);
                if(d!=null && drop!=null && BehaviorEngine.CanEat(d,drop,NowMs))
                {
                    if(r.point!=null)ReleasePoint(r);
                    look=true;lookTarget=foodPoint;
                    if((r.position-foodPoint).sqrMagnitude>.55f*.55f)
                    {
                        MoveToward(r,foodPoint,dt,out speed,out turnRate);
                        if(speed==0f)r.fallback="food-path";
                        return speed>0f?DragonBehaviorIntent.Walk:DragonBehaviorIntent.Idle;
                    }
                    return DragonBehaviorIntent.Idle;
                }
            }
            if(behavior==DragonBehavior.Eat)return DragonBehaviorIntent.Eat;
            // Manual doze is a short cosmetic request, never overrides a real gameplay rest/food decision.
            if(r.manualRestUntil>NowMs && r.decision?.Role==BehaviorRole.Presentation && r.canSleep)
            {if(r.point!=null)ReleasePoint(r);return DragonBehaviorIntent.Sleep;}
            if(behavior==DragonBehavior.SeekFood)
            {r.fallback="food-not-assigned-to-visible-marker";return DragonBehaviorIntent.Idle;}
            if(AutonomousPresentationPolicy.RequiresUnimplementedMedium(behavior))
            {r.fallback="medium-not-implemented";return DragonBehaviorIntent.Rest;}
            if(behavior==DragonBehavior.Sleep)return DragonBehaviorIntent.Sleep;
            if(behavior==DragonBehavior.Idle)return DragonBehaviorIntent.Idle;
            // Approach authored points only for compatible behaviors, using the existing reservations.
            bool pointBehavior=AutonomousPresentationPolicy.UsesPoint(behavior);
            if(pointBehavior && r.point==null)
            {
                r.rest-=dt;
                if(r.rest<=0f){if(!TryReservePoint(r,behavior))r.rest=2f;}
            }
            if(r.point!=null)
            {
                if(!Segment(r.position,r.point.Position,r.habitat) || !ClearPoint(r.point.Position,r.habitat,r.uid))
                {ReleasePoint(r);r.rest=2f;r.fallback="point-blocked";return DragonBehaviorIntent.Idle;}
                bool arrived=MoveToward(r,r.point.Position,dt,out speed,out turnRate);
                if(!arrived)return speed>0f?DragonBehaviorIntent.Walk:DragonBehaviorIntent.Idle;
                var old=r.rotation;
                r.rotation=Quaternion.RotateTowards(old,r.point.Rotation,180f*dt);
                if(dt>0f)turnRate=Quaternion.Angle(old,r.rotation)/dt;
                look=true;lookTarget=r.point.Target;
                if(Quaternion.Angle(r.rotation,r.point.Rotation)>15f)return DragonBehaviorIntent.Idle;
                if(!r.usedPoint){r.usedPoint=true;interactions.NotifyUsed(r.point.id,r.occupant);}
                return behavior==DragonBehavior.Bask?DragonBehaviorIntent.Bask:
                    behavior==DragonBehavior.Rest?DragonBehaviorIntent.Rest:DragonBehaviorIntent.Curious;
            }
            if(behavior==DragonBehavior.Bask){r.fallback="no-safe-bask-point";return DragonBehaviorIntent.Rest;}
            if(behavior==DragonBehavior.Rest)return DragonBehaviorIntent.Rest;
            if(behavior==DragonBehavior.Play)return DragonBehaviorIntent.Play;
            if(behavior==DragonBehavior.Explore && r.walking)
            {
                if(!r.hasTarget)
                {
                    if(r.rest>0f)return DragonBehaviorIntent.Idle;
                    var p=locations.SpawnPointsIn(r.habitat).FirstOrDefault(x=>
                        (x.Position-r.position).sqrMagnitude>.25f && ClearPoint(x.Position,r.habitat,r.uid) && Segment(r.position,x.Position,r.habitat));
                    if(p==null){r.rest=2f;return DragonBehaviorIntent.Idle;}
                    r.target=p.Position;r.hasTarget=true;
                }
                bool arrived=MoveToward(r,r.target,dt,out speed,out turnRate);
                if(arrived || !Segment(r.position,r.target,r.habitat) || !ClearPoint(r.target,r.habitat,r.uid))
                {r.hasTarget=false;r.rest=2f;}
                return speed>0f?DragonBehaviorIntent.Walk:DragonBehaviorIntent.Idle;
            }
            return DragonBehaviorIntent.Idle;
        }
        void Update()
        {
            if(!active)return;
            if(session==null && host.Session!=null){session=host.Session;session.DomainEvents+=OnEvents;}
            if(interactions==null && LivingEnvironment.Active!=null)interactions=LivingEnvironment.Active.interactions;
            if(!ReferenceEquals(lastState,State))RefreshRoster();
            bool ready=Ready;
            if(ready!=previousReady){previousReady=ready;RefreshButtons();decisionsDirty=true;}
            if(!ready)return;
            RefreshDecisions();
            float dt=Mathf.Min(Time.deltaTime,.1f);
            frames.Clear();
            foreach(var r in residents.Values)
            {
                var intent=GroundIntent(r,dt,out float speed,out float turnRate,out bool look,out Vector3 lookTarget);
                r.shownIntent=intent;
                frames.Add(new SanctuaryDragonFrame{uid=r.uid,speciesId=r.species,variantId=r.variant,stage=r.stage,
                    genotype=r.genotype,visible=true,position=r.position,rotation=r.rotation,intent=intent,
                    personality=r.personality,speed=speed,turnRateDegPerSec=turnRate,hasLookTarget=look,lookTarget=lookTarget});
            }
            var report=sync.Sync(frames,dt);substitutions=report.substituted;
            if(report.spawned>0 || report.despawned>0 || report.stageChanged>0)RefreshProfile();
            if(foodId!=null && foodUid!=null && Time.unscaledTime>=consumeAfter)Consume();
            ReadSelection();
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

        void Select(string id){if(selected!=id){selected=id;travelHabitat=null;StopFollow();if(manager.IsLive(id))sync.React(id,DragonReaction.Called);}RefreshProfile();}
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
            var personality=BehaviorQueries.Personality(State,d.Uid);
            if(personality==null)return;
            var diets=SpeciesCatalog.Get(d.SpeciesId).Biology.DietIds;
            if(diets.Count==0)return;
            // Preserve existing selected UID / first diet unless experimental allocation is enabled.
            var diet=sharedFoodAllocation?personality.FavoriteDiet:diets[0];
            r.manualRestUntil=0;ReleasePoint(r);
            var point=r.position+r.rotation*Vector3.forward*.4f;
            foodPoint=locations.IsWalkable(point,r.habitat)?point:r.position;
            foodRequestedUid=d.Uid;feedPending=true;
            try{Send(id=>new FeedCommand(id,diet,d.Location.ZoneId,d.Location.HabitatId));}
            finally{feedPending=false;foodRequestedUid=null;}
        }
        void Consume()
        {
            var drop=State?.Food.Drops.FirstOrDefault(f=>f.Id==foodId);
            var d=State?.FindDragon(foodUid);
            if(drop==null){ClearFood();return;}
            if(d==null || !residents.TryGetValue(foodUid,out var r) || !manager.IsLive(foodUid) ||
                (r.position-foodPoint).sqrMagnitude>.55f*.55f || !BehaviorEngine.CanEat(d,drop,NowMs))return;
            if(sharedFoodAllocation)
            {
                var decision=BehaviorQueries.Dragon(State,d.Uid,NowMs,phase);
                if(decision?.Behavior!=DragonBehavior.SeekFood || decision.TargetFoodId!=foodId)return;
            }
            var idToEat=foodId;var who=foodUid;
            if(Send(id=>new ConsumeFoodCommand(id,idToEat,who)))Message("Food consumed: "+who,"Rồng đã ăn: "+who);
            else consumeAfter=Time.unscaledTime+1f; // no retry every frame; Domain remains authoritative.
        }
        void ClearFood(){foodId=foodUid=null;if(fruit!=null)fruit.SetActive(false);decisionsDirty=true;RefreshButtons();}
        void OnEvents(IReadOnlyList<DomainEvent> events)
        {
            decisionsDirty=true;
            foreach(var e in events)
            {
                if(e is FoodSpawned f && feedPending && foodRequestedUid!=null && foodId==null)
                {
                    foodId=f.FoodId;foodUid=sharedFoodAllocation?null:foodRequestedUid;consumeAfter=Time.unscaledTime+.6f;
                    RefreshButtons();
                    if(fruit!=null){fruit.transform.position=foodPoint+Vector3.up*.12f;fruit.SetActive(true);}
                }
                else if(e is FoodConsumed consumed)
                {if(consumed.FoodId==foodId)ClearFood();} // Eat intent is derived from the accepted Domain event's timestamp.
                else if(e is DragonPetted p)sync.React(p.DragonUid,DragonReaction.Petted);
                else if(e is FoodExpired expired && expired.FoodId==foodId)ClearFood();
            }
        }
        void ToggleSleep()
        {
            if(selected!=null && residents.TryGetValue(selected,out var r) && r.canSleep &&
                r.decision?.Role==BehaviorRole.Presentation && !IsFoodTarget(r))
            {
                r.manualRestUntil=r.manualRestUntil>NowMs?0:NowMs+15000;
                ReleasePoint(r);
                Message(r.manualRestUntil>0?"Doze request (visual)":"Automatic behavior",r.manualRestUntil>0?"Nghỉ ngắn (hoạt ảnh)":"Tự sinh hoạt");
            }
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
        void Resume(){StopFollow();if(selected!=null && residents.TryGetValue(selected,out var r))r.manualRestUntil=0;}
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
            var traits=BehaviorQueries.Personality(State,d.Uid);
            var ecology=BehaviorQueries.Ecology(State,d.Uid);
            if(traits!=null)profile.text+="\n"+(vi?"Năng động / tò mò / ham chơi / dạn dĩ":"Energy / curiosity / play / boldness")+
                ": "+traits.Traits.Energy+"/"+traits.Traits.Curiosity+"/"+traits.Traits.Playfulness+"/"+traits.Traits.Boldness+
                "\n"+(vi?"Ưa thích: ":"Favorite: ")+traits.FavoriteDiet+" · "+traits.FavoriteHabitatId;
            if(ecology?.Habitat!=null)profile.text+="\n"+(vi?"Sinh thái: ":"Ecology: ")+ecology.Habitat.Score+"/100 · "+ecology.Habitat.PrimaryIssueKey+
                "\n"+string.Join(", ",ecology.Habitat.Factors.Where(f=>f.Relevant && !f.Ok).Select(f=>f.Key+": "+f.Actual+" / "+f.Required))+
                "\n"+(vi?"Comfort hiện tại: ":"Current comfort: ")+ecology.CurrentComfort+" · "+(vi?"tự đổi: tắt":"drift: off");
            if(r!=null && r.decision!=null)
            {
                status.text=BehaviorLabel(r.decision.Behavior)+" · "+phase;
                profile.text+="\n"+(vi?"Hành vi: ":"Behavior: ")+BehaviorLabel(r.decision.Behavior)+" · "+r.decision.Reason+
                    "\n"+(vi?"Trình diễn: ":"Presentation: ")+r.shownIntent+(r.fallback==null?"":" ("+r.fallback+")")+
                    "\n"+(vi?"Điểm tương tác: ":"Interaction point: ")+(r.point?.id??"—");
            }
            else status.text=vi?messageVi:messageEn;
            profile.text+="\n"+(vi?"Thay clip (khung trước): ":"Clip substitutions (last frame): ")+substitutions;
            if(notice!=null)notice.text=(vi?messageVi:messageEn)+(sharedFoodAllocation?(vi?" · Phân thức ăn thử nghiệm":" · Experimental food allocation"):"");
            RefreshButtons();
        }
        string BehaviorLabel(DragonBehavior? b)=>!vi?b?.ToString()??"—":b switch {
            DragonBehavior.Idle=>"Thư giãn",DragonBehavior.Rest=>"Nghỉ ngơi",DragonBehavior.Sleep=>"Ngủ",
            DragonBehavior.Explore=>"Khám phá",DragonBehavior.SeekFood=>"Tìm thức ăn",DragonBehavior.Eat=>"Ăn",
            DragonBehavior.Play=>"Chơi",DragonBehavior.Bask=>"Sưởi ấm",DragonBehavior.Swim=>"Bơi",
            DragonBehavior.Fly=>"Bay",DragonBehavior.Burrow=>"Đào hang",_=>"—"};
        void RefreshButtons()
        {
            if(!active)return;
            var d=Selected;
            bool visible=d!=null && residents.ContainsKey(d.Uid);
            bool ready=Ready;
            feed.interactable=ready && visible && foodId==null;
            pet.interactable=inspect.interactable=rosterInspect.interactable=observe.interactable=ready && visible;
            sleep.interactable=ready && visible && residents[selected].canSleep && residents[selected].decision?.Role==BehaviorRole.Presentation && !IsFoodTarget(residents[selected]);
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
