# U2.10 — Prototype & API Integration Guide

> Quyết định: [ADR 0204](../adr/0204-u2-10-research-social-progression.md) mục E. Code: `Assets/Dravio/Domain/Queries/ProgressionQueries.cs` và các module trong `Domain/Behavior`, `Domain/Progression`.
> **Không có lệnh mới, không event mới, không đổi save (v8).** Mọi hàm là truy vấn chỉ đọc trên `GameSession.State`, tất định, gọi bao nhiêu lần cũng được. Không cần sửa HUD, Character Runtime, Environment, Scene hay Navigation để thử: các DTO có thể đưa vào overlay debug hoặc test.

## 1. Bảng hàm

| Hàm | Trả về | Dùng cho |
|---|---|---|
| `ResearchProfile(s, speciesId, log?, pacing?)` | `ResearchProfileDto { Species, Individuals[] }` | Trang loài trong Codex |
| `IndividualResearch(s, uid, log?, pacing?)` | `IndividualResearchProfile` (null nếu không có / đã thả) | Hộ chiếu rồng: "Hiểu về bé" |
| `ObservationOpportunities(s, nowMs, phase, log?, pacing?)` | `ObservationOpportunity[]`, đáng quan sát trước | Dấu "quan sát" trên Sanctuary. Rỗng khi nghiên cứu tắt |
| `SocialInteractionCandidates(s, nowMs, phase)` | `SocialInteractionCandidate[]` | Tầng Sanctuary: cặp chơi/nghỉ/tò mò, giữ khoảng cách |
| `SocialProfile(s, uid)` | `SocialProfile` | Hộ chiếu rồng: hòa đồng / thích không gian riêng |
| `SocialRelation(s, a, b)` | `SocialRelation` (null nếu a = b hoặc không có) | Tooltip quan hệ |
| `QuestRequirements(s, log?, pacing?, rules?)` | `QuestStatusView[]` | Bảng nhiệm vụ / hành trình |
| `Overview(s, log?, pacing?, quests?)` | `ProgressionOverviewDto` | Màn tổng quan tiến trình dài hạn |
| `Rules(pacing?, social?, quests?)` | `RuleFlagsDto` | UI biết hệ nào đang bật |
| `SanctuaryEconomy(s, nowMs)` | `SanctuaryEconomyDto` (gói Starter còn hôm nay, giá; dự án đã xây, giá và vật liệu dự án tiếp theo, `NextProjectBlockedBy`) | Cửa hàng gói, nút "Xây dự án Sanctuary" (ADR 0205) |
| `ResearchLog.LastNightSampleMs(lastOnlineMs, nowMs, utcOffsetMinutes)` + `log.WriteNightJournal(s, sampleMs, nowMs, pacing)` | `ResearchFinding[]` | Chỉ cho test / mô phỏng. Ở game thật, dispatcher tự ghi nhật ký đêm khi quyết toán offline và phát event `nightJournalWritten` |

`log` bỏ trống = sổ nghiên cứu của save (`GameState.Research`, Schema v9).

### Lệnh và event U2.10 (ADR 0205)

| Lệnh | Lỗi | Event |
|---|---|---|
| `BuildSanctuaryProjectCommand(requestId)` | `not-enough-gold`, `not-enough-materials`, `projects-maxed` | `sanctuaryProjectBuilt { index, costGoldMicro, materialEach }` |
| `ObserveDragonCommand(requestId, uid, phase)` (phase = pha ngày đêm Sanctuary đang hiển thị) | `unknown-dragon`, `not-observable`, `observation-cooldown` | `dragonObserved { dragonUid, speciesId, findings, deferredByDailyLimit }` |
| `BuyPackCommand(requestId, "starter")` (đã có) | thêm `pack-daily-limit` (gói thứ 6 trong ngày UTC) | `packBought` (giá 150 G) |
| `AdvanceTimeCommand(Offline)` (đã có) | — | thêm `nightJournalWritten { sampleAtMs, findings, speciesIds }` khi offline qua 02:00 UTC |

Tham số luật bỏ trống = bộ luật **Production** (đều tắt). `log` là `ResearchLog`. Ở production chưa có nơi lưu `ResearchLog` (Schema v9 chờ duyệt), nên truyền `null`. Khi thử nội bộ, tầng Platform có thể giữ một `ResearchLog` trong bộ nhớ, nhưng nó mất khi thoát.

`phase` là `Dravio.Domain.Behavior.DayPhase`, cùng thứ tự với `Dravio.EnvironmentArt.DayPhase`: `(DayPhase)(int)timeOfDay.Phase`.

## 2. DTO chính

### `SpeciesResearchProfile`

| Trường | Ý nghĩa |
|---|---|
| `Gated` | false = luật tắt: mọi phần Codex mở cho loài đã khám phá |
| `Tier` | Unknown / Sighted / Profiled / LifeCycle / Ethology |
| `RevealedSections` | Identity, Habitat, Physiology, Behavior |
| `ObservableBehaviors`, `ObservedBehaviors` | Nhật ký tập tính |
| `ObservedPhases`, `CircadianCovered` | Chu kỳ ngày đêm |
| `NativeHabitats`, `ObservedHabitats` | Sinh cảnh phù hợp |
| `DistinctDays`, `RequiredDistinctDays`, `EthologyProgressPct` | Thanh tiến độ |
| `NextStepKey` | `research.next.<own|viewProfile|raiseToAdult|observe|returnTomorrow|done>` |
| `Individuals` | Số cá thể trong kho |

### `IndividualResearchProfile`

`Observations`, `RevealedTraits` (archetype, chronotype, favoriteHabitat, favoriteDiet), `Archetype?`, `FavoriteHabitatId`, `FavoriteDiet?` (null khi chưa hé lộ và luật bật), `ActivePhases`, `RestPhases`, `NextStepKey` (`research.individual.next.<observe|observeOtherPhase|observeAtHome|observeFeeding|done>`).

### `ObservationOpportunity`

`Uid`, `SpeciesId`, `Behavior`, `Phase`, `ZoneId`, `HabitatId`, `Availability` (`ok` / `observation-cooldown`), `CooldownRemainingMs`, `SpeciesFindings[]` (đã áp giới hạn ngày), `DeferredByDailyLimit`, `IndividualReveals[]`, `Worthwhile`, `HintKey` (`research.hint.<newBehavior|newPhase|newHabitat|individual|tomorrow|cooldown|nothingNew>`).

### `SocialInteractionCandidate`

`Kind` (PlayTogether / RestTogether / CuriousApproach / KeepDistance), `InitiatorUid`, `PartnerUid` (null với KeepDistance), `ZoneId`, `HabitatId`, `Score`, `Affinity`, `Familiarity`, `MinDistanceDm` (decimét, chia 10 ra mét), `StartedAtMs`, `UntilMs`, `Role` (luôn Presentation), `AffectsGameplay` (luôn false), `ReasonKey` (`social.<kind>`).

### `QuestStatusView`

`QuestId`, `Category`, `State` (Completed / Ready / InProgress / Locked), `IsCanonicalGate`, `LockedBy[]` (`zone:<id>`, `quest:<id>`), `Requirements[]` (`Def.Kind`, `Def.Key` = `quest.req.<kind>`, `State` Met / Unmet / Unknown / Blocked, `Current`, `Target`, `Detail`), `Reward`, `CanComplete` (luôn false ở production), `ProgressPct`.

### `ProgressionOverviewDto`

`UnlockedZones`, `UnlockedHabitats` / `TotalHabitats`, `SpeciesDiscovered` / `SpeciesTotal`, `CodexCells` / `CodexCellsTotal` (64), `ResearchTiers` (số loài ở cấp ≥ mỗi tier), `QuestsCompleted` / `Ready` / `InProgress` / `Locked`, `Materials`, `SuggestedGoals` (tối đa 3 khóa i18n, không bao giờ là vàng), `Rules`.

## 3. Ví dụ tích hợp

### Codex

```csharp
using Dravio.Domain.Queries;

ResearchProfileDto r = ProgressionQueries.ResearchProfile(session.State, "droplet");
codexPage.ShowSections(r.Species.RevealedSections);           // production: cả 4 phần
codexPage.ShowEthology(r.Species.ObservedBehaviors, r.Species.ObservableBehaviors, r.Species.EthologyProgressPct);
foreach (IndividualResearchProfile ind in r.Individuals) codexPage.AddIndividual(ind.Uid, ind.Archetype, ind.FavoriteHabitatId);
```

### Sanctuary (cùng nhịp với `BehaviorQueries.All`, không gọi mỗi frame)

```csharp
DayPhase phase = (DayPhase)(int)timeOfDay.Phase;
long now = clock.NowMs();
foreach (ObservationOpportunity o in ProgressionQueries.ObservationOpportunities(session.State, now, phase, researchLog, pacing))
    if (o.Worthwhile) markers.ShowObserveHint(o.Uid, o.HintKey);
foreach (SocialInteractionCandidate c in ProgressionQueries.SocialInteractionCandidates(session.State, now, phase))
    sanctuary.ApplySocial(c);    // trình diễn; được phép bỏ qua
```

Gọi lại khi revision của state đổi, pha đổi, hoặc `UntilMs` nhỏ nhất đã qua (giống U5.3).

### Bảng nhiệm vụ

```csharp
foreach (QuestStatusView q in ProgressionQueries.QuestRequirements(session.State))
{
    questList.Add(q.QuestId, q.State, q.ProgressPct);
    foreach (QuestRequirementStatus req in q.Requirements)
        questList.AddLine(req.Def.Key, req.State, req.Current, req.Target);   // Unknown → "cần bật nghiên cứu"
}
```

Không có nút "Hoàn thành" ở production (`CanComplete` = false). Thủy Vực vẫn mở bằng `UnlockZone` khi `Ecosystem.ZoneUnlocks` báo đủ điều kiện (`Assets/Dravio/Domain/INTEGRATION.md` mục 3).

## 4. Khóa i18n mới

- `research.next.returnTomorrow`; `research.individual.next.<observe|observeOtherPhase|observeAtHome|observeFeeding|done>`; `research.hint.<newBehavior|newPhase|newHabitat|individual|tomorrow|cooldown|nothingNew>`; `research.finding.<behavior|phase|habitat|individual>`; `research.trait.<archetype|chronotype|favoriteHabitat|favoriteDiet>`.
- `social.<playTogether|restTogether|curiousApproach|keepDistance>`; `social.style.<gregarious|tolerant|solitary>`; `social.basis.<smallLandCommon|pondGroups|meadowGrazer|flock|serpentAmphibious|territorialBasker|school|slowGrazer|pollinatorFlock|largeRaptor|giantMarine|nocturnalFlyer|legendary|legendaryGentle>`; `social.familiarity.<parentChild|siblings|halfSiblings|grandparent|sharedHabitat|sameSpecies>`.
- `quest.<id>.name`; `quest.req.<researchTier|adultWithLocomotion|materialDelivery|residentsThriving|residentsAtHome|circadianRecords|individualsKnown|speciesRecordedInRealm|routeAvailable>`; `quest.state.<completed|ready|inProgress|locked>`; `quest.reward.<worldGate|codexNote|decorBlueprint|title>`.

## 5. Bộ luật

| Bộ luật | Production | Đề xuất (test / mô phỏng) |
|---|---|---|
| `ResearchPacing` | `Production` (từ ADR 0205: ghi phát hiện, 2/loài/ngày, Ethology ≥ 2 ngày, nhật ký đêm; **không** khóa Codex) | `ProposedU210NightJournal` (có khóa Codex); `Disabled` = hành vi U2.9; `UnpacedU29` để so sánh |
| `SocialRules` | `Production` (không tác động gameplay) | `ProposedForReview` (±5 mục tiêu comfort C1) |
| `QuestRules` | `Production` (không hoàn thành) | `ProposedForReview` (`CanComplete` khi Ready) |
| Comfort drift, năng lượng, thiên tai | tắt (ADR 0202 / 0203) | không đổi ở U2.10 |

## 6. Prototype đã kiểm

- `Assets/Dravio/Tests/Domain/ResearchSocialProgressionTests.cs` — 22 test chức năng.
- `Assets/Dravio/Tests/Domain/Simulation/U210EconomySimulationTests.cs` — tất định, không đổi kinh tế khi chèn truy vấn U2.10, biến thể giới hạn Starter, Researcher mở thế giới chỉ qua lệnh canonical.
- Chưa chạy trong Unity Editor. Các DTO không tham chiếu UnityEngine; assembly `Dravio.Domain` không đổi phụ thuộc.
