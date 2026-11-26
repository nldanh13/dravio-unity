# DRG_zephyr_Unstaged

- Nguồn: `04_zephyr.glb` — bằng chứng: **thực tế (chưa nghiệm thu)**. GLB nguồn không đổi: có
- Loài / giai đoạn: zephyr / Unstaged
- Kiểm định pipeline: PASS_WITH_WARNINGS; Animation QA: **PASS_WITH_WARNINGS**

## Rig

- Archetype `winged` (locomotion contains "fly"); limbCount 4 (mesh measurement accepted (--limb-count auto)); rig v7
- Chiều cao 1.3094 m (source size kept); pivot dời theo mặt đối xứng 0.0039 m
- 32 bone (32 deform), tối đa 4 ảnh hưởng/đỉnh, 0 đỉnh không trọng số
- IK: 4 chuỗi chân; anchor: VFX_Mouth, VFX_TailTip, VFX_Back, VFX_Ground

Ghi chú rig:

- Wing downstroke limited to 0.0 deg (wanted 22.9): membrane would enter the body below that
- Wing upstroke limited to 0.0 deg (wanted 43.0): membrane would enter the body below that
- Idle blink not authored: mesh has no eyelid shape key (blink/eye_close); generated meshes rarely separate eyelids
- 5676 vertices in floating fragments ignored for landmark detection
- contact cluster at y=-0.745 split at x=-0.003 (empty band 0.079 m): left/right paws standing close together
- contact cluster at y=-0.319 split at x=-0.004 (empty band 0.132 m): left/right paws standing close together
- no neck narrowing in the profile; neck base set between chest and head

## Clip

| Clip | Frame | Loop | Tốc độ (m/s) | Giãn p99.9 | Nén p0.1 | Trượt chân | Xuyên đất | Hở tiếp xúc | Khép vòng (°) | Nâng (m) | Vùng tệ nhất | Kết quả |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Idle | 120 | có | 0.000 | 1.133 | 0.905 | 0.0000 | 0.0019 | 0.0000 | 0.287 | 0.0000 | Tail1 23%, Pelvis 17% | PASS |
| Eat | 60 | — | 0.000 | 1.599 | 0.619 | 0.0000 | 0.0000 | 0.0000 | — | 0.0202 | WingUpper_L 18%, HindLower_L 15% | PASS |
| Sleep | 120 | có | 0.000 | 1.745 | 0.668 | 0.0000 | 0.0000 | 0.0000 | 0.285 | 0.0327 | WingUpper_L 18%, HindLower_R 17% | WARNING |
| Happy | 45 | — | 0.000 | 1.248 | 0.772 | 0.0000 | 0.0000 | 0.0000 | — | 0.0125 | Tail1 27%, Pelvis 20% | PASS |
| Walk | 40 | có | 0.090 | 1.417 | 0.694 | 0.0003 | 0.0000 | 0.0009 | 0.287 | 0.0109 | Spine 26%, HindLower_R 16% | PASS |
| Run | 22 | có | 0.394 | 1.646 | 0.654 | 0.0250 | 0.0000 | 0.0000 | 0.287 | 0.0231 | Spine 22%, HindLower_R 16% | WARNING |
| Fly | 32 | có | 0.245 | 1.944 | 0.623 | 0.0000 | — | 0.0000 | 0.289 | 0.0000 | Spine 28%, ForeLower_L 14% | WARNING |
| Rest | 120 | có | 0.000 | 1.564 | 0.664 | 0.0000 | 0.0000 | 0.0000 | 0.287 | 0.0334 | HindLower_R 16%, Spine 16% | PASS |
| Wake | 60 | — | 0.000 | 1.399 | 0.676 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | HindLower_R 15%, Spine 15% | PASS |
| LookAround | 90 | — | 0.000 | 1.220 | 0.855 | 0.0000 | 0.0011 | 0.0000 | — | 0.0000 | WingUpper_L 27%, Head 24% | PASS |
| Curious | 75 | — | 0.000 | 1.103 | 0.842 | 0.0000 | 0.0707 | 0.0000 | — | 0.0080 | Pelvis 28%, Tail1 14% | WARNING |
| Drink | 75 | — | 0.000 | 1.672 | 0.626 | 0.0000 | 0.0000 | 0.0000 | — | 0.0208 | WingUpper_L 18%, HindLower_L 16% | WARNING |
| Stretch | 90 | — | 0.000 | 1.266 | 0.751 | 0.0000 | 0.0397 | 0.0000 | — | 0.0249 | Spine 18%, HindLower_R 16% | WARNING |
| Bask | 120 | có | 0.000 | 1.589 | 0.661 | 0.0000 | 0.0000 | 0.0000 | 0.287 | 0.0334 | HindLower_L 18%, HindLower_R 17% | PASS |
| Greet | 60 | — | 0.000 | 1.130 | 0.925 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Tail1 27%, Pelvis 19% | PASS |
| Perch | 120 | có | 0.000 | 1.895 | 0.572 | 0.0000 | 0.0000 | 0.0000 | 0.288 | 0.0291 | Spine 18%, HindLower_L 17% | WARNING |
| Nuzzle | 75 | — | 0.000 | 1.133 | 0.938 | 0.0000 | 0.0013 | 0.0000 | — | 0.0000 | WingUpper_L 24%, Head 21% | PASS |
| TakeOff | 40 | — | 0.000 | 1.757 | 0.593 | 0.0000 | 0.0202 | 0.0009 | — | 0.0000 | Spine 22%, HindLower_L 17% | WARNING |
| Glide | 60 | có | 0.000 | 1.972 | 0.620 | 0.0000 | — | 0.0000 | 0.289 | 0.0000 | Spine 28%, Tail1 15% | WARNING |
| Land | 40 | — | 0.000 | 1.873 | 0.591 | 0.0000 | 0.0918 | 0.0000 | — | 0.0000 | Spine 24%, HindLower_L 16% | WARNING |

Ngưỡng (`character-budgets.json` ▸ `animationQa`, không nới): giãn ≤ 1.6, nén ≥ 0.45, trượt ≤ 0.02, xuyên ≤ 0.01, hở ≤ 0.03, khép vòng ≤ 0.5° (tương đối theo chiều cao).

## Cảnh báo QA

- model: bones — bone orientation / mirror issue ({'zeroLength': [], 'mirrorErrorRel': 0.02374, 'chainDirection': []} / {'mirrorErrorRel': 0.01})
- Sleep: deformation — edge stretch over limit (1.745 / 1.6)
- Run: deformation — edge stretch over limit (1.646 / 1.6)
- Run: footSliding — planted foot slides (0.025 / 0.02)
- Fly: deformation — edge stretch over limit (1.944 / 1.6)
- Curious: ground — mesh below the ground plane (0.0707 / 0.01)
- Drink: deformation — edge stretch over limit (1.672 / 1.6)
- Stretch: ground — mesh below the ground plane (0.0397 / 0.01)
- Perch: deformation — edge stretch over limit (1.895 / 1.6)
- TakeOff: deformation — edge stretch over limit (1.757 / 1.6)
- TakeOff: ground — mesh below the ground plane (0.0202 / 0.01)
- Glide: deformation — edge stretch over limit (1.972 / 1.6)
- Land: deformation — edge stretch over limit (1.873 / 1.6)
- Land: facing — head behind the chest (face turned backwards) (3 / 0)
- Land: ground — mesh below the ground plane (0.0918 / 0.01)

## LOD

| LOD | Tam giác | Đỉnh | Trong ngân sách |
|---|---|---|---|
| 0 | 24000 | 19461 | có |
| 1 | 10000 | 11795 | có |
| 2 | 4000 | 6867 | có |
- warning: DRG_zephyr_Unstaged_LOD1: 4164 UV faces (41.64%) flipped_faces > 1.0%
- warning: DRG_zephyr_Unstaged_LOD1: 4 loose vertices
- warning: DRG_zephyr_Unstaged_LOD1: 6076 non-manifold/boundary edges after weld
- warning: DRG_zephyr_Unstaged_LOD0: 9709 UV faces (40.45%) flipped_faces > 1.0%
- warning: DRG_zephyr_Unstaged_LOD2: 1674 UV faces (41.85%) flipped_faces > 1.0%
- warning: DRG_zephyr_Unstaged_LOD2: 2337 loose vertices
- warning: DRG_zephyr_Unstaged_LOD2: 3810 non-manifold/boundary edges after weld
- warning: Image_0: 2048x2048 > recommended 1024
- pass: LOD0 rest p95 deviation 0.000% (limit 0.40%)
- pass: LOD0 worst deformed p95 deviation 0.000% (limit 0.60%)
- pass: LOD1 rest p95 deviation 0.077% (limit 1.00%)
- pass: LOD1 worst deformed p95 deviation 0.087% (limit 1.40%)
- warning: LOD1 Base Color drift p95 8.3/255
- pass: LOD2 rest p95 deviation 0.282% (limit 2.50%)
- pass: LOD2 worst deformed p95 deviation 0.309% (limit 3.00%)
- warning: LOD2 Base Color drift p95 20.4/255

## PBR

- Bộ texture URP: metallic_smoothness=`DRG_zephyr_Unstaged_MetallicSmoothness.png`, base_color=`DRG_zephyr_Unstaged_BaseColor.png`
- Base Color: image; UV chồng lấn 0.0000

## Unity

- prefab: `Assets/Dravio/Character/Prefabs/DRG_zephyr_Unstaged_Rigged_LODGroup.prefab`
- material: `Assets/Dravio/Character/Materials/DRG_zephyr_Unstaged.mat`
- baseController: `Assets/Dravio/Character/Animation/DragonBase_winged.controller`
- overrideController: `Assets/Dravio/Character/Animation/Overrides/DRG_zephyr_Unstaged.overrideController`
