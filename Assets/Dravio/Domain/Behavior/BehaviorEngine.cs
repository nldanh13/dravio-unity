using System;
using System.Collections.Generic;
using System.Globalization;
using Dravio.Domain.Biology;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Health;

namespace Dravio.Domain.Behavior
{
    /// <summary>Trọng số của một hành vi trong lần chọn ngẫu nhiên (để debug / báo cáo).</summary>
    public readonly struct BehaviorWeight
    {
        public BehaviorWeight(DragonBehavior behavior, long weight) { Behavior = behavior; Weight = weight; }
        public DragonBehavior Behavior { get; }
        public long Weight { get; }
    }

    /// <summary>U2.15: nguồn giao phần ăn.</summary>
    public enum FoodAssignmentSource
    {
        /// <summary>Chưa rồng nào ăn được lúc này (phần ăn vẫn chờ tới khi hết hạn).</summary>
        None,
        /// <summary>Luật B: chính rồng được chọn.</summary>
        Target,
        /// <summary>Luật A (không có mục tiêu, hoặc mục tiêu không ăn được).</summary>
        Fallback,
    }

    /// <summary>U2.15: snapshot giao phần ăn (<see cref="BehaviorEngine.AssignmentOf"/>).</summary>
    public sealed class FoodAssignment
    {
        public FoodAssignment(string foodId, string targetDragonUid, string assignedDragonUid, FoodAssignmentSource source, string targetBlockedReason)
        {
            FoodId = foodId; TargetDragonUid = targetDragonUid; AssignedDragonUid = assignedDragonUid; Source = source; TargetBlockedReason = targetBlockedReason;
        }
        public string FoodId { get; }
        /// <summary>Rồng được chọn trên lệnh feed (null = không ưu tiên).</summary>
        public string TargetDragonUid { get; }
        /// <summary>Rồng sẽ tìm ăn (SeekFood) lúc này; null = chưa ai.</summary>
        public string AssignedDragonUid { get; }
        public FoodAssignmentSource Source { get; }
        /// <summary>
        /// Có mục tiêu nhưng mục tiêu không nhận: <c>unknown-dragon</c>, <c>released</c>, <c>wrong-place</c>, <c>busy</c>, <c>cooldown</c>, <c>wrong-diet</c>,
        /// <c>food-expired</c>, <c>target-has-other-food</c> (mục tiêu đã được giao drop sớm hết hạn hơn). null nếu không mục tiêu hoặc mục tiêu nhận.
        /// </summary>
        public string TargetBlockedReason { get; }
    }

    /// <summary>
    /// Quyết định hành vi của một rồng tại một thời điểm. Snapshot chỉ đọc, không giữ tham chiếu vào GameState.
    /// <see cref="Visible"/> = false: rồng không ở sinh cảnh (kho, đang lai, đang thám hiểm) và <see cref="Behavior"/> là null.
    /// </summary>
    public sealed class BehaviorDecision
    {
        public string Uid { get; internal set; }
        public string SpeciesId { get; internal set; }
        public bool Visible { get; internal set; }
        public DragonBehavior? Behavior { get; internal set; }
        public BehaviorRole Role { get; internal set; }
        /// <summary>Lý do chính, khóa ổn định (vd <c>food-drop</c>, <c>circadian-sleep</c>, <c>ambient</c>, <c>in-storage</c>).</summary>
        public string Reason { get; internal set; }
        public LocomotionHint Locomotion { get; internal set; }
        public InteractionHint Interaction { get; internal set; }
        /// <summary>SeekFood: ID FoodDrop mà Sanctuary nên dẫn rồng tới rồi gửi <c>consumeFood</c>. Ngoài ra null.</summary>
        public string TargetFoodId { get; internal set; }
        public string ZoneId { get; internal set; }
        public string HabitatId { get; internal set; }
        public DayPhase Phase { get; internal set; }
        /// <summary>Mức hoạt động 0–100 theo pha (DayCycle.Activeness).</summary>
        public int Activeness { get; internal set; }
        public long StartedAtMs { get; internal set; }
        /// <summary>Thời điểm nên hỏi lại (hết nhịp, hết Eat, thức ăn hết hạn). Hỏi sớm hơn vẫn cho cùng kết quả nếu state không đổi.</summary>
        public long UntilMs { get; internal set; }
        /// <summary>Trọng số các hành vi trình diễn đã cân nhắc (rỗng với quyết định gameplay).</summary>
        public IReadOnlyList<BehaviorWeight> Weights { get; internal set; }
    }

    /// <summary>
    /// Behavioral Engine U2.9 (ADR 0203). Hàm thuần: cùng (state, uid, nowMs, phase) → cùng quyết định, trên mọi máy.
    /// Không đổi GameState, không dùng RNG được tiêm (hạt giống là hashSeed của UID + nhịp), không đọc đồng hồ.
    /// Thứ tự: hiện diện → gameplay (Eat, SeekFood, nơi trú / chăm sóc, vitals thấp, cảnh báo) → ngủ theo nhịp sinh học → chọn có trọng số.
    /// </summary>
    public static class BehaviorEngine
    {
        public static BehaviorDecision Decide(GameState s, string uid, long nowMs, DayPhase phase)
        {
            DragonState d = s?.FindDragon(uid);
            if (d == null || d.Location.Kind == LocationKind.Released) return null;
            Dictionary<string, string> food = d.Location.Kind == LocationKind.Zone
                ? AssignFood(s, d.Location.ZoneId, d.Location.HabitatId, nowMs)
                : null;
            return DecideWith(s, d, nowMs, phase, food);
        }

        /// <summary>Quyết định cho mọi rồng chưa thả, thứ tự kho. Phân bổ thức ăn tính một lần cho mỗi microhabitat.</summary>
        public static List<BehaviorDecision> DecideAll(GameState s, long nowMs, DayPhase phase)
        {
            var list = new List<BehaviorDecision>();
            var foodByHabitat = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            foreach (DragonState d in s.Dragons)
            {
                if (d.Location.Kind == LocationKind.Released) continue;
                Dictionary<string, string> food = null;
                if (d.Location.Kind == LocationKind.Zone)
                {
                    string key = d.Location.ZoneId + "/" + d.Location.HabitatId;
                    if (!foodByHabitat.TryGetValue(key, out food))
                        foodByHabitat[key] = food = AssignFood(s, d.Location.ZoneId, d.Location.HabitatId, nowMs);
                }
                list.Add(DecideWith(s, d, nowMs, phase, food));
            }
            return list;
        }

        // ------------------------------------------------------------------ nhịp

        /// <summary>Lệch pha ổn định theo UID trong [0, EpochMs).</summary>
        public static long EpochOffset(string uid) => Rng.HashSeed("behavior-phase", uid ?? "") % BehaviorCatalog.EpochMs;

        /// <summary>Chỉ số nhịp của UID tại nowMs (floor, đúng cả với thời gian âm).</summary>
        public static long EpochIndex(string uid, long nowMs)
        {
            long t = nowMs + EpochOffset(uid);
            long q = t / BehaviorCatalog.EpochMs;
            if (t < 0 && q * BehaviorCatalog.EpochMs != t) q--;
            return q;
        }

        public static long EpochStart(string uid, long nowMs) => EpochIndex(uid, nowMs) * BehaviorCatalog.EpochMs - EpochOffset(uid);

        // ------------------------------------------------------------------ thức ăn

        /// <summary>
        /// Điều kiện để rồng ăn được drop ngay lúc này — trùng các kiểm của lệnh consumeFood, nên SeekFood không bao giờ dẫn tới lệnh bị từ chối.
        /// </summary>
        public static bool CanEat(DragonState d, FoodDrop f, long nowMs)
        {
            if (d.Location.Kind != LocationKind.Zone || d.Activity != DragonActivity.Idle) return false;
            if (f.ExpiresAtMs <= nowMs || f.ZoneId != d.Location.ZoneId || f.HabitatId != d.Location.HabitatId) return false;
            if (d.LastFeedXpAtMs.HasValue && nowMs - d.LastFeedXpAtMs.Value < CareRules.FeedXpCooldownMs) return false;
            return BehaviorCatalog.Has(SpeciesCatalog.GetBiology(d.SpeciesId).DietIds, f.DietId);
        }

        /// <summary>
        /// Mỗi drop còn hạn trong microhabitat được giao cho tối đa một rồng (không tranh nhau). Kết quả: uid → foodId.
        /// <para>
        /// U2.15 luật B (issue #74), hai lượt, thứ tự drop: hết hạn sớm trước, rồi ID (ordinal).
        /// Lượt 1 — drop có <see cref="FoodDrop.TargetDragonUid"/>: giao cho chính mục tiêu nếu nó ở microhabitat này, chưa được giao drop khác và <see cref="CanEat"/>.
        /// Lượt 2 — mọi drop còn lại (không mục tiêu, hoặc mục tiêu không ăn được): luật A — rồng thích đúng loại thức ăn trước, rồi UID (ordinal).
        /// Không drop nào có mục tiêu → lượt 1 rỗng → kết quả trùng luật A từng ô (parity TS b33ec7a).
        /// </para>
        /// </summary>
        public static Dictionary<string, string> AssignFood(GameState s, string zoneId, string habitatId, long nowMs)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            var drops = new List<FoodDrop>();
            foreach (FoodDrop f in s.Food.Drops) if (f.ZoneId == zoneId && f.HabitatId == habitatId && f.ExpiresAtMs > nowMs) drops.Add(f);
            if (drops.Count == 0) return result;
            drops.Sort((a, b) => a.ExpiresAtMs != b.ExpiresAtMs ? a.ExpiresAtMs.CompareTo(b.ExpiresAtMs) : string.CompareOrdinal(a.Id, b.Id));
            var here = new List<DragonState>();
            foreach (DragonState d in s.Dragons)
                if (d.Location.Kind == LocationKind.Zone && d.Location.ZoneId == zoneId && d.Location.HabitatId == habitatId) here.Add(d);
            here.Sort((a, b) => string.CompareOrdinal(a.Uid, b.Uid));
            var taken = new HashSet<string>(StringComparer.Ordinal);
            // Lượt 1: rồng được chọn.
            foreach (FoodDrop f in drops)
            {
                if (f.TargetDragonUid == null) continue;
                DragonState t = here.Find(d => d.Uid == f.TargetDragonUid);
                if (t == null || result.ContainsKey(t.Uid) || !CanEat(t, f, nowMs)) continue;
                result[t.Uid] = f.Id;
                taken.Add(f.Id);
            }
            // Lượt 2: luật A cho phần còn lại.
            foreach (FoodDrop f in drops)
            {
                if (taken.Contains(f.Id)) continue;
                DragonState best = null; bool bestFav = false;
                foreach (DragonState d in here)
                {
                    if (result.ContainsKey(d.Uid) || !CanEat(d, f, nowMs)) continue;
                    bool fav = PersonalityProfiles.Of(d).FavoriteDiet == f.DietId;
                    if (best == null || (fav && !bestFav)) { best = d; bestFav = fav; }
                }
                if (best != null) result[best.Uid] = f.Id;
            }
            return result;
        }

        /// <summary>
        /// U2.15: phần ăn <paramref name="foodId"/> đang được giao cho ai tại <paramref name="nowMs"/> và vì sao (cho UI cho ăn). Hàm thuần, không đổi state.
        /// null nếu không có drop đó trong state (đã ăn, hết hạn và đã dọn, hoặc ID sai).
        /// </summary>
        public static FoodAssignment AssignmentOf(GameState s, string foodId, long nowMs)
        {
            FoodDrop f = s?.Food?.Drops?.Find(x => x.Id == foodId);
            if (f == null) return null;
            string assigned = null;
            if (f.ExpiresAtMs > nowMs)
                foreach (var kv in AssignFood(s, f.ZoneId, f.HabitatId, nowMs))
                    if (kv.Value == f.Id) { assigned = kv.Key; break; }
            FoodAssignmentSource src = assigned == null ? FoodAssignmentSource.None
                : f.TargetDragonUid != null && assigned == f.TargetDragonUid ? FoodAssignmentSource.Target : FoodAssignmentSource.Fallback;
            return new FoodAssignment(f.Id, f.TargetDragonUid, assigned, src,
                f.TargetDragonUid == null || src == FoodAssignmentSource.Target ? null : TargetBlock(s, f, nowMs, assigned));
        }

        /// <summary>Lý do mục tiêu không nhận drop (khóa ổn định), theo thứ tự kiểm của <see cref="CanEat"/>.</summary>
        private static string TargetBlock(GameState s, FoodDrop f, long nowMs, string assigned)
        {
            if (f.ExpiresAtMs <= nowMs) return "food-expired";
            DragonState t = s.FindDragon(f.TargetDragonUid);
            if (t == null) return "unknown-dragon";
            if (t.Location.Kind == LocationKind.Released) return "released";
            if (t.Location.Kind != LocationKind.Zone || t.Location.ZoneId != f.ZoneId || t.Location.HabitatId != f.HabitatId) return "wrong-place";
            if (t.Activity != DragonActivity.Idle) return "busy";
            if (t.LastFeedXpAtMs.HasValue && nowMs - t.LastFeedXpAtMs.Value < CareRules.FeedXpCooldownMs) return "cooldown";
            if (!BehaviorCatalog.Has(SpeciesCatalog.GetBiology(t.SpeciesId).DietIds, f.DietId)) return "wrong-diet";
            return "target-has-other-food";
        }

        // ------------------------------------------------------------------ quyết định

        private static BehaviorDecision DecideWith(GameState s, DragonState d, long nowMs, DayPhase phase, Dictionary<string, string> food)
        {
            SpeciesBiology bio = SpeciesCatalog.GetBiology(d.SpeciesId);
            int activeness = DayCycle.Activeness(bio.Activity, phase);
            long start = EpochStart(d.Uid, nowMs);
            var o = new BehaviorDecision
            {
                Uid = d.Uid, SpeciesId = d.SpeciesId, Phase = phase, Activeness = activeness, StartedAtMs = start, UntilMs = start + BehaviorCatalog.EpochMs,
                Weights = Array.Empty<BehaviorWeight>(), Interaction = InteractionHint.None, Locomotion = LocomotionHint.Idle,
            };

            // 1. Hiện diện.
            switch (d.Location.Kind)
            {
                case LocationKind.Care: return Gameplay(o, DragonBehavior.Rest, "in-care", visible: true);
                case LocationKind.Shelter: return Gameplay(o, DragonBehavior.Rest, "sheltered", visible: true);
                case LocationKind.Storage:
                    return Hidden(o, d.Activity == DragonActivity.Breeding ? "busy-breeding" : d.Activity == DragonActivity.Expedition ? "busy-expedition" : "in-storage");
            }
            if (d.Activity == DragonActivity.Breeding) return Hidden(o, "busy-breeding");
            if (d.Activity == DragonActivity.Expedition) return Hidden(o, "busy-expedition");
            if (d.Activity == DragonActivity.Care) return Gameplay(o, DragonBehavior.Rest, "in-care", visible: true);

            o.ZoneId = d.Location.ZoneId; o.HabitatId = d.Location.HabitatId;
            HabitatCatalog.TryGet(d.Location.HabitatId, out HabitatProfile h);

            // 2. Gameplay.
            if (d.LastFeedXpAtMs.HasValue && nowMs >= d.LastFeedXpAtMs.Value && nowMs - d.LastFeedXpAtMs.Value < BehaviorCatalog.EatDisplayMs)
            {
                Gameplay(o, DragonBehavior.Eat, "fed", visible: true);
                o.Interaction = InteractionHint.Eat;
                o.StartedAtMs = d.LastFeedXpAtMs.Value; o.UntilMs = d.LastFeedXpAtMs.Value + BehaviorCatalog.EatDisplayMs;
                return o;
            }
            if (food != null && food.TryGetValue(d.Uid, out string foodId))
            {
                FoodDrop f = s.Food.Drops.Find(x => x.Id == foodId);
                Gameplay(o, DragonBehavior.SeekFood, "food-drop", visible: true);
                o.TargetFoodId = foodId; o.Locomotion = MoveHint(bio, h, run: false);
                o.StartedAtMs = f.SpawnedAtMs; o.UntilMs = f.ExpiresAtMs;
                return o;
            }
            Vitals v = d.Vitals;
            if (v != null && (v.Health < BehaviorCatalog.RestBelowHealth || v.Mobility < BehaviorCatalog.RestBelowMobility))
                return Gameplay(o, DragonBehavior.Rest, "vitals-low", visible: true);
            if (v != null && v.Energy < BehaviorCatalog.SleepBelowEnergy)
                return Gameplay(o, DragonBehavior.Sleep, "energy-low", visible: true);
            DisasterState ev = s.OpenDisasterIn(d.Location.ZoneId);
            if (ev != null && (ev.Phase == DisasterPhase.Forecast || ev.Phase == DisasterPhase.Preparation) && DisasterEngine.IsExposed(s, ev, d))
                return Gameplay(o, DragonBehavior.Rest, "hazard-warning", visible: true);

            // 3. Ngủ theo nhịp sinh học.
            if (activeness <= BehaviorCatalog.CircadianSleepAtOrBelow)
            {
                o.Visible = true; o.Behavior = DragonBehavior.Sleep; o.Role = BehaviorRole.Presentation; o.Reason = "circadian-sleep";
                o.Locomotion = LocomotionHint.Sleep;
                return o;
            }

            // 4. Chọn có trọng số (trình diễn).
            List<BehaviorWeight> weights = AmbientWeights(s, d, bio, h, activeness, phase);
            long total = 0;
            foreach (BehaviorWeight w in weights) total += w.Weight;
            DragonBehavior pick = DragonBehavior.Idle;
            if (total > 0)
            {
                uint seed = Rng.HashSeed("behavior", d.Uid, EpochIndex(d.Uid, nowMs).ToString(CultureInfo.InvariantCulture));
                long roll = (long)(seed % (ulong)total);
                foreach (BehaviorWeight w in weights)
                {
                    if (w.Weight <= 0) continue;
                    if (roll < w.Weight) { pick = w.Behavior; break; }
                    roll -= w.Weight;
                }
            }
            o.Visible = true; o.Behavior = pick; o.Role = BehaviorRole.Presentation; o.Reason = "ambient"; o.Weights = weights;
            o.Locomotion = HintFor(pick, bio, h);
            if (pick == DragonBehavior.Play) o.Interaction = InteractionHint.Happy;
            return o;
        }

        /// <summary>
        /// Trọng số hành vi trình diễn (ADR 0203 B3). Số nguyên, tất định. Hành vi loài/nơi ở không cho phép có trọng số 0 và không xuất hiện.
        /// </summary>
        public static List<BehaviorWeight> AmbientWeights(GameState s, DragonState d, SpeciesBiology bio, HabitatProfile h, int activeness, DayPhase phase)
        {
            PersonalityProfile p = PersonalityProfiles.Of(d);
            PersonalityTraits t = p.Traits;
            int others = 0;
            foreach (DragonState x in s.Dragons)
                if (x.Uid != d.Uid && x.Location.Kind == LocationKind.Zone && x.Location.ZoneId == d.Location.ZoneId && x.Location.HabitatId == d.Location.HabitatId) others++;
            bool shyCrowd = others >= BehaviorCatalog.CrowdedAtOthers && t.Boldness < 40;
            bool favorite = p.FavoriteHabitatId == d.Location.HabitatId;
            SuitabilityTier tier = h != null ? Suitability.TierOf(Suitability.ForSpecies(d.SpeciesId, d.Location.HabitatId)) : SuitabilityTier.Good;
            GrowthStage stage = Economy.EconomyRates.GrowthStageOf(d.XpSec, SpeciesCatalog.Get(d.SpeciesId));
            bool sunlit = phase == DayPhase.Day || phase == DayPhase.Dawn;
            bool ocean = h != null && h.Realm == Realm.Ocean;
            bool sky = h != null && h.Realm == Realm.Sky;

            var o = new List<BehaviorWeight>();
            foreach (DragonBehavior b in BehaviorCatalog.All)
            {
                BehaviorDef def = BehaviorCatalog.Get(b);
                if (!def.Ambient || !BehaviorCatalog.Allowed(bio, h, b)) continue;
                long w = def.BaseWeight;
                switch (b)
                {
                    case DragonBehavior.Idle:
                        w = w * (130 - t.Energy) / 100;
                        if (favorite) w = w * 120 / 100;
                        break;
                    case DragonBehavior.Rest:
                        w = w * (120 - t.Energy) / 100 * (200 - activeness) / 100;
                        if (favorite) w = w * 120 / 100;
                        break;
                    case DragonBehavior.Sleep:
                        w = (100 - activeness) * 4L;
                        break;
                    case DragonBehavior.Explore:
                        w = w * (50 + t.Curiosity) / 100 * activeness / 100;
                        if (tier == SuitabilityTier.NeedsImprovement) w = w * 150 / 100;
                        if (!favorite) w = w * 120 / 100;
                        if (shyCrowd) w /= 2;
                        break;
                    case DragonBehavior.Play:
                        w = w * (30 + t.Playfulness) / 100 * activeness / 100;
                        w = w * (stage == GrowthStage.Baby ? 150 : stage == GrowthStage.Juvenile ? 120 : 70) / 100;
                        if (others == 0) w = w * 60 / 100;
                        if (tier == SuitabilityTier.NeedsImprovement) w = w * 60 / 100;
                        if (shyCrowd) w /= 2;
                        break;
                    case DragonBehavior.Bask:
                        if (!sunlit) { w = 0; break; }
                        w = w * (130 - t.Energy) / 100 * activeness / 100;
                        if (h != null && h.TemperatureBand == TemperatureBand.Warm) w *= 2;
                        break;
                    case DragonBehavior.Swim:
                        w = w * (60 + t.Energy * 8 / 10) / 100 * activeness / 100;
                        if (ocean && !BehaviorCatalog.Has(bio.Locomotion, Locomotion.Walk)) w = w * 150 / 100;
                        break;
                    case DragonBehavior.Fly:
                        w = w * (60 + t.Energy * 8 / 10) / 100 * activeness / 100;
                        if (sky) w = w * 150 / 100;
                        if (t.Boldness < 40) w = w * 70 / 100;
                        break;
                    case DragonBehavior.Burrow:
                        w = w * (60 + t.Curiosity / 2) / 100 * activeness / 100;
                        break;
                }
                o.Add(new BehaviorWeight(b, Math.Max(0, w)));
            }
            return o;
        }

        private static BehaviorDecision Gameplay(BehaviorDecision o, DragonBehavior b, string reason, bool visible)
        {
            o.Visible = visible; o.Behavior = b; o.Role = BehaviorRole.Gameplay; o.Reason = reason;
            o.Locomotion = b == DragonBehavior.Sleep ? LocomotionHint.Sleep : LocomotionHint.Idle;
            return o;
        }

        private static BehaviorDecision Hidden(BehaviorDecision o, string reason)
        {
            o.Visible = false; o.Behavior = null; o.Role = BehaviorRole.Gameplay; o.Reason = reason; o.Locomotion = LocomotionHint.Idle;
            return o;
        }

        private static LocomotionHint MoveHint(SpeciesBiology bio, HabitatProfile h, bool run)
        {
            if (h != null && BehaviorCatalog.WalksHere(bio, h)) return run ? LocomotionHint.Run : LocomotionHint.Walk;
            if (BehaviorCatalog.Has(bio.Locomotion, Locomotion.Swim)) return LocomotionHint.Swim;
            if (BehaviorCatalog.Has(bio.Locomotion, Locomotion.Fly)) return LocomotionHint.Fly;
            return LocomotionHint.Walk;
        }

        /// <summary>Gợi ý locomotion cho tầng Sanctuary; luôn là kiểu di chuyển loài có (runtime Character không phải đoán).</summary>
        public static LocomotionHint HintFor(DragonBehavior b, SpeciesBiology bio, HabitatProfile h)
        {
            switch (b)
            {
                case DragonBehavior.Sleep: return LocomotionHint.Sleep;
                case DragonBehavior.Explore:
                case DragonBehavior.SeekFood: return MoveHint(bio, h, run: false);
                case DragonBehavior.Play: return MoveHint(bio, h, run: true);
                case DragonBehavior.Swim: return LocomotionHint.Swim;
                case DragonBehavior.Fly: return LocomotionHint.Fly;
                case DragonBehavior.Burrow: return LocomotionHint.Burrow;
                default: return LocomotionHint.Idle;
            }
        }
    }
}
