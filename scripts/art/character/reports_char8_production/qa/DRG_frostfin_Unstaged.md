# DRG_frostfin_Unstaged

- Nguồn: `08_frostfin.glb` — bằng chứng: **thực tế (chưa nghiệm thu)**. GLB nguồn không đổi: có
- Loài / giai đoạn: frostfin / Unstaged
- Kiểm định pipeline: PASS_WITH_WARNINGS; Animation QA: **PASS_WITH_WARNINGS**

## Rig

- Archetype `aquatic` (locomotion lacks "walk"); limbCount 0 (canonical: no walking limbs); rig v7
- Chiều cao 1.7372 m (source size kept); pivot dời theo mặt đối xứng 0.0 m
- 17 bone (17 deform), tối đa 4 ảnh hưởng/đỉnh, 0 đỉnh không trọng số
- IK: 0 chuỗi chân; anchor: VFX_Mouth, VFX_TailTip, VFX_Back, VFX_Ground

Ghi chú rig:

- Idle blink not authored: mesh has no eyelid shape key (blink/eye_close); generated meshes rarely separate eyelids
- 3140 vertices in floating fragments ignored for landmark detection
- 1 off-midline ground contacts are not left/right pairs; limbs need manual landmarks
- no neck narrowing in the profile; neck base set between chest and head

## Clip

| Clip | Frame | Loop | Tốc độ (m/s) | Giãn p99.9 | Nén p0.1 | Trượt chân | Xuyên đất | Hở tiếp xúc | Khép vòng (°) | Nâng (m) | Vùng tệ nhất | Kết quả |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Idle | 120 | có | 0.000 | 1.621 | 0.807 | — | 0.0000 | — | 0.125 | 0.0116 | Spine4 40%, Tail1 26% | WARNING |
| Eat | 60 | — | 0.000 | 1.602 | 0.808 | — | 0.0000 | — | — | 0.0000 | Spine1 35%, Spine4 22% | WARNING |
| Sleep | 120 | có | 0.000 | 2.666 | 0.675 | — | 0.0000 | — | 0.125 | 0.0056 | Spine4 37%, Tail1 27% | WARNING |
| Happy | 45 | — | 0.000 | 3.095 | 0.711 | — | 0.0000 | — | — | 0.0000 | Spine4 39%, Tail1 27% | WARNING |
| Swim | 48 | có | 0.173 | 2.053 | 0.600 | — | — | — | 0.125 | 0.0000 | Spine3 18%, Spine4 17% | WARNING |
| Rest | 120 | có | 0.000 | 1.828 | 0.603 | — | 0.0000 | — | 0.125 | 0.0084 | Spine4 31%, Spine1 26% | WARNING |
| Wake | 60 | — | 0.000 | 1.763 | 0.773 | — | 0.0000 | — | — | 0.0000 | Spine4 37%, Tail1 27% | WARNING |
| LookAround | 90 | — | 0.000 | 1.521 | 0.736 | — | 0.0000 | — | — | 0.0000 | Spine1 34%, Neck 26% | PASS |
| Curious | 75 | — | 0.000 | 1.625 | 0.888 | — | 0.0000 | — | — | 0.0273 | Spine4 34%, Tail1 22% | WARNING |
| Greet | 60 | — | 0.000 | 1.458 | 0.855 | — | 0.0000 | — | — | 0.0000 | Spine4 40%, Tail1 27% | PASS |
| SwimIdle | 72 | có | 0.000 | 1.287 | 0.807 | — | — | — | 0.125 | 0.0000 | Spine3 18%, Spine4 18% | PASS |
| SwimRest | 120 | có | 0.000 | 1.181 | 0.897 | — | — | — | 0.125 | 0.0000 | Spine1 26%, Tail2 22% | PASS |
| Nuzzle | 75 | — | 0.000 | 1.273 | 0.897 | — | 0.0000 | — | — | 0.0000 | Spine4 31%, Spine1 28% | PASS |

Ngưỡng (`character-budgets.json` ▸ `animationQa`, không nới): giãn ≤ 1.6, nén ≥ 0.45, trượt ≤ 0.02, xuyên ≤ 0.01, hở ≤ 0.03, khép vòng ≤ 0.5° (tương đối theo chiều cao).

## Cảnh báo QA

- model: bones — bone orientation / mirror issue ({'zeroLength': [], 'mirrorErrorRel': 0.37924, 'chainDirection': []} / {'mirrorErrorRel': 0.01})
- Idle: deformation — edge stretch over limit (1.621 / 1.6)
- Eat: deformation — edge stretch over limit (1.602 / 1.6)
- Sleep: deformation — edge stretch over limit (2.666 / 1.6)
- Happy: deformation — edge stretch over limit (3.095 / 1.6)
- Swim: deformation — edge stretch over limit (2.053 / 1.6)
- Rest: deformation — edge stretch over limit (1.828 / 1.6)
- Wake: deformation — edge stretch over limit (1.763 / 1.6)
- Curious: deformation — edge stretch over limit (1.625 / 1.6)

## LOD

| LOD | Tam giác | Đỉnh | Trong ngân sách |
|---|---|---|---|
| 0 | 24000 | 16674 | có |
| 1 | 9999 | 9371 | có |
| 2 | 3999 | 5360 | có |
- warning: DRG_frostfin_Unstaged_LOD2: 2206 UV faces (55.16%) flipped_faces > 1.0%
- warning: DRG_frostfin_Unstaged_LOD2: 693 loose vertices
- warning: DRG_frostfin_Unstaged_LOD2: 3565 non-manifold/boundary edges after weld
- warning: DRG_frostfin_Unstaged_LOD0: 13853 UV faces (57.72%) flipped_faces > 1.0%
- warning: DRG_frostfin_Unstaged_LOD1: 5644 UV faces (56.45%) flipped_faces > 1.0%
- warning: DRG_frostfin_Unstaged_LOD1: 5 loose vertices
- warning: DRG_frostfin_Unstaged_LOD1: 3449 non-manifold/boundary edges after weld
- warning: Image_0: 2048x2048 > recommended 1024
- pass: LOD0 rest p95 deviation 0.000% (limit 0.40%)
- pass: LOD0 worst deformed p95 deviation 0.000% (limit 0.60%)
- pass: LOD1 rest p95 deviation 0.040% (limit 1.00%)
- pass: LOD1 worst deformed p95 deviation 0.044% (limit 1.40%)
- pass: LOD2 rest p95 deviation 0.172% (limit 2.50%)
- pass: LOD2 worst deformed p95 deviation 0.187% (limit 3.00%)
- warning: LOD2 Base Color drift p95 12.8/255

## PBR

- Bộ texture URP: metallic_smoothness=`DRG_frostfin_Unstaged_MetallicSmoothness.png`, base_color=`DRG_frostfin_Unstaged_BaseColor.png`
- Base Color: image; UV chồng lấn 0.0000

## Unity

- prefab: `Assets/Dravio/Character/Prefabs/DRG_frostfin_Unstaged_Rigged_LODGroup.prefab`
- material: `Assets/Dravio/Character/Materials/DRG_frostfin_Unstaged.mat`
- baseController: `Assets/Dravio/Character/Animation/DragonBase_aquatic.controller`
- overrideController: `Assets/Dravio/Character/Animation/Overrides/DRG_frostfin_Unstaged.overrideController`
