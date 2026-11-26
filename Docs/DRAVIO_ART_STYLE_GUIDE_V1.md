# DRAVIO — Art Style Guide v1.0
**Cute Stylized 3D · Bright Fantasy · Living Sanctuary**

Trạng thái: hướng nghệ thuật đã được chủ dự án chọn trong cuộc trao đổi ngày 27/11/2026; các thông số sản xuất dưới đây là đề xuất của AI3, cần nghiệm thu trong Unity. Baseline e075075; không thay đổi gameplay, save, dữ liệu loài hoặc tài nguyên trong PR tài liệu này.

## 01. Tầm nhìn & tiêu chí hình ảnh
Ảnh key art được chủ dự án duyệt trong chat: bốn rồng nhỏ có mắt lớn, da bóng nhẹ, thân tròn, đồng cỏ sáng, cây ăn quả, nhà mái vàng, tháp xa và núi xanh mờ. Đây là chuẩn cảm xúc và cách tạo khối, **không phải** yêu cầu sao chép nhân vật, logo, bản đồ hoặc chính xác bố cục ảnh. Ảnh key art vẫn ở cuộc trao đổi, chưa được đưa vào kho GitHub; không tạo đường dẫn giả. Khi có bản gốc, lưu ở ArtSource/StyleGuide/dravio_key_art_v1.png.
- Đáng yêu nhưng không sơ sài: khối lớn mềm, bề mặt có độ sâu, điểm nhấn nhỏ có chủ đích.
- Sáng, ấm, tươi; tránh màu xám bẩn, vàng kim quá nặng, bóng đen cứng.
- Rồng là chủ thể; môi trường và UI hỗ trợ, không tranh sự chú ý.
- Cùng một ngôn ngữ tạo hình ở cả 3D, sprite, icon, UI và minh họa.
- Art phải đẹp ở camera gameplay và Focus, không chỉ ở ảnh quảng bá.

## 02. Ngôn ngữ hình khối, màu và ánh sáng
Tỷ lệ khuyến nghị khi thiết kế mới: phần đầu rồng non khoảng 35–45% chiều dài thân từ mũi đến gốc đuôi (không tính đuôi/cánh); đây là hướng tạo hình, không được tự động scale rig hiện tại. Mắt to, mõm mềm, chân ngắn chắc; răng/móng/sừng bo đầu. Rồng trưởng thành vẫn dễ nhận biết nhưng trưởng thành hơn qua tỷ lệ và dáng đứng. Công trình có cạnh vát, mái cong, đá tròn, gỗ ấm; tránh hình hộp sắc cạnh và texture nhiễu.
Màu sRGB tham chiếu (không thay palette loài):
| Vai trò | Màu |
|---|---|
| Cỏ non | #A7D65A |
| Lá sâu | #3C9660 |
| Trời | #8CCFF1 |
| Nắng | #FFE4A1 |
| Đất/lối đi | #D7A46B |
| Đá | #B5B9B6 |
| Biển xanh | #45BED2 |
| Mây | #EDF6FF |
| UI kem | #FFF0CA |
| UI gỗ | #865433 |
| UI vàng | #F3BB56 |
| UI xanh | #4CA85B |
| UI chữ sẫm | #49372C |
Chọn 1 màu chủ đạo, 1 phụ, 1 điểm nhấn cho mỗi asset; màu loài luôn theo catalog. Không tăng bão hòa đồng loạt. Bóng có màu lạnh nhẹ, vùng sáng ấm; dùng gradient rộng và ambient occlusion nhẹ ở khe/chân đế. Kim loại và pha lê có highlight rõ; lá, da rồng có highlight mềm; đá và gỗ thiên matte. Tránh trắng cháy, neon toàn màn hình hoặc bloom che silhouette. Dùng main light + ambient hiện có, ưu tiên shader hiện hành; không thêm nhiều đèn động/post-processing mặc định. Hiệu ứng cao cấp chỉ bật sau profiling.

## 03. Rồng — character art
Giữ danh tính, silhouette, palette, locomotion và biology canonical. Bảng dưới trích trực tiếp species-biology.json tại baseline, thứ tự màu primary / secondary / belly / accent.
| ID loài | Silhouette | Palette | Locomotion |
|---|---|---|---|
| emberling | Bốn chân, đuôi lửa tròn, không cánh. | `#D86B47` / `#A83D36` / `#F9D59A` / `#FFD66C` | walk |
| droplet | Thân tròn, vây tai, đuôi dẹt, màng chân. | `#61BBD0` / `#337FA4` / `#C7F2EE` / `#86E5DE` | walk, swim |
| sprout | Mầm lá, bốn chân, đuôi lá. | `#75B57D` / `#397C58` / `#E3EAC0` / `#E7B864` | walk, burrow |
| zephyr | Cánh lông rộng, đuôi mây, chân đậu. | `#A6C8E6` / `#6388BA` / `#EDF4FC` / `#F5D999` | walk, fly |
| steamdrake | Thân dài, vây sống lưng, chi ngắn. | `#92BFC0` / `#527E83` / `#E5E5CB` / `#ECAD78` | walk, swim |
| magma | Giáp đá, sừng ngắn, chân chắc. | `#9F5544` / `#523B48` / `#F1A55C` / `#FFD261` | walk, burrow |
| sparkwing | Cánh góc cạnh, đuôi tia điện. | `#DBB65A` / `#96703B` / `#F8EAC6` / `#F7F08A` | walk, fly |
| frostfin | Vây ngực lớn như cánh; không chân đi, không bay. | `#8ABADD` / `#4B75AB` / `#E0F5FF` / `#BDEDF4` | swim |
| mossback | Mai rêu, chi bè, vây đuôi. | `#829D66` / `#506C4C` / `#D6D7A2` / `#A7CD84` | walk, swim |
| blossom | Cánh cánh hoa, đuôi dài, chân đậu. | `#DC9FB8` / `#A86B94` / `#F9E1D5` / `#B7D99A` | walk, fly |
| solaris | Mào lửa, cánh lông lớn, đuôi quạt. | `#ED874B` / `#AC4B3D` / `#FBE1A0` / `#FFE47F` | walk, fly |
| leviathan | Thân biển dài, vây san hô, không chi đất. | `#427C9A` / `#27526E` / `#C7E4D7` / `#E38F86` | swim |
| amberhorn | Sừng hổ phách, giáp lưng, bốn chân. | `#C58C45` / `#855B39` / `#F3DCA4` / `#FFE08A` | walk, burrow |
| aurorawing | Cánh dải sáng, lông cổ, đuôi cực quang. | `#879BD5` / `#5F5B9E` / `#DDE6FA` / `#8DE6CF` | walk, fly |
| celestia | Cánh sao, sừng tinh thể, đuôi sao. | `#7776B5` / `#45446F` / `#D4D2EC` / `#E9D48C` | walk, fly |
| gaia | Sừng cây, giáp đá-rêu, chân màng và vây. | `#629B7D` / `#385D57` / `#DDE5B6` / `#E8C97F` | walk, swim |

- Mắt: iris lớn, một highlight chính + highlight nhỏ, hướng nhìn rõ; tránh mắt đen vô hồn. Mí mắt và miệng biểu cảm qua rig/animation phù hợp.
- Bụng sáng hơn lưng; vảy lớn/chấm điểm nhấn vừa đủ, không dùng noise li ti. Cánh/vây/mào phải đọc được từ camera thật.
- Rồng không có cánh theo biology không tự ý thêm cánh. Không thêm chân/vây làm sai loài. Giữ scale canonical, pivot, bone, animation, collider và prefab contract khi thay model. Không thay rig của Emberling chỉ để đổi tỷ lệ.
- Concept mỗi loài: ảnh 3/4 trung tính, ảnh front/side/back nhất quán, toàn thân không cắt đuôi/cánh; thêm sheet biểu cảm và màu. Không dùng key art nhiều rồng làm input image-to-3D.
- Biến thể Normal/Crystal/Golden/Prismatic: giữ silhouette, dùng palette/material accents khác biệt, không thay luật mutation. Chất liệu Crystal có facet lớn; Golden ấm, Prismatic chuyển màu tiết chế. Không mặc định phủ cả thân bằng metallic.

## 04. Thế giới
| Zone / habitat hiện có | Hướng art | Landmark / điểm nhấn |
|---|---|---|
| Origin / origin_meadow | đồng cỏ vàng xanh, cây tán tròn, hoa nhỏ, đường đất ấm | cây cổ thụ, cổng cổ phủ hoa |
| Origin / origin_warm_rocks | đá tròn màu hổ phách, cỏ thấp, nắng ấm | đá tắm nắng, tượng rồng |
| Origin / origin_pondbank | xanh mát, nước xanh ngọc, ven bờ mềm | hồ, sen, nấm sáng |
| Ocean / ocean_estuary | xanh ngọc sáng, cát kem, rong mềm | cổng chìm |
| Ocean / ocean_reef | san hô hồng/cam, xanh lam, cá/vật thể nền chỉ khi art contract cho phép | rạn san hô, cột chìm |
| Ocean / ocean_cool_pool | lam lạnh, pha lê dịu, ánh sáng xuyên nước | đá nghỉ và tinh thể |
| Sky / sky_low_island | cỏ bạc hà, mây kem, tím pastel, ánh sáng ấm | đảo nổi, cổng, đền/cột cổ |

Bố cục: tiền cảnh viền mềm, trung cảnh rồng + vùng tương tác rõ, hậu cảnh giảm tương phản/bão hòa; dùng cụm cây, dốc nhẹ, mảng cỏ lớn, núi xa và sương để tạo chiều sâu. Không đặt vật cản trang trí vào spawn, walk area, perch, rest, swim/fly volumes. Không tạo habitat mới, gate mới, route mới từ concept. Nhà/tháp trong key art là định hướng hình ảnh, không mặc định trở thành công trình gameplay. Công trình Sanctuary chỉ hiển thị khi hệ thống xây dựng cho phép; không đưa prefab thưởng vào scene tĩnh.

Ngày: trời sáng, bóng mềm; hoàng hôn: vàng đào + tím nhẹ; đêm: xanh tím, rune/đom đóm làm điểm nhấn; vẫn đọc rồng và đường đi. Ocean caustics mờ, Sky biển mây phân tầng; giữ các giới hạn #81. VIS.1 là nền kỹ thuật để tiếp tục, không hạ cấp hoặc ghi đè hàng loạt.

## 05. Tài nguyên môi trường — 3D / 2.5D / texture
| Loại | Phương pháp | Ví dụ |
|---|---|---|
| Hero mesh | 3D đầy đủ, silhouette rõ ở Focus | rồng, cây lớn, tượng, cổng, đá lớn, đảo nổi, cầu, đền |
| Props gameplay | 3D, pivot và collider đúng hợp đồng | tổ, công trình Sanctuary, vật thể tương tác |
| Foliage phụ | alpha card giao nhau hoặc mesh ít tam giác | cỏ, dương xỉ, hoa nhỏ, rong, nấm, san hô quạt |
| Bề mặt | tile/atlas, decal có kiểm soát | rêu, đường mòn, đất, vỏ cây, đá |
| FX | sprite, particle hoặc shader | mây, sương, tia nước, bụi sao, đom đóm |

Concept 3D: một vật độc lập, nền sạch trung tính, góc 3/4, toàn hình, ánh sáng studio mềm, không chữ/đồ thừa, không bóng đổ dài; cần thêm góc nếu vật có nhiều mặt khác nhau. Foliage sprite: RGBA alpha thật, không checkerboard giả, tránh viền màu nền, tách từng cây; không dùng nguyên ảnh phong cảnh làm card. Dùng billboard cố định/chéo để không xoay bất ngờ theo camera Focus. Texture seamless phải kiểm tra khi lặp, màu không bake bóng hướng mạnh.

## 06. UI — polished casual fantasy
Chuyển từ Ancient Fantasy tối/nặng và vector phẳng sang casual fantasy sáng, bo tròn, nền kem/gỗ ấm, viền vàng mật ong, mặt nút xanh lá hoặc xanh nước. Tạo chiều sâu bằng bevel mềm, ánh sáng trên, bóng dưới, gradient rộng, một điểm trang trí nhỏ. Không khung kim loại quá dày, chi tiết góc che chữ, texture giả cũ nặng. Giữ nội dung và callback hiện có.
| Thành phần | Hướng hình | Nguồn đề xuất |
|---|---|---|
| Primary | nút xanh lá bo tròn, mặt hơi lồi | 1024×384 |
| Secondary | kem/gỗ sáng hoặc lam dịu | 1024×384 |
| Reward | vàng mật ong, highlight mềm | 1024×384 |
| Danger | san hô đỏ, vẫn thân thiện | 1024×384 |
| Close/back | tròn, ký hiệu riêng | 512×512 |
| Tab | pill/thẻ bo tròn, active sáng hơn | 512×256 |
| Panel | nền kem ấm hoặc xanh sâu, viền mỏng | góc/cạnh/nền tách hoặc 1024² |
| Card/slot | vuông bo góc, frame theo rarity | 512² |
| Progress | frame riêng + fill riêng + highlight tùy chọn | 1024×128 |
| Divider | lá nhỏ/đường vàng mềm, không chữ | 512×128 |
| Portrait | vòng tròn/bát giác mềm, lõi alpha thật | 512² |

9-slice: tạo vùng giữa có texture nhẹ và không chứa họa tiết độc nhất; giữ góc/cạnh theo border đo trên sprite thật, không dùng một border pixel cho mọi ảnh. Các góc trang trí lớn tách sprite overlay không raycast. Dùng Image Filled/mask cho fill; không kéo cả thanh theo giá trị. Button normal/highlight/pressed/disabled: cùng hình dạng, nhấn tối nhẹ + scale visual con khoảng 0,97, disabled giảm saturation nhưng chữ vẫn đọc được. Không thay vùng hit khi animate. Tab active phải có màu + hình/indicator, không chỉ màu.
Bố cục landscape hiện có: giữ Safe Area, camera free viewport và callback; ưu tiên rồng, menu nhóm chức năng, card ngắn dễ đọc. Không tự ý xóa chức năng. Thiết kế reference 1280×720; giữ cơ chế compact 960×540 hiện hành cho mobile đến khi QA quyết định. CanvasScaler, anchor, layout groups; thử 16:9, 20:9, 4:3; không tuyên bố hỗ trợ portrait khi chưa làm. Nút chạm mục tiêu 44–48 logical units tối thiểu và kiểm tra kích thước vật lý thực tế. Chữ không nằm trong ảnh. TMP/SDF là bước nâng cấp riêng với font đủ dấu tiếng Việt và fallback, không đổi serialized Text hàng loạt khi chưa kiểm thử.
Raster cao cấp + 9-slice cho họa tiết, vector cho hình học và glyph đơn giản, không dùng vector phẳng cho icon vật phẩm chính. Source 512–1024 không đồng nghĩa import tất cả ở kích thước đó. Chọn import theo kích thước hiển thị, bảo toàn alpha, mipmap tắt cho screen-space UI, clamp, SpriteAtlas padding 4–8 px tùy mip/compression, không nén mạnh gây viền bẩn. Artwork UI không cần GLB. Chỉ dùng shader/animation nhẹ cho sheen, reward, progress; có giảm chuyển động khi cần.

## 07. Icon & item
Hướng: rendered 3D cartoon, silhouette lớn, góc 3/4, ánh sáng trên trái thống nhất, bóng tiếp xúc trong hình, background alpha, không chữ/khung kèm theo. Source mỗi icon 512², chiếm ~75–85% canvas; kiểm tra ở 24, 32, 48, 64 px. Không chỉ dùng màu để phân biệt rarity hoặc trạng thái.
Bộ chức năng: care, biology, dragons, eggs, world, quests, expedition, inventory, feed, pet, rest, inspect, build, research, health, settings, day, night, lock, done, warning, upgrade, reward, hunger, water, energy, temperature, bond, leaf, close, back, next.
Bộ vật phẩm: gold (xu khắc rồng), wood (khúc gỗ tròn), seed (hạt và mầm), stone (đá bo), crystal (pha lê lam), coral (nhánh san hô), stardust (lọ bụi sao), fruit (quả mọng đỏ), leaf (lá xanh), aquatic (cây nước), aether (quả/tinh thể năng lượng), starter/elemental/mystic/astral (hộp khác silhouette/họa tiết), blueprint (giấy cuộn), egg (trứng). Art không kích hoạt vật liệu chưa có nguồn hoặc thay đổi pack odds. Trứng theo loài cần ảnh riêng khi có thiết kế canonical, không suy diễn từ màu pack.
Rarity visual: Common gỗ/kem, Rare lam, Epic tím, Legendary vàng; biểu tượng đi kèm nhãn hoặc chỉ dấu riêng. Không đổi rarity mapping trong Domain.

## 08. Kỹ thuật và ngân sách
- Unity 6000.6.10f1 + URP; ưu tiên shader đang dùng, SRP Batcher và atlas theo zone. Không thêm dependency bắt buộc chỉ để đổi style.
- Ngưỡng hiện hành ENV: 60.000 triangles, 10 materials, 6 MiB RGBA32+mip theo validator mỗi zone Ocean/Sky; giữ nguyên. VIS.1 báo Origin 33.352/60.000 triangles. Đây là số từ tài liệu baseline, không phải phép đo mới. Đo UI/character riêng, không cộng nhầm phạm vi.
- Đề xuất mục tiêu asset: hero prop 2–6k triangles LOD0; prop nhỏ 200–1.500; card 2–12 triangles tùy số plane; LOD chỉ khi camera/độ lớn cần. Theo VIS.1, GLB image-to-3D cũ có 40k triangles/mesh và 2 texture 2048²; **không giả định 12 GLB mới có cùng thông số**. Kiểm tra thật trước khi decimate; giữ UV, normals, silhouette và anchor. Character budget theo rig/character team, không áp mức prop lên rồng.
- Texture mobile: giữ ngân sách atlas #81 (props 512, emission 128, foliage 1024×512, ground 512 cho Ocean/Sky); chỉ thay nếu có budget review. ASTC phù hợp thiết bị và có fallback, giữ alpha foliage. Không nhân bản cả source PNG vào runtime. Kiểm tra mip bleed, color space, import và memory thực tế.
- Mục tiêu hiệu năng đề xuất: 30 FPS ổn định trên máy thấp và 60 FPS ở tier phù hợp nếu thiết bị đạt; kiểm chứng frame time, memory, draw calls, overdraw và thermal. Không tuyên bố đạt nếu chưa đo.
- Quy ước: nguồn ArtSource; runtime Assets/Dravio; giữ GUID khi thay asset, không sửa scene generated bằng tay nếu builder là nguồn sự thật. Ghi source id, author/provenance, SHA, license/trạng thái, loại 3D/2.5D/UI, import, target và screenshot QA trong manifest.

## 09. Lộ trình triển khai
P0 — Reference board: lưu key art đã duyệt, làm 1 mockup gameplay Origin + HUD không chữ bake; 1 sheet nút/khung và 1 sheet icon vật phẩm mẫu. Nghiệm thu art trước khi sản xuất hàng loạt.
P1 — UI: primary/secondary/reward, panel/tab/slot, 8 icon chức năng và 6 item ưu tiên; thử sliced import, responsive, tiếng Việt/Anh, trạng thái nút. PR #84 là prototype kỹ thuật; không mặc định merge vector-only vì chủ dự án không thích thẩm mỹ đó. Giữ phần tương thích input/layout; thay art có chọn lọc.
P2 — Origin + Emberling: một vertical slice gameplay thật với grass/canopy/pond, model và ánh sáng đồng nhất; giữ VIS.1 contract và rig. Chụp overview + Focus.
P3 — Ocean, Sky: nhân rộng phong cách, 2.5D foliage trước; 3D hero props và công trình Sanctuary sau khi xác minh GLB, vị trí và điều kiện xây dựng. PR #83 shader là đề xuất riêng, review trên baseline mới.
P4 — Toàn bộ 16 loài, UI/vật phẩm và các công trình còn lại. Không batch chuyển tất cả concept thành GLB.

## 10. Cổng nghiệm thu (definition of done)
1. Concept: đúng silhouette/màu, khối tròn, chất liệu mềm, cùng style key art; không vật thể thừa, không chữ bake.
2. Asset: alpha thật, không viền nền/UV lỗi, mesh không mặt lộn, scale/pivot và GUID đúng, source ghi rõ.
3. Gameplay: không đổi behavior, collider/interaction, save, world IDs; chạy validator Origin/Ocean/Sky và kiểm tra spawn, rest, perch, framing.
4. UI: đọc được trên 1280×720, 1920×1080, 2400×1080, 2560×1440, 1024×768; notch left/right và Focus; cả hai ngôn ngữ, button state và scroll. Không vỡ ảnh/méo góc hoặc che chủ thể.
5. Visual: chụp Unity Game View ngày/đêm, overview/Focus, so sánh cùng camera; Blender/key art chỉ là reference.
6. Mobile: đo FPS/frame time, memory và rendering trên thiết bị; báo số đo, không chỉ ước tính.
7. PR: ảnh trước/sau, thay đổi, scope, test nào thực chạy, rủi ro còn lại; merge sau QA.
## Tài liệu nền
- Docs/vis1/VIS1_ORIGIN_EMBERLING.md
- Docs/ENV_ALPHA_OCEAN_SKY_POLISH.md
- Assets/Dravio/Data/species-biology.json
- ArtSource/manifest.json
- PR #83 và #84 (draft prototype, không phải chuẩn art cuối).
