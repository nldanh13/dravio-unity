using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Animations;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Dravio.Editor
{
    // Explicit artist action: choose a local, licensed Emberling Baby Normal FBX.
    // Never overwrite the accepted U0/U1 scene or modify save/domain state.
    public static class EmberlingModelPipeline
    {
        const string Root = "Assets/Dravio";
        const string Source = Root + "/Scenes/AncientSanctuary.unity";
        const string StaticTarget = Root + "/Scenes/EmberlingModelPreview.unity";
        const string LivingTarget = Root + "/Scenes/EmberlingLivingPreview.unity";
        const string WanderTarget = Root + "/Scenes/EmberlingWanderPreview.unity";
        const string CareTarget = Root + "/Scenes/EmberlingCarePreview.unity";
        const string Prefabs = Root + "/Art/Imported/Prefabs";
        const string Materials = Root + "/Art/Imported/Materials";

        [MenuItem("Dravio/Preview Selected Emberling FBX")]
        public static void Preview() => PreviewInternal(false);

        [MenuItem("Dravio/Preview Selected Living Emberling FBX")]
        public static void PreviewLiving() => PreviewInternal(true);

        [MenuItem("Dravio/Preview Selected Wandering Emberling FBX")]
        public static void PreviewWander() => PreviewInternal(true, true);

        [MenuItem("Dravio/Preview Selected Care Emberling FBX")]
        public static void PreviewCare() => PreviewInternal(true, true, true);

        private static void PreviewInternal(bool living, bool wander = false, bool care = false)
        {
            string target = care ? CareTarget : wander ? WanderTarget : living ? LivingTarget : StaticTarget;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            string assetPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(assetPath) ||
                !assetPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                !assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) ||
                AssetImporter.GetAtPath(assetPath) is not ModelImporter)
            {
                EditorUtility.DisplayDialog("Dravio", "Select the imported Emberling Baby Normal FBX in the Project window.", "OK");
                return;
            }
            if (living)
            {
                // Configure only a separately exported living asset, not the accepted static FBX.
                if (Path.GetFileNameWithoutExtension(assetPath) != (care ? "EmberlingCare" : wander ? "EmberlingWander" : "EmberlingLiving"))
                {
                    EditorUtility.DisplayDialog("Dravio", care ? "Select EmberlingCare.fbx." : wander ? "Select EmberlingWander.fbx." : "Select EmberlingLiving.fbx, not the original static FBX.", "OK");
                    return;
                }
                var importer = (ModelImporter)AssetImporter.GetAtPath(assetPath);
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = true;
                importer.SaveAndReimport();
                importer = (ModelImporter)AssetImporter.GetAtPath(assetPath);
                var clips = importer.defaultClipAnimations;
                if (clips.Length == 0)
                {
                    Debug.LogError("Dravio: FBX has no animation take.");
                    return;
                }
                if (wander)
                {
                    var take = clips.OrderByDescending(x => x.lastFrame - x.firstFrame).First();
                    float start = take.firstFrame;
                    if (take.lastFrame - start < (care ? 443.5f : 215.5f))
                    {
                        Debug.LogError(care ? "Dravio: expected 445 frames from --care-preview." : "Dravio: expected 217 frames from --walk-preview.");
                        return;
                    }
                    var split = new System.Collections.Generic.List<ModelImporterClipAnimation> {
                        MakeLoop(take.takeName, "Idle", start, start + 180),
                        MakeLoop(take.takeName, "Walk", start + 181, start + 216)
                    };
                    if (care)
                    {
                        split.Add(MakeLoop(take.takeName, "Eat", start + 217, start + 277, false));
                        split.Add(MakeLoop(take.takeName, "Doze", start + 278, start + 398));
                        split.Add(MakeLoop(take.takeName, "Happy", start + 399, start + 444, false));
                    }
                    importer.clipAnimations = split.ToArray();
                }
                else
                {
                    foreach (var clip in clips)
                    {
                        clip.loopTime = true;
                        clip.loopPose = true;
                    }
                    importer.clipAnimations = clips;
                }
                importer.SaveAndReimport();
            }

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (model == null || AssetDatabase.LoadAssetAtPath<SceneAsset>(Source) == null)
            {
                Debug.LogError("Dravio: missing FBX model or Ancient Sanctuary scene.");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(target) != null)
            {
                EditorUtility.DisplayDialog("Dravio", "A model preview already exists. Open it in Project > Assets/Dravio/Scenes. To make a new preview, rename the old scene first.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Prefabs);
            Directory.CreateDirectory(Materials);
            AssetDatabase.Refresh();

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) { Debug.LogError("Dravio: URP Lit shader not found."); return; }

            GameObject wrapper = null;
            GameObject prefab;
            try
            {
                // Generate a reusable visual prefab at identity, not a camera-dependent gameplay object.
                wrapper = new GameObject("Emberling Imported Visual");
                var instance = PrefabUtility.InstantiatePrefab(model, wrapper.transform) as GameObject;
                if (instance == null) throw new InvalidOperationException("Cannot instantiate imported model.");
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
                    collider.enabled = false;
                foreach (var animator in instance.GetComponentsInChildren<Animator>(true))
                    animator.applyRootMotion = false;

                var renderers = instance.GetComponentsInChildren<Renderer>(true);
                var bounds = GetLocalBounds(wrapper.transform, renderers);
                if (bounds.size.y < .001f || !float.IsFinite(bounds.size.y))
                    throw new InvalidOperationException("FBX has invalid bounds.");
                float factor = 2.05f / bounds.size.y;
                instance.transform.localScale *= factor;
                instance.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * factor;
                bounds = GetLocalBounds(wrapper.transform, renderers);
                var hitbox = wrapper.AddComponent<BoxCollider>();
                hitbox.center = bounds.center;
                hitbox.size = Vector3.Max(bounds.size, Vector3.one * .05f);
                ConvertMaterials(renderers, shader, living);
                if (living) ConfigureIdle(instance, assetPath, wander, care);
                string prefabPath = AssetDatabase.GenerateUniqueAssetPath(Prefabs + (care ? "/EmberlingCareVisual.prefab" : wander ? "/EmberlingWanderVisual.prefab" : living ? "/EmberlingLivingVisual.prefab" : "/EmberlingBabyVisual.prefab"));
                prefab = PrefabUtility.SaveAsPrefabAsset(wrapper, prefabPath);
                if (prefab == null) throw new InvalidOperationException("Prefab save failed.");
            }
            catch (Exception error)
            {
                Debug.LogError("Dravio import failed: " + error.Message);
                Debug.LogException(error);
                return;
            }
            finally
            {
                if (wrapper != null) Object.DestroyImmediate(wrapper);
            }

            // Scene copy first; source remains unchanged even when preview generation fails.
            Scene sourceScene = EditorSceneManager.OpenScene(Source);
            if (!EditorSceneManager.SaveScene(sourceScene, target, true))
            { Debug.LogError("Dravio: could not copy scene."); return; }
            Scene preview = EditorSceneManager.OpenScene(target);
            var turntable = Object.FindFirstObjectByType<DragonTurntable>();
            if (turntable == null || Camera.main == null)
            { Debug.LogError("Dravio: expected U1 turntable and camera."); return; }

            var root = turntable.transform;
            var oldChildren = new Transform[root.childCount];
            for (int i = 0; i < oldChildren.Length; i++) oldChildren[i] = root.GetChild(i);
            var visual = PrefabUtility.InstantiatePrefab(prefab, root) as GameObject;
            if (visual == null) { Debug.LogError("Dravio: visual prefab could not be instantiated."); return; }
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;
            foreach (var child in oldChildren) Object.DestroyImmediate(child.gameObject);
            root.name = care ? "Emberling - Care Baby Normal" : wander ? "Emberling - Wandering Baby Normal" : living ? "Emberling - Living Baby Normal" : "Emberling - Imported Baby Normal";
            turntable.SetCamera(Camera.main);
            if (care)
            {
                var animator = visual.GetComponentInChildren<Animator>(true);
                CarePreviewHud.Build(root.gameObject, animator, turntable);
            }
            else if (wander)
            {
                var animator = visual.GetComponentInChildren<Animator>(true);
                root.gameObject.AddComponent<DragonWanderPreview>().Configure(animator, turntable);
                foreach (var label in Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsSortMode.None))
                    if (label.text.Contains("Drag to rotate"))
                        label.text = "Emberling  |  Auto walk & rest  |  Space: inspect / resume  |  Drag or arrows to rotate";
            }
            EditorSceneManager.MarkSceneDirty(preview);
            if (!EditorSceneManager.SaveScene(preview))
                Debug.LogError("Dravio: preview save failed.");
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root.gameObject;
            Debug.Log("Dravio: imported model preview ready. Press Play; drag Emberling, use arrows or R. Original scene preserved.");
        }

        static Bounds GetLocalBounds(Transform root, Renderer[] renderers)
        {
            Bounds result = default;
            bool first = true;
            foreach (Renderer renderer in renderers)
            {
                if (!(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) continue;
                Bounds b = renderer.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 p = root.InverseTransformPoint(b.center + Vector3.Scale(b.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    if (first) { result = new Bounds(p, Vector3.zero); first = false; }
                    else result.Encapsulate(p);
                }
            }
            if (first) throw new InvalidOperationException("FBX contains no mesh renderer.");
            return result;
        }

        static ModelImporterClipAnimation MakeLoop(string take, string name, float first, float last, bool loop = true)
        {
            return new ModelImporterClipAnimation {
                name = name, takeName = take, firstFrame = first, lastFrame = last,
                loopTime = loop, loopPose = loop, keepOriginalPositionY = true,
                lockRootHeightY = true, lockRootPositionXZ = true, lockRootRotation = true
            };
        }

        static void ConfigureIdle(GameObject instance, string assetPath, bool wander, bool care)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            var clip = assets.OfType<AnimationClip>().FirstOrDefault(x =>
                !x.name.StartsWith("__preview__", StringComparison.Ordinal) && x.length > 0 &&
                (!wander || x.name == "Idle"));
            var walk = wander ? assets.OfType<AnimationClip>().FirstOrDefault(x => x.name == "Walk" && x.length > 0) : null;
            if (wander && walk == null) throw new InvalidOperationException("Walk clip missing after FBX split.");
            if (clip == null) throw new InvalidOperationException("No imported idle clip.");
            // UnityEngine.Object can represent missing components as a managed
            // reference that compares equal to null. Do not use C# ?? here.
            if (!instance.TryGetComponent<Animator>(out var animator) || animator == null)
                animator = instance.AddComponent<Animator>();
            if (animator == null)
                throw new InvalidOperationException("Unable to attach Animator to " + instance.name);
            if (animator.avatar == null)
                animator.avatar = assets.OfType<Avatar>().FirstOrDefault(x => x.isValid);
            if (animator.avatar == null || !animator.avatar.isValid)
                throw new InvalidOperationException("Generic avatar missing or invalid. Check FBX rig import.");
            string path = AssetDatabase.GenerateUniqueAssetPath(Prefabs + (care ? "/EmberlingCare.controller" : wander ? "/EmberlingWander.controller" : "/EmberlingIdle.controller"));
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var state = controller.AddMotion(clip);
            state.name = "Idle";
            controller.layers[0].stateMachine.defaultState = state;
            if (care)
            {
                controller.AddMotion(walk).name = "Walk";
                foreach (string name in new[] {"Eat","Doze","Happy"})
                {
                    var action = assets.OfType<AnimationClip>().FirstOrDefault(x => x.name == name && x.length > 0);
                    if (action == null) throw new InvalidOperationException("Missing care clip: " + name);
                    controller.AddMotion(action).name = name;
                }
            }
            else if (wander)
            {
                controller.AddParameter("Moving", AnimatorControllerParameterType.Bool);
                var walking = controller.AddMotion(walk);
                walking.name = "Walk";
                var toWalk = state.AddTransition(walking);
                toWalk.hasExitTime = false;
                toWalk.duration = .15f;
                toWalk.hasFixedDuration = true;
                toWalk.AddCondition(AnimatorConditionMode.If, 0, "Moving");
                var toIdle = walking.AddTransition(state);
                toIdle.hasExitTime = false;
                toIdle.duration = .15f;
                toIdle.hasFixedDuration = true;
                toIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "Moving");
            }
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        static void ConvertMaterials(Renderer[] renderers, Shader shader, bool living)
        {
            var converted = new System.Collections.Generic.Dictionary<Material, Material>();
            foreach (Renderer renderer in renderers)
            {
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    Material src = slots[i];
                    if (src == null || (!living && src.shader == shader)) continue;
                    if (!converted.TryGetValue(src, out var dst))
                    {
                        dst = new Material(shader) { name = src.name + " URP" };
                        if (src.HasProperty("_BaseColor")) dst.SetColor("_BaseColor", src.GetColor("_BaseColor"));
                        else if (src.HasProperty("_Color")) dst.SetColor("_BaseColor", src.GetColor("_Color"));
                        if (src.HasProperty("_BaseMap")) dst.SetTexture("_BaseMap", src.GetTexture("_BaseMap"));
                        else if (src.HasProperty("_MainTex")) dst.SetTexture("_BaseMap", src.GetTexture("_MainTex"));
                        if (src.HasProperty("_Smoothness")) dst.SetFloat("_Smoothness", src.GetFloat("_Smoothness"));
                        else if (src.HasProperty("_Glossiness")) dst.SetFloat("_Smoothness", src.GetFloat("_Glossiness"));
                        if (living)
                        {
                            // FBX import may not preserve Blender roughness; use deliberate matte defaults.
                            string name = src.name.ToLowerInvariant();
                            bool eye = name.Contains("iris") || name.Contains("pupil") || name.Contains("catchlight");
                            dst.SetFloat("_Smoothness", eye ? .48f : .18f);
                            dst.SetFloat("_Metallic", 0f);
                        }
                        AssetDatabase.CreateAsset(dst, AssetDatabase.GenerateUniqueAssetPath(Materials + "/EmberlingMaterial.mat"));
                        converted.Add(src, dst);
                    }
                    slots[i] = dst;
                }
                renderer.sharedMaterials = slots;
            }
        }
    }
}
