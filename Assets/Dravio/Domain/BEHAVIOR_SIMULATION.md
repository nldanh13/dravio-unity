# Báo cáo mô phỏng cân bằng U2.9 — hành vi, cá tính, comfort, nghiên cứu

Tái tạo (từ thư mục gốc repo):

```bash
DRAVIO_BEHAVIOR_OUT=report.md dotnet test Assets/Dravio/Tests/Domain/DotNet~/Dravio.Domain.Tests.csproj --filter "FullyQualifiedName~Write_behavior_report_when_requested"
```

Mọi số liệu tất định (không có seed ngẫu nhiên). Phần dưới đường kẻ là đầu ra nguyên văn của test.

## Kết luận

1. **Kinh tế không đổi.** Engine chỉ đọc. Test CI `Economy_is_identical_with_behavior_queries_interleaved` chạy mô phỏng kinh tế U2.7 (Collector và BreedTrader, 2 seed, 3 ngày, lịch 3 × 10 phút/ngày) hai lần, một lần gọi toàn bộ truy vấn U2.9 sau mỗi lệnh: vàng, vàng sản xuất, G/s, số rồng và toàn bộ state cuối trùng khớp.
2. **Hành vi hợp sinh học.** Không loài nào bơi, bay hay đào khi không có khả năng hoặc nơi ở không cho phép (test duyệt hơn 50 000 quyết định của 16 loài × mọi microhabitat hợp lệ × 4 cá tính × 4 pha). Loài ban ngày ngủ khoảng 45 % một ngày, loài ban đêm khoảng 50 %, loài `both` 14–16 %.
3. **Cá tính thấy rõ nhưng không lấn sinh học.** Ban ngày: Playful chơi 9 % so với 4 % của Calm và Shy; Curious khám phá 22 % so với 15 % của Calm; Shy bay ít nhất (4 %). Loại hành vi theo loài (bơi, bay, đào) vẫn do sinh học quyết định.
4. **Comfort đề xuất C1 trung tính ở môi trường hiện tại.** Cả 30 cặp loài × microhabitat hợp lệ có suitability canonical 100, nên C1 không đổi comfort của ai cho tới khi có luật suy giảm môi trường (ADR 0202 P7). Nếu chất lượng giảm 40 điểm, 20/30 cặp sẽ xuống dưới ngưỡng lai 60 ở Ecosystem sau 4–6 giờ online; ở Cozy không cặp nào. Khuyến nghị: duyệt C1 cùng P7, cân nhắc sàn Ecosystem 60 nếu muốn môi trường chỉ là "tín hiệu" chứ không chặn.
5. **Nghiên cứu quá nhanh nếu không có giới hạn.** Đạt Ethology sau 14–51 lần quan sát (15–50 phút chơi chủ động). Loài bay và loài đào lâu hơn vì Fly/Burrow có trọng số thấp. Khuyến nghị ở ADR 0203 D5: giới hạn tập tính mới theo ngày.

---


### 1. Ma trận hành vi 16 loài

Ô ghi microhabitat nơi hành vi xảy ra được, hoặc lý do loài không làm được. Idle, Rest, Explore, Play, SeekFood, Eat: mọi loài, mọi microhabitat hợp lệ. Sleep: mọi loài (theo nhịp sinh học, hoặc Energy < 30).

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

### 2. Phân bổ thời gian trong một ngày 24 h (trung bình 4 cá tính × mọi microhabitat hợp lệ, Adult, có một bạn cùng chỗ, không thức ăn)

| Loài | idle | rest | sleep | explore | play | bask | swim | fly | burrow |
|---|---|---|---|---|---|---|---|---|---|
| emberling | 14 % | 9 % | 46 % | 13 % | 4 % | 13 % | 0 % | 0 % | 0 % |
| droplet | 20 % | 15 % | 16 % | 19 % | 6 % | 3 % | 21 % | 0 % | 0 % |
| sprout | 13 % | 9 % | 46 % | 13 % | 4 % | 9 % | 0 % | 0 % | 6 % |
| zephyr | 12 % | 8 % | 45 % | 12 % | 4 % | 8 % | 0 % | 11 % | 0 % |
| steamdrake | 20 % | 15 % | 16 % | 19 % | 6 % | 3 % | 21 % | 0 % | 0 % |
| magma | 23 % | 16 % | 16 % | 16 % | 6 % | 14 % | 0 % | 0 % | 9 % |
| sparkwing | 12 % | 8 % | 45 % | 12 % | 4 % | 8 % | 0 % | 11 % | 0 % |
| frostfin | 21 % | 15 % | 14 % | 15 % | 6 % | 0 % | 29 % | 0 % | 0 % |
| mossback | 12 % | 8 % | 45 % | 12 % | 4 % | 4 % | 14 % | 0 % | 0 % |
| blossom | 12 % | 8 % | 45 % | 12 % | 4 % | 8 % | 0 % | 11 % | 0 % |
| solaris | 11 % | 8 % | 45 % | 11 % | 3 % | 11 % | 0 % | 10 % | 0 % |
| leviathan | 21 % | 15 % | 14 % | 15 % | 6 % | 0 % | 29 % | 0 % | 0 % |
| amberhorn | 12 % | 9 % | 46 % | 12 % | 4 % | 12 % | 0 % | 0 % | 5 % |
| aurorawing | 13 % | 9 % | 50 % | 13 % | 4 % | 0 % | 0 % | 11 % | 0 % |
| celestia | 13 % | 9 % | 49 % | 13 % | 4 % | 1 % | 0 % | 11 % | 0 % |
| gaia | 20 % | 15 % | 16 % | 19 % | 6 % | 3 % | 21 % | 0 % | 0 % |

### 3. Phân bổ theo cá tính (trung bình 16 loài, ban ngày 07:00–17:30)

| Cá tính | idle | rest | sleep | explore | play | bask | swim | fly | burrow |
|---|---|---|---|---|---|---|---|---|---|
| playful | 15 % | 9 % | 19 % | 18 % | 9 % | 11 % | 9 % | 8 % | 2 % |
| calm | 20 % | 14 % | 19 % | 15 % | 4 % | 15 % | 7 % | 6 % | 1 % |
| curious | 16 % | 10 % | 19 % | 22 % | 6 % | 12 % | 8 % | 7 % | 2 % |
| shy | 20 % | 14 % | 19 % | 16 % | 4 % | 14 % | 7 % | 4 % | 2 % |

### 4. Luật comfort đề xuất C1 (TẮT ở production)

Mục tiêu comfort = max(sàn, suitability + 5 nếu là microhabitat ưa thích). Sàn Cozy 60, Ecosystem 40. Online đổi 1 điểm / 6 phút về mục tiêu; offline chỉ tăng. Ngưỡng lai / thám hiểm: comfort ≥ 60.

| Loài | Microhabitat | Suitability canonical | Mục tiêu Ecosystem | Chất lượng −20 | Chất lượng −40 | Chặn lai ở −40 (Ecosystem)? | Giờ online 100 → mục tiêu ở −40 |
|---|---|---|---|---|---|---|---|
| emberling | origin_meadow | 100 | 100 | 100 | 70 | không | 3 |
| emberling | origin_warm_rocks | 100 | 100 | 95 | 45 | có | 5.5 |
| droplet | origin_pondbank | 100 | 100 | 95 | 40 | có | 6 |
| droplet | ocean_estuary | 100 | 100 | 100 | 65 | không | 3.5 |
| sprout | origin_meadow | 100 | 100 | 90 | 30 | có | 6 |
| sprout | origin_pondbank | 100 | 100 | 95 | 35 | có | 6 |
| zephyr | origin_meadow | 100 | 100 | 90 | 55 | có | 4.5 |
| zephyr | sky_low_island | 100 | 100 | 95 | 60 | không | 4 |
| steamdrake | origin_pondbank | 100 | 100 | 100 | 45 | có | 5.5 |
| steamdrake | ocean_estuary | 100 | 100 | 100 | 70 | không | 3 |
| magma | origin_warm_rocks | 100 | 100 | 100 | 80 | không | 2 |
| sparkwing | origin_meadow | 100 | 100 | 95 | 60 | không | 4 |
| sparkwing | sky_low_island | 100 | 100 | 100 | 65 | không | 3.5 |
| frostfin | ocean_cool_pool | 100 | 100 | 85 | 45 | có | 5.5 |
| mossback | origin_pondbank | 100 | 100 | 95 | 20 | có | 6 |
| mossback | ocean_estuary | 100 | 100 | 100 | 45 | có | 5.5 |
| blossom | origin_meadow | 100 | 100 | 90 | 30 | có | 6 |
| blossom | sky_low_island | 100 | 100 | 85 | 25 | có | 6 |
| solaris | origin_meadow | 100 | 100 | 95 | 60 | không | 4 |
| solaris | origin_warm_rocks | 100 | 100 | 95 | 55 | có | 4.5 |
| solaris | sky_low_island | 100 | 100 | 100 | 65 | không | 3.5 |
| leviathan | ocean_reef | 100 | 100 | 85 | 45 | có | 5.5 |
| amberhorn | origin_meadow | 100 | 100 | 100 | 70 | không | 3 |
| amberhorn | origin_warm_rocks | 100 | 100 | 100 | 50 | có | 5 |
| aurorawing | origin_meadow | 100 | 100 | 85 | 50 | có | 5 |
| aurorawing | sky_low_island | 100 | 100 | 90 | 55 | có | 4.5 |
| celestia | origin_meadow | 100 | 100 | 85 | 45 | có | 5.5 |
| celestia | sky_low_island | 100 | 100 | 90 | 50 | có | 5 |
| gaia | origin_pondbank | 100 | 100 | 80 | 0 | có | 6 |
| gaia | ocean_estuary | 100 | 100 | 90 | 30 | có | 6 |

Tổng: 30 cặp loài × microhabitat hợp lệ; 20 cặp sẽ bị chặn lai ở Ecosystem nếu chất lượng giảm 40 điểm (P7 giả định). Ở Cozy: 0 (sàn 60).

### 5. Nghiên cứu: số tập tính cần ghi nhận và số lần quan sát để đạt Ethology (đề xuất D2, TẮT)

Mô phỏng: một rồng Calm, quan sát một lần mỗi nhịp 60 s, lần lượt ở mỗi microhabitat hợp lệ, xoay vòng 4 pha; thêm 1 lần cho ăn và 1 drop thức ăn cho mỗi rồng. Đếm số lần quan sát tới khi đủ.

| Loài | Tập tính cần ghi nhận | Số lần quan sát (≈ phút chơi) |
|---|---|---|
| emberling | 8 (idle, rest, sleep, explore, seekFood, eat, play, bask) | 15 |
| droplet | 9 (idle, rest, sleep, explore, seekFood, eat, play, bask, swim) | 18 |
| sprout | 9 (idle, rest, sleep, explore, seekFood, eat, play, bask, burrow) | 43 |
| zephyr | 9 (idle, rest, sleep, explore, seekFood, eat, play, bask, fly) | 51 |
| steamdrake | 9 (idle, rest, sleep, explore, seekFood, eat, play, bask, swim) | 18 |
| magma | 9 (idle, rest, sleep, explore, seekFood, eat, play, bask, burrow) | 15 |
| sparkwing | 9 (idle, rest, sleep, explore, seekFood, eat, play, bask, fly) | 51 |
| frostfin | 8 (idle, rest, sleep, explore, seekFood, eat, play, swim) | 14 |
| mossback | 9 (idle, rest, sleep, explore, seekFood, eat, play, bask, swim) | 19 |
| blossom | 9 (idle, rest, sleep, explore, seekFood, eat, play, bask, fly) | 51 |
| solaris | 9 (idle, rest, sleep, explore, seekFood, eat, play, bask, fly) | 18 |
| leviathan | 8 (idle, rest, sleep, explore, seekFood, eat, play, swim) | 14 |
| amberhorn | 9 (idle, rest, sleep, explore, seekFood, eat, play, bask, burrow) | 48 |
| aurorawing | 8 (idle, rest, sleep, explore, seekFood, eat, play, fly) | 37 |
| celestia | 9 (idle, rest, sleep, explore, seekFood, eat, play, bask, fly) | 37 |
| gaia | 9 (idle, rest, sleep, explore, seekFood, eat, play, bask, swim) | 18 |

