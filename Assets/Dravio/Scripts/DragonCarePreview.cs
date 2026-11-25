using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Dravio
{
    // Presentation-only: these actions do NOT spend food, gold or change bond/health.
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
        }

        public void Feed() { if(ready && isActiveAndEnabled) Enter(Activity.Eat,2.2f); }
        public void Pet() { if(ready && isActiveAndEnabled) Enter(Activity.Happy,1.7f); }
        public void ToggleDoze() { if(ready && isActiveAndEnabled) { if(current==Activity.Doze) Resume(); else Enter(Activity.Doze,0); } }
        public void ToggleInspect() { if(ready && isActiveAndEnabled) { if(current==Activity.Inspect) Resume(); else Enter(Activity.Inspect,0); } }
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
            if(status!=null) status.text=state switch {
                Activity.Idle=>"Resting",Activity.Walk=>"Exploring",Activity.Eat=>"Eating (preview)",
                Activity.Doze=>"Dozing (preview)",Activity.Happy=>"Happy (preview)",_=>"Inspecting: drag or arrows"
            };
        }

        private void Update()
        {
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
            if(current==Activity.Idle && remaining>0)
            {
                remaining-=dt;
                return;
            }
            Vector2 wp=Waypoints[next];
            Vector3 target=origin+new Vector3(wp.x,0,wp.y);
            Vector3 dir=target-transform.position;
            dir.y=0;
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
