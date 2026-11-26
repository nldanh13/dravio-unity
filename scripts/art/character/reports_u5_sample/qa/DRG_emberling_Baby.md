# DRG_emberling_Baby

- Nguồn: `SAMPLE_emberling_Baby.glb` — bằng chứng: **SAMPLE**. GLB nguồn không đổi: có
- Loài / giai đoạn: emberling / Baby
- Kiểm định pipeline: PASS; Animation QA: **PASS**

## Rig

- Archetype `quadruped` (walks; silhouette states "bốn chân"); limbCount 4 (silhouetteVi); rig v3
- Chiều cao 0.9 m (species-character-spec.json prototypeAsset: EmberlingRefinedGLB.fbx (0.90 m); DragonCarePreview keeps the authored 0.90 m mesh); pivot dời theo mặt đối xứng 0.0 m
- 26 bone (26 deform), tối đa 4 ảnh hưởng/đỉnh, 0 đỉnh không trọng số
- IK: 4 chuỗi chân; anchor: VFX_Mouth, VFX_TailTip, VFX_Back, VFX_Ground

Ghi chú rig:

- Idle blink not authored: mesh has no eyelid shape key (blink/eye_close); generated meshes rarely separate eyelids
- 112 vertices in floating fragments ignored for landmark detection
- no neck narrowing in the profile; neck base set between chest and head

## Clip

| Clip | Frame | Loop | Tốc độ (m/s) | Giãn p99.9 | Nén p0.1 | Trượt chân | Xuyên đất | Hở tiếp xúc | Khép vòng (°) | Nâng (m) | Vùng tệ nhất | Kết quả |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Idle | 120 | có | 0.000 | 1.155 | 0.863 | 0.0000 | 0.0001 | 0.0000 | 0.079 | 0.0000 | Tail1 61%, Pelvis 38% | PASS |
| Eat | 60 | — | 0.000 | 1.491 | 0.588 | 0.0000 | 0.0000 | 0.0000 | — | 0.0058 | Neck 54%, HindFoot_R 11% | PASS |
| Sleep | 120 | có | 0.000 | 1.450 | 0.644 | 0.0000 | 0.0000 | 0.0000 | 0.074 | 0.0430 | Neck 64%, Pelvis 17% | PASS |
| Happy | 45 | — | 0.000 | 1.463 | 0.658 | 0.0000 | 0.0004 | 0.0000 | — | 0.0000 | Tail1 56%, Pelvis 37% | PASS |
| Walk | 32 | có | 0.137 | 1.302 | 0.686 | 0.0000 | 0.0005 | 0.0000 | 0.079 | 0.0000 | Pelvis 41%, HindFoot_R 17% | PASS |
| Run | 20 | có | 0.530 | 1.542 | 0.532 | 0.0000 | 0.0000 | 0.0001 | 0.079 | 0.0094 | Pelvis 39%, HindFoot_L 21% | PASS |

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
- pass: LOD1 rest p95 deviation 0.018% (limit 1.00%)
- pass: LOD1 worst deformed p95 deviation 0.024% (limit 1.40%)
- pass: LOD2 rest p95 deviation 0.053% (limit 2.50%)
- pass: LOD2 worst deformed p95 deviation 0.066% (limit 3.00%)

## PBR

- Bộ texture URP: metallic_smoothness=`DRG_emberling_Baby_MetallicSmoothness.png`, normal=`DRG_emberling_Baby_Normal.png`, base_color=`DRG_emberling_Baby_BaseColor.png`
- Base Color: image; UV chồng lấn 0.0031

## Unity

- prefab: `Assets/Dravio/Character/Prefabs/DRG_emberling_Baby_Rigged_LODGroup.prefab`
- material: `Assets/Dravio/Character/Materials/DRG_emberling_Baby.mat`
- baseController: `Assets/Dravio/Character/Animation/DragonBase_quadruped.controller`
- overrideController: `Assets/Dravio/Character/Animation/Overrides/DRG_emberling_Baby.overrideController`
