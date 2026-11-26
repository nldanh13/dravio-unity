# Cơ sở sinh cơ học cho chuyển động rồng (U1-CHAR.7b)

Tài liệu này ghi lại các nghiên cứu đã dùng để đặt tham số chuyển động, chỗ áp dụng trong pipeline và runtime, và giới hạn của từng phép áp dụng.

**Nguyên tắc:**
- Rồng là sinh vật giả tưởng, nên không loài thật nào là "đúng" cho chúng.
- Chỉ dùng **quy luật vật lý và tương đồng động lực** (dynamic similarity). Các quy luật này áp dụng được cho bất kỳ cơ thể nào có chân, đuôi hay cánh với kích thước đo được trên chính model.
- **Không suy diễn giải phẫu:** số chân, cánh, vây, hô hấp vẫn lấy từ dữ liệu canonical và `asset-overrides`.
- **Không đổi giá trị gameplay, ID, công thức Domain hay ngưỡng QA.**

## 1. Tóm tắt áp dụng

| # | Quy luật | Nguồn | Áp dụng | Ở đâu |
|---|---|---|---|---|
| 1 | Sải bước tương đối và Froude: λ/h ≈ 2,3·Fr^0,3, Fr = v²/(g·h) | Alexander 1976; Alexander & Jayes 1983 | Chu kỳ Walk/Run tính từ sải bước đã author và chiều cao hông của model (`cadence: froude`) | `rig_archetype.py` ▸ `_froude_cadence`, `rig_geometry.froude_cadence` |
| 2 | Số Strouhal khi bơi/bay hành trình: St = f·A/U ≈ 0,2–0,4 | Taylor, Nudds & Thomas 2003; Triantafyllou et al. 1993 | Tốc độ danh nghĩa Swim/Fly tính từ biên độ đỉnh-đỉnh đo được ở chóp đuôi/chóp cánh, St = 0,3 | `rig_archetype.py` (khối `motion.biomechanics`) |
| 3 | Đổi tốc độ bằng tần số, giữ biên độ | cá dạng anguilliform/carangiform (Sci. Rep. 2023 tổng hợp) | Xác nhận thiết kế sẵn có: `PlaybackRate = speed / groundSpeed` cho Swim/Fly, nên St giữ nguyên khi Sanctuary đổi tốc độ | `DragonAnimationMap.PlaybackRate` (không sửa) |
| 4 | Vòng nghiêng cân bằng: tan φ = V·ω/g; chim bồ câu nghiêng đúng theo lý thuyết khí động | Ros et al. 2011 (PNAS); nghiên cứu GPS/IMU trên bồ câu | Góc nghiêng khi bay phụ thuộc tốc độ và tốc độ quay; giới hạn quay trên không của từng model suy từ góc nghiêng tối đa | `MotionShapeSolver.CoordinatedBankDeg`; `motion.maxTurnRateDegPerSec.air` |
| 5 | Ổn định ánh nhìn: đầu chim gần như bất động trước chuyển động xoay của thân (phản xạ vestibulo-collic) | Theunissen & Troje 2017 (Front. Neurosci.) | Cổ và đầu xoay ngược để bù góc nghiêng thân (`headRollStabilisation` = 1) | `DragonMotionShaper`, `MotionShapeSolver.HeadRollCompensation` |
| 6 | Cất cánh nhờ chân: 80–95 % vận tốc ban đầu đến từ cú đạp chân, nhịp đập cánh đầu tiên bắt đầu lúc rời đất | Earls 2000 (sáo đá, 91 %); Provini et al. 2012 (bồ câu 95 %, sẻ 94 %); Tobalske et al. 2004 (chim ruồi 59 %) | TakeOff phóng lên bằng chân (`launchShare` 0,9); QA đo tỉ lệ vận tốc lúc rời đất so với vận tốc leo cao nhất | `rig_archetype.py` ▸ TakeOff, `launch_share()` |
| 7 | Hạ cánh: hãm chủ yếu bằng cánh trước khi chạm đất, ngóc thân lên (mặt phẳng đập cánh nghiêng lên 17 ± 7°), chân gập khi chạm để hấp thụ | Berg & Biewener 2010 (bồ câu); Provini et al. 2014 (lực cánh → lực chân); Polet et al. 2015 (JFM); nghiên cứu chim két trên cành đung đưa (Biewener lab) | Land có pha ngóc thân `flarePitch` 0,3 rad (~17°), về ngang trước lúc chạm đất; pha gập chân sau khi chạm đã có từ U1-CHAR.7 | `rig_archetype.py` ▸ Land |

## 2. Kiểm chứng tham số có sẵn (không đổi)

| Tham số hiện có | Giá trị | Tài liệu | Kết luận |
|---|---|---|---|
| Duty factor Walk (bốn chân) | 0,65 | Đi bộ có duty > 0,5 (luôn có chân chạm đất); thú "bình thường" thấp hơn bò sát/lưỡng cư (~0,8) | phù hợp |
| Duty factor Run (trot) | 0,42 | Chạy có duty < 0,5 (có pha bay) | phù hợp |
| Duty factor Walk (serpentine) | 0,7 | Bò sát, kỳ giông có duty cao (kỳ giông khổng lồ 0,74) | phù hợp |
| Tỉ lệ downstroke | 55 % chu kỳ | Chim bay hành trình đo được khoảng 50 %, lâu hơn khi cần lực | phù hợp |
| Đào đất | chân trước cào luân phiên, chân sau trụ | Chuột chũi: 65 % cú đào là bất đối xứng (một tay cào, một tay chống); gặm nhấm cào bằng chân trước | phù hợp |
| Bước sóng thân khi bơi | serpentine 0,8 L, aquatic 1,0 L | Lươn 0,4–0,87 L; carangiform gần 1 L | phù hợp |
| Chu kỳ thở Idle / Sleep | 0,5 Hz / 0,25 Hz | Thú: f ≈ 0,58·M^−0,25 Hz lúc trao đổi chất cơ bản; số mũ thay đổi theo môi trường sống (cạn −0,30, nước −0,12) | **chưa áp dụng**: GDD không có khối lượng rồng, không suy đoán |

## 3. Kết quả trên SAMPLE

Bảng trước/sau của từng model: [`scripts/art/character/TEST_REPORT_U7b.md`](../../../scripts/art/character/TEST_REPORT_U7b.md).

## 4. Giới hạn và quyết định chờ art director

- **Froude** được fit trên thú và chim chân thẳng đứng (dữ liệu của Alexander gồm người và thú). Dự đoán kém hơn với chim, và với thú lệch tới 1,5–2,5 lần ở một số loài.
  - Rig quadruped đặt bàn chân ngay dưới hông, nên áp dụng được.
  - **Serpentine giữ chu kỳ cũ**, vì thân uốn lượn góp vào sải bước, ngoài phạm vi của quan hệ này.
- **Strouhal** cho tốc độ *danh nghĩa*. Sanctuary vẫn quyết định tốc độ thật, và playback rate tự giữ St.
  - Biên độ đuôi hoặc cánh rất lớn hay rất nhỏ sẽ cho tốc độ danh nghĩa tương ứng lớn hay nhỏ.
  - Báo cáo có ghi A/L để art director so với dải 0,1–0,3 của cá (Saadat et al. 2017).
- **Góc nghiêng tối đa 30°** là giới hạn hình ảnh của shaper, không phải giới hạn sinh học. Bồ câu nghiêng tới khoảng 45° khi quay gấp.
- **Nhịp thở và nhịp tim theo khối lượng** chưa áp dụng vì không có khối lượng.
- **Đuôi làm đối trọng quán tính:** thằn lằn agama vung đuôi tỉ lệ với nhiễu khi nhảy (Libby et al. 2012, Nature). Chưa có clip nhảy hay rơi, nên chưa áp dụng; ghi lại cho clip sau.

## 5. Nguồn

- Alexander, R. McN. (1976). Estimates of speeds of dinosaurs. *Nature* 261. Công thức như trích trong [Ruiz & Torices 2013](https://www.originalwisdom.com/wp-content/uploads/bsk-pdf-manager/2019/03/Ruiz-and-Torices_2013_Humans-running-at-stadiums-and-beaches-and-the-accuracy-of-speed-estimation-from-fossil-trackways.pdf); [Alexander 1984, American Scientist](https://cs.cmu.edu/%7Ehgeyer/Teaching/R16-899B/Papers/Alexander84AmericanScientist.pdf).
- [Donelan & Kram 1997, tương đồng động lực khi đi (trọng lực giảm)](https://www.sfu.ca/locomotionlab/assets/dynsimwalk1997.pdf); [tổng quan duty factor và dynamic similarity, PMC4933880](https://pmc.ncbi.nlm.nih.gov/articles/PMC4933880); [chuyển dáng đi theo Froude ở robot bốn chân (2023)](https://eprints.whiterose.ac.uk/201823/1/bio_gait.pdf).
- Taylor, Nudds & Thomas (2003), *Nature* 425, và tổng quan về số Strouhal: [Wikipedia: Strouhal number](https://en.wikipedia.org/wiki/Strouhal_number); [Saadat et al. 2017, Phys. Rev. Fluids](https://link.aps.org/doi/10.1103/PhysRevFluids.2.083102); [Floryan, Van Buren & Smits](https://arxiv.org/pdf/1904.05212); [Scaling laws for caudal fin swimmers (2025)](https://arxiv.org/pdf/2507.11665).
- [Bước sóng và sải bơi ở lươn/robot anguilliform, Sci. Rep. 2023](https://www.nature.com/articles/s41598-023-41074-9.pdf); [Lauder & Tytell 2006](https://sites.harvard.edu/glauder/files/2022/03/Lauder.Tytell.2006.pdf); [Di Santo et al. 2021, động học bơi hội tụ ở 44 loài cá](https://pubs.usgs.gov/publication/70226746).
- Tần số đập cánh: [Pennycuick 1990](https://cob.silverchair.com/jeb/article-pdf/150/1/171/2640396/jexbio_150_1_171.pdf); [Pennycuick 1996](https://cob.silverchair.com/jeb/article-pdf/199/7/1613/3391219/jexbio_199_7_1613.pdf); [Universal wing- and fin-beat frequency scaling (2024)](https://www.ncbi.nlm.nih.gov/pmc/articles/PMC11152310/).
- Quay khi bay: [Biewener lab, pigeon turning](https://biewenerlab.oeb.harvard.edu/pigeon-turning.md); [Ros et al. 2011, PMC3162477](https://pmc.ncbi.nlm.nih.gov/articles/PMC3162477); [cơ chế quay chậm ở bồ câu, JEB 201:655](https://cob.silverchair.com/jeb/article/201/5/655/7869/Kinematic-aerodynamic-and-anatomical-mechanisms-in).
- Ổn định đầu: [Theunissen & Troje 2017, Front. Neurosci.](https://pmc.ncbi.nlm.nih.gov/articles/PMC5633612).
- Cất cánh: [Tobalske et al. 2004, chim ruồi](https://cob.silverchair.com/jeb/article/207/8/1345/15063/Take-off-mechanics-in-hummingbirds-Trochilidae); [gà sao (trích Earls 2000)](https://cob.silverchair.com/jeb/article-split/208/17/3293/15733/Performance-of-guinea-fowl-Numida-meleagris-during); [Provini et al., SICB](https://sicb.org/?p=24409).
- Hạ cánh: Berg & Biewener 2010, *Wing and body kinematics of takeoff and landing flight in the pigeon* (JEB 213), số liệu mặt phẳng đập cánh như trích trong tóm tắt; [Provini et al. 2014, chuyển từ lực cánh sang lực chân khi hạ cánh (JEB 217)](https://cob.silverchair.com/jeb/article/217/15/2659/12207/Transition-from-wing-to-leg-forces-during-landing); [Polet et al. 2015, unsteady perching](https://www.cambridge.org/core/journals/journal-of-fluid-mechanics/article/abs/unsteady-dynamics-of-rapid-perching-manoeuvres/B0CD5CFC7735B9BD3732E98FE11ED02A); [chim két đáp xuống cành đung đưa](https://biewenerlab.oeb.harvard.edu/publication/landing-swinging-perch-peach-faced-lovebirds-prefer-extremes).
- Đào đất: [chuột chũi XROMM, PMC6832175](https://pmc.ncbi.nlm.nih.gov/articles/PMC6832175); [cú đào của chuột chũi, SICB](https://sicb.org/?p=17815).
- Duty factor: [RVC repository, thú và bò sát khi đi](https://rvc-repository.worktribe.com/OutputFile/1390551); [Wikipedia: Gait](https://Www.wikipedia.org/wiki/Gait).
- Nhịp thở: [Gillooly, quarter-power scaling](https://people.clas.ufl.edu/gillooly/files/07_quarterpowerscaling.pdf); [Worthington et al. 1991, JEB 161:533](https://cob.silverchair.com/jeb/article/161/1/533/6425/The-relationship-between-body-mass-and-ventilation); [nhịp thở cơ bản ở thú cạn/nước, 2025](https://www.ncbi.nlm.nih.gov/pmc/articles/PMC12400827/).
- Đuôi đối trọng: [Libby et al. 2012, Nature](https://faculty.washington.edu/minster/bio_inspired_robotics_2018/papers/libby_moore_siu_li_cohen_justufi_full_tail_assisted_pitch_lizards_robots_nature2012.pdf).

Một số số liệu chỉ đọc được qua tóm tắt hoặc trích dẫn thứ cấp, ví dụ con số 91 % của Earls và 45° ở bồ câu. Bảng trên ghi đúng mức độ đó.
