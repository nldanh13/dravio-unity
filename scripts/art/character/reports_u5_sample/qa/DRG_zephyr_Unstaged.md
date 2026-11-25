# DRG_zephyr_Unstaged

- Nguồn: `SAMPLE_zephyr.glb` — bằng chứng: **SAMPLE**. GLB nguồn không đổi: có
- Loài / giai đoạn: zephyr / Unstaged
- Kiểm định pipeline: PASS; Animation QA: **PASS**

## Rig

- Archetype `winged` (locomotion contains "fly"); limbCount 4 (mesh measurement accepted (--limb-count auto)); rig v3
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
| Idle | 120 | có | 0.000 | 1.137 | 0.884 | 0.0000 | 0.0001 | 0.0000 | 0.069 | 0.0000 | Tail1 64%, Pelvis 30% | PASS |
| Eat | 60 | — | 0.000 | 1.508 | 0.563 | 0.0000 | 0.0000 | 0.0000 | — | 0.0062 | Neck 40%, Spine 30% | PASS |
| Sleep | 120 | có | 0.000 | 1.426 | 0.637 | 0.0000 | 0.0000 | 0.0000 | 0.074 | 0.0426 | Neck 48%, Spine 18% | PASS |
| Happy | 45 | — | 0.000 | 1.393 | 0.689 | 0.0000 | 0.0005 | 0.0000 | — | 0.0000 | Tail1 57%, Pelvis 30% | PASS |
| Walk | 32 | có | 0.137 | 1.280 | 0.697 | 0.0000 | 0.0005 | 0.0000 | 0.069 | 0.0000 | Pelvis 30%, HindFoot_L 20% | PASS |
| Run | 20 | có | 0.528 | 1.504 | 0.531 | 0.0000 | 0.0000 | 0.0002 | 0.069 | 0.0098 | Pelvis 29%, HindFoot_L 26% | PASS |
| Fly | 32 | có | 0.000 | 1.326 | 0.777 | 0.0000 | — | 0.0000 | 0.063 | 0.0000 | Spine 29%, Pelvis 24% | PASS |
| TakeOff | 40 | — | 0.000 | 1.428 | 0.545 | 0.0000 | 0.0000 | 0.0011 | — | 0.0124 | Pelvis 25%, HindFoot_R 18% | PASS |
| Glide | 60 | có | 0.000 | 1.208 | 0.824 | 0.0000 | — | 0.0000 | 0.063 | 0.0000 | Pelvis 70%, Spine 14% | PASS |
| Land | 40 | — | 0.000 | 1.561 | 0.543 | 0.0000 | 0.0000 | 0.0000 | — | 0.0086 | Spine 42%, HindFoot_R 11% | PASS |

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
- pass: LOD2 worst deformed p95 deviation 0.065% (limit 3.00%)

## PBR

- Bộ texture URP: metallic_smoothness=`DRG_zephyr_Unstaged_MetallicSmoothness.png`, normal=`DRG_zephyr_Unstaged_Normal.png`, base_color=`DRG_zephyr_Unstaged_BaseColor.png`
- Base Color: image; UV chồng lấn 0.0000

## Unity

- prefab: `Assets/Dravio/Character/Prefabs/DRG_zephyr_Unstaged_Rigged_LODGroup.prefab`
- material: `Assets/Dravio/Character/Materials/DRG_zephyr_Unstaged.mat`
- baseController: `Assets/Dravio/Character/Animation/DragonBase_winged.controller`
- overrideController: `Assets/Dravio/Character/Animation/Overrides/DRG_zephyr_Unstaged.overrideController`
