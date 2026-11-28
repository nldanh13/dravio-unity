using System;
using System.Collections.Generic;
using System.Linq;
using Dravio.Domain;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Progression;
using Dravio.Domain.World;
using NUnit.Framework;

namespace Dravio.Tests
{
    /// <summary>
    /// U6.1: the journey banner must never leave a fresh player without a next step, and following only the
    /// banner (the page it opens + the button its hint names) must carry a new save through the tutorial into
    /// chapter 2. The bot below maps each prompt code to the exact command the named HUD button sends.
    /// </summary>
    public sealed class PlayerJourneyPlannerTests
    {
        const long Now=1795000000000;
        static int seq;
        static string Rid()=>"journey-"+(++seq);
        static readonly MoveTarget Meadow=MoveTarget.Zone("origin","origin_meadow");

        sealed class Game
        {
            public GameState S;public FixedClock Clock;
            public Game(string id){Clock=new FixedClock(Now);S=InitialStateFactory.Create(id,Now,Rng.Create(7));}
            public DispatchResult Send(GameCommand c)
            {
                var r=CommandDispatcher.Dispatch(S,c,Clock);
                Assert.That(r.Ok,Is.True,c.GetType().Name+" → "+r.Error?.Code.ToWire());
                S=r.State;return r;
            }
            public void Wait(long ms){Clock.Advance(ms);Send(new AdvanceTimeCommand(Rid(),AdvanceMode.Online));}
            public DragonState Ember=>S.Dragons.First(d=>d.SpeciesId=="emberling");
        }

        [Test] public void FreshSaveStartsChapterOneOnCarePage()
        {
            var g=new Game("fresh");
            var p=PlayerJourneyPlanner.Next(g.S);
            Assert.That((p.Chapter,p.Code,p.Page),Is.EqualTo((1,"tutorial.TUT_01",HudPage.Care)));
            Assert.That(p.Title(true),Is.Not.Empty);Assert.That(p.Hint(false),Is.Not.Empty);
        }

        [Test] public void FollowingOnlyTheBannerFinishesTheTutorialAndOpensChapterTwo()
        {
            var g=new Game("bot");
            var seen=new List<string>();
            for(int i=0;i<400 && PlayerJourneyPlanner.Next(g.S).Chapter==1;i++)
            {
                var p=PlayerJourneyPlanner.Next(g.S);
                if(seen.LastOrDefault()!=p.Code)seen.Add(p.Code);
                switch(p.Code)
                {
                    case "tutorial.TUT_01":g.Wait(1000);break;
                    case "tutorial.TUT_02":
                        Assert.That(p.Page,Is.EqualTo(HudPage.Care));
                        g.Clock.Advance(1000);
                        var fed=g.Send(new FeedCommand(Rid(),DietId.Fruit,"origin","origin_meadow"));
                        g.Send(new ConsumeFoodCommand(Rid(),fed.State.Food.Drops.Last().Id,g.Ember.Uid));break;
                    case "tutorial.TUT_03":
                        Assert.That(p.Page,Is.EqualTo(HudPage.Biology));
                        g.Send(new ViewBiologyProfileCommand(Rid(),"emberling"));break;
                    case "tutorial.TUT_04":
                        Assert.That(p.Page,Is.EqualTo(HudPage.Guide));
                        g.Send(new SurveyHabitatCommand(Rid(),"origin_warm_rocks"));g.Send(new UnlockHabitatCommand(Rid(),"origin_warm_rocks"));break;
                    case "tutorial.TUT_05":
                        Assert.That(p.Page,Is.EqualTo(HudPage.Guide));
                        g.Send(new BuyTutorialPackCommand(Rid()));break;
                    case "claim.pack":
                        Assert.That((p.Page,p.Urgent),Is.EqualTo((HudPage.Guide,true)),"tutorial egg is claimed from the Guide page");
                        g.Send(new ClaimPackCommand(Rid(),Meadow));break;
                    case "tutorial.TUT_06":
                        g.Send(new SurveyHabitatCommand(Rid(),"origin_pondbank"));g.Send(new UnlockHabitatCommand(Rid(),"origin_pondbank"));break;
                    case "tutorial.TUT_07":
                        Assert.That(p.Page,Is.EqualTo(HudPage.Care));
                        g.Wait(60_000);break;
                    default:Assert.Fail("Unexpected prompt during tutorial: "+p.Code);break;
                }
            }
            Assert.That(seen,Is.EqualTo(new[]{"tutorial.TUT_01","tutorial.TUT_02","tutorial.TUT_03","tutorial.TUT_04",
                "tutorial.TUT_05","claim.pack","tutorial.TUT_06","tutorial.TUT_07"}));
            Assert.That(g.S.Tutorial.Status,Is.EqualTo(TutorialStatus.Completed));
            var next=PlayerJourneyPlanner.Next(g.S);
            Assert.That(next.Chapter,Is.EqualTo(2));
            Assert.That(next.Code,Does.StartWith("journey."),"chapter 2 follows the canonical JourneyGuide toward Ocean");
            Assert.That(next.Hint(true),Does.Contain("Thủy Vực"));
        }

        [Test] public void WaitingRewardsOutrankTheTutorialStep()
        {
            var g=new Game("reward");
            g.Wait(16_000);
            g.S.PendingPack=null;
            g.S.BreedingNest=new BreedingEgg{Id="egg",ParentIds=new[]{"a","b"},SpeciesId="emberling",Status=EggStatus.Ready};
            Assert.That(PlayerJourneyPlanner.Next(g.S).Code,Is.EqualTo("claim.nest"));
            Assert.That(PlayerJourneyPlanner.Next(g.S).Page,Is.EqualTo(HudPage.Eggs));
        }

        [Test] public void SkippedTutorialOffersOneTapResume()
        {
            var g=new Game("skip");
            g.Send(new SetTutorialStatusCommand(Rid(),TutorialStatus.Skipped));
            var p=PlayerJourneyPlanner.Next(g.S);
            Assert.That((p.Chapter,p.Code,p.Quick),Is.EqualTo((2,"journey.resume-tutorial",JourneyQuickAction.ResumeTutorial)));
        }

        [Test] public void EveryStateOnTheBotPathHasAPrompt()
        {
            // Never null, never empty text, page always a real drawer page — across day/night and offline catch-up.
            var g=new Game("coverage");
            for(int i=0;i<30;i++)
            {
                var p=PlayerJourneyPlanner.Next(g.S);
                Assert.That(p,Is.Not.Null);
                Assert.That(Enum.IsDefined(typeof(HudPage),p.Page),Is.True);
                Assert.That(string.IsNullOrWhiteSpace(p.TitleVi)||string.IsNullOrWhiteSpace(p.TitleEn),Is.False,p.Code);
                Assert.That(p.ChapterLabel(true),Does.Contain("Chương").Or.Contain("Khu bảo tồn"));
                g.Clock.Advance(3_600_000);g.Send(new AdvanceTimeCommand(Rid(),AdvanceMode.Offline));
            }
        }

        [Test] public void CelebrationsCoverJourneyMilestones()
        {
            var g=new Game("events");
            Assert.That(PlayerJourneyPlanner.Celebrate(g.S,new ZoneUnlocked("ocean"),true),Does.Contain("Thủy Vực"));
            Assert.That(PlayerJourneyPlanner.Celebrate(g.S,new TutorialStepCompleted("TUT_03"),true),Does.Contain("3/8"));
            Assert.That(PlayerJourneyPlanner.Celebrate(g.S,new HabitatUnlocked("origin","origin_pondbank"),false),Does.Contain("Pondbank"));
        }
    }
}
