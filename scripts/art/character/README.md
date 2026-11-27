# scripts/art/character — U1-CHAR character asset tools

Test reports: `TEST_REPORT_U3.md`, `TEST_REPORT_U4.md` (rig v2 / animation QA), `TEST_REPORT_U5.md` (rig v3, SAMPLE before/after, reports), `TEST_REPORT_U6.md` (behaviour clips, planner, performance), `TEST_REPORT_U7.md` (advanced motion: water/air/perch/burrow, motion contract). Full process, budgets and naming: `Assets/Dravio/Character/README.md`; batch import, archetypes and the living-dragon runtime: `Assets/Dravio/Character/LIVING_DRAGONS.md` (Vietnamese).

| Script | Needs | Purpose |
|---|---|---|
| `inspect_character.py` | Blender 5.x / bpy | Read-only report: mesh, UV, vertex colour, Base Color/Normal/Roughness/Metallic sources, topology, skin, textures |
| `prepare_character.py` | Blender | Weld glTF seam splits, create UV0, bake vertex colour (or other Base Color data) to a texture, export URP texture set |
| `build_lods.py` | Blender | LOD0/1/2 sharing one armature; shape, skinning, UV and texture-drift metrics |
| `validate_character.py` | Python only | Gate reports against `Assets/Dravio/Character/Data/character-budgets.json` |
| `build_species_spec.py` | Python only | 16-species modelling spec from canonical data (`--check` for staleness) |
| `render_preview.py` | Blender | QA contact sheets (bake comparison, LODs at an animation frame) |
| `make_test_assets.py` | Blender | Procedural, licence-clean TEST models (never shipped) |
| `run_selftest.py`, `selftest_checks.py` | Blender | End-to-end run + cross-checks on the TEST models |
| `build_archetypes.py` | Python only | Rig/animation archetype + clip set per species from canonical locomotion/silhouette (`--check`) |
| `rig_archetype.py` | Blender | Rig v2: archetype skeleton placed on measured landmarks (`rig_geometry.py`), chain weights + bounded blend bands, ≤4 influences, species clips with ground clearance and wing limits, deformation check |
| `repair_chibi_rig.py` | Blender | CHAR.9b, opt-in per species (`repair` in the overrides; batch step after `rig_archetype`): linear skinning as Unity (`skinning`), Neck/Head share, geodesic weight blend around sharp leg–body transitions, anatomy-aware clip amplitude (`head`, `legs`, `knees`) |
| `export_emberling_legacy_contract.py` | Blender | CHAR.9: rig v7 Emberling → `Assets/Dravio/Art/Models/EmberlingRefinedGLB.fbx` (22-bone / 5-clip contract of `EmberlingAssetAudit`) + `_contract_rig.json` so `anim_qa.py` can measure the export and the re-imported FBX |
| `measure_skin_deformation.py` | Blender | Worst-frame stretch/compression (stricter diagnostic than `anim_qa.py`'s pooled percentiles) |
| `rig_geometry.py` | Python + numpy | Geometric body plan: slices, feet from ground contacts, head/tail, manual landmarks, weight helpers, gait tracks |
| `anim_qa.py` | Blender | Animation QA per model (`--samples all --strict-frame`: every frame, limits also on the worst single frame, seam-crack check; default stays 8 pooled frames): stretch/compression, foot sliding, ground penetration, contact gap, moonwalk, loops, bounds, facing, wing-body penetration, weights, bone orientation, height, LODs → `<Name>_animqa.json` (+ `.png` with `--images`) |
| `compare_animqa.py` | Python only | Before/after table of two folders of `*_animqa.json` (Vietnamese markdown) |
| `run_u4_local.ps1` | Windows, Blender, Python | Local U1-CHAR.4 run on your own GLBs: QA of the old rig, v2 batch, before/after report, source hash check |
| `run_u5_local.ps1` | Windows, Blender, Python (+ Unity optional) | Local U1-CHAR.5 run on your own GLBs: QA of the U4 rig, v3 batch, before/after, `build_character_reports.py`, optional Unity batch import + EditMode tests, source hash check |
| `run_u6_local.ps1` | Windows, Blender, Python (+ Unity optional) | Local U1-CHAR.6 run: QA of the U5 rig, v4 batch (behaviour clips), before/after, reports, optional Unity import + EditMode tests |
| `run_u7_local.ps1` | Windows, Blender, Python (+ Unity, .NET optional) | Local U1-CHAR.7 run on 01_emberling .. 07_sparkwing: QA of the U6 rig, v5 batch (motion clips, transition + envelope QA), before/after, reports (capability matrix, motion report), optional .NET tests/benchmark and Unity import + EditMode tests |
| `build_character_reports.py` | Python only | Per-model QA pages, prefab catalog (md/json), animation report (clips vs biology), static performance budget; labels SAMPLE vs real |
| `build_prop.py` | Blender | Static environment prop: pivot, LODs (prop profiles), simple collider, size metadata; never rigged |
| `batch_import.py` | Blender | Batch: source folder check, filename (+ aliases) -> species/stage or prop category, per-species/per-file overrides, cache on source + pipeline + config hashes, full chain incl. animation QA, Unity staging + `import-manifest.json` (approved assets never re-delivered), report |
| `make_sample_library.py` | Blender | Procedural "generator-like" SAMPLE GLBs for the batch test (never shipped) |
| `audit_source_glbs.py` | Blender | CHAR.9c: read-only audit of the original dragon GLBs (glTF container + welded geometry: topology, islands, winding, UV mirroring, textures, scale, skins/animations), SHA-256 checked |
| `verify_contract_fbx.py` | Blender | CHAR.9c: check the Unity contract FBX as Unity sees it (empty scene, linear skinning, every frame): 22-bone contract, weights, worst-frame stretch/compression, ground, seam cracks; optional Cycles renders |
| `tests/` | Python only | `python -m unittest discover -s scripts/art/character/tests` |

All Blender scripts accept arguments after `--` and also run with the `bpy` module
(`pip install bpy==5.3.*`, then `python script.py <args>`). No tool writes to its input file, except `repair_chibi_rig.py`, which edits the batch's own `_Rigged.blend` / `_rig.json` in the work folder (never a source GLB). `anim_qa.py` reports `skinning`: Unity plays `linear`; rig v7 alone leaves Blender on dual quaternion.
