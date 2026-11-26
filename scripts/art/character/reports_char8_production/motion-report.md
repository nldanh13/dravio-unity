# Motion metadata và chuyển tiếp (U1-CHAR.7, 7b)

Cho Environment/Sanctuary: kích thước thân, tốc độ authored, độ lệch root khi cất/hạ cánh, khoảng không cần cho từng môi trường, và kết quả kiểm chuyển tiếp (ngưỡng khép vòng: ≤ 0,5°).

| Model | Bằng chứng | Thân D×R×C (m) | Swim / Fly m/s | TakeOff / Land root (m) | Nước: cao × rộng (m) | Bay: sải (m) | Chuyển tiếp (max °) |
|---|---|---|---|---|---|---|---|
| DRG_amberhorn_Unstaged | thực tế (chưa nghiệm thu) | 1.99×0.56×1.20 | — / — | — / — | — × — | — | 6 (0.128) |
| DRG_aurorawing_Unstaged | thực tế (chưa nghiệm thu) | 1.98×1.80×1.47 | — / 0.84 | 0.689 / 0.542 | — × — | 1.82 | 6 (0.040) |
| DRG_blossom_Unstaged | thực tế (chưa nghiệm thu) | 1.99×1.47×1.13 | — / 0.22 | 0.531 / 0.418 | — × — | 1.52 | 6 (0.028) |
| DRG_celestia_Unstaged | thực tế (chưa nghiệm thu) | 1.99×0.96×1.20 | — / 0.52 | 0.564 / 0.444 | — × — | 1.02 | 6 (0.079) |
| DRG_droplet_Unstaged | thực tế (chưa nghiệm thu) | 1.99×0.88×1.03 | 0.75 / — | — / — | 1.12 × 1.11 | — | 6 (0.048) |
| DRG_emberling_Unstaged | thực tế (chưa nghiệm thu) | 1.48×0.40×0.90 | — / — | — / — | — × — | — | 2 (0.056) |
| DRG_frostfin_Unstaged | thực tế (chưa nghiệm thu) | 0.75×1.99×1.74 | 0.17 / — | — / — | 1.75 × 2.03 | — | 2 (0.125) |
| DRG_gaia_Unstaged | thực tế (chưa nghiệm thu) | 1.99×0.72×1.12 | 0.75 / — | — / — | 1.14 × 0.74 | — | 6 (0.069) |
| DRG_leviathan_Unstaged | thực tế (chưa nghiệm thu) | 1.06×1.78×1.99 | 0.40 / — | — / — | 1.99 × 1.81 | — | 2 (0.000) |
| DRG_magma_Unstaged | thực tế (chưa nghiệm thu) | 1.99×0.61×1.20 | — / — | — / — | — × — | — | 6 (0.074) |
| DRG_mossback_Unstaged | thực tế (chưa nghiệm thu) | 1.99×0.66×1.12 | 0.75 / — | — / — | 1.13 × 0.66 | — | 6 (0.056) |
| DRG_solaris_Unstaged | thực tế (chưa nghiệm thu) | 1.99×1.76×1.31 | — / 1.23 | 0.617 / 0.486 | — × — | 1.76 | 6 (0.288) |
| DRG_sparkwing_Unstaged | thực tế (chưa nghiệm thu) | 1.99×0.89×1.15 | — / 0.43 | 0.540 / 0.425 | — × — | 0.93 | 6 (0.069) |
| DRG_sprout_Unstaged | thực tế (chưa nghiệm thu) | 1.99×0.73×1.49 | — / — | — / — | — × — | — | 6 (0.056) |
| DRG_steamdrake_Unstaged | thực tế (chưa nghiệm thu) | 1.99×0.57×1.13 | 0.75 / — | — / — | 1.13 × 0.58 | — | 6 (0.093) |
| DRG_zephyr_Unstaged | thực tế (chưa nghiệm thu) | 1.99×1.78×1.31 | — / 0.25 | 0.615 / 0.484 | — × — | 1.79 | 6 (0.289) |

## Sinh cơ học (U1-CHAR.7b)

Froude theo Alexander (λ/h = 2,3·Fr^0,3); Strouhal St = f·A/U (hành trình 0,2–0,4); A/L = biên độ đỉnh-đỉnh chóp đuôi/cánh trên chiều dài thân (cá 0,1–0,3); tỉ lệ vận tốc lúc rời đất (chim 0,8–0,95). Nguồn: `Assets/Dravio/Character/BIOMECHANICS_RESEARCH.md`.

| Model | Walk: khung / Fr | Run: khung / Fr | Swim: St / A/L | Fly: St / A/L | Quay trên không °/s | Rời đất | Ghi chú |
|---|---|---|---|---|---|---|---|
| DRG_amberhorn_Unstaged | 40 / 0.003 | 22 / 0.052 | — | — | 60 | — | — |
| DRG_aurorawing_Unstaged | 34 / 0.007 | 19 / 0.131 | — | 0.30 / 0.14 | 180 | 0.85 | — |
| DRG_blossom_Unstaged | 27 / 0.021 | 15 / 0.397 | — | 0.30 / 0.04 | 180 | 0.85 | — |
| DRG_celestia_Unstaged | 30 / 0.021 | 17 / 0.396 | — | 0.30 / 0.08 | 180 | 0.85 | — |
| DRG_droplet_Unstaged | 25 / 0.020 | 14 / 0.379 | 0.94 / 0.57 | — | 60 | — | Swim: A/L ngoài dải cá, giữ sải mặc định; SwimSurface: A/L ngoài dải cá, giữ sải mặc định |
| DRG_emberling_Unstaged | 25 / 0.015 | 14 / 0.283 | — | — | 60 | — | — |
| DRG_frostfin_Unstaged | — | — | 0.30 / 0.11 | — | 60 | — | — |
| DRG_gaia_Unstaged | 22 / 0.015 | 12 / 0.279 | 0.08 / 0.05 | — | 60 | — | Swim: A/L ngoài dải cá, giữ sải mặc định; SwimSurface: A/L ngoài dải cá, giữ sải mặc định |
| DRG_leviathan_Unstaged | — | — | 0.10 / 0.06 | — | 60 | — | Swim: A/L ngoài dải cá, giữ sải mặc định |
| DRG_magma_Unstaged | 28 / 0.019 | 16 / 0.353 | — | — | 60 | — | — |
| DRG_mossback_Unstaged | 27 / 0.021 | 15 / 0.401 | 0.06 / 0.03 | — | 60 | — | Swim: A/L ngoài dải cá, giữ sải mặc định; SwimSurface: A/L ngoài dải cá, giữ sải mặc định |
| DRG_solaris_Unstaged | 35 / 0.005 | 19 / 0.087 | — | 0.30 / 0.20 | 180 | 0.85 | — |
| DRG_sparkwing_Unstaged | 30 / 0.021 | 17 / 0.396 | — | 0.30 / 0.07 | 180 | 0.85 | — |
| DRG_sprout_Unstaged | 30 / 0.021 | 17 / 0.386 | — | — | 60 | — | — |
| DRG_steamdrake_Unstaged | — | — | 0.09 / 0.06 | — | 60 | — | Swim: A/L ngoài dải cá, giữ sải mặc định; SwimSurface: A/L ngoài dải cá, giữ sải mặc định |
| DRG_zephyr_Unstaged | 40 / 0.003 | 22 / 0.049 | — | 0.30 / 0.04 | 180 | 0.85 | — |

Tốc độ Swim/Fly: clip tại chỗ. Tốc độ danh nghĩa theo Strouhal 0,3 của nhịp đo trên model. Nếu không đo được hoặc biên độ đuôi ngoài dải thì dùng số thân mỗi chu kỳ của `rig-profiles.json` ▸ `motionDefaults`. **Chờ art director xác nhận.** Giới hạn quay: Ground/Water từ `motionDefaults`, Air tính từ vòng nghiêng.
