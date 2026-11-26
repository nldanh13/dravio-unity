# DRG_steamdrake_Unstaged

- Nguồn: `05_steamdrake.glb` — bằng chứng: **thực tế (chưa nghiệm thu)**. GLB nguồn không đổi: có
- Loài / giai đoạn: steamdrake / Unstaged
- Kiểm định pipeline: PASS_WITH_WARNINGS; Animation QA: **PASS_WITH_WARNINGS**

## Rig

- Archetype `serpentine` (bodyStyle == Serpent); limbCount 4 (mesh measurement accepted (--limb-count auto)); rig v7
- Chiều cao 1.129 m (source size kept); pivot dời theo mặt đối xứng 0.0 m
- 31 bone (31 deform), tối đa 4 ảnh hưởng/đỉnh, 0 đỉnh không trọng số
- IK: 4 chuỗi chân; anchor: VFX_Mouth, VFX_TailTip, VFX_Back, VFX_Ground

Ghi chú rig:

- Idle blink not authored: mesh has no eyelid shape key (blink/eye_close); generated meshes rarely separate eyelids
- 1889 vertices in floating fragments ignored for landmark detection
- contact cluster at y=-0.576 split at x=0.000 (empty band 0.096 m): left/right paws standing close together
- no neck narrowing in the profile; neck base set between chest and head

## Clip

| Clip | Frame | Loop | Tốc độ (m/s) | Giãn p99.9 | Nén p0.1 | Trượt chân | Xuyên đất | Hở tiếp xúc | Khép vòng (°) | Nâng (m) | Vùng tệ nhất | Kết quả |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Idle | 120 | có | 0.000 | 1.088 | 0.917 | 0.0000 | 0.0000 | 0.0000 | 0.093 | 0.0000 | Neck 38%, Head 26% | PASS |
| Eat | 60 | — | 0.000 | 2.353 | 0.492 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Neck 57%, Head 14% | WARNING |
| Sleep | 120 | có | 0.000 | 4.127 | 0.261 | 0.0000 | 0.0000 | 0.0000 | 0.093 | 0.0391 | HindUpper_R 16%, Spine3 16% | WARNING |
| Happy | 45 | — | 0.000 | 1.141 | 0.877 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Tail1 34%, Neck 21% | PASS |
| Walk | 40 | có | 0.152 | 2.278 | 0.575 | 0.0018 | 0.0000 | 0.0047 | 0.093 | 0.0158 | Spine3 21%, HindUpper_R 12% | WARNING |
| Run | 26 | có | 0.460 | 3.107 | 0.509 | 0.0019 | 0.0000 | 0.0033 | 0.097 | 0.0184 | Spine3 21%, HindUpper_L 10% | WARNING |
| Swim | 48 | có | 0.746 | 4.359 | 0.606 | 0.0000 | — | 0.0000 | 0.093 | 0.0000 | Spine3 43%, Spine4 13% | WARNING |
| Rest | 120 | có | 0.000 | 3.356 | 0.288 | 0.0000 | 0.0000 | 0.0000 | 0.093 | 0.0326 | Spine3 27%, HindUpper_R 13% | WARNING |
| Wake | 60 | — | 0.000 | 3.170 | 0.315 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Spine3 21%, HindUpper_R 14% | WARNING |
| LookAround | 90 | — | 0.000 | 1.469 | 0.682 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Neck 42%, Head 30% | PASS |
| Curious | 75 | — | 0.000 | 1.364 | 0.609 | 0.0000 | 0.0000 | 0.0000 | — | 0.0055 | HindUpper_L 20%, HindUpper_R 17% | PASS |
| Drink | 75 | — | 0.000 | 2.943 | 0.317 | 0.0000 | 0.0000 | 0.0000 | — | 0.0103 | Neck 39%, Spine3 15% | WARNING |
| Stretch | 90 | — | 0.000 | 1.811 | 0.460 | 0.0000 | 0.0000 | 0.0000 | — | 0.0115 | Neck 19%, Spine3 17% | WARNING |
| Bask | 120 | có | 0.000 | 3.215 | 0.292 | 0.0000 | 0.0000 | 0.0000 | 0.093 | 0.0324 | Spine3 26%, ForeLower_R 12% | WARNING |
| Greet | 60 | — | 0.000 | 1.176 | 0.879 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Neck 48%, Spine3 19% | PASS |
| SwimIdle | 72 | có | 0.000 | 1.829 | 0.560 | 0.0000 | — | 0.0000 | 0.093 | 0.0000 | Spine3 24%, ForeLower_R 16% | WARNING |
| SwimRest | 120 | có | 0.000 | 1.349 | 0.705 | 0.0000 | — | 0.0000 | 0.097 | 0.0000 | Spine3 30%, ForeLower_R 16% | PASS |
| SwimSurface | 48 | có | 0.746 | 3.267 | 0.585 | 0.0000 | — | 0.0000 | 0.097 | 0.0000 | Spine3 30%, ForeLower_L 10% | WARNING |
| WaterEnter | 60 | — | 0.000 | 3.001 | 0.648 | 0.0000 | — | 0.0000 | — | 0.0000 | Spine3 36%, ForeLower_L 10% | WARNING |
| WaterExit | 60 | — | 0.000 | 3.040 | 0.655 | 0.0000 | — | 0.0000 | — | 0.0000 | Spine3 37%, ForeLower_L 10% | WARNING |
| Nuzzle | 75 | — | 0.000 | 1.244 | 0.856 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Neck 47%, Spine3 18% | PASS |

Ngưỡng (`character-budgets.json` ▸ `animationQa`, không nới): giãn ≤ 1.6, nén ≥ 0.45, trượt ≤ 0.02, xuyên ≤ 0.01, hở ≤ 0.03, khép vòng ≤ 0.5° (tương đối theo chiều cao).

## Cảnh báo QA

- Eat: deformation — edge stretch over limit (2.353 / 1.6)
- Sleep: deformation — edge stretch over limit (4.127 / 1.6)
- Sleep: deformation — edge compression over limit (0.261 / 0.45)
- Walk: deformation — edge stretch over limit (2.278 / 1.6)
- Run: deformation — edge stretch over limit (3.107 / 1.6)
- Swim: deformation — edge stretch over limit (4.359 / 1.6)
- Rest: deformation — edge stretch over limit (3.356 / 1.6)
- Rest: deformation — edge compression over limit (0.288 / 0.45)
- Wake: deformation — edge stretch over limit (3.17 / 1.6)
- Wake: deformation — edge compression over limit (0.315 / 0.45)
- Drink: deformation — edge stretch over limit (2.943 / 1.6)
- Drink: deformation — edge compression over limit (0.317 / 0.45)
- Stretch: deformation — edge stretch over limit (1.811 / 1.6)
- Bask: deformation — edge stretch over limit (3.215 / 1.6)
- Bask: deformation — edge compression over limit (0.292 / 0.45)
- SwimIdle: deformation — edge stretch over limit (1.829 / 1.6)
- SwimSurface: deformation — edge stretch over limit (3.267 / 1.6)
- WaterEnter: deformation — edge stretch over limit (3.001 / 1.6)
- WaterExit: deformation — edge stretch over limit (3.04 / 1.6)

## LOD

| LOD | Tam giác | Đỉnh | Trong ngân sách |
|---|---|---|---|
| 0 | 24000 | 15339 | có |
| 1 | 10000 | 8097 | có |
| 2 | 4000 | 4493 | có |
- warning: DRG_steamdrake_Unstaged_LOD1: 3471 UV faces (34.71%) flipped_faces > 1.0%
- warning: DRG_steamdrake_Unstaged_LOD1: 1 loose vertices
- warning: DRG_steamdrake_Unstaged_LOD1: 2626 non-manifold/boundary edges after weld
- warning: DRG_steamdrake_Unstaged_LOD0: 8071 UV faces (33.63%) flipped_faces > 1.0%
- warning: DRG_steamdrake_Unstaged_LOD2: 1410 UV faces (35.25%) flipped_faces > 1.0%
- warning: DRG_steamdrake_Unstaged_LOD2: 53 loose vertices
- warning: DRG_steamdrake_Unstaged_LOD2: 3236 non-manifold/boundary edges after weld
- warning: Image_0: 2048x2048 > recommended 1024
- pass: LOD0 rest p95 deviation 0.000% (limit 0.40%)
- pass: LOD0 worst deformed p95 deviation 0.000% (limit 0.60%)
- pass: LOD1 rest p95 deviation 0.041% (limit 1.00%)
- pass: LOD1 worst deformed p95 deviation 0.060% (limit 1.40%)
- pass: LOD2 rest p95 deviation 0.156% (limit 2.50%)
- pass: LOD2 worst deformed p95 deviation 0.228% (limit 3.00%)
- warning: LOD2 Base Color drift p95 11.9/255

## PBR

- Bộ texture URP: metallic_smoothness=`DRG_steamdrake_Unstaged_MetallicSmoothness.png`, base_color=`DRG_steamdrake_Unstaged_BaseColor.png`
- Base Color: image; UV chồng lấn 0.0000

## Unity

- prefab: `Assets/Dravio/Character/Prefabs/DRG_steamdrake_Unstaged_Rigged_LODGroup.prefab`
- material: `Assets/Dravio/Character/Materials/DRG_steamdrake_Unstaged.mat`
- baseController: `Assets/Dravio/Character/Animation/DragonBase_serpentine.controller`
- overrideController: `Assets/Dravio/Character/Animation/Overrides/DRG_steamdrake_Unstaged.overrideController`
