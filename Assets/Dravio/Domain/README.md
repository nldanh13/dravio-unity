# Dravio Core Domain (C#) — U2.1 → U2.5

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
| Lệnh `buyPack`, `claimFreeStarterPack`, `startBreed`, `tapEgg`, `claimBreed`; tổ ấp `breedingNest`; Save Schema v5 | **Implementation mới** theo GDD 3.1 (trang 8, 15, 29, 32, Phụ lục C) và ADR 0003 — TS b33ec7a **không có** các lệnh này | **Không** tuyên bố parity. Kiểm bằng test theo luật tài liệu + test phân phối nhiều seed |

**Luật đã cài (trích tài liệu):**
- **Gói trứng** (GDD tr. 15): Starter 100 G 90/10/0/0, Elemental 900 G 48/45/7/0, Mystic 6 000 G 15/50/32/3, Astral 30 000 G 0/28/55/17. Một gói một trứng; chọn độ hiếm theo bảng rồi chọn đều loài `packEligible` (loại Amberhorn/Aurorawing). Roll một lần, lưu ngay vào `pendingPack`, nhận bằng `claimPack` (U2.4). Thứ tự kiểm: gói lạ → `pack-pending` → `storage-full` → `not-enough-gold`.
- **Biến thể / pity** (GDD tr. 15, ADR 0003 #6): q = min(0,60; 15w/(85+15w) + 0,08b), w = 1 + 0,25·luck; Crystal/Golden/Prismatic = q × 11/15, 3,5/15, 0,5/15. Lần thứ 200 liên tiếp không Prismatic bảo đảm Prismatic, reset khi ra. Gói thường, gói miễn phí và lai **tính** pity; trứng hướng dẫn (U2.4) **không**.
- **Gói Starter miễn phí** (GDD tr. 29): tối đa 3 gói/ngày UTC (`stats.dailyFreePacksClaimed` + `lastDailyResetDate`), odds Starter, không trừ vàng. GDD xếp đây là **thưởng quảng cáo mobile** và yêu cầu "entitlement do store/backend xác thực, không trust save; mock chỉ DEV": Domain giữ hạn mức, còn `GameSession` từ chối lệnh nếu `SessionEnvironment.RewardedAdVerified` là null hoặc trả false. Bản Unity hiện **chưa có** adapter quảng cáo, nên trên production lệnh này bị chặn.
- **Sức chứa** (GDD Phụ lục C): "giới hạn kho đề xuất 500 rồng, pending không mất khi đầy". Chỗ đã dùng = rồng chưa thả + trứng chờ nhận + trứng trong tổ. Mỗi trứng giữ trước một chỗ (ADR 0003), nên trứng đã có luôn nhận được.
- **Di truyền** (GDD tr. 8): pigment R/r và eye B/b trội hoàn toàn, pattern P/p trội không hoàn toàn. Mỗi bố mẹ góp một allele với xác suất ½, các locus phân ly độc lập. Kiểu hình luôn dẫn xuất, không lưu. Biến thể Aether không phải allele.
- **Lai** (GDD tr. 8, ADR 0003 #1–#4): chỉ lai cùng nhóm T/A/V, cả hai Adult, `activity = idle`, không trong care, không thả, không ở shelter khẩn cấp, đã hết cooldown. Kho an toàn và rồng bị khóa vẫn lai được. Cấm họ hàng **đã biết**: bố mẹ–con, chung cha/mẹ, chung ông bà, ông bà–cháu. Dữ liệu tổ tiên thiếu thì không suy diễn. Phí theo độ hiếm cao nhất của bố mẹ 80/350/1 500/5 000 G, biết trước khi roll. Loài con: trọng số 60/30/9/1 sau khi bỏ nhóm rỗng; Epic cần ít nhất một bố mẹ Rare+, Legendary cần cả hai Epic+.
- **Tổ ấp** (GDD tr. 8, 32; ADR 0003): 1 tổ. Thời lượng = ceil(5/15/60/180 phút × (1 − 0,1·hatchSpeed)) theo độ hiếm **con**. Trong lúc ấp, bố mẹ ở `activity = breeding`: không sản xuất, không di chuyển được. Khi xong (kể cả offline, kể cả khi trứng còn chờ vì kho đầy), bố mẹ về idle đúng tại `finishAtMs`, cooldown 10 phút. Quyết toán vàng tách đôi tại mốc này. `tapEgg`: −3 s/lần, cách nhau ≥ 2 s, tổng ≤ floor(10 % thời lượng).
- **Phả hệ** (ADR 0003 #4): `parentIds` của con là hai bố mẹ; `generation = max(gen bố mẹ) + 1`; `ancestry = known`; `grandparentIds` = hợp `parentIds` đã biết của bố mẹ. Nếu cả hai bố mẹ là founder thì để `null`: unknown, không phải "không có họ hàng". `origin = breed`; `totalHatched` và `totalBred` +1 khi nhận con.
- **RNG tất định**: mỗi giao dịch dùng một luồng riêng, seed = `hashSeed(tag, saveId, revision)` (tag `buyPack` / `freeStarter` / `breed`). Kết quả chỉ phụ thuộc state, không phụ thuộc requestId hay RNG hiệu ứng hình ảnh của lớp trình bày. Domain không có RNG toàn cục. Nạp lại save không roll lại được vì cùng state cho cùng kết quả và kết quả đã được lưu ngay.
- **Lưu ngay** (`GameSession.IsImmediateSave`): `buyTutorialPack`, `claimPack`, `buyPack`, `claimFreeStarterPack`, `startBreed`, `claimBreed`.

**Quyết định còn thiếu / cần chủ dự án xác nhận** (không tự đặt thông số cân bằng):
1. **Giới hạn kho 500**: GDD ghi là "đề xuất (v2)". Đang dùng 500, cần chốt.
2. **Gói miễn phí hằng ngày**: GDD chỉ có "Starter miễn phí tối đa 3/ngày" dưới mục quảng cáo mobile. Chưa có quyết định gói miễn phí không cần quảng cáo (desktop/web), chưa có adapter quảng cáo/entitlement, chưa chốt quota cho Remove Ads/Supporter.
3. **Ngưỡng health ≥ 80, mobility ≥ 80, comfort ≥ 60 và "không có HealthCase mở"** (ADR 0003): GameState chưa có dữ liệu sức khỏe (TD-05, hệ chăm sóc chưa port). Giống TS, điều kiện này **chưa** được kiểm, cũng không giả lập "khỏe" bằng giá trị mặc định. Cần dữ liệu sức khỏe trước.
4. **`boostEgg`** (−25 % thời gian còn lại, một lần/tổ, quảng cáo mobile): chưa cài vì cần entitlement quảng cáo. Trường `boostUsed` có sẵn trong schema, luôn `false`.
5. **Gõ làm trứng xong ngay**: trứng hoàn tất tại thời điểm gõ, không lùi về quá khứ. Bố mẹ không được tính vàng bù cho khoảng dưới 3 s.
6. **Lộ kết quả**: event `breedStarted` không công bố loài/biến thể trước khi nở. Kết quả vẫn nằm trong save, giống `pendingPack` của TS. Chưa có quyết định ẩn/mã hóa.
7. **Không cho chọn chỗ ở mức "khu bảo tồn"**: GDD nói "nếu chưa mở habitat của loài, giữ tại khu bảo tồn thích hợp không sản xuất". Hiện dùng kho an toàn (`storage`) cho vai trò này.
8. **Thứ tự RNG trong một giao dịch** (loài → biến thể → UID trứng; lai: độ hiếm → loài → allele → biến thể → UID) là lựa chọn của bản Unity, vì TS chưa có lệnh để so.

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

**Chưa port (sau U2.5):**
- Luật của 10/12 thành tựu còn lại (`ACH_FIRST_BREED`, `ACH_MUTATION_*`, `ACH_LEGENDARY`, `ACH_GOLD_10K/250K`, `ACH_DECOR_ALL`, `ACH_CODEX_16`, `ACH_CODEX_MASTER`) — bản TS b33ec7a cũng **chưa** cài, chỉ có ID và thưởng. UI hiện thành tựu chưa cài là "chưa mở".
- `boostEgg` (quảng cáo), adapter quảng cáo/entitlement thật; điều kiện sức khỏe khi lai (cần hệ chăm sóc); lượt nghiên cứu biến thể định hướng (GDD tr. 22); bán/thả rồng (`sellDragon`, `releaseDragon`); nâng cấp (`buyUpgrade`) — các cấp `mutationLuckLevel`/`hatchSpeedLevel` đã được lệnh U2.5 đọc nhưng chưa có lệnh tăng.
- Nút nhận ×2 vàng offline (`offlineReceipt.bonusClaimed`).
- Văn bản i18n `tutorial.step.*`, `tutorial.hint.*`, `achievement.*` (thuộc UI, chưa port vào Unity).
- Chăm sóc, thiên tai, cứu hộ, vận chuyển cứu hộ giữa zone.
- Persistence và chính sách thời gian đã có ở U2.2 (`Assets/Dravio/Persistence`, `Assets/Dravio/Platform`).
- Các trường dữ liệu của những phần này vẫn có trong `GameState` và được validator kiểm, để state tương thích schema v4.

**Giới hạn đã biết:**
- `GraphemeCounter` tự cài UAX #29. Lý do: `StringInfo` của Mono/Unity không tách cụm emoji ZWJ như `Intl.Segmenter`. Khớp golden 34 chuỗi (emoji gia đình, cờ, modifier, tag, Hangul, dấu kết hợp, Devanagari GB9c). Thuộc tính Extended_Pictographic/Prepend/InCB dùng bảng phạm vi rút gọn, còn thuộc tính khác lấy từ `CharUnicodeInfo` của runtime. Vì vậy với ký tự rất mới hoặc hiếm, số đếm có thể lệch so với ICU của Node.
- `string.Normalize(FormC)` và `CharUnicodeInfo` phụ thuộc bảng Unicode của runtime. Trên Mono của Unity các bảng này có thể cũ hơn .NET 8 (môi trường đã chạy test).

## 8. Phạm vi đã kiểm thử

- **Đã chạy:**
  - `dotnet test` với .NET SDK 8.0.134 trên Linux: 315/315 pass sau U2.5 (thêm 49 của U2.5); Persistence 176/176.
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
