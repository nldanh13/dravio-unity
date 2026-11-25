# Dravio Core Domain (C#) — U2.1

Đây là bản port lõi domain của Dravio từ TypeScript sang C# thuần. Nguồn đối chiếu là **nldanh13/dravio @ `b33ec7a3dc98b4a74857fe92d143475028fc0d96`**, gồm GDD 3.1, ADR 0003/0004/0010/0013, `src/types`, `src/data`, `src/engine` và `tests/unit`. Luật, ID, thông số cân bằng và công thức kinh tế được giữ nguyên. Phần nào khác bản TS thì có ghi rõ ở mục 7.

Code nằm trong assembly `Dravio.Domain` (`Dravio.Domain.asmdef`). Assembly này đặt `noEngineReferences: true`, không tham chiếu assembly nào khác và không dùng `MonoBehaviour`, Scene, Animator hay Unity UI. Thời gian và số ngẫu nhiên đều được tiêm từ ngoài vào (`IClock`, `IRng`). Test `Domain_sources_have_no_Unity_or_ambient_time_random_dependencies` sẽ fail nếu có code gọi `UnityEngine`, `System.Random` hoặc `DateTime.Now`.

## 1. Cấu trúc

| Thư mục | Namespace | Nội dung | Nguồn TypeScript |
|---|---|---|---|
| `Model/` | `Dravio.Domain` | `GameState`, `PlayerState`, `DragonState`, `DragonLocation`, `Genotype`, `ZoneState`, `FoodState`, `OfflineReceipt`, `PendingPack`, `TutorialState`, enum, `WireNames` | `src/types/*.ts` |
| `Catalog/` | `Dravio.Domain.Catalog` | 16 loài (`SpeciesCatalog`), sinh học G0 (`G0BiologyData`, sinh tự động), 7 microhabitat, locus P0, hằng số chăm sóc, bảng kinh tế, ID tiến trình | `src/data/*.ts` |
| `Economy/` | `Dravio.Domain` / `.Economy` | `MicroGold`, `EconomyRates` | `src/data/economy.ts`, `src/engine/economy/rates.ts` |
| `Biology/` | `Dravio.Domain.Biology` | `Lifecycle` (stage, tích phân vàng), `BondRules`, `Suitability`, `Genetics` (founder, kiểm genotype) | `engine/progression/growth.ts`, `engine/biology/bond.ts`, `engine/world/habitat.ts`, `engine/genetics/alleles.ts` (một phần) |
| `World/` | `Dravio.Domain.World` | `Placement.IsProducing`, `IsPlacedInValidHabitat` | `engine/world/placement.ts` |
| `Text/` | `Dravio.Domain.Text` | `NicknameRules`, `GraphemeCounter` | `engine/inventory/nickname.ts` |
| `Core/` | `Dravio.Domain.Core` | `IRng`/`Mulberry32`/`Rng.HashSeed`, `IClock`/`TimeRules`, `GameCommand` và các lệnh, `CommandDispatcher`, `DomainEvent`, `DomainError`, `GameStateValidator`, `InitialStateFactory`, `JsCompat` | `engine/core/*.ts` |

## 2. Mô hình state

- **`GameState`** là Dravio Save Schema v4 (`ContentInfo.SchemaVersion = 4`, `ContentVersion = "g0-1.0"`). State chỉ đổi qua `CommandDispatcher.Dispatch`. Các giá trị dẫn xuất như stage, kiểu hình hay vàng/giây không được lưu.
- **`PlayerState`** gom các trường mà bản TS để phẳng trong `GameState`: `goldMicro`, `goldRemainder`, `totalGoldEarnedMicro`, `inventory`, `upgrades`, `pityCount`, `stats`, `settings`. Việc gom chỉ để rõ trách nhiệm; tên JSON gốc được ghi trong XML doc của từng thuộc tính để U2.2 ánh xạ 1-1 khi lưu save mà không đổi schema. Vì vậy thông điệp validation vẫn dùng đường dẫn TS, ví dụ `goldMicro: must be a non-negative integer`.
- **`DragonState`** tương ứng `DragonInstance`. Trường optional của TS dùng `null` hoặc `Nullable<T>`.
- **Enum ↔ chuỗi**: `WireNames.ToWire()` và `WireNames.TryParse()` cho ra đúng chuỗi TS, ví dụ `Juvenile` → `"juvenile"`, `HazardId.StrongCurrent` → `"strong_current"`, `DomainErrorCode.PetCooldown` → `"pet-cooldown"`. Không thêm giá trị enum nào ngoài các union type của TS.

## 3. Tiền tệ: `MicroGold`

- Chỉ có một loại tiền là Gold, lưu bằng microG nguyên `long`, với 1 G = 1 000 000 microG. Không dùng dấu phẩy động và không làm tròn ngầm.
- Phép cộng/trừ là `checked`. Khi tràn số, code ném `DomainInvariantException`, không quay vòng.
- Số dư hợp lệ nằm trong 0..2^53−1, tương đương `Number.isSafeInteger` để giữ tương thích với save JSON.
- `goldRemainder` lưu phần lẻ dưới 1 microG, đơn vị microG/4000. Hệ số online là 4/4 và offline là 3/4, được tính bằng phân số nguyên để tổng vàng không lệch khi chia nhỏ khoảng thời gian.
- Công thức trong `EconomyRates` (rate, giá bán, ngân sách sản xuất, giá nâng cấp, `offlineGoldMicro`) dùng phép nhân double theo đúng thứ tự của TS rồi `floor`. Golden kiểm tra 192 tổ hợp loài × stage × biến thể và mọi cấp nâng cấp; kết quả trùng từng microG.

## 4. Thời gian và RNG

- `TimeRules.ComputeElapsed`: `elapsed = clamp(max(now, highWater) − lastProcessed, 0, 24h)`. Khi đồng hồ lùi, code giữ high-water mark và không cộng thời gian.
- `CommandDispatcher` lấy thời điểm hiệu lực bằng `max(clock.NowMs(), highWaterAtMs)`, nên đồng hồ lùi cũng không dùng để né cooldown được.
- `Mulberry32` cho cùng chuỗi số với `createRng` của TS. `Rng.HashSeed` là FNV-1a theo đơn vị UTF-16. `RandomInt`, `PickUniform` và `PickWeighted` tiêu RNG giống hệt bản TS. Trạng thái RNG đọc được qua `Mulberry32.State` để lưu hoặc replay.
- `InitialStateFactory.Create` tiêu đúng 5 số RNG: 4 số cho UID, 1 số cho tính cách. Genotype founder dùng RNG riêng với seed `hashSeed("founder", uid, "g0-1.0")`.

## 5. Lệnh, bất biến và lỗi

| Lệnh | Luật (ADR 0004) |
|---|---|
| `AdvanceTimeCommand(mode)` | XP 100 % cho mọi rồng chưa thả, chặn ở Adult. Vàng tích phân theo mốc stage 35 %/100 % cho rồng sản xuất: online 100 %, offline 75 %. Offline > 60 s tạo `OfflineReceipt`. Thức ăn quá hạn bị xóa. |
| `FeedCommand(diet, zone, habitat)` | Thả thức ăn: zone/habitat phải đã mở, cách nhau ≥ 0,8 s, tối đa 5 + `foodCooldownLevel` viên, sống 12 s. Lệnh này không cho XP và không cho vàng. |
| `ConsumeFoodCommand(food, dragon)` | Nửa sau của luồng cho ăn, do mô phỏng của engine phát ra. Rồng phải cùng chỗ với viên thức ăn, đúng diet, đang idle và đã qua cooldown XP 8 s. Trước khi cộng XP, lệnh quyết toán thời gian online, rồi cộng XP 8 + 6 × `foodQualityLevel` và +1 bond. |
| `PetCommand(dragon)` | +1 bond, cooldown 30 s, trần 10 bond/ngày UTC/rồng và 100 tuyệt đối. Khi đã chạm trần, lệnh vẫn hợp lệ và vẫn phát `dragonPetted`, nhưng không phát `bondChanged`. |
| `RenameDragonCommand(dragon, nickname)` | Chuẩn hóa NFC, trim kiểu JS, độ dài 1–24 grapheme, cấm ký tự Cc, bidi và U+FFFD. `null` xóa tên. Tên được lưu nguyên văn, nên UI phải hiển thị như text thuần, không bật rich text. |
| `MoveDragonCommand(dragon, target)` (U2.3) | Đích là microhabitat (`MoveTarget.Zone`) hoặc kho (`MoveTarget.Storage`). Thứ tự kiểm (ADR 0008 mục 4): microhabitat thuộc zone → rồng rảnh (không released/care/shelter, `activity = idle`) → zone mở → microhabitat mở → suitability ≥ 50 → không phải chỗ hiện tại → còn chỗ (5/zone, rồng cùng zone không tính chính nó). Kiểm trước, rồi quyết toán thời gian online, rồi mới đổi chỗ. Vào kho thì rời ngân sách sản xuất (`productionChanged`). |
| `SetProductionSlotCommand(dragon, enabled)` (U2.3) | Bỏ chọn luôn được. Chọn chỉ khi rồng ở sinh cảnh đã mở và hợp lệ, và còn ngân sách 5 + 2 × `islandCapacityLevel` (tối đa 25, tính chung mọi zone). Quyết toán thời gian trước khi đổi. |
| `UnlockHabitatCommand(habitat)` (U2.3) | ADR 0009: ID có luật → zone mở → chưa mở → đã có khám phá điều kiện. Miễn phí, không đổi sức chứa/ngân sách/vàng, không quyết toán thời gian. Danh sách mở giữ theo thứ tự zone. |
| `SurveyHabitatCommand(habitat)` (U2.3) | Ghi nhận khám phá của microhabitat có điều kiện: đá ấm cần đang nuôi Emberling và đã xem hồ sơ của nó; bờ hồ không cần sở hữu Droplet. Event `habitatSurveyed` rồi `discoveryRecorded`. `discoveries` giữ sắp xếp. |
| `ViewBiologyProfileCommand(species)` (U2.3) | Chỉ với loài đang nuôi (chưa thả); thêm vào `tutorial.profilesViewed` (sắp xếp). Xem lại là thành công không event (vẫn tăng revision, như TS). |

`WorldGraph` (U2.3): node = zone, cạnh `origin_ocean_estuary` (cần bơi, cửa ngõ bờ hồ ↔ cửa sông) và `origin_sky_low_island` (cần bay, đồng cỏ ↔ đảo thấp có chỗ đậu); `CanTravel` là BFS. Giống bản TS b33ec7a, `CanTravel` **chưa** được nối vào `moveDragon` vì Ocean/Sky còn khóa ở G1 (ADR 0010 mục 4). Không có lệnh nào để UI tự khai báo khám phá hay mở sinh cảnh (`recordDiscovery` là hàm engine, không phải lệnh).

Hành vi chung của dispatch:
- Lệnh sai hình dạng (requestId rỗng hoặc > 64 ký tự, enum lạ, ID rỗng, nickname > 1024) hoặc thuộc loại chưa port đều trả `invalid-command`.
- `requestId` trùng trả `Duplicate = true`, không event, không tăng revision. Code nhớ 200 requestId gần nhất.
- Lệnh bị từ chối trả lại **chính object state đầu vào**, không tiêu RNG và không tiêu vàng.
- Lệnh thành công trả bản sao mới, tăng revision 1 và ghi nhận requestId.

`DomainError` mang mã có kiểu, chuỗi wire và khóa i18n `commandError.<code>`; đủ 30 mã của TS. `DomainInvariantException` dành cho dữ liệu hỏng hoặc lỗi lập trình (ID loài lạ, tràn số), không dành cho lệnh bị từ chối.

`GameStateValidator.Validate` port đủ `validateGameState`: enum, số hữu hạn, phạm vi, UID trùng, tham chiếu sản xuất, ngân sách, sức chứa 5/zone, microhabitat cần khám phá, sinh học (suitability ≥ 50), thức ăn, đồng hồ, tiến trình v4 và receipt. Thông điệp lỗi trùng chuỗi với bản TS.

## 6. Kiểm thử

**Trong Unity:** mở *Window → General → Test Runner → EditMode* rồi chạy `Dravio.Domain.Tests`. Package Test Framework 1.8.0 đã có sẵn dưới dạng phụ thuộc gián tiếp trong `packages-lock.json`, nên không phải sửa manifest. Assembly test có `defineConstraints: UNITY_INCLUDE_TESTS` và chỉ chạy trong Editor.

**Ngoài Unity** (CI hoặc máy không có Editor), dùng .NET SDK 8:

```bash
dotnet test "Assets/Dravio/Tests/Domain/DotNet~/Dravio.Domain.Tests.csproj"
```

Project `DotNet~` nằm trong thư mục mà Unity bỏ qua (tên kết thúc bằng `~`). Nó biên dịch đúng các file `.cs` mà Unity biên dịch, với `LangVersion 9` (bằng C# của Unity 6) và `TreatWarningsAsErrors`.

**Golden vectors** (`Tests/Domain/Golden/ts-golden-b33ec7a.json`, khoảng 1,1 MB) được sinh trực tiếp từ engine TypeScript gốc bằng `Tests/Domain/Tools~/golden.test.ts`. Nội dung gồm:
- 9 seed × 64 giá trị RNG, hashSeed, helper RNG, genotype founder;
- 6 state khởi tạo đầy đủ;
- 192 tổ hợp rate/giá bán, mọi cấp nâng cấp, ngân sách, `offlineGoldMicro`;
- 112 cặp suitability loài × habitat;
- 34 chuỗi nickname kèm số grapheme của `Intl.Segmenter`;
- 8 kịch bản phát lại lệnh (393 bước), trong đó có fuzz 300 bước trên 7 rồng (sản xuất/không, biến thể, kho, released, bận, nâng cấp, đồng hồ lùi, requestId trùng).

Mỗi bước kịch bản so sánh ok/lỗi, `Duplicate`, việc trả lại đúng object state, toàn bộ event theo thứ tự và toàn bộ state sau lệnh. Phần state so sánh loại trừ tutorial và achievements (xem mục 7), và vàng của TS đã trừ thưởng thành tựu.

Cách sinh lại golden. Kết quả hiện tại tái sinh được trùng từng byte:

```bash
git clone https://github.com/nldanh13/dravio && cd dravio && git checkout b33ec7a3dc98b4a74857fe92d143475028fc0d96 && npm ci
```

```bash
cp <dravio-unity>/Assets/Dravio/Tests/Domain/Tools~/golden.test.ts tests/unit/zz-golden.test.ts
```

```bash
GOLDEN_OUT=<dravio-unity>/Assets/Dravio/Tests/Domain/Golden/ts-golden-b33ec7a.json npx vitest run tests/unit/zz-golden.test.ts
```

Ngoài golden, có các test port từ `rng-clock`, `catalog`, `economy`, `g0Biology`, `gameState` và `g1Fixes` (phần world invariants), cộng thêm test riêng cho `MicroGold`, `JsCompat`, `GraphemeCounter` và tính thuần của domain.

## 7. Khác biệt có chủ đích và phần chưa port

**Khác biệt có thể quan sát so với `dispatch` của TS.** Bản TS chạy `evaluateAchievements` rồi `evaluateTutorial` sau mỗi lệnh thành công. U2.1 chưa port hai bước này (thuộc U4). Hệ quả cụ thể:
- Khi rồng đầu tiên lên Adult, bản TS cộng +100 G (`ACH_FIRST_ADULT`) vào `goldMicro`; bản C# thì chưa.
- `tutorial.completed` và hai danh sách thành tựu không tự tiến.

Điểm cắm cho hai bước này là `IPostCommandEvaluator`, được truyền vào `Dispatch` và chạy theo đúng thứ tự TS. U2.1 không truyền evaluator nào.

**Chưa port (sau U2.3):**
- Lệnh `buyTutorialPack`, `claimPack`, `setTutorialStatus`. Dispatch các lệnh này trả `invalid-command`.
- Đánh giá thành tựu và tutorial (`evaluateAchievements`, `evaluateTutorial`). Riêng `tutorial.profilesViewed` đã được `viewBiologyProfile` cập nhật.
- Roll biến thể/pity, roll gói trứng, lai tạo, kiểu hình, phân phối di truyền, phả hệ.
- Chăm sóc, thiên tai, cứu hộ, vận chuyển cứu hộ giữa zone.
- Persistence và chính sách thời gian đã có ở U2.2 (`Assets/Dravio/Persistence`, `Assets/Dravio/Platform`).
- Các trường dữ liệu của những phần này vẫn có trong `GameState` và được validator kiểm, để state tương thích schema v4.

**Giới hạn đã biết:**
- `GraphemeCounter` tự cài UAX #29. Lý do: `StringInfo` của Mono/Unity không tách cụm emoji ZWJ như `Intl.Segmenter`. Khớp golden 34 chuỗi (emoji gia đình, cờ, modifier, tag, Hangul, dấu kết hợp, Devanagari GB9c). Thuộc tính Extended_Pictographic/Prepend/InCB dùng bảng phạm vi rút gọn, còn thuộc tính khác lấy từ `CharUnicodeInfo` của runtime. Vì vậy với ký tự rất mới hoặc hiếm, số đếm có thể lệch so với ICU của Node.
- `string.Normalize(FormC)` và `CharUnicodeInfo` phụ thuộc bảng Unicode của runtime. Trên Mono của Unity các bảng này có thể cũ hơn .NET 8 (môi trường đã chạy test).

## 8. Phạm vi đã kiểm thử

- **Đã chạy:**
  - `dotnet test` với .NET SDK 8.0.134 trên Linux: 237/237 pass sau U2.3 (201 của U2.1 + 36 của U2.3).
  - U2.3: golden `ts-world-golden-b33ec7a.json` (Tools~/world-golden.test.ts) gồm 144 tổ hợp `canTravel`, khả năng tới của mọi loài, 3 kịch bản lệnh (490 bước, có fuzz 400 bước trên mọi lệnh U2.1 + U2.3) kèm `placementOptions`, `habitatUnlockStatus`, ngân sách, sức chứa và điều kiện khảo sát ở 56 điểm dò; tái sinh trùng từng byte. Cài thử 7 lỗi (chỗ trống tự tính, thứ tự kiểm, bỏ quyết toán, thứ tự mở, điều kiện khảo sát, sắp xếp hồ sơ, rời ngân sách khi vào kho): đều bị bắt.
  - Biên dịch Domain với `netstandard2.1` + C# 9: không lỗi, không cảnh báo.
  - Test TS gốc ở baseline (`npx vitest run`): pass.
  - Golden tái sinh trùng từng byte.
  - Thử cài lỗi có chủ đích (đổi XP ăn, phần dư vàng, bảng grapheme, hằng FNV): bộ test đều bắt được.
- **Chưa chạy:**
  - Biên dịch và Test Runner trong Unity Editor 6000.6.10f1.
  - Chạy trên Mono/IL2CPP và trên thiết bị.
  - So sánh trên Windows.
  - Hiệu năng với số rồng lớn.
