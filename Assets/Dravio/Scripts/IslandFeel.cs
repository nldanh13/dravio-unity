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
        public const float MinZoom=.45f,DefaultZoom=.72f;
        public static float ClampZoom(float z)=>z<MinZoom?MinZoom:(z>1f?1f:z);

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
}
