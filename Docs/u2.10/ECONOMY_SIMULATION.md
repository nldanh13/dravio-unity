# U2.10 — Economy Simulation 7 / 30 / 90 ngày

> Quyết định và phần chờ duyệt: [ADR 0204](../adr/0204-u2-10-research-social-progression.md) mục D (E1–E6).
> **Trạng thái:** chỉ là mô phỏng và đề xuất. **Không** đổi catalog, giá, phí hay luật production. Biến thể `proposed` chỉ tồn tại trong mã test (`EconomyVariant.ProposedU210`).
> **Nguồn:** báo cáo audit kinh tế hiện có (`EconomySimulator`, U5) và catalog G0. GDD 3.1 không có trong repo nên mọi mục tiêu cân bằng là đề xuất.

## 1. Phương pháp

- `Assets/Dravio/Tests/Domain/Simulation/EconomySimulator.cs` chạy **`CommandDispatcher` thật** trên `GameSession`: mua gói, ấp, cho ăn, lai, bán, nâng cấp, thám hiểm, mở zone. Không có công thức kinh tế viết lại riêng.
- 6 kiểu người chơi × 3 lịch chơi × 20 seed × mốc 1 / 7 / 30 / 90 ngày. Số liệu dưới là **trung vị 20 seed**, bảng đầy đủ có P10 / P90.
- Báo cáo đầy đủ từng kiểu người chơi (P10 / trung vị / P90 ở mọi mốc): [`sim/`](sim/).
- Lịch chơi: **5 phút/ngày** (một phiên lúc 09:00), **3 × 10 phút/ngày** (sáng, chiều, tối), **60 phút/ngày** (chia nhiều phiên).
- Thời gian offline dùng đúng luật offline đã duyệt (sản xuất vàng có trần offline). Không bật thiên tai, Comfort drift hay tiêu hao năng lượng.
- Biến thể:
  - `baseline` = catalog hiện tại.
  - `proposed` = E1 + E2 + E3 (giới hạn 5 gói Starter/ngày, Starter 150 G, phí lai Common 120 G). Phần chênh giá được trừ vào một **"quỹ ảo"** (cột "Quỹ ảo") thay vì đổi catalog, nên production không bị chạm.

| Kiểu người chơi | Hành vi |
|---|---|
| Relaxed | Chỉ cho ăn, ấp gói rẻ khi có chỗ, không bán, không tối ưu |
| Collector | Mua gói tốt nhất đủ tiền để lấp ô Codex, nâng cấp dần |
| Breeder | Lai để tìm biến dị, giữ con tốt, bán con trùng |
| Researcher | Xem hồ sơ, quan sát mọi rồng (có nhịp U2.10), thám hiểm bằng Adult rảnh, hoàn thành nhiệm vụ cổng theo đề xuất rồi gửi `unlockZone` thật |
| StarterTrader | Khai thác: mua Starter – nuôi – bán liên tục |
| BreedTrader | Khai thác: lai Emberling × Sprout rồi bán con |

## 2. Tóm tắt 90 ngày (baseline, trung vị)

| Người chơi | Lịch | Số dư 7 ng | 30 ng | 90 ng | Vàng sản xuất 90 ng | Bán 90 ng | G/s ở ngày 90 | Loài | Ô Codex /64 |
|---|---|---|---|---|---|---|---|---|---|
| Relaxed | 5 phút | 73,420 | 2.77 M | 13.95 M | 16.49 M | 0 | 3.78 | 12 | 20 |
| Relaxed | 3 × 10 | 67,908 | 2.42 M | 14.21 M | 16.76 M | 0 | 3.66 | 12.5 | 20.5 |
| Relaxed | 60 phút | 62,826 | 2.48 M | 13.75 M | 16.33 M | 0 | 3.71 | 13 | 20.5 |
| Collector | 5 phút | 79,412 | 2.43 M | 14.77 M | 17.74 M | 0 | 3.99 | 14 | 27.5 |
| Collector | 3 × 10 | 68,846 | 1.87 M | 16.39 M | 23.24 M | 0 | 4.88 | 14 | 37 |
| Collector | 60 phút | 65,670 | 2.03 M | 14.03 M | 19.01 M | 0 | 4.05 | 15 | 36.5 |
| Breeder | 5 phút | 62,646 | 2.04 M | 12.33 M | 15.34 M | 0 | 3.74 | 14 | 28 |
| Breeder | 3 × 10 | 104,398 | 1.86 M | 11.58 M | 15.97 M | 0 | 3.92 | 14 | 34 |
| Breeder | 60 phút | 127,684 | 2.00 M | 13.34 M | 16.74 M | 0 | 3.63 | 14 | 34 |
| Researcher | 5 phút | 46,932 | 2.14 M | 12.94 M | 15.52 M | 0 | 3.46 | 14 | 22 |
| Researcher | 3 × 10 | 64,901 | 1.78 M | 11.46 M | 14.04 M | 0 | 3.42 | 14 | 20.5 |
| Researcher | 60 phút | 69,872 | 1.83 M | 10.94 M | 13.52 M | 0 | 3.17 | 14 | 22 |
| StarterTrader | 5 phút | 145,860 | 984,901 | 4.55 M | 4.52 M | 119,060 | 1.06 | 10 | 29 |
| StarterTrader | 3 × 10 | 270,617 | 1.89 M | 7.82 M | 7.59 M | 760,734 | 1.58 | 10 | 37 |
| StarterTrader | 60 phút | 366,464 | 2.40 M | 9.47 M | 9.00 M | 1.53 M | 1.92 | 10 | 39 |
| BreedTrader | 5 phút | 65,870 | 663,578 | 3.05 M | 3.04 M | 15,010 | 0.69 | 3 | 8 |
| BreedTrader | 3 × 10 | 136,855 | 981,936 | 4.45 M | 4.41 M | 53,190 | 1.01 | 3 | 10 |
| BreedTrader | 60 phút | 163,858 | 1.14 M | 5.13 M | 5.07 M | 73,862 | 1.22 | 3 | 11 |

### Kết luận chính

1. **Sản xuất vàng tự động là nguồn vàng chính**, không phải bán. Người chơi thoải mái 5 phút/ngày có 14 M G sau 90 ngày, Collector 14–16 M, Breeder 12–13 M, đều nhiều hơn cả người khai thác 60 phút/ngày (9,5 M). Người chơi thường không bị thiệt so với người khai thác, đó là điều tốt.
2. **Không còn chỗ tiêu vàng sau khoảng 30 ngày.** Tổng chi để nâng mọi cấp lên tối đa ≈ 1,1 M G (mục 4), trong khi số dư 90 ngày 10–20 M. Đây là vấn đề lớn nhất về dài hạn (E5).
3. **Vòng Starter có lời**: EV bán / giá 1,41 (luck 0) đến 1,84 (luck 8). StarterTrader 60 phút/ngày bán 1,53 M trong 90 ngày. Không phá game (vẫn ít hơn sản xuất), nhưng là lối "bấm nhiều thì giàu" trái với chất cozy (E1, E2).
4. **Vòng lai Emberling × Sprout có lời 2,9× phí** nhưng nhỏ (bán 15–74 k trong 90 ngày) vì bị giới hạn thời gian ấp và chỗ (E3).
5. **Researcher không giàu hơn Relaxed** (nghiên cứu không tạo vàng, R7 / E6), trong khi đạt nhiều loài nhất (14) và mở thêm thế giới.

## 3. Trước / sau đề xuất (E1 + E2 + E3)

| Người chơi | Lịch | Số dư 90 ng trước | Sau | Bán 90 ng trước | Sau | Ô Codex trước | Sau |
|---|---|---|---|---|---|---|---|
| StarterTrader | 5 phút | 4.55 M | 3.96 M | 119,060 | 58,939 | 29 | 25.5 |
| StarterTrader | 3 × 10 | 7.82 M | 3.59 M | 760,734 | 57,936 | 37 | 25 |
| StarterTrader | 60 phút | 9.47 M | 3.47 M | 1.53 M | 58,757 | 39 | 25.5 |
| BreedTrader | 5 phút | 3.05 M | 3.05 M | 15,010 | 15,010 | 8 | 8 |
| BreedTrader | 3 × 10 | 4.45 M | 4.44 M | 53,190 | 53,190 | 10 | 10 |
| BreedTrader | 60 phút | 5.13 M | 5.11 M | 73,862 | 73,862 | 11 | 11 |
| Collector | 5 phút | 14.77 M | 12.96 M | 0 | 0 | 27.5 | 26 |
| Collector | 3 × 10 | 16.39 M | 15.52 M | 0 | 0 | 37 | 36 |
| Collector | 60 phút | 14.03 M | 13.57 M | 0 | 0 | 36.5 | 35.5 |
| Breeder | 5 phút | 12.33 M | 13.43 M | 0 | 0 | 28 | 27.5 |
| Breeder | 3 × 10 | 11.58 M | 12.46 M | 0 | 0 | 34 | 35 |
| Breeder | 60 phút | 13.34 M | 12.45 M | 0 | 0 | 34 | 32.5 |

Đọc bảng:

- **E1 + E2 chặn đúng vòng khai thác Starter**: chơi nhiều hơn không còn giàu hơn (3,5–4,0 M ở mọi lịch), doanh thu bán giảm 96 % ở lịch 60 phút.
- **E3 gần như không đổi gì** với BreedTrader (chênh < 0,5 %) vì vòng lai vốn bị giới hạn bởi thời gian. E3 chỉ nên duyệt nếu muốn phí lai có vai trò tiêu hao, không phải để chống khai thác.
- Collector và Breeder chỉ trả thêm 100–750 G trong 90 ngày (cột "Quỹ ảo" trong báo cáo). Chênh số dư ±10 % là do giá đổi làm thứ tự mua và kết quả ngẫu nhiên đổi theo, không phải xu hướng: Breeder có lịch tăng, có lịch giảm.
- Người chơi không khai thác (Relaxed, Researcher) mua ≤ 1 gói Starter mỗi ngày nên E1 không chạm tới họ; E2 làm họ trả thêm 50 G cho mỗi gói Starter, không đáng kể so với sản xuất.

## 4. Giá trị kỳ vọng (phân tích, không ngẫu nhiên)

E[hệ số bán theo biến dị]: luck 0 = 1,2995; luck 8 = 1,6912.

| Gói | Giá hiện tại | EV bán Adult (luck 0) | EV / giá (luck 0) | EV / giá (luck 8) | Giá đề xuất | EV / giá đề xuất (luck 0 / 8) |
|---|---|---|---|---|---|---|
| starter | 100 | 141,3 | **1,41** | **1,84** | 150 | 0,94 / 1,23 |
| elemental | 900 | 443,2 | 0,49 | 0,64 | 900 | không đổi |
| mystic | 6 000 | 1 460,6 | 0,24 | 0,32 | 6 000 | không đổi |
| astral | 30 000 | 3 815,2 | 0,13 | 0,17 | 30 000 | không đổi |

Lai Emberling × Sprout: EV con Adult 231,7 G. EV / phí: 80 G → **2,90**; đề xuất 120 G → 1,93.

| Nâng cấp | Tổng chi tới cấp tối đa | Cấp 1 | Nhận xét |
|---|---|---|---|
| islandCapacityLevel | 275 635 | 500 | Ở G1 chỉ có 5 chỗ vật lý, nâng cấp không tăng sản xuất (E4) |
| mutationLuckLevel | 684 696 | 1 500 | Đẩy EV Starter lên 1,84 |
| foodQualityLevel | 71 566 | 200 | |
| foodCooldownLevel | 34 123 | 250 | |
| hatchSpeedLevel | 30 696 | 600 | |
| **Tổng** | **≈ 1,10 M** | | Đạt được trước ngày 30 ở mọi kiểu người chơi thường |

## 5. Nguồn và điểm tiêu vật liệu

| Vật liệu | Nguồn | Điểm tiêu hiện tại | Điểm tiêu đề xuất |
|---|---|---|---|
| wood, seed | `origin_forest` (2–4 / chuyến, 15 phút) | không có | Q_ORIGIN_FOREST_SURVEY, QUEST_AQUATIC_EXPLORATION |
| crystal, coral | `ocean_reef` (30 phút, cần bơi) | không có | QUEST_SKY_EXPLORATION, Q_EXPEDITION_REEF |
| stone, bụi sao | chưa có | — | không dùng |

Ở production vật liệu chỉ tích lũy. Researcher dùng 10–18 đơn vị trong 90 ngày cho nhiệm vụ cổng (cột "Vật liệu dùng"), nên vật liệu không thành tiền tệ lạm phát. E5 đề xuất thêm chế tạo trang trí làm điểm tiêu lâu dài.

## 6. Researcher và nhiệm vụ (thử nghiệm, nhịp `ResearchPacing.ProposedU210`)

| Lịch | Ethology 7 / 30 / 90 ng | Cá thể hiểu rõ 90 ng | Mở Thủy Vực (trung vị, P10–P90, % seed) | Mở Thiên Giới |
|---|---|---|---|---|
| 5 phút (09:00) | 0 / 1 / 1,5 | 3 | ngày 15,5 (9–22,3), 90 % | ngày 30 (18,4–59), 45 % |
| 3 × 10 phút | 1 / 3 / 4 | 3,5 | ngày 5,3 (4,5–11,1), 100 % | ngày 13,5 (9,5–58), 55 % |
| 60 phút | 1 / 3 / 3 | 4 | ngày 5,0 (4,5–10,5), 100 % | ngày 26 (11,1–37,8), 60 % |

- Thủy Vực mở sau **khoảng 1 tuần** với người chơi đều đặn, **2 tuần** với 5 phút/ngày. Không lịch nào mở trong ngày đầu, nên nhiệm vụ không thể "cày" xong.
- Thiên Giới chỉ mở ở 45–60 % seed trong 90 ngày. Lý do chính: cần 1 loài sống ở Thủy Vực đạt Ethology, trong khi loài Thủy Vực đến từ gói đắt và thường chỉ có 1 cá thể ngủ đúng lúc người chơi online. Đề xuất cân nhắc: hạ yêu cầu xuống "LifeCycle + 4 tập tính" hoặc chờ quyết định số 3 (nhật ký đêm).
- Lịch 5 phút lúc 09:00 **không bao giờ đạt Ethology cho loài ban ngày** vì không thấy chúng ngủ (quyết định số 3 trong ADR).
- Số "Quan sát" trong báo cáo đếm cả lần bấm bị hồi từ chối; số phát hiện thật bị giới hạn 2 / loài / ngày.

## 7. Khai thác đã kiểm

| Khai thác | Baseline | Với đề xuất | Ghi chú |
|---|---|---|---|
| Mua – nuôi – bán Starter | lời 41–84 % mỗi vòng | 5 gói/ngày, EV 0,94–1,23 | E1 + E2 |
| Lai Common rồi bán | lời 190 % mỗi vòng | 93 % | giới hạn tự nhiên bởi thời gian |
| Spam quan sát | U2.9: Ethology trong 15–50 phút | 2 phát hiện / loài / ngày, hồi 30 s | R3, không tạo vàng |
| Mở thế giới qua nhiệm vụ mà bỏ qua luật | — | không thể: `unlockZone` vẫn kiểm đủ điều kiện ADR 0201 | test `Researcher_is_paced_and_unlocks_worlds_only_through_canonical_commands` |
| Vàng từ nghiên cứu / xã hội | — | không có | test `U210_read_only_queries_do_not_change_the_economy` |

## 8. Chạy lại

```bash
cd Assets/Dravio/Tests/Domain/DotNet~
# Bảng EV và nâng cấp
DRAVIO_U210_EV_OUT=/tmp/ev.md dotnet test --filter "FullyQualifiedName~Write_u210_analytic_tables"
# Báo cáo một kiểu người chơi (mặc định 20 seed; vài phút mỗi kiểu)
DRAVIO_U210_SIM_OUT=/tmp/Researcher.md DRAVIO_SIM_SEEDS=20 DRAVIO_SIM_POLICIES=Researcher \
  dotnet test --filter "FullyQualifiedName~Write_u210_economy_report"
```

Không đặt biến môi trường thì hai test báo cáo bỏ qua, CI chỉ chạy các test tất định nhanh trong `U210EconomySimulationTests`.

Trước khi đổi catalog thật: chạy lại với 1 000 seed cho biến thể được chọn (ADR 0204 quyết định số 10).
