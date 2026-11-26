# DRG_magma_Unstaged

- Nguồn: `06_magma.glb` — bằng chứng: **thực tế (chưa nghiệm thu)**. GLB nguồn không đổi: có
- Loài / giai đoạn: magma / Unstaged
- Kiểm định pipeline: PASS_WITH_WARNINGS; Animation QA: **PASS_WITH_WARNINGS**

## Rig

- Archetype `quadruped` (walks; limb count not stated (quadruped template once limbCount=4 is supplied)); limbCount 4 (mesh measurement accepted (--limb-count auto)); rig v7
- Chiều cao 1.199 m (source size kept); pivot dời theo mặt đối xứng 0.0046 m
- 26 bone (26 deform), tối đa 4 ảnh hưởng/đỉnh, 0 đỉnh không trọng số
- IK: 4 chuỗi chân; anchor: VFX_Mouth, VFX_TailTip, VFX_Back, VFX_Ground

Ghi chú rig:

- Idle blink not authored: mesh has no eyelid shape key (blink/eye_close); generated meshes rarely separate eyelids
- 3036 vertices in floating fragments ignored for landmark detection
- contact cluster at y=-0.579 split at x=-0.004 (empty band 0.079 m): left/right paws standing close together
- no neck narrowing in the profile; neck base set between chest and head

## Clip

| Clip | Frame | Loop | Tốc độ (m/s) | Giãn p99.9 | Nén p0.1 | Trượt chân | Xuyên đất | Hở tiếp xúc | Khép vòng (°) | Nâng (m) | Vùng tệ nhất | Kết quả |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Idle | 120 | có | 0.000 | 1.156 | 0.879 | 0.0000 | 0.0017 | 0.0000 | 0.074 | 0.0000 | Pelvis 35%, Tail1 27% | PASS |
| Eat | 60 | — | 0.000 | 2.374 | 0.511 | 0.0000 | 0.0000 | 0.0000 | — | 0.0174 | Pelvis 20%, Spine 19% | WARNING |
| Sleep | 120 | có | 0.000 | 3.255 | 0.435 | 0.0000 | 0.0000 | 0.0000 | 0.056 | 0.0721 | Pelvis 17%, HindLower_R 14% | WARNING |
| Happy | 45 | — | 0.000 | 1.459 | 0.663 | 0.0000 | 0.0000 | 0.0000 | — | 0.0108 | Pelvis 34%, Tail1 23% | PASS |
| Walk | 28 | có | 0.251 | 2.178 | 0.564 | 0.0005 | 0.0000 | 0.0020 | 0.074 | 0.0094 | Pelvis 30%, Spine 20% | WARNING |
| Run | 16 | có | 1.058 | 3.530 | 0.480 | 0.0000 | 0.0000 | 0.0000 | 0.069 | 0.0236 | Pelvis 29%, Spine 18% | WARNING |
| Burrow | 60 | có | 0.000 | 3.086 | 0.425 | 0.0000 | 0.0000 | 0.0000 | 0.079 | 0.0419 | Pelvis 29%, HindUpper_L 17% | WARNING |
| Rest | 120 | có | 0.000 | 3.198 | 0.438 | 0.0000 | 0.0000 | 0.0000 | 0.069 | 0.0730 | Pelvis 18%, HindLower_R 13% | WARNING |
| Wake | 60 | — | 0.000 | 2.414 | 0.469 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Pelvis 26%, HindUpper_L 14% | WARNING |
| LookAround | 90 | — | 0.000 | 1.252 | 0.811 | 0.0000 | 0.0010 | 0.0000 | — | 0.0000 | Neck 44%, Spine 14% | PASS |
| Curious | 75 | — | 0.000 | 1.461 | 0.688 | 0.0000 | 0.0000 | 0.0000 | — | 0.0093 | Pelvis 28%, Spine 21% | PASS |
| Drink | 75 | — | 0.000 | 2.587 | 0.487 | 0.0000 | 0.0000 | 0.0000 | — | 0.0177 | Pelvis 20%, Spine 19% | WARNING |
| Stretch | 90 | — | 0.000 | 2.393 | 0.612 | 0.0000 | 0.0000 | 0.0000 | — | 0.0306 | Pelvis 22%, Spine 21% | WARNING |
| Bask | 120 | có | 0.000 | 3.195 | 0.439 | 0.0000 | 0.0000 | 0.0000 | 0.069 | 0.0730 | Pelvis 18%, HindLower_R 12% | WARNING |
| Greet | 60 | — | 0.000 | 1.152 | 0.887 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Pelvis 36%, Tail1 34% | PASS |
| DigPrep | 45 | — | 0.000 | 1.473 | 0.767 | 0.0017 | 0.0046 | 0.0014 | — | 0.0000 | Spine 38%, Pelvis 20% | PASS |
| BurrowEnter | 60 | — | 0.000 | 2.218 | 0.652 | 0.0000 | 0.0085 | 0.0000 | — | 0.0000 | Spine 29%, Pelvis 20% | WARNING |
| BurrowEmerge | 60 | — | 0.000 | 1.698 | 0.711 | 0.0000 | 0.0085 | 0.0000 | — | 0.0000 | Spine 27%, Pelvis 19% | WARNING |
| Nuzzle | 75 | — | 0.000 | 1.159 | 0.906 | 0.0000 | 0.0008 | 0.0000 | — | 0.0000 | Neck 35%, Spine 17% | PASS |

Ngưỡng (`character-budgets.json` ▸ `animationQa`, không nới): giãn ≤ 1.6, nén ≥ 0.45, trượt ≤ 0.02, xuyên ≤ 0.01, hở ≤ 0.03, khép vòng ≤ 0.5° (tương đối theo chiều cao).

## Cảnh báo QA

- model: bones — bone orientation / mirror issue ({'zeroLength': [], 'mirrorErrorRel': 0.01623, 'chainDirection': []} / {'mirrorErrorRel': 0.01})
- Eat: deformation — edge stretch over limit (2.374 / 1.6)
- Sleep: deformation — edge stretch over limit (3.255 / 1.6)
- Sleep: deformation — edge compression over limit (0.435 / 0.45)
- Walk: deformation — edge stretch over limit (2.178 / 1.6)
- Run: deformation — edge stretch over limit (3.53 / 1.6)
- Burrow: deformation — edge stretch over limit (3.086 / 1.6)
- Burrow: deformation — edge compression over limit (0.425 / 0.45)
- Rest: deformation — edge stretch over limit (3.198 / 1.6)
- Rest: deformation — edge compression over limit (0.438 / 0.45)
- Wake: deformation — edge stretch over limit (2.414 / 1.6)
- Drink: deformation — edge stretch over limit (2.587 / 1.6)
- Stretch: deformation — edge stretch over limit (2.393 / 1.6)
- Bask: deformation — edge stretch over limit (3.195 / 1.6)
- Bask: deformation — edge compression over limit (0.439 / 0.45)
- BurrowEnter: deformation — edge stretch over limit (2.218 / 1.6)
- BurrowEmerge: deformation — edge stretch over limit (1.698 / 1.6)

## LOD

| LOD | Tam giác | Đỉnh | Trong ngân sách |
|---|---|---|---|
| 0 | 24000 | 16918 | có |
| 1 | 9999 | 9518 | có |
| 2 | 3999 | 5245 | có |
- warning: DRG_magma_Unstaged_LOD1: 4476 UV faces (44.76%) flipped_faces > 1.0%
- warning: DRG_magma_Unstaged_LOD1: 4 loose vertices
- warning: DRG_magma_Unstaged_LOD1: 4289 non-manifold/boundary edges after weld
- warning: DRG_magma_Unstaged_LOD2: 1823 UV faces (45.59%) flipped_faces > 1.0%
- warning: DRG_magma_Unstaged_LOD2: 896 loose vertices
- warning: DRG_magma_Unstaged_LOD2: 3587 non-manifold/boundary edges after weld
- warning: DRG_magma_Unstaged_LOD0: 10475 UV faces (43.65%) flipped_faces > 1.0%
- warning: Image_0: 2048x2048 > recommended 1024
- pass: LOD0 rest p95 deviation 0.000% (limit 0.40%)
- pass: LOD0 worst deformed p95 deviation 0.000% (limit 0.60%)
- pass: LOD1 rest p95 deviation 0.076% (limit 1.00%)
- pass: LOD1 worst deformed p95 deviation 0.086% (limit 1.40%)
- pass: LOD2 rest p95 deviation 0.278% (limit 2.50%)
- pass: LOD2 worst deformed p95 deviation 0.311% (limit 3.00%)
- warning: LOD2 Base Color drift p95 15.8/255

## PBR

- Bộ texture URP: metallic_smoothness=`DRG_magma_Unstaged_MetallicSmoothness.png`, base_color=`DRG_magma_Unstaged_BaseColor.png`
- Base Color: image; UV chồng lấn 0.0000

## Unity

- prefab: `Assets/Dravio/Character/Prefabs/DRG_magma_Unstaged_Rigged_LODGroup.prefab`
- material: `Assets/Dravio/Character/Materials/DRG_magma_Unstaged.mat`
- baseController: `Assets/Dravio/Character/Animation/DragonBase_quadruped.controller`
- overrideController: `Assets/Dravio/Character/Animation/Overrides/DRG_magma_Unstaged.overrideController`
