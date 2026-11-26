# DRG_leviathan_Unstaged

- Nguồn: `12_leviathan.glb` — bằng chứng: **thực tế (chưa nghiệm thu)**. GLB nguồn không đổi: có
- Loài / giai đoạn: leviathan / Unstaged
- Kiểm định pipeline: PASS_WITH_WARNINGS; Animation QA: **PASS_WITH_WARNINGS**

## Rig

- Archetype `serpentine` (bodyStyle == Serpent); limbCount 0 (canonical: no walking limbs); rig v7
- Chiều cao 1.9885 m (source size kept); pivot dời theo mặt đối xứng 0.025 m
- 19 bone (19 deform), tối đa 4 ảnh hưởng/đỉnh, 0 đỉnh không trọng số
- IK: 0 chuỗi chân; anchor: VFX_Mouth, VFX_TailTip, VFX_Back, VFX_Ground

Ghi chú rig:

- Idle blink not authored: mesh has no eyelid shape key (blink/eye_close); generated meshes rarely separate eyelids
- 4163 vertices in floating fragments ignored for landmark detection
- contact cluster at y=-0.365 split at x=0.015 (empty band 0.069 m): left/right paws standing close together
- no neck narrowing in the profile; neck base set between chest and head

## Clip

| Clip | Frame | Loop | Tốc độ (m/s) | Giãn p99.9 | Nén p0.1 | Trượt chân | Xuyên đất | Hở tiếp xúc | Khép vòng (°) | Nâng (m) | Vùng tệ nhất | Kết quả |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Idle | 120 | có | 0.000 | 2.139 | 0.659 | — | 0.0000 | — | 0.000 | 0.0047 | Spine8 37%, Tail1 29% | WARNING |
| Eat | 60 | — | 0.000 | 2.357 | 0.753 | — | 0.0000 | — | — | 0.0000 | Head 31%, Spine8 23% | WARNING |
| Sleep | 120 | có | 0.000 | 8.475 | 0.368 | — | 0.0009 | — | 0.000 | 0.0000 | Spine8 35%, Tail3 24% | WARNING |
| Happy | 45 | — | 0.000 | 4.694 | 0.595 | — | 0.0000 | — | — | 0.0000 | Spine8 37%, Tail1 28% | WARNING |
| Swim | 48 | có | 0.398 | 1.919 | 0.614 | — | — | — | 0.000 | 0.0000 | Spine8 48%, Tail1 26% | WARNING |
| Rest | 120 | có | 0.000 | 2.400 | 0.470 | — | 0.0010 | — | 0.000 | 0.0000 | Spine8 41%, Tail1 21% | WARNING |
| Wake | 60 | — | 0.000 | 4.889 | 0.453 | — | 0.0000 | — | — | 0.0000 | Spine8 37%, Tail1 24% | WARNING |
| LookAround | 90 | — | 0.000 | 2.606 | 0.658 | — | 0.0000 | — | — | 0.0000 | Head 57%, Spine4 12% | WARNING |
| Curious | 75 | — | 0.000 | 1.511 | 0.709 | — | 0.0000 | — | — | 0.0313 | Tail1 26%, Spine8 25% | PASS |
| Greet | 60 | — | 0.000 | 1.748 | 0.741 | — | 0.0000 | — | — | 0.0000 | Spine8 40%, Tail1 35% | WARNING |
| SwimIdle | 72 | có | 0.000 | 1.273 | 0.797 | — | — | — | 0.000 | 0.0000 | Spine8 49%, Tail1 24% | PASS |
| SwimRest | 120 | có | 0.000 | 1.228 | 0.902 | — | — | — | 0.000 | 0.0000 | Spine8 33%, Head 26% | PASS |
| Nuzzle | 75 | — | 0.000 | 1.489 | 0.841 | — | 0.0000 | — | — | 0.0000 | Spine8 32%, Tail1 29% | PASS |

Ngưỡng (`character-budgets.json` ▸ `animationQa`, không nới): giãn ≤ 1.6, nén ≥ 0.45, trượt ≤ 0.02, xuyên ≤ 0.01, hở ≤ 0.03, khép vòng ≤ 0.5° (tương đối theo chiều cao).

## Cảnh báo QA

- Idle: deformation — edge stretch over limit (2.139 / 1.6)
- Eat: deformation — edge stretch over limit (2.357 / 1.6)
- Sleep: deformation — edge stretch over limit (8.475 / 1.6)
- Sleep: deformation — edge compression over limit (0.368 / 0.45)
- Happy: deformation — edge stretch over limit (4.694 / 1.6)
- Swim: deformation — edge stretch over limit (1.919 / 1.6)
- Rest: deformation — edge stretch over limit (2.4 / 1.6)
- Wake: deformation — edge stretch over limit (4.889 / 1.6)
- LookAround: deformation — edge stretch over limit (2.606 / 1.6)
- Greet: deformation — edge stretch over limit (1.748 / 1.6)

## LOD

| LOD | Tam giác | Đỉnh | Trong ngân sách |
|---|---|---|---|
| 0 | 24000 | 17922 | có |
| 1 | 9999 | 10470 | có |
| 2 | 4000 | 5872 | có |
- warning: DRG_leviathan_Unstaged_LOD2: 1805 UV faces (45.12%) flipped_faces > 1.0%
- warning: DRG_leviathan_Unstaged_LOD2: 2 UV faces (0.05%) faces_outside_0_1 > 0.0%
- warning: DRG_leviathan_Unstaged_LOD2: 1483 loose vertices
- warning: DRG_leviathan_Unstaged_LOD2: 3686 non-manifold/boundary edges after weld
- warning: DRG_leviathan_Unstaged_LOD0: 10567 UV faces (44.03%) flipped_faces > 1.0%
- warning: DRG_leviathan_Unstaged_LOD1: 4520 UV faces (45.20%) flipped_faces > 1.0%
- warning: DRG_leviathan_Unstaged_LOD1: 2 UV faces (0.02%) faces_outside_0_1 > 0.0%
- warning: DRG_leviathan_Unstaged_LOD1: 1 loose vertices
- warning: DRG_leviathan_Unstaged_LOD1: 4841 non-manifold/boundary edges after weld
- warning: Image_0: 2048x2048 > recommended 1024
- pass: LOD0 rest p95 deviation 0.000% (limit 0.40%)
- pass: LOD0 worst deformed p95 deviation 0.000% (limit 0.60%)
- pass: LOD1 rest p95 deviation 0.067% (limit 1.00%)
- pass: LOD1 worst deformed p95 deviation 0.085% (limit 1.40%)
- pass: LOD2 rest p95 deviation 0.273% (limit 2.50%)
- pass: LOD2 worst deformed p95 deviation 0.320% (limit 3.00%)
- warning: LOD2 Base Color drift p95 19.4/255

## PBR

- Bộ texture URP: metallic_smoothness=`DRG_leviathan_Unstaged_MetallicSmoothness.png`, base_color=`DRG_leviathan_Unstaged_BaseColor.png`
- Base Color: image; UV chồng lấn 0.0000

## Unity

- prefab: `Assets/Dravio/Character/Prefabs/DRG_leviathan_Unstaged_Rigged_LODGroup.prefab`
- material: `Assets/Dravio/Character/Materials/DRG_leviathan_Unstaged.mat`
- baseController: `Assets/Dravio/Character/Animation/DragonBase_serpentine.controller`
- overrideController: `Assets/Dravio/Character/Animation/Overrides/DRG_leviathan_Unstaged.overrideController`
