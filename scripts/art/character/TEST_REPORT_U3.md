# U1-CHAR.3: Báo cáo kiểm thử

Môi trường: Linux, Python 3.13, Blender 5.3 dùng module `bpy`, mono `mcs`. **Không có Unity Editor**, nên không chạy được Unity 6000.6.10f1. **Không có GLB Hunyuan3D thật.** Mọi số liệu dưới đây là từ GLB mẫu tự sinh `SAMPLE_*` (`make_sample_library.py`), mô phỏng đầu ra của công cụ sinh: lưới dày, UV, texture 512 và một mảnh lưới rời.

## Kiểm thử tự động

| Kiểm thử | Kết quả |
|---|---|
| `python -m unittest discover -s scripts/art/character/tests` | 30/30 pass: validator, spec, archetype, nhận diện tên file |
| `build_archetypes.py --check` | dữ liệu sinh ra khớp với file đã commit |
| `run_selftest.py --bpy-module` (pipeline U1-CHAR cũ, sau khi sửa prepare/LOD/validate) | ALL OK, không hồi quy |
| C# runtime và editor, biên dịch bằng `mcs` với stub API Unity viết tay | biên dịch được. Đây chỉ là kiểm tra kiểu, **không phải biên dịch Unity thật** |
| `Tests/LivingDragonRuntimeTests.cs` (NUnit), chạy bằng mono với shim NUnit | 7/7 pass: animation map, catalog, phenotype, pool, update policy |

## Batch trên 10 GLB mẫu (`--overrides` thử nghiệm: zephyr `limbCount = 4`, crystal_cluster `height = 0.6`, **chỉ dùng để thử, không phải dữ liệu GDD**)

| File | Nhận diện | Kết quả |
|---|---|---|
| SAMPLE_emberling_Baby | quadruped, 25 bone, clip Idle/Eat/Sleep/Happy/Walk/Run | PASS, biến dạng ok. 112 đỉnh của mảnh rời được gắn cứng vào bone gần nhất |
| SAMPLE_frostfin_Juvenile | aquatic, 17 bone, clip Idle/Eat/Sleep/Happy/Swim | PASS, biến dạng ok |
| SAMPLE_leviathan_Adult | serpentine (biến thể dưới nước), 19 bone | PASS. Clip **Swim vượt ngưỡng giãn** (p99.9 = 1,94 > 1,6) |
| SAMPLE_zephyr (không có token giai đoạn) | winged, Unstaged, 31 bone, có thêm Fly | PASS. **Eat 1,66, Sleep 1,93, Fly 2,81 vượt ngưỡng** |
| SAMPLE_magma_Baby | walker, thiếu `limbCount` | **không rig** (đúng thiết kế), chỉ tạo LOD tĩnh, PASS |
| SAMPLE_mystery_object | — | unrecognized, ghi vào báo cáo |
| SAMPLE_prop_crystal_cluster | crystal / prop_small | LOD 2500/900/300, convex 96 tam giác, cao 0,60 m |
| SAMPLE_prop_treasure_chest | chest / prop_small | LOD 2500/900/300, box. Kích thước chưa xác nhận |
| SAMPLE_prop_tree_ancient | tree / prop_large | LOD 10000/4000/1200, mesh collider 1000 tam giác |
| SAMPLE_prop_floating_island | floating_island / prop_large | LOD 10000/4000/1200, mesh collider 1000 tam giác |

- **LOD rồng:** 24k / 10k / 4k tam giác trên cả 5 mẫu, nằm trong ngân sách.
- **Cache theo hash:**
  - chạy lại cùng nguồn thì cả 9 file báo `unchanged`
  - thay nội dung một file thì chỉ file đó được xử lý lại
- **SHA-256 nguồn:** không đổi sau tất cả các lần chạy.
- **Lỗi tìm ra và đã sửa trong lúc thử:**
  - mesh `_Collider` của prop bị tính vào LOD0 và báo thiếu UV. Validator Python và Unity giờ bỏ qua mesh này.
  - trạng thái `validation_failed` trước đây bị gộp vào `ok_with_notes`. Giờ đã tách riêng.

## Chưa kiểm chứng

- Import FBX, cắt clip, AnimatorController/Override, prefab và catalog chưa chạy trong Unity 6000.6. Cần mở project và chạy **Dravio ▸ Character ▸ Import Batch Manifest**.
- Cảnh stress 5 / 15 / 30 rồng (`DragonStress.unity`) mới chỉ được viết code, **chưa chạy**. Chưa có số draw call, tam giác, bộ nhớ hay FPS.
- Foot IK: phép giải hai bone đã được kiểm bằng số trong Python, với 2.000 trường hợp ngẫu nhiên và sai số đầu chi ≤ 1e-14. Nó chưa được chạy trên rig thật trong Unity.
- Chất lượng animation là placeholder procedural, cần animator duyệt. Các clip vượt ngưỡng biến dạng nêu ở trên phải chỉnh biên độ hoặc trọng số.
- Ánh xạ genotype sang hình ảnh chưa có trong GDD, nên `PhenotypeProfile` để trống.
