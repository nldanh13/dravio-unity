using System.IO;
using System.Text;
using Dravio.Navigation;
using Dravio.TechnologyLab;
using Dravio.EnvironmentArt;
using UnityEditor;
using UnityEngine;

namespace Dravio.EditorTools
{
    public static class TechnologyLabMapReport
    {
        [MenuItem("Dravio/Technology Lab/Routed Crowd Benchmark")]
        public static void Crowd()
        {
            var map=new LabLocationMap();
            JsonUtility.FromJsonOverwrite(File.ReadAllText("Assets/Dravio/Environment/Data/OriginSanctuaryLocations.json"),map);
            var report=new StringBuilder("Same authored A* paths; local steering comparison, not full production controller.\n");
            foreach(int n in new[]{5,15,30})
            {
                var lab=RoutedCrowdExperiment.Build(map,"origin_meadow",n);
                if(lab==null){report.AppendLine("No valid pair for "+n);continue;}
                for(int i=0;i<400;i++)lab.Step();
                report.AppendLine(lab.Summary());
            }
            Debug.Log(report.ToString());
        }
        [MenuItem("Dravio/Technology Lab/Authored Route Benchmark")]
        public static void Run()
        {
            string[] paths={"Assets/Dravio/Environment/Data/OriginSanctuaryLocations.json",
                "Assets/Dravio/Environment/Zones/Ocean/Data/OceanLocations.json",
                "Assets/Dravio/Environment/Zones/Sky/Data/SkyLocations.json"};
            var report=new StringBuilder("Technology Lab: authored JSON, existing A* grid sweep; not NavVolume or Unity movement.\n");
            foreach(var path in paths)
            {
                string json=File.ReadAllText(path);var map=new LabLocationMap();JsonUtility.FromJsonOverwrite(json,map);
                var data=ScriptableObject.CreateInstance<SanctuaryLocationData>();
                try
                {
                    data.LoadFromJson(json);var production=new SanctuaryNavigationSurface(data);
                    var medium=map.zoneId=="origin"?NavigationMedium.Ground:map.zoneId=="ocean"?NavigationMedium.Swim:NavigationMedium.Air;
                    int parityMismatch=0;
                    foreach(var spawn in map.spawnPoints)
                    {
                        bool mirror=map.Clear(spawn.Point,spawn.habitatId,medium);
                        bool actual=production.Clear(SanctuaryNavigationSurface.Vector(spawn.Point),spawn.habitatId,medium);
                        if(mirror!=actual)parityMismatch++;
                    }
                    report.AppendLine(map.zoneId+" spawn geometry parity mismatches="+parityMismatch);
                    foreach(var h in map.habitats)
                    {
                        foreach(float step in new[]{.30f,.45f,.60f})
                            report.AppendLine(AuthoredRouteBenchmark.Describe(AuthoredRouteBenchmark.Run(map,h.id,medium,step)));
                        report.AppendLine(AdaptiveRouteLab.Compare(map,h.id,medium));
                    }
                }
                finally{Object.DestroyImmediate(data);}
            }
            Debug.Log(report.ToString());
        }
    }
}
