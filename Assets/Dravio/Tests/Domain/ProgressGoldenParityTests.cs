using System.Collections.Generic;
using System.Linq;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Progression;
using Dravio.Domain.Tests.Support;
using NUnit.Framework;

namespace Dravio.Domain.Tests
{
    /// <summary>
    /// Đối chiếu U2.4 với golden sinh từ TypeScript gốc @ b33ec7a (Golden/ts-progress-golden-b33ec7a.json,
    /// Tools~/progress-golden.test.ts). Khác golden U2.1/U2.3: so TOÀN BỘ state (tutorial, thành tựu, vàng thật gồm thưởng)
    /// và MỌI event, cùng probe currentStep / packPlacementOptions / hatchFromPack sau từng bước.
    /// </summary>
    public sealed class ProgressGoldenParityTests
    {
        private static Dictionary<string, object> O(object v) => (Dictionary<string, object>)v;
        private static List<object> A(object v) => (List<object>)v;

        [Test]
        public void Progress_golden_targets_the_reference_commit_and_same_implemented_achievements()
        {
            Assert.That(GoldenFile.Progress["source"], Is.EqualTo("nldanh13/dravio@" + ContentInfo.ReferenceCommit));
            Assert.That(Achievements.Implemented, Is.EqualTo(A(GoldenFile.Progress["implemented"])));
        }

        private static IEnumerable<TestCaseData> Scenarios()
        {
            foreach (var sc in A(GoldenFile.Progress["scenarios"]).Select(O))
                yield return new TestCaseData((string)sc["name"]).SetName("Progress_" + (string)sc["name"]);
        }

        private static object Probe(GameState s) => new Dictionary<string, object>
        {
            ["currentStep"] = Tutorial.CurrentStep(s.Tutorial),
            ["packOptions"] = StarterPack.PackPlacementOptions(s).Select(o => (object)new Dictionary<string, object>
            {
                ["zoneId"] = o.ZoneId, ["habitatId"] = o.HabitatId, ["score"] = (double)o.Result.Score, ["freeSlots"] = (double)o.FreeSlots, ["blocked"] = o.Blocked?.ToWire(),
            }).ToList(),
            ["hatchPreview"] = s.PendingPack == null ? null : StateJson.Dragon(StarterPack.HatchFromPack(s.PendingPack, 0)),
        };

        [TestCaseSource(nameof(Scenarios))]
        public void Progress_replay_matches_TypeScript_full_state_and_all_events(string name)
        {
            var sc = A(GoldenFile.Progress["scenarios"]).Select(O).Single(x => (string)x["name"] == name);
            GameState state = StateJson.FromJson(O(sc["start"]));
            Assert.That(GameStateValidator.Validate(state).IsValid, Is.True, GameStateValidator.Validate(state).ToString());
            Assert.That(MiniJson.Diff(sc["start"], StateJson.ToGoldenFull(state, fullIds: true)), Is.Null, "start round-trip");
            var clock = new FixedClock(0);
            var steps = A(sc["steps"]);
            for (int i = 0; i < steps.Count; i++)
            {
                var st = O(steps[i]);
                string at = name + " step " + i + " " + MiniJson.Show(O(st["command"])["type"]);
                clock.Set((long)(double)st["nowMs"]);
                DispatchResult r = CommandDispatcher.Dispatch(state, StateJson.Command(O(st["command"])), clock);
                Assert.That(r.Ok, Is.EqualTo((bool)st["ok"]), at + " ok (error " + r.Error + ")");
                Assert.That(r.Duplicate, Is.EqualTo((bool)st["duplicate"]), at + " duplicate");
                if (!r.Ok) Assert.That(r.Error.WireCode, Is.EqualTo(st["errorCode"]), at + " error code");
                Assert.That(ReferenceEquals(r.State, state), Is.EqualTo((bool)st["sameStateObject"]), at + " same object");
                Assert.That(MiniJson.Diff(st["events"], r.Events.Select(e => (object)StateJson.Event(e)).ToList()), Is.Null, at + " events");
                if (st["state"] != null)
                {
                    Assert.That(MiniJson.Diff(st["state"], StateJson.ToGoldenFull(r.State, fullIds: false)), Is.Null, at + " state");
                    Assert.That(GameStateValidator.Validate(r.State).IsValid, Is.True, at + " invalid: " + GameStateValidator.Validate(r.State));
                }
                Assert.That(MiniJson.Diff(st["probe"], Probe(r.State)), Is.Null, at + " probe");
                state = r.State;
            }
            Assert.That(MiniJson.Diff(sc["final"], StateJson.ToGoldenFull(state, fullIds: true)), Is.Null, name + " final");
        }
    }
}
