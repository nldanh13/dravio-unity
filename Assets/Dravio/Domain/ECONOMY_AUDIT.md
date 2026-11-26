# Dravio — Kiểm toán cân bằng kinh tế (U2.7)

> **Chỉ đề xuất. Không thông số gốc nào bị thay đổi.** Mọi giá, tỷ lệ và công thức dùng đúng GDD 3.1 / TS b33ec7a. Dravio **không có quảng cáo** (quyết định của chủ dự án).

## 1. Phương pháp

- **Mô phỏng tất định** qua `CommandDispatcher` thật (`Tests/Domain/Simulation/EconomySimulator.cs`).
  - Mỗi seed là một `saveId` khác, nên có một luồng RNG giao dịch riêng (roll gói, lai, biến thể).
  - 100 seed cho mỗi tổ hợp (chiến lược × lịch chơi). Ghi trung vị, kèm P10/P90 cho số dư.
- **Lịch chơi** (giờ bắt đầu tính từ 09:00):
  - **5 phút/ngày**: một phiên.
  - **3 × 10 phút/ngày**: 09:00, 13:00, 20:00.
  - **60 phút/ngày**: 09:00 và 20:00, mỗi phiên 30 phút.
  - Giữa các phiên là offline (75 %, tối đa 24 h). Trong phiên, quyết toán online và ra quyết định mỗi 30 s.
- **Chiến lược**:
  - **Idle**: không mua, không bán. Chỉ để làm mốc so sánh.
  - **Collector**: người chơi sưu tầm thông thường. Mỗi phiên mua tối đa một gói (gói đắt nhất đủ tiền), lai khi có cặp, **không bao giờ bán**.
  - **StarterTrader**: kiểm tra khai thác. Mua Starter liên tục, nuôi trong kho tới Adult rồi bán.
  - **BreedTrader**: kiểm tra khai thác. Lai liên tục cặp rẻ nhất, bán con khi Adult.
  - Mọi chiến lược đều giữ 5 rồng có rate cao nhất trong khu sinh vàng.
- **Giới hạn mô hình**:
  - Không mô phỏng cho ăn. Cho ăn chỉ cho XP, nên thời gian trưởng thành trong mô phỏng là cận trên.
  - Không mô phỏng gõ tổ.
  - Chỉ có khu Khởi Nguyên (Ocean/Sky còn khóa ở G1), nên **tối đa 5 rồng sinh vàng**.
  - Chưa có hệ thiên tai/chăm sóc, thám hiểm, vật liệu.
  - Mô phỏng (dòng 32) yêu cầu ≥ 1 000 seed/chiến lược; báo cáo này dùng 100 seed. Có thể chạy lại với `DRAVIO_SIM_SEEDS=1000`.

Cách chạy lại:

```bash
DRAVIO_SIM_OUT=/tmp/sim.md DRAVIO_SIM_SEEDS=100 dotnet test "Assets/Dravio/Tests/Domain/DotNet~/Dravio.Domain.Tests.csproj" --filter "FullyQualifiedName~Write_economy_report"
```

## 2. Phân tích giải tích (giá trị kỳ vọng, đúng catalog)

E[hệ số giá bán theo biến thể] ở luck 0 = 0,85·1 + 0,11·2,2 + 0,035·4,5 + 0,005·10 = **1,2995**; ở luck 8 = **1,6912**.

| Gói | Giá | EV giá bán khi Adult (luck 0) | Tỷ lệ EV/giá | luck 8 |
|---|---|---|---|---|
| Starter | 100 G | **141,3 G** | **1,41** | 1,84 |
| Elemental | 900 G | 443,2 G | 0,49 | 0,64 |
| Mystic | 6 000 G | 1 460,6 G | 0,24 | 0,32 |
| Astral | 30 000 G | 3 815,2 G | 0,13 | 0,17 |

- Starter bán lúc Baby chỉ được EV 49,4 G, lúc Juvenile 91,8 G. Lãi chỉ xuất hiện khi chờ tới Adult: Common mất 5–8 phút, Rare 25–35 phút.
- **Lai Emberling × Sprout**: phí 80 G. Con là Emberling/Sprout/Magma, mỗi loài 1/3, nên EV giá bán khi Adult = (75 + 80 + 380)/3 × 1,2995 = **231,7 G** (≈ 2,9 lần phí).
  - Thời gian một vòng ≈ thời gian ấp (5 hoặc 15 phút) + cooldown 10 phút.
  - Rồng con lớn trong kho (XP 100 % với mọi rồng chưa thả, kể cả trong kho).
- **Đối chiếu sản xuất**: 5 Emberling Adult sinh 5 × 0,025 = 0,125 G/s = 450 G/giờ online, hoặc 337,5 G/giờ offline (75 %).

## 3. Kết quả mô phỏng (100 seed, trung vị trừ khi ghi khác)

Tóm tắt, mốc 30 ngày:

| Chiến lược | 5 phút/ngày | 3 × 10 phút/ngày | 60 phút/ngày |
|---|---|---|---|
| Idle — số dư | 49 197 G | 49 467 G | 49 810 G |
| Collector — số dư (P10–P90) | 2,29 M (1,41–3,43 M) | 2,05 M (1,18–3,74 M) | 2,12 M (1,27–3,49 M) |
| Collector — vàng sản xuất | 3,19 M | 4,96 M | 4,51 M |
| StarterTrader — số dư | 1,01 M | 1,96 M | 2,38 M |
| BreedTrader — số dư | 0,69 M | 1,05 M | 1,18 M |

Thời gian lần đầu đủ tiền mua gói (giờ đồng hồ thật, trung vị):

| Chiến lược | Starter 100 | Elemental 900 | Mystic 6 000 | Astral 30 000 |
|---|---|---|---|---|
| Idle | 0 | 11–24 | 96 | 432–456 (≈ 18–19 ngày) |
| Collector | 0 | 11–24 | 24–48 | 72–96 |
| StarterTrader | 0 | 4–24 | 11–48 | 35–72 |
| BreedTrader | 0 | 11–24 | 24–48 | 59–120 |

Bảng chi tiết theo mốc 1 giờ / 24 giờ / 7 ngày / 30 ngày:

### Idle — 5 phút/ngày

| Mốc | Số dư G (P10 / trung vị / P90) | Vàng sản xuất G (trung vị) | Bán G | Chi gói G | Chi lai G | Thưởng G | G/s cuối | Rồng | Loài | Ô codex | Thành tựu |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 giờ | 316 / 316 / 316 | 66 | 0 | 0 | 0 | 100 | 0.03 | 1 | 1 | 1 | 1 |
| 24 giờ | 1,868 / 1,868 / 1,868 | 1,618 | 0 | 0 | 0 | 100 | 0.03 | 1 | 1 | 1 | 1 |
| 7 ngày | 11,899 / 11,899 / 11,899 | 11,349 | 0 | 0 | 0 | 400 | 0.03 | 1 | 1 | 1 | 2 |
| 30 ngày | 49,197 / 49,197 / 49,197 | 48,647 | 0 | 0 | 0 | 400 | 0.03 | 1 | 1 | 1 | 2 |

Lần đầu đủ tiền (giờ, trung vị; tỷ lệ seed đạt trong 30 ngày): starter 100 G: 0 h (100%) · elemental 900 G: 24 h (100%) · mystic 6,000 G: 96 h (100%) · astral 30,000 G: 456 h (100%)

### Idle — 3 × 10 phút/ngày

| Mốc | Số dư G (P10 / trung vị / P90) | Vàng sản xuất G (trung vị) | Bán G | Chi gói G | Chi lai G | Thưởng G | G/s cuối | Rồng | Loài | Ô codex | Thành tựu |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 giờ | 318 / 318 / 318 | 68 | 0 | 0 | 0 | 100 | 0.03 | 1 | 1 | 1 | 1 |
| 24 giờ | 1,877 / 1,877 / 1,877 | 1,627 | 0 | 0 | 0 | 100 | 0.03 | 1 | 1 | 1 | 1 |
| 7 ngày | 11,962 / 11,962 / 11,962 | 11,412 | 0 | 0 | 0 | 400 | 0.03 | 1 | 1 | 1 | 2 |
| 30 ngày | 49,467 / 49,467 / 49,467 | 48,917 | 0 | 0 | 0 | 400 | 0.03 | 1 | 1 | 1 | 2 |

Lần đầu đủ tiền (giờ, trung vị; tỷ lệ seed đạt trong 30 ngày): starter 100 G: 0 h (100%) · elemental 900 G: 11 h (100%) · mystic 6,000 G: 96 h (100%) · astral 30,000 G: 436 h (100%)

### Idle — 60 phút/ngày

| Mốc | Số dư G (P10 / trung vị / P90) | Vàng sản xuất G (trung vị) | Bán G | Chi gói G | Chi lai G | Thưởng G | G/s cuối | Rồng | Loài | Ô codex | Thành tựu |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 giờ | 325 / 325 / 325 | 75 | 0 | 0 | 0 | 100 | 0.03 | 1 | 1 | 1 | 1 |
| 24 giờ | 1,889 / 1,889 / 1,889 | 1,639 | 0 | 0 | 0 | 100 | 0.03 | 1 | 1 | 1 | 1 |
| 7 ngày | 12,042 / 12,042 / 12,042 | 11,492 | 0 | 0 | 0 | 400 | 0.03 | 1 | 1 | 1 | 2 |
| 30 ngày | 49,810 / 49,810 / 49,810 | 49,260 | 0 | 0 | 0 | 400 | 0.03 | 1 | 1 | 1 | 2 |

Lần đầu đủ tiền (giờ, trung vị; tỷ lệ seed đạt trong 30 ngày): starter 100 G: 0 h (100%) · elemental 900 G: 11 h (100%) · mystic 6,000 G: 96 h (100%) · astral 30,000 G: 432 h (100%)

### Collector — 5 phút/ngày

| Mốc | Số dư G (P10 / trung vị / P90) | Vàng sản xuất G (trung vị) | Bán G | Chi gói G | Chi lai G | Thưởng G | G/s cuối | Rồng | Loài | Ô codex | Thành tựu |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 giờ | 301 / 306 / 441 | 206 | 0 | 200 | 0 | 150 | 0.08 | 3 | 2.5 | 3 | 2 |
| 24 giờ | 5,083 / 5,212 / 6,369 | 5,112 | 0 | 200 | 0 | 150 | 0.08 | 3 | 2.5 | 3 | 2 |
| 7 ngày | 34,107 / 68,772 / 191,849 | 175,664 | 0 | 103,100 | 5,010 | 1,000 | 0.79 | 14 | 8 | 9 | 6 |
| 30 ngày | 1,414,543 / 2,288,799 / 3,427,036 | 3,193,398 | 0 | 793,100 | 112,325 | 5,500 | 2.68 | 60 | 12 | 19 | 8 |

Lần đầu đủ tiền (giờ, trung vị; tỷ lệ seed đạt trong 30 ngày): starter 100 G: 0 h (100%) · elemental 900 G: 24 h (100%) · mystic 6,000 G: 48 h (100%) · astral 30,000 G: 96 h (100%)

### Collector — 3 × 10 phút/ngày

| Mốc | Số dư G (P10 / trung vị / P90) | Vàng sản xuất G (trung vị) | Bán G | Chi gói G | Chi lai G | Thưởng G | G/s cuối | Rồng | Loài | Ô codex | Thành tựu |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 giờ | 191 / 218 / 349 | 198 | 0 | 200 | 80 | 150 | 0.08 | 3 | 2.5 | 3 | 2 |
| 24 giờ | 6,960 / 10,196 / 14,947 | 11,651 | 0 | 2,000 | 240 | 600 | 0.22 | 8 | 4 | 5 | 4 |
| 7 ngày | 39,516 / 63,049 / 191,295 | 404,232 | 0 | 278,000 | 52,230 | 5,500 | 1.7 | 54 | 12 | 18 | 8 |
| 30 ngày | 1,178,151 / 2,046,556 / 3,736,488 | 4,964,242 | 0 | 2,342,900 | 586,110 | 6,500 | 3.83 | 234 | 14 | 31 | 9 |

Lần đầu đủ tiền (giờ, trung vị; tỷ lệ seed đạt trong 30 ngày): starter 100 G: 0 h (100%) · elemental 900 G: 11 h (100%) · mystic 6,000 G: 24 h (100%) · astral 30,000 G: 72 h (100%)

### Collector — 60 phút/ngày

| Mốc | Số dư G (P10 / trung vị / P90) | Vàng sản xuất G (trung vị) | Bán G | Chi gói G | Chi lai G | Thưởng G | G/s cuối | Rồng | Loài | Ô codex | Thành tựu |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 giờ | 325 / 438 / 575 | 290 | 0 | 200 | 160 | 300 | 0.13 | 5 | 3 | 3 | 3 |
| 24 giờ | 7,627 / 10,388 / 13,747 | 11,206 | 0 | 1,100 | 320 | 700 | 0.21 | 7.5 | 4 | 4 | 5 |
| 7 ngày | 38,793 / 67,873 / 209,213 | 368,734 | 0 | 241,100 | 55,085 | 5,500 | 1.47 | 58 | 12 | 17.5 | 8 |
| 30 ngày | 1,265,951 / 2,121,552 / 3,494,069 | 4,507,732 | 0 | 1,621,100 | 747,435 | 6,500 | 3.33 | 260 | 14 | 31 | 9 |

Lần đầu đủ tiền (giờ, trung vị; tỷ lệ seed đạt trong 30 ngày): starter 100 G: 0 h (100%) · elemental 900 G: 11 h (100%) · mystic 6,000 G: 24 h (100%) · astral 30,000 G: 72 h (100%)

### StarterTrader — 5 phút/ngày

| Mốc | Số dư G (P10 / trung vị / P90) | Vàng sản xuất G (trung vị) | Bán G | Chi gói G | Chi lai G | Thưởng G | G/s cuối | Rồng | Loài | Ô codex | Thành tựu |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 giờ | 298 / 308 / 502 | 208 | 0 | 200 | 0 | 150 | 0.08 | 3 | 3 | 3 | 2 |
| 24 giờ | 4,955 / 5,276 / 11,222 | 5,176 | 0 | 200 | 0 | 150 | 0.08 | 3 | 3 | 3 | 2 |
| 7 ngày | 118,684 / 147,815 / 182,280 | 145,859 | 5,219 | 6,200 | 0 | 850 | 0.43 | 14.5 | 8 | 13 | 5 |
| 30 ngày | 859,490 / 1,012,436 / 1,174,613 | 1,001,805 | 35,128 | 29,200 | 0 | 4,350 | 0.66 | 15 | 10 | 22 | 7 |

Lần đầu đủ tiền (giờ, trung vị; tỷ lệ seed đạt trong 30 ngày): starter 100 G: 0 h (100%) · elemental 900 G: 24 h (100%) · mystic 6,000 G: 48 h (100%) · astral 30,000 G: 72 h (100%)

### StarterTrader — 3 × 10 phút/ngày

| Mốc | Số dư G (P10 / trung vị / P90) | Vàng sản xuất G (trung vị) | Bán G | Chi gói G | Chi lai G | Thưởng G | G/s cuối | Rồng | Loài | Ô codex | Thành tựu |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 giờ | 267 / 364 / 528 | 364 | 0 | 300 | 0 | 150 | 0.15 | 4 | 3 | 3 | 2 |
| 24 giờ | 15,375 / 20,872 / 26,840 | 21,388 | 2,600 | 4,300 | 0 | 850 | 0.39 | 18 | 7 | 11.5 | 5 |
| 7 ngày | 228,132 / 287,881 / 375,185 | 274,174 | 50,182 | 40,300 | 0 | 4,350 | 0.75 | 19 | 10 | 25 | 7 |
| 30 ngày | 1,652,688 / 1,956,042 / 2,371,231 | 1,883,212 | 244,068 | 178,300 | 0 | 7,350 | 1.25 | 19 | 10 | 33 | 8 |

Lần đầu đủ tiền (giờ, trung vị; tỷ lệ seed đạt trong 30 ngày): starter 100 G: 0 h (100%) · elemental 900 G: 4 h (100%) · mystic 6,000 G: 11 h (100%) · astral 30,000 G: 35 h (100%)

### StarterTrader — 60 phút/ngày

| Mốc | Số dư G (P10 / trung vị / P90) | Vàng sản xuất G (trung vị) | Bán G | Chi gói G | Chi lai G | Thưởng G | G/s cuối | Rồng | Loài | Ô codex | Thành tựu |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 giờ | 241 / 298 / 512 | 452 | 75 | 650 | 0 | 250 | 0.18 | 6 | 4 | 5 | 3 |
| 24 giờ | 16,405 / 19,716 / 26,993 | 20,580 | 4,592 | 6,650 | 0 | 850 | 0.44 | 19 | 8 | 14 | 5 |
| 7 ngày | 300,171 / 356,424 / 448,880 | 328,148 | 101,564 | 78,650 | 0 | 4,350 | 0.93 | 23 | 10 | 29 | 7 |
| 30 ngày | 1,982,474 / 2,382,580 / 2,847,927 | 2,242,284 | 492,090 | 354,650 | 0 | 7,350 | 1.47 | 23 | 10 | 36 | 8 |

Lần đầu đủ tiền (giờ, trung vị; tỷ lệ seed đạt trong 30 ngày): starter 100 G: 0 h (100%) · elemental 900 G: 11 h (100%) · mystic 6,000 G: 24 h (100%) · astral 30,000 G: 35 h (100%)

### BreedTrader — 5 phút/ngày

| Mốc | Số dư G (P10 / trung vị / P90) | Vàng sản xuất G (trung vị) | Bán G | Chi gói G | Chi lai G | Thưởng G | G/s cuối | Rồng | Loài | Ô codex | Thành tựu |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 giờ | 336 / 336 / 336 | 136 | 0 | 100 | 0 | 150 | 0.05 | 2 | 2 | 2 | 2 |
| 24 giờ | 3,565 / 3,565 / 3,565 | 3,365 | 0 | 100 | 0 | 150 | 0.05 | 2 | 2 | 2 | 2 |
| 7 ngày | 48,833 / 66,212 / 86,876 | 65,850 | 0 | 100 | 480 | 700 | 0.24 | 7 | 3 | 3.5 | 5 |
| 30 ngày | 563,282 / 689,265 / 884,638 | 684,470 | 3,249 | 100 | 2,320 | 3,500 | 0.47 | 8 | 3 | 6 | 7 |

Lần đầu đủ tiền (giờ, trung vị; tỷ lệ seed đạt trong 30 ngày): starter 100 G: 0 h (100%) · elemental 900 G: 24 h (100%) · mystic 6,000 G: 48 h (100%) · astral 30,000 G: 120 h (100%)

### BreedTrader — 3 × 10 phút/ngày

| Mốc | Số dư G (P10 / trung vị / P90) | Vàng sản xuất G (trung vị) | Bán G | Chi gói G | Chi lai G | Thưởng G | G/s cuối | Rồng | Loài | Ô codex | Thành tựu |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 giờ | 223 / 247 / 247 | 127 | 0 | 100 | 80 | 150 | 0.05 | 2 | 2 | 2 | 2 |
| 24 giờ | 6,326 / 8,334 / 12,499 | 8,224 | 0 | 100 | 240 | 300 | 0.15 | 5 | 3 | 3 | 3 |
| 7 ngày | 119,301 / 150,633 / 203,486 | 149,340 | 1,964 | 100 | 1,680 | 1,000 | 0.42 | 8 | 3 | 5 | 6 |
| 30 ngày | 840,936 / 1,050,109 / 1,296,127 | 1,037,778 | 15,224 | 100 | 7,200 | 3,500 | 0.7 | 8 | 3 | 8 | 7 |

Lần đầu đủ tiền (giờ, trung vị; tỷ lệ seed đạt trong 30 ngày): starter 100 G: 0 h (100%) · elemental 900 G: 11 h (100%) · mystic 6,000 G: 24 h (100%) · astral 30,000 G: 59 h (100%)

### BreedTrader — 60 phút/ngày

| Mốc | Số dư G (P10 / trung vị / P90) | Vàng sản xuất G (trung vị) | Bán G | Chi gói G | Chi lai G | Thưởng G | G/s cuối | Rồng | Loài | Ô codex | Thành tựu |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 giờ | 348 / 409 / 565 | 215 | 0 | 100 | 160 | 300 | 0.11 | 4 | 2 | 2 | 3 |
| 24 giờ | 7,663 / 9,368 / 13,829 | 9,338 | 0 | 100 | 320 | 300 | 0.18 | 5 | 3 | 3 | 3 |
| 7 ngày | 134,726 / 161,018 / 222,047 | 159,456 | 3,006 | 100 | 2,240 | 1,000 | 0.47 | 7 | 3 | 6 | 6 |
| 30 ngày | 926,991 / 1,176,302 / 1,551,429 | 1,160,412 | 21,768 | 100 | 9,600 | 3,500 | 0.78 | 8 | 3 | 9 | 7 |

Lần đầu đủ tiền (giờ, trung vị; tỷ lệ seed đạt trong 30 ngày): starter 100 G: 0 h (100%) · elemental 900 G: 11 h (100%) · mystic 6,000 G: 24 h (100%) · astral 30,000 G: 59 h (100%)


## 4. Đánh giá

### 4.1 Tốc độ tích lũy vàng

- **Đầu game (24 giờ đầu)**:
  - Idle chỉ có Emberling khởi đầu, nên sinh ≈ 1 620–1 640 G/ngày; gần như toàn bộ là receipt offline 75 %.
  - Người chơi chủ động có 5 000–11 600 G sau 24 giờ.
  - Elemental đủ tiền sau 11–24 giờ, Mystic sau 1–4 ngày.
  - Nhịp này khớp mục tiêu "mở khóa dần" của GDD.
- **Giữa và cuối game (7–30 ngày)**:
  - Vàng sản xuất tăng **gần như theo hàm mũ**: mỗi gói mua về có thể là rồng hiếm hơn, đem thay vào 5 chỗ sinh vàng.
  - Rate cuối của Collector đạt 2,7–3,8 G/s, tức ~20–30 lần Idle.
  - Trần lý thuyết là 5 × Gaia Prismatic = 13,75 G/s ≈ 890 000 G/ngày offline.
  - Đến ngày 30, số dư trung vị của mọi chiến lược chủ động là **1–2,4 triệu G**.

### 4.2 Điểm tiêu vàng (sink) và nguy cơ lạm phát — **cao**

- Tổng mọi nâng cấp tới cấp tối đa: **1 096 716 G**. Trong đó `mutationLuckLevel` 684 696, `islandCapacityLevel` 275 635.
- Đủ 6 trang trí × 3 khu: **141 150 G**.
- Toàn bộ thưởng thành tựu: 12 500 G.
- Gói trứng là sink duy nhất không giới hạn, nhưng ở cuối game Astral có EV bán chỉ 0,13 lần giá, nên người chơi không có lý do mua nữa.

Kết luận: sau khoảng 2–3 tuần, Collector đã mua được mọi thứ trong catalog P0 và vàng tiếp tục dồn lại mà không còn chỗ tiêu. Đây là lạm phát "vô nghĩa" chứ không phá kinh tế: offline-only, không có giao dịch giữa người chơi. Tuy vậy nó làm mất động lực dài hạn.

### 4.3 Vòng lặp khai thác

1. **Starter "mua – nuôi – bán" có EV dương**: EV/giá = 1,41 ở luck 0 và 1,84 ở luck 8. Lãi kỳ vọng +41 G/100 G sau 5–35 phút nuôi trong kho, không cần chỗ trong khu sinh vàng. Kho chứa tới 500 con, và rồng trong kho vẫn lớn 100 %.
   - Trong mô phỏng, phần bán (StarterTrader 60 phút/ngày: 492 000 G / 30 ngày) nhỏ hơn phần sản xuất (2,24 M). Lý do là mô hình chỉ mua 1 gói mỗi 30 s và chỉ khi đang online.
   - Một người chơi dùng macro hoặc bấm liên tục có thể biến vòng này thành nguồn vàng chính. Rủi ro **trung bình** (chỉ là game offline).
2. **Lai Common × Common có EV ≈ 2,9 lần phí** (231,7 / 80 G), một vòng 15–25 phút.
   - BreedTrader trong mô phỏng bị giới hạn bởi 1 tổ và cooldown, nên phần bán nhỏ.
   - Không phải lỗi, nhưng làm phí lai mất vai trò sink.
3. **`islandCapacityLevel` vô dụng ở G1**: Khởi Nguyên chỉ có 5 chỗ vật lý và Ocean/Sky còn khóa, nên mỗi cấp (500 → 275 635 G tổng) không tăng sản xuất. Người chơi mua sẽ thấy bị "lừa".
4. **Không tìm thấy lỗi nhân bản hay tạo vàng sai luật**:
   - Mô phỏng đi hàng triệu lệnh thật, chưa lần nào xuất hiện vàng âm, state sai validator hay rồng trùng UID.
   - Mọi thưởng chỉ một lần, đã kiểm bằng test.
   - Vàng bán không tính vào `totalGoldEarned`, nên thành tựu vàng không thể "farm" bằng mua – bán.

### 4.4 Ảnh hưởng của việc bỏ quảng cáo

GDD trang 29 có 3 phần thưởng quảng cáo mobile. Cả 3 đã bị bỏ:

| Thưởng quảng cáo (GDD) | Giá trị ước tính mất đi | Ảnh hưởng |
|---|---|---|
| ×2 vàng offline, 1 lần/ngày | Bằng vàng của 1 receipt/ngày. Idle ≈ +1 620 G/ngày (gấp đôi thu nhập Idle). 5 Emberling Adult × 8 h ≈ +2 700 G | **Lớn nhất ở đầu game**: người chơi 5 phút/ngày mất ≈ 50 % thu nhập so với người xem quảng cáo. Không đáng kể ở cuối game (≪ 1 % của 1–2 M) |
| Starter miễn phí 3/ngày | 3 × 100 G giá mua; EV bán Adult 3 × 141 ≈ 424 G/ngày, cộng 3 lần roll pity/biến thể | Vừa phải: tương đương 25–30 % thu nhập ngày đầu, chậm hoàn thành Bách khoa một chút |
| −25 % thời gian ấp còn lại, 1 lần/tổ | 1,25–45 phút mỗi lần lai | Nhỏ |

Kết luận:
- Bỏ quảng cáo làm **24–72 giờ đầu** chậm hơn rõ rệt cho người chơi ít thời gian. Elemental vẫn đủ tiền trong 11–24 giờ, Mystic trong 1–4 ngày.
- Sau ngày thứ 3, tác động không đáng kể vì sản xuất từ rồng hiếm áp đảo mọi thưởng quảng cáo cũ.
- Bỏ quảng cáo **không** tạo lỗ hổng hay vòng lặp mới.

## 5. Đề xuất (chưa áp dụng, cần chủ dự án quyết định)

1. **Starter EV/giá > 1**: chọn một trong các hướng sau rồi mô phỏng lại.
   - Giảm giá bán của rồng có nguồn gốc gói trong N phút đầu.
   - Tăng giá Starter lên ≥ 145 G.
   - Hạ hệ số bán Adult của Common từ gói.
   - Giới hạn số Starter mua/ngày.
   - Hoặc chấp nhận và ghi vào GDD như "vòng lặp nhỏ có chủ đích".
2. **Thêm điểm tiêu vàng dài hạn** để giữ giá trị vàng sau tuần thứ 2. Các hệ GDD đã liệt kê nhưng chưa có: phục hồi zone, vật liệu/chế tạo decor, nghiên cứu, mở Ocean/Sky ở G2. Cũng có thể xem lại tốc độ tăng của rate theo độ hiếm: Legendary là 22 lần Common.
3. **`islandCapacityLevel`**: ẩn hoặc khóa nâng cấp cho tới khi có hơn 5 chỗ vật lý (G2), hoặc ghi rõ "chuẩn bị cho G2" trên UI.
4. **Phí lai**: cân nhắc tăng phí Common × Common, hoặc thêm chi phí thời gian; hiện EV con ≈ 2,9 lần phí.
5. **Đầu game sau khi bỏ quảng cáo**: nếu muốn bù nhịp 24–72 giờ đầu, có thể thêm một nguồn không gắn quảng cáo (thưởng mốc, nhiệm vụ hằng ngày…). Đây là quyết định cân bằng, chưa đặt.
6. **Chạy lại với 1 000 seed và lịch chơi GDD đầy đủ** (thêm "không bán", "chỉ sưu tầm", "ưu tiên cứu hộ") khi U2.6 (cứu hộ/thiên tai) và thám hiểm có mặt.
