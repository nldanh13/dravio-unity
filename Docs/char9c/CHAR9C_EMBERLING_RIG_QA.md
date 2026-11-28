# CHAR.9c — Emberling: kiểm lại GLB, rig, skin weights, animation (Issue #95)

Issue: https://github.com/nldanh13/dravio-unity/issues/95 (tiêu chí gốc #76). Nhánh `claude/project-thread-ya18wv`, tạo từ `feat/u5-6-player-journey` **4b3d598** (đúng HEAD lúc giao việc) rồi cherry-pick commit CHAR.9b `31201f8` (PR #78, đã merge vào `main` nhưng **chưa có** trên `feat/u5-6-player-journey`). Không merge `main` vì `main` còn mang thay đổi Domain/economy không thuộc phạm vi này.

## 0. Trạng thái (tách bạch nơi đã kiểm)

| Mức | Kết quả |
|---|---|
| **Blender verified** (Blender 5.3.0, module `bpy`, chạy thật trong môi trường này) | FBX ứng viên `ArtStaging/Character/Emberling/EmberlingRefinedGLB.fbx`: **5/5 clip hợp đồng PASS ở mọi frame** sau khi import lại FBX vào scene trống, skinning tuyến tính; **15/15 clip rig v7 PASS ở mọi frame**; khe nứt đường nối = 0 mm. Ngưỡng không đổi. |
| Unity Editor verified | **NOT RUN** (không có Unity trong môi trường này). |
| Android verified | **NOT RUN**. |
| APPROVED | **Chủ dự án duyệt ngày 27/11/2026** (trong thread). FBX ứng viên đã thay `Assets/Dravio/Art/Models/EmberlingRefinedGLB.fbx` (giữ nguyên `.meta`/GUID), sha256 `a1fc66ae…`. Bản CHAR.9b cũ: `0e27862d…`. Unity vẫn NOT RUN: AI Unity (#91) cần reimport và kiểm. |

## 1. Baseline khác với báo cáo trước — bằng chứng

Tôi chạy lại đúng pipeline CHAR.9b (`batch_import.py` + `repair_chibi_rig.py`) trên `01_emberling.glb`: **15/15 clip khớp tuyệt đối** số trong `reports_char9b` (sai khác 0,000). Sau đó đo độc lập FBX hợp đồng đã commit (`verify_contract_fbx.py`, import FBX vào scene trống, không dùng rig.json). Hai vấn đề mà QA trước không thấy:

### 1a. `anim_qa.py` chỉ đo 8 frame mỗi clip và gộp chúng lại

`SAMPLES = 8`, rồi p99,9 / p0,1 tính trên **tập cạnh gộp của 8 frame**. Báo cáo CHAR.9 ghi "mọi frame mỗi clip" là không đúng. Một frame xấu bị pha loãng bởi các frame tốt. Cùng FBX CHAR.9b:

| Clip | Frame Unity | 8 frame gộp (cách cũ) | Mọi frame gộp | **Frame tệ nhất** | Xuyên đất | Khe nứt (mm) | |
|---|---|---|---|---|---|---|---|
| Idle | 1–181 | 1,086 / 0,927 | 1,089 / 0,924 | 1,130 / 0,892 (f114) | 0,0008 | 8,3 | |
| Walk | 182–217 | 1,330 / 0,607 | 1,338 / 0,611 | 1,553 / 0,526 (f188) | 0 | 28,7 | |
| Eat | 218–278 | 1,409 / 0,494 | 1,423 / 0,486 | 1,519 / **0,428** (f233) | 0 | **45,5** | **FAIL** nén |
| Doze | 279–399 | 1,317 / 0,543 | 1,317 / 0,543 | 1,323 / 0,542 (f294) | 0,0083 | 28,9 | |
| Happy | 400–445 | 1,161 / 0,726 | 1,167 / 0,734 | 1,225 / 0,513 (f404) | 0 | 25,0 | |

Cột "8 frame gộp" tái tạo đúng số CHAR.9b (kiểm trong unit test). Trên batch 15 clip, frame tệ nhất của Eat, Drink (nén 0,428) và Run (giãn 1,696) vượt ngưỡng.

### 1b. Lưới bị **nứt** ở đường nối UV — QA giãn cạnh không thể thấy

GLB/FBX tách đỉnh ở mọi đường nối UV/normal (16 253 đỉnh cho 24 000 tam giác; 4 251 cặp đỉnh trùng vị trí). Hai đỉnh của một cặp không có cạnh nối, nên tỉ lệ giãn cạnh không đo được khoảng cách giữa chúng. Rig v7 (và bước khuếch tán của CHAR.9b) gán trọng số **riêng cho từng bên** đường nối: có cặp lệch trọng số L1 = 2,0 (hai bên theo hai xương khác nhau hoàn toàn). Khi skin, bề mặt **mở khe**: tới **45 mm (5 % chiều cao) ở Eat**, 29 mm ở Doze/Walk. Unity giữ nguyên tách đỉnh này nên sẽ thấy khe y như vậy.

Ảnh: `scripts/art/character/reports_char9c/seam_cracks_closeup_before_after.jpg` (Cycles, FBX import lại; cột trái CHAR.9b có vết nứt đen ở sừng/trán, cột phải ứng viên CHAR.9c liền). Lỗi có từ rig v7 gốc (trước khi sửa) và có ở **cả 15 loài còn lại** (§4).

## 2. Sửa (chỉ Emberling, qua `asset-overrides.char8-production.json` → `species.emberling.repair`)

| Tham số | CHAR.9b | CHAR.9c | Lý do |
|---|---|---|---|
| `seamWeld` (mới) | — | `true` | Cặp đỉnh trùng vị trí được nối trong đồ thị khuếch tán (cạnh dài 0) và nhận **trọng số giống hệt** (trung bình nhóm), cả trong lẫn ngoài vùng hòa. Khe nứt 45 mm → 0 mm. |
| `blendWidth_m` | 0,12 | 0,24 | Khi đường nối không còn "hấp thụ" biến dạng bằng cách nứt, giãn dồn vào háng/vai; dải hòa trắc địa rộng hơn trả nén Eat/Drink về ngưỡng ở frame tệ nhất. |
| `amplitude.Run.legs` | 0,8 | **0,6** | Run (không thuộc hợp đồng 5 clip) giãn ở Pelvis 1,67 tại frame tệ nhất; 0,66 vẫn 1,609. `groundSpeed` co theo, PlaybackRate vẫn khóa chân. **Cần animator xem**: biên độ chạy giảm rõ. |
| Các hệ số khác | giữ | giữ | Eat/Drink đầu ×0,6; Sleep/Rest/Bask chân ×0,72, gối ×0,7; Walk ×0,9; skinning tuyến tính. |

Không đổi: GLB nguồn, bộ xương, tên bone, khoảng frame clip, hợp đồng 22 bone / 5 clip, ngưỡng QA, hình dáng/màu/tỉ lệ.

Đã thử và loại (số trong log, không commit): giảm chân Eat/Drink ×0,8 (đạt nén nhưng trượt chân 0,014–0,016, sát ngưỡng 0,02); dải hòa 0,16/0,20 (Run vẫn trượt).

## 3. Kết quả FBX ứng viên — `ArtStaging/Character/Emberling/EmberlingRefinedGLB.fbx`

Đặt ngoài `Assets/` để Unity **không** tự import và không đụng scene/prefab của #91/#94. Cấu trúc (đo trên FBX import lại): 30 bone (đủ 22 bone hợp đồng `EmberlingAssetAudit`), mesh `Emberling_Baby_Body_GLB` 24 000 tam giác / 16 253 đỉnh, UV0, BaseColor 2048² nhúng, ≤ 4 ảnh hưởng, 0 đỉnh không trọng số, sai số chuẩn hóa 0,0001, 4 251 cặp đường nối lệch trọng số 0,0, chiều cao 0,90 m, đáy z = 0, tâm đế (0, 0), take 445 frame (FBX importer Blender lệch +1, đã bù).

**Import lại FBX, mọi frame, skinning tuyến tính** (`verify_candidate_fbx.json`):

| Clip | Frame | 8 frame gộp | Mọi frame gộp | **Frame tệ nhất** (gate) | Xuyên đất | Khe nứt | |
|---|---|---|---|---|---|---|---|
| Idle | 1–181 | 1,081 / 0,932 | 1,084 / 0,930 | 1,118 / 0,905 | 0,0004 | 0 mm | PASS |
| Walk | 182–217 | 1,374 / 0,589 | 1,376 / 0,596 | 1,572 / 0,533 | 0 | 0 mm | PASS |
| Eat | 218–278 | 1,408 / 0,516 | 1,420 / 0,506 | 1,534 / 0,459 | 0 | 0 mm | PASS |
| Doze | 279–399 | 1,254 / 0,554 | 1,255 / 0,554 | 1,263 / 0,553 | 0,0072 | 0 mm | PASS |
| Happy | 400–445 | 1,172 / 0,711 | 1,178 / 0,721 | 1,255 / 0,511 | 0 | 0 mm | PASS |

`anim_qa.py --samples all --strict-frame` trên take hợp đồng (`export_EmberlingRefinedGLB_animqa_allframes.json`): PASS, 0 cảnh báo; trượt chân Walk 0,0029, Doze 0,0015 (≤ 0,02); khép vòng Idle/Walk/Doze 0,056° (≤ 0,5°).

**Batch rig v7, 15 clip, mọi frame** (`batch_DRG_emberling_Unstaged_animqa_allframes.json`): PASS, 0 cảnh báo. Frame tệ nhất: Idle 1,118/0,905 · Eat 1,534/0,459 · Sleep 1,263/0,553 · Happy 1,255/0,511 · Walk 1,563/0,531 · Run 1,554/0,521 · Rest 1,251/0,554 · Wake 1,250/0,494 · LookAround 1,334/0,693 · Curious 1,254/0,641 · Drink 1,586/0,455 · Stretch 1,554/0,519 · Bask 1,251/0,554 · Greet 1,105/0,915 · Nuzzle 1,118/0,886. Chuyển tiếp Sleep→Wake→Idle 0,056°.

**Biên an toàn mỏng, nói rõ:** Eat nén 0,459 (ngưỡng 0,45), Drink 0,455, Walk giãn 1,572 và Drink 1,586 (ngưỡng 1,6). Đạt, nhưng không còn nhiều chỗ. Chỉnh tay trong Blender (đặc biệt vùng háng sau khi cúi ăn) sẽ cho biên rộng hơn; tôi không tự đổi dáng clip ngoài các hệ số biên độ đã nêu.

**Quan sát chưa có ngưỡng:** `seamSpeedMismatchDegPerFrame` của Walk 3,1°/frame (Fore_L đổi tốc độ tại điểm nối vòng; có từ rig v7 gốc, CHAR.9b như nhau). Cần xem trong Unity có giật nhẹ mỗi chu kỳ không.

Ảnh thực (Blender Cycles, FBX import lại):
- `reports_char9c/compare_char9b_vs_char9c_worst_frames.jpg`: frame tệ nhất từng clip, có texture + bản đồ nhiệt, CHAR.9b vs ứng viên.
- `reports_char9c/seam_cracks_closeup_before_after.jpg`: cận cảnh vết nứt.
- `reports_char9c/candidate_rest_turntable.jpg`: dáng nghỉ 4 hướng (hình dáng, màu giữ nguyên).
- `reports_char9c/*_animqa_allframes.jpg`: tờ QA của `anim_qa.py` (đỏ = giãn, xanh = nén).

## 4. P1 — 16 GLB gốc (đã đo, chưa sửa)

Audit container + hình học: `audit_source_glbs.py` → `source_glb_audit.json/.md` (nguồn chỉ đọc, kiểm SHA-256 trước/sau). Rig v7 hiện tại của 15 loài: chạy lại `batch_import.py` (không có bước repair, đúng như chưa duyệt), rồi `anim_qa.py --samples all --strict-frame` với skinning tuyến tính (Unity) → `species_remeasure/`, `species_remeasure_linear_allframes.json`. Số CHAR.8/9 cũ của các loài này là dual quaternion + 8 frame, không so sánh trực tiếp được.

Chung cho cả 16 file: xuất từ trimesh, 1 mesh, 40 000 tam giác (~20 000 đỉnh sau hàn), kín (0 cạnh biên, 0 cạnh không đa tạp), thể tích dương (pháp tuyến đúng chiều), 0 mặt suy biến, UV0 trong [0,1], BaseColor JPEG 2048² + metallic-roughness PNG 2048², **không normal map, không skin, không animation**, gốc tọa độ ở tâm hộp bao (pipeline tự đặt chân xuống đất), kích thước chuẩn hóa cạnh dài ≈ 1,99 (không phải tỉ lệ thật). 34–59 % mặt UV bị lật hướng (chart soi gương; chỉ quan trọng nếu sau này thêm normal map tangent-space).

| # | Loài | File / SHA-256 (12) | Tam giác / đỉnh hàn | Đảo rời | UV lật | Texture | Skin/Anim gốc | Archetype | Clip | Clip trượt ngưỡng (8 frame gộp → mọi frame) | Khe nứt tối đa (% cao) | Clip tệ nhất (giãn frame tệ nhất) | Cảnh báo khác | Ưu tiên |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | emberling | `01_emberling.glb` `42bffef7b055` | 40000 / 20002 | 1 | 39 % | 2048² + 2048² | 0/0 | quadruped | 15 | 0 → 0 | 0 | Drink 1,59 | — | P0, chờ duyệt |
| 2 | droplet | `02_droplet.glb` `1834c2c0f7b2` | 40000 / 20002 | 1 | 38 % | 2048² + 2048² | 0/0 | quadruped | 21 | 14 → 16 | 11,5 | Run 5,18 | — | P1 |
| 3 | sprout | `03_sprout.glb` `8392e83e3187` | 40000 / 19988 | 1 | 34 % | 2048² + 2048² | 0/0 | quadruped | 19 | 15 → 18 | 15,9 | Run 5,25 | — | P1 |
| 4 | zephyr | `04_zephyr.glb` `804c04f8d3a5` | 40000 / 19994 | 1 | 40 % | 2048² + 2048² | 0/0 | winged | 20 | 9 → 12 | 16,6 | Land 2,94 | bones, facing, footSliding, ground | P1 |
| 5 | steamdrake | `05_steamdrake.glb` `80e355739c41` | 40000 / 19998 | 1 | 33 % | 2048² + 2048² | 0/0 | serpentine | 21 | 14 → 17 | 6,1 | Swim 4,44 | — | P1 |
| 6 | magma | `06_magma.glb` `0736cace7072` | 40000 / 20002 | 1 | 43 % | 2048² + 2048² | 0/0 | quadruped | 19 | 12 → 15 | 11,2 | Run 4,18 | bones | P1 |
| 7 | sparkwing | `07_sparkwing.glb` `a0458fd23246` | 40000 / 20000 | 1 | 58 % | 2048² + 2048² | 0/0 | winged | 19 | 14 → 15 | 15,7 | Run 7,64 | ground | P1 |
| 8 | frostfin | `08_frostfin.glb` `05c056059d29` | 40000 / 20002 | 1 | 59 % | 2048² + 2048² | 0/0 | aquatic | 13 | 8 → 11 | 9,5 | Happy 5,33 | bones | P1 |
| 9 | mossback | `09_mossback.glb` `56a58cc38982` | 40000 / 20002 | 1 | 44 % | 2048² + 2048² | 0/0 | quadruped | 21 | 15 → 19 | 10,2 | Swim 11,00 | — | P1 |
| 10 | blossom | `10_blossom.glb` `627fd6cce53d` | 40000 / 20002 | 1 | 46 % | 2048² + 2048² | 0/0 | winged | 20 | 14 → 17 | 16,7 | Run 3,77 | bones, ground | P1 |
| 11 | solaris | `11_solaris.glb` `f1cfc468579b` | 40000 / 19996 | 1 | 40 % | 2048² + 2048² | 0/0 | winged | 19 | 10 → 12 | 8,9 | Run 2,79 | facing, ground | P1 |
| 12 | leviathan | `12_leviathan.glb` `d21dfe4c40fe` | 40000 / 19996 | 1 | 42 % | 2048² + 2048² | 0/0 | serpentine | 13 | 9 → 11 | 9,6 | Sleep 8,00 | — | P1 |
| 13 | amberhorn | `13_amberhorn.glb` `f7052958535f` | 40000 / 20002 | 1 | 45 % | 2048² + 2048² | 0/0 | quadruped | 19 | 12 → 14 | 13,4 | Burrow 3,81 | ground | P1 |
| 14 | aurorawing | `14_aurorawing.glb` `a2113560571a` | 40000 / 19972 | **5** | 49 % | 2048² + 2048² | 0/0 | winged | 19 | 12 → 14 | 14,4 | Sleep 2,81 | bones | P1 |
| 15 | celestia | `15_celestia.glb` `c88f8f7e239b` | 40000 / 19998 | 1 | 38 % | 2048² + 2048² | 0/0 | winged | 19 | 14 → 15 | 21,6 | Run 6,49 | bones, ground | P1 |
| 16 | gaia | `16_gaia.glb` `70c5d8622a2b` | 40000 / 19998 | 1 | 34 % | 2048² + 2048² | 0/0 | quadruped | 21 | 16 → 17 | **27,4** | Swim 13,33 | bones, ground | P1 |

Đã đo: mọi cột trên. **Chưa đo**: trong Unity, trên thiết bị, LOD1/2 của 15 loài dưới skinning tuyến tính, số chân thật (11 loài vẫn `limbCount: auto`, cần art director xác nhận), chiều cao thật theo giai đoạn.

Phát hiện P1 đáng chú ý:
- **Khe nứt đường nối ở cả 15 loài, 6–27 % chiều cao** (lỗi hệ thống rig v7, không riêng chibi). Đây là lỗi nhìn thấy rõ nhất trong game; sửa bằng `seamWeld` là thay đổi kỹ thuật, không đổi dáng.
- `08_frostfin` và `03_sprout` có trục dài theo X (nằm ngang); sprout đã có `yawOffset: 90`, frostfin thì không — cần xem hướng mặt (frostfin không có cảnh báo `facing`, nên có thể đã đúng).
- `14_aurorawing` có 5 mảnh rời (các loài khác 1).

Thứ tự đề xuất sau khi Emberling được duyệt (chờ chủ dự án/AI3): (1) bật `seamWeld` cho cả 15 loài, đo lại; (2) quadruped cùng archetype Emberling: magma, droplet, amberhorn, sprout, mossback, gaia (mai/giáp theo Spine/Chest); (3) winged: solaris, zephyr, aurorawing, sparkwing, blossom, celestia; (4) serpentine/aquatic: steamdrake, frostfin, leviathan (giảm biên độ cuộn). Không ép chung rig; không tạo 48 biến thể giai đoạn trước nghiệm thu.

## 5. Bàn giao cho AI kỹ thuật Unity (#91) — đã duyệt, FBX đã thay trong nhánh này

| File | Vai trò | SHA-256 |
|---|---|---|
| `ArtStaging/Character/Emberling/EmberlingRefinedGLB.fbx` | FBX ứng viên (thay `Assets/Dravio/Art/Models/EmberlingRefinedGLB.fbx` khi được duyệt) | `a1fc66aef92c7b16b0a193d50c9d74f4e53bc2c1830a43f4292210bde90f63ea` |
| `ArtStaging/Character/Emberling/EmberlingRefinedGLB_contract_rig.json` | khoảng frame, contact, `groundSpeed` của 5 clip hợp đồng | như trên |
| `Assets/Dravio/Art/Models/EmberlingRefinedGLB.fbx` | **đã thay** bằng FBX ứng viên (giống từng byte); bản CHAR.9b trước đó `0e27862d4ceb017d5f884c5a240ee043f514a37b071cb3925466982b920710ab` | `a1fc66aef92c7b16b0a193d50c9d74f4e53bc2c1830a43f4292210bde90f63ea` |
| `ArtSource/Dragons/images/01_emberling.glb` | nguồn, **không đổi** | `42bffef7b055d6841e595fcc89840efa83bf3d8553a7edd0b90293f22b7a546f` |

Việc còn lại trong Unity: kéo nhánh, Reimport `EmberlingRefinedGLB.fbx` (`.meta` giữ nguyên nên GUID không đổi, prefab/controller/scene đang tham chiếu không vỡ), chạy **Dravio ▸ Character ▸ Validate Refined GLB FBX** (mong đợi PASS cấu trúc), rồi xem 5 clip trong Game view, nhất là cận cảnh đầu khi Eat/Doze (vết nứt phải biến mất) và vòng lặp Walk. Không đổi tên bone, tên clip, số frame → `EmberlingModelPipeline`, `EmberlingAssetAudit`, prefab `EmberlingRefinedGLBVisual`, controller và chuỗi scene `Dravio ▸ Open Game` không phải sửa. Gói 16 rồng (`DragonVisualCatalog`) chưa tái tạo.

Không chạm: Domain/Persistence, economy, UI, camera, môi trường, shader chung, scene gameplay, prefab/catalog/bootstrap runtime, nhánh AI3 #89/#92, PR #94.

## 6. Công cụ mới / sửa

- `audit_source_glbs.py` (mới): audit chỉ đọc 16 GLB (container glTF + đo hình học trong Blender).
- `verify_contract_fbx.py` (mới): kiểm FBX hợp đồng như Unity thấy (scene trống, tuyến tính, mọi frame, khe nứt đường nối, ảnh Cycles).
- `anim_qa.py`: thêm `--samples all` và `--strict-frame` (áp cùng ngưỡng cho frame tệ nhất, cảnh báo khe nứt > 0,1 % chiều cao — ngưỡng **đề xuất**, chưa đưa vào `character-budgets.json`); thêm trường `framesMeasured`, `worstFrameStretchP999`, `worstFrameCompressP001`, `seamGapRel`. Mặc định vẫn 8 frame gộp để số cũ so sánh được.
- `repair_chibi_rig.py`: tùy chọn `seamWeld`.
- `tests/test_char9c_emberling.py`: nguồn và FBX hợp đồng không đổi, FBX ứng viên đúng file đã đo, ngưỡng không nới, 5/5 clip PASS mọi frame sau import lại, baseline CHAR.9b được tái tạo và FAIL ở frame tệ nhất, 15/15 clip batch PASS mọi frame, repair chỉ bật cho Emberling.

## 7. Tái tạo

```bash
pip install bpy==5.3.0 numpy pillow
# Emberling (thư mục nguồn chỉ chứa 01_emberling.glb), vài phút trên CPU 4 nhân
python scripts/art/character/batch_import.py <src> <work> --bpy-module --overrides Assets/Dravio/Character/Data/asset-overrides.char8-production.json --force
python scripts/art/character/export_emberling_legacy_contract.py <work>/assets/DRG_emberling_Unstaged/DRG_emberling_Unstaged_Rigged.blend ArtStaging/Character/Emberling/EmberlingRefinedGLB.fbx --rig-json <work>/assets/DRG_emberling_Unstaged/DRG_emberling_Unstaged_rig.json --basecolor <work>/assets/DRG_emberling_Unstaged/DRG_emberling_Unstaged_BaseColor.png
python scripts/art/character/verify_contract_fbx.py ArtStaging/Character/Emberling/EmberlingRefinedGLB.fbx out.json --render renders/
python scripts/art/character/anim_qa.py <Rigged.blend> <rig.json> <out> --samples all --strict-frame
python scripts/art/character/audit_source_glbs.py ArtSource/Dragons/images audit.json --md audit.md
python -m unittest discover -s scripts/art/character/tests
```

FBX không tái tạo từng byte (timestamp), số QA thì có: lần chạy thử (rig_archetype + repair riêng) và lần chạy batch cho cùng số.

## 8. Va chạm với vật thể và rồng khác (đo theo yêu cầu chủ dự án, 27/11)

Công cụ `measure_contact_clearance.py`. Input: FBX ứng viên (import lại, tuyến tính, mọi frame) + dữ liệu điều hướng thật `Assets/Dravio/Environment/Data/OriginSanctuaryLocations.asset` (34 vùng chặn, `walkClearance` 0,45 m). Kết quả: `reports_char9c/contact_clearance_origin.json`, bản đồ `contact_origin_map.png`, ảnh Cycles `contact_origin_worst_point.jpg`.

**Tầm với của Emberling** (tính từ gốc rồng, là điểm hệ thống điều hướng dùng):

| Clip | Trước | Sau | Ngang | Cao | Bán kính tối đa |
|---|---|---|---|---|---|
| Idle | 0,744 | 0,739 | 0,203 | 0,900 | 0,744 |
| Walk | 0,742 | 0,739 | 0,204 | 0,906 | 0,742 |
| Eat | 0,740 | 0,740 | 0,198 | 0,920 | 0,740 |
| Doze | 0,713 | 0,610 | 0,368 | 0,912 | 0,713 |
| Happy | 0,753 | 0,735 | 0,374 | 0,948 | 0,753 |

Animation hầu như không làm rồng vươn xa hơn dáng nghỉ: dài 1,48 m, cao 0,9 m, gốc nằm giữa thân.

**Trong scene Origin** (lưới 0,1 m; 10 264 điểm mà hệ thống đi bộ chấp nhận; xét mọi hướng quay và mọi frame):
- 15–17 % số điểm (tùy clip) khiến Emberling lấn vào vùng chặn hơn 1 cm, 10–12 % lấn hơn 10 cm. Sâu nhất **0,30 m** (Happy, điểm (6,6; −3,9), hướng 140°), tức đầu chui hẳn vào tảng đá hoặc tường (xem ảnh).
- Nguyên nhân là **khoảng chừa của hệ thống điều hướng chứ không phải rig**: `SanctuaryLocationData.IsWalkable` chỉ giữ gốc rồng cách vùng chặn 0,45 m, trong khi Emberling vươn tới 0,75 m. Muốn không lấn ở mọi hướng cần chừa ≥ 0,76 m, hoặc dùng hình viên nang theo hướng mặt (dài 0,75 m trước/sau, 0,2–0,37 m hai bên).

**Hai rồng với nhau**: runtime cho hai rồng đứng cách nhau 0,55 m tính từ gốc (`LivingSanctuaryPresentation.Separated`). Hai Emberling đối mặt sẽ chồng lên nhau tới 0,94 m; 41–51 % số cặp hướng quay có chồng lấn. Muốn an toàn ở mọi hướng cần ≥ 1,51 m, hoặc tính theo hướng mặt.

**Chưa đo**: lưới thật của từng vật thể (hệ thống điều hướng chỉ biết đa giác chân đế, nên tảng đá tròn có thể lấn ít hơn hộp), rồng đi qua bậc thang hay dốc (`DragonFootGrounding` chưa chạy trong Unity), camera xén, vật lý Unity.

**Đề xuất cho AI kỹ thuật Unity (#91)**: không sửa ở đây vì thuộc điều hướng và runtime. Đưa tầm với đo được của từng loài vào catalog (dữ liệu sinh từ pipeline, ví dụ `reach.front/back/side`), để điều hướng và khoảng cách giữa các rồng dùng số của chính con rồng đó thay vì các hằng số 0,45 / 0,55 m. Rồng mới hay rồng đổi kích thước khi đó vẫn đúng mà không phải sửa code.

Hướng dẫn thêm/sửa rồng và các công thức khoảng chừa: [ADD_OR_EDIT_DRAGON.md](ADD_OR_EDIT_DRAGON.md). `anim_qa.py` nay ghi `reach_m` (trước/sau/ngang/bán kính, từ gốc) cho từng clip; `batch_import.py --qa-strict` bật đo mọi frame cho mọi loài.


## 9. 15 loài còn lại (sau khi Emberling được duyệt, 27/11)

Đầu vào: output rig v7 của 15 GLB gốc (không đổi), skinning tuyến tính, `anim_qa --samples all --strict-frame` (mọi frame, frame tệ nhất, ngưỡng không đổi 1,6 / 0,45 / 0,01). Số liệu: `scripts/art/character/reports_char9c/species_tune/summary.json` và `DRG_<loài>_Unstaged_animqa.json`.

Ba bước, đều qua `asset-overrides` → `species.<loài>.repair`, không đổi GLB, bộ xương, tên clip, khoảng frame:
1. **Hàn đường nối** (`seamWeld`) + `blendWidth_m` 0,24 + tuyến tính: khe nứt về **0 ở cả 15 loài** (trước: 6–27 % chiều cao).
2. **Giữ tư thế chuyển tiếp** (`pinTransitions`, mới trong `repair_chibi_rig.py`): clip loop giữ một hệ số; đầu/cuối của clip một lần (Wake, TakeOff, Land, WaterEnter/Exit, Burrow…) lấy hệ số của clip loop nối với nó, hoặc 1 (tư thế gốc), rồi chuyển mượt trong 25 % độ dài clip. Mọi chuyển tiếp Sleep→Wake→Idle, bay, nước, đào hang vẫn khớp (0 cảnh báo `transition`).
3. **Dò biên độ theo loài** (`tune_repair.py`, mới): mỗi vòng đo lại mọi frame; clip nào còn vượt ngưỡng thì giảm biên độ quay của hai nhóm xương gây lỗi nhiều nhất (theo `worstBones`: chân, đầu/cổ, thân, đuôi, cánh, vây; Pelvis/Root không bao giờ bị giảm) đi 15 %, sàn 0,5. Clip nào chạm sàn mà vẫn hỏng thì **trả lại biên độ gốc** và ghi NEEDS_ART: công cụ không làm phẳng chuyển động khi không đạt.

| Loài | Kiểu | Hỏng trước | Khe nứt trước | Sau bước 1–2 | **Sau bước 3** | Biên độ nhỏ nhất | Tầm với (m) | Kết luận | Clip tệ nhất còn lại (giãn/nén) |
|---|---|---|---|---|---|---|---|---|---|
| amberhorn | quadruped | 14/19 | 13.4 % | 6 | **0** | 0.614 | 1.03 | Đạt biến dạng | – |
| aurorawing | winged | 14/19 | 14.4 % | 14 | **9** | 0.522 | 1.17 | Cần art | LookAround 4.40/0.55 Drink 4.20/0.50 Eat 3.95/0.50 |
| blossom | winged | 17/20 | 16.7 % | 9 | **2** | 0.5 | 1.09 | Cần art | Drink 1.57/0.41 Eat 1.52/0.42 |
| celestia | winged | 15/19 | 21.6 % | 13 | **4** | 0.522 | 1.06 | Cần art | Run 2.92/0.37 Stretch 2.30/0.49 Drink 2.24/0.35 |
| droplet | quadruped | 16/21 | 11.5 % | 12 | **1** | 0.5 | 1.01 | Cần art | Run 2.78/0.30 |
| frostfin | aquatic | 11/13 | 9.5 % | 11 | **4** | 0.5 | 1.06 | Cần art | Happy 3.72/0.54 Sleep 2.13/0.67 Wake 2.09/0.68 |
| gaia | quadruped | 17/21 | 27.4 % | 15 | **11** | 0.5 | 1.01 | Cần art | Swim 7.30/0.35 WaterEnter 7.30/0.37 WaterExit 7.30/0.37 |
| leviathan | serpentine | 11/13 | 9.6 % | 10 | **4** | 0.522 | 1.00 | Cần art | Happy 4.07/0.36 Sleep 3.58/0.33 Wake 3.58/0.33 |
| magma | quadruped | 15/19 | 11.2 % | 9 | **0** | 0.5 | 1.02 | Đạt biến dạng | – |
| mossback | quadruped | 19/21 | 10.2 % | 14 | **8** | 0.5 | 1.02 | Cần art | Swim 4.62/0.46 WaterEnter 4.62/0.45 WaterExit 4.62/0.45 |
| solaris | winged | 12/19 | 8.9 % | 10 | **8** | 0.614 | 1.13 | Cần art | TakeOff 2.87/0.37 Fly 2.82/0.49 Land 2.82/0.34 |
| sparkwing | winged | 15/19 | 15.7 % | 13 | **4** | 0.522 | 1.09 | Cần art | Run 3.30/0.39 Stretch 2.31/0.52 Drink 1.99/0.38 |
| sprout | quadruped | 18/19 | 15.9 % | 13 | **8** | 0.614 | 1.03 | Cần art | Run 2.77/0.34 Drink 2.48/0.20 Eat 2.37/0.20 |
| steamdrake | serpentine | 17/21 | 6.1 % | 11 | **4** | 0.5 | 1.02 | Cần art | Sleep 2.04/0.21 Wake 2.04/0.20 Rest 1.73/0.28 |
| zephyr | winged | 12/20 | 16.6 % | 8 | **3** | 0.5 | 1.07 | Cần art | Land 2.35/0.35 Sleep 1.68/0.53 Wake 1.68/0.52 |

Đọc bảng:
- **amberhorn, magma** đạt cả 19 clip ở frame tệ nhất. Run của magma và một số clip nằm nghỉ dùng hệ số 0,5–0,72 cho chân: chuyển động nhỏ hơn rõ, **cần animator xem** trước khi coi là bản cuối (mọi hệ số < 0,7 đều nên xem).
- 13 loài còn lại giảm từ 8–15 clip hỏng xuống 1–11. Phần còn lại không sửa được bằng biên độ: ngay cả 0,5 vẫn giãn 2–7 lần (gaia Swim 7,3; aurorawing LookAround 4,4; leviathan Happy 4,1). Đó là lỗi **trọng số/vị trí xương** của rig v7 trên các model này (ví dụ đầu hay cánh ăn trọng số Chest/Pelvis), hoặc tư thế tác giả quá gắt (Sleep/Rest nằm sấp). Cần làm theo từng loài: sửa trọng số vùng `worstBones` hoặc chỉnh keyframe, không nới ngưỡng.
- Cảnh báo khác (không phải biến dạng) **chưa xử lý**, có từ rig v7: `ground`/`groundContact`/`footSliding` ở clip nằm và Eat/Drink, `wings` (celestia, sparkwing: cánh xuyên thân), `bones` (gương trái/phải lệch > 1 %: gaia, magma, frostfin…). Danh sách từng loài trong `summary.json` → `otherWarnings`.
- **Tầm với 1,0–1,17 m** từ gốc ở mọi loài, lớn hơn nhiều so với `walkClearance` 0,45 m (xem §8): kiến nghị cho #91 càng quan trọng hơn với các loài lớn.
- Unity: **NOT RUN**. Gói 16 rồng (`DragonVisualCatalog`) chưa tái tạo; không tạo biến thể giai đoạn.

Chạy lại cho một loài:
```bash
python scripts/art/character/tune_repair.py <work>/assets/DRG_<loài>_Unstaged <out> --bpy-python <python có bpy>
```
(`<work>` là một lần `batch_import.py` **không** có `repair` cho loài đó.) Kết quả `tune_<tên>.json` có mọi vòng, cấu hình tốt nhất và các clip NEEDS_ART.


## 10. Thử sửa trọng số thân (27/11, sau khi merge #100)

Chẩn đoán trên hai loài nặng nhất (`diag`: các cạnh vượt ngưỡng ở frame tệ nhất, vị trí lúc nghỉ, xương chi phối):
- **gaia Swim (giãn 7,3)**: 650 đỉnh ở vùng chân, trọng số Pelvis + ForeUpper/ForeLower. Clip gập cả 4 chân 52°, mà chân của model này ngắn (0,36 m). Giảm riêng biên độ chân còn 0,3 vẫn giãn 2,3, còn đổi `blendWidth` 0,12–0,40 gần như không đổi gì. Nguyên nhân chính là **keyframe**: tư thế bơi gập chân không hợp với tỉ lệ model.
- **aurorawing LookAround (giãn 4,4)**: rig v7 cho trọng số Neck lan xuống tận giữa lưng và gốc cánh (230 đỉnh Spine+Neck nằm sau khớp Chest 0,3–0,8 m). Khi đẩy phần này về thân, clip này giảm còn 2,5.

Thêm vào `repair_chibi_rig.py` (tự chọn bật, mặc định tắt, Emberling không đổi):
- `trunkAxis`: phần trọng số thân (Pelvis + Spine* + Chest) chia lại theo vị trí dọc cột sống nhìn từ trên xuống.
- `neckSpill` (mặc định bật khi có `trunkAxis`): trọng số Neck/Head nằm sau khớp Chest trả về thân.

Đo lại 10 loài nặng nhất (dò biên độ, rồi trả lại biên độ gốc cho clip còn hỏng, giống §9). So sánh công bằng giữa hai lần dò:

| Loài | Trước | `trunkAxis` | `trunkAxis`, không `neckSpill` | Chọn |
|---|---|---|---|---|
| frostfin, leviathan, steamdrake | 4 / 4 / 4 | giống hệt (không có Pelvis, bước này không áp dụng) | – | giữ |
| mossback | 8 | 8, tổng vượt hơi cao hơn | – | giữ |
| sprout | 8 | 8: Sleep đạt nhưng **Walk hỏng** | – | giữ (Walk quan trọng hơn) |
| gaia | 11 | 12 | 11, tổng vượt gần như cũ | giữ |
| aurorawing | 9 | 10 | – | giữ |
| celestia, sparkwing | 4 / 4 | 6 (Fly/Land/TakeOff hỏng thêm) | 6 | giữ |
| **solaris** | 8 | 11 | **7** (Eat đạt) | **dùng** |

Kết luận thẳng: sửa trọng số tự động **chỉ cứu thêm 1 clip** (solaris Eat). Với phần lớn clip còn hỏng, giảm biên độ tới 0,5 vẫn không đủ, còn trọng số không phải chỗ nghẽn chính. Nghẽn nằm ở **keyframe của rig v7** quá gắt so với tỉ lệ từng model: Sleep/Rest/Bask nằm sấp, Swim gập chân, Drink/Eat cúi sâu, Fly/TakeOff/Land vỗ cánh. Bước tiếp theo cần animator: dựng lại các clip đó theo từng loài, có thể dùng `worstBones` và frame tệ nhất trong `*_animqa.json` để biết chỗ cần sửa. Không nới ngưỡng, không làm phẳng chuyển động để qua QA.


## 11. Kéo lùi keyframe cục bộ (27/11, sau khi merge #102)

`repair.local` (mới trong `repair_chibi_rig.py`): `{clip: [[nhóm xương, frame, k, nửa độ rộng], ...]}`. Chỉ quanh frame bị giãn quá ngưỡng, nhóm xương gây lỗi bị kéo về tư thế nghỉ theo hệ số k (hình chuông theo thời gian, vòng tròn nếu clip loop). Phần còn lại của clip giữ nguyên chuyển động gốc, đầu và cuối clip có chuyển tiếp vẫn giữ hệ số 1. `tune_repair.py --local` tự đặt các điểm này: mỗi vòng thêm hoặc tăng điểm ở frame tệ nhất (bước 0,8, sàn 0,3).

Chạy cho 13 loài còn hỏng. Tool đưa thêm 14 clip qua ngưỡng biến dạng, nhưng **10 clip trong số đó sinh lỗi mới**: chân trượt, xuyên đất, hở chạm đất (Run, Eat/Drink, Rest/Bask, Burrow của blossom, gaia, mossback, sparkwing, sprout, steamdrake). Lý do là kéo chân về mà không có IK giữ bàn chân thì bàn chân rời chỗ đặt. Các fix đó **không được nhận**. Chỉ giữ fix làm clip qua ngưỡng mà không thêm cảnh báo nào:

| Loài | Clip được sửa | Hệ số nhỏ nhất | Clip còn hỏng |
|---|---|---|---|
| gaia | Rest, Bask | 0,41 (chân, gần như cả clip) | 9 |
| leviathan | LookAround | 0,33 (đầu + thân, quanh frame 608) | 3 |
| mossback | SwimSurface | 0,41 (chân) | 7 |

Ba clip này thay đổi chuyển động rõ, **cần animator xem**.

Tổng cộng 15 loài hiện còn **65/283 clip** vượt ngưỡng biến dạng ở frame tệ nhất (lúc đầu 223/283). Muốn đi tiếp bằng công cụ thì cần IK khóa bàn chân khi kéo chân về. Đó là thay đổi lớn ở dây chuyền animation của rig v7, nên đi cùng việc animator dựng lại các clip nằm, bơi, ăn/uống và bay.

## 12. Hai hướng tự động nữa đã thử (không nhận) và gói ảnh cho animator

Đo trên droplet Run (giãn 2,78, chủ yếu ở các đỉnh có trọng số Pelvis + ForeLower/HindLower):
- **Xương phụ nửa góc ở hông/vai** (`*_Half`: con của Pelvis/Chest, quay 50 % góc của Upper, nhận phần trọng số chung Pelvis/Upper): 2,78 → 2,78. Không tác dụng, vì đỉnh bị rách gắn thẳng vào Pelvis và **Lower** (đầu gối gập 53–67°): chân của model ngắn tới mức khớp gối nằm sát bụng.
- **Chỉ giảm đầu gối** (`knees` 0,5 / 0,3): 2,84 / 2,97. Còn tệ hơn.
- **IK giữ bàn chân**: không thử chạy. Với lỗi do chân, giữ bàn chân đúng chỗ cũ thì IK hai đoạn cho lại đúng tư thế chân gốc, tức là lỗi quay lại.

Kết luận: 65 clip còn lại phải sửa ở **keyframe** (giảm góc gập chân/đầu gối và tư thế nằm/bơi sao cho hợp với chân ngắn) hoặc **vẽ trọng số bằng tay** ở vùng nối chân với bụng. Không nới ngưỡng.

Gói ảnh cho animator: `scripts/art/character/reports_char9c/species_tune/failing_clips/<loài>.jpg`, 13 tờ, 65 clip. Mỗi clip có 2 góc nhìn ở frame tệ nhất: đỏ là giãn > 1,6, xanh là nén < 0,45. Kèm tên clip, số frame (theo timeline rig v7), mức giãn/nén và 3 xương gây lỗi nhiều nhất. Hầu hết vùng lỗi nằm ở chỗ nối chân với bụng.

## 13. Sàn biên độ 0,3 (chủ dự án chọn, 27/11)

Chạy `tune_repair.py --floor 0.3` cho 13 loài, bắt đầu từ cấu hình đang dùng. Clip có giảm chân vẫn nhân `groundSpeed` cùng hệ số, nên bàn chân không trượt khi phát ở đúng tốc độ. Một fix chỉ được nhận nếu clip qua ngưỡng **và** không sinh cảnh báo mới (chân trượt, xuyên đất, hở chạm đất, chuyển tiếp).

**Nhận 14 clip:**
- aurorawing: Bask, Fly
- frostfin: Eat
- gaia: Run
- leviathan: Sleep, Wake
- mossback: Run
- solaris: Bask, Fly, Sleep, Wake
- sprout: Burrow
- zephyr: Sleep, Wake

**Không nhận 13 clip** vì sinh cảnh báo mới:
- blossom: Drink, Eat
- celestia: Run
- droplet: Run
- mossback: Rest, Sleep, Wake
- solaris: Drink
- sparkwing: Run
- sprout: Run
- steamdrake: Bask, Rest, Sleep, Wake

Còn **51/283 clip** vượt ngưỡng (lúc đầu 223/283).

**Cần duyệt bằng mắt**: nhiều hệ số đã xuống 0,3–0,45. Ví dụ cánh khi Fly của aurorawing và solaris chỉ còn 0,3–0,38, nghĩa là vỗ cánh nhỏ đi khoảng 3 lần. Ảnh trước/sau ở frame tệ nhất cũ: `reports_char9c/species_tune/floor03_before_after.jpg`. Hệ số từng clip nằm trong `asset-overrides` → `species.<loài>.repair.amplitude`. Ngưỡng QA không đổi; Unity NOT RUN.

## 14. Chế độ guard (27/11, sau khi merge #106)

`tune_repair.py --guard`: nếu một lần giảm biên độ làm chính clip đó có cảnh báo mới (chân trượt, xuyên đất, hở chạm đất, chuyển tiếp, cánh), công cụ hoàn tác lần giảm đó và cấm giảm tiếp nhóm xương này trên clip đó. Sau đó nó thử nhóm xương gây lỗi tiếp theo (tối đa 4 nhóm). Chạy với sàn 0,3 cho 13 loài còn hỏng.

**Nhận thêm 7 clip**, không sinh cảnh báo mới:
- aurorawing: Sleep, Wake
- frostfin: Sleep, Wake
- gaia: Sleep, Wake
- solaris: Land

Còn **44/283 clip** vượt ngưỡng. Ảnh trước/sau: `reports_char9c/species_tune/guard_before_after.jpg`.

**Cần duyệt bằng mắt**: Sleep/Wake của ba loài này có thân và đầu ở 0,3, nên rồng hầu như **không còn nằm xuống** khi ngủ (gaia rõ nhất). Đây là thay đổi lớn về diễn xuất. Nếu không chấp nhận, trả các clip đó về cấu hình trước (`beforeGuard` trong `summary.json`).

Phần còn lại guard đã chặn hết mọi nhóm xương, nghĩa là giảm nhóm nào cũng làm chân trượt hoặc xuyên đất. Đó là:
- Eat/Drink của hầu hết các loài
- Run của celestia, droplet, sparkwing, sprout
- Rest/Sleep/Bask của mossback và steamdrake
- Swim của gaia và mossback

Các clip này chỉ sửa được bằng cách dựng lại keyframe hoặc vẽ trọng số bằng tay (gói ảnh §12).
