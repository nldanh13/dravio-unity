using System;
using System.Collections.Generic;

namespace Dravio.Character
{
    /// <summary>Pool of reusable objects per key (e.g. per prefab). Pure bookkeeping; the caller supplies
    /// create/activate/deactivate callbacks. Not thread-safe (main thread only).</summary>
    public sealed class KeyedPool<TKey, T> where T : class
    {
        private readonly Dictionary<TKey, Stack<T>> _free = new Dictionary<TKey, Stack<T>>();
        private readonly Func<TKey, T> _create;
        private readonly Action<T> _onGet;
        private readonly Action<T> _onRelease;
        private readonly int _maxFreePerKey;

        public int Created { get; private set; }
        public int Reused { get; private set; }

        public KeyedPool(Func<TKey, T> create, Action<T> onGet, Action<T> onRelease, int maxFreePerKey = 32)
        {
            if (create == null) throw new ArgumentNullException("create");
            _create = create; _onGet = onGet; _onRelease = onRelease; _maxFreePerKey = maxFreePerKey;
        }

        public T Get(TKey key)
        {
            Stack<T> stack;
            T item;
            if (_free.TryGetValue(key, out stack) && stack.Count > 0) { item = stack.Pop(); Reused++; }
            else { item = _create(key); Created++; }
            if (_onGet != null) _onGet(item);
            return item;
        }

        /// <summary>Returns false when the free list is full; the caller should destroy the object.</summary>
        public bool Release(TKey key, T item)
        {
            if (item == null) return true;
            if (_onRelease != null) _onRelease(item);
            Stack<T> stack;
            if (!_free.TryGetValue(key, out stack)) _free[key] = stack = new Stack<T>();
            if (stack.Count >= _maxFreePerKey) return false;
            stack.Push(item);
            return true;
        }

        public int FreeCount(TKey key)
        {
            Stack<T> stack;
            return _free.TryGetValue(key, out stack) ? stack.Count : 0;
        }

        public IEnumerable<T> DrainAll()
        {
            foreach (var s in _free.Values) while (s.Count > 0) yield return s.Pop();
        }
    }
}
