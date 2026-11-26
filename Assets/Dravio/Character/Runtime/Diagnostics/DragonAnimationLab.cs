using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Dravio.Character.Diagnostics
{
    /// <summary>
    /// Animation review tool for imported dragons (scene DragonAnimationLab.unity, menu Dravio/Character/Animation Lab).
    /// Play mode only; it never edits assets. Pick a species/stage and a clip, then:
    ///  * playback speed, pause, single-frame steps (clip frame rate), scrubbing
    ///  * LOD switch (auto / LOD0 / LOD1 / LOD2) to compare deformation between LODs
    ///  * foot IK on/off and an uneven-ground block to test planted feet
    ///  * skeleton overlay (bones as lines, IK feet marked)
    ///  * the animation QA warnings recorded for that clip by anim_qa.py (catalog animQaWarnings)
    ///  * four fixed views (front, side, top, three-quarter) or one view
    ///  * U1-CHAR.6 behaviour preview: the real DragonBehaviorPlanner drives the clips from an intent, a
    ///    personality style and player reactions (Petted / Called / Fed), so transitions (Sleep -> Wake),
    ///    idle variations and reactions can be reviewed per species
    /// The clip is played through a manual PlayableGraph (not the Animator controller), so what is shown is exactly
    /// the imported clip at the chosen time. Root motion is off, as in game: the clips are authored in place.
    /// </summary>
    public sealed class DragonAnimationLab : MonoBehaviour
    {
        public DragonVisualCatalog catalog;
        public Camera mainCamera;
        [Tooltip("Optional extra cameras for the four-view layout (front, side, top); created if empty")]
        public Camera[] extraViews = new Camera[0];
        [Tooltip("Block of steps under the dragon to test foot IK")] public GameObject unevenGround;
        public Transform stage;

        private int _entry = -1, _clip;
        private float _time, _speed = 1f;
        private bool _paused, _ik = true, _skeleton = true, _multiView = true, _uneven;
        private int _lod = -1;
        private GameObject _instance;
        private Animator _animator;
        private DragonFootGrounding _grounding;
        private LODGroup _lodGroup;
        private AnimationClip[] _clips = new AnimationClip[0];
        private PlayableGraph _graph;
        private AnimationClipPlayable _playable;
        private Transform[] _bones = new Transform[0];
        private Material _lineMat;
        private Vector2 _scroll;
        // U1-CHAR.6 behaviour preview
        private bool _behave;
        private DragonBehaviorPlanner _planner;
        private DragonBehaviorIntent _intent = DragonBehaviorIntent.Idle;
        private DragonPersonalityStyle _style = DragonPersonalityStyle.Unspecified;
        private float _tempo = 1f;
        private string _behaviourLog = "";

        public GameObject Instance { get { return _instance; } }
        public AnimationClip CurrentClip { get { return _clip >= 0 && _clip < _clips.Length ? _clips[_clip] : null; } }
        public float CurrentTime { get { return _time; } }

        private void Start()
        {
            if (catalog != null && catalog.entries.Count > 0) Select(0);
            if (unevenGround != null) unevenGround.SetActive(_uneven);
        }

        private void OnDestroy()
        {
            if (_graph.IsValid()) _graph.Destroy();
            if (_lineMat != null) Destroy(_lineMat);
        }

        public void Select(int entryIndex)
        {
            if (catalog == null || entryIndex < 0 || entryIndex >= catalog.entries.Count) return;
            if (_graph.IsValid()) _graph.Destroy();
            if (_instance != null) Destroy(_instance);
            _entry = entryIndex;
            _planner = null;
            var e = catalog.entries[entryIndex];
            if (e.prefab == null) { _instance = null; _clips = new AnimationClip[0]; return; }
            _instance = Instantiate(e.prefab, stage != null ? stage.position : Vector3.zero, Quaternion.identity);
            _instance.name = e.speciesId + "_" + e.stage + " (lab)";
            _animator = _instance.GetComponentInChildren<Animator>(true);
            _lodGroup = _instance.GetComponentInChildren<LODGroup>(true);
            _grounding = _instance.GetComponentInChildren<DragonFootGrounding>(true);
            if (_grounding != null) _grounding.Configure(e.ikChains);
            _clips = OrderedClips(_animator, e.clips);
            _bones = SkeletonBones(_instance);
            _clip = 0;
            _time = 0f;
            if (_animator != null && _clips.Length > 0)
            {
                _animator.applyRootMotion = false;
                _animator.runtimeAnimatorController = null; // the graph below is the only writer
                _graph = PlayableGraph.Create("DragonAnimationLab");
                _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var output = AnimationPlayableOutput.Create(_graph, "Lab", _animator);
                _playable = AnimationClipPlayable.Create(_graph, _clips[0]);
                output.SetSourcePlayable(_playable);
            }
            ApplyLod();
        }

        /// <summary>Clips in rig.json order (catalog clips), then any other clip of the controller.</summary>
        public static AnimationClip[] OrderedClips(Animator animator, string[] order)
        {
            var list = new List<AnimationClip>();
            if (animator == null || animator.runtimeAnimatorController == null) return list.ToArray();
            var all = animator.runtimeAnimatorController.animationClips;
            if (order != null)
                foreach (var n in order)
                    foreach (var c in all)
                        if (c != null && c.name == n && !list.Contains(c)) { list.Add(c); break; }
            foreach (var c in all) if (c != null && !list.Contains(c)) list.Add(c);
            return list.ToArray();
        }

        private static Transform[] SkeletonBones(GameObject go)
        {
            var smr = go.GetComponentInChildren<SkinnedMeshRenderer>(true);
            return smr != null && smr.bones != null ? smr.bones : new Transform[0];
        }

        public void SetClip(int index)
        {
            if (index < 0 || index >= _clips.Length || !_graph.IsValid()) return;
            _clip = index;
            _time = 0f;
            var output = _graph.GetOutput(0);
            _playable.Destroy();
            _playable = AnimationClipPlayable.Create(_graph, _clips[index]);
            output.SetSourcePlayable(_playable);
        }

        /// <summary>Wraps (loop) or clamps (one-shot) playback time to the clip length.</summary>
        public static float AdvanceTime(float time, float delta, float length, bool loop)
        {
            if (length <= 0f) return 0f;
            float t = time + delta;
            if (loop) { t %= length; if (t < 0f) t += length; return t; }
            return Mathf.Clamp(t, 0f, length);
        }

        public void StepFrames(int frames)
        {
            var c = CurrentClip;
            if (c == null) return;
            _paused = true;
            _time = AdvanceTime(_time, frames / Mathf.Max(1f, c.frameRate), c.length, c.isLooping);
        }

        private void Update()
        {
            var c = CurrentClip;
            if (c != null && _graph.IsValid())
            {
                if (_behave) BehaviourTick();
                c = CurrentClip;
                if (!_paused) _time = AdvanceTime(_time, Time.deltaTime * _speed * (_behave ? _tempo : 1f), c.length, c.isLooping);
                _playable.SetTime(_time);
                _graph.Evaluate(0f);
            }
            if (_grounding != null) _grounding.enabled = _ik && DragonFootGrounding.AppliesTo(ShownLocomotion(c), Interaction(c));
            FrameCameras();
        }

        private void BehaviourTick()
        {
            if (catalog == null || _entry < 0 || _entry >= catalog.entries.Count) return;
            var e = catalog.entries[_entry];
            if (_planner == null)
            {
                _planner = new DragonBehaviorPlanner(new DragonAnimationMap(e.rigged ? e.clips : null),
                    i => DragonVisualCatalog.ClipSecondsOf(e, i), DragonPersonalityPresentation.For(_style), DragonBehaviorPlanner.SeedFor(e.speciesId));
            }
            var o = _planner.Update(Time.deltaTime, _intent, 0f, 0f);
            _tempo = o.tempo;
            if (o.trigger != DragonInteraction.None) _behaviourLog = "start " + o.trigger + (o.note != null ? " | " + o.note : "");
            else if (o.note != null) _behaviourLog = o.note;
            string want = _planner.ActiveAction != DragonInteraction.None ? _planner.ActiveAction.ToString() : o.locomotion.ToString();
            var c = CurrentClip;
            if (c == null || c.name != want || o.trigger != DragonInteraction.None)
                for (int i = 0; i < _clips.Length; i++)
                    if (_clips[i].name == want) { SetClip(i); _time = 0f; _paused = false; break; }
        }

        /// <summary>Clip name -> the game state it represents. One-shots (Eat, Happy, Drink, Wake, Stretch,
        /// LookAround, Curious) play over Idle; flight transitions (TakeOff, Glide, Land) count as Fly, so ground
        /// IK stays off for them.</summary>
        public static DragonLocomotion ShownLocomotion(AnimationClip c)
        {
            return ShownLocomotion(c == null ? null : c.name);
        }

        public static DragonLocomotion ShownLocomotion(string clip)
        {
            DragonInteraction a;
            if (string.IsNullOrEmpty(clip) || (System.Enum.TryParse(clip, out a) && a != DragonInteraction.None)) return DragonLocomotion.Idle;
            DragonLocomotion l;
            return System.Enum.TryParse(clip, out l) ? l : DragonLocomotion.Fly;
        }

        public static DragonInteraction Interaction(AnimationClip c)
        {
            if (c == null) return DragonInteraction.None;
            DragonInteraction a;
            return System.Enum.TryParse(c.name, out a) ? a : DragonInteraction.None;
        }

        private void ApplyLod()
        {
            if (_lodGroup != null) _lodGroup.ForceLOD(_lod);
        }

        private Bounds InstanceBounds()
        {
            var b = new Bounds(stage != null ? stage.position : Vector3.zero, Vector3.one);
            if (_instance == null) return b;
            bool any = false;
            foreach (var r in _instance.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        private void FrameCameras()
        {
            if (mainCamera == null) return;
            var b = InstanceBounds();
            float size = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            Vector3 c = b.center;
            // Dragon forward is +Z (Blender -Y). Views: three-quarter front-left (main), front, side, top.
            Place(mainCamera, c, new Vector3(-0.8f, 0.55f, 1.1f), size, _multiView ? new Rect(0f, 0f, 0.5f, 0.5f) : new Rect(0f, 0f, 1f, 1f));
            EnsureViews();
            Vector3[] dirs = { new Vector3(0f, 0.15f, 1f), new Vector3(1f, 0.15f, 0f), new Vector3(0f, 1f, 0.01f) };
            Rect[] rects = { new Rect(0f, 0.5f, 0.5f, 0.5f), new Rect(0.5f, 0.5f, 0.5f, 0.5f), new Rect(0.5f, 0f, 0.5f, 0.5f) };
            for (int i = 0; i < extraViews.Length && i < 3; i++)
            {
                if (extraViews[i] == null) continue;
                extraViews[i].enabled = _multiView;
                if (_multiView) Place(extraViews[i], c, dirs[i], size, rects[i]);
            }
        }

        private static void Place(Camera cam, Vector3 center, Vector3 dir, float size, Rect rect)
        {
            cam.rect = rect;
            cam.transform.position = center + dir.normalized * size * 2.2f;
            cam.transform.LookAt(center);
        }

        private void EnsureViews()
        {
            if (extraViews != null && extraViews.Length >= 3) return;
            extraViews = new Camera[3];
            string[] names = { "View Front", "View Side", "View Top" };
            for (int i = 0; i < 3; i++)
            {
                var go = new GameObject(names[i]);
                go.transform.SetParent(transform, false);
                extraViews[i] = go.AddComponent<Camera>();
                extraViews[i].clearFlags = mainCamera.clearFlags;
                extraViews[i].backgroundColor = mainCamera.backgroundColor;
                extraViews[i].nearClipPlane = 0.01f;
            }
        }

        private void OnRenderObject()
        {
            if (!_skeleton || _bones.Length == 0) return;
            if (_lineMat == null)
            {
                var shader = Shader.Find("Hidden/Internal-Colored");
                if (shader == null) return;
                _lineMat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                _lineMat.SetInt("_ZTest", 8); // always: bones visible through the mesh
            }
            _lineMat.SetPass(0);
            GL.PushMatrix();
            GL.Begin(GL.LINES);
            var set = new HashSet<Transform>(_bones);
            foreach (var t in _bones)
            {
                if (t == null || t.parent == null || !set.Contains(t.parent)) continue;
                bool leg = t.name.Contains("Fore") || t.name.Contains("Hind");
                GL.Color(leg ? new Color(1f, 0.55f, 0.1f) : new Color(0.2f, 0.9f, 1f));
                GL.Vertex(t.parent.position);
                GL.Vertex(t.position);
            }
            GL.End();
            GL.PopMatrix();
        }

        private void OnGUI()
        {
            if (catalog == null) { GUILayout.Label("Assign a DragonVisualCatalog (Dravio/Character/Import Batch Manifest first)."); return; }
            GUILayout.BeginArea(new Rect(10, 10, 330, Screen.height - 20), GUI.skin.box);
            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.Label("<b>Dragon Animation Lab</b>", Rich());
            for (int i = 0; i < catalog.entries.Count; i++)
            {
                var e = catalog.entries[i];
                string label = e.speciesId + " / " + e.stage + (e.rigged ? "" : " (static)") + (e.approved ? " [approved]" : "");
                if (GUILayout.Toggle(i == _entry, label, "Button") && i != _entry) Select(i);
            }
            GUILayout.Space(6);
            if (_entry >= 0 && _entry < catalog.entries.Count)
            {
                var e = catalog.entries[_entry];
                GUILayout.Label("Clip");
                for (int i = 0; i < _clips.Length; i++)
                    if (GUILayout.Toggle(i == _clip, _clips[i].name + (_clips[i].isLooping ? " (loop)" : ""), "Button") && i != _clip) SetClip(i);
                var c = CurrentClip;
                if (c != null)
                {
                    int frames = Mathf.RoundToInt(c.length * c.frameRate);
                    GUILayout.Label("Frame " + Mathf.RoundToInt(_time * c.frameRate) + " / " + frames + "   time " + _time.ToString("0.00") + " s");
                    _time = GUILayout.HorizontalSlider(_time, 0f, c.length);
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("|<")) { _time = 0f; _paused = true; }
                    if (GUILayout.Button("< 1")) StepFrames(-1);
                    if (GUILayout.Button(_paused ? "Play" : "Pause")) _paused = !_paused;
                    if (GUILayout.Button("1 >")) StepFrames(1);
                    GUILayout.EndHorizontal();
                    GUILayout.Label("Speed x" + _speed.ToString("0.00"));
                    _speed = GUILayout.HorizontalSlider(_speed, 0.05f, 2f);
                    float gs = DragonVisualCatalog.ReferenceSpeed(e, ShownLocomotion(c));
                    if (gs > 0f) GUILayout.Label("Authored ground speed " + gs.ToString("0.000") + " m/s (in place, root motion off)");
                }
                GUILayout.Space(6);
                GUILayout.Label("LOD");
                GUILayout.BeginHorizontal();
                string[] lods = { "Auto", "0", "1", "2" };
                for (int i = 0; i < lods.Length; i++)
                    if (GUILayout.Toggle(_lod == i - 1, lods[i], "Button") && _lod != i - 1) { _lod = i - 1; ApplyLod(); }
                GUILayout.EndHorizontal();
                bool ik = GUILayout.Toggle(_ik, " Foot IK" + (_grounding == null ? " (no IK chains)" : ""));
                _ik = ik;
                bool un = GUILayout.Toggle(_uneven, " Uneven ground");
                if (un != _uneven) { _uneven = un; if (unevenGround != null) unevenGround.SetActive(_uneven); }
                _skeleton = GUILayout.Toggle(_skeleton, " Skeleton");
                _multiView = GUILayout.Toggle(_multiView, " Four views (3/4, front, side, top)");
                if (_grounding != null && _grounding.enabled)
                    GUILayout.Label("Pelvis offset " + (_grounding.PelvisOffset * 100f).ToString("0.0") + " cm");
                GUILayout.Space(6);
                bool behave = GUILayout.Toggle(_behave, " Behaviour preview (planner)");
                if (behave != _behave) { _behave = behave; _planner = null; _tempo = 1f; }
                if (_behave)
                {
                    GUILayout.Label("Personality");
                    GUILayout.BeginHorizontal();
                    foreach (DragonPersonalityStyle s in System.Enum.GetValues(typeof(DragonPersonalityStyle)))
                        if (GUILayout.Toggle(_style == s, s.ToString(), "Button") && _style != s)
                        {
                            _style = s;
                            if (_planner != null) _planner.SetPresentation(DragonPersonalityPresentation.For(s));
                        }
                    GUILayout.EndHorizontal();
                    GUILayout.Label("Intent: " + _intent);
                    var intents = (DragonBehaviorIntent[])System.Enum.GetValues(typeof(DragonBehaviorIntent));
                    for (int i = 1; i < intents.Length; i += 3)
                    {
                        GUILayout.BeginHorizontal();
                        for (int k = i; k < Mathf.Min(i + 3, intents.Length); k++)
                            if (GUILayout.Toggle(_intent == intents[k], intents[k].ToString(), "Button")) _intent = intents[k];
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Pet")) _planner?.React(DragonReaction.Petted);
                    if (GUILayout.Button("Call")) _planner?.React(DragonReaction.Called);
                    if (GUILayout.Button("Feed")) _planner?.React(DragonReaction.Fed);
                    GUILayout.EndHorizontal();
                    if (_planner != null)
                        GUILayout.Label("Shown " + _planner.Shown + (_planner.ActiveAction != DragonInteraction.None ? " + " + _planner.ActiveAction : "")
                            + "  tempo x" + _tempo.ToString("0.00") + "  idle " + _planner.IdleSeconds.ToString("0") + "/" + _planner.NextVariationAt.ToString("0") + " s");
                    if (!string.IsNullOrEmpty(_behaviourLog)) GUILayout.Label(_behaviourLog);
                }
                GUILayout.Space(6);
                GUILayout.Label("Animation QA: " + (string.IsNullOrEmpty(e.animQaStatus) ? "no report" : e.animQaStatus));
                if (c != null)
                    foreach (var w in DragonVisualCatalog.WarningsFor(e, c.name))
                        GUILayout.Label("<color=#ff7a5c>" + w + "</color>", Rich());
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private static GUIStyle _rich;
        private static GUIStyle Rich()
        {
            if (_rich == null) _rich = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true };
            return _rich;
        }
    }
}
