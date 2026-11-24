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
