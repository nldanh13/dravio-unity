# U1-CHAR — Báo cáo kiểm thử

Ngày: 2026-11-25. Môi trường: cloud Linux, Blender **5.3.0** qua module `bpy` (cùng bản Blender 5.3 với máy tác giả), Python 3.13. **Không có Unity Editor.**

> Mọi số liệu dưới đây là của **mô hình kiểm thử tự sinh** `TEST_Quadruped_*` (khối cầu/trụ voxel-remesh, sinh bởi `make_test_assets.py`). Không có GLB thật nào của Dravio được cung cấp hay tối ưu. Hình dạng test trơn, không có sừng/vuốt/răng mảnh, nên sai lệch LOD trên mô hình thật sẽ **lớn hơn**.

Tái hiện: `python scripts/art/character/run_selftest.py <work> --bpy-module` (hoặc `--blender <exe>`), và `python -m unittest discover -s scripts/art/character/tests`.

## 1. Kiểm tra pipeline GLB → Blender → FBX → Unity hiện có

| Kiểm tra | Kết quả |
|---|---|
| `build_emberling_living.py --source-glb` với GLB vertex colour, không UV (60k tam giác) | Chạy được, FBX + .blend + qa.json được tạo; mesh không có UV (đúng như cảnh báo của script). |
| Cùng lệnh với GLB **có UV + texture** (40k tam giác) | **Lỗi**: `Automatic weights left 3 unweighted vertices`. Nguyên nhân: glTF tách đỉnh ở mọi đường nối UV, GLB nhập vào thành **287 mảnh rời / 8.188 cạnh biên**; bone-heat không phủ hết, và dù có phủ thì đường nối sẽ bị xé khi biến dạng. |
| Sửa: `emberling_glb_source.py` nhập với `merge_vertices=True` (chỉ hàn đỉnh trùng vị trí *và* normal) | Cùng GLB còn 1 mảnh / 0 cạnh biên; rig thành công, 20.002 đỉnh có ≤ 4 trọng số chuẩn hóa. |
| GLB có COLOR_0 **và** baseColorTexture | Importer glTF của Blender nhân hai thứ qua node Mix (đúng chuẩn glTF), và FBX exporter **làm mất texture** sau node Mix → FBX chỉ còn màu hằng 0.8. Vì vậy GLB chuẩn bị không còn COLOR_0. |
| Texture metallicRoughness của glTF qua FBX | **Mất** (FBX chỉ giữ Base Color và Normal; roughness/metallic thành hằng số). Bù bằng `<Name>_MetallicSmoothness.png` + menu tạo material URP. |
| Take animation sau khi đọc lại FBX | 445 frame giữ nguyên (Blender đọc lại thành 2–446; Unity cắt clip theo frame đầu nên không ảnh hưởng). |

## 2. Chuẩn bị: UV + bake vertex colour

| Mô hình | Trước | Việc đã làm | UV0 sau | Sai số màu (PNG 8-bit đã lưu, 4 điểm/tam giác) |
|---|---|---|---|---|
| VC_NoUV | 60.000 tam giác, không UV, vertex colour 225 màu | tạo UV Smart Project, bake 1024², margin 16 px | 0 mặt lật, 0 suy biến, 0 ngoài 0–1, dùng 63 % diện tích, chồng lấn 0,21 %, méo diện tích p05–p95 = 0,80–1,18 | trung bình 0,47/255, p95 2,02, p99 6,55, **max 115**; 94,97 % mẫu trong 2/255 |
| Low | 6.000 tam giác, không UV | như trên | 0 mặt lật | trung bình 0,30, p95 0,74, p99 1,93, max 97; 99,07 % trong 2/255 |
| PBR_UV | 40.000 tam giác, UV + 3 texture | không bake (đã có texture); xuất `_BaseColor`, `_Normal`, `_MetallicSmoothness` | giữ nguyên | không áp dụng |

- Max cao ở các tam giác nhỏ hơn 1 texel nằm ở biên đốm màu; 1024² không biểu diễn được chi tiết đó. Nếu cần, dùng `--texture-size 2048`.
- Vertex colour gốc giữ nguyên từng góc trong file `.blend` (selftest so sánh: khớp).
- `_MetallicSmoothness`: R so với kênh B gốc sai 0/255, A so với 1 − G gốc sai < 0,001/255.

## 3. LOD (sau khi rig bằng exporter hiện có, 22 bone, 5 clip)

Sai lệch = khoảng cách đỉnh ↔ bề mặt hai chiều so với mesh nguồn đã rig; "biến dạng" = xấu nhất trong 6 frame (Idle 1, Walk 190/208, Eat 250, Doze 340, Happy 420). Đường chéo bbox ≈ 2,0 m (cao 0,90 m).

| Mô hình | LOD | Tam giác (trước → sau) | Đỉnh | p95 tĩnh | p95 biến dạng xấu nhất | Max tĩnh | Trôi màu p95 | Mặt UV lật | Influence max |
|---|---|---|---|---|---|---|---|---|---|
| VC_NoUV | 0 | 60.000 → 24.000 | 12.002 | 0,16 mm (0,008 %) | 0,010 % | 0,35 mm | 1,7/255 | 11 | 4 |
| | 1 | → 10.000 | 5.002 | 0,37 mm (0,018 %) | 0,027 % | 1,11 mm | 3,6/255 | 12 | 4 |
| | 2 | → 4.000 | 2.002 | 1,0 mm (0,049 %) | 0,065 % | 3,07 mm | 8,1/255 ⚠ | 6 | 4 |
| PBR_UV | 0 | 40.000 → 24.000 | | 0,14 mm | 0,009 % | | 0,8/255 | 7 | 4 |
| | 1 | → 10.000 | | 0,38 mm | 0,027 % | | 1,9/255 | 15 | 4 |
| | 2 | → 4.000 | | 1,05 mm | 0,065 % | | 4,9/255 | 5 | 4 |
| Low | 0 | 6.000 (giữ nguyên) | | 0 | 0 | | 0 | 0 | 4 |
| | 1 | → 3.600 ⚠ dưới 8.000 | | 1,10 mm | 0,060 % | | 3,4/255 | 0 | 4 |
| | 2 | → 2.160 ⚠ dưới 3.000 | | 2,06 mm | 0,114 % | | 10,4/255 ⚠ | 0 | 4 |

- Cả ba LOD dùng **một** armature; đọc lại FBX: số tam giác mỗi LOD khớp báo cáo, danh sách bone khớp exporter rig, UV có ở mọi LOD, không đỉnh nào thiếu trọng số hoặc chưa chuẩn hóa.
- Nguồn 6.000 tam giác không bị subdivide; LOD1/LOD2 dưới ngưỡng được báo cảnh báo thay vì giả vờ đạt.
- Mặt UV lật (5–15 trên hàng nghìn) và độ trôi màu ở LOD2 đến từ collapse decimation tại đường nối UV.

## 4. Kiểm định

| Mô hình | Trạng thái | Ghi chú |
|---|---|---|
| VC_NoUV | PASS_WITH_WARNINGS | LOD2 trôi màu p95 8,1/255 |
| PBR_UV | PASS | |
| Low | PASS_WITH_WARNINGS | 3 LOD dưới ngưỡng (nguồn quá đơn giản) + trôi màu LOD2 |
| GLB nguồn VC_NoUV (trước xử lý) | FAIL (đúng kỳ vọng) | 60.000 > 25.000, không UV0, Base Color từ vertex colour |

Unit test thuần Python: 19/19 OK (luật validator, mã thoát CLI, spec 16 loài chép nguyên văn, không bịa hình thể theo giai đoạn, file sinh ra không lệch). Self-test end-to-end: 22/22 bước OK. Hash SHA-256 của mọi GLB nguồn không đổi qua toàn bộ chuỗi.

## 5. Chưa xác minh

- **Unity**: 6 script Editor chưa được biên dịch hay chạy trong Unity 6000.6. Chỉ kiểm tra cú pháp và type-check bằng `mcs` với stub API Unity tự viết (chỉ bắt lỗi của chính code, không bắt sai tên API Unity).
- Import FBX vào Unity, LODGroup, ASTC, material URP, bóng/ánh sáng, khung hình trên thiết bị.
- Bất kỳ GLB rồng thật nào, kể cả Emberling; chất lượng rig fit theo bounding box trên hình dạng thật. Số đo skinning của LOD chỉ so LOD với mesh nguồn cùng rig, không đánh giá bản thân rig.
- Bake trên UV có sẵn nhưng chồng lấn (logic có, chưa có tài nguyên test chồng lấn), mô hình nhiều material, shape key.
- Hình thể theo giai đoạn Baby/Juvenile/Adult của 16 loài: dữ liệu gốc không có.
