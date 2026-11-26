# U2.12 — Xác thực tiến trình và hệ sinh thái

> Quyết định: [ADR 0207](../adr/0207-u2-12-economy-ecosystem-validation.md). Kinh tế dài hạn: [ECONOMY_365.md](ECONOMY_365.md). Đề xuất Thiên Giới: [SKY_CLOUD_PROPOSAL.md](SKY_CLOUD_PROPOSAL.md). API: [API_INTEGRATION.md](API_INTEGRATION.md).
> Không đổi luật, không đổi save (v10). Mọi số liệu dưới đây lấy từ code và test của nhánh `feat/u2-12-ecosystem-validation`.

## 1. Kiểm tra Domain và Save Schema v10

| Hạng mục | Kết quả |
|---|---|
| `QuestProposals.ValidateCatalog()` | rỗng (17 nhiệm vụ, thưởng không trùng, không vàng) |
| Dispatcher | mọi lệnh chạy trên bản sao (`DeepClone`), lệnh bị từ chối trả state cũ; `completeQuest` trừ vật liệu trong cùng transaction nên lỗi giữa chừng không mất vật liệu |
| Save v10 của hành trình ba thế giới thật | lưu–nạp trùng từng byte canonical, validator hợp lệ, sau khi nạp không nhận lại được nhiệm vụ / trứng thế giới (`JourneyEndStateSaveTests`) |
| Ảnh v4 cho TS | không biểu diễn được chuyến thám hiểm đang chạy (giới hạn U2.8), nên save giữa hành trình chỉ xuất `.unity.json` |
| Vàng | bảo toàn: vàng = 150 + sản xuất + bán + thành tựu − gói − lai − nâng cấp − dự án (sai số làm tròn ≤ 8 G sau 30 ngày, 3 kiểu người chơi) |

## 2. Kiểm toán tĩnh: không có bế tắc

`ProgressionAudit.Run()` lặp tới điểm cố định trên catalog thật: thế giới mở → loài sở hữu được (gói ở mọi thế giới, Emberling khởi đầu, Sproutling hướng dẫn, trứng thế giới, lai theo `BreedingRules.CandidateSpecies`) → loài đặt được (sinh cảnh mở, suitability cho phép, có kiểu di chuyển vào thế giới theo WorldGraph) → yêu cầu nhiệm vụ → nhiệm vụ → điều kiện `unlockZone`.

| Đợt | Đạt được |
|---|---|
| 0 | `Q_ORIGIN_FIELD_NOTES`, `Q_ORIGIN_FOREST_SURVEY`, `Q_CONSERVE_AT_HOME`, `Q_SOCIAL_FIRST_FRIENDS` |
| 1 | `QUEST_AQUATIC_EXPLORATION`, `Q_RESEARCH_NIGHT_WATCH`, `Q_RESEARCH_PERSONALITIES`, `Q_SOCIAL_CLOSE_BOND`, `Q_EXPEDITION_PATHFINDER` |
| 2 | `Q_RESEARCH_ETHOLOGIST`, **Thủy Vực** |
| 3 | `QUEST_SKY_EXPLORATION`, `Q_RESEARCH_TIDE_JOURNAL`, `Q_CONSERVE_TIDES`, `Q_EXPEDITION_REEF` |
| 4 | **Thiên Giới** |
| 5 | `Q_RESEARCH_SKY_CHRONICLE`, `Q_CONSERVE_SKIES` |
| không bao giờ | `Q_EXPEDITION_CLOUD` — `routeAvailable:route-rewards-undefined` (luật chưa duyệt, không phải vòng lặp) |

Kết luận:

- **Không vòng lặp điều kiện.** Mỗi nhiệm vụ cổng chỉ dùng nội dung của thế giới đã mở trước nó. Cổng Thủy Vực cần Adult biết bơi, cổng Thiên Giới cần Adult biết bay; cả Droplet (bơi) lẫn Zephyr (bay) đều có trong gói Starter và sống được ở Khởi Nguyên, nên không cần trứng của chính thế giới đang mở.
- **Vật liệu không gây bế tắc.** Vật liệu giao cho nhiệm vụ (gỗ, hạt, pha lê, san hô) đều có tuyến thám hiểm lặp lại, không phí, ở thế giới đã mở trước nhiệm vụ. Dự án Sanctuary tiêu cùng vật liệu nhưng không thể "khóa" nhiệm vụ: người chơi luôn đi thám hiểm thêm được.
- **Vàng không gây bế tắc.** Không nhiệm vụ nào cần vàng. Không bán được rồng cuối cùng (`last-dragon`), nên luôn còn ít nhất một rồng sinh vàng khi đặt vào sinh cảnh.
- **Hướng dẫn không gây bế tắc.** Bỏ qua hướng dẫn thì Thủy Vực báo `tutorial-completed`; `setTutorialStatus(active)` ghi lại ngay các bước đã thỏa (test `Skipped_tutorial_…`). `Journey` hiển thị bước `resume-tutorial`.
- **Bẫy mềm (có đường thoát):** bán Emberling trước khi khảo sát đá ấm → phải có lại Emberling (gói Starter, 22,5 % mỗi gói). Bán rồng duy nhất đã được cho ăn rồi "xem lại hướng dẫn" → phải cho ăn lại để ghi TUT_02.

## 3. Loài và sinh cảnh

| Microhabitat | Thế giới | Loài sống được (suitability) |
|---|---|---|
| `origin_meadow` | Khởi Nguyên | emberling, sprout, zephyr, sparkwing, blossom, solaris, amberhorn, aurorawing, celestia |
| `origin_pondbank` | Khởi Nguyên | droplet, sprout, steamdrake, mossback, gaia |
| `origin_warm_rocks` | Khởi Nguyên | emberling, magma, solaris, amberhorn |
| `ocean_estuary` | Thủy Vực | droplet, steamdrake, mossback, gaia |
| `ocean_reef` | Thủy Vực | leviathan |
| `ocean_cool_pool` | Thủy Vực | frostfin |
| `sky_low_island` | Thiên Giới | zephyr, sparkwing, blossom, solaris, aurorawing, celestia |

- Mọi cặp (loài, sinh cảnh bản địa) đều cho điểm 100 (Good). Suitability hiện là "sống được / không", chưa có độ dốc, nên nhiệm vụ bảo tồn thực chất là "đặt đúng sinh cảnh bản địa". Muốn bảo tồn có chiều sâu (chất lượng nước, thảm thực vật, trang trí) cần ADR riêng.
- Rạn san hô và hồ lạnh mỗi nơi chỉ có một loài (Leviathan Epic, Frostfin Rare): đa dạng Thủy Vực phụ thuộc gói Elemental / Mystic hoặc lai nhóm A.
- Mỗi thế giới 5 chỗ. Thủy Vực có 6 loài, Thiên Giới 6 loài: không đủ chỗ cho mọi loài cùng lúc, người chơi phải chọn (đúng ý "Sanctuary sống", không phải bế tắc: nhiệm vụ cần tối đa 3 cư dân mỗi thế giới).
- Mọi tập tính quan sát được của mọi loài đều làm được ở ít nhất một sinh cảnh loài đó sống được (test `Every_observable_behaviour_…`), nên mọi loài sở hữu được đều đạt Ethology được. Một số tập tính chỉ có ở một nơi: Steamdrake / Droplet chỉ tắm nắng ở bờ hồ (Khởi Nguyên), không phải ở cửa sông.

### Loài không sở hữu được (F1)

Kiểm toán cho thấy **Aurorawing** là loài duy nhất không bao giờ có được: không có trong gói, và nhóm V (Zephyr, Sparkwing, Blossom, Solaris, Celestia) không loài nào có nguyên tố Nước nên không lai ra Aurorawing (Nước + Gió). Amberhorn cũng ngoài gói nhưng lai được (nhóm T). Hệ quả: `ACH_CODEX_16` (2 000 G), `title.dragon_scholar`, `title.lifecycle_master`, `title.grand_ethologist` không đạt được. Phương án ở ADR 0207 F1 và `SKY_CLOUD_PROPOSAL.md`.

## 4. Hành trình mô phỏng từ save mới tới ba thế giới

Bộ mô phỏng dùng `CommandDispatcher` thật, 30 s một bước trong phiên, offline giữa các phiên. Hai kiểu người chơi mới:

- **Guided:** như Researcher (quan sát mỗi 30 s, cho ăn mỗi phiên, thám hiểm, nhận nhiệm vụ), cộng thêm làm theo `ProgressionQueries.Journey`: tiếp tục hướng dẫn nếu bỏ qua, mua thêm gói khi hướng dẫn báo thiếu loài, đặt Adult biết bơi / bay và cư dân Thủy Vực / Thiên Giới theo yêu cầu, chuyển rồng sang sinh cảnh có tập tính còn thiếu, chọn loại thức ăn để đúng rồng mục tiêu được ăn, lai một cặp và để con cùng sinh cảnh với bố/mẹ.
- **Casual:** 1 gói/ngày, chỉ làm bước rẻ của hướng dẫn (xem hồ sơ, khảo sát, đặt Adult cần cho cổng), quan sát một lần đầu phiên, một bữa ăn mỗi phiên (xoay vòng rồng), thám hiểm, nhận nhiệm vụ, mở thế giới.

Kết quả 365 ngày (3 seed mỗi ô) ở [ECONOMY_365.md](ECONOMY_365.md) mục "Hành trình".

## 5. Nhiệm vụ: hoàn thành hợp lệ

`Guided_player_opens_three_worlds_and_completes_every_approved_quest_through_real_commands` kiểm trên một hành trình thật:

- 16/17 nhiệm vụ hoàn thành, chỉ `Q_EXPEDITION_CLOUD` vẫn khóa.
- Tiền đề luôn nhận trước; thế giới luôn mở trước nhiệm vụ của nó; nhiệm vụ cổng luôn nhận trước khi `unlockZone`.
- Trứng Droplet và Zephyr trao đúng một lần. Vật liệu không âm. State hợp lệ.
- `Q_SOCIAL_CLOSE_BOND` đạt được bằng lai + đặt con cùng microhabitat với bố/mẹ (60 + 20 = 80). Hai rồng không họ hàng tối đa 70 (bậc Bạn), đúng chủ ý U2.11.
