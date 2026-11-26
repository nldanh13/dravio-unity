# U2.13 — Báo cáo kiểm thử

> Nhánh: `claude/project-thread-vhega8-sanctuary-materials` (chồng trên sky_cloud → U2.12). .NET 8, cục bộ, ngày 2026-11-26. Mô phỏng: Release, 3 seed, lệnh thật, luật Schema v10 + U2.13. Bảng sky A chỉ bật trong phạm vi mô phỏng/test (`SkyCloudBalance.Use`), game thật vẫn tắt.

## 1. Bộ test

| Bộ | Kết quả |
|---|---|
| Domain | 514 đạt, 10 bỏ qua (test ghi báo cáo / probe chỉ chạy khi đặt biến môi trường), 0 lỗi |
| Persistence | 206 đạt |
| Character | 54 đạt (không đổi code Character) |
| Integration (`Integration~`) | 146 đạt |
| Character TypeCheck | build đạt |
| CI GitHub (PR #66, #67, #68) | xanh (ubuntu, windows, typescript-parity) |

Test mới U2.13: `U213SkyCloudTests` (10), `SkyCloudSaveTests` (3), `U213SkyCloudSimulationTests` (3 + 2 báo cáo), `U213SanctuaryMaterialTests` (7), `U213SanctuarySimulationTests` (1 + báo cáo), `U213FeedingResearchTests` (PR #67).

## 2. Mốc 7 / 30 / 90 / 365 ngày (trước → sau)

"Trước" = U2.12 (`Docs/u2.12/ECONOMY_365.md`, sky tắt, dự án gỗ + hạt). "Sau" = U2.13 với bảng sky A và vật liệu theo thế giới. Trung vị 3 seed, kiểu Guided (Casual / Researcher ở các bảng chi tiết).

| Chỉ số | Lịch | Ngày 7 | Ngày 30 | Ngày 90 | Ngày 365 |
|---|---|---|---|---|---|
| Dự án Sanctuary (trước → sau) | 5 phút/ngày | như nhau¹ | 14 → 14 | 24 → 23 | 32 → 32 |
| | 3 × 10 phút/ngày | như nhau¹ | 15 → 14 | 23 → 23 | 33 → 33 |
| | 60 phút/ngày | như nhau¹ | 16 → 15 | 23 → 23 | 31 → 32 |
| Chuyến sân mây (trước → sau) | 5 phút / 3×10 / 60 phút | 0 → 0 | 0 → 0 / 30 / 28 | 0 → 37 / 185 / 138 | 0 → 276 / 975 / 672 |
| Bụi sao còn giữ (sau) | 3 × 10 phút/ngày | 0 | 97 | 571 | 2 942 (mô phỏng sky) / 2 930 (mô phỏng dự án) |
| Nhiệm vụ (trước → sau) | 3 × 10 phút/ngày | — | — | — | 16 → **17** |
| Loài sở hữu (trước → sau) | Guided | — | — | — | 14 → 15 (16/16 qua lai Amberhorn, mục 4) |
| Vàng 365 (trước → sau) | 3 × 10 phút/ngày | — | — | — | 18.94 M → 24.17 M (khác do RNG giao dịch; không nguồn vàng mới) |

¹ Trước ngày 9,7 (sớm nhất mở Thiên Giới) không có chuyến sân mây; trước khi mở Thủy Vực, luật vật liệu mới trả về đúng gỗ + hạt như cũ, nên ngày 7 trùng U2.12 theo cấu trúc luật (U2.12 ngày 7: số dư 25–66 k G).

Chi tiết: [SKY_CLOUD_BALANCE.md](SKY_CLOUD_BALANCE.md), [SANCTUARY_MATERIALS.md](SANCTUARY_MATERIALS.md).

## 3. Hành trình ba thế giới và 17 nhiệm vụ

| Kiểm tra | Trước | Sau (bảng A) | Bằng chứng |
|---|---|---|---|
| Kiểm toán: nhiệm vụ đạt được | 16/17 (`Q_EXPEDITION_CLOUD` khóa) | **17/17** | `Approved_table_makes_every_quest_reachable_and_all_sixteen_species_obtainable` |
| Kiểm toán: loài sở hữu được | 15/16 (thiếu Aurorawing) | **16/16** | như trên |
| `Q_EXPEDITION_CLOUD` nhận được bằng lệnh | không | có | `Quest_cloud_becomes_completable_once_the_route_is_enabled` |
| Guided 3 × 10 phút: nhiệm vụ 365 ngày | 16 | 17 | mô phỏng sky |
| Ngày mở Thiên Giới | 9,7–79 | không đổi | luật U2.11 không đổi |
| Không bảng: không ai bay sân mây | — | 0 chuyến, 0 trứng | `Without_an_approved_table_no_simulated_player_ever_flies_to_the_cloud_terrace` |

## 4. 16 loài / Codex 16/16

`Codex_16_of_16_is_reachable_with_table_A_aurorawing_egg_plus_breeding_amberhorn`: save mới, lệnh thật, bảng A → mua gói tới đủ loài gói, 15 chuyến sân mây → trứng Aurorawing nở (Normal, Baby), lai cặp ứng viên tới khi nở Amberhorn → Codex 16/16. Người chơi mô phỏng thường thiếu Amberhorn (chỉ lai được) — không liên quan U2.13.

## 5. Trứng Aurorawing, nhận trùng, kho đầy, offline

| Kiểm tra | Kết quả | Test |
|---|---|---|
| Trao đúng 1 lần ở chuyến 15 (không ở 14, không lặp ở 16+) | đạt | `Aurorawing_egg_is_granted_exactly_once_on_the_15th_claimed_sky_trip` |
| Nhận chuyến hai lần / gửi lại request | không trả thưởng hai lần | `Claiming_twice_or_replaying_a_request_never_pays_twice` |
| Kho đầy hoặc đang có trứng chờ | trứng vào `rewardQueue`, giao khi có chỗ, không mất | `Aurorawing_egg_waits_in_the_queue_while_an_egg_is_pending_or_storage_is_full` |
| Không vàng / XP từ tuyến | đạt | `Candidate_tables_never_pay_gold_or_xp_and_only_pay_stardust`, `Guided_player_with_table_A_…_no_gold_from_the_route` |
| Mọi seed mô phỏng | đúng 1 trứng / seed | cột "Số trứng trao tối đa / seed" = 1 |
| Offline | chuyến chạy và hoàn tất trong lúc offline giữa các phiên (mô phỏng dùng `AdvanceTimeCommand` Offline); số chuyến A/B/C bằng nhau chính vì vậy | `SKY_CLOUD_BALANCE.md` mục 3 |
| Dự án đã xây không bị tính lại khi mở thế giới | đạt | `Built_projects_are_never_recalculated_and_opening_a_world_only_changes_future_costs` |

## 6. Save v10 và migration

**Không cần migration.** Schema v10 giữ nguyên: bụi sao trong `player.inventory`, bộ đếm trong `expeditionsClaimed.sky_cloud`, trứng trong `grantedRewards` / `rewardQueue`, vật liệu dự án tính từ save.

| Kiểm tra | Test |
|---|---|
| Trứng chờ + bụi sao lưu / nạp trùng byte | `Queued_aurorawing_egg_and_stardust_round_trip_byte_for_byte_under_schema_v10` |
| Save v10 cũ (không bụi sao, không chuyến sân mây) nạp không đổi | `Old_v10_saves_without_stardust_or_sky_claims_load_unchanged` |
| Save sửa tay có trứng trước mốc bị từ chối | `Forged_aurorawing_grant_is_rejected_on_load`, `Validator_rejects_a_hand_edited_aurorawing_grant_before_the_milestone` |

## 7. Kinh tế

| Kiểm tra | Kết quả |
|---|---|
| Nguồn vàng / XP mới | không |
| Giá 60 dự án, công thức số lượng | không đổi (test) |
| Dự án / năm | 31–35 trước, 32–35 sau; giới hạn bởi vàng |
| Người chơi bị chặn vì vật liệu ở ngày 365 | 0 % trước và sau |
| Dư vật liệu | sky tắt: pha lê / san hô vẫn dư ~400–1 500; bảng A: bụi sao dư ~780–2 960 (Q4) |
