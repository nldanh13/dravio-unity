using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Animations;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Dravio.EnvironmentArt;
using Dravio.EnvironmentArt.EditorTools;
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
        const string PolishTarget = Root + "/Scenes/EmberlingPolishedPreview.unity";
        const string FantasyTarget = Root + "/Scenes/EmberlingFantasyPreview.unity";
        const string RefinedTarget = Root + "/Scenes/EmberlingRefinedPreview.unity";
        const string IntegratedTarget = Root + "/Scenes/OriginIntegratedPreview.unity";
        const string IntegratedGlbTarget = Root + "/Scenes/OriginIntegratedGLBPreview.unity";
        const string PlayableTarget = Root + "/Scenes/OriginPlayable.unity";
        const string PlayableEnvironment = Root + "/Environment/Scenes/OriginPlayable_Environment.unity";
        const string Prefabs = Root + "/Art/Imported/Prefabs";
        const string Materials = Root + "/Art/Imported/Materials";

        [MenuItem("Dravio/Legacy Previews/Preview Selected Emberling FBX")]
        public static void Preview() => PreviewInternal(false);

        [MenuItem("Dravio/Legacy Previews/Preview Selected Living Emberling FBX")]
        public static void PreviewLiving() => PreviewInternal(true);

        [MenuItem("Dravio/Legacy Previews/Preview Selected Wandering Emberling FBX")]
        public static void PreviewWander() => PreviewInternal(true, true);

        [MenuItem("Dravio/Legacy Previews/Preview Selected Care Emberling FBX")]
        public static void PreviewCare() => PreviewInternal(true, true, true);

        [MenuItem("Dravio/Legacy Previews/Preview Selected Polished Emberling FBX")]
        public static void PreviewPolished() => PreviewInternal(true, true, true, true);

        [MenuItem("Dravio/Legacy Previews/Create or Open Fantasy Preview")]
        public static void PreviewFantasy() => PreviewInternal(true, true, true, true, true);

        [MenuItem("Dravio/Legacy Previews/Create or Open Refined Preview")]
        public static void PreviewRefined() => PreviewInternal(true, true, true, true, true, true);

        [MenuItem("Dravio/Legacy Previews/Create or Open Integrated Sanctuary")]
        public static void PreviewIntegrated() => OpenIntegrated(false);

        [MenuItem("Dravio/Legacy Previews/Create or Open Integrated GLB Sanctuary")]
        public static void PreviewIntegratedGLB() => OpenIntegrated(true);

        [MenuItem("Dravio/Open Game", priority = 0)]
        public static void OpenGame()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(PlayableTarget)!=null)
            {
                if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())EditorSceneManager.OpenScene(PlayableTarget);
                return;
            }
            bool glb=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Art/Models/EmberlingRefinedGLB.fbx")!=null;
            if(!glb && AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Art/Models/EmberlingRefined.fbx")==null)
            {
                EditorUtility.DisplayDialog("Dravio","Import EmberlingRefinedGLB.fbx or EmberlingRefined.fbx first.","OK");
                return;
            }
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            if(!OriginSanctuaryEnvironmentBuilder.SetupMaterials() || !OriginSanctuaryEnvironmentBuilder.BuildPrefabs())return;
            if(OriginSanctuaryEnvironmentBuilder.UpdateLocationData()==null)return;
            OriginSanctuaryEnvironmentBuilder.BuildScene(false,PlayableEnvironment);
            if(EditorSceneManager.GetActiveScene().path!=PlayableEnvironment)return;
            if(LivingEnvironmentBuilder.UpdateLivingData()==null)return;
            LivingEnvironmentBuilder.AddToOpenScene(false,PlayableEnvironment);
            if(GameObject.Find(LivingEnvironmentBuilder.RootName)==null)
            { Debug.LogError("Dravio: living environment generation failed.");return; }
            PreviewInternal(true,true,true,true,true,true,true,glb,true);
        }

        private static void OpenIntegrated(bool sourceGlb)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            string target = sourceGlb ? IntegratedGlbTarget : IntegratedTarget;
            string fbx = sourceGlb ? "EmberlingRefinedGLB.fbx" : "EmberlingRefined.fbx";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(target) == null &&
                AssetDatabase.LoadAssetAtPath<SceneAsset>(Dravio.EnvironmentArt.EditorTools.OriginSanctuaryEnvironmentBuilder.ScenePath) == null)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Art/Models/" + fbx) == null)
                {
                    EditorUtility.DisplayDialog("Dravio", "Export " + fbx + " first.", "OK");
                    return;
                }
                Dravio.EnvironmentArt.EditorTools.OriginSanctuaryEnvironmentBuilder.BuildEverything();
            }
            PreviewInternal(true, true, true, true, true, true, true, sourceGlb);
        }

        private static void PreviewInternal(bool living, bool wander = false, bool care = false, bool polish = false, bool fantasy = false, bool refined = false, bool integrated = false, bool sourceGlb = false, bool playable = false)
        {
            string source = playable ? PlayableEnvironment : integrated ? Dravio.EnvironmentArt.EditorTools.OriginSanctuaryEnvironmentBuilder.ScenePath : Source;
            string target = playable ? PlayableTarget : sourceGlb ? IntegratedGlbTarget : integrated ? IntegratedTarget : refined ? RefinedTarget : fantasy ? FantasyTarget : polish ? PolishTarget : care ? CareTarget : wander ? WanderTarget : living ? LivingTarget : StaticTarget;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (fantasy && AssetDatabase.LoadAssetAtPath<SceneAsset>(target) != null)
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(target);
                    if (integrated && Object.FindFirstObjectByType<SanctuaryDomainPreview>() == null)
                        Debug.LogError("Dravio: incomplete integrated scene. Back up/rename it before rebuilding.");
                }
                return;
            }
            string assetPath = sourceGlb ? Root + "/Art/Models/EmberlingRefinedGLB.fbx" : refined ? Root + "/Art/Models/EmberlingRefined.fbx" : fantasy ? Root + "/Art/Models/EmberlingPolished.fbx" : AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(assetPath) ||
                !assetPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                !assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) ||
                AssetImporter.GetAtPath(assetPath) is not ModelImporter)
            {
                EditorUtility.DisplayDialog("Dravio", sourceGlb ? "Export EmberlingRefinedGLB.fbx with --source-glb first." : refined ? "Export EmberlingRefined.fbx using scripts/art/Export-EmberlingPreview.ps1 -Mode Refined first." : fantasy ? "Export EmberlingPolished.fbx using scripts/art/Export-EmberlingPreview.ps1 -Mode Polished first." : "Select the imported Emberling Baby Normal FBX in the Project window.", "OK");
                return;
            }
            if (living)
            {
                // Configure only a separately exported living asset, not the accepted static FBX.
                if (Path.GetFileNameWithoutExtension(assetPath) != (sourceGlb ? "EmberlingRefinedGLB" : refined ? "EmberlingRefined" : polish ? "EmberlingPolished" : care ? "EmberlingCare" : wander ? "EmberlingWander" : "EmberlingLiving"))
                {
                    EditorUtility.DisplayDialog("Dravio", sourceGlb ? "Select EmberlingRefinedGLB.fbx." : refined ? "Select EmberlingRefined.fbx." : polish ? "Select EmberlingPolished.fbx." : care ? "Select EmberlingCare.fbx." : wander ? "Select EmberlingWander.fbx." : "Select EmberlingLiving.fbx, not the original static FBX.", "OK");
                    return;
                }
                var importer = (ModelImporter)AssetImporter.GetAtPath(assetPath);
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = true;
                if (sourceGlb)
                {
                    importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                    importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
                    importer.importNormals = ModelImporterNormals.Import;
                    importer.importTangents = ModelImporterTangents.CalculateMikk;
                }
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

            if (refined)
            {
                if (!EmberlingAssetAudit.Validate(assetPath, out var report))
                {
                    Debug.LogError(report);
                    return;
                }
                if (report.Contains("WARNING:")) Debug.LogWarning(report);
                else Debug.Log(report);
            }
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (model == null || AssetDatabase.LoadAssetAtPath<SceneAsset>(source) == null)
            {
                Debug.LogError("Dravio: missing FBX model or source scene: " + source);
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
                float factor = (sourceGlb ? .90f : 2.05f) / bounds.size.y;
                instance.transform.localScale *= factor;
                instance.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * factor;
                bounds = GetLocalBounds(wrapper.transform, renderers);
                var hitbox = wrapper.AddComponent<BoxCollider>();
                hitbox.center = bounds.center;
                hitbox.size = Vector3.Max(bounds.size, Vector3.one * .05f);
                if (sourceGlb) ConvertImportedGlbMaterials(renderers, shader);
                else ConvertMaterials(renderers, shader, living);
                if (living) ConfigureIdle(instance, assetPath, wander, care, polish, refined, sourceGlb);
                string prefabPath = AssetDatabase.GenerateUniqueAssetPath(Prefabs + (sourceGlb ? "/EmberlingRefinedGLBVisual.prefab" : refined ? "/EmberlingRefinedVisual.prefab" : polish ? "/EmberlingPolishedVisual.prefab" : care ? "/EmberlingCareVisual.prefab" : wander ? "/EmberlingWanderVisual.prefab" : living ? "/EmberlingLivingVisual.prefab" : "/EmberlingBabyVisual.prefab"));
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
            Scene sourceScene = EditorSceneManager.OpenScene(source);
            if (!EditorSceneManager.SaveScene(sourceScene, target, true))
            { Debug.LogError("Dravio: could not copy scene."); return; }
            Scene preview = EditorSceneManager.OpenScene(target);
            var turntable = Object.FindFirstObjectByType<DragonTurntable>();
            if (integrated)
            {
                var marker = GameObject.Find("Clearing - Dragon Spot (marker)");
                var locations = AssetDatabase.LoadAssetAtPath<SanctuaryLocationData>(OriginSanctuaryEnvironmentBuilder.LocationsAssetPath);
                var spawn = locations?.SpawnPointsIn("origin_meadow").FirstOrDefault(p=>locations.IsWalkable(p.Position,"origin_meadow"));
                if ((marker == null && spawn == null) || turntable != null)
                { Debug.LogError("Dravio: expected clean environment scene with safe meadow spawn."); return; }
                var dragon = new GameObject("Emberling - Integrated");
                dragon.transform.position = spawn!=null?spawn.Position:marker.transform.position;
                dragon.transform.rotation = spawn!=null?spawn.Rotation:Quaternion.Euler(0,180,0);
                turntable = dragon.AddComponent<DragonTurntable>();
                if(Camera.main != null)
                {
                    var fit = Camera.main.GetComponent<Dravio.EnvironmentArt.SanctuaryCameraFit>();
                    if(fit != null) fit.enabled = playable; // playable camera adapts to the right rail
                }
            }
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
            root.name = sourceGlb ? "Emberling - Integrated GLB" : integrated ? "Emberling - Integrated" : refined ? "Emberling - Refined Baby Normal" : polish ? "Emberling - Polished Baby Normal" : care ? "Emberling - Care Baby Normal" : wander ? "Emberling - Wandering Baby Normal" : living ? "Emberling - Living Baby Normal" : "Emberling - Imported Baby Normal";
            turntable.SetCamera(Camera.main);
            if (care)
            {
                var animator = visual.GetComponentInChildren<Animator>(true);
                if (fantasy) FantasyPreviewHud.Build(root.gameObject, animator, turntable, integrated, playable);
                else CarePreviewHud.Build(root.gameObject, animator, turntable);
                if (polish)
                {
                    try { EmberlingPolishBuilder.Apply(root.gameObject, visual, Camera.main, sourceGlb); }
                    catch (Exception error)
                    {
                        Debug.LogError("Dravio: polish generation incomplete. Source scene is intact. Rename the incomplete preview before retrying.");
                        Debug.LogException(error);
                        return;
                    }
                }
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

        static void ConfigureIdle(GameObject instance, string assetPath, bool wander, bool care, bool polish, bool refined, bool sourceGlb = false)
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
            string path = AssetDatabase.GenerateUniqueAssetPath(Prefabs + (sourceGlb ? "/EmberlingRefinedGLB.controller" : refined ? "/EmberlingRefined.controller" : polish ? "/EmberlingPolished.controller" : care ? "/EmberlingCare.controller" : wander ? "/EmberlingWander.controller" : "/EmberlingIdle.controller"));
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

        // Preserve the actual imported material or an artist's manual remap.
        // The FBX format may not carry every glTF PBR semantic. The Blender
        // PBR sidecar is the lossless reference for manual URP channel remapping.
        static void ConvertImportedGlbMaterials(Renderer[] renderers, Shader shader)
        {
            var converted = new System.Collections.Generic.Dictionary<Material, Material>();
            foreach (var renderer in renderers)
            {
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    var src = slots[i];
                    if (src == null || src.shader == shader) continue;
                    if (!converted.TryGetValue(src, out var dst))
                    {
                        dst = new Material(shader) { name = src.name + " GLB URP" };
                        if (src.HasProperty("_BaseColor")) dst.SetColor("_BaseColor", src.GetColor("_BaseColor"));
                        else if (src.HasProperty("_Color")) dst.SetColor("_BaseColor", src.GetColor("_Color"));
                        CopyMap(src, dst, "_BaseMap", "_BaseMap", "_MainTex");
                        CopyMap(src, dst, "_BumpMap", "_BumpMap");
                        CopyMap(src, dst, "_MetallicGlossMap", "_MetallicGlossMap");
                        CopyMap(src, dst, "_OcclusionMap", "_OcclusionMap");
                        CopyMap(src, dst, "_EmissionMap", "_EmissionMap");
                        foreach (var property in new[]{"_Metallic","_Smoothness","_BumpScale","_OcclusionStrength","_Cutoff"})
                            if (src.HasProperty(property) && dst.HasProperty(property)) dst.SetFloat(property,src.GetFloat(property));
                        if (!src.HasProperty("_Smoothness") && src.HasProperty("_Glossiness"))
                            dst.SetFloat("_Smoothness",src.GetFloat("_Glossiness"));
                        if (src.HasProperty("_EmissionColor")) dst.SetColor("_EmissionColor",src.GetColor("_EmissionColor"));
                        if (dst.GetTexture("_BumpMap") != null) dst.EnableKeyword("_NORMALMAP");
                        if (dst.GetTexture("_MetallicGlossMap") != null) dst.EnableKeyword("_METALLICSPECGLOSSMAP");
                        if (dst.GetTexture("_EmissionMap") != null ||
                            dst.GetColor("_EmissionColor").maxColorComponent > 0f) dst.EnableKeyword("_EMISSION");
                        AssetDatabase.CreateAsset(dst, AssetDatabase.GenerateUniqueAssetPath(Materials + "/EmberlingGLBMaterial.mat"));
                        converted.Add(src,dst);
                    }
                    slots[i] = dst;
                }
                renderer.sharedMaterials = slots;
            }
        }

        static void CopyMap(Material src, Material dst, string destination, params string[] sources)
        {
            foreach (string property in sources)
            {
                if (!src.HasProperty(property)) continue;
                var texture = src.GetTexture(property);
                if (texture == null) continue;
                dst.SetTexture(destination,texture);
                dst.SetTextureOffset(destination,src.GetTextureOffset(property));
                dst.SetTextureScale(destination,src.GetTextureScale(property));
                return;
            }
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
