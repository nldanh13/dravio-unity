# ADR 0205 — U2.10 chạy thật: giới hạn và giá gói Starter, dự án Sanctuary, nhật ký đêm (Save Schema v9)

- **Trạng thái:** Đã duyệt. Chủ dự án chọn "Bật cả gói E1, E2, E5, R11" cho câu hỏi "Đưa phần đề xuất kinh tế nào vào game thật?".
- **Phạm vi:** Nhánh `feat/u2-10-economy-live`, base `feat/u2-research-social-progression` (ADR 0204). Domain C# thuần, không UnityEngine.
- **Thay thế một phần ADR 0204:** E1, E2, E5 và R11 chuyển từ "đề xuất" sang "luật thật". R8 (khối `research` của Schema v9) và R9 (lệnh `observeDragon`) được làm cùng vì R11 cần một sổ nghiên cứu được lưu. Mọi phần khác của ADR 0204 vẫn ở trạng thái đề xuất.
- **Giữ nguyên:** Không bật thiên tai, Comfort drift hay tiêu hao năng lượng. Không quảng cáo. Codex **không** bị khóa theo cấp nghiên cứu (R1–R7 phần khóa vẫn chờ duyệt). Nhiệm vụ cổng vẫn không hoàn thành được (QuestRules.Production tắt). Không có phần thưởng vàng mới. Không merge `main`.

## Luật đã triển khai

| Mã | Luật thật | Code |
|---|---|---|
| E1 | Tối đa **5 gói Starter mua bằng vàng mỗi ngày UTC**. Gói hướng dẫn không tính. Lệnh bị từ chối hay lệnh trùng không tính. Gói thứ 6 trả lỗi `pack-daily-limit` (`commandError.pack-daily-limit`) | `PackCatalog.StarterDailyLimit`, `Economy/U210Rules.cs` `PackDailyLimit`, state `packDaily` |
| E2 | Giá Starter **100 → 150 G**. Gói hướng dẫn giữ 100 G (`ProgressCatalog.TutorialPackPriceG`), nên golden TS b33ec7a vẫn trùng từng byte | `PackCatalog`, `Progression.cs` |
| E5 | Lệnh `buildSanctuaryProject`. Dự án thứ n (từ 0) giá **floor(20 000 × 1,25ⁿ) G**, tính bằng số nguyên (giống nhau mọi nền tảng). Từ dự án thứ 3 (n ≥ 2) cần thêm **1 + n / 5 gỗ và cùng số hạt giống** (vật liệu thám hiểm Khởi Nguyên có chỗ tiêu). Tối đa 60 dự án. Chỉ thẩm mỹ: không cho vàng, không tăng sản xuất, không mở gameplay. Lỗi: `not-enough-gold`, `not-enough-materials`, `projects-maxed`. Event `sanctuaryProjectBuilt { index, costGoldMicro, materialEach }` | `Catalog/SanctuaryProjectCatalog.cs`, `SanctuaryProjects`, state `sanctuaryProjectsBuilt` |
| R11 | **Nhật ký đêm.** Khi quyết toán offline qua 02:00 UTC, engine đọc quyết định hành vi lúc đó; mỗi loài có rồng đang ngủ/nghỉ ở sinh cảnh được tối đa 1 phát hiện (tập tính nghỉ trước, rồi pha Night), tính trong giới hạn 2 phát hiện/loài/ngày. Không vàng, không XP, không bond. Event `nightJournalWritten { sampleAtMs, findings, speciesIds }` | `ResearchRules210.AfterOffline`, gọi trong `SettleTime` |
| R9 | Lệnh `observeDragon(uid, phase)`: BehaviorEngine quyết định rồng đang làm gì, rồi ghi phát hiện. Hồi 30 s mỗi rồng (`observation-cooldown`), rồng không ở sinh cảnh hay loài chưa có trong Codex trả `not-observable`. Không vàng, không XP, không bond. Event `dragonObserved` | `U210Commands.cs`, `ResearchRules210.Observe` |
| R8 | Khối `research { species[], individuals[] }` lưu trong save | `GameState.Research`, `GameStateJson` |

`ResearchPacing.Production` = ghi (2 phát hiện/loài/ngày, Ethology ≥ 2 ngày UTC, nhật ký đêm) nhưng `GateCodex = false`: Codex và hồ sơ cá thể vẫn hiển thị đầy đủ như trước. Vì thế save cũ không "mất" phần Codex nào. `RuleFlagsDto`: `ResearchRecordingEnabled = true`, `NightJournalEnabled = true`, `ResearchPacingEnabled` (khóa Codex) = false.

## Save Schema v9

- Thêm `packDaily { day: "YYYY-MM-DD" | null, starterBought }`, `sanctuaryProjectsBuilt` (số nguyên 0–60) và `research`.
- **Migration 8 → 9**: `packDaily = { day: null, starterBought: 0 }`, `sanctuaryProjectsBuilt = 0`, `research` rỗng. Không suy diễn quan sát từ Codex. Không đổi vàng, rồng, vật liệu, Codex, nhiệm vụ. Bỏ ba khóa mới thì state trùng từng byte với v8. Save v8 gốc được giữ nguyên văn ở `archive:schema-8` như mọi lần nâng schema.
- **Không bao giờ mất save vì sổ nghiên cứu:** khi nạp và khi ghi, khối `research` được làm sạch (`ResearchSchemaDraft.Sanitize`): bỏ loài lạ hay chưa có trong Codex, tập tính loài không làm được, sinh cảnh không bản địa, ngày sai, cá thể đã bán / thả. Khối hỏng hẳn (không phải object) thành sổ rỗng. Validator chỉ kiểm khối có tồn tại; kiểm chi tiết vẫn có ở `ResearchSchemaDraft.Validate` cho test.
- `packDaily` và `sanctuaryProjectsBuilt` sửa tay sai (số âm, có số mà không có ngày, quá 60) bị từ chối như mọi trường kinh tế khác. Validator **không** so `starterBought` với giới hạn hiện tại, để sau này đổi giới hạn thì save cũ vẫn nạp được.
- Ảnh v4 cho TS bỏ ba khóa này (như `codex`, `expedition`, sức khỏe). CI cho TS đọc thêm save `u210-economy-research`.

## Mô phỏng

Simulator dùng luật thật (không còn "quỹ ảo" cho E1, E2, E5; quỹ ảo chỉ còn cho E3, chưa duyệt). Người chơi thường xây dự án khi số dư ≥ 1,2 × giá và đủ vật liệu, và gửi thám hiểm Khởi Nguyên khi thiếu gỗ hoặc hạt giống. Kết quả: `Docs/u2.10/ECONOMY_SIMULATION.md` (mục "Sau khi bật luật thật").

## Còn chờ duyệt (không đổi so với ADR 0204)

Khóa Codex theo cấp nghiên cứu (R1–R7), E3 phí lai, E4 giao diện nâng cấp, bảng xã hội và tác động comfort (S1–S8), hoàn thành nhiệm vụ cổng và giao vật liệu (Q1–Q7), bảng thưởng `sky_cloud`, tên và hình ảnh từng dự án Sanctuary (hiện chỉ có số thứ tự; Sanctuary tự chọn mẫu trang trí hiển thị).

## Hệ quả

- UI cần: nút "Xây dự án" (đọc `ProgressionQueries.SanctuaryEconomy`), số gói Starter còn lại hôm nay, thông báo `pack-daily-limit`, nút quan sát (lệnh `observeDragon`) và thẻ "Nhật ký đêm" khi có event `nightJournalWritten`.
- Người chơi đổi gói Starter liên tục để bán (StarterTrader) bị giới hạn 5 gói/ngày với giá 150 G.
- Bản TS b33ec7a không đọc được save v9 (trả "newer-version", chỉ đọc, không ghi đè), như với v5–v8.
