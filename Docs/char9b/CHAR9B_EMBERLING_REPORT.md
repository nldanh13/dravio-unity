# CHAR.9b — Emberling vertical slice (GLB thật → rig → 5 clip → FBX hợp đồng Unity)

Issue: https://github.com/nldanh13/dravio-unity/issues/76. Nhánh `feat/char-9b-emberling-vertical-slice` (từ `main` 665229e). Sản xuất hàng loạt 48 biến thể **tạm dừng**; loài khác chưa áp dụng (xem §6).

## 0. Trạng thái (tách bạch nơi đã kiểm)

| Mức | Kết quả |
|---|---|
| **Blender verified** | **ANIMATION_QA_PASS**: rig v7 + sửa CHAR.9b, 15/15 clip PASS, 0 cảnh báo; FBX hợp đồng 5/5 clip PASS khi xuất **và** khi import lại FBX vào Blender sạch. Skinning tuyến tính (như Unity). Ngưỡng không đổi. |
| Unity verified | **NOT RUN** (không có Unity trong môi trường này). Chưa import, chưa chạy `EmberlingAssetAudit`, chưa sinh scene. |
| Android verified | **NOT RUN**. |
| APPROVED | Chỉ chủ dự án. Không đánh dấu. |

## 1. Phát hiện quan trọng: Blender QA cũ đo sai kiểu skinning

`rig_archetype.py` (rig v7) bật **Preserve Volume** (dual quaternion) trên Armature modifier cho **mọi loài**. Unity `SkinnedMeshRenderer` và FBX skin **tuyến tính** (linear blend). Vì vậy mọi số `anim_qa` từ CHAR.8 / CHAR.9 (16 loài) đo một kiểu skinning Unity không chạy.

Phát hiện khi import lại FBX: cùng ma trận skin (sai khác 0,0000), cùng trọng số (sai khác 0), cùng đỉnh, chỉ khác Preserve Volume. Trên Emberling đã sửa (trước khi chỉnh đầu gối): Sleep nén 0,455 (dual quaternion) → **0,424 (tuyến tính)**, Rest / Bask 0,479 → 0,435.

Đã làm:
- `repair_chibi_rig.py` có `"skinning": "linear"` (Emberling bật) → tắt Preserve Volume, mọi số Blender sau đó là skinning Unity.
- `anim_qa.py` ghi `"skinning": "linear" | "dualQuaternion"` vào mỗi báo cáo, để không ai nhầm PASS của Blender với Unity.
- **Chưa** đổi mặc định cho 15 loài còn lại (issue: hoãn loài khác). Khi áp dụng pipeline cho loài khác phải đo lại với `skinning: linear`; số CHAR.8/9 của chúng là dual quaternion.

## 2. Sửa rig / animation (P0)

`scripts/art/character/repair_chibi_rig.py`, bật qua `asset-overrides.char8-production.json` → `species.emberling.repair` (bước mới trong `batch_import.py` ngay sau `rig_archetype`, `PIPELINE_VERSION u1-char9b-v1`; lỗi bước này giữ rig chưa sửa và ghi chú).

1. **Skinning** tuyến tính (§1).
2. **Neck / Head**: đầu chibi bị gán một phần cho Neck → chia theo khoảng cách tới hai đoạn xương (sigmoid 15 mm).
3. **Phân vùng chân–thân / Pelvis** (nguyên nhân 67 % độ giãn): tìm mọi cạnh có trọng số đổi nhanh hơn 2 / blendWidth mỗi mét; mọi đỉnh trong **khoảng cách trắc địa** (Dijkstra trên lưới, mét) ≤ 0,12 m được khuếch tán Laplace theo độ dài cạnh, chỉ trong các xương đã có lân cận; tối đa 4 ảnh hưởng, chuẩn hóa.
4. **Biên độ theo giải phẫu chibi** (chân ngắn, không cổ): xoay Neck+Head ×0,6 ở Eat / Sleep / Rest / Bask / Drink; cả chuỗi chân ×0,72 ở Sleep / Rest / Bask, thêm **đầu gối (Lower_*) ×0,7** ở ba clip nằm (đầu gối sập dưới skinning tuyến tính); Walk ×0,9, Run ×0,8 kèm `groundSpeed` giảm cùng hệ số (PlaybackRate = speed / groundSpeed vẫn khóa chân). Wake chạy dốc từ hệ số Sleep về 1 nên Sleep → Wake → Idle vẫn khép.
5. Không đổi GLB nguồn, bộ xương, khoảng frame clip, ngưỡng QA.

### Kết quả 15 clip rig v7 (Blender, tuyến tính) — `reports_char9b/batch_DRG_emberling_Unstaged_animqa.json`

Giãn p99,9 / nén p0,1 (giới hạn ≤ 1,6 / ≥ 0,45). "Trước" = CHAR.8 production (dual quaternion, chưa sửa).

| Clip | Trước | Sau | Trượt chân | Xuyên đất | |
|---|---|---|---|---|---|
| Idle | 1,161 / 0,877 | **1,087 / 0,926** | 0,0000 | 0,0008 | PASS |
| Eat | 2,720 / 0,438 | **1,409 / 0,494** | 0,0000 | 0,0000 | PASS |
| Sleep | 3,321 / 0,425 | **1,317 / 0,543** | 0,0015 | 0,0082 | PASS |
| Happy | 1,306 / 0,674 | **1,161 / 0,726** | 0,0000 | 0,0000 | PASS |
| Walk | 2,175 / 0,496 | **1,335 / 0,612** | 0,0012 | 0,0000 | PASS |
| Run | 3,439 / 0,418 | **1,556 / 0,457** | 0,0025 | 0,0017 | PASS |
| Rest | 3,251 / 0,435 | **1,226 / 0,560** | 0,0022 | 0,0084 | PASS |
| Wake | 2,480 / 0,454 | **1,228 / 0,561** | 0,0000 | 0,0054 | PASS |
| LookAround | 1,376 / 0,699 | **1,337 / 0,716** | 0,0000 | 0,0004 | PASS |
| Curious | 1,516 / 0,645 | **1,151 / 0,747** | 0,0000 | 0,0000 | PASS |
| Drink | 3,160 / 0,430 | **1,478 / 0,469** | 0,0000 | 0,0000 | PASS |
| Stretch | 2,735 / 0,550 | **1,381 / 0,595** | 0,0000 | 0,0000 | PASS |
| Bask | 3,251 / 0,435 | **1,226 / 0,560** | 0,0022 | 0,0084 | PASS |
| Greet | 1,121 / 0,914 | **1,097 / 0,931** | 0,0000 | 0,0000 | PASS |
| Nuzzle | 1,197 / 0,876 | **1,134 / 0,907** | 0,0000 | 0,0005 | PASS |

Chuyển tiếp: Sleep.start → Wake.start 0,056°, Wake.end → Idle.start 0,056° (≤ 0,5°). Trạng thái tổng: **PASS**, 0 cảnh báo. Validation mesh (`batch_…validation.json`): PASS_WITH_WARNINGS (cảnh báo mesh có từ CHAR.8: UV lật ~41 %, LOD1/2 không manifold sau decimate, LOD2 487 đỉnh rời, texture 2048, LOD2 lệch màu 10,1/255 — không thuộc rig/animation).

## 3. FBX hợp đồng Unity (P0) — `Assets/Dravio/Art/Models/EmberlingRefinedGLB.fbx`

Giữ hợp đồng 22 bone / 5 clip của `EmberlingAssetAudit` / `EmberlingModelPipeline` (đã có trên `main`): `export_emberling_legacy_contract.py` đổi tên xương rig v7 (ForeUpper → Fore, HindUpper → Hind, Tail3 → Flame, VFX_TailTip → TailVFX_Anchor), thêm Eye_L/R, Wing_L/R không trọng số, ghép một take 1–445 frame, nhúng BaseColor 2048². Mesh `Emberling_Baby_Body_GLB`, 24 000 tam giác, UV0, tối đa 4 ảnh hưởng, 0 đỉnh không trọng số, 30 bone (22 hợp đồng + TailRoot, ForeFoot_L/R, HindFoot_L/R, VFX_Back, VFX_Ground, VFX_Mouth của rig v7).

Kiểm lặp xuất → import: xuất FBX, rồi import FBX vào Blender trống (bộ import FBX lệch take +1 frame, đã bù), chạy lại `anim_qa` với `EmberlingRefinedGLB_contract_rig.json` (contact / groundSpeed co giãn theo thời lượng clip).

| Clip hợp đồng | Nguồn rig v7 | Frame | Xuất: giãn / nén | Import lại: giãn / nén | Trượt chân | Xuyên đất | |
|---|---|---|---|---|---|---|---|
| Idle | Idle (120 → 180 frame) | 1–181 | 1,086 / 0,927 | 1,086 / 0,927 | 0,0000 | 0,0008 | PASS |
| Walk | Walk (25 → 35 frame) | 182–217 | 1,330 / 0,607 | 1,330 / 0,607 | 0,0029 | 0,0000 | PASS |
| Eat | Eat | 218–278 | 1,409 / 0,494 | 1,409 / 0,494 | 0,0000 | 0,0000 | PASS |
| Doze | Sleep | 279–399 | 1,317 / 0,543 | 1,317 / 0,543 | 0,0015 | 0,0082 | PASS |
| Happy | Happy | 400–445 | 1,161 / 0,726 | 1,161 / 0,726 | 0,0000 | 0,0000 | PASS |

Hình: `reports_char9b/{batch,export,reimport}_*_animqa.jpg` (frame tệ nhất mỗi clip, đỏ = giãn, xanh = nén). Doze là dáng **cúi thấp nằm co**, không nằm bẹp hẳn (biên độ chân đã giảm) — cần chủ dự án / animator xem.

## 4. Bàn giao cho AI 1 (Unity)

| File | Vai trò | SHA-256 |
|---|---|---|
| `Assets/Dravio/Art/Models/EmberlingRefinedGLB.fbx` | `0e27862d4ceb017d5f884c5a240ee043f514a37b071cb3925466982b920710ab` |
| `ArtSource/Dragons/images/01_emberling.glb` | `42bffef7b055d6841e595fcc89840efa83bf3d8553a7edd0b90293f22b7a546f` |
| `Assets/Dravio/Character/Data/asset-overrides.char8-production.json` | `6f06338fcce506008d67cb0ccf0a98a9d6d1b080c9c6f26ca3473ac15934570d` |
| `Assets/Dravio/Character/Data/character-budgets.json` | `cc80726e1534d77b36eafa27c5583d13151e1cdaf04347bc115bce8ce026011f` |
| `scripts/art/character/repair_chibi_rig.py` | `06943c32d5fefc48fed214411a06dd92641a5bc919fa9e25a622fdd8f903fadb` |
| `scripts/art/character/export_emberling_legacy_contract.py` | `4f8449a5e5528d50d62bb908d41542763e8bc00728d5852023014344be8dd4a8` |
| `scripts/art/character/batch_import.py` | `6acac4e052c4b3934ab913f2ab1e78459322ffd7f7480781bc93a6793a051f06` |
| `scripts/art/character/rig_archetype.py` | `bb0067c81605c192ad29e318f3f69045bcbb6f122ef2a6444b850598ea0c1252` |
| `scripts/art/character/anim_qa.py` | `46759a9cffbd38394a2c72731614032d267fd770331abdf7eb15075bbe0e105e` |
| `Assets/Dravio/Editor/EmberlingModelPipeline.cs` | `ba6f2804167d81f9935fdb8bf3dc019d7fbe66808e529f74e9ce3da3e6489d92` |
| `Assets/Dravio/Editor/EmberlingAssetAudit.cs` | `f3a15248b115e0f2eb3e91601bf831ef5ddb3656c07c2ca3b3816f06e51b8cd4` |

- **Prefab, AnimatorController, catalog**: không commit sẵn — `EmberlingModelPipeline` sinh trong Unity: `Assets/Dravio/Art/Imported/Prefabs/EmberlingRefinedGLBVisual.prefab`, `…/EmberlingRefinedGLB.controller`, vật liệu ở `Art/Imported/Materials` (copy `_BaseMap` từ texture nhúng trong FBX). Gói 16 rồng (`DragonVisualCatalog`) vẫn theo `Docs/char9/UNITY_INTEGRATION_GUIDE.md` §2, chưa tái tạo với CHAR.9b.
- **Phụ thuộc scene generator** (`Dravio ▸ Open Game`): `Art/Models/EmberlingRefinedGLB.fbx` (file này) → `OriginSanctuaryEnvironmentBuilder.BuildScene` → `Environment/Scenes/OriginLongTermGameplay_Environment.unity` → `LivingEnvironmentBuilder.AddToOpenScene` → `Scenes/OriginLongTermGameplay.unity`. Không ghi đè scene đã có.
- Các bước: import FBX (Max Size 1024, Android ASTC 6×6), **Dravio ▸ Character ▸ Validate Refined GLB FBX** (mong đợi PASS cấu trúc: 22 bone, 5 clip, UV0), rồi **Dravio ▸ Open Game**. Commit `.meta` do Unity sinh. Nếu FBX cũ (CHAR.9) đã import trên máy AI 1: reimport, giữ `.meta`.
- Walk: 35 frame / 1,1667 s, `groundSpeed` trong `EmberlingRefinedGLB_contract_rig.json` (đã nhân 0,9 theo biên độ chân).
- Cần AI 1 báo lại: kết quả audit, ảnh Game view 5 clip, có thấy nếp gấp ở đầu gối / bụng khi Doze không → lúc đó mới ghi **UNITY_VERIFIED**.

## 5. Kiểm thử

- `python3 -m unittest discover -s scripts/art/character/tests`: 91 test OK (thêm `test_char9b_emberling.py`: hash FBX = file đã đo, ngưỡng = `character-budgets.json` và = 1,6 / 0,45 / 0,01, xuất + import lại PASS 5/5 với skinning tuyến tính, 15/15 clip batch PASS, bố cục take khớp generator, sửa rig chỉ bật cho Emberling).
- Tái tạo: `python batch_import.py ArtSource/Dragons/images <work> --bpy-module --overrides Assets/Dravio/Character/Data/asset-overrides.char8-production.json --force` (thư mục nguồn chỉ có `01_emberling.glb`), rồi `export_emberling_legacy_contract.py <work>/assets/DRG_emberling_Unstaged/DRG_emberling_Unstaged_Rigged.blend Assets/Dravio/Art/Models/EmberlingRefinedGLB.fbx --rig-json …_rig.json --basecolor …_BaseColor.png`, rồi `anim_qa.py`. FBX không tái tạo từng byte (timestamp), số QA thì có.

## 6. Chưa làm (theo issue: hoãn tới khi mẫu Emberling được duyệt)

- 15 loài còn lại, 48 biến thể giai đoạn, trứng: chưa áp dụng `repair` / `skinning: linear`; số QA cũ của chúng là dual quaternion (§1).
- Vật thể / môi trường GLB mới: không có nguồn mới trên nhánh này.
- Không chạm Domain, Persistence, Save Schema, gameplay UI, Android package.
