# U2.14 — Save & độ tin cậy (Schema v10)

> **Schema v10 giữ nguyên, không migration, không trường mới.** U2.14 chỉ thêm test; không đổi code Persistence, Platform hay Domain.
> Bộ test: `Assets/Dravio/Tests/Persistence/U214SaveReliabilityTests.cs` (11 test) chạy trên **save thật của hành trình Alpha** (Guided, 3 × 10 phút/ngày, seed 1, 40 ngày, chỉ lệnh chính thức, xem [ALPHA_VALIDATION.md](ALPHA_VALIDATION.md)). Số liệu thời gian / kích thước in ra console của test.
> Môi trường: .NET 8, Release, Linux x64 cục bộ, ngày 2026-11-26. Thời gian đo là của máy chạy test, không phải thiết bị di động.

Chú thích cột "Loại": **Đã chạy** = test chạy thật, có assert. **Mô phỏng** = số từ mô phỏng nhiều ngày. **Chưa kiểm thử** = không có test trong U2.14.

## 1. Tổng hợp

| Hạng mục | Loại | Kết quả | Test |
|---|---|---|---|
| Save Schema v10 ở mọi giai đoạn | Đã chạy | 11 giai đoạn, lưu → nạp trùng canonical, ghi lại **trùng từng byte**, validator hợp lệ | `Schema_v10_save_round_trips_byte_for_byte_at_every_alpha_stage` |
| Save/load giữa hành trình rồi chơi tiếp | Đã chạy | state nạp lại và state đang chạy cho **cùng kết quả** sau 6 h online + 6 h offline, ở cả 11 giai đoạn | `A_reloaded_save_plays_on_exactly_like_the_live_state` |
| Hành trình là hàm thuần của log lệnh | Đã chạy | phát lại 30 489 lệnh từ save mới → state cuối trùng canonical | `The_alpha_journey_is_a_pure_function_of_its_command_log` |
| Offline progression | Đã chạy | mở lại sau 30 h: nhận đúng 24 h × 75 % (91 432 G = trần), chuyến thám hiểm đang đi hoàn tất (`Ready`), mở lại ngay không nhận lần hai | `Offline_progression_from_a_late_save_is_capped_at_24_hours_75_percent_and_paid_once` |
| Crash recovery (mất điện khi ghi) | Đã chạy | mất điện ở **mọi** thao tác ghi của lần lưu cuối: luôn nạp được bản trước hoặc bản mới, đúng từng byte | `Power_loss_at_every_write_step_of_late_game_saves_never_loses_progress` |
| Main hỏng (file bị cắt) | Đã chạy | tự lùi về backup1, mất tối đa một lần lưu, báo `corrupt-json` | `Corrupted_main_falls_back_to_the_newest_valid_backup_of_the_journey` |
| Duplicate request | Đã chạy | `processedRequestIds` (≤ 200) lưu trong save; gửi lại 50 lệnh cuối sau khi nạp → `Duplicate`, 0 event, state không đổi | `Request_ids_survive_save_load_so_a_retried_command_is_never_applied_twice` |
| Kho đầy | Đã chạy | 500 rồng: gói bị từ chối `storage-full`, không trừ vàng | `Five_hundred_dragons_…`, `Storage_full_never_loses_a_reward_egg_…` (Domain) |
| Hàng chờ trứng thưởng | Đã chạy | mở Thủy Vực khi kho đầy → `ZONE_EGG_OCEAN` vào `rewardQueue`; lưu / nạp giữ nguyên; bán 1 rồng → giao đúng một lần; nạp lại không giao lại | `Five_hundred_dragons_with_a_queued_reward_egg_save_load_and_deliver_once_room_is_made` |
| Phiên read-only | Đã chạy | phiên thứ hai không có khóa ghi: lệnh bị bỏ qua, không autosave, **không nhập save**, file trên đĩa không đổi | `A_second_session_on_a_late_game_save_is_read_only_and_never_writes_or_imports` |
| Lỗi nhập save | Đã chạy | 7 loại lỗi trả đúng mã, game hiện tại và kho không đổi | `Import_errors_are_reported_with_their_code_and_never_touch_the_current_game` |
| Trạng thái 500 rồng | Đã chạy | save 224 686 byte (4,3 % trần 5 MB), ghi 12 ms, đọc + kiểm 62 ms | `Five_hundred_dragons_…` |
| > 500 rồng trong save | Đã chạy (ghi nhận hành vi) | validator **chấp nhận** save 501 rồng; mọi lệnh tạo rồng mới bị chặn `storage-full` | `A_save_above_500_dragons_is_reported_as_observed_behaviour` — xem R1 |
| Save 500 rồng sau 365 ngày chơi thật | Mô phỏng | Breeder chạm trần 500 (U2.12 F13); không lỗi sổ trong mô phỏng U2.14 | [ECONOMY_ALPHA.md](ECONOMY_ALPHA.md) |
| Thiết bị thật (iOS/Android), bộ nhớ flash, kill app thật | **Chưa kiểm thử** | ngoài phạm vi Domain/Persistence; CI có `kill-test.sh` (SIGKILL tiến trình .NET trên Linux) | — |
| Save giữa hai phiên bản app (downgrade) | Đã chạy từ trước | `newer-version` → phiên dừng, không ghi đè (`SessionTests`) | không đổi |

## 2. Save tại các giai đoạn gameplay (seed 1)

| Giai đoạn (mốc đầu tiên đạt) | Ngày | Rồng | Kích thước save | Đọc + kiểm |
|---|---|---|---|---|
| Mở đủ sinh cảnh Khởi Nguyên | 0,00 | 1 | 2 814 B | < 1 ms |
| Hoàn thành tutorial | 0,00 | 2 | 4 814 B | < 1 ms |
| Mua gói bằng vàng | 0,00 | 2 | 5 032 B | < 1 ms |
| Bắt đầu lai | 1,46 | 6 | 11 394 B | 1 ms |
| Nở rồng lai | 1,46 | 7 | 11 609 B | 1 ms |
| Mở Thủy Vực | 3,46 | 9 | 14 853 B | 1 ms |
| Nhận chuyến rạn đầu tiên | 5,17 | 14 | 20 811 B | 2 ms |
| Dự án Sanctuary đầu tiên | 5,46 | 14 | 21 037 B | 2 ms |
| Nhiệm vụ Thân thiết (Close Bond) | 6,17 | 15 | 22 361 B | 3 ms |
| Mở Thiên Giới | 9,17 | 18 | 25 149 B | 3 ms |
| Cuối ngày 40 | 40 | 50 | 43 791 B | 5 ms |

Mỗi save trên có `schemaVersion = 10`, checksum SHA-256 hợp lệ, `GameStateValidator` hợp lệ. Viên thức ăn đang rơi không lưu (ADR 0004 #10) nên so sánh dùng `SaveSchema.ToPersistedState`.

## 3. Lỗi nhập save

| Tình huống | Mã lỗi (`SaveErrorCode.ToWire()`) | Game hiện tại | Ghi đĩa |
|---|---|---|---|
| Không phải JSON | `corrupt-json` | không đổi | không |
| File bị cắt 40 byte cuối | `corrupt-json` | không đổi | không |
| JSON đúng cú pháp, sai định dạng envelope | `bad-format` | không đổi | không |
| Sửa tay state, giữ checksum cũ | `bad-checksum` | không đổi | không |
| Save phiên bản 11 (mới hơn app) | `newer-version` | không đổi | không |
| Thêm trứng Aurorawing giả vào `grantedRewards`, tính lại checksum | `invalid-state` (validator U2.13: trứng trước mốc 15) | không đổi | không |
| File > 5 MB | `too-large` | không đổi | không |
| Đối chứng: save hợp lệ của hành trình (ngày 3,46) | — (có bản xem trước) | chờ xác nhận | chỉ khi xác nhận |

## 4. Rủi ro và đề xuất (không đổi trong U2.14)

| # | Rủi ro | Mức | Đề xuất | Cần duyệt |
|---|---|---|---|---|
| R1 | Validator không có trần số rồng: save sửa tay / nhập có 501+ rồng vẫn nạp được. Game không tự tạo trạng thái này (mọi lệnh tạo rồng chặn ở 500) | thấp | thêm kiểm `dragons ≤ 500 + rồng trong tổ/gói chờ` vào validator ở bản sau; cần xác nhận không có save thật nào vượt (chưa thấy trong mô phỏng) | có (đổi luật nạp save) |
| R2 | Đọc save 500 rồng 62 ms trên máy dev; trên điện thoại tầm trung có thể 3–5 lần chậm hơn (ước lượng, **chưa đo**) | thấp | AI tích hợp đo trên thiết bị Alpha; nạp save ở màn hình chờ, không ở frame gameplay | không |
| R3 | Offline đúng trần 24 h × 75 %: người chơi vắng 30 h mất 6 h | thiết kế | giữ (ADR 0010) | — |

## 5. Hướng dẫn chạy

```bash
dotnet test "Assets/Dravio/Tests/Persistence/DotNet~/Dravio.Persistence.Tests.csproj" --filter "FullyQualifiedName~U214" --logger "console;verbosity=detailed"
```

Dòng `STAGE|…`, `OFFLINE|…`, `FULL500|…`, `OVER500|…`, `IMPORT|…` trong log là nguồn của các bảng trên.
