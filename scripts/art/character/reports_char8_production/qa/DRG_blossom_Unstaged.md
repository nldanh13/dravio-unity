# DRG_blossom_Unstaged

- Nguồn: `10_blossom.glb` — bằng chứng: **thực tế (chưa nghiệm thu)**. GLB nguồn không đổi: có
- Loài / giai đoạn: blossom / Unstaged
- Kiểm định pipeline: PASS_WITH_WARNINGS; Animation QA: **PASS_WITH_WARNINGS**

## Rig

- Archetype `winged` (locomotion contains "fly"); limbCount 4 (mesh measurement accepted (--limb-count auto)); rig v7
- Chiều cao 1.1301 m (source size kept); pivot dời theo mặt đối xứng 0.0 m
- 32 bone (32 deform), tối đa 4 ảnh hưởng/đỉnh, 0 đỉnh không trọng số
- IK: 4 chuỗi chân; anchor: VFX_Mouth, VFX_TailTip, VFX_Back, VFX_Ground

Ghi chú rig:

- Wing downstroke limited to 0.0 deg (wanted 22.9): membrane would enter the body below that
- Wing upstroke limited to 0.3 deg (wanted 43.0): membrane would enter the body below that
- Idle blink not authored: mesh has no eyelid shape key (blink/eye_close); generated meshes rarely separate eyelids
- 3154 vertices in floating fragments ignored for landmark detection
- contact cluster at y=-0.562 split at x=0.001 (empty band 0.073 m): left/right paws standing close together
- no neck narrowing in the profile; neck base set between chest and head

## Clip

| Clip | Frame | Loop | Tốc độ (m/s) | Giãn p99.9 | Nén p0.1 | Trượt chân | Xuyên đất | Hở tiếp xúc | Khép vòng (°) | Nâng (m) | Vùng tệ nhất | Kết quả |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Idle | 120 | có | 0.000 | 1.156 | 0.885 | 0.0000 | 0.0007 | 0.0000 | 0.028 | 0.0000 | Tail1 23%, Pelvis 20% | PASS |
| Eat | 60 | — | 0.000 | 2.023 | 0.429 | 0.0000 | 0.0000 | 0.0000 | — | 0.0205 | Neck 22%, Pelvis 18% | WARNING |
| Sleep | 120 | có | 0.000 | 2.904 | 0.431 | 0.0000 | 0.0000 | 0.0000 | 0.000 | 0.0659 | Neck 25%, Tail1 15% | WARNING |
| Happy | 45 | — | 0.000 | 1.394 | 0.751 | 0.0000 | 0.0000 | 0.0000 | — | 0.0114 | Pelvis 31%, Tail1 24% | PASS |
| Walk | 27 | có | 0.260 | 2.057 | 0.601 | 0.0006 | 0.0000 | 0.0021 | 0.000 | 0.0099 | Pelvis 41%, HindLower_L 12% | WARNING |
| Run | 15 | có | 1.128 | 3.084 | 0.528 | 0.0000 | 0.0000 | 0.0000 | 0.028 | 0.0230 | Pelvis 40%, HindLower_L 14% | WARNING |
| Fly | 32 | có | 0.222 | 2.086 | 0.536 | 0.0000 | — | 0.0000 | 0.000 | 0.0000 | Pelvis 28%, ForeLower_R 19% | WARNING |
| Rest | 120 | có | 0.000 | 2.772 | 0.503 | 0.0000 | 0.0000 | 0.0000 | 0.000 | 0.0669 | HindLower_R 26%, Pelvis 21% | WARNING |
| Wake | 60 | — | 0.000 | 2.129 | 0.475 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Pelvis 20%, Tail1 14% | WARNING |
| LookAround | 90 | — | 0.000 | 1.444 | 0.703 | 0.0000 | 0.0010 | 0.0000 | — | 0.0000 | Neck 43%, Chest 30% | PASS |
| Curious | 75 | — | 0.000 | 1.392 | 0.745 | 0.0000 | 0.0000 | 0.0000 | — | 0.0119 | Pelvis 38%, Tail1 15% | PASS |
| Drink | 75 | — | 0.000 | 2.251 | 0.423 | 0.0000 | 0.0000 | 0.0000 | — | 0.0204 | Pelvis 19%, Neck 17% | WARNING |
| Stretch | 90 | — | 0.000 | 2.083 | 0.672 | 0.0000 | 0.0000 | 0.0000 | — | 0.0288 | Pelvis 38%, Spine 14% | WARNING |
| Bask | 120 | có | 0.000 | 2.759 | 0.502 | 0.0000 | 0.0000 | 0.0000 | 0.000 | 0.0669 | HindLower_R 26%, HindLower_L 21% | WARNING |
| Greet | 60 | — | 0.000 | 1.092 | 0.879 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Neck 29%, Tail1 20% | PASS |
| Perch | 120 | có | 0.000 | 2.091 | 0.518 | 0.0000 | 0.0000 | 0.0000 | 0.028 | 0.0241 | Pelvis 30%, Tail1 27% | WARNING |
| Nuzzle | 75 | — | 0.000 | 1.136 | 0.831 | 0.0000 | 0.0017 | 0.0000 | — | 0.0000 | Neck 24%, Chest 20% | PASS |
| TakeOff | 40 | — | 0.000 | 1.895 | 0.521 | 0.0000 | 0.0141 | 0.0011 | — | 0.0000 | Pelvis 29%, Tail1 16% | WARNING |
| Glide | 60 | có | 0.000 | 2.104 | 0.536 | 0.0000 | — | 0.0000 | 0.000 | 0.0000 | Pelvis 27%, ForeLower_R 19% | WARNING |
| Land | 40 | — | 0.000 | 2.187 | 0.541 | 0.0000 | 0.0164 | 0.0000 | — | 0.0000 | Pelvis 42%, WingUpper_L 9% | WARNING |

Ngưỡng (`character-budgets.json` ▸ `animationQa`, không nới): giãn ≤ 1.6, nén ≥ 0.45, trượt ≤ 0.02, xuyên ≤ 0.01, hở ≤ 0.03, khép vòng ≤ 0.5° (tương đối theo chiều cao).

## Cảnh báo QA

- model: bones — bone orientation / mirror issue ({'zeroLength': [], 'mirrorErrorRel': 0.01371, 'chainDirection': []} / {'mirrorErrorRel': 0.01})
- Eat: deformation — edge stretch over limit (2.023 / 1.6)
- Eat: deformation — edge compression over limit (0.429 / 0.45)
- Sleep: deformation — edge stretch over limit (2.904 / 1.6)
- Sleep: deformation — edge compression over limit (0.431 / 0.45)
- Walk: deformation — edge stretch over limit (2.057 / 1.6)
- Run: deformation — edge stretch over limit (3.084 / 1.6)
- Fly: deformation — edge stretch over limit (2.086 / 1.6)
- Rest: deformation — edge stretch over limit (2.772 / 1.6)
- Wake: deformation — edge stretch over limit (2.129 / 1.6)
- Drink: deformation — edge stretch over limit (2.251 / 1.6)
- Drink: deformation — edge compression over limit (0.423 / 0.45)
- Stretch: deformation — edge stretch over limit (2.083 / 1.6)
- Bask: deformation — edge stretch over limit (2.759 / 1.6)
- Perch: deformation — edge stretch over limit (2.091 / 1.6)
- TakeOff: deformation — edge stretch over limit (1.895 / 1.6)
- TakeOff: ground — mesh below the ground plane (0.0141 / 0.01)
- Glide: deformation — edge stretch over limit (2.104 / 1.6)
- Land: deformation — edge stretch over limit (2.187 / 1.6)
- Land: ground — mesh below the ground plane (0.0164 / 0.01)

## LOD

| LOD | Tam giác | Đỉnh | Trong ngân sách |
|---|---|---|---|
| 0 | 24000 | 16964 | có |
| 1 | 9999 | 9548 | có |
| 2 | 4000 | 5232 | có |
- warning: DRG_blossom_Unstaged_LOD1: 4736 UV faces (47.36%) flipped_faces > 1.0%
- warning: DRG_blossom_Unstaged_LOD1: 1 loose vertices
- warning: DRG_blossom_Unstaged_LOD1: 4109 non-manifold/boundary edges after weld
- warning: DRG_blossom_Unstaged_LOD2: 1876 UV faces (46.90%) flipped_faces > 1.0%
- warning: DRG_blossom_Unstaged_LOD2: 1061 loose vertices
- warning: DRG_blossom_Unstaged_LOD2: 3470 non-manifold/boundary edges after weld
- warning: DRG_blossom_Unstaged_LOD0: 11308 UV faces (47.12%) flipped_faces > 1.0%
- warning: Image_0: 2048x2048 > recommended 1024
- pass: LOD0 rest p95 deviation 0.000% (limit 0.40%)
- pass: LOD0 worst deformed p95 deviation 0.000% (limit 0.60%)
- pass: LOD1 rest p95 deviation 0.067% (limit 1.00%)
- pass: LOD1 worst deformed p95 deviation 0.080% (limit 1.40%)
- pass: LOD2 rest p95 deviation 0.242% (limit 2.50%)
- pass: LOD2 worst deformed p95 deviation 0.283% (limit 3.00%)
- warning: LOD2 Base Color drift p95 14.6/255

## PBR

- Bộ texture URP: metallic_smoothness=`DRG_blossom_Unstaged_MetallicSmoothness.png`, base_color=`DRG_blossom_Unstaged_BaseColor.png`
- Base Color: image; UV chồng lấn 0.0000

## Unity

- prefab: `Assets/Dravio/Character/Prefabs/DRG_blossom_Unstaged_Rigged_LODGroup.prefab`
- material: `Assets/Dravio/Character/Materials/DRG_blossom_Unstaged.mat`
- baseController: `Assets/Dravio/Character/Animation/DragonBase_winged.controller`
- overrideController: `Assets/Dravio/Character/Animation/Overrides/DRG_blossom_Unstaged.overrideController`
