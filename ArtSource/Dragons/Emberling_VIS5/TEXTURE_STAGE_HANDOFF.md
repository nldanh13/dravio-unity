# Emberling VIS.5 — staged texture adaptation

Source concept 01_emberling.jpg inspected. Original GLB blob 826ebe5 verified by Git tree metadata only; binary could not be retrieved through connector. Five actual source material studies are not UV mapped. Do not paste them directly on imported dragon.

## Stage actual texture

Requires Python 3 + Pillow. Run from repo root:

    python scripts/art/character/vis5_emberling_texture_stage.py extract ArtSource/Dragons/images/01_emberling.glb art-exports/emberling-original

Inspect the extracted texture(s) and manifest material/texture mapping using Blender or the texture painting tool. Make grayscale L masks of the same dimensions, named image_<index>_skin.png, _belly.png, _horn.png, _flame.png, in a dedicated mask directory. White is selected, black preserved; sum of masks must not exceed 1 at any pixel. Exclude eyes, nostrils, mouth and existing baked highlights unless specifically reviewed. If the material is shared with a non-target region, do not assume region identity. Preserve UV island padding.

    python scripts/art/character/vis5_emberling_texture_stage.py grade ArtSource/Dragons/images/01_emberling.glb art-exports/emberling-graded --masks art-exports/emberling-masks --strength 0.18

Default blend 18%, cap 35%, approximate sRGB luma preserved (clipping/rounding can change it). No new detail is painted into the real UV automatically. Eye iris reference requires artist placement, not automatic replacement. Flame ramp does not emit by itself. Never grade normal/roughness/metallic images. Do not modify the source GLB or runtime prefab. Preview original vs derivative on the exact same mesh/material with approved masks before import. #95 retains rig and mesh ownership.

## Tests actually executed

Local Python/Pillow synthetic 4x4 RGBA texture and minimal GLB container: extraction preserved embedded PNG bytes, 14 selected opaque-or-partial pixels graded, zero-alpha and unmasked pixels unchanged, all alpha unchanged, overlap and wrong-size masks rejected. Tested script Git blob dd1d7594279551e42b9d310bd20673e2af1c5f3b. Actual Emberling extraction/grade, UV review, material assignment, Blender/Unity render NOT RUN. Only embedded PNG/JPEG base colors supported. Pillow getdata emitted deprecation warning; future Pillow 14 migration needed. Original four canonical palette colors unchanged.
