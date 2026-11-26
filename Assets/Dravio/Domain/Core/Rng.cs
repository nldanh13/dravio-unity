using System;
using System.Collections.Generic;

namespace Dravio.Domain.Core
{
    /// <summary>
    /// RNG có seed, được tiêm vào engine (GDD 3.1 trang 17; src/engine/core/rng.ts).
    /// Domain không bao giờ dùng System.Random hay UnityEngine.Random.
    /// </summary>
    public interface IRng
    {
        /// <summary>Số thực trong [0, 1).</summary>
        double Next();
    }

    /// <summary>
    /// mulberry32 — cùng chuỗi số với <c>createRng(seed)</c> của TypeScript gốc cho mọi seed 32-bit.
    /// Trạng thái là một uint nên có thể lưu/khôi phục qua <see cref="State"/>.
    /// </summary>
    public sealed class Mulberry32 : IRng
    {
        private uint _a;

        public Mulberry32(uint seed) { _a = seed; }

        /// <summary>Trạng thái nội bộ hiện tại (để snapshot trong test hoặc lưu sau này).</summary>
        public uint State => _a;

        public double Next()
        {
            unchecked
            {
                _a += 0x6D2B79F5u;
                uint t = _a;
                t = (t ^ (t >> 15)) * (t | 1u);
                t ^= t + (t ^ (t >> 7)) * (t | 61u);
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        }
    }

    public static class Rng
    {
        /// <summary>createRng(seed): seed được ép về uint giống <c>seed &gt;&gt;&gt; 0</c>.</summary>
        public static IRng Create(uint seed) => new Mulberry32(seed);

        /// <summary>
        /// hashSeed(...parts): FNV-1a 32-bit theo từng đơn vị UTF-16 (giống <c>charCodeAt</c>),
        /// thêm 0x1f phân tách giữa các phần.
        /// </summary>
        public static uint HashSeed(params string[] parts)
        {
            unchecked
            {
                uint h = 0x811C9DC5u;
                foreach (string part in parts)
                {
                    for (int i = 0; i < part.Length; i++)
                    {
                        h ^= part[i];
                        h *= 0x01000193u;
                    }
                    h ^= 0x1Fu;
                    h *= 0x01000193u;
                }
                return h;
            }
        }

        public static int RandomInt(this IRng rng, int maxExclusive)
        {
            if (maxExclusive <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), maxExclusive, "maxExclusive must be a positive integer");
            return (int)Math.Floor(rng.Next() * maxExclusive);
        }

        public static T PickUniform<T>(this IRng rng, IReadOnlyList<T> items)
        {
            if (items.Count == 0) throw new ArgumentOutOfRangeException(nameof(items), "pickUniform on empty list");
            return items[rng.RandomInt(items.Count)];
        }

        /// <summary>Chọn theo trọng số không âm; trọng số ≤ 0 không bao giờ được chọn.</summary>
        public static T PickWeighted<T>(this IRng rng, IReadOnlyList<KeyValuePair<T, double>> entries)
        {
            double total = 0;
            foreach (var e in entries) total += Math.Max(0, e.Value);
            if (!(total > 0)) throw new ArgumentOutOfRangeException(nameof(entries), "pickWeighted needs a positive total weight");
            double roll = rng.Next() * total;
            foreach (var e in entries)
            {
                if (e.Value <= 0) continue;
                if (roll < e.Value) return e.Key;
                roll -= e.Value;
            }
            // Sai số dấu phẩy động: trả mục cuối có trọng số dương.
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (entries[i].Value > 0) return entries[i].Key;
            }
            throw new InvalidOperationException("unreachable");
        }
    }
}
