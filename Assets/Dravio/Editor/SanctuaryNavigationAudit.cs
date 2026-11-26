using System;
using System.IO;
using System.Linq;
using System.Text;
using Dravio.Navigation;
using Dravio.EnvironmentArt;
using UnityEditor;
using UnityEngine;

namespace Dravio.EditorTools
{
    // Read-only diagnostic: parse committed location JSON, no scene building, no gameplay session.
    public static class SanctuaryNavigationAudit
    {
        [MenuItem("Dravio/Environment/Validate Navigation Routes")]
        public static void Validate()
        {
            string[] paths={
                "Assets/Dravio/Environment/Data/OriginSanctuaryLocations.json",
                "Assets/Dravio/Environment/Zones/Ocean/Data/OceanLocations.json",
                "Assets/Dravio/Environment/Zones/Sky/Data/SkyLocations.json"};
            var report=new StringBuilder("Navigation route audit (geometry only; not movement or asset QA)\n");
            foreach(var path in paths)
            {
                var data=ScriptableObject.CreateInstance<SanctuaryLocationData>();
                try
                {
                    data.LoadFromJson(File.ReadAllText(path));var nav=new SanctuaryNavigationSurface(data);
                    report.AppendLine(data.zoneId+":");
                    foreach(var habitat in data.habitats)
                    foreach(var medium in new[]{NavigationMedium.Ground,NavigationMedium.Swim,NavigationMedium.Air})
                    {
                        var candidates=data.SpawnPointsIn(habitat.id).Where(p=>
                            (medium==NavigationMedium.Ground?(string.IsNullOrEmpty(p.medium)||p.medium=="ground"):
                                medium==NavigationMedium.Swim?p.medium=="swim":p.medium=="air") &&
                            nav.Clear(p.Position,habitat.id,medium)).ToArray();
                        int direct=0,found=0,failed=0,maxExpanded=0;
                        for(int i=0;i<candidates.Length;i++)
                        for(int j=i+1;j<candidates.Length;j++)
                        {
                            var route=nav.Plan(candidates[i].Position,candidates[j].Position,habitat.id,medium,budget:2048);
                            if(route.Status==RouteStatus.Direct)direct++;
                            else if(route.Status==RouteStatus.Found)found++;
                            else failed++;
                            maxExpanded=Math.Max(maxExpanded,route.Expanded);
                        }
                        report.AppendLine("  "+habitat.id+" "+medium+": "+candidates.Length+" valid spawns, "+
                            direct+" direct, "+found+" detours, "+failed+" unreachable/budget, max expanded "+maxExpanded);
                    }
                    report.AppendLine("  authored links="+data.areaLinks.Length+" gates="+data.zoneGates.Length+
                        " (metadata only; no cross-medium transitions certified)");
                }
                finally{UnityEngine.Object.DestroyImmediate(data);}
            }
            Debug.Log(report.ToString());
        }
    }
}
