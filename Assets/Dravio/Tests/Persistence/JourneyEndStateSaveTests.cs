using System.Linq;
using Dravio.Domain;
using Dravio.Domain.Core;
using Dravio.Domain.Progression;
using Dravio.Domain.Queries;
using Dravio.Domain.Tests.Simulation;
using Dravio.Persistence.Schema;
using Dravio.Persistence.Tests.Support;
using NUnit.Framework;

namespace Dravio.Persistence.Tests
{
    /// <summary>
    /// U2.12 (ADR 0207) × Save Schema v10 — không đổi schema. Save của một hành trình thật (người chơi theo hướng dẫn, chỉ lệnh thật, từ save mới tới khi mở
    /// đủ ba thế giới) qua vòng lưu–nạp từng byte, hợp lệ, và sau khi nạp không nhận lại được nhiệm vụ / trứng thế giới.
    /// </summary>
    public sealed class JourneyEndStateSaveTests
    {
        private static GameState ThreeWorlds(out RunResult run)
        {
            var sim = new EconomySimulator(Policy.Guided, new Schedule("3 × 10 phút/ngày", (0, 10), (4, 10), (11, 10)), 2, EconomyVariant.Live);
            run = sim.Run(new double[] { 24 * 20 });
            Assert.That(sim.State.Zones.All(z => z.Unlocked), Is.True, "hành trình mở đủ ba thế giới");
            return sim.State;
        }

        [Test]
        public void Three_world_journey_save_round_trips_byte_for_byte_under_schema_v10()
        {
            GameState s = ThreeWorlds(out _);
            Assert.That(s.SchemaVersion, Is.EqualTo(10));
            string text = SaveSchema.Serialize(s, PFix.T0);
            ParseResult r = SaveFile.Parse(text);
            Assert.That(r.Ok, Is.True, r.ToString());
            GameState back = r.Save.State;
            Assert.That(PFix.Canonical(back), Is.EqualTo(PFix.Canonical(SaveSchema.ToPersistedState(s))));
            Assert.That(GameStateValidator.Validate(back).IsValid, Is.True, GameStateValidator.Validate(back).ToString());
            Assert.That(back.CompletedQuests, Is.EqualTo(new[] { "QUEST_AQUATIC_EXPLORATION", "QUEST_SKY_EXPLORATION" }));
            Assert.That(back.CompletedSideQuests, Is.EqualTo(s.CompletedSideQuests));
            Assert.That(back.ExpeditionsClaimed, Is.EqualTo(s.ExpeditionsClaimed));
            // Truy vấn chỉ đọc cho cùng kết quả trên state đã nạp lại.
            Assert.That(ProgressionQueries.Journey(back).Steps, Is.Empty);
            Assert.That(ProgressionQueries.QuestLog(back).ReadyQuestIds, Is.EqualTo(ProgressionQueries.QuestLog(s).ReadyQuestIds));
            // Ảnh v4 cho TS không kiểm ở đây: hành trình thật thường dừng khi đang có chuyến thám hiểm, mà ảnh v4 không biểu diễn được (giới hạn từ U2.8).
        }

        [Test]
        public void After_reload_nothing_can_be_claimed_twice()
        {
            GameState s = ThreeWorlds(out _);
            GameState back = SaveFile.Parse(SaveSchema.Serialize(s, PFix.T0)).Save.State;
            var clock = new FixedClock(back.Clock.HighWaterAtMs + 60_000);
            foreach (string q in back.CompletedQuests.Concat(back.CompletedSideQuests))
                Assert.That(CommandDispatcher.Dispatch(back, new CompleteQuestCommand(PFix.Rid(), q), clock).Error?.Code, Is.EqualTo(DomainErrorCode.QuestAlreadyCompleted), q);
            Assert.That(CommandDispatcher.Dispatch(back, new UnlockZoneCommand(PFix.Rid(), "sky"), clock).Error?.Code, Is.EqualTo(DomainErrorCode.AlreadyThere));
            Assert.That(back.GrantedRewards.Count(id => id == "ZONE_EGG_SKY"), Is.EqualTo(1));
        }
    }
}
