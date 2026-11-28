# ENV Alpha — painterly surface pass (AI3)

Baseline: 029e702; includes PR #81 and U6.0.4b. Scope: shared environment shaders and Origin pond material/builder, no scene regeneration required for this pass.

## Visual changes
- Origin/Ocean/Sky: two scales of world-space ground tonal variation, fine band attenuated using pixel footprint. Existing UVs, path mask, vertex AO and atlas stay intact. GroundDetail defaults to 0.18; set it to 0 for comparison.
- Alpha-tested foliage: orient the lighting normal to the visible side of the card; subtle shadow-aware leaf transmission (LeafTransmission defaults to 0.14). No change to card geometry, wind, alpha threshold or shadow/depth passes.
- Origin pond: opt-in directional sun glints, slight grazing-angle sheen and a narrower, broken foam crest. Retain original shore vertex mask, alpha, animated ripple textures, interaction rings and wind. WaterDetail is 1 on the committed Origin pond material and its material builder, 0 by default so Sky cloud material retains the previous response at its nonzero foam width.

## Performance and scope
No new textures, meshes, render passes, draw calls or scene buffers are introduced. Texture sample counts are unchanged by source inspection; additional ALU in ground, foliage and pond fragments is not profiled. No changes to GLB/FBX, collider/location data, saved gameplay, HUD, environment budgets or asset import rules. No new shader keywords. This is a shader/material art iteration, not a mesh repair or a complete art overhaul.

## Unity acceptance (pending, not run here)
1. Import in Unity 6000.6.10f1; verify both shaders compile for the target graphics APIs, no pink materials, no new console errors.
2. Open OriginLongTermGameplay_Environment, Ocean_Environment, Sky_Environment. Compare before/after at 16:9 and 20:9, day/dusk/night, and Focus zoom. Check for repetitive bands, aliasing, overbright or dark foliage, incorrect leaf backfaces, and ground/dragon contrast.
3. Origin: compare WaterDetail 0/1, check pond bank foam, light glints and dragon drink/splash rings. Sky: verify WaterDetail remains 0 and cloud look unchanged. Re-run Origin Setup Materials and verify the opt-in persists.
4. Run environment validators on the existing scenes; compare triangle/material/texture budgets to PR #81 and verify collider checks unchanged.
5. On target mobile devices compare frame time and shader cost against baseline; revert or lower detail if the extra fragment ALU is too expensive.

No Unity compile, screenshots or device performance measurements available for this commit. GLB shape/UV repair requires inspecting the actual meshes and visual output; not claimed here.
