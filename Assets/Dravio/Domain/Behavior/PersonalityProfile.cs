using System;
using System.Collections.Generic;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;

namespace Dravio.Domain.Behavior
{
    /// <summary>
    /// Bốn nét tính cách 0–100. Dẫn xuất, KHÔNG lưu: giá trị gốc lấy theo <see cref="Personality"/> đã có trong save
    /// (roll lúc nở), cộng độ lệch ổn định theo UID <c>hashSeed("personality-trait", uid, trait)</c> trong ±<see cref="PersonalityProfiles.JitterMax"/>.
    /// Cùng UID và cùng Personality luôn ra cùng kết quả, trên mọi máy, mọi lần nạp.
    /// </summary>
    public readonly struct PersonalityTraits
    {
        public PersonalityTraits(int energy, int curiosity, int playfulness, int boldness)
        {
            Energy = energy; Curiosity = curiosity; Playfulness = playfulness; Boldness = boldness;
        }

        /// <summary>Mức năng động: ít Idle/Rest, nhiều di chuyển.</summary>
        public int Energy { get; }
        /// <summary>Thích Explore.</summary>
        public int Curiosity { get; }
        /// <summary>Thích Play.</summary>
        public int Playfulness { get; }
        /// <summary>Dạn dĩ: Shy thấp — ngại Play/Explore khi habitat đông.</summary>
        public int Boldness { get; }

        public override string ToString() => "E" + Energy + "/C" + Curiosity + "/P" + Playfulness + "/B" + Boldness;
    }

    /// <summary>Cá tính đầy đủ của một cá thể: nét tính cách + sở thích (thức ăn, microhabitat) ổn định theo UID.</summary>
    public sealed class PersonalityProfile
    {
        public string Uid { get; internal set; }
        public Personality Archetype { get; internal set; }
        public PersonalityTraits Traits { get; internal set; }
        /// <summary>Thức ăn ưa thích, luôn thuộc DietIds của loài.</summary>
        public DietId FavoriteDiet { get; internal set; }
        /// <summary>Microhabitat ưa thích, luôn thuộc HabitatRules của loài.</summary>
        public string FavoriteHabitatId { get; internal set; }
        /// <summary>Khóa i18n mô tả ngắn: <c>personality.&lt;archetype&gt;.summary</c>.</summary>
        public string SummaryKey { get; internal set; }
    }

    public static class PersonalityProfiles
    {
        /// <summary>Độ lệch tối đa theo UID quanh giá trị gốc của archetype (ADR 0203 A2).</summary>
        public const int JitterMax = 10;

        // Giá trị gốc theo archetype (ADR 0203 A1). Thứ tự: energy, curiosity, playfulness, boldness.
        private static int[] Base(Personality p)
        {
            switch (p)
            {
                case Personality.Playful: return new[] { 70, 55, 85, 60 };
                case Personality.Calm: return new[] { 35, 40, 35, 55 };
                case Personality.Curious: return new[] { 60, 85, 50, 65 };
                case Personality.Shy: return new[] { 40, 50, 35, 20 };
                default: throw new ArgumentOutOfRangeException(nameof(p));
            }
        }

        private static readonly string[] TraitIds = { "energy", "curiosity", "playfulness", "boldness" };

        /// <summary>Độ lệch [-JitterMax, +JitterMax] tất định theo UID và nét.</summary>
        internal static int Jitter(string uid, string trait)
        {
            uint h = Rng.HashSeed("personality-trait", uid ?? "", trait);
            return (int)(h % (2 * JitterMax + 1)) - JitterMax;
        }

        public static PersonalityTraits TraitsOf(string uid, Personality archetype)
        {
            int[] b = Base(archetype);
            var v = new int[4];
            for (int i = 0; i < 4; i++) v[i] = Math.Max(0, Math.Min(100, b[i] + Jitter(uid, TraitIds[i])));
            return new PersonalityTraits(v[0], v[1], v[2], v[3]);
        }

        public static PersonalityProfile Of(DragonState d)
        {
            if (d == null) throw new ArgumentNullException(nameof(d));
            SpeciesBiology b = SpeciesCatalog.GetBiology(d.SpeciesId);
            return new PersonalityProfile
            {
                Uid = d.Uid, Archetype = d.Personality, Traits = TraitsOf(d.Uid, d.Personality),
                FavoriteDiet = Pick(b.DietIds, Rng.HashSeed("favorite-diet", d.Uid ?? "")),
                FavoriteHabitatId = Pick(b.HabitatRules, Rng.HashSeed("favorite-habitat", d.Uid ?? "")),
                SummaryKey = "personality." + d.Personality.ToWire() + ".summary",
            };
        }

        private static T Pick<T>(IReadOnlyList<T> items, uint h) => items[(int)(h % (uint)items.Count)];
    }
}
