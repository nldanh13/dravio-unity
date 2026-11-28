using System.Linq;
using Dravio.Domain;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using NUnit.Framework;

namespace Dravio.Tests
{
    /// <summary>U6.2: collection book, "while you were away" report, chapter progress and finale.</summary>
    public sealed class SanctuaryCollectionTests
    {
        const long Now=1795000000000;
        static int seq;
        static string Rid()=>"collection-"+(++seq);
        static GameState New(string id)=>InitialStateFactory.Create(id,Now,Rng.Create(3));

        [Test] public void BookListsEverySpeciesWithAWayToGetMostOfThem()
        {
            var s=New("book");
            var book=SanctuaryCollection.Build(s);
            Assert.That(book.Select(e=>e.SpeciesId),Is.EqualTo(SpeciesCatalog.All.Select(x=>x.Id)));
            Assert.That(book.Single(e=>e.SpeciesId=="emberling").Discovered,Is.True);
            Assert.That(book.Count(e=>e.Discovered),Is.EqualTo(SanctuaryCollection.DiscoveredCount(s)));
            Assert.That(book.Single(e=>e.SpeciesId=="droplet").HowVi,Does.Contain("Thủy Vực"),"world reward egg");
            Assert.That(book.Single(e=>e.SpeciesId=="aurorawing").HowVi,Does.Contain("Sân mây"));
            Assert.That(book.Single(e=>e.SpeciesId=="magma").HowVi,Does.Contain("Lai Emberling × Sprout"),"breeding hint names a real parent pair");
            foreach(var e in book)Assert.That(e.HowEn,Is.Not.Empty);
        }

        [Test] public void AwayReportAppearsOnlyAfterOfflineSettlement()
        {
            var s=New("away");var clock=new FixedClock(Now);
            Assert.That(AwaySummary.Build(s),Is.Null);
            clock.Advance(3*3_600_000);
            s=CommandDispatcher.Dispatch(s,new AdvanceTimeCommand(Rid(),AdvanceMode.Offline),clock).State;
            var r=AwaySummary.Build(s);
            Assert.That(r,Is.Not.Null);
            Assert.That(r.Title(true),Does.Contain("3 giờ"));
            Assert.That(r.Lines(true),Is.Not.Empty);
            Assert.That(AwaySummary.Build(s).Id,Is.EqualTo(r.Id),"same receipt = same report, shown once");
        }

        [Test] public void ChapterProgressCountsTutorialSteps()
        {
            var s=New("progress");
            var p=PlayerJourneyPlanner.Next(s);
            Assert.That((p.ChapterRemaining,p.Progress(true)),Is.EqualTo((8,"Bước 1/8")));
            Assert.That(p.FocusUid,Is.EqualTo(s.Dragons[0].Uid),"Care steps frame the dragon they are about");
        }

        [Test] public void FinaleFollowsWorldUnlockWithItsRewardEgg()
        {
            var s=New("finale");
            var f=PlayerJourneyPlanner.ChapterFinale(s,new DomainEvent[]{new ZoneUnlocked("ocean"),new RewardEggGranted("ZONE_EGG_OCEAN","droplet",true)});
            Assert.That(f.Title(true),Does.Contain("Chương 2"));
            Assert.That(f.Lines(true).Any(l=>l.Contains("Droplet")),Is.True);
            Assert.That(PlayerJourneyPlanner.ChapterFinale(s,new DomainEvent[]{new HabitatUnlocked("origin","origin_pondbank")}),Is.Null);
        }
    }
}
