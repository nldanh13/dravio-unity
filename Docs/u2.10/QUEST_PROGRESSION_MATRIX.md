# U2.10 — Quest Progression Matrix

> Quyết định và phần chờ duyệt: [ADR 0204](../adr/0204-u2-10-research-social-progression.md) mục C (Q1–Q8).
> **Trạng thái:** catalog + evaluator chỉ đọc (`Assets/Dravio/Domain/Progression/QuestProposals.cs`). Production TẮT (`QuestRules.Production`): không nhiệm vụ nào được hoàn thành, `completedQuests` không đổi, Thủy Vực và Thiên Giới vẫn khóa như ADR 0201.
> **Nguồn:** ADR 0201 để nội dung hai nhiệm vụ cổng là "TBD". GDD 3.1 không có trong repo; nội dung dưới đây là đề xuất.

## 1. Luật canonical không đổi (đã đọc WorldGraph, Expedition Engine, quest gates)

| Thế giới | Điều kiện ADR 0201 (phải đủ tất cả) | Cạnh WorldGraph | Tuyến thám hiểm |
|---|---|---|---|
| Khởi Nguyên | mở sẵn | trung tâm | `origin_forest` 15 phút, Adult sống đất, gỗ / hạt giống |
| Thủy Vực | hướng dẫn hoàn tất + đủ 3 sinh cảnh Khởi Nguyên + `QUEST_AQUATIC_EXPLORATION` | `origin_pondbank` ↔ `ocean_estuary`, cần bơi | `ocean_reef` 30 phút, biết bơi, pha lê / san hô |
| Thiên Giới | Thủy Vực đã mở + `QUEST_SKY_EXPLORATION` | `origin_meadow` ↔ `sky_low_island`, cần bay | `sky_cloud` 45 phút, **khóa** (bảng thưởng chưa duyệt) |

Đề xuất chỉ **điền nội dung** cho hai nhiệm vụ cổng. Hoàn thành nhiệm vụ (khi được duyệt) đi qua `WorldExpansion.ApplyQuestComplete`, rồi người chơi vẫn phải gửi `unlockZone` và lệnh này vẫn kiểm đủ điều kiện như cũ. Test `Aquatic_quest_can_be_satisfied_with_origin_content_only_but_never_unlocks_the_world_by_itself` kiểm điều này.

## 2. Ma trận nhiệm vụ

Cột "Cần thế giới" là nội dung nhiệm vụ được phép dùng. `ValidateCatalog` kiểm: nhiệm vụ cổng không cần thế giới mà nó mở; vật liệu có nguồn ở thế giới đã mở; có loài sống được ở đó với kiểu di chuyển cần thiết; tiền đề không vòng.

| ID | Nhóm | Cần thế giới | Tiền đề | Yêu cầu | Thưởng (không vàng) |
|---|---|---|---|---|---|
| `Q_ORIGIN_FIELD_NOTES` | Khởi Nguyên | origin | — | 2 loài đất liền đạt **Profiled** | Ghi chú Codex |
| `Q_ORIGIN_FOREST_SURVEY` | Thám hiểm | origin | — | 1 Adult biết đi ở sinh cảnh; giao 3 gỗ + 2 hạt giống | Mẫu trang trí |
| **`QUEST_AQUATIC_EXPLORATION`** | Thủy Vực (cổng) | origin | `Q_ORIGIN_FIELD_NOTES` | 3 loài đất liền đạt **LifeCycle**; 1 Adult **biết bơi** ở sinh cảnh (Droplet, Steamdrake, Mossback, Gaia sống được ở bờ ao); 2 loài có **chu kỳ ngày đêm**; giao **6 gỗ + 4 hạt giống** | Cổng thế giới |
| **`QUEST_SKY_EXPLORATION`** | Thiên Giới (cổng) | origin, ocean | `QUEST_AQUATIC_EXPLORATION` | 1 loài có sinh cảnh Thủy Vực đạt **Ethology**; 2 loài **được ghi chép ở microhabitat Thủy Vực**; 1 Adult **biết bay** ở sinh cảnh (Zephyr, Sparkwing, Blossom, Solaris… đậu ở đồng cỏ); giao **4 pha lê + 4 san hô** | Cổng thế giới |
| `Q_RESEARCH_NIGHT_WATCH` | Nghiên cứu | origin | `Q_ORIGIN_FIELD_NOTES` | 4 loài có chu kỳ ngày đêm | Danh hiệu |
| `Q_RESEARCH_PERSONALITIES` | Nghiên cứu | origin | `Q_ORIGIN_FIELD_NOTES` | 4 cá thể đã hé lộ cá tính và nơi ưa thích | Ghi chú Codex |
| `Q_RESEARCH_ETHOLOGIST` | Nghiên cứu | origin | `Q_RESEARCH_NIGHT_WATCH` | 3 loài đạt Ethology | Danh hiệu |
| `Q_CONSERVE_AT_HOME` | Bảo tồn | origin | — | 3 cư dân ở microhabitat ưa thích; 5 cư dân ở sinh cảnh Good (≥ 80) | Mẫu trang trí |
| `Q_CONSERVE_TIDES` | Bảo tồn | origin, ocean | `QUEST_AQUATIC_EXPLORATION` | 3 cư dân Good ở microhabitat Thủy Vực | Mẫu trang trí |
| `Q_EXPEDITION_REEF` | Thám hiểm | origin, ocean | `QUEST_AQUATIC_EXPLORATION` | 1 Adult biết bơi ở microhabitat Thủy Vực; có 3 san hô | Ghi chú Codex |
| `Q_EXPEDITION_CLOUD` | Thám hiểm | origin, ocean, sky | `QUEST_SKY_EXPLORATION` | tuyến `sky_cloud` khởi hành được (**Blocked**: bảng thưởng chưa duyệt); 1 Adult biết bay ở Thiên Giới | Ghi chú Codex |

## 3. Vì sao không phải "kiếm vàng" hay "chờ đồng hồ"

- Enum `QuestRequirementKind` không có loại vàng hay thời gian (test `No_quest_is_about_gold_or_waiting_for_a_timer`).
- Nhiệm vụ cổng kết hợp ba trục người chơi tự chọn nhịp: **nghiên cứu** (quan sát tập tính, ngày đêm), **sinh học** (nuôi đúng loài có khả năng bơi/bay tới Adult và đặt vào sinh cảnh hợp), **thám hiểm** (dùng vật liệu thật từ tuyến của thế giới trước).
- Thời gian trưởng thành và thời gian chuyến đi vẫn có, nhưng chỉ là hệ quả của các luật đã duyệt, không phải yêu cầu.

## 4. Sử dụng vật liệu thám hiểm

| Vật liệu | Nguồn (ADR 0201) | Dùng trong đề xuất |
|---|---|---|
| `wood` | `origin_forest` | `Q_ORIGIN_FOREST_SURVEY` 3, `QUEST_AQUATIC_EXPLORATION` 6 |
| `seed` | `origin_forest` | `Q_ORIGIN_FOREST_SURVEY` 2, `QUEST_AQUATIC_EXPLORATION` 4 |
| `crystal` | `ocean_reef` | `QUEST_SKY_EXPLORATION` 4 |
| `coral` | `ocean_reef` | `QUEST_SKY_EXPLORATION` 4, `Q_EXPEDITION_REEF` 3 |
| `stone` | chưa có nguồn | không dùng |
| "bụi sao" | chưa có ID | không dùng |

Đề xuất: nhiệm vụ "giao" vật liệu thì vật liệu bị trừ khi nhận hoàn thành (cần lệnh `completeQuest`, Q7). Trước khi có lệnh, evaluator chỉ kiểm số lượng đang có. Mỗi chuyến cho 2–4 đơn vị, nên `QUEST_AQUATIC_EXPLORATION` cần khoảng 4 chuyến rừng (≈ 1 giờ thám hiểm, chia ra nhiều phiên).

## 5. Mục tiêu mở sinh cảnh

Sinh cảnh Khởi Nguyên đã có luật canonical (ADR 0009): khảo sát `DISC_EMBERLING_THERMAL` mở đá ấm, `DISC_AQUATIC_HABITAT` mở bờ ao. Đề xuất **không thêm đường mở mới**; ma trận dùng chính điều kiện "đủ 3 sinh cảnh Khởi Nguyên" của ADR 0201 làm cổng. Sinh cảnh Thủy Vực và Thiên Giới mở cùng thế giới (ADR 0201).

## 6. Dòng thời gian kỳ vọng (mô phỏng Researcher, thử nghiệm)

Mô phỏng hoàn thành nhiệm vụ cổng khi đề xuất báo đủ, trừ vật liệu, rồi gửi `unlockZone` thật (xem ECONOMY_SIMULATION.md):

| Lịch chơi | Mở Thủy Vực (trung vị) | Mở Thiên Giới |
|---|---|---|
| 5 phút/ngày (chỉ buổi sáng) | xem báo cáo | xem báo cáo |
| 3 × 10 phút/ngày | xem báo cáo | xem báo cáo |
| 60 phút/ngày | xem báo cáo | xem báo cáo |

## 7. Trạng thái ở production

| Nhiệm vụ | Save mới | Ghi chú |
|---|---|---|
| Cổng Thủy Vực | InProgress, mọi yêu cầu nghiên cứu ghi **Unknown** (`research-disabled`) | nên không bao giờ Ready |
| Cổng Thiên Giới | Locked (`zone:ocean`) | |
| `Q_EXPEDITION_CLOUD` | Locked + Blocked | tuyến khóa |
| `CanComplete` | luôn false | `QuestRules.Production` |
