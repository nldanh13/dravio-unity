using System;
using System.Collections.Generic;
using System.Linq;
using Dravio.EnvironmentArt;
using Dravio.EnvironmentArt.EditorTools;
using Dravio.Platform.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Dravio.Editor
{
    internal static class ConnectedWorldsBuilder
    {
        [MenuItem("Dravio/Worlds/Prepare Connected Worlds",priority=10)]
        static void Prepare()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            if(!EditorUtility.DisplayDialog("Connected Worlds",
                "Generate missing Origin/Ocean/Sky scenes from local assets, validate their roles, and register all three in global build settings with Origin first. Existing scene files will not be overwritten. Check the active Unity 6 Build Profile scene list separately.","Prepare","Cancel"))return;
            EmberlingModelPipeline.OpenGame();
            if(EditorSceneManager.GetActiveScene().path!=ConnectedWorldPolicy.OriginScene ||
                UnityEngine.Object.FindFirstObjectByType<ConnectedWorldController>()==null ||
                UnityEngine.Object.FindObjectsByType<DravioSessionHost>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length!=1)
            {Debug.LogError("Connected Worlds: generate OriginConnectedWorlds with local assets first.");return;}
            try
            {
                foreach(var zone in new[]{ZoneEnvironmentBuilder.Ocean,ZoneEnvironmentBuilder.Sky})
                {
                    if(AssetDatabase.LoadAssetAtPath<SceneAsset>(zone.ScenePath)==null && !ZoneEnvironmentBuilder.BuildEverything(zone,false))
                        throw new InvalidOperationException("Zone assets missing or build failed: "+zone.id);
                    var scene=EditorSceneManager.OpenScene(zone.ScenePath,OpenSceneMode.Single);
                    var roots=scene.GetRootGameObjects();
                    var env=roots.SelectMany(x=>x.GetComponentsInChildren<LivingEnvironment>()).Where(x=>x.isActiveAndEnabled).ToArray();
                    var cameras=roots.SelectMany(x=>x.GetComponentsInChildren<Camera>()).Where(x=>x.isActiveAndEnabled).ToArray();
                    if(env.Length!=1||env[0].locations==null||env[0].locations.zoneId!=zone.id||cameras.Length!=1||!cameras[0].orthographic ||
                        roots.Any(x=>x.GetComponentInChildren<DravioSessionHost>(true)!=null||
                            x.GetComponentInChildren<LivingSanctuaryPresentation>(true)!=null||
                            x.GetComponentInChildren<SanctuaryDomainPreview>(true)!=null||
                            x.GetComponentInChildren<EventSystem>(true)!=null||x.GetComponentInChildren<Canvas>(true)!=null))
                        throw new InvalidOperationException("Not a clean environment-only scene: "+zone.ScenePath);
                }
                string[] required={ConnectedWorldPolicy.OriginScene,ConnectedWorldPolicy.OceanScene,ConnectedWorldPolicy.SkyScene};
                var scenes=required.Select(x=>new EditorBuildSettingsScene(x,true))
                    .Concat(EditorBuildSettings.scenes.Where(x=>!required.Contains(x.path))).ToArray();
                EditorBuildSettings.scenes=scenes;
                Debug.Log("Connected Worlds: registered Origin shell and both environment scenes. Check active Build Profile overrides. No existing scene files replaced.");
            }
            catch(Exception e){Debug.LogError("Connected Worlds preparation failed: "+e.Message);}
            finally
            {
                EditorSceneManager.OpenScene(ConnectedWorldPolicy.OriginScene,OpenSceneMode.Single);
            }
        }
    }
}
