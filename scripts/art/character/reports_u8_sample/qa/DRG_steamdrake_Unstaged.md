# DRG_steamdrake_Unstaged

- Nguồn: `SAMPLE_05_steamdrake.glb` — bằng chứng: **SAMPLE**. GLB nguồn không đổi: có
- Loài / giai đoạn: steamdrake / Unstaged
- Kiểm định pipeline: PASS; Animation QA: **PASS**

## Rig

- Archetype `serpentine` (bodyStyle == Serpent); limbCount 4 (mesh measurement accepted (--limb-count auto)); rig v7
- Chiều cao 0.4179 m (source size kept); pivot dời theo mặt đối xứng -0.0168 m
- 31 bone (31 deform), tối đa 4 ảnh hưởng/đỉnh, 0 đỉnh không trọng số
- IK: 4 chuỗi chân; anchor: VFX_Mouth, VFX_TailTip, VFX_Back, VFX_Ground

Ghi chú rig:

- Idle blink not authored: mesh has no eyelid shape key (blink/eye_close); generated meshes rarely separate eyelids
- 159 vertices in floating fragments ignored for landmark detection
- no neck narrowing in the profile; neck base set between chest and head

## Clip

| Clip | Frame | Loop | Tốc độ (m/s) | Giãn p99.9 | Nén p0.1 | Trượt chân | Xuyên đất | Hở tiếp xúc | Khép vòng (°) | Nâng (m) | Vùng tệ nhất | Kết quả |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Idle | 120 | có | 0.000 | 1.106 | 0.903 | 0.0000 | 0.0000 | 0.0000 | 0.079 | 0.0000 | Spine8 30%, Tail1 24% | PASS |
| Eat | 60 | — | 0.000 | 1.196 | 0.798 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Spine1 45%, Neck 43% | PASS |
| Sleep | 120 | có | 0.000 | 1.467 | 0.615 | 0.0000 | 0.0000 | 0.0000 | 0.063 | 0.0218 | Spine1 35%, Spine5 29% | PASS |
| Happy | 45 | — | 0.000 | 1.308 | 0.756 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Spine8 32%, Tail1 27% | PASS |
| Walk | 40 | có | 0.062 | 1.312 | 0.740 | 0.0063 | 0.0000 | 0.0099 | 0.069 | 0.0043 | HindFoot_L 19%, Spine1 16% | PASS |
| Run | 26 | có | 0.189 | 1.415 | 0.620 | 0.0072 | 0.0000 | 0.0124 | 0.069 | 0.0046 | Spine1 16%, Spine5 15% | PASS |
| Swim | 48 | có | 0.851 | 1.438 | 0.805 | 0.0000 | — | 0.0000 | 0.063 | 0.0000 | Spine5 39%, Spine1 31% | PASS |
| Rest | 120 | có | 0.000 | 1.460 | 0.589 | 0.0000 | 0.0000 | 0.0000 | 0.056 | 0.0235 | Spine1 41%, Spine5 27% | PASS |
| Wake | 60 | — | 0.000 | 1.315 | 0.677 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Spine1 37%, Spine5 26% | PASS |
| LookAround | 90 | — | 0.000 | 1.266 | 0.781 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Spine1 54%, Neck 30% | PASS |
| Curious | 75 | — | 0.000 | 1.135 | 0.848 | 0.0000 | 0.0002 | 0.0000 | — | 0.0000 | Spine8 20%, Tail1 20% | PASS |
| Drink | 75 | — | 0.000 | 1.234 | 0.757 | 0.0000 | 0.0002 | 0.0000 | — | 0.0000 | Spine1 47%, Neck 32% | PASS |
| Stretch | 90 | — | 0.000 | 1.178 | 0.839 | 0.0000 | 0.0000 | 0.0000 | — | 0.0031 | Spine1 52%, Spine5 15% | PASS |
| Bask | 120 | có | 0.000 | 1.451 | 0.614 | 0.0000 | 0.0000 | 0.0000 | 0.069 | 0.0187 | Spine1 43%, Spine5 29% | PASS |
| Greet | 60 | — | 0.000 | 1.107 | 0.904 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Spine1 29%, Spine8 26% | PASS |
| SwimIdle | 72 | có | 0.000 | 1.135 | 0.927 | 0.0000 | — | 0.0000 | 0.079 | 0.0000 | Spine5 37%, Spine1 37% | PASS |
| SwimRest | 120 | có | 0.000 | 1.077 | 0.939 | 0.0000 | — | 0.0000 | 0.069 | 0.0000 | Spine1 61%, Spine5 19% | PASS |
| SwimSurface | 48 | có | 0.851 | 1.295 | 0.852 | 0.0000 | — | 0.0000 | 0.074 | 0.0000 | Spine5 38%, Spine1 36% | PASS |
| WaterEnter | 60 | — | 0.000 | 1.257 | 0.852 | 0.0000 | — | 0.0000 | — | 0.0000 | Spine1 38%, Spine5 37% | PASS |
| WaterExit | 60 | — | 0.000 | 1.252 | 0.855 | 0.0000 | — | 0.0000 | — | 0.0000 | Spine5 38%, Spine1 34% | PASS |
| Nuzzle | 75 | — | 0.000 | 1.114 | 0.903 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Spine1 56%, Neck 19% | PASS |

Ngưỡng (`character-budgets.json` ▸ `animationQa`, không nới): giãn ≤ 1.6, nén ≥ 0.45, trượt ≤ 0.02, xuyên ≤ 0.01, hở ≤ 0.03, khép vòng ≤ 0.5° (tương đối theo chiều cao).

## Cảnh báo QA

- không có

## LOD

| LOD | Tam giác | Đỉnh | Trong ngân sách |
|---|---|---|---|
| 0 | 23998 | 12095 | có |
| 1 | 10000 | 5096 | có |
| 2 | 4000 | 2096 | có |
- pass: LOD0 rest p95 deviation 0.000% (limit 0.40%)
- pass: LOD0 worst deformed p95 deviation 0.000% (limit 0.60%)
- pass: LOD1 rest p95 deviation 0.018% (limit 1.00%)
- pass: LOD1 worst deformed p95 deviation 0.019% (limit 1.40%)
- pass: LOD2 rest p95 deviation 0.059% (limit 2.50%)
- pass: LOD2 worst deformed p95 deviation 0.063% (limit 3.00%)

## PBR

- Bộ texture URP: metallic_smoothness=`DRG_steamdrake_Unstaged_MetallicSmoothness.png`, normal=`DRG_steamdrake_Unstaged_Normal.png`, base_color=`DRG_steamdrake_Unstaged_BaseColor.png`
- Base Color: image; UV chồng lấn 0.0000

## Unity

- prefab: `Assets/Dravio/Character/Prefabs/DRG_steamdrake_Unstaged_Rigged_LODGroup.prefab`
- material: `Assets/Dravio/Character/Materials/DRG_steamdrake_Unstaged.mat`
- baseController: `Assets/Dravio/Character/Animation/DragonBase_serpentine.controller`
- overrideController: `Assets/Dravio/Character/Animation/Overrides/DRG_steamdrake_Unstaged.overrideController`
