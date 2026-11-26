# U2.12 — Kinh tế 365 ngày (luật thật v10)

> Sinh bởi `U212EcosystemValidationTests.Write_u212_ecosystem_report_when_requested` (Release, `DRAVIO_U212_SIM_OUT=<thư mục> DRAVIO_SIM_SEEDS=3`), nhánh `feat/u2-12-ecosystem-validation` sau khi gộp `main` ngày 2026-11-26. 8 kiểu người chơi × 3 lịch chơi × 3 seed, mốc 7 / 30 / 90 / 365 ngày. Luật: E1, E2, E5, R11, U2.11 (17 nhiệm vụ, mốc thám hiểm), Schema v10. Tuyến `sky_cloud` khóa, không bật luật thử nghiệm nào.
> Lệnh chạy lại (khoảng 15 phút trên 4 lõi): `DRAVIO_U212_SIM_OUT=/tmp/u212 DRAVIO_SIM_SEEDS=3 dotnet test -c Release "Assets/Dravio/Tests/Domain/DotNet~/Dravio.Domain.Tests.csproj" --filter "FullyQualifiedName~Write_u212_ecosystem_report_when_requested"`.

## Tóm tắt

| Câu hỏi | Kết quả |
|---|---|
| Có nguồn vàng mới không? | Không. Chi / thu của người chơi thường 0,79–0,94; vàng = 150 + sản xuất + bán + thành tựu − chi (sai số làm tròn ≤ 8 G, test U2.12) |
| Dự án Sanctuary sau 1 năm | 29–35 / 60 với người chơi thường. Giới hạn là **giá vàng** (×1,25 mỗi dự án): dự án kế tiếp sau 365 ngày cần 59–74 ngày thu nhập. 60 dự án không đạt được trong một năm ở mọi kiểu người chơi |
| Vật liệu | Dồn ứ: người chơi chưa mở Thủy Vực giữ 400–2 100 gỗ / hạt; người chơi đã mở Thủy Vực giữ 400–1 500 pha lê / san hô vì sau cổng Thiên Giới pha lê / san hô không còn chỗ tiêu (xác nhận F3). 32–35 dự án chỉ tiêu khoảng 260–300 đơn vị gỗ + hạt, trong khi kho còn 800–4 100 đơn vị |
| Ba thế giới | Guided mở cả hai thế giới ở 100 % seed (Thủy Vực ngày 3–10, Thiên Giới ngày 13–31). Casual 100 % Thủy Vực, Thiên Giới 33–100 % tùy lịch. Người chơi không nghiên cứu (Relaxed, Collector, Breeder, Trader) không bao giờ tới Thủy Vực (F10) |
| Nhiệm vụ | Guided 16 / 17 sau 90 ngày (chỉ `Q_EXPEDITION_CLOUD` khóa). Casual 10–14. `Q_SOCIAL_CLOSE_BOND` chỉ người lai có chủ đích (Guided) đạt |
| Kho | Collector / Breeder chạm trần 500 rồng sau 1 năm (trứng thưởng sẽ phải chờ, xem U2.13) |
| Hiệu năng mô phỏng | `relationPairs` dùng `SocialEcology.Familiarity` (cùng kết quả, có test): 1 seed × 3 lịch × 365 ngày của Casual từ > 10 phút còn 51 giây |

## Phát hiện mới (bổ sung ADR 0207)

| # | Phát hiện | Mức | Đề xuất |
|---|---|---|---|
| F11 | Casual lịch 60 phút/ngày (2 phiên dài) chỉ mở Thiên Giới ở 1 / 3 seed: cổng Thiên Giới cần một loài Thủy Vực đạt Ethology, mà Casual chỉ quan sát một lần đầu phiên và một bữa ăn mỗi phiên; ít phiên thì ít lần quan sát | Trung bình (hành trình người chơi nhẹ) | Unity nhắc "quan sát rồng Thủy Vực" từ `Journey`; hoặc xem lại yêu cầu Ethology của cổng Thiên Giới |
| F12 | Sau khi mở Thủy Vực, người chơi đi tuyến mới nhất và tích hàng nghìn pha lê / san hô không dùng tới | Trung bình | Vật liệu theo thế giới cho dự án Sanctuary (đã duyệt, U2.13 PR riêng) |
| F13 | Collector / Breeder đạt trần kho 500 rồng; khi đó gói và trứng thưởng bị chặn | Thấp | Nhắc bán nhanh khi kho ≥ 90 %; trứng thưởng đã chờ trong `rewardQueue`, không mất |

### Vàng, dự án Sanctuary và vật liệu (trung vị 3 seed; 365 ngày ghi P10 / trung vị / P90)

| Kiểu | Lịch | Số dư 7 ngày | 30 ngày | 90 ngày | 365 ngày | Thu G/ngày (ngày 90–365) | Chi / thu | Dự án 30 / 90 / 365 ngày | Số ngày thu để trả dự án kế | Gỗ / hạt / pha lê / san hô còn giữ | Chuyến đã nhận | Vật liệu từ mốc |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Relaxed | 5 phút/ngày | 42 k | 410 k | 1.62 M | 9.67 M / 10.69 M / 13.27 M | 391 k | 0.91 | 13 / 22 / 32 | 64.6 | 406 / 440 / 0 / 0 | 359 | 30 |
| Relaxed | 3 × 10 phút/ngày | 28 k | 376 k | 1.35 M | 12.82 M / 12.92 M / 23.00 M | 400 k | 0.90 | 12 / 22 / 32 | 63.2 | 1500 / 1533 / 0 / 0 | 1085 | 30 |
| Relaxed | 60 phút/ngày | 25 k | 128 k | 1.41 M | 25.49 M / 30.85 M / 31.75 M | 535 k | 0.82 | 10 / 23 / 33 | 59 | 2023 / 2059 / 0 / 0 | 1447 | 30 |
| Casual | 5 phút/ngày | 65 k | 317 k | 1.61 M | 12.62 M / 14.19 M / 16.21 M | 565 k | 0.93 | 15 / 22 / 34 | 64.8 | 26 / 9 / 400 / 402 | 359 | 60 |
| Casual | 3 × 10 phút/ngày | 40 k | 422 k | 5.28 M | 34.11 M / 39.35 M / 49.97 M | 801 k | 0.84 | 16 / 25 / 35 | 62.1 | 29 / 9 / 1488 / 1477 | 1079 | 60 |
| Casual | 60 phút/ngày | 47 k | 354 k | 3.72 M | 9.25 M / 10.54 M / 34.70 M | 456 k | 0.93 | 16 / 24 / 33 | 69.2 | 8 / 10 / 1006 / 1050 | 772 | 60 |
| Collector | 5 phút/ngày | 48 k | 409 k | 2.59 M | 12.82 M / 21.01 M / 26.63 M | 360 k | 0.86 | 15 / 23 / 32 | 66.1 | 437 / 436 / 0 / 0 | 361 | 30 |
| Collector | 3 × 10 phút/ngày | 63 k | 382 k | 3.42 M | 27.52 M / 28.11 M / 35.21 M | 393 k | 0.79 | 11 / 24 / 32 | 64.3 | 1516 / 1562 / 0 / 0 | 1092 | 30 |
| Collector | 60 phút/ngày | 66 k | 382 k | 3.43 M | 11.16 M / 16.93 M / 22.82 M | 328 k | 0.89 | 14 / 24 / 32 | 74.1 | 2047 / 2083 / 0 / 0 | 1457 | 30 |
| Breeder | 5 phút/ngày | 48 k | 423 k | 2.52 M | 13.46 M / 19.17 M / 24.38 M | 382 k | 0.84 | 13 / 23 / 32 | 63.3 | 419 / 437 / 0 / 0 | 361 | 30 |
| Breeder | 3 × 10 phút/ngày | 88 k | 345 k | 2.83 M | 8.73 M / 15.46 M / 20.54 M | 321 k | 0.86 | 16 / 24 / 32 | 72.4 | 1546 / 1509 / 0 / 0 | 1091 | 30 |
| Breeder | 60 phút/ngày | 87 k | 259 k | 1.60 M | 9.37 M / 15.00 M / 16.53 M | 198 k | 0.83 | 12 / 22 / 29 | 71.2 | 2097 / 2089 / 0 / 0 | 1457 | 30 |
| Researcher | 5 phút/ngày | 39 k | 1.34 M | 4.14 M | 27.38 M / 28.76 M / 29.85 M | 626 k | 0.85 | 15 / 25 / 34 | 63 | 15 / 8 / 418 / 427 | 358 | 60 |
| Researcher | 3 × 10 phút/ngày | 42 k | 544 k | 3.74 M | 26.06 M / 44.72 M / 46.62 M | 684 k | 0.79 | 15 / 25 / 34 | 60.1 | 9 / 9 / 1487 / 1524 | 1082 | 60 |
| Researcher | 60 phút/ngày | 28 k | 334 k | 2.34 M | 12.44 M / 16.43 M / 29.41 M | 580 k | 0.91 | 14 / 25 / 34 | 68 | 9 / 21 / 1037 / 1057 | 773 | 60 |
| Guided | 5 phút/ngày | 52 k | 596 k | 1.15 M | 15.54 M / 21.25 M / 28.87 M | 409 k | 0.82 | 14 / 24 / 32 | 59 | 7 / 13 / 441 / 422 | 360 | 60 |
| Guided | 3 × 10 phút/ngày | 44 k | 463 k | 3.04 M | 13.57 M / 18.94 M / 32.39 M | 556 k | 0.88 | 15 / 23 / 33 | 63.6 | 17 / 7 / 1524 / 1500 | 1089 | 60 |
| Guided | 60 phút/ngày | 63 k | 399 k | 3.70 M | 5.13 M / 10.03 M / 13.15 M | 306 k | 0.94 | 16 / 23 / 31 | 67.9 | 7 / 15 / 1085 / 1022 | 770 | 60 |
| StarterTrader | 5 phút/ngày | 92 k | 759 k | 2.37 M | 25.37 M / 26.78 M / 27.00 M | 87 k | 0.05 | 2 / 2 / 2 | 0.4 | 0 / 0 / 0 / 0 | 0 | 0 |
| StarterTrader | 3 × 10 phút/ngày | 64 k | 392 k | 3.67 M | 23.94 M / 24.76 M / 30.27 M | 81 k | 0.05 | 2 / 2 / 2 | 0.4 | 0 / 0 / 0 / 0 | 0 | 0 |
| StarterTrader | 60 phút/ngày | 52 k | 343 k | 3.08 M | 25.84 M / 26.74 M / 27.86 M | 83 k | 0.05 | 2 / 2 / 2 | 0.4 | 0 / 0 / 0 / 0 | 0 | 0 |
| BreedTrader | 5 phút/ngày | 26 k | 496 k | 1.99 M | 19.90 M / 23.28 M / 23.56 M | 78 k | 0.05 | 2 / 2 / 2 | 0.4 | 0 / 0 / 0 / 0 | 0 | 0 |
| BreedTrader | 3 × 10 phút/ngày | 62 k | 285 k | 3.32 M | 27.64 M / 29.35 M / 32.17 M | 95 k | 0.04 | 2 / 2 / 2 | 0.3 | 0 / 0 / 0 / 0 | 0 | 0 |
| BreedTrader | 60 phút/ngày | 92 k | 435 k | 5.02 M | 33.86 M / 36.21 M / 37.19 M | 114 k | 0.03 | 2 / 2 / 2 | 0.3 | 0 / 0 / 0 / 0 | 0 | 0 |

### Hành trình (ngày mở thế giới: trung vị, % seed mở được trong 365 ngày)

| Kiểu | Lịch | Thủy Vực | Thiên Giới | Nhiệm vụ 30 / 90 / 365 ngày | Danh hiệu | Loài trong Codex | Loài đạt Ethology |
|---|---|---|---|---|---|---|---|
| Relaxed | 5 phút/ngày | — (0%) | — (0%) | 4 / 4 / 4 | 5 | 13 | 0 |
| Relaxed | 3 × 10 phút/ngày | — (0%) | — (0%) | 4 / 4 / 4 | 5 | 13 | 0 |
| Relaxed | 60 phút/ngày | — (0%) | — (0%) | 4 / 4 / 4 | 5 | 13 | 0 |
| Casual | 5 phút/ngày | 14 (100%) | 79 (100%) | 10 / 11 / 12 | 12 | 14 | 2 |
| Casual | 3 × 10 phút/ngày | 6.5 (100%) | 11.5 (100%) | 11 / 14 / 14 | 12 | 14 | 2 |
| Casual | 60 phút/ngày | 6 (100%) | 11.5 (33%) | 10 / 10 / 10 | 10 | 13 | 2 |
| Collector | 5 phút/ngày | — (0%) | — (0%) | 5 / 5 / 5 | 5 | 14 | 0 |
| Collector | 3 × 10 phút/ngày | — (0%) | — (0%) | 5 / 5 / 5 | 5 | 14 | 0 |
| Collector | 60 phút/ngày | — (0%) | — (0%) | 5 / 5 / 5 | 5 | 15 | 0 |
| Breeder | 5 phút/ngày | — (0%) | — (0%) | 5 / 5 / 5 | 5 | 14 | 0 |
| Breeder | 3 × 10 phút/ngày | — (0%) | — (0%) | 5 / 5 / 5 | 5 | 14 | 0 |
| Breeder | 60 phút/ngày | — (0%) | — (0%) | 5 / 5 / 5 | 5 | 15 | 0 |
| Researcher | 5 phút/ngày | 10 (100%) | 34 (66%) | 11 / 14 / 14 | 14 | 14 | 4 |
| Researcher | 3 × 10 phút/ngày | 7.5 (100%) | 12.2 (100%) | 13 / 15 / 15 | 14 | 14 | 4 |
| Researcher | 60 phút/ngày | 5 (100%) | 9.7 (66%) | 12 / 12 / 14 | 13 | 14 | 2 |
| Guided | 5 phút/ngày | 10 (100%) | 31 (100%) | 12 / 16 / 16 | 15 | 14 | 8 |
| Guided | 3 × 10 phút/ngày | 8.5 (100%) | 13.2 (100%) | 16 / 16 / 16 | 15 | 14 | 7 |
| Guided | 60 phút/ngày | 3 (100%) | 13 (100%) | 16 / 16 / 16 | 14 | 14 | 4 |
| StarterTrader | 5 phút/ngày | — (0%) | — (0%) | 0 / 0 / 0 | 2 | 10 | 0 |
| StarterTrader | 3 × 10 phút/ngày | — (0%) | — (0%) | 0 / 0 / 0 | 2 | 10 | 0 |
| StarterTrader | 60 phút/ngày | — (0%) | — (0%) | 0 / 0 / 0 | 2 | 10 | 0 |
| BreedTrader | 5 phút/ngày | — (0%) | — (0%) | 0 / 0 / 0 | 0 | 3 | 0 |
| BreedTrader | 3 × 10 phút/ngày | — (0%) | — (0%) | 0 / 0 / 0 | 0 | 3 | 0 |
| BreedTrader | 60 phút/ngày | — (0%) | — (0%) | 0 / 0 / 0 | 0 | 3 | 0 |

### Hệ sinh thái sau 365 ngày

| Kiểu | Lịch | Cư dân Khởi Nguyên / Thủy Vực / Thiên Giới | Cư dân Good | Loài cư dân | Rồng (kho + sinh cảnh) | Cặp ≥ Bạn | Cặp Thân |
|---|---|---|---|---|---|---|---|
| Relaxed | 5 phút/ngày | 5 / 0 / 0 | 100% | 2 | 366 | 22 | 0 |
| Relaxed | 3 × 10 phút/ngày | 5 / 0 / 0 | 100% | 3 | 367 | 32 | 0 |
| Relaxed | 60 phút/ngày | 5 / 0 / 0 | 100% | 2 | 367 | 31 | 0 |
| Casual | 5 phút/ngày | 5 / 2 / 2 | 100% | 3 | 368 | 48 | 0 |
| Casual | 3 × 10 phút/ngày | 5 / 5 / 2 | 100% | 3 | 369 | 46 | 0 |
| Casual | 60 phút/ngày | 5 / 1 / 0 | 100% | 3 | 368 | 52 | 0 |
| Collector | 5 phút/ngày | 5 / 0 / 0 | 100% | 2 | 500 | 19 k | 4 |
| Collector | 3 × 10 phút/ngày | 5 / 0 / 0 | 100% | 2 | 500 | 16 k | 8 |
| Collector | 60 phút/ngày | 5 / 0 / 0 | 100% | 3 | 500 | 18 k | 10 |
| Breeder | 5 phút/ngày | 5 / 0 / 0 | 100% | 3 | 500 | 19 k | 5 |
| Breeder | 3 × 10 phút/ngày | 5 / 0 / 0 | 100% | 2 | 500 | 22 k | 10 |
| Breeder | 60 phút/ngày | 5 / 0 / 0 | 100% | 3 | 500 | 16 k | 14 |
| Researcher | 5 phút/ngày | 5 / 4 / 2 | 100% | 5 | 368 | 40 | 0 |
| Researcher | 3 × 10 phút/ngày | 5 / 4 / 4 | 100% | 6 | 369 | 53 | 0 |
| Researcher | 60 phút/ngày | 5 / 3 / 2 | 100% | 5 | 369 | 35 | 0 |
| Guided | 5 phút/ngày | 5 / 4 / 4 | 100% | 8 | 369 | 21 | 1 |
| Guided | 3 × 10 phút/ngày | 5 / 3 / 4 | 100% | 8 | 380 | 31 | 2 |
| Guided | 60 phút/ngày | 5 / 3 / 4 | 100% | 7 | 374 | 23 | 2 |
| StarterTrader | 5 phút/ngày | 5 / 0 / 0 | 100% | 3 | 10 | 4 | 0 |
| StarterTrader | 3 × 10 phút/ngày | 5 / 0 / 0 | 100% | 3 | 5 | 6 | 0 |
| StarterTrader | 60 phút/ngày | 5 / 0 / 0 | 100% | 3 | 5 | 6 | 0 |
| BreedTrader | 5 phút/ngày | 5 / 0 / 0 | 100% | 1 | 9 | 35 | 10 |
| BreedTrader | 3 × 10 phút/ngày | 5 / 0 / 0 | 100% | 1 | 8 | 27 | 10 |
| BreedTrader | 60 phút/ngày | 5 / 0 / 0 | 100% | 1 | 9 | 35 | 10 |

### Nhiệm vụ: % seed nhận được trong 365 ngày, ngày nhận (trung vị)

| Nhiệm vụ | Casual 5 phút/ngày | Casual 3 × 10 phút/ngày | Casual 60 phút/ngày | Researcher 5 phút/ngày | Researcher 3 × 10 phút/ngày | Researcher 60 phút/ngày | Guided 5 phút/ngày | Guided 3 × 10 phút/ngày | Guided 60 phút/ngày |
|---|---|---|---|---|---|---|---|---|---|
| `Q_ORIGIN_FIELD_NOTES` | 100% · 1 | 100% · 0.2 | 100% · 0.5 | 100% · 1 | 100% · 0.2 | 100% · 0 | 100% · 1 | 100% · 0.2 | 100% · 0 |
| `Q_ORIGIN_FOREST_SURVEY` | 100% · 6 | 100% · 5.2 | 100% · 3 | 100% · 7 | 100% · 4.2 | 100% · 4 | 100% · 5 | 100% · 2 | 100% · 2.5 |
| `QUEST_AQUATIC_EXPLORATION` | 100% · 14 | 100% · 6.5 | 100% · 6 | 100% · 10 | 100% · 7.5 | 100% · 5 | 100% · 10 | 100% · 8.5 | 100% · 3 |
| `QUEST_SKY_EXPLORATION` | 100% · 79 | 100% · 11.5 | 33% · 11.5 | 66% · 34 | 100% · 12.2 | 66% · 9.7 | 100% · 31 | 100% · 13.2 | 100% · 13 |
| `Q_RESEARCH_NIGHT_WATCH` | 100% · 4 | 100% · 2.2 | 100% · 3.5 | 100% · 4 | 100% · 4 | 100% · 2 | 100% · 4 | 100% · 2 | 100% · 2 |
| `Q_RESEARCH_PERSONALITIES` | 100% · 3 | 100% · 1.2 | 100% · 1 | 100% · 3 | 100% · 1.2 | 100% · 1 | 100% · 3 | 100% · 0.5 | 100% · 0.5 |
| `Q_RESEARCH_ETHOLOGIST` | 0% · — | 33% · 16.5 | 0% · — | 66% · 16.5 | 100% · 10.2 | 33% · 125 | 100% · 9 | 100% · 8.2 | 66% · 14 |
| `Q_RESEARCH_TIDE_JOURNAL` | 33% · 105 | 66% · 27.1 | 33% · 22 | 100% · 30 | 66% · 32.5 | 100% · 12 | 100% · 12 | 100% · 14 | 100% · 5 |
| `Q_RESEARCH_SKY_CHRONICLE` | 66% · 174 | 66% · 51.6 | 33% · 19 | 66% · 35 | 100% · 15.2 | 66% · 11.7 | 100% · 32 | 100% · 13.5 | 100% · 13.5 |
| `Q_CONSERVE_AT_HOME` | 100% · 7 | 100% · 3.2 | 100% · 2 | 100% · 5 | 100% · 2.2 | 100% · 3 | 100% · 5 | 100% · 1.2 | 100% · 1.5 |
| `Q_CONSERVE_TIDES` | 100% · 15 | 100% · 10.2 | 100% · 9 | 100% · 12 | 100% · 8.2 | 100% · 8 | 100% · 11 | 100% · 9 | 100% · 4.5 |
| `Q_CONSERVE_SKIES` | 66% · 217.5 | 66% · 65.2 | 33% · 186 | 33% · 126 | 100% · 30.2 | 66% · 101 | 100% · 32 | 100% · 13.5 | 100% · 13 |
| `Q_SOCIAL_FIRST_FRIENDS` | 100% · 3 | 100% · 2.5 | 100% · 2.5 | 100% · 3 | 100% · 1.5 | 100% · 1.5 | 100% · 3 | 100% · 2 | 100% · 1.5 |
| `Q_SOCIAL_CLOSE_BOND` | 0% · — | 0% · — | 0% · — | 0% · — | 0% · — | 0% · — | 66% · 34 | 100% · 2.5 | 100% · 3.5 |
| `Q_EXPEDITION_PATHFINDER` | 100% · 10 | 100% · 6.5 | 100% · 4 | 100% · 11 | 100% · 5.5 | 100% · 5 | 100% · 9 | 100% · 3.2 | 100% · 3 |
| `Q_EXPEDITION_REEF` | 100% · 18 | 100% · 7.2 | 100% · 7 | 100% · 12 | 100% · 11.5 | 100% · 8.5 | 100% · 12 | 100% · 10 | 100% · 5.5 |
| `Q_EXPEDITION_CLOUD` | 0% · — | 0% · — | 0% · — | 0% · — | 0% · — | 0% · — | 0% · — | 0% · — | 0% · — |
