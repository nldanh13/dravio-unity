using System;
using System.Collections.Generic;
using Dravio.Domain.Behavior;
using Dravio.Domain.Biology;
using Dravio.Domain.Breeding;
using Dravio.Domain.Catalog;
using Dravio.Domain.World;

namespace Dravio.Domain.Progression
{
    /// <summary>Một bước trong thứ tự mở khóa trừu tượng: một nhiệm vụ hoặc một thế giới trở nên đạt được ở "đợt" <see cref="Wave"/>.</summary>
    public sealed class AuditStep
    {
        internal AuditStep(int wave, string kind, string id) { Wave = wave; Kind = kind; Id = id; }
        /// <summary>Đợt (0 = đạt được ngay từ save mới với nội dung Khởi Nguyên).</summary>
        public int Wave { get; }
        /// <summary>"quest" hoặc "zone".</summary>
        public string Kind { get; }
        public string Id { get; }
        public override string ToString() => Wave + ":" + Kind + ":" + Id;
    }

    public sealed class ProgressionAuditReport
    {
        /// <summary>Lỗi thiết kế catalog (<see cref="QuestProposals.ValidateCatalog"/>). Rỗng = hợp lệ.</summary>
        public IReadOnlyList<string> CatalogErrors { get; internal set; }
        /// <summary>Thứ tự đạt được (đợt tăng dần; trong một đợt theo thứ tự catalog).</summary>
        public IReadOnlyList<AuditStep> Steps { get; internal set; }
        /// <summary>Nhiệm vụ không bao giờ đạt được → lý do đầu tiên (mã yêu cầu hoặc khóa). Mong đợi: chỉ nhiệm vụ bị chặn bởi luật chưa duyệt.</summary>
        public IReadOnlyDictionary<string, string> UnreachableQuests { get; internal set; }
        public IReadOnlyList<string> UnreachableZones { get; internal set; }
        /// <summary>Loài người chơi có thể sở hữu khi đã mở đủ thế giới (gói, trứng thế giới, lai), theo thứ tự catalog.</summary>
        public IReadOnlyList<string> ObtainableSpecies { get; internal set; }

        public int WaveOf(string id)
        {
            foreach (AuditStep st in Steps) if (st.Id == id) return st.Wave;
            return -1;
        }
    }

    /// <summary>
    /// U2.12 — kiểm toán tĩnh tiến trình (chỉ đọc, không đổi luật). Mô phỏng trừu tượng "người chơi có thể làm được gì" trên catalog thật:
    /// thế giới đã mở → loài sở hữu được (gói Starter/… đều mua được ở mọi thế giới, gói hướng dẫn Sproutling, rồng khởi đầu Emberling, trứng thế giới,
    /// lai theo <see cref="BreedingRules.CandidateSpecies"/>) → loài đặt được (sinh cảnh mở, suitability cho phép, có kiểu di chuyển vào thế giới theo WorldGraph)
    /// → yêu cầu nhiệm vụ thỏa được → nhiệm vụ → điều kiện <c>unlockZone</c> → thế giới mới. Lặp tới điểm cố định.
    /// <para>Đây là chứng minh "không vòng lặp điều kiện gây bế tắc": nhiệm vụ nào không đạt được ở điểm cố định thì không bao giờ đạt được dù người chơi
    /// chơi tốt tới đâu. Kiểm toán bỏ qua yếu tố thời gian và xác suất (đó là việc của mô phỏng); nó chỉ trả lời "có đường hay không".</para>
    /// Các giả định (đều đúng với luật hiện hành, có test riêng): hướng dẫn luôn hoàn tất được (kể cả sau khi bỏ qua: setTutorialStatus active),
    /// vật liệu từ tuyến thám hiểm là nguồn lặp lại không tốn phí, vàng luôn tăng khi có rồng trong sinh cảnh (không bán được rồng cuối cùng).
    /// </summary>
    public static class ProgressionAudit
    {
        public static ProgressionAuditReport Run()
        {
            var open = new HashSet<string>(StringComparer.Ordinal) { "origin" };
            var done = new HashSet<string>(StringComparer.Ordinal);
            var steps = new List<AuditStep>();
            for (int wave = 0; wave < 64; wave++)
            {
                HashSet<string> owned = Obtainable(open);
                var newQuests = new List<string>();
                foreach (QuestProposalDef q in QuestProposals.All)
                    if (!done.Contains(q.Id) && Blocker(q, open, done, owned) == null) newQuests.Add(q.Id);
                var newZones = new List<string>();
                foreach (ZoneUnlockRule r in WorldUnlockCatalog.Rules)
                    if (!open.Contains(r.ZoneId) && ZoneReady(r, open, done, owned)) newZones.Add(r.ZoneId);
                if (newQuests.Count == 0 && newZones.Count == 0) break;
                foreach (string id in newQuests) { done.Add(id); steps.Add(new AuditStep(wave, "quest", id)); }
                foreach (string z in newZones) { open.Add(z); steps.Add(new AuditStep(wave, "zone", z)); }
            }
            HashSet<string> finalOwned = Obtainable(open);
            var unreachable = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (QuestProposalDef q in QuestProposals.All)
                if (!done.Contains(q.Id)) unreachable[q.Id] = Blocker(q, open, done, finalOwned) ?? "unknown";
            var zones = new List<string>();
            foreach (string z in WorldCatalog.ZoneIds) if (!open.Contains(z)) zones.Add(z);
            var obtainable = new List<string>();
            foreach (DragonSpecies sp in SpeciesCatalog.All) if (finalOwned.Contains(sp.Id)) obtainable.Add(sp.Id);
            return new ProgressionAuditReport
            {
                CatalogErrors = QuestProposals.ValidateCatalog(), Steps = steps, UnreachableQuests = unreachable, UnreachableZones = zones, ObtainableSpecies = obtainable,
            };
        }

        // ---------------------------------------------------------------- nguồn loài

        /// <summary>Loài sở hữu được khi các thế giới <paramref name="open"/> đã mở (đóng theo phép lai, chỉ lai bố mẹ đặt được để lớn và lai).</summary>
        internal static HashSet<string> Obtainable(HashSet<string> open)
        {
            var o = new HashSet<string>(StringComparer.Ordinal) { "emberling", ProgressCatalog.TutorialPackSpeciesId };
            foreach (DragonSpecies sp in SpeciesCatalog.All) if (sp.PackEligible) o.Add(sp.Id);
            foreach (ZoneUnlockRule r in WorldUnlockCatalog.Rules) if (open.Contains(r.ZoneId)) o.Add(r.EggSpeciesId);
            for (bool grew = true; grew;)
            {
                grew = false;
                var parents = new List<DragonSpecies>();
                foreach (DragonSpecies sp in SpeciesCatalog.All) if (o.Contains(sp.Id) && Placeable(sp.Id, open)) parents.Add(sp);
                foreach (DragonSpecies a in parents)
                    foreach (DragonSpecies b in parents)
                        foreach (DragonSpecies c in BreedingRules.CandidateSpecies(a, b))
                            if (o.Add(c.Id)) grew = true;
            }
            return o;
        }

        /// <summary>Microhabitat mở mà loài được đặt vào (suitability cho phép, có kiểu di chuyển vào thế giới).</summary>
        internal static List<string> Habitats(string speciesId, HashSet<string> open, Realm? realm = null)
        {
            var o = new List<string>();
            foreach (string h in SpeciesCatalog.GetBiology(speciesId).HabitatRules)
            {
                string zone = HabitatCatalog.ZoneOf(h);
                if (zone == null || !open.Contains(zone)) continue;
                if (realm.HasValue && (!HabitatCatalog.TryGet(h, out HabitatProfile p) || p.Realm != realm.Value)) continue;
                if (!Suitability.ForSpecies(speciesId, h).Allowed) continue;
                if (!CanEnter(speciesId, zone)) continue;
                o.Add(h);
            }
            return o;
        }

        internal static bool Placeable(string speciesId, HashSet<string> open, Realm? realm = null) => Habitats(speciesId, open, realm).Count > 0;

        /// <summary>Như <see cref="Travel.EntryLocomotion"/>: vùng gốc không cần, vùng khác cần một kiểu di chuyển của cạnh đi vào.</summary>
        private static bool CanEnter(string speciesId, string zoneId)
        {
            List<Locomotion> need = Travel.EntryLocomotion(zoneId);
            if (need.Count == 0) return true;
            foreach (Locomotion l in SpeciesCatalog.GetBiology(speciesId).Locomotion) if (need.Contains(l)) return true;
            return false;
        }

        private static bool SpeciesInRealm(string speciesId, Realm realm)
        {
            foreach (string h in SpeciesCatalog.GetBiology(speciesId).HabitatRules)
                if (HabitatCatalog.TryGet(h, out HabitatProfile p) && p.Realm == realm) return true;
            return false;
        }

        /// <summary>Sức chứa (số chỗ) của các thế giới đã mở có microhabitat thuộc realm (null = mọi realm).</summary>
        private static int Capacity(HashSet<string> open, Realm? realm)
        {
            int n = 0;
            foreach (string z in WorldCatalog.ZoneIds)
            {
                if (!open.Contains(z)) continue;
                bool any = !realm.HasValue;
                foreach (string h in Zones.HabitatsOfZone(z))
                    if (realm.HasValue && HabitatCatalog.TryGet(h, out HabitatProfile p) && p.Realm == realm.Value) any = true;
                if (any) n += WorldCatalog.P0ZoneCapacity;
            }
            return n;
        }

        // ---------------------------------------------------------------- nhiệm vụ / thế giới

        private static bool ZoneReady(ZoneUnlockRule r, HashSet<string> open, HashSet<string> done, HashSet<string> owned)
        {
            if (r.RequiresZone != null && !open.Contains(r.RequiresZone)) return false;
            if (r.RequiresQuest != null && !done.Contains(r.RequiresQuest)) return false;
            // Sinh cảnh Khởi Nguyên: đá ấm cần Emberling (rồng khởi đầu, cũng có trong gói Starter); bờ hồ không cần gì.
            if (r.RequiresAllOriginHabitats && !owned.Contains("emberling")) return false;
            return true;
        }

        /// <summary>null = nhiệm vụ đạt được với thế giới mở, nhiệm vụ đã xong và loài sở hữu được; ngược lại mã chặn đầu tiên.</summary>
        internal static string Blocker(QuestProposalDef q, HashSet<string> open, HashSet<string> done, HashSet<string> owned)
        {
            foreach (string z in q.RequiresZones) if (!open.Contains(z)) return "zone:" + z;
            foreach (string p in q.Prerequisites) if (!done.Contains(p)) return "quest:" + p;
            foreach (QuestRequirementDef r in q.Requirements)
            {
                string b = RequirementBlocker(r, open, owned);
                if (b != null) return r.Kind.ToWire() + ":" + b;
            }
            return null;
        }

        private static int CountSpecies(HashSet<string> owned, Func<string, bool> pred)
        {
            int n = 0;
            foreach (DragonSpecies sp in SpeciesCatalog.All) if (owned.Contains(sp.Id) && pred(sp.Id)) n++;
            return n;
        }

        private static string RequirementBlocker(QuestRequirementDef r, HashSet<string> open, HashSet<string> owned)
        {
            switch (r.Kind)
            {
                case QuestRequirementKind.ResearchTier:
                    // Mọi cấp nghiên cứu cần rồng của loài sống ở một sinh cảnh mở (thấy Adult, quan sát tập tính tại chỗ).
                    return CountSpecies(owned, id => (!r.Realm.HasValue || SpeciesInRealm(id, r.Realm.Value)) && Placeable(id, open)) >= r.Target ? null : "species";
                case QuestRequirementKind.CircadianRecords:
                    return CountSpecies(owned, id => Placeable(id, open)) >= r.Target ? null : "species";
                case QuestRequirementKind.SpeciesRecordedInRealm:
                    return CountSpecies(owned, id => Placeable(id, open, r.Realm)) >= r.Target ? null : "species-in-realm";
                case QuestRequirementKind.AdultWithLocomotion:
                    return CountSpecies(owned, id => BehaviorCatalog.Has(SpeciesCatalog.GetBiology(id).Locomotion, r.Locomotion.Value) && Placeable(id, open, r.Realm)) > 0
                        ? null : "no-species";
                case QuestRequirementKind.ResidentsThriving:
                {
                    int goodSpecies = CountSpecies(owned, id =>
                    {
                        foreach (string h in Habitats(id, open, r.Realm))
                            if (Suitability.TierOf(Suitability.ForSpecies(id, h)) == SuitabilityTier.Good) return true;
                        return false;
                    });
                    if (goodSpecies == 0) return "no-good-habitat";
                    return Capacity(open, r.Realm) >= r.Target ? null : "capacity";
                }
                case QuestRequirementKind.ResidentsAtHome:
                {
                    // Nơi ưa thích là một trong các sinh cảnh của loài; loài có MỌI sinh cảnh ở thế giới mở thì nơi ưa thích luôn tới được.
                    int homeSafe = CountSpecies(owned, id => Habitats(id, open).Count == SpeciesCatalog.GetBiology(id).HabitatRules.Count);
                    if (homeSafe == 0) return "no-home-species";
                    return Capacity(open, null) >= r.Target ? null : "capacity";
                }
                case QuestRequirementKind.IndividualsKnown:
                    return CountSpecies(owned, id => Placeable(id, open)) > 0 ? null : "no-species";
                case QuestRequirementKind.MaterialDelivery:
                    foreach (ExpeditionRoute route in ExpeditionCatalog.Routes)
                        if (BehaviorCatalog.Has(route.Materials, r.Param) && RouteUsable(route, open, owned)) return null;
                    return "no-source";
                case QuestRequirementKind.ExpeditionsClaimed:
                    return ExpeditionCatalog.TryGet(r.Param, out ExpeditionRoute er) && RouteUsable(er, open, owned) ? null : "route";
                case QuestRequirementKind.RouteAvailable:
                    if (!ExpeditionCatalog.TryGet(r.Param, out ExpeditionRoute ar)) return "unknown-route";
                    if (!ar.RewardsDefined) return "route-rewards-undefined";
                    return RouteUsable(ar, open, owned) ? null : "route";
                case QuestRequirementKind.RelationPairs:
                    return RelationBlocker(r, open, owned);
            }
            return "unknown-kind";
        }

        /// <summary>Tuyến mở, có bảng thưởng, và có loài sở hữu được đủ điều kiện sinh học (Adult lớn được ở sinh cảnh mở).</summary>
        private static bool RouteUsable(ExpeditionRoute route, HashSet<string> open, HashSet<string> owned)
        {
            if (!open.Contains(route.ZoneId) || !route.RewardsDefined) return false;
            foreach (DragonSpecies sp in SpeciesCatalog.All)
            {
                if (!owned.Contains(sp.Id) || !Placeable(sp.Id, open)) continue;
                IReadOnlyList<Locomotion> loco = sp.Biology.Locomotion;
                bool ok = route.Requirement == ExpeditionRequirement.LandDweller ? Has(sp.Realms, Realm.Land)
                    : route.Requirement == ExpeditionRequirement.Swimmer ? BehaviorCatalog.Has(loco, Locomotion.Swim) : BehaviorCatalog.Has(loco, Locomotion.Fly);
                if (ok) return true;
            }
            return false;
        }

        private static bool Has(IReadOnlyList<Realm> xs, Realm r)
        {
            foreach (Realm x in xs) if (x == r) return true;
            return false;
        }

        /// <summary>
        /// Bậc Bạn (≥ 50): hai rồng cùng loài, cùng microhabitat, sống chung đủ lâu (20 + 10 + 40 = 70). Bậc Thân (≥ 80): cần họ hàng — bố mẹ và con
        /// cùng microhabitat (60 + 20); nên phải có một cặp lai được mà con có chung sinh cảnh mở với bố hoặc mẹ.
        /// </summary>
        private static string RelationBlocker(QuestRequirementDef r, HashSet<string> open, HashSet<string> owned)
        {
            if (r.Relation <= RelationTier.Friend)
            {
                // n rồng chung một microhabitat cho n(n-1)/2 cặp.
                int need = 2;
                while (need * (need - 1) / 2 < r.Target) need++;
                if (CountSpecies(owned, id => Placeable(id, open)) == 0) return "no-species";
                return need <= WorldCatalog.P0ZoneCapacity ? null : "capacity";
            }
            var parents = new List<DragonSpecies>();
            foreach (DragonSpecies sp in SpeciesCatalog.All) if (owned.Contains(sp.Id) && Placeable(sp.Id, open)) parents.Add(sp);
            foreach (DragonSpecies a in parents)
                foreach (DragonSpecies b in parents)
                    foreach (DragonSpecies c in BreedingRules.CandidateSpecies(a, b))
                    {
                        List<string> child = Habitats(c.Id, open);
                        foreach (string h in Habitats(a.Id, open)) if (child.Contains(h)) return null;
                    }
            return "no-family-habitat";
        }
    }
}
