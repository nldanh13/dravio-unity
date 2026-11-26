using System;
using System.Collections.Generic;
using System.Linq;
using Dravio.Domain.Biology;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Economy;
using Dravio.Domain.Tests.Support;
using Dravio.Domain.Text;
using NUnit.Framework;

namespace Dravio.Domain.Tests
{
    /// <summary>
    /// Đối chiếu từng giá trị với golden vectors sinh từ engine TypeScript gốc @ b33ec7a
    /// (Golden/ts-golden-b33ec7a.json; cách sinh lại: README "Golden vectors").
    /// </summary>
    public sealed class GoldenParityTests
    {
        private static Dictionary<string, object> O(object v) => (Dictionary<string, object>)v;
        private static long L(object v) => checked((long)(double)v);

        [Test]
        public void Golden_file_targets_the_reference_commit()
        {
            Assert.That(GoldenFile.Root["source"], Is.EqualTo("nldanh13/dravio@" + ContentInfo.ReferenceCommit));
        }

        [Test]
        public void Mulberry32_matches_TypeScript_for_64_values_per_seed()
        {
            foreach (var entry in GoldenFile.List("rng").Select(O))
            {
                uint seed = (uint)(double)entry["seed"];
                IRng r = Rng.Create(seed);
                var expected = (List<object>)entry["values"];
                for (int i = 0; i < expected.Count; i++)
                    Assert.That(r.Next(), Is.EqualTo((double)expected[i]), "seed " + seed + " #" + i);
            }
        }

        [Test]
        public void HashSeed_matches_TypeScript()
        {
            foreach (var entry in GoldenFile.List("hashes").Select(O))
            {
                string[] parts = ((List<object>)entry["parts"]).Cast<string>().ToArray();
                Assert.That(Rng.HashSeed(parts), Is.EqualTo((uint)(double)entry["hash"]), string.Join("|", parts));
            }
        }

        [Test]
        public void RandomInt_PickUniform_PickWeighted_consume_rng_like_TypeScript()
        {
            var h = O(GoldenFile.Root["helpers"]);
            IRng r = Rng.Create((uint)(double)h["seed"]);
            var ints = (List<object>)h["ints"];
            for (int i = 0; i < ints.Count; i++) Assert.That(r.RandomInt(i % 7 + 1), Is.EqualTo((int)(double)ints[i]));
            var uniform = (List<object>)h["uniform"];
            string[] abcd = { "a", "b", "c", "d" };
            foreach (var u in uniform) Assert.That(r.PickUniform(abcd), Is.EqualTo(u));
            var entries = new List<KeyValuePair<string, double>>
            {
                new KeyValuePair<string, double>("n", 0.85), new KeyValuePair<string, double>("c", 0.11), new KeyValuePair<string, double>("g", 0.035),
                new KeyValuePair<string, double>("p", 0.005), new KeyValuePair<string, double>("z", 0),
            };
            foreach (var w in (List<object>)h["weighted"]) Assert.That(r.PickWeighted(entries), Is.EqualTo(w));
        }

        [Test]
        public void Founder_genotype_matches_TypeScript()
        {
            foreach (var entry in GoldenFile.List("founders").Select(O))
            {
                Genotype g = Genetics.FounderGenotype((string)entry["uid"], ContentInfo.ContentVersion, LociCatalog.LocusIds);
                var exp = O(entry["genotype"]);
                Assert.That(g.Count, Is.EqualTo(exp.Count));
                foreach (var kv in exp)
                {
                    Assert.That(g.TryGet(kv.Key, out AllelePair pair), Is.True);
                    var e = (List<object>)kv.Value;
                    Assert.That(pair, Is.EqualTo(new AllelePair((string)e[0], (string)e[1])), (string)entry["uid"] + " " + kv.Key);
                }
            }
        }

        [Test]
        public void Initial_state_is_identical_to_TypeScript_for_six_seeds()
        {
            foreach (var entry in GoldenFile.List("initialStates").Select(O))
            {
                GameState s = InitialStateFactory.Create((string)entry["saveId"], L(entry["nowMs"]), Rng.Create((uint)(double)entry["seed"]));
                // State khởi tạo trùng TS ngoài schemaVersion: C# ghi v5 (U2.5, tổ ấp rỗng nên không có khóa breedingNest).
                var expected = new Dictionary<string, object>(O(entry["state"])) { ["schemaVersion"] = (double)ContentInfo.SchemaVersion };
                string diff = MiniJson.Diff(expected, StateJson.ToJson(s));
                Assert.That(diff, Is.Null, "seed " + entry["seed"]);
                Assert.That(GameStateValidator.Validate(s).IsValid, Is.True);
            }
        }

        [Test]
        public void Rates_and_sell_prices_match_for_all_species_stages_mutations()
        {
            var economy = O(GoldenFile.Root["economy"]);
            int n = 0;
            foreach (var row in ((List<object>)economy["rates"]).Select(O))
            {
                DragonSpecies sp = SpeciesCatalog.Get((string)row["speciesId"]);
                WireNames.TryParse((string)row["stage"], out GrowthStage st);
                WireNames.TryParse((string)row["mutation"], out MutationType m);
                Assert.That(EconomyRates.RateMicroGPerSec(sp, st, m), Is.EqualTo(L(row["rate"])), sp.Id + " " + st + " " + m);
                Assert.That(EconomyRates.SellPriceG(sp, st, m), Is.EqualTo(L(row["sell"])), sp.Id + " " + st + " " + m);
                n++;
            }
            Assert.That(n, Is.EqualTo(16 * 3 * 4));
        }

        [Test]
        public void Upgrade_costs_budgets_and_offline_helper_match()
        {
            var economy = O(GoldenFile.Root["economy"]);
            foreach (var row in ((List<object>)economy["upgradeCosts"]).Select(O))
            {
                UpgradeDef def = EconomyTables.Upgrades.Single(u => u.Id == (string)row["id"]);
                long? expected = row["cost"] == null ? (long?)null : L(row["cost"]);
                Assert.That(EconomyRates.UpgradeCostG(def, (int)(double)row["level"]), Is.EqualTo(expected), def.Id + " L" + row["level"]);
            }
            foreach (var row in ((List<object>)economy["budgets"]).Select(O))
                Assert.That(EconomyRates.ProductionBudget((int)(double)row["level"]), Is.EqualTo((int)(double)row["budget"]));
            foreach (var row in ((List<object>)economy["offline"]).Select(O))
            {
                var r = EconomyRates.OfflineGoldMicro(L(row["rate"]), L(row["ms"]), (double)row["rem"]);
                Assert.That(r.goldMicro, Is.EqualTo(L(row["goldMicro"])));
                Assert.That(r.remainder, Is.EqualTo((double)row["remainder"]));
            }
        }

        [Test]
        public void Suitability_matches_for_every_species_and_habitat()
        {
            int n = 0;
            foreach (var row in GoldenFile.List("suitability").Select(O))
            {
                HabitatResult r = Suitability.ForSpecies((string)row["speciesId"], (string)row["habitatId"]);
                string label = row["speciesId"] + "@" + row["habitatId"];
                Assert.That(r.Allowed, Is.EqualTo((bool)row["allowed"]), label);
                Assert.That(r.Score, Is.EqualTo((int)(double)row["score"]), label);
                Assert.That(r.Reason.ToWire(), Is.EqualTo(row["reason"]), label);
                n++;
            }
            Assert.That(n, Is.EqualTo(16 * 7));
        }

        [Test]
        public void Nickname_normalization_and_grapheme_counts_match_Intl_Segmenter()
        {
            foreach (var row in GoldenFile.List("nicknames").Select(O))
            {
                string raw = (string)row["raw"];
                string label = string.Join(" ", raw.Select(c => ((int)c).ToString("X4")));
                Assert.That(GraphemeCounter.Count(JsCompat.Trim(NicknameRules.NormalizeNfc(raw))), Is.EqualTo((int)(double)row["graphemes"]), "graphemes " + label);
                var exp = O(row["result"]);
                NicknameResult r = NicknameRules.Normalize(raw);
                Assert.That(r.Ok, Is.EqualTo((bool)exp["ok"]), label);
                if (r.Ok) Assert.That(r.Value, Is.EqualTo(exp["value"]), label);
                else Assert.That(r.ErrorCode.ToWire(), Is.EqualTo(exp["code"]), label);
            }
        }

        private static readonly HashSet<string> U21Events = new HashSet<string> { "timeAdvanced", "stageChanged", "offlineReceiptCreated", "foodSpawned", "foodExpired", "foodConsumed", "bondChanged", "dragonPetted", "dragonRenamed" };

        private static IEnumerable<TestCaseData> Scenarios()
        {
            foreach (var sc in GoldenFile.List("scenarios").Select(O))
                yield return new TestCaseData((string)sc["name"]).SetName("Scenario_" + (string)sc["name"]);
        }

        /// <summary>
        /// Phát lại từng kịch bản: cùng state bắt đầu, cùng lệnh, cùng giờ → cùng ok/lỗi, cùng event (thứ tự và giá trị),
        /// cùng state sau mỗi bước (trừ tutorial/achievements chưa port; vàng đã trừ thưởng thành tựu của TS).
        /// </summary>
        [TestCaseSource(nameof(Scenarios))]
        public void Command_replay_matches_TypeScript_step_by_step(string name)
        {
            var sc = GoldenFile.List("scenarios").Select(O).Single(x => (string)x["name"] == name);
            GameState state = StateJson.FromJson(O(sc["start"]));
            Assert.That(GameStateValidator.Validate(state).IsValid, Is.True, GameStateValidator.Validate(state).ToString());
            Assert.That(MiniJson.Diff(sc["start"], StateJson.ToGoldenSubset(state, fullIds: true)), Is.Null, "start state round-trip");
            var clock = new FixedClock(0);
            var steps = (List<object>)sc["steps"];
            long rewards = 0;
            for (int i = 0; i < steps.Count; i++)
            {
                var st = O(steps[i]);
                string at = name + " step " + i + " " + MiniJson.Show(O(st["command"])["type"]);
                clock.Set(L(st["nowMs"]));
                GameCommand cmd = StateJson.Command(O(st["command"]));
                DispatchResult r = CommandDispatcher.Dispatch(state, cmd, clock);
                Assert.That(r.Ok, Is.EqualTo((bool)st["ok"]), at + " ok (error " + r.Error + ")");
                Assert.That(r.Duplicate, Is.EqualTo((bool)st["duplicate"]), at + " duplicate");
                if (!r.Ok) Assert.That(r.Error.WireCode, Is.EqualTo(st["errorCode"]), at + " error code");
                Assert.That(ReferenceEquals(r.State, state), Is.EqualTo((bool)st["sameStateObject"]), at + " same object");
                // Golden U2.1 chỉ ghi event của U2.1 và vàng đã trừ thưởng thành tựu; từ U2.4 C# cũng phát thành tựu/hướng dẫn
                // nên so cùng tập event và trừ thưởng giống hệt (thành tựu/hướng dẫn được so đầy đủ ở golden U2.4).
                rewards += r.Events.OfType<AchievementUnlocked>().Sum(e => e.RewardGoldMicro);
                var events = new List<object>(r.Events.Where(e => U21Events.Contains(e.Type)).Select(e => (object)StateJson.Event(e)));
                Assert.That(MiniJson.Diff(st["events"], events), Is.Null, at + " events");
                if (st["state"] != null)
                {
                    Assert.That(MiniJson.Diff(st["state"], StateJson.ToGoldenSubset(r.State, fullIds: false, rewardsMicro: rewards)), Is.Null, at + " state");
                    var v = GameStateValidator.Validate(r.State);
                    Assert.That(v.IsValid, Is.True, at + " invalid: " + v);
                }
                state = r.State;
            }
            Assert.That(MiniJson.Diff(sc["final"], StateJson.ToGoldenSubset(state, fullIds: true, rewardsMicro: rewards)), Is.Null, name + " final");
        }
    }
}
