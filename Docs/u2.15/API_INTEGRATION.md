# U2.15a — API Domain cho AI 1: luật đã duyệt (sky_cloud A, cho ăn B)

> Nền: [U2.14 API](../u2.14/API_INTEGRATION.md) → [U2.13 API](../u2.13/API_INTEGRATION.md) → U2.12 (mục 0 "Quy tắc tích hợp" vẫn bắt buộc).
> Chủ dự án duyệt ngày 2026-11-26 (issue #74). **Save Schema v10 giữ nguyên, không migration.** Không thêm mã lỗi mới. Không nguồn vàng hay XP mới.
> Giá Sanctuary, công thức vàng và nguồn thu nhập **không đổi** (nghiên cứu kinh tế nằm ở PR riêng, issue #75).

## 0. Trạng thái luật sau U2.15a

| Hệ | Trong game | UI làm gì |
|---|---|---|
| Tuyến `sky_cloud` "Sân mây Thiên Giới" | **BẬT, bảng A**: 45 phút, 2–4 `stardust` mỗi chuyến, rồng phải biết bay và là Adult | thẻ tuyến mở: `RouteExplorationDto.RewardsDefined == true`, `Materials == ["stardust"]` |
| Mốc `sky_cloud` | chuyến nhận thứ 1 / 5 / 15 / 40 → +2 / 3 / 4 / 6 bụi sao, mỗi mốc một lần | hiện mốc kế tiếp: `NextMilestoneAt`, `NextMilestoneBonusEach`, `NextMilestoneRewardId`, `NextMilestoneEggSpeciesId` |
| Trứng Aurorawing | đúng **một** trứng ở chuyến `sky_cloud` nhận thứ 15 (`EXPEDITION_EGG_AURORAWING`) | ở mốc 15, `NextMilestoneEggSpeciesId == "aurorawing"`; event `RewardEggGranted` (mục 1.3) |
| Vật liệu `stardust` | `Active == true` | dự án Thiên Giới đòi bụi sao (gấp đôi số lượng thay cho hai loại); chưa mở Thiên Giới thì lùi về vật liệu thế giới trước (`ProjectMaterials.Fallback == true`) như U2.13 |
| Cho ăn | **luật B**: `feed` có `targetDragonUid` tùy chọn | gửi UID rồng đang chọn (mục 2) |
| Bộ sưu tập | 16 / 16 loài có nguồn, 17 / 17 nhiệm vụ đạt được (kiểm toán catalog) | Codex hiện nguồn Aurorawing là "chuyến sân mây thứ 15" |

Không gọi `SkyCloudBalance.Use(...)` ngoài test. `SkyCloudBalance.Disabled` chỉ còn là mốc đối chứng U2.14 trong test.

## 1. Thám hiểm Thiên Giới

### 1.1 Lệnh (không đổi chữ ký)

```csharp
new StartExpeditionCommand(requestId, "sky_cloud", dragonUid)
new ClaimExpeditionCommand(requestId)
```

| Mã lỗi khi bắt đầu | Khi nào |
|---|---|
| `route-locked` | Thiên Giới chưa mở |
| `expedition-busy` | đã có một chuyến (một slot chung cho cả ba tuyến) |
| `unknown-dragon` | UID không tồn tại |
| `dragon-unavailable` | rồng đã thả |
| `dragon-busy` | rồng đang lai / thám hiểm / chăm sóc / trú ẩn |
| `dragon-unsuited` (detail `requires-fly`, `not-adult`) | rồng không bay hoặc chưa Adult |
| `vitals-below-threshold` | sức khỏe dưới ngưỡng thám hiểm |

Thứ tự kiểm: tuyến → slot → rồng (`Expeditions.CheckStart`). Trước khi bấm, UI dùng `Ecosystem.ExpeditionOptions(state)` / `Ecosystem.Actions(state, uid, nowMs).Expeditions` (trả đúng mã lệnh sẽ trả).

`route-rewards-undefined` **không còn xảy ra** với `sky_cloud` trong game thật. UI có thể vẫn giữ nhánh xử lý mã này (vô hại).

### 1.2 Event khi nhận

| Event | Trường | Ghi chú |
|---|---|---|
| `ExpeditionClaimed` | `ExpeditionId`, `Rewards` = `[("stardust", 2..4)]` | số lượng đã định từ lúc bắt đầu (`State.Expedition.Rewards`) |
| `ExplorationMilestoneReached` | `RouteId = "sky_cloud"`, `Claims` ∈ {1, 5, 15, 40}, `Bonus = [("stardust", 2/3/4/6)]`, `RewardId` | `note.cloud_first_flight` (1), null (5), `title.cloud_rider` (15), `title.star_warden` (40) |
| `RewardEggGranted` | `RewardId = "EXPEDITION_EGG_AURORAWING"`, `SpeciesId = "aurorawing"`, `Delivered` | chỉ ở chuyến 15, một lần / save |
| `RewardEggDelivered` | `RewardId`, `PackId` | khi trứng ra khỏi hàng chờ (có thể ở lệnh sau) |

### 1.3 Hàng chờ trứng

- `Delivered == true`: trứng nằm ngay ở `State.PendingPack` (Normal, `PackId = "EXPEDITION_EGG_AURORAWING"`), mở bằng `claimPack(target)` như trứng thế giới.
- `Delivered == false`: đang có trứng chờ mở (`PendingPack != null`) hoặc kho đủ 500 rồng. Trứng nằm trong `State.RewardQueue` và tự được giao sau lệnh kế tiếp có chỗ (mở trứng đang chờ, bán / thả rồng). UI hiện "Trứng Aurorawing đang chờ" khi `RewardQueue` có `SpeciesId == "aurorawing"`. Trứng không bao giờ mất, kể cả qua save / load.
- Bụi sao không bị kho rồng chặn: kho đầy vẫn nhận đủ bụi sao.

### 1.4 Rủi ro đã biết (không đổi Domain)

Bán Aurorawing duy nhất = mất loài vĩnh viễn (không trứng thứ hai, không cặp lai nào khác; Bách khoa vẫn giữ). Domain không chặn vì chưa có duyệt. **Đề nghị UI:** hộp xác nhận khi bán / thả rồng thưởng cuối cùng của loài, gợi ý `lockDragon`.

## 2. Cho ăn — luật B

### 2.1 Lệnh

```csharp
// Mới (U2.15): ưu tiên rồng đang chọn
new FeedCommand(requestId, dietId, zoneId, habitatId, targetDragonUid)

// Cũ, vẫn hợp lệ, = targetDragonUid null = luật A đúng như U2.14
new FeedCommand(requestId, dietId, zoneId, habitatId)
```

| `targetDragonUid` | Kết quả lệnh |
|---|---|
| `null` | như U2.14 (luật A) |
| chuỗi 1–64 ký tự | lệnh **không bị từ chối vì mục tiêu**, kể cả khi UID không tồn tại, rồng bận, sai chỗ, đang hồi; mục tiêu chỉ là ưu tiên |
| `""` hoặc > 64 ký tự | `invalid-command` (sai dạng, không đổi state) |

Các lỗi khác của `feed` không đổi: `zone-locked`, `habitat-locked`, `food-too-fast` (< 800 ms), `food-limit` (5 + cấp nâng cấp).

Dạng dây (wire, JSON lệnh): thêm khóa tùy chọn `"targetDragonUid"`; thiếu khóa = null.

### 2.2 Ai ăn (giao phần ăn)

`BehaviorEngine.AssignFood(state, zoneId, habitatId, nowMs)` → `uid → foodId`. Hai lượt, drop xét theo hết hạn sớm trước rồi ID:

1. Drop có mục tiêu: giao cho chính mục tiêu nếu nó **ăn được** — `BehaviorEngine.CanEat(dragon, drop, nowMs)`: ở đúng zone + microhabitat của drop, `Activity == Idle`, đã qua 8 s hồi từ bữa trước, loài ăn được `dietId`, drop còn hạn — và chưa được giao drop khác.
2. Mọi drop còn lại: luật A (rồng thích đúng loại đó trước, rồi UID ordinal), chỉ trong các rồng chưa được giao.

Không drop nào có mục tiêu → kết quả trùng luật A từng ô (đã kiểm 200 đồng cỏ ngẫu nhiên).

Điều kiện được **đánh giá lại mỗi lần hỏi**: ví dụ mục tiêu đang hồi 8 s lúc thả, rồng khác được giao; khi hết hồi (còn trong 12 s của phần ăn) thì mục tiêu được giao lại. UI nên hỏi lại ở `BehaviorDecision.UntilMs` hoặc khi state đổi, như với mọi quyết định hành vi.

### 2.3 Truy vấn cho UI

```csharp
FoodAssignment a = BehaviorEngine.AssignmentOf(state, foodId, nowMs); // null: không có drop đó
```

| Trường | Ý nghĩa |
|---|---|
| `FoodId` | ID drop (`"food-" + requestId` của lệnh feed) |
| `TargetDragonUid` | mục tiêu trên lệnh (null = không ưu tiên) |
| `AssignedDragonUid` | rồng sẽ "tìm ăn" lúc này; null = chưa ai |
| `Source` | `Target` (luật B), `Fallback` (luật A), `None` (chưa ai ăn được) |
| `TargetBlockedReason` | khi có mục tiêu mà không nhận: `unknown-dragon`, `released`, `wrong-place`, `busy`, `cooldown`, `wrong-diet`, `food-expired`, `target-has-other-food`; null nếu không mục tiêu hoặc mục tiêu nhận |

Hàm thuần, không đổi state. Gợi ý hiển thị: `Source == Fallback` + `busy` → "Rồng đang bận, rồng khác sẽ ăn"; `cooldown` → "Rồng vừa ăn, đợi chút".

`BehaviorEngine.Decide(state, uid, nowMs, phase)` của mục tiêu trả `Behavior = SeekFood`, `TargetFoodId = foodId` khi được giao (luật B dùng chung `AssignFood`).

### 2.4 Xác nhận ăn (không đổi chữ ký)

```csharp
new ConsumeFoodCommand(requestId, foodId, dragonUid)
```

Vẫn chấp nhận **bất kỳ** rồng nào ăn được (va chạm thật của Sanctuary), không chỉ rồng được giao. Luồng chuẩn: dẫn `AssignedDragonUid` tới phần ăn rồi gửi `consumeFood` với đúng UID đó.

### 2.5 Event

| Event | Trường mới (U2.15) | Ý nghĩa |
|---|---|---|
| `FoodSpawned` | `TargetDragonUid` (string, null được) | mục tiêu ghi trên lệnh; **không** bảo đảm rồng đó ăn |
| `FoodConsumed` | `TargetDragonUid`, `AteTarget` (bool) | `DragonUid` = người nhận thật; `AteTarget == true` khi có mục tiêu và chính nó ăn |

Không có event "giao phần ăn": giao là truy vấn (mục 2.3), không phải thay đổi state. Constructor cũ của hai event vẫn còn (mục tiêu null).

### 2.6 Không đổi

- Một phần ăn = một bữa; XP mỗi bữa (`CareRules.FeedXpSec`), hồi 8 s, sống 12 s, `TotalFeedClicks` giữ nguyên. Không phần ăn thêm, không XP thêm, không vàng.
- Phần ăn đang rơi (kể cả mục tiêu) **không vào save** (ADR 0004 #10): nạp lại thì không còn drop, cho ăn lại như thường. Schema v10.
- Gửi lại cùng `requestId` → `Duplicate`, không drop thứ hai.

### 2.7 Thay đổi UI tối thiểu (phần của AI 1)

`Assets/Dravio/Scripts/LivingSanctuaryPresentation.cs`, hàm cho ăn của rồng đang chọn: truyền UID vào lệnh.

```csharp
Send(id => new FeedCommand(id, diet, d.Location.ZoneId, d.Location.HabitatId, d.Uid));
```

Sau đó đọc `BehaviorEngine.AssignmentOf(...)` để biết ai tới ăn. Cách tạm của U2.14 mục 3 (chọn loại thức ăn chỉ mục tiêu thích) không còn cần thiết. U2.15a không sửa file UI này.

## 3. Bảo đảm đã kiểm (xem [TEST_REPORT_U215a.md](TEST_REPORT_U215a.md))

| Bảo đảm | Test |
|---|---|
| Production = bảng A, 45 phút, 2–4 bụi sao, không vàng / XP | `U215SkyCloudProductionTests` |
| Mỗi mốc 1 / 5 / 15 / 40 và trứng Aurorawing đúng một lần; gửi lại requestId không trả thêm | `U215SkyCloudProductionTests`, `U214AlphaJourneyTests` |
| Kho đầy / đang có trứng chờ → trứng xếp hàng rồi được giao | `U215SkyCloudProductionTests` |
| Save cũ (không bụi sao, không bộ đếm sky) nạp được và nhận trứng ở chuyến 15; save giữa chừng cho cùng kết quả | `U215ApprovedRulesSaveTests` |
| Luật B: null / sai dạng / không tồn tại / bận / sai chỗ / hồi / sai chế độ ăn / hết hạn / offline / trùng / thứ tự UID; phát lại log | `U215FeedingRuleBTests` |
| Lệnh cũ = luật A; golden TS b33ec7a giữ nguyên | `U215FeedingRuleBTests`, CI `typescript-parity` |
