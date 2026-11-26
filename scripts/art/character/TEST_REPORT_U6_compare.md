# Animation QA: rig v3 (U1-CHAR.5) → rig v4 (U1-CHAR.6)

Cùng công cụ `anim_qa.py`, cùng ngưỡng trong `character-budgets.json` (`animationQa`). Số **đậm** là vượt ngưỡng (giãn p99.9 > maxStretchP999, nén p0.1 < minCompressP001).

| Model | rig v3 (U1-CHAR.5) | rig v4 (U1-CHAR.6) | Cảnh báo rig v3 (U1-CHAR.5) | Cảnh báo rig v4 (U1-CHAR.6) |
|---|---|---|---|---|
| DRG_droplet_Baby | PASS | PASS | 0 | 0 |
| DRG_emberling_Baby | PASS | PASS | 0 | 0 |
| DRG_frostfin_Juvenile | PASS | PASS | 0 | 0 |
| DRG_leviathan_Adult | PASS | PASS | 0 | 0 |
| DRG_sprout_Baby | PASS | PASS | 0 | 0 |
| DRG_steamdrake_Juvenile | PASS | PASS | 0 | 0 |
| DRG_zephyr_Unstaged | PASS | PASS | 0 | 0 |

## DRG_droplet_Baby

| Clip | giãn rig v3 (U1-CHAR.5) | giãn rig v4 (U1-CHAR.6) | nén rig v3 (U1-CHAR.5) | nén rig v4 (U1-CHAR.6) | vùng tệ nhất (rig v4 (U1-CHAR.6)) |
|---|---|---|---|---|---|
| Idle | 1.141 | 1.141 | 0.880 | 0.880 | Pelvis 61%, Tail1 24% |
| Eat | 1.509 | 1.509 | 0.519 | 0.519 | Neck 53%, Head 22% |
| Sleep | 1.581 | 1.581 | 0.553 | 0.553 | Neck 42%, Head 27% |
| Happy | 1.440 | 1.440 | 0.697 | 0.697 | Pelvis 54%, Tail1 20% |
| Walk | 1.291 | 1.291 | 0.688 | 0.688 | Pelvis 38%, HindFoot_L 21% |
| Run | 1.534 | 1.534 | 0.513 | 0.513 | Pelvis 34%, HindFoot_L 28% |
| Swim | 1.494 | 1.494 | 0.712 | 0.712 | Pelvis 56%, Tail1 21% |
| Rest | — | 1.194 | — | 0.664 | Pelvis 35%, HindLower_L 25% |
| Wake | — | 1.383 | — | 0.594 | Neck 28%, Head 15% |
| LookAround | — | 1.445 | — | 0.692 | Neck 28%, Head 24% |
| Curious | — | 1.310 | — | 0.767 | Pelvis 34%, EarFin_R 10% |
| Drink | — | 1.559 | — | 0.489 | Neck 55%, Head 21% |
| Stretch | — | 1.220 | — | 0.795 | Pelvis 38%, Spine 27% |
| Bask | — | 1.188 | — | 0.664 | Pelvis 32%, HindLower_L 27% |

## DRG_emberling_Baby

| Clip | giãn rig v3 (U1-CHAR.5) | giãn rig v4 (U1-CHAR.6) | nén rig v3 (U1-CHAR.5) | nén rig v4 (U1-CHAR.6) | vùng tệ nhất (rig v4 (U1-CHAR.6)) |
|---|---|---|---|---|---|
| Idle | 1.155 | 1.155 | 0.863 | 0.863 | Tail1 61%, Pelvis 38% |
| Eat | 1.491 | 1.491 | 0.588 | 0.588 | Neck 54%, HindFoot_R 11% |
| Sleep | 1.450 | 1.450 | 0.644 | 0.644 | Neck 64%, Pelvis 17% |
| Happy | 1.463 | 1.463 | 0.658 | 0.658 | Tail1 56%, Pelvis 37% |
| Walk | 1.302 | 1.302 | 0.686 | 0.686 | Pelvis 41%, HindFoot_R 17% |
| Run | 1.542 | 1.542 | 0.532 | 0.532 | Pelvis 39%, HindFoot_L 21% |
| Rest | — | 1.178 | — | 0.661 | Pelvis 40%, HindLower_L 25% |
| Wake | — | 1.321 | — | 0.641 | Neck 31%, HindFoot_R 15% |
| LookAround | — | 1.266 | — | 0.771 | Neck 59%, Head 18% |
| Curious | — | 1.402 | — | 0.780 | Tail1 38%, Pelvis 32% |
| Drink | — | 1.536 | — | 0.548 | Neck 55%, HindFoot_R 12% |
| Stretch | — | 1.198 | — | 0.804 | Pelvis 48%, Spine 27% |
| Bask | — | 1.177 | — | 0.661 | Pelvis 38%, HindLower_L 25% |

## DRG_frostfin_Juvenile

| Clip | giãn rig v3 (U1-CHAR.5) | giãn rig v4 (U1-CHAR.6) | nén rig v3 (U1-CHAR.5) | nén rig v4 (U1-CHAR.6) | vùng tệ nhất (rig v4 (U1-CHAR.6)) |
|---|---|---|---|---|---|
| Idle | 1.176 | 1.176 | 0.849 | 0.849 | Spine4 48%, Tail1 41% |
| Eat | 1.207 | 1.207 | 0.620 | 0.620 | Spine1 59%, Head 30% |
| Sleep | 1.315 | 1.315 | 0.605 | 0.605 | Spine1 73%, Head 20% |
| Happy | 1.442 | 1.442 | 0.719 | 0.719 | Tail1 49%, Spine4 48% |
| Swim | 1.411 | 1.411 | 0.712 | 0.712 | Fin_L 21%, Spine4 21% |
| Rest | — | 1.245 | — | 0.763 | Spine2 42%, Spine1 24% |
| Wake | — | 1.199 | — | 0.682 | Spine1 58%, Head 23% |
| LookAround | — | 1.337 | — | 0.751 | Spine1 72%, Head 19% |
| Curious | — | 1.233 | — | 0.816 | Spine4 46%, Tail1 41% |

## DRG_leviathan_Adult

| Clip | giãn rig v3 (U1-CHAR.5) | giãn rig v4 (U1-CHAR.6) | nén rig v3 (U1-CHAR.5) | nén rig v4 (U1-CHAR.6) | vùng tệ nhất (rig v4 (U1-CHAR.6)) |
|---|---|---|---|---|---|
| Idle | 1.169 | 1.169 | 0.852 | 0.852 | Spine8 45%, Tail1 26% |
| Eat | 1.201 | 1.201 | 0.705 | 0.705 | Spine1 43%, Neck 43% |
| Sleep | 1.438 | 1.438 | 0.533 | 0.533 | Tail3 39%, Tail2 21% |
| Happy | 1.463 | 1.463 | 0.710 | 0.710 | Spine8 45%, Tail3 27% |
| Swim | 1.152 | 1.152 | 0.864 | 0.864 | Spine8 34%, Spine6 24% |
| Rest | — | 1.146 | — | 0.772 | Spine1 24%, Tail3 22% |
| Wake | — | 1.272 | — | 0.639 | Tail3 32%, Spine8 20% |
| LookAround | — | 1.250 | — | 0.779 | Spine1 45%, Neck 43% |
| Curious | — | 1.167 | — | 0.867 | Spine8 51%, Tail1 22% |

## DRG_sprout_Baby

| Clip | giãn rig v3 (U1-CHAR.5) | giãn rig v4 (U1-CHAR.6) | nén rig v3 (U1-CHAR.5) | nén rig v4 (U1-CHAR.6) | vùng tệ nhất (rig v4 (U1-CHAR.6)) |
|---|---|---|---|---|---|
| Idle | 1.154 | 1.154 | 0.871 | 0.871 | Tail1 52%, Pelvis 46% |
| Eat | 1.537 | 1.537 | 0.529 | 0.529 | Neck 35%, Head 26% |
| Sleep | 1.414 | 1.414 | 0.629 | 0.629 | Neck 53%, Head 31% |
| Happy | 1.472 | 1.472 | 0.670 | 0.670 | Pelvis 46%, Tail1 42% |
| Walk | 1.297 | 1.297 | 0.689 | 0.689 | Pelvis 43%, HindFoot_R 19% |
| Run | 1.560 | 1.560 | 0.516 | 0.516 | Pelvis 41%, HindFoot_L 23% |
| Burrow | 1.465 | 1.465 | 0.685 | 0.685 | Pelvis 50%, HindFoot_L 14% |
| Rest | — | 1.182 | — | 0.664 | Pelvis 39%, HindLower_R 24% |
| Wake | — | 1.312 | — | 0.639 | Neck 25%, Head 15% |
| LookAround | — | 1.286 | — | 0.765 | Neck 38%, Head 36% |
| Curious | — | 1.437 | — | 0.772 | Pelvis 43%, Tail1 21% |
| Drink | — | 1.591 | — | 0.500 | Neck 36%, Head 26% |
| Stretch | — | 1.244 | — | 0.794 | Pelvis 43%, Spine 29% |
| Bask | — | 1.182 | — | 0.664 | Pelvis 37%, HindLower_R 24% |

## DRG_steamdrake_Juvenile

| Clip | giãn rig v3 (U1-CHAR.5) | giãn rig v4 (U1-CHAR.6) | nén rig v3 (U1-CHAR.5) | nén rig v4 (U1-CHAR.6) | vùng tệ nhất (rig v4 (U1-CHAR.6)) |
|---|---|---|---|---|---|
| Idle | 1.106 | 1.106 | 0.903 | 0.903 | Spine8 30%, Tail1 24% |
| Eat | 1.196 | 1.196 | 0.798 | 0.798 | Spine1 45%, Neck 43% |
| Sleep | 1.503 | 1.467 | 0.539 | 0.615 | Spine1 35%, Spine5 29% |
| Happy | 1.308 | 1.308 | 0.756 | 0.756 | Spine8 32%, Tail1 27% |
| Walk | 1.312 | 1.312 | 0.740 | 0.740 | HindFoot_L 19%, Spine1 16% |
| Run | 1.415 | 1.415 | 0.620 | 0.620 | Spine1 16%, Spine5 15% |
| Swim | 1.438 | 1.439 | 0.805 | 0.804 | Spine5 38%, Spine1 32% |
| Rest | — | 1.460 | — | 0.589 | Spine1 41%, Spine5 27% |
| Wake | — | 1.315 | — | 0.677 | Spine1 37%, Spine5 26% |
| LookAround | — | 1.266 | — | 0.781 | Spine1 54%, Neck 30% |
| Curious | — | 1.135 | — | 0.848 | Spine8 20%, Tail1 20% |
| Drink | — | 1.234 | — | 0.757 | Spine1 47%, Neck 32% |
| Stretch | — | 1.178 | — | 0.839 | Spine1 52%, Spine5 15% |
| Bask | — | 1.451 | — | 0.614 | Spine1 43%, Spine5 29% |

## DRG_zephyr_Unstaged

| Clip | giãn rig v3 (U1-CHAR.5) | giãn rig v4 (U1-CHAR.6) | nén rig v3 (U1-CHAR.5) | nén rig v4 (U1-CHAR.6) | vùng tệ nhất (rig v4 (U1-CHAR.6)) |
|---|---|---|---|---|---|
| Idle | 1.137 | 1.137 | 0.884 | 0.884 | Tail1 64%, Pelvis 30% |
| Eat | 1.508 | 1.508 | 0.563 | 0.563 | Neck 40%, Spine 30% |
| Sleep | 1.426 | 1.426 | 0.637 | 0.637 | Neck 48%, Spine 18% |
| Happy | 1.393 | 1.393 | 0.689 | 0.689 | Tail1 57%, Pelvis 30% |
| Walk | 1.280 | 1.280 | 0.697 | 0.697 | Pelvis 30%, HindFoot_L 20% |
| Run | 1.504 | 1.504 | 0.531 | 0.531 | Pelvis 29%, HindFoot_L 26% |
| Fly | 1.326 | 1.326 | 0.777 | 0.777 | Spine 29%, Pelvis 24% |
| TakeOff | 1.428 | 1.428 | 0.545 | 0.545 | Pelvis 25%, HindFoot_R 18% |
| Glide | 1.208 | 1.208 | 0.824 | 0.824 | Pelvis 70%, Spine 14% |
| Land | 1.561 | 1.561 | 0.543 | 0.543 | Spine 42%, HindFoot_R 11% |
| Rest | — | 1.244 | — | 0.659 | Pelvis 34%, WingUpper_R 16% |
| Wake | — | 1.312 | — | 0.635 | Neck 24%, HindFoot_R 12% |
| LookAround | — | 1.282 | — | 0.776 | Neck 37%, Spine 31% |
| Curious | — | 1.335 | — | 0.782 | Tail1 36%, Pelvis 24% |
| Drink | — | 1.579 | — | 0.530 | Neck 38%, Spine 36% |
| Stretch | — | 1.361 | — | 0.770 | Spine 83%, Pelvis 6% |
| Bask | — | 1.173 | — | 0.659 | Pelvis 33%, HindLower_L 22% |
