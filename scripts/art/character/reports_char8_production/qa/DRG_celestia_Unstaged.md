# DRG_celestia_Unstaged

- Nguồn: `15_celestia.glb` — bằng chứng: **thực tế (chưa nghiệm thu)**. GLB nguồn không đổi: có
- Loài / giai đoạn: celestia / Unstaged
- Kiểm định pipeline: PASS_WITH_WARNINGS; Animation QA: **PASS_WITH_WARNINGS**

## Rig

- Archetype `winged` (locomotion contains "fly"); limbCount 4 (mesh measurement accepted (--limb-count auto)); rig v7
- Chiều cao 1.1989 m (source size kept); pivot dời theo mặt đối xứng 0.0 m
- 32 bone (32 deform), tối đa 4 ảnh hưởng/đỉnh, 0 đỉnh không trọng số
- IK: 4 chuỗi chân; anchor: VFX_Mouth, VFX_TailTip, VFX_Back, VFX_Ground

Ghi chú rig:

- Wing downstroke limited to 0.0 deg (wanted 22.9): membrane would enter the body below that
- Idle blink not authored: mesh has no eyelid shape key (blink/eye_close); generated meshes rarely separate eyelids
- 3052 vertices in floating fragments ignored for landmark detection
- contact cluster at y=-0.584 split at x=-0.000 (empty band 0.116 m): left/right paws standing close together
- no neck narrowing in the profile; neck base set between chest and head

## Clip

| Clip | Frame | Loop | Tốc độ (m/s) | Giãn p99.9 | Nén p0.1 | Trượt chân | Xuyên đất | Hở tiếp xúc | Khép vòng (°) | Nâng (m) | Vùng tệ nhất | Kết quả |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Idle | 120 | có | 0.000 | 1.149 | 0.874 | 0.0000 | 0.0004 | 0.0000 | 0.079 | 0.0000 | Pelvis 41%, HindLower_L 11% | PASS |
| Eat | 60 | — | 0.000 | 2.693 | 0.399 | 0.0000 | 0.0000 | 0.0000 | — | 0.0179 | Pelvis 26%, Spine 19% | WARNING |
| Sleep | 120 | có | 0.000 | 3.248 | 0.502 | 0.0000 | 0.0000 | 0.0000 | 0.079 | 0.0578 | Pelvis 29%, Neck 23% | WARNING |
| Happy | 45 | — | 0.000 | 1.366 | 0.714 | 0.0000 | 0.0000 | 0.0000 | — | 0.0108 | Pelvis 43%, HindLower_L 10% | PASS |
| Walk | 30 | có | 0.281 | 3.077 | 0.604 | 0.0000 | 0.0000 | 0.0001 | 0.079 | 0.0096 | Pelvis 44%, ForeLower_R 15% | WARNING |
| Run | 17 | có | 1.199 | 5.309 | 0.573 | 0.0000 | 0.0000 | 0.0000 | 0.084 | 0.0218 | Pelvis 42%, ForeLower_R 18% | WARNING |
| Fly | 32 | có | 0.516 | 4.119 | 0.600 | 0.0000 | — | 0.0000 | 0.079 | 0.0000 | Pelvis 47%, ForeLower_L 12% | WARNING |
| Rest | 120 | có | 0.000 | 3.248 | 0.565 | 0.0000 | 0.0000 | 0.0000 | 0.074 | 0.0583 | Pelvis 36%, HindLower_R 28% | WARNING |
| Wake | 60 | — | 0.000 | 2.545 | 0.526 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Pelvis 36%, HindLower_R 12% | WARNING |
| LookAround | 90 | — | 0.000 | 1.349 | 0.738 | 0.0000 | 0.0008 | 0.0000 | — | 0.0000 | Neck 72%, Head 7% | PASS |
| Curious | 75 | — | 0.000 | 1.570 | 0.624 | 0.0000 | 0.0000 | 0.0000 | — | 0.0105 | Pelvis 39%, HindLower_L 9% | PASS |
| Drink | 75 | — | 0.000 | 2.936 | 0.384 | 0.0000 | 0.0000 | 0.0000 | — | 0.0181 | Pelvis 23%, Spine 22% | WARNING |
| Stretch | 90 | — | 0.000 | 2.787 | 0.505 | 0.0000 | 0.0000 | 0.0000 | — | 0.0273 | Pelvis 38%, HindLower_L 12% | WARNING |
| Bask | 120 | có | 0.000 | 3.248 | 0.553 | 0.0000 | 0.0000 | 0.0000 | 0.074 | 0.0583 | Pelvis 36%, HindLower_R 21% | WARNING |
| Greet | 60 | — | 0.000 | 1.128 | 0.861 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Neck 42%, Spine 26% | PASS |
| Nuzzle | 75 | — | 0.000 | 1.226 | 0.819 | 0.0000 | 0.0013 | 0.0000 | — | 0.0000 | Neck 32%, Spine 30% | PASS |
| TakeOff | 40 | — | 0.000 | 3.115 | 0.542 | 0.0000 | 0.0121 | 0.0014 | — | 0.0000 | Pelvis 45%, ForeLower_L 14% | WARNING |
| Glide | 60 | có | 0.000 | 4.119 | 0.649 | 0.0000 | — | 0.0000 | 0.079 | 0.0000 | Pelvis 48%, ForeLower_L 12% | WARNING |
| Land | 40 | — | 0.000 | 2.429 | 0.589 | 0.0000 | 0.0142 | 0.0000 | — | 0.0000 | Pelvis 44%, ForeLower_L 13% | WARNING |

Ngưỡng (`character-budgets.json` ▸ `animationQa`, không nới): giãn ≤ 1.6, nén ≥ 0.45, trượt ≤ 0.02, xuyên ≤ 0.01, hở ≤ 0.03, khép vòng ≤ 0.5° (tương đối theo chiều cao).

## Cảnh báo QA

- model: bones — bone orientation / mirror issue ({'zeroLength': [], 'mirrorErrorRel': 0.01106, 'chainDirection': []} / {'mirrorErrorRel': 0.01})
- Eat: deformation — edge stretch over limit (2.693 / 1.6)
- Eat: deformation — edge compression over limit (0.399 / 0.45)
- Sleep: deformation — edge stretch over limit (3.248 / 1.6)
- Walk: deformation — edge stretch over limit (3.077 / 1.6)
- Run: deformation — edge stretch over limit (5.309 / 1.6)
- Fly: deformation — edge stretch over limit (4.119 / 1.6)
- Rest: deformation — edge stretch over limit (3.248 / 1.6)
- Wake: deformation — edge stretch over limit (2.545 / 1.6)
- Drink: deformation — edge stretch over limit (2.936 / 1.6)
- Drink: deformation — edge compression over limit (0.384 / 0.45)
- Stretch: deformation — edge stretch over limit (2.787 / 1.6)
- Bask: deformation — edge stretch over limit (3.248 / 1.6)
- TakeOff: deformation — edge stretch over limit (3.115 / 1.6)
- TakeOff: ground — mesh below the ground plane (0.0121 / 0.01)
- Glide: deformation — edge stretch over limit (4.119 / 1.6)
- Land: deformation — edge stretch over limit (2.429 / 1.6)
- Land: ground — mesh below the ground plane (0.0142 / 0.01)

## LOD

| LOD | Tam giác | Đỉnh | Trong ngân sách |
|---|---|---|---|
| 0 | 24000 | 16767 | có |
| 1 | 10000 | 9406 | có |
| 2 | 4000 | 5171 | có |
- warning: DRG_celestia_Unstaged_LOD1: 3936 UV faces (39.36%) flipped_faces > 1.0%
- warning: DRG_celestia_Unstaged_LOD1: 2 loose vertices
- warning: DRG_celestia_Unstaged_LOD1: 4088 non-manifold/boundary edges after weld
- warning: DRG_celestia_Unstaged_LOD0: 9217 UV faces (38.40%) flipped_faces > 1.0%
- warning: DRG_celestia_Unstaged_LOD2: 1624 UV faces (40.60%) flipped_faces > 1.0%
- warning: DRG_celestia_Unstaged_LOD2: 822 loose vertices
- warning: DRG_celestia_Unstaged_LOD2: 3604 non-manifold/boundary edges after weld
- warning: Image_0: 2048x2048 > recommended 1024
- pass: LOD0 rest p95 deviation 0.000% (limit 0.40%)
- pass: LOD0 worst deformed p95 deviation 0.000% (limit 0.60%)
- pass: LOD1 rest p95 deviation 0.059% (limit 1.00%)
- pass: LOD1 worst deformed p95 deviation 0.075% (limit 1.40%)
- pass: LOD2 rest p95 deviation 0.237% (limit 2.50%)
- pass: LOD2 worst deformed p95 deviation 0.295% (limit 3.00%)
- warning: LOD2 Base Color drift p95 10.8/255

## PBR

- Bộ texture URP: metallic_smoothness=`DRG_celestia_Unstaged_MetallicSmoothness.png`, base_color=`DRG_celestia_Unstaged_BaseColor.png`
- Base Color: image; UV chồng lấn 0.0000

## Unity

- prefab: `Assets/Dravio/Character/Prefabs/DRG_celestia_Unstaged_Rigged_LODGroup.prefab`
- material: `Assets/Dravio/Character/Materials/DRG_celestia_Unstaged.mat`
- baseController: `Assets/Dravio/Character/Animation/DragonBase_winged.controller`
- overrideController: `Assets/Dravio/Character/Animation/Overrides/DRG_celestia_Unstaged.overrideController`
