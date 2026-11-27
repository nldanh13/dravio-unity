# VIS.9 — Living water source art

Three procedural RGBA8 candidates: 256² waterfall silk, 256² stream curved ripples, and 512² sixteen-frame looping foam atlas (4×4, top-left row-major). The waterfall has longitudinal silk ribbons; stream has transverse curved ridges. Both repeat mathematically in U and V. Foam has animated crest and bubbles, with transparent cell gutters. No baked gameplay or water zones. Existing VIS3d/VIS5 assets unchanged.

Reproduce with Node 18+: `node scripts/art/environment/build_origin_vis9_water.mjs <new-dir>`.

Integration recommendation, not runtime changes: scrolling water material uses repeat, bilinear, sRGB and texture alpha, with direction checked against UV orientation on the staged waterfall/stream mesh. Do not treat as drop-in replacement for existing pond shader (samples RGB only, vertex alpha drives shore). Preserve drink/splash ripple contract. Use one texture per surface, avoid duplicate embedded/external imports. Foam: clamp atlas, 4x4 Whole Sheet, linear normalized lifetime across 16 frames, loop by controlled emitter lifetime; confirm importer row ordering and blending, use existing particle cap. Do not enable all three by default: source RGBA32 level-0 total 1.5 MiB, before mips/compression; estimate only, must fit existing 6 MiB scene budget with all other textures. Try replacement, not additive import. No collider, scene, shader or gameplay changes.

Actual checks: periodic sampling, directional gradients, foam frame/gutter/wrap alpha metrics, finite channels, deterministic regeneration, stored PNG scanline decode comparison. External PNG decoder, actual animation playback, Blender, Unity visual testing and device performance NOT RUN. Procedural art candidates, not hand-painted final art.
