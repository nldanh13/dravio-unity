# ADR 0204 — U2.10: nghiên cứu, sinh thái xã hội, tiến trình thế giới và cân bằng kinh tế

- **Trạng thái:** Đề xuất. Đang chờ chủ dự án duyệt. Mọi luật có tác động gameplay đều **TẮT** ở production.
- **Cập nhật:** E1, E2, E5 và R11 (kèm R8 khối `research` của Schema v9 và R9 lệnh `observeDragon`) đã được duyệt và chạy thật, xem **ADR 0205**. Phần còn lại vẫn là đề xuất.
- **Phạm vi:** Bản Unity (`nldanh13/dravio-unity`), giai đoạn U2.10, nhánh `feat/u2-research-social-progression` (base `feat/u5-autonomous-ecosystem` @ `8352b6d`, đã chứa U2.6 / U2.9 / U5.3, Schema v8). Domain C# thuần, không UnityEngine.
- **Nguồn đã dùng:** ADR 0201 (mở thế giới, vật liệu, nhiệm vụ TBD), ADR 0202 (P1–P8, D7), ADR 0203 (A–D, đặc biệt D1–D5 và báo cáo "nghiên cứu quá nhanh"), `ECONOMY_AUDIT.md` (U2.7), catalog canonical (`SpeciesCatalog`, `G0BiologyData`, `HabitatCatalog`, `WorldGraph`, `ExpeditionCatalog`, `WorldUnlockCatalog`, `PackCatalog`, `EconomyTables`, `BreedingCatalog`), Behavioral Engine U2.9.
- **Giới hạn nguồn:** **GDD 3.1 không có trong repo Unity** (chỉ nằm ở repo TS `nldanh13/dravio`, phiên này không đọc được). Theo các tài liệu đã có trong repo, GDD không định lượng tập tính xã hội, nhịp nghiên cứu hay nội dung hai nhiệm vụ mở thế giới. **Vì vậy mọi con số trong ADR này là đề xuất**, cần đối chiếu với GDD khi duyệt.
- **Nguyên tắc giữ nguyên:** Save Schema vẫn v8 (không đổi `GameStateJson`, validator, migration production). Không lệnh, không event mới. Không đổi giá, phí, tỷ lệ, công thức. Không bật thiên tai, Comfort drift hay tiêu hao năng lượng. Không quảng cáo. Không sửa HUD, Character Runtime, Environment, Scene, Navigation. Không merge `main`.

## Phân loại phần việc

| Loại | Nội dung |
|---|---|
| **Đã triển khai (code chỉ đọc, chạy trong test và CI)** | `Behavior/ResearchProgress.cs` (ResearchLog, ResearchPacing, hồ sơ loài/cá thể, cơ hội quan sát, bản nháp khối v9), `Behavior/SocialEcology.cs` (bảng xã hội 16 loài, hồ sơ, độ quen, ứng viên tương tác), `Progression/QuestProposals.cs` (catalog + evaluator), `Queries/ProgressionQueries.cs` (DTO), test đơn vị và hồi quy |
| **Thử nghiệm (chỉ trong mô phỏng, không phải luật)** | `EconomySimulator` thêm người chơi Relaxed / Breeder / Researcher, biến thể cân bằng bằng "quỹ ảo" (`current` = catalog hiện tại có mua nâng cấp; `proposed` = E1–E3 + dự án Sanctuary E5 + nhật ký đêm R11), việc Researcher đặt rồng vào sinh cảnh thế giới mới, hoàn thành nhiệm vụ cổng theo đề xuất rồi gửi `unlockZone` thật |
| **Chỉ đề xuất (chưa có code production)** | Nối khối `research` vào Save Schema v9, lệnh `observeDragon` / `completeQuest` / giao vật liệu, lưu độ quen theo thời gian, tác động comfort của quan hệ xã hội, mọi thay đổi giá/phí/giới hạn mua |

## A. Nghiên cứu và khám phá (Workstream A)

| Mã | Quyết định đề xuất | Trạng thái code |
|---|---|---|
| R1 | Giữ 4 cấp U2.9: Sighted → Profiled → LifeCycle → Ethology. Ba cấp đầu dẫn xuất từ save v8 (Codex, `profilesViewed`). Ethology cần ghi chép | có, dùng lại `ResearchTier` |
| R2 | Mỗi lần quan sát ghi **4 loại phát hiện**: tập tính mới của loài, pha ngày đêm mới, microhabitat bản địa mới (sinh cảnh phù hợp), nét cá thể mới | có (`ResearchFindingKind`) |
| R3 | **Giới hạn 2 phát hiện cấp loài mỗi loài mỗi ngày UTC.** Phát hiện vượt giới hạn bị hoãn, không mất; quan sát lại ngày khác vẫn thấy | có (`ResearchPacing.ProposedU210`) |
| R4 | Ethology cần đủ tập tính, đủ chu kỳ ngày đêm (một pha hoạt động mạnh + một pha nghỉ hoặc thấy ngủ) **và ghi chép trải qua ≥ 2 ngày UTC** | có |
| R5 | Nghiên cứu cá thể, hé lộ dần: cá tính (lần đầu) → nhịp sinh học (đã thấy lúc hoạt động và lúc nghỉ) → nơi ưa thích (≥ 3 lần và đã thấy ở đó) → món ưa thích (≥ 3 lần và đã thấy tìm ăn/ăn) | có |
| R6 | Chống spam: hồi 30 s mỗi rồng (giữ ADR 0203 D3); API "cơ hội quan sát" chỉ đánh dấu `Worthwhile` khi có phát hiện thật, nên UI không cần thưởng cho việc chạm liên tục | có |
| R7 | **Phần thưởng không lạm phát:** không vàng, không XP, không bond, không tiền tệ mới. Thưởng là mở phần Codex (R1), ghi chú Codex, danh hiệu, tiến độ nhiệm vụ cổng (Q1) | thiết kế |
| R8 | Save Schema v9 (khi duyệt): thêm khối `research { species[], individuals[] }` như `ResearchLogSnapshot`. Migration v8 → v9 tạo khối **rỗng** (không suy diễn từ Codex). Validator: loài đã có trong Codex, tập tính loài làm được, microhabitat bản địa, ngày `YYYY-MM-DD`, tối đa 14 ngày bộ đếm, cá thể phải còn trong kho (cắt bớt khi nạp). Ảnh v4 cho TS bỏ khối này | **bản nháp** (`ResearchSchemaDraft`), chưa nối Persistence |
| R9 | Lệnh `observeDragon(uid)` (khi duyệt) dùng đúng `ResearchLog.Observe`; phiên lưu sau mỗi phát hiện. Trước khi duyệt, Platform có thể giữ `ResearchLog` trong bộ nhớ (mất khi thoát), chỉ cho thử nghiệm nội bộ | chỉ đề xuất |
| R11 | **Nhật ký đêm** (giải quyết quyết định số 3): khi người chơi mở game sau một đêm offline, engine đọc quyết định hành vi lúc 02:00 (giờ Sanctuary); mỗi loài có rồng đang ngủ/nghỉ ở sinh cảnh được tối đa **1 phát hiện** (tập tính nghỉ trước, rồi pha Night), tính trong giới hạn R3. Không tính là quan sát cá thể, không thêm ngày ghi chép, không đổi GameState | có (`ResearchLog.WriteNightJournal`, `ResearchPacing.ProposedU210NightJournal`), TẮT |
| R10 | Codex mở rộng: trang loài thêm "Nhật ký tập tính", "Chu kỳ ngày đêm", "Sinh cảnh đã ghi nhận"; trang cá thể thêm "Hiểu về bé" theo R5 | thiết kế (DTO có sẵn) |

Hệ quả đo được (test và mô phỏng): không giới hạn (U2.9) thì đủ tập tính trong **1 ngày**; với R3–R4, cần **6–8 ngày** cho một loài với người chơi chăm chỉ, và tổng 16 loài trải dài nhiều tháng. Người chơi chỉ mở game buổi sáng **không bao giờ** đạt Ethology cho loài hoạt động ban ngày (không thấy chúng ngủ). R11 giải quyết điểm này mà không thêm cơ chế "bấm": xem mục kết quả trong `ECONOMY_SIMULATION.md`.

## B. Sinh thái xã hội (Workstream B)

| Mã | Quyết định đề xuất | Trạng thái code |
|---|---|---|
| S1 | Tập tính xã hội theo loài: **Gregarious** (droplet, sprout, zephyr, frostfin, blossom), **Tolerant** (emberling, steamdrake, sparkwing, mossback, aurorawing, gaia), **Solitary** (magma, solaris, leviathan, amberhorn, celestia). Không mặc định mọi loài thích sống theo nhóm | có (`SocialCatalog`) |
| S2 | Khoảng cách cá nhân Adult 1,0–5,0 m theo loài; Baby ×0,6, Juvenile ×0,8; cá thể nhút nhát (dạn dĩ < 40) ×1,3 | có |
| S3 | Độ hòa đồng 0–100 = gốc loài (70/50/25) + (dạn dĩ − 50)/4 + (ham chơi − 50)/5 + tuổi (Baby +15, Juvenile +5). ≥ 55 thích có bạn, < 35 thích không gian riêng | có |
| S4 | Độ quen 0–100 **dẫn xuất từ save v8**: cha mẹ–con 60, anh chị em ruột 50, cùng cha hoặc mẹ 30, ông bà 30; cùng microhabitat +20, cùng loài +10 | có |
| S5 | 4 tương tác trình diễn: Chơi cùng, Nghỉ theo nhóm, Tò mò quan sát (giữ khoảng cách cá nhân của bên kia), Giữ khoảng cách khi đông | có |
| S6 | Ghép cặp chỉ giữa hai quyết định **trình diễn** cùng microhabitat; quyết định gameplay (tìm ăn, ăn, chăm sóc, trú, vitals thấp, cảnh báo) luôn thắng. Mỗi cá thể tối đa một tương tác mỗi nhịp 60 s. Loài Solitary không nghỉ theo nhóm; cá thể thích không gian riêng không chơi cùng | có |
| S7 | Độ quen theo thời gian sống chung (vd +1/giờ cùng chỗ, trần 40, không giảm offline) cần lưu cặp trong v9. Chưa làm | chỉ đề xuất |
| S8 | Tác động gameplay (TẮT): +5 vào mục tiêu comfort C1 khi cá thể hòa đồng có bạn quen cùng chỗ; −5 khi cá thể thích không gian riêng ở nơi có ≥ 3 rồng khác (sàn Cozy/Ecosystem của C1 vẫn giữ). Chỉ có nghĩa khi ADR 0203 C1 và ADR 0202 P7 được duyệt | có, `SocialRules.Production` = 0 |
| S9 | **Không** đánh nhau, tranh giành, tổn thương, tử vong hay bất kỳ tương tác nào khi offline. `AffectsGameplay` của mọi ứng viên luôn false | có, có test |

## C. Tiến trình thế giới (Workstream C)

| Mã | Quyết định đề xuất | Trạng thái code |
|---|---|---|
| Q1 | Nội dung `QUEST_AQUATIC_EXPLORATION` (chỉ dùng nội dung Khởi Nguyên): 3 loài đất liền đạt LifeCycle; 1 Adult biết bơi đang ở sinh cảnh; 2 loài có ghi chép đủ chu kỳ ngày đêm; giao 6 gỗ + 4 hạt giống. Tiền đề: `Q_ORIGIN_FIELD_NOTES` | catalog + evaluator |
| Q2 | Nội dung `QUEST_SKY_EXPLORATION` (Khởi Nguyên + Thủy Vực): 1 loài có sinh cảnh Thủy Vực đạt Ethology; 2 loài được ghi chép ở microhabitat Thủy Vực; 1 Adult biết bay ở sinh cảnh; giao 4 pha lê + 4 san hô | catalog + evaluator |
| Q3 | Không loại yêu cầu nào là "đủ vàng" hay "chờ đồng hồ" (test kiểm tên enum) | có |
| Q4 | Không đường tắt: evaluator **không bao giờ** ghi `completedQuests`. Mở thế giới vẫn chỉ qua `unlockZone` với đủ điều kiện ADR 0201 (hướng dẫn, 3 sinh cảnh, nhiệm vụ, thế giới trước). Catalog tự kiểm: nhiệm vụ cổng không cần thế giới nó mở, vật liệu có nguồn ở thế giới đã mở, loài có kiểu di chuyển cần thiết sống được ở đó, tiền đề không vòng | có (`ValidateCatalog`) |
| Q5 | 9 nhiệm vụ phụ (Khởi Nguyên, nghiên cứu, bảo tồn, thám hiểm). Thưởng: ghi chú Codex, mẫu trang trí thẩm mỹ, danh hiệu. Không vàng | catalog |
| Q6 | Tuyến `sky_cloud` vẫn khóa: yêu cầu `RouteAvailable` trả `Blocked: route-rewards-undefined` cho tới khi bảng thưởng "bụi sao" được duyệt | có |
| Q7 | Lệnh `completeQuest(questId)` (khi duyệt): kiểm `QuestProposals.Evaluate(...).CanComplete`, trừ vật liệu giao, gọi `WorldExpansion.ApplyQuestComplete` cho nhiệm vụ cổng. ID nhiệm vụ phụ cần validator v9 chấp nhận | chỉ đề xuất |
| Q8 | Yêu cầu dựa trên sổ nghiên cứu trả `Unknown` khi nghiên cứu tắt, nên nhiệm vụ cổng không bao giờ "Ready" ở production hiện tại | có |

## D. Cân bằng kinh tế (Workstream D)

Mô phỏng 20 seed × 6 kiểu người chơi × 3 lịch chơi × 90 ngày, trước/sau: xem `Docs/u2.10/ECONOMY_SIMULATION.md`.

| Mã | Đề xuất (KHÔNG áp dụng vào production) | Cơ sở |
|---|---|---|
| E1 | Giới hạn **5 gói Starter mỗi ngày UTC** (không tính gói hướng dẫn) | Starter EV/giá 1,41–1,84 > 1; giới hạn chặn vòng "mua–nuôi–bán" mà không ảnh hưởng người chơi thường (họ mua ≤ 1 gói/ngày) |
| E2 | Giá Starter 100 → 150 G | EV/giá 1,41 → 0,94 ở luck 0 (vẫn 1,23 ở luck 8, nên cần E1) |
| E3 | Phí lai Common × Common 80 → 120 G | EV con/phí 2,90 → 1,93; vẫn có lợi nhưng phí có vai trò tiêu hao |
| E4 | Ẩn hoặc ghi "chuẩn bị cho G2" ở `islandCapacityLevel` cho tới khi Thủy Vực mở | ở G1 không tăng sản xuất (5 chỗ vật lý) |
| E5 | **Dự án Sanctuary**: điểm tiêu tự nguyện, chỉ thẩm mỹ (trang trí lớn, mở rộng cảnh quan, theo mẫu trang trí từ nhiệm vụ Q5). Dự án thứ n giá **20 000 × 1,25ⁿ G**, không giới hạn số dự án, không tăng sản xuất (không tạo vòng lặp lợi nhuận). Sau này thêm vật liệu vào công thức | 90 ngày số dư 10–20 M G, không còn gì để mua; mô phỏng E5 trong `ECONOMY_SIMULATION.md` |
| E6 | Không thưởng vàng cho nghiên cứu, xã hội hay nhiệm vụ | giữ R7, Q5 |

## E. API (Workstream E)

`Dravio.Domain.Queries.ProgressionQueries`: `ResearchProfile`, `IndividualResearch`, `ObservationOpportunities`, `SocialInteractionCandidates`, `SocialProfile`, `SocialRelation`, `QuestRequirements`, `Overview`, `Rules`. Snapshot chỉ đọc, mặc định bộ luật Production. Hướng dẫn: `Docs/u2.10/API_INTEGRATION_GUIDE.md`.

## Danh sách quyết định cần chủ dự án duyệt

1. **Nhịp nghiên cứu R3–R4:** đồng ý 2 phát hiện/loài/ngày và Ethology ≥ 2 ngày? (Phương án khác: 3/ngày; hoặc không giới hạn nhưng Ethology ≥ 3 ngày.)
2. **Schema v9 (R8):** đồng ý thêm khối `research` và migration v8 → v9 tạo khối rỗng? Có cần cho phép bật nghiên cứu trước v9 (sổ trong bộ nhớ, mất khi thoát) không?
3. **Chu kỳ ngày đêm và lịch chơi:** người chơi chỉ chơi một khung giờ không thấy được pha ngủ của loài ban ngày. **Khuyến nghị: (b) nhật ký đêm R11**, đã có code và mô phỏng. Phương án khác: (a) cho quan sát theo `settings.timeMode` (Day/Sunset/Night) của Sanctuary; (c) giữ như hiện tại.
4. **Bảng xã hội S1–S3:** duyệt phân loại Gregarious / Tolerant / Solitary của 16 loài và khoảng cách cá nhân, hay coi là tinh chỉnh trình diễn do Sanctuary tự chọn?
5. **Tác động gameplay xã hội S8:** có muốn quan hệ ảnh hưởng comfort không? Nếu có, duyệt cùng ADR 0203 C1 và ADR 0202 P7.
6. **Độ quen theo thời gian S7:** có cần lưu (Schema v9) không, hay chỉ dùng độ quen dẫn xuất từ phả hệ?
7. **Nội dung hai nhiệm vụ cổng Q1–Q2:** duyệt yêu cầu và số lượng vật liệu (mô phỏng: Thủy Vực mở khoảng ngày 5–11, Thiên Giới khoảng ngày 13–29, 90–100 % seed trong 90 ngày, nên không cần hạ yêu cầu); đồng ý nhiệm vụ cổng phụ thuộc vào nghiên cứu (nghĩa là phải duyệt R1–R8 trước, hoặc thay yêu cầu nghiên cứu bằng LifeCycle thuần)?
8. **Nhiệm vụ phụ và phần thưởng Q5:** duyệt danh sách, thưởng chỉ thẩm mỹ / Codex / danh hiệu?
9. **Lệnh mới R9 / Q7** (`observeDragon`, `completeQuest`) và việc trừ vật liệu khi giao.
10. **Kinh tế E1–E5:** **khuyến nghị E1 + E2 + E5** (E3 hầu như không đổi gì, E4 chỉ là giao diện). Sau khi chọn, chạy lại mô phỏng 1 000 seed rồi mới đổi catalog.
11. **Bảng thưởng `sky_cloud`** (ID "bụi sao") để mở Q6 — vẫn treo từ ADR 0201.

## Hệ quả

- Unity có ngay API chỉ đọc. Khi bộ luật đang tắt, trải nghiệm không đổi: Codex mở đủ như hiện tại, hồ sơ cá thể hiển thị đầy đủ, nhiệm vụ cổng vẫn khóa, không ai nhận gì thêm.
- Khi duyệt: đổi `Production` của bộ luật tương ứng, thêm lệnh (R9, Q7) và Schema v9 (R8) như U2.6 đã làm với v8, kèm test giá trị đã duyệt. Engine và DTO không phải đổi cấu trúc.
- Đổi bảng xã hội hay hàm hash ghép cặp sẽ đổi hình ảnh người chơi thấy; test tất định buộc thay đổi đó phải có chủ đích.
