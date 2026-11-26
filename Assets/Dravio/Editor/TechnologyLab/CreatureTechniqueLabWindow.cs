using System;
using Dravio.TechnologyLab;
using UnityEditor;
using UnityEngine;

namespace Dravio.EditorTools
{
    public sealed class CreatureTechniqueLabWindow:EditorWindow
    {
        float slope=15,sideSlope=0,bodyOffset=.05f;
        bool planted=true,occluded,eligible=true,inReach,facing,motionReady,hasPath=true;
        ActionSequenceLab action=new ActionSequenceLab();
        [MenuItem("Dravio/Technology Lab/Creature Techniques")]
        static void Open()=>GetWindow<CreatureTechniqueLabWindow>("Creature Techniques");
        void OnGUI()
        {
            EditorGUILayout.HelpBox("Synthetic mathematical prototypes only. No rig/scene/domain changes.",MessageType.Info);
            EditorGUILayout.LabelField("TL.4 Contact plane",EditorStyles.boldLabel);
            slope=EditorGUILayout.Slider("Slope degrees",slope,-40,40);
            sideSlope=EditorGUILayout.Slider("Side slope degrees",sideSlope,-40,40);
            bodyOffset=EditorGUILayout.Slider("Ground offset",bodyOffset,-.3f,.3f);
            planted=EditorGUILayout.Toggle("Foot planted",planted);
            double a=Math.Tan(sideSlope*Math.PI/180),b=Math.Tan(slope*Math.PI/180);
            var samples=new[]{new ContactSample(-.5,-.5,bodyOffset-a*.5-b*.5),
                new ContactSample(.5,-.5,bodyOffset+a*.5-b*.5),new ContactSample(-.5,.5,bodyOffset-a*.5+b*.5),
                new ContactSample(.5,.5,bodyOffset+a*.5+b*.5)};
            var pose=ContactLab.Solve(samples);
            EditorGUILayout.LabelField("Limited pitch / roll",pose.Pitch.ToString("F1")+" / "+pose.Roll.ToString("F1"));
            EditorGUILayout.LabelField("Body / first foot offset",pose.BodyOffset.ToString("F3")+" / "+
                ContactLab.FootOffset(0,samples[0].Height,planted,true,.18).ToString("F3"));
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("TL.5 Perception and action gates",EditorStyles.boldLabel);
            occluded=EditorGUILayout.Toggle("Target occluded",occluded);
            eligible=EditorGUILayout.Toggle("Canonical eligible (mock)",eligible);
            var memory=new LabMemory();
            memory.Sense(new LabVec(),"demo",3,0,new[]{new LabStimulus("food","demo",StimulusKind.Food,new LabVec(1,0),5000,eligible)},(a1,b1)=>!occluded);
            EditorGUILayout.LabelField("Currently sensed",memory.Snapshot().Count.ToString());
            if(GUILayout.Button("Begin synthetic action"))action.Begin("food");
            hasPath=EditorGUILayout.Toggle("Path available",hasPath);
            inReach=EditorGUILayout.Toggle("In reach",inReach);
            facing=EditorGUILayout.Toggle("Facing",facing);
            motionReady=EditorGUILayout.Toggle("Motion ready",motionReady);
            if(GUILayout.Button("Advance action"))action.Tick(eligible,hasPath,inReach,facing,motionReady);
            if(GUILayout.Button("Mock accept request"))action.Resolve(true);
            EditorGUILayout.LabelField("Action step / request count",action.Step+" / "+action.Requests);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("TL.6 Mutual social matching",EditorStyles.boldLabel);
            var pairs=SocialPairLab.Match(new[]{new SocialCandidate("a","demo",new LabVec(0,0),true),
                new SocialCandidate("b","demo",new LabVec(1,0),true),new SocialCandidate("c","demo",new LabVec(.2,0),false)},(x,y)=>true);
            foreach(var pair in pairs)EditorGUILayout.LabelField(pair.A+" + "+pair.B+" (c opts out)");
        }
    }
}
