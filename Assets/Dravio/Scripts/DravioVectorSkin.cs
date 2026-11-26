using UnityEngine;
using UnityEngine.UI;

namespace Dravio
{
    // Geometry-only UI. The original Image continues to own raycasts, masking and
    // Selectable color transitions. No sprite, texture, camera or package dependency.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class DravioVectorSkin : BaseMeshEffect
    {
        [SerializeField] string style = "primary";
        RectTransform icon, label;
        static readonly Color32 Gold = new Color32(198,160,91,255);
        static readonly Color32 LightGold = new Color32(243,218,155,255);
        static readonly Color32 DarkGold = new Color32(102,70,38,255);
        static readonly Color32 Outline = new Color32(26,30,24,255);
        static readonly Color32 Ivory = new Color32(246,232,198,255);
        static readonly Color32 Green = new Color32(87,151,91,255);
        static readonly Color32 Blue = new Color32(80,154,184,255);
        static readonly Color32 Red = new Color32(194,86,68,255);
        static readonly Color32 Purple = new Color32(153,111,193,255);
        static readonly Color32 Wood = new Color32(151,100,62,255);
        static readonly Color32 Grey = new Color32(116,139,145,255);
        VertexHelper mesh;
        Rect rect;
        float feather;

        public void SetStyle(string value)
        {
            if(style==value)return;
            style=value;
            if(graphic!=null)graphic.SetVerticesDirty();
        }

        public void ConfigureIconLayout(RectTransform image,RectTransform text)
        {
            icon=image; label=text; FitIcon();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            FitIcon();
        }

        void FitIcon()
        {
            if(icon==null || label==null)return;
            // Do not let icons steal space from long bilingual labels on a narrow
            // phone or split action row. The existing button hit area is unchanged.
            bool show=graphic!=null && graphic.rectTransform.rect.width>=132f;
            if(icon.gameObject.activeSelf!=show)icon.gameObject.SetActive(show);
            var lo=label.offsetMin;var hi=label.offsetMax;
            lo.x=show?35f:8f;hi.x=-8f;
            if(label.offsetMin!=lo)label.offsetMin=lo;
            if(label.offsetMax!=hi)label.offsetMax=hi;
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if(!IsActive())return;
            vh.Clear();
            rect=graphic.rectTransform.rect;
            if(rect.width<1 || rect.height<1)return;
            mesh=vh;
            float scale=graphic.canvas!=null?graphic.canvas.scaleFactor:1f;
            feather=Mathf.Clamp(.8f/Mathf.Max(.1f,scale),.08f,1.6f);
            if(style.StartsWith("icon/"))
            {
                float side=Mathf.Min(rect.width,rect.height);
                rect=new Rect(rect.center-new Vector2(side,side)*.5f,new Vector2(side,side));
                DrawIcon(style.Substring(5));
            }
            else DrawFrame(style);
            mesh=null;
        }

        static Color32 C(int r,int g,int b,int a=255)=>new Color32((byte)r,(byte)g,(byte)b,(byte)a);
        static Color32 Mix(Color32 a,Color32 b,float t)=>Color32.Lerp(a,b,t);
        void Vertex(Vector2 p,Color32 c)
        {
            var v=UIVertex.simpleVert;
            v.position=p;v.color=(Color)c*graphic.color;v.uv0=new Vector2(.5f,.5f);
            mesh.AddVert(v);
        }

        // Convex polygons only. Fan triangulation is stable at every aspect ratio.
        // An optional translucent fringe smooths the outer silhouette at ~one pixel.
        void Poly(Vector2[] p,Color32 bottom,Color32 top,bool aa=false)
        {
            if(p.Length<3)return;
            Vector2 center=Vector2.zero;
            float low=p[0].y,high=low;
            foreach(var q in p){center+=q;low=Mathf.Min(low,q.y);high=Mathf.Max(high,q.y);}
            center/=p.Length;
            int first=mesh.currentVertCount;
            Vertex(center,Mix(bottom,top,Mathf.InverseLerp(low,high,center.y)));
            for(int i=0;i<p.Length;i++)Vertex(p[i],Mix(bottom,top,Mathf.InverseLerp(low,high,p[i].y)));
            for(int i=0;i<p.Length;i++)mesh.AddTriangle(first,first+1+i,first+1+(i+1)%p.Length);
            if(!aa)return;
            int outer=mesh.currentVertCount;
            for(int i=0;i<p.Length;i++)
            {
                Vector2 prev=p[i]-p[(i+p.Length-1)%p.Length],next=p[(i+1)%p.Length]-p[i];
                Vector2 n1=new Vector2(prev.y,-prev.x).normalized,n2=new Vector2(next.y,-next.x).normalized;
                Vector2 n=(n1+n2).normalized;
                if(Vector2.Dot(n,p[i]-center)<0)n=-n;
                float extension=feather/Mathf.Max(.5f,Mathf.Abs(Vector2.Dot(n,n1)));
                var c=Mix(bottom,top,Mathf.InverseLerp(low,high,p[i].y));c.a=0;
                Vertex(p[i]+n*extension,c);
            }
            for(int i=0;i<p.Length;i++)
            {
                int j=(i+1)%p.Length;
                mesh.AddTriangle(first+1+i,outer+i,outer+j);
                mesh.AddTriangle(first+1+i,outer+j,first+1+j);
            }
        }
        void Solid(Vector2[] p,Color32 c,bool aa=false)=>Poly(p,c,c,aa);
        static Rect Inset(Rect r,float d)=>new Rect(r.xMin+d,r.yMin+d,Mathf.Max(0,r.width-2*d),Mathf.Max(0,r.height-2*d));
        static Vector2[] Bevel(Rect r,float k)
        {
            k=Mathf.Clamp(k,0,Mathf.Min(r.width,r.height)*.49f);
            return new[]{new Vector2(r.xMin+k,r.yMin),new Vector2(r.xMax-k,r.yMin),
                new Vector2(r.xMax,r.yMin+k),new Vector2(r.xMax,r.yMax-k),
                new Vector2(r.xMax-k,r.yMax),new Vector2(r.xMin+k,r.yMax),
                new Vector2(r.xMin,r.yMax-k),new Vector2(r.xMin,r.yMin+k)};
        }
        void Box(Rect r,float k,Color32 bottom,Color32 top,bool aa=false)
        {
            if(r.width>0 && r.height>0)Poly(Bevel(r,k),bottom,top,aa);
        }
        void Line(Vector2 a,Vector2 b,float w,Color32 c)
        {
            Vector2 d=b-a;if(d.sqrMagnitude<.0001f)return;
            Vector2 n=new Vector2(-d.y,d.x).normalized*w*.5f;
            Solid(new[]{a-n,b-n,b+n,a+n},c,true);
        }
        void Disk(Vector2 p,float rx,float ry,Color32 bottom,Color32 top,bool aa=true)
        {
            int n=Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(Mathf.Max(rx,ry)/Mathf.Max(.1f,feather))*8),16,64);
            var v=new Vector2[n];
            for(int i=0;i<n;i++){float a=i*2*Mathf.PI/n;v[i]=p+new Vector2(Mathf.Cos(a)*rx,Mathf.Sin(a)*ry);}
            Poly(v,bottom,top,aa);
        }

        void DrawFrame(string kind)
        {
            Rect r=Inset(rect,feather+0.1f);
            float m=Mathf.Min(r.width,r.height);
            if(m<2)return;
            float b=Mathf.Min(2.5f,m*.10f),k=Mathf.Min(9f,m*.22f);
            bool panel=kind=="panel" || kind=="header" || kind=="card" || kind=="notice";
            bool slot=kind.StartsWith("slot");
            if(kind=="divider")
            {
                var center=r.center;
                Line(new Vector2(r.xMin,center.y),new Vector2(r.xMax,center.y),Mathf.Min(1.5f,m*.2f),Gold);
                Diamond(center,Mathf.Min(5f,m*.38f),Gold);return;
            }
            if(kind=="round" || kind=="portrait")
            {
                float radius=m*.5f;var center=r.center;
                Disk(center,radius,radius,DarkGold,LightGold);
                Disk(center,radius-b*2,radius-b*2,C(31,40,34),C(66,84,62),false);
                return;
            }
            if(kind=="progress_fill")
            {
                Box(r,Mathf.Min(k,m*.35f),C(31,88,53),C(117,189,104),true);return;
            }
            if(panel){b=Mathf.Min(3f,m*.09f);k=Mathf.Min(13f,m*.18f);}
            Color32 rim=kind=="slot_rare"?Blue:kind=="slot_epic"?Purple:Gold;
            Color32 low=C(29,66,48),high=C(66,119,77);
            if(kind=="reward"){low=C(169,117,53);high=C(246,217,142);}
            else if(kind=="secondary"){low=C(51,40,30);high=C(99,77,50);}
            else if(kind=="danger"){low=C(91,33,31);high=C(155,66,48);}
            else if(panel || slot || kind=="progress_frame"){low=C(22,36,29);high=C(37,56,44);}
            Box(r,k,Outline,DarkGold,true);
            Box(Inset(r,b*.55f),Mathf.Max(0,k-b*.55f),DarkGold,LightGold);
            Box(Inset(r,b*1.45f),Mathf.Max(0,k-b*1.45f),rim,rim);
            Box(Inset(r,b*2.15f),Mathf.Max(0,k-b*2.15f),Outline,Outline);
            Box(Inset(r,b*2.65f),Mathf.Max(0,k-b*2.65f),low,high);
            // Ornaments maintain constant logical size rather than stretching
            // across the middle. Small controls omit them for legibility.
            if(m>=30f && kind!="progress_frame")
            {
                float o=Mathf.Min(panel?5f:3f,m*.09f),inset=b*1.6f;
                Diamond(new Vector2(r.xMin+inset+k*.45f,r.yMin+inset+k*.45f),o,rim);
                Diamond(new Vector2(r.xMax-inset-k*.45f,r.yMax-inset-k*.45f),o,LightGold);
                if(panel)
                {
                    Diamond(new Vector2(r.xMin+inset+k*.45f,r.yMax-inset-k*.45f),o,LightGold);
                    Diamond(new Vector2(r.xMax-inset-k*.45f,r.yMin+inset+k*.45f),o,rim);
                }
            }
        }

        void Diamond(Vector2 c,float s,Color32 color)
        {
            Solid(new[]{c+new Vector2(0,-s),c+new Vector2(s*.70f,0),c+new Vector2(0,s),c+new Vector2(-s*.70f,0)},color,true);
        }

        // Icon coordinate space: 0..64, origin at top left. All artwork is
        // polygons, discs and strokes; no atlas UVs or fixed pixel resolution.
        Vector2 P(float x,float y)=>new Vector2(rect.xMin+x*rect.width/64f,rect.yMax-y*rect.height/64f);
        float S(float x)=>x*rect.width/64f;
        void Path(Color32 c,params float[] xy)
        {
            var pts=new Vector2[xy.Length/2];
            for(int i=0;i<pts.Length;i++)pts[i]=P(xy[i*2],xy[i*2+1]);
            Solid(pts,c,true);
        }
        void Stroke(Color32 c,float w,params float[] xy)
        {
            for(int i=2;i<xy.Length;i+=2)Line(P(xy[i-2],xy[i-1]),P(xy[i],xy[i+1]),S(w),c);
            for(int i=0;i<xy.Length;i+=2)Disk(P(xy[i],xy[i+1]),S(w*.5f),S(w*.5f),c,c);
        }
        void Oval(float x,float y,float rx,float ry,Color32 c)
        {
            Disk(P(x,y),S(rx+1),S(ry+1),Outline,Outline);
            Disk(P(x,y),S(rx),S(ry),Mix(c,Outline,.18f),c,false);
        }
        void Gem(float x,float y,float size,Color32 c)
        {
            Diamond(P(x,y),S(size),c);
            Path(Mix(c,Ivory,.5f),x,y-size,x,y+size,x-size*.7f,y);
        }
        void Leaf(float x,float y,float s)
        {
            Path(Green,x-s,y+s,x-s*.65f,y-s*.3f,x+s,y-s,x+s*.5f,y+s*.5f);
            Stroke(Ivory,2,x-s,y+s,x+s*.55f,y-s*.6f);
        }
        void Heart()
        {
            Oval(22,25,13,12,Red);Oval(42,25,13,12,Red);
            Path(Red,9,25,55,25,32,51);
            Stroke(Ivory,3,16,25,20,20);
        }
        void Book()
        {
            Path(Ivory,8,14,29,17,29,50,8,46);
            Path(Gold,35,17,56,14,56,46,35,50);
            Stroke(DarkGold,3,32,19,32,51);
            for(int y=25;y<=41;y+=8){Stroke(DarkGold,2,13,y,25,y+1);Stroke(DarkGold,2,39,y+1,51,y);}
        }
        void Pack(Color32 c)
        {
            Path(Mix(c,Outline,.35f),9,25,55,25,55,52,9,52);
            Path(c,9,25,17,12,47,12,55,25);
            Path(Gold,27,13,37,13,37,52,27,52);
            Gem(32,34,7,DarkGold);
        }
        void DrawIcon(string id)
        {
            switch(id)
            {
                case "care":case "bond":Heart();if(id=="bond")Gem(49,13,8,Purple);break;
                case "biology":case "blueprint":Book();if(id=="blueprint")Gem(32,33,8,Blue);break;
                case "dragons":
                    Path(Green,12,42,15,23,35,12,53,25,57,35,38,43,30,54,18,51);
                    Path(Gold,20,24,20,6,32,17);Path(Gold,40,17,49,5,49,23);Oval(43,28,4,4,Ivory);break;
                case "eggs":case "egg":
                    Oval(32,35,18,23,Ivory);Stroke(DarkGold,4,16,35,24,28,32,39,41,30,49,36);break;
                case "world":
                    Path(Green,8,16,24,10,24,47,8,53);Path(Ivory,24,10,40,16,40,53,24,47);Path(Green,40,16,56,10,56,47,40,53);
                    Stroke(DarkGold,3,15,39,24,30,35,38,48,23);break;
                case "quests":
                    Path(Ivory,15,9,49,9,49,53,15,53);Stroke(Gold,6,12,12,52,12);Stroke(DarkGold,5,21,31,29,39,44,22);break;
                case "expedition":
                    Oval(32,32,24,24,Gold);Oval(32,32,18,18,DarkGold);Gem(32,32,17,Ivory);Path(Red,32,15,26,34,38,30);break;
                case "inventory":
                    Path(Wood,18,20,46,20,53,48,44,55,20,55,11,48);Stroke(Gold,5,23,20,23,11,41,11,41,20);Path(DarkGold,14,23,50,23,43,35,21,35);Gem(32,33,6,Gold);break;
                case "feed":case "hunger":case "fruit":
                    Oval(23,36,14,16,Red);Oval(41,35,14,16,Red);Stroke(Wood,4,32,22,34,10);Leaf(41,13,8);break;
                case "pet":
                    Oval(31,40,14,14,Ivory);Stroke(Ivory,7,23,38,14,27);Stroke(Ivory,7,22,34,22,15);Stroke(Ivory,7,30,32,30,10);Stroke(Ivory,7,38,32,38,12);Stroke(Ivory,6,46,34,46,19);break;
                case "rest":case "night":
                    // A crescent constructed as a curved stroke, not a cut-out bitmap.
                    {var pts=new Vector2[21];for(int i=0;i<pts.Length;i++){float a=(48+i*13f)*Mathf.Deg2Rad;pts[i]=P(32+20*Mathf.Cos(a),30+20*Mathf.Sin(a));}
                    for(int i=1;i<pts.Length;i++)Line(pts[i-1],pts[i],S(11*Mathf.Sin(Mathf.PI*i/pts.Length)+2),id=="night"?Purple:Blue);Gem(48,15,7,Ivory);break;}
                case "inspect":
                    Path(Ivory,6,32,19,19,32,15,45,19,58,32,45,45,32,49,19,45);
                    Oval(32,32,12,12,Blue);Oval(32,32,5,5,Outline);break;
                case "build":
                    Stroke(Wood,9,35,27,18,54);Path(Gold,11,15,32,7,46,20,36,33,24,23,14,28);break;
                case "research":
                    Path(Blue,25,9,39,9,39,26,54,49,48,55,16,55,10,49,25,26);
                    Stroke(Ivory,3,24,10,40,10);Oval(28,43,4,4,Ivory);Oval(39,35,3,3,Ivory);break;
                case "health":
                    Path(Red,32,7,54,15,51,40,32,57,13,40,10,15);Stroke(Ivory,7,32,20,32,43);Stroke(Ivory,7,20,32,44,32);break;
                case "settings":
                    for(int i=0;i<8;i++){float a=i*Mathf.PI/4;Stroke(Gold,7,32+18*Mathf.Cos(a),32+18*Mathf.Sin(a),32+26*Mathf.Cos(a),32+26*Mathf.Sin(a));}
                    Oval(32,32,20,20,Gold);Oval(32,32,9,9,Outline);break;
                case "day":
                    for(int i=0;i<8;i++){float a=i*Mathf.PI/4;Stroke(Gold,4,32+19*Mathf.Cos(a),32+19*Mathf.Sin(a),32+27*Mathf.Cos(a),32+27*Mathf.Sin(a));}Oval(32,32,15,15,LightGold);break;
                case "lock":
                    Stroke(Grey,5,20,29,20,18,25,11,39,11,44,18,44,29);Path(Gold,13,26,51,26,51,53,13,53);Stroke(Outline,5,32,37,32,44);break;
                case "done":Oval(32,32,24,24,Green);Stroke(Ivory,6,16,32,27,43,49,19);break;
                case "warning":Path(Gold,32,6,58,54,6,54);Stroke(Outline,5,32,22,32,37);Oval(32,45,3,3,Outline);break;
                case "upgrade":Path(Gold,32,7,54,30,10,30);Path(Gold,22,27,42,27,42,55,22,55);break;
                case "reward":Pack(Gold);break;
                case "water":Path(Blue,32,6,47,29,51,40,46,51,32,57,18,51,13,40,17,29);Stroke(Ivory,3,23,35,20,43);break;
                case "energy":Path(Gold,36,5,12,36,30,36);Path(Gold,30,24,54,24,24,59);break;
                case "temperature":Oval(32,45,13,12,Red);Path(Ivory,25,10,39,10,39,44,25,44);Stroke(Red,7,32,18,32,45);break;
                case "leaf":Leaf(32,32,23);break;
                case "close":Stroke(Ivory,6,16,16,48,48);Stroke(Ivory,6,48,16,16,48);break;
                case "back":case "next":
                    if(id=="back"){Path(Ivory,9,32,33,10,33,54);Path(Ivory,30,23,55,23,55,41,30,41);}
                    else{Path(Ivory,55,32,31,10,31,54);Path(Ivory,9,23,34,23,34,41,9,41);}break;
                case "gold":Oval(32,32,23,23,DarkGold);Oval(32,30,19,19,Gold);Gem(32,30,12,LightGold);break;
                case "wood":
                    for(int y=18;y<=46;y+=14){Path(Wood,13,y-6,44,y-9,52,y-1,48,y+9,19,y+10);Oval(17,y+2,7,7,Gold);Oval(17,y+2,3,3,DarkGold);}break;
                case "seed":Oval(24,42,12,9,Wood);Oval(39,44,12,9,DarkGold);Stroke(Green,3,32,40,32,14);Leaf(23,20,10);Leaf(41,14,10);break;
                case "stone":Path(Grey,7,43,19,22,36,18,49,29,56,47,43,54,15,54);Path(Ivory,19,22,36,18,49,29,33,37);Path(C(73,94,96),33,37,49,29,56,47,43,54);break;
                case "crystal":Gem(32,28,23,Blue);Gem(17,40,14,Grey);Gem(48,40,14,Blue);break;
                case "coral":
                    Stroke(Red,6,32,54,32,15);Stroke(Red,6,32,36,18,24,18,12);Stroke(Red,6,32,31,46,17,51,8);Stroke(Red,5,46,17,51,29);Stroke(Red,5,32,44,49,38,55,34);break;
                case "stardust":Path(Purple,23,9,41,9,41,19,49,29,49,52,15,52,15,29,23,19);Gem(30,37,10,Ivory);Gem(42,25,5,LightGold);break;
                case "aquatic":Leaf(22,36,15);Leaf(40,30,15);Oval(32,47,12,6,Blue);break;
                case "aether":Gem(32,32,23,Purple);Gem(32,32,12,Ivory);Gem(50,12,6,LightGold);break;
                case "starter":Pack(Wood);break;
                case "elemental":Pack(Green);break;
                case "mystic":Pack(Purple);break;
                case "astral":Pack(Blue);break;
                default:Gem(32,32,22,Gold);break;
            }
        }
    }
}
