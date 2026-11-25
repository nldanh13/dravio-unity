# U1-CHAR.5: Thư viện nhân vật production và rồng sống — báo cáo kiểm thử

## Phạm vi bằng chứng

- **Môi trường:** Linux, Python 3.13, Blender 5.3 (module `bpy`), .NET 8.
- **Không có Unity Editor.** Chưa chạy Unity 6000.6. Prefab, Animator, Animation Lab và stress test chưa được mở hay đo trong Unity.
- **Không truy cập được 5 GLB thật** (`B:\Dravio\...`). Phiên làm việc không có thư mục nào từ máy của bạn.
- Mọi số liệu dưới đây là trên 7 model rig **SAMPLE** (`make_sample_library.py`) và 1 model SAMPLE tĩnh (magma, chưa có limbCount).
- **Không model thật nào được nghiệm thu.** Để có số liệu thật, chạy `run_u5_local.ps1` (mục 6).

## 1. Kiểm thử tự động

| Kiểm thử | Kết quả |
|---|---|
| `python -m unittest discover -s scripts/art/character/tests` | **53/53 pass**: 46 cũ + 7 mới (bước chân C1 và biên độ ±sải/2, mặt đối xứng từ cặp chân, 5 test báo cáo) |
| Đột biến: khôi phục `foot_track` của U4 | test C1 **fail**, đúng như mong đợi |
| `build_archetypes.py --check`, `build_species_spec.py --check` | khớp với file đã commit |
| `run_selftest.py --bpy-module` (pipeline U1-CHAR cũ) | ALL OK (22 bước, exit 0), không hồi quy |
| `dotnet test Assets/Dravio/Character/Tests/DotNet~` | **14/14 pass**: 7 test runtime cũ + 7 test mới (roster: pool / spawn / despawn / stage, 30 rồng × 5 prefab; đồng bộ Sanctuary) |
| `dotnet build Assets/Dravio/Character/Tests/DotNet~/TypeCheck` | biên dịch được `DragonVisualManager` và `DragonVisualInstance` với stub API viết tay. **Không phải Unity thật** |
| Cache batch | chạy lại không đổi gì: 12/12 `unchanged` (0,07 s) |
| GLB nguồn | SHA-256 của 13 file không đổi |

## 2. Trước/sau trên 7 model SAMPLE

Trước là rig v2 (U1-CHAR.4, commit 0df18ce), sau là rig v3. Cùng `anim_qa.py`, cùng ngưỡng `animationQa` (không nới):
- giãn p99.9 ≤ 1,6, nén p0.1 ≥ 0,45
- trượt chân ≤ 2%, xuyên đất ≤ 1%, khép vòng ≤ 0,5°

| Model | v2 | v3 | Cảnh báo v2 | Cảnh báo v3 |
|---|---|---|---|---|
| droplet_Baby | PASS_WITH_WARNINGS | **PASS** | 2 (Sleep nén 0,396; Swim giãn 1,612) | 0 |
| emberling_Baby | PASS | PASS | 0 | 0 |
| frostfin_Juvenile | PASS | PASS | 0 | 0 |
| leviathan_Adult | PASS | PASS | 0 | 0 |
| sprout_Baby | PASS_WITH_WARNINGS | **PASS** | 2 (Sleep 0,446; Burrow 0,433) | 0 |
| steamdrake_Juvenile | PASS_WITH_WARNINGS | **PASS** | 1 (lệch đối xứng L/R 8%) | 0 |
| zephyr | PASS_WITH_WARNINGS | **PASS** | 1 (Sleep 0,447) | 0 |

Bảng từng clip: `TEST_REPORT_U5_compare.md`. Báo cáo từng model, catalog, animation và hiệu năng: `reports_u5_sample/`.

## 3. Nguyên nhân và cách sửa

Mỗi nguyên nhân dưới đây được tìm bằng thí nghiệm một biến, chạy rig + QA trên từng model (khoảng 11 s/lần), trước khi sửa.

1. **Sleep: nén ở cổ chân sau.**
   - Thí nghiệm: tắt riêng pitch cổ, yaw cổ hoặc độ hạ thân thì Sprout đều hết cảnh báo, tức là nén cộng dồn. Chia góc cổ sang Chest gần như không giúp Droplet/Zephyr. Còn `footFollow` 1,0 thì Droplet từ 0,396 lên 0,553.
   - Nguyên nhân: bàn chân trụ giữ phẳng trong khi cẳng chân gập, nên toàn bộ góc gập dồn vào cổ chân.
   - Sửa: `quadruped.sleep.footFollow` 0,5 → 0,8. Bàn chân xoay theo cẳng chân 80%.
   - Kết quả: Sleep nén 0,553 (Droplet), 0,629 (Sprout), 0,637 (Zephyr), 0,644 (Emberling). Xuyên đất, hở tiếp xúc và trượt đều bằng 0.
2. **Burrow (Sprout): cùng cơ chế.** Thân hạ trong khi chân sau trụ phẳng. Thêm `burrow.footFollow` = 0,5: nén 0,433 → 0,685.
3. **Swim (Droplet): giãn ở hông/gốc đuôi.** Kiểu bơi paddle vẫy gốc đuôi với 60% biên độ ngay từ hông. Thêm `swim.tailBase` (tỷ lệ biên độ ở gốc), Droplet = 0,45. Biên độ ở ngọn không đổi. Giãn 1,612 → 1,494.
4. **Steamdrake: lệch đối xứng 8%.**
   - Mẫu đặt chân ở x = ±0,150. Pipeline đo được −0,167 / +0,133, tức cả bộ lệch 0,017 m.
   - Nguyên nhân: mesh được căn giữa theo hộp bao, mà thân hình chữ S làm hộp bao lệch khỏi mặt đối xứng. Rig không sai.
   - Sửa: khi đo được các cặp chân L/R, mặt đối xứng và pivot đặt ở trung điểm các cặp chân (`rig.json` ▸ `size.midlineShift_m` = −0,0168). Chân về đúng ±0,150.
   - Có landmark tay thì không dời, vì điểm tay nằm trong hệ toạ độ của nguồn.
5. **Walk/Run: chân khựng ở đầu/cuối pha vung.** Không có ngưỡng QA nào bắt lỗi này. Phát hiện qua chỉ số `seamSpeedMismatchDegPerFrame` (6–15°/frame).
   - So với bên trong clip, điểm nối không tệ hơn, nên đây **không phải lỗi vòng lặp**. Nguyên nhân là `foot_track`: chú thích nói "vận tốc liên tục" nhưng đường cosine dừng hẳn bàn chân ở hai đầu, và chiều đứng `sin` bật lên với vận tốc khác 0.
   - Sửa: pha vung là spline Hermite có tiếp tuyến bằng vận tốc pha trụ (C1), chiều đứng dùng `sin²`.
   - **Hồi quy phát hiện trong lúc sửa:** bản Hermite đầu tiên vượt quá cuối pha trụ tới 7% sải khi Run (duty 0,42). Run của Emberling, Sprout và Droplet giãn 1,603–1,616, tức vượt ngưỡng.
   - Không nới ngưỡng. Sửa đúng cơ chế: biên độ chân giữ đúng ±sải/2 như U4, pha trụ ngắn lại để bù (`stance_stride`), `groundSpeed` tính lại theo đó (Emberling Walk 0,142 → 0,137 m/s, Run 0,606 → 0,530 m/s). Runtime đặt `PlaybackRate = speed / groundSpeed` nên chân vẫn không trượt.
   - Kết quả: Run giãn 1,53–1,56, thấp hơn U4.

**Độ mượt bước chân** (Emberling, đo góc từng bone mỗi frame):

| | Walk v2 | Walk v3 | Run v2 | Run v3 |
|---|---|---|---|---|
| Lệch vận tốc ở điểm nối vòng lặp (°/frame) | 6,64 | **2,62** | 10,38 | **4,71** |
| Lệch vận tốc bên trong clip, p90 | 8,58 | **5,65** | 10,93 | **9,49** |
| Lệch vận tốc bên trong clip, lớn nhất | 9,97 | 12,99 | 10,93 | 9,67 |

Đỉnh 12,99° của Walk v3 nằm ở lúc chân sau đổi chiều đầu pha vung (frame 21→22). v2 cũng có đỉnh ở đúng chỗ đó (11,8–12,4°). Đây là động lực của một pha vung chỉ 11 frame. Không có ngưỡng QA cho chỉ số này, nên **animator cần xem bằng mắt** trong Animation Lab.

## 4. Unity (chưa chạy, chỉ kiểm bằng biên dịch thử và test logic)

- **`DragonVisualRoster`** (mới, pure C#): sổ sách UID → instance của `DragonVisualManager`.
  - Mỗi UID có đúng một instance và ngược lại.
  - Spawn lại cùng prefab thì giữ instance.
  - Lớn lên sang prefab khác thì trả instance cũ vào pool trước.
  - Despawn hai lần vô hại.
  - Vượt `maxPooledPerPrefab` thì instance bị huỷ, đếm ở `PoolDestroyed`.
  - Stress harness ghi thêm `poolDestroyed`.
- **`SanctuaryDragonSync` + `IDragonVisualTarget`** (mới): API tích hợp với Unified Sanctuary, xem `Assets/Dravio/Character/SANCTUARY_INTEGRATION.md`. Có test: spawn, cập nhật, lớn lên, bán, ẩn khi đi thám hiểm, đổi variant, UID trùng, 30 rồng ổn định không phát sinh lệnh spawn/despawn.
- **`CharacterBatchCli.RunImport`**: entry point public cho Unity batch mode (`-executeMethod`), thoát với mã 1 khi có mục import lỗi.
- **Animator:** không đổi (controller theo archetype + override theo model). TakeOff/Glide/Land có trong FBX và Animation Lab nhưng chưa có state Animator. `DragonLocomotion` chưa có giá trị tương ứng.
- **PBR / LOD:** `prepare_character.py` và `build_lods.py` không sửa. Trên SAMPLE, LOD 24000/10000/4000 nằm trong ngân sách, và mọi kiểm `LOD_SHAPE` / `LOD_SKINNING` đều pass (`reports_u5_sample/qa/*.md`).

## 5. Không hồi quy

| Kiểm tra | Kết quả |
|---|---|
| Ngưỡng `character-budgets.json` | không đổi |
| Hình thái sinh học (limbCount, archetype, bộ clip theo sinh học) | không đổi. `animation-report.md`: 0 clip thiếu/thừa trên 7 model rig |
| Chiều cao Emberling | 0,90 m |
| GLB nguồn | SHA-256 không đổi (13 file) |
| `approved-assets.json` | logic không đổi: tên trong danh sách không bị giao lại |
| Domain, Persistence, Fantasy HUD, Scene Open Game, DragonCarePreview | không sửa file nào |

## 6. Chạy trên GLB thật tại máy bạn

```powershell
powershell -ExecutionPolicy Bypass -File scripts\art\character\run_u5_local.ps1 `
  -Source "B:\Dravio\dravio_16_dragons_3d_pack\images" `
  -Blender "C:\Program Files\Blender Foundation\Blender 5.3\blender.exe" `
  -OldWork "B:\Dravio\work_u4" -Work "B:\Dravio\work_u5" `
  -UnityOut "Assets\Dravio\Character\Imported" -Unity "C:\Program Files\Unity\Hub\Editor\6000.6.10f1\Editor\Unity.exe"
```

Gửi lại:
- `U5_before_after.md`
- `batch-report.md`
- cả thư mục `reports\`
- `qa_after\*.png`
- `unity\import.log` và `unity\editmode-results.xml`

Sau đó mở **Dravio ▸ Character ▸ Animation Lab** và xem từng clip bằng mắt trước khi đưa tên nào vào `approved-assets.json`.

## 7. Chưa kiểm chứng

- **Unity 6000.6:** import FBX, chia clip, prefab LODGroup, override controller, catalog asset, Animation Lab, Foot IK trên bậc thang, stress 5/15/30 rồng trên thiết bị.
- **Hiệu năng:** `performance-budget.md` chỉ là ước tính tĩnh theo giả định phân bố camera, **không phải số đo**.
- **GLB thật:** chưa xử lý bằng rig v3. Các sửa ở mục 3 được chứng minh trên SAMPLE. Trên model thật có thể còn cảnh báo khác.
- **Chất lượng nghệ thuật:** animation vẫn là procedural theo quy tắc. Chưa có keyframe tay hay mocap.
- **`limbCount` của Droplet, Zephyr, Steamdrake:** vẫn là "auto" (đo được 4 với độ tin cậy cao trên SAMPLE). Cần art director xác nhận.
- **Magma:** chưa rig vì thiếu `limbCount` trong dữ liệu chuẩn. Model chỉ hiện tĩnh.
