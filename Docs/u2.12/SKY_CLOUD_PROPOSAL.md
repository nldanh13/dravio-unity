# U2.12 — Đề xuất phần thưởng thám hiểm Thiên Giới và cân bằng hệ sinh thái vật liệu

> **Chỉ là đề xuất. Chưa bật, chưa có trong code.** Cần chủ dự án duyệt (ADR 0207 F1, F2, F3). Mọi số dưới đây dựa trên luật thật v10 và mô phỏng 365 ngày ([ECONOMY_365.md](ECONOMY_365.md)).
> Bật đề xuất cần: một ID vật liệu mới (`stardust`), bảng thưởng tuyến `sky_cloud`, mốc thám hiểm cho tuyến đó, và (tùy chọn) sửa công thức vật liệu E5. Save: `stardust` nằm trong `player.inventory` sẵn có và `expeditionsClaimed.sky_cloud` dùng bộ đếm sẵn có, nên **không cần Schema v11**; chỉ cần validator chấp nhận ID vật liệu mới (save cũ vẫn hợp lệ).

## 1. Vấn đề

| Hiện trạng v10 | Hệ quả |
|---|---|
| `sky_cloud` không có bảng thưởng (`Materials` rỗng) → `route-rewards-undefined` | Người chơi mở Thiên Giới không có hoạt động thám hiểm mới; `Q_EXPEDITION_CLOUD` không bao giờ đạt; Zephyr và các loài bay chỉ dùng cho nhiệm vụ cổng |
| Pha lê / san hô chỉ tiêu cho cổng Thiên Giới (4 + 4) và `Q_EXPEDITION_REEF` (3 san hô) | Sau khi mở Thiên Giới, tuyến Rạn Thủy Vực không còn lý do để đi; tồn kho pha lê / san hô chỉ tăng (xem ECONOMY_365 "Vật liệu còn giữ") |
| Dự án Sanctuary (E5) chỉ dùng gỗ + hạt giống, kể cả 12 công trình Thủy Vực / Thiên Giới | Ba thế giới chung một nguồn vật liệu (Rừng Khởi Nguyên); chương 2–3 của Sanctuary không gắn với thế giới của nó |
| Aurorawing không sở hữu được (F1) | `ACH_CODEX_16` và 3 danh hiệu không đạt được |

## 2. Đề xuất A — bảng thưởng `sky_cloud` (khuyến nghị)

| Thuộc tính | Giá trị | Lý do |
|---|---|---|
| Thời gian | 45 phút (giữ ADR U2.8-0201) | đã duyệt |
| Điều kiện | 1 Adult biết bay, đủ ngưỡng sức khỏe (giữ) | đã duyệt |
| Vật liệu | `stardust` ("Bụi sao") — **một loại** | GDD "bụi sao"; một vật liệu riêng cho thế giới thứ ba, không thêm nguồn gỗ / hạt / pha lê / san hô (không làm lệch kinh tế đã duyệt) |
| Số lượng | 2–4 đơn vị mỗi chuyến (như hai tuyến kia) | cùng thang tổng; một loại nên mỗi chuyến trung bình 3 bụi sao |
| Thành công | 100 % (giữ MVP) | không thất bại, không mất rồng |
| Vàng / XP | không | không tạo nguồn vàng mới |

Một slot thám hiểm chung vẫn giữ nguyên: người chơi chọn giữa rừng (15 phút), rạn (30 phút) và sân mây (45 phút). Với lịch 5 phút/ngày, mỗi ngày nhận khoảng một chuyến bất kể tuyến nào (chuyến chạy lúc offline), nên thời gian dài hơn không phạt người chơi nhẹ nhàng; với lịch dài, 45 phút là chi phí cơ hội thật.

### Mốc thám hiểm `sky_cloud` (cùng khung U2.11)

| Chuyến nhận thứ | Thưởng thêm | Kèm |
|---|---|---|
| 1 | 2 bụi sao | `note.cloud_first_flight` |
| 5 | 3 bụi sao | — |
| 15 | 4 bụi sao | `title.cloud_rider` **+ trứng Aurorawing (Normal, một lần, qua `rewardQueue` như trứng thế giới)** — sửa F1 |
| 40 | 6 bụi sao | `title.star_warden` |

- `Q_EXPEDITION_CLOUD` trở thành hoàn thành được (yêu cầu `routeAvailable` thành Met khi Thiên Giới mở) và thưởng `note.cloud_terrace` như đã có.
- Trứng Aurorawing: dùng lại cơ chế `grantedRewards` + `rewardQueue` (ID mới `EXPEDITION_EGG_AURORAWING`), trao một lần, không tính pity, kho đầy thì chờ. Không đổi tỉ lệ gói nào. Sau 15 chuyến sân mây (≈ 15 ngày với 5 phút/ngày nếu chỉ đi sân mây, thực tế 1–2 tháng vì chia slot với rừng), Codex 16/16 đạt được.

### Phương án thay thế cho F1 (nếu không muốn trứng thưởng)

| Phương án | Ưu | Nhược |
|---|---|---|
| (b) Aurorawing vào gói Astral (30 000 G) | không cần cơ chế mới | đổi tỉ lệ gói đã duyệt; Aurorawing thành "mua bằng vàng" |
| (c) Lai chéo Frostfin (A, Nước + Gió) × loài nhóm V | sâu về di truyền | phá luật "cùng nhóm" của TS gốc (parity) |

## 3. Đề xuất B — vật liệu theo thế giới cho dự án Sanctuary (sửa E5, cần duyệt riêng)

Giữ nguyên giá vàng, số dự án tối đa, số lượng `MaterialEach(n) = 1 + n / 5` (từ dự án thứ 3). Chỉ đổi **loại** vật liệu theo thế giới của công trình (`SanctuaryProjectContent`):

| Công trình | Hiện tại (E5) | Đề xuất |
|---|---|---|
| Khởi Nguyên (#1–8, vista origin) | gỗ + hạt | gỗ + hạt (giữ) |
| Thủy Vực (#9–14, vista ocean) | gỗ + hạt | **pha lê + san hô** |
| Thiên Giới (#15–20, vista sky) | gỗ + hạt | **bụi sao × 2** (một loại vật liệu, gấp đôi số lượng) |

Nhu cầu mỗi loại vật liệu cho 60 dự án (tính từ công thức hiện hành):

| Nguồn | Hiện tại | Đề xuất |
|---|---|---|
| Rừng Khởi Nguyên (gỗ, hạt — mỗi loại) | 388 | 128 |
| Rạn Thủy Vực (pha lê, san hô — mỗi loại) | 0 (+ 4 / 7 cho nhiệm vụ) | 125 |
| Sân mây (bụi sao) | — | 270 (= 2 × 135) |

Trong 25 dự án đầu (mức người chơi thường đạt sau 90 ngày theo U2.11): mỗi loại gỗ/hạt 19, pha lê/san hô 26, bụi sao 56. Với thưởng 2–4 mỗi chuyến, đó là khoảng 13 chuyến rừng, 17 chuyến rạn, 19 chuyến sân mây — cân hơn hiện tại (toàn bộ ~100 đơn vị dồn vào rừng).

**Ràng buộc để không tạo bế tắc** (phải có test khi bật):

1. Công trình của thế giới chưa mở vẫn hiển thị ở Khởi Nguyên (`DisplayZone`), nhưng nếu nó cần vật liệu của thế giới chưa mở thì người chơi chưa mở Thủy Vực sẽ **kẹt ở dự án #9**. Cách giải: công trình của thế giới chưa mở dùng vật liệu Khởi Nguyên (gỗ + hạt) — tức là vật liệu theo **thế giới đã mở cao nhất** chứ không theo thế giới của công trình. `ProgressionAudit` cần thêm kiểm tra "mỗi dự án luôn có nguồn vật liệu ở thế giới đang mở".
2. Save chỉ lưu số dự án đã xây nên không cần migration; dự án đã xây không bị tính lại.

## 4. Ảnh hưởng kinh tế dự kiến

- **Vàng:** không đổi (không thưởng vàng, giá dự án giữ nguyên). Giới hạn dự án dài hạn vẫn là giá vàng ×1,25 mỗi dự án (ECONOMY_365).
- **Slot thám hiểm:** chia ba thay vì hai. Người chơi 5 phút/ngày (~1 chuyến/ngày) cần khoảng 50 ngày để đủ vật liệu cho 25 dự án đầu theo đề xuất B — vẫn nhanh hơn tốc độ vàng cho phép (dự án #25 giá ≈ 4,2 M G).
- **Hệ sinh thái:** cả ba tuyến đều có lý do để đi suốt game; loài bơi và loài bay có vai trò sau cổng; pha lê / san hô có chỗ tiêu.

## 5. Việc cần làm khi được duyệt

1. `ExpeditionCatalog`: `MaterialDef("stardust", "Bụi sao", true)`, `sky_cloud.Materials = { "stardust" }`; `MaterialIds` thêm `stardust` (cuối danh sách, giữ thứ tự cũ).
2. `Exploration.Milestones`: 4 mốc `sky_cloud`; trứng Aurorawing qua `rewardQueue`.
3. Validator / migration: chấp nhận `stardust` trong inventory (không đổi số schema); ảnh v4 cho TS bỏ `stardust` như các khóa v10.
4. (Nếu duyệt B) `SanctuaryProjectCatalog.MaterialIdsFor(n, state)` theo ràng buộc mục 3.1; cập nhật `SanctuaryEconomyDto.NextProjectMaterialIds`.
5. Test: `ProgressionAudit` báo `Q_EXPEDITION_CLOUD` đạt được, Aurorawing sở hữu được; mô phỏng 365 ngày lại.
