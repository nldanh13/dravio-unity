# scripts/art/character — U1-CHAR character asset tools

Full process, budgets and naming: `Assets/Dravio/Character/README.md` (Vietnamese).

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
| `tests/` | Python only | `python -m unittest discover -s scripts/art/character/tests` |

All Blender scripts accept arguments after `--` and also run with the `bpy` module
(`pip install bpy==5.3.*`, then `python script.py <args>`). No tool writes to its input file.
