# U1-CHAR — Quy trình tài nguyên nhân vật rồng

Quy trình chuẩn để đưa một mô hình rồng (GLB) vào Unity cho game mobile màn hình ngang, dùng chung cho 16 loài và 3 giai đoạn Baby / Juvenile / Adult.

> Trạng thái: công cụ đã chạy end-to-end bằng Blender 5.3 (`bpy`) trên **tài nguyên kiểm thử tự sinh** (`TEST_*`). Chưa có GLB thật nào của Dravio được xử lý, và phần Unity chưa được biên dịch hay chạy trong Unity Editor.

> Nhập hàng loạt GLB (Hunyuan3D), archetype rig/animation theo sinh học, prop môi trường và runtime hiển thị rồng sống: xem [`LIVING_DRAGONS.md`](LIVING_DRAGONS.md).

## Nguyên tắc

1. **Không ghi đè mô hình gốc.** Mọi công cụ chỉ ghi vào thư mục output, từ chối ghi lên file nguồn, và ghi SHA-256 của nguồn trước và sau khi chạy (`source_unchanged`).
2. **Không đổi đặc điểm sinh học.** Đặc điểm loài lấy từ `Assets/Dravio/Data/species-biology.json` và `SpeciesCatalog.cs` qua `Data/species-character-spec.json` (tự sinh). Điều gì dữ liệu gốc không nói thì để trống, không tự bịa.
3. **Không dùng tài nguyên không rõ giấy phép.** Mô hình test được sinh hoàn toàn bằng code trong repo.
4. **Một PASS chỉ là cổng kỹ thuật** (số tam giác, UV, trọng số, colour space…). Duyệt hình ảnh, animation và hiệu năng trên thiết bị vẫn phải làm thủ công.

## Đặt tên và thư mục

| Thứ | Quy ước |
|---|---|
| Tên nhân vật | `DRG_<speciesId>_<Stage>`, ví dụ `DRG_emberling_Baby` (`speciesId` đúng ID canonical) |
| Mesh LOD | `<mesh>_LOD0`, `_LOD1`, `_LOD2` (Unity tự nhận LOD theo hậu tố) |
| Texture | `<Name>_BaseColor.png` (sRGB), `<Name>_Normal.png` (OpenGL +Y), `<Name>_MetallicSmoothness.png` (R = metallic, A = smoothness) |
| Model trong Unity | `Assets/Dravio/Character/Models/` (preset import lần đầu tự áp dụng) |
| Texture trong Unity | `Assets/Dravio/Character/Textures/` (NormalMap / sRGB / ASTC 6x6 cho Android, iOS) |
| Material / Prefab | `Assets/Dravio/Character/Materials/`, `Assets/Dravio/Character/Prefabs/` (công cụ không ghi đè) |

## Ngân sách (mobile, ngang)

Nguồn duy nhất: `Data/character-budgets.json`, dùng chung cho Python và Unity.

| LOD | Tam giác | Chuyển LOD (screen height) | Sai lệch hình dạng p95 tối đa (tĩnh / khi biến dạng) |
|---|---|---|---|
| LOD0 | 15.000 – 25.000 | 0,45 | 0,4 % / 0,6 % đường chéo bbox |
| LOD1 | 8.000 – 12.000 | 0,20 | 1,0 % / 1,4 % |
| LOD2 | 3.000 – 5.000 | 0,02 (dưới mức này cull) | 2,5 % / 3,0 % |

Skin tối đa 4 influence, cảnh báo khi trên 75 bone. Texture tối đa 2048 (khuyến nghị 1024, override mobile 1024 ASTC 6x6). Sai số bake màu p95 ≤ 4/255 và ≥ 90 % mẫu trong 2/255.

## Các bước

Chạy từ thư mục gốc repo. Trên Windows đặt `$blender = "C:\Program Files\Blender Foundation\Blender 5.3\blender.exe"`; mỗi lệnh Blender có dạng `& $blender --factory-startup --background --python-exit-code 1 --python <script> -- <args>`.

1. **Kiểm tra nguồn** (chỉ đọc): `scripts/art/character/inspect_character.py -- art-exports/DRG_emberling_Baby.glb art-exports/qa/source.inspect.json`
   Báo cáo từng mesh: số tam giác, UV (diện tích dùng, mặt lật, mặt suy biến, ngoài 0–1, độ méo diện tích), vertex colour (có ý nghĩa hay toàn trắng), Base Color / Normal / Roughness / Metallic lấy từ đâu (texture, vertex colour, hằng số; colour space), topology (trước và sau khi weld đường nối glTF), skin, shape key, texture (kích thước, power-of-two).
2. **Chuẩn bị** (UV + bake màu): `scripts/art/character/prepare_character.py -- <source.glb> art-exports/prep`
   - Weld các đỉnh trùng vị trí mà glTF tách ở đường nối UV/normal (không mất UV, cạnh cứng được đánh dấu sharp).
   - Thiếu UV → Smart UV Project + pack (hoặc `--uv-method lightmap`).
   - Base Color đến từ vertex colour → bake ra `<Name>_BaseColor.png` (Cycles EMIT, margin 16 px), đo sai số màu trên file PNG đã lưu. UV có chồng lấn → tạo UV bake riêng làm UV0, giữ UV gốc làm UV1.
   - Đã có texture trên UV0 hợp lệ → không bake; chỉ xuất bộ texture cho URP (`_Normal`, `_MetallicSmoothness` chuyển từ glTF: R = B gốc, A = 1 − G gốc).
   - GLB chuẩn bị **không** kèm COLOR_0 (glTF nhân COLOR_0 vào Base Color nên sẽ thành màu hai lần); vertex colour vẫn được giữ trong file `.blend`.
3. **Rig + 5 clip** bằng exporter hiện có: `scripts/art/build_emberling_living.py -- art-exports/rig/EmberlingRefinedGLB.fbx --source-glb art-exports/prep/<Name>_Prepared.glb` (thêm `--winged` cho loài bay).
4. **LOD**: `scripts/art/character/build_lods.py -- art-exports/rig/EmberlingRefinedGLB.blend art-exports/lod --name DRG_emberling_Baby`
   LOD0/1/2 dùng chung **một** armature và cùng take animation; trọng số được nội suy khi decimate, giới hạn 4 và chuẩn hóa. Báo cáo đo: số tam giác, sai lệch bề mặt so với nguồn ở tư thế nghỉ và ở các frame Idle/Walk/Eat/Doze/Happy, sức khỏe UV, độ trôi màu Base Color. Nguồn dưới 15.000 tam giác được giữ nguyên (không subdivide) và báo cáo.
5. **Kiểm định**: `inspect_character.py` trên FBX LOD (đọc lại FBX, kiểm tra round-trip) rồi
   `python scripts/art/character/validate_character.py --inspect lod.inspect.json --lods <Name>_lods.json --prepare <Name>_prepare.json --out validation.json` (exit 1 nếu có lỗi).
6. **Ảnh QA**: `scripts/art/character/render_preview.py -- --lods <Name>_LODs.blend --frame 200 --out lods.png` và `--source … --prepared … --out bake.png`.
7. **Unity** (menu **Dravio > Character**):
   - Chép FBX vào `Models/`, texture vào `Textures/`.
   - *Validate Selected Model*: kiểm tra LOD, UV0, skin, material, texture slot URP và import setting, cờ rig theo loài (ví dụ loài bay phải có `Wing_L/Wing_R`). Báo cáo JSON ghi vào `Logs/DravioCharacter/`.
   - *Create URP Material From Selected BaseColor Texture*: tạo URP/Lit từ bộ texture (không phụ thuộc chuyển đổi material của FBX).
   - *Create LOD Prefab From Selected Model* (chọn kèm material nếu muốn): tạo prefab `LODGroup` mới; không chạm vào FBX, importer hay scene.
   - *Log Species Character Spec*: in đặc điểm 16 loài và câu hỏi mở.

Self-test toàn bộ chuỗi trên mô hình test: `python scripts/art/character/run_selftest.py <work_dir> --blender $blender` (hoặc `--bpy-module` với `pip install bpy==5.3.*`). Unit test không cần Blender: `python -m unittest discover -s scripts/art/character/tests`.

## 16 loài × 3 giai đoạn

Xem `SPECIES_MORPHOLOGY.md` (tự sinh bởi `build_species_spec.py`, có `--check` để phát hiện lệch). Dữ liệu gốc chỉ có mốc tăng trưởng (Juvenile từ 35 %, Adult ở 100 %), **không có** hình thể theo giai đoạn, nên các ô giai đoạn là "chưa xác định" cho tới khi art director / GDD bổ sung. Xung đột trong dữ liệu được nêu thành câu hỏi, không tự giải quyết (ví dụ `frostfin`: BodyStyle = Winged nhưng không bay).

## Giới hạn đã biết

- FBX không mang được texture gói kênh của glTF (metallic/roughness) hay node Mix; vì vậy có bộ texture riêng và công cụ tạo material URP.
- Decimate kiểu collapse có thể làm vài mặt UV bị lật hoặc kéo giãn ở đường nối (đo được, xem `uv_flipped` và `texture_drift`). Có `--protect-group` để giảm decimate ở mặt/mắt, nhưng chưa thử trên mô hình thật.
- Shape key (blend shape) chưa được hỗ trợ trong bước LOD.
- Rig vẫn là rig fit theo bounding box của pipeline GLB cũ; chất lượng skin của LOD được đo so với LOD nguồn, không chứng minh rig đó đẹp.

## CHAR.8 — Aurorawing, chất lượng rồng sống, Sanctuary

- Aurorawing (trứng thưởng + ba giai đoạn, blockout SAMPLE qua pipeline thật, QA PASS): [`AURORAWING.md`](AURORAWING.md).
- Phản ứng nhìn / tiếp cận / ăn của rồng được chọn (nghiên cứu, hợp đồng đề xuất): [`FEEDING_RESPONSE.md`](FEEDING_RESPONSE.md).
- Mẫu phong cách và catalog prefab 20 công trình Sanctuary: [`../Environment/SANCTUARY_PREFABS.md`](../Environment/SANCTUARY_PREFABS.md).
- Báo cáo kiểm thử, hiệu năng, phần nào chạy thật: `scripts/art/character/TEST_REPORT_CHAR8.md`.
- Dữ liệu: nhóm prop `egg` trong `Data/character-budgets.json`; `Data/asset-overrides.char8-local.json` (u7-local + Aurorawing `limbCount: "auto"`, chiều cao trứng đề xuất).
