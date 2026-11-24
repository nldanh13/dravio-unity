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
