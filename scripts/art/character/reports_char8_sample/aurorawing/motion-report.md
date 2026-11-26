# Motion metadata và chuyển tiếp (U1-CHAR.7, 7b)

Cho Environment/Sanctuary: kích thước thân, tốc độ authored, độ lệch root khi cất/hạ cánh, khoảng không cần cho từng môi trường, và kết quả kiểm chuyển tiếp (ngưỡng khép vòng: ≤ 0,5°).

| Model | Bằng chứng | Thân D×R×C (m) | Swim / Fly m/s | TakeOff / Land root (m) | Nước: cao × rộng (m) | Bay: sải (m) | Chuyển tiếp (max °) |
|---|---|---|---|---|---|---|---|
| DRG_aurorawing_Adult | SAMPLE | 2.69×2.12×1.20 | — / 0.66 | 0.564 / 0.444 | — × — | 2.21 | 6 (0.069) |
| DRG_aurorawing_Baby | SAMPLE | 1.00×0.70×0.55 | — / 0.10 | 0.259 / 0.203 | — × — | 0.74 | 6 (0.040) |
| DRG_aurorawing_Juvenile | SAMPLE | 1.74×1.41×0.85 | — / 0.87 | 0.400 / 0.315 | — × — | 1.48 | 6 (0.040) |

## Sinh cơ học (U1-CHAR.7b)

Froude theo Alexander (λ/h = 2,3·Fr^0,3); Strouhal St = f·A/U (hành trình 0,2–0,4); A/L = biên độ đỉnh-đỉnh chóp đuôi/cánh trên chiều dài thân (cá 0,1–0,3); tỉ lệ vận tốc lúc rời đất (chim 0,8–0,95). Nguồn: `Assets/Dravio/Character/BIOMECHANICS_RESEARCH.md`.

| Model | Walk: khung / Fr | Run: khung / Fr | Swim: St / A/L | Fly: St / A/L | Quay trên không °/s | Rời đất | Ghi chú |
|---|---|---|---|---|---|---|---|
| DRG_aurorawing_Adult | 25 / 0.018 | 14 / 0.347 | — | 0.30 / 0.08 | 180 | 0.85 | — |
| DRG_aurorawing_Baby | 17 / 0.018 | 10 / 0.341 | — | 0.30 / 0.03 | 180 | 0.85 | — |
| DRG_aurorawing_Juvenile | 21 / 0.018 | 12 / 0.339 | — | 0.30 / 0.16 | 180 | 0.85 | — |

Tốc độ Swim/Fly: clip tại chỗ. Tốc độ danh nghĩa theo Strouhal 0,3 của nhịp đo trên model. Nếu không đo được hoặc biên độ đuôi ngoài dải thì dùng số thân mỗi chu kỳ của `rig-profiles.json` ▸ `motionDefaults`. **Chờ art director xác nhận.** Giới hạn quay: Ground/Water từ `motionDefaults`, Air tính từ vòng nghiêng.
