# U2.14 — Kinh tế & tiến trình Alpha

> **Mô phỏng** bằng `U214EconomyAlphaTests.Write_u214_economy_report_when_requested` (`DRAVIO_U214_OUT=<thư mục>`): luật production (Schema v10, sky_cloud TẮT, vật liệu theo thế giới ADR 0209), 3 seed, trung vị, Release, .NET 8, ngày 2026-11-26. Mọi lần chạy đi qua `AlphaJourney`: **mọi hành động là lệnh chính thức** (quan sát bằng `observeDragon`), và sau **mỗi lệnh** kiểm sổ vàng, trần sản xuất, sổ vật liệu, thưởng một lần.
> **Đã chạy (test có assert, CI):** các kết luận trong mục 1 có test tương ứng (`U214EconomyAlphaTests`, `U214AlphaJourneyTests`).
> **Chưa kiểm thử:** quảng cáo / IAP (không có trong Domain), hành vi người chơi thật, thiết bị thật.

Kiểu người chơi (định nghĩa trong `EconomySimulator.Policy`): **Thư giãn** = Relaxed (1 gói/ngày, không lai, không bán, có thám hiểm); **Nhà nghiên cứu** = Researcher (thêm cho ăn, xem hồ sơ, quan sát mọi rồng mỗi 30 s, nhiệm vụ, mở thế giới); **Nhà sưu tập** = Collector (1 gói/phiên, lai khi có cặp); **Nhà lai giống** = Breeder (lai liên tục cặp đắt nhất, gõ tổ, 1 gói/ngày). Thêm hai kiểu có hướng dẫn hành trình: Guided, Casual. Lịch: 5 phút/ngày, 3 × 10 phút/ngày, 60 phút/ngày (2 × 30); giữa các phiên là offline (75 %, tối đa 24 h).

## 1. Kết luận

| # | Yêu cầu | Kết quả | Loại / bằng chứng |
|---|---|---|---|
| E1 | Không nguồn vàng ngoài luật | **0 vi phạm** sổ vàng trên 54 lần chạy 365 ngày (~6,6 triệu lệnh). Vàng chỉ từ sản xuất, bán, 12 thành tựu; nhiệm vụ / thám hiểm / mốc / quan sát / tutorial không trả vàng | mô phỏng + test `Every_policy_keeps_the_gold_and_material_ledgers_closed` (7 kiểu, kể cả hai kiểu khai thác Starter/Breed trader) |
| E2 | Sản xuất không vượt trần | mọi lần quyết toán ≤ Σ rate Adult × thời gian; offline đúng 24 h × 75 % | mô phỏng + `SAVE_RELIABILITY.md` |
| E3 | Thưởng nhiệm vụ / thám hiểm đúng số lần | **0 lần trả trùng**: mỗi nhiệm vụ 1 lần, mỗi mốc (1 / 5 / 15 / 40 chuyến) 1 lần, mỗi trứng thưởng 1 lần, mỗi chuyến 1 lần; số lượng mỗi chuyến trong [Min, Max] của tuyến; gửi lại nhiệm vụ → `quest-completed` | `Quest_milestone_egg_and_achievement_rewards_are_paid_exactly_once` |
| E4 | Không bế tắc vì thiếu vật liệu | mọi dự án trong 60 dự án, ở mọi trạng thái thế giới (1 / 2 / 3 thế giới, bảng sky tắt và A), chỉ đòi vật liệu mà một tuyến **mở và có bảng thưởng** cho ra; bảng tắt không bao giờ đòi bụi sao. Mô phỏng: 0 % seed bị chặn vì vật liệu ở ngày 365 | `Every_one_of_the_60_projects_asks_only_for_materials_an_open_approved_route_produces` |
| E5 | Hoàn thành 60 dự án | **Không đạt được trong thời gian thực tế.** 29–35 dự án sau 365 ngày, chặn bởi **vàng** (không phải vật liệu). Tổng giá 60 dự án ≈ 52,2 tỉ G; ngoại suy: dự án 40 sau ~1,5–3 năm, dự án 60 sau > 150 năm | mục 4, `Sixty_projects_cost_table_…` |
| E6 | Vật liệu theo thế giới | người không mở Thủy Vực (Thư giãn, Sưu tập, Lai giống) xây toàn bộ bằng gỗ / hạt (lùi vật liệu); người mở Thủy Vực dùng pha lê / san hô; dư 400–2 100 mỗi loại sau 365 ngày | mục 3 |
| E7 | Kho 500 rồng | Sưu tập và Lai giống chạm 500 trước ngày 365 (3 × 10 phút: ~ngày 90) rồi ngừng mua gói (`storage-full`, không trừ vàng); trứng thưởng vào hàng chờ, không mất | mục 2, `SAVE_RELIABILITY.md` |
| E8 | Bốn kiểu của đề bài không mở Thủy Vực nếu không nghiên cứu | Thư giãn / Sưu tập / Lai giống: 1 thế giới, 4–5 nhiệm vụ (F10 của U2.12, không đổi) | mục 2 |

## 2. Bảng theo mốc 1 / 7 / 30 / 90 / 365 ngày (production)

Cột "Vi phạm sổ" = tổng số vi phạm (vàng, vật liệu, trần, trả trùng, lệnh bị từ chối làm đổi state) của 3 seed.

### Vàng đang giữ (G) ngày 1 / 7 / 30 / 90 / 365

| Biến thể | Kiểu | Lịch | Vàng giữ | Vàng sản xuất tích lũy | G/s cuối | Vi phạm sổ |
|---|---|---|---|---|---|---|
| production | Thư giãn (Relaxed) | 5 phút/ngày | 3565 / 42 k / 410 k / 1.62 M / 10.69 M | 3365 / 107 k / 2.55 M / 15.99 M / 123.46 M | 8.2 | 0 |
| production | Thư giãn (Relaxed) | 3 × 10 phút/ngày | 4604 / 28 k / 376 k / 1.35 M / 12.92 M | 5004 / 103 k / 2.87 M / 16.65 M / 125.65 M | 7.5 | 0 |
| production | Thư giãn (Relaxed) | 60 phút/ngày | 4889 / 25 k / 128 k / 1.41 M / 30.85 M | 5039 / 133 k / 1.93 M / 20.05 M / 168.84 M | 8.6 | 0 |
| production | Nhà nghiên cứu (Researcher) | 5 phút/ngày | 3565 / 50 k / 1.60 M / 6.34 M / 36.41 M | 3365 / 150 k / 4.17 M / 31.22 M / 245.47 M | 14.3 | 0 |
| production | Nhà nghiên cứu (Researcher) | 3 × 10 phút/ngày | 4797 / 67 k / 536 k / 1.63 M / 31.29 M | 5197 / 194 k / 4.19 M / 25.00 M / 200.86 M | 11.1 | 0 |
| production | Nhà nghiên cứu (Researcher) | 60 phút/ngày | 5083 / 40 k / 238 k / 1.93 M / 33.91 M | 5233 / 124 k / 3.31 M / 18.52 M / 171.97 M | 9.0 | 0 |
| production | Nhà sưu tập (Collector) | 5 phút/ngày | 3565 / 48 k / 409 k / 2.59 M / 21.01 M | 3365 / 129 k / 3.54 M / 20.45 M / 119.37 M | 5.6 | 0 |
| production | Nhà sưu tập (Collector) | 3 × 10 phút/ngày | 15 k / 63 k / 382 k / 3.42 M / 28.11 M | 15 k / 379 k / 4.69 M / 26.91 M / 134.90 M | 6.0 | 0 |
| production | Nhà sưu tập (Collector) | 60 phút/ngày | 12 k / 66 k / 382 k / 3.43 M / 16.93 M | 12 k / 305 k / 5.33 M / 25.47 M / 115.79 M | 5 | 0 |
| production | Nhà lai giống (Breeder) | 5 phút/ngày | 3565 / 48 k / 423 k / 2.52 M / 19.17 M | 3365 / 130 k / 2.72 M / 19.67 M / 120.60 M | 6.1 | 0 |
| production | Nhà lai giống (Breeder) | 3 × 10 phút/ngày | 6891 / 88 k / 345 k / 2.83 M / 15.46 M | 7301 / 347 k / 5.64 M / 24.19 M / 112.44 M | 4.9 | 0 |
| production | Nhà lai giống (Breeder) | 60 phút/ngày | 15 k / 87 k / 259 k / 1.60 M / 15.00 M | 15 k / 389 k / 3.30 M / 15.20 M / 69.75 M | 3.0 | 0 |
| production | Theo hướng dẫn (Guided) | 5 phút/ngày | 3565 / 49 k / 1.42 M / 1.34 M / 14.95 M | 3365 / 150 k / 3.96 M / 21.62 M / 127.70 M | 7.3 | 0 |
| production | Theo hướng dẫn (Guided) | 3 × 10 phút/ngày | 5936 / 74 k / 344 k / 1.62 M / 18.05 M | 6551 / 257 k / 4.69 M / 18.58 M / 137.19 M | 7.4 | 0 |
| production | Theo hướng dẫn (Guided) | 60 phút/ngày | 6799 / 67 k / 344 k / 2.86 M / 18.50 M | 7849 / 232 k / 4.72 M / 20.03 M / 126.87 M | 6.7 | 0 |
| production | Nhẹ nhàng có hướng dẫn (Casual) | 5 phút/ngày | 3565 / 35 k / 1.23 M / 3.98 M / 15.07 M | 3365 / 92 k / 4.32 M / 24.48 M / 149.86 M | 8.8 | 0 |
| production | Nhẹ nhàng có hướng dẫn (Casual) | 3 × 10 phút/ngày | 4732 / 38 k / 434 k / 2.81 M / 39.90 M | 5132 / 117 k / 3.24 M / 26.83 M / 248.87 M | 14.1 | 0 |
| production | Nhẹ nhàng có hướng dẫn (Casual) | 60 phút/ngày | 5018 / 23 k / 555 k / 2.52 M / 19.55 M | 5168 / 106 k / 4.01 M / 22.39 M / 168.04 M | 9.5 | 0 |

### Tiến trình ngày 1 / 7 / 30 / 90 / 365

| Biến thể | Kiểu | Lịch | Gói mua | Loài (Codex) | Dự án | Nhiệm vụ | Thế giới mở |
|---|---|---|---|---|---|---|---|
| production | Thư giãn (Relaxed) | 5 phút/ngày | 1 / 7 / 30 / 90 / 365 | 2 / 6 / 11 / 12 / 13 | 0 / 0 / 13 / 22 / 32 | 0 / 2 / 4 / 4 / 4 | 1 / 1 / 1 / 1 / 1 |
| production | Thư giãn (Relaxed) | 3 × 10 phút/ngày | 2 / 8 / 31 / 91 / 366 | 3 / 6 / 12 / 13 / 13 | 0 / 1 / 12 / 22 / 32 | 0 / 4 / 4 / 4 / 4 | 1 / 1 / 1 / 1 / 1 |
| production | Thư giãn (Relaxed) | 60 phút/ngày | 2 / 8 / 31 / 91 / 366 | 3 / 7 / 11 / 13 / 13 | 0 / 1 / 10 / 23 / 33 | 0 / 3 / 4 / 4 / 4 | 1 / 1 / 1 / 1 / 1 |
| production | Nhà nghiên cứu (Researcher) | 5 phút/ngày | 1 / 7 / 30 / 90 / 365 | 2 / 6 / 13 / 14 / 14 | 0 / 1 / 14 / 25 / 35 | 0 / 5 / 12 / 14 / 14 | 1 / 1 / 3 / 3 / 3 |
| production | Nhà nghiên cứu (Researcher) | 3 × 10 phút/ngày | 2 / 8 / 31 / 91 / 366 | 3 / 9 / 13 / 14 / 14 | 0 / 2 / 15 / 24 / 34 | 1 / 8 / 13 / 14 / 14 | 1 / 2 / 3 / 3 / 3 |
| production | Nhà nghiên cứu (Researcher) | 60 phút/ngày | 2 / 8 / 31 / 91 / 366 | 3 / 8 / 12 / 14 / 14 | 0 / 1 / 13 / 23 / 33 | 1 / 7 / 10 / 13 / 13 | 1 / 1 / 2 / 3 / 3 |
| production | Nhà sưu tập (Collector) | 5 phút/ngày | 1 / 7 / 30 / 90 / 178 | 2 / 7 / 13 / 14 / 14 | 0 / 0 / 15 / 23 / 32 | 0 / 4 / 5 / 5 / 5 | 1 / 1 / 1 / 1 / 1 |
| production | Nhà sưu tập (Collector) | 3 × 10 phút/ngày | 4 / 22 / 91 / 134 / 134 | 3 / 13 / 14 / 14 / 14 | 0 / 1 / 11 / 24 / 32 | 3 / 5 / 5 / 5 / 5 | 1 / 1 / 1 / 1 / 1 |
| production | Nhà sưu tập (Collector) | 60 phút/ngày | 3 / 15 / 61 / 78 / 78 | 4 / 13 / 15 / 15 / 15 | 0 / 0 / 14 / 24 / 32 | 4 / 5 / 5 / 5 / 5 | 1 / 1 / 1 / 1 / 1 |
| production | Nhà lai giống (Breeder) | 5 phút/ngày | 1 / 7 / 30 / 90 / 172 | 2 / 7 / 13 / 14 / 14 | 0 / 0 / 13 / 23 / 32 | 0 / 4 / 5 / 5 / 5 | 1 / 1 / 1 / 1 / 1 |
| production | Nhà lai giống (Breeder) | 3 × 10 phút/ngày | 2 / 8 / 31 / 51 / 51 | 3 / 10 / 13 / 14 / 14 | 0 / 3 / 16 / 24 / 32 | 2 / 5 / 5 / 5 / 5 | 1 / 1 / 1 / 1 / 1 |
| production | Nhà lai giống (Breeder) | 60 phút/ngày | 2 / 8 / 31 / 41 / 41 | 4 / 9 / 15 / 15 / 15 | 0 / 4 / 12 / 22 / 29 | 3 / 5 / 5 / 5 / 5 | 1 / 1 / 1 / 1 / 1 |
| production | Theo hướng dẫn (Guided) | 5 phút/ngày | 1 / 7 / 30 / 90 / 365 | 2 / 6 / 12 / 14 / 14 | 0 / 0 / 14 / 24 / 32 | 0 / 5 / 16 / 16 / 16 | 1 / 1 / 3 / 3 / 3 |
| production | Theo hướng dẫn (Guided) | 3 × 10 phút/ngày | 4 / 13 / 36 / 96 / 371 | 4 / 10 / 14 / 14 / 14 | 0 / 2 / 16 / 23 / 32 | 2 / 11 / 16 / 16 / 16 | 1 / 2 / 3 / 3 / 3 |
| production | Theo hướng dẫn (Guided) | 60 phút/ngày | 3 / 14 / 38 / 98 / 373 | 3 / 10 / 13 / 14 / 14 | 0 / 2 / 16 / 23 / 32 | 2 / 12 / 15 / 15 / 15 | 1 / 2 / 3 / 3 / 3 |
| production | Nhẹ nhàng có hướng dẫn (Casual) | 5 phút/ngày | 1 / 7 / 30 / 90 / 365 | 2 / 6 / 12 / 14 / 14 | 0 / 0 / 15 / 24 / 33 | 0 / 6 / 11 / 12 / 12 | 1 / 1 / 2 / 3 / 3 |
| production | Nhẹ nhàng có hướng dẫn (Casual) | 3 × 10 phút/ngày | 2 / 8 / 31 / 91 / 366 | 3 / 7 / 12 / 14 / 14 | 0 / 1 / 13 / 25 / 35 | 1 / 8 / 10 / 11 / 11 | 1 / 2 / 2 / 3 / 3 |
| production | Nhẹ nhàng có hướng dẫn (Casual) | 60 phút/ngày | 2 / 8 / 31 / 91 / 366 | 3 / 7 / 12 / 14 / 14 | 0 / 1 / 15 / 24 / 33 | 1 / 8 / 10 / 10 / 10 | 1 / 2 / 2 / 2 / 2 |

### Ngày 365: rồng, vật liệu, mở thế giới

| Biến thể | Kiểu | Lịch | Rồng | Trong kho | Gỗ / hạt | Pha lê / san hô | Bụi sao | Seed bị chặn vì vật liệu | Ngày Thủy Vực | Ngày Thiên Giới | Lệnh | Giây |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| production | Thư giãn (Relaxed) | 5 phút/ngày | 366 | 361 | 406 / 440 | 0 / 0 | 0 | 0% | — | — | 16846 | 4 |
| production | Thư giãn (Relaxed) | 3 × 10 phút/ngày | 367 | 362 | 1500 / 1533 | 0 / 0 | 0 | 0% | — | — | 78166 | 12 |
| production | Thư giãn (Relaxed) | 60 phút/ngày | 367 | 362 | 2023 / 2059 | 0 / 0 | 0 | 0% | — | — | 144943 | 23 |
| production | Nhà nghiên cứu (Researcher) | 5 phút/ngày | 368 | 355 | 9 / 10 | 396 / 395 | 0 | 0% | 11 (100%) | 23 (100%) | 160160 | 23 |
| production | Nhà nghiên cứu (Researcher) | 3 × 10 phút/ngày | 369 | 356 | 1 / 7 | 1508 / 1523 | 0 | 0% | 4.5 (100%) | 21 (100%) | 858914 | 130 |
| production | Nhà nghiên cứu (Researcher) | 60 phút/ngày | 369 | 360 | 19 / 27 | 941 / 987 | 0 | 0% | 12.5 (100%) | 45.2 (66%) | 1285250 | 177 |
| production | Nhà sưu tập (Collector) | 5 phút/ngày | 500 | 495 | 437 / 436 | 0 / 0 | 0 | 0% | — | — | 23932 | 16 |
| production | Nhà sưu tập (Collector) | 3 × 10 phút/ngày | 500 | 495 | 1516 / 1562 | 0 / 0 | 0 | 0% | — | — | 139635 | 91 |
| production | Nhà sưu tập (Collector) | 60 phút/ngày | 500 | 495 | 2047 / 2083 | 0 / 0 | 0 | 0% | — | — | 265466 | 181 |
| production | Nhà lai giống (Breeder) | 5 phút/ngày | 500 | 495 | 419 / 437 | 0 / 0 | 0 | 0% | — | — | 29178 | 18 |
| production | Nhà lai giống (Breeder) | 3 × 10 phút/ngày | 500 | 495 | 1546 / 1509 | 0 / 0 | 0 | 0% | — | — | 145504 | 93 |
| production | Nhà lai giống (Breeder) | 60 phút/ngày | 500 | 495 | 2097 / 2089 | 0 / 0 | 0 | 0% | — | — | 277423 | 170 |
| production | Theo hướng dẫn (Guided) | 5 phút/ngày | 369 | 356 | 12 / 7 | 417 / 418 | 0 | 0% | 11 (100%) | 19 (66%) | 148275 | 34 |
| production | Theo hướng dẫn (Guided) | 3 × 10 phút/ngày | 375 | 363 | 1 / 7 | 1507 / 1529 | 0 | 0% | 3.2 (100%) | 9.2 (100%) | 934460 | 168 |
| production | Theo hướng dẫn (Guided) | 60 phút/ngày | 377 | 364 | 11 / 1 | 965 / 1017 | 0 | 0% | 3.0 (100%) | 11.5 (100%) | 1755434 | 308 |
| production | Nhẹ nhàng có hướng dẫn (Casual) | 5 phút/ngày | 368 | 360 | 7 / 14 | 398 / 407 | 0 | 0% | 11 (100%) | 27 (66%) | 35431 | 10 |
| production | Nhẹ nhàng có hướng dẫn (Casual) | 3 × 10 phút/ngày | 369 | 360 | 11 / 9 | 1512 / 1486 | 0 | 0% | 5.2 (100%) | 31.7 (66%) | 137358 | 32 |
| production | Nhẹ nhàng có hướng dẫn (Casual) | 60 phút/ngày | 368 | 363 | 1 / 2 | 1016 / 980 | 0 | 0% | 5.5 (100%) | 25.0 (33%) | 172471 | 42 |

### Sổ thưởng (365 ngày, trung vị)

| Biến thể | Kiểu | Lịch | Vàng sản xuất | Bán | Thành tựu | Chi | Trứng thưởng | Nhiệm vụ | Mốc | Trả trùng | Ngày trứng Aurorawing | Ngày dự án 30 | Ngày dự án 35 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| production | Thư giãn (Relaxed) | 5 phút/ngày | 123.46 M | 0 | 9350 | 112.79 M | 0 | 4 | 4 | 0 | — | 272 (100%) | — |
| production | Thư giãn (Relaxed) | 3 × 10 phút/ngày | 125.65 M | 0 | 9350 | 112.76 M | 0 | 4 | 4 | 0 | — | 252 (100%) | — |
| production | Thư giãn (Relaxed) | 60 phút/ngày | 168.84 M | 0 | 9350 | 138.01 M | 0 | 4 | 4 | 0 | — | 196.5 (100%) | — |
| production | Nhà nghiên cứu (Researcher) | 5 phút/ngày | 245.47 M | 0 | 9350 | 209.05 M | 2 | 14 | 7 | 0 | — | 157 (100%) | 333 (100%) |
| production | Nhà nghiên cứu (Researcher) | 3 × 10 phút/ngày | 200.86 M | 0 | 9350 | 169.58 M | 2 | 14 | 7 | 0 | — | 182.5 (100%) | — |
| production | Nhà nghiên cứu (Researcher) | 60 phút/ngày | 171.97 M | 0 | 9350 | 137.91 M | 2 | 13 | 7 | 0 | — | 197 (100%) | — |
| production | Nhà sưu tập (Collector) | 5 phút/ngày | 119.37 M | 0 | 9500 | 108.61 M | 0 | 5 | 4 | 0 | — | 243 (100%) | — |
| production | Nhà sưu tập (Collector) | 3 × 10 phút/ngày | 134.90 M | 0 | 9500 | 106.86 M | 0 | 5 | 4 | 0 | — | 207.5 (100%) | — |
| production | Nhà sưu tập (Collector) | 60 phút/ngày | 115.79 M | 0 | 9500 | 106.08 M | 0 | 5 | 4 | 0 | — | 233 (100%) | — |
| production | Nhà lai giống (Breeder) | 5 phút/ngày | 120.60 M | 0 | 9500 | 108.58 M | 0 | 5 | 4 | 0 | — | 250 (100%) | — |
| production | Nhà lai giống (Breeder) | 3 × 10 phút/ngày | 112.44 M | 0 | 9500 | 105.40 M | 0 | 5 | 4 | 0 | — | 238 (100%) | — |
| production | Nhà lai giống (Breeder) | 60 phút/ngày | 69.75 M | 0 | 9500 | 55.12 M | 0 | 5 | 4 | 0 | — | 258 (33%) | — |
| production | Theo hướng dẫn (Guided) | 5 phút/ngày | 127.70 M | 0 | 9500 | 112.76 M | 2 | 16 | 7 | 0 | — | 222.5 (66%) | — |
| production | Theo hướng dẫn (Guided) | 3 × 10 phút/ngày | 137.19 M | 0 | 9500 | 112.88 M | 2 | 16 | 7 | 0 | — | 237.2 (100%) | — |
| production | Theo hướng dẫn (Guided) | 60 phút/ngày | 126.87 M | 0 | 9500 | 112.94 M | 2 | 15 | 7 | 0 | — | 249 (100%) | — |
| production | Nhẹ nhàng có hướng dẫn (Casual) | 5 phút/ngày | 149.86 M | 0 | 9350 | 138.03 M | 2 | 12 | 7 | 0 | — | 220 (100%) | 357 (33%) |
| production | Nhẹ nhàng có hướng dẫn (Casual) | 3 × 10 phút/ngày | 248.87 M | 0 | 9350 | 208.98 M | 2 | 11 | 7 | 0 | — | 163.2 (100%) | 328 (66%) |
| production | Nhẹ nhàng có hướng dẫn (Casual) | 60 phút/ngày | 168.04 M | 0 | 9350 | 138.01 M | 1 | 10 | 7 | 0 | — | 204.5 (100%) | — |


"Vàng sản xuất tích lũy" là `totalGoldEarned` (không gồm bán và thành tựu). Các kiểu không bán rồng nên cột "Bán" = 0; trader được kiểm riêng trong test E1.

## 3. Vật liệu theo thế giới

| Kiểu | Thế giới mở ở ngày 365 | Vật liệu dự án đã dùng | Dư ở ngày 365 (3 × 10 phút) | Bị chặn vì vật liệu |
|---|---|---|---|---|
| Thư giãn, Sưu tập, Lai giống | Khởi Nguyên | gỗ + hạt (công trình Thủy Vực / Thiên Giới lùi về gỗ + hạt) | gỗ / hạt ~1 500 / 1 500 | 0 % |
| Nghiên cứu, Guided, Casual | 2–3 thế giới | gỗ + hạt, rồi pha lê + san hô | gỗ / hạt ≤ 30; pha lê / san hô ~1 500 / 1 500 | 0 % |
| Mọi kiểu, bảng sky tắt | — | bụi sao: 0 (không bao giờ đòi) | 0 | 0 % |

Dư vật liệu là Q4 của U2.13 (chưa có nơi tiêu, chưa đề xuất số).

## 4. 60 dự án Sanctuary

Giá dự án n (0-based) = 20 000 × 1,25ⁿ G (không đổi từ U2.10).

| Dự án thứ | Giá | Tổng tích lũy |
|---|---|---|
| 10 | 149 011 G | 665 055 G |
| 20 | 1,39 M | 6,86 M |
| 30 | 12,9 M | 64,5 M |
| 32 | 20,2 M | 100,9 M |
| 35 | 39,4 M | 197,1 M |
| 40 | 120,4 M | 601,8 M |
| 45 | 367,3 M | 1,84 tỉ |
| 50 | 1,12 tỉ | 5,61 tỉ |
| 60 | 10,44 tỉ | **52,20 tỉ** |

Thu nhập sau ngày 90 (mô phỏng, vàng sản xuất tích lũy ngày 365 − ngày 90, chia 275 ngày): Guided 3 × 10 phút ≈ 431 k G/ngày; cao nhất là Casual 3 × 10 phút ≈ 807 k G/ngày. Ngoại suy tuyến tính (**ước lượng, không mô phỏng**; ngân sách sản xuất có trần nên thu nhập gần như phẳng):

| Mốc | Guided 3 × 10 phút | Casual 3 × 10 phút |
|---|---|---|
| 36 dự án | +0,9 năm sau ngày 365 | +0,4 năm |
| 40 dự án | +3,2 năm | +1,5 năm |
| 50 dự án | +35 năm | +17 năm |
| 60 dự án | +331 năm | +158 năm |

**Khả năng hoàn thành 60 dự án: không**, với đường cong giá hiện hành. Đây là quyết định thiết kế (N1 trong [BALANCE_DECISIONS.md](BALANCE_DECISIONS.md)); U2.14 không đổi giá.

## 5. Thời điểm mở thế giới, nhiệm vụ

Xem bảng mục 2 ("Ngày Thủy Vực", "Ngày Thiên Giới", "Nhiệm vụ"). Guided 3 × 10 phút: Thủy Vực ngày 3,2, Thiên Giới ngày 9,2, 16 / 17 nhiệm vụ từ ngày 30. Casual 60 phút: Thiên Giới chỉ 33 % seed (F11 của U2.12, Q5 U2.13, không đổi).

## 6. So sánh với bảng sky_cloud A (chỉ mô phỏng)

Chạy với `DRAVIO_U214_VARIANTS=skyA` cho ba kiểu có nghiên cứu (kiểu không mở Thiên Giới không bị ảnh hưởng). Game thật không đổi.

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

## 7. Chạy lại

```bash
DRAVIO_U214_OUT=/tmp/u214 dotnet test "Assets/Dravio/Tests/Domain/DotNet~/Dravio.Domain.Tests.csproj" -c Release --filter "FullyQualifiedName~Write_u214_economy_report_when_requested"
# bảng A cho ba kiểu có nghiên cứu:
DRAVIO_U214_OUT=/tmp/u214 DRAVIO_U214_VARIANTS=skyA DRAVIO_SIM_POLICIES=Guided,Casual,Researcher dotnet test … (như trên)
```

Tùy chọn: `DRAVIO_SIM_SEEDS` (mặc định 3), `DRAVIO_SIM_POLICIES`, `DRAVIO_SIM_DAYS`. Thời gian: ~25 phút cho production trên 4 lõi (cột "Giây" là thời gian của 3 seed song song).
