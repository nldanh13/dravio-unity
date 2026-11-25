# scripts/art/character — U1-CHAR character asset tools

Test reports: `TEST_REPORT_U3.md`, `TEST_REPORT_U4.md` (rig v2 / animation QA). Full process, budgets and naming: `Assets/Dravio/Character/README.md`; batch import, archetypes and the living-dragon runtime: `Assets/Dravio/Character/LIVING_DRAGONS.md` (Vietnamese).

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
| `rig_geometry.py` | Python + numpy | Geometric body plan: slices, feet from ground contacts, head/tail, manual landmarks, weight helpers, gait tracks |
| `anim_qa.py` | Blender | Animation QA per model: stretch/compression, foot sliding, ground penetration, contact gap, moonwalk, loops, bounds, facing, wing-body penetration, weights, bone orientation, height, LODs → `<Name>_animqa.json` (+ `.png` with `--images`) |
| `compare_animqa.py` | Python only | Before/after table of two folders of `*_animqa.json` (Vietnamese markdown) |
| `run_u4_local.ps1` | Windows, Blender, Python | Local U1-CHAR.4 run on your own GLBs: QA of the old rig, v2 batch, before/after report, source hash check |
| `build_prop.py` | Blender | Static environment prop: pivot, LODs (prop profiles), simple collider, size metadata; never rigged |
| `batch_import.py` | Blender | Batch: source folder check, filename (+ aliases) -> species/stage or prop category, per-species/per-file overrides, cache on source + pipeline + config hashes, full chain incl. animation QA, Unity staging + `import-manifest.json` (approved assets never re-delivered), report |
| `make_sample_library.py` | Blender | Procedural "generator-like" SAMPLE GLBs for the batch test (never shipped) |
| `tests/` | Python only | `python -m unittest discover -s scripts/art/character/tests` |

All Blender scripts accept arguments after `--` and also run with the `bpy` module
(`pip install bpy==5.3.*`, then `python script.py <args>`). No tool writes to its input file.
