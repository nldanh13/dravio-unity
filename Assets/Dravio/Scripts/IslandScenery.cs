using System.Collections.Generic;
using Dravio.EnvironmentArt;
using UnityEngine;

namespace Dravio
{
    /// <summary>
    /// U6.5 — turns the Origin sanctuary into an island in the sea and puts the system buildings on it.
    /// Presentation only, added at runtime by <see cref="IslandHud"/>; the scene file is not changed.
    /// While the Origin world is in view it:
    ///  * shows Resources/IslandScenery/SM_ISLAND_Origin (rounded ground, cliff, shore rocks, rim bushes; built by
    ///    scripts/art/environment/build_island_base.py on the same kit materials) and a vertex-coloured sea;
    ///  * hides the old ground sheet and edge cover renderers, every environment prop outside the island and the
    ///    props a building replaces (renderers only: colliders, navigation and gameplay data are untouched);
    ///  * places <see cref="IslandBuildings.Items"/> (Resources/IslandBuildings) at their spots.
    /// Leaving the Origin world restores every hidden renderer. Sky and fog stay with EnvironmentTimeOfDay.
    /// </summary>
    public sealed class IslandScenery : MonoBehaviour
    {
        public LivingSanctuaryPresentation living;

        /// <summary>World point above each placed building, for its name chip (same order as the items).</summary>
        public readonly List<(IslandBuilding item,Vector3 top)> Placed=new List<(IslandBuilding,Vector3)>();

        static readonly Color Deep=new Color(.12f,.42f,.70f),Mid=new Color(.20f,.62f,.82f),Shallow=new Color(.48f,.84f,.86f),Foam=new Color(.86f,.96f,.95f);
        readonly List<Renderer> hidden=new List<Renderer>();
        GameObject root;
        SanctuaryLocationData builtFor;

        void LateUpdate()
        {
            var data=living!=null && living.ViewActive?living.Locations:null;
            if(data!=null && data.zoneId!="origin")data=null; // the island mesh is the Origin world's
            if(data==builtFor)return;
            Restore();
            builtFor=data;
            if(data!=null)Apply();
        }

        void Apply()
        {
            root=new GameObject("Island scenery (U6.5)");
            var island=Resources.Load<GameObject>("IslandScenery/SM_ISLAND_Origin");
            if(island!=null)Instantiate(island,Vector3.zero,Quaternion.identity,root.transform).name="SM_ISLAND_Origin";
            else Debug.LogWarning("Dravio U6.5: Resources/IslandScenery/SM_ISLAND_Origin missing; island base off.");
            var seaMat=Resources.Load<Material>("IslandScenery/M_ISLAND_Sea");
            if(seaMat!=null)
            {
                var sea=new GameObject("Sea",typeof(MeshFilter),typeof(MeshRenderer));
                sea.transform.SetParent(root.transform,false);
                sea.GetComponent<MeshFilter>().sharedMesh=SeaMesh();
                var r=sea.GetComponent<MeshRenderer>();r.sharedMaterial=seaMat;
                r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            PlaceBuildings();
            HideReplaced();
        }

        void Restore()
        {
            foreach(var r in hidden)if(r!=null)r.enabled=true;
            hidden.Clear();
            Placed.Clear();
            if(root!=null)Destroy(root);
            root=null;
        }

        void OnDestroy()=>Restore();

        // ---------- hide what the island replaces ----------
        void HideReplaced()
        {
            foreach(var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
            {
                if(!r.enabled || r.transform.IsChildOf(root.transform))continue;
                if(living!=null && r.transform.IsChildOf(living.transform))continue; // dragons, food
                string prop=PropName(r.transform);
                if(prop==null)continue;
                bool hide;
                if(prop.StartsWith("SM_ENV_Ground_Sanctuary") || prop.StartsWith("SM_ENV_Cover_Edges"))hide=true;
                else if(prop.StartsWith("SM_ENV_Cover_"))hide=false; // habitat ground cover stays
                else
                {
                    var c=r.bounds.center;
                    hide=!IslandShape.Inside(c.x,c.z,.3f) || NearBuilding(c);
                }
                if(hide){r.enabled=false;hidden.Add(r);}
            }
        }

        /// <summary>Name of the environment prop instance this renderer belongs to (SM_ENV_* / SM_SANC_*), or null.</summary>
        static string PropName(Transform t)
        {
            for(;t!=null;t=t.parent)
                if(t.name.StartsWith("SM_ENV_") || t.name.StartsWith("SM_SANC_"))return t.name;
            return null;
        }

        static bool NearBuilding(Vector3 p)
        {
            foreach(var b in IslandBuildings.Items)
            {
                float dx=p.x-b.X,dz=p.z-b.Z;
                if(dx*dx+dz*dz<IslandBuildings.ClearRadius*IslandBuildings.ClearRadius)return true;
            }
            return false;
        }

        // ---------- buildings ----------
        void PlaceBuildings()
        {
            foreach(var b in IslandBuildings.Items)
            {
                GameObject go=null;
                if(!string.IsNullOrEmpty(b.Model))
                {
                    var model=Resources.Load<GameObject>("IslandBuildings/"+b.Model);
                    var mat=Resources.Load<Material>("IslandBuildings/M_"+b.Model);
                    if(model!=null)
                    {
                        go=Instantiate(model,root.transform);
                        if(mat!=null)foreach(var r in go.GetComponentsInChildren<Renderer>())r.sharedMaterial=mat;
                    }
                    else Debug.LogWarning("Dravio U6.5: building model "+b.Model+" missing; using the placeholder.");
                }
                if(go==null)go=Placeholder(root.transform);
                go.name="Building "+b.Id;
                float y=Ground(b.X,b.Z);
                go.transform.SetPositionAndRotation(new Vector3(b.X,y,b.Z),Quaternion.Euler(0f,b.Yaw,0f));
                float top=y+2f;
                foreach(var r in go.GetComponentsInChildren<Renderer>())top=Mathf.Max(top,r.bounds.max.y);
                Placed.Add((b,new Vector3(b.X,top+.25f,b.Z)));
            }
        }

        static float Ground(float x,float z)
        {
            // Inside the flat rect the ground is y = 0 (kit rule); the raycast only refines it.
            return Physics.Raycast(new Vector3(x,40f,z),Vector3.down,out var hit,80f) && Mathf.Abs(hit.point.y)<.6f?hit.point.y:0f;
        }

        /// <summary>Storehouse placeholder until a model exists: timber walls, plank door, red roof (vertex colours).</summary>
        static GameObject Placeholder(Transform parent)
        {
            var go=new GameObject("Placeholder",typeof(MeshFilter),typeof(MeshRenderer));
            go.transform.SetParent(parent,false);
            var v=new List<Vector3>();var c=new List<Color>();var t=new List<int>();
            Box(v,c,t,new Vector3(0,.12f,0),new Vector3(1.45f,.24f,1.2f),new Color(.58f,.56f,.52f));          // stone footing
            Box(v,c,t,new Vector3(0,.82f,0),new Vector3(1.25f,1.16f,1.0f),new Color(.72f,.52f,.32f));          // walls
            Box(v,c,t,new Vector3(0,.62f,-.51f),new Vector3(.42f,.76f,.04f),new Color(.40f,.26f,.15f));        // door
            Box(v,c,t,new Vector3(-.62f,.58f,-.56f),new Vector3(.16f,.16f,.16f),new Color(.82f,.66f,.38f));    // crate
            Roof(v,c,t,1.40f,1.0f+.28f,.9f,1.62f,new Color(.74f,.30f,.22f));
            var m=new Mesh{name="Storehouse placeholder"};
            m.SetVertices(v);m.SetColors(c);m.SetTriangles(t,0);m.RecalculateNormals();m.RecalculateBounds();
            go.GetComponent<MeshFilter>().sharedMesh=m;
            var mat=Resources.Load<Material>("IslandBuildings/M_BLD_Store");
            if(mat!=null)go.GetComponent<MeshRenderer>().sharedMaterial=mat;
            return go;
        }

        static void Box(List<Vector3> v,List<Color> c,List<int> t,Vector3 centre,Vector3 size,Color col)
        {
            var h=size*.5f;
            Vector3[] n={Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
            foreach(var f in n)
            {
                var u=Mathf.Abs(f.y)>.5f?Vector3.right:Vector3.up;var w=Vector3.Cross(f,u);
                int s=v.Count;
                var fc=centre+Vector3.Scale(f,h);
                var a=Vector3.Scale(u,h);var b=Vector3.Scale(w,h);
                v.Add(fc-a-b);v.Add(fc+a-b);v.Add(fc+a+b);v.Add(fc-a+b);
                float shade=f.y>.5f?1f:(f.y<-.5f?.6f:.85f);
                for(int i=0;i<4;i++)c.Add(new Color(col.r*shade,col.g*shade,col.b*shade,0f));
                t.Add(s);t.Add(s+1);t.Add(s+2);t.Add(s);t.Add(s+2);t.Add(s+3); // a×b = f: faces out
            }
        }

        /// <summary>Gable roof over x (width) and z (depth), eaves at y0, ridge at y1.</summary>
        static void Roof(List<Vector3> v,List<Color> c,List<int> t,float width,float depth,float y0,float y1,Color col)
        {
            float hx=width*.5f+.08f,hz=depth*.5f;
            var ridgeA=new Vector3(-hx,y1,0);var ridgeB=new Vector3(hx,y1,0);
            void Quad(Vector3 a,Vector3 b,Vector3 d,Vector3 e,float shade)
            {
                int s=v.Count;v.Add(a);v.Add(b);v.Add(d);v.Add(e);
                for(int i=0;i<4;i++)c.Add(new Color(col.r*shade,col.g*shade,col.b*shade,0f));
                t.Add(s);t.Add(s+1);t.Add(s+2);t.Add(s);t.Add(s+2);t.Add(s+3);
                t.Add(s);t.Add(s+2);t.Add(s+1);t.Add(s);t.Add(s+3);t.Add(s+2); // two-sided
            }
            Quad(new Vector3(-hx,y0,-hz),ridgeA,ridgeB,new Vector3(hx,y0,-hz),1f);
            Quad(new Vector3(hx,y0,hz),ridgeB,ridgeA,new Vector3(-hx,y0,hz),.8f);
            var wall=new Color(.72f,.52f,.32f);
            foreach(float sx in new[]{-1f,1f})
            {
                int s=v.Count;float x=sx*(width*.5f-.06f);
                v.Add(new Vector3(x,y0,-hz+.12f));v.Add(new Vector3(x,y1-.05f,0));v.Add(new Vector3(x,y0,hz-.12f));
                for(int i=0;i<3;i++)c.Add(new Color(wall.r*.85f,wall.g*.85f,wall.b*.85f,0f));
                t.Add(s);t.Add(s+1);t.Add(s+2);t.Add(s);t.Add(s+2);t.Add(s+1);
            }
        }

        // ---------- sea ----------
        /// <summary>Rings from just inside the shore out to the horizon; colour from foam to deep water by distance.</summary>
        static Mesh SeaMesh()
        {
            const int Seg=120;
            float[] d={-.6f,.4f,1.2f,2.6f,5f,10f,22f,45f,90f};
            var v=new List<Vector3>();var c=new List<Color>();var t=new List<int>();
            for(int r=0;r<d.Length;r++)
                for(int i=0;i<Seg;i++)
                {
                    double a=2*Mathf.PI*i/Seg;
                    IslandShape.Edge(a,out float ex,out float ez);
                    float nx=ex-IslandShape.Cx,nz=ez-IslandShape.Cz,len=Mathf.Sqrt(nx*nx+nz*nz);
                    v.Add(new Vector3(ex+nx/len*d[r],IslandShape.SeaY,ez+nz/len*d[r]));
                    Color col=d[r]<=.4f?Foam:d[r]<=1.2f?Color.Lerp(Foam,Shallow,.7f):d[r]<=2.6f?Shallow:d[r]<=5f?Color.Lerp(Shallow,Mid,.6f):d[r]<=10f?Mid:Deep;
                    col.a=0f;c.Add(col);
                }
            for(int r=0;r<d.Length-1;r++)
                for(int i=0;i<Seg;i++)
                {
                    int j=(i+1)%Seg,a=r*Seg+i,b=r*Seg+j,e=(r+1)*Seg+i,f=(r+1)*Seg+j;
                    t.Add(a);t.Add(b);t.Add(f);t.Add(a);t.Add(f);t.Add(e);
                }
            var m=new Mesh{name="Island sea"};
            m.SetVertices(v);m.SetColors(c);m.SetTriangles(t,0);
            var normals=new Vector3[v.Count];for(int i=0;i<normals.Length;i++)normals[i]=Vector3.up;m.normals=normals;
            m.RecalculateBounds();
            // Winding: make every triangle face up.
            var tris=m.triangles;
            for(int i=0;i<tris.Length;i+=3)
                if(Vector3.Cross(v[tris[i+1]]-v[tris[i]],v[tris[i+2]]-v[tris[i]]).y<0){int k=tris[i+1];tris[i+1]=tris[i+2];tris[i+2]=k;}
            m.triangles=tris;
            return m;
        }
    }
}
