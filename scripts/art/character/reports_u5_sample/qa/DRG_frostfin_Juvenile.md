# DRG_frostfin_Juvenile

- Nguồn: `SAMPLE_frostfin_Juvenile.glb` — bằng chứng: **SAMPLE**. GLB nguồn không đổi: có
- Loài / giai đoạn: frostfin / Juvenile
- Kiểm định pipeline: PASS; Animation QA: **PASS**

## Rig

- Archetype `aquatic` (locomotion lacks "walk"); limbCount 0 (canonical: no walking limbs); rig v3
- Chiều cao 0.3994 m (source size kept); pivot dời theo mặt đối xứng 0.0 m
- 17 bone (17 deform), tối đa 4 ảnh hưởng/đỉnh, 0 đỉnh không trọng số
- IK: 0 chuỗi chân; anchor: VFX_Mouth, VFX_TailTip, VFX_Back, VFX_Ground

Ghi chú rig:

- Idle blink not authored: mesh has no eyelid shape key (blink/eye_close); generated meshes rarely separate eyelids
- 112 vertices in floating fragments ignored for landmark detection
- no neck narrowing in the profile; neck base set between chest and head

## Clip

| Clip | Frame | Loop | Tốc độ (m/s) | Giãn p99.9 | Nén p0.1 | Trượt chân | Xuyên đất | Hở tiếp xúc | Khép vòng (°) | Nâng (m) | Vùng tệ nhất | Kết quả |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Idle | 120 | có | 0.000 | 1.176 | 0.849 | — | 0.0000 | — | 0.217 | 0.0091 | Spine4 48%, Tail1 41% | PASS |
| Eat | 60 | — | 0.000 | 1.207 | 0.620 | — | 0.0000 | — | — | 0.0000 | Spine1 59%, Head 30% | PASS |
| Sleep | 120 | có | 0.000 | 1.315 | 0.605 | — | 0.0000 | — | 0.217 | 0.0065 | Spine1 73%, Head 20% | PASS |
| Happy | 45 | — | 0.000 | 1.442 | 0.719 | — | 0.0000 | — | — | 0.0000 | Tail1 49%, Spine4 48% | PASS |
| Swim | 48 | có | 0.000 | 1.411 | 0.712 | — | — | — | 0.217 | 0.0000 | Fin_L 21%, Spine4 21% | PASS |

Ngưỡng (`character-budgets.json` ▸ `animationQa`, không nới): giãn ≤ 1.6, nén ≥ 0.45, trượt ≤ 0.02, xuyên ≤ 0.01, hở ≤ 0.03, khép vòng ≤ 0.5° (tương đối theo chiều cao).

## Cảnh báo QA

- không có

## LOD

| LOD | Tam giác | Đỉnh | Trong ngân sách |
|---|---|---|---|
| 0 | 24000 | 12090 | có |
| 1 | 10000 | 5090 | có |
| 2 | 4000 | 2090 | có |
- pass: LOD0 rest p95 deviation 0.000% (limit 0.40%)
- pass: LOD0 worst deformed p95 deviation 0.000% (limit 0.60%)
- pass: LOD1 rest p95 deviation 0.007% (limit 1.00%)
- pass: LOD1 worst deformed p95 deviation 0.012% (limit 1.40%)
- pass: LOD2 rest p95 deviation 0.017% (limit 2.50%)
- pass: LOD2 worst deformed p95 deviation 0.029% (limit 3.00%)

## PBR

- Bộ texture URP: metallic_smoothness=`DRG_frostfin_Juvenile_MetallicSmoothness.png`, normal=`DRG_frostfin_Juvenile_Normal.png`, base_color=`DRG_frostfin_Juvenile_BaseColor.png`
- Base Color: image; UV chồng lấn 0.0000

## Unity

- prefab: `Assets/Dravio/Character/Prefabs/DRG_frostfin_Juvenile_Rigged_LODGroup.prefab`
- material: `Assets/Dravio/Character/Materials/DRG_frostfin_Juvenile.mat`
- baseController: `Assets/Dravio/Character/Animation/DragonBase_aquatic.controller`
- overrideController: `Assets/Dravio/Character/Animation/Overrides/DRG_frostfin_Juvenile.overrideController`
