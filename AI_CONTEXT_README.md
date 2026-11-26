# DRAVIO — AI context snapshot (Gemini)
Source: https://github.com/nldanh13/dravio-unity/tree/486ee633c7e4807a855f7aee95aa889117b5bbde
Snapshot of main at 486ee633c7e4807a855f7aee95aa889117b5bbde, 2026-11-26. This branch is a generated-style read-only reference, NOT a Unity project. Never merge it into main or use it as a base for development. It excludes binary assets, Unity scenes and runtime implementation. The source documents are copied verbatim and may include historical proposals; current canonical decisions and code take priority.

## Start here
- README.md (project history, build and integration notes)
- Assets/Dravio/Data/species-biology.json (canonical species and habitats)
- Assets/Dravio/Character/SPECIES_MORPHOLOGY.md and Assets/Dravio/Environment/README.md (art direction and biology)
- Assets/Dravio/Domain/Catalog/SanctuaryProjectContent.cs (20 named projects)
- Docs/adr/ and Docs/u2.13/DECISIONS.md (review status and rules; inspect exact wording)
- ASSET_STATUS.md and asset-status.json (source file presence at snapshot, not GLB quality)
- ArtSource/manifest.json (last committed manifest, may lag actual source files)

## Gemini concept art role
Create isolated object reference images for image-to-3D; cozy ancient fantasy, stylized hand-painted mobile Unity art, rounded silhouettes, neutral background, full object in frame, consistent reference style, no text or logos. User converts images to GLB. Avoid duplicates, use exact source IDs, confirm proposed new designs with owner. Existing scene concepts are not required to become single GLBs. AI 3 owns optimization and rig/prefab; AI 1 integration; AI 2 Domain and simulation. Attach actual reference images separately if visual consistency matters, because this branch contains text only.

This is a static snapshot, not a live Gemini sync. Do not infer pending PRs or Unity execution status. For updated data, regenerate from main and reimport.