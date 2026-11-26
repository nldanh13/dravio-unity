# Advanced Motion Catalog (U1-CHAR.7)

Chuyển động trên cạn, dưới nước, trên không và dưới đất, theo khả năng sinh học của từng loài.

Nguồn sự thật:
- `Data/dragon-archetypes.json`, sinh từ dữ liệu canonical bởi `build_archetypes.py`.
- `Data/rig-profiles.json` cho tham số.
- `Runtime/Behavior/DragonBehaviorPlanner.cs` và `DragonMotionContract.cs` cho luật.
- Test: `Tests/MotionTests.cs`, `scripts/art/character/tests/test_motion.py`.

Số liệu QA hiện chỉ có trên **SAMPLE**: `scripts/art/character/reports_u7_sample/`.

## 1. Clip mới và căn cứ sinh học

| Clip | Loop | Frame | Có ở | Căn cứ canonical |
|---|---|---|---|---|
| Greet | — | 60 | mọi loài | chỉ dùng đầu, cổ, đuôi |
| SwimIdle | có | 72 | `swim` | locomotion |
| SwimRest | có | 120 | `swim` và hô hấp `gill`/`dual` | `respiration`: nghỉ được dưới nước |
| SwimSurface | có | 48 | `swim` và hô hấp `lung`/`dual` | `respiration`: thở khí trời nên bơi ở mặt nước |
| WaterEnter, WaterExit | — | 60 / 60 | `swim` và `walk` | đi được giữa cạn và nước |
| Perch | có | 120 | `fly` và silhouette ghi "chân đậu" | chỉ **Zephyr, Blossom**; Sparkwing, Solaris, Aurorawing, Celestia không có |
| DigPrep, BurrowEnter, BurrowEmerge | — | 45 / 60 / 60 | `burrow` | Sprout, Magma, Amberhorn |
| TakeOff, Glide, Land | — / có / — | 40 / 60 / 40 | `fly` | đã có từ trước; U1-CHAR.7 khớp lại hai đầu |

**Không có:**
- Chuyển động bơi cho loài không bơi.
- Clip bay cho loài không bay.
- Perch cho loài bay mà không ghi "chân đậu".
- **Sừng** (cứng, không có bone) và **mào** (Solaris, không có bone) không được tạo chuyển động.
- **Vây tai:** chỉ chuyển động trên rig có bone `EarFin` (Droplet).

## 2. Chuyển tiếp giữa các môi trường

Mọi chuyển tiếp đều có hai đầu khớp **chính xác** với clip **đã author** bên cạnh. `anim_qa.py` v3 kiểm từng cặp với ngưỡng khép vòng sẵn có (≤ 0,5°); trên SAMPLE đều ≤ 0,217°.

| Từ → đến | Chuỗi clip | Ghi chú |
|---|---|---|
| Ground → Water | WaterEnter | đứng ở mép nước, chuyển sang tư thế bơi tại chỗ; Sanctuary đưa rồng xuống nước **sau khi** clip xong |
| Water → Ground | WaterExit | |
| Ground/Perched → Air | TakeOff | khi xong, Sanctuary **nâng** rồng bằng `takeOffRootOffset` (rootOffsetToApply) |
| Air → Ground/Perched | Land | khi bắt đầu, Sanctuary **hạ** rồng bằng `landRootOffset`; clip đưa rồng xuống đất |
| Ground → Underground | DigPrep → BurrowEnter | nối liền, không có khoảng trống; xong thì **ẩn** model |
| Underground → Ground | BurrowEmerge | hiện lại từ frame đầu |
| Ground ↔ Perched | (không có clip) | Sanctuary đặt rồng lên điểm đậu |
| Water ↔ Air, … | qua Ground | ví dụ WaterExit → TakeOff |
| Sleep → bất kỳ môi trường nào | Wake trước | |

- **Clip đứng tại chỗ:** không có root motion. Mọi clip (kể cả TakeOff và Land) giữ nguyên vị trí X/Z; độ cao của rồng do Sanctuary đặt.
- **Không xuyên nền:** DigPrep và BurrowEnter khuỵu xuống nhưng vẫn ở trên mặt đất (QA kiểm xuyên đất ≤ 1%). Pha "dưới đất" là **ẩn model**. Đống đất hay lỗ hổng là VFX của Environment, đặt tại anchor `VFX_Ground`.
- **Thiếu clip chuyển tiếp** (asset cũ): chuyển ngay, ghi note và đếm vào `substituted`.

## 3. Biến đổi thủ tục (`DragonMotionShaper`, sau Animator)

| Môi trường | Nghiêng cánh (bank) | Ngóc/chúi (pitch) | Uốn thân/đuôi khi rẽ |
|---|---|---|---|
| Air | ±30° theo tốc độ quay / `maxTurnAir` | ±25° theo góc leo | 30% |
| Water | — | ±25° theo góc lên/xuống | ±20° |
| Ground | — | — | 50% |

- Xoay quanh tâm thân trên bone Root: vị trí rồng (do Sanctuary sở hữu) không đổi.
- **Dáng đuôi theo tính cách:** Playful +0,6 (cao), Curious +0,3, Calm 0, Shy −0,6 (cụp).
- **Vây tai** dựng lên khi Curious, Greet hoặc LookAround.
- Chỉ chạy cho rồng gần, cập nhật đủ nhịp.

## 4. Metadata cho Environment Integration

Có trong `rig.json` ▸ `motion`, `*_animqa.json` ▸ `motionEnvelope`, và catalog `Entry.motion`:

| Trường | Ý nghĩa |
|---|---|
| bodyLength / Width / Height | kích thước thân lúc nghỉ (m) |
| swimVerticalExtent, swimWidth | khoảng chiếm chỗ lớn nhất khi bơi. Nước phải sâu hơn `swimVerticalExtent` để bơi ngập (biên an toàn do Sanctuary chọn); dữ liệu sinh học chỉ có `depthBands: shallow` |
| flightSpan, flightVerticalExtent | sải cánh và chiều cao khi bay: khoảng không tối thiểu cần trống |
| groundHeight / Length / Width | khoảng chiếm chỗ trên cạn |
| takeOffRootOffset, landRootOffset | độ cao root so với tư thế Fly ở cuối TakeOff / đầu Land |
| Swim, SwimSurface, Fly `groundSpeed` | tốc độ danh nghĩa = số thân mỗi chu kỳ (`motionDefaults`: 0,6 khi bơi, 3,0 khi bay). **Mặc định pipeline, chờ art director xác nhận** |
| maxTurnGround / Water / Air | tốc độ quay khuyến nghị (180 / 90 / 60 °/s, mặc định pipeline) |
| transitionsChecked, transitionMaxDeg | số chuyển tiếp đã kiểm và độ lệch lớn nhất |

## 5. Tương tác xã hội (không tạo quan hệ hay đổi gameplay)

- **Nhìn cá thể khác:** đặt `lookTarget` bằng vị trí đầu của rồng kia.
- **Tiếp cận:** `Walk` kèm `lookTarget`; đường đi do Sanctuary chọn.
- **Chào:** intent `Greet`.
- **Chơi:** `Play` (Happy) và `Stretch` (play-bow).

Ai chào ai, khi nào, và có quan hệ gì là việc của Sanctuary/Domain.
