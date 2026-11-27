# CHAR.9 — Animation QA (16 rồng thật)

> **CHAR.9c (#95):** `anim_qa.py` mặc định đo 8 frame/clip gộp lại (không phải mọi frame), và không thấy được vết nứt ở đường nối UV (rig v7: 6–27 % chiều cao ở cả 16 loài). Số đo lại mọi frame, skinning tuyến tính: [Docs/char9c/CHAR9C_EMBERLING_RIG_QA.md](../char9c/CHAR9C_EMBERLING_RIG_QA.md).

> **Cập nhật CHAR.9b:** Emberling đã sửa và đạt ANIMATION_QA_PASS (Blender, skinning tuyến tính) — xem [`Docs/char9b/CHAR9B_EMBERLING_REPORT.md`](../char9b/CHAR9B_EMBERLING_REPORT.md). Số bên dưới là CHAR.9, đo với **dual quaternion** (Preserve Volume của rig v7), không phải skinning tuyến tính của Unity; 15 loài còn lại phải đo lại khi áp dụng pipeline CHAR.9b.

Nguồn số liệu: Blender 5.3, `anim_qa.py` v3, mọi frame mỗi clip, ngưỡng không đổi (`character-budgets.json`: giãn p99,9 ≤ 1,6; nén p0,1 ≥ 0,45; xuyên đất ≤ 0,01). Chi tiết từng model: `scripts/art/character/reports_char8_production/qa/`, bảng tổng: `scripts/art/character/TEST_REPORT_CHAR8_PRODUCTION.md` §4.

## Kết quả

- 16/16 loài rig được (rig v7), 13–21 clip đúng sinh học (không có Fly cho loài không bay, không Swim cho loài không bơi…).
- **ANIMATION_QA_PASS: 0/16.** Mỗi model có 8–16 clip giãn cạnh > 1,6. Clip đạt ở hầu hết model: Idle, Greet, Nuzzle, LookAround, Curious. Clip hỏng phổ biến: Sleep (16/16), Eat, Rest, Wake (15/16), Run, Drink (14/16), Bask (13/16), Walk (11/16).
- Trượt chân, xuyên đất, cánh xuyên thân, khép vòng clip và chuyển tiếp (Sleep→Wake→Idle, Idle→TakeOff→Fly→Land) đạt ở gần hết model; cảnh báo xuyên đất nhỏ ở vài loài bay khi Land.

## Emberling (ưu tiên P0) — file dùng cho scene Origin

| Clip hợp đồng | Nguồn rig v7 | Giãn p99,9 | Nén p0,1 | Xuyên đất |
|---|---|---|---|---|
| Idle | Idle (120 → 180 frame) | **1,345 ✓** | 0,784 | 0,0012 |
| Walk | Walk (25 → 35 frame) | 2,659 ✗ | 0,442 | 0 |
| Eat | Eat | 3,326 ✗ | 0,392 | 0 |
| Doze | Sleep | 3,317 ✗ | 0,413 | 0 |
| Happy | Happy | 1,970 ✗ | 0,429 | 0 |

Rig cũ của generator (khớp hộp bao + bone heat) trên cùng model: giãn 3,5–52, xuyên đất tới 1,7 × chiều cao → không dùng.

## Nguyên nhân hệ thống

67 % độ giãn nằm ở **thân / xương Pelvis sát chân**, 13 % chân sau: phân vùng chân–thân của rig tự động sai trên dáng chibi (chân ngắn, mập, mọc sát bụng), nên khi chân gập (Sleep, Rest, Bask, Eat, Run) cạnh nối bị kéo. Nới dải chuyển tiếp theo mét chỉ giúp Run (3,44 → 2,74), không giúp Sleep/Bask → chưa bật.

## Kế hoạch sửa (thứ tự)

1. Phân vùng chân theo khoảng cách trắc địa từ bàn chân tới gốc chân, có trần theo cao độ bụng.
2. Giảm biên độ gập chân của Sleep/Rest/Bask/Run/Eat theo tỉ lệ dài chân / cao thân.
3. Mai/giáp (Gaia, Mossback, Amberhorn) theo Spine/Chest; Leviathan giảm biên độ cuộn.
4. Chỉnh tay trong Blender, duyệt trong Animation Lab.
Không nới ngưỡng để đổi WARNING thành PASS.
