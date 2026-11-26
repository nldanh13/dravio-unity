using System;
using Dravio.TechnologyLab;
using UnityEditor;
using UnityEngine;

namespace Dravio.EditorTools
{
    // Editor-only synthetic arena. Does not open a scene, spawn real dragons or access GameSession.
    public sealed class DravioTechnologyLabWindow : EditorWindow
    {
        LabScenario scenario=LabScenario.HeadOn;
        int countIndex;
        static readonly int[] Counts={5,15,30};
        float flock=.5f;
        bool running;
        double nextStep;
        SteeringLab baseline,steering;
        [MenuItem("Dravio/Technology Lab/Steering Comparison")]
        static void Open()=>GetWindow<DravioTechnologyLabWindow>("Dravio Steering Lab");
        void OnEnable(){ResetLab();EditorApplication.update+=Tick;}
        void OnDisable(){EditorApplication.update-=Tick;running=false;}
        void ResetLab()
        {
            running=false;baseline=SteeringLab.Create(scenario,Counts[countIndex]);
            steering=SteeringLab.Create(scenario,Counts[countIndex]);
        }
        void Advance(){baseline.Step(LabMode.StopOnly);steering.Step(LabMode.PredictiveSteering,flock:flock);}
        void Tick()
        {
            if(!running || EditorApplication.timeSinceStartup<nextStep)return;
            nextStep=EditorApplication.timeSinceStartup+.05;
            if(baseline.Tick>=800){running=false;Repaint();return;}
            Advance();Repaint();
        }
        void OnGUI()
        {
            if(baseline==null || steering==null)ResetLab();
            EditorGUILayout.HelpBox("Synthetic 2D disc test. No scene/save/asset modifications. Stop-only is a simplified reference, not the full U5.3.6 controller.",MessageType.Info);
            EditorGUI.BeginChangeCheck();
            scenario=(LabScenario)EditorGUILayout.EnumPopup("Scenario",scenario);
            countIndex=EditorGUILayout.Popup("Agents (head-on always 2)",countIndex,new[]{"5","15","30"});
            flock=EditorGUILayout.Slider("Same-group bias",flock,0,1);
            if(EditorGUI.EndChangeCheck())ResetLab();
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button(running?"Pause":"Run")){running=!running;nextStep=EditorApplication.timeSinceStartup;}
                if(GUILayout.Button("Step")){running=false;Advance();}
                if(GUILayout.Button("Reset"))ResetLab();
                if(GUILayout.Button("Compare 400 ticks"))
                {
                    ResetLab();var timer=System.Diagnostics.Stopwatch.StartNew();
                    for(int i=0;i<400;i++)Advance();
                    timer.Stop();
                    Debug.Log("Technology Lab "+scenario+" n="+steering.Agents.Count+" ticks=400 "+
                        "\nStop: "+Summary(baseline)+"\nSteering: "+Summary(steering)+
                        "\nCombined editor CPU elapsed ms: "+timer.Elapsed.TotalMilliseconds.ToString("F2")+
                        " (not game FPS or mobile benchmark)");
                }
            }
            EditorGUILayout.LabelField("Simulated seconds", (baseline.Tick*.05).ToString("F2"));
            using(new EditorGUILayout.HorizontalScope())
            {Arena("Stop only",baseline);Arena("Predictive steering",steering);}
        }
        static string Summary(SteeringLab lab)=>"Arrived "+lab.Arrived+"/"+lab.Agents.Count+
            " | stalled ticks "+lab.StoppedAgentTicks+" | min sampled gap "+lab.MinGap.ToString("F3")+
            " | distance "+lab.DistanceTravelled.ToString("F1");
        static void Arena(string name,SteeringLab lab)
        {
            using(new EditorGUILayout.VerticalScope())
            {
                EditorGUILayout.LabelField(name,EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Arrived",lab.Arrived+"/"+lab.Agents.Count);
                EditorGUILayout.LabelField("Stopped agent ticks",lab.StoppedAgentTicks.ToString());
                EditorGUILayout.LabelField("Minimum sampled gap",lab.MinGap.ToString("F3")+" m");
                var rect=GUILayoutUtility.GetAspectRect(1f,GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(rect,new Color(.12f,.16f,.18f));
                float scale=(float)(Mathf.Min(rect.width,rect.height)/(2*lab.HalfExtent));
                Vector2 At(LabVec v)=>rect.center+new Vector2((float)v.X*scale,-(float)v.Y*scale);
                foreach(var o in lab.Obstacles)
                {
                    var p=At(o.Center);float size=(float)o.Radius*scale*2;
                    EditorGUI.DrawRect(new Rect(p.x-size/2,p.y-size/2,size,size),new Color(.44f,.45f,.44f));
                }
                foreach(var a in lab.Agents)
                {
                    var g=At(a.Goal);var p=At(a.Position);float size=Mathf.Max(4,(float)a.Radius*scale*2);
                    EditorGUI.DrawRect(new Rect(g.x-2,g.y-2,4,4),new Color(.55f,.55f,.55f));
                    Color color=a.Arrived?new Color(.35f,.8f,.43f):a.Group==1?new Color(.83f,.56f,.26f):new Color(.33f,.65f,.9f);
                    EditorGUI.DrawRect(new Rect(p.x-size/2,p.y-size/2,size,size),color);
                    if(lab.Agents.Count<=5)GUI.Label(new Rect(p.x+size/2,p.y-10,25,20),a.Id.ToString(),EditorStyles.miniLabel);
                }
            }
        }
    }
}
