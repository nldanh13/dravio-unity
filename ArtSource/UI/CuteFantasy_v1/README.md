# Dravio UI Kit v1.0 — sprite candidates

Source sheets: generated in the approved Cute Stylized 3D style. Individual PNG files are native-resolution crops, with approximate alpha masking (not a clean original transparent render). Preview and manifest show actual dimensions. Do not upscale and call them high-resolution masters.

Unity: use sprite single, no mipmaps, clamp, alpha transparency, uncompressed for QA. 9-slice borders are suggestions only, in left/top/right/bottom order, verify each source. Decorated frames are fixed-aspect references, not validated for stretching. Separate ornaments and progress frame/fill need new art. Do not use a filled bar as a dynamic meter. UI text and interactions must remain independent.

Function/item mapping is tentative. Some icon semantics need new art (see manifest); no new gameplay elements or rarities. Source sheets are preserved in References. This package is not integrated into Unity or verified on devices.

## Handoff
Copy this folder to ArtSource/UI/CuteFantasy_v1/ on a separate Git branch. Do not copy references into Unity Resources. Keep old source assets intact. Import candidate assets after visual QA; review missing_or_unresolved in manifest.json. Only the source crop sizes are supplied; high resolution standalone art is still required for large panels and close-up icons.
