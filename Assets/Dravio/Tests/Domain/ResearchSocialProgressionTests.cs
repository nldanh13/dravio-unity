using System;
using System.Collections.Generic;
using System.Linq;
using Dravio.Domain.Behavior;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Progression;
using Dravio.Domain.Queries;
using Dravio.Domain.Tests.Support;
using Dravio.Domain.World;
using NUnit.Framework;
using static Dravio.Domain.Tests.Support.Fixtures;

namespace Dravio.Domain.Tests
{
    /// <summary>
    /// U2.10 — Research, Social Ecology, World Progression, API (ADR 0204). Mọi module chỉ đọc: test kiểm luật mặc định tắt, tính tất định,
    /// không đổi state, không vượt luật canonical mở thế giới, và tiến trình nghiên cứu có nhịp theo ngày.
    /// </summary>
    public sealed class ResearchSocialProgressionTests
    {
        private const long Sec = 1000, Min = 60 * Sec, Hour = 60 * Min, Day = 24 * Hour;
        private static readonly long Day0 = new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

        private static GameState WithDragons(params DragonState[] ds)
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
            return s;
        }

        private static DragonState At(string uid, string species, string habitatId, Personality p = Personality.Calm, Action<DragonState> extra = null) =>
            Adult(uid, species, d => { d.Location = DragonLocation.Zone(HabitatCatalog.ZoneOf(habitatId), habitatId); d.Personality = p; extra?.Invoke(d); });

        private static DayPhase PhaseAt(long now) => DayCycle.PhaseOfMinute((int)((now / Min) % DayCycle.MinutesPerDay));

        /// <summary>Quan sát mọi rồng hiện ở sinh cảnh mỗi <paramref name="stepMs"/> trong [from, to). Trả số lần ghi nhận.</summary>
        private static int ObserveRange(GameState s, ResearchLog log, ResearchPacing pacing, long from, long to, long stepMs = 31 * Sec)
        {
            int n = 0;
            for (long t = from; t < to; t += stepMs)
                foreach (BehaviorDecision d in BehaviorEngine.DecideAll(s, t, PhaseAt(t)))
                    if (log.Observe(s, d, t, pacing).Recorded) n++;
            return n;
        }

        // ================================================================== Workstream A — Research

        [Test]
        public void Production_research_pacing_is_off_and_keeps_current_codex_behaviour()
        {
            Assert.That(ResearchPacing.Production.Enabled, Is.False);
            GameState s = WithDragons(At("a", "emberling", "origin_meadow"));
            var log = new ResearchLog();
            BehaviorDecision d = BehaviorEngine.Decide(s, "a", Day0 + 12 * Hour, DayPhase.Day);
            ResearchObservationResult r = log.Observe(s, d, Day0 + 12 * Hour, ResearchPacing.Production);
            Assert.That(r.Recorded, Is.False);
            Assert.That(r.Reason, Is.EqualTo("research-disabled"));
            SpeciesResearchProfile p = ResearchProgress.Species(s, "emberling", log, null);
            Assert.That(p.Gated, Is.False);
            Assert.That(p.RevealedSections, Is.EquivalentTo(new[] { CodexSection.Identity, CodexSection.Habitat, CodexSection.Physiology, CodexSection.Behavior }));
            IndividualResearchProfile ind = ResearchProgress.Individual(s, "a", log, null);
            Assert.That(ind.Archetype, Is.EqualTo(Personality.Calm));
            Assert.That(ind.FavoriteHabitatId, Is.Not.Null);
            Assert.That(ind.FavoriteDiet, Is.Not.Null);
            Assert.That(ResearchProgress.Opportunities(s, Day0 + 12 * Hour, DayPhase.Day, log, null), Is.Empty);
        }

        [Test]
        public void Observation_cooldown_blocks_tap_spam_per_dragon()
        {
            GameState s = WithDragons(At("a", "emberling", "origin_meadow"));
            var log = new ResearchLog();
            long t = Day0 + 12 * Hour;
            Assert.That(log.Observe(s, BehaviorEngine.Decide(s, "a", t, DayPhase.Day), t, ResearchPacing.ProposedU210).Recorded, Is.True);
            for (long dt = 1 * Sec; dt < 30 * Sec; dt += 3 * Sec)
                Assert.That(log.Observe(s, BehaviorEngine.Decide(s, "a", t + dt, DayPhase.Day), t + dt, ResearchPacing.ProposedU210).Reason, Is.EqualTo("observation-cooldown"));
            Assert.That(log.CooldownRemainingMs("a", t + 10 * Sec, ResearchPacing.ProposedU210), Is.EqualTo(20 * Sec));
            Assert.That(log.Observe(s, BehaviorEngine.Decide(s, "a", t + 30 * Sec, DayPhase.Day), t + 30 * Sec, ResearchPacing.ProposedU210).Recorded, Is.True);
            Assert.That(log.IndividualRecord("a").Observations, Is.EqualTo(2));
        }

        [Test]
        public void Species_findings_are_capped_per_utc_day_and_deferred_not_lost()
        {
            var ds = Enumerable.Range(0, 5).Select(i => At("e" + i, "emberling", i % 2 == 0 ? "origin_meadow" : "origin_warm_rocks", (Personality)(i % 4))).ToArray();
            GameState s = WithDragons(ds);
            var log = new ResearchLog();
            ObserveRange(s, log, ResearchPacing.ProposedU210, Day0, Day0 + Day);
            SpeciesResearchRecord r = log.SpeciesRecord("emberling");
            Assert.That(r.FindingsByDay.Values.Max(), Is.LessThanOrEqualTo(2));
            int day1 = r.Behaviors.Count + r.Phases.Count + r.Habitats.Count;
            Assert.That(day1, Is.EqualTo(2));
            ObserveRange(s, log, ResearchPacing.ProposedU210, Day0 + Day, Day0 + 2 * Day);
            Assert.That(r.Behaviors.Count + r.Phases.Count + r.Habitats.Count, Is.EqualTo(4), "phát hiện bị hoãn hôm trước xuất hiện hôm sau");
            Assert.That(r.Days.Count, Is.EqualTo(2));
        }

        [Test]
        public void Ethology_needs_several_days_under_U210_pacing_but_one_day_without_it()
        {
            DragonState[] team = { At("a", "droplet", "origin_pondbank", Personality.Playful), At("b", "droplet", "origin_pondbank", Personality.Curious),
                At("c", "droplet", "origin_pondbank", Personality.Calm) };
            GameState s = WithDragons(team);
            s.Tutorial.ProfilesViewed.Add("droplet");
            var fast = new ResearchLog();
            ObserveRange(s, fast, ResearchPacing.UnpacedU29, Day0, Day0 + Day);
            List<DragonBehavior> observable = Research.ObservableBehaviors("droplet");
            // SeekFood/Eat cần thức ăn thật; trong test này chỉ trình diễn, nên kiểm phần còn lại.
            var ambient = observable.Where(b => BehaviorCatalog.Get(b).Ambient).ToList();
            Assert.That(ambient.All(b => fast.SpeciesRecord("droplet").Behaviors.Contains(b)), Is.True, "U2.9 không giới hạn: đủ tập tính trong 1 ngày");

            var paced = new ResearchLog();
            int days = 0;
            for (; days < 30; days++)
            {
                ObserveRange(s, paced, ResearchPacing.ProposedU210, Day0 + days * Day, Day0 + (days + 1) * Day, 61 * Sec);
                if (ambient.All(b => paced.SpeciesRecord("droplet").Behaviors.Contains(b)) && paced.SpeciesRecord("droplet").Phases.Count == 4) break;
            }
            Assert.That(days + 1, Is.GreaterThanOrEqualTo(4), "2 phát hiện/ngày: cần nhiều ngày");
            Assert.That(days + 1, Is.LessThanOrEqualTo(10), "nhưng không kéo dài vô hạn");
            SpeciesResearchProfile p = ResearchProgress.Species(s, "droplet", paced, ResearchPacing.ProposedU210);
            Assert.That(p.DistinctDays, Is.GreaterThanOrEqualTo(2));
            Assert.That(p.CircadianCovered, Is.True);
        }

        [Test]
        public void Individual_traits_are_revealed_progressively()
        {
            GameState s = WithDragons(At("a", "emberling", "origin_meadow", Personality.Shy));
            var log = new ResearchLog();
            ResearchPacing pace = ResearchPacing.ProposedU210;
            Assert.That(ResearchProgress.Individual(s, "a", log, pace).Archetype, Is.Null);
            long t = Day0 + 12 * Hour;
            ResearchObservationResult first = log.Observe(s, BehaviorEngine.Decide(s, "a", t, DayPhase.Day), t, pace);
            Assert.That(first.Findings.Any(f => f.Kind == ResearchFindingKind.Individual && f.Value == "archetype"), Is.True);
            IndividualResearchProfile p = ResearchProgress.Individual(s, "a", log, pace);
            Assert.That(p.Archetype, Is.EqualTo(Personality.Shy));
            Assert.That(p.FavoriteDiet, Is.Null);
            // Ban đêm emberling ngủ → thấy pha nghỉ → hé lộ nhịp sinh học.
            long night = Day0 + 23 * Hour;
            log.Observe(s, BehaviorEngine.Decide(s, "a", night, DayPhase.Night), night, pace);
            Assert.That(ResearchProgress.Individual(s, "a", log, pace).RevealedTraits, Does.Contain(IndividualTrait.Chronotype));
        }

        [Test]
        public void Opportunities_flag_only_observations_that_teach_something()
        {
            GameState s = WithDragons(At("a", "emberling", "origin_meadow"), At("b", "sprout", "origin_meadow"));
            var log = new ResearchLog();
            ResearchPacing pace = ResearchPacing.ProposedU210;
            long t = Day0 + 12 * Hour;
            List<ObservationOpportunity> o = ResearchProgress.Opportunities(s, t, DayPhase.Day, log, pace);
            Assert.That(o.Count, Is.EqualTo(2));
            Assert.That(o.All(x => x.Worthwhile), Is.True);
            log.Observe(s, BehaviorEngine.Decide(s, "a", t, DayPhase.Day), t, pace);
            o = ResearchProgress.Opportunities(s, t + 5 * Sec, DayPhase.Day, log, pace);
            ObservationOpportunity a = o.Single(x => x.Uid == "a");
            Assert.That(a.Worthwhile, Is.False);
            Assert.That(a.HintKey, Is.EqualTo("research.hint.cooldown"));
            Assert.That(o[0].Uid, Is.EqualTo("b"), "cơ hội đáng quan sát đứng trước");
        }

        [Test]
        public void Research_log_snapshot_round_trips_and_the_v9_draft_validator_rejects_bad_data()
        {
            GameState s = WithDragons(At("a", "droplet", "origin_pondbank"), At("b", "emberling", "origin_meadow"));
            var log = new ResearchLog();
            ObserveRange(s, log, ResearchPacing.ProposedU210, Day0, Day0 + 2 * Day, 97 * Sec);
            ResearchLogSnapshot snap = log.ToSnapshot();
            Assert.That(ResearchSchemaDraft.Validate(snap, s), Is.Empty);
            string a = Dump(snap), b = Dump(ResearchLog.FromSnapshot(snap).ToSnapshot());
            Assert.That(b, Is.EqualTo(a));

            ResearchLogSnapshot bad = ResearchLog.FromSnapshot(snap).ToSnapshot();
            bad.Species.Single(x => x.SpeciesId == "emberling").Behaviors.Add("swim");
            bad.Species.Add(new ResearchLogSnapshot.SpeciesEntry { SpeciesId = "nope" });
            bad.Individuals.Add(new ResearchLogSnapshot.IndividualEntry { Uid = "zz-sold", SpeciesId = "emberling" });
            List<string> errs = ResearchSchemaDraft.Validate(bad, s);
            Assert.That(errs.Any(e => e.Contains("impossible for species swim")), Is.True, string.Join("\n", errs));
            Assert.That(errs.Any(e => e.Contains("unknown species")), Is.True);
            Assert.That(errs.Any(e => e.Contains("dragon not kept")), Is.True);

            // Migration nháp v8 → v9: khối rỗng, không suy diễn; state v8 không đổi.
            var before = StateJson.ToJson(s);
            ResearchLogSnapshot migrated = ResearchSchemaDraft.MigrateFromV8(s);
            Assert.That(migrated.Species, Is.Empty);
            Assert.That(migrated.Individuals, Is.Empty);
            Assert.That(MiniJson.Diff(before, StateJson.ToJson(s)), Is.Null);
            Assert.That(s.SchemaVersion, Is.EqualTo(8));
        }

        private static string Dump(ResearchLogSnapshot s) =>
            string.Join(";", s.Species.Select(e => e.SpeciesId + ":" + string.Join(",", e.Behaviors) + "|" + string.Join(",", e.Phases) + "|" + string.Join(",", e.Habitats) + "|" +
                string.Join(",", e.Days) + "|" + string.Join(",", e.FindingsByDay.Select(k => k.Key + "=" + k.Value)))) + "#" +
            string.Join(";", s.Individuals.Select(e => e.Uid + ":" + e.Observations + ":" + e.SeenFeeding + e.SeenInFavoriteHabitat + e.LastObservedAtMs + ":" +
                string.Join(",", e.ActivePhases) + "|" + string.Join(",", e.RestPhases) + "|" + string.Join(",", e.Revealed)));

        [Test]
        public void Pruning_drops_sold_individuals_but_keeps_species_knowledge()
        {
            GameState s = WithDragons(At("a", "emberling", "origin_meadow"), At("b", "emberling", "origin_warm_rocks"));
            var log = new ResearchLog();
            ObserveRange(s, log, ResearchPacing.ProposedU210, Day0 + 10 * Hour, Day0 + 11 * Hour);
            s.Dragons.RemoveAll(d => d.Uid == "b");
            Assert.That(log.PruneIndividuals(s), Is.EqualTo(1));
            Assert.That(log.IndividualRecord("b"), Is.Null);
            Assert.That(log.SpeciesRecord("emberling").Behaviors, Is.Not.Empty);
        }

        // ================================================================== Workstream B — Social Ecology

        [Test]
        public void Social_catalog_covers_every_species_and_not_every_species_is_gregarious()
        {
            Assert.That(SocialCatalog.All.Select(x => x.SpeciesId), Is.EquivalentTo(SpeciesCatalog.All.Select(x => x.Id)));
            foreach (SocialStyle st in new[] { SocialStyle.Gregarious, SocialStyle.Tolerant, SocialStyle.Solitary })
                Assert.That(SocialCatalog.All.Count(x => x.Style == st), Is.GreaterThanOrEqualTo(3), st.ToString());
            foreach (SpeciesSocialDef d in SocialCatalog.All) Assert.That(d.PersonalSpaceDm, Is.InRange(5, 80));
        }

        [Test]
        public void Social_profile_combines_species_personality_and_age()
        {
            DragonState adult = Adult("x1", "leviathan", d => d.Personality = Personality.Calm);
            DragonState baby = Adult("x1", "leviathan", d => { d.Personality = Personality.Calm; d.XpSec = 0; });
            SocialProfile pa = SocialEcology.Profile(adult), pb = SocialEcology.Profile(baby);
            Assert.That(pa.Style, Is.EqualTo(SocialStyle.Solitary));
            Assert.That(pb.Sociability, Is.GreaterThan(pa.Sociability), "rồng con hòa đồng hơn");
            Assert.That(pb.PersonalSpaceDm, Is.LessThan(pa.PersonalSpaceDm));
            Assert.That(SocialEcology.Profile(Adult("x2", "sprout", d => d.Personality = Personality.Playful)).PrefersCompany, Is.True);
        }

        [Test]
        public void Kinship_is_derived_from_saved_pedigree()
        {
            DragonState mom = Adult("mom", "emberling"), dad = Adult("dad", "sprout");
            DragonState c1 = Adult("c1", "emberling", d => d.ParentIds = new[] { "mom", "dad" });
            DragonState c2 = Adult("c2", "sprout", d => d.ParentIds = new[] { "mom", "dad" });
            DragonState half = Adult("c3", "emberling", d => d.ParentIds = new[] { "mom", "other" });
            Assert.That(SocialEcology.Relation(mom, c1).Sources, Does.Contain(FamiliaritySource.ParentChild));
            Assert.That(SocialEcology.Relation(c1, c2).Sources, Does.Contain(FamiliaritySource.Siblings));
            Assert.That(SocialEcology.Relation(c1, half).Sources, Does.Contain(FamiliaritySource.HalfSiblings));
            Assert.That(SocialEcology.Relation(mom, c1).Familiarity, Is.GreaterThanOrEqualTo(60));
            Assert.That(SocialEcology.Relation(dad, mom).Familiarity, Is.LessThan(SocialRules.CuriousMaxFamiliarity));
            SocialRelation r1 = SocialEcology.Relation(c1, c2), r2 = SocialEcology.Relation(c2, c1);
            Assert.That((r1.UidA, r1.UidB, r1.Affinity), Is.EqualTo((r2.UidA, r2.UidB, r2.Affinity)), "đối xứng");
        }

        private static GameState Crowd()
        {
            var ds = new List<DragonState>
            {
                At("s1", "sprout", "origin_meadow", Personality.Playful, d => { d.XpSec = 0; d.ParentIds = new[] { "s2", "wild" }; }),
                At("s2", "sprout", "origin_meadow", Personality.Playful),
                At("z1", "zephyr", "origin_meadow", Personality.Curious),
                At("m1", "magma", "origin_warm_rocks", Personality.Shy),
                At("e1", "emberling", "origin_warm_rocks", Personality.Playful),
            };
            GameState s = WithDragons(ds.ToArray());
            s.Dragons.Add(Adult("st", "sprout"));
            return s;
        }

        [Test]
        public void Social_candidates_are_presentation_only_deterministic_and_exclusive()
        {
            GameState s = Crowd();
            var json = StateJson.ToJson(s);
            int total = 0;
            var kinds = new HashSet<SocialInteractionKind>();
            for (long t = Day0; t < Day0 + Day; t += 7 * Min)
            {
                DayPhase ph = PhaseAt(t);
                List<SocialInteractionCandidate> a = SocialEcology.Candidates(s, t, ph), b = SocialEcology.Candidates(s, t, ph);
                Assert.That(a.Select(x => x.Kind + x.InitiatorUid + x.PartnerUid + x.Score), Is.EqualTo(b.Select(x => x.Kind + x.InitiatorUid + x.PartnerUid + x.Score)));
                var used = new HashSet<string>();
                foreach (SocialInteractionCandidate c in a)
                {
                    total++; kinds.Add(c.Kind);
                    Assert.That(c.AffectsGameplay, Is.False);
                    Assert.That(c.Role, Is.EqualTo(BehaviorRole.Presentation));
                    Assert.That(used.Add(c.InitiatorUid), Is.True, "một tương tác mỗi cá thể mỗi nhịp");
                    DragonState i = s.FindDragon(c.InitiatorUid);
                    Assert.That(i.Location.Kind, Is.EqualTo(LocationKind.Zone));
                    if (c.PartnerUid != null)
                    {
                        Assert.That(used.Add(c.PartnerUid), Is.True);
                        DragonState p = s.FindDragon(c.PartnerUid);
                        Assert.That(p.Location.HabitatId, Is.EqualTo(i.Location.HabitatId), "chỉ cùng microhabitat");
                        Assert.That(c.MinDistanceDm, Is.GreaterThanOrEqualTo(Math.Max(SocialEcology.Profile(i).PersonalSpaceDm, SocialEcology.Profile(p).PersonalSpaceDm)));
                    }
                    if (c.Kind == SocialInteractionKind.RestTogether)
                        Assert.That(SocialEcology.Profile(i).Style != SocialStyle.Solitary && SocialEcology.Profile(s.FindDragon(c.PartnerUid)).Style != SocialStyle.Solitary, Is.True);
                    if (c.Kind == SocialInteractionKind.PlayTogether)
                        Assert.That(SocialEcology.Profile(i).WantsSpace || SocialEcology.Profile(s.FindDragon(c.PartnerUid)).WantsSpace, Is.False);
                    Assert.That(c.InitiatorUid, Is.Not.EqualTo("st"), "rồng trong kho không tương tác");
                }
            }
            Assert.That(total, Is.GreaterThan(20));
            Assert.That(kinds, Does.Contain(SocialInteractionKind.PlayTogether));
            Assert.That(kinds, Does.Contain(SocialInteractionKind.RestTogether));
            Assert.That(MiniJson.Diff(json, StateJson.ToJson(s)), Is.Null, "chỉ đọc");
        }

        [Test]
        public void Gameplay_decisions_win_over_social_pairing()
        {
            GameState s = Crowd();
            long t = Day0 + 12 * Hour;
            s.Food.Drops.Add(new FoodDrop { Id = "f1", DietId = DietId.Leaf, ZoneId = "origin", HabitatId = "origin_meadow", SpawnedAtMs = t - 1000, ExpiresAtMs = t + 11_000 });
            string seeker = BehaviorEngine.DecideAll(s, t, DayPhase.Day).Single(d => d.Behavior == DragonBehavior.SeekFood).Uid;
            foreach (SocialInteractionCandidate c in SocialEcology.Candidates(s, t, DayPhase.Day))
                Assert.That(c.InitiatorUid != seeker && c.PartnerUid != seeker, Is.True);
        }

        [Test]
        public void Solitary_dragons_keep_their_distance_in_crowds()
        {
            var ds = new[]
            {
                At("lev", "magma", "origin_warm_rocks", Personality.Shy), At("e1", "emberling", "origin_warm_rocks"),
                At("e2", "emberling", "origin_warm_rocks"), At("e3", "emberling", "origin_warm_rocks"),
            };
            GameState s = WithDragons(ds);
            Assert.That(SocialEcology.Profile(s.FindDragon("lev")).WantsSpace, Is.True);
            bool kept = false;
            for (long t = Day0 + 8 * Hour; t < Day0 + 16 * Hour; t += 5 * Min)
                foreach (SocialInteractionCandidate c in SocialEcology.Candidates(s, t, PhaseAt(t)))
                {
                    if (c.InitiatorUid == "lev" || c.PartnerUid == "lev") Assert.That(c.Kind, Is.Not.EqualTo(SocialInteractionKind.PlayTogether).And.Not.EqualTo(SocialInteractionKind.RestTogether));
                    if (c.Kind == SocialInteractionKind.KeepDistance && c.InitiatorUid == "lev") kept = true;
                }
            Assert.That(kept, Is.True);
        }

        [Test]
        public void Social_gameplay_effects_are_off_in_production()
        {
            Assert.That(SocialRules.Production.GameplayEffectsEnabled, Is.False);
            GameState s = Crowd();
            foreach (DragonState d in s.Dragons) Assert.That(SocialEcology.ComfortTargetModifier(s, d.Uid, null), Is.Zero);
            Assert.That(s.Dragons.Any(d => SocialEcology.ComfortTargetModifier(s, d.Uid, SocialRules.ProposedForReview) != 0), Is.True, "đề xuất có tác dụng khi bật");
        }

        // ================================================================== Workstream C — Quests

        [Test]
        public void Quest_catalog_respects_canonical_world_gates()
        {
            Assert.That(QuestProposals.ValidateCatalog(), Is.Empty, string.Join("\n", QuestProposals.ValidateCatalog()));
            foreach (QuestDef q in WorldUnlockCatalog.Quests)
                Assert.That(QuestProposals.Get(q.Id), Is.Not.Null, "mọi nhiệm vụ canonical có nội dung đề xuất: " + q.Id);
            Assert.That(QuestProposals.Get("QUEST_AQUATIC_EXPLORATION").RequiresZones, Is.EquivalentTo(new[] { "origin" }));
            Assert.That(QuestProposals.Get("QUEST_SKY_EXPLORATION").RequiresZones, Is.EquivalentTo(new[] { "origin", "ocean" }));
        }

        [Test]
        public void No_quest_is_about_gold_or_waiting_for_a_timer()
        {
            foreach (string n in Enum.GetNames(typeof(QuestRequirementKind)))
                Assert.That(n.Contains("Gold") || n.Contains("Wait") || n.Contains("Time") || n.Contains("Timer"), Is.False, n);
            foreach (string n in Enum.GetNames(typeof(QuestRewardKind))) Assert.That(n.Contains("Gold"), Is.False, n);
            foreach (QuestProposalDef q in QuestProposals.All) Assert.That(q.Requirements.Any(r => r.Kind != QuestRequirementKind.RouteAvailable), Is.True, q.Id);
        }

        private static GameState AquaticReady(out ResearchLog log)
        {
            GameState s = WithDragons(At("e", "emberling", "origin_meadow"), At("s", "sprout", "origin_meadow", Personality.Playful),
                At("d", "droplet", "origin_pondbank", Personality.Curious), At("d2", "droplet", "origin_pondbank"));
            s.Tutorial.Status = TutorialStatus.Completed;
            s.Tutorial.Completed = new List<string>(ProgressCatalog.TutorialStepIds);
            foreach (string sp in new[] { "emberling", "sprout", "droplet" }) s.Tutorial.ProfilesViewed.Add(sp);
            s.Tutorial.ProfilesViewed.Sort(StringComparer.Ordinal);
            s.Player.Inventory["wood"] = 6; s.Player.Inventory["seed"] = 4;
            log = new ResearchLog();
            ObserveRange(s, log, ResearchPacing.ProposedU210, Day0, Day0 + 6 * Day, 3 * Min);
            return s;
        }

        [Test]
        public void Aquatic_quest_can_be_satisfied_with_origin_content_only_but_never_unlocks_the_world_by_itself()
        {
            GameState s = AquaticReady(out ResearchLog log);
            var json = StateJson.ToJson(s);
            QuestStatusView v = QuestProposals.Evaluate(s, log, ResearchPacing.ProposedU210, null).Single(x => x.QuestId == "QUEST_AQUATIC_EXPLORATION");
            Assert.That(v.Requirements.Where(r => r.State != RequirementState.Met).Select(r => r.Def.Kind + ":" + r.Current + "/" + r.Target), Is.Empty);
            Assert.That(v.State, Is.EqualTo(QuestState.Ready));
            Assert.That(v.CanComplete, Is.False, "production không tự hoàn thành");
            Assert.That(MiniJson.Diff(json, StateJson.ToJson(s)), Is.Null);
            Assert.That(s.CompletedQuests, Is.Empty);
            // Lệnh canonical vẫn từ chối: nhiệm vụ chưa được ghi nhận.
            Fail(s, new UnlockZoneCommand(Rid(), "ocean"), new FixedClock(Day0 + 3 * Day), DomainErrorCode.UnlockRequirementMissing);
            // Khi có hệ nhiệm vụ (đã duyệt), hoàn thành đi qua hàm engine canonical rồi lệnh unlockZone kiểm đủ điều kiện như cũ.
            Assert.That(QuestProposals.Evaluate(s, log, ResearchPacing.ProposedU210, QuestRules.ProposedForReview).Single(x => x.QuestId == "QUEST_AQUATIC_EXPLORATION").CanComplete, Is.True);
            GameState w = s.DeepClone();
            Assert.That(WorldExpansion.ApplyQuestComplete(w, "QUEST_AQUATIC_EXPLORATION", null), Is.Null);
            Ok(w, new UnlockZoneCommand(Rid(), "ocean"), new FixedClock(Day0 + 3 * Day));
        }

        [Test]
        public void Quest_requirements_that_need_research_data_are_unknown_when_research_is_off()
        {
            GameState s = AquaticReady(out ResearchLog log);
            QuestStatusView v = QuestProposals.Evaluate(s, log, null, null).Single(x => x.QuestId == "QUEST_AQUATIC_EXPLORATION");
            QuestRequirementStatus circ = v.Requirements.Single(r => r.Def.Kind == QuestRequirementKind.CircadianRecords);
            Assert.That(circ.State, Is.EqualTo(RequirementState.Unknown));
            Assert.That(circ.Detail, Is.EqualTo("research-disabled"));
            Assert.That(v.State, Is.Not.EqualTo(QuestState.Ready));
        }

        [Test]
        public void Sky_cloud_route_stays_blocked_until_its_reward_table_is_approved()
        {
            (GameState s, _) = NewGame();
            QuestStatusView v = QuestProposals.Evaluate(s).Single(x => x.QuestId == "Q_EXPEDITION_CLOUD");
            Assert.That(v.State, Is.EqualTo(QuestState.Locked));
            Assert.That(v.Requirements.Single(r => r.Def.Kind == QuestRequirementKind.RouteAvailable).State, Is.EqualTo(RequirementState.Blocked));
        }

        [Test]
        public void New_game_shows_sky_and_aquatic_gates_locked_or_in_progress_not_ready()
        {
            (GameState s, _) = NewGame();
            foreach (QuestStatusView v in QuestProposals.Evaluate(s)) Assert.That(v.State, Is.Not.EqualTo(QuestState.Ready).And.Not.EqualTo(QuestState.Completed), v.QuestId);
            Assert.That(QuestProposals.Evaluate(s).Single(x => x.QuestId == "QUEST_SKY_EXPLORATION").LockedBy, Does.Contain("zone:ocean"));
        }

        // ================================================================== Workstream E — API

        [Test]
        public void Overview_reports_all_unapproved_rules_off_and_never_suggests_gold()
        {
            (GameState s, _) = NewGame();
            ProgressionOverviewDto o = ProgressionQueries.Overview(s);
            Assert.That(o.Rules.ResearchPacingEnabled || o.Rules.SocialGameplayEffectsEnabled || o.Rules.QuestCompletionEnabled ||
                o.Rules.ComfortDriftEnabled || o.Rules.NaturalDisastersEnabled || o.Rules.EnergyDrainEnabled, Is.False);
            Assert.That(o.SpeciesTotal, Is.EqualTo(16));
            Assert.That(o.CodexCellsTotal, Is.EqualTo(64));
            Assert.That(o.UnlockedZones, Is.EqualTo(new[] { "origin" }));
            Assert.That(o.SuggestedGoals.Count, Is.InRange(1, 3));
            foreach (string g in o.SuggestedGoals) Assert.That(g.ToLowerInvariant().Contains("gold"), Is.False, g);
            ResearchProfileDto r = ProgressionQueries.ResearchProfile(s, "emberling");
            Assert.That(r.Individuals.Count, Is.EqualTo(1));
            Assert.That(ProgressionQueries.SocialProfile(s, s.Dragons[0].Uid), Is.Not.Null);
            Assert.That(ProgressionQueries.SocialRelation(s, s.Dragons[0].Uid, s.Dragons[0].Uid), Is.Null);
        }
    }
}
