# U2.14 — Ma trận thu thập 16 loài rồng

> Dữ liệu thô: [data/](data/). Bảng 1 sinh từ catalog và luật thật (`U214CollectionMatrixTests.Table()`, `DRAVIO_U214_OUT=<thư mục>`), không mô phỏng. Bảng 3 là **mô phỏng** 365 ngày (lệnh thật, 3 seed). Kết luận trong mục 2 và 4 có test chạy thật.
> Luật production: bảng `sky_cloud` **TẮT**. Cột "Bảng A" chỉ là kiểm toán trong phạm vi `SkyCloudBalance.Use(ProposedA)`, không phải game thật.

## Kết luận

- **Production: 15 / 16 loài thu thập được từ save mới.** Loài duy nhất chưa thể thu thập: **Aurorawing**.
- **Aurorawing — phân biệt hai luật:**
  - *Luật thưởng đã duyệt (ADR 0208, đang có trong code):* trứng `EXPEDITION_EGG_AURORAWING`, Normal, nở Baby, đúng **một lần** / save, tại chuyến `sky_cloud` **thứ 15** đã nhận; kho đầy thì vào `rewardQueue`.
  - *Tuyến `sky_cloud` chưa kích hoạt (bảng thưởng chờ duyệt):* lệnh `startExpedition("sky_cloud")` trả `route-rewards-undefined`, nên bộ đếm luôn = 0 và mốc 15 không bao giờ đạt.
  - Không có nguồn thứ hai: không trong gói, không là trứng thế giới, và **không cặp lai nào thiếu Aurorawing lai ra Aurorawing** (nhóm V không loài nào khác mang nguyên tố Water). Khi đã có một con, Aurorawing × rồng nhóm V (vd Zephyr) lai ra được.
  - Bật bảng A (một dòng, xem [BALANCE_DECISIONS.md](BALANCE_DECISIONS.md)) → 16 / 16.
- **Amberhorn:** chỉ lai (không trong gói); cặp nhóm T có Magma, 9,1 % mỗi lần nở. Thu được ở production nhưng người chơi không lai có chủ đích hiếm khi có.
- **Legendary (Celestia, Gaia):** chỉ gói Mystic (1,5 %) / Astral (8,5 %) hoặc lai Epic × Epic (1 %).

## 1. Ma trận (catalog + luật)

Cột "Gói": xác suất ra loài đó mỗi lần mở gói = tỉ lệ độ hiếm của gói / số loài cùng độ hiếm có trong gói. "Lai tốt nhất": cặp không chứa chính loài đó cho tỉ lệ cao nhất (60 / 30 / 9 / 1 theo độ hiếm, chia đều), kèm phí. "Thế giới sớm nhất": thế giới đầu tiên có microhabitat loài đặt được (Khởi Nguyên → Thủy Vực → Thiên Giới).

| Loài | Độ hiếm / nhóm | Gói (mỗi lần mở) | Nguồn khác | Lai tốt nhất (không cần chính loài) | Tự nhân giống | Sinh cảnh | Thế giới sớm nhất | Production | Bảng A (mô phỏng) |
|---|---|---|---|---|---|---|---|---|---|
| emberling | Common / T | starter 23 %, elemental 12 %, mystic 3.8 % | rồng khởi đầu | magma × sprout 30 % (350 G) | có | origin_meadow, origin_warm_rocks | origin | **có** | có |
| droplet | Common / A | starter 23 %, elemental 12 %, mystic 3.8 % | `ZONE_EGG_OCEAN` (mở ocean) | steamdrake × steamdrake 67 % (350 G) | có | origin_pondbank, ocean_estuary | origin | **có** | có |
| sprout | Common / T | starter 23 %, elemental 12 %, mystic 3.8 % | gói hướng dẫn 100 G | emberling × magma 30 % (350 G) | có | origin_meadow, origin_pondbank | origin | **có** | có |
| zephyr | Common / V | starter 23 %, elemental 12 %, mystic 3.8 % | `ZONE_EGG_SKY` (mở sky) | aurorawing × aurorawing 87 % (1500 G) | có | origin_meadow, sky_low_island | origin | **có** | có |
| steamdrake | Rare / A | starter 1.7 %, elemental 7.5 %, mystic 8.3 %, astral 4.7 % | — | — | có | origin_pondbank, ocean_estuary | origin | **có** | có |
| magma | Rare / T | starter 1.7 %, elemental 7.5 %, mystic 8.3 %, astral 4.7 % | — | emberling × sprout 33 % (80 G) | có | origin_warm_rocks | origin | **có** | có |
| sparkwing | Rare / V | starter 1.7 %, elemental 7.5 %, mystic 8.3 %, astral 4.7 % | — | solaris × zephyr 30 % (1500 G) | có | origin_meadow, sky_low_island | origin | **có** | có |
| frostfin | Rare / A | starter 1.7 %, elemental 7.5 %, mystic 8.3 %, astral 4.7 % | — | — | có | ocean_cool_pool | ocean | **có** | có |
| mossback | Rare / A | starter 1.7 %, elemental 7.5 %, mystic 8.3 %, astral 4.7 % | — | droplet × leviathan 30 % (1500 G) | có | origin_pondbank, ocean_estuary | origin | **có** | có |
| blossom | Rare / V | starter 1.7 %, elemental 7.5 %, mystic 8.3 %, astral 4.7 % | — | — | có | origin_meadow, sky_low_island | origin | **có** | có |
| solaris | Epic / V | elemental 3.5 %, mystic 16 %, astral 28 % | — | sparkwing × zephyr 9.1 % (350 G) | có | origin_meadow, origin_warm_rocks, sky_low_island | origin | **có** | có |
| leviathan | Epic / A | elemental 3.5 %, mystic 16 %, astral 28 % | — | droplet × mossback 9.1 % (350 G) | có | ocean_reef | ocean | **có** | có |
| amberhorn | Epic / T | — | — | emberling × magma 9.1 % (350 G) | có | origin_meadow, origin_warm_rocks | origin | **có** | có |
| aurorawing | Epic / V | — | `EXPEDITION_EGG_AURORAWING` (sky_cloud chuyến 15) | — | có | origin_meadow, sky_low_island | origin | **không** | có |
| celestia | Legendary / V | mystic 1.5 %, astral 8.5 % | — | solaris × solaris 1.0 % (1500 G) | có | origin_meadow, sky_low_island | origin | **có** | có |
| gaia | Legendary / A | mystic 1.5 %, astral 8.5 % | — | leviathan × leviathan 1.0 % (1500 G) | có | origin_pondbank, ocean_estuary | origin | **có** | có |

## 2. Điều kiện mở khóa và sinh cảnh theo nguồn

| Nguồn | Điều kiện mở khóa | Loài |
|---|---|---|
| Rồng khởi đầu | save mới | emberling |
| Gói hướng dẫn (100 G, một lần) | tutorial TUT_05 | sprout |
| Gói Starter 150 G (≤ 5 / ngày UTC) | đủ vàng, kho < 500 | 10 loài Common / Rare |
| Gói Elemental 900 G / Mystic 6 000 G / Astral 30 000 G | đủ vàng, kho < 500 | 14 loài (trừ Amberhorn, Aurorawing); Epic từ Elemental, Legendary từ Mystic |
| Trứng thế giới `ZONE_EGG_OCEAN` | mở Thủy Vực (tutorial xong, đủ 3 sinh cảnh Khởi Nguyên, `QUEST_AQUATIC_EXPLORATION`) | droplet |
| Trứng thế giới `ZONE_EGG_SKY` | mở Thiên Giới (Thủy Vực, `QUEST_SKY_EXPLORATION`) | zephyr |
| Trứng mốc thám hiểm `EXPEDITION_EGG_AURORAWING` | Thiên Giới mở, Adult biết bay, **bảng `sky_cloud` bật**, 15 chuyến đã nhận | aurorawing |
| Lai | hai Adult cùng nhóm sinh sản, rảnh, không họ hàng, đủ phí 80 / 350 / 1 500 / 5 000 G | mọi loài có cặp; bắt buộc với amberhorn |

Sinh cảnh: hai loài chỉ sống ở Thủy Vực — **frostfin** (`ocean_cool_pool`) và **leviathan** (`ocean_reef`). Mua được từ gói trước khi mở Thủy Vực: rồng chờ trong kho, **vẫn lớn**, đặt được ngay khi mở Thủy Vực (test `Ocean_only_species_bought_before_the_ocean_opens_…`). 14 loài còn lại đặt được ở Khởi Nguyên.

## 3. Khả năng tiếp cận từ save mới (mô phỏng 365 ngày, lệnh thật)

Ngày trung vị lần đầu sở hữu (sự kiện `DragonHatched`), trong ngoặc là % seed đạt được trong 365 ngày. 3 seed, luật production.

| Kiểu | Lịch | emberling | (100%) | droplet | (33%) | sprout | (100%) | zephyr | (33%) | steamdrake | (100%) | magma | (100%) | sparkwing | (100%) | frostfin | (100%) | mossback | (100%) | blossom | (100%) | solaris | (100%) | leviathan | (100%) | amberhorn | aurorawing | celestia | (100%) | gaia | (100%) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Relaxed | 5 phút/ngày | 0 (100%) | 8 (33%) | 0 (100%) | 6 (33%) | 3 (100%) | 49 (100%) | 17 (100%) | 8 (100%) | 26 (100%) | 11 (100%) | 4 (100%) | 5 (100%) | — | — | 10 (100%) | 13 (100%) |
| Relaxed | 3 × 10 phút/ngày | 0 (100%) | 0.5 (66%) | 0 (100%) | 0.5 (66%) | 12 (100%) | 26 (100%) | 40 (100%) | 5 (100%) | 12 (100%) | 16 (100%) | 2 (100%) | 5 (100%) | — | — | 15 (100%) | 20 (100%) |
| Relaxed | 60 phút/ngày | 0 (100%) | 0.0 (33%) | 0 (100%) | 3.5 (66%) | 7 (100%) | 3 (100%) | 4 (100%) | 16 (100%) | 3 (100%) | 31 (100%) | 6 (100%) | 5 (100%) | — | — | 12 (100%) | 22 (100%) |
| Researcher | 5 phút/ngày | 0 (100%) | 11.0 (100%) | 0 (100%) | 23.0 (100%) | 15 (100%) | 7 (100%) | 9 (100%) | 17 (100%) | 2 (100%) | 22 (100%) | 4 (100%) | 3 (100%) | — | — | 17 (100%) | 8 (100%) |
| Researcher | 3 × 10 phút/ngày | 0 (100%) | 4.5 (100%) | 0 (100%) | 0.0 (100%) | 7 (100%) | 2 (100%) | 3 (100%) | 19 (100%) | 2 (100%) | 3 (100%) | 12 (100%) | 9 (100%) | — | — | 15 (100%) | 7 (100%) |
| Researcher | 60 phút/ngày | 0 (100%) | 12.5 (100%) | 0 (100%) | 0.0 (100%) | 11 (100%) | 3 (100%) | 17 (100%) | 17 (100%) | 29 (100%) | 8 (100%) | 6 (100%) | 6 (100%) | — | — | 13 (100%) | 27 (100%) |
| Collector | 5 phút/ngày | 0 (100%) | 7.0 (100%) | 0 (100%) | 10 (100%) | 5 (100%) | 3 (100%) | 11 (100%) | 19 (100%) | 10 (100%) | 6 (100%) | 4 (100%) | 5 (100%) | — | — | 14 (100%) | 11 (100%) |
| Collector | 3 × 10 phút/ngày | 0 (100%) | 1.2 (100%) | 0 (100%) | 0.5 (100%) | 6.2 (100%) | 1 (100%) | 2.5 (100%) | 1.5 (100%) | 3 (100%) | 2 (100%) | 2 (100%) | 1 (100%) | 6 (33%) | — | 7 (100%) | 14 (100%) |
| Collector | 60 phút/ngày | 0 (100%) | 3.5 (100%) | 0 (100%) | 6.5 (100%) | 4 (100%) | 0.5 (100%) | 3.5 (100%) | 2.5 (100%) | 3.5 (100%) | 9 (100%) | 1 (100%) | 2.5 (100%) | 4 (66%) | — | 5 (100%) | 12 (100%) |
| Breeder | 5 phút/ngày | 0 (100%) | 6.0 (100%) | 0 (100%) | 7.0 (100%) | 7 (100%) | 3 (100%) | 9 (100%) | 15 (100%) | 2 (100%) | 9 (100%) | 6 (100%) | 3 (100%) | — | — | 9 (100%) | 12 (100%) |
| Breeder | 3 × 10 phút/ngày | 0 (100%) | 4.2 (100%) | 0 (100%) | 7.0 (100%) | 2 (100%) | 1 (100%) | 10.0 (100%) | 8 (100%) | 3 (100%) | 4 (100%) | 5 (100%) | 6 (100%) | 3.2 (33%) | — | 5 (66%) | 10 (100%) |
| Breeder | 60 phút/ngày | 0 (100%) | 2 (100%) | 0 (100%) | 5.5 (100%) | 5 (66%) | 0.5 (100%) | 5.5 (100%) | 15 (100%) | 11.5 (100%) | 12 (100%) | 3 (100%) | 5 (100%) | 4.5 (100%) | — | 9 (100%) | 14 (100%) |
| Guided | 5 phút/ngày | 0 (100%) | 11.0 (100%) | 0 (100%) | 7 (100%) | 10 (100%) | 18 (100%) | 10 (100%) | 28 (100%) | 2 (100%) | 8 (100%) | 4 (100%) | 3 (100%) | — | — | 18 (100%) | 8 (100%) |
| Guided | 3 × 10 phút/ngày | 0 (100%) | 3.2 (100%) | 0 (100%) | 0.0 (100%) | 7 (100%) | 2 (100%) | 2 (100%) | 9 (100%) | 9 (100%) | 1 (100%) | 5 (100%) | 3.2 (100%) | — | — | 5 (100%) | 6.5 (100%) |
| Guided | 60 phút/ngày | 0 (100%) | 3.0 (100%) | 0 (100%) | 0.0 (100%) | 5.0 (100%) | 3 (100%) | 1.5 (100%) | 7.0 (100%) | 10 (100%) | 1 (100%) | 4 (100%) | 3.0 (100%) | — | — | 11 (100%) | 14 (100%) |
| Casual | 5 phút/ngày | 0 (100%) | 9.0 (100%) | 0 (100%) | 2 (100%) | 25 (100%) | 37 (100%) | 49 (100%) | 3 (100%) | 2 (100%) | 12 (100%) | 5 (100%) | 6 (100%) | — | — | 12 (100%) | 13 (100%) |
| Casual | 3 × 10 phút/ngày | 0 (100%) | 5 (100%) | 0 (100%) | 31.7 (66%) | 5 (100%) | 4 (100%) | 2 (100%) | 18 (100%) | 24 (100%) | 7 (100%) | 5 (100%) | 3 (100%) | — | — | 19 (100%) | 9 (100%) |
| Casual | 60 phút/ngày | 0 (100%) | 1 (100%) | 0 (100%) | 5 (100%) | 10 (100%) | 13 (100%) | 3 (100%) | 14 (100%) | 8 (100%) | 4 (100%) | 7 (100%) | 8 (100%) | — | — | 23 (100%) | 10 (100%) |

Đọc bảng: "3.2 (100%)" = ngày trung vị 3,2, cả 3 seed có trong 365 ngày; "—" = không seed nào có. **Không kiểu nào có Aurorawing** (tuyến tắt). **Amberhorn chỉ đến với người lai nhiều** (Sưu tập, Lai giống: 33–100 % seed); Guided / Casual / Nghiên cứu / Thư giãn không bao giờ có (lai có chủ đích cùng loài hoặc không lai). Codex tối đa trong mô phỏng production: 15 / 16.

## 4. Bế tắc

| # | Trường hợp | Có bế tắc? | Bằng chứng (đã chạy) |
|---|---|---|---|
| D1 | Aurorawing khi bảng `sky_cloud` tắt | **có, theo thiết kế chờ duyệt**: không nguồn nào | `Aurorawing_first_copy_comes_only_from_the_approved_15th_sky_trip_egg_while_the_route_table_is_off` |
| D2 | Bán Aurorawing duy nhất (khi bảng bật) | **có, vĩnh viễn**: trứng không trao lại (30 chuyến thêm: 0 trứng), không cặp lai nào; Bách khoa vẫn giữ | `Deadlock_selling_the_only_aurorawing_loses_the_species_for_good_but_keeps_the_codex` |
| D3 | Loài chỉ ở Thủy Vực mua trước khi mở | không: chờ trong kho, vẫn lớn | `Ocean_only_species_bought_before_the_ocean_opens_wait_and_grow_in_storage_no_deadlock` |
| D4 | Kho đầy 500 khi nhận trứng thưởng | không: vào `rewardQueue`, giao khi có chỗ; gói bị từ chối, không trừ vàng | `Storage_full_never_loses_a_reward_egg_and_packs_are_refused_instead_of_dropped`, `SAVE_RELIABILITY.md` |
| D5 | Bán hết Amberhorn | không: lai lại được từ Magma (gói) | ma trận mục 1 |
| D6 | Bán rồng cuối cùng | không: luật chặn `last-dragon` | luật hiện hành |
| D7 | Bộ sưu tập dừng ở 15 / 16 với người không lai | không phải bế tắc: Amberhorn cần lai có chủ đích | mục 3 |

## 5. Tình trạng từng loài

| Trạng thái | Loài |
|---|---|
| Có thể thu thập (production) | emberling, droplet, sprout, zephyr, steamdrake, magma, sparkwing, frostfin, mossback, blossom, solaris, leviathan, amberhorn, celestia, gaia |
| Chưa thể thu thập (production) | aurorawing — luật trứng đã duyệt, tuyến `sky_cloud` chưa kích hoạt |
| Thu thập được khi bảng A được duyệt | cả 16 (kiểm toán catalog + hành trình 40 ngày có bảng A trong phạm vi) |
