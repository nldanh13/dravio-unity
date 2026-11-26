# DRG_magma_Baby

- Nguồn: `SAMPLE_magma_Baby.glb` — bằng chứng: **SAMPLE**. GLB nguồn không đổi: có
- Loài / giai đoạn: magma / Baby
- Kiểm định pipeline: PASS; Animation QA: **PASS**

## Rig

- Archetype `quadruped` (walks; limb count not stated (quadruped template once limbCount=4 is supplied)); limbCount 4 (mesh measurement accepted (--limb-count auto)); rig v7
- Chiều cao 0.9007 m (source size kept); pivot dời theo mặt đối xứng 0.0 m
- 26 bone (26 deform), tối đa 4 ảnh hưởng/đỉnh, 0 đỉnh không trọng số
- IK: 4 chuỗi chân; anchor: VFX_Mouth, VFX_TailTip, VFX_Back, VFX_Ground

Ghi chú rig:

- Idle blink not authored: mesh has no eyelid shape key (blink/eye_close); generated meshes rarely separate eyelids
- 112 vertices in floating fragments ignored for landmark detection
- no neck narrowing in the profile; neck base set between chest and head

## Clip

| Clip | Frame | Loop | Tốc độ (m/s) | Giãn p99.9 | Nén p0.1 | Trượt chân | Xuyên đất | Hở tiếp xúc | Khép vòng (°) | Nâng (m) | Vùng tệ nhất | Kết quả |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Idle | 120 | có | 0.000 | 1.143 | 0.876 | 0.0000 | 0.0001 | 0.0000 | 0.048 | 0.0000 | Tail1 50%, Pelvis 49% | PASS |
| Eat | 60 | — | 0.000 | 1.529 | 0.568 | 0.0000 | 0.0000 | 0.0000 | — | 0.0064 | Neck 56%, HindFoot_L 11% | PASS |
| Sleep | 120 | có | 0.000 | 1.483 | 0.634 | 0.0000 | 0.0000 | 0.0000 | 0.040 | 0.0430 | Neck 72%, Head 13% | PASS |
| Happy | 45 | — | 0.000 | 1.423 | 0.676 | 0.0000 | 0.0005 | 0.0000 | — | 0.0000 | Pelvis 48%, Tail1 42% | PASS |
| Walk | 22 | có | 0.198 | 1.328 | 0.688 | 0.0001 | 0.0005 | 0.0004 | 0.056 | 0.0000 | Pelvis 37%, HindFoot_R 22% | PASS |
| Run | 12 | có | 0.878 | 1.592 | 0.512 | 0.0000 | 0.0000 | 0.0002 | 0.056 | 0.0101 | Pelvis 35%, HindFoot_L 27% | PASS |
| Burrow | 60 | có | 0.000 | 1.423 | 0.679 | 0.0000 | 0.0000 | 0.0000 | 0.063 | 0.0216 | Pelvis 54%, Spine 21% | PASS |
| Rest | 120 | có | 0.000 | 1.191 | 0.659 | 0.0000 | 0.0000 | 0.0000 | 0.040 | 0.0430 | Pelvis 37%, HindLower_L 24% | PASS |
| Wake | 60 | — | 0.000 | 1.344 | 0.628 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Neck 36%, HindFoot_L 16% | PASS |
| LookAround | 90 | — | 0.000 | 1.279 | 0.765 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Neck 62%, Head 18% | PASS |
| Curious | 75 | — | 0.000 | 1.292 | 0.776 | 0.0000 | 0.0004 | 0.0000 | — | 0.0000 | Pelvis 45%, Tail1 20% | PASS |
| Drink | 75 | — | 0.000 | 1.584 | 0.530 | 0.0000 | 0.0000 | 0.0000 | — | 0.0066 | Neck 58%, HindFoot_R 12% | PASS |
| Stretch | 90 | — | 0.000 | 1.230 | 0.793 | 0.0000 | 0.0000 | 0.0000 | — | 0.0170 | Pelvis 40%, Spine 25% | PASS |
| Bask | 120 | có | 0.000 | 1.187 | 0.660 | 0.0000 | 0.0000 | 0.0000 | 0.040 | 0.0430 | Pelvis 37%, HindLower_L 25% | PASS |
| Greet | 60 | — | 0.000 | 1.155 | 0.854 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Pelvis 43%, Tail1 41% | PASS |
| DigPrep | 45 | — | 0.000 | 1.161 | 0.848 | 0.0014 | 0.0002 | 0.0012 | — | 0.0000 | Spine 29%, ForeLower_L 16% | PASS |
| BurrowEnter | 60 | — | 0.000 | 1.323 | 0.711 | 0.0000 | 0.0008 | 0.0000 | — | 0.0000 | Spine 33%, Neck 19% | PASS |
| BurrowEmerge | 60 | — | 0.000 | 1.259 | 0.737 | 0.0000 | 0.0008 | 0.0000 | — | 0.0000 | Neck 30%, Spine 29% | PASS |
| Nuzzle | 75 | — | 0.000 | 1.135 | 0.861 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Spine 39%, Neck 30% | PASS |

Ngưỡng (`character-budgets.json` ▸ `animationQa`, không nới): giãn ≤ 1.6, nén ≥ 0.45, trượt ≤ 0.02, xuyên ≤ 0.01, hở ≤ 0.03, khép vòng ≤ 0.5° (tương đối theo chiều cao).

## Cảnh báo QA

- không có

## LOD

| LOD | Tam giác | Đỉnh | Trong ngân sách |
|---|---|---|---|
| 0 | 24000 | 12090 | có |
| 1 | 10000 | 5090 | có |
| 2 | 4000 | 2090 | có |
- pass: LOD0 rest p95 deviation 0.000% (limit 0.40%)
- pass: LOD0 worst deformed p95 deviation 0.000% (limit 0.60%)
- pass: LOD1 rest p95 deviation 0.019% (limit 1.00%)
- pass: LOD1 worst deformed p95 deviation 0.025% (limit 1.40%)
- pass: LOD2 rest p95 deviation 0.054% (limit 2.50%)
- pass: LOD2 worst deformed p95 deviation 0.066% (limit 3.00%)

## PBR

- Bộ texture URP: metallic_smoothness=`DRG_magma_Baby_MetallicSmoothness.png`, normal=`DRG_magma_Baby_Normal.png`, base_color=`DRG_magma_Baby_BaseColor.png`
- Base Color: image; UV chồng lấn 0.0034

## Unity

- prefab: `Assets/Dravio/Character/Prefabs/DRG_magma_Baby_Rigged_LODGroup.prefab`
- material: `Assets/Dravio/Character/Materials/DRG_magma_Baby.mat`
- baseController: `Assets/Dravio/Character/Animation/DragonBase_quadruped.controller`
- overrideController: `Assets/Dravio/Character/Animation/Overrides/DRG_magma_Baby.overrideController`
