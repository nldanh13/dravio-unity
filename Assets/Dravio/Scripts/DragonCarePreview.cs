using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Rendering;
using System.Collections.Generic;

namespace Dravio
{
    // Standalone previews stay visual-only; the integrated scene uses the optional domain adapter.
    public sealed class DragonCarePreview : MonoBehaviour
    {
        private enum Activity { Idle, Walk, Eat, Doze, Happy, Inspect }
        [SerializeField] private Animator animator;
        [SerializeField] private DragonTurntable turntable;
        [SerializeField] private Text status;
        [SerializeField] private Button feed, pet, sleep, inspect, resume;
        [SerializeField] private float facingOffset = 180f;
        [SerializeField] private float speed = .55f;
        [SerializeField] private float turnSpeed = 180f;
        private static readonly Vector2[] Waypoints = {
            new Vector2(-1.1f,-.8f), new Vector2(-.9f,.35f),
            new Vector2(.9f,.35f), new Vector2(1.1f,-.8f)
        };
        private static readonly string[] ClipNames = {"Idle","Walk","Eat","Doze","Happy"};
        private Vector3 origin;
        private int next;
        private Activity current;
        private float remaining;
        private string activeClip;
        private bool ready;
        private bool vietnamese;
        private Camera inspectionCamera;
        private Vector3 normalCameraPosition;
        private float normalCameraSize;
        private readonly List<Material> previewMaterials = new();
        [SerializeField] private SanctuaryDomainPreview domain;
        public void ConfigureDomain(SanctuaryDomainPreview session) { domain=session; }
        public bool IsInspecting => current == Activity.Inspect;
        public void SetVietnamese(bool value) { vietnamese=value; if(domain!=null)domain.SetVietnamese(value); RefreshStatus(); }

        public void Configure(Animator a, DragonTurntable t, Text label,
            Button feedButton, Button petButton, Button sleepButton, Button inspectButton, Button resumeButton)
        {
            animator=a;turntable=t;status=label;
            feed=feedButton;pet=petButton;sleep=sleepButton;inspect=inspectButton;resume=resumeButton;
        }

        private void Start()
        {
            if (animator == null || turntable == null || animator.runtimeAnimatorController == null)
            { Debug.LogError("Dravio: care preview requires a configured Animator and turntable.",this); enabled=false; return; }
            foreach(string clip in ClipNames)
            {
                if(!animator.HasState(0, Animator.StringToHash("Base Layer."+clip)))
                { Debug.LogError("Dravio: missing care state "+clip,this);enabled=false;return; }
            }
            origin=transform.position;
            animator.applyRootMotion=false;
            if(gameObject.name=="Emberling - Integrated GLB")
            {
                // The imported GLB faces Unity +Z; legacy procedural rig faces -Z.
                facingOffset=0f;
                PrepareGlbPresentation();
            }
            ready=true;
            if(feed!=null)feed.onClick.AddListener(Feed);
            if(pet!=null)pet.onClick.AddListener(Pet);
            if(sleep!=null)sleep.onClick.AddListener(ToggleDoze);
            if(inspect!=null)inspect.onClick.AddListener(ToggleInspect);
            if(resume!=null)resume.onClick.AddListener(Resume);
            Enter(Activity.Idle,2f);
        }

        private void OnDestroy()
        {
            if(feed!=null)feed.onClick.RemoveListener(Feed);
            if(pet!=null)pet.onClick.RemoveListener(Pet);
            if(sleep!=null)sleep.onClick.RemoveListener(ToggleDoze);
            if(inspect!=null)inspect.onClick.RemoveListener(ToggleInspect);
            if(resume!=null)resume.onClick.RemoveListener(Resume);
            RestoreInspectionCamera();
            foreach(var material in previewMaterials)if(material!=null)Destroy(material);
        }

        // Presentation-only: preserve the authored 0.90m mesh and all textured
        // materials. For UV-less meshes, display existing vertex RGB if available.
        private void PrepareGlbPresentation()
        {
            var camera=Camera.main;
            if(camera!=null && camera.orthographic)
            {
                inspectionCamera=camera;
                normalCameraPosition=camera.transform.position;
                normalCameraSize=camera.orthographicSize;
            }
            var shader=Shader.Find("Dravio/Environment/Painterly Lit");
            foreach(var renderer in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh=renderer.sharedMesh;
                if(mesh==null || mesh.HasVertexAttribute(VertexAttribute.TexCoord0))continue;
                if(!mesh.HasVertexAttribute(VertexAttribute.Color))
                {
                    Debug.LogWarning("Dravio GLB: no UV0 or vertex colors; source material color retained.",renderer);
                    continue;
                }
                if(shader==null || !shader.isSupported)
                {
                    Debug.LogWarning("Dravio GLB: vertex-color preview shader unavailable.",renderer);
                    continue;
                }
                var slots=renderer.sharedMaterials;
                for(int i=0;i<slots.Length;i++)
                {
                    var source=slots[i];
                    var material=new Material(shader){name="GLB vertex color (Play preview)"};
                    if(source!=null)
                    {
                        if(source.HasProperty("_BaseColor"))material.SetColor("_BaseColor",source.GetColor("_BaseColor"));
                        else if(source.HasProperty("_Color"))material.SetColor("_BaseColor",source.GetColor("_Color"));
                    }
                    material.SetFloat("_VertexAO",1f);
                    material.SetFloat("_WindStrength",0f);
                    material.SetFloat("_PathBlend",0f);
                    material.SetFloat("_RimStrength",.05f);
                    previewMaterials.Add(material);
                    slots[i]=material;
                }
                renderer.sharedMaterials=slots;
                Debug.Log("Dravio GLB: displaying imported vertex colors (preview only).",renderer);
            }
        }

        private void LateUpdate()
        {
            if(inspectionCamera==null)return;
            var t=inspectionCamera.transform;
            float targetSize=normalCameraSize;
            Vector3 targetPosition=normalCameraPosition;
            if(IsInspecting)
            {
                // No camera orbit or physical scaling. Keep the dragon left of the HUD.
                targetSize=Mathf.Min(normalCameraSize,2f);
                Vector3 subject=transform.position+Vector3.up*.45f;
                float depth=Vector3.Dot(subject-normalCameraPosition,t.forward);
                targetPosition=subject-t.forward*depth+t.right*(.30f*targetSize*inspectionCamera.aspect);
            }
            float blend=1f-Mathf.Exp(-8f*Time.unscaledDeltaTime);
            t.position=Vector3.Lerp(t.position,targetPosition,blend);
            inspectionCamera.orthographicSize=Mathf.Lerp(inspectionCamera.orthographicSize,targetSize,blend);
        }

        private void RestoreInspectionCamera()
        {
            if(inspectionCamera==null)return;
            inspectionCamera.transform.position=normalCameraPosition;
            inspectionCamera.orthographicSize=normalCameraSize;
        }

        public void Feed() {
            if(!ready || !isActiveAndEnabled)return;
            if(domain==null)Enter(Activity.Eat,2.2f);
            else if(domain.RequestFood())Enter(Activity.Idle,0);
        }
        public void Pet() { if(ready && isActiveAndEnabled && (domain==null || domain.TryPet())) Enter(Activity.Happy,1.7f); }
        public void ToggleDoze() { if(ready && isActiveAndEnabled) { if(current==Activity.Doze) Resume(); else Enter(Activity.Doze,0); } }
        public void ToggleInspect() { if(ready && isActiveAndEnabled) { if(current==Activity.Inspect) Resume(); else Enter(Activity.Inspect,0); } }
        public void Inspect() { if(ready && isActiveAndEnabled && !IsInspecting) Enter(Activity.Inspect,0); }
        public void Resume() { if(ready && isActiveAndEnabled) Enter(Activity.Idle,1f); }

        private void Enter(Activity state,float duration)
        {
            current=state;
            remaining=duration;
            turntable.enabled=state==Activity.Inspect;
            string clip=state==Activity.Inspect?"Idle":state.ToString();
            // Repeated Feed/Pet clicks explicitly restart those one-shot previews.
            if(activeClip!=clip || state==Activity.Eat || state==Activity.Happy)
            {
                animator.CrossFadeInFixedTime(Animator.StringToHash("Base Layer."+clip),.12f,0,0);
                activeClip=clip;
            }
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            if(status==null)return;
            status.text=vietnamese ? current switch {
                Activity.Idle=>"Đang nghỉ",Activity.Walk=>"Đang khám phá",Activity.Eat=>domain!=null?"Đang ăn":"Đang ăn (thử nghiệm)",
                Activity.Doze=>"Đang ngủ gật",Activity.Happy=>"Vui vẻ",_=>"Đang quan sát"
            } : current switch {
                Activity.Idle=>"Resting",Activity.Walk=>"Exploring",Activity.Eat=>domain!=null?"Eating":"Eating (preview)",
                Activity.Doze=>"Dozing (preview)",Activity.Happy=>"Happy (preview)",_=>"Inspecting"
            };
        }

        private void Update()
        {
            if(domain!=null && !domain.CanInteract)return;
            var keys=Keyboard.current;
            if(keys!=null)
            {
                if(keys.fKey.wasPressedThisFrame)Feed();
                else if(keys.pKey.wasPressedThisFrame)Pet();
                else if(keys.nKey.wasPressedThisFrame)ToggleDoze();
                else if(keys.spaceKey.wasPressedThisFrame)ToggleInspect();
            }
            if(current==Activity.Doze || current==Activity.Inspect) return;
            float dt=Time.deltaTime;
            if(current==Activity.Eat || current==Activity.Happy)
            {
                remaining-=dt;
                if(remaining<=0)Enter(Activity.Idle,1f);
                return;
            }
            bool seeking=domain!=null && domain.HasFood;
            if(!seeking && current==Activity.Idle && remaining>0)
            {
                remaining-=dt;
                return;
            }
            Vector2 wp=Waypoints[next];
            Vector3 target=seeking?domain.FoodPosition:origin+new Vector3(wp.x,0,wp.y);
            target.y=origin.y;
            Vector3 dir=target-transform.position;
            dir.y=0;
            if(seeking && dir.sqrMagnitude<=.55f*.55f)
            {
                // Turn the muzzle toward the food even when already in reach.
                if(dir.sqrMagnitude>0.0001f)
                {
                    Quaternion foodHeading=Quaternion.LookRotation(dir.normalized,Vector3.up)*Quaternion.Euler(0,facingOffset,0);
                    transform.rotation=Quaternion.RotateTowards(transform.rotation,foodHeading,turnSpeed*dt);
                    if(Quaternion.Angle(transform.rotation,foodHeading)>12f)
                    {
                        if(current!=Activity.Idle)Enter(Activity.Idle,0);
                        return;
                    }
                }
                if(domain.TryConsume())Enter(Activity.Eat,2.2f);
                else if(current!=Activity.Idle)Enter(Activity.Idle,0);
                return;
            }
            if(dir.sqrMagnitude<.0036f)
            {
                next=(next+1)%Waypoints.Length;
                Enter(Activity.Idle,2.2f);
                return;
            }
            Quaternion heading=Quaternion.LookRotation(dir.normalized,Vector3.up)*Quaternion.Euler(0,facingOffset,0);
            transform.rotation=Quaternion.RotateTowards(transform.rotation,heading,turnSpeed*dt);
            if(Quaternion.Angle(transform.rotation,heading)>12f)
            {
                if(current!=Activity.Idle)Enter(Activity.Idle,0);
                return;
            }
            if(current!=Activity.Walk)Enter(Activity.Walk,0);
            transform.position=Vector3.MoveTowards(transform.position,target,speed*dt);
        }
    }
}
