# U2.14 — Alpha Domain Stability & Gameplay Validation

> Nhánh `feat/u2-14-alpha-stability` từ `main` @ `c4807a5` (sau PR #66–#69). Domain / Persistence / test. **Không đổi code game, không đổi Save Schema (v10), không bật luật chưa duyệt.**
> .NET 8, Release, cục bộ, ngày 2026-11-26; CI GitHub chạy trên PR.

Chú thích: **Đã chạy** = test chạy thật có assert. **Mô phỏng** = số liệu từ mô phỏng nhiều ngày (cũng là lệnh thật, nhưng là chính sách người chơi giả định). **Chưa kiểm thử** = không có test.

## 1. Kết quả chung

| Hạng mục | Kết quả | Loại | Tài liệu |
|---|---|---|---|
| Hành trình Alpha end-to-end (mục I) | 3 / 3 seed đạt đủ 18 mốc trong 40 ngày, chỉ bằng lệnh chính thức | Đã chạy | mục 2 |
| Bộ sưu tập 16 loài (mục II) | **15 / 16** ở production; Aurorawing chờ bảng `sky_cloud` | Đã chạy + mô phỏng | [COLLECTION_MATRIX.md](COLLECTION_MATRIX.md) |
| Kinh tế 1 / 7 / 30 / 90 / 365 ngày (mục III) | 0 vi phạm sổ vàng / vật liệu / thưởng một lần trên 54 lần chạy 365 ngày; 60 dự án **không** hoàn thành được trong thời gian thực tế | Mô phỏng + đã chạy | [ECONOMY_ALPHA.md](ECONOMY_ALPHA.md) |
| Save & độ tin cậy (mục IV) | 11 test trên save thật của hành trình; replay 30 489 lệnh trùng state | Đã chạy | [SAVE_RELIABILITY.md](SAVE_RELIABILITY.md) |
| Hai quyết định chờ duyệt (mục V) | khuyến nghị: bảng A; luật cho ăn B (không cần đổi save) | Đã chạy + mô phỏng | [BALANCE_DECISIONS.md](BALANCE_DECISIONS.md) |
| API cho AI tích hợp | không đổi API; hướng dẫn hành trình Alpha | — | [API_INTEGRATION.md](API_INTEGRATION.md) |

Bộ test sau U2.14 (cục bộ): Domain **543 đạt** / 0 lỗi (13 bỏ qua: test sinh báo cáo chỉ chạy khi đặt biến môi trường), Persistence **217 đạt**, Character 54 đạt, Integration 146 đạt (không đổi code hai bộ này). Trước U2.14: Domain 516, Persistence 206.

## 2. Hành trình end-to-end

### 2.1 Cách kiểm

`AlphaJourney` (thư mục `Assets/Dravio/Tests/Domain/Simulation`) chạy người chơi theo hướng dẫn (`Policy.Guided`, 3 × 10 phút/ngày) từ **save mới** của `InitialStateFactory`, luật production. Biến thể `EconomyVariant.Alpha()` bật `ObserveViaCommand`: cả quan sát cũng gửi lệnh `observeDragon` thay vì gọi thẳng sổ nghiên cứu, nên **không có dòng code nào đổi `GameState` ngoài `CommandDispatcher`**. Ba lớp bảo đảm:

1. **Sau mỗi lệnh** (≈ 30 000 lệnh / 40 ngày): lệnh bị từ chối phải trả lại đúng object state cũ; Δvàng = sản xuất + bán + thành tựu − chi (theo event); sản xuất ≤ trần; Δkho mỗi vật liệu = chuyến + mốc − dự án − giao nhiệm vụ; thành tựu / nhiệm vụ / mốc / trứng thưởng / chuyến trả đúng một lần; số lượng chuyến trong [Min, Max] của tuyến; ≤ 500 rồng.
2. **Phát lại:** log (lệnh, thời điểm) phát lại từ save mới cho state cuối **trùng canonical JSON** (test Persistence `The_alpha_journey_is_a_pure_function_of_its_command_log`).
3. **Save/load ở 11 giai đoạn:** xem SAVE_RELIABILITY.md.

### 2.2 Mốc hành trình (ngày lần đầu đạt, đã chạy)

| Mốc (đề bài mục I) | Lệnh | Seed 1 | Seed 2 | Seed 3 |
|---|---|---|---|---|
| Save mới → hoàn thành tutorial (8 bước) | `advanceTime`, `feed`, `consumeFood`, `viewBiologyProfile`, `surveyHabitat`, `unlockHabitat`, `buyTutorialPack`, `claimPack` | 0,00 | 0,00 | 0,00 |
| Cho ăn | `feed` + `consumeFood` | 0,00 | 0,00 | 0,00 |
| Rồng đầu tiên thành Adult | `advanceTime` | 0,00 | 0,00 | 0,00 |
| Mua gói trứng bằng vàng | `buyPack`, `claimPack` | 0,00 | 0,00 | 0,00 |
| Lai | `startBreed` | 1,46 | 1,46 | 1,46 |
| Ấp (gõ trứng) | `tapEgg` | 1,46 | 1,46 | 1,46 |
| Nở rồng lai | `claimBreed` | 1,46 | 1,46 | 1,46 |
| Mở đủ sinh cảnh Khởi Nguyên (3 / 3) | `surveyHabitat`, `unlockHabitat` | 0,00 | 0,00 | 0,00 |
| Quan sát | `observeDragon` | 0,00 | 0,00 | 0,00 |
| Nghiên cứu sinh học: Ethologist | `completeQuest` | 9,17 | 11,00 | 9,46 |
| Nghiên cứu sinh học: Sky Chronicle (cuối chuỗi) | `completeQuest` | 10,00 | 9,00 | 11,00 |
| Mở Thủy Vực (+ trứng Droplet) | `unlockZone("ocean")` | 3,46 | 3,00 | 3,17 |
| Mở Thiên Giới (+ trứng Zephyr) | `unlockZone("sky")` | 9,17 | 8,46 | 10,46 |
| Thám hiểm rừng, nhận thưởng | `startExpedition`, `claimExpedition` | 2,17 | 2,00 | 1,17 |
| Thám hiểm rạn, nhận thưởng | như trên | 5,17 | 6,00 | 4,17 |
| Mốc thám hiểm rạn 15 chuyến | `claimExpedition` | 11,00 | 12,46 | 14,00 |
| Dự án Sanctuary đầu tiên | `buildSanctuaryProject` | 5,46 | 7,00 | 3,17 |
| Dự án Sanctuary thứ 10 | như trên | 24,17 | 15,00 | 15,00 |

Ngày 40: 16 / 17 nhiệm vụ (thiếu đúng `Q_EXPEDITION_CLOUD`), 3 / 3 thế giới, 12 / 17 / 18 dự án, 0 vi phạm, 0 chuyến sân mây, 0 bụi sao. Lệnh: 30 489 / 30 918 / 33 146. Lệnh bị từ chối (bình thường): `observation-cooldown` 657–717, `not-observable` 37–56, `zone-full` 1–491, `production-budget-full` 0–41; không lệnh bị từ chối nào đổi state.

"0,00" nghĩa là trong phiên 10 phút đầu tiên. Tutorial hoàn tất trong phiên đầu vì mọi bước TUT_01–TUT_08 đều làm được ngay (Emberling khởi đầu đã gần Adult; gói hướng dẫn 100 G).

### 2.3 Cùng hành trình với bảng sky_cloud A (chỉ mô phỏng, seed 1)

`SkyCloudBalance.Use(ProposedA)` bao quanh lần chạy; game thật vẫn tắt (test kiểm `SkyCloudBalance.Current.Enabled == false` sau khi chạy). Ngày 40: **17 / 17 nhiệm vụ** (`Q_EXPEDITION_CLOUD` ngày 9,46), trứng Aurorawing ngày **14,46** (đúng một lần, sau 15 chuyến sân mây), 90 chuyến sân mây, 265 bụi sao, 16 dự án, 0 vi phạm. Mốc rạn 15 không đạt trong 40 ngày vì slot thám hiểm chung dồn cho sân mây (Q7 U2.13).

### 2.4 Bảy kiểu người chơi, 21 ngày (đã chạy)

Thư giãn, Nghiên cứu, Sưu tập, Lai giống, Casual và hai kiểu khai thác (StarterTrader mua–nuôi–bán, BreedTrader lai–bán): sổ vàng đóng, vàng cuối = vàng đầu + sản xuất + bán + thành tựu − chi (`Every_policy_keeps_the_gold_and_material_ledgers_closed`).

## 3. Danh sách test U2.14

| Tệp | Test | Nội dung |
|---|---|---|
| `Tests/Domain/Simulation/AlphaJourney.cs` | — | bộ chạy hành trình + kiểm toán sau mỗi lệnh + log phát lại |
| `Tests/Domain/Simulation/EconomySimulator.cs` | (sửa) | `EconomyVariant.Alpha`, `ObserveViaCommand`, `OnDispatch`, `NewSave(seed)`; hành vi cũ không đổi khi tắt cờ |
| `Tests/Domain/Simulation/U214AlphaJourneyTests.cs` | 14 | hành trình 3 seed; luật production giữ tắt; thưởng một lần; lệnh bị từ chối không đổi state; 7 kiểu người chơi; bảng A trong phạm vi |
| `Tests/Domain/U214CollectionMatrixTests.cs` | 7 + 1 báo cáo | 15 / 16; nguồn gói / lai / trứng; Amberhorn; Aurorawing; bế tắc bán Aurorawing; loài Thủy Vực chờ kho; kho đầy |
| `Tests/Domain/Simulation/U214EconomyAlphaTests.cs` | 6 + 1 báo cáo | vật liệu 60 dự án luôn có nguồn; bảng giá 60 dự án; tuần đầu 4 kiểu |
| `Tests/Persistence/U214SaveReliabilityTests.cs` | 11 | Schema v10 ở 11 giai đoạn; nạp lại chơi tiếp trùng; replay; offline; mất điện; main hỏng; duplicate; 500 rồng + hàng chờ; > 500; read-only; 7 lỗi nhập |

Báo cáo (`Write_u214_*`) chỉ chạy khi đặt `DRAVIO_U214_OUT`; CI bỏ qua như các báo cáo U2.10–U2.13.

## 4. Chưa kiểm thử / ngoài phạm vi

- UI, Character, Environment, tài nguyên nghệ thuật (không đụng).
- Thiết bị thật, hiệu năng di động, kill app thật trên iOS / Android.
- Người chơi thật (mọi kiểu người chơi là chính sách mô phỏng).
- Luật chưa duyệt trong game thật (bảng sky_cloud, luật cho ăn B): chỉ có trong mô phỏng / test.
- Thiên tai tự nhiên (tắt trong production từ ADR 0202, không bật trong U2.14).

## 5. Phát hiện chính

1. Alpha production đi hết hành trình ba thế giới trong ~10 ngày (3 × 10 phút/ngày) mà không bế tắc; thiếu duy nhất nội dung gắn với `sky_cloud`.
2. **60 dự án Sanctuary không hoàn thành được** (giá tăng 25 % / dự án; tổng 52,2 tỉ G) — cần quyết định thiết kế (N1).
3. Bán Aurorawing duy nhất làm mất loài vĩnh viễn (N2) — đề xuất cảnh báo ở UI.
4. Validator không giới hạn 500 rồng khi nạp (N3), game không tự tạo trạng thái này.
5. Luật cho ăn B không cần đổi save (phần ăn không được lưu) — sửa nhận định của U2.13.
