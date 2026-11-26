# Dravio Domain — Hướng dẫn tích hợp Unity (tới U2.6 / U2.8 / U2.9)

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
| `ExpeditionOptions(GameState s)` | `List<ExpeditionOption>` | 3 tuyến: thời lượng, điều kiện, vật liệu, khóa/chưa có thưởng/slot bận, ứng viên kèm lý do và `UncheckedConditions` |
| `ExpeditionEligibility(s, routeId, uid, out string detail)` | `DomainErrorCode?` | Đúng mã lỗi `startExpedition` sẽ trả; `detail` = `not-adult` / `requires-land` / `requires-swim` / `requires-fly` |
| `ZoneUnlocks(GameState s)` | `List<ZoneUnlockInfo>` | Thủy Vực/Thiên Giới: đã mở chưa, điều kiện còn thiếu, trứng thưởng đã trao/đang chờ |
| `Materials(GameState s)` | `List<MaterialStock>` | 5 vật liệu chuẩn: số lượng, đã kích hoạt chưa |
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
| `UnlockZone(zoneId)` | Mới (U2.8, ADR 0201) | có |

**Mở thế giới**: gọi `UnlockZone` khi `Ecosystem.ZoneUnlocks(s)` báo không còn điều kiện thiếu. Lỗi `unlock-requirement-missing` có `detail` là danh sách mã thiếu (`tutorial-completed`, `origin-habitats`, `zone:<id>`, `quest:<id>`). Nội dung nhiệm vụ là TBD, nên hiện chưa có gì trong game hoàn thành chúng.

## 4. Mã lỗi U2.8 (`DomainErrorCode`, wire `commandError.<code>`)

| Mã | Khi nào |
|---|---|
| `no-route` | Không có đường WorldGraph tới zone đích: thiếu kiểu di chuyển (bơi/bay), hoặc cạnh/cửa ngõ chưa mở. Kho là vùng trung lập: rời kho cần một cạnh đang mở đi vào thế giới đích mà rồng đi được |
| `unknown-route` | ID tuyến thám hiểm không có trong catalog |
| `route-locked` | Thế giới của tuyến chưa mở |
| `expedition-busy` | Slot thám hiểm P0 (1) đang dùng hoặc thưởng chưa nhận |
| `dragon-unsuited` | Rồng chưa Adult hoặc không hợp điều kiện tuyến (xem `detail`) |
| `no-expedition` | Không có chuyến nào để nhận |
| `expedition-not-ready` | Chuyến đi chưa xong |
| `route-rewards-undefined` | Tuyến chưa có bảng thưởng được duyệt (`sky_cloud`) |
| `unknown-zone` | `unlockZone` với zone lạ hoặc `origin` |

Mã dùng lại: `dragon-busy` (đang lai/thám hiểm/care/shelter), `dragon-unavailable` (đã thả, hoặc `moveDragon` khi bận), `zone-locked`, `habitat-locked`, `habitat-unsuitable`, `zone-full`, `already-there`.

## 5. Domain event U2.8

| Event | Trường | Gợi ý trình bày |
|---|---|---|
| `zoneUnlocked` | `ZoneId` | Mở khóa vùng trên bản đồ; đi kèm các `habitatUnlocked` |
| `expeditionStarted` | `ExpeditionId, RouteId, DragonUid, FinishAtMs` | Rồng rời khu (vẫn giữ chỗ), hiện đồng hồ đếm ngược |
| `expeditionReady` | `ExpeditionId, AtMs` | Rồng quay về; có thể đến trong lần quyết toán offline |
| `expeditionClaimed` | `ExpeditionId, Rewards (vật liệu → số)` | Hoạt ảnh nhận vật liệu |
| `dragonMoved` | `DragonUid, From, To` | Di chuyển, kể cả giữa thế giới |
| `questCompleted` | `QuestId` | Chỉ phát từ hàm engine (nhiệm vụ TBD) |
| `rewardEggGranted` | `RewardId, SpeciesId, Delivered` | `Delivered = false`: trứng đang chờ vì kho đầy/đang có trứng |
| `rewardEggDelivered` | `RewardId, PackId` | Trứng thưởng thành trứng chờ nhận; nhận bằng `ClaimPack` |

Event cũ vẫn giữ nguyên, xem `Core/DomainEvents.cs`.

## 6. Luật cần biết khi làm 3D

- **Sinh vàng**: rồng chỉ sinh vàng khi ở microhabitat hợp lệ, nằm trong ngân sách và đang rảnh. Đang lai hoặc thám hiểm thì không sinh vàng. Mốc chuyển trạng thái được quyết toán chính xác trong Domain; UI không cần tự tính.
- **Rồng đi thám hiểm** vẫn giữ `Location` cũ, vẫn chiếm chỗ trong khu, `Activity = Expedition`. 3D nên ẩn mô hình hoặc hiện "đang đi" theo `DragonStatus.Expedition`.
- **Chỉ hai cạnh WorldGraph**: `origin_ocean_estuary` (cần bơi, cửa ngõ bờ hồ ↔ cửa sông) và `origin_sky_low_island` (cần bay, đồng cỏ ↔ đảo thấp). Không có đường Thủy Vực ↔ Thiên Giới trực tiếp.
- **Trang trí chỉ thẩm mỹ**: không ảnh hưởng vàng, XP, chỉ số hay sức chứa.
- **Không có quảng cáo**, không có phần thưởng quảng cáo.
- **Offline**: không sinh thiên tai, không gây thương, không tử vong (U2.6). Ấp trứng, thám hiểm và chăm sóc vẫn hoàn tất offline.

## 7. U2.6 — Sức khỏe, thiên tai, trú ẩn, chăm sóc

Luật và phần chưa duyệt: [ADR 0202](../../../Docs/adr/0202-u2-6-health-disaster-rescue.md). **Thiên tai tự nhiên đang tắt ở bản chạy thật.** Muốn thử luồng thiên tai trong bản cân bằng, truyền bộ luật đề xuất vào phiên. Không có lệnh nào cho người chơi tự tạo thiên tai.

```csharp
var env = new SessionEnvironment { /* … */ DisasterRules = DisasterRules.ProposedForReview };  // null = Production (tắt)
```

### 7.1 Truy vấn (`Dravio.Domain.Queries.HealthQueries`)

| Hàm | Trả về | Dùng cho |
|---|---|---|
| `Dragon(s, uid)` | `DragonHealthOverview`: `Vitals`, `OpenCaseId`, `BreedingShortfalls`, `ExpeditionShortfalls`, `HabitatScore/Tier`, `ExposedToWarning` | Hộ chiếu rồng, nút lai / thám hiểm, gợi ý "đưa vào nơi trú" |
| `Cases(s)` | `CaseOverview[]`: phase, `Progress` (‰), `QueuePosition`, `Symptoms` (chỉ sau nhận định), `NextStep` | Danh sách ca ở trạm |
| `CarePlans(s, caseId)` | `CarePlanOption[]`: `PlanId`, `Suitable`, `ExplanationKey` | Màn chọn kế hoạch, kể cả lựa chọn không phù hợp |
| `Station(s)` | `Slots` (2), `Used`, `Queue` | Sức chứa trạm |
| `DischargeEligibility(s, caseId, target)` | đúng mã lỗi `dischargeDragon` sẽ trả (null = được) | Tô xám đích xuất viện |
| `Disasters(s, rules?)` | `NaturalDisastersEnabled`, `PendingDecisions`, `NextCheckInForegroundMs`, sự kiện mở từng zone (`Phase`, `ForecastRemainingMs`, `Sheltered`, `CaseIds`, `CooldownUntilMs`) | Bảng cảnh báo |

`NextStep` của ca: `waitForStation` → `assessCase` → `setCarePlan|startCare` → `wait` → `reviewCase` → `dischargeDragon`.

### 7.2 Lệnh (đều lưu ngay)

| Lệnh | Điều kiện chính |
|---|---|
| `MoveToShelter(dragonUid)` | Zone của rồng có sự kiện ở Forecast / Preparation; rồng rảnh, chưa trú. Rồng ngừng sản xuất trong nơi trú |
| `AssessCase(caseId)` | Ca chưa nhận định (ca đang chờ cũng nhận định được) |
| `SetCarePlan(caseId, planId)` | Đã nhận định, chưa bắt đầu, kế hoạch đúng vị trí và phù hợp. Kế hoạch không phù hợp trả `care-plan-unsuitable` với `detail` = khóa giải thích; state không đổi |
| `StartCare(caseId)` | Đã nhận định, ca đang ở trạm (không chờ), chưa bắt đầu. Có kế hoạch phù hợp: thời lượng ¾ |
| `ReviewCase(caseId)` | Tiến trình 100 % |
| `DischargeDragon(caseId, target)` | Đã xem lại; đích kiểm như `moveDragon` từ kho (`zone-full`, `habitat-locked`, …). Kho luôn được. Xuất viện nhận ca chờ tiếp theo vào trạm |

### 7.3 Mã lỗi mới (`commandError.<code>`)

`vitals-below-threshold` (thám hiểm; `detail` = `health>=80` / `mobility>=80` / `comfort>=60` / `vitals-missing`), `breed-vitals-below-threshold`, `unknown-case`, `case-already-assessed`, `case-not-assessed`, `case-queued`, `care-already-started`, `care-not-started`, `care-not-complete`, `case-already-reviewed`, `case-not-reviewed`, `unknown-care-plan`, `care-plan-wrong-site`, `care-plan-unsuitable`, `no-disaster-warning`. Rồng có ca mở: `breed-busy` khi lai, `dragon-busy` khi bán / thám hiểm, `dragon-unavailable` khi `moveDragon`.

### 7.4 Domain event

| Event | Gợi ý trình bày |
|---|---|
| `hazardForecast` (`EventId, ZoneId, Hazard, Severity, ForecastMs`) | Banner cảnh báo + đếm ngược 10 phút (chỉ trôi khi online) |
| `disasterPhaseChanged`, `disasterStruck` (`Risks`, `CaseIds`), `disasterClosed` (`CooldownUntilMs`) | Hiệu ứng thiên tai; `Risks` để debug cân bằng |
| `dragonSheltered` (`Auto` = tự đưa khi offline/Zen), `dragonLeftShelter` (`To`, `ProductionRestored`) | Rồng vào / ra nơi trú |
| `healthCaseOpened` (`Queued`), `healthCaseAdmitted` | Rồng được đưa tới trạm hoặc hàng chờ |
| `healthCaseAssessed` (`Symptoms`), `carePlanSet`, `careStarted` (`DurationMs`, `PlanBonus`), `casePhaseChanged`, `careCompleted`, `healthCaseReviewed`, `dragonDischarged` | Từng bước chăm sóc |
| `vitalsChanged` | Cập nhật thanh chỉ số |

### 7.5 Khóa i18n mới

`carePlan.<planId>` và `carePlanWhy.<planId>` với `protect_wound_shelter`, `rest_protect_gradual`, `early_flight_training`, `clean_air_monitor`, `matched_pool_monitor`; `careSymptom.<id>` cho triệu chứng trong `HealthCatalog.Sites`; `injurySite.<site>` (`skin_scale`, `limb_wing`, `respiratory`, `fin_gill`); `carePhase.<phase>`; cùng các `commandError.*` ở 7.3.

### 7.6 Luật cần biết khi làm 3D

- Rồng ở trạm có `Location = care(caseId)`, rồng ở nơi trú có `Location = shelter(zoneId)`; cả hai `Activity = care` khi có ca. Rồng chỉ trú (không có ca) giữ `Activity = idle` nhưng không sản xuất.
- Không có tử vong. Kế hoạch không phù hợp không làm rồng xấu đi.
- Xuất viện không tự gán lại slot sản xuất (ADR 0202 D5).

## 8. U2.9 — Cá tính, hành vi, sinh thái sinh cảnh, nghiên cứu

Luật và phần chờ duyệt: [ADR 0203](../../../Docs/adr/0203-u2-9-dragon-behavior-personality-ecology-research.md). Ma trận 16 loài: [BEHAVIOR_MATRIX.md](BEHAVIOR_MATRIX.md). Báo cáo cân bằng: [BEHAVIOR_SIMULATION.md](BEHAVIOR_SIMULATION.md).

**Không có lệnh mới, không có event mới, không đổi save.** Mọi thứ là truy vấn chỉ đọc trên `GameSession.State`. Gọi bao nhiêu lần cũng được; cùng đầu vào cho cùng kết quả.

### 8.1 Truy vấn (`Dravio.Domain.Queries.BehaviorQueries`)

| Hàm | Trả về | Dùng cho |
|---|---|---|
| `All(s, nowMs, phase)` | `BehaviorDecision[]` cho mọi rồng chưa thả, thứ tự kho | Tầng Sanctuary, mỗi tick hoặc khi `UntilMs` đã qua |
| `Dragon(s, uid, nowMs, phase)` | `BehaviorDecision` (null nếu không có / đã thả) | Một rồng (hộ chiếu, camera theo dõi) |
| `Personality(s, uid)` | `PersonalityProfile`: `Archetype`, `Traits` (năng động, tò mò, ham chơi, dạn dĩ 0–100), `FavoriteDiet`, `FavoriteHabitatId`, `SummaryKey` | Hộ chiếu rồng |
| `Species(speciesId)` / `Matrix()` | `SpeciesBehaviorProfile`: chu kỳ, kiểu di chuyển, 11 dòng `BehaviorAvailability` (`SpeciesBlock`, `Habitats`, `Ambient`) | Bách khoa, tooltip |
| `Ecology(speciesId, habitatId)` | `HabitatEcologyReport`: `Score`, `Tier`, `Reason` (trùng Suitability), `Factors[]` (`Key`, `Kind` gate/quality/info, `Ok`, `Actual`, `Required`, `Deficit`, `ExplanationKey`), `PrimaryIssueKey` | Màn chọn chỗ đặt: "vì sao phù hợp / không" |
| `Ecology(s, uid, rules?)` | `DragonEcologyView`: báo cáo ở chỗ hiện tại, `CurrentComfort`, `ComfortTarget`, `ComfortDriftEnabled` (false ở production), `InFavoriteHabitat` | Hộ chiếu rồng |
| `Research(s, speciesId, journal?, rules?)` | `SpeciesResearchView`: `Gated` (false ở production), `Tier`, `RevealedSections`, `ObservableBehaviors`, `ObservedBehaviors`, `NextStepKey` | Bách khoa |

`phase` là `Dravio.Domain.Behavior.DayPhase` (Dawn, Day, Dusk, Night), cùng thứ tự với `Dravio.EnvironmentArt.DayPhase`, nên ép kiểu trực tiếp được: `(DayPhase)(int)timeOfDay.Phase`. Không có Environment thì dùng `DayCycle.PhaseOfMinute(phútTrongNgày)` (ranh giới 5:00 / 7:00 / 17:30 / 19:30, giống dữ liệu `*Living.json`).

### 8.2 `BehaviorDecision`

| Trường | Ý nghĩa |
|---|---|
| `Visible` | false: rồng ở kho, đang lai, đang thám hiểm. Khi đó `Behavior = null` → `SanctuaryDragonFrame.visible = false` |
| `Behavior` | Một trong 11 hành vi |
| `Role` | `Gameplay` (suy ra từ state thật) hoặc `Presentation` (chỉ để xem) |
| `Reason` | `ambient`, `circadian-sleep`, `food-drop`, `fed`, `vitals-low`, `energy-low`, `hazard-warning`, `in-care`, `sheltered`, `in-storage`, `busy-breeding`, `busy-expedition` |
| `Locomotion` | Gợi ý trùng tên `Dravio.Character.DragonLocomotion` (Idle, Walk, Run, Swim, Fly, Burrow, Sleep). Luôn là kiểu loài có, nên runtime Character không phải hạ về Idle |
| `Interaction` | `Eat` (vừa ăn) hoặc `Happy` (Play), trùng `DragonInteraction` → `sync.Trigger(uid, …)` một lần khi quyết định đổi |
| `TargetFoodId` | Chỉ với SeekFood: dẫn rồng tới drop này rồi gửi `ConsumeFoodCommand(id, TargetFoodId, Uid)` (luôn hợp lệ tại thời điểm quyết định) |
| `ZoneId`, `HabitatId`, `Phase`, `Activeness` | Ngữ cảnh |
| `StartedAtMs`, `UntilMs` | Khoảng quyết định còn đúng nếu state không đổi. Nhịp trình diễn dài 60 s |
| `Weights` | Trọng số đã cân nhắc (debug overlay) |

### 8.3 Ví dụ tầng Sanctuary

```csharp
using Dravio.Domain.Behavior;
using Dravio.Domain.Queries;

DayPhase phase = (DayPhase)(int)timeOfDay.Phase;
foreach (BehaviorDecision b in BehaviorQueries.All(session.State, clock.NowMs(), phase))
{
    if (!b.Visible) { frames.Add(Hidden(b.Uid)); continue; }
    var locomotion = (DragonLocomotion)Enum.Parse(typeof(DragonLocomotion), b.Locomotion.ToString());
    Vector3 goal = b.Behavior == DragonBehavior.SeekFood ? FoodPosition(b.TargetFoodId) : WanderPoint(b.Uid, b.HabitatId, b.StartedAtMs);
    frames.Add(MoveToward(b.Uid, goal, locomotion));
    if (b.Behavior == DragonBehavior.SeekFood && Reached(b.Uid, goal))
        session.Send(id => new ConsumeFoodCommand(id, b.TargetFoodId, b.Uid));
    if (Changed(b) && b.Interaction != InteractionHint.None) sync.Trigger(b.Uid, (DragonInteraction)(int)b.Interaction);
}
```

Điểm đi lang thang (`WanderPoint`) và tốc độ là việc của tầng Sanctuary. Domain không biết tọa độ.

### 8.4 Khóa i18n mới

`behavior.<wire>` (`idle`, `rest`, `sleep`, `explore`, `seekFood`, `eat`, `play`, `bask`, `swim`, `fly`, `burrow`); `personality.<archetype>.summary`; `ecology.<factor>.<ok|too-cold|too-hot|not-native|altered|wrong-salinity|wrong-depth|wrong-altitude|needs-air|needs-water|cannot-swim|cannot-walk|cannot-fly|no-perch|low|not-relevant>`, `ecology.shelter.<zone|perch>`, `ecology.ok`, `ecology.invalid-env`; `research.next.<own|viewProfile|raiseToAdult|observe|done>`; `codexSection.<identity|habitat|physiology|behavior>`.

### 8.5 Luật đang tắt (chỉ test và bản cân bằng)

- `EcologyComfortRules.ProposedForReview`: comfort theo môi trường (ADR 0203 C1–C2). Production không gọi `EcologyComfort.Apply`.
- `ResearchRules.ProposedForReview` + `ObservationJournal`: cấp nghiên cứu và ghi nhận tập tính (ADR 0203 D1–D3). Sổ quan sát chưa được lưu (Schema v9 chờ duyệt, D4).
- Thiên tai tự nhiên vẫn tắt (ADR 0202).
