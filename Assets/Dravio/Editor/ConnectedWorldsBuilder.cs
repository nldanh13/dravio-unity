using System;
using System.IO;
using UnityEditor.Build.Reporting;
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
        static string[] RequiredScenes()=>new[]{ConnectedWorldPolicy.OriginScene,ConnectedWorldPolicy.OceanScene,ConnectedWorldPolicy.SkyScene};

        static T[] Components<T>(Scene scene) where T:Component =>
            scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<T>(true)).ToArray();

        static void ValidateScene(Scene scene,string zone)
        {
            if(!scene.IsValid()||!scene.isLoaded||scene.path!=ConnectedWorldPolicy.ScenePath(zone))
                throw new InvalidOperationException("Wrong scene for "+zone);
            var env=Components<LivingEnvironment>(scene).Where(x=>x.isActiveAndEnabled).ToArray();
            var cameras=Components<Camera>(scene).Where(x=>x.isActiveAndEnabled).ToArray();
            if(env.Length!=1||env[0].locations==null||env[0].locations.zoneId!=zone||
                cameras.Length!=1||!cameras[0].orthographic)
                throw new InvalidOperationException("Invalid environment or camera: "+scene.path);
            foreach(var t in Components<Transform>(scene))
                if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0)
                    throw new InvalidOperationException("Missing script: "+scene.path+" / "+t.name);
            var hosts=Components<DravioSessionHost>(scene);
            var living=Components<LivingSanctuaryPresentation>(scene);
            var adapters=Components<SanctuaryDomainPreview>(scene);
            var controllers=Components<ConnectedWorldController>(scene);
            var events=Components<EventSystem>(scene);
            if(zone==ConnectedWorldPolicy.Origin)
            {
                if(hosts.Length!=1||!hosts[0].isActiveAndEnabled||living.Length!=1||!living[0].isActiveAndEnabled||
                    adapters.Length!=1||!adapters[0].isActiveAndEnabled||controllers.Length!=1||!controllers[0].isActiveAndEnabled||
                    events.Length!=1||!events[0].isActiveAndEnabled||Components<Canvas>(scene).Length==0)
                    throw new InvalidOperationException("Origin requires one active gameplay shell, host and EventSystem: "+scene.path);
            }
            else if(hosts.Length+living.Length+adapters.Length+controllers.Length+events.Length+Components<Canvas>(scene).Length!=0)
                throw new InvalidOperationException("Not an environment-only scene: "+scene.path);
        }

        // Preserve every existing entry, but only the three gameplay scenes are enabled.
        static void RegisterAlphaScenes()
        {
            var required=RequiredScenes();
            EditorBuildSettings.scenes=required.Select(x=>new EditorBuildSettingsScene(x,true))
                .Concat(EditorBuildSettings.scenes.Where(x=>!required.Contains(x.path))
                    .Select(x=>new EditorBuildSettingsScene(x.path,false))).ToArray();
        }

        static bool CanLeaveCurrentScenes()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling)return false;
            // Do not allow "Don't Save" to discard an owner's local scene edits.
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return false;
            for(int i=0;i<SceneManager.sceneCount;i++)
                if(SceneManager.GetSceneAt(i).isDirty||string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path))
                {
                    Debug.LogWarning("Dravio Alpha: save or close untitled/modified scenes manually first.");
                    return false;
                }
            return true;
        }

        [MenuItem("Dravio/Worlds/Prepare Connected Worlds",priority=10)]
        static void Prepare()
        {
            if(!CanLeaveCurrentScenes())return;
            if(!EditorUtility.DisplayDialog("Connected Worlds",
                "Generate missing Origin/Ocean/Sky scenes from local assets, validate their roles, and register exactly three enabled scenes with Origin first; other entries will be disabled, not deleted. Existing scene files will not be overwritten. Check active Build Profile overrides separately.","Prepare","Cancel"))return;
            EmberlingModelPipeline.OpenGame();
            if(EditorSceneManager.GetActiveScene().path!=ConnectedWorldPolicy.OriginScene)
            {Debug.LogError("Connected Worlds: generate OriginLongTermGameplay with local assets first.");return;}
            try
            {
                ValidateScene(EditorSceneManager.GetActiveScene(),ConnectedWorldPolicy.Origin);
                foreach(var zone in new[]{ZoneEnvironmentBuilder.Ocean,ZoneEnvironmentBuilder.Sky})
                {
                    if(AssetDatabase.LoadAssetAtPath<SceneAsset>(zone.ScenePath)==null && !ZoneEnvironmentBuilder.BuildEverything(zone,false))
                        throw new InvalidOperationException("Zone assets missing or build failed: "+zone.id);
                    var scene=EditorSceneManager.OpenScene(zone.ScenePath,OpenSceneMode.Single);
                    ValidateScene(scene,zone.id);
                }
                RegisterAlphaScenes();
                Debug.Log("Connected Worlds: exactly three enabled gameplay scenes; other entries disabled. Check active Build Profile overrides. Existing scenes preserved.");
            }
            catch(Exception e){Debug.LogError("Connected Worlds preparation failed: "+e.Message);}
            finally
            {
                EditorSceneManager.OpenScene(ConnectedWorldPolicy.OriginScene,OpenSceneMode.Single);
            }
        }

        [MenuItem("Dravio/Alpha/Build Android Development APK",priority=20)]
        static void BuildAlphaApk()
        {
            if(!CanLeaveCurrentScenes())return;
            if(EditorUserBuildSettings.activeBuildTarget!=BuildTarget.Android||
                !BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android,BuildTarget.Android))
            {Debug.LogError("Dravio Alpha: install Android Build Support and switch active build target to Android first.");return;}
            if(EditorUserBuildSettings.buildAppBundle)
            {Debug.LogError("Dravio Alpha: disable Build App Bundle in Android Build Profiles to output an APK.");return;}
            var required=RequiredScenes();
            if(required.Any(x=>AssetDatabase.LoadAssetAtPath<SceneAsset>(x)==null))
            {Debug.LogError("Dravio Alpha: missing gameplay scene(s); run Dravio > Worlds > Prepare Connected Worlds first.");return;}
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach(var zone in new[]{ConnectedWorldPolicy.Origin,ConnectedWorldPolicy.Ocean,ConnectedWorldPolicy.Sky})
                    ValidateScene(EditorSceneManager.OpenScene(ConnectedWorldPolicy.ScenePath(zone),OpenSceneMode.Single),zone);
            }
            catch(Exception e){Debug.LogError("Dravio Alpha scene validation failed: "+e.Message);return;}
            finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}
            if(!EditorUtility.DisplayDialog("DRAVIO Android Alpha",
                "Build a local DEVELOPMENT APK from exactly Origin, Ocean and Sky, in that order. No release signing or installation. Current app identity and SDK settings remain unchanged. This is not production approval.","Choose output","Cancel"))return;
            var directory=Path.GetFullPath("Builds");
            Directory.CreateDirectory(directory);
            var output=EditorUtility.SaveFilePanel("New DRAVIO Alpha APK",directory,"Dravio-Alpha-development","apk");
            if(string.IsNullOrEmpty(output))return;
            output=Path.GetFullPath(output);
            var assets=Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar;
            if(!output.EndsWith(".apk",StringComparison.OrdinalIgnoreCase)||File.Exists(output)||
                output.StartsWith(assets,StringComparison.OrdinalIgnoreCase))
            {Debug.LogError("Dravio Alpha: choose a new .apk outside Assets. Existing output will not be replaced.");return;}
            try
            {
                // Explicit scene list, independent of the global sample scene and profile scene overrides.
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes=required,locationPathName=output,target=BuildTarget.Android,
                    options=BuildOptions.Development
                });
                if(report.summary.result!=BuildResult.Succeeded)
                    Debug.LogError("Dravio Alpha APK build failed: "+report.summary.result+"; errors "+report.summary.totalErrors);
                else Debug.Log("Dravio Alpha APK built: "+output+"; device acceptance still required.");
            }
            catch(Exception e){Debug.LogError("Dravio Alpha APK build failed: "+e.Message);}
        }
    }
}
