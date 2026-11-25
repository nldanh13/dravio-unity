# Dravio — Unity Edition

Cozy Ancient Fantasy dragon sanctuary simulation. Unity 6000.6.10f1, Universal Render Pipeline. The original TypeScript project remains the migration reference: https://github.com/nldanh13/dravio.

## Run the U0 prototype

1. Open this project in Unity Hub with 6000.6.10f1.
2. Allow package import and C# compilation to finish. Check Console for red errors.
3. In Unity Editor choose **Dravio > Create or Open Origin Prototype**.
4. Open the Game tab and press Play.
5. Drag directly on Emberling with mouse/touch; left/right arrows rotate, R resets. The camera does not orbit.

The menu saves a new scene at `Assets/Dravio/Scenes/OriginMeadow.unity` and creates persistent URP materials. If the scene exists it opens it instead of overwriting it. Generated Unity assets and their .meta files should be committed after local verification. This is a geometry blockout, not final painterly art; the scene is not added to build settings automatically.

## Migration

- U0: foundation + one-click scene + visual 360° rotation (this branch).
- U1: approved 3D model, texture, environment, rigging, fixed camera and Fantasy UI.
- U2: C# domain parity (integer microG, deterministic RNG, commands, save, genetics, biology).
- U3: habitat graph, movement and behavior.
- U4: tutorial, eggs, achievements, breeding.
- U5: care, disasters, rescue.
- U6: passport, localization, vertical slice acceptance.

Source biological data is copied from Dravio TypeScript commit b33ec7a3dc98b4a74857fe92d143475028fc0d96, `src/data/g0Biology.ts`, SPECIES_BIOLOGY. This JSON is a reference snapshot; no C# loader, economy, or gameplay has been migrated yet. Existing IDs and rules must not be silently changed.

## Source control

Commit Assets, Packages, ProjectSettings and Unity .meta files. Do not commit Library, Temp, Logs, UserSettings, build outputs or secrets. Check that ProjectSettings/ProjectVersion.txt exists after Unity finishes opening, and commit the actual generated file if missing. The project repository is public: avoid adding paid third-party assets without redistribution rights.

No Unity Editor/build test has been run in the authoring environment. Local compilation and visual acceptance are required.

## U1 visual prototype — Ancient Sanctuary

After U0 has been saved, select **Dravio > Create or Open Ancient Sanctuary**.
The editor opens/saves a separate `Assets/Dravio/Scenes/AncientSanctuary.unity` before applying changes. It leaves OriginMeadow.unity intact. Subsequent menu use opens the existing U1 scene, preserving local edits. If scene creation fails halfway, inspect Console, keep U0 unchanged and regenerate only the incomplete U1 scene after backing it up.

U1 changes:
- Fixed orthographic view closer to Emberling, larger textured meadow, forest layers and distance fog.
- Ruin stone variation, vines, flowers, moss, stepping stones and warm light.
- Larger wingless Baby Normal with layered eyes, claws and continuous tapered tail mesh.
- Read-only visual title and rotation instructions, not an economy or care interface.
- Procedurally generated geometry, materials, and texture assets under `Assets/Dravio/Art/U1`.

Test in Game view at 16:9 first, then portrait. Check for clipping, incorrect normals, eye silhouette, lighting, touch, and performance. Confirm mouse drag rotates only the dragon and arrow keys/R still work. Check Console for errors. Save the scene outside Play Mode and commit generated scene, assets, and .meta files. This branch has not been compiled or rendered in Unity by the code author.

This is a second visual blockout, not a completed hand-painted environment or rigged production dragon. No new game mechanics, fake resource counts, or changes to the canonical biology. Art direction remains the author's reference; production assets, rigging/animation, real Fantasy UI and performance optimization remain outstanding.

## U1.2 — Blender to Unity model pipeline

Requires Blender with the FBX exporter (test with Blender 4.x LTS). Install from https://www.blender.org/download/ if not present. Close Play Mode. Run from the repository root with Blender in PATH:

```powershell
blender --factory-startup --background --python-exit-code 1 --python scripts/art/build_emberling.py -- art-exports/EmberlingBaby.fbx
```

If Blender is not in PATH, use its installed executable's full path with PowerShell's & operator. The script starts from an empty Blender scene; never run it inside an unsaved artist project. It creates `art-exports/EmberlingBaby.blend` (editable source) and `EmberlingBaby.fbx` (static export). The art remains original procedural prototype; fused body, four legs, rounded flame tail, no wings, no horns for Baby. No rig, UV-painted textures, animation, or phenotype mapping yet.

Copy only the FBX into `Assets/Dravio/Art/Models/`. In Unity, select that FBX in the Project panel and choose **Dravio > Preview Selected Emberling FBX**. The tool creates a normalized visual Prefab (2.05 local units height; U1 root scale retained), a box hit target, URP materials, and a separate `EmberlingModelPreview.unity` scene without overwriting U0/U1. Imported Animator root motion is disabled. The existing turntable remains on the root. If preview already exists, open it rather than overwrite; rename/back up before generating another.

Verify model proportions, front/back, feet on ground, colors, shadow, draw calls, drag/arrows/reset, Console. The material conversion handles basic color/texture/smoothness only; normal maps, custom shaders and skinning must be artist-validated. Do not import unlicensed assets into this public repo. Commit accepted FBX, Prefab, preview Scene and meta files; keep local export/cache ignored. Neither Blender export nor Unity compilation of this change has been executed in the authoring environment.

## U1.3a — Living Emberling (rig and Idle)

U1.2 static export and model preview were run on the author's Blender 5.3 and Unity 6000.6.10f1 machine and committed as 9c7830a. This next prototype adds a separate Blender source generator, generic 13-bone quadruped rig (root, pelvis, chest, head, two tail bones, flame, four leg bones, two eye bones), explicit skinning, and a six-second in-place Idle action at 30 fps: subtle breathing, head movement, tail sway, flame pulse and a stylized blink using vertical eye scaling. No walk/eat/sleep animation or gameplay yet. Skin weights are provisional, not a production rig; check transitions, paws, eyes and tail throughout the loop.

From the repository root on the author's machine:

```powershell
& "C:\Program Files\Blender Foundation\Blender 5.3\blender.exe" --factory-startup --background --python-exit-code 1 --python scripts/art/build_emberling_living.py -- art-exports/EmberlingLiving.fbx
Copy-Item "art-exports\EmberlingLiving.fbx" "Assets\Dravio\Art\Models\"
```

Select EmberlingLiving.fbx (not the original static FBX) and choose **Dravio > Preview Selected Living Emberling FBX**. The editor configures Generic animation import, looping clip, an Animator Controller, reusable visual prefab and matte URP materials. Creates `EmberlingLivingPreview.unity` from AncientSanctuary, not from or over the accepted static FBX preview. Existing preview scenes are preserved. Press Play; verify at least two full idle loops, camera stability, root position, drag/arrows/R, no red errors, and visually acceptable skinning. Inspect Animator state and FBX Animation/Rig tabs if not moving. The editor import and export of U1.3a are not locally verified by the code author; preserve the old model while evaluating. Save and commit the new FBX, scene, prefab, materials, controller and meta files only after inspection. The .blend is an editable local source output; the generator is versioned.

The procedural rig and matte material are interim art, not the final textured and professionally rigged Emberling. No biology or economy changes.

## U1.3b — Wander preview

Based on the locally verified U1.3a idle scene commit 311fd63. The accepted Idle FBX and scenes are preserved. This visual prototype uses a separate EmberlingWander FBX with one baked take containing Idle (frames 1–181) and Walk (182–217), split by the Unity editor. A provisional four-beat in-place gait uses the existing single-bone legs; it is not a production IK gait. The Animator switches between Idle and Walk, and a visual-only controller follows four bounded waypoints around the original spawn, pausing approximately two seconds at each. Space toggles inspection: movement stops, drag/arrows/R rotate the dragon, Space resumes. World camera stays fixed. No collision avoidance, full habitat navigation, sleep/eat/pet, game economy or persistence changes. Facing offset is an Inspector setting (default 180 degrees; adjust to 0 if the imported model walks backwards). Verify leg deformation, foot sliding, direction, pause/resume, transitions and Console in Unity before accepting.

From the new repository root in PowerShell:
```powershell
& "C:\Program Files\Blender Foundation\Blender 5.3\blender.exe" --factory-startup --background --python-exit-code 1 --python scripts/art/build_emberling_living.py -- art-exports/EmberlingWander.fbx --walk-preview
Copy-Item "art-exports\EmberlingWander.fbx" "Assets\Dravio\Art\Models\"
```

Select EmberlingWander.fbx and use **Dravio > Preview Selected Wandering Emberling FBX**. It creates a separate EmberlingWanderPreview.unity scene, prefab and two-state controller. Open existing preview instead of overwriting. Save and commit new assets only after local acceptance. Neither the new Blender export nor the Unity compilation/playback was executed in the authoring environment.

## U1.3c — Care & interaction preview

Based on U1.3b source (832748a), which has not yet been locally tested. Latest locally accepted baseline is U1.3a (311fd63). This branch is independently reviewable, keeps all prior scene/FBX assets, and does not touch the Environment or Domain workstreams. No real food inventory, bond, health, gold, offline simulation or persistence changes.

Generate the cumulative model directly; there is no need to export Wander separately first:

```powershell
cd "B:\Dravio\Dravio-unity"
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/art/Export-EmberlingPreview.ps1
```

Or use the explicit Blender command with `art-exports/EmberlingCare.fbx --care-preview`, then copy the FBX to Assets/Dravio/Art/Models. The guarded PowerShell script never overwrites an existing Assets FBX. Select EmberlingCare.fbx, choose **Dravio > Preview Selected Care Emberling FBX**. This makes a separate EmberlingCarePreview scene, controller, prefab and interactive HUD. Existing preview scenes are not overwritten.

Baked clip contract at 30 fps (one FBX take; importer offsets relative to its first frame):
- Idle 1–181 (6s, loop)
- Walk 182–217 (1.167s, loop)
- Eat 218–278 (2s, one shot; head dip/bob, no jaw articulation or actual food consumption)
- Doze 279–399 (4s, loop; eyes closed and head lowered while standing, not a full lying-down sleep pose)
- Happy 400–445 (1.5s, one shot; head and tail response)

Auto wandering/rest is the default. F or Feed plays Eat, P or Pet plays Happy, N or Doze/wake toggles dozing, Space or Inspect toggles manual rotation; Resume exploring restores autonomy. Actions are interruptible and one-shot actions return to Idle automatically. Inspection uses existing drag/arrows/R, camera does not orbit. Buttons use UGUI and the Input System UI module. Visual-only states must not be treated as real gameplay commands.

Deferred acceptance checklist (run when Unity is available):
1. Export completes, FBX imported with exactly five intended named clips; Generic avatar valid.
2. Scene and Prefab/Animator assets generated without overwriting U1.3a. No red Console errors.
3. Watch two Idle and Walk loops, check orientation, feet sliding, leg deformation and bounded motion.
4. Exercise each button and keyboard shortcut, rapid interrupts and resume; test touch separately.
5. Verify one-shot completion, doze remains until interrupted, inspect drag does not compete with autonomous movement, camera remains fixed.
6. Check HUD at 16:9 and portrait, colors, frame rate, no action state alters actual game data. Save/commit generated assets only after inspection.

No Blender, Unity, input or visual test was executed on U1.3b/U1.3c by the code author. Skinning/gait and dozing are provisional; production IK, full lying-down posture, jaw animation, texture painting and mobile optimization remain future work. Keep PRs as drafts until accepted.

## U1.4 — Character visual polish preview

Cumulative branch from U1.3c (b481fc4). U1.3b/c and U1.4 are not yet tested in the user's Unity editor; the locally verified baseline is U1.3a (311fd63). No edits to Domain, Environment, shared URP pipeline assets or ProjectSettings.

The optional Polished export includes all five prior clips plus an approximate cylindrical body UV, slightly recessed eyes and a tapered rounded flame shape. Unity generates a 512×512 mipmapped procedural scale/paint texture with a continuous cream belly gradient, matte skin and emissive flame materials. The projection may stretch at paws, tail and head: this is not a professional unwrap, normal map or hand-painted texture. The .blend has the UVs, while the paint texture is generated in Unity.

The new scene adds a small world-space tail-following particle system (maximum 24 particles), a point light with subtle flicker, local Bloom/color-adjustment profile and SMAA on the fixed camera. Bloom and extra light rendering depend on the selected URP pipeline capabilities. No SSAO/renderer feature change, no global graphics setting changes, no measured frame-rate or mobile acceptance. The polish-specific material overrides and VFX are on the generated scene instance, not yet a standalone finished character prefab.

When Unity is available, use the latest branch directly:

```powershell
cd "B:\Dravio\Dravio-unity"
git fetch origin
git switch -c feat/u1-visual-polish --track origin/feat/u1-visual-polish
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/art/Export-EmberlingPreview.ps1 -Mode Polished
```

Select EmberlingPolished.fbx in Unity and choose **Dravio > Preview Selected Polished Emberling FBX**. A separate EmberlingPolishedPreview scene is created, with the care preview buttons and all five clips. Existing accepted scenes and FBX are preserved. The helper also supports -Mode Wander and -Mode Care; it never overwrites existing FBX files under Assets.

Deferred checks: verify exporter output, first UV channel, five imported animation clips, body material, eye silhouette, seams/stretching, flame emission and spark shader compilation, sparks tracking while walking/rotating, fixed camera, HUD and all care interactions; compare with/without local post-processing. Test PC and mobile quality separately, and profile before claiming performance. If generation fails midway, inspect the Console and rename/back up the incomplete preview scene before retrying. Commit accepted generated assets and their meta files only after review.

Reference API documentation: Unity URP camera data and post-processing (https://docs.unity.com/en-us/engine/6000.0/manual/cameras/urp/universal-additional-camera-data); Blender 5.3 MeshUVLoopLayer (https://docs.blender.org/api/5.3/bpy.types.MeshUVLoopLayer.html). No Blender or Unity runtime verification in the authoring environment.

## U1.5 — Fantasy UI preview

Cumulative from U1.4 commit a7f34a0. The latest locally verified milestone is U1.3a (311fd63); U1.3b through U1.5 require local Unity acceptance. No edits to the separate Environment or Core Domain workstreams.

When Unity is available:
```powershell
cd "B:\Dravio\Dravio-unity"
git fetch origin
git switch -c feat/u1-fantasy-ui --track origin/feat/u1-fantasy-ui
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/art/Export-EmberlingPreview.ps1 -Mode Polished
```
In Unity choose **Dravio > Create or Open Fantasy Preview**. The menu finds EmberlingPolished.fbx automatically, creates EmberlingFantasyPreview.unity separately from previous scenes, or opens the existing scene without rebuilding it. If the FBX already exists under Assets, the exporter preserves it; review any regenerated local file before replacing it. No new FBX mode is needed.

- Dark wood, gold, parchment palette; header and collapsible drawer; scrollable Care, Biology and Dragons tabs.
- Vietnamese default and live English switch, including the current visual activity. Language selection is session-only. Built-in font used provisionally; Vietnamese diacritics and mobile font coverage must be visually verified.
- Landscape-only mobile orientation (left and right); right-hand rail on desktop and mobile, safe-area anchoring, height-based compact scaling, two-column touch actions, hide/show controls and UI raycast guard for turntable. Portrait mobile rotation is disabled in PlayerSettings and at runtime.
- Care buttons and existing F/P/N/Space shortcuts retain the same preview actions. R/drag/arrows work during inspection. Roster has exactly the one present preview dragon and an inspect action.
- Biology fields are extracted at scene generation from the canonical `Assets/Dravio/Data/species-biology.json` Emberling entry, not invented values. Display reference fields only; no gold, health, food quantity, growth progress, comfort, genetics, passport, or inventory fabricated. Rebuild a new preview after backing up an old one if the canonical file changes.

Deferred acceptance: verify compilation, FBX and five clips, menu creation/reopen, care actions, language/diacritics, scroll and tab navigation, roster inspect, hide/show, both landscape orientations (16:9, 19.5:9, 20:9), safe areas, touch scroll and drag not rotating the dragon through UI, camera framing and performance. This is a functional UI prototype, not production-quality art or a domain-connected UI. No Unity/Blender execution by the code author. Keep prior previews and draft PRs until validated.

## U1.6 — Refined character and asset audit

Cumulative from the landscape-only U1.5 commit 75c8482. The latest locally accepted baseline remains U1.3a; U1.3b through U1.6 have not been run in the user's Unity Editor. Environment and Core Domain workstreams are unchanged. No portrait mobile layout.

The opt-in Refined export extends the 13-bone rig to 17 bones (four lower leg joints), assigns paw/claw details to the lower joints, uses two-joint FK walk poses and a lowered, folded-leg doze pose. The five clip names and ranges are unchanged. The doze pose is an approximate crouched rest, not a validated full lying-down sleep animation. The projected sagittal angles were numerically checked in an idealized two-segment model; this does not establish ground contact or correct mesh deformation in Blender/Unity. No IK, jaw articulation, new audio, or gameplay effects yet.

Generate and check the cumulative model:
```powershell
cd "B:\Dravio\Dravio-unity"
git fetch origin
git switch -c feat/u1-character-qa --track origin/feat/u1-character-qa
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/art/Export-EmberlingPreview.ps1 -Mode Refined
```
The exporter validates vertex weights (finite, normalized, at most four influences) and writes `art-exports/EmberlingRefined.qa.json` after successful FBX creation, reporting bone names, mesh/vertex/triangle counts, clip ranges and file size. The PowerShell helper preserves any existing Assets FBX.

In Unity, choose **Dravio > Create or Open Refined Preview**. This configures the imported Generic rig and splits the five clips, then runs a structural audit before creating the separate EmberlingRefinedPreview scene, prefab and controller. The audit checks valid Generic Avatar, required bone names, named clip durations and loop flags, UV0 and skinned renderer references, and reports vertex/triangle counts and provisional budget warnings. The read-only **Dravio > Validate Refined Emberling FBX** menu repeats the audit after import configuration. A PASS is not a runtime or visual acceptance result. Existing preview scenes are preserved; if incomplete, back up/rename before retrying.

Deferred acceptance: verify Blender export and audit, Unity compile/import and structural audit, two walk cycles from side/front/rear, paw/claw attachment, foot sliding/penetration, sleep leg folding and ground contact, all five clips and interruptions, camera/HUD touch and language in landscape on actual devices, and VFX/performance. No Blender or Unity executable was available to the code author. Do not merge or claim final art until the checks pass.

## U2 integration preview — Sanctuary + character + domain

Technical merge PR #12 merged **only** into feat/u2-integration-preview, preserving original branches. The integration starts from Environment #10 and Core Domain #11 already merged into feat/u1-living-dragon (7538754), with U1.6 (a10ca5f) merged on top (29fb96b). No main merge. Domain and Environment source workstreams are not edited; the adapter and scene wiring are separate.

### Generate the unified scene

```powershell
cd "B:\Dravio\Dravio-unity"
git fetch origin
git switch -c feat/u2-integration-preview --track origin/feat/u2-integration-preview
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/art/Export-EmberlingPreview.ps1 -Mode Refined
```

Open Unity and choose **Dravio > Create or Open Integrated Sanctuary**. If necessary the menu builds the environment materials, prefabs and standalone environment scene first, then copies that scene and adds Refined Emberling at the clearing marker, fantasy HUD, VFX and session adapter. The output is `Assets/Dravio/Scenes/OriginIntegratedPreview.unity`. Existing standalone scenes are preserved. An existing integrated scene opens without rebuilding; if generation failed, back up/rename the incomplete scene before retrying. No extra environment FBX export required because PR #10 committed the generated assets.

### Scope

- One real in-memory GameState created using InitialStateFactory (150 G, Emberling, canonical founder state). Gold, lifecycle stage, XP and bond in the HUD come from Core Domain. The visual model stays the Baby mesh even if the numeric stage changes; adult/juvenile models are not implemented.
- Foreground session clock uses FixedClock, accumulates unscaled visible time and dispatches AdvanceTime approximately once a second. Focus loss, app pause and gaps over 5 s are not accrued; this is deliberately not the final online/offline policy. No persistence, autosave or offline grant. State resets when Play stops.
- Feed dispatches canonical FeedCommand and places a single fruit marker in the authored clearing. The one-fruit restriction belongs to this preview UI, not the domain rule. The dragon approaches it and tries ConsumeFoodCommand within .55 world units; eating animation only plays on FoodConsumed. The domain owns TTL, cooldown, XP and bond. Pet uses PetCommand and only plays Happy on DragonPetted. Errors display in the HUD. Sleep and inspection remain cosmetic. No navigation mesh, collision avoidance or cross-habitat travel.
- Landscape-only PlayerSettings retained; the original environment camera's portrait-fit component is disabled in the integrated copy. No camera orbit. Actual framing, touch UI and shader behavior require testing.
- No changes to U2.2 persistence work, achievements, tutorial, genetics, economy constants or canonical data. Domain still lacks the first-adult achievement reward and tutorial logic noted in U2.1.

### Deferred acceptance
1. Compile all Unity scripts/shaders and run Domain EditMode tests; independently run the .NET test project and environment tests.
2. Generate Refined FBX, pass structural audit, build environment, and create the integrated scene. Inspect Console, camera and all assets.
3. In fresh Play confirm 150 G at initialization, XP and gold advance with visible time. Feed spawns a fruit; approach and consume once, observe canonical XP/bond; confirm expiry and cooldown rejection. Pet twice rapidly: one accepted animation, one rejection. Check focus loss and resume without hidden-time accrual; stop and restart Play to confirm intentionally volatile state.
4. Check camera framing, movement inside clearing, no UI drag-through, language, landscape safe areas, shaders and performance on desktop and mobile.
5. Do not accept or merge to main until runtime tests pass. No independent Unity/Blender execution in this integration authoring environment; the previous PR test reports are not a substitute for integration testing.
