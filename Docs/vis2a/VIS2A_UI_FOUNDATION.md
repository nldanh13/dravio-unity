# VIS.2a — Responsive UI foundation & art integration readiness (Issue #86)

Baseline: `feat/u5-6-player-journey` @ `e075075` (HEAD tại thời điểm bắt đầu). Theo Art Style Guide v1.0 (PR #85, **chưa merge**): Cute Stylized 3D · Bright Fantasy · polished casual fantasy UI.

**Trạng thái kiểm thử: Unity Editor/Play Mode/Game View = NOT RUN.** Môi trường làm việc không có Unity 6000.6.10f1. Chưa có ảnh Game View trước/sau. Chỉ các kiểm tra nguồn ở mục 9 đã thực sự chạy.

## 1. Phạm vi và hợp đồng giữ nguyên
- Không đổi gameplay, Domain, Persistence, save, economy, rig/animation, environment/scene generated, IDs, callback, localization Việt/Anh, camera.
- Không thêm artwork chính thức, không thêm font, không đổi art direction. Không dùng PR #84 (vector-only), không đưa PR #83/#85 vào nhánh.
- Không sửa `ArtSource/`. Không sửa scene `.unity` nào. Không đổi hàng loạt `Text` sang TMP.
- Khi **không gán theme**, HUD chạy đúng như U6.0.4b (cùng code path cũ). Hình học layout được chuyển vào `HudLayoutPolicy` với **cùng con số** (có test khóa lại).

## 2. Audit HUD hiện tại (đọc nguồn, chưa chạy Unity)
HUD được build bằng `Assets/Dravio/Editor/FantasyPreviewHud.cs`, runtime ở `Assets/Dravio/Scripts/FantasyPreviewUI.cs`.
- Cấu trúc: Canvas Screen Space Overlay → `Safe area` (anchor theo `Screen.safeArea`) → `Header` (48u, đỉnh) + `Sanctuary drawer` (rail phải, cách đỉnh 72u, cách đáy/phải 12u).
- Màn hình: hàng tab 1 **Care / Biology / Dragons**; hàng tab 2 (playable) **Guide / Eggs / Manage / Log**; trang Log = Research với 3 mode **Codex / Observe / Quests**. Mỗi trang là `ScrollRect` + `RectMask2D` + `VerticalLayoutGroup` + `ContentSizeFitter`.
- CanvasScaler: builder đặt `Expand` 1280×720, runtime ghi đè `MatchWidthOrHeight=1` (khớp chiều cao); compact 960×540 trên mobile hoặc cửa sổ cao < 600 px. Portrait không hỗ trợ (mobile khóa landscape).
- Focus/camera: `SanctuaryUiInsetSource` trên drawer báo bề rộng thật cho `SanctuaryCameraFit`; ẩn drawer → camera trả về bề rộng thiết kế. VIS.2a không đổi phần này.

Ma trận layout (tính bằng `HudLayoutPolicy`, đúng công thức runtime; notch mô phỏng 5,5% bề rộng):

| Màn hình | Notch | Ref | Scale | Safe (u) | Drawer (u) | Vùng thế giới trống | Ô tab (4 tab) | 44u = px |
|---|---|---|---|---|---|---|---|---|
| 1280x720 (Editor/PC) | — | 1280×720 | 1.00 | 1280 | 326 | 74% | 73u | 44 |
| 1280x720 (Editor/PC) | trái/phải 5,5% | 1280×720 | 1.00 | 1210 | 326 | 72% | 73u | 44 |
| 1920x1080 (PC) | — | 1280×720 | 1.50 | 1280 | 326 | 74% | 73u | 66 |
| 2400x1080 (phone 20:9) | — | 960×540 | 2.00 | 1200 | 380 | 67% | 86u | 88 |
| 2400x1080 (phone 20:9) | trái/phải 5,5% | 960×540 | 2.00 | 1134 | 380 | 65% | 86u | 88 |
| 2560x1440 (PC) | — | 1280×720 | 2.00 | 1280 | 326 | 74% | 73u | 88 |
| 1024x768 (tablet 4:3) | — | 960×540 | 1.42 | 720 | 280 | 59% | 61u | 63 |
| 1024x768 (tablet 4:3) | trái/phải 5,5% | 960×540 | 1.42 | 680 | 280 | 57% | 61u | 63 |
| 1024x768 (Editor 4:3) | — | 1280×720 | 1.07 | 960 | 326 | 65% | 73u | 47 |

Kết luận audit (nguồn):
1. Ở mọi mục tiêu, drawer ≤ 48% safe width, vùng thế giới trống ≥ 57%, ô tab ≥ 44u, header (brand + vàng + ngôn ngữ + ẩn) vừa bề rộng. Có test khóa các điều kiện này.
2. 4:3 là trường hợp chật nhất: drawer chiếm ~39%, ô tab hàng 2 còn 61u; nhãn “Hướng dẫn” dựa vào best-fit (16→12 pt). Cần xem trên Game View.
3. 44u trên điện thoại 2400×1080 ≈ 88 px ≈ 5,6 mm ở ~400 dpi: dưới mức 7–9 mm thường khuyến nghị. **Cần đo trên thiết bị** trước khi quyết định tăng.
4. Toàn bộ chữ dùng `UnityEngine.UI.Text` + `LegacyRuntime.ttf`, nhiều nhãn bật `resizeTextForBestFit`. Trên Android/iOS, font legacy dựa vào font hệ thống để có dấu tiếng Việt (khác nhau theo máy) và best-fit làm rebuild layout tốn CPU. → lý do cho kế hoạch TMP ở mục 6.
5. Mode Codex/Observe/Quests trong Research chưa có trạng thái active riêng; theme hiện coi chúng là nút thường (giới hạn đã biết).

## 3. Kiến trúc mới
`Assets/Dravio/UI/` (không asmdef, cùng Assembly-CSharp với HUD):

| File | Vai trò |
|---|---|
| `Runtime/HudLayoutPolicy.cs` | Hình học HUD landscape (thuần C#, test bằng dotnet). FantasyPreviewUI.Layout gọi vào đây. |
| `Runtime/HudSafeAreaOverride.cs` | Mô phỏng notch trái/phải trong Editor/Development build. Release luôn dùng `Screen.safeArea`. |
| `Runtime/DravioUiRole.cs` | Enum vai trò sprite/chữ (giá trị số cố định, đã serialize). |
| `Runtime/DravioUiTheme.cs` | ScriptableObject theme: sprite + state + border units + tint + fallback cho từng role; preset Legacy và Bright Fantasy v1 (đề xuất). |
| `Runtime/DravioUiSkin.cs` | Áp theme lên uGUI có sẵn: 9-slice, preserveAspect, fallback an toàn, tab indicator, giữ vùng hit. |
| `Runtime/DravioPressFeedback.cs` | Nhấn: scale con 0,97, không scale vùng hit; không Update loop. |
| `Runtime/DravioProgressBar.cs` | Frame + fill + highlight tách riêng; Filled hoặc Masked (giữ bo tròn). |
| `Runtime/DravioUiSkinned.cs` | Gắn role cho UI mới/tự dựng. |
| `Runtime/UiSpritePolicy.cs` | Luật border, pixelsPerUnitMultiplier, import size, sample ratio, atlas padding (thuần C#). |
| `Runtime/VietnameseGlyphs.cs` | Bộ ký tự tiếng Việt bắt buộc (134 chữ + ASCII), kiểm NFC. |
| `Data/ui-sprite-slots.json` | **Hợp đồng sprite với AI3** (mục 5). |
| `Editor/DravioUiSpriteImporter.cs` | Import profile tự động cho `Assets/Dravio/UI/Sprites/**`. |
| `Editor/DravioUiValidator.cs` | `Dravio > UI > Validate UI Sprites`, `Dravio > UI > QA > Print HUD Layout Matrix`. |
| `Editor/DravioUiThemeMenu.cs` | Tạo/gán theme, mô phỏng notch, chụp Game View. |
| `Editor/DravioTmpVietnameseTools.cs` | Audit font TMP cho tiếng Việt + scene mẫu độc lập. |

Thay đổi file cũ duy nhất: `Assets/Dravio/Scripts/FantasyPreviewUI.cs` (thêm field `theme` tùy chọn, nhánh `ApplyTheme`, layout qua policy, safe area qua override). `Docs/`, `.gitignore` (`/Screenshots/`), test csproj.

## 4. Theme API
```csharp
// Editor: Dravio > UI > Theme > Create Bright Fantasy v1 Theme (proposed)  → Assets/Dravio/UI/Themes/*.asset
// Editor: chọn theme → Dravio > UI > Theme > Assign Selected Theme To Open HUD (chỉ đánh dấu scene dirty, không tự save)
fantasyPreviewUI.SetTheme(theme);                       // runtime swap
DravioUiSkin.ApplyButton(button, theme, DravioUiRole.RewardButton);
DravioUiSkin.ApplyTab(tabButton, theme, active:true);    // màu/sprite + indicator hình (không chỉ màu)
DravioUiSkin.ApplySurface(image, theme, DravioUiRole.Panel);
DravioUiSkin.ApplyText(text, theme, DravioTextRole.Title);
progressBar.SetValue01(0.4f);                          // chỉ hiển thị, không tính toán progress
```
Quy tắc vẽ:
- Role sliced → `Image.Type.Sliced`, `pixelsPerUnitMultiplier = border·refPPU / (spritePPU·borderUnits)` **theo từng sprite**, nên border đúng kích thước thiết kế kể cả khi import bị giảm size.
- Icon / portrait / nút tròn → `Simple` + `preserveAspect`.
- Thiếu sprite, hoặc border không dùng được (0, hoặc trái+phải ≥ rộng) → màu phẳng fallback + 1 cảnh báo; **không bao giờ kéo méo góc**.
- Nút có art: Image gốc giữ làm vùng hit (alpha 0, `cullTransparentMesh`), art nằm ở con `Skin visual` (không raycast). State: ColorTint (pressed .86, disabled giảm sáng, chữ vẫn đọc được) hoặc SpriteSwap nếu có `_pressed/_disabled`.
- Map HUD hiện tại: tab → Tab/TabActive; Ngôn ngữ/Ẩn → Secondary; `Claim pack…` → Reward; còn lại → Primary; drawer → Panel; header → HeaderBar; viewport trang → PageBackground; chữ ≥18 → Title, ≥15 → Body (giống U6.0.4b).
- Preset Legacy = đúng màu U6.0.4b; khác biệt duy nhất khi gán là có thêm thanh indicator dưới tab active. Preset Bright Fantasy v1 lấy màu từ guide mục 02/06 — **chỉ là đề xuất**, AI3/chủ dự án duyệt.

## 5. Danh sách slot sprite cho AI3
Nguồn sự thật: `Assets/Dravio/UI/Data/ui-sprite-slots.json`. Path dưới `Assets/Dravio/UI/Sprites/`. State tùy chọn thêm hậu tố `_pressed`, `_disabled`. **Border là đề xuất, phải đo trên sprite thật**; importer chỉ chép border từ JSON khi sprite chưa có border (Sprite Editor luôn thắng). Chữ không được bake vào ảnh.

| ID | Role | Path | Nguồn | 9-slice | Border px (L,B,R,T) | Border hiển thị (u) | Import max | States |
|---|---|---|---|---|---|---|---|---|
| btn_primary | PrimaryButton | `Buttons/ui_btn_primary.png` | 1024×384 | có | 88,88,88,88 | 16 | 512 | normal, pressed, disabled |
| btn_secondary | SecondaryButton | `Buttons/ui_btn_secondary.png` | 1024×384 | có | 88,88,88,88 | 16 | 512 | normal, pressed, disabled |
| btn_reward | RewardButton | `Buttons/ui_btn_reward.png` | 1024×384 | có | 88,88,88,88 | 16 | 512 | normal, pressed, disabled |
| btn_danger | DangerButton | `Buttons/ui_btn_danger.png` | 1024×384 | có | 88,88,88,88 | 16 | 512 | normal, pressed, disabled |
| btn_close | CloseButton | `Buttons/ui_btn_close.png` | 512×512 | không (Simple) | — | — | 128 | normal, pressed, disabled |
| btn_back | BackButton | `Buttons/ui_btn_back.png` | 512×512 | không (Simple) | — | — | 128 | normal, pressed, disabled |
| tab | Tab | `Tabs/ui_tab.png` | 512×256 | có | 80,80,80,80 | 14 | 256 | normal |
| tab_active | TabActive | `Tabs/ui_tab_active.png` | 512×256 | có | 80,80,80,80 | 14 | 256 | normal |
| tab_indicator | TabIndicator | `Tabs/ui_tab_indicator.png` | 256×64 | không (Simple) | — | — | 64 | normal |
| panel | Panel | `Panels/ui_panel.png` | 1024×1024 | có | 128,128,128,128 | 24 | 512 | normal |
| header | HeaderBar | `Panels/ui_header.png` | 1024×256 | có | 96,96,96,96 | 18 | 512 | normal |
| page_bg | PageBackground | `Panels/ui_page_bg.png` | 512×512 | có | 64,64,64,64 | 12 | 256 | normal |
| card | Card | `Cards/ui_card.png` | 512×512 | có | 88,88,88,88 | 16 | 256 | normal |
| slot_common | SlotCommon | `Cards/ui_slot_common.png` | 512×512 | có | 88,88,88,88 | 16 | 256 | normal |
| slot_rare | SlotRare | `Cards/ui_slot_rare.png` | 512×512 | có | 88,88,88,88 | 16 | 256 | normal |
| slot_epic | SlotEpic | `Cards/ui_slot_epic.png` | 512×512 | có | 88,88,88,88 | 16 | 256 | normal |
| slot_legendary | SlotLegendary | `Cards/ui_slot_legendary.png` | 512×512 | có | 88,88,88,88 | 16 | 256 | normal |
| progress_frame | ProgressFrame | `Progress/ui_progress_frame.png` | 1024×128 | có | 56,56,56,56 | 10 | 512 | normal |
| progress_fill | ProgressFill | `Progress/ui_progress_fill.png` | 1024×128 | có | 40,40,40,40 | 7 | 512 | normal |
| progress_highlight | ProgressHighlight | `Progress/ui_progress_highlight.png` | 1024×128 | không (Simple) | — | — | 512 | normal |
| divider | Divider | `Decor/ui_divider.png` | 512×128 | có (chỉ ngang) | 160,0,160,0 | 8 | 128 | normal |
| portrait | Portrait | `Portraits/ui_portrait_frame.png` | 512×512 | không (Simple) | — | — | 256 | normal |

Icon chức năng (32): `Icons/ui_icon_{id}.png`, 512², chiếm 75–85% canvas, kiểm ở 24/32/48/64 px, import 256. ID: care, biology, dragons, eggs, world, quests, expedition, inventory, feed, pet, rest, inspect, build, research, health, settings, day, night, lock, done, warning, upgrade, reward, hunger, water, energy, temperature, bond, leaf, close, back, next.
Vật phẩm (17): `Items/ui_item_{id}.png`, 512², import 256: gold, wood, seed, stone, crystal, coral, stardust, fruit, leaf, aquatic, aether, pack_starter, pack_elemental, pack_mystic, pack_astral, blueprint, egg.

Yêu cầu cho sprite 9-slice: vùng giữa chỉ có texture nhẹ (không họa tiết độc nhất); góc trang trí lớn tách sprite overlay; mỗi sprite có border riêng; nền PNG alpha thật, không checkerboard, không viền màu nền.

## 6. TextMeshPro/SDF — kế hoạch an toàn
- `com.unity.ugui` 2.6.0 đã chứa TextMeshPro (Unity 6); không cần thêm package. Cần import *TMP Essential Resources* một lần khi Unity hỏi.
- Bộ ký tự bắt buộc `VietnameseGlyphs.Required`: ASCII in được + • – — … ’ “ ” × → ← · ° + 134 chữ tiếng Việt dựng sẵn (12 nguyên âm × 6 thanh × 2 hoa/thường, Đ/đ). Test xác nhận toàn bộ chuỗi trong HUD là NFC và nằm trong bộ này.
- Công cụ: `Dravio > UI > TMP > Audit Selected Font For Vietnamese` (chọn TTF/OTF hoặc TMP Font Asset; báo ký tự thiếu, cả chuỗi HUD thật). `… > Create Isolated Vietnamese Sample From Selected Font` tạo font asset SDF dynamic (atlas 2048, ~4 MiB Alpha8, đã nạp sẵn bộ tiếng Việt) và scene riêng `Assets/Dravio/UI/Samples/TmpVietnameseSample.unity` so sánh Legacy vs TMP ở 14/16/20/28 pt. Không đụng HUD.
- Font: **chưa thêm font nào**. Đề xuất ứng viên OFL có tiếng Việt để AI3 chọn theo style: Be Vietnam Pro (thân chữ), Nunito hoặc Baloo 2 (tiêu đề tròn, dễ thương). Phải kèm file license (OFL.txt) khi commit vì repo công khai.
- Lộ trình đổi: (1) chọn font + chạy audit + scene mẫu trên thiết bị; (2) thêm component song song cho một màn hình (Guide) giữ `Text` cũ ẩn để rollback; (3) mở rộng `FantasyPreviewUI.Copy` để nhận `TMP_Text` (giữ serialized `Text` references); (4) chỉ đổi builder sau khi QA hai ngôn ngữ. Không đổi hàng loạt trước bước (1)–(2).

## 7. Import profile & checklist
Importer tự áp cho `Assets/Dravio/UI/Sprites/**/*.png` (luôn): Sprite Single, FullRect, sRGB, alpha from input + alphaIsTransparency, **mipmap off**, **Clamp**, Bilinear, NPOT none, not readable. Lần import đầu: PPU 100, max size theo bảng mục 5, Standalone RGBA32 không nén, Android/iOS **ASTC 4×4** (viền UI sạch). Sau đó QA có thể chỉnh từng slot.

Checklist cho mỗi asset (AI3 → kỹ thuật):
- [ ] PNG RGBA 8-bit, straight alpha, sRGB, đúng kích thước nguồn trong JSON, tên đúng path (file sai tên bị validator báo “unmapped”, không dùng trực tiếp).
- [ ] Bản nguồn gốc giữ nguyên trong `ArtSource/` (ghi provenance/license trong manifest ArtSource); chỉ bản runtime đã duyệt vào `Assets/Dravio/UI/Sprites/`.
- [ ] Border đo trên sprite thật (Sprite Editor), không dùng một border cho mọi ảnh.
- [ ] `Dravio > UI > Validate UI Sprites` sạch: không upscale (ratio ≥ 1 ở 2560×1440/điện thoại 1440p), không >3× ở 720p với sprite 9-slice.
- [ ] Atlas: padding 4 px (không mip) / 8 px (mip hoặc nén block); tách atlas theo màn hình nếu > 2048.
- [ ] Kiểm actual display pixels bằng ảnh chụp Game View 100% (không zoom Game View).

Giới hạn vật lý: một bản import phục vụ scale 1,0 → 2,67, nên ở 1280×720 desktop sprite luôn bị thu nhỏ ≥ 2,67×. Border đề xuất đã chọn để giữ ≤ 3× (trừ divider). Nếu 720p desktop thấy răng cưa, phương án là bản `@1x` riêng cho Standalone, không bật mipmap cho UI.

## 8. Kế hoạch QA trong Unity (NOT RUN)
1. Mở scene playable hiện có (không regenerate). Play, so sánh **không gán theme** với U6.0.4b: phải giống hệt.
2. `Create Legacy Theme` → gán → kỳ vọng giống cũ + indicator tab. `Create Bright Fantasy v1` → gán → chỉ màu fallback (chưa có sprite).
3. Game View 1280×720, 1920×1080, 2400×1080, 2560×1440, 1024×768; mỗi size: notch None/Left/Right (`Dravio > UI > QA > Simulate Notch`) và Device Simulator; chụp `Dravio > UI > QA > Capture Game View` (vi/en).
4. Mỗi size: chuyển 7 tab + 3 mode Research, đổi ngôn ngữ, Ẩn/Mở drawer, claim trứng (meadow/storage), Focus + pinch không zoom khi chạm UI, cuộn hết trang dài, chữ không tràn/cắt.
5. Thêm sprite thử (do AI3 cung cấp) → Validate → Fill theme → kiểm 9-slice không méo ở 4:3 và 20:9, nút pressed/disabled, tab active.
6. Profiler trên thiết bị: draw calls/batches của Canvas, layout rebuild khi đổi tab, memory texture UI.

## 9. Kiểm thử đã thực sự chạy
- `dotnet test Assets/Dravio/Tests/Integration~` (.NET 8, NUnit): **173/173 pass** (160 test cũ + 13 test VIS.2a mới trong `UiFoundationTests.cs`): số layout trùng U6.0.4b, safe area/notch, drawer ≤ 48%, vùng thế giới ≥ 50%, ô tab ≥ 44u, header vừa, multiplier border, import size không upscale, hợp đồng JSON (ID/path/role/border/size), bộ ký tự tiếng Việt, chuỗi HUD là NFC và đủ glyph, bảng role.
- Type-check ngoài Unity: biên dịch code runtime + editor mới và `FantasyPreviewUI.cs` với `UnityEngine.dll`/`UnityEditor.dll` 2021.1 (NuGet `Unity3D.SDK`) + stub chữ ký uGUI/TMP. Lỗi còn lại chỉ là API mới hơn 2021.1 (`FindAnyObjectByType`, `FindObjectsInactive`, `TextureImporter.GetSourceTextureWidthAndHeight`) có trong Unity 6 (repo đã dùng `FindAnyObjectByType`). **Đây không phải là biên dịch Unity 6000.6.10f1**; chữ ký uGUI/TMP là stub viết tay.
- NOT RUN: Unity compile, Play Mode, Game View capture, Device Simulator, thiết bị thật, profiler, TMP font generation.

## 10. Rủi ro / giới hạn còn tồn tại
- Chưa compile trong Unity 6: rủi ro thấp nhưng có thật với API uGUI/TMP (đặc biệt `TMP_FontAsset.CreateFontAsset` overload và `HasCharacter`).
- Chưa có ảnh trước/sau; chưa có sprite thật nên border/size chỉ là đề xuất.
- Mode Codex/Observe/Quests chưa có trạng thái active trong theme. `CarePreviewHud` (scene preview cũ) không nằm trong phạm vi.
- Theme gán ở runtime không tự “gỡ” về legacy khi set null giữa phiên (cần reload scene).
- Touch target 44u ≈ 5,6 mm trên điện thoại 20:9 cần đo trên máy thật.
