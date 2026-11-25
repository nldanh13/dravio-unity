using System;
using System.Collections.Generic;
using Dravio.Domain.Biology;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Economy;
using Dravio.Domain.World;

namespace Dravio.Domain.Progression
{
    /// <summary>
    /// Gói Starter hướng dẫn và nhận trứng (GDD 3.1 trang 4, 15; S01; ADR 0013; src/engine/inventory/packs.ts).
    /// Kết quả cố định (Sproutling, Normal): không tiêu RNG domain, không đổi pityCount; roll một lần lúc mua và lưu ngay
    /// trong pendingPack. UID/tính cách lấy từ RNG có seed theo saveId / pack.id nên tái lập được.
    /// </summary>
    public static class StarterPack
    {
        private static readonly Personality[] Personalities = { Personality.Playful, Personality.Calm, Personality.Curious, Personality.Shy };

        public static MicroGold Price => MicroGold.FromWholeGold(PackCatalog.Get(ProgressCatalog.TutorialPackId).PriceG);

        public static DomainErrorCode? CheckBuy(GameState state)
        {
            if (state.PendingPack != null) return DomainErrorCode.PackPending;
            if (state.Tutorial.StarterPackUsed) return DomainErrorCode.TutorialPackUsed;
            if (state.Player.Gold < Price) return DomainErrorCode.NotEnoughGold;
            return null;
        }

        public static DomainErrorCode? ApplyBuy(GameState state, long nowMs, List<DomainEvent> events)
        {
            DomainErrorCode? err = CheckBuy(state);
            if (err != null) return err;
            MicroGold price = Price;
            DragonSpecies species = SpeciesCatalog.Get(ProgressCatalog.TutorialPackSpeciesId);
            string id = "pack_" + state.SaveId + "_tutorial";
            if (id.Length > 64) id = id.Substring(0, 64);
            string eggUid = InitialStateFactory.NewUid(Rng.Create(Rng.HashSeed("tutorial-pack", state.SaveId)));
            state.PendingPack = new PendingPack
            {
                Id = id,
                PackId = ProgressCatalog.TutorialPackId,
                SpeciesId = species.Id,
                Genotype = Genetics.FounderGenotype(eggUid, ContentInfo.ContentVersion, species.Loci),
                Mutation = ProgressCatalog.TutorialPackMutation,
                CreatedAtMs = nowMs,
                Tutorial = true,
            };
            state.Player.Gold = state.Player.Gold - price;
            state.Tutorial.StarterPackUsed = true;
            events.Add(new PackBought(ProgressCatalog.TutorialPackId, species.Id, price.Micro, true));
            return null;
        }

        /// <summary>Rồng nở từ trứng đang chờ (xác định theo pack.id): cùng một trứng luôn nở cùng UID và tính cách.</summary>
        public static DragonState HatchFromPack(PendingPack pack, long nowMs)
        {
            IRng rng = Rng.Create(Rng.HashSeed("hatch", pack.Id));
            string uid = InitialStateFactory.NewUid(rng);
            return new DragonState
            {
                Uid = uid, SpeciesId = pack.SpeciesId, Genotype = pack.Genotype.Clone(), Mutation = pack.Mutation, XpSec = 0, Locked = false,
                Location = DragonLocation.Storage, HatchedAtMs = nowMs, Personality = rng.PickUniform(Personalities), Bond = 0, ParentIds = null,
                Generation = 1, Ancestry = Ancestry.Unknown, Origin = DragonOrigin.Pack, Activity = DragonActivity.Idle, BreedCooldownUntilMs = 0,
            };
        }

        /// <summary>Các microhabitat có thể đặt trứng đang chờ (cùng luật với moveDragon), cho UI chọn chỗ.</summary>
        public static List<PlacementCheck> PackPlacementOptions(GameState state) =>
            state.PendingPack != null ? Zones.PlacementOptions(state, HatchFromPack(state.PendingPack, 0)) : new List<PlacementCheck>();

        public static DomainErrorCode? CheckClaim(GameState state, MoveTarget target, long nowMs)
        {
            if (state.PendingPack == null) return DomainErrorCode.NoPendingPack;
            DragonState dragon = HatchFromPack(state.PendingPack, nowMs);
            if (state.FindDragon(dragon.Uid) != null) return DomainErrorCode.InvalidCommand;
            if (target.Kind == LocationKind.Zone)
            {
                Zones.CheckPlacement(state, dragon, target.ZoneId, target.HabitatId, out DomainErrorCode? blocked);
                if (blocked != null) return blocked;
            }
            return null;
        }

        /// <summary>Nhận trứng: nở Baby, đặt vào microhabitat hợp lệ (kiểm như moveDragon) hoặc kho; totalHatched + 1.</summary>
        public static DomainErrorCode? ApplyClaim(GameState state, MoveTarget target, long nowMs, List<DomainEvent> events)
        {
            DomainErrorCode? err = CheckClaim(state, target, nowMs);
            if (err != null) return err;
            PendingPack pack = state.PendingPack;
            DragonState dragon = HatchFromPack(pack, nowMs);
            if (target.Kind == LocationKind.Zone) dragon.Location = DragonLocation.Zone(target.ZoneId, target.HabitatId);
            state.Dragons.Add(dragon);
            state.PendingPack = null;
            state.Player.Stats.TotalHatched += 1;
            events.Add(new DragonHatched(dragon.Uid, dragon.SpeciesId, dragon.Mutation, dragon.Location, pack.Tutorial));
            return null;
        }
    }

    /// <summary>
    /// Thành tựu (GDD Phụ lục F; ADR 0013; src/engine/progression/achievements.ts). Chỉ hai điều kiện đã cài:
    /// ACH_FIRST_HATCH (stats.totalHatched ≥ 1 — rồng có sẵn lúc tạo save không tính) và ACH_FIRST_ADULT (có rồng Adult chưa thả).
    /// Mở một lần, tự nhận một lần: thưởng cộng vào goldMicro, KHÔNG cộng vào totalGoldEarnedMicro. 10 thành tựu còn lại thuộc G2.
    /// </summary>
    public static class Achievements
    {
        public static readonly IReadOnlyList<string> Implemented = Array.AsReadOnly(new[] { "ACH_FIRST_HATCH", "ACH_FIRST_ADULT" });

        private static bool Rule(string id, GameState s)
        {
            switch (id)
            {
                case "ACH_FIRST_HATCH": return s.Player.Stats.TotalHatched >= 1;
                case "ACH_FIRST_ADULT":
                    foreach (DragonState d in s.Dragons)
                        if (d.Location.Kind != LocationKind.Released && EconomyRates.GrowthStageOf(d.XpSec, SpeciesCatalog.Get(d.SpeciesId)) == GrowthStage.Adult) return true;
                    return false;
                default: return false;
            }
        }

        private static List<string> Ordered(ICollection<string> ids)
        {
            var list = new List<string>();
            foreach (string id in ProgressCatalog.AchievementIds) if (ids.Contains(id)) list.Add(id);
            return list;
        }

        public static void Evaluate(GameState s, List<DomainEvent> events)
        {
            var fresh = new List<string>();
            foreach (string id in ProgressCatalog.AchievementIds)
                if (!s.UnlockedAchievements.Contains(id) && Rule(id, s)) fresh.Add(id);
            if (fresh.Count == 0) return;
            var claimed = new List<string>(s.ClaimedAchievements);
            var unlocked = new List<string>(s.UnlockedAchievements);
            foreach (string id in fresh)
            {
                MicroGold reward = MicroGold.FromWholeGold(ProgressCatalog.AchievementRewardG(id));
                s.Player.Gold = s.Player.Gold + reward;
                claimed.Add(id);
                unlocked.Add(id);
                events.Add(new AchievementUnlocked(id, reward.Micro));
            }
            s.UnlockedAchievements = Ordered(unlocked);
            s.ClaimedAchievements = Ordered(claimed);
        }
    }

    /// <summary>
    /// Chuỗi hướng dẫn TUT_01–TUT_08 (ADR 0009 mục 4; ADR 0013; src/engine/progression/tutorial.ts). Điều kiện đọc GameState,
    /// không đọc thao tác UI; bước chỉ hoàn thành theo thứ tự; điều kiện đã thỏa từ trước được ghi nhận liên tiếp khi tới lượt.
    /// </summary>
    public static class Tutorial
    {
        private static bool Unlocked(GameState s, string h)
        {
            foreach (ZoneState z in s.Zones) if (z.Id == "origin" && z.UnlockedHabitatIds.Contains(h)) return true;
            return false;
        }

        private static IEnumerable<DragonState> Alive(GameState s)
        {
            foreach (DragonState d in s.Dragons) if (d.Location.Kind != LocationKind.Released) yield return d;
        }

        public static bool StepDone(string step, GameState s)
        {
            switch (step)
            {
                case "TUT_01":
                    if (s.Clock.LastProcessedAtMs - s.Tutorial.StartedAtMs < ProgressCatalog.TutorialObserveMs) return false;
                    foreach (DragonState d in Alive(s)) if (d.Location.Kind == LocationKind.Zone) return true;
                    return false;
                case "TUT_02":
                    foreach (DragonState d in Alive(s)) if (d.LastFeedXpAtMs.HasValue) return true;
                    return false;
                case "TUT_03": return s.Tutorial.ProfilesViewed.Contains("emberling");
                case "TUT_04": return Unlocked(s, "origin_warm_rocks");
                case "TUT_05": return s.Tutorial.StarterPackUsed && s.PendingPack == null;
                case "TUT_06": return Unlocked(s, "origin_pondbank");
                case "TUT_07":
                    foreach (DragonState d in Alive(s))
                        if (EconomyRates.GrowthStageOf(d.XpSec, SpeciesCatalog.Get(d.SpeciesId)) == GrowthStage.Adult) return true;
                    return false;
                case "TUT_08": return Unlocked(s, "origin_meadow") && Unlocked(s, "origin_warm_rocks") && Unlocked(s, "origin_pondbank");
                default: throw new DomainInvariantException("Unknown tutorial step: " + step);
            }
        }

        public static string CurrentStep(TutorialState t)
        {
            foreach (string id in ProgressCatalog.TutorialStepIds) if (!t.Completed.Contains(id)) return id;
            return null;
        }

        /// <summary>Đánh dấu các bước liên tiếp đã thỏa (chỉ khi hướng dẫn đang chạy); đủ 8 bước → completed.</summary>
        public static void Evaluate(GameState s, List<DomainEvent> events)
        {
            if (s.Tutorial.Status != TutorialStatus.Active) return;
            while (true)
            {
                string step = CurrentStep(s.Tutorial);
                if (step == null || !StepDone(step, s)) break;
                s.Tutorial.Completed.Add(step);
                events.Add(new TutorialStepCompleted(step));
            }
            if (s.Tutorial.Completed.Count == ProgressCatalog.TutorialStepIds.Count)
            {
                s.Tutorial.Status = TutorialStatus.Completed;
                events.Add(new TutorialStatusChanged(TutorialStatus.Completed));
            }
        }

        /// <summary>Bỏ qua hoặc xem lại. Xem lại giữ các bước đã xong (trừ khi đã hoàn tất), đặt lại mốc quan sát.</summary>
        public static DomainErrorCode? SetStatus(GameState s, TutorialStatus status, long nowMs, List<DomainEvent> events)
        {
            TutorialStatus cur = s.Tutorial.Status;
            if (cur == status || (status == TutorialStatus.Skipped && cur == TutorialStatus.Completed)) return DomainErrorCode.AlreadyThere;
            if (status == TutorialStatus.Active)
            {
                if (cur == TutorialStatus.Completed) s.Tutorial.Completed = new List<string>();
                s.Tutorial.StartedAtMs = nowMs;
            }
            s.Tutorial.Status = status;
            events.Add(new TutorialStatusChanged(status));
            return null;
        }
    }

    /// <summary>Evaluator mặc định của dispatch, đúng thứ tự TypeScript: thành tựu rồi hướng dẫn.</summary>
    internal sealed class BuiltInProgressEvaluator : IPostCommandEvaluator
    {
        public static readonly BuiltInProgressEvaluator Instance = new BuiltInProgressEvaluator();
        public void Evaluate(GameState working, List<DomainEvent> events)
        {
            Achievements.Evaluate(working, events);
            Tutorial.Evaluate(working, events);
        }
    }
}
