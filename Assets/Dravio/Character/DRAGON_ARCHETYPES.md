# Archetype rig và clip của 16 loài (tự sinh – không sửa tay)

Sinh bởi `scripts/art/character/build_archetypes.py` từ spec loài canonical. `requiresInput: limbCount` nghĩa là loài có chân đi (canonical) nhưng dữ liệu không nói số chân; bước rig sẽ dừng cho tới khi art director/GDD điền giá trị vào `asset-overrides.json`.

| Loài | Archetype | Lý do | Clip | Số chân | Đặc điểm (từ silhouette) | Cần bổ sung |
|---|---|---|---|---|---|---|
| `emberling` | quadruped | walks; silhouette states "bốn chân" | Idle, Eat, Sleep, Happy, Walk, Run, Rest, Wake, LookAround, Curious, Drink, Stretch, Bask, Greet, Nuzzle | 4 | flame_tail | — |
| `droplet` | walker | walks; limb count not stated (quadruped template once limbCount=4 is supplied) | Idle, Eat, Sleep, Happy, Walk, Run, Swim, Rest, Wake, LookAround, Curious, Drink, Stretch, Bask, Greet, SwimIdle, SwimRest, SwimSurface, WaterEnter, WaterExit, Nuzzle | chưa nêu | fins, webbed_feet, flat_tail, ear_fins | limbCount |
| `sprout` | quadruped | walks; silhouette states "bốn chân" | Idle, Eat, Sleep, Happy, Walk, Run, Burrow, Rest, Wake, LookAround, Curious, Drink, Stretch, Bask, Greet, DigPrep, BurrowEnter, BurrowEmerge, Nuzzle | 4 | leaf_sprout, leaf_tail | — |
| `zephyr` | winged | locomotion contains "fly" | Idle, Eat, Sleep, Happy, Walk, Run, Fly, Rest, Wake, LookAround, Curious, Drink, Stretch, Bask, Greet, Perch, Nuzzle | chưa nêu | wings, feathers_or_fur, cloud_tail, perching_feet | limbCount |
| `steamdrake` | serpentine | bodyStyle == Serpent | Idle, Eat, Sleep, Happy, Walk, Run, Swim, Rest, Wake, LookAround, Curious, Drink, Stretch, Bask, Greet, SwimIdle, SwimRest, SwimSurface, WaterEnter, WaterExit, Nuzzle | chưa nêu | fins | limbCount |
| `magma` | walker | walks; limb count not stated (quadruped template once limbCount=4 is supplied) | Idle, Eat, Sleep, Happy, Walk, Run, Burrow, Rest, Wake, LookAround, Curious, Drink, Stretch, Bask, Greet, DigPrep, BurrowEnter, BurrowEmerge, Nuzzle | chưa nêu | horns, armor_plates | limbCount |
| `sparkwing` | winged | locomotion contains "fly" | Idle, Eat, Sleep, Happy, Walk, Run, Fly, Rest, Wake, LookAround, Curious, Drink, Stretch, Bask, Greet, Nuzzle | chưa nêu | wings, spark_tail | limbCount |
| `frostfin` | aquatic | locomotion lacks "walk" | Idle, Eat, Sleep, Happy, Swim, Rest, Wake, LookAround, Curious, Greet, SwimIdle, SwimRest, Nuzzle | — | fins | — |
| `mossback` | walker | walks; limb count not stated (quadruped template once limbCount=4 is supplied) | Idle, Eat, Sleep, Happy, Walk, Run, Swim, Rest, Wake, LookAround, Curious, Drink, Stretch, Bask, Greet, SwimIdle, SwimRest, SwimSurface, WaterEnter, WaterExit, Nuzzle | chưa nêu | shell, fins | limbCount |
| `blossom` | winged | locomotion contains "fly" | Idle, Eat, Sleep, Happy, Walk, Run, Fly, Rest, Wake, LookAround, Curious, Drink, Stretch, Bask, Greet, Perch, Nuzzle | chưa nêu | wings, perching_feet | limbCount |
| `solaris` | winged | locomotion contains "fly" | Idle, Eat, Sleep, Happy, Walk, Run, Fly, Rest, Wake, LookAround, Curious, Drink, Stretch, Bask, Greet, Nuzzle | chưa nêu | crest, wings, feathers_or_fur, fan_tail | limbCount |
| `leviathan` | serpentine (dưới nước) | bodyStyle == Serpent | Idle, Eat, Sleep, Happy, Swim, Rest, Wake, LookAround, Curious, Greet, SwimIdle, SwimRest, Nuzzle | — | fins | — |
| `amberhorn` | quadruped | walks; silhouette states "bốn chân" | Idle, Eat, Sleep, Happy, Walk, Run, Burrow, Rest, Wake, LookAround, Curious, Drink, Stretch, Bask, Greet, DigPrep, BurrowEnter, BurrowEmerge, Nuzzle | 4 | horns, armor_plates | — |
| `aurorawing` | winged | locomotion contains "fly" | Idle, Eat, Sleep, Happy, Walk, Run, Fly, Rest, Wake, LookAround, Curious, Drink, Stretch, Bask, Greet, Nuzzle | chưa nêu | wings, feathers_or_fur, aurora_tail | limbCount |
| `celestia` | winged | locomotion contains "fly" | Idle, Eat, Sleep, Happy, Walk, Run, Fly, Rest, Wake, LookAround, Curious, Drink, Stretch, Bask, Greet, Nuzzle | chưa nêu | horns, wings, star_tail | limbCount |
| `gaia` | walker | walks; limb count not stated (quadruped template once limbCount=4 is supplied) | Idle, Eat, Sleep, Happy, Walk, Run, Swim, Rest, Wake, LookAround, Curious, Drink, Stretch, Bask, Greet, SwimIdle, SwimRest, SwimSurface, WaterEnter, WaterExit, Nuzzle | chưa nêu | horns, armor_plates, fins | limbCount |

## Template xương

- **quadruped**: Root, Pelvis, Spine, Chest, Neck, Head, Tail1-3, 4 x (Upper, Lower, Foot), VFX anchors
- **winged**: quadruped body (legs only if limbCount given) + 2 x (WingUpper, WingFore, WingTip)
- **serpentine**: Root, Head, Neck, Spine1-8, Tail1-4 (+ limb chains only if limbCount given)
- **aquatic**: Root, Head, Neck, Spine1-4, Tail1-3, Fin_L/R (pectoral), TailFin
- **walker**: no template until limbCount is supplied; limbCount=4 uses the quadruped template
