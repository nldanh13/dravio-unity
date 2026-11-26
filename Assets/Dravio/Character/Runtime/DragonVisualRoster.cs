using System;
using System.Collections.Generic;

namespace Dravio.Character
{
    /// <summary>
    /// UID → displayed instance bookkeeping for <see cref="DragonVisualManager"/>, separated from Unity so spawn,
    /// despawn, stage switching and pooling are unit-testable. <typeparamref name="TKey"/> is the prefab,
    /// <typeparamref name="TInst"/> the pooled instance. Invariants (tested):
    ///   - one live instance per UID, and one UID per live instance;
    ///   - re-spawning a UID with the same prefab keeps its instance (no flicker, no pool churn);
    ///   - a prefab change (growth to a stage with its own model) returns the old instance to the pool first;
    ///   - an instance is never in the pool and live at the same time; despawning twice is harmless;
    ///   - when the free list of a prefab is full the instance is handed to <c>destroy</c>.
    /// </summary>
    public sealed class DragonVisualRoster<TKey, TInst> where TInst : class
    {
        public enum AcquireResult { Created, Reused, Kept, Swapped }

        private readonly KeyedPool<TKey, TInst> _pool;
        private readonly Action<TInst> _destroy;
        private readonly Dictionary<string, Slot> _live = new Dictionary<string, Slot>(StringComparer.Ordinal);
        private readonly HashSet<TInst> _liveSet = new HashSet<TInst>();
        private readonly IEqualityComparer<TKey> _keys = EqualityComparer<TKey>.Default;

        private struct Slot { public TKey key; public TInst inst; }

        public DragonVisualRoster(Func<TKey, TInst> create, Action<TInst> onGet, Action<TInst> onRelease, Action<TInst> destroy, int maxFreePerKey)
        {
            _pool = new KeyedPool<TKey, TInst>(create, onGet, onRelease, maxFreePerKey);
            _destroy = destroy;
        }

        public int Count { get { return _live.Count; } }
        public int Created { get { return _pool.Created; } }
        public int Reused { get { return _pool.Reused; } }
        public int Destroyed { get; private set; }
        public int FreeCount(TKey key) { return _pool.FreeCount(key); }
        public IEnumerable<string> Uids { get { return _live.Keys; } }

        public bool TryGet(string uid, out TInst inst)
        {
            Slot s;
            if (uid != null && _live.TryGetValue(uid, out s)) { inst = s.inst; return true; }
            inst = null;
            return false;
        }

        public bool TryGetKey(string uid, out TKey key)
        {
            Slot s;
            if (uid != null && _live.TryGetValue(uid, out s)) { key = s.key; return true; }
            key = default(TKey);
            return false;
        }

        /// <summary>Instance for <paramref name="uid"/> showing <paramref name="key"/>.</summary>
        public TInst Acquire(string uid, TKey key, out AcquireResult result)
        {
            if (string.IsNullOrEmpty(uid)) throw new ArgumentException("uid required");
            Slot s;
            bool swapped = false;
            if (_live.TryGetValue(uid, out s))
            {
                if (_keys.Equals(s.key, key)) { result = AcquireResult.Kept; return s.inst; }
                ReleaseSlot(s);
                _live.Remove(uid);
                swapped = true;
            }
            int reusedBefore = _pool.Reused;
            TInst inst = _pool.Get(key);
            if (_liveSet.Contains(inst)) throw new InvalidOperationException("pool returned a live instance");
            _liveSet.Add(inst);
            _live[uid] = new Slot { key = key, inst = inst };
            result = swapped ? AcquireResult.Swapped : (_pool.Reused > reusedBefore ? AcquireResult.Reused : AcquireResult.Created);
            return inst;
        }

        /// <summary>Returns the UID's instance to the pool. False if the UID was not live.</summary>
        public bool Release(string uid)
        {
            Slot s;
            if (uid == null || !_live.TryGetValue(uid, out s)) return false;
            _live.Remove(uid);
            ReleaseSlot(s);
            return true;
        }

        public void ReleaseAll()
        {
            var uids = new List<string>(_live.Keys);
            foreach (var u in uids) Release(u);
        }

        /// <summary>Free (pooled) instances, emptied; for teardown.</summary>
        public IEnumerable<TInst> DrainPool() { return _pool.DrainAll(); }

        private void ReleaseSlot(Slot s)
        {
            _liveSet.Remove(s.inst);
            if (!_pool.Release(s.key, s.inst))
            {
                Destroyed++;
                if (_destroy != null) _destroy(s.inst);
            }
        }
    }
}
