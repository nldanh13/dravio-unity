# Animation QA: rig v2 (U1-CHAR.4) → rig v3 (U1-CHAR.5)

Cùng công cụ `anim_qa.py`, cùng ngưỡng trong `character-budgets.json` (`animationQa`). Số **đậm** là vượt ngưỡng (giãn p99.9 > maxStretchP999, nén p0.1 < minCompressP001).

| Model | rig v2 (U1-CHAR.4) | rig v3 (U1-CHAR.5) | Cảnh báo rig v2 (U1-CHAR.4) | Cảnh báo rig v3 (U1-CHAR.5) |
|---|---|---|---|---|
| DRG_droplet_Baby | PASS_WITH_WARNINGS | PASS | 2 | 0 |
| DRG_emberling_Baby | PASS | PASS | 0 | 0 |
| DRG_frostfin_Juvenile | PASS | PASS | 0 | 0 |
| DRG_leviathan_Adult | PASS | PASS | 0 | 0 |
| DRG_sprout_Baby | PASS_WITH_WARNINGS | PASS | 2 | 0 |
| DRG_steamdrake_Juvenile | PASS_WITH_WARNINGS | PASS | 1 | 0 |
| DRG_zephyr_Unstaged | PASS_WITH_WARNINGS | PASS | 1 | 0 |

## DRG_droplet_Baby

| Clip | giãn rig v2 (U1-CHAR.4) | giãn rig v3 (U1-CHAR.5) | nén rig v2 (U1-CHAR.4) | nén rig v3 (U1-CHAR.5) | vùng tệ nhất (rig v3 (U1-CHAR.5)) |
|---|---|---|---|---|---|
| Idle | 1.141 | 1.141 | 0.880 | 0.880 | Pelvis 61%, Tail1 24% |
| Eat | 1.509 | 1.509 | 0.519 | 0.519 | Neck 53%, Head 22% |
| Sleep | 1.581 | 1.581 | **0.396** | 0.553 | Neck 42%, Head 27% |
| Happy | 1.440 | 1.440 | 0.697 | 0.697 | Pelvis 54%, Tail1 20% |
| Walk | 1.307 | 1.291 | 0.663 | 0.688 | Pelvis 38%, HindFoot_L 21% |
| Run | 1.580 | 1.534 | 0.495 | 0.513 | Pelvis 34%, HindFoot_L 28% |
| Swim | **1.612** | 1.494 | 0.682 | 0.712 | Pelvis 56%, Tail1 21% |

Cảnh báo rig v2 (U1-CHAR.4):
- Sleep: deformation: edge compression over limit (0.396 / ngưỡng 0.45)
- Swim: deformation: edge stretch over limit (1.612 / ngưỡng 1.6)

## DRG_emberling_Baby

| Clip | giãn rig v2 (U1-CHAR.4) | giãn rig v3 (U1-CHAR.5) | nén rig v2 (U1-CHAR.4) | nén rig v3 (U1-CHAR.5) | vùng tệ nhất (rig v3 (U1-CHAR.5)) |
|---|---|---|---|---|---|
| Idle | 1.155 | 1.155 | 0.863 | 0.863 | Tail1 61%, Pelvis 38% |
| Eat | 1.491 | 1.491 | 0.588 | 0.588 | Neck 54%, HindFoot_R 11% |
| Sleep | 1.451 | 1.450 | 0.468 | 0.644 | Neck 64%, Pelvis 17% |
| Happy | 1.463 | 1.463 | 0.658 | 0.658 | Tail1 56%, Pelvis 37% |
| Walk | 1.318 | 1.302 | 0.665 | 0.686 | Pelvis 41%, HindFoot_R 17% |
| Run | 1.575 | 1.542 | 0.519 | 0.532 | Pelvis 39%, HindFoot_L 21% |

## DRG_frostfin_Juvenile

| Clip | giãn rig v2 (U1-CHAR.4) | giãn rig v3 (U1-CHAR.5) | nén rig v2 (U1-CHAR.4) | nén rig v3 (U1-CHAR.5) | vùng tệ nhất (rig v3 (U1-CHAR.5)) |
|---|---|---|---|---|---|
| Idle | 1.176 | 1.176 | 0.849 | 0.849 | Spine4 48%, Tail1 41% |
| Eat | 1.207 | 1.207 | 0.620 | 0.620 | Spine1 59%, Head 30% |
| Sleep | 1.315 | 1.315 | 0.605 | 0.605 | Spine1 73%, Head 20% |
| Happy | 1.442 | 1.442 | 0.719 | 0.719 | Tail1 49%, Spine4 48% |
| Swim | 1.411 | 1.411 | 0.712 | 0.712 | Fin_L 21%, Spine4 21% |

## DRG_leviathan_Adult

| Clip | giãn rig v2 (U1-CHAR.4) | giãn rig v3 (U1-CHAR.5) | nén rig v2 (U1-CHAR.4) | nén rig v3 (U1-CHAR.5) | vùng tệ nhất (rig v3 (U1-CHAR.5)) |
|---|---|---|---|---|---|
| Idle | 1.169 | 1.169 | 0.852 | 0.852 | Spine8 45%, Tail1 26% |
| Eat | 1.201 | 1.201 | 0.705 | 0.705 | Spine1 43%, Neck 43% |
| Sleep | 1.438 | 1.438 | 0.533 | 0.533 | Tail3 39%, Tail2 21% |
| Happy | 1.463 | 1.463 | 0.710 | 0.710 | Spine8 45%, Tail3 27% |
| Swim | 1.152 | 1.152 | 0.864 | 0.864 | Spine8 34%, Spine6 24% |

## DRG_sprout_Baby

| Clip | giãn rig v2 (U1-CHAR.4) | giãn rig v3 (U1-CHAR.5) | nén rig v2 (U1-CHAR.4) | nén rig v3 (U1-CHAR.5) | vùng tệ nhất (rig v3 (U1-CHAR.5)) |
|---|---|---|---|---|---|
| Idle | 1.154 | 1.154 | 0.871 | 0.871 | Tail1 52%, Pelvis 46% |
| Eat | 1.537 | 1.537 | 0.529 | 0.529 | Neck 35%, Head 26% |
| Sleep | 1.423 | 1.414 | **0.446** | 0.629 | Neck 53%, Head 31% |
| Happy | 1.472 | 1.472 | 0.670 | 0.670 | Pelvis 46%, Tail1 42% |
| Walk | 1.313 | 1.297 | 0.669 | 0.689 | Pelvis 43%, HindFoot_R 19% |
| Run | 1.593 | 1.560 | 0.492 | 0.516 | Pelvis 41%, HindFoot_L 23% |
| Burrow | 1.538 | 1.465 | **0.433** | 0.685 | Pelvis 50%, HindFoot_L 14% |

Cảnh báo rig v2 (U1-CHAR.4):
- Sleep: deformation: edge compression over limit (0.446 / ngưỡng 0.45)
- Burrow: deformation: edge compression over limit (0.433 / ngưỡng 0.45)

## DRG_steamdrake_Juvenile

| Clip | giãn rig v2 (U1-CHAR.4) | giãn rig v3 (U1-CHAR.5) | nén rig v2 (U1-CHAR.4) | nén rig v3 (U1-CHAR.5) | vùng tệ nhất (rig v3 (U1-CHAR.5)) |
|---|---|---|---|---|---|
| Idle | 1.126 | 1.106 | 0.895 | 0.903 | Spine8 30%, Tail1 24% |
| Eat | 1.189 | 1.196 | 0.798 | 0.798 | Spine1 45%, Neck 43% |
| Sleep | 1.487 | 1.503 | 0.492 | 0.539 | HindFoot_L 25%, Spine1 23% |
| Happy | 1.383 | 1.308 | 0.757 | 0.756 | Spine8 32%, Tail1 27% |
| Walk | 1.360 | 1.312 | 0.726 | 0.740 | HindFoot_L 19%, Spine1 16% |
| Run | 1.489 | 1.415 | 0.597 | 0.620 | Spine1 16%, Spine5 15% |
| Swim | 1.451 | 1.438 | 0.799 | 0.805 | Spine5 39%, Spine1 31% |

Cảnh báo rig v2 (U1-CHAR.4):
- model: bones: bone orientation / mirror issue ({'zeroLength': [], 'mirrorErrorRel': 0.08205, 'chainDirection': []} / ngưỡng {'mirrorErrorRel': 0.01})

## DRG_zephyr_Unstaged

| Clip | giãn rig v2 (U1-CHAR.4) | giãn rig v3 (U1-CHAR.5) | nén rig v2 (U1-CHAR.4) | nén rig v3 (U1-CHAR.5) | vùng tệ nhất (rig v3 (U1-CHAR.5)) |
|---|---|---|---|---|---|
| Idle | 1.137 | 1.137 | 0.884 | 0.884 | Tail1 64%, Pelvis 30% |
| Eat | 1.508 | 1.508 | 0.563 | 0.563 | Neck 40%, Spine 30% |
| Sleep | 1.431 | 1.426 | **0.447** | 0.637 | Neck 48%, Spine 18% |
| Happy | 1.393 | 1.393 | 0.689 | 0.689 | Tail1 57%, Pelvis 30% |
| Walk | 1.296 | 1.280 | 0.677 | 0.697 | Pelvis 30%, HindFoot_L 20% |
| Run | 1.549 | 1.504 | 0.513 | 0.531 | Pelvis 29%, HindFoot_L 26% |
| Fly | 1.326 | 1.326 | 0.777 | 0.777 | Spine 29%, Pelvis 24% |
| TakeOff | 1.428 | 1.428 | 0.545 | 0.545 | Pelvis 25%, HindFoot_R 18% |
| Glide | 1.208 | 1.208 | 0.824 | 0.824 | Pelvis 70%, Spine 14% |
| Land | 1.561 | 1.561 | 0.543 | 0.543 | Spine 42%, HindFoot_R 11% |

Cảnh báo rig v2 (U1-CHAR.4):
- Sleep: deformation: edge compression over limit (0.447 / ngưỡng 0.45)
