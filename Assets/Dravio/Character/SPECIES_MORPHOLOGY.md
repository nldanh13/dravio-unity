# Đặc điểm hình thể 16 loài rồng (tự sinh – không sửa tay)

Sinh bởi `scripts/art/character/build_species_spec.py` từ `species-biology.json` và `SpeciesCatalog.cs`. Cột "Gốc" chép nguyên văn; cột "Yêu cầu mô hình" chỉ là hệ quả trực tiếp của trường gốc. Không có dữ liệu hình thể theo giai đoạn trong nguồn, nên các ô giai đoạn ghi **chưa xác định**.

| Loài | Silhouette (gốc) | Di chuyển | Bề mặt | Hô hấp | BodyStyle | Chân đi | Cánh bay | Số chi |
|---|---|---|---|---|---|---|---|---|
| `emberling` | Bốn chân, đuôi lửa tròn, không cánh. | walk | scale | lung | Round | có | không | 4 |
| `droplet` | Thân tròn, vây tai, đuôi dẹt, màng chân. | walk, swim | scale | dual | Round | có | không | chưa nêu |
| `sprout` | Mầm lá, bốn chân, đuôi lá. | walk, burrow | scale | lung | Round | có | không | 4 |
| `zephyr` | Cánh lông rộng, đuôi mây, chân đậu. | walk, fly | fur | lung | Winged | có | có | chưa nêu |
| `steamdrake` | Thân dài, vây sống lưng, chi ngắn. | walk, swim | scale | dual | Serpent | có | không | chưa nêu |
| `magma` | Giáp đá, sừng ngắn, chân chắc. | walk, burrow | armor | lung | Horned | có | không | chưa nêu |
| `sparkwing` | Cánh góc cạnh, đuôi tia điện. | walk, fly | scale | lung | Winged | có | có | chưa nêu |
| `frostfin` | Vây ngực lớn như cánh; không chân đi, không bay. | swim | scale | gill | Winged | không | không | chưa nêu |
| `mossback` | Mai rêu, chi bè, vây đuôi. | walk, swim | armor | dual | Round | có | không | chưa nêu |
| `blossom` | Cánh cánh hoa, đuôi dài, chân đậu. | walk, fly | scale | lung | Winged | có | có | chưa nêu |
| `solaris` | Mào lửa, cánh lông lớn, đuôi quạt. | walk, fly | fur | lung | Winged | có | có | chưa nêu |
| `leviathan` | Thân biển dài, vây san hô, không chi đất. | swim | scale | gill | Serpent | không | không | chưa nêu |
| `amberhorn` | Sừng hổ phách, giáp lưng, bốn chân. | walk, burrow | armor | lung | Horned | có | không | 4 |
| `aurorawing` | Cánh dải sáng, lông cổ, đuôi cực quang. | walk, fly | fur | lung | Winged | có | có | chưa nêu |
| `celestia` | Cánh sao, sừng tinh thể, đuôi sao. | walk, fly | scale | lung | Winged | có | có | chưa nêu |
| `gaia` | Sừng cây, giáp đá-rêu, chân màng và vây. | walk, swim | armor | dual | Horned | có | không | chưa nêu |

## Ba giai đoạn

Mốc tăng trưởng (canonical, Lifecycle.cs): Baby < 35 %, Juvenile 35–100 %, Adult = 100 % thời gian trưởng thành.

| Loài | Thời gian trưởng thành | Juvenile từ | Baby | Juvenile | Adult |
|---|---|---|---|---|---|
| `emberling` | 5 phút | 105 s | prototype GLB/procedural (chưa phải asset cuối) | chưa xác định | chưa xác định |
| `droplet` | 6 phút | 126 s | chưa xác định | chưa xác định | chưa xác định |
| `sprout` | 8 phút | 168 s | chưa xác định | chưa xác định | chưa xác định |
| `zephyr` | 8 phút | 168 s | chưa xác định | chưa xác định | chưa xác định |
| `steamdrake` | 25 phút | 525 s | chưa xác định | chưa xác định | chưa xác định |
| `magma` | 30 phút | 630 s | chưa xác định | chưa xác định | chưa xác định |
| `sparkwing` | 35 phút | 735 s | chưa xác định | chưa xác định | chưa xác định |
| `frostfin` | 30 phút | 630 s | chưa xác định | chưa xác định | chưa xác định |
| `mossback` | 25 phút | 525 s | chưa xác định | chưa xác định | chưa xác định |
| `blossom` | 35 phút | 735 s | chưa xác định | chưa xác định | chưa xác định |
| `solaris` | 120 phút | 2520 s | chưa xác định | chưa xác định | chưa xác định |
| `leviathan` | 150 phút | 3150 s | chưa xác định | chưa xác định | chưa xác định |
| `amberhorn` | 150 phút | 3150 s | chưa xác định | chưa xác định | chưa xác định |
| `aurorawing` | 180 phút | 3780 s | chưa xác định | chưa xác định | chưa xác định |
| `celestia` | 360 phút | 7560 s | chưa xác định | chưa xác định | chưa xác định |
| `gaia` | 480 phút | 10080 s | chưa xác định | chưa xác định | chưa xác định |

## Câu hỏi mở cần art director / GDD trả lời

- `emberling`: Tỉ lệ, kích thước và chi tiết riêng từng giai đoạn Baby/Juvenile/Adult chưa có trong dữ liệu gốc.
- `droplet`: Tỉ lệ, kích thước và chi tiết riêng từng giai đoạn Baby/Juvenile/Adult chưa có trong dữ liệu gốc.
- `sprout`: Tỉ lệ, kích thước và chi tiết riêng từng giai đoạn Baby/Juvenile/Adult chưa có trong dữ liệu gốc.
- `zephyr`: Tỉ lệ, kích thước và chi tiết riêng từng giai đoạn Baby/Juvenile/Adult chưa có trong dữ liệu gốc.
- `steamdrake`: Tỉ lệ, kích thước và chi tiết riêng từng giai đoạn Baby/Juvenile/Adult chưa có trong dữ liệu gốc.
- `magma`: Tỉ lệ, kích thước và chi tiết riêng từng giai đoạn Baby/Juvenile/Adult chưa có trong dữ liệu gốc.
- `sparkwing`: Tỉ lệ, kích thước và chi tiết riêng từng giai đoạn Baby/Juvenile/Adult chưa có trong dữ liệu gốc.
- `frostfin`: BodyStyle=Winged nhưng locomotion không có "fly" (silhouette: "Vây ngực lớn như cánh; không chân đi, không bay."). Mô hình theo silhouette (vây dạng cánh), không làm cánh bay; cần xác nhận.
- `frostfin`: Tỉ lệ, kích thước và chi tiết riêng từng giai đoạn Baby/Juvenile/Adult chưa có trong dữ liệu gốc.
- `mossback`: Tỉ lệ, kích thước và chi tiết riêng từng giai đoạn Baby/Juvenile/Adult chưa có trong dữ liệu gốc.
- `blossom`: Tỉ lệ, kích thước và chi tiết riêng từng giai đoạn Baby/Juvenile/Adult chưa có trong dữ liệu gốc.
- `solaris`: Tỉ lệ, kích thước và chi tiết riêng từng giai đoạn Baby/Juvenile/Adult chưa có trong dữ liệu gốc.
- `leviathan`: Tỉ lệ, kích thước và chi tiết riêng từng giai đoạn Baby/Juvenile/Adult chưa có trong dữ liệu gốc.
- `amberhorn`: Tỉ lệ, kích thước và chi tiết riêng từng giai đoạn Baby/Juvenile/Adult chưa có trong dữ liệu gốc.
- `aurorawing`: Tỉ lệ, kích thước và chi tiết riêng từng giai đoạn Baby/Juvenile/Adult chưa có trong dữ liệu gốc.
- `celestia`: Tỉ lệ, kích thước và chi tiết riêng từng giai đoạn Baby/Juvenile/Adult chưa có trong dữ liệu gốc.
- `gaia`: Tỉ lệ, kích thước và chi tiết riêng từng giai đoạn Baby/Juvenile/Adult chưa có trong dữ liệu gốc.

## Bảng màu tham chiếu (gốc)

| Loài | primary | secondary | belly | accent |
|---|---|---|---|---|
| `emberling` | `#D86B47` | `#A83D36` | `#F9D59A` | `#FFD66C` |
| `droplet` | `#61BBD0` | `#337FA4` | `#C7F2EE` | `#86E5DE` |
| `sprout` | `#75B57D` | `#397C58` | `#E3EAC0` | `#E7B864` |
| `zephyr` | `#A6C8E6` | `#6388BA` | `#EDF4FC` | `#F5D999` |
| `steamdrake` | `#92BFC0` | `#527E83` | `#E5E5CB` | `#ECAD78` |
| `magma` | `#9F5544` | `#523B48` | `#F1A55C` | `#FFD261` |
| `sparkwing` | `#DBB65A` | `#96703B` | `#F8EAC6` | `#F7F08A` |
| `frostfin` | `#8ABADD` | `#4B75AB` | `#E0F5FF` | `#BDEDF4` |
| `mossback` | `#829D66` | `#506C4C` | `#D6D7A2` | `#A7CD84` |
| `blossom` | `#DC9FB8` | `#A86B94` | `#F9E1D5` | `#B7D99A` |
| `solaris` | `#ED874B` | `#AC4B3D` | `#FBE1A0` | `#FFE47F` |
| `leviathan` | `#427C9A` | `#27526E` | `#C7E4D7` | `#E38F86` |
| `amberhorn` | `#C58C45` | `#855B39` | `#F3DCA4` | `#FFE08A` |
| `aurorawing` | `#879BD5` | `#5F5B9E` | `#DDE6FA` | `#8DE6CF` |
| `celestia` | `#7776B5` | `#45446F` | `#D4D2EC` | `#E9D48C` |
| `gaia` | `#629B7D` | `#385D57` | `#DDE5B6` | `#E8C97F` |
