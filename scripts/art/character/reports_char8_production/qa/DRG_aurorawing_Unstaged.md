# DRG_aurorawing_Unstaged

- Nguồn: `14_aurorawing.glb` — bằng chứng: **thực tế (chưa nghiệm thu)**. GLB nguồn không đổi: có
- Loài / giai đoạn: aurorawing / Unstaged
- Kiểm định pipeline: PASS_WITH_WARNINGS; Animation QA: **PASS_WITH_WARNINGS**

## Rig

- Archetype `winged` (locomotion contains "fly"); limbCount 4 (mesh measurement accepted (--limb-count auto)); rig v7
- Chiều cao 1.4653 m (source size kept); pivot dời theo mặt đối xứng 0.0 m
- 32 bone (32 deform), tối đa 4 ảnh hưởng/đỉnh, 0 đỉnh không trọng số
- IK: 4 chuỗi chân; anchor: VFX_Mouth, VFX_TailTip, VFX_Back, VFX_Ground

Ghi chú rig:

- Wing downstroke limited to 0.3 deg (wanted 22.9): membrane would enter the body below that
- Idle blink not authored: mesh has no eyelid shape key (blink/eye_close); generated meshes rarely separate eyelids
- 6411 vertices in floating fragments ignored for landmark detection
- contact cluster at y=-0.672 split at x=-0.002 (empty band 0.099 m): left/right paws standing close together
- contact cluster at y=-0.102 split at x=0.004 (empty band 0.108 m): left/right paws standing close together
- no neck narrowing in the profile; neck base set between chest and head

## Clip

| Clip | Frame | Loop | Tốc độ (m/s) | Giãn p99.9 | Nén p0.1 | Trượt chân | Xuyên đất | Hở tiếp xúc | Khép vòng (°) | Nâng (m) | Vùng tệ nhất | Kết quả |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Idle | 120 | có | 0.000 | 1.168 | 0.868 | 0.0000 | 0.0000 | 0.0000 | 0.040 | 0.0037 | Pelvis 25%, Tail1 17% | PASS |
| Eat | 60 | — | 0.000 | 2.391 | 0.569 | 0.0000 | 0.0009 | 0.0000 | — | 0.0000 | Spine 36%, Pelvis 17% | WARNING |
| Sleep | 120 | có | 0.000 | 2.938 | 0.609 | 0.0000 | 0.0000 | 0.0000 | 0.028 | 0.0364 | Spine 23%, Pelvis 18% | WARNING |
| Happy | 45 | — | 0.000 | 1.360 | 0.689 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Pelvis 33%, Tail1 27% | PASS |
| Walk | 34 | có | 0.148 | 1.464 | 0.649 | 0.0002 | 0.0008 | 0.0009 | 0.028 | 0.0000 | Pelvis 30%, Spine 28% | PASS |
| Run | 19 | có | 0.642 | 2.125 | 0.602 | 0.0000 | 0.0000 | 0.0000 | 0.040 | 0.0063 | Spine 30%, Pelvis 24% | WARNING |
| Fly | 32 | có | 0.842 | 2.133 | 0.460 | 0.0000 | — | 0.0000 | 0.028 | 0.0000 | Spine 38%, ForeLower_R 14% | WARNING |
| Rest | 120 | có | 0.000 | 1.721 | 0.627 | 0.0000 | 0.0000 | 0.0000 | 0.028 | 0.0378 | Pelvis 31%, HindLower_R 27% | WARNING |
| Wake | 60 | — | 0.000 | 2.050 | 0.603 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Pelvis 25%, Spine 22% | WARNING |
| LookAround | 90 | — | 0.000 | 1.927 | 0.732 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Spine 34%, Neck 15% | WARNING |
| Curious | 75 | — | 0.000 | 1.245 | 0.709 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Pelvis 28%, Spine 25% | PASS |
| Drink | 75 | — | 0.000 | 2.555 | 0.556 | 0.0000 | 0.0016 | 0.0000 | — | 0.0000 | Spine 39%, Pelvis 16% | WARNING |
| Stretch | 90 | — | 0.000 | 1.437 | 0.623 | 0.0000 | 0.0000 | 0.0000 | — | 0.0158 | Spine 24%, Pelvis 22% | PASS |
| Bask | 120 | có | 0.000 | 1.879 | 0.597 | 0.0000 | 0.0000 | 0.0000 | 0.028 | 0.0378 | Pelvis 26%, HindLower_R 19% | WARNING |
| Greet | 60 | — | 0.000 | 1.183 | 0.904 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Pelvis 21%, Tail1 20% | PASS |
| Nuzzle | 75 | — | 0.000 | 1.232 | 0.908 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Spine 27%, Head 16% | PASS |
| TakeOff | 40 | — | 0.000 | 2.038 | 0.523 | 0.0000 | 0.0000 | 0.0033 | — | 0.0000 | Spine 36%, WingUpper_L 9% | WARNING |
| Glide | 60 | có | 0.000 | 1.648 | 0.601 | 0.0000 | — | 0.0000 | 0.056 | 0.0000 | Spine 30%, Pelvis 19% | WARNING |
| Land | 40 | — | 0.000 | 2.108 | 0.553 | 0.0000 | 0.0010 | 0.0025 | — | 0.0000 | Spine 33%, WingUpper_L 12% | WARNING |

Ngưỡng (`character-budgets.json` ▸ `animationQa`, không nới): giãn ≤ 1.6, nén ≥ 0.45, trượt ≤ 0.02, xuyên ≤ 0.01, hở ≤ 0.03, khép vòng ≤ 0.5° (tương đối theo chiều cao).

## Cảnh báo QA

- model: bones — bone orientation / mirror issue ({'zeroLength': [], 'mirrorErrorRel': 0.04343, 'chainDirection': []} / {'mirrorErrorRel': 0.01})
- Eat: deformation — edge stretch over limit (2.391 / 1.6)
- Sleep: deformation — edge stretch over limit (2.938 / 1.6)
- Run: deformation — edge stretch over limit (2.125 / 1.6)
- Fly: deformation — edge stretch over limit (2.133 / 1.6)
- Rest: deformation — edge stretch over limit (1.721 / 1.6)
- Wake: deformation — edge stretch over limit (2.05 / 1.6)
- LookAround: deformation — edge stretch over limit (1.927 / 1.6)
- Drink: deformation — edge stretch over limit (2.555 / 1.6)
- Bask: deformation — edge stretch over limit (1.879 / 1.6)
- TakeOff: deformation — edge stretch over limit (2.038 / 1.6)
- Glide: deformation — edge stretch over limit (1.648 / 1.6)
- Land: deformation — edge stretch over limit (2.108 / 1.6)

## LOD

| LOD | Tam giác | Đỉnh | Trong ngân sách |
|---|---|---|---|
| 0 | 23999 | 19789 | có |
| 1 | 10000 | 12020 | có |
| 2 | 4000 | 7034 | có |
- warning: DRG_aurorawing_Unstaged_LOD1: 4826 UV faces (48.26%) flipped_faces > 1.0%
- warning: DRG_aurorawing_Unstaged_LOD1: 10 loose vertices
- warning: DRG_aurorawing_Unstaged_LOD1: 6208 non-manifold/boundary edges after weld
- warning: DRG_aurorawing_Unstaged_LOD0: 11798 UV faces (49.16%) flipped_faces > 1.0%
- warning: DRG_aurorawing_Unstaged_LOD0: 3 non-manifold/boundary edges after weld
- warning: DRG_aurorawing_Unstaged_LOD2: 1935 UV faces (48.38%) flipped_faces > 1.0%
- warning: DRG_aurorawing_Unstaged_LOD2: 2647 loose vertices
- warning: DRG_aurorawing_Unstaged_LOD2: 3692 non-manifold/boundary edges after weld
- warning: Image_0: 2048x2048 > recommended 1024
- pass: LOD0 rest p95 deviation 0.000% (limit 0.40%)
- pass: LOD0 worst deformed p95 deviation 0.000% (limit 0.60%)
- pass: LOD1 rest p95 deviation 0.088% (limit 1.00%)
- pass: LOD1 worst deformed p95 deviation 0.099% (limit 1.40%)
- pass: LOD2 rest p95 deviation 0.297% (limit 2.50%)
- pass: LOD2 worst deformed p95 deviation 0.336% (limit 3.00%)
- warning: LOD2 Base Color drift p95 20.1/255

## PBR

- Bộ texture URP: metallic_smoothness=`DRG_aurorawing_Unstaged_MetallicSmoothness.png`, base_color=`DRG_aurorawing_Unstaged_BaseColor.png`
- Base Color: image; UV chồng lấn 0.0000

## Unity

- prefab: `Assets/Dravio/Character/Prefabs/DRG_aurorawing_Unstaged_Rigged_LODGroup.prefab`
- material: `Assets/Dravio/Character/Materials/DRG_aurorawing_Unstaged.mat`
- baseController: `Assets/Dravio/Character/Animation/DragonBase_winged.controller`
- overrideController: `Assets/Dravio/Character/Animation/Overrides/DRG_aurorawing_Unstaged.overrideController`
