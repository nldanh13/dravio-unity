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

## Source GLB character pipeline

Optional, backward-compatible source mode on top of the unified preview. Provide a static unrigged dragon-only GLB with all parts in rest pose, no props/floor, no existing skeleton or shape keys. For a textured source, include UVs and glTF PBR materials. The importer does not remesh, decimate, sculpt over, reproject UVs or replace source materials. It joins mesh objects after baking their node transforms, rotates around Blender +Z by `--yaw-offset` (degrees), centers the X/Y bounding box, grounds the lowest vertex at Z=0 and scales overall height to .90 m. The front must face Blender -Y after yaw; 0 assumes already aligned. The exporter cannot infer the intended front or ground-contact anatomy from arbitrary geometry.

```powershell
cd "B:\Dravio\Dravio-unity"
git fetch origin
git switch -c feat/u2-glb-character --track origin/feat/u2-glb-character
$blender = "C:\Program Files\Blender Foundation\Blender 5.3\blender.exe"
& $blender --factory-startup --background --python-exit-code 1 --python scripts/art/build_emberling_living.py -- art-exports/EmberlingRefinedGLB.fbx --source-glb art-exports/EmberlingRaw.glb --yaw-offset 0
if ($LASTEXITCODE -ne 0) { throw "Blender export failed" }
Copy-Item "art-exports\EmberlingRefinedGLB.fbx" "Assets\Dravio\Art\Models\"
```

If the GLB faces +Y, use `--yaw-offset 180`; if it faces +X use -90; if it faces -X use 90 (directions in Blender after import). Add `--winged` when the input actually has two wings. The default Emberling remains canonically wingless: Wing_L/R are exported as unweighted placeholders unless `--winged` is set. Existing eye animation channels are retained, but textured eyes in a fused mesh do not physically blink without an appropriate eyelid rig. Wing animation clips are not added.

GLB mode activates the complete 445-frame Refined animation take automatically, without any --refined-preview flag. It fits 22 bones (the original 17 plus Spine, Neck, Wing_L, Wing_R, TailVFX_Anchor) using normalized bounds and vertex samples of low paw regions and the posterior tail region. Bone-heat binding uses ARMATURE_AUTO; the strongest four weights are normalized, and unweighted vertices cause a hard failure. This is an approximate anatomical fit, not reliable automatic rigging for every topology, and not IK or shape-aware retargeting. The same five clips and frame boundaries (Idle, Walk, Eat, Doze, Happy) are maintained. Bone positions, especially tail and wings, require visual QA.

FBX flags: axis_forward=-Z, axis_up=Y, add_leaf_bones=False, use_armature_deform_only=True, path_mode=COPY, embed_textures=True. All output modes use these flags. The source Blender material nodes and packed images are retained in EmberlingRefinedGLB.blend. FBX embedding retains maps that the exporter supports; FBX cannot represent every glTF PBR semantic, packed channel, UV transform, material extension or transparency behavior. The exporter also writes `EmberlingRefinedGLB_PBR/` containing original image bytes and `material-map.json` (glTF materials/textures/samplers). Keep this folder as recovery/reference; glTF metallicRoughnessTexture G is roughness, B is metallic, whereas Unity smoothness is 1 minus roughness. The sidecar is not automatically converted into URP materials. Inspect imported Base Color, normal map import settings, metallic/smoothness and transparency, extract/remap textures if necessary. Do not overwrite source material assets with the procedural paint.

Unity: choose **Dravio > Create or Open Integrated GLB Sanctuary**. It uses the new FBX, retains .90 m model height, the original landscape HUD and domain preview, and preferentially follows TailVFX_Anchor. It creates `Assets/Dravio/Scenes/OriginIntegratedGLBPreview.unity` separately from previous scenes, and a separate prefab/controller. The GLB path imports material descriptions and preserves existing URP/Lit assignments; conversion of non-URP materials copies known texture slots and values only (manual PBR remap may be necessary). The procedural skin/flame material replacement is skipped. Use **Dravio > Validate Refined GLB FBX** after configuration to check the extra bones and existing clips. The original procedural menu and modes are still available.

Artifacts: FBX, editable packed .blend, structural .qa.json and original PBR sidecar. Source GLB itself is never modified. New generated assets should be committed only after local inspection. Actual GLB asset was not supplied; no Blender/Unity execution or FBX round-trip was performed by the code author.

## U1-CHAR — Character asset pipeline

Branch `feat/u1-character-assets` (from `feat/u2-glb-character`). Tools in `scripts/art/character/` (Blender 5.x + pure Python) and `Assets/Dravio/Character/` (Unity Editor menus under **Dravio > Character**, shared budgets, 16-species modelling spec). Process, budgets and naming: `Assets/Dravio/Character/README.md`.

- Inspect UV / vertex colour / Base Color / Normal / Roughness / Metallic, bake vertex colour to a texture, create missing UVs, build LOD0/1/2 on one shared armature, validate mesh/texture/LOD against `Assets/Dravio/Character/Data/character-budgets.json`.
- Source GLB fix: `emberling_glb_source.py` now imports with glTF `merge_vertices`, because textured GLBs are split at every UV seam and the bone-heat bind otherwise fails with unweighted vertices.
- Verified only on procedural `TEST_*` models generated by `make_test_assets.py` with Blender 5.3 (`bpy`). No real Dravio GLB has been optimised, and the Unity scripts have not been compiled in Unity.

## U3 — Playable sanctuary integration

Branch `feat/u3-playable-sanctuary` combines GLB #16 with the user's Unity fixes, Environment #14/#18/#20 and Domain/Persistence #15/#17/#19. Technical merge PRs #21 and #22 target this isolated branch only. Main and the original AI branches are unchanged. Future character assets and hatching/breeding workstreams remain separate.

### First open

Stop Play, save any desired local edits and inspect `git status`. Keep local FBX and generated scenes. From the project folder:

```powershell
git fetch origin
git switch -c feat/u3-playable-sanctuary --track origin/feat/u3-playable-sanctuary
```

If the branch already exists, switch to it and pull --ff-only. If Git reports conflicting local changes, stop and review them; never reset or force switch. The existing `Assets/Dravio/Art/Models/EmberlingRefinedGLB.fbx` is reused, or the Refined procedural FBX if GLB is unavailable. No new export required. Refresh Unity, check Console, then choose **Dravio > Open Game**, and Play. This generates a new environment source `Assets/Dravio/Environment/Scenes/OriginPlayable_Environment.unity` and a new `Assets/Dravio/Scenes/OriginPlayable.unity` from current authored environment data. No old scene is overwritten. Generated environment material/prefab assets may be updated by the existing builder; back up custom changes first. Legacy preview menus are grouped, audits under Character. If generation fails and leaves an incomplete scene, back up/rename it before retrying.

### Gameplay

The new scene contains one DravioSessionHost. It owns tick, focus/pause, offline receipts, save/load, autosave and writer lock. The previous transient adapter clock is bypassed only in this scene. Existing legacy scenes retain their in-memory behavior. Save path: `Application.persistentDataPath/saves`. Save files are outside the project repository. Stop Play flushes via OnHide; save now also available in Care. A fresh persistent game starts from canonical founder state, not the prior transient preview. Do not manually delete save files to resolve errors; read the session status and preserve backups. No ×2 offline bonus implemented. The initial receipt is shown if present.

Four HUD tabs: Care, Biology, Dragons, Guide. Guide displays actual tutorial step and buttons for surveys/unlocks, Starter purchase (100 G), claim into meadow or storage. Biology includes explicit record-profile button. All commands route through the session and the domain validates prerequisites and rewards. Successful Starter transactions are saved immediately by GameSession. The roster includes newly acquired dragons; only the Emberling scene model exists today. Other species are data-only until artist assets arrive. Numeric growth does not change mesh. No general breeding or egg packs beyond Starter yet.

The current Origin location data is used to select the Emberling spawn and walkable target segments; food is placed nearby when safe. This is not A*, a navmesh, interhabitat movement UI or multi-dragon steering; it rejects paths intersecting authored obstacle/water zones rather than finding detours. Existing legacy previews retain the old waypoints. Camera fit observes the HUD panel and yields to close-up inspection, then returns to authored landscape framing. Landscape settings retained. Living environment adds day/night, ambient audio, wind and particles. Ocean/Sky environment kits are present for separate testing, but not unlocked in gameplay.

### Acceptance checklist

- Confirm Unity compilation (including new AI assemblies and NUnit tests), shader compilation and latest GitHub CI. The code author did not run Unity/Blender.
- Generate Open Game scene, inspect Console, spawn, UI and actual GLB color/rig. Check facing and food approach; verify no walk into pond/obstacle. Check right rail camera inset and inspection/resume.
- Start a fresh persistent save and verify canonical 150 G, feed, pet, profile, surveys/unlocks, Starter and rewards; ensure the roster changes. Try duplicate purchase/claim and rejected commands. Save, stop/reopen; check gold, dragons and tutorial preserved. Verify offline receipt and no double grant. Test read-only/invalid-save path without destroying user data.
- Check mobile landscape and performance; the user's GLB remains 114,100 triangles without UV0 and is not production ready. Character optimization is the separate AI assignment.
- No runtime acceptance or main merge until local testing succeeds.

## U4.1/U4.2 — Ecosystem integration and Hatchery

The isolated branch `feat/u4-ecosystem` builds on U3 and incorporates the actual Character Assets #25 and no-ad Hatching/Breeding #24 commits through technical merge PRs #26 and #27. The README append conflict was resolved only on U4; U3 and character documentation are both retained. No main merge. The requested later Character Library and Health/Disaster branches were not available on GitHub at integration time; no claim that those tasks are integrated.

### Unity upgrade

Stop Play, preserve local FBX and generated assets, inspect `git status` and switch only if Git permits without overwriting changes:

```powershell
cd "B:\Dravio\Dravio-unity"
git fetch origin
git switch -c feat/u4-ecosystem --track origin/feat/u4-ecosystem
```

If this local branch already exists, switch to it and use `git pull --ff-only`. Back up existing save files from `Application.persistentDataPath/saves` while Unity is stopped **before** opening U4; the Domain/Persistence implementation migrates schema 4 to 5 with the original v4 archive. Older builds may reject v5 saves. Do not overwrite or delete save files to downgrade.

In Unity, refresh and inspect Console, then **Dravio > Open Game** creates/opens a separate `OriginEcosystem.unity`, using the existing environment and GLB/Refined preview model. U3 scene remains intact. Same persistent session; no second economy clock. There is a new Eggs tab with the four gold-priced packs (prices read from PackCatalog), pending pack claim into meadow or storage, select distinct Adult dragons by UID, breed fee/rejection preview, start breeding, remaining incubation time based on the authoritative domain clock, tap egg and claim ready nest. All mutations go through the existing GameSession and canonical commands. This UI does not bypass eligibility, costs, cooldown, storage capacity, or save rules. No ads, no free rewarded-ad pack, no ad boost and no x2 offline grant. Numeric roster may include newly hatched dragons, but only Emberling is currently rendered. Health thresholds for breeding, full navigation, multiple visual residents and cross-realm travel await separate integration.

### One-command character preparation

This is optional; Open Game still uses the prior preview FBX. To process the actual static unrigged Emberling GLB with Blender 5.3 and a working Python executable:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/art/Build-EmberlingCharacter.ps1 -SourceGlb art-exports/EmberlingRaw.glb
```

Pass `-YawOffset`, `-TextureSize 2048` or `-Winged` only if appropriate to the source. The wrapper uses a unique run directory under art-exports/character-runs, inspects source, prepares UVs and existing color maps, rigs, creates LOD0/1/2, re-imports FBX for inspection, runs the Python validator and checks source SHA-256. Any failing step aborts. Default writes only to art-exports. `-InstallLods` additionally copies validated LOD FBX and available separate PBR maps into Character/Models and Character/Textures; refuses pre-existing destination or meta files. Select BaseColor texture and use the Character material menu if available, then select FBX (plus material) and create LOD prefab. This is **staging**, not replacement of the active model in Open Game. Missing source color cannot be reconstructed by UV creation; constant-color source is explicitly reported. Review topology/UV/weights, appearance, all five clips and LOD switching before integrating.

### Acceptance and rollback

Verify migration from a backed-up v4 save, canonical pack prices, insufficient funds, pending-pack rejection, breeding eligibility and fee, incubation online/offline, tap limits, one-time claim, save/reload and no regression to tutorial. Verify Character tooling compiles with Unity, run wrapper on real GLB, inspect reports and visual quality. The wrapper has not been executed on the user's machine; no Unity/Blender integration run by the author. Prior CI was successful on the individual Domain branch, not proof of this merged version. No original feature branch changed.
