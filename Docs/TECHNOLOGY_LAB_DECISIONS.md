# Technology Lab decision and acceptance register — TL.7

**Scope:** proposal only, no production activation. All experimental code is under Editor/TechnologyLab; pure tests are in Tests/Integration~. No dependency, save schema, Domain rule, Character runtime or environment geometry change. No external packages or third-party assets copied. Existing U5.3.6 remains the control.

## Decisions

| Topic | Candidate | Decision / blocker |
|---|---|---|
| Ground avoidance | TL.1/2 sampled predictive steering | Candidate, not selected until same-map arrival/stall/safety/CPU evidence and true production route-controller replay. Fixed discs not actual dragon bodies. |
| 3D route | Existing A* and NavVolume | Keep existing A*; measure grid sweep on committed location JSON. NavVolume README documents MIT, Unity >=6000.3, Git LFS, Burst, Collections, Mathematics. Source: https://github.com/javimanotas/NavVolume . No upstream build or pinned dependency import. Run a disposable upstream project before selecting. |
| NavMesh | DotRecast / Unity AI Navigation | Defer until actual ground map scale/performance calls for replacement. No dependency change. |
| Hybrid animation | TL.4 bounded contact plane + existing CHAR.7 | Share mathematical reference with Character AI. Need real rig contact curves, joint limits, foot raycasts, slopes, root/clip interaction, skinning and mobile profiling. |
| Perception | TL.5 memory and sequence | Candidate presentation layer. Actual LOS/UID lookup, canonical eligibility on every request, no duplicate reward. Never replace Domain decision. |
| Social | TL.6 pairing | Candidate presentation layer only, compatibility supplied by Domain/design. No invented species affinity rules, no social persistence, no automatic friendship or gameplay rewards. |
| Production | TL.7 | HOLD until local evidence. No merge into main. |

## Run in Unity (no game scene required)

Open branch feat/technology-lab in a separate worktree or back up local changes. Use the project's Unity 6000.6.10f1. Open the following editor tools:

1. Steering Comparison: head-on, crossing, obstacle, mixed groups; run 400 ticks at 5/15/30 (head-on 2). Record arrivals, stalls, distances, minimum gap.
2. Routed Crowd Benchmark: save Console report. Verify valid + skipped = requested, compare same A* routes. If skipped > 0, investigate map density; do not infer 30 valid agents from a 30-agent request.
3. Authored Route Benchmark: save all Console rows; require zero spawn parity mismatch and zero dense unsafe edges for candidate promotion. Report unreachable/budget separately. Ocean/Sky same-medium only.
4. Creature Techniques: test slope, side slope, planted/unplanted and clamped offsets. Mock target occlusion, eligibility, route, facing and readiness; confirm exactly one request then resolve. Social example excludes the unwilling agent.
5. Compare using real CHAR.7 and real 7 GLBs only after character QA. Capture Unity Console, Profiler CPU/GC, 5/15/30 stress, and Android build/device measurements. Do not claim real performance from .NET.

## NavVolume isolated trial protocol (not run in this session)

Clone upstream to a directory outside Dravio, use Git LFS per README, record commit and exact license/dependency versions. Open its project in Unity 6000.6.10f1 and run upstream demos/tests. Build a disposable test scene from authored Ocean/Sky volumes (not production save), bake free-space geometry, test radius margins and path endpoints against the same directed pairs used by TL.3. Compare bake time, memory, path query CPU/p95, route length, dense segment validation, collision cases and IL2CPP Android build. Reject any path outside authored volume or across a forbidden transition. Verify package/source/art licenses separately; only then prepare an explicit vendor/package ADR. No automatic download, installation or package manifest change in this branch.

## Proposed promotion thresholds (not measured / not approved)

- Hard gates: no invalid canonical command, no duplicate rewards, no out-of-habitat moves, no observed swept overlap, no dense unsafe segments, no stale reservations, no new save schema or unapproved gameplay rules.
- For steering, improve arrival count or reduce stalled ticks vs actual production baseline on representative maps; no regression on critical scenarios. Measure worst-case CPU and allocation; establish a device baseline before choosing numerical FPS/CPU budget.
- For contact, no observed penetration/foot slide regression on real meshes, no forced impossible joint angles, no root drift, motion readiness respected.
- For perception/social, stable UID identity and mutual eligibility, expiry/removal cancellation; no implicit all-species social rule.

Remaining evidence: Unity compile/PlayMode, full production controller comparison, actual NavVolume build/benchmark, real GLB hybrid animation, Android performance and project-owner ADR approval. These cannot be inferred from CI. The experimental suite is not a completed production integration.

## Follow-up gates

TL.8 broad-phase and adaptive route experiments passed CI. TL.9 recovery and goal-lease prototypes and TL.10 controller trace added; use TECHNOLOGY_LAB_RESULTS.md for exact benchmark and collection instructions. Do not promote a candidate solely on synthetic results. The actual controller has editor-only tracing but no runtime candidate integration or measured A/B replay yet. Retain original production routing until local traces and model readiness are available.
