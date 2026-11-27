# VIS.14 — Origin release candidate handoff

Pinned source selection across #89 and #92; #94 is QA staging only. 116 source files matched Git tree SHA and size, including all 83 UI master copies on pinned #94. No binary download or local verifier execution here. Existing source files preserved; no deletions or runtime changes. `manifest.json` is a selection list, NOT an auto-import list. `node scripts/art/environment/audit_origin_vis14_release.mjs` verifies raw bytes from local Git objects; fetch pinned commit history first. Missing history is failure, not permission to use another version.

## Gates / sequence
1. UI: retain exactly the 83 master paths; no VIS5 alternatives mixed in. Generate .meta via Unity importer and validate borders/scale, Vietnamese labels, theme creation/assign, safe-area and Focus; night icon stray alpha pixel still open. Record rollback to fallback.
2. Existing six FBX from #94: compile shader/C#, import and inspect unchanged GUIDs. Capture baseline 8h/13h/16h30/22h in 16:9/20:9 and safe-area; 4:3 exploratory, not supported contract. Validate collider, interactions, all anchors and performance.
3. Flora trial: LOD1 bloom/fern only after actual terrain and occlusion check; VIS13 positions are far north and not approved. Mushroom held because screen overlap. Keep RGB, alpha wind for bloom/fern, zero alpha mushrooms. Use shared white-base static/animated materials only after approval; no shader duplication. One mesh version per instance.
4. Architecture and water: HOLD until composition and material accepted. v2 cottage/lookout, v3 hills, VIS11 oriented water; no extra yaw. Do not use old preview embedded water maps alongside VIS9. Existing pond ripple behavior unchanged. No new walk/collision/water semantics.
5. Butterfly, canopy, VFX: optional after base scene approved, verify animation/alpha/culling and actual budget. Atlas textures are source tiles, not extra runtime maps. Emberling rig/UV and economy out of scope; separate #96.

## Numeric estimate
Baseline QA 34,472 tris; two flora trial candidates 35,640; all LOD1 once 36,120; hypothetical architecture/water/LOD1 + 2 butterflies 47,640 (not a placement approval). <60,000 triangle / <=10 materials existing Origin goals; texture cap Origin unset, 6 MiB belongs to Ocean/Sky. Need actual measured compression/mips, draw calls, GPU frame time and low/mid/high phone capture. No FPS guarantee. Unity/Android/Blender not run here. No merge.
