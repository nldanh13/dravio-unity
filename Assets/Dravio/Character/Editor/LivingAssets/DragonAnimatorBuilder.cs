using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Dravio.Character.Editor
{
    /// <summary>
    /// One base AnimatorController per archetype (quadruped, winged, serpentine, aquatic) with placeholder clips,
    /// and one AnimatorOverrideController per dragon model that maps those placeholders to the model's own clips.
    /// Each species keeps its own skeleton and clips; nothing from Emberling is shared.
    ///
    /// Parameters (see DragonAnimatorParams): Locomotion (int), Action (int), Speed (float), PlaybackRate (float).
    /// Any State -> locomotion state when Locomotion == value and Action == 0; Any State -> Eat/Happy when Action
    /// == 1/2. The runtime resets Action to 0 after the clip length, which returns to the current locomotion.
    /// States exist only for clips that some species of the archetype has (from dragon-archetypes.json).
    /// Existing controllers are never overwritten.
    /// </summary>
    internal static class DragonAnimatorBuilder
    {
        internal const string Folder = "Assets/Dravio/Character/Animation";
        internal const string ArchetypesPath = "Assets/Dravio/Character/Data/dragon-archetypes.json";

        // index = DragonLocomotion / DragonInteraction value (U1-CHAR.6 appended Rest, Bask and Drink..Curious)
        private static readonly string[] Locomotion = { "Idle", "Walk", "Run", "Swim", "Fly", "Burrow", "Sleep", "Rest", "Bask",
                                                        "SwimIdle", "SwimRest", "SwimSurface", "Perch", "Glide" };
        private static readonly string[] Actions = { "", "Eat", "Happy", "Drink", "Wake", "Stretch", "LookAround", "Curious",
                                                     "Greet", "TakeOff", "Land", "WaterEnter", "WaterExit", "DigPrep", "BurrowEnter", "BurrowEmerge" };
        private static readonly HashSet<string> MovingStates = new HashSet<string> { "Walk", "Run", "Swim", "Fly", "Burrow", "SwimSurface", "Glide" };
        /// <summary>U1-CHAR.7 medium transitions: their ends are authored to match the neighbouring clips exactly
        /// (anim_qa transitions), so a short blend is enough and a long one would smear the match.</summary>
        private static readonly HashSet<string> ChainedActions = new HashSet<string> { "Wake", "TakeOff", "Land", "WaterEnter", "WaterExit", "DigPrep", "BurrowEnter", "BurrowEmerge" };

        /// <summary>Blend times by archetype: long bodies and swimmers blend slower so the chain does not snap.</summary>
        private static float BlendSeconds(string archetype)
        {
            switch (archetype)
            {
                case "serpentine": return 0.35f;
                case "aquatic": return 0.3f;
                case "winged": return 0.25f;
                default: return 0.2f;
            }
        }

        [System.Serializable] private sealed class ArchetypeFile { public ArchSpecies[] species = new ArchSpecies[0]; }
        [System.Serializable] private sealed class ArchSpecies { public string id; public string archetype; public string[] clips = new string[0]; }

        internal static HashSet<string> ClipsForArchetype(string archetype)
        {
            var set = new HashSet<string> { "Idle" };
            if (File.Exists(ArchetypesPath))
            {
                var f = JsonUtility.FromJson<ArchetypeFile>(File.ReadAllText(ArchetypesPath));
                foreach (var s in f.species)
                {
                    // walker species become quadruped once limbCount=4 is supplied; include them there
                    string arch = s.archetype == "walker" ? "quadruped" : s.archetype;
                    if (arch == archetype) foreach (var c in s.clips) set.Add(c);
                }
            }
            return set;
        }

        internal static AnimatorController GetOrCreateBase(string archetype, IEnumerable<string> mustInclude, out string message)
        {
            Directory.CreateDirectory(Folder);
            string path = Folder + "/DragonBase_" + archetype + ".controller";
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            var needed = ClipsForArchetype(archetype);
            foreach (var c in mustInclude) needed.Add(c);
            if (existing != null)
            {
                var have = new HashSet<string>(existing.layers[0].stateMachine.states.Select(s => s.state.name));
                var missing = needed.Where(n => !have.Contains(n)).ToArray();
                bool tempo = existing.parameters.Any(p => p.name == DragonAnimatorParams.Tempo);
                message = (missing.Length == 0 ? "Using " + path
                    : "Existing " + path + " lacks states " + string.Join(", ", missing) + " (not modified; delete it to regenerate).")
                    + (tempo ? "" : " No Tempo parameter (pre-U1-CHAR.6): personality tempo is ignored until it is regenerated.");
                return existing;
            }
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            ctrl.AddParameter(DragonAnimatorParams.Locomotion, AnimatorControllerParameterType.Int);
            ctrl.AddParameter(DragonAnimatorParams.Action, AnimatorControllerParameterType.Int);
            ctrl.AddParameter(DragonAnimatorParams.Speed, AnimatorControllerParameterType.Float);
            ctrl.AddParameter(new AnimatorControllerParameter { name = DragonAnimatorParams.PlaybackRate, type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            ctrl.AddParameter(new AnimatorControllerParameter { name = DragonAnimatorParams.Tempo, type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            var sm = ctrl.layers[0].stateMachine;
            float blend = BlendSeconds(archetype);
            int row = 0;
            for (int i = 0; i < Locomotion.Length; i++)
            {
                string n = Locomotion[i];
                if (!needed.Contains(n)) continue;
                var st = sm.AddState(n, new Vector3(300, 60 * row++, 0));
                st.motion = Placeholder(ctrl, n);
                st.writeDefaultValues = true;
                // moving clips follow ground speed; in-place clips follow the personality tempo (U1-CHAR.6)
                st.speedParameterActive = true;
                st.speedParameter = MovingStates.Contains(n) ? DragonAnimatorParams.PlaybackRate : DragonAnimatorParams.Tempo;
                if (n == "Idle") sm.defaultState = st;
                var t = sm.AddAnyStateTransition(st);
                t.canTransitionToSelf = false;
                t.hasExitTime = false;
                t.duration = blend;
                t.AddCondition(AnimatorConditionMode.Equals, i, DragonAnimatorParams.Locomotion);
                t.AddCondition(AnimatorConditionMode.Equals, 0, DragonAnimatorParams.Action);
            }
            for (int a = 1; a < Actions.Length; a++)
            {
                string n = Actions[a];
                if (!needed.Contains(n)) continue;
                var st = sm.AddState(n, new Vector3(600, 60 * (a - 1), 0));
                st.motion = Placeholder(ctrl, n);
                st.speedParameterActive = true;
                st.speedParameter = DragonAnimatorParams.Tempo;
                var t = sm.AddAnyStateTransition(st);
                t.canTransitionToSelf = false;
                t.hasExitTime = false;
                t.duration = ChainedActions.Contains(n) ? 0.05f : 0.15f;
                t.AddCondition(AnimatorConditionMode.Equals, a, DragonAnimatorParams.Action);
            }
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            message = "Created " + path + " (" + string.Join(", ", sm.states.Select(s => s.state.name).ToArray()) + ")";
            return ctrl;
        }

        private static AnimationClip Placeholder(AnimatorController ctrl, string state)
        {
            var clip = new AnimationClip { name = "placeholder_" + state };
            AssetDatabase.AddObjectToAsset(clip, ctrl);
            return clip;
        }

        /// <summary>Override controller for one model. Placeholders without a model clip stay unmapped (the
        /// runtime never requests a clip the species does not have).</summary>
        internal static AnimatorOverrideController CreateOverride(AnimatorController baseCtrl, string modelPath, string name, out string message)
        {
            string dir = Folder + "/Overrides";
            Directory.CreateDirectory(dir);
            string path = dir + "/" + name + ".overrideController";
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
            if (existing != null) { message = "Override exists, not overwritten: " + path; return existing; }
            var clips = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).ToDictionary(c => c.name, c => c);
            var ov = new AnimatorOverrideController(baseCtrl) { name = name };
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            ov.GetOverrides(pairs);
            var mapped = new List<string>();
            for (int i = 0; i < pairs.Count; i++)
            {
                string state = pairs[i].Key.name.Replace("placeholder_", "");
                AnimationClip c;
                if (clips.TryGetValue(state, out c)) { pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(pairs[i].Key, c); mapped.Add(state); }
            }
            ov.ApplyOverrides(pairs);
            AssetDatabase.CreateAsset(ov, path);
            message = "Created " + path + " mapping " + string.Join(", ", mapped.ToArray());
            return ov;
        }
    }
}
