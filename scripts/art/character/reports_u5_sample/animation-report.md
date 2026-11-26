# Báo cáo animation

Bộ clip mỗi loài lấy từ khả năng sinh học (`dragon-archetypes.json`); loài không có khả năng thì không có clip, runtime không giả (Fly trên loài không bay → Idle + cảnh báo).

| Model | Bằng chứng | Clip theo sinh học | Clip đã tạo | Thiếu | Thừa | QA |
|---|---|---|---|---|---|---|
| DRG_droplet_Baby | SAMPLE | Idle, Eat, Sleep, Happy, Walk, Run, Swim | Idle, Eat, Sleep, Happy, Walk, Run, Swim | — | — | PASS |
| DRG_emberling_Baby | SAMPLE | Idle, Eat, Sleep, Happy, Walk, Run | Idle, Eat, Sleep, Happy, Walk, Run | — | — | PASS |
| DRG_frostfin_Juvenile | SAMPLE | Idle, Eat, Sleep, Happy, Swim | Idle, Eat, Sleep, Happy, Swim | — | — | PASS |
| DRG_leviathan_Adult | SAMPLE | Idle, Eat, Sleep, Happy, Swim | Idle, Eat, Sleep, Happy, Swim | — | — | PASS |
| DRG_magma_Baby | SAMPLE | Idle, Eat, Sleep, Happy, Walk, Run, Burrow | — (tĩnh) | Idle, Eat, Sleep, Happy, Walk, Run, Burrow | — | — |
| DRG_sprout_Baby | SAMPLE | Idle, Eat, Sleep, Happy, Walk, Run, Burrow | Idle, Eat, Sleep, Happy, Walk, Run, Burrow | — | — | PASS |
| DRG_steamdrake_Juvenile | SAMPLE | Idle, Eat, Sleep, Happy, Walk, Run, Swim | Idle, Eat, Sleep, Happy, Walk, Run, Swim | — | — | PASS |
| DRG_zephyr_Unstaged | SAMPLE | Idle, Eat, Sleep, Happy, Walk, Run, Fly | Idle, Eat, Sleep, Happy, Walk, Run, Fly, TakeOff, Glide, Land | — | — | PASS |

TakeOff/Glide/Land là clip chuyển tiếp của loài bay (archetype winged), có trong FBX và Animation Lab; state Animator hiện chỉ gồm Idle, Walk, Run, Swim, Fly, Burrow, Sleep + Eat, Happy (enum `DragonLocomotion`).

## DRG_droplet_Baby (SAMPLE)

| Clip | Frame | Loop | m/s | Giãn | Nén | Trượt | Xuyên | Kết quả |
|---|---|---|---|---|---|---|---|---|
| Idle | 120 | có | 0.000 | 1.141 | 0.880 | 0.0000 | 0.0001 | PASS |
| Eat | 60 | — | 0.000 | 1.509 | 0.519 | 0.0000 | 0.0000 | PASS |
| Sleep | 120 | có | 0.000 | 1.581 | 0.553 | 0.0000 | 0.0000 | PASS |
| Happy | 45 | — | 0.000 | 1.440 | 0.697 | 0.0000 | 0.0004 | PASS |
| Walk | 32 | có | 0.137 | 1.291 | 0.688 | 0.0000 | 0.0005 | PASS |
| Run | 20 | có | 0.529 | 1.534 | 0.513 | 0.0000 | 0.0000 | PASS |
| Swim | 48 | có | 0.000 | 1.494 | 0.712 | 0.0000 | — | PASS |

## DRG_emberling_Baby (SAMPLE)

| Clip | Frame | Loop | m/s | Giãn | Nén | Trượt | Xuyên | Kết quả |
|---|---|---|---|---|---|---|---|---|
| Idle | 120 | có | 0.000 | 1.155 | 0.863 | 0.0000 | 0.0001 | PASS |
| Eat | 60 | — | 0.000 | 1.491 | 0.588 | 0.0000 | 0.0000 | PASS |
| Sleep | 120 | có | 0.000 | 1.450 | 0.644 | 0.0000 | 0.0000 | PASS |
| Happy | 45 | — | 0.000 | 1.463 | 0.658 | 0.0000 | 0.0004 | PASS |
| Walk | 32 | có | 0.137 | 1.302 | 0.686 | 0.0000 | 0.0005 | PASS |
| Run | 20 | có | 0.530 | 1.542 | 0.532 | 0.0000 | 0.0000 | PASS |

## DRG_frostfin_Juvenile (SAMPLE)

| Clip | Frame | Loop | m/s | Giãn | Nén | Trượt | Xuyên | Kết quả |
|---|---|---|---|---|---|---|---|---|
| Idle | 120 | có | 0.000 | 1.176 | 0.849 | — | 0.0000 | PASS |
| Eat | 60 | — | 0.000 | 1.207 | 0.620 | — | 0.0000 | PASS |
| Sleep | 120 | có | 0.000 | 1.315 | 0.605 | — | 0.0000 | PASS |
| Happy | 45 | — | 0.000 | 1.442 | 0.719 | — | 0.0000 | PASS |
| Swim | 48 | có | 0.000 | 1.411 | 0.712 | — | — | PASS |

## DRG_leviathan_Adult (SAMPLE)

| Clip | Frame | Loop | m/s | Giãn | Nén | Trượt | Xuyên | Kết quả |
|---|---|---|---|---|---|---|---|---|
| Idle | 120 | có | 0.000 | 1.169 | 0.852 | — | 0.0000 | PASS |
| Eat | 60 | — | 0.000 | 1.201 | 0.705 | — | 0.0000 | PASS |
| Sleep | 120 | có | 0.000 | 1.438 | 0.533 | — | 0.0000 | PASS |
| Happy | 45 | — | 0.000 | 1.463 | 0.710 | — | 0.0000 | PASS |
| Swim | 48 | có | 0.000 | 1.152 | 0.864 | — | — | PASS |

## DRG_sprout_Baby (SAMPLE)

| Clip | Frame | Loop | m/s | Giãn | Nén | Trượt | Xuyên | Kết quả |
|---|---|---|---|---|---|---|---|---|
| Idle | 120 | có | 0.000 | 1.154 | 0.871 | 0.0000 | 0.0001 | PASS |
| Eat | 60 | — | 0.000 | 1.537 | 0.529 | 0.0000 | 0.0000 | PASS |
| Sleep | 120 | có | 0.000 | 1.414 | 0.629 | 0.0000 | 0.0000 | PASS |
| Happy | 45 | — | 0.000 | 1.472 | 0.670 | 0.0000 | 0.0004 | PASS |
| Walk | 32 | có | 0.131 | 1.297 | 0.689 | 0.0000 | 0.0004 | PASS |
| Run | 20 | có | 0.505 | 1.560 | 0.516 | 0.0000 | 0.0000 | PASS |
| Burrow | 60 | có | 0.000 | 1.465 | 0.685 | 0.0000 | 0.0000 | PASS |

## DRG_steamdrake_Juvenile (SAMPLE)

| Clip | Frame | Loop | m/s | Giãn | Nén | Trượt | Xuyên | Kết quả |
|---|---|---|---|---|---|---|---|---|
| Idle | 120 | có | 0.000 | 1.106 | 0.903 | 0.0000 | 0.0000 | PASS |
| Eat | 60 | — | 0.000 | 1.196 | 0.798 | 0.0000 | 0.0000 | PASS |
| Sleep | 120 | có | 0.000 | 1.503 | 0.539 | 0.0000 | 0.0000 | PASS |
| Happy | 45 | — | 0.000 | 1.308 | 0.756 | 0.0000 | 0.0000 | PASS |
| Walk | 40 | có | 0.062 | 1.312 | 0.740 | 0.0063 | 0.0000 | PASS |
| Run | 26 | có | 0.189 | 1.415 | 0.620 | 0.0072 | 0.0000 | PASS |
| Swim | 48 | có | 0.000 | 1.438 | 0.805 | 0.0000 | — | PASS |

## DRG_zephyr_Unstaged (SAMPLE)

| Clip | Frame | Loop | m/s | Giãn | Nén | Trượt | Xuyên | Kết quả |
|---|---|---|---|---|---|---|---|---|
| Idle | 120 | có | 0.000 | 1.137 | 0.884 | 0.0000 | 0.0001 | PASS |
| Eat | 60 | — | 0.000 | 1.508 | 0.563 | 0.0000 | 0.0000 | PASS |
| Sleep | 120 | có | 0.000 | 1.426 | 0.637 | 0.0000 | 0.0000 | PASS |
| Happy | 45 | — | 0.000 | 1.393 | 0.689 | 0.0000 | 0.0005 | PASS |
| Walk | 32 | có | 0.137 | 1.280 | 0.697 | 0.0000 | 0.0005 | PASS |
| Run | 20 | có | 0.528 | 1.504 | 0.531 | 0.0000 | 0.0000 | PASS |
| Fly | 32 | có | 0.000 | 1.326 | 0.777 | 0.0000 | — | PASS |
| TakeOff | 40 | — | 0.000 | 1.428 | 0.545 | 0.0000 | 0.0000 | PASS |
| Glide | 60 | có | 0.000 | 1.208 | 0.824 | 0.0000 | — | PASS |
| Land | 40 | — | 0.000 | 1.561 | 0.543 | 0.0000 | 0.0000 | PASS |
