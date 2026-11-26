using System;
using System.Collections.Generic;
using Dravio.Domain.Behavior;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;

namespace Dravio.Domain.Progression
{
    /// <summary>Một mốc thưởng thám hiểm của một tuyến: nhận chuyến thứ <see cref="Claims"/> thì được thưởng một lần.</summary>
    public sealed class ExplorationMilestoneDef
    {
        internal ExplorationMilestoneDef(string routeId, int claims, int bonusEach, string rewardId)
        {
            RouteId = routeId; Claims = claims; BonusEach = bonusEach; RewardId = rewardId;
        }
        public string RouteId { get; }
        /// <summary>Số chuyến đã nhận (đếm cả chuyến này) để đạt mốc.</summary>
        public int Claims { get; }
        /// <summary>Vật liệu thưởng thêm, mỗi loại vật liệu của tuyến.</summary>
        public int BonusEach { get; }
        /// <summary>Ghi chú Codex / danh hiệu kèm theo (null = chỉ vật liệu).</summary>
        public string RewardId { get; }
    }

    /// <summary>Danh hiệu dẫn xuất (không lưu): khám phá, nghiên cứu, nhiệm vụ, thám hiểm.</summary>
    public sealed class TitleView
    {
        public string TitleId { get; internal set; }
        public string Source { get; internal set; }
        public bool Earned { get; internal set; }
        public int Current { get; internal set; }
        public int Target { get; internal set; }
        public string NameKey => "reward." + TitleId + ".name";
    }

    /// <summary>
    /// U2.11 (ADR 0206): phần thưởng khám phá và thám hiểm.
    /// - Mốc thám hiểm theo tuyến (CHẠY THẬT): chuyến nhận thứ 1 / 5 / 15 / 40 cho thêm 2 / 3 / 4 / 6 đơn vị MỖI vật liệu của tuyến, một lần, kèm ghi chú
    ///   Codex hoặc danh hiệu. Không vàng, không XP. Bộ đếm <c>expeditionsClaimed</c> (Schema v10) nên không nhận lại được.
    /// - Danh hiệu khám phá / nghiên cứu: dẫn xuất từ Codex, thế giới đã mở và sổ nghiên cứu. Không lưu, không đổi gameplay.
    /// - Tuyến <c>sky_cloud</c> vẫn khóa (bảng thưởng "bụi sao" chưa duyệt), nên không có mốc cho tuyến đó.
    /// </summary>
    public static class Exploration
    {
        public static readonly IReadOnlyList<ExplorationMilestoneDef> Milestones = Array.AsReadOnly(new[]
        {
            new ExplorationMilestoneDef("origin_forest", 1, 2, "note.forest_first_trail"),
            new ExplorationMilestoneDef("origin_forest", 5, 3, null),
            new ExplorationMilestoneDef("origin_forest", 15, 4, "title.forest_ranger"),
            new ExplorationMilestoneDef("origin_forest", 40, 6, "title.forest_warden"),
            new ExplorationMilestoneDef("ocean_reef", 1, 2, "note.reef_first_dive"),
            new ExplorationMilestoneDef("ocean_reef", 5, 3, null),
            new ExplorationMilestoneDef("ocean_reef", 15, 4, "title.reef_diver"),
            new ExplorationMilestoneDef("ocean_reef", 40, 6, "title.tide_keeper"),
        });

        public static int Claimed(GameState s, string routeId) =>
            s.ExpeditionsClaimed != null && s.ExpeditionsClaimed.TryGetValue(routeId, out int n) ? n : 0;

        public static ExplorationMilestoneDef NextMilestone(GameState s, string routeId)
        {
            int n = Claimed(s, routeId);
            foreach (ExplorationMilestoneDef m in Milestones) if (m.RouteId == routeId && m.Claims > n) return m;
            return null;
        }

        /// <summary>Gọi trong lệnh claimExpedition, sau khi cộng vật liệu thường: tăng bộ đếm và trao mốc (nếu đạt).</summary>
        public static void OnClaimed(GameState s, string routeId, List<DomainEvent> events)
        {
            if (s.ExpeditionsClaimed == null) s.ExpeditionsClaimed = new SortedDictionary<string, int>(StringComparer.Ordinal);
            int n = Claimed(s, routeId) + 1;
            s.ExpeditionsClaimed[routeId] = n;
            if (!ExpeditionCatalog.TryGet(routeId, out ExpeditionRoute route) || !route.RewardsDefined) return;
            foreach (ExplorationMilestoneDef m in Milestones)
            {
                if (m.RouteId != routeId || m.Claims != n) continue;
                var bonus = new List<KeyValuePair<string, int>>();
                foreach (string mat in ExpeditionCatalog.MaterialIds)
                {
                    if (!BehaviorCatalog.Has(route.Materials, mat) || m.BonusEach <= 0) continue;
                    s.Player.Inventory[mat] = checked((s.Player.Inventory.TryGetValue(mat, out long cur) ? cur : 0) + m.BonusEach);
                    bonus.Add(new KeyValuePair<string, int>(mat, m.BonusEach));
                }
                events.Add(new ExplorationMilestoneReached(routeId, n, bonus, m.RewardId));
            }
        }

        /// <summary>Ghi chú / danh hiệu thám hiểm đã nhận (dẫn xuất từ bộ đếm).</summary>
        public static List<string> EarnedRewardIds(GameState s)
        {
            var o = new List<string>();
            foreach (ExplorationMilestoneDef m in Milestones) if (m.RewardId != null && Claimed(s, m.RouteId) >= m.Claims) o.Add(m.RewardId);
            return o;
        }

        // ---------------- danh hiệu khám phá và nghiên cứu (dẫn xuất) ----------------

        private static TitleView T(string id, string source, int cur, int target) =>
            new TitleView { TitleId = id, Source = source, Current = Math.Min(cur, target), Target = target, Earned = cur >= target };

        /// <summary>Mọi danh hiệu dẫn xuất (kể cả chưa đạt, có tiến độ). Thứ tự cố định.</summary>
        public static List<TitleView> Titles(GameState s, ResearchLog log = null, ResearchPacing pacing = null)
        {
            log = log ?? s.Research;
            pacing = pacing ?? ResearchPacing.Production;
            int species = Codex.SpeciesDiscovered(s), total = SpeciesCatalog.All.Count;
            int zones = 0;
            foreach (ZoneState z in s.Zones) if (z.Unlocked) zones++;
            Dictionary<ResearchTier, int> tiers = ResearchProgress.TierCounts(s, log, pacing);
            int life = tiers.TryGetValue(ResearchTier.LifeCycle, out int l) ? l : 0;
            int etho = tiers.TryGetValue(ResearchTier.Ethology, out int e) ? e : 0;
            var o = new List<TitleView>
            {
                T("title.first_steps", "discovery", species, 4),
                T("title.naturalist", "discovery", species, 8),
                T("title.dragon_scholar", "discovery", species, total),
                T("title.world_walker", "discovery", zones, WorldCatalog.ZoneIds.Count),
                T("title.lifecycle_student", "research", life, 3),
                T("title.lifecycle_scholar", "research", life, 8),
                T("title.lifecycle_master", "research", life, total),
                T("title.field_ethologist", "research", etho, 1),
                T("title.senior_ethologist", "research", etho, 5),
                T("title.grand_ethologist", "research", etho, total),
            };
            foreach (ExplorationMilestoneDef m in Milestones)
                if (m.RewardId != null && m.RewardId.StartsWith("title.", StringComparison.Ordinal)) o.Add(T(m.RewardId, "exploration", Claimed(s, m.RouteId), m.Claims));
            foreach (QuestProposalDef q in QuestProposals.All)
                if (q.Reward == QuestRewardKind.Title) o.Add(T(q.RewardId, "quest", QuestProposals.IsCompleted(s, q.Id) ? 1 : 0, 1));
            return o;
        }
    }

    /// <summary>U2.11: mốc thám hiểm đạt được trong lệnh claimExpedition.</summary>
    public sealed class ExplorationMilestoneReached : DomainEvent
    {
        public ExplorationMilestoneReached(string routeId, int claims, IReadOnlyList<KeyValuePair<string, int>> bonus, string rewardId)
        { RouteId = routeId; Claims = claims; Bonus = bonus; RewardId = rewardId; }
        public override string Type => "explorationMilestoneReached";
        public string RouteId { get; }
        public int Claims { get; }
        public IReadOnlyList<KeyValuePair<string, int>> Bonus { get; }
        public string RewardId { get; }
    }

    /// <summary>U2.11: nhiệm vụ đã nhận hoàn thành (cả cổng lẫn phụ). Nhiệm vụ cổng có thêm event questCompleted như trước.</summary>
    public sealed class QuestRewarded : DomainEvent
    {
        public QuestRewarded(string questId, QuestRewardKind reward, string rewardId, IReadOnlyList<KeyValuePair<string, int>> delivered)
        { QuestId = questId; Reward = reward; RewardId = rewardId; Delivered = delivered; }
        public override string Type => "questRewarded";
        public string QuestId { get; }
        public QuestRewardKind Reward { get; }
        public string RewardId { get; }
        /// <summary>Vật liệu đã giao (đã trừ khỏi kho).</summary>
        public IReadOnlyList<KeyValuePair<string, int>> Delivered { get; }
    }
}
