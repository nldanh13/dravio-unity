using System;
using System.Collections.Generic;
using Dravio.Domain;
using Dravio.Domain.Catalog;
using Dravio.Domain.Economy;
using Dravio.Domain.Queries;

namespace Dravio
{
    /// <summary>
    /// U6.3 island camera math (pure, dotnet-tested). The view is the fitted SanctuaryCameraFit pose scaled by
    /// <c>zoom</c> (0..1, 1 = the fitted overview) and shifted by an offset in the camera plane (world units,
    /// x along camera right, y along camera up). The free area of the zoomed view always stays inside the
    /// free area of the fitted overview, so the player can zoom in and look around but never off the island
    /// and never under the drawer.
    /// </summary>
    public static class IslandViewMath
    {
        // U6.5: above 1 the view zooms out past the fitted overview (whole island in the sea); it stays centred.
        public const float MinZoom=.45f,DefaultZoom=.8f,MaxZoom=1.6f;
        public static float ClampZoom(float z)=>z<MinZoom?MinZoom:(z>MaxZoom?MaxZoom:z);

        /// <summary>Allowed offset range for one axis. lo/hi = free viewport edges (0..1), half = fitted half extent.</summary>
        public static void Range(float zoom,float lo,float hi,float half,out float min,out float max)
        {
            float k=1f-ClampZoom(zoom);
            min=(2f*lo-1f)*half*k;max=(2f*hi-1f)*half*k;
            if(max<min){float m=(min+max)*.5f;min=max=m;}
        }

        public static void Clamp(ref float ox,ref float oy,float zoom,float x0,float x1,float y0,float y1,float halfW,float halfH)
        {
            Range(zoom,x0,x1,halfW,out float a,out float b);ox=ox<a?a:(ox>b?b:ox);
            Range(zoom,y0,y1,halfH,out a,out b);oy=oy<a?a:(oy>b?b:oy);
        }

        /// <summary>Offset that keeps the free area centred where the overview centred it.</summary>
        public static void Centre(float zoom,float x0,float x1,float y0,float y1,float halfW,float halfH,out float ox,out float oy)
        {
            float k=1f-ClampZoom(zoom);
            ox=(x0+x1-1f)*halfW*k;oy=(y0+y1-1f)*halfH*k;
        }

        /// <summary>Zoom from z to z2 keeping the world point under viewport (vx,vy) fixed, then clamp.</summary>
        public static void ZoomAt(ref float ox,ref float oy,float z,float z2,float vx,float vy,
            float x0,float x1,float y0,float y1,float halfW,float halfH)
        {
            z=ClampZoom(z);z2=ClampZoom(z2);
            float qx=ox+(2f*vx-1f)*halfW*z,qy=oy+(2f*vy-1f)*halfH*z;
            ox=qx-(2f*vx-1f)*halfW*z2;oy=qy-(2f*vy-1f)*halfH*z2;
            Clamp(ref ox,ref oy,z2,x0,x1,y0,y1,halfW,halfH);
        }

        /// <summary>Drag by a screen delta in pixels: the ground follows the finger.</summary>
        public static void Drag(ref float ox,ref float oy,float dxPx,float dyPx,float screenHeightPx,float zoom,float halfH)
        {
            float perPx=2f*halfH*ClampZoom(zoom)/Math.Max(1f,screenHeightPx);
            ox-=dxPx*perPx;oy-=dyPx*perPx;
        }
    }

    public enum BubbleAttention { None, NextStep, Care, Shelter }

    /// <summary>What the small bubble above one dragon says. Read-only, from the same queries as the HUD.</summary>
    public sealed class IslandBubble
    {
        public string Uid,Name,StageVi,StageEn;
        /// <summary>0..1 growth to adult; 1 for adults.</summary>
        public float Growth;
        public bool Adult;
        public BubbleAttention Attention;
        public string Stage(bool vi)=>vi?StageVi:StageEn;
        public string Mark=>Attention==BubbleAttention.None?"":"!";
        public string AttentionText(bool vi)=>Attention switch
        {
            BubbleAttention.Shelter=>vi?"Cần trú ẩn":"Needs shelter",
            BubbleAttention.Care=>vi?"Cần chăm sóc":"Needs care",
            BubbleAttention.NextStep=>vi?"Bước tiếp theo":"Next step",
            _=>"",
        };

        /// <summary>null for an unknown or released dragon. focusUid = the journey's current dragon (if any).</summary>
        public static IslandBubble For(GameState s,string uid,string focusUid)
        {
            var d=s?.FindDragon(uid);
            if(d==null || d.Location.Kind==LocationKind.Released || !SpeciesCatalog.TryGet(d.SpeciesId,out var sp))return null;
            var stage=EconomyRates.GrowthStageOf(d.XpSec,sp);
            var b=new IslandBubble
            {
                Uid=uid,Name=string.IsNullOrEmpty(d.Nickname)?PlayerJourneyPlanner.SpeciesName(d.SpeciesId):d.Nickname,
                Adult=stage==GrowthStage.Adult,
                Growth=sp.GrowthTimeSec<=0?1f:(float)Math.Min(1.0,Math.Max(0.0,d.XpSec/sp.GrowthTimeSec)),
            };
            int pct=(int)Math.Floor(b.Growth*100f);
            switch(stage)
            {
                case GrowthStage.Baby:b.StageVi="Rồng non "+pct+"%";b.StageEn="Baby "+pct+"%";break;
                case GrowthStage.Juvenile:b.StageVi="Đang lớn "+pct+"%";b.StageEn="Growing "+pct+"%";break;
                default:b.StageVi="Trưởng thành";b.StageEn="Adult";break;
            }
            var health=HealthQueries.Dragon(s,uid);
            if(health!=null && health.ExposedToWarning)b.Attention=BubbleAttention.Shelter;
            else if(health!=null && health.OpenCaseId!=null)b.Attention=BubbleAttention.Care;
            else if(uid==focusUid)b.Attention=BubbleAttention.NextStep;
            return b;
        }
    }

    /// <summary>
    /// Splits the real online gold of each TimeAdvanced among the producing dragons by their production rate,
    /// so "+N" pops above the dragons that made it. Exact in micro-gold: popped + still owed == earned.
    /// </summary>
    public sealed class GoldPopupLedger
    {
        readonly Dictionary<string,long> owed=new Dictionary<string,long>(StringComparer.Ordinal);
        readonly Dictionary<string,double> last=new Dictionary<string,double>(StringComparer.Ordinal);
        public double MinGapSec=2.5;
        public long OwedMicro(string uid)=>owed.TryGetValue(uid,out var v)?v:0;
        public long TotalOwedMicro{get{long t=0;foreach(var v in owed.Values)t+=v;return t;}}

        /// <summary>Producers from the state the gold was earned in (Ecosystem.Snapshot rates).</summary>
        public static List<KeyValuePair<string,long>> Producers(GameState s)
        {
            var list=new List<KeyValuePair<string,long>>();
            if(s==null)return list;
            foreach(var d in s.Dragons)
            {
                if(d.Location.Kind!=LocationKind.Zone)continue;
                var snap=Ecosystem.Snapshot(s,d);
                if(snap.Producing && snap.RateMicroGPerSec>0)list.Add(new KeyValuePair<string,long>(d.Uid,snap.RateMicroGPerSec));
            }
            return list;
        }

        public void Earn(long goldMicro,IReadOnlyList<KeyValuePair<string,long>> producers)
        {
            if(goldMicro<=0 || producers==null || producers.Count==0)return;
            long total=0;foreach(var p in producers)total+=p.Value;
            if(total<=0)return;
            long given=0;int largest=0;
            for(int i=0;i<producers.Count;i++)
            {
                long part=(long)((decimal)goldMicro*producers[i].Value/total);
                Add(producers[i].Key,part);given+=part;
                if(producers[i].Value>producers[largest].Value)largest=i;
            }
            Add(producers[largest].Key,goldMicro-given);
        }
        void Add(string uid,long micro){if(micro==0)return;owed[uid]=OwedMicro(uid)+micro;}

        /// <summary>Whole gold ready to pop for dragons in view, at most once per MinGapSec per dragon.</summary>
        public List<KeyValuePair<string,long>> Due(double nowSec,ICollection<string> inView)
        {
            var due=new List<KeyValuePair<string,long>>();
            foreach(var uid in inView)
            {
                long micro=OwedMicro(uid);
                if(micro<MicroGold.MicroPerGold)continue;
                if(last.TryGetValue(uid,out var at) && nowSec-at<MinGapSec)continue;
                long whole=micro/MicroGold.MicroPerGold;
                owed[uid]=micro-whole*MicroGold.MicroPerGold;last[uid]=nowSec;
                due.Add(new KeyValuePair<string,long>(uid,whole));
            }
            return due;
        }

        /// <summary>Drop dragons that no longer exist so the ledger cannot grow forever.</summary>
        public void Retain(GameState s)
        {
            if(s==null)return;
            var gone=new List<string>();
            foreach(var k in owed.Keys)if(s.FindDragon(k)==null)gone.Add(k);
            foreach(var k in gone){owed.Remove(k);last.Remove(k);}
        }
    }

    /// <summary>The round buttons at the bottom of the island, one per drawer page.</summary>
    public static class IslandDock
    {
        public sealed class Item
        {
            public HudPage Page;public string Vi,En;
            public string Label(bool vi)=>vi?Vi:En;
        }
        public static readonly Item[] Items=
        {
            new Item{Page=HudPage.Care,Vi="Chăm sóc",En="Care"},
            new Item{Page=HudPage.Dragons,Vi="Đàn rồng",En="Dragons"},
            new Item{Page=HudPage.Eggs,Vi="Trứng",En="Eggs"},
            new Item{Page=HudPage.Manage,Vi="Quản lý",En="Manage"},
            new Item{Page=HudPage.Research,Vi="Sổ",En="Log"},
            new Item{Page=HudPage.Biology,Vi="Sinh học",En="Biology"},
            new Item{Page=HudPage.Guide,Vi="Hướng dẫn",En="Guide"},
        };

        /// <summary>Tapping the page that is already open closes the drawer (back to the island).</summary>
        public static bool TapCloses(bool drawerOpen,int openPage,HudPage tapped)=>drawerOpen && openPage==(int)tapped;
    }

    /// <summary>
    /// U6.4 (phase B) habitat plots: each microhabitat reads as its element's plot, like the element habitats of
    /// dragon-park games. The element comes from the Domain habitat profile (no new data): sky realm or altitude
    /// = Sky, ocean or submerged = Water, warm/hot = Fire, humid or any salinity = Water, else Nature. Geometry helpers are pure.
    /// </summary>
    public static class HabitatLook
    {
        public static ElementType ElementOf(HabitatProfile h)
        {
            if(h==null)return ElementType.Nature;
            if(h.Realm==Realm.Sky || h.AltitudeBand!=AltitudeBand.Ground)return ElementType.Sky;
            if(h.Realm==Realm.Ocean || h.HumidityBand==HumidityBand.Submerged)return ElementType.Water;
            if(h.TemperatureBand==TemperatureBand.Warm || h.TemperatureBand==TemperatureBand.Hot)return ElementType.Fire;
            if(h.HumidityBand==HumidityBand.Humid || h.SalinityBand!=SalinityBand.None)return ElementType.Water;
            return ElementType.Nature;
        }

        public static string ElementName(ElementType e,bool vi)=>e switch
        {
            ElementType.Fire=>vi?"Lửa":"Fire",
            ElementType.Water=>vi?"Nước":"Water",
            ElementType.Sky=>vi?"Trời":"Sky",
            _=>vi?"Thiên nhiên":"Nature",
        };

        /// <summary>Twice the signed area (positive = counter-clockwise in x/z).</summary>
        public static double SignedArea2(float[] xz)
        {
            double a=0;int n=xz.Length/2;
            for(int i=0;i<n;i++){int j=(i+1)%n;a+=(double)xz[2*i]*xz[2*j+1]-(double)xz[2*j]*xz[2*i+1];}
            return a;
        }

        /// <summary>Ear-clipping triangulation of a simple polygon; indices into the vertex list, any winding.</summary>
        public static List<int> Triangulate(float[] xz)
        {
            var tris=new List<int>();
            int n=xz==null?0:xz.Length/2;
            if(n<3)return tris;
            var idx=new List<int>();for(int i=0;i<n;i++)idx.Add(i);
            if(SignedArea2(xz)<0)idx.Reverse();
            int guard=0;
            while(idx.Count>3 && guard++<n*n)
            {
                bool clipped=false;
                for(int k=0;k<idx.Count;k++)
                {
                    int a=idx[(k+idx.Count-1)%idx.Count],b=idx[k],c=idx[(k+1)%idx.Count];
                    if(Cross(xz,a,b,c)<=1e-9)continue; // reflex or flat
                    bool inside=false;
                    foreach(int p in idx)
                        if(p!=a && p!=b && p!=c && InTri(xz,p,a,b,c)){inside=true;break;}
                    if(inside)continue;
                    tris.Add(a);tris.Add(b);tris.Add(c);idx.RemoveAt(k);clipped=true;break;
                }
                if(!clipped)break;
            }
            if(idx.Count==3){tris.Add(idx[0]);tris.Add(idx[1]);tris.Add(idx[2]);}
            return tris;
        }
        static double Cross(float[] v,int a,int b,int c)=>
            ((double)v[2*b]-v[2*a])*((double)v[2*c+1]-v[2*a+1])-((double)v[2*b+1]-v[2*a+1])*((double)v[2*c]-v[2*a]);
        static bool InTri(float[] v,int p,int a,int b,int c)=>Cross(v,a,b,p)>=0 && Cross(v,b,c,p)>=0 && Cross(v,c,a,p)>=0;

        /// <summary>Polygon moved inward by d (mitre joins, mitre capped at 3·d), same vertex order.</summary>
        public static float[] Inset(float[] xz,float d)
        {
            int n=xz.Length/2;var o=new float[xz.Length];
            float s=SignedArea2(xz)>=0?1f:-1f; // inward = left of each edge for CCW
            for(int i=0;i<n;i++)
            {
                int pi=(i+n-1)%n,ni=(i+1)%n;
                Normal(xz,pi,i,s,out float ax,out float az);
                Normal(xz,i,ni,s,out float bx,out float bz);
                float mx=ax+bx,mz=az+bz,len=(float)Math.Sqrt(mx*mx+mz*mz);
                if(len<1e-5f){mx=ax;mz=az;len=1f;}
                mx/=len;mz/=len;
                float cos=mx*ax+mz*az,scale=d/Math.Max(cos,1f/3f);
                o[2*i]=xz[2*i]+mx*scale;o[2*i+1]=xz[2*i+1]+mz*scale;
            }
            return o;
        }
        static void Normal(float[] v,int a,int b,float s,out float nx,out float nz)
        {
            float dx=v[2*b]-v[2*a],dz=v[2*b+1]-v[2*a+1],l=(float)Math.Sqrt(dx*dx+dz*dz);
            if(l<1e-6f){nx=nz=0;return;}
            nx=-dz/l*s;nz=dx/l*s;
        }

        public static void Centroid(float[] xz,out float cx,out float cz)
        {
            cx=cz=0;int n=xz.Length/2;
            for(int i=0;i<n;i++){cx+=xz[2*i];cz+=xz[2*i+1];}
            if(n>0){cx/=n;cz/=n;}
        }
    }

    /// <summary>
    /// U6.5 island in the sea: the Origin island outline (superellipse in Unity x/z), mirrored from
    /// scripts/art/environment/build_island_base.py (OUTLINE, SEA_Y). Contains the whole gameplay flat rect.
    /// </summary>
    public static class IslandShape
    {
        public const float Cx=0f,Cz=.6f,Hx=10.3f,Hz=9.3f,P=5f,SeaY=-.95f;
        /// <summary>Below 1 inside, 1 on the outline. margin shrinks the outline by that many metres.</summary>
        public static float Level(float x,float z,float margin=0f)=>
            (float)(Math.Pow(Math.Abs(x-Cx)/Math.Max(.1f,Hx-margin),P)+Math.Pow(Math.Abs(z-Cz)/Math.Max(.1f,Hz-margin),P));
        public static bool Inside(float x,float z,float margin=0f)=>Level(x,z,margin)<1f;
        public static void Edge(double t,out float x,out float z)
        {
            double c=Math.Cos(t),s=Math.Sin(t),r=Math.Pow(Math.Pow(Math.Abs(c),P)+Math.Pow(Math.Abs(s),P),-1.0/P);
            x=(float)(Cx+Hx*r*c);z=(float)(Cz+Hz*r*s);
        }
    }

    /// <summary>
    /// U6.5 (phase C) system buildings on the island: each opens its HUD page, like the buildings of
    /// dragon-park games. Placed in the strip behind the habitats (outside every habitat polygon, inside the
    /// flat rect). Model = Resources/IslandBuildings/&lt;Model&gt;.fbx; an empty Model is the built-in placeholder.
    /// </summary>
    public sealed class IslandBuilding
    {
        public string Id,Vi,En,Model;public HudPage Page;public float X,Z,Yaw;
        public string Label(bool vi)=>vi?Vi:En;
    }
    public static class IslandBuildings
    {
        /// <summary>Environment props closer than this to a building are hidden (the building replaces them).</summary>
        public const float ClearRadius=1.15f;
        public static readonly IslandBuilding[] Items=
        {
            new IslandBuilding{Id="hatchery",Vi="Tổ ấp",En="Hatchery",Model="BLD_Hatchery",Page=HudPage.Eggs,X=-6.3f,Z=6.25f},
            new IslandBuilding{Id="research",Vi="Nghiên cứu",En="Research",Model="BLD_Research",Page=HudPage.Research,X=-3.5f,Z=6.3f},
            new IslandBuilding{Id="codex",Vi="Bách khoa",En="Codex",Model="BLD_Codex",Page=HudPage.Biology,X=3.7f,Z=6.3f},
            new IslandBuilding{Id="store",Vi="Kho",En="Storehouse",Model="",Page=HudPage.Manage,X=7.45f,Z=6.2f},
        };
    }
}
