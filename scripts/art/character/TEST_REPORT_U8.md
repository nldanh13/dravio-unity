# U1-CHAR.8: tương tác đôi, biểu cảm, địa hình, sự kiện chuyển động, mobile — báo cáo kiểm thử

Hợp đồng và luật trình diễn: [`Assets/Dravio/Character/SOCIAL_ANIMATION.md`](../../../Assets/Dravio/Character/SOCIAL_ANIMATION.md).

## Phạm vi bằng chứng

- **Môi trường:** Linux, Blender 5.3 (`bpy`), .NET 8.
- **Không có Unity, không có 7 GLB thật.** Mọi số liệu đều trên **SAMPLE**, cùng 13 file của U1-CHAR.7.
- **Chưa xem trên Unity:** tương tác đôi, biểu cảm, nghiêng thân theo dốc, `SyncPhase` và skinning theo LOD.
- **Không model thật nào được nghiệm thu.**

## 1. Kiểm thử tự động

| Kiểm thử | Kết quả |
|---|---|
| Python `unittest` | **77/77**: thêm Nuzzle cho mọi loài ở cuối danh sách clip; manifest có event và mực nước |
| `build_archetypes.py --check`, `build_species_spec.py --check` | khớp |
| `dotnet test` (Character) | **54/54**: thêm 15 test trong `SocialTests.cs` |
| `dotnet build TypeCheck` (stub, không phải Unity) | Manager, Instance, Shaper, HeadLook, Blink, **FootGrounding** (giờ được type-check) đều biên dịch được |
| 15 cặp (30 rồng) đang diễn | **0 B/frame** |
| Benchmark | 30 rồng: 28,3 µs/frame, 0 B/frame (lần đo U1-CHAR.7b: 18,1 µs/frame). Đo cùng máy nhưng nhiễu giữa các lần chạy lớn; số này gồm cả bước lập kế hoạch tương tác đôi khi không có cặp nào |
| GLB nguồn | SHA-256 không đổi |

## 2. QA (SAMPLE, ngưỡng không đổi)

- **QA:** 9/9 model rig **PASS**, không cảnh báo.
- **Clip cũ:** không thay đổi số liệu nào (so từng clip với rig v6). Chỉ thêm **Nuzzle**: độ giãn P99,9 từ 1,094 đến 1,139, ngưỡng 1,6.
- **Chuyển tiếp:** vẫn ≤ 0,217°.
- **Sự kiện:**
  - Zephyr, Sparkwing: `TakeOff.liftOff` 0,323, `Land.touchDown` 0,6.
  - Droplet, Steamdrake: `WaterEnter.contact` và `WaterExit.leave` 0,5.
- **Mực nước khi bơi nổi:**
  - Droplet: 0,577 m, đầu nhô 0,165 m.
  - Steamdrake: 0,185 m, đầu nhô 0,108 m.
- **Dữ liệu animation khi bake:** 0,31 đến 0,76 MB mỗi model, chi tiết ở `reports_u8_sample/performance-budget.md`.
- **Chỉ khoá vị trí ở bone Root:** FBX không nhỏ đi, vì exporter vẫn bake mọi kênh. Lợi ích thật nằm ở nén `Optimal` khi Unity import, hiện chưa đo được.

## 3. Lỗi gặp và cách sửa

| Lỗi | Sửa |
|---|---|
| Droplet và Steamdrake bị rig lỗi ở lần batch đầu: landmark là mảng numpy, nên `lm.get(...) and ...` không xét được giá trị đúng/sai | so `is not None`. Chạy lại hai model: PASS |
| Foot IK cấp phát hai mảng mỗi frame | dùng mảng cache |

## 4. Chưa kiểm chứng / chờ quyết định

- **Unity:**
  - chiều xoay roll khi nghiêng thân theo dốc;
  - nghiêng đầu và vẫy đuôi trên bone thật;
  - `SyncPhase` (Animator.Play với normalized time);
  - skinning Bone1/2/4 theo LOD;
  - nén animation lúc import.
- **Art director:**
  - độ trễ đáp 0,3 s;
  - nhịp chơi;
  - khoảng cách giữa hai con;
  - bảng biểu cảm;
  - tỉ lệ thân theo dốc 60 %;
  - clip Nuzzle (procedural).
- **Chạy trên máy bạn:** dùng `run_u7_local.ps1` như trước (pipeline hiện là rig v7), rồi chạy Stress scene với `behaviourIntents` + `socialPairs`.
