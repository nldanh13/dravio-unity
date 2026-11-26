# Motion metadata và chuyển tiếp (U1-CHAR.7)

Cho Environment/Sanctuary: kích thước thân, tốc độ authored, độ lệch root khi cất/hạ cánh, khoảng không cần cho từng môi trường, và kết quả kiểm chuyển tiếp (ngưỡng khép vòng: ≤ 0,5°).

| Model | Bằng chứng | Thân D×R×C (m) | Swim / Fly m/s | TakeOff / Land root (m) | Nước: cao × rộng (m) | Bay: sải (m) | Chuyển tiếp (max °) |
|---|---|---|---|---|---|---|---|
| DRG_droplet_Unstaged | SAMPLE | 1.64×0.61×0.90 | 0.61 / — | — / — | 0.92 × 0.90 | — | 6 (0.097) |
| DRG_emberling_Unstaged | SAMPLE | 1.63×0.48×0.90 | — / — | — / — | — × — | — | 2 (0.074) |
| DRG_frostfin_Juvenile | SAMPLE | 1.45×1.15×0.40 | 0.54 / — | — / — | 0.47 × 1.18 | — | 2 (0.217) |
| DRG_leviathan_Adult | SAMPLE | 2.17×0.60×0.34 | 0.81 / — | — / — | 0.36 × 0.68 | — | 2 (0.028) |
| DRG_magma_Unstaged | SAMPLE | 1.63×0.48×0.90 | — / — | — / — | — × — | — | 6 (0.056) |
| DRG_sparkwing_Unstaged | SAMPLE | 1.63×1.51×0.90 | — / 4.58 | 0.390 / 0.307 | — × — | 1.58 | 6 (0.069) |
| DRG_sprout_Unstaged | SAMPLE | 1.63×0.48×1.01 | — / — | — / — | — × — | — | 6 (0.063) |
| DRG_steamdrake_Unstaged | SAMPLE | 2.27×0.43×0.42 | 0.85 / — | — / — | 0.42 × 0.52 | — | 6 (0.079) |
| DRG_zephyr_Unstaged | SAMPLE | 1.63×1.51×0.90 | — / 4.58 | 0.390 / 0.307 | — × — | 1.58 | 6 (0.069) |

Tốc độ Swim/Fly: clip tại chỗ, tốc độ danh nghĩa = số thân mỗi chu kỳ (`rig-profiles.json` ▸ `motionDefaults`), **mặc định pipeline, chờ art director xác nhận**. Giới hạn quay mỗi môi trường cũng từ `motionDefaults`.
