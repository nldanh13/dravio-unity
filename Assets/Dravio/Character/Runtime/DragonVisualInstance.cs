using System.Collections.Generic;
using UnityEngine;

namespace Dravio.Character
{
    /// <summary>
    /// One displayed dragon (pooled). Holds the Animator, LOD renderers and VFX anchors of a catalog prefab,
    /// presents the state pushed by <see cref="DragonVisualManager"/> and never decides behaviour itself.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DragonVisualInstance : MonoBehaviour
    {
        public string Uid { get; private set; }
        public DragonVisualCatalog.Entry Entry { get; private set; }
        public DragonVisualCatalog.Match Match { get; private set; }
        public DragonVisualState State { get; private set; }
        public DragonLocomotion ShownLocomotion { get; private set; }
        public DragonInteraction CurrentInteraction { get; private set; }
        public GameObject SourcePrefab { get; private set; }
        public int LastInterval { get; private set; }
        public bool LastVisible { get; private set; }

        private Animator _animator;
        private Renderer[] _renderers;
        private DragonFootGrounding _grounding;
        private DragonAnimationMap _map;
        private readonly Dictionary<string, Transform> _anchors = new Dictionary<string, Transform>();
        private MaterialPropertyBlock _block;
        private bool _hasBlock;
        private float _actionTimer;
        private float _accumulated;
        private int _phase;
        private bool _hasParams;
        private DragonLocomotion? _lastSubstitution;

        internal void Bind(GameObject sourcePrefab, int phase)
        {
            SourcePrefab = sourcePrefab;
            _phase = phase;
            _animator = GetComponentInChildren<Animator>(true);
            _renderers = GetComponentsInChildren<Renderer>(true);
            _grounding = GetComponent<DragonFootGrounding>();
            if (_animator != null)
            {
                _animator.keepAnimatorStateOnDisable = true; // throttling disables it; do not reset the state machine
                _animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                _hasParams = _animator.runtimeAnimatorController != null;
            }
            _anchors.Clear();
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("VFX_", System.StringComparison.Ordinal) && !_anchors.ContainsKey(t.name))
                    _anchors.Add(t.name, t);
        }

        internal void Setup(string uid, DragonVisualCatalog.Entry entry, DragonVisualCatalog.Match match,
                            PhenotypeProfile profile, DragonVisualSpawn spawn)
        {
            Uid = uid;
            Entry = entry;
            Match = match;
            name = "Dragon_" + uid;
            _map = new DragonAnimationMap(entry.rigged ? entry.clips : null);
            CurrentInteraction = DragonInteraction.None;
            _actionTimer = 0f;
            _accumulated = 0f;
            _lastSubstitution = null;
            ApplyPhenotype(profile, spawn);
            if (_grounding != null) _grounding.Configure(entry.ikChains);
        }

        public bool TryGetAnchor(string anchorName, out Transform anchor)
        {
            return _anchors.TryGetValue(anchorName, out anchor);
        }

        public IEnumerable<string> AnchorNames { get { return _anchors.Keys; } }

        /// <returns>false the first time a requested locomotion has to be substituted (species lacks that clip);
        /// repeated requests for the same unsupported state do not report again.</returns>
        internal bool Apply(DragonVisualState state)
        {
            State = state;
            transform.SetPositionAndRotation(state.position, state.rotation);
            bool substituted;
            ShownLocomotion = _map.Resolve(state.locomotion, out substituted);
            if (_animator != null && _hasParams && _map.IsAnimated)
            {
                _animator.SetInteger(DragonAnimatorParams.LocomotionHash, (int)ShownLocomotion);
                _animator.SetFloat(DragonAnimatorParams.SpeedHash, state.speed);
                _animator.SetFloat(DragonAnimatorParams.PlaybackRateHash,
                    DragonAnimationMap.PlaybackRate(ShownLocomotion, state.speed, Entry.locomotionReferenceSpeed));
            }
            if (!substituted || !_map.IsAnimated) { _lastSubstitution = null; return true; }
            if (_lastSubstitution == state.locomotion) return true;
            _lastSubstitution = state.locomotion;
            return false;
        }

        internal bool Trigger(DragonInteraction interaction, float seconds)
        {
            if (!_map.CanPlay(interaction) || _animator == null || !_hasParams) return false;
            CurrentInteraction = interaction;
            _actionTimer = seconds > 0f ? seconds : 1f;
            _animator.SetInteger(DragonAnimatorParams.ActionHash, (int)interaction);
            return true;
        }

        /// <summary>interval: 1 = every frame (Animator enabled), N = manual evaluation every N frames,
        /// 0 = not evaluated (time still accumulates, capped, so the pose resumes plausibly).</summary>
        internal bool Tick(float dt, int frame, int interval, bool visible, bool footIk)
        {
            LastInterval = interval;
            LastVisible = visible;
            if (CurrentInteraction != DragonInteraction.None)
            {
                _actionTimer -= dt;
                if (_actionTimer <= 0f)
                {
                    CurrentInteraction = DragonInteraction.None;
                    if (_animator != null && _hasParams) _animator.SetInteger(DragonAnimatorParams.ActionHash, 0);
                }
            }
            if (_grounding != null)
                _grounding.enabled = footIk && interval == 1 && DragonFootGrounding.AppliesTo(ShownLocomotion, CurrentInteraction);
            if (_animator == null || !_map.IsAnimated) return false;
            if (interval == 1)
            {
                if (!_animator.enabled)
                {
                    if (_accumulated > 0f) _animator.Update(_accumulated);
                    _animator.enabled = true;
                }
                _accumulated = 0f;
                return true;
            }
            _animator.enabled = false;
            _accumulated = Mathf.Min(_accumulated + dt, 4f);
            if (UpdateRatePolicy.ShouldUpdate(frame, _phase, interval))
            {
                _animator.Update(_accumulated);
                _accumulated = 0f;
                return true;
            }
            return false;
        }

        public Bounds WorldBounds()
        {
            var b = new Bounds(transform.position, Vector3.zero);
            bool any = false;
            if (_renderers != null)
                foreach (var r in _renderers)
                {
                    if (r == null) continue;
                    if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
                }
            return b;
        }

        private void ApplyPhenotype(PhenotypeProfile profile, DragonVisualSpawn spawn)
        {
            if (_hasBlock)
            {
                foreach (var r in _renderers) if (r != null) r.SetPropertyBlock(null);
                _hasBlock = false;
            }
            if (profile == null || spawn == null) return;
            var overrides = profile.Resolve(Entry.speciesId, spawn.variantId, spawn.genotype);
            if (overrides.Count == 0) return;
            if (_block == null) _block = new MaterialPropertyBlock();
            _block.Clear();
            foreach (var o in overrides)
            {
                if (o.kind == PhenotypeProfile.ValueKind.Color) _block.SetColor(o.property, o.color);
                else _block.SetFloat(o.property, o.value);
            }
            foreach (var r in _renderers) if (r != null) r.SetPropertyBlock(_block);
            _hasBlock = true;
        }

        internal void ResetForPool()
        {
            if (_hasBlock) { foreach (var r in _renderers) if (r != null) r.SetPropertyBlock(null); _hasBlock = false; }
            CurrentInteraction = DragonInteraction.None;
            if (_animator != null)
            {
                _animator.enabled = true;
                if (_hasParams)
                {
                    _animator.SetInteger(DragonAnimatorParams.ActionHash, 0);
                    _animator.SetInteger(DragonAnimatorParams.LocomotionHash, 0);
                }
                _animator.Rebind();
            }
            Uid = null;
            Entry = null;
        }
    }
}
