using System;
using System.Collections.Generic;
using System.Linq;
using Dravio.Domain.Behavior;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Economy;
using Dravio.Domain.Progression;
using Dravio.Domain.Queries;
using Dravio.Domain.World;
using Dravio.Domain.Tests.Support;
using NUnit.Framework;
using static Dravio.Domain.Tests.Support.Fixtures;

namespace Dravio.Domain.Tests
{
    /// <summary>
    /// U2.11 (ADR 0206): nhiệm vụ thật (completeQuest), điều kiện mở Thủy Vực / Thiên Giới, quan hệ theo thời gian sống chung,
    /// mốc thưởng thám hiểm, danh hiệu, nội dung dự án Sanctuary. Luật chưa duyệt vẫn tắt.
    /// </summary>
    public sealed class U211WorldProgressionTests
    {
        private const long Sec = 1000, Min = 60 * Sec, Hour = 60 * Min, Day = 24 * Hour;
        private static readonly long Day0 = new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

        private static GameState WithDragons(long now, params DragonState[] ds)
        {
            (GameState s, _) = NewGame();
            s = s.DeepClone();
            s.Dragons.Clear();
            s.ProductionDragonIds.Clear();
            s.Dragons.AddRange(ds);
            foreach (ZoneState z in s.Zones) if (z.Id == "origin") z.UnlockedHabitatIds = Zones.HabitatsOfZone("origin");
            foreach (string disc in ProgressCatalog.DiscoveryIds) if (!s.Discoveries.Contains(disc)) s.Discoveries.Add(disc);
            s.Discoveries.Sort(StringComparer.Ordinal);
            Codex.Observe(s, null);
            s.Tutorial.Status = TutorialStatus.Completed;
            s.Tutorial.Completed = new List<string>(ProgressCatalog.TutorialStepIds);
            s.Clock = new ClockState(now, now);
            return s;
        }

        private static DragonState At(string uid, string species, string habitatId, Personality p = Personality.Calm) =>
            Adult(uid, species, d => { d.Location = DragonLocation.Zone(HabitatCatalog.ZoneOf(habitatId), habitatId); d.Personality = p; });

        private static DayPhase PhaseAt(long now) => DayCycle.PhaseOfMinute((int)((now / Min) % DayCycle.MinutesPerDay));

        private static void ObserveAt(GameState s, long t)
        {
            foreach (BehaviorDecision d in BehaviorEngine.DecideAll(s, t, PhaseAt(t))) s.Research.Observe(s, d, t, ResearchPacing.Production);
        }

        /// <summary>
        /// Người chơi nghiên cứu: quan sát mỗi 3 phút trong [from, to) và mỗi ngày lúc 12:00 cho một loài ăn (lệnh feed + consumeFood thật, xoay vòng theo loài).
        /// Hồi quan sát 30 s dài hơn thời gian hiển thị Eat (5 s), nên ngày chẵn quan sát lúc tìm ăn (SeekFood), ngày lẻ lúc ăn (Eat) — như mô phỏng.
        /// </summary>
        private static GameState Study(GameState s, long from, long to, long stepMs = 3 * Min)
        {
            int meal = 0;
            for (long day = from; day < to; day += Day)
            {
                long end = Math.Min(to, day + Day), noon = day + 12 * Hour;
                for (long t = day; t < end; t += stepMs)
                {
                    if (t <= noon && noon < t + stepMs && noon < end) { s = Meal(s, noon, meal++); continue; }
                    ObserveAt(s, t);
                }
            }
            return s;
        }

        private static GameState Meal(GameState s, long t, int n)
        {
            List<string> species = s.Dragons.Where(d => d.Location.Kind == LocationKind.Zone).Select(d => d.SpeciesId).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
            if (species.Count == 0) return s;
            string sp = species[(n / 2) % species.Count];
            DragonState eater = s.Dragons.First(d => d.SpeciesId == sp && d.Location.Kind == LocationKind.Zone);
            DietId diet = SpeciesCatalog.GetBiology(eater.SpeciesId).DietIds[0];
            DispatchResult fr = CommandDispatcher.Dispatch(s, new FeedCommand(Rid(), diet, eater.Location.ZoneId, eater.Location.HabitatId), new FixedClock(t));
            if (!fr.Ok) return s;
            s = fr.State;
            bool seek = n % 2 == 0;
            if (seek) ObserveAt(s, t);
            FoodDrop drop = s.Food.Drops.Last();
            DispatchResult cr = CommandDispatcher.Dispatch(s, new ConsumeFoodCommand(Rid(), drop.Id, eater.Uid), new FixedClock(t + Sec));
            if (cr.Ok) { s = cr.State; if (!seek) ObserveAt(s, t + 2 * Sec); }
            return s;
        }

        private static void ViewProfiles(GameState s)
        {
            foreach (string sp in s.Dragons.Select(d => d.SpeciesId).Distinct()) if (!s.Tutorial.ProfilesViewed.Contains(sp)) s.Tutorial.ProfilesViewed.Add(sp);
            s.Tutorial.ProfilesViewed.Sort(StringComparer.Ordinal);
        }

        private static long GoldFromTime(DispatchResult r) => r.Events.OfType<TimeAdvanced>().Sum(e => e.GoldMicro) + r.Events.OfType<AchievementUnlocked>().Sum(e => e.RewardGoldMicro);

        // ================================================================== catalog

        [Test]
        public void Quest_catalog_is_consistent_rewards_are_unique_and_never_gold()
        {
            Assert.That(QuestProposals.ValidateCatalog(), Is.Empty);
            Assert.That(QuestProposals.All.Count, Is.EqualTo(17), "11 nhiệm vụ U2.10 + 6 nhiệm vụ U2.11");
            Assert.That(QuestProposals.All.Select(q => q.RewardId).Distinct().Count(), Is.EqualTo(QuestProposals.All.Count));
            foreach (QuestProposalDef q in QuestProposals.All)
            {
                Assert.That(q.RewardId, Does.Match("^(note|blueprint|title|zone)\\."), q.Id);
                Assert.That(q.RewardId.Contains("gold"), Is.False, q.Id);
            }
            Assert.That(QuestProposals.All.Where(q => q.IsCanonicalGate).Select(q => q.RewardId), Is.EqualTo(new[] { "zone.ocean", "zone.sky" }));
            Assert.That(QuestRules.Production.Enabled, Is.True, "U2.11: nhiệm vụ chạy thật");
            // Mọi tiền đề nằm trong catalog, và không nhiệm vụ nào cần thế giới mà chính nó (gián tiếp) mở.
            foreach (QuestProposalDef q in QuestProposals.All) foreach (string p in q.Prerequisites) Assert.That(QuestProposals.Get(p), Is.Not.Null, q.Id);
        }

        [Test]
        public void New_quest_is_shown_but_nothing_is_ready_and_the_world_gates_stay_locked()
        {
            (GameState s, _) = NewGame();
            QuestLogDto log = ProgressionQueries.QuestLog(s);
            Assert.That(log.ReadyQuestIds, Is.Empty);
            Assert.That(log.Quests.Single(q => q.QuestId == "QUEST_AQUATIC_EXPLORATION").LockedBy, Is.EqualTo(new[] { "quest:Q_ORIGIN_FIELD_NOTES" }));
            Assert.That(log.Quests.Single(q => q.QuestId == "QUEST_SKY_EXPLORATION").LockedBy, Does.Contain("zone:ocean"));
            Assert.That(log.ZoneUnlockMissing["ocean"], Is.EqualTo(new[] { "tutorial-completed", "origin-habitats", "quest:QUEST_AQUATIC_EXPLORATION" }));
            Assert.That(log.ZoneUnlockMissing["sky"], Is.EqualTo(new[] { "zone:ocean", "quest:QUEST_SKY_EXPLORATION" }));
            Assert.That(log.Rewards.CodexNotes.Count + log.Rewards.DecorBlueprints.Count + log.Rewards.Titles.Count, Is.Zero);
        }

        // ================================================================== completeQuest

        [Test]
        public void CompleteQuest_rejects_in_order_unknown_completed_locked_not_ready()
        {
            GameState s = WithDragons(Day0, At("e", "emberling", "origin_meadow"));
            var clock = new FixedClock(Day0 + Min);
            Fail(s, new CompleteQuestCommand(Rid(), "Q_NOPE"), clock, DomainErrorCode.UnknownQuest);
            Fail(s, new CompleteQuestCommand(Rid(), ""), clock, DomainErrorCode.InvalidCommand);
            DispatchResult locked = Fail(s, new CompleteQuestCommand(Rid(), "Q_RESEARCH_NIGHT_WATCH"), clock, DomainErrorCode.QuestLocked);
            Assert.That(locked.Error.Detail, Is.EqualTo("quest:Q_ORIGIN_FIELD_NOTES"));
            DispatchResult notReady = Fail(s, new CompleteQuestCommand(Rid(), "Q_ORIGIN_FOREST_SURVEY"), clock, DomainErrorCode.QuestNotReady);
            Assert.That(notReady.Error.Detail, Is.EqualTo("materialDelivery,materialDelivery"), "có Adult biết đi; thiếu gỗ và hạt giống");
            s.Player.Inventory["wood"] = 3; s.Player.Inventory["seed"] = 2;
            GameState done = Ok(s, new CompleteQuestCommand(Rid(), "Q_ORIGIN_FOREST_SURVEY"), clock).State;
            Fail(done, new CompleteQuestCommand(Rid(), "Q_ORIGIN_FOREST_SURVEY"), clock, DomainErrorCode.QuestAlreadyCompleted);
        }

        [Test]
        public void Side_quest_completion_delivers_materials_once_gives_no_gold_and_is_idempotent_by_request_id()
        {
            GameState s = WithDragons(Day0, At("e", "emberling", "origin_meadow"));
            s.Player.Inventory["wood"] = 5; s.Player.Inventory["seed"] = 2;
            var clock = new FixedClock(Day0 + 10 * Min);
            DispatchResult r = Ok(s, new CompleteQuestCommand("cq-1", "Q_ORIGIN_FOREST_SURVEY"), clock);
            Assert.That((r.State.Player.Inventory["wood"], r.State.Player.Inventory["seed"]), Is.EqualTo((2L, 0L)));
            Assert.That(r.State.Player.Gold.Micro - s.Player.Gold.Micro, Is.EqualTo(GoldFromTime(r)), "không thưởng vàng");
            Assert.That(r.State.CompletedSideQuests, Is.EqualTo(new[] { "Q_ORIGIN_FOREST_SURVEY" }));
            Assert.That(r.State.CompletedQuests, Is.Empty, "nhiệm vụ phụ không vào completedQuests (chỉ cổng canonical)");
            QuestRewarded ev = r.Events.OfType<QuestRewarded>().Single();
            Assert.That((ev.QuestId, ev.Reward, ev.RewardId), Is.EqualTo(("Q_ORIGIN_FOREST_SURVEY", QuestRewardKind.DecorBlueprint, "blueprint.forest_lantern")));
            Assert.That(ev.Delivered.Select(kv => kv.Key + ":" + kv.Value), Is.EqualTo(new[] { "wood:3", "seed:2" }));
            Assert.That(r.Events.OfType<QuestCompleted>(), Is.Empty, "questCompleted chỉ cho nhiệm vụ cổng");
            DispatchResult dup = CommandDispatcher.Dispatch(r.State, new CompleteQuestCommand("cq-1", "Q_ORIGIN_FOREST_SURVEY"), clock);
            Assert.That((dup.Duplicate, dup.State), Is.EqualTo((true, r.State)));
            Assert.That(ProgressionQueries.Rewards(r.State).DecorBlueprints, Is.EqualTo(new[] { "blueprint.forest_lantern" }));
            Assert.That(GameStateValidator.Validate(r.State).IsValid, Is.True, GameStateValidator.Validate(r.State).ToString());
        }

        [Test]
        public void Completed_side_quests_stay_in_catalog_order_and_unlock_dependent_quests()
        {
            GameState s = WithDragons(Day0, At("e", "emberling", "origin_meadow"), At("s", "sprout", "origin_meadow"));
            ViewProfiles(s);
            s.Player.Inventory["wood"] = 3; s.Player.Inventory["seed"] = 2;
            var clock = new FixedClock(Day0 + Min);
            GameState a = Ok(s, new CompleteQuestCommand(Rid(), "Q_ORIGIN_FOREST_SURVEY"), clock).State;
            Assert.That(QuestProposals.Evaluate(a).Single(q => q.QuestId == "Q_RESEARCH_NIGHT_WATCH").State, Is.EqualTo(QuestState.Locked));
            GameState b = Ok(a, new CompleteQuestCommand(Rid(), "Q_ORIGIN_FIELD_NOTES"), clock).State;
            Assert.That(b.CompletedSideQuests, Is.EqualTo(new[] { "Q_ORIGIN_FIELD_NOTES", "Q_ORIGIN_FOREST_SURVEY" }), "thứ tự catalog, không phải thứ tự nhận");
            Assert.That(QuestProposals.Evaluate(b).Single(q => q.QuestId == "Q_RESEARCH_NIGHT_WATCH").State, Is.EqualTo(QuestState.InProgress));
            Assert.That(QuestProposals.Evaluate(b).Single(q => q.QuestId == "Q_EXPEDITION_PATHFINDER").State, Is.EqualTo(QuestState.InProgress));
        }

        [Test]
        public void Rejected_quest_command_never_changes_state_even_after_settling_time()
        {
            GameState s = WithDragons(Day0, At("e", "emberling", "origin_meadow"));
            s.ProductionDragonIds.Add("e");
            var json = StateJson.ToJson(s);
            Fail(s, new CompleteQuestCommand(Rid(), "Q_ORIGIN_FOREST_SURVEY"), new FixedClock(Day0 + 5 * Hour), DomainErrorCode.QuestNotReady);
            Assert.That(MiniJson.Diff(json, StateJson.ToJson(s)), Is.Null);
        }

        // ================================================================== Thủy Vực → Thiên Giới

        [Test]
        public void Ocean_then_sky_unlock_through_real_quests_and_canonical_unlockZone_only()
        {
            GameState s = WithDragons(Day0, At("e", "emberling", "origin_meadow"), At("s", "sprout", "origin_meadow", Personality.Playful),
                At("d", "droplet", "origin_pondbank", Personality.Curious), At("d2", "droplet", "origin_pondbank"));
            ViewProfiles(s);
            s.Player.Inventory["wood"] = 6; s.Player.Inventory["seed"] = 4;
            s = Study(s, Day0, Day0 + 6 * Day);
            var clock = new FixedClock(Day0 + 6 * Day);

            GameState a = Ok(s, new CompleteQuestCommand(Rid(), "Q_ORIGIN_FIELD_NOTES"), clock).State;
            DispatchResult gate = Ok(a, new CompleteQuestCommand(Rid(), "QUEST_AQUATIC_EXPLORATION"), clock);
            Assert.That(gate.Events.OfType<QuestCompleted>().Single().QuestId, Is.EqualTo("QUEST_AQUATIC_EXPLORATION"));
            Assert.That(gate.State.FindZone("ocean").Unlocked, Is.False);
            GameState ocean = Ok(gate.State, new UnlockZoneCommand(Rid(), "ocean"), clock).State;
            Assert.That(ocean.FindZone("ocean").Unlocked, Is.True);
            Assert.That(ocean.GrantedRewards, Does.Contain("ZONE_EGG_OCEAN"));

            // Thiên Giới: Ethology cho một loài có sinh cảnh Thủy Vực, 2 loài được ghi chép ở microhabitat Thủy Vực, 1 Adult biết bay ở sinh cảnh, 4 pha lê + 4 san hô.
            Fail(ocean, new UnlockZoneCommand(Rid(), "sky"), clock, DomainErrorCode.UnlockRequirementMissing);
            string[] estuary = SpeciesCatalog.All.Where(sp => sp.HabitatRules.Contains("ocean_estuary") && sp.Id != "droplet").Select(sp => sp.Id).Take(1).ToArray();
            GameState w = ocean.DeepClone();
            w.FindDragon("d2").Location = DragonLocation.Zone("ocean", "ocean_estuary");
            w.Dragons.Add(At("o1", estuary[0], "ocean_estuary"));
            w.Dragons.Add(At("z", "zephyr", "origin_meadow"));
            Codex.Observe(w, null);
            ViewProfiles(w);
            w.Player.Inventory["crystal"] = 4; w.Player.Inventory["coral"] = 4;
            Fail(w, new CompleteQuestCommand(Rid(), "QUEST_SKY_EXPLORATION"), clock, DomainErrorCode.QuestNotReady);
            w = Study(w, Day0 + 6 * Day, Day0 + 16 * Day);
            var later = new FixedClock(Day0 + 16 * Day);
            QuestStatusView sky = QuestProposals.Evaluate(w).Single(q => q.QuestId == "QUEST_SKY_EXPLORATION");
            Assert.That(sky.Requirements.Where(r => r.State != RequirementState.Met).Select(r => r.Def.Kind + ":" + r.Current + "/" + r.Target), Is.Empty);
            DispatchResult skyGate = Ok(w, new CompleteQuestCommand(Rid(), "QUEST_SKY_EXPLORATION"), later);
            Assert.That((skyGate.State.Player.Inventory["crystal"], skyGate.State.Player.Inventory["coral"]), Is.EqualTo((0L, 0L)));
            Assert.That(skyGate.State.CompletedQuests, Is.EqualTo(new[] { "QUEST_AQUATIC_EXPLORATION", "QUEST_SKY_EXPLORATION" }));
            GameState skyOpen = Ok(skyGate.State, new UnlockZoneCommand(Rid(), "sky"), later).State;
            Assert.That(skyOpen.FindZone("sky").Unlocked, Is.True);
            Assert.That(ProgressionQueries.QuestLog(skyOpen).ZoneUnlockMissing, Is.Empty);
            Assert.That(GameStateValidator.Validate(skyOpen).IsValid, Is.True, GameStateValidator.Validate(skyOpen).ToString());
            // U2.15: bảng A đã duyệt → yêu cầu "tuyến có thưởng" đạt ngay khi Thiên Giới mở (nhiệm vụ còn chờ số chuyến).
            QuestStatusView cloud = QuestProposals.Evaluate(skyOpen).Single(q => q.QuestId == "Q_EXPEDITION_CLOUD");
            Assert.That(cloud.Requirements.Single(r => r.Def.Kind == QuestRequirementKind.RouteAvailable).State, Is.EqualTo(RequirementState.Met));
            // Đối chứng U2.14: với bảng tắt, Q_EXPEDITION_CLOUD không bao giờ Ready.
            using (SkyCloudBalance.Use(SkyCloudBalance.Disabled))
            {
                QuestStatusView off = QuestProposals.Evaluate(skyOpen).Single(q => q.QuestId == "Q_EXPEDITION_CLOUD");
                Assert.That(off.Requirements.Single(r => r.Def.Kind == QuestRequirementKind.RouteAvailable).State, Is.EqualTo(RequirementState.Blocked));
                Assert.That(off.CanComplete, Is.False);
            }
        }

        // ================================================================== quan hệ

        [Test]
        public void Relations_accrue_one_point_per_hour_together_capped_at_40_and_never_touch_gold_or_xp()
        {
            GameState s = WithDragons(Day0, At("a", "sprout", "origin_meadow"), At("b", "sprout", "origin_meadow"), At("c", "emberling", "origin_warm_rocks"),
                Adult("st", "sprout"));
            s.ProductionDragonIds.AddRange(new[] { "a", "b", "c" });
            var clock = new FixedClock(Day0 + 5 * Hour + 30 * Min);
            DispatchResult r = Ok(s, Online(), clock);
            Assert.That(r.State.Relations.SharedMs("a", "b"), Is.EqualTo(5 * Hour + 30 * Min));
            Assert.That(r.State.Relations.SharedMs("a", "c") + r.State.Relations.SharedMs("a", "st") + r.State.Relations.SharedMs("b", "st"), Is.Zero, "khác chỗ / trong kho không tính");
            SocialRelation rel = SocialEcology.Relation(r.State, r.State.FindDragon("a"), r.State.FindDragon("b"));
            Assert.That((rel.Familiarity, rel.Tier), Is.EqualTo((20 + 10 + 5, RelationTier.Acquainted)), "cùng chỗ 20 + cùng loài 10 + 5 giờ");
            Assert.That(rel.Sources, Does.Contain(FamiliaritySource.SharedTime));
            // Không đổi vàng / XP: so với cùng lần quyết toán khi bỏ quan hệ.
            GameState noRel = s.DeepClone(); noRel.Relations = null;
            DispatchResult plain = CommandDispatcher.Dispatch(noRel, Online(), clock);
            Assert.That(plain.Ok, Is.True);
            Assert.That(plain.State.Player.Gold, Is.EqualTo(r.State.Player.Gold));
            Assert.That(plain.State.Dragons.Select(d => d.XpSec), Is.EqualTo(r.State.Dragons.Select(d => d.XpSec)));
            // Offline cũng tính (không giảm); trần 40 giờ.
            GameState t = r.State;
            for (int i = 1; i <= 3; i++) t = Ok(t, Offline(), new FixedClock(Day0 + 5 * Hour + 30 * Min + i * Day)).State;
            Assert.That(t.Relations.SharedMs("a", "b"), Is.EqualTo(RelationRules.MaxSharedMs));
            SocialRelation capped = SocialEcology.Relation(t, t.FindDragon("a"), t.FindDragon("b"));
            Assert.That((capped.Familiarity, capped.Tier), Is.EqualTo((70, RelationTier.Friend)));
            // Bán / thả một bên: cặp bị bỏ khỏi save.
            GameState sold = Ok(t, new SellDragonCommand(Rid(), "b"), new FixedClock(Day0 + 5 * Day)).State;
            Assert.That(sold.Relations.Count, Is.Zero);
        }

        [Test]
        public void Expedition_traveller_and_care_dragons_do_not_accrue_relations()
        {
            GameState s = WithDragons(Day0, At("a", "emberling", "origin_meadow"), At("b", "emberling", "origin_meadow"));
            GameState onTrip = Ok(s, new StartExpeditionCommand(Rid(), "origin_forest", "b"), new FixedClock(Day0)).State;
            GameState after = Ok(onTrip, Online(), new FixedClock(Day0 + 10 * Min)).State;
            Assert.That(after.Relations.SharedMs("a", "b"), Is.Zero);
        }

        [Test]
        public void Social_quests_use_relation_tiers()
        {
            GameState s = WithDragons(Day0, At("a", "sprout", "origin_meadow"), At("b", "sprout", "origin_meadow"), At("c", "sprout", "origin_meadow"));
            Fail(s, new CompleteQuestCommand(Rid(), "Q_SOCIAL_FIRST_FRIENDS"), new FixedClock(Day0), DomainErrorCode.QuestNotReady);
            GameState t = s;
            for (int i = 1; i <= 1; i++) t = Ok(t, Offline(), new FixedClock(Day0 + i * Day)).State;
            QuestStatusView v = QuestProposals.Evaluate(t).Single(q => q.QuestId == "Q_SOCIAL_FIRST_FRIENDS");
            Assert.That((v.Requirements[0].Current, v.State), Is.EqualTo((3, QuestState.Ready)), "3 cặp: 30 + 24 giờ = 54 ≥ Friend");
            GameState done = Ok(t, new CompleteQuestCommand(Rid(), "Q_SOCIAL_FIRST_FRIENDS"), new FixedClock(Day0 + Day)).State;
            Assert.That(ProgressionQueries.Rewards(done).Titles, Does.Contain("title.friend_of_dragons"));
            RelationsDto rd = ProgressionQueries.Relations(done, "a");
            Assert.That(rd.Relations.Select(x => x.Tier), Is.All.EqualTo(RelationTier.Friend));
        }

        [Test]
        public void Social_rules_still_have_no_gameplay_effect()
        {
            Assert.That((SocialRules.Production.GameplayEffectsEnabled, SocialRules.Production.CompanionComfortBonus, SocialRules.Production.CrowdingComfortPenalty), Is.EqualTo((false, 0, 0)));
            RuleFlagsDto f = ProgressionQueries.Rules();
            Assert.That((f.QuestCompletionEnabled, f.RelationsRecordingEnabled, f.ExplorationMilestonesEnabled), Is.EqualTo((true, true, true)));
            Assert.That(f.SocialGameplayEffectsEnabled || f.ComfortDriftEnabled || f.NaturalDisastersEnabled || f.EnergyDrainEnabled || f.ResearchPacingEnabled, Is.False,
                "thiên tai, Comfort drift, tiêu hao năng lượng, khóa Codex vẫn tắt");
        }

        // ================================================================== thám hiểm

        [Test]
        public void Expedition_milestones_pay_material_bonuses_once_at_1_5_15_40_claims()
        {
            GameState s = WithDragons(Day0, At("x", "emberling", "origin_meadow"));
            long now = Day0;
            long bonus = 0;
            var reached = new List<int>();
            for (int i = 1; i <= 16; i++)
            {
                s = Ok(s, new StartExpeditionCommand(Rid(), "origin_forest", "x"), new FixedClock(now)).State;
                now = s.Expedition.FinishAtMs;
                long before = s.Player.Inventory.Values.Sum() + s.Expedition.Rewards.Sum(kv => kv.Value);
                DispatchResult c = Ok(s, new ClaimExpeditionCommand(Rid()), new FixedClock(now));
                foreach (ExplorationMilestoneReached m in c.Events.OfType<ExplorationMilestoneReached>())
                {
                    reached.Add(m.Claims);
                    bonus += m.Bonus.Sum(kv => kv.Value);
                    Assert.That(m.Bonus.Select(kv => kv.Key), Is.EqualTo(new[] { "wood", "seed" }));
                }
                Assert.That(c.State.Player.Inventory.Values.Sum() - before, Is.EqualTo(c.Events.OfType<ExplorationMilestoneReached>().Sum(m => m.Bonus.Sum(kv => (long)kv.Value))));
                s = c.State;
            }
            Assert.That(reached, Is.EqualTo(new[] { 1, 5, 15 }));
            Assert.That(bonus, Is.EqualTo(2 * (2 + 3 + 4)));
            Assert.That(s.ExpeditionsClaimed["origin_forest"], Is.EqualTo(16));
            RouteExplorationDto dto = ProgressionQueries.ExplorationRoutes(s).Single(r => r.RouteId == "origin_forest");
            Assert.That((dto.Claimed, dto.NextMilestoneAt, dto.NextMilestoneBonusEach), Is.EqualTo((16, (int?)40, 6)));
            Assert.That(ProgressionQueries.ExplorationRoutes(s).Single(r => r.RouteId == "sky_cloud").RewardsDefined, Is.True, "U2.15: sky_cloud mở (bảng A)");
            Assert.That(ProgressionQueries.Rewards(s).Titles, Does.Contain("title.forest_ranger"));
            Assert.That(ProgressionQueries.Rewards(s).CodexNotes, Does.Contain("note.forest_first_trail"));
            Assert.That(QuestProposals.Evaluate(s).Single(q => q.QuestId == "Q_EXPEDITION_PATHFINDER").Requirements[0].Current, Is.EqualTo(16));
            // U2.15: sky_cloud theo bảng A đã duyệt: mốc 1 / 5 / 15 / 40 → +2 / 3 / 4 / 6 bụi sao; trứng Aurorawing gộp vào mốc 15.
            Assert.That(Exploration.Milestones.Where(m => m.RouteId == "sky_cloud").Select(m => (m.Claims, m.BonusEach, m.RewardId, m.EggRewardId)),
                Is.EqualTo(new[] { (1, 2, "note.cloud_first_flight", (string)null), (5, 3, (string)null, (string)null),
                    (15, 4, "title.cloud_rider", "EXPEDITION_EGG_AURORAWING"), (40, 6, "title.star_warden", (string)null) }));
            // Đối chứng U2.14 (bảng tắt): chỉ còn mốc trứng, không vật liệu / danh hiệu.
            using (SkyCloudBalance.Use(SkyCloudBalance.Disabled))
                Assert.That(Exploration.Milestones.Where(m => m.RouteId == "sky_cloud").Select(m => (m.Claims, m.BonusEach, m.RewardId, m.EggRewardId)),
                    Is.EqualTo(new[] { (15, 0, (string)null, "EXPEDITION_EGG_AURORAWING") }));
        }

        // ================================================================== nghiên cứu + danh hiệu

        [Test]
        public void Research_progression_reports_each_realm_and_derived_titles()
        {
            GameState s = WithDragons(Day0, At("e", "emberling", "origin_meadow"), At("s", "sprout", "origin_meadow"), At("d", "droplet", "origin_pondbank"));
            ViewProfiles(s);
            ResearchProgressionDto r0 = ProgressionQueries.ResearchProgression(s);
            Assert.That(r0.Realms.Select(x => x.Realm), Is.EqualTo(new[] { Realm.Land, Realm.Ocean, Realm.Sky }));
            Assert.That(r0.Realms.Single(x => x.Realm == Realm.Land).SpeciesDiscovered, Is.EqualTo(3));
            Assert.That(r0.Realms.Single(x => x.Realm == Realm.Ocean).SpeciesDiscovered, Is.EqualTo(1), "droplet sống cả ở cửa sông");
            Assert.That(r0.Realms.Sum(x => x.SpeciesTotal), Is.GreaterThanOrEqualTo(SpeciesCatalog.All.Count));
            Assert.That(r0.Realms.All(x => x.ProgressPct >= 0 && x.ProgressPct <= 100), Is.True);
            Assert.That(r0.Titles.Single(t => t.TitleId == "title.lifecycle_student").Earned, Is.True, "3 loài LifeCycle");
            Assert.That(r0.Titles.Single(t => t.TitleId == "title.field_ethologist").Earned, Is.False);
            s = Study(s, Day0, Day0 + 12 * Day);
            ResearchProgressionDto r1 = ProgressionQueries.ResearchProgression(s);
            Assert.That(r1.Tiers[ResearchTier.Ethology], Is.GreaterThanOrEqualTo(1), string.Join(" | ", s.Codex.Select(e => { var p = ResearchProgress.Species(s, e.SpeciesId, s.Research, ResearchPacing.Production);
                return e.SpeciesId + " obs=" + string.Join(",", p.ObservedBehaviors) + " need=" + string.Join(",", p.ObservableBehaviors) + " circ=" + p.CircadianCovered + " days=" + p.DistinctDays + " food=" + s.Food.Drops.Count; })));
            Assert.That(r1.Titles.Single(t => t.TitleId == "title.field_ethologist").Earned, Is.True);
            Assert.That(r1.Realms.Single(x => x.Realm == Realm.Land).ProgressPct, Is.GreaterThan(r0.Realms.Single(x => x.Realm == Realm.Land).ProgressPct));
        }

        // ================================================================== Sanctuary

        [Test]
        public void Sanctuary_project_content_covers_every_project_and_follows_the_journey()
        {
            var ids = new HashSet<string>();
            for (int n = 0; n < SanctuaryProjectCatalog.Max; n++)
            {
                SanctuaryProjectDef d = SanctuaryProjectContent.Get(n);
                Assert.That(d, Is.Not.Null, n.ToString());
                Assert.That(ids.Add(d.Id), Is.True, d.Id);
                Assert.That(WorldCatalog.IsZoneId(d.ZoneId), Is.True, d.Id);
                if (d.StyleBlueprintId != null) Assert.That(QuestProposals.All.Any(q => q.RewardId == d.StyleBlueprintId), Is.True, d.StyleBlueprintId);
            }
            Assert.That(SanctuaryProjectContent.Get(-1), Is.Null);
            Assert.That(SanctuaryProjectContent.Get(SanctuaryProjectCatalog.Max), Is.Null);
            Assert.That(SanctuaryProjectContent.Named.Take(8).All(d => d.ZoneId == "origin"), Is.True);
            Assert.That(SanctuaryProjectContent.Named.Skip(8).Take(6).All(d => d.ZoneId == "ocean"), Is.True);
            Assert.That(SanctuaryProjectContent.Named.Skip(14).All(d => d.ZoneId == "sky"), Is.True);
            // Thế giới chưa mở: hiển thị ở Khởi Nguyên, không bao giờ mất.
            (GameState s, _) = NewGame();
            Assert.That(SanctuaryProjectContent.DisplayZone(s, 9), Is.EqualTo("origin"));
            GameState w = s.DeepClone();
            WorldExpansion.ApplyZoneUnlock(w, "ocean", null);
            Assert.That(SanctuaryProjectContent.DisplayZone(w, 9), Is.EqualTo("ocean"));
            // Giá / vật liệu E5 không đổi.
            Assert.That((SanctuaryProjectCatalog.CostG(0), SanctuaryProjectCatalog.CostG(1), SanctuaryProjectCatalog.MaterialEach(2)), Is.EqualTo(((long?)20_000, (long?)25_000, 1)));
        }

        [Test]
        public void Sanctuary_project_views_show_built_projects_next_one_and_style_variants_from_quests()
        {
            GameState s = WithDragons(Day0, At("e", "emberling", "origin_meadow"));
            s.Player.Gold = MicroGold.FromWholeGold(200_000);
            s.Player.Inventory["wood"] = 3; s.Player.Inventory["seed"] = 2;
            var clock = new FixedClock(Day0 + Min);
            GameState a = Ok(s, new BuildSanctuaryProjectCommand(Rid()), clock).State;
            a = Ok(a, new BuildSanctuaryProjectCommand(Rid()), clock).State;
            List<SanctuaryProjectView> v = ProgressionQueries.SanctuaryProjects(a);
            Assert.That(v.Select(x => x.Content.Id + ":" + x.Built), Is.EqualTo(new[] { "origin_mossy_steps:True", "origin_lantern_row:True", "origin_meadow_arbor:False" }));
            Assert.That(v[1].StyleVariant, Is.False);
            GameState b = Ok(a, new CompleteQuestCommand(Rid(), "Q_ORIGIN_FOREST_SURVEY"), clock).State;
            Assert.That(ProgressionQueries.SanctuaryProjects(b)[1].StyleVariant, Is.True, "mẫu đèn rừng đổi kiểu hàng đèn đá");
            Assert.That(ProgressionQueries.SanctuaryEconomy(b, Day0).NextProject.Id, Is.EqualTo("origin_meadow_arbor"));
        }
    }
}
