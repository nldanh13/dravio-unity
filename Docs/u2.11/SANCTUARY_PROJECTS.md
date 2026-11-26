# U2.11 — Nội dung dự án trang trí Sanctuary

> Luật E5 (ADR 0205) **không đổi**: dự án thứ n (từ 0) giá floor(20 000 × 1,25ⁿ) G; từ dự án thứ 3 thêm 1 + n / 5 gỗ và cùng số hạt giống; tối đa 60; chỉ thẩm mỹ.
> U2.11 chỉ thêm **nội dung hiển thị** (`Domain/Catalog/SanctuaryProjectContent.cs`). Save chỉ lưu số dự án đã xây, nên đổi tên / thứ tự nội dung không làm hỏng save.

## Nguyên tắc

- Đi theo hành trình: phục dựng đền cổ Khởi Nguyên trước, rồi rạn san hô Thủy Vực, rồi đảo mây Thiên Giới.
- Công trình của thế giới chưa mở được đặt ở Khởi Nguyên (`DisplayZone`), không bao giờ biến mất; khi thế giới mở, Sanctuary có thể dời về đúng chỗ.
- Mỗi công trình có kiểu (`landmark`, `path`, `garden`, `light`, `water`, `vista`) để Sanctuary chọn prefab và điểm neo; dùng lại bộ asset môi trường U1-ENV (cổng đá, cột đá, tượng rồng, đá rune, hồ nhỏ, cây cổ thụ).
- Năm mẫu trang trí từ nhiệm vụ đổi kiểu dáng của năm công trình (biến thể hình ảnh, không hiệu ứng).

## Danh sách

| # | ID | Tên (nháp) | Thế giới | Kiểu | Giá (G) | Gỗ + hạt (mỗi loại) | Biến thể từ mẫu |
|---|---|---|---|---|---|---|---|
| 1 | `origin_mossy_steps` | Bậc đá rêu phong | Khởi Nguyên | path | 20 000 | 0 | — |
| 2 | `origin_lantern_row` | Hàng đèn đá dẫn lối | Khởi Nguyên | light | 25 000 | 0 | `blueprint.forest_lantern` |
| 3 | `origin_meadow_arbor` | Giàn hoa đồng cỏ | Khởi Nguyên | garden | 31 250 | 1 | `blueprint.meadow_arbor` |
| 4 | `origin_trail_shrine` | Miếu nhỏ bên lối mòn | Khởi Nguyên | landmark | 39 062 | 1 | `blueprint.trail_marker` |
| 5 | `origin_lily_pond` | Ao sen bờ hồ | Khởi Nguyên | water | 48 828 | 1 | — |
| 6 | `origin_rune_circle` | Vòng đá rune | Khởi Nguyên | light | 61 035 | 2 | — |
| 7 | `origin_ancient_gate` | Cổng đá cổ phục dựng | Khởi Nguyên | landmark | 76 293 | 2 | — |
| 8 | `origin_elder_statue` | Tượng rồng tổ | Khởi Nguyên | landmark | 95 367 | 2 | — |
| 9 | `ocean_tide_pools` | Vũng triều | Thủy Vực | water | 119 209 | 2 | `blueprint.tide_pool` |
| 10 | `ocean_coral_arch` | Vòm san hô | Thủy Vực | landmark | 149 011 | 2 | — |
| 11 | `ocean_shell_path` | Lối vỏ sò | Thủy Vực | path | 186 264 | 3 | — |
| 12 | `ocean_kelp_garden` | Vườn tảo bẹ | Thủy Vực | garden | 232 830 | 3 | — |
| 13 | `ocean_glow_buoys` | Phao phát sáng | Thủy Vực | light | 291 038 | 3 | — |
| 14 | `ocean_sunken_shrine` | Đền chìm | Thủy Vực | landmark | 363 797 | 3 | — |
| 15 | `sky_cloud_bridge` | Cầu mây | Thiên Giới | path | 454 747 | 3 | — |
| 16 | `sky_cloud_garden` | Vườn trên mây | Thiên Giới | garden | 568 434 | 4 | `blueprint.cloud_garden` |
| 17 | `sky_wind_chimes` | Chuông gió | Thiên Giới | light | 710 542 | 4 | — |
| 18 | `sky_falling_spring` | Suối rơi | Thiên Giới | water | 888 178 | 4 | — |
| 19 | `sky_star_observatory` | Đài ngắm sao | Thiên Giới | landmark | 1 110 223 | 4 | — |
| 20 | `sky_aurora_spire` | Tháp cực quang | Thiên Giới | landmark | 1 387 778 | 4 | — |
| 21–60 | `vista_<thế giới>_<k>` | Mở rộng cảnh quan k | xoay vòng origin → ocean → sky | vista | 1,73 M → ≈ 1,04 × 10¹⁰ | 5 → 12 | — |

Tổng 8 dự án Khởi Nguyên ≈ 0,40 M G; 14 dự án đầu ≈ 1,74 M G; 20 dự án ≈ 6,9 M G. Mô phỏng U2.10 (50 seed) cho người chơi thường 23–25 dự án sau 90 ngày, nên chương Thiên Giới hoàn tất khoảng cuối tháng thứ ba và phần "Mở rộng cảnh quan" là mục tiêu dài hạn.

## Chưa làm (chờ duyệt)

- Công thức dùng pha lê / san hô (Thủy Vực) và "bụi sao" (Thiên Giới) cho chương 2–3: cần đổi luật E5 đã duyệt, nên chỉ ghi ở đây làm đề xuất.
- Tên tiếng Việt chính thức và hình ảnh từng công trình.
