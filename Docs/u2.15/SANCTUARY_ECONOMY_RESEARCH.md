# U2.15b — Nghiên cứu kinh tế Sanctuary (chỉ nghiên cứu)

Issue #75. **Giá production, công thức vàng và nguồn thu nhập không đổi.** `SanctuaryCostCurve.Production` vẫn là `floor(20 000 × 1,25ⁿ)` (tổng 60 dự án 52,2 tỉ G). Ba đường cong ứng viên chỉ tồn tại trong thư mục test và chỉ có hiệu lực trong `SanctuaryCostCurve.Use(...)` của mô phỏng. Test `Production_curve_is_unchanged_and_candidates_only_apply_inside_a_scope` chặn việc vô tình đổi giá.

## 1. Kết luận ngắn

- Với giá hiện tại, **chỉ 1 trên 54 lần chạy đạt dự án 40 trong 730 ngày** (ở ngày 729), không lần nào đạt 60. Các lần chạy dừng ở khoảng 33–38 dự án vào ngày 730.
- Cả ba ứng viên đều đưa trung vị dự án 40 về khoảng 4–9 tháng (115–270 ngày) và dự án 60 về khoảng 18–20 tháng (543–592 ngày). 21–32 % lần chạy vẫn chưa xong 60 trong 730 ngày. Mọi lần chạy có 0 vi phạm sổ vàng / vật liệu.
- **Khuyến nghị: đường cong phân đoạn** (×1,25 cho dự án 1–10, ×1,15 cho 11–30, ×1,06 cho 31–60; tổng 222,6 M G).
  - Giữ nguyên 10 dự án đầu, phần đã được kiểm ở U2.14.
  - Nhịp giữa game đều nhất: dự án 20 khoảng 6 tuần, 30 khoảng 3 tháng, 40 khoảng 6–7 tháng.
  - Đây là đường cong có tỉ lệ lần chạy xong 60 trong 730 ngày cao nhất.
- Đây là **đề xuất nhịp mục tiêu để chủ dự án duyệt**, không phải quyết định. Xem mục 7 về rủi ro, đặc biệt là vàng dư sau khi xong 60 dự án và thu nhập offline chiếm 97 %.

## 2. Phương pháp

- Hành trình `AlphaJourney`: save mới, **chỉ lệnh chính thức**, kiểm sổ vàng / trần sản xuất / sổ vật liệu sau mỗi lệnh. Bảng Thiên Giới A đang bật (production từ U2.15a).
- **730 ngày** × 4 đường cong × 6 kiểu người chơi (Thư giãn, Nghiên cứu, Sưu tập, Lai giống, Theo hướng dẫn, Nhẹ nhàng) × 3 lịch chơi (5 phút/ngày, 3 × 10 phút/ngày, 60 phút/ngày) × 3 seed = **216 lần chạy**.
- Chụp số liệu ở các ngày 30 / 90 / 180 / 365 / 540 / 730.
- Ngày đạt một mốc là trung vị trên các seed, kèm khoảng min–max. `[k/n]` nghĩa là chỉ k trên n seed đạt mốc trong 730 ngày; các seed còn lại bị **cắt (censored)**. Khi đó ô ghi `≥` cộng ngày sớm nhất, không phải trung vị.
- Tái tạo:
  ```
  DRAVIO_U215_OUT=out DRAVIO_SIM_DAYS=730 dotnet test -c Release Assets/Dravio/Tests/Domain/DotNet~/Dravio.Domain.Tests.csproj --filter FullyQualifiedName~Write_u215_sanctuary_research_when_requested
  ```
  Biến lọc: `DRAVIO_SIM_POLICIES`, `DRAVIO_SIM_SCHEDULES`, `DRAVIO_U215_CURVES`, `DRAVIO_SIM_SEEDS`, `DRAVIO_SIM_SEED_FROM`, `DRAVIO_U215_SKY=A|off`, `DRAVIO_SIM_THREADS`. Dữ liệu thô nằm trong `Docs/u2.15/data/`.

## 3. Các đường cong

| Mã | Mô tả | Tổng 60 dự án |
|---|---|---|
| `x1.25` (production) | 20 000 × 1,25ⁿ | 52,2 tỉ G |
| `staged-1.25-1.15-1.06` | ×1,25 tới dự án 10, ×1,15 tới 30, ×1,06 tới 60 | 222,6 M G |
| `soft-1.13` | 20 000 × 1,13ⁿ | 235,2 M G |
| `hybrid-1.25-cap250k` | như production, nhưng mỗi dự án tăng tối đa 250 000 G so với dự án trước | 266,2 M G |

Giá từng dự án và cộng dồn:

| Đường cong | Dự án 1 | Dự án 5 | Dự án 10 | Dự án 20 | Dự án 30 | Dự án 40 | Dự án 60 |
|---|---|---|---|---|---|---|---|
| Hiện tại ×1,25 (giá) | 20,0 k | 48,8 k | 149,0 k | 1,39 M | 12,92 M | 120,37 M | 10,44 tỉ |
| Hiện tại ×1,25 (cộng dồn) | 20,0 k | 164,1 k | 665,1 k | 6,86 M | 64,54 M | 601,77 M | 52,20 tỉ |
| Phân đoạn (giá) | 20,0 k | 48,8 k | 149,0 k | 602,8 k | 2,44 M | 4,37 M | 14,01 M |
| Phân đoạn (cộng dồn) | 20,0 k | 164,1 k | 665,1 k | 4,14 M | 18,22 M | 52,29 M | 222,60 M |
| Lũy thừa mềm ×1,13 (giá) | 20,0 k | 32,6 k | 60,1 k | 203,9 k | 692,3 k | 2,35 M | 27,08 M |
| Lũy thừa mềm ×1,13 (cộng dồn) | 20,0 k | 129,6 k | 368,4 k | 1,62 M | 5,86 M | 20,27 M | 235,24 M |
| Kết hợp trần 250k (giá) | 20,0 k | 48,8 k | 149,0 k | 1,36 M | 3,86 M | 6,36 M | 11,36 M |
| Kết hợp trần 250k (cộng dồn) | 20,0 k | 164,1 k | 665,1 k | 6,83 M | 34,18 M | 86,54 M | 266,24 M |


## 4. Ngày đạt dự án 1 / 5 / 10 / 20 / 30 / 40 / 60

Tổng hợp 54 lần chạy mỗi đường cong (6 kiểu × 3 lịch × 3 seed). Ô ngày là trung vị của các lần chạy **đã đạt** mốc; phần trăm trong ngoặc là tỉ lệ lần chạy đạt mốc khi chưa phải 100 %.

| Đường cong | Dự án 10 | 20 | 30 | 40 | 60 | Xong 60 trong 730 ngày | Xong 40 | Vi phạm sổ |
|---|---|---|---|---|---|---|---|---|
| Hiện tại ×1,25 | 20 | 55 | 208 | 729 (1 %) | — (0 %) | 0 % | 1 % | 0 |
| **Phân đoạn** | 20 | 42 | 94 | 191 | 543 (79 %) | **79 %** | 100 % | 0 |
| Lũy thừa mềm ×1,13 | 15 | 30 | 58 | 115 | 548 (68 %) | 68 % | 100 % | 0 |
| Kết hợp trần 250k | 20 | 54 | 138 | 270 | 592 (70 %) | 70 % | 100 % | 0 |

Đọc nhanh:
- **Giá hiện tại** dừng ở khoảng 33–38 dự án sau 2 năm. Chỉ 1 trên 54 lần chạy đạt dự án 40, ở ngày 729.
- **Lũy thừa mềm** nhanh nhất ở giữa game (dự án 40 khoảng 4 tháng). Nhưng nó làm dự án 2–10 rẻ hơn hiện tại, tức là đổi cả phần đầu game đã kiểm. Đuôi của nó lại dốc nhất (dự án 60 giá 27 M), nên chặng 40 → 60 mất khoảng 430 ngày.
- **Kết hợp trần** giữ nguyên tới khoảng dự án 19, rồi tăng tuyến tính. Giữa game chậm nhất (dự án 40 khoảng 9 tháng), đuôi đều nhất.
- **Phân đoạn** giữ nguyên 10 dự án đầu, giữa game đều (khoảng 1,5 / 3 / 6 tháng cho dự án 20 / 30 / 40) và có tỉ lệ xong 60 cao nhất.
- Lịch chơi **gần như không đổi nhịp**: 5 phút/ngày và 60 phút/ngày cho cùng cỡ ngày đạt mốc, vì 97 % vàng đến từ offline (mục 6).

Chi tiết theo kiểu × lịch (trung vị (min–max), `[k/n]` = chỉ k / n seed đạt trong 730 ngày):

#### Hiện tại ×1,25

| Kiểu | Lịch | Dự án 1 | Dự án 5 | Dự án 10 | Dự án 20 | Dự án 30 | Dự án 40 | Dự án 60 | Dự án ngày 730 |
|---|---|---|---|---|---|---|---|---|---|
| Thư giãn | 5 phút/ngày | 8,0 (8,0–11) | 13 (12–15) | 20 (18–22) | 62 (59–70) | 272 (187–281) | — (0/3) | — (0/3) | 37 |
| Thư giãn | 3 × 10 phút/ngày | 4,5 (2,5–9,5) | 18 (13–21) | 24 (19–30) | 64 (55–64) | 252 (205–255) | — (0/3) | — (0/3) | 36 |
| Thư giãn | 60 phút/ngày | 3,5 (3,5–7,5) | 15 (14–21) | 29 (18–31) | 63 (47–74) | 196 (192–210) | — (0/3) | — (0/3) | 37 |
| Nghiên cứu | 5 phút/ngày | 6,0 (6,0–9,0) | 11 (10–13) | 22 (21–24) | 48 (45–60) | 188 (157–195) | — (0/3) | — (0/3) | 38 |
| Nghiên cứu | 3 × 10 phút/ngày | 4,2 (4,2–5,5) | 9,0 (9,0–10) | 16 (16–21) | 49 (42–53) | 182 (156–187) | — (0/3) | — (0/3) | 38 |
| Nghiên cứu | 60 phút/ngày | 4,5 (3,5–13) | 15 (9,5–29) | 21 (16–40) | 60 (54–79) | 207 (197–230) | — (0/3) | — (0/3) | 37 |
| Sưu tập | 5 phút/ngày | 8,0 (5,0–10) | 11 (9,0–14) | 19 (17–22) | 58 (49–70) | 243 (172–270) | — (0/3) | — (0/3) | 35 |
| Sưu tập | 3 × 10 phút/ngày | 6,0 (5,0–8,0) | 15 (12–16) | 24 (18–27) | 55 (46–57) | 207 (169–209) | — (0/3) | — (0/3) | 36 |
| Sưu tập | 60 phút/ngày | 7,0 (4,0–8,5) | 14 (10–20) | 22 (19–26) | 50 (46–53) | 233 (187–244) | — (0/3) | — (0/3) | 35 |
| Lai giống | 5 phút/ngày | 8,0 (6,0–13) | 11 (11–16) | 20 (18–21) | 59 (59–71) | 250 (200–263) | — (0/3) | — (0/3) | 36 |
| Lai giống | 3 × 10 phút/ngày | 2,5 (2,5–7,5) | 8,5 (6,5–12) | 16 (14–20) | 51 (41–63) | 238 (180–312) | — (0/3) | — (0/3) | 35 |
| Lai giống | 60 phút/ngày | 2,5 (1,5–2,5) | 7,5 (6,0–13) | 23 (10–28) | 65 (46–73) | 368 (258–407) | — (0/3) | — (0/3) | 33 |
| Theo hướng dẫn | 5 phút/ngày | 8,0 (7,0–8,0) | 12 (10–19) | 22 (21–28) | 57 (47–94) | 269 (198–414) | — (0/3) | — (0/3) | 36 |
| Theo hướng dẫn | 3 × 10 phút/ngày | 5,5 (3,2–7,0) | 9,0 (7,2–11) | 16 (15–21) | 53 (48–55) | 226 (202–228) | — (0/3) | — (0/3) | 36 |
| Theo hướng dẫn | 60 phút/ngày | 5,5 (3,5–5,5) | 8,5 (7,5–9,5) | 14 (13–14) | 53 (37–55) | 241 (176–245) | — (0/3) | — (0/3) | 36 |
| Nhẹ nhàng | 5 phút/ngày | 10 (8,0–11) | 12 (12–14) | 19 (17–23) | 48 (44–60) | 215 (205–242) | — (0/3) | — (0/3) | 37 |
| Nhẹ nhàng | 3 × 10 phút/ngày | 5,5 (3,5–7,5) | 11 (10–16) | 19 (17–22) | 55 (37–57) | 173 (143–195) | ≥729 [1/3] | — (0/3) | 38 |
| Nhẹ nhàng | 60 phút/ngày | 6,5 (4,5–8,5) | 11 (10–12) | 17 (16–19) | 48 (48–54) | 204 (163–219) | — (0/3) | — (0/3) | 38 |

#### Phân đoạn

| Kiểu | Lịch | Dự án 1 | Dự án 5 | Dự án 10 | Dự án 20 | Dự án 30 | Dự án 40 | Dự án 60 | Dự án ngày 730 |
|---|---|---|---|---|---|---|---|---|---|
| Thư giãn | 5 phút/ngày | 8,0 (8,0–11) | 13 (12–15) | 20 (18–22) | 51 (46–60) | 118 (113–135) | 244 (224–254) | ≥625 (625–663) [2/3] | 60 |
| Thư giãn | 3 × 10 phút/ngày | 4,5 (2,5–9,5) | 18 (13–21) | 24 (19–30) | 43 (41–43) | 95 (86–97) | 189 (171–214) | 549 (549–649) | 60 |
| Thư giãn | 60 phút/ngày | 3,5 (3,5–7,5) | 15 (14–21) | 29 (18–31) | 57 (38–67) | 109 (104–148) | 201 (191–260) | 543 (475–601) | 60 |
| Nghiên cứu | 5 phút/ngày | 6,0 (6,0–9,0) | 11 (10–13) | 22 (21–24) | 43 (39–45) | 77 (75–112) | 150 (137–177) | 427 (385–430) | 60 |
| Nghiên cứu | 3 × 10 phút/ngày | 4,2 (4,2–5,5) | 9,0 (9,0–10) | 16 (16–21) | 37 (34–38) | 81 (66–113) | 163 (133–269) | 457 (364–561) | 60 |
| Nghiên cứu | 60 phút/ngày | 4,5 (3,5–13) | 15 (9,5–29) | 21 (16–40) | 41 (40–66) | 82 (77–122) | 166 (139–220) | 426 (424–553) | 60 |
| Sưu tập | 5 phút/ngày | 8,0 (5,0–10) | 11 (9,0–14) | 19 (17–22) | 48 (37–56) | 108 (90–122) | 204 (190–213) | 630 (580–636) | 60 |
| Sưu tập | 3 × 10 phút/ngày | 6,0 (5,0–8,0) | 15 (12–16) | 24 (18–27) | 49 (37–52) | 94 (76–99) | 202 (164–212) | ≥606 [1/3] | 59 |
| Sưu tập | 60 phút/ngày | 7,0 (4,0–8,5) | 14 (10–20) | 22 (19–26) | 40 (36–45) | 80 (60–94) | 175 (117–209) | ≥402 (402–650) [2/3] | 60 |
| Lai giống | 5 phút/ngày | 8,0 (6,0–13) | 11 (11–16) | 20 (18–21) | 52 (44–55) | 107 (92–110) | 196 (180–198) | 581 (579–603) | 60 |
| Lai giống | 3 × 10 phút/ngày | 2,5 (2,5–7,5) | 8,5 (6,5–12) | 16 (14–20) | 37 (30–56) | 94 (66–141) | 218 (142–343) | ≥525 [1/3] | 57 |
| Lai giống | 60 phút/ngày | 2,5 (1,5–2,5) | 7,5 (6,0–13) | 23 (10–28) | 47 (37–54) | 110 (92–137) | 256 (223–329) | — (0/3) | 55 |
| Theo hướng dẫn | 5 phút/ngày | 8,0 (7,0–8,0) | 12 (10–19) | 22 (21–28) | 47 (40–61) | 93 (76–119) | 198 (165–232) | 599 (568–618) | 60 |
| Theo hướng dẫn | 3 × 10 phút/ngày | 5,5 (3,2–7,0) | 9,0 (7,2–11) | 16 (15–21) | 41 (33–41) | 78 (75–103) | 169 (151–228) | 520 (481–704) | 60 |
| Theo hướng dẫn | 60 phút/ngày | 5,5 (3,5–5,5) | 8,5 (7,5–9,5) | 14 (13–14) | 35 (34–39) | 104 (87–111) | 232 (195–239) | ≥569 [1/3] | 58 |
| Nhẹ nhàng | 5 phút/ngày | 10 (8,0–11) | 12 (12–14) | 19 (17–23) | 42 (39–46) | 91 (79–92) | 164 (159–175) | 461 (398–462) | 60 |
| Nhẹ nhàng | 3 × 10 phút/ngày | 5,5 (3,5–7,5) | 11 (10–16) | 19 (17–22) | 46 (31–47) | 95 (63–106) | 180 (127–184) | 458 (367–471) | 60 |
| Nhẹ nhàng | 60 phút/ngày | 6,5 (4,5–8,5) | 11 (10–12) | 17 (16–19) | 39 (38–39) | 80 (77–86) | 163 (143–171) | 421 (363–534) | 60 |

#### Lũy thừa mềm ×1,13

| Kiểu | Lịch | Dự án 1 | Dự án 5 | Dự án 10 | Dự án 20 | Dự án 30 | Dự án 40 | Dự án 60 | Dự án ngày 730 |
|---|---|---|---|---|---|---|---|---|---|
| Thư giãn | 5 phút/ngày | 8,0 (8,0–11) | 12 (11–14) | 16 (15–18) | 35 (31–40) | 70 (66–70) | 134 (122–138) | ≥612 (612–627) [2/3] | 60 |
| Thư giãn | 3 × 10 phút/ngày | 4,5 (2,5–9,5) | 15 (11–21) | 20 (15–27) | 28 (24–44) | 50 (48–78) | 116 (102–154) | 625 (611–710) | 60 |
| Thư giãn | 60 phút/ngày | 3,5 (3,5–7,5) | 14 (8,5–18) | 15 (13–21) | 27 (26–30) | 47 (45–48) | 91 (85–91) | 581 (558–605) | 60 |
| Nghiên cứu | 5 phút/ngày | 6,0 (6,0–9,0) | 10 (9,0–13) | 21 (19–22) | 44 (43–47) | 74 (71–74) | 122 (122–124) | 467 (406–503) | 60 |
| Nghiên cứu | 3 × 10 phút/ngày | 4,2 (4,2–5,5) | 9,5 (8,2–11) | 17 (13–17) | 25 (24–30) | 42 (37–74) | 81 (69–203) | 504 (354–550) | 60 |
| Nghiên cứu | 60 phút/ngày | 4,5 (3,5–13) | 11 (8,5–15) | 15 (14–18) | 29 (26–31) | 61 (41–64) | 115 (69–116) | 478 (343–521) | 60 |
| Sưu tập | 5 phút/ngày | 8,0 (5,0–10) | 10 (8,0–14) | 14 (13–17) | 34 (32–34) | 68 (65–75) | 126 (118–131) | ≥644 (644–671) [2/3] | 60 |
| Sưu tập | 3 × 10 phút/ngày | 6,0 (5,0–8,0) | 11 (10–12) | 16 (15–20) | 30 (28–38) | 51 (50–59) | 97 (97–112) | — (0/3) | 59 |
| Sưu tập | 60 phút/ngày | 7,0 (4,0–8,5) | 11 (8,0–12) | 14 (14–19) | 30 (28–33) | 49 (48–53) | 98 (94–115) | — (0/3) | 58 |
| Lai giống | 5 phút/ngày | 8,0 (6,0–13) | 11 (10–15) | 16 (14–18) | 35 (32–37) | 72 (65–81) | 125 (120–136) | ≥497 (497–675) [2/3] | 60 |
| Lai giống | 3 × 10 phút/ngày | 2,5 (2,5–7,5) | 7,5 (6,0–13) | 15 (9,0–18) | 31 (19–31) | 58 (36–60) | 119 (73–129) | ≥577 [1/3] | 56 |
| Lai giống | 60 phút/ngày | 2,5 (1,5–2,5) | 6,5 (5,5–11) | 14 (8,0–19) | 29 (21–30) | 49 (43–54) | 101 (91–122) | — (0/3) | 58 |
| Theo hướng dẫn | 5 phút/ngày | 8,0 (7,0–8,0) | 12 (10–18) | 21 (14–23) | 47 (42–48) | 78 (78–87) | 123 (120–135) | ≥640 (640–648) [2/3] | 60 |
| Theo hướng dẫn | 3 × 10 phút/ngày | 5,5 (3,2–7,0) | 8,2 (6,5–8,5) | 11 (10–13) | 21 (21–22) | 44 (42–44) | 91 (79–95) | 493 (478–646) | 60 |
| Theo hướng dẫn | 60 phút/ngày | 5,5 (3,5–5,5) | 8,0 (7,0–9,0) | 10 (9,5–11) | 27 (25–27) | 52 (51–58) | 112 (98–134) | ≥570 [1/3] | 59 |
| Nhẹ nhàng | 5 phút/ngày | 10 (8,0–11) | 11 (11–14) | 19 (17–21) | 42 (36–44) | 71 (65–76) | 123 (116–129) | 480 (362–509) | 60 |
| Nhẹ nhàng | 3 × 10 phút/ngày | 5,5 (3,5–7,5) | 14 (9,5–15) | 19 (14–22) | 31 (23–45) | 56 (47–61) | 102 (96–110) | 492 (398–609) | 60 |
| Nhẹ nhàng | 60 phút/ngày | 6,5 (4,5–8,5) | 10 (9,5–10) | 13 (13–13) | 23 (21–25) | 41 (35–58) | 82 (66–112) | 466 (378–548) | 60 |

#### Kết hợp trần 250k

| Kiểu | Lịch | Dự án 1 | Dự án 5 | Dự án 10 | Dự án 20 | Dự án 30 | Dự án 40 | Dự án 60 | Dự án ngày 730 |
|---|---|---|---|---|---|---|---|---|---|
| Thư giãn | 5 phút/ngày | 8,0 (8,0–11) | 13 (12–15) | 20 (18–22) | 62 (59–70) | 154 (115–196) | 261 (207–356) | ≥512 (512–563) [2/3] | 60 |
| Thư giãn | 3 × 10 phút/ngày | 4,5 (2,5–9,5) | 18 (13–21) | 24 (19–30) | 63 (55–64) | 154 (139–177) | 285 (277–347) | ≥631 (631–635) [2/3] | 60 |
| Thư giãn | 60 phút/ngày | 3,5 (3,5–7,5) | 15 (14–21) | 29 (18–31) | 63 (47–74) | 135 (121–168) | 259 (240–273) | 619 (603–630) | 60 |
| Nghiên cứu | 5 phút/ngày | 6,0 (6,0–9,0) | 11 (10–13) | 22 (21–24) | 48 (45–59) | 116 (99–145) | 226 (186–234) | 444 (422–487) | 60 |
| Nghiên cứu | 3 × 10 phút/ngày | 4,2 (4,2–5,5) | 9,0 (9,0–10) | 16 (16–21) | 49 (42–53) | 120 (101–121) | 219 (177–219) | 449 (386–464) | 60 |
| Nghiên cứu | 60 phút/ngày | 4,5 (3,5–13) | 15 (9,5–29) | 21 (16–40) | 60 (54–78) | 157 (133–170) | 277 (259–294) | 610 (609–642) | 60 |
| Sưu tập | 5 phút/ngày | 8,0 (5,0–10) | 11 (9,0–14) | 19 (17–22) | 58 (49–70) | 150 (101–172) | 279 (184–322) | ≥423 (423–713) [2/3] | 60 |
| Sưu tập | 3 × 10 phút/ngày | 6,0 (5,0–8,0) | 15 (12–16) | 24 (18–27) | 55 (46–56) | 126 (103–127) | 260 (211–262) | 720 (581–722) | 60 |
| Sưu tập | 60 phút/ngày | 7,0 (4,0–8,5) | 14 (10–20) | 22 (19–26) | 50 (46–53) | 135 (111–142) | 296 (235–310) | ≥660 [1/3] | 56 |
| Lai giống | 5 phút/ngày | 8,0 (6,0–13) | 11 (11–16) | 20 (18–21) | 59 (59–71) | 164 (134–184) | 330 (225–359) | ≥524 [1/3] | 55 |
| Lai giống | 3 × 10 phút/ngày | 2,5 (2,5–7,5) | 8,5 (6,5–12) | 16 (14–20) | 51 (40–62) | 138 (105–178) | 302 (226–397) | ≥638 [1/3] | 56 |
| Lai giống | 60 phút/ngày | 2,5 (1,5–2,5) | 7,5 (6,0–13) | 23 (10–28) | 65 (46–73) | 205 (144–228) | 472 (330–522) | — (0/3) | 46 |
| Theo hướng dẫn | 5 phút/ngày | 8,0 (7,0–8,0) | 12 (10–19) | 22 (21–28) | 57 (47–94) | 155 (117–257) | 287 (243–512) | ≥563 (563–652) [2/3] | 60 |
| Theo hướng dẫn | 3 × 10 phút/ngày | 5,5 (3,2–7,0) | 9,0 (7,2–11) | 16 (15–21) | 53 (48–54) | 137 (134–157) | 284 (247–336) | ≥606 (606–617) [2/3] | 60 |
| Theo hướng dẫn | 60 phút/ngày | 5,5 (3,5–5,5) | 8,5 (7,5–9,5) | 14 (13–14) | 53 (37–55) | 169 (90–174) | 348 (175–351) | ≥396 [1/3] | 55 |
| Nhẹ nhàng | 5 phút/ngày | 10 (8,0–11) | 12 (12–14) | 19 (17–23) | 48 (44–60) | 129 (112–154) | 270 (206–290) | 569 (521–687) | 60 |
| Nhẹ nhàng | 3 × 10 phút/ngày | 5,5 (3,5–7,5) | 11 (10–16) | 19 (17–22) | 54 (37–57) | 115 (91–122) | 221 (175–225) | 506 (407–509) | 60 |
| Nhẹ nhàng | 60 phút/ngày | 6,5 (4,5–8,5) | 11 (10–12) | 17 (16–19) | 48 (48–54) | 142 (107–155) | 271 (197–302) | 643 (471–688) | 60 |


## 5. Số liệu chi tiết

### Ảnh hưởng tới các khoản chi khác ở ngày 365 (trung vị 3 seed; Δ so với ×1,25)

| Kiểu | Lịch | Đường cong | Dự án | Gói mua | Lai | Vàng giữ |
|---|---|---|---|---|---|---|
| Thư giãn | 5 phút/ngày | Hiện tại ×1,25 | 32 | 365 | 0 | 10,7 M |
| Thư giãn | 5 phút/ngày | Phân đoạn | 47 (+15) | 365 (+0) | 0 (+0) | 3,8 M |
| Thư giãn | 5 phút/ngày | Lũy thừa mềm ×1,13 | 52 (+20) | 365 (+0) | 0 (+0) | 9,5 M |
| Thư giãn | 5 phút/ngày | Kết hợp trần 250k | 48 (+16) | 365 (+0) | 0 (+0) | 5,5 M |
| Thư giãn | 3 × 10 phút/ngày | Hiện tại ×1,25 | 32 | 366 | 0 | 12,9 M |
| Thư giãn | 3 × 10 phút/ngày | Phân đoạn | 51 (+19) | 366 (+0) | 0 (+0) | 5,4 M |
| Thư giãn | 3 × 10 phút/ngày | Lũy thừa mềm ×1,13 | 53 (+21) | 366 (+0) | 0 (+0) | 9,2 M |
| Thư giãn | 3 × 10 phút/ngày | Kết hợp trần 250k | 44 (+12) | 366 (+0) | 0 (+0) | 5,4 M |
| Thư giãn | 60 phút/ngày | Hiện tại ×1,25 | 33 | 366 | 0 | 30,8 M |
| Thư giãn | 60 phút/ngày | Phân đoạn | 51 (+18) | 366 (+0) | 0 (+0) | 7,0 M |
| Thư giãn | 60 phút/ngày | Lũy thừa mềm ×1,13 | 55 (+22) | 366 (+0) | 0 (+0) | 11,9 M |
| Thư giãn | 60 phút/ngày | Kết hợp trần 250k | 46 (+13) | 366 (+0) | 0 (+0) | 7,4 M |
| Nghiên cứu | 5 phút/ngày | Hiện tại ×1,25 | 34 | 365 | 0 | 28,1 M |
| Nghiên cứu | 5 phút/ngày | Phân đoạn | 56 (+22) | 365 (+0) | 0 (+0) | 10,2 M |
| Nghiên cứu | 5 phút/ngày | Lũy thừa mềm ×1,13 | 57 (+23) | 365 (+0) | 0 (+0) | 10,7 M |
| Nghiên cứu | 5 phút/ngày | Kết hợp trần 250k | 53 (+19) | 365 (+0) | 0 (+0) | 8,2 M |
| Nghiên cứu | 3 × 10 phút/ngày | Hiện tại ×1,25 | 34 | 366 | 0 | 32,7 M |
| Nghiên cứu | 3 × 10 phút/ngày | Phân đoạn | 55 (+21) | 366 (+0) | 0 (+0) | 3,4 M |
| Nghiên cứu | 3 × 10 phút/ngày | Lũy thừa mềm ×1,13 | 56 (+22) | 366 (+0) | 0 (+0) | 7,3 M |
| Nghiên cứu | 3 × 10 phút/ngày | Kết hợp trần 250k | 52 (+18) | 366 (+0) | 0 (+0) | 6,3 M |
| Nghiên cứu | 60 phút/ngày | Hiện tại ×1,25 | 33 | 366 | 0 | 27,3 M |
| Nghiên cứu | 60 phút/ngày | Phân đoạn | 57 (+24) | 366 (+0) | 0 (+0) | 3,8 M |
| Nghiên cứu | 60 phút/ngày | Lũy thừa mềm ×1,13 | 56 (+23) | 366 (+0) | 0 (+0) | 18,9 M |
| Nghiên cứu | 60 phút/ngày | Kết hợp trần 250k | 45 (+12) | 366 (+0) | 0 (+0) | 8,5 M |
| Sưu tập | 5 phút/ngày | Hiện tại ×1,25 | 32 | 178 | 321 | 21,0 M |
| Sưu tập | 5 phút/ngày | Phân đoạn | 50 (+18) | 170 (-8) | 329 (+8) | 4,2 M |
| Sưu tập | 5 phút/ngày | Lũy thừa mềm ×1,13 | 54 (+22) | 175 (-3) | 324 (+3) | 8,8 M |
| Sưu tập | 5 phút/ngày | Kết hợp trần 250k | 44 (+12) | 175 (-3) | 324 (+3) | 9,3 M |
| Sưu tập | 3 × 10 phút/ngày | Hiện tại ×1,25 | 32 | 134 | 365 | 28,1 M |
| Sưu tập | 3 × 10 phút/ngày | Phân đoạn | 48 (+16) | 132 (-2) | 367 (+2) | 7,1 M |
| Sưu tập | 3 × 10 phút/ngày | Lũy thừa mềm ×1,13 | 53 (+21) | 137 (+3) | 362 (-3) | 5,5 M |
| Sưu tập | 3 × 10 phút/ngày | Kết hợp trần 250k | 45 (+13) | 134 (+0) | 365 (+0) | 6,9 M |
| Sưu tập | 60 phút/ngày | Hiện tại ×1,25 | 32 | 78 | 421 | 16,9 M |
| Sưu tập | 60 phút/ngày | Phân đoạn | 50 (+18) | 78 (+0) | 421 (+0) | 7,5 M |
| Sưu tập | 60 phút/ngày | Lũy thừa mềm ×1,13 | 52 (+20) | 73 (-5) | 426 (+5) | 8,9 M |
| Sưu tập | 60 phút/ngày | Kết hợp trần 250k | 43 (+11) | 78 (+0) | 421 (+0) | 5,0 M |
| Lai giống | 5 phút/ngày | Hiện tại ×1,25 | 32 | 172 | 327 | 19,2 M |
| Lai giống | 5 phút/ngày | Phân đoạn | 51 (+19) | 172 (+0) | 327 (+0) | 2,8 M |
| Lai giống | 5 phút/ngày | Lũy thừa mềm ×1,13 | 54 (+22) | 176 (+4) | 323 (-4) | 5,1 M |
| Lai giống | 5 phút/ngày | Kết hợp trần 250k | 41 (+9) | 173 (+1) | 326 (-1) | 5,8 M |
| Lai giống | 3 × 10 phút/ngày | Hiện tại ×1,25 | 32 | 51 | 448 | 15,5 M |
| Lai giống | 3 × 10 phút/ngày | Phân đoạn | 47 (+15) | 52 (+1) | 447 (-1) | 3,7 M |
| Lai giống | 3 × 10 phút/ngày | Lũy thừa mềm ×1,13 | 50 (+18) | 51 (+0) | 448 (+0) | 9,6 M |
| Lai giống | 3 × 10 phút/ngày | Kết hợp trần 250k | 42 (+10) | 51 (+0) | 448 (+0) | 5,9 M |
| Lai giống | 60 phút/ngày | Hiện tại ×1,25 | 29 | 41 | 458 | 15,0 M |
| Lai giống | 60 phút/ngày | Phân đoạn | 44 (+15) | 40 (-1) | 459 (+1) | 6,2 M |
| Lai giống | 60 phút/ngày | Lũy thừa mềm ×1,13 | 52 (+23) | 38 (-3) | 461 (+3) | 4,3 M |
| Lai giống | 60 phút/ngày | Kết hợp trần 250k | 36 (+7) | 41 (+0) | 458 (+0) | 4,0 M |
| Theo hướng dẫn | 5 phút/ngày | Hiện tại ×1,25 | 32 | 365 | 1 | 12,5 M |
| Theo hướng dẫn | 5 phút/ngày | Phân đoạn | 50 (+18) | 365 (+0) | 1 (+0) | 7,4 M |
| Theo hướng dẫn | 5 phút/ngày | Lũy thừa mềm ×1,13 | 53 (+21) | 365 (+0) | 1 (+0) | 6,9 M |
| Theo hướng dẫn | 5 phút/ngày | Kết hợp trần 250k | 44 (+12) | 365 (+0) | 1 (+0) | 5,1 M |
| Theo hướng dẫn | 3 × 10 phút/ngày | Hiện tại ×1,25 | 33 | 371 | 1 | 27,2 M |
| Theo hướng dẫn | 3 × 10 phút/ngày | Phân đoạn | 53 (+20) | 371 (+0) | 1 (+0) | 7,7 M |
| Theo hướng dẫn | 3 × 10 phút/ngày | Lũy thừa mềm ×1,13 | 56 (+23) | 371 (+0) | 1 (+0) | 8,6 M |
| Theo hướng dẫn | 3 × 10 phút/ngày | Kết hợp trần 250k | 44 (+11) | 371 (+0) | 1 (+0) | 5,1 M |
| Theo hướng dẫn | 60 phút/ngày | Hiện tại ×1,25 | 32 | 373 | 1 | 21,2 M |
| Theo hướng dẫn | 60 phút/ngày | Phân đoạn | 46 (+14) | 373 (+0) | 1 (+0) | 6,1 M |
| Theo hướng dẫn | 60 phút/ngày | Lũy thừa mềm ×1,13 | 52 (+20) | 373 (+0) | 1 (+0) | 7,3 M |
| Theo hướng dẫn | 60 phút/ngày | Kết hợp trần 250k | 40 (+8) | 373 (+0) | 1 (+0) | 6,9 M |
| Nhẹ nhàng | 5 phút/ngày | Hiện tại ×1,25 | 33 | 365 | 0 | 17,1 M |
| Nhẹ nhàng | 5 phút/ngày | Phân đoạn | 55 (+22) | 365 (+0) | 0 (+0) | 8,2 M |
| Nhẹ nhàng | 5 phút/ngày | Lũy thừa mềm ×1,13 | 57 (+24) | 365 (+0) | 0 (+0) | 8,1 M |
| Nhẹ nhàng | 5 phút/ngày | Kết hợp trần 250k | 45 (+12) | 365 (+0) | 0 (+0) | 6,0 M |
| Nhẹ nhàng | 3 × 10 phút/ngày | Hiện tại ×1,25 | 34 | 366 | 0 | 28,9 M |
| Nhẹ nhàng | 3 × 10 phút/ngày | Phân đoạn | 55 (+21) | 366 (+0) | 0 (+0) | 7,8 M |
| Nhẹ nhàng | 3 × 10 phút/ngày | Lũy thừa mềm ×1,13 | 56 (+22) | 366 (+0) | 0 (+0) | 15,8 M |
| Nhẹ nhàng | 3 × 10 phút/ngày | Kết hợp trần 250k | 51 (+17) | 366 (+0) | 0 (+0) | 8,8 M |
| Nhẹ nhàng | 60 phút/ngày | Hiện tại ×1,25 | 33 | 366 | 0 | 30,0 M |
| Nhẹ nhàng | 60 phút/ngày | Phân đoạn | 56 (+23) | 366 (+0) | 0 (+0) | 5,6 M |
| Nhẹ nhàng | 60 phút/ngày | Lũy thừa mềm ×1,13 | 57 (+24) | 366 (+0) | 0 (+0) | 15,7 M |
| Nhẹ nhàng | 60 phút/ngày | Kết hợp trần 250k | 45 (+12) | 366 (+0) | 0 (+0) | 5,7 M |

### Ảnh hưởng tới các khoản chi khác ở ngày 730 (trung vị 3 seed; Δ so với ×1,25)

| Kiểu | Lịch | Đường cong | Dự án | Gói mua | Lai | Vàng giữ |
|---|---|---|---|---|---|---|
| Thư giãn | 5 phút/ngày | Hiện tại ×1,25 | 37 | 499 | 0 | 24,4 M |
| Thư giãn | 5 phút/ngày | Phân đoạn | 60 (+23) | 499 (+0) | 0 (+0) | 34,9 M |
| Thư giãn | 5 phút/ngày | Lũy thừa mềm ×1,13 | 60 (+23) | 499 (+0) | 0 (+0) | 61,2 M |
| Thư giãn | 5 phút/ngày | Kết hợp trần 250k | 60 (+23) | 499 (+0) | 0 (+0) | 108,3 M |
| Thư giãn | 3 × 10 phút/ngày | Hiện tại ×1,25 | 36 | 499 | 0 | 68,9 M |
| Thư giãn | 3 × 10 phút/ngày | Phân đoạn | 60 (+24) | 499 (+0) | 0 (+0) | 95,8 M |
| Thư giãn | 3 × 10 phút/ngày | Lũy thừa mềm ×1,13 | 60 (+24) | 499 (+0) | 0 (+0) | 63,0 M |
| Thư giãn | 3 × 10 phút/ngày | Kết hợp trần 250k | 60 (+24) | 499 (+0) | 0 (+0) | 53,4 M |
| Thư giãn | 60 phút/ngày | Hiện tại ×1,25 | 37 | 499 | 0 | 70,4 M |
| Thư giãn | 60 phút/ngày | Phân đoạn | 60 (+23) | 499 (+0) | 0 (+0) | 109,3 M |
| Thư giãn | 60 phút/ngày | Lũy thừa mềm ×1,13 | 60 (+23) | 499 (+0) | 0 (+0) | 79,5 M |
| Thư giãn | 60 phút/ngày | Kết hợp trần 250k | 60 (+23) | 499 (+0) | 0 (+0) | 57,3 M |
| Nghiên cứu | 5 phút/ngày | Hiện tại ×1,25 | 38 | 496 | 0 | 100,2 M |
| Nghiên cứu | 5 phút/ngày | Phân đoạn | 60 (+22) | 496 (+0) | 0 (+0) | 271,9 M |
| Nghiên cứu | 5 phút/ngày | Lũy thừa mềm ×1,13 | 60 (+22) | 496 (+0) | 0 (+0) | 187,9 M |
| Nghiên cứu | 5 phút/ngày | Kết hợp trần 250k | 60 (+22) | 496 (+0) | 0 (+0) | 271,8 M |
| Nghiên cứu | 3 × 10 phút/ngày | Hiện tại ×1,25 | 38 | 496 | 0 | 78,8 M |
| Nghiên cứu | 3 × 10 phút/ngày | Phân đoạn | 60 (+22) | 496 (+0) | 0 (+0) | 202,5 M |
| Nghiên cứu | 3 × 10 phút/ngày | Lũy thừa mềm ×1,13 | 60 (+22) | 496 (+0) | 0 (+0) | 189,3 M |
| Nghiên cứu | 3 × 10 phút/ngày | Kết hợp trần 250k | 60 (+22) | 496 (+0) | 0 (+0) | 293,2 M |
| Nghiên cứu | 60 phút/ngày | Hiện tại ×1,25 | 37 | 496 | 0 | 60,5 M |
| Nghiên cứu | 60 phút/ngày | Phân đoạn | 60 (+23) | 496 (+0) | 0 (+0) | 171,0 M |
| Nghiên cứu | 60 phút/ngày | Lũy thừa mềm ×1,13 | 60 (+23) | 496 (+0) | 0 (+0) | 212,6 M |
| Nghiên cứu | 60 phút/ngày | Kết hợp trần 250k | 60 (+23) | 496 (+0) | 0 (+0) | 67,4 M |
| Sưu tập | 5 phút/ngày | Hiện tại ×1,25 | 35 | 178 | 321 | 36,7 M |
| Sưu tập | 5 phút/ngày | Phân đoạn | 60 (+25) | 170 (-8) | 329 (+8) | 42,4 M |
| Sưu tập | 5 phút/ngày | Lũy thừa mềm ×1,13 | 60 (+25) | 175 (-3) | 324 (+3) | 29,4 M |
| Sưu tập | 5 phút/ngày | Kết hợp trần 250k | 60 (+25) | 175 (-3) | 324 (+3) | 9,7 M |
| Sưu tập | 3 × 10 phút/ngày | Hiện tại ×1,25 | 36 | 134 | 365 | 25,9 M |
| Sưu tập | 3 × 10 phút/ngày | Phân đoạn | 59 (+23) | 132 (-2) | 367 (+2) | 16,0 M |
| Sưu tập | 3 × 10 phút/ngày | Lũy thừa mềm ×1,13 | 59 (+23) | 137 (+3) | 362 (-3) | 12,5 M |
| Sưu tập | 3 × 10 phút/ngày | Kết hợp trần 250k | 60 (+24) | 134 (+0) | 365 (+0) | 6,1 M |
| Sưu tập | 60 phút/ngày | Hiện tại ×1,25 | 35 | 78 | 421 | 33,3 M |
| Sưu tập | 60 phút/ngày | Phân đoạn | 60 (+25) | 78 (+0) | 421 (+0) | 31,9 M |
| Sưu tập | 60 phút/ngày | Lũy thừa mềm ×1,13 | 58 (+23) | 73 (-5) | 426 (+5) | 19,4 M |
| Sưu tập | 60 phút/ngày | Kết hợp trần 250k | 56 (+21) | 78 (+0) | 421 (+0) | 8,2 M |
| Lai giống | 5 phút/ngày | Hiện tại ×1,25 | 36 | 172 | 327 | 19,2 M |
| Lai giống | 5 phút/ngày | Phân đoạn | 60 (+24) | 172 (+0) | 327 (+0) | 67,0 M |
| Lai giống | 5 phút/ngày | Lũy thừa mềm ×1,13 | 60 (+24) | 176 (+4) | 323 (-4) | 27,6 M |
| Lai giống | 5 phút/ngày | Kết hợp trần 250k | 55 (+19) | 173 (+1) | 326 (-1) | 7,9 M |
| Lai giống | 3 × 10 phút/ngày | Hiện tại ×1,25 | 35 | 51 | 448 | 27,9 M |
| Lai giống | 3 × 10 phút/ngày | Phân đoạn | 57 (+22) | 52 (+1) | 447 (-1) | 12,4 M |
| Lai giống | 3 × 10 phút/ngày | Lũy thừa mềm ×1,13 | 56 (+21) | 51 (+0) | 448 (+0) | 22,3 M |
| Lai giống | 3 × 10 phút/ngày | Kết hợp trần 250k | 56 (+21) | 51 (+0) | 448 (+0) | 4,1 M |
| Lai giống | 60 phút/ngày | Hiện tại ×1,25 | 33 | 41 | 458 | 24,2 M |
| Lai giống | 60 phút/ngày | Phân đoạn | 55 (+22) | 40 (-1) | 459 (+1) | 4,2 M |
| Lai giống | 60 phút/ngày | Lũy thừa mềm ×1,13 | 58 (+25) | 38 (-3) | 461 (+3) | 8,7 M |
| Lai giống | 60 phút/ngày | Kết hợp trần 250k | 46 (+13) | 41 (+0) | 458 (+0) | 9,1 M |
| Theo hướng dẫn | 5 phút/ngày | Hiện tại ×1,25 | 36 | 495 | 1 | 60,3 M |
| Theo hướng dẫn | 5 phút/ngày | Phân đoạn | 60 (+24) | 495 (+0) | 1 (+0) | 63,0 M |
| Theo hướng dẫn | 5 phút/ngày | Lũy thừa mềm ×1,13 | 60 (+24) | 495 (+0) | 1 (+0) | 42,2 M |
| Theo hướng dẫn | 5 phút/ngày | Kết hợp trần 250k | 60 (+24) | 495 (+0) | 1 (+0) | 45,6 M |
| Theo hướng dẫn | 3 × 10 phút/ngày | Hiện tại ×1,25 | 36 | 495 | 1 | 64,2 M |
| Theo hướng dẫn | 3 × 10 phút/ngày | Phân đoạn | 60 (+24) | 495 (+0) | 1 (+0) | 109,8 M |
| Theo hướng dẫn | 3 × 10 phút/ngày | Lũy thừa mềm ×1,13 | 60 (+24) | 495 (+0) | 1 (+0) | 144,1 M |
| Theo hướng dẫn | 3 × 10 phút/ngày | Kết hợp trần 250k | 60 (+24) | 495 (+0) | 1 (+0) | 64,7 M |
| Theo hướng dẫn | 60 phút/ngày | Hiện tại ×1,25 | 36 | 495 | 1 | 66,3 M |
| Theo hướng dẫn | 60 phút/ngày | Phân đoạn | 58 (+22) | 495 (+0) | 1 (+0) | 13,4 M |
| Theo hướng dẫn | 60 phút/ngày | Lũy thừa mềm ×1,13 | 59 (+23) | 495 (+0) | 1 (+0) | 12,0 M |
| Theo hướng dẫn | 60 phút/ngày | Kết hợp trần 250k | 55 (+19) | 495 (+0) | 1 (+0) | 7,3 M |
| Nhẹ nhàng | 5 phút/ngày | Hiện tại ×1,25 | 37 | 496 | 0 | 43,2 M |
| Nhẹ nhàng | 5 phút/ngày | Phân đoạn | 60 (+23) | 496 (+0) | 0 (+0) | 180,1 M |
| Nhẹ nhàng | 5 phút/ngày | Lũy thừa mềm ×1,13 | 60 (+23) | 496 (+0) | 0 (+0) | 180,9 M |
| Nhẹ nhàng | 5 phút/ngày | Kết hợp trần 250k | 60 (+23) | 496 (+0) | 0 (+0) | 120,9 M |
| Nhẹ nhàng | 3 × 10 phút/ngày | Hiện tại ×1,25 | 38 | 496 | 0 | 43,5 M |
| Nhẹ nhàng | 3 × 10 phút/ngày | Phân đoạn | 60 (+22) | 496 (+0) | 0 (+0) | 220,0 M |
| Nhẹ nhàng | 3 × 10 phút/ngày | Lũy thừa mềm ×1,13 | 60 (+22) | 496 (+0) | 0 (+0) | 201,7 M |
| Nhẹ nhàng | 3 × 10 phút/ngày | Kết hợp trần 250k | 60 (+22) | 496 (+0) | 0 (+0) | 191,6 M |
| Nhẹ nhàng | 60 phút/ngày | Hiện tại ×1,25 | 38 | 498 | 0 | 41,3 M |
| Nhẹ nhàng | 60 phút/ngày | Phân đoạn | 60 (+22) | 496 (-2) | 0 (+0) | 319,2 M |
| Nhẹ nhàng | 60 phút/ngày | Lũy thừa mềm ×1,13 | 60 (+22) | 496 (-2) | 0 (+0) | 193,1 M |
| Nhẹ nhàng | 60 phút/ngày | Kết hợp trần 250k | 60 (+22) | 498 (+0) | 0 (+0) | 58,2 M |

### Sổ vàng và vật liệu, 730 ngày (trung vị mỗi đường cong × kiểu, mọi lịch)

| Đường cong | Kiểu | Sản xuất | trong đó offline | Bán | Thành tựu | Chi gói | Chi lai | Chi nâng cấp | Chi dự án | % chi cho dự án | Nâng cấp (cấp) | Chuyến thám hiểm | Vật liệu dự án đã dùng | Vật liệu còn | % lần chạy bị chặn vì vật liệu |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Hiện tại ×1,25 | Thư giãn | 377,6 M | 97 % | 0 | 9350 | 14,8 M | 0 | 1,1 M | 308,1 M | 95 % | 42 | 2180 | 308 | 6266 | 0 % |
| Hiện tại ×1,25 | Nghiên cứu | 477,2 M | 97 % | 0 | 9350 | 14,7 M | 0 | 1,1 M | 385,1 M | 96 % | 42 | 1474 | 324 | 4186 | 0 % |
| Hiện tại ×1,25 | Sưu tập | 277,5 M | 97 % | 0 | 9500 | 3,6 M | 1,4 M | 1,1 M | 246,4 M | 98 % | 42 | 2187 | 292 | 6318 | 0 % |
| Hiện tại ×1,25 | Lai giống | 223,9 M | 97 % | 0 | 9500 | 1,4 M | 1,6 M | 1,1 M | 197,1 M | 98 % | 42 | 2186 | 276 | 6239 | 0 % |
| Hiện tại ×1,25 | Theo hướng dẫn | 328,3 M | 97 % | 0 | 9500 | 14,6 M | 350 | 1,1 M | 246,4 M | 94 % | 42 | 1475 | 292 | 4146 | 0 % |
| Hiện tại ×1,25 | Nhẹ nhàng | 417,2 M | 97 % | 0 | 9350 | 14,7 M | 0 | 1,1 M | 385,1 M | 96 % | 42 | 1471 | 324 | 4112 | 0 % |
| Phân đoạn | Thư giãn | 313,2 M | 97 % | 0 | 9350 | 14,8 M | 0 | 1,1 M | 222,6 M | 93 % | 42 | 2180 | 776 | 5821 | 0 % |
| Phân đoạn | Nghiên cứu | 456,0 M | 97 % | 0 | 9350 | 14,7 M | 0 | 1,1 M | 222,6 M | 93 % | 42 | 1499 | 776 | 3791 | 0 % |
| Phân đoạn | Sưu tập | 270,5 M | 97 % | 0 | 9500 | 3,6 M | 1,3 M | 1,1 M | 222,6 M | 97 % | 42 | 2187 | 776 | 5831 | 0 % |
| Phân đoạn | Lai giống | 199,9 M | 97 % | 0 | 9500 | 1,4 M | 1,6 M | 1,1 M | 182,9 M | 97 % | 42 | 2186 | 704 | 5884 | 0 % |
| Phân đoạn | Theo hướng dẫn | 301,3 M | 97 % | 0 | 9500 | 14,6 M | 350 | 1,1 M | 222,6 M | 93 % | 42 | 1500 | 776 | 3798 | 0 % |
| Phân đoạn | Nhẹ nhàng | 458,3 M | 97 % | 0 | 9350 | 14,7 M | 0 | 1,1 M | 222,6 M | 93 % | 42 | 1496 | 776 | 3791 | 0 % |
| Lũy thừa mềm ×1,13 | Thư giãn | 314,8 M | 97 % | 0 | 9350 | 14,8 M | 0 | 1,1 M | 235,2 M | 94 % | 42 | 2180 | 776 | 5794 | 0 % |
| Lũy thừa mềm ×1,13 | Nghiên cứu | 440,3 M | 97 % | 0 | 9350 | 14,7 M | 0 | 1,1 M | 235,2 M | 94 % | 42 | 1499 | 776 | 3774 | 0 % |
| Lũy thừa mềm ×1,13 | Sưu tập | 226,9 M | 97 % | 0 | 9500 | 3,6 M | 1,5 M | 1,1 M | 208,2 M | 97 % | 42 | 2187 | 752 | 5866 | 0 % |
| Lũy thừa mềm ×1,13 | Lai giống | 202,7 M | 97 % | 0 | 9500 | 1,4 M | 1,5 M | 1,1 M | 184,2 M | 97 % | 42 | 2186 | 728 | 5906 | 0 % |
| Lũy thừa mềm ×1,13 | Theo hướng dẫn | 293,2 M | 97 % | 0 | 9500 | 14,6 M | 350 | 1,1 M | 235,2 M | 94 % | 42 | 1499 | 776 | 3753 | 0 % |
| Lũy thừa mềm ×1,13 | Nhẹ nhàng | 444,1 M | 97 % | 0 | 9350 | 14,7 M | 0 | 1,1 M | 235,2 M | 94 % | 42 | 1500 | 776 | 3782 | 0 % |
| Kết hợp trần 250k | Thư giãn | 339,4 M | 97 % | 0 | 9350 | 14,8 M | 0 | 1,1 M | 266,2 M | 94 % | 42 | 2180 | 776 | 5746 | 0 % |
| Kết hợp trần 250k | Nghiên cứu | 522,3 M | 97 % | 0 | 9350 | 14,7 M | 0 | 1,1 M | 266,2 M | 94 % | 42 | 1497 | 776 | 3782 | 0 % |
| Kết hợp trần 250k | Sưu tập | 278,2 M | 97 % | 0 | 9500 | 3,6 M | 1,5 M | 1,1 M | 266,2 M | 98 % | 42 | 2187 | 776 | 5825 | 0 % |
| Kết hợp trần 250k | Lai giống | 207,5 M | 97 % | 0 | 9500 | 1,4 M | 1,6 M | 1,1 M | 192,0 M | 98 % | 42 | 2186 | 612 | 5942 | 0 % |
| Kết hợp trần 250k | Theo hướng dẫn | 327,6 M | 97 % | 0 | 9500 | 14,6 M | 350 | 1,1 M | 266,2 M | 94 % | 42 | 1494 | 776 | 3852 | 0 % |
| Kết hợp trần 250k | Nhẹ nhàng | 410,2 M | 97 % | 0 | 9350 | 14,7 M | 0 | 1,1 M | 266,2 M | 94 % | 42 | 1498 | 776 | 3744 | 0 % |

### Sau khi xong 60 dự án

| Đường cong | Lần chạy xong 60 | Trung vị ngày xong | Vàng giữ ở ngày 730 (trung vị) |
|---|---|---|---|
| Hiện tại ×1,25 | 0 / 54 | — | — |
| Phân đoạn | 43 / 54 | 543 | 109,8 M |
| Lũy thừa mềm ×1,13 | 37 / 54 | 548 | 134,5 M |
| Kết hợp trần 250k | 38 / 54 | 592 | 76,6 M |

### Tương tác với bảng sky A (trứng Aurorawing, trung vị ngày)

| Kiểu | Lịch | Hiện tại ×1,25 | Phân đoạn | Lũy thừa mềm ×1,13 | Kết hợp trần 250k |
|---|---|---|---|---|---|
| Nghiên cứu | 5 phút/ngày | 50 (45–58) | 50 (45–142) | 49 (47–51) | 50 (45–58) |
| Nghiên cứu | 3 × 10 phút/ngày | 29 (19–63) | 29 (19–117) | ≥19 (19–25) [2/3] | 29 (19–63) |
| Nghiên cứu | 60 phút/ngày | ≥36 (36–70) [2/3] | ≥34 (34–65) [2/3] | ≥23 (23–40) [2/3] | ≥36 (36–70) [2/3] |
| Theo hướng dẫn | 5 phút/ngày | ≥42 (42–47) [2/3] | ≥45 (45–47) [2/3] | ≥42 (42–48) [2/3] | ≥42 (42–47) [2/3] |
| Theo hướng dẫn | 3 × 10 phút/ngày | 14 (14–17) | 14 (14–17) | 17 (17–18) | 14 (14–17) |
| Theo hướng dẫn | 60 phút/ngày | 19 (16–122) | 19 (17–99) | 20 (19–22) | 19 (16–114) |
| Nhẹ nhàng | 5 phút/ngày | ≥46 (46–55) [2/3] | 62 (46–112) | ≥46 (46–50) [2/3] | ≥46 (46–55) [2/3] |
| Nhẹ nhàng | 3 × 10 phút/ngày | ≥28 (28–45) [2/3] | 62 (29–127) | ≥84 (84–126) [2/3] | 45 (28–217) |
| Nhẹ nhàng | 60 phút/ngày | ≥33 [1/3] | ≥33 (33–65) [2/3] | 33 (25–439) | ≥33 [1/3] |


## 6. Ảnh hưởng tới các hệ khác

- **Mua trứng, lai giống, nâng cấp, thám hiểm:** gần như không đổi giữa các đường cong (Δ 0 ± 3 gói / lần lai ở ngày 365 và 730). Nâng cấp chạm trần 42 cấp ở mọi lần chạy.
  - **Giới hạn của mô phỏng:** kịch bản 6 kiểu người chơi mua theo nhịp cố định, không mua nhiều hơn khi dư vàng. Vì vậy mô phỏng **không đo được** việc người chơi thật chuyển vàng sang dự án hay sang gói trứng.
  - Điều đo được là dự án Sanctuary chiếm 93–98 % tổng chi. Các khoản khác (gói tối đa khoảng 15 M, nâng cấp khoảng 1,1 M trong 2 năm) không cạnh tranh nổi với dự án ở bất kỳ đường cong nào.
- **Thu nhập offline:** 97 % sản lượng ở mọi đường cong, kiểu và lịch.
  - Đường cong giá không đổi tỉ lệ này. Nó chỉ quyết định bao nhiêu phần offline được tiêu.
  - Hệ quả: chơi lâu hơn hầu như không làm dự án đến sớm hơn. Đây là vấn đề của công thức thu nhập, ngoài phạm vi U2.15b (không đổi).
- **Vật liệu:**
  - Với các ứng viên, các lần chạy xong 60 dùng 776 đơn vị vật liệu dự án (giá hiện tại: 276–324 vì dừng ở khoảng 36 dự án).
  - Kho vẫn còn 3 700–5 900 đơn vị; **0 % lần chạy bị chặn vì vật liệu**. Vật liệu không phải nút thắt ở bất kỳ đường cong nào.
- **Bán rồng / thành tựu:** 0 G bán trong 6 kiểu chính, khoảng 9 350–9 500 G thành tựu, không đổi giữa đường cong.

## 7. Vàng dư sau khi xong 60 dự án

- Khi Sanctuary xong, không còn khoản chi lớn nào. Trung vị vàng giữ ở ngày 730 của các lần chạy xong 60 là: phân đoạn 109,8 M, lũy thừa mềm 134,5 M, kết hợp 76,6 M.
- Ngay cả giá hiện tại, Theo hướng dẫn 3 × 10 phút giữ 64 M ở ngày 730.
- Đây là rủi ro chính của mọi ứng viên. Dự án 60 cần có "điểm chi" tiếp theo (nội dung sau Sanctuary), nếu không vàng sẽ mất ý nghĩa sau khoảng 18 tháng.

## 8. Tương tác với bảng Thiên Giới A

- Đường cong giá không chạm tới tuyến `sky_cloud`. Với Nghiên cứu và Theo hướng dẫn, ngày nhận trứng Aurorawing gần như trùng nhau giữa các đường cong (ví dụ Theo hướng dẫn 3 × 10 phút: ngày 14–17 ở mọi đường cong).
- Với Nhẹ nhàng, ngày nhận trứng dao động mạnh hơn giữa các đường cong (ví dụ 3 × 10 phút: ≥28 ở giá hiện tại, 62 ở phân đoạn). Lý do là thứ tự mua đổi làm dòng RNG phía sau đổi theo (mục 11), không phải đường cong làm chậm tuyến sky.
- Trong 730 ngày, trứng xuất hiện ở các kiểu Nghiên cứu, Theo hướng dẫn và Nhẹ nhàng; một số seed bị cắt (`[k/n]`). Thư giãn, Sưu tập và Lai giống không nhận trứng ở mọi đường cong, nhiều khả năng do kịch bản của chúng không gửi chuyến tới sân mây.
- Bụi sao không dùng cho dự án, nên bảng A không làm dự án nhanh hơn hay chậm hơn.

## 9. Kiểm tra khai thác (Khai thác Starter, Khai thác lai-bán; 3 × 10 phút/ngày; 365 ngày; 2 seed)

Kịch bản khai thác chạy với giá hiện tại và giá phân đoạn, mỗi kịch bản 2 seed:

| Kịch bản | Dự án sau 365 ngày | Sản xuất | Bán rồng | Vàng giữ | Offline | Vi phạm sổ |
|---|---|---|---|---|---|---|
| Khai thác Starter (1 825 gói) | 2 | 24,8–32,7 M | 0,32–0,33 M | 23,7–31,6 M | 97 % | 0 |
| Khai thác lai-bán (1 095 lần lai) | 2 | 30,3–33,8 M | 0,30–0,31 M | 29,3–32,9 M | 97 % | 0 |

- Hai đường cong cho kết quả **trùng từng số**, vì kịch bản khai thác chỉ mua 2 dự án.
- Bán rồng chỉ khoảng 1 % thu nhập; nguồn chính vẫn là sản xuất offline. Không có vòng lặp mua–bán nào sinh vàng nhanh hơn sản xuất.
- Đổi giá dự án không mở thêm đường khai thác: dự án chỉ là nơi tiêu vàng, không trả lại vàng.
- Rủi ro còn lại: với giá thấp hơn, người chơi dồn vàng (khoảng 30 M sau 1 năm) có thể xây 30–40 dự án gần như ngay lập tức. Đây là "nhảy cóc", không phải khai thác sinh vàng.


## 10. Đề xuất nhịp mục tiêu (để chủ dự án duyệt)

| Mốc | Nhịp đề xuất | Phân đoạn đo được (trung vị) |
|---|---|---|
| Dự án 1 | ngày đầu – tuần đầu | 2,5–10 ngày |
| Dự án 10 | khoảng 3 tuần | 20 ngày |
| Dự án 20 | khoảng 6 tuần | 42 ngày |
| Dự án 30 | khoảng 3 tháng | 94 ngày |
| Dự án 40 | khoảng 6 tháng | 191 ngày |
| Dự án 60 | 12–18 tháng | 543 ngày (21 % lần chạy chưa xong trong 730 ngày) |

Nếu chủ dự án muốn dự án 60 trong khoảng 12 tháng, nên chỉnh đoạn cuối (ví dụ ×1,04–1,05 cho 31–60) và chạy lại mô phỏng trước khi đổi production.

## 11. Rủi ro và độ bất định

1. **3 seed mỗi ô:** khoảng min–max rộng (ví dụ dự án 60 của phân đoạn từ 364 tới hơn 730 ngày). Trung vị là chỉ dấu, không phải dự báo.
2. **Nhiễu dòng RNG:** đổi giá làm thứ tự mua đổi, nên dòng ngẫu nhiên phía sau (lai, thám hiểm) khác đi. Sản lượng vàng giữa đường cong chênh 10–20 % do nhiễu này, không do công thức.
3. **Không có người chơi thật:** kịch bản mua theo nhịp cố định. Người chơi thật có thể dồn vàng vào gói trứng / lai nhiều hơn, làm dự án chậm hơn số ở đây.
4. **Offline 97 %:** nhịp gần như do thời gian lịch quyết định, không do thời gian chơi. Thay đổi công thức offline sẽ làm số liệu này mất hiệu lực.
5. **Vàng dư sau 60 dự án:** 77–135 M G không có chỗ tiêu.
6. **Đuôi ×1,06:** dự án 31–60 có tỉ lệ tăng thấp nhưng nền cao (2,4 M → 14 M), nên 20 dự án cuối chiếm khoảng 3/4 tổng chi và gần 1 năm chơi.

## 12. Những gì PR này **không** làm

- Không đổi `SanctuaryCostCurve.Production` (vẫn ×1,25, 52,2 tỉ G), công thức vàng, offline, nguồn thu nhập, Save Schema (v10).
- Thay đổi Domain duy nhất là hook `SanctuaryCostCurve` (giá đọc qua `Current`, mặc định `Production`): kết quả production trùng từng byte với trước. Không test nào phải đổi kỳ vọng vì hook này.
- Muốn áp dụng một đường cong: cần quyết định của chủ dự án, rồi một PR riêng đổi `Production` và cập nhật test / tài liệu.
