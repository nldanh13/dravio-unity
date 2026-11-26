# CHAR.8: Creature Quality & Sanctuary Assets — báo cáo kiểm thử, hiệu năng và QA

Tài liệu liên quan: [`AURORAWING.md`](../../../Assets/Dravio/Character/AURORAWING.md), [`FEEDING_RESPONSE.md`](../../../Assets/Dravio/Character/FEEDING_RESPONSE.md), [`SANCTUARY_PREFABS.md`](../../../Assets/Dravio/Environment/SANCTUARY_PREFABS.md). Báo cáo máy sinh: [`reports_char8_sample/`](reports_char8_sample/).

## 0. Phạm vi bằng chứng — phần nào chạy thật, phần nào chỉ kiểm source

| Hạng mục | Trạng thái |
|---|---|
| Blender 5.3 (`bpy` 5.3.0, Linux, CPU, Cycles) | **Chạy thật**: tạo mẫu, batch import (prepare → rig → LOD → QA → validate), render ảnh QA, dựng 3 công trình Sanctuary và render |
| Python unit test (character 81, environment 81), `build_archetypes.py --check`, `build_species_spec.py --check` | **Chạy thật**, qua hết |
| .NET 8: `Dravio.Character.Tests` (54), TypeCheck MonoBehaviour bằng stub UnityEngine | **Chạy thật**, qua hết (TypeCheck không phải biên dịch Unity) |
| Unity 6000.6 Editor / Play mode / Animation Lab / Stress scene | **Chưa chạy** (môi trường không có Unity). Code Editor mới (`SanctuaryProjectPrefabBuilder`, `SanctuaryProjectVisual`) chưa được biên dịch trong Unity |
| Thiết bị Android/iOS: FPS, bộ nhớ, draw call thật | **Chưa đo**. Mục 4 là ước tính tĩnh |
| 7 GLB rồng thật (`01_emberling` … `07_sparkwing`) | **Không có trong repo**; nằm trên máy của bạn. Không model production nào được sửa hay kiểm lại ở đây |
| Video | Không có (không có Unity). Có ảnh render Blender ở `reports_char8_sample/images/` |

## 1. Nền tảng (Nhiệm vụ 1)

- Nhánh `feat/char-8-creature-sanctuary` tạo từ `main` mới nhất (`aa58a49`, đã gồm U2.11 và nhánh `feat/u1-social-character-animation`: nhánh này không còn commit nào ngoài `main`).
- Đã đọc: Character README, LIVING_DRAGONS (rig v2 → v7), MOTION_API, MOTION_CATALOG, ANIMATION_CATALOG, SOCIAL_ANIMATION, SPECIES_MORPHOLOGY, BIOMECHANICS_RESEARCH, CHARACTER_BEHAVIOR_API, SANCTUARY_INTEGRATION, TEST_REPORT_U8, Docs/U5.4.2B (Rig v6). Pipeline hiện là `u1-char8-v1` (rig v7).
- Không đổi API runtime nhân vật, Animator, planner hay motion contract. Tương thích Unity Presentation hiện tại: không file nào trong `Character/Runtime`, `Scripts`, `Scenes` bị sửa.

## 2. Baseline: tái hiện QA của U1-CHAR.8 trên mẫu

`make_sample_library.py` → 13 GLB SAMPLE (8 rồng, 1 vật lạ, 4 prop) → `batch_import.py --bpy-module` với `asset-overrides.u7-local.json`.

| Kết quả | Số |
|---|---|
| Rồng rig + animation QA **PASS** (ngưỡng không đổi) | 8 / 8 (emberling, droplet, sprout, zephyr, steamdrake, magma, frostfin, leviathan) |
| Prop validation PASS | 4 / 4 (`ok_with_notes`: chưa có chiều cao thật) |
| Cảnh báo QA animation | 0 |

Khớp TEST_REPORT_U8 (mọi model rig PASS, không cảnh báo). Chi tiết: `reports_char8_sample/baseline/`.

## 3. Aurorawing (Nhiệm vụ 2)

Xem `AURORAWING.md`. Tóm tắt: blockout SAMPLE ba giai đoạn + trứng, rig winged 32 bone, 19 clip theo sinh học canonical (không Swim/Burrow/Perch), LOD 24 000 / 10 000 / 4 000, **QA PASS cả ba giai đoạn**; trứng prop `egg` 2 500 / 900 / 300, collider convex 250 tam giác, 0,15 × 0,22 m (đề xuất).

Vòng lặp sửa lỗi đã chạy thật (không nới ngưỡng):

| Lần | Vấn đề QA / pipeline | Sửa (ở asset, không ở ngưỡng) | Kết quả |
|---|---|---|---|
| 1 | Màu Baby sai vùng (tô sau khi thu nhỏ) | tô màu trước khi scale | đúng vùng |
| 2 | Adult: `limbCount auto` bị từ chối, "0 bàn chân" | thế đứng Adult rộng hơn (P1) | rig được, 4 chân, độ tin cậy cao |
| 3 | Baby: giãn cạnh 1,636 (Eat), 1,674 (Sleep), 1,722 (Drink) / 1,6; cánh lẫn thân 0,0037 / 0,002 (Eat, Drink); xuyên đất 0,0102 / 0,01 (Land) | đầu Baby ×1,30 → ×1,20 | **PASS, 0 cảnh báo** (giãn lớn nhất 1,555) |
| 4 | Trứng thấp hơn rộng | sửa tỉ lệ ô-van (cao ≈ 1,35 × rộng) | 0,15 × 0,22 m |

## 4. Chất lượng rồng sống (Nhiệm vụ 3)

Kết quả đo trên 11 model rig (8 mẫu baseline + 3 Aurorawing), Blender, mọi frame mỗi clip:

| Kiểm tra (anim_qa v3) | Ngưỡng | Kết quả |
|---|---|---|
| Trượt chân trụ (Walk/Run) | ≤ ngưỡng `animationQa` | 0 – 0,0002 (tương đối) |
| Xuyên đất | ≤ 0,01 | lớn nhất 0,0095 (Aurorawing Adult, Land) |
| Giãn / nén cạnh (skinning) | p99,9 ≤ 1,6; p0,1 ≥ 0,45 | lớn nhất 1,555 (Aurorawing Baby, Drink) |
| Cánh xuyên thân | ≤ 0,002 | 0 |
| Khép vòng clip loop, chuyển tiếp Sleep→Wake→Idle, Idle→TakeOff→Fly→Land→Idle | sai lệch tư thế | ≤ 0,069° |

Những việc **không làm được ở đây** và lý do: sửa trượt chân / skinning trên **model production** (không có GLB thật trong repo); kiểm chuyển động trên Unity (Foot IK, head look, shaper, `SyncPhase`, tương tác đôi). Cách làm trên máy bạn: `run_u7_local.ps1` (pipeline hiện là rig v7) với `-Overrides Assets/Dravio/Character/Data/asset-overrides.char8-local.json`, rồi Animation Lab và Stress scene như TEST_REPORT_U8.

Phản ứng nhìn / tiếp cận / ăn của cá thể được chọn: nghiên cứu và hợp đồng đề xuất ở `FEEDING_RESPONSE.md` (dựa trên luật Domain thật, phát hiện của mô phỏng U2.12). Chưa có code.

## 5. Sanctuary (Nhiệm vụ 4)

3 mẫu phong cách (mỗi thế giới một), dựng thật, render thật; catalog 20 công trình với prefab ID, material, LOD, anchor, bounds: `SANCTUARY_PREFABS.md`.

| Mẫu | LOD tris | Kích thước (m) | Material | Anchor |
|---|---|---|---|---|
| `origin_lantern_row` (light) | 1 856 / 928 / 408 | 7,76 × 1,66 × 1,57 | ENV_Props | 7 |
| `ocean_coral_arch` (landmark) | 2 028 / 1 014 / 446 | 5,90 × 3,74 × 1,74 | ENV_Ocean_Props, ENV_Ocean_Foliage | 10 |
| `sky_star_observatory` (landmark) | 856 / 428 / 188 | 3,12 × 4,00 × 4,10 | ENV_Sky_Props | 5 |

UV của mọi mặt nằm trong ô atlas của nó (`uvErrors = 0`). Không texture mới.

## 6. Hiệu năng (ước tính tĩnh, chưa đo thiết bị)

| Thành phần | Mỗi rồng | Ghi chú |
|---|---|---|
| Tam giác | 24 000 / 10 000 / 4 000 (LOD0/1/2) | ngân sách `character-budgets.json` |
| Bone | 26 – 32 (winged 32) | < 75 (ngưỡng cảnh báo) |
| Draw call | 1 (+1 shadow) | 1 material/model; SRP Batcher giữ khi không có luật phenotype |
| Texture | BaseColor 1024² | ASTC 6×6 ≈ 0,12 MB + mip ≈ 0,16 MB |
| Mesh (ước tính) | LOD0 ≈ 1 MB, LOD1 ≈ 0,45 MB, LOD2 ≈ 0,2 MB | ~13k / 5,5k / 2,2k đỉnh × ~76 B (vị trí, normal, UV, 4 trọng số) |
| Animation khi bake | 0,31 – 0,82 MB (Aurorawing 0,70) | trước nén `Optimal` của Unity |

Cảnh 30 rồng (phân bố 20/50/20/10 %): ~318 000 tam giác vẽ, ~17 Animator đánh giá / frame (bảng `performance-budget.md`). Sanctuary: +1–2 draw call mỗi công trình, cùng material với cảnh; 20 công trình ở LOD0 tối đa ≈ 20 × 2 000 – 6 000 tam giác.

## 7. Phát hiện cần quyết định

| # | Phát hiện | Đề xuất |
|---|---|---|
| P1 | `rig_geometry` chia lưới chân theo đường chéo cả mô hình → loài sải cánh lớn có thể bị từ chối rig | landmark tay cho model thật, hoặc sửa pipeline (ô lưới theo phần thân gần đất) sau khi chạy lại 7 model của bạn |
| P2 | GLB trứng: thứ tự chỉ mục tam giác đổi giữa các lần xuất (hình học trùng); GLB rồng thì tất định | không ảnh hưởng asset; batch xử lý lại trứng mỗi lần (rẻ) |
| P3 | Aurorawing: số chân, tỉ lệ ba giai đoạn, trứng; Baby có bay không | art director / GDD (AURORAWING.md mục 5) |
| P4 | Sanctuary: duyệt phong cách trước khi làm 17 công trình còn lại; vị trí slot chưa có | art director |

## 8. Lệnh tái tạo

```bash
python -m venv .venv && .venv/bin/pip install "bpy==5.3.*"          # hoặc dùng blender --background
.venv/bin/python scripts/art/character/make_sample_library.py <src>
.venv/bin/python scripts/art/character/batch_import.py <src> <work> --bpy-module \
    --overrides Assets/Dravio/Character/Data/asset-overrides.u7-local.json
.venv/bin/python scripts/art/character/make_aurorawing_review.py <aw_src>
.venv/bin/python scripts/art/character/batch_import.py <aw_src> <aw_work> --bpy-module --qa-images \
    --overrides Assets/Dravio/Character/Data/asset-overrides.char8-local.json
python scripts/art/character/build_character_reports.py <work> <reports>
.venv/bin/python scripts/art/environment/build_sanctuary_projects.py -- --out <dir> --preview
python -m unittest discover -s scripts/art/character/tests
```
