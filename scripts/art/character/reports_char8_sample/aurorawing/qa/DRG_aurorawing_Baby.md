# DRG_aurorawing_Baby

- Nguồn: `SAMPLE_aurorawing_Baby.glb` — bằng chứng: **SAMPLE**. GLB nguồn không đổi: có
- Loài / giai đoạn: aurorawing / Baby
- Kiểm định pipeline: PASS; Animation QA: **PASS**

## Rig

- Archetype `winged` (locomotion contains "fly"); limbCount 4 (mesh measurement accepted (--limb-count auto)); rig v7
- Chiều cao 0.5501 m (source size kept); pivot dời theo mặt đối xứng 0.0 m
- 32 bone (32 deform), tối đa 4 ảnh hưởng/đỉnh, 0 đỉnh không trọng số
- IK: 4 chuỗi chân; anchor: VFX_Mouth, VFX_TailTip, VFX_Back, VFX_Ground

Ghi chú rig:

- Wing downstroke limited to 0.0 deg (wanted 22.9): membrane would enter the body below that
- Wing upstroke limited to 0.0 deg (wanted 43.0): membrane would enter the body below that
- Idle blink not authored: mesh has no eyelid shape key (blink/eye_close); generated meshes rarely separate eyelids
- 417 vertices in floating fragments ignored for landmark detection
- no neck narrowing in the profile; neck base set between chest and head

## Clip

| Clip | Frame | Loop | Tốc độ (m/s) | Giãn p99.9 | Nén p0.1 | Trượt chân | Xuyên đất | Hở tiếp xúc | Khép vòng (°) | Nâng (m) | Vùng tệ nhất | Kết quả |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Idle | 120 | có | 0.000 | 1.112 | 0.894 | 0.0000 | 0.0001 | 0.0000 | 0.040 | 0.0000 | Tail1 45%, Pelvis 33% | PASS |
| Eat | 60 | — | 0.000 | 1.486 | 0.550 | 0.0000 | 0.0000 | 0.0000 | — | 0.0044 | Spine 43%, Neck 27% | PASS |
| Sleep | 120 | có | 0.000 | 1.465 | 0.659 | 0.0000 | 0.0000 | 0.0000 | 0.040 | 0.0226 | Neck 42%, Spine 37% | PASS |
| Happy | 45 | — | 0.000 | 1.302 | 0.727 | 0.0000 | 0.0000 | 0.0000 | — | 0.0012 | Tail1 45%, Pelvis 37% | PASS |
| Walk | 17 | có | 0.145 | 1.235 | 0.772 | 0.0002 | 0.0005 | 0.0007 | 0.040 | 0.0000 | Pelvis 37%, HindFoot_R 21% | PASS |
| Run | 10 | có | 0.596 | 1.421 | 0.628 | 0.0000 | 0.0000 | 0.0000 | 0.040 | 0.0061 | Pelvis 30%, HindFoot_L 24% | PASS |
| Fly | 32 | có | 0.103 | 1.225 | 0.838 | 0.0000 | — | 0.0000 | 0.040 | 0.0000 | Pelvis 46%, WingUpper_L 13% | PASS |
| Rest | 120 | có | 0.000 | 1.221 | 0.682 | 0.0000 | 0.0000 | 0.0000 | 0.040 | 0.0226 | Pelvis 33%, WingUpper_R 14% | PASS |
| Wake | 60 | — | 0.000 | 1.315 | 0.676 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Neck 28%, Spine 13% | PASS |
| LookAround | 90 | — | 0.000 | 1.313 | 0.763 | 0.0000 | 0.0001 | 0.0000 | — | 0.0000 | Neck 47%, Spine 38% | PASS |
| Curious | 75 | — | 0.000 | 1.265 | 0.806 | 0.0000 | 0.0011 | 0.0000 | — | 0.0000 | Pelvis 30%, Tail1 24% | PASS |
| Drink | 75 | — | 0.000 | 1.555 | 0.515 | 0.0000 | 0.0000 | 0.0000 | — | 0.0045 | Spine 47%, Neck 28% | PASS |
| Stretch | 90 | — | 0.000 | 1.325 | 0.765 | 0.0000 | 0.0000 | 0.0000 | — | 0.0094 | Spine 52%, WingUpper_L 15% | PASS |
| Bask | 120 | có | 0.000 | 1.137 | 0.682 | 0.0000 | 0.0000 | 0.0000 | 0.040 | 0.0226 | Pelvis 39%, HindLower_L 13% | PASS |
| Greet | 60 | — | 0.000 | 1.120 | 0.858 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Tail1 34%, Pelvis 27% | PASS |
| Nuzzle | 75 | — | 0.000 | 1.119 | 0.830 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Spine 46%, Neck 32% | PASS |
| TakeOff | 40 | — | 0.000 | 1.241 | 0.747 | 0.0000 | 0.0060 | 0.0008 | — | 0.0000 | Pelvis 27%, HindFoot_L 17% | PASS |
| Glide | 60 | có | 0.000 | 1.171 | 0.853 | 0.0000 | — | 0.0000 | 0.056 | 0.0000 | Pelvis 60%, Spine 27% | PASS |
| Land | 40 | — | 0.000 | 1.511 | 0.652 | 0.0000 | 0.0081 | 0.0077 | — | 0.0000 | Spine 37%, HindFoot_L 12% | PASS |

Ngưỡng (`character-budgets.json` ▸ `animationQa`, không nới): giãn ≤ 1.6, nén ≥ 0.45, trượt ≤ 0.02, xuyên ≤ 0.01, hở ≤ 0.03, khép vòng ≤ 0.5° (tương đối theo chiều cao).

## Cảnh báo QA

- không có

## LOD

| LOD | Tam giác | Đỉnh | Trong ngân sách |
|---|---|---|---|
| 0 | 24000 | 12010 | có |
| 1 | 10000 | 5010 | có |
| 2 | 4000 | 2010 | có |
- pass: LOD0 rest p95 deviation 0.000% (limit 0.40%)
- pass: LOD0 worst deformed p95 deviation 0.000% (limit 0.60%)
- pass: LOD1 rest p95 deviation 0.022% (limit 1.00%)
- pass: LOD1 worst deformed p95 deviation 0.028% (limit 1.40%)
- pass: LOD2 rest p95 deviation 0.063% (limit 2.50%)
- pass: LOD2 worst deformed p95 deviation 0.075% (limit 3.00%)

## PBR

- Bộ texture URP: —
- Base Color: vertex_color; UV chồng lấn 0.0000

## Unity

- prefab: `Assets/Dravio/Character/Prefabs/DRG_aurorawing_Baby_Rigged_LODGroup.prefab`
- material: `Assets/Dravio/Character/Materials/DRG_aurorawing_Baby.mat`
- baseController: `Assets/Dravio/Character/Animation/DragonBase_winged.controller`
- overrideController: `Assets/Dravio/Character/Animation/Overrides/DRG_aurorawing_Baby.overrideController`
