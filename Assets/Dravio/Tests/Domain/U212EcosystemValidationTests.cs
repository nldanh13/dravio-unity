using System;
using System.Collections.Generic;
using System.Linq;
using Dravio.Domain.Behavior;
using Dravio.Domain.Biology;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Economy;
using Dravio.Domain.Progression;
using Dravio.Domain.Queries;
using Dravio.Domain.Tests.Simulation;
using Dravio.Domain.Tests.Support;
using Dravio.Domain.World;
using NUnit.Framework;
using static Dravio.Domain.Tests.Support.Fixtures;

namespace Dravio.Domain.Tests
{
    /// <summary>
    /// U2.12 (ADR 0207) — Economy &amp; Ecosystem Validation. Không đổi luật đã duyệt, không bật luật thử nghiệm.
    /// - Kiểm toán tĩnh: từ save mới, mọi nhiệm vụ / thế giới đạt được (không vòng lặp điều kiện gây bế tắc), trừ phần chờ duyệt.
    /// - Hướng dẫn hành trình (<see cref="ProgressionQueries.Journey"/>): chỉ đọc, chỉ đúng điều kiện engine kiểm.
    /// - Hành trình mô phỏng bằng lệnh thật: người chơi theo hướng dẫn mở đủ ba thế giới, hoàn thành mọi nhiệm vụ đã duyệt.
    /// - Bất biến kinh tế: không nguồn vàng mới, vàng bảo toàn, vật liệu không âm.
    /// </summary>
    public sealed class U212EcosystemValidationTests
    {
        private static readonly Schedule ThreeSessions = EconomySimulationTests.Schedules[1];

        // ================================================================== hiệu năng (cùng kết quả)

        [Test]
        public void Fast_familiarity_matches_full_relation_on_a_long_simulated_save()
        {
            // SocialEcology.Familiarity (dùng cho yêu cầu nhiệm vụ relationPairs, O(n²)) phải trùng Relation(...).Familiarity trên mọi cặp.
            var sim = new EconomySimulator(Policy.Guided, ThreeSessions, 2, EconomyVariant.Live);
            sim.Run(new double[] { 24 * 20 });
            GameState s = sim.State;
            var kept = s.Dragons.Where(d => d.Location.Kind != LocationKind.Released).ToList();
            Assert.That(kept.Count, Is.GreaterThan(10));
            int pairs = 0, kin = 0, timed = 0;
            for (int i = 0; i < kept.Count; i++)
                for (int j = i + 1; j < kept.Count; j++)
                {
                    SocialRelation full = SocialEcology.Relation(s, kept[i], kept[j]);
                    Assert.That(SocialEcology.Familiarity(s, kept[i], kept[j]), Is.EqualTo(full.Familiarity), kept[i].Uid + "/" + kept[j].Uid);
                    pairs++;
                    if (full.Sources.Contains(FamiliaritySource.ParentChild) || full.Sources.Contains(FamiliaritySource.Siblings)) kin++;
                    if (full.SharedMs > 0) timed++;
                }
            Assert.That(pairs, Is.GreaterThan(50));
            Assert.That(timed, Is.GreaterThan(0), "có cặp có thời gian sống chung");
        }

        // ================================================================== kiểm toán tĩnh

        [Test]
        public void Audit_proves_every_world_and_every_approved_quest_is_reachable_from_a_new_save()
        {
            ProgressionAuditReport r = ProgressionQueries.AuditCatalog();
            Assert.That(r.CatalogErrors, Is.Empty);
            Assert.That(r.UnreachableZones, Is.Empty, "cả ba thế giới mở được");
            // Chỉ một nhiệm vụ không bao giờ đạt được, và lý do là luật CHƯA DUYỆT (bảng thưởng sky_cloud), không phải vòng lặp điều kiện.
            Assert.That(r.UnreachableQuests.Keys, Is.EqualTo(new[] { "Q_EXPEDITION_CLOUD" }));
            Assert.That(r.UnreachableQuests["Q_EXPEDITION_CLOUD"], Is.EqualTo("routeAvailable:route-rewards-undefined"));
            Assert.That(r.Steps.Count(st => st.Kind == "quest"), Is.EqualTo(QuestProposals.All.Count - 1));
        }

        [Test]
        public void Audit_order_follows_the_canonical_gates_and_quest_prerequisites()
        {
            ProgressionAuditReport r = ProgressionAudit.Run();
            Assert.That(r.WaveOf("ocean"), Is.GreaterThan(r.WaveOf("QUEST_AQUATIC_EXPLORATION")).Or.EqualTo(r.WaveOf("QUEST_AQUATIC_EXPLORATION")));
            Assert.That(r.WaveOf("QUEST_SKY_EXPLORATION"), Is.GreaterThan(r.WaveOf("ocean")), "cổng Thiên Giới chỉ đạt được sau khi Thủy Vực mở");
            Assert.That(r.WaveOf("sky"), Is.GreaterThan(r.WaveOf("QUEST_SKY_EXPLORATION")).Or.EqualTo(r.WaveOf("QUEST_SKY_EXPLORATION")));
            foreach (QuestProposalDef q in QuestProposals.All)
            {
                int w = r.WaveOf(q.Id);
                if (w < 0) continue;
                foreach (string p in q.Prerequisites) Assert.That(r.WaveOf(p), Is.LessThan(w), q.Id + " sau tiền đề " + p);
                foreach (string z in q.RequiresZones) if (z != "origin") Assert.That(r.WaveOf(z), Is.LessThan(w), q.Id + " sau khi mở " + z);
            }
            // Bốn nhiệm vụ đầu đạt được ngay với nội dung Khởi Nguyên.
            Assert.That(r.Steps.Where(st => st.Wave == 0).Select(st => st.Id), Is.EquivalentTo(new[] { "Q_ORIGIN_FIELD_NOTES", "Q_ORIGIN_FOREST_SURVEY", "Q_CONSERVE_AT_HOME", "Q_SOCIAL_FIRST_FRIENDS" }));
        }

        [Test]
        public void Audit_blocker_names_the_missing_world_when_only_origin_is_open()
        {
            var origin = new HashSet<string>(StringComparer.Ordinal) { "origin" };
            HashSet<string> owned = ProgressionAudit.Obtainable(origin);
            Assert.That(owned.Contains("droplet") && owned.Contains("zephyr"), Is.True, "Droplet (bơi) và Zephyr (bay) có trong gói Starter: cổng không cần trứng thế giới");
            Assert.That(ProgressionAudit.Blocker(QuestProposals.Get("QUEST_SKY_EXPLORATION"), origin, new HashSet<string>(), owned), Is.EqualTo("zone:ocean"));
            Assert.That(ProgressionAudit.Blocker(QuestProposals.Get("QUEST_AQUATIC_EXPLORATION"), origin, new HashSet<string>(), owned), Is.EqualTo("quest:Q_ORIGIN_FIELD_NOTES"));
            Assert.That(ProgressionAudit.Blocker(QuestProposals.Get("QUEST_AQUATIC_EXPLORATION"), origin, new HashSet<string> { "Q_ORIGIN_FIELD_NOTES" }, owned), Is.Null);
        }

        /// <summary>
        /// Phát hiện U2.12 (luật có từ trước, KHÔNG sửa — chờ chủ dự án quyết định): Aurorawing (Nước + Gió, Epic, nhóm V) không có trong gói nào và nhóm V
        /// không loài nào mang nguyên tố Nước, nên không lai ra được. Hệ quả: ACH_CODEX_16, title.dragon_scholar, title.lifecycle_master, title.grand_ethologist
        /// không bao giờ đạt được. Test này ghi lại hiện trạng; khi chủ dự án chọn cách mở (xem ADR 0207), cập nhật test.
        /// </summary>
        [Test]
        public void Aurorawing_is_the_only_species_that_can_never_be_obtained()
        {
            ProgressionAuditReport r = ProgressionAudit.Run();
            string[] never = SpeciesCatalog.All.Select(sp => sp.Id).Where(id => !r.ObtainableSpecies.Contains(id)).ToArray();
            Assert.That(never, Is.EqualTo(new[] { "aurorawing" }));
            DragonSpecies aw = SpeciesCatalog.Get("aurorawing");
            Assert.That(aw.PackEligible, Is.False);
            Assert.That(SpeciesCatalog.All.Where(sp => sp.Group == aw.Group).Any(sp => sp.Id != aw.Id && sp.Elements.Contains(ElementType.Water)), Is.False);
            Assert.That(SpeciesCatalog.Get("amberhorn").PackEligible, Is.False, "Amberhorn cũng ngoài gói nhưng lai được (nhóm T, Lửa + Thiên nhiên)");
            Assert.That(r.ObtainableSpecies, Does.Contain("amberhorn"));
        }

        [Test]
        public void Every_observable_behaviour_of_every_species_can_be_seen_in_a_habitat_it_may_live_in()
        {
            // Ethology cần mọi tập tính quan sát được. Tập tính phụ thuộc nơi ở (tắm nắng, bơi, bay, đào) phải có ít nhất một microhabitat hợp lệ
            // (suitability cho phép) — nếu không, loài đó không bao giờ đạt Ethology.
            foreach (DragonSpecies sp in SpeciesCatalog.All)
            {
                SpeciesBiology bio = sp.Biology;
                var homes = bio.HabitatRules.Where(h => Suitability.ForSpecies(sp.Id, h).Allowed).Select(h => { HabitatCatalog.TryGet(h, out HabitatProfile p); return p; }).ToList();
                Assert.That(homes, Is.Not.Empty, sp.Id);
                foreach (DragonBehavior b in Research.ObservableBehaviors(sp.Id))
                    Assert.That(homes.Any(h => BehaviorCatalog.Allowed(bio, h, b)), Is.True, sp.Id + " không có nơi để " + b);
            }
        }

        // ================================================================== hướng dẫn hành trình

        [Test]
        public void New_save_journey_points_to_the_tutorial_the_origin_habitats_and_field_notes()
        {
            (GameState s, _) = NewGame();
            JourneyDto j = ProgressionQueries.Journey(s);
            Assert.That(j.NextZoneId, Is.EqualTo("ocean"));
            Assert.That(j.UnlockedZones, Is.EqualTo(new[] { "origin" }));
            JourneyStep first = j.Steps[0];
            Assert.That((first.Code, first.Target), Is.EqualTo((JourneyStepCode.FinishTutorial, "TUT_01")));
            JourneyStep rocks = j.Steps.Single(st => st.Target == "origin_warm_rocks");
            Assert.That((rocks.Code, rocks.Ready, rocks.Detail), Is.EqualTo((JourneyStepCode.SurveyHabitat, false, "survey-requirement-missing")), "cần xem hồ sơ Emberling trước");
            JourneyStep pond = j.Steps.Single(st => st.Target == "origin_pondbank");
            Assert.That((pond.Code, pond.Ready), Is.EqualTo((JourneyStepCode.SurveyHabitat, true)));
            JourneyStep notes = j.Steps.First(st => st.QuestId == "Q_ORIGIN_FIELD_NOTES");
            Assert.That((notes.Code, notes.Requirement, notes.Current, notes.Required), Is.EqualTo((JourneyStepCode.QuestRequirement, QuestRequirementKind.ResearchTier, 0, 2)));
            Assert.That(notes.Candidates, Is.EqualTo(new[] { "emberling" }), "xem hồ sơ loài đang nuôi");
            // Tiền đề trước nhiệm vụ cổng, mở thế giới cuối cùng.
            int notesAt = j.Steps.ToList().FindIndex(st => st.QuestId == "Q_ORIGIN_FIELD_NOTES");
            int gateAt = j.Steps.ToList().FindIndex(st => st.QuestId == "QUEST_AQUATIC_EXPLORATION");
            Assert.That(notesAt, Is.LessThan(gateAt));
            JourneyStep last = j.Steps.Last();
            Assert.That((last.Code, last.Target, last.Ready), Is.EqualTo((JourneyStepCode.UnlockZone, "ocean", false)));
            Assert.That(last.Detail, Is.EqualTo(string.Join(",", WorldExpansion.MissingRequirements(s, "ocean"))));
            Assert.That(j.Steps.Select(st => st.Key), Has.All.StartWith("journey.step."));
        }

        [Test]
        public void Journey_never_suggests_gold_and_never_changes_the_state()
        {
            var sim = new EconomySimulator(Policy.Guided, ThreeSessions, 2, EconomyVariant.Live);
            sim.Run(new double[] { 24 * 5 });
            GameState s = sim.State;
            var before = StateJson.ToJson(s);
            JourneyDto a = ProgressionQueries.Journey(s), b = ProgressionQueries.Journey(s);
            Assert.That(MiniJson.Diff(before, StateJson.ToJson(s)), Is.Null);
            Assert.That(a.Steps.Select(Describe), Is.EqualTo(b.Steps.Select(Describe)), "tất định");
            foreach (JourneyStep st in a.Steps)
            {
                Assert.That((st.Target ?? "") + (st.Detail ?? "") + string.Join(",", st.Candidates), Does.Not.Contain("gold"));
                if (st.Code == JourneyStepCode.ClaimQuest) Assert.That(QuestSystem.Check(s, st.QuestId, out _), Is.Null, "claim-quest khớp đúng lệnh completeQuest");
            }
            foreach (string q in a.SideQuestsReady) Assert.That(QuestSystem.Check(s, q, out _), Is.Null);
        }

        [Test]
        public void Skipped_tutorial_is_flagged_and_resuming_it_completes_it_again_so_the_ocean_gate_never_deadlocks()
        {
            var sim = new EconomySimulator(Policy.Guided, ThreeSessions, 1, EconomyVariant.Live);
            sim.Run(new double[] { 24 * 3 });
            GameState s = sim.State;
            Assume.That(s.FindZone("ocean").Unlocked, Is.False);
            Assume.That(s.Tutorial.Status, Is.EqualTo(TutorialStatus.Completed));
            long t = s.Clock.LastProcessedAtMs + 60_000;
            // "Xem lại" rồi "bỏ qua": hướng dẫn không còn Completed → Thủy Vực báo thiếu tutorial-completed.
            s = Ok(s, new SetTutorialStatusCommand(Rid(), TutorialStatus.Active), new FixedClock(t)).State;
            Assert.That((s.Tutorial.Status, s.Tutorial.Completed.Count), Is.EqualTo((TutorialStatus.Active, 0)), "xem lại xóa các bước; TUT_01 cần 15 s nên chưa ghi lại ngay");
            s = Ok(s, new SetTutorialStatusCommand(Rid(), TutorialStatus.Skipped), new FixedClock(t)).State;
            Assert.That(WorldExpansion.MissingRequirements(s, "ocean"), Does.Contain("tutorial-completed"));
            JourneyStep resume = ProgressionQueries.Journey(s).Steps[0];
            Assert.That((resume.Code, resume.Ready), Is.EqualTo((JourneyStepCode.ResumeTutorial, true)));
            // Tiếp tục: các bước đã thỏa được ghi lại (TUT_01 cần 15 s quan sát kể từ lúc bắt đầu lại).
            s = Ok(s, new SetTutorialStatusCommand(Rid(), TutorialStatus.Active), new FixedClock(t + 1000)).State;
            if (s.PendingPack != null) s = Ok(s, new ClaimPackCommand(Rid(), MoveTarget.Storage), new FixedClock(t + 2000)).State;
            s = Ok(s, new AdvanceTimeCommand(Rid(), AdvanceMode.Online), new FixedClock(t + 20_000)).State;
            if (s.Tutorial.Status != TutorialStatus.Completed)
            {
                // TUT_02 cần một rồng đã được cho ăn còn trong kho; cho ăn lại nếu rồng đó đã bán.
                DragonState d = s.Dragons.First(x => x.Location.Kind == LocationKind.Zone);
                s = Ok(s, new FeedCommand(Rid(), SpeciesCatalog.GetBiology(d.SpeciesId).DietIds[0], d.Location.ZoneId, d.Location.HabitatId), new FixedClock(t + 21_000)).State;
                s = Ok(s, new ConsumeFoodCommand(Rid(), s.Food.Drops.Last().Id, d.Uid), new FixedClock(t + 22_000)).State;
            }
            Assert.That(s.Tutorial.Status, Is.EqualTo(TutorialStatus.Completed), "hướng dẫn luôn hoàn tất lại được");
            Assert.That(WorldExpansion.MissingRequirements(s, "ocean"), Does.Not.Contain("tutorial-completed"));
        }

        [Test]
        public void Journey_is_empty_once_all_three_worlds_are_open()
        {
            var sim = new EconomySimulator(Policy.Guided, ThreeSessions, 2, EconomyVariant.Live);
            RunResult r = sim.Run(new double[] { 24 * 20 });
            Assume.That(r.ZoneUnlockHours["sky"].HasValue, Is.True);
            JourneyDto j = ProgressionQueries.Journey(sim.State);
            Assert.That(j.NextZoneId, Is.Null);
            Assert.That(j.Steps, Is.Empty);
            Assert.That(j.UnlockedZones, Is.EqualTo(new[] { "origin", "ocean", "sky" }));
        }

        private static string Describe(JourneyStep st) => st.Code + "|" + st.QuestId + "|" + st.Target + "|" + st.Requirement + "|" + st.Current + "/" + st.Required + "|" + string.Join(",", st.Candidates) + "|" + st.Ready;

        // ================================================================== hành trình mô phỏng (lệnh thật)

        [Test]
        public void Guided_player_opens_three_worlds_and_completes_every_approved_quest_through_real_commands()
        {
            var sim = new EconomySimulator(Policy.Guided, ThreeSessions, 2, EconomyVariant.Live);
            RunResult r = sim.Run(new double[] { 24 * 7, 24 * 45 });
            GameState s = sim.State;
            Assert.That(s.Zones.All(z => z.Unlocked), Is.True);
            Assert.That(sim.GuideStepsFollowed, Is.GreaterThan(0), "người chơi thật sự làm theo hướng dẫn");
            string[] approved = QuestProposals.All.Select(q => q.Id).Where(id => id != "Q_EXPEDITION_CLOUD").ToArray();
            Assert.That(r.QuestHours.Keys, Is.EquivalentTo(approved), "mọi nhiệm vụ đã duyệt hoàn thành, Q_EXPEDITION_CLOUD vẫn bị chặn");
            Assert.That(QuestProposals.IsCompleted(s, "Q_EXPEDITION_CLOUD"), Is.False);
            // Thứ tự hợp lệ: tiền đề trước, nhiệm vụ cổng trước khi mở thế giới, thế giới trước nhiệm vụ của nó.
            foreach (QuestProposalDef q in QuestProposals.All.Where(q => r.QuestHours.ContainsKey(q.Id)))
            {
                foreach (string p in q.Prerequisites) Assert.That(r.QuestHours[p], Is.LessThanOrEqualTo(r.QuestHours[q.Id]), q.Id);
                foreach (string z in q.RequiresZones.Where(z => z != "origin")) Assert.That(r.ZoneUnlockHours[z].Value, Is.LessThanOrEqualTo(r.QuestHours[q.Id]), q.Id);
            }
            Assert.That(r.QuestHours["QUEST_AQUATIC_EXPLORATION"], Is.LessThanOrEqualTo(r.ZoneUnlockHours["ocean"].Value));
            Assert.That(r.QuestHours["QUEST_SKY_EXPLORATION"], Is.LessThanOrEqualTo(r.ZoneUnlockHours["sky"].Value));
            Assert.That(s.GrantedRewards, Does.Contain("ZONE_EGG_OCEAN").And.Contain("ZONE_EGG_SKY"));
            Assert.That(s.Player.Inventory.Values.All(v => v >= 0), Is.True);
            Assert.That(GameStateValidator.Validate(s).IsValid, Is.True, GameStateValidator.Validate(s).ToString());
        }

        [Test]
        public void Close_bond_quest_is_completed_through_breeding_a_family_in_one_habitat()
        {
            var sim = new EconomySimulator(Policy.Guided, ThreeSessions, 4, EconomyVariant.Live);
            RunResult r = sim.Run(new double[] { 24 * 30 });
            Assert.That(r.QuestHours.ContainsKey("Q_SOCIAL_CLOSE_BOND"), Is.True);
            Assert.That(r.At[720].ClosePairs, Is.GreaterThan(0));
            Assert.That(r.QuestHours["Q_SOCIAL_FIRST_FRIENDS"], Is.LessThan(r.QuestHours["Q_SOCIAL_CLOSE_BOND"]));
        }

        [Test]
        public void Casual_player_who_follows_the_cheap_journey_steps_reaches_ocean_and_sky()
        {
            var sim = new EconomySimulator(Policy.Casual, ThreeSessions, 4, EconomyVariant.Live);
            RunResult r = sim.Run(new double[] { 24 * 30 });
            Assert.That(r.ZoneUnlockHours["ocean"], Is.Not.Null);
            Assert.That(r.ZoneUnlockHours["sky"], Is.Not.Null);
            Assert.That(r.ZoneUnlockHours["sky"].Value, Is.GreaterThan(r.ZoneUnlockHours["ocean"].Value));
        }

        [Test]
        public void Simulated_journeys_are_deterministic()
        {
            foreach (Policy p in new[] { Policy.Guided, Policy.Casual })
            {
                var a = new EconomySimulator(p, ThreeSessions, 6, EconomyVariant.Live);
                var b = new EconomySimulator(p, ThreeSessions, 6, EconomyVariant.Live);
                a.Run(new double[] { 24 * 4 }); b.Run(new double[] { 24 * 4 });
                Assert.That(MiniJson.Diff(StateJson.ToJson(a.State), StateJson.ToJson(b.State)), Is.Null, p.ToString());
            }
        }

        // ================================================================== bất biến kinh tế

        [Test]
        public void Gold_is_conserved_and_quests_and_milestones_add_no_gold_over_30_days()
        {
            foreach (Policy p in new[] { Policy.Guided, Policy.Casual, Policy.Relaxed })
            {
                var sim = new EconomySimulator(p, ThreeSessions, 3, EconomyVariant.Live);
                RunResult r = sim.Run(new double[] { 24 * 30 });
                Snapshot x = r.At[720];
                long expected = 150 + x.EarnedG + x.SoldG + x.RewardsG - x.SpentPacksG - x.SpentBreedG - x.SpentUpgradesG - x.ProjectsG;
                // Snapshot làm tròn từng khoản xuống G nguyên: sai số tối đa vài G.
                Assert.That(Math.Abs(x.GoldG - expected), Is.LessThanOrEqualTo(8), p + ": vàng = khởi đầu + sản xuất + bán + thành tựu − chi tiêu");
                Assert.That(sim.State.Player.Inventory.Values.All(v => v >= 0), Is.True, p.ToString());
                // Mốc thám hiểm: tối đa 2+3+4+6 = 15 mỗi vật liệu của tuyến (2 vật liệu × 2 tuyến mở được).
                Assert.That(x.MilestoneMaterials, Is.LessThanOrEqualTo(15 * 2 * 2), p.ToString());
            }
        }
        // ================================================================== báo cáo (chỉ khi được yêu cầu)

        private static double Pct(IEnumerable<double> xs, double q)
        {
            var v = xs.OrderBy(x => x).ToList();
            if (v.Count == 0) return double.NaN;
            double idx = q * (v.Count - 1);
            int lo = (int)Math.Floor(idx), hi = (int)Math.Ceiling(idx);
            return v[lo] + (v[hi] - v[lo]) * (idx - lo);
        }

        private static string F(double v) => double.IsNaN(v) ? "—" : Math.Abs(v) >= 1_000_000 ? (v / 1_000_000).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " M"
            : Math.Abs(v) >= 10_000 ? (v / 1000).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " k" : v.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);

        private static double Faucets(Snapshot x) => x.EarnedG + x.SoldG + x.RewardsG;
        private static double Sinks(Snapshot x) => x.SpentPacksG + x.SpentBreedG + x.SpentUpgradesG + x.ProjectsG;

        /// <summary>
        /// Báo cáo U2.12: DRAVIO_U212_SIM_OUT=&lt;thư mục&gt; (mỗi kiểu người chơi một file &lt;policy&gt;.md để chạy song song), DRAVIO_SIM_SEEDS (mặc định 3),
        /// DRAVIO_SIM_POLICIES (danh sách, mặc định tất cả), DRAVIO_SIM_DAYS (mặc định 365). Biến thể live (luật thật v10).
        /// </summary>
        [Test]
        public void Write_u212_ecosystem_report_when_requested()
        {
            string outDir = Environment.GetEnvironmentVariable("DRAVIO_U212_SIM_OUT");
            if (outDir == null) Assert.Ignore("Đặt DRAVIO_U212_SIM_OUT để sinh báo cáo U2.12.");
            int seeds = int.TryParse(Environment.GetEnvironmentVariable("DRAVIO_SIM_SEEDS"), out int n) ? n : 3;
            int days = int.TryParse(Environment.GetEnvironmentVariable("DRAVIO_SIM_DAYS"), out int d) ? d : 365;
            string filter = Environment.GetEnvironmentVariable("DRAVIO_SIM_POLICIES");
            var policies = new[] { Policy.Relaxed, Policy.Casual, Policy.Collector, Policy.Breeder, Policy.Researcher, Policy.Guided, Policy.StarterTrader, Policy.BreedTrader }
                .Where(p => filter == null || filter.Split(',').Contains(p.ToString())).ToArray();
            double[] horizons = { 24 * 7, 24 * 30, 24 * 90, 24.0 * days };
            System.IO.Directory.CreateDirectory(outDir);
            foreach (Policy p in policies)
            {
                var sb = new System.Text.StringBuilder();
                foreach (Schedule sc in EconomySimulationTests.Schedules)
                {
                    var runs = new RunResult[seeds];
                    System.Threading.Tasks.Parallel.For(0, seeds, i => runs[i] = new EconomySimulator(p, sc, i + 1, EconomyVariant.Live).Run(horizons));
                    double last = horizons.Last();
                    double M(double h, Func<Snapshot, double> f, double q = .5) => Pct(runs.Select(r => f(r.At[h])), q);
                    string Zone(string z)
                    {
                        var hs = runs.Select(r => r.ZoneUnlockHours[z]).Where(x => x.HasValue).Select(x => x.Value / 24).ToList();
                        return hs.Count == 0 ? "— (0%)" : $"{F(Pct(hs, .5))} ({hs.Count * 100 / runs.Length}%)";
                    }
                    double perDay = Pct(runs.Select(r => (Faucets(r.At[last]) - Faucets(r.At[24 * 90])) / (days - 90.0)), .5);
                    sb.AppendLine($"E|{p}|{sc.Name}|{F(M(168, x => x.GoldG))}|{F(M(720, x => x.GoldG))}|{F(M(2160, x => x.GoldG))}|{F(M(last, x => x.GoldG, .1))} / {F(M(last, x => x.GoldG))} / {F(M(last, x => x.GoldG, .9))}" +
                        $"|{F(perDay)}|{M(last, x => Sinks(x) / Math.Max(1, Faucets(x))).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}|{F(M(720, x => x.Projects))} / {F(M(2160, x => x.Projects))} / {F(M(last, x => x.Projects))}" +
                        $"|{F(Pct(runs.Select(r => r.At[last].NextProjectCostG / Math.Max(1.0, (Faucets(r.At[last]) - Faucets(r.At[24 * 90])) / (days - 90.0))), .5))}" +
                        $"|{F(M(last, x => x.Wood))} / {F(M(last, x => x.Seed))} / {F(M(last, x => x.Crystal))} / {F(M(last, x => x.Coral))}|{F(M(last, x => x.ExpeditionsClaimed))}|{F(M(last, x => x.MilestoneMaterials))}");
                    sb.AppendLine($"J|{p}|{sc.Name}|{Zone("ocean")}|{Zone("sky")}|{F(M(720, x => x.QuestsCompleted))} / {F(M(2160, x => x.QuestsCompleted))} / {F(M(last, x => x.QuestsCompleted))}|{F(M(last, x => x.TitlesEarned))}|{F(M(last, x => x.Species))}|{F(M(last, x => x.Ethology))}");
                    sb.AppendLine($"S|{p}|{sc.Name}|{F(M(last, x => x.OriginResidents))} / {F(M(last, x => x.OceanResidents))} / {F(M(last, x => x.SkyResidents))}" +
                        $"|{Pct(runs.Select(r => r.At[last].GoodResidents * 100.0 / Math.Max(1, r.At[last].OriginResidents + r.At[last].OceanResidents + r.At[last].SkyResidents)), .5):0}%|{F(M(last, x => x.ResidentSpecies))}" +
                        $"|{F(M(last, x => x.Dragons))}|{F(M(last, x => x.FriendPairs))}|{F(M(last, x => x.ClosePairs))}");
                    foreach (QuestProposalDef q in QuestProposals.All)
                    {
                        var hs = runs.Where(r => r.QuestHours.ContainsKey(q.Id)).Select(r => r.QuestHours[q.Id] / 24).ToList();
                        sb.AppendLine($"Q|{p}|{sc.Name}|{q.Id}|{hs.Count * 100 / runs.Length}%|{F(Pct(hs, .5))}");
                    }
                }
                System.IO.File.WriteAllText(System.IO.Path.Combine(outDir, p + ".txt"), sb.ToString(), new System.Text.UTF8Encoding(false));
            }
        }
    }
}
