using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dravio.Character.Editor
{
    /// <summary>
    /// Read-only Unity-side audit of an imported character model (FBX/GLB importer output):
    /// triangles per LOD against the shared budgets, UV0, vertex colour, skin influences and
    /// bones, material slots, URP Lit texture slots and their import settings, and species rig
    /// flags for models named DRG_&lt;speciesId&gt;_&lt;Stage&gt;. Never modifies the model or its importer.
    /// A PASS is structural only; visual, animation and device checks remain manual.
    /// </summary>
    internal static class CharacterModelValidator
    {
        private static readonly Regex LodRegex = new Regex(@"_LOD(\d+)$", RegexOptions.IgnoreCase);
        private static readonly Regex SpeciesRegex = new Regex(@"^DRG_([a-z]+)_(Baby|Juvenile|Adult)", RegexOptions.IgnoreCase);

        internal sealed class Report
        {
            public readonly List<string> Errors = new List<string>();
            public readonly List<string> Warnings = new List<string>();
            public readonly List<string> Info = new List<string>();
            public readonly Dictionary<int, int> TrianglesPerLod = new Dictionary<int, int>();
            public string Status { get { return Errors.Count > 0 ? "FAIL" : Warnings.Count > 0 ? "PASS_WITH_WARNINGS" : "PASS"; } }
        }

        [MenuItem("Dravio/Character/Validate Selected Model")]
        private static void ValidateSelectedMenu()
        {
            var paths = Selection.objects.Select(AssetDatabase.GetAssetPath)
                .Where(p => !string.IsNullOrEmpty(p) && AssetImporter.GetAtPath(p) is ModelImporter).Distinct().ToArray();
            if (paths.Length == 0)
            {
                Debug.LogWarning("Dravio character audit: select one or more imported model assets (FBX/GLB) in the Project window.");
                return;
            }
            foreach (var path in paths)
            {
                var report = Validate(path);
                var text = Format(path, report);
                if (report.Errors.Count > 0) Debug.LogError(text);
                else if (report.Warnings.Count > 0) Debug.LogWarning(text);
                else Debug.Log(text);
                WriteJson(path, report);
            }
        }

        [MenuItem("Dravio/Character/Validate Selected Model", true)]
        private static bool ValidateSelectedMenuEnabled()
        {
            return Selection.objects.Any(o => AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(o)) is ModelImporter);
        }

        /// <param name="lodProfile">null = character LOD budgets; otherwise a propProfiles key (prop_small, prop_large).</param>
        internal static Report Validate(string path, string lodProfile = null)
        {
            var r = new Report();
            CharacterBudgets budgets;
            try { budgets = CharacterBudgets.Load(lodProfile); }
            catch (Exception e) { r.Errors.Add(e.Message); return r; }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (model == null || importer == null) { r.Errors.Add("Not an imported model: " + path); return r; }

            var renderers = model.GetComponentsInChildren<Renderer>(true)
                .Where(x => x is SkinnedMeshRenderer || x is MeshRenderer)
                .Where(x => !x.gameObject.name.EndsWith("_Collider", StringComparison.Ordinal)) // prop collision mesh, not rendered
                .ToArray();
            if (renderers.Length == 0) { r.Errors.Add("No mesh renderers."); return r; }

            bool explicitLods = renderers.Any(x => LodRegex.IsMatch(x.gameObject.name));
            if (!explicitLods) r.Warnings.Add("No _LOD0/_LOD1/_LOD2 renderer names; treated as one LOD0.");

            var skinnedRoots = new HashSet<Transform>();
            var boneNames = new HashSet<string>();
            int skinnedCount = 0;
            foreach (var renderer in renderers)
            {
                var mesh = MeshOf(renderer);
                string tag = renderer.gameObject.name;
                if (mesh == null) { r.Errors.Add(tag + ": renderer has no mesh."); continue; }
                int lod = 0;
                var m = LodRegex.Match(tag);
                if (m.Success) lod = int.Parse(m.Groups[1].Value);
                int tris = Triangles(mesh);
                int prev;
                r.TrianglesPerLod.TryGetValue(lod, out prev);
                r.TrianglesPerLod[lod] = prev + tris;

                if (!mesh.HasVertexAttribute(VertexAttribute.TexCoord0))
                    r.Errors.Add(tag + ": no UV0 (run scripts/art/character/prepare_character.py).");
                if (mesh.HasVertexAttribute(VertexAttribute.Color))
                    r.Info.Add(tag + ": has vertex colours (URP Lit ignores them; Base Color must come from a texture).");

                var slots = renderer.sharedMaterials;
                if (slots.Length > budgets.materials.maxSlotsPerLodError)
                    r.Errors.Add(tag + ": " + slots.Length + " material slots.");
                else if (slots.Length > budgets.materials.maxSlotsPerLodWarn)
                    r.Warnings.Add(tag + ": " + slots.Length + " material slots (draw calls).");
                foreach (var mat in slots) CheckMaterial(tag, mat, budgets, r);

                var smr = renderer as SkinnedMeshRenderer;
                if (smr != null)
                {
                    skinnedCount++;
                    if (smr.rootBone != null) skinnedRoots.Add(smr.rootBone);
                    foreach (var b in smr.bones) if (b != null) boneNames.Add(b.name);
                    if (smr.bones.Length > budgets.skin.maxBones)
                        r.Warnings.Add(tag + ": " + smr.bones.Length + " bones > " + budgets.skin.maxBones + ".");
                    int maxInfluences = MaxInfluences(mesh);
                    if (maxInfluences < 0) r.Info.Add(tag + ": bone weights not readable in this context; importer setting checked instead.");
                    else if (maxInfluences > budgets.skin.maxInfluences)
                        r.Errors.Add(tag + ": " + maxInfluences + " bone influences > " + budgets.skin.maxInfluences + ".");
                }
            }
            if (skinnedCount > 0 && skinnedCount != renderers.Length)
                r.Errors.Add("Some LOD renderers are skinned and some are not.");
            if (skinnedRoots.Count > 1)
                r.Errors.Add("LOD renderers use different root bones; LODs must share one skeleton.");
            if (skinnedCount > 0 && importer.skinWeights == ModelImporterSkinWeights.Custom && importer.maxBonesPerVertex > budgets.skin.maxInfluences)
                r.Warnings.Add("Importer allows " + importer.maxBonesPerVertex + " bones per vertex.");

            foreach (var cfg in budgets.lods)
            {
                int t;
                if (!r.TrianglesPerLod.TryGetValue(cfg.level, out t))
                {
                    (explicitLods ? r.Errors : r.Warnings).Add("LOD" + cfg.level + " missing.");
                    continue;
                }
                if (t > cfg.maxTriangles) r.Errors.Add("LOD" + cfg.level + ": " + t + " tris > max " + cfg.maxTriangles + ".");
                else if (t < cfg.minTriangles) r.Warnings.Add("LOD" + cfg.level + ": " + t + " tris < target minimum " + cfg.minTriangles + " (only acceptable for a genuinely simpler source).");
                else r.Info.Add("LOD" + cfg.level + ": " + t + " tris within " + cfg.minTriangles + "-" + cfg.maxTriangles + ".");
            }
            var levels = r.TrianglesPerLod.Keys.OrderBy(k => k).ToArray();
            for (int i = 1; i < levels.Length; i++)
                if (r.TrianglesPerLod[levels[i]] >= r.TrianglesPerLod[levels[i - 1]])
                    r.Errors.Add("LOD" + levels[i] + " is not lighter than LOD" + levels[i - 1] + ".");

            CheckSpecies(model.name, boneNames, skinnedCount > 0, r);
            return r;
        }

        private static Mesh MeshOf(Renderer renderer)
        {
            var smr = renderer as SkinnedMeshRenderer;
            if (smr != null) return smr.sharedMesh;
            var mf = renderer.GetComponent<MeshFilter>();
            return mf != null ? mf.sharedMesh : null;
        }

        internal static int Triangles(Mesh mesh)
        {
            long total = 0;
            for (int s = 0; s < mesh.subMeshCount; s++)
                if (mesh.GetTopology(s) == MeshTopology.Triangles) total += mesh.GetIndexCount(s) / 3;
            return (int)total;
        }

        private static int MaxInfluences(Mesh mesh)
        {
            try
            {
                var perVertex = mesh.GetBonesPerVertex();
                int max = 0;
                for (int i = 0; i < perVertex.Length; i++) if (perVertex[i] > max) max = perVertex[i];
                return max;
            }
            catch (Exception) { return -1; }
        }

        private static void CheckMaterial(string tag, Material mat, CharacterBudgets budgets, Report r)
        {
            if (mat == null) { r.Errors.Add(tag + ": empty material slot."); return; }
            string where = tag + "/" + mat.name;
            if (mat.shader == null || !mat.shader.name.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal))
                r.Warnings.Add(where + ": shader '" + (mat.shader != null ? mat.shader.name : "none") + "' is not a URP shader.");
            CheckTexture(where, mat, "_BaseMap", true, false, budgets, r, required: true);
            CheckTexture(where, mat, "_BumpMap", false, true, budgets, r, required: false);
            CheckTexture(where, mat, "_MetallicGlossMap", false, false, budgets, r, required: false);
        }

        private static void CheckTexture(string where, Material mat, string prop, bool expectSrgb, bool expectNormal,
                                         CharacterBudgets budgets, Report r, bool required)
        {
            if (!mat.HasProperty(prop)) return;
            var tex = mat.GetTexture(prop);
            if (tex == null)
            {
                if (required) r.Warnings.Add(where + ": " + prop + " not assigned (Base Color is then a flat colour).");
                return;
            }
            int size = Math.Max(tex.width, tex.height);
            if (size > budgets.textures.maxSize) r.Errors.Add(where + ": " + prop + " " + tex.width + "x" + tex.height + " > " + budgets.textures.maxSize + ".");
            else if (size > budgets.textures.recommendedSize) r.Warnings.Add(where + ": " + prop + " " + tex.width + "x" + tex.height + " > recommended " + budgets.textures.recommendedSize + ".");
            if (budgets.textures.requirePowerOfTwo && (!Mathf.IsPowerOfTwo(tex.width) || !Mathf.IsPowerOfTwo(tex.height)))
                r.Warnings.Add(where + ": " + prop + " is not power-of-two.");
            var ti = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex)) as TextureImporter;
            if (ti == null) { r.Info.Add(where + ": " + prop + " is embedded/sub-asset; import settings not checkable (extract textures to check)."); return; }
            if (expectNormal && ti.textureType != TextureImporterType.NormalMap)
                r.Errors.Add(where + ": " + prop + " texture type is " + ti.textureType + ", expected NormalMap.");
            if (!expectNormal && ti.sRGBTexture != expectSrgb)
                r.Errors.Add(where + ": " + prop + " sRGB=" + ti.sRGBTexture + ", expected " + expectSrgb + ".");
            if (ti.maxTextureSize > budgets.textures.maxSize)
                r.Warnings.Add(where + ": " + prop + " importer max size " + ti.maxTextureSize + ".");
            var android = ti.GetPlatformTextureSettings("Android");
            if (android == null || !android.overridden)
                r.Warnings.Add(where + ": " + prop + " has no Android override (ASTC recommended).");
        }

        private static void CheckSpecies(string modelName, HashSet<string> bones, bool skinned, Report r)
        {
            var m = SpeciesRegex.Match(modelName);
            if (!m.Success) { r.Info.Add("Name is not DRG_<species>_<Stage>; species rig checks skipped."); return; }
            var spec = SpeciesCharacterSpec.Find(m.Groups[1].Value.ToLowerInvariant());
            if (spec == null) { r.Errors.Add("Unknown species id in name: " + m.Groups[1].Value); return; }
            if (!skinned) { r.Info.Add("Static model; species rig checks skipped."); return; }
            bool wings = bones.Contains("Wing_L") && bones.Contains("Wing_R");
            if (spec.rig.flightWings && !wings)
                r.Errors.Add(spec.id + " flies (canonical locomotion) but the skin has no Wing_L/Wing_R bones.");
            if (!spec.rig.flightWings && wings)
                r.Info.Add(spec.id + " does not fly; Wing_L/Wing_R present (the GLB exporter keeps them as unweighted placeholders).");
            bool legs = bones.Any(b => b.StartsWith("Fore", StringComparison.Ordinal) || b.StartsWith("Hind", StringComparison.Ordinal));
            if (!spec.rig.walkingLimbs && legs)
                r.Warnings.Add(spec.id + " has no 'walk' locomotion, but the rig has leg bones; quadruped template is not appropriate.");
            if (spec.rig.walkingLimbs && !legs)
                r.Errors.Add(spec.id + " walks (canonical locomotion) but the rig has no leg bones.");
        }

        internal static string Format(string path, Report r)
        {
            var sb = new StringBuilder();
            sb.Append("Dravio character audit ").Append(r.Status).Append(": ").Append(path)
              .Append(" | tris per LOD: ")
              .Append(string.Join(", ", r.TrianglesPerLod.OrderBy(k => k.Key).Select(k => "LOD" + k.Key + "=" + k.Value).ToArray()));
            foreach (var e in r.Errors) sb.Append("\n  ERROR ").Append(e);
            foreach (var w in r.Warnings) sb.Append("\n  WARN  ").Append(w);
            foreach (var i in r.Info) sb.Append("\n  info  ").Append(i);
            sb.Append("\nStructural audit only: animation playback, deformation, shading and device performance need manual review.");
            return sb.ToString();
        }

        [Serializable] private sealed class JsonReport { public string model; public string status; public string[] errors; public string[] warnings; public string[] info; public int[] lodLevels; public int[] lodTriangles; }

        private static void WriteJson(string path, Report r)
        {
            // Outside Assets/ so nothing is imported or committed (Logs/ is git-ignored).
            var dir = System.IO.Path.Combine("Logs", "DravioCharacter");
            Directory.CreateDirectory(dir);
            var levels = r.TrianglesPerLod.Keys.OrderBy(k => k).ToArray();
            var j = new JsonReport
            {
                model = path, status = r.Status, errors = r.Errors.ToArray(), warnings = r.Warnings.ToArray(), info = r.Info.ToArray(),
                lodLevels = levels, lodTriangles = levels.Select(k => r.TrianglesPerLod[k]).ToArray()
            };
            File.WriteAllText(System.IO.Path.Combine(dir, System.IO.Path.GetFileNameWithoutExtension(path) + ".validation.json"), JsonUtility.ToJson(j, true));
        }
    }
}
