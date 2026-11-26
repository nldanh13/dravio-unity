# U2.10 — Research & Discovery Design

> Quyết định và phần chờ duyệt: [ADR 0204](../adr/0204-u2-10-research-social-progression.md) mục A (R1–R10).
> **Trạng thái:** prototype chỉ đọc (`Assets/Dravio/Domain/Behavior/ResearchProgress.cs`). Production TẮT (`ResearchPacing.Production.Enabled = false`): Codex và hồ sơ cá thể hiển thị đầy đủ như hiện tại. Không đổi Save Schema (v8).
> **Nguồn:** GDD 3.1 không có trong repo Unity, nên mọi con số ở đây là đề xuất.

## 1. Mục tiêu

- Nghiên cứu là hướng chơi dài hạn riêng, song song với sưu tầm và lai tạo, đúng chất cozy và mô phỏng sinh học.
- Tiến trình đến từ **thấy được điều mới** (tập tính, ngày đêm, sinh cảnh, cá tính), không từ việc bấm nhiều.
- Không tạo vàng, XP, bond hay tiền tệ mới, nên không gây lạm phát.
- Không lập hệ thống thứ hai: dùng lại `ResearchTier`, `CodexSection`, `Research.ObservableBehaviors` và Behavioral Engine của U2.9.

## 2. Cấp hiểu biết theo loài (R1, R4)

| Cấp | Điều kiện | Dữ liệu | Mở phần Codex |
|---|---|---|---|
| Sighted | Sở hữu loài (ô Codex đầu tiên) | save v8 | Identity |
| Profiled | Đã xem hồ sơ sinh học (`viewBiologyProfile`) | save v8 | Habitat |
| LifeCycle | Đã thấy đủ Baby, Juvenile, Adult | save v8 (`codex.observedStages`) | Physiology |
| Ethology | Đã ghi mọi tập tính loài làm được + đủ chu kỳ ngày đêm + ghi chép ≥ 2 ngày UTC | sổ nghiên cứu (v9) | Behavior |

"Tập tính loài làm được" là `Research.ObservableBehaviors`: Idle, Rest, Sleep, Explore, SeekFood, Eat, Play cộng Bask / Swim / Fly / Burrow nếu sinh học và ít nhất một microhabitat bản địa cho phép (BEHAVIOR_MATRIX.md). Không loài nào bị yêu cầu tập tính nó không thể làm.

## 3. Một lần quan sát ghi gì (R2)

Người chơi chạm "quan sát" một rồng đang hiện ở sinh cảnh. Engine đọc quyết định hành vi hiện tại (chỉ đọc) và ghi:

| Loại phát hiện | Cấp | Ý nghĩa |
|---|---|---|
| `behavior` | loài | Nhật ký quan sát tập tính |
| `phase` | loài | Ghi nhận chu kỳ ngày đêm (bình minh, ngày, chạng vạng, đêm) |
| `habitat` | loài | Ghi nhận sinh cảnh phù hợp (chỉ microhabitat trong `habitatRules`) |
| `individual` | cá thể | Khám phá cá tính và sở thích (mục 5) |

Rồng ở kho, đang lai, đang thám hiểm không quan sát được. Rồng ở trạm chăm sóc hay nơi trú (Rest gameplay) vẫn quan sát được nhưng không có microhabitat nên không cho phát hiện sinh cảnh.

## 4. Nhịp tự nhiên, chống spam (R3, R6)

| Cơ chế | Giá trị đề xuất | Tác dụng |
|---|---|---|
| Hồi mỗi rồng | 30 s (giữ ADR 0203 D3) | Bấm liên tục không ghi gì |
| Giới hạn phát hiện cấp loài | 2 mỗi loài mỗi ngày UTC | Mỗi loài thành "dự án một tuần" thay vì 15–50 phút |
| Phát hiện vượt giới hạn | Hoãn, không mất | Không phạt người chơi, mai quan sát lại vẫn thấy |
| Ethology | ≥ 2 ngày khác nhau | Không thể dồn vào một phiên dài |
| Cơ hội quan sát | `Worthwhile` chỉ khi có phát hiện | UI chỉ nhấn mạnh rồng đáng quan sát, không cần nút spam |

Thứ tự ghi trong một lần quan sát: tập tính → pha → sinh cảnh, nên tập tính hiếm (bay, đào) không bị pha "chiếm chỗ".

### Số ngày tối thiểu tới Ethology (người chơi chăm chỉ, 2 phát hiện/ngày)

Tính theo ma trận hành vi U2.9. "Tối thiểu" = tập tính + 2 pha + 1 sinh cảnh. "Ghi hết" = thêm đủ 4 pha và mọi microhabitat bản địa.

| Loài | Chu kỳ | Tập tính cần ghi | Microhabitat bản địa | Phát hiện tối thiểu | Ngày tối thiểu | Ngày nếu ghi hết |
|---|---|---|---|---|---|---|
| emberling | day | 8 | 2 | 11 | 6 | 7 |
| droplet | both | 9 | 2 | 12 | 6 | 8 |
| sprout | day | 9 | 2 | 12 | 6 | 8 |
| zephyr | day | 9 | 2 | 12 | 6 | 8 |
| steamdrake | both | 9 | 2 | 12 | 6 | 8 |
| magma | both | 9 | 1 | 12 | 6 | 7 |
| sparkwing | day | 9 | 2 | 12 | 6 | 8 |
| frostfin | both | 8 | 1 | 11 | 6 | 7 |
| mossback | day | 9 | 2 | 12 | 6 | 8 |
| blossom | day | 9 | 2 | 12 | 6 | 8 |
| solaris | day | 9 | 3 | 12 | 6 | 8 |
| leviathan | both | 8 | 1 | 11 | 6 | 7 |
| amberhorn | day | 9 | 2 | 12 | 6 | 8 |
| aurorawing | night | 8 | 2 | 11 | 6 | 7 |
| celestia | night | 9 | 2 | 12 | 6 | 8 |
| gaia | both | 9 | 2 | 12 | 6 | 8 |

So sánh: U2.9 không giới hạn đạt Ethology sau 14–51 lần quan sát (15–50 phút). Test `Ethology_needs_several_days_under_U210_pacing_but_one_day_without_it` kiểm cả hai chế độ.

**Rủi ro lịch chơi (quyết định cần duyệt số 3).** Mô phỏng Researcher "5 phút/ngày lúc 09:00" không bao giờ đạt Ethology cho loài ban ngày, vì không bao giờ thấy chúng ngủ. Loài `both` vẫn đạt vì có ngủ ngắn ban ngày. Ba hướng: quan sát theo `settings.timeMode` của Sanctuary; "nhật ký đêm" tự ghi một phát hiện pha nghỉ sau một đêm offline; hoặc giữ nguyên.

## 5. Nghiên cứu cá thể (R5)

| Nét | Điều kiện hé lộ | Nguồn giá trị |
|---|---|---|
| Cá tính (Playful / Calm / Curious / Shy) | Lần quan sát đầu tiên | `DragonState.Personality` (đã lưu) |
| Nhịp sinh học | Đã thấy cá thể cả lúc hoạt động lẫn lúc nghỉ/ngủ | Behavioral Engine |
| Nơi ưa thích | ≥ 3 lần quan sát và đã thấy nó ở nơi đó | `PersonalityProfile.FavoriteHabitatId` |
| Món ưa thích | ≥ 3 lần quan sát và đã thấy nó tìm ăn / ăn | `PersonalityProfile.FavoriteDiet` |

Bán hay thả rồng thì ghi chép cá thể bị cắt (`PruneIndividuals`), còn ghi chép loài thì giữ, giống Codex.

## 6. Phần thưởng không lạm phát (R7)

- Mở phần Codex theo cấp (Identity → Habitat → Physiology → Behavior).
- Ghi chú Codex, danh hiệu nhà nghiên cứu (hiển thị).
- Tiến độ nhiệm vụ cổng mở Thủy Vực / Thiên Giới (QUEST_PROGRESSION_MATRIX.md).
- **Không** vàng, XP, bond, giảm giá, vật phẩm tiêu hao hay tiền tệ mới.

## 7. Codex mở rộng (R10)

| Trang | Khối mới | DTO |
|---|---|---|
| Loài | Thanh tiến độ Ethology (`EthologyProgressPct`), Nhật ký tập tính (`ObservedBehaviors` / `ObservableBehaviors`), Chu kỳ ngày đêm (`ObservedPhases`, `CircadianCovered`), Sinh cảnh đã ghi (`ObservedHabitats` / `NativeHabitats`), bước tiếp theo (`NextStepKey`) | `SpeciesResearchProfile` |
| Cá thể | "Hiểu về bé": cá tính, nhịp sinh học, nơi và món ưa thích khi đã hé lộ; bước tiếp (`NextStepKey`) | `IndividualResearchProfile` |
| Màn Sanctuary | Dấu "đáng quan sát" trên rồng, gợi ý (`HintKey`), hồi còn lại | `ObservationOpportunity` |

## 8. Schema v9 (R8, chỉ bản nháp)

Khối `research` theo `ResearchLogSnapshot`:

```json
"research": {
  "species": [{ "speciesId": "droplet", "behaviors": ["eat","idle","swim"], "phases": ["day","night"],
                "habitats": ["origin_pondbank"], "days": ["2026-12-01","2026-12-02"],
                "findingsByDay": [["2026-12-02", 2]] }],
  "individuals": [{ "uid": "…", "speciesId": "droplet", "observations": 4, "seenInFavoriteHabitat": true,
                    "seenFeeding": false, "lastObservedAtMs": 0, "activePhases": ["day"], "restPhases": ["night"],
                    "revealed": ["archetype","chronotype"] }]
}
```

- Migration v8 → v9: khối rỗng. Không suy diễn từ Codex vì người chơi chưa thực sự quan sát.
- Validator (`ResearchSchemaDraft.Validate`): loài có trong Codex; tập tính loài làm được; microhabitat bản địa; ngày `YYYY-MM-DD`; tối đa 14 ngày bộ đếm; cá thể còn trong kho; danh sách sắp xếp ordinal không trùng.
- Ảnh v4 cho TS bỏ khối này (giống các khối U2.6–U2.8).
- Kích thước: tối đa 16 loài × khoảng 30 chuỗi + 1 bản ghi cá thể mỗi rồng, nhỏ so với save hiện tại.
- Test `Research_log_snapshot_round_trips_and_the_v9_draft_validator_rejects_bad_data` kiểm round-trip, validator và việc state v8 không đổi.

## 9. Kết quả mô phỏng nghiên cứu

Xem ECONOMY_SIMULATION.md mục Researcher: cấp nghiên cứu ở mốc 7 / 30 / 90 ngày, số lần quan sát, ngày mở Thủy Vực theo đề xuất nhiệm vụ.
