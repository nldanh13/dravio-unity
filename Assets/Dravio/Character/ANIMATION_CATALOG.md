# Animation Catalog và Behavior-to-Animation Mapping (U1-CHAR.6)

Tài liệu này liệt kê các clip mỗi loài có, Animator dùng chúng ra sao, và mỗi behaviour intent của Sanctuary được chuyển thành clip nào.

Nguồn sự thật:
- `Data/dragon-archetypes.json`, sinh bởi `build_archetypes.py` từ dữ liệu canonical.
- `Data/rig-profiles.json` cho tham số clip.
- `Runtime/Behavior/DragonBehaviorPlanner.cs` cho luật ánh xạ.
- Test ở `Tests/BehaviorTests.cs`.

Số liệu QA hiện chỉ có trên **SAMPLE**, xem `scripts/art/character/reports_u6_sample/`.

## 1. Clip theo khả năng sinh học

| Clip | Loop | Frame (30 fps) | Có ở loài | Nguồn luật |
|---|---|---|---|---|
| Idle | có | 120 | mọi loài | care interactions có sẵn |
| Eat | — | 60 | mọi loài | care interactions có sẵn |
| Sleep | có | 120 | mọi loài | care interactions có sẵn |
| Happy | — | 45 | mọi loài | care interactions có sẵn |
| Walk, Run | có | quadruped: theo Froude và chiều cao hông (SAMPLE: 22 / 12, U1-CHAR.7b, [BIOMECHANICS_RESEARCH.md](BIOMECHANICS_RESEARCH.md)); serpentine 40 / 26 | locomotion có `walk` | canonical `locomotion` |
| Swim | có | 48 | `swim` | canonical |
| Fly (+ TakeOff, Glide, Land) | có (Glide), không | 32 / 60 / 40 / 40 | `fly` | canonical |
| Burrow | có | 60 | `burrow` | canonical |
| **Rest** | có | 120 | mọi loài | U1-CHAR.6: dựa trên thân của Sleep, đầu ngẩng, mắt mở |
| **Wake** | — | 60 | mọi loài | U1-CHAR.6: từ tư thế Sleep lên Idle, khớp chính xác hai đầu (QA `transitions`) |
| **LookAround** | — | 90 | mọi loài | U1-CHAR.6: chỉ dùng đầu và cổ |
| **Curious** | — | 75 | mọi loài | U1-CHAR.6: cúi, rướn cổ, nghiêng đầu (đánh hơi) |
| **Drink** | — | 75 | `walk` | U1-CHAR.6: cần thân đứng có chân (bờ ao) |
| **Stretch** | — | 90 | `walk` | U1-CHAR.6: vươn kiểu play-bow, chân trước trụ |
| **Bask** | có | 120 | `walk` | U1-CHAR.6: nằm phơi nắng, đầu thấp, cánh mở trong giới hạn đo được (loài bay) |

- Loài thuỷ sinh (Frostfin, Leviathan) **không có** Drink, Stretch, Bask.
- Clip mới không thêm khả năng sinh học nào. Clip di chuyển vẫn đúng bằng locomotion canonical (có test).
- Thứ tự clip giữ nguyên dải frame cũ: clip mới luôn đứng sau.

Bảng 16 loài: `DRAGON_ARCHETYPES.md`.

## 2. Animator

Mỗi archetype có một controller `Animation/DragonBase_<archetype>.controller`, và mỗi model có một override riêng.

| Tham số | Kiểu | Giá trị |
|---|---|---|
| `Locomotion` | int | Idle 0, Walk 1, Run 2, Swim 3, Fly 4, Burrow 5, Sleep 6, **Rest 7, Bask 8**; U1-CHAR.7: **SwimIdle 9, SwimRest 10, SwimSurface 11, Perch 12, Glide 13** |
| `Action` | int | None 0, Eat 1, Happy 2, **Drink 3, Wake 4, Stretch 5, LookAround 6, Curious 7**; U1-CHAR.7: **Greet 8, TakeOff 9, Land 10, WaterEnter 11, WaterExit 12, DigPrep 13, BurrowEnter 14, BurrowEmerge 15**; U1-CHAR.8: **Nuzzle 16** |
| `Speed`, `PlaybackRate` | float | state di chuyển chạy theo tốc độ mặt đất |
| **`Tempo`** | float | state tại chỗ và one-shot chạy theo nhịp của tính cách |

- Giá trị cũ không bao giờ đổi, chỉ thêm vào cuối.
- Controller cũ (trước U1-CHAR.6) **không bị ghi đè**. Builder báo state hoặc `Tempo` còn thiếu; xoá controller để sinh lại.
- Instance tự phát hiện controller không có `Tempo` thì bỏ qua tempo, không báo lỗi.

## 3. Behaviour intent → clip

| Intent | Trạng thái lặp | One-shot lặp lại | Thiếu clip thì |
|---|---|---|---|
| Idle | Idle (+ biến thể khi đứng lâu, mục 4) | — | — |
| Walk / Run | Walk / Run | — | Run → Walk; không có Walk → Idle + note |
| Swim / Fly / Burrow | Swim / Fly / Burrow | — | → Idle + note (**không giả khả năng**) |
| Sleep | Sleep | — | — |
| Rest | Rest | — | Rest → Sleep → Idle |
| Bask | Bask | — | Bask → Rest → Sleep → Idle |
| Recover | Rest, tempo ×0,85 | — | như Rest |
| Eat / Drink / Play | Idle | Eat / Drink / Happy | không có clip → Idle + note |
| LookAround / Curious / Stretch | Idle | cùng tên | không có clip → Idle + note |

`Recover` là trạng thái nghỉ nhẹ nhàng khi đang được chăm sóc. Sanctuary quyết định khi nào dùng, dựa trên dữ liệu Domain. Không có tư thế ốm hay bị thương.

**Chuyển tiếp:**
- **Rời Sleep:** Wake chạy trước, nếu loài có clip, rồi mới tới trạng thái đích.
- **Vào Sleep:** dùng crossfade của Animator, chưa có clip nằm xuống.
- **One-shot không bị ngắt** bởi one-shot khác. Phản ứng hoặc one-shot mới phải chờ.
- **Quay tại chỗ** nhanh ≥ 60°/s khi Idle thì hiện Walk (bước chân) thay vì trượt.
- **Đổi stage** (đổi model) thì planner được dựng lại theo clip của model mới.

## 4. Tính cách (chỉ là cách trình diễn)

`DragonPersonalityPresentation.For(style)` cho 4 kiểu tương ứng `Personality` của Domain: Playful, Calm, Curious, Shy.
- Giá trị dưới đây là **mặc định art direction** cho trình diễn.
- Không đọc dữ liệu Domain, không đổi luật Personality, Health hay gameplay.
- Chỉnh tại `Runtime/Behavior/DragonPersonalityPresentation.cs` cùng art director.

| Kiểu | Tempo | Biến thể Idle mỗi | Trọng số LookAround / Curious / Stretch / Happy | Bị vuốt ve | Được gọi | Nhìn theo mục tiêu |
|---|---|---|---|---|---|---|
| Playful (năng động) | 1,10 | 6–12 s | 1 / 1 / 2 / 3 | Happy | Happy | 100 %, giữ liên tục |
| Calm (điềm tĩnh) | 0,90 | 14–26 s | 2 / 0,5 / 2 / 0,5 | Happy | LookAround | 50 % |
| Curious (tò mò) | 1,00 | 7–14 s | 2 / 3 / 1 / 1 | Curious | Curious | 100 % |
| Shy (nhút nhát) | 0,95 | 10–20 s | 3 / 1 / 0,5 / 0,3 | LookAround (dè dặt) | LookAround | 60 %, liếc rồi quay đi (giữ 40 % thời gian) |
| Unspecified | 1,00 | 10–20 s | 1 / 1 / 1 / 0,5 | Happy | LookAround | 80 % |

- **Cho ăn (Fed):** Eat với mọi kiểu.
- **Đang ngủ:** phản ứng bị bỏ qua và ghi note, rồng không bị đánh thức. Có đánh thức hay không là quyết định của Sanctuary.
- **Biến thể Idle:** tất định theo UID (seed FNV-1a). Chỉ rút từ clip mà model có.

## 5. Hint của Environment → intent

Nguồn: `Environment/Data/OriginSanctuaryLiving.json` ▸ `interactionPoints[].animationHint`. Hàm chuyển: `DragonBehaviorHints.FromEnvironmentHint`.

| Hint | Intent |
|---|---|
| drink | Drink |
| bask | Bask |
| rest | Rest |
| inspect, sniff | Curious |
| look, look_up | LookAround (đặt `lookTarget` lên trời cho `look_up`) |
| dig | Burrow (loài không đào: Idle + note) |
| splash | Play (điểm này đã yêu cầu loài biết bơi) |
| hint lạ | None, không đoán |

## 6. Lớp chồng lên animation

- **Head look (`DragonHeadLook`):**
  - quay Neck/Head về `lookTarget` sau Animator, chia góc cho cổ và đầu;
  - giới hạn yaw 55°, pitch 25°, mượt với half-life 0,18 s;
  - mục tiêu ở sau lưng thì bỏ qua;
  - chỉ chạy cho rồng gần, cập nhật đủ nhịp, và khi planner cho phép (không đang ngủ, không đang one-shot dùng đầu).
- **Blink (`DragonBlink`):**
  - chỉ chạy khi mesh có blend shape mí mắt (`blink` / `eye_close`), nhắm mắt khi ngủ;
  - mesh sinh tự động thường **không có**, khi đó không chớp mắt;
  - không giả chớp mắt bằng xoay bone.
- **Thở:** đã có sẵn trong clip Idle, Sleep, Rest, Bask. Rig serpentine có chân không thở từ Spine1, để hông không nhấp nhô.

## 7. U1-CHAR.7: chuyển động theo môi trường

Clip cho nước, không trung, chỗ đậu, dưới đất và Greet; chuyển tiếp giữa các môi trường; shaper nghiêng/ngóc/uốn; metadata cho Environment: [MOTION_CATALOG.md](MOTION_CATALOG.md). Hợp đồng tích hợp: [MOTION_API.md](MOTION_API.md).

## 8. U1-CHAR.8: tương tác đôi và biểu cảm

Clip **Nuzzle** (75 khung, mọi loài, chỉ cổ và đầu), các hành động đôi (Greet, Play, RestTogether, Nuzzle, Nurture), biểu cảm, nghiêng thân theo dốc, sự kiện chuyển động và tối ưu mobile: xem [SOCIAL_ANIMATION.md](SOCIAL_ANIMATION.md).
