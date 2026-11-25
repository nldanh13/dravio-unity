using System.IO;
using Dravio.Character.Diagnostics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dravio.Character.Editor
{
    /// <summary>
    /// Dravio/Character/Create Stress Test Scene: a separate scene (Assets/Dravio/Character/Scenes/DragonStress.unity,
    /// never the main scene, not added to Build Settings) with a camera, light, ground collider, DragonVisualManager
    /// bound to DragonVisualCatalog and DragonStressHarness (5 / 15 / 30 dragons). Press Play, or make a
    /// Development build containing only this scene to measure on device. Existing scene is not overwritten.
    /// </summary>
    internal static class DragonStressSceneBuilder
    {
        internal const string ScenePath = "Assets/Dravio/Character/Scenes/DragonStress.unity";

        [MenuItem("Dravio/Character/Create Stress Test Scene")]
        private static void Menu()
        {
            if (File.Exists(ScenePath)) { Debug.Log("Stress scene exists, opening (not overwritten): " + ScenePath); OpenIfConfirmed(); return; }
            var catalog = AssetDatabase.LoadAssetAtPath<DragonVisualCatalog>(LivingAssetBatchImporter.DragonCatalogPath);
            if (catalog == null || catalog.entries.Count == 0)
            {
                Debug.LogError("No dragons in " + LivingAssetBatchImporter.DragonCatalogPath + ". Run Dravio/Character/Import Batch Manifest first.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cam.tag = "MainCamera";
            cam.transform.SetPositionAndRotation(new Vector3(0f, 9f, -18f), Quaternion.Euler(25f, 0f, 0f));
            var light = new GameObject("Directional Light", typeof(Light));
            light.GetComponent<Light>().type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(4f, 1f, 4f);
            var go = new GameObject("DragonVisualManager");
            var manager = go.AddComponent<DragonVisualManager>();
            manager.catalog = catalog;
            manager.viewCamera = cam.GetComponent<Camera>();
            var harness = new GameObject("DragonStressHarness").AddComponent<DragonStressHarness>();
            harness.manager = manager;
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("Created " + ScenePath + ". Press Play; results are logged and written to Application.persistentDataPath.");
        }

        private static void OpenIfConfirmed()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }
    }
}
