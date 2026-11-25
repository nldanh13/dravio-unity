using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dravio.Editor
{
    internal static class EmberlingAssetAudit
    {
        private const string RefinedPath="Assets/Dravio/Art/Models/EmberlingRefined.fbx";
        private static readonly string[] Bones={
            "Root","Pelvis","Chest","Head","Tail1","Tail2","Flame",
            "Fore_L","Fore_R","Hind_L","Hind_R","Eye_L","Eye_R",
            "ForeLower_L","ForeLower_R","HindLower_L","HindLower_R"
        };
        private static readonly (string name,float seconds,bool loop)[] Clips={
            ("Idle",6f,true),("Walk",35f/30f,true),("Eat",2f,false),
            ("Doze",4f,true),("Happy",1.5f,false)
        };

        [MenuItem("Dravio/Character/Validate Refined Emberling FBX")]
        static void ValidateMenu()
        {
            // Read-only audit. Initial raw FBX must first be configured by the preview importer.
            if (Validate(RefinedPath,out var report))
                Debug.Log(report);
            else Debug.LogError(report);
        }

        [MenuItem("Dravio/Character/Validate Refined GLB FBX")]
        static void ValidateGlbMenu()
        {
            if(Validate("Assets/Dravio/Art/Models/EmberlingRefinedGLB.fbx",out var report)) Debug.Log(report);
            else Debug.LogError(report);
        }

        internal static bool Validate(string path,out string report)
        {
            var errors=new List<string>();
            var warnings=new List<string>();
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(model==null){report="Dravio audit: model not imported: "+path;return false;}
            var importer=AssetImporter.GetAtPath(path) as ModelImporter;
            if(importer==null || importer.animationType!=ModelImporterAnimationType.Generic)
                errors.Add("Generic rig not configured; run Create or Open Refined Preview.");
            var assets=AssetDatabase.LoadAllAssetsAtPath(path);
            if(!assets.OfType<Avatar>().Any(a=>a.isValid && !a.isHuman))
                errors.Add("No valid Generic Avatar.");
            var names=new HashSet<string>(model.GetComponentsInChildren<Transform>(true).Select(t=>t.name));
            foreach(var name in Bones)if(!names.Contains(name))errors.Add("Missing bone: "+name);
            bool sourceGlb=System.IO.Path.GetFileNameWithoutExtension(path)=="EmberlingRefinedGLB";
            if(sourceGlb)
                foreach(var name in new[]{"Spine","Neck","Wing_L","Wing_R","TailVFX_Anchor"})
                    if(!names.Contains(name))errors.Add("Missing GLB rig bone: "+name);
            var clips=assets.OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__",StringComparison.Ordinal)).ToArray();
            foreach(var expected in Clips)
            {
                var matches=clips.Where(c=>c.name==expected.name).ToArray();
                if(matches.Length!=1){errors.Add("Expected one clip: "+expected.name);continue;}
                var clip=matches[0];
                if(Mathf.Abs(clip.length-expected.seconds)>.09f)
                    errors.Add(expected.name+" duration: "+clip.length.ToString("F3")+" s.");
                if(AnimationUtility.GetAnimationClipSettings(clip).loopTime!=expected.loop)
                    errors.Add(expected.name+" has incorrect loop flag.");
            }
            var renderers=model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var body=renderers.FirstOrDefault(r=>r.name.Contains("Emberling_Baby_Body",StringComparison.OrdinalIgnoreCase));
            if(body==null || body.sharedMesh==null)errors.Add("Skinned body missing.");
            else if(!body.sharedMesh.HasVertexAttribute(VertexAttribute.TexCoord0))
            {
                if(!sourceGlb)errors.Add("Body UV0 missing.");
                else
                {
                    // Imported GLBs can be solid-colored or use vertex colors instead
                    // of UV textures. Permit geometry/animation preview, not texture acceptance.
                    warnings.Add("GLB body UV0 missing. Geometry/animation preview only; UV-based texture fidelity is NOT validated.");
                    if(body.sharedMesh.HasVertexAttribute(VertexAttribute.Color))
                        warnings.Add("Vertex colors present. URP Lit may not display them without a vertex-color material.");
                    else
                        warnings.Add("No vertex color channel detected. Check source GLB UVs/materials in Blender.");
                }
            }
            long vertices=0,triangles=0;
            foreach(var renderer in renderers)
            {
                var mesh=renderer.sharedMesh;
                if(mesh==null){errors.Add("Missing mesh: "+renderer.name);continue;}
                if(renderer.bones.Length==0 || renderer.bones.Any(b=>b==null))
                    errors.Add("Missing skin bone references: "+renderer.name);
                vertices+=mesh.vertexCount;
                for(int i=0;i<mesh.subMeshCount;i++)
                    if(mesh.GetTopology(i)==MeshTopology.Triangles)triangles+=mesh.GetIndexCount(i)/3;
            }
            if(triangles>30000)warnings.Add("Prototype exceeds provisional 30k triangle review threshold.");
            if(renderers.Length>24)warnings.Add("Many skinned renderers: profile draw calls on mobile.");
            report="Dravio Refined FBX audit: "+(errors.Count==0?(warnings.Count==0?"PASS (asset structure only)":"PASS WITH WARNINGS (preview only)"):"FAIL")+
                "\nSkinned renderers: "+renderers.Length+"; vertices: "+vertices+"; triangles: "+triangles+
                "\n"+string.Join("\n",errors.Select(e=>"ERROR: "+e).Concat(warnings.Select(w=>"WARNING: "+w)))+
                "\nVisual deformation, shader, playback and device performance still require testing.";
            return errors.Count==0;
        }
    }
}
