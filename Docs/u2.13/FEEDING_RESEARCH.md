# U2.13 — Nghiên cứu cho ăn (chỉ nghiên cứu, KHÔNG đổi luật production)

> Sinh bởi `U213FeedingResearchTests.Write_u213_feeding_report_when_requested` (`DRAVIO_U213_FEED_OUT=<file.md>`). Lệnh `feed`, `consumeFood`, hồi XP 8 s (`CareRules.FeedXpCooldownMs`), giới hạn 5 phần ăn, phần ăn sống 12 s, thám hiểm: **luật thật**. Cách giao B và C chỉ được mô hình hóa trong test (hàm `Pick`), production vẫn là A (`BehaviorEngine.AssignFood`).

## Ba phương án

| Mã | Luật giao phần ăn | Người chơi thấy |
|---|---|---|
| **A** (hiện tại) | Phần ăn hết hạn sớm trước; rồng **thích** loại đó trước, rồi UID nhỏ nhất | Chọn rồng X rồi cho ăn, nhưng rồng khác chạy tới ăn |
| **B** | Rồng được chọn ăn được (đứng đó, rảnh, đúng khẩu vị, hết hồi XP) thì được giao; không thì như A | Rồng được chọn luôn tới ăn; nếu nó không ăn được thì phần ăn không bị phí |
| **C** | Hai chế độ: *cá thể* (phần ăn chỉ dành cho rồng được chọn; không ăn được thì hết hạn bỏ phí) và *tự do* (= A) | Người chơi bật / tắt chế độ; chế độ cá thể có thể phí thức ăn |

## Kịch bản

60 phiên cách nhau 10 phút, đồng cỏ Khởi Nguyên, 5 rồng Adult; mỗi phiên người chơi chọn một rồng theo vòng và thả một phần ăn (loại đầu tiên của loài, hoặc loại yêu thích của cá thể). Rồng được giao ăn sau 3 s. "Quan sát tìm ăn": với A là quyết định thật của `BehaviorEngine.Decide` lúc 1 s; với B / C, mục tiêu được giao thì đi tìm ăn. "Quan sát ăn": quyết định thật lúc 4 s sau khi ăn, ghi bằng `ResearchLog.Observe` thật.

## Kết quả

| Kịch bản | Chọn thức ăn | Cách giao | Mục tiêu được ăn | Rồng khác ăn | Bỏ phí | Bị cooldown | Mục tiêu bận | Quan sát "tìm ăn" mục tiêu | Quan sát "ăn" mục tiêu | Rồng ăn được / phần | XP cho ăn max / min |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 5 loài khác nhau (đồng cỏ) | FirstDiet | A_Current | 20 % | 48 | 0 | 0 | 0 | 12/60 | 12/60 | 5.0 | 480.0 |
| 5 loài khác nhau (đồng cỏ) | FirstDiet | B_SelectedFirst | 100 % | 0 | 0 | 0 | 0 | 60/60 | 60/60 | 5.0 | 1.0 |
| 5 loài khác nhau (đồng cỏ) | FirstDiet | C_IndividualMode | 100 % | 0 | 0 | 0 | 0 | 60/60 | 60/60 | 5.0 | 1.0 |
| 5 loài khác nhau (đồng cỏ) | Favorite | A_Current | 60 % | 24 | 0 | 0 | 0 | 36/60 | 36/60 | 4.0 | 288.0 |
| 5 loài khác nhau (đồng cỏ) | Favorite | B_SelectedFirst | 100 % | 0 | 0 | 0 | 0 | 60/60 | 60/60 | 4.0 | 1.0 |
| 5 loài khác nhau (đồng cỏ) | Favorite | C_IndividualMode | 100 % | 0 | 0 | 0 | 0 | 60/60 | 60/60 | 4.0 | 1.0 |
| 5 rồng cùng loài (Sproutling) | FirstDiet | A_Current | 20 % | 48 | 0 | 0 | 0 | 12/60 | 12/60 | 5.0 | 480.0 |
| 5 rồng cùng loài (Sproutling) | FirstDiet | B_SelectedFirst | 100 % | 0 | 0 | 0 | 0 | 60/60 | 60/60 | 5.0 | 1.0 |
| 5 rồng cùng loài (Sproutling) | FirstDiet | C_IndividualMode | 100 % | 0 | 0 | 0 | 0 | 60/60 | 60/60 | 5.0 | 1.0 |
| 5 rồng cùng loài (Sproutling) | Favorite | A_Current | 40 % | 36 | 0 | 0 | 0 | 24/60 | 24/60 | 5.0 | 288.0 |
| 5 rồng cùng loài (Sproutling) | Favorite | B_SelectedFirst | 100 % | 0 | 0 | 0 | 0 | 60/60 | 60/60 | 5.0 | 1.0 |
| 5 rồng cùng loài (Sproutling) | Favorite | C_IndividualMode | 100 % | 0 | 0 | 0 | 0 | 60/60 | 60/60 | 5.0 | 1.0 |
| 5 loài, mục tiêu thứ 2 đang thám hiểm | FirstDiet | A_Current | 20 % | 48 | 0 | 0 | 12 | 12/60 | 12/60 | 4.0 | 480.0 |
| 5 loài, mục tiêu thứ 2 đang thám hiểm | FirstDiet | B_SelectedFirst | 80 % | 12 | 0 | 0 | 12 | 48/60 | 48/60 | 4.0 | 192.0 |
| 5 loài, mục tiêu thứ 2 đang thám hiểm | FirstDiet | C_IndividualMode | 80 % | 0 | 12 | 0 | 12 | 48/60 | 48/60 | 4.0 | 96.0 |
| 5 loài, mục tiêu thứ 2 đang thám hiểm | Favorite | A_Current | 40 % | 36 | 0 | 0 | 12 | 24/60 | 24/60 | 3.2 | 288.0 |
| 5 loài, mục tiêu thứ 2 đang thám hiểm | Favorite | B_SelectedFirst | 80 % | 12 | 0 | 0 | 12 | 48/60 | 48/60 | 3.2 | 192.0 |
| 5 loài, mục tiêu thứ 2 đang thám hiểm | Favorite | C_IndividualMode | 80 % | 0 | 12 | 0 | 12 | 48/60 | 48/60 | 3.2 | 96.0 |
| 5 loài, hai bữa cách 2 s | FirstDiet | A_Current | 20 % | 96 | 0 | 0 | 0 | 12/60 | 12/60 | 4.5 | 480.0 |
| 5 loài, hai bữa cách 2 s | FirstDiet | B_SelectedFirst | 100 % | 60 | 0 | 0 | 0 | 60/60 | 60/60 | 4.5 | 5.0 |
| 5 loài, hai bữa cách 2 s | FirstDiet | C_IndividualMode | 100 % | 0 | 60 | 0 | 0 | 60/60 | 60/60 | 4.5 | 1.0 |
| 5 loài, hai bữa cách 2 s | Favorite | A_Current | 60 % | 72 | 0 | 0 | 0 | 36/60 | 36/60 | 3.5 | 4.0 |
| 5 loài, hai bữa cách 2 s | Favorite | B_SelectedFirst | 100 % | 60 | 0 | 0 | 0 | 60/60 | 60/60 | 3.5 | 4.0 |
| 5 loài, hai bữa cách 2 s | Favorite | C_IndividualMode | 100 % | 0 | 60 | 0 | 0 | 60/60 | 60/60 | 3.5 | 1.0 |

Cột "XP cho ăn max / min": tỉ lệ XP-từ-thức-ăn của cá thể nhiều nhất với cá thể ít nhất (min 0 thì ghi bằng XP của cá thể nhiều nhất). Một bữa cho 8 s XP (chất lượng thức ăn cấp 0), bằng khoảng 2–3 % thời gian lớn của loài Common (5–8 phút), nên XP không phải lý do chính; vấn đề là **trải nghiệm và nghiên cứu**.

## Kết luận

1. **A làm hỏng ý định của người chơi.** Với 5 rồng, rồng được chọn chỉ được ăn 20 % số lần (chọn loại thức ăn đầu tiên) hoặc 40–60 % (chọn loại yêu thích). Cùng loài thì UID nhỏ nhất luôn thắng: 4 / 5 cá thể không bao giờ được ăn khi người chơi chọn chúng (XP cho ăn dồn vào 1 cá thể), và không bao giờ quan sát được "tìm ăn" của chúng. Đây là F4 của ADR 0207, nay có số liệu.
2. **B đạt 100 % ở mọi kịch bản rồng rảnh**, không phí thức ăn, giữ A làm dự phòng khi mục tiêu bận / cooldown (rồng bận thám hiểm: 80 %, đúng bằng 4 / 5 phiên rồng rảnh). Quan sát "tìm ăn" và "ăn" của mục tiêu: 60 / 60 so với 12–36 / 60 của A.
3. **C (chế độ cá thể) bằng B về tỉ lệ trúng nhưng phí thức ăn** khi mục tiêu bận (12 phần) hoặc trong hồi XP (bữa thứ hai sau 2 s: 60 phần bỏ phí). Thêm một công tắc UI mà không lợi gì hơn B.
4. **Tranh chấp:** trung bình 3,2–5 rồng ăn được mỗi phần ăn. A giải tranh chấp theo khẩu vị rồi UID (tất định, không ngẫu nhiên); B chỉ thêm một bước "rồng được chọn trước".
5. **Busy:** cả ba đều không cho rồng bận (thám hiểm / lai / chăm sóc) ăn (`CanEat`, `consumeFood` trả `dragon-unavailable`). Nên giữ.
6. **Cooldown 8 s:** A và B giao bữa thứ hai cho rồng khác (không phí); C phí. Không cần đổi cooldown.

## Khuyến nghị (cần chủ dự án duyệt trước khi đổi luật)

- **Chọn B** (ưu tiên cá thể được chọn, dự phòng A). Đổi luật nhỏ: `AssignFood(s, zone, habitat, now, preferredUid)` với tham số tùy chọn; lệnh `feed` thêm trường tùy chọn `targetDragonUid` (không đổi save: phần ăn đã có trong `food.drops`, cần thêm khóa `targetUid` vào drop nếu muốn giữ ưu tiên sau khi nạp lại, hoặc chấp nhận mất ưu tiên khi nạp vì phần ăn chỉ sống 12 s).
- Không chọn C.
- Trước khi duyệt: UI có thể làm ngay theo luật hiện hành — gợi ý loại thức ăn mà chỉ rồng được chọn ăn được (cách Guided trong mô phỏng U2.12) và gửi `consumeFood` cho đúng rồng đó (luật hiện hành cho phép, "ăn" quan sát được; "tìm ăn" thì không).
