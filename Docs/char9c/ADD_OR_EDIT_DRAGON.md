# Thêm rồng mới / sửa rồng mà không làm vỡ cơ chế game

Một con rồng là **dữ liệu** đi qua **một dây chuyền chung**. Code gameplay chỉ biết đến hợp đồng (tên xương, tên clip, điểm gắn hiệu ứng) và các con số đo được của từng con, không bao giờ biết đến một model cụ thể. Giữ đúng hợp đồng thì thay model không phải sửa code.

## 1. Thêm rồng mới

1. **Nguồn**: đặt `ArtSource/Dragons/images/NN_<loài>.glb` (giai đoạn riêng: `NN_<loài>_Baby.glb`, `_Juvenile`, `_Adult`). Không sửa hay ghi đè file nguồn sau đó; mọi chỉnh sửa đều sinh ra file mới trong thư mục làm việc.
2. **Dữ liệu loài**: thêm loài vào dữ liệu sinh học canonical. Chạy `python scripts/art/character/build_species_spec.py --check` và `build_archetypes.py --check`; archetype (quadruped, winged, serpentine, aquatic) và bộ clip được suy ra từ cách di chuyển của loài, không viết tay.
3. **Cấu hình riêng** (chỉ những gì máy không đo được): `Assets/Dravio/Character/Data/asset-overrides.char8-production.json` → `species.<loài>`:
   - `limbCount` (số chân, hoặc `"auto"` rồi art director xác nhận), `yawOffset` nếu model quay ngang;
   - `repair`: `{"skinning": "linear", "seamWeld": true, "blendWidth_m": 0.24, "pinTransitions": true, "amplitude": {...}}`. Đã bật cho cả 16 loài. Phần `amplitude` không viết tay: chạy `tune_repair.py` (đo mọi frame, giảm biên độ nhóm xương gây lỗi, giữ tư thế chuyển tiếp, không bao giờ nới ngưỡng) rồi chép `best.config` vào đây. Clip nào nó báo NEEDS_ART thì phải sửa trọng số hoặc keyframe.
4. **Chạy dây chuyền**:
   ```bash
   python scripts/art/character/batch_import.py ArtSource/Dragons/images <work> --bpy-module \
       --overrides Assets/Dravio/Character/Data/asset-overrides.char8-production.json --qa-strict --qa-images \
       --unity-out Assets/Dravio/Character/Imported
   ```
   `--qa-strict` = đo mọi frame, áp ngưỡng cho frame tệ nhất, kiểm khe nứt đường nối.
5. **Đọc QA**: `<work>/assets/DRG_<loài>_<giai đoạn>/*_animqa.json` và `.png`. Chỉ được đưa vào game khi `status` là `PASS` và ảnh nhìn ổn. Cảnh báo nào cũng phải sửa bằng rig/animation, **không nới ngưỡng**.
6. **Unity**: **Dravio ▸ Character ▸ Import Batch Manifest** tạo prefab, controller và đăng ký vào `DragonVisualCatalog`. Prefab, material và controller đã có thì không bị ghi đè.
7. **Duyệt**: art director/chủ dự án thêm tên vào `Imported/approved-assets.json`. Từ đó dây chuyền không bao giờ ghi đè bản đã duyệt.

## 2. Sửa một con rồng đã có

- **Đổi hình** (art): xuất GLB mới với tên mới hoặc bản mới trong `ArtSource`, chạy lại bước 4. Hợp đồng tự giữ vì rig được dựng lại từ đầu theo cùng quy tắc.
- **Đổi chuyển động / trọng số**: sửa `repair` hoặc biên độ trong file cấu hình, chạy lại; không sửa tay file FBX đã xuất.
- **So sánh trước/sau**: `python scripts/art/character/compare_animqa.py <thư mục cũ> <thư mục mới> so_sanh.md`.
- Không đổi tên xương, tên clip hay điểm gắn (`VFX_Mouth`, `VFX_TailTip`, `VFX_Back`, `VFX_Ground`) nếu chưa thống nhất với phía Unity.

## 3. Hợp đồng giữa model và game (không đổi tùy tiện)

| Thành phần | Quy ước | Ai dùng |
|---|---|---|
| Hướng, gốc | mặt nhìn −Y trong Blender, gốc ở tâm đế trên mặt đất (z = 0), đơn vị mét | điều hướng, camera |
| Xương | tên theo archetype (`Root`, `Pelvis`, `Spine…`, `Head`, `ForeUpper_L`…), ≤ 4 ảnh hưởng/đỉnh | Animator, bám chân, nhìn theo |
| Clip | tên theo `dragon-archetypes.json` (`Idle`, `Walk`, `Eat`, `Sleep`…), loop đóng | bộ chọn hành vi |
| Điểm gắn | `VFX_*` | hiệu ứng lửa, nước, bụi |
| Số đo | `height`, `groundSpeed` mỗi clip, `reach_m` mỗi clip (trong `*_animqa.json`) | tốc độ phát, khoảng chừa |

Riêng prototype Emberling còn dùng hợp đồng cũ 22 xương / 5 clip (`EmberlingRefinedGLB.fbx`). Đây là ngoại lệ, nên chuyển sang prefab catalog chung khi Unity sẵn sàng.

## 4. Công thức (mọi số lấy từ chính con rồng đó)

Gọi `h` là chiều cao chuẩn, `reach(θ)` là tầm với của lưới theo hướng θ tính từ gốc, lấy qua mọi frame (`reach_m.front/back/side/radius` trong QA), và `R = reach_m.radius`.

1. **Khoảng chừa với vật cản**: `chừa ≥ R + biên` (bỏ qua hướng), hoặc `chừa(θ) ≥ reach(θ) + biên` nếu điều hướng xét hướng mặt. Emberling: R = 0,75 m, trong khi hằng số hiện tại là 0,45 m.
2. **Hai rồng A, B**: `khoảng cách gốc ≥ reach_A(hướng về B) + reach_B(hướng về A) + biên`. Đối đầu: R_A + R_B; song song: side_A + side_B.
3. **Tỉ lệ theo giai đoạn / kích thước**: `reach_mới = reach_chuẩn × h_mới / h_chuẩn`; groundSpeed cũng nhân cùng tỉ lệ.
4. **Chân không trượt**: `tốc độ phát clip = tốc độ di chuyển / groundSpeed`, với `groundSpeed = sải chân / thời gian chạm đất`. Giảm biên độ chân k lần thì groundSpeed × k (dây chuyền tự làm).
5. **Biến dạng**: `giãn = độ dài cạnh lúc động / độ dài lúc nghỉ`, yêu cầu p99,9 ≤ 1,6 và p0,1 ≥ 0,45 ở frame tệ nhất.
6. **Khe nứt đường nối**: `khoảng cách hai đỉnh trùng vị trí ≤ 0,1 % h` (đề xuất; hàn trọng số cho 0).
7. **Chạm đất**: `điểm thấp nhất ≥ −1 % h`; chân trượt ≤ 2 % h; hở tiếp xúc ≤ 3 % h.

Kiểm một con rồng trên dữ liệu điều hướng thật của một vùng:
```bash
python scripts/art/character/measure_contact_clearance.py <model.fbx> Assets/Dravio/Environment/Data/<Zone>Locations.asset out.json --png map.png
```

## 5. Không được làm

- Ghi đè GLB nguồn hoặc tài sản đã duyệt.
- Nới ngưỡng trong `character-budgets.json` để đổi WARNING thành PASS.
- Phóng to đồng đều để tạo giai đoạn khác (đầu, chân, cánh, sừng có tỉ lệ riêng).
- Ép mọi loài dùng chung một rig khi cách di chuyển khác nhau.
