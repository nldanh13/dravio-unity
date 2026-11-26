# Technology Lab — measured baseline and TL.8 optimization

## Verified baseline

Source: [CI 45739636249](https://github.com/nldanh13/dravio-unity/actions/runs/45739636249), commit 22746972. Integration 59 tests passed on Linux and Windows; same routed-crowd counts on both. Numbers are synthetic simulation on authored JSON, not measured game FPS.

| Requested / valid | Stop arrivals | Steering arrivals | Stop stalled agent ticks | Steering stalled agent ticks | Steering sampled min gap |
|---|---:|---:|---:|---:|---:|
| 5 / 5 | 4 | 5 | 336 | 0 | .1351m |
| 15 / 14 | 0 | 14 | 4543 | 0 | .0250m |
| 30 / 30 | 0 | 16 | 11568 | 167 | .0250m |

Same static A* paths, .55m/s, 400 ticks at .05s. One skipped endpoint at 15 requested. Not a full replay of U5.3.6 runtime replan or animation. Thirty residents still incomplete at 20 seconds. Min gap sampled; separate swept collision tests passed for synthetic scenarios.

| Habitat / medium | .45m successful directed pairs | .30m successful directed pairs |
|---|---:|---:|
| Origin pond bank ground | 4/12 | 6/12 |
| Origin meadow ground | 20/20 | 20/20 |
| Origin warm rocks ground | 6/12 | 7/12 |
| Ocean swim (3 habitats) | 46/46 | 46/46 |
| Sky air | 12/12 | 12/12 |

Zero dense unsafe edges and zero budget failures reported in these grid sweeps. A failed search does not establish physical disconnection. The 3D authored endpoints are all direct; this dataset alone does not justify a new volumetric library.

Synthetic crossing 30: stop-only 0/30, predictive 30/30; Ubuntu CPU 27.11ms vs 816.64ms for 400 ticks. These are one-run aggregate managed timings, not a device or production budget.

## TL.8 experiment

- Opt-in broad-phase candidate: build conservative neighbor lists once per tick. Relative motion over .8s cannot reduce center separation by more than sum of max speeds × horizon. Include radii, margin, scoring gap and epsilon. Preserve ascending neighbor order. Actual swept safety checks unchanged. Compare all-agent and filtered solver positions each tick, final velocities and metrics, exact replay plus reduced swept pair-check count; CPU timing diagnostic only. Still O(n²) pair construction; no spatial hash, ORCA or claim of improved real Unity performance.
- Adaptive lattice candidate: try .45/.30/.20m with aggregate budget 2048 nodes, first attempt gets half budget. Same geometry and obstacle clearance; never loosen margin. Dense .025m segment validation before accepting each route; reject unsafe result and retry where possible. Report rescued, regressed, rejected and aggregate expanded nodes. Coarse and fine may both fail; no unchecked fallback or new geometry.
- Both candidates are editor/test-only. Default lab solver remains all-agent reference; production A* and resident controller unchanged. Need review of latest CI results, real body clearance, Unity profile and complete baseline runtime replay before promotion.

Run `dotnet test "Assets/Dravio/Tests/Integration~/Dravio.LivingIntegration.Tests.csproj" --logger "console;verbosity=detailed"` and look for LAB BROAD / LAB ADAPT. The Authored Route Benchmark editor menu also outputs adaptive comparison.

## TL.8 verified results

Source: [CI 45740007121](https://github.com/nldanh13/dravio-unity/actions/runs/45740007121), commit 2c6fc288, 70 Integration tests, Windows/Linux green.

| Synthetic case | Original swept checks | Broad phase checks | Original CPU ms / 400 ticks | Broad phase CPU ms / 400 ticks |
|---|---:|---:|---:|---:|
| Head-on 2 | 11616 | 4704 | 5.56 | 3.92 |
| Crossing 5 | 182400 | 71040 | 39.42 | 20.63 |
| Crossing 30 | 9817207 | 3707484 | 1602.02 | 685.96 |
| Obstacle 30 | 9359493 | 2859035 | 1579.02 | 599.50 |
| Mixed groups 15 | 2386101 | 1100164 | 420.18 | 220.69 |

Exact trajectory/metric equivalence in tested replay. CPU timings are diagnostic, not Unity frame or Android budget.

Adaptive A* rescued 4 pond bank and 6 warm rocks directed routes: Origin 30/44 -> 40/44, no regression/dense rejection, max expanded 605 among pond bank pairs. Ocean 46/46 and Sky 12/12 unchanged. Additional CPU cost argues for fallback only, not unconditional use. Four pond bank pairs remain unresolved.

## TL.9 crowd coordination

- Independent exclusive goal-lease queue, FIFO timestamp then UID tie-break, expiry and eligibility cancellation, one claim per UID. Duplicate requests cannot extend the lease. This is a tested prototype, NOT connected to gameplay food/point ownership or the existing Domain allocation. 
- RecoveryFollower preserves the exact assigned goal, detects lack of waypoint progress over 40 ticks (2s at .05s), tries a dense-validated adaptive reroute around current resident discs, at most one attempt per tick / 768 expanded nodes per attempt. Goal occupied -> defer search; failure retains existing route. No teleport, goal reassignment or reduced separation.
- Routed crowd comparison uses same initial placements and routes, broad phase in both steering variants; 400 and 800 ticks. Reports actual arrivals, stopped agent ticks, safety gap, replan attempts/acceptance/failure and nodes. Tests assert swept safety, speed bound, immutable goal and budget, not superiority. Recovery need not solve every crowd.
- Unity: Dravio > Technology Lab > Crowd Recovery Benchmark.

## TL.10 actual-controller baseline collection

The real LivingSanctuaryPresentation has opt-in UNITY_EDITOR-only instrumentation; no production movement algorithm change. In Play Mode on the existing OriginMultiMediumWorld scene, select the GameObject with LivingSanctuaryPresentation, open component context menu:
1. Technology Lab/Start controller trace
2. Play the same scripted scenario with 2+ eligible dragon models (observe, explore, feed, rest, move/storage as needed). Captures at most 1200 ready controller frames.
3. Technology Lab/Stop and report trace
4. Technology Lab/Export trace CSV before exiting Play Mode. Select your output location. No automatic file writes, no UID/save content in trace.

Records per-frame projected resident count, motion attempts/arrival calls (not unique dragons), moving count, dynamic step blocks (includes geometry rejection), route plans/failures, clip substitutions, minimum sampled center separation, motion-loop and Sync CPU p95. No complete frame timing, no mesh collision proof, no GC capture, no Android measurement. Trace sampling overhead is outside motion/Sync timing but can affect overall frame rate. Use Unity Profiler separately for whole-frame and allocations. The actual-controller baseline is not yet measured and does not replay candidate trajectories. A later opt-in candidate A/B run must use matching saves, scene, actions and motion contracts before promotion.

TL.9/10 CI status should be checked on GitHub; actual Unity execution remains pending.
