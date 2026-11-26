# Báo cáo animation

Bộ clip mỗi loài lấy từ khả năng sinh học (`dragon-archetypes.json`); loài không có khả năng thì không có clip, runtime không giả (Fly trên loài không bay → Idle + cảnh báo).

| Model | Bằng chứng | Clip theo sinh học | Clip đã tạo | Thiếu | Thừa | QA |
|---|---|---|---|---|---|---|
| DRG_aurorawing_Adult | SAMPLE | Idle, Eat, Sleep, Happy, Walk, Run, Fly, Rest, Wake, LookAround, Curious, Drink, Stretch, Bask, Greet, Nuzzle | Idle, Eat, Sleep, Happy, Walk, Run, Fly, Rest, Wake, LookAround, Curious, Drink, Stretch, Bask, Greet, Nuzzle, TakeOff, Glide, Land | — | — | PASS |
| DRG_aurorawing_Baby | SAMPLE | Idle, Eat, Sleep, Happy, Walk, Run, Fly, Rest, Wake, LookAround, Curious, Drink, Stretch, Bask, Greet, Nuzzle | Idle, Eat, Sleep, Happy, Walk, Run, Fly, Rest, Wake, LookAround, Curious, Drink, Stretch, Bask, Greet, Nuzzle, TakeOff, Glide, Land | — | — | PASS |
| DRG_aurorawing_Juvenile | SAMPLE | Idle, Eat, Sleep, Happy, Walk, Run, Fly, Rest, Wake, LookAround, Curious, Drink, Stretch, Bask, Greet, Nuzzle | Idle, Eat, Sleep, Happy, Walk, Run, Fly, Rest, Wake, LookAround, Curious, Drink, Stretch, Bask, Greet, Nuzzle, TakeOff, Glide, Land | — | — | PASS |

TakeOff/Glide/Land là clip chuyển tiếp của loài bay (archetype winged), có trong FBX và Animation Lab; state Animator hiện chỉ gồm Idle, Walk, Run, Swim, Fly, Burrow, Sleep + Eat, Happy (enum `DragonLocomotion`).

## DRG_aurorawing_Adult (SAMPLE)

| Clip | Frame | Loop | m/s | Giãn | Nén | Trượt | Xuyên | Kết quả |
|---|---|---|---|---|---|---|---|---|
| Idle | 120 | có | 0.000 | 1.106 | 0.911 | 0.0000 | 0.0001 | PASS |
| Eat | 60 | — | 0.000 | 1.448 | 0.598 | 0.0000 | 0.0000 | PASS |
| Sleep | 120 | có | 0.000 | 1.382 | 0.660 | 0.0000 | 0.0000 | PASS |
| Happy | 45 | — | 0.000 | 1.312 | 0.749 | 0.0000 | 0.0000 | PASS |
| Walk | 25 | có | 0.218 | 1.248 | 0.751 | 0.0001 | 0.0007 | PASS |
| Run | 14 | có | 0.940 | 1.474 | 0.623 | 0.0001 | 0.0000 | PASS |
| Fly | 32 | có | 0.659 | 1.308 | 0.830 | 0.0000 | — | PASS |
| Rest | 120 | có | 0.000 | 1.278 | 0.714 | 0.0000 | 0.0000 | PASS |
| Wake | 60 | — | 0.000 | 1.295 | 0.681 | 0.0000 | 0.0000 | PASS |
| LookAround | 90 | — | 0.000 | 1.275 | 0.794 | 0.0000 | 0.0001 | PASS |
| Curious | 75 | — | 0.000 | 1.216 | 0.811 | 0.0000 | 0.0000 | PASS |
| Drink | 75 | — | 0.000 | 1.517 | 0.573 | 0.0000 | 0.0000 | PASS |
| Stretch | 90 | — | 0.000 | 1.282 | 0.763 | 0.0000 | 0.0000 | PASS |
| Bask | 120 | có | 0.000 | 1.139 | 0.712 | 0.0000 | 0.0000 | PASS |
| Greet | 60 | — | 0.000 | 1.111 | 0.895 | 0.0000 | 0.0000 | PASS |
| Nuzzle | 75 | — | 0.000 | 1.108 | 0.892 | 0.0000 | 0.0000 | PASS |
| TakeOff | 40 | — | 0.000 | 1.284 | 0.733 | 0.0000 | 0.0072 | PASS |
| Glide | 60 | có | 0.000 | 1.193 | 0.830 | 0.0000 | — | PASS |
| Land | 40 | — | 0.000 | 1.352 | 0.571 | 0.0000 | 0.0095 | PASS |

## DRG_aurorawing_Baby (SAMPLE)

| Clip | Frame | Loop | m/s | Giãn | Nén | Trượt | Xuyên | Kết quả |
|---|---|---|---|---|---|---|---|---|
| Idle | 120 | có | 0.000 | 1.112 | 0.894 | 0.0000 | 0.0001 | PASS |
| Eat | 60 | — | 0.000 | 1.486 | 0.550 | 0.0000 | 0.0000 | PASS |
| Sleep | 120 | có | 0.000 | 1.465 | 0.659 | 0.0000 | 0.0000 | PASS |
| Happy | 45 | — | 0.000 | 1.302 | 0.727 | 0.0000 | 0.0000 | PASS |
| Walk | 17 | có | 0.145 | 1.235 | 0.772 | 0.0002 | 0.0005 | PASS |
| Run | 10 | có | 0.596 | 1.421 | 0.628 | 0.0000 | 0.0000 | PASS |
| Fly | 32 | có | 0.103 | 1.225 | 0.838 | 0.0000 | — | PASS |
| Rest | 120 | có | 0.000 | 1.221 | 0.682 | 0.0000 | 0.0000 | PASS |
| Wake | 60 | — | 0.000 | 1.315 | 0.676 | 0.0000 | 0.0000 | PASS |
| LookAround | 90 | — | 0.000 | 1.313 | 0.763 | 0.0000 | 0.0001 | PASS |
| Curious | 75 | — | 0.000 | 1.265 | 0.806 | 0.0000 | 0.0011 | PASS |
| Drink | 75 | — | 0.000 | 1.555 | 0.515 | 0.0000 | 0.0000 | PASS |
| Stretch | 90 | — | 0.000 | 1.325 | 0.765 | 0.0000 | 0.0000 | PASS |
| Bask | 120 | có | 0.000 | 1.137 | 0.682 | 0.0000 | 0.0000 | PASS |
| Greet | 60 | — | 0.000 | 1.120 | 0.858 | 0.0000 | 0.0000 | PASS |
| Nuzzle | 75 | — | 0.000 | 1.119 | 0.830 | 0.0000 | 0.0000 | PASS |
| TakeOff | 40 | — | 0.000 | 1.241 | 0.747 | 0.0000 | 0.0060 | PASS |
| Glide | 60 | có | 0.000 | 1.171 | 0.853 | 0.0000 | — | PASS |
| Land | 40 | — | 0.000 | 1.511 | 0.652 | 0.0000 | 0.0081 | PASS |

## DRG_aurorawing_Juvenile (SAMPLE)

| Clip | Frame | Loop | m/s | Giãn | Nén | Trượt | Xuyên | Kết quả |
|---|---|---|---|---|---|---|---|---|
| Idle | 120 | có | 0.000 | 1.100 | 0.907 | 0.0000 | 0.0001 | PASS |
| Eat | 60 | — | 0.000 | 1.419 | 0.599 | 0.0000 | 0.0000 | PASS |
| Sleep | 120 | có | 0.000 | 1.411 | 0.674 | 0.0000 | 0.0000 | PASS |
| Happy | 45 | — | 0.000 | 1.272 | 0.743 | 0.0000 | 0.0000 | PASS |
| Walk | 21 | có | 0.180 | 1.239 | 0.764 | 0.0000 | 0.0005 | PASS |
| Run | 12 | có | 0.761 | 1.457 | 0.632 | 0.0000 | 0.0000 | PASS |
| Fly | 32 | có | 0.871 | 1.297 | 0.776 | 0.0000 | — | PASS |
| Rest | 120 | có | 0.000 | 1.259 | 0.693 | 0.0000 | 0.0000 | PASS |
| Wake | 60 | — | 0.000 | 1.286 | 0.686 | 0.0000 | 0.0000 | PASS |
| LookAround | 90 | — | 0.000 | 1.280 | 0.776 | 0.0000 | 0.0000 | PASS |
| Curious | 75 | — | 0.000 | 1.229 | 0.818 | 0.0000 | 0.0000 | PASS |
| Drink | 75 | — | 0.000 | 1.485 | 0.567 | 0.0000 | 0.0000 | PASS |
| Stretch | 90 | — | 0.000 | 1.246 | 0.764 | 0.0000 | 0.0000 | PASS |
| Bask | 120 | có | 0.000 | 1.165 | 0.678 | 0.0000 | 0.0000 | PASS |
| Greet | 60 | — | 0.000 | 1.103 | 0.891 | 0.0000 | 0.0000 | PASS |
| Nuzzle | 75 | — | 0.000 | 1.095 | 0.883 | 0.0000 | 0.0000 | PASS |
| TakeOff | 40 | — | 0.000 | 1.294 | 0.708 | 0.0000 | 0.0068 | PASS |
| Glide | 60 | có | 0.000 | 1.142 | 0.840 | 0.0000 | — | PASS |
| Land | 40 | — | 0.000 | 1.359 | 0.584 | 0.0000 | 0.0090 | PASS |
