# VIS.7 — Emberling UV painting handoff

CHAR.9c reports a single source mesh, 2048² base color JPEG and 2048² metallic-roughness PNG; no normal map. The repaired FBX has 24,000 triangles, UV0 and embedded 2048² BaseColor. The CHAR.9c report records owner approval of the FBX change, but Unity is not yet verified. Source GLB remains unchanged. Do not replace the FBX or modify skin weights.

From repo root, create the parent `art-exports` if needed. Node 18+:

    node scripts/art/character/emberling_uv_guide.mjs --self-test
    node scripts/art/character/emberling_uv_guide.mjs ArtSource/Dragons/images/01_emberling.glb art-exports/emberling-uv-guide

The guide draws actual TEXCOORD triangles over a transparent SVG canvas matching embedded image dimensions. Cyan/orange distinguishes UV signed area; this does not establish overlap, semantic region identity, or normal-map tangent correctness. glTF UV image origin is top-left. The script pins SHA-256, refuses transformed/compressed unsupported primitives, missing UV, or base color shared with non-base maps. Source-only, no texture write. Inspect guide over the extracted original using `vis5_emberling_texture_stage.py extract`; manually paint disjoint L masks in the exact original UV and protect eyes, mouth and baked highlights. Use the existing grade command with 0.18 default strength and inspect original vs derivative on the same mesh. Do not remap metallic/roughness. Do not assume the retopologized FBX UV is identical: compare actual FBX texture/UV visually in Blender first; preserve original embedded texture as fallback.

Executed 9 synthetic core assertions: indexed UV triangles, orientation, bounds, SVG coordinate mapping, rejects shared data map, transformed UV, out-of-bounds accessor, missing UV and flags outside-unit UV. Full Node CLI, real binary GLB/FBX, actual masks, render and Unity NOT RUN (binary retrieval unavailable). No new skin texture has been produced by this commit. No gameplay or runtime changes.
