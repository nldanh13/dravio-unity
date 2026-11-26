# U2.14 — Rà soát cân bằng Alpha: hai quyết định đang chờ duyệt

> **Không luật nào được bật trong U2.14.** `SkyCloudBalance.Production = Disabled`, `FeedCommand` / `BehaviorEngine.AssignFood` giữ luật A. Bảng A chỉ chạy trong phạm vi `SkyCloudBalance.Use(...)` của test / mô phỏng; luật cho ăn B chỉ được mô hình hóa trong `U213FeedingResearchTests`.
> Nguồn số liệu, theo loại:
> - **Đã chạy (U2.14):** `U214AlphaJourneyTests` (hành trình 40 ngày, lệnh thật, có/không bảng A), `U213FeedingResearchTests` chạy lại trên `main` mới nhất (kết quả trùng từng ô với `Docs/u2.13/FEEDING_RESEARCH.md`).
> - **Mô phỏng (U2.14):** `U214EconomyAlphaTests.Write_u214_economy_report_when_requested`, 3 seed, 365 ngày, Guided / Casual / Researcher, production và bảng A (`DRAVIO_U214_VARIANTS=production,skyA`). Bảng ở mục 1.2.
> - **Mô phỏng (U2.13, không chạy lại):** `Docs/u2.13/SKY_CLOUD_BALANCE.md` cho so sánh A / B / C.

## Tóm tắt khuyến nghị cuối cùng

| # | Quyết định | Khuyến nghị | Nếu duyệt: thay đổi | Rủi ro nếu không duyệt trước Alpha |
|---|---|---|---|---|
| 1 | Bảng thưởng `sky_cloud` | **Duyệt bảng A** (45 phút, 2–4 bụi sao, mốc 1 / 5 / 15 / 40 → +2 / 3 / 4 / 6) | 1 dòng: `SkyCloudBalance.Production = ProposedA`; không đổi save | Alpha chỉ có 15 / 16 loài, 16 / 17 nhiệm vụ; `ACH_CODEX_16` và 3 danh hiệu không đạt được |
| 2 | Cho ăn ưu tiên rồng được chọn | **Duyệt luật B** (rồng được chọn ăn trước nếu ăn được, không thì luật A) | `feed` thêm trường tùy chọn `targetDragonUid`; `AssignFood` nhận `preferredUid`. **Không đổi save** (phần ăn không lưu) | UI dùng cách tạm (mục 2.4): ăn đúng rồng, nhưng "tìm ăn" vẫn có thể là rồng khác |

---

## 1. Bảng thưởng `sky_cloud`

### 1.1 Hiện trạng đã kiểm

| Kiểm tra | Kết quả | Loại |
|---|---|---|
| Lệnh `startExpedition("sky_cloud", …)` trong game | `route-rewards-undefined` | Đã chạy |
| Hành trình Alpha 40 ngày, production | 16 / 17 nhiệm vụ (chỉ thiếu `Q_EXPEDITION_CLOUD`), 0 chuyến sân mây, 0 bụi sao, không trứng Aurorawing | Đã chạy (3 seed) |
| Cùng hành trình, bảng A trong phạm vi | 17 / 17 nhiệm vụ, đúng 1 trứng Aurorawing, sổ vàng / vật liệu đóng | Đã chạy (seed 1) |
| Kiểm toán catalog (`AuditCatalog`) | production 15 / 16 loài; A, B, C đều 16 / 16 | Đã chạy |
| Tuyến trả vàng / XP | không (cả A, B, C) | Đã chạy (U2.13) |

### 1.2 Số liệu 365 ngày (U2.14, production so với bảng A, trung vị 3 seed)

| Kiểu | Lịch | Nhiệm vụ 365 (tắt → A) | Loài 365 | Dự án 365 | Vàng giữ 365 | Pha lê / san hô cuối | Bụi sao cuối | Ngày trứng Aurorawing (% seed) | Vi phạm (A) |
|---|---|---|---|---|---|---|---|---|---|
| Nghiên cứu | 5 phút/ngày | 14 → 15 | 14 → 15 | 35 → 34 | 36.41 M → 28.14 M | 396 / 395 → 9 / 1 | 0 → 820 | 50 (100%) | 0 |
| Nghiên cứu | 3 × 10 phút/ngày | 14 → 16 | 14 → 15 | 34 → 34 | 31.29 M → 32.73 M | 1508 / 1523 → 8 / 0 | 0 → 2934 | 29.2 (100%) | 0 |
| Nghiên cứu | 60 phút/ngày | 13 → 14 | 14 → 15 | 33 → 33 | 33.91 M → 27.33 M | 941 / 987 → 19 / 9 | 0 → 1704 | 53.2 (66%) | 0 |
| Guided | 5 phút/ngày | 16 → 17 | 14 → 15 | 32 → 32 | 14.95 M → 12.51 M | 417 / 418 → 26 / 7 | 0 → 804 | 44.5 (66%) | 0 |
| Guided | 3 × 10 phút/ngày | 16 → 17 | 14 → 15 | 32 → 33 | 18.05 M → 27.18 M | 1507 / 1529 → 7 / 12 | 0 → 3016 | 14.5 (100%) | 0 |
| Guided | 60 phút/ngày | 15 → 16 | 14 → 15 | 32 → 32 | 18.50 M → 21.18 M | 965 / 1017 → 7 / 2 | 0 → 1991 | 19.5 (100%) | 0 |
| Casual | 5 phút/ngày | 12 → 12 | 14 → 15 | 33 → 33 | 15.07 M → 17.05 M | 398 / 407 → 13 / 9 | 0 → 808 | 50.5 (66%) | 0 |
| Casual | 3 × 10 phút/ngày | 11 → 12 | 14 → 15 | 35 → 34 | 39.90 M → 28.95 M | 1512 / 1486 → 111 / 115 | 0 → 2818 | 37.0 (66%) | 0 |
| Casual | 60 phút/ngày | 10 → 10 | 14 → 14 | 33 → 33 | 19.55 M → 30.05 M | 1016 / 980 → 1000 / 980 | 0 → 0 | 33 (33%) | 0 |

Đọc bảng: "Loài" là số loài trong Codex (trung vị). Vàng giữ khác nhau giữa hai cột do luồng RNG giao dịch khác (chuyến đi khác → gói khác), không có nguồn vàng mới (0 vi phạm sổ). Bật A thì pha lê / san hô dư gần hết (slot chung chuyển sang sân mây) mà vẫn 0 % seed bị chặn vì vật liệu; bụi sao dư 800–3 000. Casual 60 phút/ngày: chỉ 1 / 3 seed tới Thiên Giới (F11) nên trứng 33 %.

### 1.3 A / B / C (U2.13, 3 seed, 365 ngày)

| Chỉ số (Guided, 3 × 10 phút/ngày) | A · 45 phút · 2–4 | B · 30 phút · 2–4 | C · 60 phút · 3–5 |
|---|---|---|---|
| Chuyến sân mây 30 / 90 / 365 ngày | 30 / 185 / 975 | 30 / 185 / 975 | 30 / 185 / 975 |
| Bụi sao giữ ở ngày 365 | 2 942 | 2 942 | 3 923 |
| Ngày nhận trứng Aurorawing | 21,2 | 21,2 | 21,2 |
| Chuyến rạn / năm (slot chung) | 18 | 18 | 18 |
| Vàng, XP từ tuyến | 0 | 0 | 0 |

**Vì sao A, B, C cho cùng số chuyến:** chuyến hoàn tất trong lúc offline, người chơi nhận một lần mỗi phiên; thời gian 30–60 phút đều ngắn hơn khoảng cách giữa hai phiên. Thời lượng gần như không phải đòn bẩy; số lượng bụi sao mới là đòn bẩy.

### 1.4 Ưu / nhược

| Phương án | Ưu | Nhược |
|---|---|---|
| **A** (khuyến nghị) | thời lượng khớp ADR 0201 (45 phút, như rạn); số lượng bằng tuyến khác; trứng Aurorawing ngày 14,5–19,5 với Guided 3 × 10 / 60 phút, 29–53 với người chơi ít hơn (mục 1.2); mở 16/16 loài, 17/17 nhiệm vụ | bụi sao dư (~2 900/năm ở 3 × 10 phút) vì chỉ dự án Thiên Giới tiêu; chuyến rạn giảm mạnh vì chung slot (chi phí cơ hội có chủ đích, Q7 U2.13) |
| B | như A; người chơi online lâu có thể nhận 2 chuyến / phiên | không khác A trong mô phỏng; chuyến ngắn hơn tuyến khác làm sân mây thành lựa chọn "luôn đúng" cho slot chung |
| C | cảm giác "chuyến xa" hơn | +33 % bụi sao vào kho vốn đã dư; mốc lớn hơn, không lý do kinh tế |
| Giữ tắt | không rủi ro kinh tế | Alpha thiếu 1 loài, 1 nhiệm vụ, `ACH_CODEX_16`, `title.dragon_scholar`, `title.lifecycle_master`, `title.grand_ethologist`; luật trứng đã duyệt không bao giờ chạy |

### 1.5 Khuyến nghị cuối cùng

Duyệt **bảng A**. Đi kèm (không bắt buộc cùng lúc): theo dõi bụi sao dư (Q4 U2.13) sau Alpha, và quyết định Q2 U2.13 (Aurorawing nở Normal / Baby, đang làm như trứng thế giới). Bật = một dòng, không migration; save cũ đã có bộ đếm `expeditionsClaimed.sky_cloud = 0`.

---

## 2. Cho ăn ưu tiên rồng được chọn

### 2.1 Số liệu (đã chạy lại trên `main` U2.14, trùng U2.13)

| Kịch bản (60 phiên) | Rồng được chọn được ăn: A | B | C | Phần ăn bỏ phí: A / B / C |
|---|---|---|---|---|
| 5 loài, chọn loại đầu tiên của loài | 20 % | 100 % | 100 % | 0 / 0 / 0 |
| 5 loài, chọn loại yêu thích | 60 % | 100 % | 100 % | 0 / 0 / 0 |
| 5 rồng cùng loài | 20 % (4 / 5 cá thể không bao giờ được ăn) | 100 % | 100 % | 0 / 0 / 0 |
| Mục tiêu đang thám hiểm (1 / 5 phiên) | 20 % | 80 % | 80 % | 0 / 0 / 12 |
| Hai bữa cách 2 s (hồi XP 8 s) | 20 % | 100 % | 100 % | 0 / 0 / 60 |

Quan sát "tìm ăn" của rồng được chọn: A 12–36 / 60, B 60 / 60. XP một bữa = 8 s (~2–3 % thời gian lớn của loài Common): ảnh hưởng kinh tế không đáng kể; vấn đề là trải nghiệm và nghiên cứu (Ethology cần cả "tìm ăn" lẫn "ăn").

### 2.2 Phát hiện mới U2.14: luật B không cần đổi save

U2.13 ghi "cần thêm khóa `targetUid` vào drop nếu muốn giữ ưu tiên sau khi nạp lại". Kiểm lại: `SaveSchema.ToPersistedState` **bỏ toàn bộ phần ăn đang rơi** khỏi save (ADR 0004 #10). Ưu tiên chỉ cần sống trong bộ nhớ 12 s của phần ăn → **Schema v10 giữ nguyên**, không migration. Thay đổi chỉ ở lệnh `feed` (trường tùy chọn, mặc định null = luật A, nên golden TS cũ vẫn trùng) và `AssignFood`.

### 2.3 Ưu / nhược

| Phương án | Ưu | Nhược |
|---|---|---|
| A (hiện tại) | đã có, tất định, parity TS | ý định người chơi bị bỏ qua 40–80 % số lần; cá thể UID lớn cùng loài không bao giờ được chọn ăn |
| **B** (khuyến nghị) | 100 % khi rồng rảnh; không phí; giữ A làm dự phòng; không đổi save | thêm một trường lệnh và một tham số; cần cập nhật test parity cho trường mới |
| C (chế độ cá thể) | bằng B về tỉ lệ | phí thức ăn khi bận / hồi; thêm công tắc UI |

### 2.4 Trong lúc chờ (đã chạy trong hành trình Alpha)

Người chơi Guided chọn loại thức ăn mà chỉ rồng mục tiêu ưu tiên, rồi gửi `consumeFood(food, selectedUid)`: 120 / 120 bữa đúng rồng trong 40 ngày (seed 1). UI làm được ngay; chỉ "tìm ăn" của mục tiêu là chưa đảm bảo.

### 2.5 Khuyến nghị cuối cùng

Duyệt **B**. Không chọn C. Ước lượng công việc sau duyệt: một PR Domain nhỏ (lệnh + `AssignFood` + test), không đụng Persistence.

---

## 3. Phát hiện mới cần chủ dự án biết (không đổi gì trong U2.14)

| # | Phát hiện | Bằng chứng | Đề xuất | Cần duyệt |
|---|---|---|---|---|
| N1 | **60 dự án Sanctuary không hoàn thành được trong thời gian thực tế.** Giá tăng 25 % / dự án: tổng 60 dự án ≈ 52,2 tỉ G; dự án thứ 40 đã 120 triệu G | `Sixty_projects_cost_table_…`, [ECONOMY_ALPHA.md](ECONOMY_ALPHA.md) mục 4 | chấp nhận như "trần dài hạn" hoặc đổi đường cong giá sau Alpha (vd 1,15 / dự án) — **chưa đề xuất số**, cần mục tiêu thiết kế | có |
| N2 | Bán Aurorawing duy nhất = mất loài vĩnh viễn (trứng một lần, không cặp lai nào khác) | `Deadlock_selling_the_only_aurorawing_…` | UI: xác nhận khi bán rồng thưởng cuối cùng của loài; Domain giữ nguyên | không (UI) / có (nếu muốn chặn trong Domain) |
| N3 | Validator không giới hạn số rồng (save sửa tay > 500 vẫn nạp) | `A_save_above_500_dragons_…` | thêm kiểm ở bản sau | có |
| — | Các mục còn mở của U2.13: Q2 (Aurorawing Normal / Baby), Q4 (vật liệu dư), Q5 (F11 Casual 60 phút), Q6, Q7 | `Docs/u2.13/DECISIONS.md` | không đổi | như U2.13 |
