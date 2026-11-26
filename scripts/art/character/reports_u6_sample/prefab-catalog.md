# Prefab catalog (DragonVisualCatalog)

Sinh bằng `build_character_reports.py` từ thư mục work của batch. Đường dẫn Unity là nơi **Dravio ▸ Character ▸ Import Batch Manifest** sẽ tạo prefab/controller; catalog thật là `Assets/Dravio/Character/Data/DragonVisualCatalog.asset`. Thứ tự tra: đúng stage → Unstaged → stage gần nhất (có cảnh báo).

| Loài | Stage | Bằng chứng | Rig | Archetype | Clip | Animation QA | LOD tris | Bone | Prefab |
|---|---|---|---|---|---|---|---|---|---|
| droplet | Baby | SAMPLE | có | quadruped | Idle, Eat, Sleep, Happy, Walk, Run, Swim, Rest, Wake, LookAround, Curious, Drink, Stretch, Bask | PASS | 24000/10000/4000 | 28 | `Assets/Dravio/Character/Prefabs/DRG_droplet_Baby_Rigged_LODGroup.prefab` |
| emberling | Baby | SAMPLE | có | quadruped | Idle, Eat, Sleep, Happy, Walk, Run, Rest, Wake, LookAround, Curious, Drink, Stretch, Bask | PASS | 24000/10000/4000 | 26 | `Assets/Dravio/Character/Prefabs/DRG_emberling_Baby_Rigged_LODGroup.prefab` |
| frostfin | Juvenile | SAMPLE | có | aquatic | Idle, Eat, Sleep, Happy, Swim, Rest, Wake, LookAround, Curious | PASS | 24000/10000/4000 | 17 | `Assets/Dravio/Character/Prefabs/DRG_frostfin_Juvenile_Rigged_LODGroup.prefab` |
| leviathan | Adult | SAMPLE | có | serpentine | Idle, Eat, Sleep, Happy, Swim, Rest, Wake, LookAround, Curious | PASS | 23998/10000/4000 | 19 | `Assets/Dravio/Character/Prefabs/DRG_leviathan_Adult_Rigged_LODGroup.prefab` |
| magma | Baby | SAMPLE | tĩnh | — | — | — | 24000/10000/4000 | — | `Assets/Dravio/Character/Prefabs/DRG_magma_Baby_LODs_LODGroup.prefab` |
| sprout | Baby | SAMPLE | có | quadruped | Idle, Eat, Sleep, Happy, Walk, Run, Burrow, Rest, Wake, LookAround, Curious, Drink, Stretch, Bask | PASS | 24000/10000/4000 | 26 | `Assets/Dravio/Character/Prefabs/DRG_sprout_Baby_Rigged_LODGroup.prefab` |
| steamdrake | Juvenile | SAMPLE | có | serpentine | Idle, Eat, Sleep, Happy, Walk, Run, Swim, Rest, Wake, LookAround, Curious, Drink, Stretch, Bask | PASS | 23998/10000/4000 | 31 | `Assets/Dravio/Character/Prefabs/DRG_steamdrake_Juvenile_Rigged_LODGroup.prefab` |
| zephyr | Unstaged | SAMPLE | có | winged | Idle, Eat, Sleep, Happy, Walk, Run, Fly, Rest, Wake, LookAround, Curious, Drink, Stretch, Bask, TakeOff, Glide, Land | PASS | 24000/10000/4000 | 32 | `Assets/Dravio/Character/Prefabs/DRG_zephyr_Unstaged_Rigged_LODGroup.prefab` |

## Còn thiếu

- Loài chưa có model (8): sparkwing, mossback, blossom, solaris, amberhorn, aurorawing, celestia, gaia
- Cặp loài/stage dùng fallback stage khác (14): emberling/Juvenile, emberling/Adult, droplet/Juvenile, droplet/Adult, sprout/Juvenile, sprout/Adult, steamdrake/Baby, steamdrake/Adult, magma/Juvenile, magma/Adult, frostfin/Baby, frostfin/Adult, leviathan/Baby, leviathan/Juvenile
