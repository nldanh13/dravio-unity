# U2.13 — Bảng cân bằng tuyến Thiên Giới `sky_cloud` (CHỜ DUYỆT)

> **Trạng thái:** bảng thưởng **chưa duyệt → tuyến vẫn TẮT trong game** (`SkyCloudBalance.Production = Disabled`). Các bảng A / B / C có trong code nhưng chỉ chạy trong mô phỏng / test qua `SkyCloudBalance.Use(...)`. Bật tuyến sau khi duyệt = đổi một dòng (`Production = ProposedA` hoặc bảng được chọn), không đổi save (Schema v10).
> Đã duyệt và đã làm: trứng Aurorawing một lần tại chuyến `sky_cloud` thứ 15 đã nhận ([ADR 0208](../adr/0208-u2-13-sky-cloud-aurorawing.md)). Khi tuyến tắt, trứng không bao giờ trao.
> Số liệu: `U213SkyCloudSimulationTests.Write_u213_sky_report_when_requested` (Release, 3 seed, 365 ngày, luật thật v10 + U2.13), `DRAVIO_U213_SKY_OUT=<thư mục>`.

## 1. Ba bảng ứng viên

| Thuộc tính | A (khuyến nghị) | B | C |
|---|---|---|---|
| Thời gian chuyến | 45 phút (ADR U2.8-0201) | 30 phút | 60 phút |
| Bụi sao mỗi chuyến | 2–4 (trung bình 3) | 2–4 | 3–5 (trung bình 4) |
| Mốc 1 / 5 / 15 / 40 chuyến | +2 / +3 / +4 / +6 | +2 / +3 / +4 / +6 | +3 / +4 / +6 / +8 |
| Ghi chú / danh hiệu | `note.cloud_first_flight`, `title.cloud_rider` (15), `title.star_warden` (40) | như A | như A |
| Trứng Aurorawing | chuyến 15 (đã duyệt) | chuyến 15 | chuyến 15 |
| Vàng / XP | không | không | không |
| Điều kiện rồng | Adult biết bay, Health ≥ 80, Mobility ≥ 80, Comfort ≥ 60, không ca chăm sóc mở (luật U2.6/U2.8, không đổi) | như A | như A |
| Thành công | 100 % (MVP) | 100 % | 100 % |

## 2. Kết quả mô phỏng 365 ngày (trung vị 3 seed)

| Bảng | Kiểu | Lịch | Chuyến sân mây 30 / 90 / 365 ngày | Bụi sao còn giữ 30 / 90 / 365 | Ngày nhận trứng Aurorawing (% seed) | Chuyến rừng 365 | Chuyến rạn 365 | Gỗ / hạt còn | Pha lê / san hô còn | Vàng 365 (trung vị) | Nhiệm vụ 365 | Loài | Ngày mở Thiên Giới | Số trứng trao tối đa / seed |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Tắt (hiện tại) | Casual | 5 phút/ngày | 0 / 0 / 0 | 0 / 0 / 0 | — (0%) | 94 | 265 | 26 / 9 | 400 / 402 | 14.19 M | 12 | 14 | 79 (100%) | 0 |
| Tắt (hiện tại) | Casual | 3 × 10 phút/ngày | 0 / 0 / 0 | 0 / 0 / 0 | — (0%) | 97 | 982 | 29 / 9 | 1488 / 1477 | 39.35 M | 14 | 14 | 11.5 (100%) | 0 |
| Tắt (hiện tại) | Casual | 60 phút/ngày | 0 / 0 / 0 | 0 / 0 / 0 | — (0%) | 87 | 685 | 8 / 10 | 1006 / 1050 | 10.54 M | 10 | 13 | 11.5 (33%) | 0 |
| Tắt (hiện tại) | Guided | 5 phút/ngày | 0 / 0 / 0 | 0 / 0 / 0 | — (0%) | 78 | 282 | 7 / 13 | 441 / 422 | 21.25 M | 16 | 14 | 31 (100%) | 0 |
| Tắt (hiện tại) | Guided | 3 × 10 phút/ngày | 0 / 0 / 0 | 0 / 0 / 0 | — (0%) | 81 | 1008 | 17 / 7 | 1524 / 1500 | 18.94 M | 16 | 14 | 13.2 (100%) | 0 |
| Tắt (hiện tại) | Guided | 60 phút/ngày | 0 / 0 / 0 | 0 / 0 / 0 | — (0%) | 77 | 693 | 7 / 15 | 1085 / 1022 | 10.03 M | 16 | 14 | 13 (100%) | 0 |
| Tắt (hiện tại) | Researcher | 5 phút/ngày | 0 / 0 / 0 | 0 / 0 / 0 | — (0%) | 90 | 268 | 15 / 8 | 418 / 427 | 28.76 M | 14 | 14 | 34 (66%) | 0 |
| Tắt (hiện tại) | Researcher | 3 × 10 phút/ngày | 0 / 0 / 0 | 0 / 0 / 0 | — (0%) | 91 | 994 | 9 / 9 | 1487 / 1524 | 44.72 M | 15 | 14 | 12.2 (100%) | 0 |
| Tắt (hiện tại) | Researcher | 60 phút/ngày | 0 / 0 / 0 | 0 / 0 / 0 | — (0%) | 93 | 682 | 9 / 21 | 1037 / 1057 | 16.43 M | 14 | 14 | 9.7 (66%) | 0 |
| A · 45 phút · 2–4 | Casual | 5 phút/ngày | 0 / 6 / 233 | 0 / 23 / 719 | 107 (100%) | 92 | 34 | 9 / 12 | 58 / 61 | 21.10 M | 12 | 15 | 79 (100%) | 1 |
| A · 45 phút · 2–4 | Casual | 3 × 10 phút/ngày | 39 / 191 / 975 | 126 / 594 / 2881 | 19.2 (100%) | 89 | 15 | 11 / 8 | 27 / 19 | 27.61 M | 14 | 15 | 11.5 (100%) | 1 |
| A · 45 phút · 2–4 | Casual | 60 phút/ngày | 0 / 0 / 0 | 0 / 0 / 0 | 21 (33%) | 87 | 685 | 8 / 9 | 1000 / 1050 | 10.54 M | 10 | 13 | 11.5 (33%) | 1 |
| A · 45 phút · 2–4 | Guided | 5 phút/ngày | 0 / 37 / 276 | 0 / 118 / 849 | 60 (100%) | 80 | 5 | 10 / 8 | 8 / 3 | 6.44 M | 16 | 15 | 31 (100%) | 1 |
| A · 45 phút · 2–4 | Guided | 3 × 10 phút/ngày | 30 / 185 / 975 | 97 / 571 / 2942 | 21.2 (100%) | 86 | 18 | 9 / 23 | 31 / 27 | 24.17 M | 17 | 15 | 13.2 (100%) | 1 |
| A · 45 phút · 2–4 | Guided | 60 phút/ngày | 28 / 138 / 672 | 93 / 424 / 2045 | 21.5 (100%) | 83 | 17 | 8 / 8 | 27 / 25 | 29.78 M | 17 | 15 | 13 (100%) | 1 |
| A · 45 phút · 2–4 | Researcher | 5 phút/ngày | 0 / 24 / 263 | 0 / 79 / 781 | 71 (66%) | 91 | 5 | 11 / 8 | 12 / 5 | 27.03 M | 16 | 15 | 34 (66%) | 1 |
| A · 45 phút · 2–4 | Researcher | 3 × 10 phút/ngày | 29 / 181 / 969 | 101 / 568 / 2935 | 21.5 (100%) | 95 | 18 | 8 / 11 | 28 / 32 | 18.55 M | 16 | 15 | 12.2 (100%) | 1 |
| A · 45 phút · 2–4 | Researcher | 60 phút/ngày | 35 / 140 / 674 | 101 / 420 / 2001 | 17.7 (66%) | 94 | 6 | 8 / 22 | 11 / 6 | 16.43 M | 15 | 15 | 9.7 (66%) | 1 |
| B · 30 phút · 2–4 | Casual | 5 phút/ngày | 0 / 6 / 233 | 0 / 23 / 719 | 107 (100%) | 92 | 34 | 9 / 12 | 58 / 61 | 21.10 M | 12 | 15 | 79 (100%) | 1 |
| B · 30 phút · 2–4 | Casual | 3 × 10 phút/ngày | 39 / 191 / 975 | 126 / 594 / 2881 | 19.2 (100%) | 89 | 15 | 11 / 8 | 27 / 19 | 27.61 M | 14 | 15 | 11.5 (100%) | 1 |
| B · 30 phút · 2–4 | Casual | 60 phút/ngày | 0 / 0 / 0 | 0 / 0 / 0 | 21 (33%) | 87 | 685 | 8 / 9 | 1000 / 1050 | 10.54 M | 10 | 13 | 11.5 (33%) | 1 |
| B · 30 phút · 2–4 | Guided | 5 phút/ngày | 0 / 37 / 276 | 0 / 118 / 849 | 60 (100%) | 80 | 5 | 10 / 8 | 8 / 3 | 6.44 M | 16 | 15 | 31 (100%) | 1 |
| B · 30 phút · 2–4 | Guided | 3 × 10 phút/ngày | 30 / 185 / 975 | 97 / 571 / 2942 | 21.2 (100%) | 86 | 18 | 9 / 23 | 31 / 27 | 24.17 M | 17 | 15 | 13.2 (100%) | 1 |
| B · 30 phút · 2–4 | Guided | 60 phút/ngày | 28 / 138 / 672 | 93 / 424 / 2045 | 21.5 (100%) | 83 | 17 | 8 / 8 | 27 / 25 | 29.78 M | 17 | 15 | 13 (100%) | 1 |
| B · 30 phút · 2–4 | Researcher | 5 phút/ngày | 0 / 24 / 263 | 0 / 79 / 781 | 71 (66%) | 91 | 5 | 11 / 8 | 12 / 5 | 27.03 M | 16 | 15 | 34 (66%) | 1 |
| B · 30 phút · 2–4 | Researcher | 3 × 10 phút/ngày | 29 / 181 / 969 | 101 / 568 / 2935 | 21.5 (100%) | 95 | 18 | 8 / 11 | 28 / 32 | 18.55 M | 16 | 15 | 12.2 (100%) | 1 |
| B · 30 phút · 2–4 | Researcher | 60 phút/ngày | 35 / 140 / 674 | 101 / 420 / 2001 | 17.7 (66%) | 94 | 6 | 8 / 22 | 11 / 6 | 16.43 M | 15 | 15 | 9.7 (66%) | 1 |
| C · 60 phút · 3–5 | Casual | 5 phút/ngày | 0 / 6 / 233 | 0 / 31 / 958 | 107 (100%) | 92 | 34 | 9 / 12 | 58 / 61 | 21.10 M | 12 | 15 | 79 (100%) | 1 |
| C · 60 phút · 3–5 | Casual | 3 × 10 phút/ngày | 39 / 191 / 975 | 169 / 791 / 3862 | 19.2 (100%) | 89 | 15 | 11 / 8 | 27 / 19 | 27.61 M | 14 | 15 | 11.5 (100%) | 1 |
| C · 60 phút · 3–5 | Casual | 60 phút/ngày | 0 / 0 / 0 | 0 / 0 / 0 | 21 (33%) | 87 | 685 | 8 / 9 | 1000 / 1050 | 10.54 M | 10 | 13 | 11.5 (33%) | 1 |
| C · 60 phút · 3–5 | Guided | 5 phút/ngày | 0 / 37 / 276 | 0 / 158 / 1127 | 60 (100%) | 80 | 5 | 10 / 8 | 8 / 3 | 6.44 M | 16 | 15 | 31 (100%) | 1 |
| C · 60 phút · 3–5 | Guided | 3 × 10 phút/ngày | 30 / 185 / 975 | 131 / 762 / 3923 | 21.2 (100%) | 86 | 18 | 9 / 23 | 31 / 27 | 24.17 M | 17 | 15 | 13.2 (100%) | 1 |
| C · 60 phút · 3–5 | Guided | 60 phút/ngày | 28 / 138 / 672 | 125 / 566 / 2728 | 21.5 (100%) | 83 | 17 | 8 / 8 | 27 / 25 | 29.78 M | 17 | 15 | 13 (100%) | 1 |
| C · 60 phút · 3–5 | Researcher | 5 phút/ngày | 0 / 24 / 263 | 0 / 107 / 1050 | 71 (66%) | 91 | 5 | 11 / 8 | 12 / 5 | 27.03 M | 16 | 15 | 34 (66%) | 1 |
| C · 60 phút · 3–5 | Researcher | 3 × 10 phút/ngày | 29 / 181 / 969 | 134 / 755 / 3910 | 21.5 (100%) | 95 | 18 | 8 / 11 | 28 / 32 | 18.55 M | 16 | 15 | 12.2 (100%) | 1 |
| C · 60 phút · 3–5 | Researcher | 60 phút/ngày | 35 / 140 / 674 | 140 / 566 / 2684 | 17.7 (66%) | 94 | 6 | 8 / 22 | 11 / 6 | 16.43 M | 15 | 15 | 9.7 (66%) | 1 |

"Chuyến rừng / rạn" là số chuyến đã nhận theo tuyến sau 365 ngày; "Vàng 365" là số dư (dao động giữa các bảng do RNG giao dịch rẽ nhánh khác nhau; bất biến vàng được kiểm riêng bằng test, không có nguồn vàng mới).

## 3. Đánh giá

| Tiêu chí | Kết quả | Kết luận |
|---|---|---|
| **Bụi sao** | Lịch 3 × 10 phút: ~975 chuyến, 2 900 bụi sao (A/B) hoặc 3 900 (C) sau 1 năm; 5 phút/ngày: 230–280 chuyến, 720–1 130 bụi sao | Dư nhiều so với nhu cầu 60 dự án Thiên Giới (270, xem PR vật liệu Sanctuary). Cùng hiện tượng với gỗ / pha lê (ECONOMY_365): chỗ tiêu vật liệu bị giới hạn bởi giá vàng, không bởi vật liệu |
| **Thời gian chuyến** | A, B, C cho **cùng số chuyến** ở mọi lịch: mỗi phiên chơi người chơi nhận chuyến cũ và gửi chuyến mới, chuyến chạy khi offline; 30–60 phút đều ngắn hơn khoảng cách giữa hai phiên | Thời gian chỉ là chi phí thật với người chơi ở lại liên tục. Giữ 45 phút (đã duyệt ở U2.8) là hợp lý; 30 phút không làm nhanh hơn, 60 phút không làm chậm hơn với lịch chuẩn |
| **Số lượng** | C cho thêm ~33 % bụi sao, không đổi gì khác | Không cần: đã dư |
| **Mốc thưởng** | Trứng Aurorawing: ngày 18–22 (3 × 10 phút, 60 phút/ngày), ngày 60–71 (5 phút/ngày cho người theo hướng dẫn / nghiên cứu), ngày 107 (Casual 5 phút/ngày). Trao đúng 1 lần ở mọi seed | Mốc 15 đặt tốt: khoảng 1 tuần sau khi mở Thiên Giới cho người chơi đều đặn |
| **Sức khỏe** | Không bảng nào đổi điều kiện. Tiêu hao năng lượng vẫn TẮT nên đi liên tục không làm rồng yếu | Không rủi ro mới; nếu sau này bật tiêu hao năng lượng thì chuyến 45 phút cần ngưỡng hồi |
| **Chi phí cơ hội của slot** | Bật sân mây: chuyến rạn từ 680–1 000 xuống 5–34 / năm; pha lê / san hô còn giữ từ ~1 000 xuống 3–60; chuyến rừng gần như không đổi (77–95) | Một slot chung là chi phí cơ hội thật: người chơi chọn sân mây, bỏ rạn. Với vật liệu theo thế giới, người chơi phải quay lại rạn để có pha lê / san hô cho dự án Thủy Vực — đúng mục tiêu hệ sinh thái |
| **Hành trình** | Bật bảng: `Q_EXPEDITION_CLOUD` đạt được, nhiệm vụ 16 → 17 (Guided), 16/16 loài sở hữu được (kiểm toán) | Sửa F1, F2 của ADR 0207 |
| **Codex 16/16** | Người chơi mô phỏng có 15/16: loài còn thiếu là **Amberhorn** (Epic, chỉ lai được, nhóm T), không liên quan Thiên Giới. Test `Codex_16_of_16_is_reachable_…` đạt 16/16 bằng trứng Aurorawing + lai Amberhorn bằng lệnh thật | 16/16 đạt được sau khi bật tuyến |
| **Người chơi nhẹ** | Casual 60 phút/ngày chỉ mở Thiên Giới ở 1/3 seed (F11, ECONOMY_365) nên không có chuyến sân mây | Không do bảng; xem F11 |

## 4. Khuyến nghị

**Duyệt bảng A** (45 phút, 2–4 bụi sao, mốc 2 / 3 / 4 / 6). Lý do: giữ thời gian đã duyệt ở U2.8, cùng thang với hai tuyến kia, dư đủ cho dự án Thiên Giới; B và C không đổi nhịp chơi với lịch chuẩn. Nếu muốn giảm dư thừa vật liệu, nên xử lý chung cho cả ba tuyến (ADR riêng), không riêng sân mây.
