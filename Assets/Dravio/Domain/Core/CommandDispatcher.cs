using System;
using System.Collections.Generic;
using Dravio.Domain.Biology;
using Dravio.Domain.Catalog;
using Dravio.Domain.Progression;
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
        public static DispatchResult Dispatch(GameState state, GameCommand command, IClock clock, IReadOnlyList<IPostCommandEvaluator> postEvaluators = null)
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
            ElapsedResult er = TimeRules.ComputeElapsed(s.Clock, nowMs);
            long elapsedMs = er.ElapsedMs;
            ClockState next = er.Next;
            var local = new List<DomainEvent>();
            long remainder = s.Player.GoldRemainder;
            long gold = 0;
            var matured = new List<string>();
            foreach (DragonState d in s.Dragons)
            {
                if (d.Location.Kind == LocationKind.Released || elapsedMs == 0) continue;
                GrowthResult r = Lifecycle.AdvanceDragon(d, elapsedMs, Placement.IsProducing(s, d), mode, remainder);
                remainder = r.Remainder;
                gold = checked(gold + r.GoldMicro);
                foreach (StageChange c in r.StageChanges)
                {
                    local.Add(new StageChanged(d.Uid, c.From, c.To));
                    if (c.To == GrowthStage.Adult) matured.Add(d.Uid);
                }
                d.XpSec = r.XpMs / 1000.0;
            }
            long fromMs = s.Clock.LastProcessedAtMs;
            s.Player.Gold = s.Player.Gold + MicroGold.FromMicro(gold);
            s.Player.GoldRemainder = remainder;
            s.Player.TotalGoldEarned = s.Player.TotalGoldEarned + MicroGold.FromMicro(gold);
            s.Clock = next;
            events.Add(new TimeAdvanced(fromMs, next.LastProcessedAtMs, elapsedMs, mode, gold));
            events.AddRange(local);
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
