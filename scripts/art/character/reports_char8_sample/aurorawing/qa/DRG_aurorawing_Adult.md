# DRG_aurorawing_Adult

- Nguồn: `SAMPLE_aurorawing_Adult.glb` — bằng chứng: **SAMPLE**. GLB nguồn không đổi: có
- Loài / giai đoạn: aurorawing / Adult
- Kiểm định pipeline: PASS_WITH_WARNINGS; Animation QA: **PASS**

## Rig

- Archetype `winged` (locomotion contains "fly"); limbCount 4 (mesh measurement accepted (--limb-count auto)); rig v7
- Chiều cao 1.2006 m (source size kept); pivot dời theo mặt đối xứng 0.0 m
- 32 bone (32 deform), tối đa 4 ảnh hưởng/đỉnh, 0 đỉnh không trọng số
- IK: 4 chuỗi chân; anchor: VFX_Mouth, VFX_TailTip, VFX_Back, VFX_Ground

Ghi chú rig:

- Wing downstroke limited to 7.9 deg (wanted 22.9): membrane would enter the body below that
- Wing upstroke limited to 0.0 deg (wanted 43.0): membrane would enter the body below that
- Idle blink not authored: mesh has no eyelid shape key (blink/eye_close); generated meshes rarely separate eyelids
- no neck narrowing in the profile; neck base set between chest and head

## Clip

| Clip | Frame | Loop | Tốc độ (m/s) | Giãn p99.9 | Nén p0.1 | Trượt chân | Xuyên đất | Hở tiếp xúc | Khép vòng (°) | Nâng (m) | Vùng tệ nhất | Kết quả |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Idle | 120 | có | 0.000 | 1.106 | 0.911 | 0.0000 | 0.0001 | 0.0000 | 0.069 | 0.0000 | Pelvis 55%, Tail1 37% | PASS |
| Eat | 60 | — | 0.000 | 1.448 | 0.598 | 0.0000 | 0.0000 | 0.0000 | — | 0.0113 | Neck 20%, Head 19% | PASS |
| Sleep | 120 | có | 0.000 | 1.382 | 0.660 | 0.0000 | 0.0000 | 0.0000 | 0.069 | 0.0511 | Tail3 22%, Neck 20% | PASS |
| Happy | 45 | — | 0.000 | 1.312 | 0.749 | 0.0000 | 0.0000 | 0.0000 | — | 0.0036 | Pelvis 49%, Tail1 34% | PASS |
| Walk | 25 | có | 0.218 | 1.248 | 0.751 | 0.0001 | 0.0007 | 0.0004 | 0.074 | 0.0000 | Pelvis 44%, HindFoot_R 16% | PASS |
| Run | 14 | có | 0.940 | 1.474 | 0.623 | 0.0001 | 0.0000 | 0.0005 | 0.063 | 0.0155 | Pelvis 40%, HindFoot_L 22% | PASS |
| Fly | 32 | có | 0.659 | 1.308 | 0.830 | 0.0000 | — | 0.0000 | 0.069 | 0.0000 | Pelvis 53%, WingUpper_L 13% | PASS |
| Rest | 120 | có | 0.000 | 1.278 | 0.714 | 0.0000 | 0.0000 | 0.0000 | 0.069 | 0.0511 | Pelvis 29%, WingUpper_R 22% | PASS |
| Wake | 60 | — | 0.000 | 1.295 | 0.681 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Tail3 23%, HindFoot_L 9% | PASS |
| LookAround | 90 | — | 0.000 | 1.275 | 0.794 | 0.0000 | 0.0001 | 0.0000 | — | 0.0000 | Chest 34%, Neck 19% | PASS |
| Curious | 75 | — | 0.000 | 1.216 | 0.811 | 0.0000 | 0.0000 | 0.0000 | — | 0.0028 | Pelvis 38%, Tail1 13% | PASS |
| Drink | 75 | — | 0.000 | 1.517 | 0.573 | 0.0000 | 0.0000 | 0.0000 | — | 0.0116 | Head 19%, Neck 17% | PASS |
| Stretch | 90 | — | 0.000 | 1.282 | 0.763 | 0.0000 | 0.0000 | 0.0000 | — | 0.0238 | Spine 46%, Pelvis 18% | PASS |
| Bask | 120 | có | 0.000 | 1.139 | 0.712 | 0.0000 | 0.0000 | 0.0000 | 0.069 | 0.0511 | Pelvis 30%, HindLower_L 20% | PASS |
| Greet | 60 | — | 0.000 | 1.111 | 0.895 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Pelvis 45%, Tail1 34% | PASS |
| Nuzzle | 75 | — | 0.000 | 1.108 | 0.892 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Chest 37%, Spine 18% | PASS |
| TakeOff | 40 | — | 0.000 | 1.284 | 0.733 | 0.0000 | 0.0072 | 0.0008 | — | 0.0000 | Pelvis 36%, HindFoot_R 14% | PASS |
| Glide | 60 | có | 0.000 | 1.193 | 0.830 | 0.0000 | — | 0.0000 | 0.069 | 0.0000 | Pelvis 79%, WingUpper_L 7% | PASS |
| Land | 40 | — | 0.000 | 1.352 | 0.571 | 0.0000 | 0.0095 | 0.0000 | — | 0.0000 | Pelvis 21%, WingUpper_R 17% | PASS |

Ngưỡng (`character-budgets.json` ▸ `animationQa`, không nới): giãn ≤ 1.6, nén ≥ 0.45, trượt ≤ 0.02, xuyên ≤ 0.01, hở ≤ 0.03, khép vòng ≤ 0.5° (tương đối theo chiều cao).

## Cảnh báo QA

- không có

## LOD

| LOD | Tam giác | Đỉnh | Trong ngân sách |
|---|---|---|---|
| 0 | 24000 | 12014 | có |
| 1 | 10000 | 5014 | có |
| 2 | 4000 | 2014 | có |
- warning: DRG_aurorawing_Adult_LOD2: 43 UV faces (1.07%) flipped_faces > 1.0%
- pass: LOD0 rest p95 deviation 0.000% (limit 0.40%)
- pass: LOD0 worst deformed p95 deviation 0.000% (limit 0.60%)
- pass: LOD1 rest p95 deviation 0.025% (limit 1.00%)
- pass: LOD1 worst deformed p95 deviation 0.031% (limit 1.40%)
- pass: LOD2 rest p95 deviation 0.063% (limit 2.50%)
- pass: LOD2 worst deformed p95 deviation 0.075% (limit 3.00%)

## PBR

- Bộ texture URP: —
- Base Color: vertex_color; UV chồng lấn 0.0004

## Unity

- prefab: `Assets/Dravio/Character/Prefabs/DRG_aurorawing_Adult_Rigged_LODGroup.prefab`
- material: `Assets/Dravio/Character/Materials/DRG_aurorawing_Adult.mat`
- baseController: `Assets/Dravio/Character/Animation/DragonBase_winged.controller`
- overrideController: `Assets/Dravio/Character/Animation/Overrides/DRG_aurorawing_Adult.overrideController`
