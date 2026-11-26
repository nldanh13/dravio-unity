# DRG_sprout_Baby

- Nguồn: `SAMPLE_sprout_Baby.glb` — bằng chứng: **SAMPLE**. GLB nguồn không đổi: có
- Loài / giai đoạn: sprout / Baby
- Kiểm định pipeline: PASS; Animation QA: **PASS**

## Rig

- Archetype `quadruped` (walks; silhouette states "bốn chân"); limbCount 4 (silhouetteVi); rig v3
- Chiều cao 1.0109 m (source size kept); pivot dời theo mặt đối xứng 0.0 m
- 26 bone (26 deform), tối đa 4 ảnh hưởng/đỉnh, 0 đỉnh không trọng số
- IK: 4 chuỗi chân; anchor: VFX_Mouth, VFX_TailTip, VFX_Back, VFX_Ground

Ghi chú rig:

- Idle blink not authored: mesh has no eyelid shape key (blink/eye_close); generated meshes rarely separate eyelids
- 112 vertices in floating fragments ignored for landmark detection
- no neck narrowing in the profile; neck base set between chest and head

## Clip

| Clip | Frame | Loop | Tốc độ (m/s) | Giãn p99.9 | Nén p0.1 | Trượt chân | Xuyên đất | Hở tiếp xúc | Khép vòng (°) | Nâng (m) | Vùng tệ nhất | Kết quả |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Idle | 120 | có | 0.000 | 1.154 | 0.871 | 0.0000 | 0.0001 | 0.0000 | 0.048 | 0.0000 | Tail1 52%, Pelvis 46% | PASS |
| Eat | 60 | — | 0.000 | 1.537 | 0.529 | 0.0000 | 0.0000 | 0.0000 | — | 0.0082 | Neck 35%, Head 26% | PASS |
| Sleep | 120 | có | 0.000 | 1.414 | 0.629 | 0.0000 | 0.0000 | 0.0000 | 0.063 | 0.0418 | Neck 53%, Head 31% | PASS |
| Happy | 45 | — | 0.000 | 1.472 | 0.670 | 0.0000 | 0.0004 | 0.0000 | — | 0.0000 | Pelvis 46%, Tail1 42% | PASS |
| Walk | 32 | có | 0.131 | 1.297 | 0.689 | 0.0000 | 0.0004 | 0.0000 | 0.048 | 0.0000 | Pelvis 43%, HindFoot_R 19% | PASS |
| Run | 20 | có | 0.505 | 1.560 | 0.516 | 0.0000 | 0.0000 | 0.0000 | 0.056 | 0.0120 | Pelvis 41%, HindFoot_L 23% | PASS |
| Burrow | 60 | có | 0.000 | 1.465 | 0.685 | 0.0000 | 0.0000 | 0.0000 | 0.056 | 0.0241 | Pelvis 50%, HindFoot_L 14% | PASS |

Ngưỡng (`character-budgets.json` ▸ `animationQa`, không nới): giãn ≤ 1.6, nén ≥ 0.45, trượt ≤ 0.02, xuyên ≤ 0.01, hở ≤ 0.03, khép vòng ≤ 0.5° (tương đối theo chiều cao).

## Cảnh báo QA

- không có

## LOD

| LOD | Tam giác | Đỉnh | Trong ngân sách |
|---|---|---|---|
| 0 | 24000 | 12090 | có |
| 1 | 10000 | 5090 | có |
| 2 | 4000 | 2090 | có |
- pass: LOD0 rest p95 deviation 0.000% (limit 0.40%)
- pass: LOD0 worst deformed p95 deviation 0.000% (limit 0.60%)
- pass: LOD1 rest p95 deviation 0.019% (limit 1.00%)
- pass: LOD1 worst deformed p95 deviation 0.025% (limit 1.40%)
- pass: LOD2 rest p95 deviation 0.055% (limit 2.50%)
- pass: LOD2 worst deformed p95 deviation 0.065% (limit 3.00%)

## PBR

- Bộ texture URP: metallic_smoothness=`DRG_sprout_Baby_MetallicSmoothness.png`, normal=`DRG_sprout_Baby_Normal.png`, base_color=`DRG_sprout_Baby_BaseColor.png`
- Base Color: image; UV chồng lấn 0.0028

## Unity

- prefab: `Assets/Dravio/Character/Prefabs/DRG_sprout_Baby_Rigged_LODGroup.prefab`
- material: `Assets/Dravio/Character/Materials/DRG_sprout_Baby.mat`
- baseController: `Assets/Dravio/Character/Animation/DragonBase_quadruped.controller`
- overrideController: `Assets/Dravio/Character/Animation/Overrides/DRG_sprout_Baby.overrideController`
