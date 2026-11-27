# CHAR.9 — Alpha asset handoff (AI 3 → AI 1)

| Cần cho Alpha | Trạng thái | Ở đâu |
|---|---|---|
| `Assets/Dravio/Art/Models/EmberlingRefinedGLB.fbx` (blocker Origin scene) | **ANIMATION_QA_PASS + UNITY_READY** từ CHAR.9b (real GLB, Blender verified với skinning tuyến tính, 5/5 clip PASS cả sau import lại FBX; Unity NOT RUN; Android NOT RUN) | `Docs/char9b/CHAR9B_EMBERLING_REPORT.md` §4 (hash, phụ thuộc generator) |
| `Assets/Dravio/Art/Models/EmberlingRefined.fbx` | không cần (generator chấp nhận GLB variant) | — |
| Kit môi trường Origin / Ocean / Sky | có trên `main` (procedural U1-ENV) | `Assets/Dravio/Environment/**` |
| Scene `OriginLongTermGameplay`, `Ocean_Environment`, `Sky_Environment` | phải **sinh trong Unity** bởi AI 1 (không tạo scene giả) | xem UNITY_INTEGRATION_GUIDE.md |
| 16 rồng production | RIGGED, NEEDS_FIX (animation) | tái tạo theo PRODUCTION_ASSETS.md |
| 48 biến thể giai đoạn | 0 model riêng (dùng chung Unstaged) | DRAGON_ASSET_STATUS.json |
| Trứng, hiệu ứng nở | chưa làm (mẫu Aurorawing SAMPLE ở CHAR.8) | — |
| Sanctuary | 3/20 mẫu phong cách | SANCTUARY_PREFABS.md |

Cần chủ dự án: số chân 11 loài, chiều cao thật, có commit gói Unity sinh ra (~190 MB), ưu tiên sửa animation, GLB cho 15 vật thể còn lại.
