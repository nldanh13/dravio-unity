# DRG_leviathan_Adult

- Nguồn: `SAMPLE_leviathan_Adult.glb` — bằng chứng: **SAMPLE**. GLB nguồn không đổi: có
- Loài / giai đoạn: leviathan / Adult
- Kiểm định pipeline: PASS; Animation QA: **PASS**

## Rig

- Archetype `serpentine` (bodyStyle == Serpent); limbCount 0 (canonical: no walking limbs); rig v4
- Chiều cao 0.337 m (source size kept); pivot dời theo mặt đối xứng 0.0 m
- 19 bone (19 deform), tối đa 4 ảnh hưởng/đỉnh, 0 đỉnh không trọng số
- IK: 0 chuỗi chân; anchor: VFX_Mouth, VFX_TailTip, VFX_Back, VFX_Ground

Ghi chú rig:

- Idle blink not authored: mesh has no eyelid shape key (blink/eye_close); generated meshes rarely separate eyelids
- 112 vertices in floating fragments ignored for landmark detection
- no neck narrowing in the profile; neck base set between chest and head

## Clip

| Clip | Frame | Loop | Tốc độ (m/s) | Giãn p99.9 | Nén p0.1 | Trượt chân | Xuyên đất | Hở tiếp xúc | Khép vòng (°) | Nâng (m) | Vùng tệ nhất | Kết quả |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Idle | 120 | có | 0.000 | 1.169 | 0.852 | — | 0.0000 | — | 0.028 | 0.0012 | Spine8 45%, Tail1 26% | PASS |
| Eat | 60 | — | 0.000 | 1.201 | 0.705 | — | 0.0000 | — | — | 0.1074 | Spine1 43%, Neck 43% | PASS |
| Sleep | 120 | có | 0.000 | 1.438 | 0.533 | — | 0.0000 | — | 0.028 | 0.0703 | Tail3 39%, Tail2 21% | PASS |
| Happy | 45 | — | 0.000 | 1.463 | 0.710 | — | 0.0000 | — | — | 0.0061 | Spine8 45%, Tail3 27% | PASS |
| Swim | 48 | có | 0.000 | 1.152 | 0.864 | — | — | — | 0.028 | 0.0000 | Spine8 34%, Spine6 24% | PASS |
| Rest | 120 | có | 0.000 | 1.146 | 0.772 | — | 0.0000 | — | 0.000 | 0.0024 | Spine1 24%, Tail3 22% | PASS |
| Wake | 60 | — | 0.000 | 1.272 | 0.639 | — | 0.0000 | — | — | 0.0000 | Tail3 32%, Spine8 20% | PASS |
| LookAround | 90 | — | 0.000 | 1.250 | 0.779 | — | 0.0000 | — | — | 0.0000 | Spine1 45%, Neck 43% | PASS |
| Curious | 75 | — | 0.000 | 1.167 | 0.867 | — | 0.0000 | — | — | 0.0000 | Spine8 51%, Tail1 22% | PASS |

Ngưỡng (`character-budgets.json` ▸ `animationQa`, không nới): giãn ≤ 1.6, nén ≥ 0.45, trượt ≤ 0.02, xuyên ≤ 0.01, hở ≤ 0.03, khép vòng ≤ 0.5° (tương đối theo chiều cao).

## Cảnh báo QA

- không có

## LOD

| LOD | Tam giác | Đỉnh | Trong ngân sách |
|---|---|---|---|
| 0 | 23998 | 12089 | có |
| 1 | 10000 | 5090 | có |
| 2 | 4000 | 2090 | có |
- pass: LOD0 rest p95 deviation 0.000% (limit 0.40%)
- pass: LOD0 worst deformed p95 deviation 0.000% (limit 0.60%)
- pass: LOD1 rest p95 deviation 0.016% (limit 1.00%)
- pass: LOD1 worst deformed p95 deviation 0.018% (limit 1.40%)
- pass: LOD2 rest p95 deviation 0.044% (limit 2.50%)
- pass: LOD2 worst deformed p95 deviation 0.051% (limit 3.00%)

## PBR

- Bộ texture URP: metallic_smoothness=`DRG_leviathan_Adult_MetallicSmoothness.png`, normal=`DRG_leviathan_Adult_Normal.png`, base_color=`DRG_leviathan_Adult_BaseColor.png`
- Base Color: image; UV chồng lấn 0.0000

## Unity

- prefab: `Assets/Dravio/Character/Prefabs/DRG_leviathan_Adult_Rigged_LODGroup.prefab`
- material: `Assets/Dravio/Character/Materials/DRG_leviathan_Adult.mat`
- baseController: `Assets/Dravio/Character/Animation/DragonBase_serpentine.controller`
- overrideController: `Assets/Dravio/Character/Animation/Overrides/DRG_leviathan_Adult.overrideController`
