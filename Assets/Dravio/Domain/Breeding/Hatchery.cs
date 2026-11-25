using System;
using System.Collections.Generic;
using System.Globalization;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Economy;
using Dravio.Domain.Progression;
using Dravio.Domain.World;

namespace Dravio.Domain.Breeding
{
    /// <summary>
    /// Gói trứng thường và tổ ấp (U2.5). IMPLEMENTATION MỚI theo GDD 3.1 (trang 8, 15, 32,
    /// Phụ lục C) và ADR 0003 — TS b33ec7a chỉ có các hàm thuần (rollPackSpecies, rollMutation, checkBreed, rollBreed…), chưa có lệnh,
    /// nên phần này KHÔNG có parity với TS. Các hàm thuần dùng ở đây thì có parity (golden ts-genetics).
    ///
    /// RNG: mỗi giao dịch dùng một luồng riêng seed = hashSeed(tag, saveId, revision) — tất định theo state, độc lập với RNG
    /// hiệu ứng hình ảnh của lớp trình bày và với các luồng khác (founder, hatch). Cùng state luôn cho cùng kết quả, nên nạp lại save
    /// không "roll lại" được; kết quả được lưu ngay trong pendingPack / breedingNest.
    /// </summary>
    public static class Hatchery
    {
        private static string Rev(GameState s) => s.Revision.ToString(CultureInfo.InvariantCulture);
        private static IRng TxRng(GameState s, string tag) => Rng.Create(Rng.HashSeed(tag, s.SaveId, Rev(s)));

        /// <summary>Chỗ đã dùng: rồng chưa thả + trứng chờ nhận + trứng trong tổ (mỗi trứng giữ trước một chỗ, ADR 0003).</summary>
        public static int OccupiedSlots(GameState s)
        {
            int n = 0;
            foreach (DragonState d in s.Dragons) if (d.Location.Kind != LocationKind.Released) n++;
            if (s.PendingPack != null) n++;
            if (s.BreedingNest != null) n++;
            return n;
        }

        private static bool HasRoomForNewEgg(GameState s) => OccupiedSlots(s) < StorageCatalog.MaxDragons;

        // ---------------- Gói trứng ----------------

        public static DomainErrorCode? CheckBuyPack(GameState s, string packId)
        {
            if (!PackCatalog.TryGet(packId, out PackDef pack)) return DomainErrorCode.UnknownPack;
            if (s.PendingPack != null) return DomainErrorCode.PackPending;
            if (!HasRoomForNewEgg(s)) return DomainErrorCode.StorageFull;
            if (s.Player.Gold < MicroGold.FromWholeGold(pack.PriceG)) return DomainErrorCode.NotEnoughGold;
            return null;
        }

        public static DomainErrorCode? ApplyBuyPack(GameState s, string packId, long nowMs, List<DomainEvent> events)
        {
            DomainErrorCode? err = CheckBuyPack(s, packId);
            if (err != null) return err;
            PackDef pack = PackCatalog.Get(packId);
            MicroGold price = MicroGold.FromWholeGold(pack.PriceG);
            s.Player.Gold = s.Player.Gold - price;
            PendingPack egg = RollPack(s, pack, "buyPack", nowMs);
            events.Add(new PackBought(pack.Id, egg.SpeciesId, price.Micro, false));
            return null;
        }

        /// <summary>Roll loài (bảng gói) rồi biến thể (b = 0, có pity) trên cùng luồng RNG giao dịch; genotype founder theo UID trứng.</summary>
        private static PendingPack RollPack(GameState s, PackDef pack, string tag, long nowMs)
        {
            IRng rng = TxRng(s, tag);
            string speciesId = PackRolls.RollSpecies(rng, pack);
            MutationRoll m = Mutation.Roll(rng, s.Player.Upgrades.MutationLuckLevel, 0, s.Player.PityCount);
            string eggUid = InitialStateFactory.NewUid(rng, "e");
            s.Player.PityCount = m.PityCount;
            s.PendingPack = new PendingPack
            {
                Id = ("pack_" + eggUid).Length > 64 ? ("pack_" + eggUid).Substring(0, 64) : "pack_" + eggUid,
                PackId = pack.Id, SpeciesId = speciesId,
                Genotype = Biology.Genetics.FounderGenotype(eggUid, s.ContentVersion, SpeciesCatalog.Get(speciesId).Loci),
                Mutation = m.Mutation, CreatedAtMs = nowMs, Tutorial = false,
            };
            return s.PendingPack;
        }

        // ---------------- Tổ ấp ----------------

        private static DomainErrorCode ToCode(BreedRejection r)
        {
            switch (r)
            {
                case BreedRejection.SameDragon: return DomainErrorCode.BreedSameDragon;
                case BreedRejection.InvalidGenotype: return DomainErrorCode.BreedInvalidGenotype;
                case BreedRejection.Released: return DomainErrorCode.BreedReleased;
                case BreedRejection.EmergencyShelter: return DomainErrorCode.BreedEmergencyShelter;
                case BreedRejection.NotAdult: return DomainErrorCode.BreedNotAdult;
                case BreedRejection.Busy: return DomainErrorCode.BreedBusy;
                case BreedRejection.Cooldown: return DomainErrorCode.BreedCooldown;
                case BreedRejection.DifferentGroup: return DomainErrorCode.BreedDifferentGroup;
                case BreedRejection.Related: return DomainErrorCode.BreedRelated;
                default: return DomainErrorCode.BreedNoCandidates;
            }
        }

        /// <summary>Preview cho UI (query, không tiêu RNG): null kèm mã lỗi nếu cặp không hợp lệ.</summary>
        public static BreedPreview Preview(GameState s, string uidA, string uidB, long nowMs, out DomainErrorCode? error)
        {
            DragonState a = s.FindDragon(uidA), b = s.FindDragon(uidB);
            if (a == null || b == null) { error = DomainErrorCode.UnknownDragon; return null; }
            BreedPreview p = BreedingRules.QueryBreedPreview(a, b, nowMs, out BreedRejection? r);
            error = r == null ? (DomainErrorCode?)null : ToCode(r.Value);
            return p;
        }

        public static DomainErrorCode? CheckStartBreed(GameState s, string uidA, string uidB, long nowMs)
        {
            DragonState a = s.FindDragon(uidA), b = s.FindDragon(uidB);
            if (a == null || b == null) return DomainErrorCode.UnknownDragon;
            if (s.BreedingNest != null) return DomainErrorCode.NestBusy;
            BreedRejection? r = BreedingRules.CheckBreed(a, b, nowMs);
            if (r != null) return ToCode(r.Value);
            if (!HasRoomForNewEgg(s)) return DomainErrorCode.StorageFull;
            long fee = BreedingRules.BreedFeeG(SpeciesCatalog.Get(a.SpeciesId), SpeciesCatalog.Get(b.SpeciesId));
            if (s.Player.Gold < MicroGold.FromWholeGold(fee)) return DomainErrorCode.NotEnoughGold;
            return null;
        }

        /// <summary>
        /// Trừ phí, roll một lần (loài → genotype → biến thể với b = số bố mẹ có biến thể, có pity), khóa bố mẹ (activity = breeding,
        /// ngừng sản xuất), thời lượng = hatchSeconds(độ hiếm con, hatchSpeedLevel).
        /// </summary>
        public static DomainErrorCode? ApplyStartBreed(GameState s, string uidA, string uidB, long nowMs, List<DomainEvent> events)
        {
            DomainErrorCode? err = CheckStartBreed(s, uidA, uidB, nowMs);
            if (err != null) return err;
            DragonState a = s.FindDragon(uidA), b = s.FindDragon(uidB);
            DragonSpecies sa = SpeciesCatalog.Get(a.SpeciesId), sb = SpeciesCatalog.Get(b.SpeciesId);
            MicroGold fee = MicroGold.FromWholeGold(BreedingRules.BreedFeeG(sa, sb));
            IRng rng = TxRng(s, "breed");
            BreedOutcome outcome = BreedingRules.RollBreed(rng, a, b);
            int mutatedParents = (a.Mutation != MutationType.Normal ? 1 : 0) + (b.Mutation != MutationType.Normal ? 1 : 0);
            MutationRoll m = Mutation.Roll(rng, s.Player.Upgrades.MutationLuckLevel, mutatedParents, s.Player.PityCount);
            string eggUid = InitialStateFactory.NewUid(rng, "egg");
            long durationMs = checked(BreedingRules.HatchSeconds(SpeciesCatalog.Get(outcome.SpeciesId).Rarity, s.Player.Upgrades.HatchSpeedLevel) * 1000);
            s.Player.Gold = s.Player.Gold - fee;
            s.Player.PityCount = m.PityCount;
            a.Activity = DragonActivity.Breeding;
            b.Activity = DragonActivity.Breeding;
            s.BreedingNest = new BreedingEgg
            {
                Id = eggUid, ParentIds = new[] { a.Uid, b.Uid }, SpeciesId = outcome.SpeciesId, Genotype = outcome.Genotype, Mutation = m.Mutation,
                CreatedAtMs = nowMs, FinishAtMs = nowMs + durationMs, DurationMs = durationMs, TapReducedMs = 0, LastTapAtMs = 0,
                Status = EggStatus.Incubating,
            };
            events.Add(new BreedStarted(eggUid, a.Uid, b.Uid, fee.Micro, s.BreedingNest.FinishAtMs));
            return null;
        }

        /// <summary>Thời điểm tổ ấp xong trong khoảng quyết toán (≤ toMs), hoặc null.</summary>
        internal static long? CompletesBy(GameState s, long toMs)
        {
            BreedingEgg egg = s.BreedingNest;
            return egg != null && egg.Status == EggStatus.Incubating && egg.FinishAtMs <= toMs ? egg.FinishAtMs : (long?)null;
        }

        internal static bool IsNestParent(GameState s, DragonState d)
        {
            BreedingEgg egg = s.BreedingNest;
            return egg != null && egg.Status == EggStatus.Incubating && Array.IndexOf(egg.ParentIds, d.Uid) >= 0;
        }

        /// <summary>Ấp xong: trứng chờ nhận; bố mẹ về idle, cooldown 10 phút tính từ lúc xong (GDD trang 8, 32).</summary>
        internal static void Complete(GameState s, long atMs, List<DomainEvent> events)
        {
            BreedingEgg egg = s.BreedingNest;
            egg.Status = EggStatus.Ready;
            foreach (string uid in egg.ParentIds)
            {
                DragonState p = s.FindDragon(uid);
                if (p == null || p.Activity != DragonActivity.Breeding) continue;
                p.Activity = DragonActivity.Idle;
                p.BreedCooldownUntilMs = atMs + BreedingCatalog.ParentCooldownMs;
            }
            events.Add(new EggReady(egg.Id, atMs));
        }

        public static DomainErrorCode? CheckTap(GameState s, long nowMs)
        {
            BreedingEgg egg = s.BreedingNest;
            if (egg == null) return DomainErrorCode.NoEgg;
            if (egg.Status == EggStatus.Ready || egg.FinishAtMs <= nowMs) return DomainErrorCode.EggReady;
            if (egg.LastTapAtMs > 0 && nowMs - egg.LastTapAtMs < BreedingCatalog.TapMinIntervalMs) return DomainErrorCode.TapTooFast;
            if (TapRemainingMs(egg) <= 0) return DomainErrorCode.TapLimit;
            return null;
        }

        /// <summary>Phần còn được giảm bằng gõ: floor(10 % × durationMs) − tapReducedMs.</summary>
        public static long TapRemainingMs(BreedingEgg egg) => (long)Math.Floor(egg.DurationMs * BreedingCatalog.TapMaxShare) - egg.TapReducedMs;

        /// <summary>Chạy sau khi quyết toán tới nowMs. Nếu gõ làm trứng xong ngay thì hoàn tất tại nowMs.</summary>
        public static DomainErrorCode? ApplyTap(GameState s, long nowMs, List<DomainEvent> events)
        {
            DomainErrorCode? err = CheckTap(s, nowMs);
            if (err != null) return err;
            BreedingEgg egg = s.BreedingNest;
            long reduce = Math.Min(BreedingCatalog.TapReduceMs, TapRemainingMs(egg));
            egg.TapReducedMs += reduce;
            egg.FinishAtMs -= reduce;
            egg.LastTapAtMs = nowMs;
            events.Add(new EggTapped(egg.Id, reduce, egg.FinishAtMs));
            if (egg.FinishAtMs <= nowMs) Complete(s, nowMs, events);
            return null;
        }

        /// <summary>Rồng con nở từ tổ (xác định theo egg.id): cùng trứng luôn nở cùng UID/tính cách. Phả hệ ghi theo ADR 0003 #4.</summary>
        public static DragonState HatchFromNest(GameState s, BreedingEgg egg, long nowMs)
        {
            IRng rng = Rng.Create(Rng.HashSeed("hatch", egg.Id));
            string uid = InitialStateFactory.NewUid(rng);
            int generation = 1;
            List<string> grand = null;
            foreach (string pid in egg.ParentIds)
            {
                DragonState p = s.FindDragon(pid);
                generation = Math.Max(generation, (p?.Generation ?? 1) + 1);
                if (p?.ParentIds == null) continue;
                grand = grand ?? new List<string>();
                foreach (string g in p.ParentIds) if (!grand.Contains(g)) grand.Add(g);
            }
            return new DragonState
            {
                Uid = uid, SpeciesId = egg.SpeciesId, Genotype = egg.Genotype.Clone(), Mutation = egg.Mutation, XpSec = 0, Locked = false,
                Location = DragonLocation.Storage, HatchedAtMs = nowMs, Personality = rng.PickUniform(StarterPack.Personalities), Bond = 0,
                ParentIds = (string[])egg.ParentIds.Clone(), GrandparentIds = grand, Generation = generation, Ancestry = Ancestry.Known,
                Origin = DragonOrigin.Breed, Activity = DragonActivity.Idle, BreedCooldownUntilMs = 0,
            };
        }

        public static DomainErrorCode? CheckClaimBreed(GameState s, MoveTarget target, long nowMs)
        {
            BreedingEgg egg = s.BreedingNest;
            if (egg == null) return DomainErrorCode.NoEgg;
            if (egg.Status != EggStatus.Ready && egg.FinishAtMs > nowMs) return DomainErrorCode.EggNotReady;
            // Trứng đã giữ chỗ khi bắt đầu lai; nếu kho vẫn đầy (vượt giới hạn do dữ liệu cũ) thì trứng tiếp tục chờ, không mất.
            if (OccupiedSlots(s) > StorageCatalog.MaxDragons) return DomainErrorCode.StorageFull;
            DragonState child = HatchFromNest(s, egg, nowMs);
            if (s.FindDragon(child.Uid) != null) return DomainErrorCode.InvalidCommand;
            if (target.Kind == LocationKind.Zone)
            {
                Zones.CheckPlacement(s, child, target.ZoneId, target.HabitatId, out DomainErrorCode? blocked);
                if (blocked != null) return blocked;
            }
            return null;
        }

        public static DomainErrorCode? ApplyClaimBreed(GameState s, MoveTarget target, long nowMs, List<DomainEvent> events)
        {
            DomainErrorCode? err = CheckClaimBreed(s, target, nowMs);
            if (err != null) return err;
            DragonState child = HatchFromNest(s, s.BreedingNest, nowMs);
            if (target.Kind == LocationKind.Zone) child.Location = DragonLocation.Zone(target.ZoneId, target.HabitatId);
            s.Dragons.Add(child);
            s.BreedingNest = null;
            s.Player.Stats.TotalHatched += 1;
            s.Player.Stats.TotalBred += 1;
            Codex.RecordHatch(s, child.SpeciesId);
            events.Add(new DragonHatched(child.Uid, child.SpeciesId, child.Mutation, child.Location, false));
            return null;
        }

        /// <summary>Danh sách chỗ đặt rồng con (cùng luật moveDragon) cho UI.</summary>
        public static List<PlacementCheck> NestPlacementOptions(GameState s) =>
            s.BreedingNest != null ? Zones.PlacementOptions(s, HatchFromNest(s, s.BreedingNest, 0)) : new List<PlacementCheck>();
    }
}
