# U1-CHAR.3: Nhập tài nguyên hàng loạt và rồng sống

Hướng dẫn nhận 16 mô hình rồng và các prop môi trường sinh bằng Hunyuan3D-2.1 (hoặc công cụ khác xuất GLB), đưa vào Unity và hiển thị trong game.

> **Trạng thái:** toàn bộ chuỗi Python/Blender đã chạy trên **GLB mẫu tự sinh** (`SAMPLE_*`, `make_sample_library.py`). **Chưa có GLB Hunyuan thật nào được xử lý.** Code Unity mới chỉ được kiểm tra kiểu bằng stub và unit test logic thuần bằng mono. Code này chưa được biên dịch hay chạy trong Unity 6000.6, nên chưa có số liệu draw call, FPS hay bộ nhớ thật. Kết quả chạy thử nằm ở `scripts/art/character/TEST_REPORT_U3.md`.

## 1. Đặt tên file nguồn

Batch đọc tên file (không phân biệt hoa thường, tách theo `_ - . khoảng trắng`):

| Tên file | Nhận diện |
|---|---|
| Có từ khoá prop (`statue`, `tuong`, `arch`, `gate`, `island`, `fountain`, `tree`, `coral`, `crystal`, `rock`, `flower`, `chest`, `nest`, `basket`, `fruit`, …) | Prop tĩnh, nhóm theo `propCategories` trong `Data/character-budgets.json`. Từ khoá prop được xét trước, nên `emberling_statue` là tượng chứ không phải rồng. |
| Có đúng một species ID canonical (`emberling`, `zephyr`, …) | Rồng. Giai đoạn lấy từ token `Baby` / `Juvenile` / `Adult`, nếu không có thì là `Unstaged`. |
| Không có cả hai, hoặc có hai species ID | `unrecognized`, ghi vào báo cáo, không xử lý. |

Ví dụ: `hunyuan_emberling_Adult_v2.glb` → rồng `emberling`, giai đoạn Adult. `prop_treasure_chest.glb` → prop `chest`, id `treasure_chest`.

## 2. Chạy batch

```bash
python scripts/art/character/batch_import.py <thư_mục_GLB> <thư_mục_làm_việc> --blender "<blender.exe>" \
    --unity-out Assets/Dravio/Character/Imported --overrides <asset-overrides.json>
# hoặc --bpy-module thay cho --blender
```

- **Không ghi đè nguồn:** GLB nguồn chỉ được đọc. SHA-256 được ghi trước và sau khi xử lý, và batch dừng nếu nguồn bị thay đổi trong lúc chạy. `--unity-out` không được nằm trong thư mục nguồn.
- **Chỉ xử lý lại file đổi hash:** `<thư_mục_làm_việc>/batch-state.json` lưu dấu vân tay gồm SHA-256 của nguồn, phiên bản pipeline và override của tài nguyên đó. File không đổi thì báo `unchanged`. Dùng `--force` để chạy lại tất cả.
- **Chuỗi xử lý cho rồng:**
  1. `inspect`
  2. `prepare`: hàn seam, UV, bake texture, giảm trước xuống mục tiêu LOD0 24k
  3. `rig_archetype`
  4. `build_lods`, kiểm tra trọng số ở giữa mỗi clip
  5. `inspect` FBX
  6. `validate`
- **Chuỗi xử lý cho prop:**
  1. `inspect`
  2. `prepare`
  3. `build_prop`
  4. `inspect`
  5. `validate` theo profile `prop_small` / `prop_large`
- **Báo cáo:** `batch-report.json` và `batch-report.md` gồm:
  - trạng thái từng file: `ok`, `ok_with_notes`, `validation_failed`, `failed`, `unchanged`, `unrecognized`
  - lỗi của bước hỏng
  - loài chưa có mô hình
  - cặp loài/giai đoạn còn thiếu trên tổng 48
  - nhóm prop còn thiếu
  - dữ liệu cần art director bổ sung
- **Đầu ra cho Unity:** `Imported/Dragons/<species>/<stage>/` và `Imported/Environment/<category>/` (FBX, texture, rig.json hoặc prop.json, validation), kèm `Imported/import-manifest.json`.

### Dữ liệu GDD chưa xác định

`Data/asset-overrides.example.json` liệt kê những giá trị mà dữ liệu canonical không nói. Tất cả để `null`, không có giá trị đoán.

- **`limbCount`:** 11 loài biết đi bằng chân nhưng không ghi số chân: droplet, zephyr, steamdrake, magma, sparkwing, mossback, blossom, solaris, aurorawing, celestia, gaia. Khi chưa có giá trị, batch **không rig** loài đó, vẫn tạo LOD tĩnh và ghi rõ lý do vào báo cáo.
- **`yawOffset`:** dùng khi mô hình sinh ra không quay mặt về −Y.
- **`props.<id>.height`:** chiều cao thật tính bằng mét. Khi chưa có, prop giữ đơn vị gốc của công cụ sinh, và Unity đánh dấu `sizeConfirmed = false`.

## 3. Archetype rig và animation (`Data/dragon-archetypes.json`)

Bảng archetype được sinh bằng `build_archetypes.py` từ `locomotion`, `bodyStyle` và mô tả hình dáng canonical. Xem chi tiết từng loài ở `DRAGON_ARCHETYPES.md`. **Rig của Emberling không dùng chung cho các loài khác.** Mỗi mô hình có skeleton riêng, dựng từ hình khối của chính nó theo template của archetype.

| Archetype | Quy tắc | Template | Loài |
|---|---|---|---|
| quadruped | biết đi và mô tả ghi "bốn chân" | Root, Pelvis, Spine, Chest, Neck, Head, Tail1-3, 4 chân × (Upper, Lower, Foot) | emberling, sprout, amberhorn |
| walker | biết đi nhưng không rõ số chân | chưa có template; khi `limbCount = 4` thì dùng quadruped | droplet, magma, mossback, gaia |
| winged | có `fly` | thân quadruped, chân chỉ thêm khi có `limbCount`, cộng 2 × (WingUpper, WingFore, WingTip) | zephyr, sparkwing, blossom, solaris, aurorawing, celestia |
| serpentine | `bodyStyle = Serpent` | Spine1-8, Tail1-4, Neck, Head | steamdrake, leviathan (biến thể dưới nước) |
| aquatic | không có `walk` | Spine1-4, Tail1-3, Fin_L/R, TailFin | frostfin |

- **Clip:**
  - mọi loài đều có `Idle`, `Eat`, `Sleep`, `Happy`
  - `Walk` và `Run` chỉ khi loài biết đi
  - `Swim` chỉ khi biết bơi
  - `Fly` chỉ khi biết bay
  - `Burrow` chỉ khi biết đào
- **Anchor VFX:** `VFX_Mouth`, `VFX_TailTip`, `VFX_Back`, `VFX_Ground` là bone không có trọng số.
- **Foot IK:** mỗi chân là một chuỗi `upper → lower → foot`, ghi trong `rig.json` (`ikChains`).
- **Kiểm tra biến dạng:** đo tỉ lệ độ dài cạnh mesh ở 4 khung hình mỗi clip. Ngưỡng là p99.9 ≤ 1,6 (giãn) và p0.1 ≥ 0,45 (nén). Clip vượt ngưỡng được ghi `deformationOk = false` và đưa vào báo cáo, không bị ẩn đi.
- **Giới hạn:** bone được đặt theo hình khối và chuyển động là procedural placeholder. Animator vẫn phải duyệt và thường phải chỉnh tay trong Blender trước khi dùng thật.

## 4. Nhập vào Unity

1. Đưa thư mục `Imported/` vào `Assets/Dravio/Character/Imported/`. Preset import tự áp dụng ở lần import đầu: rồng là Generic, prop không có animation và avatar, normal map, ASTC.
2. Chạy **Dravio ▸ Character ▸ Import Batch Manifest**, hoặc `-executeMethod Dravio.Character.Editor.LivingAssetBatchImporter.RunDefault`. Với mỗi rồng, menu này:
   - cắt clip từ take FBX theo frame trong manifest, tương đối với frame đầu của take
   - tạo material URP
   - tạo prefab LODGroup gắn `DragonVisualInstance`
   - tạo AnimatorController **theo archetype**: `Animation/DragonBase_<archetype>.controller`, chỉ gồm state cho clip mà archetype có, thời gian blend riêng cho từng archetype
   - tạo AnimatorOverrideController riêng cho mô hình
   - gắn `DragonFootGrounding` nếu có `ikChains`
   - đăng ký vào `Data/DragonVisualCatalog.asset`
3. Với mỗi prop, menu này:
   - tạo prefab ở `Prefabs/Environment/` gồm LODGroup theo profile và collider đơn giản: Box lấy từ bounds của LOD0, MeshCollider convex hoặc MeshCollider tĩnh lấy từ mesh `_Collider`
   - đặt cờ static, không có Animator
   - gắn `EnvironmentPropInfo` (kích thước, bán kính chiếm chỗ)
   - đăng ký vào `Data/EnvironmentPropCatalog.asset`
4. Material, prefab và controller đã tồn tại thì **không bị ghi đè**, chỉ được dùng lại và ghi vào báo cáo. Báo cáo nằm ở `Imported/unity-import-report.txt`.

Animator gồm các tham số sau:

- `Locomotion` (int): Idle 0, Walk 1, Run 2, Swim 3, Fly 4, Burrow 5, Sleep 6
- `Action` (int): 0 là không có, Eat 1, Happy 2
- `Speed` (float)
- `PlaybackRate` (float)

Chuyển trạng thái được dựng như sau:

- Any State chuyển sang state locomotion khi `Locomotion` bằng giá trị của state đó và `Action = 0`.
- Any State chuyển sang Eat hoặc Happy khi `Action` = 1 hoặc 2.
- Runtime trả `Action` về 0 khi hết độ dài clip, và Animator quay lại locomotion hiện tại.

## 5. Runtime (`Dravio.Character.Runtime`, không phụ thuộc Domain/Persistence/HUD)

```csharp
var mgr = GetComponent<DragonVisualManager>();              // catalog + (tuỳ chọn) PhenotypeProfile
mgr.Spawn(new DragonVisualSpawn { uid = dragon.Uid, speciesId = dragon.SpeciesId,
    stage = DragonVisualStage.Juvenile,                      // tầng tích hợp tự ánh xạ từ GrowthStage của Domain
    genotype = new[] { new GenotypeEntry("pigment", "Rr") }, variantId = null,
    state = DragonVisualState.At(pos, rot) });
mgr.ApplyState(uid, new DragonVisualState { locomotion = DragonLocomotion.Walk, speed = 1.2f, position = p, rotation = r });
mgr.Trigger(uid, DragonInteraction.Eat);
mgr.SetStage(uid, DragonVisualStage.Adult);                  // lớn lên: đổi mô hình, giữ trạng thái
mgr.TryGetAnchor(uid, "VFX_Mouth", out var mouth);           // đặt VFX
mgr.Despawn(uid);
```

- **Nhiều cá thể cùng loài:** mỗi cá thể được định danh bằng UID. Prefab được lấy từ pool theo loài và giai đoạn.
- **Khi thiếu mô hình:** catalog chọn đúng giai đoạn, nếu không có thì lấy mô hình `Unstaged`, nếu vẫn không có thì lấy giai đoạn gần nhất (có thể tắt `allowOtherStageFallback`). Mọi lần phải dùng mô hình thay thế đều phát sự kiện `Warning`.
- **Không giả khả năng:** khi yêu cầu `Fly` cho loài không bay, runtime hiện `Idle` và cảnh báo một lần. Khi yêu cầu `Run` mà loài không có clip Run thì dùng `Walk`.
- **Genotype và variant:** `PhenotypeProfile` để trống theo mặc định, vì GDD chưa định nghĩa màu hay họa tiết cho từng allele. Art điền luật vào profile, khi đó các giá trị được áp bằng MaterialPropertyBlock. Block này chỉ được gắn khi có luật khớp, để dragon không có biến đổi vẫn giữ SRP Batcher.
- **Hiệu năng:**
  - **Pool:** prefab được tái sử dụng qua pool.
  - **Animator:** `cullingMode = CullUpdateTransforms` và `keepAnimatorStateOnDisable`.
  - **`UpdateRatePolicy`:** dragon gần và trong khung hình cập nhật mỗi frame. Dragon xa cập nhật 2 frame một lần, dragon ngoài khung hình 8 frame một lần, và các cá thể lệch pha nhau để không dồn vào cùng một frame. Quá 80 m thì không cập nhật.
  - **LOD:** mọi prefab có LODGroup.
  - **Foot IK:** chỉ chạy khi dragon ở gần, cập nhật mỗi frame và đang ở trạng thái mặt đất (Idle, Walk, Run).

## 6. Stress test

**Dravio ▸ Character ▸ Create Stress Test Scene** tạo `Scenes/DragonStress.unity`. Đây là scene riêng, không phải scene chính và không được thêm vào Build Settings. Nhấn Play, `DragonStressHarness` lần lượt chạy 5, 15 rồi 30 dragon, mỗi lần khởi động 3 giây rồi đo 10 giây. Các chỉ số đo được:

- FPS trung bình và 1 % low
- frame time
- draw calls, SetPass, tam giác, đỉnh (qua `ProfilerRecorder`)
- bộ nhớ Total, GC và System
- số Animator được cập nhật

Kết quả được ghi vào JSON trong `Application.persistentDataPath`. Bộ đếm render chỉ có trong Editor và bản Development, nên muốn có số liệu thiết bị thì phải build Development chỉ gồm scene này.

## 7. Prop môi trường

`build_prop.py`:

- **Pivot:** đặt ở đáy giữa.
- **Tỉ lệ:** giữ nguyên, trừ khi có `--height`.
- **LOD theo profile:**
  - `prop_small` là 2.500 / 900 / 300 tam giác
  - `prop_large` là 10.000 / 4.000 / 1.200
  - Đây là giá trị mặc định đề xuất, GDD chưa có số.
- **Collider:**
  - `box`: chỉ ghi metadata
  - `convex`: convex hull tối đa 250 tam giác
  - `mesh`: mesh tĩnh tối đa 1.000 tam giác, cho cổng vòm, đảo bay, cây
- **Metadata kích thước:** x, y, z, chiều cao, bán kính chiếm chỗ.
- **Không rig:** từ chối nguồn có armature.
- **Nhóm prop:**
  - dragon_statue, arch, floating_island, fountain, tree dùng profile `prop_large`
  - coral, crystal, rock, flower, chest, nest, fruit_basket dùng profile `prop_small`
