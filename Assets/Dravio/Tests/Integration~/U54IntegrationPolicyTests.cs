using Dravio.Domain;
using Dravio.Domain.Core;
using Dravio.Domain.Behavior;
using Dravio.Domain.Queries;
using NUnit.Framework;
namespace Dravio.Tests
{
    public sealed class U54IntegrationPolicyTests
    {
        [TestCase(true,true,true,true,false,true)]
        [TestCase(false,true,true,true,false,false)]
        [TestCase(true,false,true,true,false,false)]
        [TestCase(true,true,false,true,false,false)]
        [TestCase(true,true,true,false,false,false)]
        [TestCase(true,true,true,true,true,false)]
        public void MotionContractFailsClosed(bool live,bool visible,bool ground,bool canTranslate,bool actionPlaying,bool expected)
            =>Assert.That(U54MotionPolicy.MayMove(live,visible,ground,canTranslate,actionPlaying),Is.EqualTo(expected));

        [Test] public void NewProgressionQueriesAreReadOnlyAndProductionFlagsRemainOff()
        {
            const long now=1795000000000;
            var state=InitialStateFactory.Create("u54-preview",now,Rng.Create(1));
            var d=state.Dragons[0];
            var overview=ProgressionQueries.Overview(state);
            var r=ProgressionQueries.ResearchProfile(state,d.SpeciesId);
            var social=ProgressionQueries.SocialProfile(state,d.Uid);
            var rules=overview.Rules;
            Assert.That(r.Species,Is.Not.Null);Assert.That(social,Is.Not.Null);
            Assert.That(rules.ResearchRecordingEnabled && rules.NightJournalEnabled,Is.True);
            var economy=ProgressionQueries.SanctuaryEconomy(state,now);
            Assert.That(economy.StarterDailyLimit,Is.EqualTo(5));
            Assert.That(economy.StarterPacksLeftToday,Is.EqualTo(5));
            Assert.That(economy.StarterPriceGoldMicro,Is.EqualTo(150000000L));
            Assert.That(economy.ProjectsBuilt,Is.Zero);
            // U2.11 (ADR 0206): hoàn thành nhiệm vụ là luật thật. Khóa Codex, tác động xã hội, comfort drift, thiên tai, tiêu hao năng lượng vẫn TẮT.
            Assert.That(rules.QuestCompletionEnabled,Is.True);
            Assert.That(rules.ResearchPacingEnabled||rules.SocialGameplayEffectsEnabled||
                rules.ComfortDriftEnabled||rules.NaturalDisastersEnabled||rules.EnergyDrainEnabled,Is.False);
            foreach(var q in ProgressionQueries.QuestRequirements(state))Assert.That(q.CanComplete,Is.False,"save mới chưa đủ điều kiện nhiệm vụ nào");
            foreach(var c in ProgressionQueries.SocialInteractionCandidates(state,now,DayPhase.Day))
            {Assert.That(c.AffectsGameplay,Is.False);Assert.That(c.Role,Is.EqualTo(BehaviorRole.Presentation));}
            Assert.That(ProgressionQueries.Overview(state).CodexCells,Is.EqualTo(overview.CodexCells));
        }
    }
}
