using System.IO;
using Dravio.Character.Diagnostics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Dravio.Character.Editor
{
    /// <summary>
    /// Dravio/Character/Animation Lab: opens Assets/Dravio/Character/Scenes/DragonAnimationLab.unity, creating it the
    /// first time (camera, light, flat ground, a block of steps for foot IK tests, DragonAnimationLab bound to
    /// DragonVisualCatalog). A separate review scene: not added to Build Settings, and no other scene is modified.
    /// An existing lab scene is opened as it is, never overwritten. Press Play to use it.
    /// </summary>
    internal static class DragonAnimationLabBuilder
    {
        internal const string ScenePath = "Assets/Dravio/Character/Scenes/DragonAnimationLab.unity";

        [MenuItem("Dravio/Character/Animation Lab")]
        private static void Menu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath); return; }
            var catalog = AssetDatabase.LoadAssetAtPath<DragonVisualCatalog>(LivingAssetBatchImporter.DragonCatalogPath);
            if (catalog == null)
                Debug.LogWarning("No " + LivingAssetBatchImporter.DragonCatalogPath + " yet: the lab is created empty. " +
                                 "Run Dravio/Character/Import Batch Manifest, then assign the catalog on DragonAnimationLab.");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cam.tag = "MainCamera";
            var camera = cam.GetComponent<Camera>();
            camera.nearClipPlane = 0.01f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.32f, 0.34f, 0.37f);
            var light = new GameObject("Directional Light", typeof(Light));
            light.GetComponent<Light>().type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(2f, 1f, 2f);
            // Steps under the left legs and the front: uneven ground for foot IK (toggled from the lab panel).
            var uneven = new GameObject("Uneven Ground");
            float[] heights = { 0.04f, 0.08f, 0.12f };
            for (int i = 0; i < heights.Length; i++)
            {
                var step = GameObject.CreatePrimitive(PrimitiveType.Cube);
                step.name = "Step " + (i + 1);
                step.transform.SetParent(uneven.transform, false);
                step.transform.localScale = new Vector3(0.5f, heights[i] * 2f, 0.3f);
                step.transform.localPosition = new Vector3(-0.3f, 0f, -0.45f + 0.3f * i);
            }
            uneven.SetActive(false);
            var stage = new GameObject("Stage").transform;
            var lab = new GameObject("DragonAnimationLab").AddComponent<DragonAnimationLab>();
            lab.catalog = catalog;
            lab.mainCamera = camera;
            lab.unevenGround = uneven;
            lab.stage = stage;
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("Created " + ScenePath + ". Press Play to review clips (species, clip, speed, frame step, LOD, IK, skeleton, QA warnings).");
        }
    }
}
