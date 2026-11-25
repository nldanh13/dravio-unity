# Animation QA: rig v1 → rig v2

Cùng công cụ `anim_qa.py`, cùng ngưỡng trong `character-budgets.json` (`animationQa`). Số **đậm** là vượt ngưỡng (giãn p99.9 > maxStretchP999, nén p0.1 < minCompressP001).

| Model | rig v1 | rig v2 | Cảnh báo rig v1 | Cảnh báo rig v2 |
|---|---|---|---|---|
| DRG_droplet_Baby | PASS_WITH_WARNINGS | PASS_WITH_WARNINGS | 5 | 2 |
| DRG_emberling_Baby | PASS_WITH_WARNINGS | PASS | 4 | 0 |
| DRG_frostfin_Juvenile | PASS_WITH_WARNINGS | PASS | 4 | 0 |
| DRG_leviathan_Adult | PASS_WITH_WARNINGS | PASS | 5 | 0 |
| DRG_sprout_Baby | PASS_WITH_WARNINGS | PASS_WITH_WARNINGS | 6 | 2 |
| DRG_steamdrake_Juvenile | PASS_WITH_WARNINGS | PASS_WITH_WARNINGS | 7 | 1 |
| DRG_zephyr_Unstaged | PASS_WITH_WARNINGS | PASS_WITH_WARNINGS | 6 | 1 |

## DRG_droplet_Baby

| Clip | giãn rig v1 | giãn rig v2 | nén rig v1 | nén rig v2 | vùng tệ nhất (rig v2) |
|---|---|---|---|---|---|
| Idle | 1.066 | 1.141 | 0.936 | 0.880 | Pelvis 61%, Tail1 24% |
| Eat | 1.342 | 1.509 | 0.837 | 0.519 | Neck 53%, Head 22% |
| Sleep | 1.525 | 1.581 | 0.478 | **0.396** | Neck 32%, EarFin_L 19% |
| Happy | 1.171 | 1.440 | 0.843 | 0.697 | Pelvis 54%, Tail1 20% |
| Walk | 1.265 | 1.307 | 0.793 | 0.663 | Pelvis 34%, HindFoot_L 27% |
| Run | 1.467 | 1.580 | 0.695 | 0.495 | Pelvis 32%, HindFoot_R 27% |
| Swim | 1.370 | **1.612** | 0.734 | 0.682 | Pelvis 58%, Tail1 23% |

Cảnh báo rig v1:
- Idle: ground: mesh below the ground plane (0.0175 / ngưỡng 0.01)
- Eat: ground: mesh below the ground plane (0.0175 / ngưỡng 0.01)
- Happy: ground: mesh below the ground plane (0.0174 / ngưỡng 0.01)
- Walk: ground: mesh below the ground plane (0.0202 / ngưỡng 0.01)
- Run: ground: mesh below the ground plane (0.0101 / ngưỡng 0.01)

Cảnh báo rig v2:
- Sleep: deformation: edge compression over limit (0.396 / ngưỡng 0.45)
- Swim: deformation: edge stretch over limit (1.612 / ngưỡng 1.6)

## DRG_emberling_Baby

| Clip | giãn rig v1 | giãn rig v2 | nén rig v1 | nén rig v2 | vùng tệ nhất (rig v2) |
|---|---|---|---|---|---|
| Idle | 1.067 | 1.155 | 0.934 | 0.863 | Tail1 61%, Pelvis 38% |
| Eat | 1.350 | 1.491 | 0.831 | 0.588 | Neck 54%, HindFoot_R 11% |
| Sleep | 1.528 | 1.451 | 0.474 | 0.468 | Neck 40%, HindFoot_R 19% |
| Happy | 1.173 | 1.463 | 0.836 | 0.658 | Tail1 56%, Pelvis 37% |
| Walk | 1.257 | 1.318 | 0.801 | 0.665 | Pelvis 39%, HindFoot_L 20% |
| Run | 1.449 | 1.575 | 0.706 | 0.519 | Pelvis 37%, HindFoot_R 24% |

Cảnh báo rig v1:
- Idle: ground: mesh below the ground plane (0.0173 / ngưỡng 0.01)
- Eat: ground: mesh below the ground plane (0.0173 / ngưỡng 0.01)
- Happy: ground: mesh below the ground plane (0.0171 / ngưỡng 0.01)
- Walk: ground: mesh below the ground plane (0.0177 / ngưỡng 0.01)

## DRG_frostfin_Juvenile

| Clip | giãn rig v1 | giãn rig v2 | nén rig v1 | nén rig v2 | vùng tệ nhất (rig v2) |
|---|---|---|---|---|---|
| Idle | 1.117 | 1.176 | 0.887 | 0.849 | Spine4 48%, Tail1 41% |
| Eat | 1.308 | 1.207 | 0.733 | 0.620 | Spine1 59%, Head 30% |
| Sleep | 1.554 | 1.315 | 0.585 | 0.605 | Spine1 73%, Head 20% |
| Happy | 1.307 | 1.442 | 0.720 | 0.719 | Tail1 49%, Spine4 48% |
| Swim | 1.498 | 1.411 | 0.654 | 0.712 | Fin_L 21%, Spine4 21% |

Cảnh báo rig v1:
- Idle: ground: mesh below the ground plane (0.0654 / ngưỡng 0.01)
- Eat: ground: mesh below the ground plane (0.0726 / ngưỡng 0.01)
- Sleep: ground: mesh below the ground plane (0.0725 / ngưỡng 0.01)
- Happy: ground: mesh below the ground plane (0.0452 / ngưỡng 0.01)

## DRG_leviathan_Adult

| Clip | giãn rig v1 | giãn rig v2 | nén rig v1 | nén rig v2 | vùng tệ nhất (rig v2) |
|---|---|---|---|---|---|
| Idle | 1.087 | 1.169 | 0.918 | 0.852 | Spine8 45%, Tail1 26% |
| Eat | 1.232 | 1.201 | 0.825 | 0.705 | Spine1 43%, Neck 43% |
| Sleep | 1.356 | 1.438 | 0.706 | 0.533 | Tail3 39%, Tail2 21% |
| Happy | 1.211 | 1.463 | 0.803 | 0.710 | Spine8 45%, Tail3 27% |
| Swim | **1.852** | 1.152 | 0.590 | 0.864 | Spine8 34%, Spine6 24% |

Cảnh báo rig v1:
- Idle: ground: mesh below the ground plane (0.1779 / ngưỡng 0.01)
- Eat: ground: mesh below the ground plane (0.1779 / ngưỡng 0.01)
- Sleep: ground: mesh below the ground plane (0.1783 / ngưỡng 0.01)
- Happy: ground: mesh below the ground plane (0.1725 / ngưỡng 0.01)
- Swim: deformation: edge stretch over limit (1.852 / ngưỡng 1.6)

## DRG_sprout_Baby

| Clip | giãn rig v1 | giãn rig v2 | nén rig v1 | nén rig v2 | vùng tệ nhất (rig v2) |
|---|---|---|---|---|---|
| Idle | 1.067 | 1.154 | 0.936 | 0.871 | Tail1 52%, Pelvis 46% |
| Eat | 1.304 | 1.537 | 0.814 | 0.529 | Neck 35%, Head 26% |
| Sleep | 1.501 | 1.423 | 0.474 | **0.446** | Neck 32%, HindFoot_L 19% |
| Happy | 1.180 | 1.472 | 0.840 | 0.670 | Pelvis 46%, Tail1 42% |
| Walk | 1.370 | 1.313 | 0.731 | 0.669 | Pelvis 42%, HindFoot_L 18% |
| Run | **1.666** | 1.593 | 0.616 | 0.492 | Pelvis 41%, HindFoot_L 22% |
| Burrow | 1.465 | 1.538 | 0.668 | **0.433** | Pelvis 27%, HindFoot_L 24% |

Cảnh báo rig v1:
- Idle: ground: mesh below the ground plane (0.0153 / ngưỡng 0.01)
- Eat: ground: mesh below the ground plane (0.0153 / ngưỡng 0.01)
- Happy: ground: mesh below the ground plane (0.0152 / ngưỡng 0.01)
- Walk: ground: mesh below the ground plane (0.0148 / ngưỡng 0.01)
- Run: deformation: edge stretch over limit (1.666 / ngưỡng 1.6)
- Burrow: ground: mesh below the ground plane (0.019 / ngưỡng 0.01)

Cảnh báo rig v2:
- Sleep: deformation: edge compression over limit (0.446 / ngưỡng 0.45)
- Burrow: deformation: edge compression over limit (0.433 / ngưỡng 0.45)

## DRG_steamdrake_Juvenile

| Clip | giãn rig v1 | giãn rig v2 | nén rig v1 | nén rig v2 | vùng tệ nhất (rig v2) |
|---|---|---|---|---|---|
| Idle | 1.077 | 1.126 | 0.925 | 0.895 | Spine8 36%, Tail3 28% |
| Eat | 1.204 | 1.189 | 0.809 | 0.798 | Neck 50%, Spine1 36% |
| Sleep | 1.342 | 1.487 | 0.635 | 0.492 | HindFoot_L 24%, Spine1 23% |
| Happy | 1.205 | 1.383 | 0.807 | 0.757 | Spine8 37%, Tail3 30% |
| Walk | 1.135 | 1.360 | 0.887 | 0.726 | HindFoot_L 17%, Spine1 17% |
| Run | 1.240 | 1.489 | 0.813 | 0.597 | HindFoot_R 14%, Spine1 14% |
| Swim | 1.212 | 1.451 | 0.824 | 0.799 | Spine5 37%, Spine1 29% |

Cảnh báo rig v1:
- model: bones: bone orientation / mirror issue ({'zeroLength': [], 'mirrorErrorRel': 0.08006, 'chainDirection': []} / ngưỡng {'mirrorErrorRel': 0.01})
- Idle: ground: mesh below the ground plane (0.1247 / ngưỡng 0.01)
- Eat: ground: mesh below the ground plane (0.1247 / ngưỡng 0.01)
- Sleep: ground: mesh below the ground plane (0.1068 / ngưỡng 0.01)
- Happy: ground: mesh below the ground plane (0.1237 / ngưỡng 0.01)
- Walk: ground: mesh below the ground plane (0.1499 / ngưỡng 0.01)
- Run: ground: mesh below the ground plane (0.1424 / ngưỡng 0.01)

Cảnh báo rig v2:
- model: bones: bone orientation / mirror issue ({'zeroLength': [], 'mirrorErrorRel': 0.08205, 'chainDirection': []} / ngưỡng {'mirrorErrorRel': 0.01})

## DRG_zephyr_Unstaged

| Clip | giãn rig v1 | giãn rig v2 | nén rig v1 | nén rig v2 | vùng tệ nhất (rig v2) |
|---|---|---|---|---|---|
| Idle | 1.069 | 1.137 | 0.935 | 0.884 | Tail1 64%, Pelvis 30% |
| Eat | 1.523 | 1.508 | 0.844 | 0.563 | Neck 40%, Spine 30% |
| Sleep | **2.666** | 1.431 | 0.475 | **0.447** | Neck 32%, HindFoot_L 17% |
| Happy | 1.193 | 1.393 | 0.832 | 0.689 | Tail1 57%, Pelvis 30% |
| Walk | 1.311 | 1.296 | 0.785 | 0.677 | Pelvis 28%, HindFoot_L 23% |
| Run | 1.555 | 1.549 | 0.696 | 0.513 | Pelvis 28%, HindFoot_R 27% |
| Fly | **1.928** | 1.326 | 0.639 | 0.777 | Spine 29%, Pelvis 24% |
| TakeOff | — | 1.428 | — | 0.545 | Pelvis 25%, HindFoot_R 18% |
| Glide | — | 1.208 | — | 0.824 | Pelvis 70%, Spine 14% |
| Land | — | 1.561 | — | 0.543 | Spine 42%, HindFoot_R 11% |

Cảnh báo rig v1:
- Idle: ground: mesh below the ground plane (0.0172 / ngưỡng 0.01)
- Eat: ground: mesh below the ground plane (0.0172 / ngưỡng 0.01)
- Sleep: deformation: edge stretch over limit (2.666 / ngưỡng 1.6)
- Happy: ground: mesh below the ground plane (0.0171 / ngưỡng 0.01)
- Walk: ground: mesh below the ground plane (0.0197 / ngưỡng 0.01)
- Fly: deformation: edge stretch over limit (1.928 / ngưỡng 1.6)

Cảnh báo rig v2:
- Sleep: deformation: edge compression over limit (0.447 / ngưỡng 0.45)
