using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Dravio.EnvironmentArt.EditorTools
{
    /// <summary>
    /// U1-ENV.3 editor tools: living data asset, particle materials and systems, ambience
    /// sources and interaction markers, added as ONE extra root object to the Origin
    /// Sanctuary environment scene. Only touches that scene (never the test scenes),
    /// never moves the U1-ENV.2 props, colliders or location markers, and re-running it
    /// replaces only its own root.
    /// </summary>
    public static class LivingEnvironmentBuilder
    {
        const string Root = "Assets/Dravio/Environment";
        public const string LivingJsonPath = Root + "/Data/OriginSanctuaryLiving.json";
        public const string LivingAssetPath = Root + "/Data/OriginSanctuaryLiving.asset";
        const string AudioDir = Root + "/Audio";
        const string TexturesDir = Root + "/Textures";
        const string MaterialsDir = Root + "/Materials";
        public const string RootName = "Origin Sanctuary - Living Environment";

        // ------------------------------------------------------------------ menu
        [MenuItem("Dravio/Environment/6. Update Living Environment Data (day-night, interactions)", priority = 116)]
        public static void UpdateLivingDataMenu() => UpdateLivingData();

        [MenuItem("Dravio/Environment/7. Add or Update Living Environment in Scene", priority = 117)]
        public static void AddToSceneMenu() => AddToOpenScene(true);

        [MenuItem("Dravio/Environment/8. Validate Living Environment", priority = 118)]
        public static void ValidateMenu() => Validate(true);

        // ------------------------------------------------------------------ data
        public static SanctuaryLivingData UpdateLivingData()
        {
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(LivingJsonPath);
            if (json == null)
            {
                Debug.LogError("Dravio Environment: " + LivingJsonPath + " missing. Re-run the Blender build with --unity.");
                return null;
            }
            var data = AssetDatabase.LoadAssetAtPath<SanctuaryLivingData>(LivingAssetPath);
            bool create = data == null;
            if (create) data = ScriptableObject.CreateInstance<SanctuaryLivingData>();
            data.LoadFromJson(json.text);
            if (data.keyframes == null || data.keyframes.Length < 2)
            {
                Debug.LogError("Dravio Environment: living JSON has no time-of-day keyframes.");
                return null;
            }
            if (create) AssetDatabase.CreateAsset(data, LivingAssetPath);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Debug.Log($"Dravio Environment: living data updated ({data.keyframes.Length} keyframes, " +
                      $"{data.interactionPoints.Length} interaction points, {data.particleVolumes.Length} particle volumes).");
            return data;
        }

        // ----------------------------------------------------------------- scene
        public static void AddToOpenScene(bool dialog)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != OriginSanctuaryEnvironmentBuilder.ScenePath)
            {
                string msg = "Open " + OriginSanctuaryEnvironmentBuilder.ScenePath + " first (menu 3). " +
                             "The living environment is only added to the Origin Sanctuary environment scene.";
                Debug.LogWarning("Dravio Environment: " + msg);
                if (dialog) EditorUtility.DisplayDialog("Dravio Environment", msg, "OK");
                return;
            }
            var data = AssetDatabase.LoadAssetAtPath<SanctuaryLivingData>(LivingAssetPath);
            if (data == null) data = UpdateLivingData();
            if (data == null) return;
            var locations = AssetDatabase.LoadAssetAtPath<SanctuaryLocationData>(OriginSanctuaryEnvironmentBuilder.LocationsAssetPath);

            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);

            Light sun = null;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { sun = l; break; }
            Camera cam = Camera.main;
            if (cam == null)
            {
                var fit = Object.FindAnyObjectByType<SanctuaryCameraFit>();
                if (fit != null) cam = fit.GetComponent<Camera>();
            }

            var root = new GameObject(RootName);
            var living = root.AddComponent<LivingEnvironment>();
            living.data = data;
            living.locations = locations;

            var tod = root.AddComponent<EnvironmentTimeOfDay>();
            tod.data = data;
            tod.mainLight = sun;
            tod.targetCamera = cam;
            tod.hour = data.defaultHour;
            living.timeOfDay = tod;

            var wind = root.AddComponent<EnvironmentWind>();
            wind.timeOfDay = tod;
            living.wind = wind;

            var water = root.AddComponent<EnvironmentWater>();
            living.water = water;

            living.particles = BuildParticles(root.transform, data, tod, wind);
            living.ambience = BuildAudio(root.transform, data, tod, wind, cam);

            var inter = root.AddComponent<EnvironmentInteractions>();
            inter.data = data;
            inter.timeOfDay = tod;
            inter.particles = living.particles;
            inter.water = water;
            inter.audioMixer = living.ambience;
            living.interactions = inter;

            var markers = new GameObject("Interaction Points (data mirror)").transform;
            markers.SetParent(root.transform, false);
            foreach (var p in data.interactionPoints)
            {
                var m = OriginSanctuaryEnvironmentBuilder.Marker(markers, p.id, SanctuaryMarker.Kind.Interaction, p.habitatId, null);
                m.transform.SetPositionAndRotation(p.Position, p.Rotation);
            }

            if (sun == null) Debug.LogWarning("Dravio Environment: no directional light found; the time of day will not light the scene.");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = root;
            Debug.Log("Dravio Environment: living environment added (" + data.interactionPoints.Length +
                      " interaction points). Scrub 'hour' on Environment Time Of Day to preview dawn, day, dusk and night.");
        }

        // ------------------------------------------------------------- particles
        static EnvironmentAmbientParticles BuildParticles(Transform root, SanctuaryLivingData data, EnvironmentTimeOfDay tod, EnvironmentWind wind)
        {
            var add = ParticleMaterial("M_ENV_FX_Additive", "T_ENV_FX_SoftDot", additive: true);
            var alpha = ParticleMaterial("M_ENV_FX_Alpha", "T_ENV_FX_SoftDot", additive: false);
            var ring = ParticleMaterial("M_ENV_FX_Ring", "T_ENV_FX_Ring", additive: false);

            var group = new GameObject("Particles").transform;
            group.SetParent(root, false);
            var comp = group.gameObject.AddComponent<EnvironmentAmbientParticles>();
            comp.timeOfDay = tod;
            comp.wind = wind;

            var ff = data.FindVolume("fireflies");
            var ffp = data.FindVolume("fireflies_pond");
            var dust = data.FindVolume("dust");
            var rings = data.FindVolume("pond_rings");

            Gradient blink = AlphaGradient((0f, 0f), (0.15f, 1f), (0.3f, 0.25f), (0.45f, 1f), (0.7f, 0.35f), (0.85f, 1f), (1f, 0f));
            if (ff != null) { comp.fireflies = Ambient(group, "Fireflies", ff, add, new Vector2(0.06f, 0.1f), new Color(0.95f, 1f, 0.45f) * 1.6f, blink, 0.35f); comp.firefliesMax = ff.maxParticles; }
            if (ffp != null) { comp.firefliesPond = Ambient(group, "Fireflies Pond", ffp, add, new Vector2(0.05f, 0.09f), new Color(0.75f, 1f, 0.6f) * 1.6f, blink, 0.25f); comp.firefliesPondMax = ffp.maxParticles; }
            if (dust != null)
            {
                comp.dust = Ambient(group, "Light Dust", dust, add, new Vector2(0.03f, 0.06f), new Color(1f, 0.92f, 0.72f, 0.45f),
                                    AlphaGradient((0f, 0f), (0.25f, 1f), (0.75f, 1f), (1f, 0f)), 0.12f);
                comp.dustMax = dust.maxParticles;
            }
            if (rings != null)
            {
                var ps = Ambient(group, "Pond Rings", rings, ring, new Vector2(0.25f, 0.4f), new Color(0.9f, 0.98f, 1f, 0.7f),
                                 AlphaGradient((0f, 0f), (0.15f, 0.8f), (1f, 0f)), 0f);
                var r = ps.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
                var sol = ps.sizeOverLifetime;
                sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 2.6f));
                comp.pondRings = ps;
                comp.pondRingsMax = rings.maxParticles;
            }
            comp.burstRise = Burst(group, "Burst Rise", add, -0.03f);
            comp.burstFall = Burst(group, "Burst Fall", alpha, 1.1f);
            return comp;
        }

        static ParticleSystem Ambient(Transform parent, string name, SanctuaryLivingData.ParticleVolume v, Material mat,
                                      Vector2 size, Color color, Gradient overLife, float noise)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = v.Center;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 5f;
            float l0 = v.lifetime != null && v.lifetime.Length > 0 ? v.lifetime[0] : 4f;
            float l1 = v.lifetime != null && v.lifetime.Length > 1 ? v.lifetime[1] : l0;
            main.startLifetime = new ParticleSystem.MinMaxCurve(l0, l1);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.05f);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = Mathf.Max(1, v.maxParticles);
            main.cullingMode = ParticleSystemCullingMode.Automatic;
            main.ringBufferMode = ParticleSystemRingBufferMode.Disabled;

            var em = ps.emission;
            em.enabled = true;
            em.rateOverTime = 0f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            var s = v.Size;
            shape.scale = new Vector3(Mathf.Max(s.x, 0.01f), Mathf.Max(s.y, 0.001f), Mathf.Max(s.z, 0.01f));

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = overLife;

            if (noise > 0f)
            {
                var n = ps.noise;
                n.enabled = true;
                n.strength = noise;
                n.frequency = 0.35f;
                n.scrollSpeed = 0.15f;
                n.octaveCount = 1;
                n.quality = ParticleSystemNoiseQuality.Low;
            }
            Renderer(ps, mat);
            return ps;
        }

        static ParticleSystem Burst(Transform parent, string name, Material mat, float gravity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 1f;
            main.startLifetime = 1f;
            main.startSpeed = 0f;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 64;
            var em = ps.emission;
            em.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = AlphaGradient((0f, 1f), (0.7f, 0.8f), (1f, 0f));
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.4f));
            Renderer(ps, mat);
            return ps;
        }

        static void Renderer(ParticleSystem ps, Material mat)
        {
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.maxParticleSize = 0.08f;
        }

        static Gradient AlphaGradient(params (float t, float a)[] keys)
        {
            var g = new Gradient();
            var a = new GradientAlphaKey[keys.Length];
            for (int i = 0; i < keys.Length; i++) a[i] = new GradientAlphaKey(keys[i].a, keys[i].t);
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, a);
            return g;
        }

        static Material ParticleMaterial(string name, string texture, bool additive)
        {
            OriginSanctuaryEnvironmentBuilder.EnsureFolder(MaterialsDir);
            string path = MaterialsDir + "/" + name + ".mat";
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            bool urp = shader != null;
            if (!urp) shader = Shader.Find("Sprites/Default");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturesDir + "/" + texture + ".png");
            if (urp)
            {
                m.SetTexture("_BaseMap", tex);
                m.SetColor("_BaseColor", Color.white);
                // Transparent surface; additive (fireflies, dust, sparkles) or alpha blend.
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", additive ? 2f : 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
                m.SetFloat("_SrcBlendAlpha", (float)(additive ? BlendMode.Zero : BlendMode.One));
                m.SetFloat("_DstBlendAlpha", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
                m.SetFloat("_ZWrite", 0f);
                m.SetFloat("_Cull", (float)CullMode.Off);
                m.SetOverrideTag("RenderType", "Transparent");
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                if (additive) m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                m.renderQueue = (int)RenderQueue.Transparent;
                m.enableInstancing = false;
            }
            else
            {
                m.mainTexture = tex;
                Debug.LogWarning("Dravio Environment: URP particle shader not found; using Sprites/Default for " + name + ".");
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        // ----------------------------------------------------------------- audio
        static EnvironmentAudio BuildAudio(Transform root, SanctuaryLivingData data, EnvironmentTimeOfDay tod, EnvironmentWind wind, Camera cam)
        {
            var go = new GameObject("Ambience");
            go.transform.SetParent(root, false);
            var a = go.AddComponent<EnvironmentAudio>();
            a.data = data;
            a.timeOfDay = tod;
            a.wind = wind;
            a.viewCamera = cam;
            var loops = new List<EnvironmentAudio.NamedClip>();
            var missing = new List<string>();
            foreach (var l in data.audioLoops)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "/" + l.clip + ".wav");
                if (clip == null) missing.Add(l.clip);
                else loops.Add(new EnvironmentAudio.NamedClip { id = l.id, clip = clip });
            }
            var shots = new List<EnvironmentAudio.NamedClip>();
            foreach (var id in data.oneShots)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "/" + id + ".wav");
                if (clip == null) missing.Add(id);
                else shots.Add(new EnvironmentAudio.NamedClip { id = id, clip = clip });
            }
            a.loopClips = loops.ToArray();
            a.oneShotClips = shots.ToArray();
            foreach (var m in missing) Debug.LogWarning("Dravio Environment: audio clip missing: " + m);
            return a;
        }

        // -------------------------------------------------------------- validate
        public static bool Validate(bool dialog)
        {
            var data = AssetDatabase.LoadAssetAtPath<SanctuaryLivingData>(LivingAssetPath);
            if (data == null) data = UpdateLivingData();
            var loc = AssetDatabase.LoadAssetAtPath<SanctuaryLocationData>(OriginSanctuaryEnvironmentBuilder.LocationsAssetPath);
            if (loc == null) loc = OriginSanctuaryEnvironmentBuilder.UpdateLocationData();
            var layout = OriginSanctuaryEnvironmentBuilder.LoadLayout();
            if (data == null || loc == null || layout == null) return false;
            var report = new StringBuilder();
            int problems = 0;

            var ids = new HashSet<string>();
            foreach (var p in data.interactionPoints)
            {
                if (!ids.Add(p.id)) { problems++; report.AppendLine("  duplicate interaction id " + p.id); }
                if (loc.FindHabitat(p.habitatId) == null) { problems++; report.AppendLine("  unknown habitat " + p.habitatId + " on " + p.id); }
                if (!loc.IsWalkable(p.Position, p.habitatId)) { problems++; report.AppendLine("  not walkable: " + p.id); }
                if (p.requiresSwim && !loc.InSwimArea(p.Target, p.habitatId)) { problems++; report.AppendLine("  splash target outside swim area: " + p.id); }
            }
            report.AppendLine($"Interaction points: {data.interactionPoints.Length}, problems so far {problems}");

            // Framing with real-world insets (screen heights): notch, home bar, wider UI.
            var c = layout.camera;
            var cases = new (string name, float l, float r, float t, float b)[]
            {
                ("design", c.leftMarginScreenHeights, c.rightPanelScreenHeights, c.topMarginScreenHeights, c.bottomMarginScreenHeights),
                ("notch left", 0.12f, c.rightPanelScreenHeights, c.topMarginScreenHeights, c.bottomMarginScreenHeights),
                ("notch both + home bar", 0.15f, c.rightPanelScreenHeights + 0.15f, c.topMarginScreenHeights, 0.055f),
                ("wide UI 0.62", c.leftMarginScreenHeights, 0.62f, c.topMarginScreenHeights, c.bottomMarginScreenHeights),
            };
            var aspects = new[] { 16f / 9f, 19.5f / 9f, 20f / 9f };
            var go = new GameObject("Dravio living framing probe") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.enabled = false;
                cam.orthographic = true;
                foreach (var cs in cases)
                {
                    int bad = 0;
                    foreach (float aspect in aspects)
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
                        void Check(string name, Vector3 p)
                        {
                            var v = cam.WorldToViewportPoint(p);
                            if (v.x < f.xMin || v.x > f.xMax || v.y < f.yMin || v.y > f.yMax)
                            {
                                bad++;
                                if (bad <= 3) report.AppendLine($"  {cs.name} {aspect:F2}: {name} outside free area ({v.x:F3}, {v.y:F3})");
                            }
                        }
                        foreach (var s in loc.spawnPoints) Check(s.id, s.Position + Vector3.up * 1.2f);
                        foreach (var p in data.interactionPoints) Check(p.id, p.Position + Vector3.up * 1.2f);
                        foreach (var h in loc.habitats)
                            for (int i = 0; i + 1 < h.polygonXZ.Length; i += 2)
                                Check(h.id + " corner", new Vector3(h.polygonXZ[i], 0f, h.polygonXZ[i + 1]));
                    }
                    problems += bad;
                    report.AppendLine($"Framing '{cs.name}': {(bad == 0 ? "OK" : bad + " point(s) outside")}");
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
            }

            foreach (var l in data.audioLoops)
                if (AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "/" + l.clip + ".wav") == null) { problems++; report.AppendLine("  missing loop " + l.clip); }
            foreach (var id in data.oneShots)
                if (AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "/" + id + ".wav") == null) { problems++; report.AppendLine("  missing one-shot " + id); }

            var s13 = data.EvaluateTimeOfDay(13f);
            report.AppendLine($"13:00 light {s13.lightElevation:F0}/{s13.lightAzimuth:F0} intensity {s13.lightIntensity:F2} (U1-ENV.2 static look: 46/-38, 1.35)");

            string title = problems == 0 ? "Living environment OK" : $"Living environment: {problems} problem(s)";
            Debug.Log("Dravio Environment " + title + "\n" + report);
            if (dialog) EditorUtility.DisplayDialog("Dravio Environment - " + title, report.ToString(), "OK");
            return problems == 0;
        }
    }
}
