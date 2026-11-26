using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Dravio.Editor
{
    internal static class EmberlingPolishBuilder
    {
        const string Art = "Assets/Dravio/Art/Polish";
        public static void Apply(GameObject dragon, GameObject visual, Camera camera, bool preserveImportedMaterials = false)
        {
            Directory.CreateDirectory(Art);
            AssetDatabase.Refresh();
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var spark = Shader.Find("Dravio/EmberSpark");
            if(lit==null || spark==null) throw new InvalidOperationException("Missing URP Lit or Dravio spark shader.");
            if(!preserveImportedMaterials)
            {
                var body = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .FirstOrDefault(r => r.name.Contains("Emberling_Baby_Body",StringComparison.OrdinalIgnoreCase));
                if(body==null || body.sharedMesh==null)
                    throw new InvalidOperationException("Polished FBX body mesh missing.");
                if(!body.sharedMesh.HasVertexAttribute(VertexAttribute.TexCoord0))
                    throw new InvalidOperationException("Polished FBX is missing BodyPaint UV.");
                var material = NewMaterial(lit,"EmberlingPaint");
                material.SetColor("_BaseColor",Color.white);
                material.SetTexture("_BaseMap",SkinTexture());
                material.SetFloat("_Smoothness",.19f);
                material.SetFloat("_Metallic",0f);
                var slots=body.sharedMaterials;
                for(int i=0;i<slots.Length;i++)slots[i]=material;
                body.sharedMaterials=slots;

                // Clone flame materials; never change a previously accepted prefab or material.
                foreach(var r in visual.GetComponentsInChildren<Renderer>(true))
                {
                    string name=r.name.ToLowerInvariant();
                    if(!name.Contains("round flame") && !name.Contains("flame core") &&
                        !name.Contains("round_flame") && !name.Contains("flame_core"))continue;
                    bool core=name.Contains("core");
                    var flame=NewMaterial(lit,core?"FlameCore":"FlameShell");
                    flame.SetColor("_BaseColor",core?new Color(1,.78f,.25f):new Color(1,.32f,.035f));
                    flame.SetFloat("_Smoothness",.12f);
                    flame.EnableKeyword("_EMISSION");
                    flame.SetColor("_EmissionColor",core?new Color(2.4f,1.45f,.38f):new Color(1.55f,.35f,.06f));
                    flame.globalIlluminationFlags=MaterialGlobalIlluminationFlags.None;
                    var mats=r.sharedMaterials;
                    for(int i=0;i<mats.Length;i++)mats[i]=flame;
                    r.sharedMaterials=mats;
                }
            }
            var bones=visual.GetComponentsInChildren<Transform>(true);
            var bone=bones.FirstOrDefault(t=>t.name=="TailVFX_Anchor") ?? bones.FirstOrDefault(t=>t.name=="Flame");
            if(bone==null)Debug.LogWarning("Dravio: Flame bone not found, tail sparks skipped.");
            else AddSparks(dragon.transform,bone,spark);
            ConfigureCamera(camera);
            AssetDatabase.SaveAssets();
        }

        static Material NewMaterial(Shader shader,string name)
        {
            var mat=new Material(shader){name=name};
            AssetDatabase.CreateAsset(mat,AssetDatabase.GenerateUniqueAssetPath(Art+"/"+name+".mat"));
            return mat;
        }

        // U wraps around the body, V is longitudinal. A seamless U-scale pattern,
        // broad honey belly transition and subtle deterministic painterly variation.
        static Texture2D SkinTexture()
        {
            const int n=512;
            var tex=new Texture2D(n,n,TextureFormat.RGBA32,true,false){name="Emberling painted scale texture"};
            var pixels=new Color32[n*n];
            var top=new Color(.68f,.26f,.115f);
            var side=new Color(.88f,.43f,.20f);
            var belly=new Color(.98f,.75f,.43f);
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                float u=(float)x/n,v=(float)y/(n-1);
                float angle=u*2*Mathf.PI;
                float under=.5f-.5f*Mathf.Cos(angle);
                float cream=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.63f,.98f,under));
                cream*=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.23f,.41f,v));
                Color c=Color.Lerp(Color.Lerp(top,side,Mathf.Sqrt(under)),belly,cream);
                float row=v*24;
                float stagger=Mathf.Floor(row)%2==0?0:.5f;
                float a=Mathf.Repeat(u*28+stagger,1)-.5f;
                float b=Mathf.Repeat(row,1)-.5f;
                float r=Mathf.Sqrt(a*a+b*b);
                float rim=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.32f,.50f,r));
                float scaleDetail=(1-cream*.8f)*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.76f,.95f,v))*.75f);
                // Periodic noise around U seam.
                float noise=Mathf.PerlinNoise(17+Mathf.Cos(angle)*3.5f+v*5,19+Mathf.Sin(angle)*3.5f+v*8)-.5f;
                c*=1+noise*.12f-rim*.12f*scaleDetail;
                c.a=1;
                pixels[y*n+x]=(Color32)c;
            }
            tex.SetPixels32(pixels);tex.wrapModeU=TextureWrapMode.Repeat;
            tex.wrapModeV=TextureWrapMode.Clamp;tex.filterMode=FilterMode.Trilinear;
            tex.anisoLevel=2;tex.Apply(true,false);
            AssetDatabase.CreateAsset(tex,AssetDatabase.GenerateUniqueAssetPath(Art+"/EmberlingPaint.asset"));
            return tex;
        }

        static void AddSparks(Transform parent,Transform bone,Shader shader)
        {
            var obj=new GameObject("Tail warmth and embers");
            obj.transform.SetParent(parent,false);
            obj.transform.position=bone.position;
            obj.transform.rotation=Quaternion.identity;
            var particles=obj.AddComponent<ParticleSystem>();
            particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=particles.main;
            main.loop=true;main.playOnAwake=true;main.maxParticles=24;
            main.startLifetime=new ParticleSystem.MinMaxCurve(.45f,.85f);
            main.startSpeed=new ParticleSystem.MinMaxCurve(.08f,.24f);
            main.startSize=new ParticleSystem.MinMaxCurve(.035f,.085f);
            main.startColor=new ParticleSystem.MinMaxGradient(new Color(1,.27f,.035f),new Color(1,.79f,.23f));
            main.simulationSpace=ParticleSystemSimulationSpace.World;
            main.gravityModifier=-.08f;
            var emission=particles.emission;emission.rateOverTime=8;
            var shape=particles.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.08f;
            var fade=particles.colorOverLifetime;fade.enabled=true;
            var gradient=new Gradient();
            gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(new Color(1,.35f,.04f),1)},
                new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.9f,.13f),new GradientAlphaKey(0,1)});
            fade.color=new ParticleSystem.MinMaxGradient(gradient);
            var renderer=particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode=ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial=NewMaterial(shader,"EmberSparks");
            renderer.shadowCastingMode=ShadowCastingMode.Off;
            renderer.receiveShadows=false;
            var light=obj.AddComponent<Light>();light.type=LightType.Point;
            light.color=new Color(1,.49f,.13f);light.range=1.8f;light.intensity=.45f;light.shadows=LightShadows.None;
            obj.AddComponent<EmberlingFlameVisual>().Configure(bone,light);
        }

        static void ConfigureCamera(Camera camera)
        {
            if(camera==null)return;
            camera.allowHDR=true;
            var data=camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing=true;
            data.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.volumeLayerMask = data.volumeLayerMask.value | 1;
            var profile=ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile,AssetDatabase.GenerateUniqueAssetPath(Art+"/SanctuaryPolish.asset"));
            var bloom=profile.Add<Bloom>(true);
            bloom.threshold.Override(1.1f);bloom.intensity.Override(.28f);
            bloom.scatter.Override(.65f);
            AssetDatabase.AddObjectToAsset(bloom,profile);
            var color=profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(.08f);color.contrast.Override(6f);
            color.saturation.Override(4f);color.colorFilter.Override(new Color(1,.98f,.94f));
            AssetDatabase.AddObjectToAsset(color,profile);
            EditorUtility.SetDirty(profile);
            var go=new GameObject("U1.4 Local scene post-processing",typeof(Volume));
            var volume=go.GetComponent<Volume>();volume.isGlobal=true;volume.priority=10;volume.sharedProfile=profile;
            // Deliberately no edits to the shared URP renderer or quality settings.
        }
    }
}
