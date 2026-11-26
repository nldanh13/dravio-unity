# CHAR.9 — Production Assets & Playable Alpha: báo cáo

Nhánh `feat/char-9-production-alpha` từ `main` `42650fa` (đã gồm CHAR.8 qua PR #71; PR #65 đã đóng vì nằm trọn trong #71). Trạng thái từng asset: [DRAGON_ASSET_STATUS.json](DRAGON_ASSET_STATUS.json), [WORLD_ASSET_STATUS.json](WORLD_ASSET_STATUS.json).

Mức bằng chứng: **Blender verified** = chạy thật bằng Blender 5.3 (`bpy`) trong môi trường này; **Unity Editor / Android = NOT RUN** (không có Unity, không có thiết bị). Kết quả .NET CI không chứng minh model hay animation chạy trong Unity.

## Đã làm

| # | Việc | Kết quả | Bằng chứng |
|---|---|---|---|
| 1 | Hoàn tất bàn giao CHAR.8 | PR #71 merged (`42650fa`), PR #65 đóng | GitHub |
| 2 | **Gỡ blocker scene Origin**: `Assets/Dravio/Art/Models/EmberlingRefinedGLB.fbx` từ **GLB thật** `ArtSource/Dragons/images/01_emberling.glb` (sha256 `42bffef7…`) | Đủ 22 bone của hợp đồng `EmberlingAssetAudit`, take 445 frame (Idle 6 s, Walk 35/30 s, Eat 2 s, Doze 4 s, Happy 1,5 s), mesh `Emberling_Baby_Body_GLB` 24 000 tam giác, 4 ảnh hưởng/đỉnh, 0 đỉnh không trọng số, BaseColor 2048² nhúng | Blender verified (xuất → import lại → kiểm) |
| 3 | Sửa exporter cũ `emberling_glb_source.py` cho GLB thật | GLB thật tách đỉnh ở mọi đường nối UV/normal → bone heat trả 0/16 253 đỉnh có trọng số. Thêm hàn theo vị trí (UV giữ nguyên): 7 390 → 0 cạnh biên | Blender verified |
| 4 | So sánh rig cho model thật (cùng chỉ số anim_qa) | rig cũ (khớp hộp bao + heat): giãn 24–52, xuyên đất > 100 % chiều cao → **loại**. Rig v7 xuất theo hợp đồng: Idle 1,35 ✓, Happy 1,97, Walk 2,66, Eat 3,33, Doze 3,32, xuyên đất 0 | `scripts/art/character/reports_char9/emberling_refined_glb_deformation.json` |
| 5 | Công cụ mới | `export_emberling_legacy_contract.py` (rig v7 → hợp đồng generator), `measure_skin_deformation.py` | — |
| 6 | Theo dõi ArtSource trên `main` | không có GLB mới ngoài 16 rồng + cây (đã xử lý ở CHAR.8); 15 vật thể chỉ có ảnh | `git ls-tree origin/main ArtSource` |

## Chưa xong / trở ngại thật

- **Animation chưa đạt QA ở cả 16 loài** (ngưỡng giãn 1,6 không nới). Nguyên nhân hệ thống và kế hoạch: [ANIMATION_QA_REPORT.md](ANIMATION_QA_REPORT.md). Emberling được ưu tiên nhưng vẫn `NEEDS_FIX` (Walk/Eat/Doze/Happy).
- **48 biến thể giai đoạn: 0 model riêng.** Mỗi loài có một GLB `Unstaged`; tạo Baby/Juvenile/Adult cần thay đổi tỉ lệ có kiểm soát (đầu, chân, cánh, sừng) trên lưới thật + rig lại — chưa làm. Không phóng to đồng đều.
- **Trứng 16 loài, hiệu ứng nở, Sanctuary 17/20 công trình, 3 thế giới hoàn chỉnh theo concept**: chưa làm trong CHAR.9 (chỉ có mẫu trứng Aurorawing và 3 công trình mẫu từ CHAR.8). Kit môi trường hiện có là procedural (U1-ENV), đủ để sinh scene nhưng chưa đạt mức concept art.
- **Unity Editor / Android / stress / 20 phút gameplay: NOT RUN.** Không có Unity hay thiết bị trong môi trường này.
- Không tạo scene giả, không ghi đè scene hay asset có sẵn, không sửa Domain/Persistence/Save/UI/package identity.

## Việc chủ dự án / AI 1 làm tiếp

Xem [UNITY_INTEGRATION_GUIDE.md](UNITY_INTEGRATION_GUIDE.md) và [ALPHA_ASSET_HANDOFF.md](ALPHA_ASSET_HANDOFF.md).
