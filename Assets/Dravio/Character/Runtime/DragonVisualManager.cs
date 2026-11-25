using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dravio.Character
{
    /// <summary>
    /// Living-dragon presentation API for the integration layer. Keyed by UID; any number of instances of the
    /// same species/stage. The integration layer owns simulation (position, locomotion, interactions) and
    /// pushes it here; this component only displays it:
    ///   Spawn(spawn) / Despawn(uid) / SetStage(uid, stage) / ApplyState(uid, state) / Trigger(uid, interaction)
    ///   TryGetAnchor(uid, "VFX_Mouth", out t) for VFX placement.
    /// Performance: prefab pooling, Animator CullUpdateTransforms, per-instance update interval from
    /// <see cref="UpdateRatePolicy"/> (visible/near every frame, far and off-screen throttled with staggered
    /// phases), LODGroup on every prefab for distance LOD, foot IK only near the camera.
    /// No Domain, Persistence, HUD or scene dependency.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DragonVisualManager : MonoBehaviour
    {
        public DragonVisualCatalog catalog;
        [Tooltip("Optional. Empty or missing = no genotype/variant visual changes (none are defined in the GDD yet)")]
        public PhenotypeProfile phenotypeProfile;
        [Tooltip("Defaults to Camera.main")] public Camera viewCamera;
        public UpdateRatePolicy updatePolicy = new UpdateRatePolicy();
        public bool footIk = true;
        public float footIkDistance = 10f;
        public int maxPooledPerPrefab = 16;

        /// <summary>uid, message. Raised for fallbacks (missing stage model, unsupported locomotion, ...).</summary>
        public event Action<string, string> Warning;

        public int Count { get { return _live.Count; } }
        public int PoolCreated { get { return _pool != null ? _pool.Created : 0; } }
        public int PoolReused { get { return _pool != null ? _pool.Reused : 0; } }
        public int EvaluatedLastFrame { get; private set; }
        public int VisibleLastFrame { get; private set; }

        private readonly Dictionary<string, DragonVisualInstance> _live = new Dictionary<string, DragonVisualInstance>(StringComparer.Ordinal);
        private readonly List<DragonVisualInstance> _iter = new List<DragonVisualInstance>();
        private readonly Dictionary<string, DragonVisualSpawn> _spawns = new Dictionary<string, DragonVisualSpawn>(StringComparer.Ordinal);
        private KeyedPool<GameObject, DragonVisualInstance> _pool;
        private Transform _poolRoot;
        private readonly Plane[] _planes = new Plane[6];
        private int _nextPhase;

        private void Awake() { EnsurePool(); }

        private void EnsurePool()
        {
            if (_pool != null) return;
            var root = new GameObject("DragonVisualPool");
            root.SetActive(false);
            root.transform.SetParent(transform, false);
            _poolRoot = root.transform;
            _pool = new KeyedPool<GameObject, DragonVisualInstance>(CreateInstance,
                i => { i.transform.SetParent(transform, false); },
                i => { i.ResetForPool(); i.transform.SetParent(_poolRoot, false); },
                maxPooledPerPrefab);
        }

        private DragonVisualInstance CreateInstance(GameObject prefab)
        {
            var go = Instantiate(prefab, _poolRoot);
            var inst = go.GetComponent<DragonVisualInstance>();
            if (inst == null) inst = go.AddComponent<DragonVisualInstance>();
            inst.Bind(prefab, _nextPhase++);
            return inst;
        }

        public DragonVisualInstance Spawn(DragonVisualSpawn spawn)
        {
            if (spawn == null || string.IsNullOrEmpty(spawn.uid)) throw new ArgumentException("spawn.uid required");
            EnsurePool();
            if (catalog == null) { Warn(spawn.uid, "No DragonVisualCatalog assigned"); return null; }
            DragonVisualCatalog.Match match;
            var entry = catalog.Resolve(spawn.speciesId, spawn.stage, out match);
            if (entry == null || entry.prefab == null)
            {
                Warn(spawn.uid, "No visual for " + spawn.speciesId + "/" + spawn.stage + " in catalog");
                Despawn(spawn.uid);
                return null;
            }
            if (match != DragonVisualCatalog.Match.Exact)
                Warn(spawn.uid, spawn.speciesId + "/" + spawn.stage + " has no exact model; using " + entry.stage + " (" + match + ")");
            DragonVisualInstance inst;
            if (_live.TryGetValue(spawn.uid, out inst) && inst.SourcePrefab != entry.prefab)
            {
                Release(inst);
                inst = null;
            }
            if (inst == null)
            {
                inst = _pool.Get(entry.prefab);
                _live[spawn.uid] = inst;
            }
            _spawns[spawn.uid] = spawn;
            inst.Setup(spawn.uid, entry, match, phenotypeProfile, spawn);
            ApplyState(spawn.uid, spawn.state);
            return inst;
        }

        /// <summary>Growth: swaps to the stage's model (pooled) and keeps the current state.</summary>
        public DragonVisualInstance SetStage(string uid, DragonVisualStage stage)
        {
            DragonVisualSpawn s;
            DragonVisualInstance inst;
            if (!_spawns.TryGetValue(uid, out s) || !_live.TryGetValue(uid, out inst)) return null;
            s.stage = stage;
            s.state = inst.State;
            return Spawn(s);
        }

        public bool ApplyState(string uid, DragonVisualState state)
        {
            DragonVisualInstance inst;
            if (!_live.TryGetValue(uid, out inst)) return false;
            if (!inst.Apply(state))
                Warn(uid, inst.Entry.speciesId + " has no " + state.locomotion + " clip; showing " + inst.ShownLocomotion);
            return true;
        }

        public bool Trigger(string uid, DragonInteraction interaction)
        {
            DragonVisualInstance inst;
            if (!_live.TryGetValue(uid, out inst) || catalog == null) return false;
            bool ok = inst.Trigger(interaction, catalog.InteractionSeconds(inst.Entry, interaction));
            if (!ok) Warn(uid, inst.Entry.speciesId + " cannot play " + interaction);
            return ok;
        }

        public bool TryGetAnchor(string uid, string anchorName, out Transform anchor)
        {
            DragonVisualInstance inst;
            anchor = null;
            return _live.TryGetValue(uid, out inst) && inst.TryGetAnchor(anchorName, out anchor);
        }

        public bool TryGet(string uid, out DragonVisualInstance instance) { return _live.TryGetValue(uid, out instance); }

        public void Despawn(string uid)
        {
            DragonVisualInstance inst;
            if (_live.TryGetValue(uid, out inst)) Release(inst);
            _live.Remove(uid);
            _spawns.Remove(uid);
        }

        public void DespawnAll()
        {
            _iter.Clear();
            _iter.AddRange(_live.Values);
            foreach (var i in _iter) Release(i);
            _live.Clear();
            _spawns.Clear();
        }

        private void Release(DragonVisualInstance inst)
        {
            if (inst == null) return;
            if (!_pool.Release(inst.SourcePrefab, inst)) Destroy(inst.gameObject);
        }

        private void Update()
        {
            var cam = viewCamera != null ? viewCamera : Camera.main;
            Vector3 camPos = cam != null ? cam.transform.position : Vector3.zero;
            if (cam != null) GeometryUtility.CalculateFrustumPlanes(cam, _planes);
            int frame = Time.frameCount;
            float dt = Time.deltaTime;
            int evaluated = 0, visible = 0;
            _iter.Clear();
            _iter.AddRange(_live.Values);
            foreach (var inst in _iter)
            {
                var bounds = inst.WorldBounds();
                bool vis = cam == null || GeometryUtility.TestPlanesAABB(_planes, bounds);
                float dist = cam != null ? Vector3.Distance(camPos, bounds.center) : 0f;
                int interval = updatePolicy.IntervalFor(vis, dist);
                if (vis) visible++;
                if (inst.Tick(dt, frame, interval, vis, footIk && dist <= footIkDistance)) evaluated++;
            }
            EvaluatedLastFrame = evaluated;
            VisibleLastFrame = visible;
        }

        private void OnDestroy()
        {
            if (_pool == null) return;
            foreach (var i in _pool.DrainAll()) if (i != null) Destroy(i.gameObject);
        }

        private void Warn(string uid, string message)
        {
            var w = Warning;
            if (w != null) w(uid, message);
            else Debug.LogWarning("[DragonVisual] " + uid + ": " + message, this);
        }
    }
}
