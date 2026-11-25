# ADR 0201 — U2.8: mở thế giới, thám hiểm, vật liệu, kho trung lập, trang trí

- **Trạng thái:** Đã chấp nhận. Chủ dự án đã phê duyệt.
- **Phạm vi:** Bản Unity (`nldanh13/dravio-unity`), giai đoạn U2.8. Mọi phần dưới đây là **implementation mới theo GDD**; bản TS gốc (b33ec7a) chưa có các luật này.
- **Liên quan:** ADR 0010 mục 4 của bản TS (nối `canTravel` khi mở realm), GDD tr. 5 (thế giới) và tr. 22 (thám hiểm).
- **Nguyên tắc giữ nguyên:** Dravio **không có quảng cáo** và không có phần thưởng quảng cáo. Không đổi chỉ số cân bằng, ID hay công thức kinh tế đã có.

## Bối cảnh

Bản nháp đầu của U2.8 để lại 8 câu hỏi thiết kế (Domain README mục 5d). ADR này ghi lại câu trả lời của chủ dự án để GDD, Domain và Unity thống nhất một nguồn.

## Quyết định

### 1. Mở thế giới

| Thế giới | Điều kiện (phải đủ tất cả) | Phần thưởng |
|---|---|---|
| Thủy Vực (`ocean`) | Hướng dẫn Khởi Nguyên hoàn tất (`tutorial.status = completed`); cả 3 sinh cảnh Khởi Nguyên đã mở; nhiệm vụ `QUEST_AQUATIC_EXPLORATION` hoàn thành | 1 trứng Droplet cố định, biến thể Normal (`ZONE_EGG_OCEAN`) |
| Thiên Giới (`sky`) | Thủy Vực đã mở; nhiệm vụ `QUEST_SKY_EXPLORATION` hoàn thành | 1 trứng Zephyr cố định, biến thể Normal (`ZONE_EGG_SKY`) |

- Không có điều kiện cấp độ hay phí vàng. Không yêu cầu sở hữu loài.
- Người chơi mở bằng lệnh `unlockZone(zoneId)`. Khi mở, toàn bộ microhabitat của thế giới đó cũng mở.
- **Nội dung nhiệm vụ: TBD.** Hai nhiệm vụ nằm trong cấu hình riêng (`WorldUnlockCatalog.Quests`, `objective = "TBD"`). Trong lúc chơi bình thường chưa có gì hoàn thành chúng. Hàm engine `WorldExpansion.ApplyQuestComplete` để dành cho hệ nhiệm vụ sau này.
- **Trứng thưởng:** mỗi phần thưởng chỉ trao một lần (`grantedRewards`). Trứng cố định không tính pity và không có quảng cáo. Nếu kho đầy hoặc đang có trứng chờ nhận, quyền nhận trứng được giữ trong `rewardQueue` và giao sau lệnh thành công đầu tiên có đủ chỗ. Trứng không bao giờ mất và không bao giờ bị nhân đôi, kể cả qua lưu/nạp hay offline.

### 2. Thám hiểm thành công 100 % (MVP)

Không có rồng chết, không mất vật phẩm ngẫu nhiên. Trường `successPermille` tồn tại trong catalog (= 1000) nhưng engine chưa đọc. Muốn bật tỷ lệ thất bại thì cần một ADR mới.

### 3. Vật liệu

- ID chuẩn: `wood`, `seed`, `stone`, `crystal`, `coral`. Chỉ vật liệu có nguồn mới được kích hoạt. `stone` chưa có nguồn nên chưa kích hoạt.
- "2–4 gỗ/hạt giống" nghĩa là **tổng 2–4 đơn vị** mỗi chuyến, chọn bằng RNG tất định (`hashSeed("expedition", saveId, revision)`). Mỗi đơn vị chọn đều trong danh sách vật liệu của tuyến.
- Chỉ làm kho vật liệu và API truy vấn (`Ecosystem.Materials`). Chưa có công thức chế tạo hay chi phí nào dùng vật liệu.
- ID tuyến ổn định, nằm trong catalog trung tâm, không phụ thuộc tên hiển thị.

| Tuyến | Vật liệu |
|---|---|
| `origin_forest` | `wood`, `seed` |
| `ocean_reef` | `crystal`, `coral` |
| `sky_cloud` | *chưa có bảng thưởng được duyệt* |

GDD ghi "bụi sao" nhưng chưa có ID chuẩn, nên **tuyến `sky_cloud` bị khóa** với lỗi `route-rewards-undefined` cho tới khi bảng thưởng được duyệt.

### 4. Điều kiện sức khỏe khi thám hiểm

Ngưỡng: Health ≥ 80, Mobility ≥ 80, Comfort ≥ 60, không có ca chăm sóc đang mở, không bận việc khác. Tuyến trời còn yêu cầu bay.

Dữ liệu sức khỏe của U2.6 chưa có trên nhánh này. Vì vậy 4 điều kiện đầu được báo là **chưa kiểm được** (`ExpeditionCandidate.UncheckedConditions`), không giả là đạt. Điều kiện "không bận" và kiểu di chuyển được kiểm đầy đủ.

### 5. Chỗ ở khi thám hiểm và kho trung lập

- Rồng đi thám hiểm giữ slot, không sản xuất, không tham gia việc khác, rồi quay về đúng sinh cảnh cũ.
- **Kho là vùng trung lập.** Kho không thuộc Khởi Nguyên, Thủy Vực hay Thiên Giới, không sinh vàng, và không dùng làm điểm xuất phát Khởi Nguyên khi tìm đường.
- Rời kho phải qua đủ luật đặt chỗ, sinh học, sức chứa và khóa thế giới. Ngoài ra phải có ít nhất một cạnh WorldGraph đang mở đi vào thế giới đích mà rồng đi được (bơi cho Thủy Vực, bay cho Thiên Giới). Thế giới gốc (Khởi Nguyên) không có cạnh đi vào nên luôn vào được. Nhờ vậy kho không thể dùng để vượt giới hạn di chuyển.

### 6. Trang trí

Chỉ có tác dụng thẩm mỹ: không ảnh hưởng vàng, XP, Health, Mobility, Comfort hay sức chứa. Catalog có trường `effects`, mọi giá trị mặc định 0, và engine chưa đọc.

## Hệ quả

- Lệnh mới: `unlockZone`, được lưu ngay. Event mới: `questCompleted`, `rewardEggGranted`, `rewardEggDelivered`. Mã lỗi mới: `route-rewards-undefined`, `unknown-zone`.
- Save Schema v7 (chỉ bản Unity, chưa phát hành) thêm `completedQuests`, `grantedRewards`, `rewardQueue`. Migration v6 → v7 đặt cả ba là mảng rỗng. Validator từ chối nhiệm vụ hoặc thưởng lạ hay trùng, thưởng của vùng chưa mở, và hàng chờ chứa thưởng chưa trao, bị trùng, hoặc trùng với trứng đang chờ nhận.
- Ảnh v4 cho TS bỏ các khóa trên. Khi hàng chờ không rỗng thì không xuất ảnh v4.
- Còn mở, cần ADR sau: nội dung hai nhiệm vụ; bảng thưởng `sky_cloud` và ID "bụi sao"; nguồn của `stone`; công thức dùng vật liệu; tỷ lệ thất bại; kiểm sức khỏe sau khi U2.6 được gộp; điểm nghiên cứu từ thám hiểm.
