# ADR 0209 — U2.13: vật liệu dự án Sanctuary theo thế giới (đã duyệt), không bế tắc khi thế giới chưa mở

- **Trạng thái:** PR riêng, xếp chồng trên ADR 0208 (sky_cloud). Không merge `main` khi chưa duyệt. Domain C# thuần; không chạm Unity Presentation, Character, Environment.
- **Đã duyệt:** Khởi Nguyên dùng gỗ + hạt giống, Thủy Vực dùng pha lê + san hô, Thiên Giới dùng bụi sao.
- **Phụ thuộc chưa duyệt:** bụi sao chỉ có nguồn khi bảng `sky_cloud` (ADR 0208) được duyệt. Khi bảng tắt, dự án Thiên Giới lùi về pha lê + san hô.
- **Save:** Schema **v10 giữ nguyên**, không migration. Chi phí được tính từ save, không lưu; dự án đã xây không bao giờ tính lại.

## Giữ nguyên

- 60 dự án, giá vàng `floor(20 000 × 1,25ⁿ)` G (dự án cuối 10 440 487 148 G).
- Công thức số lượng E5: 0 cho hai dự án đầu, rồi `1 + n / 5` mỗi loại vật liệu.
- Tổng số đơn vị vật liệu cho 60 dự án: **776** trong mọi trạng thái (thế giới một vật liệu — bụi sao — cần gấp đôi một loại thay vì hai loại).
- Dự án chỉ thẩm mỹ: không vàng, không XP, không mở khóa gameplay.

## Quyết định

| # | Nội dung | Code |
|---|---|---|
| M1 | Vật liệu của dự án n theo thế giới của công trình (`SanctuaryProjectContent.Get(n).ZoneId`): origin → wood/seed, ocean → crystal/coral, sky → stardust (×2) | `Catalog/SanctuaryProjectCatalog.cs` (`SanctuaryMaterialRules.For`) |
| M2 | **Không bế tắc (P1, đang dùng):** nếu thế giới của công trình chưa mở, hoặc chưa có tuyến thám hiểm mở và có bảng thưởng cho đủ vật liệu của nó (`Sourced`), lùi sky → ocean → origin. Khởi Nguyên luôn có nguồn | như trên |
| M3 | Luật là dữ liệu: `SanctuaryMaterialRules.Production = PerWorld`; `LegacyWoodSeed` (U2.10) và `PerWorldStrict` (P2) chỉ dùng trong test/mô phỏng qua `SanctuaryMaterialRules.Use(...)` theo luồng | như trên |
| M4 | `BuildSanctuaryProjectCommand` trừ vật liệu theo M1/M2; event `sanctuaryProjectBuilt` thêm `MaterialIds` (constructor cũ vẫn có, mặc định wood/seed) | `Economy/U210Rules.cs`, `Core/U210Commands.cs` |
| M5 | API: `SanctuaryEconomyDto.NextProjectMaterials` (cần / có / thiếu), `NextProjectMaterialIds` động, `NextProjectMaterialEach` theo thế giới, `NextProjectMaterialZoneId`, `NextProjectMaterialFallback`; truy vấn mới `ProgressionQueries.SanctuaryProjectCosts(s)` | `Queries/ProgressionQueries.cs` |

## Phương án không bế tắc đã so sánh

| Phương án | Mô tả | Kết quả kiểm chứng |
|---|---|---|
| **P1 lùi về thế giới thấp hơn (khuyến nghị, đã làm)** | dự án của thế giới chưa mở/chưa có nguồn dùng vật liệu thế giới thấp hơn; mở thế giới chỉ đổi chi phí của dự án **chưa** xây | 60/60 dự án xây được ở mọi trạng thái thế giới (test `Every_one_of_the_60_projects_is_buildable…`) |
| P2 nghiêm ngặt | dự án chỉ dùng vật liệu đúng thế giới | bế tắc ở dự án #9 khi Thủy Vực chưa mở (test `Strict_per_world_rule_would_deadlock_at_project_9…`); người chơi Casual 60 phút/ngày chưa mở Thiên Giới bị kẹt vĩnh viễn |
| P3 trộn | dự án thế giới chưa mở cần một nửa vật liệu thế giới thấp hơn + một nửa thế giới đúng | vẫn bế tắc như P2 khi thế giới chưa mở; không làm |

## Hệ quả

- Nhu cầu 60 dự án: chỉ Khởi Nguyên 388 gỗ / 388 hạt; + Thủy Vực 128 / 128 / 260 pha lê / 260 san hô; + Thiên Giới (bảng A) 128 / 128 / 125 / 125 / 270 bụi sao.
- Pha lê và san hô có chỗ tiêu (giải F12 của ADR 0207 một phần). Vàng vẫn là giới hạn số dự án mỗi năm (xem `Docs/u2.13/SANCTUARY_MATERIALS.md`).
- UI phải đọc vật liệu từ DTO, không hard-code wood/seed.

## Kiểm thử

`U213SanctuaryMaterialTests` (7), `U213SanctuarySimulationTests` (60 ngày Guided + báo cáo trước/sau).
