using System;
using System.Linq;
using Dravio.Domain;
using Dravio.Domain.Core;
using Dravio.Domain.Progression;
using Dravio.Persistence.Schema;
using Dravio.Persistence.Tests.Support;
using NUnit.Framework;

namespace Dravio.Persistence.Tests
{
    public sealed class MigrationChronicleTests
    {
        [Test]
        public void Weekend_stamp_requires_consumption_and_survives_reload()
        {
            long start = new DateTimeOffset(2026,11,28,0,0,0,TimeSpan.FromHours(7)).ToUnixTimeMilliseconds();
            var clock = new FixedClock(start);
            var state = InitialStateFactory.Create("chronicle",start,Rng.Create(7));
            var feed = CommandDispatcher.Dispatch(state,new FeedCommand("drop",DietId.Fruit,"origin","origin_meadow"),clock);
            Assert.That(feed.Ok,Is.True);
            Assert.That(feed.State.FestivalStamps,Is.Empty);
            var eat = new ConsumeFoodCommand("eat",feed.State.Food.Drops[0].Id,state.Dragons[0].Uid);
            var result = CommandDispatcher.Dispatch(feed.State,eat,clock);
            Assert.That(result.Ok,Is.True);
            var back = SaveFile.Parse(SaveSchema.Serialize(result.State,start));
            Assert.That(back.Ok,Is.True,back.ToString());
            Assert.That(back.Save.State.FestivalStamps,Is.EqualTo(new[]{"picnic/2026-11-28"}));
            Assert.That(CommandDispatcher.Dispatch(back.Save.State,eat,clock).State.FestivalStamps.Count,Is.EqualTo(1));
        }

        [Test]
        public void Calendar_uses_local_midnight_and_keeps_overlapping_events()
        {
            long midnight = new DateTimeOffset(2027,1,2,0,0,0,TimeSpan.FromHours(7)).ToUnixTimeMilliseconds();
            var day = MigrationChronicle.LocalDay(midnight);
            Assert.That(MigrationChronicle.LocalDay(midnight-1),Is.EqualTo(day.AddDays(-1)));
            var live = MigrationChronicle.Agenda(day).Where(v=>v.Active(day)).Select(v=>v.Id);
            Assert.That(live,Is.EquivalentTo(new[]{"newyear","picnic","tracks"}));
            day = day.AddDays(2);
            Assert.That(MigrationChronicle.Agenda(day).Any(v=>v.Active(day)),Is.False);
            Assert.That(MigrationChronicle.Agenda(day).Any(v=>v.Id=="first-flock"),Is.False);
        }

        [Test]
        public void Existing_milestones_unlock_chapters_in_order()
        {
            var state = PFix.Fresh();
            Assert.That(MigrationChronicle.ChaptersCleared(state),Is.Zero);
            state.Player.Stats.TotalFeedClicks++;
            Assert.That(MigrationChronicle.ChaptersCleared(state),Is.EqualTo(1));
            state.FindZone("origin").UnlockedHabitatIds.Add("origin_pondbank");
            Assert.That(MigrationChronicle.ChaptersCleared(state),Is.EqualTo(2));
            state.Player.Stats.TotalHatched++;
            Assert.That(MigrationChronicle.ChaptersCleared(state),Is.EqualTo(3));
            state.ExpeditionsClaimed["origin_forest"]=1;
            Assert.That(MigrationChronicle.ChaptersCleared(state),Is.EqualTo(4));
        }
    }
}
