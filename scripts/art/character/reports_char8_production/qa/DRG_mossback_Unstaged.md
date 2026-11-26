# DRG_mossback_Unstaged

- Nguồn: `09_mossback.glb` — bằng chứng: **thực tế (chưa nghiệm thu)**. GLB nguồn không đổi: có
- Loài / giai đoạn: mossback / Unstaged
- Kiểm định pipeline: PASS_WITH_WARNINGS; Animation QA: **PASS_WITH_WARNINGS**

## Rig

- Archetype `quadruped` (walks; limb count not stated (quadruped template once limbCount=4 is supplied)); limbCount 4 (mesh measurement accepted (--limb-count auto)); rig v7
- Chiều cao 1.121 m (source size kept); pivot dời theo mặt đối xứng 0.0 m
- 26 bone (26 deform), tối đa 4 ảnh hưởng/đỉnh, 0 đỉnh không trọng số
- IK: 4 chuỗi chân; anchor: VFX_Mouth, VFX_TailTip, VFX_Back, VFX_Ground

Ghi chú rig:

- Idle blink not authored: mesh has no eyelid shape key (blink/eye_close); generated meshes rarely separate eyelids
- 1727 vertices in floating fragments ignored for landmark detection
- no neck narrowing in the profile; neck base set between chest and head

## Clip

| Clip | Frame | Loop | Tốc độ (m/s) | Giãn p99.9 | Nén p0.1 | Trượt chân | Xuyên đất | Hở tiếp xúc | Khép vòng (°) | Nâng (m) | Vùng tệ nhất | Kết quả |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Idle | 120 | có | 0.000 | 1.205 | 0.840 | 0.0000 | 0.0000 | 0.0000 | 0.056 | 0.0029 | Pelvis 34%, Tail1 20% | PASS |
| Eat | 60 | — | 0.000 | 3.468 | 0.423 | 0.0000 | 0.0000 | 0.0000 | — | 0.0220 | Neck 36%, Spine 20% | WARNING |
| Sleep | 120 | có | 0.000 | 4.453 | 0.478 | 0.0000 | 0.0000 | 0.0000 | 0.048 | 0.0737 | Neck 30%, Pelvis 15% | WARNING |
| Happy | 45 | — | 0.000 | 1.517 | 0.602 | 0.0000 | 0.0000 | 0.0000 | — | 0.0128 | Pelvis 36%, Tail1 22% | PASS |
| Walk | 27 | có | 0.259 | 2.865 | 0.608 | 0.0008 | 0.0000 | 0.0027 | 0.056 | 0.0117 | Pelvis 35%, HindLower_R 20% | WARNING |
| Run | 15 | có | 1.127 | 4.512 | 0.516 | 0.0000 | 0.0000 | 0.0002 | 0.056 | 0.0272 | Pelvis 41%, HindLower_L 14% | WARNING |
| Swim | 48 | có | 0.745 | 10.983 | 0.574 | 0.0000 | — | 0.0000 | 0.048 | 0.0000 | Pelvis 66%, ForeLower_R 10% | WARNING |
| Rest | 120 | có | 0.000 | 3.936 | 0.538 | 0.0000 | 0.0000 | 0.0000 | 0.048 | 0.0747 | HindLower_L 27%, HindLower_R 27% | WARNING |
| Wake | 60 | — | 0.000 | 2.937 | 0.499 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Pelvis 24%, HindLower_R 21% | WARNING |
| LookAround | 90 | — | 0.000 | 1.443 | 0.683 | 0.0000 | 0.0012 | 0.0000 | — | 0.0000 | Neck 50%, Chest 34% | PASS |
| Curious | 75 | — | 0.000 | 1.454 | 0.641 | 0.0000 | 0.0000 | 0.0000 | — | 0.0110 | Pelvis 33%, HindLower_L 15% | PASS |
| Drink | 75 | — | 0.000 | 3.967 | 0.410 | 0.0000 | 0.0000 | 0.0000 | — | 0.0222 | Neck 37%, Spine 22% | WARNING |
| Stretch | 90 | — | 0.000 | 2.834 | 0.533 | 0.0000 | 0.0000 | 0.0000 | — | 0.0339 | Pelvis 34%, HindLower_L 16% | WARNING |
| Bask | 120 | có | 0.000 | 3.833 | 0.537 | 0.0000 | 0.0000 | 0.0000 | 0.048 | 0.0747 | HindLower_R 27%, HindLower_L 26% | WARNING |
| Greet | 60 | — | 0.000 | 1.218 | 0.830 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Spine 26%, Neck 22% | PASS |
| SwimIdle | 72 | có | 0.000 | 3.789 | 0.669 | 0.0000 | — | 0.0000 | 0.056 | 0.0000 | Pelvis 63%, ForeLower_R 13% | WARNING |
| SwimRest | 120 | có | 0.000 | 1.895 | 0.685 | 0.0000 | — | 0.0000 | 0.056 | 0.0000 | Pelvis 46%, ForeLower_R 16% | WARNING |
| SwimSurface | 48 | có | 0.745 | 7.798 | 0.596 | 0.0000 | — | 0.0000 | 0.056 | 0.0000 | Pelvis 61%, ForeLower_L 10% | WARNING |
| WaterEnter | 60 | — | 0.000 | 8.212 | 0.640 | 0.0000 | — | 0.0000 | — | 0.0000 | Pelvis 60%, ForeLower_R 11% | WARNING |
| WaterExit | 60 | — | 0.000 | 8.228 | 0.652 | 0.0000 | — | 0.0000 | — | 0.0000 | Pelvis 64%, ForeLower_R 11% | WARNING |
| Nuzzle | 75 | — | 0.000 | 1.257 | 0.820 | 0.0000 | 0.0013 | 0.0000 | — | 0.0000 | Spine 42%, Neck 37% | PASS |

Ngưỡng (`character-budgets.json` ▸ `animationQa`, không nới): giãn ≤ 1.6, nén ≥ 0.45, trượt ≤ 0.02, xuyên ≤ 0.01, hở ≤ 0.03, khép vòng ≤ 0.5° (tương đối theo chiều cao).

## Cảnh báo QA

- Eat: deformation — edge stretch over limit (3.468 / 1.6)
- Eat: deformation — edge compression over limit (0.423 / 0.45)
- Sleep: deformation — edge stretch over limit (4.453 / 1.6)
- Walk: deformation — edge stretch over limit (2.865 / 1.6)
- Run: deformation — edge stretch over limit (4.512 / 1.6)
- Swim: deformation — edge stretch over limit (10.983 / 1.6)
- Rest: deformation — edge stretch over limit (3.936 / 1.6)
- Wake: deformation — edge stretch over limit (2.937 / 1.6)
- Drink: deformation — edge stretch over limit (3.967 / 1.6)
- Drink: deformation — edge compression over limit (0.41 / 0.45)
- Stretch: deformation — edge stretch over limit (2.834 / 1.6)
- Bask: deformation — edge stretch over limit (3.833 / 1.6)
- SwimIdle: deformation — edge stretch over limit (3.789 / 1.6)
- SwimRest: deformation — edge stretch over limit (1.895 / 1.6)
- SwimSurface: deformation — edge stretch over limit (7.798 / 1.6)
- WaterEnter: deformation — edge stretch over limit (8.212 / 1.6)
- WaterExit: deformation — edge stretch over limit (8.228 / 1.6)

## LOD

| LOD | Tam giác | Đỉnh | Trong ngân sách |
|---|---|---|---|
| 0 | 24000 | 15235 | có |
| 1 | 10000 | 8025 | có |
| 2 | 4000 | 4401 | có |
- warning: DRG_mossback_Unstaged_LOD1: 4395 UV faces (43.95%) flipped_faces > 1.0%
- warning: DRG_mossback_Unstaged_LOD1: 1 loose vertices
- warning: DRG_mossback_Unstaged_LOD1: 2610 non-manifold/boundary edges after weld
- warning: DRG_mossback_Unstaged_LOD2: 1739 UV faces (43.48%) flipped_faces > 1.0%
- warning: DRG_mossback_Unstaged_LOD2: 82 loose vertices
- warning: DRG_mossback_Unstaged_LOD2: 3166 non-manifold/boundary edges after weld
- warning: DRG_mossback_Unstaged_LOD0: 10557 UV faces (43.99%) flipped_faces > 1.0%
- warning: Image_0: 2048x2048 > recommended 1024
- pass: LOD0 rest p95 deviation 0.000% (limit 0.40%)
- pass: LOD0 worst deformed p95 deviation 0.000% (limit 0.60%)
- pass: LOD1 rest p95 deviation 0.048% (limit 1.00%)
- pass: LOD1 worst deformed p95 deviation 0.065% (limit 1.40%)
- pass: LOD2 rest p95 deviation 0.178% (limit 2.50%)
- pass: LOD2 worst deformed p95 deviation 0.247% (limit 3.00%)

## PBR

- Bộ texture URP: metallic_smoothness=`DRG_mossback_Unstaged_MetallicSmoothness.png`, base_color=`DRG_mossback_Unstaged_BaseColor.png`
- Base Color: image; UV chồng lấn 0.0000

## Unity

- prefab: `Assets/Dravio/Character/Prefabs/DRG_mossback_Unstaged_Rigged_LODGroup.prefab`
- material: `Assets/Dravio/Character/Materials/DRG_mossback_Unstaged.mat`
- baseController: `Assets/Dravio/Character/Animation/DragonBase_quadruped.controller`
- overrideController: `Assets/Dravio/Character/Animation/Overrides/DRG_mossback_Unstaged.overrideController`
