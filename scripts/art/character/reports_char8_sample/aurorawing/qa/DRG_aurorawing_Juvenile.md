# DRG_aurorawing_Juvenile

- Nguồn: `SAMPLE_aurorawing_Juvenile.glb` — bằng chứng: **SAMPLE**. GLB nguồn không đổi: có
- Loài / giai đoạn: aurorawing / Juvenile
- Kiểm định pipeline: PASS; Animation QA: **PASS**

## Rig

- Archetype `winged` (locomotion contains "fly"); limbCount 4 (mesh measurement accepted (--limb-count auto)); rig v7
- Chiều cao 0.85 m (source size kept); pivot dời theo mặt đối xứng 0.0 m
- 32 bone (32 deform), tối đa 4 ảnh hưởng/đỉnh, 0 đỉnh không trọng số
- IK: 4 chuỗi chân; anchor: VFX_Mouth, VFX_TailTip, VFX_Back, VFX_Ground

Ghi chú rig:

- Wing downstroke limited to 1.3 deg (wanted 22.9): membrane would enter the body below that
- Wing upstroke limited to 17.5 deg (wanted 43.0): membrane would enter the body below that
- Idle blink not authored: mesh has no eyelid shape key (blink/eye_close); generated meshes rarely separate eyelids
- no neck narrowing in the profile; neck base set between chest and head

## Clip

| Clip | Frame | Loop | Tốc độ (m/s) | Giãn p99.9 | Nén p0.1 | Trượt chân | Xuyên đất | Hở tiếp xúc | Khép vòng (°) | Nâng (m) | Vùng tệ nhất | Kết quả |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Idle | 120 | có | 0.000 | 1.100 | 0.907 | 0.0000 | 0.0001 | 0.0000 | 0.040 | 0.0000 | Tail1 49%, Pelvis 40% | PASS |
| Eat | 60 | — | 0.000 | 1.419 | 0.599 | 0.0000 | 0.0000 | 0.0000 | — | 0.0077 | Neck 33%, Head 16% | PASS |
| Sleep | 120 | có | 0.000 | 1.411 | 0.674 | 0.0000 | 0.0000 | 0.0000 | 0.028 | 0.0358 | Neck 42%, Head 20% | PASS |
| Happy | 45 | — | 0.000 | 1.272 | 0.743 | 0.0000 | 0.0000 | 0.0000 | — | 0.0024 | Tail1 43%, Pelvis 40% | PASS |
| Walk | 21 | có | 0.180 | 1.239 | 0.764 | 0.0000 | 0.0005 | 0.0001 | 0.040 | 0.0000 | Pelvis 42%, HindFoot_R 17% | PASS |
| Run | 12 | có | 0.761 | 1.457 | 0.632 | 0.0000 | 0.0000 | 0.0000 | 0.040 | 0.0104 | Pelvis 38%, HindFoot_L 24% | PASS |
| Fly | 32 | có | 0.871 | 1.297 | 0.776 | 0.0000 | — | 0.0000 | 0.028 | 0.0000 | WingUpper_L 28%, Pelvis 22% | PASS |
| Rest | 120 | có | 0.000 | 1.259 | 0.693 | 0.0000 | 0.0000 | 0.0000 | 0.028 | 0.0358 | Pelvis 29%, WingFore_R 20% | PASS |
| Wake | 60 | — | 0.000 | 1.286 | 0.686 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Neck 22%, HindFoot_R 11% | PASS |
| LookAround | 90 | — | 0.000 | 1.280 | 0.776 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Neck 50%, Spine 23% | PASS |
| Curious | 75 | — | 0.000 | 1.229 | 0.818 | 0.0000 | 0.0000 | 0.0000 | — | 0.0019 | Pelvis 35%, Tail1 18% | PASS |
| Drink | 75 | — | 0.000 | 1.485 | 0.567 | 0.0000 | 0.0000 | 0.0000 | — | 0.0079 | Neck 32%, Head 16% | PASS |
| Stretch | 90 | — | 0.000 | 1.246 | 0.764 | 0.0000 | 0.0000 | 0.0000 | — | 0.0161 | Spine 26%, Pelvis 22% | PASS |
| Bask | 120 | có | 0.000 | 1.165 | 0.678 | 0.0000 | 0.0000 | 0.0000 | 0.028 | 0.0358 | WingUpper_L 22%, Spine 18% | PASS |
| Greet | 60 | — | 0.000 | 1.103 | 0.891 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Tail1 46%, Pelvis 35% | PASS |
| Nuzzle | 75 | — | 0.000 | 1.095 | 0.883 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Neck 36%, Spine 29% | PASS |
| TakeOff | 40 | — | 0.000 | 1.294 | 0.708 | 0.0000 | 0.0068 | 0.0007 | — | 0.0000 | Pelvis 23%, WingUpper_L 14% | PASS |
| Glide | 60 | có | 0.000 | 1.142 | 0.840 | 0.0000 | — | 0.0000 | 0.040 | 0.0000 | Pelvis 76%, WingUpper_L 12% | PASS |
| Land | 40 | — | 0.000 | 1.359 | 0.584 | 0.0000 | 0.0090 | 0.0000 | — | 0.0000 | WingUpper_L 22%, Pelvis 19% | PASS |

Ngưỡng (`character-budgets.json` ▸ `animationQa`, không nới): giãn ≤ 1.6, nén ≥ 0.45, trượt ≤ 0.02, xuyên ≤ 0.01, hở ≤ 0.03, khép vòng ≤ 0.5° (tương đối theo chiều cao).

## Cảnh báo QA

- không có

## LOD

| LOD | Tam giác | Đỉnh | Trong ngân sách |
|---|---|---|---|
| 0 | 24000 | 12014 | có |
| 1 | 10000 | 5014 | có |
| 2 | 4000 | 2014 | có |
- pass: LOD0 rest p95 deviation 0.000% (limit 0.40%)
- pass: LOD0 worst deformed p95 deviation 0.000% (limit 0.60%)
- pass: LOD1 rest p95 deviation 0.023% (limit 1.00%)
- pass: LOD1 worst deformed p95 deviation 0.029% (limit 1.40%)
- pass: LOD2 rest p95 deviation 0.063% (limit 2.50%)
- pass: LOD2 worst deformed p95 deviation 0.073% (limit 3.00%)

## PBR

- Bộ texture URP: —
- Base Color: vertex_color; UV chồng lấn 0.0004

## Unity

- prefab: `Assets/Dravio/Character/Prefabs/DRG_aurorawing_Juvenile_Rigged_LODGroup.prefab`
- material: `Assets/Dravio/Character/Materials/DRG_aurorawing_Juvenile.mat`
- baseController: `Assets/Dravio/Character/Animation/DragonBase_winged.controller`
- overrideController: `Assets/Dravio/Character/Animation/Overrides/DRG_aurorawing_Juvenile.overrideController`
