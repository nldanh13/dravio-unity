# U2.13 — Tài liệu API Domain cho AI tích hợp Unity (phần thay đổi)

> Nền: [U2.12 API](../u2.12/API_INTEGRATION.md) (mọi quy tắc tích hợp ở mục 0 vẫn áp dụng). Quyết định: [ADR 0208](../adr/0208-u2-13-sky-cloud-aurorawing.md) (sky_cloud, trứng Aurorawing), [ADR 0209](../adr/0209-u2-13-sanctuary-world-materials.md) (vật liệu dự án theo thế giới).
> **Save Schema v10, không migration.** U2.13 không thêm lệnh hay mã lỗi. Thêm trường DTO, một truy vấn, một trường event, một vật liệu (`stardust`, chưa kích hoạt).

## 0. Trạng thái luật

| Hệ | Trong game | Ghi chú |
|---|---|---|
| Bảng thưởng `sky_cloud` | **TẮT** (chờ duyệt) | `StartExpeditionCommand("sky_cloud", …)` vẫn trả `route-rewards-undefined`; `RouteExplorationDto.RewardsDefined = false` |
| Trứng Aurorawing ở chuyến sky_cloud thứ 15 | bật (đã duyệt) | chỉ đạt được khi bảng sky_cloud bật |
| Vật liệu dự án theo thế giới | **bật** (đã duyệt) | lùi về thế giới thấp hơn khi thế giới chưa mở / chưa có nguồn |
| Luật cho ăn B (cá thể được chọn trước) | tắt, chỉ nghiên cứu | xem `FEEDING_RESEARCH.md`; `FeedCommand` không đổi |

Không đoán vật liệu hay trạng thái tuyến: luôn đọc từ DTO.

## 1. Thám hiểm — `ProgressionQueries.ExplorationRoutes(s)`

`RouteExplorationDto` thêm:

| Trường | Ý nghĩa | UI |
|---|---|---|
| `Materials` | vật liệu tuyến có thể cho; **rỗng** khi bảng thưởng chưa duyệt | biểu tượng vật liệu trên thẻ tuyến |
| `DurationMs` | thời gian một chuyến | đồng hồ đếm ngược / nhãn "45 phút" |
| `NextMilestoneEggSpeciesId` | loài trứng thưởng ở mốc kế tiếp (`"aurorawing"` ở sky_cloud mốc 15), null nếu mốc không có trứng | "Chuyến thứ 15: trứng Aurorawing" |

Mốc sky_cloud khi bảng A được duyệt: 1 / 5 / 15 / 40 chuyến, thưởng thêm 2 / 3 / 4 / 6 bụi sao; mốc 15 có thêm trứng. Khi bảng tắt chỉ còn mốc 15 (trứng, không đạt được vì tuyến khóa).

```csharp
var sky = ProgressionQueries.ExplorationRoutes(session.State).First(r => r.RouteId == "sky_cloud");
if (!sky.RewardsDefined) ShowLocked("route-rewards-undefined");
else if (sky.NextMilestoneEggSpeciesId != null) ShowEggTeaser(sky.NextMilestoneEggSpeciesId, sky.NextMilestoneAt.Value - sky.Claimed);
```

## 2. Vật liệu

`ExpeditionCatalog.MaterialIds`: `wood, seed, stone, crystal, coral, stardust` (thứ tự cũ giữ nguyên, `stardust` ở cuối). `ExpeditionCatalog.Materials[i].Active` = false cho `stone` và cho `stardust` khi bảng sky tắt; UI ẩn vật liệu không active khỏi kho trừ khi người chơi có > 0. Bụi sao nằm trong `player.inventory["stardust"]` như vật liệu khác.

## 3. Dự án Sanctuary — vật liệu động

### `ProgressionQueries.SanctuaryEconomy(s, nowMs) → SanctuaryEconomyDto`

| Trường | Trước (U2.10–12) | U2.13 |
|---|---|---|
| `NextProjectMaterialIds` | luôn `wood, seed` | động: `wood, seed` / `crystal, coral` / `stardust` |
| `NextProjectMaterialEach` | `1 + n/5` | số đơn vị **mỗi** ID trong `NextProjectMaterialIds` (bụi sao: gấp đôi) |
| `NextProjectMaterials` | — | `MaterialNeed[]`: `MaterialId`, `Required`, `Have`, `Missing`, `NameKey` (`material.<id>.name`) |
| `NextProjectMaterialZoneId` | — | thế giới cung cấp vật liệu (`origin` / `ocean` / `sky`), null khi đã xây đủ |
| `NextProjectMaterialFallback` | — | true = công trình thuộc thế giới chưa mở / chưa có nguồn nên dùng vật liệu thế giới thấp hơn |
| `NextProjectBlockedBy` | | không đổi: `not-enough-gold`, `not-enough-materials`, `projects-maxed` |

```csharp
var e = ProgressionQueries.SanctuaryEconomy(session.State, now);
foreach (MaterialNeed m in e.NextProjectMaterials)
    costRow.Add(Icon(m.MaterialId), $"{m.Have}/{m.Required}", highlight: m.Missing > 0);
if (e.NextProjectMaterialFallback) costRow.Hint("sanctuary.materials.fallback"); // "Mở thế giới này để dùng vật liệu riêng"
```

### `ProgressionQueries.SanctuaryProjectCosts(s) → List<SanctuaryProjectCostView>` (mới)

Bảng 60 dòng cho màn hình lộ trình dự án: `Index`, `ProjectId`, `ProjectZoneId` (thế giới của công trình), `Built`, `CostGoldMicro`, `MaterialIds`, `MaterialEach`, `MaterialZoneId`, `Fallback`.

- Giá vàng không đổi theo trạng thái. Vật liệu của dự án **chưa** xây có thể đổi khi mở thế giới mới hoặc khi bảng sky được bật; gọi lại sau `zoneUnlocked`.
- Dự án đã xây: dòng này là chi phí theo luật hiện hành (để tham khảo); lịch sử thật nằm trong event `sanctuaryProjectBuilt`.
- O(60), rẻ, nhưng vẫn chỉ gọi sau `session.Changed`.

Lệnh không đổi: `BuildSanctuaryProjectCommand(rid)`.

## 4. Event

| Event | Thay đổi | Gợi ý UI |
|---|---|---|
| `sanctuaryProjectBuilt { index, costGoldMicro, materialEach, materialIds }` | thêm `materialIds` | hiển thị vật liệu đã tiêu |
| `rewardEggGranted { rewardId: "EXPEDITION_EGG_AURORAWING", speciesId: "aurorawing", delivered }` | phát ra từ `ClaimExpeditionCommand` khi nhận chuyến sky_cloud thứ 15 (trước đây chỉ từ `unlockZone`) | cảnh trứng Aurorawing; `delivered = false` → "trứng đang chờ, giải phóng kho/ô trứng" |
| `rewardEggDelivered` | không đổi; phát khi trứng chờ được giao vào `pendingPack` | — |
| `explorationMilestoneReached` | sky_cloud có mốc 1 / 5 / 15 / 40 khi bảng bật | toast mốc |

Trứng Aurorawing được trao **đúng một lần** (`grantedRewards`), kể cả khi tải lại save, nhận chuyến lần 16+ hoặc kho đầy (vào `rewardQueue`, không mất). Mô hình nở dùng `speciesId = "aurorawing"` (PR #65 CHAR.8).

## 5. Save

Schema v10 không đổi. Trường mới đều dẫn xuất. Validator thêm: `grantedRewards` chứa `EXPEDITION_EGG_AURORAWING` khi `expeditionsClaimed.sky_cloud < 15` → save bị từ chối (`grantedRewards: EXPEDITION_EGG_AURORAWING before milestone`). Save cũ (U2.10–U2.12) nạp không cần migration; dự án đã xây giữ nguyên.
