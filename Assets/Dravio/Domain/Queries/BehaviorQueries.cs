using System.Collections.Generic;
using Dravio.Domain.Behavior;
using Dravio.Domain.Catalog;

namespace Dravio.Domain.Queries
{
    /// <summary>Một ô của ma trận hành vi: loài làm được không, và làm được ở microhabitat nào.</summary>
    public sealed class BehaviorAvailability
    {
        public DragonBehavior Behavior { get; internal set; }
        /// <summary>None = loài có khả năng; khác None = lý do cấp loài.</summary>
        public BehaviorBlock SpeciesBlock { get; internal set; }
        /// <summary>Microhabitat hợp lệ của loài (HabitatRules) nơi hành vi xảy ra được.</summary>
        public IReadOnlyList<string> Habitats { get; internal set; }
        /// <summary>true = có thể xuất hiện do chọn ngẫu nhiên (trình diễn); false = chỉ do luật gameplay (SeekFood, Eat).</summary>
        public bool Ambient { get; internal set; }
    }

    /// <summary>Một hàng của ma trận hành vi 16 loài.</summary>
    public sealed class SpeciesBehaviorProfile
    {
        public string SpeciesId { get; internal set; }
        public ActivityCycle Activity { get; internal set; }
        public IReadOnlyList<Locomotion> Locomotion { get; internal set; }
        public IReadOnlyList<BehaviorAvailability> Behaviors { get; internal set; }
    }

    /// <summary>Độ phù hợp sinh cảnh của rồng + mục tiêu comfort theo đề xuất C1 (chỉ hiển thị khi luật tắt).</summary>
    public sealed class DragonEcologyView
    {
        public string DragonUid { get; internal set; }
        /// <summary>null khi rồng không ở microhabitat.</summary>
        public HabitatEcologyReport Habitat { get; internal set; }
        public int? CurrentComfort { get; internal set; }
        /// <summary>Mục tiêu comfort theo bộ luật truyền vào (null nếu không ở sinh cảnh hoặc đang bận).</summary>
        public int? ComfortTarget { get; internal set; }
        /// <summary>false ở production: comfort không tự đổi theo môi trường (ADR 0202 P8 / 0203 C1 chưa duyệt).</summary>
        public bool ComfortDriftEnabled { get; internal set; }
        public bool InFavoriteHabitat { get; internal set; }
    }

    /// <summary>
    /// API chỉ đọc U2.9 cho Unity (cùng nguyên tắc với <see cref="Ecosystem"/> và <see cref="HealthQueries"/>): mọi DTO là snapshot.
    /// Gọi trên <c>GameSession.State</c>; <c>phase</c> lấy từ <c>EnvironmentTimeOfDay.Phase</c> (Dawn/Day/Dusk/Night cùng thứ tự).
    /// </summary>
    public static class BehaviorQueries
    {
        public static BehaviorDecision Dragon(GameState s, string uid, long nowMs, DayPhase phase) => BehaviorEngine.Decide(s, uid, nowMs, phase);

        public static List<BehaviorDecision> All(GameState s, long nowMs, DayPhase phase) => BehaviorEngine.DecideAll(s, nowMs, phase);

        public static PersonalityProfile Personality(GameState s, string uid)
        {
            DragonState d = s.FindDragon(uid);
            return d == null ? null : PersonalityProfiles.Of(d);
        }

        public static SpeciesBehaviorProfile Species(string speciesId)
        {
            SpeciesBiology b = SpeciesCatalog.GetBiology(speciesId);
            var rows = new List<BehaviorAvailability>();
            foreach (DragonBehavior x in BehaviorCatalog.All)
            {
                BehaviorBlock sb = BehaviorCatalog.SpeciesBlock(b, x);
                var habitats = new List<string>();
                if (sb == BehaviorBlock.None)
                    foreach (string hid in b.HabitatRules)
                        if (HabitatCatalog.TryGet(hid, out HabitatProfile h) && BehaviorCatalog.HabitatBlock(h, x) == BehaviorBlock.None) habitats.Add(hid);
                rows.Add(new BehaviorAvailability { Behavior = x, SpeciesBlock = sb, Habitats = habitats, Ambient = BehaviorCatalog.Get(x).Ambient });
            }
            return new SpeciesBehaviorProfile { SpeciesId = speciesId, Activity = b.Activity, Locomotion = b.Locomotion, Behaviors = rows };
        }

        /// <summary>Ma trận 16 loài, thứ tự SpeciesCatalog.</summary>
        public static List<SpeciesBehaviorProfile> Matrix()
        {
            var o = new List<SpeciesBehaviorProfile>();
            foreach (DragonSpecies sp in SpeciesCatalog.All) o.Add(Species(sp.Id));
            return o;
        }

        /// <summary>Giải thích loài × microhabitat theo hồ sơ canonical (cho màn chọn chỗ đặt).</summary>
        public static HabitatEcologyReport Ecology(string speciesId, string habitatId) => HabitatEcology.Explain(speciesId, habitatId);

        public static DragonEcologyView Ecology(GameState s, string uid, EcologyComfortRules rules = null)
        {
            rules = rules ?? EcologyComfortRules.Production;
            DragonState d = s.FindDragon(uid);
            if (d == null) return null;
            bool inZone = d.Location.Kind == LocationKind.Zone && HabitatCatalog.Contains(d.Location.HabitatId);
            return new DragonEcologyView
            {
                DragonUid = uid,
                Habitat = inZone ? HabitatEcology.Explain(d.SpeciesId, d.Location.HabitatId) : null,
                CurrentComfort = d.Vitals?.Comfort,
                ComfortTarget = EcologyComfort.TargetOf(s, d, rules),
                ComfortDriftEnabled = rules.Enabled,
                InFavoriteHabitat = inZone && PersonalityProfiles.Of(d).FavoriteHabitatId == d.Location.HabitatId,
            };
        }

        public static SpeciesResearchView Research(GameState s, string speciesId, ObservationJournal journal = null, ResearchRules rules = null) =>
            Behavior.Research.Species(s, speciesId, journal, rules);
    }
}
