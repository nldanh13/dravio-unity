using System;
using System.Collections;
using System.Collections.Generic;

namespace Dravio.Domain
{
    /// <summary>Hai allele tại một locus, một từ mỗi bố mẹ. Thứ tự đã chuẩn hóa: allele trội đứng trước.</summary>
    public readonly struct AllelePair : IEquatable<AllelePair>
    {
        public AllelePair(string first, string second) { First = first; Second = second; }
        public string First { get; }
        public string Second { get; }
        public bool Equals(AllelePair other) => string.Equals(First, other.First, StringComparison.Ordinal) && string.Equals(Second, other.Second, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is AllelePair p && Equals(p);
        public override int GetHashCode() => ((First?.GetHashCode() ?? 0) * 397) ^ (Second?.GetHashCode() ?? 0);
        public override string ToString() => First + Second;
    }

    /// <summary>
    /// Genotype = map locusId → AllelePair (src/types/game.ts Genotype), giữ thứ tự chèn để snapshot ổn định.
    /// Kiểu hình là giá trị dẫn xuất và không được lưu ở đây.
    /// </summary>
    public sealed class Genotype : IEnumerable<KeyValuePair<string, AllelePair>>
    {
        private readonly List<KeyValuePair<string, AllelePair>> _entries = new List<KeyValuePair<string, AllelePair>>();

        public int Count => _entries.Count;

        public IEnumerable<string> LocusIds
        {
            get { foreach (var e in _entries) yield return e.Key; }
        }

        /// <summary>Gán (hoặc thay) cặp allele cho một locus.</summary>
        public void Set(string locusId, AllelePair pair)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (string.Equals(_entries[i].Key, locusId, StringComparison.Ordinal))
                {
                    _entries[i] = new KeyValuePair<string, AllelePair>(locusId, pair);
                    return;
                }
            }
            _entries.Add(new KeyValuePair<string, AllelePair>(locusId, pair));
        }

        public void Set(string locusId, string first, string second) => Set(locusId, new AllelePair(first, second));

        public bool TryGet(string locusId, out AllelePair pair)
        {
            foreach (var e in _entries)
            {
                if (string.Equals(e.Key, locusId, StringComparison.Ordinal)) { pair = e.Value; return true; }
            }
            pair = default;
            return false;
        }

        public Genotype Clone()
        {
            var g = new Genotype();
            g._entries.AddRange(_entries);
            return g;
        }

        public IEnumerator<KeyValuePair<string, AllelePair>> GetEnumerator() => _entries.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
