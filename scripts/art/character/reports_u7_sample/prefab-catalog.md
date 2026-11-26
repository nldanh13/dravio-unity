# Prefab catalog (DragonVisualCatalog)

Sinh bằng `build_character_reports.py` từ thư mục work của batch. Đường dẫn Unity là nơi **Dravio ▸ Character ▸ Import Batch Manifest** sẽ tạo prefab/controller; catalog thật là `Assets/Dravio/Character/Data/DragonVisualCatalog.asset`. Thứ tự tra: đúng stage → Unstaged → stage gần nhất (có cảnh báo).

| Loài | Stage | Bằng chứng | Rig | Archetype | Clip | Animation QA | LOD tris | Bone | Prefab |
|---|---|---|---|---|---|---|---|---|---|
| droplet | Unstaged | SAMPLE | có | quadruped | Idle, Eat, Sleep, Happy, Walk, Run, Swim, Rest, Wake, LookAround, Curious, Drink, Stretch, Bask, Greet, SwimIdle, SwimRest, SwimSurface, WaterEnter, WaterExit | PASS | 24000/10000/4000 | 28 | `Assets/Dravio/Character/Prefabs/DRG_droplet_Unstaged_Rigged_LODGroup.prefab` |
| emberling | Unstaged | SAMPLE | có | quadruped | Idle, Eat, Sleep, Happy, Walk, Run, Rest, Wake, LookAround, Curious, Drink, Stretch, Bask, Greet | PASS | 24000/10000/4000 | 26 | `Assets/Dravio/Character/Prefabs/DRG_emberling_Unstaged_Rigged_LODGroup.prefab` |
| frostfin | Juvenile | SAMPLE | có | aquatic | Idle, Eat, Sleep, Happy, Swim, Rest, Wake, LookAround, Curious, Greet, SwimIdle, SwimRest | PASS | 24000/10000/4000 | 17 | `Assets/Dravio/Character/Prefabs/DRG_frostfin_Juvenile_Rigged_LODGroup.prefab` |
| leviathan | Adult | SAMPLE | có | serpentine | Idle, Eat, Sleep, Happy, Swim, Rest, Wake, LookAround, Curious, Greet, SwimIdle, SwimRest | PASS | 23998/10000/4000 | 19 | `Assets/Dravio/Character/Prefabs/DRG_leviathan_Adult_Rigged_LODGroup.prefab` |
| magma | Unstaged | SAMPLE | có | quadruped | Idle, Eat, Sleep, Happy, Walk, Run, Burrow, Rest, Wake, LookAround, Curious, Drink, Stretch, Bask, Greet, DigPrep, BurrowEnter, BurrowEmerge | PASS | 24000/10000/4000 | 26 | `Assets/Dravio/Character/Prefabs/DRG_magma_Unstaged_Rigged_LODGroup.prefab` |
| sparkwing | Unstaged | SAMPLE | có | winged | Idle, Eat, Sleep, Happy, Walk, Run, Fly, Rest, Wake, LookAround, Curious, Drink, Stretch, Bask, Greet, TakeOff, Glide, Land | PASS | 24000/10000/4000 | 32 | `Assets/Dravio/Character/Prefabs/DRG_sparkwing_Unstaged_Rigged_LODGroup.prefab` |
| sprout | Unstaged | SAMPLE | có | quadruped | Idle, Eat, Sleep, Happy, Walk, Run, Burrow, Rest, Wake, LookAround, Curious, Drink, Stretch, Bask, Greet, DigPrep, BurrowEnter, BurrowEmerge | PASS | 24000/10000/4000 | 26 | `Assets/Dravio/Character/Prefabs/DRG_sprout_Unstaged_Rigged_LODGroup.prefab` |
| steamdrake | Unstaged | SAMPLE | có | serpentine | Idle, Eat, Sleep, Happy, Walk, Run, Swim, Rest, Wake, LookAround, Curious, Drink, Stretch, Bask, Greet, SwimIdle, SwimRest, SwimSurface, WaterEnter, WaterExit | PASS | 23998/10000/4000 | 31 | `Assets/Dravio/Character/Prefabs/DRG_steamdrake_Unstaged_Rigged_LODGroup.prefab` |
| zephyr | Unstaged | SAMPLE | có | winged | Idle, Eat, Sleep, Happy, Walk, Run, Fly, Rest, Wake, LookAround, Curious, Drink, Stretch, Bask, Greet, Perch, TakeOff, Glide, Land | PASS | 24000/10000/4000 | 32 | `Assets/Dravio/Character/Prefabs/DRG_zephyr_Unstaged_Rigged_LODGroup.prefab` |

## Còn thiếu

- Loài chưa có model (7): mossback, blossom, solaris, amberhorn, aurorawing, celestia, gaia
- Cặp loài/stage dùng fallback stage khác (4): frostfin/Baby, frostfin/Adult, leviathan/Baby, leviathan/Juvenile
