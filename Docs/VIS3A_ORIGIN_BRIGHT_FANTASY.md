# VIS.3a — Origin Bright Fantasy surface and daylight pass

Baseline 4b3d598 (PR #88 responsive UI retained). Art direction: approved cute bright fantasy, guide PR #85. This is a first actual shader/material iteration, not a complete replacement of Origin or a claim of key-art quality.

## Changes
- Origin meadow: opt-in green-selective spring grading and broad world-space tonal variation, no extra texture sampling; path mask preserved.
- Existing trees and foliage: warmer greens, modest broad specular sheen; alpha-tested foliage visible-face lighting and shadow-aware backlight. Existing tree shape, atlas, wind, cutoff and shadow/depth passes retained.
- Origin pond: turquoise depth palette, direction-aware glint, narrower irregular shore foam; existing ripple interaction and alpha retained. No depth/opaque camera textures, no reflection probe.
- Origin daylight 8:00, 13:00, 16:30: softer warm key, cooler pastel fill and blue daytime haze. Default noon generator, JSON, serialized asset, builder, environment scene synchronized. Sun direction and shadow strength unchanged. Night/dawn/dusk keys, audio, interaction positions and Ocean/Sky _ZONE_DAY overrides unchanged.
- Emberling: existing VIS.1 material menu refreshes soft ramp, lighter shadow tint and highlight strength even if previously applied. Run menu after shader import; prefab itself is not changed by commit.

## Guardrails
Shared shaders use new parameters defaulting to zero. Only Origin material assets opt in. Any other scene reusing these exact Origin materials will inherit the grade; Ocean and Sky dedicated materials remain untouched. No new texture, mesh, render pass, light, scene placement, gameplay scene, UI, collider, save, GLB or rig changes. Existing texture sample counts unchanged by source comparison, extra ALU not profiled. PR #83 is a separate overlapping shader prototype; do not merge it blindly on top. VIS.3a supersedes its pond/foliage intent for Origin only.

## Checks performed remotely
Anchor checks, shader sample/pass/pragma counts and braces, JSON field-level diff, unchanged zone override generator tail. No Unity, Python test suite, shader compiler or actual screenshot performed here.

## Acceptance
1. Open Unity 6000.6.10f1, compile, check URP shader errors. Open OriginLongTermGameplay_Environment and playable scene. Compare same camera and exposure at 13:00, 8:00, 16:30 and night.
2. Verify greens not overbright, foliage backfaces and shadows, pond shoreline and drink/splash ripples. Check Focus and 16:9/20:9. Compare material _BrightFantasy/_BrightPond 0 vs 1 (0 disables new shader math, not changed material palette/daylight).
3. Use Dravio > Visual > VIS.1 Apply Stylized Emberling Material if desired, review before/after; preserves rig and animation.
4. Run environment validators and python -m unittest scripts/art/vis1/test_vis1.py; profile target mobile. Existing 33,352/60,000 scene triangles reported by VIS.1, not measured here.
5. Next VIS.3b: actual geometry/texture assets for fruit trees, rounded cottage/landmark silhouettes and foliage using approved separate concepts. Requires Blender export and visual checks. No new fruit, house, mountain, waterfall geometry is included here.
