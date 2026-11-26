# DRG_droplet_Unstaged

- Nguồn: `02_droplet.glb` — bằng chứng: **thực tế (chưa nghiệm thu)**. GLB nguồn không đổi: có
- Loài / giai đoạn: droplet / Unstaged
- Kiểm định pipeline: PASS_WITH_WARNINGS; Animation QA: **PASS_WITH_WARNINGS**

## Rig

- Archetype `quadruped` (walks; limb count not stated (quadruped template once limbCount=4 is supplied)); limbCount 4 (mesh measurement accepted (--limb-count auto)); rig v7
- Chiều cao 1.0282 m (source size kept); pivot dời theo mặt đối xứng 0.0 m
- 28 bone (28 deform), tối đa 4 ảnh hưởng/đỉnh, 0 đỉnh không trọng số
- IK: 4 chuỗi chân; anchor: VFX_Mouth, VFX_TailTip, VFX_Back, VFX_Ground

Ghi chú rig:

- Idle blink not authored: mesh has no eyelid shape key (blink/eye_close); generated meshes rarely separate eyelids
- 2196 vertices in floating fragments ignored for landmark detection
- contact cluster at y=-0.625 split at x=-0.000 (empty band 0.095 m): left/right paws standing close together
- no neck narrowing in the profile; neck base set between chest and head

## Clip

| Clip | Frame | Loop | Tốc độ (m/s) | Giãn p99.9 | Nén p0.1 | Trượt chân | Xuyên đất | Hở tiếp xúc | Khép vòng (°) | Nâng (m) | Vùng tệ nhất | Kết quả |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Idle | 120 | có | 0.000 | 1.192 | 0.869 | 0.0000 | 0.0000 | 0.0000 | 0.040 | 0.0030 | Pelvis 28%, HindUpper_L 16% | PASS |
| Eat | 60 | — | 0.000 | 2.543 | 0.418 | 0.0000 | 0.0000 | 0.0000 | — | 0.0113 | Spine 30%, Pelvis 21% | WARNING |
| Sleep | 120 | có | 0.000 | 3.957 | 0.395 | 0.0000 | 0.0000 | 0.0000 | 0.048 | 0.0411 | HindUpper_L 17%, Tail1 17% | WARNING |
| Happy | 45 | — | 0.000 | 1.563 | 0.644 | 0.0000 | 0.0000 | 0.0000 | — | 0.0058 | Pelvis 29%, Tail1 16% | PASS |
| Walk | 25 | có | 0.228 | 2.731 | 0.543 | 0.0002 | 0.0000 | 0.0006 | 0.028 | 0.0084 | Pelvis 32%, HindUpper_L 12% | WARNING |
| Run | 14 | có | 0.982 | 4.401 | 0.454 | 0.0000 | 0.0000 | 0.0000 | 0.040 | 0.0150 | Pelvis 36%, HindUpper_L 10% | WARNING |
| Swim | 48 | có | 0.746 | 3.470 | 0.580 | 0.0000 | — | 0.0000 | 0.040 | 0.0000 | Pelvis 28%, Tail1 12% | WARNING |
| Rest | 120 | có | 0.000 | 3.980 | 0.407 | 0.0000 | 0.0000 | 0.0000 | 0.040 | 0.0417 | HindLower_L 19%, HindLower_R 18% | WARNING |
| Wake | 60 | — | 0.000 | 2.817 | 0.426 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Pelvis 22%, HindUpper_L 18% | WARNING |
| LookAround | 90 | — | 0.000 | 1.471 | 0.716 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Head 41%, Fin_L 17% | PASS |
| Curious | 75 | — | 0.000 | 1.243 | 0.747 | 0.0000 | 0.0014 | 0.0000 | — | 0.0000 | Pelvis 29%, Spine 27% | PASS |
| Drink | 75 | — | 0.000 | 2.834 | 0.403 | 0.0000 | 0.0000 | 0.0000 | — | 0.0115 | Spine 33%, Pelvis 21% | WARNING |
| Stretch | 90 | — | 0.000 | 2.138 | 0.560 | 0.0000 | 0.0000 | 0.0000 | — | 0.0156 | Spine 29%, Pelvis 24% | WARNING |
| Bask | 120 | có | 0.000 | 3.980 | 0.407 | 0.0000 | 0.0000 | 0.0000 | 0.040 | 0.0417 | HindLower_L 19%, HindLower_R 18% | WARNING |
| Greet | 60 | — | 0.000 | 1.132 | 0.902 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Pelvis 33%, Tail1 28% | PASS |
| SwimIdle | 72 | có | 0.000 | 1.676 | 0.716 | 0.0000 | — | 0.0000 | 0.040 | 0.0000 | Pelvis 30%, HindUpper_R 14% | WARNING |
| SwimRest | 120 | có | 0.000 | 1.259 | 0.826 | 0.0000 | — | 0.0000 | 0.040 | 0.0000 | Pelvis 30%, HindUpper_R 13% | PASS |
| SwimSurface | 48 | có | 0.746 | 2.606 | 0.618 | 0.0000 | — | 0.0000 | 0.040 | 0.0000 | Pelvis 27%, HindUpper_L 14% | WARNING |
| WaterEnter | 60 | — | 0.000 | 2.151 | 0.801 | 0.0000 | — | 0.0000 | — | 0.0000 | Pelvis 40%, HindUpper_R 19% | WARNING |
| WaterExit | 60 | — | 0.000 | 2.151 | 0.812 | 0.0000 | — | 0.0000 | — | 0.0000 | Pelvis 42%, HindUpper_R 20% | WARNING |
| Nuzzle | 75 | — | 0.000 | 1.161 | 0.881 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Spine 43%, ForeUpper_R 9% | PASS |

Ngưỡng (`character-budgets.json` ▸ `animationQa`, không nới): giãn ≤ 1.6, nén ≥ 0.45, trượt ≤ 0.02, xuyên ≤ 0.01, hở ≤ 0.03, khép vòng ≤ 0.5° (tương đối theo chiều cao).

## Cảnh báo QA

- Eat: deformation — edge stretch over limit (2.543 / 1.6)
- Eat: deformation — edge compression over limit (0.418 / 0.45)
- Sleep: deformation — edge stretch over limit (3.957 / 1.6)
- Sleep: deformation — edge compression over limit (0.395 / 0.45)
- Walk: deformation — edge stretch over limit (2.731 / 1.6)
- Run: deformation — edge stretch over limit (4.401 / 1.6)
- Swim: deformation — edge stretch over limit (3.47 / 1.6)
- Rest: deformation — edge stretch over limit (3.98 / 1.6)
- Rest: deformation — edge compression over limit (0.407 / 0.45)
- Wake: deformation — edge stretch over limit (2.817 / 1.6)
- Wake: deformation — edge compression over limit (0.426 / 0.45)
- Drink: deformation — edge stretch over limit (2.834 / 1.6)
- Drink: deformation — edge compression over limit (0.403 / 0.45)
- Stretch: deformation — edge stretch over limit (2.138 / 1.6)
- Bask: deformation — edge stretch over limit (3.98 / 1.6)
- Bask: deformation — edge compression over limit (0.407 / 0.45)
- SwimIdle: deformation — edge stretch over limit (1.676 / 1.6)
- SwimSurface: deformation — edge stretch over limit (2.606 / 1.6)
- WaterEnter: deformation — edge stretch over limit (2.151 / 1.6)
- WaterExit: deformation — edge stretch over limit (2.151 / 1.6)

## LOD

| LOD | Tam giác | Đỉnh | Trong ngân sách |
|---|---|---|---|
| 0 | 24000 | 15822 | có |
| 1 | 10000 | 8586 | có |
| 2 | 4000 | 4809 | có |
- warning: DRG_droplet_Unstaged_LOD1: 3884 UV faces (38.84%) flipped_faces > 1.0%
- warning: DRG_droplet_Unstaged_LOD1: 4 loose vertices
- warning: DRG_droplet_Unstaged_LOD1: 3042 non-manifold/boundary edges after weld
- warning: DRG_droplet_Unstaged_LOD2: 1480 UV faces (37.00%) flipped_faces > 1.0%
- warning: DRG_droplet_Unstaged_LOD2: 250 loose vertices
- warning: DRG_droplet_Unstaged_LOD2: 3388 non-manifold/boundary edges after weld
- warning: DRG_droplet_Unstaged_LOD0: 9455 UV faces (39.40%) flipped_faces > 1.0%
- warning: Image_0: 2048x2048 > recommended 1024
- pass: LOD0 rest p95 deviation 0.000% (limit 0.40%)
- pass: LOD0 worst deformed p95 deviation 0.000% (limit 0.60%)
- pass: LOD1 rest p95 deviation 0.038% (limit 1.00%)
- pass: LOD1 worst deformed p95 deviation 0.054% (limit 1.40%)
- pass: LOD2 rest p95 deviation 0.167% (limit 2.50%)
- pass: LOD2 worst deformed p95 deviation 0.230% (limit 3.00%)
- warning: LOD2 Base Color drift p95 15.4/255

## PBR

- Bộ texture URP: metallic_smoothness=`DRG_droplet_Unstaged_MetallicSmoothness.png`, base_color=`DRG_droplet_Unstaged_BaseColor.png`
- Base Color: image; UV chồng lấn 0.0000

## Unity

- prefab: `Assets/Dravio/Character/Prefabs/DRG_droplet_Unstaged_Rigged_LODGroup.prefab`
- material: `Assets/Dravio/Character/Materials/DRG_droplet_Unstaged.mat`
- baseController: `Assets/Dravio/Character/Animation/DragonBase_quadruped.controller`
- overrideController: `Assets/Dravio/Character/Animation/Overrides/DRG_droplet_Unstaged.overrideController`
