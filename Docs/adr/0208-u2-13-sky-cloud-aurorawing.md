# ADR 0208 — U2.13: tuyến Thiên Giới `sky_cloud` (bảng chờ duyệt) và trứng Aurorawing (đã duyệt)

- **Trạng thái:** PR riêng, xếp chồng trên U2.12 (ADR 0207). Không merge `main` khi chưa duyệt. Domain và Persistence C# thuần; không chạm Unity Presentation, Character, Environment.
- **Quyết định đã duyệt áp dụng:** trao **một** trứng Aurorawing tại mốc **15 chuyến `sky_cloud` đã nhận**.
- **Chưa duyệt, giữ TẮT:** bảng thưởng `sky_cloud` (bụi sao, thời gian, số lượng, mốc). Game thật vẫn trả `route-rewards-undefined` cho tuyến này.
- **Save:** Schema **v10 giữ nguyên**, không migration. Bụi sao nằm trong `player.inventory`, bộ đếm trong `expeditionsClaimed.sky_cloud`, trứng trong `grantedRewards` / `rewardQueue` sẵn có.

## Quyết định

| # | Nội dung | Code |
|---|---|---|
| D1 | Bảng cân bằng là dữ liệu: `SkyCloudBalance` (thời gian, 2 số lượng, các mốc). `Production = Disabled`. Ba ứng viên A / B / C chỉ chạy trong phạm vi `SkyCloudBalance.Use(...)` theo luồng (giống luật thiên tai của dispatcher), để mô phỏng và test không đổi game thật | `Catalog/SkyCloudBalance.cs` |
| D2 | `ExpeditionCatalog.Routes` đọc tuyến `sky_cloud` từ bảng hiện hành. Tắt = không vật liệu (như U2.11). `MaterialIds` thêm `stardust` ở **cuối** (giữ thứ tự cũ); `Materials` có "Bụi sao", `Active` chỉ khi bảng bật | `Catalog/Catalogs.cs` |
| D3 | Mốc `sky_cloud` = mốc của bảng + trứng Aurorawing ở chuyến 15 (gộp vào mốc cùng số chuyến). Khi bảng tắt chỉ còn mốc trứng (0 vật liệu, không danh hiệu) và tuyến khóa nên không bao giờ đạt | `Progression/Exploration.cs` |
| D4 | Trứng dùng lại cơ chế trứng thế giới: ID `EXPEDITION_EGG_AURORAWING` vào `grantedRewards` đúng một lần, trứng vào `rewardQueue`, giao vào `pendingPack` khi slot trống và kho còn chỗ (`Rewards.TryDeliver`, chạy sau mọi lệnh thành công). Event `rewardEggGranted { delivered }` rồi `rewardEggDelivered` khi giao | như trên |
| D5 | Validator: `EXPEDITION_EGG_AURORAWING` trong `grantedRewards` chỉ hợp lệ khi `expeditionsClaimed.sky_cloud ≥ 15`; trùng ID bị từ chối. Save có trứng đã trao vẫn hợp lệ khi bảng bị tắt lại | `Core/GameStateValidator.cs` |
| D6 | Kiểm toán tiến trình coi trứng thám hiểm là nguồn loài khi tuyến của mốc dùng được (mở, có bảng, có loài đi được) | `Progression/ProgressionAudit.cs` |
| D7 | API: `RouteExplorationDto.NextMilestoneEggSpeciesId`, `Materials`, `DurationMs` | `Queries/ProgressionQueries.cs` |

Không đổi tỉ lệ gói trứng, luật lai, giá, điều kiện sức khỏe, tỉ lệ thành công, nguồn vàng hay XP.

## Trứng Aurorawing — stage và phenotype (cần quyết định bổ sung?)

Hiện làm giống trứng thế giới (Droplet, Zephyr) để không cần luật mới:

| Thuộc tính | Hiện tại (đã làm) | Phương án khác (chưa làm) |
|---|---|---|
| Đột biến | `Normal`, không tính pity | (b) cho Rare mutation cố định — làm trứng thưởng mạnh hơn trứng thế giới |
| Genotype | `Genetics.FounderGenotype(hashSeed("zone-egg", rewardId, saveId))` — tất định theo save | — |
| Stage khi nở | Baby (như mọi trứng) | (b) nở Juvenile để người chơi đã chơi lâu có Adult sớm hơn |
| Kho đầy | chờ trong `rewardQueue`, không mất | — |

Khuyến nghị giữ như hiện tại. Lưu ý: PR #65 (CHAR.8) đang làm mô hình Aurorawing 3 giai đoạn; trứng này dùng đúng `speciesId = "aurorawing"`.

## Hệ quả khi bảng được duyệt

- `Q_EXPEDITION_CLOUD` đạt được → 17/17 nhiệm vụ; 16/16 loài sở hữu được (kiểm toán và test).
- `ACH_CODEX_16`, `title.dragon_scholar`, `title.lifecycle_master`, `title.grand_ethologist` đạt được (còn cần lai Amberhorn và nghiên cứu đủ).
- Một slot thám hiểm chung → bụi sao thay chỗ chuyến rạn (xem `Docs/u2.13/SKY_CLOUD_BALANCE.md`).

## Kiểm thử

`U213SkyCloudTests` (10), `U213SkyCloudSimulationTests` (3 + báo cáo), `SkyCloudSaveTests` (3). Test U2.11 cũ cập nhật cho `stardust` và mốc trứng.
