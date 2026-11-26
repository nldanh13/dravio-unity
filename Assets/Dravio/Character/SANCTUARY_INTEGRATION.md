# Tích hợp rồng sống với Unified Sanctuary (U1-CHAR.5)

Runtime Character (`Dravio.Character.Runtime`) **chỉ hiển thị**. Nó không đọc Domain, Persistence hay HUD và không tự quyết định hành vi. Tầng Sanctuary dịch state của Domain và phần mô phỏng di chuyển thành một danh sách `SanctuaryDragonFrame` mỗi frame. `SanctuaryDragonSync` giữ cho các rồng đang hiển thị khớp với danh sách đó.

```
Domain (GameSession.State)  ─┐
                             ├─► tầng Sanctuary ─► SanctuaryDragonFrame[] ─► SanctuaryDragonSync ─► DragonVisualManager
Mô phỏng di chuyển (vị trí) ─┘        (ánh xạ)                                    (spawn/despawn/stage/state)
```

## 1. Dựng scene

1. Thêm `DragonVisualManager` vào một GameObject của scene Sanctuary và gán `catalog` = `Assets/Dravio/Character/Data/DragonVisualCatalog.asset`. `phenotypeProfile` có thể bỏ trống, vì GDD chưa định nghĩa phenotype nhìn thấy được.
2. Tạo `new SanctuaryDragonSync(manager)` (manager implement `IDragonVisualTarget`).
3. Mỗi frame (hoặc mỗi lần state đổi, cộng với mỗi frame cho vị trí) gọi `sync.Sync(frames)`.

```csharp
using Dravio.Character;
using Dravio.Character.Integration;

var sync = new SanctuaryDragonSync(manager);
// mỗi frame:
frames.Clear();
foreach (var d in sanctuaryDragons)                     // tầng Sanctuary tự dựng từ Domain + mô phỏng
{
    DragonVisualStage stage;
    if (!SanctuaryVisualMapping.TryStage(d.StageName, out stage)) continue;   // "Baby" / "Juvenile" / "Adult"
    frames.Add(new SanctuaryDragonFrame
    {
        uid = d.Uid, speciesId = d.SpeciesId, stage = stage, variantId = d.VariantId, genotype = d.Genotype,
        visible = d.IsInSanctuaryView,                 // false: đi thám hiểm, trong tổ, ở kho... (Sanctuary quyết)
        locomotion = d.Locomotion, speed = d.SpeedMps, position = d.Position, rotation = d.Rotation,
    });
}
SanctuarySyncReport r = sync.Sync(frames);              // spawned / despawned / stageChanged / updated / failed
sync.Trigger(uid, DragonInteraction.Eat);               // tương tác một lần (Eat, Happy)
manager.TryGetAnchor(uid, "VFX_Mouth", out var mouth);  // đặt VFX
// khi rời scene:
sync.Clear();
```

## 2. Quy tắc đồng bộ (có test, `Tests/SanctuaryIntegrationTests.cs`)

| Tình huống | Kết quả |
|---|---|
| UID mới và `visible` | `Spawn` (lấy instance từ pool theo prefab) |
| UID không còn trong danh sách (bán, thả) hoặc `visible = false` | `Despawn` (trả về pool) |
| Cùng loài và variant, khác stage (lớn lên) | `SetStage`: giữ trạng thái. Đổi prefab nếu stage mới có mô hình riêng |
| Đổi loài hoặc variant | Spawn lại để áp lại phenotype |
| Không đổi gì | Chỉ `ApplyState` (vị trí, hướng, locomotion, tốc độ). Không gọi spawn/despawn |
| UID trùng hoặc rỗng, hoặc catalog không có mô hình | Bỏ qua, tính vào `failed`. Mục đầu tiên của UID được giữ |

Thứ tự danh sách không ảnh hưởng kết quả. Ở trạng thái ổn định, 30 rồng mỗi frame không phát sinh lệnh spawn, despawn hay đổi stage nào (có test).

## 3. Locomotion do Sanctuary chọn

Tầng Sanctuary chọn `DragonLocomotion` theo nơi rồng đứng và việc nó đang làm. Runtime không đoán. Nếu loài **không có** khả năng đó, runtime hiện `Idle` và cảnh báo một lần. Riêng `Run` khi loài không có clip Run thì dùng `Walk`. Bộ clip của từng loài theo sinh học nằm ở `Data/dragon-archetypes.json` và được tóm trong `reports/animation-report.md`.

| Locomotion | Khi nào (gợi ý cho Sanctuary) | Foot IK |
|---|---|---|
| Idle, Sleep | đứng yên, ngủ | có (gần camera) |
| Walk, Run | di chuyển trên đất. `speed` (m/s) → `PlaybackRate = speed / groundSpeed` của clip, giới hạn 0,5–2 | có |
| Swim | trong vùng nước, chỉ loài bơi | không |
| Fly | trên không, chỉ loài bay | không |
| Burrow | đào, chỉ loài đào | không |

Phần **ánh xạ từ Domain** (ví dụ `DragonActivity.Care` hiện thế nào, rồng `Expedition` có ẩn không) là quyết định của tầng Sanctuary và GDD. Runtime Character không cài sẵn luật nào cho việc này.

## 4. Hiệu năng

- Pool theo prefab. `maxPooledPerPrefab` (mặc định 16) giới hạn số instance rảnh. Vượt giới hạn thì instance bị huỷ và được đếm ở `PoolDestroyed`.
- `UpdateRatePolicy`:
  - rồng gần, trong khung hình: cập nhật mỗi frame;
  - rồng xa: 2 frame một lần;
  - ngoài khung hình: 8 frame một lần;
  - quá 80 m: không đánh giá.
  - Các cá thể lệch pha nhau.
- Foot IK chỉ bật cho rồng gần, đang đứng trên đất.
- Theo dõi: `Count`, `VisibleLastFrame`, `EvaluatedLastFrame`, `PoolCreated`, `PoolReused`, `PoolDestroyed`. `Scenes/DragonStress.unity` ghi các chỉ số này vào JSON.
- Ước tính tĩnh cho 5/15/30 rồng (không phải số đo): `reports/performance-budget.md`, sinh bằng `build_character_reports.py`.

## 5. Ràng buộc

- Không có dependency vào Domain, Persistence, Fantasy HUD hay Scene Open Game. Assembly `Dravio.Character.Runtime` không tham chiếu các assembly đó.
- Genotype chỉ đổi hình khi `PhenotypeProfile` có luật. Mặc định không có luật nào.
- Mô hình thiếu stage: catalog lấy đúng stage, nếu không có thì `Unstaged`, rồi đến stage gần nhất. Mỗi lần thay thế đều phát sự kiện `Warning`, không âm thầm đổi.

## 6. Behaviour intent, tính cách, tương tác (U1-CHAR.6)

Các trường mới của `SanctuaryDragonFrame` đều tuỳ chọn: `intent`, `personality`, `turnRateDegPerSec`, `hasLookTarget` và `lookTarget`. Để mặc định thì mọi thứ ở trên giữ nguyên.

Khi có `intent`, mỗi rồng có một `DragonBehaviorPlanner` chọn clip mà loài đó có. Planner lo:
- Sleep → Wake → đích;
- biến thể Idle theo tính cách;
- phản hồi `sync.React(uid, Petted | Called | Fed)`;
- head look-at.

Gọi `sync.Sync(frames, deltaSeconds)` để timer chạy đúng. Hợp đồng đầy đủ: [CHARACTER_BEHAVIOR_API.md](CHARACTER_BEHAVIOR_API.md). Bảng clip và ánh xạ: [ANIMATION_CATALOG.md](ANIMATION_CATALOG.md).

## 7. Môi trường và chuyển tiếp (U1-CHAR.7)

Trước khi đổi môi trường, gọi `sync.ReadinessFor(uid, mode)`. Sau mỗi `Sync`, đọc `sync.MotionStatusOf(uid)` để biết:
- `canTranslate`: có được dịch chuyển rồng không;
- `rootOffsetToApply`: độ cao cần cộng/trừ trong frame này;
- `visible`: rồng có đang hiện không.

Chi tiết: [MOTION_API.md](MOTION_API.md).
