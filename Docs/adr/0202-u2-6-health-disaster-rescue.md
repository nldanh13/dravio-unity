# ADR 0202 — U2.6: sức khỏe, thiên tai, trú ẩn, chăm sóc và cứu hộ

- **Trạng thái:** Đề xuất. Đang chờ chủ dự án duyệt. Phần có nguồn đã được triển khai. Phần chưa có nguồn được ghi là P1–P8 và **thiên tai tự nhiên đang TẮT** ở bản chạy thật (`DisasterRules.Production.Enabled = false`).
- **Phạm vi:** Bản Unity (`nldanh13/dravio-unity`), giai đoạn U2.6, Issue #34. Domain C# thuần, Save Schema v8, phiên `GameSession`. Không sửa Character, Environment, Animator, HUD hay Scene.
- **Nguồn đã dùng:** GDD 3.1 tr. 9–11 (thiên tai, chăm sóc), tr. 18 (an toàn offline), tr. 24 (một ca chỉ tạo một lần), tr. 28–29 (chế độ Zen/Cozy); ADR 0003 của bản TS (điều kiện sức khỏe khi lai, nhịp kiểm hazard); ADR 0201 mục 4 (điều kiện sức khỏe khi thám hiểm); gói G0 (`SCENARIOS`, `PACK_README`, kịch bản S02); catalog sinh học (`resiliencePct` theo hazard).
- **Nguyên tắc giữ nguyên:** Không có quảng cáo. Không đổi cân bằng kinh tế đã duyệt. Không mất dữ liệu save cũ. Thiên tai không bao giờ gây tử vong và không bao giờ xảy ra offline.

## Bối cảnh

GDD 3.1 mô tả vòng lặp "cảnh báo → chuẩn bị → thiên tai → cứu hộ → chăm sóc → phục hồi" nhưng chưa định lượng nhiều điểm: mỗi thế giới gặp hazard nào, mức độ sự kiện, vị trí tổn thương, chỉ số giảm bao nhiêu, thời lượng từng pha sự kiện. Spec U2.6 yêu cầu "không tự đặt luật gameplay nếu GDD chưa xác định". ADR này tách rõ:

1. **Luật có nguồn**: đã triển khai và bật.
2. **Quyết định kỹ thuật D1–D8**: cần để code chạy, không đổi gameplay.
3. **Luật còn thiếu P1–P8**: có đề xuất trong code (`DisasterRules.ProposedForReview`) chỉ để test, chưa bật.

## Luật có nguồn (đã bật)

| Luật | Giá trị | Nguồn |
|---|---|---|
| Điều kiện sức khỏe khi lai (cả hai bố mẹ) | `health ≥ 80`, `mobility ≥ 80`, `comfort ≥ 60`, không có ca chăm sóc mở | ADR 0003 |
| Điều kiện sức khỏe khi thám hiểm | như trên | ADR 0201 mục 4 |
| Nhịp kiểm hazard | mỗi 60 phút World foreground, xác suất 20 % (ADR 0003 ghi là giả thuyết) | ADR 0003 |
| Cảnh báo trước | 10 phút | G0 |
| Rủi ro mỗi rồng | `risk = hệSốMứcĐộ × (1 − resilience/100) × (1 − bảoVệNơiTrú)`; nhẹ 0,30, trung bình 0,60; nơi trú bảo vệ 1 | GDD tr. 9, G0 |
| Số ca tối đa mỗi sự kiện | 2; nhiều hơn thì chọn risk giảm dần, rồi UID | G0 |
| Roll | một lần mỗi rồng mỗi sự kiện, `hashSeed("injury", eventId, uid)`; không roll lại khi nạp | GDD tr. 24 |
| Cooldown thế giới | 24 h đồng hồ thật sau khi đóng sự kiện; tối đa một sự kiện mở mỗi thế giới | G0 |
| Offline / Zen | không sinh thiên tai, không tích dự báo, không gây thương; dự báo đang chạy thì rồng đang phơi nhiễm được tự đưa vào nơi trú | GDD tr. 18, 28 |
| Cozy | sự kiện luôn ở mức nhẹ | GDD tr. 29 |
| Vị trí tổn thương theo hazard | cháy rừng → da/vảy hoặc hô hấp; dòng chảy mạnh → vây/mang; giông bão → chi/cánh | GDD tr. 10 |
| Trạm chăm sóc | 2 chỗ; ca thừa chờ trong nơi trú, nhận vào theo FIFO | G0 |
| Thời lượng chăm sóc | nhẹ 20 phút, trung bình 80 phút; kế hoạch phù hợp còn ¾ (15 / 60 phút); luôn trong khoảng GDD | G0, GDD tr. 11 |
| Pha chăm sóc | Ổn định 10 %, Hồi phục 50 %, Phục hồi chức năng 30 %, Trở lại 10 %; biên làm tròn lên | G0 |
| Phục hồi khi xong | mobility lên 90, health lên ít nhất 90, comfort lên ít nhất 80; không bao giờ hạ chỉ số đang cao hơn | G0 |
| Quy trình | nhận định → (đặt kế hoạch) → bắt đầu → chờ → xem lại → xuất viện; kế hoạch không phù hợp chỉ hiện giải thích, không gây hại | GDD tr. 10–11, G0 S02 |
| Tiến trình chăm sóc offline | chạy theo đồng hồ, giống online, không tự xuất viện | GDD tr. 18 |

## Quyết định kỹ thuật

- **D1. Chỉ số ban đầu là 100.** Mọi rồng mới, rồng nở và rồng của save cũ (qua migration) có `health = energy = comfort = mobility = hydration = 100`. GDD không nêu giá trị ban đầu, và 100 không chặn bất kỳ hành động nào đang có.
- **D2. Pha Preparation và Active diễn ra tức thời.** Forecast chính là cửa sổ chuẩn bị 10 phút. Khi hết dự báo, sự kiện đi Forecast → Preparation → Active trong cùng một lần quyết toán, rồi sang Rescue / Recovery / Closed theo số ca còn mở. Thời lượng riêng của từng pha là P5.
- **D3. Nơi trú không giới hạn sức chứa** và không tính vào sức chứa thế giới. Rồng trong nơi trú không sản xuất. Khi sự kiện kết thúc, rồng về đúng sinh cảnh cũ (nếu còn chỗ, không thì vào kho) và được trả lại ngân sách sản xuất nếu còn chỗ.
- **D4. Rồng đang lai hoặc đang thám hiểm không bị phơi nhiễm.** Chỉ rồng rảnh trong vùng có sự kiện mới tính rủi ro.
- **D5. Xuất viện không tự bật lại sản xuất.** Người chơi chọn đích xuất viện (sinh cảnh hoặc kho), rồi tự gán slot sản xuất như với mọi lần di chuyển.
- **D6. Không có thưởng vàng cho chăm sóc.** Phần thưởng 50 G cho ca hướng dẫn trong đề xuất G0 không được triển khai, để không đổi cân bằng kinh tế. Xem P6.
- **D7. Thiên tai tự nhiên tắt ở bản chạy thật** cho tới khi P1–P8 được duyệt. Test và bản cân bằng tiêm `DisasterRules` qua `SessionEnvironment.DisasterRules` hoặc tham số của `CommandDispatcher.Dispatch`.
- **D8. Dữ liệu cũ giữ nguyên.** Save v7 có rồng ở `shelter` / `care` hay `activity = care` mà không có ca được giữ đúng như vậy sau migration (không tự chuyển vào kho). Validator chấp nhận trường hợp này, nên checksum và parity với TS không đổi. Rồng đó vẫn bị kẹt như ở v7. Cách giải phóng cần một quyết định riêng.

## Luật còn thiếu (chờ duyệt, chưa bật)

| Mã | Câu hỏi | Đề xuất trong code (`ProposedForReview`) |
|---|---|---|
| P1 | Mỗi thế giới gặp hazard nào? | Khởi Nguyên = cháy rừng, Thủy Vực = dòng chảy mạnh, Thiên Giới = giông bão (suy từ bảng GDD tr. 9) |
| P2 | Mức độ của sự kiện sinh thái chọn thế nào? | Chọn đều nhẹ / trung bình bằng RNG của sự kiện (`EventSeverityPolicy.Uniform`). Khi chính sách là `Undefined` thì không tạo sự kiện nào |
| P3 | Vị trí tổn thương khi hazard có hơn một vị trí | Chọn đều bằng `hashSeed("injury-site", eventId, uid)` |
| P4 | Bị thương thì giảm chỉ số bao nhiêu? | `health −10k`, `comfort −10k`, `mobility −20k` với chi/cánh hoặc vây/mang; `k = 1` nhẹ, `2` trung bình. Với `null`, chỉ số không đổi nhưng ca vẫn chặn lai / thám hiểm / bán |
| P5 | Thời lượng pha Preparation, Active, Rescue | Tức thời (D2) |
| P6 | Ca cứu hộ trong hướng dẫn và phần thưởng | Chưa làm (cả `acceptRescue`, `advanceRehab`, `releaseDragon`). Thưởng 50 G của G0 cần duyệt kinh tế |
| P7 | Thiên tai có làm hỏng môi trường vùng không? | Chưa mô hình hóa |
| P8 | Công thức môi trường → comfort | Chưa có. `HealthQueries.Dragon` chỉ hiển thị độ phù hợp sinh cảnh |

Các điểm khác cần chủ dự án xác nhận:

- Tự đưa vào nơi trú khi offline sẽ tạm dừng sản xuất của rồng trong thời gian cảnh báo. Đây là một tác động kinh tế nhỏ.
- Khoảng online dài hơn 24 h (bị chặn) không tách tại mốc kiểm hazard.
- Hazard kiểm theo từng thế giới đang mở, theo thứ tự catalog. Một thế giới có sự kiện mở không chặn thế giới khác.
- Chưa có điều kiện hướng dẫn trước khi thiên tai được phép xảy ra.

## Hệ quả

- Khi P1–P8 được duyệt, chỉ cần đổi `DisasterRules.Production` (và thêm test cho giá trị đã duyệt). Domain, save và UI không phải đổi cấu trúc.
- Save v8 đọc được mọi save từ v1. Save v8 không có ca mở hay sự kiện mở vẫn chiếu được về v4 cho bộ đọc TS.
