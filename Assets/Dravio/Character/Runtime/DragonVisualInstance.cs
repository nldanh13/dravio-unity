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
        // U1-CHAR.6
        private bool _hasTempo;
        private float _tempo = 1f;
        private DragonHeadLook _headLook;
        private DragonBlink _blink;
        private bool _lookActive;
        // U1-CHAR.7
        private DragonMotionShaper _shaper;
        // U1-CHAR.8
        private Transform _headBone;
        private bool _hidden;
        public bool Hidden { get { return _hidden; } }

        /// <summary>Clips of the shown model (what the behaviour planner may use).</summary>
        public DragonAnimationMap Clips { get { return _map; } }
        public float Tempo { get { return _tempo; } }
        public bool LookActive { get { return _lookActive; } }

        internal void Bind(GameObject sourcePrefab, int phase)
        {
            SourcePrefab = sourcePrefab;
            _phase = phase;
            _animator = GetComponentInChildren<Animator>(true);
            _renderers = GetComponentsInChildren<Renderer>(true);
            _grounding = GetComponent<DragonFootGrounding>();
            _headLook = GetComponent<DragonHeadLook>();
            _shaper = GetComponent<DragonMotionShaper>();
            _blink = GetComponent<DragonBlink>();
            if (_animator != null)
            {
                _animator.keepAnimatorStateOnDisable = true; // throttling disables it; do not reset the state machine
                _animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                _hasParams = _animator.runtimeAnimatorController != null;
                _hasTempo = false;
                if (_hasParams)
                    foreach (var p in _animator.parameters)
                        if (p.nameHash == DragonAnimatorParams.TempoHash) { _hasTempo = true; break; }
            }
            _anchors.Clear();
            _headBone = null;
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name == "Head") { _headBone = t; break; }
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
                    DragonAnimationMap.PlaybackRate(ShownLocomotion, state.speed, DragonVisualCatalog.ReferenceSpeed(Entry, ShownLocomotion)));
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
            _actionTimer = (seconds > 0f ? seconds : 1f) / Mathf.Max(0.1f, _tempo);
            _animator.SetInteger(DragonAnimatorParams.ActionHash, (int)interaction);
            return true;
        }

        /// <summary>interval: 1 = every frame (Animator enabled), N = manual evaluation every N frames,
        /// 0 = not evaluated (time still accumulates, capped, so the pose resumes plausibly).</summary>
        internal bool Tick(float dt, int frame, int interval, bool visible, bool footIk)
        {
            return Tick(dt, frame, interval, visible, footIk, new DragonDetail { layers = interval == 1, blink = interval == 1 });
        }

        /// <param name="detail">U1-CHAR.8 mobile: which procedural layers run (UpdateRatePolicy.DetailFor).</param>
        internal bool Tick(float dt, int frame, int interval, bool visible, bool footIk, DragonDetail detail)
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
            // look-at and blink only on full-rate (near, visible) dragons; both are layered after the Animator
            if (_headLook != null) _headLook.enabled = _lookActive && interval == 1 && detail.layers && !_hidden;
            if (_shaper != null) _shaper.enabled = interval == 1 && detail.layers && !_hidden;
            if (_hidden)
            {
                if (_animator != null) _animator.enabled = false; // underground: nothing to evaluate
                if (_grounding != null) _grounding.enabled = false;
                return false;
            }
            if (_blink != null)
            {
                _blink.enabled = _blink.HasShape && interval == 1 && detail.blink;
                _blink.Asleep = ShownLocomotion == DragonLocomotion.Sleep;
            }
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

        /// <summary>Personality tempo for the non-locomotion states (needs the U1-CHAR.6 "Tempo" parameter).</summary>
        internal void SetTempo(float tempo)
        {
            _tempo = tempo > 0.1f ? tempo : 0.1f;
            if (_animator != null && _hasTempo) _animator.SetFloat(DragonAnimatorParams.TempoHash, _tempo);
        }

        /// <summary>Head look-at; ignored (false) when the prefab has no DragonHeadLook (no Neck/Head bones).</summary>
        internal bool SetLook(bool active, Vector3 worldTarget, float weight, float hold)
        {
            _lookActive = active && _headLook != null;
            if (_headLook != null) _headLook.SetTarget(active, worldTarget, weight, hold);
            return _headLook != null;
        }

        /// <summary>Underground (U1-CHAR.7): renderers off, Animator not evaluated; the instance stays live and pooled
        /// state is kept so BurrowEmerge plays at the same place.</summary>
        internal void SetHidden(bool hidden)
        {
            if (hidden == _hidden) return;
            _hidden = hidden;
            if (_renderers != null) foreach (var r in _renderers) if (r != null) r.enabled = !hidden;
            if (!hidden && _animator != null) _animator.enabled = true;
        }

        internal void SetMotionShape(DragonMotionMode mode, float turnRate, float verticalSpeed, float speed, float maxTurn)
        {
            if (_shaper != null) _shaper.SetInput(mode, turnRate, verticalSpeed, speed, maxTurn);
        }

        /// <summary>U1-CHAR.8 full expression layer (shaper: tail wag/carriage, ear fins, head tilt, neck).</summary>
        internal void SetExpressionState(DragonExpressionState s) { if (_shaper != null) _shaper.SetExpressionState(s); }

        /// <summary>World position of the Head bone (U1-CHAR.8 pair acts look at it).</summary>
        internal bool TryGetHeadPosition(out Vector3 p)
        {
            p = _headBone != null ? _headBone.position : Vector3.zero;
            return _headBone != null;
        }

        /// <summary>U1-CHAR.8: play this instance's current looping state at the leader's normalized time (same state on
        /// both Animators, both evaluated this frame). False when the states differ or an Animator is missing.</summary>
        internal bool SyncPhaseWith(DragonVisualInstance leader)
        {
            if (leader == null || leader._animator == null || _animator == null || !_hasParams || !_animator.enabled || !leader._animator.enabled) return false;
            if (leader.ShownLocomotion != ShownLocomotion || CurrentInteraction != DragonInteraction.None) return false;
            var a = leader._animator.GetCurrentAnimatorStateInfo(0);
            var b = _animator.GetCurrentAnimatorStateInfo(0);
            if (a.shortNameHash != b.shortNameHash) return false;
            _animator.Play(b.fullPathHash, 0, a.normalizedTime - Mathf.Floor(a.normalizedTime));
            return true;
        }

        internal void SetExpression(float tailCarriage, float earPerk)
        {
            if (_shaper == null) return;
            _shaper.TailCarriage = tailCarriage;
            _shaper.EarPerk = earPerk;
        }

        internal void ResetForPool()
        {
            SetHidden(false);
            if (_shaper != null) _shaper.ResetPose();
            SetTempo(1f);
            SetLook(false, Vector3.zero, 0f, 1f);
            if (_headLook != null) _headLook.ResetPose();
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
