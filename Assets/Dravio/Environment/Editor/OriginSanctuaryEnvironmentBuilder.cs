using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Dravio.EnvironmentArt.EditorTools
{
    /// <summary>
    /// U1-ENV editor tools: material setup, prefab generation and a SEPARATE
    /// Origin Sanctuary environment scene. Never opens, modifies or overwrites the
    /// existing test scenes (OriginMeadow, AncientSanctuary, Emberling previews), the
    /// Emberling assets, the Animator or any gameplay code.
    /// </summary>
    public static class OriginSanctuaryEnvironmentBuilder
    {
        const string Root = "Assets/Dravio/Environment";
        const string Models = Root + "/Models";
        const string Textures = Root + "/Textures";
        const string MaterialsDir = Root + "/Materials";
        const string PrefabsDir = Root + "/Prefabs";
        const string ScenesDir = Root + "/Scenes";
        const string LayoutPath = Root + "/Data/OriginSanctuaryLayout.json";
        public const string ScenePath = ScenesDir + "/OriginSanctuary_Environment.unity";

        const string PainterlyShader = "Dravio/Environment/Painterly Lit";
        const string WaterShader = "Dravio/Environment/Painterly Water";

        static readonly string[] SlotNames = { "ENV_Props", "ENV_Foliage", "ENV_Water", "ENV_Ground" };

        // Small things that never need to cast shadows (cheaper shadow pass on mobile).
        static readonly string[] NoShadowPrefixes =
        {
            "SM_ENV_Grass", "SM_ENV_Flower", "SM_ENV_Fern", "SM_ENV_Mushroom",
            "SM_ENV_Rock_Mossy_C", "SM_ENV_Pond", "SM_ENV_Ground", "SM_ENV_Vine",
        };

        // ------------------------------------------------------------------ menu
        [MenuItem("Dravio/Environment/Build Everything (Materials, Prefabs, Scene)", priority = 100)]
        public static void BuildEverything()
        {
            if (!SetupMaterials()) return;
            if (!BuildPrefabs()) return;
            BuildScene(false);
        }

        [MenuItem("Dravio/Environment/1. Setup Materials and Import Remaps", priority = 111)]
        public static void SetupMaterialsMenu() => SetupMaterials();

        [MenuItem("Dravio/Environment/2. Build Prefabs", priority = 112)]
        public static void BuildPrefabsMenu() => BuildPrefabs();

        [MenuItem("Dravio/Environment/3. Create or Open Origin Sanctuary Scene", priority = 113)]
        public static void BuildSceneMenu() => BuildScene(false);

        [MenuItem("Dravio/Environment/Rebuild Origin Sanctuary Scene (overwrite)", priority = 130)]
        public static void RebuildSceneMenu()
        {
            if (!EditorUtility.DisplayDialog("Dravio Environment",
                    "Overwrite " + ScenePath + " from the layout data?\nOnly this environment scene is replaced; " +
                    "local edits made in it will be lost.", "Overwrite", "Cancel")) return;
            BuildScene(true);
        }

        [MenuItem("Dravio/Environment/Report Mobile Budget (open scene)", priority = 140)]
        public static void ReportBudgetMenu() => ReportBudget(true);

        // ------------------------------------------------------------- materials
        public static bool SetupMaterials()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return false;
            EnsureFolder(MaterialsDir);

            var atlas = LoadTexture("T_ENV_PropsAtlas");
            var emission = LoadTexture("T_ENV_PropsEmission");
            var foliage = LoadTexture("T_ENV_Foliage");
            var ground = LoadTexture("T_ENV_Ground");
            var water = LoadTexture("T_ENV_Water");
            if (atlas == null || foliage == null || ground == null || water == null)
            {
                Debug.LogError("Dravio Environment: textures missing in " + Textures +
                               ". Run scripts/art/environment/build_origin_sanctuary.py with --unity first.");
                return false;
            }

            Shader painterly = ResolveShader(PainterlyShader, "Universal Render Pipeline/Lit", out bool painterlyOk);
            Shader waterShader = ResolveShader(WaterShader, "Universal Render Pipeline/Lit", out bool waterOk);
            if (painterly == null || waterShader == null)
            {
                Debug.LogError("Dravio Environment: neither the Dravio shaders nor URP/Lit could be found.");
                return false;
            }

            var mats = new Dictionary<string, Material>
            {
                ["ENV_Props"] = GetOrCreateMaterial("M_ENV_Props", painterly),
                ["ENV_Foliage"] = GetOrCreateMaterial("M_ENV_Foliage", painterly),
                ["ENV_Ground"] = GetOrCreateMaterial("M_ENV_Ground", painterly),
                ["ENV_Water"] = GetOrCreateMaterial("M_ENV_Water", waterShader),
            };

            if (painterlyOk)
            {
                ConfigurePainterly(mats["ENV_Props"], atlas, emission, wind: 0.05f, path: 0f, clip: false);
                mats["ENV_Props"].SetColor("_EmissionColor", new Color(1.7f, 1.7f, 1.7f, 1f));
                ConfigurePainterly(mats["ENV_Foliage"], foliage, null, wind: 0.11f, path: 0f, clip: true);
                ConfigurePainterly(mats["ENV_Ground"], ground, null, wind: 0f, path: 1f, clip: false);
                mats["ENV_Ground"].SetFloat("_RimStrength", 0f);
            }
            else
            {
                ConfigureLitFallback(mats["ENV_Props"], atlas, emission, false, false);
                ConfigureLitFallback(mats["ENV_Foliage"], foliage, null, true, false);
                ConfigureLitFallback(mats["ENV_Ground"], ground, null, false, false);
            }
            if (waterOk)
            {
                mats["ENV_Water"].SetTexture("_BaseMap", water);
                mats["ENV_Water"].renderQueue = (int)RenderQueue.Transparent;
            }
            else ConfigureLitFallback(mats["ENV_Water"], water, null, false, true);

            foreach (var m in mats.Values) EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssets();

            // Bind the FBX embedded material names to the project materials.
            int remapped = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Models }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not ModelImporter importer) continue;
                bool changed = false;
                foreach (var pair in mats)
                {
                    var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key);
                    importer.GetExternalObjectMap().TryGetValue(id, out var current);
                    if (current == pair.Value) continue;
                    importer.AddRemap(id, pair.Value);
                    changed = true;
                }
                if (changed)
                {
                    importer.SaveAndReimport();
                    remapped++;
                }
            }
            Debug.Log($"Dravio Environment: materials ready ({(painterlyOk ? "painterly shader" : "URP/Lit FALLBACK - check shader errors")}), " +
                      $"{remapped} model(s) remapped.");
            return true;
        }

        static void ConfigurePainterly(Material m, Texture2D baseMap, Texture2D emission, float wind, float path, bool clip)
        {
            m.SetTexture("_BaseMap", baseMap);
            m.SetTexture("_EmissionMap", emission != null ? emission : Texture2D.blackTexture);
            if (emission == null) m.SetColor("_EmissionColor", Color.black);
            m.SetFloat("_WindStrength", wind);
            m.SetFloat("_PathBlend", path);
            m.SetFloat("_AlphaClip", clip ? 1f : 0f);
            m.SetFloat("_Cull", clip ? (float)CullMode.Off : (float)CullMode.Back);
            if (clip)
            {
                m.EnableKeyword("_ALPHATEST_ON");
                m.SetOverrideTag("RenderType", "TransparentCutout");
                m.renderQueue = (int)RenderQueue.AlphaTest;
            }
            else
            {
                m.DisableKeyword("_ALPHATEST_ON");
                m.SetOverrideTag("RenderType", "Opaque");
                m.renderQueue = (int)RenderQueue.Geometry;
            }
            m.enableInstancing = true;
        }

        static void ConfigureLitFallback(Material m, Texture2D baseMap, Texture2D emission, bool clip, bool transparent)
        {
            m.SetTexture("_BaseMap", baseMap);
            m.SetFloat("_Smoothness", transparent ? 0.7f : 0.08f);
            m.SetFloat("_SpecularHighlights", 0f);
            m.SetFloat("_EnvironmentReflections", transparent ? 1f : 0f);
            if (emission != null)
            {
                m.SetTexture("_EmissionMap", emission);
                m.SetColor("_EmissionColor", Color.white * 1.5f);
                m.EnableKeyword("_EMISSION");
            }
            if (clip)
            {
                m.SetFloat("_AlphaClip", 1f);
                m.SetFloat("_Cutoff", 0.45f);
                m.SetFloat("_Cull", (float)CullMode.Off);
                m.EnableKeyword("_ALPHATEST_ON");
                m.renderQueue = (int)RenderQueue.AlphaTest;
            }
            if (transparent)
            {
                m.SetFloat("_Surface", 1f);
                m.SetColor("_BaseColor", new Color(0.55f, 0.85f, 0.85f, 0.75f));
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = (int)RenderQueue.Transparent;
            }
        }

        // --------------------------------------------------------------- prefabs
        public static bool BuildPrefabs()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return false;
            EnsureFolder(PrefabsDir);
            var known = new Dictionary<string, Material>();
            foreach (string slot in SlotNames)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialsDir + "/M_" + slot + ".mat");
                if (mat != null) known[slot] = mat;
            }
            if (known.Count != SlotNames.Length)
            {
                Debug.LogError("Dravio Environment: run '1. Setup Materials and Import Remaps' first.");
                return false;
            }

            var guids = AssetDatabase.FindAssets("t:Model", new[] { Models });
            if (guids.Length == 0)
            {
                Debug.LogError("Dravio Environment: no FBX found in " + Models);
                return false;
            }
            int built = 0;
            var problems = new List<string>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null) continue;
                string name = Path.GetFileNameWithoutExtension(path);
                string prefabPath = PrefabsDir + "/" + name + ".prefab";
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                try
                {
                    instance.name = name;
                    instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    instance.transform.localScale = Vector3.one;
                    bool castShadows = !NoShadowPrefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal));
                    foreach (var r in instance.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        var mats = r.sharedMaterials;
                        for (int i = 0; i < mats.Length; i++)
                        {
                            string key = mats[i] == null ? null : mats[i].name.Replace(" (Instance)", "");
                            if (key != null && key.StartsWith("M_", StringComparison.Ordinal)) key = key.Substring(2);
                            if (key != null && known.TryGetValue(key, out var mat)) mats[i] = mat;
                            else problems.Add($"{name}: material slot {i} '{key}' not recognised");
                        }
                        r.sharedMaterials = mats;
                        r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                        r.receiveShadows = true;
                        r.lightProbeUsage = LightProbeUsage.Off;
                        r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                        r.allowOcclusionWhenDynamic = false;
                    }
                    foreach (var t in instance.GetComponentsInChildren<Transform>(true))
                        GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
                    var saved = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath, out bool ok);
                    if (!ok || saved == null) problems.Add(name + ": prefab save failed");
                    else built++;
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }
            AssetDatabase.SaveAssets();
            foreach (var p in problems) Debug.LogWarning("Dravio Environment: " + p);
            Debug.Log($"Dravio Environment: {built} prefab(s) written to {PrefabsDir}" +
                      (problems.Count > 0 ? $" with {problems.Count} warning(s)." : "."));
            return built > 0;
        }

        // ----------------------------------------------------------------- scene
        [Serializable] class LayoutCamera { public float[] position; public float[] lookAt; public float orthographicSize; public float minVisibleWidth; }
        [Serializable] class LayoutClearing { public float x; public float z; public float radius; }
        [Serializable] class LayoutPlacement { public string asset; public float x; public float y; public float z; public float yaw; public float scale; }
        [Serializable] class LayoutData { public int version; public int seed; public LayoutCamera camera; public LayoutClearing clearing; public LayoutPlacement[] placements; }

        public static void BuildScene(bool overwrite)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!overwrite && AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                EditorSceneManager.OpenScene(ScenePath);
                Debug.Log("Dravio Environment: opened existing " + ScenePath + " (use 'Rebuild ... (overwrite)' to regenerate).");
                return;
            }
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(LayoutPath);
            if (json == null) { Debug.LogError("Dravio Environment: layout missing at " + LayoutPath); return; }
            var layout = JsonUtility.FromJson<LayoutData>(json.text);
            if (layout?.placements == null || layout.camera?.position == null || layout.camera.position.Length < 3)
            { Debug.LogError("Dravio Environment: layout JSON could not be parsed."); return; }

            EnsureFolder(ScenesDir);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Lighting: warm key light, cool painted ambient, soft distance fog.
            Color sky = new Color(0.70f, 0.80f, 0.66f);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.80f, 0.86f, 0.95f);
            RenderSettings.ambientEquatorColor = new Color(0.66f, 0.72f, 0.58f);
            RenderSettings.ambientGroundColor = new Color(0.40f, 0.36f, 0.30f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = sky;
            RenderSettings.fogStartDistance = 24f;
            RenderSettings.fogEndDistance = 52f;

            var sunGo = new GameObject("Warm Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.92f, 0.78f);
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;
            sunGo.transform.rotation = Quaternion.Euler(46f, -38f, 0f);

            var camGo = new GameObject("Fixed Ortho Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = layout.camera.orthographicSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = sky;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 80f;
            camGo.transform.position = V(layout.camera.position);
            camGo.transform.LookAt(V(layout.camera.lookAt));
            var fit = camGo.AddComponent<SanctuaryCameraFit>();
            fit.baseOrthographicSize = layout.camera.orthographicSize;
            fit.minVisibleWidth = layout.camera.minVisibleWidth;

            var envRoot = new GameObject("Origin Sanctuary - Environment").transform;
            var groups = new Dictionary<string, Transform>();
            var missing = new HashSet<string>();
            int placed = 0;
            foreach (var p in layout.placements)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsDir + "/" + p.asset + ".prefab");
                if (prefab == null) { missing.Add(p.asset); continue; }
                string group = GroupFor(p.asset);
                if (!groups.TryGetValue(group, out var parent))
                {
                    parent = new GameObject(group).transform;
                    parent.SetParent(envRoot, false);
                    GameObjectUtility.SetStaticEditorFlags(parent.gameObject, StaticEditorFlags.BatchingStatic);
                    groups[group] = parent;
                }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                go.transform.localPosition = new Vector3(p.x, p.y, p.z);
                go.transform.localRotation = Quaternion.Euler(0f, p.yaw, 0f);
                go.transform.localScale = Vector3.one * (p.scale <= 0f ? 1f : p.scale);
                placed++;
            }
            GameObjectUtility.SetStaticEditorFlags(envRoot.gameObject, StaticEditorFlags.BatchingStatic);

            // Marker only (no component): where Emberling / gameplay will stand later.
            var marker = new GameObject("Clearing - Dragon Spot (marker)");
            marker.transform.position = new Vector3(layout.clearing?.x ?? 0f, 0f, layout.clearing?.z ?? 0f);

            foreach (var m in missing)
                Debug.LogWarning("Dravio Environment: prefab not found for " + m + " (run '2. Build Prefabs').");
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                Debug.LogError("Dravio Environment: could not save " + ScenePath);
                return;
            }
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = envRoot.gameObject;
            Debug.Log($"Dravio Environment: {placed} placements -> {ScenePath}. Existing test scenes untouched.");
            ReportBudget(false);
        }

        static string GroupFor(string asset)
        {
            if (asset.Contains("Ground")) return "Ground";
            if (asset.Contains("Gate") || asset.Contains("Column") || asset.Contains("Wall") || asset.Contains("Statue")) return "Ruins";
            if (asset.Contains("Tree") || asset.Contains("Root") || asset.Contains("Vine")) return "Ancient Trees";
            if (asset.Contains("Pond")) return "Water";
            if (asset.Contains("Runestone")) return "Rune Stones";
            if (asset.Contains("Rock")) return "Mossy Rocks";
            if (asset.Contains("Mushroom")) return "Mushrooms";
            return "Grass and Flowers";
        }

        // ---------------------------------------------------------------- budget
        public static void ReportBudget(bool dialog)
        {
            const int TriangleBudget = 60000;
            const int MaterialBudget = 6;
            var renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            long tris = 0;
            var materials = new HashSet<Material>();
            var textures = new HashSet<Texture>();
            int staticCount = 0, shadowCasters = 0;
            foreach (var r in renderers)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                    for (int s = 0; s < mf.sharedMesh.subMeshCount; s++)
                        tris += mf.sharedMesh.GetIndexCount(s) / 3;
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    materials.Add(m);
                    foreach (string prop in m.GetTexturePropertyNames())
                    {
                        var t = m.GetTexture(prop);
                        if (t != null && t != Texture2D.blackTexture && t != Texture2D.whiteTexture) textures.Add(t);
                    }
                }
                if (GameObjectUtility.AreStaticEditorFlagsSet(r.gameObject, StaticEditorFlags.BatchingStatic)) staticCount++;
                if (r.shadowCastingMode != ShadowCastingMode.Off) shadowCasters++;
            }
            long texBytes = textures.Sum(t => UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t));
            var sb = new StringBuilder();
            sb.AppendLine($"Renderers: {renderers.Length} ({staticCount} batching-static, {shadowCasters} shadow casters)");
            sb.AppendLine($"Triangles: {tris:N0} / budget {TriangleBudget:N0} {(tris <= TriangleBudget ? "OK" : "OVER")}");
            sb.AppendLine($"Unique materials: {materials.Count} / budget {MaterialBudget} {(materials.Count <= MaterialBudget ? "OK" : "OVER")}");
            sb.AppendLine($"Textures: {textures.Count}, editor memory ~{texBytes / 1024f / 1024f:F1} MB (mobile ASTC 6x6 is smaller)");
            sb.AppendLine("Draw calls: static batching + SRP Batcher merge same-material renderers; check Frame Debugger in Play Mode.");
            Debug.Log("Dravio Environment budget\n" + sb);
            if (dialog) EditorUtility.DisplayDialog("Dravio Environment budget", sb.ToString(), "OK");
        }

        // --------------------------------------------------------------- helpers
        static Vector3 V(float[] a) => a != null && a.Length >= 3 ? new Vector3(a[0], a[1], a[2]) : Vector3.zero;

        static Texture2D LoadTexture(string name) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>(Textures + "/" + name + ".png");

        static Shader ResolveShader(string preferred, string fallback, out bool usedPreferred)
        {
            var s = Shader.Find(preferred);
            usedPreferred = s != null && s.isSupported && !ShaderUtil.ShaderHasError(s);
            if (usedPreferred) return s;
            Debug.LogWarning($"Dravio Environment: shader '{preferred}' unavailable or has errors; using '{fallback}'.");
            return Shader.Find(fallback);
        }

        static Material GetOrCreateMaterial(string name, Shader shader)
        {
            string path = MaterialsDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader)
            {
                m.shader = shader;
            }
            return m;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
