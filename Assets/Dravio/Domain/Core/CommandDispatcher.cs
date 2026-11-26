using System;
using System.Collections.Generic;
using Dravio.Domain.Biology;
using Dravio.Domain.Breeding;
using Dravio.Domain.Expedition;
using Dravio.Domain.Catalog;
using Dravio.Domain.Economy;
using Dravio.Domain.Health;
using Dravio.Domain.Progression;
using Dravio.Domain.Sanctuary;
using Dravio.Domain.Text;
using Dravio.Domain.World;

namespace Dravio.Domain.Core
{
    /// <summary>
    /// Bước đánh giá chạy sau mỗi lệnh thành công, trên state làm việc. Dispatch luôn chạy evaluator có sẵn
    /// (thành tựu rồi hướng dẫn, như evaluateAchievements → evaluateTutorial của TS, U2.4); evaluator truyền thêm chạy sau đó.
    /// </summary>
    public interface IPostCommandEvaluator
    {
        void Evaluate(GameState working, List<DomainEvent> events);
    }

    /// <summary>
    /// Command bus thuần (GDD Phụ lục C.3; src/engine/core/commands.ts). Điểm vào duy nhất để đổi GameState.
    /// - Lệnh sai hình dạng → invalid-command, trả lại chính object state đầu vào.
    /// - requestId đã xử lý → state hiện tại, Duplicate = true, không event.
    /// - Thành công → bản sao mới, revision + 1, ghi requestId (giữ 200 gần nhất).
    /// - Lệnh bị từ chối không đổi state, không tiêu RNG, không tiêu vàng.
    /// </summary>
    public static class CommandDispatcher
    {
        [ThreadStatic] private static DisasterRules _disasterRules;

        /// <summary>Luật thiên tai của lệnh đang xử lý (mặc định <see cref="DisasterRules.Production"/>: thiên tai tự nhiên tắt, ADR 0202).</summary>
        internal static DisasterRules Disasters => _disasterRules ?? DisasterRules.Production;

        public static DispatchResult Dispatch(GameState state, GameCommand command, IClock clock, IReadOnlyList<IPostCommandEvaluator> postEvaluators = null) =>
            Dispatch(state, command, clock, postEvaluators, null);

        /// <summary>
        /// Như trên, với bộ luật thiên tai tùy chọn (test, bản cân bằng). null = <see cref="DisasterRules.Production"/>. Cùng state + lệnh + đồng hồ + luật
        /// luôn cho cùng kết quả.
        /// </summary>
        public static DispatchResult Dispatch(GameState state, GameCommand command, IClock clock, IReadOnlyList<IPostCommandEvaluator> postEvaluators, DisasterRules disasterRules)
        {
            DisasterRules previous = _disasterRules;
            _disasterRules = disasterRules;
            try { return DispatchCore(state, command, clock, postEvaluators); }
            finally { _disasterRules = previous; }
        }

        private static DispatchResult DispatchCore(GameState state, GameCommand command, IClock clock, IReadOnlyList<IPostCommandEvaluator> postEvaluators)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (clock == null) throw new ArgumentNullException(nameof(clock));
            if (command == null || !GameCommand.IsId(command.RequestId) || !command.IsWellFormed())
                return DispatchResult.Failure(state, new DomainError(DomainErrorCode.InvalidCommand));
            if (state.ProcessedRequestIds.Contains(command.RequestId))
                return DispatchResult.Success(state, Array.Empty<DomainEvent>(), duplicate: true);

            // Đồng hồ lùi không được dùng để né cooldown: thời điểm hiệu lực không nhỏ hơn high-water mark.
            long nowMs = Math.Max(clock.NowMs(), state.Clock.HighWaterAtMs);
            GameState working = state.DeepClone();
            var events = new List<DomainEvent>();
            DomainError error = Handle(working, command, nowMs, events);
            if (error != null) return DispatchResult.Failure(state, error);

            // U2.10: sổ nghiên cứu chỉ giữ cá thể còn trong kho.
            ResearchRules210.Prune(working);
            // U2.11: quan hệ chỉ giữ cặp còn trong kho.
            if (working.Relations != null && !working.Relations.IsEmpty) working.Relations.Prune(working);
            // Sau mỗi lệnh thành công: thành tựu rồi hướng dẫn, đều đọc state (không đọc thao tác UI).
            BuiltInProgressEvaluator.Instance.Evaluate(working, events);
            if (postEvaluators != null)
                foreach (IPostCommandEvaluator e in postEvaluators) e.Evaluate(working, events);

            working.Revision = state.Revision + 1;
            working.ProcessedRequestIds.Add(command.RequestId);
            int excess = working.ProcessedRequestIds.Count - CareRules.ProcessedRequestLimit;
            if (excess > 0) working.ProcessedRequestIds.RemoveRange(0, excess);
            return DispatchResult.Success(working, events, duplicate: false);
        }

        private static DomainError Handle(GameState s, GameCommand cmd, long nowMs, List<DomainEvent> events)
        {
            switch (cmd)
            {
                case AdvanceTimeCommand c:
                    SettleTime(s, nowMs, c.Mode, events);
                    return null;
                case FeedCommand c: return Feed(s, c, nowMs, events);
                case ConsumeFoodCommand c: return ConsumeFood(s, c, nowMs, events);
                case PetCommand c: return Pet(s, c, nowMs, events);
                case RenameDragonCommand c: return Rename(s, c, events);
                case MoveDragonCommand c: return MoveOrProduction(s, c.DragonUid, nowMs, events, d => Zones.CheckMove(s, d, c.Target), d => Zones.ApplyMove(s, d, c.Target, events));
                case SetProductionSlotCommand c: return MoveOrProduction(s, c.DragonUid, nowMs, events, d => Zones.CheckProduction(s, d, c.Enabled), d => Zones.ApplyProduction(s, d, c.Enabled, events));
                // Mở chỗ không đổi vị trí rồng nào nên không cần quyết toán thời gian; vàng giữ nguyên.
                case UnlockHabitatCommand c: return ToError(Zones.ApplyUnlock(s, c.HabitatId, events));
                case SurveyHabitatCommand c: return ToError(Survey.Apply(s, c.HabitatId, events));
                case ViewBiologyProfileCommand c: return ToError(BiologyProfile.Apply(s, c.SpeciesId, events));
                case SetTutorialStatusCommand c: return ToError(Tutorial.SetStatus(s, c.Status, nowMs, events));
                // Mua: kiểm trước, quyết toán thời gian để số dư và XP nhất quán tại thời điểm mua, rồi trừ vàng.
                case BuyTutorialPackCommand _: return SettleThen(s, nowMs, events, () => StarterPack.CheckBuy(s), () => StarterPack.ApplyBuy(s, nowMs, events));
                // Nhận: rồng mới có thể vào sinh cảnh; quyết toán trước để vàng cũ tính theo đội hình cũ.
                case ClaimPackCommand c: return SettleThen(s, nowMs, events, () => StarterPack.CheckClaim(s, c.Target, nowMs), () => StarterPack.ApplyClaim(s, c.Target, nowMs, events));
                // ---- U2.5 (implementation mới, GDD/ADR 0003): mọi giao dịch kiểm trước, quyết toán online tới now, rồi áp dụng ----
                // U2.10 E1: giới hạn Starter theo ngày UTC kiểm sau các điều kiện cũ (thứ tự lỗi cũ không đổi), ghi bộ đếm cùng transaction.
                case BuyPackCommand c: return SettleThen(s, nowMs, events, () => Hatchery.CheckBuyPack(s, c.PackId) ?? PackDailyLimit.Check(s, c.PackId, nowMs), () =>
                {
                    DomainErrorCode? e = Hatchery.ApplyBuyPack(s, c.PackId, nowMs, events);
                    if (e == null) PackDailyLimit.Record(s, c.PackId, nowMs);
                    return e;
                });
                // ---- U2.10 (ADR 0204, đã duyệt E5, R9, R11) ----
                case BuildSanctuaryProjectCommand _: return SettleThen(s, nowMs, events, () => SanctuaryProjects.Check(s), () => SanctuaryProjects.Apply(s, events));
                case ObserveDragonCommand c: return SettleThen(s, nowMs, events, () => null, () => ResearchRules210.Observe(s, c.DragonUid, c.Phase, nowMs, events));
                // ---- U2.11 (ADR 0206): nhiệm vụ thật. Quyết toán trước (rồng có thể vừa thành Adult), rồi kiểm và nhận; bị từ chối thì không đổi gì ----
                case CompleteQuestCommand c:
                {
                    if (QuestProposals.Get(c.QuestId) == null) return new DomainError(DomainErrorCode.UnknownQuest);
                    SettleTime(s, nowMs, AdvanceMode.Online, events);
                    DomainErrorCode? pre = QuestSystem.Check(s, c.QuestId, out string why);
                    if (pre != null) return new DomainError(pre.Value, why);
                    return ToError(QuestSystem.Complete(s, c.QuestId, events));
                }
                case StartBreedCommand c: return SettleThen(s, nowMs, events, () => Hatchery.CheckStartBreed(s, c.ParentA, c.ParentB, nowMs), () => Hatchery.ApplyStartBreed(s, c.ParentA, c.ParentB, nowMs, events));
                case TapEggCommand _: return SettleThen(s, nowMs, events, () => Hatchery.CheckTap(s, nowMs), () => Hatchery.ApplyTap(s, nowMs, events));
                case ClaimBreedCommand c: return SettleThen(s, nowMs, events, () => Hatchery.CheckClaimBreed(s, c.Target, nowMs), () => Hatchery.ApplyClaimBreed(s, c.Target, nowMs, events));
                // ---- U2.7 (implementation mới): giao dịch vàng quyết toán online tới now trước (giá bán theo stage hiện tại) ----
                case SellDragonCommand c: return SettleThen(s, nowMs, events, () => SanctuaryRules.CheckSell(s, c.DragonUid), () => SanctuaryRules.ApplySell(s, c.DragonUid, events));
                case BulkSellCommand c: return SettleThen(s, nowMs, events, () => null, () => SanctuaryRules.ApplyBulkSell(s, c.ExpectedTotalGoldMicro, events));
                case LockDragonCommand c: return ToError(SanctuaryRules.ApplyLock(s, c.DragonUid, c.Locked, events));
                case BuyUpgradeCommand c: return SettleThen(s, nowMs, events, () => SanctuaryRules.CheckUpgrade(s, c.UpgradeId), () => SanctuaryRules.ApplyUpgrade(s, c.UpgradeId, events));
                case BuyDecorationCommand c: return SettleThen(s, nowMs, events, () => SanctuaryRules.CheckBuyDecoration(s, c.ZoneId, c.DecorationId), () => SanctuaryRules.ApplyBuyDecoration(s, c.ZoneId, c.DecorationId, events));
                // ---- U2.8 (implementation mới): thám hiểm — kiểm, quyết toán online (rồng ngừng sản xuất đúng lúc đi), rồi áp dụng ----
                case StartExpeditionCommand c: return SettleThen(s, nowMs, events, () => Expeditions.CheckStart(s, c.RouteId, c.DragonUid, out _), () => Expeditions.ApplyStart(s, c.RouteId, c.DragonUid, nowMs, events));
                // Mở thế giới không đổi vị trí rồng nên không cần quyết toán thời gian.
                case UnlockZoneCommand c: return ToError(WorldExpansion.ApplyZoneUnlockCommand(s, c.ZoneId, nowMs, events));
                // U2.11: sau khi cộng vật liệu, đếm chuyến theo tuyến và trao mốc thưởng thám hiểm (một lần).
                case ClaimExpeditionCommand _: return SettleThen(s, nowMs, events, () => Expeditions.CheckClaim(s, nowMs), () =>
                {
                    string route = s.Expedition?.RouteId;
                    DomainErrorCode? e = Expeditions.ApplyClaim(s, nowMs, events);
                    if (e == null && route != null) Exploration.OnClaimed(s, route, events);
                    return e;
                });
                case ToggleDecorationCommand c: return ToError(SanctuaryRules.ApplyToggleDecoration(s, c.ZoneId, c.DecorationId, c.Active, events));
                // ---- U2.6 (implementation mới): trú ẩn và chăm sóc — kiểm, quyết toán online tới now (tiến trình ca, sản xuất), rồi áp dụng ----
                case MoveToShelterCommand c: return MoveOrProduction(s, c.DragonUid, nowMs, events, d => DisasterEngine.CheckMoveToShelter(s, d), d => DisasterEngine.ApplyMoveToShelter(s, d, events));
                case AssessCaseCommand c: return SettleThen(s, nowMs, events, () => CareCenter.CheckAssess(s, c.CaseId), () => CareCenter.ApplyAssess(s, c.CaseId, events));
                case SetCarePlanCommand c:
                {
                    DomainErrorCode? pre = CareCenter.CheckSetPlan(s, c.CaseId, c.PlanId, out string why);
                    if (pre != null) return new DomainError(pre.Value, why);
                    return SettleThen(s, nowMs, events, () => null, () => CareCenter.ApplySetPlan(s, c.CaseId, c.PlanId, events));
                }
                case StartCareCommand c: return SettleThen(s, nowMs, events, () => CareCenter.CheckStart(s, c.CaseId), () => CareCenter.ApplyStart(s, c.CaseId, nowMs, events));
                case ReviewCaseCommand c:
                    // Kiểm sau khi quyết toán: ca có thể vừa đủ thời lượng tại now. Lệnh bị từ chối vẫn không đổi state (dispatcher bỏ bản sao).
                    return SettleThen(s, nowMs, events, () => s.FindCase(c.CaseId) == null ? DomainErrorCode.UnknownCase : (DomainErrorCode?)null, () => CareCenter.ApplyReview(s, c.CaseId, events));
                case DischargeDragonCommand c: return SettleThen(s, nowMs, events, () => s.FindCase(c.CaseId) == null ? DomainErrorCode.UnknownCase : (DomainErrorCode?)null, () => CareCenter.ApplyDischarge(s, c.CaseId, c.Target, events));
                default:
                    // Không có kiểu lệnh tự khai báo hoàn thành/khám phá (recordDiscovery, completeTutorial, grantHabitatUnlock…).
                    return new DomainError(DomainErrorCode.InvalidCommand, "unsupported command type " + cmd.Type);
            }
        }

        private static DomainError ToError(DomainErrorCode? code) => code.HasValue ? new DomainError(code.Value) : null;

        /// <summary>
        /// moveDragon / setProductionSlot (ADR 0008 mục 6): kiểm trên state hiện tại trước để lệnh bị từ chối không đổi gì;
        /// rồi quyết toán thời gian online tới lúc đổi chỗ để vàng trước đó tính theo vị trí/ngân sách cũ; rồi mới đổi.
        /// </summary>
        private static DomainError MoveOrProduction(GameState s, string uid, long nowMs, List<DomainEvent> events,
            Func<DragonState, DomainErrorCode?> check, Func<DragonState, DomainErrorCode?> apply)
        {
            DragonState dragon = s.FindDragon(uid);
            if (dragon == null) return new DomainError(DomainErrorCode.UnknownDragon);
            DomainError pre = ToError(check(dragon));
            if (pre != null) return pre;
            SettleTime(s, nowMs, AdvanceMode.Online, events);
            return ToError(apply(dragon));
        }

        private static DomainError SettleThen(GameState s, long nowMs, List<DomainEvent> events, Func<DomainErrorCode?> check, Func<DomainErrorCode?> apply)
        {
            DomainError pre = ToError(check());
            if (pre != null) return pre;
            SettleTime(s, nowMs, AdvanceMode.Online, events);
            return ToError(apply());
        }

        /// <summary>Xóa thức ăn đã hết hạn tại thời điểm nowMs.</summary>
        private static void ExpireFood(GameState s, long nowMs, List<DomainEvent> events)
        {
            List<FoodDrop> drops = s.Food.Drops;
            for (int i = 0; i < drops.Count; i++)
                if (drops[i].ExpiresAtMs <= nowMs) events.Add(new FoodExpired(drops[i].Id));
            drops.RemoveAll(f => f.ExpiresAtMs <= nowMs);
        }

        /// <summary>
        /// Tiến thời gian tới nowMs: XP 100 % cho mọi rồng chưa thả, vàng theo mốc stage cho rồng sản xuất
        /// (online 100 %, offline 75 %), elapsed chặn 24h, đồng hồ lùi giữ high-water mark. Không sinh thiên tai.
        /// </summary>
        internal static void SettleTime(GameState s, long nowMs, AdvanceMode mode, List<DomainEvent> events)
        {
            // U2.6: thiên tai chỉ tiến khi World foreground online (GDD tr. 9; ADR 0003). Offline / Zen / Companion: tự đưa rồng vào nơi trú
            // nếu đang có cảnh báo, không tích dự báo, không sinh sự kiện hay tổn thương mới.
            DisasterRules rules = Disasters;
            bool worldOnline = mode == AdvanceMode.Online && s.Player.Settings.Mode == AppMode.World;
            if (!worldOnline) DisasterEngine.AutoShelter(s, events);
            bool hazards = rules.Enabled && worldOnline;
            if (hazards)
            {
                // Tách quyết toán đúng tại mỗi mốc (kiểm hazard / hết dự báo), để vàng và vị trí trước mốc tính theo đội hình cũ — giống hệt tick 1 Hz.
                while (true)
                {
                    long raw = Math.Max(nowMs, s.Clock.HighWaterAtMs) - s.Clock.LastProcessedAtMs;
                    if (raw <= 0 || raw > TimeRules.OfflineCapMs) break;
                    long? offset = DisasterEngine.NextMilestoneOffset(s, rules);
                    if (offset == null || offset.Value >= raw) break;
                    SettleCore(s, s.Clock.LastProcessedAtMs + offset.Value, mode, events, true);
                    DisasterEngine.ProcessMilestones(s, rules, s.Clock.LastProcessedAtMs, events);
                }
            }
            long offlineFrom = s.Clock.LastProcessedAtMs;
            SettleCore(s, nowMs, mode, events, hazards);
            if (hazards) DisasterEngine.ProcessMilestones(s, rules, s.Clock.LastProcessedAtMs, events);
            // U2.10 R11: nhật ký đêm khi người chơi quay lại sau một đêm offline.
            if (mode == AdvanceMode.Offline) ResearchRules210.AfterOffline(s, offlineFrom, s.Clock.LastProcessedAtMs, events);
        }

        private static void SettleCore(GameState s, long nowMs, AdvanceMode mode, List<DomainEvent> events, bool accrueHazard)
        {
            ElapsedResult er = TimeRules.ComputeElapsed(s.Clock, nowMs);
            long elapsedMs = er.ElapsedMs;
            ClockState next = er.Next;
            var local = new List<DomainEvent>();
            long remainder = s.Player.GoldRemainder;
            long gold = 0;
            var matured = new List<string>();
            long fromMs = s.Clock.LastProcessedAtMs;
            // U2.11: độ quen theo thời gian sống chung (chỉ trình diễn), tính theo vị trí trước khi đổi chỗ — không ảnh hưởng vàng / XP.
            s.Relations?.Accrue(s, elapsedMs);
            // U2.5: tổ ấp xong trong khoảng này thì bố mẹ (đang "breeding", không sản xuất) được giải phóng đúng tại finishAtMs:
            // phần trước mốc không sản xuất, phần sau mốc sản xuất theo vị trí. Thời gian mô phỏng có thể bị chặn 24 h nên mốc
            // được kẹp vào [0, elapsedMs].
            // U2.8: thám hiểm xong trong khoảng này cũng giải phóng rồng đúng tại finishAtMs (cùng cách tách đôi). Một rồng chỉ ở một job.
            long? nestDoneAt = Hatchery.CompletesBy(s, next.LastProcessedAtMs);
            long? expDoneAt = Expeditions.CompletesBy(s, next.LastProcessedAtMs);
            long Pre(long? at) => at.HasValue ? Math.Max(0, Math.Min(elapsedMs, at.Value - fromMs)) : 0;
            var releasedParents = new List<DragonState>();
            foreach (DragonState d in s.Dragons)
            {
                if (d.Location.Kind == LocationKind.Released || elapsedMs == 0) continue;
                bool nestSplit = nestDoneAt.HasValue && Hatchery.IsNestParent(s, d);
                bool expSplit = expDoneAt.HasValue && Expeditions.IsTraveller(s, d);
                bool split = nestSplit || expSplit;
                long firstMs = nestSplit ? Pre(nestDoneAt) : expSplit ? Pre(expDoneAt) : elapsedMs;
                GrowthResult r = Lifecycle.AdvanceDragon(d, firstMs, Placement.IsProducing(s, d), mode, remainder);
                remainder = r.Remainder;
                gold = checked(gold + r.GoldMicro);
                var changes = new List<StageChange>(r.StageChanges);
                d.XpSec = r.XpMs / 1000.0;
                if (split)
                {
                    d.Activity = DragonActivity.Idle;
                    releasedParents.Add(d);
                    GrowthResult r2 = Lifecycle.AdvanceDragon(d, elapsedMs - firstMs, Placement.IsProducing(s, d), mode, remainder);
                    remainder = r2.Remainder;
                    gold = checked(gold + r2.GoldMicro);
                    changes.AddRange(r2.StageChanges);
                    d.XpSec = r2.XpMs / 1000.0;
                }
                foreach (StageChange c in changes)
                {
                    local.Add(new StageChanged(d.Uid, c.From, c.To));
                    if (c.To == GrowthStage.Adult) matured.Add(d.Uid);
                }
            }
            // Trả activity để Complete tự giải phóng (và ghi cooldown) ở một chỗ duy nhất.
            foreach (DragonState d in releasedParents) d.Activity = Expeditions.IsTraveller(s, d) ? DragonActivity.Expedition : DragonActivity.Breeding;
            s.Player.Gold = s.Player.Gold + MicroGold.FromMicro(gold);
            s.Player.GoldRemainder = remainder;
            s.Player.TotalGoldEarned = s.Player.TotalGoldEarned + MicroGold.FromMicro(gold);
            s.Clock = next;
            events.Add(new TimeAdvanced(fromMs, next.LastProcessedAtMs, elapsedMs, mode, gold));
            events.AddRange(local);
            // Event hoàn tất theo thứ tự thời gian (tổ trước khi cùng mốc).
            if (nestDoneAt.HasValue && (!expDoneAt.HasValue || nestDoneAt.Value <= expDoneAt.Value)) { Hatchery.Complete(s, nestDoneAt.Value, events); nestDoneAt = null; }
            if (expDoneAt.HasValue) Expeditions.Complete(s, expDoneAt.Value, events);
            if (nestDoneAt.HasValue) Hatchery.Complete(s, nestDoneAt.Value, events);
            if (mode == AdvanceMode.Offline && elapsedMs > CareRules.OfflineReceiptMinMs)
            {
                string id = "offline-" + next.LastProcessedAtMs.ToString(System.Globalization.CultureInfo.InvariantCulture);
                s.OfflineReceipt = new OfflineReceipt
                {
                    Id = id,
                    FromMs = next.LastProcessedAtMs - elapsedMs,
                    ToMs = next.LastProcessedAtMs,
                    GoldMicro = gold,
                    MaturedIds = matured,
                    BonusClaimed = false,
                    CreatedAtMs = next.LastProcessedAtMs,
                };
                events.Add(new OfflineReceiptCreated(id, gold, elapsedMs));
            }
            ExpireFood(s, next.LastProcessedAtMs, events);
            // U2.6: tiến trình chăm sóc theo mốc thời gian tuyệt đối (như tổ ấp / thám hiểm); đồng hồ hazard tích phần World foreground.
            CareCenter.Progress(s, next.LastProcessedAtMs, events);
            if (accrueHazard) DisasterEngine.Accrue(s, elapsedMs);
        }

        private static DomainError Feed(GameState s, FeedCommand c, long nowMs, List<DomainEvent> events)
        {
            ZoneState zone = s.FindZone(c.ZoneId);
            if (zone == null || !zone.Unlocked) return new DomainError(DomainErrorCode.ZoneLocked);
            if (HabitatCatalog.ZoneOf(c.HabitatId) != c.ZoneId || !zone.UnlockedHabitatIds.Contains(c.HabitatId))
                return new DomainError(DomainErrorCode.HabitatLocked);
            ExpireFood(s, nowMs, events);
            long? last = s.Food.LastSpawnAtMs;
            if (last.HasValue && nowMs - last.Value < CareRules.FoodSpawnIntervalMs) return new DomainError(DomainErrorCode.FoodTooFast);
            if (s.Food.Drops.Count >= CareRules.FoodMaxBase + s.Player.Upgrades.FoodCooldownLevel) return new DomainError(DomainErrorCode.FoodLimit);
            var drop = new FoodDrop
            {
                Id = "food-" + c.RequestId,
                DietId = c.DietId,
                ZoneId = c.ZoneId,
                HabitatId = c.HabitatId,
                SpawnedAtMs = nowMs,
                ExpiresAtMs = nowMs + CareRules.FoodTtlMs,
            };
            s.Food.LastSpawnAtMs = nowMs;
            s.Food.Drops.Add(drop);
            s.Player.Stats.TotalFeedClicks += 1;
            events.Add(new FoodSpawned(drop.Id, drop.DietId, drop.ZoneId, drop.HabitatId, drop.ExpiresAtMs));
            return null;
        }

        private static DomainError ConsumeFood(GameState s, ConsumeFoodCommand c, long nowMs, List<DomainEvent> events)
        {
            DragonState dragon = s.FindDragon(c.DragonUid);
            if (dragon == null) return new DomainError(DomainErrorCode.UnknownDragon);
            FoodDrop drop = s.Food.Drops.Find(f => f.Id == c.FoodId);
            if (drop == null) return new DomainError(DomainErrorCode.UnknownFood);
            if (drop.ExpiresAtMs <= nowMs) return new DomainError(DomainErrorCode.FoodExpired);
            if (dragon.Location.Kind != LocationKind.Zone || dragon.Activity != DragonActivity.Idle) return new DomainError(DomainErrorCode.DragonUnavailable);
            if (dragon.Location.ZoneId != drop.ZoneId || dragon.Location.HabitatId != drop.HabitatId) return new DomainError(DomainErrorCode.WrongPlace);
            bool eats = false;
            foreach (DietId d in SpeciesCatalog.Get(dragon.SpeciesId).Biology.DietIds) if (d == drop.DietId) eats = true;
            if (!eats) return new DomainError(DomainErrorCode.WrongDiet);
            if (dragon.LastFeedXpAtMs.HasValue && nowMs - dragon.LastFeedXpAtMs.Value < CareRules.FeedXpCooldownMs)
                return new DomainError(DomainErrorCode.FeedCooldown);

            // Quyết toán thời gian trước để vàng tới lúc ăn tính theo stage cũ.
            SettleTime(s, nowMs, AdvanceMode.Online, events);
            int xpSec = CareRules.FeedXpSec(s.Player.Upgrades.FoodQualityLevel);
            var grown = Lifecycle.AddXp(dragon, xpSec);
            int bondBefore = dragon.Bond;
            dragon.XpSec = grown.xpMs / 1000.0;
            dragon.LastFeedXpAtMs = nowMs;
            int gained = BondRules.GainBond(dragon, nowMs);
            events.Add(new FoodConsumed(drop.Id, dragon.Uid, xpSec));
            foreach (StageChange sc in grown.stageChanges) events.Add(new StageChanged(dragon.Uid, sc.From, sc.To));
            if (gained == 1) events.Add(new BondChanged(dragon.Uid, bondBefore, dragon.Bond, BondSource.Feed));
            s.Food.Drops.RemoveAll(f => f.Id == drop.Id);
            return null;
        }

        private static DomainError Pet(GameState s, PetCommand c, long nowMs, List<DomainEvent> events)
        {
            DragonState dragon = s.FindDragon(c.DragonUid);
            if (dragon == null) return new DomainError(DomainErrorCode.UnknownDragon);
            if (dragon.Location.Kind == LocationKind.Released) return new DomainError(DomainErrorCode.DragonUnavailable);
            if (dragon.LastPetAtMs.HasValue && nowMs - dragon.LastPetAtMs.Value < CareRules.PetCooldownMs)
                return new DomainError(DomainErrorCode.PetCooldown);
            int before = dragon.Bond;
            dragon.LastPetAtMs = nowMs;
            int gained = BondRules.GainBond(dragon, nowMs);
            events.Add(new DragonPetted(dragon.Uid));
            if (gained == 1) events.Add(new BondChanged(dragon.Uid, before, dragon.Bond, BondSource.Pet));
            return null;
        }

        private static DomainError Rename(GameState s, RenameDragonCommand c, List<DomainEvent> events)
        {
            DragonState dragon = s.FindDragon(c.DragonUid);
            if (dragon == null) return new DomainError(DomainErrorCode.UnknownDragon);
            if (c.Nickname == null)
            {
                dragon.Nickname = null;
                events.Add(new DragonRenamed(dragon.Uid, null));
                return null;
            }
            NicknameResult n = NicknameRules.Normalize(c.Nickname);
            if (!n.Ok) return new DomainError(n.ErrorCode);
            dragon.Nickname = n.Value;
            events.Add(new DragonRenamed(dragon.Uid, n.Value));
            return null;
        }
    }
}
