using System;
using System.Collections.Generic;
using Dravio.Domain.Core;

namespace Dravio.Domain.Behavior
{
    // U2.11 (ADR 0206) — Quan hệ giữa các rồng, phần LƯU (đề xuất S7 của ADR 0204, giờ chạy thật).
    // Độ quen theo thời gian sống chung: mỗi giờ hai cá thể cùng ở một microhabitat cộng 1 điểm, trần 40 điểm (40 giờ). Không giảm, kể cả khi offline
    // hay khi tách nhau. CHỈ TRÌNH DIỄN: không đổi vàng, XP, bond, vitals hay comfort (S8 vẫn TẮT, SocialRules.Production = 0).

    /// <summary>Bậc quan hệ hiển thị, theo độ quen tổng (phả hệ + cùng chỗ + cùng loài + thời gian sống chung).</summary>
    public enum RelationTier
    {
        /// <summary>0–19.</summary>
        [WireName("stranger")] Stranger,
        /// <summary>20–49.</summary>
        [WireName("acquainted")] Acquainted,
        /// <summary>50–79.</summary>
        [WireName("friend")] Friend,
        /// <summary>80–100.</summary>
        [WireName("close")] Close,
    }

    public static class RelationRules
    {
        /// <summary>Một điểm độ quen cho mỗi giờ sống chung.</summary>
        public const long MsPerPoint = 3_600_000;
        /// <summary>Trần điểm từ thời gian sống chung.</summary>
        public const int TimeCap = 40;
        public const long MaxSharedMs = MsPerPoint * TimeCap;
        /// <summary>Số cặp tối đa được lưu (giữ save nhỏ). Khi vượt, bỏ cặp có ít thời gian nhất (hòa: thứ tự khóa).</summary>
        public const int MaxPairs = 600;
        public const int AcquaintedAt = 20, FriendAt = 50, CloseAt = 80;

        public static RelationTier TierOf(int familiarity) =>
            familiarity >= CloseAt ? RelationTier.Close : familiarity >= FriendAt ? RelationTier.Friend : familiarity >= AcquaintedAt ? RelationTier.Acquainted : RelationTier.Stranger;

        public static int TimePoints(long sharedMs) => (int)Math.Min(TimeCap, Math.Max(0, sharedMs) / MsPerPoint);
    }

    /// <summary>Snapshot để lưu (Schema v10, khối <c>relations</c>).</summary>
    public sealed class RelationLogSnapshot
    {
        public sealed class Pair
        {
            public string A;
            public string B;
            public long SharedMs;
        }
        public List<Pair> Pairs = new List<Pair>();
    }

    /// <summary>
    /// Thời gian sống chung theo cặp (Schema v10). Khóa cặp luôn theo thứ tự ordinal (A &lt; B). Tất định: chỉ phụ thuộc vị trí rồng
    /// và thời gian đã quyết toán.
    /// </summary>
    public sealed class RelationLog
    {
        private readonly SortedDictionary<string, long> _ms = new SortedDictionary<string, long>(StringComparer.Ordinal);

        public static string Key(string a, string b) => string.CompareOrdinal(a, b) <= 0 ? a + "|" + b : b + "|" + a;

        public long SharedMs(string a, string b) => a == null || b == null ? 0 : _ms.TryGetValue(Key(a, b), out long v) ? v : 0;
        public int Count => _ms.Count;
        public bool IsEmpty => _ms.Count == 0;

        public RelationLog Clone()
        {
            var c = new RelationLog();
            foreach (var kv in _ms) c._ms[kv.Key] = kv.Value;
            return c;
        }

        private static bool Present(GameState s, DragonState d) =>
            d.Location.Kind == LocationKind.Zone && d.Activity != DragonActivity.Expedition;

        /// <summary>
        /// Cộng <paramref name="elapsedMs"/> cho mọi cặp đang cùng microhabitat (không tính rồng đi thám hiểm, ở trạm chăm sóc, nơi trú, kho hay đã thả).
        /// Gọi trong quyết toán thời gian, trước khi vị trí đổi. Không sinh event.
        /// </summary>
        public void Accrue(GameState s, long elapsedMs)
        {
            if (elapsedMs <= 0 || s.Dragons == null) return;
            var here = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (DragonState d in s.Dragons)
            {
                if (!Present(s, d)) continue;
                string k = d.Location.ZoneId + "/" + d.Location.HabitatId;
                if (!here.TryGetValue(k, out var list)) here[k] = list = new List<string>();
                list.Add(d.Uid);
            }
            foreach (var kv in here)
            {
                List<string> uids = kv.Value;
                if (uids.Count < 2) continue;
                uids.Sort(StringComparer.Ordinal);
                for (int i = 0; i < uids.Count; i++)
                    for (int j = i + 1; j < uids.Count; j++)
                    {
                        string key = uids[i] + "|" + uids[j];
                        long cur = _ms.TryGetValue(key, out long v) ? v : 0;
                        if (cur >= RelationRules.MaxSharedMs) continue;
                        _ms[key] = Math.Min(RelationRules.MaxSharedMs, cur + elapsedMs);
                    }
            }
            Trim();
        }

        /// <summary>Bỏ cặp có cá thể đã bán / thả / không còn trong kho. Trả số cặp đã bỏ.</summary>
        public int Prune(GameState s)
        {
            var gone = new List<string>();
            foreach (string key in _ms.Keys)
            {
                int bar = key.IndexOf('|');
                DragonState a = s.FindDragon(key.Substring(0, bar)), b = s.FindDragon(key.Substring(bar + 1));
                if (a == null || b == null || a.Location.Kind == LocationKind.Released || b.Location.Kind == LocationKind.Released) gone.Add(key);
            }
            foreach (string k in gone) _ms.Remove(k);
            return gone.Count;
        }

        private void Trim()
        {
            if (_ms.Count <= RelationRules.MaxPairs) return;
            var all = new List<KeyValuePair<string, long>>(_ms);
            all.Sort((x, y) => x.Value != y.Value ? x.Value.CompareTo(y.Value) : string.CompareOrdinal(x.Key, y.Key));
            for (int i = 0; i < all.Count - RelationRules.MaxPairs; i++) _ms.Remove(all[i].Key);
        }

        public IEnumerable<KeyValuePair<string, long>> All => _ms;

        public RelationLogSnapshot ToSnapshot()
        {
            var snap = new RelationLogSnapshot();
            foreach (var kv in _ms)
            {
                int bar = kv.Key.IndexOf('|');
                snap.Pairs.Add(new RelationLogSnapshot.Pair { A = kv.Key.Substring(0, bar), B = kv.Key.Substring(bar + 1), SharedMs = kv.Value });
            }
            return snap;
        }

        public static RelationLog FromSnapshot(RelationLogSnapshot snap)
        {
            var log = new RelationLog();
            if (snap == null) return log;
            foreach (var p in snap.Pairs) if (p != null && p.A != null && p.B != null && p.A != p.B && p.SharedMs > 0) log._ms[Key(p.A, p.B)] = Math.Min(RelationRules.MaxSharedMs, p.SharedMs);
            log.Trim();
            return log;
        }

        /// <summary>
        /// Làm sạch khi nạp / ghi (giống sổ nghiên cứu): bỏ cặp trùng, cặp tự với chính nó, cá thể không còn trong kho, thời gian âm hoặc 0;
        /// kẹp về trần. Quan hệ không chạm vàng / rồng / vật liệu nên KHÔNG BAO GIỜ làm save bị từ chối.
        /// </summary>
        public static RelationLogSnapshot Sanitize(RelationLogSnapshot snap, GameState s)
        {
            var o = new RelationLogSnapshot();
            if (snap == null) return o;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var kept = new List<RelationLogSnapshot.Pair>();
            foreach (var p in snap.Pairs)
            {
                if (p == null || p.A == null || p.B == null || p.A == p.B || p.SharedMs <= 0) continue;
                DragonState a = s.FindDragon(p.A), b = s.FindDragon(p.B);
                if (a == null || b == null || a.Location.Kind == LocationKind.Released || b.Location.Kind == LocationKind.Released) continue;
                string k = Key(p.A, p.B);
                if (!seen.Add(k)) continue;
                bool ordered = string.CompareOrdinal(p.A, p.B) < 0;
                kept.Add(new RelationLogSnapshot.Pair { A = ordered ? p.A : p.B, B = ordered ? p.B : p.A, SharedMs = Math.Min(RelationRules.MaxSharedMs, p.SharedMs) });
            }
            // Thứ tự khóa + trần số cặp: đi qua FromSnapshot để dùng đúng một luật Trim.
            o.Pairs = FromSnapshot(new RelationLogSnapshot { Pairs = kept }).ToSnapshot().Pairs;
            return o;
        }
    }
}
