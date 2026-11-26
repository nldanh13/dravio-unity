# U1-CHAR.7: Advanced Creature Motion & Biological Animation — báo cáo kiểm thử

## Phạm vi bằng chứng

- **Môi trường:** Linux, Python 3.13, Blender 5.3 (module `bpy`), .NET 8.
- **Không có Unity Editor**, nên chưa chạy Unity 6000.6. Animator, shaper, head look, ẩn model, Animation Lab và stress test **chưa được mở hay đo trong Unity**.
- **Không truy cập được 7 GLB thật** (`01_emberling.glb` … `07_sparkwing.glb`). Phiên làm việc không có thư mục nào từ máy của bạn.
- Mọi số liệu là trên **SAMPLE**, đặt tên theo đúng 7 file của bạn (`SAMPLE_01_emberling` … `SAMPLE_07_sparkwing`) để kiểm cả đường nhận tên. Kèm Frostfin, Leviathan và 4 prop.
  - `SAMPLE_07_sparkwing` **dùng hình của mẫu Zephyr**, vì thư viện mẫu không có Sparkwing.
  - `SAMPLE_06_magma` là khối thô.
- **Không model thật nào được nghiệm thu.** Script chạy tại máy: `run_u7_local.ps1` (mục 7).

## 1. Kiểm thử tự động

| Kiểm thử | Kết quả |
|---|---|
| Python `unittest` | **64/64**: 56 cũ + 8 mới (luật clip bơi, đậu, đào theo dữ liệu sinh học; tên 7 GLB; override chỉ dùng "auto" cho loài thiếu số chân; khối `motion` phẳng; capability matrix) |
| `build_archetypes.py --check`, `build_species_spec.py --check` | khớp với file đã commit |
| `run_selftest.py --bpy-module` | ALL OK |
| `dotnet test Assets/Dravio/Character/Tests/DotNet~` | **38/38**: 27 cũ + 11 test Motion Contract |
| Đột biến runtime (4 lỗi cài thử) | cả 4 bị bắt: bỏ khoá dịch chuyển, chuỗi đào bị hở, không giữ môi trường khi lớn lên, sai dấu độ lệch root |
| `dotnet build .../TypeCheck` (stub, không phải Unity) | Manager, Instance, HeadLook, Blink, MotionShaper biên dịch được |
| Domain / Persistence (không sửa) | 422 pass + 3 bỏ qua / 192 pass |
| Cache batch | 13/13 `unchanged` |
| GLB nguồn | SHA-256 không đổi |

## 2. QA trước/sau (SAMPLE, ngưỡng không đổi)

Trước là rig v4 (U1-CHAR.6), sau là rig v5. So khớp theo loài trên cùng hình SAMPLE.

| Model | Clip | v4 | v5 | Chuyển tiếp kiểm (max °) |
|---|---|---|---|---|
| 01_emberling | 13 → 14 | PASS | **PASS** | 2 (0,074) |
| 02_droplet | 14 → 20 | PASS | **PASS** | 6 (0,097) |
| 03_sprout | 14 → 18 | PASS | **PASS** | 6 (0,063) |
| 04_zephyr | 17 → 19 | PASS | **PASS** | 6 (0,069) |
| 05_steamdrake | 14 → 20 | PASS | **PASS** | 6 (0,079) |
| 06_magma | chưa rig → 18 | — | **PASS** | 6 (0,056) |
| 07_sparkwing (hình Zephyr) | chưa rig → 18 | — | **PASS** | 6 (0,069) |
| frostfin | 9 → 12 | PASS | **PASS** | 2 (0,217) |
| leviathan | 9 → 12 | PASS | **PASS** | 2 (0,028) |

- **Clip cũ giữ nguyên số liệu, trừ:**
  - **TakeOff, Land** của Zephyr: được khớp lại hai đầu, tốt hơn (TakeOff nén 0,545 → 0,655; Land giãn 1,561 → 1,489).
  - **Swim** của Steamdrake: lệch ở chữ số thứ ba.
- Bảng từng clip: `TEST_REPORT_U7_compare.md`.
- Báo cáo từng model, prefab catalog, capability matrix, motion report: `reports_u7_sample/`.

## 3. Lỗi phát hiện và cách sửa

Tìm bằng thí nghiệm một biến. Không nới ngưỡng nào.

| Lỗi | Nguyên nhân | Sửa |
|---|---|---|
| TakeOff và Land không khớp với Idle/Fly ở hai đầu (đuôi Idle có pha lắc, đầu trên không là tư thế riêng) | clip được author độc lập | đầu chạm đất = Idle **đã author**; đầu trên không = Fly **đã author** cộng độ lệch root, xuất thành `motion.rootOffsets_m` (Zephyr: TakeOff +0,390 m, Land +0,307 m). QA v3 kiểm cả độ lệch: đo được khớp đúng giá trị khai báo |
| Zephyr TakeOff xuyên đất 1,25% (ngưỡng 1%) | clip nối chuỗi không còn được tự giảm biên độ, và khuỵu 0,08 thân làm ngón chân lún xuống. Thử cho bàn chân xoay theo cẳng chân thì tệ hơn (2,47%) | `takeoff.crouchH` 0,05 → 0,63% |
| Sprout BurrowEnter/Emerge xuyên đất 1,79% | bàn chân sau xoay theo cẳng chân khi thân hạ làm ngón chân lún xuống. Thử với `footFollow` 0 và độ hạ 0 thì hết hẳn | `dig.footFollow` 0, `dig.dropH` 0,015 → 0,18%, vẫn giữ dáng khuỵu |
| Planner tạo chuỗi "cannot …" mỗi tick | ghép chuỗi trong vòng cập nhật | cache theo intent → 0 B/frame (có test) |
| Test Unity `ShownLocomotion("TakeOff") == Fly` sẽ vỡ | TakeOff giờ là interaction | test kiểm đúng mục đích ban đầu (Foot IK tắt khi TakeOff/Glide, bật khi Perch) |

## 4. Runtime: Motion Contract (Workstream E)

- **`DragonMotionMode`** (Ground, Water, Air, Perched, Underground) và **`DragonMotionCapabilities.From(clips)`**: khả năng theo loài × stage, đọc từ clip của model.
- **Planner:**
  - máy trạng thái theo môi trường; chuyển tiếp nối chuỗi trong mảng cố định;
  - Wake chạy trước khi rời Sleep;
  - one-shot lặp và phản hồi người chơi chờ tới khi môi trường ổn định;
  - asset cũ thiếu clip chuyển tiếp thì chuyển ngay và ghi note.
- **`SanctuaryDragonSync`:**
  - `MotionStatusOf`: `canTranslate`, `rootOffsetToApply`, `visible`, `transition`, `completed`;
  - `ReadinessFor`, `CapabilitiesOf`;
  - frame có thêm `verticalSpeed`;
  - lớn lên giữ nguyên môi trường.
- **`IDragonMotionTarget`** (manager implement): `MotionInfoOf`, `SetHidden` (ẩn model khi ở dưới đất, không đánh giá Animator), `SetMotionShape`, `SetExpression`.
- **`DragonMotionShaper`:** nghiêng cánh, ngóc/chúi, uốn thân/đuôi, dáng đuôi theo tính cách, vây tai. Không di chuyển vị trí.
- **Tương thích ngược:**
  - giá trị int của Animator không đổi;
  - target U5/U6 vẫn chạy;
  - đường cũ (không intent) giữ trạng thái mặc định;
  - API mà Sanctuary hiện dùng (`ShownLocomotion`, `CurrentInteraction`, `Sync`, `React`) không đổi.

## 5. Hiệu năng

**Đo được, nhưng chỉ phần C# thuần.**
- Lệnh: `dotnet run -c Release --project Assets/Dravio/Character/Tests/DotNet~/Bench/...`, trên Xeon 2,1 GHz, 4 nhân, .NET 8.
- Benchmark giờ chạy đường Motion Contract với mọi intent, gồm cả intent nước và không trung.
- Không gồm Animator, skinning, render. **Không phải thiết bị di động.**

| Rồng | Có intent và Motion Contract | Không intent | Cấp phát |
|---|---|---|---|
| 5 | 3,5 µs/frame | 2,1 µs/frame | 0 B/frame |
| 15 | 9,8 µs/frame | 4,8 µs/frame | 0 B/frame |
| 30 | 19,6 µs/frame | 9,4 µs/frame | 0 B/frame |

So với U6 (11,5 µs cho 30 rồng), chi phí tăng do mỗi rồng có thêm metadata, shaper, biểu cảm và trạng thái chuyển động. Vẫn dưới 0,02 ms/frame cho 30 rồng.

**Chưa đo:** FPS và bộ nhớ trên thiết bị với 5/15/30 rồng. Stress scene có chế độ `behaviourIntents` (tôn trọng `canTranslate`, có intent theo môi trường).

## 6. Không hồi quy

| Kiểm tra | Kết quả |
|---|---|
| Ngưỡng `character-budgets.json` | không đổi |
| Hình thái sinh học | không đổi. Clip mới chỉ theo locomotion, hô hấp và "chân đậu" của dữ liệu canonical (có test) |
| `limbCount` Magma, Sparkwing | "auto" trong `asset-overrides.u7-local.json` (cùng quy tắc với U4): rig chỉ khi đo rõ ràng, ghi chú chờ art director xác nhận |
| GameState, Persistence, HUD, Environment, Open Game Scene | không sửa file nào |
| GLB nguồn, asset đã duyệt | không bị ghi đè |

## 7. Chạy trên 7 GLB thật tại máy bạn

```powershell
powershell -ExecutionPolicy Bypass -File scripts\art\character\run_u7_local.ps1 `
  -Source "B:\Dravio\<thư mục chứa 01_emberling.glb..07_sparkwing.glb>" `
  -Blender "C:\Program Files\Blender Foundation\Blender 5.3\blender.exe" `
  -OldWork "B:\Dravio\work_u6" -Work "B:\Dravio\work_u7" -DotNet `
  -UnityOut "Assets\Dravio\Character\Imported" -Unity "C:\Program Files\Unity\Hub\Editor\6000.6.10f1\Editor\Unity.exe"
```

Gửi lại:
- `U7_before_after.md`
- `batch-report.md`
- cả thư mục `reports\` (capability matrix, motion report, QA từng model)
- `qa_after\*.png`
- `dotnet\*`
- `unity\*`

**Trong Unity:**
1. Xoá `Animation\DragonBase_*.controller` cũ để sinh state mới.
2. Mở Animation Lab, xem Behaviour preview và thanh trượt turn/climb.
3. Chạy stress scene với `behaviourIntents` trên thiết bị.

## 8. Chưa kiểm chứng / giới hạn

- **Unity:** state Animator mới, blend ngắn của clip nối chuỗi, shaper trên bone thật (trục sau khi import FBX), ẩn model, Animation Lab, stress trên thiết bị.
- **GLB thật:** chưa xử lý.
- **Chất lượng nghệ thuật:** clip vẫn là procedural. Cần animator duyệt Greet, Perch, chuỗi đào và chuyển động nước.
- **Tốc độ Swim/Fly** (số thân mỗi chu kỳ) và **tốc độ quay tối đa** là mặc định pipeline, chờ art director xác nhận.
- **Chưa có:**
  - cảnh nằm trong lòng đất (chỉ ẩn model; VFX đất là việc của Environment);
  - clip quay riêng (dùng shaper và bước Walk);
  - clip nằm xuống.
- **Sừng, mào:** không có bone nên không chuyển động.
- **Biến đổi thủ tục:** tính toán đã có test; hình ảnh chưa xem trong Unity.
