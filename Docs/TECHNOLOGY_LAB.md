# Dravio Technology Lab — TL.1 steering research

Base: feat/u5-multimedium-world @ 1cf4f4c6. Scope is experimental, isolated, editor-only. No production behavior, Domain, save, Character, package manifest, generated scene or other AI branch changes. All implementation code in this lab is original; no upstream source or art assets have been copied.

## Research and selection (2026-11-26; documentation review only)

| Candidate / source | License reported by upstream | Fit / decision |
|---|---|---|
| [Reynolds, Steering Behaviors for Autonomous Characters (1999)](https://www.red3d.com/cwr/steer/gdc99/) | research reference, not imported software | Separate goal selection, steering and locomotion. Reference separation/alignment/cohesion and predictive avoidance; implement an independent lab. |
| [SebLague/Boids](https://github.com/SebLague/Boids) | MIT | Useful visual reference, not a collision safety guarantee; GPU/ECS approach not justified for 5–30 residents yet. Group preference must be provided by Domain/approved design, never all species by default. |
| [NavVolume](https://github.com/javimanotas/NavVolume) | MIT | 3D SVO, weighted A*, ORCA and asynchronous queries. README requires Unity 6000.3+, Burst, Collections, Mathematics. Dravio 6000.6.10f1 meets stated editor minimum, actual dependency compatibility/IL2CPP untested. Priority 3D sandbox after authored volume/transition audit. |
| [RVO2-CS](https://github.com/snape/RVO2-CS) | Apache-2.0 | ORCA candidate for ground avoidance. Verify exact tagged runtime, target frameworks, dependency and NOTICE obligations before import. This lab is NOT ORCA. |
| [DotRecast](https://github.com/ikpil/DotRecast) | Zlib | C# navmesh, path queries and crowd modules; promising when terrain grows. Keep current small-world A* until geometry/perf evidence supports replacement. |
| [Unity AI Navigation 6000.6](https://docs.unity.com/en-us/engine/6000.6/manual/packages-list/packages-all/pack-safe/com-unity-ai-navigation) | Unity package terms, not assumed MIT | Official 2.0.17 listed for 6000.6. Ground surfaces/links candidate; does not solve free-flight volumes. Package not installed. |
| [Bonehead by WeaverDev](https://weaverdev.itch.io/procedural-animation-tutorial) | MIT code; sample gecko personal use | Procedural leg/body/head/tail design reference for CHAR.7. Never distribute the sample model without rights. |
| [Sopiro/Unity-Procedural-Animation](https://github.com/Sopiro/Unity-Procedural-Animation) | MIT | Multi-leg contact and procedural gait reference. |
| [OpenIK](https://github.com/WouterApts/openik-unity) | MIT | Joint limits and multiple IK solvers; compare with existing DragonFootGrounding on real models; not installed. |

Before any import: select immutable commit/tag; record exact license and copyright/NOTICE, distinguish source/art, review dependencies and binary assets, check build and target platform, create THIRD_PARTY_NOTICES as applicable. No package is accepted solely because its README says Unity 6. No third-party install in this PR.

## TL.1 experiment

Unity menu: Dravio > Technology Lab > Steering Comparison. No scene open/save, no catalog or GLB needed. Editor-only 2D synthetic arena, squares in the display represent discs in the solver. Stop-only reference is a deliberately simplified straight-line approach + collision stop, NOT the entire U5.3.6 A* controller. PredictiveSteering samples heading/speed candidates, scores goal progress, optional same-group alignment/cohesion and short-horizon relative motion risk. Both use identical bounds, speed/radius, fixed dt=0.05s, starting positions, goal and swept collision checks. The solver is neither full ORCA nor a global path planner.

Reservation algorithm: snapshot positions and desired velocity, reserve all stationary trajectories, visit agents with rotating priority, replace only with a safe swept trajectory against all current reservations; commit simultaneously. Initial overlap is rejected. The pairwise continuous linear motion check prevents tunneling in this simplified model. Future prediction is heuristic; safety is enforced by the actual-step test. No arbitrary species/group membership assumptions in production; groups in the arena are artificial.

Scenarios: head-on (2), crossing (5/15/30), central circular obstacle, mixed groups. Run/step/reset, compare 400 ticks (20 simulated seconds). Compare arrived agents, stalled agent ticks, traveled distance, sampled minimum gap, CPU time. Safety tests separately measure swept distance, including between frames; the displayed minimum gap is sampled only. No guarantee of completion in crowded/blocked cases, no rigidbody/mesh/3D/animation validation. O(n² × sampled candidates); needs spatial partitioning if actual profiling warrants.

## Reproducible tests

From repository root with .NET 8:
```powershell
dotnet test "Assets/Dravio/Tests/Integration~/Dravio.LivingIntegration.Tests.csproj" --filter FullyQualifiedName~SteeringLabTests --logger "console;verbosity=detailed"
```
Checks: head-on reference deadlock vs steering progress, no swept overlap for crowd/obstacle/group scenarios, determinism, invalid input, speed bounds, comparison report. CI on Windows/Linux includes these tests in the existing projection-policy step. CPU numbers are synthetic managed CPU timings; no mobile FPS claim. The lab deliberately does not alter the production controller.

## Promotion plan

- TL.2 compare with real U5.3.6 route-following and fixed body radii from CHAR.7, same map/seed/goals, include near-obstacle and blocked narrow passage. Compare actual A* baseline, not just straight-line reference. Profile CPU/GC at 5/15/30 in Unity and on Android. Preserve canonical path validation, occupancy and motion readiness.
- TL.3 evaluate NavVolume in a separate project/branch using authored Ocean/Sky 3D volumes and transitions. Reject out-of-volume paths and verify smoothing clearance, landing/entry and IL2CPP builds. Only adopt if better than existing A* on representative maps.
- TL.4 Character AI compare procedural foot contacts/body/neck/tail with current Rig v4/CHAR.7 on 7 real GLBs. Domain AI evaluate perception/action sequence and social candidate DTO, no gameplay rule activation without ADR.
- Production integration gate: local Unity compile + PlayMode; no collision regression, measurable reduction in stalls/arrival times, no new gameplay or save changes, measured Android performance and approved license register. Neither synthetic success nor a permissive license alone is sufficient.

Owner: integration lead. No Unity Editor run or real GLB experiment was performed in this remote session.
