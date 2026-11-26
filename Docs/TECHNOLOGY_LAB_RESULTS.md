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
