# Ngân sách hiệu năng (ước tính tĩnh)

**Không phải số đo trên thiết bị.** Tính từ LOD trung bình của 7 model rig trong catalog (23999/10000/4000 tris), 26 bone/model, 1 material/model, và mặc định `UpdateRatePolicy` (gần ≤ 12.0 m mỗi frame, xa mỗi 2 frame, ngoài màn hình mỗi 8 frame, > 80.0 m không đánh giá). Phân bố giả định: 20 % gần, 50 % giữa, 20 % xa, 10 % ngoài màn hình.

| Số rồng | Tam giác vẽ | Skinned mesh vẽ | Animator đánh giá / frame (TB) | Bone đánh giá / frame (TB) | Foot IK (gần, mặt đất) |
|---|---|---|---|---|---|
| 5 | 52,999 | 4 | 2.8 | 72 | ≤ 1 |
| 15 | 158,998 | 14 | 8.4 | 216 | ≤ 3 |
| 30 | 317,997 | 27 | 16.9 | 432 | ≤ 6 |

Đo thật: mở `Scenes/DragonStress.unity` (menu **Dravio ▸ Character ▸ Create Stress Test Scene**) trên thiết bị, ghi FPS, `EvaluatedLastFrame`, `VisibleLastFrame`, `PoolCreated/Reused/Destroyed` của `DragonVisualManager` và Profiler (Animator.Update, SkinnedMesh) cho 5/15/30 rồng.
