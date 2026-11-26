# U2.13 — Danh sách quyết định cần duyệt

Mọi mục dưới đây đang **tắt hoặc giữ nguyên** trong game cho tới khi chủ dự án duyệt. Đã duyệt và đã làm: trứng Aurorawing ở chuyến sky_cloud thứ 15 (ADR 0208), vật liệu dự án theo thế giới (ADR 0209).

| # | Quyết định | Khuyến nghị | Bằng chứng | Nếu duyệt thì đổi gì |
|---|---|---|---|---|
| Q1 | Bảng thưởng `sky_cloud` | **Bảng A**: 45 phút, 2–4 bụi sao, mốc 1 / 5 / 15 / 40 → +2 / 3 / 4 / 6 bụi sao, danh hiệu `note.cloud_first_flight`, `title.cloud_rider`, `title.star_warden` | `SKY_CLOUD_BALANCE.md`: A/B/C cho cùng số chuyến (chuyến chạy offline); trứng đến ngày 18–22 (3×10 hoặc 60 phút/ngày), 60–107 (5 phút/ngày); không vàng, không XP | một dòng: `SkyCloudBalance.Production = ProposedA` |
| Q2 | Trứng Aurorawing: đột biến và giai đoạn nở | giữ **Normal, nở Baby, genotype founder tất định** (như trứng Droplet/Zephyr) | ADR 0208 bảng phương án | phương án khác: Rare cố định hoặc nở Juvenile (luật mới) |
| Q3 | Luật cho ăn | **B — cá thể được chọn ăn trước**, phần còn lại theo phân bổ cũ | `FEEDING_RESEARCH.md` (PR #67): A cho rồng được chọn ăn 20–60% số lần, 4/5 cá thể cùng loài không bao giờ ăn khi được chọn; B 100% khi rồng rảnh, không phí; C phí thức ăn khi rồng bận / hồi chiêu | luật mới trong `FeedCommand` (chưa làm) |
| Q4 | Dư vật liệu | chấp nhận sau ADR 0209 hoặc thêm nơi tiêu (chưa đề xuất số) | `SANCTUARY_MATERIALS.md`: vàng giới hạn số dự án/năm; vật liệu vẫn dư; bụi sao dư ~2 900/năm ở 3×10 phút | — |
| Q5 | F11: Casual 60 phút/ngày chỉ mở Thiên Giới ở 1/3 seed | xem lại yêu cầu `QUEST_SKY_EXPLORATION` (Ethology Thủy Vực) hoặc thêm gợi ý hành trình; **không** tự đổi | `ECONOMY_365.md` (U2.12), `SKY_CLOUD_BALANCE.md` dòng Casual 60 phút: trứng Aurorawing 33% seed | luật nhiệm vụ (U2.11) |
| Q6 | F13: kho tối đa 500 rồng | giữ; theo dõi | ECONOMY_365: Breeder chạm trần, phần thưởng vào hàng chờ không mất | — |
| Q7 | Trần sân mây có chung slot thám hiểm | giữ một slot chung (chi phí cơ hội có chủ đích) | bật sky làm chuyến rạn giảm từ 680–1 000 xuống 5–34/năm; vật liệu Thủy Vực vẫn dư | thêm slot = luật mới |

PR: #66 (U2.12), #68 (sky_cloud + trứng), #67 (nghiên cứu cho ăn), PR vật liệu Sanctuary (ADR 0209). Không PR nào merge `main` khi chưa duyệt.
