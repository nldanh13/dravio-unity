using System;
using System.Collections.Generic;
using Dravio.Domain.Behavior;
using Dravio.Domain.Catalog;
using Dravio.Domain.Economy;
using Dravio.Domain.World;

namespace Dravio.Domain.Progression
{
    /// <summary>Loại bước hành trình (wire name ổn định, i18n <c>journey.step.&lt;code&gt;</c>).</summary>
    public enum JourneyStepCode
    {
        /// <summary>Hướng dẫn đã bị bỏ qua; Thủy Vực cần hướng dẫn hoàn tất → setTutorialStatus(active). Các bước đã thỏa được ghi nhận lại ngay.</summary>
        [WireName("resume-tutorial")] ResumeTutorial,
        /// <summary>Hướng dẫn đang chạy; Target = bước hiện tại (TUT_xx).</summary>
        [WireName("finish-tutorial")] FinishTutorial,
        /// <summary>Khảo sát sinh cảnh Khởi Nguyên (surveyHabitat). Detail = survey-requirement-missing khi chưa đủ điều kiện.</summary>
        [WireName("survey-habitat")] SurveyHabitat,
        /// <summary>Mở sinh cảnh đã khảo sát (unlockHabitat).</summary>
        [WireName("unlock-habitat")] UnlockHabitat,
        /// <summary>Một yêu cầu nhiệm vụ chưa đạt; Requirement, Current/Target, Candidates gợi ý đối tượng hành động.</summary>
        [WireName("quest-requirement")] QuestRequirement,
        /// <summary>Nhiệm vụ đủ yêu cầu → completeQuest(questId).</summary>
        [WireName("claim-quest")] ClaimQuest,
        /// <summary>Mở thế giới (unlockZone). Ready = false khi vẫn còn điều kiện (các bước trước).</summary>
        [WireName("unlock-zone")] UnlockZone,
    }

    public sealed class JourneyStep
    {
        public JourneyStepCode Code { get; internal set; }
        /// <summary>Nhiệm vụ liên quan (null nếu không có).</summary>
        public string QuestId { get; internal set; }
        /// <summary>Đối tượng: bước TUT_xx, microhabitat, zone, vật liệu hoặc tuyến (tùy Code / Requirement).</summary>
        public string Target { get; internal set; }
        public QuestRequirementKind? Requirement { get; internal set; }
        public int Current { get; internal set; }
        public int Required { get; internal set; }
        /// <summary>
        /// Gợi ý hành động, thứ tự ổn định. ResearchTier / CircadianRecords / SpeciesRecordedInRealm: ID loài; AdultWithLocomotion: UID rồng nên đặt vào sinh cảnh
        /// (Adult trước, rồi rồng non); MaterialDelivery / ExpeditionsClaimed: ID tuyến thám hiểm.
        /// </summary>
        public IReadOnlyList<string> Candidates { get; internal set; }
        /// <summary>Lý do phụ: survey-requirement-missing, need-species (chưa có loài phù hợp → mua gói / lai), route-locked, research-disabled…</summary>
        public string Detail { get; internal set; }
        /// <summary>true = thực hiện được ngay (claim-quest, unlock-zone đủ điều kiện, khảo sát/mở sinh cảnh được).</summary>
        public bool Ready { get; internal set; }
        public string Key => "journey.step." + Code.ToWire();
    }

    public sealed class JourneyDto
    {
        /// <summary>Thế giới kế tiếp trên hành trình (thứ tự catalog), null khi đã mở đủ.</summary>
        public string NextZoneId { get; internal set; }
        public IReadOnlyList<string> UnlockedZones { get; internal set; }
        /// <summary>Bước còn lại theo thứ tự nên làm (điều kiện thế giới → chuỗi nhiệm vụ cổng, tiền đề trước → mở thế giới).</summary>
        public IReadOnlyList<JourneyStep> Steps { get; internal set; }
        /// <summary>Nhiệm vụ phụ nhận được ngay (không thuộc đường chính), thứ tự catalog — để UI nhắc "nhận thưởng".</summary>
        public IReadOnlyList<string> SideQuestsReady { get; internal set; }
    }

    /// <summary>
    /// U2.12 — hướng dẫn hành trình (chỉ đọc, tất định, không đổi luật hay save). Trả lời "tôi cần làm gì để tới thế giới kế tiếp?" bằng đúng các điều kiện
    /// engine sẽ kiểm (<see cref="WorldExpansion.MissingRequirements"/>, <see cref="QuestProposals.Evaluate(GameState, ResearchLog, ResearchPacing, QuestRules)"/>).
    /// Lý do: mô phỏng U2.11 cho thấy người chơi không mở hồ sơ sinh học / không quan sát thì không bao giờ tới Thủy Vực; UI cần chỉ đúng bước còn thiếu.
    /// Hướng dẫn không bao giờ gợi ý vàng hay chờ đồng hồ.
    /// </summary>
    public static class JourneyGuide
    {
        public static JourneyDto Build(GameState s)
        {
            var unlocked = new List<string>();
            foreach (ZoneState z in s.Zones) if (z.Unlocked) unlocked.Add(z.Id);
            ZoneUnlockRule next = null;
            foreach (ZoneUnlockRule r in WorldUnlockCatalog.Rules) if (s.FindZone(r.ZoneId)?.Unlocked == false) { next = r; break; }
            var steps = new List<JourneyStep>();
            var chain = new List<string>();
            if (next != null)
            {
                Tutorial(s, next, steps);
                Habitats(s, next, steps);
                if (next.RequiresQuest != null) Chain(s, next.RequiresQuest, chain, new HashSet<string>(StringComparer.Ordinal));
                foreach (string qid in chain) QuestSteps(s, qid, steps);
                List<string> missing = WorldExpansion.MissingRequirements(s, next.ZoneId);
                steps.Add(new JourneyStep { Code = JourneyStepCode.UnlockZone, Target = next.ZoneId, Ready = missing.Count == 0, Detail = missing.Count == 0 ? null : string.Join(",", missing), Candidates = Array.Empty<string>() });
            }
            var side = new List<string>();
            foreach (QuestStatusView v in QuestProposals.Evaluate(s))
                if (v.CanComplete && !chain.Contains(v.QuestId)) side.Add(v.QuestId);
            return new JourneyDto { NextZoneId = next?.ZoneId, UnlockedZones = unlocked, Steps = steps, SideQuestsReady = side };
        }

        private static void Tutorial(GameState s, ZoneUnlockRule r, List<JourneyStep> steps)
        {
            if (!r.RequiresTutorialCompleted || s.Tutorial.Status == TutorialStatus.Completed) return;
            if (s.Tutorial.Status == TutorialStatus.Skipped)
                steps.Add(new JourneyStep { Code = JourneyStepCode.ResumeTutorial, Ready = true, Candidates = Array.Empty<string>() });
            else
                steps.Add(new JourneyStep { Code = JourneyStepCode.FinishTutorial, Target = Progression.Tutorial.CurrentStep(s.Tutorial), Candidates = Array.Empty<string>() });
        }

        private static void Habitats(GameState s, ZoneUnlockRule r, List<JourneyStep> steps)
        {
            if (!r.RequiresAllOriginHabitats) return;
            ZoneState origin = s.FindZone("origin");
            foreach (string h in Zones.HabitatsOfZone("origin"))
            {
                if (origin != null && origin.UnlockedHabitatIds.Contains(h)) continue;
                HabitatUnlockRule rule = ProgressCatalog.UnlockRuleFor(h);
                if (rule?.Requires != null && !s.Discoveries.Contains(rule.Requires))
                {
                    bool met = Survey.RequirementMet(s, rule.Requires);
                    steps.Add(new JourneyStep { Code = JourneyStepCode.SurveyHabitat, Target = h, Ready = met, Detail = met ? null : "survey-requirement-missing", Candidates = Array.Empty<string>() });
                }
                else steps.Add(new JourneyStep { Code = JourneyStepCode.UnlockHabitat, Target = h, Ready = Zones.CheckUnlock(s, h) == null, Candidates = Array.Empty<string>() });
            }
        }

        /// <summary>Nhiệm vụ chưa nhận của chuỗi tiền đề, tiền đề trước (hậu thứ tự DFS, tiền đề theo thứ tự catalog).</summary>
        private static void Chain(GameState s, string questId, List<string> o, HashSet<string> seen)
        {
            if (!seen.Add(questId) || QuestProposals.IsCompleted(s, questId)) return;
            QuestProposalDef q = QuestProposals.Get(questId);
            if (q == null) return;
            foreach (string p in q.Prerequisites) Chain(s, p, o, seen);
            o.Add(questId);
        }

        private static void QuestSteps(GameState s, string questId, List<JourneyStep> steps)
        {
            QuestProposalDef def = QuestProposals.Get(questId);
            QuestStatusView v = QuestProposals.Evaluate(s, def, s.Research, null, QuestSystem.Rules);
            if (v.CanComplete) { steps.Add(new JourneyStep { Code = JourneyStepCode.ClaimQuest, QuestId = questId, Ready = true, Candidates = Array.Empty<string>() }); return; }
            foreach (QuestRequirementStatus r in v.Requirements)
            {
                if (r.State == RequirementState.Met) continue;
                List<string> cands = Candidates(s, r.Def);
                string detail = r.Detail ?? (cands.Count == 0 && NeedsSpecies(r.Def.Kind) ? "need-species" : null);
                steps.Add(new JourneyStep
                {
                    Code = JourneyStepCode.QuestRequirement, QuestId = questId, Requirement = r.Def.Kind, Target = r.Def.Param, Current = r.Current, Required = r.Target,
                    Candidates = cands, Detail = detail,
                });
            }
        }

        private static bool NeedsSpecies(QuestRequirementKind k) =>
            k == QuestRequirementKind.ResearchTier || k == QuestRequirementKind.AdultWithLocomotion || k == QuestRequirementKind.CircadianRecords || k == QuestRequirementKind.SpeciesRecordedInRealm;

        private static bool Kept(DragonState d) => d.Location.Kind != LocationKind.Released;

        private static bool InRealm(string speciesId, Realm realm)
        {
            foreach (string h in SpeciesCatalog.GetBiology(speciesId).HabitatRules)
                if (HabitatCatalog.TryGet(h, out HabitatProfile p) && p.Realm == realm) return true;
            return false;
        }

        private static List<string> Candidates(GameState s, QuestRequirementDef r)
        {
            var o = new List<string>();
            switch (r.Kind)
            {
                case QuestRequirementKind.ResearchTier:
                    foreach (CodexEntry e in s.Codex)
                    {
                        if (r.Realm.HasValue && !InRealm(e.SpeciesId, r.Realm.Value)) continue;
                        if (ResearchProgress.Species(s, e.SpeciesId, s.Research, ResearchPacing.Production).Tier < r.Tier && OwnsSpecies(s, e.SpeciesId)) o.Add(e.SpeciesId);
                    }
                    break;
                case QuestRequirementKind.CircadianRecords:
                    foreach (CodexEntry e in s.Codex)
                        if (OwnsSpecies(s, e.SpeciesId) && !ResearchProgress.Species(s, e.SpeciesId, s.Research, ResearchPacing.Production).CircadianCovered) o.Add(e.SpeciesId);
                    break;
                case QuestRequirementKind.SpeciesRecordedInRealm:
                {
                    var recorded = new HashSet<string>(StringComparer.Ordinal);
                    if (s.Research != null)
                        foreach (SpeciesResearchRecord rec in s.Research.AllSpecies)
                            foreach (string h in rec.Habitats)
                                if (HabitatCatalog.TryGet(h, out HabitatProfile p) && p.Realm == r.Realm.Value) recorded.Add(rec.SpeciesId);
                    foreach (DragonSpecies sp in SpeciesCatalog.All)
                        if (!recorded.Contains(sp.Id) && OwnsSpecies(s, sp.Id) && InRealmHabitatOpen(s, sp.Id, r.Realm.Value)) o.Add(sp.Id);
                    break;
                }
                case QuestRequirementKind.AdultWithLocomotion:
                {
                    var adults = new List<string>(); var young = new List<string>();
                    foreach (DragonState d in s.Dragons)
                    {
                        if (!Kept(d) || d.Location.Kind == LocationKind.Zone && (!r.Realm.HasValue || RealmOf(d.Location.HabitatId) == r.Realm.Value)) continue;
                        if (!BehaviorCatalog.Has(SpeciesCatalog.GetBiology(d.SpeciesId).Locomotion, r.Locomotion.Value)) continue;
                        if (r.Realm.HasValue && !InRealmHabitatOpen(s, d.SpeciesId, r.Realm.Value)) continue;
                        if (EconomyRates.GrowthStageOf(d.XpSec, SpeciesCatalog.Get(d.SpeciesId)) == GrowthStage.Adult) adults.Add(d.Uid); else young.Add(d.Uid);
                    }
                    // Rồng non đang ở sinh cảnh cũng là ứng viên (chỉ cần lớn).
                    foreach (DragonState d in s.Dragons)
                        if (Kept(d) && d.Location.Kind == LocationKind.Zone && BehaviorCatalog.Has(SpeciesCatalog.GetBiology(d.SpeciesId).Locomotion, r.Locomotion.Value)
                            && (!r.Realm.HasValue || RealmOf(d.Location.HabitatId) == r.Realm.Value) && !young.Contains(d.Uid)) young.Add(d.Uid);
                    o.AddRange(adults); o.AddRange(young);
                    break;
                }
                case QuestRequirementKind.MaterialDelivery:
                    foreach (ExpeditionRoute route in ExpeditionCatalog.Routes)
                        if (BehaviorCatalog.Has(route.Materials, r.Param) && Expedition.Expeditions.RouteOpen(s, route) && route.RewardsDefined) o.Add(route.Id);
                    break;
                case QuestRequirementKind.ExpeditionsClaimed:
                    if (ExpeditionCatalog.TryGet(r.Param, out ExpeditionRoute er) && Expedition.Expeditions.RouteOpen(s, er) && er.RewardsDefined) o.Add(er.Id);
                    break;
            }
            return o;
        }

        private static Realm? RealmOf(string habitatId) => HabitatCatalog.TryGet(habitatId, out HabitatProfile p) ? p.Realm : (Realm?)null;

        private static bool OwnsSpecies(GameState s, string speciesId)
        {
            foreach (DragonState d in s.Dragons) if (Kept(d) && d.SpeciesId == speciesId) return true;
            return false;
        }

        private static bool InRealmHabitatOpen(GameState s, string speciesId, Realm realm)
        {
            foreach (string h in SpeciesCatalog.GetBiology(speciesId).HabitatRules)
            {
                if (!HabitatCatalog.TryGet(h, out HabitatProfile p) || p.Realm != realm) continue;
                ZoneState z = s.FindZone(p.ZoneId);
                if (z != null && z.Unlocked && z.UnlockedHabitatIds.Contains(h)) return true;
            }
            return false;
        }
    }
}
