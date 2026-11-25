using System;
using System.Collections.Generic;
using Dravio.Domain.Biology;
using Dravio.Domain.Catalog;
using Dravio.Domain.Text;
using Dravio.Domain.World;

namespace Dravio.Domain.Core
{
    /// <summary>
    /// Bước đánh giá chạy sau mỗi lệnh thành công, trên state làm việc (bản TS: evaluateAchievements rồi evaluateTutorial).
    /// U2.1 chưa port hai bước đó (thuộc U4) nên mặc định không có evaluator nào; xem README "Khác biệt có chủ đích".
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
                default:
                    // Lệnh chưa port ở U2.1 (moveDragon, setProductionSlot, unlockHabitat, ...).
                    return new DomainError(DomainErrorCode.InvalidCommand, "unsupported command type " + cmd.Type);
            }
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
