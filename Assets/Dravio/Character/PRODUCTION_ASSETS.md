# CHAR.8 — Tài nguyên production: hướng dẫn tích hợp Unity cho AI 1

> Nguồn: `ArtSource/` (nhánh `feat/art-source-packs`, commit `0561a42`, 59 tệp, SHA-256 đã khớp manifest). Báo cáo QA: `scripts/art/character/TEST_REPORT_CHAR8_PRODUCTION.md`.
> **Trạng thái:** 16 rồng thật + cây Blossom Sky đã qua pipeline Blender (rig, clip, LOD, texture). **Animation chưa đạt QA** (biến dạng ở các clip gập chân) — dùng được cho tích hợp, hiển thị và đo hiệu năng, **chưa được nghiệm thu**. Không file nào được thêm vào `approved-assets.json`.
> Không đổi Domain, Persistence, Save Schema, luật sinh học. `ArtSource/` nằm ngoài `Assets/` nên Unity không import trực tiếp.

## 1. Tạo gói Unity (không commit sẵn vì 278 MB và còn tái tạo được)

Từ thư mục gốc repo, Blender 5.3 (hoặc module `bpy` 5.3):

```bash
python scripts/art/character/batch_import.py ArtSource/Dragons/images <work> --blender "<blender>" \
    --unity-out Assets/Dravio/Character/Imported \
    --overrides Assets/Dravio/Character/Data/asset-overrides.char8-production.json [--qa-images]
python scripts/art/character/batch_import.py ArtSource/NatureAndGameplay/foliage <work_tree> --blender "<blender>" \
    --unity-out Assets/Dravio/Character/Imported \
    --overrides Assets/Dravio/Character/Data/asset-overrides.char8-production.json
```

- Nguồn chỉ đọc, kiểm SHA-256 trước/sau; `<work>` không được nằm trong `ArtSource/`.
- ~6–7 phút mỗi rồng trên CPU 4 nhân (Cycles chỉ dùng cho ảnh QA).
- Đầu ra: `Imported/Dragons/<species>/Unstaged/` (`*_LODs.fbx`, `*_BaseColor.png`, `*_MetallicSmoothness.png`, `*_rig.json`, QA), `Imported/Environment/tree/`, `Imported/import-manifest.json` (bản đã sinh: `scripts/art/character/reports_char8_production/import-manifest.json`).

## 2. Import trong Unity 6000.6

1. Mở project, chờ import (preset: rồng Generic, prop không animation/avatar, ASTC).
2. **Dravio ▸ Character ▸ Import Batch Manifest** (hoặc batch mode `-executeMethod Dravio.Character.Editor.CharacterBatchCli.RunImport`):
   - cắt clip theo frame trong manifest;
   - tạo material URP, prefab LODGroup `Prefabs/DRG_<species>_Unstaged_Rigged_LODGroup.prefab` có `DragonVisualInstance`, `DragonFootGrounding` (loài có chân);
   - AnimatorController theo archetype + override riêng; đăng ký `Data/DragonVisualCatalog.asset`;
   - prop cây → `Prefabs/Environment/PRP_02_tree_blossom_sky*.prefab` + `EnvironmentPropCatalog.asset`.
   Prefab / material / controller đã có thì **không ghi đè**.
3. Kiểm trong **Dravio ▸ Character ▸ Animation Lab**: chọn loài, xem các clip có cảnh báo trong `prefab-catalog.md` (Sleep, Rest, Bask, Run, Swim…).
4. Chỉ khi art director đồng ý mới thêm tên vào `Imported/approved-assets.json` (từ đó batch không bao giờ ghi đè).

## 3. Catalog

| Loài | Prefab | Archetype | Chân | Clip | Ghi chú tích hợp |
|---|---|---|---|---|---|
| emberling | `DRG_emberling_Unstaged` | quadruped | 4 | 15 | thay prototype Emberling cũ khi được duyệt (prototype vẫn giữ) |
| droplet | `DRG_droplet_Unstaged` | quadruped | 4 (đo) | 21 | Swim, WaterEnter/Exit |
| sprout | `DRG_sprout_Unstaged` | quadruped | 4 | 19 | Burrow; nguồn quay ngang, đã xoay +90° qua override |
| zephyr | `DRG_zephyr_Unstaged` | winged | 4 (đo) | 20 | Perch (chân đậu) |
| steamdrake | `DRG_steamdrake_Unstaged` | serpentine có chân | 4 (đo) | 21 | — |
| magma | `DRG_magma_Unstaged` | quadruped | 4 (đo) | 19 | Burrow |
| sparkwing | `DRG_sparkwing_Unstaged` | winged | 4 (đo) | 19 | — |
| frostfin | `DRG_frostfin_Unstaged` | aquatic | 0 | 13 | không đi, không bay |
| mossback | `DRG_mossback_Unstaged` | quadruped | 4 (đo) | 21 | — |
| blossom | `DRG_blossom_Unstaged` | winged | 4 (đo) | 20 | Perch |
| solaris | `DRG_solaris_Unstaged` | winged | 4 (đo) | 19 | — |
| leviathan | `DRG_leviathan_Unstaged` | serpentine (nước) | 0 | 13 | — |
| amberhorn | `DRG_amberhorn_Unstaged` | quadruped | 4 | 19 | Burrow |
| **aurorawing** | `DRG_aurorawing_Unstaged` | winged | 4 (đo) | 19 | trứng thưởng 15 chuyến Thiên Giới (luật Domain chờ duyệt, PR U2.12) |
| celestia | `DRG_celestia_Unstaged` | winged | 4 (đo) | 19 | — |
| gaia | `DRG_gaia_Unstaged` | quadruped | 4 (đo) | 21 | — |

"4 (đo)" = `limbCount: "auto"`, đo được 4 chân độ tin cậy cao; dữ liệu canonical chưa ghi số chân → art director xác nhận. Danh sách clip đầy đủ, LOD, bone: `reports_char8_production/prefab-catalog.md`; motion (tốc độ, độ lệch root, sự kiện): `motion-report.md`.

Giai đoạn: mọi model là `Unstaged`. `DragonVisualManager` tra đúng giai đoạn → `Unstaged` → giai đoạn gần nhất, nên Baby / Juvenile / Adult đều hiển thị model này; mỗi lần dùng model thay thế, runtime phát sự kiện `Warning` (đúng thiết kế, LIVING_DRAGONS.md §5). Khi có model theo giai đoạn, đặt tên `NN_<species>_Baby.glb`… và chạy lại batch.

## 4. Dùng tạm khi animation chưa đạt

- An toàn: Idle, LookAround, Curious, Greet, Nuzzle, Happy (đa số model đạt), di chuyển ở xa camera.
- Tránh cận cảnh: Sleep, Rest, Bask, Run, Swim, Drink, Stretch (giãn lưới ở háng/vai).
- Không đổi logic game theo animation; planner chỉ chọn clip loài có (Fly cho loài không bay → Idle + cảnh báo).
