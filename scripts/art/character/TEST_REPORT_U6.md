# U1-CHAR.6: Living Character, Expressive Animation & Production QA — báo cáo kiểm thử

## Phạm vi bằng chứng

- **Môi trường:** Linux, Python 3.13, Blender 5.3 (module `bpy`), .NET 8.
- **Không có Unity Editor**, nên chưa chạy Unity 6000.6. Prefab, Animator, Animation Lab, head look, blink và stress test **chưa được mở hay đo trong Unity**.
- **Không truy cập được GLB thật.** Phiên làm việc không có thư mục nào từ máy của bạn (kiểm tra lại ở đầu U1-CHAR.6).
- Workstream A (QA trên GLB thật) **chưa chạy**. Mọi số liệu là trên 7 model rig **SAMPLE** và 1 model SAMPLE tĩnh.
- **Không model thật nào được nghiệm thu.** Script chạy tại máy: `run_u6_local.ps1` (mục 7).

## 1. Kiểm thử tự động

| Kiểm thử | Kết quả |
|---|---|
| Python `unittest` (scripts/art/character/tests) | **56/56**: 53 cũ + 3 test luật clip hành vi |
| `build_archetypes.py --check`, `build_species_spec.py --check` | khớp với file đã commit (DRAGON_ARCHETYPES.md, dragon-archetypes.json được sinh lại) |
| `run_selftest.py --bpy-module` | ALL OK |
| `dotnet test Assets/Dravio/Character/Tests/DotNet~` | **27/27**: 14 cũ + 13 mới (planner, phản hồi, biến thể Idle, head look, blink, hint, catalog, sync tương thích ngược, 30 rồng, không cấp phát mỗi frame) |
| Đột biến planner/sync (4 lỗi cài thử) | cả 4 đều bị test bắt: bỏ Wake, đánh thức rồng ngủ, ngắt one-shot, lambda gây cấp phát mỗi frame |
| `dotnet build .../TypeCheck` | Manager, Instance, HeadLook, Blink biên dịch được với stub. **Không phải Unity thật.** Đã thêm vào CI |
| Domain / Persistence (không sửa) | 380 pass + 2 bỏ qua / 192 pass |
| Cache batch | chạy lại không đổi gì: 12/12 `unchanged` |
| GLB nguồn | SHA-256 của 13 file không đổi |

## 2. Trước/sau trên 7 model SAMPLE

Trước là rig v3 (U1-CHAR.5), sau là rig v4. Cùng ngưỡng `animationQa`, không nới. QA v2 chỉ thêm kiểm tra chuyển tiếp, dùng ngưỡng khép vòng sẵn có.

| Model | Clip v3 → v4 | Kết quả v3 | Kết quả v4 | Sleep→Wake / Wake→Idle (°) |
|---|---|---|---|---|
| droplet_Baby | 7 → 14 | PASS | **PASS** | 0,088 / 0,097 |
| emberling_Baby | 6 → 13 | PASS | **PASS** | 0,069 / 0,074 |
| frostfin_Juvenile | 5 → 9 | PASS | **PASS** | 0,217 / 0,217 |
| leviathan_Adult | 5 → 9 | PASS | **PASS** | 0,028 / 0,028 |
| sprout_Baby | 7 → 14 | PASS | **PASS** | 0,069 / 0,056 |
| steamdrake_Juvenile | 7 → 14 | PASS | **PASS** | 0,069 / 0,079 |
| zephyr | 10 → 17 | PASS | **PASS** | 0,074 / 0,069 |

Clip cũ giữ nguyên số liệu, trừ hai clip của Steamdrake: Sleep (đổi `footFollow`, xem mục 3) và Swim (giãn 1,438 → 1,439, nén 0,805 → 0,804, lệch ở chữ số thứ ba). Bảng từng clip: `TEST_REPORT_U6_compare.md`. Báo cáo theo model, prefab catalog, animation report: `reports_u6_sample/`.

## 3. Lỗi phát hiện và cách sửa

Mỗi lỗi được tìm bằng thí nghiệm một biến trước khi sửa. Không nới ngưỡng nào.

| Lỗi (lần chạy đầu) | Nguyên nhân | Sửa |
|---|---|---|
| Emberling Stretch giãn 1,653 | đuôi được nâng ngay ở gốc, kéo căng vùng mông | nâng đuôi từ đốt thứ hai trở đi (`tails[1:3]`): 1,198 |
| Sprout Drink giãn 1,637 (Neck/Head) | gần hết góc cúi dồn vào cổ | chia cho ngực: neck 0,40, head 0,30, chest 0,16 → 1,591 |
| Steamdrake Rest giãn 1,655 / nén 0,427, Bask 1,624 | (a) nhịp thở xoay Spine1, gốc của cả thân, nên hông nhấp nhô trên chân trụ; (b) Sleep chỉ đạt nhờ bước tránh xuyên đất đã thu biên độ còn 0,614, còn Rest/Bask hạ thân đủ 0,4 sải chân; (c) bàn chân trụ phẳng | không thở từ Spine1 khi rig có chân (cùng luật với Idle); serpentine `sleep.footFollow` 0,8; Rest/Bask `dropLegs` 0,3 → PASS |
| **Wake có thể giật ở đầu clip** (lần này chưa vượt ngưỡng nhưng có nguy cơ) | Wake tự tính hệ số tránh xuyên đất riêng; lần đầu trùng với Sleep (0,614) chỉ là ngẫu nhiên | Wake nội suy giữa Sleep **đã author** và Idle **đã author** (cùng hệ số và độ nâng), không tự giảm biên độ. QA kiểm chuyển tiếp: ≤ 0,217° |
| Wake: bàn chân trượt khi đứng dậy | chân đổi vị trí trong lúc vẫn tính là chân trụ | chân nào dời chỗ thì nhấc theo cung nhỏ và không tính là chân trụ |
| Runtime cấp phát 32–110 B/rồng/frame | (a) chuỗi note ghép mỗi tick; (b) `enum.ToString()` khi tra clip; (c) closure của lambda bị trình biên dịch tối ưu đưa lên đầu `Behave`, cấp phát cả ở đường cũ | cache note; bảng tra theo enum; tách hàm `CreatePlanner` → **0 B/frame**. Test chạy ở build tối ưu (bản Debug không tái hiện được lỗi (c)) |

## 4. Runtime (Workstream C/D)

Mọi phần bên dưới không phụ thuộc Domain. Tương thích ngược:
- giá trị enum cũ không đổi;
- `intent = None` chạy đúng đường của U1-CHAR.5;
- target U5 bỏ qua intent;
- controller cũ không bị ghi đè.

| Thành phần | Nội dung |
|---|---|
| `DragonBehaviorPlanner` | intent → clip loài có, thay thế trung thực, Sleep → Wake → đích, one-shot không bị ngắt, biến thể Idle tất định theo UID, quay tại chỗ ≥ 60°/s thì bước, Recover nhịp chậm |
| `DragonPersonalityPresentation` | Playful / Calm / Curious / Shy: tempo, biến thể Idle, phản hồi Pet/Call/Feed, head look (Shy liếc rồi quay đi). Chỉ là trình diễn, không đổi luật Domain |
| `DragonHeadLook` / `HeadLookSolver` | look-at sau Animator, chia góc cổ/đầu, giới hạn 55°/25°, mượt; bỏ qua mục tiêu sau lưng; chỉ chạy cho rồng gần, cập nhật đủ nhịp |
| `DragonBlink` / `BlinkScheduler` | chỉ chạy khi có blend shape mí mắt, nhắm khi ngủ. Mesh SAMPLE và Hunyuan thường không có, nên không chớp mắt và không giả bằng bone |
| `SanctuaryDragonSync` | các trường tuỳ chọn `intent`, `personality`, `turnRateDegPerSec`, `lookTarget`; `Sync(frames, dt)`; `React(uid, …)`; report thêm `triggered`/`substituted` |
| Animator builder / importer | state `Rest`, `Bask` và Drink…Curious; tham số `Tempo`; `clipLengths` trong catalog; prefab mới có HeadLook/Blink |
| Animation Lab | bảng Behaviour preview: intent, tính cách, Pet/Call/Feed, chạy planner thật |
| Stress harness | `behaviourIntents = true`: chạy qua sync, có intent, tính cách, look-at; ghi `oneShotsStarted`, `substitutions` |

## 5. Hiệu năng

**Đo được, nhưng chỉ phần C# thuần.**
- Lệnh: `dotnet run -c Release --project Assets/Dravio/Character/Tests/DotNet~/Bench/Dravio.Character.Bench.csproj`, trên Xeon 2,1 GHz, 4 nhân, .NET 8.
- Không gồm Animator, skinning, render. **Không phải thiết bị di động.**

| Rồng | Có intent (sync + planner) | Không intent (đường U5) | Cấp phát |
|---|---|---|---|
| 5 | 2,0 µs/frame | 1,3 µs/frame | 0 B/frame |
| 15 | 6,1 µs/frame | 4,6 µs/frame | 0 B/frame |
| 30 | 11,5 µs/frame | 7,7 µs/frame | 0 B/frame |

**Ước tính tĩnh** (tam giác, skinned mesh, Animator cần đánh giá): `reports_u6_sample/performance-budget.md`. Thêm clip không đổi số tam giác hay bone.

Head look và blink chỉ chạy cho rồng gần, cập nhật đủ nhịp; mỗi rồng tối đa 2 phép xoay bone mỗi frame.

**Chưa đo:** FPS, draw call, bộ nhớ trên thiết bị với 5/15/30 rồng. Chạy `Scenes/DragonStress.unity` với `behaviourIntents = true`.

## 6. Không hồi quy

| Kiểm tra | Kết quả |
|---|---|
| Ngưỡng `character-budgets.json` | không đổi |
| Hình thái sinh học | không đổi; clip di chuyển đúng bằng locomotion canonical (có test) |
| Clip cũ | giữ nguyên số liệu, trừ Steamdrake: Sleep giãn 1,503 → 1,467 và nén 0,539 → 0,615 (tốt hơn); Swim lệch ở chữ số thứ ba |
| Giá trị int của Animator | không đổi (có test) |
| Domain, Persistence, Environment, Fantasy HUD, Scene Open Game | không sửa file nào |
| Asset đã duyệt, GLB nguồn | không bị ghi đè; SHA-256 không đổi |

## 7. Chạy trên GLB thật tại máy bạn

```powershell
powershell -ExecutionPolicy Bypass -File scripts\art\character\run_u6_local.ps1 `
  -Source "B:\Dravio\dravio_16_dragons_3d_pack\images" `
  -Blender "C:\Program Files\Blender Foundation\Blender 5.3\blender.exe" `
  -OldWork "B:\Dravio\work_u5" -Work "B:\Dravio\work_u6" `
  -UnityOut "Assets\Dravio\Character\Imported" -Unity "C:\Program Files\Unity\Hub\Editor\6000.6.10f1\Editor\Unity.exe"
```

Gửi lại:
- `U6_before_after.md`
- `batch-report.md`
- cả thư mục `reports\`
- `qa_after\*.png`
- `unity\*.xml` và `unity\*.log`

Sau đó mở Animation Lab, xem từng clip và bảng Behaviour preview, rồi mới đưa tên nào vào `approved-assets.json`.

**Controller cũ:** controller đã tạo từ trước không bị ghi đè. Để có state và tham số `Tempo` mới, xoá `Assets/Dravio/Character/Animation/DragonBase_*.controller` rồi import lại manifest.

## 8. Chưa kiểm chứng / giới hạn

- **Unity 6000.6:** import, state Animator mới, override, `Tempo`, head look trên bone thật (trục bone sau khi import FBX), Animation Lab, stress trên thiết bị.
- **GLB thật:** chưa xử lý bằng rig v4.
- **Chất lượng nghệ thuật:** clip vẫn là procedural theo quy tắc. Animator cần duyệt bằng mắt, nhất là Stretch, Curious, Bask.
- **Chưa có:**
  - clip nằm xuống (vào Sleep chỉ crossfade);
  - clip quay tại chỗ riêng (dùng Walk);
  - Drink cho loài thuỷ sinh;
  - blink trên mesh không có blend shape.
- **Mặc định tính cách** (tempo, tần suất, phản hồi) cần art director duyệt.
- **`limbCount`:** Droplet, Zephyr, Steamdrake vẫn "auto"; Magma chưa có, nên chưa có rig.
