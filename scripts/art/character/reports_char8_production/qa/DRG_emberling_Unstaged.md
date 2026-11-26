# DRG_emberling_Unstaged

- Nguồn: `01_emberling.glb` — bằng chứng: **thực tế (chưa nghiệm thu)**. GLB nguồn không đổi: có
- Loài / giai đoạn: emberling / Unstaged
- Kiểm định pipeline: PASS_WITH_WARNINGS; Animation QA: **PASS_WITH_WARNINGS**

## Rig

- Archetype `quadruped` (walks; silhouette states "bốn chân"); limbCount 4 (silhouetteVi); rig v7
- Chiều cao 0.9 m (species-character-spec.json prototypeAsset: EmberlingRefinedGLB.fbx (0.90 m); DragonCarePreview keeps the authored 0.90 m mesh); pivot dời theo mặt đối xứng 0.0 m
- 26 bone (26 deform), tối đa 4 ảnh hưởng/đỉnh, 0 đỉnh không trọng số
- IK: 4 chuỗi chân; anchor: VFX_Mouth, VFX_TailTip, VFX_Back, VFX_Ground

Ghi chú rig:

- Idle blink not authored: mesh has no eyelid shape key (blink/eye_close); generated meshes rarely separate eyelids
- 2523 vertices in floating fragments ignored for landmark detection
- contact cluster at y=-0.418 split at x=-0.000 (empty band 0.061 m): left/right paws standing close together
- no neck narrowing in the profile; neck base set between chest and head

## Clip

| Clip | Frame | Loop | Tốc độ (m/s) | Giãn p99.9 | Nén p0.1 | Trượt chân | Xuyên đất | Hở tiếp xúc | Khép vòng (°) | Nâng (m) | Vùng tệ nhất | Kết quả |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Idle | 120 | có | 0.000 | 1.161 | 0.877 | 0.0000 | 0.0012 | 0.0000 | 0.056 | 0.0000 | Pelvis 34%, HindUpper_R 22% | PASS |
| Eat | 60 | — | 0.000 | 2.720 | 0.438 | 0.0000 | 0.0000 | 0.0000 | — | 0.0148 | Spine 20%, Pelvis 16% | WARNING |
| Sleep | 120 | có | 0.000 | 3.321 | 0.425 | 0.0000 | 0.0000 | 0.0000 | 0.056 | 0.0465 | Pelvis 24%, HindUpper_R 15% | WARNING |
| Happy | 45 | — | 0.000 | 1.306 | 0.674 | 0.0000 | 0.0000 | 0.0000 | — | 0.0076 | Pelvis 41%, HindUpper_R 11% | PASS |
| Walk | 25 | có | 0.181 | 2.175 | 0.496 | 0.0005 | 0.0000 | 0.0018 | 0.056 | 0.0063 | Pelvis 47%, HindUpper_R 16% | WARNING |
| Run | 14 | có | 0.780 | 3.439 | 0.418 | 0.0000 | 0.0000 | 0.0000 | 0.048 | 0.0171 | Pelvis 51%, HindUpper_R 13% | WARNING |
| Rest | 120 | có | 0.000 | 3.251 | 0.435 | 0.0000 | 0.0000 | 0.0000 | 0.056 | 0.0473 | Pelvis 27%, HindUpper_R 19% | WARNING |
| Wake | 60 | — | 0.000 | 2.480 | 0.454 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Pelvis 31%, HindUpper_R 16% | WARNING |
| LookAround | 90 | — | 0.000 | 1.376 | 0.699 | 0.0000 | 0.0005 | 0.0000 | — | 0.0000 | Neck 61%, Head 34% | PASS |
| Curious | 75 | — | 0.000 | 1.516 | 0.645 | 0.0000 | 0.0000 | 0.0000 | — | 0.0078 | Pelvis 27%, Spine 20% | PASS |
| Drink | 75 | — | 0.000 | 3.160 | 0.430 | 0.0000 | 0.0000 | 0.0000 | — | 0.0149 | Spine 23%, Pelvis 14% | WARNING |
| Stretch | 90 | — | 0.000 | 2.735 | 0.550 | 0.0000 | 0.0000 | 0.0000 | — | 0.0224 | Pelvis 26%, Spine 24% | WARNING |
| Bask | 120 | có | 0.000 | 3.251 | 0.435 | 0.0000 | 0.0000 | 0.0000 | 0.056 | 0.0473 | Pelvis 27%, HindUpper_R 19% | WARNING |
| Greet | 60 | — | 0.000 | 1.121 | 0.914 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Neck 52%, Tail1 17% | PASS |
| Nuzzle | 75 | — | 0.000 | 1.197 | 0.876 | 0.0000 | 0.0007 | 0.0000 | — | 0.0000 | Spine 36%, Neck 30% | PASS |

Ngưỡng (`character-budgets.json` ▸ `animationQa`, không nới): giãn ≤ 1.6, nén ≥ 0.45, trượt ≤ 0.02, xuyên ≤ 0.01, hở ≤ 0.03, khép vòng ≤ 0.5° (tương đối theo chiều cao).

## Cảnh báo QA

- Eat: deformation — edge stretch over limit (2.72 / 1.6)
- Eat: deformation — edge compression over limit (0.438 / 0.45)
- Sleep: deformation — edge stretch over limit (3.321 / 1.6)
- Sleep: deformation — edge compression over limit (0.425 / 0.45)
- Walk: deformation — edge stretch over limit (2.175 / 1.6)
- Run: deformation — edge stretch over limit (3.439 / 1.6)
- Run: deformation — edge compression over limit (0.418 / 0.45)
- Rest: deformation — edge stretch over limit (3.251 / 1.6)
- Rest: deformation — edge compression over limit (0.435 / 0.45)
- Wake: deformation — edge stretch over limit (2.48 / 1.6)
- Drink: deformation — edge stretch over limit (3.16 / 1.6)
- Drink: deformation — edge compression over limit (0.43 / 0.45)
- Stretch: deformation — edge stretch over limit (2.735 / 1.6)
- Bask: deformation — edge stretch over limit (3.251 / 1.6)
- Bask: deformation — edge compression over limit (0.435 / 0.45)

## LOD

| LOD | Tam giác | Đỉnh | Trong ngân sách |
|---|---|---|---|
| 0 | 24000 | 16253 | có |
| 1 | 9999 | 8988 | có |
| 2 | 4000 | 5066 | có |
- warning: DRG_emberling_Unstaged_LOD1: 4099 UV faces (40.99%) flipped_faces > 1.0%
- warning: DRG_emberling_Unstaged_LOD1: 3381 non-manifold/boundary edges after weld
- warning: DRG_emberling_Unstaged_LOD2: 1597 UV faces (39.92%) flipped_faces > 1.0%
- warning: DRG_emberling_Unstaged_LOD2: 487 loose vertices
- warning: DRG_emberling_Unstaged_LOD2: 3460 non-manifold/boundary edges after weld
- warning: DRG_emberling_Unstaged_LOD0: 9823 UV faces (40.93%) flipped_faces > 1.0%
- warning: Image_0: 2048x2048 > recommended 1024
- pass: LOD0 rest p95 deviation 0.000% (limit 0.40%)
- pass: LOD0 worst deformed p95 deviation 0.000% (limit 0.60%)
- pass: LOD1 rest p95 deviation 0.048% (limit 1.00%)
- pass: LOD1 worst deformed p95 deviation 0.060% (limit 1.40%)
- pass: LOD2 rest p95 deviation 0.202% (limit 2.50%)
- pass: LOD2 worst deformed p95 deviation 0.248% (limit 3.00%)
- warning: LOD2 Base Color drift p95 10.1/255

## PBR

- Bộ texture URP: metallic_smoothness=`DRG_emberling_Unstaged_MetallicSmoothness.png`, base_color=`DRG_emberling_Unstaged_BaseColor.png`
- Base Color: image; UV chồng lấn 0.0000

## Unity

- prefab: `Assets/Dravio/Character/Prefabs/DRG_emberling_Unstaged_Rigged_LODGroup.prefab`
- material: `Assets/Dravio/Character/Materials/DRG_emberling_Unstaged.mat`
- baseController: `Assets/Dravio/Character/Animation/DragonBase_quadruped.controller`
- overrideController: `Assets/Dravio/Character/Animation/Overrides/DRG_emberling_Unstaged.overrideController`
