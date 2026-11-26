# Motion Integration API (U1-CHAR.7): hợp đồng cho Sanctuary và AI tích hợp

Mở rộng [CHARACTER_BEHAVIOR_API.md](CHARACTER_BEHAVIOR_API.md), tương thích ngược hoàn toàn: frame không có intent và target U5/U6 vẫn chạy như cũ.

Character Runtime **chỉ thể hiện chuyển động**. Những việc sau thuộc Sanctuary:
- vị trí, đường đi, va chạm;
- điểm hạ cánh hay điểm đậu;
- độ sâu của nước, không gian bay;
- khi nào đổi môi trường.

## Quy tắc

1. **Kiểm tra trước khi đổi môi trường:** `sync.ReadinessFor(uid, DragonMotionMode.Water | Air | Perched | Underground | Ground)`.
   - `Ready`: gửi intent (Swim / SwimIdle / Fly / Glide / Perch / Underground, …).
   - `Unsupported`: model không có khả năng đó, **đừng gửi**.
   - `Busy`: đang chuyển tiếp, chờ.
   - `NeedsWake`: sẽ phát Wake trước.
   - `NotLive`: rồng không hiển thị.
2. **Sau mỗi `Sync`, đọc `sync.MotionStatusOf(uid)`:**
   - `canTranslate == false`: **giữ nguyên vị trí và hướng** (TakeOff, Land, WaterEnter/Exit, DigPrep, BurrowEnter/Emerge, Wake).
   - `rootOffsetToApply != 0`: cộng vào độ cao của rồng **trong frame này** (+ khi TakeOff xong, − khi Land bắt đầu), để hình ảnh không giật.
   - `actionPlaying`: đang có one-shot tại chỗ. Nên đứng yên, nếu không chân sẽ trượt.
   - `visible == false`: rồng đang dưới đất. Model bị ẩn; Sanctuary hiện VFX đất tại `TryGetAnchor(uid, "VFX_Ground")`.
   - `mode` / `targetMode` / `transition` / `completed`: trạng thái chuyển tiếp hiện tại.
3. **Đổi môi trường bằng intent:**
   - Rồng đang ở nước mà nhận `Idle` sẽ tự phát WaterExit.
   - Đang bay mà nhận `Idle` sẽ tự phát Land.
   - Muốn đứng yên trong nước dùng `SwimIdle`; muốn lượn dùng `Glide`.
4. **Gửi thêm dữ liệu cho shaper:**
   - `turnRateDegPerSec` (có dấu, + = rẽ phải);
   - `verticalSpeed` (m/s, + = leo);
   - `speed`.
   Shaper dùng chúng để nghiêng cánh, ngóc/chúi và uốn thân. Nên giữ tốc độ quay trong `MotionInfo.maxTurn*`.
   Từ U1-CHAR.7b, góc nghiêng khi bay là của vòng nghiêng cân bằng (cần `speed` > 0). Đầu giữ thăng bằng. Cơ sở: [BIOMECHANICS_RESEARCH.md](BIOMECHANICS_RESEARCH.md).
5. **Khả năng theo loài × stage:** `sync.CapabilitiesOf(uid)` đọc từ clip của model đang hiện. Bảng 16 loài nằm ở `reports_u7_sample/capability-matrix.md`.
6. **Lớn lên** (đổi model) giữ nguyên môi trường đang ở, không phát lại chuyển tiếp. Nếu model mới không có khả năng đó thì rồng giữ nguyên chỗ và báo `substituted`.

## Kiểu dữ liệu

```csharp
enum DragonMotionMode { Ground, Water, Air, Perched, Underground }
enum DragonMotionReadiness { Ready, Unsupported, Busy, NeedsWake, NotLive }
// DragonBehaviorIntent thêm (cuối enum): SwimIdle, SwimRest, SwimSurface, Glide, Perch, Underground, Greet
// DragonLocomotion thêm: SwimIdle=9, SwimRest=10, SwimSurface=11, Perch=12, Glide=13  (int cũ không đổi)
// DragonInteraction thêm: Greet=8, TakeOff=9, Land=10, WaterEnter=11, WaterExit=12, DigPrep=13, BurrowEnter=14, BurrowEmerge=15
struct DragonMotionStatus {
    bool live; DragonMotionMode mode, targetMode; DragonInteraction transition, completed;
    bool canTranslate, actionPlaying, visible; float rootOffsetToApply; DragonMotionCapabilities capabilities;
}
struct SanctuaryDragonFrame { /* ... U5/U6 ... */ float verticalSpeed; /* U1-CHAR.7 */ }
interface IDragonMotionTarget : IDragonBehaviorTarget {   // DragonVisualManager implement
    DragonVisualCatalog.MotionInfo MotionInfoOf(string uid);
    void SetHidden(string uid, bool hidden);
    void SetMotionShape(string uid, DragonMotionMode mode, float turnRateDegPerSec, float verticalSpeed, float speed);
    void SetExpression(string uid, float tailCarriage, float earPerk);
}
```

## Ví dụ: cất cánh, bay, hạ cánh

```csharp
if (sync.ReadinessFor(uid, DragonMotionMode.Air) == DragonMotionReadiness.Ready) d.Intent = DragonBehaviorIntent.Fly;
// mỗi frame, sau khi gửi frames:
var report = sync.Sync(frames, dt);
var st = sync.MotionStatusOf(uid);
d.Position.y += st.rootOffsetToApply;                  // cuối TakeOff: nâng; đầu Land: hạ
if (st.canTranslate && st.mode == DragonMotionMode.Air) MoveAlongFlightPath(d, dt);   // đường bay của Sanctuary
// muốn hạ cánh ở điểm do Sanctuary chọn: tới nơi rồi gửi Idle (hoặc Perch nếu điểm là chỗ đậu và capabilities.perch)
```

## Ví dụ: xuống nước, đào đất

```csharp
// tới mép nước (vị trí do Sanctuary chọn), kiểm sẵn sàng, gửi Swim; chỉ đưa rồng xuống nước khi canTranslate trở lại
if (sync.ReadinessFor(uid, DragonMotionMode.Water) == DragonMotionReadiness.Ready) d.Intent = DragonBehaviorIntent.Swim;
// đào: gửi Underground; khi st.visible == false thì hiện VFX đất tại VFX_Ground; muốn trồi lên thì gửi Idle
```

## Đảm bảo (có test)

| Đảm bảo | Test |
|---|---|
| Khả năng đọc từ clip, khớp sinh học (không Perch khi không có "chân đậu", loài chỉ bơi luôn ở trong nước) | `Capabilities_FollowTheModelsClips`, `test_motion.py` |
| WaterEnter khoá dịch chuyển tới khi xong; trong nước đổi trạng thái không cần chuyển tiếp; `Idle` thì phát WaterExit | `WaterEntry_LocksTranslationUntilDone...` |
| TakeOff / Glide / Land-to-Perch / TakeOff-from-perch; loài không chân đậu thì `Unsupported` | `Flight_...` |
| DigPrep → BurrowEnter nối liền; ẩn khi ở dưới đất; hiện lại từ frame đầu của Emerge | `Burrow_...` |
| Sleep → Wake → chuyển tiếp; khoá suốt cả quá trình | `WaterDwellers_..._SleepWakesBeforeAMediumChange` |
| Asset cũ thiếu clip chuyển tiếp: chuyển ngay và ghi note | `MissingTransitionClip_...` |
| Phản hồi và one-shot lặp chờ môi trường ổn định | `Repeats_And_Reactions_WaitForTheMediumToSettle` |
| Root offset cộng đúng một lần, đúng dấu; lớn lên không phát lại TakeOff; Readiness | `Sync_ReportsStatus_RootOffsets_Readiness...` |
| Ẩn khi dưới đất; dragon đi đường cũ có trạng thái mặc định | `Sync_HidesUnderground_LegacyDragonsReportDefaults` |
| Shaper: dấu, giới hạn, trên cạn không nghiêng | `MotionShape_...` |
| Không cấp phát mỗi frame, kể cả với intent môi trường | `Sync_MotionIntents_AllocateNothingPerFrame` |

Hai đầu mút của chuyển tiếp còn được `anim_qa.py` kiểm trên chính model.
