# U1-CHAR.7b: áp dụng sinh cơ học vào chuyển động — báo cáo kiểm thử

Cơ sở nghiên cứu, nguồn và giới hạn: [`Assets/Dravio/Character/BIOMECHANICS_RESEARCH.md`](../../../Assets/Dravio/Character/BIOMECHANICS_RESEARCH.md).

## Phạm vi bằng chứng

- **Môi trường:** Linux, Blender 5.3 (module `bpy`), .NET 8.
- **Không có Unity, không có 7 GLB thật.** Mọi số liệu là trên **SAMPLE**, cùng bộ 13 file của U1-CHAR.7.
- **Không model thật nào được nghiệm thu.** Để chạy tại máy bạn: `run_u7_local.ps1` (đã chuyển sang rig v6).

## 1. Kiểm thử tự động

| Kiểm thử | Kết quả |
|---|---|
| Python `unittest` | **75/75**: 64 cũ + 11 `test_biomech.py` (quan hệ Froude khứ hồi và tương đồng theo kích thước, Strouhal, vòng nghiêng, MaxBank/Gravity khớp giữa Python và C#, profile cất/hạ cánh trong dải tài liệu, dải biên độ cá) |
| `dotnet test` (Character) | **39/39**: thêm `CoordinatedBank_GrowsWithSpeedAndTurnRate_AndTheHeadStaysLevel`; `MotionShape_...` kiểm góc nghiêng theo tan φ = V·ω/g và đường cũ khi không có speed |
| `dotnet build TypeCheck` (stub) | Shaper biên dịch được |
| Benchmark, 30 rồng | 18,1 µs/frame, 0 B/frame |
| GLB nguồn | SHA-256 không đổi |
| CI trên PR | xanh ở 5b23f75 và 939430b |

## 2. QA trước/sau (SAMPLE, ngưỡng không đổi)

Trước là rig v5 (U1-CHAR.7), sau là rig v6. St là số Strouhal, A/L là biên độ đỉnh-đỉnh chóp đuôi hoặc cánh trên chiều dài thân.

| Model | QA v5 → v6 | Walk frames / m/s | Run frames / m/s | Fr walk / run | Swim m/s (St v5→v6, A/L) | Fly m/s (St v5→v6) | Air °/s | TakeOff share |
|---|---|---|---|---|---|---|---|---|
| droplet_Unstaged | PASS → PASS | 32→22 / 0.14→0.20 | 20→12 / 0.53→0.88 | 0.020/0.373 | 0.61→0.61 (0.76→0.764, A/L 0.46) | — | 60 | — |
| emberling_Unstaged | PASS → PASS | 32→22 / 0.14→0.20 | 20→12 / 0.53→0.88 | 0.020/0.373 | — | — | 60 | — |
| frostfin_Juvenile | PASS → PASS | — | — | — | 0.54→0.54 (0.10→0.103, A/L 0.06) | — | 60 | — |
| leviathan_Adult | PASS → PASS | — | — | — | 0.81→0.81 (0.11→0.109, A/L 0.07) | — | 60 | — |
| magma_Unstaged | PASS → PASS | 32→22 / 0.14→0.20 | 20→12 / 0.53→0.88 | 0.020/0.372 | — | — | 60 | — |
| sparkwing_Unstaged | PASS → PASS | 32→22 / 0.14→0.20 | 20→12 / 0.53→0.88 | 0.020/0.373 | — | 4.58→0.99 (0.06→0.3) | 180 | 0.85 |
| sprout_Unstaged | PASS → PASS | 32→22 / 0.13→0.19 | 20→12 / 0.51→0.84 | 0.018/0.341 | — | — | 60 | — |
| steamdrake_Unstaged | PASS → PASS | 40→40 / 0.06→0.06 | 26→26 / 0.19→0.19 | — (serpentine: giữ) | 0.85→0.85 (0.10→0.104, A/L 0.06) | — | 60 | — |
| zephyr_Unstaged | PASS → PASS | 32→22 / 0.14→0.20 | 20→12 / 0.53→0.88 | 0.020/0.373 | — | 4.58→0.99 (0.06→0.3) | 180 | 0.85 |

- **QA:** cả 9 model **PASS**, không có cảnh báo.
- **Chuyển tiếp:** vẫn ≤ 0,217°.
- **TakeOff/Land:** root offset không đổi (TakeOff 0,390 m, Land 0,307 m), vì hai đầu clip vẫn là tư thế đã author.
- **Land:** đo trên Zephyr, chênh độ cao đầu so với hông theo thời gian là 0,336 → 0,513 → 0,432 → 0,336 m. Thân ngóc lên khi hãm rồi về ngang trước lúc chạm đất.

## 3. Thay đổi và lý do

| Thay đổi | Lý do (tài liệu) | Kết quả trên SAMPLE |
|---|---|---|
| Walk 32 → 22 khung, Run 20 → 12 khung (quadruped, winged). Sải bước giữ nguyên | Alexander: λ/h = 2,3·Fr^0,3, với h là chiều cao hông đo trên model | Walk Fr 0,02 (đi chậm), Run Fr 0,37, ngay trên ngưỡng đi→chạy nước kiệu của thú, khớp duty 0,42. Tốc độ danh nghĩa 0,20 / 0,88 m/s |
| Serpentine giữ 40/26 khung | Quan hệ được fit trên động vật chân thẳng; thân uốn lượn góp vào sải | không đổi |
| Tốc độ Fly 4,58 → 0,99 m/s | St cũ chỉ 0,06, ngoài dải 0,2–0,4. Sải đập cánh bị giới hạn bởi mesh (không chạm thân), nên ở nhịp 0,94 Hz tốc độ hành trình nhất quán là khoảng 1 m/s | St 0,30. Tốc độ quay trên không suy ra là 180 °/s (trần) |
| Swim giữ 0,6 thân/chu kỳ | Biên độ đuôi đo được nằm ngoài dải của cá (A/L 0,1–0,3): Droplet 0,46 quá lớn; Frostfin, Leviathan, Steamdrake 0,06–0,07 quá nhỏ. Ép theo St sẽ cho tốc độ phi sinh học | gắn cờ trong `motion-report.md` để art director xem lại biên độ |
| TakeOff: chân đẩy (`launchShare` 0,9) | Chim lấy 80–95 % vận tốc ban đầu từ cú đạp chân | tỉ lệ vận tốc lúc rời đất 0,85 (v5: khoảng 0,3) |
| Land: ngóc thân 0,3 rad (~17°) | Bồ câu nghiêng mặt phẳng đập cánh lên 17 ± 7° khi hãm | đúng hướng, về ngang khi chạm đất |
| Runtime: nghiêng theo vòng nghiêng cân bằng, đầu giữ thăng bằng | Bồ câu nghiêng theo lý thuyết khí động; đầu gần như bất động trước chuyển động xoay | có test |
| `magma.run.liftLegs` 0,24 | Chu kỳ Run ngắn lấy mẫu trúng một tư thế giãn 1,601 (ngưỡng 1,6; v5 là 1,593). Fit trên mesh khối thô SAMPLE | 1,592, PASS |

## 4. Lỗi gặp và cách xử lý

| Lỗi | Nguyên nhân | Xử lý |
|---|---|---|
| Magma Run vượt ngưỡng 1,601 / 1,6 | khung lấy mẫu mới trúng tư thế cực trị vốn sát ngưỡng | giảm nhấc chân Run 0,26 → 0,24 cho riêng Magma. **Không nới ngưỡng** |
| Bản đầu dùng St cho mọi clip bơi, cho Droplet 1,57 m/s và Frostfin 0,19 m/s | biên độ đuôi của clip ngoài dải sinh học | chỉ dùng St khi A/L trong dải cá; ngoài dải giữ sải mặc định (cá đi 0,6–0,7 thân mỗi nhịp đuôi) và gắn cờ |

## 5. Chưa kiểm chứng / chờ quyết định

- **Unity:** shaper (vòng nghiêng, bù nghiêng đầu) trên bone thật; nhịp Walk/Run mới trong Animator; cảm giác tốc độ trên thiết bị.
- **GLB thật:** chưa xử lý. Override `magma.run` được fit trên mesh SAMPLE; với mesh thật, chạy `run_u7_local.ps1` rồi xem lại.
- **Art director cần quyết:**
  - biên độ đuôi khi bơi, để đưa A/L về khoảng 0,2;
  - tần số đập cánh: theo Pennycuick, chim sải 1,6 m đập 2–4 Hz, còn clip là 0,94 Hz (chủ ý cho rồng nặng nề, hoặc tăng lên);
  - nhịp thở theo khối lượng (cần khối lượng trong GDD).
- **Sanctuary:** nên đặt tốc độ di chuyển gần `groundSpeed` mới của từng clip. Playback rate chỉ bù trong khoảng 0,5–2 lần.
