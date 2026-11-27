# VIS.4b — Tích hợp art Unity & QA hình ảnh Origin (Issue #91)

Nhánh QA: `claude/project-thread-paq1n1`. Liên quan #86, #87, #88, #89, #90.
Báo cáo này chỉ ghi kết quả đã chạy thật. Mục nào không chạy được ghi **NOT RUN**, không đánh dấu pass.

## Baseline và phạm vi

| Mục | SHA |
|---|---|
| HEAD tích hợp `feat/u5-6-player-journey` | `4b3d598b7ea05b6b244f7a6ea43c36a5c294715a` (merge PR #88) |
| PR #89 head (VIS.3a–c) | `8714386b6abc4c0744320bed0c658c5c8dcce6f4` (khớp issue) |
| PR #87 head (UI kit v1) | `7c32847d5899e8ba733847818a6d016d51cfb5ae` (khớp issue) |
| QA lại sau bàn giao AI3 (27/11) | #87 `12d7249`, #89 `2b702e1` (mục 10) |

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
| 3 GLB `Origin_VIS3c` (v1) | **FAIL – báo AI3** → v2 xem mục 10 | Validator 0 lỗi, nhưng mặt bên cửa/cửa sổ bị lật, mặt trước quay ngược camera |
| Chuyển GLB → FBX thử | DONE cho v2 (staging, ngoài Assets) | Mục 10; chưa import, chưa đặt |
| Thử bố cục | DONE (Blender, không phải scene Unity) | Không có vị trí đọc tốt mà không đổi camera |
| UI kit #87 theo hợp đồng #88 | **0/83 đạt** | Không dùng; thay bằng bộ master #92 (mục 11) |
| UI master #92 theo hợp đồng #88 | **83/83 đạt (offline)** | Đã chép vào `Assets/Dravio/UI/Sprites`; chưa tạo/gán theme (mục 11) |
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

## 10. QA lại bàn giao AI3 (27/11): #87 `12d7249`, #89 `2b702e1`

Đã merge hai head mới vào nhánh QA. Test lại: `check_vis4b_integration.py` 0 failure; `scripts/qa` unittest OK; `check_script_meta` PASS; env tests OK.

### 10.1 #87: khôi phục ArtSource/UI v1
`git diff 4b3d598 12d7249 -- ArtSource/UI` ngoài `CuteFantasy_v1/` là **rỗng** → 18 file gốc đúng byte. Commit khôi phục riêng trên nhánh QA nay trùng nội dung, merge sạch. **PASS.**

### 10.2 #89: `ArtSource/Origin_VIS3c_v2` (Blender 5.3 bpy + Khronos glTF-Validator đã chạy)

| | Cottage | Lookout | Soft Hills |
|---|---|---|---|
| glTF-Validator | 0/0/0/0 | 0/0/0/0 | 0/0/0/0 |
| `build_origin_vis3c_v2.mjs` tái sinh | giống hệt byte | giống hệt byte | giống hệt byte |
| Tris (manifest) | 3560 ✓ | 1732 ✓ | 1920 ✓ |
| Cạnh winding ngược / island kín bị lật | 0 / 0 | 0 / 0 | 0 / 0 |
| Non-manifold / cạnh biên | 0 / 52 (mái tấm hở, như v1) | 0 / 0 | 0 / 0 |
| Mặt sau nhìn thấy (render Cycles tô đỏ) | không còn | không còn | không |
| Pivot đáy Y / tâm XZ | 0 / (0, 0) | 0 / (0, 0) | 0 / (0, 0) |
| Tam giác chôn khuất | 26.9 % | 23.3 % | 21.7 % |
| UV / alpha màu đỉnh | không / 1.0 | không / 1.0 | không / 1.0 |

- Lỗi mặt lật cửa/cửa sổ: **đã hết**; cửa và cửa sổ vòm đọc rõ. Hướng mặt tiền Cottage/Lookout: glTF −Z = Blender +Y = **Unity −Z (hướng camera)**, đúng quy ước kit, không cần xoay 180°. (`vis4b/v2/vis3c_v2_glb_check.png`, `vis3c_v2_door_closeup.png`.)
- **[Medium, AI3] Soft Hills v2 chỉ được căn tâm, không đổi hướng.** Ba đồi xanh lam cao (2.6–3.5 m) nằm phía camera (Blender +Y 0.35–1.25), hai đồi xanh lá thấp (1.8–1.95 m) nằm phía sau (−1.65…−1.85). Nhìn từ camera Origin, đồi cao che đồi thấp, ngược với lớp xa-gần (thấp và ấm ở trước, cao và lạnh ở sau). Cần đảo thứ tự trong generator, hoặc ghi rõ Hills phải đặt yaw 180°.
- [Low] Cửa Lookout là tấm phẳng đặt lên tường cong, nên hai góc dưới hở nhẹ khỏi tường khi nhìn chéo.

### 10.3 FBX thử nghiệm v2 (staging, không vào Assets)
`scripts/qa/vis4b/stage_vis3c_v2_fbx.py` → `vis4b/v2/staged_fbx/SM_ENV_Fairytale_Cottage.fbx`, `SM_ENV_Fairytale_Lookout.fbx`, `SM_ENV_Soft_Hills.fbx`. Không xoay thêm; alpha màu đỉnh = 0; giữ RGB; 1 slot `ENV_Architecture`; 1 kênh UV; xuất bằng đúng `export_fbx` của kit. Import lại: tris khớp (3560/1732/1920), bounds lệch 0 mm, đáy Z = 0, alpha = 0, mặt tiền Cottage nhiều diện tích hướng +Y (Unity −Z).

**Chưa import vào Unity, chưa đặt.** Lý do: atlas props hiện **không có ô trung tính**. Ô sáng nhất `mushroom_stem` có màu trung bình (218, 199, 163) và độ lệch luma 27, nên remap vào `ENV_Props` sẽ làm mọi màu tối và ngả vàng khoảng 15–35 %, kèm vân. Hai cách, đều cần duyệt:
1. Material tĩnh mới `M_ENV_Architecture`: cùng shader `Painterly Lit`, base map trắng mặc định, `_VertexAO 1`, `_WindStrength 0`, `_BrightFantasy 1`. Thêm 1 material (Origin 4 → 5, dưới giới hạn 10), không thêm texture, không thêm shader.
2. AI3 thêm ô trắng/trung tính vào atlas props (đổi texture, ảnh hưởng ngân sách texture).

Bố cục vẫn chờ composition review (mục 5.4); không đặt asset nào vào scene thật.

## 11. Bộ UI master #92 (`45c277f`, AI3 VIS.4–6)

Đã merge #92 vào nhánh QA. #92 không sửa file nào trong `Assets/`.

- Audit của AI3 `node scripts/art/ui/audit_vis6_masters.mjs --self-test`: 16 kiểm tra tổng hợp đạt. Chạy thật trên PNG đã commit: master 83/83, alternatives 6, **0 lỗi** (`vis4b/ui_masters/ui-vis6-audit.json`).
- Kiểm tra độc lập `scripts/qa/vis4b/check_ui_masters_vis4.py` theo hợp đồng #88: **83/83 không lỗi cứng**.
  - Kích thước, border 9-slice, đường dẫn runtime và state đều đúng hợp đồng. `ValidateBorder` sạch.
  - Tỉ lệ mẫu với kích thước import mà `UiSpritePolicy` đề xuất: ≥ 1.03 ở canvas 2.67 (thấp nhất là button/tab), ≥ 2.75 ở 720p. Không bị phóng to.
  - Các state button/close/back cùng canvas và cùng khung nội dung (normal/pressed/disabled).
  - Coverage icon/item nằm trong khoảng 75–85 %.
  - Cảnh báo: `ui_icon_night` có 1 pixel alpha lẻ tại (410, 338), không thấy được khi import ≤ 256 px, báo AI3 xoá. `ui_progress_highlight` có viền mềm sáng, đúng với vai trò lớp bóng sáng.
- Ảnh 9-slice ở canvas 1.0 và 2.67 (`vis4b/ui_masters/ui_masters_vis4_scale_preview.png`): góc sắc, không còn mảnh sót từ sheet, không bị rail.
- **Tích hợp:** chép 83 PNG đạt vào đúng đường dẫn hợp đồng `Assets/Dravio/UI/Sprites/...`, giống byte với ArtSource. **Không kèm `.meta`**: Unity tạo `.meta` khi mở, và `DravioUiSpriteImporter` tự áp thiết lập + border. Không tạo hay gán theme, và không có code runtime nào nạp các đường dẫn này. HUD vẫn dùng màu fallback cho đến khi ai đó chạy *Dravio > UI > Theme > Create Bright Fantasy v1 Theme* và *Assign*.
- Bộ nhớ ước tính nếu theme dùng hết 83 sprite ở kích thước import đề xuất: **RGBA32 khoảng 20.8 MiB** (standalone, không nén), **ASTC 4×4 khoảng 5.2 MiB** (Android/iOS). Cần đo trong Unity Profiler; nên cân nhắc Sprite Atlas.
- Alternatives (2 bộ button 3 state) và ornaments của `BrightFantasy_VIS5_Polish` **chưa tích hợp**: giữ riêng theo yêu cầu AI3, không trộn với master.
- **NOT RUN:** Unity import, `Dravio > UI > Validate`, Game View 16:9/20:9/4:3/notch, độ tương phản chữ Việt/Anh trên nút, input/chuyển tab. Mở Unity xong cần commit các `.meta` được sinh ra.

## 12. Hills v3 (#89 `9c15e2e`) và thực vật VIS.8 (#92 `e9d7099`)

Đã merge cả hai head vào nhánh QA. Hai head này không sửa file nào trong `Assets/`. Đã chạy Blender 5.3 bpy + Khronos glTF-Validator.

| GLB | Validator | Tái sinh | Tris (manifest) | Winding ngược / lật | Non-manifold / biên | Pivot đáy / tâm XZ | Kích thước (m) |
|---|---|---|---|---|---|---|---|
| Hills v3 `Origin_Soft_Hills` | 0 lỗi | giống byte | 1920 ✓ | 0 / 0 | 0 / 0 | 0 / (0, 0) | 17.5 × 7.5 × 3.49 |
| VIS8 `Origin_Bloom_Cluster` | 0 lỗi | giống byte | 1164 ✓ | 0 / 0 | 0 / 0 | 0 / (0.02, 0.05) | 0.65 × 0.44 × 0.56 |
| VIS8 `Origin_Fern_Rosette` | 0 lỗi | giống byte | 1512 ✓ | 0 / 0 | 0 / 0 | 0 / (0.00, 0.02) | 0.67 × 0.65 × 0.22 |
| VIS8 `Origin_Coral_Mushroom_Cluster` | 0 lỗi | giống byte | 672 ✓ | 0 / 0 | 0 / 0 | 0 / (0.01, 0.03) | 0.73 × 0.51 × 0.53 |

- **Hills v3: lỗi lớp xa-gần đã sửa.** Hai đồi xanh lá thấp (1.8–1.95 m) nằm phía camera (Blender +Y 1.65–1.85, tức Unity −Z), ba đồi xanh lam cao (2.6–3.5 m) nằm phía sau (−0.35…−1.25). Render từ phía camera xác nhận (`vis4b/v3_vis8/hills_v3_flora_check.png`). Không cần xoay thêm.
- **Thực vật VIS.8:** không có mặt sau lộ ra, đáy đặt đúng mặt đất (lệch tâm XZ ≤ 5 cm, chấp nhận được với vật trang trí). Ở camera Origin (khoảng 60 px/m ở 720p) mỗi cụm cao khoảng 13–34 px trên màn hình, trong khi tốn 672–1512 tris. Mật độ tam giác cao so với kích thước hiển thị, nên chỉ dùng thưa như AI3 đề xuất.
- **FBX thử (staging, không vào Assets):** `vis4b/v3_vis8/staged_fbx/` gồm Cottage/Lookout v2, Hills v3, Bloom, Fern, Mushroom. Alpha màu đỉnh = 0, 1 kênh UV, slot `ENV_Architecture`. Import lại: tris khớp, bounds lệch 0 mm, đáy Z = 0.
- **Ngân sách tris (giới hạn < 60 000):** scene hiện tại 34 472. Thêm Cottage + Lookout + Hills: 41 684. Thêm 1 bộ thực vật (mỗi loại 1 cụm): 45 032. Tối đa khoảng 5 bộ thực vật trước khi chạm giới hạn. Material: +1 (`M_ENV_Architecture`) nếu được duyệt, tổng 5/10. Texture: không đổi.
- **Vẫn chờ:** quyết định của chủ dự án về `M_ENV_Architecture` (AI3 cũng đề xuất cách này) và buổi review bố cục. Chưa đặt asset nào vào scene thật. Unity/thiết bị: NOT RUN.

## 13. Nước VIS.9 (#92 `7502a66`) và mesh nước VIS.3d

Đã merge `7502a66` vào nhánh QA. Head này không sửa file nào trong `Assets/`.

### 13.1 Texture `ArtSource/Origin_VIS9_Water` (decode bằng Pillow/numpy)
| Texture | Định dạng | Alpha | Nối mép U (mép / kề nhau) | Nối mép V |
|---|---|---|---|---|
| T_VIS9_Waterfall_Silk 256² | RGBA8 | 173–232 | 4.48 / 2.87 | 0.40 / 0.40 |
| T_VIS9_Stream_Curved_Ripples 256² | RGBA8 | 168–214 | 1.51 / 0.72 | 3.18 / 2.08 |
| T_VIS9_Foam_Loop_16 512² (4×4) | RGBA8 | 0–252 | 0.00 | 0.00 |

- Lát 2×2 không thấy đường nối (`vis4b/vis9_water/vis9_water_tile_2x2.png`). Chênh lệch ở mép chỉ cỡ 1.5–2 lần chênh lệch giữa hai pixel kề nhau, tức vài mức trên 255.
- Foam: khoảng trống trong suốt nhỏ nhất 18 px ở mọi ô. Thay đổi alpha giữa các khung trung bình 3.36, lớn nhất 4.02; khung 15 → 0 là 3.76, nên vòng lặp liền mạch (khớp số AI3 báo).
- **Chưa tích hợp:** `Painterly Water` chỉ đọc RGB, còn alpha đỉnh điều khiển bờ. Dùng các texture này cần material/shader nước chảy mới hoặc particle foam, tức là phải sửa shader/scene, nằm ngoài phạm vi khi chưa có duyệt.
- **Ngân sách texture:** giới hạn 6 MiB trong code (`ZoneEnvironmentBuilder.TextureBudgetBytes`) chỉ áp cho scene Ocean/Sky. Origin không có cổng kiểm tra texture; ước tính RGBA32 + mips cho 5 texture Origin hiện tại là **khoảng 11.0 MiB**. Thêm cả 3 texture VIS.9 là khoảng +2.0 MiB. Cần chủ dự án quyết định ngân sách texture cho Origin trước khi thêm.

### 13.2 Mesh `ArtSource/Origin_VIS3d/Models` (Blender 5.3 bpy + glTF-Validator)
- Validator 0 lỗi. Hai mesh đá có info `UNUSED_OBJECT TEXCOORD_0` (UV thừa, vô hại). Winding ngược 0, island lật 0, non-manifold 0, mặt suy biến 0.
- Tris: Waterfall Rock Frame 1764, Waterfall Surface 432, Stream Surface 384, Bank Stone Cluster 980; tổng 3560. Nếu thêm vào sau mục 12: 45 032 → **48 592** (< 60 000).
- **[Medium, AI3] Waterfall Surface là tấm một mặt, mặt trước quay glTF +Z.** Theo quy ước v2 (camera ở phía −Z), camera Origin sẽ nhìn vào mặt sau; render tô đỏ toàn bộ tấm nước (`vis3d_glb_backface_check.png`). Cần xoay 180°, đảo mặt, hoặc dùng material `Cull Off`. Rock Frame đối xứng nên không bị ảnh hưởng.
- [Low] Pivot: Stream Surface có tâm Z lệch 2.25 m (pivot ở một đầu dòng suối), Bank Stones lệch 0.91 m, Waterfall Surface đáy Y = 0.09 (cố ý để khớp pivot chung với Rock Frame theo README). Ghi lại để khi đặt không bị nhầm.

## 14. Tích hợp sau duyệt (27/11): `M_ENV_Architecture`, nước chảy VIS.9, ngân sách texture; QA VIS.10

Chủ dự án chọn "Material tĩnh mới" cho nhà/tháp/đồi và duyệt làm material nước chảy + foam ("làm đi"). Chưa có con số ngân sách texture, nên QA đề xuất một con số (mục 14.3); chủ dự án có thể đổi. **Chưa đặt asset nào vào scene** vì vẫn chờ review bố cục; không đổi collider, spawn, location, shader hồ hay hiệu ứng uống/té nước.

### 14.1 Material kiến trúc tĩnh
- `Materials/M_ENV_Architecture.mat` (Painterly Lit): `_BaseMap` để trống (shader dùng màu trắng mặc định), `_VertexAO` 1, `_WindStrength` 0, không emission, `_BrightFantasy` 1, `_SoftSheen` 0.11, cùng grade VIS.1 như props.
- `OriginSanctuaryEnvironmentBuilder`: thêm slot `ENV_Architecture` và `ENV_WaterFlow`; menu "1. Setup Materials and Import Remaps" tự cấu hình và remap 2 material mới. Lỗi thiếu texture nước chỉ là cảnh báo, không chặn.
- FBX vào `Models/Origin_VIS3c/` (thư mục con, nên test `CommittedOutputTests` của kit vẫn khớp danh sách builder): Cottage, Lookout (v2), Soft Hills (v3), Waterfall Rock Frame, Stream Bank Stone Cluster dùng `ENV_Architecture` (alpha đỉnh 0, UV ghim 0.5); Waterfall Surface, Stream Surface dùng `ENV_WaterFlow` (giữ UV gốc). Sinh bằng `scripts/qa/vis4b/stage_vis3c_v2_fbx.py` (đã thêm bảng quy tắc nước). Round-trip: tris khớp, bounds lệch 0 mm.
- `.meta` của các file mới chỉ ghi `guid` (để GUID cố định giữa các máy). Unity sẽ điền phần importer khi mở; cần commit lại các `.meta` đó.

### 14.2 Shader nước chảy `Dravio/Environment/Painterly Flow`
- File mới `Shaders/DravioPainterlyFlow.shader`; **không sửa** `Painterly Water` (hồ, foam bờ theo alpha đỉnh, `_DravioRipples`).
- Một material `M_ENV_WaterFlow` cho cả hai mặt nước, nên tổng material mesh nếu đặt đủ là 6/6 (giới hạn Origin). Alpha đỉnh chọn bề mặt: 1 = thác (silk + dải foam flipbook ở đáy), 0 = suối (ripples). 3 lần lấy mẫu texture, 1 pass, trong suốt, không cần depth/opaque texture, có fog và instancing; mọi thuộc tính số nằm trong `UnityPerMaterial` (SRP Batcher).
- Hướng chảy: trong Unity, v của thác = 1 ở đỉnh, nên nước chảy về −v (đo trên FBX: tương quan v với độ cao −0.97 trong không gian cục bộ, xem `vis3c_v2_fbx_trial.json`). Suối chảy từ đầu gần thác ra xa theo cùng chiều −v.
- `Cull Off` mặc định, nên lỗi "Waterfall Surface quay lưng camera" (mục 13.2) không còn chặn; mặt sau được đảo pháp tuyến khi tính sáng. AI3 vẫn có thể xoay mesh nếu muốn.
- Foam: atlas 4×4, khung theo hàng từ góc trên trái, clamp; mỗi ô ngang chạy lệch pha 5 khung để không nhấp nháy đồng loạt. Kiểm bằng mô phỏng CPU trong không gian UV (`vis4b/vis10/flow_waterfall_uvspace_t0-0.75.png`, `flow_stream_uvspace_t0-1.png`): silk trôi xuống, foam nằm ở dải đáy, mép hai bên mờ dần. Đây là kiểm toán học, **không phải render Unity**.
- Import texture: `T_ENV_WaterFlow_Silk/Ripples` (256, repeat) và `T_ENV_WaterFlow_Foam` (512, clamp) trong `Textures/`, giữ alpha, ASTC 6×6 trên Android/iOS (`EnvironmentImportSettings`, version 6 → 7).
- Không làm particle foam riêng: dải foam trong shader rẻ hơn và không cần object mới trong scene.

### 14.3 Ngân sách texture Origin (đề xuất: 14 MiB)
- Cách đo giống Ocean/Sky: RGBA32 + mips (`w × h × 4 × 1.333`) cho texture mà material của scene dùng. Ocean/Sky là 6 MiB; Origin là khu chính nên đề xuất **14 MiB**.
- Hiện tại: 5 texture kit 11.0 MiB + 3 texture VIS.9 2.0 MiB = **13.0 / 14 MiB**, còn khoảng 1 MiB dự phòng. Trên máy dùng ASTC 6×6, thực tế chỉ khoảng 1/9 số đó (khoảng 1.5 MiB).
- Có cổng kiểm tra: `OriginTextureBudgetBytes` trong builder, in OK/OVER trong "Report Budget"; `check_vis4b_integration.py` mục 5 kiểm cùng con số mà không cần Unity.

### 14.4 QA VIS.10 (#92 `e0f5b13`, `00c5bfe`): đã merge vào nhánh QA, **chưa đưa vào Assets**
- glTF-Validator: 0 lỗi cả 3 GLB (bloom/fern có info `UNUSED_OBJECT TEXCOORD_0`, vô hại).
- Gió (`check_vis10_wind.py`, viết lại hàm `DravioWind()` của shader bằng numpy; 3 456 tư thế/mesh: base 0.5/1/2.2, gust 0/1, 8 hướng, 24 thời điểm, 3 vị trí world): rễ alpha 0, đỉnh trùng vị trí cùng alpha (không rách seam), tương quan alpha với độ cao 0.99. Dịch chuyển lớn nhất 5.1 cm; dot pháp tuyến nhỏ nhất 0.990 (bloom) / 0.940 (fern); tỉ lệ diện tích 0.86–1.14. Khớp số AI3 báo.
- Bướm: 384 tris, 0 mặt suy biến, clip `Flutter_Loop` 0.6 s, 9 key/cánh, quaternion đơn vị, key đầu = key cuối, 0 texture.
- **Chưa tích hợp vì:** (1) cần thêm material thực vật có gió, sẽ thành 7/6 material của Origin; (2) importer của kit tắt animation (`importAnimation = false`), nên bướm cần thư mục/quy tắc import riêng và hành vi bay; (3) cần duyệt bố cục. Không dùng chung được `M_ENV_Foliage` vì material đó dùng texture alpha-clip; cần chủ dự án duyệt material thứ 7 hoặc nâng giới hạn.

### 14.5 Kết quả chạy
- `check_vis4b_integration.py`: 0 failure (thêm: shader Flow trong CBUFFER, thuộc tính 2 material mới tồn tại trong shader, GUID texture trỏ đúng file, ngân sách texture 13.0/14 MiB, slot FBX hợp lệ, có `.meta`).
- `test_environment_kit` 23 test OK; `test_vis1` OK. Kiểm cú pháp Roslyn (C# 9) cho 2 file Editor đã sửa: 0 lỗi.
- Tris nếu đặt mỗi asset mới một lần: 34 472 + 7 212 (nhà, tháp, đồi) + 3 560 (thác, suối) = **45 244 / 60 000**.
- **NOT RUN:** compile Unity và shader, import FBX/texture, Game View, ảnh chụp, FPS/draw call, thiết bị Android, phát animation bướm.
