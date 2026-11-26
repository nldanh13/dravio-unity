# DRG_droplet_Baby

- Nguồn: `SAMPLE_droplet_Baby.glb` — bằng chứng: **SAMPLE**. GLB nguồn không đổi: có
- Loài / giai đoạn: droplet / Baby
- Kiểm định pipeline: PASS; Animation QA: **PASS**

## Rig

- Archetype `quadruped` (walks; limb count not stated (quadruped template once limbCount=4 is supplied)); limbCount 4 (mesh measurement accepted (--limb-count auto)); rig v3
- Chiều cao 0.9006 m (source size kept); pivot dời theo mặt đối xứng 0.0 m
- 28 bone (28 deform), tối đa 4 ảnh hưởng/đỉnh, 0 đỉnh không trọng số
- IK: 4 chuỗi chân; anchor: VFX_Mouth, VFX_TailTip, VFX_Back, VFX_Ground

Ghi chú rig:

- Idle blink not authored: mesh has no eyelid shape key (blink/eye_close); generated meshes rarely separate eyelids
- 112 vertices in floating fragments ignored for landmark detection
- no neck narrowing in the profile; neck base set between chest and head

## Clip

| Clip | Frame | Loop | Tốc độ (m/s) | Giãn p99.9 | Nén p0.1 | Trượt chân | Xuyên đất | Hở tiếp xúc | Khép vòng (°) | Nâng (m) | Vùng tệ nhất | Kết quả |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Idle | 120 | có | 0.000 | 1.141 | 0.880 | 0.0000 | 0.0001 | 0.0000 | 0.097 | 0.0000 | Pelvis 61%, Tail1 24% | PASS |
| Eat | 60 | — | 0.000 | 1.509 | 0.519 | 0.0000 | 0.0000 | 0.0000 | — | 0.0063 | Neck 53%, Head 22% | PASS |
| Sleep | 120 | có | 0.000 | 1.581 | 0.553 | 0.0000 | 0.0000 | 0.0000 | 0.088 | 0.0442 | Neck 42%, Head 27% | PASS |
| Happy | 45 | — | 0.000 | 1.440 | 0.697 | 0.0000 | 0.0004 | 0.0000 | — | 0.0000 | Pelvis 54%, Tail1 20% | PASS |
| Walk | 32 | có | 0.137 | 1.291 | 0.688 | 0.0000 | 0.0005 | 0.0000 | 0.093 | 0.0000 | Pelvis 38%, HindFoot_L 21% | PASS |
| Run | 20 | có | 0.529 | 1.534 | 0.513 | 0.0000 | 0.0000 | 0.0002 | 0.088 | 0.0100 | Pelvis 34%, HindFoot_L 28% | PASS |
| Swim | 48 | có | 0.000 | 1.494 | 0.712 | 0.0000 | — | 0.0000 | 0.088 | 0.0000 | Pelvis 56%, Tail1 21% | PASS |

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
- pass: LOD1 rest p95 deviation 0.021% (limit 1.00%)
- pass: LOD1 worst deformed p95 deviation 0.027% (limit 1.40%)
- pass: LOD2 rest p95 deviation 0.059% (limit 2.50%)
- pass: LOD2 worst deformed p95 deviation 0.071% (limit 3.00%)

## PBR

- Bộ texture URP: metallic_smoothness=`DRG_droplet_Baby_MetallicSmoothness.png`, normal=`DRG_droplet_Baby_Normal.png`, base_color=`DRG_droplet_Baby_BaseColor.png`
- Base Color: image; UV chồng lấn 0.0001

## Unity

- prefab: `Assets/Dravio/Character/Prefabs/DRG_droplet_Baby_Rigged_LODGroup.prefab`
- material: `Assets/Dravio/Character/Materials/DRG_droplet_Baby.mat`
- baseController: `Assets/Dravio/Character/Animation/DragonBase_quadruped.controller`
- overrideController: `Assets/Dravio/Character/Animation/Overrides/DRG_droplet_Baby.overrideController`
