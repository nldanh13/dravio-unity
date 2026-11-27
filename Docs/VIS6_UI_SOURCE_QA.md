# VIS.6 — source UI QA handoff (#90/#91)

Baseline PR #92 b9401163527d27e24a4ba4adff9839f850328f57; contract from integrated #88. Existing masters, alternatives and runtime files unchanged.

## Verified in this pass
- 83/83 required master states: exact ID/state, candidate runtime path, dimensions and border declarations, alpha count total, unique source paths and Git tree blob/size. Zero errors.
- 6 alternatives (two complete three-state button families): contract and Git tree match. 5 optional ornaments match Git tree; not required slots.
- 16 synthetic assertions executed against the pure JS core of the new validator (including all five PNG scanline filters, missing/duplicate state, border, alpha, coverage and edge checks).

## Run locally
From repository root with Node.js 18+:

    node scripts/art/ui/audit_vis6_masters.mjs --self-test
    node scripts/art/ui/audit_vis6_masters.mjs > ui-vis6-audit.json

The second command verifies actual PNG signature, CRC, RGBA8, blob SHA, dimensions, alpha counts, icon/item coverage and exact alpha silhouette across states. Reports edge/bounds warnings. Master and polish sets are checked independently: do not mix state artwork between them. No auto copy into Assets. Read-only except your optional redirected report.

## NOT RUN
Connector is text-only for PNG binary and container cannot resolve github.com; full PNG decode/CRC against committed images, CLI execution, Unity import, stretched 9-slice visual preview, label contrast with actual runtime tint, device memory and performance are NOT RUN. Metadata pass is not art approval. Alpha silhouette equality does not guarantee internal details align. Ornaments require non-raycast overlays and separate review. No runtime or gameplay changes, no budget adjustment. Keep fallback until Unity QA approves.
