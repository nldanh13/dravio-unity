using System.Collections.Generic;
using System.Linq;
using Dravio.Domain.Biology;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Tests.Support;
using Dravio.Domain.World;
using NUnit.Framework;

namespace Dravio.Domain.Tests
{
    /// <summary>
    /// Đối chiếu U2.3 với golden sinh từ TypeScript gốc @ b33ec7a (Golden/ts-world-golden-b33ec7a.json,
    /// Tools~/world-golden.test.ts): world graph, placementOptions, habitatUnlockStatus, điều kiện khảo sát,
    /// ngân sách, sức chứa và phát lại lệnh moveDragon / setProductionSlot / unlockHabitat / surveyHabitat / viewBiologyProfile.
    /// </summary>
    public sealed class WorldGoldenParityTests
    {
        private static Dictionary<string, object> O(object v) => (Dictionary<string, object>)v;
        private static List<object> A(object v) => (List<object>)v;
        private static Dictionary<string, object> Graph => O(GoldenFile.World["graph"]);

        private static Locomotion[] Locos(object list) => A(list).Select(x => { WireNames.TryParse((string)x, out Locomotion l); return l; }).ToArray();

        [Test]
        public void World_golden_targets_the_reference_commit()
        {
            Assert.That(GoldenFile.World["source"], Is.EqualTo("nldanh13/dravio@" + ContentInfo.ReferenceCommit));
        }

        [Test]
        public void Edges_gateways_and_zone_habitats_match()
        {
            var edges = A(Graph["edges"]).Select(O).ToList();
            Assert.That(WorldGraph.Edges.Select(e => e.Id), Is.EqualTo(edges.Select(e => e["id"])));
            foreach (var (e, g) in WorldGraph.Edges.Zip(edges, (a, b) => (a, b)))
            {
                Assert.That((e.A, e.B), Is.EqualTo(((string)g["a"], (string)g["b"])));
                Assert.That(e.Requires.Select(r => r.ToWire()), Is.EqualTo(A(g["requires"])));
                var gw = O(g["gateways"]);
                Assert.That(e.Gateways.Count, Is.EqualTo(gw.Count));
                foreach (var kv in gw) Assert.That(e.Gateways[kv.Key], Is.EqualTo(kv.Value));
                foreach (var kv in e.Gateways) Assert.That(HabitatCatalog.ZoneOf(kv.Value), Is.EqualTo(kv.Key), "gateway habitat belongs to its zone");
            }
            foreach (var kv in O(Graph["edgesOf"])) Assert.That(WorldGraph.EdgesOf(kv.Key).Select(e => e.Id), Is.EqualTo(A(kv.Value)));
            foreach (var kv in O(Graph["habitatsOfZone"])) Assert.That(Zones.HabitatsOfZone(kv.Key), Is.EqualTo(A(kv.Value)));
        }

        [Test]
        public void CanTravel_matches_for_every_zone_pair_and_locomotion_subset()
        {
            var rows = A(Graph["travel"]).Select(O).ToList();
            Assert.That(rows.Count, Is.EqualTo(3 * 3 * 16));
            foreach (var t in rows)
                Assert.That(WorldGraph.CanTravel((string)t["from"], (string)t["to"], Locos(t["locomotion"])), Is.EqualTo((bool)t["ok"]),
                    t["from"] + "→" + t["to"] + " " + string.Join(",", A(t["locomotion"])));
        }

        [Test]
        public void Every_species_can_reach_every_zone_holding_one_of_its_allowed_habitats()
        {
            foreach (var r in A(Graph["reach"]).Select(O))
            {
                SpeciesBiology b = SpeciesCatalog.GetBiology((string)r["speciesId"]);
                Assert.That(WorldGraph.CanTravel("origin", (string)r["zoneId"], b.Locomotion), Is.EqualTo((bool)r["ok"]).And.True, r["speciesId"] + "@" + r["habitatId"]);
            }
        }

        [Test]
        public void CheckPlacement_for_a_habitat_outside_the_zone_is_habitat_locked_without_details()
        {
            GameState s = InitialStateFactory.Create("zones", (long)(double)GoldenFile.World["T0"], Rng.Create(8));
            foreach (var m in A(GoldenFile.World["mismatch"]).Select(O))
            {
                PlacementCheck c = Zones.CheckPlacement(s, s.Dragons[0], (string)m["zoneId"], (string)m["habitatId"], out DomainErrorCode? blocked);
                Assert.That(c, Is.Null);
                Assert.That(blocked?.ToWire(), Is.EqualTo(O(m["check"])["blocked"]));
            }
        }

        /// <summary>Tập event golden U2.3 ghi lại (thành tựu/hướng dẫn được so đầy đủ ở golden U2.4).</summary>
        private static readonly HashSet<string> WorldEvents = new HashSet<string> { "timeAdvanced", "stageChanged", "offlineReceiptCreated", "foodSpawned", "foodExpired", "foodConsumed", "bondChanged", "dragonPetted", "dragonRenamed",
            "dragonMoved", "productionChanged", "habitatUnlocked", "discoveryRecorded", "habitatSurveyed", "biologyProfileViewed" };

        private static IEnumerable<TestCaseData> Scenarios()
        {
            foreach (var sc in A(GoldenFile.World["scenarios"]).Select(O))
                yield return new TestCaseData((string)sc["name"]).SetName("World_" + (string)sc["name"]);
        }

        private static object PlacementJson(GameState s, DragonState d) =>
            Zones.PlacementOptions(s, d).Select(o => (object)new Dictionary<string, object>
            {
                ["zoneId"] = o.ZoneId, ["habitatId"] = o.HabitatId, ["allowed"] = o.Result.Allowed, ["score"] = (double)o.Result.Score,
                ["reason"] = o.Result.Reason.ToWire(), ["tier"] = o.Tier.ToWire(), ["freeSlots"] = (double)o.FreeSlots,
                ["blocked"] = o.Blocked?.ToWire(),
            }).ToList();

        private static object UnlockJson(GameState s) =>
            Zones.HabitatUnlockStatus(s).Select(u => (object)new Dictionary<string, object>
            {
                ["habitatId"] = u.HabitatId, ["unlocked"] = u.Unlocked, ["requires"] = u.Requires, ["eligible"] = u.Eligible,
            }).ToList();

        [TestCaseSource(nameof(Scenarios))]
        public void World_command_replay_matches_TypeScript_step_by_step(string name)
        {
            var sc = A(GoldenFile.World["scenarios"]).Select(O).Single(x => (string)x["name"] == name);
            GameState state = StateJson.FromJson(O(sc["start"]));
            Assert.That(GameStateValidator.Validate(state).IsValid, Is.True, GameStateValidator.Validate(state).ToString());
            Assert.That(MiniJson.Diff(sc["start"], StateJson.ToGoldenSubset(state, fullIds: true, keepProfiles: true)), Is.Null, "start round-trip");
            var clock = new FixedClock(0);
            var steps = A(sc["steps"]);
            int probes = 0;
            long rewards = 0;
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
                rewards += r.Events.OfType<AchievementUnlocked>().Sum(e => e.RewardGoldMicro);
                Assert.That(MiniJson.Diff(st["events"], r.Events.Where(e => WorldEvents.Contains(e.Type)).Select(e => (object)StateJson.Event(e)).ToList()), Is.Null, at + " events");
                if (st["state"] != null)
                {
                    Assert.That(MiniJson.Diff(st["state"], StateJson.ToGoldenSubset(r.State, fullIds: false, keepProfiles: true, rewardsMicro: rewards)), Is.Null, at + " state");
                    Assert.That(GameStateValidator.Validate(r.State).IsValid, Is.True, at + " invalid: " + GameStateValidator.Validate(r.State));
                }
                if (st.TryGetValue("probe", out object pv))
                {
                    var p = O(pv);
                    DragonState d = r.State.FindDragon((string)p["dragonUid"]);
                    Assert.That(MiniJson.Diff(p["placement"], PlacementJson(r.State, d)), Is.Null, at + " placementOptions");
                    Assert.That(MiniJson.Diff(p["unlockStatus"], UnlockJson(r.State)), Is.Null, at + " habitatUnlockStatus");
                    Assert.That(Zones.CurrentProductionBudget(r.State), Is.EqualTo((int)(double)p["budget"]), at + " budget");
                    foreach (var kv in O(p["occupancy"])) Assert.That(Zones.ZoneOccupancy(r.State, kv.Key), Is.EqualTo((int)(double)kv.Value), at + " occupancy " + kv.Key);
                    var sv = O(p["survey"]);
                    Assert.That(Survey.RequirementMet(r.State, "DISC_EMBERLING_THERMAL"), Is.EqualTo((bool)sv["thermal"]), at + " thermal");
                    Assert.That(Survey.RequirementMet(r.State, "DISC_AQUATIC_HABITAT"), Is.EqualTo((bool)sv["aquatic"]), at + " aquatic");
                    probes++;
                }
                state = r.State;
            }
            Assert.That(MiniJson.Diff(sc["final"], StateJson.ToGoldenSubset(state, fullIds: true, keepProfiles: true, rewardsMicro: rewards)), Is.Null, name + " final");
            Assert.That(probes, Is.GreaterThan(5));
        }
    }
}
