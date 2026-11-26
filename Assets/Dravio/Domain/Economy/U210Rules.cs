using System.Collections.Generic;
using Dravio.Domain.Behavior;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;

namespace Dravio.Domain.Economy
{
    /// <summary>U2.10 E1 (ADR 0204, đã duyệt): tối đa <see cref="PackCatalog.StarterDailyLimit"/> gói Starter mua bằng vàng mỗi ngày UTC.</summary>
    public static class PackDailyLimit
    {
        public static int StarterBoughtToday(GameState s, long nowMs) =>
            s.PackDaily != null && s.PackDaily.Day == JsCompat.UtcDay(nowMs) ? s.PackDaily.StarterBought : 0;

        /// <summary>Số gói Starter còn mua được hôm nay (UTC).</summary>
        public static int StarterLeftToday(GameState s, long nowMs) => System.Math.Max(0, PackCatalog.StarterDailyLimit - StarterBoughtToday(s, nowMs));

        public static DomainErrorCode? Check(GameState s, string packId, long nowMs) =>
            packId == "starter" && StarterLeftToday(s, nowMs) <= 0 ? DomainErrorCode.PackDailyLimit : (DomainErrorCode?)null;

        public static void Record(GameState s, string packId, long nowMs)
        {
            if (packId != "starter") return;
            string day = JsCompat.UtcDay(nowMs);
            int n = StarterBoughtToday(s, nowMs);
            s.PackDaily = new PackDailyState { Day = day, StarterBought = n + 1 };
        }
    }

    /// <summary>U2.10 E5 (ADR 0204, đã duyệt): dự án Sanctuary. Tự nguyện, chỉ thẩm mỹ, không tăng sản xuất. U2.13: vật liệu theo thế giới (ADR 0208).</summary>
    public static class SanctuaryProjects
    {
        public static DomainErrorCode? Check(GameState s)
        {
            int n = s.SanctuaryProjectsBuilt;
            long? cost = SanctuaryProjectCatalog.CostG(n);
            if (cost == null) return DomainErrorCode.ProjectsMaxed;
            if (s.Player.Gold < MicroGold.FromWholeGold(cost.Value)) return DomainErrorCode.NotEnoughGold;
            ProjectMaterials mats = SanctuaryProjectCatalog.Materials(s, n);
            foreach (string m in mats.Ids)
                if ((s.Player.Inventory.TryGetValue(m, out long have) ? have : 0) < mats.Each) return DomainErrorCode.NotEnoughMaterials;
            return null;
        }

        public static DomainErrorCode? Apply(GameState s, List<DomainEvent> events)
        {
            DomainErrorCode? err = Check(s);
            if (err != null) return err;
            int n = s.SanctuaryProjectsBuilt;
            MicroGold cost = MicroGold.FromWholeGold(SanctuaryProjectCatalog.CostG(n).Value);
            ProjectMaterials mats = SanctuaryProjectCatalog.Materials(s, n);
            s.Player.Gold = s.Player.Gold - cost;
            if (mats.Each > 0) foreach (string m in mats.Ids) s.Player.Inventory[m] -= mats.Each;
            s.SanctuaryProjectsBuilt = n + 1;
            events.Add(new SanctuaryProjectBuilt(n, cost.Micro, mats.Each, mats.Ids));
            return null;
        }
    }

    /// <summary>U2.10 R9 + R11 (ADR 0204, đã duyệt): lệnh quan sát và nhật ký đêm trên sổ nghiên cứu của save (Schema v9).</summary>
    public static class ResearchRules210
    {
        public static ResearchPacing Pacing => ResearchPacing.Production;

        public static DomainErrorCode? Observe(GameState s, string uid, DayPhase phase, long nowMs, List<DomainEvent> events)
        {
            DragonState d = s.FindDragon(uid);
            if (d == null || d.Location.Kind == LocationKind.Released) return DomainErrorCode.UnknownDragon;
            BehaviorDecision decision = BehaviorEngine.Decide(s, uid, nowMs, phase);
            ResearchObservationResult r = s.Research.Observe(s, decision, nowMs, Pacing);
            if (!r.Recorded) return r.Reason == "observation-cooldown" ? DomainErrorCode.ObservationCooldown : DomainErrorCode.NotObservable;
            events.Add(new DragonObserved(uid, d.SpeciesId, r.Findings, r.DeferredByDailyLimit));
            return null;
        }

        /// <summary>Gọi sau quyết toán offline [fromMs, toMs]: nếu khoảng offline qua 02:00 UTC thì ghi nhật ký đêm.</summary>
        public static void AfterOffline(GameState s, long fromMs, long toMs, List<DomainEvent> events)
        {
            if (!Pacing.Enabled || !Pacing.NightJournal) return;
            long? sample = ResearchLog.LastNightSampleMs(fromMs, toMs);
            if (sample == null) return;
            List<ResearchFinding> f = s.Research.WriteNightJournal(s, sample.Value, toMs, Pacing, out List<string> species);
            if (f.Count > 0) events.Add(new NightJournalWritten(sample.Value, f, species));
        }

        /// <summary>Sau mỗi lệnh thành công: bỏ ghi chép cá thể đã bán / thả, để save luôn hợp lệ.</summary>
        public static void Prune(GameState s)
        {
            if (s.Research != null && !s.Research.IsEmpty) s.Research.PruneIndividuals(s);
        }
    }
}
