# CHAR.9 — Unity integration guide (cho AI 1)

Unity 6000.6.10f1. **Chưa chạy trong Unity** — các bước dưới đây là đường đi đã đối chiếu với code generator trên `feat/u5-6-player-journey`, không phải kết quả đã kiểm.

## 1. Scene Origin gameplay (gỡ blocker U5.9)

1. Kéo `main` hoặc nhánh `feat/char-9-production-alpha`. File mới: `Assets/Dravio/Art/Models/EmberlingRefinedGLB.fbx` (real GLB, rig v7 theo hợp đồng cũ; `.meta` do Unity sinh — commit sau khi import).
2. Chọn FBX → Inspector Model ▸ Max Texture Size 1024, Android ASTC 6×6 (fallback ETC2 nếu thiết bị yêu cầu).
3. Dùng menu generator hiện có của `EmberlingModelPipeline` (playable) để tạo `Assets/Dravio/Scenes/OriginLongTermGameplay.unity`; generator tự dựng môi trường Origin từ kit có sẵn nếu chưa có scene môi trường. Không ghi đè scene đã có. Generator cấu hình importer (Generic, cắt 5 clip) ở lần đầu.
4. **Dravio ▸ Character ▸ Validate Refined GLB FBX** (`EmberlingAssetAudit`): mong đợi PASS cấu trúc (22 bone, 5 clip đúng thời lượng, UV0). Audit chỉ kiểm cấu trúc, không kiểm biến dạng.
5. Ocean/Sky: **Dravio ▸ Environment** (Ocean / Sky build) → `Environment/Scenes/Ocean_Environment.unity`, `Sky_Environment.unity` từ kit đã có trên `main`.
6. **Dravio ▸ Worlds ▸ Prepare Connected Worlds**, rồi `python scripts/qa/check_alpha_build.py`, rồi **Dravio ▸ Alpha ▸ Build Android Development APK**.

## 2. 16 rồng production (DragonVisualCatalog)

Theo `Assets/Dravio/Character/PRODUCTION_ASSETS.md`: chạy `batch_import.py` trên `ArtSource/Dragons/images` với `--unity-out Assets/Dravio/Character/Imported` và `asset-overrides.char8-production.json`, rồi **Dravio ▸ Character ▸ Import Batch Manifest**. Tạo prefab `Prefabs/DRG_<species>_Unstaged_Rigged_LODGroup.prefab`, AnimatorController theo archetype, đăng ký `Data/DragonVisualCatalog.asset`. Gói không commit (≈ 278 MB, tái tạo được).

## 3. Sanctuary

`Assets/Dravio/Environment/SANCTUARY_PREFABS.md`: **Dravio ▸ Environment ▸ Sanctuary ▸ Build Project Style-Sample Prefabs** (3 mẫu `PF_SANC_*`, anchor `ANCHOR_*`, không giá/buff).

## 4. Giới hạn khi chơi Alpha

Emberling (`EmberlingRefinedGLB.fbx`): đạt QA trong Blender từ CHAR.9b (5 clip hợp đồng), Unity chưa kiểm — xem `Docs/char9b/CHAR9B_EMBERLING_REPORT.md` §4. 15 loài khác: animation chưa đạt QA, tránh cận cảnh Sleep/Rest/Bask/Run/Eat. Không đổi logic game theo animation.
