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
using Dravio.Domain.World;
using Dravio.EnvironmentArt;
using Dravio.Navigation;
using Dravio.Platform;
using Dravio.Platform.Unity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Dravio
{
    // Presentation only. GameSession remains the sole writer of canonical state.
    // U5.3.6: cached bounded ground routes; no cross-habitat or unapproved medium transitions.
    public sealed class LivingSanctuaryPresentation : MonoBehaviour
    {
        [SerializeField] DravioSessionHost host;
        [SerializeField] SanctuaryLocationData locations;
        [SerializeField] DragonVisualManager manager;
        [SerializeField] DragonCarePreview legacy;
        [SerializeField] SanctuaryDomainPreview adapter;
        [SerializeField] GameObject fruit;
        [Tooltip("Experimental ADR 0203 B7/A4: use the selected dragon's favorite diet without prioritizing that dragon.")]
        [SerializeField] bool sharedFoodAllocation;
#if UNITY_EDITOR
        [Tooltip("TL.11 experimental selected-UID food perception/action sequence. Off by default; ignored with shared allocation.")]
        [SerializeField] bool experimentalFoodPerception;
        readonly FoodActionPresentationGate foodGate=new FoodActionPresentationGate();
        bool foodGateForMarker;
        bool UseFoodGate=>foodGateForMarker;
        string lastFoodGateOutcome="—";
#endif
        // Diagnostics remain available to developers in the Editor, never enabled in a player build.
#if UNITY_EDITOR
        [SerializeField] bool showProfileDiagnostics;
#endif
        bool ProfileDiagnostics
        {
            get
            {
#if UNITY_EDITOR
                return showProfileDiagnostics || experimentalFoodPerception || UseFoodGate || SocialEnabled || spatialForView;
#else
                return false;
#endif
            }
        }
        [SerializeField] Text title,status,progress,notice,profile,travel;
        [SerializeField] Button feed,pet,sleep,inspect,resume,rosterInspect,nextDragon,observe,nextTravel,move,store;
        [SerializeField] Button researchObserve;
        public void ConfigureResearch(Button button)=>researchObserve=button;
        sealed class Resident
        {
            public string uid,habitat,species,variant;
            public NavigationMedium medium;
            public MediumReviewEnvelope envelope;
            public DragonVisualCatalog.Entry spatialEntry;
            public float spatialSpeed,spatialTurn;
            public int spatialDestination;
            public bool Spatial=>medium==NavigationMedium.Swim||medium==NavigationMedium.Air;
            public DragonVisualStage stage;
            public GenotypeEntry[] genotype;
            public Vector3 position,target;
            public Quaternion rotation;
            public bool walking,canSleep,hasTarget,usedPoint;
            public float rest;
            public int occupant;
            public BehaviorDecision decision;
            public DragonPersonalityStyle personality;
            public SanctuaryLivingData.InteractionPoint point;
            public DragonBehaviorIntent shownIntent;
            public string fallback;
            public long manualRestUntil;
            public RouteResult route;
            public Vector3 routeGoal;
            public int routeIndex;
            public float retryAt,blockedUntil;
            public string blockedPoint;
            public bool routeFailed;
            public string routeState="—";
        }
        readonly Dictionary<string,Resident> residents=new(StringComparer.Ordinal);
        readonly Dictionary<string,string> unavailable=new(StringComparer.Ordinal);
        readonly List<SanctuaryDragonFrame> frames=new();
        readonly List<RaycastResult> uiHits=new();
        readonly List<string> removals=new();
        SanctuaryDragonSync sync;
        SanctuaryNavigationSurface navigation;
        int routeBudget;
        int plannedRoutes,failedRoutes;
#if UNITY_EDITOR
        ControllerFrameTrace controllerTrace;
        [Tooltip("TL.13 editor-only fallback after the original ground A* fails. Off by default.")]
        [SerializeField] bool experimentalAdaptiveGroundRouting;
        int traceAttempts,traceArrivals,traceBlocked,adaptiveAttempts,adaptiveRescues;
        bool traceAdaptiveMode;
#endif
        [SerializeField] bool drawSelectedRoute=true;
        EnvironmentInteractions interactions;
        BehaviorPhase phase=BehaviorPhase.Day;
        int nextOccupant=1;
        bool decisionsDirty=true;
        long decisionDeadline;
        int substitutions;
        GameState progressionState;
        ProgressionOverviewDto progression;
        string relationUid,relationEn,relationVi;
        readonly Dictionary<string,ResearchProfileDto> researchProfiles=new(StringComparer.Ordinal);
        List<SocialInteractionCandidate> socialCandidates=new();
#if UNITY_EDITOR
        [Tooltip("U5.4.4 read-only ground social presentation trial. Off by default.")]
        [SerializeField] bool experimentalSocialPresentation;
#endif
        bool SocialEnabled
        {
            get
            {
#if UNITY_EDITOR
                return experimentalSocialPresentation;
#else
                return false;
#endif
            }
        }
        readonly SocialPresentationScheduler socialScheduler=new();
        Vector3 socialGoal;
        Resident socialA,socialB;
        DragonVisualStage socialStageA,socialStageB;
        string socialVariantA,socialVariantB;
        float socialRetryAt;
        string socialLast="—";
        SocialPresentationLease Social=>socialScheduler.Active;
        bool feedPending;
        GameSession session;
        GameState lastState;
        string selected,travelHabitat,travelZone,foodUid;
        readonly List<string> portions=new List<string>();
        readonly Dictionary<string,Vector3> mealSpots=new Dictionary<string,Vector3>();
        string foodId=>portions.Count==0?null:portions[0];
        bool placingMeal, tapPending, touchTap;
        Vector2 tapStart;
        FantasyPreviewUI worldHud;
        float feedbackUntil;
        public bool WorldInteractionAvailable=>active;
        public string WorldFeedback(bool vietnamese)=>Time.unscaledTime<feedbackUntil?(vietnamese?messageVi:messageEn):null;
        [SerializeField] bool connectedWorlds;
        bool viewSuspended;
#if UNITY_EDITOR
        [Tooltip("U5.4.3b: same-medium resident review, requires actual QA catalog. No live transition or player-build activation.")]
        [SerializeField] bool experimentalSpatialResidents;
#endif
        bool spatialForView;
        readonly HashSet<string> spatialFaults=new(StringComparer.Ordinal);
        public bool WorldViewAvailable=>active;
        string ViewZone=>locations?.zoneId??ConnectedWorldPolicy.Origin;
        public void EnableConnectedWorlds()=>connectedWorlds=true;
        Vector3 foodPoint;
        float consumeAfter;
        bool vi=true,active,following,focus=true,paused,previousReady;
        Camera cameraView;
        SanctuaryCameraFit cameraFit;
        bool restoreFit;
        Vector3 savedCamera;
        float savedSize;
        float followZoom=1f;
        float pinchDistance;
        int pinchA=-1,pinchB=-1;
        RectTransform focusPanel;
        string messageEn="",messageVi="";
        bool Ready=>active && !viewSuspended && focus && !paused && session!=null && session.Status==SessionStatus.Ready && !session.Hidden && session.State!=null;
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
            navigation=new SanctuaryNavigationSurface(locations);
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
            worldHud=FindAnyObjectByType<FantasyPreviewUI>();
            session=host.Session;
            if(session!=null)session.DomainEvents+=OnEvents;
            interactions=LivingEnvironment.Active!=null?LivingEnvironment.Active.interactions:null;
            cameraView=Camera.main;
            if(cameraView!=null)cameraFit=cameraView.GetComponent<SanctuaryCameraFit>();
            feed.onClick.AddListener(Feed);pet.onClick.AddListener(Pet);
            sleep.onClick.AddListener(ToggleSleep);inspect.onClick.AddListener(Follow);
            resume.onClick.AddListener(Resume);rosterInspect.onClick.AddListener(Follow);
            nextDragon.onClick.AddListener(NextDragon);observe.onClick.AddListener(Observe);
            if(researchObserve!=null)researchObserve.onClick.AddListener(ResearchObserve);
            nextTravel.onClick.AddListener(NextTravel);move.onClick.AddListener(Move);
            store.onClick.AddListener(Store);
            RefreshRoster();
        }

        void FallbackText()
        {
            if(profile!=null)profile.text=vi?"Chưa nhập DragonVisualCatalog có Emberling. Đang dùng bản thử một rồng.":"Import a DragonVisualCatalog with Emberling. Legacy single-dragon preview is active.";
        }
        public void SetVietnamese(bool value){vi=value;if(active)RefreshProfile();else FallbackText();}
        void OnApplicationFocus(bool value){focus=value;if(!value)tapPending=false;}
        void OnApplicationPause(bool value){paused=value;if(value)tapPending=false;}
        void OnDestroy()
        {
            StopSocial("destroy");
            if(session!=null)session.DomainEvents-=OnEvents;
            StopFollow();
            foreach(var resident in residents.Values)ReleasePoint(resident);
            if(sync!=null && manager!=null)sync.Clear();
            if(fruit!=null && active)fruit.SetActive(false);
        }

        public void SuspendWorldView()
        {
            if(!active)return;
            tapPending=false;viewSuspended=true;StopSocial("world-switch");StopFollow();
            foreach(var r in residents.Values)ReleasePoint(r);
            residents.Clear();unavailable.Clear();spatialFaults.Clear();sync.Clear();frames.Clear();
            ClearFood();interactions=null;cameraView=null;cameraFit=null;manager.viewCamera=null;
            lastState=null;
#if UNITY_EDITOR
            controllerTrace?.Stop();
#endif
            RefreshButtons();
        }
        public bool BindWorldView(SanctuaryLocationData data,EnvironmentInteractions points,Camera camera)
        {
            if(!active || data==null || camera==null || !ConnectedWorldPolicy.CanVisit(State,data.zoneId))return false;
            locations=data;navigation=new SanctuaryNavigationSurface(data);interactions=points;
#if UNITY_EDITOR
            spatialForView=connectedWorlds && experimentalSpatialResidents;
#else
            spatialForView=false;
#endif
            cameraView=camera;cameraFit=camera.GetComponent<SanctuaryCameraFit>();manager.viewCamera=camera;
            viewSuspended=false;lastState=null;decisionsDirty=true;travelZone=null;travelHabitat=null;RefreshRoster();return true;
        }
        bool Presentable(DragonState d)=>ConnectedWorldPolicy.InView(State,d,ViewZone);
        bool ClearPoint(Vector3 point,string habitat,string except)
        {
            if(navigation==null || !navigation.Clear(point,habitat,NavigationMedium.Ground))return false;
            return Separated(point,except);
        }
        bool Separated(Vector3 point,string except)
        {
            foreach(var other in residents.Values)
            {
                if(Social!=null && Social.Partner(except)==other.uid && !SocialPresentationScheduler.Spacing(
                    SanctuaryNavigationSurface.Point(point),SanctuaryNavigationSurface.Point(other.position),Social.MinDistance))return false;
                if(other.uid!=except && (other.Spatial?!SpatialResidentPolicy.Separated(SanctuaryNavigationSurface.Point(point),.275f,
                    SanctuaryNavigationSurface.Point(other.position),other.envelope.Horizontal):(other.position-point).sqrMagnitude<.55f*.55f))return false;
            }
            return true;
        }
        bool Segment(Vector3 from,Vector3 to,string habitat)
            =>navigation!=null && navigation.Segment(from,to,habitat,NavigationMedium.Ground);

        void ResetRoute(Resident r)
        {
            r.route=null;r.routeIndex=0;r.routeFailed=false;r.retryAt=0f;r.routeState="—";
        }
        bool EnsureRoute(Resident r,Vector3 goal)
        {
            if((goal-r.routeGoal).sqrMagnitude>.0004f)
            {ResetRoute(r);r.routeGoal=goal;}
            if(r.route!=null && r.route.Success)return true;
            if(Time.unscaledTime<r.retryAt || routeBudget<=0)return false;
            routeBudget--;plannedRoutes++;
            // Dynamic residents are obstacles at plan time; final step rechecks current positions.
            r.route=navigation.Plan(r.position,goal,r.habitat,NavigationMedium.Ground,
                point=>Separated(point,r.uid));
#if UNITY_EDITOR
            if(experimentalAdaptiveGroundRouting && !r.route.Success)
            {
                var fallback=ExperimentalGroundRouteFallback.Try(SanctuaryNavigationSurface.Point(r.position),
                    SanctuaryNavigationSurface.Point(goal),r.route,
                    point=>ClearPoint(SanctuaryNavigationSurface.Vector(point),r.habitat,r.uid));
                adaptiveAttempts+=fallback.Attempts;
                if(fallback.Rescued){adaptiveRescues++;r.route=fallback.Path;}
            }
#endif
            r.routeIndex=0;r.routeState=r.route.Status.ToString();
            r.routeFailed=!r.route.Success;
            if(r.routeFailed){failedRoutes++;r.retryAt=Time.unscaledTime+1f;return false;}
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
                var biology=SpeciesCatalog.Get(d.SpeciesId).Biology;
                var medium=SpatialResidentPolicy.RequestedMedium(ViewZone,biology.Locomotion.Contains(Locomotion.Swim),
                    biology.Locomotion.Contains(Locomotion.Fly),spatialForView);
                if(medium==NavigationMedium.Ground && !LivingResidentPolicy.CanWalk(d)){unavailable[d.Uid]="ground-only";RemoveResident(d.Uid);continue;}
                if(!SanctuaryVisualMapping.TryStage(Lifecycle.StageOf(d).ToString(),out var stage))continue;
                var entry=manager.catalog.Resolve(d.SpeciesId,stage,out var match);
                if(medium!=NavigationMedium.Ground)
                {
                    ProjectSpatial(d,stage,entry,match,medium);
                    continue;
                }
                if(entry==null || entry.prefab==null){unavailable[d.Uid]="missing-model";RemoveResident(d.Uid);continue;}
                if(!residents.TryGetValue(d.Uid,out var resident))
                {
                    var spawn=locations.SpawnPointsIn(d.Location.HabitatId).FirstOrDefault(p=>(string.IsNullOrEmpty(p.medium)||p.medium=="ground") && ClearPoint(p.Position,d.Location.HabitatId,d.Uid));
                    if(spawn==null){unavailable[d.Uid]="no-safe-spawn";continue;}
                    resident=new Resident{uid=d.Uid,habitat=d.Location.HabitatId,position=spawn.Position,rotation=spawn.Rotation,rest=1f,occupant=nextOccupant++};
                    residents.Add(d.Uid,resident);
                }
                if(resident.stage!=stage)ReleasePoint(resident);
                resident.stage=stage;resident.species=d.SpeciesId;resident.variant=d.Mutation.ToWire();
                resident.personality=DragonPersonalityPresentation.StyleFromName(d.Personality.ToString());
                resident.walking=entry.rigged && entry.clips!=null && entry.clips.Contains("Walk");
                resident.canSleep=entry.rigged && entry.clips!=null && entry.clips.Contains("Sleep");
                if(!resident.canSleep)resident.manualRestUntil=0;
                resident.genotype=d.Genotype?.Select(g=>new GenotypeEntry(g.Key,g.Value.ToString())).ToArray()??Array.Empty<GenotypeEntry>();
            }
            var current=lastState.FindDragon(selected);
            if(current==null || current.Location.Kind==LocationKind.Released)
            {
                selected=residents.Keys.FirstOrDefault()??lastState.Dragons.FirstOrDefault(d=>d.Location.Kind!=LocationKind.Released)?.Uid;
                travelHabitat=null;
            }
            if(following && (selected==null || !residents.ContainsKey(selected)))StopFollow();
            for(int i=portions.Count-1;i>=0;i--)
                if(!lastState.Food.Drops.Any(f=>f.Id==portions[i]))RemovePortion(portions[i]);
            RefreshProfile();
        }

        static string MediumClip(NavigationMedium m)=>m==NavigationMedium.Swim?"Swim":"Fly";
        static DragonLocomotion SpatialClip(Resident r)=>r.medium==NavigationMedium.Swim?DragonLocomotion.Swim:DragonLocomotion.Fly;
        static DragonMotionMode SpatialMode(Resident r)=>r.medium==NavigationMedium.Swim?DragonMotionMode.Water:DragonMotionMode.Air;
        static bool SpatialMetadata(DragonVisualCatalog.Entry e,DragonVisualCatalog.Match match,NavigationMedium medium,
            out MediumReviewEnvelope bounds,out float speed,out float turn)
        {
            bounds=default;speed=turn=0;
            if(e==null||e.prefab==null||!e.rigged||e.animQaStatus!="PASS"||
                (match!=DragonVisualCatalog.Match.Exact && match!=DragonVisualCatalog.Match.Unstaged)||
                (e.prefab.transform.localScale-Vector3.one).sqrMagnitude>.000001f||e.motion==null||
                e.clips==null||!e.clips.Contains(MediumClip(medium)))return false;
            var clip=e.clipSpeeds?.FirstOrDefault(x=>x!=null&&x.clip==MediumClip(medium)&&SpatialResidentPolicy.Positive(x.groundSpeed));
            if(clip==null)return false;
            speed=clip.groundSpeed;var m=e.motion;
            turn=medium==NavigationMedium.Swim?m.maxTurnWater:m.maxTurnAir;
            return SpatialResidentPolicy.Positive(turn)&&MediumReviewEnvelope.TryFromDimensions(m.bodyLength,m.bodyWidth,m.bodyHeight,
                medium==NavigationMedium.Swim?m.swimWidth:m.flightSpan,
                medium==NavigationMedium.Swim?m.swimVerticalExtent:m.flightVerticalExtent,out bounds);
        }
        bool SpatialClear(Vector3 point,string habitat,string uid,NavigationMedium medium,MediumReviewEnvelope bounds)
        {
            if(!navigation.ClearReviewVolume(point,habitat,medium,bounds))return false;
            foreach(var other in residents.Values)
                if(other.uid!=uid&&!SpatialResidentPolicy.Separated(SanctuaryNavigationSurface.Point(point),bounds.Horizontal,
                    SanctuaryNavigationSurface.Point(other.position),other.Spatial?other.envelope.Horizontal:.275f))return false;
            return true;
        }
        bool SpatialClear(Resident r,NavPoint p)=>SpatialClear(SanctuaryNavigationSurface.Vector(p),r.habitat,r.uid,r.medium,r.envelope);
        void ProjectSpatial(DragonState d,DragonVisualStage stage,DragonVisualCatalog.Entry entry,
            DragonVisualCatalog.Match match,NavigationMedium medium)
        {
            string uid=d.Uid,habitat=d.Location.HabitatId,variant=d.Mutation.ToWire();
            if(spatialFaults.Contains(uid)){unavailable[uid]="spatial-clip-fault";RemoveResident(uid);return;}
            if(!SpatialMetadata(entry,match,medium,out var bounds,out float speed,out float turn))
            {unavailable[uid]="spatial-metadata-missing";RemoveResident(uid);return;}
            if(!navigation.HasAuthoredVolume(habitat,medium))
            {unavailable[uid]="spatial-volume-missing";RemoveResident(uid);return;}
            if(residents.TryGetValue(uid,out var old) &&
                (!old.Spatial||old.medium!=medium||old.stage!=stage||old.spatialEntry!=entry||old.variant!=variant))
            {
                // Stage/variant/model changes reproject from a validated authored spawn, not a live transition.
                RemoveResident(uid);manager.Despawn(uid);
            }
            if(!residents.TryGetValue(uid,out var r))
            {
                string label=medium==NavigationMedium.Swim?"swim":"air";
                var spawn=locations.SpawnPointsIn(habitat).FirstOrDefault(x=>x.medium==label&&SpatialClear(x.Position,habitat,uid,medium,bounds));
                if(spawn==null){unavailable[uid]="spatial-no-safe-spawn";return;}
                r=new Resident{uid=uid,habitat=habitat,species=d.SpeciesId,variant=variant,stage=stage,
                    medium=medium,position=spawn.Position,rotation=Quaternion.Euler(0,spawn.Rotation.eulerAngles.y,0),
                    spatialEntry=entry,envelope=bounds,spatialSpeed=speed,spatialTurn=turn,occupant=nextOccupant++};
                residents.Add(uid,r);
            }
            r.genotype=d.Genotype?.Select(g=>new GenotypeEntry(g.Key,g.Value.ToString())).ToArray()??Array.Empty<GenotypeEntry>();
            r.personality=DragonPersonalityPresentation.StyleFromName(d.Personality.ToString());
        }
        bool SpatialVisualReady(Resident r,bool movementRequested)
        {
            bool live=manager.TryGet(r.uid,out var model);
            return SpatialResidentPolicy.MayTranslate(live,live&&model.Entry==r.spatialEntry,
                live&&model.ShownLocomotion==SpatialClip(r),live&&model.CurrentInteraction!=DragonInteraction.None,movementRequested);
        }
        void SpatialStep(Resident r,float dt,out float speed,out float turn,out float vertical)
        {
            speed=turn=vertical=0;
            // No sleep, food, pet or land on an unsupported pose; pause translation in gameplay Rest.
            var b=r.decision?.Behavior;
            bool move=r.decision?.Role==BehaviorRole.Presentation &&
                (b==DragonBehavior.Explore || b==DragonBehavior.Play ||
                    (r.medium==NavigationMedium.Swim?b==DragonBehavior.Swim:b==DragonBehavior.Fly));
            r.fallback=move?"same-medium-review":"same-medium-hold; care pose unavailable";
            if(!SpatialVisualReady(r,move)||dt<=0)return;
            if(!SpatialClear(r,SanctuaryNavigationSurface.Point(r.position)))
            {r.fallback="current-spatial-envelope-blocked";ResetRoute(r);return;}
            if(r.route==null || !r.route.Success)
            {
                if(Time.unscaledTime<r.retryAt||routeBudget<=0)return;
                var label=r.medium==NavigationMedium.Swim?"swim":"air";
                var points=locations.SpawnPointsIn(r.habitat).Where(x=>x.medium==label).OrderBy(x=>x.id,StringComparer.Ordinal).ToArray();
                if(points.Length==0)return;
                var candidate=points[(r.spatialDestination++ & int.MaxValue)%points.Length];
                if((candidate.Position-r.position).sqrMagnitude<.04f||!SpatialClear(r,SanctuaryNavigationSurface.Point(candidate.Position)))
                {r.retryAt=Time.unscaledTime+.5f;return;}
                routeBudget--;plannedRoutes++;
                r.route=SpatialMediumReview.Plan(SanctuaryNavigationSurface.Point(r.position),SanctuaryNavigationSurface.Point(candidate.Position),
                    r.medium,p=>SpatialClear(r,p),768);
                r.routeIndex=0;r.routeState=r.route.Status.ToString();
                if(!r.route.Success){failedRoutes++;r.retryAt=Time.unscaledTime+1f;return;}
            }
            if(r.routeIndex>=r.route.Waypoints.Count)
            {ResetRoute(r);r.retryAt=Time.unscaledTime+1f;return;}
            var target=SanctuaryNavigationSurface.Vector(r.route.Waypoints[r.routeIndex]);
            var heading=Vector3.ProjectOnPlane(target-r.position,Vector3.up);
            if(heading.sqrMagnitude>.000001f)
            {
                var before=r.rotation;r.rotation=Quaternion.RotateTowards(before,Quaternion.LookRotation(heading),r.spatialTurn*dt);
                turn=SignedTurn(before,r.rotation,dt);
                if(Vector3.Angle(r.rotation*Vector3.forward,heading)>10f)return;
            }
            var next=Vector3.MoveTowards(r.position,target,r.spatialSpeed*dt);
            if(!SanctuaryRoutePlanner.SegmentClear(SanctuaryNavigationSurface.Point(r.position),SanctuaryNavigationSurface.Point(next),
                p=>SpatialClear(r,p),.025f))
            {ResetRoute(r);r.routeState="spatial-dynamic-block";r.retryAt=Time.unscaledTime+.5f;return;}
            var delta=next-r.position;r.position=next;speed=delta.magnitude/dt;vertical=delta.y/dt;
            if((target-next).sqrMagnitude<.000001f)r.routeIndex++;
        }

        long NowMs=>State?.Clock.LastProcessedAtMs??0L;
        BehaviorPhase CurrentPhase()
        {
            var living=LivingEnvironment.Active;
            if(living==null || living.timeOfDay==null || living.timeOfDay.data==null)return BehaviorPhase.Day;
            // Use the phase actually applied to lighting/audio, not a second independent clock.
            return living.Phase switch {
                Dravio.EnvironmentArt.DayPhase.Dawn=>BehaviorPhase.Dawn,
                Dravio.EnvironmentArt.DayPhase.Day=>BehaviorPhase.Day,
                Dravio.EnvironmentArt.DayPhase.Dusk=>BehaviorPhase.Dusk,
                _=>BehaviorPhase.Night};
        }
        void RemoveResident(string uid)
        {
            if(Social!=null && Social.Contains(uid))StopSocial("resident-removed");
            if(residents.TryGetValue(uid,out var r))ReleasePoint(r);
            residents.Remove(uid);
        }
        void ReleasePoint(Resident r)
        {
            if(r.point!=null && interactions!=null)interactions.Release(r.point.id,r.occupant);
            r.point=null;r.usedPoint=false;r.hasTarget=false;r.rest=0f;ResetRoute(r);
        }
        void RefreshDecisions()
        {
            var current=CurrentPhase();long now=NowMs;
            if(!decisionsDirty && current==phase && now<decisionDeadline)return;
            phase=current;decisionsDirty=false;decisionDeadline=now+1000;
            var all=BehaviorQueries.All(State,now,phase);
            // Same canonical decision cadence; no social candidate writes or rewards.
            socialCandidates=ProgressionQueries.SocialInteractionCandidates(State,now,phase);
            foreach(var d in all)
            {
                if(!residents.TryGetValue(d.Uid,out var r))continue;
                var old=r.decision;
                if(old==null || old.Behavior!=d.Behavior || old.StartedAtMs!=d.StartedAtMs ||
                    old.TargetFoodId!=d.TargetFoodId || old.Phase!=d.Phase)ReleasePoint(r);
                r.decision=d;
                if(d.UntilMs>now)decisionDeadline=Math.Min(decisionDeadline,d.UntilMs);
            }
            if(foodId!=null)
            {
                var assigned=all.FirstOrDefault(d=>d.Behavior==DragonBehavior.SeekFood && d.TargetFoodId==foodId);
#if UNITY_EDITOR
                if(UseFoodGate && foodUid!=assigned?.Uid)foodGate.Clear();
#endif
                foodUid=assigned?.Uid;
            }
            RefreshProfile();
        }
        float MealSpeed(Resident r,Vector3 spot)
        {
            float distance=Vector3.Distance(r.position,spot);
            bool clear=distance<AutonomousPresentationPolicy.MealSlowdownDistance && Segment(r.position,spot,r.habitat);
            return AutonomousPresentationPolicy.MealApproachSpeed(distance,clear);
        }

        bool IsFoodTarget(Resident r)=>r.decision?.TargetFoodId!=null && mealSpots.ContainsKey(r.decision.TargetFoodId);

        /// <summary>U6.4: set by IslandHud; a dragon that already has an island bubble skips the IMGUI
        /// "Selected"/"Food" label so the two do not overlap (the bubble shows selection by its gold name).</summary>
        public System.Func<string,bool> LabelCovered;

        void OnGUI()
        {
            if(Event.current.type!=EventType.Repaint || !Ready || cameraView==null)return;
            bool selectedGetsFood=selected!=null && residents.TryGetValue(selected,out var selectedResident) && IsFoodTarget(selectedResident);
            var cameraRect=cameraView.pixelRect;
            var safe=Screen.safeArea;
            var view=Rect.MinMaxRect(Mathf.Max(cameraRect.xMin,safe.xMin),Mathf.Max(cameraRect.yMin,safe.yMin),
                Mathf.Min(cameraRect.xMax,safe.xMax),Mathf.Min(cameraRect.yMax,safe.yMax));
            var first=DrawDragonLabel(selected,selectedGetsFood?(vi?"Đang chọn · Thức ăn":"Selected · Food"):
                (vi?"Đang chọn":"Selected"),view,null);
            if(portions.Count==1 && !selectedGetsFood)DrawDragonLabel(foodUid,vi?"Thức ăn":"Food",view,first);
        }

        (float x,float y,float width,float height)? DrawDragonLabel(string uid,string label,Rect view,
            (float x,float y,float width,float height)? occupied)
        {
            if(string.IsNullOrEmpty(uid) || (LabelCovered!=null && LabelCovered(uid)) || !manager.TryGet(uid,out var instance))return null;
            var bounds=instance.WorldBounds();
            var point=cameraView.WorldToScreenPoint(bounds.center+Vector3.up*bounds.extents.y);
            if(point.z<=0 || !cameraView.pixelRect.Contains(new Vector2(point.x,point.y)))return null;
            var size=GUI.skin.box.CalcSize(new GUIContent(label));
            var placement=AutonomousPresentationPolicy.PlaceLabel(point.x-view.xMin,view.yMax-point.y,
                size.x,size.y,view.width,view.height,occupied);
            if(!placement.HasValue)return null;
            var pos=placement.Value;
            GUI.Label(new Rect(view.xMin+pos.x,Screen.height-view.yMax+pos.y,size.x,size.y),label,GUI.skin.box);
            return (pos.x,pos.y,size.x,size.y);
        }

        string FeedingNotice()
        {
            if(placingMeal)return vi?"Chạm đất để thả thức ăn.":"Tap the ground to scatter food.";
            if(portions.Count>1)return vi?$"Còn {portions.Count} phần ăn.":$"{portions.Count} portions left.";
            if(foodId==null)return vi?messageVi:messageEn;
            var assignment=BehaviorEngine.AssignmentOf(State,foodId,NowMs);
            if(assignment==null)return vi?messageVi:messageEn;
            var recipient=State.FindDragon(assignment.AssignedDragonUid);
            string reason="";
            if(assignment.TargetBlockedReason=="cooldown")
            {
                var target=State.FindDragon(assignment.TargetDragonUid);
                if(target!=null)reason=DragonName(target)+(vi?" vừa ăn. ":" just ate. ");
            }
            return reason+(recipient==null?(vi?"Chưa có rồng nhận thức ăn.":"No dragon has claimed the food."):
                (vi?"Rồng nhận thức ăn: ":"Food assigned to: ")+DragonName(recipient));
        }

        static string DragonName(DragonState dragon)=>string.IsNullOrEmpty(dragon.Nickname)?dragon.SpeciesId:dragon.Nickname;
        bool SocialEligible(Resident r,SocialInteractionKind kind,bool initiator)
        {
            if(r==null||r.Spatial||!r.walking||r.decision==null||!r.decision.Visible||
                r.decision.Role!=BehaviorRole.Presentation||r.decision.UntilMs<=NowMs||r.manualRestUntil>NowMs||
                IsFoodTarget(r)||r.decision.Behavior==DragonBehavior.Sleep||r.decision.Behavior==DragonBehavior.SeekFood||
                r.decision.Behavior==DragonBehavior.Eat||!manager.TryGet(r.uid,out var live)||
                live.Clips==null||live.Entry==null||live.Entry.animQaStatus!="PASS"||
                (live.Entry.stage!=r.stage&&live.Entry.stage!=DragonVisualStage.Unstaged)||
                !Presentable(State.FindDragon(r.uid)))return false;
            if(kind==SocialInteractionKind.PlayTogether)return live.Clips.CanPlay(DragonInteraction.Happy);
            if(kind==SocialInteractionKind.RestTogether)return live.Clips.Has(DragonLocomotion.Rest);
            if(kind==SocialInteractionKind.CuriousApproach)return !initiator||live.Clips.CanPlay(DragonInteraction.Greet);
            return false;
        }
        void StopSocial(string reason)
        {
            if(Social==null)return;
            socialLast=reason;socialScheduler.Release();
            if(socialA!=null)ReleasePoint(socialA);
            if(socialB!=null)ReleasePoint(socialB);
            socialA=socialB=null;socialRetryAt=Time.unscaledTime+1f;
        }
        bool SocialSpacing(Vector3 a,Vector3 b,float min)=>SocialPresentationScheduler.Spacing(
            SanctuaryNavigationSurface.Point(a),SanctuaryNavigationSurface.Point(b),min);
        bool TrySocialGoal(Resident a,Resident b,float min,out Vector3 goal)
        {
            goal=a.position;
            if(!SocialSpacing(a.position,b.position,min))return false;
            // Partner stays at its existing position; initiator may use only authored ground endpoints.
            if(Vector3.Distance(a.position,b.position)<=min+.75f && Segment(a.position,b.position,a.habitat))return true;
            foreach(var p in locations.SpawnPointsIn(a.habitat).Where(x=>string.IsNullOrEmpty(x.medium)||x.medium=="ground")
                .OrderBy(x=>(x.Position-a.position).sqrMagnitude).ThenBy(x=>x.id,StringComparer.Ordinal))
            {
                if(!SocialSpacing(p.Position,b.position,min+.12f) || Vector3.Distance(p.Position,b.position)>min+.75f||
                    !ClearPoint(p.Position,a.habitat,a.uid)||!Segment(p.Position,b.position,a.habitat))continue;
                goal=p.Position;return true;
            }
            return false;
        }
        bool Faces(Resident a,Resident b)
        {
            var d=b.position-a.position;d.y=0;
            return d.sqrMagnitude>.000001f&&Vector3.Angle(a.rotation*Vector3.forward,d)<=15f;
        }
        void PrepareSocial()
        {
            if(!SocialEnabled||foodId!=null)
            {StopSocial("disabled-or-food-priority");return;}
            if(Social!=null)
            {
                bool valid=residents.TryGetValue(Social.A,out var a)&&ReferenceEquals(a,socialA)&&
                    residents.TryGetValue(Social.B,out var b)&&ReferenceEquals(b,socialB)&&
                    socialA.stage==socialStageA&&socialB.stage==socialStageB&&
                    socialA.variant==socialVariantA&&socialB.variant==socialVariantB&&
                    socialA.habitat==Social.Habitat&&socialB.habitat==Social.Habitat&&ViewZone==Social.Zone&&
                    socialCandidates.Any(Social.Matches)&&SocialEligible(socialA,Social.Kind,true)&&SocialEligible(socialB,Social.Kind,false);
                bool arrived=valid&&(socialA.position-socialGoal).sqrMagnitude<=.0036f&&
                    Vector3.Distance(socialA.position,socialB.position)<=Social.MinDistance+.80f&&
                    Segment(socialA.position,socialB.position,socialA.habitat);
                Social.Tick(NowMs,valid,arrived,valid&&Faces(socialA,socialB)&&Faces(socialB,socialA),
                    valid&&GroundMotionReady(socialA)&&GroundMotionReady(socialB),
                    valid&&SocialSpacing(socialA.position,socialB.position,Social.MinDistance));
                if(Social.Terminal){StopSocial(Social.Phase.ToString());return;}
                if(socialA.routeFailed){StopSocial("route-failed");return;}
                return;
            }
            if(Time.unscaledTime<socialRetryAt)return;
            socialRetryAt=Time.unscaledTime+1f;
            foreach(var c in socialCandidates)
            {
                if(c.ZoneId!=ViewZone||c.PartnerUid==null||!residents.TryGetValue(c.InitiatorUid,out var a)||
                    !residents.TryGetValue(c.PartnerUid,out var b)||a.habitat!=c.HabitatId||b.habitat!=c.HabitatId||
                    !SocialEligible(a,c.Kind,true)||!SocialEligible(b,c.Kind,false)||
                    !GroundMotionReady(a)||!GroundMotionReady(b)||
                    !TrySocialGoal(a,b,Math.Max(.55f,c.MinDistanceDm/10f),out var target))continue;
                if(!socialScheduler.TryBegin(c,NowMs,true))continue;
                socialA=a;socialB=b;socialGoal=target;socialStageA=a.stage;socialStageB=b.stage;
                socialVariantA=a.variant;socialVariantB=b.variant;
                ReleasePoint(a);ReleasePoint(b);socialLast="started";break;
            }
        }
        DragonBehaviorIntent SocialIntent(Resident r,float dt,out float speed,out float turn,out bool look,out Vector3 target)
        {
            speed=turn=0;look=true;
            var partner=r==socialA?socialB:socialA;target=partner.position+Vector3.up*.25f;
            r.fallback="social-preview: "+Social.Kind+" / "+Social.Phase;
            if(Social.Phase==SocialPresentationPhase.Approach)
            {
                if(r==socialA && !MoveToward(r,socialGoal,dt,out speed,out turn))
                    return speed>0?DragonBehaviorIntent.Walk:DragonBehaviorIntent.Idle;
                return DragonBehaviorIntent.Idle;
            }
            if(Social.Phase==SocialPresentationPhase.Face)
            {
                var delta=partner.position-r.position;delta.y=0;
                if(delta.sqrMagnitude>.000001f&&GroundMotionReady(r))
                {var old=r.rotation;r.rotation=Quaternion.RotateTowards(old,Quaternion.LookRotation(delta),180f*dt);turn=SignedTurn(old,r.rotation,dt);}
                return DragonBehaviorIntent.Idle;
            }
            return Social.Kind==SocialInteractionKind.RestTogether?DragonBehaviorIntent.Rest:
                Social.Kind==SocialInteractionKind.PlayTogether?DragonBehaviorIntent.Play:
                r==socialA?DragonBehaviorIntent.Greet:DragonBehaviorIntent.Idle;
        }
        bool TryReservePoint(Resident r,DragonBehavior behavior)
        {
            if(interactions==null || !r.walking)return false;
            SanctuaryLivingData.InteractionPoint best=null;float score=float.MaxValue;
            foreach(var p in interactions.All)
            {
                if(p==null || p.habitatId!=r.habitat || p.requiresSwim || p.requiresFly ||
                    (!string.IsNullOrEmpty(p.medium) && p.medium!="ground") ||
                    !AutonomousPresentationPolicy.PointMatches(behavior,p.kind) || !interactions.IsFree(p.id) ||
                    !ClearPoint(p.Position,r.habitat,r.uid) || (p.id==r.blockedPoint && Time.unscaledTime<r.blockedUntil))continue;
                float distance=Vector3.Distance(r.position,p.Position)-
                    (p.Prefers((Dravio.EnvironmentArt.DayPhase)phase) ? .5f : 0f);
                if(distance<score){best=p;score=distance;}
            }
            if(best==null || !interactions.TryReserve(best.id,r.occupant))return false;
            r.point=best;r.target=best.Position;r.hasTarget=true;r.usedPoint=false;ResetRoute(r);
            return true;
        }
        bool GroundMotionReady(Resident r)
        {
            var st=sync.MotionStatusOf(r.uid);
            return U54MotionPolicy.MayMove(st.live,st.visible,st.mode==DragonMotionMode.Ground,st.canTranslate,st.actionPlaying) &&
                manager.TryGet(r.uid,out var live) && live.ShownLocomotion!=DragonLocomotion.Sleep && live.CurrentInteraction==DragonInteraction.None;
        }
        static float SignedTurn(Quaternion old,Quaternion next,float dt)=>dt>0f?Mathf.DeltaAngle(old.eulerAngles.y,next.eulerAngles.y)/dt:0f;
        bool MoveToward(Resident r,Vector3 target,float dt,out float speed,out float turnRate,
            float walkSpeed=AutonomousPresentationPolicy.GroundWalkSpeed)
        {
#if UNITY_EDITOR
            if(controllerTrace!=null && controllerTrace.Recording)traceAttempts++;
#endif
            speed=0f;turnRate=0f;
            if(!GroundMotionReady(r))return false;
            if((target-r.position).sqrMagnitude<.0036f && ClearPoint(target,r.habitat,r.uid)){
#if UNITY_EDITOR
                if(controllerTrace!=null && controllerTrace.Recording)traceArrivals++;
#endif
                ResetRoute(r);return true;
            }
            if(!r.walking){r.routeFailed=true;r.routeState="no-walk-clip";return false;}
            if(!EnsureRoute(r,target))return false;
            var points=r.route.Waypoints;
            while(r.routeIndex<points.Count && (SanctuaryNavigationSurface.Vector(points[r.routeIndex])-r.position).sqrMagnitude<.0036f)
                r.routeIndex++;
            if(r.routeIndex>=points.Count)
            {
                if(ClearPoint(target,r.habitat,r.uid)){
#if UNITY_EDITOR
                if(controllerTrace!=null && controllerTrace.Recording)traceArrivals++;
#endif
                ResetRoute(r);return true;
            }
                r.route=null;r.routeIndex=0;r.routeState="dynamic-goal-block";r.retryAt=Time.unscaledTime+.45f;return false;
            }
            var waypoint=SanctuaryNavigationSurface.Vector(points[r.routeIndex]);
            var delta=waypoint-r.position;delta.y=0f;
            if(delta.sqrMagnitude<.000001f){r.routeIndex++;return false;}
            var next=Vector3.MoveTowards(r.position,waypoint,walkSpeed*dt);
            if(!ClearPoint(next,r.habitat,r.uid) || !Segment(r.position,next,r.habitat) ||
                ((spatialForView || (Social!=null && Social.Contains(r.uid))) && !SanctuaryRoutePlanner.SegmentClear(SanctuaryNavigationSurface.Point(r.position),
                    SanctuaryNavigationSurface.Point(next),p=>Separated(SanctuaryNavigationSurface.Vector(p),r.uid),.025f)))
            {
                // Stop, never slide through a moving resident. Replan after a short cooldown.
                r.route=null;r.routeIndex=0;r.routeState="dynamic-block";r.routeFailed=false;
#if UNITY_EDITOR
                if(controllerTrace!=null && controllerTrace.Recording)traceBlocked++;
#endif
                r.retryAt=Time.unscaledTime+.45f;return false;
            }
            var old=r.rotation;var heading=Quaternion.LookRotation(delta);
            r.rotation=Quaternion.RotateTowards(old,heading,180f*dt);
            turnRate=SignedTurn(old,r.rotation,dt);
            if(Quaternion.Angle(r.rotation,heading)<15f)
            {speed=dt>0f?Vector3.Distance(r.position,next)/dt:0f;r.position=next;}
            if((target-r.position).sqrMagnitude<.0036f){
#if UNITY_EDITOR
                if(controllerTrace!=null && controllerTrace.Recording)traceArrivals++;
#endif
                ResetRoute(r);return true;
            }
            return false;
        }
        void OnDrawGizmos()
        {
            if(!Application.isPlaying || !drawSelectedRoute || selected==null ||
                !residents.TryGetValue(selected,out var r) || r.route==null || !r.route.Success)return;
            Gizmos.color=Color.cyan;var previous=r.position;
            for(int i=r.routeIndex;i<r.route.Waypoints.Count;i++)
            {
                var point=SanctuaryNavigationSurface.Vector(r.route.Waypoints[i]);
                Gizmos.DrawLine(previous+Vector3.up*.05f,point+Vector3.up*.05f);
                Gizmos.DrawWireSphere(point+Vector3.up*.05f,.07f);previous=point;
            }
        }
#if UNITY_EDITOR
        bool FoodFacing(Resident r)
        {
            var delta=foodPoint-r.position;delta.y=0;
            return delta.sqrMagnitude<.0001f || Vector3.Angle(r.rotation*Vector3.forward,delta)<=FoodActionPresentationGate.FacingDegrees;
        }
        bool FoodMotionReady(Resident r)=>GroundMotionReady(r) && manager.TryGet(r.uid,out var live) &&
            live.ShownLocomotion==DragonLocomotion.Idle && live.CurrentInteraction==DragonInteraction.None;
        bool FoodPerceptionEligible(Resident r)
        {
            // Actual gameplay authority stays in the existing policy. Health recovery wins over the experiment.
            return !(r.decision?.Role==BehaviorRole.Gameplay && r.decision.Behavior==DragonBehavior.Rest) &&
                AutonomousPresentationPolicy.CanConsumeMarker(State,r.uid,foodUid,foodId,NowMs,phase);
        }
        DragonBehaviorIntent ExperimentalFoodIntent(Resident r,float dt,out float speed,out float turnRate,out bool look,out Vector3 lookTarget)
        {
            speed=0;turnRate=0;look=false;lookTarget=Vector3.zero;
            var drop=State.Food.Drops.FirstOrDefault(f=>f.Id==foodId);
            bool eligible=drop!=null && FoodPerceptionEligible(r);
            double distance=Vector3.Distance(r.position,foodPoint);
            // Conservative navigable-segment proxy for sight; not a physics raycast or full sensory model.
            bool clear=eligible && Segment(r.position,foodPoint,r.habitat);
            if(!foodGate.Observe(r.uid,foodId,r.habitat,NowMs,drop?.ExpiresAtMs??0,eligible,distance,clear))
            {
                r.fallback="perception: "+foodGate.Phase;
                if(r.decision?.Role==BehaviorRole.Gameplay && r.decision.Behavior==DragonBehavior.Rest)
                    return DragonBehaviorIntent.Recover;
                return DragonBehaviorIntent.Idle;
            }
            if(r.point!=null)ReleasePoint(r);
            look=true;lookTarget=foodPoint;
            if(distance>FoodActionPresentationGate.Reach)
            {
                MoveToward(r,foodPoint,dt,out speed,out turnRate,AutonomousPresentationPolicy.MealApproachSpeed((float)distance,clear));
                foodGate.Presentation(false,false,false);
                return speed>0?DragonBehaviorIntent.Walk:DragonBehaviorIntent.Idle;
            }
            var delta=foodPoint-r.position;delta.y=0;
            if(delta.sqrMagnitude>.0001f && GroundMotionReady(r))
            {
                var old=r.rotation;
                r.rotation=Quaternion.RotateTowards(old,Quaternion.LookRotation(delta),180f*dt);
                turnRate=SignedTurn(old,r.rotation,dt);
            }
            foodGate.Presentation(true,FoodFacing(r),FoodMotionReady(r));
            return DragonBehaviorIntent.Idle;
        }
#endif
        DragonBehaviorIntent GroundIntent(Resident r,float dt,out float speed,out float turnRate,out bool look,out Vector3 lookTarget)
        {
            speed=0f;turnRate=0f;look=false;lookTarget=Vector3.zero;r.fallback=null;
            var behavior=r.decision?.Behavior??DragonBehavior.Idle;
#if UNITY_EDITOR
            if(UseFoodGate && IsFoodTarget(r))return ExperimentalFoodIntent(r,dt,out speed,out turnRate,out look,out lookTarget);
#endif
            if(IsFoodTarget(r))
            {
                var foodPoint=mealSpots[r.decision.TargetFoodId];
                var d=State.FindDragon(r.uid);var drop=State.Food.Drops.FirstOrDefault(f=>f.Id==r.decision.TargetFoodId);
                if(d!=null && drop!=null && BehaviorEngine.CanEat(d,drop,NowMs))
                {
                    if(r.point!=null)ReleasePoint(r);
                    look=true;lookTarget=foodPoint;
                    float approachSpeed=MealSpeed(r,foodPoint);
                    if(approachSpeed>0)
                    {
                        MoveToward(r,foodPoint,dt,out speed,out turnRate,approachSpeed);
                        if(speed==0f)r.fallback="food-path: "+r.routeState;
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
            {
                // Do not animate movement in a medium without an authored volume and a certified transition.
                var medium=behavior==DragonBehavior.Swim?NavigationMedium.Swim:
                    behavior==DragonBehavior.Fly?NavigationMedium.Air:NavigationMedium.Burrow;
                var motionMode=behavior==DragonBehavior.Swim?DragonMotionMode.Water:
                    behavior==DragonBehavior.Fly?DragonMotionMode.Air:DragonMotionMode.Underground;
                var readiness=sync.ReadinessFor(r.uid,motionMode);
                r.fallback=readiness==DragonMotionReadiness.Unsupported?"model-medium-unsupported":
                    medium==NavigationMedium.Burrow?"burrow-transition-not-certified":
                    navigation.HasAuthoredVolume(r.habitat,medium)?"medium-transition-not-certified":"no-authored-3d-volume";
                return DragonBehaviorIntent.Rest;
            }
            if(behavior==DragonBehavior.Sleep)return DragonBehaviorIntent.Sleep;
            if(behavior==DragonBehavior.Idle)return DragonBehaviorIntent.Idle;
            // Approach authored points only for compatible behaviors, using the existing reservations.
            // A real health/disaster Rest must happen in place, never walk to a scenic spot.
            if(behavior==DragonBehavior.Rest && r.decision?.Role==BehaviorRole.Gameplay)
            {if(r.point!=null)ReleasePoint(r);return DragonBehaviorIntent.Recover;}
            bool pointBehavior=AutonomousPresentationPolicy.AllowsPoint(behavior,r.decision?.Role??BehaviorRole.Presentation);
            if(pointBehavior && r.point==null)
            {
                r.rest-=dt;
                if(r.rest<=0f){if(!TryReservePoint(r,behavior))r.rest=2f;}
            }
            if(r.point!=null)
            {
                bool arrived=MoveToward(r,r.point.Position,dt,out speed,out turnRate);
                if(r.routeFailed)
                {
                    r.blockedPoint=r.point.id;r.blockedUntil=Time.unscaledTime+5f;
                    string reason=r.routeState;ReleasePoint(r);r.rest=1f;r.fallback="point-path: "+reason;
                    return DragonBehaviorIntent.Idle;
                }
                if(!arrived)return speed>0f?DragonBehaviorIntent.Walk:DragonBehaviorIntent.Idle;
                if(!GroundMotionReady(r))return DragonBehaviorIntent.Idle;
                var old=r.rotation;
                r.rotation=Quaternion.RotateTowards(old,r.point.Rotation,180f*dt);
                turnRate=SignedTurn(old,r.rotation,dt);
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
                        (x.Position-r.position).sqrMagnitude>.25f && ClearPoint(x.Position,r.habitat,r.uid) &&
                        (x.id!=r.blockedPoint || Time.unscaledTime>=r.blockedUntil));
                    if(p==null){r.rest=2f;return DragonBehaviorIntent.Idle;}
                    r.target=p.Position;r.hasTarget=true;ResetRoute(r);
                }
                bool arrived=MoveToward(r,r.target,dt,out speed,out turnRate);
                if(r.routeFailed)
                {
                    var failed=locations.SpawnPointsIn(r.habitat).FirstOrDefault(x=>(x.Position-r.target).sqrMagnitude<.0004f);
                    r.blockedPoint=failed?.id;r.blockedUntil=Time.unscaledTime+5f;r.fallback="explore-path: "+r.routeState;
                    r.hasTarget=false;r.rest=2f;ResetRoute(r);
                }
                else if(arrived){r.hasTarget=false;r.rest=2f;}
                return speed>0f?DragonBehaviorIntent.Walk:DragonBehaviorIntent.Idle;
            }
            return DragonBehaviorIntent.Idle;
        }
        void Update()
        {
            if(!active || viewSuspended)return;
            if(session==null && host.Session!=null){session=host.Session;session.DomainEvents+=OnEvents;}
            if(interactions==null && LivingEnvironment.Active!=null)interactions=LivingEnvironment.Active.interactions;
            if(!ReferenceEquals(lastState,State))RefreshRoster();
            bool ready=Ready;
            if(ready!=previousReady){previousReady=ready;RefreshButtons();decisionsDirty=true;}
            if(!ready){StopSocial("session-not-ready");return;}
            RefreshDecisions();
            PrepareSocial();
            float dt=Mathf.Min(Time.deltaTime,.1f);
#if UNITY_EDITOR
            if(controllerTrace!=null && controllerTrace.Recording && traceAdaptiveMode!=experimentalAdaptiveGroundRouting)
            {controllerTrace.Stop();Debug.LogWarning("TL.13 route mode changed; trace stopped to preserve A/B comparability.");}
            bool tracing=controllerTrace!=null && controllerTrace.Recording;
            long traceStart=tracing?System.Diagnostics.Stopwatch.GetTimestamp():0L;
            int tracePlansBefore=plannedRoutes,traceFailsBefore=failedRoutes,traceMoving=0;
            int traceAdaptiveBefore=adaptiveAttempts,traceRescueBefore=adaptiveRescues;
            traceAttempts=traceArrivals=traceBlocked=0;
#endif
            frames.Clear();
            routeBudget=1; // bounded work per rendered frame, even with many residents
            foreach(var r in residents.Values)
            {
                float speed,turnRate,vertical=0;bool look=false;Vector3 lookTarget=Vector3.zero;
                DragonBehaviorIntent intent;
                if(r.Spatial){SpatialStep(r,dt,out speed,out turnRate,out vertical);intent=DragonBehaviorIntent.None;}
                else if(Social!=null && Social.Contains(r.uid))intent=SocialIntent(r,dt,out speed,out turnRate,out look,out lookTarget);
                else intent=GroundIntent(r,dt,out speed,out turnRate,out look,out lookTarget);
                r.shownIntent=intent;
#if UNITY_EDITOR
                if(tracing && speed>0f)traceMoving++;
#endif
                frames.Add(new SanctuaryDragonFrame{uid=r.uid,speciesId=r.species,variantId=r.variant,stage=r.stage,
                    genotype=r.genotype,visible=true,position=r.position,rotation=r.rotation,intent=intent,
                    locomotion=r.Spatial?SpatialClip(r):DragonLocomotion.Idle,
                    personality=r.personality,speed=speed,turnRateDegPerSec=turnRate,verticalSpeed=vertical,hasLookTarget=look,lookTarget=lookTarget});
            }
#if UNITY_EDITOR
            long traceSyncStart=tracing?System.Diagnostics.Stopwatch.GetTimestamp():0L;
#endif
            var report=sync.Sync(frames,dt);substitutions=report.substituted;
            removals.Clear();
            foreach(var frame in frames)
            {
                if(!residents.TryGetValue(frame.uid,out var r)||!r.Spatial)continue;
                if(!manager.TryGet(r.uid,out var live)||live.Entry!=r.spatialEntry||live.ShownLocomotion!=SpatialClip(r))
                {spatialFaults.Add(r.uid);unavailable[r.uid]="spatial-clip-fault";manager.Despawn(r.uid);removals.Add(r.uid);continue;}
                manager.SetMotionShape(r.uid,SpatialMode(r),frame.turnRateDegPerSec,frame.verticalSpeed,frame.speed);
            }
            foreach(var id in removals)RemoveResident(id);
            if(removals.Count>0)RefreshProfile();
#if UNITY_EDITOR
            if(tracing)
            {
                long traceEnd=System.Diagnostics.Stopwatch.GetTimestamp();
                double scale=1000.0/System.Diagnostics.Stopwatch.Frequency;
                float min=float.PositiveInfinity;
                for(int i=0;i<frames.Count;i++)
                for(int j=0;j<i;j++)min=Mathf.Min(min,Vector3.Distance(frames[i].position,frames[j].position));
                controllerTrace.Add(new ControllerFrameSample(dt,frames.Count,traceMoving,traceAttempts,traceArrivals,
                    traceBlocked,plannedRoutes-tracePlansBefore,failedRoutes-traceFailsBefore,
                    report.substituted,float.IsInfinity(min)?-1:min,
                    (traceSyncStart-traceStart)*scale,(traceEnd-traceSyncStart)*scale,
                    adaptiveAttempts-traceAdaptiveBefore,adaptiveRescues-traceRescueBefore));
            }
#endif
            if(report.spawned>0 || report.despawned>0 || report.stageChanged>0)RefreshProfile();
            if(foodId!=null && Time.unscaledTime>=consumeAfter)
            {
                consumeAfter=Time.unscaledTime+.2f;
                foreach(var diner in residents.Values.ToArray())
                    if(IsFoodTarget(diner))Consume(diner.uid,diner.decision.TargetFoodId);
            }
            ReadSelection();
        }

#if UNITY_EDITOR
        [ContextMenu("Technology Lab/Start controller trace")]
        void StartControllerTrace()
        {
            if(!Application.isPlaying){Debug.LogWarning("Controller trace requires Play Mode.");return;}
            traceAdaptiveMode=experimentalAdaptiveGroundRouting;
            controllerTrace=new ControllerFrameTrace(1200);
            Debug.Log("Dravio TL.10: recording up to 1200 ready controller frames; adaptive="+traceAdaptiveMode+". Stop/export before leaving Play Mode.");
        }
        [ContextMenu("Technology Lab/Stop and report trace")]
        void StopControllerTrace()
        {
            if(controllerTrace==null)return;
            controllerTrace.Stop();Debug.Log(controllerTrace.Summary());
        }
        [ContextMenu("Technology Lab/Export trace CSV")]
        void ExportControllerTrace()
        {
            if(controllerTrace==null || controllerTrace.Count==0){Debug.LogWarning("No controller trace.");return;}
            controllerTrace.Stop();
            string path=UnityEditor.EditorUtility.SaveFilePanel("Export controller trace","",
                "dravio-controller-trace-"+(traceAdaptiveMode?"adaptive":"baseline"),"csv");
            if(!string.IsNullOrEmpty(path))System.IO.File.WriteAllText(path,controllerTrace.Csv());
            Debug.Log(controllerTrace.Summary());
        }
#endif

        void ReadSelection()
        {
            if(cameraView==null)cameraView=Camera.main;
            if(cameraView==null)return;
            Vector2 point;
            var touch=Touchscreen.current;
            var mouse=Mouse.current;
            bool useTouch=touch!=null && (touch.primaryTouch.press.isPressed || touch.primaryTouch.press.wasReleasedThisFrame);
            if(touch!=null && touch.touches.Count(t=>t.press.isPressed)>1){tapPending=false;return;}
            var button=useTouch?touch.primaryTouch.press:mouse?.leftButton;
            if(button==null){tapPending=false;return;}
            point=useTouch?touch.primaryTouch.position.ReadValue():mouse.position.ReadValue();
            if(button.wasPressedThisFrame)
            {
                tapStart=point;touchTap=useTouch;
                tapPending=WorldPoint(point,new Rect(0,0,1,1));
            }
            if(!tapPending)return;
            float slop=Mathf.Max(8f,Mathf.Min(Screen.width,Screen.height)*.012f);
            if(touchTap!=useTouch || (point-tapStart).sqrMagnitude>slop*slop){tapPending=false;return;}
            if(!button.wasReleasedThisFrame)return;
            tapPending=false;
            if(!WorldPoint(point,new Rect(0,0,1,1)))return;
            var ray=cameraView.ScreenPointToRay(point);
            bool direct=worldHud!=null && worldHud.Collapsed;
            if(!placingMeal)
            {
                string hit=null;float nearest=float.PositiveInfinity;
                foreach(var r in residents.Values)
                    if(manager.TryGet(r.uid,out var instance) && instance.WorldBounds().IntersectRay(ray,out float distance) && distance<nearest)
                    {nearest=distance;hit=r.uid;}
                if(hit!=null && Physics.Raycast(ray,out var obstruction,nearest) && obstruction.collider!=null &&
                    obstruction.collider.GetComponentInParent<DragonVisualInstance>()==null)return;
                if(hit!=null)
                {
                    Select(hit);
                    if(direct)Pet();
                    return;
                }
            }
            if(placingMeal || (direct && foodId==null))
            {
                var selectedDragon=Selected;
                if(selectedDragon==null || !residents.TryGetValue(selectedDragon.Uid,out var anchor)){placingMeal=false;return;}
                var ground=new Plane(Vector3.up,anchor.position);
                if(ground.Raycast(ray,out float distanceToGround))
                {
                    var groundPoint=ray.GetPoint(distanceToGround);
                    if(locations!=null && locations.TryGroundHeight(groundPoint,out float groundY))
                    {
                        groundPoint.y=groundY;
                        if(navigation!=null && navigation.Clear(groundPoint,anchor.habitat,NavigationMedium.Ground))
                        {
                            DropMeal(groundPoint);
                            return;
                        }
                    }
                }
                Message("Choose walkable ground in this habitat.","Chọn chỗ đất trống trong sinh cảnh này.");
                return;
            }
        }

        void Select(string id){if(selected!=id){selected=id;travelHabitat=null;StopFollow();if(residents.TryGetValue(id,out var r)&&!r.Spatial&&manager.IsLive(id)&&(Social==null||!Social.Contains(id)))sync.React(id,DragonReaction.Called);}RefreshProfile();}
        void NextDragon()
        {
            if(!Ready)return;
            var ids=State.Dragons.Where(d=>d.Location.Kind!=LocationKind.Released).Select(d=>d.Uid).ToArray();
            string next=LivingResidentPolicy.NextUid(ids,selected);
            if(next!=null)Select(next);
        }
        void Message(string en,string vn){feedbackUntil=Time.unscaledTime+2.5f;messageEn=en;messageVi=vn;if(status!=null)status.text=vi?vn:en;}
        bool Send(Func<string,GameCommand> build)
        {
            if(!Ready)return false;
            var error=session.Send(build);
            if(error!=null){Message("Rejected: "+error.WireCode,"Lệnh bị từ chối: "+error.WireCode);return false;}
            return true;
        }
        void Pet()
        {
            var d=Selected;if(d==null || !residents.TryGetValue(d.Uid,out var r)||r.Spatial)return;
            if(Social!=null && Social.Contains(d.Uid))StopSocial("player-pet");
            if(Send(id=>new PetCommand(id,d.Uid)))Message("Petted.","Đã vuốt ve.");
        }
        void Feed()
        {
            placingMeal=!placingMeal && Ready && foodId==null;
            RefreshProfile();
        }

        void DropMeal(Vector3 point)
        {
            placingMeal=false;
            var d=Selected;
            if(d==null || !residents.TryGetValue(d.Uid,out var r) || r.Spatial || foodId!=null || !Ready)return;
            var personality=BehaviorQueries.Personality(State,d.Uid);
            if(personality==null)return;
            var diets=SpeciesCatalog.Get(d.SpeciesId).Biology.DietIds;
            if(diets.Count==0)return;
            var diet=sharedFoodAllocation?personality.FavoriteDiet:diets[0];
            string targetUid=sharedFoodAllocation?null:d.Uid;
            StopSocial("player-feed");r.manualRestUntil=0;ReleasePoint(r);
            foodPoint=point;
            var probe=new FoodDrop{ZoneId=d.Location.ZoneId,HabitatId=r.habitat,DietId=diet,ExpiresAtMs=NowMs+CareRules.FoodTtlMs};
            var diners=residents.Values.Where(x=>!x.Spatial && manager.IsLive(x.uid) &&
                (x.position-point).sqrMagnitude<=16f && State.FindDragon(x.uid) is DragonState dragon &&
                BehaviorEngine.CanEat(dragon,probe,NowMs)).OrderBy(x=>x.uid==targetUid?0:1)
                .ThenBy(x=>(x.position-point).sqrMagnitude).ThenBy(x=>x.uid,StringComparer.Ordinal)
                .Take(CareRules.FoodMaxBase).Select(x=>x.uid).ToList();
            feedPending=true;
            try{if(Send(id=>new FeedCommand(id,diet,d.Location.ZoneId,d.Location.HabitatId,
                diners.FirstOrDefault()??targetUid,diners.Skip(1).ToArray())) && foodId!=null)
                Message("Food placed: "+diet,"Đã thả thức ăn: "+diet);}
            finally{feedPending=false;}
        }
        void Consume(string foodUid,string foodId)
        {
            var drop=State?.Food.Drops.FirstOrDefault(f=>f.Id==foodId);
            var d=State?.FindDragon(foodUid);
            if(drop==null){RemovePortion(foodId);return;}
            var foodPoint=mealSpots[foodId];
            if(d==null || !residents.TryGetValue(foodUid,out var r) || !manager.IsLive(foodUid) ||
                MealSpeed(r,foodPoint)>0 || !BehaviorEngine.CanEat(d,drop,NowMs))return;
            if(!GroundMotionReady(r) || !AutonomousPresentationPolicy.CanConsumeMarker(State,d.Uid,foodUid,foodId,NowMs,phase))return;
            var idToEat=foodId;var who=foodUid;
#if UNITY_EDITOR
            if(UseFoodGate && !foodGate.TryRequest(who,idToEat,r.habitat,NowMs,drop.ExpiresAtMs,
                FoodPerceptionEligible(r),Vector3.Distance(r.position,foodPoint),
                Segment(r.position,foodPoint,r.habitat),FoodFacing(r),FoodMotionReady(r)))return;
#endif
#if UNITY_EDITOR
            bool usedGate=UseFoodGate;
#endif
            bool accepted=Send(id=>new ConsumeFoodCommand(id,idToEat,who));
#if UNITY_EDITOR
            if(usedGate)
            {
                // Accepted FoodConsumed can synchronously clear the marker and gate during Send.
                foodGate.Resolve(accepted);
                lastFoodGateOutcome=accepted?"accepted":"rejected";
                Debug.Log("TL.11 food request "+lastFoodGateOutcome);
            }
#endif
            // Successful consumption feedback comes only from the canonical event.
            if(!accepted)consumeAfter=Time.unscaledTime+1f; // original retry only; experimental gate permits one request per marker.
        }
        void ClearFood()
        {
#if UNITY_EDITOR
            foodGate.Clear();foodGateForMarker=false;
#endif
            placingMeal=false;portions.Clear();mealSpots.Clear();foodUid=null;if(fruit!=null)fruit.SetActive(false);decisionsDirty=true;RefreshButtons();
        }
        void RemovePortion(string id)
        {
            if(!portions.Remove(id))return;
            mealSpots.Remove(id);
            decisionsDirty=true;
            if(portions.Count==0)ClearFood();
        }
        Vector3 FindMealSpot(int slot,int count,string habitat)
        {
            if(count<=1)return foodPoint;
            var spot=foodPoint;
            AutonomousPresentationPolicy.TryMealRingOffset(slot,count,(x,z)=>
            {
                var candidate=foodPoint+new Vector3(x,0,z);
                if(locations==null || !locations.TryGroundHeight(candidate,out float groundY))return false;
                candidate.y=groundY;
                if(!Segment(foodPoint,candidate,habitat))return false;
                foreach(var used in mealSpots.Values)
                {
                    float dx=candidate.x-used.x,dz=candidate.z-used.z;
                    if(dx*dx+dz*dz<.55f*.55f)return false;
                }
                spot=candidate;
                return true;
            });
            // Keep the placeable center as a last resort in very narrow spaces.
            return spot;
        }
        void OnEvents(IReadOnlyList<DomainEvent> events)
        {
            decisionsDirty=true;
            if(viewSuspended)return;
            int spawned=feedPending?events.Count(e=>e is FoodSpawned):0;
            foreach(var e in events)
            {
                if(e is FoodSpawned f && feedPending)
                {
                    mealSpots[f.FoodId]=FindMealSpot(portions.Count,spawned,f.HabitatId);
                    portions.Add(f.FoodId);consumeAfter=Time.unscaledTime+.6f;
#if UNITY_EDITOR
                    foodGateForMarker=spawned==1 && experimentalFoodPerception && !sharedFoodAllocation;
                    if(foodGateForMarker)lastFoodGateOutcome="pending";
#endif
                    RefreshButtons();
                    if(fruit!=null){fruit.transform.position=foodPoint+Vector3.up*.12f;fruit.SetActive(true);}
                }
                else if(e is FoodConsumed consumed)
                {
                    if(portions.Contains(consumed.FoodId))
                    {
                        var eater=State?.FindDragon(consumed.DragonUid);
                        RemovePortion(consumed.FoodId);
                        Message(ResearchDiscoveryView.FoodFeedback(eater?.SpeciesId,consumed.DragonUid,consumed.XpGainedSec,false),
                            ResearchDiscoveryView.FoodFeedback(eater?.SpeciesId,consumed.DragonUid,consumed.XpGainedSec,true));
                    }
                } // Eat intent is derived from the accepted Domain event's timestamp.
                else if(e is DragonPetted p && residents.TryGetValue(p.DragonUid,out var r)&&!r.Spatial)
                {
                    if(Social!=null&&Social.Contains(p.DragonUid))StopSocial("pet-event");
                    sync.React(p.DragonUid,DragonReaction.Petted);
                }
                else if(e is ExpeditionStarted trip)
                {
                    if(Social!=null&&Social.Contains(trip.DragonUid))StopSocial("expedition-start");
                    if(selected==trip.DragonUid)StopFollow();
                }
                else if(e is FoodExpired expired && portions.Contains(expired.FoodId))
                {RemovePortion(expired.FoodId);if(foodId==null)Message("Food expired before consumption.","Thức ăn đã hết hạn trước khi rồng ăn.");}
            }
        }
        void ToggleSleep()
        {
            if(Ready && selected!=null && residents.TryGetValue(selected,out var r) && r.canSleep &&
                r.decision?.Role==BehaviorRole.Presentation && !IsFoodTarget(r))
            {
                if(Social!=null && Social.Contains(r.uid))StopSocial("player-doze");
                r.manualRestUntil=r.manualRestUntil>NowMs?0:NowMs+15000;
                ReleasePoint(r);
                Message(r.manualRestUntil>0?"Doze request (visual)":"Automatic behavior",r.manualRestUntil>0?"Nghỉ ngắn (hoạt ảnh)":"Tự sinh hoạt");
            }
        }
        public List<ObservationOpportunity> VisibleResearchOpportunities()
        {
            if(!Ready)return new List<ObservationOpportunity>();
            return ResearchDiscoveryView.LocalOpportunities(State,NowMs,CurrentPhase(),ViewZone,
                uid=>residents.ContainsKey(uid)&&sync!=null&&sync.IsShown(uid)&&manager.IsLive(uid));
        }
        public bool TryRecordVisibleResearch(string uid)
        {
            if(!Ready||string.IsNullOrEmpty(uid))return false;
            var opportunity=VisibleResearchOpportunities().FirstOrDefault(o=>o.Uid==uid);
            if(!ResearchDiscoveryView.CanRecord(opportunity))
            {
                Message(ResearchDiscoveryView.ObservationFeedback(opportunity,false),
                    ResearchDiscoveryView.ObservationFeedback(opportunity,true));
                return false;
            }
            bool recorded=false;
            void Capture(IReadOnlyList<DomainEvent> events){recorded|=events.Any(e=>e is DragonObserved);}
            var current=session;
            current.DomainEvents+=Capture;
            bool accepted;
            try{accepted=Send(id=>new ObserveDragonCommand(id,uid,CurrentPhase()));}
            finally{current.DomainEvents-=Capture;}
            if(accepted&&recorded)Message("Observation recorded.","Đã ghi quan sát nghiên cứu.");
            else if(accepted)Message("Observation not recorded; refresh and try again.","Chưa ghi quan sát; kiểm tra và thử lại.");
            return accepted&&recorded;
        }
        void ResearchObserve(){if(Selected!=null)TryRecordVisibleResearch(Selected.Uid);}
        void Observe()
        {
            var d=Selected;if(d==null || !residents.ContainsKey(d.Uid))return;
            if(Send(id=>new ViewBiologyProfileCommand(id,d.SpeciesId)))Message("Profile recorded.","Đã ghi nhận hồ sơ loài.");
        }
        List<TravelOption> TravelOptions()=>State==null || Selected==null?new List<TravelOption>():
            Ecosystem.TravelOptions(State,selected).Where(o=>connectedWorlds?ConnectedWorldPolicy.ScenePath(o.ZoneId)!=null:o.ZoneId=="origin").ToList();
        void NextTravel()
        {
            if(!Ready)return;
            var options=TravelOptions();if(options.Count==0)return;
            var next=options[(options.FindIndex(o=>ConnectedWorldPolicy.MatchesDestination(o,travelZone,travelHabitat))+1)%options.Count];
            travelZone=next.ZoneId;travelHabitat=next.HabitatId;RefreshProfile();
        }
        void Move()
        {
            if(!Ready || Selected==null || travelHabitat==null)return;
            string who=selected,habitat=travelHabitat,zone=travelZone;
            if(zone==null || !TravelOptions().Any(o=>ConnectedWorldPolicy.MatchesDestination(o,zone,habitat)&&o.Blocked==null))return;
            if(Send(id=>new MoveDragonCommand(id,who,MoveTarget.Zone(zone,habitat))))Message("Moved to habitat: "+zone,"Đã chuyển sinh cảnh: "+zone);
        }
        void Store()
        {
            if(Selected==null)return;string who=selected;
            if(Send(id=>new MoveDragonCommand(id,who,MoveTarget.Storage)))Message("Moved to storage.","Đã chuyển vào kho.");
        }
        /// <summary>U6.3 island HUD: each drawn resident with a point just above its visual.</summary>
        public void ResidentAnchors(List<KeyValuePair<string,Vector3>> into)
        {
            into.Clear();
            // Not Ready: bubbles must also show while the window is unfocused or the session paused.
            if(!active || viewSuspended || manager==null)return;
            foreach(var r in residents.Values)
            {
                if(!manager.TryGet(r.uid,out var visual))continue;
                var b=visual.WorldBounds();
                var top=new Vector3(b.center.x,b.max.y+.12f,b.center.z);
                if(float.IsNaN(top.x+top.y+top.z) || float.IsInfinity(top.x+top.y+top.z))continue;
                into.Add(new KeyValuePair<string,Vector3>(r.uid,top));
            }
        }
        /// <summary>U6.3: select a resident in view (same as tapping it). False when it is not in view.</summary>
        public bool SelectResident(string uid)
        {
            if(!Ready || string.IsNullOrEmpty(uid) || !residents.ContainsKey(uid))return false;
            Select(uid);return true;
        }
        public string SelectedUid=>selected;
        /// <summary>U6.4 habitat plots: location data of the world in view (null before Configure).</summary>
        public SanctuaryLocationData Locations=>locations;
        public bool ViewActive=>active && !viewSuspended;
        /// <summary>U6.2 journey banner: select a resident in view and frame it (same as Inspect). False when it is not in view.</summary>
        public bool FocusDragon(string uid)
        {
            if(!Ready || string.IsNullOrEmpty(uid) || !residents.ContainsKey(uid))return false;
            Select(uid);
            if(!following)Follow();
            return following;
        }
        void Follow()
        {
            if(!Ready || selected==null || !residents.ContainsKey(selected) || cameraView==null || !cameraView.orthographic)return;
            if(following){StopFollow();return;}
            savedCamera=cameraView.transform.position;savedSize=cameraView.orthographicSize;
            followZoom=1f;
            pinchDistance=0f;pinchA=pinchB=-1;
            var inset=UnityEngine.Object.FindAnyObjectByType<SanctuaryUiInsetSource>(FindObjectsInactive.Include);
            focusPanel=inset!=null?inset.GetComponent<RectTransform>():null;
            restoreFit=cameraFit!=null && cameraFit.enabled;
            if(restoreFit)cameraFit.enabled=false;
            following=true;
        }
        void Resume(){StopFollow();if(selected!=null && residents.TryGetValue(selected,out var r))r.manualRestUntil=0;}
        void LateUpdate()
        {
            if(!following || cameraView==null)return;
            if(!Ready || selected==null || !residents.TryGetValue(selected,out var r)){StopFollow();return;}
            var t=cameraView.transform;
            var free=FollowViewport();
            // Fit the live visual, not its collision radius or canonical size. Retain
            // the established overview as the upper bound, and keep the UI area free.
            float size=Mathf.Min(savedSize,1.8f);
            var subject=r.position+Vector3.up*.45f;
            if(manager.TryGet(selected,out var visual))
            {
                var bounds=visual.WorldBounds();
                var e=bounds.extents;
                float width=ProjectedExtent(e,t.right),height=ProjectedExtent(e,t.up);
                if(width>0f && height>0f && !float.IsInfinity(width+height))
                {
                    subject=bounds.center;
                    // At default zoom the projected bounds occupy at most 40% of
                    // the free width/height, unless limited by the overview clamp.
                    size=Mathf.Max(width/Mathf.Max(.05f,free.width*cameraView.aspect*.4f),
                        height/Mathf.Max(.05f,free.height*.4f));
                }
            }
            ReadFollowZoom(free);
            size=Mathf.Clamp(size*followZoom,Mathf.Min(.55f,savedSize),savedSize);
            float depth=Vector3.Dot(subject-savedCamera,t.forward);
            var target=subject-t.forward*depth-t.right*((free.center.x-.5f)*2f*size*cameraView.aspect)
                -t.up*((free.center.y-.5f)*2f*size);
            float blend=1f-Mathf.Exp(-8f*Time.unscaledDeltaTime);
            t.position=Vector3.Lerp(t.position,target,blend);
            cameraView.orthographicSize=Mathf.Lerp(cameraView.orthographicSize,size,blend);
        }
        static float ProjectedExtent(Vector3 e,Vector3 axis)=>
            Mathf.Abs(axis.x)*e.x+Mathf.Abs(axis.y)*e.y+Mathf.Abs(axis.z)*e.z;
        Rect FollowViewport()
        {
            if(cameraFit==null)return new Rect(0,0,1,1);
            if(focusPanel==null)return cameraFit.FreeViewport;
            float w=Mathf.Max(1,Screen.width),h=Mathf.Max(1,Screen.height);
            var safe=cameraFit.useSafeArea?Screen.safeArea:new Rect(0,0,w,h);
            var canvas=focusPanel.GetComponentInParent<Canvas>();
            var uiCam=canvas!=null && canvas.rootCanvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.rootCanvas.worldCamera:null;
            float pixels=focusPanel.gameObject.activeInHierarchy?
                SanctuaryCameraFit.MeasureOccluderPixels(focusPanel,uiCam,w):0f;
            var ins=cameraFit.ResolveInsets(w,h,safe,pixels);
            float aspect=Mathf.Max(.1f,cameraView.aspect);
            float x0=ins.x/aspect,y0=ins.w;
            return Rect.MinMaxRect(x0,y0,Mathf.Max(x0,1f-ins.y/aspect),Mathf.Max(y0,1f-ins.z));
        }
        bool WorldPoint(Vector2 point,Rect free)
        {
            var v=cameraView.ScreenToViewportPoint(point);
            if(!free.Contains(new Vector2(v.x,v.y)))return false;
            if(EventSystem.current==null)return true;
            uiHits.Clear();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},uiHits);
            return !uiHits.Any(hit=>hit.module is GraphicRaycaster);
        }
        void ReadFollowZoom(Rect free)
        {
            var touch=Touchscreen.current;
            if(touch!=null)
            {
                int count=0,a=-1,b=-1;
                Vector2 p=default,q=default;
                foreach(var finger in touch.touches)
                {
                    if(!finger.press.isPressed)continue;
                    if(count==0){a=finger.touchId.ReadValue();p=finger.position.ReadValue();}
                    else if(count==1){b=finger.touchId.ReadValue();q=finger.position.ReadValue();}
                    count++;
                }
                if(count==2 && WorldPoint(p,free) && WorldPoint(q,free))
                {
                    float distance=Vector2.Distance(p,q);
                    if(distance>8f && pinchDistance>8f && a==pinchA && b==pinchB)
                        followZoom=Mathf.Clamp(followZoom*Mathf.Clamp(pinchDistance/distance,.8f,1.25f),.65f,2.2f);
                    pinchDistance=distance;pinchA=a;pinchB=b;
                    return;
                }
                pinchDistance=0f;pinchA=pinchB=-1;
                if(count>0)return;
            }
            var mouse=Mouse.current;
            if(mouse==null)return;
            float scroll=mouse.scroll.ReadValue().y;
            if(Mathf.Approximately(scroll,0f))return;
            Vector2 point=mouse.position.ReadValue();
            if(!WorldPoint(point,free))return;
            float steps=Mathf.Clamp(scroll/120f,-3f,3f);
            followZoom=Mathf.Clamp(followZoom*Mathf.Exp(-steps*.22f),.65f,2.2f);
        }
        void StopFollow()
        {
            if(!following)return;
            following=false;
            if(cameraView!=null){cameraView.transform.position=savedCamera;cameraView.orthographicSize=savedSize;}
            if(restoreFit && cameraFit!=null)cameraFit.enabled=true;
            restoreFit=false;focusPanel=null;pinchDistance=0f;pinchA=pinchB=-1;
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
            title.text=(string.IsNullOrEmpty(d.Nickname)?d.SpeciesId:d.Nickname)+" · "+ResearchDiscoveryView.StageLabel(snapshot.Stage,vi);
            progress.text="XP "+d.XpSec.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+"/"+SpeciesCatalog.Get(d.SpeciesId).GrowthTimeSec+
                " · "+(vi?"Gắn kết ":"Bond ")+d.Bond+"/100";
            if(foodId==null && !sharedFoodAllocation && Ready && residents.TryGetValue(d.Uid,out var hungryResident) && !hungryResident.Spatial)
            {
                int wait=SanctuaryFeedback.FeedWaitSeconds(d.LastFeedXpAtMs,NowMs);
                if(wait>0)progress.text+=vi?$" · Vừa ăn, còn {wait} giây":$" · Just ate, {wait}s left";
            }
            string visual=vi?"Không ở sinh cảnh hiện tại":"Not in current habitat view";
            if(residents.TryGetValue(d.Uid,out var r))
            {
                var entry=manager.catalog.Resolve(r.species,r.stage,out var match);
                visual=(manager.IsLive(d.Uid)?(vi?"Model: ":"Model: "):(vi?"Chưa spawn: ":"Not spawned: "))+match+
                    (entry==null?"":" · "+entry.stage+" · "+(entry.approved?"approved":"unapproved"));
            }
            else if(unavailable.TryGetValue(d.Uid,out string why))visual=why switch{
                "missing-model"=>vi?"Thiếu model đúng loài":"Missing species model",
                "spatial-clip-fault"=>"Spatial review: imported clip mismatch; revisit to retry",
                "spatial-metadata-missing"=>"Spatial review: missing exact/unstaged PASS model or motion metadata",
                "spatial-volume-missing"=>"Spatial review: no authored volume",
                "spatial-no-safe-spawn"=>"Spatial review: no safe 3D spawn",
                "ground-only"=>vi?"Chưa hỗ trợ bơi/bay trong game":"Live swim/fly not implemented",
                _=>vi?"Không có điểm đặt an toàn":"No safe spawn"};
            profile.text=title.text+"\n"+snapshot.Mutation+" · "+d.Personality+"\n"+
                ResearchDiscoveryView.StatusLabel(snapshot.Status,vi)+" · "+
                (snapshot.HabitatId!=null?ResearchDiscoveryView.HabitatName(snapshot.HabitatId,vi):ResearchDiscoveryView.ZoneName(snapshot.ZoneId,vi)??"—")+
                "\n"+(vi?"Phù hợp sinh cảnh: ":"Habitat suitability: ")+(snapshot.Suitability?.ToString()??"—");
            if(ProfileDiagnostics)
                profile.text+="\n"+(vi?"Thế giới đang xem: ":"Viewing: ")+ViewZone+"\nUID: "+d.Uid+"\n"+visual+
                    "\n"+(vi?"Hiển thị: ":"Visual residents: ")+residents.Count;
            else if(snapshot.Status==DragonStatus.InHabitat&&d.Location.ZoneId==ViewZone&&!manager.IsLive(d.Uid))
                profile.text+="\n"+(vi?"Chưa thể hiển thị cá thể này trong thế giới.":"This dragon cannot be displayed in the world yet.");
            if(d.Location.Kind==LocationKind.Zone && d.Location.ZoneId!=ViewZone)
                profile.text+="\n"+(vi?"Đang ở thế giới khác; đổi vùng xem để tương tác.":"In another world; visit that world to interact.");
            var options=TravelOptions();
            if(!options.Any(o=>ConnectedWorldPolicy.MatchesDestination(o,travelZone,travelHabitat)))
            {var first=options.FirstOrDefault();travelZone=first?.ZoneId;travelHabitat=first?.HabitatId;}
            var option=options.FirstOrDefault(o=>ConnectedWorldPolicy.MatchesDestination(o,travelZone,travelHabitat));
            travel.text=option==null?"—":ResearchDiscoveryView.ZoneName(option.ZoneId,vi)+" / "+ResearchDiscoveryView.HabitatName(option.HabitatId,vi)+" · "+option.Score+"/100\n"+
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
                profile.text+="\n"+(vi?"Hành vi: ":"Behavior: ")+BehaviorLabel(r.decision.Behavior);
                if(ProfileDiagnostics)profile.text+=" · "+r.decision.Reason+
                    "\n"+(vi?"Trình diễn: ":"Presentation: ")+r.shownIntent+(r.fallback==null?"":" ("+r.fallback+")")+
                    "\n"+(vi?"Điểm tương tác: ":"Interaction point: ")+(r.point?.id??"—")+
                    "\n"+(vi?"Đường đi: ":"Route: ")+r.routeState+" · "+(r.route==null?0:Math.Max(0,r.route.Waypoints.Count-r.routeIndex))+" wp";
            }
            else status.text=vi?messageVi:messageEn;
            AppendProgression(d);
            if(r!=null && r.Spatial)
                profile.text+="\n"+(vi?"Thử nghiệm cư dân 3D: ":"Spatial resident trial: ")+r.medium+" · "+r.routeState+
                    "\n"+(vi?"Chăm sóc trong nước/không trung chưa hỗ trợ.":"Water/air care not implemented.");
            else if(ProfileDiagnostics && r!=null && sync.IsShown(r.uid))
            {
                var st=sync.MotionStatusOf(r.uid);var caps=sync.CapabilitiesOf(r.uid);
                profile.text+="\n"+(vi?"Rig / môi trường: ":"Rig / medium: ")+st.mode+" · "+st.transition+
                    "\n"+(vi?"Khả năng model: ":"Model capabilities: ")+
                    (caps.ground?"Ground ":"")+(caps.water?"Water ":"")+(caps.air?"Air ":"")+(caps.perch?"Perch ":"")+(caps.underground?"Burrow":"")+
                    "\n"+(vi?"Được dịch chuyển: ":"Motion unlocked: ")+GroundMotionReady(r);
            }
            if(ProfileDiagnostics)profile.text+="\n"+(vi?"Thay clip (khung trước): ":"Clip substitutions (last frame): ")+substitutions+
                "\n"+(vi?"Lượt tìm đường / lỗi: ":"Routes planned / failed: ")+plannedRoutes+"/"+failedRoutes;
#if UNITY_EDITOR
            if(experimentalFoodPerception || UseFoodGate)profile.text+="\nTL.11: "+(UseFoodGate?foodGate.Phase.ToString():"next marker")+" · "+(foodGate.Visible?"visible":foodGate.Remembered?"memory":"not sensed")+" · requests "+foodGate.Requests+" · last "+lastFoodGateOutcome;
#endif
            if(notice!=null)notice.text=FeedingNotice()+(sharedFoodAllocation?(vi?" · Phân thức ăn thử nghiệm":" · Experimental food allocation"):"");
            RefreshButtons();
        }
        void AppendProgression(DragonState d)
        {
            if(!ReferenceEquals(progressionState,State) || progression==null)
            {
                progressionState=State;progression=ProgressionQueries.Overview(State);
                researchProfiles.Clear();relationUid=null;
            }
            if(!researchProfiles.TryGetValue(d.SpeciesId,out var research))
            {research=ProgressionQueries.ResearchProfile(State,d.SpeciesId);researchProfiles[d.SpeciesId]=research;}
            var social=ProgressionQueries.SocialProfile(State,d.Uid);
            var rule=progression.Rules;
            if(relationUid!=d.Uid)
            {
                var relations=ProgressionQueries.Relations(State,d.Uid);
                string Partner(string uid)
                {
                    var other=State.FindDragon(uid);
                    if(other==null)return uid;
                    return (string.IsNullOrEmpty(other.Nickname)?other.SpeciesId:other.Nickname)+
                        " #"+uid.Substring(0,Math.Min(6,uid.Length));
                }
                relationEn=ResearchDiscoveryView.RelationSummary(relations,Partner,false);
                relationVi=ResearchDiscoveryView.RelationSummary(relations,Partner,true);
                relationUid=d.Uid;
            }
            profile.text+="\n"+(vi?"Nghiên cứu: ":"Research: ")+research.Species.Tier+
                " · "+(rule.ResearchRecordingEnabled?(vi?"đang ghi nhận":"recording active"):(vi?"chưa ghi nhận":"recording off"))+
                " · "+(rule.NightJournalEnabled?(vi?"nhật ký đêm bật":"night journal on"):"")+
                "\n"+(vi?"Codex: ":"Codex: ")+progression.CodexCells+"/"+progression.CodexCellsTotal+
                " · "+(vi?"Sinh cảnh: ":"Habitats: ")+progression.UnlockedHabitats+"/"+progression.TotalHabitats+
                "\n"+(vi?"Thế giới mở: ":"Unlocked realms: ")+string.Join(", ",progression.UnlockedZones);
            if(social!=null)
                profile.text+="\n"+(vi?"Xã hội (đề xuất): ":"Social (proposal): ")+social.Style+" · "+social.Sociability+"/100 · "+
                    (social.PersonalSpaceDm/10f).ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+" m";
            profile.text+="\n"+(vi?relationVi:relationEn)+"\n"+
                (rule.SocialGameplayEffectsEnabled?(vi?"Hiệu ứng xã hội: bật":"Social gameplay effects: on"):
                    (vi?"Không cộng vàng, XP hay chỉ số từ quan hệ.":"Relationships grant no gold, XP or stat bonus."));
            var candidate=socialCandidates.FirstOrDefault(c=>(c.InitiatorUid==d.Uid||c.PartnerUid==d.Uid) &&
                c.UntilMs>NowMs && !c.AffectsGameplay && c.Role==BehaviorRole.Presentation);
            if(SocialEnabled)profile.text+="\n"+(vi?"Thử nghiệm xã hội: ":"Social preview: ")+
                (Social!=null&&Social.Contains(d.Uid)?Social.Kind+" / "+Social.Phase:socialLast);
            if(candidate!=null)
            {
                string partner=candidate.InitiatorUid==d.Uid?candidate.PartnerUid:candidate.InitiatorUid;
                // Candidate remains a read-only hint; opt-in scheduler may render it but never sends a command.
                profile.text+="\n"+(vi?"Gợi ý xã hội: ":"Social candidate: ")+candidate.Kind+
                    (string.IsNullOrEmpty(partner)?"":" · "+(State.FindDragon(partner)?.SpeciesId??"—"));
            }
            profile.text+="\n"+(rule.QuestCompletionEnabled?(vi?"Nhiệm vụ đã hoàn thành: ":"Quests completed: "):
                (vi?"Nhiệm vụ (chưa bật nhận thưởng): ":"Quests (completion disabled): "))+
                progression.QuestsCompleted+" / "+(progression.QuestsCompleted+progression.QuestsReady+progression.QuestsInProgress+progression.QuestsLocked)+
                "\n"+(vi?"Xem mục Sổ để theo dõi hành trình và nhận nhiệm vụ.":"Open Log for journey guidance and quest claims.");
            if(ProfileDiagnostics)profile.text+="\n"+(vi?"Mục tiêu gợi ý: ":"Suggested goals: ")+string.Join(", ",progression.SuggestedGoals)+
                "\n"+(vi?"Luật xã hội mới / nhiệm vụ: ":"New social / quest rules: ")+rule.SocialGameplayEffectsEnabled+" / "+rule.QuestCompletionEnabled;
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
            bool ground=visible&&!residents[selected].Spatial;
            bool ready=Ready;
            feed.interactable=ready && ground && foodId==null;
            pet.interactable=ready && ground;
            inspect.interactable=rosterInspect.interactable=observe.interactable=ready && visible;
            if(researchObserve!=null)researchObserve.interactable=ready && visible;
            sleep.interactable=ready && visible && residents[selected].canSleep && residents[selected].decision?.Role==BehaviorRole.Presentation && !IsFoodTarget(residents[selected]);
            resume.interactable=ready && (visible || following);
            nextDragon.interactable=ready && State.Dragons.Count(x=>x.Location.Kind!=LocationKind.Released)>1;
            // Eligibility can change on each Domain revision; never calculate gameplay rules here.
            if(!ready || d==null){nextTravel.interactable=move.interactable=store.interactable=false;return;}
            var options=TravelOptions();
            nextTravel.interactable=options.Count>1;
            move.interactable=options.Any(o=>ConnectedWorldPolicy.MatchesDestination(o,travelZone,travelHabitat) && o.Blocked==null);
            store.interactable=Ecosystem.Actions(State,d.Uid,State.Clock.LastProcessedAtMs).MoveToStorage==null;
        }
    }
}
