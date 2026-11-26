# Ma trận hành vi 16 loài (U2.9)

Nguồn: `Queries.BehaviorQueries.Matrix()`, tính từ catalog sinh học G0 (`locomotion`, `respiration`, `thermalBands`, `activity`, `habitatRules`) và hồ sơ 7 microhabitat. Không có dữ liệu sinh học mới. Luật khả năng: ADR 0203 B3. Bảng dưới được sinh bởi `BehaviorSimulationTests` (mục 1 của [BEHAVIOR_SIMULATION.md](BEHAVIOR_SIMULATION.md)); test `Species_level_capabilities_follow_G0_…` và `Every_decision_is_biologically_possible_…` khóa các ô này.

## 1. Khả năng theo loài và nơi ở

| Loài | Chu kỳ | Di chuyển | Bask | Swim | Fly | Burrow |
|---|---|---|---|---|---|---|
| emberling | day | walk | ✓ origin_meadow, origin_warm_rocks | ✗ no-locomotion | ✗ no-locomotion | ✗ no-locomotion |
| droplet | both | walk+swim | ✓ origin_pondbank | ✓ origin_pondbank, ocean_estuary | ✗ no-locomotion | ✗ no-locomotion |
| sprout | day | walk+burrow | ✓ origin_meadow, origin_pondbank | ✗ no-locomotion | ✗ no-locomotion | ✓ origin_meadow, origin_pondbank |
| zephyr | day | walk+fly | ✓ origin_meadow, sky_low_island | ✗ no-locomotion | ✓ origin_meadow, sky_low_island | ✗ no-locomotion |
| steamdrake | both | walk+swim | ✓ origin_pondbank | ✓ origin_pondbank, ocean_estuary | ✗ no-locomotion | ✗ no-locomotion |
| magma | both | walk+burrow | ✓ origin_warm_rocks | ✗ no-locomotion | ✗ no-locomotion | ✓ origin_warm_rocks |
| sparkwing | day | walk+fly | ✓ origin_meadow, sky_low_island | ✗ no-locomotion | ✓ origin_meadow, sky_low_island | ✗ no-locomotion |
| frostfin | both | swim | ✗ not-thermophile | ✓ ocean_cool_pool | ✗ no-locomotion | ✗ no-locomotion |
| mossback | day | walk+swim | ✓ origin_pondbank | ✓ origin_pondbank, ocean_estuary | ✗ no-locomotion | ✗ no-locomotion |
| blossom | day | walk+fly | ✓ origin_meadow, sky_low_island | ✗ no-locomotion | ✓ origin_meadow, sky_low_island | ✗ no-locomotion |
| solaris | day | walk+fly | ✓ origin_meadow, origin_warm_rocks, sky_low_island | ✗ no-locomotion | ✓ origin_meadow, origin_warm_rocks, sky_low_island | ✗ no-locomotion |
| leviathan | both | swim | ✗ not-thermophile | ✓ ocean_reef | ✗ no-locomotion | ✗ no-locomotion |
| amberhorn | day | walk+burrow | ✓ origin_meadow, origin_warm_rocks | ✗ no-locomotion | ✗ no-locomotion | ✓ origin_meadow, origin_warm_rocks |
| aurorawing | night | walk+fly | ✗ not-thermophile | ✗ no-locomotion | ✓ origin_meadow, sky_low_island | ✗ no-locomotion |
| celestia | night | walk+fly | ✓ origin_meadow, sky_low_island | ✗ no-locomotion | ✓ origin_meadow, sky_low_island | ✗ no-locomotion |
| gaia | both | walk+swim | ✓ origin_pondbank | ✓ origin_pondbank, ocean_estuary | ✗ no-locomotion | ✗ no-locomotion |


Ký hiệu: `✓ <microhabitat>` = làm được ở những microhabitat đó; `✗ no-locomotion` = loài không có kiểu di chuyển này; `✗ not-thermophile` = thở mang (frostfin, leviathan) hoặc không chịu được dải Warm/Hot (aurorawing: cool/mild).

Chu kỳ: `day` ngủ đêm, `night` ngủ ngày, `both` không ngủ theo nhịp mà nghỉ rải rác (ADR 0203 B2).

## 2. Hành vi gameplay và hành vi trình diễn

Vai trò nằm trên **từng quyết định** (`BehaviorDecision.Role`). Không quyết định nào tự đổi `GameState`.

| Hành vi | Khi nào là Gameplay (`reason`) | Khi nào là Trình diễn | Lệnh có sẵn liên quan | Gợi ý hiển thị |
|---|---|---|---|---|
| Idle | — | chọn ngẫu nhiên (`ambient`) | — | Idle |
| Rest | ở trạm (`in-care`), ở nơi trú (`sheltered`), Health/Mobility < 60 (`vitals-low`), zone có cảnh báo (`hazard-warning`) | chọn ngẫu nhiên | — | Idle |
| Sleep | Energy < 30 (`energy-low`; Energy hiện luôn 100) | nhịp sinh học (`circadian-sleep`) hoặc chọn ngẫu nhiên lúc chạng vạng / loài `both` | — | Sleep |
| Explore | — | chọn ngẫu nhiên | — | Walk (hoặc Swim nếu loài không đi trên đất ở đó) |
| SeekFood | có drop thức ăn được giao (`food-drop`, `TargetFoodId`) | không bao giờ | Sanctuary dẫn rồng tới drop rồi gửi `consumeFood(foodId, uid)`; luôn hợp lệ ngay lúc quyết định | Walk / Swim |
| Eat | 5 s sau `consumeFood` thành công (`fed`) | không bao giờ | — | Idle + `Interaction = Eat` |
| Play | — | chọn ngẫu nhiên | — | Run / Swim + `Interaction = Happy` |
| Bask | — | chọn ngẫu nhiên, chỉ lúc bình minh / ban ngày | — | Idle |
| Swim | — | chọn ngẫu nhiên, chỉ nơi có nước | — | Swim |
| Fly | — | chọn ngẫu nhiên, chỉ Sky hoặc nơi có chỗ đậu | — | Fly |
| Burrow | — | chọn ngẫu nhiên, chỉ nền đất Land | — | Burrow |

Rồng ở kho (`in-storage`), đang lai (`busy-breeding`) hoặc đang thám hiểm (`busy-expedition`) có `Visible = false`, `Behavior = null`. Rồng đã thả: `Decide` trả `null`.

## 3. Thứ tự quyết định

1. Hiện diện (kho / lai / thám hiểm ẩn; trạm / nơi trú → Rest).
2. Eat (vừa được cho ăn).
3. SeekFood (được giao drop).
4. Rest khi vitals thấp.
5. Sleep khi Energy thấp.
6. Rest khi có cảnh báo thiên tai trong zone (thiên tai tự nhiên vẫn tắt).
7. Sleep theo nhịp sinh học (mức hoạt động ≤ 10).
8. Chọn có trọng số (`Weights` có trong DTO để debug).

## 4. Ảnh hưởng của cá tính

Xem mục 3 của [BEHAVIOR_SIMULATION.md](BEHAVIOR_SIMULATION.md). Tóm tắt ban ngày: Playful chơi gấp đôi Calm/Shy; Curious khám phá nhiều nhất; Calm và Shy Idle/Rest/Bask nhiều nhất; Shy bay ít nhất. Cá tính không ảnh hưởng kinh tế (test `Economy_is_identical_with_behavior_queries_interleaved`).
