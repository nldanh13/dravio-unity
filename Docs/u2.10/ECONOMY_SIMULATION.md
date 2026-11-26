# U2.10 — Economy Simulation 7 / 30 / 90 ngày

> Quyết định và phần chờ duyệt: [ADR 0204](../adr/0204-u2-10-research-social-progression.md) mục D (E1–E6) và R11.
> **Trạng thái:** mục 1–8 là mô phỏng **trước khi duyệt** (PR #46, biến thể `proposed` dùng "quỹ ảo"). Từ ADR 0205, E1, E2, E5 và R11 là luật thật; kết quả trên luật thật ở **mục 0**.
> **Nguồn:** `EconomySimulator` (U2.7), catalog G0. GDD 3.1 không có trong repo, nên mọi mục tiêu cân bằng là đề xuất.

## 0. Sau khi bật luật thật (ADR 0205)

Simulator chạy trên dispatcher thật: Starter 150 G, tối đa 5 gói/ngày, lệnh `buildSanctuaryProject`, nhật ký đêm khi quyết toán offline. Hai biến thể hành vi:

- `no-projects`: người chơi mua nâng cấp nhưng không xây dự án.
- `live`: mua nâng cấp, xây dự án khi số dư ≥ 1,2 × giá và đủ vật liệu, gửi thám hiểm Khởi Nguyên khi thiếu gỗ hoặc hạt giống.

Đã chạy **50 seed × 6 kiểu người chơi × 3 lịch × 2 biến thể × 90 ngày** (45 phút, 4 luồng). Bảng đầy đủ: [`sim/live-50-seeds.md`](sim/live-50-seeds.md).

| Người chơi (3 × 10 phút/ngày, trung vị 90 ngày) | Số dư no-projects | Số dư live | Tiêu / nguồn | Ngày thu nhập đang giữ | Dự án | Gỗ + hạt đã dùng |
|---|---|---|---|---|---|---|
| Relaxed | 14,95 M | 2,37 M | 0,19 → 0,87 | 72 → 11 | 23 | 126 |
| Collector | 20,91 M | 2,67 M | 0,23 → 0,88 | 70 → 11 | 24 | 136 |
| Breeder | 16,12 M | 2,56 M | 0,21 → 0,88 | 71 → 11 | 23,5 | 131 |
| Researcher | 23,46 M | 3,16 M | 0,14 → 0,88 | 78 → 11 | 25 | 146 |
| StarterTrader | 3,15 M | 3,34 M | 0,27 | 66 | 2 | 0 |
| BreedTrader | 4,31 M | 3,94 M | 0,21 → 0,23 | 71 → 69 | 2 | 0 |

Kết luận:

- **E5 làm việc của nó.** Với người chơi thường, lượng vàng tiêu đi tăng từ khoảng 20 % lên khoảng 88 % lượng vàng kiếm được. Số dư sau 90 ngày giảm từ 15–23 M xuống 2–3 M, tức chỉ còn khoảng 11 ngày thu nhập thay vì 70–78 ngày.
- **Vật liệu có chỗ tiêu.** Mỗi người chơi thường dùng 116–146 gỗ + hạt giống cho dự án trong 90 ngày. Hạt giống là thứ giới hạn nhịp xây dự án; nếu người chơi không đi thám hiểm Khởi Nguyên thì sẽ dừng ở dự án thứ 2.
- **E1 + E2 chặn vòng mua–bán.** StarterTrader bán được 68–72 k G trong 90 ngày, so với 1,88 M G trước U2.10 (PR #46, 60 phút/ngày). Hai kiểu "trader" không đi thám hiểm nên chỉ xây 2 dự án.
- **Không làm chậm sưu tầm.** Số loài và ô Codex của `live` bằng `no-projects` (chênh ≤ 1,5 ô).
- **Nhịp đầu game không đổi.** Số dư 7 ngày giảm nhẹ ở người chơi thường (Starter đắt hơn 50 G), vẫn 36–69 k G.

Mô phỏng 1 000 seed cho `live` (lịch 3 × 10 phút/ngày, 6 kiểu người chơi) đang chạy; kết quả sẽ thêm vào [`sim/live-1000-seeds.md`](sim/live-1000-seeds.md).

## 1. Phương pháp

- `Assets/Dravio/Tests/Domain/Simulation/EconomySimulator.cs` chạy **`CommandDispatcher` thật** trên `GameSession`: mua gói, ấp, cho ăn, lai, bán, nâng cấp, thám hiểm, đặt rồng, mở zone. Không có công thức kinh tế viết lại.
- 6 kiểu người chơi × 3 lịch chơi × 2 biến thể × 20 seed, mốc 1 / 7 / 30 / 90 ngày. Số liệu là **trung vị 20 seed**. Báo cáo đầy đủ (P10 / trung vị / P90 ở mọi mốc): [`sim/`](sim/).
- Lịch chơi: **5 phút/ngày** (09:00), **3 × 10 phút/ngày**, **60 phút/ngày**. Offline dùng đúng luật offline đã duyệt. Không bật thiên tai, Comfort drift hay tiêu hao năng lượng.
- Biến thể:
  - `current` = catalog hiện tại. Người chơi mua nâng cấp (mỗi phiên tối đa một nâng cấp rẻ nhất có giá ≤ 50 % số dư).
  - `proposed` = `current` + **E1** (5 gói Starter / ngày) + **E2** (Starter 150 G) + **E3** (phí lai Common 120 G) + **E5** (dự án Sanctuary 20 000 × 1,25ⁿ G, xây khi số dư ≥ 1,2 × giá) + **R11** (nhật ký đêm). Phần chênh giá và dự án trừ vào **quỹ ảo**, nên catalog không bị chạm.
- Chỉ số theo dõi (theo cách EVE Online và Unity khuyên):
  - **Nguồn** = sản xuất + bán + thưởng thành tựu.
  - **Tiêu** = gói + lai + nâng cấp + quỹ ảo.
  - **Tiêu / nguồn.** Gần 1 nghĩa là vàng vào bao nhiêu thì có chỗ ra bấy nhiêu.
  - **Ngày thu nhập đang giữ** = số dư / thu nhập trung bình mỗi ngày.

| Kiểu người chơi | Hành vi |
|---|---|
| Relaxed | Cho ăn, một gói mỗi ngày, không bán |
| Collector | Mỗi phiên mua gói tốt nhất đủ tiền, lai khi có cặp |
| Breeder | Lai cặp đắt nhất, gõ tổ, một gói mỗi ngày |
| Researcher | Xem hồ sơ, quan sát mọi rồng, thám hiểm, đặt rồng vào sinh cảnh thế giới mới, giữ một Adult biết bay ở sinh cảnh, hoàn thành nhiệm vụ cổng theo đề xuất rồi gửi `unlockZone` thật |
| StarterTrader | Khai thác: mua Starter, nuôi, bán liên tục |
| BreedTrader | Khai thác: lai Emberling × Sprout rồi bán con |

## 2. Kết quả: trước / sau (90 ngày, trung vị)

| Người chơi | Lịch | Số dư trước | Số dư sau | Tiêu / nguồn trước → sau | Ngày thu nhập đang giữ trước → sau | Dự án | Loài | Ô Codex trước → sau |
|---|---|---|---|---|---|---|---|---|
| Relaxed | 5 phút | 12.73 M | 1.66 M | 0,22 → 0,91 | 70 → 8 | 22 | 13 → 12 | 24 → 22,5 |
| Relaxed | 3 × 10 | 14.78 M | 1.61 M | 0,20 → 0,91 | 72 → 8 | 22,5 | 13 | 24 → 26 |
| Relaxed | 60 phút | 14.68 M | 2.81 M | 0,20 → 0,85 | 72 → 13 | 23 | 13 | 25 → 24,5 |
| Collector | 5 phút | 14.17 M | 2.04 M | 0,24 → 0,89 | 69 → 10 | 23 | 14 | 34 → 33,5 |
| Collector | 3 × 10 | 19.83 M | 2.10 M | 0,24 → 0,93 | 68 → 6 | 24 | 14 | 41 → 41,5 |
| Collector | 60 phút | 16.23 M | 1.87 M | 0,21 → 0,91 | 71 → 8 | 23 | 14 | 38,5 |
| Breeder | 5 phút | 12.72 M | 1.86 M | 0,26 → 0,88 | 67 → 11 | 22 | 14 | 34 → 34,5 |
| Breeder | 3 × 10 | 14.82 M | 1.91 M | 0,21 → 0,88 | 71 → 10 | 22,5 | 14 | 35,5 → 36,5 |
| Breeder | 60 phút | 14.88 M | 2.44 M | 0,19 → 0,86 | 73 → 12 | 22 | 14 | 36 |
| Researcher | 5 phút | 19.44 M | 3.65 M | 0,16 → 0,85 | 76 → 14 | 24,5 | 14 | 25 |
| Researcher | 3 × 10 | 23.60 M | 3.02 M | 0,13 → 0,89 | 78 → 10 | 25 | 14 | 26 → 25 |
| Researcher | 60 phút | 24.71 M | 2.89 M | 0,13 → 0,89 | 78 → 10 | 25 | 14 | 26 → 27 |
| StarterTrader | 5 phút | 3.63 M | 0.46 M | 0,25 → 0,87 | 68 → 12 | 16 | 10 | 32 → 27 |
| StarterTrader | 3 × 10 | 7.51 M | 0.57 M | 0,18 → 0,87 | 74 → 12 | 16 | 10 | 38 → 29 |
| StarterTrader | 60 phút | 9.28 M | 0.54 M | 0,19 → 0,87 | 73 → 11 | 16 | 10 | 40 → 28 |
| BreedTrader | 5 phút | 2.32 M | 0.45 M | 0,32 → 0,85 | 61 → 14 | 15 | 3 | 10 |
| BreedTrader | 3 × 10 | 4.47 M | 0.61 M | 0,20 → 0,89 | 72 → 10 | 17 | 3 | 11 |
| BreedTrader | 60 phút | 5.01 M | 0.74 M | 0,19 → 0,87 | 73 → 12 | 18 | 3 | 12 |

Doanh thu bán của StarterTrader 60 phút/ngày: **1,88 M → 70,6 k**. Mọi kiểu người chơi đều nâng cấp tối đa (42 cấp) ở cả hai biến thể.

## 3. Vấn đề đã tìm ra và cách giải quyết

| # | Vấn đề (catalog hiện tại) | Đo được | Cách giải quyết đề xuất | Kết quả trong mô phỏng |
|---|---|---|---|---|
| 1 | **Không có điểm tiêu dài hạn.** Nâng cấp tối đa chỉ tốn khoảng 1,1 M G, sau đó vàng chỉ tích lũy | Tiêu / nguồn 0,13–0,26; số dư 90 ngày 12–25 M; giữ 67–78 ngày thu nhập | **E5 dự án Sanctuary**: tự nguyện, chỉ thẩm mỹ, giá tăng 25 % mỗi dự án, không tăng sản xuất | Tiêu / nguồn **0,85–0,93**; số dư 1,6–3,7 M; giữ **6–14 ngày** thu nhập; mỗi người chơi luôn có mục tiêu tiếp theo (22–25 dự án sau 90 ngày) |
| 2 | **Vòng mua Starter rồi bán có lời**: EV / giá 1,41–1,84 | StarterTrader 60 phút bán 1,88 M | **E1 + E2** | Bán 70,6 k; người khai thác nghèo hơn người chơi thường (0,5 M so với 1,6–3,7 M) |
| 3 | **Người chơi chỉ chơi buổi sáng không đạt Ethology** cho loài ban ngày (không thấy chúng ngủ) | Researcher 5 phút: 0 loài Ethology ở ngày 30, 1 ở ngày 90 | **R11 nhật ký đêm** | **3 loài ở ngày 30, 3 ở ngày 90**, ngang người chơi nhiều phiên |
| 4 | **Thiên Giới mở ở ít seed** (45–60 % trong báo cáo trước) | Nguyên nhân: Researcher trong mô phỏng cũ không bao giờ đặt rồng vào sinh cảnh Thủy Vực, nên không có loài nào "được ghi chép ở Thủy Vực" | Sửa người chơi mô phỏng (đặt rồng vào thế giới mới, giữ một Adult biết bay). **Không cần hạ yêu cầu Q2** | 90–95 % seed mở Thiên Giới trong 90 ngày với biến thể proposed (mục 4) |

Ghi chú:

- Báo cáo trước (bản đầu của PR này) cho người chơi không mua nâng cấp. Báo cáo này cho họ mua nâng cấp, nên số dư `current` khác báo cáo cũ một chút. Kết luận không đổi.
- **E3** (phí lai 120 G) gần như không ảnh hưởng BreedTrader, vì vòng lai bị giới hạn bởi thời gian ấp. E3 chỉ nên duyệt nếu muốn phí lai có vai trò tiêu hao.
- Dự án Sanctuary không làm chậm sưu tầm: số loài và ô Codex trước và sau chênh không quá 1,5 ô, trừ StarterTrader (người khai thác mất bớt ô Codex vì E1).
- Tiêu / nguồn không đạt 1 vì người chơi giữ lại khoảng 20 % làm dự trữ (quy tắc "xây khi số dư ≥ 1,2 × giá").

## 4. Researcher: nghiên cứu và mở thế giới (thử nghiệm)

| Lịch | Biến thể | Ethology 7 / 30 / 90 ng | Mở Thủy Vực (trung vị, P10–P90, % seed) | Mở Thiên Giới |
|---|---|---|---|---|
| 5 phút (09:00) | current | 0 / 0 / 1 | ngày 12 (7,7–26,3), 90 % | ngày 27,5 (13,3–44,4), 70 % |
| 5 phút (09:00) | proposed | 0 / **3** / **3** | ngày 11 (8,9–15,3), **100 %** | ngày 29 (20,2–43,6), **90 %** |
| 3 × 10 phút | current | 1 / 3 / 3 | ngày 5,5 (4,5–10,5), 100 % | ngày 10,2 (7,9–42,8), 95 % |
| 3 × 10 phút | proposed | 1 / 3 / 3 | ngày 5,2 (4,2–10,5), 100 % | ngày 15 (8,2–37,6), 95 % |
| 60 phút | current | 1 / 3 / 3 | ngày 5,5 (5–10,7), 100 % | ngày 11,7 (9–33,4), 100 % |
| 60 phút | proposed | 1 / 3 / 3 | ngày 5,5 (4–10,3), 100 % | ngày 13 (8–46,6), 95 % |

- Thủy Vực mở sau khoảng **1 tuần** với người chơi đều đặn, **khoảng 11 ngày** với 5 phút/ngày. Không lịch nào mở trong ngày đầu.
- Thiên Giới mở sau khoảng **2 tuần** (chơi đều) đến **4 tuần** (5 phút/ngày).
- Số "Quan sát" trong báo cáo đếm cả lần bấm bị hồi từ chối. Số phát hiện thật bị giới hạn 2 / loài / ngày.

## 5. Giá trị kỳ vọng (phân tích, không ngẫu nhiên)

E[hệ số bán theo biến dị]: luck 0 = 1,2995; luck 8 = 1,6912.

| Gói | Giá hiện tại | EV bán Adult (luck 0) | EV / giá (luck 0) | EV / giá (luck 8) | Giá đề xuất | EV / giá đề xuất (luck 0 / 8) |
|---|---|---|---|---|---|---|
| starter | 100 | 141,3 | **1,41** | **1,84** | 150 | 0,94 / 1,23 |
| elemental | 900 | 443,2 | 0,49 | 0,64 | 900 | không đổi |
| mystic | 6 000 | 1 460,6 | 0,24 | 0,32 | 6 000 | không đổi |
| astral | 30 000 | 3 815,2 | 0,13 | 0,17 | 30 000 | không đổi |

Lai Emberling × Sprout: EV con Adult 231,7 G. EV / phí: 80 G → **2,90**; đề xuất 120 G → 1,93.

| Nâng cấp | Tổng chi tới cấp tối đa (G) | Cấp 1 (G) |
|---|---|---|
| islandCapacityLevel | 275 635 | 500 |
| mutationLuckLevel | 684 696 | 1 500 |
| foodQualityLevel | 71 566 | 200 |
| foodCooldownLevel | 34 123 | 250 |
| hatchSpeedLevel | 30 696 | 600 |
| **Tổng** | **≈ 1,10 M** | |

Dự án Sanctuary E5: dự án thứ n giá 20 000 × 1,25ⁿ G. Dự án 1 giá 20 k, dự án 10 khoảng 149 k, dự án 20 khoảng 1,39 M, dự án 25 khoảng 4,24 M. Không có trần, nên người chơi lâu năm vẫn có thứ để làm.

## 6. Nguồn và điểm tiêu vật liệu

| Vật liệu | Nguồn | Điểm tiêu hiện tại | Điểm tiêu đề xuất |
|---|---|---|---|
| wood, seed | `origin_forest` (2–4 mỗi chuyến, 15 phút) | không có | Q_ORIGIN_FOREST_SURVEY, QUEST_AQUATIC_EXPLORATION; sau này thêm vào công thức dự án E5 |
| crystal, coral | `ocean_reef` (30 phút, cần bơi) | không có | QUEST_SKY_EXPLORATION, Q_EXPEDITION_REEF; sau này thêm vào công thức dự án E5 |
| stone, bụi sao | chưa có | — | không dùng |

Vật liệu vẫn chỉ tích lũy (Researcher giữ vài trăm đơn vị sau 90 ngày). Đưa vật liệu vào công thức dự án Sanctuary là bước tiếp theo tự nhiên.

## 7. Khai thác đã kiểm

| Khai thác | Hiện tại | Với đề xuất | Ghi chú |
|---|---|---|---|
| Mua – nuôi – bán Starter | lời 41–84 % mỗi vòng | 5 gói/ngày, EV 0,94–1,23 | E1 + E2 |
| Lai Common rồi bán | lời 190 % mỗi vòng | 93 % | bị giới hạn tự nhiên bởi thời gian |
| Spam quan sát | U2.9: Ethology trong 15–50 phút | 2 phát hiện / loài / ngày, hồi 30 s | R3, không tạo vàng |
| Nhật ký đêm | — | tối đa 1 phát hiện / loài / đêm, trong giới hạn ngày, chỉ khi có rồng đang ngủ ở sinh cảnh | R11, không tạo vàng |
| Mở thế giới qua nhiệm vụ mà bỏ qua luật | — | không thể: `unlockZone` vẫn kiểm đủ điều kiện ADR 0201 | test `Researcher_progress_is_paced_over_days_and_unlocks_worlds_only_through_canonical_commands` |
| Vàng từ nghiên cứu / xã hội | — | không có | test `U210_read_only_queries_do_not_change_the_economy` |
| Dự án Sanctuary sinh lời | — | không: chỉ thẩm mỹ, không tăng sản xuất | E5 |

## 8. Chạy lại

```bash
cd Assets/Dravio/Tests/Domain/DotNet~
# Bảng EV và nâng cấp
DRAVIO_U210_EV_OUT=/tmp/ev.md dotnet test --filter "FullyQualifiedName~Write_u210_analytic_tables"
# Báo cáo một kiểu người chơi (20 seed; seed chạy song song, kết quả không đổi theo số luồng)
# Lọc thêm: DRAVIO_SIM_VARIANTS=no-projects,live  DRAVIO_SIM_SCHEDULES="3 × 10 phút/ngày"
DRAVIO_U210_SIM_OUT=/tmp/Researcher.md DRAVIO_SIM_SEEDS=20 DRAVIO_SIM_POLICIES=Researcher \
  dotnet test --filter "FullyQualifiedName~Write_u210_economy_report"
```

Không đặt biến môi trường thì hai test báo cáo bỏ qua. CI chỉ chạy các test tất định nhanh trong `U210EconomySimulationTests`. Trước khi đổi catalog thật, chạy lại với 1 000 seed cho biến thể được chọn (ADR 0204 quyết định số 10).
