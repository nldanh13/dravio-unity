using System.Collections.Generic;
using Dravio.Domain;
using Dravio.Domain.Catalog;
using Dravio.Domain.Queries;
using Dravio.EnvironmentArt;
using Dravio.Platform.Unity;
using UnityEngine;

namespace Dravio
{
    /// <summary>
    /// U6.4 (phase B) — each microhabitat of the world in view is drawn as its element's plot: a coloured border
    /// following the walkable habitat polygon from SanctuaryLocationData and a light tint inside. Locked habitats
    /// get a grey border and no tint. <see cref="IslandHud"/> puts a name chip on each plot from <see cref="Plots"/>.
    /// Presentation only (no collider, no shadow, no raycast target); rebuilt when the world or an unlock changes.
    /// </summary>
    public sealed class HabitatPlots : MonoBehaviour
    {
        const float Border=.22f,Lift=.045f;
        public sealed class Plot
        {
            public string Id,NameVi,NameEn;
            public ElementType Element;
            public bool Unlocked;
            public Vector3 LabelPoint;
            public string Name(bool vi)=>vi?NameVi:NameEn;
        }

        public LivingSanctuaryPresentation living;
        public DravioSessionHost host;
        readonly List<Plot> plots=new List<Plot>();
        public IReadOnlyList<Plot> Plots=>plots;
        GameObject root;
        Material material;
        SanctuaryLocationData builtFor;
        string unlockKey;

        public static Color ElementColor(ElementType e)=>e switch
        {
            ElementType.Fire=>new Color(1f,.55f,.25f),
            ElementType.Water=>new Color(.32f,.70f,1f),
            ElementType.Sky=>new Color(.82f,.84f,1f),
            _=>new Color(.52f,.86f,.34f),
        };

        void LateUpdate()
        {
            var data=living!=null && living.ViewActive?living.Locations:null;
            var s=host!=null && host.Session!=null?host.Session.State:null;
            string key=data==null||s==null?null:UnlockKey(data,s);
            if(data==builtFor && key==unlockKey)return;
            builtFor=data;unlockKey=key;
            Rebuild(data,s);
        }

        static string UnlockKey(SanctuaryLocationData data,GameState s)
        {
            var sb=new System.Text.StringBuilder();
            foreach(var h in data.habitats)
            {
                var o=Ecosystem.Habitat(s,h.id);
                sb.Append(o!=null && o.Unlocked?'1':'0');
            }
            return sb.ToString();
        }

        void Rebuild(SanctuaryLocationData data,GameState s)
        {
            if(root!=null)Destroy(root);
            plots.Clear();
            if(data==null || s==null)return;
            if(material==null)
            {
                var shader=Shader.Find("Sprites/Default");
                if(shader==null){Debug.LogWarning("Dravio U6.4: Sprites/Default shader missing; habitat plots off.");enabled=false;return;}
                material=new Material(shader){renderQueue=3000};
            }
            root=new GameObject("Habitat plots (U6.4)");
            foreach(var h in data.habitats)
            {
                if(h.polygonXZ==null || h.polygonXZ.Length<6)continue;
                HabitatCatalog.TryGet(h.id,out var profile);
                var overview=Ecosystem.Habitat(s,h.id);
                bool open=overview==null || overview.Unlocked;
                var element=HabitatLook.ElementOf(profile);
                float y=GroundY(data,h)+Lift;
                var color=open?ElementColor(element):new Color(.62f,.62f,.60f);
                var mesh=BuildMesh(h.polygonXZ,y,color,open?.85f:.55f,open?.16f:0f);
                var go=new GameObject(h.id,typeof(MeshFilter),typeof(MeshRenderer));
                go.transform.SetParent(root.transform,false);
                go.GetComponent<MeshFilter>().sharedMesh=mesh;
                var r=go.GetComponent<MeshRenderer>();
                r.sharedMaterial=material;
                r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;
                HabitatLook.Centroid(h.polygonXZ,out float cx,out float cz);
                float maxZ=float.MinValue;for(int i=1;i<h.polygonXZ.Length;i+=2)maxZ=Mathf.Max(maxZ,h.polygonXZ[i]);
                plots.Add(new Plot{Id=h.id,NameVi=h.displayNameVi,NameEn=h.displayNameEn,Element=element,Unlocked=open,
                    LabelPoint=new Vector3(cx,y,Mathf.Lerp(cz,maxZ,.7f))});
            }
        }

        static float GroundY(SanctuaryLocationData data,SanctuaryLocationData.Habitat h)
        {
            float sum=0;int n=0;
            foreach(var p in data.SpawnPointsIn(h.id)){sum+=p.y;n++;}
            if(n>0)return sum/n;
            HabitatLook.Centroid(h.polygonXZ,out float cx,out float cz);
            return Physics.Raycast(new Vector3(cx,60f,cz),Vector3.down,out var hit,200f)?hit.point.y:0f;
        }

        /// <summary>Fill (ear-clipped) + border ribbon between the outline and its inset, vertex-coloured.</summary>
        static Mesh BuildMesh(float[] xz,float y,Color c,float borderAlpha,float fillAlpha)
        {
            int n=xz.Length/2;
            var inner=HabitatLook.Inset(xz,Border);
            var verts=new List<Vector3>();var cols=new List<Color>();var tris=new List<int>();
            var edge=c;edge.a=borderAlpha;var soft=c;soft.a=borderAlpha*.35f;
            for(int i=0;i<n;i++){verts.Add(new Vector3(xz[2*i],y,xz[2*i+1]));cols.Add(edge);}
            for(int i=0;i<n;i++){verts.Add(new Vector3(inner[2*i],y,inner[2*i+1]));cols.Add(soft);}
            bool ccw=HabitatLook.SignedArea2(xz)>=0;
            for(int i=0;i<n;i++)
            {
                int j=(i+1)%n,a=i,b=j,ia=n+i,ib=n+j;
                // Faces up (+Y) seen from the camera: winding depends on polygon orientation.
                if(ccw){tris.Add(a);tris.Add(ib);tris.Add(b);tris.Add(a);tris.Add(ia);tris.Add(ib);}
                else{tris.Add(a);tris.Add(b);tris.Add(ib);tris.Add(a);tris.Add(ib);tris.Add(ia);}
            }
            if(fillAlpha>0f)
            {
                int start=verts.Count;var fill=c;fill.a=fillAlpha;
                for(int i=0;i<n;i++){verts.Add(new Vector3(inner[2*i],y-.002f,inner[2*i+1]));cols.Add(fill);}
                var t=HabitatLook.Triangulate(inner);
                for(int i=0;i<t.Count;i+=3)
                {
                    // Triangulate returns CCW (x/z) triangles; reverse for an upward-facing Unity triangle.
                    tris.Add(start+t[i]);tris.Add(start+t[i+2]);tris.Add(start+t[i+1]);
                }
            }
            var m=new Mesh{name="Habitat plot"};
            m.SetVertices(verts);m.SetColors(cols);m.SetTriangles(tris,0);m.RecalculateBounds();
            return m;
        }

        void OnDestroy(){if(root!=null)Destroy(root);if(material!=null)Destroy(material);}
    }
}
