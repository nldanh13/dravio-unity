# U2.14 — Tài liệu Domain cho AI tích hợp (Playable Alpha U6.0)

> Nền: [U2.13 API](../u2.13/API_INTEGRATION.md) → [U2.12 API](../u2.12/API_INTEGRATION.md) (mục 0 "Quy tắc tích hợp" vẫn bắt buộc).
> **U2.14 không thêm hay đổi lệnh, truy vấn, event, mã lỗi hay trường save. Save Schema v10.** Mọi thay đổi của U2.14 nằm trong thư mục test.
> Tài liệu này gom lại những gì AI tích hợp cần để dựng Playable Alpha theo đúng hành trình đã kiểm thử ([ALPHA_VALIDATION.md](ALPHA_VALIDATION.md)).

## 0. Trạng thái luật cho Alpha

| Hệ | Trong game | UI làm gì |
|---|---|---|
| Tuyến `sky_cloud` (bảng thưởng) | **TẮT**, chờ duyệt | thẻ tuyến khóa: `RouteExplorationDto.RewardsDefined == false`, lệnh trả `route-rewards-undefined` |
| Trứng Aurorawing ở chuyến `sky_cloud` thứ 15 | luật **đã duyệt**, nhưng không đạt được khi tuyến tắt | có thể hiện "Chuyến thứ 15: trứng Aurorawing" (`NextMilestoneEggSpeciesId`) kèm trạng thái khóa; **không** hứa ngày nhận |
| Luật cho ăn B (rồng được chọn ăn trước) | **TẮT**, chờ duyệt | dùng cách làm được ngay với luật hiện hành (mục 3) |
| Vật liệu dự án theo thế giới (ADR 0209) | bật | đọc `SanctuaryEconomy(...).NextProjectMaterials` |
| Bộ sưu tập Alpha | **15 / 16 loài** thu được; Aurorawing chưa | Codex hiện ô Aurorawing là "chưa mở" kèm nguồn (mục 4) |

Không đổi `SkyCloudBalance.Production`, không gọi `SkyCloudBalance.Use(...)` ngoài test.

## 1. Hành trình Alpha theo lệnh (đã chạy thật trong `U214AlphaJourneyTests`)

Thứ tự dưới đây là thứ tự người chơi theo hướng dẫn thật sự đi qua (3 seed, 3 × 10 phút/ngày). Cột "Ngày" là trung vị; mọi bước chỉ dùng lệnh trong cột "Lệnh".

| # | Bước | Lệnh | Truy vấn / event UI cần | Ngày |
|---|---|---|---|---|
| 1 | Hoàn thành tutorial (TUT_01…08) | `advanceTime`, `feed` + `consumeFood`, `viewBiologyProfile("emberling")`, `surveyHabitat` + `unlockHabitat` (đá ấm, bờ hồ), `buyTutorialPack`, `claimPack` | `TutorialStepCompleted`, `TutorialStatusChanged(Completed)` | 0 (phiên đầu) |
| 2 | Cho ăn, lớn | `feed(diet, zone, habitat)` rồi `consumeFood(foodId, dragonUid)` | `FoodConsumed`, `StageChanged(→Adult)` | 0 |
| 3 | Mua gói | `buyPack(packId)` → `claimPack(target)` | `PackBought`, `DragonHatched`; lỗi `storage-full`, `pack-daily-limit` | 0 |
| 4 | Lai và ấp | `startBreed(a, b)` → `tapEgg` (mỗi ≥ 2 s) → `claimBreed(target)` | `BreedStarted`, `EggTapped`, `EggReady`, `DragonHatched` | 1–2 |
| 5 | Mở đủ sinh cảnh Khởi Nguyên | `surveyHabitat` + `unlockHabitat` | `HabitatUnlocked` | 0 |
| 6 | Nghiên cứu sinh học | `viewBiologyProfile`, `observeDragon(uid, phase)` | `DragonObserved`, `NightJournalWritten`; lỗi thường gặp `observation-cooldown`, `not-observable` (bình thường, không báo lỗi to) | liên tục |
| 7 | Nhiệm vụ | `completeQuest(questId)` khi `QuestLog(s).ReadyQuestIds` có | `QuestCompleted`, `QuestRewarded` | 0,2–10 |
| 8 | Mở Thủy Vực | `unlockZone("ocean")` | `ZoneUnlocked`, `RewardEggGranted(ZONE_EGG_OCEAN)` | 3,5 |
| 9 | Mở Thiên Giới | `unlockZone("sky")` | `RewardEggGranted(ZONE_EGG_SKY)` | 9–13 |
| 10 | Thám hiểm, nhận thưởng | `startExpedition(route, uid)` → `claimExpedition` | `ExpeditionReady`, `ExpeditionClaimed`, `ExplorationMilestoneReached` | 2 (rừng), 5 (rạn) |
| 11 | Dự án Sanctuary | `buildSanctuaryProject` | `SanctuaryProjectBuilt { materialIds }`; chặn `not-enough-gold` / `not-enough-materials` | 5,5 (dự án 1) |

Mã lỗi gặp trong hành trình thật (40 ngày, seed 1): `observation-cooldown`, `not-observable`, `zone-full`, `production-budget-full`. Đều là phản hồi bình thường của UI (chỗ đầy, chưa hết hồi), không phải lỗi hệ thống. Lệnh bị từ chối **không bao giờ đổi state** (đã kiểm sau mỗi lệnh).

## 2. Bảo đảm Domain mà UI có thể dựa vào (đã kiểm sau MỖI lệnh của hành trình)

| Bảo đảm | Ý nghĩa cho UI |
|---|---|
| Δvàng = sản xuất + bán + thưởng thành tựu − chi | hiển thị vàng từ `State.Player.Gold`; không có vàng ẩn từ nhiệm vụ / thám hiểm / quan sát |
| Sản xuất của một lần quyết toán ≤ Σ rate Adult của rồng trong ngân sách × thời gian | biên lai offline (`OfflineReceipt`) không bao giờ vượt trần |
| Δkho = thám hiểm + mốc − dự án − giao nhiệm vụ | kho vật liệu không âm, không tự tăng |
| Một lần: thành tựu, nhiệm vụ, mốc, trứng thưởng | UI có thể phát hiệu ứng "lần đầu" theo event, không cần tự chống lặp |
| Gửi lại cùng `requestId` (kể cả sau khi nạp save) → `Duplicate`, không event | retry mạng / double-tap an toàn nếu UI giữ nguyên requestId |
| State là hàm thuần của (save mới, log lệnh, đồng hồ) | tái hiện lỗi: gửi log lệnh + seed là đủ |

## 3. Cho ăn với luật hiện hành (luật B chưa duyệt)

Luật A giao phần ăn cho rồng thích loại đó trước rồi UID nhỏ nhất, nên rồng người chơi chạm vào có thể không phải rồng tới ăn. Trong lúc chờ duyệt B, UI làm được ngay, không đổi luật:

1. Chọn loại thức ăn mà rồng được chọn ăn được và rồng khác cùng microhabitat không ưu tiên (`SpeciesCatalog.GetBiology(id).DietIds`, `PersonalityProfiles.Of(d).FavoriteDiet`).
2. `feed(...)` rồi gửi `consumeFood(foodId, selectedUid)` cho đúng rồng đó.

Trong hành trình Alpha, cách này cho 120 / 120 bữa đúng rồng được chọn ("ăn" quan sát được; "tìm ăn" của rồng được chọn vẫn phụ thuộc luật A). Xem [BALANCE_DECISIONS.md](BALANCE_DECISIONS.md) mục 2.

## 4. Codex / bộ sưu tập

Bảng nguồn từng loài: [COLLECTION_MATRIX.md](COLLECTION_MATRIX.md). Cho UI:

- Gói: tỉ lệ = `PackCatalog.Odds(pack, rarity) / số loài cùng độ hiếm có trong gói`. Amberhorn và Aurorawing **không** có trong gói.
- Amberhorn: chỉ lai (cặp nhóm T có bố/mẹ Magma, ~9,1 % mỗi lần nở). `BreedingRules.PreviewBreed(a, b)` cho tỉ lệ hiển thị.
- Aurorawing: chỉ trứng `EXPEDITION_EGG_AURORAWING` (chuyến `sky_cloud` thứ 15, một lần / save). Khi đã có, Aurorawing × rồng nhóm V lai ra Aurorawing được.
- **Cảnh báo nên có trong UI:** bán Aurorawing duy nhất là mất loài vĩnh viễn (không trứng thứ hai, không cặp lai nào khác). Bách khoa vẫn giữ. Gợi ý: nhắc khóa (`lockDragon`) hoặc hộp xác nhận khi bán rồng thưởng cuối cùng của loài. Domain không chặn (luật hiện hành).

## 5. Save

- Không đổi so với U2.13. Lỗi nhập save và cách hiển thị: [SAVE_RELIABILITY.md](SAVE_RELIABILITY.md) mục 3 (`corrupt-json`, `bad-format`, `bad-checksum`, `newer-version`, `invalid-state`, `too-large`).
- Phiên `ReadOnly`: `session.Send` trả `null` nhưng không làm gì; UI phải đọc `session.Status` và khóa nút, không dựa vào giá trị trả về.
- 500 rồng: save ~225 KB, đọc + kiểm 62 ms trên máy dev. Nạp ở màn hình chờ.

## 6. Không làm trong Alpha

- Không bật bảng `sky_cloud`, không cài luật cho ăn B, không đổi Save Schema — đều chờ chủ dự án ([BALANCE_DECISIONS.md](BALANCE_DECISIONS.md)).
- Không gọi `ResearchLog.Observe` hay sửa `GameState` trực tiếp từ UI; dùng `observeDragon`.
- Không gọi `ProgressionQueries.Journey` / `QuestLog` / `Overview` mỗi frame (O(n²) trên số rồng).
