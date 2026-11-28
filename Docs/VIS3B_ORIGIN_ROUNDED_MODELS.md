# VIS.3b — Rounded Origin geometry candidates

Depends on VIS.3a (PR #89). The current committed FBX files remain unchanged.
This pass adds a **staging-only Blender generator** for six existing assets:
Ancient Tree A/B, Round Shrub, Mossy Rock A/B/C.

- Smooth canopy and stone blobs towards soft ellipsoids while retaining every original blob's world-axis bounds, topology and RNG.
- Preserve trunk/root, foliage card positions, pivots, materials, atlas layout, and gameplay data.
- Add 3/4 ornamental red fruits to Tree A/B respectively (existing red mushroom-cap tile as an approximation, not a new item or harvest system). Raycast the actual canopy, require spacing and bounds, reject invalid hits. Proposed extra cost 240/320 tris per tree. Exact result is pending Blender.
- Fix VIS.3a's foliage backface assumption: the existing FBX generator bakes up-facing custom normals on both faces of cards; do not flip them to face down. Use restrained wrapped lighting instead, only for the opt-in Origin grade.

## Build and review

Run from repository root with Blender 5.3 and the generator's existing Python dependencies:

```powershell
blender --factory-startup --background --python-exit-code 1 --python scripts/art/environment/build_origin_vis3_models.py -- --out art-exports/vis3b
```

Use the full path to blender.exe if needed. Output is staging only, not Assets.
Checks before exporting: baseline triangle/material match with committed manifest, candidate UVs/finite geometry, no increase in degenerate faces, same material slots, same local AABB within 2 mm, per-asset budgets, scene total <60k. If any check fails, stop and report; do not increase the budget blindly. Inspect staged blend, FBX, validation.json and manifest. No actual checks have run here because Blender is not available.

If visual QA passes, copy only six staged FBX files into Assets/Dravio/Environment/Models, retaining existing .meta files, and stage the updated OriginSanctuaryManifest.json in Data (remove the staged marker after accepted import). Verify Unity import remaps, colliders, Focus, framing and environment budget; screenshots before/after. Existing generator remains unchanged; rebuild VIS.3b through this staging script after base generator updates, not by invoking the old full exporter alone. No new prefab or cottage/house is placed; any new landmark must be designed and checked against the existing sanctuary project/placement contract. No new GLB or 2D concept files included.

## Status
Source implementation only, not compiled or executed in Blender/Unity. No FBX replacement, scene placement changes, new texture or art quality claims. This iteration uses the current atlas, which limits the fruit texture realism; dedicated art may be needed.
