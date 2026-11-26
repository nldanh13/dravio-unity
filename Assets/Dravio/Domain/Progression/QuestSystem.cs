using System;
using System.Collections.Generic;
using Dravio.Domain.Core;
using Dravio.Domain.World;

namespace Dravio.Domain.Progression
{
    /// <summary>
    /// U2.11 (ADR 0206): hoàn thành nhiệm vụ thật (lệnh completeQuest, đề xuất Q7 của ADR 0204).
    /// - Chỉ hoàn thành khi <see cref="QuestProposals.Evaluate(GameState, QuestProposalDef, Behavior.ResearchLog, Behavior.ResearchPacing, QuestRules)"/> báo CanComplete.
    /// - Vật liệu yêu cầu "giao" bị trừ đúng một lần, cùng transaction.
    /// - Nhiệm vụ cổng đi qua <see cref="WorldExpansion.ApplyQuestComplete"/> (completedQuests, event questCompleted). Thế giới KHÔNG tự mở: người chơi vẫn gửi
    ///   unlockZone và lệnh đó vẫn kiểm đủ điều kiện ADR 0201.
    /// - Nhiệm vụ phụ ghi vào completedSideQuests (thứ tự catalog).
    /// - Thưởng không lạm phát: ghi chú Codex, mẫu trang trí, danh hiệu. Không vàng, không XP, không bond, không tiền tệ mới. Thưởng dẫn xuất từ nhiệm vụ
    ///   đã hoàn thành (<see cref="Rewards"/>), không lưu riêng, nên không thể nhận hai lần.
    /// </summary>
    public static class QuestSystem
    {
        public static QuestRules Rules => QuestRules.Production;

        /// <summary>Kiểm (không đổi state). Thứ tự lỗi: unknown-quest → quest-completed → quest-locked → quest-not-ready.</summary>
        public static DomainErrorCode? Check(GameState s, string questId, out string detail)
        {
            detail = null;
            QuestProposalDef q = QuestProposals.Get(questId);
            if (q == null) return DomainErrorCode.UnknownQuest;
            if (QuestProposals.IsCompleted(s, questId)) return DomainErrorCode.QuestAlreadyCompleted;
            QuestStatusView v = QuestProposals.Evaluate(s, q, s.Research, null, Rules);
            if (v.State == QuestState.Locked) { detail = string.Join(",", v.LockedBy); return DomainErrorCode.QuestLocked; }
            if (!v.CanComplete)
            {
                var missing = new List<string>();
                foreach (QuestRequirementStatus r in v.Requirements)
                    if (r.State != RequirementState.Met) missing.Add(r.Def.Kind.ToWire() + (r.Detail != null ? ":" + r.Detail : ""));
                detail = string.Join(",", missing);
                return DomainErrorCode.QuestNotReady;
            }
            return null;
        }

        public static DomainErrorCode? Complete(GameState s, string questId, List<DomainEvent> events)
        {
            DomainErrorCode? err = Check(s, questId, out _);
            if (err != null) return err;
            QuestProposalDef q = QuestProposals.Get(questId);
            var delivered = new List<KeyValuePair<string, int>>();
            foreach (QuestRequirementDef r in q.Requirements)
            {
                if (r.Kind != QuestRequirementKind.MaterialDelivery) continue;
                s.Player.Inventory[r.Param] = s.Player.Inventory[r.Param] - r.Target;
                delivered.Add(new KeyValuePair<string, int>(r.Param, r.Target));
            }
            if (q.IsCanonicalGate)
            {
                DomainErrorCode? e = WorldExpansion.ApplyQuestComplete(s, questId, events);
                if (e != null) return e;
            }
            else
            {
                var ordered = new List<string>();
                foreach (QuestProposalDef d in QuestProposals.All)
                    if (!d.IsCanonicalGate && (d.Id == questId || s.CompletedSideQuests.Contains(d.Id))) ordered.Add(d.Id);
                s.CompletedSideQuests = ordered;
            }
            events.Add(new QuestRewarded(questId, q.Reward, q.RewardId, delivered));
            return null;
        }

        /// <summary>Phần thưởng đã nhận (thứ tự catalog). Gồm cả zone.* của nhiệm vụ cổng.</summary>
        public static List<QuestProposalDef> Rewards(GameState s)
        {
            var o = new List<QuestProposalDef>();
            foreach (QuestProposalDef q in QuestProposals.All) if (QuestProposals.IsCompleted(s, q.Id)) o.Add(q);
            return o;
        }

        public static bool HasReward(GameState s, string rewardId)
        {
            foreach (QuestProposalDef q in QuestProposals.All) if (q.RewardId == rewardId) return QuestProposals.IsCompleted(s, q.Id);
            return false;
        }
    }
}
