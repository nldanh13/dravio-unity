# DRG_sprout_Unstaged

- Nguồn: `03_sprout.glb` — bằng chứng: **thực tế (chưa nghiệm thu)**. GLB nguồn không đổi: có
- Loài / giai đoạn: sprout / Unstaged
- Kiểm định pipeline: PASS_WITH_WARNINGS; Animation QA: **PASS_WITH_WARNINGS**

## Rig

- Archetype `quadruped` (walks; silhouette states "bốn chân"); limbCount 4 (silhouetteVi); rig v7
- Chiều cao 1.4877 m (source size kept); pivot dời theo mặt đối xứng 0.0 m
- 26 bone (26 deform), tối đa 4 ảnh hưởng/đỉnh, 0 đỉnh không trọng số
- IK: 4 chuỗi chân; anchor: VFX_Mouth, VFX_TailTip, VFX_Back, VFX_Ground

Ghi chú rig:

- Idle blink not authored: mesh has no eyelid shape key (blink/eye_close); generated meshes rarely separate eyelids
- 2840 vertices in floating fragments ignored for landmark detection
- no neck narrowing in the profile; neck base set between chest and head

## Clip

| Clip | Frame | Loop | Tốc độ (m/s) | Giãn p99.9 | Nén p0.1 | Trượt chân | Xuyên đất | Hở tiếp xúc | Khép vòng (°) | Nâng (m) | Vùng tệ nhất | Kết quả |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Idle | 120 | có | 0.000 | 1.256 | 0.849 | 0.0000 | 0.0000 | 0.0000 | 0.056 | 0.0000 | Tail1 41%, Pelvis 24% | PASS |
| Eat | 60 | — | 0.000 | 3.793 | 0.384 | 0.0000 | 0.0000 | 0.0000 | — | 0.0191 | Neck 47%, Spine 17% | WARNING |
| Sleep | 120 | có | 0.000 | 3.684 | 0.409 | 0.0000 | 0.0000 | 0.0000 | 0.056 | 0.0557 | Neck 45%, Tail1 22% | WARNING |
| Happy | 45 | — | 0.000 | 1.889 | 0.682 | 0.0000 | 0.0000 | 0.0000 | — | 0.0104 | Pelvis 34%, Tail1 34% | WARNING |
| Walk | 30 | có | 0.288 | 2.728 | 0.651 | 0.0000 | 0.0000 | 0.0000 | 0.048 | 0.0083 | Pelvis 38%, Tail1 18% | WARNING |
| Run | 17 | có | 1.227 | 4.401 | 0.556 | 0.0000 | 0.0000 | 0.0000 | 0.056 | 0.0235 | Pelvis 32%, HindLower_L 16% | WARNING |
| Burrow | 60 | có | 0.000 | 3.601 | 0.407 | 0.0000 | 0.0000 | 0.0000 | 0.048 | 0.0375 | Pelvis 33%, Neck 22% | WARNING |
| Rest | 120 | có | 0.000 | 2.941 | 0.472 | 0.0000 | 0.0000 | 0.0000 | 0.056 | 0.0563 | HindLower_L 27%, HindLower_R 24% | WARNING |
| Wake | 60 | — | 0.000 | 2.630 | 0.447 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Tail1 24%, Neck 23% | WARNING |
| LookAround | 90 | — | 0.000 | 1.588 | 0.634 | 0.0000 | 0.0001 | 0.0000 | — | 0.0000 | Neck 82%, Spine 5% | PASS |
| Curious | 75 | — | 0.000 | 1.757 | 0.656 | 0.0000 | 0.0000 | 0.0000 | — | 0.0103 | Pelvis 27%, Tail1 22% | WARNING |
| Drink | 75 | — | 0.000 | 4.194 | 0.362 | 0.0000 | 0.0000 | 0.0000 | — | 0.0193 | Neck 48%, Spine 21% | WARNING |
| Stretch | 90 | — | 0.000 | 2.649 | 0.667 | 0.0000 | 0.0000 | 0.0000 | — | 0.0274 | Pelvis 50%, ForeUpper_R 14% | WARNING |
| Bask | 120 | có | 0.000 | 2.863 | 0.472 | 0.0000 | 0.0000 | 0.0000 | 0.056 | 0.0563 | HindLower_L 26%, HindLower_R 26% | WARNING |
| Greet | 60 | — | 0.000 | 1.357 | 0.809 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Neck 33%, Tail1 22% | PASS |
| DigPrep | 45 | — | 0.000 | 1.464 | 0.682 | 0.0017 | 0.0038 | 0.0014 | — | 0.0000 | Neck 31%, Pelvis 26% | PASS |
| BurrowEnter | 60 | — | 0.000 | 2.536 | 0.487 | 0.0000 | 0.0074 | 0.0000 | — | 0.0000 | Neck 41%, Pelvis 20% | WARNING |
| BurrowEmerge | 60 | — | 0.000 | 2.076 | 0.546 | 0.0000 | 0.0074 | 0.0000 | — | 0.0000 | Neck 38%, Spine 19% | WARNING |
| Nuzzle | 75 | — | 0.000 | 1.304 | 0.724 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Neck 33%, Spine 26% | PASS |

Ngưỡng (`character-budgets.json` ▸ `animationQa`, không nới): giãn ≤ 1.6, nén ≥ 0.45, trượt ≤ 0.02, xuyên ≤ 0.01, hở ≤ 0.03, khép vòng ≤ 0.5° (tương đối theo chiều cao).

## Cảnh báo QA

- Eat: deformation — edge stretch over limit (3.793 / 1.6)
- Eat: deformation — edge compression over limit (0.384 / 0.45)
- Sleep: deformation — edge stretch over limit (3.684 / 1.6)
- Sleep: deformation — edge compression over limit (0.409 / 0.45)
- Happy: deformation — edge stretch over limit (1.889 / 1.6)
- Walk: deformation — edge stretch over limit (2.728 / 1.6)
- Run: deformation — edge stretch over limit (4.401 / 1.6)
- Burrow: deformation — edge stretch over limit (3.601 / 1.6)
- Burrow: deformation — edge compression over limit (0.407 / 0.45)
- Rest: deformation — edge stretch over limit (2.941 / 1.6)
- Wake: deformation — edge stretch over limit (2.63 / 1.6)
- Wake: deformation — edge compression over limit (0.447 / 0.45)
- Curious: deformation — edge stretch over limit (1.757 / 1.6)
- Drink: deformation — edge stretch over limit (4.194 / 1.6)
- Drink: deformation — edge compression over limit (0.362 / 0.45)
- Stretch: deformation — edge stretch over limit (2.649 / 1.6)
- Bask: deformation — edge stretch over limit (2.863 / 1.6)
- BurrowEnter: deformation — edge stretch over limit (2.536 / 1.6)
- BurrowEmerge: deformation — edge stretch over limit (2.076 / 1.6)

## LOD

| LOD | Tam giác | Đỉnh | Trong ngân sách |
|---|---|---|---|
| 0 | 24000 | 16209 | có |
| 1 | 9999 | 8895 | có |
| 2 | 4000 | 4882 | có |
- warning: DRG_sprout_Unstaged_LOD1: 3564 UV faces (35.64%) flipped_faces > 1.0%
- warning: DRG_sprout_Unstaged_LOD1: 9 loose vertices
- warning: DRG_sprout_Unstaged_LOD1: 3517 non-manifold/boundary edges after weld
- warning: DRG_sprout_Unstaged_LOD2: 1390 UV faces (34.75%) flipped_faces > 1.0%
- warning: DRG_sprout_Unstaged_LOD2: 469 loose vertices
- warning: DRG_sprout_Unstaged_LOD2: 3496 non-manifold/boundary edges after weld
- warning: DRG_sprout_Unstaged_LOD0: 8454 UV faces (35.23%) flipped_faces > 1.0%
- warning: Image_0: 2048x2048 > recommended 1024
- pass: LOD0 rest p95 deviation 0.000% (limit 0.40%)
- pass: LOD0 worst deformed p95 deviation 0.000% (limit 0.60%)
- pass: LOD1 rest p95 deviation 0.053% (limit 1.00%)
- pass: LOD1 worst deformed p95 deviation 0.073% (limit 1.40%)
- pass: LOD2 rest p95 deviation 0.207% (limit 2.50%)
- pass: LOD2 worst deformed p95 deviation 0.271% (limit 3.00%)
- warning: LOD2 Base Color drift p95 16.4/255

## PBR

- Bộ texture URP: metallic_smoothness=`DRG_sprout_Unstaged_MetallicSmoothness.png`, base_color=`DRG_sprout_Unstaged_BaseColor.png`
- Base Color: image; UV chồng lấn 0.0000

## Unity

- prefab: `Assets/Dravio/Character/Prefabs/DRG_sprout_Unstaged_Rigged_LODGroup.prefab`
- material: `Assets/Dravio/Character/Materials/DRG_sprout_Unstaged.mat`
- baseController: `Assets/Dravio/Character/Animation/DragonBase_quadruped.controller`
- overrideController: `Assets/Dravio/Character/Animation/Overrides/DRG_sprout_Unstaged.overrideController`
