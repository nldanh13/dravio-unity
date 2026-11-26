# ADR 0207 — U2.12: Economy & Ecosystem Validation (không đổi luật, không đổi save)

- **Trạng thái:** Draft PR, chờ chủ dự án xem. Không merge `main`.
- **Phạm vi:** Nhánh `feat/u2-12-ecosystem-validation`, xếp chồng trên U2.11 (PR #57, nhánh `claude/project-thread-hcwo4k`, ADR 0206). Domain và Persistence C# thuần, không UnityEngine. Không chạm UI, Character, Environment, Unity Presentation.
- **Giữ nguyên:** mọi luật đã duyệt (E1, E2, E5, R11, U2.11). Save Schema vẫn **v10**, không trường mới, không migration. Thiên tai, Comfort drift, tiêu hao năng lượng, khóa Codex, tác động comfort của quan hệ vẫn TẮT. Tuyến `sky_cloud` vẫn khóa. Không bật luật thử nghiệm nào.

## Đã làm

| Hạng mục | Nội dung | Code |
|---|---|---|
| Kiểm toán tĩnh tiến trình (mới, chỉ đọc) | Điểm cố định "thế giới mở → loài sở hữu được → loài đặt được → yêu cầu nhiệm vụ → nhiệm vụ → `unlockZone`" trên catalog thật. Chứng minh: cả 3 thế giới mở được, 16/17 nhiệm vụ đạt được từ save mới, **không có vòng lặp điều kiện**. Nhiệm vụ duy nhất không đạt được là `Q_EXPEDITION_CLOUD` vì bảng thưởng `sky_cloud` chưa duyệt | `Progression/ProgressionAudit.cs`, `ProgressionQueries.AuditCatalog()` |
| Hướng dẫn hành trình (mới, chỉ đọc) | `ProgressionQueries.Journey(s)`: bước còn thiếu tới thế giới kế tiếp, đúng thứ tự (hướng dẫn → sinh cảnh Khởi Nguyên → chuỗi nhiệm vụ cổng, tiền đề trước → `unlockZone`), kèm ứng viên hành động (loài cần xem hồ sơ, rồng nên đặt vào sinh cảnh, tuyến thám hiểm cho vật liệu). Không bao giờ gợi ý vàng hay chờ đồng hồ | `Progression/JourneyGuide.cs` |
| Hành trình mô phỏng bằng lệnh thật | Hai kiểu người chơi mới trong bộ mô phỏng: **Guided** (theo hướng dẫn, nghiên cứu đầy đủ, lai cho nhiệm vụ Thân thiết) và **Casual** (1 gói/ngày, chỉ làm bước rẻ: xem hồ sơ, 1 bữa ăn + 2 lần quan sát mỗi phiên) | `Tests/Domain/Simulation/EconomySimulator.cs` |
| Mô phỏng kinh tế 7 / 30 / 90 / 365 ngày | 8 kiểu người chơi × 3 lịch chơi × 3 seed, luật thật v10 | `U212EcosystemValidationTests.Write_u212_ecosystem_report_when_requested`, kết quả `Docs/u2.12/ECONOMY_365.md` |
| Kiểm thử tự động | 14 test Domain (kiểm toán, hướng dẫn, hành trình, bất biến kinh tế) + 2 test Persistence (save v10 của hành trình ba thế giới) | `U212EcosystemValidationTests.cs`, `JourneyEndStateSaveTests.cs` |
| Tối ưu mô phỏng (không đổi kết quả) | `ArrangeProducers` thoát sớm khi chắc chắn không đổi chỗ được; đã so từng dòng kết quả có / không tối ưu | `EconomySimulator.cs` |

## Phát hiện (không sửa luật — cần chủ dự án quyết định)

| # | Phát hiện | Mức | Đề xuất |
|---|---|---|---|
| F1 | **Aurorawing không bao giờ sở hữu được.** `PackEligible = false`, nhóm V, nguyên tố Nước + Gió; nhóm V không loài nào có Nước nên không lai ra. Hệ quả: `ACH_CODEX_16` (2 000 G), `title.dragon_scholar`, `title.lifecycle_master`, `title.grand_ethologist` không bao giờ đạt. Luật có từ trước (parity TS), không phải lỗi U2.11 | Cao (thành tựu không đạt được) | Chọn một: (a) **trứng Aurorawing một lần ở mốc 15 chuyến `sky_cloud`** (khuyến nghị, gắn với Thiên Giới, không đổi tỉ lệ gói); (b) cho Aurorawing vào gói Astral; (c) cho lai chéo Frostfin (A) × loài nhóm V |
| F2 | Bảng thưởng `sky_cloud` vẫn treo, nên `Q_EXPEDITION_CLOUD` và mốc thám hiểm Thiên Giới không đạt được | Trung bình | Xem `Docs/u2.12/SKY_CLOUD_PROPOSAL.md` |
| F3 | Pha lê / san hô gần như không có chỗ tiêu sau cổng Thiên Giới (chỉ 4 + 4 cho cổng, 3 san hô cho Reef). Tồn kho tăng tuyến tính tới 365 ngày | Trung bình (hệ sinh thái vật liệu) | Sửa E5: dự án Thủy Vực / Thiên Giới dùng pha lê, san hô, bụi sao (đề xuất 4 của ADR 0206, có số cụ thể trong `SKY_CLOUD_PROPOSAL.md`) |
| F4 | Cho ăn có chủ đích khó: `AssignFood` giao phần ăn cho rồng thích loại đó, rồi UID nhỏ nhất. Trong sinh cảnh đông, một rồng cụ thể có thể không bao giờ được giao, nên không quan sát được "tìm ăn / ăn" → Ethology chậm. Mô phỏng Guided phải chọn loại thức ăn mà chỉ rồng mục tiêu ăn được | Trung bình (trải nghiệm nghiên cứu) | UI: khi người chơi chọn rồng rồi cho ăn, gợi ý loại thức ăn mà chỉ rồng đó (hoặc rồng thích loại đó) ăn được, và gửi `consumeFood` cho đúng rồng đó (luật hiện hành cho phép; "ăn" quan sát được ngay, "tìm ăn" chỉ khi rồng đó được giao phần ăn). Hoặc (đổi luật) `AssignFood` ưu tiên rồng người chơi đang chọn |
| F5 | Quan sát rồi cho ăn ngay: hồi quan sát 30 s chặn lần quan sát lúc ăn. Người chơi quan sát đầu phiên rồi cho ăn sẽ không bao giờ ghi được "ăn" | Thấp (UI) | UI dùng `ObservationOpportunities` để nhắc "quan sát lúc đang ăn", hoặc đợi hết hồi quan sát |
| F6 | "Xem lại hướng dẫn" khi chưa mở Thủy Vực xóa các bước đã xong → Thủy Vực tạm báo thiếu `tutorial-completed`. Không bế tắc: bước tự ghi lại (TUT_01 sau 15 s; TUT_02 cần rồng đã được cho ăn còn trong kho; TUT_05 cần không có trứng chờ). Có test | Thấp | UI cảnh báo trước khi "xem lại"; hoặc (đổi luật) điều kiện Thủy Vực tính "đã từng hoàn tất" |
| F7 | Nhiệm vụ đếm cả rồng đang đi thám hiểm là "Adult ở sinh cảnh" / "cư dân Good" (vị trí vẫn là Zone), trong khi quan hệ thì bỏ qua rồng đi xa | Thấp (nhất quán) | (Đổi luật) loại rồng đang thám hiểm khỏi `adultWithLocomotion`, `residentsThriving`, `residentsAtHome` |
| F8 | `QuestProposals.Evaluate` (`relationPairs`) và `QuestLog` là O(n²) theo số rồng; kho tối đa 500 rồng → ~125 000 cặp | Thấp (hiệu năng Unity) | Unity gọi `QuestLog` / `Journey` khi có event (không mỗi frame), lưu kết quả tới lệnh kế tiếp |
| F9 | Ảnh v4 cho TS không biểu diễn được chuyến thám hiểm đang chạy (giới hạn từ U2.8), nên save giữa hành trình thật không chiếu sang TS được | Thông tin | Giữ nguyên; test v4 dùng save không có chuyến đang chạy |
| F10 | Người chơi không xem hồ sơ / không quan sát thì không bao giờ tới Thủy Vực (U2.11 đã thấy). Casual có hướng dẫn tới được cả Thủy Vực và Thiên Giới | Thiết kế | Unity hiển thị `Journey(s).Steps[0]` trên HUD hành trình |

## Hệ quả

- API mới chỉ đọc: `ProgressionQueries.Journey`, `ProgressionQueries.AuditCatalog`, các kiểu `JourneyDto`, `JourneyStep`, `JourneyStepCode`, `ProgressionAuditReport`, `AuditStep`. Không lệnh mới, không event mới, không mã lỗi mới.
- Save: **không ảnh hưởng**. Schema v10 giữ nguyên; test mới chỉ xác nhận save của hành trình ba thế giới qua vòng lưu–nạp từng byte.
- Unity: `Dravio.Persistence.Tests.asmdef` tham chiếu thêm `Dravio.Domain.Tests` (để dùng bộ mô phỏng làm dữ liệu test). Chỉ ảnh hưởng assembly test trong Editor.
