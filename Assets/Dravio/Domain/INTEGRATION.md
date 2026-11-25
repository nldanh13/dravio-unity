# Dravio Domain — Hướng dẫn tích hợp Unity (tới U2.8)

Tầng Unity **không tự tính luật**. Có hai việc duy nhất:

1. **Gửi lệnh** qua `GameSession.Send(id => new XxxCommand(id, …))`. Phiên tự gán `requestId`, quyết toán thời gian, lưu ngay các giao dịch quan trọng và phát `DomainEvents`.
2. **Đọc state** qua API truy vấn chỉ đọc `Dravio.Domain.Queries.Ecosystem` (cùng các hàm kiểm của Domain), trên `GameSession.State`.

Domain là C# thuần (không `UnityEngine`). Mọi DTO là snapshot, không giữ tham chiếu vào `GameState`, nên an toàn khi giữ một frame.

## 1. Vòng đời

```csharp
var session = new GameSession(new SessionEnvironment { Clock = clock, Store = store, Scheduler = scheduler, WriterLock = writerLock });
session.DomainEvents += events => { foreach (var e in events) Presenter.Handle(e); };  // hoạt ảnh, âm thanh, toast
session.Changed += () => Hud.Refresh(session.State);                                     // vẽ lại từ state
session.Start();
```

`Send` trả `DomainError` (null = thành công). Hiển thị bằng khóa i18n `error.MessageKey`, tức `commandError.<code>`.

## 2. API truy vấn (`Dravio.Domain.Queries.Ecosystem`)

| Hàm | Trả về | Dùng cho |
|---|---|---|
| `World(GameState s)` | `List<ZoneOverview>` | Bản đồ 3 thế giới: mở/khóa, sức chứa/đang dùng, microhabitat, trang trí, cạnh WorldGraph đang mở |
| `Habitat(GameState s, string habitatId)` | `HabitatOverview` | Môi trường (nhiệt, ẩm, mặn, sâu, cao, chỗ đậu), luật mở, rồng trong sinh cảnh, số đang sản xuất |
| `TravelOptions(GameState s, string uid)` | `List<TravelOption>` | Mọi microhabitat kèm suitability, chỗ trống, lý do chặn, đường đi |
| `TravelEligibility(GameState s, string uid, MoveTarget target)` | `DomainErrorCode?` | Đúng mã lỗi `moveDragon` sẽ trả |
| `ExpeditionOptions(GameState s)` | `List<ExpeditionOption>` | 3 tuyến: thời lượng, điều kiện, vật liệu, khóa/slot bận, ứng viên kèm lý do |
| `ExpeditionEligibility(s, routeId, uid, out string detail)` | `DomainErrorCode?` | Đúng mã lỗi `startExpedition` sẽ trả; `detail` = `not-adult` / `requires-land` / `requires-swim` / `requires-fly` |
| `Discovery(GameState s)` | `DiscoveryProgress` | Khám phá, luật mở sinh cảnh, hồ sơ sinh học đã xem, Bách khoa (loài/ô), thế giới đã mở, thành tựu |
| `Summary(GameState s)` | `EcosystemSummary` | Đếm rồng: sống, đang ở sinh cảnh, sản xuất, kho, care, shelter, lai, thám hiểm, đã thả; ngân sách; kho; G/s; chỗ theo zone |
| `DragonsInZone(s, zoneId)` / `DragonsWithStatus(s, status)` | `List<DragonSnapshot>` | Danh sách cho 3D/UI |
| `Actions(GameState s, string uid, long nowMs)` | `DragonActions` | Mọi hành động trên một rồng và lý do bị khóa (chuyển, sản xuất, bán + giá, khóa, lai, thám hiểm theo tuyến) |
| `StatusOf(s, dragon)` / `Snapshot(s, dragon)` | `DragonStatus` / `DragonSnapshot` | Trạng thái dẫn xuất và vàng/giây hiện tại |

Các truy vấn khác đã có từ trước:

| Hàm | Dùng cho |
|---|---|
| `Zones.HabitatUnlockStatus(s)` | Trạng thái mở sinh cảnh |
| `StarterPack.PackPlacementOptions(s)` | Chỗ đặt trứng chờ nhận |
| `Hatchery.Preview(s, a, b, nowMs, out err)` | Xem trước cặp lai: không tiêu RNG |
| `Hatchery.NestPlacementOptions(s)` | Chỗ đặt rồng con |
| `SanctuaryRules.BulkSellQuoteMicro(s)` | Tổng tiền bán nhanh, truyền lại vào `BulkSellCommand` |
| `Tutorial.CurrentStep(s.Tutorial)` | Bước hướng dẫn hiện tại |

Ví dụ:

```csharp
GameState s = session.State;
foreach (ZoneOverview z in Ecosystem.World(s))
    worldMap.SetZone(z.ZoneId, z.Unlocked, $"{z.Occupancy}/{z.Capacity}");

foreach (TravelOption o in Ecosystem.TravelOptions(s, uid))
    picker.Add(o.HabitatId, enabled: o.Blocked == null, tooltip: o.Blocked?.ToWire(), score: o.Score);

DragonActions a = Ecosystem.Actions(s, uid, clock.NowMs());
sellButton.interactable = a.Sell == null;  // giá: a.SellPriceG

var routes = Ecosystem.ExpeditionOptions(s);  // chọn tuyến + rồng có Blocked == null
session.Send(id => new StartExpeditionCommand(id, "origin_forest", uid));
```

## 3. Lệnh (tới U2.8)

| Lệnh | Nguồn | Lưu ngay |
|---|---|---|
| `AdvanceTime`, `Feed`, `ConsumeFood`, `Pet`, `RenameDragon` | Port TS (U2.1) | — |
| `MoveDragon`, `SetProductionSlot`, `UnlockHabitat`, `SurveyHabitat`, `ViewBiologyProfile` | Port TS (U2.3); U2.8 thêm kiểm đường đi | — |
| `BuyTutorialPack`, `ClaimPack`, `SetTutorialStatus` | Port TS (U2.4) | mua / nhận |
| `BuyPack`, `StartBreed`, `TapEgg`, `ClaimBreed` | Mới (U2.5) | mua / lai / nhận |
| `SellDragon`, `BulkSell`, `LockDragon`, `BuyUpgrade`, `BuyDecoration`, `ToggleDecoration` | Mới (U2.7) | trừ `ToggleDecoration` |
| `StartExpedition(routeId, dragonUid)`, `ClaimExpedition()` | Mới (U2.8) | cả hai |

**Không có lệnh mở thế giới** (Thủy Vực/Thiên Giới). Luật mở GDD gọi là "qua nhiệm vụ" chưa được định nghĩa. Hàm engine `WorldExpansion.ApplyZoneUnlock(state, zoneId, events)` dành cho hệ nhiệm vụ tương lai, không dành cho UI.

## 4. Mã lỗi U2.8 (`DomainErrorCode`, wire `commandError.<code>`)

| Mã | Khi nào |
|---|---|
| `no-route` | Không có đường WorldGraph tới zone đích: thiếu kiểu di chuyển (bơi/bay), hoặc cạnh/cửa ngõ chưa mở. Rồng trong kho xuất phát từ Khởi Nguyên |
| `unknown-route` | ID tuyến thám hiểm không có trong catalog |
| `route-locked` | Thế giới của tuyến chưa mở |
| `expedition-busy` | Slot thám hiểm P0 (1) đang dùng hoặc thưởng chưa nhận |
| `dragon-unsuited` | Rồng chưa Adult hoặc không hợp điều kiện tuyến (xem `detail`) |
| `no-expedition` | Không có chuyến nào để nhận |
| `expedition-not-ready` | Chuyến đi chưa xong |

Mã dùng lại: `dragon-busy` (đang lai/thám hiểm/care/shelter), `dragon-unavailable` (đã thả, hoặc `moveDragon` khi bận), `zone-locked`, `habitat-locked`, `habitat-unsuitable`, `zone-full`, `already-there`.

## 5. Domain event U2.8

| Event | Trường | Gợi ý trình bày |
|---|---|---|
| `zoneUnlocked` | `ZoneId` | Mở khóa vùng trên bản đồ; đi kèm các `habitatUnlocked` |
| `expeditionStarted` | `ExpeditionId, RouteId, DragonUid, FinishAtMs` | Rồng rời khu (vẫn giữ chỗ), hiện đồng hồ đếm ngược |
| `expeditionReady` | `ExpeditionId, AtMs` | Rồng quay về; có thể đến trong lần quyết toán offline |
| `expeditionClaimed` | `ExpeditionId, Rewards (vật liệu → số)` | Hoạt ảnh nhận vật liệu |
| `dragonMoved` | `DragonUid, From, To` | Di chuyển, kể cả giữa thế giới |

Event cũ vẫn giữ nguyên, xem `Core/DomainEvents.cs`.

## 6. Luật cần biết khi làm 3D

- **Sinh vàng**: rồng chỉ sinh vàng khi ở microhabitat hợp lệ, nằm trong ngân sách và đang rảnh. Đang lai hoặc thám hiểm thì không sinh vàng. Mốc chuyển trạng thái được quyết toán chính xác trong Domain; UI không cần tự tính.
- **Rồng đi thám hiểm** vẫn giữ `Location` cũ, vẫn chiếm chỗ trong khu, `Activity = Expedition`. 3D nên ẩn mô hình hoặc hiện "đang đi" theo `DragonStatus.Expedition`.
- **Chỉ hai cạnh WorldGraph**: `origin_ocean_estuary` (cần bơi, cửa ngõ bờ hồ ↔ cửa sông) và `origin_sky_low_island` (cần bay, đồng cỏ ↔ đảo thấp). Không có đường Thủy Vực ↔ Thiên Giới trực tiếp.
- **Không có quảng cáo**, không có phần thưởng quảng cáo.
- **Offline**: không sinh thiên tai, không tử vong (chưa có hệ thiên tai, U2.6). Ấp trứng và thám hiểm vẫn hoàn tất offline.
