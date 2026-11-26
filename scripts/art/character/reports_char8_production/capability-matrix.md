# Species × Stage × Capability Matrix (U1-CHAR.7)

Khả năng lấy từ bộ clip canonical (`dragon-archetypes.json`), giống nhau ở mọi stage của một loài; runtime đọc đúng clip của model đang hiện (`DragonMotionCapabilities.From`). Cột stage cho biết model nào đang có; ô trống = catalog dùng stage khác (có cảnh báo). Model SAMPLE không phải asset thật.

| Loài | Locomotion | Ground | Water | Water rest | Surface | Water entry/exit | Air | Perch | Underground | Greet | Baby | Juvenile | Adult | Unstaged | Thiếu dữ liệu |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| emberling | walk | ✓ | — | — | — | — | — | — | — | ✓ |  |  |  | thực tế (chưa nghiệm thu) | — |
| droplet | walk, swim | ✓ | ✓ | ✓ | ✓ | ✓ | — | — | — | ✓ |  |  |  | thực tế (chưa nghiệm thu) | limbCount |
| sprout | walk, burrow | ✓ | — | — | — | — | — | — | ✓ | ✓ |  |  |  | thực tế (chưa nghiệm thu) | — |
| zephyr | walk, fly | ✓ | — | — | — | — | ✓ | ✓ | — | ✓ |  |  |  | thực tế (chưa nghiệm thu) | limbCount |
| steamdrake | walk, swim | ✓ | ✓ | ✓ | ✓ | ✓ | — | — | — | ✓ |  |  |  | thực tế (chưa nghiệm thu) | limbCount |
| magma | walk, burrow | ✓ | — | — | — | — | — | — | ✓ | ✓ |  |  |  | thực tế (chưa nghiệm thu) | limbCount |
| sparkwing | walk, fly | ✓ | — | — | — | — | ✓ | — | — | ✓ |  |  |  | thực tế (chưa nghiệm thu) | limbCount |
| frostfin | swim | — | ✓ | ✓ | — | — | — | — | — | ✓ |  |  |  | thực tế (chưa nghiệm thu) | — |
| mossback | walk, swim | ✓ | ✓ | ✓ | ✓ | ✓ | — | — | — | ✓ |  |  |  | thực tế (chưa nghiệm thu) | limbCount |
| blossom | walk, fly | ✓ | — | — | — | — | ✓ | ✓ | — | ✓ |  |  |  | thực tế (chưa nghiệm thu) | limbCount |
| solaris | walk, fly | ✓ | — | — | — | — | ✓ | — | — | ✓ |  |  |  | thực tế (chưa nghiệm thu) | limbCount |
| leviathan | swim | — | ✓ | ✓ | — | — | — | — | — | ✓ |  |  |  | thực tế (chưa nghiệm thu) | — |
| amberhorn | walk, burrow | ✓ | — | — | — | — | — | — | ✓ | ✓ |  |  |  | thực tế (chưa nghiệm thu) | — |
| aurorawing | walk, fly | ✓ | — | — | — | — | ✓ | — | — | ✓ |  |  |  | thực tế (chưa nghiệm thu) | limbCount |
| celestia | walk, fly | ✓ | — | — | — | — | ✓ | — | — | ✓ |  |  |  | thực tế (chưa nghiệm thu) | limbCount |
| gaia | walk, swim | ✓ | ✓ | ✓ | ✓ | ✓ | — | — | — | ✓ |  |  |  | thực tế (chưa nghiệm thu) | limbCount |

Loài chỉ bơi (Frostfin, Leviathan) có cột Ground là —: mọi hành vi của chúng diễn ra trong nước.
