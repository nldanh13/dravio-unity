using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Dravio.Editor
{
    /// <summary>Non-destructive second visual slice. Generated art is still procedural.</summary>
    public static class AncientSanctuaryBuilder
    {
        const string Root = "Assets/Dravio";
        const string Source = Root + "/Scenes/OriginMeadow.unity";
        const string Target = Root + "/Scenes/AncientSanctuary.unity";
        const string Art = Root + "/Art/U1";

        [MenuItem("Dravio/Create or Open Ancient Sanctuary")]
        public static void CreateOrOpen()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Target) != null)
            {
                EditorSceneManager.OpenScene(Target);
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Source) == null)
            {
                Debug.LogError("Dravio: Create the Origin Prototype first.");
                return;
            }
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) { Debug.LogError("Dravio: URP Lit unavailable."); return; }
            Directory.CreateDirectory(Art);
            AssetDatabase.Refresh();
            // Create a separate scene file before modifying any object in the original.
            var scene = EditorSceneManager.OpenScene(Source);
            var dragon = Object.FindFirstObjectByType<DragonTurntable>();
            var cam = Camera.main;
            var envObject = GameObject.Find("Origin Meadow - Blockout");
            if (dragon == null || cam == null || envObject == null)
            {
                Debug.LogError("Dravio: U0 scene structure not found.");
                return;
            }

            if (!EditorSceneManager.SaveScene(scene, Target))
            {
                Debug.LogError("Dravio: could not create separate U1 scene; U0 is unchanged.");
                return;
            }

            Material stone = Mat(shader,"Stone","#AAA18C",.18f);
            Material stoneLight = Mat(shader,"StoneLight","#D2C2A3",.16f);
            Material stoneDark = Mat(shader,"StoneShadow","#777C6D",.12f);
            Material moss = Mat(shader,"Moss","#557A37",.08f);
            Material leaf = Mat(shader,"Leaf","#79A842",.20f);
            Material leafLight = Mat(shader,"LeafLight","#B5CF62",.16f);
            Material forest = Mat(shader,"Forest","#355E45",.10f);
            Material wood = Mat(shader,"Wood","#735139",.16f);
            Material orange = Mat(shader,"DragonOrange","#D86B47",.32f);
            Material belly = Mat(shader,"DragonBelly","#F9D59A",.28f);
            Material dark = Mat(shader,"DragonAccent","#A83D36",.24f);
            Material iris = Mat(shader,"Iris","#A07A22",.55f);
            Material pupil = Mat(shader,"Pupil","#30211B",.65f);
            Material shine = Mat(shader,"Glint","#FFF6DA",.65f);
            Material amber = Mat(shader,"Flame","#FFB43E",.35f);
            Material flowerPink = Mat(shader,"PetalPink","#E88BB6",.25f);
            Material flowerBlue = Mat(shader,"PetalBlue","#74C5D6",.25f);
            Material flowerGold = Mat(shader,"PetalGold","#FFD36A",.25f);

            cam.orthographic = true;
            cam.orthographicSize = 4.15f;
            cam.transform.position = new Vector3(0,6.5f,-12);
            cam.transform.LookAt(new Vector3(0,1.65f,1.2f));
            cam.backgroundColor = new Color(.66f,.77f,.57f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.73f,.73f,.62f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = cam.backgroundColor;
            RenderSettings.fogStartDistance = 18;
            RenderSettings.fogEndDistance = 37;
            var sun = GameObject.Find("Warm Sun")?.GetComponent<Light>();
            if (sun != null)
            {
                sun.color = new Color(1,.91f,.73f);
                sun.intensity = 1.5f;
                sun.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(49,-35,0);
            }

            Transform env = envObject.transform;
            env.name = "Ancient Sanctuary - Procedural";
            Transform ground = env.Find("Meadow");
            ground.localScale = new Vector3(80,.4f,80);
            ground.localPosition = new Vector3(0,-.22f,8);
            Material meadow = Mat(shader,"Meadow","#FFFFFF",.06f);
            meadow.SetTexture("_BaseMap", MeadowTexture());
            meadow.SetTextureScale("_BaseMap", new Vector2(6,6));
            ground.GetComponent<Renderer>().sharedMaterial = meadow;
            EditorUtility.SetDirty(meadow);

            // Remove sparse U0 decorations only from the separate U1 scene.
            for (int i=env.childCount-1;i>=0;i--)
            {
                var child=env.GetChild(i);
                if (child.name=="Flower" || child.name=="Mossy Rock" ||
                    child.name.StartsWith("Canopy") || child.name=="Tree Trunk")
                    Object.DestroyImmediate(child.gameObject);
                else if(child.name=="Arch Stone")
                    child.GetComponent<Renderer>().sharedMaterial = i%3==0?stoneLight:stone;
            }

            var rng = new System.Random(914);
            float Rand(float a,float b) => a+(float)rng.NextDouble()*(b-a);
            Transform landscape = new GameObject("Layered forest and ruins").transform;
            landscape.SetParent(env,false);

            // Layered silhouettes, framing foliage and a walkable central clearing.
            for(int i=0;i<15;i++)
            {
                float x=-15+i*2.1f, z=Rand(9,17), h=Rand(3.8f,6.8f);
                Shape(landscape,"Distant trunk",PrimitiveType.Cylinder,new Vector3(x,h*.37f,z),new Vector3(.5f,h*.37f,.5f),wood);
                Shape(landscape,"Distant canopy",PrimitiveType.Sphere,new Vector3(x,h,z),new Vector3(Rand(3,5),Rand(3,4.5f),3.5f),forest);
            }
            foreach(int side in new[]{-1,1})
            {
                float x=side*7.1f;
                Shape(landscape,"Old tree",PrimitiveType.Cylinder,new Vector3(x,2.15f,3.8f),new Vector3(1.2f,2.2f,1.05f),wood);
                for(int i=0;i<7;i++)
                    Shape(landscape,"Crown",PrimitiveType.Sphere,new Vector3(x+Rand(-1.6f,1.6f),Rand(4.1f,5.9f),Rand(2.7f,5.3f)),new Vector3(Rand(2.5f,4.4f),Rand(1.8f,3),Rand(2.4f,3.5f)),i%3==0?leafLight:leaf);
                for(int i=0;i<7;i++)
                {
                    float y=.4f+i*.46f;
                    var rock=Shape(landscape,"Ruined column block",PrimitiveType.Cube,new Vector3(side*5.55f,y,5.4f),new Vector3(.95f,.44f,1.0f),i%2==0?stone:stoneLight);
                    rock.localRotation=Quaternion.Euler(0,Rand(-8,8),Rand(-3,3));
                }
                for(int i=0;i<11;i++)
                {
                    float y=3.8f-i*.25f;
                    float vx=side*3.2f+.18f*Mathf.Sin(i*1.2f);
                    Shape(landscape,"Trailing vine",PrimitiveType.Sphere,new Vector3(vx,y,3.55f),new Vector3(.12f,.38f,.12f),moss);
                    var l=Shape(landscape,"Vine leaf",PrimitiveType.Sphere,new Vector3(vx+(i%2==0?.17f:-.17f),y,3.42f),new Vector3(.40f,.16f,.18f),i%3==0?leafLight:leaf);
                    l.localRotation=Quaternion.Euler(0,0,i%2==0?30:-30);
                }
            }

            // Path stones behind and beside the dragon, not a navigation mesh.
            for(int i=0;i<12;i++)
            {
                float z=2.2f+i*.49f;
                var rock=Shape(landscape,"Ancient stepping stone",PrimitiveType.Sphere,new Vector3(.4f*Mathf.Sin(i*.55f),.04f,z),new Vector3(Rand(.9f,1.5f),.13f,Rand(.5f,.8f)),i%2==0?stoneLight:stone);
                rock.localRotation=Quaternion.Euler(0,Rand(0,180),0);
            }
            for(int i=0;i<25;i++)
            {
                float x=Rand(-8,8),z=Rand(-3.4f,7.5f);
                if(Mathf.Abs(x)<2.25f && z<3) continue;
                if(Mathf.Abs(x)<.95f && z>=2.2f) continue;
                Shape(landscape,"Moss-covered stone",PrimitiveType.Sphere,new Vector3(x,.13f,z),new Vector3(Rand(.45f,1),.32f,Rand(.42f,.85f)),stoneDark);
                Shape(landscape,"Moss cap",PrimitiveType.Sphere,new Vector3(x,.28f,z),new Vector3(.6f,.13f,.48f),moss);
                for(int j=0;j<2;j++)
                {
                    float dx=Rand(-.24f,.24f), dz=Rand(-.24f,.24f);
                    Flower(landscape,new Vector3(x+dx,.36f,z+dz),j==0?flowerPink:j==1?flowerGold:flowerBlue,flowerGold,leaf);
                }
            }

            // Refine the character without changing its canonical anatomy or the U0 asset.
            Transform d=dragon.transform;
            d.name="Emberling - Baby Normal U1";
            d.localScale=Vector3.one*1.33f;
            d.localPosition=new Vector3(0,0,-.65f);
            d.rotation=Quaternion.Euler(0,148,0);
            for(int i=d.childCount-1;i>=0;i--)
            {
                Transform ch=d.GetChild(i);
                if(ch.name=="Eye" || ch.name=="Glint" || ch.name=="Tail" || ch.name=="Round Flame" || ch.name=="Flame Tip")
                    Object.DestroyImmediate(ch.gameObject);
                else
                {
                    Renderer rr=ch.GetComponent<Renderer>();
                    if(rr!=null) rr.sharedMaterial=ch.name=="Chest"||ch.name=="Muzzle"?belly:ch.name=="Nostril"?dark:orange;
                }
            }
            foreach(int side in new[]{-1,1})
            {
                Vector3 center=new Vector3(side*.37f,1.60f,1.01f);
                var socket=Shape(d,"Eye surround",PrimitiveType.Sphere,center,new Vector3(.45f,.54f,.30f),dark);
                socket.localRotation=Quaternion.Euler(0,side*22,0);
                Shape(d,"Golden iris",PrimitiveType.Sphere,center+new Vector3(side*.025f,.005f,.115f),new Vector3(.32f,.40f,.15f),iris);
                Shape(d,"Pupil",PrimitiveType.Sphere,center+new Vector3(side*.025f,.01f,.17f),new Vector3(.205f,.31f,.08f),pupil);
                Shape(d,"Eye catchlight",PrimitiveType.Sphere,center+new Vector3(-.035f,.115f,.215f),new Vector3(.075f,.09f,.035f),shine);
                Shape(d,"Eye small glint",PrimitiveType.Sphere,center+new Vector3(.065f,-.07f,.21f),new Vector3(.034f,.04f,.025f),shine);
                Shape(d,"Cheek",PrimitiveType.Sphere,new Vector3(side*.40f,1.30f,1.0f),new Vector3(.31f,.24f,.27f),orange);
                for(int i=-1;i<=1;i++)
                {
                    Shape(d,"Front claw",PrimitiveType.Sphere,new Vector3(side*.43f+i*.10f,.105f,.84f),new Vector3(.075f,.10f,.12f),belly);
                    Shape(d,"Rear claw",PrimitiveType.Sphere,new Vector3(side*.48f+i*.11f,.105f,-.11f),new Vector3(.075f,.10f,.12f),belly);
                }
            }
            for(int i=0;i<4;i++)
                Shape(d,"Rounded dorsal scale",PrimitiveType.Sphere,new Vector3(0,1.14f-i*.04f,-.3f-i*.15f),new Vector3(.16f,.22f,.20f),dark);
            // Continuous tapered tail mesh, rather than visible stacked spheres.
            var tailPoints=new List<Vector3>();
            var radii=new List<float>();
            for(int i=0;i<=24;i++)
            {
                float t=i/24f;
                tailPoints.Add(new Vector3(.18f*Mathf.Sin(t*Mathf.PI),.58f+.65f*t*t,-.61f-1.08f*t));
                radii.Add(Mathf.Lerp(.26f,.10f,t));
            }
            var tailMesh=Tube(tailPoints,radii);
            string meshPath=Art+"/EmberlingTail.asset";
            Mesh saved=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if(saved==null){ AssetDatabase.CreateAsset(tailMesh,meshPath); saved=tailMesh; }
            else Object.DestroyImmediate(tailMesh);
            var tail=new GameObject("Continuous tail",typeof(MeshFilter),typeof(MeshRenderer));
            tail.transform.SetParent(d,false);
            tail.GetComponent<MeshFilter>().sharedMesh=saved;
            tail.GetComponent<MeshRenderer>().sharedMaterial=orange;
            Shape(d,"Round flame",PrimitiveType.Sphere,new Vector3(0,1.30f,-1.70f),new Vector3(.44f,.60f,.45f),amber,true);
            Shape(d,"Flame highlight",PrimitiveType.Sphere,new Vector3(-.025f,1.48f,-1.74f),new Vector3(.23f,.34f,.23f),belly);

            AddHud();
            if(!EditorSceneManager.SaveScene(scene,Target))
            { Debug.LogError("Dravio: could not save U1 scene. U0 source not overwritten."); return; }
            AssetDatabase.SaveAssets();
            Selection.activeGameObject=d.gameObject;
            Debug.Log("Dravio: Ancient Sanctuary created. Play to rotate Emberling. U0 preserved.");
        }

        static Material Mat(Shader shader,string name,string hex,float smooth)
        {
            string path=Art+"/"+name+".mat";
            var existing=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(existing!=null) return existing;
            var mat=new Material(shader){name=name};
            if(ColorUtility.TryParseHtmlString(hex,out var c)) mat.SetColor("_BaseColor",c);
            mat.SetFloat("_Smoothness",smooth);
            AssetDatabase.CreateAsset(mat,path);
            return mat;
        }

        static Texture2D MeadowTexture()
        {
            string path=Art+"/MeadowTexture.asset";
            var previous=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if(previous!=null) return previous;
            const int n=256;
            var texture=new Texture2D(n,n,TextureFormat.RGBA32,true);
            var pixels=new Color32[n*n];
            var low=new Color(.36f,.48f,.19f);
            var high=new Color(.68f,.77f,.36f);
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                float a=Mathf.PerlinNoise(x*.037f+9,y*.037f+7);
                float b=Mathf.PerlinNoise(x*.19f+13,y*.19f+3);
                // Fade edges to same colour to reduce repetition seams.
                float edge=Mathf.Clamp01(Mathf.Min(Mathf.Min(x,n-1-x),Mathf.Min(y,n-1-y))/16f);
                float f=Mathf.Lerp(.5f,Mathf.Clamp01(a*.72f+b*.28f),edge);
                pixels[y*n+x]=(Color32)Color.Lerp(low,high,f);
            }
            texture.SetPixels32(pixels); texture.wrapMode=TextureWrapMode.Repeat;
            texture.filterMode=FilterMode.Trilinear; texture.Apply(true,false);
            AssetDatabase.CreateAsset(texture,path);
            return texture;
        }

        static Transform Shape(Transform parent,string name,PrimitiveType kind,Vector3 p,Vector3 size,Material mat,bool collider=false)
        {
            var go=GameObject.CreatePrimitive(kind);
            go.name=name;go.transform.SetParent(parent,false);
            go.transform.localPosition=p;go.transform.localScale=size;
            go.GetComponent<Renderer>().sharedMaterial=mat;
            if(!collider)Object.DestroyImmediate(go.GetComponent<Collider>());
            return go.transform;
        }

        static void Flower(Transform parent,Vector3 p,Material petals,Material center,Material leaves)
        {
            Shape(parent,"Stem",PrimitiveType.Cylinder,p-Vector3.up*.09f,new Vector3(.035f,.12f,.035f),leaves);
            for(int k=0;k<5;k++)
            {
                float a=k*Mathf.PI*2/5;
                Shape(parent,"Petal",PrimitiveType.Sphere,p+new Vector3(Mathf.Cos(a)*.083f,0,Mathf.Sin(a)*.083f),new Vector3(.14f,.065f,.14f),petals);
            }
            Shape(parent,"Flower center",PrimitiveType.Sphere,p+Vector3.up*.024f,new Vector3(.09f,.07f,.09f),center);
        }

        static Mesh Tube(List<Vector3> centers,List<float> radii)
        {
            const int ring=16;
            var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int i=0;i<centers.Count;i++)
            {
                Vector3 tangent=(centers[Mathf.Min(i+1,centers.Count-1)]-centers[Mathf.Max(i-1,0)]).normalized;
                Vector3 a=Vector3.Cross(tangent,Vector3.right).normalized;
                Vector3 b=Vector3.Cross(tangent,a).normalized;
                for(int j=0;j<ring;j++)
                {
                    float angle=j*Mathf.PI*2/ring;
                    vertices.Add(centers[i]+(a*Mathf.Cos(angle)+b*Mathf.Sin(angle))*radii[i]);
                    if(i==0)continue;
                    int p=(i-1)*ring+j,q=(i-1)*ring+(j+1)%ring,r=i*ring+j,s=i*ring+(j+1)%ring;
                    triangles.Add(p);triangles.Add(q);triangles.Add(r);
                    triangles.Add(q);triangles.Add(s);triangles.Add(r);
                }
            }
            var mesh=new Mesh{name="Emberling smooth tapered tail"};
            mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);
            mesh.RecalculateNormals();mesh.RecalculateBounds();
            return mesh;
        }

        static void AddHud()
        {
            var go=new GameObject("U1 Visual HUD",typeof(Canvas),typeof(CanvasScaler));
            var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
            Font font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if(font==null)return;
            var title=new GameObject("Title",typeof(RectTransform),typeof(Image));
            title.transform.SetParent(go.transform,false);
            var tr=title.GetComponent<RectTransform>();tr.anchorMin=new Vector2(0,1);tr.anchorMax=new Vector2(0,1);tr.pivot=new Vector2(0,1);
            tr.anchoredPosition=new Vector2(20,-20);tr.sizeDelta=new Vector2(350,77);
            title.GetComponent<Image>().color=new Color(.24f,.18f,.12f,.92f);
            Label(title.transform,font,"DRAVIO  /  ORIGIN SANCTUARY",22,new Color(1,.86f,.58f),TextAnchor.MiddleCenter);
            var hint=new GameObject("Rotation hint",typeof(RectTransform),typeof(Image));
            hint.transform.SetParent(go.transform,false);
            var hr=hint.GetComponent<RectTransform>();hr.anchorMin=new Vector2(0,0);hr.anchorMax=new Vector2(1,0);hr.pivot=new Vector2(.5f,0);
            hr.anchoredPosition=new Vector2(0,18);hr.sizeDelta=new Vector2(-40,50);
            hint.GetComponent<Image>().color=new Color(.24f,.18f,.12f,.88f);
            Label(hint.transform,font,"Emberling  |  Drag to rotate  |  Arrow keys  |  R: reset",19,new Color(1,.93f,.75f),TextAnchor.MiddleCenter);
        }

        static void Label(Transform parent,Font font,string value,int size,Color color,TextAnchor anchor)
        {
            var go=new GameObject("Text",typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);
            var rect=go.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=Vector2.zero;rect.offsetMax=Vector2.zero;
            var text=go.GetComponent<Text>();text.font=font;text.text=value;text.fontSize=size;text.resizeTextForBestFit=true;text.resizeTextMinSize=11;text.resizeTextMaxSize=size;text.color=color;text.alignment=anchor;text.raycastTarget=false;
        }
    }
}
