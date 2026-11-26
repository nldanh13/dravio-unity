# Animation QA: rig v4 (U1-CHAR.6) → rig v5 (U1-CHAR.7)

Cùng công cụ `anim_qa.py`, cùng ngưỡng trong `character-budgets.json` (`animationQa`). Số **đậm** là vượt ngưỡng (giãn p99.9 > maxStretchP999, nén p0.1 < minCompressP001).

| Model | rig v4 (U1-CHAR.6) | rig v5 (U1-CHAR.7) | Cảnh báo rig v4 (U1-CHAR.6) | Cảnh báo rig v5 (U1-CHAR.7) |
|---|---|---|---|---|
| DRG_droplet_Unstaged | PASS | PASS | 0 | 0 |
| DRG_emberling_Unstaged | PASS | PASS | 0 | 0 |
| DRG_frostfin_Juvenile | PASS | PASS | 0 | 0 |
| DRG_leviathan_Adult | PASS | PASS | 0 | 0 |
| DRG_magma_Unstaged | không có (chưa rig) | PASS | — | 0 |
| DRG_sparkwing_Unstaged | không có (chưa rig) | PASS | — | 0 |
| DRG_sprout_Unstaged | PASS | PASS | 0 | 0 |
| DRG_steamdrake_Unstaged | PASS | PASS | 0 | 0 |
| DRG_zephyr_Unstaged | PASS | PASS | 0 | 0 |

## DRG_droplet_Unstaged

| Clip | giãn rig v4 (U1-CHAR.6) | giãn rig v5 (U1-CHAR.7) | nén rig v4 (U1-CHAR.6) | nén rig v5 (U1-CHAR.7) | vùng tệ nhất (rig v5 (U1-CHAR.7)) |
|---|---|---|---|---|---|
| Idle | 1.141 | 1.141 | 0.880 | 0.880 | Pelvis 61%, Tail1 24% |
| Eat | 1.509 | 1.509 | 0.519 | 0.519 | Neck 53%, Head 22% |
| Sleep | 1.581 | 1.581 | 0.553 | 0.553 | Neck 42%, Head 27% |
| Happy | 1.440 | 1.440 | 0.697 | 0.697 | Pelvis 54%, Tail1 20% |
| Walk | 1.291 | 1.291 | 0.688 | 0.688 | Pelvis 38%, HindFoot_L 21% |
| Run | 1.534 | 1.534 | 0.513 | 0.513 | Pelvis 34%, HindFoot_L 28% |
| Swim | 1.494 | 1.494 | 0.712 | 0.712 | Pelvis 56%, Tail1 21% |
| Rest | 1.194 | 1.194 | 0.664 | 0.664 | Pelvis 35%, HindLower_L 25% |
| Wake | 1.383 | 1.383 | 0.594 | 0.594 | Neck 28%, Head 15% |
| LookAround | 1.445 | 1.445 | 0.692 | 0.692 | Neck 28%, Head 24% |
| Curious | 1.310 | 1.310 | 0.767 | 0.767 | Pelvis 34%, EarFin_R 10% |
| Drink | 1.559 | 1.559 | 0.489 | 0.489 | Neck 55%, Head 21% |
| Stretch | 1.220 | 1.220 | 0.795 | 0.795 | Pelvis 38%, Spine 27% |
| Bask | 1.188 | 1.188 | 0.664 | 0.664 | Pelvis 32%, HindLower_L 27% |
| Greet | — | 1.173 | — | 0.868 | Pelvis 51%, Tail1 19% |
| SwimIdle | — | 1.148 | — | 0.875 | Pelvis 57%, Tail1 21% |
| SwimRest | — | 1.081 | — | 0.917 | Neck 26%, Pelvis 22% |
| SwimSurface | — | 1.356 | — | 0.752 | Pelvis 43%, Tail1 18% |
| WaterEnter | — | 1.196 | — | 0.824 | Pelvis 58%, Tail3 18% |
| WaterExit | — | 1.195 | — | 0.825 | Pelvis 59%, Tail3 18% |

## DRG_emberling_Unstaged

| Clip | giãn rig v4 (U1-CHAR.6) | giãn rig v5 (U1-CHAR.7) | nén rig v4 (U1-CHAR.6) | nén rig v5 (U1-CHAR.7) | vùng tệ nhất (rig v5 (U1-CHAR.7)) |
|---|---|---|---|---|---|
| Idle | 1.155 | 1.155 | 0.863 | 0.863 | Tail1 61%, Pelvis 38% |
| Eat | 1.491 | 1.491 | 0.588 | 0.588 | Neck 54%, HindFoot_R 11% |
| Sleep | 1.450 | 1.450 | 0.644 | 0.644 | Neck 64%, Pelvis 17% |
| Happy | 1.463 | 1.463 | 0.658 | 0.658 | Tail1 56%, Pelvis 37% |
| Walk | 1.302 | 1.302 | 0.686 | 0.686 | Pelvis 41%, HindFoot_R 17% |
| Run | 1.542 | 1.542 | 0.532 | 0.532 | Pelvis 39%, HindFoot_L 21% |
| Rest | 1.178 | 1.178 | 0.661 | 0.661 | Pelvis 40%, HindLower_L 25% |
| Wake | 1.321 | 1.321 | 0.641 | 0.641 | Neck 31%, HindFoot_R 15% |
| LookAround | 1.266 | 1.266 | 0.771 | 0.771 | Neck 59%, Head 18% |
| Curious | 1.402 | 1.402 | 0.780 | 0.780 | Tail1 38%, Pelvis 32% |
| Drink | 1.536 | 1.536 | 0.548 | 0.548 | Neck 55%, HindFoot_R 12% |
| Stretch | 1.198 | 1.198 | 0.804 | 0.804 | Pelvis 48%, Spine 27% |
| Bask | 1.177 | 1.177 | 0.661 | 0.661 | Pelvis 38%, HindLower_L 25% |
| Greet | — | 1.171 | — | 0.846 | Tail1 53%, Pelvis 35% |

## DRG_frostfin_Juvenile

| Clip | giãn rig v4 (U1-CHAR.6) | giãn rig v5 (U1-CHAR.7) | nén rig v4 (U1-CHAR.6) | nén rig v5 (U1-CHAR.7) | vùng tệ nhất (rig v5 (U1-CHAR.7)) |
|---|---|---|---|---|---|
| Idle | 1.176 | 1.176 | 0.849 | 0.849 | Spine4 48%, Tail1 41% |
| Eat | 1.207 | 1.207 | 0.620 | 0.620 | Spine1 59%, Head 30% |
| Sleep | 1.315 | 1.315 | 0.605 | 0.605 | Spine1 73%, Head 20% |
| Happy | 1.442 | 1.442 | 0.719 | 0.719 | Tail1 49%, Spine4 48% |
| Swim | 1.411 | 1.411 | 0.712 | 0.712 | Fin_L 21%, Spine4 21% |
| Rest | 1.245 | 1.245 | 0.763 | 0.763 | Spine2 42%, Spine1 24% |
| Wake | 1.199 | 1.199 | 0.682 | 0.682 | Spine1 58%, Head 23% |
| LookAround | 1.337 | 1.337 | 0.751 | 0.751 | Spine1 72%, Head 19% |
| Curious | 1.233 | 1.233 | 0.816 | 0.816 | Spine4 46%, Tail1 41% |
| Greet | — | 1.144 | — | 0.856 | Spine4 43%, Tail1 39% |
| SwimIdle | — | 1.129 | — | 0.887 | Spine4 23%, Fin_R 20% |
| SwimRest | — | 1.056 | — | 0.870 | Spine1 58%, Spine4 10% |

## DRG_leviathan_Adult

| Clip | giãn rig v4 (U1-CHAR.6) | giãn rig v5 (U1-CHAR.7) | nén rig v4 (U1-CHAR.6) | nén rig v5 (U1-CHAR.7) | vùng tệ nhất (rig v5 (U1-CHAR.7)) |
|---|---|---|---|---|---|
| Idle | 1.169 | 1.169 | 0.852 | 0.852 | Spine8 45%, Tail1 26% |
| Eat | 1.201 | 1.201 | 0.705 | 0.705 | Spine1 43%, Neck 43% |
| Sleep | 1.438 | 1.438 | 0.533 | 0.533 | Tail3 39%, Tail2 21% |
| Happy | 1.463 | 1.463 | 0.710 | 0.710 | Spine8 45%, Tail3 27% |
| Swim | 1.152 | 1.152 | 0.864 | 0.864 | Spine8 34%, Spine6 24% |
| Rest | 1.146 | 1.146 | 0.772 | 0.772 | Spine1 24%, Tail3 22% |
| Wake | 1.272 | 1.272 | 0.639 | 0.639 | Tail3 32%, Spine8 20% |
| LookAround | 1.250 | 1.250 | 0.779 | 0.779 | Spine1 45%, Neck 43% |
| Curious | 1.167 | 1.167 | 0.867 | 0.867 | Spine8 51%, Tail1 22% |
| Greet | — | 1.121 | — | 0.881 | Spine8 46%, Tail1 27% |
| SwimIdle | — | 1.051 | — | 0.951 | Spine8 35%, Spine6 23% |
| SwimRest | — | 1.047 | — | 0.936 | Spine1 63%, Neck 32% |

## DRG_magma_Unstaged

| Clip | giãn rig v4 (U1-CHAR.6) | giãn rig v5 (U1-CHAR.7) | nén rig v4 (U1-CHAR.6) | nén rig v5 (U1-CHAR.7) | vùng tệ nhất (rig v5 (U1-CHAR.7)) |
|---|---|---|---|---|---|
| Idle | — | 1.143 | — | 0.876 | Tail1 50%, Pelvis 49% |
| Eat | — | 1.529 | — | 0.568 | Neck 56%, HindFoot_L 11% |
| Sleep | — | 1.483 | — | 0.634 | Neck 72%, Head 13% |
| Happy | — | 1.423 | — | 0.676 | Pelvis 48%, Tail1 42% |
| Walk | — | 1.317 | — | 0.675 | Pelvis 36%, HindFoot_R 21% |
| Run | — | 1.593 | — | 0.494 | Pelvis 34%, HindFoot_L 24% |
| Burrow | — | 1.423 | — | 0.679 | Pelvis 54%, Spine 21% |
| Rest | — | 1.191 | — | 0.659 | Pelvis 37%, HindLower_L 24% |
| Wake | — | 1.344 | — | 0.628 | Neck 36%, HindFoot_L 16% |
| LookAround | — | 1.279 | — | 0.765 | Neck 62%, Head 18% |
| Curious | — | 1.292 | — | 0.776 | Pelvis 45%, Tail1 20% |
| Drink | — | 1.584 | — | 0.530 | Neck 58%, HindFoot_R 12% |
| Stretch | — | 1.230 | — | 0.793 | Pelvis 40%, Spine 25% |
| Bask | — | 1.187 | — | 0.660 | Pelvis 37%, HindLower_L 25% |
| Greet | — | 1.155 | — | 0.854 | Pelvis 43%, Tail1 41% |
| DigPrep | — | 1.161 | — | 0.848 | Spine 29%, ForeLower_L 16% |
| BurrowEnter | — | 1.323 | — | 0.711 | Spine 33%, Neck 19% |
| BurrowEmerge | — | 1.259 | — | 0.737 | Neck 30%, Spine 29% |

## DRG_sparkwing_Unstaged

| Clip | giãn rig v4 (U1-CHAR.6) | giãn rig v5 (U1-CHAR.7) | nén rig v4 (U1-CHAR.6) | nén rig v5 (U1-CHAR.7) | vùng tệ nhất (rig v5 (U1-CHAR.7)) |
|---|---|---|---|---|---|
| Idle | — | 1.137 | — | 0.884 | Tail1 64%, Pelvis 30% |
| Eat | — | 1.508 | — | 0.563 | Neck 40%, Spine 30% |
| Sleep | — | 1.426 | — | 0.637 | Neck 48%, Spine 18% |
| Happy | — | 1.393 | — | 0.689 | Tail1 57%, Pelvis 30% |
| Walk | — | 1.280 | — | 0.697 | Pelvis 30%, HindFoot_L 20% |
| Run | — | 1.504 | — | 0.531 | Pelvis 29%, HindFoot_L 26% |
| Fly | — | 1.326 | — | 0.777 | Spine 29%, Pelvis 24% |
| Rest | — | 1.244 | — | 0.659 | Pelvis 34%, WingUpper_R 16% |
| Wake | — | 1.312 | — | 0.635 | Neck 24%, HindFoot_R 12% |
| LookAround | — | 1.282 | — | 0.776 | Neck 37%, Spine 31% |
| Curious | — | 1.335 | — | 0.782 | Tail1 36%, Pelvis 24% |
| Drink | — | 1.579 | — | 0.530 | Neck 38%, Spine 36% |
| Stretch | — | 1.361 | — | 0.770 | Spine 83%, Pelvis 6% |
| Bask | — | 1.173 | — | 0.659 | Pelvis 33%, HindLower_L 22% |
| Greet | — | 1.150 | — | 0.853 | Tail1 50%, Pelvis 27% |
| TakeOff | — | 1.328 | — | 0.655 | Pelvis 20%, HindFoot_R 17% |
| Glide | — | 1.208 | — | 0.824 | Pelvis 70%, Spine 14% |
| Land | — | 1.489 | — | 0.569 | Spine 40%, HindFoot_R 11% |

## DRG_sprout_Unstaged

| Clip | giãn rig v4 (U1-CHAR.6) | giãn rig v5 (U1-CHAR.7) | nén rig v4 (U1-CHAR.6) | nén rig v5 (U1-CHAR.7) | vùng tệ nhất (rig v5 (U1-CHAR.7)) |
|---|---|---|---|---|---|
| Idle | 1.154 | 1.154 | 0.871 | 0.871 | Tail1 52%, Pelvis 46% |
| Eat | 1.537 | 1.537 | 0.529 | 0.529 | Neck 35%, Head 26% |
| Sleep | 1.414 | 1.414 | 0.629 | 0.629 | Neck 53%, Head 31% |
| Happy | 1.472 | 1.472 | 0.670 | 0.670 | Pelvis 46%, Tail1 42% |
| Walk | 1.297 | 1.297 | 0.689 | 0.689 | Pelvis 43%, HindFoot_R 19% |
| Run | 1.560 | 1.560 | 0.516 | 0.516 | Pelvis 41%, HindFoot_L 23% |
| Burrow | 1.465 | 1.465 | 0.685 | 0.685 | Pelvis 50%, HindFoot_L 14% |
| Rest | 1.182 | 1.182 | 0.664 | 0.664 | Pelvis 39%, HindLower_R 24% |
| Wake | 1.312 | 1.312 | 0.639 | 0.639 | Neck 25%, Head 15% |
| LookAround | 1.286 | 1.286 | 0.765 | 0.765 | Neck 38%, Head 36% |
| Curious | 1.437 | 1.437 | 0.772 | 0.772 | Pelvis 43%, Tail1 21% |
| Drink | 1.591 | 1.591 | 0.500 | 0.500 | Neck 36%, Head 26% |
| Stretch | 1.244 | 1.244 | 0.794 | 0.794 | Pelvis 43%, Spine 29% |
| Bask | 1.182 | 1.182 | 0.664 | 0.664 | Pelvis 37%, HindLower_R 24% |
| Greet | — | 1.172 | — | 0.857 | Tail1 46%, Pelvis 42% |
| DigPrep | — | 1.149 | — | 0.854 | Spine 18%, ForeLower_L 13% |
| BurrowEnter | — | 1.305 | — | 0.721 | Spine 22%, Neck 18% |
| BurrowEmerge | — | 1.249 | — | 0.742 | Spine 22%, Neck 19% |

## DRG_steamdrake_Unstaged

| Clip | giãn rig v4 (U1-CHAR.6) | giãn rig v5 (U1-CHAR.7) | nén rig v4 (U1-CHAR.6) | nén rig v5 (U1-CHAR.7) | vùng tệ nhất (rig v5 (U1-CHAR.7)) |
|---|---|---|---|---|---|
| Idle | 1.106 | 1.106 | 0.903 | 0.903 | Spine8 30%, Tail1 24% |
| Eat | 1.196 | 1.196 | 0.798 | 0.798 | Spine1 45%, Neck 43% |
| Sleep | 1.467 | 1.467 | 0.615 | 0.615 | Spine1 35%, Spine5 29% |
| Happy | 1.308 | 1.308 | 0.756 | 0.756 | Spine8 32%, Tail1 27% |
| Walk | 1.312 | 1.312 | 0.740 | 0.740 | HindFoot_L 19%, Spine1 16% |
| Run | 1.415 | 1.415 | 0.620 | 0.620 | Spine1 16%, Spine5 15% |
| Swim | 1.439 | 1.438 | 0.804 | 0.805 | Spine5 39%, Spine1 31% |
| Rest | 1.460 | 1.460 | 0.589 | 0.589 | Spine1 41%, Spine5 27% |
| Wake | 1.315 | 1.315 | 0.677 | 0.677 | Spine1 37%, Spine5 26% |
| LookAround | 1.266 | 1.266 | 0.781 | 0.781 | Spine1 54%, Neck 30% |
| Curious | 1.135 | 1.135 | 0.848 | 0.848 | Spine8 20%, Tail1 20% |
| Drink | 1.234 | 1.234 | 0.757 | 0.757 | Spine1 47%, Neck 32% |
| Stretch | 1.178 | 1.178 | 0.839 | 0.839 | Spine1 52%, Spine5 15% |
| Bask | 1.451 | 1.451 | 0.614 | 0.614 | Spine1 43%, Spine5 29% |
| Greet | — | 1.107 | — | 0.904 | Spine1 29%, Spine8 26% |
| SwimIdle | — | 1.135 | — | 0.927 | Spine5 37%, Spine1 37% |
| SwimRest | — | 1.077 | — | 0.939 | Spine1 61%, Spine5 19% |
| SwimSurface | — | 1.295 | — | 0.852 | Spine5 38%, Spine1 36% |
| WaterEnter | — | 1.257 | — | 0.852 | Spine1 38%, Spine5 37% |
| WaterExit | — | 1.252 | — | 0.855 | Spine5 38%, Spine1 34% |

## DRG_zephyr_Unstaged

| Clip | giãn rig v4 (U1-CHAR.6) | giãn rig v5 (U1-CHAR.7) | nén rig v4 (U1-CHAR.6) | nén rig v5 (U1-CHAR.7) | vùng tệ nhất (rig v5 (U1-CHAR.7)) |
|---|---|---|---|---|---|
| Idle | 1.137 | 1.137 | 0.884 | 0.884 | Tail1 64%, Pelvis 30% |
| Eat | 1.508 | 1.508 | 0.563 | 0.563 | Neck 40%, Spine 30% |
| Sleep | 1.426 | 1.426 | 0.637 | 0.637 | Neck 48%, Spine 18% |
| Happy | 1.393 | 1.393 | 0.689 | 0.689 | Tail1 57%, Pelvis 30% |
| Walk | 1.280 | 1.280 | 0.697 | 0.697 | Pelvis 30%, HindFoot_L 20% |
| Run | 1.504 | 1.504 | 0.531 | 0.531 | Pelvis 29%, HindFoot_L 26% |
| Fly | 1.326 | 1.326 | 0.777 | 0.777 | Spine 29%, Pelvis 24% |
| Rest | 1.244 | 1.244 | 0.659 | 0.659 | Pelvis 34%, WingUpper_R 16% |
| Wake | 1.312 | 1.312 | 0.635 | 0.635 | Neck 24%, HindFoot_R 12% |
| LookAround | 1.282 | 1.282 | 0.776 | 0.776 | Neck 37%, Spine 31% |
| Curious | 1.335 | 1.335 | 0.782 | 0.782 | Tail1 36%, Pelvis 24% |
| Drink | 1.579 | 1.579 | 0.530 | 0.530 | Neck 38%, Spine 36% |
| Stretch | 1.361 | 1.361 | 0.770 | 0.770 | Spine 83%, Pelvis 6% |
| Bask | 1.173 | 1.173 | 0.659 | 0.659 | Pelvis 33%, HindLower_L 22% |
| TakeOff | 1.428 | 1.328 | 0.545 | 0.655 | Pelvis 20%, HindFoot_R 17% |
| Glide | 1.208 | 1.208 | 0.824 | 0.824 | Pelvis 70%, Spine 14% |
| Land | 1.561 | 1.489 | 0.543 | 0.569 | Spine 40%, HindFoot_R 11% |
| Greet | — | 1.150 | — | 0.853 | Tail1 50%, Pelvis 27% |
| Perch | — | 1.385 | — | 0.720 | WingUpper_R 24%, Pelvis 22% |
