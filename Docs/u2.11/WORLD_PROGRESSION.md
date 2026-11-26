# U2.11 — Tiến trình thế giới: nhiệm vụ, mở thế giới, nghiên cứu, thám hiểm, quan hệ

> Quyết định: [ADR 0206](../adr/0206-u2-11-world-progression.md). Nền: [ADR 0204](../adr/0204-u2-10-research-social-progression.md), [ADR 0205](../adr/0205-u2-10-live-economy-night-journal.md).
> **Trạng thái:** luật thật trên nhánh U2.11 (Draft PR). Thiên tai, Comfort drift, tiêu hao năng lượng, khóa Codex, tác động comfort của quan hệ vẫn TẮT.

## 1. Đường mở thế giới

```
Khởi Nguyên (mở sẵn)
  ├─ hoàn tất hướng dẫn ─┐
  ├─ mở đủ 3 sinh cảnh ──┤ (đá ấm, bờ ao: khảo sát ADR 0009)
  └─ Q_ORIGIN_FIELD_NOTES ─→ QUEST_AQUATIC_EXPLORATION ─┴─→ unlockZone("ocean") → trứng Droplet
                                                              │
       Thủy Vực đã mở ─→ QUEST_SKY_EXPLORATION ───────────────→ unlockZone("sky") → trứng Zephyr
```

- Hoàn thành nhiệm vụ = lệnh `completeQuest(questId)`. **Không** tự mở thế giới.
- Mở thế giới = lệnh `unlockZone(zoneId)`, kiểm đủ điều kiện ADR 0201 như trước. `ProgressionQueries.QuestLog(s).ZoneUnlockMissing` trả mã còn thiếu (`tutorial-completed`, `origin-habitats`, `zone:<id>`, `quest:<id>`) để UI hiển thị.

## 2. Ma trận nhiệm vụ (17)

Cột "Hiện khi" là thế giới phải mở. Tiền đề phải được **nhận** hoàn thành.

| ID | Nhóm | Hiện khi | Tiền đề | Yêu cầu | Thưởng |
|---|---|---|---|---|---|
| `Q_ORIGIN_FIELD_NOTES` | Khởi Nguyên | origin | — | 2 loài đất liền Profiled | `note.origin_field_notes` |
| `Q_ORIGIN_FOREST_SURVEY` | Thám hiểm | origin | — | 1 Adult biết đi ở sinh cảnh; giao 3 gỗ + 2 hạt | `blueprint.forest_lantern` |
| **`QUEST_AQUATIC_EXPLORATION`** | Cổng Thủy Vực | origin | Field Notes | 3 loài đất liền LifeCycle; 1 Adult biết bơi ở sinh cảnh; 2 loài đủ chu kỳ ngày đêm; giao 6 gỗ + 4 hạt | `zone.ocean` |
| **`QUEST_SKY_EXPLORATION`** | Cổng Thiên Giới | origin, ocean | cổng Thủy Vực | 1 loài có sinh cảnh Thủy Vực đạt Ethology; 2 loài ghi chép ở Thủy Vực; 1 Adult biết bay ở sinh cảnh; giao 4 pha lê + 4 san hô | `zone.sky` |
| `Q_RESEARCH_NIGHT_WATCH` | Nghiên cứu | origin | Field Notes | 4 loài đủ chu kỳ ngày đêm | `title.night_watcher` |
| `Q_RESEARCH_PERSONALITIES` | Nghiên cứu | origin | Field Notes | 4 cá thể đã hé lộ cá tính + nơi ưa thích | `note.personalities` |
| `Q_RESEARCH_ETHOLOGIST` | Nghiên cứu | origin | Night Watch | 3 loài Ethology | `title.ethologist` |
| `Q_RESEARCH_TIDE_JOURNAL` *(mới)* | Nghiên cứu | origin, ocean | cổng Thủy Vực | 3 loài ghi chép ở microhabitat Thủy Vực | `note.tide_journal` |
| `Q_RESEARCH_SKY_CHRONICLE` *(mới)* | Nghiên cứu | 3 thế giới | cổng Thiên Giới | 2 loài ghi chép ở Thiên Giới | `title.sky_chronicler` |
| `Q_CONSERVE_AT_HOME` | Bảo tồn | origin | — | 3 cư dân ở nơi ưa thích; 5 cư dân Good | `blueprint.meadow_arbor` |
| `Q_CONSERVE_TIDES` | Bảo tồn | origin, ocean | cổng Thủy Vực | 3 cư dân Good ở Thủy Vực | `blueprint.tide_pool` |
| `Q_CONSERVE_SKIES` *(mới)* | Bảo tồn | 3 thế giới | cổng Thiên Giới | 3 cư dân Good ở Thiên Giới | `blueprint.cloud_garden` |
| `Q_SOCIAL_FIRST_FRIENDS` *(mới)* | Quan hệ | origin | — | 2 cặp rồng bậc Bạn (≥ 50) | `title.friend_of_dragons` |
| `Q_SOCIAL_CLOSE_BOND` *(mới)* | Quan hệ | origin | First Friends | 1 cặp bậc Thân (≥ 80) | `note.close_bonds` |
| `Q_EXPEDITION_PATHFINDER` *(mới)* | Thám hiểm | origin | Forest Survey | 5 chuyến rừng đã nhận | `blueprint.trail_marker` |
| `Q_EXPEDITION_REEF` | Thám hiểm | origin, ocean | cổng Thủy Vực | 1 Adult biết bơi ở Thủy Vực; giao 3 san hô | `note.reef_survey` |
| `Q_EXPEDITION_CLOUD` | Thám hiểm | 3 thế giới | cổng Thiên Giới | tuyến `sky_cloud` khởi hành được (**Blocked**); 1 Adult biết bay ở Thiên Giới | `note.cloud_terrace` |

`Q_SOCIAL_CLOSE_BOND` cần phả hệ: cha mẹ–con (60) + cùng chỗ (20) đã đủ 80; hai rồng không họ hàng tối đa 20 + 10 + 40 = 70 (bậc Bạn). Đây là chủ ý: "Thân" là quan hệ gia đình.

`Q_EXPEDITION_CLOUD` không bao giờ Ready cho tới khi bảng thưởng `sky_cloud` được duyệt (quyết định 1 của ADR 0206).

## 3. Tiến trình nghiên cứu sinh học

| Cấp | Điều kiện (không đổi từ U2.10) |
|---|---|
| Sighted | có trong Codex |
| Profiled | đã xem hồ sơ sinh học |
| LifeCycle | Profiled + đã thấy Adult |
| Ethology | đủ tập tính quan sát được (kể cả tìm ăn, ăn), đủ chu kỳ ngày đêm, ghi chép trải ≥ 2 ngày UTC, tối đa 2 phát hiện/loài/ngày |

`ProgressionQueries.ResearchProgression(s)` trả thêm tiến độ theo thế giới và danh hiệu:

| Danh hiệu | Điều kiện |
|---|---|
| `title.first_steps` / `title.naturalist` / `title.dragon_scholar` | 4 / 8 / 16 loài trong Codex |
| `title.world_walker` | mở đủ 3 thế giới |
| `title.lifecycle_student` / `_scholar` / `_master` | 3 / 8 / 16 loài LifeCycle |
| `title.field_ethologist` / `title.senior_ethologist` / `title.grand_ethologist` | 1 / 5 / 16 loài Ethology |

## 4. Thưởng thám hiểm

Xem bảng mốc ở ADR 0206 mục C. `ProgressionQueries.ExplorationRoutes(s)` trả mỗi tuyến: mở chưa, có bảng thưởng chưa, số chuyến đã nhận, mốc kế tiếp và phần thưởng.

## 5. Quan hệ giữa các rồng

| Nguồn | Điểm |
|---|---|
| Cha mẹ – con | 60 |
| Anh chị em ruột | 50 |
| Cùng cha hoặc mẹ, ông bà – cháu | 30 |
| Đang cùng microhabitat | +20 |
| Cùng loài | +10 |
| **Thời gian sống chung (mới, lưu trong save)** | +1 / giờ, trần +40 |

Bậc: Lạ 0–19, Quen 20–49, Bạn 50–79, Thân 80–100. `ProgressionQueries.Relations(s, uid)` trả mọi quan hệ của một cá thể, độ quen giảm dần. Không đánh nhau, không tổn thương, không ảnh hưởng gameplay.

## 6. API cho Unity (mới ở U2.11)

| Hàm | Dùng cho |
|---|---|
| `new CompleteQuestCommand(requestId, questId)` | nút "Nhận" trên thẻ nhiệm vụ Ready |
| `ProgressionQueries.QuestLog(s)` | màn hành trình: nhiệm vụ, nhiệm vụ nhận được ngay, thưởng đã có, điều kiện mở thế giới còn thiếu |
| `ProgressionQueries.Rewards(s)` | Codex (ghi chú), kho mẫu trang trí, danh hiệu |
| `ProgressionQueries.ResearchProgression(s)` | màn nghiên cứu theo thế giới |
| `ProgressionQueries.ExplorationRoutes(s)` | màn thám hiểm (tiến độ mốc) |
| `ProgressionQueries.Relations(s, uid)` | thẻ cá thể: "Bạn bè" |
| `ProgressionQueries.SanctuaryProjects(s)` | dựng công trình đã xây và bản xem trước dự án kế tiếp |
| Event `questRewarded`, `explorationMilestoneReached` | thông báo nhận thưởng |
| Mã lỗi `unknown-quest`, `quest-locked`, `quest-not-ready`, `quest-completed` | `commandError.<mã>` |
