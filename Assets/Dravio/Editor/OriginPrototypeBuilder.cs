using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Dravio.Editor
{
    public static class OriginPrototypeBuilder
    {
        private const string Root = "Assets/Dravio";
        private const string ScenePath = Root + "/Scenes/OriginMeadow.unity";

        [MenuItem("Dravio/Create or Open Origin Prototype")]
        public static void CreateOrOpen()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                EditorSceneManager.OpenScene(ScenePath);
                return;
            }

            Directory.CreateDirectory(Root + "/Scenes");
            Directory.CreateDirectory(Root + "/Art/Materials");
            AssetDatabase.Refresh();
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("Dravio: URP Lit shader not available. Resolve URP packages first.");
                return;
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.64f, .67f, .52f);
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.66f, .80f, .69f);
            camera.transform.position = new Vector3(0, 7, -13);
            camera.transform.LookAt(new Vector3(0, 1.0f, 1));
            var sun = new GameObject("Warm Sun", typeof(Light)).GetComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, .89f, .68f);
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(42, -32, 0);

            var stone = Mat(shader, "WeatheredStone", "#B0A18A");
            var moss = Mat(shader, "Moss", "#658C43");
            var grass = Mat(shader, "MeadowGrass", "#8BA852");
            var trunk = Mat(shader, "WarmWood", "#71523A");
            var gold = Mat(shader, "FlowerGold", "#FFCE63");
            var pink = Mat(shader, "FlowerPink", "#F08BA6");
            var orange = Mat(shader, "EmberlingBody", "#D86B47");
            var cream = Mat(shader, "EmberlingBelly", "#F9D59A");
            var dark = Mat(shader, "EmberlingAccent", "#A83D36");
            var eye = Mat(shader, "Eye", "#392E2A");
            var white = Mat(shader, "EyeGlint", "#FFF9E9");

            var environment = new GameObject("Origin Meadow - Blockout").transform;
            Shape(environment, "Meadow", PrimitiveType.Cube, new Vector3(0,-.22f,1), new Vector3(19,.4f,15), grass, true);
            // Ruined arch behind the walkable area; not a gameplay obstacle map yet.
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 3.2f;
                Shape(environment, "Plinth", PrimitiveType.Cube, new Vector3(x,.25f,4.1f), new Vector3(1.35f,.5f,1.4f), stone);
                Shape(environment, "Pillar", PrimitiveType.Cylinder, new Vector3(x,1.8f,4.1f), new Vector3(.88f,1.5f,.88f), stone);
                Shape(environment, "Capital", PrimitiveType.Cube, new Vector3(x,3.35f,4.1f), new Vector3(1.3f,.4f,1.3f), stone);
                Shape(environment, "Moss", PrimitiveType.Sphere, new Vector3(x,3.58f,4.1f), new Vector3(1.3f,.3f,1.2f), moss);
            }
            for (int i = 0; i <= 10; i++)
            {
                float a = Mathf.PI * i / 10;
                var block = Shape(environment, "Arch Stone", PrimitiveType.Cube,
                    new Vector3(Mathf.Cos(a)*3.2f,3.55f+Mathf.Sin(a)*1.7f,4.1f),
                    new Vector3(1.05f,.72f,1.05f), stone);
                block.localRotation = Quaternion.Euler(0,0,Mathf.Atan2(1.7f*Mathf.Cos(a),-3.2f*Mathf.Sin(a))*Mathf.Rad2Deg);
            }

            var random = new System.Random(71);
            for (int i = 0; i < 22; i++)
            {
                float x = (float)(random.NextDouble()*16-8);
                float z = (float)(random.NextDouble()*9-3);
                if (Mathf.Abs(x)<2.6f && z<2.4f) continue;
                Shape(environment, "Mossy Rock", PrimitiveType.Sphere, new Vector3(x,.12f,z), new Vector3(.8f,.32f,.6f), moss);
                Shape(environment, "Flower", PrimitiveType.Sphere, new Vector3(x,.32f,z), new Vector3(.22f,.16f,.22f), i%2==0?gold:pink);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side*7.4f;
                Shape(environment,"Tree Trunk",PrimitiveType.Cylinder,new Vector3(x,1.55f,5),new Vector3(.62f,1.55f,.62f),trunk);
                Shape(environment,"Canopy",PrimitiveType.Sphere,new Vector3(x,4,5),new Vector3(4.3f,2.8f,3.5f),moss);
                Shape(environment,"Canopy Light",PrimitiveType.Sphere,new Vector3(x-.5f,4.6f,4.9f),new Vector3(2.7f,1.6f,2.3f),grass);
            }

            var dragon = new GameObject("Emberling - Baby Normal (Prototype)").transform;
            // Canonical Emberling: four legs, round flame tail, no wings.
            Shape(dragon,"Body",PrimitiveType.Sphere,new Vector3(0,.71f,-.12f),new Vector3(1.14f,1.06f,1.56f),orange,true);
            Shape(dragon,"Chest",PrimitiveType.Sphere,new Vector3(0,.76f,.48f),new Vector3(.84f,.88f,.72f),cream,true);
            Shape(dragon,"Head",PrimitiveType.Sphere,new Vector3(0,1.49f,.67f),new Vector3(1.2f,1.1f,1.1f),orange,true);
            Shape(dragon,"Muzzle",PrimitiveType.Sphere,new Vector3(0,1.23f,1.12f),new Vector3(.84f,.54f,.64f),cream,true);
            for (int side = -1; side <= 1; side += 2)
            {
                Shape(dragon,"Front Leg",PrimitiveType.Sphere,new Vector3(side*.38f,.36f,.41f),new Vector3(.46f,.68f,.46f),orange,true);
                Shape(dragon,"Front Paw",PrimitiveType.Sphere,new Vector3(side*.43f,.16f,.58f),new Vector3(.5f,.3f,.64f),orange,true);
                Shape(dragon,"Rear Leg",PrimitiveType.Sphere,new Vector3(side*.45f,.46f,-.48f),new Vector3(.62f,.7f,.74f),orange,true);
                Shape(dragon,"Rear Paw",PrimitiveType.Sphere,new Vector3(side*.48f,.16f,-.36f),new Vector3(.56f,.3f,.64f),orange,true);
                Shape(dragon,"Eye",PrimitiveType.Sphere,new Vector3(side*.39f,1.59f,1.02f),new Vector3(.41f,.5f,.31f),eye);
                Shape(dragon,"Glint",PrimitiveType.Sphere,new Vector3(side*.39f-.045f,1.69f,1.16f),new Vector3(.09f,.10f,.05f),white);
                Shape(dragon,"Nostril",PrimitiveType.Sphere,new Vector3(side*.135f,1.38f,1.40f),new Vector3(.075f,.06f,.045f),dark);
            }
            for (int i=0;i<=10;i++)
            {
                float t=i/10f, r=.25f-.15f*t;
                Shape(dragon,"Tail",PrimitiveType.Sphere,new Vector3(.18f*Mathf.Sin(t*Mathf.PI),.56f+.65f*t*t,-.65f-.95f*t),new Vector3(r*2,r*2,(r+.09f)*2),orange,true);
            }
            Shape(dragon,"Round Flame",PrimitiveType.Sphere,new Vector3(0,1.2f,-1.68f),new Vector3(.44f,.64f,.46f),gold,true);
            Shape(dragon,"Flame Tip",PrimitiveType.Sphere,new Vector3(0,1.4f,-1.72f),new Vector3(.28f,.42f,.3f),cream,true);
            dragon.rotation = Quaternion.Euler(0, 155, 0);
            dragon.gameObject.AddComponent<DragonTurntable>().SetCamera(camera);

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                Debug.LogError("Dravio: could not save generated scene.");
                return;
            }
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = dragon.gameObject;
            Debug.Log("Dravio: Origin prototype created. Press Play, drag the dragon or use left/right arrows. R resets rotation.");
        }

        private static Material Mat(Shader shader, string name, string hex)
        {
            string path = Root + "/Art/Materials/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var material = new Material(shader) { name = name };
            if (ColorUtility.TryParseHtmlString(hex, out Color color)) material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", .22f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Transform Shape(Transform parent, string name, PrimitiveType kind, Vector3 pos, Vector3 scale, Material material, bool collider = false)
        {
            var go = GameObject.CreatePrimitive(kind);
            go.name = name;
            go.transform.SetParent(parent,false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go.transform;
        }
    }
}
