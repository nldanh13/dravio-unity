# CHAR.9c — Emberling: kiểm lại GLB, rig, skin weights, animation (Issue #95)

Issue: https://github.com/nldanh13/dravio-unity/issues/95 (tiêu chí gốc #76). Nhánh `claude/project-thread-ya18wv`, tạo từ `feat/u5-6-player-journey` **4b3d598** (đúng HEAD lúc giao việc) rồi cherry-pick commit CHAR.9b `31201f8` (PR #78, đã merge vào `main` nhưng **chưa có** trên `feat/u5-6-player-journey`). Không merge `main` vì `main` còn mang thay đổi Domain/economy không thuộc phạm vi này.

## 0. Trạng thái (tách bạch nơi đã kiểm)

| Mức | Kết quả |
|---|---|
| **Blender verified** (Blender 5.3.0, module `bpy`, chạy thật trong môi trường này) | FBX ứng viên `ArtStaging/Character/Emberling/EmberlingRefinedGLB.fbx`: **5/5 clip hợp đồng PASS ở mọi frame** sau khi import lại FBX vào scene trống, skinning tuyến tính; **15/15 clip rig v7 PASS ở mọi frame**; khe nứt đường nối = 0 mm. Ngưỡng không đổi. |
| Unity Editor verified | **NOT RUN** (không có Unity trong môi trường này). |
| Android verified | **NOT RUN**. |
| APPROVED | Không. Chỉ chủ dự án/AI3 duyệt. FBX hợp đồng trong `Assets/` **chưa bị thay** (vẫn bản CHAR.9b, sha256 `0e27862d…`). |

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

## 5. Bàn giao cho AI kỹ thuật Unity (#91) — chỉ sau khi duyệt

| File | Vai trò | SHA-256 |
|---|---|---|
| `ArtStaging/Character/Emberling/EmberlingRefinedGLB.fbx` | FBX ứng viên (thay `Assets/Dravio/Art/Models/EmberlingRefinedGLB.fbx` khi được duyệt) | `a1fc66aef92c7b16b0a193d50c9d74f4e53bc2c1830a43f4292210bde90f63ea` |
| `ArtStaging/Character/Emberling/EmberlingRefinedGLB_contract_rig.json` | khoảng frame, contact, `groundSpeed` của 5 clip hợp đồng | như trên |
| `Assets/Dravio/Art/Models/EmberlingRefinedGLB.fbx` | **không đổi** (CHAR.9b) | `0e27862d4ceb017d5f884c5a240ee043f514a37b071cb3925466982b920710ab` |
| `ArtSource/Dragons/images/01_emberling.glb` | nguồn, **không đổi** | `42bffef7b055d6841e595fcc89840efa83bf3d8553a7edd0b90293f22b7a546f` |

Cách thay (một bước, có thể hoàn tác): copy FBX ứng viên đè lên `Assets/Dravio/Art/Models/EmberlingRefinedGLB.fbx`, **giữ nguyên `.meta`** (GUID không đổi nên prefab/controller/scene đang tham chiếu không vỡ), Reimport, chạy **Dravio ▸ Character ▸ Validate Refined GLB FBX** (mong đợi PASS cấu trúc), rồi xem 5 clip trong Game view, nhất là cận cảnh đầu khi Eat/Doze (vết nứt phải biến mất) và vòng lặp Walk. Không đổi tên bone, tên clip, số frame → `EmberlingModelPipeline`, `EmberlingAssetAudit`, prefab `EmberlingRefinedGLBVisual`, controller và chuỗi scene `Dravio ▸ Open Game` không phải sửa. Gói 16 rồng (`DragonVisualCatalog`) chưa tái tạo.

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
