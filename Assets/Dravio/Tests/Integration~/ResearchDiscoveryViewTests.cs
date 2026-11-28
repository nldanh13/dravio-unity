using System.Linq;
using Dravio.Domain;
using Dravio.Domain.Behavior;
using Dravio.Domain.Core;
using Dravio.Domain.Queries;
using Dravio.Domain.Progression;
using NUnit.Framework;
namespace Dravio.Tests
{
    public sealed class ResearchDiscoveryViewTests
    {
        const long Now=1795000000000;
        static GameState New()=>InitialStateFactory.Create("research-ui",Now,Rng.Create(1));
        [TestCase(true)]
        [TestCase(false)]
        public void ChapterHintsFollowTheCurrentMilestone(bool vi)
        {
            var state=New();
            for(int chapter=0; chapter<4; chapter++)
            {
                string clue=ChronicleView.Story(state,vi);
                var guided=ChronicleView.Story(state,vi,true);
                Assert.That(clue,Does.Contain("?"));
                Assert.That(guided.Length,Is.GreaterThan(clue.Length));
                Assert.That(clue,Does.Not.Contain(vi?"Kết ·":"Epilogue ·"));
                switch(chapter)
                {
                    case 0: state.Player.Stats.TotalFeedClicks++; break;
                    case 1: state.FindZone("origin").UnlockedHabitatIds.Add("origin_pondbank"); break;
                    case 2: state.Player.Stats.TotalHatched++; break;
                    case 3: state.ExpeditionsClaimed["origin_forest"]=1; break;
                }
            }
            Assert.That(ChronicleView.Story(state,vi,true),Is.EqualTo(ChronicleView.Story(state,vi)));
        }

        [Test] public void PlayerLabelsTranslateCanonicalStatusAndStageWithoutChangingIds()
        {
            Assert.That(ResearchDiscoveryView.StageLabel(GrowthStage.Adult,true),Is.EqualTo("Trưởng thành"));
            Assert.That(ResearchDiscoveryView.StatusLabel(DragonStatus.Expedition,true),Is.EqualTo("Đang thám hiểm"));
            Assert.That(ResearchDiscoveryView.HabitatName("origin_meadow",true),Is.EqualTo("Đồng cỏ"));
            Assert.That(ResearchDiscoveryView.HabitatName("future_habitat",false),Is.EqualTo("future_habitat"));
        }
        [Test] public void RelationsSummaryUsesCanonicalTiersAndBoundsVisibleRows()
        {
            var s=New();var uid=s.Dragons[0].Uid;var rev=s.Revision;
            var empty=ProgressionQueries.Relations(s,uid);
            Assert.That(ResearchDiscoveryView.RelationSummary(empty,null,false),Does.Contain("No other individuals"));
            // DTO-only fixture: Domain owns score and ordering in production.
            var rows=new[]{85,55,25,10}.Select((score,i)=>new SocialRelation
                {UidA=i%2==0?uid:"other"+i,UidB=i%2==0?"other"+i:uid,Familiarity=score,Affinity=42,SharedMs=3600000}).ToArray();
            var dto=new RelationsDto {Uid=uid,Relations=rows};
            var text=ResearchDiscoveryView.RelationSummary(dto,x=>"name-"+x,false);
            Assert.That(text,Does.Contain("name-other0 · Close · 85/100").And.Contain("name-other1 · Friend").And.Contain("name-other2 · Acquainted"));
            Assert.That(text,Does.Not.Contain("name-other3"));
            Assert.That(text,Does.Contain("More individuals: 1").And.Contain("Recorded shared time: 1 h"));
            Assert.That(s.Revision,Is.EqualTo(rev));
        }
        [Test] public void UnknownSpeciesDoesNotRevealIdentityOrBiology()
        {
            var s=New();var text=ResearchDiscoveryView.Species(s,"leviathan",false);
            Assert.That(text,Does.Contain("Undiscovered").And.Not.Contain("leviathan"));
            Assert.That(ResearchDiscoveryView.Species(s,"emberling",false),Does.Contain("emberling"));
        }
        [Test] public void LocalOpportunitiesRequireLiveUidAndUnlockedView()
        {
            var s=New();var uid=s.Dragons[0].Uid;
            Assert.That(ResearchDiscoveryView.LocalOpportunities(s,Now,DayPhase.Day,"origin",_=>false),Is.Empty);
            Assert.That(ResearchDiscoveryView.LocalOpportunities(s,Now,DayPhase.Day,"ocean",_=>true),Is.Empty);
            var list=ResearchDiscoveryView.LocalOpportunities(s,Now,DayPhase.Day,"origin",x=>x==uid);
            Assert.That(list.Count,Is.EqualTo(1));Assert.That(list[0].Uid,Is.EqualTo(uid));
            Assert.That(ResearchDiscoveryView.CanRecord(list[0]),Is.True);
            Assert.That(ResearchDiscoveryView.CanRecord(null),Is.False);
        }
        [Test] public void SelectionRemainsBoundToUidWhenOrderChanges()
        {
            Assert.That(ResearchDiscoveryView.KeepOrFirst(new[]{"b","a"},"a"),Is.EqualTo("a"));
            Assert.That(ResearchDiscoveryView.KeepOrFirst(new[]{"b"},"a"),Is.EqualTo("b"));
            Assert.That(ResearchDiscoveryView.Next(new[]{"b","a"},"a"),Is.EqualTo("b"));
            Assert.That(ResearchDiscoveryView.Next(new string[0],"a"),Is.Null);
        }
        [Test] public void RealObservationRefreshShowsCanonicalCooldownAndSavedRecord()
        {
            var s=New();string uid=s.Dragons[0].Uid;var clock=new FixedClock(Now);
            var result=CommandDispatcher.Dispatch(s,new ObserveDragonCommand("research-ui-observe",uid,DayPhase.Day),clock);
            Assert.That(result.Ok,Is.True,result.Error?.ToString());
            Assert.That(result.Events.OfType<DragonObserved>().Single().DragonUid,Is.EqualTo(uid));
            Assert.That(result.State.Research.IsEmpty,Is.False);
            Assert.That(s.Research.IsEmpty,Is.True);
            var opportunity=ResearchDiscoveryView.LocalOpportunities(result.State,Now,DayPhase.Day,"origin",x=>x==uid).Single();
            Assert.That(ResearchDiscoveryView.CanRecord(opportunity),Is.False);
            Assert.That(opportunity.CooldownRemainingMs,Is.GreaterThan(0));
            Assert.That(ProgressionQueries.IndividualResearch(result.State,uid).Observations,Is.EqualTo(1));
        }
        [Test] public void QuestActionUsesExactCanonicalEligibilityAndDoesNotAutoUnlock()
        {
            var s=New();var log=ProgressionQueries.QuestLog(s);
            Assert.That(ResearchDiscoveryView.QuestAction(log,"QUEST_AQUATIC_EXPLORATION"),Is.Null);
            Assert.That(ResearchDiscoveryView.QuestAction(log,"unknown"),Is.Null);
            Assert.That(ResearchDiscoveryView.WorldProgress(log,true),Does.Contain("Hoàn thành hướng dẫn"));
            // DTO-only fixtures exercise presentation; production eligibility remains in Domain.
            var q=log.Quests.Single(x=>x.QuestId=="QUEST_AQUATIC_EXPLORATION");
            q.State=QuestState.Ready;q.CanComplete=true;
            log.ReadyQuestIds=new[]{"QUEST_AQUATIC_EXPLORATION"};
            Assert.That(ResearchDiscoveryView.QuestAction(log,q.QuestId),Is.EqualTo("claim"));
            q.State=QuestState.Completed;q.CanComplete=false;
            Assert.That(ResearchDiscoveryView.QuestAction(log,q.QuestId),Is.Null);
            log.ZoneUnlockMissing=new System.Collections.Generic.Dictionary<string,System.Collections.Generic.IReadOnlyList<string>>
                {{"ocean",new string[0]}};
            Assert.That(ResearchDiscoveryView.QuestAction(log,q.QuestId),Is.EqualTo("ocean"));
            Assert.That(ResearchDiscoveryView.QuestAction(log,"Q_ORIGIN_FIELD_NOTES"),Is.Null);
            log.ZoneUnlockMissing=new System.Collections.Generic.Dictionary<string,System.Collections.Generic.IReadOnlyList<string>>();
            Assert.That(ResearchDiscoveryView.QuestAction(log,q.QuestId),Is.Null);
        }
        [Test] public void QuestDetailsExposeDeliveryAndLockedRoute()
        {
            var s=New();var log=ProgressionQueries.QuestLog(s);
            var q=log.Quests.Single(x=>x.QuestId=="Q_ORIGIN_FOREST_SURVEY");
            Assert.That(ResearchDiscoveryView.Quest(q,true,true),Does.Contain("Vật liệu giao khi nhận").And.Contain("Gỗ").And.Contain("blueprint.forest_lantern"));
            Assert.That(ResearchDiscoveryView.Missing("route-rewards-undefined",true),Does.Contain("chưa có bảng thưởng hoạt động"));
            Assert.That(ResearchDiscoveryView.QuestName("QUEST_SKY_EXPLORATION",true),Is.EqualTo("Khám phá Thiên Giới"));
        }
        [Test] public void JourneyFollowsCanonicalGatePrerequisiteAndKeepsTutorialBlockers()
        {
            var s=New();var log=ProgressionQueries.QuestLog(s);
            Assert.That(ResearchDiscoveryView.NextWorld(log),Is.EqualTo("ocean"));
            Assert.That(ResearchDiscoveryView.JourneyQuest(log),Is.EqualTo("Q_ORIGIN_FIELD_NOTES"));
            Assert.That(ResearchDiscoveryView.JourneySummary(log,true),Does.Contain("Thủy Vực").And.Contain("hướng dẫn").And.Contain("Ghi chép Khởi Nguyên"));
            var gate=log.Quests.Single(q=>q.QuestId=="QUEST_AQUATIC_EXPLORATION");
            gate.LockedBy=new string[0];
            Assert.That(ResearchDiscoveryView.JourneyQuest(log),Is.EqualTo(gate.QuestId));
            log.ZoneUnlockMissing=new System.Collections.Generic.Dictionary<string,System.Collections.Generic.IReadOnlyList<string>>
                {{"sky",new[]{"quest:QUEST_SKY_EXPLORATION"}}};
            Assert.That(ResearchDiscoveryView.NextWorld(log),Is.EqualTo("sky"));
            log.ZoneUnlockMissing=new System.Collections.Generic.Dictionary<string,System.Collections.Generic.IReadOnlyList<string>>();
            Assert.That(ResearchDiscoveryView.JourneyQuest(log),Is.Null);
            Assert.That(ResearchDiscoveryView.JourneySummary(log,false),Does.Contain("all three worlds unlocked"));
        }
        [Test] public void JourneyGuidanceUsesOwnedSpeciesAndUnlockedMaterialSourcesWithoutMutating()
        {
            var s=New();var log=ProgressionQueries.QuestLog(s);long rev=s.Revision;
            var field=log.Quests.Single(q=>q.QuestId=="Q_ORIGIN_FIELD_NOTES");
            var hint=ResearchDiscoveryView.QuestGuidance(s,field,true);
            Assert.That(hint,Does.Contain("emberling").And.Not.Contain("leviathan"));
            var forest=log.Quests.Single(q=>q.QuestId=="Q_ORIGIN_FOREST_SURVEY");
            Assert.That(ResearchDiscoveryView.QuestGuidance(s,forest,true),Does.Contain("Rừng Khởi Nguyên").And.Contain("Còn thiếu"));
            var sky=log.Quests.Single(q=>q.QuestId=="Q_EXPEDITION_CLOUD");
            Assert.That(ResearchDiscoveryView.QuestGuidance(s,sky,false),Does.Contain("prerequisites"));
            Assert.That(s.Revision,Is.EqualTo(rev));
            Assert.That(s.Research.IsEmpty,Is.True);
        }
        [Test] public void CareFeedbackShowsActualEaterAndObservationCooldown()
        {
            Assert.That(ResearchDiscoveryView.FoodFeedback("emberling","123456789",8,false),
                Does.Contain("emberling #123456").And.Contain("+8 XP"));
            var opportunity=new ObservationOpportunity {Availability="observation-cooldown",CooldownRemainingMs=1001};
            Assert.That(ResearchDiscoveryView.ObservationFeedback(opportunity,false),Does.Contain("2 s"));
            Assert.That(ResearchDiscoveryView.ObservationFeedback(null,true),Does.Contain("chưa thể quan sát"));
        }
        [Test] public void CanonicalJourneyShowsTutorialAndNextWorld()
        {
            var s=New();var j=ProgressionQueries.Journey(s);
            Assert.That(ResearchDiscoveryView.CanonicalJourneySummary(j,false),Does.Contain("Ocean").And.Contain("tutorial"));
            Assert.That(ResearchDiscoveryView.CanonicalJourneyFocus(j),Is.EqualTo(j.Steps.First(x=>x.QuestId!=null).QuestId));
        }
        [Test] public void QuestAndCodexBrowsingDoesNotMutateCanonicalState()
        {
            var s=New();long rev=s.Revision,gold=s.Player.Gold.Micro;
            foreach(var q in ProgressionQueries.QuestRequirements(s))
            {Assert.That(ResearchDiscoveryView.Quest(q,false,false),Does.Contain("Completion and rewards disabled"));Assert.That(q.CanComplete,Is.False);}
            ResearchDiscoveryView.Species(s,"emberling",true);
            Assert.That(s.Revision,Is.EqualTo(rev));Assert.That(s.Player.Gold.Micro,Is.EqualTo(gold));
            Assert.That(s.Research.IsEmpty,Is.True);
        }
    }
}
