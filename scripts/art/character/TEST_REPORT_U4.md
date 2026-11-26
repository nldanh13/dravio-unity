# U1-CHAR.4: Báo cáo kiểm thử rig và animation production

**Phạm vi bằng chứng.** Môi trường: Linux, Python 3.13, Blender 5.3 (module `bpy`), mono `mcs`.
- **Không có Unity Editor**, nên chưa chạy Unity 6000.6.10f1.
- **Không truy cập được 5 GLB Hunyuan3D-2.1 thật** (`B:\Dravio\dravio_16_dragons_3d_pack\images`).
- Mọi số liệu bên dưới là từ GLB mẫu tự sinh `SAMPLE_*` (`make_sample_library.py`).
- **Báo cáo này không khẳng định đã sửa được các mô hình thật.** Để có số liệu thật, chạy `run_u4_local.ps1` (mục 5) trên máy của bạn.

## 1. Kiểm thử tự động

| Kiểm thử | Kết quả |
|---|---|
| `python -m unittest discover -s scripts/art/character/tests` | 46/46 pass. 30 test cũ cộng 16 test mới: nhận diện 4 chân (độ tin cậy cao), bụng chạm đất không bị tính là chân, landmark thủ công, chain weights liên tục và chuẩn hoá, dải biên chỉ làm mịn đúng vùng, bone không được phép không bao giờ lọt vào, Root không bao giờ skin, quỹ đạo chân trượt lùi đều (không moonwalk) và khép vòng, lỗi thư mục nguồn, alias tên file, override theo file, hash pipeline, so sánh trước/sau |
| `build_archetypes.py --check` | khớp với file đã commit |
| `run_selftest.py --bpy-module` (pipeline U1-CHAR cũ) | ALL OK, không hồi quy |
| C# runtime và editor (`mcs`, stub API Unity viết tay) | biên dịch được. Chỉ là kiểm tra kiểu, **không phải biên dịch Unity thật** |
| NUnit (mono, shim) | 12/12 pass: 7 test cũ, cộng 5 test `AnimationProductionTests` (IK chỉ bật khi đứng trên đất, swing fade, hạ pelvis, tốc độ tham chiếu theo clip, thời gian từng frame trong Lab) |

## 2. Trước/sau trên 7 model mẫu

Cả hai bên dùng cùng `anim_qa.py` và cùng ngưỡng `animationQa` trong `character-budgets.json`:
- giãn cạnh p99.9 ≤ 1,6; nén p0.1 ≥ 0,45
- trượt chân ≤ 2% chiều cao; xuyên đất ≤ 1%
- lỗi khép vòng ≤ 0,5°; xuyên cánh ≤ 0,2%
- không frame nào đầu quay ra sau ngực

**Ngưỡng không được nới** để đổi WARNING thành PASS. "v1" là rig U1-CHAR.3 (commit 8665415), "v2" là rig của nhánh này.

| Model | v1 | v2 | Cảnh báo v1 | Cảnh báo v2 |
|---|---|---|---|---|
| emberling_Baby | PASS_WITH_WARNINGS | **PASS** | 4 | 0 |
| sprout_Baby | PASS_WITH_WARNINGS | PASS_WITH_WARNINGS | 6 | 2 |
| droplet_Baby (limbCount auto → 4) | PASS_WITH_WARNINGS | PASS_WITH_WARNINGS | 5 | 2 |
| zephyr (limbCount auto → 4, có cánh) | PASS_WITH_WARNINGS | PASS_WITH_WARNINGS | 6 | 1 |
| steamdrake_Juvenile (auto → 4, serpentine) | PASS_WITH_WARNINGS | PASS_WITH_WARNINGS | 7 | 1 |
| frostfin_Juvenile | PASS_WITH_WARNINGS | **PASS** | 4 | 0 |
| leviathan_Adult | PASS_WITH_WARNINGS | **PASS** | 5 | 0 |

Bảng chi tiết từng clip (giãn/nén trước → sau, bone tệ nhất): `TEST_REPORT_U4_compare.md`.

**Nói thẳng.** Trên mẫu, rig v1 **không tái hiện** đúng các cảnh báo biến dạng Sleep/Walk/Run mà bạn gặp trên GLB thật.
- Phần lớn cảnh báo v1 trên mẫu là **xuyên đất**: thân hoặc bàn chân thấp hơn mặt đất 1,5–1,9% chiều cao.
- Chỉ Sprout Run vượt ngưỡng giãn (1,666).
- Vì vậy bảng trên chứng minh các cơ chế sửa hoạt động trên mẫu. Nó **không chứng minh** cảnh báo trên Emberling/Sproutling thật đã hết.

## 3. Cảnh báo Emberling/Sproutling của bạn: nguyên nhân và cách sửa

**Nguyên nhân 1: cách phân vùng trong v1.**
- v1 gán vùng skin (thân/chân/đuôi) bằng hộp bao và skin bằng bone heat trên toàn lưới.
- Với lưới Hunyuan (chân dính sát bụng, đuôi to, mảnh rời), bone chân kéo cả bụng và gốc đuôi.
- Khi Walk/Run vung chân, hoặc Sleep gập chân, các cạnh ở bụng và háng bị giãn/nén mạnh.
- Bằng chứng: trên mẫu v2, `worstBones` của mọi clip đi bộ nằm ở Pelvis/HindFoot, không còn ở bụng hay mắt.

**Sửa trong v2** (`rig_geometry.py`, `rig_archetype.py`):
- **Vùng:** đo bằng hình học thật: lát cắt theo trục thân, phát hiện chân từ điểm chạm đất, đầu/đuôi theo trục.
- **Landmark thủ công:** đặt qua `files.<stem>.landmarks` khi phép đo sai.
- **Chân, đuôi, cổ dùng chain weights:** trọng số liên tục dọc xương, tối đa 3 bone cộng bone cha. Bone cha không bao giờ chạm tới cẳng chân.
- **Bone chân không được skin lên thân, mắt hay đuôi:** chỉ một dải hẹp 2 vòng cạnh ở háng, chỉ với bone đùi.
- **Dải chuyển tiếp có giới hạn:** thân/đuôi, thân/cổ, cổ/đầu. Cánh/vây dùng dải riêng.
- **Mảnh rời:** gắn cứng theo từng mảnh.
- **Chuẩn trọng số:** tối đa 4 ảnh hưởng mỗi đỉnh, 0 đỉnh không có trọng số, lỗi chuẩn hoá ≤ 1e-5 (đã kiểm trên cả 7 mẫu).

**Nguyên nhân 2: tư thế Sleep gập chân quá mức.**
- v1 hạ thân mà không di chuyển bàn chân, nên đầu gối gập ngược.

**Sửa:**
- Chân sau trượt ra sau (`hindReachLegs`), bàn chân xoay theo cẳng chân (`footFollow`).
- Cuộn đuôi bắt đầu sau gốc.
- Xoay cổ/đầu chia cho cả hai bone.
- Mọi tham số ở `rig-profiles.json`, không viết cứng.

**Nguyên nhân 3: xuyên đất.**
- **Sửa:** mỗi clip được tạo hai lượt.
- Lượt đầu đo độ sâu dưới mặt đất. Với loài có chân: giảm biên độ (×0,85, tối đa 4 lần). Sau đó nâng toàn bộ root và đích IK một hằng số. Giá trị nâng ghi ở `groundClearanceLift_m`.
- Không áp dụng cho Swim, Fly, Glide.

**Còn lại trên mẫu** (không ẩn, không nới ngưỡng):

| Model | Clip | Giá trị | Vùng | Đánh giá |
|---|---|---|---|---|
| sprout | Sleep | nén 0,446 (ngưỡng 0,45) | Neck 32%, HindFoot 19% | cổ gập sát thân khi ngủ. Hạ `sleep.neck` trong profile nếu animator thấy cần |
| sprout | Burrow | nén 0,433 | Pelvis 27%, HindFoot 24% | chân sau đạp đất khi đào. Biên độ do `burrow.*` trong profile |
| droplet | Sleep | nén 0,396 | Neck 32%, EarFin 19% | vây tai bị cổ ép. Có thể cần landmark tay cho vây |
| droplet | Swim | giãn 1,612 (ngưỡng 1,6) | Pelvis 58%, Tail 23% | sóng đuôi ở gốc |
| zephyr | Sleep | nén 0,447 | Neck 32% | như sprout |
| steamdrake | — | lệch đối xứng L/R 8% | xương | **do mẫu không đối xứng theo x**, không phải lỗi rig. Trên mô hình thật nên kiểm lại |

## 4. Droplet, Zephyr, Steamdrake (thiếu limbCount)

Canonical data/GDD không ghi số chân, nên pipeline **không tự bịa**. Có hai cách:
1. Art director điền `limbCount` (0/2/4) vào `asset-overrides.json`.
2. Dùng `"limbCount": "auto"`. Rig chỉ được dựng khi phép đo trên lưới rõ ràng: 2 hoặc 4 bàn chân chạm đất đối xứng, độ tin cậy cao. Nếu không, rig bị từ chối và báo cáo ghi lại số đo. Khi được chấp nhận, batch ghi chú "Confirm with the art director".

Mỗi loài dùng archetype riêng theo sinh học trong canonical data, **không dùng rig Emberling**:
- **Zephyr:** winged. Cánh có giới hạn hình học để không xuyên thân (dò nhị phân trên lưới đã skin). Clip Fly/TakeOff/Glide/Land.
- **Steamdrake:** serpentine, thân dài nhiều đốt, uốn sóng thân. Có chân nếu đo được.
- **Droplet:** theo archetype của loài, có Swim. Chân co lại khi bơi (`swim.limbTuck`).

`Assets/Dravio/Character/Data/asset-overrides.u4-local.json` đặt sẵn "auto" cho 3 loài này để chạy thử tại máy.

## 5. Chạy trên GLB thật tại máy bạn

```powershell
powershell -ExecutionPolicy Bypass -File scripts\art\character\run_u4_local.ps1 `
  -Source "B:\Dravio\dravio_16_dragons_3d_pack\images" `
  -Blender "C:\Program Files\Blender Foundation\Blender 5.3\blender.exe" `
  -OldWork "<thư mục work của lần batch U1-CHAR.3>" -Work "B:\Dravio\work_u4"
```

Script làm theo thứ tự:
1. Đo lại rig cũ bằng QA mới (trước).
2. Chạy batch v2 (sau).
3. Ghi `B:\Dravio\work_u4\U4_before_after.md`.
4. Kiểm SHA-256 các GLB nguồn không đổi.

Không cần `-UnityOut`. Khi có `-UnityOut`, các tên trong `approved-assets.json` không bị ghi đè. Gửi lại `U4_before_after.md`, `batch-report.md` và các ảnh `qa_after\*.png`.

## 6. Không hồi quy

| Kiểm tra | Kết quả |
|---|---|
| Ngân sách LOD (24k/10k/4k) và PBR | không đổi. `prepare_character.py` và `build_lods.py` không sửa |
| Chiều cao | Emberling 0,90 m giữ nguyên (QA: rest 0,900, rig 0,900). Các model khác giữ kích thước nguồn |
| Batch cache | chạy lại không đổi thì `unchanged`. Đổi override thì chỉ file đó chạy lại. FBX đã duyệt giữ nguyên SHA |
| GLB nguồn | SHA-256 không đổi |
| Scene Open Game, Fantasy HUD, Core Domain, Persistence, DragonCarePreview | không sửa file nào |
| `run_selftest.py --bpy-module` | ALL OK (exit 0) |

## 7. Chưa kiểm chứng

- **Unity:** chưa chạy Unity 6000.6. Chưa kiểm import FBX, `DragonAnimationLab.unity` (menu **Dravio ▸ Character ▸ Animation Lab**), Foot IK trên địa hình bậc thang và ForceLOD trong Editor.
- **GLB thật:** 5 GLB thật chưa được xử lý bằng v2.
- **Chất lượng animation:** vẫn là animation procedural theo quy tắc. Animator cần duyệt bằng mắt trong Animation Lab. Chưa có mocap hay keyframe tay.
- **Chớp mắt (Idle blink):** chỉ tạo khi lưới có shape key mí mắt (tên chứa blink/eye_close). GLB Hunyuan thường không có, nên khi đó rig.json ghi chú "Idle blink not authored" thay vì xoay bone gây méo mắt. Mẫu không có shape key, nên chưa kiểm được.
