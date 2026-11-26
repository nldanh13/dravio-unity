# U2.13 — Vật liệu dự án Sanctuary theo thế giới (đã duyệt): mô phỏng trước / sau

> Quyết định: [ADR 0209](../adr/0209-u2-13-sanctuary-world-materials.md). Luật đang bật: `SanctuaryMaterialRules.Production = PerWorld` (lùi về thế giới thấp hơn khi thế giới chưa mở / chưa có nguồn).
> Số liệu: `U213SanctuarySimulationTests.Write_u213_sanctuary_report_when_requested` (Release, 3 seed, 365 ngày, lệnh thật, Schema v10), `DRAVIO_U213_SANCTUARY_OUT=<thư mục> DRAVIO_SIM_SEEDS=3`. Trung vị 3 seed. Người chơi mô phỏng chọn tuyến thám hiểm theo vật liệu dự án kế tiếp.

## 1. Giữ nguyên

- 60 dự án, giá `floor(20 000 × 1,25ⁿ)` G, số lượng `1 + n / 5` mỗi loại từ dự án thứ 3 (test `Gold_prices_count_and_quantity_formula_are_unchanged`).
- Tổng 776 đơn vị vật liệu cho 60 dự án ở mọi trạng thái.

## 2. Nhu cầu 60 dự án theo trạng thái thế giới

| Trạng thái | Luật | Gỗ | Hạt | Pha lê | San hô | Bụi sao | Dự án lùi vật liệu | Tổng đơn vị |
|---|---|---|---|---|---|---|---|---|
| Chỉ Khởi Nguyên | U2.12 (gỗ + hạt) | 388 | 388 | 0 | 0 | 0 | — | 776 |
| Chỉ Khởi Nguyên | PerWorld | 388 | 388 | 0 | 0 | 0 | 38 | 776 |
| + Thủy Vực | PerWorld | 128 | 128 | 260 | 260 | 0 | 19 | 776 |
| + Thiên Giới, bảng sky tắt | PerWorld | 128 | 128 | 260 | 260 | 0 | 19 (dự án Thiên Giới dùng pha lê + san hô) | 776 |
| + Thiên Giới, bảng A | PerWorld | 128 | 128 | 125 | 125 | 270 | 0 | 776 |

**Không bế tắc:** 60/60 dự án xây được bằng vật liệu từ tuyến đang mở ở cả 4 trạng thái (test `Every_one_of_the_60_projects_is_buildable_from_open_world_routes_in_every_world_state`). Luật nghiêm ngặt P2 bế tắc ở dự án #9 trước khi mở Thủy Vực (test `Strict_per_world_rule_would_deadlock_at_project_9_before_the_ocean_opens`).

## 3. Mô phỏng 365 ngày, trước / sau

| Luật | Kiểu | Lịch | Dự án 30 / 90 / 365 ngày | Dự án lùi vật liệu | Dùng gỗ / hạt | Dùng pha lê / san hô | Dùng bụi sao | Giữ gỗ / hạt | Giữ pha lê / san hô | Giữ bụi sao | Seed bị chặn vì vật liệu (365) | Vàng 365 | Chuyến rừng / rạn / mây |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Trước (U2.12: gỗ + hạt) | Casual | 5 phút/ngày | 15 / 22 / 34 | 21 | 131 / 131 | 0 / 0 | 0 | 26 / 9 | 400 / 402 | 0 | 0% | 14.19 M | 94 / 265 / 0 |
| Trước (U2.12: gỗ + hạt) | Casual | 3 × 10 phút/ngày | 16 / 25 / 35 | 22 | 138 / 138 | 0 / 0 | 0 | 29 / 9 | 1488 / 1477 | 0 | 0% | 39.35 M | 97 / 982 / 0 |
| Trước (U2.12: gỗ + hạt) | Casual | 60 phút/ngày | 16 / 24 / 33 | 20 | 124 / 124 | 0 / 0 | 0 | 8 / 10 | 1006 / 1050 | 0 | 0% | 10.54 M | 87 / 685 / 0 |
| Trước (U2.12: gỗ + hạt) | Guided | 5 phút/ngày | 14 / 24 / 32 | 20 | 117 / 117 | 0 / 0 | 0 | 7 / 13 | 441 / 422 | 0 | 0% | 21.25 M | 78 / 282 / 0 |
| Trước (U2.12: gỗ + hạt) | Guided | 3 × 10 phút/ngày | 15 / 23 / 33 | 20 | 124 / 124 | 0 / 0 | 0 | 17 / 7 | 1524 / 1500 | 0 | 0% | 18.94 M | 81 / 1008 / 0 |
| Trước (U2.12: gỗ + hạt) | Guided | 60 phút/ngày | 16 / 23 / 31 | 19 | 110 / 110 | 0 / 0 | 0 | 7 / 15 | 1085 / 1022 | 0 | 0% | 10.03 M | 77 / 693 / 0 |
| Trước (U2.12: gỗ + hạt) | Researcher | 5 phút/ngày | 15 / 25 / 34 | 21 | 131 / 131 | 0 / 0 | 0 | 15 / 8 | 418 / 427 | 0 | 0% | 28.76 M | 90 / 268 / 0 |
| Trước (U2.12: gỗ + hạt) | Researcher | 3 × 10 phút/ngày | 15 / 25 / 34 | 21 | 131 / 131 | 0 / 0 | 0 | 9 / 9 | 1487 / 1524 | 0 | 0% | 44.72 M | 91 / 994 / 0 |
| Trước (U2.12: gỗ + hạt) | Researcher | 60 phút/ngày | 14 / 25 / 34 | 21 | 131 / 131 | 0 / 0 | 0 | 9 / 21 | 1037 / 1057 | 0 | 0% | 16.43 M | 93 / 682 / 0 |
| Sau, bảng sky tắt | Casual | 5 phút/ngày | 14 / 24 / 34 | 10 | 38 / 38 | 93 / 93 | 0 | 0 / 11 | 393 / 444 | 0 | 0% | 21.41 M | 28 / 331 / 0 |
| Sau, bảng sky tắt | Casual | 3 × 10 phút/ngày | 16 / 25 / 35 | 11 | 38 / 38 | 100 / 100 | 0 | 12 / 8 | 1494 / 1471 | 0 | 0% | 39.35 M | 33 / 1046 / 0 |
| Sau, bảng sky tắt | Casual | 60 phút/ngày | 16 / 25 / 33 | 10 | 38 / 38 | 86 / 86 | 0 | 4 / 2 | 995 / 1012 | 0 | 0% | 33.49 M | 25 / 711 / 0 |
| Sau, bảng sky tắt | Guided | 5 phút/ngày | 14 / 23 / 32 | 10 | 31 / 31 | 86 / 86 | 0 | 7 / 13 | 417 / 424 | 0 | 0% | 17.50 M | 28 / 333 / 0 |
| Sau, bảng sky tắt | Guided | 3 × 10 phút/ngày | 15 / 23 / 33 | 10 | 38 / 38 | 86 / 86 | 0 | 2 / 7 | 1516 / 1452 | 0 | 0% | 18.94 M | 27 / 1046 / 0 |
| Sau, bảng sky tắt | Guided | 60 phút/ngày | 15 / 25 / 33 | 10 | 38 / 38 | 86 / 86 | 0 | 0 / 10 | 965 / 1008 | 0 | 0% | 21.84 M | 29 / 712 / 0 |
| Sau, bảng sky tắt | Researcher | 5 phút/ngày | 15 / 24 / 34 | 10 | 38 / 38 | 93 / 93 | 0 | 0 / 6 | 409 / 423 | 0 | 0% | 22.96 M | 29 / 331 / 0 |
| Sau, bảng sky tắt | Researcher | 3 × 10 phút/ngày | 15 / 25 / 35 | 11 | 38 / 38 | 100 / 100 | 0 | 8 / 11 | 1481 / 1516 | 0 | 0% | 26.19 M | 30 / 1055 / 0 |
| Sau, bảng sky tắt | Researcher | 60 phút/ngày | 14 / 25 / 33 | 10 | 38 / 38 | 86 / 86 | 0 | 0 / 15 | 991 / 1004 | 0 | 0% | 19.61 M | 28 / 708 / 0 |
| Sau, bảng sky A | Casual | 5 phút/ngày | 14 / 24 / 34 | 5 | 38 / 38 | 65 / 65 | 56 | 0 / 5 | 3 / 16 | 816 | 0% | 28.41 M | 27 / 41 / 291 |
| Sau, bảng sky A | Casual | 3 × 10 phút/ngày | 16 / 25 / 34 | 0 | 38 / 38 | 46 / 46 | 94 | 3 / 1 | 23 / 7 | 2958 | 0% | 32.96 M | 28 / 35 / 1015 |
| Sau, bảng sky A | Casual | 60 phút/ngày | 16 / 25 / 34 | 5 | 38 / 38 | 65 / 65 | 56 | 1 / 10 | 79 / 65 | 1866 | 0% | 10.34 M | 29 / 83 / 625 |
| Sau, bảng sky A | Guided | 5 phút/ngày | 14 / 23 / 32 | 0 | 31 / 31 | 39 / 39 | 94 | 1 / 9 | 7 / 0 | 814 | 0% | 21.85 M | 25 / 27 / 306 |
| Sau, bảng sky A | Guided | 3 × 10 phút/ngày | 14 / 23 / 33 | 0 | 38 / 38 | 39 / 39 | 94 | 0 / 15 | 10 / 8 | 2930 | 0% | 25.70 M | 30 / 32 / 1008 |
| Sau, bảng sky A | Guided | 60 phút/ngày | 15 / 23 / 32 | 0 | 31 / 31 | 39 / 39 | 94 | 8 / 22 | 7 / 7 | 1983 | 0% | 21.59 M | 31 / 26 / 685 |
| Sau, bảng sky A | Researcher | 5 phút/ngày | 15 / 26 / 35 | 0 | 38 / 38 | 46 / 46 | 108 | 8 / 8 | 10 / 11 | 782 | 0% | 22.96 M | 32 / 33 / 293 |
| Sau, bảng sky A | Researcher | 3 × 10 phút/ngày | 15 / 25 / 35 | 0 | 38 / 38 | 46 / 46 | 108 | 10 / 11 | 0 / 12 | 2907 | 0% | 20.73 M | 31 / 34 / 1017 |
| Sau, bảng sky A | Researcher | 60 phút/ngày | 15 / 25 / 34 | 0 | 38 / 38 | 46 / 46 | 94 | 8 / 14 | 22 / 1 | 1929 | 0% | 19.61 M | 30 / 35 / 673 |

"Dự án lùi vật liệu" ở dòng Trước là số dự án thuộc thế giới khác nhưng trả gỗ + hạt (luật cũ không phân biệt). "Seed bị chặn vì vật liệu" = % seed mà lúc ngày 365 dự án kế bị chặn bởi vật liệu chứ không phải vàng.

## 4. Đánh giá

| Tiêu chí | Trước | Sau (sky tắt) | Sau (bảng A) |
|---|---|---|---|
| Dự án sau 365 ngày | 31–35 | 32–35 | 32–35 |
| Giới hạn | vàng | vàng | vàng |
| Seed bị chặn vì vật liệu ở ngày 365 | 0 % | 0 % | 0 % |
| Pha lê / san hô có chỗ tiêu | 0 | 86–100 mỗi loại | 39–65 mỗi loại |
| Pha lê / san hô còn giữ | 400–1 500 | 390–1 500 | 0–80 |
| Bụi sao dùng / còn giữ | — | — | 56–108 / 780–2 960 |
| Chuyến rừng / năm | 77–97 | 25–33 | 25–32 |

- **Số dự án và tốc độ không đổi** (vàng vẫn là giới hạn; ADR 0207 F12 không làm chậm người chơi). Không người chơi nào bị chặn vì vật liệu.
- Khi bảng sky tắt, dư pha lê / san hô vẫn còn vì người chơi chạy rạn liên tục (một slot, chuyến chạy offline): luật mới tạo chỗ tiêu ~90 mỗi loại nhưng nguồn ~1 000 / năm.
- Với bảng A, slot chuyển sang sân mây; pha lê / san hô hết dư, bụi sao thành vật liệu dư (~2 900 ở 3 × 10 phút). Đây là Q4 trong `DECISIONS.md`.
- Người chơi chưa mở Thiên Giới (Casual 60 phút/ngày, 2/3 seed) vẫn xây đủ dự án Thiên Giới bằng pha lê + san hô (lùi), đúng phương án không bế tắc.

## 5. API cho UI

`SanctuaryEconomyDto.NextProjectMaterials`, `NextProjectMaterialZoneId`, `NextProjectMaterialFallback`, `ProgressionQueries.SanctuaryProjectCosts(s)`; chi tiết ở [API_INTEGRATION.md](API_INTEGRATION.md) mục 3.
