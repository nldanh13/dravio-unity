# DRG_amberhorn_Unstaged

- Nguồn: `13_amberhorn.glb` — bằng chứng: **thực tế (chưa nghiệm thu)**. GLB nguồn không đổi: có
- Loài / giai đoạn: amberhorn / Unstaged
- Kiểm định pipeline: PASS_WITH_WARNINGS; Animation QA: **PASS_WITH_WARNINGS**

## Rig

- Archetype `quadruped` (walks; silhouette states "bốn chân"); limbCount 4 (silhouetteVi); rig v7
- Chiều cao 1.197 m (source size kept); pivot dời theo mặt đối xứng 0.0 m
- 26 bone (26 deform), tối đa 4 ảnh hưởng/đỉnh, 0 đỉnh không trọng số
- IK: 4 chuỗi chân; anchor: VFX_Mouth, VFX_TailTip, VFX_Back, VFX_Ground

Ghi chú rig:

- Idle blink not authored: mesh has no eyelid shape key (blink/eye_close); generated meshes rarely separate eyelids
- 2275 vertices in floating fragments ignored for landmark detection
- contact cluster at y=-0.522 split at x=0.000 (empty band 0.064 m): left/right paws standing close together
- no neck narrowing in the profile; neck base set between chest and head

## Clip

| Clip | Frame | Loop | Tốc độ (m/s) | Giãn p99.9 | Nén p0.1 | Trượt chân | Xuyên đất | Hở tiếp xúc | Khép vòng (°) | Nâng (m) | Vùng tệ nhất | Kết quả |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Idle | 120 | có | 0.000 | 1.220 | 0.851 | 0.0000 | 0.0019 | 0.0000 | 0.128 | 0.0000 | Tail1 31%, Pelvis 25% | PASS |
| Eat | 60 | — | 0.000 | 2.368 | 0.507 | 0.0000 | 0.0000 | 0.0000 | — | 0.0386 | Neck 18%, Pelvis 13% | WARNING |
| Sleep | 120 | có | 0.000 | 3.333 | 0.559 | 0.0000 | 0.0000 | 0.0000 | 0.122 | 0.0811 | HindUpper_R 22%, Tail1 14% | WARNING |
| Happy | 45 | — | 0.000 | 1.718 | 0.634 | 0.0000 | 0.0000 | 0.0000 | — | 0.0185 | Tail1 29%, Pelvis 23% | WARNING |
| Walk | 40 | có | 0.095 | 2.025 | 0.586 | 0.0004 | 0.0000 | 0.0013 | 0.128 | 0.0162 | Spine 34%, HindUpper_L 12% | WARNING |
| Run | 22 | có | 0.416 | 3.110 | 0.445 | 0.0000 | 0.0000 | 0.0000 | 0.122 | 0.0469 | Spine 33%, Pelvis 14% | WARNING |
| Burrow | 60 | có | 0.000 | 3.394 | 0.411 | 0.0000 | 0.0000 | 0.0000 | 0.125 | 0.0738 | Spine 23%, Pelvis 16% | WARNING |
| Rest | 120 | có | 0.000 | 3.036 | 0.531 | 0.0000 | 0.0000 | 0.0000 | 0.125 | 0.0831 | Spine 19%, HindUpper_R 15% | WARNING |
| Wake | 60 | — | 0.000 | 2.505 | 0.603 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Spine 19%, HindUpper_R 14% | WARNING |
| LookAround | 90 | — | 0.000 | 1.222 | 0.823 | 0.0000 | 0.0000 | 0.0000 | — | 0.0032 | Neck 42%, Head 26% | PASS |
| Curious | 75 | — | 0.000 | 1.399 | 0.653 | 0.0000 | 0.0000 | 0.0000 | — | 0.0211 | Spine 21%, Pelvis 18% | PASS |
| Drink | 75 | — | 0.000 | 2.555 | 0.482 | 0.0000 | 0.0000 | 0.0000 | — | 0.0400 | Neck 19%, Pelvis 14% | WARNING |
| Stretch | 90 | — | 0.000 | 2.129 | 0.664 | 0.0000 | 0.0000 | 0.0000 | — | 0.0605 | Spine 26%, Pelvis 19% | WARNING |
| Bask | 120 | có | 0.000 | 3.014 | 0.526 | 0.0000 | 0.0000 | 0.0000 | 0.125 | 0.0831 | Spine 20%, HindLower_R 14% | WARNING |
| Greet | 60 | — | 0.000 | 1.229 | 0.864 | 0.0000 | 0.0000 | 0.0000 | — | 0.0000 | Pelvis 33%, Tail1 30% | PASS |
| DigPrep | 45 | — | 0.000 | 1.298 | 0.783 | 0.0009 | 0.0115 | 0.0008 | — | 0.0000 | Spine 42%, Neck 11% | WARNING |
| BurrowEnter | 60 | — | 0.000 | 1.728 | 0.687 | 0.0000 | 0.0204 | 0.0000 | — | 0.0000 | Spine 33%, Neck 14% | WARNING |
| BurrowEmerge | 60 | — | 0.000 | 1.482 | 0.719 | 0.0000 | 0.0204 | 0.0000 | — | 0.0000 | Spine 28%, Neck 16% | WARNING |
| Nuzzle | 75 | — | 0.000 | 1.164 | 0.916 | 0.0000 | 0.0000 | 0.0000 | — | 0.0032 | Neck 34%, Tail1 16% | PASS |

Ngưỡng (`character-budgets.json` ▸ `animationQa`, không nới): giãn ≤ 1.6, nén ≥ 0.45, trượt ≤ 0.02, xuyên ≤ 0.01, hở ≤ 0.03, khép vòng ≤ 0.5° (tương đối theo chiều cao).

## Cảnh báo QA

- Eat: deformation — edge stretch over limit (2.368 / 1.6)
- Sleep: deformation — edge stretch over limit (3.333 / 1.6)
- Happy: deformation — edge stretch over limit (1.718 / 1.6)
- Walk: deformation — edge stretch over limit (2.025 / 1.6)
- Run: deformation — edge stretch over limit (3.11 / 1.6)
- Run: deformation — edge compression over limit (0.445 / 0.45)
- Burrow: deformation — edge stretch over limit (3.394 / 1.6)
- Burrow: deformation — edge compression over limit (0.411 / 0.45)
- Rest: deformation — edge stretch over limit (3.036 / 1.6)
- Wake: deformation — edge stretch over limit (2.505 / 1.6)
- Drink: deformation — edge stretch over limit (2.555 / 1.6)
- Stretch: deformation — edge stretch over limit (2.129 / 1.6)
- Bask: deformation — edge stretch over limit (3.014 / 1.6)
- DigPrep: ground — mesh below the ground plane (0.0115 / 0.01)
- BurrowEnter: deformation — edge stretch over limit (1.728 / 1.6)
- BurrowEnter: ground — mesh below the ground plane (0.0204 / 0.01)
- BurrowEmerge: ground — mesh below the ground plane (0.0204 / 0.01)

## LOD

| LOD | Tam giác | Đỉnh | Trong ngân sách |
|---|---|---|---|
| 0 | 24000 | 15873 | có |
| 1 | 10000 | 8575 | có |
| 2 | 4000 | 4655 | có |
- warning: DRG_amberhorn_Unstaged_LOD1: 4622 UV faces (46.22%) flipped_faces > 1.0%
- warning: DRG_amberhorn_Unstaged_LOD1: 3 loose vertices
- warning: DRG_amberhorn_Unstaged_LOD1: 3330 non-manifold/boundary edges after weld
- warning: DRG_amberhorn_Unstaged_LOD2: 1826 UV faces (45.65%) flipped_faces > 1.0%
- warning: DRG_amberhorn_Unstaged_LOD2: 460 loose vertices
- warning: DRG_amberhorn_Unstaged_LOD2: 3330 non-manifold/boundary edges after weld
- warning: DRG_amberhorn_Unstaged_LOD0: 10907 UV faces (45.45%) flipped_faces > 1.0%
- warning: Image_0: 2048x2048 > recommended 1024
- pass: LOD0 rest p95 deviation 0.000% (limit 0.40%)
- pass: LOD0 worst deformed p95 deviation 0.000% (limit 0.60%)
- pass: LOD1 rest p95 deviation 0.060% (limit 1.00%)
- pass: LOD1 worst deformed p95 deviation 0.070% (limit 1.40%)
- pass: LOD2 rest p95 deviation 0.218% (limit 2.50%)
- pass: LOD2 worst deformed p95 deviation 0.258% (limit 3.00%)
- warning: LOD2 Base Color drift p95 17.2/255

## PBR

- Bộ texture URP: metallic_smoothness=`DRG_amberhorn_Unstaged_MetallicSmoothness.png`, base_color=`DRG_amberhorn_Unstaged_BaseColor.png`
- Base Color: image; UV chồng lấn 0.0000

## Unity

- prefab: `Assets/Dravio/Character/Prefabs/DRG_amberhorn_Unstaged_Rigged_LODGroup.prefab`
- material: `Assets/Dravio/Character/Materials/DRG_amberhorn_Unstaged.mat`
- baseController: `Assets/Dravio/Character/Animation/DragonBase_quadruped.controller`
- overrideController: `Assets/Dravio/Character/Animation/Overrides/DRG_amberhorn_Unstaged.overrideController`
