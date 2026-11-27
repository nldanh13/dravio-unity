# VIS.4b — Tích hợp art Unity & QA hình ảnh Origin (Issue #91)

Nhánh QA: `claude/project-thread-paq1n1`. Liên quan #86, #87, #88, #89, #90.
Báo cáo này chỉ ghi kết quả đã chạy thật. Mục nào không chạy được ghi **NOT RUN**, không đánh dấu pass.

## Baseline và phạm vi

| Mục | SHA |
|---|---|
| HEAD tích hợp `feat/u5-6-player-journey` | `4b3d598b7ea05b6b244f7a6ea43c36a5c294715a` (merge PR #88) |
| PR #89 head (VIS.3a–c) | `8714386b6abc4c0744320bed0c658c5c8dcce6f4` (khớp issue) |
| PR #87 head (UI kit v1) | `7c32847d5899e8ba733847818a6d016d51cfb5ae` (khớp issue) |

Commit trên nhánh QA (theo thứ tự): merge #89 → merge #87 → khôi phục `ArtSource/UI` v1 → import 6 FBX VIS.3b → công cụ QA → báo cáo này.
Không merge #83/#84. Không sửa nhánh AI3. Không đổi Domain/Persistence/Data/collider/location/spawn/animation (đã kiểm bằng script, mục 2).

Môi trường: Linux container, **không có Unity Editor, không có thiết bị**. Có Blender 5.3 dưới dạng module Python `bpy 5.3.0`, .NET SDK 8.0.134, Node 22, Khronos glTF-Validator 2.0.0-dev.3.10, EEVEE chạy qua lavapipe (Vulkan phần mềm).

## 1. Kết quả tóm tắt

| Hạng mục (issue #91) | Trạng thái | Ghi chú |
|---|---|---|
| Tích hợp #89 + #87 vào nhánh QA | DONE | #87 xoá nhầm 17 sprite + manifest `ArtSource/UI` v1 → đã khôi phục (mục 4.1) |
| Test sẵn có (.NET + Python) | PASS | 950 test .NET, 106 test Python, xem mục 2 |
| Review shader/material/ánh sáng | PASS (source review) | Không có lỗi chặn; 3 ghi chú (mục 3) |
| Compile shader/C# trong Unity | **NOT RUN** | Không có Unity |
| `build_origin_vis3_models.py` | PASS (Blender 5.3 bpy) | 6 FBX đạt mọi kiểm tra, đã import, giữ `.meta` |
| 3 GLB `Origin_VIS3c` | **FAIL – báo AI3** | Validator 0 lỗi, nhưng mặt bên cửa/cửa sổ bị lật, mặt trước quay ngược camera |
| Chuyển GLB → FBX thử | **HOÃN** | Chờ AI3 sửa lỗi hình học; xem yêu cầu kỹ thuật mục 5.3 |
| Thử bố cục | DONE (Blender, không phải scene Unity) | Không có vị trí đọc tốt mà không đổi camera |
| UI kit #87 theo hợp đồng #88 | **0/83 đạt** | Không tích hợp sprite nào; theme giữ fallback màu |
| Landscape 16:9/20:9/4:3, notch, Focus, chữ Việt/Anh, input | **NOT RUN** | Cần Unity/thiết bị; chỉ có test logic `UiFoundationTests` pass |
| Ảnh Game View 8h/13h/16h30/đêm | **NOT RUN** | Thay bằng render Blender trước/sau cùng camera (mục 6) |
| Draw calls / tris / materials / texture memory / FPS | Một phần | Tris & materials & texture tính offline; draw calls/FPS **NOT RUN** |

## 2. Test và kiểm tra đã chạy

| Lệnh | Kết quả |
|---|---|
| `dotnet test Assets/Dravio/Tests/Domain/DotNet~/Dravio.Domain.Tests.csproj` | Passed 516, Skipped 11, Failed 0 |
| `dotnet test Assets/Dravio/Tests/Persistence/DotNet~/...` | Passed 207 |
| `dotnet test Assets/Dravio/Character/Tests/DotNet~/...` | Passed 54 |
| `dotnet test Assets/Dravio/Tests/Integration~/...` (gồm `UiFoundationTests`) | Passed 173 |
| `dotnet build .../TypeCheck/...` (stub UnityEngine, không phải Unity compile) | 0 error |
| `python -m unittest discover -s scripts/qa` + `check_script_meta.py` | 13 OK, PASS |
| `scripts/art/environment` `test_environment_kit` / `test_living_environment` / `test_zone_environments` | 23 / 30 / 31 OK (chạy lại sau khi import FBX) |
| `scripts/art/vis1/test_vis1.py` | 9 OK (sau khi import FBX) |
| `living.py` tái sinh `OriginSanctuaryLiving.json` | Trùng khớp file đã commit |
| `build_origin_sanctuary.py` (bpy) tái sinh kit | Textures + Layout/Locations/Colliders/Living trùng khớp; 6 FBX gốc tái sinh giống hệt bản commit |
| `scripts/qa/vis4b/check_vis4b_integration.py` (mới) | 0 failure (xem dưới) |

`check_vis4b_integration.py` kiểm: mọi property số của 2 shader Painterly nằm trong `UnityPerMaterial` (SRP Batcher giữ tương thích); mọi property trong `M_ENV_*.mat` tồn tại trong shader; `OriginSanctuaryLiving.asset` trùng `.json` ở 14 keyframe (màu sáng, ambient, fog, cường độ, bóng, góc); ánh sáng tĩnh 13:00 trong builder C# và scene trùng keyframe 13:00; không có thay đổi ở Domain/Persistence/Data/Platform/Character/Scripts, Colliders/Locations/Layout, `.anim/.controller`, ProjectSettings so với base.

## 3. Review shader URP / material / ánh sáng (PR #89)

Source review `DravioPainterlyLit.shader`, `DravioPainterlyWater.shader`, builder, 4 material, `living.py`. **Không compile trong Unity.**

- Nhận bóng: giữ nguyên. Pass ForwardLit vẫn `TransformWorldToShadowCoord` + `GetMainLight(shadowCoord)`, multi_compile `_MAIN_LIGHT_SHADOWS(_CASCADE)` và soft shadows; ShadowCaster/DepthOnly không đổi. `_SoftSheen` và transmission nhân `ramp`/`shadowAttenuation` nên không sáng trong vùng bóng.
- Uống/té nước: ripple `_DravioRipples` (DravioRipples) vẫn cộng sau khối `_BrightPond`, alpha vẫn `max(alpha, rings*0.85)` → hiệu ứng uống/té nước giữ nguyên.
- Normals: comment VIS.3b đúng, card lá giữ normal hướng lên cả hai mặt; không flip backface.
- Nhánh `if (_BrightFantasy > 0)` là uniform branch, không tạo keyword/variant mới; SRP Batcher OK.
- Ghi chú (không chặn):
  1. Nước không nhận bóng (`GetMainLight()` không shadowCoord) – có từ trước #89.
  2. Shader không xử lý additional lights / Forward+ – có từ trước; Origin chỉ có 1 directional light.
  3. Thứ tự `m_Floats` trong 4 `.mat` không theo alphabet (chèn tay); Unity sẽ tự sắp lại khi lưu, gây diff nhỏ lần đầu mở.
- Ánh sáng: ngày 8h/13h/16h30 sáng và lạnh hơn nhẹ (pastel), đêm/hoàng hôn/bình minh không đổi; cường độ 0.98→1.02, 1.12→1.10, 1.00→1.02. Không tăng bóng.

## 4. Tích hợp có kiểm soát

### 4.1 PR #87 xoá nhầm ArtSource gốc

#87 tạo nhánh từ `e075075` và commit của nó **xoá 17 sprite + `manifest_dravio_ui.json` + preview** trong `ArtSource/UI/{Bars,Buttons,Dividers,Frames,Inventory,Tabs}`, trái với chính `Docs/CUTE_FANTASY_UI_ASSET_INTAKE.md` ("Do not overwrite existing ArtSource") và #90 ("không ghi đè gói v1/v1.1"). Nhánh QA khôi phục nguyên byte từ `4b3d598`. **Đề nghị AI3 revert phần xoá trên nhánh #87 trước khi merge.**

### 4.2 VIS.3b FBX (đã import)

`python scripts/art/environment/build_origin_vis3_models.py -- --out <staging ngoài Assets>` chạy bằng bpy 5.3:

| Asset | Tris trước → sau | Quả trang trí | UV lỗi | Mặt suy biến | AABB lệch |
|---|---|---|---|---|---|
| SM_ENV_Tree_Ancient | 3344 → 3584 | 3 | 0 | 0 | 0 mm |
| SM_ENV_Tree_Ancient_B | 2450 → 2770 | 4 | 0 | 0 | 0 mm |
| SM_ENV_Shrub_Round | 422 → 422 | 0 | 0 | 0 | 0 mm |
| SM_ENV_Rock_Mossy_A/B/C | 320/160/240 (không đổi) | 0 | 0 | 0 | 0 mm |

Scene Origin: 33 352 → **34 472 tris** (< 60k). Material slot, lớp UV/màu đỉnh, transform giống bản cũ khi import lại. Copy 6 FBX vào `Assets/Dravio/Environment/Models`, **giữ nguyên `.fbx.meta`** (GUID, remap material, collider). Manifest giữ cờ `vis3b.staged = true` vì chưa nghiệm thu trong Unity. Texture không đổi nên texture memory không đổi.

## 5. Ba GLB `ArtSource/Origin_VIS3c`

Công cụ: `scripts/qa/vis4b/inspect_origin_vis3c_glb.py` (bpy) + Khronos glTF-Validator. Tái sinh bằng `build_origin_vis3c.mjs` cho ra file giống hệt bản commit.

| | Cottage | Lookout | Soft Hills |
|---|---|---|---|
| glTF-Validator | 0 lỗi/0 cảnh báo | 0/0 | 0/0 |
| Tris (manifest) | 3968 ✓ | 2072 ✓ | 1920 ✓ |
| Normal lưu vs winding | khớp | khớp | khớp |
| Island kín bị lật toàn bộ | 0 | 0 | 0 |
| Cạnh non-manifold / biên | 2 / 52 (mái là tấm hở) | 0 / 0 | 0 / 0 |
| Tam giác chôn trong khối khác | 28.6 % | 24.7 % | 21.7 % |
| Pivot đáy (Y) | 0.00 | **0.02 (lơ lửng 2 cm)** | 0.00 |
| Tâm XZ | (0, 0.02) | (0, 0) | **(0.25, −1.25)** |
| UV | **không có** | **không có** | **không có** |
| Màu đỉnh | RGB, alpha = 1 | RGB, alpha = 1 | RGB, alpha = 1 |

Lỗi cho AI3 (không sửa nhánh/generator của AI3):

- **[High] Mặt bên cửa, khung cửa, kính/khung cửa sổ bị lật** (Cottage, Lookout). Render Cycles tô đỏ mặt sau thấy rõ quanh cửa. Nguyên nhân trong `build_origin_vis3c.mjs`, hàm `rounded()`: khi bán kính bo `rad > d/2` (hoặc `> w/2`), tâm góc `cz = d/2 − r` đổi dấu, outline tự cắt nên các mặt bên quay vào trong. Ví dụ cửa `d=.18, rad=.18`, khung cửa sổ `d=.16, rad=.12`, kính `d=.10, rad=.09`, cửa Lookout `d=.18, rad=.14`. Trong URP (Cull Back) các mặt này sẽ biến mất, nhìn xuyên qua. Kẹp `rad = min(rad, w/2−ε, d/2−ε)` thì hết lỗi (đã thử trong staging, không commit), nhưng **mất dáng vòm của cửa**. Dáng vòm hiện tại là do lỗi tạo ra, nên AI3 cần dựng vòm có chủ đích.
- **[Medium] Hướng mặt trước**: GLB quay mặt trước về +Z glTF = +Z Unity, tức là **quay lưng về camera Origin** (kit quy ước mặt trang trí hướng Unity −Z). Khi đặt cần yaw 180°, hoặc xoay sẵn khi chuyển FBX.
- **[Medium] Không có UV, màu đỉnh alpha = 1**: `Painterly Lit` dùng RGB màu đỉnh làm AO và **alpha làm trọng số gió / mặt nạ đường đi**. Nếu dùng thẳng với `M_ENV_Props` (`_WindStrength 0.05`) thì cả ngôi nhà sẽ đung đưa, và không có UV vào atlas. Cần một trong hai: (a) UV map vào ô trung tính của atlas + alpha màu đỉnh = 0 khi chuyển FBX, hoặc (b) material vertex-colour riêng (thêm 1 material, vẫn ≤ 10).
- **[Low]** Lookout lơ lửng 2 cm; Hills pivot lệch tâm; 22–29 % tam giác nằm khuất trong khối khác (lãng phí ngân sách, có thể bỏ khi tối ưu).

### 5.3 Chuyển FBX thử nghiệm: HOÃN

Chưa chuyển vì lỗi mặt lật sẽ đi thẳng vào Unity. Khi AI3 sửa xong, pipeline kỹ thuật: import GLB (bpy) → xoay 180° → UV vào ô atlas trung tính + đặt alpha màu đỉnh = 0 → export bằng `env.export_fbx` (cùng thiết lập kit) → staging ngoài Assets → kiểm như VIS.3b.

### 5.4 Thử bố cục (Blender, không đổi camera, không ghi vào dữ liệu game)

`render_origin_qa.py --scenery`: đặt thử ngoài vùng chơi (`flatRectXZ` x ±8.6, z −7..7.4):
- Lượt 1 (z 11.6–17.5, sau mép rừng như #89 đề xuất): **hoàn toàn ngoài khung hình** camera orthographic hiện tại.
- Lượt 2 (Cottage x −10.6 z 8.7; Lookout x 10.7 z 8.5; Hills z 14.5 ×1.25): Cottage bị cây cổ thụ trái che hết; chỉ thấy chân Lookout như một cột kem sau cây phải; Hills thành một khối xanh nhạt sau cổng vòm, đọc như vệt trời, và ở z 12.5 thì sườn đồi cắt vào chân cổng.

Lưu ý: trong 2 render thử bố cục, dải rừng nền phía bắc không hiện (khác với render trước/sau ở mục 6). Đây là lỗi của lần render thử, nên trong scene thật Cottage/Hills còn bị che nhiều hơn. Vì vậy không đính kèm ảnh thử bố cục.

Kết luận: không có vị trí đọc tốt nếu giữ nguyên camera. Đây là quyết định bố cục/art direction cho chủ dự án/AI3; **không đặt asset nào vào scene thật**.

## 6. Ảnh so sánh trước/sau (Blender EEVEE, KHÔNG phải Unity Game View)

`scripts/qa/vis4b/render_origin_qa.py` (bản sao của `scripts/art/vis1/render_origin_slice.py`, file gốc giữ nguyên). Cùng camera Unity (ortho 6, pitch 32°, có offset UI), 16:9 1280×720, Emberling tại `origin_meadow.spawn_01`.
- Trước: kit gốc + living JSON của `4b3d598` + look `vis1`.
- Sau: 6 mesh VIS.3b + living JSON #89 + look `vis3` (VIS.1 + mặt nạ lá `_BrightFantasy`; **bỏ qua** sheen, meadow drift, transmission, màu nước mới).
- Giờ: 8h, 13h (thêm Focus), 16h30, 22h (đêm). Ảnh lưu ở project files `vis4b/`.

Ảnh: `vis4b/origin_before_after_blender.jpg` (bảng tổng hợp) và từng ảnh `before_*.jpg` / `after_*.jpg`.

Quan sát (trên render Blender): quả đỏ VIS.3b đọc được là chấm đỏ nhỏ trên tán, đủ rõ ở 16:9 nhưng vẫn mang texture nấm (như #89 đã ghi). Ban ngày cỏ/lá sáng và tươi hơn, bóng giữ được. Nhưng đây chỉ là xấp xỉ shader, nên phải chụp lại trong Unity để nghiệm thu.

## 7. UI kit v1 (#87) theo hợp đồng sprite #88

`scripts/qa/vis4b/check_ui_kit_v1.py` (port `UiSpritePolicy` + `DravioUiValidator` + kiểm alpha/state/ngữ nghĩa). Báo cáo đầy đủ: `vis4b/ui_kit_v1_qa.md` + `ui_kit_v1_scale_preview.png` (project files).

- Kit có **83 sprite** (manifest + PR #87), không phải 107 như issue ghi.
- sha256 khớp 83/83; tất cả RGBA; không có speck alpha rời; không có pixel đặc chạm mép canvas.
- **0/83 đạt hợp đồng #88** → **không tích hợp sprite nào**; mọi slot giữ màu fallback (theme chưa được tạo/gán nên runtime không đổi).
  - Kích thước gốc 43–286 px so với hợp đồng 512–1024 px. Ở canvas scale tối đa 2.67, tỉ lệ mẫu 0.35–0.65 (phóng to và mờ); 14 sprite mờ ngay cả ở 720p.
  - Button: border 9-slice và độ rộng nội dung **khác nhau giữa các state** (normal 26 px, highlighted/pressed/disabled 32–35 px; nội dung hẹp hơn 12–17 px) → chuyển state sẽ "nhảy". State `highlighted` không có trong hợp đồng (normal/pressed/disabled).
  - 5 slot hợp đồng là 9-slice nhưng kit chỉ có ảnh fixed-aspect (card, portrait, slot common/rare/epic/legendary).
  - Ảnh preview 9-slice cho thấy mép trên button/panel còn sót mảnh của phần tử bên cạnh trên sheet gốc, và tab selected dính thanh gỗ (rail) ở đáy.
  - Thiếu art cho: slot `btn_back, tab_indicator, header, page_bg, progress_frame, progress_fill, progress_highlight, divider`; icon `rest, day, night, upgrade, temperature`; item `stardust, aquatic, aether, pack_mystic, pack_astral`.
  - 18 asset ngoài hợp đồng (round play/pause/plus/minus/done, frame panel/ribbon/speech, slot green/locked/empty/expand/selected, 4 bar reference) cần thống nhất slot trước khi dùng.
- Nếu chủ dự án chấp nhận theme thử chỉ cho 720p: 46 sprite đạt tỉ lệ ≥ 1 ở 720p (danh sách trong JSON). Đây là quyết định của chủ dự án, chưa làm.

## 8. Danh sách cho AI3 (ưu tiên)

1. [High] Revert phần xoá `ArtSource/UI` v1 trong #87.
2. [High] Sửa `rounded()` trong `build_origin_vis3c.mjs` (kẹp bán kính) và dựng vòm cửa có chủ đích; tái xuất 3 GLB.
3. [High] UI master độ phân giải theo hợp đồng #88 (button 1024×384, icon/item 512, panel 1024), các state cùng canvas/border/nội dung; bỏ sót mảnh sheet; tab không rail.
4. [Medium] Quy ước mặt trước GLB (−Z Unity hướng camera) hoặc ghi rõ yaw; UV/atlas hoặc material vertex-colour cho Cottage/Lookout/Hills.
5. [Medium] Bố cục Cottage/Lookout/Hills: cần composition pass cùng chủ dự án, vì khung camera hiện tại không có chỗ đọc tốt.
6. [Low] Pivot Lookout (y=0.02), Hills (tâm lệch), bớt tam giác chôn khuất.

## 9. Việc cần làm trong Unity (chủ dự án)

1. Mở nhánh `claude/project-thread-paq1n1`, chờ compile; Console không có lỗi shader/C#.
2. Kiểm import 6 FBX VIS.3b: remap material, collider, Focus, khung hình.
3. Chụp Game View cùng camera/resolution ở 8h, 13h, 16h30, đêm (16:9, 20:9, 4:3); so với ảnh Blender trong `vis4b/`.
4. Profiler/Frame Debugger: draw calls, tris, materials, texture memory; FPS trên thiết bị.
5. UI: safe area/notch, Focus, chữ Việt/Anh, chuyển tab/panel (chưa có sprite mới nên chỉ là regression của #88).
