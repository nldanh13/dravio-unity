using System;
using System.Collections.Generic;
using Dravio.Character.Integration;
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
    public sealed class DragonVisualManager : MonoBehaviour, IDragonBehaviorTarget
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

        public int Count { get { return _roster != null ? _roster.Count : 0; } }
        public int PoolCreated { get { return _roster != null ? _roster.Created : 0; } }
        public int PoolReused { get { return _roster != null ? _roster.Reused : 0; } }
        /// <summary>Instances destroyed because their prefab's free list was full.</summary>
        public int PoolDestroyed { get { return _roster != null ? _roster.Destroyed : 0; } }
        public int EvaluatedLastFrame { get; private set; }
        public int VisibleLastFrame { get; private set; }

        private DragonVisualRoster<GameObject, DragonVisualInstance> _roster;
        private readonly List<DragonVisualInstance> _iter = new List<DragonVisualInstance>();
        private readonly Dictionary<string, DragonVisualSpawn> _spawns = new Dictionary<string, DragonVisualSpawn>(StringComparer.Ordinal);
        private Transform _poolRoot;
        private readonly Plane[] _planes = new Plane[6];
        private int _nextPhase;

        private void Awake() { EnsurePool(); }

        private void EnsurePool()
        {
            if (_roster != null) return;
            var root = new GameObject("DragonVisualPool");
            root.SetActive(false);
            root.transform.SetParent(transform, false);
            _poolRoot = root.transform;
            _roster = new DragonVisualRoster<GameObject, DragonVisualInstance>(CreateInstance,
                i => { i.transform.SetParent(transform, false); },
                i => { i.ResetForPool(); i.transform.SetParent(_poolRoot, false); },
                i => { if (i != null) Destroy(i.gameObject); },
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
            DragonVisualRoster<GameObject, DragonVisualInstance>.AcquireResult how;
            DragonVisualInstance inst = _roster.Acquire(spawn.uid, entry.prefab, out how);
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
            if (!_spawns.TryGetValue(uid, out s) || _roster == null || !_roster.TryGet(uid, out inst)) return null;
            s.stage = stage;
            s.state = inst.State;
            return Spawn(s);
        }

        public bool ApplyState(string uid, DragonVisualState state)
        {
            DragonVisualInstance inst;
            if (_roster == null || !_roster.TryGet(uid, out inst)) return false;
            if (!inst.Apply(state))
                Warn(uid, inst.Entry.speciesId + " has no " + state.locomotion + " clip; showing " + inst.ShownLocomotion);
            return true;
        }

        public bool Trigger(string uid, DragonInteraction interaction)
        {
            DragonVisualInstance inst;
            if (_roster == null || !_roster.TryGet(uid, out inst) || catalog == null) return false;
            bool ok = inst.Trigger(interaction, catalog.InteractionSeconds(inst.Entry, interaction));
            if (!ok) Warn(uid, inst.Entry.speciesId + " cannot play " + interaction);
            return ok;
        }

        public bool TryGetAnchor(string uid, string anchorName, out Transform anchor)
        {
            DragonVisualInstance inst;
            anchor = null;
            return _roster != null && _roster.TryGet(uid, out inst) && inst.TryGetAnchor(anchorName, out anchor);
        }

        public bool TryGet(string uid, out DragonVisualInstance instance)
        {
            instance = null;
            return _roster != null && _roster.TryGet(uid, out instance);
        }

        public bool IsLive(string uid) { DragonVisualInstance i; return TryGet(uid, out i); }

        public void Despawn(string uid)
        {
            if (uid == null) return;
            if (_roster != null) _roster.Release(uid);
            _spawns.Remove(uid);
        }

        public void DespawnAll()
        {
            if (_roster != null) _roster.ReleaseAll();
            _spawns.Clear();
        }

        bool IDragonVisualTarget.Spawn(DragonVisualSpawn spawn) { return Spawn(spawn) != null; }

        // ---- U1-CHAR.6 behaviour support (IDragonBehaviorTarget) ----
        public DragonAnimationMap ClipsOf(string uid) { DragonVisualInstance i; return TryGet(uid, out i) ? i.Clips : null; }

        public float ClipSeconds(string uid, DragonInteraction clip)
        {
            DragonVisualInstance i;
            return TryGet(uid, out i) ? DragonVisualCatalog.ClipSecondsOf(i.Entry, clip) : 0f;
        }

        public void SetTempo(string uid, float tempo) { DragonVisualInstance i; if (TryGet(uid, out i)) i.SetTempo(tempo); }

        public void SetLook(string uid, bool active, Vector3 worldTarget, float weight, float hold)
        {
            DragonVisualInstance i;
            if (TryGet(uid, out i)) i.SetLook(active, worldTarget, weight, hold);
        }
        bool IDragonVisualTarget.SetStage(string uid, DragonVisualStage stage) { return SetStage(uid, stage) != null; }

        private void Update()
        {
            var cam = viewCamera != null ? viewCamera : Camera.main;
            Vector3 camPos = cam != null ? cam.transform.position : Vector3.zero;
            if (cam != null) GeometryUtility.CalculateFrustumPlanes(cam, _planes);
            int frame = Time.frameCount;
            float dt = Time.deltaTime;
            int evaluated = 0, visible = 0;
            _iter.Clear();
            if (_roster != null)
                foreach (var uid in _roster.Uids) { DragonVisualInstance i; if (_roster.TryGet(uid, out i)) _iter.Add(i); }
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
            if (_roster == null) return;
            foreach (var i in _roster.DrainPool()) if (i != null) Destroy(i.gameObject);
        }

        private void Warn(string uid, string message)
        {
            var w = Warning;
            if (w != null) w(uid, message);
            else Debug.LogWarning("[DragonVisual] " + uid + ": " + message, this);
        }
    }
}
