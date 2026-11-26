# Dravio Persistence & Platform (C#) — U2.2

U2.2 port lớp lưu game và chính sách thời gian của Dravio từ TypeScript sang C#. Nguồn đối chiếu là **nldanh13/dravio @ `b33ec7a3dc98b4a74857fe92d143475028fc0d96`**:
- ADR 0006 (persistence), 0007 (Save Schema), 0009 (`discoveries`), 0010 (vòng đời, ghi đa tab), 0013 (schema v4);
- `src/persistence/*`, `src/platform/*`;
- test `tests/unit/persistence.test.ts` và `tests/unit/g1Fixes.test.ts`;
- fixture `tests/fixtures/save-schema-v1.json`, `save-schema-v2.json`.

U2.2 không đổi luật game nào. Giới hạn offline 24 h, hiệu suất vàng offline 75 %, high-water mark và receipt > 60 s vẫn nằm trong `Dravio.Domain`; lớp này chỉ quyết định *khi nào* gửi `advanceTime` online hay offline.

## 1. Tách lớp

| Assembly | Thư mục | Phụ thuộc | Nội dung |
|---|---|---|---|
| `Dravio.Domain` | `Assets/Dravio/Domain` | không có | U2.1, không đổi trong U2.2 |
| `Dravio.Persistence` | `Assets/Dravio/Persistence` | Domain, `noEngineReferences` | JSON tương thích JS, Save Schema v4, SHA-256, migration, `SaveFile`, `SaveManager`, kho bộ nhớ/file, khóa người ghi, `Autosave` |
| `Dravio.Platform` | `Assets/Dravio/Platform` | Domain, Persistence, `noEngineReferences` | `TimePolicy`, `LifecycleMapper`, `PumpedScheduler`, `GameSession` |
| `Dravio.Platform.Unity` | `Assets/Dravio/Platform/Unity` | ba assembly trên + UnityEngine | `DravioSessionHost` (MonoBehaviour), `SystemClock` |

Chỉ `Dravio.Platform.Unity` chạm vào UnityEngine và đọc đồng hồ hệ thống. Test `LayeringTests` sẽ fail nếu điều này bị vi phạm.

`DravioSessionHost` **chưa được gắn vào Scene nào**: U2.2 không sửa Scene hay UI. Lớp trình bày sau này thêm component này vào một GameObject khởi động, rồi đọc `Session.State`, `Session.Status` và đăng ký `Session.Changed`.

## 2. Save Schema v10 (v4 của TS + `breedingNest` + `codex` + `expedition` + trạng thái mở thế giới + sức khỏe / thiên tai + giới hạn gói / dự án / sổ nghiên cứu + nhiệm vụ phụ / bộ đếm thám hiểm / quan hệ)

Envelope có các trường `{ format: "dravio-save", schemaVersion: 8, contentVersion, savedAtMs, revision, checksum, state }`.

**U2.5 — Schema v5 chỉ có ở bản Unity.** TS b33ec7a dừng ở v4. Bản Unity thêm đúng một khóa `breedingNest`: `null`, hoặc `BreedingEgg` theo GDD Phụ lục C.2 (`id, parentIds, speciesId, genotype, mutation, createdAtMs, finishAtMs, durationMs, tapReducedMs, lastTapAtMs, status`).
- **Migration 4 → 5**: thêm `breedingNest = null`. Rồng nào mang `activity = "breeding"` mà không có tổ (state v4 không thể tạo ra trứng) được trả về `idle`. Ngoài ra không đổi vàng, rồng, gene, phả hệ hay thành tựu.
- **Lưu trữ bản gốc**: save v4 do TS ghi được giữ nguyên văn trong `archive:schema-4` trước lần ghi v5 đầu tiên.
- **Mã lỗi**: save v4 hỏng nội dung vẫn báo `invalid-state` như TS, không thành `migration-failed`, vì bước 4 → 5 không biến đổi dữ liệu.
- **Validator v5**: chặn trứng có bố mẹ không ở trạng thái lai, rồng "breeding" mà không có tổ, `finishAtMs ≠ createdAtMs + durationMs − tapReducedMs`, và phần giảm do gõ vượt 10 %. Nhờ vậy sửa tay để rút ngắn ấp hoặc lai hai lần cùng lúc đều bị từ chối khi nạp.
- **Tương thích ngược**: TS b33ec7a đọc save v5 sẽ báo `newer-version`, tức chỉ đọc và không ghi đè, đúng chính sách hiện có. CI kiểm cả hai chiều:
  - mọi save C# đều có file `.v5.json` mà TS phải từ chối là `newer-version`;
  - khi tổ trống, ảnh chiếu v4 (`.save.json`) phải được TS đọc ra state trùng từng byte, kể cả rồng lai có `parentIds`, `generation 2`, `origin breed`.
- **Parity với golden persistence TS v4**: các phép so chạy trên ảnh v5 của kết quả TS (`PFix.AsV5`, đúng bước migration), hoặc chiếu save C# về v4 (`PFix.AsV4`/`AsV4Text`). Canonical JSON, SHA-256 và văn bản `serializeSave` của phần chung vẫn trùng từng byte.

- `GameStateJson` ánh xạ `GameState` ↔ `state` JSON. Các trường lấy từ `PlayerState` được trải phẳng lại thành `goldMicro`, `goldRemainder`, `totalGoldEarnedMicro`, `inventory`, `upgrades`, `pityCount`, `stats`, `settings`, đúng như `src/types/state.ts`.
- Trường optional đang `null` thì bị bỏ khỏi JSON, giống `undefined` của TS.
- `checksum` = SHA-256 hex của `canonicalJson(state)`, tức JSON có khóa sắp xếp theo đơn vị UTF-16. Checksum chỉ để phát hiện hỏng hoặc sửa tay, **không phải chữ ký**.
- `JsonParser` và `JsonWriter` bám đúng `JSON.parse`/`JSON.stringify` của V8:
  - số in theo Number::toString, tự cài bằng `BigInteger` nên không phụ thuộc `double.ToString` của Mono hay IL2CPP;
  - `-0` in thành `0`, `NaN`/`Infinity` in thành `null`;
  - surrogate lẻ được escape `\udxxx`;
  - khóa trùng thì giá trị sau thắng;
  - BOM, dấu phẩy thừa, số 0 đứng đầu bị từ chối.
- Thức ăn đang rơi (`food.drops`) không được lưu (ADR 0004 #10).
**U2.7 — Schema v6** thêm `codex`: `{ [speciesId]: { discoveredMutations, totalHatched, observedStages } }`, theo GDD Phụ lục C.2 và thứ tự catalog.
- **Migration 5 → 6**:
  - Ghi bù Bách khoa từ mọi rồng đang có, kể cả rồng đã thả. Biến thể của rồng được ghi thành ô đã khám phá; stage quan sát gồm mọi stage ≤ stage hiện tại. `totalHatched` để 0 vì không có dữ liệu, không suy diễn.
  - Không đổi trường nào khác. ID trang trí ngoài catalog trong save TS cũ được **giữ nguyên**, vì TS cho phép chuỗi bất kỳ ở đó.
  - Bản gốc nằm trong `archive:schema-5`, và save v4 của TS vẫn nằm trong `archive:schema-4`.
- **Validator v6**: Bách khoa phải đúng loài, đúng thứ tự, không trùng. Trang trí: không trùng, và món đang bật phải là món đã sở hữu.
- **Tương thích chéo với TS**: ảnh chiếu v4 bỏ `breedingNest` và `codex`. Trang trí, khóa và cấp nâng cấp là các trường có sẵn ở v4 nên TS vẫn đọc trùng state. File `.unity.json` (v6 thật) bị TS từ chối với `newer-version`.
- **Lưu ngay**: sau `sellDragon`, `bulkSell`, `buyUpgrade`, `buyDecoration`, `lockDragon`.
**U2.8 — Schema v7** thêm `completedQuests` (mảng ID nhiệm vụ), `grantedRewards` (mảng ID thưởng mở thế giới, mỗi cái một lần), `rewardQueue` (mảng `{ rewardId, speciesId, mutation, queuedAtMs }`: trứng thưởng chờ có chỗ; ADR 0201) và `expedition`: `null`, hoặc `{ id, routeId, dragonUid, startedAtMs, finishAtMs, rewards: { vậtLiệu: số }, status: "in-progress" | "ready" }`.
- **Migration 6 → 7**: thêm `expedition = null`. Rồng mang `activity = "expedition"` mà không có chuyến (chỉ có thể do sửa tay) được trả về `idle`. Vật liệu trong `inventory` giữ nguyên. Bản gốc nằm trong `archive:schema-6`.
- **Validator v7**: tuyến phải có trong catalog; `finishAtMs = startedAtMs + thời lượng`; thưởng tổng 2–4 và đúng vật liệu; rồng đang đi phải có `activity = expedition`; không rồng "expedition" nào thiếu chuyến. Sửa tay để rút ngắn chuyến hoặc tăng thưởng đều bị từ chối khi nạp. Nhiệm vụ/thưởng phải là ID catalog, không trùng; thưởng chỉ hợp lệ khi vùng của nó đã mở; hàng chờ chỉ chứa thưởng đã trao, không trùng, và không trùng trứng đang chờ nhận. Migration v6 → v7 đặt cả ba mảng là rỗng.
- **Ảnh chiếu v4 cho TS**: bỏ `expedition` khi slot trống; thế giới đã mở và vật liệu vẫn hợp lệ với TS.
- **Lưu ngay** sau `startExpedition` (seed + thưởng đã chốt) và `claimExpedition`.
**U2.6 — Schema v8** thêm `vitals` cho từng rồng (`{ health, energy, comfort, mobility, hydration }`, số nguyên 0–100), `healthCases` (mảng ca: `caseId, dragonId, eventId?, cause, site, severity, symptoms, assessed, carePlanId?, createdAtMs, startedAtMs, durationMs, finishAtMs, nextReviewAtMs, phase, progress, reviewed, status`), `disasters` (mảng sự kiện mở: `id, hazard, zoneId, severity, phase, createdAtMs, forecastProgressMs, struckAtMs, caseIds, sheltered[{ uid, habitatId, producing, auto }]`) và `hazard` (`{ foregroundMs, checksDone, cooldownUntilMs: { zoneId: ms } }`). Xem ADR 0202.
- **Migration 7 → 8**: thêm `healthCases = []`, `disasters = []`, `hazard = { 0, 0, {} }` và `vitals` 100 cho mọi rồng (kể cả rồng đã thả). Không đổi trường nào khác, kể cả rồng ở `shelter` / `care` từ save cũ (ADR 0202 D8). Bản gốc nằm trong `archive:schema-7`.
- **Validator v8**: xem Domain README mục 5e. Sửa tay chỉ số, rút ngắn chăm sóc, đặt kế hoạch khi chưa nhận định, trỏ ca sang rồng khác hay trú ẩn rồng không tồn tại đều bị từ chối (`invalid-state`).

**U2.10 — Schema v9** thêm `packDaily` (`{ day: "YYYY-MM-DD" | null, starterBought }`: số gói Starter mua bằng vàng trong ngày UTC, tối đa 5), `sanctuaryProjectsBuilt` (0–60) và `research` (`{ species: [{ speciesId, behaviors, phases, habitats, days, findingsByDay }], individuals: [{ uid, speciesId, observations, seenInFavoriteHabitat, seenFeeding, lastObservedAtMs, activePhases, restPhases, revealed }] }`). Xem ADR 0205.

- **Migration 8 → 9**: `packDaily = { day: null, starterBought: 0 }`, `sanctuaryProjectsBuilt = 0`, `research` rỗng. Không đổi trường nào của v8; save gốc giữ ở `archive:schema-8`.
- **Sổ nghiên cứu không bao giờ làm mất save**: khối `research` được làm sạch khi nạp và khi ghi (bỏ mục không hợp lệ); khối hỏng hẳn thành sổ rỗng. `packDaily` / `sanctuaryProjectsBuilt` sửa tay sai bị từ chối (`invalid-state`).
- Ảnh v4 cho TS bỏ ba khóa này (`EconomyResearchSaveTests`).

**U2.11 — Schema v10** thêm `completedSideQuests` (ID nhiệm vụ phụ, thứ tự catalog), `expeditionsClaimed` (`{ routeId: số chuyến đã nhận }`) và `relations` (`{ pairs: [{ a, b, sharedMs }] }`). Xem ADR 0206.
- **Migration 9 → 10**: ba khối rỗng, không suy diễn từ dữ liệu cũ. Save gốc giữ ở `archive:schema-9`.
- **Quan hệ không bao giờ làm mất save**: `relations` được làm sạch khi nạp và khi ghi; khối hỏng thành rỗng. `completedSideQuests` / `expeditionsClaimed` sửa tay sai bị từ chối.
- Ảnh v4 cho TS bỏ ba khóa này (`WorldProgressionSaveTests`).

**U2.12 — không đổi schema** (ADR 0207). Save v10 của một hành trình thật (người chơi theo hướng dẫn, chỉ lệnh thật, từ save mới tới khi mở đủ ba thế giới) qua vòng lưu–nạp trùng từng byte canonical, hợp lệ, và sau khi nạp không nhận lại được nhiệm vụ hay trứng thế giới (`JourneyEndStateSaveTests`). Ảnh v4 không biểu diễn được chuyến thám hiểm đang chạy, nên test này không xuất cho TS.
- **Ảnh chiếu v4 cho TS**: bỏ `vitals`, `healthCases`, `disasters`, `hazard`. Chỉ chiếu được khi không có ca mở hay sự kiện mở; save có ca chỉ xuất `.unity.json` (TS từ chối `newer-version`).
- **Lưu ngay** sau `moveToShelter`, `assessCase`, `setCarePlan`, `startCare`, `reviewCase`, `dischargeDragon`, và sau tick có `hazardForecast`, `disasterStruck`, `healthCaseOpened`, `dragonSheltered`, `disasterClosed`, `careCompleted` (tắt app ngay sau đó không làm mất hay tạo lại ca).
- Migration: 1 → 2 → 4 → 5 → 6 → 7 → 8 → 9 → 10. Số 3 là bí danh tiền phát hành của v1. Thứ tự kiểm: checksum trên state gốc → migrate → validate. Bản gốc được giữ vĩnh viễn trong `archive:schema-N`. Save do bản mới hơn ghi sẽ khóa ghi cả phiên.

## 3. Lưu nguyên tử, backup, phục hồi

`FileSaveStore` ghi vào `Application.persistentDataPath/saves`.

**Cơ chế ghi.** Mỗi lần lưu tạo một thế hệ bất biến `save-<gen>.json` theo trình tự: ghi file tạm → `Flush(true)` (fsync) → rename không ghi đè. Chỉ sau đó mới xóa các thế hệ cũ hơn 3 bản gần nhất. Như vậy:
- main = thế hệ mới nhất; backup1, backup2 = hai thế hệ trước đó;
- mất điện ở bất kỳ bước nào chỉ có thể để lại file `*.tmp` cụt (bị bỏ qua và dọn ở lần lưu sau) hoặc một thế hệ thừa;
- không có slot nào bị ghi dở.

**Khi đọc.** Thử main → backup1 → backup2. Mỗi slot phải qua JSON, checksum, migration và validate.
- Mọi slot đều hỏng → `Unrecoverable`. Hệ thống không tự ghi đè; khi người chơi chọn bắt đầu lại, dữ liệu hỏng được đẩy xuống backup.

**Một người ghi.** File `dravio-save-writer.lock` được mở độc quyền; phiên thứ hai chỉ đọc. Ngoài ra còn kiểm compare-and-swap trên main: nếu nơi khác đã ghi thì phiên chuyển sang `Conflict` và không ghi đè.

**Trùng lặp và import.**
- Save trùng (cùng `saveId` + `revision`) bị bỏ qua.
- Import đi qua hai bước (xem trước rồi xác nhận). Hệ thống luôn bỏ `registryRef` (Verified) và mọi khóa lạ như `entitlements` hay `purchases`.

## 4. Thời gian online/offline (ADR 0010)

`GameSession` xử lý theo thứ tự sau:
1. **Khi mở app:** nạp save → `advanceTime` offline (75 %, chặn 24 h) → **lưu ngay** → tick online 1 Hz.
2. **Autosave:** mỗi 30 s theo thời gian đơn điệu (`realtimeSinceStartup`), nên đổi giờ hệ thống không ảnh hưởng.
3. **Khi ẩn (Hide):** quyết toán online tới lúc ẩn, rồi lưu ngay.
4. **Khi hiện lại (Show):** thời gian vắng mặt được tính offline, có receipt nếu > 60 s.
5. **Khoảng trống giữa hai tick > 5 s** (ngủ máy, treo) thì cũng tính offline.

Tín hiệu Unity được `LifecycleMapper` ánh xạ như sau:

| Tín hiệu Unity | Mobile | Desktop |
|---|---|---|
| `OnApplicationPause(true)` | Hide | Hide |
| `OnApplicationPause(false)` | Show | Show |
| `OnApplicationFocus(false)` | Hide | Flush: vẫn online vì cửa sổ còn hiện, giống tab mất focus nhưng vẫn hiển thị của bản web |
| `OnApplicationFocus(true)` | Show | Show |
| `OnApplicationQuit` | Hide | Hide |

**Chống cộng thưởng offline nhiều lần:**
- Phần quyết toán offline được lưu ngay sau khi mở app. Nếu app chết trước khi lưu, lần mở sau tính lại *cùng* khoảng thời gian từ mốc cũ, không cộng dồn.
- `Show` lặp lại không có tác dụng.
- High-water mark được lưu, nên chỉnh giờ lùi không cho thêm gì. Chỉnh giờ tới để nhận thưởng rồi trả giờ về cũng không được trả hai lần.
- Khôi phục từ backup cũ không bao giờ cho nhiều vàng hơn đi qua main.

## 5. Khác biệt nền tảng so với bản web

| Chủ đề | TypeScript (trình duyệt) | C# (Unity) |
|---|---|---|
| Kho | IndexedDB `dravio/saves`; xoay 3 slot trong một transaction `durability: strict` | Thư mục file; thế hệ bất biến + fsync + rename. Rename thư mục chưa được fsync riêng: .NET không có API này. Với ext4/f2fs/APFS/NTFS, rename sau fsync được xem là đủ an toàn khi mất điện, nhưng chưa được kiểm trên thiết bị. |
| Archive | khóa `archive:schema-N` trong cùng transaction | file `archive-schema-N.json`, ghi trước thế hệ mới. Mất điện giữa hai bước chỉ để lại archive (vô hại). |
| Một người ghi | Web Locks, hoặc lease `localStorage` khi không có Web Locks | `FileShare.None` trên file khóa; hệ điều hành tự nhả khi tiến trình chết. Mono/Unity trên macOS/Linux có thể chỉ khóa trong tiến trình (chưa kiểm). CAS vẫn chặn ghi đè. |
| Vòng đời | `visibilitychange`, `freeze`, `pagehide`, `pageshow` | `OnApplicationPause`/`Focus`/`Quit` (bảng mục 4). Trên iOS/Android, app bị hệ điều hành giết khi chạy nền không có callback; dữ liệu an toàn nhờ save lúc Hide. |
| Hẹn giờ | `setInterval` | `PumpedScheduler` bơm trong `Update` bằng `realtimeSinceStartup`. Khi app bị treo lâu, mỗi việc chỉ chạy bù một lần. |
| API | async (Promise), hàng đợi ghi tuần tự | đồng bộ, có khóa nội bộ. Ghi 3–200 KB kèm fsync chạy trên main thread mỗi 30 s và khi Hide; trên thiết bị chậm có thể gây giật một frame (chưa đo). |
| Khóa lạ trong state | được giữ khi nạp rồi lưu lại (chỉ bị lọc khi import) | model C# có kiểu **bỏ khóa lạ** khi nạp. Checksum vẫn kiểm trên văn bản gốc, nên không ảnh hưởng tính hợp lệ. |
| Thông điệp lỗi | `validateGameState` trên object thô | Mọi lỗi TS báo đều có trong lỗi C# (đã kiểm trên golden). C# có thể báo thêm lỗi kiểu khi đổi sang model có kiểu. |
| WebGL | n/a | chưa hỗ trợ. `persistentDataPath` trên WebGL là IDBFS và cần `FS.syncfs` để bền; file khóa không có tác dụng. |

## 6. Kiểm thử

Lệnh chạy ngoài Unity, với .NET SDK 8:

```bash
dotnet test "Assets/Dravio/Tests/Persistence/DotNet~/Dravio.Persistence.Tests.csproj"
```

```bash
bash "Assets/Dravio/Tests/Persistence/Tools~/KillTest/kill-test.sh" 40
```

Trong Unity: *Test Runner → EditMode → `Dravio.Persistence.Tests`*.

CI: `.github/workflows/domain-persistence-tests.yml` gồm các bước:
- test U2.1 và U2.2 trên Ubuntu và Windows;
- kill-test trên Linux;
- tái sinh golden từ TS gốc rồi `git diff --exit-code`;
- TS đọc lại save do C# ghi.

Nội dung bộ test:
- **Golden** `Tests/Persistence/Golden/ts-persistence-golden-b33ec7a.json`, sinh từ TS gốc bằng `Tools~/persistence-golden.test.ts`, tái sinh trùng từng byte:
  - 535 số và 17 chuỗi đối chiếu `JSON.stringify`;
  - 4 state đối chiếu canonical + SHA-256 + `serializeSave` (state mới trùng từng byte);
  - 54 file đi qua `parseSaveText`: hợp lệ, cụt, BOM, khóa trùng, escape unicode, số mũ, `-0`, `1e400`, sửa tay, checksum hoa, sai kiểu, phiên bản 0/−1/1/2/3/5/99, migration lỗi, khóa lạ, 2 fixture thật v1/v2;
  - báo cáo lọc import, chính sách thời gian.
- **Mất điện:**
  - mô phỏng mất điện ở *từng* thao tác ghi (`MemoryFileSystem`), kể cả lần lưu đầu sau migration;
  - main hỏng trên đĩa thật: cụt, 0 byte, lật 1 bit, UTF-8 sai, sector toàn 0;
  - kill-test `SIGKILL` 40 vòng trên tiến trình thật.
- **Đồng hồ và phiên:** đồng hồ lùi giữa hai phiên, nhảy giờ tới rồi lùi, offline 24 h qua nhiều lần khởi động, crash trước khi lưu quyết toán offline, Hide/Show dồn dập, chuỗi vòng đời ngẫu nhiên (5 seed × 300 bước, kiểm vàng ≤ tốc độ × thời gian thực), phiên thứ hai chỉ đọc, xung đột ghi, save bản mới hơn, bắt đầu lại.
- **Tương thích chéo:** 18 save do C# ghi được `parseSaveText` của TS đọc với state trùng (`CrossRuntimeExportTests` + `Tools~/verify-csharp-saves.test.ts`).
- **U2.4 (`ProgressSaveTests`):** `tutorial` / `pendingPack` / thành tựu qua vòng lưu–nạp Schema v4 không đổi một byte canonical; `GameSession` lưu ngay sau `buyTutorialPack` và `claimPack` (không đợi autosave 30 s; lệnh bị từ chối không ghi); 3 lần khởi động lại không cấp lại thưởng và không mua được gói lần hai; save v2 thật sau migration không được cấp gì, lần dispatch đầu ghi nhận `TUT_01`, `TUT_02` và `ACH_FIRST_ADULT` (+100 G) đúng một lần, giống TS. 3 save C# giữa/sau hướng dẫn được xuất cho TS đọc (tổng cộng hơn 20 save chéo).
- **U2.8 (`ExpeditionSaveTests`):**
  - Migration v6 → v7.
  - Thám hiểm và thế giới mở rộng qua lưu–nạp; phần thưởng không roll lại khi nạp.
  - Sửa tay bị từ chối.
  - Thoát game giữa chuyến: mở lại sau 1 giờ thì chuyến đã hoàn tất offline.
  - Nhận thưởng đúng một lần qua nhiều lần khởi động lại.
- **U2.7 (`SanctuarySaveTests`):**
  - Migration v5 → v6 ghi bù Bách khoa (kể cả rồng đã thả), giữ ID trang trí lạ, giữ `archive:schema-5`. Lần dispatch đầu sau đó mở thành tựu biến thể/Legendary đúng một lần.
  - Bách khoa, trang trí, khóa và nâng cấp qua vòng lưu–nạp; ảnh chiếu v4 cho TS đọc.
  - Lưu ngay sau giao dịch.
  - Khởi động lại: rồng đã bán không quay lại, không nhận lại thưởng, khóa vẫn còn.
  - `requestId` trùng không bán lần hai.
- **U2.5 (`BreedingSaveTests`):** tổ đang ấp qua vòng lưu–nạp v5; migration v4 → v5 (giữ mọi trường, lưu `archive:schema-4` nguyên văn, giải phóng rồng "breeding" mồ côi); tổ bị sửa tay bị từ chối khi nạp; `GameSession` lưu ngay sau `buyPack` / `startBreed` / `claimBreed`; tắt app ngay sau giao dịch rồi mở lại: không roll lại, không trừ vàng hai lần, nhận con đúng một lần; mua thất bại không ghi và không đổi vàng.

Chưa chạy:
- Unity Editor 6000.6.10f1: chưa biên dịch thật, chưa chạy Test Runner. `DravioSessionHost` chỉ được biên dịch với UnityEngine giả.
- Mono/IL2CPP.
- Android/iOS: vòng đời thật, app bị kill khi chạy nền, mất điện thật.
- Windows trên thiết bị (job Windows trong CI là máy ảo, chưa chạy vì CI chưa được kích hoạt trên PR này tại thời điểm viết).
- Hiệu năng ghi trên main thread.
