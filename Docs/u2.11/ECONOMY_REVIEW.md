# U2.11 — Đánh giá kinh tế sau Schema v9 / v10

> Nguồn: `U211EconomyReviewTests.Write_u211_economy_review_when_requested` (biến thể live, 6 kiểu người chơi × 3 lịch chơi × 90 ngày, **4 seed**).
> Lần chạy 20 seed vượt giới hạn 30 phút của môi trường nên đã giảm xuống 4 seed. P10 / P90 với 4 seed chỉ mang tính tham khảo. Chạy lại: `DRAVIO_U211_SIM_OUT=<file> DRAVIO_SIM_SEEDS=20 dotnet test … --filter Write_u211_economy_review` (khoảng 1,5–2 giờ).
> So sánh với mô phỏng U2.10 50 seed: người chơi thường tiêu khoảng 88% vàng, giữ 2–3 M, xây 23–25 dự án sau 90 ngày.

## Kết luận

1. **Kinh tế vàng gần như không đổi so với U2.10.** Người chơi thường (Relaxed, Collector, Breeder) tiêu 85–93% nguồn thu, giữ 1,3–3,9 M (trung vị), xây 22–24 dự án. Thưởng nhiệm vụ U2.11 không có vàng, nên không thêm nguồn vàng.
2. **Hạt giống vẫn là nút thắt, nhưng nhẹ hơn.** Thưởng mốc thám hiểm thêm 30 vật liệu cho người chơi thường và 48–60 cho Researcher. Con số này nhỏ (khoảng 25% so với khoảng 120 vật liệu dùng cho dự án), đúng như thiết kế. Vật liệu còn giữ sau 90 ngày khá nhiều (134–983), nên giới hạn dự án lúc này là giá vàng (×1,25 mỗi dự án) chứ không còn là hạt.
3. **Researcher là người xây nhiều nhất** (24,5–25 dự án, số dư 2–4,4 M). Lý do là nhiệm vụ và mốc thưởng cho thêm vật liệu và người chơi này cũng làm thám hiểm. Không có dấu hiệu "farm": thưởng là một lần cho mỗi nhiệm vụ và mỗi mốc.
4. **Khai thác bán lại vẫn bị chặn.** StarterTrader và BreedTrader chỉ tiêu 19–40%, dừng ở 2 dự án, không có vật liệu và không làm nhiệm vụ. Số dư cao (1,7–5 M) là vàng chưa tiêu, không phải lợi thế tiến trình.

## Mở thế giới

| Người chơi | Thủy Vực (ngày trung vị, % seed) | Thiên Giới |
|---|---|---|
| Researcher 5 phút/ngày | 10 (100%) | 36 (75%) |
| Researcher 3 × 10 phút/ngày | 8,3 (100%) | 13,2 (100%) |
| Researcher 60 phút/ngày | 5 (100%) | 10 (75%) |
| Relaxed / Collector / Breeder | — (0%) | — |

- Researcher mở Thiên Giới ở 75–100% seed, cùng mức với kết quả U2.10 (90–95% sau khi sửa mô phỏng).
- Ba kiểu người chơi còn lại **không bao giờ mở Thủy Vực**. Đây là giới hạn của mô phỏng chứ không phải của luật: các policy này không bao giờ mở hồ sơ sinh học, nên không đạt `Q_ORIGIN_FIELD_NOTES`. Người chơi thật mở Codex sẽ đi tiếp. Đề xuất: UI nên gợi ý "Xem hồ sơ" khi nhiệm vụ Field Notes là bước còn thiếu.

## Nhiệm vụ

- Relaxed hoàn thành 4 nhiệm vụ không cần nghiên cứu: First Friends (ngày 1,5–2,5), Forest Survey (3–5,5), At Home (4,5–5), Pathfinder (4–9,5).
- Researcher hoàn thành 13–15 trên 17 nhiệm vụ.
  - `Q_RESEARCH_ETHOLOGIST` ở lịch 60 phút/ngày chỉ đạt 25%. Lý do là giới hạn 2 phát hiện mỗi loài mỗi ngày: chơi dài trong một ngày không giúp ích.
  - `Q_RESEARCH_TIDE_JOURNAL` khá muộn (ngày 12–40).
- **Không seed nào đạt `Q_SOCIAL_CLOSE_BOND`.** Policy Researcher không ấp trứng, nên không có cặp cha mẹ và con. Nhiệm vụ này cần lai tạo (xem WORLD_PROGRESSION mục 2).
- `Q_EXPEDITION_CLOUD` ở 0% như dự kiến, vì bảng thưởng `sky_cloud` chưa được duyệt.
- Cột "Cặp bạn bè" của Collector và Breeder lên hàng nghìn vì đàn rồng lớn và có nhiều họ hàng. Quan hệ chỉ để hiển thị nên không ảnh hưởng kinh tế. `MaxPairs = 600` chỉ giới hạn số cặp có thời gian sống chung được lưu trong save.

## Bảng đầy đủ

<!-- Sinh bởi U211EconomyReviewTests.Write_u211_economy_review_when_requested; seeds = 4, biến thể live -->

| Người chơi | Lịch | Số dư 30 ng | Số dư 90 ng (P10 / trung vị / P90) | Tiêu / nguồn 90 ng | Dự án 90 ng | Gỗ + hạt cho dự án | Vật liệu giao nhiệm vụ | Vật liệu thưởng mốc | Vật liệu còn giữ | Chuyến thám hiểm | Nhiệm vụ | Danh hiệu | Cặp bạn bè | Thủy Vực (ngày, % seed) | Thiên Giới (ngày, % seed) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Relaxed | 5 phút/ngày | 413,476 | 1.60 M / 2.01 M / 3.92 M | 0.88 | 22 | 116 | 5 | 30 | 164 | 84.5 | 4 | 5 | 17 | — (0%) | — (0%) |
| Relaxed | 3 × 10 phút/ngày | 298,972 | 894,931 / 1.32 M / 2.01 M | 0.93 | 22 | 116 | 5 | 30 | 684 | 258 | 4 | 5 | 18 | — (0%) | — (0%) |
| Relaxed | 60 phút/ngày | 250,362 | 944,108 / 1.99 M / 2.85 M | 0.91 | 23 | 126 | 5 | 30 | 928 | 347 | 4 | 5 | 26 | — (0%) | — (0%) |
| Collector | 5 phút/ngày | 402,158 | 1.06 M / 2.17 M / 3.53 M | 0.87 | 22.5 | 121 | 5 | 30 | 158 | 86 | 5 | 5 | 4,161 | — (0%) | — (0%) |
| Collector | 3 × 10 phút/ngày | 340,252 | 2.22 M / 2.90 M / 3.94 M | 0.89 | 24 | 136 | 5 | 30 | 688 | 267 | 5 | 5 | 15,770 | — (0%) | — (0%) |
| Collector | 60 phút/ngày | 410,546 | 2.28 M / 3.93 M / 4.78 M | 0.85 | 24 | 136 | 5 | 30 | 964 | 357 | 5 | 5 | 17,214 | — (0%) | — (0%) |
| Breeder | 5 phút/ngày | 404,238 | 936,491 / 2.15 M / 3.30 M | 0.87 | 22 | 116 | 5 | 30 | 163 | 86 | 5 | 5 | 4,812 | — (0%) | — (0%) |
| Breeder | 3 × 10 phút/ngày | 325,148 | 2.01 M / 2.83 M / 2.93 M | 0.87 | 23.5 | 131 | 5 | 30 | 706 | 266 | 5 | 5 | 21,111 | — (0%) | — (0%) |
| Breeder | 60 phút/ngày | 254,914 | 1.34 M / 1.51 M / 2.38 M | 0.90 | 22 | 116 | 5 | 30 | 983 | 357 | 5 | 5 | 16,843 | — (0%) | — (0%) |
| Researcher | 5 phút/ngày | 1.19 M | 3.24 M / 4.36 M / 4.66 M | 0.84 | 24.5 | 141 | 26 | 48 | 134 | 83.5 | 14 | 13 | 23 | 10 (100%) | 36 (75%) |
| Researcher | 3 × 10 phút/ngày | 592,388 | 2.54 M / 3.76 M / 5.23 M | 0.88 | 25 | 146 | 26 | 60 | 669 | 257 | 15 | 14.5 | 33 | 8.3 (100%) | 13.2 (100%) |
| Researcher | 60 phút/ngày | 324,754 | 1.58 M / 2.08 M / 5.00 M | 0.89 | 25 | 146 | 26 | 60 | 493 | 198 | 13 | 13 | 27.5 | 5 (100%) | 10 (75%) |
| StarterTrader | 5 phút/ngày | 726,182 | 2.12 M / 2.30 M / 2.91 M | 0.34 | 2 | 0 | 0 | 0 | 0 | 0 | 0 | 2 | 4.5 | — (0%) | — (0%) |
| StarterTrader | 3 × 10 phút/ngày | 404,850 | 3.03 M / 4.14 M / 4.95 M | 0.23 | 2 | 0 | 0 | 0 | 0 | 0 | 0 | 2 | 4 | — (0%) | — (0%) |
| StarterTrader | 60 phút/ngày | 367,484 | 2.89 M / 3.69 M / 4.66 M | 0.25 | 2 | 0 | 0 | 0 | 0 | 0 | 0 | 2 | 4 | — (0%) | — (0%) |
| BreedTrader | 5 phút/ngày | 484,322 | 1.34 M / 1.72 M / 2.11 M | 0.40 | 2 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 27 | — (0%) | — (0%) |
| BreedTrader | 3 × 10 phút/ngày | 292,244 | 3.04 M / 3.80 M / 4.32 M | 0.24 | 2 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 27 | — (0%) | — (0%) |
| BreedTrader | 60 phút/ngày | 511,918 | 4.91 M / 5.02 M / 5.05 M | 0.19 | 2 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 27 | — (0%) | — (0%) |

### Nhiệm vụ theo thời gian

#### Relaxed — 5 phút/ngày

| Nhiệm vụ | % seed nhận trong 90 ngày | Ngày nhận (trung vị) |
|---|---|---|
| `Q_ORIGIN_FIELD_NOTES` | 0% | — |
| `Q_ORIGIN_FOREST_SURVEY` | 100% | 5.5 |
| `QUEST_AQUATIC_EXPLORATION` | 0% | — |
| `QUEST_SKY_EXPLORATION` | 0% | — |
| `Q_RESEARCH_NIGHT_WATCH` | 0% | — |
| `Q_RESEARCH_PERSONALITIES` | 0% | — |
| `Q_RESEARCH_ETHOLOGIST` | 0% | — |
| `Q_RESEARCH_TIDE_JOURNAL` | 0% | — |
| `Q_RESEARCH_SKY_CHRONICLE` | 0% | — |
| `Q_CONSERVE_AT_HOME` | 100% | 5 |
| `Q_CONSERVE_TIDES` | 0% | — |
| `Q_CONSERVE_SKIES` | 0% | — |
| `Q_SOCIAL_FIRST_FRIENDS` | 100% | 2.5 |
| `Q_SOCIAL_CLOSE_BOND` | 0% | — |
| `Q_EXPEDITION_PATHFINDER` | 100% | 9.5 |
| `Q_EXPEDITION_REEF` | 0% | — |
| `Q_EXPEDITION_CLOUD` | 0% | — |

#### Relaxed — 3 × 10 phút/ngày

| Nhiệm vụ | % seed nhận trong 90 ngày | Ngày nhận (trung vị) |
|---|---|---|
| `Q_ORIGIN_FIELD_NOTES` | 0% | — |
| `Q_ORIGIN_FOREST_SURVEY` | 100% | 3.8 |
| `QUEST_AQUATIC_EXPLORATION` | 0% | — |
| `QUEST_SKY_EXPLORATION` | 0% | — |
| `Q_RESEARCH_NIGHT_WATCH` | 0% | — |
| `Q_RESEARCH_PERSONALITIES` | 0% | — |
| `Q_RESEARCH_ETHOLOGIST` | 0% | — |
| `Q_RESEARCH_TIDE_JOURNAL` | 0% | — |
| `Q_RESEARCH_SKY_CHRONICLE` | 0% | — |
| `Q_CONSERVE_AT_HOME` | 100% | 5.2 |
| `Q_CONSERVE_TIDES` | 0% | — |
| `Q_CONSERVE_SKIES` | 0% | — |
| `Q_SOCIAL_FIRST_FRIENDS` | 100% | 1.5 |
| `Q_SOCIAL_CLOSE_BOND` | 0% | — |
| `Q_EXPEDITION_PATHFINDER` | 100% | 5 |
| `Q_EXPEDITION_REEF` | 0% | — |
| `Q_EXPEDITION_CLOUD` | 0% | — |

#### Relaxed — 60 phút/ngày

| Nhiệm vụ | % seed nhận trong 90 ngày | Ngày nhận (trung vị) |
|---|---|---|
| `Q_ORIGIN_FIELD_NOTES` | 0% | — |
| `Q_ORIGIN_FOREST_SURVEY` | 100% | 3 |
| `QUEST_AQUATIC_EXPLORATION` | 0% | — |
| `QUEST_SKY_EXPLORATION` | 0% | — |
| `Q_RESEARCH_NIGHT_WATCH` | 0% | — |
| `Q_RESEARCH_PERSONALITIES` | 0% | — |
| `Q_RESEARCH_ETHOLOGIST` | 0% | — |
| `Q_RESEARCH_TIDE_JOURNAL` | 0% | — |
| `Q_RESEARCH_SKY_CHRONICLE` | 0% | — |
| `Q_CONSERVE_AT_HOME` | 100% | 4.5 |
| `Q_CONSERVE_TIDES` | 0% | — |
| `Q_CONSERVE_SKIES` | 0% | — |
| `Q_SOCIAL_FIRST_FRIENDS` | 100% | 1.5 |
| `Q_SOCIAL_CLOSE_BOND` | 0% | — |
| `Q_EXPEDITION_PATHFINDER` | 100% | 4 |
| `Q_EXPEDITION_REEF` | 0% | — |
| `Q_EXPEDITION_CLOUD` | 0% | — |

#### Researcher — 5 phút/ngày

| Nhiệm vụ | % seed nhận trong 90 ngày | Ngày nhận (trung vị) |
|---|---|---|
| `Q_ORIGIN_FIELD_NOTES` | 100% | 1 |
| `Q_ORIGIN_FOREST_SURVEY` | 100% | 6.5 |
| `QUEST_AQUATIC_EXPLORATION` | 100% | 10 |
| `QUEST_SKY_EXPLORATION` | 75% | 36 |
| `Q_RESEARCH_NIGHT_WATCH` | 100% | 4 |
| `Q_RESEARCH_PERSONALITIES` | 100% | 3 |
| `Q_RESEARCH_ETHOLOGIST` | 75% | 17 |
| `Q_RESEARCH_TIDE_JOURNAL` | 100% | 40 |
| `Q_RESEARCH_SKY_CHRONICLE` | 75% | 37 |
| `Q_CONSERVE_AT_HOME` | 100% | 4.5 |
| `Q_CONSERVE_TIDES` | 100% | 13.5 |
| `Q_CONSERVE_SKIES` | 0% | — |
| `Q_SOCIAL_FIRST_FRIENDS` | 100% | 2.5 |
| `Q_SOCIAL_CLOSE_BOND` | 0% | — |
| `Q_EXPEDITION_PATHFINDER` | 100% | 10.5 |
| `Q_EXPEDITION_REEF` | 100% | 15.5 |
| `Q_EXPEDITION_CLOUD` | 0% | — |

#### Researcher — 3 × 10 phút/ngày

| Nhiệm vụ | % seed nhận trong 90 ngày | Ngày nhận (trung vị) |
|---|---|---|
| `Q_ORIGIN_FIELD_NOTES` | 100% | 0.2 |
| `Q_ORIGIN_FOREST_SURVEY` | 100% | 4.2 |
| `QUEST_AQUATIC_EXPLORATION` | 100% | 8.3 |
| `QUEST_SKY_EXPLORATION` | 100% | 13.2 |
| `Q_RESEARCH_NIGHT_WATCH` | 100% | 4.5 |
| `Q_RESEARCH_PERSONALITIES` | 100% | 1.2 |
| `Q_RESEARCH_ETHOLOGIST` | 100% | 10.7 |
| `Q_RESEARCH_TIDE_JOURNAL` | 75% | 33 |
| `Q_RESEARCH_SKY_CHRONICLE` | 100% | 14.8 |
| `Q_CONSERVE_AT_HOME` | 100% | 2.7 |
| `Q_CONSERVE_TIDES` | 100% | 9.2 |
| `Q_CONSERVE_SKIES` | 100% | 31.7 |
| `Q_SOCIAL_FIRST_FRIENDS` | 100% | 1.5 |
| `Q_SOCIAL_CLOSE_BOND` | 0% | — |
| `Q_EXPEDITION_PATHFINDER` | 100% | 5.5 |
| `Q_EXPEDITION_REEF` | 100% | 10.7 |
| `Q_EXPEDITION_CLOUD` | 0% | — |

#### Researcher — 60 phút/ngày

| Nhiệm vụ | % seed nhận trong 90 ngày | Ngày nhận (trung vị) |
|---|---|---|
| `Q_ORIGIN_FIELD_NOTES` | 100% | 0 |
| `Q_ORIGIN_FOREST_SURVEY` | 100% | 4 |
| `QUEST_AQUATIC_EXPLORATION` | 100% | 5 |
| `QUEST_SKY_EXPLORATION` | 75% | 10 |
| `Q_RESEARCH_NIGHT_WATCH` | 100% | 2 |
| `Q_RESEARCH_PERSONALITIES` | 100% | 1 |
| `Q_RESEARCH_ETHOLOGIST` | 25% | 19 |
| `Q_RESEARCH_TIDE_JOURNAL` | 75% | 12 |
| `Q_RESEARCH_SKY_CHRONICLE` | 75% | 14 |
| `Q_CONSERVE_AT_HOME` | 100% | 2.5 |
| `Q_CONSERVE_TIDES` | 100% | 7.5 |
| `Q_CONSERVE_SKIES` | 50% | 26.5 |
| `Q_SOCIAL_FIRST_FRIENDS` | 100% | 1.5 |
| `Q_SOCIAL_CLOSE_BOND` | 0% | — |
| `Q_EXPEDITION_PATHFINDER` | 100% | 5 |
| `Q_EXPEDITION_REEF` | 100% | 8.5 |
| `Q_EXPEDITION_CLOUD` | 0% | — |

