# Dravio Asset Tracker

## Owner workflow (GitHub Desktop, one checkout)
1. Keep the backup/local-unity-assets branch; never merge it blindly.
2. In B:\Dravio\Dravio-unity, choose main and Fetch/Pull origin.
3. Save a new GLB beside its reference image under ArtSource with the identical base filename (e.g. NatureAndGameplay/foliage/01_tree_sanctuary_oak.glb).
4. In GitHub Desktop commit only intended ArtSource files, then Push origin. Do not commit Unity changes with source assets. If branch protection requires a PR, use an asset branch and PR instead.
5. Actions > Dravio Asset Tracker validates and publishes generated reports on main; Pull origin again before the next edit because the bot may have added a commit. No additional clone or local Python installation is needed.
6. Do not overwrite a reviewed source without coordinating with AI 3. Keep original files and licenses locally; source assets in this public repository are public.

## Shared handoff for all AIs
- Read ArtSource/PROGRESS.md, progress.json and manifest.json on main before each task. Use the full relative asset ID, SHA-256 and source commit to identify the model. Do not rely on old branch copies.
- IMAGE_ONLY = needs conversion; CONCEPT_ONLY = scene reference, not necessarily a single mesh; GLB_UPLOADED = basic GLB 2.0 header/chunks and mesh JSON check passed; GLB_INVALID/AMBIGUOUS_GLB/UNMATCHED_GLB require review.
- AI 3 records separate evidence for inspection, optimization, rig/animation where relevant, and Unity-ready prefab/catalog in its PR; AI 1 records actual integration and runtime QA; AI 2 reviews gameplay compatibility. No automatic production approval and no AI chat notifications.
- Preview sheets excluded from conversion counts but included in source manifest. Seven scene concepts tracked separately. Missing original README/specs are not reconstructed.
- Reports are deterministic, generated only; no timestamps and no automatic deletion/replacement of source files. Invalid/unmatched assets are reported rather than silently counted. Basic GLB checks are not Blender, Unity, topology, texture or licensing QA.
- Workflow writes only reports on main (never a PR head), no force push, retries on concurrent push. If repository settings/branch protection deny bot push, the read-only validation job still supplies downloadable reports; an owner can run python scripts/art/track_sources.py and commit the three reports manually. GitHub's default GITHUB_TOKEN push does not recursively trigger another workflow.
