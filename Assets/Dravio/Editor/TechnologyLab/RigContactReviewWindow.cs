using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dravio.Character;
using Dravio.Character.Diagnostics;
using Dravio.TechnologyLab;
using UnityEditor;
using UnityEngine;

namespace Dravio.EditorTools
{
    // Read-only probe of the actual Character Animation Lab instance. No bone or collider writes.
    public sealed class RigContactReviewWindow:EditorWindow
    {
        DragonAnimationLab lab;
        Transform flatGround,unevenGround;
        GameObject capturedInstance;
        AnimationClip capturedClip;
        Transform capturedFlat,capturedUneven;
        Transform[] feet;
        DragonFootGrounding grounding;
        bool ikMode,running,capturedUnevenActive;
        int lastFrame=-1;
        float rayAbove=.5f,rayBelow=.5f,capturedAbove,capturedBelow;
        string label="",message="Assign Animation Lab and explicit ground roots in Play Mode.";
        RigContactCapture capture;
        RigContactFrame latest;
        readonly List<RigFootSample> samples=new List<RigFootSample>();
        [MenuItem("Dravio/Technology Lab/Rig Contact Review")]
        static void Open()=>GetWindow<RigContactReviewWindow>("Rig Contact Review");
        void OnEnable(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=OnPlayMode;}
        void OnDisable(){EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=OnPlayMode;running=false;}
        void OnPlayMode(PlayModeStateChange state){if(state==PlayModeStateChange.ExitingPlayMode){running=false;message="Play Mode ended; export the existing capture or start a new one.";Repaint();}}
        void OnGUI()
        {
            EditorGUILayout.HelpBox("Read-only sample of actual catalog IK feet. Run Character > Animation Lab in Play Mode. Compare the same clip, terrain and sample times with IK off/on. Ground distance is a bone-anchor proxy, not mesh penetration or authored planted contact.",MessageType.Info);
            using(new EditorGUI.DisabledScope(running))
            {
                lab=(DragonAnimationLab)EditorGUILayout.ObjectField("Animation Lab",lab,typeof(DragonAnimationLab),true);
                if(GUILayout.Button("Find active Animation Lab"))
                {
                    lab=UnityEngine.Object.FindFirstObjectByType<DragonAnimationLab>();
                    if(lab!=null)unevenGround=lab.unevenGround!=null?lab.unevenGround.transform:null;
                }
                flatGround=(Transform)EditorGUILayout.ObjectField("Flat ground root",flatGround,typeof(Transform),true);
                unevenGround=(Transform)EditorGUILayout.ObjectField("Uneven ground root",unevenGround,typeof(Transform),true);
                rayAbove=EditorGUILayout.Slider("Ray above foot (m)",rayAbove,.05f,2);
                rayBelow=EditorGUILayout.Slider("Ray below foot (m)",rayBelow,.05f,2);
                if(GUILayout.Button("Start capture (600 samples max)"))Begin();
            }
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("Stop"))running=false;
                using(new EditorGUI.DisabledScope(capture==null||capture.Count==0))
                    if(GUILayout.Button("Export CSV"))
                    {
                        running=false;
                        var path=EditorUtility.SaveFilePanel("Export rig contact review","","dravio-rig-contact","csv");
                        if(!string.IsNullOrEmpty(path))File.WriteAllText(path,capture.Csv());
                    }
            }
            EditorGUILayout.LabelField("Capture",label,EditorStyles.wordWrappedLabel);
            EditorGUILayout.HelpBox(message,MessageType.None);
            if(capture!=null)
            {
                EditorGUILayout.LabelField("Summary",capture.Summary(),EditorStyles.wordWrappedLabel);
                if(capture.Count>0)
                {
                    EditorGUILayout.LabelField("Last clip time",latest.Time.ToString("F3"));
                    EditorGUILayout.LabelField("Last valid foot rays",latest.Hits+"/"+latest.Feet);
                    EditorGUILayout.LabelField("Candidate tilt / height",latest.Candidate.Valid?
                        latest.Candidate.Pitch.ToString("F1")+" / "+latest.Candidate.Roll.ToString("F1")+" / "+latest.Candidate.BodyOffset.ToString("F3"):"insufficient near-ground feet");
                }
            }
        }
        void Begin()
        {
            if(!Application.isPlaying||lab==null||lab.Instance==null||lab.CurrentClip==null||lab.catalog==null||
                flatGround==null){message="Play Mode, active clip, catalog and explicit flat ground required.";return;}
            var instance=lab.Instance;var clip=lab.CurrentClip;
            if(!DragonFootGrounding.AppliesTo(DragonAnimationLab.ShownLocomotion(clip),DragonAnimationLab.Interaction(clip)))
            {message="Current clip is not a supported ground clip.";return;}
            // Animation Lab names its spawned instance from the exact catalog entry. Reject ambiguity.
            var matches=lab.catalog.entries.Where(e=>e!=null&&e.rigged&&
                instance.name==e.speciesId+"_"+e.stage+" (lab)").ToArray();
            if(matches.Length!=1||matches[0].ikChains==null||matches[0].ikChains.Length==0)
            {message="No unique rigged catalog entry with IK chains; no guessed anatomy.";return;}
            var bones=instance.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name)
                .ToDictionary(g=>g.Key,g=>g.ToArray(),StringComparer.Ordinal);
            Transform Find(string name)=>!string.IsNullOrEmpty(name)&&bones.TryGetValue(name,out var a)&&a.Length==1?a[0]:null;
            var list=new List<Transform>();
            foreach(var chain in matches[0].ikChains)
            {
                if(chain==null){message="Invalid chain.";return;}
                var upper=Find(chain.upper);var lower=Find(chain.lower);var foot=Find(chain.foot);
                if(upper==null||lower==null||foot==null||upper==lower||lower==foot||!lower.IsChildOf(upper)||
                    !foot.IsChildOf(lower)||list.Contains(foot))
                {message="Missing, duplicate or invalid chain: "+chain.foot;return;}
                list.Add(foot);
            }
            if(flatGround.IsChildOf(instance.transform)||(unevenGround!=null&&unevenGround.IsChildOf(instance.transform)))
            {message="Ground cannot be part of the dragon.";return;}
            grounding=instance.GetComponentInChildren<DragonFootGrounding>(true);
            if(grounding==null||grounding.ChainCount!=list.Count)
            {message="Existing Character grounding not configured for every declared chain.";return;}
            feet=list.ToArray();capturedInstance=instance;capturedClip=clip;capturedFlat=flatGround;capturedUneven=unevenGround;
            capturedAbove=rayAbove;capturedBelow=rayBelow;ikMode=grounding.isActiveAndEnabled;
            capturedUnevenActive=unevenGround!=null&&unevenGround.gameObject.activeInHierarchy;
            label=matches[0].speciesId+" / "+matches[0].stage+" / "+clip.name+" / IK "+(ikMode?"on":"off")+
                " / uneven "+(capturedUnevenActive?"on":"off")+" / "+feet.Length+" chains";
            capture=new RigContactCapture();lastFrame=-1;running=true;message="Recording rendered pose snapshots. No transformations applied.";
        }
        bool GroundHit(RaycastHit h)
        {
            if(h.collider==null||h.collider.transform.IsChildOf(capturedInstance.transform))return false;
            var t=h.collider.transform;
            return t==capturedFlat||t.IsChildOf(capturedFlat)||
                (capturedUneven!=null&&(t==capturedUneven||t.IsChildOf(capturedUneven)));
        }
        void Tick()
        {
            if(!running)return;
            if(!Application.isPlaying||lab==null||lab.Instance!=capturedInstance||capturedInstance==null||
                lab.CurrentClip!=capturedClip||grounding==null||grounding.isActiveAndEnabled!=ikMode||
                capturedFlat==null||!capturedFlat.gameObject.activeInHierarchy||
                (capturedUneven!=null&&capturedUneven.gameObject.activeInHierarchy)!=capturedUnevenActive)
            {running=false;message="Capture stopped: rig, clip, IK or ground changed.";Repaint();return;}
            if(Time.frameCount==lastFrame)return;
            lastFrame=Time.frameCount;
            var root=capturedInstance.transform;
            var forward=Vector3.ProjectOnPlane(root.forward,Vector3.up).normalized;
            if(forward.sqrMagnitude<.5f){running=false;message="Invalid root forward.";Repaint();return;}
            var right=Vector3.Cross(Vector3.up,forward);
            samples.Clear();
            foreach(var foot in feet)
            {
                if(foot==null){running=false;message="A rig bone disappeared.";Repaint();return;}
                var p=foot.position;var relative=p-root.position;
                var hits=Physics.RaycastAll(p+Vector3.up*capturedAbove,Vector3.down,capturedAbove+capturedBelow,~0,QueryTriggerInteraction.Ignore);
                float best=float.PositiveInfinity,groundY=0;
                foreach(var hit in hits)
                    if(GroundHit(hit)&&hit.normal.y>.2f&&hit.distance<best)
                    {best=hit.distance;groundY=hit.point.y-root.position.y;}
                samples.Add(new RigFootSample(Vector3.Dot(relative,right),Vector3.Dot(relative,forward),groundY,relative.y,!float.IsInfinity(best)));
            }
            latest=new RigContactFrame(lab.CurrentTime,samples);capture.Add(latest);
            if(capture.Full){running=false;message="Capture complete. Export before starting another capture.";}
            Repaint();
        }
    }
}
