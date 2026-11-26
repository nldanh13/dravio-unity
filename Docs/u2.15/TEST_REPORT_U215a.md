# U2.15a — Báo cáo kiểm thử: luật đã duyệt (sky_cloud A, cho ăn B)

> Issue #74. Nhánh dựng trên PR #73 (U2.14). Save Schema v10, không migration. Không đổi giá Sanctuary, công thức vàng hay nguồn thu nhập.
> "Đã chạy" = test thật bằng lệnh chính thức trên luật production. "Mô phỏng" = số liệu U2.14 (365 ngày) không chạy lại ở đây.

## 1. Thay đổi production

| File | Thay đổi |
|---|---|
| `Domain/Catalog/SkyCloudBalance.cs` | `Production = ProposedA` (một dòng + tài liệu) |
| `Domain/Core/Commands.cs` | `FeedCommand` thêm constructor 5 tham số, `TargetDragonUid` tùy chọn; constructor 4 tham số giữ nguyên = null |
| `Domain/Model/GameState.cs` | `FoodDrop.TargetDragonUid` (chỉ bộ nhớ, không ghi save) |
| `Domain/Core/CommandDispatcher.cs` | `feed` chép mục tiêu vào drop; event mang mục tiêu |
| `Domain/Core/DomainEvents.cs` | `FoodSpawned.TargetDragonUid`, `FoodConsumed.TargetDragonUid` + `AteTarget`; constructor cũ giữ nguyên |
| `Domain/Behavior/BehaviorEngine.cs` | `AssignFood` hai lượt (luật B rồi luật A); truy vấn mới `AssignmentOf` + `FoodAssignment`, `FoodAssignmentSource` |

Không đổi: `Persistence/**` (GameStateJson không ghi trường mới), `Scripts/**` (UI thuộc AI 1), thư mục `Platform`, `Character`.

## 2. Kết quả test (cục bộ, .NET 8.0.134, Linux)

| Bộ | Kết quả | Trước U2.15a |
|---|---|---|
| Domain | **568 đạt**, 13 bỏ qua (báo cáo theo yêu cầu), 0 lỗi | 543 đạt |
| Persistence | **220 đạt**, 0 lỗi | 217 |
| Character | 54 đạt | 54 |
| TypeCheck (stub UnityEngine) | build đạt | đạt |
| Integration (Living Sanctuary) | 146 đạt | 146 |
| TypeScript parity (golden b33ec7a, TS đọc save C#) | chạy trên CI (cần repo TS) | đạt |

Test mới: `U215FeedingRuleBTests` (18), `U215SkyCloudProductionTests` (7), `U215ApprovedRulesSaveTests` (3).
22 test cũ kỳ vọng "tuyến tắt" được cập nhật: kỳ vọng production mới, và kỳ vọng cũ giữ lại trong phạm vi `SkyCloudBalance.Use(SkyCloudBalance.Disabled)` làm đối chứng U2.14 (không xóa test nào, không bỏ qua test nào).

## 3. Bảng A trong game thật (đã chạy)

| Kiểm tra | Kết quả |
|---|---|
| 120 save khác nhau, một chuyến `sky_cloud` | 45 phút; chỉ `stardust`; 2, 3, 4 đều xuất hiện; Δvàng = thưởng thành tựu sẵn có (chuyến đầu tiên), tuyến không trả vàng; XP không tăng do nhận |
| 41 chuyến liên tiếp | mốc đúng 1 / 5 / 15 / 40 → +2 / 3 / 4 / 6, ghi chú / danh hiệu `note.cloud_first_flight`, `title.cloud_rider`, `title.star_warden`; sổ bụi sao = Σ chuyến + 15; trứng Aurorawing đúng 1 |
| +20 chuyến sau đó | không mốc, không trứng |
| Gửi lại requestId nhận / nhận lần 2 | `Duplicate` không event / `no-expedition`; không trả thêm |
| Đang có trứng chờ mở ở chuyến 15 | `RewardEggGranted.Delivered = false`, trứng vào `rewardQueue`; mở trứng đang chờ → `RewardEggDelivered`, rồi nở đúng 1 Aurorawing |
| Kho đủ 500 rồng ở chuyến 15 | trứng xếp hàng; bụi sao vẫn nhận đủ |
| Phát lại log 32 lệnh | state canonical trùng từng byte |
| Kiểm toán catalog | 17 / 17 nhiệm vụ, 16 / 16 loài có nguồn, 0 lỗi catalog |

### 3.1 Hành trình Alpha 40 ngày (Guided, 3 × 10 phút/ngày, lệnh thật, đã chạy)

| Seed | Mở Thiên Giới | `Q_EXPEDITION_CLOUD` | Trứng Aurorawing | Nhiệm vụ | Vi phạm sổ |
|---|---|---|---|---|---|
| 1 | ngày 9,2 | ngày 9,5 | ngày 14,5 | 17 / 17 | 0 |
| 2 | ngày 8,5 | ngày 9,0 | ngày 14,0 | 17 / 17 | 0 |
| 3 | ngày 10,5 | ngày 11,0 | ngày 17,0 | 17 / 17 | 0 |

Đối chứng U2.14 (bảng tắt, seed 1): 16 / 17 nhiệm vụ, 0 chuyến sân mây, 0 bụi sao, không trứng — vẫn được kiểm.

**Thay đổi kỳ vọng hành trình:** mốc "rạn 15 chuyến" không còn đạt trong 40 ngày vì slot thám hiểm chung chuyển sang sân mây (chi phí cơ hội có chủ đích, Q7 U2.13). Hành trình nay kiểm mốc rạn 5 và mốc sân mây 15.

**Giới hạn:** đây là 3 hành trình của người chơi theo hướng dẫn. Kiểm toán 16 / 16 là "mỗi loài có nguồn", không phải "mọi lượt chơi đều thu đủ 16 loài". Người chơi ít phiên hơn nhận trứng muộn hơn (U2.14 mô phỏng 365 ngày: ngày 29–53 cho Researcher / Casual 5 phút/ngày; Casual 60 phút/ngày chỉ 1 / 3 seed tới Thiên Giới). Xem [../u2.14/BALANCE_DECISIONS.md](../u2.14/BALANCE_DECISIONS.md) mục 1.2.

## 4. Luật cho ăn B (đã chạy)

| Trường hợp | Kết quả |
|---|---|
| Lệnh cũ (4 tham số) / `targetDragonUid = null` | drop không mục tiêu; giao trùng luật A chép lại từ U2.14 trên 200 đồng cỏ ngẫu nhiên (1–6 rồng, 1–4 drop, có rồng đang hồi) |
| `""`, 65 ký tự | `invalid-command`, state không đổi |
| UID không tồn tại | lệnh đạt, luật A, lý do `unknown-dragon` |
| 5 Sproutling, chọn d4 / d2 / d0 | đúng rồng được chọn được giao và `SeekFood` (luật A luôn chọn d0) |
| Rồng khác thích loại đó | mục tiêu vẫn thắng (mọi cặp mục tiêu × chế độ ăn của 5 loài) |
| Mục tiêu đang thám hiểm | luật A, `busy`, không phí phần ăn |
| Mục tiêu trong kho / sinh cảnh khác | luật A, `wrong-place` |
| Mục tiêu đang hồi 8 s | luật A lúc đầu (`cooldown`); hết hồi trước khi drop hết hạn → mục tiêu được giao lại |
| Mục tiêu không ăn loại đó | luật A, `wrong-diet` |
| Hết hạn | không ai, `food-expired`; lệnh kế tiếp dọn drop (`FoodExpired`) |
| Offline 1 giờ | drop hết hạn, không tự ăn, không XP |
| Hai drop cùng mục tiêu | drop hết hạn sớm cho mục tiêu, drop kia luật A (`target-has-other-food`) |
| Drop không mục tiêu thả trước | drop có mục tiêu vẫn nhận mục tiêu (lượt 1 trước lượt 2) |
| Gửi lại requestId `feed` | `Duplicate`, không drop thứ hai, `TotalFeedClicks` +1 một lần |
| Event | `FoodSpawned.TargetDragonUid`; `FoodConsumed.DragonUid` = người nhận thật, `AteTarget` đúng cả khi rồng khác ăn |
| Có / không mục tiêu, cùng lệnh | XP mỗi bữa, vàng, số drop, `TotalFeedClicks`, kho vật liệu bằng nhau; chỉ đổi ai ăn |
| Log 120 lệnh feed (có / không / mục tiêu sai) + consumeFood | phát lại trùng canonical; dạng dây giữ `targetDragonUid`, thiếu khóa = null |
| Truy vấn | `AssignFood`, `AssignmentOf`, `DecideAll` không đổi state |
| Save có drop mang mục tiêu | text không chứa `targetDragonUid`; nạp lại Schema v10, không drop; cho ăn lại vẫn ưu tiên |

Parity TS: golden C# chỉ ghi `targetDragonUid` khi khác null, nên golden b33ec7a không đổi; CI `typescript-parity` kiểm byte.

## 5. Save / load (đã chạy)

| Kiểm tra | Kết quả |
|---|---|
| Save "U2.14" (không bụi sao, không bộ đếm sky) | nạp được; 15 chuyến production → đúng 1 trứng, ≥ 39 bụi sao |
| Lưu / nạp sau mốc 15, bay thêm 3 chuyến | không trao lại |
| Lưu ở chuyến 10, nạp, bay tiếp 10 | bụi sao và `grantedRewards` trùng chạy liền 20 chuyến (RNG theo `saveId` + `revision`) |
| Bộ U2.13 `SkyCloudSaveTests`, U2.14 `U214SaveReliabilityTests` | đạt không sửa |

## 6. Rủi ro còn lại (không đổi Domain khi chưa duyệt)

| # | Rủi ro | Đề xuất |
|---|---|---|
| R1 | Bán Aurorawing duy nhất = mất loài vĩnh viễn | UI xác nhận + gợi ý khóa (API mục 1.4); chặn trong Domain cần duyệt |
| R2 | Bụi sao dư 800–3 000 / năm (mô phỏng U2.14) | theo dõi sau Alpha (Q4 U2.13); nằm trong nghiên cứu #75 |
| R3 | Chuyến rạn giảm mạnh vì slot chung | có chủ đích (Q7 U2.13); pha lê / san hô vẫn đủ cho dự án (0 % seed bị chặn, U2.14) |
| R4 | UI chưa gửi `targetDragonUid` | AI 1 sửa một dòng (API mục 2.7) |
