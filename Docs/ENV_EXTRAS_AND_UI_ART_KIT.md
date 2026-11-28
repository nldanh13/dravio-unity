# AI3 — UI art kit / environment intake

Baseline e075075, with VIS.1 and PR #81 retained. Original source PNGs remain unchanged except adding missing primary (same inspected green button as ui_btn_reward) and documenting semantic mapping. Several source labels/preview tiles were shifted; do not import by original names blindly. Runtime copies use the inspected appearance. The 1024px window was not reliably decoded here; the divider source is actually a legendary slot; no true divider is claimed. Dark common slot becomes the drawer panel. Rounded tab is available as a round control; existing tabs are wide text buttons so selected uses gold, unselected uses wood. Unused runtime textures are prepared, not connected to new screens. Source icons are original procedural 64px RGBA atlas cells, not illustrated final art.

## Runtime
Existing FantasyPreviewUI Start applies skin after original color pass; tabs retain selection, same localized labels and callbacks. Main/claim/secondary images use sliced sprites; dark panel retains camera occluder and drawer dimensions. Function and item icons are atlas sprites, selectively applied to existing action buttons and four pack controls. Other item/status icons are prepared for future inventory and status views, not connected to data. No save, gameplay, scene, collider, or economy changes. No scene rebuild necessary.

## Environment intake
- 2.5D candidates: Origin fern, grass, mushrooms; Ocean fan coral, anemone, seaweed. Inspect alpha/ground shadow before use, do not place raw concept background in scene.
- True mesh: Origin ruins/roots, Ocean branching coral and sunken architecture, Sky bridge/pillar/temple/cliff/island. Existing generated FBX assets are retained; replace only after silhouette and collision review.
- 12 sanctuary source GLBs in e075075 require mesh/material/UV/scale validation and Blender export; not processed by this UI PR.
- PR #83 shader work is separate; this branch starts on e075075 and does not merge #83.

## QA pending
Unity 6000.6.10f1: import and compile; open existing playable Origin scene, verify no missing textures, button/tabs normal/highlight/pressed/disabled states, language switching, claim actions, drawer open/close, safe area 16:9/20:9, labels at small width and pack icons, no unintended raycast interception. Check icon atlas cell order, alpha and pixel cropping. Compare frame time and memory on mobile. Source review only; no Unity runtime or device validation.
