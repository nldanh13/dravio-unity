# CHAR.9 — Mobile performance

**Android verified: NOT RUN. Unity Editor verified: NOT RUN.** Không có số đo thiết bị trong CHAR.9. Mọi con số dưới đây là **static analysis** từ pipeline Blender.

| Hạng mục | Giá trị | Nguồn | Ngưỡng DRAVIO (U5.8B, đề xuất) |
|---|---|---|---|
| Rồng LOD0/1/2 | 24 000 / 10 000 / 4 000 tam giác | pipeline (đã đo) | 15–25k / 8–12k / 3–5k |
| Bone / ảnh hưởng | 17–32 / ≤ 4 | rig.json | ≤ 75 / ≤ 4 |
| Material / rồng | 1 | pipeline | ưu tiên 1–2 |
| Texture | nguồn 2048² → pipeline 1024² (gói `Imported`); `EmberlingRefinedGLB.fbx` nhúng 2048² (importer nên đặt Max Size 1024, ASTC 6×6) | đo | khuyến nghị 1024, tối đa 2048 |
| Animation bake | 0,31–0,82 MB / model trước nén Optimal | performance-budget.md | — |
| 30 rồng (ước tính) | ~318 000 tam giác vẽ, ~17 Animator đánh giá / frame | performance-budget.md | 500 000 tam giác, 350 draw, 160 SetPass |

Chưa đo: FPS trung bình / 1 % low, draw/SetPass thật, bộ nhớ PSS, nhiệt, 20 phút gameplay. Cách đo: `Dravio ▸ Character ▸ Create Stress Test Scene` (Development Build, 5/15/30 rồng, warmup ≥ 30 s, sample ≥ 60 s), rồi `python scripts/qa/check_alpha_stress.py <report.json> --target 30` (nhánh `feat/u5-6-player-journey`).
