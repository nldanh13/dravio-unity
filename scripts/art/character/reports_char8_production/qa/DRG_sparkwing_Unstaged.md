# DRG_sparkwing_Unstaged

- Nguồn: `07_sparkwing.glb` — bằng chứng: **thực tế (chưa nghiệm thu)**. GLB nguồn không đổi: có
- Loài / giai đoạn: sparkwing / Unstaged
- Kiểm định pipeline: PASS_WITH_WARNINGS; Animation QA: **PASS_WITH_WARNINGS**

## Rig

- Archetype `winged` (locomotion contains "fly"); limbCount 4 (mesh measurement accepted (--limb-count auto)); rig v7
- Chiều cao 1.1489 m (source size kept); pivot dời theo mặt đối xứng 0.0 m
- 32 bone (32 deform), tối đa 4 ảnh hưởng/đỉnh, 0 đỉnh không trọng số
- IK: 4 chuỗi chân; anchor: VFX_Mouth, VFX_TailTip, VFX_Back, VFX_Ground

Ghi chú rig:

- Wing downstroke limited to 0.0 deg (wanted 22.9): membrane would enter the body below that
- Idle blink not authored: mesh has no eyelid shape key (blink/eye_close); generated meshes rarely separate eyelids
- 3093 vertices in floating fragments ignored for landmark detection
- contact cluster at y=-0.571 split at x=0.000 (empty band 0.099 m): left/right paws standing close together
- no neck narrowing in the profile; neck base set between chest and head

## Clip

| Clip | Frame | Loop | Tốc độ (m/s) | Giãn p99.9 | Nén p0.1 | Trượt chân | Xuyên đất | Hở tiếp xúc | Khép vòng (°) | Nâng (m) | Vùng tệ nhất | Kết quả |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Idle | 120 | có | 0.000 | 1.153 | 0.878 | 0.0000 | 0.0005 | 0.0000 | 0.063 | 0.0000 | Pelvis 38%, HindLower_R 11% | PASS |
| Eat | 60 | — | 0.000 | 2.804 | 0.441 | 0.0000 | 0.0000 | 0.0000 | — | 0.0182 | Spine 20%, Pelvis 19% | WARNING |
| Sleep | 120 | có | 0.000 | 4.073 | 0.525 | 0.0000 | 0.0000 | 0.0000 | 0.056 | 0.0640 | Pelvis 23%, Neck 17% | WARNING |
| Happy | 45 | — | 0.000 | 1.495 | 0.740 | 0.0000 | 0.0000 | 0.0000 | — | 0.0112 | Pelvis 39%, Spine 11% | PASS |
| Walk | 30 | có | 0.283 | 3.571 | 0.627 | 0.0006 | 0.0000 | 0.0022 | 0.069 | 0.0100 | Pelvis 28%, Spine 16% | WARNING |
| Run | 17 | có | 1.204 | 6.145 | 0.588 | 0.0000 | 0.0000 | 0.0000 | 0.069 | 0.0221 | Pelvis 27%, Spine 15% | WARNING |
| Fly | 32 | có | 0.425 | 4.109 | 0.600 | 0.0000 | — | 0.0000 | 0.063 | 0.0000 | Pelvis 34%, Spine 18% | WARNING |
| Rest | 120 | có | 0.000 | 4.073 | 0.601 | 0.0000 | 0.0000 | 0.0000 | 0.056 | 0.0645 | Pelvis 30%, HindLower_R 24% | WARNING |
| Wake | 60 | — | 0.000 | 3.097 | 0.563 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Pelvis 30%, HindLower_R 15% | WARNING |
| LookAround | 90 | — | 0.000 | 1.378 | 0.713 | 0.0000 | 0.0010 | 0.0000 | — | 0.0000 | Head 44%, Neck 36% | PASS |
| Curious | 75 | — | 0.000 | 1.642 | 0.628 | 0.0000 | 0.0000 | 0.0000 | — | 0.0106 | Pelvis 28%, Spine 19% | WARNING |
| Drink | 75 | — | 0.000 | 3.052 | 0.426 | 0.0000 | 0.0000 | 0.0000 | — | 0.0185 | Pelvis 18%, Spine 18% | WARNING |
| Stretch | 90 | — | 0.000 | 2.502 | 0.529 | 0.0000 | 0.0000 | 0.0000 | — | 0.0285 | Pelvis 38%, ForeLower_R 11% | WARNING |
| Bask | 120 | có | 0.000 | 4.073 | 0.589 | 0.0000 | 0.0000 | 0.0000 | 0.056 | 0.0645 | Pelvis 32%, HindLower_R 20% | WARNING |
| Greet | 60 | — | 0.000 | 1.088 | 0.887 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Chest 33%, Neck 22% | PASS |
| Nuzzle | 75 | — | 0.000 | 1.171 | 0.837 | 0.0000 | 0.0012 | 0.0000 | — | 0.0000 | Chest 24%, Pelvis 16% | PASS |
| TakeOff | 40 | — | 0.000 | 3.221 | 0.586 | 0.0000 | 0.0133 | 0.0015 | — | 0.0000 | Pelvis 35%, Spine 16% | WARNING |
| Glide | 60 | có | 0.000 | 4.109 | 0.619 | 0.0000 | — | 0.0000 | 0.063 | 0.0000 | Pelvis 34%, Spine 19% | WARNING |
| Land | 40 | — | 0.000 | 2.770 | 0.603 | 0.0000 | 0.0154 | 0.0000 | — | 0.0000 | Pelvis 37%, Spine 14% | WARNING |

Ngưỡng (`character-budgets.json` ▸ `animationQa`, không nới): giãn ≤ 1.6, nén ≥ 0.45, trượt ≤ 0.02, xuyên ≤ 0.01, hở ≤ 0.03, khép vòng ≤ 0.5° (tương đối theo chiều cao).

## Cảnh báo QA

- Eat: deformation — edge stretch over limit (2.804 / 1.6)
- Eat: deformation — edge compression over limit (0.441 / 0.45)
- Sleep: deformation — edge stretch over limit (4.073 / 1.6)
- Walk: deformation — edge stretch over limit (3.571 / 1.6)
- Run: deformation — edge stretch over limit (6.145 / 1.6)
- Fly: deformation — edge stretch over limit (4.109 / 1.6)
- Rest: deformation — edge stretch over limit (4.073 / 1.6)
- Wake: deformation — edge stretch over limit (3.097 / 1.6)
- Curious: deformation — edge stretch over limit (1.642 / 1.6)
- Drink: deformation — edge stretch over limit (3.052 / 1.6)
- Drink: deformation — edge compression over limit (0.426 / 0.45)
- Stretch: deformation — edge stretch over limit (2.502 / 1.6)
- Bask: deformation — edge stretch over limit (4.073 / 1.6)
- TakeOff: deformation — edge stretch over limit (3.221 / 1.6)
- TakeOff: ground — mesh below the ground plane (0.0133 / 0.01)
- Glide: deformation — edge stretch over limit (4.109 / 1.6)
- Land: deformation — edge stretch over limit (2.77 / 1.6)
- Land: ground — mesh below the ground plane (0.0154 / 0.01)

## LOD

| LOD | Tam giác | Đỉnh | Trong ngân sách |
|---|---|---|---|
| 0 | 24000 | 16764 | có |
| 1 | 10000 | 9436 | có |
| 2 | 4000 | 5259 | có |
- warning: DRG_sparkwing_Unstaged_LOD2: 2236 UV faces (55.90%) flipped_faces > 1.0%
- warning: DRG_sparkwing_Unstaged_LOD2: 793 loose vertices
- warning: DRG_sparkwing_Unstaged_LOD2: 3572 non-manifold/boundary edges after weld
- warning: DRG_sparkwing_Unstaged_LOD1: 5661 UV faces (56.61%) flipped_faces > 1.0%
- warning: DRG_sparkwing_Unstaged_LOD1: 1 loose vertices
- warning: DRG_sparkwing_Unstaged_LOD1: 3894 non-manifold/boundary edges after weld
- warning: DRG_sparkwing_Unstaged_LOD0: 13929 UV faces (58.04%) flipped_faces > 1.0%
- warning: Image_0: 2048x2048 > recommended 1024
- pass: LOD0 rest p95 deviation 0.000% (limit 0.40%)
- pass: LOD0 worst deformed p95 deviation 0.000% (limit 0.60%)
- pass: LOD1 rest p95 deviation 0.057% (limit 1.00%)
- pass: LOD1 worst deformed p95 deviation 0.072% (limit 1.40%)
- pass: LOD2 rest p95 deviation 0.228% (limit 2.50%)
- pass: LOD2 worst deformed p95 deviation 0.286% (limit 3.00%)
- warning: LOD2 Base Color drift p95 11.4/255

## PBR

- Bộ texture URP: metallic_smoothness=`DRG_sparkwing_Unstaged_MetallicSmoothness.png`, base_color=`DRG_sparkwing_Unstaged_BaseColor.png`
- Base Color: image; UV chồng lấn 0.0000

## Unity

- prefab: `Assets/Dravio/Character/Prefabs/DRG_sparkwing_Unstaged_Rigged_LODGroup.prefab`
- material: `Assets/Dravio/Character/Materials/DRG_sparkwing_Unstaged.mat`
- baseController: `Assets/Dravio/Character/Animation/DragonBase_winged.controller`
- overrideController: `Assets/Dravio/Character/Animation/Overrides/DRG_sparkwing_Unstaged.overrideController`
