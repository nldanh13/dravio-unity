using System;
using System.Collections.Generic;
using System.Linq;
using Dravio.Character;
using Dravio.Domain;
using Dravio.Domain.Catalog;
using Dravio.EnvironmentArt;
using Dravio.Navigation;
using Dravio.Platform.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dravio.EditorTools
{
    /// <summary>Isolated, one-model Play Mode review inside an existing environment-only zone.
    /// Spawns directly inside the selected medium for inspection; never simulates entry/exit or a real resident.</summary>
    public sealed class SpatialMotionReviewWindow:EditorWindow
    {
        const string CatalogPath="Assets/Dravio/Character/Data/DragonVisualCatalog.asset";
        const string OceanScene="Assets/Dravio/Environment/Scenes/Ocean_Environment.unity";
        const string SkyScene="Assets/Dravio/Environment/Scenes/Sky_Environment.unity";
        const string ReviewUid="spatial-motion-review-only";
        DragonVisualCatalog catalog;
        DragonVisualManager manager;
        GameObject reviewRoot;
        SanctuaryLocationData locations;
        SanctuaryNavigationSurface surface;
        DragonVisualCatalog.Entry entry;
        SanctuaryLocationData.SpawnPoint start;
        MediumReviewEnvelope envelope;
        RouteResult route;
        NavigationMedium medium;
        DragonLocomotion locomotion;
        int entryIndex,habitatIndex,startIndex,goalIndex=1,waypoint,lastFrame=-1;
        SceneHandle sceneHandle;
        bool running,consent;
        float speed,turnLimit,travelled;
        Vector3 position;
        Quaternion rotation;
        string message="Open an environment-only Ocean or Sky scene, enter Play Mode, then choose a model.";
        string zoneScene;
        [MenuItem("Dravio/Technology Lab/Spatial Motion Review")]
        static void Open()=>GetWindow<SpatialMotionReviewWindow>("Spatial Motion Review");
        void OnEnable(){catalog=AssetDatabase.LoadAssetAtPath<DragonVisualCatalog>(CatalogPath);EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=OnPlayMode;}
        void OnDisable(){EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=OnPlayMode;Stop("Review closed.");}
        void OnPlayMode(PlayModeStateChange s){if(s==PlayModeStateChange.ExitingPlayMode)Stop("Play Mode ended.");}
        bool EnvironmentOnly(out SanctuaryLocationData data,out NavigationMedium m)
        {
            data=null;m=NavigationMedium.Ground;
            if(!Application.isPlaying || SceneManager.sceneCount!=1 || UnityEngine.Object.FindAnyObjectByType<DravioSessionHost>()!=null)return false;
            var scene=SceneManager.GetActiveScene();
            if(scene.path==OceanScene)m=NavigationMedium.Swim;
            else if(scene.path==SkyScene)m=NavigationMedium.Air;
            else return false;
            var living=LivingEnvironment.Active;
            data=living==null?null:living.locations;
            return data!=null && data.zoneId==(m==NavigationMedium.Swim?"ocean":"sky");
        }
        void OnGUI()
        {
            EditorGUILayout.HelpBox("Single model in an existing authored volume. Direct visual placement for QA only; no takeoff, landing, water entry, session, unlock, or save. Centered envelope is a geometry approximation, not certified swept mesh clearance.",MessageType.Info);
            if(running)
            {
                EditorGUILayout.LabelField("Path progress",waypoint+"/"+route.Waypoints.Count);
                EditorGUILayout.LabelField("Distance",travelled.ToString("F2")+" m");
                if(GUILayout.Button("Stop / remove review model"))Stop("Stopped by reviewer.");
                EditorGUILayout.HelpBox(message,MessageType.None);return;
            }
            if(!EnvironmentOnly(out var data,out var mode))
            {EditorGUILayout.HelpBox("Requires Play Mode in Ocean_Environment.unity or Sky_Environment.unity, one scene and no game session.",MessageType.Warning);EditorGUILayout.LabelField(message,EditorStyles.wordWrappedLabel);return;}
            catalog=(DragonVisualCatalog)EditorGUILayout.ObjectField("Catalog",catalog,typeof(DragonVisualCatalog),false);
            if(catalog==null || catalog.entries==null || catalog.entries.Count==0)
            {EditorGUILayout.HelpBox("Import Rig v5 catalog first.",MessageType.Warning);return;}
            string clip=mode==NavigationMedium.Swim?"Swim":"Fly";
            var models=catalog.entries.Where(e=>e!=null&&e.prefab!=null&&e.rigged&&e.clips!=null&&e.clips.Contains(clip)).ToArray();
            if(models.Length==0){EditorGUILayout.HelpBox("No model with "+clip+" clip.",MessageType.Warning);return;}
            entryIndex=Mathf.Clamp(entryIndex,0,models.Length-1);
            entryIndex=EditorGUILayout.Popup("Review model",entryIndex,models.Select(e=>e.speciesId+" / "+e.stage).ToArray());
            var model=models[entryIndex];
            var species=SpeciesCatalog.All.FirstOrDefault(e=>e.Id==model.speciesId);
            if(species==null || !species.Biology.Locomotion.Contains(mode==NavigationMedium.Swim?Locomotion.Swim:Locomotion.Fly))
            {EditorGUILayout.HelpBox("Canonical species does not support this medium.",MessageType.Warning);return;}
            var habitats=data.habitats.Where(h=>h!=null&&species.HabitatRules.Contains(h.id)).ToArray();
            if(habitats.Length==0){EditorGUILayout.HelpBox("No canonical compatible habitat in this zone.",MessageType.Warning);return;}
            habitatIndex=Mathf.Clamp(habitatIndex,0,habitats.Length-1);
            habitatIndex=EditorGUILayout.Popup("Habitat",habitatIndex,habitats.Select(h=>h.id).ToArray());
            var hId=habitats[habitatIndex].id;
            if(!TryEnvelope(model,mode,out var bounds,out float authoredSpeed,out float yawRate))
            {EditorGUILayout.HelpBox("Missing/invalid Rig v5 motion envelope, explicit speed or turn rate. No guessed values.",MessageType.Warning);return;}
            var nav=new SanctuaryNavigationSurface(data);
            var spawns=data.SpawnPointsIn(hId).Where(p=>p!=null&&p.medium==(mode==NavigationMedium.Swim?"swim":"air")).ToArray();
            var valid=spawns.Where(p=>nav.ClearReviewVolume(p.Position,hId,mode,bounds)).ToArray();
            EditorGUILayout.LabelField("Authored spawns passing envelope",valid.Length+"/"+spawns.Length);
            EditorGUILayout.LabelField("Horizontal / vertical margin",bounds.Horizontal.ToString("F2")+" / "+bounds.Vertical.ToString("F2")+" m");
            EditorGUILayout.LabelField("Authored speed / yaw rate",authoredSpeed.ToString("F2")+" m/s / "+yawRate.ToString("F1")+" deg/s");
            EditorGUILayout.LabelField("Asset QA / approval",(model.animQaStatus??"unknown")+" / "+model.approved);
            if(valid.Length<2)
            {EditorGUILayout.HelpBox("Not enough safe authored endpoints. Do not shrink the envelope to force a route.",MessageType.Warning);return;}
            startIndex=Mathf.Clamp(startIndex,0,valid.Length-1);goalIndex=Mathf.Clamp(goalIndex,0,valid.Length-1);
            startIndex=EditorGUILayout.Popup("Start",startIndex,valid.Select(p=>p.id).ToArray());
            goalIndex=EditorGUILayout.Popup("Destination",goalIndex,valid.Select(p=>p.id).ToArray());
            consent=EditorGUILayout.ToggleLeft("I understand this is a temporary visual QA trial, not an approved in-game transition.",consent);
            using(new EditorGUI.DisabledScope(!consent||startIndex==goalIndex))
                if(GUILayout.Button("Run route review"))
                    Begin(data,mode,model,valid[startIndex],valid[goalIndex],bounds,authoredSpeed,yawRate);
            EditorGUILayout.LabelField(message,EditorStyles.wordWrappedLabel);
        }
        static bool Positive(float x)=>x>0 && !float.IsNaN(x)&&!float.IsInfinity(x);
        static bool TryEnvelope(DragonVisualCatalog.Entry e,NavigationMedium mode,out MediumReviewEnvelope bounds,out float speed,out float turn)
        {
            bounds=default;speed=turn=0;
            var m=e.motion;if(m==null)return false;
            string clip=mode==NavigationMedium.Swim?"Swim":"Fly";
            var speeds=e.clipSpeeds==null?Array.Empty<DragonVisualCatalog.ClipSpeed>():e.clipSpeeds;
            var explicitSpeed=speeds.FirstOrDefault(x=>x!=null&&x.clip==clip&&Positive(x.groundSpeed));
            if(explicitSpeed==null)return false;
            speed=explicitSpeed.groundSpeed;turn=mode==NavigationMedium.Swim?m.maxTurnWater:m.maxTurnAir;
            return Positive(turn)&&MediumReviewEnvelope.TryFromDimensions(m.bodyLength,m.bodyWidth,m.bodyHeight,
                mode==NavigationMedium.Swim?m.swimWidth:m.flightSpan,mode==NavigationMedium.Swim?m.swimVerticalExtent:m.flightVerticalExtent,out bounds);
        }
        void Begin(SanctuaryLocationData data,NavigationMedium mode,DragonVisualCatalog.Entry model,
            SanctuaryLocationData.SpawnPoint a,SanctuaryLocationData.SpawnPoint z,MediumReviewEnvelope bounds,float authoredSpeed,float yawRate)
        {
            if(!EnvironmentOnly(out var current,out var actual)||current!=data||actual!=mode||
                UnityEngine.Object.FindAnyObjectByType<DragonVisualManager>()!=null)
            {message="A visual manager already exists or environment changed. No review spawned.";return;}
            var resolved=catalog.Resolve(model.speciesId,model.stage,out var match);
            if(resolved!=model || match==DragonVisualCatalog.Match.OtherStage || model.animQaStatus!="PASS" ||
                (model.prefab.transform.localScale-Vector3.one).sqrMagnitude>.000001f)
            {message="Exact selected model, PASS QA and unit prefab root scale required for this review.";return;}
            locations=data;medium=mode;entry=model;start=a;envelope=bounds;surface=new SanctuaryNavigationSurface(data);
            speed=authoredSpeed;turnLimit=yawRate;
            bool Clear(NavPoint p)=>surface.ClearReviewVolume(SanctuaryNavigationSurface.Vector(p),a.habitatId,mode,bounds);
            route=SpatialMediumReview.Plan(SanctuaryNavigationSurface.Point(a.Position),SanctuaryNavigationSurface.Point(z.Position),mode,Clear);
            if(!route.Success){message="Route rejected: "+route.Status+" ("+route.Expanded+" nodes).";return;}
            position=a.Position;rotation=a.Rotation;waypoint=0;travelled=0;lastFrame=-1;
            locomotion=mode==NavigationMedium.Swim?DragonLocomotion.Swim:DragonLocomotion.Fly;
            reviewRoot=new GameObject("TEMP - Spatial Motion Review (no save)");
            reviewRoot.hideFlags=HideFlags.DontSave;
            manager=reviewRoot.AddComponent<DragonVisualManager>();manager.catalog=catalog;manager.viewCamera=Camera.main;manager.footIk=false;
            var live=manager.Spawn(new DragonVisualSpawn{uid=ReviewUid,speciesId=entry.speciesId,stage=entry.stage,
                state=new DragonVisualState{position=position,rotation=rotation,locomotion=locomotion,speed=0}});
            if(live==null||live.Entry!=entry||live.Clips==null||!live.Clips.Has(locomotion)||live.ShownLocomotion!=locomotion)
            {Stop("Actual imported model cannot show requested clip; review aborted.");return;}
            var scene=SceneManager.GetActiveScene();sceneHandle=scene.handle;zoneScene=scene.path;
            running=true;message="Running direct same-medium visual review. No gameplay resident.";
        }
        void Tick()
        {
            if(!running)return;
            if(!EnvironmentOnly(out var data,out var actual)||data!=locations||actual!=medium||
                SceneManager.GetActiveScene().handle!=sceneHandle||SceneManager.GetActiveScene().path!=zoneScene||
                manager==null||!manager.TryGet(ReviewUid,out var live)||live.Entry!=entry||live.ShownLocomotion!=locomotion)
            {Stop("Scene/model/medium changed; review aborted.");return;}
            if(Time.frameCount==lastFrame)return;lastFrame=Time.frameCount;
            float dt=Mathf.Min(Time.deltaTime,.05f);if(dt<=0)return;
            if(waypoint>=route.Waypoints.Count){Stop("Arrived: "+travelled.ToString("F2")+" m, all sampled steps clear.");return;}
            var target=SanctuaryNavigationSurface.Vector(route.Waypoints[waypoint]);
            var delta=target-position;
            var heading=Vector3.ProjectOnPlane(delta,Vector3.up);
            var before=rotation;
            if(heading.sqrMagnitude>.000001f)
                rotation=Quaternion.RotateTowards(rotation,Quaternion.LookRotation(heading),turnLimit*dt);
            float turn=Mathf.DeltaAngle(before.eulerAngles.y,rotation.eulerAngles.y)/dt;
            var next=heading.sqrMagnitude<=.000001f||Vector3.Angle(rotation*Vector3.forward,heading)<10f?
                Vector3.MoveTowards(position,target,speed*dt):position;
            bool Clear(NavPoint p)=>surface.ClearReviewVolume(SanctuaryNavigationSurface.Vector(p),start.habitatId,medium,envelope);
            if(!SanctuaryRoutePlanner.SegmentClear(SanctuaryNavigationSurface.Point(position),SanctuaryNavigationSurface.Point(next),Clear,.025f))
            {Stop("Geometry changed / step rejected; no movement applied.");return;}
            var step=next-position;travelled+=step.magnitude;position=next;
            if(!manager.ApplyState(ReviewUid,new DragonVisualState{position=position,rotation=rotation,locomotion=locomotion,speed=step.magnitude/dt})||
                live.ShownLocomotion!=locomotion){Stop("Clip substitution; review aborted.");return;}
            manager.SetMotionShape(ReviewUid,medium==NavigationMedium.Swim?DragonMotionMode.Water:DragonMotionMode.Air,
                turn,step.y/dt,step.magnitude/dt);
            if((target-position).sqrMagnitude<.000001f)waypoint++;
            Repaint();
        }
        void Stop(string reason)
        {
            running=false;message=reason;consent=false;
            if(manager!=null)manager.DespawnAll();
            if(reviewRoot!=null)
            {
                if(Application.isPlaying)UnityEngine.Object.Destroy(reviewRoot);
                else UnityEngine.Object.DestroyImmediate(reviewRoot);
            }
            manager=null;reviewRoot=null;Repaint();
            Debug.Log("U5.4.2 Spatial Motion Review: "+reason);
        }
    }
}
