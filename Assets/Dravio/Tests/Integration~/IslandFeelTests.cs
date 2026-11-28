using System.Collections.Generic;
using System.Linq;
using Dravio.Domain;
using Dravio.Domain.Core;
using Dravio.Domain.Catalog;
using NUnit.Framework;

namespace Dravio.Tests
{
    /// <summary>U6.3: island camera bounds, dragon bubbles, gold popups and the bottom dock.</summary>
    public sealed class IslandFeelTests
    {
        const long Now=1795000000000;
        static GameState New(string id)=>InitialStateFactory.Create(id,Now,Rng.Create(5));
        // Fitted overview: free area = left 60% (drawer on the right), a little top/bottom margin.
        const float X0=.03f,X1=.6f,Y0=.05f,Y1=.94f,HalfW=6f*16f/9f,HalfH=6f;

        [Test] public void ZoomedViewNeverLeavesTheFittedFreeArea()
        {
            for(float z=IslandViewMath.MinZoom;z<=1f;z+=.05f)
            {
                float ox=1000,oy=-1000;
                IslandViewMath.Clamp(ref ox,ref oy,z,X0,X1,Y0,Y1,HalfW,HalfH);
                // free area of the zoomed view, in camera-plane units of the fitted view
                Assert.That(ox+(2*X1-1)*HalfW*z,Is.LessThanOrEqualTo((2*X1-1)*HalfW+1e-3f));
                Assert.That(oy+(2*Y0-1)*HalfH*z,Is.GreaterThanOrEqualTo((2*Y0-1)*HalfH-1e-3f));
            }
            float cx=5,cy=5;IslandViewMath.Clamp(ref cx,ref cy,1f,X0,X1,Y0,Y1,HalfW,HalfH);
            Assert.That((cx,cy),Is.EqualTo((0f,0f)),"zoom 1 = exactly the fitted overview");
            IslandViewMath.Centre(IslandViewMath.DefaultZoom,X0,X1,Y0,Y1,HalfW,HalfH,out var dx,out var dy);
            float kx=dx,ky=dy;IslandViewMath.Clamp(ref kx,ref ky,IslandViewMath.DefaultZoom,X0,X1,Y0,Y1,HalfW,HalfH);
            Assert.That((kx,ky),Is.EqualTo((dx,dy)),"default pose is inside the bounds");
        }

        [Test] public void PinchKeepsThePointUnderTheFingersAndDragFollowsTheFinger()
        {
            IslandViewMath.Centre(.9f,X0,X1,Y0,Y1,HalfW,HalfH,out var ox,out var oy);
            float vx=.3f,vy=.5f,before=ox+(2*vx-1)*HalfW*.9f;
            IslandViewMath.ZoomAt(ref ox,ref oy,.9f,.6f,vx,vy,X0,X1,Y0,Y1,HalfW,HalfH);
            Assert.That(ox+(2*vx-1)*HalfW*.6f,Is.EqualTo(before).Within(1e-3f));
            float px=ox;IslandViewMath.Drag(ref ox,ref oy,100,0,1080,.6f,HalfH);
            Assert.That(ox,Is.LessThan(px),"dragging right moves the camera left");
            Assert.That(IslandViewMath.ClampZoom(.1f),Is.EqualTo(IslandViewMath.MinZoom));
        }

        [Test] public void BubbleShowsNameGrowthAndNextStep()
        {
            var s=New("bubble");var d=s.Dragons[0];
            var b=IslandBubble.For(s,d.Uid,d.Uid);
            Assert.That(b.Name,Is.EqualTo(PlayerJourneyPlanner.SpeciesName(d.SpeciesId)));
            Assert.That(b.Stage(true),Is.Not.Empty);
            Assert.That(b.Attention,Is.EqualTo(BubbleAttention.NextStep));
            Assert.That(IslandBubble.For(s,d.Uid,null).Attention,Is.EqualTo(BubbleAttention.None));
            Assert.That(IslandBubble.For(s,"nope",null),Is.Null);
        }

        [Test] public void GoldPopupsAddUpToTheRealOnlineGold()
        {
            var ledger=new GoldPopupLedger{MinGapSec=0};
            var producers=new List<KeyValuePair<string,long>>{new("a",300),new("b",100),new("c",33)};
            long earned=0;
            for(int i=0;i<50;i++){ledger.Earn(777_777,producers);earned+=777_777;}
            long popped=ledger.Due(10,new[]{"a","b","c"}).Sum(p=>p.Value)*1_000_000;
            Assert.That(popped+ledger.TotalOwedMicro,Is.EqualTo(earned));
            Assert.That(ledger.OwedMicro("a"),Is.LessThan(1_000_000));
            var gap=new GoldPopupLedger{MinGapSec=5};gap.Earn(5_000_000,producers);
            Assert.That(gap.Due(0,new[]{"a"}).Count,Is.EqualTo(1));
            gap.Earn(5_000_000,producers);
            Assert.That(gap.Due(1,new[]{"a"}),Is.Empty,"one popup per dragon per gap");
        }

        [Test] public void LedgerFollowsDomainProduction()
        {
            var s=New("gold");var clock=new FixedClock(Now);
            var producers=GoldPopupLedger.Producers(s);
            clock.Advance(120_000);
            var r=CommandDispatcher.Dispatch(s,new AdvanceTimeCommand("gold-1",AdvanceMode.Online),clock);
            var t=r.Events.OfType<TimeAdvanced>().Single();
            var ledger=new GoldPopupLedger();ledger.Earn(t.GoldMicro,producers);
            Assert.That(ledger.TotalOwedMicro,Is.EqualTo(producers.Count>0?t.GoldMicro:0));
            Assert.That(producers.All(p=>s.FindDragon(p.Key)!=null),Is.True);
        }

        [Test] public void DockCoversEveryDrawerPageOnce()
        {
            Assert.That(IslandDock.Items.Select(i=>i.Page).OrderBy(p=>p),Is.EqualTo(System.Enum.GetValues(typeof(HudPage)).Cast<HudPage>().OrderBy(p=>p)));
            Assert.That(IslandDock.TapCloses(true,(int)HudPage.Eggs,HudPage.Eggs),Is.True);
            Assert.That(IslandDock.TapCloses(false,(int)HudPage.Eggs,HudPage.Eggs),Is.False);
        }

        [Test] public void HabitatElementsComeFromTheDomainProfile()
        {
            ElementType E(string id){HabitatCatalog.TryGet(id,out var h);return HabitatLook.ElementOf(h);}
            Assert.That(E("origin_meadow"),Is.EqualTo(ElementType.Nature));
            Assert.That(E("origin_pondbank"),Is.EqualTo(ElementType.Water));
            Assert.That(E("origin_warm_rocks"),Is.EqualTo(ElementType.Fire));
            Assert.That(E("ocean_reef"),Is.EqualTo(ElementType.Water),"ocean realm before temperature");
            Assert.That(E("sky_low_island"),Is.EqualTo(ElementType.Sky));
        }

        static double Area(float[] v,List<int> t)
        {
            double a=0;
            for(int i=0;i<t.Count;i+=3)
            {
                int p=t[i],q=t[i+1],r=t[i+2];
                a+=System.Math.Abs((v[2*q]-v[2*p])*(v[2*r+1]-v[2*p+1])-(v[2*q+1]-v[2*p+1])*(v[2*r]-v[2*p]))/2;
            }
            return a;
        }

        [Test] public void PlotFillCoversConcavePolygonsInEitherWinding()
        {
            var l=new float[]{0,0, 4,0, 4,1, 1,1, 1,3, 0,3}; // L shape, area 6
            var t=HabitatLook.Triangulate(l);
            Assert.That(t.Count,Is.EqualTo(12));
            Assert.That(Area(l,t),Is.EqualTo(6).Within(1e-4));
            var cw=new float[l.Length];for(int i=0;i<6;i++){cw[2*i]=l[2*(5-i)];cw[2*i+1]=l[2*(5-i)+1];}
            Assert.That(Area(cw,HabitatLook.Triangulate(cw)),Is.EqualTo(6).Within(1e-4));
        }

        [Test] public void PlotBorderInsetMovesInward()
        {
            var sq=new float[]{0,0, 2,0, 2,2, 0,2};
            var a=HabitatLook.Inset(sq,.2f);
            Assert.That(a,Is.EqualTo(new[]{.2f,.2f, 1.8f,.2f, 1.8f,1.8f, .2f,1.8f}).Within(1e-4f));
            var rev=new float[]{0,2, 2,2, 2,0, 0,0};
            Assert.That(HabitatLook.Inset(rev,.2f)[0],Is.EqualTo(.2f).Within(1e-4f),"clockwise input also shrinks");
        }
    }
}
