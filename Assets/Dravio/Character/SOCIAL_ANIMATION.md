# Tương tác giữa hai rồng, biểu cảm và chuyển động thích ứng (U1-CHAR.8)

Runtime Character vẫn **chỉ trình diễn**.

**Sanctuary quyết định:**
- rồng nào gặp rồng nào và khi nào;
- đặt hai con ở đâu, quay mặt về đâu;
- khi nào kết thúc.

**Runtime không:**
- tạo quan hệ hay đổi trạng thái Domain;
- tự di chuyển rồng.

Giá trị thời gian và biên độ trong tài liệu này là **mặc định art direction**, không phải luật gameplay. Cơ sở nghiên cứu ở mục 6.

## 1. Hành động đôi (`DragonSocialAct`)

**Điều kiện:** cả hai frame phải ghi tên nhau (`partnerUid`) **và** cùng một act. Nếu chỉ một bên ghi thì trạng thái là `WaitingForPartner`, và rồng kia không bị điều khiển.

| Act | Vai trò | Diễn ra | Khoảng cách gốc–gốc (`desiredSpacing`) |
|---|---|---|---|
| `Greet` | Leader / Responder | Hai con nhìn đầu nhau. Leader chạy Greet, responder đáp sau **0,3 s**. Hai con giữ nhìn nhau 1,5 s rồi sang `Done` | (La + Lb)/2 + 0,1·Lmin: mõm gần chạm |
| `Nuzzle` | Leader / Responder | Leader dụi đầu (clip **Nuzzle**), responder đáp bằng Happy (thiếu clip thì LookAround) | (La + Lb)/2 − 0,05·Lmin: chạm mõm |
| `Play` | luân phiên | Mở bằng **play bow** (Stretch), rồi **luân phiên lượt có đổi vai**: lượt chẵn leader, lượt lẻ responder. Bên kia đáp sau 0,3 s. Cứ 3 lượt lại cúi chào chơi một lần | (La + Lb)/2 + 0,3·Lmin |
| `RestTogether` | Leader / Responder | Cả hai nằm Rest (dưới nước thì SwimRest, hoặc SwimIdle nếu không có clip đó). **Nhịp thở được khớp pha** một lần khi cùng clip. Thỉnh thoảng liếc nhau (giữ 30 % thời gian) | (Wa + Wb)/2 + 0,05·Lmin: nằm cạnh |
| `Nurture` | Guardian / Young | Cần một Baby và một Juvenile/Adult. Baby chơi quanh (Happy/Curious, mỗi 3–6 s), Guardian theo dõi (giữ nhìn 85 %) và **dụi đầu Baby mỗi 3 nhịp**. Baby có tempo ×1,1 | như Play |

- **Vai trò:** stage lớn hơn làm leader (Nurture thì đó là guardian). Cùng stage thì UID nhỏ hơn làm leader. Tất định.
- **Các pha:**
  - `OutOfRange`: Sanctuary đưa hai con về `desiredSpacing ± 35 %`. Trong lúc đó mỗi con vẫn chạy intent riêng (ví dụ Walk) và đã nhìn bạn.
  - `Preparing`: một bên đang chuyển tiếp, ngủ hoặc chạy one-shot.
  - `Acting`: hành động đang diễn.
  - `Done`.
  - `Rejected`: khác môi trường; nghỉ trên không hoặc dưới đất; Nurture không có Baby.
- **Đồng bộ animation giữa hai cá thể:**
  - one-shot của hai con được **cue trong cùng một tick**, chỉ khi cả hai đều rảnh;
  - responder có độ trễ cố định;
  - clip lặp (nghỉ, thở) được khớp pha bằng `SyncPhase`: follower chạy clip từ normalized time của leader.
- **Thiếu clip:** chuyển sang clip gần nghĩa trong chính model (Nuzzle → Curious → Greet; Greet → Happy → LookAround). Không giả khả năng.
- **Ngủ:** frame có intent Sleep sẽ không bị đánh thức. Pha là `Preparing`, kèm note.
- Trong act, planner dùng intent nội bộ `Social`: giữ dáng nghỉ của môi trường hiện tại, không chạy biến thể idle.

```csharp
frames.Add(new SanctuaryDragonFrame { uid = "a", partnerUid = "b", social = DragonSocialAct.Greet, intent = DragonBehaviorIntent.Idle, /* ... */ });
frames.Add(new SanctuaryDragonFrame { uid = "b", partnerUid = "a", social = DragonSocialAct.Greet, intent = DragonBehaviorIntent.Idle, /* ... */ });
sync.Sync(frames, dt);
var st = sync.SocialStatusOf("b");     // phase, role, desiredSpacing, spacingTolerance, distance, inRange, faceEachOther, beats, note
if (st.phase == DragonSocialPhase.OutOfRange) MoveToward("b", "a", st.desiredSpacing);   // Sanctuary di chuyển
if (st.faceEachOther) FaceEachOther("a", "b");                                          // Sanctuary xoay
// kết thúc: social = None ở cả hai frame
```

## 2. Biểu cảm: mắt, đầu, cổ, đuôi (`DragonExpression`)

Rig không có bone mặt, nên không giả biểu cảm khuôn mặt.

**Biểu cảm thể hiện qua:**
- đuôi: dáng đuôi và vẫy (sóng chạy ra chóp);
- vây tai: chỉ khi rig có bone `EarFin_*`;
- nghiêng đầu (roll);
- nâng hoặc hạ cổ;
- ánh nhìn.

| Biểu cảm | Dáng đuôi | Vẫy (° / Hz) | Vây tai | Nghiêng đầu | Cổ |
|---|---|---|---|---|---|
| Happy | +0,4 | 18 / 2,0 | 0,6 | 0 | +4° |
| Playful | +0,6 | 22 / 2,5 | 0,8 | 0 | +2° |
| Curious | +0,2 | 6 / 1,0 | 1 | 12° | +6° |
| Alert | +0,3 | — | 1 | 0 | +8° |
| Calm | −0,1 | 5 / 0,4 | 0,2 | 0 | −2° |
| Shy | ≤ −0,6 | — | 0 | 6° | −8° |
| Affection | +0,3 | 10 / 1,2 | 0,5 | 8° | −3° |
| Sleepy | −0,2 | — | 0 | 0 | −6° |

- Dáng đuôi được cộng vào dáng theo tính cách (Playful +0,6 … Shy −0,6), rồi kẹp trong ±1.
- **Giới hạn:** nghiêng đầu ≤ 15°, cổ ≤ 10°, vẫy ≤ 25°.
- **Nguồn biểu cảm:**
  - có act đôi: biểu cảm của act;
  - không có act: suy từ one-shot đang chạy (Happy/Greet → Happy, Curious → Curious, LookAround → Alert, hoặc Shy nếu tính cách nhút nhát) và từ trạng thái lặp (Sleep → Sleepy; Rest/Bask/SwimRest → Calm).
- **Ánh mắt:**
  - head look quay cổ và đầu về mục tiêu (đầu bạn, người chơi, thức ăn);
  - khi ánh nhìn chuyển **≥ 17°** (nhìn mục tiêu mới hoặc mục tiêu nhảy), mi mắt chớp theo, gọi là gaze-evoked blink;
  - chỉ có khi mesh có blend shape mí mắt; mesh sinh tự động thường không có, khi đó không chớp mắt.

API: `IDragonSocialTarget.SetExpressionState(uid, DragonExpressionSolver.For(expression, personalityTailCarriage))`. `DragonVisualManager` đã implement.

## 3. Chuyển động thích ứng địa hình

`DragonFootGrounding` (foot IK) giờ làm thêm hai việc:
- **Nghiêng thân theo dốc:**
  - lấy độ cao mặt đất dưới chân trước, chân sau, chân trái và chân phải;
  - thân theo **60 %** độ dốc (`bodyAlign`): pitch tối đa 20°, roll tối đa 12°;
  - chân IK bù phần còn lại;
  - đầu giữ thăng bằng bằng head look.
- **Không cấp phát mỗi frame:** bỏ hai mảng `new float[]` cũ trong LateUpdate.

Thân xoay quanh gốc của rồng, nên vị trí rồng vẫn do Sanctuary quyết định.

## 4. Hoàn thiện cất cánh, hạ cánh, xuống nước: sự kiện và mực nước

- **`DragonMotionStatus.motionEvent`** (`DragonMotionEvent`): `LiftOff`, `TouchDown`, `WaterContact`, `WaterLeave`.
  - Bắn **một lần** ở frame mà clip chuyển tiếp đi qua mốc thời gian đã author.
  - Dùng để phát bụi, bọt nước hay âm thanh tại anchor (`VFX_Ground`, `VFX_Mouth`, …).
- **Mốc thời gian:**
  - **TakeOff:** đo trên clip; là thời điểm root rời đất sau cú đạp chân. SAMPLE: 0,32.
  - **Land:** chạm đất ở 0,6 của clip. Pha trước đó hãm bằng cánh và ngóc thân; sau đó chân gập để hấp thụ lực.
  - **WaterEnter / WaterExit:** qua mực nước ở 0,5.
- **Mực nước khi bơi nổi** (loài có SwimSurface: thở phổi hoặc lưỡng hô hấp):
  - `MotionInfo.surfaceWaterline` là độ cao của đường sống lưng so với root, nằm ngang mặt nước;
  - `MotionInfo.headAboveWater` là khoảng tâm đầu nhô trên mặt nước;
  - Sanctuary tự đặt mặt nước.

## 5. Tối ưu cho mobile

| Hạng mục | Thay đổi |
|---|---|
| Lớp thủ tục | Shaper, head look và biểu cảm chỉ chạy cho rồng cập nhật mỗi frame và ở **≤ 12 m** (`UpdateRatePolicy.detailDistance`). Chớp mắt chỉ ở **≤ 6 m** (`blinkDistance`) |
| Skinning theo LOD | LOD0 4 bone/đỉnh, LOD1 2, LOD2 1 (`SkinQuality`), `updateWhenOffscreen` tắt. Prefab builder đặt cho prefab mới |
| Nén animation | Lần import đầu: `Optimal` với sai số xoay, vị trí và tỉ lệ 0,5. Asset đã import không bị đổi |
| Keyframe trong pipeline | Chỉ bone Root có khoá vị trí; các bone khác chỉ xoay. Kích thước bake từng model ở `reports_u8_sample/performance-budget.md` |
| Cấp phát | Sync, planner và coordinator: 0 B/frame với 15 cặp đang diễn (test). Foot IK: bỏ cấp phát mỗi frame |
| Không làm | `Optimize Game Objects`: shaper, head look và foot IK cần ghi vào transform của bone; bật tuỳ chọn này thì các lớp đó mất tác dụng |

## 6. Cơ sở

- **Play bow** là tín hiệu mời và duy trì cuộc chơi ở họ chó. Cuộc chơi có đổi vai và tự chấp (self-handicap) (Bekoff 1995, *Play signals as punctuation*).
- **Gaze-evoked blink:** người nhắm mi ở 97 % lần dịch ánh nhìn trên 33°, và mọi lần quay đầu trên 17° (Evinger et al. 1994). Nhiều động vật có xương sống cũng chớp mắt khi dịch ánh nhìn lớn.
- **Độ dốc:** dê trên dốc điều chỉnh chủ yếu ở chân trước, và cột sống phối hợp vùng ngực–thắt lưng (Biomimetics 2022). Chưa có số đo trực tiếp về tỉ lệ thân theo dốc, nên **60 % là mặc định art**.
- **Độ trễ đáp 0,3 s, nhịp chơi, khoảng cách:** đây là **mặc định art direction**, không có số đo sinh học tương ứng cho rồng.
- Các nguồn khác: `BIOMECHANICS_RESEARCH.md`.

## 7. Kiểm thử

`Tests/SocialTests.cs` có 15 test: điều kiện hai chiều, đồng bộ và độ trễ, khoảng cách, luân phiên đổi vai, khớp pha, Nurture, từ chối, fallback clip, biểu cảm, sự kiện, độ dốc, vẫy đuôi, chính sách mobile, 0 B/frame.

Chạy:

```bash
dotnet test Assets/Dravio/Character/Tests/DotNet~ -c Release
```
