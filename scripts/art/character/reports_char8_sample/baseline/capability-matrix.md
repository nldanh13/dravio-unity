# Species × Stage × Capability Matrix (U1-CHAR.7)

Khả năng lấy từ bộ clip canonical (`dragon-archetypes.json`), giống nhau ở mọi stage của một loài; runtime đọc đúng clip của model đang hiện (`DragonMotionCapabilities.From`). Cột stage cho biết model nào đang có; ô trống = catalog dùng stage khác (có cảnh báo). Model SAMPLE không phải asset thật.

| Loài | Locomotion | Ground | Water | Water rest | Surface | Water entry/exit | Air | Perch | Underground | Greet | Baby | Juvenile | Adult | Unstaged | Thiếu dữ liệu |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| emberling | walk | ✓ | — | — | — | — | — | — | — | ✓ | SAMPLE |  |  |  | — |
| droplet | walk, swim | ✓ | ✓ | ✓ | ✓ | ✓ | — | — | — | ✓ | SAMPLE |  |  |  | limbCount |
| sprout | walk, burrow | ✓ | — | — | — | — | — | — | ✓ | ✓ | SAMPLE |  |  |  | — |
| zephyr | walk, fly | ✓ | — | — | — | — | ✓ | ✓ | — | ✓ |  |  |  | SAMPLE | limbCount |
| steamdrake | walk, swim | ✓ | ✓ | ✓ | ✓ | ✓ | — | — | — | ✓ |  | SAMPLE |  |  | limbCount |
| magma | walk, burrow | ✓ | — | — | — | — | — | — | ✓ | ✓ | SAMPLE |  |  |  | limbCount |
| sparkwing | walk, fly | ✓ | — | — | — | — | ✓ | — | — | ✓ |  |  |  |  | limbCount |
| frostfin | swim | — | ✓ | ✓ | — | — | — | — | — | ✓ |  | SAMPLE |  |  | — |
| mossback | walk, swim | ✓ | ✓ | ✓ | ✓ | ✓ | — | — | — | ✓ |  |  |  |  | limbCount |
| blossom | walk, fly | ✓ | — | — | — | — | ✓ | ✓ | — | ✓ |  |  |  |  | limbCount |
| solaris | walk, fly | ✓ | — | — | — | — | ✓ | — | — | ✓ |  |  |  |  | limbCount |
| leviathan | swim | — | ✓ | ✓ | — | — | — | — | — | ✓ |  |  | SAMPLE |  | — |
| amberhorn | walk, burrow | ✓ | — | — | — | — | — | — | ✓ | ✓ |  |  |  |  | — |
| aurorawing | walk, fly | ✓ | — | — | — | — | ✓ | — | — | ✓ |  |  |  |  | limbCount |
| celestia | walk, fly | ✓ | — | — | — | — | ✓ | — | — | ✓ |  |  |  |  | limbCount |
| gaia | walk, swim | ✓ | ✓ | ✓ | ✓ | ✓ | — | — | — | ✓ |  |  |  |  | limbCount |

Loài chỉ bơi (Frostfin, Leviathan) có cột Ground là —: mọi hành vi của chúng diễn ra trong nước.
