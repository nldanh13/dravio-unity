# U2.12 — Tài liệu API Domain cho AI tích hợp Unity

> Quyết định: [ADR 0207](../adr/0207-u2-12-economy-ecosystem-validation.md). Nền: [U2.10 guide](../u2.10/API_INTEGRATION_GUIDE.md), [U2.11 world progression](../u2.11/WORLD_PROGRESSION.md).
> Domain: `Assets/Dravio/Domain` (C# thuần, không UnityEngine, tất định). Persistence: `Assets/Dravio/Persistence`. Phiên chơi: `Assets/Dravio/Platform/GameSession.cs`.
> **Save Schema v10.** U2.12 không thêm lệnh, event, mã lỗi hay trường save. Chỉ thêm hai truy vấn chỉ đọc: `ProgressionQueries.Journey`, `ProgressionQueries.AuditCatalog`.

## 0. Quy tắc tích hợp (bắt buộc)

1. **Mọi thay đổi state đi qua lệnh.** `session.Send(rid => new XxxCommand(rid, …))` trả `DomainError` (null = thành công). Không bao giờ sửa `GameState` trực tiếp; không giữ tham chiếu `GameState` cũ sau lệnh (mỗi lệnh thành công tạo bản sao mới).
2. **Truy vấn là chỉ đọc và tất định.** Gọi trên `session.State`. Gọi lại sau event `session.Changed` hoặc `session.DomainEvents`, **không gọi mỗi frame**: `QuestLog`, `Journey`, `Overview` duyệt các cặp rồng (O(n²), kho tối đa 500 rồng). Lưu kết quả tới lần đổi state kế tiếp.
3. **Lệnh bị từ chối không đổi gì** (không tốn vàng, không tiêu RNG). Hiển thị `commandError.<mã wire>` từ `DomainError.Code.ToWire()`; `DomainError.Detail` có chi tiết (vd danh sách yêu cầu còn thiếu).
4. **Không có vàng từ nhiệm vụ, mốc thám hiểm, quan hệ hay hướng dẫn.** Nguồn vàng duy nhất: rồng sản xuất trong sinh cảnh, bán rồng, thành tựu.
5. **Luật chưa duyệt luôn tắt.** Đọc `ProgressionQueries.Rules()` để biết hệ nào bật; không tự đoán. `sky_cloud` khóa (`route-rewards-undefined`).
6. ID (loài, sinh cảnh, nhiệm vụ, phần thưởng, tuyến, vật liệu) là chuỗi ổn định; tên hiển thị lấy qua khóa i18n (`quest.<id>.name`, `reward.<id>.name`, `journey.step.<code>`, `quest.req.<kind>`).

## 1. Vòng đời phiên

```csharp
var session = new GameSession(new SessionEnvironment { Clock = clock, Store = store, Scheduler = scheduler, WriterLock = writerLock });
session.Changed += Refresh;                       // state mới → tính lại truy vấn đã lưu
session.DomainEvents += events => Notify(events); // toast, âm thanh, hiệu ứng
session.Start();                                  // nạp save (migrate tới v10), quyết toán offline
// session.Tick() chạy 1 Hz (qua Scheduler, không phải mỗi frame); khi ẩn/hiện app: session.OnHide() / session.OnShow()
```

`session.Status`: `Ready`, `ReadOnly` (tab khác đang ghi), `NewerVersion` (save mới hơn app), `Unrecoverable`, `Conflict`. Chỉ gửi lệnh khi `Ready`.

## 2. Hành trình mở ba thế giới (U2.12)

### `ProgressionQueries.Journey(GameState s) → JourneyDto`

| Trường | Ý nghĩa |
|---|---|
| `NextZoneId` | `"ocean"`, `"sky"` hoặc `null` (đã mở đủ) |
| `UnlockedZones` | thế giới đã mở, thứ tự catalog |
| `Steps` | bước còn lại, **đúng thứ tự nên làm**; rỗng khi đã mở đủ |
| `SideQuestsReady` | nhiệm vụ phụ nhận được ngay (không thuộc đường chính) |

`JourneyStep`: `Code`, `QuestId`, `Target`, `Requirement` (`QuestRequirementKind?`), `Current`, `Required`, `Candidates`, `Detail`, `Ready`, `Key` (`journey.step.<code>`).

| `Code` (wire) | Hành động UI | Lệnh |
|---|---|---|
| `resume-tutorial` | "Tiếp tục hướng dẫn" (đã bỏ qua; Thủy Vực cần hướng dẫn hoàn tất) | `SetTutorialStatusCommand(rid, TutorialStatus.Active)` |
| `finish-tutorial` | chỉ dẫn bước `Target` (`TUT_01`…`TUT_08`) | lệnh của bước đó |
| `survey-habitat` | khảo sát `Target`. `Ready = false`, `Detail = survey-requirement-missing`: đá ấm cần nuôi Emberling và đã xem hồ sơ Emberling | `SurveyHabitatCommand(rid, habitatId)` |
| `unlock-habitat` | mở `Target` | `UnlockHabitatCommand(rid, habitatId)` |
| `quest-requirement` | thẻ yêu cầu `Requirement` của `QuestId`: `Current / Required`, gợi ý từ `Candidates` (bảng dưới) | tùy loại |
| `claim-quest` | nút "Nhận" | `CompleteQuestCommand(rid, questId)` |
| `unlock-zone` | nút "Mở thế giới" khi `Ready`; ngược lại `Detail` = mã còn thiếu (`tutorial-completed`, `origin-habitats`, `zone:<id>`, `quest:<id>`) | `UnlockZoneCommand(rid, zoneId)` |

`Candidates` theo `Requirement`:

| `Requirement` | `Candidates` | Gợi ý |
|---|---|---|
| `researchTier` | ID loài đang nuôi chưa đạt cấp | Profiled: `ViewBiologyProfileCommand`; LifeCycle: nuôi tới Adult; Ethology: quan sát đủ tập tính, ≥ 2 ngày UTC |
| `circadianRecords` | ID loài chưa đủ chu kỳ ngày đêm | quan sát cả lúc hoạt động và nghỉ (nhật ký đêm tự ghi khi offline qua 02:00 UTC) |
| `speciesRecordedInRealm` | ID loài đang nuôi, sống được ở realm đó, chưa được ghi chép ở đó | `MoveDragonCommand` vào sinh cảnh realm đó rồi quan sát |
| `adultWithLocomotion` | UID rồng nên đặt vào sinh cảnh (Adult trong kho trước, rồi rồng non) | `MoveDragonCommand` |
| `materialDelivery` | ID tuyến thám hiểm cho vật liệu đó | `StartExpeditionCommand` |
| `expeditionsClaimed` | ID tuyến | thám hiểm tuyến đó |

`Detail = need-species`: chưa nuôi loài phù hợp → gợi ý mua gói hoặc lai (không bao giờ gợi ý vàng). `Detail = research-disabled / research-log-missing / route-rewards-undefined`: hiển thị khóa, không có hành động.

```csharp
JourneyDto j = ProgressionQueries.Journey(session.State);
JourneyStep next = j.Steps.FirstOrDefault();          // HUD "Bước tiếp theo"
if (next?.Code == JourneyStepCode.ClaimQuest) ShowClaim(next.QuestId);
if (next?.Code == JourneyStepCode.UnlockZone && next.Ready) ShowUnlock(next.Target);
```

### Đường chính (luật U2.11, không đổi)

```
save mới ─ hướng dẫn TUT_01–08 ─ đá ấm + bờ hồ ─ Q_ORIGIN_FIELD_NOTES ─ QUEST_AQUATIC_EXPLORATION ─ unlockZone("ocean") → trứng Droplet
        ─ QUEST_SKY_EXPLORATION (Ethology loài Thủy Vực, 2 loài ghi chép ở Thủy Vực, Adult biết bay, 4 pha lê + 4 san hô) ─ unlockZone("sky") → trứng Zephyr
```

Nhận nhiệm vụ cổng **không** mở thế giới; luôn cần `unlockZone`. Trứng thế giới trao một lần; kho đầy thì vào hàng chờ, không mất.

## 3. Bảng lệnh theo màn hình

| Màn hình | Lệnh | Mã lỗi thường gặp |
|---|---|---|
| Sanctuary | `MoveDragonCommand(rid, uid, MoveTarget.Zone(zone, habitat) / MoveTarget.Storage)`, `SetProductionSlotCommand`, `FeedCommand(rid, diet, zone, habitat)`, `ConsumeFoodCommand(rid, foodId, uid)`, `PetCommand`, `ObserveDragonCommand(rid, uid, phase)` | `zone-full`, `habitat-unsuitable`, `no-route`, `wrong-diet`, `feed-cooldown`, `observation-cooldown`, `not-observable` |
| Cửa hàng / ấp | `BuyTutorialPackCommand`, `BuyPackCommand(rid, packId)`, `ClaimPackCommand(rid, target)`, `StartBreedCommand(rid, a, b)`, `TapEggCommand`, `ClaimBreedCommand(rid, target)` | `not-enough-gold`, `pack-daily-limit`, `pack-pending`, `storage-full`, `breed-*`, `nest-busy` |
| Kho | `SellDragonCommand`, `BulkSellCommand`, `LockDragonCommand`, `RenameDragonCommand` | `last-dragon`, `dragon-locked`, `quote-changed` |
| Nâng cấp / trang trí / dự án | `BuyUpgradeCommand`, `BuyDecorationCommand`, `ToggleDecorationCommand`, `BuildSanctuaryProjectCommand` | `upgrade-maxed`, `not-enough-materials`, `projects-maxed` |
| Thám hiểm | `StartExpeditionCommand(rid, routeId, uid)`, `ClaimExpeditionCommand` | `route-locked`, `route-rewards-undefined`, `expedition-busy`, `dragon-unsuited` (detail `not-adult`, `requires-swim`…), `vitals-below-threshold` |
| Hành trình | `CompleteQuestCommand(rid, questId)`, `UnlockZoneCommand(rid, zoneId)`, `SurveyHabitatCommand`, `UnlockHabitatCommand`, `ViewBiologyProfileCommand`, `SetTutorialStatusCommand` | `unknown-quest`, `quest-completed`, `quest-locked` (detail `quest:<id>` / `zone:<id>`), `quest-not-ready` (detail các yêu cầu chưa đạt), `unlock-requirement-missing` |
| Chăm sóc | `MoveToShelterCommand`, `AssessCaseCommand`, `SetCarePlanCommand`, `StartCareCommand`, `ReviewCaseCommand`, `DischargeDragonCommand` | `case-*`, `care-*` |
| Thời gian | `AdvanceTimeCommand(rid, Online / Offline)` (GameSession tự gửi) | — |

## 4. Truy vấn theo màn hình

| Màn hình | Truy vấn |
|---|---|
| HUD hành trình | `ProgressionQueries.Journey(s)` (U2.12), `QuestLog(s)` (`Quests`, `ReadyQuestIds`, `Rewards`, `ZoneUnlockMissing`) |
| Codex / nghiên cứu | `ResearchProgression(s)`, `ResearchProfile(s, speciesId)`, `IndividualResearch(s, uid)`, `ObservationOpportunities(s, now, phase)` |
| Thẻ rồng | `Relations(s, uid)`, `SocialProfile(s, uid)`, `HealthQueries.*`, `Zones.PlacementOptions(s, dragon)` |
| Thám hiểm | `ExplorationRoutes(s)` (mở chưa, có bảng thưởng chưa, số chuyến, mốc kế tiếp) |
| Dự án Sanctuary | `SanctuaryEconomy(s, now)`, `SanctuaryProjects(s)` |
| Tổng quan | `Overview(s)`, `Rewards(s)`, `Rules()` |
| Công cụ nội dung / test | `ProgressionQueries.AuditCatalog()` (U2.12): `CatalogErrors`, `Steps` (đợt mở khóa), `UnreachableQuests`, `UnreachableZones`, `ObtainableSpecies` |

## 5. Event để thông báo

| Event (`Type`) | Khi nào | Gợi ý UI |
|---|---|---|
| `questRewarded { questId, reward, rewardId, delivered }` | nhận nhiệm vụ | toast phần thưởng (ghi chú, mẫu trang trí, danh hiệu); `delivered` = vật liệu đã giao |
| `questCompleted { questId }` | nhiệm vụ cổng | mở nút "Mở thế giới" |
| `zoneUnlocked`, `habitatUnlocked`, `rewardEggGranted`, `rewardEggDelivered` | `unlockZone` | cảnh mở thế giới, trứng |
| `explorationMilestoneReached { routeId, claims, bonus, rewardId }` | nhận chuyến thứ 1 / 5 / 15 / 40 | toast mốc |
| `expeditionStarted`, `expeditionReady`, `expeditionClaimed` | thám hiểm | — |
| `dragonObserved { findings, deferredByDailyLimit }`, `nightJournalWritten` | nghiên cứu | ghi chép mới, "để dành hôm sau" |
| `sanctuaryProjectBuilt { index, … }` | dự án | dựng công trình `SanctuaryProjectContent.Get(index)` |
| `tutorialStepCompleted`, `tutorialStatusChanged` | hướng dẫn | — |

## 6. Save

- Schema **v10** (U2.11). U2.12 không đổi. Migration 9 → 10 giữ save gốc ở `archive:schema-9`.
- `session.SaveNow()`; autosave do `GameSession` lo. `ExportText()` / `PrepareImport(text)` / `ConfirmImport()` cho sao lưu.
- Khối `relations` hỏng được làm sạch khi nạp, không bao giờ mất save. Số liệu nhiệm vụ / thám hiểm sửa tay bị từ chối.
- Test U2.12: save của hành trình ba thế giới thật qua vòng lưu–nạp từng byte, sau khi nạp không nhận lại được nhiệm vụ hay trứng thế giới (`JourneyEndStateSaveTests`).

## 7. Lưu ý trải nghiệm từ mô phỏng (U2.12)

- **Cho ăn để nghiên cứu:** phần ăn được giao cho rồng thích loại đó, rồi UID nhỏ nhất (`BehaviorEngine.AssignFood`). Khi người chơi muốn cho một rồng cụ thể ăn, gợi ý loại thức ăn chỉ rồng đó ăn được và gửi `ConsumeFoodCommand` cho đúng UID đó.
- **Quan sát lúc ăn:** hồi quan sát 30 s mỗi rồng. Đừng gửi `ObserveDragonCommand` ngay trước khi cho ăn nếu muốn ghi "ăn"; dùng `ObservationOpportunities` để nhắc đúng lúc.
- **Tập tính theo nơi ở:** tắm nắng cần sinh cảnh trên cạn không lạnh, bay cần chỗ đậu hoặc Thiên Giới, bơi cần nước. Thẻ loài nên gợi ý "chuyển sang <sinh cảnh>" khi tập tính còn thiếu không làm được ở chỗ hiện tại (`BehaviorCatalog.Allowed`).
- **Xem lại hướng dẫn trước khi mở Thủy Vực** sẽ tạm khóa Thủy Vực tới khi các bước tự ghi lại; nên cảnh báo người chơi.
