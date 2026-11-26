# CHAR.8 — Nghiên cứu: phản ứng nhìn, tiếp cận và ăn của cá thể được chọn

> **Trạng thái:** nghiên cứu và đề xuất hợp đồng trình diễn. **Chưa có code mới** trong CHAR.8; mọi phần dựa trên API đã có (`DragonBehaviorPlanner`, `DragonHeadLook`, `DragonMotionContract`, anchor `VFX_Mouth`, metadata `rig.json ▸ motion`). Không đổi luật Domain.

## 1. Luật Domain hiện hành (chỉ đọc)

| Luật | Nguồn | Hệ quả trình diễn |
|---|---|---|
| Thả thức ăn: `feed(diet, zone, habitat)` tạo một `FoodDrop` có hạn dùng ở microhabitat | `CommandDispatcher.Feed` | điểm rơi là mục tiêu nhìn / tiếp cận |
| Giao phần ăn: mỗi drop cho tối đa một rồng; ưu tiên rồng **thích loại đó** (`FavoriteDiet`), rồi **UID nhỏ nhất** | `BehaviorEngine.AssignFood` | rồng được giao mới diễn `SeekFood` (tìm ăn) |
| Ăn: `consumeFood(foodId, uid)` chấp nhận **bất kỳ** rồng đủ điều kiện (cùng sinh cảnh, ăn được loại đó, hết hồi 8 s) | `CommandDispatcher.ConsumeFood` | rồng người chơi chọn có thể ăn dù không được giao |
| Quan sát: hồi 30 s mỗi rồng; ≤ 2 phát hiện mỗi loài mỗi ngày UTC | `ResearchRules210` | quan sát ngay trước khi ăn sẽ chặn quan sát lúc ăn |

Mô phỏng U2.12 cho thấy hệ quả thật: trong sinh cảnh đông, rồng có UID lớn hầu như không bao giờ được giao phần ăn → không quan sát được "tìm ăn", Ethology chậm (ADR 0207, F4–F5).

## 2. Chuỗi trình diễn đề xuất cho rồng được chọn

| Pha | Bắt đầu khi | Diễn | Kết thúc khi | Tham số (lấy từ dữ liệu có sẵn) |
|---|---|---|---|---|
| 1. Notice | drop xuất hiện trong ≤ 6 m | `DragonHeadLook` quay về drop; Idle giữ nguyên | sau độ trễ phản ứng | độ trễ 0,3 s (như đáp xã hội, SOCIAL_ANIMATION); Shy +0,2 s, Playful −0,1 s (`DragonPersonalityPresentation`) |
| 2. Orient | sau Notice | quay thân tại chỗ | hướng lệch ≤ 15° | tốc độ quay `motion.maxTurnRateDegPerSec.ground` (180°/s) |
| 3. Approach | lệch ≤ 15° và khoảng cách > điểm dừng | Walk (Run nếu > 4 m và tính cách Playful), `PlaybackRate = speed / groundSpeed` | tới điểm dừng | tốc độ `motion.biomechanics.walk.speed_mps`; **điểm dừng** = khoảng cách ngang từ pivot tới `VFX_Mouth` ở frame cúi đầu của Eat (đo từ clip; không đoán) |
| 4. Arrive | tới điểm dừng | blend Walk → Idle 0,15 s; head look khóa vào drop | 1 nhịp thở | — |
| 5. Eat | Arrive xong **và** lệnh `consumeFood` thành công | clip Eat (Action 1); head look tắt (clip điều khiển cổ) | hết clip | Domain quyết định thành công; trình diễn chỉ chạy sau kết quả |
| 6. Settle | hết Eat | Happy (nếu bond tăng) hoặc Idle | — | `bondChanged` event |

Không bay, không bơi để ăn: loài chỉ diễn tiếp cận bằng locomotion của môi trường hiện tại (`DragonMotionMode`); drop ngoài tầm (khác sinh cảnh) thì chỉ Notice. Loài thủy sinh dùng SwimIdle → Swim thay cho Walk.

## 3. Đề xuất cho tầng tích hợp UI (không đổi luật)

1. Khi người chơi **chọn một rồng rồi cho ăn**: UI gợi ý loại thức ăn trong `SpeciesCatalog.GetBiology(id).DietIds` mà **không rồng nào khác cùng microhabitat** (UID nhỏ hơn hoặc thích loại đó) ăn được; nếu đã hé lộ khẩu vị (`IndividualResearchProfile.FavoriteDiet`) thì ưu tiên khẩu vị. Khi đó rồng được chọn chính là rồng được giao → có cả "tìm ăn" lẫn "ăn".
2. Gửi `consumeFood(drop, uid được chọn)` khi pha Arrive kết thúc (không trước): giữ chuyển động và luật khớp nhau.
3. Không gửi `observeDragon` trong 30 s trước pha Eat nếu mục tiêu là ghi "ăn"; dùng `ObservationOpportunities` để nhắc.
4. Nếu Domain từ chối (`feed-cooldown`, `wrong-diet`, `wrong-place`): dừng ở Arrive, chơi LookAround, không diễn Eat.

## 4. Cần đo trên model thật trước khi làm code

- Khoảng cách pivot → `VFX_Mouth` ở frame cúi đầu thấp nhất của Eat cho từng archetype (quadruped, winged, serpentine).
- Thời gian Orient + Approach thực tế ở 2–6 m với 16 loài (tốc độ Froude rất chậm ở rồng nhỏ: Baby Aurorawing 0,145 m/s → 6 m mất ~40 s, có thể quá lâu cho thao tác cho ăn; cân nhắc Run hoặc giới hạn khoảng cách gợi ý).
- Có cần animation "nhặt / cúi" riêng ngắn hơn Eat 60 frame cho thao tác chạm liên tục không.
