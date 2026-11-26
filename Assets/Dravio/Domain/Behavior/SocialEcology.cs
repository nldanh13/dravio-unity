using System;
using System.Collections.Generic;
using System.Globalization;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Economy;

namespace Dravio.Domain.Behavior
{
    // U2.10 Workstream B — Social Ecology (ADR 0204 S1–S9). U2.11 (ADR 0206): độ quen cộng thời gian sống chung đã lưu (Relations.cs); vẫn chỉ trình diễn.
    // Lớp trên Behavioral Engine U2.9: chỉ ghép cặp các quyết định TRÌNH DIỄN đã có. Không có lệnh, không đổi GameState,
    // không đánh nhau, không thương tổn, không tử vong; offline không có tương tác nào (hàm chỉ được gọi khi Sanctuary đang hiển thị).

    /// <summary>Tập tính xã hội của loài (S1). Không mặc định mọi loài đều sống theo nhóm.</summary>
    public enum SocialStyle
    {
        /// <summary>Thích ở gần đồng loại, chơi và nghỉ theo nhóm.</summary>
        [WireName("gregarious")] Gregarious,
        /// <summary>Chấp nhận bạn cùng chỗ, tương tác vừa phải.</summary>
        [WireName("tolerant")] Tolerant,
        /// <summary>Thích không gian riêng; ít khi chơi cùng, không nghỉ theo nhóm.</summary>
        [WireName("solitary")] Solitary,
    }

    /// <summary>Loại tương tác trình diễn giữa hai cá thể (S5).</summary>
    public enum SocialInteractionKind
    {
        [WireName("playTogether")] PlayTogether,
        [WireName("restTogether")] RestTogether,
        /// <summary>Tò mò quan sát một cá thể khác từ xa (không lại gần hơn khoảng cách cá nhân của bên kia).</summary>
        [WireName("curiousApproach")] CuriousApproach,
        /// <summary>Giữ khoảng cách khi nơi ở đông (cá thể thích không gian riêng). Không có đối phương.</summary>
        [WireName("keepDistance")] KeepDistance,
    }

    /// <summary>Thông số xã hội đề xuất của một loài (S1–S2). Chỉ là đề xuất: GDD 3.1 không định lượng tập tính xã hội.</summary>
    public sealed class SpeciesSocialDef
    {
        internal SpeciesSocialDef(string speciesId, SocialStyle style, int personalSpaceDm, string basis)
        {
            SpeciesId = speciesId; Style = style; PersonalSpaceDm = personalSpaceDm; BasisKey = basis;
        }

        public string SpeciesId { get; }
        public SocialStyle Style { get; }
        /// <summary>Khoảng cách cá nhân của Adult, decimét (số nguyên để tất định). Unity đổi sang mét: /10.</summary>
        public int PersonalSpaceDm { get; }
        /// <summary>Căn cứ sinh học ngắn (khóa i18n social.basis.*), để chủ dự án duyệt.</summary>
        public string BasisKey { get; }
    }

    /// <summary>Bảng xã hội 16 loài (đề xuất). Căn cứ: realm, kích thước/độ hiếm, nhóm sinh sản, chu kỳ hoạt động trong catalog G0.</summary>
    public static class SocialCatalog
    {
        public static readonly IReadOnlyList<SpeciesSocialDef> All = Array.AsReadOnly(new[]
        {
            new SpeciesSocialDef("emberling", SocialStyle.Tolerant, 15, "social.basis.smallLandCommon"),
            new SpeciesSocialDef("droplet", SocialStyle.Gregarious, 10, "social.basis.pondGroups"),
            new SpeciesSocialDef("sprout", SocialStyle.Gregarious, 10, "social.basis.meadowGrazer"),
            new SpeciesSocialDef("zephyr", SocialStyle.Gregarious, 15, "social.basis.flock"),
            new SpeciesSocialDef("steamdrake", SocialStyle.Tolerant, 20, "social.basis.serpentAmphibious"),
            new SpeciesSocialDef("magma", SocialStyle.Solitary, 30, "social.basis.territorialBasker"),
            new SpeciesSocialDef("sparkwing", SocialStyle.Tolerant, 20, "social.basis.flock"),
            new SpeciesSocialDef("frostfin", SocialStyle.Gregarious, 15, "social.basis.school"),
            new SpeciesSocialDef("mossback", SocialStyle.Tolerant, 15, "social.basis.slowGrazer"),
            new SpeciesSocialDef("blossom", SocialStyle.Gregarious, 15, "social.basis.pollinatorFlock"),
            new SpeciesSocialDef("solaris", SocialStyle.Solitary, 35, "social.basis.largeRaptor"),
            new SpeciesSocialDef("leviathan", SocialStyle.Solitary, 50, "social.basis.giantMarine"),
            new SpeciesSocialDef("amberhorn", SocialStyle.Solitary, 30, "social.basis.territorialBasker"),
            new SpeciesSocialDef("aurorawing", SocialStyle.Tolerant, 25, "social.basis.nocturnalFlyer"),
            new SpeciesSocialDef("celestia", SocialStyle.Solitary, 40, "social.basis.legendary"),
            new SpeciesSocialDef("gaia", SocialStyle.Tolerant, 30, "social.basis.legendaryGentle"),
        });

        public static SpeciesSocialDef Get(string speciesId)
        {
            foreach (SpeciesSocialDef d in All) if (d.SpeciesId == speciesId) return d;
            throw new DomainInvariantException("social: unknown species " + speciesId);
        }

        /// <summary>Độ hòa đồng gốc theo tập tính loài (S3).</summary>
        public static int BaseSociability(SocialStyle s) => s == SocialStyle.Gregarious ? 70 : s == SocialStyle.Tolerant ? 50 : 25;
    }

    /// <summary>Hồ sơ xã hội của một cá thể: dẫn xuất từ loài + cá tính + tuổi (S3). Không lưu.</summary>
    public sealed class SocialProfile
    {
        public string Uid { get; internal set; }
        public string SpeciesId { get; internal set; }
        public SocialStyle Style { get; internal set; }
        public GrowthStage Stage { get; internal set; }
        /// <summary>0–100. ≥ 55: thích có bạn; &lt; 35: thích không gian riêng.</summary>
        public int Sociability { get; internal set; }
        /// <summary>Khoảng cách cá nhân, decimét.</summary>
        public int PersonalSpaceDm { get; internal set; }
        public bool PrefersCompany => Sociability >= SocialRules.PrefersCompanyAtOrAbove;
        public bool WantsSpace => Sociability < SocialRules.WantsSpaceBelow;
    }

    /// <summary>Nguồn của độ quen (S4). Chỉ các nguồn dẫn xuất được từ save v8.</summary>
    public enum FamiliaritySource
    {
        [WireName("parentChild")] ParentChild,
        [WireName("siblings")] Siblings,
        [WireName("halfSiblings")] HalfSiblings,
        [WireName("grandparent")] Grandparent,
        [WireName("sharedHabitat")] SharedHabitat,
        [WireName("sameSpecies")] SameSpecies,
        /// <summary>U2.11 (ADR 0206): thời gian sống chung đã lưu (Schema v10), 1 điểm / giờ, trần 40.</summary>
        [WireName("sharedTime")] SharedTime,
    }

    /// <summary>Quan hệ giữa hai cá thể (S4). Snapshot.</summary>
    public sealed class SocialRelation
    {
        public string UidA { get; internal set; }
        public string UidB { get; internal set; }
        /// <summary>0–100: phả hệ, cùng chỗ, cùng loài và (U2.11) thời gian sống chung đã lưu.</summary>
        public int Familiarity { get; internal set; }
        /// <summary>U2.11: bậc hiển thị theo <see cref="Familiarity"/>.</summary>
        public RelationTier Tier => RelationRules.TierOf(Familiarity);
        /// <summary>U2.11: thời gian sống chung đã lưu (ms, trần 40 giờ).</summary>
        public long SharedMs { get; internal set; }
        public IReadOnlyList<FamiliaritySource> Sources { get; internal set; }
        /// <summary>0–100: hai cá thể có muốn ở gần nhau không.</summary>
        public int Affinity { get; internal set; }
    }

    /// <summary>
    /// Ứng viên tương tác trình diễn. <see cref="AffectsGameplay"/> luôn false: không dẫn tới lệnh, không đổi vitals/bond/vàng.
    /// Sanctuary có thể bỏ qua bất kỳ ứng viên nào (đường đi bị chặn, thiếu clip) mà không sai luật.
    /// </summary>
    public sealed class SocialInteractionCandidate
    {
        public SocialInteractionKind Kind { get; internal set; }
        public string InitiatorUid { get; internal set; }
        /// <summary>null với KeepDistance.</summary>
        public string PartnerUid { get; internal set; }
        public string ZoneId { get; internal set; }
        public string HabitatId { get; internal set; }
        public int Score { get; internal set; }
        public int Affinity { get; internal set; }
        public int Familiarity { get; internal set; }
        /// <summary>Khoảng cách tối thiểu nên giữ giữa hai cá thể, decimét (lớn hơn trong hai khoảng cách cá nhân, hoặc của chính mình với KeepDistance).</summary>
        public int MinDistanceDm { get; internal set; }
        public long StartedAtMs { get; internal set; }
        public long UntilMs { get; internal set; }
        public BehaviorRole Role => BehaviorRole.Presentation;
        public bool AffectsGameplay => false;
        /// <summary>social.&lt;kind&gt;.</summary>
        public string ReasonKey { get; internal set; }
    }

    /// <summary>
    /// Luật xã hội. Hằng ngưỡng là đề xuất trình diễn. <see cref="Production"/> TẮT mọi tác động gameplay (S8):
    /// <see cref="CompanionComfortBonus"/> / <see cref="CrowdingComfortPenalty"/> chỉ là đề xuất cho mục tiêu comfort C1 (cũng đang tắt).
    /// </summary>
    public sealed class SocialRules
    {
        public const int PrefersCompanyAtOrAbove = 55;
        public const int WantsSpaceBelow = 35;
        public const int PlayAffinityMin = 50;
        public const int RestAffinityMin = 45;
        public const int CuriousMinCuriosity = 60;
        public const int CuriousMaxFamiliarity = 40;
        /// <summary>Số rồng khác cùng microhabitat để cá thể thích không gian riêng chủ động giữ khoảng cách.</summary>
        public const int KeepDistanceAtOthers = 2;
        /// <summary>Nhịp ghép cặp (dùng chung cho cả hai bên, để hai cá thể cùng thấy một tương tác).</summary>
        public const long EpochMs = 60_000;

        public SocialRules(bool gameplayEffectsEnabled, int companionComfortBonus, int crowdingComfortPenalty)
        {
            GameplayEffectsEnabled = gameplayEffectsEnabled; CompanionComfortBonus = companionComfortBonus; CrowdingComfortPenalty = crowdingComfortPenalty;
        }

        public bool GameplayEffectsEnabled { get; }
        /// <summary>Cộng vào mục tiêu comfort C1 khi cá thể hòa đồng có bạn quen (độ quen ≥ 40) cùng chỗ.</summary>
        public int CompanionComfortBonus { get; }
        /// <summary>Trừ khỏi mục tiêu comfort C1 khi cá thể thích không gian riêng ở nơi đông (sàn Cozy/Ecosystem của C1 vẫn giữ).</summary>
        public int CrowdingComfortPenalty { get; }

        public static readonly SocialRules Production = new SocialRules(false, 0, 0);
        public static readonly SocialRules ProposedForReview = new SocialRules(true, 5, 5);
    }

    public static class SocialEcology
    {
        public static SocialProfile Profile(DragonState d)
        {
            if (d == null) throw new ArgumentNullException(nameof(d));
            SpeciesSocialDef def = SocialCatalog.Get(d.SpeciesId);
            PersonalityTraits t = PersonalityProfiles.TraitsOf(d.Uid, d.Personality);
            GrowthStage stage = EconomyRates.GrowthStageOf(d.XpSec, SpeciesCatalog.Get(d.SpeciesId));
            int ageAdj = stage == GrowthStage.Baby ? 15 : stage == GrowthStage.Juvenile ? 5 : 0;
            int soc = SocialCatalog.BaseSociability(def.Style) + (t.Boldness - 50) / 4 + (t.Playfulness - 50) / 5 + ageAdj;
            int space = def.PersonalSpaceDm * (stage == GrowthStage.Baby ? 60 : stage == GrowthStage.Juvenile ? 80 : 100) / 100;
            if (t.Boldness < 40) space = space * 130 / 100;
            return new SocialProfile
            {
                Uid = d.Uid, SpeciesId = d.SpeciesId, Style = def.Style, Stage = stage, Sociability = Clamp(soc), PersonalSpaceDm = Math.Max(5, space),
            };
        }

        private static int Clamp(int v) => Math.Max(0, Math.Min(100, v));

        private static bool SameHabitat(DragonState a, DragonState b) =>
            a.Location.Kind == LocationKind.Zone && b.Location.Kind == LocationKind.Zone && a.Location.ZoneId == b.Location.ZoneId && a.Location.HabitatId == b.Location.HabitatId;

        private static bool IsParentOf(DragonState p, DragonState c) => c.ParentIds != null && Array.IndexOf(c.ParentIds, p.Uid) >= 0;

        /// <summary>Độ quen dẫn xuất (S4): cha mẹ–con 60, anh chị em ruột 50, cùng cha hoặc mẹ 30, ông bà 30; cùng chỗ +20, cùng loài +10.</summary>
        public static SocialRelation Relation(DragonState a, DragonState b) => Relation(null, a, b);

        /// <summary>U2.11: như trên, cộng điểm thời gian sống chung từ <see cref="GameState.Relations"/> (null = không có).</summary>
        public static SocialRelation Relation(GameState s, DragonState a, DragonState b)
        {
            var src = new List<FamiliaritySource>();
            int kin = 0;
            if (IsParentOf(a, b) || IsParentOf(b, a)) { kin = 60; src.Add(FamiliaritySource.ParentChild); }
            else if (a.ParentIds != null && b.ParentIds != null)
            {
                int shared = 0;
                foreach (string p in a.ParentIds) if (Array.IndexOf(b.ParentIds, p) >= 0) shared++;
                if (shared >= 2) { kin = 50; src.Add(FamiliaritySource.Siblings); }
                else if (shared == 1) { kin = 30; src.Add(FamiliaritySource.HalfSiblings); }
            }
            if (kin == 0 && ((a.GrandparentIds != null && a.GrandparentIds.Contains(b.Uid)) || (b.GrandparentIds != null && b.GrandparentIds.Contains(a.Uid))))
            {
                kin = 30; src.Add(FamiliaritySource.Grandparent);
            }
            int fam = kin;
            if (SameHabitat(a, b)) { fam += 20; src.Add(FamiliaritySource.SharedHabitat); }
            if (a.SpeciesId == b.SpeciesId) { fam += 10; src.Add(FamiliaritySource.SameSpecies); }
            long sharedMs = s?.Relations?.SharedMs(a.Uid, b.Uid) ?? 0;
            int timePts = RelationRules.TimePoints(sharedMs);
            if (timePts > 0) { fam += timePts; src.Add(FamiliaritySource.SharedTime); }
            fam = Clamp(fam);

            SocialProfile pa = Profile(a), pb = Profile(b);
            int aff = (pa.Sociability + pb.Sociability) / 2 + fam / 4;
            if (a.SpeciesId == b.SpeciesId) aff += 10;
            else if (SpeciesCatalog.Get(a.SpeciesId).Group == SpeciesCatalog.Get(b.SpeciesId).Group) aff += 5;
            ActivityCycle ca = SpeciesCatalog.GetBiology(a.SpeciesId).Activity, cb = SpeciesCatalog.GetBiology(b.SpeciesId).Activity;
            if ((ca == ActivityCycle.Day && cb == ActivityCycle.Night) || (ca == ActivityCycle.Night && cb == ActivityCycle.Day)) aff -= 15;
            if (pa.Style == SocialStyle.Solitary || pb.Style == SocialStyle.Solitary) aff -= 20;
            bool ordered = string.CompareOrdinal(a.Uid, b.Uid) <= 0;
            return new SocialRelation { UidA = ordered ? a.Uid : b.Uid, UidB = ordered ? b.Uid : a.Uid, Familiarity = fam, Sources = src, Affinity = Clamp(aff), SharedMs = sharedMs };
        }

        private static bool IsRestLike(BehaviorDecision d) => d.Behavior == DragonBehavior.Rest || d.Behavior == DragonBehavior.Sleep || d.Behavior == DragonBehavior.Idle;

        /// <summary>
        /// Ứng viên tương tác trình diễn trong mọi microhabitat (S5–S6). Chỉ cặp cá thể đang hiện, cùng microhabitat, cả hai đều có quyết định
        /// TRÌNH DIỄN (quyết định gameplay như SeekFood/Eat/Rest do vitals, chăm sóc, cảnh báo luôn thắng và không bị ghép).
        /// Mỗi cá thể tối đa một tương tác mỗi nhịp; chọn tham lam theo điểm, hòa điểm theo hashSeed("social", cặp, nhịp). Tất định.
        /// </summary>
        public static List<SocialInteractionCandidate> Candidates(GameState s, long nowMs, DayPhase phase)
        {
            List<BehaviorDecision> decisions = BehaviorEngine.DecideAll(s, nowMs, phase);
            var byHabitat = new SortedDictionary<string, List<BehaviorDecision>>(StringComparer.Ordinal);
            foreach (BehaviorDecision d in decisions)
            {
                if (d == null || !d.Visible || d.Behavior == null || d.ZoneId == null || d.HabitatId == null || d.Role != BehaviorRole.Presentation) continue;
                string key = d.ZoneId + "/" + d.HabitatId;
                if (!byHabitat.TryGetValue(key, out var list)) byHabitat[key] = list = new List<BehaviorDecision>();
                list.Add(d);
            }
            long epoch = FloorDiv(nowMs, SocialRules.EpochMs);
            var all = new List<SocialInteractionCandidate>();
            foreach (var kv in byHabitat)
            {
                List<BehaviorDecision> here = kv.Value;
                here.Sort((x, y) => string.CompareOrdinal(x.Uid, y.Uid));
                int occupants = 0;
                string zone = here[0].ZoneId, hab = here[0].HabitatId;
                foreach (DragonState d in s.Dragons) if (d.Location.Kind == LocationKind.Zone && d.Location.ZoneId == zone && d.Location.HabitatId == hab) occupants++;
                var pool = new List<(SocialInteractionCandidate c, uint tie)>();
                for (int i = 0; i < here.Count; i++)
                    for (int j = i + 1; j < here.Count; j++)
                    {
                        SocialInteractionCandidate c = PairCandidate(s, here[i], here[j]);
                        if (c == null) continue;
                        pool.Add((c, Rng.HashSeed("social", c.InitiatorUid + "|" + (c.PartnerUid ?? ""), epoch.ToString(CultureInfo.InvariantCulture))));
                    }
                foreach (BehaviorDecision d in here)
                {
                    DragonState dragon = s.FindDragon(d.Uid);
                    SocialProfile p = Profile(dragon);
                    if (p.WantsSpace && occupants - 1 >= SocialRules.KeepDistanceAtOthers)
                    {
                        var c = new SocialInteractionCandidate
                        {
                            Kind = SocialInteractionKind.KeepDistance, InitiatorUid = d.Uid, ZoneId = zone, HabitatId = hab, Score = 40 + (SocialRules.WantsSpaceBelow - p.Sociability),
                            MinDistanceDm = p.PersonalSpaceDm, StartedAtMs = d.StartedAtMs, UntilMs = d.UntilMs, ReasonKey = "social.keepDistance",
                        };
                        pool.Add((c, Rng.HashSeed("social", d.Uid + "|", epoch.ToString(CultureInfo.InvariantCulture))));
                    }
                }
                pool.Sort((x, y) => x.c.Score != y.c.Score ? y.c.Score.CompareTo(x.c.Score) : x.tie != y.tie ? x.tie.CompareTo(y.tie) : string.CompareOrdinal(x.c.InitiatorUid, y.c.InitiatorUid));
                var busy = new HashSet<string>(StringComparer.Ordinal);
                foreach (var (c, _) in pool)
                {
                    if (busy.Contains(c.InitiatorUid) || (c.PartnerUid != null && busy.Contains(c.PartnerUid))) continue;
                    busy.Add(c.InitiatorUid);
                    if (c.PartnerUid != null) busy.Add(c.PartnerUid);
                    all.Add(c);
                }
            }
            return all;
        }

        /// <summary>Ứng viên tốt nhất cho một cặp (hoặc null). Initiator là bên có lý do chủ động (chơi, tò mò).</summary>
        private static SocialInteractionCandidate PairCandidate(GameState s, BehaviorDecision a, BehaviorDecision b)
        {
            DragonState da = s.FindDragon(a.Uid), db = s.FindDragon(b.Uid);
            SocialProfile pa = Profile(da), pb = Profile(db);
            SocialRelation r = Relation(s, da, db);
            int dist = Math.Max(pa.PersonalSpaceDm, pb.PersonalSpaceDm);
            long start = Math.Max(a.StartedAtMs, b.StartedAtMs), until = Math.Min(a.UntilMs, b.UntilMs);
            SocialInteractionCandidate Make(SocialInteractionKind k, BehaviorDecision init, BehaviorDecision partner, int score) => new SocialInteractionCandidate
            {
                Kind = k, InitiatorUid = init.Uid, PartnerUid = partner.Uid, ZoneId = a.ZoneId, HabitatId = a.HabitatId, Score = score, Affinity = r.Affinity,
                Familiarity = r.Familiarity, MinDistanceDm = dist, StartedAtMs = start, UntilMs = until, ReasonKey = "social." + k.ToWire(),
            };

            // Chơi cùng: một bên đang Play, bên kia đang Play/Idle/Explore và hòa đồng đủ; không ai muốn không gian riêng.
            bool aPlay = a.Behavior == DragonBehavior.Play, bPlay = b.Behavior == DragonBehavior.Play;
            if ((aPlay || bPlay) && !pa.WantsSpace && !pb.WantsSpace && r.Affinity >= SocialRules.PlayAffinityMin)
            {
                BehaviorDecision init = aPlay ? a : b, other = aPlay ? b : a;
                SocialProfile po = aPlay ? pb : pa;
                bool joins = other.Behavior == DragonBehavior.Play ||
                    ((other.Behavior == DragonBehavior.Idle || other.Behavior == DragonBehavior.Explore) && po.Sociability >= 45);
                if (joins) return Make(SocialInteractionKind.PlayTogether, init, other, 60 + r.Affinity / 2);
            }
            // Nghỉ theo nhóm: cả hai nghỉ/ngủ/đứng yên, không loài nào sống đơn độc.
            if (IsRestLike(a) && IsRestLike(b) && (a.Behavior != DragonBehavior.Idle || b.Behavior != DragonBehavior.Idle) &&
                pa.Style != SocialStyle.Solitary && pb.Style != SocialStyle.Solitary && r.Affinity >= SocialRules.RestAffinityMin)
                return Make(SocialInteractionKind.RestTogether, string.CompareOrdinal(a.Uid, b.Uid) <= 0 ? a : b, string.CompareOrdinal(a.Uid, b.Uid) <= 0 ? b : a, 50 + r.Affinity / 2);
            // Tò mò: một bên đang Explore, tò mò cao, chưa quen bên kia; bên kia không ngủ.
            foreach (var (x, y, px) in new[] { (a, b, da), (b, a, db) })
            {
                if (x.Behavior != DragonBehavior.Explore || y.Behavior == DragonBehavior.Sleep) continue;
                if (PersonalityProfiles.TraitsOf(px.Uid, px.Personality).Curiosity < SocialRules.CuriousMinCuriosity) continue;
                if (r.Familiarity >= SocialRules.CuriousMaxFamiliarity) continue;
                return Make(SocialInteractionKind.CuriousApproach, x, y, 45 + (SocialRules.CuriousMaxFamiliarity - r.Familiarity) / 2);
            }
            return null;
        }

        /// <summary>
        /// Điều chỉnh mục tiêu comfort đề xuất (S8). Production = 0 luôn. Chỉ có ý nghĩa khi C1 (ADR 0203) và P7 (ADR 0202) được duyệt.
        /// </summary>
        public static int ComfortTargetModifier(GameState s, string uid, SocialRules rules)
        {
            rules = rules ?? SocialRules.Production;
            if (!rules.GameplayEffectsEnabled) return 0;
            DragonState d = s.FindDragon(uid);
            if (d == null || d.Location.Kind != LocationKind.Zone) return 0;
            SocialProfile p = Profile(d);
            int others = 0; bool familiarCompanion = false;
            foreach (DragonState x in s.Dragons)
            {
                if (x.Uid == d.Uid || !SameHabitat(d, x)) continue;
                others++;
                if (Relation(s, d, x).Familiarity >= 40) familiarCompanion = true;
            }
            if (p.WantsSpace && others >= BehaviorCatalog.CrowdedAtOthers) return -rules.CrowdingComfortPenalty;
            if (p.PrefersCompany && familiarCompanion) return rules.CompanionComfortBonus;
            return 0;
        }

        private static long FloorDiv(long a, long b)
        {
            long q = a / b;
            if (a < 0 && q * b != a) q--;
            return q;
        }
    }
}
