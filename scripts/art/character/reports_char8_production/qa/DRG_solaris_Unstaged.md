# DRG_solaris_Unstaged

- Nguồn: `11_solaris.glb` — bằng chứng: **thực tế (chưa nghiệm thu)**. GLB nguồn không đổi: có
- Loài / giai đoạn: solaris / Unstaged
- Kiểm định pipeline: PASS_WITH_WARNINGS; Animation QA: **PASS_WITH_WARNINGS**

## Rig

- Archetype `winged` (locomotion contains "fly"); limbCount 4 (mesh measurement accepted (--limb-count auto)); rig v7
- Chiều cao 1.3135 m (source size kept); pivot dời theo mặt đối xứng 0.0 m
- 32 bone (32 deform), tối đa 4 ảnh hưởng/đỉnh, 0 đỉnh không trọng số
- IK: 4 chuỗi chân; anchor: VFX_Mouth, VFX_TailTip, VFX_Back, VFX_Ground

Ghi chú rig:

- Wing downstroke limited to 0.0 deg (wanted 22.9): membrane would enter the body below that
- Idle blink not authored: mesh has no eyelid shape key (blink/eye_close); generated meshes rarely separate eyelids
- 6036 vertices in floating fragments ignored for landmark detection
- contact cluster at y=-0.770 split at x=0.000 (empty band 0.080 m): left/right paws standing close together
- contact cluster at y=-0.296 split at x=0.001 (empty band 0.106 m): left/right paws standing close together
- no neck narrowing in the profile; neck base set between chest and head

## Clip

| Clip | Frame | Loop | Tốc độ (m/s) | Giãn p99.9 | Nén p0.1 | Trượt chân | Xuyên đất | Hở tiếp xúc | Khép vòng (°) | Nâng (m) | Vùng tệ nhất | Kết quả |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Idle | 120 | có | 0.000 | 1.120 | 0.900 | 0.0000 | 0.0011 | 0.0000 | 0.287 | 0.0000 | Pelvis 42%, Spine 21% | PASS |
| Eat | 60 | — | 0.000 | 1.805 | 0.635 | 0.0000 | 0.0000 | 0.0000 | — | 0.0297 | Pelvis 31%, Neck 19% | WARNING |
| Sleep | 120 | có | 0.000 | 2.368 | 0.638 | 0.0000 | 0.0000 | 0.0000 | 0.283 | 0.0597 | Spine 25%, HindLower_R 20% | WARNING |
| Happy | 45 | — | 0.000 | 1.230 | 0.770 | 0.0000 | 0.0000 | 0.0000 | — | 0.0153 | Pelvis 43%, Spine 17% | PASS |
| Walk | 35 | có | 0.112 | 1.424 | 0.649 | 0.0002 | 0.0000 | 0.0008 | 0.285 | 0.0119 | Spine 38%, Pelvis 28% | PASS |
| Run | 19 | có | 0.497 | 2.262 | 0.650 | 0.0000 | 0.0000 | 0.0000 | 0.285 | 0.0336 | Spine 37%, Pelvis 23% | WARNING |
| Fly | 32 | có | 1.232 | 1.997 | 0.619 | 0.0000 | — | 0.0000 | 0.288 | 0.0000 | Spine 40%, ForeLower_R 17% | WARNING |
| Rest | 120 | có | 0.000 | 2.338 | 0.647 | 0.0000 | 0.0000 | 0.0000 | 0.283 | 0.0611 | Spine 26%, HindLower_R 21% | WARNING |
| Wake | 60 | — | 0.000 | 1.693 | 0.657 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Pelvis 31%, Spine 24% | WARNING |
| LookAround | 90 | — | 0.000 | 1.169 | 0.866 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Neck 68%, Head 15% | PASS |
| Curious | 75 | — | 0.000 | 1.094 | 0.828 | 0.0000 | 0.0125 | 0.0000 | — | 0.0080 | Pelvis 34%, Spine 17% | WARNING |
| Drink | 75 | — | 0.000 | 1.925 | 0.633 | 0.0000 | 0.0000 | 0.0000 | — | 0.0303 | Pelvis 32%, Neck 18% | WARNING |
| Stretch | 90 | — | 0.000 | 1.325 | 0.767 | 0.0000 | 0.0000 | 0.0000 | — | 0.0284 | Pelvis 35%, Spine 24% | PASS |
| Bask | 120 | có | 0.000 | 2.338 | 0.647 | 0.0000 | 0.0000 | 0.0000 | 0.283 | 0.0611 | Spine 26%, HindLower_R 21% | WARNING |
| Greet | 60 | — | 0.000 | 1.087 | 0.948 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Pelvis 40%, Neck 30% | PASS |
| Nuzzle | 75 | — | 0.000 | 1.104 | 0.953 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Neck 45%, Spine 17% | PASS |
| TakeOff | 40 | — | 0.000 | 1.755 | 0.598 | 0.0000 | 0.0163 | 0.0008 | — | 0.0000 | Spine 38%, Pelvis 20% | WARNING |
| Glide | 60 | có | 0.000 | 1.940 | 0.621 | 0.0000 | — | 0.0000 | 0.288 | 0.0000 | Spine 40%, ForeLower_R 17% | WARNING |
| Land | 40 | — | 0.000 | 1.657 | 0.637 | 0.0000 | 0.0192 | 0.0000 | — | 0.0000 | Spine 38%, Pelvis 18% | WARNING |

Ngưỡng (`character-budgets.json` ▸ `animationQa`, không nới): giãn ≤ 1.6, nén ≥ 0.45, trượt ≤ 0.02, xuyên ≤ 0.01, hở ≤ 0.03, khép vòng ≤ 0.5° (tương đối theo chiều cao).

## Cảnh báo QA

- Eat: deformation — edge stretch over limit (1.805 / 1.6)
- Sleep: deformation — edge stretch over limit (2.368 / 1.6)
- Run: deformation — edge stretch over limit (2.262 / 1.6)
- Fly: deformation — edge stretch over limit (1.997 / 1.6)
- Rest: deformation — edge stretch over limit (2.338 / 1.6)
- Wake: deformation — edge stretch over limit (1.693 / 1.6)
- Curious: ground — mesh below the ground plane (0.0125 / 0.01)
- Drink: deformation — edge stretch over limit (1.925 / 1.6)
- Bask: deformation — edge stretch over limit (2.338 / 1.6)
- TakeOff: deformation — edge stretch over limit (1.755 / 1.6)
- TakeOff: ground — mesh below the ground plane (0.0163 / 0.01)
- Glide: deformation — edge stretch over limit (1.94 / 1.6)
- Land: deformation — edge stretch over limit (1.657 / 1.6)
- Land: facing — head behind the chest (face turned backwards) (3 / 0)
- Land: ground — mesh below the ground plane (0.0192 / 0.01)

## LOD

| LOD | Tam giác | Đỉnh | Trong ngân sách |
|---|---|---|---|
| 0 | 24000 | 19469 | có |
| 1 | 10000 | 11811 | có |
| 2 | 4000 | 6877 | có |
- warning: DRG_solaris_Unstaged_LOD1: 3997 UV faces (39.97%) flipped_faces > 1.0%
- warning: DRG_solaris_Unstaged_LOD1: 2 loose vertices
- warning: DRG_solaris_Unstaged_LOD1: 6188 non-manifold/boundary edges after weld
- warning: DRG_solaris_Unstaged_LOD2: 1587 UV faces (39.67%) flipped_faces > 1.0%
- warning: DRG_solaris_Unstaged_LOD2: 2214 loose vertices
- warning: DRG_solaris_Unstaged_LOD2: 3926 non-manifold/boundary edges after weld
- warning: DRG_solaris_Unstaged_LOD0: 9538 UV faces (39.74%) flipped_faces > 1.0%
- warning: Image_0: 2048x2048 > recommended 1024
- pass: LOD0 rest p95 deviation 0.000% (limit 0.40%)
- pass: LOD0 worst deformed p95 deviation 0.000% (limit 0.60%)
- pass: LOD1 rest p95 deviation 0.078% (limit 1.00%)
- pass: LOD1 worst deformed p95 deviation 0.087% (limit 1.40%)
- pass: LOD2 rest p95 deviation 0.273% (limit 2.50%)
- pass: LOD2 worst deformed p95 deviation 0.301% (limit 3.00%)
- warning: LOD2 Base Color drift p95 16.7/255

## PBR

- Bộ texture URP: metallic_smoothness=`DRG_solaris_Unstaged_MetallicSmoothness.png`, base_color=`DRG_solaris_Unstaged_BaseColor.png`
- Base Color: image; UV chồng lấn 0.0000

## Unity

- prefab: `Assets/Dravio/Character/Prefabs/DRG_solaris_Unstaged_Rigged_LODGroup.prefab`
- material: `Assets/Dravio/Character/Materials/DRG_solaris_Unstaged.mat`
- baseController: `Assets/Dravio/Character/Animation/DragonBase_winged.controller`
- overrideController: `Assets/Dravio/Character/Animation/Overrides/DRG_solaris_Unstaged.overrideController`
