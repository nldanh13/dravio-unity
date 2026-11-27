# VIS.5 VFX source handoff — Emberling / Origin

Seven RGBA8 sRGB source PNGs, no runtime import. One 512² 4×4 flame sheet (16 frames of 128²), four 128² textures (firefly, magic spark, dust, water drop), two 256² textures (pond ring, foam). Total uncompressed level-0 RGBA32 1.75 MiB (before compression); theoretical ASTC 4×4 0.4375 MiB, not measured. The flame has three tongues, warm core and stable lower anchor. Static preview of all frames, not an animation render. Source atlas row-major, top-left first. The baked mesh flame in Emberling must remain; test a small overlay only if it enhances rather than duplicates silhouette. Use existing flameBone / TailVFX_Anchor after technical AI #95 confirms actual anchor; do not modify rig, emitter or light here. No added dynamic lights.

## Technical references consulted
- Unity 6 WholeSheet ordering: https://docs.unity3d.com/6000.0/Documentation/ScriptReference/ParticleSystemAnimationType.WholeSheet.html
- Unity 6 texture sheet animation: https://docs.unity3d.com/6000.0/Documentation/ScriptReference/ParticleSystem-textureSheetAnimation.html
- URP Particles Unlit inspector: https://docs.unity3d.com/6000.0/Documentation/Manual/urp/particles-unlit-shader.html
- Existing EnvironmentAmbientParticles.cs, LivingEnvironmentBuilder.cs, EmberlingFlameVisual.cs at 4b3d598.

## Integration candidate (NOT applied)
- Flame: URP Particles/Unlit, alpha blend, grid 4x4, Whole Sheet, suggested 16–24 frames/sec (0.67–1s/cycle), verify ordering, loop and stable foot. Start with one small card, test camera overlap. Do not increase existing tail light intensity or change animation. Disable optional effect if the existing flame mesh reads better.
- Firefly / magic spark / dust: neutral RGB white; existing particle tint supplies color. Additive; avoid a second large halo, and check bright daytime backgrounds.
- Water drop / foam: alpha blend; don't globally replace M_ENV_FX_Alpha because burstFall is shared by Splash and DustPuff. burstRise likewise handles petals, rune, heat, fireflies, bubbles and clouds. Keep old generic material unless explicit per-style routing is approved; don't create additional emitters just for decoration without profiling.
- Pond ring: neutral white alpha, use existing HorizontalBillboard, size-over-life, color and pond volume. Preserve day/night weighting and water interaction.
- Preserve existing ambient caps (36+14 fireflies, 48 dust, 8 rings; quality scale .65/.35, actual builder reads volume counts). Existing burst systems each have cap 64, individual bursts max16. No automatic new particle budgets or shader passes. No claim that new textures reduce overdraw.
- Texture import on QA branch: clamp/bilinear, sRGB, alpha transparency, non-readable; start with no mipmaps on flipbook to avoid cross-cell bleed; check aliasing, test ASTC 4×4 first and fallback/device support. For world effects, evaluate mips separately if needed. Preserve per-frame gutters; no tight pack/rotation. No frame blending until measured. Check alpha sorting, nighttime brightness, daytime legibility, mobile GPU overdraw, Frame Debugger batches and actual texture memory.

## QA
Generation checked finite pixels, independent PNG decode roundtrip, non-empty alpha for each cell, transparent gutters (flame minimum 15px). Source swatch inspected and flame silhouette revised. No Blender/Unity, external PNG decoder, rendered animation, compression, GPU or device QA. These are procedural candidates, not approved final art. Existing source art and runtime untouched. #91 handles environment integration, #95 character anchor/rig ownership.
