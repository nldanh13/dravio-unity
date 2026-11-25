using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Dravio.Character.Editor
{
    /// <summary>
    /// Dravio/Character/Import Batch Manifest: turns the batch pipeline output (Assets/Dravio/Character/Imported,
    /// import-manifest.json) into Unity assets.
    ///   dragons: importer settings + clip split from the manifest (rigged) or no animation (static fallback),
    ///            URP material, LOD prefab with DragonVisualInstance, per-archetype AnimatorController +
    ///            per-model override, optional DragonFootGrounding, registration in DragonVisualCatalog
    ///   props:   static import, URP material, LODGroup prefab with a simple collider, static flags,
    ///            EnvironmentPropInfo, registration in EnvironmentPropCatalog
    /// Never overwrites an existing material, prefab or controller (existing ones are reused and reported).
    /// Writes Assets/Dravio/Character/Imported/unity-import-report.txt.
    /// </summary>
    internal static class LivingAssetBatchImporter
    {
        internal const string DragonCatalogPath = "Assets/Dravio/Character/Data/DragonVisualCatalog.asset";
        internal const string PropCatalogPath = "Assets/Dravio/Character/Data/EnvironmentPropCatalog.asset";

        [MenuItem("Dravio/Character/Import Batch Manifest")]
        private static void Menu()
        {
            string path = ImportManifest.DefaultPath;
            var sel = Selection.activeObject as TextAsset;
            if (sel != null && AssetDatabase.GetAssetPath(sel).EndsWith("import-manifest.json")) path = AssetDatabase.GetAssetPath(sel);
            string report = Run(path);
            Debug.Log(report);
        }

        /// <summary>Also callable from batch mode: -executeMethod Dravio.Character.Editor.LivingAssetBatchImporter.RunDefault</summary>
        internal static void RunDefault() { Debug.Log(Run(ImportManifest.DefaultPath)); }

        internal static string Run(string manifestPath)
        {
            var log = new StringBuilder();
            ImportManifest manifest;
            try { manifest = ImportManifest.Load(manifestPath); }
            catch (Exception e) { return "Dravio batch import: " + e.Message; }
            string root = ImportManifest.Folder(manifestPath);
            AssetDatabase.Refresh();
            var dragons = LoadOrCreate<DragonVisualCatalog>(DragonCatalogPath);
            var props = LoadOrCreate<EnvironmentPropCatalog>(PropCatalogPath);
            int ok = 0, failed = 0;
            foreach (var e in manifest.entries)
            {
                string folder = root + "/" + e.unityFolder;
                string model = folder + "/" + (e.files != null ? e.files.fbx : "");
                log.AppendLine("== " + e.name + " (" + e.kind + ", pipeline validation " + e.validation + ")");
                try
                {
                    bool done = e.kind == "dragon" ? ImportDragon(e, folder, model, dragons, log) : ImportProp(e, folder, model, props, log);
                    if (done) ok++; else failed++;
                }
                catch (Exception ex) { failed++; log.AppendLine("  ERROR " + ex.Message); }
            }
            EditorUtility.SetDirty(dragons);
            EditorUtility.SetDirty(props);
            AssetDatabase.SaveAssets();
            string header = "Dravio batch import (" + manifest.pipelineVersion + "): " + ok + " imported, " + failed + " failed. Manifest " + manifestPath + "\n";
            File.WriteAllText(root + "/unity-import-report.txt", header + log);
            AssetDatabase.ImportAsset(root + "/unity-import-report.txt");
            return header + log;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) return a;
            a = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        // ---------------------------------------------------------------- dragons
        private static bool ImportDragon(ImportManifest.Entry e, string folder, string modelPath, DragonVisualCatalog catalog, StringBuilder log)
        {
            var mi = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (mi == null) { log.AppendLine("  model not imported: " + modelPath); return false; }
            if (e.approved)
            {
                // Approved by the art director: importer settings, clips, material and prefab stay exactly as they are.
                log.AppendLine("  approved (approved-assets.json): import settings, clips and prefab not touched");
                return true;
            }
            FixTextureTypes(folder, e.name, log);
            bool animated = e.rigged && e.clipList != null && e.clipList.Length > 0;
            if (animated) ConfigureClips(mi, e, log);
            else
            {
                if (mi.importAnimation) { mi.importAnimation = false; mi.SaveAndReimport(); }
                log.AppendLine("  static (not rigged): no Animator states; the catalog entry is marked rigged=false");
            }
            string msg;
            var mat = CharacterMaterialBuilder.Build(folder + "/" + e.name + "_BaseColor.png", out msg);
            if (mat == null) mat = AssetDatabase.LoadAssetAtPath<Material>(CharacterMaterialBuilder.MaterialFolder + "/" + e.name + ".mat");
            log.AppendLine("  material: " + msg);
            string prefabPath = CharacterLodPrefabBuilder.PrefabFolder + "/" + Path.GetFileNameWithoutExtension(modelPath) + "_LODGroup.prefab";
            bool existed = File.Exists(prefabPath);
            var prefab = existed ? AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) : CharacterLodPrefabBuilder.Build(modelPath, mat, out msg);
            log.AppendLine("  prefab: " + (existed ? "exists, kept unchanged: " + prefabPath : msg));
            if (prefab == null) return false;
            if (!existed) DecorateDragonPrefab(prefabPath, e, modelPath, animated, log);
            var entry = catalog.entries.FirstOrDefault(x => x.speciesId == e.species && x.stage == ParseStage(e.stage));
            if (entry == null) { entry = new DragonVisualCatalog.Entry(); catalog.entries.Add(entry); }
            entry.speciesId = e.species;
            entry.stage = ParseStage(e.stage);
            entry.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            entry.archetype = e.archetype;
            entry.rigged = animated;
            entry.clips = animated ? e.clipList.Select(c => c.name).ToArray() : new string[0];
            entry.anchors = e.anchors ?? new string[0];
            entry.ikChains = (e.ikChains ?? new ImportManifest.IkChain[0])
                .Select(c => new DragonVisualCatalog.IkChain { upper = c.upper, lower = c.lower, foot = c.foot }).ToArray();
            entry.clipSpeeds = animated
                ? e.clipList.Where(c => c.groundSpeed > 0f).Select(c => new DragonVisualCatalog.ClipSpeed { clip = c.name, groundSpeed = c.groundSpeed }).ToArray()
                : new DragonVisualCatalog.ClipSpeed[0];
            var walk = animated ? e.clipList.FirstOrDefault(c => c.name == "Walk") : null;
            entry.locomotionReferenceSpeed = walk != null ? walk.groundSpeed : 0f;
            entry.animQaStatus = e.animQa != null ? e.animQa.status : null;
            entry.animQaWarnings = e.animQa != null && e.animQa.warnings != null ? e.animQa.warnings : new string[0];
            if (entry.animQaWarnings.Length > 0)
                log.AppendLine("  animation QA " + entry.animQaStatus + ": " + string.Join(" | ", entry.animQaWarnings));
            entry.clipLengths = animated
                ? e.clipList.Select(c => new DragonVisualCatalog.ClipLength { clip = c.name, seconds = ClipSeconds(e, c.name) }).ToArray()
                : new DragonVisualCatalog.ClipLength[0];
            entry.eatSeconds = ClipSeconds(e, "Eat");
            entry.happySeconds = ClipSeconds(e, "Happy");
            entry.validation = e.validation;
            entry.sourceManifest = modelPath;
            log.AppendLine("  catalog: " + e.species + "/" + entry.stage + (animated ? " clips " + string.Join(",", entry.clips) : " static"));
            return true;
        }

        internal static DragonVisualStage ParseStage(string s)
        {
            DragonVisualStage st;
            return Enum.TryParse(s, out st) ? st : DragonVisualStage.Unstaged;
        }

        private static float ClipSeconds(ImportManifest.Entry e, string name)
        {
            var c = e.clipList == null ? null : e.clipList.FirstOrDefault(x => x.name == name);
            return c == null ? 0f : (c.end - c.start) / (float)Math.Max(1, e.fps);
        }

        /// <summary>Splits the single baked take into the manifest's clips. Frame numbers are mapped relative to
        /// the take's first frame, so it works whether the FBX take starts at frame 0 or at Blender's frame 1.</summary>
        private static void ConfigureClips(ModelImporter mi, ImportManifest.Entry e, StringBuilder log)
        {
            if (mi.animationType != ModelImporterAnimationType.Generic || !mi.importAnimation)
            {
                mi.animationType = ModelImporterAnimationType.Generic;
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                mi.importAnimation = true;
                mi.SaveAndReimport();
            }
            var takes = mi.defaultClipAnimations;
            if (takes == null || takes.Length == 0) { log.AppendLine("  WARNING no animation take found in FBX"); return; }
            float takeFirst = takes[0].firstFrame;
            int first = e.clipList.Min(c => c.start);
            var clips = e.clipList.Select(c =>
            {
                var t = new ModelImporterClipAnimation
                {
                    name = c.name, takeName = takes[0].takeName,
                    firstFrame = takeFirst + (c.start - first), lastFrame = takeFirst + (c.end - first),
                    loopTime = c.loop, loopPose = false, wrapMode = c.loop ? WrapMode.Loop : WrapMode.Once,
                    keepOriginalOrientation = true, keepOriginalPositionY = true, keepOriginalPositionXZ = true,
                };
                return t;
            }).ToArray();
            float takeLast = takes[0].lastFrame;
            float needed = takeFirst + (e.clipList.Max(c => c.end) - first);
            if (needed > takeLast + 0.5f) log.AppendLine("  WARNING clip range ends at " + needed + " but take ends at " + takeLast);
            mi.clipAnimations = clips;
            mi.SaveAndReimport();
            log.AppendLine("  clips: " + string.Join(", ", clips.Select(c => c.name + "[" + c.firstFrame + "-" + c.lastFrame + (c.loopTime ? " loop" : "") + "]").ToArray()));
        }

        private static void DecorateDragonPrefab(string prefabPath, ImportManifest.Entry e, string modelPath, bool animated, StringBuilder log)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                if (root.GetComponent<DragonVisualInstance>() == null) root.AddComponent<DragonVisualInstance>();
                var animator = root.GetComponentInChildren<Animator>(true);
                if (animated)
                {
                    if (animator == null) animator = root.AddComponent<Animator>();
                    string m;
                    var baseCtrl = DragonAnimatorBuilder.GetOrCreateBase(e.archetype, e.clipList.Select(c => c.name), out m);
                    log.AppendLine("  animator: " + m);
                    var ov = DragonAnimatorBuilder.CreateOverride(baseCtrl, modelPath, e.name, out m);
                    log.AppendLine("  animator: " + m);
                    animator.runtimeAnimatorController = ov;
                    animator.applyRootMotion = false; // the integration layer owns movement
                    animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                    // U1-CHAR.6: head look (needs Neck + Head bones) and blink (self-disables without an eyelid blend shape)
                    var bones = root.GetComponentsInChildren<Transform>(true);
                    if (bones.Any(b => b.name == "Neck") && bones.Any(b => b.name == "Head") && root.GetComponent<DragonHeadLook>() == null)
                    {
                        root.AddComponent<DragonHeadLook>().enabled = false; // the manager enables it while a look is requested
                        log.AppendLine("  head look: Neck/Head");
                    }
                    if (root.GetComponent<DragonBlink>() == null) root.AddComponent<DragonBlink>();
                    if (e.ikChains != null && e.ikChains.Length > 0 && root.GetComponent<DragonFootGrounding>() == null)
                    {
                        var g = root.AddComponent<DragonFootGrounding>();
                        g.enabled = false; // the manager enables it for near dragons in ground states
                        log.AppendLine("  foot IK: " + e.ikChains.Length + " chains");
                    }
                }
                else if (animator != null) UnityEngine.Object.DestroyImmediate(animator);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void FixTextureTypes(string folder, string name, StringBuilder log)
        {
            var ti = AssetImporter.GetAtPath(folder + "/" + name + "_Normal.png") as TextureImporter;
            if (ti != null && ti.textureType != TextureImporterType.NormalMap)
            {
                ti.textureType = TextureImporterType.NormalMap;
                ti.SaveAndReimport();
                log.AppendLine("  normal map texture type set");
            }
            var ms = AssetImporter.GetAtPath(folder + "/" + name + "_MetallicSmoothness.png") as TextureImporter;
            if (ms != null && ms.sRGBTexture) { ms.sRGBTexture = false; ms.SaveAndReimport(); }
        }

        // ---------------------------------------------------------------- props
        private static bool ImportProp(ImportManifest.Entry e, string folder, string modelPath, EnvironmentPropCatalog catalog, StringBuilder log)
        {
            var mi = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (mi == null) { log.AppendLine("  model not imported: " + modelPath); return false; }
            if (mi.importAnimation || mi.animationType != ModelImporterAnimationType.None || mi.addCollider)
            {
                mi.animationType = ModelImporterAnimationType.None;
                mi.importAnimation = false;
                mi.addCollider = false;
                mi.SaveAndReimport();
            }
            FixTextureTypes(folder, e.name, log);
            string msg;
            var mat = CharacterMaterialBuilder.Build(folder + "/" + e.name + "_BaseColor.png", out msg);
            if (mat == null) mat = AssetDatabase.LoadAssetAtPath<Material>(CharacterMaterialBuilder.MaterialFolder + "/" + e.name + ".mat");
            log.AppendLine("  material: " + msg);
            var prefab = PropPrefabBuilder.Build(modelPath, mat, e, out msg);
            log.AppendLine("  prefab: " + msg);
            if (prefab == null) return false;
            var entry = catalog.Find(e.propId);
            if (entry == null) { entry = new EnvironmentPropCatalog.Entry(); catalog.entries.Add(entry); }
            entry.propId = e.propId;
            entry.category = e.category;
            entry.prefab = prefab;
            entry.height = e.size != null ? e.size.height : 0f;
            entry.footprintRadius = e.size != null ? e.size.footprintRadius : 0f;
            entry.sizeConfirmed = e.sizeConfirmed;
            entry.validation = e.validation;
            if (!e.sizeConfirmed) log.AppendLine("  NOTE real-world size not confirmed (generator units)");
            return true;
        }
    }
}
