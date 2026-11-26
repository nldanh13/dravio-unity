# Motion metadata và chuyển tiếp (U1-CHAR.7, 7b)

Cho Environment/Sanctuary: kích thước thân, tốc độ authored, độ lệch root khi cất/hạ cánh, khoảng không cần cho từng môi trường, và kết quả kiểm chuyển tiếp (ngưỡng khép vòng: ≤ 0,5°).

| Model | Bằng chứng | Thân D×R×C (m) | Swim / Fly m/s | TakeOff / Land root (m) | Nước: cao × rộng (m) | Bay: sải (m) | Chuyển tiếp (max °) |
|---|---|---|---|---|---|---|---|
| DRG_droplet_Baby | SAMPLE | 1.64×0.61×0.90 | 0.61 / — | — / — | 0.92 × 0.90 | — | 6 (0.097) |
| DRG_emberling_Baby | SAMPLE | 1.63×0.48×0.90 | — / — | — / — | — × — | — | 2 (0.074) |
| DRG_frostfin_Juvenile | SAMPLE | 1.45×1.15×0.40 | 0.54 / — | — / — | 0.47 × 1.18 | — | 2 (0.217) |
| DRG_leviathan_Adult | SAMPLE | 2.17×0.60×0.34 | 0.81 / — | — / — | 0.36 × 0.68 | — | 2 (0.028) |
| DRG_magma_Baby | SAMPLE | 1.63×0.48×0.90 | — / — | — / — | — × — | — | 6 (0.056) |
| DRG_sprout_Baby | SAMPLE | 1.63×0.48×1.01 | — / — | — / — | — × — | — | 6 (0.063) |
| DRG_steamdrake_Juvenile | SAMPLE | 2.27×0.43×0.42 | 0.85 / — | — / — | 0.42 × 0.52 | — | 6 (0.079) |
| DRG_zephyr_Unstaged | SAMPLE | 1.63×1.51×0.90 | — / 0.99 | 0.390 / 0.307 | — × — | 1.58 | 6 (0.069) |

## Sinh cơ học (U1-CHAR.7b)

Froude theo Alexander (λ/h = 2,3·Fr^0,3); Strouhal St = f·A/U (hành trình 0,2–0,4); A/L = biên độ đỉnh-đỉnh chóp đuôi/cánh trên chiều dài thân (cá 0,1–0,3); tỉ lệ vận tốc lúc rời đất (chim 0,8–0,95). Nguồn: `Assets/Dravio/Character/BIOMECHANICS_RESEARCH.md`.

| Model | Walk: khung / Fr | Run: khung / Fr | Swim: St / A/L | Fly: St / A/L | Quay trên không °/s | Rời đất | Ghi chú |
|---|---|---|---|---|---|---|---|
| DRG_droplet_Baby | 22 / 0.020 | 12 / 0.373 | 0.76 / 0.46 | — | 60 | — | Swim: A/L ngoài dải cá, giữ sải mặc định; SwimSurface: A/L ngoài dải cá, giữ sải mặc định |
| DRG_emberling_Baby | 22 / 0.020 | 12 / 0.373 | — | — | 60 | — | — |
| DRG_frostfin_Juvenile | — | — | 0.10 / 0.06 | — | 60 | — | Swim: A/L ngoài dải cá, giữ sải mặc định |
| DRG_leviathan_Adult | — | — | 0.11 / 0.07 | — | 60 | — | Swim: A/L ngoài dải cá, giữ sải mặc định |
| DRG_magma_Baby | 22 / 0.020 | 12 / 0.372 | — | — | 60 | — | — |
| DRG_sprout_Baby | 22 / 0.018 | 12 / 0.341 | — | — | 60 | — | — |
| DRG_steamdrake_Juvenile | — | — | 0.10 / 0.06 | — | 60 | — | Swim: A/L ngoài dải cá, giữ sải mặc định; SwimSurface: A/L ngoài dải cá, giữ sải mặc định |
| DRG_zephyr_Unstaged | 22 / 0.020 | 12 / 0.373 | — | 0.30 / 0.19 | 180 | 0.85 | — |

Tốc độ Swim/Fly: clip tại chỗ. Tốc độ danh nghĩa theo Strouhal 0,3 của nhịp đo trên model. Nếu không đo được hoặc biên độ đuôi ngoài dải thì dùng số thân mỗi chu kỳ của `rig-profiles.json` ▸ `motionDefaults`. **Chờ art director xác nhận.** Giới hạn quay: Ground/Water từ `motionDefaults`, Air tính từ vòng nghiêng.
