# Ngân sách hiệu năng (ước tính tĩnh)

**Không phải số đo trên thiết bị.** Tính từ LOD trung bình của 9 model rig trong catalog (24000/10000/4000 tris), 26 bone/model, 1 material/model, và mặc định `UpdateRatePolicy` (gần ≤ 12.0 m mỗi frame, xa mỗi 2 frame, ngoài màn hình mỗi 8 frame, > 80.0 m không đánh giá). Phân bố giả định: 20 % gần, 50 % giữa, 20 % xa, 10 % ngoài màn hình.

| Số rồng | Tam giác vẽ | Skinned mesh vẽ | Animator đánh giá / frame (TB) | Bone đánh giá / frame (TB) | Foot IK (gần, mặt đất) |
|---|---|---|---|---|---|
| 5 | 53,000 | 4 | 2.8 | 74 | ≤ 1 |
| 15 | 158,999 | 14 | 8.4 | 222 | ≤ 3 |
| 30 | 317,997 | 27 | 16.9 | 444 | ≤ 6 |

## Dữ liệu animation và skinning (U1-CHAR.8, mobile)

Kích thước khi bake (trước khi Unity giảm keyframe), với 4 float quaternion mỗi bone và vị trí chỉ ở bone Root. Importer đặt nén `Optimal` (sai số 0,5) cho lần import đầu. Skinning: LOD0 4 bone/đỉnh, LOD1 2, LOD2 1. Shaper, head look và biểu cảm chỉ chạy ≤ 12 m; chớp mắt ≤ 6 m.

| Model | Khung | Bone | Float/khung | MB khi bake |
|---|---|---|---|---|
| DRG_droplet_Unstaged | 1573 | 28 | 115 | 0.724 |
| DRG_emberling_Unstaged | 1159 | 26 | 107 | 0.496 |
| DRG_frostfin_Juvenile | 1078 | 17 | 71 | 0.306 |
| DRG_leviathan_Adult | 1078 | 19 | 79 | 0.341 |
| DRG_magma_Unstaged | 1388 | 26 | 107 | 0.594 |
| DRG_sparkwing_Unstaged | 1335 | 32 | 131 | 0.700 |
| DRG_sprout_Unstaged | 1388 | 26 | 107 | 0.594 |
| DRG_steamdrake_Unstaged | 1605 | 31 | 127 | 0.815 |
| DRG_zephyr_Unstaged | 1456 | 32 | 131 | 0.763 |

Đo thật: mở `Scenes/DragonStress.unity` (menu **Dravio ▸ Character ▸ Create Stress Test Scene**) trên thiết bị, ghi FPS, `EvaluatedLastFrame`, `VisibleLastFrame`, `PoolCreated/Reused/Destroyed` của `DragonVisualManager` và Profiler (Animator.Update, SkinnedMesh) cho 5/15/30 rồng.
