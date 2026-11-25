using System.Collections.Generic;
using UnityEngine;

namespace Dravio.Character
{
    /// <summary>
    /// Eyelid blink (U1-CHAR.6) driven by <see cref="BlinkScheduler"/> on a blend shape whose name contains
    /// blink / eye_close / eyeclose / eyesclosed, on every LOD's SkinnedMeshRenderer that has it. Without such a
    /// shape (the usual case for generated meshes) <see cref="HasShape"/> is false and nothing happens: eyes are
    /// never faked by moving bones. Asleep: eyes closed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DragonBlink : MonoBehaviour
    {
        private static readonly string[] ShapeWords = { "blink", "eye_close", "eyeclose", "eyesclosed" };
        private readonly List<SkinnedMeshRenderer> _renderers = new List<SkinnedMeshRenderer>();
        private readonly List<int> _indices = new List<int>();
        private BlinkScheduler _scheduler;

        public bool HasShape { get { return _indices.Count > 0; } }
        public bool Asleep { get; set; }

        private void Awake()
        {
            _renderers.Clear(); _indices.Clear();
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var m = r.sharedMesh;
                if (m == null) continue;
                for (int i = 0; i < m.blendShapeCount; i++)
                {
                    string n = m.GetBlendShapeName(i).ToLowerInvariant();
                    bool hit = false;
                    foreach (var w in ShapeWords) if (n.Contains(w)) { hit = true; break; }
                    if (hit) { _renderers.Add(r); _indices.Add(i); break; }
                }
            }
            _scheduler = new BlinkScheduler(DragonBehaviorPlanner.SeedFor(name));
            if (!HasShape) enabled = false;
        }

        private void Update()
        {
            float w = _scheduler.Update(Time.deltaTime, Asleep) * 100f;
            for (int i = 0; i < _renderers.Count; i++)
                if (_renderers[i] != null) _renderers[i].SetBlendShapeWeight(_indices[i], w);
        }
    }
}
