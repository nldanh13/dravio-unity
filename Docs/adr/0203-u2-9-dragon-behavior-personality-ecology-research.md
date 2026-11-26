# ADR 0203 — U2.9: sinh học, cá tính, hành vi, sinh thái sinh cảnh và nghiên cứu

- **Trạng thái:** Đề xuất. Đang chờ chủ dự án duyệt.
- **Phạm vi:** Bản Unity (`nldanh13/dravio-unity`), giai đoạn U2.9, nhánh `feat/u2-dragon-behavior` (base `feat/u5-living-sanctuary`, đã chứa U2.6 / Schema v8). Domain C# thuần, không UnityEngine.
- **Nguồn đã dùng:** catalog sinh học G0 (`activity`, `locomotion`, `respiration`, `thermalBands`, `dietIds`, `habitatRules`, `comfortMin`); Suitability (GDD 3.1 tr. 5); `DragonState.Personality` (roll lúc nở, đã có trong save); ranh giới ngày đêm của dữ liệu Environment (`*Living.json`); ADR 0202 (P7, P8, D1); ADR 0201 (vật liệu, nhiệm vụ mở thế giới); Bách khoa (GDD tr. 22).
- **Giới hạn nguồn:** GDD 3.1 nằm ở repo TypeScript `nldanh13/dravio`, phiên làm việc này không đọc được repo đó. Mọi trích dẫn GDD ở đây lấy từ các tài liệu đã có trong repo Unity (README Domain, ADR 0201, ADR 0202, INTEGRATION, ECONOMY_AUDIT). Theo các tài liệu đó, GDD **không** định lượng cá tính, hành vi, công thức môi trường → comfort hay tiến trình nghiên cứu. Vì vậy toàn bộ thông số dưới đây là đề xuất.
- **Nguyên tắc giữ nguyên:** Không đổi Save Schema (vẫn v8). Không đổi vàng, XP, bond, giá bán, tỷ lệ lai hay bất kỳ lệnh nào. Không bật thiên tai tự nhiên. Không phạt khi offline, không tử vong. Không quảng cáo. Không sửa Character, Environment, HUD, Animator hay Scene.

## Bối cảnh

Spec U2.9 muốn mỗi rồng là một cá thể có tính cách, nhu cầu và hành vi hợp sinh học, không chỉ là đơn vị sinh vàng. Domain đã có đủ dữ liệu sinh học (G0), độ phù hợp sinh cảnh (Suitability), chỉ số sức khỏe (Vitals, U2.6) và trường `Personality`, nhưng chưa có lớp nào biến chúng thành hành vi. ADR 0202 còn để ngỏ P8 "công thức môi trường → comfort".

## Quyết định chung

1. **Behavioral Engine là hàm thuần, chỉ đọc.** `BehaviorEngine.Decide(state, uid, nowMs, phase)` không đổi `GameState`, không tạo lệnh, không đọc đồng hồ và không dùng RNG được tiêm. Hạt giống là `hashSeed("behavior", uid, nhịp)`. Cùng đầu vào cho cùng kết quả trên mọi máy (test vân tay golden chạy trên Linux và Windows trong CI).
2. **Không đổi schema.** Cá tính và hành vi đều dẫn xuất, không lưu. Phần duy nhất cần lưu (sổ quan sát nghiên cứu) đang tắt và chưa vào save (xem D4).
3. **Hành vi trình diễn không phải luật gameplay.** Chúng chỉ là gợi ý hiển thị cho tầng Sanctuary. PR này không nối Sanctuary, nên chưa có gì thay đổi với người chơi. Nếu chủ dự án coi bảng trọng số là luật cần duyệt, thì có thể duyệt riêng phần A và B trước khi Sanctuary dùng.
4. **Luật có tác động gameplay (C1, D1–D3) có bộ luật `Production` tắt** và bộ luật `ProposedForReview` chỉ dùng trong test và mô phỏng, giống cách ADR 0202 làm với thiên tai.

## A. Cá tính (Workstream A)

| Mã | Quyết định đề xuất |
|---|---|
| A1 | Bốn nét 0–100 dẫn xuất từ `Personality` đã lưu. Giá trị gốc (năng động / tò mò / ham chơi / dạn dĩ): Playful 70/55/85/60, Calm 35/40/35/55, Curious 60/85/50/65, Shy 40/50/35/20 |
| A2 | Mỗi nét lệch ổn định theo UID trong ±10: `hashSeed("personality-trait", uid, nét) mod 21 − 10`. Khoảng cách giữa các archetype ở nét đặc trưng lớn hơn 20, nên một cá thể không bao giờ "trông như" archetype khác (có test) |
| A3 | Sở thích ổn định theo UID: một thức ăn ưa thích trong `dietIds` của loài (`hashSeed("favorite-diet", uid)`), một microhabitat ưa thích trong `habitatRules` (`hashSeed("favorite-habitat", uid)`) |
| A4 | Cá tính chỉ đổi trọng số hành vi trình diễn và thứ tự ưu tiên khi chia thức ăn. **Không** ảnh hưởng vàng, XP, bond, giá bán, lai, thám hiểm hay sức khỏe. Thưởng nơi ưa thích chỉ xuất hiện trong đề xuất C1 (đang tắt) |

## B. Behavioral Engine (Workstream B)

| Mã | Quyết định đề xuất |
|---|---|
| B1 | 11 hành vi: Idle, Rest, Sleep, Explore, SeekFood, Eat, Play, Bask, Swim, Fly, Burrow. SeekFood và Eat chỉ xuất hiện do luật gameplay; 9 hành vi còn lại có thể được chọn ngẫu nhiên có trọng số |
| B2 | Mức hoạt động theo pha (`ActivityCycle` G0): loài ban ngày 100 ngày / 60 chạng vạng / 10 đêm; loài ban đêm đối xứng; loài `both` 80 ngày và đêm, 100 lúc chạng vạng. Mức ≤ 10 thì ngủ theo nhịp sinh học |
| B3 | Khả năng theo loài: Swim / Fly / Burrow cần `locomotion` tương ứng; Bask cần thở phổi hoặc lưỡng hô hấp và chịu được dải Warm hoặc Hot. Theo nơi ở: Swim cần nước (Ocean, hoặc bờ ẩm có độ mặn); Fly cần Sky hoặc chỗ đậu; Burrow cần nền đất Land không ngập; Bask cần microhabitat trên cạn không lạnh và chỉ khi có nắng (bình minh, ngày) |
| B4 | Thứ tự quyết định: (1) hiện diện: kho, đang lai, đang thám hiểm thì ẩn; trạm chăm sóc, nơi trú thì Rest; (2) Eat trong 5 s sau khi được cho ăn thật (`lastFeedXpAtMs`); (3) SeekFood nếu được giao một drop; (4) Rest khi Health hoặc Mobility < 60; (5) Sleep khi Energy < 30; (6) Rest khi zone có cảnh báo thiên tai; (7) ngủ theo nhịp sinh học; (8) chọn có trọng số |
| B5 | Trọng số trình diễn là số nguyên, công thức ở `BehaviorEngine.AmbientWeights`: nét năng động giảm Idle/Rest và tăng Swim/Fly; tò mò tăng Explore/Burrow; ham chơi tăng Play (Baby ×1,5, Juvenile ×1,2, Adult ×0,7; ở một mình ×0,6); Shy (dạn dĩ < 40) giảm một nửa Play/Explore khi có từ 3 rồng khác cùng chỗ, và Fly ×0,7; Bask ×2 ở nơi Warm; Explore ×1,5 và Play ×0,6 khi sinh cảnh chỉ "cần cải thiện" (50–79) |
| B6 | Nhịp 60 s, mỗi UID lệch pha riêng (`hashSeed("behavior-phase", uid) mod 60 000`), để các rồng không đổi hành vi cùng lúc. `UntilMs` cho Unity biết lúc nên hỏi lại |
| B7 | SeekFood dùng **đúng** các kiểm của `consumeFood` (cùng zone và microhabitat, rảnh, đúng thức ăn, chưa hết hạn, hết hồi feed 8 s), nên SeekFood không bao giờ dẫn tới lệnh bị từ chối (có test). Mỗi drop giao cho tối đa một rồng: drop sắp hết hạn trước; rồng thích đúng loại đó trước, rồi theo UID |
| B8 | Hành vi gameplay và trình diễn được phân biệt trên **từng quyết định** (`BehaviorRole`), không theo loại hành vi. Gameplay: suy ra từ state thật và có thể dẫn tới lệnh có sẵn (SeekFood → `consumeFood`), hoặc phản ánh trạng thái thật (chăm sóc, trú, vitals thấp). Trình diễn: chỉ để xem, không bao giờ dẫn tới lệnh |
| B9 | Energy hiện luôn 100 (ADR 0202 D1, chưa có luật nào đổi), nên nhánh "Energy < 30 → ngủ" chưa bao giờ xảy ra trong production. Nhánh có sẵn để khi luật năng lượng (C3) được duyệt thì không phải đổi engine |

## C. Sinh thái sinh cảnh (Workstream C)

`HabitatEcology.Explain` tách Suitability thành từng yếu tố để UI giải thích. Điểm và kết luận gọi thẳng `Suitability.Evaluate`, không có engine thứ hai (test đối chiếu 16 loài × 7 microhabitat). Yếu tố cứng (sai một cái là không phù hợp): microhabitat bản địa, nhiệt độ (quá lạnh / quá nóng), độ ẩm, độ mặn, độ sâu, độ cao, realm, hô hấp, di chuyển, chỗ đậu (Sky). Yếu tố chất lượng (mỗi điểm thiếu so với `comfortMin` trừ 1 điểm): không khí, nước, thực vật, thức ăn, chỉ những chỉ số liên quan theo realm. Thông tin thêm: nơi trú (mọi thế giới có nơi trú, ADR 0202 D3).

| Mã | Câu hỏi | Đề xuất (code có, **TẮT**: `EcologyComfortRules.Production.Enabled = false`) |
|---|---|---|
| C1 | Môi trường → comfort (trả lời ADR 0202 P8) | Mục tiêu = `max(sàn, suitability + 5 nếu là nơi ưa thích)`, giới hạn 0–100. Sàn: Cozy 60 (môi trường không bao giờ tự chặn lai hay thám hiểm), Ecosystem 40 |
| C2 | Tốc độ và thời điểm | Comfort tiến về mục tiêu 1 điểm mỗi mốc 6 phút tuyệt đối (tách khoảng thời gian không đổi kết quả). Online: tăng và giảm. **Offline: chỉ tăng**. Rồng ở kho, trạm, nơi trú, đang bận hoặc có ca mở: không đổi |
| C3 | Năng lượng | Chưa làm. Gợi ý cho sau: Sleep hồi Energy, Play/Explore tiêu nhẹ, sàn 30 để không bao giờ chặn gì |
| C4 | Điểm nối khi duyệt | Gọi `EcologyComfort.Apply` trong quyết toán thời gian của `CommandDispatcher` (cùng chỗ U2.6 gọi `CareCenter.Progress`), truyền qua `SessionEnvironment` như `DisasterRules`. Không cần đổi schema vì `vitals.comfort` đã có |

Hệ quả đo được (báo cáo mô phỏng, mục 4): ở môi trường canonical, **cả 30 cặp loài × microhabitat hợp lệ đều đạt suitability 100**, nên C1 không đổi gì cho tới khi có luật làm suy giảm môi trường (ADR 0202 P7). Nếu chất lượng giảm 40 điểm, 20/30 cặp sẽ xuống dưới comfort 60 ở Ecosystem (chặn lai và thám hiểm sau 4–6 giờ online); Cozy không cặp nào bị chặn. Vì vậy C1 nên được duyệt cùng P7.

## D. Nghiên cứu và khám phá (Workstream D)

| Mã | Câu hỏi | Đề xuất (code có, **TẮT**: `ResearchRules.Production.Enabled = false`) |
|---|---|---|
| D1 | Cấp hiểu biết theo loài | Sighted (đã sở hữu) → Profiled (đã mở hồ sơ sinh học) → LifeCycle (đã thấy đủ 3 giai đoạn) → Ethology (đã ghi nhận mọi tập tính loài làm được, cả lúc hoạt động lẫn lúc ngủ). Ba cấp đầu **dẫn xuất từ dữ liệu đã lưu** (Codex, `tutorial.profilesViewed`), không cần lưu thêm |
| D2 | Mở hồ sơ Bách khoa theo cấp | Identity → Habitat → Physiology → Behavior. Khi luật tắt: mọi phần mở cho loài đã khám phá (giữ nguyên hành vi hiện tại) |
| D3 | Ghi nhận tập tính | Người chơi chạm "quan sát" một rồng đang ở sinh cảnh; ghi lại hành vi và pha hiện tại. Mỗi rồng tối đa 1 lần / 30 s. Rồng ở kho, đang lai, đang thám hiểm không quan sát được |
| D4 | Lưu sổ quan sát | **Chưa lưu.** `ObservationJournal` nằm ngoài `GameState`. Khi D1–D3 được duyệt: thêm khối `research: { species: [{ speciesId, behaviors[], phases[] }] }` vào Save Schema v9, migration v8 → v9 tạo khối rỗng (không suy diễn), validator và test hồi quy như U2.6 |
| D5 | Nối với vật liệu thám hiểm và nhiệm vụ mở thế giới | Chỉ đề xuất, chưa có code: (a) nhiệm vụ `QUEST_AQUATIC_EXPLORATION` có thể là "đạt LifeCycle cho 3 loài Khởi Nguyên", `QUEST_SKY_EXPLORATION` là "đạt Ethology cho 1 loài Thủy Vực"; (b) vật liệu thám hiểm (`coral`, `crystal`…) dùng để "khảo sát" mở phần Physiology của loài cùng realm mà không cần nuôi đủ 3 giai đoạn; (c) không thêm tiền tệ mới, không thưởng vàng cho nghiên cứu (giữ cân bằng kinh tế) |

Hệ quả đo được (báo cáo mô phỏng, mục 5): mỗi loài có 8–9 tập tính cần ghi nhận; đạt Ethology sau **14–51 lần quan sát** (khoảng 15–50 phút chơi chủ động). Như vậy là nhanh. Nếu muốn nghiên cứu là mục tiêu dài hạn, nên thêm một giới hạn, ví dụ mỗi loài tối đa 2 tập tính mới mỗi ngày UTC, hoặc yêu cầu quan sát trong ít nhất 2 ngày khác nhau.

## Câu hỏi cần chủ dự án quyết

1. Bảng trọng số A1, B2, B5 có cần duyệt như luật không, hay chỉ là tinh chỉnh hình ảnh do Sanctuary tự chọn?
2. Duyệt C1/C2 cùng lúc với ADR 0202 P7 (suy giảm môi trường)? Sàn Ecosystem 40 hay 60?
3. Có muốn luật năng lượng (C3) không? Nếu có, Energy có được chặn hành động nào không?
4. Có bật nghiên cứu D1–D3 không? Nếu có, giới hạn tốc độ nào (D5 hệ quả), và đồng ý Schema v9 như D4?
5. Nội dung nhiệm vụ mở thế giới có nên dùng cấp nghiên cứu (D5a)?

## Hệ quả

- Unity có ngay API chỉ đọc (`Queries.BehaviorQueries`) và DTO cho Sanctuary, không phải chờ duyệt, vì API không đổi state.
- Khi C1 hoặc D1–D3 được duyệt: đổi `Production` của bộ luật tương ứng, nối điểm gọi (C4) hoặc thêm Schema v9 (D4), thêm test cho giá trị đã duyệt. Engine và DTO không phải đổi cấu trúc.
- Đổi trọng số hay hàm hash sẽ đổi hành vi người chơi thấy; test vân tay golden buộc thay đổi đó phải có chủ đích.
