# Character Behaviour API: hợp đồng tích hợp (dành cho AI và lập trình viên)

Phạm vi: `Dravio.Character.Runtime` (U1-CHAR.6). Runtime này **chỉ hiển thị**, không có dependency vào Domain, Persistence, HUD hay Scene.

Nó nhận từ Sanctuary: rồng nào, ở đâu, muốn làm gì (`intent`), tính cách gì, nhìn về đâu. Nó trả lại animation khớp với clip loài đó có.

Mọi luật gameplay đều nằm ngoài runtime: sức khoẻ, phần thưởng, nhu cầu, việc chọn hành vi, và việc rồng có được tới một điểm tương tác hay không.

## Quy tắc bắt buộc

1. **Không suy diễn từ Domain trong Character.** Tầng Sanctuary tự chuyển state Domain thành `SanctuaryDragonFrame`:
   - stage dùng `SanctuaryVisualMapping.TryStage`;
   - tính cách dùng `DragonPersonalityPresentation.StyleFromName`;
   - intent là logic của Sanctuary.
2. **Mỗi frame gửi đủ danh sách rồng đang hiện:** `sync.Sync(frames, deltaSeconds)`. Thiếu UID nghĩa là despawn; `visible = false` nghĩa là ẩn.
3. **`intent = None`** (mặc định) giữ hành vi U1-CHAR.5: `locomotion` của frame được hiển thị nguyên như vậy.
4. **Không ép khả năng.** Intent Swim/Fly/Burrow trên loài không có khả năng đó sẽ hiện Idle và ghi note, được đếm ở `report.substituted`. Sanctuary nên tránh gửi những intent này.
5. **Tương tác người chơi:** gọi `sync.React(uid, DragonReaction.Petted | Called | Fed)`. Runtime chọn cách phản hồi theo tính cách. Rồng đang ngủ không bị đánh thức.
6. **Chăm sóc:** dùng `intent = Recover` để rồng nghỉ nhẹ nhàng, nhịp chậm. Không có tư thế ốm hay bị thương. Sanctuary quyết định khi nào, dựa trên Domain.

## Kiểu dữ liệu

```csharp
// Runtime/Integration/SanctuaryDragonSync.cs
public struct SanctuaryDragonFrame {
    string uid; string speciesId; DragonVisualStage stage; string variantId; GenotypeEntry[] genotype;
    bool visible; DragonLocomotion locomotion; float speed; Vector3 position; Quaternion rotation;
    // U1-CHAR.6, tuỳ chọn
    DragonBehaviorIntent intent;            // None = đường cũ
    DragonPersonalityStyle personality;     // Playful / Calm / Curious / Shy / Unspecified
    float turnRateDegPerSec;                // >= 60 khi Idle -> bước tại chỗ
    bool hasLookTarget; Vector3 lookTarget; // người chơi, thức ăn, rồng khác
}
enum DragonBehaviorIntent { None, Idle, Walk, Run, Swim, Fly, Burrow, Sleep, Rest, Bask, Eat, Drink, Play, LookAround, Curious, Stretch, Recover }
enum DragonReaction { None, Petted, Fed, Called }
```

## Vòng đời

```csharp
var manager = sanctuaryRoot.GetComponent<DragonVisualManager>();  // catalog = Data/DragonVisualCatalog.asset
var sync = new SanctuaryDragonSync(manager);                       // manager là IDragonBehaviorTarget

void Update() {
    frames.Clear();
    foreach (var d in sanctuary.VisibleDragons) {          // dữ liệu của Sanctuary, không phải Character
        DragonVisualStage stage;
        if (!SanctuaryVisualMapping.TryStage(d.StageName, out stage)) continue;
        frames.Add(new SanctuaryDragonFrame {
            uid = d.Uid, speciesId = d.SpeciesId, stage = stage, visible = true,
            position = d.Position, rotation = d.Rotation, speed = d.SpeedMps, turnRateDegPerSec = d.YawRate,
            intent = d.Intent,                               // vd. DragonBehaviorHints.FromEnvironmentHint(point.animationHint)
            personality = DragonPersonalityPresentation.StyleFromName(d.PersonalityName),
            hasLookTarget = d.LookTarget.HasValue, lookTarget = d.LookTarget ?? default,
        });
    }
    var report = sync.Sync(frames, Time.deltaTime);         // spawned / despawned / stageChanged / updated / failed / triggered / substituted
}
void OnPlayerPets(string uid) { sync.React(uid, DragonReaction.Petted); }
void OnDestroy() { sync.Clear(); }
```

## Đảm bảo (có test, `Tests/BehaviorTests.cs`, `Tests/SanctuaryIntegrationTests.cs`)

| Đảm bảo | Test |
|---|---|
| Không intent thì giống hệt U1-CHAR.5; target U5 (không implement `IDragonBehaviorTarget`) bỏ qua intent | `Sync_WithoutIntent_IsTheU5Behaviour` |
| Sleep → Wake → đích; one-shot không bị ngắt; quay lại `None` thì tempo và look-at được reset | `LeavingSleep_PlaysWakeFirst...`, `Sync_WithIntent_PlansTriggersAndSwitchesBackCleanly` |
| Thiếu clip thì thay bằng clip gần nghĩa nhất và báo, không giả khả năng | `Intents_MapToClipsTheSpeciesHas_AndSubstituteHonestly` |
| Phản hồi theo tính cách; đợi one-shot đang chạy; không đánh thức rồng ngủ | `Reactions_FollowPersonality...` |
| Biến thể Idle tất định theo UID; Playful nhiều hơn Calm; Shy chủ yếu LookAround; chỉ dùng clip có thật | `IdleVariations_...` |
| Đổi stage thì planner dùng clip của model mới; 30 rồng với mọi intent không lỗi | `Sync_StageChangeRebuildsPlanner...` |
| Trạng thái ổn định không cấp phát mỗi frame (đo trong .NET, build tối ưu) | `Sync_SteadyState_AllocatesNothingPerFrame` |
| Pool, spawn/despawn, đổi prefab (từ U1-CHAR.5) | `SanctuaryIntegrationTests` |

## Chi phí (đo ngoài Unity)

- **Phép đo:** `dotnet run -c Release --project Assets/Dravio/Character/Tests/DotNet~/Bench/Dravio.Character.Bench.csproj` trên Xeon 2,1 GHz, .NET 8.
- **Kết quả:** sync + planner cho 30 rồng mất khoảng 11,5 µs/frame và **0 B/frame**.
- **Chưa đo:** Animator, skinning, render và thiết bị di động. Phải đo bằng `Scenes/DragonStress.unity` với `behaviourIntents = true`.

## Không thuộc runtime (đừng tìm ở đây)

- Chọn intent, nhu cầu (đói, khát, mệt), lịch sinh hoạt.
- Tìm đường, và việc loài nào được dùng điểm tương tác nào.
- Sức khoẻ, bệnh, phần thưởng, thành tựu.
- Âm thanh và VFX phản hồi. Vị trí đặt VFX lấy bằng `TryGetAnchor`.
