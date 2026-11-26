# Dravio Core Domain (C#) — U2.1 → U2.9

Đây là bản port lõi domain của Dravio từ TypeScript sang C# thuần. Nguồn đối chiếu là **nldanh13/dravio @ `b33ec7a3dc98b4a74857fe92d143475028fc0d96`**, gồm GDD 3.1, ADR 0003/0004/0010/0013, `src/types`, `src/data`, `src/engine` và `tests/unit`. Luật, ID, thông số cân bằng và công thức kinh tế được giữ nguyên. Phần nào khác bản TS thì có ghi rõ ở mục 7.

Code nằm trong assembly `Dravio.Domain` (`Dravio.Domain.asmdef`). Assembly này đặt `noEngineReferences: true`, không tham chiếu assembly nào khác và không dùng `MonoBehaviour`, Scene, Animator hay Unity UI. Thời gian và số ngẫu nhiên đều được tiêm từ ngoài vào (`IClock`, `IRng`). Test `Domain_sources_have_no_Unity_or_ambient_time_random_dependencies` sẽ fail nếu có code gọi `UnityEngine`, `System.Random` hoặc `DateTime.Now`.

## 1. Cấu trúc

| Thư mục | Namespace | Nội dung | Nguồn TypeScript |
|---|---|---|---|
| `Model/` | `Dravio.Domain` | `GameState`, `PlayerState`, `DragonState`, `DragonLocation`, `Genotype`, `ZoneState`, `FoodState`, `OfflineReceipt`, `PendingPack`, `TutorialState`, enum, `WireNames` | `src/types/*.ts` |
| `Catalog/` | `Dravio.Domain.Catalog` | 16 loài (`SpeciesCatalog`), sinh học G0 (`G0BiologyData`, sinh tự động), 7 microhabitat, locus P0, hằng số chăm sóc, bảng kinh tế, ID tiến trình | `src/data/*.ts` |
| `Economy/` | `Dravio.Domain` / `.Economy` | `MicroGold`, `EconomyRates` | `src/data/economy.ts`, `src/engine/economy/rates.ts` |
| `Biology/` | `Dravio.Domain.Biology` | `Lifecycle` (stage, tích phân vàng), `BondRules`, `Suitability`, `Genetics` (founder, kiểm genotype) | `engine/progression/growth.ts`, `engine/biology/bond.ts`, `engine/world/habitat.ts`, `engine/genetics/alleles.ts` (một phần) |
| `World/` | `Dravio.Domain.World` | `Placement.IsProducing`, `IsPlacedInValidHabitat`, `Zones`, `WorldGraph`, `Survey` (U2.3) | `engine/world/*.ts` |
| `Breeding/` | `Dravio.Domain.Breeding` | `BreedingRules` (U2.5, port TS), `Hatchery` (U2.5, **implementation mới**) | `engine/genetics/breeding.ts`; lệnh: GDD/ADR 0003 |
| `Expedition/` | `Dravio.Domain.Expedition` | `Expeditions`: thám hiểm (U2.8, **implementation mới**) | GDD tr. 22 |
| `Queries/` | `Dravio.Domain.Queries` | `Ecosystem`: API truy vấn chỉ đọc cho Unity (U2.8) | — |
| `Behavior/` | `Dravio.Domain.Behavior` | `PersonalityProfiles`, `BehaviorCatalog`, `BehaviorEngine`, `HabitatEcology`, `EcologyComfort` (tắt), `Research` (tắt): U2.9, **implementation mới**, chỉ đọc | — (ADR 0203) |
| `Sanctuary/` | `Dravio.Domain.Sanctuary` | `SanctuaryRules`: bán, bán nhanh, khóa, nâng cấp, trang trí (U2.7, **implementation mới**) | GDD tr. 15, Phụ lục C.3, E, F |
| `Progression/` | `Dravio.Domain.Progression` | `StarterPack`, `Achievements`, `Tutorial` (U2.4) | `engine/inventory/packs.ts` (phần gói hướng dẫn), `engine/progression/achievements.ts`, `engine/progression/tutorial.ts` |
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

| `BuyTutorialPackCommand()` (U2.4) | Gói Starter hướng dẫn (ADR 0013, GDD tr. 4/15, kịch bản S01): giá **100 G**, một lần mỗi save. Thứ tự kiểm: còn trứng chờ (`pack-pending`) → đã dùng (`tutorial-pack-used`) → thiếu vàng (`not-enough-gold`). Kiểm trước, quyết toán thời gian online, rồi trừ vàng. Kết quả **cố định**: Sproutling (`sprout`) Normal, genotype founder; uid trứng sinh từ `hashSeed("tutorial-pack", saveId)` nên cùng save luôn ra cùng trứng. Không chạm `pityCount` hay RNG domain. Đặt `tutorial.starterPackUsed = true`, tạo `pendingPack`, phát `packBought`. |
| `ClaimPackCommand(target)` (U2.4) | Nở trứng đang chờ vào kho hoặc microhabitat, dùng **cùng** kiểm tra sinh học/sức chứa của `moveDragon` (`Zones.CheckPlacement`). Rồng mới: XP 0, gen 1, `origin = pack`, tính cách theo `hashSeed("hatch", pack.id)`. `stats.totalHatched + 1`, phát `dragonHatched`. Không có trứng → `no-pending-pack`. `StarterPack.PackPlacementOptions` cho UI danh sách chỗ đặt. |
| `SetTutorialStatusCommand(status)` (U2.4) | Chỉ `active` (xem lại) hoặc `skipped` (bỏ qua); `completed` là `invalid-command`. Cùng trạng thái, hoặc skip khi đã hoàn tất → `already-there`. Về `active` đặt lại `startedAtMs = now`; từ `completed` thì xóa danh sách bước (xem lại từ đầu). Không quyết toán thời gian. |

**Đánh giá sau mỗi lệnh thành công (U2.4).** Giống `dispatch` của TS, sau mỗi lệnh thành công (kể cả không đổi gì), dispatcher luôn chạy `Achievements.Evaluate` rồi `Tutorial.Evaluate` trên bản sao làm việc, trước các `IPostCommandEvaluator` bổ sung. Lệnh bị từ chối hoặc trùng `requestId` không đánh giá, nên không thể nhận thưởng qua lệnh lỗi.
- Thành tựu: bản TS b33ec7a mới cài luật cho **2/12** thành tựu — `ACH_FIRST_HATCH` (`stats.totalHatched ≥ 1`, thưởng 50 G) và `ACH_FIRST_ADULT` (có rồng chưa thả ở Adult, thưởng 100 G). Thưởng cộng thẳng vào `goldMicro`, **không** vào `totalGoldEarnedMicro`. Một thành tựu đã có trong `unlockedAchievements` không bao giờ được đánh giá lại (chống nhận nhiều lần, kể cả khi save có thành tựu mở mà chưa nhận). Emberling khởi tạo **không** tính là lần nở (`totalHatched` bắt đầu 0); chỉ `claimPack` tăng bộ đếm. Bảng thưởng 12 mục (GDD Phụ lục F) có trong `ProgressCatalog.AchievementRewardG`.
- Hướng dẫn TUT_01–TUT_08 (ADR 0009 mục 4, ADR 0013): chỉ chạy khi `status = active`; điều kiện đọc GameState, không đọc thao tác UI; bước chỉ hoàn thành theo thứ tự, điều kiện đã thỏa trước được ghi liên tiếp khi tới lượt. TUT_01: có rồng chưa thả trong zone và `clock.lastProcessedAtMs − startedAtMs ≥ 15 s`; TUT_02: có rồng đã ăn được XP (`lastFeedXpAtMs` khác null — thả thức ăn sai diet không tính); TUT_03: đã xem hồ sơ Emberling; TUT_04: đá ấm đã mở; TUT_05: đã mua và nhận gói Starter (`starterPackUsed` và không còn `pendingPack`); TUT_06: bờ hồ đã mở; TUT_07: có rồng chưa thả ở Adult; TUT_08: ba microhabitat Origin đều mở. Đủ 8 bước → `completed` + `tutorialStatusChanged`. Hướng dẫn không cho thêm vàng ngoài thưởng thành tựu.

`GameSession` (Platform) lưu ngay sau `buyTutorialPack` và `claimPack` thành công (`IMMEDIATE_SAVE` của `gameSession.ts`), để tắt app không làm lại được kết quả gói hay thưởng đã nhận.

`WorldGraph` (U2.3): node = zone, cạnh `origin_ocean_estuary` (cần bơi, cửa ngõ bờ hồ ↔ cửa sông) và `origin_sky_low_island` (cần bay, đồng cỏ ↔ đảo thấp có chỗ đậu); `CanTravel` là BFS. Giống bản TS b33ec7a, `CanTravel` **chưa** được nối vào `moveDragon` vì Ocean/Sky còn khóa ở G1 (ADR 0010 mục 4). Không có lệnh nào để UI tự khai báo khám phá hay mở sinh cảnh (`recordDiscovery` là hàm engine, không phải lệnh).

Hành vi chung của dispatch:
- Lệnh sai hình dạng (requestId rỗng hoặc > 64 ký tự, enum lạ, ID rỗng, nickname > 1024, đích claim/move sai, status hướng dẫn khác active/skipped) đều trả `invalid-command`. Từ U2.4 đủ 13 loại lệnh của bản TS b33ec7a.
- `requestId` trùng trả `Duplicate = true`, không event, không tăng revision. Code nhớ 200 requestId gần nhất.
- Lệnh bị từ chối trả lại **chính object state đầu vào**, không tiêu RNG và không tiêu vàng.
- Lệnh thành công trả bản sao mới, tăng revision 1 và ghi nhận requestId.

`DomainError` mang mã có kiểu, chuỗi wire và khóa i18n `commandError.<code>`; đủ 30 mã của TS. `DomainInvariantException` dành cho dữ liệu hỏng hoặc lỗi lập trình (ID loài lạ, tràn số), không dành cho lệnh bị từ chối.

`GameStateValidator.Validate` port đủ `validateGameState`: enum, số hữu hạn, phạm vi, UID trùng, tham chiếu sản xuất, ngân sách, sức chứa 5/zone, microhabitat cần khám phá, sinh học (suitability ≥ 50), thức ăn, đồng hồ, tiến trình v4 và receipt. Thông điệp lỗi trùng chuỗi với bản TS.

## 5b. U2.5 — Trứng, di truyền và lai tạo

**Hai loại mã, hai mức cam kết.**

| Phần | Nguồn | Parity với TS |
|---|---|---|
| `Genetics.LocusDistribution`, `DerivePhenotype`, `RollOffspringGenotype`; `BreedingRules.AreRelated`, `CandidateSpecies`, `CheckBreed`, `SpeciesDistribution`, `PreviewBreed`, `QueryBreedPreview`, `BreedFeeG`, `HatchSeconds`, `RollBreed`; `Mutation.Distribution`, `Mutation.Roll`; `PackRolls.RollSpecies` | Port nguyên văn hàm thuần TS b33ec7a (`alleles.ts`, `genetics/breeding.ts`, `economy/mutation.ts`, `economy/packs.ts`) | **Có** — golden `ts-genetics-golden-b33ec7a.json`, khớp bit-exact (xác suất double, chuỗi RNG, lượng RNG tiêu thụ) |
| Lệnh `buyPack`, `startBreed`, `tapEgg`, `claimBreed`; tổ ấp `breedingNest`; Save Schema v5 | **Implementation mới** theo GDD 3.1 (trang 8, 15, 32, Phụ lục C) và ADR 0003 — TS b33ec7a **không có** các lệnh này | **Không** tuyên bố parity. Kiểm bằng test theo luật tài liệu + test phân phối nhiều seed |

**Luật đã cài (trích tài liệu):**
- **Gói trứng** (GDD tr. 15): Starter 100 G 90/10/0/0, Elemental 900 G 48/45/7/0, Mystic 6 000 G 15/50/32/3, Astral 30 000 G 0/28/55/17. Một gói một trứng; chọn độ hiếm theo bảng rồi chọn đều loài `packEligible` (loại Amberhorn/Aurorawing). Roll một lần, lưu ngay vào `pendingPack`, nhận bằng `claimPack` (U2.4). Thứ tự kiểm: gói lạ → `pack-pending` → `storage-full` → `not-enough-gold`.
- **Biến thể / pity** (GDD tr. 15, ADR 0003 #6): q = min(0,60; 15w/(85+15w) + 0,08b), w = 1 + 0,25·luck; Crystal/Golden/Prismatic = q × 11/15, 3,5/15, 0,5/15. Lần thứ 200 liên tiếp không Prismatic bảo đảm Prismatic, reset khi ra. Gói thường và lai **tính** pity; trứng hướng dẫn (U2.4) **không**.
- **Không có quảng cáo** (quyết định của chủ dự án, U2.5): game không có quảng cáo và không có phần thưởng nào gắn với quảng cáo. Ba mục GDD tr. 29 xếp là "quảng cáo mobile" — Starter miễn phí 3/ngày, `boostEgg` (−25 % thời gian ấp còn lại) và nhận ×2 vàng offline — **không được cài và sẽ không cài**. Trường `stats.dailyFreePacksClaimed` / `lastDailyResetDate` và `offlineReceipt.bonusClaimed` vẫn giữ trong schema chỉ để tương thích save TS v4 (luôn giữ nguyên giá trị, không lệnh nào dùng). Trứng chỉ đến từ mua bằng vàng hoặc lai.
- **Sức chứa** (GDD Phụ lục C): "giới hạn kho đề xuất 500 rồng, pending không mất khi đầy". Chỗ đã dùng = rồng chưa thả + trứng chờ nhận + trứng trong tổ. Mỗi trứng giữ trước một chỗ (ADR 0003), nên trứng đã có luôn nhận được.
- **Di truyền** (GDD tr. 8): pigment R/r và eye B/b trội hoàn toàn, pattern P/p trội không hoàn toàn. Mỗi bố mẹ góp một allele với xác suất ½, các locus phân ly độc lập. Kiểu hình luôn dẫn xuất, không lưu. Biến thể Aether không phải allele.
- **Lai** (GDD tr. 8, ADR 0003 #1–#4): chỉ lai cùng nhóm T/A/V, cả hai Adult, `activity = idle`, không trong care, không thả, không ở shelter khẩn cấp, đã hết cooldown. Kho an toàn và rồng bị khóa vẫn lai được. Cấm họ hàng **đã biết**: bố mẹ–con, chung cha/mẹ, chung ông bà, ông bà–cháu. Dữ liệu tổ tiên thiếu thì không suy diễn. Phí theo độ hiếm cao nhất của bố mẹ 80/350/1 500/5 000 G, biết trước khi roll. Loài con: trọng số 60/30/9/1 sau khi bỏ nhóm rỗng; Epic cần ít nhất một bố mẹ Rare+, Legendary cần cả hai Epic+.
- **Tổ ấp** (GDD tr. 8, 32; ADR 0003): 1 tổ. Thời lượng = ceil(5/15/60/180 phút × (1 − 0,1·hatchSpeed)) theo độ hiếm **con**. Trong lúc ấp, bố mẹ ở `activity = breeding`: không sản xuất, không di chuyển được. Khi xong (kể cả offline, kể cả khi trứng còn chờ vì kho đầy), bố mẹ về idle đúng tại `finishAtMs`, cooldown 10 phút. Quyết toán vàng tách đôi tại mốc này. `tapEgg`: −3 s/lần, cách nhau ≥ 2 s, tổng ≤ floor(10 % thời lượng).
- **Phả hệ** (ADR 0003 #4): `parentIds` của con là hai bố mẹ; `generation = max(gen bố mẹ) + 1`; `ancestry = known`; `grandparentIds` = hợp `parentIds` đã biết của bố mẹ. Nếu cả hai bố mẹ là founder thì để `null`: unknown, không phải "không có họ hàng". `origin = breed`; `totalHatched` và `totalBred` +1 khi nhận con.
- **RNG tất định**: mỗi giao dịch dùng một luồng riêng, seed = `hashSeed(tag, saveId, revision)` (tag `buyPack` / `breed`). Kết quả chỉ phụ thuộc state, không phụ thuộc requestId hay RNG hiệu ứng hình ảnh của lớp trình bày. Domain không có RNG toàn cục. Nạp lại save không roll lại được vì cùng state cho cùng kết quả và kết quả đã được lưu ngay.
- **Lưu ngay** (`GameSession.IsImmediateSave`): `buyTutorialPack`, `claimPack`, `buyPack`, `startBreed`, `claimBreed`.

**Quyết định còn thiếu / cần chủ dự án xác nhận** (không tự đặt thông số cân bằng):
1. **Giới hạn kho 500**: GDD ghi là "đề xuất (v2)". Đang dùng 500, cần chốt.
2. **Nguồn trứng miễn phí thay cho quảng cáo**: sau khi bỏ quảng cáo, game chỉ còn trứng hướng dẫn (một lần) và gói mua bằng vàng. Có cần nguồn miễn phí khác (ví dụ thưởng mốc) để bù nhịp tiến triển hay không là quyết định cân bằng, chưa đặt.
3. **Ngưỡng health ≥ 80, mobility ≥ 80, comfort ≥ 60 và "không có HealthCase mở"** (ADR 0003): GameState chưa có dữ liệu sức khỏe (TD-05, hệ chăm sóc chưa port). Giống TS, điều kiện này **chưa** được kiểm, cũng không giả lập "khỏe" bằng giá trị mặc định. Cần dữ liệu sức khỏe trước.
4. **Không có tăng tốc ấp bằng quảng cáo**: chỉ còn nâng cấp `hatchSpeed` và gõ tổ (≤ 10 %). Trường `boostUsed` của GDD đã bỏ khỏi schema v5.
5. **Gõ làm trứng xong ngay**: trứng hoàn tất tại thời điểm gõ, không lùi về quá khứ. Bố mẹ không được tính vàng bù cho khoảng dưới 3 s.
6. **Lộ kết quả**: event `breedStarted` không công bố loài/biến thể trước khi nở. Kết quả vẫn nằm trong save, giống `pendingPack` của TS. Chưa có quyết định ẩn/mã hóa.
7. **Không cho chọn chỗ ở mức "khu bảo tồn"**: GDD nói "nếu chưa mở habitat của loài, giữ tại khu bảo tồn thích hợp không sản xuất". Hiện dùng kho an toàn (`storage`) cho vai trò này.
8. **Thứ tự RNG trong một giao dịch** (loài → biến thể → UID trứng; lai: độ hiếm → loài → allele → biến thể → UID) là lựa chọn của bản Unity, vì TS chưa có lệnh để so.

## 5c. U2.7 — Quản lý khu bảo tồn, nâng cấp, trang trí, Bách khoa

| Phần | Nguồn | Parity với TS |
|---|---|---|
| `EconomyRates.SellPriceG`, `UpgradeCostG`, `ProductionBudget` | Port TS (U2.1) | **Có** (golden U2.1: 192 tổ hợp giá bán, mọi cấp nâng cấp) |
| `ACH_FIRST_HATCH`, `ACH_FIRST_ADULT` | Port TS (U2.4) | **Có** (golden U2.4) |
| Lệnh `sellDragon`, `bulkSell`, `lockDragon`, `buyUpgrade`, `buyDecoration`, `toggleDecoration`; Bách khoa (`codex`); 10 thành tựu còn lại; Schema v6 | **Implementation mới** theo GDD 3.1 | **Không** — TS b33ec7a không có các lệnh/luật này |

**Luật đã cài (trích tài liệu):**
- **Bán** (GDD tr. 15, tr. 8):
  - Giá = floor(baseSell × 0,35/0,65/1 × mutationSell), tính theo stage lúc bán (đã quyết toán thời gian).
  - Không bán: rồng cuối cùng (`last-dragon`), rồng bị khóa (`dragon-locked`), rồng bận — đang lai, thám hiểm, care, shelter (`dragon-busy`) — và rồng đã thả.
  - Vàng bán cộng vào `goldMicro`, **không** vào `totalGoldEarned` (Phụ lục F). `stats.totalSold` + 1. Rồng rời ngân sách sản xuất.
  - Phả hệ của con cháu giữ UID bố mẹ, nên kiểm cận huyết vẫn đúng sau khi bán bố/mẹ (ADR 0003 #4).
- **Bán nhanh** (Phụ lục F):
  - Gồm mọi rồng Common Adult chưa khóa, không bận.
  - Người chơi xác nhận tổng tiền: lệnh mang `expectedTotalGoldMicro` lấy từ `SanctuaryRules.BulkSellQuoteMicro`. Nếu tổng đổi sau quyết toán thì báo `quote-changed` và không bán gì.
  - Luôn chừa một rồng.
- **Khóa** (`lockDragon`): chỉ chống bán và chuyển giao; rồng bị khóa vẫn lai được (ADR 0003).
- **Nâng cấp** (Phụ lục E):
  - Năm loại theo catalog: `islandCapacityLevel` 500/1,85/10, `foodQualityLevel` 200/1,75/10, `foodCooldownLevel` 250/1,8/8, `mutationLuckLevel` 1 500/2,2/8, `hatchSpeedLevel` 600/1,9/6.
  - Cost = floor(base × scale^level). Trừ vàng và tăng cấp cùng transaction. Hết cấp thì báo `upgrade-maxed`.
  - Hiệu ứng đã nối từ trước: ngân sách 5 + 2L (tối đa 25), XP ăn, số viên thức ăn, trọng số biến thể, thời gian ấp. `hatchSpeed` chỉ áp cho tổ mới.
  - GDD không đặt điều kiện mở khóa nào khác.
- **Trang trí** (Phụ lục E, tr. 21):
  - 6 món giữ ID và giá v2: `cherry_tree` 350, `crystal_pond` 1 200, `sky_lantern` 2 500, `ancient_gate` 6 000, `glow_mushroom` 12 000, `star_observatory` 25 000 G.
  - Mỗi món một bản sao theo khu, chỉ ở khu đã mở. Mua xong tự bật; bật/tắt miễn phí; không bán lại.
  - **Không có hiệu ứng kinh tế**: GDD v3 bỏ +5 % vàng mỗi món. "Hiệu ứng sinh cảnh" chỉ là đề xuất hình ảnh, thuộc lớp trình bày.
- **Bách khoa** (tr. 22, Phụ lục C.2, F):
  - 64 ô = 16 loài × 4 biến thể. Một ô được ghi khi người chơi **sở hữu** một rồng của loài/biến thể đó; trứng chưa nhận không tính.
  - Emberling khởi đầu là khám phá đầu tiên nhưng không phải lần nở (`totalHatched` của loài = 0).
  - Mỗi loài lưu `discoveredMutations`, `totalHatched`, `observedStages`.
  - Chỉ tăng, giữ sau bán/thả: được ghi lại ngay trước khi rồng rời kho.
- **10 thành tựu** (Phụ lục F; thưởng cộng vào vàng, mở và nhận một lần, không nhận lại sau khi bán rồi có lại):

| ID | Điều kiện | Thưởng |
|---|---|---|
| `ACH_FIRST_BREED` | "Nhận rồng lai đầu tiên" — `totalBred ≥ 1` (lúc `claimBreed`, không phải lúc bắt đầu lai) | 150 |
| `ACH_MUTATION_CRYSTAL` / `_GOLDEN` / `_PRISMATIC` | Ô Bách khoa đầu tiên có biến thể đó | 100 / 300 / 1 000 |
| `ACH_LEGENDARY` | Ô Bách khoa đầu tiên của loài Legendary | 2 000 |
| `ACH_GOLD_10K` / `ACH_GOLD_250K` | `totalGoldEarned` ≥ 10 000 / 250 000 G (chỉ vàng sản xuất) | 300 / 2 500 |
| `ACH_DECOR_ALL` | "Sở hữu đủ 6 loại trang trí bất kỳ đảo" | 1 000 |
| `ACH_CODEX_16` | Đủ 16 loài | 2 000 |
| `ACH_CODEX_MASTER` | ≥ 32/64 ô | 3 000 |

- **Không có quảng cáo**: không lệnh nào gắn với quảng cáo (xem 5b).
- **Thả rồng**: GDD chỉ định nghĩa `releaseDragon` trong luồng cứu hộ (Care/rescue, chưa port), nên **không** có lệnh thả rồng sở hữu.
- **Lưu ngay**: `sellDragon`, `bulkSell`, `buyUpgrade`, `buyDecoration`, `lockDragon`.

**Quyết định còn thiếu (U2.7)** — chưa tự đặt:
1. **Môi trường hợp lệ của từng món trang trí**: GDD ghi "chỉ đặt ở môi trường hợp lệ" nhưng không có bảng món ↔ realm/microhabitat. Hiện mọi khu đã mở đều nhận được. "6 vị trí định sẵn" (vị trí món nào ở đâu) thuộc lớp trình bày.
2. **Bán nhanh chừa con nào**: GDD chỉ nói "chừa một rồng". Hiện chừa rồng đầu tiên trong kho.
3. **Thả rồng sở hữu**: chưa có quy định ngoài luồng cứu hộ.
4. **Điều kiện mở khóa nâng cấp**: GDD không có; chỉ kiểm giá và cấp tối đa.
5. **`islandCapacityLevel` vô dụng ở G1**: ngân sách 5 + 2L, nhưng Khởi Nguyên chỉ có 5 chỗ vật lý và Ocean/Sky còn khóa. Cấp 1–10 tốn vàng mà không tăng sản xuất cho tới G2. Xem báo cáo kinh tế.
6. **`totalHatched` theo loài khi migration v5 → v6**: không có dữ liệu, để 0 (không suy diễn).
7. **"Khám phá" Bách khoa**: hiện định nghĩa là "sở hữu rồng". GDD còn nhắc khám phá qua thám hiểm/cứu hộ hoang dã — các hệ đó chưa port.

Phân tích cân bằng (chỉ đề xuất, không đổi thông số): [ECONOMY_AUDIT.md](ECONOMY_AUDIT.md).

## 5d. U2.8 — Mở rộng thế giới, di chuyển, thám hiểm, API truy vấn

Hướng dẫn tích hợp đầy đủ (chữ ký API, DTO, ví dụ, mã lỗi, event): [INTEGRATION.md](INTEGRATION.md).

| Phần | Nguồn | Parity với TS |
|---|---|---|
| `WorldGraph` (2 cạnh, cửa ngõ, `CanTravel`), 7 microhabitat và hồ sơ môi trường, suitability, sức chứa 5/zone, ngân sách 5 + 2L | Port TS (U2.1/U2.3) | **Có** (golden U2.3: 144 tổ hợp `canTravel`, khả năng tới của mọi loài, `placementOptions`) |
| Nối `canTravel` vào `moveDragon` (kèm luật cửa ngõ), `WorldExpansion.ApplyZoneUnlock`, thám hiểm, `Queries.Ecosystem`, Schema v7 | **Implementation mới** (ADR 0010 mục 4 "nối khi mở realm"; GDD tr. 5, 22) | **Không** |

**Luật đã cài:**
- **Di chuyển** (`moveDragon`), thứ tự kiểm giữ nguyên TS rồi thêm một bước cuối:
  - Một cạnh chỉ dùng được khi cả hai zone đã mở **và** cả hai microhabitat cửa ngõ đã mở. Rồng cần ít nhất một kiểu di chuyển của cạnh: bơi cho Thủy Vực, bay cho Thiên Giới.
  - Tìm đường bằng BFS. Từ Thủy Vực sang Thiên Giới phải đi qua Khởi Nguyên, nên cần cả bơi và bay.
  - **Kho là vùng trung lập** (ADR 0201): không phải điểm xuất phát Khởi Nguyên. Rời kho vào một thế giới cần một cạnh đang mở đi vào thế giới đó mà rồng đi được; Khởi Nguyên không có cạnh đi vào nên luôn vào được.
  - Chuyển chỗ quyết toán sản xuất đúng tại thời điểm chuyển (đã có từ U2.3).
- **Mở thế giới** (ADR 0201), lệnh `unlockZone(zoneId)`:
  - Thủy Vực cần hướng dẫn hoàn tất, đủ 3 sinh cảnh Khởi Nguyên và nhiệm vụ `QUEST_AQUATIC_EXPLORATION`. Thiên Giới cần Thủy Vực và `QUEST_SKY_EXPLORATION`. Không có điều kiện vàng hay cấp độ.
  - Nội dung nhiệm vụ là TBD, nên save mới vẫn khóa cả hai thế giới. `WorldExpansion.ApplyQuestComplete` là hàm engine cho hệ nhiệm vụ. Từ U2.11 (ADR 0206) lệnh `completeQuest` gọi hàm này cho nhiệm vụ cổng; xem `Docs/u2.11/WORLD_PROGRESSION.md`.
  - Khi mở: mở zone cùng mọi microhabitat; trao một lần trứng Droplet/Zephyr cố định, Normal, không tính pity. Kho đầy hoặc đang có trứng chờ → trứng nằm trong `rewardQueue`, giao sau lệnh thành công đầu tiên có chỗ.
- **Thám hiểm** (GDD tr. 22):

| Tuyến | Điều kiện | Thời gian | Thưởng |
|---|---|---|---|
| `origin_forest` (Rừng Khởi Nguyên) | 1 Adult sống đất (realm đất liền) | 15 phút | tổng 2–4 `wood`/`seed` |
| `ocean_reef` (Rạn Thủy Vực) | 1 Adult bơi | 30 phút | tổng 2–4 `crystal`/`coral` |
| `sky_cloud` (Sân mây Thiên Giới) | 1 Adult bay | 45 phút | chưa duyệt → tuyến khóa (`route-rewards-undefined`) |

  - Một slot chung. Tuyến chỉ mở khi thế giới của nó đã mở và có bảng thưởng được duyệt. Thành công 100 % (MVP).
  - Ngưỡng sức khỏe (Health ≥ 80, Mobility ≥ 80, Comfort ≥ 60, không có ca chăm sóc mở) được kiểm từ U2.6 (lỗi `vitals-below-threshold`, `detail` là điều kiện thiếu; ca mở → `dragon-busy`).
  - Seed và phần thưởng chốt lúc khởi hành: số lượng chọn đều trong 2–4, mỗi đơn vị chọn đều trong danh sách vật liệu của tuyến. Luồng RNG là `hashSeed("expedition", saveId, revision)`. Không phí.
  - Trong lúc đi, rồng không sinh vàng, không lai, không bán, không chuyển. Rồng giữ chỗ cũ.
  - Hoàn tất cả khi offline: rồng về idle đúng tại `finishAtMs`, quyết toán sản xuất được tách tại mốc này.
  - Nhận thưởng một lần, cộng vào `inventory`. Không tự khởi hành tiếp.
- **API truy vấn chỉ đọc** `Queries.Ecosystem`, gồm 13 hàm, xem INTEGRATION.md. Test xác nhận mã lỗi trả về trùng với kết quả dispatch thật trên mọi rồng.

**Quyết định U2.8 đã duyệt:** xem [ADR 0201](../../../Docs/adr/0201-u2-8-world-unlock-expedition-materials.md), gồm mở thế giới, thành công 100 %, ID vật liệu, ngưỡng sức khỏe, kho trung lập, trang trí chỉ thẩm mỹ (trường `Effects` = 0).

**Còn mở** (không tự đặt):
1. Nội dung hai nhiệm vụ mở thế giới.
2. Bảng thưởng `sky_cloud` và ID "bụi sao"; nguồn của `stone`.
3. Công thức dùng vật liệu (chế tạo, phục hồi).
4. Tỷ lệ thất bại thám hiểm (`SuccessPermille` có sẵn, đang tắt).
5. ~~Kiểm ngưỡng sức khỏe sau khi U2.6 được gộp.~~ Đã kiểm từ U2.6.
6. Điểm nghiên cứu từ thám hiểm (hệ nghiên cứu chưa port); vận chuyển bằng phương tiện cứu hộ.

## 5e. U2.6 — Sức khỏe, thiên tai, trú ẩn, chăm sóc

Quyết định và phần còn thiếu: [ADR 0202](../../../Docs/adr/0202-u2-6-health-disaster-rescue.md) (đề xuất, chờ duyệt). Hướng dẫn tích hợp: [INTEGRATION.md mục 7](INTEGRATION.md). Toàn bộ là **implementation mới theo GDD**, không có parity TS.

| Phần | Code | Trạng thái |
|---|---|---|
| Chỉ số `Vitals` (health, energy, comfort, mobility, hydration; 0–100, gốc 100) | `Model/Health.cs`, `DragonState.Vitals` | bật |
| Ngưỡng lai 80/80/60 + không ca mở (ADR 0003) | `Breeding/Hatchery.cs` (`HealthBlock`, chạy sau `BreedingRules.CheckBreed` để giữ parity) | bật |
| Ngưỡng thám hiểm 80/80/60 + không ca mở (ADR 0201 mục 4) | `Expedition/Expeditions.cs` (`DragonBlock`), `UncheckedHealthConditions` nay rỗng | bật |
| Trạm chăm sóc 2 chỗ, hàng chờ FIFO, nhận định → kế hoạch → bắt đầu → xem lại → xuất viện, 4 pha | `Health/HealthEngine.cs` (`CareCenter`) | bật |
| Cảnh báo 10 phút, rủi ro theo resilience, ≤ 2 ca, trú ẩn, cooldown 24 h, an toàn offline/Zen | `Health/HealthEngine.cs` (`DisasterEngine`) | **tắt** ở `DisasterRules.Production` (D7) |
| Truy vấn chỉ đọc | `Queries/HealthQueries.cs` | bật |

**Tất định và online/offline:** mọi roll dùng `Rng.HashSeed` theo `saveId`/`eventId`/UID, không đọc RNG của state, nên lưu–nạp không bao giờ roll lại. `advanceTime` online tách quyết toán tại từng mốc kiểm hazard và mốc hết dự báo, nên một lần tiến 75 phút cho kết quả trùng 4 500 tick 1 s (vàng, ca, sự kiện, vị trí rồng). Đồng hồ hazard chỉ tích khi online ở World mode. Tiến trình chăm sóc chạy theo đồng hồ thật, giống nhau online, offline và qua mức chặn 24 h.

**Validator v8:** chỉ số trong 0–100; ca duy nhất, đúng rồng, tối đa một ca mở mỗi rồng; kế hoạch hợp lệ và đã nhận định; ca đang chạy phải ở `care(caseId)`, ca chờ phải ở nơi trú; thời lượng khớp mức độ và kế hoạch; tiến trình không vượt đồng hồ; không quá 2 ca trong trạm và không để ca chờ khi trạm còn chỗ; sự kiện duy nhất mỗi zone, zone đã mở; không sản xuất khi đang có ca. Rồng ở nơi trú / trạm mà không có ca từ save cũ vẫn hợp lệ (D8).

## 5f. U2.9 — Cá tính, hành vi, sinh thái sinh cảnh, nghiên cứu

Quyết định và phần chờ duyệt: [ADR 0203](../../../Docs/adr/0203-u2-9-dragon-behavior-personality-ecology-research.md) (đề xuất). Hướng dẫn tích hợp: [INTEGRATION.md mục 8](INTEGRATION.md). Ma trận 16 loài: [BEHAVIOR_MATRIX.md](BEHAVIOR_MATRIX.md). Báo cáo cân bằng: [BEHAVIOR_SIMULATION.md](BEHAVIOR_SIMULATION.md). Toàn bộ là **implementation mới**, không có parity TS. **Không đổi Save Schema (vẫn v8), không có lệnh hay event mới.**

| Phần | Code | Trạng thái |
|---|---|---|
| Cá tính: 4 nét 0–100 dẫn xuất từ `Personality` đã lưu + lệch ±10 ổn định theo UID; thức ăn và microhabitat ưa thích | `Behavior/PersonalityProfile.cs` | bật (chỉ đọc) |
| 11 hành vi, khả năng theo loài và nơi ở, chu kỳ ngày đêm, ưu tiên gameplay, chọn có trọng số tất định theo nhịp 60 s | `Behavior/BehaviorCatalog.cs`, `Behavior/BehaviorEngine.cs` | bật (chỉ đọc; Sanctuary chưa nối) |
| Giải thích độ phù hợp theo từng yếu tố (điểm lấy từ Suitability, không engine mới) | `Behavior/HabitatEcology.cs` (`HabitatEcology`) | bật (chỉ đọc) |
| Môi trường → comfort (ADR 0202 P8 / 0203 C1–C2) | `Behavior/HabitatEcology.cs` (`EcologyComfort`) | **tắt** (`EcologyComfortRules.Production`) |
| Cấp nghiên cứu, mở hồ sơ Codex theo cấp, sổ quan sát tập tính | `Behavior/Research.cs` | **tắt** (`ResearchRules.Production`); sổ quan sát chưa lưu (Schema v9 chờ duyệt) |
| Truy vấn cho Unity | `Queries/BehaviorQueries.cs` | bật |

## 5g. U2.10 — Nghiên cứu, sinh thái xã hội, tiến trình thế giới

Quyết định và phần chờ duyệt: [ADR 0204](../../../Docs/adr/0204-u2-10-research-social-progression.md) (đề xuất). Thiết kế: [Docs/u2.10](../../../Docs/u2.10/). Toàn bộ là **implementation mới**, chỉ đọc. **Không đổi Save Schema (vẫn v8), không có lệnh hay event mới, không đổi giá hay công thức.**

| Phần | Code | Trạng thái |
|---|---|---|
| Sổ nghiên cứu, nhịp theo ngày, phát hiện tập tính / pha / sinh cảnh / cá thể, cơ hội quan sát | `Behavior/ResearchProgress.cs` | **tắt** (`ResearchPacing.Production`); sổ chưa lưu |
| Bản nháp khối `research` Schema v9 + migration v8 → v9 + validator | `Behavior/ResearchProgress.cs` (`ResearchSchemaDraft`) | **chỉ bản nháp**, chưa nối Persistence |
| Tập tính xã hội 16 loài, độ hòa đồng, độ quen từ phả hệ, ứng viên tương tác trình diễn | `Behavior/SocialEcology.cs` | bật (chỉ đọc, trình diễn) |
| Tác động comfort của quan hệ | `Behavior/SocialEcology.cs` (`ComfortTargetModifier`) | **tắt** (`SocialRules.Production`) |
| Nội dung nhiệm vụ cổng và nhiệm vụ phụ, evaluator | `Progression/QuestProposals.cs` | **tắt** (`QuestRules.Production`): không hoàn thành nhiệm vụ nào |
| Truy vấn cho Unity | `Queries/ProgressionQueries.cs` | bật |

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

Mỗi bước kịch bản so sánh ok/lỗi, `Duplicate`, việc trả lại đúng object state, toàn bộ event theo thứ tự và toàn bộ state sau lệnh. Golden U2.1/U2.3 được sinh khi C# chưa có thành tựu/hướng dẫn nên loại trừ tutorial, achievements và event của chúng, và vàng của TS đã trừ thưởng thành tựu; từ U2.4 test C# trừ phần thưởng C# tự cộng trước khi so, nên các golden cũ đồng thời kiểm số tiền thưởng khớp TS.

**Golden U2.4** (`Golden/ts-progress-golden-b33ec7a.json`, sinh bằng `Tools~/progress-golden.test.ts`) so **toàn bộ** state (tutorial, thành tựu, vàng thật gồm thưởng) và **mọi** event, cộng probe `currentStep` / `packPlacementOptions` / `hatchFromPack` sau từng bước. 13 kịch bản, 584 bước: chơi hết 8 bước, thứ tự/cascade, TUT_01 cần rồng trong zone, bỏ qua và xem lại, luật gói (requestId trùng, `pack-pending`, `tutorial-pack-used`, `no-pending-pack`, chỗ đặt bị khóa/không hợp, đích sai), 100 G − 1 microG và đúng 100 G, zone đầy, chống thưởng lặp, thành tựu mở mà chưa nhận, Emberling khởi tạo không tính lần nở, save Schema v2 thật sau migration, và fuzz 500 bước trên đủ 13 lệnh.

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

**Khác biệt so với `dispatch` của TS.** Từ U2.4 không còn khác biệt quan sát được trên 13 lệnh của bản b33ec7a: thành tựu và hướng dẫn chạy sau mỗi lệnh thành công như TS (trước U2.4, C# thiếu +100 G của `ACH_FIRST_ADULT`). `IPostCommandEvaluator` vẫn có để cắm thêm luật, chạy sau phần đánh giá có sẵn.

**Chưa port (sau U2.6):**
- Lượt nghiên cứu biến thể định hướng (GDD tr. 22); `releaseDragon` (luồng cứu hộ); nội dung nhiệm vụ mở Thủy Vực/Thiên Giới (TBD); dùng vật liệu (chế tạo decor, phục hồi zone); tuyến thám hiểm P1 theo loài, di tích, cứu hộ hoang dã; mốc Codex 64/64 và thành tựu cứu hộ (P1).
- Văn bản i18n `tutorial.step.*`, `tutorial.hint.*`, `achievement.*` (thuộc UI, chưa port vào Unity).
- Luồng cứu hộ trong hướng dẫn (`acceptRescue`, `advanceRehab`, `releaseDragon`), vận chuyển cứu hộ giữa zone, hư hại môi trường vùng, công thức môi trường → comfort (ADR 0202 P6–P8).
- Persistence và chính sách thời gian đã có ở U2.2 (`Assets/Dravio/Persistence`, `Assets/Dravio/Platform`).
- Các trường dữ liệu của những phần này vẫn có trong `GameState` và được validator kiểm, để state tương thích schema v4.

**Giới hạn đã biết:**
- `GraphemeCounter` tự cài UAX #29. Lý do: `StringInfo` của Mono/Unity không tách cụm emoji ZWJ như `Intl.Segmenter`. Khớp golden 34 chuỗi (emoji gia đình, cờ, modifier, tag, Hangul, dấu kết hợp, Devanagari GB9c). Thuộc tính Extended_Pictographic/Prepend/InCB dùng bảng phạm vi rút gọn, còn thuộc tính khác lấy từ `CharUnicodeInfo` của runtime. Vì vậy với ký tự rất mới hoặc hiếm, số đếm có thể lệch so với ICU của Node.
- `string.Normalize(FormC)` và `CharUnicodeInfo` phụ thuộc bảng Unicode của runtime. Trên Mono của Unity các bảng này có thể cũ hơn .NET 8 (môi trường đã chạy test).

## 8. Phạm vi đã kiểm thử

- **Đã chạy:**
  - U2.9: Domain 422 pass + 3 bỏ qua có chủ đích (thêm báo cáo mô phỏng hành vi); Persistence 192/192 (không đổi); Domain biên dịch được với `netstandard2.1` + C# 9. `BehaviorTests` (40) và `BehaviorSimulationTests` (2 chạy trong CI) gồm vân tay golden tất định, khả năng theo sinh học trên hơn 50 000 quyết định, SeekFood → `consumeFood` luôn hợp lệ, truy vấn không đổi state, kinh tế trùng khớp khi chèn truy vấn vào mô phỏng U2.7, luật đề xuất mặc định tắt. Cài thử 5 lỗi (bỏ kiểm hồi feed, bay không cần chỗ đậu, bỏ ngủ theo nhịp, gợi ý đi bộ dưới nước, giao một drop cho nhiều rồng): đều bị bắt.
  - U2.6: Domain 380 pass + 2 bỏ qua có chủ đích; Persistence 192/192; bộ đọc TS (`verify-csharp-saves.test.ts`) đọc đúng mọi save C# xuất ra, kể cả save v8.
  - U2.8: Domain 342 pass + 2 bỏ qua có chủ đích; Persistence 182/182. Cài thử 8 lỗi U2.8 (bỏ kiểm cửa ngõ, bỏ kiểm kiểu di chuyển, khóa tuyến, slot, nhận hai lần, tách quyết toán, khoảng 2–4, migration rồng mồ côi): đều bị bắt.
  - `dotnet test` với .NET SDK 8.0.134 trên Linux: Domain 330 pass + 2 bỏ qua có chủ đích (sinh báo cáo mô phỏng và đo thời gian, chỉ chạy khi đặt biến môi trường) sau U2.7; Persistence 179/179. Cài thử 12 lỗi U2.7: 11 bị bắt, 1 tương đương hành vi (Bách khoa quét cả rồng đã thả — rồng đó đã được ghi khi còn sở hữu).
  - U2.5: golden `ts-genetics-golden-b33ec7a.json` (Tools~/genetics-golden.test.ts) — 27 phân phối locus, 28 kiểu hình, 256 cặp loài (ứng viên, phân phối, phí), 28 thời gian ấp, 27 phân phối biến thể, 4 × 400 roll biến thể/pity, 8 × 300 roll gói, 6 × 200 roll lai, 100 genotype con, 23 ca checkBreed. Tái sinh trùng từng byte. Test phân phối nhiều seed qua đường lệnh: 4 gói × 8 000, biến thể 2 × 20 000 (luck 0 và 8), lai 12 000 (loài, 3 locus, biến thể b = 2), Mendel 27 tổ hợp × 6 000, pity trên 5 seed × 50 000. Dung sai 4,5σ, seed cố định nên không chập chờn. Hồi quy Tutorial/Achievement: mọi golden U2.1/U2.3/U2.4 vẫn pass nguyên trạng. Cài thử 13 lỗi (hệ số bố mẹ biến thể, pity lệch 1, chung ông bà, luật Legendary, thứ tự allele, cooldown, giữ chỗ trứng, pity gói, reset ngày, tách quyết toán, trần gõ, …): đều bị bắt.
  - U2.4: golden `ts-progress-golden-b33ec7a.json` 13 kịch bản / 584 bước, tái sinh trùng từng byte. Port `tests/unit/tutorial.test.ts` (`TutorialProgressTests`). Cài thử 8 lỗi (mốc quan sát 14 s, không tăng `totalHatched`, Emberling khởi tạo tính là nở, thưởng lặp, biên 100 G, thứ tự lỗi gói, không đưa vào `claimed`, bỏ lưu ngay sau mua/nhận): đều bị bắt.
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
