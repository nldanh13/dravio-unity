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
    /// U1-ENV.4 editor tools for the Ocean (Thủy Vực) and Sky (Thiên Giới) environment kits.
    /// Each zone gets its own materials, prefabs, data assets and a SEPARATE scene
    /// (Scenes/Ocean_Environment.unity, Scenes/Sky_Environment.unity). The Origin Sanctuary
    /// scene, its folders and the test scenes are never opened for writing.
    ///
    /// Reuses the Origin systems as they are: SanctuaryCameraFit (fixed landscape camera,
    /// Fantasy UI and Safe Area insets), the painterly shaders, the import rules, the
    /// living environment (time of day, wind, particles, ambience, interactions,
    /// EnvironmentQuality tiers) and the location data / marker components.
    /// Nothing here touches GameState, unlocks or travel: gates are data and markers only.
    /// </summary>
    public static class ZoneEnvironmentBuilder
    {
        const string Root = "Assets/Dravio/Environment";
        const string OriginTextures = Root + "/Textures";
        const string OriginAudio = Root + "/Audio";
        const int TriangleBudget = 60000;
        const int MaterialBudget = 10;  // 3-4 zone materials + particle / FX materials (8 expected)
        const long TextureBudgetBytes = 6L * 1024 * 1024; // editor-side estimate (uncompressed RGBA32 + mips)

        public sealed class Zone
        {
            public readonly string id, prefix, nameVi;
            public readonly string[] canonicalHabitats;
            public readonly bool perchAllowed;
            public readonly string worldEdge, gatewayHere, gatewayOrigin;
            public readonly string[] noShadowPrefixes;

            public Zone(string id, string prefix, string nameVi, string[] habitats, bool perch, string edge, string here,
                        string origin, string[] noShadow)
            {
                this.id = id; this.prefix = prefix; this.nameVi = nameVi; canonicalHabitats = habitats; perchAllowed = perch;
                worldEdge = edge; gatewayHere = here; gatewayOrigin = origin; noShadowPrefixes = noShadow;
            }

            public string Dir => Root + "/Zones/" + prefix;
            public string Models => Dir + "/Models";
            public string Textures => Dir + "/Textures";
            public string Data => Dir + "/Data";
            public string Audio => Dir + "/Audio";
            public string Materials => Dir + "/Materials";
            public string Prefabs => Dir + "/Prefabs";
            public string ScenePath => OriginSanctuaryEnvironmentBuilder.ScenesDir + "/" + prefix + "_Environment.unity";
            public string LayoutJson => Data + "/" + prefix + "Layout.json";
            public string LocationsJson => Data + "/" + prefix + "Locations.json";
            public string CollidersJson => Data + "/" + prefix + "Colliders.json";
            public string LivingJson => Data + "/" + prefix + "Living.json";
            public string LocationsAsset => Data + "/" + prefix + "Locations.asset";
            public string LivingAsset => Data + "/" + prefix + "Living.asset";
            public string Slot(string s) => "ENV_" + prefix + "_" + s;
            public string RootName => prefix + " - Environment";
        }

        // Canonical facts mirrored from the domain port (scripts/art/environment/canonical_world.py).
        public static readonly Zone Ocean = new Zone("ocean", "Ocean", "Thủy Vực",
            new[] { "ocean_estuary", "ocean_reef", "ocean_cool_pool" }, perch: false,
            edge: "origin_ocean_estuary", here: "ocean_estuary", origin: "origin_pondbank",
            noShadow: new[] { "SM_ENV_Ocean_Cover", "SM_ENV_Ocean_Seabed", "SM_ENV_Ocean_Shell", "SM_ENV_Ocean_Sea_Fan" });

        public static readonly Zone Sky = new Zone("sky", "Sky", "Thiên Giới",
            new[] { "sky_low_island" }, perch: true,
            edge: "origin_sky_low_island", here: "sky_low_island", origin: "origin_meadow",
            noShadow: new[] { "SM_ENV_Sky_Cover", "SM_ENV_Sky_Cloud", "SM_ENV_Sky_Islet_Float" });

        // ------------------------------------------------------------------ menu
        [MenuItem("Dravio/Environment/Zones/Ocean - Build Everything (Thủy Vực)", priority = 200)]
        public static void BuildOceanMenu() => BuildEverything(Ocean, false);

        [MenuItem("Dravio/Environment/Zones/Sky - Build Everything (Thiên Giới)", priority = 201)]
        public static void BuildSkyMenu() => BuildEverything(Sky, false);

        [MenuItem("Dravio/Environment/Zones/Rebuild Ocean Scene (overwrite)", priority = 210)]
        public static void RebuildOceanMenu() => RebuildWithConfirm(Ocean);

        [MenuItem("Dravio/Environment/Zones/Rebuild Sky Scene (overwrite)", priority = 211)]
        public static void RebuildSkyMenu() => RebuildWithConfirm(Sky);

        [MenuItem("Dravio/Environment/Zones/Validate Ocean and Sky (budget, locations, framing)", priority = 220)]
        public static void ValidateMenu()
        {
            var sb = new StringBuilder();
            bool ok = Validate(Ocean, sb) & Validate(Sky, sb);
            Debug.Log("Dravio Environment zones: " + (ok ? "OK" : "problems found") + "\n" + sb);
            EditorUtility.DisplayDialog("Dravio Environment - Ocean / Sky " + (ok ? "OK" : "problems"), sb.ToString(), "OK");
        }

        static void RebuildWithConfirm(Zone z)
        {
            if (!EditorUtility.DisplayDialog("Dravio Environment",
                    "Overwrite " + z.ScenePath + " from the zone data?\nOnly this zone scene is replaced; the Origin " +
                    "Sanctuary and test scenes are not touched.", "Overwrite", "Cancel")) return;
            BuildEverything(z, true);
        }

        public static bool BuildEverything(Zone z, bool overwriteScene)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return false;
            if (!SetupMaterials(z)) return false;
            if (!BuildPrefabs(z)) return false;
            var loc = UpdateLocationData(z);
            var living = UpdateLivingData(z);
            if (loc == null || living == null) return false;
            if (!BuildScene(z, loc, living, overwriteScene)) return false;
            var sb = new StringBuilder();
            bool ok = Validate(z, sb);
            Debug.Log($"Dravio Environment {z.prefix}: " + (ok ? "validation OK" : "validation found problems") + "\n" + sb);
            return true;
        }

        // ------------------------------------------------------------- materials
        static Texture2D Tex(string dir, string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/" + name + ".png");

        public static bool SetupMaterials(Zone z)
        {
            string P = "T_ENV_" + z.prefix + "_";
            var atlas = Tex(z.Textures, P + "PropsAtlas");
            var emission = Tex(z.Textures, P + "PropsEmission");
            var foliage = Tex(z.Textures, P + "Foliage");
            var ground = Tex(z.Textures, P + "Ground");
            var water = Tex(z.Textures, P + "Water");
            if (atlas == null || foliage == null || ground == null)
            {
                Debug.LogError($"Dravio Environment: {z.prefix} textures missing in {z.Textures}. Run " +
                               "scripts/art/environment/build_zone_kits.py with --unity first.");
                return false;
            }
            Shader painterly = OriginSanctuaryEnvironmentBuilder.ResolveShader(OriginSanctuaryEnvironmentBuilder.PainterlyShader,
                "Universal Render Pipeline/Lit", out bool painterlyOk);
            Shader waterShader = OriginSanctuaryEnvironmentBuilder.ResolveShader(OriginSanctuaryEnvironmentBuilder.WaterShader,
                "Universal Render Pipeline/Lit", out bool waterOk);
            if (painterly == null || waterShader == null) return false;

            var mats = new Dictionary<string, Material>
            {
                [z.Slot("Props")] = OriginSanctuaryEnvironmentBuilder.GetOrCreateMaterial("M_" + z.Slot("Props"), painterly, z.Materials),
                [z.Slot("Foliage")] = OriginSanctuaryEnvironmentBuilder.GetOrCreateMaterial("M_" + z.Slot("Foliage"), painterly, z.Materials),
                [z.Slot("Ground")] = OriginSanctuaryEnvironmentBuilder.GetOrCreateMaterial("M_" + z.Slot("Ground"), painterly, z.Materials),
            };
            if (water != null)
                mats[z.Slot("Water")] = OriginSanctuaryEnvironmentBuilder.GetOrCreateMaterial("M_" + z.Slot("Water"), waterShader, z.Materials);

            bool ocean = z == Ocean;
            var props = mats[z.Slot("Props")];
            var fol = mats[z.Slot("Foliage")];
            var gr = mats[z.Slot("Ground")];
            if (painterlyOk)
            {
                // Kelp and seagrass sway in the current; sky grass in the high wind.
                OriginSanctuaryEnvironmentBuilder.ConfigurePainterly(props, atlas, emission, wind: 0.05f, path: 0f, clip: false);
                // ENV Alpha (#79): 1.7 clipped crystals / anemones / glyphs to white in the Game view.
                props.SetColor("_EmissionColor", ocean ? new Color(1.2f, 1.2f, 1.2f, 1f) : new Color(1.3f, 1.3f, 1.3f, 1f));
                OriginSanctuaryEnvironmentBuilder.ConfigurePainterly(fol, foliage, null, wind: ocean ? 0.16f : 0.13f, path: 0f, clip: true);
                OriginSanctuaryEnvironmentBuilder.ConfigurePainterly(gr, ground, null, wind: 0f, path: 1f, clip: false);
                gr.SetFloat("_RimStrength", 0f);
                gr.SetColor("_PathColor", ocean ? new Color(0.93f, 0.86f, 0.66f) : new Color(0.86f, 0.82f, 0.74f));
                foreach (var m in new[] { props, fol, gr })
                {
                    m.SetColor("_ShadowTint", ocean ? new Color(0.42f, 0.60f, 0.76f) : new Color(0.66f, 0.70f, 0.92f));
                    m.SetColor("_RimColor", ocean ? new Color(0.68f, 0.95f, 1f) : new Color(1f, 0.95f, 0.86f));
                }
                if (ocean) props.SetFloat("_RimStrength", 0.18f);
            }
            else
            {
                OriginSanctuaryEnvironmentBuilder.ConfigureLitFallback(props, atlas, emission, false, false);
                OriginSanctuaryEnvironmentBuilder.ConfigureLitFallback(fol, foliage, null, true, false);
                OriginSanctuaryEnvironmentBuilder.ConfigureLitFallback(gr, ground, null, false, false);
            }
            if (water != null)
            {
                var w = mats[z.Slot("Water")];
                if (waterOk)
                {
                    // Sky: the water shader drawn as a slowly drifting sea of clouds.
                    // ENV Alpha (#79): near-white shallow / foam blew out; clouds now stay below white and the
                    // deep tone carries more blue, so the cloud relief (texture) reads against the islands.
                    w.SetTexture("_BaseMap", water);
                    w.SetColor("_ShallowColor", new Color(0.90f, 0.92f, 0.98f));
                    w.SetColor("_DeepColor", new Color(0.60f, 0.67f, 0.88f));
                    w.SetColor("_FoamColor", new Color(0.95f, 0.96f, 1f));
                    w.SetFloat("_FoamWidth", 0.16f);
                    w.SetFloat("_Opacity", 0.96f);
                    w.SetFloat("_ShoreOpacity", 0f);
                    w.SetFloat("_SparkleStrength", 0.04f);
                    w.SetVector("_ScrollA", new Vector4(0.006f, 0.002f, 0f, 0f));
                    w.SetVector("_ScrollB", new Vector4(-0.004f, 0.003f, 0f, 0f));
                    w.renderQueue = (int)RenderQueue.Transparent;
                }
                else OriginSanctuaryEnvironmentBuilder.ConfigureLitFallback(w, water, null, false, true);
            }
            foreach (var m in mats.Values) EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssets();

            int remapped = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { z.Models }))
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
                if (changed) { importer.SaveAndReimport(); remapped++; }
            }
            Debug.Log($"Dravio Environment {z.prefix}: {mats.Count} materials ready" +
                      $"{(painterlyOk ? "" : " (URP/Lit FALLBACK)")}, {remapped} model(s) remapped.");
            return true;
        }

        // --------------------------------------------------------------- prefabs
        public static bool BuildPrefabs(Zone z)
        {
            OriginSanctuaryEnvironmentBuilder.EnsureFolder(z.Prefabs);
            var known = new Dictionary<string, Material>();
            foreach (string s in new[] { "Props", "Foliage", "Ground", "Water" })
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(z.Materials + "/M_" + z.Slot(s) + ".mat");
                if (m != null) known[z.Slot(s)] = m;
            }
            if (known.Count < 3) { Debug.LogError($"Dravio Environment {z.prefix}: set up materials first."); return false; }
            var guids = AssetDatabase.FindAssets("t:Model", new[] { z.Models });
            if (guids.Length == 0) { Debug.LogError("Dravio Environment: no FBX found in " + z.Models); return false; }
            var colliders = OriginSanctuaryEnvironmentBuilder.LoadColliderSpecs(z.CollidersJson);
            int built = 0;
            var problems = new List<string>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null) continue;
                string name = Path.GetFileNameWithoutExtension(path);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                try
                {
                    instance.name = name;
                    instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    instance.transform.localScale = Vector3.one;
                    bool castShadows = !z.noShadowPrefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal));
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
                    if (colliders.TryGetValue(name, out var boxes)) OriginSanctuaryEnvironmentBuilder.AddColliders(instance, boxes);
                    foreach (var t in instance.GetComponentsInChildren<Transform>(true))
                        GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
                    var saved = PrefabUtility.SaveAsPrefabAsset(instance, z.Prefabs + "/" + name + ".prefab", out bool ok);
                    if (!ok || saved == null) problems.Add(name + ": prefab save failed"); else built++;
                }
                finally { Object.DestroyImmediate(instance); }
            }
            AssetDatabase.SaveAssets();
            foreach (var p in problems) Debug.LogWarning("Dravio Environment: " + p);
            Debug.Log($"Dravio Environment {z.prefix}: {built} prefab(s) -> {z.Prefabs}");
            return built > 0;
        }

        // ------------------------------------------------------------------ data
        public static SanctuaryLocationData UpdateLocationData(Zone z) =>
            UpdateAsset<SanctuaryLocationData>(z.LocationsJson, z.LocationsAsset, (a, j) => a.LoadFromJson(j),
                a => a.habitats != null && a.habitats.Length > 0);

        public static SanctuaryLivingData UpdateLivingData(Zone z) =>
            UpdateAsset<SanctuaryLivingData>(z.LivingJson, z.LivingAsset, (a, j) => a.LoadFromJson(j),
                a => a.keyframes != null && a.keyframes.Length >= 2);

        static T UpdateAsset<T>(string jsonPath, string assetPath, Action<T, string> load, Func<T, bool> valid) where T : ScriptableObject
        {
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(jsonPath);
            if (json == null) { Debug.LogError("Dravio Environment: " + jsonPath + " missing. Re-run build_zone_kits.py --unity."); return null; }
            var asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            bool create = asset == null;
            if (create) asset = ScriptableObject.CreateInstance<T>();
            load(asset, json.text);
            if (!valid(asset)) { Debug.LogError("Dravio Environment: " + jsonPath + " has no usable data."); return null; }
            if (create) AssetDatabase.CreateAsset(asset, assetPath);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        // ----------------------------------------------------------------- scene
        public static bool BuildScene(Zone z, SanctuaryLocationData loc, SanctuaryLivingData living, bool overwrite)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            if (z.ScenePath == OriginSanctuaryEnvironmentBuilder.ScenePath) throw new InvalidOperationException("zone scene path collides with Origin");
            if (!overwrite && AssetDatabase.LoadAssetAtPath<SceneAsset>(z.ScenePath) != null)
            {
                EditorSceneManager.OpenScene(z.ScenePath);
                Debug.Log($"Dravio Environment: opened existing {z.ScenePath} (use 'Rebuild {z.prefix} Scene (overwrite)').");
                RefreshArtInOpenScene(z, living);
                return true;
            }
            var layout = OriginSanctuaryEnvironmentBuilder.LoadLayout(z.LayoutJson);
            if (layout == null) return false;
            OriginSanctuaryEnvironmentBuilder.EnsureFolder(OriginSanctuaryEnvironmentBuilder.ScenesDir);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var st = living.EvaluateTimeOfDay(living.defaultHour);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = st.ambientSky;
            RenderSettings.ambientEquatorColor = st.ambientEquator;
            RenderSettings.ambientGroundColor = st.ambientGround;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = st.fogColor;
            RenderSettings.fogStartDistance = living.fog != null && living.fog.end > 0f ? living.fog.start : 24f;
            RenderSettings.fogEndDistance = living.fog != null && living.fog.end > 0f ? living.fog.end : 52f;

            var sunGo = new GameObject(z == Ocean ? "Sun Through Water" : "High Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = st.lightColor;
            sun.intensity = st.lightIntensity;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = st.shadowStrength;
            sunGo.transform.rotation = st.LightRotation;

            var camGo = new GameObject("Fixed Ortho Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = layout.camera.orthographicSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = st.fogColor;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 90f;
            camGo.transform.SetPositionAndRotation(OriginSanctuaryEnvironmentBuilder.V(layout.camera.anchorPosition),
                Quaternion.Euler(OriginSanctuaryEnvironmentBuilder.V(layout.camera.eulerAngles)));
            OriginSanctuaryEnvironmentBuilder.ConfigureCameraFit(camGo.AddComponent<SanctuaryCameraFit>(), layout.camera);

            var envRoot = new GameObject(z.RootName).transform;
            var groups = new Dictionary<string, Transform>();
            var missing = new HashSet<string>();
            int placed = 0;
            foreach (var p in layout.placements)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(z.Prefabs + "/" + p.asset + ".prefab");
                if (prefab == null) { missing.Add(p.asset); continue; }
                string group = string.IsNullOrEmpty(p.habitat) ? "Backdrop and Wide-screen Dressing" : "Habitat " + p.habitat;
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

            var markers = OriginSanctuaryEnvironmentBuilder.AddLocationMarkers(loc, z.prefix + " - Locations (data mirror)");
            AddZoneMarkers(markers.transform, loc);
            BuildFx(z, living, envRoot);

            var livingRoot = LivingEnvironmentBuilder.BuildLivingRoot(z.prefix + " - Living Environment", living, loc,
                new[] { z.Audio, OriginAudio }, (r, d, tod, wind) => BuildZoneParticles(z, r, d, tod, wind), addWater: false);
            var fx = envRoot.GetComponentInChildren<EnvironmentScrollingFx>();
            if (fx != null) fx.timeOfDay = livingRoot.GetComponent<EnvironmentTimeOfDay>();

            foreach (var m in missing) Debug.LogWarning($"Dravio Environment {z.prefix}: prefab missing for {m}.");
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, z.ScenePath)) { Debug.LogError("Dravio Environment: could not save " + z.ScenePath); return false; }
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = envRoot.gameObject;
            Debug.Log($"Dravio Environment: {placed} placements -> {z.ScenePath}. Origin Sanctuary scene untouched.");
            OriginSanctuaryEnvironmentBuilder.ReportBudget(false);
            return true;
        }

        static void AddZoneMarkers(Transform root, SanctuaryLocationData loc)
        {
            var rests = new GameObject("Rest Points").transform;
            rests.SetParent(root, false);
            foreach (var p in loc.restPoints)
                OriginSanctuaryEnvironmentBuilder.Marker(rests, p.id, SanctuaryMarker.Kind.Rest, p.habitatId, null).transform.position = p.Position;
            var links = new GameObject("Area Links").transform;
            links.SetParent(root, false);
            foreach (var l in loc.areaLinks)
                OriginSanctuaryEnvironmentBuilder.Marker(links, l.id, SanctuaryMarker.Kind.AreaLink, l.areaA, l.areaB).transform.position = l.Position;
            var gates = new GameObject("Zone Gates (WorldGraph, locked in G1 - data only)").transform;
            gates.SetParent(root, false);
            foreach (var g in loc.zoneGates)
            {
                var m = OriginSanctuaryEnvironmentBuilder.Marker(gates, g.id, SanctuaryMarker.Kind.ZoneGate, g.habitatId, g.toHabitatId);
                m.transform.position = g.Position;
                var trigger = m.AddComponent<SphereCollider>(); // a trigger only; no script reacts to it here
                trigger.isTrigger = true;
                trigger.radius = g.radius;
            }
        }

        // Caustics and sun shafts (Ocean): additive quads driven by EnvironmentScrollingFx.
        static void BuildFx(Zone z, SanctuaryLivingData living, Transform envRoot)
        {
            if (living.fx == null || living.fx.Length == 0) return;
            var group = new GameObject("Light FX (caustics, sun shafts)");
            group.transform.SetParent(envRoot, false);
            var comp = group.AddComponent<EnvironmentScrollingFx>();
            var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var entries = new List<EnvironmentScrollingFx.Entry>();
            var mats = new Dictionary<string, Material>();
            foreach (var f in living.fx)
            {
                if (!mats.TryGetValue(f.texture, out var mat))
                {
                    string texPath = z.Textures + "/" + f.texture + ".png";
                    string matName = "M_ENV_" + z.prefix + "_" + f.texture.Replace("T_ENV_", "");
                    mat = f.kind == "caustics"
                        ? CausticsMaterial(matName, texPath, z.Materials)
                        : LivingEnvironmentBuilder.ParticleMaterial(matName, texPath, true, z.Materials);
                    mats[f.texture] = mat;
                }
                var go = new GameObject(f.id);
                go.transform.SetParent(group.transform, false);
                go.transform.position = f.Position;
                bool floor = f.kind == "caustics";
                go.transform.rotation = floor ? Quaternion.Euler(90f, f.yaw, 0f) : Quaternion.Euler(0f, f.yaw, f.tilt);
                go.transform.localScale = new Vector3(f.width, f.height, 1f);
                go.AddComponent<MeshFilter>().sharedMesh = quad;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                float tiling = f.tiling > 0f ? f.tiling : 1f;
                entries.Add(new EnvironmentScrollingFx.Entry
                {
                    renderer = r, kind = f.kind, alpha = f.alpha, scroll = f.Scroll,
                    tiling = floor ? new Vector2(tiling, tiling * f.height / Mathf.Max(0.01f, f.width)) : Vector2.one,
                    pulseSeconds = floor ? 0f : 7f,
                });
            }
            comp.entries = entries.ToArray();
            comp.fullLightIntensity = FullLightIntensity(living);
        }

        // Seabed caustics: "Dravio/Environment/Caustics" (photon-traced web, wobble from mip-5 noise,
        // second layer scrolled in the shader). Falls back to the URP particle material if the shader is missing.
        static Material CausticsMaterial(string name, string texturePath, string materialsDir)
        {
            var shader = Shader.Find("Dravio/Environment/Caustics");
            if (shader == null || !shader.isSupported || ShaderUtil.ShaderHasError(shader))
            {
                Debug.LogWarning("Dravio Environment: caustics shader unavailable; using URP particle material for " + name + ".");
                return LivingEnvironmentBuilder.ParticleMaterial(name, texturePath, true, materialsDir);
            }
            OriginSanctuaryEnvironmentBuilder.EnsureFolder(materialsDir);
            string path = materialsDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.shaderKeywords = System.Array.Empty<string>();
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (tex == null) Debug.LogWarning("Dravio Environment: caustics texture missing: " + texturePath);
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_ScaleB", 1.31f);
            m.SetFloat("_RotateB", 0.52f);
            m.SetVector("_ScrollB", new Vector4(-0.015f, 0.011f, 0f, 0f));
            m.SetFloat("_Mix", 0.35f);
            m.SetFloat("_Warp", 0.05f);
            m.SetFloat("_WarpSpeed", 1f);
            m.SetFloat("_Patches", 0.5f);
            m.SetVector("_NoiseRange", new Vector4(0.07f, 0.40f, 0f, 0f));
            m.SetFloat("_Intensity", 3f);
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        // Light at which the FX reach their authored alpha: the zone's own default-hour (day) light.
        static float FullLightIntensity(SanctuaryLivingData living) =>
            Mathf.Max(0.5f, living.EvaluateTimeOfDay(living.defaultHour).lightIntensity);

        /// <summary>
        /// ENV Alpha (#79): carry art-only changes (FX alpha, particle colours) into an EXISTING zone scene without
        /// rebuilding it. Matches by object name (fx id / particle volume id); positions, IDs, markers, cameras,
        /// prefab instances and GUIDs are not touched. Materials, prefabs (colliders) and data assets are already
        /// refreshed by BuildEverything before the scene is opened.
        /// </summary>
        public static int RefreshArtInOpenScene(Zone z, SanctuaryLivingData living)
        {
            if (EditorSceneManager.GetActiveScene().path != z.ScenePath) return 0;
            int changed = 0;
            var byId = (living.fx ?? Array.Empty<SanctuaryLivingData.FxQuad>()).ToDictionary(f => f.id, f => f);
            foreach (var fx in Object.FindObjectsByType<EnvironmentScrollingFx>(FindObjectsInactive.Include))
            {
                Undo.RecordObject(fx, "Refresh zone FX");
                for (int i = 0; fx.entries != null && i < fx.entries.Length; i++)
                {
                    var e = fx.entries[i];
                    if (e.renderer != null && byId.TryGetValue(e.renderer.name, out var f))
                    {
                        if (!Mathf.Approximately(e.alpha, f.alpha)) changed++;
                        e.alpha = f.alpha;
                        float w = Mathf.Max(0.01f, f.width);
                        var t = e.renderer.transform;
                        if (!Mathf.Approximately(t.localScale.x, w)) { Undo.RecordObject(t, "Refresh zone FX"); t.localScale = new Vector3(w, f.height, 1f); changed++; }
                    }
                }
                fx.fullLightIntensity = FullLightIntensity(living);
                EditorUtility.SetDirty(fx);
            }
            foreach (var v in living.particleVolumes ?? Array.Empty<SanctuaryLivingData.ParticleVolume>())
            {
                string ch = string.IsNullOrEmpty(v.channel) ? v.id : v.channel;
                foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include))
                {
                    if (ps.name != v.id) continue;
                    Undo.RecordObject(ps, "Refresh zone particles");
                    var main = ps.main;
                    main.startColor = LookFor(z, ch).color;
                    EditorUtility.SetDirty(ps);
                    changed++;
                }
            }
            if (living.fog != null && living.fog.end > 0f &&
                (!Mathf.Approximately(RenderSettings.fogStartDistance, living.fog.start) ||
                 !Mathf.Approximately(RenderSettings.fogEndDistance, living.fog.end)))
            {
                RenderSettings.fogStartDistance = living.fog.start; // scene render settings, not an object
                RenderSettings.fogEndDistance = living.fog.end;
                changed++;
            }
            if (changed > 0) EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"Dravio Environment {z.prefix}: refreshed {changed} FX / particle value(s) in the open scene " +
                      "(not saved automatically; review, then save).");
            return changed;
        }

        // ------------------------------------------------------------- particles
        struct Look { public Color color; public Vector2 size; public float noise; public bool additive; public string texture; public float rise; }

        static Look LookFor(Zone z, string channel)
        {
            bool ocean = z == Ocean;
            switch (channel)
            {
                case "fireflies": // plankton glow / star motes
                    return new Look { color = (ocean ? new Color(0.45f, 1f, 1f) : new Color(0.85f, 0.92f, 1f)) * 1.6f,
                                      size = new Vector2(0.04f, 0.08f), noise = 0.3f, additive = true, texture = "T_ENV_FX_SoftDot" };
                case "firefliesPond": // anemone glow / shrine motes
                    return new Look { color = (ocean ? new Color(0.5f, 0.9f, 1f) : new Color(1f, 0.88f, 0.55f)) * 1.6f,
                                      size = new Vector2(0.05f, 0.09f), noise = 0.25f, additive = true, texture = "T_ENV_FX_SoftDot" };
                case "dust": // marine snow / blossom petals
                    return ocean
                        ? new Look { color = new Color(0.92f, 1f, 0.96f, 0.5f), size = new Vector2(0.025f, 0.05f), noise = 0.08f, additive = true, texture = "T_ENV_FX_SoftDot", rise = -0.03f }
                        : new Look { color = new Color(1f, 0.78f, 0.88f, 0.9f), size = new Vector2(0.06f, 0.1f), noise = 0.25f, additive = false, texture = "T_ENV_FX_Petal", rise = -0.05f };
                default: // pondRings slot: vent bubbles / cloud wisps
                    return ocean
                        ? new Look { color = new Color(0.9f, 1f, 1f, 0.85f), size = new Vector2(0.05f, 0.11f), noise = 0.1f, additive = false, texture = "T_ENV_FX_Bubble", rise = 0.7f }
                        : new Look { color = new Color(0.93f, 0.95f, 1f, 0.42f), size = new Vector2(2.2f, 3.6f), noise = 0f, additive = false, texture = "T_ENV_FX_CloudWisp", rise = 0f };
            }
        }

        static EnvironmentAmbientParticles BuildZoneParticles(Zone z, Transform root, SanctuaryLivingData data,
                                                              EnvironmentTimeOfDay tod, EnvironmentWind wind)
        {
            var group = new GameObject("Particles").transform;
            group.SetParent(root, false);
            var comp = group.gameObject.AddComponent<EnvironmentAmbientParticles>();
            comp.timeOfDay = tod;
            comp.wind = wind;
            bool ocean = z == Ocean;
            comp.glowColor = ocean ? new Color(0.45f, 1f, 1f) : new Color(1f, 0.9f, 0.6f);
            comp.sparkleColor = ocean ? new Color(0.55f, 0.95f, 1f) : new Color(1f, 0.85f, 0.45f);
            comp.dustColor = ocean ? new Color(0.88f, 0.8f, 0.62f, 0.7f) : new Color(0.95f, 0.95f, 1f, 0.6f);

            Material Mat(string texture, bool additive)
            {
                string dir = File.Exists(z.Textures + "/" + texture + ".png") ? z.Textures : OriginTextures;
                string name = "M_ENV_" + z.prefix + "_FX_" + texture.Replace("T_ENV_FX_", "") + (additive ? "_Add" : "_Alpha");
                return LivingEnvironmentBuilder.ParticleMaterial(name, dir + "/" + texture + ".png", additive, z.Materials);
            }

            Gradient blink = LivingEnvironmentBuilder.AlphaGradient((0f, 0f), (0.2f, 1f), (0.45f, 0.4f), (0.7f, 1f), (1f, 0f));
            Gradient soft = LivingEnvironmentBuilder.AlphaGradient((0f, 0f), (0.25f, 1f), (0.75f, 1f), (1f, 0f));
            foreach (var v in data.particleVolumes)
            {
                string ch = string.IsNullOrEmpty(v.channel) ? v.id : v.channel;
                var look = LookFor(z, ch);
                var ps = LivingEnvironmentBuilder.Ambient(group, v.id, v, Mat(look.texture, look.additive), look.size, look.color,
                    ch.StartsWith("fireflies", StringComparison.Ordinal) ? blink : soft, look.noise);
                var r = ps.GetComponent<ParticleSystemRenderer>();
                if (look.size.y > 1f) r.maxParticleSize = 0.6f; // cloud wisps are large on screen
                if (Mathf.Abs(look.rise) > 0.001f)
                {
                    var vel = ps.velocityOverLifetime;
                    vel.enabled = true;
                    vel.space = ParticleSystemSimulationSpace.World;
                    vel.x = new ParticleSystem.MinMaxCurve(0f);
                    vel.y = new ParticleSystem.MinMaxCurve(look.rise * 0.7f, look.rise * 1.3f);
                    vel.z = new ParticleSystem.MinMaxCurve(0f);
                }
                switch (ch)
                {
                    case "fireflies": comp.fireflies = ps; comp.firefliesMax = v.maxParticles; break;
                    case "firefliesPond": comp.firefliesPond = ps; comp.firefliesPondMax = v.maxParticles; break;
                    case "dust": comp.dust = ps; comp.dustMax = v.maxParticles; break;
                    default: comp.pondRings = ps; comp.pondRingsMax = v.maxParticles; break;
                }
            }
            comp.burstRise = LivingEnvironmentBuilder.Burst(group, "Burst Rise", Mat("T_ENV_FX_SoftDot", true), ocean ? -0.02f : -0.03f);
            comp.burstFall = LivingEnvironmentBuilder.Burst(group, "Burst Fall", Mat("T_ENV_FX_SoftDot", false), ocean ? 0.25f : 1.1f);
            return comp;
        }

        // -------------------------------------------------------------- validate
        /// <summary>
        /// Data, framing and budget checks. Data checks need only the generated assets; the
        /// collider ray casts and the scene budget run when the zone scene is open.
        /// </summary>
        public static bool Validate(Zone z, StringBuilder report)
        {
            var loc = AssetDatabase.LoadAssetAtPath<SanctuaryLocationData>(z.LocationsAsset) ?? UpdateLocationData(z);
            var living = AssetDatabase.LoadAssetAtPath<SanctuaryLivingData>(z.LivingAsset) ?? UpdateLivingData(z);
            var layout = OriginSanctuaryEnvironmentBuilder.LoadLayout(z.LayoutJson);
            report.AppendLine($"== {z.prefix} ({z.nameVi}) ==");
            if (loc == null || living == null || layout == null) { report.AppendLine("  data missing"); return false; }
            int problems = 0;
            void Fail(string msg) { problems++; if (problems <= 25) report.AppendLine("  " + msg); }

            // IDs: exactly the canonical microhabitats of the zone, perches only where allowed.
            var ids = loc.habitats.Select(h => h.id).OrderBy(s => s).ToArray();
            if (!ids.SequenceEqual(z.canonicalHabitats.OrderBy(s => s))) Fail("habitat IDs differ from canonical: " + string.Join(", ", ids));
            foreach (var h in loc.habitats) if (h.defaultUnlocked) Fail(h.id + " marked defaultUnlocked (zone is locked in G1)");
            if (!z.perchAllowed && loc.perchPoints.Length > 0) Fail("perch points in a perch:false zone");
            if (z.perchAllowed && loc.perchPoints.Length == 0) Fail("no perch points in a perch:true zone");
            foreach (var g in loc.zoneGates)
            {
                if (g.worldEdgeId != z.worldEdge || g.habitatId != z.gatewayHere || g.toHabitatId != z.gatewayOrigin || !g.lockedInG1)
                    Fail("gate does not match WorldGraph: " + g.id);
            }
            if (loc.zoneGates.Length != 1) Fail("expected exactly one WorldGraph gate");
            if (loc.spawnPoints.Length < 5) Fail("fewer spawn points than the zone capacity (5)");

            bool Valid(string medium, Vector3 p, string habitat)
            {
                switch (medium)
                {
                    case "swim": return loc.IsSwimmable(p, habitat) && !loc.BlockedAt(p, 0.35f);
                    case "air": return loc.IsFlyable(p, habitat);
                    default:
                        return loc.IsWalkable(p, habitat) && loc.TryGroundHeight(p, out float y) && Mathf.Abs(y - p.y) < 0.01f;
                }
            }
            foreach (var s in loc.spawnPoints) if (!Valid(s.medium, s.Position, s.habitatId)) Fail("spawn invalid for its medium: " + s.id);
            foreach (var p in living.interactionPoints) if (!Valid(p.medium, p.Position, p.habitatId)) Fail("interaction invalid: " + p.id);
            report.AppendLine($"  data: {loc.habitats.Length} habitats, {loc.spawnPoints.Length} spawns, {loc.perchPoints.Length} perches, " +
                              $"{loc.restPoints.Length} rest points, {loc.volumes.Length} volumes, {loc.walkAreas.Length} walk areas, " +
                              $"{living.interactionPoints.Length} interactions");

            // Framing with the same Safe Area / Fantasy UI cases as the Origin validator.
            var pts = new List<(string, Vector3)>();
            foreach (var s in loc.spawnPoints) pts.Add((s.id, s.Position + Vector3.up * (s.medium == "swim" || s.medium == "air" ? 0.6f : 1.2f)));
            foreach (var p in loc.perchPoints) pts.Add((p.id, p.Position));
            foreach (var p in loc.restPoints) pts.Add((p.id, p.Position));
            foreach (var g in loc.zoneGates) pts.Add((g.id, g.Position + Vector3.up));
            foreach (var v in loc.volumes)
                for (int i = 0; i + 1 < v.polygonXZ.Length; i += 2) pts.Add((v.id + " top", new Vector3(v.polygonXZ[i], v.yMax, v.polygonXZ[i + 1])));
            problems += FramingProblems(layout.camera, pts, report);

            // Scene checks: collider heights under perches / rests and the mobile budget.
            var active = EditorSceneManager.GetActiveScene();
            if (active.path == z.ScenePath)
            {
                Physics.SyncTransforms();
                foreach (var p in loc.perchPoints.Concat(loc.restPoints))
                {
                    if (Physics.Raycast(p.Position + Vector3.up * 3f, Vector3.down, out var hit, 6f) && Mathf.Abs(hit.point.y - p.y) > 0.2f)
                        Fail($"{p.id}: collider top {hit.point.y:F2} vs data {p.y:F2}");
                }
                problems += BudgetProblems(report);
            }
            else report.AppendLine("  (open " + z.ScenePath + " to also check colliders and the scene budget)");
            report.AppendLine(problems == 0 ? "  OK" : $"  {problems} problem(s)");
            return problems == 0;
        }

        static int FramingProblems(OriginSanctuaryEnvironmentBuilder.LayoutCamera c, List<(string name, Vector3 p)> pts, StringBuilder report)
        {
            var cases = new (string name, float l, float r, float t, float b)[]
            {
                ("design", c.leftMarginScreenHeights, c.rightPanelScreenHeights, c.topMarginScreenHeights, c.bottomMarginScreenHeights),
                ("notch left", 0.12f, c.rightPanelScreenHeights, c.topMarginScreenHeights, c.bottomMarginScreenHeights),
                ("notch both + home bar", 0.15f, c.rightPanelScreenHeights + 0.15f, c.topMarginScreenHeights, 0.055f),
                ("wide UI 0.62", c.leftMarginScreenHeights, 0.62f, c.topMarginScreenHeights, c.bottomMarginScreenHeights),
            };
            int bad = 0;
            var go = new GameObject("Dravio zone framing probe") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.enabled = false;
                cam.orthographic = true;
                foreach (var cs in cases)
                    foreach (float aspect in new[] { 16f / 9f, 19.5f / 9f, 20f / 9f })
                    {
                        var r = SanctuaryCameraFit.ComputeFraming(new SanctuaryCameraFit.FramingInput
                        {
                            anchor = OriginSanctuaryEnvironmentBuilder.V(c.anchorPosition),
                            euler = OriginSanctuaryEnvironmentBuilder.V(c.eulerAngles),
                            baseSize = c.orthographicSize, minAspect = c.minAspect > 0 ? c.minAspect : 16f / 9f, aspect = aspect,
                            designLeft = c.leftMarginScreenHeights, designRight = c.rightPanelScreenHeights,
                            designTop = c.topMarginScreenHeights, designBottom = c.bottomMarginScreenHeights,
                            left = cs.l, right = cs.r, top = cs.t, bottom = cs.b,
                        });
                        go.transform.SetPositionAndRotation(r.position, r.rotation);
                        cam.orthographicSize = r.orthographicSize;
                        cam.aspect = aspect;
                        var f = r.freeViewport;
                        foreach (var (name, p) in pts)
                        {
                            var v = cam.WorldToViewportPoint(p);
                            if (v.x < f.xMin || v.x > f.xMax || v.y < f.yMin || v.y > f.yMax)
                            {
                                bad++;
                                if (bad <= 5) report.AppendLine($"  framing {cs.name} {aspect:F2}: {name} outside ({v.x:F3}, {v.y:F3})");
                            }
                        }
                    }
            }
            finally { Object.DestroyImmediate(go); }
            report.AppendLine(bad == 0 ? $"  framing: {pts.Count} points inside the free area in 12 aspect / inset cases" : $"  framing: {bad} miss(es)");
            return bad;
        }

        static int BudgetProblems(StringBuilder report)
        {
            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);
            long tris = 0;
            var mats = new HashSet<Material>();
            var texs = new HashSet<Texture>();
            foreach (var r in renderers)
            {
                if (r is MeshRenderer && r.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh != null)
                    for (int s = 0; s < mf.sharedMesh.subMeshCount; s++) tris += mf.sharedMesh.GetIndexCount(s) / 3;
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    mats.Add(m);
                    foreach (string prop in m.GetTexturePropertyNames())
                    {
                        var t = m.GetTexture(prop);
                        if (t != null && t != Texture2D.blackTexture && t != Texture2D.whiteTexture) texs.Add(t);
                    }
                }
            }
            long texBytes = 0;
            foreach (var t in texs) texBytes += (long)(t.width * t.height * 4 * 1.333f);
            int bad = 0;
            if (tris > TriangleBudget) bad++;
            if (mats.Count > MaterialBudget) bad++;
            if (texBytes > TextureBudgetBytes) bad++;
            report.AppendLine($"  budget: {tris:N0}/{TriangleBudget:N0} triangles, {mats.Count}/{MaterialBudget} materials, " +
                              $"{texs.Count} textures ~{texBytes / 1048576f:F1}/{TextureBudgetBytes / 1048576f:F0} MB RGBA32 estimate " +
                              "(ASTC on device is ~4-8x smaller)");
            return bad;
        }
    }
}
