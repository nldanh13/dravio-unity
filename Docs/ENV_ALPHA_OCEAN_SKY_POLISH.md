# ENV Alpha — Ocean / Sky texture budget, Sky perch collider, lighting polish (AI 3)

Issue: https://github.com/nldanh13/dravio-unity/issues/79. Nhánh `feat/env-alpha-ocean-sky-polish`, tạo từ nhánh tích hợp `feat/u5-6-player-journey` @ `02e25cf` (baseline QA của chủ dự án). Độc lập với CHAR.9b (#76 / #78): không chạm Character / Art/Models.

## 0. Trạng thái — tách bạch nơi đã kiểm

| Hạng mục | Đã kiểm ở đây | Unity `Validate Ocean and Sky` | Thiết bị (ASTC) |
|---|---|---|---|
| Texture ≤ 6 MiB RGBA32 / zone | **11,12 → 5,87 MiB** mỗi zone, tính đúng công thức của validator (`w × h × 4 × 1,333`) trên đúng 8 texture mà validator đếm (tổng "trước" = 11,1 MiB như báo cáo chủ dự án) | **PENDING** (không có Unity ở môi trường này) | **NOT MEASURED** |
| `sky_low_island.perch_06` | đỉnh collider **4,27 → 4,494** so với Y 4,49 (Δ 0,22 → 0,004), mô phỏng đúng ray cast của validator, dung sai 0,20 giữ nguyên | **PENDING** | — |
| Ánh sáng / màu 3 thế giới | ảnh preview Blender Cycles trước/sau, cùng camera ortho cố định của Unity, cùng exposure (mục 4) | **PENDING** — cần ảnh Game View của chủ dự án | — |
| Framing 42 / 23 điểm × 12 trường hợp | không đổi vị trí nào (dữ liệu ID / spawn / perch / volume giống hệt, chỉ thêm 1 chướng ngại nằm trong footprint cũ) | **PENDING** | — |

Không có mục nào được ghi PASS cho Unity. Ngưỡng không đổi: 60 000 tam giác, 10 vật liệu, 6 MiB, dung sai collider 0,20, framing 12 trường hợp (`ZoneEnvironmentBuilder.cs` hằng số giữ nguyên).

## 1. Texture Ocean / Sky (≤ 6 MiB RGBA32 + mip, theo validator hiện hành)

Nguyên nhân: `PropsAtlas` 1024² một mình đã 5,33 MiB. Sửa ở **generator** (`scripts/art/environment/zone_textures.py`): mọi map vẫn vẽ ở kích thước gốc (cùng seed, cùng cọ), rồi lọc hộp (trung bình vùng, tất định) xuống `OUTPUT_SIZE`. UV là tỉ lệ của atlas nên không mesh nào phải unwrap lại. Ground (diện tích màn hình lớn nhất) và Foliage (mép alpha-clip) giữ nguyên.

| Texture (Ocean) | Trước | MiB | Sau | MiB |
|---|---|---|---|---|
| `T_ENV_Ocean_PropsAtlas` | 1024×1024 | 5,33 | **512×512** | 1,33 |
| `T_ENV_Ocean_PropsEmission` | 512×512 | 1,33 | **128×128** | 0,08 |
| `T_ENV_Ocean_Foliage` | 1024×512 | 2,67 | 1024×512 | 2,67 |
| `T_ENV_Ocean_Ground` | 512×512 | 1,33 | 512×512 | 1,33 |
| `T_ENV_FX_Caustics` | 256×256 | 0,33 | 256×256 | 0,33 |
| `T_ENV_FX_LightShaft` | 64×256 | 0,08 | 64×256 | 0,08 |
| `T_ENV_FX_Bubble` | 64×64 | 0,02 | 64×64 | 0,02 |
| `T_ENV_FX_SoftDot` (Origin, dùng chung) | 64×64 | 0,02 | 64×64 | 0,02 |
| **Tổng / ngân sách 6** | | **11,12** | | **5,87** |

| Texture (Sky) | Trước | MiB | Sau | MiB |
|---|---|---|---|---|
| `T_ENV_Sky_PropsAtlas` | 1024×1024 | 5,33 | **512×512** | 1,33 |
| `T_ENV_Sky_PropsEmission` | 512×512 | 1,33 | **128×128** | 0,08 |
| `T_ENV_Sky_Foliage` | 1024×512 | 2,67 | 1024×512 | 2,67 |
| `T_ENV_Sky_Ground` | 512×512 | 1,33 | 512×512 | 1,33 |
| `T_ENV_Sky_Water` | 256×256 | 0,33 | 256×256 | 0,33 |
| `T_ENV_FX_CloudWisp` | 128×128 | 0,08 | 128×128 | 0,08 |
| `T_ENV_FX_Petal` | 64×64 | 0,02 | 64×64 | 0,02 |
| `T_ENV_FX_SoftDot` (Origin, dùng chung) | 64×64 | 0,02 | 64×64 | 0,02 |
| **Tổng / ngân sách 6** | | **11,12** | | **5,87** |

File PNG: atlas 1 077 → 396 KB (Ocean), 1 005 → 381 KB (Sky). Test mới `test_unity_rgba32_estimate_fits_the_validator_budget` tính đúng công thức validator trên đúng bộ texture này.

- Không dùng nén để "che" con số: validator đếm RGBA32 không nén; số trên là kích thước texture thật sau import (nguồn PNG đã nhỏ lại, `.meta` / GUID giữ nguyên, importer không đổi). ASTC trên thiết bị nhỏ hơn 4–8 lần nhưng **chưa đo**.
- Độ đọc: `Docs/env-alpha/atlas_readability.jpg` (4 ô atlas ở cỡ hiển thị điện thoại trước/sau, và một ô 1:1). Mạch gạch, vết nứt, đốm san hô vẫn đọc được; emission 128² chỉ chứa 2/16 ô phát sáng (tinh thể / hải quỳ / chữ rune), vốn mờ.
- Mip: inset ô atlas 12 px ở 1024 thành 6 px ở 512, không lem ô tới mip 2 (ô 128 px → 32 px).

## 2. Collider `sky_low_island.perch_06`

Perch đặt trên **đá khóa vòm** cổng `SM_ENV_Sky_Gate` (scale 0,7). Vòm là nửa vòng tròn (chân vòm 3,88, bán kính 2,05), đỉnh đá khóa = 3,88 + 2,05 + 0,98/2 = 6,42 → 6,42 × 0,7 = **4,494** = Y của perch (dữ liệu đúng, mesh đúng — Blender ray cast 6,42). Collider chỉ có hộp dầm phẳng tới 6,1 → **4,27** (đúng con số Unity của chủ dự án). Sửa nguồn `sky_zone.py` `BLENDER_COLLIDERS`: thêm hộp đá khóa 0,8 × 0,86 × 0,98 tại z 5,93 (chính khối đá khóa) → đỉnh collider 4,494.

- `SkyColliders.json` thêm 1 hộp; `SkyLocations.json` thêm 1 chướng ngại (đỉnh 4,494) nằm **trong** footprint dầm cũ. Không đổi ID, WorldGraph, habitat, spawn, perch, transition, gate, volume.
- Prefab do `BuildPrefabs` thêm collider từ JSON → scene hiện có tự nhận qua prefab instance (không cần dựng lại scene).
- Test mới `test_box_collider_top_under_every_perch_and_rest_matches_its_height`: mô phỏng ray cast của validator cho mọi perch / rest có collider hộp ở cả hai zone, dung sai 0,20.

## 3. Ánh sáng, màu, độ sâu (chỉ tham số art)

Mọi thay đổi là tham số art (dữ liệu sống `*Living.json`, màu vật liệu / hạt trong builder). Không đổi vị trí, camera, safe area, UI, luật.

| Thế giới | Vấn đề (QA chủ dự án) | Trước → sau | Nguồn |
|---|---|---|---|
| Ocean | caustics / tia sáng trắng mạnh che chi tiết | caustics alpha 0,22 → **0,11**; 6 tia sáng 0,26…0,10 → **0,13…0,05**, hẹp hơn ~20 %; `fullLightIntensity` theo ánh sáng ngày thật của zone (thay hằng 1,05) | `ocean_zone.py` FX, `ZoneEnvironmentBuilder.BuildFx` |
| Ocean | tinh thể / hải quỳ cháy trắng | emission vật liệu Props 1,7 → **1,2** | `ZoneEnvironmentBuilder.SetupMaterials` |
| Ocean | phẳng, xanh đục | fill dưới nước giảm (trời 0,42/0,74/0,80 → 0,38/0,68/0,76; xích đạo → 0,22/0,45/0,52; đất → 0,12/0,20/0,24), key ×0,78 → ×0,82, bóng ×0,6 → ×0,72; sương 18–48 → **22–56** m | `ocean_zone.py` `_grade`, `FOG` |
| Sky | trắng cháy, mây thiếu tương phản | biển mây: shallow 0,97 → **0,90/0,92/0,98**, deep → 0,60/0,67/0,88, foam 1 → 0,95, lấp lánh 0,08 → 0,04; mây trôi α 0,55 → 0,42, hơi xanh; nền trời 0,70/0,82/0,96 → **0,60/0,75/0,93**; key ×1,06 → ×0,96; fill kéo về xanh trời tối hơn; sương 26–60 → **32–72** m | `ZoneEnvironmentBuilder`, `sky_zone.py` |
| Sky | glyph cháy | emission Props 1,7 → **1,3** | `ZoneEnvironmentBuilder` |
| Origin | phẳng | ban ngày: key 1,35 → **1,42**, ấm hơn chút (1/0,91/0,75); fill trilight giảm ~12 % (trời 0,80/0,86/0,95 → 0,70/0,78/0,92, xích đạo 0,66/0,72/0,58 → 0,58/0,64/0,50, đất 0,40/0,36/0,30 → 0,33/0,28/0,23); bóng 0,75 → **0,82**; khung 8 h và 16,5 h chỉnh cùng hướng; đêm / bình minh / hoàng hôn giữ nguyên | `living.py`, `OriginSanctuaryLiving.json/.asset`, `OriginSanctuaryEnvironmentBuilder` (ánh sáng tĩnh) |

Tỉ lệ key/fill tăng là thứ tạo độ sâu trên shader painterly (bóng tự đổ + đổ bóng mềm đọc được); màu chủ đạo của từng thế giới giữ nguyên.

## 4. Ảnh trước / sau

Blender 5.3 Cycles, **cùng** camera ortho cố định của Unity (`zone_common` / `layout` camera), 16:9, cùng exposure, cùng sample; "trước" = generator ở baseline `02e25cf`, "sau" = nhánh này. Đây là preview của pipeline art, **không** phải Unity Game View (shader painterly / URP, hạt, tia sáng động khác); ảnh Unity phải do chủ dự án chụp lại.

| Thế giới | Ảnh | Luma TB | Độ tương phản (σ luma) | Điểm ảnh cháy ≥ 250 | Độ bão hòa TB |
|---|---|---|---|---|---|
| Ocean | [`ocean_before_after.jpg`](env-alpha/ocean_before_after.jpg) | 136,7 → 131,3 | 16,1 → **17,5** | 0,01 % → 0,01 % | 81,6 → 85,0 |
| Sky | [`sky_before_after.jpg`](env-alpha/sky_before_after.jpg) | 212,1 → 202,7 | 39,4 → 40,5 | **18,37 % → 0,67 %** | 34,0 → **43,9** |
| Origin (giờ mặc định, dữ liệu sống) | [`origin_before_after.jpg`](env-alpha/origin_before_after.jpg) | 131,1 → 129,3 | 35,3 → 35,4 | 0,04 % → 0,04 % | 133,4 → 135,5 |

Đọc ảnh:
- **Ocean**: các cột sáng trắng còn mờ nhẹ, không còn phủ vòm, tường và cụm rong bên trái; đá, san hô, cát đọc rõ hơn. Màn sương xanh của preview (volume) vẫn dày hơn Unity.
- **Sky**: biển mây hết cháy trắng (18 % → < 1 % điểm ảnh bão hòa), vân mây hiện lại, đảo tách khỏi nền; mây và cỏ đậm màu hơn.
- **Origin**: thay đổi nhẹ và gần như không thấy trên preview Blender (Blender chỉ mô phỏng ambient bằng một màu nền, không có trilight / shader painterly). Hiệu quả thật (bóng 0,75 → 0,82, fill thấp hơn) chỉ đánh giá được trong Unity. Đại tu hình ảnh Origin là issue #80, không thuộc PR này.
- `atlas_readability.jpg`: độ đọc texture sau khi giảm (mục 1).
- Preview ban ngày "cố định" cũ của Origin dùng đèn U1-ENV.2 cứng nên không phản ánh dữ liệu sống; generator giờ render thêm khung `day_living` (giờ mặc định từ JSON) — dùng cho bảng trên.

## 5. Scene / asset đã sinh của chủ dự án

Trong lúc làm, chủ dự án đã đẩy scene và asset Ocean / Sky đã sinh (`83dddeb`, U6.0.3b) lên nhánh tích hợp; nhánh này đã merge vào và **cập nhật trực tiếp** các file đó, giữ nguyên GUID / fileID, không dựng lại scene:

| File | Thay đổi |
|---|---|
| `Zones/Sky/Prefabs/SM_ENV_Sky_Gate.prefab` | thêm 1 BoxCollider đá khóa (0,8 × 0,98 × 0,86 tại y 5,93) — scene nhận qua prefab instance |
| `M_ENV_Ocean_Props.mat`, `M_ENV_Sky_Props.mat` | `_EmissionColor` 1,7 → 1,2 / 1,3 |
| `M_ENV_Sky_Water.mat` | shallow / deep / foam / foam width / sparkle như mục 3 |
| `OceanLiving.asset`, `SkyLiving.asset`, `SkyLocations.asset` | sinh lại từ JSON; bộ ghi YAML được kiểm: đọc–ghi lại tái tạo đúng từng byte file cũ, và JSON cũ tái tạo đúng file cũ, rồi mới áp JSON mới |
| `Scenes/Ocean_Environment.unity` | alpha 7 quad FX, bề rộng 6 tia sáng, `fullLightIntensity` 1,05 → 1,1644, sương 18–48 → 22–56 (32 dòng) |
| `Scenes/Sky_Environment.unity` | sương 26–60 → 32–72, màu hạt `cloud_wisps` (6 dòng) |

Ánh sáng / ambient trong scene do `EnvironmentTimeOfDay` (`[ExecuteAlways]`) lấy từ `.asset` mỗi frame, nên không cần sửa đèn tĩnh trong scene.

Nếu cần sinh lại từ builder thay vì dùng file đã commit, chạy **Dravio ▸ Environment ▸ Zones ▸ Ocean / Sky – Build Everything** (không phải "Rebuild … (overwrite)"):
1. vật liệu (emission, mây) và prefab (collider perch_06) cập nhật tại chỗ, GUID giữ nguyên;
2. `OceanLiving.asset` / `SkyLiving.asset` / `…Locations.asset` nạp lại từ JSON (ánh sáng ngày–đêm đọc mỗi frame);
3. scene có sẵn được mở và `RefreshArtInOpenScene` (mới) chỉ cập nhật alpha / bề rộng quad caustics–tia sáng theo id, màu hạt mây theo id volume, khoảng sương — **không lưu tự động**, chủ dự án xem rồi tự lưu.
4. Origin: `OriginSanctuaryLiving.asset` đã đồng bộ trong commit (sinh lại từ JSON, cùng định dạng YAML, kiểm bằng cách tái tạo đúng file cũ trước khi áp). Ánh sáng tĩnh của `OriginSanctuaryEnvironmentBuilder` đổi cùng giá trị giờ mặc định (test so trực tiếp với C#). Scene Origin đã sinh vẫn dùng ánh sáng sống khi chạy; ánh sáng tĩnh trong Editor cũ đến khi chủ dự án dựng lại scene môi trường Origin.

Sau đó: mở từng scene, **Validate Ocean and Sky** riêng với mỗi scene đang active; mong đợi 0 problem, ≤ 60k tam giác, ≤ 10 vật liệu, ~5,9 / 6 MiB, 42 / 23 điểm framing trong 12 trường hợp. Phối hợp với AI 1 khi chủ dự án đẩy baseline scene hiện tại.

## 6. Kiểm thử

- `python3 -m unittest` cho `scripts/art/environment/test_environment_kit.py`, `test_living_environment.py`, `test_zone_environments.py`: **OK** (`test_environment_kit` OK, `test_living_environment` 30 OK, `test_zone_environments` 31 OK). Test mới: ước tính RGBA32 của validator ≤ 6 MiB; đỉnh collider dưới mọi perch / rest (dung sai 0,20); ánh sáng tĩnh Origin = giờ mặc định (đọc thẳng C#); file Unity đã commit (asset, prefab, scene, vật liệu) = JSON nguồn.
- Generator tái tạo đúng từng byte texture / dữ liệu / audio đang commit ở baseline trước khi sửa (chỉ `*Manifest.json` lệch 4 tam giác do metaball mây — không chép model hay manifest).
- Không đổi: Domain, luật gameplay, Save Schema, UI, camera, WorldGraph, vị trí tương tác; FBX model, audio, GUID / `.meta`.
