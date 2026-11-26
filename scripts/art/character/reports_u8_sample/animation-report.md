# Báo cáo animation

Bộ clip mỗi loài lấy từ khả năng sinh học (`dragon-archetypes.json`); loài không có khả năng thì không có clip, runtime không giả (Fly trên loài không bay → Idle + cảnh báo).

| Model | Bằng chứng | Clip theo sinh học | Clip đã tạo | Thiếu | Thừa | QA |
|---|---|---|---|---|---|---|
| DRG_droplet_Unstaged | SAMPLE | Idle, Eat, Sleep, Happy, Walk, Run, Swim, Rest, Wake, LookAround, Curious, Drink, Stretch, Bask, Greet, SwimIdle, SwimRest, SwimSurface, WaterEnter, WaterExit, Nuzzle | Idle, Eat, Sleep, Happy, Walk, Run, Swim, Rest, Wake, LookAround, Curious, Drink, Stretch, Bask, Greet, SwimIdle, SwimRest, SwimSurface, WaterEnter, WaterExit, Nuzzle | — | — | PASS |
| DRG_emberling_Unstaged | SAMPLE | Idle, Eat, Sleep, Happy, Walk, Run, Rest, Wake, LookAround, Curious, Drink, Stretch, Bask, Greet, Nuzzle | Idle, Eat, Sleep, Happy, Walk, Run, Rest, Wake, LookAround, Curious, Drink, Stretch, Bask, Greet, Nuzzle | — | — | PASS |
| DRG_frostfin_Juvenile | SAMPLE | Idle, Eat, Sleep, Happy, Swim, Rest, Wake, LookAround, Curious, Greet, SwimIdle, SwimRest, Nuzzle | Idle, Eat, Sleep, Happy, Swim, Rest, Wake, LookAround, Curious, Greet, SwimIdle, SwimRest, Nuzzle | — | — | PASS |
| DRG_leviathan_Adult | SAMPLE | Idle, Eat, Sleep, Happy, Swim, Rest, Wake, LookAround, Curious, Greet, SwimIdle, SwimRest, Nuzzle | Idle, Eat, Sleep, Happy, Swim, Rest, Wake, LookAround, Curious, Greet, SwimIdle, SwimRest, Nuzzle | — | — | PASS |
| DRG_magma_Unstaged | SAMPLE | Idle, Eat, Sleep, Happy, Walk, Run, Burrow, Rest, Wake, LookAround, Curious, Drink, Stretch, Bask, Greet, DigPrep, BurrowEnter, BurrowEmerge, Nuzzle | Idle, Eat, Sleep, Happy, Walk, Run, Burrow, Rest, Wake, LookAround, Curious, Drink, Stretch, Bask, Greet, DigPrep, BurrowEnter, BurrowEmerge, Nuzzle | — | — | PASS |
| DRG_sparkwing_Unstaged | SAMPLE | Idle, Eat, Sleep, Happy, Walk, Run, Fly, Rest, Wake, LookAround, Curious, Drink, Stretch, Bask, Greet, Nuzzle | Idle, Eat, Sleep, Happy, Walk, Run, Fly, Rest, Wake, LookAround, Curious, Drink, Stretch, Bask, Greet, Nuzzle, TakeOff, Glide, Land | — | — | PASS |
| DRG_sprout_Unstaged | SAMPLE | Idle, Eat, Sleep, Happy, Walk, Run, Burrow, Rest, Wake, LookAround, Curious, Drink, Stretch, Bask, Greet, DigPrep, BurrowEnter, BurrowEmerge, Nuzzle | Idle, Eat, Sleep, Happy, Walk, Run, Burrow, Rest, Wake, LookAround, Curious, Drink, Stretch, Bask, Greet, DigPrep, BurrowEnter, BurrowEmerge, Nuzzle | — | — | PASS |
| DRG_steamdrake_Unstaged | SAMPLE | Idle, Eat, Sleep, Happy, Walk, Run, Swim, Rest, Wake, LookAround, Curious, Drink, Stretch, Bask, Greet, SwimIdle, SwimRest, SwimSurface, WaterEnter, WaterExit, Nuzzle | Idle, Eat, Sleep, Happy, Walk, Run, Swim, Rest, Wake, LookAround, Curious, Drink, Stretch, Bask, Greet, SwimIdle, SwimRest, SwimSurface, WaterEnter, WaterExit, Nuzzle | — | — | PASS |
| DRG_zephyr_Unstaged | SAMPLE | Idle, Eat, Sleep, Happy, Walk, Run, Fly, Rest, Wake, LookAround, Curious, Drink, Stretch, Bask, Greet, Perch, Nuzzle | Idle, Eat, Sleep, Happy, Walk, Run, Fly, Rest, Wake, LookAround, Curious, Drink, Stretch, Bask, Greet, Perch, Nuzzle, TakeOff, Glide, Land | — | — | PASS |

TakeOff/Glide/Land là clip chuyển tiếp của loài bay (archetype winged), có trong FBX và Animation Lab; state Animator hiện chỉ gồm Idle, Walk, Run, Swim, Fly, Burrow, Sleep + Eat, Happy (enum `DragonLocomotion`).

## DRG_droplet_Unstaged (SAMPLE)

| Clip | Frame | Loop | m/s | Giãn | Nén | Trượt | Xuyên | Kết quả |
|---|---|---|---|---|---|---|---|---|
| Idle | 120 | có | 0.000 | 1.141 | 0.880 | 0.0000 | 0.0001 | PASS |
| Eat | 60 | — | 0.000 | 1.509 | 0.519 | 0.0000 | 0.0000 | PASS |
| Sleep | 120 | có | 0.000 | 1.581 | 0.553 | 0.0000 | 0.0000 | PASS |
| Happy | 45 | — | 0.000 | 1.440 | 0.697 | 0.0000 | 0.0004 | PASS |
| Walk | 22 | có | 0.199 | 1.299 | 0.700 | 0.0001 | 0.0005 | PASS |
| Run | 12 | có | 0.881 | 1.544 | 0.521 | 0.0000 | 0.0000 | PASS |
| Swim | 48 | có | 0.615 | 1.494 | 0.712 | 0.0000 | — | PASS |
| Rest | 120 | có | 0.000 | 1.194 | 0.664 | 0.0000 | 0.0000 | PASS |
| Wake | 60 | — | 0.000 | 1.383 | 0.594 | 0.0000 | 0.0000 | PASS |
| LookAround | 90 | — | 0.000 | 1.445 | 0.692 | 0.0000 | 0.0000 | PASS |
| Curious | 75 | — | 0.000 | 1.310 | 0.767 | 0.0000 | 0.0003 | PASS |
| Drink | 75 | — | 0.000 | 1.559 | 0.489 | 0.0000 | 0.0000 | PASS |
| Stretch | 90 | — | 0.000 | 1.220 | 0.795 | 0.0000 | 0.0000 | PASS |
| Bask | 120 | có | 0.000 | 1.188 | 0.664 | 0.0000 | 0.0000 | PASS |
| Greet | 60 | — | 0.000 | 1.173 | 0.868 | 0.0000 | 0.0000 | PASS |
| SwimIdle | 72 | có | 0.000 | 1.148 | 0.875 | 0.0000 | — | PASS |
| SwimRest | 120 | có | 0.000 | 1.081 | 0.917 | 0.0000 | — | PASS |
| SwimSurface | 48 | có | 0.615 | 1.356 | 0.752 | 0.0000 | — | PASS |
| WaterEnter | 60 | — | 0.000 | 1.196 | 0.824 | 0.0000 | — | PASS |
| WaterExit | 60 | — | 0.000 | 1.195 | 0.825 | 0.0000 | — | PASS |
| Nuzzle | 75 | — | 0.000 | 1.151 | 0.875 | 0.0000 | 0.0000 | PASS |

## DRG_emberling_Unstaged (SAMPLE)

| Clip | Frame | Loop | m/s | Giãn | Nén | Trượt | Xuyên | Kết quả |
|---|---|---|---|---|---|---|---|---|
| Idle | 120 | có | 0.000 | 1.155 | 0.863 | 0.0000 | 0.0001 | PASS |
| Eat | 60 | — | 0.000 | 1.491 | 0.588 | 0.0000 | 0.0000 | PASS |
| Sleep | 120 | có | 0.000 | 1.450 | 0.644 | 0.0000 | 0.0000 | PASS |
| Happy | 45 | — | 0.000 | 1.463 | 0.658 | 0.0000 | 0.0004 | PASS |
| Walk | 22 | có | 0.200 | 1.315 | 0.696 | 0.0001 | 0.0005 | PASS |
| Run | 12 | có | 0.884 | 1.545 | 0.541 | 0.0000 | 0.0000 | PASS |
| Rest | 120 | có | 0.000 | 1.178 | 0.661 | 0.0000 | 0.0000 | PASS |
| Wake | 60 | — | 0.000 | 1.321 | 0.641 | 0.0000 | 0.0000 | PASS |
| LookAround | 90 | — | 0.000 | 1.266 | 0.771 | 0.0000 | 0.0001 | PASS |
| Curious | 75 | — | 0.000 | 1.402 | 0.780 | 0.0000 | 0.0003 | PASS |
| Drink | 75 | — | 0.000 | 1.536 | 0.548 | 0.0000 | 0.0000 | PASS |
| Stretch | 90 | — | 0.000 | 1.198 | 0.804 | 0.0000 | 0.0000 | PASS |
| Bask | 120 | có | 0.000 | 1.177 | 0.661 | 0.0000 | 0.0000 | PASS |
| Greet | 60 | — | 0.000 | 1.171 | 0.846 | 0.0000 | 0.0000 | PASS |
| Nuzzle | 75 | — | 0.000 | 1.139 | 0.869 | 0.0000 | 0.0000 | PASS |

## DRG_frostfin_Juvenile (SAMPLE)

| Clip | Frame | Loop | m/s | Giãn | Nén | Trượt | Xuyên | Kết quả |
|---|---|---|---|---|---|---|---|---|
| Idle | 120 | có | 0.000 | 1.176 | 0.849 | — | 0.0000 | PASS |
| Eat | 60 | — | 0.000 | 1.207 | 0.620 | — | 0.0000 | PASS |
| Sleep | 120 | có | 0.000 | 1.315 | 0.605 | — | 0.0000 | PASS |
| Happy | 45 | — | 0.000 | 1.442 | 0.719 | — | 0.0000 | PASS |
| Swim | 48 | có | 0.543 | 1.411 | 0.712 | — | — | PASS |
| Rest | 120 | có | 0.000 | 1.245 | 0.763 | — | 0.0000 | PASS |
| Wake | 60 | — | 0.000 | 1.199 | 0.682 | — | 0.0000 | PASS |
| LookAround | 90 | — | 0.000 | 1.337 | 0.751 | — | 0.0000 | PASS |
| Curious | 75 | — | 0.000 | 1.233 | 0.816 | — | 0.0000 | PASS |
| Greet | 60 | — | 0.000 | 1.144 | 0.856 | — | 0.0000 | PASS |
| SwimIdle | 72 | có | 0.000 | 1.129 | 0.887 | — | — | PASS |
| SwimRest | 120 | có | 0.000 | 1.056 | 0.870 | — | — | PASS |
| Nuzzle | 75 | — | 0.000 | 1.094 | 0.881 | — | 0.0000 | PASS |

## DRG_leviathan_Adult (SAMPLE)

| Clip | Frame | Loop | m/s | Giãn | Nén | Trượt | Xuyên | Kết quả |
|---|---|---|---|---|---|---|---|---|
| Idle | 120 | có | 0.000 | 1.169 | 0.852 | — | 0.0000 | PASS |
| Eat | 60 | — | 0.000 | 1.201 | 0.705 | — | 0.0000 | PASS |
| Sleep | 120 | có | 0.000 | 1.438 | 0.533 | — | 0.0000 | PASS |
| Happy | 45 | — | 0.000 | 1.463 | 0.710 | — | 0.0000 | PASS |
| Swim | 48 | có | 0.813 | 1.152 | 0.864 | — | — | PASS |
| Rest | 120 | có | 0.000 | 1.146 | 0.772 | — | 0.0000 | PASS |
| Wake | 60 | — | 0.000 | 1.272 | 0.639 | — | 0.0000 | PASS |
| LookAround | 90 | — | 0.000 | 1.250 | 0.779 | — | 0.0000 | PASS |
| Curious | 75 | — | 0.000 | 1.167 | 0.867 | — | 0.0000 | PASS |
| Greet | 60 | — | 0.000 | 1.121 | 0.881 | — | 0.0000 | PASS |
| SwimIdle | 72 | có | 0.000 | 1.051 | 0.951 | — | — | PASS |
| SwimRest | 120 | có | 0.000 | 1.047 | 0.936 | — | — | PASS |
| Nuzzle | 75 | — | 0.000 | 1.106 | 0.901 | — | 0.0000 | PASS |

## DRG_magma_Unstaged (SAMPLE)

| Clip | Frame | Loop | m/s | Giãn | Nén | Trượt | Xuyên | Kết quả |
|---|---|---|---|---|---|---|---|---|
| Idle | 120 | có | 0.000 | 1.143 | 0.876 | 0.0000 | 0.0001 | PASS |
| Eat | 60 | — | 0.000 | 1.529 | 0.568 | 0.0000 | 0.0000 | PASS |
| Sleep | 120 | có | 0.000 | 1.483 | 0.634 | 0.0000 | 0.0000 | PASS |
| Happy | 45 | — | 0.000 | 1.423 | 0.676 | 0.0000 | 0.0005 | PASS |
| Walk | 22 | có | 0.198 | 1.328 | 0.688 | 0.0001 | 0.0005 | PASS |
| Run | 12 | có | 0.878 | 1.592 | 0.512 | 0.0000 | 0.0000 | PASS |
| Burrow | 60 | có | 0.000 | 1.423 | 0.679 | 0.0000 | 0.0000 | PASS |
| Rest | 120 | có | 0.000 | 1.191 | 0.659 | 0.0000 | 0.0000 | PASS |
| Wake | 60 | — | 0.000 | 1.344 | 0.628 | 0.0000 | 0.0000 | PASS |
| LookAround | 90 | — | 0.000 | 1.279 | 0.765 | 0.0000 | 0.0000 | PASS |
| Curious | 75 | — | 0.000 | 1.292 | 0.776 | 0.0000 | 0.0004 | PASS |
| Drink | 75 | — | 0.000 | 1.584 | 0.530 | 0.0000 | 0.0000 | PASS |
| Stretch | 90 | — | 0.000 | 1.230 | 0.793 | 0.0000 | 0.0000 | PASS |
| Bask | 120 | có | 0.000 | 1.187 | 0.660 | 0.0000 | 0.0000 | PASS |
| Greet | 60 | — | 0.000 | 1.155 | 0.854 | 0.0000 | 0.0000 | PASS |
| DigPrep | 45 | — | 0.000 | 1.161 | 0.848 | 0.0014 | 0.0002 | PASS |
| BurrowEnter | 60 | — | 0.000 | 1.323 | 0.711 | 0.0000 | 0.0008 | PASS |
| BurrowEmerge | 60 | — | 0.000 | 1.259 | 0.737 | 0.0000 | 0.0008 | PASS |
| Nuzzle | 75 | — | 0.000 | 1.135 | 0.861 | 0.0000 | 0.0000 | PASS |

## DRG_sparkwing_Unstaged (SAMPLE)

| Clip | Frame | Loop | m/s | Giãn | Nén | Trượt | Xuyên | Kết quả |
|---|---|---|---|---|---|---|---|---|
| Idle | 120 | có | 0.000 | 1.137 | 0.884 | 0.0000 | 0.0001 | PASS |
| Eat | 60 | — | 0.000 | 1.508 | 0.563 | 0.0000 | 0.0000 | PASS |
| Sleep | 120 | có | 0.000 | 1.426 | 0.637 | 0.0000 | 0.0000 | PASS |
| Happy | 45 | — | 0.000 | 1.393 | 0.689 | 0.0000 | 0.0005 | PASS |
| Walk | 22 | có | 0.199 | 1.290 | 0.708 | 0.0001 | 0.0005 | PASS |
| Run | 12 | có | 0.880 | 1.509 | 0.541 | 0.0000 | 0.0000 | PASS |
| Fly | 32 | có | 0.985 | 1.326 | 0.777 | 0.0000 | — | PASS |
| Rest | 120 | có | 0.000 | 1.244 | 0.659 | 0.0000 | 0.0000 | PASS |
| Wake | 60 | — | 0.000 | 1.312 | 0.635 | 0.0000 | 0.0000 | PASS |
| LookAround | 90 | — | 0.000 | 1.282 | 0.776 | 0.0000 | 0.0001 | PASS |
| Curious | 75 | — | 0.000 | 1.335 | 0.782 | 0.0000 | 0.0004 | PASS |
| Drink | 75 | — | 0.000 | 1.579 | 0.530 | 0.0000 | 0.0000 | PASS |
| Stretch | 90 | — | 0.000 | 1.361 | 0.770 | 0.0000 | 0.0000 | PASS |
| Bask | 120 | có | 0.000 | 1.173 | 0.659 | 0.0000 | 0.0000 | PASS |
| Greet | 60 | — | 0.000 | 1.150 | 0.853 | 0.0000 | 0.0000 | PASS |
| Nuzzle | 75 | — | 0.000 | 1.135 | 0.850 | 0.0000 | 0.0000 | PASS |
| TakeOff | 40 | — | 0.000 | 1.307 | 0.678 | 0.0000 | 0.0044 | PASS |
| Glide | 60 | có | 0.000 | 1.208 | 0.824 | 0.0000 | — | PASS |
| Land | 40 | — | 0.000 | 1.489 | 0.569 | 0.0000 | 0.0068 | PASS |

## DRG_sprout_Unstaged (SAMPLE)

| Clip | Frame | Loop | m/s | Giãn | Nén | Trượt | Xuyên | Kết quả |
|---|---|---|---|---|---|---|---|---|
| Idle | 120 | có | 0.000 | 1.154 | 0.871 | 0.0000 | 0.0001 | PASS |
| Eat | 60 | — | 0.000 | 1.537 | 0.529 | 0.0000 | 0.0000 | PASS |
| Sleep | 120 | có | 0.000 | 1.414 | 0.629 | 0.0000 | 0.0000 | PASS |
| Happy | 45 | — | 0.000 | 1.472 | 0.670 | 0.0000 | 0.0004 | PASS |
| Walk | 22 | có | 0.190 | 1.303 | 0.700 | 0.0000 | 0.0004 | PASS |
| Run | 12 | có | 0.842 | 1.567 | 0.524 | 0.0000 | 0.0000 | PASS |
| Burrow | 60 | có | 0.000 | 1.465 | 0.685 | 0.0000 | 0.0000 | PASS |
| Rest | 120 | có | 0.000 | 1.182 | 0.664 | 0.0000 | 0.0000 | PASS |
| Wake | 60 | — | 0.000 | 1.312 | 0.639 | 0.0000 | 0.0000 | PASS |
| LookAround | 90 | — | 0.000 | 1.286 | 0.765 | 0.0000 | 0.0001 | PASS |
| Curious | 75 | — | 0.000 | 1.437 | 0.772 | 0.0000 | 0.0003 | PASS |
| Drink | 75 | — | 0.000 | 1.591 | 0.500 | 0.0000 | 0.0000 | PASS |
| Stretch | 90 | — | 0.000 | 1.244 | 0.794 | 0.0000 | 0.0000 | PASS |
| Bask | 120 | có | 0.000 | 1.182 | 0.664 | 0.0000 | 0.0000 | PASS |
| Greet | 60 | — | 0.000 | 1.172 | 0.857 | 0.0000 | 0.0000 | PASS |
| DigPrep | 45 | — | 0.000 | 1.149 | 0.854 | 0.0012 | 0.0002 | PASS |
| BurrowEnter | 60 | — | 0.000 | 1.305 | 0.721 | 0.0000 | 0.0018 | PASS |
| BurrowEmerge | 60 | — | 0.000 | 1.249 | 0.742 | 0.0000 | 0.0018 | PASS |
| Nuzzle | 75 | — | 0.000 | 1.137 | 0.879 | 0.0000 | 0.0000 | PASS |

## DRG_steamdrake_Unstaged (SAMPLE)

| Clip | Frame | Loop | m/s | Giãn | Nén | Trượt | Xuyên | Kết quả |
|---|---|---|---|---|---|---|---|---|
| Idle | 120 | có | 0.000 | 1.106 | 0.903 | 0.0000 | 0.0000 | PASS |
| Eat | 60 | — | 0.000 | 1.196 | 0.798 | 0.0000 | 0.0000 | PASS |
| Sleep | 120 | có | 0.000 | 1.467 | 0.615 | 0.0000 | 0.0000 | PASS |
| Happy | 45 | — | 0.000 | 1.308 | 0.756 | 0.0000 | 0.0000 | PASS |
| Walk | 40 | có | 0.062 | 1.312 | 0.740 | 0.0063 | 0.0000 | PASS |
| Run | 26 | có | 0.189 | 1.415 | 0.620 | 0.0072 | 0.0000 | PASS |
| Swim | 48 | có | 0.851 | 1.438 | 0.805 | 0.0000 | — | PASS |
| Rest | 120 | có | 0.000 | 1.460 | 0.589 | 0.0000 | 0.0000 | PASS |
| Wake | 60 | — | 0.000 | 1.315 | 0.677 | 0.0000 | 0.0000 | PASS |
| LookAround | 90 | — | 0.000 | 1.266 | 0.781 | 0.0000 | 0.0000 | PASS |
| Curious | 75 | — | 0.000 | 1.135 | 0.848 | 0.0000 | 0.0002 | PASS |
| Drink | 75 | — | 0.000 | 1.234 | 0.757 | 0.0000 | 0.0002 | PASS |
| Stretch | 90 | — | 0.000 | 1.178 | 0.839 | 0.0000 | 0.0000 | PASS |
| Bask | 120 | có | 0.000 | 1.451 | 0.614 | 0.0000 | 0.0000 | PASS |
| Greet | 60 | — | 0.000 | 1.107 | 0.904 | 0.0000 | 0.0000 | PASS |
| SwimIdle | 72 | có | 0.000 | 1.135 | 0.927 | 0.0000 | — | PASS |
| SwimRest | 120 | có | 0.000 | 1.077 | 0.939 | 0.0000 | — | PASS |
| SwimSurface | 48 | có | 0.851 | 1.295 | 0.852 | 0.0000 | — | PASS |
| WaterEnter | 60 | — | 0.000 | 1.257 | 0.852 | 0.0000 | — | PASS |
| WaterExit | 60 | — | 0.000 | 1.252 | 0.855 | 0.0000 | — | PASS |
| Nuzzle | 75 | — | 0.000 | 1.114 | 0.903 | 0.0000 | 0.0000 | PASS |

## DRG_zephyr_Unstaged (SAMPLE)

| Clip | Frame | Loop | m/s | Giãn | Nén | Trượt | Xuyên | Kết quả |
|---|---|---|---|---|---|---|---|---|
| Idle | 120 | có | 0.000 | 1.137 | 0.884 | 0.0000 | 0.0001 | PASS |
| Eat | 60 | — | 0.000 | 1.508 | 0.563 | 0.0000 | 0.0000 | PASS |
| Sleep | 120 | có | 0.000 | 1.426 | 0.637 | 0.0000 | 0.0000 | PASS |
| Happy | 45 | — | 0.000 | 1.393 | 0.689 | 0.0000 | 0.0005 | PASS |
| Walk | 22 | có | 0.199 | 1.290 | 0.708 | 0.0001 | 0.0005 | PASS |
| Run | 12 | có | 0.880 | 1.509 | 0.541 | 0.0000 | 0.0000 | PASS |
| Fly | 32 | có | 0.985 | 1.326 | 0.777 | 0.0000 | — | PASS |
| Rest | 120 | có | 0.000 | 1.244 | 0.659 | 0.0000 | 0.0000 | PASS |
| Wake | 60 | — | 0.000 | 1.312 | 0.635 | 0.0000 | 0.0000 | PASS |
| LookAround | 90 | — | 0.000 | 1.282 | 0.776 | 0.0000 | 0.0001 | PASS |
| Curious | 75 | — | 0.000 | 1.335 | 0.782 | 0.0000 | 0.0004 | PASS |
| Drink | 75 | — | 0.000 | 1.579 | 0.530 | 0.0000 | 0.0000 | PASS |
| Stretch | 90 | — | 0.000 | 1.361 | 0.770 | 0.0000 | 0.0000 | PASS |
| Bask | 120 | có | 0.000 | 1.173 | 0.659 | 0.0000 | 0.0000 | PASS |
| Greet | 60 | — | 0.000 | 1.150 | 0.853 | 0.0000 | 0.0000 | PASS |
| Perch | 120 | có | 0.000 | 1.385 | 0.720 | 0.0000 | 0.0000 | PASS |
| Nuzzle | 75 | — | 0.000 | 1.135 | 0.850 | 0.0000 | 0.0000 | PASS |
| TakeOff | 40 | — | 0.000 | 1.307 | 0.678 | 0.0000 | 0.0044 | PASS |
| Glide | 60 | có | 0.000 | 1.208 | 0.824 | 0.0000 | — | PASS |
| Land | 40 | — | 0.000 | 1.489 | 0.569 | 0.0000 | 0.0068 | PASS |
