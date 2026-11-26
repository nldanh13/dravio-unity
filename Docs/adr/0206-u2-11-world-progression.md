# ADR 0206 — U2.11: nhiệm vụ thật, mở Thủy Vực / Thiên Giới, quan hệ rồng, thưởng thám hiểm, nội dung dự án Sanctuary (Save Schema v10)

- **Trạng thái:** Triển khai theo yêu cầu U2.11 của chủ dự án ("Hoàn thiện hệ thống nhiệm vụ", "Điều kiện mở khóa Thủy Vực và Thiên Giới", "Tiến trình nghiên cứu sinh học", "Phần thưởng khám phá và thám hiểm", "Hệ thống quan hệ giữa các rồng", "Đánh giá kinh tế sau khi áp dụng Schema v9", "Thiết kế nội dung dự án trang trí Sanctuary"). Draft PR, chờ chủ dự án xem.
- **Phạm vi:** Nhánh `claude/project-thread-hcwo4k`, base `feat/u2-10-economy-live` (PR #49, ADR 0205). Domain và Persistence C# thuần, không UnityEngine.
- **Thay thế một phần ADR 0204:** Q1, Q2, Q5, Q7 (nội dung và lệnh hoàn thành nhiệm vụ) và S7 (độ quen theo thời gian) chuyển từ "đề xuất" sang "luật thật". Thêm luật mới: mốc thưởng thám hiểm, danh hiệu dẫn xuất, nội dung dự án Sanctuary.
- **Giữ nguyên:** Không thiên tai, không Comfort drift, không tiêu hao năng lượng, không quảng cáo. Codex **không** khóa theo cấp nghiên cứu (R1–R7 phần khóa vẫn chờ duyệt). Quan hệ **không** ảnh hưởng comfort (S8 vẫn tắt, `SocialRules.Production` = 0). Không thưởng vàng mới. Giá, vật liệu, giới hạn của E1, E2, E5 không đổi. Tuyến `sky_cloud` vẫn khóa. Không merge `main`.

## Phân loại

| Loại | Nội dung |
|---|---|
| **Đã triển khai (luật thật)** | Lệnh `completeQuest`; 17 nhiệm vụ (11 của U2.10 + 6 mới); tiền đề phải được nhận hoàn thành; mốc thưởng thám hiểm theo tuyến; độ quen theo thời gian sống chung (chỉ trình diễn); Save Schema v10 |
| **Đã triển khai (chỉ đọc, trình diễn)** | Danh hiệu dẫn xuất (khám phá, nghiên cứu, nhiệm vụ, thám hiểm); tiến trình nghiên cứu theo thế giới; nội dung 20 dự án Sanctuary + "Mở rộng cảnh quan"; biến thể kiểu dáng dự án theo mẫu trang trí; API `ProgressionQueries` mới |
| **Thử nghiệm (chỉ trong mô phỏng)** | E3 phí lai qua "quỹ ảo" (không đổi so với U2.10) |
| **Chỉ đề xuất (chưa đổi)** | Bảng thưởng `sky_cloud` ("bụi sao"); khóa Codex R1–R7; E3, E4; S8 (quan hệ ảnh hưởng comfort); thất bại thám hiểm |

## A. Nhiệm vụ (Q1, Q2, Q5, Q7)

| Mã | Quyết định | Code |
|---|---|---|
| Q7 | Lệnh `completeQuest(questId)`. Dispatcher quyết toán thời gian online trước (rồng có thể vừa thành Adult), rồi kiểm `QuestProposals.Evaluate(...).CanComplete`. Thứ tự lỗi: `unknown-quest` → `quest-completed` → `quest-locked` (chi tiết `quest:<id>` / `zone:<id>`) → `quest-not-ready` (chi tiết các yêu cầu chưa đạt). Lệnh bị từ chối không đổi state | `Core/U211Commands.cs`, `Progression/QuestSystem.cs` |
| Q7 | Vật liệu yêu cầu "giao" bị trừ đúng một lần, cùng transaction. Nhiệm vụ cổng ghi qua `WorldExpansion.ApplyQuestComplete` (event `questCompleted` như cũ); nhiệm vụ phụ ghi vào `completedSideQuests` (thứ tự catalog). Event mới `questRewarded { questId, reward, rewardId, delivered }` | như trên |
| Q4 | **Không đường tắt:** hoàn thành nhiệm vụ cổng KHÔNG mở thế giới. Người chơi vẫn gửi `unlockZone`, lệnh này vẫn kiểm đủ ADR 0201: hướng dẫn hoàn tất, 3 sinh cảnh Khởi Nguyên, nhiệm vụ cổng, thế giới trước | `World/Travel.cs` không đổi |
| Q1 | `QUEST_AQUATIC_EXPLORATION` (giữ nguyên số của U2.10): tiền đề `Q_ORIGIN_FIELD_NOTES`; 3 loài đất liền đạt LifeCycle; 1 Adult biết bơi ở sinh cảnh; 2 loài đủ chu kỳ ngày đêm; giao 6 gỗ + 4 hạt giống | `QuestProposals.All` |
| Q2 | `QUEST_SKY_EXPLORATION`: tiền đề cổng Thủy Vực; 1 loài có sinh cảnh Thủy Vực đạt Ethology; 2 loài được ghi chép ở microhabitat Thủy Vực; 1 Adult biết bay ở sinh cảnh; giao 4 pha lê + 4 san hô | như trên |
| Q5 | Thưởng chỉ là ghi chú Codex, mẫu trang trí, danh hiệu, với ID ổn định (`note.*`, `blueprint.*`, `title.*`; cổng = `zone.*`). Thưởng **dẫn xuất** từ nhiệm vụ đã nhận, không lưu riêng, nên không nhận hai lần được | `QuestSystem.Rewards` |
| Q8′ | Tiền đề phải được **nhận** hoàn thành (U2.10 tính cả "Ready"). Chuỗi Khởi Nguyên: `Q_ORIGIN_FIELD_NOTES` → cổng Thủy Vực → cổng Thiên Giới | `Evaluate` |
| Q9 (mới) | 6 nhiệm vụ mới: `Q_SOCIAL_FIRST_FRIENDS` (2 cặp bậc Bạn), `Q_SOCIAL_CLOSE_BOND` (1 cặp bậc Thân), `Q_EXPEDITION_PATHFINDER` (5 chuyến rừng), `Q_RESEARCH_TIDE_JOURNAL` (3 loài được ghi chép ở Thủy Vực), `Q_RESEARCH_SKY_CHRONICLE` (2 loài ở Thiên Giới), `Q_CONSERVE_SKIES` (3 cư dân Good ở Thiên Giới). Hai loại yêu cầu mới: `relationPairs`, `expeditionsClaimed`. Vẫn không có loại "vàng" hay "chờ đồng hồ" | catalog + test |

Ma trận đầy đủ: `Docs/u2.11/WORLD_PROGRESSION.md`.

## B. Tiến trình nghiên cứu sinh học

Không đổi nhịp R3–R4 (2 phát hiện/loài/ngày, Ethology ≥ 2 ngày UTC) và nhật ký đêm R11. Thêm phần đọc:

- `ProgressionQueries.ResearchProgression`: số loài ở từng cấp, tiến độ theo thế giới (Khởi Nguyên / Thủy Vực / Thiên Giới: số loài sống được, đã gặp, LifeCycle, Ethology, % = trung bình cấp / 4).
- Danh hiệu nghiên cứu dẫn xuất: 3 / 8 / 16 loài LifeCycle, 1 / 5 / 16 loài Ethology. Danh hiệu khám phá: 4 / 8 / 16 loài trong Codex, mở đủ 3 thế giới.
- Nhiệm vụ nghiên cứu dẫn người chơi theo hành trình: Khởi Nguyên (Field Notes, Night Watch, Personalities, Ethologist) → Thủy Vực (Tide Journal) → Thiên Giới (Sky Chronicle).

## C. Phần thưởng khám phá và thám hiểm (luật thật)

| Tuyến | Chuyến nhận thứ | Thưởng thêm (mỗi vật liệu của tuyến) | Kèm |
|---|---|---|---|
| `origin_forest` | 1 / 5 / 15 / 40 | 2 / 3 / 4 / 6 gỗ và cùng số hạt giống | `note.forest_first_trail` / — / `title.forest_ranger` / `title.forest_warden` |
| `ocean_reef` | 1 / 5 / 15 / 40 | 2 / 3 / 4 / 6 pha lê và cùng số san hô | `note.reef_first_dive` / — / `title.reef_diver` / `title.tide_keeper` |
| `sky_cloud` | — | tuyến khóa (bảng thưởng chưa duyệt) | — |

Bộ đếm `expeditionsClaimed` tăng trong lệnh `claimExpedition`, sau khi cộng vật liệu thường. Event `explorationMilestoneReached { routeId, claims, bonus, rewardId }`. Không vàng, không XP. Tổng thưởng mốc qua 40 chuyến là 15 đơn vị mỗi vật liệu, tức 30 đơn vị (≈ 25 % so với ≈ 120 đơn vị nhận thường trong 40 chuyến, vì mỗi chuyến cho tổng 2–4). Phần lớn thưởng rơi vào 15 chuyến đầu, đúng lúc người chơi cần gỗ / hạt cho nhiệm vụ cổng và dự án thứ 3 trở đi. Save cũ không được thưởng bù cho chuyến đã nhận trước v10.

## D. Quan hệ giữa các rồng (S7 chạy thật, S8 vẫn tắt)

- Độ quen = phả hệ (S4) + cùng microhabitat 20 + cùng loài 10 + **thời gian sống chung**: 1 điểm mỗi giờ hai cá thể cùng ở một microhabitat, trần 40 điểm (40 giờ), không giảm, tính cả offline. Kẹp 0–100.
- Không tính rồng đi thám hiểm, ở kho, trạm chăm sóc, nơi trú hay đã thả. Cặp có cá thể bán / thả bị bỏ khỏi save. Tối đa 600 cặp.
- Bậc hiển thị: Lạ (0–19), Quen (20–49), Bạn (50–79), Thân (80–100). Ứng viên tương tác trình diễn (S5–S6) dùng độ quen mới, nên rồng sống chung lâu hay chơi / nghỉ cùng nhau hơn.
- **Chỉ trình diễn:** không đổi vàng, XP, bond, vitals hay comfort. Có test so vàng / XP khi có và không có khối quan hệ.

## E. Nội dung dự án trang trí Sanctuary

20 dự án đặt tên theo hành trình (8 Khởi Nguyên, 6 Thủy Vực, 6 Thiên Giới), sau đó "Mở rộng cảnh quan" cấp 1–40 xoay vòng ba thế giới. Mỗi dự án có kiểu công trình (`landmark`, `path`, `garden`, `light`, `water`, `vista`) để Sanctuary chọn prefab và điểm neo. Dự án của thế giới chưa mở hiển thị ở Khởi Nguyên, không bao giờ biến mất. Năm mẫu trang trí từ nhiệm vụ đổi kiểu dáng năm dự án. **Không đổi giá, vật liệu, giới hạn hay hiệu ứng E5.** Save chỉ có số dự án, không cần trường mới. Chi tiết: `Docs/u2.11/SANCTUARY_PROJECTS.md`.

## Save Schema v10

- Thêm `completedSideQuests` (mảng ID, thứ tự catalog), `expeditionsClaimed` (object tuyến → số chuyến), `relations { pairs: [{ a, b, sharedMs }] }`.
- **Migration 9 → 10:** ba khối rỗng. Không suy diễn từ dữ liệu cũ. Không đổi vàng, rồng, vật liệu, Codex, nhiệm vụ cổng, sổ nghiên cứu. Bỏ ba khóa mới thì state trùng từng byte với v9. Save v9 gốc giữ nguyên văn ở `archive:schema-9`.
- **Không bao giờ mất save vì quan hệ:** khối `relations` được làm sạch khi nạp và khi ghi (bỏ cặp trùng, tự-cặp, cá thể không còn, thời gian ≤ 0; kẹp về 40 giờ). Khối hỏng hẳn thành rỗng.
- `completedSideQuests` có ID lạ, ID cổng, hoặc trùng; `expeditionsClaimed` âm hoặc tuyến lạ: bị từ chối như mọi trường tiến trình khác.
- Ảnh v4 cho TS bỏ ba khóa mới. CI cho TS đọc thêm save `u211-world-progression`.

## Đánh giá kinh tế

Xem `Docs/u2.11/ECONOMY_REVIEW.md` (20 seed × 6 kiểu người chơi × 3 lịch × 90 ngày trên luật thật v9 + U2.11).

## Quyết định cần chủ dự án duyệt

1. **Bảng thưởng `sky_cloud`** (vẫn treo từ ADR 0201): đề xuất vật liệu mới `stardust` ("bụi sao"), 2–4 đơn vị mỗi chuyến, dùng cho dự án Thiên Giới. Cần thêm ID vật liệu nên chưa làm.
2. Giữ hay chỉnh số trong mốc thám hiểm (2 / 3 / 4 / 6 tại 1 / 5 / 15 / 40 chuyến).
3. Tên tiếng Việt của 20 dự án và 6 nhiệm vụ mới (đang dùng bản nháp, i18n key có sẵn).
