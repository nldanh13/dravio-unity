# Cute Fantasy UI v1 — art package intake

AI3, issue #86. This branch prepares the upload only. **The binary assets are not yet in GitHub**. The owner has the ZIP in the ChatGPT conversation.

- Archive: Dravio_UI_Kit_v1.zip
- SHA256: fc0b086660569dd5537c287ef553c1dc4721635a3d9f52b0da8287213dfed25b
- Verified archive integrity locally; 94 files, 83 draft sprites (22 buttons, 7 frames, 2 tabs, 9 slots, 4 bar references, 27 function icons, 12 item icons). Includes approved concept boards, preview and manifest.
- Source-only destination: `ArtSource/UI/CuteFantasy_v1/`.
- Do not overwrite existing ArtSource, use in runtime Resources, or merge into shared branch before visual review.

## Owner import
In GitHub Desktop fetch and switch to `ai3/cute-fantasy-ui-assets`. Extract the folder `Dravio_UI_Kit_v1` from the ZIP and rename/copy it to the destination above. Alternatively from the repository root run:

```powershell
py scripts/art/ui/import_cute_fantasy_kit.py "$env:USERPROFILE\Downloads\Dravio_UI_Kit_v1.zip"
```

Adjust path if saved elsewhere. The script verifies the archive hash, file count and each sprite hash before copying; refuses to overwrite an existing target. It does not commit, push, modify gameplay or import Unity assets. Then commit and push in GitHub Desktop.

## QA
These are native-resolution draft crops, approximate alpha and tentative borders, not high-res masters. Inspect alpha, edge artifacts, 9-slice and mapping. Missing five function icons (rest/day/night/upgrade/temperature), five item icons (mystic/astral/aether/aquatic/stardust), dynamic progress frame/fill, decorative corners/edges and verified standalone tabs/portrait/divider. See manifest after upload. Unity NOT RUN.
