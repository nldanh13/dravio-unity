# DRG_zephyr_Unstaged

- Nguồn: `SAMPLE_04_zephyr.glb` — bằng chứng: **SAMPLE**. GLB nguồn không đổi: có
- Loài / giai đoạn: zephyr / Unstaged
- Kiểm định pipeline: PASS; Animation QA: **PASS**

## Rig

- Archetype `winged` (locomotion contains "fly"); limbCount 4 (mesh measurement accepted (--limb-count auto)); rig v7
- Chiều cao 0.9007 m (source size kept); pivot dời theo mặt đối xứng 0.0 m
- 32 bone (32 deform), tối đa 4 ảnh hưởng/đỉnh, 0 đỉnh không trọng số
- IK: 4 chuỗi chân; anchor: VFX_Mouth, VFX_TailTip, VFX_Back, VFX_Ground

Ghi chú rig:

- Wing upstroke limited to 0.0 deg (wanted 43.0): membrane would enter the body below that
- Idle blink not authored: mesh has no eyelid shape key (blink/eye_close); generated meshes rarely separate eyelids
- 112 vertices in floating fragments ignored for landmark detection
- no neck narrowing in the profile; neck base set between chest and head

## Clip

| Clip | Frame | Loop | Tốc độ (m/s) | Giãn p99.9 | Nén p0.1 | Trượt chân | Xuyên đất | Hở tiếp xúc | Khép vòng (°) | Nâng (m) | Vùng tệ nhất | Kết quả |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Idle | 120 | có | 0.000 | 1.137 | 0.884 | 0.0000 | 0.0001 | 0.0000 | 0.063 | 0.0000 | Tail1 64%, Pelvis 30% | PASS |
| Eat | 60 | — | 0.000 | 1.508 | 0.563 | 0.0000 | 0.0000 | 0.0000 | — | 0.0062 | Neck 40%, Spine 30% | PASS |
| Sleep | 120 | có | 0.000 | 1.426 | 0.637 | 0.0000 | 0.0000 | 0.0000 | 0.069 | 0.0426 | Neck 48%, Spine 18% | PASS |
| Happy | 45 | — | 0.000 | 1.393 | 0.689 | 0.0000 | 0.0005 | 0.0000 | — | 0.0000 | Tail1 57%, Pelvis 30% | PASS |
| Walk | 22 | có | 0.199 | 1.290 | 0.708 | 0.0001 | 0.0005 | 0.0004 | 0.069 | 0.0000 | Pelvis 32%, HindFoot_R 21% | PASS |
| Run | 12 | có | 0.880 | 1.509 | 0.541 | 0.0000 | 0.0000 | 0.0002 | 0.074 | 0.0098 | Pelvis 31%, HindFoot_L 28% | PASS |
| Fly | 32 | có | 0.985 | 1.326 | 0.777 | 0.0000 | — | 0.0000 | 0.056 | 0.0000 | Spine 29%, Pelvis 24% | PASS |
| Rest | 120 | có | 0.000 | 1.244 | 0.659 | 0.0000 | 0.0000 | 0.0000 | 0.069 | 0.0426 | Pelvis 34%, WingUpper_R 16% | PASS |
| Wake | 60 | — | 0.000 | 1.312 | 0.635 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Neck 24%, HindFoot_R 12% | PASS |
| LookAround | 90 | — | 0.000 | 1.282 | 0.776 | 0.0000 | 0.0001 | 0.0000 | — | 0.0000 | Neck 37%, Spine 31% | PASS |
| Curious | 75 | — | 0.000 | 1.335 | 0.782 | 0.0000 | 0.0004 | 0.0000 | — | 0.0000 | Tail1 36%, Pelvis 24% | PASS |
| Drink | 75 | — | 0.000 | 1.579 | 0.530 | 0.0000 | 0.0000 | 0.0000 | — | 0.0064 | Neck 38%, Spine 36% | PASS |
| Stretch | 90 | — | 0.000 | 1.361 | 0.770 | 0.0000 | 0.0000 | 0.0000 | — | 0.0166 | Spine 83%, Pelvis 6% | PASS |
| Bask | 120 | có | 0.000 | 1.173 | 0.659 | 0.0000 | 0.0000 | 0.0000 | 0.069 | 0.0426 | Pelvis 33%, HindLower_L 22% | PASS |
| Greet | 60 | — | 0.000 | 1.150 | 0.853 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Tail1 50%, Pelvis 27% | PASS |
| Perch | 120 | có | 0.000 | 1.385 | 0.720 | 0.0000 | 0.0000 | 0.0000 | 0.069 | 0.0097 | WingUpper_R 24%, Pelvis 22% | PASS |
| Nuzzle | 75 | — | 0.000 | 1.135 | 0.850 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Spine 52%, Neck 18% | PASS |
| TakeOff | 40 | — | 0.000 | 1.307 | 0.678 | 0.0000 | 0.0044 | 0.0009 | — | 0.0000 | Pelvis 19%, HindFoot_R 16% | PASS |
| Glide | 60 | có | 0.000 | 1.208 | 0.824 | 0.0000 | — | 0.0000 | 0.063 | 0.0000 | Pelvis 70%, Spine 14% | PASS |
| Land | 40 | — | 0.000 | 1.489 | 0.569 | 0.0000 | 0.0068 | 0.0034 | — | 0.0000 | Spine 40%, HindFoot_R 11% | PASS |

Ngưỡng (`character-budgets.json` ▸ `animationQa`, không nới): giãn ≤ 1.6, nén ≥ 0.45, trượt ≤ 0.02, xuyên ≤ 0.01, hở ≤ 0.03, khép vòng ≤ 0.5° (tương đối theo chiều cao).

## Cảnh báo QA

- không có

## LOD

| LOD | Tam giác | Đỉnh | Trong ngân sách |
|---|---|---|---|
| 0 | 24000 | 12094 | có |
| 1 | 10000 | 5094 | có |
| 2 | 4000 | 2094 | có |
- pass: LOD0 rest p95 deviation 0.000% (limit 0.40%)
- pass: LOD0 worst deformed p95 deviation 0.000% (limit 0.60%)
- pass: LOD1 rest p95 deviation 0.019% (limit 1.00%)
- pass: LOD1 worst deformed p95 deviation 0.024% (limit 1.40%)
- pass: LOD2 rest p95 deviation 0.055% (limit 2.50%)
- pass: LOD2 worst deformed p95 deviation 0.066% (limit 3.00%)

## PBR

- Bộ texture URP: metallic_smoothness=`DRG_zephyr_Unstaged_MetallicSmoothness.png`, normal=`DRG_zephyr_Unstaged_Normal.png`, base_color=`DRG_zephyr_Unstaged_BaseColor.png`
- Base Color: image; UV chồng lấn 0.0000

## Unity

- prefab: `Assets/Dravio/Character/Prefabs/DRG_zephyr_Unstaged_Rigged_LODGroup.prefab`
- material: `Assets/Dravio/Character/Materials/DRG_zephyr_Unstaged.mat`
- baseController: `Assets/Dravio/Character/Animation/DragonBase_winged.controller`
- overrideController: `Assets/Dravio/Character/Animation/Overrides/DRG_zephyr_Unstaged.overrideController`
