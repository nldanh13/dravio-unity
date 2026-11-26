# Ngân sách hiệu năng (ước tính tĩnh)

**Không phải số đo trên thiết bị.** Tính từ LOD trung bình của 3 model rig trong catalog (24000/10000/4000 tris), 32 bone/model, 1 material/model, và mặc định `UpdateRatePolicy` (gần ≤ 12.0 m mỗi frame, xa mỗi 2 frame, ngoài màn hình mỗi 8 frame, > 80.0 m không đánh giá). Phân bố giả định: 20 % gần, 50 % giữa, 20 % xa, 10 % ngoài màn hình.

| Số rồng | Tam giác vẽ | Skinned mesh vẽ | Animator đánh giá / frame (TB) | Bone đánh giá / frame (TB) | Foot IK (gần, mặt đất) |
|---|---|---|---|---|---|
| 5 | 53,000 | 4 | 2.8 | 90 | ≤ 1 |
| 15 | 159,000 | 14 | 8.4 | 270 | ≤ 3 |
| 30 | 318,000 | 27 | 16.9 | 540 | ≤ 6 |

## Dữ liệu animation và skinning (U1-CHAR.8, mobile)

Kích thước khi bake (trước khi Unity giảm keyframe), với 4 float quaternion mỗi bone và vị trí chỉ ở bone Root. Importer đặt nén `Optimal` (sai số 0,5) cho lần import đầu. Skinning: LOD0 4 bone/đỉnh, LOD1 2, LOD2 1. Shaper, head look và biểu cảm chỉ chạy ≤ 12 m; chớp mắt ≤ 6 m.

| Model | Khung | Bone | Float/khung | MB khi bake |
|---|---|---|---|---|
| DRG_aurorawing_Adult | 1340 | 32 | 131 | 0.702 |
| DRG_aurorawing_Baby | 1328 | 32 | 131 | 0.696 |
| DRG_aurorawing_Juvenile | 1334 | 32 | 131 | 0.699 |

Đo thật: mở `Scenes/DragonStress.unity` (menu **Dravio ▸ Character ▸ Create Stress Test Scene**) trên thiết bị, ghi FPS, `EvaluatedLastFrame`, `VisibleLastFrame`, `PoolCreated/Reused/Destroyed` của `DragonVisualManager` và Profiler (Animator.Update, SkinnedMesh) cho 5/15/30 rồng.
