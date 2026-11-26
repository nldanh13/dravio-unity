# DRG_gaia_Unstaged

- Nguồn: `16_gaia.glb` — bằng chứng: **thực tế (chưa nghiệm thu)**. GLB nguồn không đổi: có
- Loài / giai đoạn: gaia / Unstaged
- Kiểm định pipeline: PASS_WITH_WARNINGS; Animation QA: **PASS_WITH_WARNINGS**

## Rig

- Archetype `quadruped` (walks; limb count not stated (quadruped template once limbCount=4 is supplied)); limbCount 4 (mesh measurement accepted (--limb-count auto)); rig v7
- Chiều cao 1.1186 m (source size kept); pivot dời theo mặt đối xứng 0.0691 m
- 27 bone (27 deform), tối đa 4 ảnh hưởng/đỉnh, 0 đỉnh không trọng số
- IK: 4 chuỗi chân; anchor: VFX_Mouth, VFX_TailTip, VFX_Back, VFX_Ground

Ghi chú rig:

- Idle blink not authored: mesh has no eyelid shape key (blink/eye_close); generated meshes rarely separate eyelids
- 4504 vertices in floating fragments ignored for landmark detection
- no neck narrowing in the profile; neck base set between chest and head

## Clip

| Clip | Frame | Loop | Tốc độ (m/s) | Giãn p99.9 | Nén p0.1 | Trượt chân | Xuyên đất | Hở tiếp xúc | Khép vòng (°) | Nâng (m) | Vùng tệ nhất | Kết quả |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Idle | 120 | có | 0.000 | 1.223 | 0.817 | 0.0000 | 0.0000 | 0.0000 | 0.063 | 0.0049 | Pelvis 48%, HindLower_L 10% | PASS |
| Eat | 60 | — | 0.000 | 3.317 | 0.458 | 0.0000 | 0.0000 | 0.0000 | — | 0.0145 | Chest 26%, Pelvis 23% | WARNING |
| Sleep | 120 | có | 0.000 | 5.280 | 0.471 | 0.0000 | 0.0000 | 0.0000 | 0.069 | 0.0599 | Pelvis 39%, HindLower_L 11% | WARNING |
| Happy | 45 | — | 0.000 | 1.556 | 0.602 | 0.0000 | 0.0000 | 0.0000 | — | 0.0081 | Pelvis 55%, Tail1 13% | PASS |
| Walk | 22 | có | 0.165 | 2.121 | 0.562 | 0.0000 | 0.0000 | 0.0000 | 0.063 | 0.0055 | Pelvis 57%, HindLower_L 11% | WARNING |
| Run | 12 | có | 0.730 | 3.582 | 0.519 | 0.0002 | 0.0000 | 0.0010 | 0.069 | 0.0356 | Pelvis 47%, HindLower_L 11% | WARNING |
| Swim | 48 | có | 0.746 | 13.437 | 0.559 | 0.0000 | — | 0.0000 | 0.069 | 0.0000 | Pelvis 79%, ForeLower_R 9% | WARNING |
| Rest | 120 | có | 0.000 | 5.280 | 0.517 | 0.0000 | 0.0000 | 0.0000 | 0.069 | 0.0599 | Pelvis 44%, HindLower_L 18% | WARNING |
| Wake | 60 | — | 0.000 | 2.958 | 0.512 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Pelvis 44%, HindLower_L 12% | WARNING |
| LookAround | 90 | — | 0.000 | 1.823 | 0.623 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Chest 39%, Head 28% | WARNING |
| Curious | 75 | — | 0.000 | 1.295 | 0.720 | 0.0000 | 0.0201 | 0.0000 | — | 0.0025 | Pelvis 51%, HindLower_L 9% | WARNING |
| Drink | 75 | — | 0.000 | 3.635 | 0.458 | 0.0000 | 0.0000 | 0.0000 | — | 0.0145 | Chest 24%, Pelvis 20% | WARNING |
| Stretch | 90 | — | 0.000 | 2.043 | 0.610 | 0.0000 | 0.0000 | 0.0000 | — | 0.0125 | Pelvis 54%, ForeLower_R 9% | WARNING |
| Bask | 120 | có | 0.000 | 5.280 | 0.518 | 0.0000 | 0.0000 | 0.0000 | 0.069 | 0.0599 | Pelvis 44%, HindLower_L 18% | WARNING |
| Greet | 60 | — | 0.000 | 1.189 | 0.852 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Pelvis 35%, Tail1 25% | PASS |
| SwimIdle | 72 | có | 0.000 | 4.624 | 0.538 | 0.0000 | — | 0.0000 | 0.063 | 0.0000 | Pelvis 71%, ForeLower_R 8% | WARNING |
| SwimRest | 120 | có | 0.000 | 1.913 | 0.536 | 0.0000 | — | 0.0000 | 0.074 | 0.0000 | Pelvis 57%, ForeLower_R 17% | WARNING |
| SwimSurface | 48 | có | 0.746 | 9.426 | 0.554 | 0.0000 | — | 0.0000 | 0.069 | 0.0000 | Pelvis 71%, ForeLower_R 9% | WARNING |
| WaterEnter | 60 | — | 0.000 | 8.320 | 0.568 | 0.0000 | — | 0.0000 | — | 0.0000 | Pelvis 66%, ForeLower_R 10% | WARNING |
| WaterExit | 60 | — | 0.000 | 8.320 | 0.585 | 0.0000 | — | 0.0000 | — | 0.0000 | Pelvis 70%, ForeLower_R 10% | WARNING |
| Nuzzle | 75 | — | 0.000 | 1.249 | 0.858 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Chest 27%, Head 19% | PASS |

Ngưỡng (`character-budgets.json` ▸ `animationQa`, không nới): giãn ≤ 1.6, nén ≥ 0.45, trượt ≤ 0.02, xuyên ≤ 0.01, hở ≤ 0.03, khép vòng ≤ 0.5° (tương đối theo chiều cao).

## Cảnh báo QA

- model: bones — bone orientation / mirror issue ({'zeroLength': [], 'mirrorErrorRel': 0.05218, 'chainDirection': []} / {'mirrorErrorRel': 0.01})
- Eat: deformation — edge stretch over limit (3.317 / 1.6)
- Sleep: deformation — edge stretch over limit (5.28 / 1.6)
- Walk: deformation — edge stretch over limit (2.121 / 1.6)
- Run: deformation — edge stretch over limit (3.582 / 1.6)
- Swim: deformation — edge stretch over limit (13.437 / 1.6)
- Rest: deformation — edge stretch over limit (5.28 / 1.6)
- Wake: deformation — edge stretch over limit (2.958 / 1.6)
- LookAround: deformation — edge stretch over limit (1.823 / 1.6)
- Curious: ground — mesh below the ground plane (0.0201 / 0.01)
- Drink: deformation — edge stretch over limit (3.635 / 1.6)
- Stretch: deformation — edge stretch over limit (2.043 / 1.6)
- Bask: deformation — edge stretch over limit (5.28 / 1.6)
- SwimIdle: deformation — edge stretch over limit (4.624 / 1.6)
- SwimRest: deformation — edge stretch over limit (1.913 / 1.6)
- SwimSurface: deformation — edge stretch over limit (9.426 / 1.6)
- WaterEnter: deformation — edge stretch over limit (8.32 / 1.6)
- WaterExit: deformation — edge stretch over limit (8.32 / 1.6)

## LOD

| LOD | Tam giác | Đỉnh | Trong ngân sách |
|---|---|---|---|
| 0 | 24000 | 17964 | có |
| 1 | 10000 | 10505 | có |
| 2 | 4000 | 6046 | có |
- warning: DRG_gaia_Unstaged_LOD1: 3658 UV faces (36.58%) flipped_faces > 1.0%
- warning: DRG_gaia_Unstaged_LOD1: 8 loose vertices
- warning: DRG_gaia_Unstaged_LOD1: 4682 non-manifold/boundary edges after weld
- warning: DRG_gaia_Unstaged_LOD2: 1431 UV faces (35.77%) flipped_faces > 1.0%
- warning: DRG_gaia_Unstaged_LOD2: 1343 loose vertices
- warning: DRG_gaia_Unstaged_LOD2: 3810 non-manifold/boundary edges after weld
- warning: DRG_gaia_Unstaged_LOD0: 8553 UV faces (35.64%) flipped_faces > 1.0%
- warning: Image_0: 2048x2048 > recommended 1024
- pass: LOD0 rest p95 deviation 0.000% (limit 0.40%)
- pass: LOD0 worst deformed p95 deviation 0.000% (limit 0.60%)
- pass: LOD1 rest p95 deviation 0.064% (limit 1.00%)
- pass: LOD1 worst deformed p95 deviation 0.077% (limit 1.40%)
- pass: LOD2 rest p95 deviation 0.262% (limit 2.50%)
- pass: LOD2 worst deformed p95 deviation 0.304% (limit 3.00%)
- warning: LOD2 Base Color drift p95 17.3/255

## PBR

- Bộ texture URP: metallic_smoothness=`DRG_gaia_Unstaged_MetallicSmoothness.png`, base_color=`DRG_gaia_Unstaged_BaseColor.png`
- Base Color: image; UV chồng lấn 0.0000

## Unity

- prefab: `Assets/Dravio/Character/Prefabs/DRG_gaia_Unstaged_Rigged_LODGroup.prefab`
- material: `Assets/Dravio/Character/Materials/DRG_gaia_Unstaged.mat`
- baseController: `Assets/Dravio/Character/Animation/DragonBase_quadruped.controller`
- overrideController: `Assets/Dravio/Character/Animation/Overrides/DRG_gaia_Unstaged.overrideController`
