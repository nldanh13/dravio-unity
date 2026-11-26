# U2.10 — Social Ecology Design

> Quyết định và phần chờ duyệt: [ADR 0204](../adr/0204-u2-10-research-social-progression.md) mục B (S1–S9).
> **Trạng thái:** prototype chỉ đọc (`Assets/Dravio/Domain/Behavior/SocialEcology.cs`). Mọi tương tác là **trình diễn**; tác động gameplay (S8) TẮT. Không đổi Save Schema.
> **Nguồn:** GDD 3.1 không định lượng tập tính xã hội (theo tài liệu có trong repo). Bảng dưới là đề xuất dựa trên catalog G0.

## 1. Nguyên tắc

1. **Không mặc định mọi loài sống theo nhóm.** Mỗi loài có một tập tính xã hội riêng.
2. **Không đánh nhau, tranh giành, tổn thương hay tử vong.** Không có tương tác nào khi offline (hàm chỉ chạy khi Sanctuary đang hiển thị).
3. **Tách trình diễn khỏi gameplay.** Ứng viên tương tác luôn có `Role = Presentation`, `AffectsGameplay = false`. Quyết định gameplay của Behavioral Engine (tìm ăn, ăn, chăm sóc, trú, vitals thấp, cảnh báo) luôn thắng và không bị ghép cặp.
4. **Một lớp trên Behavioral Engine**, không phải engine thứ hai: chỉ ghép các quyết định trình diễn đã có của U2.9.

## 2. Tập tính xã hội theo loài (S1, S2)

| Loài | Tập tính | Khoảng cách cá nhân Adult | Căn cứ |
|---|---|---|---|
| emberling | Tolerant | 1,5 m | loài nhỏ, phổ biến, trên cạn |
| droplet | Gregarious | 1,0 m | sống thành nhóm ở bờ ao |
| sprout | Gregarious | 1,0 m | ăn cỏ ở đồng cỏ |
| zephyr | Gregarious | 1,5 m | bay theo đàn |
| steamdrake | Tolerant | 2,0 m | rắn lưỡng cư |
| magma | Solitary | 3,0 m | phơi nắng, giữ lãnh thổ trên đá ấm |
| sparkwing | Tolerant | 2,0 m | bay theo đàn lỏng |
| frostfin | Gregarious | 1,5 m | bơi thành đàn |
| mossback | Tolerant | 1,5 m | ăn cỏ chậm chạp |
| blossom | Gregarious | 1,5 m | đàn thụ phấn |
| solaris | Solitary | 3,5 m | săn mồi cỡ lớn |
| leviathan | Solitary | 5,0 m | khổng lồ biển sâu |
| amberhorn | Solitary | 3,0 m | phơi nắng, giữ lãnh thổ |
| aurorawing | Tolerant | 2,5 m | bay đêm |
| celestia | Solitary | 4,0 m | huyền thoại |
| gaia | Tolerant | 3,0 m | huyền thoại hiền lành |

Kết quả: 5 Gregarious, 6 Tolerant, 5 Solitary (test kiểm mỗi nhóm có ít nhất 3 loài). Rồng con: khoảng cách ×0,6; thiếu niên ×0,8; cá thể nhút nhát (dạn dĩ < 40) ×1,3.

## 3. Hồ sơ cá thể (S3)

`Sociability = gốc loài (70 / 50 / 25) + (dạn dĩ − 50) / 4 + (ham chơi − 50) / 5 + tuổi (Baby +15, Juvenile +5)`, giới hạn 0–100.

- ≥ 55: **thích có bạn** (chủ động chơi, nghỉ gần nhau).
- < 35: **thích không gian riêng** (không chơi cùng, giữ khoảng cách khi đông).

Ví dụ (trước độ lệch ±10 theo UID): Leviathan Adult tính Calm ≈ 23 (thích không gian riêng), Leviathan Baby ≈ 38. Sprout Adult tính Playful ≈ 79 (thích có bạn).

## 4. Độ quen và độ hợp (S4)

Độ quen 0–100 chỉ dùng dữ liệu đã có trong save v8:

| Nguồn | Điểm |
|---|---|
| Cha mẹ – con (`parentIds`) | 60 |
| Anh chị em ruột (chung 2 bố mẹ) | 50 |
| Cùng cha hoặc mẹ | 30 |
| Ông bà (`grandparentIds`) | 30 |
| Đang cùng microhabitat | +20 |
| Cùng loài | +10 |

Độ hợp (affinity) 0–100 = trung bình độ hòa đồng + độ quen / 4, cùng loài +10, cùng nhóm sinh sản +5, lệch chu kỳ ngày/đêm −15, có bên Solitary −20. Quan hệ đối xứng.

**Chưa làm (S7):** độ quen tăng theo thời gian sống chung cần lưu theo cặp trong Schema v9. Đề xuất: +1 mỗi giờ online cùng microhabitat, trần 40, không giảm khi offline.

## 5. Tương tác trình diễn (S5, S6)

| Tương tác | Điều kiện | Gợi ý cho Sanctuary |
|---|---|---|
| **Chơi cùng** | Một bên đang Play; bên kia Play, hoặc Idle/Explore với độ hòa đồng ≥ 45; độ hợp ≥ 50; không ai thích không gian riêng | Hai rồng chạy vòng quanh nhau, giữ `MinDistanceDm` |
| **Nghỉ theo nhóm** | Cả hai đang Rest/Sleep/Idle (ít nhất một bên Rest hoặc Sleep); không loài Solitary; độ hợp ≥ 45 | Nằm gần nhau, không gần hơn `MinDistanceDm` |
| **Tò mò quan sát** | Một bên đang Explore, tò mò ≥ 60, độ quen < 40; bên kia không ngủ | Đứng nhìn từ xa, quay đầu (head look) về phía kia |
| **Giữ khoảng cách** | Cá thể thích không gian riêng ở microhabitat có ≥ 2 rồng khác | Chọn điểm đứng xa đám đông |

Ghép cặp tất định:

- Chỉ giữa rồng cùng zone và cùng microhabitat, đang hiện, có quyết định trình diễn.
- Mỗi cá thể tối đa một tương tác mỗi nhịp 60 s (nhịp chung cho cả cặp, để hai bên cùng thấy).
- Chọn tham lam theo điểm; hòa điểm theo `hashSeed("social", cặp, nhịp)`.
- `UntilMs` = sớm hơn trong hai `UntilMs` của quyết định, nên tương tác kết thúc khi một bên đổi hành vi.

Sanctuary có thể bỏ qua bất kỳ ứng viên nào (đường bị chặn, thiếu clip, không đủ chỗ) mà không sai luật.

## 6. Quan hệ giữa cá tính, tuổi và sinh học

| Yếu tố | Ảnh hưởng |
|---|---|
| Sinh học (loài) | Tập tính xã hội, khoảng cách cá nhân, nhóm sinh sản, chu kỳ ngày đêm (loài ngày và đêm ít hợp nhau) |
| Tuổi | Rồng con hòa đồng hơn và đứng gần hơn |
| Cá tính | Dạn dĩ và ham chơi tăng độ hòa đồng; tò mò quyết định "tò mò quan sát"; nhút nhát tăng khoảng cách cá nhân |
| Phả hệ | Độ quen (gia đình nghỉ cùng nhau, ít tò mò về nhau) |

## 7. Trình diễn và gameplay

| | Trình diễn (đã làm, bật) | Gameplay (đề xuất S8, TẮT) |
|---|---|---|
| Ví dụ | chơi cùng, nghỉ nhóm, tò mò, giữ khoảng cách | +5 mục tiêu comfort khi hòa đồng có bạn quen; −5 khi thích không gian riêng mà nơi ở đông |
| Dẫn tới lệnh? | Không | Không (chỉ đổi mục tiêu của C1) |
| Đổi save? | Không | Không (comfort đã có trong v8) |
| Phụ thuộc | Behavioral Engine | ADR 0203 C1 (Comfort drift) + ADR 0202 P7 (môi trường suy giảm) |
| API | `SocialEcology.Candidates` | `SocialEcology.ComfortTargetModifier(s, uid, rules)` = 0 ở Production |

## 8. API prototype cho Sanctuary

```csharp
DayPhase phase = (DayPhase)(int)timeOfDay.Phase;
foreach (SocialInteractionCandidate c in ProgressionQueries.SocialInteractionCandidates(session.State, clock.NowMs(), phase))
{
    float minDist = c.MinDistanceDm / 10f;
    switch (c.Kind)
    {
        case SocialInteractionKind.PlayTogether:    sanctuary.PlayPair(c.InitiatorUid, c.PartnerUid, minDist, c.UntilMs); break;
        case SocialInteractionKind.RestTogether:    sanctuary.RestNear(c.InitiatorUid, c.PartnerUid, minDist, c.UntilMs); break;
        case SocialInteractionKind.CuriousApproach: sanctuary.WatchFrom(c.InitiatorUid, c.PartnerUid, minDist, c.UntilMs); break;
        case SocialInteractionKind.KeepDistance:    sanctuary.KeepAway(c.InitiatorUid, minDist, c.UntilMs); break;
    }
}
```

Chi tiết DTO: API_INTEGRATION_GUIDE.md.

## 9. Kiểm thử

- `Social_catalog_covers_every_species_and_not_every_species_is_gregarious`
- `Social_profile_combines_species_personality_and_age`
- `Kinship_is_derived_from_saved_pedigree` (đối xứng)
- `Social_candidates_are_presentation_only_deterministic_and_exclusive`: quét 24 h, mỗi cá thể một tương tác, cùng microhabitat, khoảng cách ≥ khoảng cách cá nhân, Solitary không nghỉ nhóm, không đổi state
- `Gameplay_decisions_win_over_social_pairing`
- `Solitary_dragons_keep_their_distance_in_crowds`
- `Social_gameplay_effects_are_off_in_production`
- `U210_read_only_queries_do_not_change_the_economy` (mô phỏng kinh tế có và không có truy vấn xã hội cho state cuối giống hệt)
